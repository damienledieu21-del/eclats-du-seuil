using EdS.Runtime;
using UnityEditor;

namespace EdS.Game.Editor
{
    /// <summary>
    /// Sous-lot V2.2.1 (compagnon d'affichage) : pour l'essai du chat qui suit. <b>En mode Jeu</b>, montre Pain-d'Épice même sans l'objet au
    /// sac (affichage seulement : le sac, la quête et la sauvegarde ne changent pas) ; « ne plus forcer » le fait partir (sauf si l'objet est
    /// vraiment au sac). Hors du mode Jeu : rien à faire (le chat vient avec la quête de Rosalie).
    /// </summary>
    public static class EdsCompanionTrial
    {
        [MenuItem("Éclats du Seuil/Essai du compagnon : montrer Pain-d'Épice")]
        private static void Show()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Essai du compagnon", "Lance d'abord le jeu (Play), puis l'aventure : le chat se montre en mode Jeu.", "OK");
                return;
            }
            CompanionTrial.Forced = Companions.PainDEpice.Id;
            EditorUtility.DisplayDialog("Essai du compagnon",
                "Pain-d'Épice suit le joueur (affichage seulement). Marche, arrête-toi, attends : il s'assoit, fait sa toilette, ronronne. "
                + "Menu « ne plus forcer » pour le voir partir.", "OK");
        }

        [MenuItem("Éclats du Seuil/Essai du compagnon : ne plus forcer")]
        private static void Release()
        {
            CompanionTrial.Forced = null;
            EditorUtility.DisplayDialog("Essai du compagnon", "Le chat ne suit plus que si l'objet « Pain-d'Épice » est dans le sac.", "OK");
        }
    }
}
