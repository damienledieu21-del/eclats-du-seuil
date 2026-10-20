using System.IO;
using EdS.Runtime;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace EdS.Editor
{
    /// <summary>
    /// Sous-lot 11.8 : un jeu construit (téléphone Android, ou PC sans le dépôt à côté) emporte les données (<c>data/v0</c>) et les
    /// scénarios (<c>scenarios</c>) du dépôt. Avant la construction, ils sont copiés dans <c>Assets/StreamingAssets/eds</c> avec la liste de
    /// leurs fichiers et l'empreinte de leur contenu (<see cref="BundledData.CopyTo"/>, lue par <c>GameFiles</c>) ; après, la copie est
    /// retirée (le dossier est ignoré par git). Revue Codex de la PR #36 : la collecte, l'empreinte et la copie sont dans
    /// <see cref="BundledData"/>, exercées par les tests.
    /// </summary>
    public sealed class EdsBuildData : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        private static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        private static string Target => Path.Combine(Application.dataPath, "StreamingAssets", BundledData.Folder);

        public void OnPreprocessBuild(BuildReport report)
        {
            Clean();
            int count;
            try
            {
                count = BundledData.CopyTo(Repo, Target).Count;
            }
            catch (DirectoryNotFoundException e)
            {
                throw new BuildFailedException(e.Message + " (le projet Unity doit rester dans le dossier unity/ du dépôt).");
            }
            AssetDatabase.Refresh();
            Debug.Log($"Éclats du Seuil : {count} fichiers de données emportés dans le jeu construit.");
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
