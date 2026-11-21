using System.Text.Json.Nodes;
using EdS.Data.Characters;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.World;
using static EdS.Data.Tests.GrandeCarteLieux;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Chantier V2.46 (PLAN_GRANDE_CARTE.md) : la grotte derrière la cascade, premier vrai dessous. Une zone à part (`floor: "dessous"`)
/// reliée au pied de la cascade par une porte, ses créatures du bestiaire existant (échos, ours) à 8 m au moins de la porte, son gardien
/// (un grand ours à deux formes) qui donne l'anneau de la cascade à sa première défaite, et son rendu (pénombre, ni pluie ni neige,
/// pièces du kit de grotte par leur nom).
/// </summary>
[Trait("Category", "World")]
public class V246GrotteTests
{
    private static readonly string Scenarios = Path.Combine(AppContext.BaseDirectory, "scenarios");

    private static ItemCatalog Items => ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!;

    private static CharacterProgression Progression => ProgressionJsonReader.ReadFile(ProgressionJsonReader.PathIn(DataDirectory), ShippedData).Progression!;

    private static JsonObject ZoneJson() => JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.grotte_cascade")))!.AsObject();

    private static ZoneLoadResult Read(JsonObject root) => ZoneJsonReader.Read(root.ToJsonString(), Scenarios, ShippedData, Items);

    private static readonly Lazy<ZoneDefinition> Grotte = new(() =>
    {
        ZoneLoadResult r = Read(ZoneJson());
        Assert.True(r.IsValid, r.Report.ToString());
        return r.Zone!;
    });

    /// <summary>Point dans le polygone de l'obstacle (rayon, bords compris).</summary>
    private static bool Inside(SpaceObstacle o, double x, double z)
    {
        var pts = o.Points.Select(P).ToList();
        bool inside = false;
        for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
        {
            if (((pts[i].Z > z) != (pts[j].Z > z)) && x < (pts[j].X - pts[i].X) * (z - pts[i].Z) / (pts[j].Z - pts[i].Z) + pts[i].X) inside = !inside;
        }
        return inside;
    }

    [Fact]
    public void Grotte_livree_un_dessous_relie_au_pied_de_la_cascade()
    {
        ZoneDefinition grotte = Grotte.Value;
        Assert.Equal(15, grotte.SchemaVersion);
        Assert.True(grotte.Underground && grotte.Sheltered && !grotte.Indoor);
        Assert.Equal(new[] { "echo_grotte1", "echo_grotte2", "ours_grotte1" }, grotte.Populations.Select(p => p.Creature));
        Assert.Equal(new[] { (10, "salle_du_rideau"), (12, "salle_du_bassin") }, grotte.Places.Select(p => (p.MinLevel, p.Id)));
        Door sortie = Assert.Single(grotte.Doors);
        Assert.Equal(("zone.vallons", "porte_grotte_cascade", DoorKind.Door), (sortie.To, sortie.ToDoor, sortie.Kind));

        // Côté surface : la porte est au pied de la falaise de la cascade, dans le lieu « cascade », hors de tout obstacle, à quelques
        // mètres du point sûr du pied de la cascade.
        ZoneDefinition vallons = Vallons.Value;
        Door entree = vallons.DoorOf("porte_grotte_cascade")!;
        Assert.Equal(("zone.grotte_cascade", "sortie", DoorKind.Door), (entree.To, entree.ToDoor, entree.Kind));
        (double x, double z) = P(entree.Position);
        Place cascade = vallons.Places.Single(p => p.Id == "cascade");
        Assert.InRange(x, P(cascade.Min).X, P(cascade.Max).X);
        Assert.InRange(z, P(cascade.Min).Z, P(cascade.Max).Z);
        Assert.DoesNotContain(vallons.Setup.Space!.Obstacles, o => Inside(o, x, z));
        Assert.True(Walks((540, 1000), (640, 1095), SafePoint("pied_de_la_cascade"), (x, z)));   // on y va à pied depuis le point sûr

        // Les deux zones se répondent (porte et genre), avec les autres zones reliées.
        Assert.True(ZoneValidator.CheckDoors(new[] { vallons, grotte }.Concat(V16TestData.Interiors.Where(z => z.Id != grotte.Id)).ToArray()).IsValid);
        Assert.Contains(V16TestData.Interiors, z => z.Id == "zone.grotte_cascade");
    }

