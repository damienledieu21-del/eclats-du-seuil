using System.Text.Json;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;

namespace EdS.Simulation.Tests.Engine;

/// <summary>Classe fictive « Essai » et combats de test, construits sans fichier de données.</summary>
internal static class TestCombat
{
    public const string Frappe = "skill.essai.frappe";          // 1.2, 10 endurance, recharge 6 s
    public const string Heurt = "skill.essai.heurt";            // 0.8, 15 endurance, recharge 10 s
    public const string Onde = "skill.essai.onde";              // zone, 0.5, 5 endurance, recharge 2 s
    public const string Gratuite = "skill.essai.gratuite";      // 0.1, sans coût ni recharge
    public const string Ruineuse = "skill.essai.ruineuse";      // 1000 endurance : jamais payable
    public const string Inerte = "skill.essai.inerte";          // sans données de combat
    public const string Ultime = "skill.essai.ultime";          // ultime lançable, niveau 10
    public const string NonEquipee = "skill.essai.non_equipee";
    public const string Percee = "skill.essai.percee";          // imparable, 1.0, sans coût ni recharge
    public const string Rempart = "skill.essai.rempart";        // consomme 2 Charges de rempart
    public const string Recharge = "skill.essai.recharge";      // produit 3 Charges de rempart

    private static SkillCombatDefinition Combat(string coefficient, int cost, long cooldownMs, SkillTargeting targeting = SkillTargeting.Enemy,
        bool unblockable = false, int chargeCost = 0, int chargeGain = 0) =>
        new(PowerType.Physique, DamageType.Physique, Fixed.Parse(coefficient), cost, cooldownMs, targeting, true, unblockable, chargeCost, chargeGain);

    public static ClassDefinition Class(long frappeCooldownMs = 6000, string frappeCoefficient = "1.2") => new(
        "class.essai", "Essai",
        new[] { "alpha", "beta", "gamma" }.Select(k => new TalentTreeDefinition($"tree.essai.{k}", k,
            new[] { new TalentDefinition($"talent.essai.{k}.a", "A", 5) })).ToArray(),
        new[]
        {
            new SkillDefinition(Frappe, "Frappe", SkillKind.Active, combat: Combat(frappeCoefficient, 10, frappeCooldownMs)),
            new SkillDefinition(Heurt, "Heurt", SkillKind.Active, combat: Combat("0.8", 15, 10000)),
            new SkillDefinition(Onde, "Onde", SkillKind.Active, combat: Combat("0.5", 5, 2000, SkillTargeting.AllEnemies)),
            new SkillDefinition(Gratuite, "Gratuite", SkillKind.Active, combat: Combat("0.1", 0, 0)),
            new SkillDefinition(Ruineuse, "Ruineuse", SkillKind.Active, combat: Combat("1", 1000, 0)),
            new SkillDefinition(Inerte, "Inerte", SkillKind.Active),
            new SkillDefinition(NonEquipee, "Non équipée", SkillKind.Active, combat: Combat("1", 0, 0)),
            new SkillDefinition(Ultime, "Ultime", SkillKind.Ultimate, minLevel: 10, combat: Combat("3", 0, 60000)),
            new SkillDefinition(Percee, "Percée", SkillKind.Active, combat: Combat("1", 0, 0, unblockable: true)),
            new SkillDefinition(Rempart, "Rempart", SkillKind.Active, combat: Combat("0.5", 0, 0, chargeCost: 2)),
            new SkillDefinition(Recharge, "Recharge", SkillKind.Active, combat: Combat("0.1", 0, 0, chargeGain: 3)),
        });

    public static readonly ClassDefinition Essai = Class();

    public static readonly CombatDataIdentity Identity = new(
        new DataFileIdentity("rules", "0.1.0-test", "sha256:regles"),
        new[] { new DataFileIdentity("class.essai", "0.1.0-test", "sha256:classe") });

    public static Attributes Attrs(int force = 14, int vitalite = 14, int endurance = 0) => new(force, 8, 6, vitalite, 10, endurance);

