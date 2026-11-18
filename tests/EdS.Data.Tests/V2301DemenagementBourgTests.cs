using System.Text.Json.Nodes;
using EdS.Data.Characters;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Zones;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.30.1 (déménagement du bourg, retenu par Damien le 11 novembre 2026, PLAN_GRANDE_CARTE.md § 4.4 bis) : les boutiques,
/// l'auberge, la Guilde, la chapelle, la maison et le grand jour de marché passent dans la ville fortifiée, en bloc, de
/// (−1 032, −34) m. Identifiants inchangés (portes, intérieurs, point sûr <c>place_du_bourg</c>, lieu <c>bourg</c>, PNJ, étals) ; seules
/// les positions changent. Il reste à l'ancien emplacement un hameau de deux maisons et la grand-rue vers la route d'Ambre.
/// Vérifié ici sur les vraies données : portes collées à leur bâtiment et dégagées, rien laissé au hameau, PNJ et étals hors des
/// murs, marche de la place du marché à chaque porte, reprise d'une sauvegarde au point sûr de la place.
/// </summary>
[Trait("Category", "World")]
public class V2301DemenagementBourgTests
{
    private static readonly Lazy<ZoneDefinition> Vallons = new(() => ZoneJsonReader.ReadFile(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons"),
        Path.Combine(AppContext.BaseDirectory, "scenarios"), ShippedData, ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!).Zone!);

    /// <summary>Le décalage du bloc du bourg (mètres).</summary>
    private const double DX = -1032, DZ = -34;

    /// <summary>Les portes des boutiques et de leur bâtiment (inchangés).</summary>
    private static readonly (string Door, string Building)[] Shops =
    {
        ("porte_auberge", "auberge"), ("porte_chapelle", "chapelle"), ("porte_maison", "maison5"), ("porte_guilde", "maison6"),
        ("porte_boulangerie", "maison2"), ("porte_epicerie", "maison4"), ("porte_herboristerie", "maison7"), ("porte_mercerie", "maison1"),
    };

    private static (double X, double Z) P(GroundVector v) => (v.X.Raw / 1e4, v.Z.Raw / 1e4);

    private static (double X0, double Z0, double X1, double Z1) Box(string id)
    {
        var pts = Vallons.Value.Setup.Space!.Obstacles.Single(o => o.Id == id).Points.Select(P).ToList();
        return (pts.Min(p => p.X), pts.Min(p => p.Z), pts.Max(p => p.X), pts.Max(p => p.Z));
    }

    private static bool Inside((double X0, double Z0, double X1, double Z1) b, double x, double z, double margin) =>
        x > b.X0 - margin && x < b.X1 + margin && z > b.Z0 - margin && z < b.Z1 + margin;

    private static bool Blocked(double x, double z, double margin) =>
        Vallons.Value.Setup.Space!.Obstacles.Any(o => Inside(Box(o.Id), x, z, margin));

    [Fact]
    public void Portes_dans_la_ville_collees_a_leur_batiment_et_degagees()
    {
        ZoneDefinition zone = Vallons.Value;
        var town = zone.Places.Single(p => p.Id == "ville_du_bourg");
        foreach ((string doorId, string building) in Shops)
        {
            Door door = zone.Doors.Single(d => d.Id == doorId);
            (double x, double z) = P(door.Position);
            Assert.Equal(building, door.Building);
            Assert.InRange(x, P(town.Min).X, P(town.Max).X);
            Assert.InRange(z, P(town.Min).Z, P(town.Max).Z);
            var b = Box(building);
            Assert.True(Inside(b, x, z, 1.5), $"{doorId} à plus de 1,5 m de {building}");
            // Le bâtiment a bougé avec sa porte : même place relative qu'avant (décalage du bloc).
            Assert.True(b.X0 < -1000 && b.Z0 < -15, $"{building} pas dans la ville");
        }
        // Les intérieurs ne bougent pas : chaque porte mène toujours à sa zone, par sa sortie.
        Assert.Equal("zone.auberge", zone.Doors.Single(d => d.Id == "porte_auberge").To);
        Assert.All(Shops, s => Assert.Equal("sortie", zone.Doors.Single(d => d.Id == s.Door).ToDoor));
    }

