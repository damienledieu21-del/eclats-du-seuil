using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;

namespace EdS.Simulation.Engine
{
    /// <summary>Nature d'un combattant vue par un pilote (sous-lot 8.6).</summary>
    public enum CombatantKind
    {
        Character = 0,
        Dummy = 1,
        Assailant = 2,
    }

    /// <summary>
    /// Sous-lot 8.6 : instantané en lecture seule d'un combattant au début du tick courant, avant ses expirations et ses commandes, pour
    /// un pilote de mesure (<c>EdS.Simulation.Pilot</c>). Une altération qui expire à ce tick n'y figure plus ; une fenêtre de parade qui
    /// se ferme à ce tick non plus. Aucune référence à l'état mutable du moteur n'est exposée.
    /// </summary>
    public sealed class CombatantView
    {
        public string Id { get; internal set; } = string.Empty;
        public string Team { get; internal set; } = string.Empty;
        public CombatantKind Kind { get; internal set; }
        public bool IsDead { get; internal set; }

        /// <summary>
        /// Sous-lot 11.4d : présent dans le combat (arrivé et pas parti). Un absent n'agit pas et n'est pas ciblé : un affichage ne le montre
        /// pas. <see cref="ArrivalTick"/> : tick d'arrivée attendu ou passé d'un combattant qui arrive en cours de route ; null sinon.
        /// </summary>
        public bool IsPresent { get; internal set; } = true;
        public long? ArrivalTick { get; internal set; }
        public bool HasLeft { get; internal set; }

        /// <summary>PV ; null pour un mannequin ou un assaillant aux PV infinis.</summary>
        public long? Health { get; internal set; }
        public long? MaxHealth { get; internal set; }

        // Personnage seulement (valeurs neutres sinon).
        public CharacterSetup? Character { get; internal set; }
        public CombatantRules? Rules { get; internal set; }

        /// <summary>
        /// Sous-lot V1.8d : bonus d'équipement en vigueur au début du tick (celui de l'en-tête, puis du dernier bonus modifié accepté) ; null
        /// hors personnage.
        /// </summary>
        public Domain.Stats.GearBonus? Gear { get; internal set; }
        public long Endurance { get; internal set; }
        public long MaxEndurance { get; internal set; }
        public int Resonance { get; internal set; }
        public int RampartCharges { get; internal set; }
        public bool Guarding { get; internal set; }

        /// <summary>Fenêtre de parade encore ouverte à ce tick (elle se ferme à <c>ParryEndTick</c>).</summary>
        public bool ParryOpen { get; internal set; }
        public long ParryRecoveryUntilTick { get; internal set; }
        public long GlobalCooldownReadyTick { get; internal set; }
        public IReadOnlyDictionary<string, long> CooldownReadyTicks { get; internal set; } = new Dictionary<string, long>();

        /// <summary>Sous-lot 12.6 : charges restantes de chaque objet de soin emporté ; recharge partagée (tick de disponibilité).</summary>
        public IReadOnlyDictionary<string, int> ConsumableCharges { get; internal set; } = new Dictionary<string, int>();
        public long ConsumableReadyTick { get; internal set; }

        /// <summary>Compétences, garde et parade non bloquées par une altération encore active à ce tick ; faux pour un absent ou un mort.</summary>
        public bool CanCast { get; internal set; }
        public bool CanGuard { get; internal set; }
        public bool CanParry { get; internal set; }

        /// <summary>Altérations actives à ce tick (identifiants, ordre ordinal, sans doublon).</summary>
        public IReadOnlyList<string> States { get; internal set; } = Array.Empty<string>();

        // Assaillant seulement.
        public AssailantSetup? Assailant { get; internal set; }

        /// <summary>Tick du prochain tour programmé ; null s'il n'y en a plus avant la fin.</summary>
        public long? NextAttackTick { get; internal set; }
        public AssailantAttack? NextAttack { get; internal set; }

        /// <summary>Cible actuelle de ses attaques à cible unique (provocation la plus récente comprise).</summary>
        public string? CurrentTarget { get; internal set; }

