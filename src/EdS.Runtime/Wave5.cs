using System;
using System.Collections.Generic;
using System.Linq;

namespace EdS.Runtime
{
    /// <summary>Sous-lot V2.11 : rôle d'une ressource de la 5e vague (ce qu'elle habille dans le jeu, ou ce qu'elle attend).</summary>
    public enum Wave5Role
    {
        // --- Branchées (le jeu les montre dès qu'elles sont déposées) ---
        /// <summary>Croc-Gris (ses deux formes) et le loup blanc : le grand loup animé d'emptypen2477.</summary>
        DireWolf = 0,
        /// <summary>Galeran, dans le souvenir de la Rupture : le chevalier en armure à la lance (ou le roi en armure).</summary>
        Galeran,
        /// <summary>Le heaume de Galeran.</summary>
        Helmet,
        /// <summary>Masse ou fléau dans la main.</summary>
        Mace,
        /// <summary>Hache ou hachette dans la main.</summary>
        Axe,
        /// <summary>Épée dans la main (quand le modèle du héros n'a pas la sienne).</summary>
        Sword,
        /// <summary>Cape portée : la demi-cape, dans le dos.</summary>
        HalfCape,
        /// <summary>Pierres du Seuil : les menhirs aux runes lumineuses.</summary>
        Menhir,
        /// <summary>Cristaux lumineux autour du cœur du Seuil et des failles.</summary>
        Crystals,
        /// <summary>Grand cristal étrange au cœur du Seuil.</summary>
        StrangeCrystal,
        /// <summary>Cadre de portail autour de la déchirure d'une faille ouverte.</summary>
        Portal,
        /// <summary>Feu follet qui flotte au cœur du Seuil.</summary>
        Wisp,
        /// <summary>Four à pain de briques de Firmin.</summary>
        BreadOven,
        /// <summary>Sacs de farine près du four.</summary>
        FlourSack,
        /// <summary>Table du fournil (pains posés dessus).</summary>
        BakeryTable,
        /// <summary>Pains sur la table du fournil.</summary>
        Bread,
        /// <summary>Viennoiseries et gâteaux sur la table du fournil.</summary>
        Pastry,
        /// <summary>Caisse de poires à l'étal de Bertille.</summary>
        PearCrate,
        /// <summary>Épouvantail dans les champs.</summary>
        Scarecrow,
        /// <summary>Bottes de foin aux champs.</summary>
        HayBales,
        /// <summary>Charrette au hameau.</summary>
        Cart,
        /// <summary>Guirlande de fanions sur la place, les jours de fête.</summary>
        Bunting,
        /// <summary>Bannière suspendue sur la place, les jours de fête.</summary>
        Banner,
        /// <summary>Le corbeau animé (case <see cref="StyleSlot.Crow"/> : vérifié avec les bêtes, hors de <see cref="Wave5.Entries"/>).</summary>
        Crow,

        // --- Prêtes (catalogue fait, lieu ou créature pas encore dans le jeu) ---
        Spider,
        Rat,
        Bat,
        SkeletonWarrior,
        Skeleton,
        Lich,
        Ghost,
        Troll,
        Ogre,
        Golem,
        Werewolf,
        Witch,
        Priest,
        Worker,
        Crossbow,
        Dagger,
        Gambeson,
        StoneBridge,
        RuinedTowers,
        Fountain,
        StreetLantern,
        MarketStalls,
        Mushrooms,
        LongBanner,
        Coffin,
        Bones,
        Skulls,
        Altar,
        Tombstones,
        WallTorch,
        CastleKit,
        DungeonKit,
        DungeonProps,
        SpiderWeb,
        IronGate,
        Fort,
        Cheese,
        RollingPin,

        // --- Intérieurs (pris par les règles des intérieurs : InteriorRules) ---
        Cauldron,
        Chest,
        Tankard,
    }

    /// <summary>Sous-lot V2.11 : où en est une ressource dans le jeu.</summary>
    public enum Wave5Use
    {
        /// <summary>Branchée : montrée dès qu'elle est déposée (repli propre sinon).</summary>
        Live = 0,
        /// <summary>Prête : choisie par la préparation, montrée quand le lieu ou la créature existera.</summary>
        Ready = 1,
        /// <summary>Prise par les intérieurs (<see cref="InteriorRules"/>), où ils existent déjà.</summary>
        Interior = 2,
    }

    /// <summary>Sous-lot V2.11 : réglages notés dans CONVERSION_V5.md, appliqués par le jeu sur ses copies des matériaux.</summary>
    [Flags]
    public enum Wave5Fix
    {
        None = 0,
        /// <summary>Transparence découpée (plumes du corbeau, brins du foin, toile) : « Alpha Clipping ».</summary>
        AlphaClip = 1,
        /// <summary>Lueur : émission allumée (texture d'émission du FBX, sinon sa couleur), pour le Bloom d'URP.</summary>
        Emissive = 2,
        /// <summary>Livré sans texture : teinté d'une couleur du jeu (<see cref="Wave5Entry.Tint"/>).</summary>
        Untextured = 4,
        /// <summary>Flotte (posé sur le point le plus bas de son clip) : pas recollé au sol.</summary>
        Floats = 8,
        /// <summary>Plus de 20 000 triangles : lourd pour le Note8.</summary>
        Heavy = 16,
        /// <summary>Pas montré sur téléphone (repli du jeu à la place).</summary>
        NotOnPhone = 32,
        /// <summary>Une seule longue prise, découpée en clips par la préparation (<see cref="Wave5.SkeletonCuts"/>).</summary>
        CutClips = 64,
        /// <summary>Animé : animations « Legacy », jouées par le jeu.</summary>
        Animated = 128,
        /// <summary>Scan photo (Quixel Megascans) : matériau URP Lit fait par la préparation depuis ses textures (couleur, relief, rugosité, occlusion).</summary>
        ScannedMaterial = 256,
    }

    /// <summary>Sous-lot V2.11 : crédit d'une ressource (CC BY : titre, auteur, lien et licence obligatoires).</summary>
    public sealed class Wave5Credit
    {
        public Wave5Credit(string title, string author, string url, string license)
        {
            Title = title;
            Author = author;
            Url = url;
            License = license;
        }

        public string Title { get; }
        public string Author { get; }
        public string Url { get; }
        public string License { get; }

        /// <summary>Licence à attribution (CC BY, OGA-BY) : crédit obligatoire.</summary>
        public bool Attribution => License.Contains("BY");
    }

