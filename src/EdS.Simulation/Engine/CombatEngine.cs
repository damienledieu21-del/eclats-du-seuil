using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;
using EdS.Simulation.Combat;
using EdS.Simulation.Randomness;

namespace EdS.Simulation.Engine
{
    /// <summary>
    /// Moteur de combat : horloge logique en ticks, file d'événements ordonnée, commandes validées atomiquement,
    /// coûts, recharges, temps global de recharge, dégâts directs, régénération d'endurance (lot 5) ; garde, parade
    /// parfaite, Charges de rempart, Résonance, Fragilité, rupture de garde et assaillant d'entraînement (lot 6) ; altérations
    /// génériques définies en données (lot 7.2) : DOT, contrôles, modificateurs et immunités, voir <see cref="StatusDefinition"/>.
    /// Aucune lecture de l'horloge système, aucun flottant, aucun état partagé entre combats : tout l'état appartient
    /// à l'instance. Même combat (règles, participants, commandes, graine) = même journal, octet par octet.
    /// <para>Ordre d'un tick (A10) : expirations → commandes (initiative décroissante, puis identifiant) → résolution
    /// des actions et dégâts → effets périodiques (entretien de la garde) → régénération → morts et fin de combat.</para>
    /// <para>Budgets (lot 7.1) : chaque traitement élémentaire (une échéance, le retrait d'une commande, une commande, une
    /// action complète avec toutes ses cibles, le passage d'un combattant dans une phase balayée) calcule son coût exact
    /// en travail, lignes et places de file à partir de l'état courant, puis le <b>réserve avant toute mutation</b>. Si la
    /// réserve dépasse un budget, le combat s'arrête proprement sans que ce traitement ait rien modifié. Voir
    /// docs/LIMITES_LOT_7.md pour le modèle de coût et les limites d'atomicité restantes (débordement numérique).</para>
    /// </summary>
    public sealed partial class CombatEngine
    {
        /// <summary>Version du moteur, inscrite au journal : un rejeu exige la même version.</summary>
        public const string Version = "0.11.0";

        public const string LogFormat = "eds-combat-log";

        /// <summary>
        /// Version du format du journal. 2 (correctif 6.1) : <c>stateApplied</c> et <c>stateExpired</c> portent
        /// <c>instance</c> et <c>generation</c> ; nouvel événement <c>stateRemoved</c>. 3 (lot 7.2) : toutes les lignes d'état portent
        /// <c>category</c> et <c>source</c> ; nouveaux événements <c>stateBlocked</c> et <c>dotDamage</c> ; <c>attackStarted</c> porte
        /// <c>applies</c> (liste des altérations) au lieu de <c>appliesFragility</c> ; garde interrompue (<c>guardStopped</c>, raison
        /// <c>interrupted</c>) ; motifs de refus et de saut issus des altérations. 4 (lot 7.3) : chaque ligne de jeu porte <c>eid</c>
        /// (identité de l'effet) et <c>cause</c> (eid du parent, null pour l'effet racine) ; l'effet racine porte <c>root</c> ; un effet
        /// de profondeur ≥ 1 porte <c>depth</c> et <c>trigger</c>. Les diagnostics n'en portent aucun. 5 (sous-lot 8.1) : la ligne
        /// <c>combatant</c> porte <c>statusResistance</c> et <c>rules</c> (règles effectives d'un personnage, null sinon) ;
        /// <c>guardBroken</c> porte <c>rampartChargesSpent</c>. Un journal d'un autre format est refusé au rejeu.
        /// </summary>
        public const int LogFormatVersion = 5;

        // Identifiants stables du journal.
        public const string Resonance = "resonance";
        public const string RampartCharges = "rampartCharges";
        public const string EnduranceResource = "endurance";

        private sealed class Fighter
        {
            public CombatantSetup Setup = null!;
            public CharacterSetup? Character;
            public AssailantSetup? Assailant;
            public DerivedStats Stats = null!;

            /// <summary>Sous-lot 8.1 : règles effectives (règles de base et modificateurs X1, évaluation des X2) ; neutres hors personnage.</summary>
            public CombatantRules Rules = null!;
            public readonly Dictionary<string, DerivedStats> AttackStats = new Dictionary<string, DerivedStats>(StringComparer.Ordinal);
            public int NextAttack;
            public HashSet<string> Castable = new HashSet<string>(StringComparer.Ordinal);
            public long? Health;
            public long? MaxHealth;
            public long Endurance;
            public long MaxEndurance;

            /// <summary>Premier tick compté pour la régénération (dernière dépense + délai) ; null = rien à régénérer.</summary>
            public long? RegenFromTick;
            public long RegenGranted;

            public long GlobalCooldownReadyTick;
            public readonly Dictionary<string, long> CooldownReadyTick = new Dictionary<string, long>(StringComparer.Ordinal);

            public bool Guarding;

            /// <summary>Ticks réellement gardés depuis le début du combat (garde active en phase 4), toutes gardes confondues.</summary>
            public long GuardedTicks;

            /// <summary>Entretien déjà payé depuis le début du combat : le reliquat fractionnaire n'est jamais perdu (M01).</summary>
            public long GuardUpkeepPaid;

            public bool ParryOpen;
            public long ParryStartTick;
            public long ParryEndTick;             // fenêtre [début, fin[
            public bool ParrySucceeded;
            public long ParryRecoveryUntilTick;   // nouvelle parade refusée tant que tick < valeur

            /// <summary>
            /// Altérations actives (lot 7.2), une instance par clé : (altération, source) pour un cumul par source, (altération) sinon ;
            /// voir <see cref="StatusInstance"/>.
            /// </summary>
            public readonly Dictionary<StatusKey, StatusInstance> Statuses = new Dictionary<StatusKey, StatusInstance>();

            public int Resonance;
            public int RampartCharges;

            /// <summary>Numéro du prochain tour d'un assaillant (racine <c>assailantTurn:&lt;id&gt;:&lt;n&gt;</c>), sauts compris.</summary>
            public long NextTurn;

            /// <summary>
            /// Lot 7.3 (A6) : provenance du premier effet qui a amené les points de vie à 0 (impact ou tick de DOT), cause = cet effet ;
            /// la ligne <c>died</c> et les retraits qui en découlent en héritent.
            /// </summary>
            public Provenance? LethalCause;

            /// <summary>Lot 7.3 : déclencheurs permanents (talents, compétences équipées, assaillant), triés (§5.4).</summary>
            public List<TriggerSlot> Triggers = null!;

            public Pcg32 Random = null!;
            public bool IsDead;
            public readonly CombatantResult Result = new CombatantResult();

            public string Id => Setup.Id;
            public string Team => Setup.Team;
        }

        /// <summary>Clé d'une instance d'altération sur une cible : la source n'y figure que pour un cumul par source (DOT, A13).</summary>
        private readonly struct StatusKey : IEquatable<StatusKey>
        {
            public readonly string State;
            public readonly string? Source;

            public StatusKey(string state, string? source)
            {
                State = state;
                Source = source;
            }

            public bool Equals(StatusKey other) => string.Equals(State, other.State, StringComparison.Ordinal) && string.Equals(Source, other.Source, StringComparison.Ordinal);
            public override bool Equals(object? obj) => obj is StatusKey other && Equals(other);
            public override int GetHashCode() => unchecked(StringComparer.Ordinal.GetHashCode(State) * 31 + (Source is null ? 0 : StringComparer.Ordinal.GetHashCode(Source)));
        }

        /// <summary>
        /// Instance d'un état temporaire (correctif 6.1, M02). <see cref="Id"/> est unique dans le combat et jamais réutilisé ;
        /// <see cref="Generation"/> augmente à chaque renouvellement. Une échéance programmée porte le couple (instance,
        /// génération) en vigueur au moment où elle a été programmée : elle n'agit que si ce couple est toujours celui de
        /// l'état actif. Une échéance périmée (renouvellement, retrait à la mort, réapplication après expiration) ne
        /// déclenche donc jamais de transition, et un état non cumulable expire exactement une fois.
        /// </summary>
        private sealed class StatusInstance
        {
            public long Id;
            public int Generation;
            public StatusDefinition Definition = null!;
            public StatusKey Key;

            /// <summary>Combattant qui porte l'instance.</summary>
            public Fighter Owner = null!;

            /// <summary>Combattant qui a posé (ou renouvelé en dernier) l'instance ; null pour une rupture de garde par l'entretien.</summary>
            public string? Source;

            /// <summary>
            /// Hors DOT : actif tant que tick &lt; UntilTick, retiré en phase 1 du tick UntilTick. DOT : fin de l'instance ; le dernier
            /// tick est le premier dont le suivant dépasserait cette fin, et il expire l'instance en phase 4 après ses dégâts.
            /// </summary>
            public long UntilTick;

            /// <summary>DOT : puissance figée par tick (puissance de la source × coefficient), la plus forte conservée au renouvellement.</summary>
            public Fixed Power;

            /// <summary>DOT : tick du prochain tick de dégâts (cadence d'origine, jamais décalée par un renouvellement, D4).</summary>
            public long NextTick;

            /// <summary>
            /// Lot 7.3 (A1, A2) : provenance de la dernière application acceptée, cause = son <c>stateApplied</c>, avec l'instance et la
            /// génération courantes. Un renouvellement la remplace ; un refus n'y touche pas. Ticks de DOT, expiration et état accordé
            /// la lisent au moment où ils agissent (A3 à A5).
            /// </summary>
            public Provenance Origin = null!;

            /// <summary>Lot 7.3 : déclencheurs portés par l'instance (identité : numéro d'instance) ; null si l'altération n'en a pas.</summary>
            public TriggerSlot[]? Slots;

            public string State => Definition.Id;
        }

        private abstract class Scheduled { }

        private sealed class CommandItem : Scheduled
        {
            public int Index;
            public CombatCommand Command = null!;
        }

        private sealed class AssailantTurn : Scheduled
        {
            public Fighter Fighter = null!;
            public long Turn;
        }

        private sealed class CooldownExpiry : Scheduled
        {
            public Fighter Fighter = null!;
            public string Skill = null!;
        }

        private sealed class StateExpiry : Scheduled
        {
            public Fighter Fighter = null!;
            public StatusKey Key;
            public long Instance;
            public int Generation;
        }

        /// <summary>Tick d'un DOT (lot 7.2) : un seul élément par instance, qui se reprogramme lui-même en phase 4.</summary>
        private sealed class DotTick : Scheduled
        {
            public Fighter Fighter = null!;
            public StatusKey Key;
            public long Instance;
        }

        private sealed class ParryWindowEnd : Scheduled
        {
            public Fighter Fighter = null!;
            public long StartTick;

            /// <summary>Provenance héritée de la commande <c>parry</c> qui a ouvert la fenêtre, cause = <c>parryStarted</c>.</summary>
            public Provenance Origin = null!;
        }

        private sealed class PendingAction
        {
            public Fighter Actor = null!;
            public SkillDefinition? Skill;
            public SkillCombatDefinition? Combat;
            public AssailantAttack? Attack;
            public Fighter? Target;

            /// <summary>Provenance des conséquences de l'action (impacts, gains), cause = <c>skillCast</c> ou <c>attackStarted</c>.</summary>
            public Provenance Origin = null!;

            /// <summary>Faux pour une action qui dépense de la Résonance : pas de génération récursive (A5).</summary>
            public bool GeneratesResonance = true;

            /// <summary>Lot 7.3 : frappe d'une réaction (effet <c>strike</c>), au rang du déclencheur ; ni compétence ni attaque.</summary>
            public TriggerEffect? Strike;
            public int StrikeRank = 1;

            public SkillTargeting Targeting => Combat?.Targeting ?? Attack?.Targeting ?? SkillTargeting.Enemy;
            public bool Unblockable => Combat?.Unblockable ?? Attack?.Unblockable ?? Strike!.Unblockable;
            public IReadOnlyList<StatusApplication> Applies => Combat?.Applies ?? Attack?.Applies ?? (IReadOnlyList<StatusApplication>)Array.Empty<StatusApplication>();
            public DamageType DamageType => Combat?.DamageType ?? Attack?.DamageType ?? Strike!.DamageType!.Value;
        }

        /// <summary>Un budget du tick ou la capacité de la file serait dépassé par le traitement suivant (lot 7.1).</summary>
        private sealed class BudgetExceeded : Exception
        {
            public string Reason { get; }
            public long Limit { get; }

            public BudgetExceeded(string reason, long limit)
            {
                Reason = reason;
                Limit = limit;
            }
        }

