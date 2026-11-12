using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Domain;
using EdS.Domain.Combat;
using EdS.Domain.Space;

namespace EdS.Simulation.Engine
{
    /// <summary>
    /// Sous-lot 11.4d : combattants qui arrivent et partent en cours de combat (ARCHITECTURE_MONDE.md, section 4 ; PLAN_LOT_11.md, 11.4).
    /// <list type="bullet">
    /// <item>Un combattant du scénario qui a <c>arrivalMs</c> attend son tick ; au début de ce tick (phase <c>setup</c>, avant les
    /// expirations), il arrive à sa place (<c>combatantJoined</c>), sauf si une emprise solide l'occupe (<c>arrivalRefused</c>,
    /// <c>occupied</c>). Un assaillant arrivé attaque <c>firstAttackMs</c> après son arrivée.</item>
    /// <item>Un départ (commande <c>leave</c>) est noté dans la phase des commandes et prend effet en fin de tick, après les morts du tick
    /// (<c>combatantLeft</c>) : ses états sont retirés (<c>left</c>), ses provocations et protections sur les autres aussi
    /// (<c>sourceLeft</c>). Un combattant mort pendant ce tick ne part pas (<c>leaveCancelled</c>).</item>
    /// <item>Un absent (pas encore arrivé, refusé ou parti) ne coûte rien, n'agit pas, n'est ni ciblé, ni solide, ni compté dans un groupe.
    /// Ses commandes sont refusées (<c>actorAbsent</c>), comme celles qui le visent (<c>targetAbsent</c>).</item>
    /// <item>Pilote : <see cref="Join"/> fait arriver un combattant au tick courant, sans place réservée ; le combat complété
    /// (<see cref="CompletedSetup"/>) le compte parmi les arrivées et se rejoue à l'identique.</item>
    /// </list>
    /// </summary>
    public sealed partial class CombatEngine
    {
        /// <summary>Combattants arrivés par <see cref="Join"/>, dans l'ordre d'arrivée.</summary>
        private readonly List<CombatantSetup> _joined = new List<CombatantSetup>();

        /// <summary>Sous-lot 11.4d : combattants arrivés en cours de combat par <see cref="Join"/>, dans l'ordre.</summary>
        public IReadOnlyList<CombatantSetup> JoinedCombatants => _joined.AsReadOnly();

        /// <summary>
        /// Sous-lot 11.4d : le combat de départ complété des arrivées (<see cref="Join"/>) et des commandes soumises (<see cref="Submit"/>) :
        /// un scénario ordinaire, qui se rejoue à l'identique (en-tête mis à part).
        /// </summary>
        public CombatSetup CompletedSetup() => _setup.Completed(_joined, _submitted);

        // --- Arrivées ----------------------------------------------------------------------------

        private void RunArrivals(long tick)
        {
            while (_queue.TryPeek(tick, CombatPhase.Setup, out Scheduled item))
            {
                Fighter f = ((ArrivalItem)item).Fighter;
                // Sous-lot V2.26 (recyclage des places) : dans une zone, une arrivée exige une place libre (présents vivants au plus
                // combat.limits.maxCombatants) ; sinon refus « full ». Jamais atteint par une zone d'avant, qui n'avait pas plus de
                // combattants que de places.
                bool full = _setup.Zone && LivingPresent() >= _rules.MaxCombatants;
                Fighter? blocker = full ? null : ArrivalBlocker(f);
                AssailantSetup? assailant = blocker is null ? f.Assailant : null;
                long firstTurn = assailant is null ? 0 : checked(tick + assailant.FirstAttackMs / _tickMs);
                bool turn = assailant != null && firstTurn < _durationTicks;
                // Sous-lot V2.26 : une échéance par recharge emportée (personnage admis), réservée avec l'arrivée.
                long carriedCooldowns = !full && blocker is null && f.Character != null ? f.Setup.Carried?.Cooldowns.Count ?? 0 : 0;
                ReserveAndDequeue(Cost.Op + Cost.Line + Cost.Enqueue.If(turn && !full) + carriedCooldowns * Cost.Enqueue);
                Charge(1);
                if (full)
                {
                    f.Presence = Presence.Refused;
                    EmitDiagnostic(tick, CombatPhase.Setup, "arrivalRefused", w => w.Field("combatant", f.Id).Field("reason", "full"));
                    continue;
                }
                if (blocker != null)
                {
                    f.Presence = Presence.Refused;
                    EmitDiagnostic(tick, CombatPhase.Setup, "arrivalRefused", w => w
                        .Field("combatant", f.Id).Field("reason", "occupied").Field("blocker", blocker.Id));
                    continue;
                }
                f.Presence = Presence.Present;
                f.ArrivedTick = tick;
                f.Result.ArrivedTick = tick;
                ApplyCarried(f, tick);   // sous-lot V2.26 : état emporté d'un autre secteur
                Emit(tick, CombatPhase.Setup, "combatantJoined", Provenance.NewRoot($"arrival:{f.Id}", f.Id), w => CombatantFields(w, f));
                AddSubscriptions(f);
                if (turn)
                {
                    f.NextAttackTick = firstTurn;
                    Schedule(firstTurn, CombatPhase.Commands, new AssailantTurn { Fighter = f, Turn = f.NextTurn++ });
                }
            }
        }

