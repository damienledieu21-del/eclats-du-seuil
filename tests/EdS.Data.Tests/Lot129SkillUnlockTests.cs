using System.Text.Json.Nodes;
using EdS.Data.Characters;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Validation;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.Simulation.Engine;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 12.9b (essai du 25 octobre 2026, choix de Damien : « La 2 pour l'instant et la 3 après ») : le personnage gagne des
/// compétences en montant de niveau, équipées d'elles-mêmes à la halte qui suit, aux niveaux où les règles ouvrent leurs emplacements
/// (Barrière d'acier au niveau 5, Onde de rupture et l'ultime Bastion absolu au niveau 10). Progression en schéma 2.
/// </summary>
[Trait("Category", "World")]
public class Lot129SkillUnlockTests : IDisposable
{
    private const string Barriere = "skill.bastion.barriere_d_acier", Onde = "skill.bastion.onde_de_rupture", Absolu = "skill.bastion.bastion_absolu",
        Provocation = "skill.bastion.provocation", Contrecoup = "skill.bastion.contrecoup";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_competences_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private static string ProgressionPath => ProgressionJsonReader.PathIn(DataDirectory);
    private static CharacterProgression Shipped => ProgressionJsonReader.ReadFile(ProgressionPath, ShippedData).Progression!;
    private static ProgressionRules Rules => ShippedData.Rules.Progression;
    private static ItemCatalog Items => ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!;

    private static ProgressionLoadResult Mutated(Action<JsonArray> mutate)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ProgressionPath))!.AsObject();
        mutate(root["data"]!["classes"]![0]!["skillUnlocks"]!.AsArray());
        return ProgressionJsonReader.Read(root.ToJsonString(), ShippedData);
    }

    private static JsonObject Unlock(int level, string skill) => new() { ["level"] = level, ["skill"] = skill };

    private static (string Path, ValidationCode Code) Error(ProgressionLoadResult result)
    {
        Assert.False(result.IsValid);
        return (result.Report.Errors[0].Path, result.Report.Errors[0].Code);
    }

    private static SavedCharacter Hero(long xp) => PlayerCharacters.Create("bastion", "class.bastion", Shipped).WithXp(xp);

    [Fact]
    public void Competences_gagnees_livrees()
    {
        ClassProgression bastion = Shipped.ForClass("class.bastion");
        // Réponse de Damien du 30 octobre 2026 : Provocation au niveau 3 et Contrecoup au niveau 7, dans la réserve (aucune place ne s'ouvre à
        // leur niveau) ; les autres viennent avec leur place.
        Assert.Equal(new[] { (3, Provocation, SkillKind.Active, false), (5, Barriere, SkillKind.Active, true), (7, Contrecoup, SkillKind.Active, false),
                (10, Onde, SkillKind.Active, true), (10, Absolu, SkillKind.Ultimate, true) },
            bastion.SkillUnlocks.Select(u => (u.Level, u.Skill, u.Kind, u.WithPlace)));
        // Les niveaux suivent les emplacements des règles (lot 4) : 2 actives au niveau 1, 3 au 5, 4 et l'ultime au 10.
        Assert.Equal(new[] { 1, 1, 5, 10 }, Rules.ActiveSlotLevels);
        Assert.Equal(10, Rules.UltimateUnlockLevel);
    }

    [Theory]
    [InlineData(0L, 0)]       // niveau 1 : build de départ
    [InlineData(220L, 0)]     // niveau 3 : Provocation, dans la réserve
    [InlineData(360L, 0)]     // niveau 4
    [InlineData(520L, 1)]     // niveau 5 : Barrière d'acier
    [InlineData(900L, 1)]     // niveau 7 : Contrecoup, dans la réserve
    [InlineData(1360L, 1)]    // niveau 9
    [InlineData(1620L, 3)]    // niveau 10 : Onde de rupture et Bastion absolu
    public void Le_niveau_fait_gagner_ses_competences(long xp, int count)
    {
        SavedCharacter hero = Hero(xp);
        SavedCharacter learned = PlayerCharacters.Learn(hero, Shipped, Rules, out IReadOnlyList<SkillUnlock> gained);
        Assert.Equal(count, gained.Count);
        var expected = new List<string> { "skill.bastion.frappe_du_rempart", "skill.bastion.heurt_de_bouclier" };
        if (count >= 1) expected.Add(Barriere);
        if (count >= 2) expected.Add(Onde);
        Assert.Equal(expected, learned.ActiveSkills);
        Assert.Equal(count == 3 ? Absolu : null, learned.Ultimate);
        Assert.Equal(new[] { "skill.bastion.endurance_du_roc" }, learned.PassiveSkills);
        if (count == 0) Assert.Same(hero, learned);
        // Une seconde fois : rien de plus (le personnage les a déjà).
        Assert.Same(learned, PlayerCharacters.Learn(learned, Shipped, Rules, out IReadOnlyList<SkillUnlock> again));
        Assert.Empty(again);
        // Le build obtenu est valide à son niveau (entrée en zone possible).
        PlayerCharacters.Setup(learned, Shipped, ShippedData, new CombatantPlacement(GroundVector.Parse("0", "0"), Fixed.Parse("0.5")));
    }

    /// <summary>
    /// Un emplacement déjà pris : rien n'est remplacé. Avec le registre du monde (la forme qu'utilise la partie), la compétence va dans la
    /// réserve pour de bon : elle est donnée, la halte ne la placera plus (relecture indépendante de la PR #74). La forme sans registre
    /// (<see cref="PlayerCharacters.Learn(SavedCharacter, CharacterProgression, ProgressionRules, out IReadOnlyList{SkillUnlock})"/>) ne
    /// se souvient de rien : elle la placerait dès qu'une place se libère.
    /// </summary>
    [Fact]
    public void Un_emplacement_deja_pris_met_la_competence_dans_la_reserve()
    {
        // Personnage qui a choisi lui-même sa troisième active : la Barrière ne remplace rien.
        var chosen = new SavedCharacter("bastion", "class.bastion", 520, null,
            new[] { "skill.bastion.frappe_du_rempart", "skill.bastion.heurt_de_bouclier", "skill.bastion.provocation" }, null,
            new[] { "skill.bastion.endurance_du_roc" });
        var registry = new HashSet<string>(StringComparer.Ordinal);
        Assert.Same(chosen, PlayerCharacters.Learn(chosen, Shipped, Rules, registry.Contains, out IReadOnlyList<SkillUnlock> none,
            out IReadOnlyList<SkillUnlock> handed));
        Assert.Empty(none);
        Assert.Equal(new[] { Barriere }, handed.Select(u => u.Skill));   // donnée : en réserve
        foreach (SkillUnlock u in handed) registry.Add(u.GivenKey);
        // Au niveau 10, avec le registre : la Barrière reste dans la réserve, l'Onde prend la quatrième place, l'ultime arrive.
        SavedCharacter tenGiven = PlayerCharacters.Learn(chosen.WithXp(1620), Shipped, Rules, registry.Contains, out IReadOnlyList<SkillUnlock> placed, out _);
        Assert.Equal(new[] { Onde, Absolu }, placed.Select(u => u.Skill));
        Assert.Equal(new[] { "skill.bastion.frappe_du_rempart", "skill.bastion.heurt_de_bouclier", "skill.bastion.provocation", Onde }, tenGiven.ActiveSkills);
        // Sans registre (la fonction seule) :
        Assert.Same(chosen, PlayerCharacters.Learn(chosen, Shipped, Rules, out none));
        Assert.Empty(none);
        // Déjà équipée ailleurs : pas en double.
        var already = new SavedCharacter("bastion", "class.bastion", 520, null, new[] { Barriere, "skill.bastion.heurt_de_bouclier" }, null,
            new[] { "skill.bastion.endurance_du_roc" });
        Assert.Same(already, PlayerCharacters.Learn(already, Shipped, Rules, out none));
        Assert.Empty(none);
        // Sans registre, au niveau 10, dans l'ordre de la progression : la Barrière prend la quatrième place, l'Onde ne trouve plus de place,
        // l'ultime arrive ; Contrecoup (réserve) n'est jamais placé d'office.
        SavedCharacter ten = PlayerCharacters.Learn(chosen.WithXp(1620), Shipped, Rules, out IReadOnlyList<SkillUnlock> gained);
        Assert.Equal(new[] { Barriere, Absolu }, gained.Select(u => u.Skill));
        Assert.Equal(new[] { "skill.bastion.frappe_du_rempart", "skill.bastion.heurt_de_bouclier", "skill.bastion.provocation", Barriere }, ten.ActiveSkills);
        Assert.Equal(Absolu, ten.Ultimate);
    }

    [Fact]
    public void Competences_gagnees_invalides_refusees_avec_leur_chemin()
    {
        const string at = "$.data.classes[0].skillUnlocks";
        Assert.Equal((at + "[0].level", ValidationCode.OutOfRange), Error(Mutated(u => u[0]!["level"] = 1)));
        Assert.Equal((at + "[4].level", ValidationCode.OutOfRange), Error(Mutated(u => u[4]!["level"] = 11)));
        Assert.Equal((at + "[0].skill", ValidationCode.UnknownReference), Error(Mutated(u => u[0]!["skill"] = "skill.bastion.inconnue")));
        Assert.Equal((at + "[0].skill", ValidationCode.DuplicateId), Error(Mutated(u => u[0]!["skill"] = "skill.bastion.frappe_du_rempart")));
        Assert.Equal((at + "[1].skill", ValidationCode.DuplicateId), Error(Mutated(u => u[1]!["skill"] = Provocation)));
        Assert.Equal((at + "[1].level", ValidationCode.InvalidValue), Error(Mutated(u => u[1]!["level"] = 2)));   // ordre des niveaux
        Assert.Equal((at + "[4].level", ValidationCode.InvalidValue), Error(Mutated(u => { for (int i = 1; i <= 4; i++) u[i]!["level"] = 5; })));   // l'ultime demande le niveau 10
        // Contrecoup au niveau 5 : il viendrait avec la troisième place, déjà prise par la Barrière (une seule place s'ouvre).
        Assert.Equal((at, ValidationCode.InvalidValue), Error(Mutated(u => u[2]!["level"] = 5)));
        // Contrecoup au niveau 10 : une cinquième active avec sa place, pas d'emplacement.
        Assert.Equal((at, ValidationCode.InvalidValue), Error(Mutated(u => u[2]!["level"] = 10)));
        // Barrière au niveau 4 : aucune place ne s'ouvre, elle va dans la réserve ; admise (rien à faire tenir dans la barre).
        ProgressionLoadResult reserve = Mutated(u => u[1]!["level"] = 4);
        Assert.True(reserve.IsValid, reserve.Report.ToString());
        Assert.False(reserve.Progression!.ForClass("class.bastion").SkillUnlocks[1].WithPlace);
        Assert.Equal((at + "[1].level", ValidationCode.MissingField), Error(Mutated(u => u[1]!.AsObject().Remove("level"))));
        Assert.Equal((at + "[1].skill", ValidationCode.MissingField), Error(Mutated(u => u[1]!.AsObject().Remove("skill"))));
        Assert.Equal((at + "[1]", ValidationCode.WrongType), Error(Mutated(u => u[1] = null)));
        Assert.Equal(at + "[0].bonus", Error(Mutated(u => u[0]!["bonus"] = 1)).Path);
        Assert.Equal(at + "[0].level", Error(Mutated(u => u[0]!["level"] = "cinq")).Path);
        Assert.Equal(at, Error(Mutated(u => { for (int i = 0; i < 60; i++) u.Add(Unlock(10, "skill.bastion.provocation")); })).Path);   // plus de 64
        // Liste absente : refusée ; vide : admise (aucune compétence gagnée).
        JsonObject root = JsonNode.Parse(File.ReadAllText(ProgressionPath))!.AsObject();
        root["data"]!["classes"]![0]!.AsObject().Remove("skillUnlocks");
        Assert.Equal((at, ValidationCode.MissingField), Error(ProgressionJsonReader.Read(root.ToJsonString(), ShippedData)));
        ProgressionLoadResult empty = Mutated(u => u.Clear());
        Assert.True(empty.IsValid, empty.Report.ToString());
        Assert.Empty(empty.Progression!.ForClass("class.bastion").SkillUnlocks);
    }

    [Fact]
    public void La_session_equipe_les_competences_et_les_sauvegarde()
    {
        var save = new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Hero(520));
        var zone = ZoneJsonReader.ReadFile(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons"), Path.Combine(AppContext.BaseDirectory, "scenarios"),
            ShippedData, Items).Zone!;
        var first = new WorldSession(save, zone, ShippedData, Shipped, Items);
        Assert.Equal(new[] { Barriere }, first.Learned.Select(u => u.Skill));
        Assert.Contains(Barriere, first.Character.ActiveSkills);
        WorldSave saved = first.Save();
        Assert.Equal(3, saved.Character!.ActiveSkills.Count);
        // Relue : la sauvegarde garde la compétence, la session suivante n'a plus rien à gagner.
        WorldSave reread = SaveJsonReader.Read(SaveJsonReader.Write(saved)).Save!;
        var second = new WorldSession(reread, zone, ShippedData, Shipped, Items);
        Assert.Empty(second.Learned);
        Assert.Equal(saved.Character.ActiveSkills, second.Character.ActiveSkills);
    }

    [Fact]
    public void L_aventure_annonce_le_niveau_puis_la_competence_a_la_halte()
    {
        // Un loup affaibli (1 PV, donc 1 point d'expérience) près de l'entrée ; il manque 1 point pour le niveau 5.
        string scenarios = Path.Combine(_folder, "scenarios");
        Directory.CreateDirectory(Path.Combine(scenarios, "zones"));
        JsonObject vallons = JsonNode.Parse(File.ReadAllText(ScenarioPath("zones/vallons")))!.AsObject();
        foreach (JsonNode? c in vallons["data"]!["combatants"]!.AsArray())
            if ((string)c!["id"]! == "loup1")
            {
                c["position"] = new JsonArray(-15, -12);
                c["health"] = 1;
            }
        File.WriteAllText(Path.Combine(scenarios, "zones", "vallons.json"), vallons.ToJsonString());
        File.Copy(ScenarioPath("zones/vallons_chef"), Path.Combine(scenarios, "zones", "vallons_chef.json"));
        CopyMissingZoneScenarios(scenarios);   // intérieurs : reliés aux Vallons par leurs portes
        string savePath = Path.Combine(_folder, "partie", "sauvegarde.json");
        Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
        SaveStore.Write(savePath, new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(),
            PlayerCharacters.Create(AdventureSession.PlayerId, AdventureSession.PlayerClass, Shipped).WithXp(519)));
        AdventureSession a = AdventureSession.Load(DataDirectory, scenarios, savePath);
        Assert.Equal(new[] { "Partie reprise." }, a.DrainMessages());
        Assert.Empty(a.World.Learned);
        CombatEngine e = a.World.Zone.Engine;
        for (int i = 0; i < 400 && !e.View("loup1").IsDead; i++)
        {
            long now = e.CurrentTick * 50;
            CombatantView me = e.View(AdventureSession.PlayerId), wolf = e.View("loup1");
            if (me.Position is GroundVector p && wolf.Position is GroundVector w && wolf.IsPresent)
            {
                double dx = (w.X.Raw - p.X.Raw) / 1e4, dz = (w.Z.Raw - p.Z.Raw) / 1e4;
                a.Combat.SetMove(Math.Sqrt(dx * dx + dz * dz) > 2 ? dx : 0, Math.Sqrt(dx * dx + dz * dz) > 2 ? dz : 0);
                try { e.Submit(new CombatCommand(now, AdventureSession.PlayerId, "skill.bastion.frappe_du_rempart", "loup1")); }
                catch (ArgumentException) { }
            }
            a.Advance(0.05);
        }
        Assert.True(e.View("loup1").IsDead);
        a.Combat.SetMove(0, 0);
        IReadOnlyList<string> messages = a.DrainMessages();
        Assert.Contains("Niveau 5 ! Il prendra effet à ta prochaine halte.", messages);
        Assert.Contains("À ta prochaine halte, nouvelle compétence : Barrière d'acier.", messages);
        Assert.Equal(2, a.World.Character.ActiveSkills.Count);   // pas avant la halte
        // Retour au point sûr d'entrée, halte : la compétence est équipée sur la touche 3, et sauvegardée.
        for (int i = 0; i < 400 && !a.CanRest; i++)
        {
            GroundVector p = e.View(AdventureSession.PlayerId).Position!.Value;
            a.Combat.SetMove(-18 - p.X.Raw / 1e4, -12 - p.Z.Raw / 1e4);
            a.Advance(0.05);
        }
        a.Combat.SetMove(0, 0);
        Assert.True(a.Rest());
        Assert.Contains("Nouvelle compétence : Barrière d'acier (touche 3).", a.DrainMessages());
        Assert.Equal(Barriere, a.World.Character.ActiveSkills[2]);
        Assert.Contains(Barriere, a.World.Zone.Engine.View(AdventureSession.PlayerId).Character!.Build.ActiveSkills);   // au combat
        a.Save();
        Assert.Contains(Barriere, SaveStore.Read(savePath).Save!.Character!.ActiveSkills);
    }
}