    /// <summary>Sous-lot V2.11 : une ressource de la 5e vague, ses fichiers du meilleur au dernier repli.</summary>
    public sealed class Wave5Entry
    {
        public Wave5Entry(Wave5Role role, string label, Wave5Use use, string without, Wave5Fix fix, double height, (double R, double G, double B) tint,
            IReadOnlyList<string> files, IReadOnlyList<Wave5Credit> credits)
        {
            if (files is null || files.Count == 0) throw new ArgumentException("Au moins un fichier attendu.", nameof(files));
            Role = role;
            Label = label;
            Use = use;
            Without = without;
            Fix = fix;
            Height = height;
            Tint = tint;
            Files = files;
            Credits = credits;
        }

        public Wave5Role Role { get; }

        /// <summary>Nom affiché et rôle (« Croc-Gris et le loup blanc »).</summary>
        public string Label { get; }

        public Wave5Use Use { get; }

        /// <summary>Ce que le jeu montre sans le fichier.</summary>
        public string Without { get; }

        public Wave5Fix Fix { get; }

        /// <summary>Taille voulue (m) : hauteur, ou longueur pour une arme ; 0 : sa taille réelle (convertie en mètres).</summary>
        public double Height { get; }

        /// <summary>Teinte d'un modèle sans texture (<see cref="Wave5Fix.Untextured"/>).</summary>
        public (double R, double G, double B) Tint { get; }

        /// <summary>Chemins attendus dans <c>Assets/Style</c>, le meilleur d'abord.</summary>
        public IReadOnlyList<string> Files { get; }

        /// <summary>Crédit de chaque fichier (même rang).</summary>
        public IReadOnlyList<Wave5Credit> Credits { get; }

        public bool Has(Wave5Fix fix) => (Fix & fix) == fix;
    }

    /// <summary>
    /// Sous-lot V2.11 (demande de Damien du 8 novembre 2026 : « tout brancher avant le premier essai dans Unity ») : la 5e vague de ressources
    /// déposée dans <c>Assets/Style</c> (68 modèles de Sketchfab convertis en FBX, objets de Poly Haven, sons et musiques). Liste partagée par la
    /// préparation (<c>EdsWave5</c>), le jeu et le vérificateur d'essai : chaque rôle, ses fichiers du meilleur au repli, ses réglages et ses
    /// crédits. Un fichier est reconnu par son nom, dans n'importe quel dossier (comme <see cref="EssaiManifest.Pick"/>) ; sans lui, le jeu
    /// garde ce qu'il montrait (formes simples, ancien modèle) : jamais d'erreur ni d'objet invisible. Affichage seulement.
    /// </summary>
    public static class Wave5
    {
        public const string Folder = EssaiManifest.Root + "/Sketchfab/";
        private const string Ph = EssaiManifest.Root + "/PolyHaven/";
        private const string ByLicense = "CC BY 4.0";
        private const string Cc0 = "CC0";

        private static Wave5Credit Sf(string title, string author, string id) => new Wave5Credit(title, author, "https://sketchfab.com/3d-models/" + id, ByLicense);
        private static Wave5Credit Poly(string title, string id) => new Wave5Credit(title, "Poly Haven", "https://polyhaven.com/a/" + id, Cc0);

        private static readonly (double, double, double) NoTint = (1, 1, 1);

        private static Wave5Entry E(Wave5Role role, string label, Wave5Use use, string without, Wave5Fix fix, double height, params (string File, Wave5Credit Credit)[] files) =>
            E(role, label, use, without, fix, height, NoTint, files);

        private static Wave5Entry E(Wave5Role role, string label, Wave5Use use, string without, Wave5Fix fix, double height, (double, double, double) tint,
            params (string File, Wave5Credit Credit)[] files) =>
            new Wave5Entry(role, label, use, without, fix, height, tint, files.Select(f => f.File).ToList(), files.Select(f => f.Credit).ToList());

        private static (string, Wave5Credit) PolyFile(string folder, string id, string title) => (Ph + folder + "/" + id + "/" + id + "_1k.fbx", Poly(title, id));

        /// <summary>Dossier des pains de Quixel Megascans (Fab, licence standard Fab : hors dépôt, notée au manifeste des licences).</summary>
        public const string MegascansFolder = EssaiManifest.Root + "/Fab/Megascans_Pains/";

        /// <summary>Licence des ressources de Fab (Quixel Megascans).</summary>
        public const string FabLicense = "licence standard Fab";

        private static (string, Wave5Credit) Mega(string folder, string stem, string listing, string title) =>
            (MegascansFolder + folder + "/" + stem + "_Mid.fbx", new Wave5Credit(title, "Quixel Megascans", "https://www.fab.com/listings/" + listing, FabLicense));

        private const Wave5Use Live = Wave5Use.Live;
        private const Wave5Use Ready = Wave5Use.Ready;
        private const string Later = "rien (prêt pour plus tard)";