        /// <summary>
        /// Coût d'un traitement (lot 7.1) : unités de travail, lignes de simulation comptées, places de file prises.
        /// Une opération (retrait de la file, impact, application ou retrait d'état, passage d'un combattant) vaut une
        /// unité ; une ligne vaut une unité et une ligne ; une mise en file vaut une unité et une place.
        /// </summary>
        private readonly struct Cost
        {
            public readonly long Work;
            public readonly long Events;
            public readonly long Queue;

            public Cost(long work, long events, long queue)
            {
                Work = work;
                Events = events;
                Queue = queue;
            }

            public static readonly Cost None = new Cost(0, 0, 0);
            public static readonly Cost Op = new Cost(1, 0, 0);
            public static readonly Cost Line = new Cost(1, 1, 0);
            public static readonly Cost Enqueue = new Cost(1, 0, 1);

            public static Cost operator +(Cost a, Cost b) =>
                new Cost(checked(a.Work + b.Work), checked(a.Events + b.Events), checked(a.Queue + b.Queue));

            public static Cost operator *(long k, Cost c) => new Cost(checked(k * c.Work), checked(k * c.Events), checked(k * c.Queue));

            public Cost If(bool condition) => condition ? this : None;

            /// <summary>Majorant composante par composante (lot 7.3, réservation majorée d'une réaction).</summary>
            public static Cost Max(Cost a, Cost b) => new Cost(Math.Max(a.Work, b.Work), Math.Max(a.Events, b.Events), Math.Max(a.Queue, b.Queue));
        }

        private readonly CombatSetup _setup;
        private readonly CombatRules _rules;
        private readonly ICombatLog _log;
        private readonly List<Fighter> _fighters;          // ordre du scénario
        private readonly List<Fighter> _fightersById;      // ordre ordinal des identifiants
        private readonly Dictionary<string, Fighter> _byId;
        private readonly EventQueue<Scheduled> _queue = new EventQueue<Scheduled>();
        private readonly long _tickMs;
        private readonly long _durationTicks;
        private readonly long _globalCooldownTicks;
        private readonly long _regenDelayTicks;
        private readonly CombatantRules _baseRules;
        private readonly StatusCatalog _statuses;
        private readonly Dictionary<string, int> _statusOrder = new Dictionary<string, int>(StringComparer.Ordinal);
        private long _sequence;
        private long _lastEid;
        private long _lastStatusInstance;
        private long _eventsThisTick;
        private long _workThisTick;
        private bool _countEvents;

        // Réserve du traitement en cours (lot 7.1) : le travail et les lignes ne peuvent pas dépasser ces valeurs, ni les
        // mises en file ce nombre. Hors traitement, la réserve est vide : toute consommation non réservée est une erreur.
        private long _scopeWork;
        private long _scopeEvents;
        private long _scopeEnqueues;

        private long _peakWork;
        private long _peakEvents;
        private long _peakPending;

        public long CurrentTick { get; private set; }

        /// <summary>Travail consommé par le dernier tick simulé (lot 7.1, diagnostic hors journal).</summary>
        public long LastTickWork { get; private set; }

        /// <summary>Lignes de simulation comptées au dernier tick simulé (lot 7.1, diagnostic hors journal).</summary>
        public long LastTickEvents { get; private set; }
        public bool IsFinished => Result != null;
        public CombatResult? Result { get; private set; }

        /// <exception cref="CombatSetupException">Le combat est invalide (voir <see cref="CombatSetup.Validate"/>).</exception>
        public CombatEngine(CombatSetup setup, ICombatLog log) : this(setup, log, queueBound: true) { }

        /// <summary>Réservé aux tests : <paramref name="queueBound"/> faux omet le majorant de saturation (correctif 7.2) pour
        /// exercer l'arrêt <c>queueLimitExceeded</c>, devenu un filet de sécurité inatteignable pour un combat validé.</summary>
        internal CombatEngine(CombatSetup setup, ICombatLog log, bool queueBound) : this(setup, log, queueBound, numericLimits: true) { }

        /// <summary>Réservé aux tests (correctif 7.3.1) : <paramref name="numericLimits"/> faux omet le précontrôle numérique pour
        /// exercer l'arrêt <c>overflow</c> et vérifier qu'aucun impact n'est appliqué à moitié.</summary>
        internal CombatEngine(CombatSetup setup, ICombatLog log, bool queueBound, bool numericLimits)
        {
            _setup = setup ?? throw new ArgumentNullException(nameof(setup));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            var violations = setup.Validate(queueBound, numericLimits);
            if (violations.Count > 0) throw new CombatSetupException(violations);

            _rules = setup.Combat;
            _tickMs = _rules.TickMilliseconds;
            _durationTicks = setup.DurationMs / _tickMs;
            _globalCooldownTicks = _rules.GlobalCooldownMs / _tickMs;
            _regenDelayTicks = _rules.EnduranceRegenDelayMs / _tickMs;
            _baseRules = CombatantRules.For(_rules, ModifierSet.Empty);
            _statuses = _rules.Statuses;
            for (int i = 0; i < _statuses.Definitions.Count; i++) _statusOrder[_statuses.Definitions[i].Id] = i;

            _fighters = setup.Combatants.Select(CreateFighter).ToList();
            _fightersById = _fighters.OrderBy(f => f.Id, StringComparer.Ordinal).ToList();
            _byId = _fighters.ToDictionary(f => f.Id, StringComparer.Ordinal);
            BuildTriggerIndex();

            for (int i = 0; i < setup.Commands.Count; i++)
            {
                CombatCommand command = setup.Commands[i];
                Schedule(command.AtMs / _tickMs, CombatPhase.Commands, new CommandItem { Index = i, Command = command });
            }
            foreach (Fighter f in _fighters)
                if (f.Assailant is { } assailant && assailant.FirstAttackMs / _tickMs < _durationTicks)
                    Schedule(assailant.FirstAttackMs / _tickMs, CombatPhase.Commands, new AssailantTurn { Fighter = f, Turn = f.NextTurn++ });

            _log.Append(CombatLogHeader.Write(setup));
            foreach (Fighter f in _fighters) LogCombatant(f);
        }

        /// <summary>Simule un combat complet et renvoie son résultat.</summary>
        public static CombatResult Run(CombatSetup setup, ICombatLog log)
        {
            var engine = new CombatEngine(setup, log);
            while (engine.Step()) { }
            return engine.Result!;
        }

        private Fighter CreateFighter(CombatantSetup setup)
        {
            var f = new Fighter { Setup = setup, Random = new Pcg32(unchecked((ulong)_setup.Seed), StreamOf(setup.Id)) };
            f.Result.Id = setup.Id;
            f.Result.Team = setup.Team;
            switch (setup)
            {
                case CharacterSetup character:
                    f.Character = character;
                    // Sous-lot 8.1 : mêmes fonctions que la validation (précontrôle) : modificateurs agrégés, statistiques, règles.
                    ModifierSet modifiers = ModifierSet.For(character);
                    f.Stats = CombatantStats.ForCharacter(_setup.Stats, character.Build, modifiers);
                    f.Rules = CombatantRules.For(_rules, modifiers);
                    f.MaxHealth = f.Stats.MaxHealth.RoundToInt(RoundingMode.Floor);
                    f.Health = f.MaxHealth;
                    f.MaxEndurance = f.Stats.MaxEndurance.RoundToInt(RoundingMode.Floor);
                    f.Endurance = f.MaxEndurance;
                    foreach (string skill in character.Build.ActiveSkills) f.Castable.Add(skill);
                    if (character.Build.Ultimate != null) f.Castable.Add(character.Build.Ultimate);
                    break;
                case DummySetup dummy:
                    f.Rules = _baseRules;
                    f.Stats = CombatantStats.ForDefenses(_setup.Stats, dummy.Armor, dummy.MagicResistance, dummy.StatusResistance);
                    break;
                case AssailantSetup assailant:
                    f.Assailant = assailant;
                    f.Rules = _baseRules;
                    f.Stats = CombatantStats.ForDefenses(_setup.Stats, assailant.Armor, assailant.MagicResistance, assailant.StatusResistance);
                    foreach (AssailantAttack attack in assailant.Attacks)
                        f.AttackStats[attack.Id] = CombatantStats.ForAssailantAttack(_setup.Stats, attack.RawDamage);
                    break;
            }
            return f;
        }

        /// <summary>Flux PCG32 propre à chaque combattant (FNV-1a 64 de son identifiant) : ajouter un combattant ne décale pas les tirages des autres.</summary>
        internal static ulong StreamOf(string id)
        {
            ulong hash = 14695981039346656037UL;
            foreach (byte b in Encoding.UTF8.GetBytes(id))
                unchecked { hash = (hash ^ b) * 1099511628211UL; }
            return hash;
        }

        /// <summary>Avance d'un tick. Renvoie faux quand le combat est terminé.</summary>
        public bool Step()
        {
            if (IsFinished) return false;
            long tick = CurrentTick;
            _eventsThisTick = 0;
            _workThisTick = 0;
            _scopeWork = _scopeEvents = _scopeEnqueues = 0;
            _countEvents = true;
            try
            {
                RunExpirations(tick);
                List<PendingAction> actions = RunCommands(tick);
                RunResolution(tick, actions);
                RunPeriodic(tick);
                RunRegeneration(tick);
                RunEndOfTick(tick);
                CloseScope();
            }
            catch (BudgetExceeded e)
            {
                Abort(tick, e.Reason, e.Limit);
            }
            catch (OverflowException)
            {
                Abort(tick, "overflow", null);
            }
            RecordTickPeaks();
            LastTickWork = _workThisTick;
            LastTickEvents = _eventsThisTick;
            if (!IsFinished) CurrentTick++;
            return !IsFinished;
        }

        // --- Phase 1 : expirations ---------------------------------------------------------------

        private void RunExpirations(long tick)
        {
            while (_queue.TryPeek(tick, CombatPhase.Expirations, out Scheduled item))
            {
                if (ProcessTriggerItem(tick, CombatPhase.Expirations, item)) continue;
                ReserveAndDequeue(Cost.Op + ExpirationCost(tick, item));
                Charge(1);
                switch (item)
                {
                    case CooldownExpiry expiry:
                        if (expiry.Fighter.CooldownReadyTick.TryGetValue(expiry.Skill, out long ready) && ready == tick)
                            Emit(tick, CombatPhase.Expirations, "cooldownReady", Provenance.NewRoot($"cooldown:{expiry.Fighter.Id}:{expiry.Skill}:{tick}", expiry.Fighter.Id),
                                w => w.Field("actor", expiry.Fighter.Id).Field("skill", expiry.Skill));
                        break;
                    case ParryWindowEnd end:
                        CloseParryWindow(tick, end);
                        break;
                    case StateExpiry state:
                        ExpireState(tick, state);
                        break;
                }
            }
        }

        /// <summary>
        /// Coût exact d'une échéance hors retrait : une ligne si elle agit, rien si elle est périmée ; une expiration d'altération
        /// ajoute le retrait de l'état et l'application éventuelle de l'état accordé (<see cref="StatusDefinition.GrantsOnExpiry"/>).
        /// </summary>
        private Cost ExpirationCost(long tick, Scheduled item) => item switch
        {
            CooldownExpiry e => Cost.Line.If(e.Fighter.CooldownReadyTick.TryGetValue(e.Skill, out long ready) && ready == tick),
            ParryWindowEnd e => Cost.Line.If(e.Fighter.ParryOpen && e.Fighter.ParryStartTick == e.StartTick),
            StateExpiry e => IsCurrent(e, out StatusInstance? status) ? EndStatusCost(tick, status) : Cost.None,
            _ => Cost.None,
        };

        private static bool IsCurrent(StateExpiry expiry, out StatusInstance status) =>
            expiry.Fighter.Statuses.TryGetValue(expiry.Key, out status!) && status.Id == expiry.Instance && status.Generation == expiry.Generation;

        private void CloseParryWindow(long tick, ParryWindowEnd end)
        {
            Fighter f = end.Fighter;
            if (!f.ParryOpen || f.ParryStartTick != end.StartTick) return;   // fenêtre déjà fermée (rupture, mort)
            f.ParryOpen = false;
            long? recoveryUntil = null;
            if (!f.ParrySucceeded)
            {
                f.ParryRecoveryUntilTick = checked(tick + f.Rules.ParryFailureRecoveryMs / _tickMs);
                recoveryUntil = f.ParryRecoveryUntilTick;
                f.Result.FailedParries++;
            }
            Emit(tick, CombatPhase.Expirations, "parryEnded", end.Origin, w => w
                .Field("actor", f.Id).Field("outcome", f.ParrySucceeded ? "success" : "failed").Field("recoveryUntilTick", recoveryUntil));
        }

        private void ExpireState(long tick, StateExpiry expiry)
        {
            // Échéance périmée : état renouvelé (autre génération), retiré, ou remplacé par une nouvelle instance.
            if (!IsCurrent(expiry, out StatusInstance? status)) return;
            EndStatus(tick, CombatPhase.Expirations, expiry.Fighter, status);
        }

        // --- Phase 2 : commandes -----------------------------------------------------------------

