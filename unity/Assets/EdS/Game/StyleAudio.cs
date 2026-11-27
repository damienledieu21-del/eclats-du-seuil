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
    /// V2.54 (passe son des Vallons) : toujours créée, même sans catalogue ; la musique d'un lieu et d'un moment
    /// (<see cref="SoundRules.Music"/> : souterrains, Envers, nuit, hiver, combat sous terre), ses replis (<see cref="SoundRules.MusicChain"/>)
    /// et la durée de chaque fondu (<see cref="SoundRules.FadeSeconds"/>) ; les nappes des milieux (marais, grotte, égouts, galeries, grottes
    /// noyées, Envers, sous l'eau, rafales : <see cref="SoundRules.BedVolume"/>), le fichier du catalogue sinon la synthèse
    /// (<see cref="SoundMixer.Bed"/>), faite au premier besoin ; volumes de la musique et de l'ambiance du mixage (<see cref="SoundMixer"/>).
    /// </summary>
    public sealed class StyleAudio
    {
        private float _crossFade = 2.5f;
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
                AudioClip clip = catalog != null ? catalog.MusicClip(kind) : null;
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
                // Sous-lot V2.11 : les braises du four sont un son posé sur le four de Firmin (WorldMarkers), pas une nappe.
                if (kind == StyleAmbience.Bakery) continue;
                AudioClip clip = catalog != null ? catalog.AmbienceClip(kind) : null;
                if (clip == null) continue;   // V2.54 : une nappe de milieu sans fichier est faite au premier besoin (Bed)
                AudioSource source = Source(_root, "Ambiance " + kind);
                source.clip = clip;
                source.volume = 0f;
                // Chaque nappe part d'un endroit différent de sa boucle.
                source.time = clip.length > 1f ? clip.length * (0.17f + 0.29f * (int)kind) % clip.length : 0f;
                source.Play();
                _ambience[kind] = source;
            }
        }

        /// <summary>Musique et ambiance de l'aventure (V2.54 : toujours, même sans catalogue : les nappes de synthèse jouent alors seules).</summary>
        public static StyleAudio Create(Transform parent)
        {
            SoundMixer.Prewarm();   // revue de la PR #267 : synthèses faites d'avance, à l'ouverture de l'aventure
            return new StyleAudio(StyleCatalog.Loaded, parent);
        }

        private float MusicVolume => _catalog != null ? _catalog.MusicVolume : 0.5f;
        private float AmbienceVolume => _catalog != null ? _catalog.AmbienceVolume : 0.6f;

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

        /// <summary>
        /// V2.51b : la partie est dans l'Envers : les nappes du monde (campagne, forêt, rivière, pluie) se taisent, le bourdon grave de
        /// l'Envers (fait par le code, <see cref="EnversLook.Drone"/>) monte ; la musique est celle du Seuil (AdventureSession.Mood).
        /// </summary>
        public bool Envers { get; set; }

        /// <summary>V2.54 : le milieu sonore (<see cref="AdventureSession.SoundBiomeHere"/>).</summary>
        public SoundBiome Biome { get; set; }

        /// <summary>V2.54 : c'est l'hiver (musique d'exploration de l'hiver).</summary>
        public bool Winter { get; set; }

        /// <summary>V2.54 : le personnage est sous l'eau (plongée) : tout s'étouffe, la nappe sous l'eau joue seule.</summary>
        public bool Underwater { get; set; }

        /// <summary>V2.54 : le volume d'une nappe de milieu ; sa source (fichier du catalogue ou synthèse) est faite au premier besoin.</summary>
        private void BedVolume(StyleAmbience kind, float level, float deltaTime)
        {
            if (!_ambience.TryGetValue(kind, out AudioSource source))
            {
                if (level <= 0f) return;
                AudioClip clip = SoundMixer.Bed(kind);
                if (clip == null) return;
                source = Source(_root, "Nappe " + kind + " (synthèse)");
                source.clip = clip;
                source.volume = 0f;
                source.time = clip.length * (0.13f * (int)kind % 1f);
                source.Play();
                _ambience[kind] = source;
            }
            source.volume = Mathf.MoveTowards(source.volume, level, deltaTime * 0.5f);
            // Revue de la PR #267 : une nappe éteinte est mise en pause (elle ne coûte rien), et reprend où elle en était.
            if (level <= 0f && source.volume <= 0f)
            {
                if (source.isPlaying) source.Pause();
            }
            else if (!source.isPlaying)
            {
                if (source.time > 0f) source.UnPause();
                else source.Play();
            }
        }

        public void Update(AdventureMood mood, bool paused, float deltaTime)
        {
            float now = Time.time;
            if (mood.Fighting || mood.BossEngaged) _lastFight = now;
            bool fighting = now - _lastFight < CombatLinger;
            // Sous-lot V2.9 (2e vague) : la musique du Seuil (« Sanctum ») au Seuil, celle des scènes de l'histoire pendant une scène.
            // Sous-lot V2.9.2 : l'auberge (« The Old Tower Inn »), le bourg la nuit (« Night Town »), les attaques (« Determined Pursuit »).
            // Sous-lot V2.11 (5e vague) : « Song of the Forge » près de la forge le jour, la musique de la crypte dans une crypte ; le bourg en
            // fête (réglage « Human Festival ») a sa foule. Sous-lot V2.10 : la musique des fêtes du jeu (mood.Places, sans le réglage).
            AmbiencePlace places = mood.Places | (_catalog != null && _catalog.HumanFestival ? AmbiencePlace.Festival : AmbiencePlace.None);
            StyleMusic placeMusic = StyleRules.MusicFor(mood.BossEngaged, fighting, mood.InVillage, (places & AmbiencePlace.Threshold) != 0, Story,
                mood.Night, (places & AmbiencePlace.TavernInside) != 0, Assault, mood.Indoors ? double.PositiveInfinity : mood.ForgeDistance,
                (places & AmbiencePlace.Crypt) != 0, mood.Places);
            StyleMusic wanted = Available(SoundRules.Music(placeMusic, Biome, mood.Night, Winter));   // V2.54 : lieu et moment
            if (_playing is null || SoundRules.IsFight(wanted) || _playing is StyleMusic now0 && SoundRules.IsFight(now0))
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

            // V2.54 : volume de la musique du mixage ; étouffée sous l'eau.
            float level = Hushed ? 0f : MusicVolume * SoundMixer.Gain(SoundBus.Music) * (paused ? 0.5f : 1f) * (Underwater ? 0.35f : 1f);
            float step = level > 0f ? level * deltaTime / _crossFade : Hushed ? MusicVolume * deltaTime / _crossFade + 0.0001f : 1f;
            foreach (KeyValuePair<StyleMusic, AudioSource> music in _music)
            {
                AudioSource source = music.Value;
                float target = music.Key == _playing ? level : 0f;
                source.volume = Mathf.MoveTowards(source.volume, target, step);
                // Une musique éteinte est mise en pause : elle reprendra où elle en était.
                if (target <= 0f && source.volume <= 0f && source.isPlaying) source.Pause();
            }

            float ambience = Hushed ? 0f : AmbienceVolume * SoundMixer.Gain(SoundBus.Ambience) * (paused ? 0.5f : 1f);
            // V2.54 : les nappes des milieux (le bourdon de l'Envers de la V2.51b en est une), faites au premier besoin.
            SoundBiome biome = Envers ? SoundBiome.Envers : Biome;
            for (StyleAmbience bed = StyleAmbience.Marsh; bed <= StyleAmbience.Gusts; bed++)
                BedVolume(bed, ambience * (float)SoundRules.BedVolume(bed, biome, fighting, Underwater, mood.Night, mood.Rain), deltaTime);
            // V2.51b, V2.54 : ni oiseaux, ni rivière, ni pluie dans l'Envers, sous terre (hors crypte) ni sous l'eau.
            if (SoundRules.MutesWorld(biome, Underwater)) ambience = 0f;
            foreach (KeyValuePair<StyleAmbience, AudioSource> layer in _ambience)
            {
                if (SoundRules.IsBed(layer.Key)) continue;
                // Relecture de l'essai 7 : une nappe arrêtée (aventure quittée puis reprise) repart.
                if (!layer.Value.isPlaying) layer.Value.Play();
                // Sous-lot V2.9 (2e vague) : marché, taverne, forge, feu de camp, campagne et forêt suivent le lieu et l'heure du monde.
                float target = ambience * (float)(StyleRules.IsPlaceAmbience(layer.Key)
                    ? StyleRules.PlaceVolume(layer.Key, places, mood.Minute, mood.ForgeDistance, fighting, mood.Indoors, mood.Night, mood.Rain)
                    : StyleRules.AmbienceVolume(layer.Key, mood.RiverDistance, mood.InVillage, fighting, mood.Indoors, mood.Night,
                        mood.Rain));   // sous-lot V2.2 : la nappe de pluie suit la pluie
                layer.Value.volume = Mathf.MoveTowards(layer.Value.volume, target, deltaTime * 0.5f);
            }
        }

        /// <summary>La musique voulue, ou la suivante qui existe : le Gardien sans musique prend celle du combat, le reste celle de l'exploration.</summary>
        private StyleMusic Available(StyleMusic wanted)
        {
            // Sous-lot V2.9.2 : le Gardien et les attaques prennent le combat, le bourg la nuit et l'auberge la musique du village.
            // V2.54 : toute la chaîne des replis (galeries → grottes → crypte, Envers → Seuil, nuit → exploration…).
            if (SoundRules.MusicChain(wanted, _music.ContainsKey) is StyleMusic found) return found;
            if (wanted == StyleMusic.Story && _playing is StyleMusic current && !SoundRules.IsFight(current)) return current;
            return StyleMusic.Exploration;
        }

        private void Switch(StyleMusic wanted)
        {
            _candidate = null;
            if (wanted == _playing) return;
            _crossFade = (float)SoundRules.FadeSeconds(_playing, wanted);   // V2.54 : 1 s vers le combat, 4 s pour en sortir, 5 s pour l'Envers
            _playing = wanted;
            if (!_music.TryGetValue(wanted, out AudioSource source)) return;
            bool fromStart = SoundRules.IsFight(wanted);
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
