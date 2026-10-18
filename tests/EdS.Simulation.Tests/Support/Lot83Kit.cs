using System.Text.Json;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Support;

/// <summary>
/// Sous-lot 8.3 : données d'essai du soutien. Classe « Soutien » (compétences sans coût ni recharge) et catalogue enrichi d'un
/// bouclier, d'un renforcement, d'une provocation et de deux protections. Combattants : le Bastion (2 100 PV), un allié fragile
/// (240 PV), un gardien (240 PV) et un assaillant d'entraînement (armure 0) qui frappe 100 bruts à 1 s puis toutes les 2 s.
/// </summary>
internal static class Lot83Kit
{
    public const string Barriere = "skill.soutien.barriere";        // allié : bouclier égide (10 % des PV max du lanceur)
    public const string Garde = "skill.soutien.garde";              // soi : renforcement « fortifié »
    public const string Ralliement = "skill.soutien.ralliement";    // tous les alliés : « fortifié »
    public const string Provocation = "skill.soutien.provocation";  // ennemi, sans dégâts : provocation de 3 s
    public const string Clameur = "skill.soutien.clameur";          // tous les ennemis, sans dégâts : provocation
    public const string Defi = "skill.soutien.defi";                // soi : provocation (refusée : le lanceur n'est pas un assaillant)
    public const string Lien = "skill.soutien.lien";                // allié : protection « lien » (25 %)
    public const string LienTotal = "skill.soutien.lien_total";     // allié : protection « lienTotal » (100 %)
    public const string Reseau = "skill.soutien.reseau";            // soi, application à cible propre allAllies : « lien »
    public const string Coup = "skill.soutien.coup";                // ennemi, 1.0 physique ; application à cible propre self : « fortifié »
    public const string Absolu = "skill.soutien.absolu";            // soi : « fortifié » à tous les alliés, provocation à tous les ennemis
    public const string Passif = "skill.soutien.passif";            // passif porteur de déclencheurs et de modificateurs d'essai

    public static readonly StatusDefinition Egide = new("egide", StatusCategory.Shield, 4000, StatusStacking.PerSourceKeepStronger);
    public static readonly StatusDefinition Fortifie = new("fortifie", StatusCategory.Buff, 8000, StatusStacking.Refresh,
        modifiers: new[] { Mod("damageTaken", "-0.3") });
    public static readonly StatusDefinition Provoc = new("provoc", StatusCategory.Taunt, 3000, StatusStacking.PerSourceKeepStronger);
    public static readonly StatusDefinition LienStatus = new("lien", StatusCategory.Protection, 60_000, StatusStacking.PerSourceKeepStronger,
        redirectRatio: Fixed.Parse("0.25"));
    public static readonly StatusDefinition LienTotalStatus = new("lienTotal", StatusCategory.Protection, 60_000, StatusStacking.PerSourceKeepStronger,
        redirectRatio: Fixed.One);

    public static readonly CombatRules Rules = new()
    {
        GuardUpkeepPerSecond = 0,
        Statuses = new StatusCatalog(StatusCatalog.Provisional.Definitions.Concat(new[] { Egide, Fortifie, Provoc, LienStatus, LienTotalStatus })),
    };

    private static SkillCombatDefinition Support(SkillTargeting targeting, params StatusApplication[] applies) =>
        new(null, null, null, 0, 0, targeting, applies: applies);

    public static StatusApplication Own(string status, ApplicationTarget target, string? coefficient = null) =>
        new(status, coefficient is null ? null : Fixed.Parse(coefficient), target);