        /// <summary>
        /// Sous-lot 12.1 : ennemi à comportement engagé (il poursuit sa cible) ; vrai aussi pour un assaillant sans comportement, qui attaque
        /// d'emblée ; faux pour un personnage ou un mannequin. Sert au monde pour reconnaître les rencontres en cours.
        /// </summary>
        public bool Engaged { get; internal set; }

        /// <summary>
        /// Sous-lot V2.4 (bêtes qui se battent) : un personnage l'a frappé (impact ou tick de DOT) dans cette vie. Le monde ne compte une
        /// créature vaincue (expérience, butin, objectifs) que si un personnage l'a frappée.
        /// </summary>
        public bool HurtByCharacter { get; internal set; }

        /// <summary>Sous-lot V2.4 : bête qui fuit (cerf, lièvre : comportement <c>flee</c>).</summary>
        public bool Flees { get; internal set; }

        /// <summary>Ses attaques sont bloquées à ce tick (contrôle actif).</summary>
        public bool AttacksBlocked { get; internal set; }

        // Sous-lot 11.4 : combat positionné seulement (null sinon).

        /// <summary>Centre de l'emprise au début du tick.</summary>
        public GroundVector? Position { get; internal set; }

        /// <summary>Orientation (vecteur unitaire à 10⁻⁴ près).</summary>
        public GroundVector? Facing { get; internal set; }

        /// <summary>Rayon de l'emprise au sol.</summary>
        public Fixed? Radius { get; internal set; }

        /// <summary>Direction de marche en cours (unitaire) ; null à l'arrêt.</summary>
        public GroundVector? MoveDirection { get; internal set; }

        /// <summary>Sous-lot 11.9 : marche en cours à reculons (l'orientation ne suit pas la marche) ; faux à l'arrêt.</summary>
        public bool MoveBackward { get; internal set; }

        /// <summary>Déplacement non bloqué par une altération encore active à ce tick (l'étourdissement immobilise) ; faux hors combat positionné.</summary>
        public bool CanMove { get; internal set; }

        /// <summary>Sous-lot 11.4c : cible sélectionnée (commande <c>select</c>), vers laquelle le personnage se tourne en garde ; null sinon.</summary>
        public string? SelectedTarget { get; internal set; }

        /// <summary>Tick de disponibilité d'une compétence (0 si jamais lancée).</summary>
        public long CooldownReadyTick(string skill) => CooldownReadyTicks.TryGetValue(skill, out long tick) ? tick : 0;

        /// <summary>
        /// Sous-lot 9.2 : compteurs du combat jusqu'au tick précédent (mêmes valeurs que <see cref="CombatantResult"/> en fin de combat) :
        /// l'affichage en tire ses retours (dégâts, blocage, parade, rupture, refus) par différence d'un tick à l'autre.
        /// </summary>
        public CombatantCounters Counters { get; internal set; } = new CombatantCounters();
    }

    /// <summary>Sous-lot 9.2 : compteurs d'un combattant en cours de combat (copie, aucune référence à l'état du moteur).</summary>
    public sealed class CombatantCounters
    {
        public long DamageDealt { get; internal set; }
        public long DamageTaken { get; internal set; }
        public long DamageAbsorbed { get; internal set; }
        public long DamageMitigated { get; internal set; }
        public long Hits { get; internal set; }
        public long CriticalHits { get; internal set; }
        public long SkillsCast { get; internal set; }
        public long UltimatesCast { get; internal set; }
        public long CommandsRejected { get; internal set; }
        public long Blocks { get; internal set; }
        public long PerfectParries { get; internal set; }
        public long FailedParries { get; internal set; }
        public long GuardBreaks { get; internal set; }
    }

    public sealed partial class CombatEngine
    {
        private readonly Dictionary<(string Actor, long Tick), int> _commandsPerActorTick = new Dictionary<(string, long), int>();
        private readonly List<CombatCommand> _submitted = new List<CombatCommand>();
        private int _commandCount;
        private long _initialEvents = -1;

