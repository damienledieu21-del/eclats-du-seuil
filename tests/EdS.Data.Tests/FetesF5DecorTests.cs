using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EdS.Data.Feasts;
using EdS.Data.Saves;
using EdS.Runtime;
using EdS.World;
using Xunit;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot F5 (décor du mois) : pendant chaque fête, ses cinq décorations sur la place du village (le forain) et la place du marché du Bourg,
/// à des places validées (hors obstacles, routes, portes, PNJ, invités, points sûrs, accessoires du Bourg, étal), retirées après la fête ;
/// modèles des 60 décorations ; contrôles de source pour Unity.
/// </summary>
[Trait("Category", "World")]
public sealed class FetesF5DecorTests
{
    private static string Root => Lot93UnityPackageTests.Root;
    private static string Source(string relative) => File.ReadAllText(Path.Combine(Root, "unity", "Assets", "EdS", relative));

    private static readonly Lazy<AdventureSession> Session = new(() =>
    {
        string save = Path.Combine(Path.GetTempPath(), "eds-f5-" + Guid.NewGuid().ToString("N") + ".json");
        SaveStore.Write(save, new WorldSave("zone.vallons", WorldEventSchedule.TickAt(new WorldClock(50), 13 * 1440 + 12 * 60), 0,
            new[] { PlayerCharacters.PlacesKey }, Hero()));
        try
        {
            return Adventure(save);
        }
        finally
        {
            File.Delete(save);
        }
    });

    [Fact]
    public void Decor_du_mois_pose_aux_deux_places_sans_bloquer_routes_ni_portes()
    {
        AdventureSession a = Session.Value;
        AdventureMap map = a.RegionMap.Whole;
        var obstacles = map.Obstacles.Select(o => (IReadOnlyList<(double X, double Z)>)o.Points.Select(q => (q.X.Raw / 10000.0, q.Z.Raw / 10000.0)).ToList()).ToList();
        var doors = map.Doors.Select(d => (d.X, d.Z)).ToList();
        FeastBook book = a.World.FeastBook!;
        var report = new List<string>();
        foreach (Feast feast in book.Feasts)
        {
            IReadOnlyList<FeastPiece> pieces = a.FeastDecorOf(feast);
            report.Add(feast.Festival + " " + pieces.Count);
            Assert.Equal(feast.Decorations.Select(r => r.Code).Concat(feast.Decorations.Select(r => r.Code)), pieces.Select(p => p.Code));
            for (int i = 0; i < pieces.Count; i++)
            {
                FeastPiece p = pieces[i];
                MapPlace place = map.Places.Single(q => q.Id == (i < 5 ? "bourg" : "place_du_marche_de_la_ville"));
                Assert.InRange(p.X, place.MinX, place.MaxX);
                Assert.InRange(p.Z, place.MinZ, place.MaxZ);
                Assert.True(FeastLooks.Fits(p.X, p.Z, p.Yaw, p.Width, p.Depth, obstacles,
                    pieces.Take(i).Select(o => FeastLooks.Box(o.X, o.Z, o.Yaw, o.Width, o.Depth)).ToList(),
                    map.Roads, doors, Array.Empty<(double, double)>()), feast.Festival + " " + p.Code);
                foreach ((double cx, double cz, double r) in p.Footprint)
                {
                    Assert.True(TownRules.RoadAt(cx, cz, map.Roads).Edge >= r, p.Code + " sur une route");
                    Assert.All(doors, d => Assert.True(Math.Sqrt((cx - d.X) * (cx - d.X) + (cz - d.Z) * (cz - d.Z)) >= r + FeastLooks.DoorClearance, p.Code + " devant une porte"));
                }
            }
        }
        Assert.True(report.Count == 12, string.Join(", ", report));
    }

    [Fact]
    public void Hors_fete_aucun_decor_et_Unity_le_pose_puis_le_retire()
    {
        // Un jour sans fête : rien (l'étal et le décor sont retirés : la clé change, le support est détruit).
        string save = Path.Combine(Path.GetTempPath(), "eds-f5-" + Guid.NewGuid().ToString("N") + ".json");
        SaveStore.Write(save, new WorldSave("zone.vallons", WorldEventSchedule.TickAt(new WorldClock(50), 15 * 1440 + 12 * 60), 0,
            new[] { PlayerCharacters.PlacesKey }, Hero()));
        AdventureSession plain;
        try
        {
            plain = Adventure(save);
        }
        finally
        {
            File.Delete(save);
        }
        Assert.Null(plain.FeastNow);
        Assert.Empty(plain.FeastDecorPieces);
        Assert.Null(plain.FeastStallLook);
        // Le jour du Carnaval : ses cinq décorations aux deux places, calculées une fois.
        AdventureSession a = Session.Value;
        Assert.Equal("festival.carnaval", a.FeastNow?.Festival);
        Assert.Equal(10, a.FeastDecorPieces.Count);
        Assert.Same(a.FeastDecorPieces, a.FeastDecorPieces);
        string view = Source("Game/WorldMarkers.Feasts.cs");
        Assert.Contains("foreach (FeastPiece piece in adventure.FeastDecorPieces)", view);
        Assert.Contains("Object.Destroy(_feastStall);", view);
    }
}
