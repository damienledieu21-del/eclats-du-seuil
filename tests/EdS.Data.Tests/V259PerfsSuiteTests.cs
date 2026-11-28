using System;
using System.IO;
using System.Linq;
using EdS.Runtime;
using Xunit;

namespace EdS.Data.Tests;

/// <summary>
/// V2.59 (suite de l'audit des performances, docs/PERFS.md) : Bourg posé par lots (le plus proche de la caméra d'abord), version
/// lointaine des maisons sans ombre sur une machine allégée, occulteurs de l'Envers rangés par case, ordre des lanternes sans
/// allocation, et leur branchement dans les scripts Unity.
/// </summary>
[Trait("Category", "Runtime")]
public sealed class V259PerfsSuiteTests
{
    private static string Script(params string[] path) =>
        File.ReadAllText(Path.Combine(new[] { Lot93UnityPackageTests.Root, "unity", "Assets", "EdS" }.Concat(path).ToArray())).Replace("\r\n", "\n");

    [Fact]
    public void Le_Bourg_est_pose_du_plus_proche_au_plus_eloigne_de_la_camera()
    {
        var spots = new (double X, double Z)[] { (10, 0), (1, 1), (-3, 0), (0, 10), (1, -1), (double.NaN, 0) };
        int[] order = BourgBuild.Order(spots, 0, 0);
        // (1,1) et (1,-1) à égalité : ordre des indices ; une place non finie en dernier.
        Assert.Equal(new[] { 1, 4, 2, 0, 3, 5 }, order);
        Assert.Equal(new[] { 3, 1, 2, 4, 0, 5 }, BourgBuild.Order(spots, 0, 9));
        Assert.Empty(BourgBuild.Order(Array.Empty<(double, double)>(), 0, 0));
        Assert.Throws<ArgumentNullException>(() => BourgBuild.Order(null!, 0, 0));
    }

    [Fact]
    public void L_ordre_du_Bourg_est_une_permutation_triee_par_distance()
    {
        var rng = new Random(259);
        for (int run = 0; run < 50; run++)
        {
            var spots = Enumerable.Range(0, rng.Next(0, 300)).Select(_ => ((double)rng.Next(-200, 200), (double)rng.Next(-200, 200))).ToArray();
            double cx = rng.Next(-100, 100), cz = rng.Next(-100, 100);
            int[] order = BourgBuild.Order(spots, cx, cz);
            Assert.Equal(Enumerable.Range(0, spots.Length), order.OrderBy(i => i));
            double D(int i) => (spots[i].Item1 - cx) * (spots[i].Item1 - cx) + (spots[i].Item2 - cz) * (spots[i].Item2 - cz);
            for (int k = 1; k < order.Length; k++)
                Assert.True(D(order[k - 1]) < D(order[k]) || D(order[k - 1]) == D(order[k]) && order[k - 1] < order[k]);
        }
    }

    [Fact]
    public void Chaque_image_pose_au_moins_une_piece_puis_respecte_le_budget()
    {
        Assert.True(BourgBuild.Continue(0, 1000, 2));   // la construction avance à chaque image
        Assert.True(BourgBuild.Continue(1, 1.9, 2));
        Assert.False(BourgBuild.Continue(1, 2.0, 2));
        Assert.False(BourgBuild.Continue(BourgBuild.MaxPerFrame, 0, 2));
        Assert.False(BourgBuild.Continue(3, double.NaN, 2));
        // Budget du reste du décor (cases) : 2 ms sur toutes les machines.
        foreach (DeviceTier tier in new[] { DeviceTier.Phone, DeviceTier.Modest, DeviceTier.Full })
            Assert.True(StreamingProfile.For(tier).BudgetMs > 0);
    }

