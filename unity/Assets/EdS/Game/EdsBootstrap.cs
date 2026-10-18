using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot 9.3 : point d'entrée du prototype. Au lancement (bouton Play), crée l'objet de jeu dans la scène ouverte, quelle qu'elle
    /// soit : aucune scène n'est à préparer à la main, l'arène est construite par le code.
    /// </summary>
    public static class EdsBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Start()
        {
            if (Object.FindFirstObjectByType<EdsGame>() != null) return;
            var root = new GameObject("Éclats du Seuil");
            root.AddComponent<EdsGame>();
        }
    }
}
