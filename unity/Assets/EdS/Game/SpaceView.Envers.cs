using System.Collections.Generic;
using System.Linq;
using EdS.Data.Zones;
using EdS.Domain.Space;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Chantier V2.51b : l'Envers des Vallons. Le même décor que la surface (cases en reflet : mêmes obstacles, mêmes routes, même terrain),
    /// habillé par <see cref="EnversLook"/> : chaque matériau du décor a sa variante sombre (copie mise en cache, une par matériau d'origine,
    /// pas une par objet), les retouches de l'Envers (arbres morts, ruines inversées, éclats noirs, autel de l'Aube et sa lueur chaude), des
    /// formes flottantes dans le ciel (rochers, débris, arches brisées : sans ombre, occulteurs en fondu, nombre selon la machine), des cendres
    /// qui montent autour de la caméra, la lumière, la brume et le ciel de l'Envers (<see cref="StyleWorld.SetEnvers"/>). Les failles (portes
    /// de genre faille) sont dessinées des deux côtés. Modèles pris dans <c>Resources/Envers/&lt;nom&gt;</c>, formes simples en repli ;
    /// pose : <see cref="FitSewerModel"/> (bornes réelles, échelle avant le lacet). Placements calculés par des fonctions pures testées.
    /// </summary>
    public sealed partial class SpaceView
    {
        private bool _envers;
        private readonly Dictionary<Material, Material> _darkMaterials = new Dictionary<Material, Material>();
        private readonly List<(Transform Body, FloatingRock Rock, float Ground)> _floating = new List<(Transform, FloatingRock, float)>();
        private readonly List<Transform> _ash = new List<Transform>();
        private Material _ashMaterial;
        private Material _riftMaterial;

        /// <summary>
        /// Relecture indépendante des PR #260 et #261 : racines posées qui gardent leur clarté (l'autel de l'Aube, les brasiers, les failles et
        /// leur lame « EdS/Faille ») : leurs renderers, même enfants d'un modèle, ne sont jamais assombris.
        /// </summary>
        private readonly HashSet<Transform> _keepBright = new HashSet<Transform>();

        /// <summary>Relecture des PR #260 et #261 : formes flottantes et semis construits par case autour de la caméra (clé : rangée × 64 + colonne).</summary>
        private readonly Dictionary<int, GameObject> _enversCells = new Dictionary<int, GameObject>();
        private RegionGrid _enversRegion;
        private float _enversMinX, _enversMinZ, _enversMaxX, _enversMaxZ;

        /// <summary>Marque comme gardant leur clarté les enfants du décor posés depuis le rang <paramref name="first"/>.</summary>
        private void KeepBrightFrom(int first)
        {
            for (int i = first; i < _root.transform.childCount; i++) _keepBright.Add(_root.transform.GetChild(i));
        }

        private bool KeptBright(Transform t)
        {
            for (Transform at = t; at != null && at != _root.transform; at = at.parent)
                if (_keepBright.Contains(at)) return true;
            return false;
        }

        /// <summary>Dessine l'obstacle <paramref name="o"/> s'il est une retouche de l'Envers ; faux sinon. L'autel et les brasiers gardent leur clarté.</summary>
        private bool EnversObstacle(SpaceObstacle o)
        {
            int first = _root.transform.childCount;
            bool drawn = EnversObstacleDrawn(o);
            if (drawn && EnversLook.Glows(EnversLook.PieceFor(o.Id))) KeepBrightFrom(first);
            return drawn;
        }

        private bool EnversObstacleDrawn(SpaceObstacle o)
        {
            EnversPiece piece = EnversLook.PieceFor(o.Id);
            if (piece == EnversPiece.None) return false;
            var points = o.Points.Select(p => new Vector3(Raw(p.X.Raw), 0f, Raw(p.Z.Raw))).ToList();
            float x0 = points.Min(p => p.x), x1 = points.Max(p => p.x), z0 = points.Min(p => p.z), z1 = points.Max(p => p.z);
            float cx = (x0 + x1) / 2f, cz = (z0 + z1) / 2f, height = (float)EnversLook.HeightOf(piece);
            bool occludes = EnversLook.Occludes(piece);
            if (EnversLook.Glows(piece))
            {
                var glow = piece == EnversPiece.DawnAltar ? EnversLook.AltarGlow : EnversLook.RiftGlow;
                CaveGlow(o.Id, cx, cz, new Color((float)glow.R, (float)glow.G, (float)glow.B), (float)EnversLook.AltarGlowIntensity, (float)EnversLook.AltarGlowRange);
            }
            GameObject model = CryptModel(EnversLook.Folder, EnversLook.ModelFor(piece));
            if (model != null && EnversLook.Tiled(piece) && Footprint(model, out float modelLong, out float modelShort))
            {
                // Ruines et maisons effondrées en tuiles bout à bout, d'après les bornes réelles du modèle (sans trou).
                int n = 0;
                foreach (var t in SewerLooks.WallTiles(x0, z0, x1, z1, modelLong, modelShort))
                    FitSewerModel(model, o.Id + " (tuile " + (++n) + ")", (float)t.MinX, (float)t.MinZ, (float)t.MaxX, (float)t.MaxZ, height, occludes, 0f, 0f, true);
                return true;
            }
            if (FitSewerModel(model, o.Id, x0, z0, x1, z1, height, occludes)) return true;
            // Formes de remplacement (kit préparé, pas encore généré).
            switch (piece)
            {
                case EnversPiece.DeadTree:
                    Prism(o.Id, points, height * 0.6f, Plain(new Color(0.24f, 0.22f, 0.24f)), true, 1f);
                    // Les racines tournées vers le ciel : une couronne plus large au sommet.
                    Box(o.Id + " (racines)", new Vector3(cx, height * 0.75f, cz), new Vector3((x1 - x0) * 2.6f, height * 0.3f, (z1 - z0) * 2.6f), Plain(new Color(0.2f, 0.18f, 0.2f)), 1f);
                    break;
                case EnversPiece.InvertedRuin:
                    Prism(o.Id, points, height, Plain(new Color(0.34f, 0.32f, 0.36f)), true, 2f);
                    break;
                case EnversPiece.BlackShard:
                    Prism(o.Id, points, height, Plain(new Color(0.08f, 0.06f, 0.12f)), false, 1f);
                    break;
                case EnversPiece.InvertedStatue:
                case EnversPiece.SplitStone:
                case EnversPiece.CollapsedHouse:
                    Prism(o.Id, points, height, Plain(new Color(0.3f, 0.28f, 0.32f)), true, 2f);
                    break;
                case EnversPiece.VioletBrazier:
                    Prism(o.Id, points, height, Plain(new Color(0.16f, 0.14f, 0.16f)), false, 1f);
                    break;
                default:   // l'autel de l'Aube
                    Prism(o.Id, points, height, Plain(new Color(0.86f, 0.78f, 0.6f)), false, 1f);
                    break;
            }
            return true;
        }

        /// <summary>
        /// La faille sur chaque porte de genre faille de la vue, à la surface comme dans l'Envers : le modèle (bornes réelles, debout sur la
        /// porte), sinon une lame de lumière violette ; une lueur violette dans les deux cas.
        /// </summary>
        private void RiftDoors()
        {
            foreach (MapDoor door in _doors)
            {
                if (door.Kind != DoorKind.Rift) continue;
                int first = _root.transform.childCount;
                RiftDoor(door);
                KeepBrightFrom(first);   // la faille garde sa lumière (jamais assombrie)
            }
        }

        private void RiftDoor(MapDoor door)
        {
            {
                float x = (float)door.X, z = (float)door.Z, half = (float)EnversLook.RiftWidth / 2f, height = (float)EnversLook.RiftHeight;
                var glow = EnversLook.RiftGlow;
                CaveGlow(door.Id, x, z, new Color((float)glow.R, (float)glow.G, (float)glow.B), (float)EnversLook.RiftGlowIntensity, (float)EnversLook.RiftGlowRange);
                float ground = Ground(x, z, 0.4f);
                if (FitSewerModel(CryptModel(EnversLook.Folder, EnversLook.RiftModel), door.Id, x - half, z - 0.5f, x + half, z + 0.5f, height, false, ground)) return;
                if (_riftMaterial == null)
                {
                    Shader shader = Shader.Find("EdS/Faille");
                    _riftMaterial = shader != null ? new Material(shader) : ArenaView.MakeMaterial(new Color((float)glow.R, (float)glow.G, (float)glow.B));
                }
                GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Quad);
                blade.name = door.Id + " (faille)";
                Object.Destroy(blade.GetComponent<Collider>());
                blade.transform.SetParent(_root.transform, false);
                blade.transform.position = new Vector3(x, ground + height / 2f, z);
                blade.transform.localScale = new Vector3(half * 2f, height, 1f);
                Renderer renderer = blade.GetComponent<Renderer>();
                renderer.sharedMaterial = _riftMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        /// <summary>
        /// Le décor propre à l'Envers, après le décor de la surface : formes flottantes des cases de la vue, variantes sombres de tous les
        /// matériaux posés jusque-là, puis les cendres (non assombries) ; la lumière et le ciel de l'Envers.
        /// </summary>
        private void EnversDecor(CombatSpace space, AdventureMap map)
        {
            StyleWorld.SetEnvers(true);
            DeviceTier tier = DeviceQuality.Tier;
            // Relecture des PR #260 et #261 : les formes flottantes et le semis ne sont plus construits sur toute l'aire (768 cases) mais par
            // case autour de la caméra, au fil des images (EnversFrame, EnversLook.CellsNear).
            _enversRegion = map.Region;
            EnversLieuxSecrets(map);   // V2.52b : coffres et stèles, dessinés par case
            _enversMinX = Raw(space.Min.X.Raw);
            _enversMinZ = Raw(space.Min.Z.Raw);
            _enversMaxX = Raw(space.Max.X.Raw);
            _enversMaxZ = Raw(space.Max.Z.Raw);
            DarkenDecor();
            _ashMaterial = Plain(new Color(0.55f, 0.48f, 0.66f));
            for (int i = 0; i < EnversLook.MaxAsh(tier); i++)
            {
                GameObject ash = GameObject.CreatePrimitive(PrimitiveType.Quad);
                ash.name = "Cendre " + i;
                Object.Destroy(ash.GetComponent<Collider>());
                ash.transform.SetParent(_root.transform, false);
                ash.transform.localScale = Vector3.one * 0.08f;
                Renderer renderer = ash.GetComponent<Renderer>();
                renderer.sharedMaterial = _ashMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                ash.SetActive(false);
                _ash.Add(ash.transform);
            }
        }

        /// <summary>Une forme flottante : le modèle, à la taille voulue d'après ses bornes réelles, sinon un rocher aplati ; sans ombre.</summary>
        private void Floating(FloatingRock rock, Transform parent)
        {
            GameObject model = CryptModel(EnversLook.Folder, EnversLook.FloatingModels[rock.Model]);
            GameObject body;
            float size = (float)rock.Size;
            if (model != null)
            {
                body = new GameObject("Envers flottant (" + model.name + ")");
                GameObject copy = Object.Instantiate(model, body.transform, false);
                foreach (Collider c in body.GetComponentsInChildren<Collider>()) Object.Destroy(c);
                float big = StyleLook.VisibleBounds(copy, out Bounds b) ? Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)) : 0f;
                // Échelle uniforme posée avant le lacet (sur le corps, le lacet sur le parent).
                copy.transform.localScale = big > 0.01f ? Vector3.one * (size / big) : Vector3.one;
            }
            else
            {
                body = new GameObject("Envers flottant (rocher)");
                GameObject stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.Destroy(stone.GetComponent<Collider>());
                stone.transform.SetParent(body.transform, false);
                stone.transform.localScale = new Vector3(size, size * 0.55f, size * 0.8f);
                stone.GetComponent<Renderer>().sharedMaterial = Plain(new Color(0.22f, 0.2f, 0.24f));
            }
            if (rock.Model == 0 && DeviceQuality.Tier != DeviceTier.Phone)
            {
                // Racines suspendues (fiche E14, échouée chez Tripo : toujours par le code) : trois lames sombres sous le rocher.
                for (int k = 0; k < 3; k++)
                {
                    GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    root.name = "Racines suspendues " + k;
                    Object.Destroy(root.GetComponent<Collider>());
                    root.transform.SetParent(body.transform, false);
                    float a = k * 2.1f;
                    root.transform.localPosition = new Vector3(Mathf.Cos(a) * size * 0.2f, -size * 0.45f, Mathf.Sin(a) * size * 0.2f);
                    root.transform.localScale = new Vector3(0.12f, size * 0.5f, 0.12f);
                    root.GetComponent<Renderer>().sharedMaterial = Plain(new Color(0.18f, 0.15f, 0.16f));
                }
            }
            body.transform.SetParent(parent, false);
            body.transform.rotation = Quaternion.Euler(0f, (float)rock.Yaw, 0f);
            // Revue Codex de la PR #261 (2e tour) : au-dessus du relief affiché (collines, col), le plus haut sous son emprise.
            float ground = (float)EnversLook.FloatingGround(rock, (x, z) => StyleWorld.StandHeight((float)x, (float)z));
            body.transform.position = new Vector3((float)rock.X, (float)EnversLook.FloatingAltitude(rock, ground, 0), (float)rock.Z);
            foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            float half = size / 2f;
            var span = EnversLook.FloatingBounds(rock, ground);   // toute l'oscillation, au-dessus du sol local
            AddOccluder(body.transform, false, new Vector3((float)rock.X - half, (float)span.Min, (float)rock.Z - half), new Vector3((float)rock.X + half, (float)span.Max, (float)rock.Z + half));
            _floating.Add((body.transform, rock, ground));
        }

        /// <summary>
        /// Une place du semis au sol : souche de cendre ou champignons lumineux (modèles), herbe morte (fiche E08, échouée chez Tripo :
        /// toujours par le code, deux lames croisées) ; sans ombre ni collision, posée sur le sol affiché.
        /// </summary>
        private void Scattered(double x, double z, int kind, double scale, double yaw, Transform parent)
        {
            float k = (float)scale;
            float ground = Ground((float)x, (float)z, 0.3f);
            GameObject model = kind < 2 ? CryptModel(EnversLook.Folder, EnversLook.ScatterModels[kind]) : null;
            GameObject body;
            if (model != null)
            {
                body = new GameObject("Envers semis (" + model.name + ")");
                GameObject copy = Object.Instantiate(model, body.transform, false);
                foreach (Collider c in body.GetComponentsInChildren<Collider>()) Object.Destroy(c);
                copy.transform.localScale = Vector3.one * k;   // le modèle est déjà à la taille de sa fiche (finition) : l'échelle avant le lacet
            }
            else
            {
                body = new GameObject("Envers semis (herbe morte)");
                Material grass = Plain(kind == 0 ? new Color(0.2f, 0.18f, 0.18f) : kind == 1 ? new Color(0.4f, 0.5f, 0.7f) : new Color(0.42f, 0.4f, 0.42f));
                for (int i = 0; i < 2; i++)
                {
                    GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Object.Destroy(blade.GetComponent<Collider>());
                    blade.transform.SetParent(body.transform, false);
                    blade.transform.localPosition = new Vector3(0f, 0.3f * k, 0f);
                    blade.transform.localRotation = Quaternion.Euler(0f, i * 90f, 0f);
                    blade.transform.localScale = new Vector3(0.9f * k, 0.6f * k, 1f);
                    blade.GetComponent<Renderer>().sharedMaterial = grass;
                }
            }
            body.transform.SetParent(parent, false);
            body.transform.rotation = Quaternion.Euler(0f, (float)yaw, 0f);
            body.transform.position = new Vector3((float)x, ground, (float)z);
            foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        /// <summary>Remplace chaque matériau du décor par sa variante sombre (une copie par matériau d'origine) ; l'autel de l'Aube garde sa clarté.</summary>
        private void DarkenDecor()
        {
            DarkenUnder(_root);
        }

        /// <summary>Assombrit les renderers sous <paramref name="root"/>, sauf ceux des racines qui gardent leur clarté (en remontant les parents).</summary>
        private void DarkenUnder(GameObject root)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (KeptBright(renderer.transform)) continue;
                Material[] shared = renderer.sharedMaterials;
                if (shared == null) continue;
                bool changed = false;
                for (int i = 0; i < shared.Length; i++)
                {
                    Material dark = Dark(shared[i]);
                    if (dark != shared[i]) { shared[i] = dark; changed = true; }
                }
                if (changed) renderer.sharedMaterials = shared;
            }
        }

        private Material Dark(Material source)
        {
            if (source == null) return null;
            if (_darkMaterials.TryGetValue(source, out Material dark)) return dark;
            dark = new Material(source);
            foreach (string property in new[] { "_BaseColor", "_Color" })
                if (dark.HasProperty(property))
                {
                    Color c = dark.GetColor(property);
                    var d = EnversLook.Darken(c.r, c.g, c.b);
                    dark.SetColor(property, new Color((float)d.R, (float)d.G, (float)d.B, c.a));
                }
            _darkMaterials[source] = dark;
            return dark;
        }

        /// <summary>
        /// V2.51b : à chaque image dans l'Envers : les formes flottantes oscillent lentement, les cendres montent autour de la caméra
        /// (<paramref name="density"/> de 0 à 1,5 : part des cendres montrées). Ailleurs : rien.
        /// </summary>
        public void EnversFrame(Vector3 camera, float seconds, float density)
        {
            if (!_envers) return;
            StreamCells(camera);
            StreamLieux(camera);   // V2.52b : pièces des douze lieux et secrets, par case
            _floating.RemoveAll(f => f.Body == null);
            foreach ((Transform body, FloatingRock rock, float floor) in _floating)
                if (body != null) body.position = new Vector3((float)rock.X, (float)EnversLook.FloatingAltitude(rock, floor, seconds), (float)rock.Z);
            int shown = Mathf.Clamp(Mathf.RoundToInt(_ash.Count * density / 1.5f), 0, _ash.Count);
            float ground = StyleWorld.StandHeight(camera.x, camera.z);
            for (int i = 0; i < _ash.Count; i++)
            {
                Transform ash = _ash[i];
                bool show = i < shown;
                if (ash.gameObject.activeSelf != show) ash.gameObject.SetActive(show);
                if (!show) continue;
                var p = EnversLook.Ash(i, camera.x, camera.z, seconds);
                ash.position = new Vector3((float)p.X, ground + (float)p.Y, (float)p.Z);
                ash.LookAt(camera);
            }
        }

        /// <summary>
        /// Construit les cases proches de la caméra qui manquent (au plus <see cref="EnversLook.StreamCellsPerFrame"/> par image, la plus
        /// proche d'abord) et défait celles qui sont sorties du rayon plus la marge, avec leurs occulteurs.
        /// </summary>
        private void StreamCells(Vector3 camera)
        {
            if (_enversRegion == null) return;
            double radius = EnversLook.StreamRadius(DeviceQuality.Tier);
            var gone = new List<int>();
            foreach (KeyValuePair<int, GameObject> cell in _enversCells)
                if (!EnversLook.CellKept(_enversRegion, cell.Key % 64, cell.Key / 64, camera.x, camera.z, radius)) gone.Add(cell.Key);
            foreach (int key in gone)
            {
                GameObject cell = _enversCells[key];
                _enversCells.Remove(key);
                if (cell == null) continue;
                // Les occulteurs de ses formes flottantes partent avec elle (leurs copies en fondu sont rendues), avant la destruction.
                foreach (Occluder o in _occluders.Where(o => o.Transform != null && o.Transform.IsChildOf(cell.transform)).ToList())
                {
                    ReleaseFade(o);
                    _occluders.Remove(o);
                }
                Object.Destroy(cell);
            }
            int built = 0;
            foreach ((int column, int row) in EnversLook.CellsNear(_enversRegion, camera.x, camera.z, radius))
            {
                if (built >= EnversLook.StreamCellsPerFrame) break;
                int key = row * 64 + column;
                if (_enversCells.ContainsKey(key)) continue;
                _enversCells[key] = BuildCell(column, row);
                built++;
            }
        }

        /// <summary>Les formes flottantes et le semis d'une case (fonctions pures), sous une racine de case, assombris.</summary>
        private GameObject BuildCell(int column, int row)
        {
            var cell = new GameObject("Envers case c" + column + "_r" + row);
            cell.transform.SetParent(_root.transform, false);
            DeviceTier tier = DeviceQuality.Tier;
            foreach (FloatingRock rock in EnversLook.FloatingRocks(_enversRegion, column, row, tier)) Floating(rock, cell.transform);
            foreach (var spot in EnversLook.Scatter(_enversRegion, column, row, tier))
                if (spot.X >= _enversMinX && spot.X <= _enversMaxX && spot.Z >= _enversMinZ && spot.Z <= _enversMaxZ && !InObstacle((float)spot.X, (float)spot.Z, 1f)
                    && TownRules.RoadAt(spot.X, spot.Z, _roads).Edge > 0.5)
                    Scattered(spot.X, spot.Z, spot.Kind, spot.Scale, spot.Yaw, cell.transform);
            DarkenUnder(cell);
            return cell;
        }

        private void ReleaseEnvers()
        {
            _enversCells.Clear();
            ReleaseLieux();   // V2.52b
            _keepBright.Clear();
            _enversRegion = null;
            foreach (Material m in _darkMaterials.Values)
                if (m != null) Object.Destroy(m);
            _darkMaterials.Clear();
            if (_riftMaterial != null) Object.Destroy(_riftMaterial);
            _riftMaterial = null;
            _floating.Clear();
            _ash.Clear();
            if (_envers) StyleWorld.SetEnvers(false);
            _envers = false;
        }
    }
}
