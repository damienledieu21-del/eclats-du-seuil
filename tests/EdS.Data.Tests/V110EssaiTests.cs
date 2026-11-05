using EdS.Runtime;

namespace EdS.Data.Tests;

/// <summary>
/// V1.10 (essai 10 de Damien sous Unity, 5 novembre 2026) : règles sans Unity des corrections d'affichage : décor qui passe en fondu quand
/// il cache le joueur, poutres qui s'effacent quand la caméra arrive à leur hauteur, halo des feux des packs qui n'est plus dessiné.
/// </summary>
[Trait("Category", "Runtime")]
public class V110EssaiTests
{
    [Fact]
    public void Un_decor_qui_cache_le_joueur_passe_en_fondu()
    {
        Assert.Equal(1, OccluderRules.Target(false, false));
        Assert.Equal(OccluderRules.FadeAlpha, OccluderRules.Target(true, false));
        Assert.Equal(0, OccluderRules.Target(true, true));
        Assert.InRange(OccluderRules.FadeAlpha, 0.1, 0.5);   // on devine le mur, on voit le personnage
        // Un quart de seconde de l'opaque au transparent, par pas réguliers, sans dépasser la cible.
        double a = 1;
        for (int i = 0; i < 3; i++) a = OccluderRules.Step(a, OccluderRules.FadeAlpha, 0.05);
        Assert.Equal(0.4, a, 9);
        a = OccluderRules.Step(a, OccluderRules.FadeAlpha, 1);
        Assert.Equal(OccluderRules.FadeAlpha, a, 9);
        Assert.Equal(1, OccluderRules.Step(0.9, 1, 1), 9);
        Assert.Equal(0.5, OccluderRules.Step(0.5, 0, -1), 9);   // temps négatif : rien ne bouge
        Assert.Equal(1, OccluderRules.Step(double.NaN, 1, 0.1), 9);
        // Plein : matériaux d'origine ; en fondu : copies transparentes ; presque rien : plus dessiné.
        Assert.False(OccluderRules.Clear(1));
        Assert.True(OccluderRules.Clear(0.9));
        Assert.True(OccluderRules.Shown(OccluderRules.FadeAlpha));
        Assert.False(OccluderRules.Shown(0));
    }

    [Fact]
    public void Le_corps_entier_compte_et_pas_seulement_la_tete()
    {
        // Avant V1.10, seule la tête (milieu du corps + 1 m) comptait : un mur qui cachait les jambes et le buste restait plein.
        Assert.Equal(new[] { -0.7, 0.0, 1.0 }, OccluderRules.BodyPoints);
    }

