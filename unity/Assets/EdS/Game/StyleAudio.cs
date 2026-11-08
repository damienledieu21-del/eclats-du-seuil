using System.Collections.Generic;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Essai réaliste 7 (demande de Damien du 28 octobre 2026 : « le son […] le plus gros manque ») : musique et nappes d'ambiance de
    /// l'aventure, prises dans le catalogue de l'essai de style (dossiers <c>Assets/Style/Music</c> et <c>Assets/Style/Ambience</c>). La
    /// musique suit le lieu et le combat (<see cref="StyleRules.MusicFor"/>) ; le combat garde sa musique six secondes après le dernier
    /// ennemi proche. Relecture de l'essai 7 : une source par musique, chacune monte ou descend vers son propre volume (pas de saut de
    /// volume quand on revient en arrière pendant un fondu) ; une musique quittée est mise en pause et reprend où elle en était (le combat
    /// et le Gardien repartent du début) ; passer du village à l'exploration demande d'y rester quatre secondes (on ne bascule pas à chaque
    /// pas sur le bord d'un lieu). Les ambiances (oiseaux, vent, rivière) tournent en boucle, leur volume suit la rivière et le combat
    /// (<see cref="StyleRules.AmbienceVolume"/>). Volumes réglables dans le catalogue. Sans catalogue, ou sans musique ni ambiance, rien ne
    /// joue.
    /// </summary>
    public sealed class StyleAudio
    {
        private const float CrossFade = 2.5f;
        private const float CombatLinger = 6f;

        /// <summary>Temps (s) passé dans un autre lieu avant que la musique du lieu change.</summary>
        private const float PlaceDwell = 4f;

        private readonly StyleCatalog _catalog;
        private readonly GameObject _root;
        private readonly Dictionary<StyleMusic, AudioSource> _music = new Dictionary<StyleMusic, AudioSource>();
        private readonly Dictionary<StyleAmbience, AudioSource> _ambience = new Dictionary<StyleAmbience, AudioSource>();
        private StyleMusic? _playing;
        private StyleMusic? _candidate;
        private float _candidateSince;
        private float _lastFight = -99f;

        private StyleAudio(StyleCatalog catalog, Transform parent)
        {
            _catalog = catalog;
            _root = new GameObject("Musique et ambiance");
            _root.transform.SetParent(parent, false);
            foreach (StyleMusic kind in System.Enum.GetValues(typeof(StyleMusic)))
            {
                AudioClip clip = catalog.MusicClip(kind);
                if (clip == null) continue;
                AudioSource source = Source(_root, "Musique " + kind);
                source.clip = clip;
                source.volume = 0f;
                _music[kind] = source;
            }
            foreach (StyleAmbience kind in System.Enum.GetValues(typeof(StyleAmbience)))
            {
                // Sous-lot V2.2 : le tonnerre n'est pas une nappe : il est joué une fois après chaque éclair (StyleWeather).
                if (kind == StyleAmbience.Thunder) continue;
                AudioClip clip = catalog.AmbienceClip(kind);
                if (clip == null) continue;
                AudioSource source = Source(_root, "Ambiance " + kind);
                source.clip = clip;
                source.volume = 0f;
                // Chaque nappe part d'un endroit différent de sa boucle.
                source.time = clip.length > 1f ? clip.length * (0.17f + 0.29f * (int)kind) % clip.length : 0f;
                source.Play();
                _ambience[kind] = source;
            }
        }

        /// <summary>Musique et ambiance de l'aventure, ou null sans catalogue, ni musique, ni ambiance.</summary>
        public static StyleAudio Create(Transform parent)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            if (catalog == null) return null;
            bool any = catalog.Music != null && catalog.Music.Length > 0 || catalog.Ambiences != null && catalog.Ambiences.Length > 0;
            return any ? new StyleAudio(catalog, parent) : null;
        }

        private static AudioSource Source(GameObject root, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.loop = true;
            return source;
        }

        /// <summary>
        /// Plusieurs fois par seconde : la musique et les ambiances d'après <paramref name="mood"/> ; en pause, tout baisse de moitié.
        /// <paramref name="deltaTime"/> : temps écoulé depuis l'appel précédent.
        /// </summary>
        /// <summary>
        /// Sous-lot V1.9.8 : musique et nappes se taisent (fondu) : le souvenir de la Rupture a son propre chant (<see cref="StoryStage"/>).
        /// </summary>
        public bool Hushed { get; set; }

        /// <summary>Sous-lot V2.9 (2e vague) : une scène de l'histoire se joue (musique « Story », « The Legend of Kvothe », si elle est là).</summary>
        public bool Story { get; set; }

        /// <summary>
        /// Sous-lot V2.9.2 (4e vague) : une attaque du monde est en cours (pillage ou faille) : un combat prend la musique des chefs
        /// (« Determined Pursuit »).
        /// </summary>
        public bool Assault { get; set; }

        public void Update(AdventureMood mood, bool paused, float deltaTime)
        {
            float now = Time.time;
            if (mood.Fighting || mood.BossEngaged) _lastFight = now;
            bool fighting = now - _lastFight < CombatLinger;
            // Sous-lot V2.9 (2e vague) : la musique du Seuil (« Sanctum ») au Seuil, celle des scènes de l'histoire pendant une scène.
            // Sous-lot V2.9.2 : l'auberge (« The Old Tower Inn »), le bourg la nuit (« Night Town »), les attaques (« Determined Pursuit »).
            StyleMusic wanted = Available(StyleRules.MusicFor(mood.BossEngaged, fighting, mood.InVillage, (mood.Places & AmbiencePlace.Threshold) != 0, Story,
                mood.Night, (mood.Places & AmbiencePlace.TavernInside) != 0, Assault, mood.Places));   // sous-lot V2.10 : la musique des fêtes
            if (_playing is null || wanted == StyleMusic.Combat || wanted == StyleMusic.Boss || _playing == StyleMusic.Combat || _playing == StyleMusic.Boss)
                Switch(wanted);
            else if (wanted != _playing)
            {
                // Village et exploration : la nouvelle musique attend que le joueur soit resté quatre secondes dans le lieu.
                if (_candidate != wanted)
                {
                    _candidate = wanted;
                    _candidateSince = now;
                }
                else if (now - _candidateSince >= PlaceDwell) Switch(wanted);
            }
            else _candidate = null;

            float level = Hushed ? 0f : _catalog.MusicVolume * (paused ? 0.5f : 1f);
            float step = level > 0f ? level * deltaTime / CrossFade : Hushed ? _catalog.MusicVolume * deltaTime / CrossFade + 0.0001f : 1f;
            foreach (KeyValuePair<StyleMusic, AudioSource> music in _music)
            {
                AudioSource source = music.Value;
                float target = music.Key == _playing ? level : 0f;
                source.volume = Mathf.MoveTowards(source.volume, target, step);
                // Une musique éteinte est mise en pause : elle reprendra où elle en était.
                if (target <= 0f && source.volume <= 0f && source.isPlaying) source.Pause();
            }

            float ambience = Hushed ? 0f : _catalog.AmbienceVolume * (paused ? 0.5f : 1f);
            foreach (KeyValuePair<StyleAmbience, AudioSource> layer in _ambience)
            {
                // Relecture de l'essai 7 : une nappe arrêtée (aventure quittée puis reprise) repart.
                if (!layer.Value.isPlaying) layer.Value.Play();
                // Sous-lot V2.9 (2e vague) : marché, taverne, forge, feu de camp, campagne et forêt suivent le lieu et l'heure du monde.
                float target = ambience * (float)(StyleRules.IsPlaceAmbience(layer.Key)
                    ? StyleRules.PlaceVolume(layer.Key, mood.Places, mood.Minute, mood.ForgeDistance, fighting, mood.Indoors, mood.Night, mood.Rain)
                    : StyleRules.AmbienceVolume(layer.Key, mood.RiverDistance, mood.InVillage, fighting, mood.Indoors, mood.Night,
                        mood.Rain));   // sous-lot V2.2 : la nappe de pluie suit la pluie
                layer.Value.volume = Mathf.MoveTowards(layer.Value.volume, target, deltaTime * 0.5f);
            }
        }

        /// <summary>La musique voulue, ou la suivante qui existe : le Gardien sans musique prend celle du combat, le reste celle de l'exploration.</summary>
        private StyleMusic Available(StyleMusic wanted)
        {
            if (_music.ContainsKey(wanted)) return wanted;
            // Sous-lot V2.9.2 : le Gardien et les attaques prennent le combat, le bourg la nuit et l'auberge la musique du village.
            if (StyleRules.MusicFallback(wanted) is StyleMusic next && _music.ContainsKey(next)) return next;
            if (wanted == StyleMusic.Story && _playing is StyleMusic current && current != StyleMusic.Combat && current != StyleMusic.Boss) return current;
            return StyleMusic.Exploration;
        }

        private void Switch(StyleMusic wanted)
        {
            _candidate = null;
            if (wanted == _playing) return;
            _playing = wanted;
            if (!_music.TryGetValue(wanted, out AudioSource source)) return;
            bool fromStart = wanted == StyleMusic.Combat || wanted == StyleMusic.Boss;
            if (source.isPlaying && source.volume > 0f) return;   // encore audible (fondu de sortie) : elle remonte d'où elle est
            if (fromStart || source.time <= 0f)
            {
                source.Stop();
                source.Play();
            }
            else source.UnPause();
        }

        /// <summary>Coupe tout (fin de l'aventure) ; une nouvelle aventure recrée la musique.</summary>
        public void Destroy()
        {
            foreach (AudioSource source in _music.Values) source.Stop();
            foreach (AudioSource source in _ambience.Values) source.Stop();
            Object.Destroy(_root);
        }
    }
}
