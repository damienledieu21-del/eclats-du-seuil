using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Scenarios;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.Simulation.Engine;
using EdS.TestSupport;
using EdS.World;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.8 (recherche de chemin ; accord de Damien du 7 novembre 2026) : moteur 0.30.0, journal au format 24 (<c>navigation</c> dans
/// l'en-tête d'un combat positionné) ; scénarios (17), règles (16) et instantané (6) inchangés ; nouvelle référence figée
/// <c>Fixtures/reference-0.30.0-contenu-0.6.0-regles-0.17.0</c> (corps identiques pour les anciens journaux, et <c>espace_chemins</c>).
/// </summary>
[Trait("Category", "Behavior")]
[Trait("Category", "Regression")]
public class V28CheminsMoteurTests
{
    private const string Chemins = "espace_chemins";
    private const string Latest = "reference-0.30.0-contenu-0.6.0-regles-0.17.0", Previous = "reference-0.29.0-contenu-0.6.0-regles-0.17.0";

    private static string Fixture(string set, string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", set, name + ".jsonl");

    private static CombatSetup Load(string name = Chemins)
    {
        ScenarioLoadResult result = ScenarioJsonReader.ReadFile(ScenarioPath(name), ShippedData);
        Assert.True(result.IsValid, result.Report.ToString());
        return result.Setup!;
    }

    private static MemoryCombatLog Simulate(CombatSetup setup)
    {
        var log = new MemoryCombatLog();
        CombatEngine.Run(setup, log);
        return log;
    }

    private static List<JsonElement> Events(MemoryCombatLog log) =>
        log.Lines.Skip(1).Select(l => JsonDocument.Parse(l).RootElement.Clone()).ToList();

    private static GroundVector At(CombatEngine engine, string id) => engine.View(id).Position!.Value;

    private static ConvexPolygon Polygon(CombatSetup setup, string id) => new ConvexPolygon(setup.Space!.Obstacles.Single(o => o.Id == id).Points);

    [Fact]
    public void Versions_du_sous_lot()
    {
        Assert.Equal(("0.30.0", 24, 17, 6), (CombatEngine.Version, CombatEngine.LogFormatVersion, ScenarioValidator.CurrentSchemaVersion, CombatSnapshot.FormatVersion));
        Assert.Equal((8, 8_192), (CombatEngine.MaxPathQueriesPerTick, CombatEngine.MaxPathExpanded));
    }

    [Fact]
    public void Reference_figee_identique_et_rejouee()
    {
        string[] frozen = File.ReadAllLines(Fixture(Latest, Chemins));
        Assert.Equal(frozen, Simulate(Load()).Lines);
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(frozen, ShippedData).Status);
        JsonElement navigation = JsonDocument.Parse(frozen[0]).RootElement.GetProperty("navigation");
        Assert.Equal((8, 8192), (navigation.GetProperty("queriesPerTick").GetInt32(), navigation.GetProperty("maxExpanded").GetInt32()));
    }

    [Theory]
    [InlineData("espace_betes")]
    [InlineData("espace_promenade")]
    [InlineData("entrainement_bonus")]
    [InlineData("entrainement_bouclier")]
    [InlineData("entrainement_declencheurs")]
    [InlineData("entrainement_equipement")]
    public void Ancienne_reference_refusee_et_corps_reproduit(string name)
    {
        // Ligne droite libre partout (ou combat sans espace) : le moteur 0.30.0 reproduit octet par octet les lignes d'événements du moteur
        // 0.29.0 ; seul l'en-tête change (format, version, bornes de navigation pour un combat positionné). L'ancienne référence reste figée
        // et refusée au rejeu.
        string[] old = File.ReadAllLines(Fixture(Previous, name)), latest = File.ReadAllLines(Fixture(Latest, name));
        Assert.Equal(ReplayStatus.Refused, CombatLogReplayer.Replay(old, ShippedData).Status);
        Assert.Equal(old.Skip(1), latest.Skip(1));
        Assert.Equal(latest, Simulate(Load(name)).Lines);
        bool spatial = Load(name).Space != null;
        Assert.Equal(spatial, JsonDocument.Parse(latest[0]).RootElement.TryGetProperty("navigation", out _));
    }

    [Fact]
    public void Poursuite_autour_d_une_maison_hors_d_une_impasse_et_enfermee_sans_bouger()
    {
        CombatSetup setup = Load();
        var engine = new CombatEngine(setup, new MemoryCombatLog());
        ConvexPolygon house = Polygon(setup, "maison");
        ConvexPolygon[] walls = setup.Space!.Obstacles.Select(o => new ConvexPolygon(o.Points)).ToArray();
        GroundVector penned = At(engine, "loup_enclos");
        bool aroundWest = false, outOfDeadEnd = false;
        while (!engine.IsFinished)
        {
            engine.Step();
            foreach (string id in new[] { "loup_maison", "loup_impasse", "loup_enclos", "lievre" })
            {
                long r = engine.View(id).Radius!.Value.Raw;
                GroundVector p = At(engine, id);
                // Jamais dans un mur : chaque emprise reste hors du décor.
                Assert.DoesNotContain(walls, w => w.OverlapsCircle(p.X.Raw, p.Z.Raw, r - 1));
            }
            GroundVector m = At(engine, "loup_maison");
            if (m.Z.Raw < 70_000 && m.Z.Raw > 40_000 && Math.Abs(m.X.Raw) > 30_000) aroundWest = true;   // il longe un côté de la maison
            if (At(engine, "loup_impasse").X.Raw > 150_000) outOfDeadEnd = true;                  // il sort de l'impasse par l'est
            // Aucun chemin : le loup enfermé ne bouge jamais (aucun aller-retour).
            Assert.Equal(penned, At(engine, "loup_enclos"));
        }
        Assert.True(aroundWest, "le loup n'a pas contourné la maison");
        Assert.True(outOfDeadEnd, "le loup n'est pas sorti de l'impasse");
        Assert.False(house.OverlapsCircle(At(engine, "loup_maison").X.Raw, At(engine, "loup_maison").Z.Raw, 1));
        // Les deux loups qui ont un chemin atteignent le Bastion et mordent ; l'enfermé, jamais.
        List<JsonElement> events = Events(Simulate(Load()));
        string?[] biters = events.Where(e => e.GetProperty("event").GetString() == "damage").Select(e => e.TryGetProperty("source", out JsonElement s) ? s.GetString() : null).Distinct().ToArray();
        Assert.Contains("loup_maison", biters);
        Assert.Contains("loup_impasse", biters);
        Assert.DoesNotContain("loup_enclos", biters);
    }

    [Fact]
    public void Fuite_de_biais_le_long_d_un_muret()
    {
        CombatSetup setup = Load();
        var engine = new CombatEngine(setup, new MemoryCombatLog());
        GroundVector start = At(engine, "lievre");
        for (int i = 0; i < 60; i++) engine.Step();
        GroundVector end = At(engine, "lievre");
        // Le muret est au sud : le lièvre ne s'y colle pas, il file de biais (vers l'est) puis le contourne.
        Assert.True(end.X.Raw - start.X.Raw > 30_000, $"lièvre en {end}");
        double Away(GroundVector p) => Math.Sqrt(Math.Pow(p.X.Raw / 1e4, 2) + Math.Pow(p.Z.Raw / 1e4, 2));
        Assert.True(Away(end) > Away(start) + 3);
    }

    [Fact]
    public void Rejeu_identique_et_reprise_d_instantane_sans_cache()
    {
        CombatSetup setup = Load();
        Assert.Equal(Simulate(setup).Lines, Simulate(Load()).Lines);
        // Le cache des chemins n'est pas dans l'instantané : une reprise (cache vide) écrit exactement les mêmes lignes.
        var log = new MemoryCombatLog();
        var engine = new CombatEngine(setup, log);
        for (int i = 0; i < 40; i++) engine.Step();
        var resumedLog = new MemoryCombatLog();
        CombatEngine resumed = CombatEngine.Restore(engine.CompletedSetup(), engine.Snapshot(), resumedLog);
        int from = log.Lines.Count;
        while (engine.CurrentTick < 280)
        {
            engine.Step();
            resumed.Step();
        }
        Assert.Equal(log.Lines.Skip(from), resumedLog.Lines);
        Assert.Equal(engine.Snapshot().Text, resumed.Snapshot().Text);
    }

    [Fact]
    public void Budget_de_recherches_borne_par_tick()
    {
        var engine = new CombatEngine(Load(), new MemoryCombatLog());
        long before = engine.PathSearches;
        while (!engine.IsFinished)
        {
            engine.Step();
            long now = engine.PathSearches;
            Assert.True(now - before <= CombatEngine.MaxPathQueriesPerTick, $"{now - before} recherches en un tick");
            Assert.True(engine.PathLastExpanded <= CombatEngine.MaxPathExpanded);
            before = now;
        }
        Assert.True(engine.PathSearches > 0);
    }
}

/// <summary>Sous-lot V2.8 : grille de navigation et A* (EdS.Domain), sans moteur.</summary>
[Trait("Category", "Space")]
public class V28CheminsGrilleTests
{
    private static ConvexPolygon Rect(double x0, double z0, double x1, double z1) =>
        new ConvexPolygon(new[] { P(x0, z0), P(x1, z0), P(x1, z1), P(x0, z1) });

    private static GroundVector P(double x, double z) => GroundVector.FromRaw((long)Math.Round(x * 1e4), (long)Math.Round(z * 1e4));

    private static long R(double m) => (long)Math.Round(m * 1e4);

    private static Navigator House(int maxExpanded = 8_192, int cache = 512) =>
        new Navigator(R(-20), R(-20), R(20), R(20), new[] { Rect(-3, -2, 3, 2) }, maxExpanded, cache);

    private static void AssertClearRoute(Navigator nav, long radius, (double X, double Z) from, List<GroundVector> route)
    {
        long ax = R(from.X), az = R(from.Z);
        foreach (GroundVector p in route)
        {
            Assert.True(nav.Clear(ax, az, p.X.Raw, p.Z.Raw, radius), $"segment ({ax}, {az}) → {p} dans un mur");
            ax = p.X.Raw;
            az = p.Z.Raw;
        }
    }

    [Fact]
    public void Chemin_autour_d_une_maison()
    {
        Navigator nav = House();
        long r = R(0.5);
        Assert.Equal(NavSteer.Direct, nav.Steer(r, R(-10), R(5), R(10), R(5), out _, out _));
        Assert.Equal(NavSteer.Path, nav.Steer(r, R(0), R(-8), R(0), R(8), out long ax, out long az));
        Assert.NotEqual((R(0), R(8)), (ax, az));
        Assert.True(nav.Clear(R(0), R(-8), ax, az, r));
        List<GroundVector> route = nav.Route(r, R(0), R(-8), R(0), R(8))!;
        Assert.True(route.Count >= 2 && route.Count <= 6, $"{route.Count} points");
        Assert.Equal(P(0, 8), route[^1]);
        AssertClearRoute(nav, r, (0, -8), route);
        // Trajet court : moins de 1,6 fois la ligne droite (16 m) autour d'une maison de 6 × 4 m.
        double length = 0, px = 0, pz = -8;
        foreach (GroundVector p in route)
        {
            length += Math.Sqrt(Math.Pow(p.X.Raw / 1e4 - px, 2) + Math.Pow(p.Z.Raw / 1e4 - pz, 2));
            (px, pz) = (p.X.Raw / 1e4, p.Z.Raw / 1e4);
        }
        Assert.InRange(length, 16, 16 * 1.6);
        // Départage stable : contourne toujours par le même côté.
        Assert.Equal(route, House().Route(r, R(0), R(-8), R(0), R(8)));
    }

    [Fact]
    public void Impasse_on_ressort_par_l_ouverture()
    {
        // U ouvert à l'est ; le but est à l'ouest du fond.
        var nav = new Navigator(R(-20), R(-20), R(20), R(20), new[] { Rect(-0.5, -4, 0, 4), Rect(0, 3.5, 6, 4), Rect(0, -4, 6, -3.5) });
        long r = R(0.6);
        List<GroundVector> route = nav.Route(r, R(2), R(0), R(-5), R(0))!;
        Assert.NotEmpty(route);
        Assert.True(route[0].X.Raw > R(5), $"premier point {route[0]}");
        AssertClearRoute(nav, r, (2, 0), route);
    }

    [Fact]
    public void Zone_sans_chemin_aucune_recherche_et_rester_sur_place()
    {
        var nav = new Navigator(R(-20), R(-20), R(20), R(20),
            new[] { Rect(-5, -5, -4.5, 5), Rect(4.5, -5, 5, 5), Rect(-4.5, 4.5, 4.5, 5), Rect(-4.5, -5, 4.5, -4.5) });
        long r = R(0.5);
        Assert.Equal(NavSteer.None, nav.Steer(r, R(0), R(0), R(12), R(0), out long ax, out long az));
        Assert.Equal((R(0), R(0)), (ax, az));
        Assert.Null(nav.Route(r, R(0), R(0), R(12), R(0)));
        Assert.Equal(0, nav.Searches);   // composantes différentes : su sans chercher
        Assert.Equal(2, nav.Grid(r).ComponentCount);
        // But dans un mur épais (aucune case libre à moins de 2 m) : aucun chemin.
        var thick = new Navigator(R(-20), R(-20), R(20), R(20), new[] { Rect(-8, -8, 8, 8) });
        Assert.Equal(NavSteer.None, thick.Steer(r, R(-15), R(0), R(0), R(0), out _, out _));
    }

    [Fact]
    public void Performance_bornee_chemin_partiel_vers_la_case_la_plus_proche()
    {
        // Long mur avec un seul passage au bout : avec 200 cases au plus, le chemin est partiel mais avance vers le but.
        var obstacles = new[] { Rect(-1, -18, 1, 20) };
        var small = new Navigator(R(-20), R(-20), R(20), R(20), obstacles, maxExpanded: 200);
        long r = R(0.5);
        int[]? cells = small.Cells(r, R(-10), R(10), R(10), R(10), out NavStatus status);
        Assert.Equal(NavStatus.Partial, status);
        Assert.NotNull(cells);
        Assert.True(small.LastExpanded <= 200);
        var full = new Navigator(R(-20), R(-20), R(20), R(20), obstacles);
        Assert.Equal(NavStatus.Found, full.Cells(r, R(-10), R(10), R(10), R(10), out NavStatus s2) is null ? NavStatus.None : s2);
        Assert.True(full.LastExpanded <= 8_192, $"{full.LastExpanded} cases développées");
        // Sur la grille de 80 × 80 cases, la recherche complète reste loin de tout développer.
        Assert.True(full.LastExpanded < 80 * 80);
    }

    [Fact]
    public void Cache_sans_effet_sur_les_reponses()
    {
        Navigator cached = House(), tiny = House(cache: 1);
        long r = R(0.5);
        for (int i = 0; i < 40; i++)
        {
            long fx = R(-12 + (i * 7) % 24), fz = R(-9 + (i * 5) % 3), tx = R(10 - (i * 3) % 20), tz = R(6 + (i * 11) % 7);
            for (int pass = 0; pass < 2; pass++)
            {
                NavSteer a = cached.Steer(r, fx, fz, tx, tz, out long ax, out long az);
                NavSteer b = tiny.Steer(r, fx, fz, tx, tz, out long bx, out long bz);
                Assert.Equal((a, ax, az), (b, bx, bz));
            }
        }
    }

    [Fact]
    public void Recherche_sans_allocation_et_bornes_numeriques()
    {
        var grid = new NavGrid(R(-1000), R(-1000), R(1000), R(1000), Array.Empty<ConvexPolygon>(), R(0.5));
        Assert.True(grid.Count <= NavGrid.MaxCells);
        Assert.Equal(1, grid.ComponentCount);
        var search = new NavSearch();
        var path = new int[64];
        int start = grid.NearestFree(R(-900), R(-900), 2), goal = grid.NearestFree(R(900), R(900), 2);
        Assert.Equal(NavStatus.Found, search.Find(grid, start, goal, 8_192, path, out int length));
        Assert.Equal(64, length);   // chemin tronqué au tampon : seules les premières cases comptent pour avancer
        Assert.Equal(start, path[0]);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 20; i++) search.Find(grid, start, goal, 8_192, path, out _);
        Assert.True(GC.GetAllocatedBytesForCurrentThread() - allocated < 4_096);
    }
}