        /// <summary>Toutes les ressources de la 5e vague, branchées d'abord.</summary>
        public static readonly IReadOnlyList<Wave5Entry> Entries = new[]
        {
            // 1. Ce qui était en formes simples ou en repli.
            E(Wave5Role.DireWolf, "Croc-Gris et le loup blanc (grand loup animé)", Live, "le loup des autres meutes, plus grand", Wave5Fix.Animated | Wave5Fix.Heavy, 0,
                (Folder + "Creatures/eds_SF_Animated_Wolf_emptypen2477.fbx", Sf("Animated Wolf", "emptypen2477", "0d82257ee1454f87a05458980e43f1b2"))),
            E(Wave5Role.Galeran, "Galeran en armure (souvenir de la Rupture)", Live, "lance, épaulières et plastron en formes simples", Wave5Fix.Animated | Wave5Fix.Heavy, 1.85,
                (Folder + "Personnages/eds_SF_Armored_Guard_Knight_Rig_SuperKapoo913.fbx", Sf("Armored Guard Knight Rig", "SuperKapoo913", "b7bba7eddb13470b88eb8b24531cb6b3")),
                (Folder + "Personnages/eds_SF_Armored_King_Calviking073.fbx", Sf("Armored King", "Calviking073", "e3978fb660cf47278485d44eeba488ef"))),
            E(Wave5Role.Helmet, "Heaume de Galeran", Live, "tête nue", Wave5Fix.None, 0.36,
                (Folder + "Armes/eds_SF_Medieval_helmet_bingiz.fbx", Sf("Medieval helmet", "bingiz", "b2f9c198e30043f09170fa4eef12733a"))),
            E(Wave5Role.Mace, "Masse et fléau en main", Live, "masse en formes simples", Wave5Fix.None, 0.75,
                PolyFile("Armes", "ornate_medieval_mace", "Ornate Medieval Mace")),
            E(Wave5Role.Axe, "Hache et hachette en main", Live, "hache en formes simples", Wave5Fix.None, 0.7,
                PolyFile("Armes", "wooden_axe_02", "Wooden Axe 02"), PolyFile("Armes", "wooden_axe_03", "Wooden Axe 03")),
            E(Wave5Role.Sword, "Épée en main (corps sans épée)", Live, "épée en formes simples", Wave5Fix.None, 1.0,
                (Folder + "Armes/eds_SF_medieval_sword_lowseb.fbx", Sf("medieval sword", "lowseb", "da574cba504e4b83a3293f3d3bd067fb")),
                PolyFile("Armes", "wooden_handle_saber", "Wooden Handle Saber")),
            E(Wave5Role.HalfCape, "Cape dans le dos (demi-cape)", Live, "cape en forme simple (et toujours sur téléphone)", Wave5Fix.Heavy | Wave5Fix.NotOnPhone, 0.6,
                (Folder + "Armes/eds_SF_Halfcape_Soft_madsstensrud.fbx", Sf("Halfcape Soft", "madsstensrud", "c2a7e27288aa4c46b4ab910696eea112"))),

            // 2. Le Seuil et les failles.
            E(Wave5Role.Menhir, "Pierres du Seuil (menhirs aux runes)", Live, "rochers des packs", Wave5Fix.Emissive, 2.5,
                (Folder + "Village/eds_SF_Menhirs_DragonJojo.fbx", Sf("Menhirs", "DragonJojo", "12340f47ccd74aa6a87e25ad692fab10"))),
            E(Wave5Role.Crystals, "Cristaux lumineux (cœur du Seuil, failles)", Live, "pas de cristaux", Wave5Fix.Emissive, 1.2,
                (Folder + "Seuil/eds_SF_Glowing_Crystals_ked182.fbx", Sf("Glowing Crystals", "ked182", "cb97c1da9c284f77b1fd0842bd0b26ed"))),
            E(Wave5Role.StrangeCrystal, "Grand cristal du cœur du Seuil", Live, "pas de grand cristal", Wave5Fix.Emissive, 1.5,
                (Folder + "Seuil/eds_SF_Strange_Crystal_SebastianSosnowski.fbx", Sf("Strange Crystal", "SebastianSosnowski", "88da8269bc5a4c76ab420585eb9d622e"))),
            E(Wave5Role.Portal, "Portail autour des failles ouvertes", Live, "la déchirure seule", Wave5Fix.Emissive | Wave5Fix.AlphaClip, 3.4,
                (Folder + "Seuil/eds_SF_Magic_Portal_nickbroad.fbx", Sf("Magic Portal", "nickbroad", "dc447f04768a4bfe8932d976bd2b1677"))),
            E(Wave5Role.Wisp, "Feu follet du cœur du Seuil", Live, "pas de feu follet", Wave5Fix.Untextured | Wave5Fix.Emissive | Wave5Fix.Floats | Wave5Fix.Animated, 0.6,
                (0.55, 0.75, 1.0), (Folder + "Seuil/eds_SF_Wisp_soul_seyXR.fbx", Sf("Wisp soul", "seyXR", "f4fc937e7e1f4afeb59e2b0ac1c41855"))),

            // 3. Boulangerie, étals, champs, fêtes.
            E(Wave5Role.BreadOven, "Four à pain de Firmin", Live, "pas de four (Firmin devant sa maison)", Wave5Fix.None, 0,
                (Folder + "Boulangerie/eds_SF_Brick_Bread_Oven_torricane.fbx", Sf("Brick Bread Oven", "torricane", "f65f1a343b5347caaeb204d2c023667f"))),
            E(Wave5Role.FlourSack, "Sacs de farine du fournil", Live, "pas de sacs", Wave5Fix.Untextured, 0, (0.86, 0.8, 0.66),
                (Folder + "Boulangerie/eds_SF_Sack_of_Flour_Low_Poly_gokhan_bayhan44.fbx", Sf("Sack of Flour Low Poly", "gokhan.bayhan44", "8122a0e6a8ad4971a8ce237dbfda5642")),
                (Folder + "Boulangerie/eds_SF_Flour_sack_rozalia_skvarkova.fbx", Sf("Flour sack", "rozalia.skvarkova", "1080516fda8a4b91a5947c2b16948cb9"))),
            E(Wave5Role.BakeryTable, "Table du fournil", Live, "pas de table (pains posés sur un sac)", Wave5Fix.None, 0,
                PolyFile("Objets", "wooden_picnic_table", "Wooden Picnic Table")),
            // Demande de Damien du 8 novembre 2026 (23 h 45) : les neuf pains scannés de Quixel Megascans (Fab, licence standard Fab, hors
            // dépôt) passent devant ceux de Sketchfab ; matériau URP fait par la préparation depuis leurs textures (Wave5Fix.ScannedMaterial).
            E(Wave5Role.Bread, "Pains du fournil et des étals", Live, "pas de pains", Wave5Fix.ScannedMaterial, 0,
                Mega("baguette_bread_ujqhebs", "Baguette_Bread_ujqhebs", "e555b990-d7ca-49db-bdde-653ce637854e", "Baguette Bread"),
                Mega("sourdough_bread_whpmaba", "Sourdough_Bread_whpmaba", "f56664e8-778f-4080-9914-68cd0b8d25e4", "Sourdough Bread"),
                Mega("round_rye_bread_whplfip", "Round_Rye_Bread_whplfip", "f6e4caa0-61be-47ad-944a-bf0f68c52ad5", "Round Rye Bread"),
                Mega("nutty_bread_thmhfjkpa", "Nutty_Bread_thmhfjkpa", "bffa92bd-e214-4a4f-94cf-2fa2e4337e7f", "Nutty Bread"),
                Mega("half_walnut_bread_tf3nehypa", "Half_Walnut_Bread_tf3nehypa", "49705e8b-86f4-4db5-8bdb-af0c227336a9", "Half Walnut Bread"),
                Mega("bread_roll_ti0lbipqx", "Bread_Roll_ti0lbipqx", "00e7e55b-47db-4359-9a52-bc75e835660f", "Bread Roll"),
                Mega("bread_roll_tiwkagvva", "Bread_Roll_tiwkagvva", "10c6a427-55a1-4ec2-ba45-2c90bf0c1255", "Bread Roll"),
                Mega("sliced_brown_bread_teewecnla", "Sliced_Brown_Bread_teewecnla", "4f6b8aaf-4279-4815-90a2-9a5c9dcfa4f5", "Sliced Brown Bread"),
                Mega("slice_of_bread_tfxndfdpa", "Slice_of_Bread_tfxndfdpa", "2ae80ba8-3cc9-4eb3-affb-e286dad9d269", "Slice of Bread"),
                (Folder + "Boulangerie/eds_SF_Medieval_Bread_Loaf_FBX_GetDeadEntertainment.fbx", Sf("Medieval_Bread_Loaf_FBX", "GetDeadEntertainment", "6c974828b9f4427bb4460287041c7f5f")),
                (Folder + "Boulangerie/eds_SF_Game_Ready_Free_Bread_Loaf_Igrodev.fbx", Sf("(Game Ready Free) Bread Loaf", "Igrodev", "daf4801f72ae4f9eba9c338afbbb2926")),
                (Folder + "Boulangerie/eds_SF_French_baguette_encoded01.fbx", Sf("French baguette", "encoded01", "b6c483995db04698bf5041c9cef369c0")),
                (Folder + "Boulangerie/eds_SF_Sliced_B_Read_FBX_GetDeadEntertainment.fbx", Sf("Sliced B Read FBX", "GetDeadEntertainment", "86516676fcac4e4e85d397b6aea561cc"))),
            E(Wave5Role.Pastry, "Croissants, gâteaux et tourte du fournil", Live, "pas de viennoiseries", Wave5Fix.None, 0,
                PolyFile("Nourriture", "croissant", "Croissant"), PolyFile("Nourriture", "carrot_cake", "Carrot Cake"),
                PolyFile("Nourriture", "strawberry_chocolate_cake", "Strawberry Chocolate Cake"),
                (Folder + "Boulangerie/eds_SF_Pie_rudolfs.fbx", Sf("Pie", "rudolfs", "951f96e462b14ea79cefb31602a3a3fd"))),
            E(Wave5Role.PearCrate, "Caisse de poires de Bertille", Live, "étal sans fruits", Wave5Fix.None, 0,
                (Folder + "Village/eds_SF_Crate_of_Pears_Game_Ready_4K_P_meerschaumdigital.fbx", Sf("Crate of Pears (Game Ready / 4K PBR)", "meerschaumdigital", "f4c3ab39ce8b429082a6f0f598e6b2cf"))),
            E(Wave5Role.Scarecrow, "Épouvantail des champs", Live, "pas d'épouvantail", Wave5Fix.None, 1.9,
                (Folder + "Village/eds_SF_Scarecrow_baardus.fbx", Sf("Scarecrow", "baardus", "dd66376c24f246f28b10a67f047b8d6a"))),
            E(Wave5Role.HayBales, "Bottes de foin des champs", Live, "pas de foin", Wave5Fix.AlphaClip, 0,
                (Folder + "Village/eds_SF_Hay_bales_zbrojmistrz.fbx", Sf("Hay bales", "zbrojmistrz", "d6e087f9a2a9416c94918f0503943c17"))),
            E(Wave5Role.Cart, "Charrette du hameau", Live, "pas de charrette", Wave5Fix.None, 0,
                (Folder + "Village/eds_SF_Medieval_Cart_Peter_Nox.fbx", Sf("Medieval Cart", "Peter.Nox", "5dedd347d77943729475486f0d1d05e8"))),
            E(Wave5Role.Bunting, "Guirlande de fête sur la place", Live, "pas de guirlande (et jamais sur téléphone)", Wave5Fix.Untextured | Wave5Fix.Heavy | Wave5Fix.NotOnPhone, 0,
                (0.85, 0.3, 0.22), (Folder + "Fete/eds_SF_Colorful_Festival_Bunting_Bann_nadiya69.fbx", Sf("Colorful Festival Bunting Banner", "nadiya69", "f8f0f6dcad85423bbbf2249bdd18bad7"))),
            E(Wave5Role.Banner, "Bannières de fête sur la place", Live, "pas de bannière", Wave5Fix.Untextured, 0, (0.55, 0.12, 0.12),
                (Folder + "Fete/eds_SF_Hanging_Banner_awenram.fbx", Sf("Hanging Banner", "awenram", "74d14ebdbbc94d8780f03df21cd93674"))),

            // Prêtes : créatures (aucune dans les Vallons aujourd'hui), personnages, armes, décor sans lieu.
            E(Wave5Role.Spider, "Araignée géante", Ready, Later, Wave5Fix.Animated | Wave5Fix.Heavy, 0,
                (Folder + "Creatures/eds_SF_Spider_animated_character_TheGameAssets.fbx", Sf("Spider animated character", "TheGameAssets", "746f5c5173ce4f47ad0970b62ee4106e")),
                (Folder + "Creatures/eds_SF_Animated_Spider_shadocks.fbx", Sf("Animated Spider", "shadocks", "af87017501fc44e39a33c220f2435100"))),
            E(Wave5Role.Rat, "Rat", Ready, Later, Wave5Fix.Animated | Wave5Fix.Heavy, 0,
                (Folder + "Creatures/eds_SF_Black_Rat_Free_download_Nestaeric.fbx", Sf("Black Rat ( Free download )", "Nestaeric", "3db3acb4140d4de8bd62a171212bad9c")),
                PolyFile("Donjon", "street_rat", "Street Rat")),
            E(Wave5Role.Bat, "Chauve-souris", Ready, Later, Wave5Fix.Animated | Wave5Fix.Floats, 0,
                (Folder + "Creatures/eds_SF_Bat_Attack_LostModels2025.fbx", Sf("Bat Attack", "LostModels2025", "c6abf0e73f904f959522f7d9da6891a7"))),
            E(Wave5Role.SkeletonWarrior, "Squelette guerrier", Ready, Later, Wave5Fix.Animated, 0,
                (Folder + "Creatures/eds_SF_Skeleton_Warrior_Animated_Low__Arsyuta777.fbx", Sf("Skeleton Warrior - Animated Low-Poly Enemy", "Arsyuta777", "dfeb342871204ecba0ae25f48c487738"))),
            E(Wave5Role.Skeleton, "Squelette (prise longue découpée)", Ready, Later, Wave5Fix.Animated | Wave5Fix.CutClips, 0,
                (Folder + "Creatures/eds_SF_Skeleton_Animated_one_boy.fbx", Sf("Skeleton (Animated)", "one_boy", "bd1de4a8153a44fe93e728ca8e06701b"))),
            E(Wave5Role.Lich, "Liche", Ready, Later, Wave5Fix.Animated | Wave5Fix.Floats, 0,
                (Folder + "Creatures/eds_SF_Lich_Spinnee.fbx", Sf("Lich", "Spinnee", "4ad4ece50273473ba1d843797b8f4fa6"))),
            E(Wave5Role.Ghost, "Fantôme (base pour le shader EdS/Echo)", Ready, Later, Wave5Fix.Untextured, 0, (0.75, 0.85, 1.0),
                (Folder + "Creatures/eds_SF_Ghost_csbr.fbx", Sf("Ghost", "csbr", "8fc07a30ce9f45d5a10ef229f2ece730"))),
            E(Wave5Role.Troll, "Troll", Ready, Later, Wave5Fix.Animated, 0,
                (Folder + "Creatures/eds_SF_Troll_sjunior3d.fbx", Sf("Troll", "sjunior3d", "bceb1cb47186457c8571a23e6aff1b8e"))),
            E(Wave5Role.Ogre, "Ogre", Ready, Later, Wave5Fix.Animated, 0,
                (Folder + "Creatures/eds_SF_Ogre_Character_snackwacko.fbx", Sf("Ogre Character", "snackwacko", "676c3e1fb1154768b4f99410c38ea00f"))),
            E(Wave5Role.Golem, "Golem de pierre", Ready, Later, Wave5Fix.Animated, 0,
                (Folder + "Creatures/eds_SF_Drugdo_r_The_Golem_Animated_Phons.fbx", Sf("Drugdör The Golem Animated", "Phons", "19c1855bdb2c4cdc89da2cfb64da48cf"))),
            E(Wave5Role.Werewolf, "Loup-garou", Ready, Later, Wave5Fix.Animated, 0,
                (Folder + "Creatures/eds_SF_Werewolf_Stylized_Animated_imitate.fbx", Sf("Werewolf Stylized Animated", "imitate", "16cefdf83e5e457eb6e53f1fbd97edb5"))),
            E(Wave5Role.Witch, "Sorcière", Ready, Later, Wave5Fix.Animated, 0,
                (Folder + "Creatures/eds_SF_Witch_MAXDESIGN.fbx", Sf("Witch", "MAXDESIGN", "2a7672475d514543b618b5a82fd5582b"))),
            E(Wave5Role.Priest, "Prêtre (repli)", Ready, "le corps humain du prêtre", Wave5Fix.Animated, 0,
                (Folder + "Personnages/eds_SF_Low_Poly_Priest_Rigged_Yanez_Designs.fbx", Sf("Low Poly Priest (Rigged)", "Yanez-Designs", "a8900861df984faebabfb0e0d5ff59eb"))),
            E(Wave5Role.Worker, "Ouvrière", Ready, "les corps humains", Wave5Fix.Animated, 0,
                (Folder + "Personnages/eds_SF_Girl_with_clothes_Worker_set_cattleya.fbx", Sf("Girl with clothes. Worker set", "cattleya", "4cf10e2dde6d4b12b6dccd25a4cf3b77"))),
            E(Wave5Role.Crossbow, "Arbalète", Ready, Later, Wave5Fix.None, 0,
                (Folder + "Armes/eds_SF_Medieval_Crossbow_Game_Model_makivic.fbx", Sf("Medieval Crossbow (Game Model)", "makivic", "697aa7fee7894ad2aa0e87ebc1b9d631"))),
            E(Wave5Role.Dagger, "Dague", Ready, Later, Wave5Fix.None, 0, PolyFile("Armes", "ornate_medieval_dagger", "Ornate Medieval Dagger")),
            E(Wave5Role.Gambeson, "Gambison (sur mannequin)", Ready, Later, Wave5Fix.Heavy | Wave5Fix.NotOnPhone, 0,
                (Folder + "Armes/eds_SF_Gambeson_Leon_Steiner.fbx", Sf("Gambeson", "Leon.Steiner", "0fc44d2876ed449ca9473e78c83a8d39"))),
            E(Wave5Role.StoneBridge, "Pont de pierre (le pont cintré de bois reste : choix de Damien)", Ready, "le pont cintré fait par le code", Wave5Fix.Heavy, 0,
                (Folder + "Village/eds_SF_Medieval_Bridge_JooL.fbx", Sf("Medieval Bridge", "_JooL_", "e536f8aa3a15473bb9eaffeb53ae31e9"))),
            E(Wave5Role.RuinedTowers, "Tours en ruine (73 k triangles : jamais sur téléphone)", Ready, Later, Wave5Fix.Heavy | Wave5Fix.NotOnPhone, 0,
                (Folder + "Village/eds_SF_Pack_of_old_towers_in_ruins_taz83.fbx", Sf("Pack of old towers in ruins", "taz83", "f213359c6cbb4c29bf8880764faa0fb8"))),
            E(Wave5Role.Fountain, "Fontaine", Ready, Later, Wave5Fix.None, 0,
                (Folder + "Village/eds_SF_Medieval_Fountain_Empty_Free_wolfgar74.fbx", Sf("Medieval Fountain Empty (Free)", "wolfgar74", "952a9c257db641229902c36abb97e8bc"))),
            E(Wave5Role.StreetLantern, "Lanterne de rue", Ready, Later, Wave5Fix.None, 0,
                (Folder + "Village/eds_SF_Medieval_Street_lantern_Alexey_inc.fbx", Sf("Medieval Street lantern", "Alexey.inc", "1a38842082494b0cb62f74e630a00043"))),
            E(Wave5Role.MarketStalls, "Étals du marché (88 textures, 51 Mo)", Ready, Later, Wave5Fix.Heavy | Wave5Fix.NotOnPhone, 0,
                (Folder + "Village/eds_SF_Medieval_Market_Asset_Pack_vmatthew.fbx", Sf("Medieval Market Asset Pack", "vmatthew", "006ffc4ac5f34a1782f567b07e6605f2"))),
            E(Wave5Role.Mushrooms, "Champignons", Ready, Later, Wave5Fix.None, 0,
                (Folder + "Village/eds_SF_Mushrooms_leha_pu.fbx", Sf("Mushrooms", "leha_pu", "f9dbd4c34a4f4389bf3901527ecc89a0"))),
            E(Wave5Role.LongBanner, "Longue bannière", Ready, Later, Wave5Fix.Untextured, 0, (0.55, 0.12, 0.12),
                (Folder + "Village/eds_SF_Long_Banner_awenram.fbx", Sf("Long Banner", "awenram", "1b9956e1c98742a5bab251d81ac364ff"))),
            E(Wave5Role.Coffin, "Cercueils (crypte)", Ready, Later, Wave5Fix.None, 0,
                (Folder + "Donjon/eds_SF_Wooden_Coffins_Free_Shaddy.fbx", Sf("Wooden Coffins (Free)", "Shaddy", "23f49c5ab417455a9d8fca828919354e")),
                (Folder + "Donjon/eds_SF_Broken_Coffin_DivyeSh_PanchAl.fbx", Sf("Broken Coffin", "DivyeSh.PanchAl", "715fd5c8503b4a0e8106168535c186ff"))),
            E(Wave5Role.Bones, "Tas d'os (crypte, antre)", Ready, Later, Wave5Fix.None, 0,
                (Folder + "Donjon/eds_SF_Low_Poly_Bone_Pile_Downloadabl_3dsauce.fbx", Sf("Low Poly Bone Pile (Downloadable)", "3dsauce", "5ec580b41a934cea86d4297980d1378f"))),
            E(Wave5Role.Skulls, "Tas de crânes (crypte)", Ready, Later, Wave5Fix.None, 0,
                (Folder + "Donjon/eds_SF_Pile_of_Skulls_abimaelgonzalez.fbx", Sf("Pile of Skulls", "abimaelgonzalez", "11d46d32494c44218a55192adc067e57"))),
            E(Wave5Role.Altar, "Autel de pierre (crypte)", Ready, Later, Wave5Fix.None, 0,
                (Folder + "Donjon/eds_SF_Stone_Altar_TheoClarke.fbx", Sf("Stone Altar", "TheoClarke", "3d4f2edb18e4424cabb41b60aa160cb5"))),
            E(Wave5Role.Tombstones, "Stèles (cimetière)", Ready, Later, Wave5Fix.None, 0,
                (Folder + "Donjon/eds_SF_Tombstones_drobluda.fbx", Sf("Tombstones", "drobluda", "047c28dfd28f4f9eb630bfd42dfeb910"))),
            E(Wave5Role.WallTorch, "Torches murales (crypte)", Ready, Later, Wave5Fix.None, 0,
                (Folder + "Donjon/eds_SF_Medieval_Wall_Torch_Kigha.fbx", Sf("Medieval Wall Torch", "Kigha", "77db436da2844cbfb4dde0bb9b396835")),
                (Folder + "Donjon/eds_SF_Torch_bumstrum.fbx", Sf("Torch", "bumstrum", "238cd6056e2940debb4f67fc24c6df35"))),
            E(Wave5Role.CastleKit, "Kit d'intérieur de château", Ready, Later, Wave5Fix.None, 0,
                (Folder + "Donjon/eds_SF_Medieval_castle_modular_kits_i_pedramashoori.fbx", Sf("Medieval castle modular kits (interior)", "pedramashoori", "bde29c804d6247c59d29a2ade11e69bc"))),
            E(Wave5Role.DungeonKit, "Donjon modulaire (33 k triangles)", Ready, Later, Wave5Fix.Heavy, 0,
                (Folder + "Donjon/eds_SF_Modular_Dungeon_bumstrum.fbx", Sf("Modular Dungeon", "bumstrum", "c4d0cbde62b748caa58e40699477f8cd"))),
            E(Wave5Role.DungeonProps, "Objets de donjon", Ready, Later, Wave5Fix.None, 0,
                (Folder + "Donjon/eds_SF_Modular_Dungeon_Props_just_polygons.fbx", Sf("Modular Dungeon Props", "just_polygons", "62ccabc4e78b4822ac8c5af74fe9c66a"))),
            E(Wave5Role.SpiderWeb, "Toiles d'araignée", Ready, Later, Wave5Fix.Untextured | Wave5Fix.AlphaClip, 0, (0.9, 0.9, 0.88),
                (Folder + "Donjon/eds_SF_SPIDER_WEB_sammephi.fbx", Sf("SPIDER WEB", "sammephi", "cc5d3b794341445a90bc6f6e9422792d"))),
            E(Wave5Role.IronGate, "Grille de fer", Ready, Later, Wave5Fix.None, 0, PolyFile("Donjon", "large_iron_gate", "Large Iron Gate")),
            E(Wave5Role.Fort, "Fort modulaire", Ready, Later, Wave5Fix.None, 0, PolyFile("Donjon", "modular_fort_01", "Modular Fort 01")),
            E(Wave5Role.Cheese, "Fromages (étal, auberge)", Ready, Later, Wave5Fix.None, 0,
                (Folder + "Boulangerie/eds_SF_Aged_Cheese_FBX_GetDeadEntertainment.fbx", Sf("Aged Cheese FBX", "GetDeadEntertainment", "7fe307bc3142404e8b2666f9228d83c5")),
                (Folder + "Boulangerie/eds_SF_Half_Cheese_Wheel_meerschaumdigital.fbx", Sf("Half Cheese Wheel", "meerschaumdigital", "53eb788dee544f8c926638a27d7bea5a"))),
            E(Wave5Role.RollingPin, "Rouleau à pâtisserie", Ready, Later, Wave5Fix.None, 0,
                (Folder + "Boulangerie/eds_SF_Wooden_Rolling_Pin_GameDevNick.fbx", Sf("Wooden Rolling Pin", "GameDevNick", "e5d077535f1241d39121d5c87c65bfaa"))),

            // Intérieurs (auberge, maisons, chapelle : pris par InteriorRules).
            E(Wave5Role.Cauldron, "Chaudron (intérieurs)", Wave5Use.Interior, "un pot en forme simple", Wave5Fix.None, 0,
                (Folder + "Interieurs/eds_SF_Cauldron_DarrenMcnerney3D.fbx", Sf("Cauldron", "DarrenMcnerney3D", "969634fbc14f40feb263409aa3c3ed5d"))),
            E(Wave5Role.Chest, "Coffre (intérieurs)", Wave5Use.Interior, "un coffre en forme simple", Wave5Fix.None, 0,
                (Folder + "Interieurs/eds_SF_Medieval_Chest_aiv.fbx", Sf("Medieval Chest", "aiv", "037b03a3e0274279be4b93b7c7cedf01"))),
            E(Wave5Role.Tankard, "Chopes (intérieurs)", Wave5Use.Interior, "des gobelets en forme simple", Wave5Fix.None, 0,
                (Folder + "Interieurs/eds_SF_Medieval_Tankard_Kigha.fbx", Sf("Medieval Tankard", "Kigha", "d95203038a9841d1b6f87b28c2e347aa"))),
        };

