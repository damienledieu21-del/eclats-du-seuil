using EdS.Data.Zones;
using EdS.Runtime;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.27 « chargement par cases dans Unity » (docs/PLAN_GRANDE_CARTE.md, § 6) : réglages par classe de machine, case d'ancrage
/// avec hystérésis, anneaux, délai de déchargement, budget de mémoire, construction étalée, raccord des cases et du terrain lointain
/// (jamais de trou ni de double sol), lien avec les secteurs de simulation (V2.26) et le relief affiché (V2.24). Sans Unity.
/// </summary>
[Trait("Category", "World")]
public class V227StreamingTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_v227_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private AdventureMap Vallons()
    {
        AdventureSession a = AdventureSession.Load(DataDirectory, Path.Combine(AppContext.BaseDirectory, "scenarios"), Path.Combine(_folder, "partie.json"));
        a.Advance(0.05);
        return a.Map;
    }

    private static readonly RegionGrid Grid = RegionGrid.Vallons();

    /// <summary>
    /// Enregistre ce qui est montré (les tableaux d'un morceau ne valent que pendant l'appel : on en copie la forme), et vérifie à chaque
    /// changement qu'aucune case n'est ni vide ni couverte deux fois.
    /// </summary>
    private sealed class Recorder : ITerrainSink
    {
        public readonly Dictionary<RegionCell, int> Cells = new();
        public readonly Dictionary<(int, int), (double X0, double Z0, double Step, int QuadsX, bool[] Kept)> Far = new();
        public readonly List<string> Log = new();

        /// <summary>Pas déjà faits dans l'image quand un changement est montré (doit être 0 : montrer est le premier pas d'une image).</summary>
        public Func<int>? StepsSoFar;

        public int FarShown, FarExpected, LastFrom = -1;

        public void ShowCell(RegionCell cell, int ring, TerrainPatch? patch)
        {
            if (StepsSoFar != null) Assert.Equal(0, StepsSoFar());
            Log.Add("case " + cell.Name + " " + ring);
            bool had = Cells.ContainsKey(cell);
            if (patch == null) Cells.Remove(cell);
            else
            {
                Assert.True(patch.Done);
                Cells[cell] = ring;
            }
            if (had != (patch != null)) FarExpected++;   // le trou du bloc lointain change seulement si la case gagne ou perd son maillage
        }

        public void ShowFarBlock(int column, int row, TerrainPatch patch)
        {
            if (StepsSoFar != null) Assert.Equal(0, StepsSoFar());
            Assert.True(patch.Done);
            Log.Add("loin " + column + "," + row + " " + patch.TriangleCount);
            var kept = new bool[patch.QuadsX * patch.QuadsZ];
            for (int j = 0; j < patch.QuadsZ; j++)
                for (int i = 0; i < patch.QuadsX; i++) kept[j * patch.QuadsX + i] = patch.Kept(i, j);
            Far[(column, row)] = (patch.X0, patch.Z0, patch.Step, patch.QuadsX, kept);
            FarShown++;
        }

        /// <summary>Chaque case de la région (hors du rectangle couvert) a exactement un sol : le sien ou celui de son bloc lointain.</summary>
        public void CheckCoverage(RegionGrid grid, (double X0, double Z0, double X1, double Z1)? covered)
        {
            const int per = RegionGrid.CellMeters / StreamingProfile.FarStep;
            foreach (RegionCell cell in grid.Cells)
            {
                var block = (cell.Column / StreamingProfile.FarBlockCells, cell.Row / StreamingProfile.FarBlockCells);
                if (!Far.TryGetValue(block, out var far)) continue;   // bloc pas encore construit (début du chargement)
                int i0 = cell.Column % StreamingProfile.FarBlockCells * per, j0 = cell.Row % StreamingProfile.FarBlockCells * per;
                bool own = Cells.ContainsKey(cell);
                for (int j = j0; j < j0 + per; j++)
                    for (int i = i0; i < i0 + per; i++)
                    {
                        double x = far.X0 + i * far.Step, z = far.Z0 + j * far.Step;
                        bool inCovered = covered is { } c && x >= c.X0 && x + far.Step <= c.X1 && z >= c.Z0 && z + far.Step <= c.Z1;
                        if (inCovered) continue;
                        Assert.True(far.Kept[j * far.QuadsX + i] != own, $"{cell.Name} : sol {(own ? "double" : "absent")} ({i},{j})");
                    }
            }
        }
    }

    [Fact]
    public void Reglages_par_classe_de_machine_suivent_le_plan()
    {
        StreamingProfile phone = StreamingProfile.For(DeviceTier.Phone), modest = StreamingProfile.For(DeviceTier.Modest),
            full = StreamingProfile.For(DeviceTier.Full);
        Assert.Equal((1, 65), (phone.DetailRadius, phone.DetailResolution));
        Assert.Equal((1, 129), (modest.DetailRadius, modest.DetailResolution));
        Assert.Equal((2, 129), (full.DetailRadius, full.DetailResolution));
        Assert.Equal(new[] { 600.0, 900.0 }, new[] { phone.ViewMeters, modest.ViewMeters });
        Assert.True(full.ViewMeters >= Math.Sqrt(4096.0 * 4096 + 3072.0 * 3072));   // toute la région
        Assert.Equal(new[] { 150, 300, 1500 }, new[] { phone.DrawCallBudget, modest.DrawCallBudget, full.DrawCallBudget });
        foreach (StreamingProfile p in new[] { phone, modest, full })
        {
            Assert.Equal(2.0, p.BudgetMs);
            Assert.True(p.ViewRadius > p.DetailRadius);
            // Les pas de case tombent juste (1, 2, 4 ou 8 m) : les points des bords sont les mêmes d'une case à l'autre.
            Assert.Equal(0, RegionGrid.CellMeters % (p.DetailResolution - 1));
            Assert.Equal(0, RegionGrid.CellMeters % (p.SimpleResolution - 1));
            Assert.Equal(0, RegionGrid.CellMeters % StreamingProfile.FarStep);
            // Propriété : tout ce que les anneaux veulent tient dans le budget de mémoire, avec une couronne de cases gardées en plus.
            int detail = (2 * p.DetailRadius + 1) * (2 * p.DetailRadius + 1), view = (2 * p.ViewRadius + 1) * (2 * p.ViewRadius + 1);
            long wanted = detail * TerrainPatch.EstimateBytes(p.DetailResolution) + (view - detail) * TerrainPatch.EstimateBytes(p.SimpleResolution);
            Assert.True(wanted + (2 * p.DetailRadius + 1) * TerrainPatch.EstimateBytes(p.DetailResolution) <= p.MemoryBudgetBytes,
                $"{p.Tier} : {wanted} octets voulus");
            long triangles = detail * TerrainPatch.EstimateTriangles(p.DetailResolution) + (view - detail) * TerrainPatch.EstimateTriangles(p.SimpleResolution)
                + 2L * (4096 / StreamingProfile.FarStep) * (3072 / StreamingProfile.FarStep);
            Assert.True(triangles <= p.TerrainTriangleBudget, $"{p.Tier} : {triangles} triangles voulus");
            // Au pire (nouvelle rangée de détail construite avant que l'ancienne cède), encore sous la cible du plan.
            Assert.True(triangles + (2 * p.DetailRadius + 1) * TerrainPatch.EstimateTriangles(p.DetailResolution) <= p.TriangleBudget);
            // Appels de dessin du terrain : cases en vue + blocs lointains, loin sous la cible.
            Assert.True(view + 12 <= p.DrawCallBudget / 2);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => StreamingProfile.For((DeviceTier)9));
    }

    [Fact]
    public void L_ancrage_ne_bouge_qu_au_dela_de_l_hysteresis()
    {
        var s = new CellStreamer(Grid, StreamingProfile.For(DeviceTier.Phone));
        // Bord est de la case c15_r12 : x = 140.
        s.Update(130, 0, 0);
        Assert.Equal("c15_r12", s.Anchor.Name);
        for (int k = 0; k < 50; k++) s.Update(k % 2 == 0 ? 152 : 128, 0, k);   // aller-retour à 12 m du bord : rien ne bouge
        Assert.Equal("c15_r12", s.Anchor.Name);
        Assert.Equal(0, s.AnchorChanges);
        s.Update(156.5, 0, 60);
        Assert.Equal("c16_r12", s.Anchor.Name);
        s.Update(130, 0, 61);   // retour à 10 m en deçà du bord : l'ancrage reste
        Assert.Equal("c16_r12", s.Anchor.Name);
        // Hors de la région : ramené à la case du bord.
        s.Update(1e6, -1e6, 62);
        Assert.Equal("c31_r0", s.Anchor.Name);
        Assert.Throws<ArgumentException>(() => s.Update(double.NaN, 0, 63));
        Assert.Throws<ArgumentException>(() => s.Update(0, 0, double.PositiveInfinity));
    }

    [Fact]
    public void Ordre_stable_detail_d_abord_puis_un_seul_changement_a_la_fois()
    {
        StreamingProfile p = StreamingProfile.For(DeviceTier.Modest);
        var s = new CellStreamer(Grid, p);
        s.Update(0, 0, 0);
        var order = new List<CellChange>();
        while (s.TryNext(out CellChange c))
        {
            Assert.Throws<InvalidOperationException>(() => s.TryNext(out _));   // un seul à la fois
            Assert.Throws<InvalidOperationException>(() => s.Complete(new CellChange(c.Cell, c.From, 1 - c.To)));
            Assert.True(s.Busy);   // refus atomique : le changement en cours reste
            s.Complete(c);
            order.Add(c);
        }
        Assert.Equal(49, order.Count);   // 7 × 7 cases
        Assert.Equal(s.Anchor, order[0].Cell);
        Assert.All(order.Take(9), c => Assert.Equal(0, c.To));
        Assert.All(order.Skip(9), c => Assert.Equal(1, c.To));
        for (int k = 1; k < order.Count; k++)
            Assert.True(s.DistanceToAnchor(order[k - 1].Cell) <= s.DistanceToAnchor(order[k].Cell) || order[k - 1].To < order[k].To);
        Assert.True(s.Ready && s.DetailReady);
        Assert.Throws<InvalidOperationException>(() => s.Complete(order[0]));
        // Même suite de changements à chaque fois (ordre des cases, pas d'un dictionnaire).
        var t = new CellStreamer(Grid, p);
        t.Update(0, 0, 0);
        var again = new List<CellChange>();
        while (t.TryNext(out CellChange c)) { t.Complete(c); again.Add(c); }
        Assert.Equal(order, again);
    }

    [Fact]
    public void Demi_tour_sans_reconstruction_puis_dechargement_apres_le_delai()
    {
        StreamingProfile p = StreamingProfile.For(DeviceTier.Phone);
        var s = new CellStreamer(Grid, p);
        void Drain(List<CellChange> into) { while (s.TryNext(out CellChange c)) { s.Complete(c); into.Add(c); } }
        var log = new List<CellChange>();
        s.Update(0, 0, 0);
        Drain(log);
        RegionCell start = s.Anchor;
        log.Clear();
        s.Update(300, 0, 1);   // deux cases à l'est
        Drain(log);
        Assert.All(log, c => Assert.True(c.Finer));   // rien ne perd son détail avant le délai
        Assert.Equal(0, s.Built(start));
        log.Clear();
        s.Update(0, 0, 5);   // demi-tour avant 10 s
        Drain(log);
        Assert.DoesNotContain(log, c => c.Cell == start);   // la case de départ n'est pas refaite
        s.Update(300, 0, 6);
        Drain(log);
        s.Update(300, 0, 16.5);   // délai passé : la case de départ redevient simplifiée
        log.Clear();
        Drain(log);
        Assert.Contains(log, c => c.Cell == start && c.To == CellStreamer.Far);
        s.Update(2000, 1400, 40);
        Drain(log);
        s.Update(2000, 1400, 60);
        Drain(log);
        Assert.Equal(CellStreamer.Far, s.Built(start));
        Assert.True(s.Tracked <= (2 * p.ViewRadius + 1) * (2 * p.ViewRadius + 1));
    }

    [Fact]
    public void Au_dela_du_budget_de_memoire_les_cases_gardees_cedent_d_abord()
    {
        StreamingProfile p = StreamingProfile.For(DeviceTier.Phone);
        var s = new CellStreamer(Grid, p);
        double t = 0;
        // Zigzag rapide : beaucoup de cases gardées en même temps, jamais plus que le budget (une case de détail de marge).
        for (int k = 0; k < 400; k++, t += 0.5)
        {
            s.Update(-1800 + 9 * k, -1500 + 7 * k + (k % 20 < 10 ? 0 : 300), t);
            while (s.TryNext(out CellChange c)) s.Complete(c);
            Assert.True(s.Bytes <= p.MemoryBudgetBytes + TerrainPatch.EstimateBytes(p.DetailResolution), $"{s.Bytes} octets");
            Assert.True(s.TrianglesWithFar <= p.TerrainTriangleBudget + TerrainPatch.EstimateTriangles(p.DetailResolution), $"{s.TrianglesWithFar} triangles");
            Assert.True(s.Ready);
        }
    }

    [Fact]
    public void Deux_cases_voisines_se_raccordent_et_les_jupes_bouchent_les_fentes()
    {
        GroundRelief relief = GroundRelief.For(Vallons());
        Assert.False(relief.IsFlat);
        Func<double, double, double> h = (x, z) => relief.Height(x, z);
        TerrainPatch Build(double x0, double z0, int quads)
        {
            var patch = new TerrainPatch(x0, z0, 128.0 / quads, quads, quads, h);
            int n = 0;
            while (!patch.StepOnce()) n++;
            Assert.Equal(patch.TotalSteps, n + 1);
            return patch;
        }
        // c15_r12 (de x = 12 à 140) et c16_r12 (de 140 à 268), sur la rive des collines hors de l'aire.
        TerrainPatch west = Build(12, -78, 64), east = Build(140, -78, 64), coarse = Build(140, -78, 16);
        for (int j = 0; j <= 64; j++)
        {
            int vw = j * 65 + 64, ve = j * 65;
            for (int k = 0; k < 3; k++)
            {
                Assert.Equal(west.Positions[3 * vw + k], east.Positions[3 * ve + k]);
                Assert.Equal(west.Normals[3 * vw + k], east.Normals[3 * ve + k]);
            }
        }
        // Finesses différentes : mêmes points là où ils se rencontrent (un sur quatre).
        for (int j = 0; j <= 16; j++) Assert.Equal(east.HeightAt(0, 4 * j), coarse.HeightAt(0, j));
        // Jupe : 4 côtés de 64 segments, deux sommets abaissés chacun.
        Assert.Equal(65 * 65 + 2 * 4 * 64, east.VertexCount);
        Assert.Equal(64 * 64 * 6 + 4 * 64 * 12, east.IndexCount);
        Assert.All(east.Indices.Take(east.IndexCount), i => Assert.InRange(i, 0, east.VertexCount - 1));
        Assert.False(east.WideIndices);
        Assert.Equal(TerrainPatch.EstimateBytes(65), east.Bytes);
        Assert.Equal(TerrainPatch.EstimateTriangles(65), east.TriangleCount);
        // Normales unitaires, vers le haut.
        for (int v = 0; v < east.VertexCount; v++)
        {
            double nx = east.Normals[3 * v], ny = east.Normals[3 * v + 1], nz = east.Normals[3 * v + 2];
            Assert.InRange(nx * nx + ny * ny + nz * nz, 0.999, 1.001);
            Assert.True(ny > 0);
        }
    }

    [Fact]
    public void Le_terrain_de_la_zone_reste_au_dessus_et_rien_n_est_fait_dessous()
    {
        var covered = (X0: -145.0, Z0: -115.0, X1: 185.0, Z1: 145.0);
        TerrainPatch patch = new TerrainPatch(12, -78, 2, 64, 64, (x, z) => 1.0, covered: covered);
        while (!patch.StepOnce()) { }
        for (int j = 0; j < 64; j++)
            for (int i = 0; i < 64; i++) Assert.False(patch.Kept(i, j));   // case entièrement sous le terrain de la zone
        Assert.Equal(0, patch.IndexCount);
        TerrainPatch edge = new TerrainPatch(140, -78, 2, 64, 64, (x, z) => 1.0, covered: covered);
        while (!edge.StepOnce()) { }
        Assert.False(edge.Kept(0, 0));
        Assert.True(edge.Kept(30, 0));   // x de 200 à 202 : hors du rectangle
        Assert.True(edge.Kept(22, 0));   // x de 184 à 186 : à cheval, gardé…
        Assert.Equal(1.0 - TerrainPatch.Sink, edge.Positions[3 * 22 + 1], 5);   // … mais enfoncé sous le terrain de la zone
        Assert.Equal(1.0f, edge.Positions[3 * 40 + 1]);   // x = 220 : à sa hauteur
        // Arguments refusés.
        Assert.Throws<ArgumentOutOfRangeException>(() => new TerrainPatch(0, 0, 0, 4, 4, (x, z) => 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TerrainPatch(0, 0, 1, 0, 4, (x, z) => 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TerrainPatch(0, 0, 1, 4, 513, (x, z) => 0));
        Assert.Throws<ArgumentException>(() => new TerrainPatch(double.NaN, 0, 1, 4, 4, (x, z) => 0));
        var bad = new TerrainPatch(0, 0, 1, 4, 4, (x, z) => double.NaN);
        Assert.Throws<InvalidOperationException>(() => bad.StepOnce());
        Assert.Throws<InvalidOperationException>(() => new TerrainPatch(0, 0, 1, 4, 4, (x, z) => 0).HeightAt(0, 0));
        Assert.Throws<InvalidOperationException>(() => new TerrainPatch(0, 0, 1, 4, 4, (x, z) => 0).Kept(0, 0));
        // Recyclage : les tableaux rendus servent au morceau suivant ; le morceau rendu ne se lit plus ni ne se rend deux fois.
        TerrainBuffers buffers = edge.Release();
        Assert.True(edge.Released);
        Assert.Throws<InvalidOperationException>(() => edge.Release());
        Assert.Throws<InvalidOperationException>(() => edge.HeightAt(0, 0));
        Assert.Throws<InvalidOperationException>(() => edge.Kept(30, 0));   // le masque est parti avec les tableaux
        float[] reused = buffers.Positions;
        var again = new TerrainPatch(140, -78, 2, 64, 64, (x, z) => 2.0, covered: covered, buffers: buffers);
        while (!again.StepOnce()) { }
        Assert.Same(reused, again.Positions);
        Assert.Equal(2.0f, again.Positions[3 * 40 + 1]);
    }

    /// <summary>
    /// Chevauchée à 20 m/s (vitesse maximale du moteur) à travers plusieurs bords de secteur, budget d'une image tenu en pas : à chaque image,
    /// chaque case a un seul sol ; dès que l'anneau 0 est prêt, il couvre les 96 m d'activation des secteurs autour de la caméra (V2.26).
    /// </summary>
    [Theory]
    [InlineData(DeviceTier.Phone)]
    [InlineData(DeviceTier.Modest)]
    public void Chevauchee_sans_trou_ni_double_sol_au_passage_des_secteurs(DeviceTier tier)
    {
        AdventureMap map = Vallons();
        Assert.NotNull(map.Region);
        GroundRelief relief = GroundRelief.For(map);
        var covered = (X0: map.MinX - 45, Z0: map.MinZ - 45, X1: map.MaxX + 45, Z1: map.MaxZ + 45);
        StreamingProfile p = StreamingProfile.For(tier);
        var terrain = new RegionTerrain(map.Region!, p, (x, z) => relief.Height(x, z), covered);
        var sink = new Recorder();
        terrain.Update(20, 10, 0);
        terrain.Warm(sink);
        Assert.True(terrain.Idle && terrain.FarReady && terrain.Streamer.DetailReady);
        Assert.Equal(12, sink.Far.Count);
        sink.CheckCoverage(map.Region!, covered);
        int notReady = 0, frames = 0, sectorChanges = 0;
        string sector = terrain.Streamer.CellAt(20, 10).SectorName;
        // 30 images par seconde, 20 m/s vers le nord-est puis l'est : 600 m, au moins trois bords de secteur.
        int stepsPerFrame = 60;
        for (double t = 0; t < 30; t += 1.0 / 30, frames++)
        {
            double x = 20 + 20 * t * 0.8, z = 10 + 20 * t * 0.6;
            terrain.Update(x, z, t);
            int budget = stepsPerFrame, done = 0;
            sink.StepsSoFar = () => done;
            terrain.Work(() => { done++; return --budget <= 0; }, sink);
            sink.StepsSoFar = null;
            sink.CheckCoverage(map.Region!, covered);
            string now = terrain.Streamer.CellAt(x, z).SectorName;
            if (now != sector) { sectorChanges++; sector = now; }
            if (!terrain.Streamer.DetailReady) { notReady++; continue; }
            Assert.Contains(now, terrain.Streamer.DetailSectors);
            for (double dx = -96; dx <= 96; dx += 16)
                for (double dz = -96; dz <= 96; dz += 16)
                {
                    RegionCell c = terrain.Streamer.CellAt(x + dx, z + dz);
                    if (Math.Abs(x + dx - Grid.OriginX - 2048) > 2048 || Math.Abs(z + dz - Grid.OriginZ - 1536) > 1536) continue;
                    Assert.Equal(0, terrain.Streamer.Built(c));
                }
        }
        Assert.True(sectorChanges >= 3, $"{sectorChanges} passages de secteur");
        // L'anneau 0 suit : au plus une petite part des images attendent encore une case de détail (l'anneau 1 est là).
        Assert.True(notReady < frames / 4, $"{notReady} images sur {frames} sans anneau 0 complet");
        Assert.True(sink.Cells.Count <= (2 * p.ViewRadius + 1) * (2 * p.ViewRadius + 1) + 2 * (2 * p.ViewRadius + 1));
        Assert.Equal(sink.Cells.Keys.OrderBy(c => c), terrain.Streamer.Loaded);
        // Passage de l'anneau 1 à l'anneau 0 (et retour) : le bloc lointain n'est pas refait (12 blocs du premier chargement en plus).
        Assert.Contains(sink.Log, l => l.StartsWith("case ") && l.EndsWith(" 0"));
        Assert.Equal(12 + sink.FarExpected, sink.FarShown);
    }

    [Fact]
    public void Meme_chemin_meme_suite_de_changements()
    {
        Func<double, double, double> h = (x, z) => Math.Sin(x * 0.01) + Math.Cos(z * 0.013);
        List<string> Run()
        {
            var terrain = new RegionTerrain(Grid, StreamingProfile.For(DeviceTier.Phone), h);
            var sink = new Recorder();
            for (int f = 0; f < 600; f++)
            {
                terrain.Update(-1500 + 5 * f, -1200 + 3 * f, f / 30.0);
                int budget = 12;
                terrain.Work(() => --budget <= 0, sink);
            }
            return sink.Log;
        }
        Assert.Equal(Run(), Run());
        Assert.Throws<ArgumentException>(() => new RegionTerrain(Grid, StreamingProfile.For(DeviceTier.Phone), h, (5, 5, 5, 9)));
    }
    private static long Wanted(StreamingProfile p, bool triangles)
    {
        int detail = (2 * p.DetailRadius + 1) * (2 * p.DetailRadius + 1), view = (2 * p.ViewRadius + 1) * (2 * p.ViewRadius + 1);
        return triangles
            ? detail * TerrainPatch.EstimateTriangles(p.DetailResolution) + (view - detail) * TerrainPatch.EstimateTriangles(p.SimpleResolution)
            : detail * TerrainPatch.EstimateBytes(p.DetailResolution) + (view - detail) * TerrainPatch.EstimateBytes(p.SimpleResolution);
    }

    /// <summary>
    /// Relecture indépendante de la PR #154 : la branche d'éviction est vraiment atteinte. Budget de mémoire réduit à ce que veulent les
    /// anneaux : les cases quittées cèdent leur détail tout de suite (avant les 10 s), la plus ancienne d'abord ; sans pression, elles le gardent.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Sous_pression_les_cases_gardees_cedent_avant_le_delai_la_plus_ancienne_d_abord(bool byTriangles)
    {
        StreamingProfile phone = StreamingProfile.For(DeviceTier.Phone);
        StreamingProfile tight = byTriangles
            ? phone.WithBudgets(long.MaxValue, (Wanted(phone, true) + RegionTerrain.FarTriangles(Grid)) * 5 / 4)
            : phone.WithBudgets(Wanted(phone, false), long.MaxValue);
        Assert.Throws<ArgumentOutOfRangeException>(() => phone.WithBudgets(-1, 0));
        foreach ((StreamingProfile p, bool pressure) in new[] { (phone.WithBudgets(long.MaxValue, long.MaxValue), false), (tight, true) })
        {
            var s = new CellStreamer(Grid, p);
            var log = new List<CellChange>();
            void Finer() { while (s.TryNext(out CellChange c)) { if (!c.Finer) { s.Cancel(); break; } s.Complete(c); } }
            s.Update(0, 0, 0);
            while (s.TryNext(out CellChange c0)) s.Complete(c0);
            RegionCell first = s.Anchor;                 // c14_r12
            s.Update(300, 0, 1);                         // ancrage c17_r12 : la colonne c12 sort de vue
            Finer();                                     // nouvelles cases seulement : les quittées attendent
            s.Update(300, 300, 2);                       // ancrage c17_r14 : la rangée r10 sort de vue
            Finer();
            while (s.TryNext(out CellChange c)) { s.Complete(c); log.Add(c); }
            if (!pressure)
            {
                Assert.Empty(log);                       // rien ne cède avant le délai
                Assert.Equal(0, s.Built(first));
                continue;
            }
            Assert.NotEmpty(log);
            Assert.All(log, c => Assert.False(c.Finer));
            Assert.Equal(CellStreamer.Far, s.Built(first));   // cédée avant les 10 s
            if (!byTriangles) Assert.True(s.Bytes <= p.MemoryBudgetBytes);
            else Assert.True(s.TrianglesWithFar <= p.TerrainTriangleBudget);
            // La plus ancienne d'abord : les cases qui ont perdu du détail à t = 1 (ancrage c14_r12 → c17_r12) avant celles de t = 2.
            int Ring(RegionCell c, int col, int row) => p.RingAt(Math.Max(Math.Abs(c.Column - col), Math.Abs(c.Row - row)));
            bool Old(RegionCell c) => Ring(c, 17, 12) > Ring(c, 14, 12);
            int lastOld = log.FindLastIndex(c => Old(c.Cell)), firstNew = log.FindIndex(c => !Old(c.Cell));
            Assert.True(lastOld >= 0 && firstNew >= 0, string.Join(" ", log));
            Assert.True(lastOld < firstNew, string.Join(" ", log));
        }
    }

    /// <summary>Relecture indépendante de la PR #154 : une exception pendant la construction n'abîme rien ; tout reprend ensuite.</summary>
    [Fact]
    public void Une_exception_abandonne_proprement_le_changement_en_cours()
    {
        bool broken = false;
        int calls = 0;
        Func<double, double, double> h = (x, z) =>
        {
            if (broken && ++calls > 200) throw new InvalidOperationException("relief cassé");
            return Math.Sin(x * 0.01);
        };
        var terrain = new RegionTerrain(Grid, StreamingProfile.For(DeviceTier.Phone), h);
        var sink = new Recorder();
        terrain.Update(0, 0, 0);
        broken = true;
        Assert.Throws<InvalidOperationException>(() => terrain.Warm(sink));
        Assert.False(terrain.Busy);
        Assert.False(terrain.Streamer.Busy);
        int shown = sink.Log.Count;
        broken = false;
        terrain.Warm(sink);
        Assert.True(terrain.Idle && terrain.FarReady && terrain.Streamer.DetailReady);
        Assert.Equal(12, sink.Far.Count);
        Assert.True(sink.Log.Count > shown);
        sink.CheckCoverage(Grid, null);
        // Montreur qui échoue : le changement est abandonné, le flux reste utilisable.
        terrain.Update(600, 0, 1);
        var failing = new FailingSink();
        Assert.Throws<InvalidOperationException>(() => terrain.Warm(failing));
        Assert.False(terrain.Busy);
        Assert.False(terrain.Streamer.Busy);
        terrain.Cancel();   // sans changement en cours : rien
        terrain.Warm(sink);
        Assert.True(terrain.Idle);
    }

    private sealed class FailingSink : ITerrainSink
    {
        public void ShowCell(RegionCell cell, int ring, TerrainPatch? patch) => throw new InvalidOperationException("montreur cassé");
        public void ShowFarBlock(int column, int row, TerrainPatch patch) => throw new InvalidOperationException("montreur cassé");
    }

    /// <summary>Relecture indépendante de la PR #154 : <c>Warm</c> respecte vraiment son plafond de pas.</summary>
    [Fact]
    public void Warm_respecte_son_plafond_de_pas()
    {
        var terrain = new RegionTerrain(Grid, StreamingProfile.For(DeviceTier.Phone), (x, z) => 0);
        terrain.Update(0, 0, 0);
        var sink = new Recorder();
        Assert.Equal(5, terrain.Warm(sink, 5));
        Assert.False(terrain.Idle);
        Assert.Equal(5, terrain.Steps);
        Assert.Throws<ArgumentOutOfRangeException>(() => terrain.Warm(sink, 0));
        int all = terrain.Warm(sink);
        Assert.True(terrain.Idle);
        Assert.Equal(0, terrain.Warm(sink));
        Assert.Equal(5 + all, terrain.Steps);
    }
}