        private List<PendingAction> RunCommands(long tick)
        {
            var batch = new List<(Fighter Actor, int Index, Scheduled Item)>();
            while (_queue.TryPeek(tick, CombatPhase.Commands, out Scheduled item))
            {
                ReserveAndDequeue(Cost.Op);
                Charge(1);
                if (item is CommandItem command) batch.Add((_byId[command.Command.Actor], command.Index, item));
                else batch.Add((((AssailantTurn)item).Fighter, -1, item));   // le tour d'un assaillant passe avant ses éventuelles commandes
            }

            var actions = new List<PendingAction>();
            // Tri stable : initiative décroissante, identifiant ordinal, puis ordre du scénario.
            foreach (var entry in batch
                .OrderByDescending(c => c.Actor.Setup.Initiative)
                .ThenBy(c => c.Actor.Id, StringComparer.Ordinal)
                .ThenBy(c => c.Index))
            {
                Reserve(entry.Item is CommandItem planned ? CommandCost(tick, planned) : AssailantTurnCost(tick, entry.Actor));
                PendingAction? action = entry.Item is CommandItem command ? Execute(tick, command) : AssailantAttacks(tick, (AssailantTurn)entry.Item);
                if (action != null) actions.Add(action);
            }
            DrainTriggers(tick, CombatPhase.Commands);
            return actions;
        }

        /// <summary>Coût exact d'une commande, calculé avec les mêmes contrôles que son exécution, sans rien modifier.</summary>
        private Cost CommandCost(long tick, CommandItem item)
        {
            Fighter actor = _byId[item.Command.Actor];
            switch (item.Command.Action)
            {
                case CombatAction.GuardStart:
                case CombatAction.GuardStop:
                    return Cost.Line;
                case CombatAction.Parry:
                    return CheckParry(tick, actor) is null ? Cost.Enqueue + Cost.Line : Cost.Line;
                default:
                    var (skill, target, resonanceCost) = CastContext(item, actor);
                    if (CheckSkill(tick, actor, skill, target, resonanceCost) != null) return Cost.Line;
                    SkillCombatDefinition combat = skill!.Combat!;
                    return Cost.Enqueue.If(actor.Rules.CooldownMs(skill, combat) / _tickMs > 0) + Observable(TriggerEvent.SkillCast) + GainCost(actor, combat.RampartChargeGain);
            }
        }

        /// <summary>
        /// Coût exact du tour d'un assaillant : prochain tour s'il tombe avant la fin, et une ligne (<c>attackSkipped</c>, diagnostic,
        /// ou <c>attackStarted</c>, observable).
        /// </summary>
        private Cost AssailantTurnCost(long tick, Fighter assailant)
        {
            AssailantSetup setup = assailant.Assailant!;
            AssailantAttack attack = setup.Attacks[assailant.NextAttack];
            Fighter? target = attack.Targeting == SkillTargeting.Enemy ? _byId[setup.Target!] : null;
            bool skipped = BlockReason(assailant, StatusBlocks.AssailantAttacks) != null || target is { IsDead: true };
            return Cost.Enqueue.If(checked(tick + setup.IntervalMs / _tickMs) < _durationTicks) + (skipped ? Cost.Line : Observable(TriggerEvent.SkillCast));
        }

        private (SkillDefinition? Skill, Fighter? Target, int ResonanceCost) CastContext(CommandItem item, Fighter actor)
        {
            CombatCommand command = item.Command;
            Fighter? target = command.Target != null ? _byId[command.Target] : null;
            SkillDefinition? skill = null;
            actor.Character?.Class.TryGetSkill(command.Skill!, out skill);
            bool isUltimate = skill != null && actor.Character?.Build.Ultimate == skill.Id;
            return (skill, target, isUltimate ? _rules.UltimateResonanceCost : 0);
        }

        /// <summary>Valide une commande sans rien modifier, puis seulement l'applique (refus atomique).</summary>
        private PendingAction? Execute(long tick, CommandItem item)
        {
            CombatCommand command = item.Command;
            Fighter actor = _byId[command.Actor];
            Provenance root = Provenance.NewRoot($"command:{item.Index}", actor.Id);
            switch (command.Action)
            {
                case CombatAction.GuardStart:
                    if (Reject(tick, item, actor, CheckGuardStart(tick, actor))) return null;
                    actor.Guarding = true;
                    Emit(tick, CombatPhase.Commands, "guardStarted", root, w => w.Field("actor", actor.Id).Field("command", item.Index).Field("endurance", actor.Endurance));
                    return null;

                case CombatAction.GuardStop:
                    if (Reject(tick, item, actor, CheckAlive(actor) ?? (actor.Guarding ? null : "guardNotActive"))) return null;
                    actor.Guarding = false;
                    ResumeRegeneration(actor, tick);
                    Emit(tick, CombatPhase.Commands, "guardStopped", root, w => w.Field("actor", actor.Id).Field("command", item.Index).Field("endurance", actor.Endurance));
                    return null;

                case CombatAction.Parry:
                    if (Reject(tick, item, actor, CheckParry(tick, actor))) return null;
                    actor.ParryOpen = true;
                    actor.ParryStartTick = tick;
                    actor.ParryEndTick = checked(tick + actor.Rules.ParryWindowMs / _tickMs);   // ≥ 1 tick (CombatantRules)
                    actor.ParrySucceeded = false;
                    var end = new ParryWindowEnd { Fighter = actor, StartTick = tick };
                    Schedule(actor.ParryEndTick, CombatPhase.Expirations, end);
                    long started = Emit(tick, CombatPhase.Commands, "parryStarted", root, w => w
                        .Field("actor", actor.Id).Field("command", item.Index).Field("windowEndTick", actor.ParryEndTick).Field("guarding", actor.Guarding));
                    end.Origin = root.CausedBy(started);
                    return null;

                default:
                    return CastSkill(tick, item, actor, root);
            }
        }

        private PendingAction? CastSkill(long tick, CommandItem item, Fighter actor, Provenance root)
        {
            CombatCommand command = item.Command;
            var (skill, target, resonanceCost) = CastContext(item, actor);

            if (Reject(tick, item, actor, CheckSkill(tick, actor, skill, target, resonanceCost))) return null;

            SkillCombatDefinition combat = skill!.Combat!;
            if (combat.EnduranceCost > 0) SpendEndurance(actor, combat.EnduranceCost, tick);
            actor.Resonance -= resonanceCost;
            actor.RampartCharges -= combat.RampartChargeCost;
            actor.GlobalCooldownReadyTick = checked(tick + _globalCooldownTicks);
            long cooldownTicks = actor.Rules.CooldownMs(skill, combat) / _tickMs;
            long readyTick = checked(tick + cooldownTicks);
            if (cooldownTicks > 0)
            {
                actor.CooldownReadyTick[skill.Id] = readyTick;
                Schedule(readyTick, CombatPhase.Expirations, new CooldownExpiry { Fighter = actor, Skill = skill.Id });
            }
            actor.Result.SkillsCast++;
            long cast = Emit(tick, CombatPhase.Commands, "skillCast", root, new ObservedEvent { Kind = TriggerEvent.SkillCast, Source = actor.Id, Target = command.Target, Skill = skill.Id }, w => w
                .Field("actor", actor.Id).Field("skill", skill.Id).Field("target", command.Target).Field("command", item.Index)
                .Field("enduranceCost", combat.EnduranceCost).Field("endurance", actor.Endurance)
                .Field("resonanceCost", resonanceCost).Field("resonance", actor.Resonance)
                .Field("rampartChargeCost", combat.RampartChargeCost).Field("rampartCharges", actor.RampartCharges)
                .Field("cooldownReadyTick", readyTick).Field("globalCooldownReadyTick", actor.GlobalCooldownReadyTick));
            Provenance consequences = root.CausedBy(cast);
            Gain(tick, CombatPhase.Commands, actor, RampartCharges, combat.RampartChargeGain, "skill", consequences);
            return new PendingAction { Actor = actor, Skill = skill, Combat = combat, Target = target, GeneratesResonance = resonanceCost == 0, Origin = consequences };
        }

        private bool Reject(long tick, CommandItem item, Fighter actor, string? reason)
        {
            if (reason is null) return false;
            CombatCommand command = item.Command;
            actor.Result.CommandsRejected++;
            EmitDiagnostic(tick, CombatPhase.Commands, "commandRejected", w => w
                .Field("actor", actor.Id).Field("action", CombatActionIds.Of(command.Action)).Field("skill", command.Skill)
                .Field("target", command.Target).Field("command", item.Index).Field("reason", reason));
            return true;
        }

        private static string? CheckAlive(Fighter actor)
        {
            if (actor.IsDead) return "actorDead";
            if (actor.Character is null) return "actorCannotAct";
            return null;
        }

        /// <summary>
        /// Lot 7.2 (D11) : motif de refus d'une action bloquée par une altération active, juste après les motifs de mort ; si
        /// plusieurs altérations bloquent l'action, la première dans l'ordre du catalogue l'emporte (étourdissement avant rupture).
        /// </summary>
        private string? BlockReason(Fighter f, StatusBlocks action)
        {
            StatusDefinition? first = null;
            foreach (StatusInstance status in f.Statuses.Values)
                if ((status.Definition.Blocks & action) != 0 && (first is null || _statusOrder[status.State] < _statusOrder[first.Id]))
                    first = status.Definition;
            return first?.BlockReason;
        }

        private string? CheckGuardStart(long tick, Fighter actor)
        {
            if (CheckAlive(actor) is { } reason) return reason;
            if (BlockReason(actor, StatusBlocks.Guard) is { } blocked) return blocked;
            if (actor.Guarding) return "guardAlreadyActive";
            return null;
        }

        private string? CheckParry(long tick, Fighter actor)
        {
            if (CheckAlive(actor) is { } reason) return reason;
            if (BlockReason(actor, StatusBlocks.Parry) is { } blocked) return blocked;
            if (actor.ParryOpen) return "parryActive";
            if (tick < actor.ParryRecoveryUntilTick) return "parryRecovery";
            return null;
        }

        /// <summary>Motif de refus d'une compétence, dans un ordre fixe et documenté ; null si la commande est acceptée.</summary>
        private string? CheckSkill(long tick, Fighter actor, SkillDefinition? skill, Fighter? target, int resonanceCost)
        {
            if (CheckAlive(actor) is { } reason) return reason;
            if (BlockReason(actor, StatusBlocks.Skills) is { } blocked) return blocked;
            if (skill is null || !actor.Castable.Contains(skill.Id)) return "skillNotEquipped";
            if (skill.Combat is not { } combat) return "skillNotImplemented";
            if (actor.Guarding) return "guardActive";
            if (tick < actor.GlobalCooldownReadyTick) return "globalCooldown";
            if (actor.CooldownReadyTick.TryGetValue(skill.Id, out long ready) && tick < ready) return "cooldown";
            switch (combat.Targeting)
            {
                case SkillTargeting.Enemy:
                    if (target is null) return "targetRequired";
                    if (target.Team == actor.Team) return "targetNotEnemy";
                    if (target.IsDead) return "targetDead";
                    break;
                case SkillTargeting.AllEnemies:
                    if (target != null) return "targetNotAllowed";
                    break;
            }
            if (actor.Endurance < combat.EnduranceCost) return "insufficientEndurance";
            if (actor.Resonance < resonanceCost) return "insufficientResonance";
            if (actor.RampartCharges < combat.RampartChargeCost) return "insufficientRampartCharges";
            return null;
        }

        /// <summary>Tour d'un assaillant : attaque suivante de sa liste, puis tour suivant programmé.</summary>
        private PendingAction? AssailantAttacks(long tick, AssailantTurn turn)
        {
            Fighter assailant = turn.Fighter;
            AssailantSetup setup = assailant.Assailant!;
            AssailantAttack attack = setup.Attacks[assailant.NextAttack];
            assailant.NextAttack = (assailant.NextAttack + 1) % setup.Attacks.Count;
            long next = checked(tick + setup.IntervalMs / _tickMs);
            if (next < _durationTicks) Schedule(next, CombatPhase.Commands, new AssailantTurn { Fighter = assailant, Turn = assailant.NextTurn++ });

            Fighter? target = attack.Targeting == SkillTargeting.Enemy ? _byId[setup.Target!] : null;
            // Lot 7.2 : un assaillant sous contrôle saute son attaque ; l'attaque suivante de sa liste et sa cadence ne changent pas.
            if (BlockReason(assailant, StatusBlocks.AssailantAttacks) is { } blocked)
            {
                EmitDiagnostic(tick, CombatPhase.Commands, "attackSkipped", w => w
                    .Field("actor", assailant.Id).Field("attack", attack.Id).Field("target", target?.Id).Field("reason", blocked));
                return null;
            }
            if (target is { IsDead: true })
            {
                EmitDiagnostic(tick, CombatPhase.Commands, "attackSkipped", w => w
                    .Field("actor", assailant.Id).Field("attack", attack.Id).Field("target", target.Id).Field("reason", "targetDead"));
                return null;
            }
            Provenance root = Provenance.NewRoot($"assailantTurn:{assailant.Id}:{turn.Turn}", assailant.Id);
            long started = Emit(tick, CombatPhase.Commands, "attackStarted", root, new ObservedEvent { Kind = TriggerEvent.SkillCast, Source = assailant.Id, Target = target?.Id, Skill = attack.Id }, w => w
                .Field("actor", assailant.Id).Field("attack", attack.Id).Field("target", target?.Id)
                .Field("rawDamage", attack.RawDamage).Field("unblockable", attack.Unblockable).Names("applies", attack.Applies.Select(a => a.Status)));
            return new PendingAction { Actor = assailant, Attack = attack, Target = target, GeneratesResonance = false, Origin = root.CausedBy(started) };
        }