        /// <summary>Identifiants des combattants, dans l'ordre du scénario.</summary>
        public IReadOnlyList<string> CombatantIds => _fighters.Select(f => f.Id).ToArray();

        /// <summary>Durée du tick en millisecondes.</summary>
        public long TickMilliseconds => _tickMs;

        /// <summary>Description du combat simulé.</summary>
        public CombatSetup Setup => _setup;

        /// <summary>Sous-lot 8.6 : commandes soumises pendant la simulation (<see cref="Submit"/>), dans l'ordre de soumission.</summary>
        public IReadOnlyList<CombatCommand> SubmittedCommands => _submitted.AsReadOnly();

        private void CountSubmitted(CombatCommand command)
        {
            var key = (command.Actor, command.AtMs / _tickMs);
            _commandsPerActorTick[key] = _commandsPerActorTick.TryGetValue(key, out int n) ? n + 1 : 1;
            _commandCount++;
        }

        /// <summary>Vue en lecture seule d'un combattant au début du tick courant (sous-lot 8.6).</summary>
        public CombatantView View(string id)
        {
            if (!_byId.TryGetValue(id ?? throw new ArgumentNullException(nameof(id)), out Fighter? f))
                throw new ArgumentException($"Combattant inconnu : {id}.", nameof(id));
            long tick = CurrentTick;
            bool Blocked(StatusBlocks action) => f.Statuses.Values.Any(s => (s.Definition.Blocks & action) != 0 && s.UntilTick > tick);
            var view = new CombatantView
            {
                Id = f.Id,
                Team = f.Team,
                Kind = f.Character != null ? CombatantKind.Character : f.Assailant != null ? CombatantKind.Assailant : CombatantKind.Dummy,
                IsDead = f.IsDead,
                HurtByCharacter = f.HurtByCharacter,   // sous-lot V2.4
                IsPresent = !f.Absent,
                ArrivalTick = f.Setup.ArrivalMs is long arrivalMs ? arrivalMs / _tickMs : (long?)null,
                HasLeft = f.Presence == Presence.Left,
                Health = f.Health,
                MaxHealth = f.MaxHealth,
                States = f.Statuses.Values.Where(s => s.UntilTick > tick).Select(s => s.State).Distinct().OrderBy(s => s, StringComparer.Ordinal).ToArray(),
                Counters = new CombatantCounters
                {
                    DamageDealt = f.Result.DamageDealt,
                    DamageTaken = f.Result.DamageTaken,
                    DamageAbsorbed = f.Result.DamageAbsorbed,
                    DamageMitigated = f.Result.DamageMitigated,
                    Hits = f.Result.Hits,
                    CriticalHits = f.Result.CriticalHits,
                    SkillsCast = f.Result.SkillsCast,
                    UltimatesCast = f.Result.UltimatesCast,
                    CommandsRejected = f.Result.CommandsRejected,
                    Blocks = f.Result.Blocks,
                    PerfectParries = f.Result.PerfectParries,
                    FailedParries = f.Result.FailedParries,
                    GuardBreaks = f.Result.GuardBreaks,
                },
            };
            if (f.Character != null)
            {
                view.Character = f.Character;
                view.Rules = f.Rules;
                view.Gear = f.Gear;   // sous-lot V1.8d
                view.Endurance = f.Endurance;
                view.MaxEndurance = f.MaxEndurance;
                view.Resonance = f.Resonance;
                view.RampartCharges = f.RampartCharges;
                view.Guarding = f.Guarding;
                view.ParryOpen = f.ParryOpen && f.ParryEndTick > tick;
                view.ParryRecoveryUntilTick = f.ParryRecoveryUntilTick;
                view.GlobalCooldownReadyTick = f.GlobalCooldownReadyTick;
                view.CooldownReadyTicks = new Dictionary<string, long>(f.CooldownReadyTick, StringComparer.Ordinal);
                view.ConsumableCharges = new Dictionary<string, int>(f.ConsumableCharges, StringComparer.Ordinal);   // sous-lot 12.6
                view.ConsumableReadyTick = f.ConsumableReadyTick;
                // Revue Codex de la PR #32 (16e tour) : un absent (attendu, refusé, parti) ou un mort n'agit pas, comme pour CanMove.
                view.CanCast = !f.Out && !Blocked(StatusBlocks.Skills);
                view.CanGuard = !f.Out && !Blocked(StatusBlocks.Guard);
                view.CanParry = !f.Out && !Blocked(StatusBlocks.Parry);
            }
            if (f.Body is { } body)
            {
                view.Position = GroundVector.FromRaw(body.X, body.Z);
                view.Facing = GroundVector.FromRaw(body.FacingX, body.FacingZ);
                view.Radius = Fixed.FromRaw(body.Radius);
                view.MoveDirection = body.Moving ? GroundVector.FromRaw(body.DirectionX, body.DirectionZ) : (GroundVector?)null;
                view.MoveBackward = body.Moving && body.Backward;
                // Un mannequin ne marche pas ; revue Codex de la PR #33 (16e tour) : un ennemi à comportement non plus sur commande (move et stop
                // lui sont refusés : il marche seul).
                view.CanMove = !f.Out && (f.Character != null || f.Assailant != null) && f.Behavior is null && !Blocked(StatusBlocks.Movement);
                view.SelectedTarget = body.Selected?.Id;
            }
            if (f.Assailant is { } assailant)
            {
                view.Assailant = assailant;
                view.NextAttackTick = f.NextAttackTick;
                view.NextAttack = assailant.Attacks[f.NextAttack];
                view.CurrentTarget = ViewTarget(f, tick);
                view.Engaged = f.Behavior is null || f.Engaged;
                view.Flees = f.Behavior?.Flee == true;
                view.AttacksBlocked = Blocked(StatusBlocks.AssailantAttacks);
            }
            return view;
        }