        /// <summary>
        /// Sous-lot V2.26 (format 25) : applique l'état emporté (<see cref="CombatantSetup.Carried"/>) d'un combattant admis à ce tick, avant sa
        /// ligne <c>combatantJoined</c> (qui écrit donc ses PV et ressources d'arrivée) : PV, endurance et reprise de sa régénération (cumul
        /// remis à zéro, comme après une dépense), Résonance, Charges, recharges (globale, de chaque compétence avec son échéance en file,
        /// des objets de soin). Validé avec le combat : rien ne dépasse les bornes que le moteur atteint.
        /// </summary>
        private void ApplyCarried(Fighter f, long tick)
        {
            if (f.Setup.Carried is not { } carried) return;
            if (carried.Health is long health && f.MaxHealth != null) f.Health = health;   // écart déjà noté (CarriedHealthShift, CreateFighter)
            if (f.Character is null) return;
            if (carried.Endurance is long endurance) f.Endurance = endurance;
            if (f.Endurance < f.MaxEndurance)
            {
                f.RegenFromTick = checked(tick + carried.EnduranceRegenMs / _tickMs);
                f.RegenGranted = 0;
            }
            f.Resonance = carried.Resonance;
            f.RampartCharges = carried.RampartCharges;
            if (carried.GlobalCooldownMs > 0) f.GlobalCooldownReadyTick = checked(tick + carried.GlobalCooldownMs / _tickMs);
            foreach (KeyValuePair<string, long> cooldown in carried.Cooldowns)
            {
                long ready = checked(tick + cooldown.Value / _tickMs);
                f.CooldownReadyTick[cooldown.Key] = ready;
                Schedule(ready, CombatPhase.Expirations, new CooldownExpiry { Fighter = f, Skill = cooldown.Key });
            }
            if (carried.ConsumableCooldownMs > 0) f.ConsumableReadyTick = checked(tick + carried.ConsumableCooldownMs / _tickMs);
        }

        /// <summary>
        /// Sous-lot V2.26 : état de combat de <paramref name="id"/> à emporter dans un autre secteur, entre deux ticks : PV, endurance, délai
        /// restant avant la reprise de la régénération, Résonance, Charges, recharges restantes (comptées à partir du prochain tick, celui de
        /// l'arrivée de l'autre côté). Un champ plein ou prêt est omis. Les charges restantes des objets de soin : <see cref="CarriedConsumables"/>.
        /// </summary>
        /// <exception cref="ArgumentException">Combattant inconnu, mort ou jamais entré.</exception>
        public CarriedState Carried(string id)
        {
            if (id is null || !_byId.TryGetValue(id, out Fighter? f)) throw new ArgumentException($"Combattant inconnu : {id}.", nameof(id));
            if (f.IsDead || f.Presence == Presence.Pending || f.Presence == Presence.Refused)
                throw new ArgumentException($"{id} mort ou jamais entré : rien à emporter.", nameof(id));
            long next = IsFinished ? checked(CurrentTick + 1) : CurrentTick;
            long Remaining(long ready) => ready > next ? checked((ready - next) * _tickMs) : 0;
            long? health = f.Health is long hp && f.MaxHealth is long max && hp < max ? hp : (long?)null;
            if (f.Character is null) return new CarriedState(health);
            var cooldowns = new List<KeyValuePair<string, long>>();
            foreach (KeyValuePair<string, long> c in f.CooldownReadyTick.OrderBy(c => c.Key, StringComparer.Ordinal))
                if (Remaining(c.Value) > 0) cooldowns.Add(new KeyValuePair<string, long>(c.Key, Remaining(c.Value)));
            bool tired = f.Endurance < f.MaxEndurance;
            bool hasConsumables = f.ConsumableCharges.Any(c => c.Value > 0);
            return new CarriedState(health, tired ? f.Endurance : (long?)null,
                tired && f.RegenFromTick is long regen ? Remaining(regen) : 0, f.Resonance, f.RampartCharges,
                Remaining(f.GlobalCooldownReadyTick), cooldowns, hasConsumables ? Remaining(f.ConsumableReadyTick) : 0);
        }

