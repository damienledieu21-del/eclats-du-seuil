using System.Collections.Generic;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>Essai réaliste 7 : un son de l'interface.</summary>
    public enum UiCue
    {
        Click,
        Hover,
        Open,
        Close,
        Tab,
        SkillReady,
        ChargeGained,
        TalentAdded,
        TalentRemoved,
        Confirm,
        Error,
        LevelUp,
        QuestDone,
    }

    /// <summary>
    /// Essai réaliste 7 (demande de Damien du 28 octobre 2026 : « sons d'interface libres : clic, compétence prête, charge gagnée ») : les
    /// sons de l'interface, livrés avec le jeu (<c>Resources/EdsInterface/Sons</c> : « Interface Sounds », « UI Audio » et « Music Jingles »
    /// de Kenney, CC0). Une seule source, sans position, qui ne s'arrête pas pendant la pause ; un même son n'est pas rejoué avant 60 ms
    /// (les survols rapides, plusieurs compétences prêtes ensemble). Volume réglé une fois pour tous. Sans le fichier, rien ne sonne.
    /// </summary>
    public static class UiSounds
    {
        /// <summary>Volume de l'interface (0 à 1).</summary>
        public static float Volume = 0.55f;

        private static readonly Dictionary<UiCue, (string File, float Volume)> Files = new Dictionary<UiCue, (string, float)>
        {
            [UiCue.Click] = ("clic", 0.7f),
            [UiCue.Hover] = ("survol", 0.25f),
            [UiCue.Open] = ("ouverture", 0.6f),
            [UiCue.Close] = ("fermeture", 0.55f),
            [UiCue.Tab] = ("onglet", 0.5f),
            [UiCue.SkillReady] = ("competence_prete", 0.45f),
            [UiCue.ChargeGained] = ("charge", 0.8f),
            [UiCue.TalentAdded] = ("talent", 0.7f),
            [UiCue.TalentRemoved] = ("retrait", 0.6f),
            [UiCue.Confirm] = ("valide", 0.7f),
            [UiCue.Error] = ("erreur", 0.5f),
            [UiCue.LevelUp] = ("niveau", 0.9f),
            [UiCue.QuestDone] = ("quete", 0.85f),
        };

        private static readonly Dictionary<UiCue, AudioClip> Clips = new Dictionary<UiCue, AudioClip>();
        private static readonly Dictionary<UiCue, float> Last = new Dictionary<UiCue, float>();
        private static AudioSource _source;
        private static AudioListener _listener;

        /// <summary>Joue le son <paramref name="cue"/> (rien sans son fichier, ou s'il vient d'être joué).</summary>
        public static void Play(UiCue cue)
        {
            AudioClip clip = Clip(cue);
            if (clip == null) return;
            float now = Time.unscaledTime;
            if (Last.TryGetValue(cue, out float last) && now - last < 0.06f) return;
            Last[cue] = now;
            AudioSource source = Source();
            if (source != null) source.PlayOneShot(clip, Files[cue].Volume * Volume);
        }

        private static AudioClip Clip(UiCue cue)
        {
            if (Clips.TryGetValue(cue, out AudioClip known)) return known;
            AudioClip clip = Files.TryGetValue(cue, out (string File, float Volume) entry) ? Resources.Load<AudioClip>("EdsInterface/Sons/" + entry.File) : null;
            Clips[cue] = clip;
            return clip;
        }

        private static AudioSource Source()
        {
            if (_source != null) return _source;
            var go = new GameObject("Sons de l'interface");
            Object.DontDestroyOnLoad(go);
            _source = go.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.ignoreListenerPause = true;
            // Le jeu crée son écouteur avec la caméra ; sans lui (menus avant la partie), un écouteur ici, retiré quand la caméra prend le
            // sien (ReleaseListener).
            if (Object.FindFirstObjectByType<AudioListener>() == null) _listener = go.AddComponent<AudioListener>();
            return _source;
        }

        /// <summary>
        /// Relecture de l'essai 7 : retire l'écouteur posé ici faute de caméra (un son d'interface joué avant le premier son du jeu), pour que
        /// la caméra du jeu ait le sien. Sans effet s'il n'y en a pas.
        /// </summary>
        public static void ReleaseListener()
        {
            if (_listener == null) return;
            Object.DestroyImmediate(_listener);
            _listener = null;
        }
    }
}
