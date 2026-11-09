using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Space;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Intérieurs (choix de Damien du 28 octobre 2026 : « on fait la façon 1 », puis « je veux vraiment de beaux intérieurs bien détaillés,
    /// le mieux que tu puisses faire ») : le décor d'une pièce, construit par le code d'après l'aménagement de <see cref="InteriorRules"/> :
    /// plancher ou dallage, murs (crépi, colombages et soubassement de pierre ; pierre pour la chapelle) percés de la porte et des fenêtres,
    /// poutres du plafond, cheminée avec son feu, comptoir, escalier, autel, bancs d'église, pieds de chandelier ; les meubles et objets de
    /// Poly Haven du catalogue (une forme simple à défaut) ; les feux, bougies et lanternes qui éclairent la pièce, et le jour qui entre par
    /// les fenêtres. Un mur qui cache le joueur s'abaisse, comme les murs dehors. Affichage seulement : le moteur ne connaît que l'aire et
    /// les obstacles.
    /// </summary>
    public sealed partial class SpaceView
    {
        /// <summary>
        /// V1.10 (essai 10 : « tache blanche du feu ») : force de la lueur des braises de la cheminée. À 3,5, la plaque de braises (0,9 ×
        /// 0,45 m) dépassait trois fois le blanc : le tonemapping la blanchissait et le halo (bloom) des intérieurs l'étalait en une tache
        /// blanche sur le foyer. À 1,4, elle reste orange et brille encore un peu.
        /// </summary>
        private const float EmberGlow = 1.4f;

        /// <summary>Épaisseur (m) des murs d'une pièce (dessinés hors de l'aire).</summary>
        private const float WallThickness = 0.3f;

        /// <summary>Largeur et hauteur (m) de l'ouverture d'une porte.</summary>
        private const float DoorWidth = 1.4f, DoorHeight = 2.35f;

        private bool _indoor;
        private InteriorLayout _layout;

        /// <summary>Lumières qui vacillent (feu, bougies, lanternes) : la lumière, sa force de base, sa graine.</summary>
        private readonly List<(Light Light, float Base, float Seed, float Speed)> _flickers = new List<(Light, float, float, float)>();

        private static readonly Dictionary<string, Material> TintedMaterials = new Dictionary<string, Material>();

        /// <summary>Matériau texturé assombri ou teinté (bois sombre des poutres, suie), partagé.</summary>
        private static Material Tinted(Material source, Color tint, string key)
        {
            if (TintedMaterials.TryGetValue(key, out Material cached) && cached != null) return cached;
            var copy = new Material(source);
            if (copy.HasProperty("_BaseColor")) copy.SetColor("_BaseColor", copy.GetColor("_BaseColor") * tint);
            if (copy.HasProperty("_Color")) copy.SetColor("_Color", copy.GetColor("_Color") * tint);
            TintedMaterials[key] = copy;
            return copy;
        }

        /// <summary>Matériau qui brille (flamme, braises, carreaux d'une fenêtre au jour), partagé.</summary>
        private static Material Glow(Color color, float strength)
        {
            string key = "lueur " + color + " " + strength;
            if (TintedMaterials.TryGetValue(key, out Material cached) && cached != null) return cached;
            Material material = ArenaView.MakeMaterial(color);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * new Color(strength, strength, strength));
            }
            TintedMaterials[key] = material;
            return material;
        }

        /// <summary>Matériau des rais de lumière des fenêtres : additif, transparent, fondu par les couleurs des sommets ; null sans shader.</summary>
        private static Material _shaft;
        private static bool _shaftTried;

        private static Material ShaftMaterial()
        {
            if (_shaftTried) return _shaft;
            _shaftTried = true;
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) return null;
            _shaft = new Material(shader);
            ParticleBlend blend = ParticleRules.Blend(4);   // additif (source × alpha + destination)
            _shaft.SetFloat("_Surface", 1f);
            _shaft.SetFloat("_Blend", blend.Blend);
            _shaft.SetFloat("_SrcBlend", blend.Src);
            _shaft.SetFloat("_DstBlend", blend.Dst);
            _shaft.SetFloat("_ZWrite", 0f);
            _shaft.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _shaft.SetOverrideTag("RenderType", "Transparent");
            _shaft.renderQueue = 3000;
            _shaft.SetColor("_BaseColor", new Color(1f, 0.93f, 0.8f, 1f));
            return _shaft;
        }

        /// <summary>Construit la pièce de <paramref name="map"/> (un intérieur).</summary>
        private void Interior(CombatSpace space, AdventureMap map)
        {
            _indoor = true;
            StyleWorld.SetIndoors(true);
            InteriorLayout layout = InteriorRules.Layout(map);
            _layout = layout;
            float minX = Raw(space.Min.X.Raw), minZ = Raw(space.Min.Z.Raw), maxX = Raw(space.Max.X.Raw), maxZ = Raw(space.Max.Z.Raw);
            float height = (float)layout.WallHeight;
            bool chapel = layout.Style == InteriorStyle.Chapel;

            Material wood = Built(StyleSurface.Wood, new Color(0.45f, 0.32f, 0.2f), out float woodTile);
            Material darkWood = Tinted(wood, new Color(0.5f, 0.42f, 0.36f), "bois sombre");
            Material plaster = Built(StyleSurface.Plaster, new Color(0.84f, 0.79f, 0.7f), out float plasterTile);
            Material stone = Built(StyleSurface.Stone, new Color(0.5f, 0.47f, 0.42f), out float stoneTile);
            Material flags = Built(StyleSurface.Cobble, new Color(0.46f, 0.44f, 0.4f), out float flagTile);

            // Sol : plancher (auberge, maison) ou dallage (chapelle), un peu plus grand que l'aire (sous les murs).
            Box("Sol", new Vector3((minX + maxX) / 2f, -0.05f, (minZ + maxZ) / 2f), new Vector3(maxX - minX + 2 * WallThickness, 0.1f, maxZ - minZ + 2 * WallThickness),
                chapel ? flags : wood, chapel ? flagTile : Mathf.Max(0.8f, woodTile * 0.6f));

            Walls(map, layout, minX, minZ, maxX, maxZ, height, chapel ? stone : plaster, chapel ? stoneTile : plasterTile, stone, stoneTile, darkWood, woodTile, chapel);
            Beams(layout, minX, minZ, maxX, maxZ, height, darkWood, woodTile, chapel);

            // Meubles faits par le code d'après les obstacles.
            foreach (SpaceObstacle o in space.Obstacles)
            {
                // Sous-lot V1.9.8 : la porte de lumière du souvenir est une arche de lumière (StoryStage), pas un autel.
                if (StoryStage.IsLightGate(o.Id)) continue;
                float x0 = Raw(o.Points.Min(p => p.X.Raw)), x1 = Raw(o.Points.Max(p => p.X.Raw)), z0 = Raw(o.Points.Min(p => p.Z.Raw)), z1 = Raw(o.Points.Max(p => p.Z.Raw));
                switch (InteriorRules.FurnitureFor(o.Id))
                {
                    case InteriorFurniture.Counter: Counter(o.Id, x0, z0, x1, z1, map, wood, darkWood, woodTile); break;
                    case InteriorFurniture.Hearth: Hearth(o.Id, x0, z0, x1, z1, map, height, stone, stoneTile, darkWood, woodTile); break;
                    case InteriorFurniture.Stairs: Stairs(o.Id, x0, z0, x1, z1, map, wood, darkWood, woodTile); break;
                    case InteriorFurniture.Altar: Altar(o.Id, x0, z0, x1, z1, stone, stoneTile); break;
                    case InteriorFurniture.Pew: Pew(o.Id, x0, z0, x1, z1, map, darkWood, woodTile); break;
                    case InteriorFurniture.Candelabra: Candelabra(o.Id, x0, z0, x1, z1); break;
                    case InteriorFurniture.Statue: Box(o.Id + " (socle)", new Vector3((x0 + x1) / 2f, 0.225f, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.45f, z1 - z0), stone, stoneTile); break;
                }
            }

            // Meubles et objets du catalogue (Poly Haven), dans l'ordre de l'aménagement (un objet posé sur un autre vient après lui).
            var placed = new GameObject[layout.Pieces.Count];
            for (int i = 0; i < layout.Pieces.Count; i++)
            {
                InteriorPiece piece = layout.Pieces[i];
                if (StoryStage.IsLightGate(piece.Id)) continue;   // sous-lot V1.9.8 : ni chandeliers ni trône sur la porte de lumière
                float y = (float)piece.Y;
                if (piece.Parent >= 0)
                {
                    GameObject under = placed[piece.Parent];
                    if (under == null) continue;   // pas de meuble dessous : rien dessus
                    y = StyleLook.VisibleBounds(under, out Bounds ub) ? ub.max.y : y;
                }
                Transform parent = _root.transform;
                if (piece.OnWall)
                {
                    (int X, int Z) wall = InteriorRules.NearestWall(map, piece.X, piece.Z);
                    parent = WallRoot(wall);
                }
                placed[i] = Furnish(piece, parent, new Vector3((float)piece.X, y, (float)piece.Z), map);
            }

            Lights(layout);
            // Sous-lot V1.9.8 : le souvenir de la Rupture n'a pas de villageois (ni fidèle devant la porte de lumière, ni paysan à la place de
            // la reine Ysolde : StoryStage la dessine).
            if (!space.Obstacles.Any(o => StoryStage.IsLightGate(o.Id))) Villagers(layout.Villagers);
        }

        // --- Murs, fenêtres, porte ------------------------------------------------------------------------------------------

        /// <summary>Support des pièces d'un mur (à l'origine) : il s'abaisse d'un bloc quand le mur cache le joueur.</summary>
        private readonly Dictionary<(int, int), Transform> _wallRoots = new Dictionary<(int, int), Transform>();

        private Transform WallRoot((int X, int Z) wall) =>
            _wallRoots.TryGetValue(wall, out Transform root) ? root : _root.transform;

        private void Walls(AdventureMap map, InteriorLayout layout, float minX, float minZ, float maxX, float maxZ, float height, Material wallMaterial, float wallTile,
            Material stone, float stoneTile, Material darkWood, float woodTile, bool chapel)
        {
            float cx = (minX + maxX) / 2f, cz = (minZ + maxZ) / 2f;
            Material glass = Glow(new Color(0.78f, 0.84f, 0.92f), 1.6f);
            Material shaft = ShaftMaterial();
            foreach ((int X, int Z) wall in new[] { (0, 1), (0, -1), (1, 0), (-1, 0) })
            {
                var root = new GameObject("Mur " + wall);
                root.transform.SetParent(_root.transform, false);
                _wallRoots[wall] = root.transform;
                // Le mur longe l'aire : t le long du mur (depuis le milieu de la pièce), de -half à +half, épaisseur vers l'extérieur.
                bool alongX = wall.Z != 0;
                float half = (alongX ? maxX - minX : maxZ - minZ) / 2f + WallThickness;
                float inner = wall.X > 0 ? minX : wall.X < 0 ? maxX : wall.Z > 0 ? minZ : maxZ;   // face intérieure
                float outward = -(wall.X + wall.Z);   // vers l'extérieur de la pièce, le long de la normale
                Vector3 At(float t, float offset) => alongX
                    ? new Vector3(cx + t, 0f, inner + outward * offset)
                    : new Vector3(inner + outward * offset, 0f, cz + t);
                // Ouvertures : portes et fenêtres de ce mur.
                var openings = new List<(float T, float Width, float Bottom, float Top, bool Door)>();
                foreach (MapDoor door in map.Doors)
                    if (InteriorRules.NearestWall(map, door.X, door.Z) == wall)
                        openings.Add(((float)(alongX ? door.X - cx : door.Z - cz), DoorWidth, 0f, DoorHeight, true));
                foreach (InteriorWindow w in layout.Windows.Where(w => w.Wall == wall))
                    openings.Add(((float)(alongX ? w.X - cx : w.Z - cz), (float)w.Width, (float)w.Bottom, (float)w.Top, false));
                openings.Sort((a, b) => a.T.CompareTo(b.T));
                // Pans pleins entre les ouvertures, allèges et linteaux.
                float from = -half;
                foreach (var o in openings)
                {
                    float a = o.T - o.Width / 2f, b = o.T + o.Width / 2f;
                    WallPiece(root.transform, wall, At, from, a, 0f, height, wallMaterial, wallTile);
                    if (o.Bottom > 0f) WallPiece(root.transform, wall, At, a, b, 0f, o.Bottom, wallMaterial, wallTile);
                    WallPiece(root.transform, wall, At, a, b, o.Top, height, wallMaterial, wallTile);
                    from = b;
                }
                WallPiece(root.transform, wall, At, from, half, 0f, height, wallMaterial, wallTile);

                if (!chapel)
                {
                    // Soubassement de pierre (sous les fenêtres aussi, pas sous la porte), colombages : poteaux, sablière, linteaux.
                    float s0 = -half + WallThickness;
                    foreach (var o in openings.Where(o => o.Door))
                    {
                        WallTrim(root.transform, wall, At, s0, o.T - o.Width / 2f, 0f, 0.55f, 0.04f, stone, stoneTile);
                        s0 = o.T + o.Width / 2f;
                    }
                    WallTrim(root.transform, wall, At, s0, half - WallThickness, 0f, 0.55f, 0.04f, stone, stoneTile);
                    WallTrim(root.transform, wall, At, -half + WallThickness, half - WallThickness, height - 0.38f, height - 0.16f, 0.07f, darkWood, woodTile);
                    var posts = new List<float> { -half + WallThickness + 0.1f, half - WallThickness - 0.1f };
                    foreach (var o in openings)
                    {
                        posts.Add(o.T - o.Width / 2f - 0.1f);
                        posts.Add(o.T + o.Width / 2f + 0.1f);
                        WallTrim(root.transform, wall, At, o.T - o.Width / 2f - 0.2f, o.T + o.Width / 2f + 0.2f, o.Top, o.Top + 0.18f, 0.07f, darkWood, woodTile);
                        if (!o.Door) WallTrim(root.transform, wall, At, o.T - o.Width / 2f - 0.12f, o.T + o.Width / 2f + 0.12f, o.Bottom - 0.08f, o.Bottom, 0.1f, darkWood, woodTile);
                    }
                    // Poteaux intermédiaires tous les 1,8 m environ, hors des ouvertures.
                    for (float t = -half + 1.9f; t < half - 1.2f; t += 1.8f)
                        if (openings.All(o => Mathf.Abs(t - o.T) > o.Width / 2f + 0.5f) && posts.All(p => Mathf.Abs(p - t) > 0.6f)) posts.Add(t);
                    foreach (float t in posts) WallTrim(root.transform, wall, At, t - 0.1f, t + 0.1f, 0.55f, height - 0.38f, 0.06f, darkWood, woodTile);
                    // Traverse à mi-hauteur entre les ouvertures (colombage).
                    float r0 = -half + WallThickness;
                    foreach (var o in openings)
                    {
                        WallTrim(root.transform, wall, At, r0, o.T - o.Width / 2f, 1.95f, 2.1f, 0.05f, darkWood, woodTile);
                        r0 = o.T + o.Width / 2f;
                    }
                    WallTrim(root.transform, wall, At, r0, half - WallThickness, 1.95f, 2.1f, 0.05f, darkWood, woodTile);
                }
                else
                {
                    // Chapelle : pilastres de pierre entre les fenêtres, corniche en haut.
                    for (float t = -half + 1.5f; t < half - 1.0f; t += 3.0f)
                        if (openings.All(o => Mathf.Abs(t - o.T) > o.Width / 2f + 0.4f)) WallTrim(root.transform, wall, At, t - 0.25f, t + 0.25f, 0f, height - 0.4f, 0.14f, stone, stoneTile);
                    WallTrim(root.transform, wall, At, -half + WallThickness, half - WallThickness, height - 0.45f, height - 0.2f, 0.16f, stone, stoneTile);
                }

                // Fenêtres : cadre, meneau, carreaux qui brillent, rai de lumière vers le sol.
                foreach (var o in openings.Where(o => !o.Door))
                {
                    float frame = 0.07f;
                    WallTrim(root.transform, wall, At, o.T - o.Width / 2f, o.T - o.Width / 2f + frame, o.Bottom, o.Top, 0.05f, darkWood, woodTile);
                    WallTrim(root.transform, wall, At, o.T + o.Width / 2f - frame, o.T + o.Width / 2f, o.Bottom, o.Top, 0.05f, darkWood, woodTile);
                    WallTrim(root.transform, wall, At, o.T - 0.03f, o.T + 0.03f, o.Bottom, o.Top, 0.03f, darkWood, woodTile);
                    for (float y = o.Bottom + 0.45f; y < o.Top - 0.2f; y += 0.45f) WallTrim(root.transform, wall, At, o.T - o.Width / 2f, o.T + o.Width / 2f, y - 0.02f, y + 0.02f, 0.03f, darkWood, woodTile);
                    Vector3 pane = At(o.T, WallThickness * 0.5f) + Vector3.up * ((o.Bottom + o.Top) / 2f);
                    GameObject glassPane = Block("Carreaux", pane, alongX ? new Vector3(o.Width, o.Top - o.Bottom, 0.02f) : new Vector3(0.02f, o.Top - o.Bottom, o.Width), glass);
                    glassPane.transform.SetParent(root.transform, true);
                    if (shaft != null) Shaft(root.transform, wall, At(o.T, 0f), o.Width, o.Bottom, o.Top, shaft);
                }

                var min = alongX ? new Vector3(minX - WallThickness, 0f, Mathf.Min(inner, inner + outward * WallThickness)) : new Vector3(Mathf.Min(inner, inner + outward * WallThickness), 0f, minZ - WallThickness);
                var max = alongX ? new Vector3(maxX + WallThickness, height, Mathf.Max(inner, inner + outward * WallThickness)) : new Vector3(Mathf.Max(inner, inner + outward * WallThickness), height, maxZ + WallThickness);
                _occluders.Add(new Occluder { Transform = root.transform, Lower = true, Min = min, Max = max });
            }
        }

        /// <summary>Pan de mur de <paramref name="from"/> à <paramref name="to"/> (le long du mur), de <paramref name="bottom"/> à <paramref name="top"/>.</summary>
        private void WallPiece(Transform parent, (int X, int Z) wall, System.Func<float, float, Vector3> at, float from, float to, float bottom, float top, Material material, float tile)
        {
            if (to - from < 0.01f || top - bottom < 0.01f) return;
            Vector3 a = at(from, 0f), b = at(to, 0f), c = at(to, WallThickness), d = at(from, WallThickness);
            var points = new List<Vector3> { a, b, c, d }.Select(p => new Vector3(p.x, bottom, p.z)).ToList();
            GameObject piece = Prism("Mur", points, top - bottom, material, false, tile);
            piece.transform.SetParent(parent, true);
        }

        /// <summary>Bande posée sur la face intérieure d'un mur (soubassement, poteau, sablière), saillante de <paramref name="depth"/> m.</summary>
        private void WallTrim(Transform parent, (int X, int Z) wall, System.Func<float, float, Vector3> at, float from, float to, float bottom, float top, float depth, Material material,
            float tile)
        {
            if (to - from < 0.01f || top - bottom < 0.01f) return;
            Vector3 a = at(from, -depth), b = at(to, -depth), c = at(to, 0.001f), d = at(from, 0.001f);
            var points = new List<Vector3> { a, b, c, d }.Select(p => new Vector3(p.x, bottom, p.z)).ToList();
            GameObject piece = Prism("Habillage", points, top - bottom, material, false, tile);
            piece.transform.SetParent(parent, true);
        }

        /// <summary>Rai de lumière d'une fenêtre : un volume transparent qui descend vers le sol de la pièce, plus clair contre la fenêtre.</summary>
        private void Shaft(Transform parent, (int X, int Z) wall, Vector3 sill, float width, float bottom, float top, Material material)
        {
            var inward = new Vector3(wall.X, 0f, wall.Z);
            var side = new Vector3(Mathf.Abs(wall.Z), 0f, Mathf.Abs(wall.X)) * (width / 2f);
            float reach = top * 0.9f;   // la lumière tombe à environ 50 degrés
            Vector3 w0 = sill + Vector3.up * top, w1 = sill + Vector3.up * bottom;
            Vector3 f0 = sill + inward * reach + Vector3.up * 0.01f, f1 = sill + inward * (reach * 0.35f) + Vector3.up * 0.01f;
            var vertices = new[] { w0 - side, w0 + side, w1 - side, w1 + side, f0 - side * 1.2f, f0 + side * 1.2f, f1 - side * 1.2f, f1 + side * 1.2f };
            var colors = new[]
            {
                new Color(1f, 1f, 1f, 0.16f), new Color(1f, 1f, 1f, 0.16f), new Color(1f, 1f, 1f, 0.16f), new Color(1f, 1f, 1f, 0.16f),
                new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, 0.02f), new Color(1f, 1f, 1f, 0.02f),
            };
            // Quatre faces : haut (w0 → f0), bas (w1 → f1), côtés ; vues des deux côtés (additif).
            int[] faces = { 0, 1, 5, 0, 5, 4, 2, 3, 7, 2, 7, 6, 0, 2, 6, 0, 6, 4, 1, 3, 7, 1, 7, 5 };
            var triangles = new List<int>();
            for (int i = 0; i < faces.Length; i += 3)
            {
                triangles.AddRange(new[] { faces[i], faces[i + 1], faces[i + 2] });
                triangles.AddRange(new[] { faces[i], faces[i + 2], faces[i + 1] });
            }
            var mesh = new Mesh { name = "Rai de lumière" };
            _meshes.Add(mesh);
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateBounds();
            var go = new GameObject("Rai de lumière");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().mesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>Poutres du plafond (et la panne qui les porte), en bois sombre ; chapelle : charpente plus haute.</summary>
        private void Beams(InteriorLayout layout, float minX, float minZ, float maxX, float maxZ, float height, Material darkWood, float woodTile, bool chapel)
        {
            float y = height - (chapel ? 0.6f : 0.3f);
            foreach (double at in layout.Beams)
            {
                Vector3 center = layout.BeamsAlongX ? new Vector3((minX + maxX) / 2f, y, (float)at) : new Vector3((float)at, y, (minZ + maxZ) / 2f);
                Vector3 size = layout.BeamsAlongX ? new Vector3(maxX - minX + 2 * WallThickness, 0.3f, 0.24f) : new Vector3(0.24f, 0.3f, maxZ - minZ + 2 * WallThickness);
                GameObject beam = Box("Poutre", center, size, darkWood, woodTile);
                // V1.10 (essai 10 : « la caméra passe dans les poutres ») : la caméra d'une pièce se tient au-dessus du plafond (jusqu'à 8 m
                // du personnage, 22° par défaut : 4 à 5 m de haut, poutres à 2,7 m) ; seules les poutres sur la ligne de la tête
                // s'effaçaient, les autres traversaient l'image et la caméra passait dedans en tournant. Une poutre est un décor
                // « au-dessus » : elle disparaît en fondu dès que la caméra arrive à sa hauteur (OccluderRules.Overhead), et revient quand
                // la caméra repasse dessous (de près, vue basse).
                _occluders.Add(new Occluder
                {
                    Transform = beam.transform, Renderer = beam.GetComponent<Renderer>(), Lower = false, Overhead = true,
                    Min = center - size / 2f, Max = center + size / 2f,
                });
            }
        }

        // --- Meubles faits par le code -----------------------------------------------------------------------------------------

        /// <summary>Direction (vecteur unitaire le long d'un axe) du mur le plus proche d'une boîte vers la pièce.</summary>
        private static (int X, int Z) Inward(AdventureMap map, float x0, float z0, float x1, float z1)
        {
            float w = x0 - (float)map.MinX, e = (float)map.MaxX - x1, s = z0 - (float)map.MinZ, n = (float)map.MaxZ - z1;
            float min = Mathf.Min(Mathf.Min(w, e), Mathf.Min(s, n));
            return min == s ? (0, 1) : min == n ? (0, -1) : min == w ? (1, 0) : (-1, 0);
        }

        /// <summary>Support d'un meuble fait par le code (à l'origine), abaissé d'un bloc quand il cache le joueur.</summary>
        private Transform Furniture(string id, float x0, float z0, float x1, float z1, float top)
        {
            var go = new GameObject(id + " (meuble)");
            go.transform.SetParent(_root.transform, false);
            _occluders.Add(new Occluder { Transform = go.transform, Lower = true, Min = new Vector3(x0, 0f, z0), Max = new Vector3(x1, top, z1) });
            return go.transform;
        }

        private GameObject Part(Transform parent, string name, Vector3 center, Vector3 size, Material material, float tile)
        {
            GameObject piece = Box(name, center, size, material, tile);
            piece.transform.SetParent(parent, true);
            return piece;
        }

        /// <summary>Comptoir de l'auberge : façade de planches à montants, plateau débordant, plinthe de pierre.</summary>
        private void Counter(string id, float x0, float z0, float x1, float z1, AdventureMap map, Material wood, Material darkWood, float woodTile)
        {
            Transform t = Furniture(id, x0, z0, x1, z1, (float)InteriorRules.CounterHeight);
            float h = (float)InteriorRules.CounterHeight;
            Vector3 c = new Vector3((x0 + x1) / 2f, 0f, (z0 + z1) / 2f);
            Part(t, id + " (caisson)", c + Vector3.up * (h - 0.06f) / 2f, new Vector3(x1 - x0 - 0.08f, h - 0.06f, z1 - z0 - 0.08f), darkWood, woodTile);
            Part(t, id + " (plateau)", c + Vector3.up * (h - 0.03f), new Vector3(x1 - x0 + 0.06f, 0.06f, z1 - z0 + 0.06f), wood, woodTile);
            bool alongX = x1 - x0 >= z1 - z0;
            float length = alongX ? x1 - x0 : z1 - z0;
            // Revue Codex de la PR #67 : un nombre entier borné de montants (InteriorRules.CounterPosts), plus de pas nul possible.
            foreach (double post in InteriorRules.CounterPosts(length))
            {
                float s = (float)post;
                Vector3 at = alongX ? new Vector3(x0 + s, (h - 0.06f) / 2f, c.z) : new Vector3(c.x, (h - 0.06f) / 2f, z0 + s);
                Part(t, id + " (montant)", at, alongX ? new Vector3(0.1f, h - 0.06f, z1 - z0 - 0.02f) : new Vector3(x1 - x0 - 0.02f, h - 0.06f, 0.1f), wood, woodTile);
            }
        }

        /// <summary>
        /// Cheminée : jambages et contrecœur de pierre, âtre, linteau de bois, hotte jusqu'au plafond, bûches qui brûlent (effet de feu du
        /// pack), braises ; des bûches empilées aux deux bouts.
        /// </summary>
        private void Hearth(string id, float x0, float z0, float x1, float z1, AdventureMap map, float height, Material stone, float stoneTile, Material darkWood, float woodTile)
        {
            Transform t = Furniture(id, x0, z0, x1, z1, height);
            (int X, int Z) f = Inward(map, x0, z0, x1, z1);
            bool alongX = f.Z != 0;   // la cheminée longe son mur
            float length = alongX ? x1 - x0 : z1 - z0, depth = alongX ? z1 - z0 : x1 - x0;
            float body = Mathf.Min(length, 2.3f), jamb = 0.38f, mantel = (float)InteriorRules.MantelHeight;
            var c = new Vector3((x0 + x1) / 2f, 0f, (z0 + z1) / 2f);
            Vector3 along = alongX ? new Vector3(1f, 0f, 0f) : new Vector3(0f, 0f, 1f);
            var inward = new Vector3(f.X, 0f, f.Z);
            Vector3 Size(float a, float h, float d) => alongX ? new Vector3(a, h, d) : new Vector3(d, h, a);
            Material soot = Tinted(stone, new Color(0.28f, 0.26f, 0.25f), "suie");
            // Âtre, jambages, contrecœur.
            Part(t, id + " (âtre)", c + Vector3.up * 0.06f, Size(body, 0.12f, depth), stone, stoneTile);
            for (int s = -1; s <= 1; s += 2)
                Part(t, id + " (jambage)", c + along * (s * (body / 2f - jamb / 2f)) + Vector3.up * (mantel / 2f), Size(jamb, mantel, depth), stone, stoneTile);
            Part(t, id + " (contrecœur)", c - inward * (depth / 2f - 0.1f) + Vector3.up * (mantel / 2f), Size(body - 2 * jamb, mantel, 0.2f), soot, stoneTile);
            // Linteau de bois, puis hotte jusqu'au plafond.
            Part(t, id + " (linteau)", c + Vector3.up * (mantel + 0.12f) + inward * 0.04f, Size(body + 0.2f, 0.24f, depth + 0.08f), darkWood, woodTile);
            Part(t, id + " (hotte)", c - inward * 0.08f + Vector3.up * ((mantel + 0.24f + height) / 2f), Size(body - 0.2f, height - mantel - 0.24f, depth - 0.16f), stone, stoneTile);
            // Bûches qui brûlent, braises, feu du pack.
            Material bark = StyleWorld.Surface(StyleSurface.Bark, out float barkTile) ?? darkWood;
            Vector3 fire = c + Vector3.up * 0.14f + inward * 0.05f;
            for (int i = 0; i < 3; i++)
            {
                GameObject log = Block(id + " (bûche)", fire + Vector3.up * (0.06f + 0.08f * (i == 2 ? 1 : 0)), alongX ? new Vector3(0.75f, 0.12f, 0.12f) : new Vector3(0.12f, 0.12f, 0.75f), bark);
                log.transform.rotation = Quaternion.Euler(0f, (i - 1) * 28f, 0f);
                log.transform.SetParent(t, true);
            }
            GameObject embers = Block(id + " (braises)", fire + Vector3.up * 0.01f, alongX ? new Vector3(0.9f, 0.04f, 0.45f) : new Vector3(0.45f, 0.04f, 0.9f),
                Glow(new Color(1f, 0.35f, 0.08f), EmberGlow));
            embers.transform.SetParent(t, true);
            Effects.Ambient(StyleVfx.ForgeFire, t, fire + Vector3.up * 0.12f);
            // Bois empilé aux deux bouts (dans l'emprise).
            float pile = (length - body) / 2f;
            if (pile > 0.3f)
                for (int s = -1; s <= 1; s += 2)
                    for (int row = 0; row < 3; row++)
                    {
                        Vector3 at = c + along * (s * (body / 2f + pile / 2f)) + Vector3.up * (0.08f + row * 0.15f) + inward * ((row % 2) * 0.05f);
                        GameObject log = Block(id + " (bois)", at, alongX ? new Vector3(pile * 0.8f, 0.14f, depth * 0.7f) : new Vector3(depth * 0.7f, 0.14f, pile * 0.8f), bark);
                        log.transform.SetParent(t, true);
                    }
        }

        /// <summary>Escalier de bois contre le mur : marches qui montent vers le mur, limon, rampe et balustres du côté de la pièce.</summary>
        private void Stairs(string id, float x0, float z0, float x1, float z1, AdventureMap map, Material wood, Material darkWood, float woodTile)
        {
            bool alongX = x1 - x0 >= z1 - z0;
            float length = alongX ? x1 - x0 : z1 - z0, width = alongX ? z1 - z0 : x1 - x0;
            float cx = (float)(map.MinX + map.MaxX) / 2f, cz = (float)(map.MinZ + map.MaxZ) / 2f;
            // Le bas des marches est le bout tourné vers le milieu de la pièce.
            float lo = alongX ? x0 : z0, hi = alongX ? x1 : z1, mid = alongX ? cx : cz;
            float dir = Mathf.Abs(hi - mid) < Mathf.Abs(lo - mid) ? -1f : 1f;   // sens de la montée
            float start = dir > 0 ? lo : hi;
            float rise = 2.7f;
            int steps = Mathf.Max(6, Mathf.RoundToInt(length / 0.24f));
            Transform t = Furniture(id, x0, z0, x1, z1, rise);
            float across = alongX ? (z0 + z1) / 2f : (x0 + x1) / 2f;
            for (int i = 0; i < steps; i++)
            {
                float s = start + dir * (i + 0.5f) * length / steps;
                float h = rise * (i + 1) / steps;
                Vector3 center = alongX ? new Vector3(s, h / 2f, across) : new Vector3(across, h / 2f, s);
                Part(t, id + " (marche)", center, alongX ? new Vector3(length / steps + 0.01f, h, width - 0.1f) : new Vector3(width - 0.1f, h, length / steps + 0.01f),
                    i % 2 == 0 ? wood : Tinted(wood, new Color(0.92f, 0.9f, 0.88f), "bois clair"), woodTile);
            }
            // Rampe du côté de la pièce (le côté le plus loin du mur).
            float sideLo = alongX ? z0 : x0, sideHi = alongX ? z1 : x1, other = alongX ? cz : cx;
            float rail = Mathf.Abs(sideLo - other) < Mathf.Abs(sideHi - other) ? sideLo + 0.04f : sideHi - 0.04f;
            for (int i = 0; i <= steps; i += 2)
            {
                float s = start + dir * i * length / steps;
                float h = rise * i / steps;
                Vector3 post = alongX ? new Vector3(s, (h + 0.95f + h) / 2f, rail) : new Vector3(rail, (h + 0.95f + h) / 2f, s);
                Part(t, id + " (balustre)", post, new Vector3(0.06f, 0.95f, 0.06f), darkWood, woodTile);
            }
            float span = Mathf.Sqrt(length * length + rise * rise);
            Vector3 railMid = alongX ? new Vector3(start + dir * length / 2f, rise / 2f + 0.95f, rail) : new Vector3(rail, rise / 2f + 0.95f, start + dir * length / 2f);
            GameObject top = Block(id + " (main courante)", railMid, alongX ? new Vector3(span, 0.07f, 0.09f) : new Vector3(0.09f, 0.07f, span), darkWood);
            float angle = Mathf.Atan2(rise, length) * Mathf.Rad2Deg * (alongX ? dir : -dir);
            top.transform.rotation = alongX ? Quaternion.Euler(0f, 0f, angle) : Quaternion.Euler(angle, 0f, 0f);
            top.transform.SetParent(t, true);
        }

        /// <summary>Autel de pierre couvert d'une nappe blanche, à bande rouge, qui retombe devant.</summary>
        private void Altar(string id, float x0, float z0, float x1, float z1, Material stone, float stoneTile)
        {
            float h = (float)InteriorRules.AltarHeight;
            Transform t = Furniture(id, x0, z0, x1, z1, h + 0.05f);
            var c = new Vector3((x0 + x1) / 2f, 0f, (z0 + z1) / 2f);
            Part(t, id + " (pierre)", c + Vector3.up * (h / 2f), new Vector3(x1 - x0 - 0.06f, h, z1 - z0 - 0.06f), stone, stoneTile);
            Material linen = Plain(new Color(0.88f, 0.86f, 0.8f));
            GameObject cloth = Block(id + " (nappe)", c + Vector3.up * (h + 0.01f), new Vector3(x1 - x0 + 0.1f, 0.02f, z1 - z0 + 0.1f), linen);
            cloth.transform.SetParent(t, true);
            bool alongX = x1 - x0 >= z1 - z0;
            GameObject band = Block(id + " (bande)", c + Vector3.up * (h + 0.025f), alongX ? new Vector3(0.36f, 0.012f, z1 - z0 + 0.12f) : new Vector3(x1 - x0 + 0.12f, 0.012f, 0.36f),
                Plain(new Color(0.55f, 0.08f, 0.07f)));
            band.transform.SetParent(t, true);
            // Retombée de la nappe sur les quatre côtés.
            foreach ((float dx, float dz) in new[] { (0f, 1f), (0f, -1f), (1f, 0f), (-1f, 0f) })
            {
                Vector3 at = c + new Vector3(dx * ((x1 - x0) / 2f + 0.05f), h - 0.2f, dz * ((z1 - z0) / 2f + 0.05f));
                Vector3 size = dx != 0 ? new Vector3(0.012f, 0.42f, z1 - z0 + 0.1f) : new Vector3(x1 - x0 + 0.1f, 0.42f, 0.012f);
                GameObject drape = Block(id + " (retombée)", at, size, linen);
                drape.transform.SetParent(t, true);
            }
        }

        /// <summary>Banc d'église : siège, dossier, accoudoirs pleins aux deux bouts, agenouilloir ; tourné vers l'autel.</summary>
        private void Pew(string id, float x0, float z0, float x1, float z1, AdventureMap map, Material darkWood, float woodTile)
        {
            Transform t = Furniture(id, x0, z0, x1, z1, 1.0f);
            bool alongX = x1 - x0 >= z1 - z0;
            float length = alongX ? x1 - x0 : z1 - z0, depth = alongX ? z1 - z0 : x1 - x0;
            // L'autel est vers le plus grand z (ou x) : on regarde vers le mur le plus lointain de l'entrée.
            float facing = 1f;
            if (map.Doors.Count > 0)
            {
                MapDoor door = map.Doors[0];
                facing = alongX ? (door.Z < (z0 + z1) / 2f ? 1f : -1f) : (door.X < (x0 + x1) / 2f ? 1f : -1f);
            }
            var c = new Vector3((x0 + x1) / 2f, 0f, (z0 + z1) / 2f);
            Vector3 across = alongX ? new Vector3(0f, 0f, facing) : new Vector3(facing, 0f, 0f);
            Vector3 Size(float a, float h, float d) => alongX ? new Vector3(a, h, d) : new Vector3(d, h, a);
            float back = -depth / 2f + 0.04f;   // le dossier est derrière celui qui est assis
            Part(t, id + " (siège)", c + across * (back + 0.24f) + Vector3.up * 0.44f, Size(length - 0.1f, 0.05f, 0.4f), darkWood, woodTile);
            Part(t, id + " (dossier)", c + across * back + Vector3.up * 0.7f, Size(length - 0.1f, 0.6f, 0.05f), darkWood, woodTile);
            Part(t, id + " (traverse)", c + across * (back + 0.2f) + Vector3.up * 0.2f, Size(length - 0.2f, 0.06f, 0.05f), darkWood, woodTile);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 end = alongX ? new Vector3(s * (length / 2f - 0.04f), 0f, 0f) : new Vector3(0f, 0f, s * (length / 2f - 0.04f));
                Part(t, id + " (joue)", c + end + across * (back + 0.2f) + Vector3.up * 0.48f, Size(0.07f, 0.96f, 0.48f), darkWood, woodTile);
            }
            Part(t, id + " (agenouilloir)", c + across * (depth / 2f - 0.08f) + Vector3.up * 0.1f, Size(length - 0.2f, 0.08f, 0.14f),
                Plain(new Color(0.4f, 0.1f, 0.09f)), woodTile);
        }

        /// <summary>Pied de chandelier de fer : trépied, fût, coupelle (la bougie est posée dessus par l'aménagement).</summary>
        private void Candelabra(string id, float x0, float z0, float x1, float z1)
        {
            Transform t = Furniture(id, x0, z0, x1, z1, 1.5f);
            Material iron = Plain(new Color(0.07f, 0.065f, 0.06f));
            var c = new Vector3((x0 + x1) / 2f, 0f, (z0 + z1) / 2f);
            Part(t, id + " (fût)", c + Vector3.up * 0.73f, new Vector3(0.045f, 1.46f, 0.045f), iron, 1f);
            Part(t, id + " (coupelle)", c + Vector3.up * 1.445f, new Vector3(0.2f, 0.02f, 0.2f), iron, 1f);
            for (int i = 0; i < 3; i++)
            {
                GameObject foot = Block(id + " (pied)", c + Vector3.up * 0.06f, new Vector3(0.36f, 0.03f, 0.04f), iron);
                foot.transform.rotation = Quaternion.Euler(0f, i * 120f, -8f);
                foot.transform.SetParent(t, true);
            }
        }

        // --- Meubles et objets du catalogue ------------------------------------------------------------------------------------

        /// <summary>
        /// Pose un objet de l'aménagement : le modèle du catalogue mis à l'échelle (son plus grand côté au sol, ou sa hauteur), tourné, le bas
        /// à la hauteur voulue, centré sur sa place ; accroché à un mur, son dos contre le mur. Sans modèle, une forme simple (ou rien pour la
        /// petite vaisselle). Les grands meubles s'effacent quand ils cachent le joueur.
        /// </summary>
        private GameObject Furnish(InteriorPiece piece, Transform parent, Vector3 at, AdventureMap map)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            InteriorLook look = catalog != null ? catalog.Interior(piece.Prop, piece.Id) : null;
            GameObject holder = look != null ? Model(look, piece, parent) : Fallback(piece, parent);
            if (holder == null) return null;
            if (!StyleLook.VisibleBounds(holder, out Bounds b)) return holder;
            Vector3 target = at;
            if (piece.OnWall)
            {
                (int X, int Z) wall = InteriorRules.NearestWall(map, piece.X, piece.Z);
                float depth = (wall.X != 0 ? b.size.x : b.size.z) / 2f;
                target += new Vector3(wall.X, 0f, wall.Z) * depth;
            }
            holder.transform.position += new Vector3(target.x - b.center.x, target.y - b.min.y, target.z - b.center.z);
            if (piece.Prop == InteriorProp.Candlestick) Candle(holder, look == null);
            if (piece.Prop == InteriorProp.Lantern || piece.Prop == InteriorProp.HangingLantern) Lit(holder, 0.35f);
            // Relecture indépendante : les lustres et les chandeliers de laiton brillent aussi (leurs bougies).
            if (piece.Prop == InteriorProp.Chandelier || piece.Prop == InteriorProp.Candelabra) Lit(holder, 0.18f);
            if (b.size.y > 1.6f && !piece.OnWall) AddModelOccluders(holder);
            return holder;
        }

        /// <summary>Copie du modèle du catalogue, à l'échelle de l'aménagement (et de celle réglée dans le catalogue), tournée.</summary>
        private static GameObject Model(InteriorLook look, InteriorPiece piece, Transform parent)
        {
            var holder = new GameObject(piece.Id + " (" + look.Model.name + ")");
            holder.transform.SetParent(parent, false);
            GameObject model = Object.Instantiate(look.Model, holder.transform, false);
            foreach (Collider c in model.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            // Lot de variantes de Poly Haven (bouteilles, gobelets) : gardé entier ; niveaux de détail laissés à Unity.
            if (StyleLook.VisibleBounds(holder, out Bounds b))
            {
                float size = piece.Fit > 0 ? Mathf.Max(b.size.x, b.size.z) : b.size.y;
                float want = (float)(piece.Fit > 0 ? piece.Fit : piece.Height);
                float k = size > 1e-4f && want > 0f ? want / size : 1f;
                holder.transform.localScale = Vector3.one * k * (look.Scale > 0f ? look.Scale : 1f);
            }
            holder.transform.rotation = Quaternion.Euler(0f, (float)piece.Yaw + look.Yaw, 0f);
            return holder;
        }

        /// <summary>Forme simple d'un meuble sans modèle (table, banc, tabouret, lit, coffre, caisse, tonneau, étagère, bougeoir, lanterne, porte).</summary>
        private GameObject Fallback(InteriorPiece piece, Transform parent)
        {
            Material wood = Built(StyleSurface.Wood, new Color(0.45f, 0.32f, 0.2f), out float tile);
            Material dark = Tinted(wood, new Color(0.6f, 0.5f, 0.42f), "bois des meubles");
            float fit = (float)piece.Fit, h = (float)piece.Height;
            var holder = new GameObject(piece.Id + " (forme simple)");
            holder.transform.SetParent(parent, false);
            void Add(Vector3 center, Vector3 size, Material m)
            {
                GameObject part = Box(piece.Id, center, size, m, tile);
                part.transform.SetParent(holder.transform, false);
            }
            switch (piece.Prop)
            {
                case InteriorProp.Table:
                    Add(new Vector3(0f, 0.76f, 0f), new Vector3(fit, 0.06f, Mathf.Min(0.9f, fit * 0.5f)), wood);
                    foreach ((float sx, float sz) in new[] { (1f, 1f), (1f, -1f), (-1f, 1f), (-1f, -1f) })
                        Add(new Vector3(sx * (fit / 2f - 0.1f), 0.37f, sz * (Mathf.Min(0.9f, fit * 0.5f) / 2f - 0.08f)), new Vector3(0.08f, 0.74f, 0.08f), dark);
                    break;
                case InteriorProp.Bench:
                    Add(new Vector3(0f, 0.44f, 0f), new Vector3(fit, 0.05f, 0.3f), wood);
                    Add(new Vector3(fit / 2f - 0.15f, 0.21f, 0f), new Vector3(0.06f, 0.42f, 0.26f), dark);
                    Add(new Vector3(-fit / 2f + 0.15f, 0.21f, 0f), new Vector3(0.06f, 0.42f, 0.26f), dark);
                    break;
                case InteriorProp.Stool:
                    Add(new Vector3(0f, 0.44f, 0f), new Vector3(fit, 0.05f, fit), wood);
                    Add(new Vector3(0f, 0.21f, 0f), new Vector3(fit * 0.25f, 0.42f, fit * 0.25f), dark);
                    break;
                case InteriorProp.Bed:
                    Add(new Vector3(0f, 0.25f, 0f), new Vector3(fit * 0.68f, 0.5f, fit), dark);
                    Add(new Vector3(0f, 0.55f, 0.08f), new Vector3(fit * 0.64f, 0.12f, fit * 0.82f), Plain(new Color(0.55f, 0.18f, 0.14f)));
                    Add(new Vector3(0f, 0.6f, -fit * 0.4f), new Vector3(fit * 0.5f, 0.12f, 0.3f), Plain(new Color(0.86f, 0.83f, 0.76f)));
                    break;
                case InteriorProp.Chest:
                case InteriorProp.Crate:
                    Add(new Vector3(0f, fit * 0.3f, 0f), new Vector3(fit, fit * 0.6f, fit * 0.6f), piece.Prop == InteriorProp.Chest ? dark : wood);
                    break;
                case InteriorProp.Barrel:
                {
                    // Sous-lot V2.12.7 (essai du 9 novembre 2026 : tonneau bleu uni) : fût de bois à cercles sombres (BarrelLook), plus un cylindre uni.
                    GameObject barrel = BarrelLook.Make(holder.transform, piece.Id, Vector3.zero, 0f);
                    barrel.transform.localPosition = Vector3.zero;
                    barrel.transform.localRotation = Quaternion.identity;
                    // Même encombrement que l'ancien cylindre : diamètre fit, hauteur 1,2 × fit (BarrelLook.Make : 0,7 m de large, 0,95 m de haut).
                    barrel.transform.localScale = new Vector3(fit / 0.7f, 1.2f * fit / 0.95f, fit / 0.7f);
                    break;
                }
                case InteriorProp.Shelves:
                case InteriorProp.Drawers:
                case InteriorProp.Cabinet:
                case InteriorProp.Commode:
                {
                    float tall = piece.Prop == InteriorProp.Commode ? 0.9f : 1.9f;
                    Add(new Vector3(0f, tall / 2f, 0f), new Vector3(fit, tall, 0.4f), dark);
                    for (float y = 0.4f; y < tall; y += 0.45f) Add(new Vector3(0f, y, 0.2f), new Vector3(fit - 0.06f, 0.03f, 0.02f), wood);
                    break;
                }
                case InteriorProp.Candlestick:
                    Add(new Vector3(0f, 0.02f, 0f), new Vector3(0.09f, 0.04f, 0.09f), Plain(new Color(0.42f, 0.3f, 0.12f)));
                    Add(new Vector3(0f, 0.04f + (h - 0.04f) / 2f, 0f), new Vector3(0.025f, h - 0.04f, 0.025f), Plain(new Color(0.42f, 0.3f, 0.12f)));
                    break;
                case InteriorProp.Lantern:
                case InteriorProp.HangingLantern:
                    Add(new Vector3(0f, h * 0.45f, 0f), new Vector3(h * 0.45f, h * 0.8f, h * 0.45f), Plain(new Color(0.08f, 0.075f, 0.07f)));
                    break;
                case InteriorProp.Chandelier:
                {
                    // Couronne de fer et sa chaîne (sans modèle).
                    Material iron = Plain(new Color(0.08f, 0.075f, 0.07f));
                    Add(new Vector3(0f, h * 0.15f, 0f), new Vector3(h * 0.8f, 0.05f, h * 0.8f), iron);
                    Add(new Vector3(0f, h * 0.6f, 0f), new Vector3(0.03f, h * 0.9f, 0.03f), iron);
                    break;
                }
                case InteriorProp.Door:
                    Add(new Vector3(0f, h / 2f, 0f), new Vector3(DoorWidth - 0.1f, h, 0.08f), dark);
                    break;
                default:
                    Object.Destroy(holder);
                    return null;
            }
            holder.transform.rotation = Quaternion.Euler(0f, (float)piece.Yaw, 0f);
            return holder;
        }

        /// <summary>Flamme au sommet d'un bougeoir (et sa bougie de cire, pour la forme simple qui n'en a pas).</summary>
        private void Candle(GameObject holder, bool wax)
        {
            if (!StyleLook.VisibleBounds(holder, out Bounds b)) return;
            var top = new Vector3(b.center.x, b.max.y, b.center.z);
            if (wax)
            {
                GameObject candle = Block("Bougie", top + Vector3.up * 0.06f, new Vector3(0.04f, 0.12f, 0.04f), Plain(new Color(0.93f, 0.9f, 0.8f)));
                candle.transform.SetParent(holder.transform, true);
                top += Vector3.up * 0.12f;
            }
            GameObject flame = Block("Flamme", top + Vector3.up * 0.03f, new Vector3(0.022f, 0.05f, 0.022f), Glow(new Color(1f, 0.72f, 0.3f), 6f));
            flame.transform.SetParent(holder.transform, true);
        }

        /// <summary>Lueur au cœur d'une lanterne.</summary>
        private void Lit(GameObject holder, float share)
        {
            if (!StyleLook.VisibleBounds(holder, out Bounds b)) return;
            GameObject glow = Block("Lueur", b.center, b.size * share, Glow(new Color(1f, 0.68f, 0.32f), 4f));
            glow.transform.SetParent(holder.transform, true);
        }

        // --- Lumières --------------------------------------------------------------------------------------------------------

        /// <summary>Lumières de l'aménagement : feu (ombres sur ordinateur), bougies, lanternes ; elles vacillent (<see cref="Flicker"/>).</summary>
        private void Lights(InteriorLayout layout)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            float power = catalog != null ? catalog.IndoorLights : 1f;
            int index = 0;
            foreach (InteriorLight l in layout.Lights)
            {
                var go = new GameObject("Lumière " + l.Kind);
                go.transform.SetParent(_root.transform, false);
                go.transform.position = new Vector3((float)l.X, (float)l.Y, (float)l.Z);
                Light light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = (float)l.Range;
                light.color = l.Kind == InteriorLightKind.Fire ? new Color(1f, 0.52f, 0.22f) : l.Kind == InteriorLightKind.Candle ? new Color(1f, 0.7f, 0.4f)
                    : l.Kind == InteriorLightKind.Lantern ? new Color(1f, 0.74f, 0.45f) : new Color(0.85f, 0.9f, 1f);
                float strength = (float)l.Intensity * power;
                light.intensity = strength;
                light.shadows = l.Kind == InteriorLightKind.Fire && !Application.isMobilePlatform ? LightShadows.Soft : LightShadows.None;
                if (l.Kind != InteriorLightKind.Window)
                    _flickers.Add((light, strength, 13.7f * index + 3.1f, l.Kind == InteriorLightKind.Fire ? 5.5f : 3f));
                index++;
            }
        }

        /// <summary>Le feu, les bougies et les lanternes vacillent doucement (le feu davantage).</summary>
        private void Flicker()
        {
            float now = Time.time;
            foreach ((Light light, float strength, float seed, float speed) in _flickers)
            {
                if (light == null) continue;
                float wave = Mathf.PerlinNoise(seed, now * speed);
                float amount = speed > 4f ? 0.35f : 0.15f;
                light.intensity = strength * (1f - amount / 2f + amount * wave);
            }
        }
    }
}
