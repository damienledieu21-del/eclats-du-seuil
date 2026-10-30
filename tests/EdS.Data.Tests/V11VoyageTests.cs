using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Zones;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V1.1 : points sûrs découverts (clé au registre du monde, gardée dans la sauvegarde) et voyage rapide entre eux, hors combat
/// (règle de la halte et des talents : aucun ennemi à moins de 12 m, aucun ennemi engagé), par le chemin d'une porte (sauvegarde, puis
/// nouvelle session au point d'arrivée).
/// </summary>
[Trait("Category", "World")]
public class V11VoyageTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v11_voyage_" + Guid.NewGuid().ToString("N"));

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

    /// <summary>Données et scénarios copiés dans le dossier du test, scénario (et zone) des Vallons retouchés.</summary>
    private (string Data, string ScenariosDirectory) Copy(Action<JsonObject> scenario, Action<JsonObject>? zone = null)
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
        scenario(root);
        File.WriteAllText(path, root.ToJsonString());
        if (zone != null)
        {
            string zonePath = ZoneJsonReader.PathIn(data, "zone.vallons");
            JsonObject z = JsonNode.Parse(File.ReadAllText(zonePath))!.AsObject();
            zone(z);
            File.WriteAllText(zonePath, z.ToJsonString());
        }
        return (data, scenarios);
    }

    private static JsonObject Creature(JsonObject scenario, string id) =>
        scenario["data"]!["combatants"]!.AsArray().Single(c => (string)c!["id"]! == id)!.AsObject();

    // --- Découverte ----------------------------------------------------------------------------------------------------

    [Fact]
    public void Point_d_entree_decouvert_sans_bruit()
    {
        AdventureSession a = Adventure(SavePath);
        Assert.Equal(new[] { "Nouvelle partie : bienvenue dans les Vallons d'Aube." }, a.DrainMessages());
        TravelDestination here = Assert.Single(a.TravelDestinations());
        Assert.Equal(("zone.vallons", "place_du_village", "Place du village", true), (here.Zone, here.SafePoint, here.Name, here.Here));
        Assert.True(a.World.IsDiscovered(a.World.Definition!, "place_du_village"));
        Assert.False(a.World.IsDiscovered(a.World.Definition!, "place_du_bourg"));
        Assert.Equal("zone.vallons/safe/place_du_bourg", a.World.Definition!.SafePointKey("place_du_bourg"));
        Assert.False(File.Exists(SavePath));   // rien n'a changé que le joueur ait fait
    }

    [Fact]
    public void Decouvrir_le_bourg_puis_y_revenir_d_un_coup()
    {
        AdventureSession a = Adventure(SavePath);
        a.DrainMessages();
        Assert.Equal(TravelRefusal.NotDiscovered, a.CanTravel("zone.vallons", "place_du_bourg"));
        WalkToBourg(a);
        Assert.Contains("Point sûr découvert : Place du Bourg. Le voyage rapide y mène depuis la carte (L).", a.DrainMessages());
        Assert.Equal(new[] { "place_du_village", "place_du_bourg" }, a.TravelDestinations().Select(d => d.SafePoint));
        Assert.True(a.TravelDestinations()[1].Here);
        // Retour d'un coup au village : sauvegarde, puis nouvelle session au point sûr (PV pleins, comme une halte).
        Assert.Equal(TravelRefusal.None, a.CanTravel("zone.vallons", "place_du_village"));
        int opened = a.Opened;
        Assert.Equal(TravelRefusal.None, a.Travel("zone.vallons", "place_du_village"));
        Assert.Equal((opened + 1, 1), (a.Opened, a.Travels));
        Assert.Equal("Voyage rapide : te voici à Place du village.", a.DrainMessages().Last());
        a.Advance(0.05);
        Assert.Equal(At(-18, -12), Position(a));
        Assert.Equal("Place du village", a.LastSafePoint);
        WorldSave saved = SaveStore.Read(SavePath).Save!;
        Assert.Contains("zone.vallons/safe/place_du_bourg", saved.Consumed);
        Assert.Contains("zone.vallons/safe/place_du_village", saved.Consumed);
        // La découverte survit à la reprise : le bourg reste une destination, et l'on y retourne.
        AdventureSession again = Adventure(SavePath);
        Assert.Equal(new[] { "place_du_village", "place_du_bourg" }, again.TravelDestinations().Select(d => d.SafePoint));
        Assert.Equal(TravelRefusal.None, again.Travel("zone.vallons", "place_du_bourg"));
        again.Advance(0.05);
        Assert.Equal(At(-58, -6), Position(again));
    }

    [Fact]
    public void Destinations_inconnues_ou_pas_decouvertes()
    {
        AdventureSession a = Adventure(SavePath);
        a.DrainMessages();
        Assert.Equal(TravelRefusal.UnknownPlace, a.Travel("zone.inconnue", "place_du_village"));
        Assert.Equal(TravelRefusal.UnknownPlace, a.Travel("zone.vallons", "nulle_part"));
        Assert.Equal(TravelRefusal.UnknownPlace, a.Travel("zone.auberge", "coin_du_feu"));   // un intérieur : on y entre par sa porte
        Assert.Equal(TravelRefusal.NotDiscovered, a.Travel("zone.vallons", "hameau_de_la_carriere"));
        Assert.Equal(new[] { "Destination inconnue.", "Destination inconnue.", "Destination inconnue.", "Point sûr pas encore découvert : va d'abord jusqu'à lui." },
            a.DrainMessages());
        Assert.Equal((1, 0), (a.Opened, a.Travels));
        Assert.False(File.Exists(SavePath));
        Assert.Equal(TravelRefusal.UnknownPlace, a.World.CanTravel(null!, "place_du_village"));
    }

    [Fact]
    public void Pas_de_voyage_pres_d_un_ennemi()
    {
        // Un sanglier à 8 m de l'entrée : en plein combat (règle de la halte et des talents), le voyage est refusé ; rien ne change.
        (string data, string scenarios) = Copy(s => Creature(s, "sanglier")["position"] = new JsonArray(-10, -12));
        string savePath = SavePath;
        AdventureSession a = Adventure(savePath, data, scenarios);
        a.DrainMessages();
        Assert.True(a.World.InCombat);
        Assert.False(a.CanChangeTalents);   // même règle
        Assert.Equal(TravelRefusal.InCombat, a.CanTravel("zone.vallons", "place_du_village"));
        a.Save();
        string before = File.ReadAllText(savePath);
        Assert.Equal(TravelRefusal.InCombat, a.Travel("zone.vallons", "place_du_village"));
        Assert.Equal("Pas de voyage en plein combat : éloigne-toi des ennemis.", a.DrainMessages().Last());
        Assert.Equal((1, 0), (a.Opened, a.Travels));
        Assert.Equal(before, File.ReadAllText(savePath));
    }

    [Fact]
    public void Pas_de_voyage_avec_un_ennemi_engage_meme_de_loin()
    {
        // Un archer qui garde ses distances (14 à 16 m) et vise le personnage : au-delà des 12 m, c'est encore un combat.
        ZoneDefinition zone = Zone(-18, -12, s =>
        {
            JsonObject archer = Creature(s, "archer");
            archer["position"] = new JsonArray(-18, -27);
            archer["behavior"] = new JsonObject { ["maxRange"] = 16, ["minRange"] = 14, ["aggroRange"] = 20, ["pack"] = false };
            foreach (JsonNode? attack in archer["attacks"]!.AsArray()) attack!["rawDamage"] = 1;
        });
        WorldSession w = Play(zone, Hero());
        bool engaged = false;
        for (int i = 0; i < 200 && !engaged; i++)
        {
            w.Step();
            var archer = w.Zone.Engine.View("archer");
            engaged = archer.Engaged && archer.CurrentTarget == "bastion";
        }
        Assert.True(engaged);
        GroundVector me = V11TestData.Position(w), them = w.Zone.Engine.View("archer").Position!.Value;
        double distance = Math.Sqrt(Math.Pow((me.X.Raw - them.X.Raw) / 1e4, 2) + Math.Pow((me.Z.Raw - them.Z.Raw) / 1e4, 2));
        Assert.True(distance > 12, distance.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.True(w.InCombat);
        Assert.Equal(TravelRefusal.InCombat, w.CanTravel(zone, "place_du_village"));
        // Le même archer, calme et loin : hors combat (vers le bourg découvert ; le village est le point où l'on est, relecture de la PR #72).
        WorldSession calm = Play(Zone(), Hero(), consumed: new[] { "zone.vallons/safe/place_du_bourg" });
        Assert.False(calm.InCombat);
        Assert.Equal(TravelRefusal.None, calm.CanTravel(calm.Definition!, "place_du_bourg"));
        Assert.Equal(TravelRefusal.AlreadyHere, calm.CanTravel(calm.Definition!, "place_du_village"));
    }

    [Fact]
    public void Voyage_depuis_un_interieur()
    {
        // La partie reprise dans l'auberge : la carte propose les points sûrs du dehors ; le voyage ramène aux Vallons.
        Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
        SaveStore.Write(SavePath, new WorldSave("zone.auberge", 0, 2, new[] { "zone.vallons/safe/place_du_bourg", "zone.vallons/safe/place_du_village" }, Hero(5)));
        AdventureSession a = Adventure(SavePath);
        Assert.True(a.Indoors);
        Assert.Equal(new[] { "place_du_village", "place_du_bourg" }, a.TravelDestinations().Select(d => d.SafePoint));
        Assert.All(a.TravelDestinations(), d => Assert.False(d.Here));
        Assert.Equal(TravelRefusal.None, a.Travel("zone.vallons", "place_du_bourg"));
        Assert.False(a.Indoors);
        a.Advance(0.05);
        Assert.Equal(At(-58, -6), Position(a));
        Assert.Equal(("zone.vallons", 5L), (SaveStore.Read(SavePath).Save!.Zone, SaveStore.Read(SavePath).Save!.Character!.Money));
        // Dans un intérieur, rien ne se découvre (la porte n'est pas un point sûr du voyage).
        Assert.DoesNotContain(SaveStore.Read(SavePath).Save!.Consumed, k => k.StartsWith("zone.auberge/", StringComparison.Ordinal));
    }

    [Fact]
    public void Ancienne_sauvegarde_lue()
    {
        // Une sauvegarde de version 5 (avant les points sûrs) : seul le point d'arrivée de la session est découvert, la bourse est vide.
        Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
        string v6 = SaveJsonReader.Write(new WorldSave("zone.vallons", 0, 1, Array.Empty<string>(), Hero()));
        File.WriteAllText(SavePath, v6.Replace(",\n      \"money\": 0\n", "\n").Replace("\"schemaVersion\": 6", "\"schemaVersion\": 5"));
        AdventureSession a = Adventure(SavePath);
        Assert.Equal("Partie reprise.", a.DrainMessages().First());
        Assert.Equal("place_du_village", Assert.Single(a.TravelDestinations()).SafePoint);
        Assert.Equal(0, a.Money);
        Assert.Equal("0 cuivre", a.Purse);
        a.Save();
        Assert.Contains("\"schemaVersion\": 6,", File.ReadAllText(SavePath), StringComparison.Ordinal);
        Assert.Contains("zone.vallons/safe/place_du_village", SaveStore.Read(SavePath).Save!.Consumed);
    }

    [Fact]
    public void Voyage_indisponible_pendant_un_dialogue_ou_apres_une_defaite()
    {
        // Dialogue ouvert avec Odile : pas de voyage tant qu'il n'est pas fermé.
        (string data, string scenarios) = Copy(_ => { }, z => z["data"]!["safePoints"]![0]!["position"] = new JsonArray(-9, 2));
        AdventureSession talk = Adventure(SavePath, data, scenarios);
        Assert.True(talk.Interact());
        Assert.NotNull(talk.Dialogue);
        Assert.Equal(TravelRefusal.Unavailable, talk.CanTravel("zone.vallons", "place_du_village"));
        talk.CloseDialogue();
        // Dialogue fermé : seul reste le refus du point où l'on est (relecture de la PR #72 : le voyage n'est pas une halte gratuite).
        Assert.Equal(TravelRefusal.AlreadyHere, talk.CanTravel("zone.vallons", "place_du_village"));
        Directory.Delete(_folder, true);

        // Tombé : le voyage attend la reprise (Entrée), qui ramène au dernier point sûr.
        (data, scenarios) = Copy(s =>
        {
            JsonObject boar = Creature(s, "sanglier");
            boar["position"] = new JsonArray(-14, -12);
            foreach (JsonNode? attack in boar["attacks"]!.AsArray()) attack!["rawDamage"] = 100_000;
        });
        AdventureSession a = Adventure(SavePath, data, scenarios);
        for (int i = 0; i < 1200 && !a.World.IsDefeated; i++) a.Advance(0.05);
        Assert.True(a.World.IsDefeated);
        Assert.Equal(TravelRefusal.Unavailable, a.World.CanTravel(a.World.Definition!, "place_du_village"));
        Assert.Equal(TravelRefusal.Unavailable, a.Travel("zone.vallons", "place_du_village"));
        Assert.Equal("Voyage impossible pour l'instant.", a.DrainMessages().Last());
        Assert.Equal(0, a.Travels);
    }

    [Fact]
    public void Sauvegarde_impossible_on_reste_ou_l_on_est()
    {
        // Partie au village, le bourg déjà découvert (relecture de la PR #72 : pas de voyage vers le point où l'on est).
        Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
        SaveStore.Write(SavePath, new WorldSave("zone.vallons", 0, 1, new[] { "zone.vallons/safe/place_du_bourg", "zone.vallons/safe/place_du_village" }, Hero()));
        string before = File.ReadAllText(SavePath);
        AdventureSession a = Adventure(SavePath);
        a.DrainMessages();
        // Un dossier à la place du fichier temporaire : l'écriture échoue.
        Directory.CreateDirectory(SaveStore.TemporaryPath(SavePath));
        Assert.Equal(TravelRefusal.SaveFailed, a.Travel("zone.vallons", "place_du_bourg"));
        Assert.StartsWith("Sauvegarde impossible", a.DrainMessages().Last(), StringComparison.Ordinal);
        Assert.Equal((1, 0), (a.Opened, a.Travels));
        Assert.Equal(before, File.ReadAllText(SavePath));
        Assert.Equal("zone.vallons", a.CurrentZone);
    }

    [Fact]
    public void Decouverte_dans_la_session_du_monde()
    {
        // Au niveau de la session : un point sûr approché à 5 m est découvert une fois (événement de l'histoire), puis gardé au registre.
        ZoneDefinition zone = Zone(-62, -6);   // à 4 m de la place du bourg
        WorldSession w = new WorldSession(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Hero()), zone, ShippedData, Progression, ItemsCatalog, Book(zone));
        var events = new List<StoryEvent>();
        for (int i = 0; i < 5; i++) events.AddRange(w.Step().Story);
        Assert.Equal(new[] { "SafePointDiscovered place_du_bourg" }, events.Select(e => e.ToString()));
        Assert.Equal(new[] { "zone.vallons/safe/place_du_bourg", "zone.vallons/safe/place_du_village" }, w.Save().Consumed);
        // Une nouvelle session sur la sauvegarde : déjà découverts, aucun nouvel événement.
        var again = new WorldSession(w.Save(), zone, ShippedData, Progression, ItemsCatalog, Book(zone));
        Assert.Empty(Enumerable.Range(0, 5).SelectMany(_ => again.Step().Story));
        // Registre presque plein : la découverte attend, la place des morts possibles d'un tick (une par combattant, chef compris) reste
        // gardée. Relecture de la PR #72 : à l'ouverture, la découverte du point de départ se fait après l'arrivée du chef, qui compte donc
        // (avant, elle se faisait sans lui : avec autant de places que de combattants, le point était découvert et le premier tick levait
        // « Registre des événements presque plein »). Avec une place de plus que les combattants (chef compris), le point d'entrée est
        // découvert ; ensuite, plus rien tant que la place manque. Le cas limite (autant de places que de combattants) est dans
        // V11RelectureTests.Decouverte_du_depart_laisse_au_chef_sa_place_au_registre.
        int combatants = new WorldSession(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Hero()), zone, ShippedData, Progression, ItemsCatalog,
            Book(zone)).Zone.Engine.CombatantIds.Count;
        string[] full = Enumerable.Range(0, SaveValidator.MaxConsumed - combatants - 1).Select(i => $"zone.vallons/test/k{i}").ToArray();
        var crowded = new WorldSession(new WorldSave("zone.vallons", 0, 0, full, Hero()), zone, ShippedData, Progression, ItemsCatalog, Book(zone));
        Assert.True(crowded.IsDiscovered(zone, "place_du_village"));
        Assert.Empty(Enumerable.Range(0, 3).SelectMany(_ => crowded.Step().Story));
        Assert.False(crowded.IsDiscovered(zone, "place_du_bourg"));
        Assert.Equal(combatants, crowded.Zone.RegistryRoom);
    }
}