/// <summary>Sous-lot V2.8 : les PNJ et les passants de l'affichage contournent les maisons des Vallons (Runtime, Unity).</summary>
[Trait("Category", "World")]
public class V28CheminsMondeTests
{
    private static CombatSpace Vallons()
    {
        ScenarioLoadResult result = ScenarioJsonReader.ReadFile(ScenarioPath("zones/vallons"), ShippedData);
        Assert.True(result.IsValid, result.Report.ToString());
        return result.Setup!.Space!;
    }

    [Fact]
    public void PNJ_du_bourg_contournent_les_maisons()
    {
        CombatSpace space = Vallons();
        var paths = new WalkPaths(space);
        long r = (long)Math.Round(WalkPaths.DefaultRadius * 1e4);
        NavGrid grid = paths.Navigator.Grid(r);
        // Paires de cases libres de la même composante, tirées à pas fixes sur la carte : chaque trajet est libre de bout en bout.
        var free = Enumerable.Range(0, grid.Count).Where(c => grid.IsFree(c) && grid.Component(c) == grid.Component(grid.NearestFree(0, 0, 4))).ToList();
        int blocked = 0, routed = 0;
        for (int i = 0; i < 30; i++)
        {
            int a = free[(i * 7919) % free.Count], b = free[(i * 104_729 + 17) % free.Count];
            double ax = grid.CellX(a) / 1e4, az = grid.CellZ(a) / 1e4, bx = grid.CellX(b) / 1e4, bz = grid.CellZ(b) / 1e4;
            List<(double X, double Z)>? route = paths.Route(ax, az, bx, bz);
            if (route is null)
            {
                blocked++;
                continue;
            }
            if (route.Count > 0) routed++;
            var walker = new Walker();
            double x = ax, z = az;
            for (int step = 0; step < 20_000; step++)
            {
                (double tx, double tz) = walker.Next(paths, x, z, bx, bz);
                double dx = tx - x, dz = tz - z, d = Math.Sqrt(dx * dx + dz * dz);
                if (d < 1e-9) break;
                double move = Math.Min(d, 0.07);   // 1,4 m/s à 20 images par seconde
                // Le corps (0,35 m) peut frôler un coin de quelques centimètres entre deux points de passage ; son centre reste à plus de la
                // moitié de son rayon de tout mur.
                double nx = x + dx / d * move, nz = z + dz / d * move;
                Assert.True(paths.Navigator.Clear((long)Math.Round(x * 1e4), (long)Math.Round(z * 1e4), (long)Math.Round(nx * 1e4), (long)Math.Round(nz * 1e4), r / 2),
                    $"trajet {i} dans un mur en ({x:0.00}, {z:0.00})");
                (x, z) = (nx, nz);
            }
            Assert.True(Math.Abs(x - bx) < 0.06 && Math.Abs(z - bz) < 0.06, $"trajet {i} arrêté en ({x:0.00}, {z:0.00})");
        }
        Assert.Equal(0, blocked);
        Assert.True(routed > 0, "aucun trajet n'a eu à contourner");
    }

