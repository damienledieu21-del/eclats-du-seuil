using System.Collections.Generic;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.Simulation.Engine;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V2.28b (3) : corps des créatures des <b>autres</b> secteurs ouverts (<see cref="SectorCrowd"/>), à côté de ceux de la session
    /// du personnage (<see cref="ArenaView"/>). Chaque corps glisse d'un tick du monde à l'autre ; un ennemi hors de vue du personnage
    /// (décor de l'aire entière) est caché, comme dans la session. Un corps est fait à la première vue de sa créature (modèle du catalogue,
    /// sinon forme simple) et gardé tant que sa fiche existe : aucune allocation par image. Pas d'écho fantôme, d'oursons ni de voix pour
    /// ces créatures lointaines (limite connue) ; elles les ont dès que le personnage entre dans leur secteur.
    /// </summary>
    public sealed class SectorCrowdView
    {
        private sealed class Shown
        {
            public GameObject Body;
            public StyleBody Look;
            public WildlifeLook Wild;
            public bool Active;
        }

        private static readonly Dictionary<Color, Material> Materials = new Dictionary<Color, Material>();
        private readonly Transform _parent;
        private readonly Dictionary<SectorCrowd.Member, Shown> _bodies = new Dictionary<SectorCrowd.Member, Shown>();
        private readonly List<SectorCrowd.Member> _gone = new List<SectorCrowd.Member>();
        private long _generation = -1;

        public SectorCrowdView(Transform parent)
        {
            _parent = parent;
        }

        /// <summary>Corps montrés à la dernière image.</summary>
        public int Visible { get; private set; }

        /// <summary>Corps faits (montrés ou non).</summary>
        public int Bodies => _bodies.Count;

        /// <summary>
        /// Met les corps à la fraction <paramref name="t"/> du tick ; <paramref name="eye"/> : place montrée du personnage (null : tout
        /// ennemi est en vue) ; <paramref name="sight"/> : obstacles de vue de l'aire entière.
        /// </summary>
        public void Sync(SectorCrowd crowd, double t, Vector3? eye, IReadOnlyList<ConvexPolygon> sight, System.Func<string, Color?> paint, float deltaTime)
        {
            if (crowd.Generation != _generation) Prune(crowd);
            IReadOnlyList<SectorCrowd.Member> members = crowd.Members;
            int visible = 0;
            for (int i = 0; i < members.Count; i++)
            {
                SectorCrowd.Member m = members[i];
                if (!_bodies.TryGetValue(m, out Shown b))
                {
                    if (!m.Shown) continue;
                    b = Make(m, paint);
                    _bodies[m] = b;
                }
                bool show = m.Shown && m.Track.Known;
                float x = (float)m.Track.X(t), z = (float)m.Track.Z(t);
                if (show && m.Enemy && eye.HasValue && sight != null) show = InSight(eye.Value, x, z, sight);
                if (b.Active != show)
                {
                    b.Body.SetActive(show);
                    b.Active = show;
                }
                if (!show) continue;
                visible++;
                var place = new Vector3(x, 1f + StyleWorld.StandHeight(x, z), z);
                Transform body = b.Body.transform;
                body.position = place;
                if (m.FacingX != 0 || m.FacingZ != 0)
                {
                    float aim = Mathf.Atan2(m.FacingX, m.FacingZ) * Mathf.Rad2Deg;
                    bool beast = (b.Look != null && b.Look.IsBeast) || b.Wild != null;
                    double rate = Locomotion.MaxTurnRate(b.Look != null ? b.Look.Speed : 1f, beast);
                    body.rotation = Quaternion.Euler(0f, (float)Locomotion.TurnStep(body.eulerAngles.y, aim, rate, 0.06, deltaTime), 0f);
                }
                if (b.Look != null) b.Look.Update(place, false, deltaTime);
                if (b.Wild != null) b.Wild.Update(place, false, deltaTime);
            }
            Visible = visible;
        }

        /// <summary>Ajoute à la carte les ennemis montrés (comme ceux de la session : en vue seulement).</summary>
        public void Enemies(SectorCrowd crowd, List<MapEnemy> into, System.Func<string, bool> boss)
        {
            IReadOnlyList<SectorCrowd.Member> members = crowd.Members;
            for (int i = 0; i < members.Count; i++)
            {
                SectorCrowd.Member m = members[i];
                if (!m.Enemy || !_bodies.TryGetValue(m, out Shown b) || !b.Active) continue;
                Vector3 p = b.Body.transform.position;
                into.Add(new MapEnemy(p.x, p.z, boss != null && boss(m.Id)));
            }
        }

        /// <summary>Détruit tous les corps (session nouvelle, autre zone).</summary>
        public void Clear()
        {
            foreach (Shown b in _bodies.Values)
                if (b.Body != null) Object.Destroy(b.Body);
            _bodies.Clear();
            _generation = -1;
            Visible = 0;
        }

        private void Prune(SectorCrowd crowd)
        {
            _generation = crowd.Generation;
            var alive = new HashSet<SectorCrowd.Member>(crowd.Members);
            _gone.Clear();
            foreach (SectorCrowd.Member m in _bodies.Keys)
                if (!alive.Contains(m)) _gone.Add(m);
            foreach (SectorCrowd.Member m in _gone)
            {
                if (_bodies[m].Body != null) Object.Destroy(_bodies[m].Body);
                _bodies.Remove(m);
            }
            _gone.Clear();
        }

        private Shown Make(SectorCrowd.Member m, System.Func<string, Color?> paint)
        {
            GameObject body = GameObject.CreatePrimitive(m.Kind == CombatantKind.Dummy ? PrimitiveType.Cylinder : PrimitiveType.Capsule);
            body.name = m.Key;
            Object.Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(_parent, false);
            Color color = (paint != null ? paint(m.Id) : null) ?? (!m.Enemy ? new Color(0.3f, 0.8f, 0.4f)
                : m.Kind == CombatantKind.Dummy ? new Color(0.6f, 0.45f, 0.3f) : new Color(0.85f, 0.2f, 0.2f));
            if (!Materials.TryGetValue(color, out Material material) || material == null)
            {
                material = ArenaView.MakeMaterial(color);
                Materials[color] = material;   // un matériau partagé par couleur
            }
            body.GetComponent<Renderer>().sharedMaterial = material;
            float d = (float)(2.0 * m.Radius);
            if (d > 0f) body.transform.localScale = new Vector3(d, 1f, d);
            StyleSlot slot = StyleRules.ForCombatant(m.Id, false);
            StyleBody look = StyleLook.Dress(body, slot, m.Id, slot == StyleSlot.Hero || slot == StyleSlot.Character ? 1.8f : (float)StyleRules.BeastHeight(m.Id));
            WildlifeLook wild = look == null && Wildlife.KindOf(m.Id) is WildlifeKind kind && kind != WildlifeKind.None ? new WildlifeLook(body, kind) : null;
            body.SetActive(false);
            return new Shown { Body = body, Look = look, Wild = wild, Active = false };
        }

        private static bool InSight(Vector3 eye, float x, float z, IReadOnlyList<ConvexPolygon> sight)
        {
            long ax = Raw(eye.x), az = Raw(eye.z), bx = Raw(x), bz = Raw(z);
            for (int i = 0; i < sight.Count; i++)
                if (sight[i].IntersectsSegment(ax, az, bx, bz)) return false;
            return true;
        }

        private static long Raw(float metres) => (long)System.Math.Round(metres * 10000.0);
    }
}
