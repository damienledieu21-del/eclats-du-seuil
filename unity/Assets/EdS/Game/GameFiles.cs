using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot 11.8 : où le jeu trouve ses données (<c>data/v0</c>) et ses scénarios (<c>scenarios</c>). Dans l'éditeur et sur PC : le
    /// dépôt, comme avant. Un jeu construit les emporte aussi dans <c>StreamingAssets/eds</c> (copiés à la construction par
    /// <c>EdsBuildData</c>, avec la liste des fichiers) : sur PC, ils servent si le dépôt n'est pas trouvé ; sur Android, ils sont rangés
    /// dans l'APK, illisibles comme des fichiers ordinaires : ils sont recopiés au lancement dans le dossier de l'application
    /// (<see cref="Application.persistentDataPath"/>), puis lus de là. Rien n'est téléchargé depuis le réseau.
    /// </summary>
    public static class GameFiles
    {
        /// <summary>Dossier des données emportées, sous <c>StreamingAssets</c> (et sous le dossier de l'application sur Android).</summary>
        public const string Folder = "eds";

        /// <summary>
        /// Liste des fichiers emportés : une ligne <c># empreinte …</c> (contenu de tous les fichiers), puis les chemins relatifs, un par
        /// ligne, séparateur « / ».
        /// </summary>
        public const string Index = "fichiers.txt";

        /// <summary>
        /// Prépare la racine des données et la rend à <paramref name="done"/> (racine, erreur éventuelle). À lancer en coroutine : sur
        /// Android, la copie lit l'APK fichier par fichier.
        /// </summary>
        public static IEnumerator Prepare(string repoRoot, Action<string, string> done)
        {
            string bundled = Path.Combine(Application.streamingAssetsPath, Folder);
            if (Application.platform != RuntimePlatform.Android)
            {
                bool repo = Directory.Exists(Path.Combine(repoRoot, "data", "v0")) && Directory.Exists(Path.Combine(repoRoot, "scenarios"));
                done(!repo && Directory.Exists(Path.Combine(bundled, "data", "v0")) ? bundled : repoRoot, null);
                yield break;
            }
            string target = Path.Combine(Application.persistentDataPath, Folder);
            string index = null, error = null;
            yield return Read(bundled + "/" + Index, bytes => index = System.Text.Encoding.UTF8.GetString(bytes), e => error = e);
            if (index == null)
            {
                done(null, "liste des données absente du jeu construit (" + error + ") : reconstruire le jeu depuis l'éditeur.");
                yield break;
            }
            // Déjà copiées pour ce jeu (même liste, empreinte du contenu comprise : revue Codex de la PR #36) : rien à refaire. La liste est
            // écrite en dernier, après tous les fichiers.
            string copied = Path.Combine(target, Index);
            if (File.Exists(copied) && File.ReadAllText(copied) == index)
            {
                done(target, null);
                yield break;
            }
            var files = new List<string>();
            foreach (string line in index.Split('\n'))
            {
                string relative = line.Trim();
                if (relative.Length == 0 || relative.StartsWith("#", StringComparison.Ordinal)) continue;
                // Une liste retouchée ne fait rien écrire hors du dossier de l'application.
                if (relative.Contains("..") || Path.IsPathRooted(relative))
                {
                    done(null, "liste des données invalide : " + relative);
                    yield break;
                }
                files.Add(relative);
            }
            // Revue Codex de la PR #36 : la copie précédente est retirée entière (fichiers retirés ou renommés depuis compris), comme pour
            // une installation propre.
            if (Directory.Exists(target)) Directory.Delete(target, true);
            foreach (string relative in files)
            {
                byte[] content = null;
                yield return Read(bundled + "/" + relative, bytes => content = bytes, e => error = e);
                if (content == null)
                {
                    done(null, "donnée illisible dans le jeu construit : " + relative + " (" + error + ").");
                    yield break;
                }
                string path = Path.Combine(target, relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, content);
            }
            File.WriteAllText(copied, index);
            done(target, null);
        }

        private static IEnumerator Read(string url, Action<byte[]> ok, Action<string> failed)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success) ok(request.downloadHandler.data);
                else failed(request.error);
            }
        }
    }
}