    [Fact]
    public void Marcheur_sans_chemin_reste_ou_va_droit()
    {
        CombatSpace closed = new CombatSpace(GroundVector.Parse("-20", "-20"), GroundVector.Parse("20", "20"), new[]
        {
            new SpaceObstacle("o", new[] { GroundVector.Parse("-5", "-5"), GroundVector.Parse("-4.5", "-5"), GroundVector.Parse("-4.5", "5"), GroundVector.Parse("-5", "5") }),
            new SpaceObstacle("e", new[] { GroundVector.Parse("4.5", "-5"), GroundVector.Parse("5", "-5"), GroundVector.Parse("5", "5"), GroundVector.Parse("4.5", "5") }),
            new SpaceObstacle("n", new[] { GroundVector.Parse("-4.5", "4.5"), GroundVector.Parse("4.5", "4.5"), GroundVector.Parse("4.5", "5"), GroundVector.Parse("-4.5", "5") }),
            new SpaceObstacle("s", new[] { GroundVector.Parse("-4.5", "-5"), GroundVector.Parse("4.5", "-5"), GroundVector.Parse("4.5", "-4.5"), GroundVector.Parse("-4.5", "-4.5") }),
        });
        var paths = new WalkPaths(closed);
        var stay = new Walker();
        Assert.Equal((0.0, 0.0), stay.Next(paths, 0, 0, 12, 0));
        Assert.True(stay.Blocked);
        Assert.Equal((0.0, 0.0), stay.Next(paths, 0, 0, 12, 0));   // aucun aller-retour : toujours sur place
        var straight = new Walker { StraightWhenBlocked = true };
        Assert.Equal((12.0, 0.0), straight.Next(paths, 0, 0, 12, 0));
        // Sans trajets (null) : ligne droite, comme avant V2.8.
        Assert.Equal((12.0, 0.0), new Walker().Next(null, 0, 0, 12, 0));
    }