        // --- Phase 3 : résolution ----------------------------------------------------------------

        private void RunResolution(long tick, List<PendingAction> actions)
        {
            foreach (PendingAction action in actions)
            {
                IReadOnlyList<Fighter> targets = action.Targeting == SkillTargeting.Enemy
                    ? new[] { action.Target! }
                    : _fightersById.Where(f => f.Team != action.Actor.Team && !f.IsDead).ToArray();
                // A5 : une attaque directe réussie rapporte de la Résonance une seule fois, quel que soit le nombre de cibles.
                bool resonance = targets.Count > 0 && action.Actor.Character != null && action.GeneratesResonance;
                // Lot 7.1 : l'action entière (toutes ses cibles) est réservée avant le premier impact ; les cibles étant
                // distinctes, l'impact sur l'une ne change pas le coût prévu pour les suivantes.
                Cost cost = resonance ? GainCost(action.Actor, action.Actor.Rules.ResonancePerDirectHit) : Cost.None;
                foreach (Fighter target in targets) cost += HitCost(tick, action, target);
                Reserve(cost);
                foreach (Fighter target in targets) Hit(tick, action, target);
                if (resonance) Gain(tick, CombatPhase.Resolution, action.Actor, Resonance, action.Actor.Rules.ResonancePerDirectHit, "directHit", action.Origin);
            }
            // Lot 7.3 : puis les notifications et réactions de la phase, dans l'ordre de la file (réactions reportées du tick
            // précédent d'abord, puis celles de ce tick).
            DrainTriggers(tick, CombatPhase.Resolution);
        }

        /// <summary>
        /// Coût exact d'un impact, déduit de l'état de la cible avant l'impact, avec les mêmes branches que <see cref="Hit"/> :
        /// défense, puis chaque altération portée dans l'ordre, avec l'état de garde et de parade laissé par les étapes précédentes
        /// (une rupture de garde ou un étourdissement change ce que les suivantes interrompent).
        /// </summary>
        private Cost HitCost(long tick, PendingAction action, Fighter target)
        {
            Cost cost = Cost.Op + Observable(TriggerEvent.HitDealt, TriggerEvent.HitTaken);   // impact, ligne « damage »
            bool guarding = target.Guarding, parryOpen = target.ParryOpen;
            bool defending = target.Character != null && !target.IsDead && (target.ParryOpen || target.Guarding);
            bool parried = false;
            if (defending && action.Unblockable) { }
            else if (defending && target.ParryOpen)
            {
                parried = true;
                if (!target.ParrySucceeded)
                    cost += Observable(TriggerEvent.PerfectParry) + GainCost(target, _rules.RampartChargesPerParry) + GainCost(target, _rules.ResonancePerPerfectParry);
            }
            else if (defending)
            {
                if (GuardBreaksOnBlock(target))
                {
                    cost += BreakGuardCost(tick, target, action.Actor.Id, impact: true);
                    guarding = parryOpen = false;
                }
                else cost += Observable(TriggerEvent.Block) + GainCost(target, _rules.ResonancePerBlock);
            }
            foreach (StatusApplication application in action.Applies)
            {
                StatusDefinition definition = _statuses[application.Status];
                if (parried)
                {
                    cost += Cost.Line;   // stateBlocked « parried »
                    continue;
                }
                StatusPlan plan = Plan(tick, target, definition, action.Actor.Id, null, action.Actor);
                cost += ApplyCost(plan, definition, guarding, parryOpen);
                if (plan.Applies)
                {
                    if ((definition.Interrupts & StatusInterrupts.Guard) != 0) guarding = false;
                    if ((definition.Interrupts & StatusInterrupts.Parry) != 0) parryOpen = false;
                }
            }
            return cost;
        }

        /// <summary>
        /// Somme des bonus de dégâts directs reçus des altérations actives (catégorie « altération », A19) : chaque définition compte
        /// une fois, même portée par plusieurs sources, ce qui garde le majorant du précontrôle (somme du catalogue).
        /// </summary>
        private Fixed DamageTakenIncrease(Fighter target)
        {
            Fixed total = Fixed.Zero;
            HashSet<string>? seen = null;
            foreach (StatusInstance status in target.Statuses.Values)
                if (status.Definition.DamageTakenIncrease != Fixed.Zero && (seen ??= new HashSet<string>(StringComparer.Ordinal)).Add(status.State))
                    total += status.Definition.DamageTakenIncrease;
            return total;
        }

        private void Hit(long tick, PendingAction action, Fighter target) => Hit(tick, CombatPhase.Resolution, action, target);

        private void Hit(long tick, CombatPhase phase, PendingAction action, Fighter target)
        {
            Charge(1);
            Fighter actor = action.Actor;
            Provenance origin = action.Origin;
            long? parryEid = null, blockEid = null;
            bool critical = action.Combat != null && CriticalRoll.Roll(actor.Random, actor.Stats);   // l'assaillant ne critique jamais (aucun tirage)
            Fixed increase = DamageTakenIncrease(target);
            bool fragile = increase != Fixed.Zero;
            DamageBonuses bonuses = fragile ? DamageBonuses.None.Add(BonusCategory.Alteration, increase) : DamageBonuses.None;

            // Défense de la cible : parade (prioritaire) ou garde ; jamais les deux. Un impact imparable ignore l'une et l'autre.
            string defense = "none";
            Fixed mitigation = Fixed.Zero;
            bool parryReward = false, blockReward = false;
            bool defending = target.Character != null && !target.IsDead && (target.ParryOpen || target.Guarding);
            if (defending && action.Unblockable)
            {
                defense = "unblockable";
            }
            else if (defending && target.ParryOpen)
            {
                defense = "parry";
                mitigation = _rules.ParryDamageReduction;
                if (!target.ParrySucceeded)
                {
                    target.ParrySucceeded = true;   // récompense une seule fois par tentative
                    target.Result.PerfectParries++;
                    parryReward = true;
                    parryEid = Emit(tick, phase, "parrySucceeded", origin, HitObserved(TriggerEvent.PerfectParry, action, target, HitDefense.Parry), w => w.Field("actor", target.Id).Field("source", actor.Id));
                }
            }
            else if (defending)
            {
                if (GuardBreaksOnBlock(target))
                {
                    BreakGuard(tick, phase, target, "insufficientEndurance", actor.Id, origin, impact: true);
                    defense = "guardBroken";
                }
                else
                {
                    // Sous-lot 8.1 (guard.noUpkeepBreak) : sans endurance suffisante, la garde tient ; le combattant paie ce qui lui
                    // reste et la réduction est ramenée à guard.reductionAtZeroEndurance. Ligne « blocked » normale, coût réellement payé.
                    bool weakened = target.Endurance < target.Rules.GuardCostPerBlock;
                    long blockCost = weakened ? target.Endurance : target.Rules.GuardCostPerBlock;
                    if (!weakened || blockCost > 0) SpendEndurance(target, blockCost, tick);   // garde affaiblie sans endurance : pas une dépense
                    target.Result.Blocks++;
                    defense = "guard";
                    mitigation = weakened ? target.Rules.GuardReductionAtZeroEndurance : target.Rules.GuardDamageReduction;
                    blockReward = true;
                    blockEid = Emit(tick, phase, "blocked", origin, HitObserved(TriggerEvent.Block, action, target, HitDefense.Guard), w => w
                        .Field("actor", target.Id).Field("source", actor.Id).Field("enduranceCost", blockCost).Field("endurance", target.Endurance));
                }
            }

            // Sous-lot 8.1 (X1, X2, B2) : modificateurs de dégâts lus au calcul des dégâts, sur l'état courant (après la défense de cet
            // impact, avant ses gains), sans ligne ni notification. Dégâts subis du défenseur : réduction totale multiplicative
            // plafonnée à 85 %, hors parade ; dégâts infligés de l'attaquant : catégorie talent. Sans modificateur, rien ne change.
            if (defense != "parry")
                mitigation = target.Rules.TotalReduction(mitigation, target.Rules.DamageTakenAt(target.Guarding, target.RampartCharges, target.Resonance));
            Fixed dealt = actor.Rules.DamageDealtAt(actor.Guarding, actor.RampartCharges, actor.Resonance);
            if (dealt != Fixed.Zero) bonuses = bonuses.Add(BonusCategory.Talent, dealt);

            DerivedStats attacker = AttackerStats(action);
            DamageInput input = action.Combat is { } combat
                ? new DamageInput(attacker, target.Stats, combat.PowerType, combat.DamageType, actor.Rules.Coefficient(action.Skill!, combat), bonuses, critical, mitigation)
                : action.Attack is { } attack
                    ? new DamageInput(attacker, target.Stats, CombatantStats.PowerOf(attack.DamageType), attack.DamageType, Fixed.One, bonuses, false, mitigation)
                    // Lot 7.3 : frappe de réaction, sans critique ni tirage (Q6, Q7), puissance du porteur.
                    : new DamageInput(attacker, target.Stats, action.Strike!.PowerType!.Value, action.Strike.DamageType!.Value,
                        action.Strike.Coefficient!.Value.At(action.StrikeRank), bonuses, false, mitigation);
            long amount = DamageCalculator.Compute(input, _setup.Damage).FinalDamage;

            // Correctif 7.3.1 (A73-M01) : nouvelles valeurs préparées et vérifiées avant toute affectation ; un débordement arrête
            // le combat sans impact à moitié appliqué.
            DamageUpdate update = PrepareDamage(target, actor, amount);
            long overkill = update.Overkill;
            target.Health = update.Health;
            target.Result.DamageTaken = update.Taken;
            actor.Result.DamageDealt = update.Dealt!.Value;
            actor.Result.Hits = update.Hits;
            if (critical) actor.Result.CriticalHits++;
            ObservedEvent hit = HitObserved(TriggerEvent.HitDealt, action, target, TriggerIds.Defenses[defense]);
            hit.Also = TriggerEvent.HitTaken;
            long damage = Emit(tick, phase, "damage", origin, hit, w => w
                .Field("source", actor.Id).Field("target", target.Id).Field("skill", action.Skill?.Id).Field("attack", action.Attack?.Id)
                .Field("critical", critical).Field("fragile", fragile).Field("defense", defense).Field("amount", amount).Field("overkill", overkill)
                .Field("health", target.Health).Field("damageTaken", target.Result.DamageTaken));
            Provenance impact = origin.CausedBy(damage);
            NoteLethal(target, impact);

            if (parryReward)
            {
                Provenance parried = origin.CausedBy(parryEid!.Value).WithSource(target.Id);
                Gain(tick, phase, target, RampartCharges, _rules.RampartChargesPerParry, "perfectParry", parried);
                Gain(tick, phase, target, Resonance, _rules.ResonancePerPerfectParry, "perfectParry", parried);
            }
            if (blockReward) Gain(tick, phase, target, Resonance, _rules.ResonancePerBlock, "block", origin.CausedBy(blockEid!.Value).WithSource(target.Id));

            // Lot 7.2 : altérations portées, après les dégâts, dans l'ordre de la liste. Un DOT fige sa puissance maintenant.
            // Correctif 7.2 : un impact paré (parade parfaite, réussie à cet impact ou plus tôt dans la même fenêtre) n'applique
            // aucune altération (stateBlocked « parried ») ; un impact bloqué par la garde ou imparable les applique.
            foreach (StatusApplication application in action.Applies)
            {
                StatusDefinition definition = _statuses[application.Status];
                if (defense == "parry")
                {
                    EmitDiagnostic(tick, phase, "stateBlocked", w => w
                        .Field("combatant", target.Id).Field("state", definition.Id).Field("category", StatusIds.Of(definition.Category))
                        .Field("source", actor.Id).Field("reason", "parried"));
                    continue;
                }
                Fixed power = definition.IsDot ? DotPower(attacker, definition, application) : Fixed.Zero;
                ApplyStatus(tick, phase, target, definition, actor.Id, power, impact, dealer: actor);
            }
        }

