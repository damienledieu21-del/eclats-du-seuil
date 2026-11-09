using System;
using System.Collections.Generic;
using System.Linq;

namespace EdS.Runtime
{
    /// <summary>Sous-lot V2.16 : un personnage fait par Damien dans MakeHuman, rangé dans <c>Assets/Style/MakeHuman/&lt;nom&gt;/</c>.</summary>
    public sealed class MadeHumanModel
    {
        public MadeHumanModel(string name, string folder, string path)
        {
            Name = name;
            Folder = folder;
            Path = path;
        }

        /// <summary>Nom du dossier, réduit (« Gaspard le Roux » → gasparderoux) : sert à le retrouver.</summary>
        public string Name { get; }

        /// <summary>Nom du dossier tel qu'il est écrit (affiché dans le créateur de personnage).</summary>
        public string Folder { get; }

        /// <summary>Chemin du fichier FBX du personnage.</summary>
        public string Path { get; }
    }

    /// <summary>Sous-lot V2.16 : une ligne du fichier de correspondance qu'on n'a pas pu lire.</summary>
    public readonly struct MappingProblem
    {
        public MappingProblem(int line, string message)
        {
            Line = line;
            Message = message;
        }

        /// <summary>Numéro de la ligne (à partir de 1).</summary>
        public int Line { get; }

        public string Message { get; }

        public override string ToString() => "PNJ.txt, ligne " + Line + " : " + Message;
    }

    /// <summary>Sous-lot V2.16 : le fichier <c>PNJ.txt</c> lu : qui porte quel personnage MakeHuman.</summary>
    public sealed class MadeHumanMapping
    {
        public MadeHumanMapping(IReadOnlyDictionary<string, string> entries, IReadOnlyList<MappingProblem> problems)
        {
            Entries = entries;
            Problems = problems;
        }

        /// <summary>Clé réduite du PNJ → modèle voulu (nom de dossier ou de fichier, réduit), dans l'ordre alphabétique des clés.</summary>
        public IReadOnlyDictionary<string, string> Entries { get; }

        /// <summary>Lignes illisibles (ignorées), dans l'ordre du fichier.</summary>
        public IReadOnlyList<MappingProblem> Problems { get; }

        public static MadeHumanMapping Empty { get; } = new MadeHumanMapping(new Dictionary<string, string>(), Array.Empty<MappingProblem>());
    }

    /// <summary>
    /// Sous-lot V2.16 (souhait de Damien du 9 novembre 2026 : « designer des choses nous-mêmes comme les personnages ») : règles pures des
    /// personnages que Damien fait lui-même dans MakeHuman (humains réalistes exportés en FBX, sans licence à craindre : CC0). Un dossier
    /// <c>Assets/Style/MakeHuman/&lt;nom&gt;/</c> par personnage ; un fichier texte <c>PNJ.txt</c> dit qui le porte (« gaspard = Gaspard.fbx »).
    /// Le choix du modèle d'un PNJ, la limite des textures et le budget de triangles sont ici ; l'import Unity est dans <c>EdsMakeHuman</c>.
    /// Affichage seulement : rien ne revient vers le moteur.
    /// </summary>
    public static class MadeHumans
    {
        /// <summary>Dossier des personnages faits à la main, dans <c>Assets/Style</c>.</summary>
        public const string Folder = "MakeHuman";

        /// <summary>Fichier de correspondance, à la racine du dossier <see cref="Folder"/>.</summary>
        public const string MappingFile = "PNJ.txt";

        /// <summary>Identifiant du héros dans le fichier de correspondance et dans le créateur de personnage.</summary>
        public const string HeroKey = "heros";

        /// <summary>Plafond des textures (pixels de côté) en mode PC modeste : 4 Go de RAM, GTX 470 (choix de Damien du 9 novembre 2026).</summary>
        public const int ModestTextureCap = 1024;

        /// <summary>Plafond des textures (pixels de côté) sur un PC ordinaire.</summary>
        public const int StandardTextureCap = 2048;

        /// <summary>Plafond des textures sur téléphone.</summary>
        public const int PhoneTextureCap = 512;

        /// <summary>Triangles conseillés par personnage (corps, vêtements et cheveux réunis) en mode PC modeste.</summary>
        public const int ModestTriangles = 15000;

        /// <summary>Triangles conseillés par personnage sur un PC ordinaire.</summary>
        public const int StandardTriangles = 30000;

