using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    private const string Latest = "reference-0.31.0-contenu-0.6.0-regles-0.17.0", Previous = "reference-0.29.0-contenu-0.6.0-regles-0.17.0";

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
        Assert.Equal(("0.31.0", 25, 18, 6), (CombatEngine.Version, CombatEngine.LogFormatVersion, ScenarioValidator.CurrentSchemaVersion, CombatSnapshot.FormatVersion));
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
    /// <summary>Douze sangliers qui patrouillent, chacun enfermé dans un petit enclos : chaque point de leur circuit est hors d'atteinte.</summary>
    private static CombatSetup PennedBoars() => ReadMutated(r =>
    {
        JsonObject data = r["data"]!.AsObject();
        data["space"]!["bounds"] = new JsonObject { ["min"] = new JsonArray(-40, -30), ["max"] = new JsonArray(40, 30) };
        var obstacles = new JsonArray();
        var combatants = new JsonArray();
        JsonNode bastion = data["combatants"]![0]!.DeepClone();
        bastion["position"] = new JsonArray(36, 26);
        combatants.Add(bastion);
        JsonNode wolf = data["combatants"]![1]!;
        for (int k = 0; k < 12; k++)
        {
            double cx = -30 + (k % 6) * 12, cz = k < 6 ? -10 : 10;
            JsonArray Box(double x0, double z0, double x1, double z1) => new JsonArray(new JsonArray(x0, z0), new JsonArray(x1, z0), new JsonArray(x1, z1), new JsonArray(x0, z1));
            obstacles.Add(new JsonObject { ["id"] = $"enclos{k}_o", ["points"] = Box(cx - 2.1, cz - 2.1, cx - 1.6, cz + 2.1) });
            obstacles.Add(new JsonObject { ["id"] = $"enclos{k}_e", ["points"] = Box(cx + 1.6, cz - 2.1, cx + 2.1, cz + 2.1) });
            obstacles.Add(new JsonObject { ["id"] = $"enclos{k}_n", ["points"] = Box(cx - 1.6, cz + 1.6, cx + 1.6, cz + 2.1) });
            obstacles.Add(new JsonObject { ["id"] = $"enclos{k}_s", ["points"] = Box(cx - 1.6, cz - 2.1, cx + 1.6, cz - 1.6) });
            JsonNode boar = wolf.DeepClone();
            boar["id"] = $"sanglier{k:00}";
            boar["team"] = "gibier";
            boar["position"] = new JsonArray(cx, cz);
            boar["radius"] = 0.9;
            boar["behavior"] = new JsonObject
            {
                ["maxRange"] = 0.5, ["aggroRange"] = 3, ["pack"] = false,
                ["wander"] = new JsonObject { ["style"] = "patrol", ["radius"] = 3, ["pace"] = 0.5, ["legMs"] = 2000, ["pauseMs"] = new JsonArray(100, 200) },
            };
            combatants.Add(boar);
        }
        data["space"]!["obstacles"] = obstacles;
        data["combatants"] = combatants;
        data.Remove("truces");
    }, scenario: Chemins).Setup!;

    [Fact]
    public void Etat_affiche_de_l_errance_egal_a_l_intention_sous_budget_epuise()
    {
        // Relecture de la PR #113 : douze bêtes ont besoin d'une recherche à chaque étape, huit seulement en ont une par tick (ordre qui
        // tourne avec le tick) : l'état affiché vient de la même passe que l'intention (« patrol » marche, sinon elle attend).
        CombatSetup setup = PennedBoars();
        var engine = new CombatEngine(setup, new MemoryCombatLog());
        string[] ids = Enumerable.Range(0, 12).Select(k => $"sanglier{k:00}").ToArray();
        int straight = 0, held = 0;
        for (int t = 0; t < 200; t++)
        {
            var shown = ids.ToDictionary(id => id, id => engine.View(id).Wander);
            engine.Step();
            foreach (string id in ids)
            {
                bool moving = engine.View(id).MoveDirection != null;
                Assert.True(moving == (shown[id] == "walk"), $"tick {t}, {id} : affiché « {shown[id]} », marche {moving}");
                if (moving) straight++;
                else if (shown[id] != null) held++;
            }
        }
        // Les deux cas se présentent : budget épuisé (droit, il glisse contre son enclos) et aucun chemin (il attend).
        Assert.True(straight > 0 && held > 0, $"{straight} pas droits, {held} attentes");
    }

    [Fact]
    public void Contourne_une_maison_symetrique_sans_changer_de_cote()
    {
        // Relecture de la PR #113 : devant un obstacle symétrique, le départage stable choisit un côté et le garde (aucune oscillation).
        var engine = new CombatEngine(Load(), new MemoryCombatLog());
        int side = 0;
        while (!engine.IsFinished && At(engine, "loup_maison").Z.Raw > 40_000)
        {
            engine.Step();
            long x = At(engine, "loup_maison").X.Raw;
            if (Math.Abs(x) < 5_000) continue;
            int now = Math.Sign(x);
            if (side == 0) side = now;
            Assert.Equal(side, now);
        }
        Assert.NotEqual(0, side);
    }
}

