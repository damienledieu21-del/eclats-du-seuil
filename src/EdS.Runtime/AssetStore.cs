using System;
using System.Collections.Generic;
using System.Linq;

namespace EdS.Runtime
{
    /// <summary>Sous-lot V2.12 : un pack gratuit de l'Asset Store importé par Damien dans <c>Assets</c> (hors dépôt).</summary>
    public sealed class AssetStorePack
    {
        public AssetStorePack(string title, string publisher, string folder, string usage)
        {
            Title = title;
            Publisher = publisher;
            Folder = folder;
            Usage = usage;
        }

        /// <summary>Nom du pack sur l'Asset Store.</summary>
        public string Title { get; }

        /// <summary>Éditeur (dossier racine du pack quand l'éditeur n'est pas noté).</summary>
        public string Publisher { get; }

        /// <summary>Dossier du pack, sous <c>Assets</c> (« Assets/RPG FOOD PROPS/UNIVERSAL RENDER PIPELINE/Prefabs »).</summary>
        public string Folder { get; }

        /// <summary>Où il sert dans le jeu (manifeste des licences).</summary>
        public string Usage { get; }

        public Wave5Credit Credit => new Wave5Credit(Title, Publisher, AssetStore.StoreUrl, AssetStore.License);
    }

    /// <summary>
    /// Sous-lot V2.12 (décision de Damien du 9 novembre 2026, avant son essai) : les packs gratuits de l'Asset Store importés dans <c>Assets</c>
    /// (pas dans <c>Assets/Style</c> ; jamais dans le dépôt, licence standard de l'Asset Store : seuls leurs chemins sont ici). Liste partagée par
    /// la préparation (<c>EdsWave5</c>), le jeu (<c>Wave5View</c>, <c>WorldMarkers</c>) et le vérificateur d'essai, dans la même forme que la
    /// 5e vague (<see cref="Wave5Entry"/>, rôles <see cref="Wave5Role"/> ajoutés à la fin). Un prefab est reconnu par la fin de son chemin
    /// (dossier du pack et nom) : un pack rangé dans un sous-dossier compte encore ; des noms courts comme « Wall » ou « Tent » ne sont jamais
    /// pris ailleurs. Sans le fichier, le jeu garde ce qu'il montrait. Affichage seulement : le cœur déterministe n'en sait rien.
    /// </summary>
    public static class AssetStore
    {
        /// <summary>Licence de tous ces packs.</summary>
        public const string License = "Standard Unity Asset Store EULA";

        /// <summary>Lien générique (les pages exactes sont dans « My Assets » de Damien ; rien d'inventé).</summary>
        public const string StoreUrl = "https://assetstore.unity.com";

