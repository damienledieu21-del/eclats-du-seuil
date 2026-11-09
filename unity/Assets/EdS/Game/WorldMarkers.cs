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

            /// <summary>Sous-lot V2.3 : ce qu'il fait à cette heure (« au fournil »), sous son nom ; null : rien à montrer.</summary>
            public string Activity;
            public string Label;

            /// <summary>
            /// Sous-lot V2.3.1 (PNJ qui changent de lieu) : le PNJ est dans la zone pour le cœur (<see cref="WorldMarker.Present"/>) ; absent,
            /// son repère est à la porte par où il part ou reviendra.
            /// </summary>
            public bool Present = true;

            /// <summary>Sous-lot V2.3.1 : le PNJ est montré (caché une fois arrivé à la porte quand il part, montré à la porte quand il revient).</summary>
            public bool Shown = true;

            /// <summary>Sous-lot V2.3.1 : l'état montré est à appliquer (au premier passage de <see cref="Walk"/>, avec le corps de la pièce).</summary>
            public bool Dirty = true;

            /// <summary>
            /// Sous-lot V2.8 (recherche de chemin) : son trajet, qui contourne les maisons et les murs (recalculé quand sa place change) ;
            /// sans chemin, il va droit comme avant.
            /// </summary>
            public readonly Walker Route = new Walker { StraightWhenBlocked = true };
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
            bool dressedIndoors = adventure.Indoors && catalog != null && catalog.Villagers > 0
                && (catalog.Character("paysan") != null || HumanBodies.Usable(catalog));   // sous-lot V2.9.2 : ou les corps humains
            // Relecture de la PR #99 : la place et la pose de jour viennent de l'histoire (NpcsByDay), même pour une partie chargée la nuit.
            var byDay = adventure.NpcsByDay().ToDictionary(n => n.Id);
            // Sous-lot V2.3.1 : tous ceux qui passent ici dans la journée ; un absent attend, caché, à la porte par où il reviendra.
            foreach (WorldMarker npc in adventure.NpcStage())
            {
                WorldMarker day = byDay.TryGetValue(npc.Id, out WorldMarker d0) ? d0 : npc;
                Marker m = Add(npc, PrimitiveType.Capsule, new Vector3(0.8f, 1f, 0.8f), 1f, new Color(0.95f, 0.78f, 0.3f), 2.3f, NpcLabelRange);
                m.Present = npc.Present;
                m.Shown = npc.Present;
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
            // Sous-lot V2.11 (5e vague) : le fournil de Firmin, l'étal de Bertille, les champs, le hameau, les fêtes et le cœur du Seuil.
            if (!adventure.Indoors && catalog != null) Wave5Decor(adventure, byDay, catalog);
        }

        /// <summary>
        /// Sous-lot V2.11 (5e vague) : décor posé près des PNJ et des lieux, à une place libre (jamais dans un mur) : le four à pain de Firmin
        /// (avec ses sacs de farine, sa table de pains scannés et de viennoiseries, et le son des braises tout près), la caisse de poires de
        /// Bertille, l'épouvantail et le foin des champs, la charrette du hameau, guirlande et bannières quand le bourg est en fête, cristaux et
        /// feu follet au cœur du Seuil. Chaque objet absent du catalogue n'est simplement pas posé.
        /// </summary>
        private void Wave5Decor(AdventureSession adventure, Dictionary<string, WorldMarker> byDay, StyleCatalog catalog)
        {
            AdventureMap map = adventure.Map;
            if (map == null) return;
            List<IReadOnlyList<(double X, double Z)>> obstacles = Wave5View.Polygons(map.Obstacles);
            Transform root = _root.transform;
            WorldMarker Npc(string word) => byDay.Values.FirstOrDefault(n => StyleRules.Key(n.Id) == "npc" + word);
            MapPlace Place(string id) => map.Places.FirstOrDefault(p => p.Id == id);
            float Yaw(WorldMarker n) => _npcs.TryGetValue(n.Id, out Marker m) ? m.RestYaw : 0f;

            // Le fournil : le four à la droite de Firmin, la table à sa gauche.
            if (Npc("boulanger") is WorldMarker baker)
            {
                float face = Yaw(baker);
                GameObject oven = Wave5View.PlaceNear(root, Wave5Role.BreadOven, "four de Firmin", baker.X, baker.Z, face + 90, 3.2, 1.9, obstacles, face + 180);
                if (oven != null)
                {
                    Vector3 o = oven.transform.position;
                    Wave5View.PlaceNear(root, Wave5Role.FlourSack, "farine 1", o.x, o.z, face + 180, 2.2, 0.5, obstacles, StyleRules.Variant("farine1", 360));
                    Wave5View.PlaceNear(root, Wave5Role.FlourSack, "farine 2", o.x, o.z, face + 200, 2.9, 0.5, obstacles, StyleRules.Variant("farine2", 360));
                    OvenSound(oven, catalog);
                }
                GameObject table = Wave5View.PlaceNear(root, Wave5Role.BakeryTable, "table du fournil", baker.X, baker.Z, face - 90, 2.4, 1.1, obstacles, face);
                // Sous-lot V2.12 : sans la table de Poly Haven, l'étal marchand de RPG Food Props sert de vitrine.
                if (table == null) table = Wave5View.PlaceNear(root, Wave5Role.TraderTable, "étal du fournil", baker.X, baker.Z, face - 90, 2.4, 1.2, obstacles, face);
                if (table != null) Showcase(root, table, catalog, face);
            }
            if (Npc("epiciere") is WorldMarker grocer)
            {
                Wave5View.PlaceNear(root, Wave5Role.PearCrate, "poires de Bertille", grocer.X, grocer.Z, Yaw(grocer) + 60, 1.4, 0.45, obstacles, Yaw(grocer));
                // Sous-lot V2.12 : l'étal de Bertille (table garnie de victuailles, caisses au pied), devant elle.
                MarketStall(root, grocer.X, grocer.Z, Yaw(grocer), obstacles, false, "étal de Bertille");
            }
            StoreDecor(adventure, root, obstacles, catalog);
            if (Place("champs") is MapPlace fields)
            {
                Wave5View.PlaceNear(root, Wave5Role.Scarecrow, "épouvantail", fields.CenterX, fields.CenterZ, 30, 3, 0.8, obstacles, 200);
                Wave5View.PlaceNear(root, Wave5Role.HayBales, "foin 1", fields.CenterX, fields.CenterZ, 140, 7, 0.8, obstacles, 20);
                Wave5View.PlaceNear(root, Wave5Role.HayBales, "foin 2", fields.CenterX, fields.CenterZ, 160, 8.5, 0.8, obstacles, 75);
            }
            // Décision de Damien du 9 novembre 2026 : les tours en ruine aux ruines, sur ordinateur seulement (Wave5 : jamais sur téléphone, où
            // les ruines restent en formes simples), à la taille du lieu, le long de son plus grand côté.
            if (Place("ruines") is MapPlace ruins && Wave5View.Has(Wave5Role.RuinedTowers))
            {
                bool alongX = ruins.MaxX - ruins.MinX >= ruins.MaxZ - ruins.MinZ;
                GameObject towers = Wave5View.Place(root, Wave5Role.RuinedTowers, "tours en ruine", new Vector3((float)ruins.CenterX, 0f, (float)ruins.CenterZ), alongX ? 90f : 0f);
                if (towers != null && StyleLook.VisibleBounds(towers, out Bounds tb))
                {
                    float side = (float)System.Math.Max(ruins.MaxX - ruins.MinX, ruins.MaxZ - ruins.MinZ) * 0.8f;
                    float longest = Mathf.Max(tb.size.x, tb.size.z);
                    if (longest > side) towers.transform.localScale *= side / longest;
                    StyleLook.Ground(towers, 0f);
                }
            }
            if (Place("hameau") is MapPlace hamlet)
                Wave5View.PlaceNear(root, Wave5Role.Cart, "charrette", hamlet.CenterX, hamlet.CenterZ, 250, 5, 2.2, obstacles, 70);
            // Les fêtes : réglage « Human Festival » du catalogue (tenues de fête) ; la PR des fêtes du jeu (#120) pourra l'allumer aussi.
            if (catalog.HumanFestival && Place("bourg") is MapPlace town)
            {
                // Sous-lot V2.12 : un étal sous tente au bourg les jours de fête (ordinateur seulement ; la table garnie seule sur téléphone).
                MarketStall(root, town.CenterX, town.CenterZ, 180f, obstacles, true, "étal de fête");
                Wave5View.PlaceNear(root, Wave5Role.Bunting, "guirlande", town.CenterX, town.CenterZ, 0, 3, 1.6, obstacles, 90);
                Wave5View.PlaceNear(root, Wave5Role.Banner, "bannière 1", town.CenterX, town.CenterZ, 90, 6, 0.6, obstacles, 270);
                Wave5View.PlaceNear(root, Wave5Role.Banner, "bannière 2", town.CenterX, town.CenterZ, 270, 6, 0.6, obstacles, 90);
            }
            // Le cœur du Seuil : le grand cristal, une couronne de cristaux lumineux et le feu follet qui flotte.
            if (Place("coeur_du_seuil") is MapPlace heart)
            {
                GameObject big = Wave5View.PlaceNear(root, Wave5Role.StrangeCrystal, "grand cristal", heart.CenterX, heart.CenterZ, 0, 3, 1.0, obstacles, 0);
                for (int i = 0; i < 4; i++)
                    Wave5View.PlaceNear(root, Wave5Role.Crystals, "cristaux " + i, heart.CenterX, heart.CenterZ, 45 + 90 * i, 7, 0.8, obstacles, 90 * i + 20);
                GameObject wisp = Wave5View.PlaceNear(root, Wave5Role.Wisp, "feu follet", heart.CenterX, heart.CenterZ, 180, 2.5, 0.5, obstacles, 0);
                if (wisp != null) wisp.transform.position += Vector3.up * 1.6f;
                if (big != null || wisp != null)
                {
                    var light = new GameObject("Lueur du cœur du Seuil (V2.11)").AddComponent<Light>();
                    light.transform.SetParent(root, false);
                    light.transform.position = new Vector3((float)heart.CenterX, 2.2f, (float)heart.CenterZ);
                    light.type = LightType.Point;
                    light.color = new Color(0.55f, 0.65f, 1f);
                    light.range = 9f;
                    light.intensity = 1.6f;
                }
            }
        }

        /// <summary>
        /// Sous-lot V2.12 : un étal marchand (RPG Food Props) près de (<paramref name="x"/>, <paramref name="z"/>), face à <paramref name="face"/> :
        /// la table garnie de victuailles (jamais tomate, orange, pastèque ni citrouille : <see cref="AssetStore.Avoided"/>), deux caisses garnies
        /// au pied et, si <paramref name="tent"/>, la tente (ordinateur seulement). Public : le marché et les fêtes de la V2.10 (PR #120) pourront
        /// le poser à leurs places. Null sans la table (rien n'est posé).
        /// </summary>
        public static GameObject MarketStall(Transform root, double x, double z, float face, List<IReadOnlyList<(double X, double Z)>> obstacles, bool tent, string id)
        {
            GameObject stall = null;
            if (tent) stall = Wave5View.PlaceNear(root, Wave5Role.TraderTent, id + " (tente)", x, z, face, 2.6, 1.8, obstacles, face + 180f);
            if (stall == null) stall = Wave5View.PlaceNear(root, Wave5Role.TraderTable, id, x, z, face, 1.9, 1.1, obstacles, face + 180f);
            if (stall == null) return null;
            Wave5View.Spread(root, stall, Wave5Role.StoreFood, 8, face + 180f, 0.28f, id + " victuailles");
            Vector3 at = stall.transform.position;
            Wave5View.PlaceNear(root, Wave5Role.StoreCrate, id + " caisse 1", at.x, at.z, face + 100f, 1.3, 0.35, obstacles, face + 15f, 0f, StyleRules.Variant(id + "1", 6));
            Wave5View.PlaceNear(root, Wave5Role.StoreCrate, id + " caisse 2", at.x, at.z, face - 100f, 1.3, 0.35, obstacles, face - 20f, 0f, StyleRules.Variant(id + "2", 6));
            return stall;
        }

        /// <summary>
        /// Sous-lot V2.12 : décor des packs de l'Asset Store, à des places libres (jamais dans un mur), réglables au catalogue (échelle, rotation,
        /// décalage) : la forge (enclume, établi, présentoir d'armes, bac), la roue à eau entre le moulin et la rivière, l'abri à bêtes et le rouet du
        /// hameau, tonneaux, seaux, caisses, échelle, charrettes et souche au village et au bourg, tentes au camp des rôdeurs. Les bâtiments choisis
        /// par Damien (maisons de Sketchfab, moulin de Tim0) ne sont pas remplacés. Rien n'est posé sans le pack.
        /// </summary>
        private void StoreDecor(AdventureSession adventure, Transform root, List<IReadOnlyList<(double X, double Z)>> obstacles, StyleCatalog catalog)
        {
            AdventureMap map = adventure.Map;
            MapPlace Place(string id) => map.Places.FirstOrDefault(p => p.Id == id);
            foreach (WorldMarker w in adventure.Workshops())
            {
                Wave5View.PlaceNear(root, Wave5Role.Anvil, "enclume " + w.Id, w.X, w.Z, 40, 1.7, 0.6, obstacles, 90);
                Wave5View.PlaceNear(root, Wave5Role.SmithTable, "établi " + w.Id, w.X, w.Z, 140, 2.6, 0.9, obstacles, 320);
                Wave5View.PlaceNear(root, Wave5Role.WeaponStand, "présentoir " + w.Id, w.X, w.Z, 230, 2.6, 0.7, obstacles, 50);
                Wave5View.PlaceNear(root, Wave5Role.QuenchTub, "bac " + w.Id, w.X, w.Z, 320, 2.0, 0.6, obstacles, 0);
                Wave5View.PlaceNear(root, Wave5Role.VillageBarrel, "tonneau " + w.Id, w.X, w.Z, 270, 3.2, 0.5, obstacles, 0);
            }
            // La roue à eau : sur la rive de la rivière la plus proche du moulin (le moulin de Tim0 reste ; position estimée, à régler par Offset).
            if (Place("moulin") is MapPlace mill && Place("riviere") is MapPlace river && Wave5View.Has(Wave5Role.WaterWheel))
            {
                float bx = Mathf.Clamp((float)mill.CenterX, (float)river.MinX, (float)river.MaxX), bz = Mathf.Clamp((float)mill.CenterZ, (float)river.MinZ, (float)river.MaxZ);
                float yaw = Mathf.Atan2(bx - (float)mill.CenterX, bz - (float)mill.CenterZ) * Mathf.Rad2Deg;
                Wave5View.Place(root, Wave5Role.WaterWheel, "roue à eau", new Vector3(bx, 0f, bz), yaw + 90f);
            }
            (Wave5Role Role, double Yaw, double Distance, double Clearance)[] village =
            {
                (Wave5Role.VillageBarrel, 20, 6, 0.5), (Wave5Role.VillageBarrel, 35, 6.6, 0.5), (Wave5Role.VillageBucket, 60, 5.5, 0.3),
                (Wave5Role.PropCrate, 110, 7, 0.6), (Wave5Role.PropCrate, 125, 7.4, 0.6), (Wave5Role.Ladder, 160, 8, 0.6), (Wave5Role.Handcart, 210, 7, 1.2),
                (Wave5Role.Stump, 300, 6, 0.5),
            };
            foreach (string placeId in new[] { "village", "bourg" })
                if (Place(placeId) is MapPlace p)
                    for (int i = 0; i < village.Length; i++)
                        Wave5View.PlaceNear(root, village[i].Role, placeId + " décor " + i, p.CenterX, p.CenterZ, village[i].Yaw + (placeId == "bourg" ? 15 : 0),
                            village[i].Distance, village[i].Clearance, obstacles, StyleRules.Variant(placeId + i, 360), 0f, i);
            if (Place("bourg") is MapPlace town) Wave5View.PlaceNear(root, Wave5Role.VillageCart, "charrette du bourg", town.CenterX, town.CenterZ, 320, 8, 2, obstacles, 40);
            if (Place("hameau") is MapPlace hamlet)
            {
                Wave5View.PlaceNear(root, Wave5Role.AnimalShed, "abri à bêtes", hamlet.CenterX, hamlet.CenterZ, 120, 9, 3.2, obstacles, 300);
                Wave5View.PlaceNear(root, Wave5Role.SpinningWheel, "rouet", hamlet.CenterX, hamlet.CenterZ, 30, 4, 0.6, obstacles, 210);
                Wave5View.PlaceNear(root, Wave5Role.VillageBucket, "seau du hameau", hamlet.CenterX, hamlet.CenterZ, 60, 4.5, 0.3, obstacles, 0);
                Wave5View.PlaceNear(root, Wave5Role.Stump, "souche du hameau", hamlet.CenterX, hamlet.CenterZ, 200, 5, 0.5, obstacles, 0);
            }
            if (Place("camp_des_rodeurs") is MapPlace camp)
            {
                Wave5View.PlaceNear(root, Wave5Role.CampTent, "tente 1", camp.CenterX, camp.CenterZ, 0, 4, 1.6, obstacles, 180);
                Wave5View.PlaceNear(root, Wave5Role.CampTent, "tente 2", camp.CenterX, camp.CenterZ, 120, 4.5, 1.6, obstacles, 300);
                Wave5View.PlaceNear(root, Wave5Role.PropCrate, "caisse du camp", camp.CenterX, camp.CenterZ, 240, 3, 0.5, obstacles, 60, 0f, 2);
            }
        }

        /// <summary>
        /// Pains en vitrine sur la table du fournil, chacun à sa place, sans se toucher : ceux de LUMI (V2.12) d'abord, puis les scans de
        /// Megascans, puis les autres ; viennoiseries de Poly Haven entre eux.
        /// </summary>
        private static void Showcase(Transform root, GameObject table, StyleCatalog catalog, float face)
        {
            if (!StyleLook.VisibleBounds(table, out Bounds top)) return;
            List<Wave5Look> breads = catalog.Looks(Wave5Role.StoreBread), pastries = catalog.Looks(Wave5Role.Pastry);
            breads.AddRange(catalog.Looks(Wave5Role.Bread));
            int count = Mathf.Min(6, breads.Count + pastries.Count);
            for (int i = 0; i < count; i++)
            {
                Wave5Look look = i < breads.Count && (i % 3 != 2 || pastries.Count == 0) ? breads[i % breads.Count] : pastries.Count > 0 ? pastries[i % pastries.Count] : breads[i % breads.Count];
                float u = (i % 3 - 1) * 0.3f, v = (i / 3 - 0.5f) * 0.32f;
                Vector3 at = top.center + Quaternion.Euler(0f, face, 0f) * new Vector3(u * top.size.x / 1.2f, 0f, v);
                at.y = top.max.y;
                var holder = new GameObject("vitrine " + i + " (V2.11)");
                GameObject model = Object.Instantiate(look.Model, holder.transform, false);
                foreach (Collider c in holder.GetComponentsInChildren<Collider>()) Object.Destroy(c);
                Wave5View.Fix(holder, look.Model.name, look.Material);
                holder.transform.SetParent(root, false);
                // Un pain d'environ 30 cm (les scans et les modèles n'ont pas tous la même unité).
                if (StyleLook.VisibleBounds(holder, out Bounds b) && Mathf.Max(b.size.x, b.size.z) > 0.001f)
                    holder.transform.localScale = Vector3.one * (0.3f / Mathf.Max(b.size.x, b.size.z)) * (look.Scale > 0f ? look.Scale : 1f);
                holder.transform.rotation = Quaternion.Euler(0f, face + StyleRules.Variant("vitrine" + i, 60) - 30 + look.Yaw, 0f);
                holder.transform.position = at + look.Offset;
                StyleLook.Ground(holder, at.y + look.Offset.y);
            }
        }

        /// <summary>Les braises du four (Sacha.Julien), entendues tout près du four seulement (son 3D en boucle).</summary>
        private static void OvenSound(GameObject oven, StyleCatalog catalog)
        {
            AudioClip clip = catalog.AmbienceClip(StyleAmbience.Bakery);
            if (clip == null) return;
            AudioSource source = oven.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2f;
            source.maxDistance = 14f;
            source.volume = catalog.AmbienceVolume * 1.4f;
            source.time = clip.length * StyleRules.Variant("four", 100) / 100f;
            source.Play();
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
        /// <param name="arena">Revue Codex de la PR #103 : dans une pièce, le corps visible d'un PNJ est un villageois du décor (le repère est
        /// caché) : il suit le repère, à chaque image (null : pas de corps à suivre).</param>
        /// <remarks>
        /// Sous-lot V2.3.1 (PNJ qui changent de lieu) : un PNJ qui part dans une autre zone marche jusqu'à la porte (son repère y est, <see
        /// cref="AdventureSession.NpcStage"/>), puis disparaît ; quand il revient, il apparaît à la porte et marche jusqu'à sa place. Le cœur
        /// le tient déjà pour parti (on ne lui parle plus ici dès l'heure de son créneau) : ce n'est que l'affichage.
        /// </remarks>
        public void Walk(AdventureSession adventure, float deltaTime, ArenaView arena = null)
        {
            _npcAge += deltaTime;
            WalkPaths paths = adventure.Paths;   // sous-lot V2.8
            if (_npcAge >= 0.5f)
            {
                _npcAge = 0f;
                foreach (WorldMarker npc in adventure.NpcStage())
                    if (_npcs.TryGetValue(npc.Id, out Marker m))
                    {
                        m.Data = npc;
                        // Il revient : il apparaît à la porte (où son repère attendait), puis marche jusqu'à sa place.
                        if (npc.Present && !m.Shown)
                        {
                            m.Shown = true;
                            m.Dirty = true;
                        }
                        m.Present = npc.Present;
                        // Sous-lot V2.3 : son activité de l'emploi du temps, sous son nom (relue avec sa place).
                        string activity = adventure.ActivityOf(npc.Id);
                        if (activity != m.Activity || m.Label == null)
                        {
                            m.Activity = activity;
                            m.Label = activity == null ? null : npc.Name + " · " + activity;
                        }
                    }
            }
            foreach (Marker m in _npcs.Values)
            {
                Vector3 at = m.Body.transform.position;
                var target = new Vector3((float)m.Data.X, at.y, (float)m.Data.Z);
                // Sous-lot V2.8 : il vise le prochain point de passage de son trajet (le but lui-même si la ligne droite est libre).
                Vector3 aim = target;
                if (m.Shown)
                {
                    (double ax, double az) = m.Route.Next(paths, at.x, at.z, target.x, target.z);
                    aim = new Vector3((float)ax, at.y, (float)az);
                }
                Vector3 step = Vector3.MoveTowards(at, aim, NpcWalk * deltaTime);
                bool moving = (step - at).sqrMagnitude > 1e-8f;
                // Sous-lot V2.3.1 : parti, arrivé à la porte : il disparaît (un caché ne marche pas, il attend à la porte).
                if (!m.Present && m.Shown && !moving)
                {
                    m.Shown = false;
                    m.Dirty = true;
                }
                if (!m.Shown && (target - at).sqrMagnitude > 1e-8f)
                {
                    m.Body.transform.position = step = target;
                    m.Route.Reset();
                    moving = false;
                }
                if (m.Dirty)
                {
                    m.Dirty = false;
                    m.Body.SetActive(m.Shown);
                    if (arena != null) arena.ShowSpeaker(m.Data.Id, m.Shown);
                }
                m.Walking = moving;
                if (moving)
                {
                    m.Body.transform.position = step;
                    Vector3 way = aim - at;
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
                else if (arena != null && m.Data != null)
                {
                    Vector3 here = m.Body.transform.position;
                    arena.MoveSpeaker(m.Data.Id, here.x, here.z, deltaTime);
                }
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
                hud.Label(screen, m.Label ?? m.Data.Name, Color.Lerp(m.Color, Color.white, 0.5f));   // sous-lot V2.3 : et son activité   // essai réaliste 6 : interface uGUI
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