    /// <summary>Personnage de niveau 10 : quatre actives (les quatre emplacements du niveau 10) et l'ultime.</summary>
    public static CharacterSetup Character(string id = "bastion", string team = "joueurs", int initiative = 0, Attributes? attributes = null,
        string[]? actives = null, ClassDefinition? definition = null, string? ultimate = Ultime) =>
        new(id, team, definition ?? Essai,
            new CharacterBuild("class.essai", 10, attributes ?? Attrs(), null, actives ?? new[] { Frappe, Heurt, Onde, Gratuite }, ultimate),
            initiative);

    public static DummySetup Dummy(string id = "mannequin", string team = "cibles", int armor = 100) =>
        new(id, team, Fixed.FromInt(armor), Fixed.Zero);

    public static CombatCommand Cmd(long atMs, string skill, string actor = "bastion", string? target = "mannequin") =>
        new(atMs, actor, skill, target);

    public static CombatCommand GuardStart(long atMs, string actor = "bastion") => new(atMs, actor, CombatAction.GuardStart);
    public static CombatCommand GuardStop(long atMs, string actor = "bastion") => new(atMs, actor, CombatAction.GuardStop);
    public static CombatCommand Parry(long atMs, string actor = "bastion") => new(atMs, actor, CombatAction.Parry);

    /// <summary>Attaque d'assaillant : 100 dégâts bruts physiques par défaut (A21).</summary>
    public static AssailantAttack Blow(long raw = 100, bool unblockable = false, bool fragility = false,
        SkillTargeting targeting = SkillTargeting.Enemy, string id = "coup") =>
        new(id, raw, DamageType.Physique, targeting, unblockable, fragility);

    /// <summary>Assaillant d'entraînement : PV infinis, armure nulle, attaque toutes les 2 s à partir de 1 s par défaut.</summary>
    public static AssailantSetup Assailant(long firstAttackMs = 1000, long intervalMs = 2000, string id = "assaillant", string team = "cibles",
        string? target = "bastion", params AssailantAttack[] attacks) =>
        new(id, team, Fixed.Zero, Fixed.Zero, target, firstAttackMs, intervalMs, attacks.Length == 0 ? new[] { Blow() } : attacks);

    /// <summary>Personnage résistant (2100 PV, 100 d'endurance) face à un assaillant : les entraînements durent sans mort.</summary>
    public static CharacterSetup Tank(string id = "bastion", string team = "joueurs", int endurance = 0, string[]? actives = null) =>
        Character(id, team, attributes: Attrs(vitalite: 200, endurance: endurance), actives: actives);

    /// <summary>Règles sans entretien de garde, pour isoler les autres coûts.</summary>
    public static readonly CombatRules NoUpkeep = new() { GuardUpkeepPerSecond = 0 };

    public static CombatSetup Setup(IEnumerable<CombatCommand> commands, IEnumerable<CombatantSetup>? combatants = null,
        long seed = 42, long durationMs = 10_000, CombatRules? rules = null) =>
        new(seed, durationMs, StatRules.Provisional, DamageRules.Provisional, ProgressionRules.Provisional, rules ?? CombatRules.Provisional,
            combatants ?? new CombatantSetup[] { Character(), Dummy() }, commands, Identity);

    public static (CombatResult Result, MemoryCombatLog Log) Run(CombatSetup setup)
    {
        var log = new MemoryCombatLog();
        return (CombatEngine.Run(setup, log), log);
    }

    /// <summary>Événements du journal (lignes après l'en-tête), analysés.</summary>
    public static List<JsonElement> Events(MemoryCombatLog log) =>
        log.Lines.Skip(1).Select(l => JsonDocument.Parse(l).RootElement.Clone()).ToList();

    public static IEnumerable<JsonElement> OfKind(this IEnumerable<JsonElement> events, string kind) =>
        events.Where(e => e.GetProperty("event").GetString() == kind);

    public static long Tick(this JsonElement e) => e.GetProperty("tick").GetInt64();
    public static string? Str(this JsonElement e, string name) => e.GetProperty(name).GetString();
    public static long Int(this JsonElement e, string name) => e.GetProperty(name).GetInt64();
}
