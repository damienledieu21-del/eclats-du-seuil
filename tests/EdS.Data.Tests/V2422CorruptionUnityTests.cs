using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using EdS.Data.Corruption;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Zones;
using EdS.Runtime;
using EdS.World;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.42.2 : la corruption dans Unity (affichage seulement). Décor des terres corrompues posé par <see cref="CorruptionLooks.Layout"/>
/// (déterministe, plus dense à chaque palier, dans le disque, à l'écart de l'autel), autel corrompu ou purifié, modèles prévus branchés par
/// leur nom (liste de l'atelier, rien de lancé), formes de remplacement et branchements des scripts Unity.
/// </summary>
[Trait("Category", "World")]
public class V2422CorruptionUnityTests
{
    private static readonly WorldClock Clock = new(50);
    private static readonly ZoneDefinition Vallons = Zone(1421, -670);
    private static readonly CorruptionBook Book = CorruptionJsonReader.ReadFile(CorruptionJsonReader.PathIn(DataDirectory), Vallons, ItemsCatalog).Book!;

    private static IReadOnlyList<CorruptionView> Views(long day, SavedCharacter? hero = null)
    {
        var w = new WorldSession(new WorldSave("zone.vallons", WorldEventSchedule.TickAt(Clock, day * 1440 + 12 * 60), 0, new[] { PlayerCharacters.PlacesKey },
            hero ?? Hero()), Vallons, ShippedData, Progression, ItemsCatalog, V11TestData.Book(Vallons), safePoint: "place_du_village");
        w.UseCorruption(Book);
        w.Step();
        return w.Corruption;
    }

    private static double Distance2(double ax, double az, double bx, double bz) => (ax - bx) * (ax - bx) + (az - bz) * (az - bz);

    [Fact]
    public void Decor_deterministe_plus_dense_a_chaque_palier()
    {
        IReadOnlyList<CorruptionView> day3 = Views(3), day9 = Views(9);
        CorruptionView ouest3 = day3.Single(v => v.Area.Id == "pierres_ouest"), ouest9 = day9.Single(v => v.Area.Id == "pierres_ouest");
        Assert.Equal((1, 3), (ouest3.Stage, ouest9.Stage));
        IReadOnlyList<CorruptionPiece> few = CorruptionLooks.Layout(ouest3), many = CorruptionLooks.Layout(ouest9);
        // L'autel d'abord, puis 12 pièces par palier.
        Assert.Equal(CorruptionLooks.AltarCorrupted, few[0].Model);
        Assert.Equal(1 + CorruptionLooks.PiecesPerStage, few.Count);
        Assert.Equal(1 + 3 * CorruptionLooks.PiecesPerStage, many.Count);
        // Les pièces d'un palier restent quand la terre grandit, et la même terre donne toujours les mêmes pièces.
        Assert.Equal(few.Select(p => (p.Model, p.X, p.Z)), many.Take(few.Count).Select(p => (p.Model, p.X, p.Z)));
        Assert.Equal(many.Select(p => (p.Model, p.X, p.Z, p.Yaw, p.Scale)), CorruptionLooks.Layout(ouest9).Select(p => (p.Model, p.X, p.Z, p.Yaw, p.Scale)));
        // Toutes dans le disque, à l'écart de l'autel, de modèles prévus, à une échelle raisonnable.
        foreach (CorruptionView v in day9)
        {
            double cx = v.Position.X.Raw / 10000.0, cz = v.Position.Z.Raw / 10000.0, r = v.Radius.Raw / 10000.0;
            double ax = v.Altar.X.Raw / 10000.0, az = v.Altar.Z.Raw / 10000.0;
            IReadOnlyList<CorruptionPiece> pieces = CorruptionLooks.Layout(v);
            Assert.Equal((v.IsSource ? 0 : 1) + v.Stage * CorruptionLooks.PiecesPerStage, pieces.Count);
            foreach (CorruptionPiece p in pieces.Skip(v.IsSource ? 0 : 1))
            {
                Assert.True(Distance2(p.X, p.Z, cx, cz) <= r * r, v.Area.Id);
                if (!v.IsSource) Assert.True(Distance2(p.X, p.Z, ax, az) >= CorruptionLooks.AltarClearance * CorruptionLooks.AltarClearance, v.Area.Id);
                Assert.Contains(p.Model, CorruptionLooks.Models);
                Assert.NotEqual(CorruptionLooks.AltarPurified, p.Model);
                Assert.InRange(p.Scale, 0.79, 1.21);
                Assert.InRange(p.Yaw, 0, 359);
            }
            if (v.IsSource) Assert.All(pieces, p => Assert.True(p.Model is CorruptionLooks.Crystals or CorruptionLooks.Rock1));
        }
        // Des arbres, des rochers et des cristaux dans une terre au palier 3.
        Assert.Contains(many, p => p.Model is CorruptionLooks.Tree1 or CorruptionLooks.Tree2);
        Assert.Contains(many, p => p.Model is CorruptionLooks.Rock1 or CorruptionLooks.Rock2);
        Assert.Contains(many, p => p.Model == CorruptionLooks.Crystals);
    }

