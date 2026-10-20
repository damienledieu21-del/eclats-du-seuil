using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Combat;
using EdS.Domain.Space;
using EdS.Simulation.Engine;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot 11.6 : décor d'un combat positionné, construit par le code à partir de l'espace du scénario (formes simples). Le sol a la
    /// taille de l'aire ; chaque obstacle est un prisme de sa forme exacte, habillé selon son identifiant (moulin, rivière, arbre, mur,
    /// pilier ; le pont et le toit en sont déduits) ; les attaques annoncées sont dessinées au sol ; un décor qui cache le joueur à la
    /// caméra est coupé (abaissé, ou retiré pour un feuillage ou un toit). Affichage seulement : collisions, portées et ligne de vue sont
    /// celles du moteur, et la caméra ne change rien au combat.
    /// </summary>
    public sealed class SpaceView
    {
        /// <summary>Décor qui peut cacher le joueur : abaissé (prisme) ou retiré (feuillage, toit) tant qu'il le cache.</summary>
        private sealed class Occluder
        {
            public Transform Transform;
            public Renderer Renderer;
            public bool Lower;
            public Vector3 Min;
            public Vector3 Max;
            public float Cut = 1f;
        }

        /// <summary>Hauteur gardée par un prisme coupé (fraction de sa hauteur).</summary>
        private const float CutHeight = 0.12f;

        private readonly GameObject _root;
        private readonly List<Occluder> _occluders = new List<Occluder>();
        private readonly List<ZoneView> _zones = new List<ZoneView>();
        private readonly Dictionary<string, long> _firstSeen = new Dictionary<string, long>();
        private readonly Material _outline;
        private readonly Material _fill;
        private readonly Material _water;
        private Vector3 _roofMin;
        private Vector3 _roofMax;
        private bool _hasRoof;

        public SpaceView(CombatSpace space, Transform parent)
        {
            _root = new GameObject("Décor");
            _root.transform.SetParent(parent, false);
            _outline = ArenaView.MakeMaterial(new Color(1f, 0.6f, 0.12f));
            _fill = ArenaView.MakeMaterial(new Color(0.85f, 0.16f, 0.1f));
            _water = ArenaView.MakeMaterial(new Color(0.18f, 0.36f, 0.62f));

            float minX = Raw(space.Min.X.Raw), minZ = Raw(space.Min.Z.Raw), maxX = Raw(space.Max.X.Raw), maxZ = Raw(space.Max.Z.Raw);
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Sol";
            Object.Destroy(ground.GetComponent<Collider>());
            ground.transform.SetParent(_root.transform, false);
            ground.transform.position = new Vector3((minX + maxX) / 2f, -0.05f, (minZ + maxZ) / 2f);
            ground.transform.localScale = new Vector3(maxX - minX, 0.1f, maxZ - minZ);
            ground.GetComponent<Renderer>().sharedMaterial = ArenaView.MakeMaterial(new Color(0.33f, 0.42f, 0.25f));
            // Bord de l'aire : quatre murets bas (on ne sort pas de l'aire, le moteur l'interdit).
            Material border = ArenaView.MakeMaterial(new Color(0.3f, 0.27f, 0.22f));
            Block("Bord ouest", new Vector3(minX - 0.15f, 0.25f, (minZ + maxZ) / 2f), new Vector3(0.3f, 0.5f, maxZ - minZ + 0.6f), border);
            Block("Bord est", new Vector3(maxX + 0.15f, 0.25f, (minZ + maxZ) / 2f), new Vector3(0.3f, 0.5f, maxZ - minZ + 0.6f), border);
            Block("Bord sud", new Vector3((minX + maxX) / 2f, 0.25f, minZ - 0.15f), new Vector3(maxX - minX, 0.5f, 0.3f), border);
            Block("Bord nord", new Vector3((minX + maxX) / 2f, 0.25f, maxZ + 0.15f), new Vector3(maxX - minX, 0.5f, 0.3f), border);

            foreach (SpaceObstacle obstacle in space.Obstacles) Obstacle(obstacle);
            Bridge(space);
            Roof(space);
        }

        private static float Raw(long raw) => raw / 10000f;

        private GameObject Block(string name, Vector3 center, Vector3 size, Material material)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            Object.Destroy(block.GetComponent<Collider>());
            block.transform.SetParent(_root.transform, false);
            block.transform.position = center;
            block.transform.localScale = size;
            block.GetComponent<Renderer>().sharedMaterial = material;
            return block;
        }

        // --- Obstacles -------------------------------------------------------------------------------------------------------

        private void Obstacle(SpaceObstacle obstacle)
        {
            string id = obstacle.Id;
            List<Vector3> points = obstacle.Points.Select(p => new Vector3(Raw(p.X.Raw), 0f, Raw(p.Z.Raw))).ToList();
            if (id.StartsWith("riviere"))
            {
                // Eau : prisme bas (la rivière arrête la marche et, au 11.6, la vue : un seul genre d'obstacle dans le moteur).
                Prism(id, points, 0.12f, _water, false);
                return;
            }
            if (id.StartsWith("arbre"))
            {
                Prism(id, points, 2.4f, ArenaView.MakeMaterial(new Color(0.36f, 0.25f, 0.15f)), true);
                Vector3 c = Center(points);
                GameObject leaves = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                leaves.name = id + " (feuillage)";
                Object.Destroy(leaves.GetComponent<Collider>());
                leaves.transform.SetParent(_root.transform, false);
                leaves.transform.position = c + Vector3.up * 3.2f;
                leaves.transform.localScale = new Vector3(3.2f, 2.8f, 3.2f);
                leaves.GetComponent<Renderer>().sharedMaterial = ArenaView.MakeMaterial(new Color(0.2f, 0.45f, 0.18f));
                AddOccluder(leaves.transform, false, c + new Vector3(-1.6f, 1.8f, -1.6f), c + new Vector3(1.6f, 4.6f, 1.6f));
                return;
            }
            if (id.StartsWith("moulin"))
            {
                Prism(id, points, 6f, ArenaView.MakeMaterial(new Color(0.62f, 0.58f, 0.5f)), true);
                Vector3 c = Center(points);
                float half = points.Max(p => Mathf.Max(Mathf.Abs(p.x - c.x), Mathf.Abs(p.z - c.z)));
                GameObject roof = Block(id + " (toit)", c + Vector3.up * 6.6f, new Vector3(half * 2.2f, 1.2f, half * 2.2f), ArenaView.MakeMaterial(new Color(0.45f, 0.2f, 0.15f)));
                AddOccluder(roof.transform, false, c + new Vector3(-half * 1.1f, 6f, -half * 1.1f), c + new Vector3(half * 1.1f, 7.2f, half * 1.1f));
                // Ailes du moulin, sur la face sud.
                Material wood = ArenaView.MakeMaterial(new Color(0.5f, 0.38f, 0.25f));
                Vector3 hub = new Vector3(c.x, 4.5f, c.z - half - 0.3f);
                GameObject a = Block(id + " (aile 1)", hub, new Vector3(7f, 0.4f, 0.1f), wood);
                GameObject b = Block(id + " (aile 2)", hub, new Vector3(0.4f, 7f, 0.1f), wood);
                a.transform.rotation = Quaternion.Euler(0f, 0f, 20f);
                b.transform.rotation = Quaternion.Euler(0f, 0f, 20f);
                AddOccluder(a.transform, false, hub - new Vector3(3.5f, 3.5f, 0.2f), hub + new Vector3(3.5f, 3.5f, 0.2f));
                AddOccluder(b.transform, false, hub - new Vector3(3.5f, 3.5f, 0.2f), hub + new Vector3(3.5f, 3.5f, 0.2f));
                return;
            }
            if (id.StartsWith("mur"))
            {
                Prism(id, points, 3f, ArenaView.MakeMaterial(new Color(0.48f, 0.47f, 0.45f)), true);
                return;
            }
            if (id.StartsWith("pilier"))
            {
                Prism(id, points, 3f, ArenaView.MakeMaterial(new Color(0.42f, 0.3f, 0.2f)), true);
                return;
            }
            Prism(id, points, 2f, ArenaView.MakeMaterial(new Color(0.45f, 0.42f, 0.38f)), true);
        }

        private static Vector3 Center(List<Vector3> points) =>
            new Vector3(points.Average(p => p.x), 0f, points.Average(p => p.z));

        /// <summary>Prisme droit de base <paramref name="points"/> (polygone convexe), de hauteur <paramref name="height"/>, faces vers l'extérieur.</summary>
        private void Prism(string name, List<Vector3> points, float height, Material material, bool occludes)
        {
            Vector3 c = Center(points) + Vector3.up * (height / 2f);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            void Triangle(Vector3 a, Vector3 b, Vector3 d, Vector3 outward)
            {
                // Face visible du côté de sa normale (produit vectoriel, comme Mesh.RecalculateNormals) : ordre corrigé vers l'extérieur.
                Vector3 n = Vector3.Cross(b - a, d - a);
                if (Vector3.Dot(n, outward) < 0f)
                {
                    Vector3 t = b;
                    b = d;
                    d = t;
                }
                int k = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(d);
                triangles.Add(k);
                triangles.Add(k + 1);
                triangles.Add(k + 2);
            }
            Vector3 up = Vector3.up * height;
            for (int i = 1; i + 1 < points.Count; i++) Triangle(points[0] + up, points[i] + up, points[i + 1] + up, Vector3.up);
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 p = points[i], q = points[(i + 1) % points.Count];
                Vector3 outward = (p + q) / 2f + Vector3.up * (height / 2f) - c;
                outward.y = 0f;
                Triangle(p, q, q + up, outward);
                Triangle(p, q + up, p + up, outward);
            }
            var mesh = new Mesh();
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            go.AddComponent<MeshFilter>().mesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            if (!occludes) return;
            var min = new Vector3(points.Min(p => p.x), 0f, points.Min(p => p.z));
            var max = new Vector3(points.Max(p => p.x), height, points.Max(p => p.z));
            _occluders.Add(new Occluder { Transform = go.transform, Renderer = renderer, Lower = true, Min = min, Max = max });
        }

        private void AddOccluder(Transform transform, bool lower, Vector3 min, Vector3 max) =>
            _occluders.Add(new Occluder { Transform = transform, Renderer = transform.GetComponent<Renderer>(), Lower = lower, Min = min, Max = max });

        /// <summary>Pont : tablier entre les deux bras de la rivière (« riviere_sud » et « riviere_nord »), s'ils existent.</summary>
        private void Bridge(CombatSpace space)
        {
            SpaceObstacle south = space.Obstacles.FirstOrDefault(o => o.Id == "riviere_sud");
            SpaceObstacle north = space.Obstacles.FirstOrDefault(o => o.Id == "riviere_nord");
            if (south == null || north == null) return;
            float x0 = Raw(south.Points.Min(p => p.X.Raw)) - 0.6f, x1 = Raw(south.Points.Max(p => p.X.Raw)) + 0.6f;
            float z0 = Raw(south.Points.Max(p => p.Z.Raw)), z1 = Raw(north.Points.Min(p => p.Z.Raw));
            Material wood = ArenaView.MakeMaterial(new Color(0.55f, 0.4f, 0.24f));
            Block("Pont", new Vector3((x0 + x1) / 2f, 0.06f, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.12f, z1 - z0), wood);
            Block("Pont (rambarde sud)", new Vector3((x0 + x1) / 2f, 0.5f, z0 + 0.05f), new Vector3(x1 - x0, 0.1f, 0.1f), wood);
            Block("Pont (rambarde nord)", new Vector3((x0 + x1) / 2f, 0.5f, z1 - 0.05f), new Vector3(x1 - x0, 0.1f, 0.1f), wood);
        }

        /// <summary>Toit : posé sur les piliers (« pilier… ») s'il y en a, il cache ce qui est dessous ; retiré quand le joueur y entre.</summary>
        private void Roof(CombatSpace space)
        {
            List<SpaceObstacle> pillars = space.Obstacles.Where(o => o.Id.StartsWith("pilier")).ToList();
            if (pillars.Count < 2) return;
            float x0 = Raw(pillars.Min(o => o.Points.Min(p => p.X.Raw))) - 0.8f, x1 = Raw(pillars.Max(o => o.Points.Max(p => p.X.Raw))) + 0.8f;
            float z0 = Raw(pillars.Min(o => o.Points.Min(p => p.Z.Raw))) - 0.8f, z1 = Raw(pillars.Max(o => o.Points.Max(p => p.Z.Raw))) + 0.8f;
            GameObject roof = Block("Toit", new Vector3((x0 + x1) / 2f, 3.2f, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.35f, z1 - z0),
                ArenaView.MakeMaterial(new Color(0.5f, 0.22f, 0.16f)));
            _roofMin = new Vector3(x0, 0f, z0);
            _roofMax = new Vector3(x1, 3.4f, z1);
            _hasRoof = true;
            AddOccluder(roof.transform, false, new Vector3(x0, 3f, z0), new Vector3(x1, 3.4f, z1));
        }

        // --- Coupe des décors qui cachent le joueur --------------------------------------------------------------------------

        /// <summary>
        /// Coupe ce qui se trouve entre la caméra et le joueur (et le toit quand il est dessous) ; le reste reprend sa taille. Seul
        /// l'affichage change : un ennemi derrière une paroi reste caché (<see cref="EdS.Runtime.OtherState.InSight"/>).
        /// </summary>
        public void Cut(Vector3 camera, Vector3 player, float deltaTime)
        {
            Vector3 head = player + Vector3.up * 1f;
            bool underRoof = _hasRoof && player.x >= _roofMin.x && player.x <= _roofMax.x && player.z >= _roofMin.z && player.z <= _roofMax.z;
            foreach (Occluder o in _occluders)
            {
                bool masks = SegmentHitsBox(camera, head, o.Min, o.Max) || underRoof && o.Min.y >= 2.9f && Overlaps(o, _roofMin, _roofMax);
                float target = masks ? CutHeight : 1f;
                o.Cut = Mathf.MoveTowards(o.Cut, target, deltaTime * 4f);
                if (o.Lower)
                {
                    Vector3 s = o.Transform.localScale;
                    o.Transform.localScale = new Vector3(s.x, o.Cut, s.z);
                }
                else if (o.Renderer != null) o.Renderer.enabled = o.Cut > 0.5f;
            }
        }

        private static bool Overlaps(Occluder o, Vector3 min, Vector3 max) =>
            o.Max.x >= min.x && o.Min.x <= max.x && o.Max.z >= min.z && o.Min.z <= max.z;

        /// <summary>Le segment [a, b] traverse-t-il la boîte alignée [min, max] ? (méthode des dalles)</summary>
        private static bool SegmentHitsBox(Vector3 a, Vector3 b, Vector3 min, Vector3 max)
        {
            float t0 = 0f, t1 = 1f;
            Vector3 d = b - a;
            for (int axis = 0; axis < 3; axis++)
            {
                float start = axis == 0 ? a.x : axis == 1 ? a.y : a.z;
                float delta = axis == 0 ? d.x : axis == 1 ? d.y : d.z;
                float lo = axis == 0 ? min.x : axis == 1 ? min.y : min.z;
                float hi = axis == 0 ? max.x : axis == 1 ? max.y : max.z;
                if (Mathf.Abs(delta) < 1e-6f)
                {
                    if (start < lo || start > hi) return false;
                    continue;
                }
                float u = (lo - start) / delta, v = (hi - start) / delta;
                if (u > v)
                {
                    float t = u;
                    u = v;
                    v = t;
                }
                t0 = Mathf.Max(t0, u);
                t1 = Mathf.Min(t1, v);
                if (t0 > t1) return false;
            }
            return true;
        }

        // --- Attaques annoncées au sol -------------------------------------------------------------------------------------

        /// <summary>
        /// Dessine les attaques annoncées : contour orange de la forme verrouillée, remplissage rouge qui grandit jusqu'à l'impact ; une
        /// zone qui reste est pleine pendant ses impulsions. Une attaque dirigée annoncée (sans forme) est un cercle sous sa cible.
        /// </summary>
        public void Zones(IReadOnlyList<AnnouncedAttack> announced, double now, System.Func<string, Vector3?> position)
        {
            var seen = new HashSet<string>();
            int used = 0;
            foreach (AnnouncedAttack attack in announced)
            {
                string key = attack.Actor + "/" + attack.Attack + "/" + attack.ImpactTick;
                seen.Add(key);
                if (!_firstSeen.TryGetValue(key, out long first))
                {
                    first = (long)now;
                    _firstSeen[key] = first;
                }
                double span = System.Math.Max(1, attack.ImpactTick - first);
                float progress = Mathf.Clamp01((float)((now - first) / span));
                if (used == _zones.Count) _zones.Add(new ZoneView(_root.transform, _outline, _fill));
                ZoneView zone = _zones[used++];
                if (attack.Zone != null) zone.Show(attack.Zone, progress);
                else if (attack.Target != null && position(attack.Target) is Vector3 p) zone.ShowCircle(p.x, p.z, 1.1f, progress);
                else zone.Hide();
            }
            for (int i = used; i < _zones.Count; i++) _zones[i].Hide();
            foreach (string gone in _firstSeen.Keys.Where(k => !seen.Contains(k)).ToList()) _firstSeen.Remove(gone);
        }

        public void Destroy() => Object.Destroy(_root);
    }

    /// <summary>
    /// Sous-lot 11.6 : une forme d'attaque annoncée au sol (contour et remplissage), maillages refaits à chaque image. Revue Codex de la
    /// PR #34 : dessinée au-dessus de la plus haute surface où l'on marche (tablier du pont, eau, décor abaissé : 0,12 m), et une bande a
    /// les bouts arrondis du moteur (distance au segment : deux demi-disques aux extrémités).
    /// </summary>
    internal sealed class ZoneView
    {
        private const int Segments = 40;
        private const int CapSegments = 16;
        private const float Band = 0.12f;
        private const float Height = 0.16f;
        private readonly GameObject _outline;
        private readonly GameObject _fill;
        private readonly Mesh _outlineMesh = new Mesh();
        private readonly Mesh _fillMesh = new Mesh();

        public ZoneView(Transform parent, Material outline, Material fill)
        {
            _outline = Part("Annonce (contour)", parent, outline, _outlineMesh, Height + 0.005f);
            _fill = Part("Annonce", parent, fill, _fillMesh, Height);
        }

        private static GameObject Part(string name, Transform parent, Material material, Mesh mesh, float height)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0f, height, 0f);
            go.AddComponent<MeshFilter>().mesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        public void Hide()
        {
            _outline.SetActive(false);
            _fill.SetActive(false);
        }

        public void Show(AttackZone zone, float progress)
        {
            float ox = zone.OriginX / 10000f, oz = zone.OriginZ / 10000f, reach = zone.Reach / 10000f;
            float ax = zone.AxisX / 10000f, az = zone.AxisZ / 10000f;
            switch (zone.Shape)
            {
                case AttackShape.Cone:
                {
                    float axis = Mathf.Atan2(az, ax), half = zone.Angle * Mathf.Deg2Rad / 2f;
                    ConeOutline(_outlineMesh, ox, oz, reach, axis - half, axis + half);
                    Sector(_fillMesh, ox, oz, 0f, Mathf.Max(0.05f, reach * progress), axis - half, axis + half);
                    break;
                }
                case AttackShape.Line:
                {
                    float w = zone.HalfWidth / 10000f;
                    Stadium(_outlineMesh, ox, oz, ax, az, reach, w, true);
                    Stadium(_fillMesh, ox, oz, ax, az, Mathf.Max(0.05f, reach * progress), w, false);
                    break;
                }
                default:   // cercle, zone qui reste
                    ShowCircle(ox, oz, reach, progress);
                    return;
            }
            _outline.SetActive(true);
            _fill.SetActive(true);
        }

        public void ShowCircle(float x, float z, float radius, float progress)
        {
            Sector(_outlineMesh, x, z, radius - Band, radius, 0f, 2f * Mathf.PI);
            Sector(_fillMesh, x, z, 0f, Mathf.Max(0.05f, radius * progress), 0f, 2f * Mathf.PI);
            _outline.SetActive(true);
            _fill.SetActive(true);
        }

        /// <summary>Couronne (ou disque, rayon intérieur nul) entre les angles <paramref name="from"/> et <paramref name="to"/>, face vers le haut.</summary>
        private static void Sector(Mesh mesh, float x, float z, float inner, float outer, float from, float to)
        {
            var vertices = new Vector3[(Segments + 1) * 2];
            var triangles = new int[Segments * 6];
            for (int i = 0; i <= Segments; i++)
            {
                float a = Mathf.Lerp(from, to, i / (float)Segments);
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                vertices[2 * i] = new Vector3(x + c * Mathf.Max(0f, inner), 0f, z + s * Mathf.Max(0f, inner));
                vertices[2 * i + 1] = new Vector3(x + c * outer, 0f, z + s * outer);
            }
            for (int i = 0; i < Segments; i++)
            {
                // Angles croissants (sens trigonométrique vu de dessus) : (intérieur, extérieur suivant, extérieur) regarde vers le haut.
                int k = 6 * i, v = 2 * i;
                triangles[k] = v;
                triangles[k + 1] = v + 3;
                triangles[k + 2] = v + 1;
                triangles[k + 3] = v;
                triangles[k + 4] = v + 2;
                triangles[k + 5] = v + 3;
            }
            Set(mesh, vertices, triangles);
        }

        /// <summary>
        /// Revue Codex de la PR #34 : contour d'un cône : l'arc au bout (couronne de largeur <see cref="Band"/>) et les deux rayons qui le
        /// bornent (bandes de même largeur, à l'intérieur du cône), pour voir ses côtés avant que le remplissage ait grandi.
        /// </summary>
        private static void ConeOutline(Mesh mesh, float x, float z, float reach, float from, float to)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i <= Segments; i++)
            {
                float a = Mathf.Lerp(from, to, i / (float)Segments);
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                vertices.Add(new Vector3(x + c * Mathf.Max(0f, reach - Band), 0f, z + s * Mathf.Max(0f, reach - Band)));
                vertices.Add(new Vector3(x + c * reach, 0f, z + s * reach));
            }
            for (int i = 0; i < Segments; i++)
            {
                int v = 2 * i;
                triangles.AddRange(new[] { v, v + 3, v + 1, v, v + 2, v + 3 });
            }
            // Côtés : du sommet au bout de chaque rayon, la bande décalée vers l'intérieur du cône (normale tournée vers l'autre rayon).
            void Side(float angle, float inward)
            {
                float c = Mathf.Cos(angle), s = Mathf.Sin(angle), nx = -s * inward * Band, nz = c * inward * Band;
                int b = vertices.Count;
                vertices.Add(new Vector3(x, 0f, z));
                vertices.Add(new Vector3(x + c * reach, 0f, z + s * reach));
                vertices.Add(new Vector3(x + c * reach + nx, 0f, z + s * reach + nz));
                vertices.Add(new Vector3(x + nx, 0f, z + nz));
                // Les deux faces : l'ordre des coins dépend du côté, on garde le quadrilatère visible d'en haut dans les deux cas.
                triangles.AddRange(inward > 0f ? new[] { b, b + 2, b + 1, b, b + 3, b + 2 } : new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            }
            Side(from, 1f);
            Side(to, -1f);
            Set(mesh, vertices.ToArray(), triangles.ToArray());
        }

        /// <summary>
        /// Contour fermé d'une bande à bouts arrondis (« stade ») de longueur <paramref name="length"/> et de rayon <paramref name="r"/> le
        /// long de l'axe, dans le sens trigonométrique vu de dessus : demi-cercle autour du bout, puis demi-cercle autour de l'origine.
        /// </summary>
        private static List<Vector3> StadiumLoop(float x, float z, float ax, float az, float length, float r)
        {
            var points = new List<Vector3>();
            float axis = Mathf.Atan2(az, ax);
            for (int i = 0; i <= CapSegments; i++)
            {
                float a = axis - Mathf.PI / 2f + Mathf.PI * i / CapSegments;
                points.Add(new Vector3(x + ax * length + Mathf.Cos(a) * r, 0f, z + az * length + Mathf.Sin(a) * r));
            }
            for (int i = 0; i <= CapSegments; i++)
            {
                float a = axis + Mathf.PI / 2f + Mathf.PI * i / CapSegments;
                points.Add(new Vector3(x + Mathf.Cos(a) * r, 0f, z + Mathf.Sin(a) * r));
            }
            return points;
        }

        /// <summary>
        /// Bande de longueur <paramref name="length"/> et de demi-largeur <paramref name="half"/> le long de l'axe, bouts arrondis comme
        /// dans le moteur ; contour (couronne de largeur <see cref="Band"/>) ou pleine (éventail depuis le milieu, la forme est convexe).
        /// </summary>
        private static void Stadium(Mesh mesh, float x, float z, float ax, float az, float length, float half, bool outline)
        {
            List<Vector3> outer = StadiumLoop(x, z, ax, az, length, half);
            int n = outer.Count;
            if (outline)
            {
                List<Vector3> inner = StadiumLoop(x, z, ax, az, length, Mathf.Max(0f, half - Band));
                var vertices = new Vector3[2 * n];
                var triangles = new int[6 * n];
                for (int i = 0; i < n; i++)
                {
                    vertices[2 * i] = inner[i];
                    vertices[2 * i + 1] = outer[i];
                }
                for (int i = 0; i < n; i++)
                {
                    // Comme une couronne de Sector : (intérieur, extérieur suivant, extérieur) et (intérieur, intérieur suivant, extérieur
                    // suivant) regardent vers le haut ; le dernier quadrilatère referme le contour.
                    int v = 2 * i, w = 2 * ((i + 1) % n), k = 6 * i;
                    triangles[k] = v;
                    triangles[k + 1] = w + 1;
                    triangles[k + 2] = v + 1;
                    triangles[k + 3] = v;
                    triangles[k + 4] = w;
                    triangles[k + 5] = w + 1;
                }
                Set(mesh, vertices, triangles);
                return;
            }
            var fill = new Vector3[n + 1];
            var fan = new int[3 * n];
            fill[0] = new Vector3(x + ax * length / 2f, 0f, z + az * length / 2f);
            for (int i = 0; i < n; i++) fill[i + 1] = outer[i];
            for (int i = 0; i < n; i++)
            {
                fan[3 * i] = 0;
                fan[3 * i + 1] = 1 + (i + 1) % n;
                fan[3 * i + 2] = 1 + i;
            }
            Set(mesh, fill, fan);
        }

        private static void Set(Mesh mesh, Vector3[] vertices, int[] triangles)
        {
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
    }
}
