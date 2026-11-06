using System.Collections.Generic;
using System.Linq;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot 12.8 : repères d'une zone du monde, en formes simples (provisoire) : les PNJ (capsules dorées), les nœuds de récolte
    /// (sphère verte pour une herbe, cube gris pour un filon ; cachés tant qu'ils repoussent) et les points sûrs (disques bleus au sol). Leurs
    /// noms s'affichent au-dessus d'eux quand le joueur est assez près. Depuis l'essai du 12.9 : la forge (enclume sombre). Affichage seulement : portées et règles sont celles du cœur.
    /// <para>
    /// Essai réaliste (demandes de Damien du 27 octobre 2026 : « la forge est à faire, les minerais et l'herbe de l'Aube aussi », « Odile est
    /// tournée du mauvais côté ») : les PNJ se tournent vers le joueur quand il approche ; l'herbe de l'Aube est une pervenche de Poly Haven,
    /// un filon un rocher réaliste veiné de fer, la forge une enclume avec son marteau, un foyer de pierre qui rougeoie, un tonneau et une
    /// caisse. Sans catalogue, les formes simples restent.
    /// </para>
    /// </summary>
    public sealed class WorldMarkers
    {
        private const float NpcLabelRange = 15f, NodeLabelRange = 7f, SafeLabelRange = 12f;

        /// <summary>Vitesse (degrés par seconde) à laquelle un PNJ se tourne.</summary>
        private const float TurnSpeed = 240f;

        private sealed class Marker
        {
            public WorldMarker Data;
            public GameObject Body;
            public float Height;
            public float Range;
            public Color Color;

            /// <summary>Personnage habillé (PNJ) : il se tourne vers le joueur.</summary>
            public bool Faces;

            /// <summary>Angle de repos (degrés), repris quand le joueur s'éloigne.</summary>
            public float RestYaw;

            /// <summary>Sous-lot V2.1 : personnage habillé du PNJ (il marche quand il rentre chez lui ou revient à sa place).</summary>
            public StyleBody Look;

            /// <summary>Sous-lot V2.1 : il marche (il ne se tourne pas vers le joueur en chemin).</summary>
            public bool Walking;

            /// <summary>Sous-lot V2.1 : sa place de jour et son angle de repos là-bas.</summary>
            public Vector3 DayPlace;
            public float DayYaw;
        }

        private readonly GameObject _root;
        private readonly List<Marker> _markers = new List<Marker>();
        private readonly List<Material> _owned = new List<Material>();
        private readonly Dictionary<string, Marker> _nodes = new Dictionary<string, Marker>();

        /// <summary>Sous-lot V2.1 : repères des PNJ, pour les mener à leur place de l'heure (devant leur porte la nuit).</summary>
        private readonly Dictionary<string, Marker> _npcs = new Dictionary<string, Marker>();

        /// <summary>Sous-lot V2.1 : temps (s) depuis la dernière relecture des places des PNJ.</summary>
        private float _npcAge;

        /// <summary>Sous-lot V2.1 : vitesse (m/s) d'un PNJ qui rentre chez lui.</summary>
        private const float NpcWalk = 1.4f;
        private Light _fire;
        private float _fireBase;

        public WorldMarkers(AdventureSession adventure)
        {
            _root = new GameObject("Repères");
            IReadOnlyList<WorldMarker> safePoints = adventure.SafePoints();
            // Sous-lot V1.6 : dans une pièce, le corps d'un PNJ qui parle est un villageois du décor (InteriorRules) ; le repère ne garde que
            // son nom (pas de capsule par-dessus), sauf sans personnages de villageois dans le catalogue.
            StyleCatalog catalog = StyleCatalog.Loaded;
            bool dressedIndoors = adventure.Indoors && catalog != null && catalog.Villagers > 0 && catalog.Character("paysan") != null;
            // Relecture de la PR #99 : la place et la pose de jour viennent de l'histoire (NpcsByDay), même pour une partie chargée la nuit.
            var byDay = adventure.NpcsByDay().ToDictionary(n => n.Id);
            foreach (WorldMarker npc in adventure.Npcs())
            {
                WorldMarker day = byDay.TryGetValue(npc.Id, out WorldMarker d0) ? d0 : npc;
                Marker m = Add(npc, PrimitiveType.Capsule, new Vector3(0.8f, 1f, 0.8f), 1f, new Color(0.95f, 0.78f, 0.3f), 2.3f, NpcLabelRange);
                _npcs[npc.Id] = m;
                if (dressedIndoors)
                {
                    m.Body.GetComponent<Renderer>().enabled = false;
                    continue;
                }
                // Essai réaliste (demande de Damien du 26 octobre 2026) : le PNJ prend son personnage Mixamo (dossier Odile, Maelle,
                // Gaspard) s'il existe, et respire en attendant (l'attente tourne en boucle sans autre mise à jour).
                StyleBody look = StyleLook.Dress(m.Body, StyleSlot.Character, npc.Id, 1.75f);
                if (look != null) look.Update(m.Body.transform.position, false, 0f);
                m.Faces = look != null;
                m.Look = look;
                // Au repos, il regarde le point sûr le plus proche (la place du village, le camp), d'où l'on arrive.
                WorldMarker home = null;
                double best = double.MaxValue;
                foreach (WorldMarker s in safePoints)
                {
                    double d = (s.X - day.X) * (s.X - day.X) + (s.Z - day.Z) * (s.Z - day.Z);
                    if (d < best && d > 0.01)
                    {
                        best = d;
                        home = s;
                    }
                }
                if (home != null) m.RestYaw = (float)StyleRules.FaceYaw(day.X, day.Z, home.X, home.Z);
                m.DayPlace = new Vector3((float)day.X, 0f, (float)day.Z);
                m.DayYaw = m.RestYaw;
                // Chargé la nuit devant sa porte : il regarde la rue, vers sa place de jour (dos à sa porte), comme s'il venait d'arriver.
                bool away = System.Math.Abs(npc.X - day.X) > 0.01 || System.Math.Abs(npc.Z - day.Z) > 0.01;
                if (away) m.RestYaw = (float)StyleRules.FaceYaw(npc.X, npc.Z, day.X, day.Z);
                m.Body.transform.rotation = Quaternion.Euler(0f, m.RestYaw, 0f);
            }
            foreach (WorldMarker node in adventure.Nodes())
            {
                bool herb = node.Profession == "herboristerie";
                Marker m = Add(node, herb ? PrimitiveType.Sphere : PrimitiveType.Cube, herb ? new Vector3(0.7f, 0.5f, 0.7f) : new Vector3(0.8f, 0.7f, 0.8f),
                    herb ? 0.25f : 0.35f, herb ? new Color(0.35f, 0.8f, 0.35f) : new Color(0.55f, 0.55f, 0.6f), 1.2f, NodeLabelRange);
                if (herb) Herb(m);
                else Ore(m);
                _nodes[node.Id] = m;
            }
            foreach (WorldMarker safe in adventure.SafePoints())
                Add(safe, PrimitiveType.Cylinder, new Vector3(3f, 0.02f, 3f), 0.02f, new Color(0.3f, 0.55f, 1f), 0.6f, SafeLabelRange);
            // Essai du 12.9 : la forge (enclume sombre au centre de l'atelier), où l'on fabrique.
            foreach (WorldMarker workshop in adventure.Workshops())
                Forge(Add(workshop, PrimitiveType.Cube, new Vector3(1.2f, 0.8f, 0.7f), 0.4f, new Color(0.3f, 0.3f, 0.34f), 1.6f, NpcLabelRange));
            // Intérieurs : chaque porte, un seuil doré au sol et le nom de ce qu'il y a derrière (« Auberge de l'Aube », « Bourg d'Aube »).
            foreach (WorldMarker door in adventure.Doors())
            {
                Marker m = Add(door, PrimitiveType.Cylinder, new Vector3(1.3f, 0.015f, 1.3f), 0.015f, new Color(0.95f, 0.72f, 0.32f), 2.6f, DoorLabelRange);
                Material sill = Made(new Color(0.35f, 0.22f, 0.08f));
                sill.EnableKeyword("_EMISSION");
                sill.SetColor("_EmissionColor", new Color(0.9f, 0.55f, 0.18f));
                m.Body.GetComponent<Renderer>().sharedMaterial = sill;
            }
        }

        /// <summary>Intérieurs : distance (m) d'où se lit le nom d'une porte.</summary>
        private const float DoorLabelRange = 9f;

        private Marker Add(WorldMarker data, PrimitiveType shape, Vector3 scale, float y, Color color, float height, float range)
        {
            GameObject body = GameObject.CreatePrimitive(shape);
            body.name = data.Id;
            Object.Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(_root.transform, false);
            body.transform.position = new Vector3((float)data.X, y, (float)data.Z);
            body.transform.localScale = scale;
            body.GetComponent<Renderer>().sharedMaterial = Made(color);
            var marker = new Marker { Data = data, Body = body, Height = height, Range = range, Color = color };
            _markers.Add(marker);
            return marker;
        }

        /// <summary>
        /// Habille un repère d'un décor posé au sol à sa place (enfant d'un support sans échelle, lui-même enfant du repère pour être caché
        /// avec lui quand le nœud repousse) ; la forme simple n'est plus dessinée.
        /// </summary>
        private static Transform Holder(Marker m)
        {
            var holder = new GameObject(m.Data.Id + " (essai réaliste)");
            holder.transform.SetParent(m.Body.transform, false);
            Vector3 s = m.Body.transform.localScale;
            holder.transform.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);
            holder.transform.position = new Vector3(m.Body.transform.position.x, 0f, m.Body.transform.position.z);
            return holder.transform;
        }

        private static void Hide(Marker m)
        {
            Renderer own = m.Body.GetComponent<Renderer>();
            if (own != null) own.enabled = false;
        }

        /// <summary>Herbe de l'Aube : une pervenche de Poly Haven (une variante du lot), plus grande que nature pour qu'on la voie.</summary>
        private static void Herb(Marker m)
        {
            Transform holder = Holder(m);
            GameObject plant = StyleLook.Prop(holder, StyleProp.Herb, m.Data.Id, holder.position, StyleRules.Variant(m.Data.Id, 360), 2.4f, true);
            if (plant == null)
            {
                Object.Destroy(holder.gameObject);
                return;
            }
            Hide(m);
        }

        /// <summary>
        /// Filon de fer : un rocher réaliste du catalogue (choisi d'après le nœud), d'un mètre de large, hérissé de quelques éclats de minerai
        /// rouille et métal.
        /// </summary>
        private void Ore(Marker m)
        {
            Transform holder = Holder(m);
            GameObject rock = StyleLook.Decor(holder, StyleSlot.Rock, m.Data.Id, holder.position, 0f, 1.1f);
            if (rock == null)
            {
                Object.Destroy(holder.gameObject);
                return;
            }
            Hide(m);
            StyleLook.VisibleBounds(rock, out Bounds b);
            Material ore = Made(new Color(0.42f, 0.27f, 0.2f));
            ore.SetFloat("_Metallic", 0.75f);
            ore.SetFloat("_Smoothness", 0.55f);
            for (int i = 0; i < 6; i++)
            {
                string key = m.Data.Id + "/eclat" + i;
                float angle = (i * 60f + StyleRules.Variant(key, 40)) * Mathf.Deg2Rad;
                float lift = 0.25f + 0.012f * StyleRules.Variant(key + "h", 50);
                var at = new Vector3(b.center.x + Mathf.Cos(angle) * b.extents.x * 0.75f, b.min.y + b.size.y * lift, b.center.z + Mathf.Sin(angle) * b.extents.z * 0.75f);
                GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shard.name = "Éclat de minerai";
                Object.Destroy(shard.GetComponent<Collider>());
                shard.transform.SetParent(holder, false);
                shard.transform.position = at;
                float size = 0.12f + 0.002f * StyleRules.Variant(key + "s", 40);
                shard.transform.localScale = new Vector3(size, size * 2.2f, size);
                shard.transform.rotation = Quaternion.Euler(StyleRules.Variant(key + "x", 70) - 35f, angle * Mathf.Rad2Deg, StyleRules.Variant(key + "z", 70) - 35f);
                shard.GetComponent<Renderer>().sharedMaterial = ore;
            }
        }

        /// <summary>
        /// Forge de Maëlle : une enclume (faite par le code, fer sombre) avec le marteau posé dessus, le foyer de pierre à côté avec ses
        /// braises et une lueur qui vacille, un tonneau et une caisse derrière. Sans le foyer dans le catalogue, l'enclume simple reste.
        /// </summary>
        private void Forge(Marker m)
        {
            Transform holder = Holder(m);
            // Maëlle se tient au centre de la forge : l'enclume est un pas derrière elle (au nord), le foyer à sa droite.
            Vector3 c = holder.position + new Vector3(0.2f, 0f, 1.6f);
            GameObject pit = StyleLook.Prop(holder, StyleProp.FirePit, m.Data.Id + "/foyer", c + new Vector3(1.9f, 0f, -0.2f), 0f);
            if (pit == null)
            {
                Object.Destroy(holder.gameObject);
                return;
            }
            Hide(m);
            Anvil(holder, c);
            // Le marteau couché sur la table de l'enclume.
            GameObject hammer = StyleLook.Prop(holder, StyleProp.Hammer, m.Data.Id + "/marteau", c + new Vector3(0.05f, 0.79f, 0.02f), 20f);
            if (hammer != null)
            {
                hammer.transform.rotation = Quaternion.Euler(90f, 20f, 0f);
                StyleLook.Ground(hammer, 0.79f);
            }
            StyleLook.Prop(holder, StyleProp.Barrel, m.Data.Id + "/tonneau", c + new Vector3(-1.5f, 0f, 0.3f), 35f);
            StyleLook.Prop(holder, StyleProp.Crate, m.Data.Id + "/caisse", c + new Vector3(-1.7f, 0f, -1.6f), 75f);
            // Braises au fond du foyer et lueur orange (une seule lumière par forge).
            StyleLook.VisibleBounds(pit, out Bounds b);
            GameObject embers = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            embers.name = "Braises";
            Object.Destroy(embers.GetComponent<Collider>());
            embers.transform.SetParent(holder, false);
            embers.transform.position = new Vector3(b.center.x, b.min.y + 0.08f, b.center.z);
            embers.transform.localScale = new Vector3(b.size.x * 0.55f, 0.03f, b.size.z * 0.55f);
            Material glow = Made(new Color(0.25f, 0.06f, 0.02f));
            glow.EnableKeyword("_EMISSION");
            glow.SetColor("_EmissionColor", new Color(1.4f, 0.42f, 0.1f));   // V1.10 : moins fort, pour ne pas blanchir sous le halo
            embers.GetComponent<Renderer>().sharedMaterial = glow;
            var light = new GameObject("Lueur du foyer").AddComponent<Light>();
            light.transform.SetParent(holder, false);
            light.transform.position = new Vector3(b.center.x, b.max.y + 0.4f, b.center.z);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.55f, 0.2f);
            light.range = 5f;
            light.intensity = 2.5f;
            _fire = light;
            _fireBase = light.intensity;
            // Essai réaliste 8 : sur ordinateur, les flammes et la fumée du Particle Pack de Unity dans le foyer, s'il est importé.
            Effects.Ambient(StyleVfx.ForgeFire, holder, new Vector3(b.center.x, b.min.y + 0.1f, b.center.z));
            // V1.10 (essai 10 : « fumée noire de la forge ») : la fumée du jeu, claire et non éclairée, à la place de celle du pack (Effects.Smoke).
            Effects.Smoke(holder, new Vector3(b.center.x, b.max.y + 0.2f, b.center.z));
        }

        /// <summary>Enclume de fer sombre faite de quatre pavés (pied, taille, table, bigorne), d'environ 0,8 m de haut.</summary>
        private void Anvil(Transform holder, Vector3 c)
        {
            Material iron = Made(new Color(0.16f, 0.16f, 0.18f));
            iron.SetFloat("_Metallic", 0.85f);
            iron.SetFloat("_Smoothness", 0.5f);
            Material wood = StyleWorld.Surface(StyleSurface.Wood, out float _) ?? Made(new Color(0.35f, 0.25f, 0.16f));
            void Block(string name, Vector3 center, Vector3 size, Material material)
            {
                GameObject b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b.name = name;
                Object.Destroy(b.GetComponent<Collider>());
                b.transform.SetParent(holder, false);
                b.transform.position = c + center;
                b.transform.localScale = size;
                b.GetComponent<Renderer>().sharedMaterial = material;
            }
            Block("Billot", new Vector3(0f, 0.25f, 0f), new Vector3(0.6f, 0.5f, 0.6f), wood);
            Block("Enclume (pied)", new Vector3(0f, 0.55f, 0f), new Vector3(0.36f, 0.1f, 0.28f), iron);
            Block("Enclume (taille)", new Vector3(0f, 0.64f, 0f), new Vector3(0.2f, 0.1f, 0.16f), iron);
            Block("Enclume (table)", new Vector3(0f, 0.74f, 0f), new Vector3(0.56f, 0.1f, 0.2f), iron);
            GameObject horn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            horn.name = "Enclume (bigorne)";
            Object.Destroy(horn.GetComponent<Collider>());
            horn.transform.SetParent(holder, false);
            horn.transform.position = c + new Vector3(0.36f, 0.745f, 0f);
            horn.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            horn.transform.localScale = new Vector3(0.12f, 0.09f, 0.12f);
            horn.GetComponent<Renderer>().sharedMaterial = iron;
        }

        /// <summary>
        /// Essai réaliste (demande de Damien du 27 octobre 2026 : « Odile est tournée du mauvais côté ») : chaque PNJ habillé se tourne vers
        /// le joueur quand il est à moins de <see cref="StyleRules.FaceRange"/> mètres, puis reprend sa pose ; la lueur de la forge vacille.
        /// </summary>
        public void Face(Vector3 player, float deltaTime)
        {
            foreach (Marker m in _markers)
            {
                if (!m.Faces || m.Walking) continue;
                Vector3 at = m.Body.transform.position;
                float dx = player.x - at.x, dz = player.z - at.z;
                bool near = dx * dx + dz * dz < StyleRules.FaceRange * StyleRules.FaceRange;
                double target = near ? StyleRules.FaceYaw(at.x, at.z, player.x, player.z) : m.RestYaw;
                double yaw = StyleRules.Turn(m.Body.transform.eulerAngles.y, target, TurnSpeed * deltaTime);
                m.Body.transform.rotation = Quaternion.Euler(0f, (float)yaw, 0f);
            }
            if (_fire != null) _fire.intensity = _fireBase * (0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 3f, 0.5f));
        }

        /// <summary>
        /// Sous-lot V2.1 (jour et nuit) : chaque PNJ marche vers sa place de l'heure (<see cref="AdventureSession.Npcs"/> : devant sa porte la
        /// nuit pour ceux qui rentrent chez eux, sa place le jour), au pas. Les places sont relues deux fois par seconde ; on lui parle à la
        /// place du monde (le repère n'est que l'affichage).
        /// </summary>
        public void Walk(AdventureSession adventure, float deltaTime)
        {
            _npcAge += deltaTime;
            if (_npcAge >= 0.5f)
            {
                _npcAge = 0f;
                foreach (WorldMarker npc in adventure.Npcs())
                    if (_npcs.TryGetValue(npc.Id, out Marker m)) m.Data = npc;
            }
            foreach (Marker m in _npcs.Values)
            {
                Vector3 at = m.Body.transform.position;
                var target = new Vector3((float)m.Data.X, at.y, (float)m.Data.Z);
                Vector3 step = Vector3.MoveTowards(at, target, NpcWalk * deltaTime);
                bool moving = (step - at).sqrMagnitude > 1e-8f;
                m.Walking = moving;
                if (moving)
                {
                    m.Body.transform.position = step;
                    Vector3 way = target - at;
                    way.y = 0f;
                    if (m.Faces && way.sqrMagnitude > 0.0001f) m.Body.transform.rotation = Quaternion.LookRotation(way);
                    // Arrivé : à sa place de jour, sa pose de jour ; devant sa porte, il regarde la rue (dos à la porte).
                    if ((target - step).sqrMagnitude < 1e-6f)
                    {
                        bool day = Mathf.Abs(target.x - m.DayPlace.x) < 0.01f && Mathf.Abs(target.z - m.DayPlace.z) < 0.01f;
                        if (day) m.RestYaw = m.DayYaw;
                        else if (way.sqrMagnitude > 0.0001f) m.RestYaw = Quaternion.LookRotation(way).eulerAngles.y + 180f;
                    }
                }
                if (m.Look != null) m.Look.Update(m.Body.transform.position, false, deltaTime);
            }
        }

        /// <summary>Nœuds récoltés cachés jusqu'à leur repousse.</summary>
        public void Sync(AdventureSession adventure)
        {
            foreach (WorldMarker node in adventure.Nodes())
                if (_nodes.TryGetValue(node.Id, out Marker m))
                {
                    m.Data = node;
                    m.Body.SetActive(node.Ready);
                }
        }

        /// <summary>Noms des repères proches du joueur (essai réaliste 6 : dans l'interface uGUI, à chaque image).</summary>
        public void Labels(Camera camera, Vector3 player, HudView hud)
        {
            foreach (Marker m in _markers)
            {
                if (!m.Body.activeSelf) continue;
                Vector3 at = m.Body.transform.position;
                float dx = at.x - player.x, dz = at.z - player.z;
                if (dx * dx + dz * dz > m.Range * m.Range) continue;
                Vector3 screen = camera.WorldToScreenPoint(new Vector3(at.x, m.Height, at.z));
                hud.Label(screen, m.Data.Name, Color.Lerp(m.Color, Color.white, 0.5f));   // essai réaliste 6 : interface uGUI
            }
        }

        /// <summary>Matériau fait pour les repères, détruit avec eux (relecture : les repères sont refaits à chaque porte).</summary>
        private Material Made(Color color)
        {
            Material material = ArenaView.MakeMaterial(color);
            _owned.Add(material);
            return material;
        }

        public void Destroy()
        {
            Object.Destroy(_root);
            foreach (Material material in _owned)
                if (material != null) Object.Destroy(material);
            _owned.Clear();
        }
    }
}