        /// <summary>
        /// Cible des attaques à cible unique vue au début du tick (<see cref="SingleTarget"/> après les expirations du tick) : la source de
        /// la provocation active la plus récente, sinon la cible du scénario (null si l'assaillant n'a que des attaques de zone).
        /// </summary>
        private string? ViewTarget(Fighter assailant, long tick)
        {
            StatusInstance? taunt = null;
            foreach (StatusInstance status in assailant.Statuses.Values)
                if (status.Definition.IsTaunt && status.UntilTick > tick && (taunt is null || status.AppliedSeq > taunt.AppliedSeq)) taunt = status;
            if (taunt?.Source is { } source) return source;
            AssailantSetup setup = assailant.Assailant!;
            // Sous-lot 11.5 : cible de scénario là et vivante, sinon l'ennemi le plus proche.
            if (setup.Behavior is null || (setup.Target != null && !_byId[setup.Target].Out)) return setup.Target;
            return Nearest(assailant)?.Id;
        }

        /// <summary>
        /// Sous-lot 8.6 : soumet une commande pour le tick courant, avant qu'il soit simulé (pilote de mesure). Elle est traitée exactement
        /// comme une commande du scénario de même rang : son numéro suit ceux du scénario puis des commandes déjà soumises, si bien qu'un
        /// combat rejoué avec le scénario complété de <see cref="SubmittedCommands"/> est identique. Mêmes plafonds d'admission que le
        /// scénario (commandes au total, par acteur et par tick, capacité de la file, événements initiaux du scénario complété) ; l'acteur
        /// et la cible doivent exister ; sous-lot 11.4a, un déplacement n'est admis que dans un combat positionné, avec une direction valide. Un
        /// refus ne modifie rien. Le majorant prudent de la file (correctif 7.2) n'est vérifié qu'au rejeu
        /// du scénario complété (<c>PilotedCombat</c>), qui refuse alors le combat entier.
        /// </summary>
        public void Submit(CombatCommand command) => SubmitAll(new[] { command ?? throw new ArgumentNullException(nameof(command)) });

