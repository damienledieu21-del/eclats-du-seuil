using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Runtime;
using EdS.World;
using Xunit;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.9.4 (« vrais vêtements des villageois ») : reconnaissance des pièces des Modular Character Outfits - Fantasy de Quaternius,
/// tenue par métier, saison, pluie, fête, usure, choix déterministe des pièces et replis (jamais de corps à moitié habillé). Affichage seulement.
/// </summary>
[Trait("Category", "Runtime")]
public sealed class V294VetementsTests
{
    private const string O = "Assets/Style/Quaternius/Outfits/FBX (Unity)/";

    /// <summary>Un pack complet supposé (noms à la manière de Quaternius : « Male_Peasant_Body »), pour les deux corps.</summary>
    private static readonly string[] Full = new[] { "Male", "Female" }.SelectMany(g => new[]
    {
        g + "_Peasant_Body", g + "_Peasant_Arms", g + "_Peasant_Legs", g + "_Peasant_Feet", g + "_Peasant_Hat",
        g + "_Ranger_Body", g + "_Ranger_Arms", g + "_Ranger_Legs", g + "_Ranger_Feet", g + "_Ranger_Hood", g + "_Ranger_Pauldrons", g + "_Ranger_Cape",
        g + "_Knight_Body", g + "_Knight_Arms", g + "_Knight_Legs", g + "_Knight_Feet", g + "_Knight_Helmet", g + "_Knight_Helmet_Horned",
        g + "_Knight_Pauldrons", g + "_Knight_Pauldrons_Spiked", g + "_Knight_Pauldrons_Lion", g + "_Knight_Gorget",
        g + "_Noble_Body", g + "_Noble_Arms", g + "_Noble_Legs", g + "_Noble_Feet", g + "_Noble_Hat", g + "_Noble_Circlet", g + "_Noble_Cloak",
        g + "_Wizard_Body", g + "_Wizard_Arms", g + "_Wizard_Legs", g + "_Wizard_Feet", g + "_Wizard_Hat",
    }).Select(n => O + n + ".fbx").ToArray();

    private static List<WardrobeItem> Items(IEnumerable<string> files) =>
        files.Select(f => HumanWardrobe.Classify(f, string.Empty)).Where(i => i != null).Select(i => i!.Value).ToList();

    [Fact]
    public void Les_pieces_sont_reconnues_par_famille_place_et_corps()
    {
        WardrobeItem hood = HumanWardrobe.Classify(O + "Female_Ranger_Hood.fbx", "")!.Value;
        Assert.Equal((WardrobeFamily.Ranger, WardrobeSlot.Head, "hood", 2), (hood.Family, hood.Slot, hood.Kind, hood.Gender));
        WardrobeItem arms = HumanWardrobe.Classify(O + "Male_Knight_Arms.fbx", "")!.Value;
        Assert.Equal((WardrobeFamily.Knight, WardrobeSlot.Arms, 1), (arms.Family, arms.Slot, arms.Gender));
        // « Armet » est un casque, pas un bras ; « Peasant_Head » seul serait une tête ; un maillage nommé l'emporte sur le fichier.
        Assert.Equal(WardrobeSlot.Head, HumanWardrobe.Classify(O + "Knight_Armet.fbx", "")!.Value.Slot);
        Assert.Null(HumanWardrobe.Classify(O + "Peasant_Body.fbx", "Head"));
        Assert.Equal(WardrobeSlot.Legs, HumanWardrobe.Classify(O + "Male_Peasant.fbx", "Peasant_Legs")!.Value.Slot);
        Assert.Equal(WardrobeSlot.Head, HumanWardrobe.Classify(O + "Male_Ranger.fbx", "RangerHood")!.Value.Slot);
        Assert.Equal(WardrobeSlot.Feet, HumanWardrobe.Classify(O + "Female_Wizard.fbx", "Wizard_Boots")!.Value.Slot);
        Assert.Equal(WardrobeSlot.Back, HumanWardrobe.Classify(O + "Noble_Cloak.fbx", "")!.Value.Slot);
        // Hors du dossier des vêtements, niveau de détail réduit, cheveux, corps nu : rien.
        Assert.Null(HumanWardrobe.Classify("Assets/Style/Quaternius/BaseCharacters/Male_Peasant_Body.fbx", ""));
        Assert.Null(HumanWardrobe.Classify(O + "Male_Peasant_Body_LOD1.fbx", ""));
        Assert.Null(HumanWardrobe.Classify(O + "Hair_Long.fbx", ""));
        Assert.Null(HumanWardrobe.Classify(O + "Superhero_Male_FullBody.fbx", ""));
        Assert.True(HumanWardrobe.IsWardrobePath("Assets/Style/Quaternius/Modular Character Outfits - Fantasy[Standard]/FBX/Knight.fbx"));
        Assert.Equal(Full.Length, Items(Full).Count);
    }

