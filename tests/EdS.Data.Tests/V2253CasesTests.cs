using System.Text.Json.Nodes;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Tools.Map;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.25.3 (grande carte, étape 1 : « cases et repère », docs/PLAN_GRANDE_CARTE.md § 2.1 et § 7) : schéma de zone 13 (région :
/// grille de cases de 128 m, secteurs de 256 m, repère unique), découpage du cœur des Vallons dans ce repère sans rien changer au jeu,
/// bornes relevées et bornes par case, outil <c>eds carte</c>.
/// </summary>
[Trait("Category", "World")]
public class V2253CasesTests
{
    private static string Scenarios => Path.Combine(AppContext.BaseDirectory, "scenarios");
    private static ItemCatalog Items { get; } = ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!;
    private static readonly Lazy<ZoneDefinition> Shipped = new(() => Read(ZoneJson()).Zone!);
    private static ZoneDefinition Vallons => Shipped.Value;

    private static readonly string[] CoreCells = { "c14_r12", "c15_r12", "c14_r13", "c15_r13" };

    private static JsonObject ZoneJson() => JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!.AsObject();
    private static ZoneLoadResult Read(JsonObject root) => ZoneJsonReader.Read(root.ToJsonString(), Scenarios, ShippedData, Items);
    private static ZoneLoadResult Mutated(Action<JsonObject> mutate)
    {
        JsonObject root = ZoneJson();
        mutate(root);
        return Read(root);
    }

    private static JsonObject Region(JsonObject root) => root["data"]!["region"]!.AsObject();

    /// <summary>Les Vallons au schéma 12, sans région : l'ancien fichier.</summary>
    private static JsonObject Schema12()
    {
        JsonObject root = ZoneJson();
        root["schemaVersion"] = 12;
        root["data"]!.AsObject().Remove("region");
        return root;
    }

    private static void AssertError(ZoneLoadResult r, string path, ValidationCode code) =>
        Assert.True(r.Report.Errors.Any(e => e.Path == path && e.Code == code), $"erreur {code} attendue en {path} ; rapport : {r.Report}");

    private static GroundVector P(double x, double z) => new(Fixed.Parse(x.ToString(System.Globalization.CultureInfo.InvariantCulture)), Fixed.Parse(z.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    // ---- Repère ----

    [Fact]
    public void Repere_du_plan_32_x_24_cases_192_secteurs()
    {
        Assert.Equal((13, 12), (ZoneValidator.CurrentSchemaVersion, ZoneValidator.SpreadSchemaVersion));
        RegionGrid g = Vallons.Region!;
        Assert.Equal((-1908, -1614, 32, 24), (g.OriginX, g.OriginZ, g.Columns, g.Rows));
        Assert.Equal((P(-1908, -1614), P(2188, 1458)), (g.Min, g.Max));
        Assert.Equal((768, 16, 12, 192), (g.CellCount, g.SectorColumns, g.SectorRows, g.SectorCount));
        Assert.Equal((128, 2, 256), (RegionGrid.CellMeters, RegionGrid.SectorCells, RegionGrid.SectorMeters));
        RegionGrid plan = RegionGrid.Vallons();
        Assert.Equal((plan.OriginX, plan.OriginZ, plan.Columns, plan.Rows), (g.OriginX, g.OriginZ, g.Columns, g.Rows));
        Assert.Equal(768, g.Cells.Count);
        Assert.Equal(g.Cells.OrderBy(c => c).ToList(), g.Cells);   // rangées du sud au nord, colonnes d'ouest en est
        Assert.Equal(("c0_r0", "s00_00"), (g.Cells[0].Name, g.Cells[0].SectorName));
        Assert.Equal(("c31_r23", "s15_11"), (g.Cells[^1].Name, g.Cells[^1].SectorName));
    }

    [Fact]
    public void Aire_actuelle_entiere_dans_les_quatre_cases_du_secteur_s07_06()
    {
        RegionGrid g = Vallons.Region!;
        CombatSpace space = Vallons.Setup.Space!;
        Assert.Equal((P(-100, -70), P(140, 100)), (space.Min, space.Max));
        // L'aire tient dans les quatre cases du secteur s07_06 ; son bord est (x = 140) est le bord de c16 (revue Codex de la PR #148 :
        // un rectangle fermé touche toutes les cases de ses points au sens de CellOf).
        IReadOnlyList<RegionCell> cells = CoreCells.Select(n => g.Cells.Single(c => c.Name == n)).ToList();
        Assert.All(cells, c => Assert.Equal("s07_06", c.SectorName));
        Assert.Equal((P(-116, -78), P(140, 178)), (g.CellMin(cells[0]), g.CellMax(cells[^1])));
        Assert.True(space.Min.X >= g.CellMin(cells[0]).X && space.Min.Z >= g.CellMin(cells[0]).Z && space.Max.X <= g.CellMax(cells[^1]).X && space.Max.Z <= g.CellMax(cells[^1]).Z);
        Assert.Equal(new[] { "c14_r12", "c15_r12", "c16_r12", "c14_r13", "c15_r13", "c16_r13" }, g.CellsCovering(space.Min, space.Max).Select(c => c.Name));
    }

    [Fact]
    public void Bords_des_cases()
    {
        RegionGrid g = RegionGrid.Vallons();
        Assert.Equal("c0_r0", g.CellOf(P(-1908, -1614)).Name);
        Assert.Equal("c0_r0", g.CellOf(P(-1780.0001, -1486.0001)).Name);
        Assert.Equal("c1_r1", g.CellOf(P(-1780, -1486)).Name);   // un bord entre deux cases est à la case de l'est et du nord
        Assert.Equal("c15_r12", g.CellOf(P(12, -78)).Name);
        Assert.Equal("c14_r12", g.CellOf(P(11.9999, -78)).Name);
        Assert.Equal("c31_r23", g.CellOf(P(2188, 1458)).Name);   // bords est et nord de la région : dernière case
        Assert.Equal("c16_r12", g.CellOf(P(140, 0)).Name);
        Assert.Throws<ArgumentOutOfRangeException>(() => g.CellOf(P(2188.0001, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => g.CellOf(P(0, -1614.0001)));
        // Revue Codex de la PR #148 : un rectangle fermé touche les cases de tous ses points au sens de CellOf, bords est et nord compris.
        Assert.Equal(new[] { "c15_r12", "c16_r12", "c15_r13", "c16_r13" }, g.CellsCovering(P(12, -78), P(140, 50)).Select(c => c.Name));
        Assert.Equal(new[] { "c15_r12" }, g.CellsCovering(P(12, -78), P(139.9999, 49.9999)).Select(c => c.Name));
        Assert.Equal(new[] { "c16_r12" }, g.CellsCovering(P(140, 0), P(140, 10)).Select(c => c.Name));
        Assert.Equal(new[] { "c15_r12", "c16_r12" }, g.CellsCovering(P(139, 0), P(141, 10)).Select(c => c.Name));
        Assert.Throws<ArgumentOutOfRangeException>(() => g.CellsCovering(P(10, 0), P(0, 10)));
        // Ligne brisée : réunion des boîtes des tronçons, sans doublon, rangée.
        Assert.Equal(new[] { "c14_r12", "c15_r12", "c15_r13" },
            g.CellsAlong(new[] { P(0, 0), P(20, 0), P(20, 60) }).Select(c => c.Name));
        // Revue Codex de la PR #148 : un sommet posé sur une frontière de case est dans la case de CellOf, avec la route.
        Assert.Equal(new[] { "c14_r12", "c15_r12" }, g.CellsAlong(new[] { P(0, 0), P(12, 0) }).Select(c => c.Name));
        Assert.Equal(new[] { "c15_r12", "c15_r13" }, g.CellsAlong(new[] { P(20, 0), P(20, 50) }).Select(c => c.Name));
        Assert.Equal(new[] { "c15_r12" }, g.CellsAlong(new[] { P(12, 0), P(12, 40) }).Select(c => c.Name));
        foreach (GroundVector[] road in new[] { new[] { P(0, 0), P(12, 0) }, new[] { P(-116, -78), P(12, 50), P(140, -78) } })
            Assert.All(road, v => Assert.Contains(g.CellOf(v), g.CellsAlong(road)));
        // Lieux et obstacles : un coin sur une frontière est dans la case de CellOf.
        foreach ((GroundVector min, GroundVector max) in new[] { (P(0, 0), P(12, 10)), (P(0, -100), P(10, -78)), (P(-116, 0), P(12, 50)) })
            foreach (GroundVector corner in new[] { min, max, new GroundVector(min.X, max.Z), new GroundVector(max.X, min.Z) })
                Assert.Contains(g.CellOf(corner), g.CellsCovering(min, max));
        Assert.Contains(g.CellOf(P(12, 10)), g.CellsOfPolygon(new[] { P(0, 0), P(12, 10), P(0, 10) }));
        Assert.True(g.TryParseCell("c14_r12", out RegionCell parsed) && parsed == new RegionCell(14, 12));
        foreach (string bad in new[] { "c32_r0", "c0_r24", "c014_r1", "c_r1", "x1_r1", "c1r1", "", "c-1_r0" })
            Assert.False(g.TryParseCell(bad, out _), bad);
        Assert.Throws<ArgumentOutOfRangeException>(() => new RegionGrid(0, 0, 3, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RegionGrid(0, 0, 2, 66));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RegionGrid(100_001, 0, 2, 2));
        new RegionGrid(-100_000, 100_000, 64, 64);   // bornes comprises, sans débordement
    }

    // ---- Découpage du cœur ----

    [Fact]
    public void Decoupage_du_coeur_chaque_element_dans_sa_case()
    {
        ZoneDefinition z = Vallons;
        RegionGrid g = z.Region!;
        IReadOnlyList<RegionCellContent> cut = RegionCutting.Cut(z);
        Assert.Equal(768, cut.Count);
        Assert.Equal(g.Cells, cut.Select(c => c.Cell));
        // Le cœur, plus les cases c16 que touchent les lieux dont le bord est est celui de l'aire (x = 140).
        Assert.Equal(new[] { "c14_r12", "c15_r12", "c16_r12", "c14_r13", "c15_r13", "c16_r13" }, cut.Where(c => c.Total > 0).Select(c => c.Cell.Name));
        Assert.All(cut.Where(c => c.Cell.Column == 16), c => Assert.Equal(c.Total, c.Places.Count));
        RegionCellContent At(string name) => cut.Single(c => c.Cell.Name == name);
        bool In(GroundVector p, RegionCell c) => p.X >= g.CellMin(c).X && p.X < g.CellMax(c).X && p.Z >= g.CellMin(c).Z && p.Z < g.CellMax(c).Z;
        // Rectangle fermé (revue Codex de la PR #148) : la case contient un de ses points au sens de CellOf (bords ouest et sud compris).
        bool Meets(GroundVector min, GroundVector max, RegionCell c) =>
            min.X < g.CellMax(c).X && max.X >= g.CellMin(c).X && min.Z < g.CellMax(c).Z && max.Z >= g.CellMin(c).Z;
        // Éléments ponctuels : exactement une case, celle qui contient le point.
        void Point(string id, GroundVector p, Func<RegionCellContent, IReadOnlyList<string>> list)
        {
            RegionCellContent[] holders = cut.Where(c => list(c).Contains(id)).ToArray();
            Assert.True(holders.Length == 1, $"{id} : {holders.Length} cases");
            Assert.True(In(p, holders[0].Cell), $"{id} en {p} hors de {holders[0].Cell}");
        }
        foreach (SafePoint s in z.SafePoints) Point(s.Id, s.Position, c => c.SafePoints);
        foreach (Door d in z.Doors) Point(d.Id, d.Position, c => c.Doors);
        foreach (NightSpot n in z.NightSpots) Point(n.Npc, n.Position, c => c.NightSpots);
        foreach (Population p in z.Populations) Point(p.Creature, p.Template.Placement!.Position, c => c.Populations);
        // Rectangles : toutes les cases que l'intérieur rencontre, et elles seules.
        void Box(string id, GroundVector min, GroundVector max, Func<RegionCellContent, IReadOnlyList<string>> list)
        {
            var holders = cut.Where(c => list(c).Contains(id)).Select(c => c.Cell).ToList();
            var expected = g.Cells.Where(c => Meets(min, max, c)).ToList();
            Assert.Equal(expected, holders);
        }
        foreach (Place p in z.Places) Box(p.Id, p.Min, p.Max, c => c.Places);
        foreach (SpaceObstacle o in z.Setup.Space!.Obstacles)
            Box(o.Id, new GroundVector(o.Points.Min(p => p.X), o.Points.Min(p => p.Z)), new GroundVector(o.Points.Max(p => p.X), o.Points.Max(p => p.Z)), c => c.Obstacles);
        // Routes : chaque point de la route est dans une de ses cases, et chacune de ses cases touche la boîte d'un tronçon.
        foreach (Road r in z.Roads)
        {
            var holders = cut.Where(c => c.Roads.Contains(r.Id)).Select(c => c.Cell).ToList();
            Assert.All(r.Points, p => Assert.Contains(g.CellOf(p), holders));
            Assert.All(holders, c => Assert.True(Enumerable.Range(1, r.Points.Count - 1).Any(i =>
            {
                GroundVector a = r.Points[i - 1], b = r.Points[i];
                var min = new GroundVector(Fixed.Min(a.X, b.X), Fixed.Min(a.Z, b.Z));
                var max = new GroundVector(Fixed.Max(a.X, b.X), Fixed.Max(a.Z, b.Z));
                return Meets(min, max, c);
            }), $"{r.Id} : {c}"));
        }
        // Totaux : chaque élément compté (au moins une fois), rien d'autre.
        Assert.Equal(z.SafePoints.Count, cut.Sum(c => c.SafePoints.Count));
        Assert.Equal(z.Doors.Count, cut.Sum(c => c.Doors.Count));
        Assert.Equal(z.Populations.Count, cut.Sum(c => c.Populations.Count));
        Assert.Equal(z.Setup.Space!.Obstacles.Select(o => o.Id).OrderBy(x => x, StringComparer.Ordinal),
            cut.SelectMany(c => c.Obstacles).Distinct().OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal(z.Places.Select(p => p.Id).OrderBy(x => x, StringComparer.Ordinal), cut.SelectMany(c => c.Places).Distinct().OrderBy(x => x, StringComparer.Ordinal));
        // Le lieu qui couvre tout le cœur est dans les quatre cases ; la place du village est dans c14_r12.
        Assert.Contains("place_du_village", At("c14_r12").SafePoints);
        // Bornes par case respectées par le cœur.
        Assert.All(cut, c => Assert.True(c.Places.Count <= ZoneValidator.MaxPlacesPerCell && c.Doors.Count <= ZoneValidator.MaxDoorsPerCell
            && c.Roads.Count <= ZoneValidator.MaxRoadsPerCell, c.Cell.Name));
    }

    [Fact]
    public void Decoupage_deterministe()
    {
        ZoneDefinition other = Read(ZoneJson()).Zone!;
        string Dump(ZoneDefinition z) => string.Join("\n", RegionCutting.Cut(z).Select(c => c.Cell.Name + ":" + string.Join(",", c.Obstacles) + "|" + string.Join(",", c.Places)
            + "|" + string.Join(",", c.SafePoints) + "|" + string.Join(",", c.Doors) + "|" + string.Join(",", c.Roads) + "|" + string.Join(",", c.Populations) + "|" + string.Join(",", c.NightSpots)));
        Assert.Equal(Dump(Vallons), Dump(other));
        Assert.Equal(Dump(Vallons), Dump(Vallons.ForLevel(12)));   // la région suit la zone mise au niveau du joueur
        Assert.Same(Vallons.Region, Vallons.ForLevel(12).Region);
        Assert.Throws<ArgumentException>(() => RegionCutting.Cut(Read(Schema12()).Zone!));
    }

    // ---- Rien ne change au jeu ----

    [Fact]
    public void Schema_12_encore_lu_et_meme_jeu()
    {
        ZoneLoadResult old = Read(Schema12());
        Assert.True(old.IsValid, old.Report.ToString());
        Assert.Equal((12, (RegionGrid?)null), (old.Zone!.SchemaVersion, old.Zone.Region));
        ZoneDefinition a = Vallons, b = old.Zone;
        Assert.Equal(a.Setup.Space!.Obstacles.Count, b.Setup.Space!.Obstacles.Count);
        Assert.Equal((a.Places.Count, a.SafePoints.Count, a.Doors.Count, a.Roads.Count, a.Populations.Count, a.NightSpots.Count, a.Events.Count),
            (b.Places.Count, b.SafePoints.Count, b.Doors.Count, b.Roads.Count, b.Populations.Count, b.NightSpots.Count, b.Events.Count));
        // Même simulation : mêmes empreintes du moteur, tick après tick, et même sauvegarde.
        WorldSession wa = V11TestData.Play(a, V11TestData.Hero()), wb = V11TestData.Play(b, V11TestData.Hero());
        for (int i = 0; i < 200; i++)
        {
            wa.Step();
            wb.Step();
        }
        Assert.Equal(wa.Zone.Engine.Snapshot().Fingerprint, wb.Zone.Engine.Snapshot().Fingerprint);
        Assert.Equal(SaveJsonReader.Write(wa.Save()), SaveJsonReader.Write(wb.Save()));
        // Les bornes d'avant restent celles d'une zone sans région.
        JsonObject many = Schema12();
        JsonArray places = many["data"]!["places"]!.AsArray();
        for (int i = places.Count; i <= ZoneValidator.MaxPlaces; i++)
        {
            JsonObject copy = places[0]!.DeepClone().AsObject();
            copy["id"] = "lieu_copie_" + i;
            places.Add(copy);
        }
        AssertError(Read(many), "$.data.places", ValidationCode.LimitExceeded);
        // La région demande le schéma 13.
        JsonObject early = ZoneJson();
        early["schemaVersion"] = 12;
        AssertError(Read(early), "$.data.region", ValidationCode.InvalidValue);
    }

    // ---- Validation ----

    [Fact]
    public void Region_valide_et_refus_au_chemin_precis()
    {
        Assert.True(Read(ZoneJson()).IsValid);
        ZoneLoadResult small = Mutated(r =>   // plus petite région qui tient l'aire : les quatre cases du cœur
        {
            Region(r)["origin"] = new JsonArray(-116, -78);
            Region(r)["columns"] = 2;
            Region(r)["rows"] = 2;
        });
        Assert.True(small.IsValid, small.Report.ToString());
        Assert.Equal(4, small.Zone!.Region!.CellCount);

        AssertError(Mutated(r => Region(r)["origin"] = new JsonArray(-1908.5, -1614)), "$.data.region.origin", ValidationCode.InvalidValue);
        AssertError(Mutated(r => Region(r)["origin"] = new JsonArray(-100_001, -1614)), "$.data.region.origin", ValidationCode.InvalidValue);
        AssertError(Mutated(r => Region(r).Remove("origin")), "$.data.region.origin", ValidationCode.MissingField);
        AssertError(Mutated(r => Region(r)["columns"] = 31), "$.data.region.columns", ValidationCode.OutOfRange);
        AssertError(Mutated(r => Region(r)["columns"] = 66), "$.data.region.columns", ValidationCode.OutOfRange);
        AssertError(Mutated(r => Region(r)["rows"] = 0), "$.data.region.rows", ValidationCode.OutOfRange);
        AssertError(Mutated(r => Region(r).Remove("rows")), "$.data.region.rows", ValidationCode.MissingField);
        AssertError(Mutated(r => Region(r)["cellSize"] = 64), "$.data.region.cellSize", ValidationCode.UnknownField);
        // L'aire praticable doit tenir dans la région.
        AssertError(Mutated(r => Region(r)["origin"] = new JsonArray(-99, -1614)), "$.data.region", ValidationCode.OutOfRange);
        AssertError(Mutated(r => { Region(r)["origin"] = new JsonArray(-120, -78); Region(r)["rows"] = 2; Region(r)["columns"] = 2; }),
            "$.data.region", ValidationCode.OutOfRange);   // bord est de la région en 136, celui de l'aire en 140
    }

    [Fact]
    public void Bornes_relevees_et_bornes_par_case()
    {
        // Plus de 64 lieux au total : accepté avec une région, tant que chaque case en a 64 au plus.
        JsonObject spread = ZoneJson();
        JsonArray places = spread["data"]!["places"]!.AsArray();
        int before = places.Count;
        for (int i = 0; places.Count <= ZoneValidator.MaxPlaces; i++)
        {
            // Des lieux d'un mètre, chacun dans une case du cœur, à tour de rôle : au plus 64 par case (le cœur en a déjà).
            int cx = i % 2 == 0 ? -90 : 30, cz = (i / 2) % 2 == 0 ? -60 : 60;
            places.Add(new JsonObject { ["id"] = "lieu_" + i, ["name"] = "Lieu " + i, ["min"] = new JsonArray(cx, cz), ["max"] = new JsonArray(cx + 1, cz + 1) });
        }
        ZoneLoadResult ok = Read(spread);
        Assert.True(ok.IsValid, ok.Report.ToString());
        Assert.True(ok.Zone!.Places.Count > ZoneValidator.MaxPlaces);
        Assert.All(RegionCutting.Cut(ok.Zone), c => Assert.True(c.Places.Count <= ZoneValidator.MaxPlacesPerCell));

        // 65 lieux dans une case : refusé au premier qui déborde, avec le nom de la case.
        JsonObject crowded = ZoneJson();
        JsonArray cp = crowded["data"]!["places"]!.AsArray();
        int inCell = RegionCutting.Cut(Vallons).Single(c => c.Cell.Name == "c14_r12").Places.Count;
        int start = cp.Count;
        for (int i = 0; i < ZoneValidator.MaxPlacesPerCell - inCell + 1; i++)
            cp.Add(new JsonObject { ["id"] = "lieu_" + i, ["name"] = "Lieu " + i, ["min"] = new JsonArray(-90, -60), ["max"] = new JsonArray(-89, -59) });
        ZoneLoadResult refused = Read(crowded);
        ValidationError e = Assert.Single(refused.Report.Errors);
        Assert.Equal(($"$.data.places[{cp.Count - 1}]", ValidationCode.LimitExceeded), (e.Path, e.Code));
        Assert.Contains("case c14_r12", e.Message, StringComparison.Ordinal);
        Assert.Null(refused.Zone);

        // Au total : 2 048 lieux au plus (le lecteur s'arrête à la même borne).
        JsonObject huge = ZoneJson();
        JsonArray hp = huge["data"]!["places"]!.AsArray();
        while (hp.Count <= ZoneValidator.MaxRegionPlaces) hp.Add(hp[0]!.DeepClone());
        AssertError(Read(huge), "$.data.places", ValidationCode.LimitExceeded);

        // Portes et routes : 16 et 32 par case.
        JsonObject doors = ZoneJson();
        JsonArray dl = doors["data"]!["doors"]!.AsArray();
        for (int i = 0; i < ZoneValidator.MaxDoorsPerCell; i++)
        {
            JsonObject copy = dl[0]!.DeepClone().AsObject();
            copy["id"] = "porte_copie_" + i;
            dl.Add(copy);
        }
        ZoneLoadResult dr = Read(doors);
        Assert.Contains(dr.Report.Errors, x => x.Path.StartsWith("$.data.doors[", StringComparison.Ordinal) && x.Code == ValidationCode.LimitExceeded && x.Message.Contains("case c", StringComparison.Ordinal));
        JsonObject roads = ZoneJson();
        JsonArray rl = roads["data"]!["roads"]!.AsArray();
        for (int i = 0; i < ZoneValidator.MaxRoadsPerCell; i++)
        {
            JsonObject copy = rl[0]!.DeepClone().AsObject();
            copy["id"] = "route_copie_" + i;
            rl.Add(copy);
        }
        ZoneLoadResult rr = Read(roads);
        Assert.Contains(rr.Report.Errors, x => x.Path.StartsWith("$.data.roads[", StringComparison.Ordinal) && x.Code == ValidationCode.LimitExceeded && x.Message.Contains("case c", StringComparison.Ordinal));
        // Une route de 128 points est admise avec une région (32 sans).
        ZoneLoadResult longRoad = Mutated(r => r["data"]!["roads"]![0]!["points"] = new JsonArray(Enumerable.Range(0, ZoneValidator.MaxRegionRoadPoints)
            .Select(i => (JsonNode?)new JsonArray(-90 + i, 0)).ToArray()));
        Assert.True(longRoad.IsValid, longRoad.Report.ToString());
        Assert.True(before <= ZoneValidator.MaxPlaces);
    }

    // ---- Outil eds carte ----

    private static (int Code, string Out, string Err) Carte(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var all = args.Concat(new[] { "--data", DataDirectory, "--scenarios", Scenarios }).ToArray();
        int code = RegionMap.Run(all, output, error);
        return (code, output.ToString(), error.ToString());
    }

    [Fact]
    public void Outil_carte_cases_et_densite()
    {
        (int code, string text, string err) = Carte("cases", "--ids");
        Assert.True(code == 0, err);
        Assert.StartsWith("Région de zone.vallons : 32 × 24 cases de 128 m (768), 16 × 12 secteurs de 256 m (192), de [-1908, -1614] à [2188, 1458].", text);
        foreach (string c in CoreCells) Assert.Contains(c, text);
        Assert.Contains("s07_06", text);
        Assert.Contains("place_du_village", text);
        Assert.EndsWith("6 cases occupées, 762 vides.\n", text);
        Assert.Equal(text, Carte("cases", "--ids").Out);   // déterministe

        (code, text, err) = Carte("densite", "--vides");
        Assert.True(code == 0, err);
        string[] lines = text.Split('\n');
        Assert.Equal(24, lines.Count(l => l.StartsWith("r", StringComparison.Ordinal)));
        Assert.All(lines.Where(l => l.StartsWith("r", StringComparison.Ordinal)), l => Assert.Equal(4 + 32, l.Length));
        Assert.Contains("762 cases sans point d'intérêt sur 768.", text);
        Assert.Contains("c0_r0", text);
        Assert.DoesNotContain("c14_r12 ", text);
        string row12 = lines.Single(l => l.StartsWith("r12 ", StringComparison.Ordinal));
        Assert.Equal(new string('.', 14), row12.Substring(4, 14));
        Assert.NotEqual('.', row12[4 + 14]);
        Assert.NotEqual('.', row12[4 + 15]);
    }

    [Fact]
    public void Outil_carte_image_png()
    {
        string dir = Path.Combine(Path.GetTempPath(), "eds_v2253_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string full = Path.Combine(dir, "region.png"), core = Path.Combine(dir, "coeur.png");
            Assert.Equal(0, Carte("image", "--out", full).Code);
            Assert.Equal(0, Carte("image", "--out", core, "--pixels-par-case", "64", "--cadre", "c13_r11:c16_r14").Code);
            (int W, int H) Size(byte[] png)
            {
                Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png.Take(8));
                Assert.Equal("IHDR", System.Text.Encoding.ASCII.GetString(png, 12, 4));
                int Be(int o) => (png[o] << 24) | (png[o + 1] << 16) | (png[o + 2] << 8) | png[o + 3];
                return (Be(16), Be(20));
            }
            Assert.Equal((32 * 16 + 1, 24 * 16 + 1), Size(File.ReadAllBytes(full)));
            Assert.Equal((4 * 64 + 1, 4 * 64 + 1), Size(File.ReadAllBytes(core)));
            Assert.Equal(File.ReadAllBytes(core), RegionMap.Png(Vallons, 64, new RegionCell(13, 11), new RegionCell(16, 14)));   // mêmes octets
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Outil_carte_refus()
    {
        Assert.Equal(1, Carte().Code);
        Assert.Equal(1, Carte("autre").Code);
        Assert.Equal(1, Carte("image").Code);   // --out manquant
        Assert.Equal(1, Carte("image", "--out", "x.png", "--cadre", "c16_r14:c13_r11").Code);
        Assert.Equal(1, Carte("image", "--out", "x.png", "--cadre", "c40_r0:c41_r1").Code);
        Assert.Equal(1, Carte("cases", "--pixels-par-case", "3").Code);
        Assert.Equal(1, Carte("cases", "--inconnue").Code);
        (int code, _, string err) = Carte("cases", "--zone", ZoneJsonReader.PathIn(DataDirectory, "zone.auberge"));
        Assert.Equal(1, code);
        Assert.Contains("sans région", err, StringComparison.Ordinal);
        Assert.False(File.Exists("x.png"));
    }
}
