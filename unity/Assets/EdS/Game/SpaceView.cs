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
    public sealed partial class SpaceView
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

            /// <summary>V1.10 : décor au-dessus (poutres) : il disparaît en fondu dès que la caméra arrive à sa hauteur.</summary>
            public bool Overhead;

            /// <summary>V1.10 : fondu préparé (renderers, matériaux d'origine et leurs copies transparentes), ou non encore regardé.</summary>
            public bool FadeChecked;
            public Renderer[] Faded;
            public Material[][] Solid;
            public Material[][] Clear;
            public float[][] Alpha;
            public bool Clearing;

            /// <summary>V1.10 (relecture) : état d'origine de chaque renderer (un renderer éteint ailleurs le reste).</summary>
            public bool[] WasEnabled;

            /// <summary>V1.10 (relecture) : dernière opacité appliquée, et si le décor était dessiné (rien n'est réappliqué sans changement).</summary>
            public float Applied = 1f;
            public bool AppliedShown = true;

            /// <summary>V1.10 (relecture) : la caméra est-elle à hauteur de ce décor au-dessus (avec hystérésis) ?</summary>
            public bool AtHeight;
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
        private readonly Material _fill;
        private readonly Material _water;
        private readonly GameObject _terrain;

        /// <summary>Essai réaliste : polygones d'eau dont le lit est creusé dans le terrain (vide sans la vraie eau ou sans terrain).</summary>
        private readonly List<List<Vector3>> _rivers;
        private Vector3 _roofMin;
        private Vector3 _roofMax;
        private bool _hasRoof;

        /// <summary>Vallons agrandis : routes et champs de la carte de l'aventure (vides hors aventure), taches du sol rangées par cases.</summary>
        private readonly IReadOnlyList<StyleRoad> _roads;

        /// <summary>Intérieurs : portes de la zone (vide hors aventure).</summary>
        private readonly IReadOnlyList<MapDoor> _doors;
        private readonly IReadOnlyList<StyleField> _fields;
        private readonly SpotGrid _spots;
        private readonly List<StyleGrass> _crops = new List<StyleGrass>();

        /// <summary>Relecture des Vallons agrandis : maillages faits par le code (rendus à la destruction du décor).</summary>
        private readonly List<Mesh> _meshes = new List<Mesh>();

        /// <summary>Obstacles en polygones avec leur boîte (herbe et plantes : hors du polygone, pas de sa boîte).</summary>
        private readonly List<(float MinX, float MinZ, float MaxX, float MaxZ, List<(double X, double Z)> Points)> _shapes =
            new List<(float MinX, float MinZ, float MaxX, float MaxZ, List<(double X, double Z)> Points)>();

        /// <summary>
        /// Vallons agrandis : clé du décor (aire, obstacles, routes). Le même espace et la même carte gardent le même décor : une halte ne le
        /// reconstruit plus (la zone est grande : terrain, bois, bourg) ; une porte ouverte change la clé, et le décor est refait.
        /// </summary>
        public string Key { get; }

        /// <summary>Clé du décor d'un espace et d'une carte (<see cref="Key"/>).</summary>
        public static string KeyOf(CombatSpace space, AdventureMap map)
        {
            var key = new System.Text.StringBuilder();
            key.Append(space.Min.X.Raw).Append(',').Append(space.Min.Z.Raw).Append(',').Append(space.Max.X.Raw).Append(',').Append(space.Max.Z.Raw);
            foreach (SpaceObstacle o in space.Obstacles) key.Append('|').Append(o.Id).Append(':').Append(o.Points.Count);
            if (map != null)
                foreach (StyleRoad road in map.Roads) key.Append('/').Append(road.Id);
            return key.ToString();
        }

        public SpaceView(CombatSpace space, Transform parent, AdventureMap map = null)
        {
            Key = KeyOf(space, map);
            _space = space;
            foreach (SpaceObstacle o in space.Obstacles)
                _shapes.Add((Raw(o.Points.Min(p => p.X.Raw)), Raw(o.Points.Min(p => p.Z.Raw)), Raw(o.Points.Max(p => p.X.Raw)), Raw(o.Points.Max(p => p.Z.Raw)),
                    o.Points.Select(p => ((double)Raw(p.X.Raw), (double)Raw(p.Z.Raw))).ToList()));
            _roads = map != null ? map.Roads : (IReadOnlyList<StyleRoad>)new List<StyleRoad>();
            _doors = map != null ? map.Doors : (IReadOnlyList<MapDoor>)new List<MapDoor>();
            _spots = new SpotGrid(Spots(space));
            if (map != null)
            {
                var boxes = space.Obstacles.Select(o => ((double)Raw(o.Points.Min(p => p.X.Raw)), (double)Raw(o.Points.Min(p => p.Z.Raw)),
                    (double)Raw(o.Points.Max(p => p.X.Raw)), (double)Raw(o.Points.Max(p => p.Z.Raw)))).ToList();
                _fields = map.Places.Where(p => TownRules.Farmland(p.Id))
                    .SelectMany(p => TownRules.Fields(p.MinX, p.MinZ, p.MaxX, p.MaxZ, boxes, _roads, map.Clearings, (map.MinX, map.MinZ, map.MaxX, map.MaxZ))).ToList();
            }
            else _fields = new List<StyleField>();
            _root = new GameObject("Décor");
            _root.transform.SetParent(parent, false);
            _fill = ArenaView.MakeMaterial(new Color(0.85f, 0.16f, 0.1f));   // annonce sans le shader « EdS/Annonce »
            // Essai réaliste : l'eau de la rivière (vagues qui glissent) si le catalogue est là, sinon le bleu simple.
            _water = StyleWorld.Water() ?? ArenaView.MakeMaterial(new Color(0.18f, 0.36f, 0.62f));

            // Intérieurs : une pièce (plancher, murs, meubles, lumières), sans terrain, herbe ni ciel.
            if (map != null && map.Indoor)
            {
                _rivers = new List<List<Vector3>>();
                Interior(space, map);
                return;
            }
            float minX = Raw(space.Min.X.Raw), minZ = Raw(space.Min.Z.Raw), maxX = Raw(space.Max.X.Raw), maxZ = Raw(space.Max.Z.Raw);
            // Essai réaliste : un terrain peint (terre, roche, herbe) à la place du sol simple, s'il y a des textures de sol. Essai du 27 octobre
            // 2026 : avec la vraie eau, le lit de la rivière y est creusé (sous ses bras et sous le pont), l'eau coule sous le sol.
            _rivers = StyleWorld.RealWater ? Rivers(space) : new List<List<Vector3>>();
            _terrain = StyleWorld.Ground(_root.transform, minX, minZ, maxX, maxZ, Spots(space),
                _rivers.Select(r => (IReadOnlyList<(double X, double Z)>)r.Select(p => ((double)p.x, (double)p.z)).ToList()).ToList(), _roads, _fields,
                _shapes.Where((o, i) => TownRules.BuildingFor(space.Obstacles[i].Id) == StyleBuilding.Woods).Select(o => (IReadOnlyList<(double X, double Z)>)o.Points).ToList());
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
            Pavements();
            Fields();
            Villagers(map);   // essai réaliste 8 : les villageois du bourg
            Livestock(map);   // sous-lot V2.9 (2e vague) : vaches, chevaux, chiens, corbeaux, cris de la ferme
            NightDecor(space, map);   // sous-lot V2.1 : lanternes, réverbères, feux follets, hiboux
            WeatherDecor();   // sous-lot V2.2 : flaques le long des chemins
            Plants(space);
            if (terrain) Forest(space);
            Bridge(space);
            Roof(space);
            if (terrain) Grass(space);
            // Essai réaliste 4 : la rivière reflète ce qui l'entoure (sonde prise une fois, tout le décor posé).
            if (_waters.Count > 0)
            {
                float rx0 = _rivers.Min(r => r.Min(p => p.x)), rx1 = _rivers.Max(r => r.Max(p => p.x));
                float rz0 = _rivers.Min(r => r.Min(p => p.z)), rz1 = _rivers.Max(r => r.Max(p => p.z));
                StyleWorld.Reflections(_root.transform, new Vector3((rx0 + rx1) / 2f, 1.5f, (rz0 + rz1) / 2f),
                    new Vector3(maxX - minX + 40f, 40f, maxZ - minZ + 40f), _waters);
            }
        }

        /// <summary>Essai réaliste 4 : surfaces d'eau posées (pour la sonde de reflets, qui ne doit pas les voir).</summary>
        private readonly List<GameObject> _waters = new List<GameObject>();

        /// <summary>Essai réaliste 4 : herbe en brins (null sans son matériau).</summary>
        private StyleGrass _grass;

        /// <summary>
        /// Essai réaliste 4 (demande de Damien du 27 octobre 2026 : « le maximum de réalisme ») : herbe en brins sur l'aire, là où le sol
        /// est en herbe (<see cref="StyleRules.GroundMix"/>), hors des obstacles et loin de l'eau (<see cref="StyleGrass"/>).
        /// </summary>
        private void Grass(CombatSpace space)
        {
            float minX = Raw(space.Min.X.Raw), minZ = Raw(space.Min.Z.Raw), maxX = Raw(space.Max.X.Raw), maxZ = Raw(space.Max.Z.Raw);
            var rivers = _rivers.Select(r => (IReadOnlyList<(double X, double Z)>)r.Select(p => ((double)p.x, (double)p.z)).ToList()).ToList();
            var banks = StyleRules.UnionBoundary(rivers);
            // Vallons agrandis : ni sur les routes (terre battue), ni dans les champs (le blé y pousse) ; relecture : hors du polygone des
            // obstacles (à 40 cm près), pas de leur boîte (l'herbe manquait autour des bois, du lac, de la tour et des tentes).
            _grass = StyleGrass.Create(minX, minZ, maxX, maxZ, (x, z) =>
                !InObstacle(x, z, 0.4f)
                && !InField(x, z, 0.3f)
                && (banks.Count == 0 || StyleRules.UnionDistance(x, z, rivers, banks) < -0.7)
                && TownRules.RoadMix(_spots.Mix(x, z), x, z, TownRules.RoadAt(x, z, _roads)).Grass >= 0.5);
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
            // Essai réaliste 5 (choix de Damien du 27 octobre 2026 : « le mur de feuilles, je ne suis pas fan » → muret de pierres sèches).
            if (fence == StyleFence.Hedge)
            {
                DryStoneWall(id, points);
                return;
            }
            // Vallons agrandis : bois, maisons, auberge, chapelle, grange, puits, étals, tour de guet, tentes.
            StyleBuilding building = TownRules.BuildingFor(id);
            if (building == StyleBuilding.Woods)
            {
                Woods(id, points);
                return;
            }
            if (building != StyleBuilding.None)
            {
                Building(building, id, points);
                return;
            }
            if (id.StartsWith("arbre"))
            {
                // Essai de style : un arbre des packs à la place du tronc et de sa boule de feuillage (même emprise au moteur).
                // Essai réaliste 4 : hauteur des arbres réglée dans le catalogue (les arbres de Sketchfab sont grands).
                GameObject tree = StyleLook.Decor(_root.transform, StyleSlot.Tree, id, Center(points), TreeHeight() * 0.8f);
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
            // Essai réaliste 5 : les piliers sont les poteaux de l'abri, construits avec lui (Roof).
            if (id.StartsWith("pilier")) return;
            // Essai réaliste 5 (« le gros bloc en brique, je ne sais pas à quoi il sert » : c'est le front de taille de la carrière) : une
            // paroi de gros rochers sur son emprise.
            if (id.StartsWith("front_de_taille") && QuarryFace(id, points)) return;
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
            uint seed = 12345;
            float Next()
            {
                seed = seed * 1664525u + 1013904223u;
                return (seed >> 8) / 16777216f;
            }
            // Essai réaliste : nombre réglé dans le catalogue (moitié sur téléphone), plantes à leur taille réelle quand elle est plausible.
            // Vallons agrandis : le nombre suit la surface (celle des premiers Vallons, 84 × 58 m, porte le nombre du catalogue ; quatre fois
            // au plus), ni sur les routes ni dans les champs.
            float area = (maxX - minX) * (maxZ - minZ);
            bool phone = Application.isMobilePlatform;
            int wanted = Mathf.RoundToInt((phone ? catalog.PlantCount / 2 : catalog.PlantCount) * Mathf.Clamp(area / 4872f, 1f, phone ? 2f : 4f));
            int placed = 0;
            for (int i = 0; i < wanted * 8 && placed < wanted; i++)
            {
                float x = Mathf.Lerp(minX + 1f, maxX - 1f, Next()), z = Mathf.Lerp(minZ + 1f, maxZ - 1f, Next());
                if (InObstacle(x, z, 1f) || InField(x, z, 1f) || TownRules.RoadAt(x, z, _roads).Edge < 0.6) continue;
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
            // Vallons agrandis : le nombre d'arbres suit le tour de l'aire (celui des premiers Vallons, 284 m, porte le nombre du catalogue).
            float perimeter = 2f * (Raw(space.Max.X.Raw) - Raw(space.Min.X.Raw) + Raw(space.Max.Z.Raw) - Raw(space.Min.Z.Raw));
            bool phone = Application.isMobilePlatform;
            int count = Mathf.RoundToInt((phone ? catalog.ForestTrees / 3 : catalog.ForestTrees) * Mathf.Clamp(perimeter / 284f, 1f, phone ? 2f : 3.5f));
            var spots = StyleRules.Forest(Raw(space.Min.X.Raw), Raw(space.Min.Z.Raw), Raw(space.Max.X.Raw), Raw(space.Max.Z.Raw), catalog.ForestMargin, count);
            bool debris = catalog.Debris != null && catalog.Debris.Length > 0;
            for (int i = 0; i < spots.Count; i++)
            {
                string id = "lisiere" + i;
                var at = new Vector3((float)spots[i].X, 0f, (float)spots[i].Z);
                // Une place sur quatre : du bois mort (souche, tronc couché) à sa taille réelle, s'il y en a.
                GameObject look = debris && i % 4 == 3
                    ? StyleLook.Decor(_root.transform, StyleSlot.Debris, id, at, 1f, 0f, true)
                    : StyleLook.Decor(_root.transform, StyleSlot.Tree, id, at, TreeHeight() * (0.75f + 0.005f * StyleRules.Variant(id, 101)));
                // Revue Codex de la PR #56 : en vue 2, la caméra (6 m derrière le Bastion) peut entrer dans la lisière quand il longe le bord ;
                // ces arbres sont coupés comme ceux des obstacles quand ils le cachent.
                if (look != null) AddModelOccluders(look);
            }
        }

        // --- Vallons agrandis : bourg, bois, rues pavées, champs -------------------------------------------------------------

        /// <summary>
        /// Vallons agrandis : un bâtiment fait par le code (<see cref="TownShapes.Building"/>) sur l'emprise de son obstacle, sa porte vers
        /// la route la plus proche, habillé des matières du catalogue (crépi, pierre, planches, tuiles) ; abaissé comme un mur quand il
        /// cache le joueur.
        /// </summary>
        private void Building(StyleBuilding kind, string id, List<Vector3> points)
        {
            float x0 = points.Min(p => p.x), x1 = points.Max(p => p.x), z0 = points.Min(p => p.z), z1 = points.Max(p => p.z);
            // Intérieurs : un bâtiment qui a une porte tourne sa façade vers elle (on la voit où l'on entre) ; sinon vers la route.
            bool hasDoor = _doors.Any(d => d.Building == id);
            (int, int) facing = TownRules.FacingOf(id, x0, z0, x1, z1, _roads, _doors);
            DoorLantern(kind, id, x0, z0, x1, z1, facing);   // sous-lot V2.1 : sa lanterne de porte, allumée la nuit
            // Essai réaliste 8 : le vrai bâtiment de Sketchfab s'il y en a un de ce genre dans le catalogue.
            if (ModelBuilding(kind, id, x0, z0, x1, z1, facing, hasDoor)) return;
            Material plaster = Built(StyleSurface.Plaster, new Color(0.86f, 0.82f, 0.74f), out float plasterTile);
            Material stone = Built(StyleSurface.Stone, new Color(0.5f, 0.48f, 0.44f), out float stoneTile);
            Material wood = Built(StyleSurface.Wood, new Color(0.42f, 0.3f, 0.19f), out float woodTile);
            Material roof = RoofMaterial(TownShapes.RoofLookOf(kind, id), out float roofTile);
            var footprint = points.Select(p => ((double)p.x, (double)p.z)).ToList();
            StyleBuildingShapes shapes = TownShapes.Building(kind, footprint, id, facing, plasterTile, stoneTile, woodTile, roofTile);
            Material walls = shapes.WallSurface == StyleSurface.Stone ? stone : shapes.WallSurface == StyleSurface.Wood ? wood : plaster;
            var go = new GameObject(id + " (bâtiment)");
            go.transform.SetParent(_root.transform, false);
            var parts = new (StyleMesh Mesh, Material Material, string Name)[]
            {
                (shapes.Walls, walls, "murs"), (shapes.Stone, stone, "pierre"), (shapes.Wood, wood, "bois"), (shapes.Roof, roof, "toit"),
                (shapes.Cloth, Plain(ClothColor(shapes.ClothColor)), "toile"), (shapes.ClothStripes, Plain(new Color(0.9f, 0.85f, 0.72f)), "bandes"),
                (shapes.Dark, Plain(new Color(0.045f, 0.045f, 0.05f)), "ouvertures"),
                (shapes.Windows, WindowGlass(), "fenêtres"),   // sous-lot V2.1 : allumées la nuit
            };
            foreach ((StyleMesh mesh, Material material, string name) in parts)
                if (mesh.Triangles.Count > 0) Shape(id + " (" + name + ")", mesh, material, go.transform);
            _occluders.Add(new Occluder { Transform = go.transform, Lower = true, Min = new Vector3(x0, 0f, z0), Max = new Vector3(x1, (float)shapes.Top, z1) });
        }

        /// <summary>
        /// Essai réaliste 8 (demande de Damien du 28 octobre 2026 : de vrais bâtiments en 3D) : un modèle de Sketchfab du genre
        /// <paramref name="kind"/>, choisi et posé par <see cref="TownModels.Pick"/> (face vers la route, pied ajusté à l'emprise, étiré
        /// d'un quart au plus), une maison sur deux en miroir ; abaissé comme un mur quand il cache le joueur. False sans modèle de ce genre
        /// (le bâtiment fait par le code reste).
        /// </summary>
        private bool ModelBuilding(StyleBuilding kind, string id, float x0, float z0, float x1, float z1, (int X, int Z) facing, bool locked = false)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            if (catalog == null) return false;
            List<BuildingLook> models = catalog.BuildingsOf(kind);
            if (models.Count == 0) return false;
            var feet = models.Select(m => ((double)m.Size.x, (double)m.Size.y)).ToList();
            (int index, (int X, int Z) face, ModelFit fit) = TownModels.Pick(id, x1 - x0, z1 - z0, facing, feet, TownModels.Overflow(kind), locked);
            // Relecture indépendante de l'essai 8 : une pose de repli trop déformée laisse le bâtiment fait par le code.
            if (index < 0 || !TownModels.Usable(fit)) return false;
            var center = new Vector3((x0 + x1) / 2f, 0f, (z0 + z1) / 2f);
            var go = new GameObject(id + " (bâtiment)");
            go.transform.SetParent(_root.transform, false);
            go.transform.position = center;
            var turn = new GameObject(models[index].Model.name);
            turn.transform.SetParent(go.transform, false);
            turn.transform.localRotation = Quaternion.Euler(0f, (float)fit.Yaw, 0f);
            // Échelles dans les axes du modèle : tourné d'un quart de tour, son x suit le z du monde.
            bool across = face.X != 0;
            float mirror = kind == StyleBuilding.House && TownModels.Mirrored(id) ? -1f : 1f;
            turn.transform.localScale = new Vector3(mirror * (float)(across ? fit.ScaleZ : fit.ScaleX), (float)fit.ScaleY, (float)(across ? fit.ScaleX : fit.ScaleZ));
            GameObject model = Object.Instantiate(models[index].Model, turn.transform, false);
            foreach (Collider c in model.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            // Le pied du modèle au centre de l'emprise, le bas au sol.
            if (StyleLook.VisibleBounds(model, out Bounds b))
                model.transform.position += new Vector3(center.x - b.center.x, -b.min.y, center.z - b.center.z);
            float top = StyleLook.VisibleBounds(go, out Bounds all) ? all.max.y : 8f;
            _occluders.Add(new Occluder { Transform = go.transform, Lower = true, Min = new Vector3(x0, 0f, z0), Max = new Vector3(x1, top, z1) });
            return true;
        }

        /// <summary>Essai réaliste 8 : un villageois posé (son corps, son modèle) ; un marcheur garde son allée et son sens.</summary>
        private sealed class Villager
        {
            public StyleVillager Data;
            public Transform Body;
            public StyleBody Look;
            public float Along;
            public bool Back;
            public float Yaw;
            /// <summary>Hauteur du centre du corps (m) : 1 m, fois la taille du villageois.</summary>
            public float Height = 1f;
            /// <summary>Sous-lot V2.3 : rentré chez lui pour sa course (caché).</summary>
            public bool Away;

            /// <summary>Sous-lot V2.3.1 : PNJ d'une pièce sorti par la porte (il est dans une autre zone) : caché.</summary>
            public bool Gone;

            /// <summary>Revue Codex de la PR #109 : corps d'un PNJ qui parle (« npc.… ») : ni la nuit, ni la pluie, ni les courses ne le cachent.</summary>
            public bool Speaker;
            /// <summary>Sous-lot V2.3 : l'activité jouée en ce moment (changée seulement quand elle change).</summary>
            public VillagerActivity Playing;
            /// <summary>Relecture de la PR #103 : sa course, calculée au placement ; la place visée, relue quand la minute change.</summary>
            public VillagerPlan Plan;
            public VillagerGoal Goal;
            public int GoalMinute = -1;

            /// <summary>Sous-lot V2.8 (recherche de chemin) : son trajet vers la place de sa course, qui contourne les maisons.</summary>
            public readonly Walker Route = new Walker { StraightWhenBlocked = true };
        }

        /// <summary>Sous-lot V2.8 : espace de la vue (pour les trajets des passants, construits à la première course).</summary>
        private readonly CombatSpace _space;
        private WalkPaths _paths;

        /// <summary>Sous-lot V2.8 : trajets des passants dans cet espace (construits une fois, à la première demande).</summary>
        private WalkPaths Paths
        {
            get
            {
                if (_paths == null) _paths = new WalkPaths(_space);
                return _paths;
            }
        }

        /// <summary>
        /// Revue Codex de la PR #103 : dans une pièce, le corps visible d'un PNJ qui parle (Mathurin, Frère Anselme…) est un villageois du
        /// décor posé à sa place à l'entrée (<see cref="AdventureMap.Speakers"/>) ; il suit ensuite le repère du PNJ (<see cref="MoveSpeaker"/>).
        /// </summary>
        private readonly Dictionary<string, Villager> _speakers = new Dictionary<string, Villager>();

        /// <summary>
        /// Revue Codex de la PR #103 : le corps visible du PNJ <paramref name="id"/> d'une pièce va à (<paramref name="x"/>, <paramref name="z"/>),
        /// la place de son repère, qui marche déjà au pas vers la place de l'heure (emploi du temps) : il marche avec lui (animation de marche,
        /// tourné vers où il va), puis reprend sa pose. Faux si la pièce n'a pas de corps pour lui.
        /// </summary>
        public bool MoveSpeaker(string id, float x, float z, float deltaTime)
        {
            if (id == null || !_speakers.TryGetValue(id, out Villager v) || v.Body == null) return false;
            Vector3 at = v.Body.position;
            var target = new Vector3(x, v.Height, z);
            Vector3 way = target - at;
            way.y = 0f;
            bool moving = way.sqrMagnitude > 1e-8f;
            if (moving)
            {
                v.Body.position = target;
                v.Yaw = (float)StyleRules.Turn(v.Yaw, (float)StyleRules.FaceYaw(0, 0, way.x, way.z), 400f * Mathf.Max(deltaTime, 0.0001f));
                v.Body.rotation = Quaternion.Euler(0f, v.Yaw, 0f);
            }
            VillagerActivity play = moving ? VillagerActivity.Walk : v.Data.Activity;
            if (play != v.Playing && v.Look != null)
            {
                v.Playing = play;
                v.Look.Loop(play, StyleRules.Variant(v.Data.Id, 97));
            }
            return true;
        }

        /// <summary>
        /// Sous-lot V2.3.1 (PNJ qui changent de lieu) : montre ou cache le corps visible du PNJ <paramref name="id"/> d'une pièce (caché quand
        /// il est sorti par la porte, montré quand il rentre). Faux si la pièce n'a pas de corps pour lui.
        /// </summary>
        public bool ShowSpeaker(string id, bool shown)
        {
            if (id == null || !_speakers.TryGetValue(id, out Villager v) || v.Body == null) return false;
            v.Gone = !shown;
            bool visible = Shown(v);
            if (v.Body.gameObject.activeSelf != visible) v.Body.gameObject.SetActive(visible);
            return true;
        }

        /// <summary>Sous-lot V2.3 : places des courses des villageois du décor (auberge, puits, maisons) ; null dans un intérieur.</summary>
        private VillageSpots _errands;

        /// <summary>Sous-lot V2.3 : minute du monde montrée (−1 : pas encore donnée, chacun reste à son poste).</summary>
        private int _villageMinute = -1;

        /// <summary>
        /// Sous-lot V2.3 (villageois qui suivent leur journée) : la minute du monde, pour les allers-retours des villageois du décor
        /// (<see cref="VillageDay"/>) : le matin au puits, à midi chez eux, le soir devant l'auberge.
        /// </summary>
        public void Village(int minute) => _villageMinute = minute;

        private readonly List<Villager> _villagers = new List<Villager>();

        /// <summary>Allure (m/s) des passants.</summary>
        private const float WalkSpeed = 1.1f;

        /// <summary>
        /// Essai réaliste 8 (demande de Damien du 28 octobre 2026 : des villageois dans le bourg) : les villageois d'affichage
        /// (<see cref="TownModels.Villagers"/>), habillés des personnages de Mixamo du catalogue (dossiers Paysan, Garde, Sentinelle), qui
        /// bavardent, regardent autour d'eux, cueillent ou passent ; le tiers sur téléphone. Le moteur ne les connaît pas : ils n'arrêtent rien.
        /// </summary>
        private void Villagers(AdventureMap map)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            if (map == null || catalog == null || catalog.Villagers <= 0) return;
            int max = Application.isMobilePlatform ? catalog.Villagers / 3 : catalog.Villagers;
            _errands = VillageDay.Spots(map);   // sous-lot V2.3 : les courses de la journée
            Villagers(TownModels.Villagers(map, _fields, max));
        }

        /// <summary>Habille et pose les villageois d'affichage <paramref name="villagers"/> (bourg ou intérieur).</summary>
        private void Villagers(IEnumerable<StyleVillager> villagers)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            if (catalog == null || catalog.Villagers <= 0) return;
            bool humans = HumanBodies.Usable(catalog);   // sous-lot V2.9.2 : les corps humains, si les personnages de Mixamo manquent
            foreach (StyleVillager v in villagers)
            {
                if (catalog.Character(v.Character) == null && !humans) continue;
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = v.Id + " (villageois)";
                Object.Destroy(body.GetComponent<Collider>());
                body.transform.SetParent(_root.transform, false);
                body.transform.position = new Vector3((float)v.X, 1f, (float)v.Z);
                body.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
                body.transform.rotation = Quaternion.Euler(0f, (float)v.Yaw, 0f);
                StyleBody look = StyleLook.Dress(body, StyleSlot.Character, v.Character, 1.8f, v.Id);   // sous-lot V2.9.2 : l'aspect suit le villageois
                if (look == null)
                {
                    Object.Destroy(body);
                    continue;
                }
                // Tailles variées (± 5 %) : la hauteur du personnage du catalogue l'emporte dans Dress (relecture indépendante de l'essai 8) ;
                // le corps entier est donc mis à l'échelle après, les pieds restant au sol (bas de la capsule à 1 m sous son centre).
                float size = 1f + 0.01f * (StyleRules.Variant(v.Id, 11) - 5);
                body.transform.localScale = body.transform.localScale * size;
                body.transform.position = new Vector3((float)v.X, size, (float)v.Z);
                look.Loop(v.Activity, StyleRules.Variant(v.Id, 97));
                // Revue Codex de la PR #109 : le corps d'un PNJ qui parle ne fait pas de courses (le cœur le place).
                bool speaker = v.Id != null && v.Id.StartsWith("npc.", System.StringComparison.Ordinal);
                VillagerPlan plan = VillageDay.Plan(v, speaker ? null : _errands);   // relecture de la PR #103 : une fois, au placement
                var placed = new Villager { Data = v, Body = body.transform, Look = look, Yaw = (float)v.Yaw, Height = size, Playing = v.Activity,
                    Plan = plan, Goal = plan.Post, Speaker = speaker };
                _villagers.Add(placed);
                // Revue Codex de la PR #103 : le corps d'un PNJ qui parle (identifiant « npc.… ») suit son repère.
                if (speaker) _speakers[v.Id] = placed;
            }
        }

        /// <summary>
        /// Essai réaliste 8 : les passants vont et viennent le long de leur allée, et se retournent au bout. Sous-lot V2.3 : les autres font
        /// leur course de la journée (<see cref="VillageDay.Goal"/>) : ils y marchent au pas, y bavardent, ou rentrent chez eux, puis reviennent.
        /// </summary>
        private void Walk(float deltaTime)
        {
            foreach (Villager v in _villagers)
            {
                if (!v.Data.Walks && deltaTime > 0f && v.Body != null) Errand(v, deltaTime);
                if (!v.Data.Walks || deltaTime <= 0f) continue;
                float dx = (float)(v.Data.ToX - v.Data.X), dz = (float)(v.Data.ToZ - v.Data.Z);
                float length = Mathf.Sqrt(dx * dx + dz * dz);
                if (length < 0.1f) continue;
                v.Along += (v.Back ? -1f : 1f) * WalkSpeed * deltaTime / length;
                if (v.Along >= 1f || v.Along <= 0f)
                {
                    v.Along = Mathf.Clamp01(v.Along);
                    v.Back = !v.Back;
                }
                float target = (float)StyleRules.FaceYaw(0, 0, v.Back ? -dx : dx, v.Back ? -dz : dz);
                v.Yaw = (float)StyleRules.Turn(v.Yaw, target, 200f * deltaTime);
                v.Body.position = new Vector3((float)v.Data.X + dx * v.Along, v.Height, (float)v.Data.Z + dz * v.Along);
                v.Body.rotation = Quaternion.Euler(0f, v.Yaw, 0f);
            }
        }

        /// <summary>Sous-lot V2.3 : un villageois du décor va vers la place de sa course (ou revient à son poste), au pas.</summary>
        private void Errand(Villager v, float deltaTime)
        {
            if (_villageMinute < 0 || v.Plan.Errand == VillagerErrand.None) return;
            if (v.GoalMinute != _villageMinute)
            {
                v.GoalMinute = _villageMinute;
                v.Goal = VillageDay.Goal(v.Plan, _villageMinute);
            }
            VillagerGoal goal = v.Goal;
            Vector3 at = v.Body.position;
            // Sous-lot V2.8 : il suit son trajet, qui contourne les maisons (le but lui-même si la ligne droite est libre).
            (double ax, double az) = v.Route.Next(Paths, at.x, at.z, goal.X, goal.Z);
            var target = new Vector3((float)ax, v.Height, (float)az);
            Vector3 step = Vector3.MoveTowards(at, target, WalkSpeed * deltaTime);
            bool moving = (step - at).sqrMagnitude > 1e-8f;
            if (moving)
            {
                v.Body.position = step;
                Vector3 way = target - at;
                float aim = (float)StyleRules.FaceYaw(0, 0, way.x, way.z);
                v.Yaw = (float)StyleRules.Turn(v.Yaw, aim, 200f * deltaTime);
            }
            else v.Yaw = (float)StyleRules.Turn(v.Yaw, goal.Yaw, 120f * deltaTime);
            v.Body.rotation = Quaternion.Euler(0f, v.Yaw, 0f);
            VillagerActivity play = moving ? VillagerActivity.Walk : goal.Activity;
            if (play != v.Playing && v.Look != null)
            {
                v.Playing = play;
                v.Look.Loop(play, StyleRules.Variant(v.Data.Id, 97));
            }
            bool away = goal.Indoors && !moving;
            if (away != v.Away)
            {
                v.Away = away;
                v.Body.gameObject.SetActive(Shown(v));
            }
        }

        private static readonly Dictionary<StyleRoofLook, Material> RoofMaterials = new Dictionary<StyleRoofLook, Material>();
        private static readonly Dictionary<Color, Material> PlainMaterials = new Dictionary<Color, Material>();

        /// <summary>Couverture d'un toit : tuiles du catalogue (assombries pour l'auberge, bleuies en ardoises), bardeaux de planches.</summary>
        private static Material RoofMaterial(StyleRoofLook look, out float tile)
        {
            Material source = look == StyleRoofLook.Shingles ? Built(StyleSurface.Wood, new Color(0.4f, 0.3f, 0.2f), out tile)
                : Built(StyleSurface.Roof, new Color(0.55f, 0.25f, 0.16f), out tile);
            if (look == StyleRoofLook.Tiles) return source;
            if (RoofMaterials.TryGetValue(look, out Material cached) && cached != null) return cached;
            var tint = look == StyleRoofLook.DarkTiles ? new Color(0.72f, 0.6f, 0.58f) : look == StyleRoofLook.Slate ? new Color(0.48f, 0.54f, 0.66f) : new Color(0.78f, 0.7f, 0.6f);
            var copy = new Material(source);
            if (copy.HasProperty("_BaseColor")) copy.SetColor("_BaseColor", copy.GetColor("_BaseColor") * tint);
            if (copy.HasProperty("_Color")) copy.SetColor("_Color", copy.GetColor("_Color") * tint);
            RoofMaterials[look] = copy;
            return copy;
        }

        private static Material _soil;

        /// <summary>Terre des labours (texture de terre du catalogue, ou une couleur), partagée d'une construction du décor à l'autre.</summary>
        private static Material Soil(StyleCatalog catalog)
        {
            if (_soil != null) return _soil;
            bool textured = catalog != null && catalog.SoilColor != null;
            _soil = ArenaView.MakeMaterial(textured ? Color.white : new Color(0.4f, 0.3f, 0.2f));
            if (!textured) return _soil;
            _soil.SetTexture("_BaseMap", catalog.SoilColor);
            _soil.SetTexture("_MainTex", catalog.SoilColor);
            if (catalog.SoilNormal != null)
            {
                _soil.SetTexture("_BumpMap", catalog.SoilNormal);
                _soil.EnableKeyword("_NORMALMAP");
            }
            return _soil;
        }

        /// <summary>Matériau d'une couleur unie (toiles, ouvertures), partagé.</summary>
        private static Material Plain(Color color)
        {
            if (PlainMaterials.TryGetValue(color, out Material cached) && cached != null) return cached;
            Material material = ArenaView.MakeMaterial(color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.15f);
            PlainMaterials[color] = material;
            return material;
        }

        /// <summary>Toile : rouge, bleue (auvents rayés), écrue ou sombre (tentes).</summary>
        private static Color ClothColor(int kind) => kind == 0 ? new Color(0.62f, 0.16f, 0.13f) : kind == 1 ? new Color(0.18f, 0.3f, 0.55f)
            : kind == 2 ? new Color(0.8f, 0.74f, 0.6f) : new Color(0.52f, 0.44f, 0.33f);

        /// <summary>
        /// Vallons agrandis : un bois, massif qu'on ne traverse pas (l'obstacle du moteur) : les arbres du catalogue serrés sur son emprise
        /// (<see cref="TownRules.WoodTrees"/>, comme la carte peinte), du bois mort au cœur ; les arbres de la lisière sont coupés quand ils
        /// cachent le joueur. Un arbre du cœur sur trois sur téléphone (sous-bois de terre sombre en dessous). Sans arbre dans le catalogue, un
        /// bloc vert.
        /// </summary>
        private void Woods(string id, List<Vector3> points)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            if (catalog == null || catalog.Trees == null || catalog.Trees.Length == 0)
            {
                Prism(id, points, 7f, ArenaView.MakeMaterial(new Color(0.16f, 0.3f, 0.12f)), true);
                return;
            }
            IReadOnlyList<StyleWoodTree> trees = TownRules.WoodTrees(points.Select(p => ((double)p.x, (double)p.z)).ToList(), id, MapPainter.WoodSpacing);
            bool phone = Application.isMobilePlatform;
            bool debris = catalog.Debris != null && catalog.Debris.Length > 0;
            for (int i = 0; i < trees.Count; i++)
            {
                StyleWoodTree tree = trees[i];
                if (phone && !tree.Edge && i % 3 != 0) continue;   // relecture : un arbre du cœur sur trois sur téléphone
                string part = id + "/" + i;
                var at = new Vector3((float)tree.X, 0f, (float)tree.Z);
                GameObject look = debris && !tree.Edge && i % 6 == 5
                    ? StyleLook.Decor(_root.transform, StyleSlot.Debris, part, at, 1f, 0f, true)
                    : StyleLook.Decor(_root.transform, StyleSlot.Tree, part, at, TreeHeight() * (float)(0.6 + 0.3 * tree.Scale));
                if (look != null && tree.Edge) AddModelOccluders(look);
            }
        }

        /// <summary>Vallons agrandis : les rues pavées (la place du bourg), en pavés du catalogue posés sur le sol (<see cref="TownShapes.Pavement"/>).</summary>
        private void Pavements()
        {
            foreach (StyleRoad road in _roads.Where(r => r.Paved))
            {
                Material cobble = Built(StyleSurface.Cobble, new Color(0.42f, 0.4f, 0.37f), out float tile);
                Shape("Rue pavée " + road.Id, TownShapes.Pavement(road, tile), cobble);
            }
        }

        /// <summary>
        /// Vallons agrandis : les champs des lieux de culture (<see cref="TownRules.Fields"/>, comme la carte peinte) : blé mûr ou vert en
        /// rangs (touffes dessinées par la carte graphique, comme l'herbe, plus hautes et dorées), labours en billons de terre.
        /// </summary>
        private void Fields()
        {
            if (_fields.Count == 0) return;
            StyleCatalog catalog = StyleCatalog.Loaded;
            float step = Application.isMobilePlatform ? 0.6f : 0.32f;   // relecture : moins de blé sur téléphone
            var ripe = new List<(double X, double Z, double Yaw, double Scale)>();
            var green = new List<(double X, double Z, double Yaw, double Scale)>();
            float soilTile = catalog != null && catalog.SoilColor != null ? catalog.GroundTile / 2f : 2f;
            foreach (StyleField field in _fields)
            {
                if (field.Crop == 2)
                {
                    Material soil = Soil(catalog);
                    Shape("Labour", TownShapes.Furrows(field, soilTile), soil);
                }
                else (field.Crop == 0 ? ripe : green).AddRange(TownShapes.CropSpots(field, step));
            }
            StyleGrass wheat = StyleGrass.CreateCrop(ripe, new Color(0.42f, 0.34f, 0.12f), new Color(0.86f, 0.72f, 0.36f));
            StyleGrass young = StyleGrass.CreateCrop(green, new Color(0.18f, 0.3f, 0.08f), new Color(0.5f, 0.66f, 0.24f));
            if (wheat != null) _crops.Add(wheat);
            if (young != null) _crops.Add(young);
        }

        /// <summary>
        /// Essai réaliste : les endroits qui changent la couche du sol (roche autour des blocs, des pierres du Seuil, des murs et des
        /// piliers ; terre autour des arbres et du moulin), d'après les obstacles (<see cref="StyleRules.GroundFor"/>).
        /// </summary>
        private static List<StyleSpot> Spots(CombatSpace space) =>
            // Vallons agrandis : règles communes avec la carte peinte (un obstacle allongé donne une rangée de taches ; terre battue autour
            // des bâtiments).
            TownRules.GroundSpots(space.Obstacles.Select(o => (o.Id, (double)Raw(o.Points.Min(p => p.X.Raw)), (double)Raw(o.Points.Min(p => p.Z.Raw)),
                (double)Raw(o.Points.Max(p => p.X.Raw)), (double)Raw(o.Points.Max(p => p.Z.Raw)))));

        /// <summary>Le point est-il dans un obstacle (son polygone, à <paramref name="margin"/> mètres près) ?</summary>
        private bool InObstacle(float x, float z, float margin)
        {
            foreach (var o in _shapes)
                if (x >= o.MinX - margin && x <= o.MaxX + margin && z >= o.MinZ - margin && z <= o.MaxZ + margin
                    && StyleRules.InsideDistance(x, z, o.Points) > -margin) return true;
            return false;
        }

        /// <summary>Vallons agrandis : le point est-il dans un champ (à <paramref name="margin"/> mètres près) ?</summary>
        private bool InField(float x, float z, float margin)
        {
            foreach (StyleField f in _fields)
                if (x >= f.MinX - margin && x <= f.MaxX + margin && z >= f.MinZ - margin && z <= f.MaxZ + margin) return true;
            return false;
        }

        /// <summary>Hauteur des arbres du catalogue (11 m sans catalogue).</summary>
        private static float TreeHeight() => StyleCatalog.Loaded != null ? StyleCatalog.Loaded.TreeHeight : 11f;

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
            _meshes.Add(mesh);
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject(name + " (eau)");
            go.transform.SetParent(_root.transform, false);
            go.AddComponent<MeshFilter>().mesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = _water;
            _waters.Add(go);
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
            _meshes.Add(mesh);
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
        /// Essai réaliste 5 (choix de Damien du 27 octobre 2026) : muret de pierres sèches sur l'emprise d'une haie
        /// (<see cref="StyleShapes.DryStoneWall"/>), en pierre brute du catalogue (pierre des murs à défaut) ; abaissé comme un mur quand il
        /// cache le joueur.
        /// </summary>
        private void DryStoneWall(string id, List<Vector3> points)
        {
            float x0 = points.Min(p => p.x), x1 = points.Max(p => p.x), z0 = points.Min(p => p.z), z1 = points.Max(p => p.z);
            Material stone = Stone(out float tile);
            StyleMesh wall = StyleShapes.DryStoneWall(x0, z0, x1, z1, id, tile);
            GameObject go = Shape(id + " (muret de pierres sèches)", wall, stone);
            float top = (float)wall.Bounds().Max.Y;
            _occluders.Add(new Occluder { Transform = go.transform, Renderer = go.GetComponent<Renderer>(), Lower = true, Min = new Vector3(x0, 0f, z0), Max = new Vector3(x1, top, z1) });
        }

        /// <summary>Pierre brute du catalogue (muret, culées, socles), sinon la pierre des murs, sinon une couleur.</summary>
        private static Material Stone(out float tile) =>
            StyleWorld.Rock(out tile) ?? Built(StyleSurface.Stone, new Color(0.45f, 0.43f, 0.38f), out tile);

        /// <summary>
        /// Essai réaliste 5 : front de taille de la carrière en paroi de gros rochers du catalogue, serrés le long de son grand côté, un
        /// second rang plus bas au pied et quelques blocs tombés devant. False sans rocher dans le catalogue (le bloc simple reste).
        /// </summary>
        private bool QuarryFace(string id, List<Vector3> points)
        {
            float x0 = points.Min(p => p.x), x1 = points.Max(p => p.x), z0 = points.Min(p => p.z), z1 = points.Max(p => p.z);
            bool alongX = x1 - x0 >= z1 - z0;
            float length = alongX ? x1 - x0 : z1 - z0, depth = alongX ? z1 - z0 : x1 - x0;
            int count = Mathf.Max(1, Mathf.CeilToInt(length / 3.2f));
            int placed = 0;
            for (int i = 0; i < count; i++)
            {
                float s = (i + 0.5f) / count * length;
                string part = id + "/" + i;
                float jitter = (StyleRules.Variant(part, 100) - 50) * 0.008f;
                Vector3 at = alongX ? new Vector3(x0 + s, 0f, (z0 + z1) / 2f + jitter) : new Vector3((x0 + x1) / 2f + jitter, 0f, z0 + s);
                GameObject rock = StyleLook.Decor(_root.transform, StyleSlot.Rock, part, at, 0f, depth * (1.0f + 0.003f * StyleRules.Variant(part + "/taille", 100)));
                if (rock == null) continue;
                AddModelOccluders(rock);
                placed++;
                // Second rang, plus petit, et un bloc tombé de temps en temps, dans l'emprise (côté de l'aire : vers le plus petit x ou z) :
                // aucun rocher du décor ne fait croire à un obstacle là où l'on peut marcher.
                string low = part + "/pied";
                Vector3 foot = at + (alongX ? new Vector3(1.6f, 0f, -depth * 0.3f) : new Vector3(-depth * 0.3f, 0f, 1.6f));
                GameObject small = StyleLook.Decor(_root.transform, StyleSlot.Rock, low, foot, 0f, depth * (0.45f + 0.003f * StyleRules.Variant(low, 100)));
                if (small != null) AddModelOccluders(small);
                if (i % 3 == 1)
                {
                    string fallen = part + "/eboulis";
                    Vector3 front = at + (alongX ? new Vector3(-0.8f, 0f, -depth * 0.4f) : new Vector3(-depth * 0.4f, 0f, -0.8f));
                    GameObject block = StyleLook.Decor(_root.transform, StyleSlot.Rock, fallen, front, 0f, 0.9f + 0.006f * StyleRules.Variant(fallen, 100));
                    if (block != null) AddModelOccluders(block);
                }
            }
            return placed > 0;
        }

        /// <summary>Essai réaliste 5 : objet d'une forme faite par le code (<see cref="StyleMesh"/>), avec ses tangentes pour le relief.</summary>
        private GameObject Shape(string name, StyleMesh shape, Material material) => Shape(name, shape, material, _root.transform);

        private GameObject Shape(string name, StyleMesh shape, Material material, Transform parent)
        {
            var mesh = new Mesh { name = name };
            _meshes.Add(mesh);
            if (shape.Vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = shape.Vertices.Select(v => new Vector3((float)v.X, (float)v.Y, (float)v.Z)).ToArray();
            mesh.uv = shape.Uvs.Select(uv => new Vector2((float)uv.U, (float)uv.V)).ToArray();
            mesh.triangles = shape.Triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().mesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
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
            _meshes.Add(mesh);
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
            StyleWorld.Surface(kind, out tile) ?? Plain(color);   // relecture des Vallons agrandis : couleur simple partagée

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

        /// <summary>Flèche (m) du pont cintré : le milieu du tablier est à 4 cm + 35 cm au-dessus du sol.</summary>
        private const float BridgeRise = 0.35f;

        /// <summary>
        /// Pont entre les deux bras de la rivière (« riviere_sud » et « riviere_nord »), s'ils existent. La rivière coule suivant z (bande
        /// x0..x1) ; le passage est la coupure entre ses deux bras (z0..z1), et le pont se traverse suivant x, d'une rive à l'autre
        /// (revue Codex de la PR #34 : axes précisés). Essai réaliste 5 (choix de Damien du 27 octobre 2026 : « pont cintré fait par moi ») :
        /// pont de bois bombé fait par le code (<see cref="StyleShapes.Bridge"/>) : planches une à une, longerons, poteaux et lisses de
        /// rondins, culées de pierres ; les personnages montent avec son tablier (<see cref="StyleWorld.StandHeight"/>).
        /// </summary>
        private void Bridge(CombatSpace space)
        {
            SpaceObstacle south = space.Obstacles.FirstOrDefault(o => o.Id == "riviere_sud");
            SpaceObstacle north = space.Obstacles.FirstOrDefault(o => o.Id == "riviere_nord");
            if (south == null || north == null) return;
            float x0 = Raw(south.Points.Min(p => p.X.Raw)) - 0.6f, x1 = Raw(south.Points.Max(p => p.X.Raw)) + 0.6f;
            float z0 = Raw(south.Points.Max(p => p.Z.Raw)), z1 = Raw(north.Points.Min(p => p.Z.Raw));
            if (z1 <= z0) return;
            // Essai réaliste (demande de Damien du 27 octobre 2026 : « le pont n'est pas réaliste ») : la rivière coule sous le pont.
            if (_rivers.Count > 0 && BridgeGap(space) is (float gx0, float gz0, float gx1, float gz1))
                WaterSurface("Rivière (sous le pont)", new List<Vector3> { new Vector3(gx0, 0f, gz0), new Vector3(gx1, 0f, gz0), new Vector3(gx1, 0f, gz1), new Vector3(gx0, 0f, gz1) });
            Material wood = Built(StyleSurface.Wood, new Color(0.55f, 0.4f, 0.24f), out float woodTile);
            Material bark = StyleWorld.Surface(StyleSurface.Bark, out float barkTile) ?? wood;
            Material stone = Stone(out float stoneTile);
            StyleBridgeShapes bridge = StyleShapes.Bridge(x0, z0, x1, z1, BridgeRise, "pont", woodTile, barkTile, stoneTile);
            Shape("Pont (planches et poutres)", bridge.Wood, wood);
            Shape("Pont (rambardes)", bridge.Bark, bark);
            Shape("Pont (culées)", bridge.Stone, stone);
            StyleWorld.Deck(x0, z0, x1, z1, BridgeRise);
        }

        /// <summary>
        /// Abri sur les piliers (« pilier… ») s'il y en a : ses poteaux sont les piliers du moteur. Essai réaliste 5 (demande de Damien du
        /// 27 octobre 2026 : « il faudrait texturer le bâtiment ») : vraie charpente faite par le code (<see cref="StyleShapes.Shelter"/>) :
        /// poteaux sur socles de pierre, sablières, entraits, aisseliers, et toit à deux pans couvert de tuiles. Le toit (et ce qui est
        /// dessous : chevrons, faîtage) cache ce qui est dessous et est retiré quand le joueur y entre.
        /// </summary>
        private void Roof(CombatSpace space)
        {
            List<SpaceObstacle> pillars = space.Obstacles.Where(o => o.Id.StartsWith("pilier")).ToList();
            if (pillars.Count < 2) return;
            var posts = pillars.Select(o => ((double)Raw((o.Points.Min(p => p.X.Raw) + o.Points.Max(p => p.X.Raw)) / 2),
                (double)Raw((o.Points.Min(p => p.Z.Raw) + o.Points.Max(p => p.Z.Raw)) / 2))).ToList();
            Material wood = Built(StyleSurface.Wood, new Color(0.42f, 0.3f, 0.2f), out float woodTile);
            Material tiles = Built(StyleSurface.Roof, new Color(0.5f, 0.22f, 0.16f), out float roofTile);
            Material stone = Stone(out float stoneTile);
            StyleShelterShapes shelter = StyleShapes.Shelter(posts, "abri", woodTile, roofTile, stoneTile);
            Shape("Abri (charpente)", shelter.Frame, wood);
            Shape("Abri (socles)", shelter.Stone, stone);
            GameObject roof = Shape("Toit", shelter.RoofTiles, tiles);
            GameObject under = Shape("Toit (voliges et chevrons)", shelter.RoofWood, wood);
            float x0 = Raw(pillars.Min(o => o.Points.Min(p => p.X.Raw))) - 0.8f, x1 = Raw(pillars.Max(o => o.Points.Max(p => p.X.Raw))) + 0.8f;
            float z0 = Raw(pillars.Min(o => o.Points.Min(p => p.Z.Raw))) - 0.8f, z1 = Raw(pillars.Max(o => o.Points.Max(p => p.Z.Raw))) + 0.8f;
            float top = (float)shelter.RoofTop;
            _roofMin = new Vector3(x0, 0f, z0);
            _roofMax = new Vector3(x1, top, z1);
            _hasRoof = true;
            var min = new Vector3((float)shelter.X0, 3f, (float)shelter.Z0);
            var max = new Vector3((float)shelter.X1, top, (float)shelter.Z1);
            AddOccluder(roof.transform, false, min, max);
            AddOccluder(under.transform, false, min, max);
        }

        // --- Coupe des décors qui cachent le joueur --------------------------------------------------------------------------

        /// <summary>
        /// Coupe ce qui se trouve entre la caméra et le joueur (et le toit quand il est dessous) ; le reste reprend sa taille. Seul
        /// l'affichage change : un ennemi derrière une paroi reste caché (<see cref="EdS.Runtime.OtherState.InSight"/>).
        /// <paramref name="player"/> est le **milieu du corps** affiché (les pieds plus 1 m, comme <c>ArenaView.Feet</c> l'enlève), le
        /// repère de <see cref="OccluderRules.BodyPoints"/> (V1.10).
        /// </summary>
        public void Cut(Vector3 camera, Vector3 player, float deltaTime)
        {
            _grass?.Draw(camera);   // essai réaliste 4 : l'herbe en brins proche de la caméra
            Walk(deltaTime);   // essai réaliste 8 : les passants du bourg
            if (_indoor) Flicker();   // intérieurs : le feu, les bougies et les lanternes vacillent
            foreach (StyleGrass crop in _crops) crop.Draw(camera);   // Vallons agrandis : le blé des champs
            StyleWorld.AnimateWater(Time.time);   // essai réaliste : les vagues de la rivière glissent
            bool underRoof = _hasRoof && player.x >= _roofMin.x && player.x <= _roofMax.x && player.z >= _roofMin.z && player.z <= _roofMax.z;
            foreach (Occluder o in _occluders)
            {
                if (o.Transform == null) continue;
                // V1.10 (essai 10) : un décor au-dessus atteint par la caméra, ou le toit sous lequel on se tient, disparaît ; un décor entre
                // la caméra et le corps (bas des jambes, milieu ou tête : avant, la tête seule) cache le joueur.
                if (o.Overhead) o.AtHeight = OccluderRules.Overhead(camera.y, o.Min.y, o.AtHeight);
                bool hidden = o.Overhead && o.AtHeight || underRoof && o.Min.y >= 2.9f && Overlaps(o, _roofMin, _roofMax);
                bool masks = hidden || Masks(camera, player, o);
                if (!o.FadeChecked && masks) PrepareFade(o);
                if (o.Faded != null)
                {
                    // V1.10 : fondu en transparence (matériaux d'URP), au lieu de s'abaisser ou de disparaître d'un coup.
                    o.Cut = (float)OccluderRules.Step(o.Cut, OccluderRules.Target(masks, hidden), deltaTime);
                    ApplyFade(o);
                    continue;
                }
                // Décor sans matériau qui sache devenir transparent (shader d'un pack) : coupe d'avant, abaissé ou retiré.
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

        /// <summary>V1.10 : le décor est-il entre la caméra et l'un des points du corps (<see cref="OccluderRules.BodyPoints"/>) ?</summary>
        private static bool Masks(Vector3 camera, Vector3 player, Occluder o)
        {
            return OccluderRules.MasksBody((camera.x, camera.y, camera.z), (player.x, player.y, player.z), (o.Min.x, o.Min.y, o.Min.z), (o.Max.x, o.Max.y, o.Max.z));
        }

        /// <summary>
        /// V1.10 : prépare le fondu d'un décor, la première fois qu'il cache le joueur : ses renderers (ceux de ses enfants pour un mur
        /// d'intérieur, qui porte aussi ses meubles accrochés), leurs matériaux et une copie transparente de chacun. Un seul matériau qui
        /// ne sait pas devenir transparent (shader sans « _Surface » ni « _BaseColor », comme les feuillages) : pas de fondu, la coupe
        /// d'avant reste. Les particules (feu de la cheminée) ne s'effacent pas.
        /// </summary>
        private static void PrepareFade(Occluder o)
        {
            o.FadeChecked = true;
            Renderer[] all = o.Renderer != null ? new[] { o.Renderer } : o.Transform.GetComponentsInChildren<Renderer>();
            var renderers = new List<Renderer>();
            foreach (Renderer r in all)
                if (r != null && !(r is ParticleSystemRenderer)) renderers.Add(r);
            if (renderers.Count == 0) return;
            var copies = new Dictionary<Material, Material>();
            var solid = new Material[renderers.Count][];
            var clear = new Material[renderers.Count][];
            var alpha = new float[renderers.Count][];
            for (int i = 0; i < renderers.Count; i++)
            {
                Material[] materials = renderers[i].sharedMaterials ?? new Material[0];
                solid[i] = materials;
                clear[i] = new Material[materials.Length];
                alpha[i] = new float[materials.Length];
                for (int k = 0; k < materials.Length; k++)
                {
                    Material m = materials[k];
                    if (m == null || !m.HasProperty("_Surface") || !m.HasProperty("_BaseColor"))
                    {
                        foreach (Material made in copies.Values) Object.Destroy(made);
                        return;
                    }
                    if (!copies.TryGetValue(m, out Material copy)) copies[m] = copy = Transparent(m);
                    clear[i][k] = copy;
                    alpha[i][k] = m.GetColor("_BaseColor").a;
                }
            }
            o.Faded = renderers.ToArray();
            o.WasEnabled = renderers.Select(r => r.enabled).ToArray();
            o.Applied = 1f;
            o.AppliedShown = true;
            o.Solid = solid;
            o.Clear = clear;
            o.Alpha = alpha;
        }

        /// <summary>
        /// V1.10 : copie transparente (mélange alpha d'URP, sans écriture de profondeur, file des transparents) d'un matériau d'URP. Le modèle
        /// transparent est emporté par la construction (<c>EdsBuildShaders</c>, « EdsCouleurFondu ») pour que sa variante reste dans le jeu.
        /// </summary>
        private static Material Transparent(Material source)
        {
            var copy = new Material(source);
            if (source.GetFloat("_Surface") > 0.5f) return copy;   // déjà transparent (rais de lumière) : son mélange reste
            ParticleBlend blend = ParticleRules.Transparent(0);
            copy.SetFloat("_Surface", blend.Surface);
            copy.SetFloat("_Blend", blend.Blend);
            copy.SetFloat("_SrcBlend", blend.Src);
            copy.SetFloat("_DstBlend", blend.Dst);
            copy.SetFloat("_SrcBlendAlpha", blend.SrcAlpha);
            copy.SetFloat("_DstBlendAlpha", blend.DstAlpha);
            copy.SetFloat("_ZWrite", 0f);
            copy.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            copy.SetOverrideTag("RenderType", "Transparent");
            copy.renderQueue = blend.Queue;
            return copy;
        }

        /// <summary>
        /// V1.10 : met l'opacité du décor (copies transparentes tant qu'il n'est pas plein, matériaux d'origine sinon). Relecture : rien
        /// n'est réappliqué si l'opacité n'a pas changé ; un renderer n'est allumé ou éteint qu'au passage du seuil <see cref="OccluderRules.Shown"/>,
        /// et un renderer éteint avant le fondu (ailleurs dans le jeu) n'est jamais rallumé.
        /// </summary>
        private static void ApplyFade(Occluder o)
        {
            if (Mathf.Abs(o.Cut - o.Applied) < 1e-4f) return;
            bool clear = OccluderRules.Clear(o.Cut);
            bool shown = OccluderRules.Shown(o.Cut);
            for (int i = 0; i < o.Faded.Length; i++)
            {
                Renderer r = o.Faded[i];
                if (r == null) continue;
                if (clear != o.Clearing) r.sharedMaterials = clear ? o.Clear[i] : o.Solid[i];
                if (shown != o.AppliedShown) r.enabled = shown && o.WasEnabled[i];
                if (!clear || !shown) continue;
                for (int k = 0; k < o.Clear[i].Length; k++)
                {
                    Material m = o.Clear[i][k];
                    Color c = m.GetColor("_BaseColor");
                    c.a = o.Alpha[i][k] * o.Cut;
                    m.SetColor("_BaseColor", c);
                }
            }
            o.Clearing = clear;
            o.AppliedShown = shown;
            o.Applied = o.Cut;
        }

        /// <summary>V1.10 : rend au décor ses matériaux d'origine et libère les copies transparentes.</summary>
        private static void ReleaseFade(Occluder o)
        {
            if (o.Faded == null) return;
            var made = new HashSet<Material>();
            for (int i = 0; i < o.Faded.Length; i++)
            {
                if (o.Faded[i] != null)
                {
                    o.Faded[i].sharedMaterials = o.Solid[i];
                    if (!o.AppliedShown) o.Faded[i].enabled = o.WasEnabled[i];   // rendu à son état d'origine
                }
                foreach (Material m in o.Clear[i]) made.Add(m);
            }
            foreach (Material m in made) Object.Destroy(m);
            o.Faded = null;
            o.FadeChecked = false;
            o.Clearing = false;
        }

        private static bool Overlaps(Occluder o, Vector3 min, Vector3 max) =>
            o.Max.x >= min.x && o.Min.x <= max.x && o.Max.z >= min.z && o.Min.z <= max.z;

        // --- Attaques annoncées au sol -------------------------------------------------------------------------------------

        /// <summary>
        /// Dessine les attaques annoncées. Essai réaliste 6 (essai de Damien du 27 octobre 2026 : « la charge rouge du sanglier ou les rond
        /// des autres personnages ») : une forme translucide au bord lumineux (shader « EdS/Annonce »), dont le front avance jusqu'au bout
        /// au moment de l'impact, avec des chevrons ou des ondes ; rouge vif et pulsée si l'attaque est imparable ; une zone qui reste
        /// pulse pendant ses impulsions. Une attaque dirigée annoncée (sans forme) est un cercle sous sa cible.
        /// </summary>
        public void Zones(IReadOnlyList<AnnouncedAttack> announced, double now, System.Func<string, Vector3?> position,
            System.Func<string, string, bool> unblockable)
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
                bool lingering = now >= attack.ImpactTick && attack.LastTick > attack.ImpactTick;
                if (used == _zones.Count) _zones.Add(new ZoneView(_root.transform, _fill));
                ZoneView zone = _zones[used++];
                bool danger = unblockable(attack.Actor, attack.Attack);
                if (attack.Zone != null) zone.Show(attack.Zone, progress, danger, lingering);
                else if (attack.Target != null && position(attack.Target) is Vector3 p) zone.ShowCircle(p.x, p.z, 1.1f, progress, danger, false);
                else zone.Hide();
            }
            for (int i = used; i < _zones.Count; i++) _zones[i].Hide();
            foreach (string gone in _firstSeen.Keys.Where(k => !seen.Contains(k)).ToList()) _firstSeen.Remove(gone);
        }

        public void Destroy()
        {
            StyleWorld.Release(_terrain);   // essai réaliste : données du terrain créées par le code, rendues avec lui
            _grass?.Destroy();
            foreach (StyleGrass crop in _crops) crop.Destroy();
            foreach (Mesh mesh in _meshes) Object.Destroy(mesh);   // relecture des Vallons agrandis : les maillages faits par le code
            foreach (Occluder o in _occluders) ReleaseFade(o);   // V1.10 : copies transparentes des décors passés en fondu
            StyleWorld.Deck(0f, 0f, 0f, 0f, 0f);   // essai réaliste 5 : plus de pont cintré sous les pieds
            if (_indoor) StyleWorld.SetIndoors(false);   // intérieurs : le ciel, le soleil et la brume reviennent
            ReleaseNight();   // sous-lot V2.1 : matériaux du décor de nuit
            ReleaseWeather();   // sous-lot V2.2 : matériau des flaques
            Object.Destroy(_root);
        }

        /// <summary>
        /// Vallons agrandis : le décor est gardé pour un nouveau combat sur le même espace (<see cref="Key"/>) : les annonces au sol sont
        /// effacées et tout ce qui avait été coupé reprend sa taille.
        /// </summary>
        public void Reset()
        {
            _firstSeen.Clear();
            foreach (ZoneView zone in _zones) zone.Hide();
            foreach (Occluder o in _occluders)
            {
                o.Cut = 1f;
                if (o.Faded != null)
                {
                    ApplyFade(o);   // V1.10 : matériaux d'origine, décor plein
                    continue;
                }
                if (o.Lower)
                {
                    Vector3 s = o.Transform.localScale;
                    o.Transform.localScale = new Vector3(s.x, 1f, s.z);
                }
                else if (o.Renderer != null) o.Renderer.enabled = true;
            }
        }
    }

    /// <summary>
    /// Essai réaliste 6 : matériau des annonces au sol (shader « EdS/Annonce ») : celui emporté par la construction
    /// (<c>Resources/EdsAnnonce</c>, voir <c>EdsBuildShaders</c>), sinon le shader du projet ; null s'il manque (forme pleine ordinaire).
    /// Sert aussi aux anneaux de la cible et de l'allié visé (<see cref="ArenaView"/>).
    /// </summary>
    internal static class Annonces
    {
        public const string MaterialName = "EdsAnnonce";
        private static Material _template;
        private static bool _loaded;

        public static Material Template
        {
            get
            {
                if (_loaded) return _template;
                _loaded = true;
                _template = Resources.Load<Material>(MaterialName);
                if (_template == null)
                {
                    Shader shader = Shader.Find("EdS/Annonce");
                    if (shader != null && shader.isSupported) _template = new Material(shader);
                }
                return _template;
            }
        }

        /// <summary>
        /// Remplit <paramref name="mesh"/> avec la forme <paramref name="shape"/> : avancée et largeur dans les premières coordonnées de
        /// texture, distance au bord et portée dans les secondes ; <paramref name="followDeck"/> : chaque sommet monte avec le tablier du
        /// pont cintré (revue Codex de la PR #60 : une annonce qui touche le pont suit son tablier ; les cases d'environ 30 cm suffisent).
        /// </summary>
        public static void Fill(Mesh mesh, TelegraphMesh shape, bool followDeck)
        {
            int n = shape.Vertices.Count;
            var vertices = new Vector3[n];
            var uv = new Vector2[n];
            var uv2 = new Vector2[n];
            bool deck = followDeck && n > 0 && StyleWorld.OnDeck((float)shape.Vertices.Min(v => v.X), (float)shape.Vertices.Min(v => v.Z),
                (float)shape.Vertices.Max(v => v.X), (float)shape.Vertices.Max(v => v.Z));
            for (int i = 0; i < n; i++)
            {
                float x = (float)shape.Vertices[i].X, z = (float)shape.Vertices[i].Z;
                vertices[i] = new Vector3(x, deck ? StyleWorld.StandHeight(x, z) : 0f, z);
                uv[i] = new Vector2((float)shape.Progress[i], (float)shape.Side[i]);
                uv2[i] = new Vector2((float)shape.Edge[i], (float)shape.Reach);
            }
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.uv2 = uv2;
            mesh.triangles = shape.Triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
    }

    /// <summary>
    /// Sous-lot 11.6 : une forme d'attaque annoncée au sol, refaite à chaque image ; dessinée au-dessus de la plus haute surface où l'on
    /// marche (tablier du pont, eau, décor abaissé : 0,12 m), les bandes ont les bouts arrondis du moteur (revue Codex de la PR #34).
    /// Essai réaliste 6 : une seule forme pleine (<see cref="TelegraphShapes"/>) dessinée par le shader « EdS/Annonce » (bord lumineux,
    /// front qui avance jusqu'à l'impact, chevrons ou ondes, rouge pulsé si imparable) ; sans ce shader, la forme pleine ordinaire.
    /// </summary>
    internal sealed class ZoneView
    {
        private const float Height = 0.16f;
        private readonly GameObject _body;
        private readonly MeshRenderer _renderer;
        private readonly Mesh _mesh = new Mesh();
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private readonly bool _styled;

        public ZoneView(Transform parent, Material plain)
        {
            _body = new GameObject("Annonce");
            _body.transform.SetParent(parent, false);
            _body.transform.position = new Vector3(0f, Height, 0f);
            _body.AddComponent<MeshFilter>().mesh = _mesh;
            _renderer = _body.AddComponent<MeshRenderer>();
            Material template = Annonces.Template;
            _styled = template != null;
            _renderer.sharedMaterial = template ?? plain;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
        }

        public void Hide() => _body.SetActive(false);

        /// <summary>Forme du maillage actuel : il n'est refait que si elle change (relecture de l'essai 6 : pas de maillage neuf à chaque image).</summary>
        private string _shape;

        public void Show(AttackZone zone, float progress, bool unblockable, bool lingering)
        {
            string key = zone.Shape + "|" + zone.OriginX + "|" + zone.OriginZ + "|" + zone.AxisX + "|" + zone.AxisZ + "|" + zone.Reach + "|" + zone.Angle + "|"
                + zone.HalfWidth;
            if (key != _shape)
            {
                _shape = key;
                float ox = zone.OriginX / 10000f, oz = zone.OriginZ / 10000f, reach = zone.Reach / 10000f;
                float ax = zone.AxisX / 10000f, az = zone.AxisZ / 10000f;
                TelegraphMesh shape = zone.Shape == AttackShape.Cone ? TelegraphShapes.Cone(ox, oz, Mathf.Atan2(az, ax), zone.Angle * Mathf.Deg2Rad, reach)
                    : zone.Shape == AttackShape.Line ? TelegraphShapes.Line(ox, oz, ax, az, reach, zone.HalfWidth / 10000f)
                    : TelegraphShapes.Circle(ox, oz, reach);   // cercle, zone qui reste
                Annonces.Fill(_mesh, shape, true);
            }
            _body.transform.position = new Vector3(0f, Height, 0f);
            Style(zone.Shape == AttackShape.Cone || zone.Shape == AttackShape.Line ? 1f : 0f, progress, unblockable, lingering);
        }

        /// <summary>
        /// Cercle sous la cible d'une attaque dirigée : maillage centré, refait seulement si le rayon change ; l'annonce suit la cible
        /// (rehaussée du tablier du pont sous son centre).
        /// </summary>
        public void ShowCircle(float x, float z, float radius, float progress, bool unblockable, bool lingering)
        {
            string key = "cible|" + Mathf.RoundToInt(radius * 1000f);
            if (key != _shape)
            {
                _shape = key;
                Annonces.Fill(_mesh, TelegraphShapes.Circle(0, 0, radius), false);
            }
            _body.transform.position = new Vector3(x, Height + StyleWorld.StandHeight(x, z), z);
            Style(0f, progress, unblockable, lingering);
        }

        private void Style(float kind, float progress, bool unblockable, bool lingering)
        {
            if (_styled)
            {
                _block.SetFloat("_Progress", lingering ? 1f : progress);
                _block.SetFloat("_Kind", kind);
                _block.SetFloat("_Unblockable", unblockable ? 1f : 0f);
                _block.SetFloat("_Linger", lingering ? 1f : 0f);
                _renderer.SetPropertyBlock(_block);
            }
            _body.SetActive(true);
        }
    }
}
