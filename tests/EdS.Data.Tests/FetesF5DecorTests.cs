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
    public void Decor_du_mois_pose_aux_places_validees_sans_bloquer_routes_portes_ni_PNJ()
    {
        AdventureSession a = Session.Value;
        AdventureMap map = a.RegionMap.Whole;
        var obstacles = map.Obstacles.Select(o => (IReadOnlyList<(double X, double Z)>)o.Points.Select(q => (q.X.Raw / 10000.0, q.Z.Raw / 10000.0)).ToList()).ToList();
        var doors = map.Doors.Select(d => (d.X, d.Z)).ToList();
        var streets = FeastLooks.Streets(map.Roads)!;   // le dallage des places n'est pas une rue (décision de Damien du 19 novembre 2026 : mêmes règles que l'étal)
        // Relecture indépendante de la PR #219 : les vraies distances : PNJ (places de jour et de pluie), invités, points sûrs, portes,
        // accessoires du Bourg fortifié ; l'étal du forain et ses décorations.
        var avoid = a.FeastAvoid().ToList();
        Assert.Contains(avoid, p => Math.Abs(p.X - (-1088.0)) < 1e-6 && Math.Abs(p.Z - (-37.0)) < 1e-6);   // le forain lui-même
        avoid.AddRange(map.Bourg.Pieces.Where(b => !BourgModels.IsGround(b.Piece) && b.Covers is null).Select(b => (b.X, b.Z)));
        // Décisions de Damien du 19 novembre 2026 : deux places, devant l'auberge, parvis de la chapelle, portes du Bourg.
        var places = a.FeastDecorPlaces();
        Assert.Equal(new[] { "bourg", "place_du_marche_de_la_ville", "devant:auberge", "devant:chapelle" }, places.Take(4).Select(p => p.Id));
        Assert.True(places.Count(p => p.Id.StartsWith("porte:", StringComparison.Ordinal)) >= 3);   // chaque porte du Bourg posée (est, ouest, nord…)
        foreach (Feast feast in a.World.FeastBook!.Feasts)
        {
            IReadOnlyList<FeastPiece> pieces = a.FeastDecorOf(feast);
            var stall = a.FeastStallOf(feast)!.Value;
            var taken = new List<IReadOnlyList<(double X, double Z)>> { FeastLooks.Box(stall.X, stall.Z, stall.Yaw, FeastLooks.StallWidth, FeastLooks.StallDepth) };
            taken.AddRange(stall.Pieces.Select(p => FeastLooks.Box(p.X, p.Z, p.Yaw, p.Width, p.Depth)));
            var perPlace = places.ToDictionary(p => p.Id, _ => 0);
            for (int i = 0; i < pieces.Count; i++)
            {
                FeastPiece p = pieces[i];
                var place = places.Single(q => q.Id == p.Place);
                Assert.InRange(p.X, place.MinX, place.MaxX);
                Assert.InRange(p.Z, place.MinZ, place.MaxZ);
                perPlace[place.Id]++;
                Assert.True(FeastLooks.Fits(p.X, p.Z, p.Yaw, p.Width, p.Depth, obstacles,
                    taken.Concat(pieces.Take(i).Select(o => FeastLooks.Box(o.X, o.Z, o.Yaw, o.Width, o.Depth))).ToList(), streets, doors, avoid), feast.Festival + " " + p.Code);
                foreach ((double cx, double cz, double r) in p.Footprint)
                {
                    Assert.True(TownRules.RoadAt(cx, cz, streets).Edge >= r, p.Code + " sur une route");
                    Assert.All(doors, d => Assert.True(Math.Sqrt((cx - d.X) * (cx - d.X) + (cz - d.Z) * (cz - d.Z)) >= r + FeastLooks.DoorClearance, p.Code + " devant une porte"));
                    Assert.All(avoid, q => Assert.True(Math.Sqrt((cx - q.X) * (cx - q.X) + (cz - q.Z) * (cz - q.Z)) >= r + FeastLooks.PeopleClearance, p.Code + " sur un PNJ"));
                    Assert.All(taken, t => Assert.True(StyleRules.InsideDistance(cx, cz, t) <= -r + 1e-9, p.Code + " sur l'étal"));
                }
            }
            Assert.True(perPlace.All(kv => kv.Value >= 1) && perPlace["bourg"] == 5 && perPlace["place_du_marche_de_la_ville"] == 5,
                feast.Festival + " : " + string.Join(", ", perPlace.Select(kv => kv.Key + " " + kv.Value)));
        }
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
        Assert.True(a.FeastDecorPieces.Count >= 17, a.FeastDecorPieces.Count.ToString());
        Assert.Same(a.FeastDecorPieces, a.FeastDecorPieces);
        string view = Source("Game/WorldMarkers.Feasts.cs");
        Assert.Contains("foreach (FeastPiece piece in adventure.FeastDecorPieces)", view);
        Assert.Contains("Object.Destroy(_feastStall);", view);
    }

    [Fact]
    public void Fete_commencee_en_interieur_le_decor_vient_en_sortant()
    {
        // Relecture indépendante de la PR #219 : en intérieur, rien n'est posé ni retenu ; la clé du calcul porte la zone, et une liste vide
        // n'est jamais gardée (un retour dehors, ou une sauvegarde reprise en intérieur, calcule le décor du mois).
        string save = Path.Combine(Path.GetTempPath(), "eds-f5-" + Guid.NewGuid().ToString("N") + ".json");
        SaveStore.Write(save, new WorldSave("zone.auberge", WorldEventSchedule.TickAt(new WorldClock(50), 13 * 1440 + 12 * 60), 0,
            new[] { PlayerCharacters.PlacesKey }, Hero()));
        try
        {
            AdventureSession inside = Adventure(save);
            Assert.True(inside.Indoors);
            Assert.Equal("festival.carnaval", inside.FeastNow?.Festival);
            Assert.Empty(inside.FeastDecorPieces);
        }
        finally
        {
            File.Delete(save);
        }
        Assert.NotEmpty(Session.Value.FeastDecorPieces);
        string code = File.ReadAllText(Path.Combine(Root, "src", "EdS.Runtime", "AdventureSession.Feasts.cs"));
        Assert.Contains("string key = feast.Festival + \"|\" + World.Definition?.Id;", code);
        Assert.Contains("if (World.FeastStall is null || !(World.FeastNow is Feast feast)) return Array.Empty<FeastPiece>();", code);
    }
}
