using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EdS.Data.Json;
using EdS.Data.Rebuilds;
using EdS.Data.Saves;
using EdS.Data.Zones;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.World;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Lieux reconstruits, sous-lot V2.43.3 (décor selon l'état) : pièces de chaque état (rien en ruine ; échafaudage, tas de bois qui se vide,
/// chariot vide, éclats et outils en chantier ; chariot chargé, caisse et tonneau une fois reconstruit), posées de façon déterministe dans
/// chacun des cinq lieux, hors des obstacles et sans se chevaucher.
/// </summary>
[Trait("Category", "World")]
public class DecorDesChantiersTests
{
    private static readonly Lazy<ZoneDefinition> Vallons = new(() => Zone());
    private static readonly Lazy<RebuildBook> Book5 = new(() =>
        RebuildJsonReader.ReadFile(RebuildJsonReader.PathIn(DataDirectory), Vallons.Value, ItemsCatalog, Book(Vallons.Value)).Book!);

    [Fact]
    public void Pieces_de_chaque_etat()
    {
        Assert.Empty(RebuildDecor.PiecesFor(RebuildPhase.Ruin, 0));
        Assert.Equal(new[] { RebuildDecor.Scaffold, RebuildDecor.WoodFull, RebuildDecor.EmptyCart, RebuildDecor.Chips, RebuildDecor.Tools },
            RebuildDecor.PiecesFor(RebuildPhase.Building, 0));
        Assert.Contains(RebuildDecor.WoodHalf, RebuildDecor.PiecesFor(RebuildPhase.Building, 1));
        Assert.Contains(RebuildDecor.WoodLow, RebuildDecor.PiecesFor(RebuildPhase.Building, 2));
        Assert.Equal(new[] { RebuildDecor.LoadedCart, RebuildDecor.Crate, RebuildDecor.Barrel }, RebuildDecor.PiecesFor(RebuildPhase.Rebuilt, 3));
        Assert.DoesNotContain(RebuildDecor.Scaffold, RebuildDecor.PiecesFor(RebuildPhase.Rebuilt, 3));   // l'échafaudage part
    }

    /// <summary>Une aventure sur les données livrées, la sauvegarde portant les chantiers <paramref name="rebuilds"/>.</summary>
    private static AdventureSession Adventure(SavedRebuilds rebuilds)
    {
        string folder = Path.Combine(Path.GetTempPath(), "eds_chantiers_" + Guid.NewGuid().ToString("N"));
        string savePath = Path.Combine(folder, "sauvegarde.json");
        Directory.CreateDirectory(folder);
        SaveStore.Write(savePath, new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(),
            PlayerCharacters.Create(AdventureSession.PlayerId, AdventureSession.PlayerClass, Progression).WithRebuilds(rebuilds)));
        AdventureSession a = AdventureSession.Load(DataDirectory, ScenariosDirectory, savePath);
        a.DrainMessages();
        return a;
    }

    private static readonly SavedRebuilds Mixed = new(new[]
    {
        new SavedRebuild("hameau", 0, new[] { new KeyValuePair<string, long>("or", 1) }),
        new SavedRebuild("pont_effondre", 3, done: true),
        new SavedRebuild("moulin_a_foulon", 1),
        new SavedRebuild("tour_de_signal", 2, done: true),
        new SavedRebuild("dortoir_de_l_abbaye", 2),
    });

    [Fact]
    public void Decor_des_cinq_lieux_dans_la_partie_selon_les_memes_regles_que_les_fetes()
    {
        AdventureSession a = Adventure(Mixed);
        Assert.Equal("hameau:1:0;pont_effondre:2:3;moulin_a_foulon:1:1;tour_de_signal:2:2;dortoir_de_l_abbaye:1:2", a.RebuildDecorKey);
        IReadOnlyList<DecorSpot> spots = a.RebuildSpots();
        // Chaque lieu a tout son décor (chantier ou reconstruit), dans son lieu.
        var states = new[] { ("hameau", 0, RebuildPhase.Building), ("pont_effondre", 3, RebuildPhase.Rebuilt), ("moulin_a_foulon", 1, RebuildPhase.Building),
            ("tour_de_signal", 2, RebuildPhase.Rebuilt), ("dortoir_de_l_abbaye", 2, RebuildPhase.Building) };
        var expected = states.SelectMany(e => RebuildDecor.Landmark(Book5.Value.Of(e.Item1)!, e.Item3, a.RegionMap.Whole.Obstacles).Spots.Select(s => s.Piece)
            .Concat(RebuildDecor.PiecesFor(e.Item3, e.Item2))).ToList();
        Assert.Equal(expected, spots.Select(s => s.Piece));
        // Le grand décor de chaque lieu (20 novembre 2026) : pont reconstruit, palan au moulin, tour restaurée qui remplace l'ancienne, dortoir en chantier.
        Assert.Equal(new[] { "pont_reconstruit", "palan_et_pierres", "tour_de_signal_restauree", "dortoir_en_chantier" },
            spots.Where(s => RebuildDecor.Models.ContainsKey(s.Piece) && s.Piece != RebuildDecor.Scaffold).Select(s => s.Piece));
        Assert.Equal(new[] { "tour_de_signal_bergers" }, a.RebuildCovered());
        DecorSpot bridge = spots.Single(s => s.Piece == "pont_reconstruit");
        Assert.Equal((2000.0, 176.0, 0.0), (bridge.X, bridge.Z, bridge.Yaw));   // au milieu du ravin, sa longueur d'une rive à l'autre
        // Même espace que le jeu (aire entière), mêmes règles que le décor des fêtes : obstacles, rues, portes, PNJ, points sûrs.
        AdventureMap map = a.RegionMap.Whole;
        var obstacles = RebuildDecor.Polygons(map.Obstacles);
        var doors = map.Doors.Select(d => (d.X, d.Z)).ToList();
        var avoid = a.FeastAvoid();
        var none = new List<IReadOnlyList<(double X, double Z)>>();
        var landmarks = spots.Where(s => RebuildDecor.Models.ContainsKey(s.Piece) && s.Piece != RebuildDecor.Scaffold).Select(RebuildDecor.BoxOf).ToList();
        foreach (DecorSpot s in spots.Where(s => !RebuildDecor.Models.ContainsKey(s.Piece) || s.Piece == RebuildDecor.Scaffold))
        {
            double side = 2 * RebuildDecor.Radius(s.Piece);
            Assert.True(FeastLooks.Fits(s.X, s.Z, s.Yaw, side, side, none, landmarks, Array.Empty<StyleRoad>(), Array.Empty<(double, double)>(),
                Array.Empty<(double, double)>()), $"{s.Piece} sur le grand décor");
            Assert.True(FeastLooks.Fits(s.X, s.Z, s.Yaw, side, side, obstacles, none, FeastLooks.Streets(map.Roads)!, doors, avoid), $"{s.Piece} ({s.X}, {s.Z})");
        }
        var small = spots.Where(s => !RebuildDecor.Models.ContainsKey(s.Piece) || s.Piece == RebuildDecor.Scaffold).ToList();
        for (int i = 0; i < small.Count; i++)
            for (int j = i + 1; j < small.Count; j++)
                Assert.True(Math.Sqrt(Math.Pow(small[i].X - small[j].X, 2) + Math.Pow(small[i].Z - small[j].Z, 2))
                    >= RebuildDecor.Radius(small[i].Piece) + RebuildDecor.Radius(small[j].Piece), $"{small[i].Piece} {small[j].Piece}");
        Assert.Equal(spots, a.RebuildSpots());   // déterministe
    }

    [Fact]
    public void La_cle_change_au_palier_et_rien_sans_catalogue_ni_hors_de_sa_zone()
    {
        AdventureSession ruin = Adventure(SavedRebuilds.Empty);
        Assert.Equal("hameau:0:0;pont_effondre:0:0;moulin_a_foulon:0:0;tour_de_signal:0:0;dortoir_de_l_abbaye:0:0", ruin.RebuildDecorKey);
        Assert.Empty(ruin.RebuildSpots());   // tout en ruine : rien de plus
        string before = Adventure(Mixed).RebuildDecorKey;
        var next = new SavedRebuilds(Mixed.Entries.Where(e => e.Site != "moulin_a_foulon").Append(new SavedRebuild("moulin_a_foulon", 2, done: true)));
        Assert.NotEqual(before, Adventure(next).RebuildDecorKey);   // un palier de plus : le décor est refait
        // Sans catalogue : ni clé ni décor.
        ruin.World.UseRebuilds(null);
        Assert.Equal(string.Empty, ruin.RebuildDecorKey);
        Assert.Empty(ruin.RebuildSpots());
        // Un catalogue d'une autre zone (l'auberge) : rien dans les Vallons.
        ZoneDefinition auberge = V16TestData.Interiors.Single(z => z.Id == "zone.auberge");
        RebuildLoadResult other = RebuildJsonReader.Read("{\"schemaVersion\": 1, \"kind\": \"rebuilds\", \"contentVersion\": \"0.1.0\", \"data\": {\"sites\": ["
            + "{\"id\": \"auberge\", \"place\": \"auberge\", \"stages\": [{\"id\": \"a\", \"needs\": [{\"id\": \"or\", \"coins\": 1}]}]}]}}",
            auberge, ItemsCatalog, Book(Vallons.Value));
        Assert.True(other.IsValid, other.Report.ToString());
        ruin.World.UseRebuilds(other.Book);
        Assert.Equal(string.Empty, ruin.RebuildDecorKey);
        Assert.Empty(ruin.RebuildSpots());
    }

    [Fact]
    public void Un_obstacle_ecarte_les_pieces_et_rien_en_ruine()
    {
        RebuildSite hameau = Book5.Value.Of("hameau")!;
        var space = RebuildDecor.Polygons(Vallons.Value.Setup.Space!.Obstacles);
        Assert.Empty(RebuildDecor.Layout(hameau, RebuildPhase.Ruin, 0, space));
        // Un obstacle qui couvre tout le lieu et sa marge : aucune place libre, rien n'est posé (jamais dans un mur).
        var m = hameau.Margin + EdS.Domain.Numerics.Fixed.FromInt(5);
        var wall = new SpaceObstacle("mur", new[]
        {
            new GroundVector(hameau.Place.Min.X - m, hameau.Place.Min.Z - m), new GroundVector(hameau.Place.Max.X + m, hameau.Place.Min.Z - m),
            new GroundVector(hameau.Place.Max.X + m, hameau.Place.Max.Z + m), new GroundVector(hameau.Place.Min.X - m, hameau.Place.Max.Z + m),
        });
        Assert.Empty(RebuildDecor.Layout(hameau, RebuildPhase.Building, 0, RebuildDecor.Polygons(new[] { wall })));
        // Un PNJ au milieu des places candidates : aucune pièce ne le couvre.
        IReadOnlyList<DecorSpot> free = RebuildDecor.Layout(hameau, RebuildPhase.Building, 0, space);
        var person = (free[0].X, free[0].Z);
        IReadOnlyList<DecorSpot> moved = RebuildDecor.Layout(hameau, RebuildPhase.Building, 0, space, avoid: new[] { person });
        Assert.All(moved, s => Assert.True(Math.Sqrt(Math.Pow(s.X - person.X, 2) + Math.Pow(s.Z - person.Z, 2)) >= RebuildDecor.Radius(s.Piece), s.Piece));
    }

    [Fact]
    public void Modeles_des_chantiers_dans_le_depot()
    {
        // 20 novembre 2026 : les 11 modèles Tripo des lieux reconstruits, chacun avec sa matière (« _couleur », « _normale »), au poids surveillé.
        string folder = Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Modeles", "Decor", "chantiers");
        Assert.True(Directory.Exists(folder), folder);
        long total = 0;
        foreach (string piece in RebuildDecor.Models.Keys)
        {
            string path = "Assets/EdS/Modeles/Decor/chantiers/chantiers_" + piece + ".fbx";
            Assert.Equal("chantiers", AtelierDecor.OwnDecorOf(path));
            foreach (string file in new[] { piece + ".fbx", piece + "_couleur.jpg", piece + "_normale.png" })
            {
                var info = new FileInfo(Path.Combine(folder, "chantiers_" + file));
                Assert.True(info.Exists, info.Name);
                total += info.Length;
            }
        }
        Assert.Equal(RebuildDecor.Models.Count * 3, Directory.GetFiles(folder).Count(f => !f.EndsWith(".meta", StringComparison.Ordinal)));
        Assert.InRange(total, 1, 24L * 1024 * 1024);   // 18 Mo livrés : au plus 24 Mo pour les onze
    }
}
