using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Simulation.Engine;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 8.5 : talents et passifs livrés, au combat, avec les données livrées (data/v0) et des scénarios écrits ici. Les
/// modificateurs sont vérifiés par les règles effectives de la ligne <c>combatant</c> (le moteur les applique depuis le sous-lot 8.1) ;
/// les déclencheurs, par leurs effets dans le journal : Écho défensif, Égide réactive, Riposte précise, Jugement du colosse, Écho de
/// guerre, Lien protecteur, Serment du gardien, Sentinelle ; Rempart éternel, Présence et Rempart vivant par leurs conséquences.
/// Attributs communs des builds de comparaison (niveau 30) : 400 PV, 140 d'endurance, puissance physique 60.
/// </summary>
[Trait("Category", "Bastion")]
[Trait("Category", "Data")]
public class Lot85TalentCombatTests
{
    private static string R(string t) => $"talent.bastion.rempart.{t}";
    private static string C(string t) => $"talent.bastion.contrecoup.{t}";
    private static string G(string t) => $"talent.bastion.gardien.{t}";
    private static string S(string s) => $"skill.bastion.{s}";

    private static JsonObject Hero(string id = "bastion", Dictionary<string, int>? talents = null, string[]? passives = null, string[]? actives = null,
        int level = 30, int endurance = 20)
    {
        var talentObject = new JsonObject();
        foreach (var (talent, rank) in talents ?? new()) talentObject[talent] = rank;
        return new JsonObject
        {
            ["id"] = id, ["type"] = "character", ["team"] = "joueurs", ["initiative"] = 0, ["class"] = "class.bastion",
            ["build"] = new JsonObject
            {
                ["level"] = level,
                ["attributes"] = new JsonObject { ["force"] = 30, ["dexterite"] = 10, ["intelligence"] = 6, ["vitalite"] = 30, ["volonte"] = 15, ["endurance"] = endurance },
                ["talents"] = talentObject,
                ["activeSkills"] = new JsonArray((actives ?? new[] { "frappe_du_rempart", "provocation", "barriere_d_acier" }).Select(a => (JsonNode?)S(a)).ToArray()),
                ["ultimate"] = null,
                ["passiveSkills"] = new JsonArray((passives ?? Array.Empty<string>()).Select(p => (JsonNode?)S(p)).ToArray()),
            },
        };
    }

    private static JsonObject Assailant(string target, long firstMs = 1000, long intervalMs = 1000, string id = "assaillant", long raw = 100) => new()
    {
        ["id"] = id, ["type"] = "assailant", ["team"] = "cibles", ["initiative"] = 0, ["armor"] = 0, ["magicResistance"] = 0, ["target"] = target,
        ["firstAttackMs"] = firstMs, ["intervalMs"] = intervalMs,
        ["attacks"] = new JsonArray(new JsonObject
        {
            ["id"] = "coup", ["rawDamage"] = raw, ["damageType"] = "physique", ["targeting"] = "enemy", ["unblockable"] = false, ["applies"] = new JsonArray(),
        }),
    };

    private static JsonObject Dummy(string id = "mannequin") => new()
    {
        ["id"] = id, ["type"] = "dummy", ["team"] = "cibles", ["initiative"] = 0, ["armor"] = 0, ["magicResistance"] = 0,
    };

    private static JsonObject Act(long atMs, string action, string actor = "bastion") => new() { ["atMs"] = atMs, ["actor"] = actor, ["action"] = action };

    private static JsonObject Cast(long atMs, string skill, string? target, string actor = "bastion") =>
        new() { ["atMs"] = atMs, ["actor"] = actor, ["skill"] = S(skill), ["target"] = target };

