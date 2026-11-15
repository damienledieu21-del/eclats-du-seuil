using System;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Saisons (décisions de Damien du 14 et du 15 novembre 2026) : l'hôte fournit au cœur l'instant UTC (le cœur ne lit jamais d'horloge ;
    /// en multijoueur, le serveur fait foi), puis expose au rendu la saison et sa transition : globales de shader <c>_EdsSeasonTint</c>
    /// (teinte autour de 0,5 = neutre, force dans a), <c>_EdsSnow</c> (neige sur les faces tournées vers le haut), <c>_EdsIce</c> (glace du
    /// lac, de 0 à 1) et <c>_EdsSeason</c> (saison, transition adoucie, neige, glace). Les shaders d'herbe et de feuillage les lisent ; à 0
    /// (jamais posées), ils ne changent rien. Affichage seulement : aucune règle de combat ne lit la saison.
    /// </summary>
    public sealed class SeasonView
    {
        /// <summary>Intervalle entre deux instants fournis au cœur (secondes de jeu affiché).</summary>
        public const float ClockInterval = 30f;

        private static readonly int TintId = Shader.PropertyToID("_EdsSeasonTint"), SnowId = Shader.PropertyToID("_EdsSnow"),
            IceId = Shader.PropertyToID("_EdsIce"), SeasonId = Shader.PropertyToID("_EdsSeason");

        private float _sinceClock = float.MaxValue;
        private bool _shown;
        private SeasonLook _last;

        public void Sync(AdventureSession adventure, float deltaTime)
        {
            _sinceClock += deltaTime;
            if (_sinceClock >= ClockInterval)
            {
                _sinceClock = 0f;
                adventure.SetCivilTime(DateTimeOffset.UtcNow.ToUnixTimeSeconds());   // l'hôte fournit la date, en UTC (le fuseau du téléphone n'y est pour rien)
            }
            SeasonLook look = adventure.SeasonShown;
            if (_shown && look.Equals(_last)) return;
            _shown = true;
            _last = look;
            Shader.SetGlobalColor(TintId, new Color(look.R, look.G, look.B, look.Strength));
            Shader.SetGlobalFloat(SnowId, look.Snow);
            Shader.SetGlobalFloat(IceId, look.Ice);
            Shader.SetGlobalVector(SeasonId, new Vector4(look.Season, look.Blend, look.Snow, look.Ice));
        }

        /// <summary>Remet les globales à zéro (le décor sans effet de saison).</summary>
        public static void Clear()
        {
            Shader.SetGlobalColor(TintId, new Color(0.5f, 0.5f, 0.5f, 0f));
            Shader.SetGlobalFloat(SnowId, 0f);
            Shader.SetGlobalFloat(IceId, 0f);
            Shader.SetGlobalVector(SeasonId, Vector4.zero);
        }
    }
}
