using System.IO;
using EdS.Data.Json;
using EdS.Data.Story;

namespace EdS.Game
{
    /// <summary>
    /// Interface V2.1 : textes de l'interface lus dans les données (<c>data/v0/world/texts/fr.json</c>, clés <c>text.ui.hud.*</c>), une fois au
    /// lancement, pour tous les modes (entraînement, réseau, aventure). Sans données lisibles, la clé s'affiche (jamais d'exception à
    /// l'écran) ; les tests portables exigent que toutes les clés existent.
    /// </summary>
    public static class UiTexts
    {
        private static TextCatalog _texts;

        /// <summary>Charge les textes du dossier de données <paramref name="dataDirectory"/> (data/v0) ; garde les précédents si la lecture échoue.</summary>
        public static void Load(string dataDirectory)
        {
            if (string.IsNullOrEmpty(dataDirectory)) return;
            string path = StoryJsonReader.TextsPathIn(dataDirectory);
            if (!File.Exists(path)) return;
            TextsLoadResult loaded = StoryJsonReader.ReadTextsFile(path);
            if (loaded.Texts != null) _texts = loaded.Texts;
        }

        /// <summary>Texte de la clé, ou la clé elle-même.</summary>
        public static string Get(string key) => key == null ? "" : _texts != null ? _texts.Get(key) : key;
    }
}