    private static List<JsonElement> Run(IEnumerable<JsonObject> combatants, IEnumerable<JsonObject> commands, long durationMs)
    {
        var root = new JsonObject
        {
            ["schemaVersion"] = 7, ["kind"] = "scenario", ["contentVersion"] = "0.1.0-test",
            ["data"] = new JsonObject
            {
                ["seed"] = 1, ["durationMs"] = durationMs,
                ["combatants"] = new JsonArray(combatants.Select(c => (JsonNode?)c).ToArray()),
                ["commands"] = new JsonArray(commands.Select(c => (JsonNode?)c).ToArray()),
            },
        };
        ScenarioLoadResult loaded = ScenarioJsonReader.Read(root.ToJsonString(), ShippedData);
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        var log = new MemoryCombatLog();
        CombatEngine.Run(loaded.Setup!, log);
        return log.Lines.Skip(1).Select(l => JsonDocument.Parse(l).RootElement.Clone()).ToList();
    }

    private static IEnumerable<JsonElement> Of(List<JsonElement> events, string kind) => events.Where(e => e.GetProperty("event").GetString() == kind);
    private static string? Str(JsonElement e, string name) => e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    private static long Num(JsonElement e, string name) => e.GetProperty(name).GetInt64();
    private static long Tick(JsonElement e) => Num(e, "tick");
    private static IEnumerable<JsonElement> By(List<JsonElement> events, string kind, string trigger) =>
        Of(events, kind).Where(e => Str(e, "trigger")?.StartsWith(trigger + "/", StringComparison.Ordinal) == true);

    private static JsonElement Combatant(List<JsonElement> events, string id = "bastion") => Of(events, "combatant").Single(c => Str(c, "combatant") == id);
    private static JsonElement Rules(List<JsonElement> events) => Combatant(events).GetProperty("rules");

    // --- Modificateurs : règles et statistiques effectives -----------------------------------------------------------------------

    [Fact]
    public void Rempart_regles_effectives_des_talents_et_passifs()
    {
        // Garde stable 3, Écho défensif, Peau de fer 5, Souffle du roc 5, Poids du rempart 4, Rempart éternel ; Vigilance 5 ;
        // Endurance du roc, Bouclier lourd, Réserve de Charges, Posture de fer.
        var events = Run(new[] { Hero(talents: new() { [R("garde_stable")] = 3, [R("echo_defensif")] = 1, [R("peau_de_fer")] = 5, [R("souffle_du_roc")] = 5,
                [R("poids_du_rempart")] = 4, [R("rempart_eternel")] = 1, [G("egide_reactive")] = 3, [G("vigilance")] = 5 },
                passives: new[] { "endurance_du_roc", "bouclier_lourd", "reserve_de_charges", "posture_de_fer" }), Dummy() },
            Array.Empty<JsonObject>(), 100);
        JsonElement rules = Rules(events);
        Assert.Equal("0.72", rules.GetProperty("guard.damageReduction").GetRawText());
        Assert.Equal("1.25", rules.GetProperty("guard.upkeepPerSecond").GetRawText());
        Assert.Equal((7, true, "0.4"), (rules.GetProperty("guard.costPerBlock").GetInt32(), rules.GetProperty("guard.noUpkeepBreak").GetBoolean(),
            rules.GetProperty("guard.reductionAtZeroEndurance").GetRawText()));
        Assert.Equal((6, "15"), (rules.GetProperty("rampartCharges.max").GetInt32(), rules.GetProperty("endurance.regenPerSecond").GetRawText()));
        Assert.Equal("-0.075", rules.GetProperty("damageTaken").GetRawText());
        Assert.Equal("""[{"key":"damageTaken","conditions":["whileGuarding"],"value":-0.05},{"key":"damageTaken","conditions":["whileGuarding","perRampartCharge"],"value":-0.02}]""",
            rules.GetProperty("conditional").GetRawText());
        JsonElement bastion = Combatant(events);
        Assert.Equal((440L, 175L), (Num(bastion, "maxHealth"), Num(bastion, "maxEndurance")));   // 400 × 1,1 ; 140 + 20 + 15
    }

