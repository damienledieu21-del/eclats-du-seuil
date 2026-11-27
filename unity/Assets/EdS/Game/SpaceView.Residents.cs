using System.Collections.Generic;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// V2.58 (villageois vivants, demande de Damien du 27 novembre 2026) : les habitants du Bourg et des hameaux suivent leur journée
    /// (<see cref="VillageLife"/>, fonctions pures : travail, repas, marché, taverne, maison, abri sous l'averse, fête, saison). Seuls les plus
    /// proches de la caméra ont un corps, et leur nombre suit le réglage « Qualité » (<see cref="VillageLife.Cap"/> : 10 sur le PC de Damien).
    /// Petites vies : ils regardent le joueur qui passe, le saluent, et bavardent entre eux. Affichage seulement : le moteur ne les connaît pas.
    /// </summary>
    public sealed partial class SpaceView
    {
        private sealed class ResidentBody
        {
            public Resident Data;
            public int Index;
            public Transform Body;
            public StyleBody Look;
            public float Height = 1f;
            public float Yaw;
            public VillagerActivity Playing = (VillagerActivity)(-1);
            public bool Greeted;
            public int Greetings;
            public float ChatAt;
            public int ChatTurn;
            public string Bubble;
            public float BubbleUntil;
            public bool Seen;
            /// <summary>Relecture de la PR #268 : trajet de rattrapage par les routes (sa place a changé sous ses pieds), et où il en est.</summary>
            public IReadOnlyList<(double X, double Z)> Chase;
            public int ChaseStep;
            public double ChaseToX, ChaseToZ;
        }

        /// <summary>Habitants de la région (gardés d'une vue à l'autre : même carte entière, mêmes habitants).</summary>
        private static VillageWorld _lifeWorld;
        private static AdventureMap _lifeWorldOf;

        private VillageWorld _life;
        private readonly Dictionary<string, ResidentBody> _residentBodies = new Dictionary<string, ResidentBody>();
        private readonly List<LifePose> _lifePoses = new List<LifePose>();
        private readonly List<ResidentBody> _lifeShown = new List<ResidentBody>();
        private readonly List<string> _lifeGone = new List<string>();
        /// <summary>Relecture de la PR #268 : habitants que rien n'a pu habiller (jamais réessayés : pas de corps créé puis détruit à chaque image).</summary>
        private readonly HashSet<string> _lifeFailed = new HashSet<string>();
        /// <summary>Indices des habitants qui ont un corps (hystérésis du choix, <see cref="VillageLife.Pick"/>).</summary>
        private readonly HashSet<int> _lifeCurrent = new HashSet<int>();
        private IReadOnlyList<int> _lifeNear = new int[0];
        private float _lifeNextPick;
        private double _lifeMinute = -1;
        private VillageContext _lifeContext = new VillageContext(WardrobeSeason.Spring, false, false, false);

        /// <summary>Corps habillés au plus par image (le reste attend les images suivantes : pas d'à-coup en entrant dans le Bourg).</summary>
        private const int ResidentsBuiltPerFrame = 2;

        /// <summary>Choix des habitants montrés, refait toutes les secondes (pas à chaque image).</summary>
        private const float ResidentsPickSeconds = 1f;

        /// <summary>Durée (s) d'un salut ou d'une réplique au-dessus de la tête ; écart entre deux répliques d'une conversation.</summary>
        private const float BubbleSeconds = 3.2f, ChatSeconds = 6.5f;

        /// <summary>
        /// V2.58 : la minute du monde (avec sa fraction), la saison, la fête et le grand jour de marché, et l'allure de marche en mètres par
        /// minute du monde (EdsGame, à chaque image). L'averse vient de la météo de la vue (<see cref="_sheltering"/>).
        /// </summary>
        public void Residents(double minute, WardrobeSeason season, bool festive, bool marketDay, double metersPerMinute)
        {
            _lifeMinute = minute;
            _lifeContext = new VillageContext(season, _sheltering, festive, marketDay, metersPerMinute);
        }

        /// <summary>Les habitants de la carte (une fois par région ; aucun dans un intérieur, dans l'Envers ni sans villageois au catalogue).</summary>
        private void ResidentsOf(AdventureMap map)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            if (map == null || map.Indoor || _envers || catalog == null || catalog.Villagers <= 0) return;
            AdventureMap whole = map.Whole;
            if (_lifeWorld == null || !ReferenceEquals(_lifeWorldOf, whole))
            {
                // Relecture de la PR #268 : loin des PNJ des pièces et des villageois du décor (marchands des étals compris).
                var avoid = new List<(double X, double Z)>();
                for (int i = 0; i < whole.Speakers.Count; i++) avoid.Add((whole.Speakers[i].X, whole.Speakers[i].Z));
                for (int i = 0; i < _villagers.Count; i++) avoid.Add((_villagers[i].Data.X, _villagers[i].Data.Z));
                _lifeWorld = VillageLife.World(whole, whole.AreaFields(_roads), avoid);
                _lifeWorldOf = whole;
            }
            _life = _lifeWorld;
        }

        /// <summary>
        /// Chaque image : choisit (toutes les secondes) les habitants dehors les plus proches de la caméra, habille ceux qui arrivent, défait
        /// ceux qui sont partis, puis pose chacun là où sa journée le met (<see cref="VillageLife.At"/>).
        /// </summary>
        private void ResidentsStep(Vector3 camera, Vector3 player, float deltaTime)
        {
            if (_life == null || _lifeMinute < 0 || _life.Residents.Count == 0) return;
            if (Time.time >= _lifeNextPick)
            {
                _lifeNextPick = Time.time + ResidentsPickSeconds;
                DeviceTier tier = DeviceQuality.Tier;
                _lifeCurrent.Clear();
                foreach (ResidentBody b in _residentBodies.Values) _lifeCurrent.Add(b.Index);
                _lifeNear = VillageLife.Pick(_life, _lifeMinute, _lifeContext, camera.x, camera.z, VillageLife.Radius(tier),
                    VillageLife.Cap(tier, DeviceQuality.Light), _lifeCurrent);
                foreach (ResidentBody b in _residentBodies.Values) b.Seen = false;
                for (int k = 0; k < _lifeNear.Count; k++)
                    if (_residentBodies.TryGetValue(_life.Residents[_lifeNear[k]].Id, out ResidentBody kept)) kept.Seen = true;
                _lifeGone.Clear();
                foreach (KeyValuePair<string, ResidentBody> kv in _residentBodies)
                    if (!kv.Value.Seen) _lifeGone.Add(kv.Key);
                for (int k = 0; k < _lifeGone.Count; k++)
                {
                    ResidentBody gone = _residentBodies[_lifeGone[k]];
                    _residentBodies.Remove(_lifeGone[k]);
                    if (gone.Body != null) Object.Destroy(gone.Body.gameObject);
                }
            }
            int built = 0;
            for (int k = 0; k < _lifeNear.Count && built < ResidentsBuiltPerFrame; k++)
            {
                Resident r = _life.Residents[_lifeNear[k]];
                if (_residentBodies.ContainsKey(r.Id) || _lifeFailed.Contains(r.Id)) continue;
                ResidentBody made = DressResident(r);
                built++;
                if (made == null)
                {
                    _lifeFailed.Add(r.Id);
                    continue;
                }
                made.Index = _lifeNear[k];
                _residentBodies[r.Id] = made;
            }
            _lifeShown.Clear();
            _lifePoses.Clear();
            foreach (ResidentBody b in _residentBodies.Values)
            {
                if (b.Body == null) continue;
                LifePose pose = VillageLife.At(_life, b.Data, _lifeMinute, _lifeContext);
                PlaceResident(b, pose, player, deltaTime);
                if (!b.Body.gameObject.activeSelf) continue;
                _lifeShown.Add(b);
                _lifePoses.Add(pose);
            }
            Chats();
        }

        /// <summary>
        /// Habille un habitant : le modèle du kit des villageois s'il est au catalogue (Tripo, pas encore livré), sinon le personnage du
        /// catalogue de son métier (corps humains existants) ; un enfant est plus petit. Null si rien ne peut l'habiller.
        /// </summary>
        private ResidentBody DressResident(Resident r)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            if (catalog == null) return null;
            string character = catalog.Character(r.Model) != null ? r.Model : r.Character;
            if (catalog.Character(character) == null && !HumanBodies.Usable(catalog)) return null;
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = r.Id + " (habitant)";
            Object.Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(_root.transform, false);
            body.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            StyleBody look = StyleLook.Dress(body, StyleSlot.Character, character, 1.8f, r.Id);
            if (look == null)
            {
                Object.Destroy(body);
                return null;
            }
            float size = (float)r.Scale * (1f + 0.01f * (StyleRules.Variant(r.Id, 11) - 5));
            body.transform.localScale = body.transform.localScale * size;
            LifePose pose = VillageLife.At(_life, r, _lifeMinute, _lifeContext);
            body.transform.position = StyleWorld.OnGround(new Vector3((float)pose.X, size, (float)pose.Z));
            body.transform.rotation = Quaternion.Euler(0f, (float)pose.Yaw, 0f);
            return new ResidentBody { Data = r, Body = body.transform, Look = look, Height = size, Yaw = (float)pose.Yaw };
        }

        /// <summary>
        /// Pose un habitant à sa place du moment : s'il en est loin (l'averse vient de tomber, la fête commence), il y marche au pas ; sinon il
        /// suit la place de sa journée. Arrêté et près du joueur, il se tourne vers lui et le salue une fois par passage.
        /// </summary>
        private void PlaceResident(ResidentBody b, LifePose pose, Vector3 player, float deltaTime)
        {
            bool show = !pose.Hidden;
            Vector3 at = b.Body.position;
            float gx = (float)pose.X, gz = (float)pose.Z;
            float dx = gx - at.x, dz = gz - at.z;
            float far = Mathf.Sqrt(dx * dx + dz * dz);
            bool moving = pose.Moving;
            float aim = (float)pose.Yaw;
            if (far > 2.5f && deltaTime > 0f)
            {
                // Rattrapage au pas, par les routes (relecture de la PR #268 : jamais à travers les maisons) : sa journée a changé de place
                // sous ses pieds (l'averse vient de tomber, la fête commence). Trajet refait si sa place a bougé de plus de 3 m.
                if (pose.Hidden) show = true;   // il rentre : on le voit marcher jusqu'à la porte
                if (b.Chase == null || b.ChaseStep >= b.Chase.Count || (b.ChaseToX - pose.X) * (b.ChaseToX - pose.X) + (b.ChaseToZ - pose.Z) * (b.ChaseToZ - pose.Z) > 9.0)
                {
                    b.Chase = _life.Roads.Route(at.x, at.z, pose.X, pose.Z, false);
                    b.ChaseStep = 1;
                    b.ChaseToX = pose.X;
                    b.ChaseToZ = pose.Z;
                }
                float step = WalkSpeed * 1.3f * (float)b.Data.Pace * deltaTime;
                float nx = at.x, nz = at.z;
                while (step > 0f && b.ChaseStep < b.Chase.Count)
                {
                    float tx = (float)b.Chase[b.ChaseStep].X - nx, tz = (float)b.Chase[b.ChaseStep].Z - nz;
                    float left = Mathf.Sqrt(tx * tx + tz * tz);
                    if (left <= step)
                    {
                        nx += tx;
                        nz += tz;
                        step -= left;
                        b.ChaseStep++;
                        continue;
                    }
                    nx += tx / left * step;
                    nz += tz / left * step;
                    aim = (float)StyleRules.FaceYaw(0, 0, tx, tz);
                    step = 0f;
                }
                gx = nx;
                gz = nz;
                moving = true;
            }
            else b.Chase = null;
            if (show != b.Body.gameObject.activeSelf) b.Body.gameObject.SetActive(show);
            if (!show) return;
            b.Body.position = new Vector3(gx, b.Height + StyleWorld.StandHeight(gx, gz), gz);
            float px = player.x - gx, pz = player.z - gz;
            float toPlayer = Mathf.Sqrt(px * px + pz * pz);
            var still = new LifePose(gx, gz, aim, pose.Activity, pose.Place, moving, false);
            if (VillageLife.LooksAtPlayer(still, toPlayer)) aim = (float)StyleRules.FaceYaw(0, 0, px, pz);
            if (deltaTime > 0f) b.Yaw = (float)Locomotion.TurnStep(b.Yaw, aim, moving ? 200.0 : 120.0, 0.15, deltaTime);
            b.Body.rotation = Quaternion.Euler(0f, b.Yaw, 0f);
            VillagerActivity play = moving ? VillagerActivity.Walk : pose.Activity;
            // Salut : une fois par passage (il faut s'éloigner pour qu'il salue de nouveau), d'un geste de la main s'il est libre.
            if (VillageLife.Greets(still, toPlayer, (int)_lifeMinute) && !b.Greeted)
            {
                b.Greeted = true;
                Say(b, VillageLife.Greeting(b.Data, (int)_lifeMinute, _lifeContext, b.Greetings++));
            }
            else if (toPlayer > VillageLife.AttentionRange + 2f) b.Greeted = false;
            if (!moving && b.Greeted && Time.time < b.BubbleUntil && pose.Activity != VillagerActivity.Gather) play = VillagerActivity.Wave;
            if (play != b.Playing && b.Look != null)
            {
                b.Playing = play;
                b.Look.Loop(play, StyleRules.Variant(b.Data.Id, 97));
            }
            if (moving && b.Look != null) b.Look.Pace(WalkSpeed * (float)b.Data.Pace);
        }

        /// <summary>Deux habitants arrêtés côte à côte à une place où l'on bavarde se parlent à tour de rôle (une réplique toutes les 6,5 s).</summary>
        private void Chats()
        {
            for (int i = 0; i < _lifeShown.Count; i++)
                for (int j = i + 1; j < _lifeShown.Count; j++)
                {
                    if (!VillageLife.Chatting(_lifePoses[i], _lifePoses[j])) continue;
                    ResidentBody a = _lifeShown[i], b = _lifeShown[j];
                    if (Time.time < a.ChatAt || Time.time < b.ChatAt) continue;
                    ResidentBody speaker = a.ChatTurn <= b.ChatTurn ? a : b;
                    int turn = a.ChatTurn + b.ChatTurn;
                    Say(speaker, VillageLife.ChatLine(a.Data.Id, b.Data.Id, _lifePoses[i].Place, _lifeContext, turn));
                    speaker.ChatTurn++;
                    a.ChatAt = b.ChatAt = Time.time + ChatSeconds;
                    if (speaker.Look != null && speaker.Playing != VillagerActivity.Talk)
                    {
                        speaker.Playing = VillagerActivity.Talk;
                        speaker.Look.Loop(VillagerActivity.Talk, StyleRules.Variant(speaker.Data.Id, 97));
                    }
                }
        }

        private static void Say(ResidentBody b, string text)
        {
            b.Bubble = "« " + text + " »";   // relecture de la PR #268 : la chaîne est faite une fois, pas à chaque image
            b.BubbleUntil = Time.time + BubbleSeconds;
        }

        /// <summary>V2.58 : les saluts et les répliques au-dessus des habitants proches (comme les noms des PNJ, dans l'interface).</summary>
        public void ResidentBubbles(Camera camera, Vector3 player, HudView hud)
        {
            if (camera == null || hud == null) return;
            for (int i = 0; i < _lifeShown.Count; i++)
            {
                ResidentBody b = _lifeShown[i];
                if (b.Bubble == null || Time.time >= b.BubbleUntil || b.Body == null) continue;
                Vector3 at = b.Body.position;
                float dx = at.x - player.x, dz = at.z - player.z;
                if (dx * dx + dz * dz > 18f * 18f) continue;
                hud.Label(camera.WorldToScreenPoint(at + Vector3.up * (1.15f * b.Height)), b.Bubble, new Color(1f, 0.95f, 0.82f));
            }
        }
    }
}