        /// <summary>Instantané d'un impact (§1.4) : auteur, cible, compétence ou attaque, type de dégâts, défense.</summary>
        private static ObservedEvent HitObserved(TriggerEvent kind, PendingAction action, Fighter target, HitDefense defense) => new ObservedEvent
        {
            Kind = kind,
            Source = action.Actor.Id,
            Target = target.Id,
            Skill = action.Skill?.Id ?? action.Attack?.Id,
            DamageType = action.DamageType,
            Defense = defense,
        };

        /// <summary>Statistiques offensives d'une action : celles du personnage, ou celles de l'attaque d'assaillant (dégâts bruts).</summary>
        private static DerivedStats AttackerStats(PendingAction action) =>
            action.Attack != null ? action.Actor.AttackStats[action.Attack.Id] : action.Actor.Stats;

        /// <summary>
        /// Puissance figée d'un DOT (A12) : puissance du type de l'altération (physique pour un saignement, magique pour une brûlure ;
        /// dégâts bruts pour un assaillant) × coefficient de l'application, arrondie à 4 décimales. Sans critique ni bonus (décision 4).
        /// </summary>
        private static Fixed DotPower(DerivedStats attacker, StatusDefinition definition, StatusApplication application) =>
            attacker.GetPower(CombatantStats.PowerOf(definition.DamageType!.Value)) * application.Coefficient!.Value;

        // --- Altérations (lot 7.2) ----------------------------------------------------------------

        /// <summary>Issue d'une application, calculée sans rien modifier ; sert au coût réservé et à l'exécution.</summary>
        private enum StatusOutcome { New, Renew, Immune, Resisted, Active, NotLonger }

        private readonly struct StatusPlan
        {
            public readonly StatusOutcome Outcome;
            public readonly StatusKey Key;
            public readonly StatusInstance? Existing;
            public readonly long DurationTicks;

            public StatusPlan(StatusOutcome outcome, StatusKey key, StatusInstance? existing, long durationTicks)
            {
                Outcome = outcome;
                Key = key;
                Existing = existing;
                DurationTicks = durationTicks;
            }

            public bool Applies => Outcome == StatusOutcome.New || Outcome == StatusOutcome.Renew;

            public string Reason => Outcome switch
            {
                StatusOutcome.Immune => "immune",
                StatusOutcome.Resisted => "resisted",
                StatusOutcome.Active => "active",
                _ => "notLonger",
            };
        }

        /// <summary>
        /// Décide d'une application (cycle de vie, lot 7.2), dans cet ordre : immunité active (<c>immune</c>) ; durée réduite à
        /// 0 tick par la résistance (<c>resisted</c>, D7) ; nouvelle instance si la clé est libre ; sinon le mode de cumul :
        /// <c>refresh</c> et <c>perSourceKeepStronger</c> renouvellent, <c>keepLonger</c> renouvelle seulement si la nouvelle fin est
        /// plus tardive (<c>notLonger</c> sinon), <c>ignore</c> refuse (<c>active</c>). <paramref name="excluding"/> est une instance
        /// en cours de retrait, ignorée (expiration qui accorde un état).
        /// </summary>
        private StatusPlan Plan(long tick, Fighter target, StatusDefinition definition, string? source, StatusInstance? excluding,
            Fighter? dealer, long shortenMs = 0)
        {
            var key = new StatusKey(definition.Id, definition.Stacking == StatusStacking.PerSourceKeepStronger ? source : null);
            target.Statuses.TryGetValue(key, out StatusInstance? existing);
            if (existing == excluding) existing = null;
            foreach (StatusInstance status in target.Statuses.Values)
                if (status != excluding && status.Definition.ImmuneTo.Contains(definition.Id))
                    return new StatusPlan(StatusOutcome.Immune, key, existing, 0);
            long ticks = DurationTicks(definition, target, dealer, shortenMs);
            if (ticks == 0) return new StatusPlan(StatusOutcome.Resisted, key, existing, 0);
            if (existing is null) return new StatusPlan(StatusOutcome.New, key, null, ticks);
            return definition.Stacking switch
            {
                StatusStacking.Ignore => new StatusPlan(StatusOutcome.Active, key, existing, ticks),
                StatusStacking.KeepLonger when checked(tick + ticks) <= existing.UntilTick => new StatusPlan(StatusOutcome.NotLonger, key, existing, ticks),
                _ => new StatusPlan(StatusOutcome.Renew, key, existing, ticks),
            };
        }

        /// <summary>
        /// Durée en ticks d'une application (A14, A23) : durée de base, réduite pour un contrôle à résistance <c>duration</c> par le
        /// facteur r = K / (K + résistance aux altérations effective), calculé en <c>Fixed</c> arrondi vers le bas, puis
        /// ⌊ ticks × r ⌋. Ticks de base ≤ 86 400 000 et r brut ≤ 10⁴ : produit &lt; 10¹², sans débordement.
        /// <para>Sous-lot 8.1 : la durée de base reçoit d'abord, en ms, <c>status.&lt;id&gt;.durationDealt</c> de l'auteur d'une
        /// application directe (<paramref name="dealer"/>, null pour une rupture de garde ou un état accordé), <c>durationTaken</c> de la
        /// cible et le raccourci de rupture (<c>guard.breakShortenPerCharge</c>) ; le total est écrêté dans [1 tick, 24 h] (une période au
        /// moins pour un DOT), ce qui garde la borne ci-dessus. Les ajouts sont des multiples du tick (et de la période d'un DOT, par
        /// validation) et 24 h est un multiple du tick ; un DOT écrêté à 24 h peut ne plus être un multiple de sa période, et sa fin est
        /// alors ramenée au dernier tick périodique couvert (<see cref="DotEffectiveEnd"/>). Seule la résistance peut donner 0 tick
        /// (<c>resisted</c>).</para>
        /// </summary>
        private long DurationTicks(StatusDefinition definition, Fighter target, Fighter? dealer, long shortenMs)
        {
            long durationMs = definition.DurationMs;
            long adjust = checked(target.Rules.DurationTakenMs(definition.Id) + (dealer?.Rules.DurationDealtMs(definition.Id) ?? 0) - shortenMs);
            // Sous-lot 8.1 : durée modifiée écrêtée dans [1 tick (une période pour un DOT), 24 h] : un modificateur raccourcit, il
            // n'annule jamais (seule la résistance peut ramener une application à 0 tick, « resisted »).
            if (adjust != 0)
                durationMs = Math.Max(definition.IsDot ? definition.PeriodMs : _tickMs, Math.Min(CombatRules.AbsoluteMaxDurationMs, checked(durationMs + adjust)));
            long baseTicks = durationMs / _tickMs;
            if (definition.Resistance != StatusResistanceMode.Duration) return baseTicks;
            long constantRaw = checked(_setup.Stats.StatusResistanceConstant * Fixed.Scale);
            long factorRaw = IntMath.MulDiv(Fixed.Scale, constantRaw, checked(constantRaw + target.Stats.StatusResistance.Raw), RoundingMode.Floor);
            return IntMath.MulDiv(baseTicks, factorRaw, Fixed.Scale, RoundingMode.Floor);
        }

        /// <summary>
        /// Coût exact d'une application planifiée : refusée, une ligne <c>stateBlocked</c> ; sinon l'application (1), l'échéance ou le
        /// premier tick mis en file (sauf renouvellement d'un DOT, dont l'élément de file existe déjà), la ligne <c>stateApplied</c>,
        /// et une ligne par posture interrompue encore active.
        /// </summary>
        private Cost ApplyCost(StatusPlan plan, StatusDefinition definition, bool guarding, bool parryOpen)
        {
            if (!plan.Applies) return Cost.Line;
            bool requeue = !(definition.IsDot && plan.Outcome == StatusOutcome.Renew);
            return Cost.Op + Cost.Enqueue.If(requeue) + Observable(TriggerEvent.StateApplied)
                + Cost.Line.If(guarding && (definition.Interrupts & StatusInterrupts.Guard) != 0)
                + Cost.Line.If(parryOpen && (definition.Interrupts & StatusInterrupts.Parry) != 0);
        }

        /// <summary>
        /// Applique une altération (lot 7.2) selon <see cref="Plan"/>. Une instance nouvelle reçoit un identifiant jamais réutilisé ;
        /// un renouvellement garde l'instance et passe à la génération suivante, ce qui périme l'échéance précédente (M02). Un DOT
        /// renouvelé garde sa cadence et la plus forte puissance de sa source ; sa fin est repoussée (D4, décision 11). Les postures
        /// interrompues (garde, parade) le sont après la ligne <c>stateApplied</c>.
        /// </summary>
        private void ApplyStatus(long tick, CombatPhase phase, Fighter target, StatusDefinition definition, string? source, Fixed power,
            Provenance origin, StatusInstance? excluding = null, Fighter? dealer = null, long shortenMs = 0)
        {
            StatusPlan plan = Plan(tick, target, definition, source, excluding, dealer, shortenMs);
            if (!plan.Applies)
            {
                EmitDiagnostic(tick, phase, "stateBlocked", w => w
                    .Field("combatant", target.Id).Field("state", definition.Id).Field("category", StatusIds.Of(definition.Category))
                    .Field("source", source).Field("reason", plan.Reason));
                return;
            }
            Charge(1);
            bool renewed = plan.Outcome == StatusOutcome.Renew;
            StatusInstance status = plan.Existing ?? new StatusInstance { Id = ++_lastStatusInstance, Definition = definition, Key = plan.Key, Owner = target };
            if (!renewed)
            {
                target.Statuses[plan.Key] = status;
                status.Slots = StatusSlots(target, status);
            }
            status.Generation = checked(status.Generation + 1);
            status.Source = source;
            if (definition.IsDot)
            {
                status.Power = renewed ? Fixed.Max(status.Power, power) : power;
                if (!renewed)
                {
                    status.NextTick = checked(tick + definition.PeriodMs / _tickMs);
                    Schedule(status.NextTick, CombatPhase.Periodic, new DotTick { Fighter = target, Key = plan.Key, Instance = status.Id });
                }
                status.UntilTick = DotEffectiveEnd(status.NextTick, checked(tick + plan.DurationTicks), definition);
            }
            else
            {
                status.UntilTick = checked(tick + plan.DurationTicks);
                Schedule(status.UntilTick, CombatPhase.Expirations,
                    new StateExpiry { Fighter = target, Key = plan.Key, Instance = status.Id, Generation = status.Generation });
            }
            Provenance applying = origin.WithSource(source);
            long applied = Emit(tick, phase, "stateApplied", applying, StateObserved(TriggerEvent.StateApplied, target, status, renewed), w =>
            {
                StateFields(w, target, status).Field("untilTick", status.UntilTick).Field("renewed", renewed);
                if (definition.IsDot) w.Field("power", status.Power).Field("nextTick", status.NextTick);
                if (definition.MovementSpeedModifier != Fixed.Zero) w.Field("movementSpeedModifier", definition.MovementSpeedModifier);
            });
            status.Origin = applying.CausedBy(applied).ForInstance(status.Id, status.Generation);

            if ((definition.Interrupts & StatusInterrupts.Guard) != 0 && target.Guarding)
            {
                target.Guarding = false;
                ResumeRegeneration(target, tick);
                Emit(tick, phase, "guardStopped", status.Origin, w => w
                    .Field("actor", target.Id).Field("command", (long?)null).Field("endurance", target.Endurance)
                    .Field("reason", "interrupted").Field("state", definition.Id));
            }
            if ((definition.Interrupts & StatusInterrupts.Parry) != 0 && target.ParryOpen)
            {
                target.ParryOpen = false;
                Emit(tick, phase, "parryEnded", status.Origin, w => w
                    .Field("actor", target.Id).Field("outcome", target.ParrySucceeded ? "success" : "interrupted").Field("recoveryUntilTick", (long?)null));
            }
        }

        /// <summary>
        /// Correctif 7.2 : fin effective d'un DOT, alignée sur sa cadence. C'est le dernier tick périodique (à partir du prochain
        /// tick programmé, de période en période) qui ne dépasse pas la fin demandée (application + durée) ; l'instance inflige ses
        /// derniers dégâts et expire à ce tick, en phase périodique. Une nouvelle application a toujours une fin alignée (durée
        /// multiple de la période) ; un renouvellement hors cadence voit sa fin ramenée au dernier tick qu'elle couvre.
        /// <c>stateApplied</c> journalise cette fin effective (<c>untilTick</c>) et le prochain tick (<c>nextTick</c>).
        /// </summary>
        private long DotEffectiveEnd(long nextTick, long requestedEnd, StatusDefinition definition)
        {
            long period = definition.PeriodMs / _tickMs;
            return checked(nextTick + (requestedEnd - nextTick) / period * period);
        }