        /// <summary>Au-delà de ce nombre de triangles, le personnage est refusé (il ferait ramer tout un village).</summary>
        public const int MaxTriangles = 60000;

        /// <summary>Plafond de côté des textures : 512 sur téléphone, 1024 en mode PC modeste, 2048 sinon.</summary>
        public static int TextureCap(bool modest, bool phone) => phone ? PhoneTextureCap : modest ? ModestTextureCap : StandardTextureCap;

        /// <summary>Triangles conseillés par personnage.</summary>
        public static int TriangleBudget(bool modest) => modest ? ModestTriangles : StandardTriangles;

        /// <summary>
        /// Remarque sur le poids d'un personnage de <paramref name="triangles"/> triangles : null s'il tient dans le budget, un conseil au-delà,
        /// et un refus au-delà de <see cref="MaxTriangles"/>.
        /// </summary>
        public static string? BudgetNote(string name, int triangles, bool modest)
        {
            if (triangles <= 0) return null;
            if (triangles > MaxTriangles)
                return "« " + name + " » a " + triangles + " triangles : trop lourd (au plus " + MaxTriangles + "). Dans MakeHuman, prends le proxy « Game engine » (ou « Low-poly »), et coche « Subdivide » à zéro.";
            int budget = TriangleBudget(modest);
            if (triangles > budget)
                return "« " + name + " » a " + triangles + " triangles : au-dessus des " + budget + " conseillés" + (modest ? " en mode PC modeste" : "")
                    + ". Garde un proxy léger, et retire les vêtements ou cheveux très détaillés.";
            return null;
        }

        /// <summary>Le refus est-il définitif (<see cref="BudgetNote"/> au-delà de <see cref="MaxTriangles"/>) ?</summary>
        public static bool TooHeavy(int triangles) => triangles > MaxTriangles;

        /// <summary>Le fichier est-il le FBX d'un personnage rangé dans <c>.../MakeHuman/&lt;nom&gt;/</c> (pas dans un sous-dossier de textures) ?</summary>
        public static bool IsMadeHumanFile(string? path)
        {
            if (path == null || !path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) return false;
            string[] parts = path.Replace('\\', '/').Split('/');
            return parts.Length >= 3 && StyleRules.Key(parts[parts.Length - 3]) == "makehuman" && StyleRules.Key(parts[parts.Length - 2]).Length > 0;
        }

        /// <summary>Le chemin (tout fichier : modèle, texture, texte) est-il rangé quelque part sous le dossier <see cref="Folder"/> ?</summary>
        public static bool IsUnderFolder(string? path) =>
            path != null && path.Replace('\\', '/').Split('/').Any(p => StyleRules.Key(p) == "makehuman");

        /// <summary>
        /// Les personnages trouvés parmi <paramref name="paths"/>, un par dossier, dans l'ordre alphabétique des noms. Si un dossier a
        /// plusieurs FBX, on prend celui qui porte le nom du dossier, sinon le premier dans l'ordre ordinal.
        /// </summary>
        public static IReadOnlyList<MadeHumanModel> Models(IEnumerable<string>? paths)
        {
            var byFolder = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            var written = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string path in paths ?? Array.Empty<string>())
            {
                if (!IsMadeHumanFile(path)) continue;
                string[] parts = path.Replace('\\', '/').Split('/');
                string key = StyleRules.Key(parts[parts.Length - 2]);
                if (!byFolder.TryGetValue(key, out List<string>? list)) byFolder[key] = list = new List<string>();
                list.Add(path);
                if (!written.ContainsKey(key)) written[key] = parts[parts.Length - 2];
            }
            var result = new List<MadeHumanModel>();
            foreach (KeyValuePair<string, List<string>> folder in byFolder)
            {
                List<string> files = folder.Value.OrderBy(p => p, StringComparer.Ordinal).ToList();
                string pick = files.FirstOrDefault(f => StyleRules.Key(Stem(f)) == folder.Key) ?? files[0];
                result.Add(new MadeHumanModel(folder.Key, written[folder.Key], pick));
            }
            return result;
        }

        private static string Stem(string path)
        {
            string file = path.Replace('\\', '/');
            file = file.Substring(file.LastIndexOf('/') + 1);
            int dot = file.LastIndexOf('.');
            return dot > 0 ? file.Substring(0, dot) : file;
        }