    [Fact]
    public void Contrecoup_regles_effectives_des_talents_et_passifs()
    {
        // Riposte précise 2, Fureur contenue 2, Main lourde 5, Élan 5 ; Œil du duelliste, Ferveur, Ténacité.
        var events = Run(new[] { Hero(talents: new() { [C("riposte_precise")] = 2, [C("fureur_contenue")] = 2, [C("main_lourde")] = 5, [C("elan")] = 5 },
                passives: new[] { "oeil_du_duelliste", "ferveur", "tenacite" }), Dummy() }, Array.Empty<JsonObject>(), 100);
        JsonElement rules = Rules(events);
        Assert.Equal((200, 250, 4), (rules.GetProperty("parry.windowMs").GetInt32(), rules.GetProperty("parry.failureRecoveryMs").GetInt32(),
            rules.GetProperty("resonance.perDirectHit").GetInt32()));
        Assert.Equal("0.1", rules.GetProperty("damageDealt").GetRawText());
        Assert.Equal("""[{"key":"damageDealt","conditions":["perResonanceStep"],"value":0.02}]""", rules.GetProperty("conditional").GetRawText());
        Assert.Equal(130L, Num(Combatant(events), "statusResistance"));   // Volonté 15 × 2 + 100
    }

    [Fact]
    public void Gardien_regles_effectives_des_talents()
    {
        // Égide réactive 3, Lien protecteur, Vigilance 5, Présence 5, Rempart vivant 4, Serment du gardien.
        var events = Run(new[] { Hero(talents: new() { [G("egide_reactive")] = 3, [G("lien_protecteur")] = 1, [G("vigilance")] = 5, [G("presence")] = 5,
                [G("rempart_vivant")] = 4, [G("serment_du_gardien")] = 1 }), Dummy() }, Array.Empty<JsonObject>(), 100);
        JsonElement rules = Rules(events);
        Assert.Equal(("0.15", true, "0.16"), (rules.GetProperty("protection.redirectRatio").GetRawText(), rules.GetProperty("protection.redirectThroughGuard").GetBoolean(),
            rules.GetProperty("shield.potencyDealt").GetRawText()));
        // Présence : les provocations de Provocation et de Serment (+1 s au rang 5), pas celle de l'ultime (décision 6 : 8 s pour ses trois effets).
        Assert.Equal((1000, 1000), (rules.GetProperty("status.taunt.durationDealt").GetInt32(), rules.GetProperty("status.oathTaunt.durationDealt").GetInt32()));
        Assert.False(rules.TryGetProperty("status.bastionAbsoluteTaunt.durationDealt", out _));
        Assert.Equal(440L, Num(Combatant(events), "maxHealth"));
    }

    [Fact]
    public void Sans_talent_ni_passif_les_regles_de_base()
    {
        JsonElement rules = Rules(Run(new[] { Hero(), Dummy() }, Array.Empty<JsonObject>(), 100));
        Assert.Equal(("0.6", "2", 10, "0"), (rules.GetProperty("guard.damageReduction").GetRawText(), rules.GetProperty("guard.upkeepPerSecond").GetRawText(),
            rules.GetProperty("guard.costPerBlock").GetInt32(), rules.GetProperty("damageTaken").GetRawText()));
        Assert.Equal("[]", rules.GetProperty("conditional").GetRawText());
    }

    // --- Rempart ----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Echo_defensif_rend_4_endurance_par_blocage_et_une_Charge_au_plus_toutes_les_3_s()
    {
        // Garde dès 0 ; impacts bloqués aux ticks 20, 40, 60, 80 et 100.
        var events = Run(new[] { Hero(talents: new() { [R("garde_stable")] = 3, [R("echo_defensif")] = 1 }), Assailant("bastion") },
            new[] { Act(0, "guardStart") }, 5500);
        Assert.Equal(5, Of(events, "blocked").Count());
        JsonElement[] endurance = By(events, "resourceGained", R("echo_defensif")).Where(g => Str(g, "resource") == "endurance").ToArray();
        Assert.Equal(new[] { 20L, 40L, 60L, 80L, 100L }, endurance.Select(Tick));
        Assert.All(endurance, g => Assert.Equal((4L, "trigger"), (Num(g, "amount"), Str(g, "source"))));
        Assert.Equal(new[] { 20L, 80L }, By(events, "resourceGained", R("echo_defensif")).Where(g => Str(g, "resource") == "rampartCharges").Select(Tick));
    }