        /// <summary>
        /// Le corbeau animé (reconnu par <see cref="StyleRules"/>, case <see cref="StyleSlot.Crow"/>, et vérifié avec les bêtes) : ses réglages
        /// (plumes en transparence) et son crédit.
        /// </summary>
        public static readonly Wave5Entry Crow = new Wave5Entry(Wave5Role.Crow, "Corbeau animé", Live, "le corbeau à une animation", Wave5Fix.AlphaClip | Wave5Fix.Animated, 0,
            NoTint, new[] { Folder + "Creatures/eds_SF_Animated_Crow_emptypen2477.fbx" }, new[] { Sf("Animated Crow", "emptypen2477", "0609852ae9084d8a8643517e4eae68e1") });

        /// <summary>Sons et musiques de la 5e vague à attribution (crédit obligatoire), reconnus par <see cref="StyleRules"/>.</summary>
        public static readonly IReadOnlyList<Wave5Credit> AudioCredits = new[]
        {
            new Wave5Credit("Grist Mill Start Process (n° 70201)", "JustinBW", "https://freesound.org/s/70201/", "CC BY 4.0"),
            new Wave5Credit("Water Wheel (n° 202764)", "morturn", "https://freesound.org/s/202764/", "CC BY 3.0"),
            new Wave5Credit("Sehusa Fest Medieval Crowd (n° 40133)", "inchadney", "https://freesound.org/s/40133/", "CC BY 4.0"),
            new Wave5Credit("Water drips in the cave HQ (n° 546359)", "tosha73", "https://freesound.org/s/546359/", "CC BY 4.0"),
            new Wave5Credit("Creepy Dungeon Ambience (n° 166187)", "DrMinky", "https://freesound.org/s/166187/", "CC BY 4.0"),
            new Wave5Credit("Chains 1 (n° 348973)", "freemaster2", "https://freesound.org/s/348973/", "CC BY 4.0"),
            new Wave5Credit("Spider_Killed_Foley_01 (n° 197447)", "dheming", "https://freesound.org/s/197447/", "CC BY 4.0"),
            new Wave5Credit("Bat Wings, Flapping (n° 811528)", "designerschoice", "https://freesound.org/s/811528/", "CC BY 4.0"),
            new Wave5Credit("Sword Clash and Slide (n° 361485)", "FunWithSound", "https://freesound.org/s/361485/", "CC BY 4.0"),
            new Wave5Credit("SwordClash09 (n° 180828)", "32cheeseman32", "https://freesound.org/s/180828/", "CC BY 4.0"),
            new Wave5Credit("Cut Through Armor / Slice / Clang (n° 420675)", "SypherZent", "https://freesound.org/s/420675/", "CC BY 4.0"),
            new Wave5Credit("Shield block-01 (n° 812518)", "DinisNakamura", "https://freesound.org/s/812518/", "CC BY 4.0"),
            new Wave5Credit("Magic Healing Spell SFX (n° 853303)", "TommasoMotteran", "https://freesound.org/s/853303/", "CC BY 4.0"),
            new Wave5Credit("Fantasy Sound Effects Library", "Little Robot Sound Factory", "https://opengameart.org/content/fantasy-sound-effects-library", "CC BY 3.0"),
            new Wave5Credit("Magic SFX Sample", "ViRiX Dreamcore (David McKee)", "https://opengameart.org/content/magic-sfx-sample", "CC BY 3.0"),
            new Wave5Credit("Musiques (16 morceaux, dossier Music/ScottBuckley2)", "Scott Buckley", "https://www.scottbuckley.com.au", "CC BY 4.0"),
            new Wave5Credit("RPG For Wenches, Ale, and LOOT!! ; RPG Never Go Full Bard ; RPG - The Graveyard", "Hitctrl", "https://opengameart.org/users/hitctrl", "CC BY 3.0"),
        };

