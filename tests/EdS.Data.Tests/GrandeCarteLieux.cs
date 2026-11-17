using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Zones;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Runtime;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lots V2.31 et suivants (les lieux de la grande carte, étape 2 du plan) : outils communs aux essais des lieux. Chaque obstacle d'un
/// lieu porte l'étiquette de son lieu (<see cref="ScenarioTestData.LieuTags"/>) ; un lieu est livré « habité » (obstacles, lieux nommés,
/// sentiers, points sûrs) avant d'être « beau » (modèles demandés, PLAN_GRANDE_CARTE.md § 8).
/// </summary>
internal static class GrandeCarteLieux
{
    public static readonly Lazy<ZoneDefinition> Vallons = new(() => ZoneJsonReader.ReadFile(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons"),
        Path.Combine(AppContext.BaseDirectory, "scenarios"), ShippedData, ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!).Zone!);

    public static (double X, double Z) P(GroundVector v) => (v.X.Raw / 1e4, v.Z.Raw / 1e4);

    public static GroundVector G(double x, double z) => new(Fixed.FromRaw((long)Math.Round(x * 1e4)), Fixed.FromRaw((long)Math.Round(z * 1e4)));

    public static List<SpaceObstacle> Tagged(string tag) =>
        Vallons.Value.Setup.Space!.Obstacles.Where(o => o.Id.Contains(tag, StringComparison.Ordinal)).ToList();

    public static List<(double X, double Z)> Shape(SpaceObstacle o) => o.Points.Select(P).ToList();

    /// <summary>Chaque sommet des obstacles de l'étiquette est dans l'un des lieux nommés.</summary>
    public static void InPlaces(string tag, params string[] places)
    {
        ZoneDefinition zone = Vallons.Value;
        var rects = places.Select(id => zone.Places.Single(p => p.Id == id)).ToList();
        List<SpaceObstacle> obstacles = Tagged(tag);
        Assert.NotEmpty(obstacles);
        foreach (SpaceObstacle o in obstacles)
            foreach (GroundVector v in o.Points)
                Assert.True(rects.Any(p => p.Contains(v)), $"{o.Id} hors de {string.Join(", ", places)} en {P(v)}");
    }

    /// <summary>
    /// Aucune route ne traverse un obstacle (hors des passages et du puits de la place pavée), à 1 m près, ni ne longe un obstacle du lieu à
    /// moins de sa demi-largeur.
    /// </summary>
    public static void RoadsClear(string tag)
    {
        ZoneDefinition zone = Vallons.Value;
        var gates = zone.Passages.Select(p => p.Obstacle).ToHashSet(StringComparer.Ordinal);
        var all = zone.Setup.Space!.Obstacles.Where(o => !gates.Contains(o.Id) && o.Id != "puits").Select(o => (o.Id, Shape: Shape(o))).ToList();
        var mine = Tagged(tag).Select(o => o.Id).ToHashSet(StringComparer.Ordinal);
        foreach (Road road in zone.Roads)
            for (int i = 0; i + 1 < road.Points.Count; i++)
            {
                (double ax, double az) = P(road.Points[i]);
                (double bx, double bz) = P(road.Points[i + 1]);
                int n = (int)Math.Ceiling(Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az)));
                for (int k = 0; k <= n; k++)
                {
                    double x = ax + (bx - ax) * k / n, z = az + (bz - az) * k / n;
                    foreach (var o in all)
                    {
                        double d = GroundRelief.PolygonDistance(o.Shape, x, z);
                        Assert.True(d > 0, $"{road.Id} traverse {o.Id} en ({x:0.0}, {z:0.0})");
                        if (mine.Contains(o.Id)) Assert.True(d > road.Width.Raw / 2e4 + 0.5, $"{road.Id} frôle {o.Id} en ({x:0.0}, {z:0.0})");
                    }
                }
            }
    }

    /// <summary>Le point sûr est dans un lieu de la région, à plus de 3 m de tout obstacle et à moins de 6 m d'une route.</summary>
    public static void SafePointClear(string id)
    {
        ZoneDefinition zone = Vallons.Value;
        SafePoint s = zone.SafePoints.Single(p => p.Id == id);
        (double x, double z) = P(s.Position);
        Assert.All(zone.Setup.Space!.Obstacles, o => Assert.True(GroundRelief.PolygonDistance(Shape(o), x, z) > 3, $"{id} contre {o.Id}"));
        Assert.NotEmpty(zone.PlacesAt(s.Position));
        Assert.True(TownRules.RoadAt(x, z, AdventureSession.Roads(zone)).Edge < 6, $"{id} loin des routes");
    }

    /// <summary>Remplissage (cases de <paramref name="step"/> m) d'une fenêtre, sans les obstacles <paramref name="without"/>.</summary>
    public static bool Walks((double X, double Z) min, (double X, double Z) max, (double X, double Z) from, (double X, double Z) to,
        double step = 1, params string[] without)
    {
        var obstacles = Vallons.Value.Setup.Space!.Obstacles.Where(o => !without.Contains(o.Id));
        var flood = V228cSurfaceTests.Flood(obstacles, min, max, from, step);
        return V228cSurfaceTests.Reaches(flood.Reached, min, to, step);
    }

    public static (double X, double Z) SafePoint(string id) => P(Vallons.Value.SafePoints.Single(p => p.Id == id).Position);
}
