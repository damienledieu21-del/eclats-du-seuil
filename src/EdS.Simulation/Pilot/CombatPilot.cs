using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;

namespace EdS.Simulation.Pilot
{
    /// <summary>
    /// Profils de joueur des mesures (feuille de route, « Builds et scénarios de référence ») : ils ne diffèrent que par les parades
    /// parfaites. Débutant : aucune ; standard : une attaque éligible sur deux (la première, la troisième…) ; expert : toutes.
    /// </summary>
    public enum PilotProfile
    {
        Beginner = 0,
        Standard = 1,
        Expert = 2,
    }

    /// <summary>
    /// Sous-lot 8.6 : pilote de mesure déterministe d'un personnage. À chaque tick, avant qu'il soit simulé, il lit l'état du combat
    /// (<see cref="CombatEngine.View"/>) et choisit ses commandes, comme le ferait un joueur qui voit venir les coups :
    /// <list type="bullet">
    /// <item>parade parfaite au tick d'une attaque éligible (bloquable, qui le vise ou de zone), selon le profil ;</item>
    /// <item>garde tenue tant qu'un assaillant le menace (reprise au tick qui suit un lancer) ; baissée pour régénérer quand l'endurance ne
    /// paie plus un blocage, relevée à la moitié de l'endurance maximale, sauf garde incassable (Rempart éternel) ; aucune garde face à un
    /// mannequin ou à des assaillants qui visent un autre ;</item>
    /// <item>compétences par priorité : l'ultime dès que la Résonance suffit, puis les actives dans l'ordre des emplacements, quand elles
    /// sont prêtes et payables en gardant de quoi bloquer un coup (aucune compétence payante pendant une pause de garde) ; jamais au tick
    /// d'un impact qu'il bloquerait sans le parer (lancer impose de quitter la garde).</item>
    /// </list>
    /// Choix des cibles : ennemi désigné = l'assaillant qui le vise et frappe le plus tôt, sinon le premier ennemi vivant ; compétence de
    /// contrôle sans dégâts sur un ennemi (Provocation) = un assaillant qui vise un allié, sinon rien ; soutien d'un allié (Barrière d'acier) =
    /// l'allié vivant le plus entamé, sinon soi ; Contrecoup (toutes les Charges) seulement à 3 Charges ou au maximum s'il est plus bas.
    /// Les règles sont identiques pour tous les builds et tous les profils.
    /// </summary>
    public sealed class CombatPilot
    {
        /// <summary>Charges visées avant un Contrecoup (toutes les Charges) : 3, ou le maximum effectif s'il est plus bas.</summary>
        public const int ChargesBeforeSpendAll = 3;

        private readonly CombatEngine _engine;
        private readonly string _actor;
        private readonly PilotProfile _profile;
        private int _eligibleAttacks;
        private bool _resting;

        /// <summary>Endurance à partir de laquelle la garde est relevée après une pause : la moitié du maximum, au moins deux blocages.</summary>
        public static long ResumeEndurance(CombatantView me) => Math.Max(checked(2 * me.Rules!.GuardCostPerBlock), me.MaxEndurance / 2);

        public CombatPilot(CombatEngine engine, string actor, PilotProfile profile)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _actor = actor ?? throw new ArgumentNullException(nameof(actor));
            if (!Enum.IsDefined(typeof(PilotProfile), profile)) throw new ArgumentOutOfRangeException(nameof(profile));
            _profile = profile;
            if (engine.View(actor).Kind != CombatantKind.Character) throw new ArgumentException($"« {actor} » n'est pas un personnage.", nameof(actor));
        }

