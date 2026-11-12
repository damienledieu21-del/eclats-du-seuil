using UnityEditor;

namespace EdS.Game.Editor
{
    /// <summary>
    /// V2.25.4 (arbres de l'atelier) : essai des saisons. <b>En mode Jeu</b>, force la saison du feuillage des arbres (atlas de la saison,
    /// imposteurs, feuilles au sol en automne) sans rien changer à la partie ; « saison du monde » rend celle du calendrier (28 jours par
    /// saison). Hors du mode Jeu : rien (lancer le jeu d'abord).
    /// </summary>
    public static class EdsSeasonTrial
    {
        [MenuItem("Éclats du Seuil/Essai des saisons : printemps")]
        private static void Spring() => Force(0, "printemps");

        [MenuItem("Éclats du Seuil/Essai des saisons : été")]
        private static void Summer() => Force(1, "été");

        [MenuItem("Éclats du Seuil/Essai des saisons : automne")]
        private static void Autumn() => Force(2, "automne");

        [MenuItem("Éclats du Seuil/Essai des saisons : hiver")]
        private static void Winter() => Force(3, "hiver");

        [MenuItem("Éclats du Seuil/Essai des saisons : saison du monde (ne plus forcer)")]
        private static void World() => Force(-1, "saison du monde");

        private static void Force(int season, string name)
        {
            if (!EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Essai des saisons", "Lance d'abord le mode Jeu, puis choisis la saison.", "OK");
                return;
            }
            SpaceView.ForcedSeason = season;
            EditorUtility.DisplayDialog("Essai des saisons", season < 0 ? "Les arbres suivent de nouveau la saison du monde."
                : "Arbres en " + name + " (affichage seulement). Menu « saison du monde » pour revenir.", "OK");
        }
    }
}
