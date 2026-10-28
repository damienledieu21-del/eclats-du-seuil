using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot 11.6 (H14, proposition du cadrage : une caméra réglable entre les deux positions, rappel rapide, réglage conservé, aucune
    /// bascule automatique) : une seule caméra, réglée en continu entre la <b>vue 1</b> (haute, de trois quarts) et la <b>vue 2</b>
    /// (troisième personne, le personnage entier). La molette règle la position entre les deux ; C rappelle l'autre réglage gardé (au
    /// départ : la vue 2 depuis la vue 1) ; A / E tournent la vue d'un huitième de tour, le bouton du milieu maintenu la tourne librement.
    /// Le réglage et le rappel sont gardés d'une partie à l'autre (<see cref="PlayerPrefs"/>). Rien ne change de vue tout seul ; en vue
    /// proche de la 2, l'orientation suit doucement la marche du personnage, comme avant. La caméra ne change rien au combat.
    /// </summary>
    public sealed class CameraRig
    {
        private const string SettingKey = "eds.camera.reglage";
        private const string RecallKey = "eds.camera.rappel";

        private readonly Camera _camera;
        private float _yaw = 45f;
        private float _setting;
        private float _recall = 1f;
        private float _manualUntil;
        private bool _unsaved;
        private float _savedAt;

        // Essai réaliste 7 : choc des coups (0 à 1) et pose lissée de la caméra, sans le tremblement.
        private double _shock;
        private Vector3 _basePosition;
        private Quaternion _baseRotation;
        private bool _based;

        public Camera Camera => _camera;

        /// <summary>Orientation horizontale de la vue : les déplacements au clavier lui sont relatifs.</summary>
        public float Yaw => _yaw;

        /// <summary>Position de la caméra entre la vue 1 (0) et la vue 2 (1).</summary>
        public float Setting => _setting;

        private CameraRig(Camera camera)
        {
            _camera = camera;
            _setting = Mathf.Clamp01(PlayerPrefs.GetFloat(SettingKey, 0f));
            _recall = Mathf.Clamp01(PlayerPrefs.GetFloat(RecallKey, 1f));
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
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 250f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.09f, 0.1f, 0.13f);
            return new CameraRig(camera);
        }

        /// <summary>Rappel rapide : passe à l'autre réglage gardé, et garde celui qu'on quitte pour le prochain rappel.</summary>
        public void Toggle(float playerYaw)
        {
            float current = _setting;
            _setting = Mathf.Abs(_recall - current) < 0.05f ? (current < 0.5f ? 1f : 0f) : _recall;
            _recall = current;
            if (_setting > 0.5f && current <= 0.5f) _yaw = playerYaw;   // vers la vue 2 : derrière le personnage
            Save();
        }

        private void Save()
        {
            PlayerPrefs.SetFloat(SettingKey, _setting);
            PlayerPrefs.SetFloat(RecallKey, _recall);
            PlayerPrefs.Save();
            _unsaved = false;
            _savedAt = Time.time;
        }

        /// <summary>
        /// Sous-lot 11.8 : rotation (degrés) et réglage entre les vues 1 et 2 demandés au doigt (glisser, pincer). La vue ne suit plus la
        /// marche pendant une seconde après une rotation au doigt ; le réglage est gardé au plus deux fois par seconde.
        /// </summary>
        public void Nudge(float yawDegrees, float settingDelta)
        {
            if (yawDegrees != 0f)
            {
                _yaw += yawDegrees;
                _manualUntil = Time.time + 1f;
            }
            if (settingDelta == 0f) return;
            _setting = Mathf.Clamp01(_setting + settingDelta);
            _unsaved = true;
        }

        /// <summary>
        /// Suit le joueur ; <paramref name="playerYaw"/> oriente la vue proche de la 2 quand la souris ne le fait pas. Caméra à la souris
        /// libre (<paramref name="freeLook"/>, choix de Damien du 26 octobre 2026) : la souris tourne la vue (gauche-droite) et l'incline
        /// (haut-bas : de la vue 1, haute, à la vue 2, derrière le Bastion), sans clic.
        /// </summary>
        public void Follow(Vector3 player, float playerYaw, bool playerMoving, float deltaTime, bool freeLook = false)
        {
            float scroll = Input.mouseScrollDelta.y;
            if (scroll != 0f)
            {
                _setting = Mathf.Clamp01(_setting + scroll * 0.08f);
                Save();
            }
            // Par position : A / E en AZERTY, Q / E en QWERTY (voir Keys).
            if (Input.GetKeyDown(Keys.TurnLeft)) _yaw -= 45f;
            if (Input.GetKeyDown(Keys.TurnRight)) _yaw += 45f;
            // Même cause que la garde (essai du 12.9) : un bouton du milieu simulé par des doigts ne tourne pas la caméra.
            if (freeLook)
            {
                _yaw += Input.GetAxis("Mouse X") * 3f;
                float tilt = Input.GetAxis("Mouse Y");
                if (tilt != 0f)
                {
                    _setting = Mathf.Clamp01(_setting + tilt * 0.03f);
                    _unsaved = true;
                }
            }
            else if (PointerInput.MouseLook(Input.touchCount, Input.GetMouseButton(2))) _yaw += Input.GetAxis("Mouse X") * 4f;
            else if (playerMoving && _setting > 0.5f && Time.time >= _manualUntil) _yaw = Mathf.LerpAngle(_yaw, playerYaw, 1f - Mathf.Exp(-2.5f * deltaTime));
            if (_unsaved && Time.time - _savedAt >= 0.5f) Save();

            float s = _setting;
            float pitch = Mathf.Lerp(52f, 16f, s);
            float distance = Mathf.Lerp(17f, 6f, s);
            Vector3 target = player + Vector3.up * Mathf.Lerp(0.8f, 1.6f, s);
            Quaternion rotation = Quaternion.Euler(pitch, _yaw, 0f);
            Vector3 position = target - rotation * Vector3.forward * distance;
            float k = 1f - Mathf.Exp(-12f * deltaTime);
            Transform t = _camera.transform;
            if (!_based)
            {
                _basePosition = t.position;
                _baseRotation = t.rotation;
                _based = true;
            }
            _basePosition = Vector3.Lerp(_basePosition, position, k);
            _baseRotation = Quaternion.Slerp(_baseRotation, rotation, k);
            // Essai réaliste 7 : le tremblement s'ajoute à la pose lissée, sans s'y accumuler (GameFeel, testé dans le cœur).
            _shock = GameFeel.Settle(_shock, deltaTime);
            (double right, double up, double roll) = GameFeel.Offset(_shock, Time.time);
            t.position = _basePosition + _baseRotation * new Vector3((float)right, (float)up, 0f);
            t.rotation = _baseRotation * Quaternion.Euler(0f, 0f, (float)roll);
        }

        /// <summary>
        /// Essai réaliste 7 (demande de Damien du 28 octobre 2026 : une caméra qui réagit aux coups) : ajoute un choc (0 à 1) ; la caméra
        /// tremble un instant, d'autant plus que le choc est fort, puis se calme toute seule.
        /// </summary>
        public void Shake(double shock) => _shock = GameFeel.Add(_shock, shock);

        public string Describe() =>
            _setting < 0.05f ? "vue 1" : _setting > 0.95f ? "vue 2" : $"entre les vues 1 et 2 ({_setting * 100f:0} %)";
    }
}
