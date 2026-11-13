using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.28, première partie (PLAN_GRANDE_CARTE.md § 3, « reprise d'une partie ») : sauvegarde en version 19 avec le point de reprise
/// (<c>resume</c> : dernier point sûr approché et son secteur) ; une partie relancée reprend à ce point sûr, plus à l'entrée de la zone ;
/// une sauvegarde de version 18 est lue (reprise à l'entrée) et mise à jour.
/// </summary>
[Trait("Category", "World")]
public class V228RepriseTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v228_reprise_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private string SavePath => Path.Combine(_folder, "partie", "sauvegarde.json");

    private static GroundVector Position(AdventureSession a) => a.World.Zone.Engine.View(AdventureSession.PlayerId).Position!.Value;

    private static GroundVector At(double x, double z) => new GroundVector(Fixed.Parse(x.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        Fixed.Parse(z.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    /// <summary>Marche vers (x, z) jusqu'à <paramref name="within"/> mètres, tick par tick ; rend vrai une fois arrivé.</summary>
    private static bool WalkTo(AdventureSession a, double x, double z, double within = 1)
    {
        for (int i = 0; i < 4000; i++)
        {
            GroundVector p = Position(a);
            double dx = x - p.X.Raw / 1e4, dz = z - p.Z.Raw / 1e4;
            if (Math.Sqrt(dx * dx + dz * dz) <= within)
            {
                a.Combat.SetMove(0, 0);
                a.Advance(0.05);
                return true;
            }
            a.Combat.SetMove(dx, dz);
            a.Advance(0.05);
        }
        return false;
    }

    /// <summary>Du village au bourg par la grand-rue (aucun obstacle, aucune créature à portée).</summary>
    private static void WalkToBourg(AdventureSession a)
    {
        Assert.True(WalkTo(a, -20, -2));
        Assert.True(WalkTo(a, -50, -1));
        Assert.True(WalkTo(a, -58, -4));
    }

    /// <summary>La sauvegarde écrite, retouchée (objet <c>data</c>) et réécrite telle quelle.</summary>
    private void EditSave(Action<JsonObject> edit, int? version = null)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(SavePath))!.AsObject();
        if (version is int v) root["schemaVersion"] = v;
        edit(root["data"]!.AsObject());
        File.WriteAllText(SavePath, root.ToJsonString());
    }

    private static string Text(string resume, int version = SaveValidator.CurrentSchemaVersion) =>
        "{\n  \"schemaVersion\": " + version + ",\n  \"kind\": \"save\",\n  \"contentVersion\": \"0.1.0-provisoire\",\n  \"data\": {\n"
        + "    \"zone\": \"zone.vallons\",\n    \"worldTick\": 1200,\n    \"sessions\": 3,\n    \"character\": null,\n" + resume
        + "    \"consumed\": []\n  }\n}\n";

    // --- Format (sauvegarde v19) ------------------------------------------------------------------------------------------

    [Fact]
    public void Version_19_point_de_reprise_ecrit_relu_canonique()
    {
        Assert.Equal((19, 19), (SaveValidator.CurrentSchemaVersion, SaveValidator.ResumeSince));
        var save = new WorldSave("zone.vallons", 1200, 3, new[] { "zone.vallons/safe/place_du_bourg" }, null, false, null, new SavedResume("place_du_bourg", "s07_06"));
        string written = SaveJsonReader.Write(save);
        Assert.Contains(SaveVersion.Current, written, StringComparison.Ordinal);
        Assert.Contains("    \"resume\": {\n      \"safePoint\": \"place_du_bourg\",\n      \"sector\": \"s07_06\"\n    },\n", written, StringComparison.Ordinal);
        SaveLoadResult read = SaveJsonReader.Read(written);
        Assert.True(read.IsValid, read.Report.ToString());
        Assert.False(read.Save!.Migrated);
        Assert.Equal(("place_du_bourg", "s07_06"), (read.Save.Resume!.SafePoint, read.Save.Resume.Sector));
        Assert.Equal(written, SaveJsonReader.Write(read.Save));
        MiniJsonSchema schema = MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "save.schema.json"));
        Assert.True(schema.IsValid(System.Text.Json.JsonDocument.Parse(written).RootElement));
        Assert.False(schema.IsValid(System.Text.Json.JsonDocument.Parse(written.Replace("\"safePoint\": \"place_du_bourg\"", "\"safePoint\": \"Place\"", StringComparison.Ordinal)).RootElement));
        // Sans secteur (zone sans région : un intérieur), et sans point de reprise (rien n'est écrit).
        string indoor = SaveJsonReader.Write(save.WithResume(new SavedResume("coin_du_feu")));
        Assert.Contains("    \"resume\": {\n      \"safePoint\": \"coin_du_feu\"\n    },\n", indoor, StringComparison.Ordinal);
        Assert.Null(SaveJsonReader.Read(indoor).Save!.Resume!.Sector);
        Assert.Equal(indoor, SaveJsonReader.Write(SaveJsonReader.Read(indoor).Save!));
        string none = SaveJsonReader.Write(save.WithResume(null));
        Assert.DoesNotContain("resume", none, StringComparison.Ordinal);
        Assert.Null(SaveJsonReader.Read(none).Save!.Resume);
        // WithResume ne change rien d'autre.
        WorldSave moved = save.WithResume(new SavedResume("place_du_village", "s07_06"));
        Assert.Equal((save.Zone, save.WorldTick, save.Sessions, save.Migrated), (moved.Zone, moved.WorldTick, moved.Sessions, moved.Migrated));
        Assert.Equal(save.Consumed, moved.Consumed);
    }

    [Fact]
    public void Version_18_lue_sans_reprise_puis_mise_a_jour()
    {
        SaveLoadResult old = SaveJsonReader.Read(Text(string.Empty, 18));
        Assert.True(old.IsValid, old.Report.ToString());
        Assert.True(old.Save!.Migrated);
        Assert.Null(old.Save.Resume);
        Assert.Contains(SaveVersion.Current, SaveJsonReader.Write(old.Save), StringComparison.Ordinal);
        // Le champ n'existe pas avant la version 19 : refusé, jamais ignoré en silence.
        SaveLoadResult refused = SaveJsonReader.Read(Text("    \"resume\": { \"safePoint\": \"place_du_bourg\" },\n", 18));
        ValidationError e = Assert.Single(refused.Report.Errors);
        Assert.Equal(("$.data.resume", ValidationCode.UnknownField), (e.Path, e.Code));
        // Une version future n'est pas lue.
        Assert.False(SaveJsonReader.Read(Text(string.Empty, SaveValidator.CurrentSchemaVersion + 1)).IsValid);
    }

    [Theory]
    [InlineData("{ }", "$.data.resume.safePoint", ValidationCode.MissingField)]
    [InlineData("{ \"sector\": \"s07_06\" }", "$.data.resume.safePoint", ValidationCode.MissingField)]
    [InlineData("{ \"safePoint\": \"\" }", "$.data.resume.safePoint", ValidationCode.InvalidValue)]
    [InlineData("{ \"safePoint\": \"Place\" }", "$.data.resume.safePoint", ValidationCode.InvalidValue)]
    [InlineData("{ \"safePoint\": \"1place\" }", "$.data.resume.safePoint", ValidationCode.InvalidValue)]
    [InlineData("{ \"safePoint\": \"place du bourg\" }", "$.data.resume.safePoint", ValidationCode.InvalidValue)]
    [InlineData("{ \"safePoint\": \"zone.vallons/safe/place\" }", "$.data.resume.safePoint", ValidationCode.InvalidValue)]
    [InlineData("{ \"safePoint\": \"place\\n\" }", "$.data.resume.safePoint", ValidationCode.InvalidValue)]
    [InlineData("{ \"safePoint\": \"place\", \"sector\": \"s7_6\" }", "$.data.resume.sector", ValidationCode.InvalidValue)]
    [InlineData("{ \"safePoint\": \"place\", \"sector\": \"\" }", "$.data.resume.sector", ValidationCode.InvalidValue)]
    [InlineData("{ \"safePoint\": \"place\", \"sector\": \"c14_r12\" }", "$.data.resume.sector", ValidationCode.InvalidValue)]
    [InlineData("{ \"safePoint\": \"place\", \"position\": [1, 2] }", "$.data.resume.position", ValidationCode.UnknownField)]
    public void Point_de_reprise_refuse(string resume, string path, ValidationCode code)
    {
        SaveLoadResult read = SaveJsonReader.Read(Text("    \"resume\": " + resume + ",\n"));
        Assert.False(read.IsValid);
        Assert.Null(read.Save);
        Assert.Contains(read.Report.Errors, e => e.Path == path && e.Code == code);
    }

    [Theory]
    [InlineData("\"place_du_bourg\"")]
    [InlineData("[\"place_du_bourg\"]")]
    [InlineData("7")]
    [InlineData("{ \"safePoint\": 7 }")]
    [InlineData("{ \"safePoint\": \"place\", \"sector\": 706 }")]
    [InlineData("{ \"safePoint\": \"place\", \"safePoint\": \"autre\" }")]
    public void Point_de_reprise_mal_forme_refuse(string resume)
    {
        SaveLoadResult read = SaveJsonReader.Read(Text("    \"resume\": " + resume + ",\n"));
        Assert.False(read.IsValid);
        Assert.Null(read.Save);
        Assert.Contains(read.Report.Errors, e => e.Path.StartsWith("$.data.resume", StringComparison.Ordinal));
    }

    [Fact]
    public void Bornes_de_l_identifiant_et_du_secteur()
    {
        string id64 = "p" + new string('a', 63), id65 = id64 + "a";
        Assert.True(SaveValidator.IsSafePointId(id64));
        Assert.False(SaveValidator.IsSafePointId(id65));
        Assert.False(SaveValidator.IsSafePointId(null));
        Assert.True(SaveJsonReader.Read(Text("    \"resume\": { \"safePoint\": \"" + id64 + "\", \"sector\": \"s99_99\" },\n")).IsValid);
        Assert.False(SaveJsonReader.Read(Text("    \"resume\": { \"safePoint\": \"" + id65 + "\" },\n")).IsValid);
        // Objets publics : mêmes bornes à la construction (une sauvegarde construite s'écrit et se relit toujours).
        Assert.Throws<ArgumentException>(() => new SavedResume(id65));
        Assert.Throws<ArgumentException>(() => new SavedResume("Place"));
        Assert.Throws<ArgumentException>(() => new SavedResume(null!));
        Assert.Throws<ArgumentException>(() => new SavedResume("place", "s7_6"));
        Assert.Throws<ArgumentException>(() => new SavedResume("place", "secteur"));
        // Propriété : tout identifiant accepté par les zones et tout secteur de la plus grande grille s'écrivent et se relisent à l'identique.
        var save = new WorldSave("zone.vallons", 0, 0, Array.Empty<string>());
        foreach (string id in new[] { "a", "a1", "place_du_bourg", "x_", id64 })
            for (int column = 0; column < 32; column += 31)
                for (int row = 0; row < 32; row += 31)
                {
                    var resume = new SavedResume(id, SectorMap.NameOf(column, row));
                    SavedResume back = SaveJsonReader.Read(SaveJsonReader.Write(save.WithResume(resume))).Save!.Resume!;
                    Assert.Equal((resume.SafePoint, resume.Sector), (back.SafePoint, back.Sector));
                }
    }

    // --- Règle (session du monde) -----------------------------------------------------------------------------------------

    [Fact]
    public void La_session_tient_le_point_de_reprise_et_son_secteur()
    {
        ZoneDefinition zone = V11TestData.Zone();
        var start = new WorldSave(zone.Id, 0, 0, Array.Empty<string>(), Hero());
        var session = new WorldSession(start, zone, ShippedData, Progression, ItemsCatalog);
        Assert.Equal("place_du_village", session.LastSafePoint);
        Assert.Equal(("place_du_village", "s07_06"), (session.Resume!.SafePoint, session.Resume.Sector));
        for (int i = 0; i < 5; i++) session.Step();
        Assert.False(session.TrackSafePoint());   // rien n'a bougé : rien ne change
        WorldSave saved = SaveJsonReader.Read(SaveJsonReader.Write(session.Save())).Save!;
        Assert.Equal(("place_du_village", "s07_06"), (saved.Resume!.SafePoint, saved.Resume.Sector));
        // Session ouverte à un autre point sûr (halte, voyage, reprise) : c'est lui.
        var there = new WorldSession(saved, zone, ShippedData, Progression, ItemsCatalog, safePoint: "place_du_bourg");
        Assert.Equal("place_du_bourg", there.LastSafePoint);
        Assert.Equal("place_du_bourg", there.Save().Resume!.SafePoint);
        // Le secteur écrit est celui qui simule la place du point sûr.
        SectorMap map = SectorMap.For(zone);
        foreach (SafePoint point in zone.SafePoints)
            Assert.Equal(map.OwnerOf(point.Position), new WorldSession(start, zone, ShippedData, Progression, ItemsCatalog, safePoint: point.Id).Resume!.Sector);
    }

    [Fact]
    public void Regle_de_reprise_fonction_pure_des_donnees()
    {
        ZoneDefinition zone = V11TestData.Zone();
        StoryBook story = Book(zone);
        WorldSave Save(SavedResume? resume, string? zoneId = null, params string[] facts)
        {
            SavedCharacter hero = Hero();
            if (facts.Length > 0)
                hero = hero.WithStory(new SavedStory(facts, hero.Story.Quests, hero.Story.Trust, hero.Story.Discoveries, hero.Story.Tracked, hero.Story.Guild,
                    hero.Story.Hunted, hero.Story.Reputation, hero.Story.Festivals));
            return new WorldSave(zoneId ?? zone.Id, 0, 0, Array.Empty<string>(), hero, resume: resume);
        }
        (string hidden, string fact) = Assert.Single(story.Reputation.Hidden);
        // Propriété : chaque point sûr de la zone est rendu tel quel, sauf le point secret tant que son fait n'est pas appris.
        foreach (SafePoint point in zone.SafePoints)
        {
            string? expected = point.Id == hidden ? null : point.Id;
            Assert.Equal(expected, WorldSession.ResumeSafePoint(Save(new SavedResume(point.Id, "s07_06")), zone, story, zone.Id));
            Assert.Equal(point.Id, WorldSession.ResumeSafePoint(Save(new SavedResume(point.Id), null, fact), zone, story, zone.Id));
            // Le secteur sauvegardé ne décide de rien (le point sûr fait foi), l'histoire absente non plus.
            Assert.Equal(expected, WorldSession.ResumeSafePoint(Save(new SavedResume(point.Id, "s00_00")), zone, story, zone.Id));
            Assert.Equal(point.Id, WorldSession.ResumeSafePoint(Save(new SavedResume(point.Id)), zone));
            // Le point de reprise d'une autre zone ne vaut pas ici.
            Assert.Null(WorldSession.ResumeSafePoint(Save(new SavedResume(point.Id), "zone.auberge"), zone, story, zone.Id));
        }
        // Rien à reprendre, ou un point sûr que les données n'ont plus : l'entrée (null), sans erreur.
        Assert.Null(WorldSession.ResumeSafePoint(Save(null), zone, story, zone.Id));
        Assert.Null(WorldSession.ResumeSafePoint(Save(new SavedResume("nulle_part", "s07_06")), zone, story, zone.Id));
        Assert.Null(WorldSession.ResumeSafePoint(Save(new SavedResume("coin_du_feu")), zone, story, zone.Id));
        // Sauvegarde sans personnage : le point secret reste fermé.
        Assert.Null(WorldSession.ResumeSafePoint(new WorldSave(zone.Id, 0, 0, Array.Empty<string>(), resume: new SavedResume(hidden)), zone, story, zone.Id));
        Assert.Throws<ArgumentNullException>(() => WorldSession.ResumeSafePoint(null!, zone));
        Assert.Throws<ArgumentNullException>(() => WorldSession.ResumeSafePoint(Save(null), null!));
    }

    [Fact]
    public void Session_sans_zone_du_monde_garde_le_point_de_reprise()
    {
        ZoneDefinition zone = V11TestData.Zone();
        var resume = new SavedResume("place_du_bourg", "s07_06");
        var save = new WorldSave(zone.Id, 0, 0, Array.Empty<string>(), Hero(), resume: resume);
        var session = new WorldSession(save, zone.Setup, ShippedData, Progression, ItemsCatalog, new CombatantPlacement(At(-18, -12), Fixed.Parse("0.5")));
        session.Step();
        Assert.Null(session.LastSafePoint);
        Assert.False(session.TrackSafePoint());
        Assert.Same(resume, session.Save().Resume);
    }

    // --- Partie (aventure) ------------------------------------------------------------------------------------------------

    [Fact]
    public void Partie_relancee_reprend_au_dernier_point_sur()
    {
        AdventureSession a = Adventure(SavePath);
        Assert.Equal("Place du village", a.LastSafePoint);
        a.Save();
        WorldSave first = SaveStore.Read(SavePath).Save!;
        Assert.Equal(("place_du_village", "s07_06"), (first.Resume!.SafePoint, first.Resume.Sector));
        WalkToBourg(a);
        Assert.Equal("Place du Bourg", a.LastSafePoint);
        // S'éloigner ne change rien : le dernier point sûr approché reste celui de la reprise.
        Assert.True(WalkTo(a, -50, -1));
        Assert.Equal("Place du Bourg", a.LastSafePoint);
        a.Save();
        WorldSave saved = SaveStore.Read(SavePath).Save!;
        Assert.Equal(("zone.vallons", "place_du_bourg", "s07_06"), (saved.Zone, saved.Resume!.SafePoint, saved.Resume.Sector));
        long sessions = saved.Sessions;
        // Le jeu est relancé : nouvelle session au point sûr (PV pleins), plus à l'entrée de la zone.
        AdventureSession again = Adventure(SavePath);
        Assert.Equal(At(-58, -6), Position(again));
        Assert.Equal("Place du Bourg", again.LastSafePoint);
        Assert.Equal(sessions + 1, again.World.Zone.Session);
        Assert.Equal(again.World.Zone.Engine.View(AdventureSession.PlayerId).MaxHealth, again.World.Zone.Engine.View(AdventureSession.PlayerId).Health);
        Assert.Contains(again.Text("text.ui.loaded"), again.DrainMessages());
        // Relancé sans rien faire : même sauvegarde, même reprise.
        again.Save();
        Assert.Equal("place_du_bourg", SaveStore.Read(SavePath).Save!.Resume!.SafePoint);
        AdventureSession third = Adventure(SavePath);
        Assert.Equal(At(-58, -6), Position(third));
    }

    [Fact]
    public void Point_sur_approche_sauvegarde_tout_seul()
    {
        AdventureSession a = Adventure(SavePath);
        WalkToBourg(a);
        // La découverte puis l'approche marquent la partie à sauvegarder : la sauvegarde automatique écrit le point de reprise.
        a.Advance(AdventureSession.AutosaveSeconds);
        Assert.Equal("place_du_bourg", SaveStore.Read(SavePath).Save!.Resume!.SafePoint);
        // Retour au village à pied (point sûr déjà découvert) : le point de reprise change, et se sauvegarde sans autre événement.
        Assert.True(WalkTo(a, -50, -1));
        Assert.True(WalkTo(a, -20, -2));
        Assert.True(WalkTo(a, -18, -11));
        Assert.Equal("Place du village", a.LastSafePoint);
        a.Advance(AdventureSession.AutosaveSeconds);
        Assert.Equal("place_du_village", SaveStore.Read(SavePath).Save!.Resume!.SafePoint);
    }

    [Fact]
    public void Sauvegarde_de_version_18_reprise_a_l_entree_puis_mise_a_jour()
    {
        AdventureSession a = Adventure(SavePath);
        WalkToBourg(a);
        a.Save();
        // La même partie telle qu'une version 18 l'aurait écrite : sans point de reprise.
        EditSave(data => data.Remove("resume"), version: 18);
        Assert.True(SaveStore.Read(SavePath).Save!.Migrated);
        AdventureSession again = Adventure(SavePath);
        Assert.Null(again.SaveProblem);
        Assert.Equal(At(-18, -12), Position(again));   // comme avant le V2.28 : l'entrée de la zone
        Assert.Equal("Place du village", again.LastSafePoint);
        again.Save();
        string written = File.ReadAllText(SavePath);
        Assert.Contains(SaveVersion.Current, written, StringComparison.Ordinal);
        Assert.Equal("place_du_village", SaveStore.Read(SavePath).Save!.Resume!.SafePoint);
        Assert.Contains("zone.vallons/safe/place_du_bourg", SaveStore.Read(SavePath).Save!.Consumed);   // rien d'autre n'est perdu
    }

    [Fact]
    public void Point_de_reprise_inconnu_secret_ou_au_secteur_perime()
    {
        AdventureSession a = Adventure(SavePath);
        a.Save();
        // Un point sûr que les données n'ont plus : l'entrée de la zone, sans refuser la sauvegarde.
        EditSave(data => data["resume"] = new JsonObject { ["safePoint"] = "nulle_part", ["sector"] = "s07_06" });
        AdventureSession unknown = Adventure(SavePath);
        Assert.Null(unknown.SaveProblem);
        Assert.Equal(At(-18, -12), Position(unknown));
        // Le point secret des contrebandiers, sans le fait qui le révèle : l'entrée (jamais la planque).
        EditSave(data => data["resume"] = new JsonObject { ["safePoint"] = "planque_des_contrebandiers", ["sector"] = "s07_06" });
        AdventureSession secret = Adventure(SavePath);
        Assert.Null(secret.SaveProblem);
        Assert.Equal(At(-18, -12), Position(secret));
        Assert.Equal("Place du village", secret.LastSafePoint);
        // Un secteur périmé (le point sûr a été déplacé par les données) : le point sûr fait foi, le secteur est réécrit.
        EditSave(data => data["resume"] = new JsonObject { ["safePoint"] = "place_du_bourg", ["sector"] = "s00_00" });
        AdventureSession stale = Adventure(SavePath);
        Assert.Equal(At(-58, -6), Position(stale));
        stale.Save();
        WorldSave rewritten = SaveStore.Read(SavePath).Save!;
        Assert.Equal(("place_du_bourg", "s07_06"), (rewritten.Resume!.SafePoint, rewritten.Resume.Sector));
        // Un point de reprise mal formé : la sauvegarde est refusée comme toute sauvegarde invalide (copie de secours, sinon nouvelle partie).
        EditSave(data => data["resume"] = new JsonObject { ["safePoint"] = "Place Du Bourg" });
        Assert.False(SaveJsonReader.Read(File.ReadAllText(SavePath)).IsValid);
    }

    [Fact]
    public void Porte_voyage_et_interieur()
    {
        AdventureSession a = Adventure(SavePath);
        WalkToBourg(a);
        // Voyage rapide : la partie sauvegardée reprend au point sûr d'arrivée.
        Assert.Equal(TravelRefusal.None, a.Travel("zone.vallons", "place_du_village"));
        Assert.Equal("place_du_village", SaveStore.Read(SavePath).Save!.Resume!.SafePoint);
        a.Advance(0.05);
        Assert.Equal(TravelRefusal.None, a.Travel("zone.vallons", "place_du_bourg"));
        WorldSave traveled = SaveStore.Read(SavePath).Save!;
        Assert.Equal(("place_du_bourg", "s07_06"), (traveled.Resume!.SafePoint, traveled.Resume.Sector));
        // Porte de l'auberge : la partie est sauvegardée dans l'intérieur, à son point sûr (zone sans région : pas de secteur).
        a.Advance(0.05);
        Assert.True(WalkTo(a, -66, 0));
        Assert.True(WalkTo(a, -69, 2.5, 0.5));
        Assert.True(a.UseDoor());
        WorldSave inside = SaveStore.Read(SavePath).Save!;
        Assert.Equal(("zone.auberge", "coin_du_feu", null), (inside.Zone, inside.Resume!.SafePoint, inside.Resume.Sector));
        Assert.Equal("Coin du feu", a.LastSafePoint);
        AdventureSession again = Adventure(SavePath);
        Assert.True(again.Indoors);
        Assert.Equal("Coin du feu", again.LastSafePoint);
        // En ressortant : le point sûr le plus proche de la porte (la place du Bourg), écrit dans la sauvegarde ; la partie relancée y reprend.
        Assert.True(WalkTo(again, 0, -3.3, 0.5));
        Assert.True(again.UseDoor());
        Assert.False(again.Indoors);
        Assert.Equal("Place du Bourg", again.LastSafePoint);
        WorldSave outside = SaveStore.Read(SavePath).Save!;
        Assert.Equal(("zone.vallons", "place_du_bourg", "s07_06"), (outside.Zone, outside.Resume!.SafePoint, outside.Resume.Sector));
        AdventureSession back = Adventure(SavePath);
        Assert.Equal(At(-58, -6), Position(back));
    }

    // --- Point sûr de reprise occupé (relecture indépendante de la PR #155) ---------------------------------------------------

    /// <summary>Le sanglier arrive au premier tick sur la place du Bourg (il passe avant le personnage, que le validateur ne protège pas d'une arrivée).</summary>
    private static void BoarOnTheBourg(JsonObject scenario)
    {
        JsonObject boar = scenario["data"]!["combatants"]!.AsArray().Single(c => (string)c!["id"]! == "sanglier")!.AsObject();
        boar["position"] = new JsonArray(-58, -6);
        boar["arrivalMs"] = 0;
    }

    private (string Data, string Scenarios) CopyWithBoarOnTheBourg()
    {
        string data = Path.Combine(_folder, "data", "v0"), scenarios = Path.Combine(_folder, "scenarios");
        foreach ((string from, string to) in new[] { (DataDirectory, data), (ScenariosDirectory, scenarios) })
            foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            {
                string target = Path.Combine(to, Path.GetRelativePath(from, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, true);
            }
        string path = Path.Combine(scenarios, "zones", "vallons.json");
        JsonObject root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        BoarOnTheBourg(root);
        File.WriteAllText(path, root.ToJsonString());
        return (data, scenarios);
    }

    private static double Distance(GroundVector a, GroundVector b) => Math.Sqrt(Math.Pow((a.X.Raw - b.X.Raw) / 1e4, 2) + Math.Pow((a.Z.Raw - b.Z.Raw) / 1e4, 2));

    [Fact]
    public void Point_de_reprise_occupe_place_libre_la_plus_proche()
    {
        (string data, string scenarios) = CopyWithBoarOnTheBourg();
        AdventureSession a = Adventure(SavePath, data, scenarios);
        Assert.False(a.Stranded);   // nouvelle partie : l'entrée (le village) est libre
        a.Save();
        EditSave(d => d["resume"] = new JsonObject { ["safePoint"] = "place_du_bourg", ["sector"] = "s07_06" });
        WorldSave saved = SaveStore.Read(SavePath).Save!;
        // Le cas est réel : une session ouverte sur le point sûr même n'y arrive pas (le sanglier y est déjà).
        var exact = new WorldSession(saved, a.World.Definition!, ShippedData, Progression, ItemsCatalog, safePoint: "place_du_bourg");
        exact.Step();
        exact.Step();
        Assert.False(exact.Zone.Engine.View(AdventureSession.PlayerId).IsPresent);
        // Le jeu relancé : le personnage arrive à la place libre la plus proche du point sûr, sans erreur ni attente.
        AdventureSession again = Adventure(SavePath, data, scenarios);
        Assert.Null(again.SaveProblem);
        Assert.False(again.Stranded);
        Assert.False(again.MustContinue);
        Assert.True(again.World.Zone.Engine.View("sanglier").IsPresent);
        Assert.InRange(Distance(again.World.Zone.Engine.View("sanglier").Position!.Value, At(-58, -6)), 0, 1.0);   // arrivé sur le point sûr (il a fait un pas depuis)
        GroundVector here = Position(again);
        Assert.NotEqual(At(-58, -6), here);
        Assert.InRange(Distance(here, At(-58, -6)), 0.5, 8.0);
        Assert.Equal("Place du Bourg", again.LastSafePoint);
        // C'est la place de la règle (fonction pure des données et de la sauvegarde), la même à chaque relance.
        (string? point, GroundVector? place) = WorldSession.ResumeArrival(saved, again.World.Definition!, Progression, again.World.Narrative!.Book, AdventureSession.ZoneId);
        Assert.Equal(("place_du_bourg", (GroundVector?)here), (point, place));
        Assert.Equal(here, Position(Adventure(SavePath, data, scenarios)));
    }

    [Fact]
    public void Regle_d_arrivee_de_la_reprise_et_repli_sur_l_entree()
    {
        ZoneDefinition busy = V11TestData.Zone(scenario: BoarOnTheBourg), calm = V11TestData.Zone();
        WorldSave Save(ZoneDefinition zone, string? safePoint) =>
            new WorldSave(zone.Id, 0, 0, new[] { PlayerCharacters.PlacesKey }, Hero(), resume: safePoint is null ? null : new SavedResume(safePoint, "s07_06"));
        // Point sûr libre : le point sûr même (aucune place à part) ; rien à reprendre : l'entrée.
        Assert.Equal(("place_du_bourg", (GroundVector?)null), WorldSession.ResumeArrival(Save(calm, "place_du_bourg"), calm, Progression));
        Assert.Equal(((string?)null, (GroundVector?)null), WorldSession.ResumeArrival(Save(calm, null), calm, Progression));
        Assert.Equal(((string?)null, (GroundVector?)null), WorldSession.ResumeArrival(Save(calm, "nulle_part"), calm, Progression));
        // Point sûr occupé : la première place libre des anneaux de 0,5 m (ordre fixe : l'est d'abord), hors de l'emprise du sanglier.
        (string? point, GroundVector? place) = WorldSession.ResumeArrival(Save(busy, "place_du_bourg"), busy, Progression);
        Assert.Equal("place_du_bourg", point);
        Assert.NotNull(place);
        Assert.Equal(At(-58, -6).Z, place!.Value.Z);
        Assert.True(place.Value.X > At(-58, -6).X);
        Assert.Equal(place, WorldSession.ResumeArrival(Save(busy, "place_du_bourg"), busy, Progression).Place);   // déterministe
        // Les autres points sûrs de la même zone ne sont pas touchés.
        Assert.Equal(("place_du_village", (GroundVector?)null), WorldSession.ResumeArrival(Save(busy, "place_du_village"), busy, Progression));
        // Aucune place libre dans les anneaux (ici réduits à 0,5 m, tous sous le sanglier) : l'entrée de la zone, sans erreur.
        Assert.Equal(((string?)null, (GroundVector?)null), WorldSession.ResumeArrival(Save(busy, "place_du_bourg"), busy, Progression, null, null, rings: 1));
        Assert.Equal(((string?)null, (GroundVector?)null), WorldSession.ResumeArrival(Save(busy, "place_du_bourg"), busy, Progression, null, null, rings: 0));
        // La règle seule : une place hors de l'aire à plus de 8 m n'a aucune place libre ; une place libre est rendue telle quelle.
        Fixed r = WorldSession.CharacterRadius;
        Assert.Null(WorldSession.FreeArrival(calm.Setup, Array.Empty<EdS.Domain.Combat.CombatantSetup>(), At(-120, 0), r));
        Assert.Equal(At(-58, -6), WorldSession.FreeArrival(calm.Setup, Array.Empty<EdS.Domain.Combat.CombatantSetup>(), At(-58, -6), r));
        Assert.Throws<ArgumentNullException>(() => WorldSession.ResumeArrival(Save(calm, null), calm, null!));
        // La session n'accepte une place d'arrivée qu'autour de son point sûr (8 m), jamais avec une porte.
        var aside = new WorldSession(Save(busy, null), busy, ShippedData, Progression, ItemsCatalog, safePoint: "place_du_bourg", place: place);
        aside.Step();
        aside.Step();
        Assert.True(aside.Zone.Engine.View("bastion").IsPresent);
        Assert.Equal(place, aside.Zone.Engine.View("bastion").Position);
        Assert.Equal("place_du_bourg", aside.LastSafePoint);
        Assert.Throws<ArgumentException>(() => new WorldSession(Save(calm, null), calm, ShippedData, Progression, ItemsCatalog, safePoint: "place_du_bourg", place: At(-49.9, -6)));
        Assert.Throws<ArgumentException>(() => new WorldSession(Save(calm, null), calm, ShippedData, Progression, ItemsCatalog, door: "porte_auberge", place: At(-69, 4)));
    }

    [Fact]
    public void Donnees_livrees_aucun_point_sur_n_est_occupe_au_debut_d_une_session()
    {
        // Avec les données livrées, le repli ne sert jamais : le validateur des zones refuse une créature de départ ou une forme de chef sur
        // un point sûr, et aucune créature n'y arrive au premier tick. Propriété : à chaque heure du monde sur quatre jours (nuits, attaque
        // du hameau, faille, chasse de l'aube : niveau 12), chaque point sûr est libre ; un échantillon de sessions le confirme au moteur.
        ZoneDefinition zone = V11TestData.Zone();
        StoryBook story = Book(zone);
        long xp = Enumerable.Range(0, 100_000).Select(i => (long)i * 10).First(x => Progression.LevelFor(x) >= 12);
        SavedCharacter hero = Hero().WithXp(xp).WithStory(new SavedStory(story.Reputation.Hidden.Select(h => h.Fact)));
        int sessions = 0;
        for (int hour = 0; hour < 96; hour++)
            foreach (SafePoint point in zone.SafePoints)
            {
                var save = new WorldSave(zone.Id, hour * 9000L, 3, new[] { PlayerCharacters.PlacesKey }, hero, resume: new SavedResume(point.Id));
                Assert.Equal((point.Id, (GroundVector?)null), WorldSession.ResumeArrival(save, zone, Progression, story, zone.Id));
                if (hour % 8 != 5) continue;   // 13 h, 21 h (attaque du hameau), 5 h (aube) : sessions jouées
                var session = new WorldSession(save, zone, ShippedData, Progression, ItemsCatalog, story, safePoint: point.Id);
                session.Step();
                session.Step();
                Assert.True(session.Zone.Engine.View("bastion").IsPresent, $"{point.Id}, heure {hour}");
                sessions++;
            }
        Assert.Equal(12 * zone.SafePoints.Count, sessions);
    }

    [Fact]
    public void Halte_et_reprise_comme_avant()
    {
        AdventureSession a = Adventure(SavePath);
        WalkToBourg(a);
        Assert.True(a.CanRest);
        int opened = a.Opened;
        Assert.True(a.Rest());
        a.Advance(0.05);
        Assert.Equal((opened + 1, At(-58, -6)), (a.Opened, Position(a)));
        Assert.Equal("place_du_bourg", SaveStore.Read(SavePath).Save!.Resume!.SafePoint);
    }
}
