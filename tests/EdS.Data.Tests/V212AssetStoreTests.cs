using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EdS.Runtime;
using Xunit;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.12 (packs gratuits de l'Asset Store importés par Damien) : la liste partagée (<see cref="AssetStore"/>), ses replis, les
/// anachronismes écartés, les intérieurs en repli, les sons médiévaux, l'interface en bois, les icônes de CraftPix sans fond, le manifeste
/// des licences et le branchement dans la préparation, le jeu et le vérificateur. Affichage seulement.
/// </summary>
[Trait("Category", "Runtime")]
public sealed class V212AssetStoreTests
{
    private static string Doc(params string[] path) => File.ReadAllText(Path.Combine(new[] { Lot93UnityPackageTests.Root }.Concat(path).ToArray())).Replace("\r\n", "\n");

    private static string Script(params string[] path) => Doc(new[] { "unity", "Assets", "EdS" }.Concat(path).ToArray());

    private static readonly IReadOnlyList<string> AllFiles = AssetStore.Entries.SelectMany(e => e.Files).ToList();

    [Fact]
    public void Chaque_role_de_l_Asset_Store_a_une_ressource_hors_Assets_Style_dans_son_pack()
    {
        // Les rôles V2.12 viennent après tous ceux de la V2.11 (valeurs sérialisées du catalogue inchangées).
        Assert.True(AssetStore.Entries.All(e => (int)e.Role > (int)Wave5Role.Tankard));
        Assert.Equal(AssetStore.Entries.Count, AssetStore.Entries.Select(e => e.Role).Distinct().Count());
        Assert.Empty(AssetStore.Entries.Select(e => e.Role).Intersect(Wave5.Entries.Select(e => e.Role)));
        foreach (Wave5Entry e in AssetStore.Entries)
        {
            Assert.Equal(e.Files.Count, e.Credits.Count);
            Assert.NotEmpty(e.Label);
            Assert.NotEmpty(e.Without);
            Assert.All(e.Credits, c => Assert.Equal(AssetStore.License, c.License));
            Assert.All(e.Credits, c => Assert.False(c.Attribution));
            foreach (string f in e.Files)
            {
                Assert.StartsWith("Assets/", f);
                Assert.DoesNotContain(EssaiManifest.Root + "/", f);   // à la racine de Assets, pas dans Assets/Style
                Assert.EndsWith(".prefab", f);
                Assert.Contains(AssetStore.Packs, p => f.StartsWith(p.Folder + "/", StringComparison.Ordinal));
                Assert.DoesNotContain("HD RENDER PIPELINE", f);       // le dossier HDRP de RPG Food Props n'est jamais pris
            }
        }
        // Un fichier n'appartient qu'à une ressource.
        Assert.Equal(AllFiles.Count, AllFiles.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        // Branchées, puis prêtes, puis intérieurs.
        List<Wave5Use> uses = AssetStore.Entries.Select(e => e.Use).ToList();
        Assert.Equal(uses.OrderBy(u => u == Wave5Use.Live ? 0 : u == Wave5Use.Ready ? 1 : 2), uses);
        // La liste de la V2.11 les retrouve (Wave5View.Place, StyleCatalog.Shown) sans les compter parmi les siennes.
        Assert.Same(AssetStore.EntryOrNull(Wave5Role.TraderTable), Wave5.EntryOrNull(Wave5Role.TraderTable));
        Assert.True(AssetStore.IsStoreRole(Wave5Role.StoreBread));
        Assert.False(AssetStore.IsStoreRole(Wave5Role.Bread));
    }

    [Fact]
    public void Prefab_reconnu_par_la_fin_de_son_chemin_meilleur_puis_repli()
    {
        Wave5Entry bread = AssetStore.EntryOrNull(Wave5Role.StoreBread)!;
        string best = bread.Files[0], croissant = bread.Files[1];
        // Un pack rangé dans un sous-dossier compte ; la casse et les barres obliques inverses aussi.
        string moved = "Assets/Packs/" + croissant.Substring("Assets/".Length).ToUpperInvariant().Replace('/', '\\');
        Assert.Equal(best, AssetStore.Pick(bread, new[] { moved, best }, out int r0));
        Assert.Equal(0, r0);
        Assert.Equal(moved, AssetStore.Pick(bread, new[] { moved }, out int r1));
        Assert.Equal(1, r1);
        // Un nom court ailleurs n'est jamais pris (« Wall.prefab » d'un autre pack, « Bread1 » hors de RPG Food Props).
        Wave5Entry wall = AssetStore.EntryOrNull(Wave5Role.CastleWall)!;
        Assert.Null(AssetStore.Pick(wall, new[] { "Assets/Viking Village/Prefabs/Wall.prefab" }, out int r2));
        Assert.Equal(-1, r2);
        Wave5Entry food = AssetStore.EntryOrNull(Wave5Role.StoreFood)!;
        Assert.Empty(AssetStore.Found(food, new[] { "Assets/Style/Bread1.prefab", "Assets/RPG FOOD PROPS/HD RENDER PIPELINE/Prefabs/Bread1.prefab" }));
        Assert.Null(AssetStore.Pick(food, null!, out int _));
        // Plusieurs fichiers : dans l'ordre de la liste, chacun une fois.
        IReadOnlyList<string> found = AssetStore.Found(food, food.Files.Reverse().Concat(food.Files));
        Assert.Equal(food.Files, found);
        Assert.True(AssetStore.Variants(Wave5Role.StoreFood) <= food.Files.Count);
        Assert.Equal(AssetStore.Variants(Wave5Role.StoreFood), Wave5.Variants(Wave5Role.StoreFood));
        Assert.Equal(6, Wave5.Variants(Wave5Role.Bread));   // la V2.11 ne bouge pas
    }

    [Fact]
    public void Vérificateur_absent_repli_et_cout_telephone()
    {
        Wave5Entry tent = AssetStore.EntryOrNull(Wave5Role.TraderTent)!;
        Assert.True(tent.Has(Wave5Fix.NotOnPhone));
        Assert.False(Wave5.Shown(tent, phone: true));
        Assert.True(Wave5.Shown(tent, phone: false));
        (EssaiLevel level, string detail, string fix) = AssetStore.Describe(tent, Array.Empty<string>(), false);
        Assert.Equal(EssaiLevel.Warning, level);
        Assert.Contains(tent.Without, detail);
        Assert.Contains("RPG Food Props", fix);
        Assert.Contains("jamais sur téléphone", AssetStore.Describe(tent, tent.Files, false).Detail);
        // Prêt et absent : rien ne gêne aujourd'hui.
        Assert.Equal(EssaiLevel.Ok, AssetStore.Describe(AssetStore.EntryOrNull(Wave5Role.CastleTower)!, null!, false).Level);
        // Bâtiments, château, armures et grands décors : jamais sur téléphone ; objets posés en nombre : partout.
        foreach (Wave5Role r in new[] { Wave5Role.WaterWheel, Wave5Role.AnimalShed, Wave5Role.VillageHouse, Wave5Role.Tavern, Wave5Role.CastleKeep, Wave5Role.ArmoredMan })
            Assert.True(AssetStore.EntryOrNull(r)!.Has(Wave5Fix.NotOnPhone), r.ToString());
        foreach (Wave5Role r in new[] { Wave5Role.StoreBread, Wave5Role.StoreFood, Wave5Role.StoreCrate, Wave5Role.Anvil, Wave5Role.VillageBarrel })
            Assert.False(AssetStore.EntryOrNull(r)!.Has(Wave5Fix.Heavy), r.ToString());
    }

    [Fact]
    public void Choix_de_Damien_gardes_et_anachronismes_ecartes()
    {
        // Décision de Damien du 9 novembre 2026 : maisons, taverne et moulin du pack en décor lointain, ordinateur seulement ; les maisons de
        // Sketchfab et le moulin de Tim0 restent (le bâtiment de forge du pack reste prêt).
        foreach (Wave5Role r in new[] { Wave5Role.VillageHouse, Wave5Role.Tavern, Wave5Role.VillageWindmill })
        {
            Assert.Equal(Wave5Use.Live, AssetStore.EntryOrNull(r)!.Use);
            Assert.True(AssetStore.EntryOrNull(r)!.Has(Wave5Fix.NotOnPhone));
        }
        Assert.Equal(Wave5Use.Ready, AssetStore.EntryOrNull(Wave5Role.Smithy)!.Use);
        // Château et donjon : pas de lieu, rien de posé.
        Assert.All(AssetStore.Entries.Where(e => e.Role.ToString().StartsWith("Castle") || e.Role.ToString().StartsWith("Dungeon")), e => Assert.Equal(Wave5Use.Ready, e.Use));
        // Tomate, orange, pastèque, citrouille : jamais sur les étals ordinaires, seulement chez le marchand venu de loin (décision de Damien).
        Wave5Entry exotic = AssetStore.EntryOrNull(Wave5Role.ExoticFruit)!;
        foreach (string avoided in AssetStore.Avoided)
        {
            Assert.Contains(exotic.Files, f => EssaiManifest.FileName(f).Equals(avoided + ".prefab", StringComparison.Ordinal));
            Assert.DoesNotContain(AssetStore.Entries.Where(e => e != exotic).SelectMany(e => e.Files), f => EssaiManifest.FileName(f).Equals(avoided + ".prefab", StringComparison.OrdinalIgnoreCase));
        }
        Assert.DoesNotContain(AssetStore.Entries.Where(e => e != exotic).SelectMany(e => e.Files),
            f => f.Contains("Tomato") || f.Contains("Orange") || f.Contains("Watermelon") || f.Contains("Pumpkin"));
        Assert.Contains("public static GameObject ExoticStall(", Script("Game", "WorldMarkers.cs"));
        // Les pains de LUMI d'abord dans la vitrine, avant ceux de la V2.11.
        string markers = Script("Game", "WorldMarkers.cs");
        Assert.True(markers.IndexOf("catalog.Looks(Wave5Role.StoreBread)", StringComparison.Ordinal) < markers.IndexOf("catalog.Looks(Wave5Role.Bread)", StringComparison.Ordinal));
    }

    [Fact]
    public void Interieurs_de_l_Asset_Store_en_repli_seulement()
    {
        Assert.Equal(InteriorProp.ClayPot, AssetStore.InteriorKind("Assets/Medieval Props Pack 01/Universal Render Pipeline(URP)/Prefabs/Vase_3.prefab"));
        Assert.Equal(InteriorProp.ClayPot, AssetStore.InteriorKind("Assets/x/Clay Pot.prefab"));
        Assert.Equal(InteriorProp.Goblets, AssetStore.InteriorKind("Assets/x/Clay_Mug.prefab"));
        Assert.Equal(InteriorProp.Barrel, AssetStore.InteriorKind("Assets/x/Barrel 01.prefab"));
        Assert.Equal(InteriorProp.Table, AssetStore.InteriorKind("Assets/x/Old_Table.prefab"));
        Assert.Equal(InteriorProp.Bowl, AssetStore.InteriorKind("Assets/x/Wooden_Bowl_URP.prefab"));
        Wave5Entry interior = AssetStore.Entries.Single(e => e.Use == Wave5Use.Interior);
        Assert.All(interior.Files, f => Assert.NotEqual(InteriorProp.None, AssetStore.InteriorKind(f)));
        // Les sortes déjà là (intérieurs de Damien) ne sont jamais remplacées ; au plus trois par sorte.
        IReadOnlyList<(string Path, InteriorProp Kind)> added = AssetStore.InteriorFallbacks(interior.Files, new[] { InteriorProp.Barrel, InteriorProp.Table });
        Assert.DoesNotContain(added, a => a.Kind == InteriorProp.Barrel || a.Kind == InteriorProp.Table);
        Assert.Contains(added, a => a.Kind == InteriorProp.ClayPot);
        Assert.True(added.GroupBy(a => a.Kind).All(g => g.Count() <= 3));
        Assert.Empty(AssetStore.InteriorFallbacks(null!, null!));
        Assert.Contains("EdsWave5.StoreFurniture(", Script("Editor", "EdsStyle.cs"));
    }

    [Fact]
    public void Sons_medievaux_seulement_en_repli_des_sons_choisis()
    {
        string Rpg(string name) => "Assets/Multiple Solutions/Basic RPG Sounds/Audio/" + name + ".wav";
        Assert.True(AssetStore.IsMedievalSound(Rpg("Sword Hit 01")));
        Assert.True(AssetStore.IsMedievalSound(Rpg("Coin Pickup")));
        Assert.True(AssetStore.IsMedievalSound(Rpg("Wooden Door Open")));
        foreach (string modern in new[] { "Bumper", "D Pad Item", "Pistol Revolver", "Explosion 2", "Laser Shot", "Power Up", "Retro Coin", "Menu Click" })
            Assert.False(AssetStore.IsMedievalSound(Rpg(modern)), modern);
        Assert.False(AssetStore.IsMedievalSound(Rpg("Whatever")));   // aucun mot médiéval : écarté
        // Relecture de la PR #122 : mot à mot, jamais en sous-chaîne.
        Assert.True(AssetStore.IsMedievalSound(Rpg("Wooden Cart Wheel")));    // « cart » n'est pas « car »
        Assert.False(AssetStore.IsMedievalSound(Rpg("Rocket Launch")));       // « rocket » n'est pas « rock »
        Assert.False(AssetStore.IsMedievalSound(Rpg("White Noise")));         // « white » n'est pas « hit »
        Assert.True(AssetStore.IsMedievalSound(Rpg("SwordHit2")));            // majuscules et chiffres coupent les mots
        Assert.True(AssetStore.IsMedievalSound(Rpg("Footsteps Grass")));      // pluriel
        Assert.Contains("dpad", AssetStore.SoundWords(Rpg("D Pad Item")));    // paires de mots voisins
        Assert.True(AssetStore.IsStoreSound(Rpg("Sword Hit 01")));
        Assert.False(AssetStore.IsStoreSound("Assets/Style/Sounds/sword_hit.wav"));
        string[] store = { Rpg("Sword Hit 01"), Rpg("Pistol Hit"), Rpg("Coin Pickup") };
        Assert.Equal(new[] { Rpg("Coin Pickup"), Rpg("Sword Hit 01") }, AssetStore.MedievalSounds(store));
        // Les sons de Damien d'abord ; Basic RPG Sounds seulement quand il n'y en a pas (jamais un mélange).
        string mine = "Assets/Style/Sounds/impactPunch_medium_000.ogg";
        Assert.Equal(mine, AssetStore.PickSound(new[] { mine }, store, StyleSound.Hit));
        Assert.Equal(Rpg("Sword Hit 01"), AssetStore.PickSound(Array.Empty<string>(), store, StyleSound.Hit));
        Assert.Equal(new[] { mine }, AssetStore.PickSounds(new[] { mine }, store, StyleSound.Hit, 6));
        Assert.Empty(AssetStore.PickSounds(Array.Empty<string>(), new[] { Rpg("Pistol Hit") }, StyleSound.Hit, 6));
        Assert.Contains("AssetStore.PickSounds(sounds, storeSounds", Script("Editor", "EdsStyle.cs"));
        Assert.Contains("AssetStore.PickSounds(sounds, storeSounds", Script("Editor", "EdsVerification.cs"));
    }

    [Fact]
    public void Interface_en_bois_pierre_d_abord_avec_repli()
    {
        string folder = "Assets/Fantasy Wooden GUI  Free/normal_ui_set A/";
        string[] gui = { folder + "UI board Small stone.png", folder + "UI board Large stone.png", folder + "UI board Large parchment.png", folder + "TextBTN_Big.png" };
        Assert.True(AssetStore.IsGuiPath(gui[0]));
        Assert.False(AssetStore.IsGuiPath("Assets/Style/Interface/board.png"));
        Assert.Equal(gui[1], AssetStore.Board(gui, AssetStore.GuiBoard.Stone));        // la grande d'abord
        Assert.Equal(gui[2], AssetStore.Board(gui, AssetStore.GuiBoard.Parchment));
        Assert.Null(AssetStore.Board(gui, AssetStore.GuiBoard.Off));
        Assert.Null(AssetStore.Board(new[] { gui[3] }, AssetStore.GuiBoard.Stone));      // sans planche : la dalle du jeu
        Assert.Null(AssetStore.Board(null!, AssetStore.GuiBoard.Stone));
        // Relecture de la PR #122 : seules les grandes fenêtres prennent la planche (bords en proportion de l'image) ; les petits cadres gardent la dalle.
        string kit = Script("Game", "UiKit.cs");
        Assert.Contains("GuiBoard()", kit);
        Assert.Contains("GuiBorderFraction", kit);
        Assert.DoesNotContain("name == \"dalle\"", kit);
        Assert.Contains("UiKit.WindowSprite()", Script("Game", "AdventureBook.cs"));
        Assert.Contains("UiKit.WindowSprite()", Script("Game", "AdventureBook.Forge.cs"));
        string hud = Script("Game", "HudView.cs");
        Assert.Contains("UiKit.Image(_notice, \"Dalle\", UiKit.Sprite(\"dalle\", 40f)", hud);
        Assert.Contains("UiKit.Image(_help, \"Dalle\", UiKit.Sprite(\"dalle\", 40f)", hud);
    }

    [Fact]
    public void Icones_de_CraftPix_sans_fond_d_abord_au_vrai_chemin()
    {
        string root = "Assets/Fantasy RPG Icons Pack/Asset/";
        string[] icons =
        {
            root + "Sword Icons/Background/Sword_01.png", root + "Sword Icons/No Background/Sword_01.png",
            root + "Food Icons/Background/Bread_02.png",   // seulement avec fond : gardée
            root + "Armor Icons/No Background/Armor_07.png",
        };
        Assert.True(icons.All(ItemIcons.IsIcon));
        IReadOnlyList<string> kept = AssetStore.PreferNoBackground(icons);
        Assert.DoesNotContain(icons[0], kept);
        Assert.Contains(icons[1], kept);
        Assert.Contains(icons[2], kept);
        Assert.Equal(icons[1], ItemIcons.Pick("item.epee_de_garde", icons));
        Assert.Equal(icons[2], ItemIcons.Pick("item.miche_du_matin", icons));
        // Une pièce d'armure sans icône à son nom prend la catégorie « Armor Icons ».
        Assert.Equal(icons[3], ItemIcons.Pick("item.casque_de_fer", icons));
        Assert.Empty(ItemIcons.CategoryWords("item.epee_de_garde"));
        Assert.Null(ItemIcons.Pick("item.registre_de_garde", icons));
        Assert.Equal(2, ItemIcons.Map(new[] { "item.epee_de_garde", "item.miche_du_matin", "item.registre_de_garde" }, icons).Count);
        // La préparation et le vérificateur disent combien d'objets sont reliés.
        Assert.Contains("objets reliés sur", Script("Editor", "EdsWave5.cs"));
        Assert.Contains("Objets reliés à une icône de CraftPix", Script("Editor", "EdsVerification.cs"));
    }

    [Fact]
    public void Manifeste_des_licences_chemins_seulement()
    {
        string rows = AssetStore.ManifestRows(p => p == AssetStore.Bakery ? 3 : 0);
        foreach (AssetStorePack p in AssetStore.Packs.Where(p => p != AssetStore.Icons))
        {
            Assert.Contains(p.Title, rows);
            Assert.Contains(p.Folder, rows);
        }
        Assert.Contains("3 fichiers pris", rows);
        Assert.Contains("non trouvé", rows);
        Assert.Contains(AssetStore.License, rows);
        Assert.Contains("AssetStore.ManifestRows(", Script("Editor", "EdsWave5.cs"));
        // Aucun fichier des packs dans le dépôt (seuls unity/Assets/EdS et ses fichiers sont suivis).
        string ignore = Doc(".gitignore");
        Assert.Contains("unity/Assets/*", ignore);
    }

    [Fact]
    public void Preparation_jeu_et_verificateur_lisent_la_liste_partagee()
    {
        string wave5 = Script("Editor", "EdsWave5.cs"), check = Script("Editor", "EdsVerification.cs"), markers = Script("Game", "WorldMarkers.cs"), view = Script("Game", "Wave5View.cs");
        Assert.Contains("AssetStore.Entries", wave5);
        Assert.Contains("AssetStore.Entries", check);
        Assert.Contains("AssetStore.Describe(", check);
        Assert.Contains("look.Offset", view);
        Assert.Contains("Offset = before != null ? before.Offset", wave5);   // réglages faits à la main gardés
        Assert.Contains("public static GameObject MarketStall(", markers);  // prêt pour le marché et les fêtes de la V2.10
        foreach (string script in new[] { wave5, check, markers, view })
        {
            Assert.DoesNotContain("SM_Bread_03", script);   // aucun nom de fichier recopié : la liste est dans EdS.Runtime
            Assert.DoesNotContain("TraderTable.prefab", script);
        }
        // Pas de copie de matériau perdue : les réglages passent par Wave5View.Fix (copies gardées par MaterialCopies).
        Assert.Contains("Fix(holder, look.Model.name, look.Material);", view);
        // Relecture de la PR #122 : les ressources prêtes n'entrent pas au catalogue ; les « jamais sur téléphone » sont retirées de sa construction.
        Assert.Contains("if (entry.Use == Wave5Use.Ready) continue;", wave5);
        string phone = Script("Editor", "EdsPhoneBuild.cs");
        Assert.Contains("Wave5.Shown(e, true)", phone);
        Assert.Contains("OnPostprocessBuild", phone);
        Assert.Contains("V2.12", Doc("docs", "ESSAI_V1.10.md"));
        // Le nombre de ressources branchées annoncé dans l'essai suit la liste.
        Assert.Contains("branchées sur " + AssetStore.Entries.Count(e => e.Use == Wave5Use.Live) + ",", Doc("docs", "ESSAI_V1.10.md"));
        Assert.Contains("V2.12", Doc("docs", "UNITY.md"));
        Assert.Contains("V2.12", Doc("docs", "FEUILLE_DE_ROUTE.md"));
    }

    [Fact]
    public void Vitrine_chaque_pain_et_viennoiserie_poses_avant_de_se_repeter()
    {
        // Relecture de la PR #122 : la galette (3e pain de LUMI) n'est plus sautée ; le croissant de LUMI compte comme viennoiserie.
        Assert.True(AssetStore.IsPastry("SM_Croissant_02a"));
        Assert.False(AssetStore.IsPastry("SM_Flatbread_01a (Clone)"));
        Assert.False(AssetStore.IsPastry("SM_Bread_03"));
        IReadOnlyList<(bool Pastry, int Index)> order = Wave5.ShowcaseOrder(3, 2, 6);
        Assert.Equal(5, order.Count);
        Assert.Equal(new[] { 0, 1, 2 }, order.Where(o => !o.Pastry).Select(o => o.Index));
        Assert.Equal(new[] { 0, 1 }, order.Where(o => o.Pastry).Select(o => o.Index));
        Assert.True(order[2].Pastry);
        for (int b = 0; b <= 8; b++)
            for (int p = 0; p <= 8; p++)
            {
                IReadOnlyList<(bool Pastry, int Index)> o = Wave5.ShowcaseOrder(b, p, 6);
                Assert.Equal(Math.Min(6, b + p), o.Count);
                Assert.Equal(o.Count, o.Distinct().Count());
                Assert.All(o, x => Assert.True(x.Index < (x.Pastry ? p : b)));
            }
        Assert.Empty(Wave5.ShowcaseOrder(-1, -1, 6));
        Assert.Contains("Wave5.ShowcaseOrder(", Script("Game", "WorldMarkers.cs"));
    }

    [Fact]
    public void Decor_lointain_hors_de_l_aire_de_jeu_tourne_vers_elle()
    {
        IReadOnlyList<(double X, double Z, double Yaw)> west = AssetStore.DistantSpots(0, 0, 200, 100, 10, 50, 12, 3, 16);
        Assert.Equal(3, west.Count);
        Assert.All(west, s => Assert.Equal(-12, s.X));
        Assert.All(west, s => Assert.Equal(90, s.Yaw));
        Assert.Equal(new[] { 50.0, 34, 66 }, west.Select(s => s.Z));
        IReadOnlyList<(double X, double Z, double Yaw)> north = AssetStore.DistantSpots(0, 0, 200, 100, 100, 95, 12, 2, 400);
        Assert.All(north, s => Assert.True(s.Z > 100 && s.X >= 0 && s.X <= 200));
        // Jamais dans l'aire, quel que soit le point.
        for (int x = 0; x <= 200; x += 25)
            for (int z = 0; z <= 100; z += 25)
                Assert.All(AssetStore.DistantSpots(0, 0, 200, 100, x, z, 8, 4, 20), s => Assert.True(s.X < 0 || s.X > 200 || s.Z < 0 || s.Z > 100));
        Assert.Empty(AssetStore.DistantSpots(0, 0, 0, 0, 0, 0, 8, 3, 10));
        // La roue à eau passe par une place libre (moulin de Tim0 compris).
        Assert.Contains("Wave5.FreeSpot(new[] { (bx, bz) }", Script("Game", "WorldMarkers.cs"));
    }
}
