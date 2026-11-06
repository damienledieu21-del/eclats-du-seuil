using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EdS.Runtime;
using EdS.World;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.2.1 : compagnon d'affichage lié à un objet (Pain-d'Épice suit le joueur tant que <c>item.chat_de_rosalie</c> est dans le
/// sac). Catalogue juste, choix d'après le sac, mouvement (suit le chemin à distance, freine sans dépasser, s'assoit, s'occupe, se couche,
/// reparaît derrière le joueur, part en rapetissant), déterminisme, pause, valeurs folles refusées, sons reconnus ; rien dans les formats.
/// </summary>
[Trait("Category", "World")]
public sealed class V221CompagnonTests
{
    private const double Dt = 0.05;

    [Fact]
    public void Le_catalogue_est_juste_et_lie_pain_d_epice_a_l_objet_de_quete()
    {
        Assert.Empty(Companions.Check(ItemsCatalog));
        CompanionDefinition cat = Assert.Single(Companions.All);
        Assert.Equal("item.chat_de_rosalie", cat.ItemId);
        Assert.Equal(CompanionShape.Cat, cat.Shape);
        Assert.Same(cat, Companions.Find("companion.pain_d_epice"));
        Assert.Null(Companions.Find("companion.inconnu"));
        Assert.Null(Companions.Find(null));
        Assert.True(cat.CombatDistance > cat.FollowDistance);
    }

    [Fact]
    public void Le_chat_suit_seulement_quand_l_objet_est_au_sac_ou_force_pour_l_essai()
    {
        var bag = new Inventory(ItemsCatalog);
        Assert.Empty(Companions.Shown(bag));
        Assert.Equal(InventoryRefusal.None, bag.Add("item.chat_de_rosalie", 1));
        Assert.Equal("companion.pain_d_epice", Assert.Single(Companions.Shown(bag)).Id);
        Assert.Equal(InventoryRefusal.None, bag.Remove("item.chat_de_rosalie", 1));   // rendu à Rosalie
        Assert.Empty(Companions.Shown(bag));
        Assert.Single(Companions.Shown(bag, "companion.pain_d_epice"));                  // essai : affichage seulement
        Assert.Equal(0, bag.Count("item.chat_de_rosalie"));
        Assert.Empty(Companions.Shown(bag, "companion.inconnu"));
        Assert.Single(Companions.Shown(_ => 1, "companion.pain_d_epice"));               // jamais en double
        Assert.Throws<ArgumentNullException>(() => Companions.Shown((Func<string, int>)null!));
    }