        /// <summary>Instantané d'une ligne d'état (§1.4) : porteur (cible), source, altération, catégorie, instance, renouvellement.</summary>
        private static ObservedEvent StateObserved(TriggerEvent kind, Fighter target, StatusInstance status, bool renewed = false) => new ObservedEvent
        {
            Kind = kind,
            Source = status.Source,
            Target = target.Id,
            State = status.State,
            Category = status.Definition.Category,
            Instance = status.Id,
            DamageType = status.Definition.DamageType,
            Renewed = renewed,
        };

        /// <summary>Champs communs des lignes d'état (format 3) : cible, altération, catégorie, source, instance, génération.</summary>
        private static JsonLineWriter StateFields(JsonLineWriter w, Fighter target, StatusInstance status) => w
            .Field("combatant", target.Id).Field("state", status.State).Field("category", StatusIds.Of(status.Definition.Category))
            .Field("source", status.Source).Field("instance", status.Id).Field("generation", status.Generation);

        /// <summary>Coût de la fin d'une instance par expiration : retrait (1), ligne <c>stateExpired</c>, état accordé éventuel.</summary>
        private Cost EndStatusCost(long tick, StatusInstance status)
        {
            Cost cost = Cost.Op + Observable(TriggerEvent.StateExpired);
            Fighter target = status.Owner;
            if (status.Definition.GrantsOnExpiry is { } granted)
            {
                StatusDefinition definition = _statuses[granted];
                cost += ApplyCost(Plan(tick, target, definition, status.Source, status, null), definition, target.Guarding, target.ParryOpen);
            }
            return cost;
        }

        /// <summary>
        /// Fin d'une instance par expiration (phase 1, ou phase 4 pour le dernier tick d'un DOT) : retrait, ligne <c>stateExpired</c>,
        /// reprise de la régénération si l'altération la bloquait, puis l'état accordé (<c>stun</c> → <c>stunImmunity</c>, A14),
        /// avec la même source. Profondeur 1 : un état accordé n'accorde rien (validé dans le catalogue).
        /// </summary>
        private void EndStatus(long tick, CombatPhase phase, Fighter target, StatusInstance status)
        {
            target.Statuses.Remove(status.Key);
            Charge(1);
            long expired = Emit(tick, phase, "stateExpired", status.Origin, StateObserved(TriggerEvent.StateExpired, target, status), w => StateFields(w, target, status));
            if ((status.Definition.Blocks & StatusBlocks.Regeneration) != 0) ResumeRegeneration(target, tick);
            if (status.Definition.GrantsOnExpiry is { } granted)
                ApplyStatus(tick, phase, target, _statuses[granted], status.Source, Fixed.Zero, status.Origin.CausedBy(expired));
        }

        /// <summary>Sous-lot 8.1 : la garde se brise-t-elle sur ce blocage ? Endurance insuffisante, sauf avec <c>guard.noUpkeepBreak</c>.</summary>
        private static bool GuardBreaksOnBlock(Fighter target) => target.Endurance < target.Rules.GuardCostPerBlock && !target.Rules.GuardNoUpkeepBreak;

        /// <summary>
        /// Sous-lot 8.1 (<c>guard.breakShortenPerCharge</c>) : Charges consommées par une rupture <b>par impact</b> (toutes, si le
        /// combattant a ce modificateur ; aucune sinon, ni pour l'entretien) ; lues avant la rupture, à l'identique pour le coût.
        /// </summary>
        private static int ChargesSpentByBreak(Fighter f, bool impact) => impact && f.Rules.GuardBreakShortenPerChargeMs > 0 ? f.RampartCharges : 0;

        private Cost BreakGuardCost(long tick, Fighter f, string? source, bool impact)
        {
            StatusDefinition definition = _statuses[_rules.GuardBreakStatus];
            long shortenMs = checked(ChargesSpentByBreak(f, impact) * f.Rules.GuardBreakShortenPerChargeMs);
            return Observable(TriggerEvent.GuardBroken) + Cost.Line.If(f.ParryOpen) + ApplyCost(Plan(tick, f, definition, source, null, null, shortenMs), definition, false, false);
        }

        /// <summary>
        /// Rupture de garde (A1, A19) : fin de la garde et de toute fenêtre de parade, puis l'altération de rupture
        /// (<c>combat.guard.breakStatus</c>) : ni garde ni parade ni régénération pendant sa durée. Sous-lot 8.1 : une rupture par
        /// impact consomme les Charges selon <see cref="ChargesSpentByBreak"/> (champ <c>rampartChargesSpent</c>) et raccourcit
        /// l'altération de <c>guard.breakShortenPerCharge</c> par Charge consommée.
        /// </summary>
        private void BreakGuard(long tick, CombatPhase phase, Fighter f, string reason, string? source, Provenance origin, bool impact)
        {
            int spent = ChargesSpentByBreak(f, impact);
            long shortenMs = checked(spent * f.Rules.GuardBreakShortenPerChargeMs);
            f.Guarding = false;
            f.RampartCharges -= spent;
            f.Result.GuardBreaks++;
            long broken = Emit(tick, phase, "guardBroken", origin, new ObservedEvent { Kind = TriggerEvent.GuardBroken, Source = source, Target = f.Id }, w => w
                .Field("actor", f.Id).Field("reason", reason).Field("endurance", f.Endurance).Field("rampartChargesSpent", spent));
            Provenance consequences = origin.CausedBy(broken);
            if (f.ParryOpen)
            {
                f.ParryOpen = false;
                Emit(tick, phase, "parryEnded", consequences, w => w
                    .Field("actor", f.Id).Field("outcome", f.ParrySucceeded ? "success" : "interrupted").Field("recoveryUntilTick", (long?)null));
            }
            ApplyStatus(tick, phase, f, _statuses[_rules.GuardBreakStatus], source, Fixed.Zero, consequences, shortenMs: shortenMs);
        }

        // --- Ressources --------------------------------------------------------------------------

        /// <summary>
        /// Coût d'un gain : une ligne s'il a lieu (montant positif, personnage), même si tout est perdu au plafond, et sa notification
        /// pour un gain naturel. Lot 7.3 (Q8) : un gain produit par une réaction <c>gainResource</c> est journalisé mais ne notifie
        /// pas <c>onResourceGained</c>, quelle que soit la profondeur.
        /// </summary>
        private Cost GainCost(Fighter f, long amount, bool observable = true) =>
            (observable ? Observable(TriggerEvent.ResourceGained) : Cost.Line).If(amount > 0 && f.Character != null);

        /// <summary>
        /// Gain plafonné d'une ressource (A4, A5) ; l'excédent est perdu et journalisé. Un gain ne déclenche jamais d'autre gain par
        /// les règles de base. Lot 7.3 : l'endurance n'est gagnée ainsi que par une réaction ; <paramref name="observable"/> faux pour
        /// un gain de réaction (Q8) : journalisé, jamais notifié.
        /// </summary>
        private void Gain(long tick, CombatPhase phase, Fighter f, string resource, long amount, string source, Provenance origin, bool observable = true)
        {
            if (amount <= 0 || f.Character is null) return;
            long before, max;
            TriggerResource kind;
            switch (resource)
            {
                case Resonance:
                    (before, max, kind) = (f.Resonance, _rules.ResonanceMax, TriggerResource.Resonance);
                    break;
                case RampartCharges:
                    (before, max, kind) = (f.RampartCharges, f.Rules.RampartChargesMax, TriggerResource.RampartCharges);
                    break;
                default:
                    (before, max, kind) = (f.Endurance, f.MaxEndurance, TriggerResource.Endurance);
                    break;
            }
            long after = Math.Min(max, checked(before + amount));
            switch (kind)
            {
                case TriggerResource.Resonance: f.Resonance = (int)after; break;
                case TriggerResource.RampartCharges: f.RampartCharges = (int)after; break;
                default: f.Endurance = after; break;
            }
            Emit(tick, phase, "resourceGained", origin, observable ? new ObservedEvent { Kind = TriggerEvent.ResourceGained, Source = f.Id, Resource = kind } : null, w => w
                .Field("actor", f.Id).Field("resource", resource).Field("source", source)
                .Field("amount", after - before).Field("wasted", before + amount - after).Field("value", after));
        }

        private void SpendEndurance(Fighter f, long amount, long tick)
        {
            f.Endurance -= amount;
            f.RegenFromTick = checked(tick + _regenDelayTicks);
            f.RegenGranted = 0;
        }

        /// <summary>
        /// Fin d'une garde ou d'une rupture : la régénération reprend dès qu'une seconde complète s'est écoulée depuis la
        /// dernière dépense, mais aucun point n'est accordé au titre des ticks suspendus.
        /// </summary>
        private static void ResumeRegeneration(Fighter f, long tick)
        {
            if (f.RegenFromTick is long from && from < tick)
            {
                f.RegenFromTick = tick;
                f.RegenGranted = 0;
            }
        }

        // --- Phases 4 et 5 : effets périodiques, régénération ------------------------------------

        /// <summary>
        /// A1 : entretien de la garde. Un tick est gardé si la garde est active à cette phase (une garde prise puis quittée
        /// dans le même tick ne compte pas ; prise, quittée et reprise, elle compte une fois). L'entretien dû est
        /// ⌊ coût/s × ticks gardés depuis le début du combat × durée du tick / 1000 ⌋ : cumulé sur toutes les gardes du
        /// combat, il ne dépend que du nombre de ticks réellement gardés, quelle que soit leur répartition (M01).
        /// S'il ne peut être payé, la garde est brisée et ce tick n'est pas compté : le reliquat reste dû.
        /// <para>Sous-lot 8.1 : coût/s effectif du combattant (décimal, à 4 décimales, même arrondi par défaut). Avec
        /// <c>guard.noUpkeepBreak</c>, un entretien impayable ne brise pas la garde : le combattant paie ce qui lui reste, le tick est
        /// compté et le reste du dû est abandonné (ligne « guardUpkeep » avec le montant réellement payé, éventuellement 0).</para>
        /// </summary>
        private void RunPeriodic(long tick)
        {
            // Lot 7.2 : ticks de DOT d'abord, dans l'ordre de la file (ordre de programmation), puis l'entretien des gardes.
            while (_queue.TryPeek(tick, CombatPhase.Periodic, out Scheduled item))
            {
                if (ProcessTriggerItem(tick, CombatPhase.Periodic, item)) continue;
                var dot = (DotTick)item;
                ReserveAndDequeue(Cost.Op + DotTickCost(tick, dot));
                Charge(1);
                RunDotTick(tick, dot);
            }

            foreach (Fighter f in _fightersById)
            {
                if (!f.Guarding || f.IsDead)
                {
                    Reserve(Cost.Op);
                    Charge(1);
                    continue;
                }
                long guardedTicks = checked(f.GuardedTicks + 1);
                long due = UpkeepDue(f, guardedTicks);
                long amount = due - f.GuardUpkeepPaid;
                bool shortfall = amount > 0 && f.Endurance < amount;
                bool breaks = shortfall && !f.Rules.GuardNoUpkeepBreak;
                Reserve(Cost.Op + (breaks ? BreakGuardCost(tick, f, null, impact: false) : Cost.Line.If(amount > 0)));
                Charge(1);
                Provenance upkeep = Provenance.NewRoot($"periodic:{f.Id}:{f.Id}:{tick}", f.Id);
                if (breaks)
                {
                    BreakGuard(tick, CombatPhase.Periodic, f, "upkeep", null, upkeep, impact: false);
                    continue;
                }
                f.GuardedTicks = guardedTicks;
                if (amount <= 0) continue;
                long paid = shortfall ? f.Endurance : amount;
                if (paid > 0) SpendEndurance(f, paid, tick);   // rien à payer : pas une dépense (le délai de régénération ne repart pas)
                f.GuardUpkeepPaid = due;
                Emit(tick, CombatPhase.Periodic, "guardUpkeep", upkeep, w => w.Field("actor", f.Id).Field("amount", paid).Field("endurance", f.Endurance));
            }
            DrainTriggers(tick, CombatPhase.Periodic);
        }

        /// <summary>
        /// Entretien dû après <paramref name="guardedTicks"/> ticks gardés : ⌊ coût/s × ticks × durée du tick / 1000 ⌋, coût/s
        /// décimal effectif ; égal au calcul entier du lot 7 pour un coût entier. Sans débordement : coût/s &lt; 2³¹, durée ≤ 24 h.
        /// </summary>
        private long UpkeepDue(Fighter f, long guardedTicks) =>
            IntMath.MulDiv(f.Rules.GuardUpkeepPerSecond.Raw, checked(guardedTicks * _tickMs), 1000 * Fixed.Scale, RoundingMode.Floor);

        private static bool IsCurrent(DotTick dot, out StatusInstance status) =>
            dot.Fighter.Statuses.TryGetValue(dot.Key, out status!) && status.Id == dot.Instance;