    [Fact]
    public void Rempart_eternel_la_garde_ne_se_brise_plus_et_tient_a_40_pourcent_sans_endurance()
    {
        // 18 points de Rempart et le majeur ; un impact de 10 toutes les 100 ms : l'endurance (120) s'épuise, la garde tient.
        var events = Run(new[] { Hero(endurance: 0, talents: new() { [R("garde_stable")] = 3, [R("echo_defensif")] = 1, [R("peau_de_fer")] = 5,
                [R("souffle_du_roc")] = 5, [R("poids_du_rempart")] = 4, [R("rempart_eternel")] = 1 }), Assailant("bastion", firstMs: 100, intervalMs: 100, raw: 10) },
            new[] { Act(0, "guardStart") }, 6000);
        Assert.Empty(Of(events, "guardBroken"));
        Assert.Empty(Of(events, "died"));
        Assert.Equal(59, Of(events, "blocked").Count());
        Assert.Contains(Of(events, "blocked"), b => Num(b, "endurance") == 0);
        // Blocage normal (10 d'endurance) : réduction 72 %, au plus 3 dégâts ; blocage affaibli (endurance insuffisante) : réduction 40 %,
        // 5 ou 6 dégâts selon les Charges (Peau de fer, Poids du rempart).
        var pairs = Of(events, "blocked").Zip(Of(events, "damage"), (b, d) => (Cost: Num(b, "enduranceCost"), Amount: Num(d, "amount"))).ToList();
        Assert.All(pairs.Where(p => p.Cost == 10), p => Assert.InRange(p.Amount, 1, 3));
        Assert.Contains(pairs, p => p.Cost < 10);
        Assert.All(pairs.Where(p => p.Cost < 10), p => Assert.InRange(p.Amount, 5, 6));
    }

    // --- Contrecoup -------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Riposte_precise_frappe_l_attaquant_sans_critique_ni_Resonance_d_impact()
    {
        // Rang 3 : 0,5 + 0,25 × 3 = 1,25 × 60 = 75.
        var events = Run(new[] { Hero(talents: new() { [C("riposte_precise")] = 3 }), Assailant("bastion") }, new[] { Act(950, "parry") }, 1500);
        JsonElement strike = Assert.Single(By(events, "damage", C("riposte_precise")));
        Assert.Equal(("bastion", "assaillant", 75L, false, 20L), (Str(strike, "source"), Str(strike, "target"), Num(strike, "amount"),
            strike.GetProperty("critical").GetBoolean(), Tick(strike)));
        // Seule la Résonance de la parade parfaite (+8) : aucune Résonance d'impact direct pour la frappe de réaction (L8-D1).
        Assert.Equal(new[] { 8L }, Of(events, "resourceGained").Where(g => Str(g, "resource") == "resonance").Select(g => Num(g, "amount")));
    }

