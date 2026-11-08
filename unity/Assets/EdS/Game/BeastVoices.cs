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

        private readonly Dictionary<string, Ear> _ears = new Dictionary<string, Ear>();
        private readonly SoundPool _pool;
        private int _frame;
        private float _howlIn = 12f, _bellowIn = 20f, _gruntIn = 6f;
        private int _howls, _bellows, _grunts, _dashes;

        public BeastVoices(Transform parent)
        {
            _pool = new SoundPool(parent, "Voix des bêtes (V2.9)", VoiceCount);
        }

        /// <summary>Nouveau combat : les bêtes notées sont oubliées (les voix en cours finissent).</summary>
        public void Clear() => _ears.Clear();

        public void Destroy()
        {
            _pool.Destroy();
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
            _pool.Update();
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

        private void Play(StyleSound sound, int n, Vector3 at, float seconds, float volume, float range) => _pool.Play(sound, n, at, seconds, volume, range);
    }

    /// <summary>
    /// Sous-lot V2.9 : quelques sources 3D réutilisées qui jouent des passages de sons (<see cref="BeastCalls.Cue"/>) avec un fondu, entendus
    /// jusqu'à un multiple de la distance du catalogue (<see cref="StyleCatalog.BeastHearing"/>). Aucune allocation par image.
    /// </summary>
    public sealed class SoundPool
    {
        private sealed class Voice
        {
            public AudioSource Source;
            public float Started;
            public float Length;
            public float Volume;
            public bool Busy;
        }

        private readonly Voice[] _voices;
        private readonly GameObject _root;

        public SoundPool(Transform parent, string name, int count, float spatialBlend = 1f)
        {
            _root = new GameObject(name);
            if (parent != null) _root.transform.SetParent(parent, false);
            _voices = new Voice[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Voix " + i);
                go.transform.SetParent(_root.transform, false);
                AudioSource source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = spatialBlend;
                source.dopplerLevel = 0f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 3f;
                _voices[i] = new Voice { Source = source };
            }
        }

        public void Destroy()
        {
            if (_root != null) Object.Destroy(_root);
        }

        /// <summary>À chaque image : fondus des passages en cours, fin de ceux qui sont joués.</summary>
        public void Update()
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < _voices.Length; i++)
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
        }

        /// <summary>
        /// Joue un passage du son <paramref name="sound"/> (variante et passage d'après <paramref name="n"/>) à la place <paramref name="at"/>,
        /// entendu jusqu'à <paramref name="range"/> fois la distance du catalogue. Faux sans catalogue, sans fichier ou sans source libre.
        /// </summary>
        /// <param name="pitch">Sous-lot V2.9.2 : hauteur (l'âne sans braiment prend le hennissement, plus grave).</param>
        public bool Play(StyleSound sound, int n, Vector3 at, float seconds, float volume, float range, bool beastVolume = true, float pitch = 1f)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            AudioClip clip = catalog != null ? catalog.Sound(sound, BeastCalls.Pick(n, 1 << 20)) : null;
            if (clip == null) return false;
            Voice voice = null;
            for (int i = 0; i < _voices.Length && voice == null; i++)
                if (!_voices[i].Busy) voice = _voices[i];
            if (voice == null) return false;
            (double start, double length) = BeastCalls.Cue(clip.name, clip.length, n, seconds);
            if (length <= 0) return false;
            AudioSource source = voice.Source;
            source.transform.position = at + Vector3.up;
            source.maxDistance = catalog.BeastHearing * range;
            source.clip = clip;
            voice.Started = Time.unscaledTime;
            voice.Length = (float)length;
            voice.Volume = volume * (beastVolume ? catalog.BeastVolume : 1f);
            // Relecture de la PR #114 : le volume de la première image est déjà le bon (un pas part à plein volume, sans montée).
            source.volume = voice.Volume * (float)BeastCalls.Envelope(0, length);
            source.time = Mathf.Clamp((float)start, 0f, Mathf.Max(0f, clip.length - 0.05f));
            source.pitch = pitch;
            source.Play();
            voice.Busy = true;
            return true;
        }
    }
}
