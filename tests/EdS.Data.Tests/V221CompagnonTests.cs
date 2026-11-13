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
        g.Step(8, 0, 90, 0, false);                        // pause (au-delà de 14 m, elle se replacerait : voir la relecture)
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
        Assert.Equal(18, EdS.Data.Saves.SaveValidator.CurrentSchemaVersion);   // V2.26 : 18 (registre purgé, secteurs)   /* V2.7 : 15 (réputation) ; V2.6 : 14 (cicatrices) ; V2.5 : 13 (registre des événements) */
    }

    [Fact]
    public void Relecture_un_combat_qui_commence_tout_pres_la_fait_s_ecarter_en_douceur()
    {
        var f = new CompanionFollower(Companions.PainDEpice);
        for (int i = 0; i < 40; i++) f.Step(0, 0, 0, Dt, false);   // assise à 1,7 m
        Assert.InRange(Distance(f, 0, 0), 1.6, 1.9);
        double before = Distance(f, 0, 0), maxStep = 0;
        for (int i = 0; i < 100; i++)
        {
            double x = f.X, z = f.Z;
            f.Step(0, 0, 0, Dt, true);
            maxStep = Math.Max(maxStep, Math.Sqrt((f.X - x) * (f.X - x) + (f.Z - z) * (f.Z - z)));
            Assert.True(Distance(f, 0, 0) >= before - 1e-9, "elle ne revient jamais vers le joueur");
            before = Distance(f, 0, 0);
            if (i == 2) Assert.Equal(CompanionPose.Walk, f.Pose);
        }
        Assert.True(maxStep <= CompanionFollower.BackSpeed * Dt + 1e-9, $"en douceur : {maxStep}");
        Assert.InRange(Distance(f, 0, 0), Companions.PainDEpice.CombatDistance - CompanionFollower.BackMargin - 0.01, Companions.PainDEpice.CombatDistance + 0.01);
        for (int i = 0; i < 40; i++) f.Step(0, 0, 0, Dt, true);
        Assert.NotEqual(CompanionPose.Walk, f.Pose);   // arrivée, elle s'arrête (puis s'assoit)
        // Sur le joueur même : elle s'écarte vers l'arrière de son regard.
        var g = new CompanionFollower(Companions.PainDEpice);
        g.Step(0, 0, 0, Dt, false);
        g.Step(g.X, g.Z, 0, Dt, true);
        g.Step(g.X, g.Z, 0, Dt, true);
        Assert.False(double.IsNaN(g.X) || double.IsNaN(g.Z));
    }

    [Fact]
    public void Relecture_en_pause_elle_prend_sa_place_et_miaule_seulement_a_la_reprise()
    {
        var f = new CompanionFollower(Companions.PainDEpice);
        Assert.False(f.Placed);
        f.Step(30, 40, 0, 0, false);   // première image en pause
        Assert.True(f.Placed);
        Assert.InRange(Distance(f, 30, 40), 1.6, 1.9);
        Assert.Equal(CompanionCue.None, f.Cue);
        f.Step(30, 40, 0, Dt, false);
        Assert.Equal(CompanionCue.Appear, f.Cue);
        for (int i = 0; i < 20; i++) f.Step(30, 40, 0, Dt, false);
        f.Replace();
        f.Step(-80, 5, 90, 0, false);   // une porte passée pendant la pause
        Assert.True(f.Placed);
        Assert.InRange(Distance(f, -80, 5), 1.6, 1.9);
        Assert.Equal(CompanionCue.None, f.Cue);
        f.Step(-80, 5, 90, Dt, false);
        Assert.Equal(CompanionCue.None, f.Cue);   // pas de second miaulement d'arrivée
    }

    [Fact]
    public void Relecture_partir_puis_etre_replacee_ou_teleportee_reste_sain()
    {
        var f = new CompanionFollower(Companions.PainDEpice);
        for (int i = 0; i < 30; i++) f.Step(0, 0, 0, Dt, false);
        f.Leave();
        f.Step(0, 0, 0, Dt, false);
        double p = f.Presence;
        f.Replace();
        f.Step(500, 500, 0, Dt, false);   // replacée près du joueur, toujours en partance
        Assert.InRange(Distance(f, 500, 500), 1.6, 1.9);
        Assert.Equal(CompanionCue.None, f.Cue);
        Assert.True(f.Leaving);
        Assert.Equal(p, f.Presence);
        f.Step(-500, 0, 0, Dt, false);    // téléportée (trop loin), même chose
        Assert.InRange(Distance(f, -500, 0), 1.6, 1.9);
        for (int i = 0; i < 20; i++) f.Step(-500, 0, 0, Dt, false);
        Assert.True(f.Gone);
        Assert.Equal(0, f.Speed);
        // Partie avant même d'avoir paru : aucune arrivée, aucun miaulement.
        var g = new CompanionFollower(Companions.PainDEpice);
        g.Leave();
        g.Step(0, 0, 0, Dt, false);
        Assert.Equal(CompanionCue.None, g.Cue);
        g.Step(0, 0, 0, Dt, false);
        Assert.True(g.Gone);
    }

    [Fact]
    public void Relecture_trois_compagnons_au_plus_dans_l_ordre_du_catalogue_sans_doublon()
    {
        CompanionDefinition Beast(int n) => new CompanionDefinition("companion.b" + n, "item.b" + n, "Bête " + n, CompanionShape.Cat,
            StyleTint.White, StyleTint.White, StyleTint.White, 0.5, 1.5, 5, 8);
        var catalog = Enumerable.Range(0, 5).Select(Beast).ToList();
        var into = new List<CompanionDefinition> { Beast(9) };   // vidée avant d'être remplie
        Companions.Shown(catalog, _ => 1, "companion.b4", into);
        Assert.Equal(new[] { "companion.b0", "companion.b1", "companion.b2" }, into.Select(c => c.Id));
        Assert.Equal(Companions.MaxShown, into.Count);
        Companions.Shown(catalog, item => item == "item.b3" ? 1 : 0, "companion.b1", into);
        Assert.Equal(new[] { "companion.b1", "companion.b3" }, into.Select(c => c.Id));
        Companions.Shown(catalog, _ => 0, null, into);
        Assert.Empty(into);
        // Avec le sac : même liste réutilisée.
        var bag = new Inventory(ItemsCatalog);
        Companions.Shown(bag, null, into);
        Assert.Empty(into);
        bag.Add("item.chat_de_rosalie", 1);
        Companions.Shown(bag, "companion.pain_d_epice", into);
        Assert.Equal("companion.pain_d_epice", Assert.Single(into).Id);
        Assert.Throws<ArgumentNullException>(() => Companions.Shown(bag, null, null!));
    }

    [Fact]
    public void Relecture_stay_en_pleine_disparition_la_fait_regrandir()
    {
        var f = new CompanionFollower(Companions.PainDEpice);
        for (int i = 0; i < 30; i++) f.Step(0, 0, 0, Dt, false);
        f.Leave();
        for (int i = 0; i < 6; i++) f.Step(0, 0, 0, Dt, false);   // à moitié partie
        double half = f.Presence;
        Assert.InRange(half, 0.4, 0.6);
        f.Stay();
        f.Step(0, 0, 0, Dt, false);
        Assert.True(f.Presence > half);
        Assert.False(f.Leaving);
        for (int i = 0; i < 20; i++) f.Step(0, 0, 0, Dt, false);
        Assert.Equal(1, f.Presence);
        Assert.False(f.Gone);
    }

    [Fact]
    public void Relecture_couchee_en_boule_elle_se_leve_des_que_le_joueur_repart()
    {
        var f = new CompanionFollower(Companions.PainDEpice);
        for (int i = 0; i < (int)(35 / Dt); i++) f.Step(0, 0, 0, Dt, false);
        Assert.Equal(CompanionPose.Curl, f.Pose);
        double px = 0;
        int frames = 0;
        while (f.Pose == CompanionPose.Curl && frames < 40)
        {
            f.Step(0, px += 5 * Dt, 0, Dt, false);
            frames++;
        }
        Assert.InRange(frames, 1, 10);
        Assert.Equal(CompanionPose.Walk, f.Pose);
        Assert.Equal(0, f.PoseSeconds);
        // Un combat qui commence pendant le sommeil la fait aussi se lever (elle s'écarte).
        var g = new CompanionFollower(Companions.PainDEpice);
        for (int i = 0; i < (int)(35 / Dt); i++) g.Step(0, 0, 0, Dt, false);
        Assert.Equal(CompanionPose.Curl, g.Pose);
        for (int i = 0; i < 3; i++) g.Step(0, 0, 0, Dt, true);
        Assert.Equal(CompanionPose.Walk, g.Pose);
    }

    [Fact]
    public void Codex_entre_deux_actes_il_y_a_toujours_4_a_9_secondes_meme_apres_un_miaulement_et_plusieurs_arrets()
    {
        var f = new CompanionFollower(Companions.PainDEpice);
        double px = 0, t = 0, last = double.NaN;
        int acts = 0, meows = 0, stops = 0;
        f.Step(px, 0, 90, Dt, false);
        for (int round = 0; round < 12; round++)
        {
            for (int i = 0; i < 40; i++) f.Step(px += 5 * Dt, 0, 90, Dt, false);   // il marche
            last = double.NaN;
            stops++;
            for (int i = 0; i < (int)(28 / Dt); i++)                                // arrêt de 28 s (avant la boule)
            {
                CompanionPose before = f.Pose;
                f.Step(px, 0, 90, Dt, false);
                t += Dt;
                bool started = f.Cue == CompanionCue.Meow
                    || (before == CompanionPose.Sit && f.Pose is CompanionPose.Groom or CompanionPose.Look or CompanionPose.Stretch);
                bool backToSit = before is CompanionPose.Groom or CompanionPose.Look or CompanionPose.Stretch && f.Pose == CompanionPose.Sit;
                if (started)
                {
                    acts++;
                    if (f.Cue == CompanionCue.Meow) meows++;
                    if (!double.IsNaN(last)) Assert.InRange(t - last, 4.0 - 1e-9, 9.0 + Dt + 1e-9);
                    last = t;
                }
                if (backToSit) last = t;   // compté depuis le retour assis
            }
        }
        Assert.Equal(12, stops);
        Assert.True(acts >= 24, $"actes : {acts}");
        Assert.True(meows >= 2, $"miaulements : {meows}");
    }

    private static double Distance(CompanionFollower f, double x, double z) => Math.Sqrt((f.X - x) * (f.X - x) + (f.Z - z) * (f.Z - z));
}
