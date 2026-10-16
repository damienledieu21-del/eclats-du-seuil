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
    /// </summary>
    public sealed class CombatEngine
    {
        /// <summary>Version du moteur, inscrite au journal : un rejeu exige la même version.</summary>
        public const string Version = "0.6.0";

        public const string LogFormat = "eds-combat-log";
        public const int LogFormatVersion = 1;

        // Identifiants stables du journal.
        public const string Resonance = "resonance";
        public const string RampartCharges = "rampartCharges";
        public const string Fragility = "fragility";
        public const string GuardBreak = "guardBreak";

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
            public long GuardStartTick;
            public long GuardUpkeepPaid;
            public long GuardBrokenUntilTick;     // rupture active tant que tick < valeur

            public bool ParryOpen;
            public long ParryStartTick;
            public long ParryEndTick;             // fenêtre [début, fin[
            public bool ParrySucceeded;
            public long ParryRecoveryUntilTick;   // nouvelle parade refusée tant que tick < valeur

            public long FragileUntilTick;         // Fragilité active tant que tick < valeur

            public int Resonance;
            public int RampartCharges;

            public Pcg32 Random = null!;
            public bool IsDead;
            public readonly CombatantResult Result = new CombatantResult();

            public string Id => Setup.Id;
            public string Team => Setup.Team;
            public bool IsGuardBroken(long tick) => tick < GuardBrokenUntilTick;
            public bool IsFragile(long tick) => tick < FragileUntilTick;
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

        private sealed class EventBudgetExceeded : Exception { }

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
        private int _eventsThisTick;
        private bool _countEvents;

        public long CurrentTick { get; private set; }
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
            _guardBreakTicks = _rules.GuardBreakDurationMs / _tickMs;
            _parryWindowTicks = _rules.ParryWindowMs / _tickMs;
            _parryRecoveryTicks = _rules.ParryFailureRecoveryMs / _tickMs;
            _fragilityTicks = _rules.FragilityDurationMs / _tickMs;

            _fighters = setup.Combatants.Select(CreateFighter).ToList();
            _fightersById = _fighters.OrderBy(f => f.Id, StringComparer.Ordinal).ToList();
            _byId = _fighters.ToDictionary(f => f.Id, StringComparer.Ordinal);

            for (int i = 0; i < setup.Commands.Count; i++)
            {
                CombatCommand command = setup.Commands[i];
                _queue.Enqueue(command.AtMs / _tickMs, CombatPhase.Commands, new CommandItem { Index = i, Command = command });
            }
            foreach (Fighter f in _fighters)
                if (f.Assailant is { } assailant && assailant.FirstAttackMs / _tickMs < _durationTicks)
                    _queue.Enqueue(assailant.FirstAttackMs / _tickMs, CombatPhase.Commands, new AssailantTurn { Fighter = f });

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
                    f.Stats = DerivedStats.Calculate(_setup.Stats, character.Build.Attributes);
                    f.MaxHealth = f.Stats.MaxHealth.RoundToInt(RoundingMode.Floor);
                    f.Health = f.MaxHealth;
                    f.MaxEndurance = f.Stats.MaxEndurance.RoundToInt(RoundingMode.Floor);
                    f.Endurance = f.MaxEndurance;
                    foreach (string skill in character.Build.ActiveSkills) f.Castable.Add(skill);
                    if (character.Build.Ultimate != null) f.Castable.Add(character.Build.Ultimate);
                    break;
                case DummySetup dummy:
                    f.Stats = DefensesOnly(dummy.Armor, dummy.MagicResistance);
                    break;
                case AssailantSetup assailant:
                    f.Assailant = assailant;
                    f.Stats = DefensesOnly(assailant.Armor, assailant.MagicResistance);
                    // Dégâts bruts d'une attaque = puissance du type correspondant, coefficient 1, sans critique.
                    foreach (AssailantAttack attack in assailant.Attacks)
                        f.AttackStats[attack.Id] = DerivedStats.Calculate(_setup.Stats, Attributes.None, new[]
                        {
                            new StatContributions
                            {
                                PhysicalPower = Fixed.FromInt(attack.RawDamage) - _setup.Stats.BasePhysicalPower,
                                MagicPower = Fixed.FromInt(attack.RawDamage) - _setup.Stats.BaseMagicPower,
                            },
                        });
                    break;
            }
            return f;
        }

        private DerivedStats DefensesOnly(Fixed armor, Fixed magicResistance) =>
            DerivedStats.Calculate(_setup.Stats, Attributes.None, new[]
            {
                new StatContributions { Armor = armor - _setup.Stats.BaseArmor, MagicResistance = magicResistance - _setup.Stats.BaseMagicResistance },
            });

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
            _countEvents = true;
            try
            {
                RunExpirations(tick);
                List<PendingAction> actions = RunCommands(tick);
                RunResolution(tick, actions);
                RunPeriodic(tick);
                RunRegeneration(tick);
                RunEndOfTick(tick);
            }
            catch (EventBudgetExceeded)
            {
                Abort(tick, "eventBudgetExceeded", _rules.MaxEventsPerTick);
            }
            catch (OverflowException)
            {
                Abort(tick, "overflow", null);
            }
            if (!IsFinished) CurrentTick++;
            return !IsFinished;
        }

        // --- Phase 1 : expirations ---------------------------------------------------------------

        private void RunExpirations(long tick)
        {
            while (_queue.TryDequeue(tick, CombatPhase.Expirations, out Scheduled item))
            {
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
            long until = expiry.State == Fragility ? f.FragileUntilTick : f.GuardBrokenUntilTick;
            if (until != tick) return;   // renouvelé depuis : cette échéance est périmée
            Emit(tick, CombatPhase.Expirations, "stateExpired", w => w.Field("combatant", f.Id).Field("state", expiry.State));
            if (expiry.State == GuardBreak) ResumeRegeneration(f, tick);
        }

        // --- Phase 2 : commandes -----------------------------------------------------------------

        private List<PendingAction> RunCommands(long tick)
        {
            var batch = new List<(Fighter Actor, int Index, Scheduled Item)>();
            while (_queue.TryDequeue(tick, CombatPhase.Commands, out Scheduled item))
            {
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
                PendingAction? action = entry.Item is CommandItem command ? Execute(tick, command) : AssailantAttacks(tick, entry.Actor);
                if (action != null) actions.Add(action);
            }
            return actions;
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
                    actor.GuardStartTick = tick;
                    actor.GuardUpkeepPaid = 0;
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
                    _queue.Enqueue(actor.ParryEndTick, CombatPhase.Expirations, new ParryWindowEnd { Fighter = actor, StartTick = tick });
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
            Fighter? target = command.Target != null ? _byId[command.Target] : null;
            SkillDefinition? skill = null;
            actor.Character?.Class.TryGetSkill(command.Skill!, out skill);
            bool isUltimate = skill != null && actor.Character?.Build.Ultimate == skill.Id;
            int resonanceCost = isUltimate ? _rules.UltimateResonanceCost : 0;

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
                _queue.Enqueue(readyTick, CombatPhase.Expirations, new CooldownExpiry { Fighter = actor, Skill = skill.Id });
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
            if (actor.IsGuardBroken(tick)) return "guardBroken";
            if (actor.Guarding) return "guardAlreadyActive";
            return null;
        }

        private static string? CheckParry(long tick, Fighter actor)
        {
            if (CheckAlive(actor) is { } reason) return reason;
            if (actor.IsGuardBroken(tick)) return "guardBroken";
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
            if (next < _durationTicks) _queue.Enqueue(next, CombatPhase.Commands, new AssailantTurn { Fighter = assailant });

            Fighter? target = attack.Targeting == SkillTargeting.Enemy ? _byId[setup.Target!] : null;
            if (target is { IsDead: true })
            {
                Emit(tick, CombatPhase.Commands, "attackSkipped", w => w
                    .Field("actor", assailant.Id).Field("attack", attack.Id).Field("target", target.Id).Field("reason", "targetDead"));
                return null;
            }
            Emit(tick, CombatPhase.Commands, "attackStarted", w => w
                .Field("actor", assailant.Id).Field("attack", attack.Id).Field("target", target?.Id)
                .Field("rawDamage", attack.RawDamage).Field("unblockable", attack.Unblockable).Field("appliesFragility", attack.AppliesFragility));
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
                foreach (Fighter target in targets) Hit(tick, action, target);
                // A5 : une attaque directe réussie rapporte de la Résonance une seule fois, quel que soit le nombre de cibles.
                if (targets.Count > 0 && action.Actor.Character != null && action.GeneratesResonance)
                    Gain(tick, CombatPhase.Resolution, action.Actor, Resonance, _rules.ResonancePerDirectHit, "directHit");
            }
        }

        private void Hit(long tick, PendingAction action, Fighter target)
        {
            Fighter actor = action.Actor;
            bool critical = action.Combat != null && CriticalRoll.Roll(actor.Random, actor.Stats);   // l'assaillant ne critique jamais (aucun tirage)
            bool fragile = target.IsFragile(tick);
            DamageBonuses bonuses = fragile ? DamageBonuses.None.Add(BonusCategory.Alteration, _rules.FragilityDamageTakenIncrease) : DamageBonuses.None;

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
                    action.Attack.DamageType == DamageType.Physique ? PowerType.Physique : PowerType.Magique,
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
            if (action.Attack is { AppliesFragility: true }) ApplyFragility(tick, target);
        }

        private void ApplyFragility(long tick, Fighter target)
        {
            // A19 : pas de cumul ; une réapplication renouvelle la durée à partir de maintenant.
            bool renewed = target.IsFragile(tick);
            target.FragileUntilTick = checked(tick + _fragilityTicks);
            _queue.Enqueue(target.FragileUntilTick, CombatPhase.Expirations, new StateExpiry { Fighter = target, State = Fragility });
            Emit(tick, CombatPhase.Resolution, "stateApplied", w => w
                .Field("combatant", target.Id).Field("state", Fragility).Field("untilTick", target.FragileUntilTick).Field("renewed", renewed));
        }

        /// <summary>Rupture de garde (A1, A19) : fin de la garde et de toute fenêtre de parade, ni garde ni parade ni régénération pendant la durée.</summary>
        private void BreakGuard(long tick, CombatPhase phase, Fighter f, string reason)
        {
            f.Guarding = false;
            f.GuardBrokenUntilTick = checked(tick + _guardBreakTicks);
            f.Result.GuardBreaks++;
            Emit(tick, phase, "guardBroken", w => w.Field("actor", f.Id).Field("reason", reason).Field("endurance", f.Endurance));
            if (f.ParryOpen)
            {
                f.ParryOpen = false;
                Emit(tick, phase, "parryEnded", w => w
                    .Field("actor", f.Id).Field("outcome", f.ParrySucceeded ? "success" : "interrupted").Field("recoveryUntilTick", (long?)null));
            }
            _queue.Enqueue(f.GuardBrokenUntilTick, CombatPhase.Expirations, new StateExpiry { Fighter = f, State = GuardBreak });
            Emit(tick, phase, "stateApplied", w => w
                .Field("combatant", f.Id).Field("state", GuardBreak).Field("untilTick", f.GuardBrokenUntilTick).Field("renewed", false));
        }

        // --- Ressources --------------------------------------------------------------------------

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

        /// <summary>A1 : entretien de la garde, ⌊ coût/s × ticks gardés × durée du tick / 1000 ⌋ cumulé ; s'il ne peut être payé, la garde est brisée.</summary>
        private void RunPeriodic(long tick)
        {
            foreach (Fighter f in _fightersById)
            {
                if (!f.Guarding || f.IsDead) continue;
                long guardedTicks = tick - f.GuardStartTick + 1;
                long due = checked(_rules.GuardUpkeepPerSecond * guardedTicks * _tickMs) / 1000;
                long amount = due - f.GuardUpkeepPaid;
                if (amount <= 0) continue;
                if (f.Endurance < amount)
                {
                    BreakGuard(tick, CombatPhase.Periodic, f, "upkeep");
                    continue;
                }
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
                if (f.Character is null || f.IsDead || f.RegenFromTick is not long from) continue;
                if (f.Guarding || f.IsGuardBroken(tick)) continue;
                long regenTicks = tick - from + 1;
                if (regenTicks <= 0) continue;
                long target = checked(perSecond * regenTicks * _tickMs) / 1000;
                long gain = Math.Min(target - f.RegenGranted, f.MaxEndurance - f.Endurance);
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
                if (!f.IsDead && f.Health == 0)
                {
                    f.IsDead = true;
                    f.Guarding = false;
                    f.ParryOpen = false;
                    Emit(tick, CombatPhase.EndOfTick, "died", w => w.Field("combatant", f.Id));
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
            Result = new CombatResult(reason, abortReason, tick, durationMs, winner, _fighters.Select(f => f.Result).ToArray());
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

        private void Emit(long tick, CombatPhase phase, string kind, Action<JsonLineWriter> fields)
        {
            if (_countEvents && ++_eventsThisTick > _rules.MaxEventsPerTick) throw new EventBudgetExceeded();
            var w = new JsonLineWriter().BeginObject()
                .Field("tick", tick).Field("seq", _sequence++).Field("phase", PhaseId(phase)).Field("event", kind);
            fields(w);
            _log.Append(w.EndObject().ToString());
        }
    }
}
