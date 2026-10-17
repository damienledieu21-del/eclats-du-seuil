using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using EdS.Domain;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Modifiers;

// Sous-lot 8.1, validation des modificateurs au niveau du combat (conception design81.md, D10 prioritaire sur D1–D9).
// Correspondance avec la liste obligatoire de l'utilisateur :
//   [L1] chaque clé au rang 1 et au rang maximal ......... damageDealt aux rangs 1 à 5 (précontrôle), durées en ms en base et perRank ;
//   [L2] bornes N−1, N, N+1 ............................... multiples du tick et de la période, frontière du précontrôle numérique
//                                                           (force, résistance aux altérations), majorant de file et capacité ;
//   [L3] combinaisons de modificateurs .................... précontrôle (charges relevées + conditionnel, coefficient + bonus),
//                                                           sous-ensembles de modificateurs négatifs ou défensifs ;
//   [L4] conditions vraies et fausses ..................... hors de ce fichier (moteur) ; ici, majorant d'un conditionnel au facteur maximal ;
//   [L5] agrégation indépendante de l'ordre ............... validation, majorant et journal identiques quelle que soit la répartition ;
//   [L6] immuabilité ...................................... entrées inchangées après validation et simulation, sources modifiées sans effet ;
//   [L7] débordements refusés avant simulation ............ précontrôle numérique, aucune ligne de journal (pas même l'en-tête) ;
//   [L8] aucune mutation partielle ........................ combat refusé sans écriture ni modification, deux moteurs indépendants ;
//   [L9] déterminisme et rejeu ............................ journal identique octet par octet, graines : seules les lignes aléatoires changent ;
//   [L10] non-régression des 5 scénarios livrés ........... couverte par Lot81ReferenceTests et Lot81EngineBaselineTests (non refaite).

/// <summary>Outils communs aux tests de validation de combat du sous-lot 8.1 (classe d'essai, combats, refus avant journal).</summary>
internal static class Lot81CombatFixtures
{
    /// <summary>Modificateurs groupés par porteur (identifiant de talent ou de compétence passive), dans l'ordre donné.</summary>
    public static Dictionary<string, ModifierDefinition[]> Mods(params (string Owner, ModifierDefinition Modifier)[] items) =>
        items.GroupBy(i => i.Owner).ToDictionary(g => g.Key, g => g.Select(i => i.Modifier).ToArray());

    /// <summary>Combat de test avec des règles de statistiques et de combat au choix (TestCombat.Setup fige les statistiques).</summary>
    public static CombatSetup Make(IEnumerable<CombatantSetup> combatants, IEnumerable<CombatCommand> commands, long durationMs = 10_000,
        StatRules? stats = null, CombatRules? rules = null, long seed = 42) =>
        new(seed, durationMs, stats ?? StatRules.Provisional, DamageRules.Provisional, ProgressionRules.Provisional, rules ?? CombatRules.Provisional,
            combatants, commands, Identity);

    /// <summary>
    /// Refus avant tout journal : une seule violation, le moteur (construction et <see cref="CombatEngine.Run"/>) lève
    /// <see cref="CombatSetupException"/> et le journal reste vide, sans même l'en-tête.
    /// </summary>
    public static RuleViolation AssertRefusedBeforeLog(CombatSetup setup)
    {
        RuleViolation violation = Assert.Single(setup.Validate());
        var log = new MemoryCombatLog();
        var error = Assert.Throws<CombatSetupException>(() => CombatEngine.Run(setup, log));
        Assert.Equal(violation.Message, Assert.Single(error.Violations).Message);
        Assert.Empty(log.Lines);
        Assert.Throws<CombatSetupException>(() => new CombatEngine(setup, log));
        Assert.Empty(log.Lines);
        return violation;
    }

    /// <summary>
    /// Description textuelle complète de ce qu'un appelant a fourni : règles lues par les modificateurs, combattants, classe (talents,
    /// compétences, modificateurs avec valeurs et conditions), build, commandes. Sert d'instantané avant/après.
    /// </summary>
    public static string Describe(CombatSetup s)
    {
        var b = new StringBuilder();
        CombatRules r = s.Combat;
        b.Append($"seed={s.Seed} duration={s.DurationMs} tick={r.TickMilliseconds} gcd={r.GlobalCooldownMs} pending={r.MaxPendingEvents} ")
         .Append($"events={r.MaxEventsPerTick} work={r.MaxWorkPerTick} guard={r.GuardDamageReduction} upkeep={r.GuardUpkeepPerSecond} block={r.GuardCostPerBlock} ")
         .Append($"parry={r.ParryWindowMs}/{r.ParryFailureRecoveryMs} charges={r.RampartChargesMax} resonance={r.ResonanceMax}/{r.ResonancePerDirectHit} ")
         .Append($"regen={r.EnduranceRegenPerSecond} break={r.GuardBreakStatus} statuses={string.Join(",", r.Statuses.Definitions.Select(d => $"{d.Id}:{d.DurationMs}:{d.Stacking}"))}\n");
        b.Append($"stats={s.Stats.BaseStatusResistance}/{s.Stats.MaxHealthPerVitalite}/{s.Stats.PhysicalPowerPerForce}\n");
        foreach (CombatantSetup c in s.Combatants)
        {
            b.Append($"{c.GetType().Name} {c.Id}/{c.Team}/{c.Initiative}\n");
            switch (c)
            {
                case CharacterSetup ch:
                {
                    CharacterBuild build = ch.Build;
                    Attributes a = build.Attributes;
                    b.Append($"  build {build.ClassId} {build.Level} {a.Force},{a.Dexterite},{a.Intelligence},{a.Vitalite},{a.Volonte},{a.Endurance} ")
                     .Append($"talents=[{string.Join(",", build.TalentRanks.OrderBy(t => t.Key, StringComparer.Ordinal).Select(t => $"{t.Key}={t.Value}"))}] ")
                     .Append($"actives=[{string.Join(",", build.ActiveSkills)}] ultimate={build.Ultimate} passives=[{string.Join(",", build.PassiveSkills)}]\n");
                    b.Append($"  class {ch.Class.Id} {ch.Class.Name}\n");
                    foreach (TalentTreeDefinition tree in ch.Class.Trees)
                        foreach (TalentDefinition t in tree.Talents)
                            b.Append($"  talent {tree.Id}/{t.Id} {t.MaxRank} {ModifiersText(t.Modifiers)}\n");
                    foreach (SkillDefinition sk in ch.Class.Skills)
                        b.Append($"  skill {sk.Id} {sk.Kind} {sk.Combat?.Coefficient} {sk.Combat?.CooldownMs} ")
                         .Append($"{string.Join(",", sk.Combat?.Applies.Select(x => x.Status) ?? Array.Empty<string>())} {ModifiersText(sk.Modifiers)}\n");
                    break;
                }
                case AssailantSetup a:
                    b.Append($"  {a.Target} {a.FirstAttackMs} {a.IntervalMs} {string.Join(",", a.Attacks.Select(x => $"{x.Id}:{x.RawDamage}:{string.Join("+", x.Applies.Select(p => p.Status))}"))}\n");
                    break;
                case DummySetup d:
                    b.Append($"  {d.Armor} {d.MagicResistance} {d.StatusResistance}\n");
                    break;
            }
        }
        foreach (CombatCommand cmd in s.Commands) b.Append($"cmd {cmd.AtMs} {cmd.Actor} {cmd.Action} {cmd.Skill} {cmd.Target}\n");
        return b.ToString();
    }

    private static string ModifiersText(IReadOnlyList<ModifierDefinition> modifiers) =>
        string.Join(";", modifiers.Select(m => $"{m.Key}={m.Add.Base}+{m.Add.PerRank}[{string.Join(",", m.Conditions)}]"));
}

/// <summary>
/// Sous-lot 8.1 (D2, D9) : contrôles des modificateurs qui dépendent des règles du combat, faits par <c>CombatSetup.ValidateCharacter</c> :
/// altération <c>status.&lt;id&gt;</c> connue du catalogue, <c>durationDealt</c> sur une altération applicable directement (ni immunité, ni
/// rupture de garde configurée), durées multiples du tick des règles et de la période d'un DOT. Chaque refus est rattaché à
/// <c>combatants[i].class</c> avec le chemin interne à la classe dans le message, avant toute ligne de journal.
/// </summary>
[Trait("Category", "Modifier")]
[Trait("Category", "Status")]
public class Lot81ModifierCombatValidationTests
{
    private const string Prefix = "classe « class.essai », ";

    /// <summary>Mannequin en premier, personnage en second : le chemin attendu est <c>combatants[1].class</c>.</summary>
    private static CombatSetup Second(ClassDefinition definition, CombatRules? rules = null, IReadOnlyDictionary<string, int>? talents = null,
        string[]? passives = null) =>
        Setup(new[] { Cmd(0, Frappe) }, new CombatantSetup[] { Dummy(), Character(definition: definition, talents: talents, passives: passives) }, rules: rules);

    private static ClassDefinition OnTalent(params ModifierDefinition[] modifiers) => Class(modifiers: Lot81CombatFixtures.Mods(modifiers.Select(m => (TalentAlpha, m)).ToArray()));

    private static ClassDefinition OnPassive(params ModifierDefinition[] modifiers) => Class(modifiers: Lot81CombatFixtures.Mods(modifiers.Select(m => (Passif, m)).ToArray()));

    /// <summary>Indice de <see cref="Passif"/> dans la liste des compétences de la classe d'essai (16ᵉ compétence, comptée à la main).</summary>
    private const int PassiveIndex = 15;

    private static void AssertClassRefusal(CombatSetup setup, RuleViolationKind kind, string internalPath, string message)
    {
        RuleViolation violation = Lot81CombatFixtures.AssertRefusedBeforeLog(setup);
        Assert.Equal(kind, violation.Kind);
        Assert.Equal("combatants[1].class", Assert.Single(violation.Fields));
        Assert.Equal($"{Prefix}{internalPath} : {message}", violation.Message);
    }