        /// <summary>Nouvelles valeurs d'un impact ou d'un tick de DOT, calculées sans rien modifier.</summary>
        private readonly struct DamageUpdate
        {
            public readonly long? Health;
            public readonly long Overkill;
            public readonly long Taken;
            public readonly long? Dealt;
            public readonly long Hits;

            public DamageUpdate(long? health, long overkill, long taken, long? dealt, long hits)
            {
                Health = health;
                Overkill = overkill;
                Taken = taken;
                Dealt = dealt;
                Hits = hits;
            }
        }

        /// <summary>
        /// Correctif 7.3.1 (A73-M01) : prépare dans des variables locales, avec opérations vérifiées, les PV de la cible, ses dégâts
        /// subis et les dégâts infligés de la source (et son compteur de coups, pour un impact direct). Toutes les opérations qui
        /// peuvent déborder sont faites ici ; l'appelant n'affecte qu'ensuite. Un débordement (configuration hors du précontrôle)
        /// arrête donc le combat (<c>overflow</c>) avant toute mutation de l'impact : ni PV, ni compteur, ni ligne partiels.
        /// </summary>
        private static DamageUpdate PrepareDamage(Fighter target, Fighter? source, long amount)
        {
            long? health = target.Health;
            long overkill = 0;
            if (health is long current)
            {
                overkill = Math.Max(0, checked(amount - current));
                health = Math.Max(0, checked(current - amount));
            }
            long taken = checked(target.Result.DamageTaken + amount);
            long? dealt = source is null ? (long?)null : checked(source.Result.DamageDealt + amount);
            long hits = source is null ? 0 : checked(source.Result.Hits + 1);
            return new DamageUpdate(health, overkill, taken, dealt, hits);
        }

        /// <summary>Le tick en cours est-il le dernier de l'instance (le suivant dépasserait sa fin) ?</summary>
        private static bool IsLastDotTick(long tick, StatusInstance status) => tick >= status.UntilTick;

        /// <summary>
        /// Coût exact d'un tick de DOT hors retrait de la file : périmé (instance retirée à la mort), rien ; sinon l'impact (1) et la
        /// ligne <c>dotDamage</c>, puis soit le tick suivant mis en file, soit la fin de l'instance (retrait, ligne, état accordé).
        /// </summary>
        private Cost DotTickCost(long tick, DotTick dot)
        {
            if (!IsCurrent(dot, out StatusInstance? status)) return Cost.None;
            return Cost.Op + Observable(TriggerEvent.DotTick) + (IsLastDotTick(tick, status) ? EndStatusCost(tick, status) : Cost.Enqueue);
        }

        /// <summary>
        /// Tick de DOT (A12, décisions 1, 2, 4) : dégâts = puissance figée réduite par la défense <b>actuelle</b> de la cible pour le
        /// type de l'altération (armure pour un saignement, résistance magique pour une brûlure), sans critique, ni garde, ni parade,
        /// ni Fragilité (dégâts directs seulement), ni Résonance, ni compteur de coups. Sous-lot 8.1 : réduits par les modificateurs de
        /// dégâts subis de la cible (<c>damageTaken</c>, plafond de 85 %), pas par ceux de dégâts infligés de la source. Crédités à la source même morte. Le dernier
        /// tick inflige ses dégâts puis expire l'instance dans la même étape : 6 s / 2 s donnent exactement 3 ticks.
        /// </summary>
        private void RunDotTick(long tick, DotTick dot)
        {
            if (!IsCurrent(dot, out StatusInstance? status)) return;
            Charge(1);
            Fighter target = dot.Fighter;
            StatusDefinition definition = status.Definition;
            // Sous-lot 8.1 : modificateurs de dégâts subis de la cible, lus sur son état au tick (réduction ≤ 85 %, sans garde ni
            // parade, A12). Sans modificateur, aucune réduction.
            Fixed mitigation = target.Rules.TotalReduction(Fixed.Zero, target.Rules.DamageTakenAt(target.Guarding, target.RampartCharges, target.Resonance));
            long amount = DamageCalculator.ComputePeriodic(status.Power, target.Stats, definition.DamageType!.Value,
                definition.Resistance == StatusResistanceMode.Defense, _setup.Damage, mitigation);
            // Correctif 7.3.1 (A73-M01) : PV et compteurs préparés et vérifiés avant toute affectation (voir PrepareDamage).
            Fighter? source = status.Source != null && _byId.TryGetValue(status.Source, out Fighter? s) ? s : null;
            DamageUpdate update = PrepareDamage(target, source, amount);
            long overkill = update.Overkill;
            target.Health = update.Health;
            target.Result.DamageTaken = update.Taken;
            if (source != null) source.Result.DamageDealt = update.Dealt!.Value;
            ObservedEvent dotObserved = StateObserved(TriggerEvent.DotTick, target, status);
            long dotEid = Emit(tick, CombatPhase.Periodic, "dotDamage", status.Origin, dotObserved, w => w
                .Field("source", status.Source).Field("target", target.Id).Field("state", status.State).Field("instance", status.Id)
                .Field("amount", amount).Field("overkill", overkill).Field("health", target.Health).Field("damageTaken", target.Result.DamageTaken));
            NoteLethal(target, status.Origin.CausedBy(dotEid));
            if (IsLastDotTick(tick, status))
            {
                EndStatus(tick, CombatPhase.Periodic, target, status);
                return;
            }
            status.NextTick = checked(tick + definition.PeriodMs / _tickMs);
            Schedule(status.NextTick, CombatPhase.Periodic, new DotTick { Fighter = target, Key = status.Key, Instance = status.Id });
        }

        /// <summary>
        /// A3 : la régénération démarre après un délai complet sans dépense. Le tick t compte comme régénéré s'il commence
        /// au moins <c>délai</c> après la dernière dépense ; le total accordé est
        /// ⌊ régénération/s × ticks régénérés × durée du tick / 1000 ⌋, sans dérive ni flottant, plafonné au maximum.
        /// A1 : suspendue pendant la garde et sous une altération qui bloque la régénération (rupture de garde).
        /// </summary>
        private void RunRegeneration(long tick)
        {
            foreach (Fighter f in _fightersById)
            {
                long regenTicks = f.RegenFromTick is long from ? tick - from + 1 : 0;
                bool eligible = f.Character != null && !f.IsDead && f.RegenFromTick != null && !f.Guarding
                    && BlockReason(f, StatusBlocks.Regeneration) is null && regenTicks > 0;
                // Sous-lot 8.1 : régénération/s effective du combattant (décimale), même arrondi par défaut.
                long target = eligible
                    ? IntMath.MulDiv(f.Rules.EnduranceRegenPerSecond.Raw, checked(regenTicks * _tickMs), 1000 * Fixed.Scale, RoundingMode.Floor)
                    : 0;
                long gain = eligible ? Math.Min(target - f.RegenGranted, f.MaxEndurance - f.Endurance) : 0;
                Reserve(Cost.Op + Cost.Line.If(gain > 0));
                Charge(1);
                if (!eligible) continue;
                f.RegenGranted = target;
                if (gain > 0)
                {
                    f.Endurance += gain;
                    Emit(tick, CombatPhase.Regeneration, "enduranceRegenerated", Provenance.NewRoot($"regeneration:{f.Id}:{tick}", f.Id), w => w.Field("actor", f.Id).Field("amount", gain).Field("endurance", f.Endurance));
                }
                if (f.Endurance >= f.MaxEndurance) f.RegenFromTick = null;   // plein : plus rien à régénérer jusqu'à la prochaine dépense
            }
            DrainTriggers(tick, CombatPhase.Regeneration);
        }

        // --- Phase 6 : morts et fin de combat ----------------------------------------------------

        /// <summary>
        /// Phase 6 (lot 7.3, §5.4) : point fixe. Relevé des morts, puis notifications et réactions de la phase, puis nouveau relevé,
        /// jusqu'à ce que la phase soit vide ; la fin de combat est évaluée ensuite. Sans déclencheur, un seul relevé (inchangé).
        /// </summary>
        private void RunEndOfTick(long tick)
        {
            do ScanDeaths(tick);
            while (DrainTriggers(tick, CombatPhase.EndOfTick) > 0);

            var aliveTeams = _fighters.Where(f => !f.IsDead).Select(f => f.Team).Distinct(StringComparer.Ordinal).ToList();
            if (aliveTeams.Count <= 1)
                Finish(tick, CombatEndReason.TeamDefeated, null, aliveTeams.Count == 1 ? aliveTeams[0] : null);
            else if (tick >= _durationTicks - 1)
                Finish(tick, CombatEndReason.DurationReached, null, null);
        }

        private void ScanDeaths(long tick)
        {
            foreach (Fighter f in _fightersById)
            {
                bool dies = !f.IsDead && f.Health == 0;
                Reserve(Cost.Op + (dies ? Observable(TriggerEvent.Death) + f.Statuses.Count * (Cost.Op + Observable(TriggerEvent.StateRemoved)) : Cost.None));
                Charge(1);
                if (!dies) continue;
                f.IsDead = true;
                f.Guarding = false;
                f.ParryOpen = false;
                // A6 : provenance de l'effet mortel ; sans effet mortel (points de vie nuls dès le départ), racine propre.
                Provenance lethal = f.LethalCause ?? Provenance.NewRoot($"endOfTick:{f.Id}:{tick}", null);
                long died = Emit(tick, CombatPhase.EndOfTick, "died", lethal,
                    new ObservedEvent { Kind = TriggerEvent.Death, Source = lethal.Source, Target = f.Id }, w => w.Field("combatant", f.Id));
                // M02 : la mort retire les états actifs (ordre ordinal de l'altération, puis de la source) ; leurs échéances et
                // ticks deviennent périmés ; aucun état n'est accordé sur un retrait. Lot 7.3 : leurs déclencheurs disparaissent.
                foreach (StatusInstance status in f.Statuses.Values
                    .OrderBy(x => x.State, StringComparer.Ordinal).ThenBy(x => x.Source ?? string.Empty, StringComparer.Ordinal).ToList())
                {
                    f.Statuses.Remove(status.Key);
                    Charge(1);
                    Emit(tick, CombatPhase.EndOfTick, "stateRemoved", lethal.CausedBy(died), StateObserved(TriggerEvent.StateRemoved, f, status),
                        w => StateFields(w, f, status).Field("reason", "death"));
                }
            }
        }

        private void Abort(long tick, string reason, long? limit)
        {
            _countEvents = false;
            EmitDiagnostic(tick, CombatPhase.EndOfTick, "simulationAborted", w => w.Field("reason", reason).Field("limit", limit));
            Finish(tick, CombatEndReason.Aborted, reason, null);
        }

        /// <summary>
        /// Fin du combat : résumé de chaque combattant. Charges et Résonance appartiennent à cette instance de combat :
        /// elles sont remises à zéro ici et chaque combat repart de zéro (A4).
        /// </summary>
        private void Finish(long tick, CombatEndReason reason, string? abortReason, string? winner)
        {
            _countEvents = false;
            foreach (Fighter f in _fighters)
            {
                bool character = f.Character != null;
                f.Result.Health = f.Health;
                f.Result.Endurance = character ? f.Endurance : (long?)null;
                f.Result.Resonance = character ? f.Resonance : (int?)null;
                f.Result.RampartCharges = character ? f.RampartCharges : (int?)null;
                f.Result.IsDead = f.IsDead;
                CombatantResult r = f.Result;
                EmitDiagnostic(tick, CombatPhase.EndOfTick, "combatantSummary", w => w
                    .Field("combatant", r.Id).Field("team", r.Team).Field("damageDealt", r.DamageDealt).Field("damageTaken", r.DamageTaken)
                    .Field("hits", r.Hits).Field("criticalHits", r.CriticalHits).Field("skillsCast", r.SkillsCast)
                    .Field("commandsRejected", r.CommandsRejected).Field("blocks", r.Blocks).Field("perfectParries", r.PerfectParries)
                    .Field("failedParries", r.FailedParries).Field("guardBreaks", r.GuardBreaks)
                    .Field("health", r.Health).Field("endurance", r.Endurance)
                    .Field("resonance", r.Resonance).Field("rampartCharges", r.RampartCharges).Field("dead", r.IsDead));
                f.Resonance = 0;
                f.RampartCharges = 0;
            }
            long durationMs = checked((tick + 1) * _tickMs);
            string reasonId = reason switch
            {
                CombatEndReason.DurationReached => "durationReached",
                CombatEndReason.TeamDefeated => "teamDefeated",
                _ => "aborted",
            };
            EmitDiagnostic(tick, CombatPhase.EndOfTick, "combatEnded", w => w
                .Field("reason", reasonId).Field("winner", winner).Field("ticks", tick + 1).Field("durationMs", durationMs)
                .Field("deferredDropped", _deferredPending));
            RecordTickPeaks();
            Result = new CombatResult(reason, abortReason, tick, durationMs, winner, _fighters.Select(f => f.Result).ToArray(),
                new CombatLoadPeaks(_peakWork, _peakEvents, _peakPending));
        }

