using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace EdS.Editor
{
    /// <summary>
    /// Correctif du sous-lot 11.8 (premier essai sur le Galaxy Note8) : le jeu fabrique ses matériaux au démarrage
    /// (<c>ArenaView.MakeMaterial</c>), et Unity n'emporte dans un jeu construit que les shaders utilisés par un matériau ou une scène
    /// du projet : sur Android, <c>Shader.Find</c> ne trouvait plus rien, le démarrage s'arrêtait sur « Préparation des données du jeu… »
    /// et le sol restait rose (shader manquant). Avant la construction, un matériau modèle du rendu actif (URP « Lit », sinon
    /// « Standard ») est créé dans <c>Assets/EdS/Resources</c> : Unity l'emporte avec son shader, et le jeu y copie ses couleurs. Après,
    /// il est retiré (le dossier est ignoré par git), comme les données de <see cref="EdsBuildData"/>.
    /// </summary>
    public sealed class EdsBuildShaders : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        /// <summary>Nom du modèle, chargé par <c>ArenaView.MakeMaterial</c> (<c>Resources.Load</c>).</summary>
        public const string TemplateName = "EdsCouleur";

        private const string Folder = "Assets/EdS/Resources";

        private static string TemplatePath => Folder + "/" + TemplateName + ".mat";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            Clean();
            bool urp = GraphicsSettings.defaultRenderPipeline != null;
            Shader shader = urp ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard");
            if (shader == null)
                throw new BuildFailedException("Éclats du Seuil : shader de couleur introuvable (" + (urp ? "URP Lit" : "Standard")
                    + ") ; menu Éclats du Seuil → Configurer le rendu URP, puis reconstruire.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/EdS", "Resources");
            AssetDatabase.CreateAsset(new Material(shader), TemplatePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Éclats du Seuil : matériau modèle « " + shader.name + " » emporté dans le jeu construit.");
        }

        public void OnPostprocessBuild(BuildReport report) => Clean();

        private static void Clean()
        {
            if (AssetDatabase.IsValidFolder(Folder)) AssetDatabase.DeleteAsset(Folder);
            string full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", Folder));
            if (Directory.Exists(full)) Directory.Delete(full, true);
            if (File.Exists(full + ".meta")) File.Delete(full + ".meta");
            AssetDatabase.Refresh();
        }
    }
}