    // --- Altération inconnue ---------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("poison", "durationTaken", false)]
    [InlineData("poison", "durationTaken", true)]
    [InlineData("poison", "durationDealt", false)]
    [InlineData("poison", "durationDealt", true)]
    [InlineData("taunt", "durationTaken", false)]   // D2 : status.taunt.* refusée tant que taunt n'est pas au catalogue (X7, 8.3)
    [InlineData("taunt", "durationDealt", true)]
    public void Alteration_inconnue_refusee_avec_le_chemin_interne(string status, string suffix, bool passive)
    {
        ModifierDefinition modifier = Mod($"status.{status}.{suffix}", "1000");
        ClassDefinition definition = passive ? OnPassive(modifier) : OnTalent(modifier);

        // La classe seule ne connaît pas le catalogue : la clé est bien formée, la classe est cohérente.
        Assert.Empty(definition.Validate(ProgressionRules.Provisional));

        string path = passive ? $"skills[{PassiveIndex}].modifiers[0].key" : "trees[0].talents[0].modifiers[0].key";
        AssertClassRefusal(Second(definition), RuleViolationKind.UnknownReference, path, $"altération inconnue « {status} ».");
    }

    [Fact]
    public void Alteration_connue_du_catalogue_des_regles_acceptee()
    {
        // La même clé est acceptée dès que le catalogue des règles contient l'altération : la référence dépend des règles du combat.
        var catalog = new StatusCatalog(StatusCatalog.Provisional.Definitions
            .Append(new StatusDefinition("poison", StatusCategory.Modifier, 4000, StatusStacking.Refresh, damageTakenIncrease: Fixed.FromRatio(1, 20))));
        var rules = new CombatRules { Statuses = catalog };
        Assert.Empty(Second(OnTalent(Mod("status.poison.durationTaken", "1000"), Mod("status.poison.durationDealt", "500")), rules).Validate());
    }

    // --- durationDealt : altération applicable directement ----------------------------------------------------------------------

    [Theory]
    [InlineData("stunImmunity")]   // immunité : accordée seulement à l'expiration d'une autre altération
    [InlineData("guardBreak")]     // rupture de garde configurée (combat.guard.breakStatus)
    public void Duree_infligee_refusee_sur_une_immunite_ou_la_rupture_de_garde(string status)
    {
        AssertClassRefusal(Second(OnTalent(Mod($"status.{status}.durationDealt", "1000"))), RuleViolationKind.InvalidValue,
            "trees[0].talents[0].modifiers[0].key",
            $"« {status} » n'est jamais appliquée directement (immunité ou rupture de garde) : durationDealt sans objet.");

        // durationTaken s'applique à toute application sur le porteur (D5) : acceptée sur les mêmes altérations.
        Assert.Empty(Second(OnTalent(Mod($"status.{status}.durationTaken", "1000"))).Validate());
    }

    [Fact]
    public void Rupture_de_garde_designee_par_les_regles_et_non_par_son_nom()
    {
        // Rupture de garde configurée sur « fragility » : durationDealt y est refusée, et « guardBreak », devenue une altération
        // ordinaire applicable directement, l'accepte.
        var rules = new CombatRules { GuardBreakStatus = "fragility" };
        AssertClassRefusal(Second(OnTalent(Mod("status.fragility.durationDealt", "1000")), rules), RuleViolationKind.InvalidValue,
            "trees[0].talents[0].modifiers[0].key",
            "« fragility » n'est jamais appliquée directement (immunité ou rupture de garde) : durationDealt sans objet.");
        Assert.Empty(Second(OnTalent(Mod("status.guardBreak.durationDealt", "1000")), rules).Validate());
    }

    // --- Durées multiples du tick ([L2] N−1, N, N+1 autour de 50 ms) --------------------------------------------------------------

    public static TheoryData<string> MillisecondKeys() => new()
    {
        "parry.windowMs", "parry.failureRecoveryMs", "guard.breakShortenPerCharge", "skill.essai.frappe.cooldownMs",
        "status.stun.durationTaken", "status.freeze.durationDealt", "status.fragility.durationTaken", "status.stunImmunity.durationTaken",
    };

    [Theory]
    [MemberData(nameof(MillisecondKeys))]
    public void Duree_non_multiple_du_tick_refusee_en_base(string key)
    {
        foreach (var (value, accepted) in new[] { ("49", false), ("50", true), ("51", false), ("-49", false), ("-50", true), ("100", true), ("125", false) })
        {
            CombatSetup setup = Second(OnTalent(Mod(key, value)));
            if (accepted)
            {
                Assert.Empty(setup.Validate());
                continue;
            }
            AssertClassRefusal(setup, RuleViolationKind.InvalidValue, "trees[0].talents[0].modifiers[0].add",
                "durée non multiple de la durée du tick (50 ms), base et perRank.");
        }
    }

    [Theory]
    [MemberData(nameof(MillisecondKeys))]
    public void Duree_non_multiple_du_tick_refusee_par_rang(string key)
    {
        // [L1] perRank contrôlé comme la base : 50 + 49 × (rang − 1) n'est un multiple du tick à aucun rang > 1 visé.
        foreach (var (perRank, accepted) in new[] { ("49", false), ("50", true), ("51", false) })
        {
            CombatSetup setup = Second(OnTalent(Mod(key, "50", perRank)), talents: new Dictionary<string, int> { [TalentAlpha] = 5 });
            if (accepted)
            {
                Assert.Empty(setup.Validate());
                continue;
            }
            AssertClassRefusal(setup, RuleViolationKind.InvalidValue, "trees[0].talents[0].modifiers[0].add",
                "durée non multiple de la durée du tick (50 ms), base et perRank.");
        }
    }

    [Fact]
    public void Multiple_du_tick_des_regles_du_combat()
    {
        // Tick de 25 ms (diviseur de 1000, toutes les durées des règles provisoires en sont multiples) : 75 ms est accepté, mais
        // refusé au tick de 50 ms ; la même classe est donc valide ou non selon les règles du combat.
        var quarter = new CombatRules { TickMilliseconds = 25 };
        ClassDefinition definition = OnTalent(Mod("parry.windowMs", "75"));
        Assert.Empty(Second(definition, quarter).Validate());
        AssertClassRefusal(Second(definition), RuleViolationKind.InvalidValue, "trees[0].talents[0].modifiers[0].add",
            "durée non multiple de la durée du tick (50 ms), base et perRank.");

        // Et 60 ms, multiple de 20 mais pas de 25 : refusé au tick de 25 ms, avec la durée du tick des règles dans le message.
        AssertClassRefusal(Second(OnTalent(Mod("parry.windowMs", "60")), quarter), RuleViolationKind.InvalidValue,
            "trees[0].talents[0].modifiers[0].add", "durée non multiple de la durée du tick (25 ms), base et perRank.");
    }

    // --- DOT : multiples de la période (2000 ms pour brûlure et saignement) ------------------------------------------------------

    [Theory]
    [InlineData("status.burn.durationTaken", "burn")]
    [InlineData("status.burn.durationDealt", "burn")]
    [InlineData("status.bleed.durationTaken", "bleed")]
    [InlineData("status.bleed.durationDealt", "bleed")]
    public void Duree_d_un_dot_multiple_de_sa_periode(string key, string status)
    {
        // [L2] N−1, N, N+1 autour de 2000 ms, chaque valeur étant déjà un multiple du tick (50 ms) : seule la période décide.
        foreach (var (value, perRank, accepted) in new[]
                 {
                     ("1950", "0", false), ("2000", "0", true), ("2050", "0", false), ("1000", "0", false), ("-2000", "0", true),
                     ("4000", "0", true), ("2000", "1000", false), ("2000", "2000", true), ("2000", "1950", false),
                 })
        {
            CombatSetup setup = Second(OnTalent(Mod(key, value, perRank)), talents: new Dictionary<string, int> { [TalentAlpha] = 5 });
            if (accepted)
            {
                Assert.Empty(setup.Validate());
                continue;
            }
            AssertClassRefusal(setup, RuleViolationKind.InvalidValue, "trees[0].talents[0].modifiers[0].add",
                $"durée non multiple de la période de « {status} » (2000 ms), base et perRank.");
        }
    }

    // --- Plusieurs porteurs, ordre et portée --------------------------------------------------------------------------------------

    [Fact]
    public void Chaque_porteur_signale_avec_son_chemin_dans_l_ordre_de_la_classe()
    {
        // Talent beta (2ᵉ arbre) et passif : deux refus, dans l'ordre de la classe (talents puis compétences), chacun de sa nature.
        ClassDefinition definition = Class(modifiers: Lot81CombatFixtures.Mods(
            (Passif, Mod("status.burn.durationTaken", "1000")),
            (TalentBeta, Mod("status.poison.durationDealt", "50")),
            (TalentBeta, Mod("parry.windowMs", "75"))));
        IReadOnlyList<RuleViolation> errors = Second(definition).Validate();
        Assert.Equal(3, errors.Count);
        Assert.All(errors, e => Assert.Equal("combatants[1].class", Assert.Single(e.Fields)));
        Assert.Equal(new[]
        {
            (RuleViolationKind.UnknownReference, $"{Prefix}trees[1].talents[0].modifiers[0].key : altération inconnue « poison »."),
            (RuleViolationKind.InvalidValue, $"{Prefix}trees[1].talents[0].modifiers[1].add : durée non multiple de la durée du tick (50 ms), base et perRank."),
            (RuleViolationKind.InvalidValue, $"{Prefix}skills[{PassiveIndex}].modifiers[0].add : durée non multiple de la période de « burn » (2000 ms), base et perRank."),
        }, errors.Select(e => (e.Kind, e.Message)));
    }

    [Fact]
    public void Classe_entiere_confrontee_aux_regles_meme_sans_rang_ni_passif_equipe()
    {
        // Comme les déclencheurs (7.3) : la classe est confrontée aux règles en entier, qu'un modificateur soit actif ou non avec ce
        // build (talent alpha sans rang, passif non équipé).
        foreach (ClassDefinition definition in new[] { OnTalent(Mod("status.poison.durationTaken", "50")), OnPassive(Mod("status.poison.durationTaken", "50")) })
        {
            CombatSetup setup = Second(definition);
            Assert.Empty(((CharacterSetup)setup.Combatants[1]).ActiveModifiers());
            Assert.Equal(RuleViolationKind.UnknownReference, Assert.Single(setup.Validate()).Kind);
        }
    }