        // --- Journal -----------------------------------------------------------------------------

        private void LogCombatant(Fighter f)
        {
            DerivedStats s = f.Stats;
            bool character = f.Character != null;
            string type = character ? "character" : f.Assailant != null ? "assailant" : "dummy";
            EmitDiagnostic(0, CombatPhase.Setup, "combatant", w =>
            {
                w.Field("combatant", f.Id).Field("team", f.Team).Field("type", type)
                    .Field("initiative", f.Setup.Initiative).Field("health", f.Health).Field("maxHealth", f.MaxHealth)
                    .Field("endurance", character ? f.Endurance : (long?)null).Field("maxEndurance", character ? f.MaxEndurance : (long?)null)
                    .Field("resonance", character ? f.Resonance : (long?)null).Field("rampartCharges", character ? f.RampartCharges : (long?)null)
                    .Field("physicalPower", s.PhysicalPower).Field("agilePower", s.AgilePower).Field("magicPower", s.MagicPower)
                    .Field("armor", s.Armor).Field("magicResistance", s.MagicResistance).Field("statusResistance", s.StatusResistance)
                    .Field("criticalChanceBasisPoints", s.CriticalChanceBasisPoints).Field("criticalMultiplier", s.CriticalMultiplier);
                w.Name("rules");
                if (character) WriteRules(w, f);
                else w.Value((string?)null);
            });
        }

        /// <summary>
        /// Sous-lot 8.1 (format 5) : règles effectives d'un personnage. Valeurs résultantes des clés de règle scalaires (les clés de
        /// statistiques, <c>endurance.max</c>, <c>maxHealth</c> et <c>statusResistance</c>, sont dans les champs du combattant) ; puis
        /// chaque clé paramétrée visée par un modificateur actif, dans l'ordre ordinal (coefficient et recharge effectifs pour une
        /// compétence, ajout en ms pour une durée d'altération) ; puis les modificateurs conditionnels agrégés (<c>conditional</c>).
        /// </summary>
        private static void WriteRules(JsonLineWriter w, Fighter f)
        {
            CombatantRules r = f.Rules;
            w.BeginObject()
                .Field("guard.damageReduction", r.GuardDamageReduction).Field("guard.upkeepPerSecond", r.GuardUpkeepPerSecond)
                .Field("guard.costPerBlock", r.GuardCostPerBlock).Field("guard.noUpkeepBreak", r.GuardNoUpkeepBreak)
                .Field("guard.reductionAtZeroEndurance", r.GuardReductionAtZeroEndurance).Field("guard.breakShortenPerCharge", r.GuardBreakShortenPerChargeMs)
                .Field("parry.windowMs", r.ParryWindowMs).Field("parry.failureRecoveryMs", r.ParryFailureRecoveryMs)
                .Field("rampartCharges.max", r.RampartChargesMax).Field("resonance.perDirectHit", r.ResonancePerDirectHit)
                .Field("endurance.regenPerSecond", r.EnduranceRegenPerSecond)
                .Field("damageDealt", r.DamageDealt).Field("damageTaken", r.DamageTaken);
            foreach (ModifierKey key in r.Modifiers.Keys)
            {
                switch (key.Field)
                {
                    case ModifierField.StatusDurationDealt:
                        w.Field(key.ToString(), r.DurationDealtMs(key.Parameter!));
                        break;
                    case ModifierField.StatusDurationTaken:
                        w.Field(key.ToString(), r.DurationTakenMs(key.Parameter!));
                        break;
                    case ModifierField.SkillCoefficient when f.Character!.Class.TryGetSkill(key.Parameter!, out SkillDefinition skill) && skill.Combat is { } combat:
                        w.Field(key.ToString(), r.Coefficient(skill, combat));
                        break;
                    case ModifierField.SkillCooldownMs when f.Character!.Class.TryGetSkill(key.Parameter!, out SkillDefinition skill) && skill.Combat is { } combat:
                        w.Field(key.ToString(), r.CooldownMs(skill, combat));
                        break;
                }
            }
            w.Name("conditional").BeginArray();
            foreach (ConditionalModifier c in r.Modifiers.Conditional)
            {
                w.BeginObject().Field("key", ModifierKeys.Specs[(int)c.Field].Name);
                w.Name("conditions").BeginArray();
                foreach (ModifierCondition condition in ModifierKeys.Of(c.Conditions)) w.Value(ModifierKeys.Of(condition));
                w.EndArray().Field("value", c.Value).EndObject();
            }
            w.EndArray().EndObject();
        }

        private static string PhaseId(CombatPhase phase) => phase switch
        {
            CombatPhase.Setup => "setup",
            CombatPhase.Expirations => "expirations",
            CombatPhase.Commands => "commands",
            CombatPhase.Resolution => "resolution",
            CombatPhase.Periodic => "periodic",
            CombatPhase.Regeneration => "regeneration",
            _ => "endOfTick",
        };

        // --- Budgets (lot 7.1) ---------------------------------------------------------------------

        /// <summary>
        /// Réserve le coût exact du traitement suivant, avant toute mutation. Ordre de vérification fixe : travail, file,
        /// lignes ; l'arrêt porte la raison du premier budget qui échoue.
        /// </summary>
        private void Reserve(Cost cost) => Reserve(cost, cost);

        /// <summary>
        /// Lot 7.3 : réservation majorée. Les budgets sont vérifiés contre <paramref name="bound"/> (coût maximal du traitement
        /// entier, par exemple une réaction et tous ses effets), mais seule la partie exacte <paramref name="exact"/> est ouverte ;
        /// les réservations exactes suivantes du même traitement tiennent alors toujours dans les budgets.
        /// </summary>
        private void Reserve(Cost exact, Cost bound) => Reserve(exact, bound, removing: 0);

        /// <summary>
        /// Correctif 7.3.1 (A73-M04) : traitement d'un événement programmé, en tête de la file. Réserve son coût, puis le retire. Le
        /// retrait est certain dès que la réservation passe (aucune mutation ne la précède) : la place qu'il libère est créditée au
        /// contrôle de capacité, qui porte sur l'occupation nette (file − 1 + insertions) et non sur le nombre d'insertions. Seul point
        /// de retrait des événements programmés : expirations, commandes, ticks de DOT, notifications et réactions.
        /// </summary>
        private Scheduled ReserveAndDequeue(Cost exact, Cost bound)
        {
            Reserve(exact, bound, removing: 1);
            return _queue.Dequeue();
        }

        private Scheduled ReserveAndDequeue(Cost cost) => ReserveAndDequeue(cost, cost);

        /// <summary>Occupation de la file après le retrait certain de <paramref name="removing"/> élément(s) en tête.</summary>
        private long OccupancyAfter(int removing) => _queue.Count - removing;

        private void Reserve(Cost exact, Cost bound, int removing)
        {
            CloseScope();
            if (checked(_workThisTick + bound.Work) > _rules.MaxWorkPerTick) throw new BudgetExceeded("workBudgetExceeded", _rules.MaxWorkPerTick);
            if (checked(OccupancyAfter(removing) + bound.Queue) > _rules.MaxPendingEvents) throw new BudgetExceeded("queueLimitExceeded", _rules.MaxPendingEvents);
            if (checked(_eventsThisTick + bound.Events) > _rules.MaxEventsPerTick) throw new BudgetExceeded("eventBudgetExceeded", _rules.MaxEventsPerTick);
            Cost cost = exact;
            _scopeWork = _workThisTick + cost.Work;
            _scopeEvents = _eventsThisTick + cost.Events;
            _scopeEnqueues = cost.Queue;
        }

        /// <summary>
        /// Termine le traitement réservé. Un dépassement de la réserve lève aussitôt une erreur (voir <see cref="Charge"/>) ;
        /// en Debug, une réserve non entièrement consommée en lève une aussi, ce qui vérifie l'exactitude des coûts prévus
        /// sur toute la suite de tests.
        /// </summary>
        private void CloseScope()
        {
#if DEBUG
            if (_workThisTick != _scopeWork || _eventsThisTick != _scopeEvents || _scopeEnqueues != 0)
                throw new InvalidOperationException(
                    $"Coût réservé inexact : travail {_workThisTick}/{_scopeWork}, lignes {_eventsThisTick}/{_scopeEvents}, mises en file restantes {_scopeEnqueues}.");
#endif
            _scopeWork = _workThisTick;
            _scopeEvents = _eventsThisTick;
            _scopeEnqueues = 0;
        }

        private void Charge(long work)
        {
            if (!_countEvents) return;
            _workThisTick += work;
            if (_workThisTick > _scopeWork)
                throw new InvalidOperationException($"Travail consommé au-delà de la réserve ({_workThisTick} > {_scopeWork}).");
        }

        /// <summary>Met un élément en file ; pendant un tick, cela coûte une unité de travail et une place réservées.</summary>
        private void Schedule(long tick, CombatPhase phase, Scheduled item)
        {
            if (_countEvents)
            {
                Charge(1);
                if (--_scopeEnqueues < 0) throw new InvalidOperationException("Mise en file non réservée.");
            }
            _queue.Enqueue(tick, phase, item);
            if (_queue.Count > _peakPending) _peakPending = _queue.Count;
        }

        private void RecordTickPeaks()
        {
            if (_workThisTick > _peakWork) _peakWork = _workThisTick;
            if (_eventsThisTick > _peakEvents) _peakEvents = _eventsThisTick;
        }

        /// <summary>
        /// Lot 7.3 (A6) : retient le premier effet qui amène les points de vie d'un combattant vivant à 0 ; la mort, constatée en
        /// phase 6, en hérite. Les impacts suivants du même tick sur un combattant déjà à 0 ne changent rien.
        /// </summary>
        private static void NoteLethal(Fighter target, Provenance impact)
        {
            if (target.Health == 0 && !target.IsDead && target.LethalCause is null) target.LethalCause = impact;
        }

        /// <summary>
        /// Écrit une ligne de jeu (lot 7.3) : un effet, avec son identité <c>eid</c> (compteur du combat, dans l'ordre de création,
        /// distinct de <c>seq</c>) et sa provenance compacte : <c>cause</c> toujours (null pour l'effet racine), <c>root</c> sur l'effet
        /// racine seulement, <c>depth</c> et <c>trigger</c> à partir de la profondeur 1. Renvoie l'eid, cause de ses conséquences.
        /// </summary>
        private long Emit(long tick, CombatPhase phase, string kind, Provenance origin, Action<JsonLineWriter> fields) =>
            Emit(tick, phase, kind, origin, null, fields);

        /// <summary>
        /// Variante pour un effet observable (lot 7.3) : après la ligne, l'instantané reçoit l'eid et la provenance, et sa notification
        /// est mise en file en fin de la phase courante si son type a un abonné possible (coût réservé par <see cref="Observable"/>).
        /// </summary>
        private long Emit(long tick, CombatPhase phase, string kind, Provenance origin, ObservedEvent? observed, Action<JsonLineWriter> fields)
        {
            long eid = ++_lastEid;
            WriteLine(tick, phase, kind, w =>
            {
                w.Field("eid", eid).Field("cause", origin.Cause);
                if (origin.Cause is null) w.Field("root", origin.Root);
                if (origin.Depth > 0) w.Field("depth", origin.Depth).Field("trigger", origin.Trigger);
                if (_deferredFrom is long from) w.Field("deferredFromTick", from);
                fields(w);
            });
            if (observed != null && Subscribed(observed))
            {
                observed.Eid = eid;
                observed.Tick = tick;
                observed.Phase = phase;
                observed.Provenance = origin;
                observed.Candidates = Candidates(observed);
                Schedule(tick, phase, new NotifyItem { Event = observed });
            }
            return eid;
        }

        /// <summary>
        /// Écrit un diagnostic ou une ligne d'enveloppe (lot 7.3, §1.1) : ni eid ni provenance, jamais présenté à un abonné. Une
        /// méthode distincte de <see cref="Emit"/> garantit qu'aucun diagnostic ne devient un effet observable.
        /// </summary>
        private void EmitDiagnostic(long tick, CombatPhase phase, string kind, Action<JsonLineWriter> fields) => WriteLine(tick, phase, kind, fields);

        private void WriteLine(long tick, CombatPhase phase, string kind, Action<JsonLineWriter> fields)
        {
            if (_countEvents)
            {
                Charge(1);
                if (++_eventsThisTick > _scopeEvents)
                    throw new InvalidOperationException($"Ligne écrite au-delà de la réserve ({_eventsThisTick} > {_scopeEvents}).");
            }
            var w = new JsonLineWriter().BeginObject()
                .Field("tick", tick).Field("seq", _sequence++).Field("phase", PhaseId(phase)).Field("event", kind);
            fields(w);
            _log.Append(w.EndObject().ToString());
        }
    }
}