        public static readonly AssetStorePack Bakery = new AssetStorePack("Baked Bread and Pastry", "LUMI", "Assets/Baked Bread and Pastry/Prefabs",
            "vitrine du fournil de Firmin (pains et croissant photoscannés)");
        public static readonly AssetStorePack Food = new AssetStorePack("RPG Food Props", "RPG FOOD PROPS (dossier du pack)", "Assets/RPG FOOD PROPS/UNIVERSAL RENDER PIPELINE/Prefabs",
            "étal de Bertille, étal des fêtes (pains, fromages, viandes, bouteilles, caisses garnies) ; tomate, orange, pastèque et citrouille écartées");
        public static readonly AssetStorePack Village = new AssetStorePack("Free Medieval Village Pack Vol.1", "MyDreamGameStudio", "Assets/MyDreamGameStudio/Medieval Village Pack Vol.1/Prefabs/URP",
            "forge (enclume, établi, présentoir), roue à eau, abri à bêtes, tonneaux, seaux, rouet, charrette, souche ; maisons, taverne et moulin prêts");
        public static readonly AssetStorePack Castle = new AssetStorePack("Medieval Castle (modulaire)", "Advance Studios", "Assets/Advance Studios/Medieval Castle/Prefabs",
            "prêt pour un château ou une ville fortifiée (pas encore de lieu)");
        public static readonly AssetStorePack Dungeon = new AssetStorePack("Dungeon Modular Pack", "DungeonModularPack (dossier du pack)", "Assets/DungeonModularPack/Prefabs",
            "prêt pour la crypte (pas encore de lieu)");
        public static readonly AssetStorePack Props = new AssetStorePack("Medieval Props Pack 01", "Medieval Props Pack 01 (dossier du pack)", "Assets/Medieval Props Pack 01/Universal Render Pipeline(URP)/Prefabs",
            "caisses, échelle, charrette à bras, tentes du camp des rôdeurs ; vases, pots, chopes, tonneau, seau et table dans les intérieurs (en repli)");
        public static readonly AssetStorePack Sounds = new AssetStorePack("Basic RPG Sounds", "Multiple Solutions", "Assets/Multiple Solutions/Basic RPG Sounds/Audio",
            "bruitages en repli des sons choisis (sons médiévaux seulement : arcade et modernes écartés)");
        public static readonly AssetStorePack Gui = new AssetStorePack("Fantasy Wooden GUI Free", "Fantasy Wooden GUI (dossier du pack)", "Assets/Fantasy Wooden GUI  Free/normal_ui_set A",
            "fond de pierre des fenêtres du livre (sac, boutique, forge, compétences, voyage) ; parchemin en option");
        public static readonly AssetStorePack Armor = new AssetStorePack("Character Constructor (URP Medieval Fantasy)", "SopaDopa", "Assets/SopaDopa/URP_Medieval_Fantasy",
            "prêt pour gardes et chevaliers (ordinateur seulement)");
        public static readonly AssetStorePack Icons = new AssetStorePack("6200 Fantasy RPG Icons Pack", "CraftPix", "Assets/Fantasy RPG Icons Pack/Asset",
            "icônes de l'inventaire et des boutiques (version « No Background » d'abord)");

        /// <summary>Tous les packs, dans l'ordre du manifeste.</summary>
        public static readonly IReadOnlyList<AssetStorePack> Packs = new[] { Bakery, Food, Village, Castle, Dungeon, Props, Sounds, Gui, Armor, Icons };

        private const Wave5Use Live = Wave5Use.Live;
        private const Wave5Use Ready = Wave5Use.Ready;
        private const string Later = "rien (prêt pour plus tard)";
        private const Wave5Fix Big = Wave5Fix.Heavy | Wave5Fix.NotOnPhone;
        private static readonly (double, double, double) NoTint = (1, 1, 1);

        private static Wave5Entry E(Wave5Role role, string label, Wave5Use use, string without, Wave5Fix fix, double height, AssetStorePack pack, params string[] names)
        {
            List<string> files = names.Select(n => pack.Folder + "/" + n + (n.EndsWith(".prefab", StringComparison.Ordinal) ? "" : ".prefab")).ToList();
            return new Wave5Entry(role, label, use, without, fix, height, NoTint, files, files.Select(_ => pack.Credit).ToList());
        }

        /// <summary>Prefab du pack de village (suffixe « _URP »).</summary>
        private static string[] V(params string[] names) => names.Select(n => n + "_URP").ToArray();

