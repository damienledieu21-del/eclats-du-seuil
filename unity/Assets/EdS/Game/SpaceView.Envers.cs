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
        private readonly List<(Transform Body, FloatingRock Rock)> _floating = new List<(Transform, FloatingRock)>();
        private readonly List<Transform> _ash = new List<Transform>();
        private Material _ashMaterial;
        private Material _riftMaterial;

        /// <summary>Dessine l'obstacle <paramref name="o"/> s'il est une retouche de l'Envers ; faux sinon.</summary>
        private bool EnversObstacle(SpaceObstacle o)
        {
            EnversPiece piece = EnversLook.PieceFor(o.Id);
            if (piece == EnversPiece.None) return false;
            var points = o.Points.Select(p => new Vector3(Raw(p.X.Raw), 0f, Raw(p.Z.Raw))).ToList();
            float x0 = points.Min(p => p.x), x1 = points.Max(p => p.x), z0 = points.Min(p => p.z), z1 = points.Max(p => p.z);
            float cx = (x0 + x1) / 2f, cz = (z0 + z1) / 2f, height = (float)EnversLook.HeightOf(piece);
            bool occludes = EnversLook.Occludes(piece);
            if (piece == EnversPiece.DawnAltar)
            {
                var glow = EnversLook.AltarGlow;
                CaveGlow(o.Id, cx, cz, new Color((float)glow.R, (float)glow.G, (float)glow.B), (float)EnversLook.AltarGlowIntensity, (float)EnversLook.AltarGlowRange);
            }
            if (FitSewerModel(CryptModel(EnversLook.Folder, EnversLook.ModelFor(piece)), o.Id, x0, z0, x1, z1, height, occludes)) return true;
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
                float x = (float)door.X, z = (float)door.Z, half = (float)EnversLook.RiftWidth / 2f, height = (float)EnversLook.RiftHeight;
                var glow = EnversLook.RiftGlow;
                CaveGlow(door.Id, x, z, new Color((float)glow.R, (float)glow.G, (float)glow.B), (float)EnversLook.RiftGlowIntensity, (float)EnversLook.RiftGlowRange);
                float ground = Ground(x, z, 0.4f);
                if (FitSewerModel(CryptModel(EnversLook.Folder, EnversLook.RiftModel), door.Id, x - half, z - 0.5f, x + half, z + 0.5f, height, false, ground)) continue;
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
            if (map.Region is RegionGrid region)
            {
                double minX = Raw(space.Min.X.Raw), minZ = Raw(space.Min.Z.Raw), maxX = Raw(space.Max.X.Raw), maxZ = Raw(space.Max.Z.Raw);
                int c0 = Mathf.Max(0, (int)((minX - region.OriginX) / RegionGrid.CellMeters)), c1 = Mathf.Min(region.Columns - 1, (int)((maxX - region.OriginX) / RegionGrid.CellMeters));
                int r0 = Mathf.Max(0, (int)((minZ - region.OriginZ) / RegionGrid.CellMeters)), r1 = Mathf.Min(region.Rows - 1, (int)((maxZ - region.OriginZ) / RegionGrid.CellMeters));
                for (int r = r0; r <= r1; r++)
                    for (int c = c0; c <= c1; c++)
                        foreach (FloatingRock rock in EnversLook.FloatingRocks(region, c, r, tier))
                            Floating(rock);
            }
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
        private void Floating(FloatingRock rock)
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
            body.transform.SetParent(_root.transform, false);
            body.transform.rotation = Quaternion.Euler(0f, (float)rock.Yaw, 0f);
            body.transform.position = new Vector3((float)rock.X, (float)rock.Y, (float)rock.Z);
            foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            float half = size / 2f;
            AddOccluder(body.transform, false, body.transform.position - new Vector3(half, half, half), body.transform.position + new Vector3(half, half, half));
            _floating.Add((body.transform, rock));
        }

        /// <summary>Remplace chaque matériau du décor par sa variante sombre (une copie par matériau d'origine) ; l'autel de l'Aube garde sa clarté.</summary>
        private void DarkenDecor()
        {
            foreach (Renderer renderer in _root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.gameObject.name.StartsWith("envers_autel", System.StringComparison.Ordinal)) continue;
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
            foreach ((Transform body, FloatingRock rock) in _floating)
                if (body != null) body.position = new Vector3((float)rock.X, (float)EnversLook.BobbedHeight(rock, seconds), (float)rock.Z);
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

        private void ReleaseEnvers()
        {
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
