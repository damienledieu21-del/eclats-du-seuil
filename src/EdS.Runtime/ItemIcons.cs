using System;
using System.Collections.Generic;
using System.Linq;

namespace EdS.Runtime
{
    /// <summary>
    /// Sous-lot V2.11 (achat de Damien du 8 novembre 2026 : « 6200 Fantasy RPG Icons Pack » de CraftPix, licence de l'Asset Store) : icône peinte
    /// du pack pour un objet de l'inventaire et des boutiques, d'après son identifiant. Le pack est importé par Damien dans <c>Assets</c> (jamais
    /// dans le dépôt : sa licence ne le permet pas) ; son dossier n'est pas supposé : il est reconnu par son nom (« Fantasy RPG Icons »,
    /// « CraftPix »). Correspondance : une table pour les objets importants, puis des règles de nom (mots français de l'identifiant → mots
    /// anglais des fichiers et dossiers du pack), puis rien : l'icône actuelle du jeu reste. Affichage seulement.
    /// </summary>
    public static class ItemIcons
    {
        /// <summary>Nom du manifeste des licences de l'Asset Store, écrit hors du dépôt par la préparation (dans <c>Assets/Style/Generated</c>).</summary>
        public const string LicenseManifest = EssaiManifest.Root + "/Generated/LICENCES_ASSET_STORE.md";

        /// <summary>Le fichier vient-il du pack d'icônes de CraftPix ? (un dossier du chemin, réduit, le nomme)</summary>
        public static bool IsPackPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string[] parts = path.Replace('\\', '/').Split('/');
            for (int i = 0; i < parts.Length - 1; i++)
            {
                string k = StyleRules.Key(parts[i]);
                if (k.Contains("fantasyrpgicons") || k.Contains("craftpix") || k.Contains("6200fantasy") || k.Contains("fantasyicons")) return true;
            }
            return false;
        }