        /// <summary>Toutes les ressources de l'Asset Store, branchées d'abord, puis prêtes, puis intérieurs.</summary>
        public static readonly IReadOnlyList<Wave5Entry> Entries = new[]
        {
            // 1. Boulangerie et étals : les pains photoscannés de LUMI passent devant tout le reste dans la vitrine de Firmin.
            E(Wave5Role.StoreBread, "Pains et croissant photoscannés (LUMI) dans la vitrine", Live, "les pains de Megascans, de Sketchfab ou de Poly Haven", Wave5Fix.None, 0, Bakery,
                "SM_Bread_03", "SM_Croissant_02a", "SM_Flatbread_01a"),
            E(Wave5Role.StoreFood, "Victuailles des étals (pains, fromages, viandes, bouteilles)", Live, "étal sans victuailles", Wave5Fix.None, 0, Food,
                "Bread1", "Cheese1", "Meat1", "Bottle1", "Bread2", "Cheese2", "Sausage", "Bottle2", "Meat2", "Apple", "Plate"),
            E(Wave5Role.StoreCrate, "Caisses garnies au pied des étals", Live, "pas de caisses", Wave5Fix.None, 0, Food,
                "WoodenBox1 Cheese", "WoodenBox1 Bottle1", "WoodenBox1 Apple", "WoodenBox1 Bottle2", "WoodenBox1", "WoodenBox2"),
            E(Wave5Role.TraderTable, "Étal marchand (table) de Bertille, du marché et des fêtes", Live, "la caisse de poires seule", Wave5Fix.None, 0, Food, "TraderTable"),
            E(Wave5Role.TraderTent, "Étal sous tente (fêtes, marché)", Live, "pas de tente (et jamais sur téléphone)", Big, 0, Food, "TraderTent"),
            // 2. La forge : à côté de l'enclume en forme simple de l'atelier.
            E(Wave5Role.Anvil, "Enclume de la forge", Live, "l'enclume en forme simple", Wave5Fix.None, 0, Village, V("Anvil")),
            E(Wave5Role.SmithTable, "Établi du forgeron", Live, "pas d'établi", Wave5Fix.None, 0, Village, V("Blacksmith_Table")),
            E(Wave5Role.WeaponStand, "Présentoir d'armes de la forge", Live, "pas de présentoir", Wave5Fix.None, 0, Village, V("Weapon_Stand")),
            E(Wave5Role.QuenchTub, "Bac à tremper de la forge", Live, "pas de bac", Wave5Fix.None, 0, Village, V("Wooden_Tub")),
            // 3. Village, hameau, rivière, camp.
            E(Wave5Role.WaterWheel, "Roue à eau au bord de la rivière, près du moulin", Live, "pas de roue (et jamais sur téléphone)", Big, 0, Village, V("Water_Wheel")),
            E(Wave5Role.AnimalShed, "Abri à bêtes du hameau", Live, "pas d'abri (et jamais sur téléphone)", Big, 0, Village, V("Animal_Shed")),
            E(Wave5Role.VillageBarrel, "Tonneaux du village", Live, "pas de tonneaux", Wave5Fix.None, 0, Village, V("Wooden_Barrel")),
            E(Wave5Role.VillageBucket, "Seaux du village", Live, "pas de seaux", Wave5Fix.None, 0, Village, V("Wooden_Bucket")),
            E(Wave5Role.SpinningWheel, "Rouet devant une maison du hameau", Live, "pas de rouet", Wave5Fix.None, 0, Village, V("Wooden_Spinning_Wheel")),
            E(Wave5Role.VillageCart, "Charrette du bourg", Live, "pas de charrette au bourg", Wave5Fix.None, 0, Village, V("Wooden_Cart")),
            E(Wave5Role.Stump, "Souche à fendre le bois", Live, "pas de souche", Wave5Fix.None, 0, Village, V("Stump_A")),
            E(Wave5Role.PropCrate, "Caisses empilées du village", Live, "pas de caisses", Wave5Fix.None, 0, Props, "Crate_01", "Crate_02", "Crate_03", "Wooden Box 01"),
            E(Wave5Role.Ladder, "Échelle contre un mur", Live, "pas d'échelle", Wave5Fix.None, 0, Props, "Folding_Ladder"),
            E(Wave5Role.Handcart, "Charrette à bras du village", Live, "pas de charrette à bras", Wave5Fix.None, 0, Props, "Handcart"),
            E(Wave5Role.CampTent, "Tentes du camp des rôdeurs", Live, "pas de tentes", Wave5Fix.None, 0, Props, "Tent"),

            // Prêtes : bâtiments (les maisons de Sketchfab et le moulin de Tim0 sont des choix de Damien : pas remplacés), château, donjon, armures.
            E(Wave5Role.VillageHouse, "Maisons du pack de village (alternative aux maisons de Sketchfab)", Ready, Later, Big, 0, Village,
                V("House_1", "House_2", "House_3", "House_4", "House_5", "House_6", "House_7", "House_8", "House_9", "House_10")),
            E(Wave5Role.Tavern, "Taverne (alternative)", Ready, Later, Big, 0, Village, V("Tavern")),
            E(Wave5Role.Smithy, "Bâtiment de la forge (alternative)", Ready, Later, Big, 0, Village, V("Blacksmith", "Blacksmith_Logo")),
            E(Wave5Role.VillageWindmill, "Moulin à vent du pack (le moulin de Tim0 reste, choix de Damien)", Ready, Later, Big, 0, Village, V("WindMill")),
            E(Wave5Role.SmithWeapons, "Armes posées de la forge (marteau, épée, lance, bouclier)", Ready, Later, Wave5Fix.None, 0, Village,
                V("Hammer", "Sword", "Spear", "Shield")),
            E(Wave5Role.CastleKeep, "Château (bloc entier)", Ready, Later, Big, 0, Castle, "Castle"),
            E(Wave5Role.CastleTower, "Tours de château", Ready, Later, Big, 0, Castle, "Tower A", "Tower B"),
            E(Wave5Role.CastleWall, "Murs et colonnes de rempart", Ready, Later, Big, 0, Castle, "Wall", "Column"),
            E(Wave5Role.CastleBridge, "Ponts de château", Ready, Later, Big, 0, Castle, "Bridge", "Bridge Large", "Bridge Small"),
            E(Wave5Role.CastleGate, "Portes de rempart", Ready, Later, Big, 0, Castle, "Double Door Frame", "Simple Door Frame"),
            E(Wave5Role.CastleHouse, "Maisons de ville fortifiée", Ready, Later, Big, 0, Castle, "House A", "House B"),
            E(Wave5Role.DungeonWall, "Murs de crypte (donjon modulaire)", Ready, Later, Wave5Fix.None, 0, Dungeon, "Wall_A", "Wall_B", "Wall_C"),
            E(Wave5Role.DungeonFloor, "Dalles et marches de crypte", Ready, Later, Wave5Fix.None, 0, Dungeon, "Tile_A", "Tile_B", "Step_A"),
            E(Wave5Role.DungeonArch, "Arches et piliers de crypte", Ready, Later, Wave5Fix.None, 0, Dungeon, "Arch_A", "Pillar_A", "Pillar_B"),
            E(Wave5Role.DungeonTorch, "Torches de crypte", Ready, Later, Wave5Fix.None, 0, Dungeon, "Torch_A", "Torch_B"),
            E(Wave5Role.DungeonRail, "Rambardes de crypte", Ready, Later, Wave5Fix.None, 0, Dungeon, "Handrail_A", "Handrail_B"),
            E(Wave5Role.ArmoredMan, "Garde ou chevalier en armure modulaire (homme)", Ready, Later, Big, 1.8, Armor, "Constructor_URP"),
            E(Wave5Role.ArmoredWoman, "Garde ou chevalière en armure modulaire (femme)", Ready, Later, Big, 1.7, Armor, "Constructor_fem_1"),

            // Intérieurs : en repli seulement (une sorte d'objet que les intérieurs de Poly Haven ou de Sketchfab n'ont pas).
            new Wave5Entry(Wave5Role.StoreFurniture, "Objets d'intérieur de l'Asset Store (en repli)", Wave5Use.Interior, "les formes simples des intérieurs", Wave5Fix.None, 0, NoTint,
                InteriorFiles, InteriorFiles.Select(f => (f.StartsWith(Props.Folder, StringComparison.Ordinal) ? Props : f.StartsWith(Food.Folder, StringComparison.Ordinal) ? Food : Village).Credit).ToList()),
        };