    [Fact]
    public void Essai_des_chemins()
    {
        string? root = AppContext.BaseDirectory;
        while (root != null && !File.Exists(Path.Combine(root, "global.json"))) root = Path.GetDirectoryName(root);
        Assert.NotNull(root);
        string menu = File.ReadAllText(Path.Combine(root!, "unity", "Assets", "EdS", "Editor", "EdsPathsTrial.cs"));
        Assert.Contains("Éclats du Seuil/Essai des chemins : 11 h 30 (les PNJ traversent le bourg)", menu);
        Assert.Contains("Éclats du Seuil/Essai des chemins : 19 h (portes de l'auberge, passants)", menu);
        string essai = File.ReadAllText(Path.Combine(root!, "docs", "ESSAI_V1.10.md"));
        Assert.Contains("## 15. Les chemins (V2.8)", essai);
        string directory = Path.Combine(Path.GetTempPath(), "eds_v28_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "vallons.json");
            Assert.Contains("Aucune sauvegarde", PathsTrial.MoveToMarket(path));
            SaveStore.Write(path, new WorldSave("zone.vallons", 0, 0, new[] { PlayerCharacters.PlacesKey }, Hero()));
            Assert.Contains("contournent les maisons", PathsTrial.MoveToMarket(path, stamp: "essai"));
            Assert.Equal(PathsTrial.MarketMinute, new WorldClock(50).At(((WorldSave)SaveStore.Read(path).Save!).WorldTick).Minute);
            Assert.Contains("porte", PathsTrial.MoveToEvening(path, stamp: "essai2"));
            Assert.Equal(PathsTrial.EveningMinute, new WorldClock(50).At(((WorldSave)SaveStore.Read(path).Save!).WorldTick).Minute);
        }
        finally { Directory.Delete(directory, true); }
    }
}
