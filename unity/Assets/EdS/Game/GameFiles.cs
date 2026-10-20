using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using EdS.Runtime;
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
        /// <summary>Dossier des données emportées (<see cref="BundledData.Folder"/>).</summary>
        public const string Folder = BundledData.Folder;

        /// <summary>Liste des fichiers emportés, avec l'empreinte de leur contenu (<see cref="BundledData.Index"/>).</summary>
        public const string Index = BundledData.IndexFile;

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
            string existing = null;
            Try(() => existing = File.Exists(copied) ? File.ReadAllText(copied) : null);   // illisible : la copie est refaite
            if (existing == index)
            {
                done(target, null);
                yield break;
            }
            // Une liste retouchée ne fait rien écrire hors du dossier de l'application (BundledData.ParseIndex).
            IReadOnlyList<string> files;
            try
            {
                files = BundledData.ParseIndex(index);
            }
            catch (FormatException e)
            {
                done(null, e.Message);
                yield break;
            }
            // Revue Codex de la PR #36 : la copie précédente est retirée entière (fichiers retirés ou renommés depuis compris), comme pour
            // une installation propre.
            // Revue Codex de la PR #36 (2e tour) : un stockage plein ou inaccessible est signalé (écran d'erreur), pas une coroutine arrêtée.
            string failure = Try(() =>
            {
                if (Directory.Exists(target)) Directory.Delete(target, true);
            });
            if (failure != null)
            {
                done(null, "copie des données du jeu impossible : " + failure);
                yield break;
            }
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
                failure = Try(() =>
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllBytes(path, content);
                });
                if (failure != null)
                {
                    done(null, "copie des données du jeu impossible (" + relative + ") : " + failure);
                    yield break;
                }
            }
            failure = Try(() => File.WriteAllText(copied, index));
            done(failure == null ? target : null, failure == null ? null : "copie des données du jeu impossible : " + failure);
        }

        /// <summary>Exécute une opération de fichiers ; rend null si elle a réussi, le message de l'erreur sinon.</summary>
        private static string Try(Action action)
        {
            try
            {
                action();
                return null;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is NotSupportedException || e is System.Security.SecurityException)
            {
                return e.Message;
            }
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