    [Fact]
    public void Erreurs_de_reference_independantes_du_precontrole_numerique()
    {
        // D9 : contrôles de ValidateCharacter, non soumis au précontrôle numérique. Une durée invalide reste refusée par le moteur
        // construit sans majorant ni précontrôle (variante interne du filet de sécurité), toujours avant toute ligne.
        CombatSetup setup = Second(OnTalent(Mod("status.freeze.durationTaken", "75")));
        var log = new MemoryCombatLog();
        var error = Assert.Throws<CombatSetupException>(() => new CombatEngine(setup, log, queueBound: false, numericLimits: false));
        RuleViolation violation = Assert.Single(error.Violations);
        Assert.Equal((RuleViolationKind.InvalidValue, "combatants[1].class"), (violation.Kind, Assert.Single(violation.Fields)));
        Assert.Equal($"{Prefix}trees[0].talents[0].modifiers[0].add : durée non multiple de la durée du tick (50 ms), base et perRank.", violation.Message);
        Assert.Empty(log.Lines);
    }
}

/// <summary>
/// Sous-lot 8.1 (D7) : précontrôle numérique sur les valeurs effectives. Statistiques par la fonction partagée (PV max en %, résistance
/// aux altérations plate), coefficient effectif, bonus talent du pire impact (permanent + conditionnels positifs à leur facteur maximal :
/// Charges max effectives, ⌊Résonance max / 10⌋). Un combat valide sans modificateur mais qui déborde avec eux est refusé
/// (<c>NumericLimit</c> sur <c>combatants[i]</c>) avant toute ligne de journal ; frontière N/N+1 ; damageTaken ne fait que réduire.
/// </summary>
[Trait("Category", "Modifier")]
[Trait("Category", "Numeric")]
public class Lot81ModifierNumericPrecheckTests
{
    // Règles du précontrôle : 600 s au tick de 50 ms (12 000 ticks) × 10⁷ événements par tick = 1,2 × 10¹¹ impacts comptés au plus.
    private const long DurationMs = 600_000;
    private static readonly CombatRules Wide = new() { MaxWorkPerTick = 10_000_000, MaxEventsPerTick = 10_000_000 };
    private const long MaxImpacts = DurationMs / 50 * 10_000_000;
    private const int Strong = 1_000_000;   // force maximale d'un build (BuildValidator.MaxAttributeValue)

    /// <summary>
    /// Mannequin (indice 0) contre personnage (indice 1) de force <paramref name="force"/>, talents alpha et beta aux rangs donnés,
    /// passifs équipés ; <paramref name="modifiers"/> nul : la même classe sans aucun modificateur.
    /// </summary>
    private static CombatSetup Duel(int force, Dictionary<string, ModifierDefinition[]>? modifiers, int alpha = 5, int beta = 1,
        CombatRules? rules = null, StatRules? stats = null, int vitalite = 14)
    {
        ClassDefinition definition = Class(modifiers: modifiers, secondPassive: true);
        CharacterSetup character = Character(definition: definition, attributes: new Attributes(force, 8, 6, vitalite, 10, 0),
            talents: new Dictionary<string, int> { [TalentAlpha] = alpha, [TalentBeta] = beta }, passives: new[] { Passif, PassifB });
        return Lot81CombatFixtures.Make(new CombatantSetup[] { Dummy(), character }, new[] { Cmd(0, Frappe) }, DurationMs, stats, rules ?? Wide);
    }

    /// <summary>
    /// Pire impact attendu, calculé à la main : puissance 2 × force (2 par point de force), coefficient, bonus Altération 1,1
    /// (Fragilité, seule altération à bonus du catalogue), bonus Talent min(1 + T, 10), critique 1,5, aucune défense. Avec une force
    /// multiple de 20 et T entier, chaque étape est exacte à 4 décimales : le résultat est entier, sans arrondi.
    /// </summary>
    private static long WorstHit(long force, int coefficientTenths, int talent) =>
        2 * force * coefficientTenths * Math.Min(1 + talent, 10) * 11 * 15 / (10 * 10 * 10);

    private static string CumulMessage(long worst, string skill) =>
        $"cumul des dégâts sur la durée du combat hors des limites numériques (pire impact {worst}, impact de compétence « {skill} », × {MaxImpacts} événements comptés au plus).";

    /// <summary>Cas de débordement dû aux seuls modificateurs : construction (avec ou sans eux), champ et message attendus.</summary>
    public static readonly Dictionary<string, (Func<bool, CombatSetup> Build, string Field, string Message)> Overflows = new()
    {
        // [L1] damageDealt +10 au rang maximal du talent (2 + 2 × 4) : T = 10, multiplicateur 10 ; ultime (coefficient 3) en pire impact.
        ["degats_infliges_rang_maximal"] = (with => Duel(Strong, with ? Lot81CombatFixtures.Mods((TalentAlpha, Mod("damageDealt", "2", "2"))) : null),
            "combatants[1]", CumulMessage(WorstHit(Strong, 30, 10), Ultime)),
        // Coefficient de Frappe 1,2 + 100, écrêté à 100 : Frappe devient le pire impact (2 × 10⁶ × 100 × 1,1 × 1,5).
        ["coefficient_plus_100"] = (with => Duel(Strong, with ? Lot81CombatFixtures.Mods((Passif, Mod("skill.essai.frappe.coefficient", "100"))) : null),
            "combatants[1]", CumulMessage(WorstHit(Strong, 1000, 0), Frappe)),
        // [L3] Conditionnel +1 par Charge et Charges max 5 + 5 = 10 : T = 10 au facteur maximal effectif.
        ["par_charge_avec_charges_max_relevees"] = (with => Duel(Strong, with
                ? Lot81CombatFixtures.Mods((Passif, Mod("damageDealt", "1", null, ModifierCondition.PerRampartCharge)), (TalentBeta, Mod("rampartCharges.max", "5")))
                : null),
            "combatants[1]", CumulMessage(WorstHit(Strong, 30, 10), Ultime)),
        // Conditionnel +1 par palier de Résonance : ⌊100 / 10⌋ = 10 paliers, T = 10.
        ["par_palier_de_resonance"] = (with => Duel(Strong, with ? Lot81CombatFixtures.Mods((Passif, Mod("damageDealt", "1", null, ModifierCondition.PerResonanceStep))) : null),
            "combatants[1]", CumulMessage(WorstHit(Strong, 30, 10), Ultime)),
        // Conditionnel tout ou rien (en garde) compté comme vrai : T = 9, multiplicateur 10.
        ["en_garde"] = (with => Duel(Strong, with ? Lot81CombatFixtures.Mods((Passif, Mod("damageDealt", "9", null, ModifierCondition.WhileGuarding))) : null),
            "combatants[1]", CumulMessage(WorstHit(Strong, 30, 9), Ultime)),
        // Résistance aux altérations plate : base des règles 998 999 981 + 2 × volonté 10 + 10⁶ = 10⁹ + 1, au-delà de la limite 10⁹.
        ["resistance_aux_alterations"] = (with => Duel(14, with ? Lot81CombatFixtures.Mods((PassifB, Mod("statusResistance", "1000000"))) : null,
                stats: new StatRules { BaseStatusResistance = Fixed.FromInt(998_999_981) }),
            "combatants[1]", "résistance aux altérations effective calculée (1000000001) au-delà de la limite technique 1000000000."),
        // PV max +1 000 % : (100 + 10⁸ × 10⁶) × 11 dépasse la plage de Fixed (≈ 9,22 × 10¹⁴) ; sans modificateur, 10¹⁴ + 100 tient.
        // Revue 8.1 : le dépassement est rattaché à la classe (les modificateurs), pas aux attributs, qui tiennent seuls.
        ["pv_max_pourcentage"] = (with => Duel(14, with ? Lot81CombatFixtures.Mods((PassifB, Mod("maxHealth", "10"))) : null,
                stats: new StatRules { MaxHealthPerVitalite = Fixed.FromInt(100_000_000) }, vitalite: Strong),
            "combatants[1].class",
            "statistiques dérivées hors des limites numériques avec les modificateurs permanents de la classe (maxHealth, endurance.max, statusResistance)."),
    };

    public static TheoryData<string> OverflowNames() => new(Overflows.Keys);

    [Theory]
    [MemberData(nameof(OverflowNames))]
    public void Debordement_du_seul_fait_des_modificateurs_refuse_avant_tout_journal(string name)
    {
        var (build, field, message) = Overflows[name];

        // Sans modificateur, le même combat (même build, mêmes règles) est accepté.
        Assert.Empty(build(false).Validate());

        // [L7] Avec eux : NumericLimit sur le combattant, levée par le moteur, journal vide (pas même l'en-tête).
        RuleViolation violation = Lot81CombatFixtures.AssertRefusedBeforeLog(build(true));
        Assert.Equal(RuleViolationKind.NumericLimit, violation.Kind);
        Assert.Equal(field, Assert.Single(violation.Fields));
        Assert.Equal(message, violation.Message);
    }

    [Theory]
    [InlineData(1, true)]    // T = 2 : 29 700 000 × 1,2·10¹¹ = 3,564·10¹⁸
    [InlineData(2, true)]    // T = 4 : 49 500 000 → 5,94·10¹⁸
    [InlineData(3, true)]    // T = 6 : 69 300 000 → 8,316·10¹⁸ ≤ 2⁶³ − 1
    [InlineData(4, false)]   // T = 8 : 89 100 000 → 1,0692·10¹⁹ > 2⁶³ − 1
    [InlineData(5, false)]   // T = 10 : 99 000 000
    public void Degats_infliges_a_chaque_rang_du_talent(int rank, bool accepted)
    {
        // [L1] [L2] damageDealt 2 + 2 × (rang − 1) : la frontière tombe entre les rangs 3 et 4, calculée sans le moteur.
        int talent = 2 + 2 * (rank - 1);
        long worst = WorstHit(Strong, 30, talent);
        Assert.Equal(accepted, worst <= long.MaxValue / MaxImpacts);
        CombatSetup setup = Duel(Strong, Lot81CombatFixtures.Mods((TalentAlpha, Mod("damageDealt", "2", "2"))), alpha: rank);
        if (accepted) Assert.Empty(setup.Validate());
        else Assert.Equal(CumulMessage(worst, Ultime), Lot81CombatFixtures.AssertRefusedBeforeLog(setup).Message);
    }

