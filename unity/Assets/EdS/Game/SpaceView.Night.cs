using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Space;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V2.1 (jour et nuit qui changent le monde, sujets 32 et 49) : le décor de la nuit, sans asset payant. Lanternes allumées à
    /// côté des portes, réverbères le long des rues pavées, fenêtres qui s'allument (bâtiments faits par le code), feux follets sur le lac,
    /// la rivière et les ruines, yeux de hiboux dans les arbres ; les villageois du décor rentrent chez eux (un veilleur sur quatre reste
    /// dehors). Tout s'allume au crépuscule et s'éteint à l'aube, en douceur (<see cref="StyleWorld.Lamps"/>). Pour rester léger, huit
    /// lumières seulement éclairent le sol : celles des lanternes les plus proches de la caméra ; les autres brillent sans éclairer.
    /// </summary>
    public sealed partial class SpaceView
    {
        /// <summary>Lumières qui éclairent vraiment (les lanternes les plus proches de la caméra).</summary>
        private const int LampLights = 8;

        private static readonly Color LanternColor = new Color(1f, 0.74f, 0.42f);
        private static readonly Color WindowDay = new Color(0.045f, 0.045f, 0.05f);
        private static readonly Color WindowNight = new Color(1f, 0.7f, 0.36f);
        private static readonly Color WispColor = new Color(0.55f, 0.92f, 1f);
        private static readonly Color OwlColor = new Color(1f, 0.82f, 0.25f);

        private sealed class Lamp
        {
            public NightLamp Data;
            public Transform Body;
            public Vector3 Glow;
        }

        private readonly List<NightLamp> _lampSpots = new List<NightLamp>();
        private readonly List<Lamp> _lanterns = new List<Lamp>();
        private readonly List<Lamp> _wisps = new List<Lamp>();
        private readonly List<Lamp> _owls = new List<Lamp>();
        private readonly List<Light> _lampLights = new List<Light>();
        private readonly List<Material> _nightMaterials = new List<Material>();
        private Material _windowGlass, _lanternGlow, _wispGlow, _owlGlow;
        private GameObject _nightRoot;
        private float _lampSort = 1f, _shownLamps = -1f, _shownNight = -1f;
        /// <summary>Villageois du décor rentrés : 1 la nuit du monde, 2 sous la pluie (sous-lot V2.2), 3 les deux.</summary>
        private int _villagersHome;

        /// <summary>Fond des fenêtres des bâtiments faits par le code : sombre le jour, chaud la nuit (un seul matériau pour toutes).</summary>
        private Material WindowGlass()
        {
            if (_windowGlass == null) _windowGlass = Owned(Emissive(WindowDay, Color.black));
            return _windowGlass;
        }

        private Material Owned(Material material)
        {
            _nightMaterials.Add(material);
            return material;
        }

        private static Material Emissive(Color color, Color emission)
        {
            Material material = ArenaView.MakeMaterial(color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.6f);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
            }
            return material;
        }

        /// <summary>
        /// Lanterne de porte d'un bâtiment (posée avec le reste du décor de nuit). V2.17 : un bâtiment de l'atelier (<paramref name="own"/>)
        /// porte sa lanterne dans son modèle : seule sa lumière est ajoutée : sur la lanterne du modèle si elle est connue (V2.18, <see cref="TownModels.OwnLantern"/>), sinon à côté de la porte (<see cref="TownModels.OwnDoorLantern"/>).
        /// </summary>
        private void DoorLantern(StyleBuilding kind, string id, float x0, float z0, float x1, float z1, (int X, int Z) facing, bool own = false)
        {
            if (!NightLamps.HasLantern(kind)) return;
            if (!own)
            {
                _lampSpots.Add(NightLamps.DoorLantern(id, x0, z0, x1, z1, facing));
                return;
            }
            MapDoor door = _doors.FirstOrDefault(d => d.Building == id);
            NightLamp light = TownModels.OwnDoorLantern(id, x0, z0, x1, z1, facing, door != null ? door.X : (double?)null, door != null ? door.Z : (double?)null);
            // V2.18 : la lumière va sur la lanterne du modèle quand elle est connue (TownModels.OwnLantern), posée comme le modèle.
            BuildingLook look = StyleCatalog.Loaded != null ? StyleCatalog.Loaded.OwnBuilding(id) : null;
            (double X, double Y, double Z)? local = look != null ? TownModels.OwnLantern(look.Model.name) : null;
            if (local.HasValue)
            {
                (double wx, double wy, double wz) = TownModels.OwnPlace((x0 + x1) / 2, (z0 + z1) / 2, facing, local.Value.X, local.Value.Y, local.Value.Z);
                light = new NightLamp(light.Id, light.Kind, wx, wy, wz, light.Yaw);
                // V2.21 : la lanterne de Sauge reste allumée jour et nuit (décision de Damien du 10 novembre 2026).
                if (TownModels.OwnLanternAlwaysLit(look.Model.name)) _alwaysLit.Add(light.Id);
            }
            _lampSpots.Add(light);
            _lightOnly.Add(light.Id);
        }

        /// <summary>Sous-lot V2.17 : lanternes dont seule la lumière est posée (la lanterne est dans le modèle de l'atelier).</summary>
        private readonly HashSet<string> _lightOnly = new HashSet<string>();

        /// <summary>V2.21 : lanternes toujours allumées (<see cref="TownModels.OwnLanternAlwaysLit"/>), et pour chacune des huit lumières, si elle est sur l'une d'elles.</summary>
        private readonly HashSet<string> _alwaysLit = new HashSet<string>();
        private readonly List<bool> _lightAlways = new List<bool>();

        /// <summary>Pose les lanternes, réverbères, feux follets et hiboux (dehors seulement), éteints : <see cref="Night"/> les allume.</summary>
        private void NightDecor(CombatSpace space, AdventureMap map)
        {
            _nightRoot = new GameObject("Nuit (V2.1)");
            _nightRoot.transform.SetParent(_root.transform, false);
            _lanternGlow = Owned(Emissive(new Color(0.25f, 0.2f, 0.12f), Color.black));
            _wispGlow = Owned(Emissive(WispColor, Color.black));
            _owlGlow = Owned(Emissive(new Color(0.1f, 0.08f, 0.02f), Color.black));
            Material iron = Owned(ArenaView.MakeMaterial(new Color(0.12f, 0.11f, 0.1f)));

            bool Free(double x, double z, double margin) =>
                !_shapes.Any(o => x > o.MinX - margin && x < o.MaxX + margin && z > o.MinZ - margin && z < o.MaxZ + margin);

            // Réverbères le long des rues pavées.
            foreach (StyleRoad road in _roads.Where(r => r.Paved))
                _lampSpots.AddRange(NightLamps.Street(road.Id, road.Points, road.Width, (x, z) => Free(x, z, 0.8)));
            foreach (NightLamp spot in _lampSpots)
            {
                var holder = new GameObject(spot.Id);
                holder.transform.SetParent(_nightRoot.transform, false);
                holder.transform.position = StyleWorld.OnGround(new Vector3((float)spot.X, 0f, (float)spot.Z));   // V2.24 : sur le sol affiché
                holder.transform.rotation = Quaternion.Euler(0f, (float)spot.Yaw, 0f);
                float top = (float)NightLamps.LanternTop(spot);
                // Revue Codex de la PR #99 : pièces placées en coordonnées locales au support (chaque lanterne à sa place, tournée avec lui).
                if (!_lightOnly.Contains(spot.Id))
                    foreach (LanternPiece p in NightLamps.LanternPieces(spot))
                        Piece(holder.transform, p.Name, p.Name == "poteau" ? PrimitiveType.Cylinder : PrimitiveType.Cube, new Vector3((float)p.X, (float)p.Y, (float)p.Z),
                            new Vector3((float)p.ScaleX, (float)p.ScaleY, (float)p.ScaleZ), p.Glass ? _lanternGlow : iron, true);
                _lanterns.Add(new Lamp { Data = spot, Body = holder.transform, Glow = holder.transform.position + new Vector3(0f, top, 0f) });
            }
            // Feux follets : sur l'eau et dans les ruines, là où les échos reviennent.
            if (map != null)
                foreach (MapPlace place in map.Places.Where(p => p.Id == "lac" || p.Id == "riviere" || p.Id == "ruines"))
                    // Sur l'eau, ils flottent au-dessus des obstacles de la rivière et du lac ; dans les ruines, hors des pierres.
                    foreach (NightLamp w in NightLamps.Wisps(place.Id, place.MinX, place.MinZ, place.MaxX, place.MaxZ, place.Id == "riviere" ? 3 : 5,
                                 (x, z) => place.Id != "ruines" || Free(x, z, 0.5)))
                    {
                        Transform body = Piece(_nightRoot.transform, w.Id, PrimitiveType.Sphere, StyleWorld.OnGround(new Vector3((float)w.X, (float)w.Y, (float)w.Z)),
                            new Vector3(0.16f, 0.16f, 0.16f), _wispGlow).transform;
                        _wisps.Add(new Lamp { Data = w, Body = body, Glow = body.position });
                    }
            // Hiboux : dans les arbres isolés (« arbre… »).
            foreach (SpaceObstacle tree in space.Obstacles.Where(o => o.Id.StartsWith("arbre", System.StringComparison.Ordinal)))
            {
                double cx = tree.Points.Average(p => p.X.Raw / 10000.0), cz = tree.Points.Average(p => p.Z.Raw / 10000.0);
                if (!(NightLamps.Owl(tree.Id, cx, cz) is NightLamp owl)) continue;
                var holder = new GameObject(owl.Id);
                holder.transform.SetParent(_nightRoot.transform, false);
                holder.transform.position = StyleWorld.OnGround(new Vector3((float)owl.X, (float)owl.Y, (float)owl.Z));   // V2.24 : l'arbre a monté
                holder.transform.rotation = Quaternion.Euler(0f, (float)owl.Yaw, 0f);
                foreach (float side in new[] { -0.07f, 0.07f })
                    Piece(holder.transform, "œil", PrimitiveType.Sphere, holder.transform.position + holder.transform.rotation * new Vector3(side, 0f, 0.45f),
                        new Vector3(0.06f, 0.06f, 0.06f), _owlGlow);
                _owls.Add(new Lamp { Data = owl, Body = holder.transform, Glow = holder.transform.position });
            }
            for (int i = 0; i < LampLights && i < _lanterns.Count; i++)
            {
                var light = new GameObject("Lumière de lanterne " + i).AddComponent<Light>();
                light.transform.SetParent(_nightRoot.transform, false);
                light.type = LightType.Point;
                light.color = LanternColor;
                light.range = 7f;
                light.intensity = 0f;
                light.shadows = LightShadows.None;
                _lampLights.Add(light);
                _lightAlways.Add(false);
            }
            SetNightVisible(0f);
        }

        /// <param name="local">Revue Codex de la PR #99 : <paramref name="position"/> est locale au support (lanternes) ; sinon dans le
        /// monde (feux follets, yeux de hiboux).</param>
        private static GameObject Piece(Transform parent, string name, PrimitiveType shape, Vector3 position, Vector3 scale, Material material, bool local = false)
        {
            GameObject piece = GameObject.CreatePrimitive(shape);
            piece.name = name;
            Object.Destroy(piece.GetComponent<Collider>());
            piece.transform.SetParent(parent, false);
            if (local)
            {
                piece.transform.localPosition = position;
                piece.transform.localRotation = Quaternion.identity;
            }
            else piece.transform.position = position;
            piece.transform.localScale = scale;
            Renderer renderer = piece.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return piece;
        }

        private void SetNightVisible(float night)
        {
            bool show = night > 0.35f;
            foreach (Lamp w in _wisps) if (w.Body != null) w.Body.gameObject.SetActive(show);
            foreach (Lamp o in _owls) if (o.Body != null) o.Body.gameObject.SetActive(show);
        }

        /// <summary>
        /// À chaque image dehors : lanternes et fenêtres d'après <see cref="StyleWorld.Lamps"/>, feux follets et hiboux d'après la part de nuit,
        /// les huit lumières sur les lanternes les plus proches de <paramref name="camera"/> ; les villageois du décor rentrent quand c'est
        /// la nuit du monde (<paramref name="worldNight"/>, de 22 h à 5 h) et ressortent à l'aube.
        /// </summary>
        public void Night(Vector3 camera, bool worldNight, float deltaTime)
        {
            SeuilGlow(StyleWorld.Lamps);   // V2.25.2 : fissures et éclats des pierres du Seuil
            if (_nightRoot == null) return;
            float lamps = StyleWorld.Lamps, night = StyleWorld.NightShare;
            StyleCatalog catalog = StyleCatalog.Loaded;
            float strength = catalog != null ? catalog.LanternIntensity : 1.6f;
            if (Mathf.Abs(lamps - _shownLamps) > 0.002f)
            {
                _shownLamps = lamps;
                SetGlow(_lanternGlow, Color.Lerp(new Color(0.25f, 0.2f, 0.12f), LanternColor, lamps), LanternColor * (lamps * 2.2f));
                if (_windowGlass != null) SetGlow(_windowGlass, Color.Lerp(WindowDay, WindowNight, lamps), WindowNight * (lamps * 1.3f));
            }
            if (Mathf.Abs(night - _shownNight) > 0.002f)
            {
                _shownNight = night;
                SetGlow(_wispGlow, WispColor, WispColor * (night * 2.5f));
                SetGlow(_owlGlow, Color.Lerp(new Color(0.1f, 0.08f, 0.02f), OwlColor, night), OwlColor * (night * 1.8f));
                SetNightVisible(night);
            }
            // Les feux follets flottent et dérivent ; les hiboux clignent des yeux de temps en temps.
            if (night > 0.35f)
            {
                float t = Time.time;
                foreach (Lamp w in _wisps)
                {
                    float seed = StyleRules.Variant(w.Data.Id, 1000) * 0.0063f;
                    w.Body.position = w.Glow + new Vector3(Mathf.Sin(t * 0.37f + seed) * 0.9f, Mathf.Sin(t * 1.3f + seed * 2f) * 0.25f, Mathf.Cos(t * 0.29f + seed) * 0.9f);
                }
                foreach (Lamp o in _owls)
                {
                    float seed = StyleRules.Variant(o.Data.Id, 1000) * 0.01f;
                    bool open = Mathf.Repeat(t + seed, 5f) > 0.18f;
                    if (o.Body.gameObject.activeSelf != open) o.Body.gameObject.SetActive(open);
                }
            }
            // Les huit lumières : sur les lanternes allumées les plus proches de la caméra (relues quatre fois par seconde). V2.22 (relecture
            // de la PR #140) : le jour et au crépuscule, les lanternes toujours allumées passent d'abord (TownModels.LampOrder), pour que
            // celle de Sauge garde sa lumière même quand huit autres lanternes, éteintes, sont plus proches.
            _lampSort += deltaTime;
            if (_lampSort >= 0.25f)
            {
                _lampSort = 0f;
                var distances = new List<double>(_lanterns.Count);
                var always = new List<bool>(_lanterns.Count);
                foreach (Lamp l in _lanterns)
                {
                    distances.Add((l.Glow - camera).sqrMagnitude);
                    always.Add(_alwaysLit.Contains(l.Data.Id));
                }
                var nearest = TownModels.LampOrder(distances, always, lamps, _lampLights.Count).Select(i => _lanterns[i]).ToList();
                for (int i = 0; i < _lampLights.Count; i++)
                {
                    Light light = _lampLights[i];
                    if (i < nearest.Count) light.transform.position = nearest[i].Glow + new Vector3(0f, -0.1f, 0f);
                    _lightAlways[i] = i < nearest.Count && _alwaysLit.Contains(nearest[i].Data.Id);
                    light.enabled = i < nearest.Count && (lamps > 0.01f || _lightAlways[i]);
                }
            }
            for (int i = 0; i < _lampLights.Count; i++)
            {
                Light light = _lampLights[i];
                float level = (float)TownModels.LanternLevel(_lightAlways[i], lamps);
                light.intensity = level * strength * (0.92f + 0.08f * Mathf.PerlinNoise(Time.time * 2.3f, light.transform.position.x));
            }
            // Villageois du décor : la nuit du monde, ils rentrent chez eux ; les veilleurs restent. Sous-lot V2.2 : sous une vraie pluie aussi
            // (un sur trois reste dehors).
            _worldNight = worldNight;
            int home = (worldNight ? 1 : 0) | (_sheltering ? 2 : 0);
            if (home != _villagersHome)
            {
                _villagersHome = home;
                foreach (Villager v in _villagers)
                    if (v.Body != null) v.Body.gameObject.SetActive(Shown(v));
            }
        }

        /// <summary>Nuit du monde montrée (les villageois du décor rentrent).</summary>
        private bool _worldNight;

        /// <summary>
        /// Un villageois du décor est dehors : pas la nuit (sauf les veilleurs), pas sous une vraie pluie (sauf un sur trois), et, sous-lot
        /// V2.3, pas rentré manger chez lui ; sous-lot V2.3.1, pas sorti par la porte (un PNJ d'une pièce parti dans une autre zone).
        /// </summary>
        /// <remarks>Revue Codex de la PR #109 : le corps d'un PNJ qui parle n'obéit qu'au cœur (<see cref="VillageDay.Shown"/>).</remarks>
        private bool Shown(Villager v) =>
            VillageDay.Shown(v.Speaker, v.Gone, _worldNight, NightLamps.AwakeAtNight(v.Data), _sheltering, WeatherCycle.StaysOutInRain(v.Data), v.Away);

        private static void SetGlow(Material material, Color color, Color emission)
        {
            if (material == null) return;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", emission);
        }

        /// <summary>Matériaux du décor de nuit, détruits avec le décor.</summary>
        private void ReleaseNight()
        {
            foreach (Material material in _nightMaterials)
                if (material != null) Object.Destroy(material);
            _nightMaterials.Clear();
        }
    }
}
