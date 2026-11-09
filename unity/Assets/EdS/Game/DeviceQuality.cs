using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V2.12.4 (essai de Damien du 9 novembre 2026 : « System out of memory » sur sa GTX 470 à 1224 Mo) : classe de la machine
    /// (<see cref="GraphicsTier"/>) d'après la mémoire vidéo et la mémoire système, et limite des textures (mipmaps) appliquée au démarrage
    /// selon le choix du joueur (réglages de la pause, sauvegardé ; Auto par défaut). Sur un PC modeste, les ressources « jamais sur
    /// téléphone » sont écartées comme sur téléphone (<see cref="Light"/>). Affichage seulement.
    /// </summary>
    public static class DeviceQuality
    {
        private const string ChoiceKey = "eds.textures";
        private static bool _known;
        private static DeviceTier _tier;

        /// <summary>Mémoire vidéo (Mo) rapportée par Unity.</summary>
        public static int Vram => SystemInfo.graphicsMemorySize;

        /// <summary>Mémoire système (Mo) rapportée par Unity.</summary>
        public static int Ram => SystemInfo.systemMemorySize;

        /// <summary>Classe de cette machine (calculée une fois).</summary>
        public static DeviceTier Tier
        {
            get
            {
                if (!_known)
                {
                    _tier = GraphicsTier.Detect(Vram, Ram, Application.isMobilePlatform);
                    _known = true;
                }
                return _tier;
            }
        }

        /// <summary>Téléphone ou PC modeste : les ressources « jamais sur téléphone » ne se montrent pas.</summary>
        public static bool Light => GraphicsTier.Light(Tier);

        /// <summary>Choix du joueur (préférences ; Auto par défaut).</summary>
        public static TextureChoice Choice => GraphicsTier.Choice(PlayerPrefs.GetInt(ChoiceKey, 0));

        /// <summary>Limite de mipmaps qui correspond au choix du joueur sur cette machine.</summary>
        public static int Limit => GraphicsTier.MipLimit(Choice, Vram, Ram, Application.isMobilePlatform);

        /// <summary>Applique la limite des textures (au démarrage et à chaque changement de réglage).</summary>
        public static void Apply() => QualitySettings.globalTextureMipmapLimit = Limit;

        /// <summary>Passe au choix suivant (auto, pleine, moitié, quart), le sauvegarde et l'applique.</summary>
        public static void Cycle()
        {
            PlayerPrefs.SetInt(ChoiceKey, (int)GraphicsTier.Next(Choice));
            PlayerPrefs.Save();
            Apply();
        }

        /// <summary>Texte du bouton des réglages.</summary>
        public static string Label => GraphicsTier.Label(Choice, Limit);
    }
}