    [Fact]
    public void Frontiere_de_la_force_avec_degats_infliges_au_rang_maximal()
    {
        // [L2] Pire impact 99 × force (ultime 3 × 2 × force × 10 × 1,1 × 1,5, exact pour toute force entière) ; cumul 99 × force × 1,2·10¹¹.
        // Plus grande force acceptée : ⌊(2⁶³ − 1) / (99 × 1,2·10¹¹)⌋ = 776 378 ; la suivante est refusée.
        long frontier = long.MaxValue / (99 * MaxImpacts);
        Assert.Equal(776_378, frontier);
        Dictionary<string, ModifierDefinition[]> mods = Lot81CombatFixtures.Mods((TalentAlpha, Mod("damageDealt", "2", "2")));

        // Recherche par Validate() seule sur les candidats voisins : l'ensemble accepté est exactement { force ≤ frontière }.
        var accepted = new List<long>();
        for (long force = frontier - 3; force <= frontier + 3; force++)
            if (Duel((int)force, mods).Validate().Count == 0) accepted.Add(force);
        Assert.Equal(new[] { frontier - 3, frontier - 2, frontier - 1, frontier }, accepted);

        RuleViolation violation = Lot81CombatFixtures.AssertRefusedBeforeLog(Duel((int)frontier + 1, mods));
        Assert.Equal((RuleViolationKind.NumericLimit, "combatants[1]"), (violation.Kind, Assert.Single(violation.Fields)));
        Assert.Equal(CumulMessage(99 * (frontier + 1), Ultime), violation.Message);

        // Sans modificateur, la force suivante et même la force maximale restent acceptées : le refus vient des seuls modificateurs.
        Assert.Empty(Duel((int)frontier + 1, null).Validate());
        Assert.Empty(Duel(Strong, null).Validate());

        // À la frontière, le combat est simulé jusqu'au bout sans arrêt (aucun débordement en cours de combat).
        var (result, log) = Run(Duel((int)frontier, mods));
        Assert.Equal(CombatEndReason.DurationReached, result.EndReason);
        Assert.True(log.Lines.Count > 1);
    }

    [Fact]
    public void Frontiere_de_la_resistance_aux_alterations()
    {
        // [L2] Résistance effective = base des règles + 2 × volonté (10) + 10⁶ (modificateur plat) ; limite 10⁹ incluse.
        Dictionary<string, ModifierDefinition[]> mods = Lot81CombatFixtures.Mods((PassifB, Mod("statusResistance", "1000000")));
        long frontier = 1_000_000_000 - 20 - 1_000_000;
        Assert.Empty(Duel(14, mods, stats: new StatRules { BaseStatusResistance = Fixed.FromInt(frontier) }).Validate());
        RuleViolation violation = Lot81CombatFixtures.AssertRefusedBeforeLog(Duel(14, mods, stats: new StatRules { BaseStatusResistance = Fixed.FromInt(frontier + 1) }));
        Assert.Equal((RuleViolationKind.NumericLimit, "combatants[1]"), (violation.Kind, Assert.Single(violation.Fields)));
        Assert.Equal("résistance aux altérations effective calculée (1000000001) au-delà de la limite technique 1000000000.", violation.Message);
    }

    [Fact]
    public void Facteur_maximal_d_un_conditionnel_lu_sur_les_regles_effectives()
    {
        // +1 par Charge : Charges max de base 5 → T = 5, multiplicateur 6, pire impact 59,4 × force = 59 400 000 ; × 1,2·10¹¹ =
        // 7,128·10¹⁸, accepté. Relevées à 10 par un second modificateur (combinaison), le même conditionnel est refusé.
        ModifierDefinition perCharge = Mod("damageDealt", "1", null, ModifierCondition.PerRampartCharge);
        Assert.True(WorstHit(Strong, 30, 5) <= long.MaxValue / MaxImpacts);
        Assert.Empty(Duel(Strong, Lot81CombatFixtures.Mods((Passif, perCharge))).Validate());
        Assert.Equal(CumulMessage(WorstHit(Strong, 30, 10), Ultime),
            Assert.Single(Duel(Strong, Lot81CombatFixtures.Mods((Passif, perCharge), (TalentBeta, Mod("rampartCharges.max", "5")))).Validate()).Message);

        // Charges max abaissées à 0 par un modificateur négatif : facteur 0, plus aucun bonus conditionnel (accepté même à +10 par Charge).
        Assert.Empty(Duel(Strong, Lot81CombatFixtures.Mods((Passif, Mod("damageDealt", "10", null, ModifierCondition.PerRampartCharge)),
            (TalentBeta, Mod("rampartCharges.max", "-5")))).Validate());

        // [L2] +1 par palier de Résonance, facteur ⌊Résonance max / 10⌋ : 69 → 6 paliers, multiplicateur 7, 69,3 × force = 69 300 000,
        // × 1,2·10¹¹ = 8,316·10¹⁸, accepté ; 70 → 7 paliers, multiplicateur 8, 79 200 000 → 9,504·10¹⁸, refusé ; 100 (règles
        // provisoires) → 10 paliers, refusé.
        ModifierDefinition perStep = Mod("damageDealt", "1", null, ModifierCondition.PerResonanceStep);
        CombatRules Resonance(int max) => new() { MaxWorkPerTick = 10_000_000, MaxEventsPerTick = 10_000_000, ResonanceMax = max, UltimateResonanceCost = 50 };
        Assert.Empty(Duel(Strong, Lot81CombatFixtures.Mods((Passif, perStep)), rules: Resonance(50)).Validate());
        Assert.Empty(Duel(Strong, Lot81CombatFixtures.Mods((Passif, perStep)), rules: Resonance(69)).Validate());
        Assert.Equal(CumulMessage(WorstHit(Strong, 30, 7), Ultime),
            Lot81CombatFixtures.AssertRefusedBeforeLog(Duel(Strong, Lot81CombatFixtures.Mods((Passif, perStep)), rules: Resonance(70))).Message);
        Assert.Equal(CumulMessage(WorstHit(Strong, 30, 10), Ultime), Assert.Single(Duel(Strong, Lot81CombatFixtures.Mods((Passif, perStep))).Validate()).Message);
    }

    [Fact]
    public void Modificateurs_negatifs_ou_defensifs_ne_durcissent_jamais_le_precontrole()
    {
        // À la frontière (force 776 378, damageDealt +10 au rang 5), le combat est accepté. Chaque sous-ensemble de modificateurs
        // négatifs ou défensifs ajouté au même personnage le laisse accepté ; à la force suivante, le refus garde le même message
        // (même pire impact : damageTaken n'entre pas dans le pire impact, un conditionnel négatif n'est jamais compté).
        const int frontier = 776_378;
        (string Owner, ModifierDefinition Modifier)[] candidates =
        {
            (TalentBeta, Mod("damageTaken", "-1")),                                                   // réduction maximale permanente
            (TalentBeta, Mod("damageTaken", "-0.5", null, ModifierCondition.WhileGuarding)),
            (TalentGamma, Mod("damageTaken", "-0.1", null, ModifierCondition.PerRampartCharge)),
            (TalentGamma, Mod("damageDealt", "-0.5", null, ModifierCondition.WhileGuarding)),       // conditionnel négatif
            (Passif, Mod("damageDealt", "-0.2", null, ModifierCondition.PerResonanceStep)),
            (Passif, Mod("skill.essai.ultime.coefficient", "-1")),                                   // coefficient abaissé
            (PassifB, Mod("maxHealth", "-0.5")),
            (PassifB, Mod("guard.damageReduction", "0.05")),                                         // défensif, hors du pire impact
        };
        string refusedAlone = Assert.Single(Duel(frontier + 1, Lot81CombatFixtures.Mods((TalentAlpha, Mod("damageDealt", "2", "2")))).Validate()).Message;
        for (int mask = 0; mask < 1 << candidates.Length; mask++)
        {
            var chosen = new List<(string, ModifierDefinition)> { (TalentAlpha, Mod("damageDealt", "2", "2")) };
            for (int k = 0; k < candidates.Length; k++)
                if ((mask & (1 << k)) != 0) chosen.Add(candidates[k]);
            Dictionary<string, ModifierDefinition[]> mods = Lot81CombatFixtures.Mods(chosen.ToArray());
            var gamma = new Dictionary<string, int> { [TalentAlpha] = 5, [TalentBeta] = 1, [TalentGamma] = 1 };
            CombatSetup atFrontier = Lot81CombatFixtures.Make(new CombatantSetup[] { Dummy(), Character(definition: Class(modifiers: mods, secondPassive: true),
                attributes: new Attributes(frontier, 8, 6, 14, 10, 0), talents: gamma, passives: new[] { Passif, PassifB }) }, new[] { Cmd(0, Frappe) }, DurationMs, rules: Wide);
            IReadOnlyList<RuleViolation> errors = atFrontier.Validate();
            Assert.True(errors.Count == 0, $"sous-ensemble {mask} : {string.Join(" ; ", errors)}");

            // Le coefficient abaissé de l'ultime (3 − 1) change le pire impact (Frappe, ultime 2) : refus comparé seulement sans lui.
            if ((mask & (1 << 5)) != 0) continue;
            CombatSetup above = Lot81CombatFixtures.Make(new CombatantSetup[] { Dummy(), Character(definition: Class(modifiers: mods, secondPassive: true),
                attributes: new Attributes(frontier + 1, 8, 6, 14, 10, 0), talents: gamma, passives: new[] { Passif, PassifB }) }, new[] { Cmd(0, Frappe) }, DurationMs, rules: Wide);
            Assert.Equal(refusedAlone, Assert.Single(above.Validate()).Message);
        }
    }