        private static IReadOnlyList<string> InteriorFiles => new[]
        {
            Props.Folder + "/Barrel 01.prefab", Props.Folder + "/Wooden_Bucket_1.prefab", Props.Folder + "/Clay Pot.prefab", Props.Folder + "/Clay_Mug.prefab",
            Props.Folder + "/Clay Mug 2.prefab", Props.Folder + "/Old_Table.prefab", Props.Folder + "/Vase_1.prefab", Props.Folder + "/Vase_2.prefab",
            Props.Folder + "/Vase_3.prefab", Props.Folder + "/Vase_4.prefab", Props.Folder + "/Vase_5.prefab",
            Village.Folder + "/Wooden_Bowl_URP.prefab", Village.Folder + "/Pot_1_URP.prefab", Village.Folder + "/Pot_2_URP.prefab", Village.Folder + "/Wooden_Table_URP.prefab",
        };

        /// <summary>
        /// Écartés des étals (anachronismes pour un monde inspiré du Moyen Âge européen : tomate, orange, pastèque, citrouille venues plus tard) ;
        /// gardés hors de la liste, à réserver à un usage justifié (marchand venu de loin) sur décision de Damien.
        /// </summary>
        public static readonly IReadOnlyList<string> Avoided = new[]
        {
            "Tomato", "Orange", "Watermelon", "Pumpkin", "WoodenBox1 Orange", "WoodenBox1 Tomato", "WoodenBox2 Pumpkin", "WoodenBox2 Watermelon",
        };

