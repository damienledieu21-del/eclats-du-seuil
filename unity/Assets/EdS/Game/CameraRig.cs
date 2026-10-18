using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot 9.3 : caméra du prototype, deux modes à comparer (feuille de route : caméra non tranchée).
    /// <list type="bullet">
    /// <item>isométrique : vue plongeante à 50°, orientation fixe (rotation de 90° avec A/E), recul réglable à la molette ;</item>
    /// <item>troisième personne : derrière le personnage, orientée par la souris (bouton du milieu maintenu) ou par son déplacement.</item>
    /// </list>
    /// </summary>
    public sealed class CameraRig
    {
        public enum Mode
        {
            Isometric = 0,
            ThirdPerson = 1,
        }

        private readonly Camera _camera;
        private float _isoYaw = 45f;
        private float _isoDistance = 16f;
        private float _thirdYaw;
        private float _thirdDistance = 6.5f;

        public Mode Current { get; private set; } = Mode.Isometric;
        public Camera Camera => _camera;

        /// <summary>Orientation horizontale de la vue : les déplacements au clavier lui sont relatifs.</summary>
        public float Yaw => Current == Mode.Isometric ? _isoYaw : _thirdYaw;

        private CameraRig(Camera camera)
        {
            _camera = camera;
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
            camera.farClipPlane = 200f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.09f, 0.1f, 0.13f);
            return new CameraRig(camera);
        }

        public void Toggle(float playerYaw)
        {
            Current = Current == Mode.Isometric ? Mode.ThirdPerson : Mode.Isometric;
            if (Current == Mode.ThirdPerson) _thirdYaw = playerYaw;
        }

        /// <summary>Suit le joueur ; <paramref name="playerYaw"/> oriente la vue de troisième personne quand la souris ne le fait pas.</summary>
        public void Follow(Vector3 player, float playerYaw, bool playerMoving, float deltaTime)
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Current == Mode.Isometric)
            {
                if (Input.GetKeyDown(KeyCode.A)) _isoYaw -= 90f;
                if (Input.GetKeyDown(KeyCode.E)) _isoYaw += 90f;
                _isoDistance = Mathf.Clamp(_isoDistance - scroll * 1.5f, 8f, 30f);
                Quaternion rotation = Quaternion.Euler(50f, _isoYaw, 0f);
                Vector3 target = player + Vector3.up * 0.8f;
                Vector3 position = target - rotation * Vector3.forward * _isoDistance;
                Place(position, rotation, deltaTime);
            }
            else
            {
                if (Input.GetMouseButton(2)) _thirdYaw += Input.GetAxis("Mouse X") * 4f;
                else if (playerMoving) _thirdYaw = Mathf.LerpAngle(_thirdYaw, playerYaw, 1f - Mathf.Exp(-2.5f * deltaTime));
                _thirdDistance = Mathf.Clamp(_thirdDistance - scroll * 0.8f, 3f, 14f);
                Quaternion rotation = Quaternion.Euler(18f, _thirdYaw, 0f);
                Vector3 target = player + Vector3.up * 1.6f;
                Vector3 position = target - rotation * Vector3.forward * _thirdDistance;
                Place(position, rotation, deltaTime);
            }
        }

        private void Place(Vector3 position, Quaternion rotation, float deltaTime)
        {
            float k = 1f - Mathf.Exp(-12f * deltaTime);
            Transform t = _camera.transform;
            t.position = Vector3.Lerp(t.position, position, k);
            t.rotation = Quaternion.Slerp(t.rotation, rotation, k);
        }

        public string Describe() => Current == Mode.Isometric ? "isométrique" : "troisième personne";
    }
}