        /// <summary>Commandes du tick courant, à soumettre avant de le simuler.</summary>
        public IReadOnlyList<CombatCommand> Decide()
        {
            var commands = new List<CombatCommand>();
            CombatantView me = _engine.View(_actor);
            if (me.IsDead || _engine.IsFinished) return commands;
            long tick = _engine.CurrentTick;
            long atMs = checked(tick * _engine.TickMilliseconds);
            List<CombatantView> others = _engine.CombatantIds.Where(id => id != _actor).Select(_engine.View).ToList();
            List<CombatantView> enemies = others.Where(o => o.Team != me.Team && !o.IsDead).ToList();

            // Attaques qui frappent à ce tick : assaillants non contrôlés dont le tour tombe maintenant, qui le visent ou frappent en zone.
            List<CombatantView> incoming = enemies.Where(e => e.Kind == CombatantKind.Assailant && e.NextAttackTick == tick && !e.AttacksBlocked
                && (e.NextAttack!.Targeting == SkillTargeting.AllEnemies || e.CurrentTarget == _actor)).ToList();
            bool eligible = incoming.Any(e => !e.NextAttack!.Unblockable);
            bool parry = false;
            if (eligible)
            {
                _eligibleAttacks++;
                bool wants = _profile == PilotProfile.Expert || (_profile == PilotProfile.Standard && _eligibleAttacks % 2 == 1);
                parry = wants && me.CanParry && !me.ParryOpen && tick >= me.ParryRecoveryUntilTick;
            }

            // Garde : tenue tant qu'un assaillant le menace (sa prochaine attaque le vise ou frappe en zone) ; baissée pour régénérer quand
            // l'endurance ne paie plus un blocage, relevée à la moitié de l'endurance maximale (jamais baissée si la garde est incassable).
            CombatantRules rules = me.Rules!;
            bool threatened = enemies.Any(e => e.Kind == CombatantKind.Assailant && e.NextAttackTick != null
                && (e.NextAttack!.Targeting == SkillTargeting.AllEnemies || e.CurrentTarget == _actor));
            if (_resting && me.Endurance >= ResumeEndurance(me)) _resting = false;
            if (!_resting && !rules.GuardNoUpkeepBreak && me.Endurance < rules.GuardCostPerBlock) _resting = true;
            bool wantsGuard = threatened && !_resting && me.CanGuard;

            (string Skill, string? Target)? cast = null;
            bool holdGuard = wantsGuard && incoming.Count > 0 && !parry;   // lancer ferait quitter la garde au moment d'un impact
            if (me.CanCast && tick >= me.GlobalCooldownReadyTick && !holdGuard)
                cast = ChooseSkill(me, others, enemies, tick);

            if (cast is { } c)
            {
                if (me.Guarding) commands.Add(new CombatCommand(atMs, _actor, CombatAction.GuardStop));
                commands.Add(new CombatCommand(atMs, _actor, c.Skill, c.Target));
            }
            else if (wantsGuard && !me.Guarding)
                commands.Add(new CombatCommand(atMs, _actor, CombatAction.GuardStart));   // garde (re)prise, par exemple au tick qui suit un lancer
            else if (!wantsGuard && me.Guarding)
                commands.Add(new CombatCommand(atMs, _actor, CombatAction.GuardStop));
            if (parry) commands.Add(new CombatCommand(atMs, _actor, CombatAction.Parry));
            return commands;
        }

        private (string Skill, string? Target)? ChooseSkill(CombatantView me, List<CombatantView> others, List<CombatantView> enemies, long tick)
        {
            CharacterSetup character = me.Character!;
            CombatantRules rules = me.Rules!;
            CharacterBuild build = character.Build;
            var order = new List<(string Id, bool Ultimate)>();
            if (build.Ultimate != null) order.Add((build.Ultimate, true));
            order.AddRange(build.ActiveSkills.Select(s => (s, false)));
            foreach (var (id, ultimate) in order)
            {
                if (!character.Class.TryGetSkill(id, out SkillDefinition skill) || skill.Combat is not { } combat) continue;
                if (tick < me.CooldownReadyTick(id)) continue;
                if (ultimate && me.Resonance < _engine.Setup.Combat.UltimateResonanceCost) continue;
                // Garder de quoi bloquer un coup après le lancer (sauf compétence gratuite).
                if (combat.EnduranceCost > 0 && (_resting || me.Endurance < checked(combat.EnduranceCost + rules.GuardCostPerBlock))) continue;
                if (me.RampartCharges < combat.RampartChargeCost) continue;
                if (combat.RampartChargeSpendAll && me.RampartCharges < Math.Max(1, Math.Min(ChargesBeforeSpendAll, rules.RampartChargesMax))) continue;
                if (enemies.Count == 0) continue;
                switch (combat.Targeting)
                {
                    case SkillTargeting.Enemy when combat.HasDamage:
                        return (id, PrimaryEnemy(enemies, tick).Id);
                    case SkillTargeting.Enemy:
                        // Contrôle sans dégâts (Provocation) : détourner un assaillant qui vise un allié (revue Codex de la PR #10 : un
                        // assaillant sans cible unique, qui n'attaque qu'en zone, n'a rien à détourner).
                        CombatantView? away = enemies.Where(e => e.Kind == CombatantKind.Assailant && e.CurrentTarget != null && e.CurrentTarget != _actor)
                            .OrderBy(e => e.NextAttackTick ?? long.MaxValue).ThenBy(e => e.Id, StringComparer.Ordinal).FirstOrDefault();
                        if (away is null) continue;
                        return (id, away.Id);
                    case SkillTargeting.Ally:
                        // Rapports PV / PV max comparés en entiers (revue Codex de la PR #10 : pas de double dans un choix déterministe).
                        CombatantView? ally = others.Where(o => o.Team == me.Team && !o.IsDead && o.Kind == CombatantKind.Character && o.MaxHealth > 0)
                            .OrderBy(o => o, MostDamagedFirst).FirstOrDefault();
                        return (id, ally?.Id ?? _actor);
                    default:
                        return (id, null);
                }
            }
            return null;
        }

        /// <summary>
        /// Compare deux rapports PV / PV max sans arrondi : signe de <c>healthA × maxB − healthB × maxA</c> (PV et PV max d'un combattant
        /// tiennent sur 32 bits : le produit tient sur 64 bits, vérifié). Les PV max doivent être strictement positifs.
        /// </summary>
        public static int CompareHealthRatio(long healthA, long maxA, long healthB, long maxB)
        {
            if (maxA <= 0) throw new ArgumentOutOfRangeException(nameof(maxA));
            if (maxB <= 0) throw new ArgumentOutOfRangeException(nameof(maxB));
            return checked(healthA * maxB).CompareTo(checked(healthB * maxA));
        }