        /// <summary>La ressource du rôle (de l'Asset Store), ou null.</summary>
        public static Wave5Entry? EntryOrNull(Wave5Role role) => Entries.FirstOrDefault(e => e.Role == role);

        /// <summary>Le rôle vient-il de l'Asset Store (V2.12) ?</summary>
        public static bool IsStoreRole(Wave5Role role) => EntryOrNull(role) != null;

        /// <summary>Nombre de fichiers différents gardés pour un rôle (tous, au plus 12 : pains, victuailles, caisses, pièces de château).</summary>
        public static int Variants(Wave5Role role) => EntryOrNull(role) is Wave5Entry e ? Math.Min(12, e.Files.Count) : 1;

        private static string Norm(string path) => "/" + (path ?? string.Empty).Replace('\\', '/').ToLowerInvariant().TrimStart('/');

        /// <summary>Fin de chemin comparée : le chemin attendu sans « Assets/ » (« /rpg food props/…/prefabs/bread1.prefab »).</summary>
        private static string Tail(string expected)
        {
            string n = Norm(expected);
            return n.StartsWith("/assets/", StringComparison.Ordinal) ? n.Substring("/assets".Length) : n;
        }

        /// <summary>Le prefab <paramref name="path"/> est-il le fichier attendu <paramref name="expected"/> (même fin de chemin, casse ignorée) ?</summary>
        public static bool Same(string path, string expected) => path != null && expected != null && Norm(path).EndsWith(Tail(expected), StringComparison.Ordinal);

        /// <summary>
        /// Fichiers trouvés pour <paramref name="entry"/> parmi <paramref name="prefabs"/>, dans l'ordre de la liste (le meilleur d'abord), chacun
        /// une fois ; pour un même fichier attendu, le premier chemin dans l'ordre ordinal.
        /// </summary>
        public static IReadOnlyList<string> Found(Wave5Entry entry, IEnumerable<string> prefabs)
        {
            if (entry is null) throw new ArgumentNullException(nameof(entry));
            List<string> all = (prefabs ?? Array.Empty<string>()).Where(p => p != null).Distinct().OrderBy(p => p, StringComparer.Ordinal).ToList();
            var found = new List<string>();
            foreach (string file in entry.Files)
                if (all.FirstOrDefault(p => Same(p, file)) is string hit && !found.Contains(hit)) found.Add(hit);
            return found;
        }