        /// <summary>
        /// Sous-lot 9.2 (revue Codex de la PR #13) : soumet un groupe de commandes du tick courant, tout ou rien. Chacune est contrôlée
        /// comme par <see cref="Submit"/>, en comptant les précédentes du groupe ; si l'une est refusée, aucune n'est admise et rien ne
        /// change (par exemple quitter la garde pour lancer une compétence : la garde n'est pas quittée si le lancer ne peut pas l'être).
        /// </summary>
        public void SubmitAll(IReadOnlyList<CombatCommand> commands)
        {
            if (commands is null) throw new ArgumentNullException(nameof(commands));
            if (IsFinished) throw new InvalidOperationException("Combat terminé : plus aucune commande.");
            // Revue Codex de la PR #33 : calculé ici, retenu seulement si le groupe est admis (un refus ne change rien, instantané compris).
            long initialEvents = _initialEvents >= 0 ? _initialEvents : _setup.InitialEventCount();
            var groupPerActor = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < commands.Count; i++)
            {
                CombatCommand command = commands[i] ?? throw new ArgumentNullException(nameof(commands));
                if (!_byId.ContainsKey(command.Actor)) throw new ArgumentException($"Acteur inconnu : {command.Actor}.", nameof(commands));
                if (command.Target != null && !_byId.ContainsKey(command.Target)) throw new ArgumentException($"Cible inconnue : {command.Target}.", nameof(commands));
                if (command.AtMs % _tickMs != 0 || command.AtMs / _tickMs != CurrentTick)
                    throw new ArgumentException($"Commande à {command.AtMs} ms : seul le tick courant ({CurrentTick}) est admis.", nameof(commands));
                // Revue Codex de la PR #28 : mêmes contrôles de forme qu'une commande du scénario (CombatSetup.Validate), pour que le moteur
                // n'exécute jamais une commande que le scénario complété refuserait : action connue ; déplacement seulement dans un combat
                // positionné, avec une direction valide ; une direction seulement pour un déplacement.
                if (!Enum.IsDefined(typeof(CombatAction), command.Action))
                    throw new ArgumentException("Action inconnue.", nameof(commands));
                if ((command.Action == CombatAction.Move || command.Action == CombatAction.Stop) && _setup.Space is null)
                    throw new ArgumentException("Déplacement dans un combat sans espace (bloc space absent).", nameof(commands));
                // Revue Codex de la PR #33 : un ennemi qui a un comportement se déplace seul ; le scénario complété refuserait sa marche
                // pilotée (et l'intention autonome l'écraserait dans la même phase).
                if ((command.Action == CombatAction.Move || command.Action == CombatAction.Stop) && _byId[command.Actor].Behavior != null)
                    throw new ArgumentException("Un ennemi qui a un comportement se déplace seul (pas de move ni de stop).", nameof(commands));
                // Sous-lot 11.4c : sélection seulement dans un combat positionné ; une cible seulement pour une compétence ou une sélection.
                if (command.Action == CombatAction.Select && _setup.Space is null)
                    throw new ArgumentException("Sélection de cible dans un combat sans espace (bloc space absent).", nameof(commands));
                if (command.Target != null && command.Action != CombatAction.UseSkill && command.Action != CombatAction.Select)
                    throw new ArgumentException("Une garde, une parade ou un déplacement n'a pas de cible.", nameof(commands));
                if (command.Action == CombatAction.Move ? command.Direction is not { } direction || !SpaceLimits.IsDirection(direction) : command.Direction != null)
                    throw new ArgumentException(command.Action == CombatAction.Move
                        ? "Direction de déplacement : composantes dans [−1, 1], non toutes deux nulles."
                        : "Seul un déplacement (move) a une direction.", nameof(commands));
                // Revue Codex de la PR #47 : même contrôle de l'objet d'un soin que CombatSetup.Validate, pour que le scénario complété reste
                // valide (rejeu, restauration) : un soin nomme un objet de soin emporté par son personnage ; seule cette commande nomme un objet.
                if (command.Action == CombatAction.Consume)
                {
                    if (command.Item is null) throw new ArgumentException("Soin (consume) sans objet.", nameof(commands));
                    if (!(_byId[command.Actor].Setup is CharacterSetup carrier && carrier.Consumables.Any(c => c.Id == command.Item)))
                        throw new ArgumentException($"Objet de soin « {command.Item} » absent de ce que « {command.Actor} » emporte.", nameof(commands));
                }
                else if (command.Item != null) throw new ArgumentException("Seul un soin (consume) nomme un objet.", nameof(commands));
                // Sous-lot V1.8d : un bonus modifié porte un bonus bien formé, pour un personnage ; seule cette commande en porte un. Le
                // précontrôle numérique du combat complété est vérifié plus bas, pour le groupe entier.
                if (command.Action == CombatAction.Gear)
                {
                    if (command.Gear is not { } gear) throw new ArgumentException("Bonus modifié (gear) sans bonus.", nameof(commands));
                    for (int k = 0; k < Domain.Stats.GearBonus.Fields.Count; k++)
                        if (gear[k] < 0 || gear[k] > Domain.Stats.GearBonus.MaxValue)
                            throw new ArgumentException($"Bonus modifié : {Domain.Stats.GearBonus.Fields[k]} {gear[k]} hors de [0, {Domain.Stats.GearBonus.MaxValue}].", nameof(commands));
                    if (_byId[command.Actor].Character is null)
                        throw new ArgumentException($"Bonus modifié de « {command.Actor} », qui n'est pas un personnage.", nameof(commands));
                }
                else if (command.Gear != null) throw new ArgumentException("Seul un bonus modifié (gear) porte un bonus d'équipement.", nameof(commands));
                int earlier = groupPerActor.TryGetValue(command.Actor, out int g) ? g : 0;
                int already = _commandsPerActorTick.TryGetValue((command.Actor, CurrentTick), out int n) ? n : 0;
                if (checked(already + earlier) >= _rules.MaxCommandsPerActorPerTick)
                    throw new InvalidOperationException($"Plus de {_rules.MaxCommandsPerActorPerTick} commandes de « {command.Actor} » au tick {CurrentTick}.");
                if (checked(_commandCount + i) >= _rules.MaxCommands)
                    throw new InvalidOperationException($"Plus de {_rules.MaxCommands} commandes (combat.limits.maxCommands).");
                // Revue Codex de la PR #10 : capacité de la file contrôlée avant toute modification. La file ne dépasse jamais sa capacité,
                // et le scénario complété garde un nombre d'événements initiaux admissible (une entrée par commande, comme à la construction).
                // Sous-lot 11.4d : le combat complété compte aussi une entrée par arrivée (Join).
                if (checked(_queue.Count + i) >= _rules.MaxPendingEvents || checked(initialEvents + _joined.Count + _submitted.Count + i + 1) > _rules.MaxPendingEvents)
                    throw new InvalidOperationException($"File pleine : capacité {_rules.MaxPendingEvents} (combat.limits.maxPendingEvents).");
                groupPerActor[command.Actor] = earlier + 1;
            }
            // Sous-lot V1.8d : un bonus modifié change les statistiques en cours de combat : le combat complété du groupe doit passer la
            // validation entière (précontrôle numérique sur l'enveloppe des bonus compris), comme une arrivée (Join) ; sinon rien ne change.
            if (commands.Any(c => c.Action == CombatAction.Gear))
            {
                IReadOnlyList<Domain.RuleViolation> violations = _setup.Completed(_joined, _submitted.Concat(commands)).Validate();
                if (violations.Count > 0)
                    throw new ArgumentException($"Bonus modifié refusé : {string.Join(" ; ", violations.Take(3).Select(v => $"{string.Join(", ", v.Fields)} : {v.Message}"))}", nameof(commands));
            }
            _initialEvents = initialEvents;
            foreach (CombatCommand command in commands)
            {
                CountSubmitted(command);
                // Hors traitement, comme les commandes du scénario mises en file à la construction : aucun budget du tick n'est consommé.
                _queue.Enqueue(CurrentTick, PhaseOf(command), new CommandItem { Index = _setup.Commands.Count + _submitted.Count, Command = command });
                if (_queue.Count > _peakPending) _peakPending = _queue.Count;
                _submitted.Add(command);
            }
        }
    }
}