        /// <summary>Le fichier est-il une image d'icône (PNG, TGA, PSD) du pack ?</summary>
        public static bool IsIcon(string path) =>
            IsPackPath(path) && (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".tga", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".psd", StringComparison.OrdinalIgnoreCase));

        /// <summary>Table des objets importants (identifiant → mots cherchés, le meilleur d'abord) : armes du Seuil, quête, Croc-Gris.</summary>
        private static readonly Dictionary<string, string[]> Explicit = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["item.lame_du_seuil"] = new[] { "magicsword", "crystalsword", "sword" },
            ["item.collier_de_croc_gris"] = new[] { "fang", "tooth", "necklace", "amulet" },
            ["item.croc_de_croc_gris"] = new[] { "fang", "tooth", "claw" },
            ["item.eclat_de_seuil"] = new[] { "crystal", "shard", "gem" },
            ["item.eclat_du_seuil_endormi"] = new[] { "crystal", "shard", "gem" },
            ["item.poussiere_d_echo"] = new[] { "dust", "powder", "sand", "crystal" },
            ["item.talisman_du_seuil"] = new[] { "talisman", "amulet", "necklace" },
            ["item.potion_de_soin"] = new[] { "healthpotion", "redpotion", "potion" },
            ["item.remede_de_l_aube"] = new[] { "elixir", "potion", "flask" },
            ["item.herbe_de_l_aube"] = new[] { "herb", "flower", "plant", "leaf" },
            ["item.minerai_de_fer"] = new[] { "ironore", "ore", "iron", "stone" },
            ["item.peau_de_loup"] = new[] { "wolfskin", "fur", "hide", "leather", "skin" },
            ["item.miche_du_matin"] = new[] { "bread", "loaf", "bun" },
            ["item.chat_de_rosalie"] = new[] { "cat" },
            ["item.cor_de_la_tour"] = new[] { "horn" },
            ["item.oeil_de_verre"] = new[] { "eye", "orb", "glass" },
            ["item.pierre_a_aiguiser"] = new[] { "whetstone", "stone" },
        };

        /// <summary>Règles de nom : début d'un mot français de l'identifiant → mots anglais cherchés (le meilleur d'abord).</summary>
        private static readonly (string French, string[] English)[] Rules =
        {
            ("epee", new[] { "sword" }), ("lame", new[] { "blade", "sword" }), ("hachette", new[] { "hatchet", "axe" }), ("hache", new[] { "axe" }),
            ("masse", new[] { "mace" }), ("fleau", new[] { "flail", "mace" }), ("marteau", new[] { "hammer" }), ("pioche", new[] { "pickaxe", "pick" }),
            ("faucille", new[] { "sickle", "scythe" }), ("ecu", new[] { "shield" }), ("targe", new[] { "buckler", "shield" }), ("rondache", new[] { "buckler", "roundshield", "shield" }),
            ("pavois", new[] { "towershield", "shield" }), ("bouclier", new[] { "shield" }), ("casque", new[] { "helmet", "helm" }), ("heaume", new[] { "helm", "helmet" }),
            ("coiffe", new[] { "hood", "cap", "helmet" }), ("chapeau", new[] { "hat" }), ("epaulieres", new[] { "shoulder", "pauldron" }), ("spalieres", new[] { "pauldron", "shoulder" }),
            ("mantelet", new[] { "mantle", "shoulder", "cape" }), ("cape", new[] { "cape", "cloak" }), ("gambison", new[] { "gambeson", "tunic", "armor" }),
            ("cotte", new[] { "chainmail", "mail", "armor" }), ("brigandine", new[] { "brigandine", "armor" }), ("cuirasse", new[] { "cuirass", "breastplate", "armor" }),
            ("plastron", new[] { "breastplate", "chest", "armor" }), ("brassards", new[] { "bracer", "bracers" }), ("gantelets", new[] { "gauntlet", "glove" }),
            ("gants", new[] { "glove", "gloves" }), ("ceinturon", new[] { "belt" }), ("ceinture", new[] { "belt" }), ("braies", new[] { "pants", "trousers", "legs" }),
            ("jambieres", new[] { "greaves", "leggings", "pants" }), ("cuissards", new[] { "cuisses", "leggings", "pants" }), ("bottes", new[] { "boots", "boot" }),
            ("solerets", new[] { "sabatons", "boots" }), ("amulette", new[] { "amulet", "necklace" }), ("pendentif", new[] { "pendant", "necklace", "amulet" }),
            ("medaillon", new[] { "medallion", "amulet" }), ("chapelet", new[] { "rosary", "necklace" }), ("collier", new[] { "necklace", "amulet" }),
            ("anneau", new[] { "ring" }), ("chevaliere", new[] { "signet", "ring" }), ("relique", new[] { "relic", "artifact" }), ("talisman", new[] { "talisman", "amulet" }),
            ("dent", new[] { "tooth", "fang" }), ("croc", new[] { "fang", "tooth" }), ("griffe", new[] { "claw" }), ("defense", new[] { "tusk", "horn" }),
            ("pierre", new[] { "stone", "rock", "gem" }), ("eclat", new[] { "shard", "crystal" }), ("peau", new[] { "hide", "fur", "leather", "skin" }),
            ("fleche", new[] { "arrow" }), ("minerai", new[] { "ore" }), ("herbe", new[] { "herb", "plant" }), ("ferrure", new[] { "iron", "metal", "nail" }),
            ("potion", new[] { "potion" }), ("elixir", new[] { "elixir", "potion" }), ("remede", new[] { "remedy", "potion" }), ("huile", new[] { "oil", "flask" }),
            ("miche", new[] { "bread" }), ("pommes", new[] { "apple" }), ("lettre", new[] { "letter", "scroll" }), ("registre", new[] { "book", "ledger" }),
            ("carnet", new[] { "notebook", "book" }), ("sac", new[] { "bag", "sack" }), ("bourse", new[] { "pouch", "purse", "bag" }), ("corde", new[] { "rope" }),
            ("seau", new[] { "bucket" }), ("cor", new[] { "horn" }), ("poussiere", new[] { "dust", "powder" }),
        };

        /// <summary>Mots anglais cherchés pour l'objet <paramref name="itemId"/> (« item.epee_de_garde » → sword), le meilleur d'abord ; vide sinon.</summary>
        public static IReadOnlyList<string> Keywords(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return Array.Empty<string>();
            var words = new List<string>();
            if (Explicit.TryGetValue(itemId, out string[]? table)) words.AddRange(table);
            string id = itemId.StartsWith("item.", StringComparison.Ordinal) ? itemId.Substring(5) : itemId;
            // Le premier mot qui a une règle décide (« epee_du_capitaine » : épée ; « peau_d_ours » : peau).
            foreach (string part in id.Split('_', '.', '-'))
            {
                if (part.Length < 2) continue;
                (string French, string[] English) rule = Rules.FirstOrDefault(r => part == r.French || (r.French.Length >= 4 && part.StartsWith(r.French, StringComparison.Ordinal)));
                if (rule.English == null) continue;
                foreach (string w in rule.English)
                    if (!words.Contains(w)) words.Add(w);
                break;
            }
            return words;
        }

        /// <summary>Mots d'un chemin (dossiers et nom du fichier, coupés aux signes et aux majuscules, réduits).</summary>
        public static IReadOnlyList<string> PathWords(string path)
        {
            var words = new List<string>();
            foreach (string part in (path ?? string.Empty).Replace('\\', '/').Split('/'))
            {
                string p = part;
                int dot = p.LastIndexOf('.');
                if (dot > 0) p = p.Substring(0, dot);
                var current = new System.Text.StringBuilder();
                for (int i = 0; i < p.Length; i++)
                {
                    char c = p[i];
                    bool cut = !char.IsLetter(c) || (char.IsUpper(c) && i > 0 && char.IsLower(p[i - 1]));
                    if (cut && current.Length > 0)
                    {
                        words.Add(current.ToString());
                        current.Clear();
                    }
                    if (char.IsLetter(c)) current.Append(char.ToLowerInvariant(c));
                }
                if (current.Length > 0) words.Add(current.ToString());
                // Le nom entier réduit aussi (« HealthPotion » → healthpotion).
                string whole = StyleRules.Key(p);
                if (whole.Length > 0 && !words.Contains(whole)) words.Add(whole);
            }
            return words;
        }

        /// <summary>
        /// Icône du pack pour l'objet <paramref name="itemId"/> parmi <paramref name="icons"/> : pour le premier mot cherché qui en trouve, une
        /// des icônes dont un mot du chemin le vaut (<see cref="Matches"/>) (le nom du fichier d'abord, puis ses dossiers), choisie de façon stable d'après
        /// l'identifiant (six épées n'ont pas toutes la même) ; null : l'icône actuelle du jeu reste.
        /// </summary>
        public static string? Pick(string itemId, IEnumerable<string> icons)
        {
            List<string> pack = (icons ?? Array.Empty<string>()).Where(p => p != null && IsIcon(p)).Distinct().OrderBy(p => p, StringComparer.Ordinal).ToList();
            if (pack.Count == 0) return null;
            var words = pack.Select(p => (Path: p, File: PathWords(EssaiManifest.FileName(p)), All: PathWords(p))).ToList();
            foreach (string wanted in Keywords(itemId))
            {
                List<string> inName = words.Where(w => w.File.Any(x => Matches(x, wanted))).Select(w => w.Path).ToList();
                List<string> found = inName.Count > 0 ? inName : words.Where(w => w.All.Any(x => Matches(x, wanted))).Select(w => w.Path).ToList();
                if (found.Count > 0) return found[StyleRules.Variant(itemId, found.Count)];
            }
            return null;
        }

        /// <summary>
        /// Un mot du chemin vaut-il le mot cherché ? Le même, son pluriel (« axes », « boxes »), ou, pour un mot cherché d'au moins cinq lettres,
        /// un mot qui commence par lui (« swordiron ») ; « hat » n'est pas « hatchet », « ring » n'est pas « rings » d'un autre sens que l'anneau.
        /// </summary>
        public static bool Matches(string word, string wanted) =>
            word == wanted || word == wanted + "s" || word == wanted + "es"
            || (wanted.Length >= 5 && word.StartsWith(wanted, StringComparison.Ordinal));

        /// <summary>Correspondance de tous les objets <paramref name="itemIds"/> (ceux qui ont une icône du pack seulement), dans l'ordre donné.</summary>
        public static IReadOnlyList<(string Item, string Icon)> Map(IEnumerable<string> itemIds, IEnumerable<string> icons)
        {
            List<string> pack = (icons ?? Array.Empty<string>()).Where(p => p != null && IsIcon(p)).ToList();
            var map = new List<(string, string)>();
            foreach (string id in itemIds ?? Array.Empty<string>())
                if (Pick(id, pack) is string icon) map.Add((id, icon));
            return map;
        }

        /// <summary>Texte du manifeste des licences de l'Asset Store (hors dépôt) : le pack, sa licence, son dossier, où il sert.</summary>
        public static string Manifest(string? packFolder, int mapped, int items) =>
            "# Ressources sous licence de l'Asset Store (hors dépôt)\n\n"
            + "Écrit par « Préparer l'essai de style » (sous-lot V2.11). Ces fichiers ne vont **jamais** dans le dépôt git : la licence standard de "
            + "l'Asset Store permet de les utiliser dans le jeu construit, pas de les redistribuer en source.\n\n"
            + "| Pack | Éditeur | Licence | Dossier | Usage |\n|---|---|---|---|---|\n"
            + "| 6200 Fantasy RPG Icons Pack | CraftPix | licence standard de l'Asset Store (Standard Unity Asset Store EULA) | "
            + (packFolder ?? "non trouvé") + " | icônes de l'inventaire et des boutiques : " + mapped + " objets sur " + items + " |\n";
    }
}