        /// <summary>Premier fichier trouvé (du meilleur au repli) et son rang ; null et -1 sans aucun.</summary>
        public static string? Pick(Wave5Entry entry, IEnumerable<string> prefabs, out int rank)
        {
            if (entry is null) throw new ArgumentNullException(nameof(entry));
            List<string> all = (prefabs ?? Array.Empty<string>()).Where(p => p != null).Distinct().OrderBy(p => p, StringComparer.Ordinal).ToList();
            for (int i = 0; i < entry.Files.Count; i++)
            {
                string expected = entry.Files[i];
                if (all.FirstOrDefault(p => Same(p, expected)) is string hit)
                {
                    rank = i;
                    return hit;
                }
            }
            rank = -1;
            return null;
        }

        /// <summary>Le pack d'une ressource (celui de son premier fichier).</summary>
        public static AssetStorePack PackOf(Wave5Entry entry) =>
            Packs.FirstOrDefault(p => entry.Files[0].StartsWith(p.Folder + "/", StringComparison.Ordinal)) ?? throw new ArgumentException("Hors des packs : " + entry.Role);

        /// <summary>Ligne du vérificateur (gravité, constat, quoi faire) : trouvé, repli, ou absent (rien ne casse : repli du jeu).</summary>
        public static (EssaiLevel Level, string Detail, string Fix) Describe(Wave5Entry entry, IReadOnlyList<string> found, bool phone)
        {
            string state = entry.Use == Wave5Use.Ready ? " (prêt : pas encore de lieu)" : entry.Use == Wave5Use.Interior ? " (intérieurs, en repli)" : "";
            string cost = entry.Has(Wave5Fix.NotOnPhone) ? " ; jamais sur téléphone (Note8)" : "";
            AssetStorePack pack = PackOf(entry);
            int count = found?.Count ?? 0;
            if (count == entry.Files.Count) return (EssaiLevel.Ok, "trouvé : " + count + " prefab" + (count > 1 ? "s" : "") + state + cost, "");
            string import = "Importe « " + pack.Title + " » (Package Manager → My Assets) : prefabs attendus dans " + pack.Folder + ".";
            if (count > 0) return (EssaiLevel.Ok, "trouvé : " + count + " sur " + entry.Files.Count + state + cost, "");
            return (entry.Use == Wave5Use.Live ? EssaiLevel.Warning : EssaiLevel.Ok, "absent : " + entry.Without, import);
        }

        // --- Intérieurs -----------------------------------------------------------------------------------------------------------------------

        /// <summary>Sorte d'objet d'intérieur d'un prefab de l'Asset Store (vases et pots de terre : pot de terre ; chopes : gobelets).</summary>
        public static InteriorProp InteriorKind(string path)
        {
            string key = StyleRules.Key(EssaiManifest.FileName(path ?? string.Empty).Replace(".prefab", "").Replace("_URP", ""));
            if (key.StartsWith("vase", StringComparison.Ordinal) || key == "claypot") return InteriorProp.ClayPot;
            if (key.StartsWith("claymug", StringComparison.Ordinal)) return InteriorProp.Goblets;
            if (key.StartsWith("woodenbowl", StringComparison.Ordinal)) return InteriorProp.Bowl;
            return InteriorRules.PropOf(path ?? string.Empty);
        }

        /// <summary>
        /// Objets d'intérieur à ajouter en repli : ceux des prefabs <paramref name="found"/> dont la sorte manque dans <paramref name="have"/>
        /// (les intérieurs de Damien ne sont jamais remplacés), au plus <paramref name="perKind"/> par sorte, dans l'ordre de la liste.
        /// </summary>
        public static IReadOnlyList<(string Path, InteriorProp Kind)> InteriorFallbacks(IEnumerable<string> found, IEnumerable<InteriorProp> have, int perKind = 3)
        {
            var taken = new HashSet<InteriorProp>(have ?? Array.Empty<InteriorProp>());
            var list = new List<(string, InteriorProp)>();
            foreach (IGrouping<InteriorProp, string> g in (found ?? Array.Empty<string>()).Where(p => p != null).GroupBy(InteriorKind))
                if (g.Key != InteriorProp.None && !taken.Contains(g.Key))
                    list.AddRange(g.Take(Math.Max(0, perKind)).Select(p => (p, g.Key)));
            return list;
        }