    [Fact]
    public void La_version_lointaine_des_maisons_ne_projette_plus_d_ombre_sur_une_machine_allegee()
    {
        Assert.True(PerfRules.FarHouseShadow(DeviceTier.Full));
        Assert.False(PerfRules.FarHouseShadow(DeviceTier.Modest));
        Assert.False(PerfRules.FarHouseShadow(DeviceTier.Phone));
        // Elle n'apparaît qu'au bord de la distance d'ombre (dernière part de la dernière cascade).
        foreach (DeviceTier tier in new[] { DeviceTier.Phone, DeviceTier.Modest })
        {
            double far = BourgKit.LodDistances(tier).Far, shadow = PerfRules.Render(tier).ShadowDistance;
            Assert.True(far >= 0.8 * shadow, tier.ToString());
        }
    }

    [Fact]
    public void L_ordre_des_lanternes_sans_allocation_est_celui_de_LampOrder()
    {
        var rng = new Random(59);
        for (int run = 0; run < 300; run++)
        {
            int n = rng.Next(0, 40), count = rng.Next(0, 10);
            var d = Enumerable.Range(0, n).Select(_ => (double)rng.Next(0, 12)).ToArray();   // beaucoup d'égalités
            var always = Enumerable.Range(0, n).Select(_ => rng.Next(4) == 0).ToArray();
            double lamps = rng.Next(3) * 0.5;
            var expected = TownModels.LampOrder(d, always, lamps, count);
            // Tableaux plus longs que n (gardés d'une relecture à l'autre) : seules les n premières lanternes comptent.
            var dl = d.Concat(new[] { -1.0, -2.0 }).ToArray();
            var al = always.Concat(new[] { true, true }).ToArray();
            var result = new int[Math.Max(count, 1) + 2];
            int found = TownModels.LampOrder(dl, al, n, lamps, count, result);
            Assert.Equal(expected, result.Take(found));
        }
        var small = new int[2];
        Assert.Equal(2, TownModels.LampOrder(new double[] { 3, 2, 1 }, new bool[3], 3, 1.0, 8, small));
        Assert.Equal(new[] { 2, 1 }, small);
        Assert.Throws<ArgumentOutOfRangeException>(() => TownModels.LampOrder(new double[1], new bool[1], 2, 1.0, 8, small));
    }

    [Fact]
    public void Les_scripts_Unity_branchent_les_corrections()
    {
        string bourg = Script("Game", "SpaceView.Bourg.cs");
        Assert.Contains("BourgBuild.Order(spots, camera.x, camera.z)", bourg);
        Assert.Contains("BourgBuild.Continue(built, _bourgWatch.Elapsed.TotalMilliseconds, budget)", bourg);
        Assert.Contains("SmallCasters(holder);", bourg);
        Assert.Contains("if (_envers) DarkenUnder(holder);", bourg);
        Assert.Contains("_reflections.RenderProbe();", bourg);
        Assert.Contains("PerfRules.FarHouseShadow(DeviceQuality.Tier)", bourg);
        string view = Script("Game", "SpaceView.cs");
        Assert.Contains("BourgStep(camera);", view);
        Assert.Contains("_reflections = StyleWorld.Reflections(", view);
        foreach (string file in new[] { "SpaceView.Envers.cs", "SpaceView.EnversLieux.cs" })
        {
            string envers = Script("Game", file);
            Assert.DoesNotContain("IsChildOf(cell.transform)).ToList()", envers);
            Assert.Contains("DropCellOccluders(", envers);
            Assert.Contains("SmallCasters(cell);", envers);
            Assert.Contains("List<int> gone = _goneCells;", envers);
        }
        string night = Script("Game", "SpaceView.Night.cs");
        Assert.Contains("TownModels.LampOrder(_lampDistances, _lampAlways, n, lamps, _lampLights.Count, _lampNearest)", night);
        Assert.DoesNotContain(".Select(i => _lanterns[i]).ToList()", night);
        string game = Script("Game", "EdsGame.cs");
        Assert.Contains("_hud.Clock(TrainingClock(elapsed));", game);
        Assert.DoesNotContain("return string.Format(a.Text(", game);
    }
}