    [Fact]
    public void Degats_subis_du_defenseur_sans_effet_sur_le_pire_impact_de_l_attaquant()
    {
        // D7 : la mitigation 0 reste le pire cas. Un défenseur ennemi réel avec damageTaken −1 (et conditionnels) ne relâche pas le
        // précontrôle de l'attaquant : même acceptation à la frontière, même refus juste au-dessus, défenseur en premier.
        Dictionary<string, ModifierDefinition[]> attackerMods = Lot81CombatFixtures.Mods((TalentAlpha, Mod("damageDealt", "2", "2")));
        ClassDefinition defenderClass = Class(modifiers: Lot81CombatFixtures.Mods((Passif, Mod("damageTaken", "-1")),
            (Passif, Mod("damageTaken", "-1", null, ModifierCondition.WhileGuarding))));
        CombatSetup Versus(int force) => Lot81CombatFixtures.Make(new CombatantSetup[]
        {
            Character("mur", "cibles", definition: defenderClass, passives: new[] { Passif }),
            Character(definition: Class(modifiers: attackerMods), attributes: new Attributes(force, 8, 6, 14, 10, 0), talents: new Dictionary<string, int> { [TalentAlpha] = 5 }),
        }, new[] { Cmd(0, Frappe, target: "mur") }, DurationMs, rules: Wide);
        Assert.Empty(Versus(776_378).Validate());
        RuleViolation violation = Assert.Single(Versus(776_379).Validate());
        Assert.Equal((RuleViolationKind.NumericLimit, "combatants[1]"), (violation.Kind, Assert.Single(violation.Fields)));
        Assert.Equal(CumulMessage(99L * 776_379, Ultime), violation.Message);
    }
}

/// <summary>
/// Sous-lot 8.1 (D5) : majorant prudent de la file (<see cref="CombatSetup.ComputeQueueBound"/>) avec les règles effectives. Fenêtre d'une
/// altération D = base + max(0, plus grand durationDealt) + max(0, plus grand durationTaken), écrêtée à 24 h ; recharge effective dans
/// l'espacement des commandes ; rupture de garde (jamais de durationDealt). Valeurs exactes calculées à la main avec la formule
/// documentée, et refus de saturation dû au seul modificateur.
/// </summary>
[Trait("Category", "Modifier")]
[Trait("Category", "Limits")]
public class Lot81ModifierQueueBoundTests
{
    /// <summary>Assaillant qui pose Fragilité (refresh, 6 s) à chaque tick sur un personnage résistant, porteur éventuel d'un durationTaken.</summary>
    private static CombatSetup FragilityEachTick(string? takenMs, CombatRules? rules = null)
    {
        ClassDefinition definition = takenMs is null ? Essai : Class(modifiers: Lot81CombatFixtures.Mods((Passif, Mod("status.fragility.durationTaken", takenMs))));
        CharacterSetup tank = Character(definition: definition, attributes: Attrs(vitalite: 200), passives: new[] { Passif });
        return Setup(Array.Empty<CombatCommand>(), new CombatantSetup[] { tank, Assailant(0, 50, attacks: Strike("coup", 1, applies: Apply("fragility"))) },
            durationMs: 10_000, rules: rules);
    }

    [Theory]
    // Assaillant toutes les 50 ms (intervalle 1 tick), 10 s = 200 tours ; k = min(200, (D − 1) / 1 + 1) applications vivantes, D = (6000 + x) / 50.
    [InlineData(null, 120)]      // sans modificateur : D = 120
    [InlineData("-1000", 120)]   // un raccourci ne réduit jamais le majorant : max(0, −1000) = 0
    [InlineData("1000", 140)]    // D = 7000 / 50
    [InlineData("3950", 199)]    // [L2] N − 1 : D = 199
    [InlineData("4000", 200)]    // N : D = 200 = nombre de tours
    [InlineData("4050", 200)]    // N + 1 : D = 201, borné par les 200 tours du combat
    public void Duree_subie_allonge_la_fenetre_d_une_alteration_refresh(string? takenMs, long expiries)
    {
        CombatSetup.QueueBound bound = FragilityEachTick(takenMs).ComputeQueueBound();
        Assert.Equal((1L, 0L, expiries, 0L, true), (bound.Initial, bound.Dots, bound.Expiries, bound.Transient, bound.Guaranteed));
    }

    /// <summary>Personnage qui lance Givre (gel, keepLonger, 4 s) ou Assommer à chaque tick pendant 2 s sur le mannequin.</summary>
    private static CombatSetup CastsEachTick(string skill, ClassDefinition definition, CombatRules? rules = null, int count = 40) =>
        Setup(Enumerable.Range(0, count).Select(i => Cmd(i * 50L, skill)),
            new CombatantSetup[] { Character(definition: definition, actives: new[] { Givre, Assommer, Frappe, Gratuite }, passives: new[] { Passif }), Dummy() },
            rules: rules);

    [Theory]
    // GCD 1000 ms = 20 ticks, Givre sans recharge : espacement 20 ; applications en D ticks = (D − 1) / 20 + 1, gel en keepLonger :
    // min(applications, 1 cible × D). 40 commandes, toujours plus que le débit.
    [InlineData(null, 4)]       // D = 80 : 79 / 20 + 1 = 4
    [InlineData("50", 5)]       // D = 81 : 80 / 20 + 1 = 5 (une seule durée de tick de plus suffit)
    [InlineData("1000", 5)]     // D = 100 : 99 / 20 + 1 = 5
    [InlineData("1050", 6)]     // D = 101 : 100 / 20 + 1 = 6
    [InlineData("-3000", 4)]    // raccourci : sans effet sur le majorant
    public void Duree_infligee_allonge_la_fenetre_d_une_alteration_keep_longer(string? dealtMs, long expiries)
    {
        ClassDefinition definition = dealtMs is null ? Essai : Class(modifiers: Lot81CombatFixtures.Mods((Passif, Mod("status.freeze.durationDealt", dealtMs))));
        CombatSetup.QueueBound bound = CastsEachTick(Givre, definition).ComputeQueueBound();
        Assert.Equal((40L, 0L, expiries), (bound.Initial, bound.Dots, bound.Expiries));
    }

    [Fact]
    public void Duree_infligee_sans_effet_sur_une_alteration_ignore()
    {
        // Étourdissement en ignore : une échéance vivante par cible (1), plus l'immunité accordée à l'expiration (ignore, 1), quelle que
        // soit la durée infligée.
        foreach (string? dealt in new[] { null, "1000", "86400000" })
        {
            ClassDefinition definition = dealt is null ? Essai : Class(modifiers: Lot81CombatFixtures.Mods((Passif, Mod("status.stun.durationDealt", dealt))));
            Assert.Equal(2L, CastsEachTick(Assommer, definition).ComputeQueueBound().Expiries);
        }
    }

    /// <summary>Classe d'essai dont Givre (gel) a une recharge de 3 s (60 ticks, trois fois le GCD), modificateurs au choix.</summary>
    private static ClassDefinition SlowFrost(Dictionary<string, ModifierDefinition[]>? modifiers)
    {
        ClassDefinition basis = Class(modifiers: modifiers);
        var frost = new SkillCombatDefinition(PowerType.Physique, DamageType.Physique, Fixed.Parse("0.1"), 0, 3000, applies: new[] { Apply("freeze") });
        return new ClassDefinition(basis.Id, basis.Name, basis.Trees,
            basis.Skills.Select(s => s.Id == Givre ? new SkillDefinition(s.Id, s.Name, s.Kind, combat: frost, modifiers: s.Modifiers) : s).ToArray());
    }

    [Theory]
    // Givre en 3000 ms (60 ticks), GCD 20 ticks : espacement max(20, recharge effective / 50) ; D = 80 ; applications (79 / espacement) + 1.
    [InlineData(null, 1000, 2)]        // espacement 60 : 79 / 60 + 1 = 2
    [InlineData("-1000", 1000, 2)]     // 2000 ms, 40 ticks : 79 / 40 + 1 = 2
    [InlineData("-1050", 1000, 3)]     // [L2] 1950 ms, 39 ticks : 79 / 39 + 1 = 3
    [InlineData("-2000", 1000, 4)]     // 1000 ms, 20 ticks : 79 / 20 + 1 = 4
    [InlineData("-3000", 1000, 4)]     // 0 ms : le GCD (20 ticks) espace seul
    [InlineData("-5000", 1000, 4)]     // écrêtée à 0
    [InlineData("1000", 1000, 1)]      // 4000 ms, 80 ticks : 79 / 80 + 1 = 1 (une hausse réduit le compte)
    [InlineData(null, 0, 2)]           // sans GCD : espacement 60, 2
    [InlineData("-3000", 0, 40)]       // sans GCD ni recharge : 4 × 80 = 320 par fenêtre, borné par les 40 commandes
    public void Reduction_de_recharge_augmente_les_applications_comptees(string? cooldownMs, long gcdMs, long expiries)
    {
        ClassDefinition definition = SlowFrost(cooldownMs is null ? null : Lot81CombatFixtures.Mods((Passif, Mod("skill.essai.givre.cooldownMs", cooldownMs))));
        CombatSetup setup = CastsEachTick(Givre, definition, new CombatRules { GlobalCooldownMs = gcdMs });
        Assert.Empty(setup.Validate());
        Assert.Equal((40L, expiries), (setup.ComputeQueueBound().Initial, setup.ComputeQueueBound().Expiries));
    }

    /// <summary>
    /// Catalogue provisoire où la rupture de garde se renouvelle (refresh) au lieu d'être ignorée : sa fenêtre entre dans le majorant.
    /// Capacité de la file au plafond technique, pour accepter la fenêtre de 24 h.
    /// </summary>
    private static readonly CombatRules RefreshBreak = new()
    {
        MaxPendingEvents = CombatRules.AbsoluteMaxPendingEvents,
        Statuses = new StatusCatalog(StatusCatalog.Provisional.Definitions.Select(d => d.Id == "guardBreak"
            ? new StatusDefinition("guardBreak", StatusCategory.Control, 3000, StatusStacking.Refresh,
                blocks: StatusBlocks.Guard | StatusBlocks.Parry | StatusBlocks.Regeneration, blockReason: "guardBroken")
            : d)),
    };

