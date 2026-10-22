using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.Simulation.Engine;
using UnityEngine;
using UnityEngine.Rendering;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot 9.3 : arène construite par le code (graphismes provisoires : formes simples). Le joueur est une capsule bleue avec un
    /// bouclier visible en garde (doré pendant la fenêtre de parade) ; les assaillants sont des capsules rouges, le mannequin un
    /// cylindre, un allié une capsule verte ; la cible est cerclée de jaune, l'allié visé par les soutiens de vert (lot 10, à deux). Les
    /// positions sont purement visuelles : le cœur n'a pas de système de positions. Sert au combat solo comme au combat en réseau
    /// (<see cref="ICombatClient"/>, sous-lot 10.4). Sous-lot 11.6 : dans un combat positionné, le décor vient de l'espace du scénario
    /// (<see cref="SpaceView"/>), chaque combattant est à la place que lui donne le moteur (entre deux ticks, l'affichage glisse de la
    /// place précédente à la suivante), avec son orientation et sa taille ; un ennemi hors de vue du joueur n'est pas affiché.
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
        private GameObject _allyRing;
        private Material _shieldGuard;
        private Material _shieldParry;
        private Material _shieldBroken;

        // Sous-lot 11.6 : combat positionné (null sinon) ; places du tick précédent et du tick affiché, pour glisser entre les deux.
        private SpaceView _space;
        private GameObject _defaultGround;
        private readonly Dictionary<string, Vector3> _from = new Dictionary<string, Vector3>();
        private readonly Dictionary<string, Vector3> _to = new Dictionary<string, Vector3>();
        private long _shownTick = -1;

        public Transform Player => _player != null ? _player.transform : null;

        /// <summary>Combat positionné affiché (décor du scénario, places du moteur).</summary>
        public bool Spatial => _space != null;

        public ArenaView()
        {
            _root = new GameObject("Arène");
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ground.name = "Sol";
            ground.transform.SetParent(_root.transform, false);
            ground.transform.localScale = new Vector3(Radius * 2f + 2f, 0.05f, Radius * 2f + 2f);
            ground.transform.position = new Vector3(0f, -0.05f, 0f);
            ground.GetComponent<Renderer>().sharedMaterial = MakeMaterial(new Color(0.22f, 0.23f, 0.26f));
            _defaultGround = ground;

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
        public void Populate(ICombatClient combat)
        {
            foreach (GameObject body in _bodies.Values) Object.Destroy(body);
            _bodies.Clear();
            _renderers.Clear();
            _baseColors.Clear();
            if (_player != null) Object.Destroy(_player);
            if (_targetRing != null) Object.Destroy(_targetRing);
            if (_allyRing != null) Object.Destroy(_allyRing);
            if (_space != null) _space.Destroy();
            _space = combat.Space != null ? new SpaceView(combat.Space, _root.transform) : null;
            _defaultGround.SetActive(_space == null);
            _from.Clear();
            _to.Clear();
            _shownTick = -1;

            _player = Body(combat.PlayerId, PrimitiveType.Capsule, new Color(0.25f, 0.45f, 0.95f), Vector3.zero);
            Fit(_player, combat.Player().View);
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
                Fit(body, other.View);
            }
            if (_space != null) Place(combat, true);

            _targetRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _targetRing.name = "Cible";
            Object.Destroy(_targetRing.GetComponent<Collider>());
            _targetRing.transform.SetParent(_root.transform, false);
            _targetRing.transform.localScale = new Vector3(1.6f, 0.01f, 1.6f);
            _targetRing.GetComponent<Renderer>().sharedMaterial = MakeMaterial(new Color(1f, 0.85f, 0.2f));

            _allyRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _allyRing.name = "Allié visé";
            Object.Destroy(_allyRing.GetComponent<Collider>());
            _allyRing.transform.SetParent(_root.transform, false);
            _allyRing.transform.localScale = new Vector3(1.4f, 0.01f, 1.4f);
            _allyRing.GetComponent<Renderer>().sharedMaterial = MakeMaterial(new Color(0.3f, 0.9f, 0.45f));
            _allyRing.SetActive(false);
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
        public Vector3? Head(string id) => _bodies.TryGetValue(id, out GameObject body) && body.activeSelf ? body.transform.position + Vector3.up * 1.3f : (Vector3?)null;

        /// <summary>Position au sol d'un combattant affiché, ou null.</summary>
        public Vector3? Ground(string id)
        {
            if (!_bodies.TryGetValue(id, out GameObject body) || !body.activeSelf) return null;
            Vector3 p = body.transform.position;
            return new Vector3(p.x, 0f, p.z);
        }

        /// <summary>Sous-lot 11.6 : taille de l'emprise du moteur (capsule de 1 m de diamètre à l'échelle 1).</summary>
        private static void Fit(GameObject body, CombatantView view)
        {
            if (view.Radius is EdS.Domain.Numerics.Fixed r)
            {
                float d = r.Raw / 5000f;   // diamètre en mètres
                body.transform.localScale = new Vector3(d, 1f, d);
            }
        }

        private static Vector3 World(GroundVector p) => new Vector3(p.X.Raw / 10000f, 0f, p.Z.Raw / 10000f);

        private static float Yaw(GroundVector facing) => Mathf.Atan2(facing.X.Raw, facing.Z.Raw) * Mathf.Rad2Deg;

        /// <summary>
        /// Sous-lot 11.6 : places du moteur. Au changement de tick, la place affichée part de celle du tick précédent vers la nouvelle ;
        /// entre deux ticks, elle glisse selon <see cref="ICombatClient.TickProgress"/>. Un absent, ou un ennemi hors de vue du joueur,
        /// n'est pas affiché. Sous-lot 11.7 : en réseau, le joueur est montré à sa place prédite (<see cref="ICombatClient.PredictedPosition"/>).
        /// </summary>
        private void Place(ICombatClient combat, bool snap)
        {
            CombatantView me = combat.Player().View;
            var views = new List<CombatantView> { me };
            // Revue Codex de la PR #32 (20e tour) : le joueur absent (avant son arrivée, après son départ) reste caché.
            var visible = new Dictionary<string, bool> { [combat.PlayerId] = me.IsPresent };
            foreach (OtherState other in combat.Others())
            {
                views.Add(other.View);
                visible[other.Id] = other.View.IsPresent && (!other.IsEnemy || other.InSight);
            }
            bool newTick = combat.Tick != _shownTick;
            _shownTick = combat.Tick;
            float t = snap ? 1f : (float)combat.TickProgress;
            (double X, double Z)? predicted = combat.PredictedPosition();
            foreach (CombatantView v in views)
            {
                if (!_bodies.TryGetValue(v.Id, out GameObject body)) continue;
                // Revue Codex de la PR #34 : un combattant sans place (renfort pas encore arrivé) est caché, barre comprise (Head le suit).
                if (v.Position is not GroundVector p)
                {
                    body.SetActive(false);
                    continue;
                }
                Vector3 target = World(p);
                if (snap || !_to.ContainsKey(v.Id))
                {
                    _from[v.Id] = target;
                    _to[v.Id] = target;
                }
                else if (newTick)
                {
                    _from[v.Id] = _to[v.Id];
                    _to[v.Id] = target;
                }
                Vector3 place = Vector3.Lerp(_from[v.Id], _to[v.Id], t) + Vector3.up;
                // Sous-lot 11.7 : en réseau, le joueur est montré à sa place prédite, rejointe en douceur (un écart corrigé glisse).
                if (v.Id == combat.PlayerId && predicted.HasValue)
                {
                    Vector3 aim = new Vector3((float)predicted.Value.X, 1f, (float)predicted.Value.Z);
                    place = snap ? aim : Vector3.Lerp(body.transform.position, aim, 1f - Mathf.Exp(-18f * Time.deltaTime));
                }
                body.transform.position = place;
                if (v.Facing is GroundVector f)
                    body.transform.rotation = Quaternion.Slerp(body.transform.rotation, Quaternion.Euler(0f, Yaw(f), 0f), snap ? 1f : 0.3f);
                body.SetActive(visible.TryGetValue(v.Id, out bool shown) && shown);
            }
        }

        /// <summary>Sous-lot 11.6 : attaques annoncées au sol et coupe du décor qui cache le joueur (combat positionné seulement).</summary>
        public void SyncSpace(ICombatClient combat, Vector3 camera, float deltaTime)
        {
            if (_space == null || _player == null) return;
            _space.Zones(combat.Announced(), combat.Tick + combat.TickProgress, Ground);
            _space.Cut(camera, _player.transform.position - Vector3.up, deltaTime);
        }

        /// <summary>
        /// Met l'arène à l'état du combat : garde, parade, cible, allié visé (null : aucun), combattants hors de combat, adversaires
        /// tournés vers le joueur.
        /// </summary>
        public void Sync(ICombatClient combat, PlayerState player, string allyId)
        {
            CombatantView me = player.View;
            bool broken = me.States.Contains("guardBreak");
            // Revue Codex de la PR #32 (20e tour) : le joueur absent (avant son arrivée, après son départ) est caché, bouclier compris.
            _player.SetActive(me.IsPresent);
            _shield.SetActive(me.IsPresent && (me.Guarding || me.ParryOpen || broken));
            _shieldRenderer.sharedMaterial = me.ParryOpen ? _shieldParry : broken ? _shieldBroken : _shieldGuard;
            if (_space != null) Place(combat, false);

            foreach (OtherState other in combat.Others())
            {
                if (!_bodies.TryGetValue(other.Id, out GameObject body)) continue;
                if (_space != null)
                {
                    // Place gère déjà la visibilité en combat positionné (présent et en vue).
                    Tint(other.Id, other.View.IsDead, other.View.States.Contains("stun"));
                    continue;   // orientation donnée par le moteur
                }
                // Revue Codex de la PR #32 (16e tour) : un absent (attendu, refusé, parti) est caché ; il apparaît à son arrivée.
                body.SetActive(other.View.IsPresent);
                if (!other.View.IsPresent) continue;
                Vector3 look = _player.transform.position;
                look.y = body.transform.position.y;
                if (other.IsEnemy && other.View.Kind != CombatantKind.Dummy && (look - body.transform.position).sqrMagnitude > 0.01f)
                    body.transform.rotation = Quaternion.Slerp(body.transform.rotation, Quaternion.LookRotation(look - body.transform.position), 0.15f);
                Tint(other.Id, other.View.IsDead, other.View.States.Contains("stun"));
            }
            Tint(combat.PlayerId, me.IsDead, me.States.Contains("stun"));

            if (player.TargetId != null && _bodies.TryGetValue(player.TargetId, out GameObject target) && target.activeSelf)
            {
                _targetRing.SetActive(true);
                Vector3 p = target.transform.position;
                _targetRing.transform.position = new Vector3(p.x, 0.02f, p.z);
            }
            else _targetRing.SetActive(false);

            if (allyId != null && _bodies.TryGetValue(allyId, out GameObject ally))
            {
                _allyRing.SetActive(true);
                Vector3 p = ally.transform.position;
                _allyRing.transform.position = new Vector3(p.x, 0.015f, p.z);
            }
            else _allyRing.SetActive(false);
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

        /// <summary>Déplace le joueur dans l'arène (purement visuel) et l'oriente ; sans effet dans un combat positionné (le moteur le déplace).</summary>
        public void MovePlayer(Vector3 delta, float yaw)
        {
            if (_space != null) return;
            Vector3 p = _player.transform.position + delta;
            Vector3 flat = new Vector3(p.x, 0f, p.z);
            if (flat.magnitude > Radius - 1f) flat = flat.normalized * (Radius - 1f);
            _player.transform.position = new Vector3(flat.x, p.y, flat.z);
            _player.transform.rotation = Quaternion.Slerp(_player.transform.rotation, Quaternion.Euler(0f, yaw, 0f), 0.25f);
        }
    }
}
