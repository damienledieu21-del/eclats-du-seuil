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
    /// Moteur de combat minimal (lot 5) : horloge logique en ticks, file d'événements ordonnée, commandes validées
    /// atomiquement, coûts, recharges, temps global de recharge, dégâts directs, régénération d'endurance, journal
    /// JSON Lines. Aucune lecture de l'horloge système, aucun flottant, aucun état partagé entre combats : tout l'état
    /// appartient à l'instance. Même combat (règles, participants, commandes, graine) = même journal, octet par octet.
    /// <para>Ordre d'un tick (A10) : expirations → commandes (initiative décroissante, puis identifiant) → résolution
    /// des actions et dégâts → effets périodiques (aucun en v0.1) → régénération → morts et fin de combat.</para>
    /// </summary>
    public sealed class CombatEngine
    {
        /// <summary>Version du moteur, inscrite au journal : un rejeu exige la même version.</summary>
        public const string Version = "0.5.0";

        public const string LogFormat = "eds-combat-log";
        public const int LogFormatVersion = 1;

        private sealed class Fighter
        {
            public CombatantSetup Setup = null!;
            public CharacterSetup? Character;
            public DerivedStats Stats = null!;
            public HashSet<string> Castable = new HashSet<string>(StringComparer.Ordinal);
            public long? Health;
            public long? MaxHealth;
            public long Endurance;
            public long MaxEndurance;
            public long? LastSpendTick;
            public long RegenGranted;
            public long GlobalCooldownReadyTick;
            public readonly Dictionary<string, long> CooldownReadyTick = new Dictionary<string, long>(StringComparer.Ordinal);
            public Pcg32 Random = null!;
            public bool IsDead;
            public readonly CombatantResult Result = new CombatantResult();

            public string Id => Setup.Id;
            public string Team => Setup.Team;
        }

        private abstract class Scheduled { }

        private sealed class CommandItem : Scheduled
        {
            public int Index;
            public CombatCommand Command = null!;
        }

        private sealed class CooldownExpiry : Scheduled
        {
            public Fighter Fighter = null!;
            public string Skill = null!;
        }

        private sealed class PendingAction
        {
            public Fighter Actor = null!;
            public SkillDefinition Skill = null!;
            public SkillCombatDefinition Combat = null!;
            public Fighter? Target;
        }

        private sealed class EventBudgetExceeded : Exception { }

        private readonly CombatSetup _setup;
        private readonly ICombatLog _log;
        private readonly List<Fighter> _fighters;          // ordre du scénario
        private readonly List<Fighter> _fightersById;      // ordre ordinal des identifiants
        private readonly Dictionary<string, Fighter> _byId;
        private readonly EventQueue<Scheduled> _queue = new EventQueue<Scheduled>();
        private readonly long _tickMs;
        private readonly long _durationTicks;
        private readonly long _globalCooldownTicks;
        private readonly long _regenDelayTicks;
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

            CombatRules rules = setup.Combat;
            _tickMs = rules.TickMilliseconds;
            _durationTicks = setup.DurationMs / _tickMs;
            _globalCooldownTicks = rules.GlobalCooldownMs / _tickMs;
            _regenDelayTicks = rules.EnduranceRegenDelayMs / _tickMs;

            _fighters = setup.Combatants.Select(CreateFighter).ToList();
            _fightersById = _fighters.OrderBy(f => f.Id, StringComparer.Ordinal).ToList();
            _byId = _fighters.ToDictionary(f => f.Id, StringComparer.Ordinal);

            for (int i = 0; i < setup.Commands.Count; i++)
            {
                CombatCommand command = setup.Commands[i];
                _queue.Enqueue(command.AtMs / _tickMs, CombatPhase.Commands, new CommandItem { Index = i, Command = command });
            }

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
                    f.Stats = DerivedStats.Calculate(_setup.Stats, Attributes.None, new[]
                    {
                        new StatContributions { Armor = dummy.Armor - _setup.Stats.BaseArmor, MagicResistance = dummy.MagicResistance - _setup.Stats.BaseMagicResistance },
                    });
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
            _countEvents = true;
            try
            {
                RunExpirations(tick);
                List<PendingAction> actions = RunCommands(tick);
                RunResolution(tick, actions);
                // Phase 4, effets périodiques : aucun en v0.1 (altérations au lot 7).
                RunRegeneration(tick);
                RunEndOfTick(tick);
            }
            catch (EventBudgetExceeded)
            {
                Abort(tick, "eventBudgetExceeded", _setup.Combat.MaxEventsPerTick);
            }
            catch (OverflowException)
            {
                Abort(tick, "overflow", null);
            }
            if (!IsFinished) CurrentTick++;
            return !IsFinished;
        }

        // --- Phases ------------------------------------------------------------------------------

        private void RunExpirations(long tick)
        {
            while (_queue.TryDequeue(tick, CombatPhase.Expirations, out Scheduled item))
            {
                var expiry = (CooldownExpiry)item;
                if (expiry.Fighter.CooldownReadyTick.TryGetValue(expiry.Skill, out long ready) && ready == tick)
                    Emit(tick, CombatPhase.Expirations, "cooldownReady", w => w.Field("actor", expiry.Fighter.Id).Field("skill", expiry.Skill));
            }
        }

        private List<PendingAction> RunCommands(long tick)
        {
            var batch = new List<CommandItem>();
            while (_queue.TryDequeue(tick, CombatPhase.Commands, out Scheduled item)) batch.Add((CommandItem)item);

            var actions = new List<PendingAction>();
            // Tri stable : initiative décroissante, identifiant ordinal, puis ordre du scénario.
            foreach (CommandItem item in batch
                .OrderByDescending(c => _byId[c.Command.Actor].Setup.Initiative)
                .ThenBy(c => c.Command.Actor, StringComparer.Ordinal)
                .ThenBy(c => c.Index))
            {
                PendingAction? action = Execute(tick, item);
                if (action != null) actions.Add(action);
            }
            return actions;
        }

        /// <summary>Valide une commande sans rien modifier, puis seulement l'applique (refus atomique).</summary>
        private PendingAction? Execute(long tick, CommandItem item)
        {
            CombatCommand command = item.Command;
            Fighter actor = _byId[command.Actor];
            Fighter? target = command.Target != null ? _byId[command.Target] : null;
            SkillDefinition? skill = null;
            actor.Character?.Class.TryGetSkill(command.Skill, out skill);

            string? reason = Check(tick, actor, skill, target);
            if (reason != null)
            {
                actor.Result.CommandsRejected++;
                Emit(tick, CombatPhase.Commands, "commandRejected", w => w
                    .Field("actor", actor.Id).Field("skill", command.Skill).Field("target", command.Target)
                    .Field("command", item.Index).Field("reason", reason));
                return null;
            }

            SkillCombatDefinition combat = skill!.Combat!;
            if (combat.EnduranceCost > 0)
            {
                actor.Endurance -= combat.EnduranceCost;
                actor.LastSpendTick = tick;
                actor.RegenGranted = 0;
            }
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
                .Field("cooldownReadyTick", readyTick).Field("globalCooldownReadyTick", actor.GlobalCooldownReadyTick));
            return new PendingAction { Actor = actor, Skill = skill, Combat = combat, Target = target };
        }

        /// <summary>Motif de refus, dans un ordre fixe et documenté ; null si la commande est acceptée.</summary>
        private string? Check(long tick, Fighter actor, SkillDefinition? skill, Fighter? target)
        {
            if (actor.IsDead) return "actorDead";
            if (actor.Character is null) return "actorCannotAct";
            if (skill is null || !actor.Castable.Contains(skill.Id)) return "skillNotEquipped";
            if (skill.Combat is not { } combat) return "skillNotImplemented";
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
            return null;
        }

        private void RunResolution(long tick, List<PendingAction> actions)
        {
            foreach (PendingAction action in actions)
            {
                IEnumerable<Fighter> targets = action.Combat.Targeting == SkillTargeting.Enemy
                    ? new[] { action.Target! }
                    : _fightersById.Where(f => f.Team != action.Actor.Team && !f.IsDead).ToArray();
                foreach (Fighter target in targets) Hit(tick, action, target);
            }
        }

        private void Hit(long tick, PendingAction action, Fighter target)
        {
            Fighter actor = action.Actor;
            bool critical = CriticalRoll.Roll(actor.Random, actor.Stats);
            var input = new DamageInput(actor.Stats, target.Stats, action.Combat.PowerType, action.Combat.DamageType,
                action.Combat.Coefficient, DamageBonuses.None, critical);
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
                .Field("source", actor.Id).Field("target", target.Id).Field("skill", action.Skill.Id)
                .Field("critical", critical).Field("amount", amount).Field("overkill", overkill)
                .Field("health", target.Health).Field("damageTaken", target.Result.DamageTaken));
        }

        /// <summary>
        /// A3 : la régénération démarre après un délai complet sans dépense. Le tick t compte comme régénéré s'il commence
        /// au moins <c>délai</c> après la dernière dépense ; le total accordé est
        /// ⌊ régénération/s × ticks régénérés × durée du tick / 1000 ⌋, sans dérive ni flottant, plafonné au maximum.
        /// </summary>
        private void RunRegeneration(long tick)
        {
            int perSecond = _setup.Combat.EnduranceRegenPerSecond;
            foreach (Fighter f in _fightersById)
            {
                if (f.Character is null || f.IsDead || f.LastSpendTick is not long lastSpend) continue;
                long regenTicks = tick - lastSpend - _regenDelayTicks + 1;
                if (regenTicks <= 0) continue;
                long target = checked(perSecond * regenTicks * _tickMs) / 1000;
                long gain = Math.Min(target - f.RegenGranted, f.MaxEndurance - f.Endurance);
                f.RegenGranted = target;
                if (gain > 0)
                {
                    f.Endurance += gain;
                    Emit(tick, CombatPhase.Regeneration, "enduranceRegenerated", w => w.Field("actor", f.Id).Field("amount", gain).Field("endurance", f.Endurance));
                }
                if (f.Endurance >= f.MaxEndurance) f.LastSpendTick = null;   // plein : plus rien à régénérer jusqu'à la prochaine dépense
            }
        }

        private void RunEndOfTick(long tick)
        {
            foreach (Fighter f in _fightersById)
            {
                if (!f.IsDead && f.Health == 0)
                {
                    f.IsDead = true;
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

        private void Finish(long tick, CombatEndReason reason, string? abortReason, string? winner)
        {
            _countEvents = false;
            foreach (Fighter f in _fighters)
            {
                f.Result.Health = f.Health;
                f.Result.Endurance = f.Character != null ? f.Endurance : (long?)null;
                f.Result.IsDead = f.IsDead;
                CombatantResult r = f.Result;
                Emit(tick, CombatPhase.EndOfTick, "combatantSummary", w => w
                    .Field("combatant", r.Id).Field("team", r.Team).Field("damageDealt", r.DamageDealt).Field("damageTaken", r.DamageTaken)
                    .Field("hits", r.Hits).Field("criticalHits", r.CriticalHits).Field("skillsCast", r.SkillsCast)
                    .Field("commandsRejected", r.CommandsRejected).Field("health", r.Health).Field("endurance", r.Endurance)
                    .Field("dead", r.IsDead));
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
            Emit(0, CombatPhase.Setup, "combatant", w => w
                .Field("combatant", f.Id).Field("team", f.Team).Field("type", f.Character != null ? "character" : "dummy")
                .Field("initiative", f.Setup.Initiative).Field("health", f.Health).Field("maxHealth", f.MaxHealth)
                .Field("endurance", f.Character != null ? f.Endurance : (long?)null).Field("maxEndurance", f.Character != null ? f.MaxEndurance : (long?)null)
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
            if (_countEvents && ++_eventsThisTick > _setup.Combat.MaxEventsPerTick) throw new EventBudgetExceeded();
            var w = new JsonLineWriter().BeginObject()
                .Field("tick", tick).Field("seq", _sequence++).Field("phase", PhaseId(phase)).Field("event", kind);
            fields(w);
            _log.Append(w.EndObject().ToString());
        }
    }
}