        /// <summary>La ressource du rôle <paramref name="role"/>.</summary>
        public static Wave5Entry Entry(Wave5Role role) => Entries.First(e => e.Role == role);

        /// <summary>
        /// Fichier que la préparation prend pour <paramref name="entry"/> parmi <paramref name="models"/> : le premier de ses fichiers (du meilleur
        /// au repli) présent sous le même nom, dans n'importe quel dossier ; null s'il n'y en a aucun. Le rang trouvé : <paramref name="rank"/>.
        /// </summary>
        public static string? Pick(Wave5Entry entry, IEnumerable<string> models, out int rank)
        {
            if (entry is null) throw new ArgumentNullException(nameof(entry));
            var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in (models ?? Array.Empty<string>()).Where(p => p != null).OrderBy(p => p, StringComparer.Ordinal))
            {
                string name = EssaiManifest.FileName(path);
                if (!byName.ContainsKey(name)) byName[name] = path;
            }
            for (rank = 0; rank < entry.Files.Count; rank++)
                if (byName.TryGetValue(EssaiManifest.FileName(entry.Files[rank]), out string? found)) return found;
            rank = -1;
            return null;
        }

        /// <summary>Comme <see cref="Pick(Wave5Entry, IEnumerable{string}, out int)"/>, sans le rang.</summary>
        public static string? Pick(Wave5Entry entry, IEnumerable<string> models) => Pick(entry, models, out int _);