    [Theory]
    // Un personnage qui prend la garde ; rupture en refresh : 1 × D échéances, D = (3000 + durationTaken positif) / 50.
    [InlineData(null, 60)]
    [InlineData("1000", 80)]
    [InlineData("-1000", 60)]
    [InlineData("86400000", 1_728_000)]   // 3000 + 86 400 000 écrêté à 24 h : 86 400 000 / 50
    public void Fenetre_de_la_rupture_de_garde_par_la_duree_subie(string? takenMs, long expiries)
    {
        ClassDefinition definition = takenMs is null ? Essai : Class(modifiers: Lot81CombatFixtures.Mods((Passif, Mod("status.guardBreak.durationTaken", takenMs))));
        CombatSetup setup = Setup(new[] { GuardStart(0) },
            new CombatantSetup[] { Character(definition: definition, passives: new[] { Passif }), Dummy() }, rules: RefreshBreak);
        Assert.Empty(setup.Validate());
        Assert.Equal((1L, expiries), (setup.ComputeQueueBound().Initial, setup.ComputeQueueBound().Expiries));

        // Rupture en ignore (catalogue provisoire) : elle remplace une commande guardStart retirée, rien n'est ajouté.
        Assert.Equal(0L, Setup(new[] { GuardStart(0) }, new CombatantSetup[] { Character(definition: definition, passives: new[] { Passif }), Dummy() })
            .ComputeQueueBound().Expiries);
    }

    [Fact]
    public void Rupture_de_garde_sans_duree_infligee()
    {
        // La fenêtre de la rupture ne lit que la durée subie : un durationDealt sur la rupture de garde ne peut même pas être déclaré.
        CombatSetup setup = Setup(new[] { GuardStart(0) }, new CombatantSetup[]
        {
            Character(definition: Class(modifiers: Lot81CombatFixtures.Mods((Passif, Mod("status.guardBreak.durationDealt", "1000")))), passives: new[] { Passif }), Dummy(),
        }, rules: RefreshBreak);
        RuleViolation violation = Assert.Single(setup.Validate());
        Assert.Equal("combatants[0].class", Assert.Single(violation.Fields));
        Assert.Contains("durationDealt sans objet", violation.Message);
    }

    [Fact]
    public void Duree_d_un_dot_sans_effet_sur_le_compte_des_instances()
    {
        // Un DOT ne programme qu'un tick par instance (source, cible, altération) : allonger sa durée (subie ou infligée) ne change ni
        // les instances ni les échéances comptées. Assaillant qui pose brûlure à chaque tick : 1 instance, 0 échéance.
        foreach (string? taken in new[] { null, "2000", "86400000" })
        {
            ClassDefinition definition = taken is null ? Essai : Class(modifiers: Lot81CombatFixtures.Mods((Passif, Mod("status.burn.durationTaken", taken)),
                (Passif, Mod("status.burn.durationDealt", "4000"))));
            CombatSetup setup = Setup(Array.Empty<CombatCommand>(), new CombatantSetup[]
            {
                Character(definition: definition, attributes: Attrs(vitalite: 200), passives: new[] { Passif }),
                Assailant(0, 50, attacks: Strike("feu", 1, DamageType.Magique, applies: Apply("burn", "0.5"))),
            });
            Assert.Empty(setup.Validate());
            CombatSetup.QueueBound bound = setup.ComputeQueueBound();
            Assert.Equal((1L, 1L, 0L), (bound.Initial, bound.Dots, bound.Expiries));
        }
    }

    [Fact]
    public void Saturation_refusee_du_seul_fait_d_un_modificateur()
    {
        // Sans modificateur : majorant 1 + 120 = 121. Avec durationTaken +1000 ms : 1 + 140 = 141.
        Assert.Equal(121L, FragilityEachTick(null).ComputeQueueBound().Total);
        Assert.Equal(141L, FragilityEachTick("1000").ComputeQueueBound().Total);

        // Capacité 121 : le combat sans modificateur passe, le même avec le modificateur est refusé avant tout journal.
        var tight = new CombatRules { MaxPendingEvents = 121 };
        Assert.Empty(FragilityEachTick(null, tight).Validate());
        RuleViolation violation = Lot81CombatFixtures.AssertRefusedBeforeLog(FragilityEachTick("1000", tight));
        Assert.Equal((RuleViolationKind.LimitExceeded, "combatants"), (violation.Kind, Assert.Single(violation.Fields)));
        Assert.Equal("majorant prudent de la file 141 (événements initiaux 1, instances de DOT 0, échéances d'altérations 140) " +
                     "au-delà de sa capacité 121 (combat.limits.maxPendingEvents).", violation.Message);

        // [L2] Capacité 140 (N − 1) : refusé ; 141 (N) : accepté, simulé, pic mesuré au-delà de l'ancien majorant et au plus au nouveau.
        Assert.Equal(RuleViolationKind.LimitExceeded, Assert.Single(FragilityEachTick("1000", new CombatRules { MaxPendingEvents = 140 }).Validate()).Kind);
        var (result, _) = Run(FragilityEachTick("1000", new CombatRules { MaxPendingEvents = 141 }));
        Assert.Equal(CombatEndReason.DurationReached, result.EndReason);
        Assert.InRange(result.Peaks.PendingEvents, 122, 141);
    }

    /// <summary>Scénarios avec modificateurs de durée ou de recharge, paramétrés par la capacité de la file seule.</summary>
    public static readonly Dictionary<string, Func<int, CombatSetup>> Scenarios = new()
    {
        ["fragilite_subie"] = capacity => FragilityEachTick("1000", new CombatRules { MaxPendingEvents = capacity }),
        ["gel_inflige"] = capacity => CastsEachTick(Givre, Class(modifiers: Lot81CombatFixtures.Mods((Passif, Mod("status.freeze.durationDealt", "1050")))),
            new CombatRules { MaxPendingEvents = capacity }),
        ["gel_recharge_reduite"] = capacity => CastsEachTick(Givre, SlowFrost(Lot81CombatFixtures.Mods((Passif, Mod("skill.essai.givre.cooldownMs", "-1050")))),
            new CombatRules { MaxPendingEvents = capacity }),
        ["etourdissement_et_immunite_subis"] = capacity => Setup(Array.Empty<CombatCommand>(), new CombatantSetup[]
        {
            Character(definition: Class(modifiers: Lot81CombatFixtures.Mods((Passif, Mod("status.stun.durationTaken", "1000")),
                (Passif, Mod("status.stunImmunity.durationTaken", "1000")))), attributes: Attrs(vitalite: 200), passives: new[] { Passif }),
            Assailant(0, 50, attacks: Strike("coup", 1, applies: Apply("stun"))),
        }, rules: new CombatRules { MaxPendingEvents = capacity }),
        ["rupture_refresh_subie"] = capacity => Setup(new[] { GuardStart(0), GuardStart(4000) }, new CombatantSetup[]
        {
            Character(definition: Class(modifiers: Lot81CombatFixtures.Mods((Passif, Mod("status.guardBreak.durationTaken", "1000")))), attributes: Attrs(vitalite: 200),
                passives: new[] { Passif }),
            Assailant(1000, 500),
        }, rules: new CombatRules
        {
            Statuses = RefreshBreak.Statuses, GuardCostPerBlock = 1000, GuardUpkeepPerSecond = 0, MaxPendingEvents = capacity,
        }),
    };

    public static TheoryData<string> ScenarioNames() => new(Scenarios.Keys);

    [Theory]
    [MemberData(nameof(ScenarioNames))]
    public void Majorant_avec_modificateurs_suffisant_et_atteint_au_plus(string name)
    {
        // Comme au correctif 7.2, avec les règles effectives : au majorant, accepté et simulé sans arrêt, pic mesuré au plus égal ;
        // à majorant − 1, refusé avant toute ligne (LimitExceeded sur combatants).
        Func<int, CombatSetup> build = Scenarios[name];
        long bound = build(100_000).ComputeQueueBound().Total;
        Assert.True(build(100_000).ComputeQueueBound().Guaranteed);
        var (result, _) = Run(build((int)bound));
        Assert.NotEqual(CombatEndReason.Aborted, result.EndReason);
        Assert.InRange(result.Peaks.PendingEvents, 1, bound);
        RuleViolation violation = Lot81CombatFixtures.AssertRefusedBeforeLog(build((int)bound - 1));
        Assert.Equal((RuleViolationKind.LimitExceeded, "combatants"), (violation.Kind, Assert.Single(violation.Fields)));
        if (name == "rupture_refresh_subie") Assert.True(result["bastion"].GuardBreaks >= 1);
    }

    [Fact]
    public void Majorant_independant_de_la_repartition_des_modificateurs()
    {
        // [L5] Mêmes ajouts (somme 1000 ms sur la Fragilité subie, 500 ms sur le gel infligé), répartis autrement entre talent et passifs
        // ou déclarés dans un autre ordre : majorant identique.
        CharacterSetup With(Dictionary<string, ModifierDefinition[]> mods) => Character(definition: Class(modifiers: mods, secondPassive: true),
            attributes: Attrs(vitalite: 200), actives: new[] { Givre, Frappe, Heurt, Gratuite }, talents: new Dictionary<string, int> { [TalentAlpha] = 1 },
            passives: new[] { Passif, PassifB });
        CombatSetup Make(Dictionary<string, ModifierDefinition[]> mods) => Setup(Enumerable.Range(0, 20).Select(i => Cmd(i * 50L, Givre, target: "assaillant")),
            new CombatantSetup[] { With(mods), Assailant(0, 50, attacks: Strike("coup", 1, applies: Apply("fragility"))) });
        var distributions = new[]
        {
            Lot81CombatFixtures.Mods((TalentAlpha, Mod("status.fragility.durationTaken", "1000")), (TalentAlpha, Mod("status.freeze.durationDealt", "500"))),
            Lot81CombatFixtures.Mods((TalentAlpha, Mod("status.freeze.durationDealt", "500")), (TalentAlpha, Mod("status.fragility.durationTaken", "1000"))),
            Lot81CombatFixtures.Mods((Passif, Mod("status.fragility.durationTaken", "400")), (PassifB, Mod("status.fragility.durationTaken", "600")),
                (TalentAlpha, Mod("status.freeze.durationDealt", "500"))),
            Lot81CombatFixtures.Mods((PassifB, Mod("status.freeze.durationDealt", "1500")), (Passif, Mod("status.freeze.durationDealt", "-1000")),
                (TalentAlpha, Mod("status.fragility.durationTaken", "1000"))),
        };
        var bounds = distributions.Select(d => Make(d).ComputeQueueBound()).Select(b => (b.Initial, b.Dots, b.Expiries)).ToList();
        // Fragilité : D = 140, assaillant à chaque tick → 140 ; gel : D = 90, (89 / 20) + 1 = 5 applications ; 20 commandes + 1 tour.
        Assert.All(bounds, b => Assert.Equal((21L, 0L, 145L), b));
    }
}

