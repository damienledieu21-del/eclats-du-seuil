using System.Collections.Generic;
using System.Linq;
using EdS.Runtime;
using EdS.Simulation.Engine;
using UnityEngine;
using UnityEngine.Rendering;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot 9.3 : arène construite par le code (graphismes provisoires : formes simples). Le joueur est une capsule bleue avec un
    /// bouclier visible en garde (doré pendant la fenêtre de parade) ; les assaillants sont des capsules rouges, le mannequin un
    /// cylindre, un allié une capsule verte ; la cible est cerclée de jaune. Les positions sont purement visuelles : le cœur n'a pas de
    /// système de positions.
    /// </summary>
    public sealed class ArenaView
    {
        public const float Radius = 14f;

        private readonly GameObject _root;
        private readonly Dictionary<string, GameObject> _bodies = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, Renderer> _renderers = new Dictionary<string, Renderer>();
        private readonly Dictionary<string, Color> _baseColors = new Dictionary<string, Color>();
        private GameObject _player;
        private GameObject _shield;
        private Renderer _shieldRenderer;
        private GameObject _targetRing;
        private Material _shieldGuard;
        private Material _shieldParry;
        private Material _shieldBroken;

        public Transform Player => _player != null ? _player.transform : null;

        public ArenaView()
        {
            _root = new GameObject("Arène");
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ground.name = "Sol";
            ground.transform.SetParent(_root.transform, false);
            ground.transform.localScale = new Vector3(Radius * 2f + 2f, 0.05f, Radius * 2f + 2f);
            ground.transform.position = new Vector3(0f, -0.05f, 0f);
            ground.GetComponent<Renderer>().sharedMaterial = MakeMaterial(new Color(0.22f, 0.23f, 0.26f));

            if (Object.FindFirstObjectByType<Light>() == null)
            {
                var sun = new GameObject("Soleil");
                sun.transform.SetParent(_root.transform, false);
                Light light = sun.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                light.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            _shieldGuard = MakeMaterial(new Color(0.65f, 0.7f, 0.78f));
            _shieldParry = MakeMaterial(new Color(1f, 0.82f, 0.25f));
            _shieldBroken = MakeMaterial(new Color(0.75f, 0.15f, 0.12f));
        }

        /// <summary>Matériau opaque compatible avec le rendu actif (URP si configuré, sinon le rendu intégré).</summary>
        public static Material MakeMaterial(Color color)
        {
            Shader shader = null;
            if (GraphicsSettings.currentRenderPipeline != null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            var material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            return material;
        }

        /// <summary>Remplace les combattants affichés par ceux d'un nouveau combat.</summary>
        public void Populate(RealtimeCombat combat)
        {
            foreach (GameObject body in _bodies.Values) Object.Destroy(body);
            _bodies.Clear();
            _renderers.Clear();
            _baseColors.Clear();
            if (_player != null) Object.Destroy(_player);
            if (_targetRing != null) Object.Destroy(_targetRing);

            _player = Body(combat.PlayerId, PrimitiveType.Capsule, new Color(0.25f, 0.45f, 0.95f), Vector3.zero);
            _shield = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _shield.name = "Bouclier";
            Object.Destroy(_shield.GetComponent<Collider>());
            _shield.transform.SetParent(_player.transform, false);
            _shield.transform.localPosition = new Vector3(0f, 0.1f, 0.55f);
            _shield.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _shield.transform.localScale = new Vector3(0.9f, 0.04f, 1.1f);
            _shieldRenderer = _shield.GetComponent<Renderer>();
            _shield.SetActive(false);

            List<OtherState> others = combat.Others().ToList();
            for (int i = 0; i < others.Count; i++)
            {
                OtherState other = others[i];
                // Ennemis en arc de cercle devant le joueur, alliés à ses côtés.
                float spread = others.Count == 1 ? 0f : Mathf.Lerp(-40f, 40f, i / (float)(others.Count - 1));
                Vector3 position = other.IsEnemy
                    ? Quaternion.Euler(0f, spread, 0f) * new Vector3(0f, 0f, 6f)
                    : new Vector3(2.5f * (i % 2 == 0 ? -1f : 1f), 0f, -1.5f);
                PrimitiveType shape = other.View.Kind == CombatantKind.Dummy ? PrimitiveType.Cylinder : PrimitiveType.Capsule;
                Color color = !other.IsEnemy ? new Color(0.3f, 0.8f, 0.4f)
                    : other.View.Kind == CombatantKind.Dummy ? new Color(0.6f, 0.45f, 0.3f)
                    : new Color(0.85f, 0.2f, 0.2f);
                GameObject body = Body(other.Id, shape, color, position);
                body.transform.LookAt(new Vector3(0f, body.transform.position.y, 0f));
            }

            _targetRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _targetRing.name = "Cible";
            Object.Destroy(_targetRing.GetComponent<Collider>());
            _targetRing.transform.SetParent(_root.transform, false);
            _targetRing.transform.localScale = new Vector3(1.6f, 0.01f, 1.6f);
            _targetRing.GetComponent<Renderer>().sharedMaterial = MakeMaterial(new Color(1f, 0.85f, 0.2f));
        }

        private GameObject Body(string id, PrimitiveType shape, Color color, Vector3 position)
        {
            GameObject body = GameObject.CreatePrimitive(shape);
            body.name = id;
            body.transform.SetParent(_root.transform, false);
            body.transform.position = position + Vector3.up;
            Renderer renderer = body.GetComponent<Renderer>();
            renderer.material = MakeMaterial(color);
            _bodies[id] = body;
            _renderers[id] = renderer;
            _baseColors[id] = color;
            return body;
        }

        /// <summary>Position d'affichage d'un combattant (au-dessus de sa tête), ou null s'il n'est pas affiché.</summary>
        public Vector3? Head(string id) => _bodies.TryGetValue(id, out GameObject body) ? body.transform.position + Vector3.up * 1.3f : (Vector3?)null;

        /// <summary>Met l'arène à l'état du combat : garde, parade, cible, combattants hors de combat, adversaires tournés vers le joueur.</summary>
        public void Sync(RealtimeCombat combat, PlayerState player)
        {
            CombatantView me = player.View;
            bool broken = me.States.Contains("guardBreak");
            _shield.SetActive(me.Guarding || me.ParryOpen || broken);
            _shieldRenderer.sharedMaterial = me.ParryOpen ? _shieldParry : broken ? _shieldBroken : _shieldGuard;

            foreach (OtherState other in combat.Others())
            {
                if (!_bodies.TryGetValue(other.Id, out GameObject body)) continue;
                Vector3 look = _player.transform.position;
                look.y = body.transform.position.y;
                if (other.IsEnemy && other.View.Kind != CombatantKind.Dummy && (look - body.transform.position).sqrMagnitude > 0.01f)
                    body.transform.rotation = Quaternion.Slerp(body.transform.rotation, Quaternion.LookRotation(look - body.transform.position), 0.15f);
                Tint(other.Id, other.View.IsDead, other.View.States.Contains("stun"));
            }
            Tint(combat.PlayerId, me.IsDead, me.States.Contains("stun"));

            if (player.TargetId != null && _bodies.TryGetValue(player.TargetId, out GameObject target))
            {
                _targetRing.SetActive(true);
                Vector3 p = target.transform.position;
                _targetRing.transform.position = new Vector3(p.x, 0.02f, p.z);
            }
            else _targetRing.SetActive(false);
        }

        private void Tint(string id, bool dead, bool stunned)
        {
            if (!_renderers.TryGetValue(id, out Renderer renderer)) return;
            Color color = _baseColors[id];
            if (dead) color = Color.Lerp(color, Color.black, 0.75f);
            else if (stunned) color = Color.Lerp(color, new Color(1f, 1f, 0.4f), 0.5f + 0.5f * Mathf.Sin(Time.time * 12f));
            Material material = renderer.material;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }

        /// <summary>Déplace le joueur dans l'arène (purement visuel) et l'oriente.</summary>
        public void MovePlayer(Vector3 delta, float yaw)
        {
            Vector3 p = _player.transform.position + delta;
            Vector3 flat = new Vector3(p.x, 0f, p.z);
            if (flat.magnitude > Radius - 1f) flat = flat.normalized * (Radius - 1f);
            _player.transform.position = new Vector3(flat.x, p.y, flat.z);
            _player.transform.rotation = Quaternion.Slerp(_player.transform.rotation, Quaternion.Euler(0f, yaw, 0f), 0.25f);
        }
    }
}