        /// <summary>
        /// Sous-lot V2.26 : objets de soin de <paramref name="id"/> (personnage) avec leurs charges restantes, dans l'ordre de son combat ; un
        /// objet vide n'est plus emporté.
        /// </summary>
        public IReadOnlyList<ConsumableSetup> CarriedConsumables(string id)
        {
            if (id is null || !_byId.TryGetValue(id, out Fighter? f)) throw new ArgumentException($"Combattant inconnu : {id}.", nameof(id));
            if (f.Character is null) return Array.Empty<ConsumableSetup>();
            return f.Character.Consumables.Where(c => f.ConsumableCharges[c.Id] > 0)
                .Select(c => new ConsumableSetup(c.Id, c.Heal, f.ConsumableCharges[c.Id], c.CooldownMs)).ToArray();
        }

        /// <summary>Sous-lot V2.26 : combattants présents et vivants (places prises).</summary>
        private int LivingPresent()
        {
            int n = 0;
            foreach (Fighter f in _fighters)
                if (!f.Out) n++;
            return n;
        }

        /// <summary>
        /// Sous-lot V2.26 (recyclage des places) : places prises dans une zone : combattants présents vivants et arrivées encore attendues.
        /// Un mort ou un parti libère la sienne. Dans un combat fermé : toutes les identités, comme avant.
        /// </summary>
        public int PlacesTaken
        {
            get
            {
                if (!_setup.Zone) return _fighters.Count;
                int n = 0;
                foreach (Fighter f in _fighters)
                    if (f.Presence == Presence.Pending || !f.Out) n++;
                return n;
            }
        }

        /// <summary>Sous-lot V2.26 : places encore libres (au moins 0) ; une arrivée pilotée (<see cref="Join"/>) en exige une.</summary>
        public int FreePlaces => Math.Max(0, _rules.MaxCombatants - PlacesTaken);

        /// <summary>Sous-lot V2.26 : identités encore possibles dans la session (zone : plafond technique ; combat fermé : places).</summary>
        public int IdentitiesLeft => Math.Max(0, (_setup.Zone ? CombatRules.MaxSessionCombatants : _rules.MaxCombatants) - _fighters.Count);

        /// <summary>
        /// Premier combattant présent et vivant (ordre des identifiants) dont l'emprise, solide pour l'arrivant (règle de contact),
        /// chevauche sa place ; null si la place est libre ou hors d'un combat positionné. Le décor et l'aire sont vérifiés par la validation.
        /// </summary>
        private Fighter? ArrivalBlocker(Fighter f)
        {
            if (!_spatial) return null;
            int index = _fightersById.IndexOf(f);
            Body b = f.Body!;
            for (int j = 0; j < _fightersById.Count; j++)
            {
                Fighter other = _fightersById[j];
                if (j == index || other.Out || !Solid(index, j)) continue;
                Body o = other.Body!;
                if (Planar.CirclesOverlap(b.X, b.Z, b.Radius, o.X, o.Z, o.Radius)) return other;
            }
            return null;
        }

        // --- Départs -----------------------------------------------------------------------------

        /// <summary>
        /// Départs notés pendant le tick, par identifiant : réserve leur coût exact (passage, ligne, retrait de chaque état et de chaque
        /// provocation ou protection dont il est la source), puis rend le combattant absent. Un mort du tick reste (<c>leaveCancelled</c>).
        /// </summary>
        private void RunDepartures(long tick)
        {
            foreach (Fighter f in _fightersById)
            {
                if (f.LeaveCommand is not int command) continue;
                if (f.IsDead)
                {
                    Reserve(Cost.Line);
                    f.LeaveCommand = null;
                    EmitDiagnostic(tick, CombatPhase.EndOfTick, "leaveCancelled", w => w.Field("combatant", f.Id).Field("command", command).Field("reason", "actorDead"));
                    continue;
                }
                List<StatusInstance> bound = BoundTo(f);
                Reserve(Cost.Op + Cost.Line + checked(f.Statuses.Count + bound.Count) * (Cost.Op + Observable(TriggerEvent.StateRemoved)));
                Charge(1);
                f.LeaveCommand = null;
                f.Presence = Presence.Left;
                f.LeftTick = tick;
                f.Result.LeftTick = tick;
                f.Guarding = false;
                f.ParryOpen = false;
                f.NextAttackTick = null;   // ses tours encore en file sont périmés (AssailantAttacks)
                if (f.Body != null) f.Body.Moving = false;
                Provenance root = Provenance.NewRoot($"command:{command}", f.Id);
                long left = Emit(tick, CombatPhase.EndOfTick, "combatantLeft", root, w =>
                {
                    w.Field("combatant", f.Id).Field("command", command);
                    if (f.Body is { } body) WriteVector(w, "position", body.X, body.Z);
                });
                Provenance cause = root.CausedBy(left);
                // Comme à la mort (M02) : ses états, dans l'ordre ordinal de l'altération puis de la source ; leurs échéances deviennent
                // périmées et aucun état n'est accordé sur un retrait.
                foreach (StatusInstance status in f.Statuses.Values
                    .OrderBy(x => x.State, StringComparer.Ordinal).ThenBy(x => x.Source ?? string.Empty, StringComparer.Ordinal).ToList())
                {
                    f.Statuses.Remove(status.Key);
                    Charge(1);
                    Emit(tick, CombatPhase.EndOfTick, "stateRemoved", cause, StateObserved(TriggerEvent.StateRemoved, f, status),
                        w => StateFields(w, f, status).Field("reason", "left"));
                }
                foreach (StatusInstance status in bound)
                {
                    status.Owner.Statuses.Remove(status.Key);
                    Charge(1);
                    ResumeAfterEndOfTickRemoval(status.Owner, status, tick);
                    Emit(tick, CombatPhase.EndOfTick, "stateRemoved", cause, StateObserved(TriggerEvent.StateRemoved, status.Owner, status),
                        w => StateFields(w, status.Owner, status).Field("reason", "sourceLeft"));
                }
            }
        }

