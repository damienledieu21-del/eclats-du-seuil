using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace EdS.Editor
{
    /// <summary>
    /// Sous-lot 11.8 : un jeu construit (téléphone Android, ou PC sans le dépôt à côté) emporte les données (<c>data/v0</c>) et les
    /// scénarios (<c>scenarios</c>) du dépôt. Avant la construction, ils sont copiés dans <c>Assets/StreamingAssets/eds</c> avec la liste de
    /// leurs fichiers (<c>fichiers.txt</c>, lue par <c>GameFiles</c>) ; après, la copie est retirée (le dossier est ignoré par git). La
    /// liste commence par l'empreinte du contenu (<c># empreinte</c>, SHA-256 des chemins et des octets) : une nouvelle construction aux
    /// mêmes chemins mais aux données changées fait recopier les fichiers sur Android (revue Codex de la PR #36).
    /// </summary>
    public sealed class EdsBuildData : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        private static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        private static string Target => Path.Combine(Application.dataPath, "StreamingAssets", "eds");

        public void OnPreprocessBuild(BuildReport report)
        {
            Clean();
            var files = new List<string>();
            foreach (string folder in new[] { Path.Combine("data", "v0"), "scenarios" })
            {
                string source = Path.Combine(Repo, folder);
                if (!Directory.Exists(source)) throw new BuildFailedException("Données du jeu introuvables : " + source + " (le projet Unity doit rester dans le dossier unity/ du dépôt).");
                foreach (string file in Directory.GetFiles(source, "*.json", SearchOption.AllDirectories).OrderBy(f => f, System.StringComparer.Ordinal))
                {
                    string relative = file.Substring(Repo.Length + 1).Replace(Path.DirectorySeparatorChar, '/');
                    string destination = Path.Combine(Target, relative.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.Copy(file, destination, true);
                    files.Add(relative);
                }
            }
            File.WriteAllText(Path.Combine(Target, "fichiers.txt"), "# empreinte " + Fingerprint(files) + "\n" + string.Join("\n", files) + "\n", new UTF8Encoding(false));
            AssetDatabase.Refresh();
            Debug.Log($"Éclats du Seuil : {files.Count} fichiers de données emportés dans le jeu construit.");
        }

        /// <summary>SHA-256 (hexadécimal) des fichiers copiés : pour chacun, son chemin, sa taille et ses octets, dans l'ordre de la liste.</summary>
        private static string Fingerprint(IEnumerable<string> files)
        {
            using (SHA256 sha = SHA256.Create())
            {
                foreach (string relative in files)
                {
                    byte[] content = File.ReadAllBytes(Path.Combine(Target, relative.Replace('/', Path.DirectorySeparatorChar)));
                    byte[] head = Encoding.UTF8.GetBytes(relative + "\n" + content.Length.ToString(CultureInfo.InvariantCulture) + "\n");
                    sha.TransformBlock(head, 0, head.Length, null, 0);
                    sha.TransformBlock(content, 0, content.Length, null, 0);
                }
                sha.TransformFinalBlock(new byte[0], 0, 0);
                return BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant();
            }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            Clean();
            AssetDatabase.Refresh();
        }

        private static void Clean()
        {
            if (Directory.Exists(Target)) Directory.Delete(Target, true);
            string meta = Target + ".meta";
            if (File.Exists(meta)) File.Delete(meta);
        }
    }
}
