using System.Collections.Generic;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Essai réaliste 7 (demande de Damien du 28 octobre 2026 : « le son […] le plus gros manque ») : musique et nappes d'ambiance de
    /// l'aventure, prises dans le catalogue de l'essai de style (dossiers <c>Assets/Style/Music</c> et <c>Assets/Style/Ambience</c>). La
    /// musique suit le lieu et le combat (<see cref="StyleRules.MusicFor"/>) : deux sources qui se relaient en fondu enchaîné de deux
    /// secondes et demie ; le combat garde sa musique six secondes après le dernier ennemi proche (pas de va-et-vient). Les ambiances
    /// (oiseaux, vent, rivière) tournent en boucle, leur volume suit la rivière et le combat (<see cref="StyleRules.AmbienceVolume"/>).
    /// Volumes réglables dans le catalogue. Sans catalogue, ou sans musique ni ambiance, rien ne joue.
    /// </summary>
    public sealed class StyleAudio
    {
        private const float CrossFade = 2.5f;
        private const float CombatLinger = 6f;

        private readonly StyleCatalog _catalog;
        private readonly AudioSource[] _music = new AudioSource[2];
        private readonly Dictionary<StyleAmbience, AudioSource> _ambience = new Dictionary<StyleAmbience, AudioSource>();
        private int _front;
        private StyleMusic? _playing;
        private float _fade = 1f;
        private float _lastFight = -99f;

        private StyleAudio(StyleCatalog catalog, Transform parent)
        {
            _catalog = catalog;
            var root = new GameObject("Musique et ambiance");
            root.transform.SetParent(parent, false);
            for (int i = 0; i < 2; i++) _music[i] = Source(root, "Musique " + (i + 1));
            foreach (StyleAmbience kind in System.Enum.GetValues(typeof(StyleAmbience)))
            {
                AudioClip clip = catalog.AmbienceClip(kind);
                if (clip == null) continue;
                AudioSource source = Source(root, "Ambiance " + kind);
                source.clip = clip;
                source.loop = true;
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

        /// <summary>À chaque image : la musique et les ambiances d'après <paramref name="mood"/> ; en pause, tout baisse de moitié.</summary>
        public void Update(AdventureMood mood, bool paused, float deltaTime)
        {
            if (mood.Fighting || mood.BossEngaged) _lastFight = Time.time;
            bool fighting = Time.time - _lastFight < CombatLinger;
            StyleMusic wanted = StyleRules.MusicFor(mood.BossEngaged, fighting, mood.InVillage);
            // Sans la musique voulue, la suivante qui existe : le Gardien sans musique prend celle du combat, le combat celle de l'exploration.
            AudioClip clip = _catalog.MusicClip(wanted);
            if (clip == null && wanted == StyleMusic.Boss) clip = _catalog.MusicClip(wanted = StyleMusic.Combat);
            if (clip == null && wanted != StyleMusic.Exploration) clip = _catalog.MusicClip(wanted = StyleMusic.Exploration);
            if (clip != null && wanted != _playing)
            {
                _playing = wanted;
                _front = 1 - _front;
                AudioSource next = _music[_front];
                next.clip = clip;
                next.volume = 0f;
                next.Play();
                _fade = 0f;
            }
            _fade = Mathf.Min(1f, _fade + deltaTime / CrossFade);
            float level = _catalog.MusicVolume * (paused ? 0.5f : 1f);
            _music[_front].volume = level * _fade;
            AudioSource back = _music[1 - _front];
            back.volume = level * (1f - _fade);
            if (_fade >= 1f && back.isPlaying) back.Stop();

            float ambience = _catalog.AmbienceVolume * (paused ? 0.5f : 1f);
            foreach (KeyValuePair<StyleAmbience, AudioSource> layer in _ambience)
            {
                float target = ambience * (float)StyleRules.AmbienceVolume(layer.Key, mood.RiverDistance, mood.InVillage, fighting);
                layer.Value.volume = Mathf.MoveTowards(layer.Value.volume, target, deltaTime * 0.5f);
            }
        }

        /// <summary>Coupe tout (fin de l'aventure).</summary>
        public void Stop()
        {
            foreach (AudioSource source in _music) source.Stop();
            foreach (AudioSource source in _ambience.Values) source.Stop();
            _playing = null;
        }
    }
}