    [Fact]
    public void La_coupe_recoit_le_milieu_du_corps()
    {
        // Revue Codex de la PR #97 (3e tour) : ArenaView passait les pieds à SpaceView.Cut, qui sondait alors −0,7 / 0 / 1 m au lieu de
        // 0,3 / 1 / 2 m au-dessus des pieds. Le corps affiché est au milieu du corps : c'est lui qu'on passe.
        string arena = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "ArenaView.cs"));
        Assert.Contains("_space.Cut(camera, _player.transform.position, deltaTime);", arena, StringComparison.Ordinal);
        Assert.DoesNotContain("_space.Cut(camera, _player.transform.position - Vector3.up", arena, StringComparison.Ordinal);
        // Un rebord de 40 cm juste devant le joueur, côté caméra (haute, 8 m) : la ligne vers le bas des jambes (0,3 m) passe au-dessus ;
        // avec les pieds pris pour le milieu du corps, la ligne vers les pieds le touchait (faux positif : rebord transparent à tort).
        (double, double, double) camera = (0.0, 4.6, -8.0), body = (0.0, 1.0, 0.0);
        Assert.False(OccluderRules.MasksBody(camera, body, (-3.0, 0.0, -0.5), (3.0, 0.4, -0.3)));
        Assert.True(OccluderRules.MasksBody(camera, (0.0, 0.0, 0.0), (-3.0, 0.0, -0.5), (3.0, 0.4, -0.3)));   // avec les pieds : faux positif
    }

    [Theory]
    [InlineData(4.6, 2.55, true)]    // caméra d'une maison au réglage par défaut (8 m, 22°) : au-dessus des poutres
    [InlineData(2.3, 2.55, true)]    // à moins de 35 cm sous la poutre : elle s'efface déjà (la caméra n'y entre jamais)
    [InlineData(2.1, 2.55, false)]   // vue basse, de près : les poutres restent au plafond
    [InlineData(3.0, 5.75, false)]   // chapelle : la charpente est haute
    public void Les_poutres_s_effacent_quand_la_camera_arrive_a_leur_hauteur(double cameraY, double beamBottom, bool hidden) =>
        Assert.Equal(hidden, OccluderRules.Overhead(cameraY, beamBottom));

    [Fact]
    public void Les_poutres_ne_clignotent_pas_quand_la_camera_hesite()
    {
        // Relecture de la PR #97 : hystérésis de 10 cm ; à 2,15 m sous une poutre à 2,55 m, la réponse dépend de l'état d'avant.
        Assert.False(OccluderRules.Overhead(2.15, 2.55, false));
        Assert.True(OccluderRules.Overhead(2.15, 2.55, true));
        Assert.False(OccluderRules.Overhead(2.05, 2.55, true));
        Assert.True(OccluderRules.Overhead(2.25, 2.55, false));
    }

    [Fact]
    public void Un_mur_qui_ne_cache_que_les_jambes_compte()
    {
        // Caméra à 8 m au sud, haute (4,6 m) ; joueur au milieu du corps à 1 m ; muret de 1,2 m à 0,7 m devant lui, côté caméra : il cache
        // les jambes (point à 0,3 m), pas la tête. Avant V1.10 (la tête seule), il restait plein.
        var camera = (0.0, 4.6, -8.0);
        var player = (0.0, 1.0, 0.0);
        (double, double, double) min = (-3.0, 0.0, -1.0), max = (3.0, 1.2, -0.7);
        Assert.False(OccluderRules.SegmentHitsBox(camera, (0.0, 2.0, 0.0), min, max));   // la tête : non
        Assert.True(OccluderRules.MasksBody(camera, player, min, max));                  // les jambes : oui
        // Un mur derrière le joueur ne cache rien ; un mur haut entre les deux cache tout.
        Assert.False(OccluderRules.MasksBody(camera, player, (-3.0, 0.0, 1.0), (3.0, 3.0, 1.3)));
        Assert.True(OccluderRules.MasksBody(camera, player, (-3.0, 0.0, -3.0), (3.0, 3.0, -2.7)));
        // La caméra dans la boîte : touchée.
        Assert.True(OccluderRules.SegmentHitsBox((0.0, 1.0, 0.0), (5.0, 1.0, 0.0), (-1.0, 0.0, -1.0), (1.0, 2.0, 1.0)));
    }

    [Fact]
    public void Le_test_du_segment_garde_ses_resultats_sans_allouer()
    {
        // Revue Codex de la PR #97 : la version sans allocation rend les mêmes réponses que l'ancienne (tableaux par axe), sur des segments
        // tirés au hasard (graine fixe), segments parallèles à un axe compris ; et elle n'alloue rien.
        var random = new Random(20261105);
        double R() => Math.Round(random.NextDouble() * 8 - 4, random.Next(3) == 0 ? 0 : 2);
        int hits = 0;
        for (int i = 0; i < 20000; i++)
        {
            var a = (R(), R(), R());
            var b = random.Next(4) == 0 ? (a.Item1, R(), a.Item3) : (R(), R(), R());
            double x0 = R(), y0 = R(), z0 = R();
            var min = (x0, y0, z0);
            var max = (x0 + Math.Abs(R()), y0 + Math.Abs(R()), z0 + Math.Abs(R()));
            bool expected = Reference(a, b, min, max);
            Assert.Equal(expected, OccluderRules.SegmentHitsBox(a, b, min, max));
            if (expected) hits++;
        }
        Assert.InRange(hits, 100, 19900);   // des deux sortes (touchés et manqués)
        (double, double, double) c = (0.0, 4.6, -8.0), p = (0.0, 1.0, 0.0), lo = (-3.0, 0.0, -1.0), hi = (3.0, 1.2, -0.7);
        OccluderRules.MasksBody(c, p, lo, hi);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) OccluderRules.MasksBody(c, p, lo, hi);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    /// <summary>L'ancienne version (V1.10, 4/4), gardée comme référence.</summary>
    private static bool Reference((double X, double Y, double Z) a, (double X, double Y, double Z) b, (double X, double Y, double Z) min,
        (double X, double Y, double Z) max)
    {
        double t0 = 0, t1 = 1;
        double[] start = { a.X, a.Y, a.Z }, delta = { b.X - a.X, b.Y - a.Y, b.Z - a.Z }, lo = { min.X, min.Y, min.Z }, hi = { max.X, max.Y, max.Z };
        for (int axis = 0; axis < 3; axis++)
        {
            if (Math.Abs(delta[axis]) < 1e-9)
            {
                if (start[axis] < lo[axis] || start[axis] > hi[axis]) return false;
                continue;
            }
            double u = (lo[axis] - start[axis]) / delta[axis], v = (hi[axis] - start[axis]) / delta[axis];
            if (u > v) (u, v) = (v, u);
            t0 = Math.Max(t0, u);
            t1 = Math.Min(t1, v);
            if (t0 > t1) return false;
        }
        return true;
    }

    [Theory]
    [InlineData("Glow", "FlameMaterial", true)]
    [InlineData("Flames", "HeatDistortion", true)]
    [InlineData("Fire Halo", null, true)]
    [InlineData("Glow01", null, true)]
    [InlineData("Flames", "FlameRoundYellow", false)]
    [InlineData("Sparks", "SparkMaterial", false)]
    [InlineData("Wheat", "WheatField", false)]
    [InlineData("Sheath", null, false)]
    [InlineData("EmberGlow", "Embers", false)]
    [InlineData(null, null, false)]
    public void Le_halo_d_un_feu_de_pack_n_est_plus_dessine(string? objectName, string? materialName, bool halo) =>
        Assert.Equal(halo, ParticleRules.AmbientHalo(objectName, materialName));
}