    [Fact]
    public void Les_pieces_du_pack_ne_sont_jamais_un_heros_ni_un_decor()
    {
        string[] models = { O + "Male_Knight_Body.fbx", "Assets/Style/Mixamo/Paladin/Paladin.fbx" };
        Assert.DoesNotContain(StyleRules.Candidates(models, StyleSlot.Hero), p => p.Contains("Outfits"));
        Assert.Single(StyleRules.Candidates(models, StyleSlot.Hero));
    }

    private static HumanLook Look(string id, string role) => HumanLooks.For(id, role);

    private static (int[]? Picks, WardrobePlan Plan) Dress(string id, string role, IReadOnlyList<WardrobeItem> items, WardrobeSeason season = WardrobeSeason.Spring,
        bool rain = false, bool festive = false)
    {
        HumanLook look = Look(id, role);
        WardrobePlan plan = HumanWardrobe.Plan(id, role, look, season, rain, festive);
        return (HumanWardrobe.Resolve(plan, items, look.Female), plan);
    }

    private static IEnumerable<WardrobeItem> Worn(IReadOnlyList<WardrobeItem> items, int[] picks) => picks.Where(p => p >= 0).Select(p => items[p]);

    [Fact]
    public void Chaque_role_est_habille_de_pied_en_cap_avec_le_pack_complet()
    {
        var items = Items(Full);
        string[] ids =
        {
            "npc.odile", "npc.gaspard", "npc.maelle", "npc.albe", "npc.renart", "npc.aubergiste", "npc.capitaine", "npc.doyen", "npc.boulanger",
            "bourg/bavard1", "hameau/paysan3", "bourg/garde2", "tour/sentinelle1", "camp/bandit4", "foret/rodeur2", "heros",
        };
        string[] roles = { "", "", "", "", "", "", "", "", "", "paysan", "paysan", "garde", "sentinelle", "bandit", "rodeur", "heros" };
        for (int i = 0; i < ids.Length; i++)
            foreach (WardrobeSeason season in Enum.GetValues(typeof(WardrobeSeason)))
                foreach (bool festive in new[] { false, true })
                {
                    (int[]? picks, WardrobePlan plan) = Dress(ids[i], roles[i].Length > 0 ? roles[i] : ids[i], items, season, false, festive);
                    Assert.NotNull(picks);
                    HumanLook look = Look(ids[i], roles[i].Length > 0 ? roles[i] : ids[i]);
                    var worn = Worn(items, picks!).ToList();
                    foreach (WardrobeSlot slot in new[] { WardrobeSlot.Body, WardrobeSlot.Arms, WardrobeSlot.Legs, WardrobeSlot.Feet })
                        Assert.Single(worn, w => w.Slot == slot);
                    Assert.True(worn.Count(w => w.Slot == WardrobeSlot.Head) <= 1, ids[i] + " : deux coiffes");
                    // Le corps de l'homme porte les pièces d'homme, celui de la femme les pièces de femme.
                    Assert.All(worn, w => Assert.Equal(look.Female ? 2 : 1, w.Gender));
                }
    }