        /// <summary>L'allié le plus entamé (plus petit rapport PV / PV max) d'abord, puis l'ordre ordinal des identifiants.</summary>
        private static readonly Comparer<CombatantView> MostDamagedFirst = Comparer<CombatantView>.Create((a, b) =>
        {
            int ratio = CompareHealthRatio(a.Health!.Value, a.MaxHealth!.Value, b.Health!.Value, b.MaxHealth!.Value);
            return ratio != 0 ? ratio : StringComparer.Ordinal.Compare(a.Id, b.Id);
        });

        /// <summary>Assaillant qui le vise et frappe le plus tôt ; sinon le premier ennemi vivant (ordre ordinal).</summary>
        private CombatantView PrimaryEnemy(List<CombatantView> enemies, long tick) =>
            enemies.Where(e => e.Kind == CombatantKind.Assailant && e.CurrentTarget == _actor)
                .OrderBy(e => e.NextAttackTick ?? long.MaxValue).ThenBy(e => e.Id, StringComparer.Ordinal).FirstOrDefault()
            ?? enemies.OrderBy(e => e.Id, StringComparer.Ordinal).First();
    }

    /// <summary>Résultat d'un combat piloté : le scénario complété des commandes du pilote, son journal et son résultat.</summary>
    public sealed class PilotedCombat
    {
        /// <summary>Scénario de départ complété des commandes du pilote : un combat ordinaire, rejouable.</summary>
        public CombatSetup Setup { get; }
        public CombatResult Result { get; }

        private PilotedCombat(CombatSetup setup, CombatResult result)
        {
            Setup = setup;
            Result = result;
        }

        /// <summary>
        /// Pilote <paramref name="actor"/> sur <paramref name="baseSetup"/> (ses commandes de départ sont gardées), puis rejoue le combat
        /// complété des commandes du pilote dans <paramref name="log"/> et vérifie que les deux passes donnent le même résultat.
        /// </summary>
        public static PilotedCombat Run(CombatSetup baseSetup, string actor, PilotProfile profile, ICombatLog log)
        {
            if (baseSetup is null) throw new ArgumentNullException(nameof(baseSetup));
            if (log is null) throw new ArgumentNullException(nameof(log));
            var planning = new CombatEngine(baseSetup, NullCombatLog.Instance);
            var pilot = new CombatPilot(planning, actor, profile);
            while (!planning.IsFinished)
            {
                foreach (CombatCommand command in pilot.Decide()) planning.Submit(command);
                planning.Step();
            }
            var setup = new CombatSetup(baseSetup.Seed, baseSetup.DurationMs, baseSetup.Stats, baseSetup.Damage, baseSetup.Progression, baseSetup.Combat,
                baseSetup.Combatants, baseSetup.Commands.Concat(planning.SubmittedCommands), baseSetup.Data);
            CombatResult result = CombatEngine.Run(setup, log);
            string? difference = Difference(planning.Result!, result);
            if (difference != null)
                throw new InvalidOperationException($"Combat piloté non reproductible ({actor}, {profile}) : {difference}.");
            return new PilotedCombat(setup, result);
        }

        /// <summary>Premier écart entre deux résultats (fin, puis chaque compteur de chaque combattant) ; null s'ils sont identiques.</summary>
        internal static string? Difference(CombatResult a, CombatResult b)
        {
            if (a.EndReason != b.EndReason || a.EndTick != b.EndTick || a.Winner != b.Winner) return "fin du combat";
            for (int i = 0; i < a.Combatants.Count; i++)
            {
                CombatantResult x = a.Combatants[i], y = b.Combatants[i];
                var fields = new (string Name, object? X, object? Y)[]
                {
                    ("damageDealt", x.DamageDealt, y.DamageDealt), ("damageTaken", x.DamageTaken, y.DamageTaken),
                    ("damageAbsorbed", x.DamageAbsorbed, y.DamageAbsorbed), ("damageTransferred", x.DamageTransferred, y.DamageTransferred),
                    ("damageRedirected", x.DamageRedirected, y.DamageRedirected), ("damageMitigated", x.DamageMitigated, y.DamageMitigated),
                    ("hits", x.Hits, y.Hits), ("skillsCast", x.SkillsCast, y.SkillsCast), ("commandsRejected", x.CommandsRejected, y.CommandsRejected),
                    ("blocks", x.Blocks, y.Blocks), ("perfectParries", x.PerfectParries, y.PerfectParries), ("guardTicks", x.GuardTicks, y.GuardTicks),
                    ("health", x.Health, y.Health), ("deathTick", x.DeathTick, y.DeathTick),
                };
                foreach (var (name, xv, yv) in fields)
                    if (!Equals(xv, yv)) return $"{x.Id}.{name} : {xv} puis {yv}";
            }
            return null;
        }
    }
}
