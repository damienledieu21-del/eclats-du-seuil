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
    /// parfaite, Charges de rempart, Résonance, Fragilité, rupture de garde et assaillant d'entraînement (lot 6).
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
        public const string Version = "0.7.0";

        public const string LogFormat = "eds-combat-log";

        /// <summary>
        /// Version du format du journal. 2 (correctif 6.1) : <c>stateApplied</c> et <c>stateExpired</c> portent
        /// <c>instance</c> et <c>generation</c> ; nouvel événement <c>stateRemoved</c>. Un journal d'un autre format est refusé au rejeu.
        /// </summary>
        public const int LogFormatVersion = 2;

        // Identifiants stables du journal.
        public const string Resonance = "resonance";
        public const string RampartCharges = "rampartCharges";
        public const string Fragility = "fragility";
        public const string GuardBreak = "guardBreak";

        private static bool AppliesFragility(AssailantAttack attack) => attack.Applies.Any(a => a.Status == Fragility);

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

            /// <summary>États temporaires actifs, au plus une instance par état (non cumulables) ; voir <see cref="StatusInstance"/>.</summary>
            public readonly Dictionary<string, StatusInstance> Statuses = new Dictionary<string, StatusInstance>(StringComparer.Ordinal);

            public int Resonance;
            public int RampartCharges;

            public Pcg32 Random = null!;
            public bool IsDead;
            public readonly CombatantResult Result = new CombatantResult();

            public string Id => Setup.Id;
            public string Team => Setup.Team;
            public bool IsGuardBroken => Statuses.ContainsKey(GuardBreak);
            public bool IsFragile => Statuses.ContainsKey(Fragility);
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
            public string State = null!;
            public long UntilTick;   // actif tant que tick < UntilTick ; retiré en phase 1 du tick UntilTick
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
            public string State = null!;
            public long Instance;
            public int Generation;
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
        private readonly long _guardBreakTicks;
        private readonly long _parryWindowTicks;
        private readonly long _parryRecoveryTicks;
        private readonly long _fragilityTicks;
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
        public CombatEngine(CombatSetup setup, ICombatLog log)
        {
            _setup = setup ?? throw new ArgumentNullException(nameof(setup));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            var violations = setup.Validate();
            if (violations.Count > 0) throw new CombatSetupException(violations);

            _rules = setup.Combat;
            _tickMs = _rules.TickMilliseconds;
            _durationTicks = setup.DurationMs / _tickMs;
            _globalCooldownTicks = _rules.GlobalCooldownMs / _tickMs;
            _regenDelayTicks = _rules.EnduranceRegenDelayMs / _tickMs;
            _guardBreakTicks = _rules.Statuses[_rules.GuardBreakStatus].DurationMs / _tickMs;
            _parryWindowTicks = _rules.ParryWindowMs / _tickMs;
            _parryRecoveryTicks = _rules.ParryFailureRecoveryMs / _tickMs;
            _fragilityTicks = _rules.Statuses[Fragility].DurationMs / _tickMs;

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
                    f.Stats = CombatantStats.ForDefenses(_setup.Stats, dummy.Armor, dummy.MagicResistance);
                    break;
                case AssailantSetup assailant:
                    f.Assailant = assailant;
                    f.Stats = CombatantStats.ForDefenses(_setup.Stats, assailant.Armor, assailant.MagicResistance);
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

        /// <summary>Coût exact d'une échéance hors retrait : une ligne si elle agit, rien si elle est périmée.</summary>
        private static Cost ExpirationCost(long tick, Scheduled item) => item switch
        {
            CooldownExpiry e => Cost.Line.If(e.Fighter.CooldownReadyTick.TryGetValue(e.Skill, out long ready) && ready == tick),
            ParryWindowEnd e => Cost.Line.If(e.Fighter.ParryOpen && e.Fighter.ParryStartTick == e.StartTick),
            StateExpiry e => (Cost.Op + Cost.Line).If(IsCurrent(e)),
            _ => Cost.None,
        };

        private static bool IsCurrent(StateExpiry expiry) =>
            expiry.Fighter.Statuses.TryGetValue(expiry.State, out StatusInstance? status)
            && status.Id == expiry.Instance && status.Generation == expiry.Generation;

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
            Fighter f = expiry.Fighter;
            // Échéance périmée : état renouvelé (autre génération), retiré, ou remplacé par une nouvelle instance.
            if (!IsCurrent(expiry)) return;
            StatusInstance status = f.Statuses[expiry.State];
            f.Statuses.Remove(expiry.State);
            Charge(1);
            Emit(tick, CombatPhase.Expirations, "stateExpired", w => w
                .Field("combatant", f.Id).Field("state", status.State).Field("instance", status.Id).Field("generation", status.Generation));
            if (expiry.State == GuardBreak) ResumeRegeneration(f, tick);
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

        private static string? CheckGuardStart(long tick, Fighter actor)
        {
            if (CheckAlive(actor) is { } reason) return reason;
            if (actor.IsGuardBroken) return "guardBroken";
            if (actor.Guarding) return "guardAlreadyActive";
            return null;
        }

        private static string? CheckParry(long tick, Fighter actor)
        {
            if (CheckAlive(actor) is { } reason) return reason;
            if (actor.IsGuardBroken) return "guardBroken";
            if (actor.ParryOpen) return "parryActive";
            if (tick < actor.ParryRecoveryUntilTick) return "parryRecovery";
            return null;
        }

        /// <summary>Motif de refus d'une compétence, dans un ordre fixe et documenté ; null si la commande est acceptée.</summary>
        private static string? CheckSkill(long tick, Fighter actor, SkillDefinition? skill, Fighter? target, int resonanceCost)
        {
            if (CheckAlive(actor) is { } reason) return reason;
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
            if (target is { IsDead: true })
            {
                Emit(tick, CombatPhase.Commands, "attackSkipped", w => w
                    .Field("actor", assailant.Id).Field("attack", attack.Id).Field("target", target.Id).Field("reason", "targetDead"));
                return null;
            }
            Emit(tick, CombatPhase.Commands, "attackStarted", w => w
                .Field("actor", assailant.Id).Field("attack", attack.Id).Field("target", target?.Id)
                .Field("rawDamage", attack.RawDamage).Field("unblockable", attack.Unblockable).Field("appliesFragility", AppliesFragility(attack)));
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
                foreach (Fighter target in targets) cost += HitCost(action, target);
                Reserve(cost);
                foreach (Fighter target in targets) Hit(tick, action, target);
                if (resonance) Gain(tick, CombatPhase.Resolution, action.Actor, Resonance, _rules.ResonancePerDirectHit, "directHit");
            }
        }

        /// <summary>Coût exact d'un impact, déduit de l'état de la cible avant l'impact, avec les mêmes branches que <see cref="Hit"/>.</summary>
        private Cost HitCost(PendingAction action, Fighter target)
        {
            Cost cost = Cost.Op + Cost.Line;   // impact, ligne « damage »
            bool defending = target.Character != null && !target.IsDead && (target.ParryOpen || target.Guarding);
            if (defending && action.Unblockable) { }
            else if (defending && target.ParryOpen)
            {
                if (!target.ParrySucceeded)
                    cost += Cost.Line + GainCost(target, _rules.RampartChargesPerParry) + GainCost(target, _rules.ResonancePerPerfectParry);
            }
            else if (defending)
            {
                cost += target.Endurance < _rules.GuardCostPerBlock ? BreakGuardCost(target) : Cost.Line + GainCost(target, _rules.ResonancePerBlock);
            }
            if (action.Attack is { } attack && AppliesFragility(attack)) cost += ApplyStateCost;
            return cost;
        }

        private void Hit(long tick, PendingAction action, Fighter target)
        {
            Charge(1);
            Fighter actor = action.Actor;
            bool critical = action.Combat != null && CriticalRoll.Roll(actor.Random, actor.Stats);   // l'assaillant ne critique jamais (aucun tirage)
            bool fragile = target.IsFragile;
            DamageBonuses bonuses = fragile ? DamageBonuses.None.Add(BonusCategory.Alteration, _rules.Statuses[Fragility].DamageTakenIncrease) : DamageBonuses.None;

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
                    BreakGuard(tick, CombatPhase.Resolution, target, "insufficientEndurance");
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

            DamageInput input = action.Combat is { } combat
                ? new DamageInput(actor.Stats, target.Stats, combat.PowerType, combat.DamageType, combat.Coefficient, bonuses, critical, mitigation)
                : new DamageInput(actor.AttackStats[action.Attack!.Id], target.Stats,
                    CombatantStats.PowerOf(action.Attack.DamageType),
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
            if (action.Attack is { } attack && AppliesFragility(attack)) ApplyState(tick, CombatPhase.Resolution, target, Fragility, _fragilityTicks);
        }

        /// <summary>
        /// Applique un état non cumulable (A19) : une réapplication renouvelle la durée à partir de maintenant, garde
        /// l'instance et passe à la génération suivante, ce qui périme l'échéance précédente (M02).
        /// </summary>
        private static readonly Cost ApplyStateCost = Cost.Op + Cost.Enqueue + Cost.Line;

        private void ApplyState(long tick, CombatPhase phase, Fighter f, string state, long durationTicks)
        {
            Charge(1);
            bool renewed = f.Statuses.TryGetValue(state, out StatusInstance? status);
            if (status is null)
            {
                status = new StatusInstance { Id = ++_lastStatusInstance, State = state };
                f.Statuses[state] = status;
            }
            status.Generation = checked(status.Generation + 1);
            status.UntilTick = checked(tick + durationTicks);
            Schedule(status.UntilTick, CombatPhase.Expirations,
                new StateExpiry { Fighter = f, State = state, Instance = status.Id, Generation = status.Generation });
            Emit(tick, phase, "stateApplied", w => w
                .Field("combatant", f.Id).Field("state", state).Field("instance", status.Id).Field("generation", status.Generation)
                .Field("untilTick", status.UntilTick).Field("renewed", renewed));
        }

        private static Cost BreakGuardCost(Fighter f) => Cost.Line + Cost.Line.If(f.ParryOpen) + ApplyStateCost;

        /// <summary>Rupture de garde (A1, A19) : fin de la garde et de toute fenêtre de parade, ni garde ni parade ni régénération pendant la durée.</summary>
        private void BreakGuard(long tick, CombatPhase phase, Fighter f, string reason)
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
            ApplyState(tick, phase, f, GuardBreak, _guardBreakTicks);
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
                Reserve(Cost.Op + (breaks ? BreakGuardCost(f) : Cost.Line.If(amount > 0)));
                Charge(1);
                if (breaks)
                {
                    BreakGuard(tick, CombatPhase.Periodic, f, "upkeep");
                    continue;
                }
                f.GuardedTicks = guardedTicks;
                if (amount <= 0) continue;
                SpendEndurance(f, amount, tick);
                f.GuardUpkeepPaid = due;
                Emit(tick, CombatPhase.Periodic, "guardUpkeep", w => w.Field("actor", f.Id).Field("amount", amount).Field("endurance", f.Endurance));
            }
        }

        /// <summary>
        /// A3 : la régénération démarre après un délai complet sans dépense. Le tick t compte comme régénéré s'il commence
        /// au moins <c>délai</c> après la dernière dépense ; le total accordé est
        /// ⌊ régénération/s × ticks régénérés × durée du tick / 1000 ⌋, sans dérive ni flottant, plafonné au maximum.
        /// A1 : suspendue pendant la garde et la rupture de garde.
        /// </summary>
        private void RunRegeneration(long tick)
        {
            int perSecond = _rules.EnduranceRegenPerSecond;
            foreach (Fighter f in _fightersById)
            {
                long regenTicks = f.RegenFromTick is long from ? tick - from + 1 : 0;
                bool eligible = f.Character != null && !f.IsDead && f.RegenFromTick != null && !f.Guarding && !f.IsGuardBroken && regenTicks > 0;
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
                    // M02 : la mort retire les états actifs (ordre ordinal) ; leurs échéances deviennent périmées.
                    foreach (string state in f.Statuses.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList())
                    {
                        StatusInstance status = f.Statuses[state];
                        f.Statuses.Remove(state);
                        Charge(1);
                        Emit(tick, CombatPhase.EndOfTick, "stateRemoved", w => w
                            .Field("combatant", f.Id).Field("state", state).Field("instance", status.Id).Field("generation", status.Generation)
                            .Field("reason", "death"));
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