    [Fact]
    public void Le_hameau_reste_a_l_ancien_emplacement_sans_porte_ni_boutique()
    {
        ZoneDefinition zone = Vallons.Value;
        static bool OldBourg((double X, double Z) p) => p.X >= -100 && p.X <= -26 && p.Z >= -24 && p.Z <= 19;
        Assert.DoesNotContain(zone.Doors, d => OldBourg(P(d.Position)));
        Assert.DoesNotContain(zone.NightSpots, n => OldBourg(P(n.Position)));
        Assert.DoesNotContain(zone.SafePoints, s => OldBourg(P(s.Position)));
        // Deux maisons du hameau restent ; la grand-rue va toujours du village à la route d'Ambre.
        Assert.Equal((-35.0, -15.0, -25.0, -6.0), Box("maison3"));
        Assert.Equal((-99.0, 4.0, -93.0, 10.5), Box("maison8"));
        Assert.Equal((-50.0, -1.0), P(zone.Roads.Single(r => r.Id == "grand_rue_ouest").Points[0]));
        Assert.Equal((-99.0, -1.0), P(zone.Roads.Single(r => r.Id == "route_d_ambre_ouest").Points[0]));
        // Le point sûr et le lieu gardent leurs identifiants, déplacés en bloc.
        Assert.Equal((-58.0 + DX, -6.0 + DZ), P(zone.SafePoints.Single(s => s.Id == "place_du_bourg").Position));
        var bourg = zone.Places.Single(p => p.Id == "bourg");
        Assert.Equal((-100.0 + DX, -24.0 + DZ, -24.0 + DX, 19.0 + DZ), (P(bourg.Min).X, P(bourg.Min).Z, P(bourg.Max).X, P(bourg.Max).Z));
        Assert.False(Blocked(-58 + DX, -6 + DZ, 1));
        // Le bloc du bourg est entier dans l'enceinte, à l'écart de la route d'Ambre et de la place du marché.
        foreach ((_, string building) in Shops)
        {
            var b = Box(building);
            Assert.True(b.X0 > -1310 && b.X1 < -1050 && b.Z0 > -70 && b.Z1 < 180, $"{building} : {b}");
        }
    }

    [Fact]
    public void PNJ_etals_et_invites_des_fetes_hors_des_murs_et_hors_du_hameau()
    {
        var story = JsonNode.Parse(File.ReadAllText(Path.Combine(DataDirectory, "world", "story.json")))!["data"]!;
        var merchants = JsonNode.Parse(File.ReadAllText(Path.Combine(DataDirectory, "world", "merchants.json")))!["data"]!;
        var places = new List<(string Where, double X, double Z)>();
        foreach (JsonNode? npc in story["npcs"]!.AsArray())
        {
            string? npcZone = (string?)npc!["zone"];
            void Add(string where, JsonNode? pos, string? zone)
            {
                if (pos is null || (zone ?? npcZone ?? "zone.vallons") != "zone.vallons") return;
                places.Add((where, (double)pos[0]!, (double)pos[1]!));
            }
            string id = (string)npc["id"]!;
            Add(id, npc["position"], null);
            foreach (JsonNode? slot in npc["schedule"]?.AsArray() ?? new JsonArray())
            {
                Add(id + "/" + (string)slot!["activity"]!, slot["position"], (string?)slot["zone"]);
                Add(id + "/pluie", slot["rain"]?["position"], (string?)slot["zone"]);
            }
        }
        foreach (JsonNode? festival in story["festivals"]!.AsArray())
            foreach (JsonNode? guest in festival!["guests"]?.AsArray() ?? new JsonArray())
                places.Add(((string)festival["id"]! + "/invité", (double)guest!["position"]![0]!, (double)guest["position"]![1]!));
        foreach (JsonNode? stall in merchants["market"]!["stalls"]!.AsArray())
            places.Add(((string)stall!["id"]!, (double)stall["position"]![0]!, (double)stall["position"]![1]!));
        Assert.True(places.Count > 60);
        // Rien au hameau (la forge de Maëlle, au bord du village en x = −24,2, n'a pas bougé).
        Assert.DoesNotContain(places, p => p.X >= -100 && p.X <= -26 && p.Z >= -24 && p.Z <= 19);
        // Dans la ville : jamais dans un mur.
        var inTown = places.Where(p => p.X < -1000).ToList();
        Assert.True(inTown.Count > 50, $"{inTown.Count} places dans la ville");
        Assert.All(inTown, p => Assert.False(Blocked(p.X, p.Z, 0.2), $"{p.Where} dans un mur en ({p.X}, {p.Z})"));
    }

