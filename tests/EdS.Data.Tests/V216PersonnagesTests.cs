using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Validation;
using EdS.Runtime;
using EdS.World;
using Xunit;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.16 (souhait de Damien du 9 novembre 2026 : « designer des choses nous-mêmes comme les personnages ») : règles pures des
/// personnages faits dans MakeHuman (dossiers, fichier PNJ.txt, budgets) et du créateur de personnage (choix, aspect gardé dans la
/// sauvegarde v17). Affichage seulement : rien ne revient vers le moteur.
/// </summary>
[Trait("Category", "Runtime")]
public sealed class V216PersonnagesTests
{
    private const string Root = "Assets/Style/MakeHuman/";

    private static IReadOnlyList<MadeHumanModel> Made(params string[] paths) => MadeHumans.Models(paths);

    // --- Dossiers de MakeHuman ---------------------------------------------------------------------------------------

    [Fact]
    public void Un_personnage_par_dossier_dans_l_ordre_alphabetique()
    {
        IReadOnlyList<MadeHumanModel> m = Made(Root + "Odile/Odile.fbx", Root + "Gaspard le Roux/Gaspard.fbx", Root + "Gaspard le Roux/Textures/skin.png",
            Root + "Odile/autre.fbx", "Assets/Style/Mixamo/Archer/Archer.fbx", Root + "PNJ.txt", Root + "Marc\\Marc.fbx");
        Assert.Equal(new[] { "gaspardleroux", "marc", "odile" }, m.Select(x => x.Name));
        Assert.Equal("Gaspard le Roux", m[0].Folder);
        Assert.Equal(Root + "Odile/Odile.fbx", m[2].Path);   // le fichier qui porte le nom du dossier d'abord
        Assert.Equal(Root + "Marc\\Marc.fbx", m[1].Path);
    }

    [Fact]
    public void Fichier_de_personnage_reconnu_seulement_sous_MakeHuman()
    {
        Assert.True(MadeHumans.IsMadeHumanFile("Assets/Style/MakeHuman/Odile/Odile.fbx"));
        Assert.True(MadeHumans.IsMadeHumanFile("Assets/Style/makehuman/odile/o.FBX"));
        Assert.False(MadeHumans.IsMadeHumanFile("Assets/Style/MakeHuman/Odile/Textures/o.fbx"));   // un sous-dossier n'est pas un personnage
        Assert.False(MadeHumans.IsMadeHumanFile("Assets/Style/MakeHuman/Odile.fbx"));
        Assert.False(MadeHumans.IsMadeHumanFile("Assets/Style/Mixamo/Archer/Archer.fbx"));
        Assert.False(MadeHumans.IsMadeHumanFile("Assets/Style/MakeHuman/Odile/Odile.png"));
        Assert.False(MadeHumans.IsMadeHumanFile(null));
        Assert.True(MadeHumans.IsUnderFolder("Assets/Style/MakeHuman/Odile/Textures/o.png"));
        Assert.False(MadeHumans.IsUnderFolder("Assets/Style/Mixamo/o.png"));
        Assert.Empty(MadeHumans.Models(null));
    }

