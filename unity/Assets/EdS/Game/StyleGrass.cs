using System.Collections.Generic;
using EdS.Runtime;
using UnityEngine;
using UnityEngine.Rendering;

namespace EdS.Game
{
    /// <summary>
    /// Essai réaliste 4 (demande de Damien du 27 octobre 2026 : « le maximum de réalisme ») : herbe en brins sur le sol de l'aire. Une
    /// touffe de brins faite par le code est dessinée des milliers de fois par la carte graphique (shader « EdS/Herbe » du catalogue :
    /// couleurs du pied à la pointe, vent), rangée par cases de <see cref="CellSize"/> mètres ; seules les cases proches de la caméra
    /// sont dessinées. Places fixes (<see cref="StyleRules.GrassSpots"/>), hors des obstacles et de l'eau. Le moteur ne la connaît pas.
    /// </summary>
    public sealed class StyleGrass
    {
        private const float CellSize = 8f;

        private readonly Mesh _mesh;
        private readonly Material _material;
        private readonly List<Cell> _cells = new List<Cell>();
        private readonly float _distance;

        private sealed class Cell
        {
            public Vector3 Center;
            public Matrix4x4[] Items;
            public Bounds Bounds;
        }

        private StyleGrass(Mesh mesh, Material material, float distance)
        {
            _mesh = mesh;
            _material = material;
            _distance = distance;
        }

        /// <summary>
        /// Herbe sur [minX, maxX] × [minZ, maxZ], là où <paramref name="allowed"/> le permet ; null sans matériau d'herbe dans le catalogue
        /// ou si son shader ne se compile pas.
        /// </summary>
        public static StyleGrass Create(float minX, float minZ, float maxX, float maxZ, System.Func<float, float, bool> allowed)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            if (catalog == null || catalog.Grass == null || catalog.Grass.shader == null || !catalog.Grass.shader.isSupported || catalog.GrassDensity <= 0f)
                return null;
            bool phone = Application.isMobilePlatform;
            var material = new Material(catalog.Grass) { enableInstancing = true };
            var grass = new StyleGrass(Clump(phone ? 6 : 11), material, phone ? catalog.GrassDistance / 2f : catalog.GrassDistance);
            float density = phone ? catalog.GrassDensity / 5f : catalog.GrassDensity;
            var byCell = new Dictionary<(int, int), List<Matrix4x4>>();
            foreach (StyleRules.GrassSpot spot in StyleRules.GrassSpots(minX, minZ, maxX, maxZ, density))
            {
                float x = (float)spot.X, z = (float)spot.Z;
                if (!allowed(x, z)) continue;
                var key = (Mathf.FloorToInt(x / CellSize), Mathf.FloorToInt(z / CellSize));
                if (!byCell.TryGetValue(key, out List<Matrix4x4> items)) byCell[key] = items = new List<Matrix4x4>();
                items.Add(Matrix4x4.TRS(new Vector3(x, 0f, z), Quaternion.Euler(0f, (float)spot.Yaw, 0f), Vector3.one * (float)spot.Scale));
            }
            foreach (KeyValuePair<(int, int), List<Matrix4x4>> cell in byCell)
            {
                var center = new Vector3((cell.Key.Item1 + 0.5f) * CellSize, 0f, (cell.Key.Item2 + 0.5f) * CellSize);
                // Par paquets de 1 023 au plus (limite d'un dessin répété).
                for (int start = 0; start < cell.Value.Count; start += 1023)
                {
                    int count = Mathf.Min(1023, cell.Value.Count - start);
                    grass._cells.Add(new Cell
                    {
                        Center = center, Items = cell.Value.GetRange(start, count).ToArray(),
                        Bounds = new Bounds(center + Vector3.up * 0.5f, new Vector3(CellSize + 2f, 2f, CellSize + 2f)),
                    });
                }
            }
            return grass;
        }

        /// <summary>Dessine les cases proches de <paramref name="camera"/> (à appeler à chaque image).</summary>
        public void Draw(Vector3 camera)
        {
            float max = (_distance + CellSize) * (_distance + CellSize);
            foreach (Cell cell in _cells)
            {
                float dx = cell.Center.x - camera.x, dz = cell.Center.z - camera.z;
                if (dx * dx + dz * dz > max) continue;
                var parameters = new RenderParams(_material) { worldBounds = cell.Bounds, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = true };
                Graphics.RenderMeshInstanced(parameters, _mesh, 0, cell.Items);
            }
        }

        public void Destroy()
        {
            Object.Destroy(_mesh);
            Object.Destroy(_material);
        }

        /// <summary>
        /// Touffe de <paramref name="blades"/> brins (25 à 50 cm, courbés, effilés jusqu'à la pointe), sur un disque de 15 cm ; coordonnées
        /// de texture : x le brin, y la hauteur sur le brin (le shader en tire couleurs et vent). Toujours la même touffe.
        /// </summary>
        private static Mesh Clump(int blades)
        {
            const int Segments = 4;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            uint seed = 0x9E3779B9;
            float Next()
            {
                seed ^= seed << 13;
                seed ^= seed >> 17;
                seed ^= seed << 5;
                return (seed >> 8) / 16777216f;
            }
            for (int b = 0; b < blades; b++)
            {
                float angle = Next() * 2f * Mathf.PI, radius = Mathf.Sqrt(Next()) * 0.15f;
                var root = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                float facing = Next() * 2f * Mathf.PI;
                var side = new Vector3(Mathf.Cos(facing), 0f, Mathf.Sin(facing));
                var forward = new Vector3(-side.z, 0f, side.x);
                float height = 0.25f + 0.25f * Next(), width = 0.018f + 0.012f * Next(), lean = 0.08f + 0.18f * Next();
                int first = vertices.Count;
                for (int i = 0; i <= Segments; i++)
                {
                    float t = i / (float)Segments;
                    // Le brin se courbe vers l'avant en montant, et s'effile.
                    Vector3 spine = root + Vector3.up * (height * t) + forward * (lean * t * t * height);
                    float w = width * (1f - t * 0.9f);
                    var normal = Vector3.Normalize(forward * -1f + Vector3.up * (0.4f + t));
                    if (i < Segments)
                    {
                        vertices.Add(spine - side * w);
                        vertices.Add(spine + side * w);
                        normals.Add(normal);
                        normals.Add(normal);
                        uvs.Add(new Vector2(b / (float)blades, t));
                        uvs.Add(new Vector2(b / (float)blades, t));
                    }
                    else
                    {
                        vertices.Add(spine);
                        normals.Add(normal);
                        uvs.Add(new Vector2(b / (float)blades, 1f));
                    }
                }
                for (int i = 0; i < Segments - 1; i++)
                {
                    int a = first + 2 * i;
                    triangles.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
                }
                int last = first + 2 * (Segments - 1);
                triangles.AddRange(new[] { last, last + 2, last + 1 });
            }
            var mesh = new Mesh { name = "Touffe d'herbe" };
            mesh.vertices = vertices.ToArray();
            mesh.normals = normals.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