        /// <summary>Clé réduite d'un PNJ ou d'un villageois : « npc.Gaspard » → gaspard, « bourg/bavard1 » → bourgbavard1.</summary>
        public static string NpcKey(string? id)
        {
            string text = (id ?? string.Empty).Trim();
            if (text.StartsWith("npc.", StringComparison.OrdinalIgnoreCase) || text.StartsWith("pnj.", StringComparison.OrdinalIgnoreCase)) text = text.Substring(4);
            return StyleRules.Key(text);
        }

        /// <summary>Nom de modèle réduit d'un côté droit : « Gaspard/Gaspard.fbx », « Gaspard.fbx » ou « Gaspard » → gaspard.</summary>
        public static string ModelKey(string? text)
        {
            string t = (text ?? string.Empty).Trim().Trim('"', '\'').Replace('\\', '/');
            t = t.Substring(t.LastIndexOf('/') + 1);
            if (t.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) t = t.Substring(0, t.Length - 4);
            return StyleRules.Key(t);
        }

        /// <summary>
        /// Lit le fichier <c>PNJ.txt</c> : une ligne « pnj = modèle » (« gaspard = Gaspard.fbx », « npc.odile = Odile », « heros = Aldric.fbx »),
        /// les lignes vides et celles qui commencent par <c>#</c>, <c>//</c> ou <c>;</c> sont des commentaires. Une ligne illisible est
        /// signalée et ignorée, jamais fatale ; si un PNJ est écrit deux fois, la dernière ligne gagne (signalé).
        /// </summary>
        public static MadeHumanMapping ParseMapping(string? text)
        {
            var entries = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var problems = new List<MappingProblem>();
            string[] lines = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim().TrimStart('﻿');
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith("//", StringComparison.Ordinal) || line.StartsWith(";", StringComparison.Ordinal)) continue;
                int eq = line.IndexOf('=');
                if (eq < 0) { problems.Add(new MappingProblem(i + 1, "il manque le signe « = » (écris par exemple : gaspard = Gaspard.fbx).")); continue; }
                string key = NpcKey(line.Substring(0, eq)), model = ModelKey(line.Substring(eq + 1));
                if (key.Length == 0) { problems.Add(new MappingProblem(i + 1, "il manque le nom du PNJ avant le « = ».")); continue; }
                if (model.Length == 0) { problems.Add(new MappingProblem(i + 1, "il manque le nom du personnage après le « = ».")); continue; }
                if (entries.ContainsKey(key)) problems.Add(new MappingProblem(i + 1, "« " + line.Substring(0, eq).Trim() + " » est déjà écrit plus haut : cette ligne-ci l'emporte."));
                entries[key] = model;
            }
            return new MadeHumanMapping(entries, problems);
        }

        /// <summary>
        /// Personnage MakeHuman que porte <paramref name="id"/> (« npc.gaspard », « heros », « bourg/bavard1 »), ou null : le PNJ garde son corps
        /// d'avant. Ordre : la ligne du fichier de correspondance (identifiant complet, puis le prénom : « npc.gaspard_2 » → gaspard), puis
        /// un dossier qui porte le prénom du PNJ. Une ligne qui vise un personnage absent est ignorée (voir <see cref="Missing"/>).
        /// </summary>
        public static MadeHumanModel? ModelFor(MadeHumanMapping? mapping, IReadOnlyList<MadeHumanModel>? models, string? id)
        {
            if (models == null || models.Count == 0) return null;
            string full = NpcKey(id), first = StyleRules.CharacterOf(id ?? string.Empty);
            if (mapping != null)
                foreach (string key in new[] { full, first })
                    if (key.Length > 0 && mapping.Entries.TryGetValue(key, out string? wanted) && Find(models, wanted) is MadeHumanModel found) return found;
            // Dossier au nom du PNJ : pas besoin de ligne dans PNJ.txt.
            return first.Length > 0 ? models.FirstOrDefault(m => m.Name == first) : null;
        }

        private static MadeHumanModel? Find(IReadOnlyList<MadeHumanModel> models, string key) =>
            models.FirstOrDefault(m => m.Name == key) ?? models.FirstOrDefault(m => ModelKey(m.Path) == key);

        /// <summary>Lignes du fichier de correspondance qui visent un personnage absent des dossiers (pour le rapport du menu).</summary>
        public static IReadOnlyList<string> Missing(MadeHumanMapping? mapping, IReadOnlyList<MadeHumanModel>? models)
        {
            var list = new List<string>();
            if (mapping == null) return list;
            foreach (KeyValuePair<string, string> e in mapping.Entries)
                if (models == null || Find(models, e.Value) == null) list.Add(e.Key + " = " + e.Value);
            return list;
        }

        /// <summary>
        /// Une matière de MakeHuman est-elle découpée (cheveux, sourcils, cils, barbe : la texture porte la transparence, URP Lit en « Alpha Clip »),
        /// d'après son nom (« eyebrow001 », « short02 », « Eyelashes01 », « ponytail01 ») ? La peau, les yeux et les vêtements ne le sont pas.
        /// </summary>
        public static bool IsCutoutMaterial(string? name)
        {
            string k = StyleRules.Key(name ?? string.Empty);
            if (k.Contains("eyebrow") || k.Contains("eyelash") || k.Contains("lash")) return true;
            foreach (string hair in new[] { "hair", "short", "long", "bob", "ponytail", "braid", "afro", "fringe", "bun", "beard", "mustache", "moustache", "goatee", "stubble" })
                if (k.Contains(hair)) return true;
            return false;
        }

        /// <summary>Nom du fichier de licences et de crédits de chaque dossier de personnage (comme les autres vagues).</summary>
        public const string ManifestFile = "MANIFESTE.md";

        /// <summary>
        /// Licence permise dans le dépôt : CC0 (ou domaine public) et CC BY seulement (avec ou sans version : « CC-BY 4.0 », « cc by ») ;
        /// jamais NC (pas d'usage commercial), ND (pas de modification) ni SA (partage dans les mêmes conditions).
        /// </summary>
        public static bool IsAllowedLicense(string? license)
        {
            string k = new string((license ?? string.Empty).ToLowerInvariant().Where(c => char.IsLetterOrDigit(c) || c == '.').ToArray());
            return System.Text.RegularExpressions.Regex.IsMatch(k, @"^(cc0(1\.0)?|publicdomain|ccby(\d+(\.\d+)?)?)$");
        }

        /// <summary>Licence déclarée par le manifeste (ligne « Licence du personnage : CC0 »), ou null s'il n'y en a pas.</summary>
        public static string? ManifestLicense(string? manifest)
        {
            foreach (string raw in (manifest ?? string.Empty).Replace("\r", "").Split('\n'))
            {
                string line = raw.Trim().TrimStart('-', '*', ' ');
                if (!line.StartsWith("Licence du personnage", StringComparison.OrdinalIgnoreCase)) continue;
                int colon = line.IndexOf(':');
                string value = colon >= 0 ? line.Substring(colon + 1).Trim() : string.Empty;
                return value.Length == 0 ? null : value;
            }
            return null;
        }

        /// <summary>Manifeste à remplir pour le personnage <paramref name="folder"/> (écrit une fois par l'import, jamais écrasé).</summary>
        public static string ManifestTemplate(string folder) =>
            "# " + folder + " : personnage fait dans MakeHuman\n\n"
            + "- Fait par : Damien\n"
            + "- Outil : MakeHuman (les modèles exportés sont CC0 : https://static.makehumancommunity.org/oldsite/faq/can_i_sell_models_created_with_makehuman.html)\n"
            + "- Licence du personnage : CC0\n"
            + "- Squelette : Game engine\n\n"
            + "## Éléments utilisés\n\n"
            + "Une ligne par élément pris dans la bibliothèque de MakeHuman : nom, auteur, licence (CC0 ou CC BY seulement), lien de sa page.\n"
            + "Pour un élément CC BY, le nom de l'auteur est à reporter dans docs/CREDITS.md.\n\n"
            + "- Corps de base : MakeHuman, CC0\n"
            + "- Cheveux : (nom, auteur, licence, lien)\n"
            + "- Vêtements : (nom, auteur, licence, lien)\n"
            + "- Textures de peau : (nom, auteur, licence, lien)\n\n"
            + "## Remarques\n\n";

        /// <summary>Nom de fichier d'un personnage importé dans le créateur : « made.gaspard » (clé de <c>look.body</c> de la sauvegarde).</summary>
        public static string BodyKey(MadeHumanModel model) => HeroLooks.MadePrefix + model.Name;
    }
}