        /// <summary>Ligne du rapport de vérification pour une ressource de la 5e vague (gravité, constat, quoi faire).</summary>
        public static (EssaiLevel Level, string Detail, string Fix) Describe(Wave5Entry entry, string? found, int rank)
        {
            string best = EssaiManifest.FileName(entry.Files[0]), where = EssaiManifest.Folder(entry.Files[0]);
            string state = entry.Use == Wave5Use.Ready ? " (prêt : pas encore de lieu ni de créature)" : "";
            if (found != null && rank == 0) return (EssaiLevel.Ok, "trouvé : " + EssaiManifest.FileName(found) + state, "");
            if (found != null) return (EssaiLevel.Warning, "repli : " + EssaiManifest.FileName(found) + state, "Pour le meilleur, dépose " + best + " dans " + where + ".");
            // Une ressource prête mais absente ne gêne rien aujourd'hui.
            return (entry.Use == Wave5Use.Ready ? EssaiLevel.Ok : EssaiLevel.Warning, "absent : " + entry.Without, "Dépose " + best + " dans " + where + ".");
        }

        /// <summary>La ressource se montre-t-elle sur cette plateforme ? (<see cref="Wave5Fix.NotOnPhone"/> : pas sur téléphone).</summary>
        public static bool Shown(Wave5Entry entry, bool phone) => entry != null && !(phone && entry.Has(Wave5Fix.NotOnPhone));