    [Fact]
    public void La_tenue_suit_le_metier()
    {
        var items = Items(Full);
        WardrobeFamily BodyOf(string id, string role, bool festive = false)
        {
            (int[]? p, WardrobePlan _) = Dress(id, role, items, WardrobeSeason.Spring, false, festive);
            return Worn(items, p!).First(w => w.Slot == WardrobeSlot.Body).Family;
        }
        Assert.Equal(WardrobeFamily.Knight, BodyOf("bourg/garde1", "garde"));
        Assert.Equal(WardrobeFamily.Wizard, BodyOf("npc.albe", "npc.albe"));
        Assert.Equal(WardrobeFamily.Noble, BodyOf("npc.renart", "npc.renart"));
        Assert.Equal(WardrobeFamily.Peasant, BodyOf("npc.boulanger", "npc.boulanger"));
        Assert.Equal(WardrobeFamily.Peasant, BodyOf("npc.maelle", "npc.maelle"));
        Assert.Equal(WardrobeFamily.Ranger, BodyOf("camp/bandit2", "bandit"));
        Assert.Equal(WardrobeFamily.Ranger, BodyOf("tour/sentinelle1", "sentinelle"));
        Assert.Equal(WardrobeFamily.Knight, BodyOf("heros", "heros"));
        // Un jour de fête, l'aubergiste sort le drap noble ; le capitaine laisse la cuirasse.
        Assert.Equal(WardrobeFamily.Noble, BodyOf("npc.aubergiste", "npc.aubergiste", true));
        Assert.Equal(WardrobeFamily.Noble, BodyOf("npc.capitaine", "npc.capitaine", true));
        // Les paysans : en majorité en paysans (le reste, si le pack n'a pas tout, se complète).
        int peasants = Enumerable.Range(0, 60).Count(i => BodyOf("hameau/paysan" + i, "paysan") == WardrobeFamily.Peasant);
        Assert.Equal(60, peasants);
        // Le capitaine a les épaulières au lion ; aucun garde n'a le casque à cornes.
        (int[]? cap, WardrobePlan _) = Dress("npc.capitaine", "npc.capitaine", items);
        Assert.Contains(Worn(items, cap!), w => w.Slot == WardrobeSlot.Shoulders && w.Key.Contains("lion"));
        for (int i = 0; i < 40; i++)
        {
            (int[]? g, WardrobePlan _) = Dress("bourg/garde" + i, "garde", items);
            Assert.DoesNotContain(Worn(items, g!), w => w.Key.Contains("horned"));
        }
    }

    [Fact]
    public void Saisons_pluie_et_fete_changent_la_tenue()
    {
        var items = Items(Full);
        int Count(WardrobeSeason season, bool rain, bool festive, WardrobeSlot slot, string kind) => Enumerable.Range(0, 80).Count(i =>
        {
            (int[]? p, WardrobePlan _) = Dress("hameau/paysan" + i, "paysan", items, season, rain, festive);
            return Worn(items, p!).Any(w => w.Slot == slot && (kind.Length == 0 || w.Kind == kind));
        });
        Assert.True(Count(WardrobeSeason.Winter, false, false, WardrobeSlot.Back, "cloak") > 55, "manteaux l'hiver");
        Assert.Equal(0, Count(WardrobeSeason.Summer, false, false, WardrobeSlot.Back, "cloak"));
        Assert.Equal(0, Count(WardrobeSeason.Summer, false, false, WardrobeSlot.Head, "hood"));
        Assert.Equal(80, Count(WardrobeSeason.Summer, true, false, WardrobeSlot.Head, "hood"));
        Assert.Equal(WardrobeSeason.Winter, HumanWardrobe.SeasonOf(12));
        Assert.Equal(WardrobeSeason.Winter, HumanWardrobe.SeasonOf(2));
        Assert.Equal(WardrobeSeason.Spring, HumanWardrobe.SeasonOf(3));
        Assert.Equal(WardrobeSeason.Summer, HumanWardrobe.SeasonOf(8));
        Assert.Equal(WardrobeSeason.Autumn, HumanWardrobe.SeasonOf(11));
        Assert.True(HumanWardrobe.IsRain(WeatherKind.Shower) && !HumanWardrobe.IsRain(WeatherKind.Mist));
        // La forge ne prend jamais de manteau ; les gardes ont un gorgerin l'hiver.
        (int[]? smith, WardrobePlan _) = Dress("npc.maelle", "npc.maelle", items, WardrobeSeason.Winter);
        Assert.DoesNotContain(Worn(items, smith!), w => w.Slot == WardrobeSlot.Back);
        (int[]? guard, WardrobePlan _) = Dress("bourg/garde1", "garde", items, WardrobeSeason.Winter);
        Assert.Contains(Worn(items, guard!), w => w.Slot == WardrobeSlot.Neck);
        // Fête : couleurs de fête, plus d'usure ; bandits toujours usés.
        WardrobePlan fete = HumanWardrobe.Plan("hameau/paysan1", "paysan", Look("hameau/paysan1", "paysan"), WardrobeSeason.Summer, false, true);
        Assert.True(fete.Tint >= 12 && fete.Wear == 0);
        Assert.Equal(2, HumanWardrobe.Plan("camp/bandit1", "bandit", Look("camp/bandit1", "bandit"), WardrobeSeason.Summer, false, true).Wear);
        Assert.Equal(0, HumanWardrobe.Plan("npc.renart", "npc.renart", Look("npc.renart", "npc.renart"), WardrobeSeason.Summer, false, false).Wear);
    }