    [Fact]
    public void Le_Grand_Solitaire_assiege_le_Bourg_a_sa_porte_de_l_Est()
    {
        // Revue Codex de la PR #190 : la cible « bourg » du boss du monde suit le bourg déménagé : la route d'approche longe la route d'Ambre
        // et les trois objectifs sont devant la porte de l'Est de la ville fortifiée (le boss n'entre pas dans les murs).
        var root = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(DataDirectory, "world", "worldbosses.json")))!;
        var target = root["data"]!["bosses"]![0]!["targets"]!.AsArray().Single(t => (string)t!["id"]! == "bourg")!;
        Assert.Equal("bourg", (string)target["place"]!);
        var points = target["route"]!.AsArray().Select(p => ((double)p![0]!, (double)p[1]!))
            .Concat(target["objectives"]!.AsArray().Select(o => ((double)o!["position"]![0]!, (double)o["position"]![1]!))).ToList();
        Assert.Equal(5, points.Count);
        Assert.All(points, p => Assert.InRange(p.Item1, -710, -300));   // hors des murs, à l'est de la palissade (x = −710)
        Assert.All(points.Skip(2), p => Assert.InRange(Math.Abs(p.Item1 - -710), 0, 20));   // devant la porte
        Assert.All(points, p => Assert.False(Blocked(p.Item1, p.Item2, 3), $"({p.Item1}, {p.Item2}) dans un obstacle"));
        Assert.Contains(points, p => p.Item2 > -60 && p.Item2 < -40);   // la porte elle-même
    }

    [Fact]
    public void De_la_place_du_marche_a_chaque_porte_a_pied()
    {
        CharacterProgression progression = ProgressionJsonReader.ReadFile(ProgressionJsonReader.PathIn(DataDirectory), ShippedData).Progression!;
        ItemCatalog items = ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!;
        SavedCharacter hero = Inventory.Outfit(PlayerCharacters.Create("heros", AdventureSession.PlayerClass, progression), items);
        WorldSession w = V228b2PassageTests.Open(Vallons.Value, new WorldSave("zone.vallons", 0, 0, new[] { PlayerCharacters.PlacesKey }, hero),
            safePoint: "place_du_marche_de_la_ville");
        // Par la rue du marché jusqu'à la place du bourg (coordonnées d'avant le déménagement, décalées du bloc).
        void Go(double x, double z) => V228b2PassageTests.WalkTo(w, x + DX, z + DZ);
        V228b2PassageTests.WalkTo(w, -1056, -33.5);
        Go(-58, -6);
        Assert.True(w.Registered("zone.vallons/safe/place_du_bourg"));
        foreach ((string doorId, string building) in Shops)
        {
            (double x, double z) = P(Vallons.Value.Doors.Single(d => d.Id == doorId).Position);
            x -= DX;
            z -= DZ;
            var b = Box(building);
            (double x0, double z0, double x1, double z1) = (b.X0 - DX, b.Z0 - DZ, b.X1 - DX, b.Z1 - DZ);
            // Devant la porte, à 1,2 m du mur où elle est.
            double ax = x, az = z;
            if (Math.Abs(z - z0) < 0.6) az = z0 - 1.2;
            else if (Math.Abs(z - z1) < 0.6) az = z1 + 1.2;
            else if (Math.Abs(x - x0) < 0.6) ax = x0 - 1.2;
            else ax = x1 + 1.2;
            double lane = az > -1 ? 1.5 : -4;   // allée nord ou sud de la grand-rue, entre le puits et les maisons
            Go(-58, -6);
            Go(-55, -4);
            Go(-55, lane);
            Go(ax, lane);
            Go(ax, az);
            Go(ax, lane);
            Go(-55, lane);
        }
    }

    [Fact]
    public void De_la_place_du_bourg_au_village_a_pied()
    {
        // Le retour : rue du marché, place du marché, porte de l'Est, route d'Ambre, grand-rue (le chemin des essais à l'envers).
        CharacterProgression progression = ProgressionJsonReader.ReadFile(ProgressionJsonReader.PathIn(DataDirectory), ShippedData).Progression!;
        ItemCatalog items = ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!;
        SavedCharacter hero = Inventory.Outfit(PlayerCharacters.Create("heros", AdventureSession.PlayerClass, progression), items);
        WorldSession w = V228b2PassageTests.Open(Vallons.Value, new WorldSave("zone.vallons", 0, 0, new[] { PlayerCharacters.PlacesKey }, hero),
            safePoint: "place_du_bourg");
        foreach ((double x, double z) in Enumerable.Reverse(RoadToBourg)) V228b2PassageTests.WalkTo(w, x, z);
        V228b2PassageTests.WalkTo(w, -20, -2);
        V228b2PassageTests.WalkTo(w, -18, -11);
        Assert.True(w.Registered("zone.vallons/safe/place_du_marche_de_la_ville"));
        Assert.True(w.Registered("zone.vallons/safe/place_du_village"));
    }

    [Fact]
    public void Partie_solo_du_village_au_bourg_et_retour_a_pied()
    {
        // Relecture du V2.30.1 : l'aller et retour en partie solo (passages de secteur, secteur rouvert au retour). Le joueur est suivi par
        // l'identifiant de son combattant dans la session du moment (« bastion », puis « bastion_pN » après un passage).
        string path = Path.Combine(Path.GetTempPath(), "eds_v2301_" + Guid.NewGuid().ToString("N"), "partie.json");
        try
        {
            AdventureSession a = AdventureSession.Load(DataDirectory, Path.Combine(AppContext.BaseDirectory, "scenarios"), path);
            a.Advance(0.05);
            bool Walk(double x, double z)
            {
                for (int i = 0; i < 4000; i++)
                {
                    (double px, double pz) = P(a.World.Zone.Engine.View(a.World.CombatantId).Position!.Value);
                    double dx = x - px, dz = z - pz;
                    if (Math.Sqrt(dx * dx + dz * dz) <= 1) return true;
                    a.Combat.SetMove(dx, dz);
                    a.Advance(0.05);
                }
                return false;
            }
            Assert.True(Walk(-20, -2));
            foreach ((double x, double z) in RoadToBourg) Assert.True(Walk(x, z), $"aller : bloqué avant ({x}, {z})");
            Assert.Equal("Place du Bourg", a.LastSafePoint);
            foreach ((double x, double z) in Enumerable.Reverse(RoadToBourg).Skip(1)) Assert.True(Walk(x, z), $"retour : bloqué avant ({x}, {z})");
            Assert.True(Walk(-18, -11));
            Assert.Equal("Place du village", a.LastSafePoint);
        }
        finally
        {
            string dir = Path.GetDirectoryName(path)!;
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Panneaux_aux_croisements_du_village_au_Bourg()
    {
        // Décision de Damien du 16 novembre 2026 : des panneaux aux croisements entre le village et le Bourg.
        Assert.Equal("Bourg d'Aube : est, 1,0 km", AdventureSession.WaySignPart("Bourg d'Aube", 0, 0, 1000, 0));
        Assert.Equal("Village : nord, 500 m", AdventureSession.WaySignPart("Village", 0, 0, 0, 500));
        Assert.Equal("Village : sud-ouest, 140 m", AdventureSession.WaySignPart("Village", 0, 0, -100, -100));
        Assert.Equal("Village : ici", AdventureSession.WaySignPart("Village", 0, 0, 5, 5));
        string path = Path.Combine(Path.GetTempPath(), "eds_v2301_" + Guid.NewGuid().ToString("N"), "partie.json");
        try
        {
            AdventureSession a = LoadAtBourg(path);
            IReadOnlyList<WorldMarker> signs = a.WaySigns();
            Assert.Equal(new[] { "panneau_hameau", "panneau_route_d_ambre", "panneau_porte_est", "panneau_place_du_marche" }, signs.Select(s => s.Id));
            Assert.Equal("Bourg d'Aube : ouest, 400 m · Village : est, 690 m", signs.Single(s => s.Id == "panneau_porte_est").Name);   // V2.30.5 : poteau déplacé de 3 m vers l'est, hors du fossé
            Assert.All(signs, s => Assert.Matches("^Bourg d'Aube : [a-z-]+, [0-9,]+ k?m · Village : [a-z-]+, [0-9,]+ k?m$", s.Name));
            ZoneDefinition zone = Vallons.Value;
            foreach (WorldMarker s in signs)
            {
                // Au bord de la route, jamais dessus ; un poteau seul (rien d'autre à moins d'un mètre).
                var roads = AdventureSession.Roads(zone);
                Assert.InRange(TownRules.RoadAt(s.X, s.Z, roads).Edge, 0.5, 4);
                Assert.DoesNotContain(zone.Setup.Space!.Obstacles, o => o.Id != s.Id && Inside(Box(o.Id), s.X, s.Z, 1));
            }
            Assert.Empty(LoadInside(path).WaySigns());
        }
        finally
        {
            string dir = Path.GetDirectoryName(path)!;
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    private static AdventureSession LoadInside(string path)
    {
        EdS.World.SaveStore.Write(path, new WorldSave("zone.auberge", 0, 0, Array.Empty<string>()));
        var a = AdventureSession.Load(DataDirectory, Path.Combine(AppContext.BaseDirectory, "scenarios"), path);
        a.Advance(0.05);
        return a;
    }

    [Fact]
    public void Une_partie_sauvegardee_a_la_place_du_bourg_reprend_sur_la_nouvelle_place()
    {
        // Sauvegarde d'avant le déménagement : même point sûr, ancien secteur (le secteur n'est pas comparé, le point sûr fait foi).
        CharacterProgression progression = ProgressionJsonReader.ReadFile(ProgressionJsonReader.PathIn(DataDirectory), ShippedData).Progression!;
        var save = new WorldSave("zone.vallons", 1200, 3, new[] { "zone.vallons/safe/place_du_bourg" }, null, false, null,
            new SavedResume("place_du_bourg", "s14_12"));
        (string? point, GroundVector? place) = WorldSession.ResumeArrival(save, Vallons.Value, progression);
        Assert.Equal("place_du_bourg", point);
        (double x, double z) = place is GroundVector g ? P(g) : P(Vallons.Value.SafePoints.Single(s => s.Id == "place_du_bourg").Position);
        Assert.InRange(x, -58 + DX - 8, -58 + DX + 8);
        Assert.InRange(z, -6 + DZ - 8, -6 + DZ + 8);
    }
}
