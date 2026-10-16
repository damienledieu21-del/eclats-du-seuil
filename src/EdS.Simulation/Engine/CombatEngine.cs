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
    public sealed class CombatEngine
    {
        /// <summary>Version du moteur, inscrite au journal : un rejeu exige la même version.</summary>
        public const string Version = "0.8.1";

        public const string LogFormat = "eds-combat-log";

        /// <summary>
        /// Version du format du journal. 2 (correctif 6.1) : <c>stateApplied</c> et <c>stateExpired</c> portent
        /// <c>instance</c> et <c>generation</c> ; nouvel événement <c>stateRemoved</c>. 3 (lot 7.2) : toutes les lignes d'état portent
        /// <c>category</c> et <c>source</c> ; nouveaux événements <c>stateBlocked</c> et <c>dotDamage</c> ; <c>attackStarted</c> porte
        /// <c>applies</c> (liste des altérations) au lieu de <c>appliesFragility</c> ; garde interrompue (<c>guardStopped</c>, raison
        /// <c>interrupted</c>) ; motifs de refus et de saut issus des altérations. Un journal d'un autre format est refusé au rejeu.
        /// </summary>
        public const int LogFormatVersion = 3;

        // Identifiants stables du journal.
        public const string Resonance = "resonance";
        public const string RampartCharges = "rampartCharges";

        private sealed class Fighter
        {
            public CombatantSetup Setup = null!;
            public CharacterSetup? Character;
            public AssailantSetup? Assailant;
            public DerivedStats Stats = null!;
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
        }

        private sealed class PendingAction
        {
            public Fighter Actor = null!;
            public SkillDefinition? Skill;
            public SkillCombatDefinition? Combat;
            public AssailantAttack? Attack;
            public Fighter? Target;

            /// <summary>Faux pour une action qui dépense de la Résonance : pas de génération récursive (A5).</summary>
            public bool GeneratesResonance = true;

            public SkillTargeting Targeting => Combat?.Targeting ?? Attack!.Targeting;
            public bool Unblockable => Combat?.Unblockable ?? Attack!.Unblockable;
            public IReadOnlyList<StatusApplication> Applies => Combat?.Applies ?? Attack!.Applies;
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
        private readonly long _parryWindowTicks;
        private readonly long _parryRecoveryTicks;
        private readonly StatusCatalog _statuses;
        private readonly Dictionary<string, int> _statusOrder = new Dictionary<string, int>(StringComparer.Ordinal);
        private long _sequence;
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
        internal CombatEngine(CombatSetup setup, ICombatLog log, bool queueBound)
        {
            _setup = setup ?? throw new ArgumentNullException(nameof(setup));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            var violations = setup.Validate(queueBound);
            if (violations.Count > 0) throw new CombatSetupException(violations);

            _rules = setup.Combat;
            _tickMs = _rules.TickMilliseconds;
            _durationTicks = setup.DurationMs / _tickMs;
            _globalCooldownTicks = _rules.GlobalCooldownMs / _tickMs;
            _regenDelayTicks = _rules.EnduranceRegenDelayMs / _tickMs;
            _parryWindowTicks = _rules.ParryWindowMs / _tickMs;
            _parryRecoveryTicks = _rules.ParryFailureRecoveryMs / _tickMs;
            _statuses = _rules.Statuses;
            for (int i = 0; i < _statuses.Definitions.Count; i++) _statusOrder[_statuses.Definitions[i].Id] = i;

            _fighters = setup.Combatants.Select(CreateFighter).ToList();
            _fightersById = _fighters.OrderBy(f => f.Id, StringComparer.Ordinal).ToList();
            _byId = _fighters.ToDictionary(f => f.Id, StringComparer.Ordinal);

            for (int i = 0; i < setup.Commands.Count; i++)
            {
                CombatCommand command = setup.Commands[i];
                Schedule(command.AtMs / _tickMs, CombatPhase.Commands, new CommandItem { Index = i, Command = command });
            }
            foreach (Fighter f in _fighters)
                if (f.Assailant is { } assailant && assailant.FirstAttackMs / _tickMs < _durationTicks)
                    Schedule(assailant.FirstAttackMs / _tickMs, CombatPhase.Commands, new AssailantTurn { Fighter = f });

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
                    f.Stats = CombatantStats.ForCharacter(_setup.Stats, character.Build);
                    f.MaxHealth = f.Stats.MaxHealth.RoundToInt(RoundingMode.Floor);
                    f.Health = f.MaxHealth;
                    f.MaxEndurance = f.Stats.MaxEndurance.RoundToInt(RoundingMode.Floor);
                    f.Endurance = f.MaxEndurance;
                    foreach (string skill in character.Build.ActiveSkills) f.Castable.Add(skill);
                    if (character.Build.Ultimate != null) f.Castable.Add(character.Build.Ultimate);
                    break;
                case DummySetup dummy:
                    f.Stats = CombatantStats.ForDefenses(_setup.Stats, dummy.Armor, dummy.MagicResistance, dummy.StatusResistance);
                    break;
                case AssailantSetup assailant:
                    f.Assailant = assailant;
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
                Reserve(Cost.Op + ExpirationCost(tick, item));
                _queue.Dequeue();
                Charge(1);
                switch (item)
                {
                    case CooldownExpiry expiry:
                        if (expiry.Fighter.CooldownReadyTick.TryGetValue(expiry.Skill, out long ready) && ready == tick)
                            Emit(tick, CombatPhase.Expirations, "cooldownReady", w => w.Field("actor", expiry.Fighter.Id).Field("skill", expiry.Skill));
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
                f.ParryRecoveryUntilTick = checked(tick + _parryRecoveryTicks);
                recoveryUntil = f.ParryRecoveryUntilTick;
                f.Result.FailedParries++;
            }
            Emit(tick, CombatPhase.Expirations, "parryEnded", w => w
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
                Reserve(Cost.Op);
                _queue.Dequeue();
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
                PendingAction? action = entry.Item is CommandItem command ? Execute(tick, command) : AssailantAttacks(tick, entry.Actor);
                if (action != null) actions.Add(action);
            }
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
                    return Cost.Enqueue.If(combat.CooldownMs / _tickMs > 0) + Cost.Line + GainCost(actor, combat.RampartChargeGain);
            }
        }

        /// <summary>Coût exact du tour d'un assaillant : prochain tour s'il tombe avant la fin, et une ligne.</summary>
        private Cost AssailantTurnCost(long tick, Fighter assailant) =>
            Cost.Enqueue.If(checked(tick + assailant.Assailant!.IntervalMs / _tickMs) < _durationTicks) + Cost.Line;

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
            switch (command.Action)
            {
                case CombatAction.GuardStart:
                    if (Reject(tick, item, actor, CheckGuardStart(tick, actor))) return null;
                    actor.Guarding = true;
                    Emit(tick, CombatPhase.Commands, "guardStarted", w => w.Field("actor", actor.Id).Field("command", item.Index).Field("endurance", actor.Endurance));
                    return null;

                case CombatAction.GuardStop:
                    if (Reject(tick, item, actor, CheckAlive(actor) ?? (actor.Guarding ? null : "guardNotActive"))) return null;
                    actor.Guarding = false;
                    ResumeRegeneration(actor, tick);
                    Emit(tick, CombatPhase.Commands, "guardStopped", w => w.Field("actor", actor.Id).Field("command", item.Index).Field("endurance", actor.Endurance));
                    return null;

                case CombatAction.Parry:
                    if (Reject(tick, item, actor, CheckParry(tick, actor))) return null;
                    actor.ParryOpen = true;
                    actor.ParryStartTick = tick;
                    actor.ParryEndTick = checked(tick + _parryWindowTicks);
                    actor.ParrySucceeded = false;
                    Schedule(actor.ParryEndTick, CombatPhase.Expirations, new ParryWindowEnd { Fighter = actor, StartTick = tick });
                    Emit(tick, CombatPhase.Commands, "parryStarted", w => w
                        .Field("actor", actor.Id).Field("command", item.Index).Field("windowEndTick", actor.ParryEndTick).Field("guarding", actor.Guarding));
                    return null;

                default:
                    return CastSkill(tick, item, actor);
            }
        }

        private PendingAction? CastSkill(long tick, CommandItem item, Fighter actor)
        {
            CombatCommand command = item.Command;
            var (skill, target, resonanceCost) = CastContext(item, actor);

            if (Reject(tick, item, actor, CheckSkill(tick, actor, skill, target, resonanceCost))) return null;

            SkillCombatDefinition combat = skill!.Combat!;
            if (combat.EnduranceCost > 0) SpendEndurance(actor, combat.EnduranceCost, tick);
            actor.Resonance -= resonanceCost;
            actor.RampartCharges -= combat.RampartChargeCost;
            actor.GlobalCooldownReadyTick = checked(tick + _globalCooldownTicks);
            long cooldownTicks = combat.CooldownMs / _tickMs;
            long readyTick = checked(tick + cooldownTicks);
            if (cooldownTicks > 0)
            {
                actor.CooldownReadyTick[skill.Id] = readyTick;
                Schedule(readyTick, CombatPhase.Expirations, new CooldownExpiry { Fighter = actor, Skill = skill.Id });
            }
            actor.Result.SkillsCast++;
            Emit(tick, CombatPhase.Commands, "skillCast", w => w
                .Field("actor", actor.Id).Field("skill", skill.Id).Field("target", command.Target).Field("command", item.Index)
                .Field("enduranceCost", combat.EnduranceCost).Field("endurance", actor.Endurance)
                .Field("resonanceCost", resonanceCost).Field("resonance", actor.Resonance)
                .Field("rampartChargeCost", combat.RampartChargeCost).Field("rampartCharges", actor.RampartCharges)
                .Field("cooldownReadyTick", readyTick).Field("globalCooldownReadyTick", actor.GlobalCooldownReadyTick));
            Gain(tick, CombatPhase.Commands, actor, RampartCharges, combat.RampartChargeGain, "skill");
            return new PendingAction { Actor = actor, Skill = skill, Combat = combat, Target = target, GeneratesResonance = resonanceCost == 0 };
        }

        private bool Reject(long tick, CommandItem item, Fighter actor, string? reason)
        {
            if (reason is null) return false;
            CombatCommand command = item.Command;
            actor.Result.CommandsRejected++;
            Emit(tick, CombatPhase.Commands, "commandRejected", w => w
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
        private PendingAction? AssailantAttacks(long tick, Fighter assailant)
        {
            AssailantSetup setup = assailant.Assailant!;
            AssailantAttack attack = setup.Attacks[assailant.NextAttack];
            assailant.NextAttack = (assailant.NextAttack + 1) % setup.Attacks.Count;
            long next = checked(tick + setup.IntervalMs / _tickMs);
            if (next < _durationTicks) Schedule(next, CombatPhase.Commands, new AssailantTurn { Fighter = assailant });

            Fighter? target = attack.Targeting == SkillTargeting.Enemy ? _byId[setup.Target!] : null;
            // Lot 7.2 : un assaillant sous contrôle saute son attaque ; l'attaque suivante de sa liste et sa cadence ne changent pas.
            if (BlockReason(assailant, StatusBlocks.AssailantAttacks) is { } blocked)
            {
                Emit(tick, CombatPhase.Commands, "attackSkipped", w => w
                    .Field("actor", assailant.Id).Field("attack", attack.Id).Field("target", target?.Id).Field("reason", blocked));
                return null;
            }
            if (target is { IsDead: true })
            {
                Emit(tick, CombatPhase.Commands, "attackSkipped", w => w
                    .Field("actor", assailant.Id).Field("attack", attack.Id).Field("target", target.Id).Field("reason", "targetDead"));
                return null;
            }
            Emit(tick, CombatPhase.Commands, "attackStarted", w => w
                .Field("actor", assailant.Id).Field("attack", attack.Id).Field("target", target?.Id)
                .Field("rawDamage", attack.RawDamage).Field("unblockable", attack.Unblockable).Names("applies", attack.Applies.Select(a => a.Status)));
            return new PendingAction { Actor = assailant, Attack = attack, Target = target, GeneratesResonance = false };
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
                Cost cost = resonance ? GainCost(action.Actor, _rules.ResonancePerDirectHit) : Cost.None;
                foreach (Fighter target in targets) cost += HitCost(tick, action, target);
                Reserve(cost);
                foreach (Fighter target in targets) Hit(tick, action, target);
                if (resonance) Gain(tick, CombatPhase.Resolution, action.Actor, Resonance, _rules.ResonancePerDirectHit, "directHit");
            }
        }

        /// <summary>
        /// Coût exact d'un impact, déduit de l'état de la cible avant l'impact, avec les mêmes branches que <see cref="Hit"/> :
        /// défense, puis chaque altération portée dans l'ordre, avec l'état de garde et de parade laissé par les étapes précédentes
        /// (une rupture de garde ou un étourdissement change ce que les suivantes interrompent).
        /// </summary>
        private Cost HitCost(long tick, PendingAction action, Fighter target)
        {
            Cost cost = Cost.Op + Cost.Line;   // impact, ligne « damage »
            bool guarding = target.Guarding, parryOpen = target.ParryOpen;
            bool defending = target.Character != null && !target.IsDead && (target.ParryOpen || target.Guarding);
            bool parried = false;
            if (defending && action.Unblockable) { }
            else if (defending && target.ParryOpen)
            {
                parried = true;
                if (!target.ParrySucceeded)
                    cost += Cost.Line + GainCost(target, _rules.RampartChargesPerParry) + GainCost(target, _rules.ResonancePerPerfectParry);
            }
            else if (defending)
            {
                if (target.Endurance < _rules.GuardCostPerBlock)
                {
                    cost += BreakGuardCost(tick, target, action.Actor.Id);
                    guarding = parryOpen = false;
                }
                else cost += Cost.Line + GainCost(target, _rules.ResonancePerBlock);
            }
            foreach (StatusApplication application in action.Applies)
            {
                StatusDefinition definition = _statuses[application.Status];
                if (parried)
                {
                    cost += Cost.Line;   // stateBlocked « parried »
                    continue;
                }
                StatusPlan plan = Plan(tick, target, definition, action.Actor.Id, null);
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

        private void Hit(long tick, PendingAction action, Fighter target)
        {
            Charge(1);
            Fighter actor = action.Actor;
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
                    Emit(tick, CombatPhase.Resolution, "parrySucceeded", w => w.Field("actor", target.Id).Field("source", actor.Id));
                }
            }
            else if (defending)
            {
                if (target.Endurance < _rules.GuardCostPerBlock)
                {
                    BreakGuard(tick, CombatPhase.Resolution, target, "insufficientEndurance", actor.Id);
                    defense = "guardBroken";
                }
                else
                {
                    SpendEndurance(target, _rules.GuardCostPerBlock, tick);
                    target.Result.Blocks++;
                    defense = "guard";
                    mitigation = _rules.GuardDamageReduction;
                    blockReward = true;
                    Emit(tick, CombatPhase.Resolution, "blocked", w => w
                        .Field("actor", target.Id).Field("source", actor.Id).Field("enduranceCost", _rules.GuardCostPerBlock).Field("endurance", target.Endurance));
                }
            }

            DerivedStats attacker = AttackerStats(action);
            DamageInput input = action.Combat is { } combat
                ? new DamageInput(attacker, target.Stats, combat.PowerType, combat.DamageType, combat.Coefficient, bonuses, critical, mitigation)
                : new DamageInput(attacker, target.Stats, CombatantStats.PowerOf(action.Attack!.DamageType),
                    action.Attack.DamageType, Fixed.One, bonuses, false, mitigation);
            long amount = DamageCalculator.Compute(input, _setup.Damage).FinalDamage;

            long overkill = 0;
            if (target.Health is long health)
            {
                overkill = Math.Max(0, amount - health);
                target.Health = Math.Max(0, health - amount);
            }
            target.Result.DamageTaken = checked(target.Result.DamageTaken + amount);
            actor.Result.DamageDealt = checked(actor.Result.DamageDealt + amount);
            actor.Result.Hits++;
            if (critical) actor.Result.CriticalHits++;
            Emit(tick, CombatPhase.Resolution, "damage", w => w
                .Field("source", actor.Id).Field("target", target.Id).Field("skill", action.Skill?.Id).Field("attack", action.Attack?.Id)
                .Field("critical", critical).Field("fragile", fragile).Field("defense", defense).Field("amount", amount).Field("overkill", overkill)
                .Field("health", target.Health).Field("damageTaken", target.Result.DamageTaken));

            if (parryReward)
            {
                Gain(tick, CombatPhase.Resolution, target, RampartCharges, _rules.RampartChargesPerParry, "perfectParry");
                Gain(tick, CombatPhase.Resolution, target, Resonance, _rules.ResonancePerPerfectParry, "perfectParry");
            }
            if (blockReward) Gain(tick, CombatPhase.Resolution, target, Resonance, _rules.ResonancePerBlock, "block");

            // Lot 7.2 : altérations portées, après les dégâts, dans l'ordre de la liste. Un DOT fige sa puissance maintenant.
            // Correctif 7.2 : un impact paré (parade parfaite, réussie à cet impact ou plus tôt dans la même fenêtre) n'applique
            // aucune altération (stateBlocked « parried ») ; un impact bloqué par la garde ou imparable les applique.
            foreach (StatusApplication application in action.Applies)
            {
                StatusDefinition definition = _statuses[application.Status];
                if (defense == "parry")
                {
                    Emit(tick, CombatPhase.Resolution, "stateBlocked", w => w
                        .Field("combatant", target.Id).Field("state", definition.Id).Field("category", StatusIds.Of(definition.Category))
                        .Field("source", actor.Id).Field("reason", "parried"));
                    continue;
                }
                Fixed power = definition.IsDot ? DotPower(attacker, definition, application) : Fixed.Zero;
                ApplyStatus(tick, CombatPhase.Resolution, target, definition, actor.Id, power);
            }
        }

        /// <summary>Statistiques offensives d'une action : celles du personnage, ou celles de l'attaque d'assaillant (dégâts bruts).</summary>
        private static DerivedStats AttackerStats(PendingAction action) =>
            action.Combat != null ? action.Actor.Stats : action.Actor.AttackStats[action.Attack!.Id];

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
        private StatusPlan Plan(long tick, Fighter target, StatusDefinition definition, string? source, StatusInstance? excluding)
        {
            var key = new StatusKey(definition.Id, definition.Stacking == StatusStacking.PerSourceKeepStronger ? source : null);
            target.Statuses.TryGetValue(key, out StatusInstance? existing);
            if (existing == excluding) existing = null;
            foreach (StatusInstance status in target.Statuses.Values)
                if (status != excluding && status.Definition.ImmuneTo.Contains(definition.Id))
                    return new StatusPlan(StatusOutcome.Immune, key, existing, 0);
            long ticks = DurationTicks(definition, target);
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
        /// </summary>
        private long DurationTicks(StatusDefinition definition, Fighter target)
        {
            long baseTicks = definition.DurationMs / _tickMs;
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
        private static Cost ApplyCost(StatusPlan plan, StatusDefinition definition, bool guarding, bool parryOpen)
        {
            if (!plan.Applies) return Cost.Line;
            bool requeue = !(definition.IsDot && plan.Outcome == StatusOutcome.Renew);
            return Cost.Op + Cost.Enqueue.If(requeue) + Cost.Line
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
            StatusInstance? excluding = null)
        {
            StatusPlan plan = Plan(tick, target, definition, source, excluding);
            if (!plan.Applies)
            {
                Emit(tick, phase, "stateBlocked", w => w
                    .Field("combatant", target.Id).Field("state", definition.Id).Field("category", StatusIds.Of(definition.Category))
                    .Field("source", source).Field("reason", plan.Reason));
                return;
            }
            Charge(1);
            bool renewed = plan.Outcome == StatusOutcome.Renew;
            StatusInstance status = plan.Existing ?? new StatusInstance { Id = ++_lastStatusInstance, Definition = definition, Key = plan.Key, Owner = target };
            if (!renewed) target.Statuses[plan.Key] = status;
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
            Emit(tick, phase, "stateApplied", w =>
            {
                StateFields(w, target, status).Field("untilTick", status.UntilTick).Field("renewed", renewed);
                if (definition.IsDot) w.Field("power", status.Power).Field("nextTick", status.NextTick);
                if (definition.MovementSpeedModifier != Fixed.Zero) w.Field("movementSpeedModifier", definition.MovementSpeedModifier);
            });

            if ((definition.Interrupts & StatusInterrupts.Guard) != 0 && target.Guarding)
            {
                target.Guarding = false;
                ResumeRegeneration(target, tick);
                Emit(tick, phase, "guardStopped", w => w
                    .Field("actor", target.Id).Field("command", (long?)null).Field("endurance", target.Endurance)
                    .Field("reason", "interrupted").Field("state", definition.Id));
            }
            if ((definition.Interrupts & StatusInterrupts.Parry) != 0 && target.ParryOpen)
            {
                target.ParryOpen = false;
                Emit(tick, phase, "parryEnded", w => w
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

        /// <summary>Champs communs des lignes d'état (format 3) : cible, altération, catégorie, source, instance, génération.</summary>
        private static JsonLineWriter StateFields(JsonLineWriter w, Fighter target, StatusInstance status) => w
            .Field("combatant", target.Id).Field("state", status.State).Field("category", StatusIds.Of(status.Definition.Category))
            .Field("source", status.Source).Field("instance", status.Id).Field("generation", status.Generation);

        /// <summary>Coût de la fin d'une instance par expiration : retrait (1), ligne <c>stateExpired</c>, état accordé éventuel.</summary>
        private Cost EndStatusCost(long tick, StatusInstance status)
        {
            Cost cost = Cost.Op + Cost.Line;
            Fighter target = status.Owner;
            if (status.Definition.GrantsOnExpiry is { } granted)
            {
                StatusDefinition definition = _statuses[granted];
                cost += ApplyCost(Plan(tick, target, definition, status.Source, status), definition, target.Guarding, target.ParryOpen);
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
            Emit(tick, phase, "stateExpired", w => StateFields(w, target, status));
            if ((status.Definition.Blocks & StatusBlocks.Regeneration) != 0) ResumeRegeneration(target, tick);
            if (status.Definition.GrantsOnExpiry is { } granted)
                ApplyStatus(tick, phase, target, _statuses[granted], status.Source, Fixed.Zero);
        }

        private Cost BreakGuardCost(long tick, Fighter f, string? source)
        {
            StatusDefinition definition = _statuses[_rules.GuardBreakStatus];
            return Cost.Line + Cost.Line.If(f.ParryOpen) + ApplyCost(Plan(tick, f, definition, source, null), definition, false, false);
        }

        /// <summary>
        /// Rupture de garde (A1, A19) : fin de la garde et de toute fenêtre de parade, puis l'altération de rupture
        /// (<c>combat.guard.breakStatus</c>) : ni garde ni parade ni régénération pendant sa durée.
        /// </summary>
        private void BreakGuard(long tick, CombatPhase phase, Fighter f, string reason, string? source)
        {
            f.Guarding = false;
            f.Result.GuardBreaks++;
            Emit(tick, phase, "guardBroken", w => w.Field("actor", f.Id).Field("reason", reason).Field("endurance", f.Endurance));
            if (f.ParryOpen)
            {
                f.ParryOpen = false;
                Emit(tick, phase, "parryEnded", w => w
                    .Field("actor", f.Id).Field("outcome", f.ParrySucceeded ? "success" : "interrupted").Field("recoveryUntilTick", (long?)null));
            }
            ApplyStatus(tick, phase, f, _statuses[_rules.GuardBreakStatus], source, Fixed.Zero);
        }

        // --- Ressources --------------------------------------------------------------------------

        /// <summary>Coût d'un gain : une ligne s'il a lieu (montant positif, personnage), même si tout est perdu au plafond.</summary>
        private static Cost GainCost(Fighter f, int amount) => Cost.Line.If(amount > 0 && f.Character != null);

        /// <summary>Gain plafonné d'une ressource (A4, A5) ; l'excédent est perdu et journalisé. Un gain ne déclenche jamais d'autre gain.</summary>
        private void Gain(long tick, CombatPhase phase, Fighter f, string resource, int amount, string source)
        {
            if (amount <= 0 || f.Character is null) return;
            bool resonance = resource == Resonance;
            int before = resonance ? f.Resonance : f.RampartCharges;
            int max = resonance ? _rules.ResonanceMax : _rules.RampartChargesMax;
            int after = (int)Math.Min(max, (long)before + amount);
            if (resonance) f.Resonance = after; else f.RampartCharges = after;
            Emit(tick, phase, "resourceGained", w => w
                .Field("actor", f.Id).Field("resource", resource).Field("source", source)
                .Field("amount", after - before).Field("wasted", (long)before + amount - after).Field("value", after));
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
        /// </summary>
        private void RunPeriodic(long tick)
        {
            // Lot 7.2 : ticks de DOT d'abord, dans l'ordre de la file (ordre de programmation), puis l'entretien des gardes.
            while (_queue.TryPeek(tick, CombatPhase.Periodic, out Scheduled item))
            {
                var dot = (DotTick)item;
                Reserve(Cost.Op + DotTickCost(tick, dot));
                _queue.Dequeue();
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
                long due = checked(_rules.GuardUpkeepPerSecond * guardedTicks * _tickMs) / 1000;
                long amount = due - f.GuardUpkeepPaid;
                bool breaks = amount > 0 && f.Endurance < amount;
                Reserve(Cost.Op + (breaks ? BreakGuardCost(tick, f, null) : Cost.Line.If(amount > 0)));
                Charge(1);
                if (breaks)
                {
                    BreakGuard(tick, CombatPhase.Periodic, f, "upkeep", null);
                    continue;
                }
                f.GuardedTicks = guardedTicks;
                if (amount <= 0) continue;
                SpendEndurance(f, amount, tick);
                f.GuardUpkeepPaid = due;
                Emit(tick, CombatPhase.Periodic, "guardUpkeep", w => w.Field("actor", f.Id).Field("amount", amount).Field("endurance", f.Endurance));
            }
        }

        private static bool IsCurrent(DotTick dot, out StatusInstance status) =>
            dot.Fighter.Statuses.TryGetValue(dot.Key, out status!) && status.Id == dot.Instance;

        /// <summary>Le tick en cours est-il le dernier de l'instance (le suivant dépasserait sa fin) ?</summary>
        private static bool IsLastDotTick(long tick, StatusInstance status) => tick >= status.UntilTick;

        /// <summary>
        /// Coût exact d'un tick de DOT hors retrait de la file : périmé (instance retirée à la mort), rien ; sinon l'impact (1) et la
        /// ligne <c>dotDamage</c>, puis soit le tick suivant mis en file, soit la fin de l'instance (retrait, ligne, état accordé).
        /// </summary>
        private Cost DotTickCost(long tick, DotTick dot)
        {
            if (!IsCurrent(dot, out StatusInstance? status)) return Cost.None;
            return Cost.Op + Cost.Line + (IsLastDotTick(tick, status) ? EndStatusCost(tick, status) : Cost.Enqueue);
        }

        /// <summary>
        /// Tick de DOT (A12, décisions 1, 2, 4) : dégâts = puissance figée réduite par la défense <b>actuelle</b> de la cible pour le
        /// type de l'altération (armure pour un saignement, résistance magique pour une brûlure), sans critique, ni garde, ni parade,
        /// ni Fragilité (dégâts directs seulement), ni Résonance, ni compteur de coups. Crédités à la source même morte. Le dernier
        /// tick inflige ses dégâts puis expire l'instance dans la même étape : 6 s / 2 s donnent exactement 3 ticks.
        /// </summary>
        private void RunDotTick(long tick, DotTick dot)
        {
            if (!IsCurrent(dot, out StatusInstance? status)) return;
            Charge(1);
            Fighter target = dot.Fighter;
            StatusDefinition definition = status.Definition;
            long amount = DamageCalculator.ComputePeriodic(status.Power, target.Stats, definition.DamageType!.Value,
                definition.Resistance == StatusResistanceMode.Defense, _setup.Damage);
            long overkill = 0;
            if (target.Health is long health)
            {
                overkill = Math.Max(0, amount - health);
                target.Health = Math.Max(0, health - amount);
            }
            target.Result.DamageTaken = checked(target.Result.DamageTaken + amount);
            if (status.Source != null && _byId.TryGetValue(status.Source, out Fighter? source))
                source.Result.DamageDealt = checked(source.Result.DamageDealt + amount);
            Emit(tick, CombatPhase.Periodic, "dotDamage", w => w
                .Field("source", status.Source).Field("target", target.Id).Field("state", status.State).Field("instance", status.Id)
                .Field("amount", amount).Field("overkill", overkill).Field("health", target.Health).Field("damageTaken", target.Result.DamageTaken));
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
            int perSecond = _rules.EnduranceRegenPerSecond;
            foreach (Fighter f in _fightersById)
            {
                long regenTicks = f.RegenFromTick is long from ? tick - from + 1 : 0;
                bool eligible = f.Character != null && !f.IsDead && f.RegenFromTick != null && !f.Guarding
                    && BlockReason(f, StatusBlocks.Regeneration) is null && regenTicks > 0;
                long target = eligible ? checked(perSecond * regenTicks * _tickMs) / 1000 : 0;
                long gain = eligible ? Math.Min(target - f.RegenGranted, f.MaxEndurance - f.Endurance) : 0;
                Reserve(Cost.Op + Cost.Line.If(gain > 0));
                Charge(1);
                if (!eligible) continue;
                f.RegenGranted = target;
                if (gain > 0)
                {
                    f.Endurance += gain;
                    Emit(tick, CombatPhase.Regeneration, "enduranceRegenerated", w => w.Field("actor", f.Id).Field("amount", gain).Field("endurance", f.Endurance));
                }
                if (f.Endurance >= f.MaxEndurance) f.RegenFromTick = null;   // plein : plus rien à régénérer jusqu'à la prochaine dépense
            }
        }

        // --- Phase 6 : morts et fin de combat ----------------------------------------------------

        private void RunEndOfTick(long tick)
        {
            foreach (Fighter f in _fightersById)
            {
                bool dies = !f.IsDead && f.Health == 0;
                Reserve(Cost.Op + (dies ? Cost.Line + f.Statuses.Count * (Cost.Op + Cost.Line) : Cost.None));
                Charge(1);
                if (dies)
                {
                    f.IsDead = true;
                    f.Guarding = false;
                    f.ParryOpen = false;
                    Emit(tick, CombatPhase.EndOfTick, "died", w => w.Field("combatant", f.Id));
                    // M02 : la mort retire les états actifs (ordre ordinal de l'altération, puis de la source) ; leurs échéances et
                    // ticks deviennent périmés ; aucun état n'est accordé sur un retrait.
                    foreach (StatusInstance status in f.Statuses.Values
                        .OrderBy(x => x.State, StringComparer.Ordinal).ThenBy(x => x.Source ?? string.Empty, StringComparer.Ordinal).ToList())
                    {
                        f.Statuses.Remove(status.Key);
                        Charge(1);
                        Emit(tick, CombatPhase.EndOfTick, "stateRemoved", w => StateFields(w, f, status).Field("reason", "death"));
                    }
                }
            }

            var aliveTeams = _fighters.Where(f => !f.IsDead).Select(f => f.Team).Distinct(StringComparer.Ordinal).ToList();
            if (aliveTeams.Count <= 1)
                Finish(tick, CombatEndReason.TeamDefeated, null, aliveTeams.Count == 1 ? aliveTeams[0] : null);
            else if (tick >= _durationTicks - 1)
                Finish(tick, CombatEndReason.DurationReached, null, null);
        }

        private void Abort(long tick, string reason, long? limit)
        {
            _countEvents = false;
            Emit(tick, CombatPhase.EndOfTick, "simulationAborted", w => w.Field("reason", reason).Field("limit", limit));
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
                Emit(tick, CombatPhase.EndOfTick, "combatantSummary", w => w
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
            Emit(tick, CombatPhase.EndOfTick, "combatEnded", w => w
                .Field("reason", reasonId).Field("winner", winner).Field("ticks", tick + 1).Field("durationMs", durationMs));
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
            Emit(0, CombatPhase.Setup, "combatant", w => w
                .Field("combatant", f.Id).Field("team", f.Team).Field("type", type)
                .Field("initiative", f.Setup.Initiative).Field("health", f.Health).Field("maxHealth", f.MaxHealth)
                .Field("endurance", character ? f.Endurance : (long?)null).Field("maxEndurance", character ? f.MaxEndurance : (long?)null)
                .Field("resonance", character ? f.Resonance : (long?)null).Field("rampartCharges", character ? f.RampartCharges : (long?)null)
                .Field("physicalPower", s.PhysicalPower).Field("agilePower", s.AgilePower).Field("magicPower", s.MagicPower)
                .Field("armor", s.Armor).Field("magicResistance", s.MagicResistance)
                .Field("criticalChanceBasisPoints", s.CriticalChanceBasisPoints).Field("criticalMultiplier", s.CriticalMultiplier));
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
        private void Reserve(Cost cost)
        {
            CloseScope();
            if (checked(_workThisTick + cost.Work) > _rules.MaxWorkPerTick) throw new BudgetExceeded("workBudgetExceeded", _rules.MaxWorkPerTick);
            if (checked(_queue.Count + cost.Queue) > _rules.MaxPendingEvents) throw new BudgetExceeded("queueLimitExceeded", _rules.MaxPendingEvents);
            if (checked(_eventsThisTick + cost.Events) > _rules.MaxEventsPerTick) throw new BudgetExceeded("eventBudgetExceeded", _rules.MaxEventsPerTick);
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

        private void Emit(long tick, CombatPhase phase, string kind, Action<JsonLineWriter> fields)
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