    [Fact]
    public void Le_choix_est_deterministe_et_ne_touche_pas_aux_traits_du_corps()
    {
        var items = Items(Full);
        for (int i = 0; i < 30; i++)
        {
            string id = "bourg/bavard" + i;
            (int[]? a, WardrobePlan pa) = Dress(id, "paysan", items, WardrobeSeason.Autumn);
            (int[]? b, WardrobePlan pb) = Dress(id, "paysan", items, WardrobeSeason.Autumn);
            Assert.Equal(a, b);
            Assert.Equal((pa.Fabric, pa.Tint, pa.Wear), (pb.Fabric, pb.Tint, pb.Wear));
        }
        // Les traits du corps (genre, peau, taille) d'un PNJ ne dépendent pas du rôle d'affichage : les vêtements non plus.
        HumanLook x = Look("npc.odile", "paysan"), y = Look("npc.odile", "npc.odile");
        Assert.Equal((x.Female, x.DarkSkin, x.Height), (y.Female, y.DarkSkin, y.Height));
        (int[]? o1, WardrobePlan _) = Dress("npc.odile", "paysan", items);
        (int[]? o2, WardrobePlan _) = Dress("npc.odile", "npc.odile", items);
        Assert.Equal(o1, o2);
        // Des villageois différents ne sont pas tous pareils (pièces, étoffe ou teinte).
        var looks = Enumerable.Range(0, 30).Select(i =>
        {
            (int[]? p, WardrobePlan plan) = Dress("hameau/paysan" + i, "paysan", items, WardrobeSeason.Autumn);
            return string.Join(",", p!) + "|" + plan.Fabric + "|" + plan.Tint + "|" + plan.Wear;
        }).Distinct().Count();
        Assert.True(looks > 10, looks + " tenues différentes sur 30");
    }

    [Fact]
    public void Replis_un_pack_partiel_complete_et_un_pack_incomplet_rend_la_tenue_peinte()
    {
        // Le pack gratuit seul (supposé : paysan et rôdeur) : le garde prend des pièces de rôdeur, jamais rien de manquant.
        var partial = Items(Full.Where(f => f.Contains("Peasant") || f.Contains("Ranger")));
        (int[]? guard, WardrobePlan _) = Dress("bourg/garde1", "garde", partial);
        Assert.NotNull(guard);
        Assert.Equal(WardrobeFamily.Ranger, Worn(partial, guard!).First(w => w.Slot == WardrobeSlot.Body).Family);
        (int[]? priest, WardrobePlan _) = Dress("npc.albe", "npc.albe", partial);
        Assert.NotNull(priest);
        // Sans pieds pour ce corps : null (tenue peinte), jamais un corps à moitié habillé.
        var noFeet = Items(Full.Where(f => !f.EndsWith("_Feet.fbx")));
        Assert.Null(Dress("hameau/paysan1", "paysan", noFeet).Picks);
        Assert.Null(Dress("hameau/paysan1", "paysan", new List<WardrobeItem>()).Picks);
        // Des pièces faites pour les deux corps (genre inconnu) servent aux deux.
        var unisex = Items(new[] { "Peasant_Body", "Peasant_Arms", "Peasant_Legs", "Peasant_Feet" }.Select(n => O + n + ".fbx"));
        Assert.NotNull(Dress("npc.odile", "npc.odile", unisex).Picks);
        Assert.NotNull(Dress("npc.gaspard", "npc.gaspard", unisex).Picks);
    }

