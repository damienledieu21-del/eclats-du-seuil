using System.Collections.Generic;
using System.Linq;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Essai de style : un combattant habillé d'un modèle des packs. Le corps simple reste (place, rotation, cible, barres) mais n'est plus
    /// dessiné ; le modèle, enfant du corps, joue l'animation qui suit l'allure affichée (attente, marche, course), l'attaque quand on la
    /// lui demande, la mort à la fin. Rien ne revient vers le moteur : l'aspect ne change aucun combat.
    /// </summary>
    public sealed class StyleBody
    {
        // Sous-lot V2.9.2 : les animations passent par un lecteur : le composant Animation (« Legacy ») des modèles des packs et de Mixamo,
        // ou le graphe Humanoid des corps humains (HumanMotion).
        private readonly IClipPlayer _animation;
        private readonly HumanOutfit? _outfit;
        private readonly Transform _look;
        private readonly List<string> _clips = new List<string>();

        /// <summary>
        /// Relecture de l'essai 7 : animation choisie pour chaque demande (mouvement, préférence, variante), retenue : la liste des animations
        /// ne change plus après la construction, et le choix (expressions régulières) coûtait à chaque image et pour chaque corps.
        /// </summary>
        private readonly Dictionary<(StyleMotion, string, int), string> _picked = new Dictionary<(StyleMotion, string, int), string>();
        private bool _fallen;
        private string _playing;

        /// <summary>Sous-lot V2.15 (relecture de la PR #133) : le clip joué est-il une marche ? Calculé quand il change, lu par <see cref="Pace"/>.</summary>
        private bool _playingIsWalk;

        /// <summary>Matériaux du modèle (copies propres à ce corps) et leur couleur d'origine, pour la teinte.</summary>
        private readonly List<Material> _materials = new List<Material>();
        private readonly List<Color> _colors = new List<Color>();
        private bool _tinted;
        private Vector3 _last;
        private bool _started;
        private float _speed;

        // Sous-lot V2.9 : une bête (loup, sanglier, cerf, biche, lièvre) règle la cadence de sa marche et de son galop sur sa vitesse ; un
        // modèle sans squelette (le sanglier de Hanabi_Map) est animé par le code (BeastCalls.StaticGait).
        private bool _beast;
        private StyleMotion _playingMotion;
        private bool _procedural;
        private Vector3 _basePosition;
        private Quaternion _baseRotation;
        private float _metersToLocal;
        private float _clock;
        private float _overlayUntil;
        private StyleMotion _overlayMotion;
        private bool _overlaid;

        // Sous-lot V2.15 (démarche réaliste) : allure, virages, attentes, regard.
        private uint _seed;
        private bool _moving;
        private float _still;
        private float _turnHold;
        private LocomotionGait _gait;
        private float _yawLast;
        private bool _yawKnown;
        private float _yawRate;
        private float _lean;
        private readonly Quaternion _leanBase;
        private bool _gaitsKnown;
        private string _walkClip, _trotClip, _gallopClip;
        private double _walkNatural, _trotNatural, _gallopNatural;
        private List<string> _idles;
        private int _idleVariant;
        private float _idleUntil = -1f;
        private float _life;
        private HeadTurn _head;

        // Essai réaliste 7 : geste en cours (attaque, sort, coup reçu) et sa fin.
        private float _actionUntil;
        private StyleMotion _actionMotion;
        private string _actionClip;

        /// <summary>Essai réaliste 7 : hauteur du modèle (mètres), pour placer les effets à hauteur de poitrine.</summary>
        public float Height { get; }

        internal StyleBody(Transform look, Animation animation, float height)
            : this(look, animation != null ? new LegacyClips(animation) : null, height, null)
        {
        }

        /// <summary>Sous-lot V2.9.2 : un corps animé par <paramref name="player"/> ; <paramref name="outfit"/> : la tenue d'un corps humain (son travail).</summary>
        internal StyleBody(Transform look, IClipPlayer player, float height, HumanOutfit? outfit)
        {
            _look = look;
            _animation = player;
            _outfit = outfit;
            Height = height;
            _leanBase = look != null ? look.localRotation : Quaternion.identity;
            // Relecture de la PR #117 : les matériaux ne sont copiés qu'à la première teinte (mort, étourdi), et ces copies sont détruites
            // avec le corps (MaterialCopies) : les corps humains gardent leurs matériaux partagés (instanciation, mémoire).
            if (player == null) return;
            foreach (string name in player.Names)
            {
                _clips.Add(name);
                player.SetLoop(name, StyleRules.Loops(name));
            }
        }

        /// <summary>
        /// Sous-lot V2.9 : une bête (<see cref="StyleRules.IsBeast"/>) ; sans animation, elle est animée par le code autour de sa place
        /// (<paramref name="basePosition"/>, <paramref name="baseRotation"/> : celles du modèle dans son corps ; <paramref name="metersToLocal"/> :
        /// un mètre dans les unités du corps).
        /// </summary>
        internal void Beast(Vector3 basePosition, Quaternion baseRotation, float metersToLocal)
        {
            _beast = true;
            _procedural = _animation == null || _clips.Count == 0;
            _basePosition = basePosition;
            _baseRotation = baseRotation;
            _metersToLocal = metersToLocal;
        }

        /// <summary>
        /// Essai réaliste 8 : un villageois joue en boucle l'animation de son activité (<see cref="TownModels.VillagerClip"/>), commencée à
        /// un moment propre à chacun (deux bavards ne bougent pas ensemble). Rend son nom, ou null s'il n'a pas d'animation.
        /// </summary>
        public string Loop(VillagerActivity activity, int variant)
        {
            if (_animation == null) return null;
            // Sous-lot V2.9.2 : un corps humain qui « cueille » fait le travail de son métier (la forge frappe, les paysans récoltent).
            string clip = (activity == VillagerActivity.Gather && _outfit is HumanOutfit outfit ? HumanLooks.WorkClip(_clips, outfit, variant) : null)
                ?? TownModels.VillagerClip(_clips, activity, variant);
            if (clip == null) return null;
            if (clip == _playing) return clip;
            if (_animation.Has(clip))
            {
                _animation.SetLoop(clip, true);
                _animation.SetTime(clip, _animation.Length(clip) * (Mathf.Abs(variant) % 10) / 10f);
            }
            _animation.CrossFade(clip, 0.25f);
            _playing = clip;
            _playingIsWalk = StyleRules.Key(clip).Contains("walk");   // V2.15 : une fois par changement de clip, pas à chaque image
            return clip;
        }

        /// <summary>
        /// Revue Codex de la PR #55 : même signal que le corps simple : assombri mort, clignotant jaune étourdi (chaque image tant que
        /// l'étourdissement dure), sinon les couleurs d'origine du modèle (rendues une fois au retour).
        /// </summary>
        public void Tint(bool dead, bool stunned)
        {
            if (!dead && !stunned && !_tinted) return;
            if (!_copied) CopyMaterials();
            for (int i = 0; i < _materials.Count; i++) ArenaView.ApplyColor(_materials[i], ArenaView.Shade(_colors[i], dead, stunned));
            _tinted = dead || stunned;
        }

        private bool _copied;

        /// <summary>Copies propres à ce corps de ses matériaux, et leur couleur d'origine (pour la teinte) ; détruites avec lui.</summary>
        private void CopyMaterials()
        {
            _copied = true;
            if (_look == null) return;
            MaterialCopies owner = _look.gameObject.GetComponent<MaterialCopies>();
            if (owner == null) owner = _look.gameObject.AddComponent<MaterialCopies>();   // pas « ?? » : le null de Unity n'est pas celui de C#
            foreach (Renderer r in _look.GetComponentsInChildren<Renderer>())
                foreach (Material m in r.materials)
                {
                    _materials.Add(m);
                    owner.Copies.Add(m);
                    _colors.Add(m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white);
                }
        }

        /// <summary>
        /// Demande l'animation d'attaque pendant un court instant (compétence lancée par le joueur) : la durée de l'animation, entre 0,6 et
        /// 1,2 seconde (essai réaliste : le coup d'épée de Mixamo dure 1,5 seconde, on n'en garde que le geste).
        /// </summary>
        public void Strike() => Act(StyleMotion.Attack, null, 0, 0.6f, 1.2f);

        /// <summary>
        /// Essai réaliste 7 (demande de Damien du 28 octobre 2026 : « coups variés ») : le geste de la compétence
        /// <paramref name="skillId"/> (<see cref="StyleRules.SkillAnimation"/>) : chaque frappe a le sien, l'ultime et les soutiens sont des
        /// sorts. Entre 0,6 et 1,4 seconde.
        /// </summary>
        public void Strike(string skillId, bool ultimate)
        {
            (StyleMotion motion, string prefer, int variant) = StyleRules.SkillAnimation(skillId ?? "", ultimate);
            Act(motion, prefer, variant, 0.6f, 1.4f);
        }

        /// <summary>
        /// Essai réaliste 7 : un adversaire porte l'attaque <paramref name="attackId"/> (annoncée au sol) : son geste
        /// (<see cref="StyleRules.AttackAnimation"/>), entre 0,6 et 1,6 seconde. Jusqu'ici, les adversaires ne jouaient jamais leur attaque.
        /// </summary>
        public void Attack(string attackId)
        {
            (StyleMotion motion, string prefer, int variant) = StyleRules.AttackAnimation(attackId ?? "");
            Act(motion, prefer, variant, 0.6f, 1.6f);
        }

        /// <summary>
        /// Essai réaliste 7 : coup reçu, sur la garde (<paramref name="guarded"/>) ou non. Il n'interrompt ni une attaque ni un sort ; sans
        /// animation de coup reçu, rien ne change.
        /// </summary>
        public void Hurt(bool guarded)
        {
            if (Time.time < _actionUntil && (_actionMotion == StyleMotion.Attack || _actionMotion == StyleMotion.Cast)) return;
            Act(guarded ? StyleMotion.GuardHit : StyleMotion.Hurt, null, 0, 0.3f, 0.7f);
        }

        private void Act(StyleMotion motion, string prefer, int variant, float shortest, float longest)
        {
            if (_fallen) return;
            if (_procedural)
            {
                // Sous-lot V2.9 : le sanglier sans squelette charge (le groin en avant) ou encaisse (un sursaut).
                _actionUntil = Time.time + (motion == StyleMotion.Hurt || motion == StyleMotion.GuardHit ? 0.3f : 0.55f);
                _actionMotion = motion;
                return;
            }
            if (_animation == null) return;
            string clip = Pick(motion, prefer, variant);
            // Sous-lot V2.9 (2e vague) : une bête sans clip d'attaque (le loup de roo3d, le cerf élaphe) ou de coup reçu charge ou sursaute par
            // le code, par-dessus son animation.
            bool missing = clip == null || ((motion == StyleMotion.Attack || motion == StyleMotion.Cast) && clip == Pick(StyleMotion.Idle));
            if (_beast && missing)
            {
                _overlayUntil = Time.time + (motion == StyleMotion.Hurt || motion == StyleMotion.GuardHit ? 0.3f : 0.55f);
                _overlayMotion = motion;
                return;
            }
            if (clip == null) return;
            bool known = _animation.Has(clip);
            _actionUntil = Time.time + (known ? Mathf.Clamp(_animation.Length(clip), shortest, longest) : shortest);
            _actionMotion = motion;
            _actionClip = clip;
            // Deux coups de suite : le geste recommence. Relecture de l'essai 7 : toujours depuis le début, même redemandé pendant son fondu
            // de sortie (il ne repart pas de sa dernière image).
            if (known) _animation.SetTime(clip, 0f);
        }

        /// <summary>Animation pour cette demande (<see cref="StyleRules.Clip(IEnumerable{string}, StyleMotion, string, int)"/>), retenue.</summary>
        private string Pick(StyleMotion motion, string prefer = null, int variant = 0)
        {
            var key = (motion, prefer ?? string.Empty, variant);
            if (!_picked.TryGetValue(key, out string clip))
            {
                clip = StyleRules.Clip(_clips, motion, prefer, variant);
                _picked[key] = clip;
            }
            return clip;
        }

        /// <summary>À chaque image : allure d'après le déplacement affiché, puis l'animation qui va avec.</summary>
        /// <param name="guarding">
        /// Essai réaliste 6 (essai de Damien du 27 octobre 2026 : le disque du bouclier « c'est à corriger ») : garde levée ou parade ;
        /// immobile, le modèle joue son clip de garde (« Sword And Shield Block » de Mixamo), sinon il reste au repos.
        /// </param>
        /// <param name="wander">Sous-lot V2.4.1 : ce que fait une bête qui erre (<c>CombatantView.Wander</c>) : arrêtée, elle broute ou fouille.</param>
        public void Update(Vector3 position, bool dead, float deltaTime, bool guarding = false, string wander = null)
        {
            // Revue Codex de la PR #133 : un mort ne regarde plus rien, même couché sans animation de mort (retours anticipés plus bas).
            if (dead && _head != null)
            {
                _head.Glance = 0f;
                _head.HasTarget = false;
            }
            // Un modèle sans animation de mort (le loup et le cochon des packs) se couche sur le flanc, immobile.
            if (dead && (_animation == null || Pick(StyleMotion.Death) == null))
            {
                if (_fallen) return;
                _fallen = true;
                if (_animation != null) _animation.Stop();
                _look.localRotation = _look.localRotation * Quaternion.Euler(0f, 0f, 90f);
                return;
            }
            if (_animation == null && !_procedural) return;
            if (_started && deltaTime > 0f)
            {
                Vector3 d = position - _last;
                d.y = 0f;
                // Vitesse lissée : le corps glisse d'une place du moteur à la suivante, par à-coups d'un tick. Sous-lot V2.15 : départ en
                // douceur, arrêt vif (le cycle ne continue pas pendant que le corps est déjà arrêté).
                _speed = (float)Locomotion.Ease(_speed, d.magnitude / deltaTime, 0.15, 0.06, deltaTime);
            }
            _last = position;
            _started = true;
            Gauge(deltaTime);
            if (_procedural)
            {
                Procedural(dead, deltaTime, wander);
                return;
            }
            // Essai réaliste 7 : un geste en cours (attaque, sort, coup reçu) l'emporte sur l'allure, pas sur la mort.
            bool acting = !dead && Time.time < _actionUntil && _actionClip != null;
            if (_head != null) _head.Glance = acting || dead ? 0f : _moving ? 0.35f : 1f;
            if (acting) Play(_actionClip, _actionMotion, _actionMotion == StyleMotion.Hurt || _actionMotion == StyleMotion.GuardHit ? 0.06f : 0.1f);
            else if (dead) Play(Pick(StyleMotion.Death), StyleMotion.Death, 0.18f);
            else if (_moving) Locomote();
            else if (_turnHold > 0f && Gaits() && _walkClip != null)
            {
                // Sous-lot V2.15 : il pivote à l'arrêt : petits pas lents, jamais une rotation de statue sur place.
                Play(_walkClip, StyleMotion.Walk, 0.2f);
                _animation.SetSpeed(_walkClip, 0.6f);
            }
            else
            {
                StyleMotion motion = StyleRules.Motion(0.0, false, false, guarding, wander);
                Play(motion == StyleMotion.Idle ? IdleClip() : Pick(motion), motion, 0.3f);
            }
            if (_beast) Overlay(dead);
            else if (!dead) Lean(deltaTime);
        }

        /// <summary>
        /// Sous-lot V2.15 : en marche ou à l'arrêt (hystérésis, <see cref="Locomotion.Moving"/>), vitesse de rotation du corps (lacet de son
        /// parent, lissé) et pas sur place quand il pivote sans avancer.
        /// </summary>
        private void Gauge(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            _life += deltaTime;
            float yaw = _look != null && _look.parent != null ? _look.parent.eulerAngles.y : 0f;
            if (_yawKnown) _yawRate = (float)Locomotion.Ease(_yawRate, Locomotion.Wrap(yaw - _yawLast) / deltaTime, 0.1, 0.1, deltaTime);
            _yawLast = yaw;
            _yawKnown = true;
            _still = _speed < Locomotion.StopSpeed ? _still + deltaTime : 0f;
            _moving = Locomotion.Moving(_moving, _speed, _still);
            if (!_moving && Locomotion.TurningInPlace(_speed, _yawRate)) _turnHold = 0.3f;
            else _turnHold -= deltaTime;
            if (!_moving) _gait = LocomotionGait.Stand;
        }

        /// <summary>Joue <paramref name="clip"/> (fondu de <paramref name="fade"/> s) s'il ne joue pas déjà ; rien sans clip.</summary>
        private void Play(string clip, StyleMotion motion, float fade)
        {
            if (clip == null || clip == _playing) return;
            _playing = clip;
            _playingIsWalk = StyleRules.Key(clip).Contains("walk");   // V2.15 : une fois par changement de clip, pas à chaque image
            _playingMotion = motion;
            _animation.CrossFade(clip, fade);
            // Sous-lot V2.15 : chaque attente à sa propre cadence (deux voisins ne respirent pas ensemble).
            if (motion == StyleMotion.Idle) _animation.SetSpeed(clip, (float)Locomotion.IdleRate(_seed));
        }

        /// <summary>
        /// Sous-lot V2.15 : allure en marche : pas, trot ou course, galop ou sprint (<see cref="Locomotion.ChooseGait"/>), chacun à la cadence
        /// de la vitesse réelle ; un corps humain mêle marche et course selon la vitesse, pas synchronisés (<see cref="HumanMotion.Blend"/>).
        /// </summary>
        private void Locomote()
        {
            if (!Gaits()) return;
            _gait = Locomotion.ChooseGait(_gait == LocomotionGait.Stand ? LocomotionGait.Walk : _gait, _speed, Height, _beast, _trotClip != null,
                _gallopClip != null);
            if (_animation is HumanMotion human && _walkClip != null && _trotClip != null && _gait != LocomotionGait.Gallop)
            {
                double t = Locomotion.RunBlend(_speed, _walkNatural, _trotNatural);
                (double rw, double rr) = Locomotion.BlendRates(_animation.Length(_walkClip), _animation.Length(_trotClip), t, _speed, _walkNatural,
                    _trotNatural);
                human.Blend(_walkClip, _trotClip, (float)t, (float)rw, (float)rr, 0.2f);
                _playing = Blended;
                _playingIsWalk = false;
                _playingMotion = t < 0.5 ? StyleMotion.Walk : StyleMotion.Run;
                return;
            }
            // Revue Codex de la PR #133 : l'allure voulue sans clip prend le cycle présent le plus proche (un modèle qui n'a que le galop
            // galope au ralenti à faible vitesse), à la cadence de sa vitesse naturelle.
            LocomotionGait shown = Locomotion.ClosestGait(_gait, _walkClip != null, _trotClip != null, _gallopClip != null);
            string clip = shown == LocomotionGait.Gallop ? _gallopClip : shown == LocomotionGait.Trot ? _trotClip : _walkClip;
            if (clip == null) return;
            double natural = clip == _gallopClip ? _gallopNatural : clip == _trotClip ? _trotNatural : _walkNatural;
            Play(clip, _gait == LocomotionGait.Walk ? StyleMotion.Walk : StyleMotion.Run, 0.25f);
            _animation.SetSpeed(clip, (float)Locomotion.PlaybackRate(_speed, natural));
        }

        /// <summary>Marqueur du mélange marche ↔ course en cours (pas un nom d'animation).</summary>
        private const string Blended = "\u0001mélange";

        /// <summary>
        /// Sous-lot V2.15 : cycles du pas, du trot (ou de la course) et du galop (ou du sprint) du modèle, et leur vitesse naturelle (mesurée
        /// dans l'animation, sinon d'après la taille : <see cref="Locomotion.NaturalSpeed"/>), cherchés une fois. Faux sans aucun cycle.
        /// </summary>
        private bool Gaits()
        {
            if (!_gaitsKnown)
            {
                _gaitsKnown = true;
                _walkClip = Pick(StyleMotion.Walk);
                if (_walkClip != null && !StyleRules.Key(_walkClip).Contains("walk")) _walkClip = null;   // le repli d'une marche absente
                foreach (string name in _clips)
                {
                    string key = StyleRules.Key(name);
                    if (_gallopClip == null && (key.Contains("gallop") || key.Contains("sprint"))) _gallopClip = name;
                    if (_trotClip == null && (key.Contains("trot") || key.Contains("jog"))) _trotClip = name;
                }
                if (_trotClip == null)
                {
                    string run = Pick(StyleMotion.Run);
                    if (run != null && run != _gallopClip && run != _walkClip && !StyleRules.Key(run).Contains("walk")) _trotClip = run;
                }
                if (_walkClip == null && _trotClip == null && _gallopClip == null) _walkClip = Pick(StyleMotion.Walk);
                float scale = _look != null ? Mathf.Max(0.0001f, _look.lossyScale.y) : 1f;
                _walkNatural = Natural(LocomotionGait.Walk, _walkClip, scale);
                _trotNatural = Natural(LocomotionGait.Trot, _trotClip, scale);
                _gallopNatural = Natural(LocomotionGait.Gallop, _gallopClip, scale);
            }
            return _walkClip != null || _trotClip != null || _gallopClip != null;
        }

        private double Natural(LocomotionGait gait, string clip, float scale)
        {
            // Une animation Humanoid mesure son déplacement à l'échelle du bassin (environ la moitié de la taille) ; une autre, dans les unités du modèle.
            double measured = clip == null ? 0.0 : _animation.Travel(clip) * (_animation is HumanMotion ? 0.53 * Height : scale);
            return Locomotion.NaturalSpeed(gait, Height, _beast, measured);
        }

        /// <summary>
        /// Sous-lot V2.15 : attente du moment : l'ordinaire le plus souvent, parfois une autre (regarder autour, bras croisés, renifler) si le
        /// modèle en a (<see cref="Locomotion.IsIdleVariant"/>), à des moments propres à chacun (<see cref="Locomotion.IdleVariant"/>).
        /// </summary>
        private string IdleClip()
        {
            if (_idles == null)
            {
                _idles = new List<string>();
                string main = Pick(StyleMotion.Idle);
                if (main != null) _idles.Add(main);
                foreach (string name in _clips)
                    if (_idles.Count < 4 && name != main && Locomotion.IsIdleVariant(name)) _idles.Add(name);
            }
            if (_idles.Count == 0) return null;
            if (_life >= _idleUntil)
            {
                (int variant, double until) = Locomotion.IdleVariant(_seed, _life, _idles.Count);
                _idleVariant = variant;
                _idleUntil = (float)until;
            }
            return _idles[Mathf.Clamp(_idleVariant, 0, _idles.Count - 1)];
        }

        /// <summary>Sous-lot V2.15 : penché dans les virages (l'équilibre d'un coureur), lissé ; droit à l'arrêt.</summary>
        private void Lean(float deltaTime)
        {
            if (_look == null || _fallen || deltaTime <= 0f) return;
            float target = _moving ? -(float)Locomotion.Lean(_yawRate, _speed, 7.0) : 0f;
            _lean = (float)Locomotion.Ease(_lean, target, 0.15, 0.15, deltaTime);
            _look.localRotation = _leanBase * Quaternion.Euler(0f, 0f, _lean);
        }

        /// <summary>Sous-lot V2.15 : vitesse affichée (m/s, lissée).</summary>
        public float Speed => _speed;

        /// <summary>Sous-lot V2.15 : une bête (allures, virages plus lents).</summary>
        public bool IsBeast => _beast;

        /// <summary>
        /// Sous-lot V2.15 : graine d'affichage de ce corps (son identifiant) : départ et cadence de son attente, moments de ses variantes, regard
        /// qui erre ; pose aussi le suivi de la tête (si le squelette en a une).
        /// </summary>
        public void Seed(string id)
        {
            _seed = Locomotion.Seed(id);
            if (_animation != null)
            {
                string idle = Pick(StyleMotion.Idle);
                if (idle != null && _animation.Has(idle)) _animation.SetTime(idle, _animation.Length(idle) * (float)Locomotion.IdlePhase(_seed));
            }
            if (_head == null && _look != null) _head = HeadTurn.Attach(_look, _look.parent, _seed, _beast);
            if (_head != null) _head.Seed = _seed;
        }

        /// <summary>
        /// Sous-lot V2.15 : un villageois qui marche (<see cref="Loop"/>) règle la cadence de son pas sur sa vitesse <paramref name="speed"/>
        /// (m/s) : plus de pieds qui glissent quand il ralentit pour arriver.
        /// </summary>
        public void Pace(float speed)
        {
            if (_animation == null || _playing == null || !_playingIsWalk) return;
            Gaits();
            _animation.SetSpeed(_playing, (float)Locomotion.PlaybackRate(speed, _walkNatural, 0.35, 1.6));
        }

        /// <summary>Sous-lot V2.15 : point que la tête suit (null : aucun, le regard erre).</summary>
        public void Watch(Vector3? point)
        {
            if (_head == null) return;
            _head.HasTarget = point.HasValue && !_fallen;
            if (point.HasValue) _head.Target = point.Value;
        }

        /// <summary>Sous-lot V2.9 (2e vague) : charge (groin ou museau en avant) ou sursaut d'une bête animée sans le clip voulu ; puis sa place d'origine.</summary>
        private void Overlay(bool dead)
        {
            if (_look == null || _fallen) return;
            if (dead || Time.time >= _overlayUntil)
            {
                if (!_overlaid) return;
                _overlaid = false;
                _look.localPosition = _basePosition;
                _look.localRotation = _baseRotation;
                return;
            }
            _overlaid = true;
            float push = Mathf.Sin(Mathf.PI * (1f - Mathf.Clamp01((_overlayUntil - Time.time) / 0.55f)));
            bool hurt = _overlayMotion == StyleMotion.Hurt || _overlayMotion == StyleMotion.GuardHit;
            _look.localPosition = _basePosition + new Vector3(0f, 0f, hurt ? -0.08f * Height * push : 0.25f * Height * push) * _metersToLocal;
            _look.localRotation = _baseRotation * Quaternion.Euler(hurt ? -6f * push : 10f * push, 0f, hurt ? 6f * push : 0f);
        }

        /// <summary>
        /// Sous-lot V2.9 : allure d'un modèle sans squelette (<see cref="BeastCalls.StaticGait"/>) : il se dandine au pas, bondit au trot,
        /// fouille le groin au sol, charge le groin en avant ; mort, il se couche sur le flanc (plus haut).
        /// </summary>
        private void Procedural(bool dead, float deltaTime, string wander)
        {
            if (dead || _look == null) return;
            _clock += deltaTime;
            (double bob, double pitch, double roll, double yaw) = BeastCalls.StaticGait(_speed, _clock, wander);
            float forward = 0f;
            if (Time.time < _actionUntil)
            {
                float left = Mathf.Clamp01((_actionUntil - Time.time) / 0.55f);
                float push = Mathf.Sin(Mathf.PI * (1f - left));
                if (_actionMotion == StyleMotion.Hurt || _actionMotion == StyleMotion.GuardHit) roll += 8.0 * push;
                else
                {
                    pitch += 14.0 * push;
                    forward = 0.25f * Height * push;
                }
            }
            _look.localPosition = _basePosition + new Vector3(0f, (float)bob * Height, forward) * _metersToLocal;
            _look.localRotation = _baseRotation * Quaternion.Euler((float)pitch, (float)yaw, (float)roll);
        }
    }


    /// <summary>Essai de style : habillage des corps et du décor par les modèles du <see cref="StyleCatalog"/>.</summary>
    public static class StyleLook
    {
        /// <summary>
        /// Habille <paramref name="body"/> (capsule de 2 m de haut à l'échelle 1, centrée à 1 m du sol) du modèle de <paramref name="slot"/>,
        /// à la hauteur <paramref name="height"/> ; rend null (rien ne change) sans catalogue ou sans modèle.
        /// </summary>
        /// <param name="lookId">
        /// Sous-lot V2.9.2 : identifiant qui décide de l'aspect d'un corps humain (le villageois « bourg/bavard1 », le PNJ « npc.odile ») ;
        /// sans lui, <paramref name="id"/>.
        /// </param>
        public static StyleBody Dress(GameObject body, StyleSlot slot, string id, float height, string lookId = null) =>
            Dress(body, slot, id, height, lookId, true);

        private static StyleBody Dress(GameObject body, StyleSlot slot, string id, float height, string lookId, bool humans)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            if (catalog == null) return null;
            // Sous-lot V2.9.2 : un corps humain (villageois, gardes, PNJ, bandits sans personnage de Mixamo, le héros sans paladin) ; sinon le
            // modèle du catalogue, comme avant.
            HumanLook? human = humans ? HumanBodies.Wanted(catalog, slot, id, lookId) : null;
            GameObject prefab = human is HumanLook wanted ? (wanted.Female ? catalog.Humans.Female : catalog.Humans.Male) : catalog.Model(slot, id);
            if (prefab == null) return null;
            if (human is HumanLook sized) height = (float)sized.Height;
            else if (slot == StyleSlot.Character && catalog.Character(id) is CharacterLook character && character.Height > 0f) height = character.Height;
            bool beast = StyleRules.IsBeast(slot);
            GameObject look = Fit(prefab, height, 0f, null, false, out float bottom, out GameObject model, beast);
            if (human is HumanLook person)
            {
                // Sous-lot V2.9.4 : les vrais vêtements (le rôle choisit la tenue, l'identifiant ses variantes), sinon la tenue peinte.
                IClipPlayer player = HumanBodies.Dress(model, catalog.Humans, person, HumanBodies.Clothing(catalog, slot, id, lookId, person));
                // Relecture de la PR #117 : sans animations (graphe impossible), pas de corps figé en pose en T : le modèle d'avant (ou la
                // forme simple).
                if (player == null)
                {
                    Object.Destroy(look);
                    return Dress(body, slot, id, height, lookId, false);
                }
                Vector3 hs = body.transform.localScale;
                look.transform.SetParent(body.transform, false);
                float hk = look.transform.localScale.y;
                look.transform.localScale = new Vector3(hk / hs.x, hk / hs.y, hk / hs.z);
                look.transform.localPosition = new Vector3(0f, -1f - bottom * hk / hs.y, 0f);
                look.transform.localRotation = Quaternion.identity;
                Renderer capsule = body.GetComponent<Renderer>();
                if (capsule != null) capsule.enabled = false;
                var dressed = new StyleBody(look.transform, player, height, person.Outfit);
                dressed.Seed(lookId ?? id);   // sous-lot V2.15 : attente et regard propres à chacun
                return dressed;
            }
            Vector3 s = body.transform.localScale;
            look.transform.SetParent(body.transform, false);
            float k = look.transform.localScale.y;
            look.transform.localScale = new Vector3(k / s.x, k / s.y, k / s.z);
            // Les pieds au sol : le bas de la capsule est à 1 m sous son centre (à l'échelle de hauteur du corps).
            look.transform.localPosition = new Vector3(0f, -1f - bottom * k / s.y, 0f);
            look.transform.localRotation = Quaternion.Euler(0f, catalog.Yaw(slot, id), 0f);
            Renderer own = body.GetComponent<Renderer>();
            if (own != null) own.enabled = false;
            // Essai du 26 octobre 2026 : les loups et les sangliers des packs n'ont pas de texture (couleur unique ou couleurs de sommets que
            // le rendu n'affiche pas) et sortaient blancs ; leurs matériaux sans texture prennent la couleur du catalogue.
            // Essai réaliste 8 : le loup réaliste de l'Asset Store a ses textures sur « _MainTex » ou sur d'autres propriétés (pelage) : il
            // garde ses couleurs.
            // Sous-lot V2.9 : seuls les matériaux sans texture restés blancs la prennent (les bêtes de Quaternius ont leurs couleurs : pelage,
            // ventre clair, yeux, truffe) ; le loup blanc du contrat de la guilde est éclairci (sauf les yeux et la truffe, sombres).
            if (slot == StyleSlot.Wolf || slot == StyleSlot.Boar)
            {
                bool white = slot == StyleSlot.Wolf && StyleRules.Key(id ?? "").StartsWith("loupblanc", System.StringComparison.Ordinal);
                foreach (Renderer r in look.GetComponentsInChildren<Renderer>())
                    foreach (Material m in r.materials)
                    {
                        if (Textured(m, "_BaseMap") || Textured(m, "_MainTex") || Textured(m, "_BaseColorMap")) continue;
                        Color c = BaseColor(m);
                        if (white && c.maxColorComponent > 0.1f) ArenaView.ApplyColor(m, Color.Lerp(c, catalog.WhiteWolfColor, 0.8f));
                        else if (Mathf.Min(c.r, Mathf.Min(c.g, c.b)) > 0.85f) ArenaView.ApplyColor(m, slot == StyleSlot.Wolf ? catalog.WolfColor : catalog.BoarColor);
                    }
            }
            Animation animation = look.GetComponentInChildren<Animation>();
            // Essai réaliste 8 : un prefab de pack garde son Animator, qui ne sait pas jouer des animations « Legacy » : coupé, le
            // composant Animation joue.
            if (animation != null)
                foreach (Animator animator in look.GetComponentsInChildren<Animator>())
                {
                    animator.enabled = false;
                    Object.Destroy(animator);
                }
            // Essai réaliste : les personnages de Mixamo ont leurs animations dans des fichiers à part (catalogue), jouées par un composant
            // Animation posé sur la racine du modèle (les chemins des os partent de là).
            AnimationClip[] clips = catalog.Clips(slot, id);
            if (clips != null && clips.Length > 0)
            {
                if (animation == null) animation = model.AddComponent<Animation>();
                foreach (AnimationClip clip in clips)
                    if (clip != null && animation.GetClip(clip.name) == null) animation.AddClip(clip, clip.name);
            }
            var styled = new StyleBody(look.transform, animation, height);
            if (beast) styled.Beast(look.transform.localPosition, look.transform.localRotation, 1f / Mathf.Max(0.01f, s.y));
            styled.Seed(lookId ?? id);   // sous-lot V2.15
            return styled;
        }

        /// <summary>Couleur de base d'un matériau (URP ou ancien rendu), blanc sans couleur.</summary>
        internal static Color BaseColor(Material m) =>
            m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;

        /// <summary>Le matériau a-t-il une texture sur <paramref name="property"/> ?</summary>
        private static bool Textured(Material m, string property) => m.HasProperty(property) && m.GetTexture(property) != null;

        /// <summary>
        /// Élément de décor du modèle de <paramref name="slot"/> posé au sol en <paramref name="center"/>, de hauteur <paramref name="height"/>
        /// ou, si <paramref name="width"/> est positif, de largeur <paramref name="width"/> (rochers sur l'emprise de l'obstacle), ou, si
        /// <paramref name="natural"/>, à sa taille réelle quand elle est plausible (plantes) ; null sans catalogue ou sans modèle.
        /// </summary>
        public static GameObject Decor(Transform parent, StyleSlot slot, string id, Vector3 center, float height, float width = 0f, bool natural = false)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            GameObject prefab = catalog != null ? catalog.Model(slot, id) : null;
            if (prefab == null) return null;
            GameObject look = Fit(prefab, height, width, id, natural, out float bottom, out GameObject model);
            float k = look.transform.localScale.y;
            look.name = id + " (essai de style)";
            look.transform.SetParent(parent, false);
            look.transform.position = new Vector3(center.x, -bottom * k, center.z);
            look.transform.rotation = Quaternion.Euler(0f, StyleRules.Variant(id, 360), 0f);
            return look;
        }

        /// <summary>
        /// Copie du modèle, sans collision, à l'échelle voulue ; <paramref name="bottom"/> : bas du modèle à l'échelle 1 ;
        /// <paramref name="model"/> : la copie, posée dans le support rendu.
        /// <para>
        /// Essai de Damien du 26 octobre 2026 (le chevalier, les loups et les sangliers restaient invisibles) : le modèle importé garde la
        /// rotation et l'échelle de sa racine (un modèle venu de Blender est redressé par elle) ; il est posé dans un support vide, et c'est
        /// le support qu'on met à l'échelle, place et tourne. Les maillages animés sont calculés même hors champ (pas de disparition après
        /// la mise à l'échelle).
        /// </para>
        /// <para>
        /// Essai réaliste : un élément de décor (<paramref name="part"/> non nul) tiré d'un modèle de Poly Haven, livré en lot de variantes
        /// avec leurs niveaux de détail, n'en garde qu'une, choisie d'après l'identifiant, ramenée au centre du support
        /// (<see cref="StyleRules.KeepParts"/>). <paramref name="natural"/> : taille réelle si elle est plausible
        /// (<see cref="StyleRules.NaturalScale"/>), légèrement variée.
        /// </para>
        /// </summary>
        internal static GameObject Fit(GameObject prefab, float height, float width, string part, bool natural, out float bottom, out GameObject model,
            bool center = false)
        {
            var look = new GameObject(prefab.name + " (essai de style)");
            model = Object.Instantiate(prefab, look.transform, false);
            foreach (Collider c in look.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            foreach (SkinnedMeshRenderer skin in look.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.updateWhenOffscreen = true;
            Wave5View.Fix(look, prefab.name, null);   // sous-lot V2.11 : plumes du corbeau animé, réglages de la 5e vague
            Renderer[] renderers = look.GetComponentsInChildren<Renderer>();
            bool lot = false;
            if (part != null && renderers.Length > 1)
            {
                var names = new List<string>();
                foreach (Renderer r in renderers) names.Add(r.gameObject.name);
                IReadOnlyList<int> keep = StyleRules.KeepParts(prefab.name, names, part);
                if (keep != null && keep.Count > 0 && keep.Count < renderers.Length)
                {
                    renderers = Keep(look, model, renderers, keep);
                    lot = true;
                }
            }
            bool any = false;
            Bounds bounds = default;
            foreach (Renderer r in renderers)
            {
                if (!any) bounds = r.bounds;
                else bounds.Encapsulate(r.bounds);
                any = true;
            }
            bottom = any ? bounds.min.y : 0f;
            float size = any ? bounds.size.y : 0f;
            // Sous-lot V2.9 : une bête est ramenée au centre de son corps (le sanglier de Hanabi_Map et le lièvre de Pneshik sont posés loin
            // de leur origine dans leur fichier) : elle tourne sur elle-même et reste sous sa barre de vie.
            if (center && any) model.transform.localPosition -= new Vector3(bounds.center.x, 0f, bounds.center.z);
            float k;
            if (width > 0f && any) k = (float)StyleRules.Scale(Mathf.Max(bounds.size.x, bounds.size.z), width);
            else if (natural) k = (float)StyleRules.NaturalScale(size, height) * (0.85f + 0.003f * StyleRules.Variant(part ?? prefab.name, 100));
            else k = (float)StyleRules.Scale(size, height);
            look.transform.localScale = new Vector3(k, k, k);
            if (Logged.Add(prefab.name))
                Debug.Log("Essai de style : " + prefab.name + " (hauteur importée " + size + " m, largeur " + (any ? Mathf.Max(bounds.size.x, bounds.size.z) : 0f)
                    + " m, bas " + bottom + ", échelle " + k + (lot ? ", une variante du lot" : "") + ")");
            return look;
        }

        /// <summary>
        /// Ne montre que les pièces <paramref name="keep"/> du modèle (une variante d'un lot de Poly Haven, un tronçon du ponton), ramenées au
        /// centre du support ; rend leurs rendus.
        /// </summary>
        private static Renderer[] Keep(GameObject look, GameObject model, Renderer[] renderers, IReadOnlyList<int> keep)
        {
            // Les niveaux de détail importés ne choisissent plus : seule la pièce gardée s'affiche.
            foreach (LODGroup group in look.GetComponentsInChildren<LODGroup>()) group.enabled = false;
            var kept = new List<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
                if (keep.Contains(i)) kept.Add(renderers[i]);
                else renderers[i].gameObject.SetActive(false);
            Bounds piece = kept[0].bounds;
            foreach (Renderer r in kept) piece.Encapsulate(r.bounds);
            Vector3 center = piece.center;
            model.transform.localPosition = model.transform.localPosition - new Vector3(center.x, 0f, center.z);
            return kept.ToArray();
        }

        /// <summary>Boîte des rendus actifs d'un objet (dans le monde) ; false s'il n'en a aucun.</summary>
        public static bool VisibleBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (!r.gameObject.activeInHierarchy) continue;
                if (!any) bounds = r.bounds;
                else bounds.Encapsulate(r.bounds);
                any = true;
            }
            return any;
        }

        /// <summary>
        /// Essai réaliste (27 octobre 2026) : objet de décor du catalogue (<see cref="StyleProp"/>) posé au sol en <paramref name="at"/>,
        /// tourné de <paramref name="yaw"/> degrés, à sa taille réelle multipliée par <paramref name="scale"/> (et par l'échelle du
        /// catalogue) ; <paramref name="oneVariant"/> : une seule variante d'un lot de Poly Haven (les pervenches). Null sans modèle.
        /// </summary>
        public static GameObject Prop(Transform parent, StyleProp kind, string id, Vector3 at, float yaw, float scale = 1f, bool oneVariant = false)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            PropLook prop = catalog != null ? catalog.Prop(kind) : null;
            if (prop == null) return null;
            var look = new GameObject(id + " (essai de style)");
            GameObject model = Object.Instantiate(prop.Model, look.transform, false);
            foreach (Collider c in look.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            Renderer[] renderers = look.GetComponentsInChildren<Renderer>();
            if (oneVariant && renderers.Length > 1)
            {
                var names = new List<string>();
                foreach (Renderer r in renderers) names.Add(r.gameObject.name);
                IReadOnlyList<int> keep = StyleRules.KeepParts(prop.Model.name, names, id);
                if (keep != null && keep.Count > 0 && keep.Count < renderers.Length) Keep(look, model, renderers, keep);
            }
            look.transform.SetParent(parent, false);
            look.transform.localScale = Vector3.one * scale * (prop.Scale > 0f ? prop.Scale : 1f);
            look.transform.rotation = Quaternion.Euler(0f, yaw + prop.Yaw, 0f);
            look.transform.position = at;
            Ground(look, at.y);
            return look;
        }

        /// <summary>
        /// Bâtiment du catalogue (le moulin de Sketchfab) posé sur une emprise carrée de <paramref name="width"/> mètres centrée en
        /// <paramref name="at"/> : ce qui touche le sol (la tour, pas les ailes qui dépassent) prend la largeur de l'emprise et y est centré ;
        /// son animation (les ailes du moulin) tourne en boucle s'il en a une. Null sans modèle.
        /// </summary>
        public static GameObject Building(Transform parent, StyleProp kind, string id, Vector3 at, float yaw, float width)
        {
            GameObject look = Prop(parent, kind, id, at, yaw);
            if (look == null) return null;
            // Le moulin de Sketchfab est livré avec sa version simplifiée posée à côté : seul l'exemplaire le plus détaillé reste.
            Renderer[] renderers = look.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 1)
            {
                var boxes = new List<(double, double, double, double, double, double)>();
                var detail = new List<int>();
                foreach (Renderer r in renderers)
                {
                    Bounds rb = r.bounds;
                    boxes.Add((rb.min.x, rb.min.y, rb.min.z, rb.max.x, rb.max.y, rb.max.z));
                    MeshFilter filter = r.GetComponent<MeshFilter>();
                    SkinnedMeshRenderer skin = r as SkinnedMeshRenderer;
                    Mesh mesh = filter != null ? filter.sharedMesh : skin != null ? skin.sharedMesh : null;
                    detail.Add(mesh != null ? mesh.vertexCount : 0);
                }
                IReadOnlyList<int> keep = StyleRules.Cluster(boxes, detail);
                if (keep.Count > 0 && keep.Count < renderers.Length) Keep(look, look.transform.GetChild(0).gameObject, renderers, keep);
            }
            if (Foot(look, out Bounds foot) && Mathf.Max(foot.size.x, foot.size.z) > 0.01f)
            {
                look.transform.localScale = look.transform.localScale * (width / Mathf.Max(foot.size.x, foot.size.z));
                Ground(look, at.y);
                Foot(look, out foot);
                look.transform.position += new Vector3(at.x - foot.center.x, 0f, at.z - foot.center.z);
            }
            foreach (Animation animation in look.GetComponentsInChildren<Animation>())
            {
                foreach (AnimationState state in animation) state.wrapMode = WrapMode.Loop;
                animation.Play();
            }
            return look;
        }

        /// <summary>Boîte des rendus actifs qui touchent le bas de l'objet (à 5 % de sa hauteur près) : la tour d'un moulin, sans ses ailes.</summary>
        private static bool Foot(GameObject look, out Bounds foot)
        {
            foot = default;
            if (!VisibleBounds(look, out Bounds all)) return false;
            bool any = false;
            foreach (Renderer r in look.GetComponentsInChildren<Renderer>())
            {
                if (!r.gameObject.activeInHierarchy || r.bounds.min.y > all.min.y + 0.05f * all.size.y) continue;
                if (!any) foot = r.bounds;
                else foot.Encapsulate(r.bounds);
                any = true;
            }
            return any;
        }

        /// <summary>Descend ou monte l'objet pour que le bas de ses rendus actifs soit à la hauteur <paramref name="y"/>.</summary>
        public static void Ground(GameObject look, float y)
        {
            if (VisibleBounds(look, out Bounds b)) look.transform.position += Vector3.up * (y - b.min.y);
        }

        /// <summary>Modèles déjà décrits dans le journal de Unity (une ligne par modèle, pour régler l'essai).</summary>
        private static readonly HashSet<string> Logged = new HashSet<string>();

        private static int _variant;

        /// <summary>
        /// Joue un son de l'essai (rien sans catalogue ou sans ce son). Essai réaliste 7 : une variante différente à chaque fois quand le
        /// catalogue en a plusieurs.
        /// </summary>
        public static void Play(AudioSource source, StyleSound sound, float volume = 0.8f)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            AudioClip clip = catalog != null ? catalog.Sound(sound, _variant++) : null;
            if (source != null && clip != null) source.PlayOneShot(clip, volume);
        }
    }
}