    /// <summary>Classe de soutien ; le passif porte les déclencheurs et modificateurs demandés.</summary>
    public static ClassDefinition SupportClass(IEnumerable<TriggerDefinition>? passiveTriggers = null, ModifierDefinition[]? passiveModifiers = null) =>
        new("class.soutien", "Soutien",
            new[] { "a", "b", "c" }.Select(k => new TalentTreeDefinition($"tree.soutien.{k}", k,
                new[] { new TalentDefinition($"talent.soutien.{k}.t", "T", 5) })).ToArray(),
            new[]
            {
                new SkillDefinition(Barriere, "Barrière", SkillKind.Active, combat: Support(SkillTargeting.Ally, Apply("egide", "0.1"))),
                new SkillDefinition(Garde, "Garde", SkillKind.Active, combat: Support(SkillTargeting.Self, Apply("fortifie"))),
                new SkillDefinition(Ralliement, "Ralliement", SkillKind.Active, combat: Support(SkillTargeting.AllAllies, Apply("fortifie"))),
                new SkillDefinition(Provocation, "Provocation", SkillKind.Active, combat: Support(SkillTargeting.Enemy, Apply("provoc"))),
                new SkillDefinition(Clameur, "Clameur", SkillKind.Active, combat: Support(SkillTargeting.AllEnemies, Apply("provoc"))),
                new SkillDefinition(Defi, "Défi", SkillKind.Active, combat: Support(SkillTargeting.Self, Apply("provoc"))),
                new SkillDefinition(Lien, "Lien", SkillKind.Active, combat: Support(SkillTargeting.Ally, Apply("lien"))),
                new SkillDefinition(LienTotal, "Lien total", SkillKind.Active, combat: Support(SkillTargeting.Ally, Apply("lienTotal"))),
                new SkillDefinition(Reseau, "Réseau", SkillKind.Active, combat: Support(SkillTargeting.Self, Own("lien", ApplicationTarget.AllAllies))),
                new SkillDefinition(Coup, "Coup", SkillKind.Active, combat: new SkillCombatDefinition(PowerType.Physique, DamageType.Physique,
                    Fixed.One, 0, 0, SkillTargeting.Enemy, applies: new[] { Own("fortifie", ApplicationTarget.Self) })),
                new SkillDefinition(Absolu, "Absolu", SkillKind.Active, combat: Support(SkillTargeting.Self,
                    Own("fortifie", ApplicationTarget.AllAllies), Own("provoc", ApplicationTarget.AllEnemies))),
                new SkillDefinition(Passif, "Passif", SkillKind.Passive, triggers: passiveTriggers, modifiers: passiveModifiers),
            });

    /// <summary>Personnage de soutien de niveau 10 : quatre actives au choix, sans ultime ; passif équipé.</summary>
    public static CharacterSetup Hero(string id, string[] actives, int vitalite = 14, ClassDefinition? definition = null, int initiative = 0) =>
        new(id, "joueurs", definition ?? SupportClass(),
            new CharacterBuild("class.soutien", 10, Attrs(vitalite: vitalite), null, actives, null, new[] { Passif }), initiative);

    public static CharacterSetup Bastion(params string[] actives) => Hero("bastion", actives.Length == 0 ? new[] { Lien, Barriere, Garde, Provocation } : actives, 200);

    public static CharacterSetup Ally(params string[] actives) => Hero("allie", actives.Length == 0 ? new[] { Lien, Barriere, Garde, Provocation } : actives);

    /// <summary>Assaillant d'entraînement : 100 bruts physiques (ou l'attaque donnée) à 1 s puis toutes les 2 s, sur sa cible.</summary>
    public static AssailantSetup Brute(string target = "allie", long raw = 100, long intervalMs = 2000, params StatusApplication[] applies) =>
        new("assaillant", "cibles", Fixed.Zero, Fixed.Zero, target, 1000, intervalMs, new[] { new AssailantAttack("coup", raw, DamageType.Physique, SkillTargeting.Enemy, false, applies) });

    public static CombatCommand Use(long atMs, string skill, string actor = "bastion", string? target = null) => new(atMs, actor, skill, target);

    public static CombatSetup Fight(IEnumerable<CombatCommand> commands, IEnumerable<CombatantSetup> combatants, long durationMs = 3100, CombatRules? rules = null) =>
        Setup(commands, combatants, durationMs: durationMs, rules: rules ?? Rules);

    public static List<JsonElement> Simulate(CombatSetup setup, out CombatResult result)
    {
        var (r, log) = Run(setup);
        result = r;
        return Events(log);
    }

    public static long Long(this JsonElement e, string name) => e.GetProperty(name).GetInt64();

    public static bool Bool(this JsonElement e, string name) => e.GetProperty(name).GetBoolean();
}