        /// <summary>
        /// Réglages à appliquer à un modèle instancié, d'après son nom (celui du fichier, sans extension, ou « … (Clone) ») : ceux de la
        /// ressource de la 5e vague du même nom, ou du corbeau animé ; <see cref="Wave5Fix.None"/> pour tout autre modèle.
        /// </summary>
        public static Wave5Fix FixesFor(string modelName)
        {
            string key = StyleRules.Key((modelName ?? string.Empty).Replace("(Clone)", ""));
            if (key.Length == 0) return Wave5Fix.None;
            foreach (Wave5Entry entry in Entries.Concat(new[] { Crow }))
                foreach (string file in entry.Files)
                {
                    string name = EssaiManifest.FileName(file);
                    int dot = name.LastIndexOf('.');
                    if (StyleRules.Key(dot > 0 ? name.Substring(0, dot) : name) == key) return entry.Fix;
                }
            return Wave5Fix.None;
        }

        /// <summary>La teinte d'un modèle sans texture, d'après son nom ; blanc pour les autres.</summary>
        public static (double R, double G, double B) TintFor(string modelName)
        {
            string key = StyleRules.Key((modelName ?? string.Empty).Replace("(Clone)", ""));
            foreach (Wave5Entry entry in Entries)
                foreach (string file in entry.Files)
                {
                    string name = EssaiManifest.FileName(file);
                    int dot = name.LastIndexOf('.');
                    if (StyleRules.Key(dot > 0 ? name.Substring(0, dot) : name) == key) return entry.Tint;
                }
            return NoTint;
        }