    [Fact]
    public void Couleurs_etoffes_et_peau()
    {
        (double r, double g, double b) neuf = HumanWardrobe.Color(0, 0), use = HumanWardrobe.Color(0, 2);
        Assert.Equal((1.0, 1.0, 1.0), neuf);
        Assert.True(use.r < 0.9 && use.b < use.r, "l'usure ternit et brunit");
        Assert.True(HumanWardrobe.Smoothness(0) > HumanWardrobe.Smoothness(2));
        Assert.True(HumanWardrobe.HidesHair("hood") && HumanWardrobe.HidesHair("helmet") && !HumanWardrobe.HidesHair("circlet"));
        Assert.True(HumanWardrobe.IsSkinMaterial("MI_Regular_Male") && !HumanWardrobe.IsSkinMaterial("MI_Ranger"));
        string[] textures =
        {
            O + "Textures/T_Peasant_1_BaseColor.png", O + "Textures/T_Peasant_2_BaseColor.png", O + "Textures/T_Peasant_3_BaseColor.png",
            O + "Textures/T_Peasant_Normal.png", O + "Textures/T_Ranger_1_BaseColor.png", O + "Textures/T_Regular_Male_Dark_BaseColor.png",
            O + "Textures/T_Regular_Male_Light_BaseColor.png", O + "Textures/T_Regular_Female_Light_BaseColor.png",
        };
        Assert.Equal(3, HumanWardrobe.FabricTextures(textures, WardrobeFamily.Peasant, 1).Count);
        Assert.EndsWith("T_Regular_Male_Dark_BaseColor.png", HumanWardrobe.SkinTexture(textures, false, true));
        Assert.EndsWith("T_Regular_Male_Light_BaseColor.png", HumanWardrobe.SkinTexture(textures, false, false));
        Assert.EndsWith("T_Regular_Female_Light_BaseColor.png", HumanWardrobe.SkinTexture(textures, true, false));
        Assert.Null(HumanWardrobe.SkinTexture(textures, true, true));
    }

    [Fact]
    public void Relecture_1_sans_tete_seule_qui_va_au_corps_pas_de_vrais_vetements()
    {
        Assert.True(HumanWardrobe.HeadFits(12566, 12566));
        Assert.False(HumanWardrobe.HeadFits(null, 12566));        // tête non préparée : tenue peinte, pas deux tenues l'une dans l'autre
        Assert.False(HumanWardrobe.HeadFits(12812, 12566));       // tête de l'autre corps
        Assert.False(HumanWardrobe.HeadFits(0, 0));
    }

    [Fact]
    public void Relecture_2_seuls_les_noms_de_peau_connus_sont_de_la_peau()
    {
        foreach (string skin in new[] { "MI_Regular_Male", "MI_Regular_Female", "MI_Regular_Male (Instance)", "MI_Superhero_Female", "Regular_Male" })
            Assert.True(HumanWardrobe.IsSkinMaterial(skin), skin);
        foreach (string cloth in new[] { "Body", "MI_Body_Ranger", "Regular_Belt", "MI_Ranger", "Skin_Leather", "MI_Peasant_Regular", "" })
            Assert.False(HumanWardrobe.IsSkinMaterial(cloth), cloth);
    }