    [Fact]
    public void Jugement_du_colosse_frappe_tous_les_ennemis_et_donne_10_Resonance_toutes_les_3_s()
    {
        // Contrecoup au majeur (Écho de guerre 5 compris) ; parades parfaites aux ticks 20, 40, 60 et 80.
        var talents = new Dictionary<string, int> { [C("riposte_precise")] = 3, [C("fureur_contenue")] = 2, [C("main_lourde")] = 5, [C("echo_de_guerre")] = 5,
            [C("elan")] = 3, [C("jugement_du_colosse")] = 1 };
        var events = Run(new[] { Hero(talents: talents), Assailant("bastion"), Dummy() },
            Enumerable.Range(0, 4).Select(i => Act(950 + 1000L * i, "parry")), 4500);
        Assert.Equal(4, Of(events, "parrySucceeded").Count());
        JsonElement[] area = By(events, "damage", C("jugement_du_colosse")).ToArray();
        Assert.Equal(new[] { (20L, "assaillant"), (20L, "mannequin"), (80L, "assaillant"), (80L, "mannequin") }, area.Select(d => (Tick(d), Str(d, "target")!)));
        Assert.Equal(new[] { 20L, 80L }, By(events, "resourceGained", C("jugement_du_colosse")).Select(Tick));
        // Écho de guerre 5 : +10 Résonance à chaque parade parfaite ; Riposte précise à chacune aussi.
        Assert.Equal(4, By(events, "resourceGained", C("echo_de_guerre")).Count(g => Num(g, "amount") == 10));
        Assert.Equal(4, By(events, "damage", C("riposte_precise")).Count());
    }

    // --- Gardien ----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Egide_reactive_bouclier_au_blocage_ou_a_la_parade_au_plus_toutes_les_2_s()
    {
        // Rang 3 : 9 % de 400 PV = 36, 4 s. Parade parfaite au tick 20, puis blocages aux ticks 40, 60, 80 et 100.
        var events = Run(new[] { Hero(talents: new() { [G("egide_reactive")] = 3 }), Assailant("bastion") }, new[] { Act(950, "parry"), Act(1050, "guardStart") }, 5500);
        JsonElement[] shields = By(events, "stateApplied", G("egide_reactive")).ToArray();
        Assert.Equal(new[] { 20L, 60L, 100L }, shields.Select(Tick));
        Assert.All(shields, s => Assert.Equal(("bastion", "reactiveAegis", "bastion", 36L), (Str(s, "combatant"), Str(s, "state"), Str(s, "source"), Num(s, "absorb"))));
        Assert.Equal(100L, Num(shields[0], "untilTick"));   // 4 s
    }

    [Fact]
    public void Lien_protecteur_la_Barriere_sur_un_allie_le_protege_25_pourcent()
    {
        JsonObject hero = Hero(talents: new() { [G("egide_reactive")] = 3, [G("lien_protecteur")] = 1 });
        var events = Run(new[] { hero, Hero("allie"), Assailant("allie") }, new[] { Cast(0, "barriere_d_acier", "allie") }, 1500);
        JsonElement link = Assert.Single(By(events, "stateApplied", G("lien_protecteur")));
        Assert.Equal(("allie", "protectiveLink", "bastion"), (Str(link, "combatant"), Str(link, "state"), Str(link, "source")));
        JsonElement transfer = Assert.Single(Of(events, "damageTransferred"));
        Assert.Equal(("allie", "bastion", "0.25", 25L), (Str(transfer, "protected"), Str(transfer, "target"), transfer.GetProperty("ratio").GetRawText(), Num(transfer, "amount")));

        // Sur soi : la protection est refusée (notApplicable), le bouclier de la Barrière posé.
        var self = Run(new[] { (JsonObject)hero.DeepClone(), Assailant("bastion") }, new[] { Cast(0, "barriere_d_acier", "bastion") }, 500);
        Assert.Equal("notApplicable", Assert.Single(Of(self, "stateBlocked"), b => Str(b, "state") == "protectiveLink").GetProperty("reason").GetString());
        Assert.Single(Of(self, "stateApplied"), a => Str(a, "state") == "steelBarrier");
    }

