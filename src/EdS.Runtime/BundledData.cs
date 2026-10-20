using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace EdS.Runtime
{
    /// <summary>
    /// Sous-lot 11.8 : données emportées par un jeu construit (téléphone Android, ou PC sans le dépôt à côté). Avant la construction, l'éditeur
    /// Unity copie les <c>.json</c> de <c>data/v0</c> et de <c>scenarios</c> dans <c>StreamingAssets/eds</c>, avec leur liste
    /// (<see cref="IndexFile"/>) ; sur Android, le jeu les recopie au lancement dans son dossier, en se fiant à cette liste. Revue Codex de la
    /// PR #36 : la logique est ici, sans Unity, pour être exercée par les tests (collecte, empreinte, copie, lecture de la liste).
    /// </summary>
    public static class BundledData
    {
        /// <summary>Dossier des données emportées, sous <c>StreamingAssets</c> (et sous le dossier de l'application sur Android).</summary>
        public const string Folder = "eds";

        /// <summary>Liste des fichiers emportés : une ligne <c># empreinte …</c>, puis les chemins relatifs (séparateur « / »), un par ligne.</summary>
        public const string IndexFile = "fichiers.txt";

        /// <summary>Dossiers du dépôt emportés (chemins relatifs, séparateur « / »).</summary>
        public static readonly IReadOnlyList<string> Sources = new[] { "data/v0", "scenarios" };

        private const string FingerprintPrefix = "# empreinte ";

        /// <summary>
        /// Fichiers à emporter : tous les <c>.json</c> des <see cref="Sources"/> du dépôt <paramref name="repoRoot"/>, en chemins relatifs
        /// (« / »), dans l'ordre ordinal. Un dossier source absent est une erreur (<see cref="DirectoryNotFoundException"/>).
        /// </summary>
        public static IReadOnlyList<string> Collect(string repoRoot)
        {
            if (repoRoot is null) throw new ArgumentNullException(nameof(repoRoot));
            string root = Path.GetFullPath(repoRoot);
            var files = new List<string>();
            foreach (string source in Sources)
            {
                string folder = Path.Combine(root, source.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(folder)) throw new DirectoryNotFoundException("Données du jeu introuvables : " + folder + ".");
                foreach (string file in Directory.GetFiles(folder, "*.json", SearchOption.AllDirectories))
                    files.Add(file.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace('\\', '/'));
            }
            files.Sort(StringComparer.Ordinal);
            return files;
        }

        /// <summary>
        /// Liste des fichiers <paramref name="files"/> (dans cet ordre), précédée de l'empreinte de leur contenu : SHA-256 (hexadécimal) de
        /// chaque chemin, de sa taille et de ses octets (<paramref name="read"/>). Même contenu, même liste ; un octet changé change la liste.
        /// </summary>
        public static string Index(IReadOnlyList<string> files, Func<string, byte[]> read)
        {
            if (files is null) throw new ArgumentNullException(nameof(files));
            if (read is null) throw new ArgumentNullException(nameof(read));
            using (SHA256 sha = SHA256.Create())
            {
                foreach (string relative in files)
                {
                    byte[] content = read(relative);
                    byte[] head = Encoding.UTF8.GetBytes(relative + "\n" + content.Length.ToString(CultureInfo.InvariantCulture) + "\n");
                    sha.TransformBlock(head, 0, head.Length, null, 0);
                    sha.TransformBlock(content, 0, content.Length, null, 0);
                }
                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                string fingerprint = BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant();
                return FingerprintPrefix + fingerprint + "\n" + string.Concat(files.Select(f => f + "\n"));
            }
        }

        /// <summary>
        /// Chemins relatifs d'une liste (<see cref="Index"/>) : lignes vides et lignes <c>#</c> sautées. Une liste qui ferait écrire hors du
        /// dossier de l'application (<c>..</c>, chemin absolu) est refusée (<see cref="FormatException"/>).
        /// </summary>
        public static IReadOnlyList<string> ParseIndex(string index)
        {
            if (index is null) throw new ArgumentNullException(nameof(index));
            var files = new List<string>();
            foreach (string line in index.Split('\n'))
            {
                string relative = line.Trim();
                if (relative.Length == 0 || relative.StartsWith("#", StringComparison.Ordinal)) continue;
                if (relative.Contains("..") || Path.IsPathRooted(relative) || relative.StartsWith("/", StringComparison.Ordinal) || relative.Contains("\\"))
                    throw new FormatException("liste des données invalide : " + relative);
                files.Add(relative);
            }
            return files;
        }

        /// <summary>
        /// Copie de construction : retire <paramref name="target"/>, y copie les fichiers de <see cref="Collect"/> (mêmes chemins relatifs),
        /// puis écrit la liste (<see cref="IndexFile"/>) en dernier. Rend les fichiers copiés.
        /// </summary>
        public static IReadOnlyList<string> CopyTo(string repoRoot, string target)
        {
            if (target is null) throw new ArgumentNullException(nameof(target));
            IReadOnlyList<string> files = Collect(repoRoot);
            string root = Path.GetFullPath(repoRoot);
            if (Directory.Exists(target)) Directory.Delete(target, true);
            foreach (string relative in files)
            {
                string destination = Path.Combine(target, relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)), destination, true);
            }
            string index = Index(files, relative => File.ReadAllBytes(Path.Combine(target, relative.Replace('/', Path.DirectorySeparatorChar))));
            File.WriteAllText(Path.Combine(target, IndexFile), index, new UTF8Encoding(false));
            return files;
        }
    }
}
