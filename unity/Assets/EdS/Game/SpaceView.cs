using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Combat;
using EdS.Domain.Space;
using EdS.Runtime;
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

        /// <summary>
        /// Hauteur gardée par un prisme coupé, en mètres (sous les annonces au sol, dessinées à 0,16 m). Revue Codex de la PR #34 : le prisme
        /// est mis à l'échelle de cette hauteur rapportée à la sienne (un mur de 3 m garde 0,12 m, pas 12 % de sa hauteur).
        /// </summary>
        private const float CutHeight = 0.12f;

        private readonly GameObject _root;
        private readonly List<Occluder> _occluders = new List<Occluder>();
        private readonly List<ZoneView> _zones = new List<ZoneView>();
        private readonly Dictionary<string, long> _firstSeen = new Dictionary<string, long>();
        private readonly Material _outline;
        private readonly Material _fill;
        private readonly Material _water;
        private readonly GameObject _terrain;

        /// <summary>Essai réaliste : polygones d'eau dont le lit est creusé dans le terrain (vide sans la vraie eau ou sans terrain).</summary>
        private readonly List<List<Vector3>> _rivers;
        private Vector3 _roofMin;
        private Vector3 _roofMax;
        private bool _hasRoof;

        public SpaceView(CombatSpace space, Transform parent)
        {
            _root = new GameObject("Décor");
            _root.transform.SetParent(parent, false);
            _outline = ArenaView.MakeMaterial(new Color(1f, 0.6f, 0.12f));
            _fill = ArenaView.MakeMaterial(new Color(0.85f, 0.16f, 0.1f));
            // Essai réaliste : l'eau de la rivière (vagues qui glissent) si le catalogue est là, sinon le bleu simple.
            _water = StyleWorld.Water() ?? ArenaView.MakeMaterial(new Color(0.18f, 0.36f, 0.62f));

            float minX = Raw(space.Min.X.Raw), minZ = Raw(space.Min.Z.Raw), maxX = Raw(space.Max.X.Raw), maxZ = Raw(space.Max.Z.Raw);
            // Essai réaliste : un terrain peint (terre, roche, herbe) à la place du sol simple, s'il y a des textures de sol. Essai du 27 octobre
            // 2026 : avec la vraie eau, le lit de la rivière y est creusé (sous ses bras et sous le pont), l'eau coule sous le sol.
            _rivers = StyleWorld.RealWater ? Rivers(space) : new List<List<Vector3>>();
            _terrain = StyleWorld.Ground(_root.transform, minX, minZ, maxX, maxZ, Spots(space),
                _rivers.Select(r => (IReadOnlyList<(double X, double Z)>)r.Select(p => ((double)p.x, (double)p.z)).ToList()).ToList());
            bool terrain = _terrain != null;
            if (!terrain) _rivers.Clear();
            if (!terrain)
            {
                GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.name = "Sol";
                Object.Destroy(ground.GetComponent<Collider>());
                ground.transform.SetParent(_root.transform, false);
                ground.transform.position = new Vector3((minX + maxX) / 2f, -0.05f, (minZ + maxZ) / 2f);
                ground.transform.localScale = new Vector3(maxX - minX, 0.1f, maxZ - minZ);
                ground.GetComponent<Renderer>().sharedMaterial = ArenaView.MakeMaterial(new Color(0.33f, 0.42f, 0.25f));
            }
            // Bord de l'aire : quatre murets bas (on ne sort pas de l'aire, le moteur l'interdit). Essai réaliste : en pavés.
            Material border = Built(StyleSurface.Cobble, new Color(0.3f, 0.27f, 0.22f), out float borderTile);
            Box("Bord ouest", new Vector3(minX - 0.15f, 0.25f, (minZ + maxZ) / 2f), new Vector3(0.3f, 0.5f, maxZ - minZ + 0.6f), border, borderTile);
            Box("Bord est", new Vector3(maxX + 0.15f, 0.25f, (minZ + maxZ) / 2f), new Vector3(0.3f, 0.5f, maxZ - minZ + 0.6f), border, borderTile);
            Box("Bord sud", new Vector3((minX + maxX) / 2f, 0.25f, minZ - 0.15f), new Vector3(maxX - minX, 0.5f, 0.3f), border, borderTile);
            Box("Bord nord", new Vector3((minX + maxX) / 2f, 0.25f, maxZ + 0.15f), new Vector3(maxX - minX, 0.5f, 0.3f), border, borderTile);

            foreach (SpaceObstacle obstacle in space.Obstacles) Obstacle(obstacle);
            Plants(space);
            if (terrain) Forest(space);
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
                // Essai réaliste (27 octobre 2026) : la vraie eau coule sous le sol, entre ses berges (le lit est creusé dans le terrain).
                if (_rivers.Count > 0)
                {
                    WaterSurface(id, points);
                    return;
                }
                // Eau : prisme bas (la rivière arrête la marche et, au 11.6, la vue : un seul genre d'obstacle dans le moteur).
                Prism(id, points, 0.12f, _water, false, 4f);
                return;
            }
            // Essai réaliste (27 octobre 2026) : palissade de rondins pour les murs et la porte, haie d'arbustes pour les haies.
            StyleFence fence = StyleRules.FenceFor(id);
            if (fence == StyleFence.Palisade)
            {
                Palisade(id, points);
                return;
            }
            if (fence == StyleFence.Hedge && Hedge(id, points)) return;
            if (id.StartsWith("arbre"))
            {
                // Essai de style : un arbre des packs à la place du tronc et de sa boule de feuillage (même emprise au moteur).
                GameObject tree = StyleLook.Decor(_root.transform, StyleSlot.Tree, id, Center(points), 5f);
                if (tree != null)
                {
                    AddModelOccluders(tree);
                    return;
                }
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
                // Essai réaliste (choix de Damien du 27 octobre 2026) : le moulin de Sketchfab, sa tour sur l'emprise, ses ailes qui tournent.
                float side = Mathf.Min(points.Max(p => p.x) - points.Min(p => p.x), points.Max(p => p.z) - points.Min(p => p.z));
                GameObject mill = StyleLook.Building(_root.transform, StyleProp.Mill, id, Center(points), 0f, side);
                if (mill != null)
                {
                    AddModelOccluders(mill);
                    return;
                }
                Material walls = Built(StyleSurface.Plaster, new Color(0.62f, 0.58f, 0.5f), out float wallTile);
                Prism(id, points, 6f, walls, true, wallTile);
                Vector3 c = Center(points);
                float half = points.Max(p => Mathf.Max(Mathf.Abs(p.x - c.x), Mathf.Abs(p.z - c.z)));
                Material tiles = Built(StyleSurface.Roof, new Color(0.45f, 0.2f, 0.15f), out float roofTile);
                GameObject roof = Box(id + " (toit)", c + Vector3.up * 6.6f, new Vector3(half * 2.2f, 1.2f, half * 2.2f), tiles, roofTile);
                AddOccluder(roof.transform, false, c + new Vector3(-half * 1.1f, 6f, -half * 1.1f), c + new Vector3(half * 1.1f, 7.2f, half * 1.1f));
                // Ailes du moulin, sur la face sud (essai réaliste : en planches).
                Material wood = Built(StyleSurface.Wood, new Color(0.5f, 0.38f, 0.25f), out float sailTile);
                Vector3 hub = new Vector3(c.x, 4.5f, c.z - half - 0.3f);
                GameObject a = Block(id + " (aile 1)", hub, new Vector3(7f, 0.4f, 0.1f), Tiled(wood, 7f / sailTile, 0.4f / sailTile));
                GameObject b = Block(id + " (aile 2)", hub, new Vector3(0.4f, 7f, 0.1f), Tiled(wood, 0.4f / sailTile, 7f / sailTile));
                a.transform.rotation = Quaternion.Euler(0f, 0f, 20f);
                b.transform.rotation = Quaternion.Euler(0f, 0f, 20f);
                AddOccluder(a.transform, false, hub - new Vector3(3.5f, 3.5f, 0.2f), hub + new Vector3(3.5f, 3.5f, 0.2f));
                AddOccluder(b.transform, false, hub - new Vector3(3.5f, 3.5f, 0.2f), hub + new Vector3(3.5f, 3.5f, 0.2f));
                return;
            }
            if (id.StartsWith("pilier"))
            {
                Prism(id, points, 3f, Built(StyleSurface.Wood, new Color(0.42f, 0.3f, 0.2f), out float pillarTile), true, pillarTile);
                return;
            }
            // Essai de style : un rocher des packs sur l'emprise des blocs de la carrière et des pierres du Seuil.
            if (StyleRules.ForObstacle(id) == StyleSlot.Rock)
            {
                float width = Mathf.Max(points.Max(p => p.x) - points.Min(p => p.x), points.Max(p => p.z) - points.Min(p => p.z));
                GameObject rock = StyleLook.Decor(_root.transform, StyleSlot.Rock, id, Center(points), 0f, width * 1.1f);
                if (rock != null)
                {
                    AddModelOccluders(rock);
                    return;
                }
            }
            Prism(id, points, 2f, Built(StyleSurface.Stone, new Color(0.45f, 0.42f, 0.38f), out float blockTile), true, blockTile);
        }

        /// <summary>
        /// Essai de style : quelques plantes basses semées sur le sol praticable, hors des obstacles (simple décor : elles n'arrêtent rien,
        /// le moteur ne les connaît pas). Places tirées d'une suite fixe : les mêmes à chaque lancement.
        /// </summary>
        private void Plants(CombatSpace space)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            if (catalog == null || catalog.Plants == null || catalog.Plants.Length == 0) return;
            float minX = Raw(space.Min.X.Raw), minZ = Raw(space.Min.Z.Raw), maxX = Raw(space.Max.X.Raw), maxZ = Raw(space.Max.Z.Raw);
            var boxes = space.Obstacles.Select(o => (MinX: Raw(o.Points.Min(p => p.X.Raw)) - 1f, MaxX: Raw(o.Points.Max(p => p.X.Raw)) + 1f,
                MinZ: Raw(o.Points.Min(p => p.Z.Raw)) - 1f, MaxZ: Raw(o.Points.Max(p => p.Z.Raw)) + 1f)).ToList();
            uint seed = 12345;
            float Next()
            {
                seed = seed * 1664525u + 1013904223u;
                return (seed >> 8) / 16777216f;
            }
            // Essai réaliste : nombre réglé dans le catalogue (moitié sur téléphone), plantes à leur taille réelle quand elle est plausible.
            int wanted = Application.isMobilePlatform ? catalog.PlantCount / 2 : catalog.PlantCount;
            int placed = 0;
            for (int i = 0; i < wanted * 8 && placed < wanted; i++)
            {
                float x = Mathf.Lerp(minX + 1f, maxX - 1f, Next()), z = Mathf.Lerp(minZ + 1f, maxZ - 1f, Next());
                if (boxes.Any(b => x >= b.MinX && x <= b.MaxX && z >= b.MinZ && z <= b.MaxZ)) continue;
                StyleLook.Decor(_root.transform, StyleSlot.Plant, "plante" + placed, new Vector3(x, 0f, z), 0.6f + 0.4f * Next(), 0f, true);
                placed++;
            }
        }

        /// <summary>
        /// Essai réaliste : la forêt autour de l'aire (hors de l'aire : le moteur ne la connaît pas, elle n'arrête rien), sur le terrain qui
        /// déborde de l'aire ; le tiers des arbres sur téléphone. Places fixes (<see cref="StyleRules.Forest"/>).
        /// </summary>
        private void Forest(CombatSpace space)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            if (catalog == null || catalog.Trees == null || catalog.Trees.Length == 0) return;
            int count = Application.isMobilePlatform ? catalog.ForestTrees / 3 : catalog.ForestTrees;
            var spots = StyleRules.Forest(Raw(space.Min.X.Raw), Raw(space.Min.Z.Raw), Raw(space.Max.X.Raw), Raw(space.Max.Z.Raw), catalog.ForestMargin, count);
            bool debris = catalog.Debris != null && catalog.Debris.Length > 0;
            for (int i = 0; i < spots.Count; i++)
            {
                string id = "lisiere" + i;
                var at = new Vector3((float)spots[i].X, 0f, (float)spots[i].Z);
                // Une place sur quatre : du bois mort (souche, tronc couché) à sa taille réelle, s'il y en a.
                GameObject look = debris && i % 4 == 3
                    ? StyleLook.Decor(_root.transform, StyleSlot.Debris, id, at, 1f, 0f, true)
                    : StyleLook.Decor(_root.transform, StyleSlot.Tree, id, at, 5f + 0.035f * StyleRules.Variant(id, 100));
                // Revue Codex de la PR #56 : en vue 2, la caméra (6 m derrière le Bastion) peut entrer dans la lisière quand il longe le bord ;
                // ces arbres sont coupés comme ceux des obstacles quand ils le cachent.
                if (look != null) AddModelOccluders(look);
            }
        }

        /// <summary>
        /// Essai réaliste : les endroits qui changent la couche du sol (roche autour des blocs, des pierres du Seuil, des murs et des
        /// piliers ; terre autour des arbres et du moulin), d'après les obstacles (<see cref="StyleRules.GroundFor"/>).
        /// </summary>
        private static List<StyleSpot> Spots(CombatSpace space)
        {
            var spots = new List<StyleSpot>();
            foreach (SpaceObstacle obstacle in space.Obstacles)
            {
                if (!(StyleRules.GroundFor(obstacle.Id) is StyleGround ground)) continue;
                float x0 = Raw(obstacle.Points.Min(p => p.X.Raw)), x1 = Raw(obstacle.Points.Max(p => p.X.Raw));
                float z0 = Raw(obstacle.Points.Min(p => p.Z.Raw)), z1 = Raw(obstacle.Points.Max(p => p.Z.Raw));
                float radius = Mathf.Max(x1 - x0, z1 - z0) / 2f + (ground == StyleGround.Rock ? 1.5f : 1f);
                spots.Add(new StyleSpot((x0 + x1) / 2f, (z0 + z1) / 2f, radius, ground));
            }
            return spots;
        }

        private static Vector3 Center(List<Vector3> points) =>
            new Vector3(points.Average(p => p.x), 0f, points.Average(p => p.z));

        /// <summary>
        /// Essai réaliste : l'eau à creuser dans le terrain : chaque bras de rivière (« riviere… »), et la coupure entre « riviere_sud » et
        /// « riviere_nord » sous le pont (la rivière y coule aussi ; on la passe sur le pont, le moteur n'y met pas d'obstacle).
        /// </summary>
        private static List<List<Vector3>> Rivers(CombatSpace space)
        {
            var rivers = space.Obstacles.Where(o => o.Id.StartsWith("riviere"))
                .Select(o => o.Points.Select(p => new Vector3(Raw(p.X.Raw), 0f, Raw(p.Z.Raw))).ToList()).ToList();
            if (BridgeGap(space) is (float x0, float z0, float x1, float z1))
                rivers.Add(new List<Vector3> { new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z0), new Vector3(x1, 0f, z1), new Vector3(x0, 0f, z1) });
            return rivers;
        }

        /// <summary>Coupure entre les deux bras de la rivière (bande x0..x1 de la rivière, z0..z1 entre les bras), ou null.</summary>
        private static (float X0, float Z0, float X1, float Z1)? BridgeGap(CombatSpace space)
        {
            SpaceObstacle south = space.Obstacles.FirstOrDefault(o => o.Id == "riviere_sud");
            SpaceObstacle north = space.Obstacles.FirstOrDefault(o => o.Id == "riviere_nord");
            if (south == null || north == null) return null;
            float z0 = Raw(south.Points.Max(p => p.Z.Raw)), z1 = Raw(north.Points.Min(p => p.Z.Raw));
            if (z1 <= z0) return null;
            return (Raw(south.Points.Min(p => p.X.Raw)), z0, Raw(south.Points.Max(p => p.X.Raw)), z1);
        }

        /// <summary>
        /// Essai réaliste (demande de Damien du 27 octobre 2026) : surface de l'eau, à <see cref="StyleRules.WaterLevel"/> sous le sol, sur
        /// le polygone de la rivière (ses berges creusées l'entourent) ; matériau « EdS/Eau ».
        /// </summary>
        private GameObject WaterSurface(string name, List<Vector3> points)
        {
            float y = (float)StyleRules.WaterLevel;
            var vertices = points.Select(p => new Vector3(p.x, y, p.z)).ToList();
            var triangles = new List<int>();
            for (int i = 1; i + 1 < vertices.Count; i++)
            {
                // Face vers le haut quel que soit le sens des sommets.
                Vector3 n = Vector3.Cross(vertices[i] - vertices[0], vertices[i + 1] - vertices[0]);
                if (n.y >= 0f) triangles.AddRange(new[] { 0, i, i + 1 });
                else triangles.AddRange(new[] { 0, i + 1, i });
            }
            var mesh = new Mesh();
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject(name + " (eau)");
            go.transform.SetParent(_root.transform, false);
            go.AddComponent<MeshFilter>().mesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = _water;
            return go;
        }

        /// <summary>
        /// Essai réaliste (choix de Damien du 27 octobre 2026 : « Rondins faits par moi ») : palissade de rondins taillés en pointe sur
        /// l'emprise d'un mur ou d'une porte (<see cref="StyleRules.Palisade"/>), un seul maillage, écorce du catalogue (planches à défaut).
        /// Abaissée comme un mur quand elle cache le joueur.
        /// </summary>
        private void Palisade(string id, List<Vector3> points)
        {
            float x0 = points.Min(p => p.x), x1 = points.Max(p => p.x), z0 = points.Min(p => p.z), z1 = points.Max(p => p.z);
            IReadOnlyList<StyleLog> logs = StyleRules.Palisade(x0, z0, x1, z1, id);
            Material bark = StyleWorld.Surface(StyleSurface.Bark, out float tile) ?? Built(StyleSurface.Wood, new Color(0.4f, 0.3f, 0.2f), out tile);
            const int Sides = 10;
            const float Tip = 0.38f;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            float scale = 1f / Mathf.Max(0.1f, tile);
            float top = 0f;
            foreach (StyleLog log in logs)
            {
                float r = (float)log.Radius, h = (float)log.Height, cx = (float)log.X, cz = (float)log.Z;
                top = Mathf.Max(top, h);
                // Chaque rondin tourné autrement, pour que l'écorce ne se répète pas d'un rondin à l'autre.
                float turn = StyleRules.Variant(id + "/" + vertices.Count, 360) * Mathf.Deg2Rad;
                // Fût : anneaux du bas (sous le sol) et du haut, couture doublée pour la texture.
                int start = vertices.Count;
                for (int ring = 0; ring < 2; ring++)
                    for (int s = 0; s <= Sides; s++)
                    {
                        float a = 2f * Mathf.PI * s / Sides + turn;
                        float y = ring == 0 ? -0.2f : h - Tip;
                        vertices.Add(new Vector3(cx + Mathf.Cos(a) * r, y, cz + Mathf.Sin(a) * r));
                        uvs.Add(new Vector2(2f * Mathf.PI * r * s / Sides * scale, y * scale));
                    }
                for (int s = 0; s < Sides; s++)
                {
                    int a = start + s, b = start + s + 1, c = start + Sides + 1 + s, d = start + Sides + 2 + s;
                    // Vu de dehors : (bas, haut, bas suivant) tourne dans le sens des aiguilles d'une montre (angles croissants).
                    triangles.AddRange(new[] { a, c, b, b, c, d });
                }
                // Pointe taillée : un cône de l'anneau du haut jusqu'au sommet.
                int apex = vertices.Count;
                vertices.Add(new Vector3(cx, h, cz));
                uvs.Add(new Vector2(Mathf.PI * r * scale, h * scale));
                int ringTop = vertices.Count;
                for (int s = 0; s <= Sides; s++)
                {
                    float a = 2f * Mathf.PI * s / Sides + turn;
                    vertices.Add(new Vector3(cx + Mathf.Cos(a) * r, h - Tip, cz + Mathf.Sin(a) * r));
                    uvs.Add(new Vector2(2f * Mathf.PI * r * s / Sides * scale, (h - Tip) * scale));
                }
                for (int s = 0; s < Sides; s++) triangles.AddRange(new[] { ringTop + s, apex, ringTop + s + 1 });
            }
            var mesh = new Mesh();
            mesh.vertices = vertices.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();   // revue Codex de la PR #58 (3e tour) : le relief de l'écorce, comme celui des prismes
            mesh.RecalculateBounds();
            var go = new GameObject(id + " (palissade)");
            go.transform.SetParent(_root.transform, false);
            go.AddComponent<MeshFilter>().mesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = bark;
            _occluders.Add(new Occluder { Transform = go.transform, Renderer = renderer, Lower = true, Min = new Vector3(x0, 0f, z0), Max = new Vector3(x1, top, z1) });
        }

        /// <summary>
        /// Essai réaliste (27 octobre 2026) : haie d'arbustes réalistes le long d'une haie (jusque-là un bloc de pierre), un arbuste par
        /// mètre environ (moins sur téléphone), retirés quand ils cachent le joueur. False sans arbuste dans le catalogue.
        /// </summary>
        private bool Hedge(string id, List<Vector3> points)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            if (catalog == null || catalog.Shrubs == null || catalog.Shrubs.Length == 0) return false;
            float x0 = points.Min(p => p.x), x1 = points.Max(p => p.x), z0 = points.Min(p => p.z), z1 = points.Max(p => p.z);
            var spots = StyleRules.Row(x0, z0, x1, z1, Application.isMobilePlatform ? 1.6 : 1.1);
            for (int i = 0; i < spots.Count; i++)
            {
                string part = id + "/" + i;
                GameObject shrub = StyleLook.Decor(_root.transform, StyleSlot.Shrub, part, new Vector3((float)spots[i].X, 0f, (float)spots[i].Z),
                    1.6f + 0.004f * StyleRules.Variant(part, 100));
                if (shrub != null) AddModelOccluders(shrub);
            }
            return true;
        }

        /// <summary>Prisme droit de base <paramref name="points"/> (polygone convexe), de hauteur <paramref name="height"/>, faces vers l'extérieur.</summary>
        /// <summary>
        /// Prisme droit de base <paramref name="points"/> (polygone convexe, posé à la hauteur de ses points), de hauteur
        /// <paramref name="height"/>, faces vers l'extérieur. Essai réaliste : coordonnées de texture à l'échelle du monde (une répétition
        /// tous les <paramref name="tile"/> mètres ; dessus en x et z, flancs le long du bord et en hauteur), pour la pierre, le crépi, les
        /// planches et l'eau.
        /// </summary>
        private GameObject Prism(string name, List<Vector3> points, float height, Material material, bool occludes, float tile = 2.5f)
        {
            Vector3 c = Center(points) + Vector3.up * (height / 2f);
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            float scale = 1f / Mathf.Max(0.1f, tile);
            void Triangle(Vector3 a, Vector3 b, Vector3 d, Vector3 outward, System.Func<Vector3, Vector2> uv)
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
                uvs.Add(uv(a) * scale);
                uvs.Add(uv(b) * scale);
                uvs.Add(uv(d) * scale);
                triangles.Add(k);
                triangles.Add(k + 1);
                triangles.Add(k + 2);
            }
            Vector3 up = Vector3.up * height;
            System.Func<Vector3, Vector2> top = v => new Vector2(v.x, v.z);
            for (int i = 1; i + 1 < points.Count; i++) Triangle(points[0] + up, points[i] + up, points[i + 1] + up, Vector3.up, top);
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 p = points[i], q = points[(i + 1) % points.Count];
                Vector3 outward = (p + q) / 2f + Vector3.up * (height / 2f) - c;
                outward.y = 0f;
                Vector3 along = q - p;
                along.y = 0f;
                along = along.normalized;
                System.Func<Vector3, Vector2> side = v => new Vector2(Vector3.Dot(v - p, along), v.y);
                Triangle(p, q, q + up, outward, side);
                Triangle(p, q + up, p + up, outward, side);
            }
            var mesh = new Mesh();
            mesh.vertices = vertices.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            // Revue Codex de la PR #57 (2e tour) : les cartes de relief (murs, tuiles, planches, vagues) ont besoin des tangentes.
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            go.AddComponent<MeshFilter>().mesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            if (!occludes) return go;
            var min = new Vector3(points.Min(p => p.x), 0f, points.Min(p => p.z));
            var max = new Vector3(points.Max(p => p.x), height, points.Max(p => p.z));
            _occluders.Add(new Occluder { Transform = go.transform, Renderer = renderer, Lower = true, Min = min, Max = max });
            return go;
        }

        /// <summary>
        /// Essai réaliste : pavé droit aligné (centre, taille), en prisme pour avoir des textures à l'échelle du monde ; même place que le
        /// bloc simple qu'il remplace.
        /// </summary>
        private GameObject Box(string name, Vector3 center, Vector3 size, Material material, float tile)
        {
            float y = center.y - size.y / 2f, hx = size.x / 2f, hz = size.z / 2f;
            var points = new List<Vector3>
            {
                new Vector3(center.x - hx, y, center.z - hz), new Vector3(center.x + hx, y, center.z - hz),
                new Vector3(center.x + hx, y, center.z + hz), new Vector3(center.x - hx, y, center.z + hz),
            };
            return Prism(name, points, size.y, material, false, tile);
        }

        /// <summary>Matériau d'une matière de bâtiment (textures du catalogue), ou la couleur simple sans elles.</summary>
        private static Material Built(StyleSurface kind, Color color, out float tile) =>
            StyleWorld.Surface(kind, out tile) ?? ArenaView.MakeMaterial(color);

        /// <summary>Copie d'un matériau texturé répétée (u, v) fois sur un bloc simple (les ailes du moulin, tournées).</summary>
        private static Material Tiled(Material material, float u, float v)
        {
            var copy = new Material(material);
            copy.SetTextureScale("_BaseMap", new Vector2(Mathf.Max(0.05f, u), Mathf.Max(0.05f, v)));
            return copy;
        }

        private void AddOccluder(Transform transform, bool lower, Vector3 min, Vector3 max) =>
            _occluders.Add(new Occluder { Transform = transform, Renderer = transform.GetComponent<Renderer>(), Lower = lower, Min = min, Max = max });

        /// <summary>
        /// Essai de style (revue Codex de la PR #55) : un arbre ou un rocher des packs garde la coupe du décor qu'il remplace : chacune de ses
        /// parties est retirée tant que le modèle (sa boîte entière) se trouve entre la caméra et le joueur.
        /// </summary>
        private void AddModelOccluders(GameObject model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) return;
            Bounds box = renderers[0].bounds;
            foreach (Renderer r in renderers) box.Encapsulate(r.bounds);
            foreach (Renderer r in renderers)
                _occluders.Add(new Occluder { Transform = r.transform, Renderer = r, Lower = false, Min = box.min, Max = box.max });
        }

        /// <summary>
        /// Pont : tablier entre les deux bras de la rivière (« riviere_sud » et « riviere_nord »), s'ils existent. La rivière coule suivant z
        /// (bande x0..x1) ; le passage est la coupure entre ses deux bras (z0..z1), et le pont se traverse suivant x, d'une rive à l'autre
        /// (revue Codex de la PR #34 : axes précisés). Les rambardes longent donc le passage suivant x, à z0 et z1, au bord des deux bras.
        /// </summary>
        private void Bridge(CombatSpace space)
        {
            SpaceObstacle south = space.Obstacles.FirstOrDefault(o => o.Id == "riviere_sud");
            SpaceObstacle north = space.Obstacles.FirstOrDefault(o => o.Id == "riviere_nord");
            if (south == null || north == null) return;
            float x0 = Raw(south.Points.Min(p => p.X.Raw)) - 0.6f, x1 = Raw(south.Points.Max(p => p.X.Raw)) + 0.6f;
            float z0 = Raw(south.Points.Max(p => p.Z.Raw)), z1 = Raw(north.Points.Min(p => p.Z.Raw));
            // Essai réaliste (demande de Damien du 27 octobre 2026 : « le pont n'est pas réaliste ») : la rivière coule sous le pont, et le
            // pont est un tronçon du ponton de Poly Haven, tablier au ras du sol, pieux dans le lit.
            if (_rivers.Count > 0 && BridgeGap(space) is (float gx0, float gz0, float gx1, float gz1))
                WaterSurface("Rivière (sous le pont)", new List<Vector3> { new Vector3(gx0, 0f, gz0), new Vector3(gx1, 0f, gz0), new Vector3(gx1, 0f, gz1), new Vector3(gx0, 0f, gz1) });
            if (StyleLook.Bridge(_root.transform, new Vector3((x0 + x1) / 2f, 0f, (z0 + z1) / 2f), x1 - x0, 0.08f) != null) return;
            Material wood = Built(StyleSurface.Wood, new Color(0.55f, 0.4f, 0.24f), out float tile);
            Box("Pont", new Vector3((x0 + x1) / 2f, 0.06f, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.12f, z1 - z0), wood, tile);
            Box("Pont (rambarde sud)", new Vector3((x0 + x1) / 2f, 0.5f, z0 + 0.05f), new Vector3(x1 - x0, 0.1f, 0.1f), wood, tile);
            Box("Pont (rambarde nord)", new Vector3((x0 + x1) / 2f, 0.5f, z1 - 0.05f), new Vector3(x1 - x0, 0.1f, 0.1f), wood, tile);
        }

        /// <summary>Toit : posé sur les piliers (« pilier… ») s'il y en a, il cache ce qui est dessous ; retiré quand le joueur y entre.</summary>
        private void Roof(CombatSpace space)
        {
            List<SpaceObstacle> pillars = space.Obstacles.Where(o => o.Id.StartsWith("pilier")).ToList();
            if (pillars.Count < 2) return;
            float x0 = Raw(pillars.Min(o => o.Points.Min(p => p.X.Raw))) - 0.8f, x1 = Raw(pillars.Max(o => o.Points.Max(p => p.X.Raw))) + 0.8f;
            float z0 = Raw(pillars.Min(o => o.Points.Min(p => p.Z.Raw))) - 0.8f, z1 = Raw(pillars.Max(o => o.Points.Max(p => p.Z.Raw))) + 0.8f;
            Material tiles = Built(StyleSurface.Roof, new Color(0.5f, 0.22f, 0.16f), out float tile);
            GameObject roof = Box("Toit", new Vector3((x0 + x1) / 2f, 3.2f, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.35f, z1 - z0), tiles, tile);
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
            StyleWorld.AnimateWater(Time.time);   // essai réaliste : les vagues de la rivière glissent
            Vector3 head = player + Vector3.up * 1f;
            bool underRoof = _hasRoof && player.x >= _roofMin.x && player.x <= _roofMax.x && player.z >= _roofMin.z && player.z <= _roofMax.z;
            foreach (Occluder o in _occluders)
            {
                bool masks = SegmentHitsBox(camera, head, o.Min, o.Max) || underRoof && o.Min.y >= 2.9f && Overlaps(o, _roofMin, _roofMax);
                float height = o.Max.y - o.Min.y;
                float target = !masks ? 1f : o.Lower && height > CutHeight ? CutHeight / height : CutHeight;
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

        public void Destroy()
        {
            StyleWorld.Release(_terrain);   // essai réaliste : données du terrain créées par le code, rendues avec lui
            Object.Destroy(_root);
        }
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
