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

        /// <summary>Suit le joueur ; <paramref name="playerYaw"/> oriente la vue proche de la 2 quand la souris ne le fait pas.</summary>
        public void Follow(Vector3 player, float playerYaw, bool playerMoving, float deltaTime)
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
            if (PointerInput.MouseLook(Input.touchCount, Input.GetMouseButton(2))) _yaw += Input.GetAxis("Mouse X") * 4f;
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
            t.position = Vector3.Lerp(t.position, position, k);
            t.rotation = Quaternion.Slerp(t.rotation, rotation, k);
        }

        public string Describe() =>
            _setting < 0.05f ? "vue 1" : _setting > 0.95f ? "vue 2" : $"entre les vues 1 et 2 ({_setting * 100f:0} %)";
    }
}