        /// <summary>Présence écrite sur <c>combatantSummary</c> (format 13) ; null pour un combattant présent du début à la fin.</summary>
        private static string? PresenceId(Fighter f) => f.Presence switch
        {
            Presence.Left => "left",
            Presence.Pending => "pending",
            Presence.Refused => "refused",
            _ => f.ArrivedTick != null ? "joined" : null,
        };

        // --- Pilote ------------------------------------------------------------------------------

        /// <summary>
        /// Sous-lot 11.4d : fait arriver <paramref name="combatant"/> au tick courant (avant qu'il soit simulé), sans place réservée d'avance :
        /// son <c>arrivalMs</c> doit être l'instant du tick courant et son identifiant nouveau. Le combat complété de cette arrivée
        /// (<see cref="CompletedSetup"/>) doit être valide (mêmes contrôles qu'un scénario, majorant de file et précontrôle numérique
        /// compris), sinon rien ne change. L'arrivée a lieu au début du tick, comme celle d'un scénario : refusée (<c>arrivalRefused</c>) si
        /// une emprise solide occupe la place à ce moment-là. Des commandes peuvent être soumises pour lui dès ce tick.
        /// </summary>
        /// <exception cref="ArgumentException">Combattant invalide, identifiant déjà pris, instant d'arrivée autre que le tick courant, ou
        /// combat complété refusé par la validation (premier motif dans le message).</exception>
        public void Join(CombatantSetup combatant)
        {
            if (combatant is null) throw new ArgumentNullException(nameof(combatant));
            if (IsFinished) throw new InvalidOperationException("Combat terminé : plus aucune arrivée.");
            if (_byId.ContainsKey(combatant.Id)) throw new ArgumentException($"Identifiant déjà pris : {combatant.Id}.", nameof(combatant));
            if (combatant.ArrivalMs != checked(CurrentTick * _tickMs))
                throw new ArgumentException($"Arrivée à {combatant.ArrivalMs?.ToString() ?? "null"} ms : seul le tick courant ({CurrentTick}, {CurrentTick * _tickMs} ms) est admis.", nameof(combatant));
            // Sous-lot V2.26 : une place libre (présents vivants et arrivées attendues), refus atomique sinon.
            if (_setup.Zone && FreePlaces == 0)
                throw new ArgumentException($"Arrivée refusée : plus de place libre ({_rules.MaxCombatants} places prises, combat.limits.maxCombatants).", nameof(combatant));
            CombatSetup completed = _setup.Completed(_joined.Concat(new[] { combatant }), _submitted);
            IReadOnlyList<RuleViolation> violations = completed.Validate();
            if (violations.Count > 0)
                throw new ArgumentException($"Arrivée refusée : {string.Join(" ; ", violations.Take(3).Select(v => $"{string.Join(", ", v.Fields)} : {v.Message}"))}", nameof(combatant));
            if (_queue.Count >= _rules.MaxPendingEvents)
                throw new InvalidOperationException($"File pleine : capacité {_rules.MaxPendingEvents} (combat.limits.maxPendingEvents).");

            Fighter f = CreateFighter(combatant);
            _fighters.Add(f);
            _byId[f.Id] = f;
            int at = _fightersById.FindIndex(x => string.CompareOrdinal(x.Id, f.Id) > 0);
            _fightersById.Insert(at < 0 ? _fightersById.Count : at, f);
            _fightersByInitiative = _fighters.OrderByDescending(x => x.Setup.Initiative).ThenBy(x => x.Id, StringComparer.Ordinal).ToList();
            f.Triggers = PermanentSlots(f);
            _joined.Add(combatant);
            _queue.Enqueue(CurrentTick, CombatPhase.Setup, new ArrivalItem { Fighter = f });
            if (_queue.Count > _peakPending) _peakPending = _queue.Count;
        }
    }
}
