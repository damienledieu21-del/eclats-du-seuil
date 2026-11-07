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
    /// <para>
    /// Essai réaliste 6 : les polices de l'interface sont créées au lancement avec le shader « TextMeshPro/Mobile/Distance Field » ; un
    /// second matériau modèle (<see cref="TextName"/>) l'emporte aussi, quand les ressources de TextMeshPro sont dans le projet. Les annonces
    /// d'attaque au sol et les anneaux de la cible ont leur shader (« EdS/Annonce ») : un troisième matériau (<see cref="AnnonceName"/>),
    /// que le jeu copie.
    /// </para>
    /// <para>Essai réaliste 7 : les effets en particules ont le leur (« EdS/Particule ») : un quatrième matériau (<see cref="ParticuleName"/>).</para>
    /// </summary>
    public sealed class EdsBuildShaders : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        /// <summary>Nom du modèle, chargé par <c>ArenaView.MakeMaterial</c> (<c>Resources.Load</c>).</summary>
        public const string TemplateName = "EdsCouleur";

        /// <summary>Matériau qui emporte le shader du texte de l'interface (TextMeshPro).</summary>
        public const string TextName = "EdsTexte";

        private const string TextShader = "TextMeshPro/Mobile/Distance Field";

        /// <summary>Matériau des annonces au sol, chargé par le jeu (nom partagé avec <c>Annonces.MaterialName</c>).</summary>
        public const string AnnonceName = "EdsAnnonce";

        /// <summary>Essai réaliste 7 : matériau des particules, chargé par le jeu (nom partagé avec <c>Effects.MaterialName</c>).</summary>
        public const string ParticuleName = "EdsParticule";

        /// <summary>Sous-lot V2.9.1 : matériau des échos fantômes (nom partagé avec <c>EchoGhosts.MaterialName</c>).</summary>
        public const string EchoName = "EdsEcho";

        /// <summary>Sous-lot V2.9.1 : matériau des déchirures des failles (nom partagé avec <c>WorldEventsView.TearMaterialName</c>).</summary>
        public const string FailleName = "EdsFaille";

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
            if (urp)
            {
                // V1.10 : les décors qui cachent le joueur passent en transparence (SpaceView.PrepareFade) : un modèle transparent garde la
                // variante « _SURFACE_TYPE_TRANSPARENT » du shader dans le jeu construit (sinon retirée, aucun matériau ne l'employant).
                var faded = new Material(shader);
                faded.SetFloat("_Surface", 1f);
                faded.SetFloat("_ZWrite", 0f);
                faded.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                faded.SetOverrideTag("RenderType", "Transparent");
                faded.renderQueue = 3000;
                AssetDatabase.CreateAsset(faded, Folder + "/" + TemplateName + "Fondu.mat");
            }
            Shader text = Shader.Find(TextShader);
            if (text != null)
            {
                // Relecture de l'essai 6 : l'ombre (UNDERLAY_ON) et le contour (OUTLINE_ON) sont des variantes optionnelles du shader, retirées
                // de la construction si aucun matériau ne les emploie : un modèle par combinaison utilisée par l'interface.
                var shadow = new Material(text);
                shadow.EnableKeyword("UNDERLAY_ON");
                AssetDatabase.CreateAsset(shadow, Folder + "/" + TextName + ".mat");
                var outlined = new Material(text);
                outlined.EnableKeyword("UNDERLAY_ON");
                outlined.EnableKeyword("OUTLINE_ON");
                AssetDatabase.CreateAsset(outlined, Folder + "/" + TextName + "Contour.mat");
            }
            else Debug.LogWarning("Éclats du Seuil : shader « " + TextShader + " » absent (menu Éclats du Seuil → Préparer l'interface) ; le jeu construit "
                + "affichera le texte ordinaire.");
            Shader annonce = Shader.Find("EdS/Annonce");
            if (annonce != null) AssetDatabase.CreateAsset(new Material(annonce), Folder + "/" + AnnonceName + ".mat");
            else Debug.LogWarning("Éclats du Seuil : shader « EdS/Annonce » absent ; le jeu construit dessinera les annonces en formes pleines.");
            // Essai réaliste 7 : effets en particules (impacts, parades, sorts) ; le jeu copie ce matériau pour chaque texture.
            Shader particule = Shader.Find("EdS/Particule");
            if (particule != null) AssetDatabase.CreateAsset(new Material(particule), Folder + "/" + ParticuleName + ".mat");
            else Debug.LogWarning("Éclats du Seuil : shader « EdS/Particule » absent ; le jeu construit n'aura pas d'effets en particules.");
            // Sous-lot V2.9.1 : échos fantômes et déchirures des failles ; sans eux, échos pâles (V2.1) et failles en formes simples (V2.5).
            Shader echo = Shader.Find("EdS/Echo");
            if (echo != null) AssetDatabase.CreateAsset(new Material(echo), Folder + "/" + EchoName + ".mat");
            else Debug.LogWarning("Éclats du Seuil : shader « EdS/Echo » absent ; le jeu construit dessinera les échos pâles.");
            Shader faille = Shader.Find("EdS/Faille");
            if (faille != null) AssetDatabase.CreateAsset(new Material(faille), Folder + "/" + FailleName + ".mat");
            else Debug.LogWarning("Éclats du Seuil : shader « EdS/Faille » absent ; le jeu construit dessinera les failles en formes simples.");
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
