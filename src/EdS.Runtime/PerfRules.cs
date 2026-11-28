using System;
using System.Globalization;

namespace EdS.Runtime
{
    /// <summary>
    /// V2.53 (audit des performances, docs/PERFS.md) : réglages du rendu URP d'une classe de machine. Le PC complet garde ceux de
    /// l'essai réaliste (90 m, 4 cascades, carte 4096, ombres douces, MSAA ×4, occlusion de l'écran) ; le PC modeste (GTX 470 de
    /// Damien, ou réglage « Basse ») reçoit un rendu à lui ; le téléphone garde le sien (35 m, 2 cascades). Affichage seulement.
    /// </summary>
    public readonly struct RenderProfile
    {
        public RenderProfile(float shadowDistance, int cascades, int shadowResolution, bool softShadows, int msaa, int lightsPerObject, bool ambientOcclusion)
        {
            ShadowDistance = shadowDistance;
            Cascades = cascades;
            ShadowResolution = shadowResolution;
            SoftShadows = softShadows;
            Msaa = msaa;
            LightsPerObject = lightsPerObject;
            AmbientOcclusion = ambientOcclusion;
        }

        /// <summary>Distance (m) jusqu'où le soleil projette des ombres.</summary>
        public float ShadowDistance { get; }

        /// <summary>Cascades de la carte d'ombres du soleil (chaque cascade redessine les objets qui projettent une ombre).</summary>
        public int Cascades { get; }

        /// <summary>Côté (pixels) de la carte d'ombres du soleil.</summary>
        public int ShadowResolution { get; }

        /// <summary>Ombres douces (filtrage de plusieurs points par pixel).</summary>
        public bool SoftShadows { get; }

        /// <summary>Lissage des bords (1, 2, 4 ou 8 échantillons).</summary>
        public int Msaa { get; }

        /// <summary>Lumières d'appoint par objet (8 : intérieurs, essai 10).</summary>
        public int LightsPerObject { get; }

        /// <summary>Occlusion ambiante de l'écran (renderer avec la fonction SSAO).</summary>
        public bool AmbientOcclusion { get; }
    }

    /// <summary>V2.53 (audit des performances) : règles de calcul pures de l'allègement du rendu, sans Unity, testées.</summary>
    public static class PerfRules
    {
        /// <summary>
        /// Rendu d'une classe de machine. PC modeste : ombres sur 50 m en deux cascades (carte 2048 : même finesse près du joueur que
        /// 4096 partagée en quatre), dures, MSAA ×2, sans occlusion de l'écran ; huit lumières par objet gardées (les intérieurs
        /// restent éclairés comme sur le PC complet).
        /// </summary>
        public static RenderProfile Render(DeviceTier tier) => tier switch
        {
            DeviceTier.Phone => new RenderProfile(35f, 2, 1024, false, 2, 4, false),
            DeviceTier.Modest => new RenderProfile(50f, 2, 2048, false, 2, 8, false),
            _ => new RenderProfile(90f, 4, 4096, true, 4, 8, true),
        };

        /// <summary>Classe dont la scène est allégée comme sur téléphone (densité d'herbe, ombres des feux, petits objets) ?</summary>
        public static bool Reduced(DeviceTier tier) => tier != DeviceTier.Full;

        /// <summary>
        /// Taille (m, plus grand côté de sa boîte) sous laquelle un objet du décor ne projette plus d'ombre sur une machine allégée :
        /// bougies, gobelets, cailloux, petites caisses, lanternes. Rien ne change sur le PC complet.
        /// </summary>
        public const float SmallCaster = 0.6f;

