using System;
using System.Text.RegularExpressions;

namespace EdS.Data.Saves
{
    /// <summary>
    /// Sous-lot V2.16 (sauvegarde v17, créateur de personnage) : l'aspect que le joueur a choisi pour son héros. Affichage seulement : rien
    /// n'en dépend dans le moteur. Les indices suivent les tables de la couche d'affichage (<c>EdS.Runtime.HeroLooks</c>, dont les tests vérifient
    /// l'accord avec les bornes ci-dessous). Facultatif dans le personnage : absent tant que le joueur n'a rien choisi (le héros garde l'aspect
    /// par défaut du jeu).
    /// </summary>
    public sealed class SavedLook
    {
        /// <summary>Corps des Universal Base Characters : homme.</summary>
        public const string Male = "male";

        /// <summary>Corps des Universal Base Characters : femme.</summary>
        public const string Female = "female";

        /// <summary>Préfixe d'un personnage fait dans MakeHuman (« made.gaspard » : dossier <c>Assets/Style/MakeHuman/Gaspard</c>).</summary>
        public const string MadePrefix = "made.";

        /// <summary>Plus grande coiffure (HumanHair : 0 aucune, 1 à 5 les coiffures de Quaternius).</summary>
        public const int MaxHair = 5;

        /// <summary>Plus grand ton de cheveux (HumanLooks.HairTones : six couleurs).</summary>
        public const int MaxHairTone = 5;

        /// <summary>Plus grande tenue peinte (HumanLooks.Palettes : treize tenues).</summary>
        public const int MaxPalette = 12;

        /// <summary>Longueur au plus du nom du corps.</summary>
        public const int MaxBodyLength = 48;

        private static readonly Regex MadeKey = new Regex(@"^made\.[a-z0-9]{1,40}\z", RegexOptions.CultureInvariant);

        /// <summary>Corps bien formé : « male », « female » ou « made.&lt;nom réduit&gt; ».</summary>
        public static bool IsBody(string? body) => body == Male || body == Female || (body != null && body.Length <= MaxBodyLength && MadeKey.IsMatch(body));

        public string Body { get; }
        public int Hair { get; }
        public bool Beard { get; }
        public bool DarkSkin { get; }
        public int HairTone { get; }
        public int Palette { get; }

        public SavedLook(string body, int hair, bool beard, bool darkSkin, int hairTone, int palette)
        {
            if (!IsBody(body)) throw new ArgumentException($"Corps « {body} » hors du format des sauvegardes (male, female ou made.<nom>).", nameof(body));
            if (hair < 0 || hair > MaxHair) throw new ArgumentOutOfRangeException(nameof(hair), hair, $"Coiffure hors de [0, {MaxHair}].");
            if (hairTone < 0 || hairTone > MaxHairTone) throw new ArgumentOutOfRangeException(nameof(hairTone), hairTone, $"Couleur de cheveux hors de [0, {MaxHairTone}].");
            if (palette < 0 || palette > MaxPalette) throw new ArgumentOutOfRangeException(nameof(palette), palette, $"Tenue hors de [0, {MaxPalette}].");
            Body = body;
            Hair = hair;
            Beard = beard;
            DarkSkin = darkSkin;
            HairTone = hairTone;
            Palette = palette;
        }

        public override bool Equals(object? obj) => obj is SavedLook o && o.Body == Body && o.Hair == Hair && o.Beard == Beard && o.DarkSkin == DarkSkin
            && o.HairTone == HairTone && o.Palette == Palette;

        public override int GetHashCode() => ((((Body.GetHashCode() * 31 + Hair) * 31 + (Beard ? 1 : 0)) * 31 + (DarkSkin ? 1 : 0)) * 31 + HairTone) * 31 + Palette;
    }

    /// <summary>Sous-lot V2.16 (version 17) : l'aspect du héros tel que lu, avant validation.</summary>
    public sealed class SavedLookData
    {
        public string? Body { get; set; }
        public int? Hair { get; set; }
        public bool? Beard { get; set; }
        public bool? Dark { get; set; }
        public int? HairTone { get; set; }
        public int? Palette { get; set; }
    }
}