    [Fact]
    public void Un_catalogue_d_objets_sans_le_chat_est_signale_avec_son_chemin()
    {
        string folder = Path.Combine(Path.GetTempPath(), "eds_v221_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string text = File.ReadAllText(EdS.Data.Json.ItemJsonReader.PathIn(DataDirectory)).Replace("item.chat_de_rosalie", "item.chat_perdu");
            string path = Path.Combine(folder, "items.json");
            File.WriteAllText(path, text);
            var catalog = EdS.Data.Json.ItemJsonReader.ReadFile(path, ShippedData).Catalog;
            Assert.NotNull(catalog);
            Assert.Contains(Companions.Check(catalog!), e => e.StartsWith("companions[0].itemId", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Il_apparait_derriere_le_joueur_avec_un_miaulement_puis_grandit()
    {
        var f = new CompanionFollower(Companions.PainDEpice);
        f.Step(10, 20, 0, Dt, false);   // le joueur regarde vers +Z
        Assert.Equal(CompanionCue.Appear, f.Cue);
        Assert.True(f.Z < 20 - 1.5, $"derrière : {f.Z}");
        Assert.InRange(Distance(f, 10, 20), 1.6, 1.9);
        Assert.Equal(0, f.Presence);
        for (int i = 0; i < 20; i++) f.Step(10, 20, 0, Dt, false);
        Assert.Equal(1, f.Presence);
        Assert.Equal(CompanionCue.None, f.Cue);
    }

    [Fact]
    public void Il_suit_le_chemin_du_joueur_a_distance_sans_jamais_s_approcher_plus_pres()
    {
        var f = new CompanionFollower(Companions.PainDEpice);
        // Le joueur marche à 5 m/s vers +X, puis tourne à angle droit vers +Z (le coin d'une maison).
        double px = 0, pz = 0;
        f.Step(px, pz, 90, Dt, false);
        double minimum = double.MaxValue;
        bool cutCorner = false;
        for (int i = 0; i < 200; i++)
        {
            if (i < 100) px += 5 * Dt;
            else pz += 5 * Dt;
            f.Step(px, pz, i < 100 ? 90 : 0, Dt, false);
            if (i > 10) minimum = Math.Min(minimum, Distance(f, px, pz));
            // Le coin est en (25, 0) : la bête passe par le chemin du joueur, pas en diagonale à travers la maison (x < 24, z > 1).
            if (f.X < 24 && f.Z > 1) cutCorner = true;
            Assert.Equal(CompanionPose.Walk, i > 10 ? f.Pose : CompanionPose.Walk);
        }
        Assert.False(cutCorner);
        Assert.True(minimum >= Companions.PainDEpice.FollowDistance - 0.6, $"trop près : {minimum}");
        Assert.InRange(Distance(f, px, pz), 1.6, 4.0);   // elle ne se laisse pas distancer
        Assert.True(f.Stride > 40);
        Assert.True(f.TrailCount <= CompanionFollower.MaxTrail);
        Assert.True(f.Yaw < 10 || f.Yaw > 350, $"tournée vers +Z après le coin : {f.Yaw}");
    }

    [Fact]
    public void Quand_le_joueur_s_arrete_il_freine_s_assoit_s_occupe_ronronne_puis_se_couche()
    {
        var f = new CompanionFollower(Companions.PainDEpice);
        double px = 0;
        f.Step(px, 0, 90, Dt, false);
        for (int i = 0; i < 60; i++) f.Step(px += 5 * Dt, 0, 90, Dt, false);
        var poses = new HashSet<CompanionPose>();
        var cues = new List<CompanionCue>();
        double sitAt = -1;
        for (int i = 0; i < (int)(40 / Dt); i++)
        {
            f.Step(px, 0, 90, Dt, false);
            poses.Add(f.Pose);
            if (f.Cue != CompanionCue.None) cues.Add(f.Cue);
            if (sitAt < 0 && f.Pose == CompanionPose.Sit) sitAt = i * Dt;
            Assert.True(Distance(f, px, 0) >= Companions.PainDEpice.FollowDistance - 0.05);
        }
        Assert.InRange(sitAt, 0.5, 3.0);
        Assert.Contains(CompanionPose.Sit, poses);
        Assert.Contains(CompanionPose.Curl, poses);
        Assert.True(poses.Overlaps(new[] { CompanionPose.Groom, CompanionPose.Look, CompanionPose.Stretch }));
        Assert.Equal(1, cues.Count(c => c == CompanionCue.Purr));   // une fois par pause
        Assert.Equal(CompanionPose.Curl, f.Pose);
        Assert.Equal(0, f.Speed);
        // Le joueur repart : elle se lève et suit.
        for (int i = 0; i < 20; i++) f.Step(px += 5 * Dt, 0, 90, Dt, false);
        Assert.Equal(CompanionPose.Walk, f.Pose);
    }

    [Fact]
    public void En_combat_elle_se_tient_a_l_ecart()
    {
        var f = new CompanionFollower(Companions.PainDEpice);
        double px = 0;
        f.Step(px, 0, 90, Dt, true);
        Assert.InRange(Distance(f, px, 0), 5.9, 6.2);
        for (int i = 0; i < 100; i++) f.Step(px += 3 * Dt, 0, 90, Dt, true);
        for (int i = 0; i < 100; i++) f.Step(px, 0, 90, Dt, true);
        Assert.True(Distance(f, px, 0) >= Companions.PainDEpice.CombatDistance - 0.05);
        Assert.NotEqual(CompanionPose.Curl, f.Pose);
    }

    [Fact]
    public void Trop_loin_elle_reparait_derriere_le_joueur_sans_miauler()
    {
        var f = new CompanionFollower(Companions.PainDEpice);
        for (int i = 0; i < 30; i++) f.Step(0, 0, 0, Dt, false);
        f.Step(200, -50, 180, Dt, false);   // une porte, un voyage
        Assert.InRange(Distance(f, 200, -50), 1.6, 1.9);
        Assert.True(f.Z > -50, "derrière un joueur tourné vers -Z");
        Assert.Equal(CompanionCue.None, f.Cue);
        Assert.Equal(1, f.Presence);
        f.Replace();
        f.Step(200, -50, 90, Dt, false);
        Assert.True(f.X < 200, "replacée derrière un joueur tourné vers +X");
    }

    [Fact]
    public void Quand_l_objet_quitte_le_sac_elle_rapetisse_puis_disparait_et_peut_revenir()
    {
        var f = new CompanionFollower(Companions.PainDEpice);
        for (int i = 0; i < 30; i++) f.Step(0, 0, 0, Dt, false);
        f.Leave();
        f.Step(3, 0, 0, Dt, false);
        Assert.True(f.Presence < 1 && !f.Gone);
        f.Stay();   // l'objet revient aussitôt
        for (int i = 0; i < 20; i++) f.Step(3, 0, 0, Dt, false);
        Assert.Equal(1, f.Presence);
        f.Leave();
        for (int i = 0; i < 20; i++) f.Step(3, 0, 0, Dt, false);
        Assert.True(f.Gone);
    }

    [Fact]
    public void Le_mouvement_est_determine_la_pause_ne_bouge_rien_et_les_valeurs_folles_sont_ignorees()
    {
        string Run()
        {
            var f = new CompanionFollower(Companions.PainDEpice);
            var trace = new System.Text.StringBuilder();
            double px = 0;
            for (int i = 0; i < 1200; i++)
            {
                if (i % 300 < 100) px += 4 * Dt;
                f.Step(px, Math.Sin(i * 0.01) * 3, 90, Dt, false);
                trace.Append(FormattableString.Invariant($"{f.X:R};{f.Z:R};{f.Yaw:R};{f.Pose};{f.Cue}|"));
            }
            return trace.ToString();
        }
        Assert.Equal(Run(), Run());

        var g = new CompanionFollower(Companions.PainDEpice);
        for (int i = 0; i < 30; i++) g.Step(i * 0.2, 0, 90, Dt, false);
        (double x, double z, double s) = (g.X, g.Z, g.Stride);
        g.Step(50, 0, 90, 0, false);                       // pause
        g.Step(double.NaN, 0, 90, Dt, false);
        g.Step(1, double.PositiveInfinity, 90, Dt, false);
        g.Step(1, 0, 90, double.NaN, false);
        Assert.Equal((x, z, s), (g.X, g.Z, g.Stride));
        g.Step(6.2, 0, 90, 5.0, false);                     // une image très lente compte pour 0,1 s
        Assert.True(g.Stride - s <= Companions.PainDEpice.MaxSpeed * CompanionFollower.MaxStep + 1e-9);
        Assert.Throws<ArgumentNullException>(() => new CompanionFollower(null!));
    }

    [Fact]
    public void Les_sons_deposes_sont_reconnus_et_rien_ne_change_dans_les_formats()
    {
        string[] files = { "Assets/Style/Sounds/Animaux/chat_miaou_1.ogg", "Assets/Style/Sounds/Animaux/chat_ronron.ogg", "Assets/Style/Sounds/Combat/swish_1.ogg" };
        Assert.Equal("Assets/Style/Sounds/Animaux/chat_miaou_1.ogg", StyleRules.PickSound(files, StyleSound.Meow));
        Assert.Equal("Assets/Style/Sounds/Animaux/chat_ronron.ogg", StyleRules.PickSound(files, StyleSound.Purr));
        Assert.Null(StyleRules.PickSound(new[] { "Assets/Style/Sounds/Combat/swish_1.ogg" }, StyleSound.Meow));
        foreach (StyleSound sound in Enum.GetValues(typeof(StyleSound))) StyleRules.PickSounds(files, sound, 6);   // chaque son a ses mots
        Assert.Equal(12, EdS.Data.Saves.SaveValidator.CurrentSchemaVersion);
    }

    private static double Distance(CompanionFollower f, double x, double z) => Math.Sqrt((f.X - x) * (f.X - x) + (f.Z - z) * (f.Z - z));
}