        /// <summary>Croc-Gris (ses deux formes, vies suivantes comprises) et le loup blanc prennent le grand loup animé.</summary>
        public static bool IsDireWolf(string id)
        {
            string key = StyleRules.Key(id ?? string.Empty);
            return key.StartsWith("crocgris", StringComparison.Ordinal) || key.StartsWith("loupblanc", StringComparison.Ordinal);
        }

        /// <summary>Ressource de l'arme tenue en main pour la forme <paramref name="look"/> (le marteau prend celui de la forge) ; null sinon.</summary>
        public static Wave5Role? WeaponRole(WeaponLook look) =>
            look == WeaponLook.Mace ? Wave5Role.Mace : look == WeaponLook.Axe ? Wave5Role.Axe : look == WeaponLook.Sword ? Wave5Role.Sword : (Wave5Role?)null;

        /// <summary>Longueur (m) de l'arme en main pour la forme <paramref name="look"/>.</summary>
        public static double WeaponLength(WeaponLook look) =>
            look == WeaponLook.Mace ? 0.75 : look == WeaponLook.Axe ? 0.7 : look == WeaponLook.Hammer ? 0.8 : 1.0;

        /// <summary>
        /// Une arme lourde de la tête (masse, hache, marteau) se tient tête en haut ; une épée, la garde (le côté le plus chargé) en bas.
        /// <paramref name="centroid"/> : centre des sommets le long du grand axe, <paramref name="min"/>..<paramref name="max"/> : ses bornes.
        /// Rend +1 si le bout « max » doit être en haut, -1 sinon.
        /// </summary>
        public static int GripSign(double centroid, double min, double max, bool headHeavy)
        {
            double middle = (min + max) / 2;
            bool heavyAtMax = centroid >= middle;
            return heavyAtMax == headHeavy ? 1 : -1;
        }

        /// <summary>Grand axe d'une boîte (0 : x, 1 : y, 2 : z) ; le premier en cas d'égalité.</summary>
        public static int LongAxis(double x, double y, double z) => x >= y && x >= z ? 0 : y >= z ? 1 : 2;

        /// <summary>
        /// Demande de Damien du 8 novembre 2026 (23 h 45, « à venir peut-être ») : animations de métiers de Mixamo (« FBX for Unity, Without
        /// Skin ») dans ce dossier ; ni un personnage ni un héros. Sans elles, les villageois gardent les gestes de la bibliothèque de Quaternius.
        /// </summary>
        public const string TradeFolder = EssaiManifest.Root + "/Mixamo/Metiers";

        /// <summary>Animations de métiers attendues (mots du nom du fichier), dans l'ordre où Damien les a citées.</summary>
        public static readonly IReadOnlyList<string> TradeClips = new[] { "Talking", "Carrying", "Stirring", "Picking Up", "Sitting Idle", "Kneeling" };

        /// <summary>Le fichier est-il rangé dans le dossier des animations de métiers ?</summary>
        public static bool IsTradeClip(string path) =>
            ("/" + (path ?? string.Empty).Replace('\\', '/').ToLowerInvariant() + "/").Contains("/mixamo/metiers/");

        /// <summary>L'animation de métier <paramref name="clip"/> (« Picking Up ») parmi <paramref name="files"/> (nom réduit qui la contient), ou null.</summary>
        public static string? TradeClip(string clip, IEnumerable<string> files)
        {
            string want = StyleRules.Key(clip);
            return (files ?? Array.Empty<string>()).Where(f => f != null && IsTradeClip(f) && StyleRules.Key(EssaiManifest.FileName(f)).Contains(want))
                .OrderBy(f => f, StringComparer.Ordinal).FirstOrDefault();
        }

        /// <summary>
        /// Le squelette de one_boy livré en une seule prise de 1 255 images (≈ 42 s) : clips découpés par la préparation, d'après les repères de
        /// CONVERSION_V5.md (garde, pas en garde, fentes, échanges, reprise ; le saut avec déplacement intégré n'est pas gardé).
        /// </summary>
        public static readonly IReadOnlyList<(string Name, int First, int Last, bool Loop)> SkeletonCuts = new[]
        {
            ("Idle", 0, 290, true), ("Walk", 300, 640, true), ("Attack", 650, 800, false), ("Attack2", 800, 975, false), ("Idle2", 1075, 1255, true),
        };

        /// <summary>
        /// Première place libre parmi <paramref name="candidates"/> : à au moins <paramref name="radius"/> m de tous les obstacles
        /// (<paramref name="obstacles"/>, polygones) ; null si aucune ne l'est (le décor n'est alors pas posé, plutôt que dans un mur).
        /// </summary>
        public static (double X, double Z)? FreeSpot(IEnumerable<(double X, double Z)> candidates, IReadOnlyList<IReadOnlyList<(double X, double Z)>> obstacles, double radius)
        {
            foreach ((double x, double z) in candidates ?? Array.Empty<(double, double)>())
            {
                bool free = true;
                foreach (IReadOnlyList<(double X, double Z)> polygon in obstacles ?? Array.Empty<IReadOnlyList<(double, double)>>())
                {
                    if (polygon == null || polygon.Count < 3) continue;
                    if (StyleRules.InsideDistance(x, z, polygon) > -radius)
                    {
                        free = false;
                        break;
                    }
                }
                if (free) return (x, z);
            }
            return null;
        }

        /// <summary>
        /// Places essayées autour de (<paramref name="x"/>, <paramref name="z"/>) pour un décor : d'abord dans la direction voulue
        /// (<paramref name="yaw"/>, degrés, 0 = +z) à <paramref name="distance"/> m, puis en tournant par pas de 45°, puis plus loin.
        /// </summary>
        public static IEnumerable<(double X, double Z)> Around(double x, double z, double yaw, double distance)
        {
            foreach (double d in new[] { distance, distance * 1.5, distance * 2 })
                for (int i = 0; i < 8; i++)
                {
                    int step = (i + 1) / 2 * (i % 2 == 1 ? 1 : -1);
                    double a = (yaw + 45 * step) * Math.PI / 180;
                    yield return (x + d * Math.Sin(a), z + d * Math.Cos(a));
                }
        }
    }
}