/// <summary>Sous-lot V2.8 : grille de navigation et A* (EdS.Domain), sans moteur.</summary>
[Trait("Category", "Space")]
public class V28CheminsGrilleTests
{
    private readonly Xunit.Abstractions.ITestOutputHelper output;

    public V28CheminsGrilleTests(Xunit.Abstractions.ITestOutputHelper output) => this.output = output;

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
        // Chemin tronqué au tampon : seules les premières cases comptent pour avancer, et il est rendu partiel (relecture de la PR #113).
        Assert.Equal(NavStatus.Partial, search.Find(grid, start, goal, 8_192, path, out int length));
        Assert.Equal(64, length);
        var whole = new int[4_096];
        Assert.Equal(NavStatus.Found, search.Find(grid, start, goal, 8_192, whole, out int full));
        Assert.Equal(goal, whole[full - 1]);
        Assert.Equal(start, path[0]);
        // CI de la PR #151 : mise en route d'abord (une quarantaine d'appels : la recompilation à chaud du moteur d'exécution, vers le 30e
        // appel, peut allouer sur le fil de l'appelant) ; seule la recherche elle-même est mesurée ensuite.
        for (int i = 0; i < 40; i++) search.Find(grid, start, goal, 8_192, path, out _);
        // Le compteur par fil est approximatif autour d'une collecte (le tampon d'allocation du fil est repris et recompté,
        // d'un quantum d'environ 8 Kio) ; la suite de tests, parallèle, déclenche des collectes sur d'autres fils pendant la mesure. Une région
        // sans collecte rend la lecture exacte : aucune collecte ne peut survenir entre les deux lectures.
        // Revue de la PR #153 : le budget de la région (8 Mo) est global ; des tests parallèles peuvent le dépasser et provoquer une collecte.
        // Les compteurs de collectes avant et après disent si la lecture est exacte ; sinon on refait (3 essais au plus). Sans mesure
        // exacte, le résultat est non concluant (message dans la sortie du test), pas un échec : le seuil reste 4 096 octets.
        long delta = -1;
        for (int attempt = 1; attempt <= 3 && delta < 0; attempt++)
        {
            bool region;
            try
            {
                region = GC.TryStartNoGCRegion(8_000_000);
            }
            catch (ArgumentOutOfRangeException)
            {
                region = false;   // budget refusé par ce système : repli sans région, contrôlé par les compteurs seuls
            }
            try
            {
                int gen0 = GC.CollectionCount(0), gen1 = GC.CollectionCount(1), gen2 = GC.CollectionCount(2);
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 20; i++) search.Find(grid, start, goal, 8_192, path, out _);
                long measured = GC.GetAllocatedBytesForCurrentThread() - allocated;
                if (gen0 == GC.CollectionCount(0) && gen1 == GC.CollectionCount(1) && gen2 == GC.CollectionCount(2)) delta = measured;
            }
            finally
            {
                if (region && System.Runtime.GCSettings.LatencyMode == System.Runtime.GCLatencyMode.NoGCRegion)
                {
                    try
                    {
                        GC.EndNoGCRegion();
                    }
                    catch (InvalidOperationException)
                    {
                        // région déjà terminée par une collecte : l'essai est écarté par les compteurs
                    }
                }
            }
        }
        if (delta < 0)
        {
            output.WriteLine("NON CONCLUANT : une collecte a eu lieu pendant chacun des 3 essais de mesure, la lecture par fil n'est pas exacte.");
            return;
        }
        Assert.True(delta < 4_096, $"{delta} octets alloués par 20 recherches.");
    }
    [Fact]
    public void Statut_garde_en_cache_reponses_identiques_au_premier_appel()
    {
        // Relecture de la PR #113 : un chemin partiel en cache rend le même statut, le même trajet et le même « complet » qu'au premier appel.
        var obstacles = new[] { Rect(-1, -18, 1, 20) };
        long r = R(0.5);
        var nav = new Navigator(R(-20), R(-20), R(20), R(20), obstacles, maxExpanded: 200);
        List<GroundVector> first = nav.Route(r, R(-10), R(10), R(10), R(10), out bool c1)!;
        NavSteer s1 = nav.Steer(r, R(-10), R(10), R(10), R(10), out long a1, out long b1);
        long searches = nav.Searches;
        List<GroundVector> second = nav.Route(r, R(-10), R(10), R(10), R(10), out bool c2)!;
        NavSteer s2 = nav.Steer(r, R(-10), R(10), R(10), R(10), out long a2, out long b2);
        Assert.Equal(searches, nav.Searches);   // servi par le cache
        Assert.False(c1);
        Assert.Equal(first, second);
        Assert.Equal((c1, s1, a1, b1), (c2, s2, a2, b2));
        Assert.Equal(NavSteer.Partial, s1);
    }

    [Fact]
    public void Poche_sans_progres_va_droit_jamais_bloque()
    {
        // Relecture de la PR #113 : au fond d'une poche ouverte à l'opposé du but, avec un budget trop petit pour en sortir, la case de
        // départ reste la plus proche du but : au lieu de rester sur place pour toujours, il va droit (comme avant V2.8) et glisse.
        var pocket = new[] { Rect(-0.5, -6, 0, 6), Rect(0, 5.5, 10, 6), Rect(0, -6, 10, -5.5) };
        var nav = new Navigator(R(-20), R(-20), R(20), R(20), pocket, maxExpanded: 20);
        long r = R(0.5);
        // Départ dans la case la plus à l'ouest de la poche (centre à 0,75 m, à plus de 0,625 m du fond), dans la rangée du but.
        Assert.Equal(NavSteer.Straight, nav.Steer(r, R(0.8), R(-0.1), R(-10), R(0), out long ax, out long az));
        Assert.Equal((R(-10), R(0)), (ax, az));
        // L'affichage n'a pas de trajet : le marcheur applique son repli (rester, ou aller droit).
        Assert.Null(nav.Route(r, R(0.8), R(-0.1), R(-10), R(0), out bool complete));
        Assert.False(complete);
        // Avec le budget normal, il sort de la poche par l'est.
        var normal = new Navigator(R(-20), R(-20), R(20), R(20), pocket);
        Assert.Equal(NavSteer.Path, normal.Steer(r, R(0.8), R(-0.1), R(-10), R(0), out long nx, out _));
        Assert.True(nx > R(0.8));
    }
    [Fact]
    public void Arete_diagonale_coupee_par_un_obstacle_minuscule()
    {
        // Revue Codex de la PR #113 (tour 2), cas exact : emprise 0,1 m, cases de 0,5 m (centres en 0,25 et 0,75), carré de 2 cm en
        // (0,49 ; 0,49)–(0,51 ; 0,51) : les quatre cases autour restent libres, mais les deux diagonales passent dessus.
        var square = Rect(0.49, 0.49, 0.51, 0.51);
        var grid = new NavGrid(R(-10), R(-10), R(10), R(10), new[] { square }, R(0.1));
        int sw = grid.NearestFree(R(0.25), R(0.25), 0), se = grid.NearestFree(R(0.75), R(0.25), 0);
        int nw = grid.NearestFree(R(0.25), R(0.75), 0), ne = grid.NearestFree(R(0.75), R(0.75), 0);
        Assert.Equal((R(0.25), R(0.25), R(0.75), R(0.75)), (grid.CellX(sw), grid.CellZ(sw), grid.CellX(ne), grid.CellZ(ne)));
        Assert.True(grid.IsFree(sw) && grid.IsFree(se) && grid.IsFree(nw) && grid.IsFree(ne));
        Assert.Equal(-1, grid.Neighbor(sw, 1));   // nord-est
        Assert.Equal(-1, grid.Neighbor(se, 3));   // nord-ouest
        Assert.Equal(-1, grid.Neighbor(ne, 5));   // sud-ouest (même arête vue de l'autre bout)
        Assert.Equal(se, grid.Neighbor(sw, 0));   // l'orthogonale, à 0,24 m du carré, reste ouverte
        var path = new int[16];
        Assert.Equal(NavStatus.Found, new NavSearch().Find(grid, sw, ne, 100, path, out int length));
        Assert.Equal(3, length);   // par un côté, jamais au travers
        for (int k = 1; k < length; k++)
            Assert.False(square.OverlapsCapsule(grid.CellX(path[k - 1]), grid.CellZ(path[k - 1]), grid.CellX(path[k]), grid.CellZ(path[k]), R(0.1)));
    }

    [Fact]
    public void Arete_orthogonale_coupee_par_un_mur_fin()
    {
        // Mur de 2 cm entre deux rangées de centres libres (0,24 m de part et d'autre) : on en fait le tour, on ne le traverse pas.
        var wall = Rect(0.49, -5, 0.51, 5);
        long r = R(0.1);
        var grid = new NavGrid(R(-10), R(-10), R(10), R(10), new[] { wall }, r);
        int west = grid.NearestFree(R(0.25), R(0.25), 0), east = grid.NearestFree(R(0.75), R(0.25), 0);
        Assert.True(grid.IsFree(west) && grid.IsFree(east));
        Assert.Equal(-1, grid.Neighbor(west, 0));
        Assert.Equal(-1, grid.Neighbor(east, 4));
        Assert.Equal(1, grid.ComponentCount);   // on passe par les bouts du mur
        var nav = new Navigator(R(-10), R(-10), R(10), R(10), new[] { wall });
        Assert.Equal(NavSteer.Path, nav.Steer(r, R(0.25), R(0.25), R(0.75), R(0.25), out _, out _));
        List<GroundVector> route = nav.Route(r, R(0.25), R(0.25), R(0.75), R(0.25))!;
        Assert.True(route.Count >= 2, $"{route.Count} points");
        AssertClearRoute(nav, r, (0.25, 0.25), route);
        Assert.True(route.Max(p => Math.Abs(p.Z.Raw)) > R(5), "le trajet passe par un bout du mur");
    }

    [Fact]
    public void Toutes_les_aretes_ouvertes_sont_libres_pour_l_emprise()
    {
        // Revue Codex de la PR #113 (tour 2), exhaustif : sur une aire semée d'obstacles minuscules, fins, en biais et ordinaires, pour
        // trois emprises, chaque arête rendue par Neighbor est libre (gélule exacte), et chaque arête refusée entre deux cases libres
        // (hors coin coupé) touche un obstacle.
        var obstacles = new List<ConvexPolygon>
        {
            Rect(0.49, 0.49, 0.51, 0.51), Rect(-3.01, -6, -2.99, 6), Rect(2, -1, 6, 3), Rect(-7.2, 4.4, -6.9, 4.7),
            new ConvexPolygon(new[] { P(4, -7), P(7, -4), P(6.98, -3.98), P(3.98, -6.98) }),   // mur fin en biais
            new ConvexPolygon(new[] { P(-6, -6), P(-4, -7), P(-3.5, -5), P(-5.5, -4) }),
        };
        foreach (double radius in new[] { 0.1, 0.35, 0.6 })
        {
            long sweep = Navigator.RadiusClass(R(radius));
            var grid = new NavGrid(R(-10), R(-10), R(10), R(10), obstacles, sweep);
            int open = 0, closed = 0;
            for (int c = 0; c < grid.Count; c++)
            {
                if (!grid.IsFree(c)) continue;
                for (int d = 0; d < 8; d++)
                {
                    int col = c % grid.Width + new[] { 1, 1, 0, -1, -1, -1, 0, 1 }[d], row = c / grid.Width + new[] { 0, 1, 1, 1, 0, -1, -1, -1 }[d];
                    if (col < 0 || row < 0 || col >= grid.Width || row >= grid.Height) continue;
                    int nb = row * grid.Width + col;
                    if (!grid.IsFree(nb)) continue;
                    bool hits = obstacles.Any(o => o.OverlapsCapsule(grid.CellX(c), grid.CellZ(c), grid.CellX(nb), grid.CellZ(nb), sweep));
                    int got = grid.Neighbor(c, d);
                    if (got >= 0)
                    {
                        Assert.Equal(nb, got);
                        Assert.False(hits, $"rayon {radius} : arête {c} → {nb} ouverte à travers un obstacle");
                        open++;
                    }
                    else
                    {
                        bool corner = NavGrid.Diagonal(d) && (!grid.IsFree(c - c % grid.Width + col) || !grid.IsFree(row * grid.Width + c % grid.Width));
                        Assert.True(hits || corner, $"rayon {radius} : arête {c} → {nb} fermée sans raison");
                        closed++;
                    }
                }
            }
            Assert.True(open > 0 && closed > 0);
        }
    }

    [Fact]
    public void Trajets_et_points_vises_libres_avec_obstacles_fins()
    {
        // Les trajets lissés (affichage) et le point visé (moteur) ne traversent jamais un obstacle fin, depuis des départs quelconques.
        var obstacles = new[] { Rect(0.49, 0.49, 0.51, 0.51), Rect(-3.01, -6, -3, 6), Rect(2.99, -8, 3.01, 2) };
        var nav = new Navigator(R(-10), R(-10), R(10), R(10), obstacles);
        foreach (double radius in new[] { 0.1, 0.35 })
        {
            long r = R(radius);
            for (int i = 0; i < 40; i++)
            {
                double fx = -9 + (i * 7) % 18 + 0.13, fz = -9 + (i * 5) % 18 + 0.07, tx = 9 - (i * 3) % 18 - 0.11, tz = -9 + (i * 11) % 18 + 0.21;
                if (obstacles.Any(o => o.OverlapsCircle(R(fx), R(fz), r)) || obstacles.Any(o => o.OverlapsCircle(R(tx), R(tz), r))) continue;
                List<GroundVector>? route = nav.Route(r, R(fx), R(fz), R(tx), R(tz));
                if (route is null) continue;
                // Le premier segment part de la place exacte : il est libre, ou mène au centre de la case de départ (à moins d'une case).
                long ax = R(fx), az = R(fz);
                for (int k = 0; k < route.Count; k++)
                {
                    GroundVector p = route[k];
                    bool clear = nav.Clear(ax, az, p.X.Raw, p.Z.Raw, r);
                    Assert.True(clear || (k == 0 && Planar.WithinDistance(ax, az, p.X.Raw, p.Z.Raw, R(0.75))), $"trajet {i}, rayon {radius}, segment {k} vers {p}");
                    ax = p.X.Raw;
                    az = p.Z.Raw;
                }
            }
        }
    }
    [Fact]
    public void Depart_et_but_rattaches_a_une_case_atteignable_jamais_derriere_un_mur_fin()
    {
        // Revue Codex de la PR #113 (tour 3), cas exact : aire (−2, −2)–(2, 2), emprise 0,1 m, mur fin (0,10 ; −2)–(0,12 ; 2), obstacle
        // (0,60 ; −0,50)–(0,70 ; 0,50), départ (0,23 ; 0), but (1,50 ; 0). La case libre la plus proche du départ, (−0,25 ; −0,25), est de
        // l'autre côté du mur : elle n'est plus choisie ; la créature contourne l'obstacle au lieu de rester immobile.
        var wall = Rect(0.10, -2, 0.12, 2);
        var block = Rect(0.60, -0.50, 0.70, 0.50);
        var nav = new Navigator(R(-2), R(-2), R(2), R(2), new[] { wall, block });
        long r = R(0.1);
        NavGrid grid = nav.Grid(r);
        int nearest = grid.NearestFree(R(0.23), 0, Navigator.StartReach);
        Assert.Equal((R(-0.25), R(-0.25)), (grid.CellX(nearest), grid.CellZ(nearest)));   // l'ancien rattachement, derrière le mur
        int[]? cells = nav.Cells(r, R(0.23), 0, R(1.5), 0, out NavStatus status);
        Assert.NotNull(cells);
        Assert.Equal(NavStatus.Found, status);
        Assert.True(grid.CellX(cells![0]) > R(0.12), $"départ rattaché en {grid.CellX(cells[0])}");
        Assert.True(nav.Clear(R(0.23), 0, grid.CellX(cells[0]), grid.CellZ(cells[0]), r));
        Assert.Equal(NavSteer.Path, nav.Steer(r, R(0.23), 0, R(1.5), 0, out long ax, out long az));
        Assert.True(ax > R(0.12) && nav.Clear(R(0.23), 0, ax, az, r), $"point visé ({ax}, {az})");
        List<GroundVector> route = nav.Route(r, R(0.23), 0, R(1.5), 0)!;
        Assert.Equal(P(1.5, 0), route[^1]);
        AssertClearRoute(nav, r, (0.23, 0), route);
        // Même chose pour le but : (0,23 ; 0) depuis (1,50 ; 0).
        int[]? back = nav.Cells(r, R(1.5), 0, R(0.23), 0, out NavStatus backStatus);
        Assert.NotNull(back);
        Assert.Equal(NavStatus.Found, backStatus);
        Assert.True(grid.CellX(back![^1]) > R(0.12));
        AssertClearRoute(nav, r, (1.5, 0), nav.Route(r, R(1.5), 0, R(0.23), 0)!);
    }

    [Fact]
    public void Coince_sans_case_atteignable_va_droit()
    {
        // Emprise de 0,1 m serrée entre deux murs fins à 0,11 m : aucune case libre n'est atteignable en ligne droite : il va droit et
        // glisse (comme avant V2.8), il ne reste pas immobile pour toujours.
        var nav = new Navigator(R(-5), R(-5), R(5), R(5), new[] { Rect(-0.13, -1, -0.11, 1), Rect(0.11, -1, 0.13, 1), Rect(-0.13, 1, 0.13, 1.02), Rect(-0.13, -1.02, 0.13, -1) });
        long r = R(0.1);
        Assert.Equal(NavSteer.Straight, nav.Steer(r, 0, 0, R(3), 0, out long ax, out long az));
        Assert.Equal((R(3), 0L), (ax, az));
        Assert.Null(nav.Route(r, 0, 0, R(3), 0));
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

    [Fact]
    public void Marcheur_reprend_un_chemin_partiel_jusqu_au_but()
    {
        // Relecture de la PR #113 : budget de 40 cases par recherche, un muret près du départ et un long trajet ensuite : le premier
        // trajet est partiel (au-delà du muret), le marcheur le reprend depuis son bout (au plus Walker.MaxResumes fois) et arrive sans
        // traverser le muret.
        var wall = new SpaceObstacle("mur", new[] { GroundVector.Parse("-15.5", "8"), GroundVector.Parse("-14.5", "8"), GroundVector.Parse("-14.5", "10.2"), GroundVector.Parse("-15.5", "10.2") });
        var paths = new WalkPaths(new CombatSpace(GroundVector.Parse("-20", "-20"), GroundVector.Parse("20", "20"), new[] { wall }), maxExpanded: 40);
        List<(double X, double Z)>? first = paths.Route(-18, 10.5, 10, 10.5, out bool complete);
        Assert.NotNull(first);
        Assert.False(complete);
        var walker = new Walker();
        double x = -18, z = 10.5;
        var poly = new ConvexPolygon(wall.Points);
        for (int step = 0; step < 4_000; step++)
        {
            (double tx, double tz) = walker.Next(paths, x, z, 10, 10.5);
            double dx = tx - x, dz = tz - z, d = Math.Sqrt(dx * dx + dz * dz);
            if (d < 1e-9) break;
            double move = Math.Min(d, 0.1);
            x += dx / d * move;
            z += dz / d * move;
            Assert.False(poly.OverlapsCircle((long)Math.Round(x * 1e4), (long)Math.Round(z * 1e4), 1_000), $"dans le mur en ({x:0.00}, {z:0.00})");
        }
        Assert.True(Math.Abs(x - 10) < 0.06 && Math.Abs(z - 10.5) < 0.06, $"arrêté en ({x:0.00}, {z:0.00})");
        Assert.False(walker.Blocked);
        // Un budget trop petit pour progresser assez avant MaxResumes reprises : il s'arrête (sans aller-retour) au lieu de tourner en rond.
        var tight = new WalkPaths(new CombatSpace(GroundVector.Parse("-20", "-20"), GroundVector.Parse("20", "20"), new[] { wall }), maxExpanded: 3);
        var stuck = new Walker();
        (double X, double Z) last = (-18, 10);
        for (int i = 0; i < Walker.MaxResumes + 3; i++) last = stuck.Next(tight, last.X, last.Z, 10, 10);
        Assert.True(stuck.Blocked);
        Assert.Equal(last, stuck.Next(tight, last.X, last.Z, 10, 10));
    }

    [Fact]
    public void Marcheur_relance_son_trajet_quand_les_trajets_changent()
    {
        // Relecture de la PR #113 : même but, mais la zone a été rouverte (AdventureSession.Paths reconstruit, décor différent).
        var open = new WalkPaths(new CombatSpace(GroundVector.Parse("-20", "-20"), GroundVector.Parse("20", "20")));
        var walled = new WalkPaths(new CombatSpace(GroundVector.Parse("-20", "-20"), GroundVector.Parse("20", "20"), new[]
        {
            new SpaceObstacle("mur", new[] { GroundVector.Parse("-1", "-5"), GroundVector.Parse("1", "-5"), GroundVector.Parse("1", "5"), GroundVector.Parse("-1", "5") }),
        }));
        var walker = new Walker();
        Assert.Equal((10.0, 0.0), walker.Next(open, -10, 0, 10, 0));
        (double X, double Z) aim = walker.Next(walled, -10, 0, 10, 0);
        Assert.NotEqual((10.0, 0.0), aim);
        Assert.True(Math.Abs(aim.Z) > 5, $"point visé {aim}");
    }
}