    [Fact]
    public void Serment_du_gardien_provoque_l_assaillant_de_l_allie_et_porte_le_transfert_a_40_pourcent_par_la_garde()
    {
        // Gardien au majeur, Présence 4 (+0,8 s) ; Barrière d'acier sur l'allié à 0, garde du Bastion dès 50 ms ; l'assaillant frappe
        // l'allié chaque seconde.
        var talents = new Dictionary<string, int> { [G("egide_reactive")] = 3, [G("lien_protecteur")] = 1, [G("vigilance")] = 5, [G("presence")] = 4,
            [G("rempart_vivant")] = 5, [G("serment_du_gardien")] = 1 };
        var events = Run(new[] { Hero(talents: talents), Hero("allie"), Assailant("allie") },
            new[] { Cast(0, "barriere_d_acier", "allie"), Act(50, "guardStart") }, 6500);
        // Premier impact sur l'allié (tick 20) : 40 % transférés, réduits par la garde du Bastion (60 %).
        JsonElement transfer = Of(events, "damageTransferred").First();
        Assert.Equal(("0.4", "guard", 40L), (transfer.GetProperty("ratio").GetRawText(), Str(transfer, "defense"), Num(transfer, "amount")));
        Assert.Equal(16L, Num(transfer, "taken") + Num(transfer, "absorbed"));
        // Provocation de Serment (2 s + 0,8 s) posée sur l'assaillant au tick 20, puis au plus toutes les 3 s : au tick 80, l'assaillant
        // revenu à l'allié à la fin de la première (tick 76).
        JsonElement[] oaths = By(events, "stateApplied", G("serment_du_gardien")).ToArray();
        Assert.Equal(new[] { 20L, 80L }, oaths.Select(Tick));
        Assert.All(oaths, o => Assert.Equal(("assaillant", "oathTaunt", "bastion", Tick(o) + 56), (Str(o, "combatant"), Str(o, "state"), Str(o, "source"), Num(o, "untilTick"))));
        // L'assaillant frappe le Bastion tant qu'il est provoqué (ticks 40, 60, puis 100, 120), l'allié sinon (ticks 20 et 80).
        Assert.Equal(new[] { "allie", "bastion", "bastion", "allie", "bastion", "bastion" }, Of(events, "attackStarted").Select(a => Str(a, "target")));
    }

    [Fact]
    public void Presence_allonge_la_provocation_de_1_s_au_rang_5()
    {
        var events = Run(new[] { Hero(talents: new() { [G("egide_reactive")] = 3, [G("presence")] = 5 }), Assailant("allie"), Hero("allie") },
            new[] { Cast(0, "provocation", "assaillant") }, 5000);
        JsonElement taunt = Assert.Single(Of(events, "stateApplied"), a => Str(a, "state") == "taunt");
        Assert.Equal(80L, Num(taunt, "untilTick"));   // 3 s + 1 s
    }

    [Fact]
    public void Rempart_vivant_renforce_les_boucliers_poses_par_le_Bastion()
    {
        // Rang 5 : +20 % ; Barrière d'acier sur soi : 400 × 0,15 × 1,2 = 72.
        var events = Run(new[] { Hero(talents: new() { [G("rempart_vivant")] = 5 }), Dummy() }, new[] { Cast(0, "barriere_d_acier", "bastion") }, 100);
        Assert.Equal(72L, Num(Of(events, "stateApplied").Single(a => Str(a, "state") == "steelBarrier"), "absorb"));
    }

    [Fact]
    public void Sentinelle_une_Charge_au_premier_impact_sur_un_allie_puis_6_s_de_recharge()
    {
        // Impacts de 20 sur l'allié chaque seconde (il survit) ; Charges aux ticks 20 et 140.
        var events = Run(new[] { Hero(passives: new[] { "sentinelle" }), Hero("allie"), Assailant("allie", raw: 20) }, Array.Empty<JsonObject>(), 8500);
        JsonElement[] gains = Of(events, "resourceGained").Where(g => Str(g, "trigger")?.StartsWith(S("sentinelle") + "/") == true).ToArray();
        Assert.Equal(new[] { 20L, 140L }, gains.Select(Tick));
        Assert.All(gains, g => Assert.Equal(("bastion", "rampartCharges", 1L), (Str(g, "actor"), Str(g, "resource"), Num(g, "amount"))));
    }
}