    [Fact]
    public void Relecture_3_etoffes_reliees_par_le_nom_cartes_de_couleur_seulement()
    {
        Assert.Equal("ranger", HumanWardrobe.FabricStem("MI_Ranger"));
        Assert.Equal("ranger", HumanWardrobe.FabricStem(O + "Textures/T_Ranger_2_BaseColor.png"));
        Assert.Equal("rangerbelts", HumanWardrobe.FabricStem("MI_Ranger_Belts (Instance)"));
        Assert.Equal("rangerbelts", HumanWardrobe.FabricStem("T_Ranger_Belts_1_BaseColor"));
        Assert.NotEqual(HumanWardrobe.FabricStem("MI_Ranger"), HumanWardrobe.FabricStem("MI_Ranger_Belts"));
        Assert.True(HumanWardrobe.IsColorMap("T_Knight_1_BaseColor.png") && HumanWardrobe.IsColorMap("Wizard_Albedo.png") && HumanWardrobe.IsColorMap("Noble_Diffuse.tga"));
        foreach (string map in new[] { "T_Knight_Opacity.png", "T_Knight_Alpha.png", "T_Knight_Specular.png", "T_Knight_Detail_Color.png", "T_Knight_Bump.png",
                     "T_Knight_Normal.png", "T_Knight_1.png", "T_Regular_Male_BaseColor.png" })
            Assert.False(HumanWardrobe.IsColorMap(map), map);
    }

    [Fact]
    public void Relecture_4_dans_un_fichier_a_plusieurs_maillages_seul_le_nom_du_maillage_dit_la_place()
    {
        // Un maillage au nom inconnu n'est pas pris pour la pièce du fichier (« Male_Ranger_Body.fbx » qui porte aussi « Straps »).
        Assert.Null(HumanWardrobe.Classify(O + "Male_Ranger_Body.fbx", "Straps", true));
        Assert.Null(HumanWardrobe.Classify(O + "Male_Ranger_Body.fbx", "Cube.001", true));
        Assert.Equal(WardrobeSlot.Body, HumanWardrobe.Classify(O + "Male_Ranger_Body.fbx", "Ranger_Body", true)!.Value.Slot);
        WardrobeItem hood = HumanWardrobe.Classify(O + "Male_Ranger.fbx", "Hood", true)!.Value;
        Assert.Equal((WardrobeFamily.Ranger, WardrobeSlot.Head, 1), (hood.Family, hood.Slot, hood.Gender));   // famille et corps lus sur le fichier
        // Un fichier d'un seul maillage : le nom du fichier suffit.
        Assert.Equal(WardrobeSlot.Body, HumanWardrobe.Classify(O + "Male_Ranger_Body.fbx", "Cube.001", false)!.Value.Slot);
    }

    [Fact]
    public void Relecture_5_et_6_coiffure_sous_la_coiffe_et_plafond_du_telephone()
    {
        Assert.Equal(HumanHair.None, HumanWardrobe.HairUnder("hood", HumanHair.Long, true));
        Assert.Equal(HumanHair.None, HumanWardrobe.HairUnder("helmet", HumanHair.Buzzed, false));
        Assert.Equal(HumanHair.BuzzedFemale, HumanWardrobe.HairUnder("circlet", HumanHair.Buns, true));
        Assert.Equal(HumanHair.Buzzed, HumanWardrobe.HairUnder("hat", HumanHair.SimpleParted, false));
        Assert.Equal(HumanHair.Buzzed, HumanWardrobe.HairUnder("crown", HumanHair.Long, false));
        Assert.Equal(HumanHair.Long, HumanWardrobe.HairUnder("", HumanHair.Long, true));
        var items = Items(Full);
        for (int i = 0; i < 40; i++)
        {
            string id = "bourg/garde" + i;
            HumanLook look = Look(id, "garde");
            WardrobePlan phone = HumanWardrobe.Plan(id, "garde", look, WardrobeSeason.Winter, true, false, HumanWardrobe.MaxOptional(true));
            Assert.True(phone.Requests.Count(q => !q.Required) <= 1);
            Assert.Equal(4, phone.Requests.Count(q => q.Required));
            Assert.NotNull(HumanWardrobe.Resolve(phone, items, look.Female));
            WardrobePlan pc = HumanWardrobe.Plan(id, "garde", look, WardrobeSeason.Winter, true, false, HumanWardrobe.MaxOptional(false));
            // La coiffe passe d'abord sur téléphone.
            if (pc.Requests.Any(q => q.Slot == WardrobeSlot.Head)) Assert.Contains(phone.Requests, q => q.Slot == WardrobeSlot.Head);
        }
    }
}