        // --- Sons -------------------------------------------------------------------------------------------------------------------------------

        /// <summary>Le son vient-il de Basic RPG Sounds ?</summary>
        public static bool IsStoreSound(string path) => Norm(path).Contains("/basic rpg sounds/");

        /// <summary>Mots d'un son arcade, électronique ou moderne : jamais pris (« Bumper », « D Pad Item », « Pistol Revolver », « Explosion »…).</summary>
        public static readonly IReadOnlyList<string> ModernWords = new[]
        {
            "bumper", "dpad", "pistol", "revolver", "gun", "rifle", "shotgun", "explosion", "explode", "bomb", "laser", "blaster", "arcade", "retro", "8bit",
            "chiptune", "synth", "beep", "blip", "bleep", "powerup", "levelup", "jump", "boing", "robot", "scifi", "electric", "zap", "car", "engine", "phone",
            "alarm", "siren", "glitch", "menu", "click", "select", "cursor", "pop", "bubble", "spring", "cartoon", "toy", "coin8", "pinball", "game over", "gameover", "keyboard", "typing", "computer", "notification",
        };

        /// <summary>Mots d'un son médiéval réaliste (au moins un) : lames, armures, bois, pièces, potions, pas, portes, magie sobre.</summary>
        public static readonly IReadOnlyList<string> MedievalWords = new[]
        {
            "sword", "blade", "slash", "stab", "swing", "swish", "whoosh", "metal", "armor", "armour", "shield", "block", "parry", "clang", "punch", "hit", "impact",
            "wood", "door", "chest", "coin", "gold", "pickup", "bag", "leather", "cloth", "footstep", "step", "potion", "bottle", "drink", "liquid", "heal",
            "spell", "magic", "cast", "fire", "bow", "arrow", "axe", "hammer", "anvil", "forge", "chain", "lock", "key", "book", "page", "paper", "eat", "bite",
            "growl", "roar", "grunt", "death", "die", "hurt", "pain", "dig", "chop", "mine", "rock", "stone",
        };

        /// <summary>Un son de Basic RPG Sounds est-il gardé ? (un mot médiéval, aucun mot arcade ou moderne)</summary>
        public static bool IsMedievalSound(string path)
        {
            string name = EssaiManifest.FileName(path ?? string.Empty);
            int dot = name.LastIndexOf('.');
            string key = StyleRules.Key(dot > 0 ? name.Substring(0, dot) : name);
            if (key.Length == 0) return false;
            if (ModernWords.Any(w => key.Contains(StyleRules.Key(w)))) return false;
            return MedievalWords.Any(w => key.Contains(w));
        }

        /// <summary>Sons gardés de Basic RPG Sounds parmi <paramref name="paths"/>, dans l'ordre ordinal.</summary>
        public static IReadOnlyList<string> MedievalSounds(IEnumerable<string> paths) =>
            (paths ?? Array.Empty<string>()).Where(p => p != null && IsStoreSound(p) && IsMedievalSound(p)).Distinct().OrderBy(p => p, StringComparer.Ordinal).ToList();

        /// <summary>Le son choisi (<see cref="StyleRules.PickSound"/>) parmi ceux de Damien, sinon parmi ceux de Basic RPG Sounds gardés ; null sinon.</summary>
        public static string? PickSound(IEnumerable<string> sounds, IEnumerable<string> store, StyleSound sound) =>
            StyleRules.PickSound(sounds, sound) ?? StyleRules.PickSound(MedievalSounds(store), sound);

