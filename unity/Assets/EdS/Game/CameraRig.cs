using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Caméra de l'aventure, de l'entraînement et du réseau. Essai réaliste 9 (Damien, 29 octobre 2026 : « je n'aime pas les vues de
    /// caméra », « elle bouge mal » ; style choisi : World of Warcraft) : elle tourne autour du personnage, son orientation, son
    /// inclinaison et sa distance se règlent séparément (<see cref="CameraRules"/>) et elle ne tourne jamais toute seule (l'ancien réglage
    /// entre la vue 1 et la vue 2 suivait la marche du personnage). La souris l'oriente et l'incline sans clic (choix du 26 octobre 2026,
    /// gardé), ou en tenant le clic gauche comme dans World of Warcraft si on le choisit dans les réglages (pause) ; le bouton du milieu
    /// la tourne aussi ; la molette rapproche ou éloigne, du très près au loin ; A / E la tournent en douceur tant qu'on les tient ; C la
    /// remet derrière le personnage. Distance, inclinaison et réglages sont gardés d'une partie à l'autre (<see cref="PlayerPrefs"/>). La
    /// caméra ne change rien au combat.
    /// </summary>
    public sealed class CameraRig
    {
        private const string DistanceKey = "eds.camera.distance";
        private const string PitchKey = "eds.camera.inclinaison";
        private const string ClickKey = "eds.camera.clic";
        private const string InvertKey = "eds.camera.inverser";
        private const string SensitivityKey = "eds.camera.sensibilite";

        private readonly Camera _camera;
        private float _yaw = 45f;
        private float _pitch = (float)CameraRules.DefaultPitch;
        private float _distance = (float)CameraRules.DefaultDistance;
        private float _shownDistance;
        private Vector3 _target;
        private bool _placed;
        private bool _dragging;
        private bool _unsaved;
        private float _savedAt;

        // Essai réaliste 7 : choc des coups (0 à 1), ajouté à la pose sans s'y accumuler.
        private double _shock;

        public Camera Camera => _camera;

        /// <summary>Orientation horizontale de la vue : les déplacements au clavier lui sont relatifs.</summary>
        public float Yaw => _yaw;

        /// <summary>
        /// Vue derrière le personnage (0 à 1 ; <see cref="CameraRules.Behind"/>) : au-delà de la moitié, reculer marche à reculons, comme dans
        /// World of Warcraft ; vue de haut, le personnage se tourne vers sa marche.
        /// </summary>
        public float Setting => (float)CameraRules.Behind(_pitch);

        /// <summary>Réglage : la souris tourne la vue seulement en tenant le clic gauche (World of Warcraft) ; sinon sans clic.</summary>
        public bool ClickLook { get; private set; }

        /// <summary>Réglage : souris vers le haut, la vue descend (inversé).</summary>
        public bool InvertY { get; private set; }

        /// <summary>Réglage : sensibilité de la souris (0,5 à 2).</summary>
        public float Sensitivity { get; private set; } = 1f;

        /// <summary>
        /// Intérieurs : la caméra reste dans la pièce. Relecture : la distance choisie dehors est gardée à part ; dans une pièce, une
        /// distance propre, bornée, part de la plus petite des deux.
        /// </summary>
        public bool Indoors
        {
            get => _indoors;
            set
            {
                if (value && !_indoors) _indoorDistance = (float)CameraRules.Zoom(_distance, 0, true);
                _indoors = value;
            }
        }

        private bool _indoors;
        private float _indoorDistance = (float)CameraRules.IndoorMaxDistance;

        /// <summary>Distance demandée là où l'on est (dehors ou dans une pièce).</summary>
        private float Wanted => _indoors ? _indoorDistance : _distance;

        /// <summary>Clic gauche maintenu en cours (réglage « clic maintenu ») : le jeu cache alors le curseur, comme World of Warcraft.</summary>
        public bool Dragging => _dragging;

        /// <summary>
        /// Relecture : un écran, un menu (même dessiné en IMGUI) ou les commandes tactiles sont ouverts ; un clic gauche n'y commence pas
        /// de rotation. Revue Codex de la PR #68 : un écran qui s'ouvre **annule** aussi la rotation en cours, sans attendre
        /// <see cref="Follow"/> (qui n'est plus appelé tant que l'écran d'entraînement est ouvert) : le curseur revient pour l'écran.
        /// </summary>
        public bool Blocked
        {
            get => _blocked;
            set
            {
                _blocked = value;
                if (value) _dragging = false;
            }
        }

        private bool _blocked;

        /// <summary>
        /// Sous-lot V2.12.6 (essai du 9 novembre 2026 : la caméra entrait dans les maisons du bourg) : boîtes des bâtiments de l'espace
        /// affiché (null : aucune) ; la caméra se rapproche quand l'une d'elles s'interpose (<see cref="CameraRules.ClearDistance"/>).
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<CameraBox> Blockers { get; set; }

        private float _clear = (float)CameraRules.MaxDistance;

        /// <summary>Sous-lot V2.24 : distance libre affichée contre le relief (<see cref="CameraRules.SettleTerrain"/>).</summary>
        private float _terrainClear = (float)CameraRules.MaxDistance;

        /// <summary>Sous-lot V2.24 : hauteur du sol affiché, délégué créé une fois (pas d'allocation par image).</summary>
        private static readonly System.Func<double, double, double> StandHeight = (x, z) => StyleWorld.StandHeight((float)x, (float)z);

        private CameraRig(Camera camera)
        {
            _camera = camera;
            _distance = (float)CameraRules.Zoom(PlayerPrefs.GetFloat(DistanceKey, (float)CameraRules.DefaultDistance), 0, false);
            _pitch = (float)CameraRules.Tilt(PlayerPrefs.GetFloat(PitchKey, (float)CameraRules.DefaultPitch), 0, 1, false);
            ClickLook = PlayerPrefs.GetInt(ClickKey, 0) == 1;
            InvertY = PlayerPrefs.GetInt(InvertKey, 0) == 1;
            Sensitivity = (float)CameraRules.Sensitivity(PlayerPrefs.GetFloat(SensitivityKey, 1f));
            _shownDistance = _distance;
        }

        /// <summary>Reprend la caméra principale de la scène, ou en crée une.</summary>
        public static CameraRig Create()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                var go = new GameObject("Caméra");
                go.tag = "MainCamera";
                camera = go.AddComponent<Camera>();
            }
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 250f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.09f, 0.1f, 0.13f);
            return new CameraRig(camera);
        }

        /// <summary>C : remet la vue derrière le personnage, à l'inclinaison de départ (la distance est gardée).</summary>
        public void Toggle(float playerYaw)
        {
            _yaw = playerYaw;
            _pitch = (float)CameraRules.DefaultPitch;
            Save();
        }

        /// <summary>Réglages (pause) : souris libre ou clic maintenu.</summary>
        public void SetClickLook(bool click)
        {
            ClickLook = click;
            _dragging = false;
            Save();
        }

        /// <summary>Réglages (pause) : haut-bas inversé.</summary>
        public void SetInvertY(bool invert)
        {
            InvertY = invert;
            Save();
        }

        /// <summary>Réglages (pause) : sensibilité de la souris, bornée.</summary>
        public void SetSensitivity(float sensitivity)
        {
            Sensitivity = (float)CameraRules.Sensitivity(sensitivity);
            Save();
        }

        private void Save()
        {
            PlayerPrefs.SetFloat(DistanceKey, _distance);
            PlayerPrefs.SetFloat(PitchKey, _pitch);
            PlayerPrefs.SetInt(ClickKey, ClickLook ? 1 : 0);
            PlayerPrefs.SetInt(InvertKey, InvertY ? 1 : 0);
            PlayerPrefs.SetFloat(SensitivityKey, Sensitivity);
            PlayerPrefs.Save();
            _unsaved = false;
            _savedAt = Time.time;
        }

        /// <summary>
        /// Sous-lot 11.8 : rotation (degrés) et rapprochement demandés au doigt (glisser, pincer ; <paramref name="settingDelta"/> positif :
        /// plus près). Les réglages sont gardés au plus deux fois par seconde.
        /// </summary>
        public void Nudge(float yawDegrees, float settingDelta, float pitchDegrees = 0f)
        {
            if (yawDegrees != 0f) _yaw = (float)CameraRules.Wrap(_yaw + yawDegrees);
            // Relecture : le doigt qui glisse vers le haut ou le bas incline aussi la vue (vue haute ou derrière le personnage).
            if (pitchDegrees != 0f)
            {
                _pitch = (float)CameraRules.Incline(_pitch, pitchDegrees);
                _unsaved = true;
            }
            if (settingDelta == 0f) return;
            Zoom(CameraRules.Pinch(Wanted, settingDelta, _indoors));
        }

        /// <summary>Nouvelle distance, là où l'on est : celle de dehors est gardée d'une partie à l'autre.</summary>
        private void Zoom(double distance)
        {
            if (_indoors) _indoorDistance = (float)distance;
            else _distance = (float)distance;
            _unsaved = true;
        }

        /// <summary>
        /// Suit le joueur. <paramref name="freeLook"/> : la souris tourne la vue sans clic (curseur caché ; voir <c>PointerInput.FreeLook</c>).
        /// Sinon, avec le réglage « clic maintenu », le clic gauche tenu hors de l'interface la tourne ; le bouton du milieu aussi. Aucune
        /// rotation automatique : <paramref name="playerYaw"/> et <paramref name="playerMoving"/> ne servent plus qu'à garder la signature.
        /// </summary>
        public void Follow(Vector3 player, float playerYaw, bool playerMoving, float deltaTime, bool freeLook = false)
        {
            float scroll = Input.mouseScrollDelta.y;
            if (scroll != 0f) Zoom(CameraRules.Zoom(Wanted, scroll, _indoors));
            // Par position : A / E en AZERTY, Q / E en QWERTY (voir Keys) ; tenues, elles tournent en douceur.
            float turn = (float)CameraRules.TurnSpeed * deltaTime;
            if (Input.GetKey(Keys.TurnLeft)) _yaw -= turn;
            if (Input.GetKey(Keys.TurnRight)) _yaw += turn;
            // Clic gauche maintenu (réglage) : seulement s'il commence hors de l'interface (boutons, carte, écrans).
            if (!ClickLook || freeLook) _dragging = false;
            else if (Input.GetMouseButtonDown(0)) _dragging = Input.touchCount == 0 && !Blocked && !OverInterface();
            else if (!Input.GetMouseButton(0)) _dragging = false;
            // Même cause que la garde (essai du 12.9) : un bouton du milieu simulé par des doigts ne tourne pas la caméra.
            bool look = freeLook || _dragging || PointerInput.MouseLook(Input.touchCount, Input.GetMouseButton(2));
            if (look)
            {
                _yaw += Input.GetAxis("Mouse X") * 3f * Sensitivity;
                float tilt = Input.GetAxis("Mouse Y");
                if (tilt != 0f)
                {
                    _pitch = (float)CameraRules.Tilt(_pitch, tilt, Sensitivity, InvertY);
                    _unsaved = true;
                }
            }
            _yaw = (float)CameraRules.Wrap(_yaw);
            if (_unsaved && Time.time - _savedAt >= 0.5f) Save();

            // Le point visé suit le joueur en douceur ; l'orientation et l'inclinaison répondent tout de suite à la souris (plus de retard
            // sur la rotation), la distance glisse vers celle demandée.
            // Relecture : <paramref name="player"/> est le milieu du corps affiché (les pieds plus 1 m, comme ArenaView.Feet) ; la hauteur
            // visée se compte depuis les pieds (la tête de près, la poitrine de loin).
            Vector3 aim = player - Vector3.up + Vector3.up * (float)CameraRules.TargetHeight(_shownDistance);
            if (!_placed)
            {
                _target = aim;
                _shownDistance = Wanted;
                _placed = true;
                _terrainClear = float.NaN;   // V2.24 : à la pose, la distance libre contre le relief est prise telle quelle
            }
            _target = Vector3.Lerp(_target, aim, 1f - Mathf.Exp(-14f * deltaTime));
            _shownDistance = Mathf.Lerp(_shownDistance, Wanted, 1f - Mathf.Exp(-10f * deltaTime));
            float held = (float)CameraRules.GroundSafeDistance(_shownDistance, _pitch, _target.y);
            // Sous-lot V2.12.6 : devant le premier mur d'un bâtiment entre le personnage et la caméra (un rayon par image, sans physique :
            // les décors n'ont plus de collisions), rapprochée tout de suite, revenue en douceur ; plus près du point visé, elle reste
            // au-dessus du sol.
            (double dx, double dy, double dz) = CameraRules.Offset(_yaw, _pitch, 1.0);
            double clear = CameraRules.ClearDistance((_target.x, _target.y, _target.z), (dx, dy, dz), Blockers);
            _clear = (float)CameraRules.SettleClear(_clear, clear, deltaTime);
            held = Mathf.Min(held, _clear);
            // Sous-lot V2.24 (revue Codex de la PR #144) : avec le relief affiché (collines au bord des Vallons), la caméra se rapproche
            // quand le sol passe entre elle et le personnage (CameraRules.TerrainClearDistance), vite mais sans saut, puis revient en
            // douceur ; sa hauteur reste au-dessus du sol sous elle. Un délégué gardé, aucune allocation par image.
            if (StyleWorld.HasRelief)
            {
                double ground = CameraRules.TerrainClearDistance((_target.x, _target.y, _target.z), (dx, dy, dz), StandHeight, _shownDistance);
                _terrainClear = (float)CameraRules.SettleTerrain(_terrainClear, ground, deltaTime);
                held = Mathf.Min(held, _terrainClear);
            }
            else _terrainClear = (float)CameraRules.MaxDistance;
            (double ox, double oy, double oz) = CameraRules.Offset(_yaw, _pitch, held);
            Vector3 position = _target + new Vector3((float)ox, (float)oy, (float)oz);
            if (StyleWorld.HasRelief)
                position.y = (float)CameraRules.AboveGround(position.y, StyleWorld.StandHeight(position.x, position.z));
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            // Essai réaliste 7 : le tremblement s'ajoute à la pose, sans s'y accumuler (GameFeel, testé dans le cœur).
            _shock = GameFeel.Settle(_shock, deltaTime);
            (double right, double up, double roll) = GameFeel.Offset(_shock, Time.time);
            Transform t = _camera.transform;
            t.position = position + rotation * new Vector3((float)right, (float)up, 0f);
            t.rotation = rotation * Quaternion.Euler(0f, 0f, (float)roll);
        }

        /// <summary>Le pointeur est-il sur un élément de l'interface (uGUI) ?</summary>
        private static bool OverInterface() =>
            UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

        /// <summary>
        /// Essai réaliste 7 (demande de Damien du 28 octobre 2026 : une caméra qui réagit aux coups) : ajoute un choc (0 à 1) ; la caméra
        /// tremble un instant, d'autant plus que le choc est fort, puis se calme toute seule.
        /// </summary>
        public void Shake(double shock) => _shock = GameFeel.Add(_shock, shock);

        /// <summary>Texte de l'aide : la distance de la caméra et la façon de la tourner.</summary>
        public string Describe() =>
            "caméra à " + Mathf.RoundToInt(Wanted) + " m · " + (ClickLook ? "clic gauche pour tourner" : "souris libre");
    }
}