/// <summary>
/// Sous-lot 8.1 : atomicité, immuabilité et déterminisme d'un combat avec modificateurs. Un combat refusé n'écrit rien et ne modifie
/// aucune entrée ; une classe, un build et une description de combat sont inchangés après validation et simulation ; deux moteurs du
/// même combat sont indépendants ; le journal est reproductible octet par octet, et une autre graine ne change que les lignes qui
/// dépendent du tirage critique.
/// </summary>
[Trait("Category", "Modifier")]
[Trait("Category", "Engine")]
public class Lot81ModifierAtomicityTests
{
    /// <summary>
    /// Combat riche : un personnage avec des modificateurs de toutes les familles (garde, parade, Charges, durées, coefficient, recharge,
    /// X2 en garde, par Charge, par palier) face à un assaillant qui pose Fragilité, et une rotation de compétences, garde et parade.
    /// </summary>
    internal static CombatSetup Rich(long seed = 42, int durationMs = 20_000)
    {
        Dictionary<string, ModifierDefinition[]> mods = Lot81CombatFixtures.Mods(
            (TalentAlpha, Mod("guard.damageReduction", "0.02", "0.01")),
            (TalentAlpha, Mod("damageDealt", "0.05", "0.05", ModifierCondition.PerResonanceStep)),
            (TalentBeta, Mod("damageTaken", "-0.05", "-0.01", ModifierCondition.WhileGuarding)),
            (TalentBeta, Mod("status.fragility.durationTaken", "1000")),
            (Passif, Mod("skill.essai.frappe.coefficient", "0.3")),
            (Passif, Mod("skill.essai.frappe.cooldownMs", "-2000")),
            (Passif, Mod("parry.windowMs", "100")),
            (PassifB, Mod("damageTaken", "-0.01", null, ModifierCondition.PerRampartCharge)),
            (PassifB, Mod("rampartCharges.max", "2")),
            (PassifB, Mod("endurance.max", "50")));
        CharacterSetup bastion = Character(definition: Class(modifiers: mods, secondPassive: true), attributes: Attrs(vitalite: 200, endurance: 50),
            talents: new Dictionary<string, int> { [TalentAlpha] = 3, [TalentBeta] = 2 }, passives: new[] { Passif, PassifB });
        var commands = new List<CombatCommand> { GuardStart(0), GuardStop(2500), Parry(2950), GuardStart(3500), GuardStop(6500), Parry(6950) };
        for (long t = 0; t < durationMs; t += 1000)
        {
            commands.Add(Cmd(t, Frappe, target: "assaillant"));
            commands.Add(Cmd(t, Gratuite, target: "assaillant"));
            commands.Add(Cmd(t, Onde, target: null));
        }
        return Setup(commands, new CombatantSetup[] { bastion, Assailant(1000, 2000, attacks: new[] { Blow(fragility: true), Blow(id: "coup_bis") }) },
            seed: seed, durationMs: durationMs);
    }

    [Fact]
    public void Combat_riche_valide_et_simule_avec_ses_regles_effectives()
    {
        // Garde-fou des tests suivants : le combat riche est valide, simulé jusqu'au bout, et ses modificateurs sont bien actifs.
        CombatSetup setup = Rich();
        Assert.Empty(setup.Validate());
        var (result, log) = Run(setup);
        Assert.Equal(CombatEndReason.DurationReached, result.EndReason);
        var events = Events(log);
        var rules = events.OfKind("combatant").Single(e => e.Str("combatant") == "bastion").GetProperty("rules");
        Assert.Equal("0.64", rules.GetProperty("guard.damageReduction").GetRawText());   // 0,6 + 0,02 + 0,01 × 2
        Assert.Equal("250", rules.GetProperty("parry.windowMs").GetRawText());           // 150 + 100
        Assert.Equal("7", rules.GetProperty("rampartCharges.max").GetRawText());         // 5 + 2
        Assert.True(result["bastion"].PerfectParries >= 1 && result["bastion"].Blocks >= 1);
    }

    // --- [L8] Aucune mutation partielle ---------------------------------------------------------------------------------------

    public static TheoryData<string> RefusedNames() => new(Refused.Keys);

    /// <summary>Un combat refusé de chaque famille : référence au catalogue, précontrôle numérique, saturation de la file.</summary>
    private static readonly Dictionary<string, Func<CombatSetup>> Refused = new()
    {
        ["reference"] = () => Setup(new[] { Cmd(0, Frappe) }, new CombatantSetup[]
        {
            Character(definition: Class(modifiers: Lot81CombatFixtures.Mods((Passif, Mod("status.poison.durationTaken", "1000")))), passives: new[] { Passif }), Dummy(),
        }),
        ["numerique"] = () => Lot81CombatFixtures.Make(new CombatantSetup[]
        {
            Dummy(), Character(definition: Class(modifiers: Lot81CombatFixtures.Mods((TalentAlpha, Mod("damageDealt", "2", "2")))),
                attributes: new Attributes(1_000_000, 8, 6, 14, 10, 0), talents: new Dictionary<string, int> { [TalentAlpha] = 5 }),
        }, new[] { Cmd(0, Frappe) }, 600_000, rules: new CombatRules { MaxWorkPerTick = 10_000_000, MaxEventsPerTick = 10_000_000 }),
        ["saturation"] = () => Setup(Array.Empty<CombatCommand>(), new CombatantSetup[]
        {
            Character(definition: Class(modifiers: Lot81CombatFixtures.Mods((Passif, Mod("status.fragility.durationTaken", "1000")))), attributes: Attrs(vitalite: 200),
                passives: new[] { Passif }),
            Assailant(0, 50, attacks: Strike("coup", 1, applies: Apply("fragility"))),
        }, rules: new CombatRules { MaxPendingEvents = 121 }),
    };

    [Theory]
    [MemberData(nameof(RefusedNames))]
    public void Combat_refuse_n_ecrit_rien_et_ne_modifie_aucune_entree(string name)
    {
        CombatSetup setup = Refused[name]();
        var character = (CharacterSetup)setup.Combatants.Single(c => c is CharacterSetup);
        string before = Lot81CombatFixtures.Describe(setup);
        IReadOnlyList<ModifierKey> keysBefore = ModifierSet.For(character).Keys;

        // Refus répété à l'identique, sans aucune écriture, puis entrées inchangées.
        string first = string.Join(" | ", setup.Validate());
        Lot81CombatFixtures.AssertRefusedBeforeLog(setup);
        Lot81CombatFixtures.AssertRefusedBeforeLog(setup);
        Assert.Equal(first, string.Join(" | ", setup.Validate()));
        Assert.Equal(before, Lot81CombatFixtures.Describe(setup));
        Assert.Equal(keysBefore, ModifierSet.For(character).Keys);
    }

    // --- [L6] Immuabilité -----------------------------------------------------------------------------------------------------

    [Fact]
    public void Classe_build_et_combat_inchanges_apres_validation_et_simulation()
    {
        CombatSetup setup = Rich();
        var character = (CharacterSetup)setup.Combatants[0];
        string before = Lot81CombatFixtures.Describe(setup);
        CombatantRules rulesBefore = CombatantRules.For(setup.Combat, ModifierSet.For(character));
        string effectiveBefore = Effective(rulesBefore, character);

        Assert.Empty(setup.Validate());
        setup.ComputeQueueBound();
        string firstLog = Run(setup).Log.ToString();
        string secondLog = Run(setup).Log.ToString();

        Assert.Equal(before, Lot81CombatFixtures.Describe(setup));
        Assert.Equal(effectiveBefore, Effective(CombatantRules.For(setup.Combat, ModifierSet.For(character)), character));
        Assert.Equal(firstLog, secondLog);
        // Les objets construits avant la simulation n'ont pas bougé non plus.
        Assert.Equal(effectiveBefore, Effective(rulesBefore, character));
    }

    /// <summary>Valeurs effectives d'un combattant, en texte (règles X1, coefficients et recharges des compétences, durées).</summary>
    private static string Effective(CombatantRules r, CharacterSetup character) =>
        $"{r.GuardDamageReduction} {r.GuardUpkeepPerSecond} {r.GuardCostPerBlock} {r.GuardNoUpkeepBreak} {r.GuardReductionAtZeroEndurance} " +
        $"{r.GuardBreakShortenPerChargeMs} {r.ParryWindowMs} {r.ParryFailureRecoveryMs} {r.RampartChargesMax} {r.ResonancePerDirectHit} " +
        $"{r.EnduranceRegenPerSecond} {r.DamageDealt} {r.DamageTaken} {r.DurationTakenMs("fragility")} {r.DamageTakenAt(true, 3, 40)} {r.DamageDealtAt(false, 0, 40)} " +
        string.Join(",", character.Class.Skills.Where(s => s.Combat != null).Select(s => $"{s.Id}:{r.Coefficient(s, s.Combat!)}:{r.CooldownMs(s, s.Combat!)}"));

