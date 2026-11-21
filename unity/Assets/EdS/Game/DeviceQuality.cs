using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V2.12.4 (essai de Damien du 9 novembre 2026 : « System out of memory » sur sa GTX 470 à 1224 Mo) : classe de la machine
    /// (<see cref="MachineTier"/>) d'après la mémoire vidéo et la mémoire système, et limite des textures (mipmaps). Sur un PC modeste, les
    /// ressources « jamais sur téléphone » sont écartées comme sur téléphone (<see cref="Light"/>). Affichage seulement.
    /// Demande de Damien du 21 novembre 2026 : réglage « Qualité » (Automatique, Basse, Moyenne, Haute ; Automatique par défaut,
    /// <c>PlayerPrefs</c> <c>eds.qualite</c>) qui force le niveau à la place de la détection. Les textures changent tout de suite ; la
    /// classe (chargement par cases, brume, images) et les ressources lourdes sont lues une fois au lancement et changent au prochain.
    /// </summary>
    public static class DeviceQuality
    {
        private const string ChoiceKey = "eds.qualite";
        private static bool _known;
        private static QualityLevel _session;

        /// <summary>Mémoire vidéo (Mo) rapportée par Unity.</summary>
        public static int Vram => SystemInfo.graphicsMemorySize;

        /// <summary>Mémoire système (Mo) rapportée par Unity.</summary>
        public static int Ram => SystemInfo.systemMemorySize;

        /// <summary>Classe détectée de cette machine (sans le réglage du joueur).</summary>
        public static DeviceTier Detected => MachineTier.Detect(Vram, Ram, Application.isMobilePlatform);

        /// <summary>Réglage du joueur (préférences ; Automatique par défaut).</summary>
        public static QualityChoice Choice => MachineTier.Choice(PlayerPrefs.GetInt(ChoiceKey, 0));

        /// <summary>Niveau qui correspond au réglage actuel sur cette machine.</summary>
        public static QualityLevel Wanted => MachineTier.Level(Choice, Vram, Ram, Application.isMobilePlatform);

        /// <summary>Niveau de cette partie : figé à la première lecture (au lancement) ; un changement de réglage compte au prochain lancement.</summary>
        public static QualityLevel Session
        {
            get
            {
                if (!_known)
                {
                    _session = Wanted;
                    _known = true;
                }
                return _session;
            }
        }

        /// <summary>Classe dont les réglages s'appliquent dans cette partie (détectée, ou forcée par le réglage au lancement).</summary>
        public static DeviceTier Tier => Session.Tier;

        /// <summary>Ressources « jamais sur téléphone » écartées dans cette partie (téléphone, PC modeste, Basse ou Moyenne).</summary>
        public static bool Light => Session.Light;

        /// <summary>Limite de mipmaps du réglage actuel (appliquée tout de suite).</summary>
        public static int Limit => Wanted.MipLimit;

        /// <summary>Haute forcée sur un téléphone ou un PC modeste.</summary>
        public static bool Risky => MachineTier.Risky(Choice, Vram, Ram, Application.isMobilePlatform);

        /// <summary>Applique la limite des textures (au démarrage et à chaque changement de réglage).</summary>
        public static void Apply()
        {
            _ = Session;   // fige le niveau de la partie avant tout changement de réglage
            QualitySettings.globalTextureMipmapLimit = Limit;
        }

        /// <summary>Passe au réglage suivant (automatique, basse, moyenne, haute), le sauvegarde et applique les textures.</summary>
        public static void Cycle()
        {
            _ = Session;   // fige le niveau de la partie avant le changement
            PlayerPrefs.SetInt(ChoiceKey, (int)MachineTier.Next(Choice));
            PlayerPrefs.Save();
            Apply();
        }

        /// <summary>Texte du bouton des réglages (dit « au prochain lancement » et l'avertissement quand il le faut).</summary>
        public static string Label => MachineTier.Label(Choice, Wanted, Session, Risky);
    }
}
