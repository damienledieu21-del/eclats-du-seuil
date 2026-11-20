using System;
using EdS.Domain.Time;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Saisons (décisions de Damien du 14 et du 15 novembre 2026) : l'hôte fournit au cœur l'instant UTC (le cœur ne lit jamais d'horloge ;
    /// en multijoueur, le serveur fait foi), puis expose au rendu la saison et sa transition : globales de shader <c>_EdsSeasonTint</c>
    /// (teinte autour de 0,5 = neutre, force dans a), <c>_EdsSnow</c> (neige sur les faces tournées vers le haut), <c>_EdsIce</c> (glace du
    /// lac, de 0 à 1) et <c>_EdsSeason</c> (saison, transition adoucie, neige, glace). Les shaders d'herbe et de feuillage les lisent ; à 0
    /// (jamais posées), ils ne changent rien. Affichage seulement : aucune règle de combat ne lit la saison. En solo, le poste est l'hôte
    /// (<see cref="Sync"/>) ; en réseau, la date vient du serveur (<see cref="SyncServer"/>, protocole 6, sous-lot V2.44.1).
    /// </summary>
    public sealed class SeasonView
    {
        /// <summary>Intervalle entre deux instants fournis au cœur (secondes de jeu affiché).</summary>
        public const float ClockInterval = 30f;

        private static readonly int TintId = Shader.PropertyToID("_EdsSeasonTint"), SnowId = Shader.PropertyToID("_EdsSnow"),
            IceId = Shader.PropertyToID("_EdsIce"), SeasonId = Shader.PropertyToID("_EdsSeason"), PassId = Shader.PropertyToID("_EdsSnowPass");

        /// <summary>V2.29.2 : dernière saison montrée (pour les panoramas de l'horizon) ; <see cref="HasLook"/> faux tant que rien n'a été posé.</summary>
        public static SeasonLook Shown { get; private set; }

        public static bool HasLook { get; private set; }

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
            Show(adventure.SeasonShown);
        }

        /// <summary>
        /// Sous-lot V2.44.1 (protocole 6, le serveur fait foi) : en partie en réseau, la saison vient de la date du serveur
        /// (<c>NetworkClient.ServerCivilNow</c>), jamais de l'horloge du poste.
        /// </summary>
        public void SyncServer(long serverUtcSeconds)
        {
            Show(SeasonLook.Of(SeasonCalendar.AtUtc(serverUtcSeconds)));
        }

        private void Show(SeasonLook look)
        {
            if (_shown && look.Equals(_last)) return;
            _shown = true;
            _last = look;
            Shown = look;
            HasLook = true;
            Shader.SetGlobalColor(TintId, new Color(look.R, look.G, look.B, look.Strength));
            Shader.SetGlobalFloat(SnowId, look.Snow);
            Shader.SetGlobalFloat(IceId, look.Ice);
            Shader.SetGlobalVector(SeasonId, new Vector4(look.Season, look.Blend, look.Snow, look.Ice));
            // V2.44.3 : neige du col au-dessus de sa limite (quantité, limite, fondu), lue par l'herbe, le feuillage et la neige posée.
            Shader.SetGlobalVector(PassId, new Vector4(look.PassSnow, look.SnowLine, SeasonLook.SnowBand, 0f));
        }

        /// <summary>V2.44.3 : neige la plus forte montrée (plaine ou col) ; 0 : rien à couvrir.</summary>
        public static float SnowShown => HasLook ? Mathf.Max(Shown.Snow, Shown.PassSnow) : 0f;

        /// <summary>Remet les globales à zéro (le décor sans effet de saison).</summary>
        public static void Clear()
        {
            HasLook = false;
            Shader.SetGlobalColor(TintId, new Color(0.5f, 0.5f, 0.5f, 0f));
            Shader.SetGlobalFloat(SnowId, 0f);
            Shader.SetGlobalFloat(IceId, 0f);
            Shader.SetGlobalVector(SeasonId, Vector4.zero);
            Shader.SetGlobalVector(PassId, Vector4.zero);
        }
    }
}