    [Fact]
    public void Sources_des_modificateurs_modifiees_apres_construction_sans_effet()
    {
        // Les tableaux passés aux définitions sont copiés : les modifier après coup ne change ni la classe, ni la validation, ni le journal.
        var conditions = new[] { ModifierCondition.WhileGuarding };
        var talentModifiers = new[]
        {
            new ModifierDefinition("damageTaken", new RankedValue(Fixed.Parse("-0.2"), Fixed.Zero), conditions),
            Mod("guard.damageReduction", "0.1"),
        };
        ClassDefinition definition = Class(modifiers: new Dictionary<string, ModifierDefinition[]> { [TalentAlpha] = talentModifiers });
        CombatSetup setup = Setup(new[] { GuardStart(0) }, new CombatantSetup[]
        {
            Character(definition: definition, attributes: Attrs(vitalite: 200), talents: new Dictionary<string, int> { [TalentAlpha] = 1 }), Assailant(),
        });
        string reference = Run(setup).Log.ToString();
        string description = Lot81CombatFixtures.Describe(setup);

        conditions[0] = ModifierCondition.PerResonanceStep;
        talentModifiers[0] = Mod("damageTaken", "-1");
        talentModifiers[1] = Mod("guard.damageReduction", "-0.6");

        Assert.Equal(description, Lot81CombatFixtures.Describe(setup));
        Assert.Equal(new[] { ModifierCondition.WhileGuarding }, definition.Trees[0].Talents[0].Modifiers[0].Conditions);
        Assert.Throws<NotSupportedException>(() => ((IList<ModifierCondition>)definition.Trees[0].Talents[0].Modifiers[0].Conditions)[0] = ModifierCondition.PerRampartCharge);
        Assert.Throws<NotSupportedException>(() => ((IList<ModifierDefinition>)definition.Trees[0].Talents[0].Modifiers).Clear());
        Assert.Empty(setup.Validate());
        Assert.Equal(reference, Run(setup).Log.ToString());
    }

    [Fact]
    public void Deux_moteurs_du_meme_combat_independants_et_identiques()
    {
        // Deux moteurs construits sur la même description, avancés en alternance : aucun état partagé (règles effectives, file,
        // générateurs), journaux identiques entre eux et à une simulation seule.
        CombatSetup setup = Rich();
        var logA = new MemoryCombatLog();
        var logB = new MemoryCombatLog();
        var a = new CombatEngine(setup, logA);
        var b = new CombatEngine(setup, logB);
        bool runningA = true, runningB = true;
        while (runningA || runningB)
        {
            if (runningA) runningA = a.Step();
            if (runningB) runningB = b.Step();
            if (runningB) runningB = b.Step();   // B prend de l'avance : l'ordre d'avancement ne compte pas
        }
        Assert.True(CombatReplay.Compare(logA.Lines, logB.Lines).Identical);
        Assert.Equal(Run(setup).Log.ToString(), logA.ToString());
    }

    // --- [L9] Déterminisme et rejeu -------------------------------------------------------------------------------------------

    [Fact]
    public void Meme_combat_avec_modificateurs_journal_identique_octet_par_octet()
    {
        // Même description, puis description reconstruite à l'identique (rejeu depuis les mêmes entrées) : mêmes octets, même empreinte.
        string first = Run(Rich()).Log.ToString();
        string second = Run(Rich()).Log.ToString();
        CombatSetup setup = Rich();
        string third = Run(setup).Log.ToString();
        string fourth = Run(setup).Log.ToString();
        foreach (string other in new[] { second, third, fourth })
        {
            Assert.Equal(Encoding.UTF8.GetBytes(first), Encoding.UTF8.GetBytes(other));
            Assert.Equal(Hash(first), Hash(other));
        }
        Assert.True(CombatReplay.Compare(first.Split('\n'), fourth.Split('\n')).Identical);
    }

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    [Fact]
    public void Graines_differentes_seules_les_lignes_aleatoires_changent()
    {
        // Seul le tirage critique du personnage est aléatoire (l'assaillant ne critique jamais). Il change le montant et le cumul des
        // dégâts des impacts du personnage, et les bilans de fin ; tout le reste (règles effectives, Résonance, garde, parade, durées,
        // conditions X2) est identique ligne pour ligne.
        IReadOnlyList<string> reference = Run(Rich(seed: 1)).Log.Lines;
        int differingSeeds = 0;
        for (long seed = 2; seed <= 10; seed++)
        {
            IReadOnlyList<string> other = Run(Rich(seed: seed)).Log.Lines;
            Assert.Equal(reference.Count, other.Count);

            // En-tête : seule la graine du scénario diffère.
            JsonObject h1 = JsonNode.Parse(reference[0])!.AsObject(), h2 = JsonNode.Parse(other[0])!.AsObject();
            JsonObject s1 = h1["scenario"]!.AsObject(), s2 = h2["scenario"]!.AsObject();
            Assert.Equal((1L, seed), (s1["seed"]!.GetValue<long>(), s2["seed"]!.GetValue<long>()));
            s1.Remove("seed");
            s2.Remove("seed");
            Assert.Equal(h1.ToJsonString(), h2.ToJsonString());

            bool differs = false;
            for (int i = 1; i < reference.Count; i++)
            {
                if (reference[i] == other[i]) continue;
                differs = true;
                JsonObject x = JsonNode.Parse(reference[i])!.AsObject(), y = JsonNode.Parse(other[i])!.AsObject();
                string kind = x["event"]!.GetValue<string>();
                Assert.Equal(kind, y["event"]!.GetValue<string>());
                string[] random = kind switch
                {
                    // Impact du personnage : critique, montant, cumul de la cible ; impact subi par le personnage : rien d'aléatoire.
                    "damage" when x["source"]!.GetValue<string>() == "bastion" => new[] { "critical", "amount", "damageTaken" },
                    "combatantSummary" => new[] { "damageDealt", "damageTaken", "criticalHits" },
                    _ => Array.Empty<string>(),
                };
                Assert.True(random.Length > 0, $"ligne {i + 1} ({kind}) changée par la graine {seed} : {reference[i]} / {other[i]}");
                foreach (string field in random)
                {
                    x.Remove(field);
                    y.Remove(field);
                }
                Assert.Equal(x.ToJsonString(), y.ToJsonString());
            }
            if (differs) differingSeeds++;
        }
        Assert.True(differingSeeds >= 1, "aucune graine n'a changé un tirage critique");
    }

    [Fact]
    public void Repartition_et_ordre_des_sources_sans_effet_sur_le_journal()
    {
        // [L5] Mêmes sommes par clé (et par ensemble de conditions), sources permutées ou réparties autrement : même validation,
        // même majorant, même journal octet par octet (l'objet rules du journal est la forme agrégée).
        CombatSetup Make(Dictionary<string, ModifierDefinition[]> mods) => Setup(
            new[] { GuardStart(0), Cmd(500, Frappe, target: "assaillant"), GuardStop(4000), Parry(4950), Cmd(6000, Gratuite, target: "assaillant") },
            new CombatantSetup[]
            {
                Character(definition: Class(modifiers: mods, secondPassive: true), attributes: Attrs(vitalite: 200), talents: new Dictionary<string, int> { [TalentAlpha] = 1, [TalentGamma] = 1 },
                    passives: new[] { Passif, PassifB }),
                Assailant(1000, 1000, attacks: Blow(fragility: true)),
            });
        var a = Lot81CombatFixtures.Mods(
            (TalentAlpha, Mod("guard.damageReduction", "0.05")), (TalentAlpha, Mod("damageTaken", "-0.1", null, ModifierCondition.WhileGuarding)),
            (TalentAlpha, Mod("status.fragility.durationTaken", "1000")), (TalentAlpha, Mod("skill.essai.frappe.coefficient", "0.5")),
            (TalentAlpha, Mod("damageDealt", "0.2", null, ModifierCondition.PerResonanceStep)));
        var b = Lot81CombatFixtures.Mods(
            (PassifB, Mod("damageDealt", "0.2", null, ModifierCondition.PerResonanceStep)), (Passif, Mod("skill.essai.frappe.coefficient", "0.5")),
            (TalentGamma, Mod("status.fragility.durationTaken", "1000")), (PassifB, Mod("damageTaken", "-0.1", null, ModifierCondition.WhileGuarding)),
            (Passif, Mod("guard.damageReduction", "0.05")));
        var c = Lot81CombatFixtures.Mods(
            (TalentAlpha, Mod("guard.damageReduction", "0.08")), (Passif, Mod("guard.damageReduction", "-0.03")),
            (TalentGamma, Mod("damageTaken", "-0.04", null, ModifierCondition.WhileGuarding)), (PassifB, Mod("damageTaken", "-0.06", null, ModifierCondition.WhileGuarding)),
            (Passif, Mod("status.fragility.durationTaken", "3000")), (PassifB, Mod("status.fragility.durationTaken", "-2000")),
            (TalentGamma, Mod("skill.essai.frappe.coefficient", "0.25")), (TalentAlpha, Mod("skill.essai.frappe.coefficient", "0.25")),
            (Passif, Mod("damageDealt", "0.15", null, ModifierCondition.PerResonanceStep)), (TalentGamma, Mod("damageDealt", "0.05", null, ModifierCondition.PerResonanceStep)));
        CombatSetup reference = Make(a);
        Assert.Empty(reference.Validate());
        var (_, referenceLog) = Run(reference);
        string log = referenceLog.ToString();

        // Garde-fou : les modificateurs sont bien actifs (règles effectives agrégées sur la ligne du personnage).
        var rules = Events(referenceLog).OfKind("combatant").Single(e => e.Str("combatant") == "bastion").GetProperty("rules");
        Assert.Equal("0.65", rules.GetProperty("guard.damageReduction").GetRawText());   // 0,6 + 0,05
        Assert.Equal(2, rules.GetProperty("conditional").GetArrayLength());           // damageDealt par palier, damageTaken en garde
        foreach (var mods in new[] { b, c })
        {
            CombatSetup other = Make(mods);
            Assert.Empty(other.Validate());
            Assert.Equal(reference.ComputeQueueBound().Total, other.ComputeQueueBound().Total);
            Assert.Equal(log, Run(other).Log.ToString());
        }
    }
}