        /// <summary>
        /// Un objet de taille <paramref name="sizeX"/> × <paramref name="sizeY"/> × <paramref name="sizeZ"/> (m) garde-t-il son ombre sur
        /// la classe <paramref name="tier"/> ? Toujours sur le PC complet ; ailleurs, seulement s'il dépasse <see cref="SmallCaster"/>.
        /// Une taille invalide (NaN) garde l'ombre (aucun changement sur un cas inconnu).
        /// </summary>
        public static bool CastsShadow(float sizeX, float sizeY, float sizeZ, DeviceTier tier)
        {
            if (!Reduced(tier)) return true;
            float largest = Math.Max(sizeX, Math.Max(sizeY, sizeZ));
            return float.IsNaN(largest) || largest >= SmallCaster;
        }

        /// <summary>Un feu d'intérieur projette-t-il des ombres (lumière ponctuelle : six vues de la scène par image) ? Seulement sur le PC complet.</summary>
        public static bool FireShadows(DeviceTier tier) => tier == DeviceTier.Full;
    }

    /// <summary>
    /// V2.53 (essai du soir avec Damien) : mesure des images pour l'affichage F9 : moyenne, pire image et images par seconde sur les
    /// <see cref="Capacity"/> dernières images. Tableau circulaire fixe : aucune allocation par image. Le texte n'est refait que quand
    /// <see cref="Due"/> le dit (deux fois par seconde, d'après les temps ajoutés).
    /// </summary>
    public sealed class FrameStats
    {
        /// <summary>Images gardées (2 s à 60 images par seconde).</summary>
        public const int Capacity = 120;

        /// <summary>Intervalle (s) entre deux textes affichés.</summary>
        public const double RefreshSeconds = 0.5;

        /// <summary>Au-delà (ms), une image compte comme une saccade (sous 20 images par seconde).</summary>
        public const double HitchMs = 50.0;

        private readonly double[] _ms = new double[Capacity];
        private int _next, _count;
        private double _sinceText = RefreshSeconds;

        /// <summary>Saccades (images de plus de <see cref="HitchMs"/> ms) comptées depuis le début.</summary>
        public int Hitches { get; private set; }

        /// <summary>Ajoute une image de <paramref name="seconds"/> secondes (une valeur négative ou NaN est ignorée).</summary>
        public void Add(double seconds)
        {
            if (double.IsNaN(seconds) || seconds < 0) return;
            double ms = seconds * 1000.0;
            _ms[_next] = ms;
            _next = (_next + 1) % Capacity;
            if (_count < Capacity) _count++;
            if (ms > HitchMs) Hitches++;
            _sinceText += seconds;
        }

        /// <summary>Images mesurées (au plus <see cref="Capacity"/>).</summary>
        public int Count => _count;

        /// <summary>Temps moyen d'une image (ms), 0 sans mesure.</summary>
        public double AverageMs
        {
            get
            {
                if (_count == 0) return 0;
                double sum = 0;
                for (int i = 0; i < _count; i++) sum += _ms[i];
                return sum / _count;
            }
        }

        /// <summary>Pire image (ms) des dernières mesurées, 0 sans mesure.</summary>
        public double WorstMs
        {
            get
            {
                double worst = 0;
                for (int i = 0; i < _count; i++) worst = Math.Max(worst, _ms[i]);
                return worst;
            }
        }

        /// <summary>Images par seconde d'après le temps moyen (0 sans mesure).</summary>
        public double Fps => AverageMs > 0 ? 1000.0 / AverageMs : 0;

        /// <summary>Le texte est-il à refaire (premier appel, ou <see cref="RefreshSeconds"/> écoulées) ? Le remet à zéro quand oui.</summary>
        public bool Due()
        {
            if (_sinceText < RefreshSeconds) return false;
            _sinceText = 0;
            return true;
        }

        /// <summary>Texte affiché : « 58 i/s · 17.2 ms (pire 41.0) · saccades 3 » (culture invariante : rien à charger sous IL2CPP).</summary>
        public string Text()
        {
            CultureInfo fr = CultureInfo.InvariantCulture;
            return Math.Round(Fps).ToString("0", fr) + " i/s · " + AverageMs.ToString("0.0", fr) + " ms (pire " + WorstMs.ToString("0.0", fr)
                + ") · saccades " + Hitches.ToString(fr);
        }
    }
}