    [Theory]
    [InlineData("Élodie", "elodie")]
    [InlineData("Maëlle la Forgeronne", "maellelaforgeronne")]
    [InlineData("Françoise_2", "francoise2")]
    [InlineData("Cœur d'Æther", "coeurdaether")]
    [InlineData("日本", "")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Cle_ascii_sans_accent(string? name, string key) => Assert.Equal(key, MadeHumans.AsciiKey(name));

    [Fact]
    public void Cle_bornee_a_40_caracteres_et_acceptee_par_la_sauvegarde()
    {
        string longName = new string('a', 41);
        Assert.Equal(40, MadeHumans.AsciiKey(longName).Length);
        IReadOnlyList<MadeHumanModel> m = Made(Root + "Élodie/Elodie.fbx", Root + longName + "/x.fbx");
        Assert.Equal(new[] { new string('a', 40), "elodie" }, m.Select(x => x.Name));
        foreach (MadeHumanModel model in m)
        {
            Assert.True(SavedLook.IsBody(MadeHumans.BodyKey(model)));
            // Le créateur ne lève plus d'exception avec ces corps.
            SavedLook look = new SavedLook("male", 1, false, false, 0, 0);
            for (int i = 0; i < 4; i++) look = HeroLooks.Cycle(look, HeroPart.Body, 1, m);
            Assert.Equal("Élodie", HeroLooks.BodyName("made.elodie", m));
        }
        // Un personnage accentué se retrouve par PNJ.txt, avec ou sans accent.
        Assert.Equal("elodie", MadeHumans.ModelFor(MadeHumans.ParseMapping("npc.Élodie = Élodie.fbx"), m, "npc.elodie")!.Name);
        Assert.Equal("elodie", MadeHumans.ModelFor(null, m, "npc.Élodie")!.Name);
    }

    [Fact]
    public void Dossier_sans_cle_ou_en_collision_ignore_et_signale()
    {
        IReadOnlyList<MadeHumanModel> m = MadeHumans.Models(new[] { Root + "Élodie/a.fbx", Root + "Elodie/b.fbx", Root + "日本/c.fbx", Root + "Marc/d.fbx" }, out IReadOnlyList<string> skipped);
        Assert.Equal(new[] { "elodie", "marc" }, m.Select(x => x.Name));
        Assert.Equal("Elodie", m[0].Folder);   // le premier dans l'ordre ordinal garde la clé
        Assert.Equal(2, skipped.Count);
        Assert.Contains(skipped, x => x.Contains("日本", StringComparison.Ordinal) && x.Contains("ni lettre ni chiffre", StringComparison.Ordinal));
        Assert.Contains(skipped, x => x.Contains("Élodie", StringComparison.Ordinal) && x.Contains("même clé", StringComparison.Ordinal));
        MadeHumans.Models(new[] { Root + "Marc/d.fbx" }, out IReadOnlyList<string> none);
        Assert.Empty(none);
    }

    // --- Fichier de correspondance -----------------------------------------------------------------------------------

    [Fact]
    public void Fichier_PNJ_lu_avec_commentaires_et_lignes_illisibles_signalees()
    {
        string text = "﻿# qui porte quoi\r\ngaspard = Gaspard.fbx\n\n  npc.Odile = \"Odile\" \n// note\nheros = Aldric/Aldric.fbx\n; encore\nbourg/bavard1 = Marc\n"
            + "sans signe\n= Marc\nrenart =\ngaspard = Autre.fbx\n";
        MadeHumanMapping map = MadeHumans.ParseMapping(text);
        Assert.Equal(new[] { "bourgbavard1", "gaspard", "heros", "odile" }, map.Entries.Keys);
        Assert.Equal("autre", map.Entries["gaspard"]);   // la dernière ligne l'emporte
        Assert.Equal("aldric", map.Entries["heros"]);
        Assert.Equal(new[] { 9, 10, 11, 12 }, map.Problems.Select(p => p.Line));
        Assert.Contains("signe « = »", map.Problems[0].Message, StringComparison.Ordinal);
        Assert.Contains("déjà écrit", map.Problems[3].Message, StringComparison.Ordinal);
        Assert.StartsWith("PNJ.txt, ligne 9 : ", map.Problems[0].ToString(), StringComparison.Ordinal);
        Assert.Empty(MadeHumans.ParseMapping(null).Entries);
        Assert.Empty(MadeHumans.ParseMapping("").Problems);
        Assert.Empty(MadeHumanMapping.Empty.Entries);
    }

    [Fact]
    public void Choix_du_personnage_d_un_PNJ_par_la_ligne_puis_par_le_dossier()
    {
        IReadOnlyList<MadeHumanModel> m = Made(Root + "Gaspard/Gaspard.fbx", Root + "Odile/Odile.fbx", Root + "Vieux Marc/Marc.fbx");
        MadeHumanMapping map = MadeHumans.ParseMapping("renart = Gaspard.fbx\nnpc.odile = Marc.fbx\nboulanger = Absent.fbx\nheros = Odile\nbourg/bavard1 = Vieux Marc");
        Assert.Equal("gaspard", MadeHumans.ModelFor(map, m, "npc.renart")!.Name);
        Assert.Equal("vieuxmarc", MadeHumans.ModelFor(map, m, "npc.odile")!.Name);      // la ligne passe avant le dossier du même nom
        Assert.Equal("vieuxmarc", MadeHumans.ModelFor(map, m, "npc.odile_2")!.Name);    // l'identifiant complet, puis le prénom
        Assert.Equal("odile", MadeHumans.ModelFor(map, m, "heros")!.Name);
        Assert.Equal("vieuxmarc", MadeHumans.ModelFor(map, m, "bourg/bavard1")!.Name);
        Assert.Equal("gaspard", MadeHumans.ModelFor(map, m, "npc.gaspard")!.Name);      // sans ligne : le dossier au nom du PNJ
        Assert.Equal("gaspard", MadeHumans.ModelFor(null, m, "npc.gaspard")!.Name);
        Assert.Null(MadeHumans.ModelFor(map, m, "npc.boulanger"));                       // visé mais absent : le PNJ garde son corps
        Assert.Null(MadeHumans.ModelFor(map, m, "npc.inconnu"));
        Assert.Null(MadeHumans.ModelFor(map, m, null));
        Assert.Null(MadeHumans.ModelFor(map, Array.Empty<MadeHumanModel>(), "npc.odile"));
        Assert.Null(MadeHumans.ModelFor(map, null, "npc.odile"));
        Assert.Equal(new[] { "boulanger = absent" }, MadeHumans.Missing(map, m));
        Assert.Empty(MadeHumans.Missing(null, m));
    }

    [Fact]
    public void Le_modele_peut_etre_vise_par_son_fichier_aussi()
    {
        IReadOnlyList<MadeHumanModel> m = Made(Root + "Grand Gaspard/Gaspard_v2.fbx");
        Assert.Equal("grandgaspard", MadeHumans.ModelFor(MadeHumans.ParseMapping("renart = gaspard_v2.fbx"), m, "npc.renart")!.Name);
        Assert.Equal("grandgaspard", MadeHumans.ModelFor(MadeHumans.ParseMapping("renart = Grand Gaspard/Gaspard_v2.fbx"), m, "npc.renart")!.Name);
    }

    [Fact]
    public void Modele_vise_par_son_fichier_quand_le_catalogue_garde_le_chemin_du_FBX()
    {
        // Comme le catalogue du jeu : clé du dossier + chemin d'origine du FBX (champ File), qui ne porte pas le nom du dossier.
        var m = new[] { new MadeHumanModel("grandgaspard", "Grand Gaspard", Root + "Grand Gaspard/Gaspard_v2.fbx") };
        MadeHumanMapping map = MadeHumans.ParseMapping("renart = Gaspard_v2.fbx\nodile = grand gaspard");
        Assert.Equal("grandgaspard", MadeHumans.ModelFor(map, m, "npc.renart")!.Name);
        Assert.Equal("grandgaspard", MadeHumans.ModelFor(map, m, "npc.odile")!.Name);
        Assert.Empty(MadeHumans.Missing(map, m));
        // Sans chemin gardé (ancien catalogue : le nom du dossier tient lieu de chemin), le dossier se vise encore.
        var old = new[] { new MadeHumanModel("grandgaspard", "Grand Gaspard", "grandgaspard") };
        Assert.Equal("grandgaspard", MadeHumans.ModelFor(MadeHumans.ParseMapping("odile = Grand Gaspard"), old, "npc.odile")!.Name);
    }

    // --- Budgets pour un PC modeste ------------------------------------------------------------------------------------

    [Fact]
    public void Budgets_de_textures_et_de_triangles_pour_le_PC_modeste()
    {
        Assert.Equal((512, 1024, 2048), (MadeHumans.TextureCap(false, true), MadeHumans.TextureCap(true, false), MadeHumans.TextureCap(false, false)));
        Assert.Equal((15000, 30000), (MadeHumans.TriangleBudget(true), MadeHumans.TriangleBudget(false)));
        Assert.Null(MadeHumans.BudgetNote("Odile", 12000, true));
        Assert.Null(MadeHumans.BudgetNote("Odile", 0, true));
        Assert.Contains("conseillés en mode PC modeste", MadeHumans.BudgetNote("Odile", 20000, true)!, StringComparison.Ordinal);
        Assert.Null(MadeHumans.BudgetNote("Odile", 20000, false));
        Assert.Contains("trop lourd", MadeHumans.BudgetNote("Odile", 70000, false)!, StringComparison.Ordinal);
        Assert.True(MadeHumans.TooHeavy(60001));
        Assert.False(MadeHumans.TooHeavy(60000));
    }

    [Theory]
    [InlineData("eyebrow001", true)]
    [InlineData("Eyelashes01", true)]
    [InlineData("short02", true)]
    [InlineData("ponytail01", true)]
    [InlineData("bob01", true)]
    [InlineData("goatee", true)]
    [InlineData("young_caucasian_male", false)]
    [InlineData("brown_eye", false)]
    [InlineData("male_casualsuit01", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Matieres_decoupees_pour_les_cheveux_sourcils_et_cils(string? name, bool cutout) =>
        Assert.Equal(cutout, MadeHumans.IsCutoutMaterial(name));

    [Theory]
    [InlineData("CC0", true)]
    [InlineData("cc0 1.0", true)]
    [InlineData("CC-BY 4.0", true)]
    [InlineData("CC BY", true)]
    [InlineData("Domaine public (public domain)", false)]
    [InlineData("public domain", true)]
    [InlineData("CC BY-NC 4.0", false)]
    [InlineData("CC-BY-ND", false)]
    [InlineData("CC BY-SA 3.0", false)]
    [InlineData("AGPL", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Licences_permises_CC0_et_CC_BY_seulement(string? license, bool allowed) =>
        Assert.Equal(allowed, MadeHumans.IsAllowedLicense(license));

    [Fact]
    public void Manifeste_modele_et_licence_declaree()
    {
        string template = MadeHumans.ManifestTemplate("Gaspard le Roux");
        Assert.StartsWith("# Gaspard le Roux : personnage fait dans MakeHuman", template, StringComparison.Ordinal);
        Assert.Equal("CC0", MadeHumans.ManifestLicense(template));
        Assert.True(MadeHumans.IsAllowedLicense(MadeHumans.ManifestLicense(template)));
        Assert.Equal("CC BY-NC 4.0", MadeHumans.ManifestLicense("# x\r\n* Licence du personnage : CC BY-NC 4.0\r\n"));
        Assert.Null(MadeHumans.ManifestLicense("# sans licence\n- Fait par : Damien\n"));
        Assert.Null(MadeHumans.ManifestLicense("- Licence du personnage :\n"));
        Assert.Null(MadeHumans.ManifestLicense(null));
        Assert.Equal("MANIFESTE.md", MadeHumans.ManifestFile);
    }

    // --- Créateur de personnage ----------------------------------------------------------------------------------------

    [Fact]
    public void Les_bornes_de_la_sauvegarde_suivent_les_tables_d_affichage()
    {
        Assert.Equal(SavedLook.MaxHair, Enum.GetValues(typeof(HumanHair)).Cast<int>().Max());
        Assert.Equal(SavedLook.MaxHairTone, HumanLooks.HairTones.Length - 1);
        Assert.Equal(SavedLook.MaxPalette, HumanLooks.Palettes.Length - 1);
        Assert.Equal(HumanLooks.HairTones.Length, HeroLooks.HairToneNames.Length);
        Assert.Equal(HumanLooks.Palettes.Length, HeroLooks.PaletteNames.Length);
        Assert.Equal(Enum.GetValues(typeof(HumanHair)).Length, HeroLooks.HairNames.Length);
        Assert.Equal(HumanLooks.Palettes.Length, Enumerable.Range(0, 13).Count(p => HeroLooks.ClothesTint(p) < HumanWardrobe.Tints.Length));
        Assert.Equal(0, HeroLooks.ClothesTint(99));
    }

    [Fact]
    public void Aspect_de_depart_est_celui_du_heros_par_defaut()
    {
        SavedLook d = HeroLooks.Default();
        HumanLook h = HumanLooks.For("heros", "heros");
        Assert.Equal((h.Female, h.DarkSkin, (int)h.Hair, h.Beard, h.HairTone, h.Palette), (d.Body == "female", d.DarkSkin, d.Hair, d.Beard, d.HairTone, d.Palette));
        Assert.True(HeroLooks.IsDefault(null));
        Assert.True(HeroLooks.IsDefault(d));
        Assert.False(HeroLooks.IsDefault(HeroLooks.Cycle(d, HeroPart.Skin, 1, null)));
        Assert.Equal(d, HeroLooks.Normalize(null, null));
    }

    [Fact]
    public void Chaque_choix_fait_le_tour_et_revient()
    {
        SavedLook start = new SavedLook("male", 3, false, false, 1, 0);
        IReadOnlyList<MadeHumanModel> made = Made(Root + "Odile/Odile.fbx");
        foreach ((HeroPart part, int turns) in new[] { (HeroPart.Body, 3), (HeroPart.Hair, 4), (HeroPart.Skin, 2), (HeroPart.HairTone, 6), (HeroPart.Outfit, 13) })
        {
            SavedLook l = start;
            var seen = new HashSet<SavedLook>();
            for (int i = 0; i < turns; i++)
            {
                l = HeroLooks.Cycle(l, part, 1, made);
                seen.Add(l);
            }
            Assert.Equal(start, l);   // un tour complet
            Assert.Equal(turns, seen.Count);
            // En arrière : un pas avant puis un pas arrière ramène au point de départ.
            Assert.Equal(start, HeroLooks.Cycle(HeroLooks.Cycle(start, part, 1, made), part, -1, made));
        }
        Assert.True(HeroLooks.Cycle(start, HeroPart.Beard, 1, null).Beard);
    }

    [Fact]
    public void Changer_de_corps_garde_une_coiffure_et_une_barbe_qui_lui_vont()
    {
        // Barbe d'homme + chignons : passer à la femme retire la barbe et garde une coiffure de femme.
        SavedLook man = new SavedLook("male", (int)HumanHair.SimpleParted, true, false, 2, 3);
        SavedLook woman = HeroLooks.Cycle(man, HeroPart.Body, 1, null);
        Assert.Equal("female", woman.Body);
        Assert.False(woman.Beard);
        Assert.Contains((HumanHair)woman.Hair, HeroLooks.HairsFor("female"));
        Assert.DoesNotContain(HumanHair.SimpleParted, HeroLooks.HairsFor("female"));
        Assert.Equal((2, 3), (woman.HairTone, woman.Palette));   // les couleurs restent
        // Elle ne peut pas prendre la barbe.
        Assert.False(HeroLooks.Applies(HeroPart.Beard, "female"));
        Assert.Equal(woman, HeroLooks.Cycle(woman, HeroPart.Beard, 1, null));
        // Les coiffures de l'homme restent à l'homme.
        SavedLook shaved = new SavedLook("female", (int)HumanHair.Buns, false, false, 0, 0);
        Assert.Equal((int)HumanHair.Long, HeroLooks.Cycle(shaved, HeroPart.Hair, -1, null).Hair);
    }

    [Fact]
    public void Personnage_de_MakeHuman_a_sa_peau_ses_cheveux_et_ses_vetements()
    {
        IReadOnlyList<MadeHumanModel> made = Made(Root + "Odile/Odile.fbx", Root + "Marc/Marc.fbx");
        Assert.Equal(new[] { "male", "female", "made.marc", "made.odile" }, HeroLooks.Bodies(made));
        SavedLook l = new SavedLook("male", 2, true, false, 1, 4);
        l = HeroLooks.Cycle(l, HeroPart.Body, 1, made);
        l = HeroLooks.Cycle(l, HeroPart.Body, 1, made);
        Assert.Equal("made.marc", l.Body);
        Assert.Equal("Marc", HeroLooks.BodyName(l.Body, made));
        Assert.Equal("marc", HeroLooks.MadeOf(l.Body, made)!.Name);
        foreach (HeroPart part in new[] { HeroPart.Hair, HeroPart.Beard, HeroPart.Skin, HeroPart.HairTone, HeroPart.Outfit })
        {
            Assert.False(HeroLooks.Applies(part, l.Body));
            Assert.Equal(l, HeroLooks.Cycle(l, part, 1, made));   // rien ne change
            Assert.Equal("fait par le personnage", HeroLooks.ValueText(l, part, made));
        }
        // Le dossier a disparu : retour au corps d'homme, sans erreur.
        SavedLook back = HeroLooks.Normalize(l, Made(Root + "Odile/Odile.fbx"));
        Assert.Equal("male", back.Body);
        Assert.Equal("marc (absent)", HeroLooks.BodyName("made.marc", Made()));
        Assert.Equal("Homme", HeroLooks.ValueText(back, HeroPart.Body, null));
    }

    [Fact]
    public void Aspect_vers_corps_de_quaternius()
    {
        HumanLook h = HeroLooks.ToHumanLook(new SavedLook("female", (int)HumanHair.Buns, false, true, 3, 9));
        Assert.Equal((true, true, HumanHair.Buns, false, 3, HumanOutfit.Merchant, 9), (h.Female, h.DarkSkin, h.Hair, h.Beard, h.HairTone, h.Outfit, h.Palette));
        Assert.True(h.Height < HeroLooks.ToHumanLook(new SavedLook("male", 1, true, false, 0, 0)).Height);
        Assert.True(HeroLooks.ToHumanLook(new SavedLook("male", 1, true, false, 0, 0)).Beard);
        Assert.Equal("Prune", HeroLooks.ValueText(new SavedLook("male", 1, false, false, 0, 9), HeroPart.Outfit, null));
        Assert.Equal("Corps", HeroLooks.PartName(HeroPart.Body));
    }

    // --- Sauvegarde v17 --------------------------------------------------------------------------------------------------

    private static WorldSave SaveWith(SavedLook? look) =>
        new WorldSave("zone.vallons", 10, 1, Array.Empty<string>(), new SavedCharacter("bastion", "class.bastion", 0).WithLook(look));

    [Fact]
    public void Sauvegarde_v17_de_l_aspect_et_v16_encore_lue()
    {
        Assert.Equal(17, SaveValidator.CurrentSchemaVersion);
        Assert.Equal(SaveValidator.LookSince, SaveValidator.CurrentSchemaVersion);
        var look = new SavedLook("made.odile", 3, false, true, 5, 12);
        string text = SaveJsonReader.Write(SaveWith(look));
        Assert.Contains("\"look\": {\"body\": \"made.odile\", \"hair\": 3, \"beard\": false, \"dark\": true, \"hairTone\": 5, \"palette\": 12}", text, StringComparison.Ordinal);
        SaveLoadResult read = SaveJsonReader.Read(text);
        Assert.True(read.IsValid, read.Report.ToString());
        Assert.Equal(look, read.Save!.Character!.Look);
        Assert.Equal(text, SaveJsonReader.Write(read.Save));   // octet par octet
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "save.schema.json")).IsValid(JsonDocument.Parse(text).RootElement));
        // Les autres champs (stock, cicatrices) gardent leur virgule avec ou sans aspect.
        WorldSave full = new WorldSave("zone.vallons", 10, 1, Array.Empty<string>(), SaveWith(look).Character!
            .WithStock(new[] { new SavedStock("merchant.herboriste/item.peau_de_loup", 0, 3, 1) }));
        string fullText = SaveJsonReader.Write(full);
        Assert.True(SaveJsonReader.Read(fullText).IsValid, SaveJsonReader.Read(fullText).Report.ToString());
        Assert.Equal(look, SaveJsonReader.Read(fullText).Save!.Character!.Look);
        Assert.Equal(fullText, SaveJsonReader.Write(SaveJsonReader.Read(fullText).Save!));
        // Sans aspect choisi : pas de champ, et le héros garde l'aspect par défaut.
        string none = SaveJsonReader.Write(SaveWith(null));
        Assert.DoesNotContain("\"look\"", none, StringComparison.Ordinal);
        Assert.Null(SaveJsonReader.Read(none).Save!.Character!.Look);
        // v16 : pas de « look » (champ inconnu) ; sans lui, lue telle quelle.
        SaveLoadResult refused = SaveJsonReader.Read(text.Replace($"\"schemaVersion\": {SaveValidator.CurrentSchemaVersion}", "\"schemaVersion\": 16"));
        Assert.Contains(refused.Report.Errors, e => e.Path.EndsWith(".look", StringComparison.Ordinal) && e.Code == ValidationCode.UnknownField);
        SaveLoadResult old = SaveJsonReader.Read(none.Replace($"\"schemaVersion\": {SaveValidator.CurrentSchemaVersion}", "\"schemaVersion\": 16"));
        Assert.True(old.IsValid, old.Report.ToString());
        Assert.Null(old.Save!.Character!.Look);
    }

    [Theory]
    [InlineData("\"body\": \"made.odile\"", "\"body\": \"Odile\"", ValidationCode.InvalidValue, ".body")]
    [InlineData("\"body\": \"made.odile\"", "\"body\": \"made.\"", ValidationCode.InvalidValue, ".body")]
    [InlineData("\"hair\": 3", "\"hair\": 6", ValidationCode.OutOfRange, ".hair")]
    [InlineData("\"hair\": 3", "\"hair\": -1", ValidationCode.OutOfRange, ".hair")]
    [InlineData("\"hairTone\": 5", "\"hairTone\": 6", ValidationCode.OutOfRange, ".hairTone")]
    [InlineData("\"palette\": 12", "\"palette\": 13", ValidationCode.OutOfRange, ".palette")]
    public void Aspect_hors_format_refuse_au_chemin_precis(string from, string to, ValidationCode code, string suffix)
    {
        string text = SaveJsonReader.Write(SaveWith(new SavedLook("made.odile", 3, false, true, 5, 12)));
        Assert.Contains(SaveJsonReader.Read(text.Replace(from, to)).Report.Errors, e => e.Code == code && e.Path.EndsWith(suffix, StringComparison.Ordinal));
    }

    [Fact]
    public void Aspect_incomplet_ou_mal_forme_refuse()
    {
        string text = SaveJsonReader.Write(SaveWith(new SavedLook("male", 1, true, false, 0, 0)));
        Assert.Contains(SaveJsonReader.Read(text.Replace("\"beard\": true, ", "")).Report.Errors, e => e.Path.EndsWith(".look.beard", StringComparison.Ordinal));
        Assert.Contains(SaveJsonReader.Read(text.Replace("\"dark\": false", "\"dark\": 1")).Report.Errors, e => e.Path.EndsWith(".dark", StringComparison.Ordinal));
        Assert.False(SaveJsonReader.Read(text.Replace("\"look\": {", "\"look\": {\"inconnu\": 1, ")).IsValid);
        Assert.Throws<ArgumentException>(() => new SavedLook("Odile", 0, false, false, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SavedLook("male", 6, false, false, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SavedLook("male", 0, false, false, 0, 13));
    }

    [Fact]
    public void Aspect_garde_par_les_autres_modifications_du_personnage()
    {
        var look = new SavedLook("female", 4, false, false, 2, 7);
        SavedCharacter c = new SavedCharacter("bastion", "class.bastion", 0).WithLook(look);
        Assert.Equal(look, c.WithXp(500).WithMoney(12).WithBoons(Array.Empty<SavedBoon>()).WithEvents(Array.Empty<SavedEvent>()).WithScars(Array.Empty<SavedScar>()).Look);
        Assert.Null(c.WithLook(null).Look);
    }

    [Fact]
    public void Ecriture_qui_echoue_laisse_l_ancien_aspect_et_permet_de_reessayer()
    {
        string folder = Path.Combine(Path.GetTempPath(), "eds_v216_" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(folder, "un fichier à la place du dossier : l'écriture de la sauvegarde échoue");
        try
        {
            AdventureSession a = AdventureSession.Load(ScenarioTestData.DataDirectory, Path.Combine(AppContext.BaseDirectory, "scenarios"),
                Path.Combine(folder, "sauvegarde.json"));
            var look = new SavedLook("female", 4, false, true, 2, 7);
            Assert.Null(a.HeroLook);
            Assert.ThrowsAny<IOException>(() => a.ChooseLook(look));
            Assert.Null(a.HeroLook);   // pas d'aspect appliqué sans sauvegarde
            // Le disque redevient utilisable : la même demande est de nouveau tentée (pas de « rien n'a changé »).
            File.Delete(folder);
            a.ChooseLook(look);
            Assert.Equal(look, a.HeroLook);
            AdventureSession again = AdventureSession.Load(ScenarioTestData.DataDirectory, Path.Combine(AppContext.BaseDirectory, "scenarios"),
                Path.Combine(folder, "sauvegarde.json"));
            Assert.Equal(look, again.HeroLook);
            Assert.False(again.IsNewGame);
            a.ChooseLook(look);   // inchangé : rien à écrire
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            else if (File.Exists(folder)) File.Delete(folder);
        }
    }
}