    [Fact]
    public void De_la_porte_au_gardien_a_pied()
    {
        ZoneDefinition grotte = Grotte.Value;
        var space = grotte.Setup.Space!;
        var flood = V228cSurfaceTests.Flood(space.Obstacles.Where(o => !o.Low), (-20, -4), (20, 32), P(grotte.SafePoints[0].Position), 0.5);
        foreach (var p in grotte.Populations.Select(p => grotte.Setup.Combatants.Single(c => c.Id == p.Creature).Placement!.Position))
            Assert.True(V228cSurfaceTests.Reaches(flood.Reached, (-20, -4), P(p), 0.5));
        Assert.True(V228cSurfaceTests.Reaches(flood.Reached, (-20, -4), (0, 26), 0.5));   // la salle du gardien
        // Chaque obstacle de la grotte prend une pièce du kit (aucun rocher sans modèle prévu).
        Assert.All(space.Obstacles, o => Assert.NotEqual(CavePiece.None, CaveLooks.PieceFor(o.Id)));
    }

    [Fact]
    public void Gardien_de_la_grotte_et_anneau_de_la_cascade()
    {
        Boss gardien = Assert.Single(Grotte.Value.Chiefs);
        Assert.Equal(("gardien_de_la_grotte", "fact.gardien_de_la_grotte.vaincu", "loot.ours"), (gardien.Id, gardien.Fact, gardien.Loot));
        Assert.Equal(new[] { "gardien_de_la_grotte", "gardien_de_la_grotte_furieux" }, gardien.Forms.Select(f => f.Id));
        Assert.Equal(("item.anneau_de_la_cascade", 1), Assert.Single(gardien.Rewards));
        ItemDefinition anneau = Items.Item("item.anneau_de_la_cascade");
        Assert.Equal((ItemType.Equipment, "ring", 12, ItemRarity.Rare, true), (anneau.Type, anneau.Slot, anneau.RequiredLevel, anneau.Rarity, anneau.Named));
        Assert.Equal((60, 25), (anneau.Bonus.MaxHealth, anneau.Bonus.Armor));
        Assert.Equal(1200, V11TestData.ShippedMerchants.Values["item.anneau_de_la_cascade"]);
    }

