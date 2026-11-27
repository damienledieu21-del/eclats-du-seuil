using System.Collections.Generic;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// V2.54 (passe son des Vallons) : le mixage. Quatre volumes réglables dans les réglages de la pause (musique, ambiance, effets,
    /// interface ; <see cref="SoundRules"/>), gardés dans les préférences du joueur comme la luminosité ; une limite de bruitages joués en
    /// même temps selon la machine (<see cref="SoundRules.VoiceLimit"/> : 24 sur le PC modeste), avec priorités ; les sons de synthèse
    /// joués quand aucun fichier n'est déposé (<see cref="SoundSynth"/>), faits une fois et gardés.
    /// </summary>
    public static class SoundMixer
    {
        public const int Rate = 22050;
        private const string KeyPrefix = "eds.volume.";
        private const int Variants = 3;

        private static readonly int[] Percents = { -1, -1, -1, -1 };
        private static readonly float[] Ends = new float[64];
        private static readonly Dictionary<StyleSound, float> LastStart = new Dictionary<StyleSound, float>();
        private static readonly Dictionary<int, AudioClip> Cues = new Dictionary<int, AudioClip>();
        private static readonly Dictionary<StyleAmbience, AudioClip> Beds = new Dictionary<StyleAmbience, AudioClip>();
        private static int _limit = -1;

        /// <summary>Le canal que règle la ligne « Volume » des réglages (un clic sur son nom passe au suivant).</summary>
        public static SoundBus Shown { get; private set; } = SoundBus.Music;

        /// <summary>Pourcentage choisi pour <paramref name="bus"/> (lu une fois des préférences).</summary>
        public static int Percent(SoundBus bus)
        {
            int i = (int)bus;
            if (Percents[i] < 0) Percents[i] = SoundRules.ClampPercent(PlayerPrefs.GetInt(KeyPrefix + i, SoundRules.DefaultPercent(bus)));
            return Percents[i];
        }

        /// <summary>Gain (0 à 1) de <paramref name="bus"/>.</summary>
        public static float Gain(SoundBus bus) => (float)SoundRules.Gain(Percent(bus));

        /// <summary>Un pas de plus ou de moins sur le canal montré : sauvegardé, entendu tout de suite.</summary>
        public static void Step(int direction)
        {
            int i = (int)Shown;
            Percents[i] = SoundRules.StepPercent(Percent(Shown), direction);
            PlayerPrefs.SetInt(KeyPrefix + i, Percents[i]);
            PlayerPrefs.Save();
            if (Shown == SoundBus.Interface) UiSounds.Play(UiCue.Click);
        }

        /// <summary>Passe au canal suivant dans la ligne des réglages.</summary>
        public static void NextBus() => Shown = SoundRules.NextBus(Shown);

        /// <summary>Texte de la ligne : « Musique : 70 % ».</summary>
        public static string Label => SoundRules.Label(Shown, Percent(Shown));

        private static int Limit
        {
            get
            {
                if (_limit < 0) _limit = SoundRules.VoiceLimit(DeviceQuality.Tier);
                return _limit;
            }
        }

        /// <summary>
        /// Un bruitage <paramref name="sound"/> de <paramref name="length"/> s peut-il partir ? Non s'il vient de partir (salve) ou si trop de
        /// bruitages jouent déjà pour sa priorité ; oui : il est compté jusqu'à sa fin.
        /// </summary>
        public static bool TryStart(StyleSound sound, float length)
        {
            float now = Time.unscaledTime;
            if (LastStart.TryGetValue(sound, out float last) && now - last < (float)SoundRules.MinGap(sound)) return false;
            int playing = 0, free = -1;
            for (int i = 0; i < Ends.Length; i++)
            {
                if (Ends[i] > now) playing++;
                else if (free < 0) free = i;
            }
            if (!SoundRules.Admit(SoundRules.PriorityOf(sound), playing, Limit)) return false;
            LastStart[sound] = now;
            if (free >= 0) Ends[free] = now + Mathf.Max(0.05f, length);
            return true;
        }

        /// <summary>Le bruitage de synthèse <paramref name="sound"/> (une de trois variantes), ou null s'il n'en a pas.</summary>
        public static AudioClip Synth(StyleSound sound, int variant)
        {
            if (!SoundSynth.HasCue(sound)) return null;
            int v = ((variant % Variants) + Variants) % Variants;
            int key = (int)sound * Variants + v;
            if (Cues.TryGetValue(key, out AudioClip known) && known != null) return known;
            AudioClip clip = Make("Synthèse " + sound + " " + v, SoundSynth.Cue(sound, Rate, v));
            Cues[key] = clip;
            return clip;
        }

        /// <summary>La nappe de synthèse <paramref name="kind"/>, ou null si elle n'en a pas.</summary>
        public static AudioClip Bed(StyleAmbience kind)
        {
            if (!SoundSynth.HasBed(kind)) return null;
            if (Beds.TryGetValue(kind, out AudioClip known) && known != null) return known;
            AudioClip clip = Make("Nappe de synthèse " + kind, SoundSynth.Bed(kind, Rate, 254));
            Beds[kind] = clip;
            return clip;
        }

        private static AudioClip Make(string name, float[] samples)
        {
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, Rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