    [Fact]
    public void Autel_purifie_sans_decor_pendant_le_repit()
    {
        IReadOnlyList<CorruptionView> views = Views(6, Hero().WithCorruption(new SavedCorruption(new[] { new KeyValuePair<string, long>("pierres_ouest", 6) })));
        CorruptionView ouest = views.Single(v => v.Area.Id == "pierres_ouest");
        Assert.False(ouest.IsCorrupted);
        CorruptionPiece altar = Assert.Single(CorruptionLooks.Layout(ouest));
        Assert.Equal(CorruptionLooks.AltarPurified, altar.Model);
        Assert.Equal((1420.0, -670.0), (altar.X, altar.Z));
    }

    [Fact]
    public void Modeles_prevus_noms_tailles_et_liste_de_l_atelier()
    {
        Assert.Equal(8, CorruptionLooks.Models.Count);
        Assert.Equal(CorruptionLooks.Models.Count, CorruptionLooks.Models.Distinct().Count());
        foreach (string m in CorruptionLooks.Models)
        {
            var size = CorruptionLooks.Size(m);
            Assert.True(size.Length > 0 && size.Width > 0 && size.Height > 0, m);
            Assert.Equal(m, CorruptionLooks.NameOf(CorruptionLooks.Folder + "/" + m + "/" + m + ".fbx"));
        }
        Assert.Null(CorruptionLooks.NameOf("Assets/EdS/Modeles/Corruption/autre/autre.fbx"));
        Assert.Null(CorruptionLooks.NameOf(""));
        Assert.Throws<ArgumentException>(() => CorruptionLooks.Size("autre"));
        Assert.Equal(CorruptionLooks.Seed("pierres_ouest"), CorruptionLooks.Seed("pierres_ouest"));
        Assert.NotEqual(CorruptionLooks.Seed("pierres_ouest"), CorruptionLooks.Seed("faille_du_nord"));
        // Aucun modèle versé pour l'instant (aucune dépense) : le dossier est absent ou ne contient que des modèles prévus.
        string folder = Path.Combine(Lot93UnityPackageTests.Root, "unity", CorruptionLooks.Folder);
        if (Directory.Exists(folder))
            Assert.All(Directory.GetFiles(folder, "*.fbx", SearchOption.AllDirectories), f => Assert.NotNull(CorruptionLooks.NameOf(f)));
    }

    [Fact]
    public void Scripts_unity_branches()
    {
        string game = Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS");
        string view = File.ReadAllText(Path.Combine(game, "Game", "CorruptionDecorView.cs"));
        Assert.Contains("CorruptionLooks.Layout(v)", view);
        Assert.Contains("StyleCatalog.Loaded.CorruptionModel(p.Model)", view);
        Assert.Contains("Stand(p.Model, holder.transform, size)", view);
        string edsGame = File.ReadAllText(Path.Combine(game, "Game", "EdsGame.cs"));
        Assert.Contains("_corruption.Sync(a, _camera.Camera.transform.position);", edsGame);
        Assert.Contains("_corruption?.Destroy();", edsGame);
        Assert.Contains("a.AltarInReach != null) return a.Text(\"text.ui.corruption.purify\")", edsGame);
        Assert.Contains("a.CorruptionLabel", edsGame);
        Assert.Contains("AdventureSession.CorruptionFadeAt(a.Corruption, at)", File.ReadAllText(Path.Combine(game, "Game", "ScarsView.cs")));
        Assert.Contains("public GameObject CorruptionModel(string name)", File.ReadAllText(Path.Combine(game, "Game", "StyleCatalog.cs")));
        Assert.Contains("catalog.Corruption = CorruptionPieces(notes);", File.ReadAllText(Path.Combine(game, "Editor", "EdsStyle.cs")));
    }
}