    [Theory]
    [InlineData("populations", 0, 0.0, 4.0)]     // un écho juste devant la porte (moins de 8 m) : refusé
    [InlineData("populations", 2, 5.0, 2.0)]
    public void Creature_trop_pres_de_la_porte_refusee_avec_son_chemin(string list, int index, double x, double z)
    {
        // Adversarial : la créature est déplacée dans le scénario ; la zone qui la peuple est refusée, au chemin de la population.
        string dir = Path.Combine(Path.GetTempPath(), "eds_v246_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "zones"));
        try
        {
            JsonObject zone = ZoneJson();
            string creature = (string)zone["data"]![list]![index]!["creature"]!;
            JsonObject s = JsonNode.Parse(File.ReadAllText(ScenarioPath("zones/grotte_cascade")))!.AsObject();
            JsonObject c = s["data"]!["combatants"]!.AsArray().Single(n => (string)n!["id"]! == creature)!.AsObject();
            c["position"] = new JsonArray(x, z);
            File.WriteAllText(Path.Combine(dir, "zones", "grotte_cascade.json"), s.ToJsonString());
            File.Copy(ScenarioPath("zones/grotte_cascade_gardien"), Path.Combine(dir, "zones", "grotte_cascade_gardien.json"));
            ZoneLoadResult r = ZoneJsonReader.Read(zone.ToJsonString(), dir, ShippedData, Items);
            Assert.False(r.IsValid);
            Assert.Contains(r.Report.Errors, e => e.Path == $"$.data.populations[{index}].creature" && e.Message.Contains("porte « sortie »", StringComparison.Ordinal));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Frontiere_huit_metres_exactement_passe()
    {
        string dir = Path.Combine(Path.GetTempPath(), "eds_v246_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "zones"));
        try
        {
            JsonObject zone = ZoneJson();
            JsonObject s = JsonNode.Parse(File.ReadAllText(ScenarioPath("zones/grotte_cascade")))!.AsObject();
            // La porte est en (0, −2,5) : un écho en (0, 5,5) est à 8 m pile (passe), en (0, 5,4999) à moins (refusé).
            JsonObject echo = s["data"]!["combatants"]!.AsArray().Single(n => (string)n!["id"]! == "echo_grotte1")!.AsObject();
            File.Copy(ScenarioPath("zones/grotte_cascade_gardien"), Path.Combine(dir, "zones", "grotte_cascade_gardien.json"));
            echo["position"] = new JsonArray(0.0, 5.5);
            File.WriteAllText(Path.Combine(dir, "zones", "grotte_cascade.json"), s.ToJsonString());
            Assert.True(ZoneJsonReader.Read(zone.ToJsonString(), dir, ShippedData, Items).IsValid);
            echo["position"] = new JsonArray(0.0, 5.4999);
            File.WriteAllText(Path.Combine(dir, "zones", "grotte_cascade.json"), s.ToJsonString());
            Assert.False(ZoneJsonReader.Read(zone.ToJsonString(), dir, ShippedData, Items).IsValid);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Grotte_marquee_interieur_refusee()
    {
        JsonObject zone = ZoneJson();
        zone["data"]!["indoor"] = true;
        ZoneLoadResult r = Read(zone);
        Assert.False(r.IsValid);
        Assert.Contains(r.Report.Errors, e => e.Path == "$.data.indoor");
    }

    [Fact]
    public void Dans_la_grotte_ni_cheval_ni_voyage_rapide_vers_elle_creatures_presentes()
    {
        ZoneDefinition grotte = Grotte.Value;
        var w = new WorldSession(new WorldSave(grotte.Id, 0, 0, Array.Empty<string>(),
            PlayerCharacters.Create("bastion", "class.bastion", Progression).WithMount(PlayerCharacters.Horse)), grotte, ShippedData, Progression, Items);
        w.Step();
        Assert.False(w.CanCallMount);
        Assert.True(w.IsDiscovered(grotte, "rideau_d_eau"));
        Assert.Equal(TravelRefusal.UnknownPlace, w.CanTravel(grotte, "rideau_d_eau"));
        Assert.All(grotte.Populations, p => Assert.True(w.Zone.Engine.View(p.Creature).IsPresent));
    }

    [Fact]
    public void Carte_d_un_dessous_et_cle_distincte()
    {
        var space = Grotte.Value.Setup.Space!;
        var under = new AdventureMap(space, Array.Empty<MapPlace>(), underground: true);
        var surface = new AdventureMap(space, Array.Empty<MapPlace>());
        Assert.True(under.Underground && !under.Indoor && !surface.Underground);
        Assert.NotEqual(under.Signature, surface.Signature);
        Assert.False(new AdventureMap(space, Array.Empty<MapPlace>(), indoor: true, underground: true).Underground);   // jamais les deux
        Assert.True(GroundRelief.For(under).IsFlat);   // sol plat, pas de relief de surface
    }

    [Fact]
    public void Partie_reprise_dans_la_grotte_a_l_abri_et_carte_de_dessous()
    {
        string dir = Path.Combine(Path.GetTempPath(), "eds_v246_" + Guid.NewGuid().ToString("N"));
        string save = Path.Combine(dir, "partie", "sauvegarde.json");
        Directory.CreateDirectory(Path.GetDirectoryName(save)!);
        try
        {
            SaveStore.Write(save, new WorldSave("zone.grotte_cascade", 0, 2, new[] { "zone.vallons/safe/pied_de_la_cascade" }, V11TestData.Hero(5)));
            AdventureSession a = V11TestData.Adventure(save);
            Assert.True(a.Underground && a.Sheltered && !a.Indoors);   // à l'abri : ni pluie, ni neige, ni glace à l'affichage
            Assert.True(a.Map.Underground && !a.Map.Indoor);
            Assert.True(a.Mood().Indoors);                                // ambiance sonore à l'abri
            Assert.DoesNotContain(a.TravelDestinations(), d => d.Zone == "zone.grotte_cascade");
            Assert.Equal(TravelRefusal.None, a.Travel("zone.vallons", "pied_de_la_cascade"));   // on en part par le voyage rapide
            Assert.False(a.Underground || a.Sheltered);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Unity_grotte_dessinee_et_meteo_a_l_abri()
    {
        static string Game(string name) => File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", name));
        string game = Game("EdsGame.cs"), space = Game("SpaceView.cs"), cave = Game("SpaceView.Cave.cs"), style = Game("StyleWorld.cs");
        Assert.Contains("_camera.Camera.transform.position, a.Sheltered,", game);              // la pluie : ni dans un dessous
        Assert.Contains("_snowCover.Sync(Time.deltaTime, a.Sheltered,", game);
        Assert.Contains("a.InScene || a.Sheltered ? 0f : Time.deltaTime", game);              // ni les bêtes du dehors
        Assert.True(space.IndexOf("map.Underground", StringComparison.Ordinal) < space.IndexOf("map != null && map.Indoor)", StringComparison.Ordinal));
        Assert.Contains("if (_cave) StyleWorld.SetCave(false);", space);                        // la sortie rend le ciel
        Assert.Contains("Resources.Load<GameObject>(CaveLooks.Folder + name)", cave);           // modèles par leur nom
        Assert.Contains("CaveModel(CaveLooks.CurtainModel)", cave);
        Assert.Contains("RenderSettings.fogEndDistance = (float)CaveLooks.FogEnd;", style);
    }

    /// <summary>
    /// Kit de grotte livré (décision de Damien du 21 novembre 2026 : 240 crédits validés) : les huit modèles finis dans Blender sont dans
    /// Resources/Grotte, chacun avec sa couleur et sa normale, et reconnus par leur nom ; la préparation leur donne leur matière.
    /// </summary>
    [Fact]
    public void Kit_de_grotte_present_et_reconnu()
    {
        string folder = Path.Combine(Lot93UnityPackageTests.Root, "unity", CaveLooks.AssetFolder);
        foreach (string name in CaveLooks.Models)
        {
            string fbx = Path.Combine(folder, name + ".fbx");
            Assert.True(File.Exists(fbx), fbx);
            Assert.True(new FileInfo(fbx).Length > 10_000, fbx);
            Assert.True(File.Exists(Path.Combine(folder, name + "_couleur.jpg")), name);
            Assert.True(File.Exists(Path.Combine(folder, name + "_normale.png")), name);
            Assert.Equal(name, CaveLooks.NameOf(CaveLooks.AssetFolder + "/" + name + ".fbx"));
        }
        Assert.Equal(CaveLooks.Models.Count, Directory.GetFiles(folder, "*.fbx").Length);   // rien d'autre, rien d'oublié
        Assert.Equal("Grotte/", CaveLooks.Folder);
        Assert.EndsWith("Resources/" + CaveLooks.Folder.TrimEnd('/'), CaveLooks.AssetFolder);   // Resources.Load(Folder + nom) trouve le fichier
        Assert.Contains("CavePieces(notes);", File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Editor", "EdsStyle.cs")));
    }

    [Theory]
    [InlineData("Assets/EdS/Modeles/Resources/Grotte/paroi_grotte.fbx", "paroi_grotte")]
    [InlineData("Assets\\EdS\\Modeles\\Resources\\Grotte\\coffre_grotte.FBX", "coffre_grotte")]
    [InlineData("Assets/EdS/Modeles/Resources/Grotte/sous/paroi_grotte.fbx", null)]
    [InlineData("Assets/EdS/Modeles/Resources/Grotte/paroi_grotte.glb", null)]
    [InlineData("Assets/EdS/Modeles/Resources/Grotte/paroi.fbx", null)]
    [InlineData("Assets/EdS/Modeles/Caravanes/paroi_grotte.fbx", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Nom_d_un_modele_du_kit(string? path, string? name) => Assert.Equal(name, CaveLooks.NameOf(path!));

    [Theory]
    [InlineData("paroi_grotte_ouest", CavePiece.Wall)]
    [InlineData("pilier_grotte", CavePiece.Pillar)]
    [InlineData("stalagmite_grotte_5", CavePiece.Stalagmite)]
    [InlineData("bassin_grotte", CavePiece.Pool)]
    [InlineData("cristaux_grotte_2", CavePiece.Crystals)]
    [InlineData("eboulis_grotte_1", CavePiece.Rubble)]
    [InlineData("coffre_grotte", CavePiece.Chest)]
    [InlineData("paroi_grottex", CavePiece.None)]          // le préfixe doit être suivi de « _ » ou de rien
    [InlineData("_paroi_grotte", CavePiece.None)]
    [InlineData("falaise_cascade", CavePiece.None)]
    [InlineData("", CavePiece.None)]
    [InlineData(null, CavePiece.None)]
    public void Pieces_du_kit_par_le_nom_de_l_obstacle(string? id, CavePiece piece)
    {
        Assert.Equal(piece, CaveLooks.PieceFor(id!));
        if (piece == CavePiece.None) Assert.Null(CaveLooks.ModelFor(piece));
        else Assert.Contains(CaveLooks.ModelFor(piece), CaveLooks.Models);
    }

    [Fact]
    public void Kit_de_huit_modeles_et_penombre()
    {
        Assert.Equal(8, CaveLooks.Models.Distinct().Count());
        Assert.Equal(CaveLooks.CurtainModel, CaveLooks.Models[3]);
        // Pénombre : plus sombre que l'ambiance par défaut d'une pièce (0,2 ; 0,16 ; 0,13), brume dans l'aire.
        Assert.True(CaveLooks.Ambient.R < 0.1 && CaveLooks.Ambient.G < 0.1 && CaveLooks.Ambient.B < 0.1);
        Assert.True(CaveLooks.FogStart > 0 && CaveLooks.FogStart < CaveLooks.FogEnd && CaveLooks.FogEnd <= 40);
    }

    [Theory]
    [InlineData(0.0, 1.0, 1.0)]
    [InlineData(-3.0, 0.6, 0.6)]
    [InlineData(14.0, 1.0, 0.0)]          // à la portée : éteint
    [InlineData(40.0, 1.0, 0.0)]
    [InlineData(7.0, 1.0, 0.25)]          // à mi-portée : un quart (décroissance au carré)
    [InlineData(3.0, 0.0, 0.0)]           // la nuit : l'entrée est noire
    [InlineData(0.0, 2.0, 1.0)]           // lumière bornée
    [InlineData(0.0, -1.0, 0.0)]
    [InlineData(double.NaN, 1.0, 0.0)]
    [InlineData(1.0, double.NaN, 0.0)]
    public void Jour_du_rideau_d_eau(double distance, double daylight, double expected)
    {
        Assert.Equal(expected, CaveLooks.CurtainLight(distance, daylight), 6);
    }

    [Fact]
    public void Jour_du_rideau_decroit_avec_la_distance()
    {
        double last = double.MaxValue;
        for (double d = 0; d <= CaveLooks.CurtainReach + 1; d += 0.5)
        {
            double v = CaveLooks.CurtainLight(d, 1);
            Assert.InRange(v, 0, 1);
            Assert.True(v <= last);
            last = v;
        }
    }
}
