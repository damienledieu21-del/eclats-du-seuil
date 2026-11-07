using System.Collections.Generic;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V2.9 (« vrais modèles et sons », demande de Damien du 7 novembre 2026) : les voix des bêtes, en 3D à leur place, entendues
    /// jusqu'à la distance réglée dans le catalogue (<see cref="StyleCatalog.BeastHearing"/>) : hurlements des loups la nuit et dans la
    /// brume, brame du cerf (rare, à l'aube et au crépuscule), grognements du sanglier tout proche, lièvre qui détale. Les longs
    /// enregistrements ne sont joués que par passages (<see cref="BeastCalls.Cue"/>), avec un fondu. Sans fichier : silence (les sons
    /// des packs et ceux faits par le code restent). Aucune allocation par image : les bêtes sont notées une fois, les sources réutilisées.
    /// </summary>
    public sealed class BeastVoices
    {
        private const int VoiceCount = 4;

        private sealed class Ear
        {
            public StyleSlot Slot;
            public Vector3 At;
            public bool Seen;
            public float Speed;
            public bool Calm = true;
            public bool Dead;
            public int Frame;
        }

        private sealed class Voice
        {
            public AudioSource Source;
            public float Started;
            public float Length;
            public float Volume;
            public bool Busy;
        }

        private readonly Dictionary<string, Ear> _ears = new Dictionary<string, Ear>();
        private readonly Voice[] _voices = new Voice[VoiceCount];
        private readonly GameObject _root;
        private int _frame;
        private float _howlIn = 12f, _bellowIn = 20f, _gruntIn = 6f;
        private int _howls, _bellows, _grunts, _dashes;

        public BeastVoices(Transform parent)
        {
            _root = new GameObject("Voix des bêtes (V2.9)");
            if (parent != null) _root.transform.SetParent(parent, false);
            for (int i = 0; i < VoiceCount; i++)
            {
                var go = new GameObject("Voix " + i);
                go.transform.SetParent(_root.transform, false);
                AudioSource source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 1f;
                source.dopplerLevel = 0f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 3f;
                _voices[i] = new Voice { Source = source };
            }
        }

        /// <summary>Nouveau combat : les bêtes notées sont oubliées (les voix en cours finissent).</summary>
        public void Clear() => _ears.Clear();

        public void Destroy()
        {
            if (_root != null) Object.Destroy(_root);
            _ears.Clear();
        }

        /// <summary>Une bête vue à cette image (place affichée, morte ou non) ; un lièvre qui se met à courir détale, avec son bruit dans l'herbe.</summary>
        public void Hear(string id, Vector3 at, bool dead, float deltaTime)
        {
            if (id == null) return;
            if (!_ears.TryGetValue(id, out Ear ear))
            {
                ear = new Ear { Slot = StyleRules.ForCombatant(id, false) };
                _ears[id] = ear;
            }
            if (ear.Seen && deltaTime > 0f)
            {
                Vector3 d = at - ear.At;
                d.y = 0f;
                ear.Speed = Mathf.Lerp(ear.Speed, d.magnitude / deltaTime, 1f - Mathf.Exp(-8f * deltaTime));
            }
            ear.At = at;
            ear.Seen = true;
            ear.Dead = dead;
            ear.Frame = _frame;
            if (ear.Slot != StyleSlot.Hare || dead || deltaTime <= 0f) return;
            (bool dash, bool calm) = BeastCalls.HareDash(ear.Calm, ear.Speed);
            ear.Calm = calm;
            if (dash) Play(StyleSound.HareDash, _dashes++, at, 3f, 0.6f, 0.7f);
        }

        /// <summary>
        /// À chaque image, après les bêtes : qui donne de la voix. <paramref name="listener"/> : la caméra ; <paramref name="minute"/> : minute
        /// du jour du monde ; <paramref name="deltaTime"/> : 0 en pause (personne n'appelle).
        /// </summary>
        public void Update(Vector3 listener, int minute, bool night, bool mist, float deltaTime)
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < VoiceCount; i++)
            {
                Voice v = _voices[i];
                if (!v.Busy) continue;
                float t = now - v.Started;
                if (t >= v.Length || v.Source == null || !v.Source.isPlaying)
                {
                    if (v.Source != null) v.Source.Stop();
                    v.Busy = false;
                    continue;
                }
                v.Source.volume = v.Volume * (float)BeastCalls.Envelope(t, v.Length);
            }
            if (deltaTime > 0f)
            {
                StyleCatalog catalog = StyleCatalog.Loaded;
                float hearing = catalog != null ? catalog.BeastHearing : 60f;
                _howlIn -= deltaTime;
                _bellowIn -= deltaTime;
                _gruntIn -= deltaTime;
                if (_howlIn <= 0f)
                    _howlIn = Call(StyleSound.Howl, StyleSlot.Wolf, BeastCalls.HowlTime(night, mist), listener, 2f * hearing, 8f, 0.9f, 2f, ref _howls);
                if (_bellowIn <= 0f)
                    _bellowIn = Call(StyleSound.Bellow, StyleSlot.Stag, BeastCalls.BellowTime(minute), listener, 2f * hearing, 7f, 0.85f, 2f, ref _bellows);
                if (_gruntIn <= 0f)
                    _gruntIn = Call(StyleSound.BoarGrumble, StyleSlot.Boar, true, listener, 0.5f * hearing, 2.6f, 0.55f, 1f, ref _grunts);
            }
            _frame++;
        }

        /// <summary>
        /// La bête de <paramref name="slot"/> vivante la plus proche de l'oreille, vue à cette image et à moins de <paramref name="reach"/>
        /// mètres, appelle (si c'est le moment) ; rend les secondes avant le prochain essai.
        /// </summary>
        private float Call(StyleSound sound, StyleSlot slot, bool time, Vector3 listener, float reach, float seconds, float volume, float range, ref int count)
        {
            if (!time) return 5f;
            Ear best = null;
            float bestDistance = reach * reach;
            foreach (Ear ear in _ears.Values)
            {
                if (ear.Slot != slot || ear.Dead || ear.Frame != _frame) continue;
                float d = (ear.At - listener).sqrMagnitude;
                if (d < bestDistance)
                {
                    best = ear;
                    bestDistance = d;
                }
            }
            if (best == null) return 4f;
            Play(sound, count, best.At, seconds, volume, range);
            return (float)BeastCalls.Interval(sound, count++);
        }

        /// <summary>
        /// Joue un passage du son <paramref name="sound"/> (variante et passage d'après <paramref name="n"/>) à la place <paramref name="at"/>,
        /// entendu jusqu'à <paramref name="range"/> fois la distance du catalogue. Rien sans catalogue ou sans fichier.
        /// </summary>
        private void Play(StyleSound sound, int n, Vector3 at, float seconds, float volume, float range)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            AudioClip clip = catalog != null ? catalog.Sound(sound, BeastCalls.Pick(n, 1 << 20)) : null;
            if (clip == null) return;
            Voice voice = null;
            for (int i = 0; i < VoiceCount && voice == null; i++)
                if (!_voices[i].Busy) voice = _voices[i];
            if (voice == null) return;   // quatre voix à la fois au plus : la suivante attendra
            (double start, double length) = BeastCalls.Cue(clip.name, clip.length, n, seconds);
            if (length <= 0) return;
            AudioSource source = voice.Source;
            source.transform.position = at + Vector3.up;
            source.maxDistance = catalog.BeastHearing * range;
            source.clip = clip;
            source.volume = 0f;
            source.time = Mathf.Clamp((float)start, 0f, Mathf.Max(0f, clip.length - 0.05f));
            source.Play();
            voice.Started = Time.unscaledTime;
            voice.Length = (float)length;
            voice.Volume = volume * catalog.BeastVolume;
            voice.Busy = true;
        }
    }
}