        /// <summary>Variantes d'un son : celles de Damien, sinon celles de Basic RPG Sounds gardées (jamais un mélange).</summary>
        public static IReadOnlyList<string> PickSounds(IEnumerable<string> sounds, IEnumerable<string> store, StyleSound sound, int max)
        {
            IReadOnlyList<string> mine = StyleRules.PickSounds(sounds, sound, max);
            return mine.Count > 0 ? mine : StyleRules.PickSounds(MedievalSounds(store), sound, max);
        }

        // --- Interface en bois (Fantasy Wooden GUI) ------------------------------------------------------------------------------------------

        /// <summary>Sorte de fond de fenêtre pris dans Fantasy Wooden GUI.</summary>
        public enum GuiBoard
        {
            /// <summary>Le fond du jeu (« dalle » de Fragments du Seuil).</summary>
            Off = 0,
            /// <summary>Planche de pierre : sombre, garde le texte clair et doré des Fragments lisible (choix par défaut).</summary>
            Stone = 1,
            /// <summary>Parchemin : clair, demande une encre sombre (en option, à juger à l'essai).</summary>
            Parchment = 2,
        }

        /// <summary>Le fichier est-il une image de Fantasy Wooden GUI ?</summary>
        public static bool IsGuiPath(string path) => Norm(path).Contains("/fantasy wooden gui") && Norm(path).EndsWith(".png", StringComparison.Ordinal);

        /// <summary>
        /// Image de fond de fenêtre parmi <paramref name="paths"/> : « UI board Large … » de la sorte voulue (pierre ou parchemin), sinon la
        /// moyenne, sinon la petite ; null sans le pack (le fond du jeu reste).
        /// </summary>
        public static string? Board(IEnumerable<string> paths, GuiBoard kind)
        {
            if (kind == GuiBoard.Off) return null;
            string word = kind == GuiBoard.Stone ? "stone" : "parchment";
            List<string> gui = (paths ?? Array.Empty<string>()).Where(p => p != null && IsGuiPath(p)).OrderBy(p => p, StringComparer.Ordinal).ToList();
            foreach (string size in new[] { "large", "medium", "small" })
                if (gui.FirstOrDefault(p => { string k = StyleRules.Key(EssaiManifest.FileName(p)); return k.Contains("uiboard") && k.Contains(size) && k.Contains(word); }) is string hit)
                    return hit;
            return null;
        }

        // --- Icônes de CraftPix -------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Les icônes à garder : pour une même image rangée en « Background » et en « No Background », la version sans fond seulement (plus
        /// propre sur les cases sombres des Fragments) ; une image qui n'existe qu'avec fond est gardée.
        /// </summary>
        public static IReadOnlyList<string> PreferNoBackground(IEnumerable<string> icons)
        {
            List<string> all = (icons ?? Array.Empty<string>()).Where(p => p != null).Distinct().ToList();
            var set = new HashSet<string>(all.Select(Norm));
            return all.Where(p =>
            {
                string n = Norm(p);
                int at = n.IndexOf("/background/", StringComparison.Ordinal);
                return at < 0 || !set.Contains(n.Substring(0, at) + "/no background/" + n.Substring(at + "/background/".Length));
            }).OrderBy(p => p, StringComparer.Ordinal).ToList();
        }

        // --- Manifeste ----------------------------------------------------------------------------------------------------------------------

        /// <summary>Lignes du manifeste des licences (hors dépôt) : chaque pack, son dossier, ce qui en est pris (<paramref name="found"/> : prefabs, sons, images).</summary>
        public static string ManifestRows(Func<AssetStorePack, int> found)
        {
            var text = new System.Text.StringBuilder();
            foreach (AssetStorePack p in Packs.Where(p => p != Icons))
            {
                int n = found != null ? found(p) : 0;
                text.Append("| " + p.Title + " | " + p.Publisher + " | licence standard de l'Asset Store (" + License + ") | " + p.Folder + " | " + p.Usage + " : "
                    + (n > 0 ? n + " fichiers pris" : "non trouvé") + " (sous-lot V2.12) |\n");
            }
            return text.ToString();
        }
    }
}
