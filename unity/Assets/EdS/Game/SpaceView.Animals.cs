using System.Collections.Generic;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V2.9 (2e vague de ressources, demande de Damien du 7 novembre 2026 : « animer le village et les fermes ») : vaches dans les
    /// champs, chevaux au hameau, chiens au bourg et au hameau, corbeaux posés dans les champs et les ruines (ils s'envolent quand on
    /// approche, reviennent une minute plus tard), avec leurs cris en 3D (meuglements, hennissements, aboiements, croassements) ; le coq à
    /// l'aube, les poules et les moutons le jour au hameau, la hulotte la nuit dans la forêt. Ni collision ni combat : décor seulement. Sans
    /// modèle au catalogue, rien n'est posé (les cris de la ferme restent, sans bête à voir). Places fixes, tirées du nom de la zone.
    /// </summary>
    public sealed partial class SpaceView
    {
        private sealed class Beast
        {
            public StyleSlot Slot;
            public Transform Root;
            public Animation Animation;
            public string Idle;
            public string Idle2;
            public string Rest;      // manger, s'asseoir
            public string TakeOff;   // le corbeau
            public string Fly;       // sous-lot V2.11 : le vol du corbeau animé (5e vague)
            public float FlightEnd;
            public float Next;
            public int Step;
            public bool Flown;
            public float Back;
            public Vector3 Home;

            // Demande de Damien du 7 novembre 2026 (19 h 58) : errance d'affichage dans son aire (marche, repos), sans traverser les maisons.
            public string Walk;
            public float MinX, MinZ, MaxX, MaxZ;
            public float Radius;
            public float Speed;
            public float Height;
            public float Lift;
            public float Yaw;
            public bool Moving;

            /// <summary>Sous-lot V2.15 : allure du moment (m/s) : elle part au pas en accélérant, ralentit avant de s'arrêter, tourne en marchant.</summary>
            public float Pace;

            /// <summary>Sous-lot V2.15 : sa tête, qui suit le héros proche ou regarde autour (null sans os de tête).</summary>
            public HeadTurn Head;
            public Vector3 Target;
            public float RestUntil;
            public int Wander;
            public bool Grazer;

            // Revue Codex de la PR #114 (tour 2) : sans clip de marche (vache, chien), les hauts de pattes balancent par le code.
            public Transform[] Legs;
            public bool[] LegFront;
            public bool[] LegLeft;
            public Quaternion[] LegBase;
            public float Stride;
            public bool Stepping;

            /// <summary>Sous-lot V2.9.2 : prochain passage de sabots (cheval, âne qui marche).</summary>
            public float HoofIn;

            /// <summary>Sous-lot V2.8 sur main : trajet par la recherche de chemin (contourne maisons, murs, rochers).</summary>
            public readonly Walker Route = new Walker();
        }

        private readonly List<Beast> _beasts = new List<Beast>();
        private readonly List<(StyleSound Sound, Vector3 At)> _farmCalls = new List<(StyleSound, Vector3)>();
        private float[] _callIn = new float[0];   // relecture de la PR #114 : un compte à rebours par cri noté, autant que la zone en a
        private int _calls;
        private SoundPool _farmVoices;
        private SoundPool _hooves;   // sous-lot V2.9.2 : sabots des chevaux et des ânes
        private float _beastClock;

        private static readonly StyleSound[] FarmSounds =
            { StyleSound.Moo, StyleSound.Neigh, StyleSound.Bark, StyleSound.Caw, StyleSound.Rooster, StyleSound.Hens, StyleSound.Sheep, StyleSound.Owl, StyleSound.Bray };

        /// <summary>Pose les bêtes de la ferme et des champs d'après les lieux de la carte (rien dans une maison ou sans carte).</summary>
        private void Livestock(AdventureMap map)
        {
            if (map == null || map.Indoor) return;
            StyleCatalog catalog = StyleCatalog.Loaded;
            float k = catalog != null ? catalog.Livestock * (Application.isMobilePlatform ? 0.5f : 1f) : 1f;
            _farmVoices = new SoundPool(_root.transform, "Cris de la ferme (V2.9)", 3);
            _hooves = new SoundPool(_root.transform, "Sabots (V2.9.2)", 2);
            foreach (MapPlace place in map.Places)
            {
                string key = StyleRules.Key(place.Id);
                if (key.StartsWith("champs", System.StringComparison.Ordinal))
                {
                    Herd(place, StyleSlot.Cow, Mathf.RoundToInt(3 * k), 1.5f, StyleSound.Moo);
                    Herd(place, StyleSlot.Crow, Mathf.RoundToInt(5 * k), 0.32f, StyleSound.Caw);
                    _farmCalls.Add((StyleSound.Sheep, PlaceCenter(place)));
                }
                else if (key.StartsWith("hameau", System.StringComparison.Ordinal))
                {
                    Herd(place, StyleSlot.Horse, Mathf.RoundToInt(2 * k), 1.75f, StyleSound.Neigh);
                    Herd(place, StyleSlot.Dog, Mathf.RoundToInt(1 * k), 0.55f, StyleSound.Bark);
                    // Sous-lot V2.9.2 (3e vague) : un âne, la basse-cour (cinq poules et le coq) ; leurs cris partent d'eux (le coq et les
                    // poules chantaient jusqu'ici du milieu du hameau, ce qu'ils font encore sans modèle).
                    Herd(place, StyleSlot.Donkey, Mathf.RoundToInt(1 * k), 1.35f, StyleSound.Bray);
                    Herd(place, StyleSlot.Hen, Mathf.RoundToInt(5 * k), 0.42f, StyleSound.Hens);
                    Herd(place, StyleSlot.Rooster, Mathf.RoundToInt(1 * k), 0.55f, StyleSound.Rooster);
                }
                else if (key.StartsWith("bourg", System.StringComparison.Ordinal))
                {
                    Herd(place, StyleSlot.Dog, Mathf.RoundToInt(1 * k), 0.55f, StyleSound.Bark);
                    Herd(place, StyleSlot.Donkey, Mathf.RoundToInt(1 * k), 1.35f, StyleSound.Bray);   // sous-lot V2.9.2 : l'âne du bourg
                    Herd(place, StyleSlot.Hen, Mathf.RoundToInt(3 * k), 0.42f, StyleSound.Hens);
                }
                else if (key.StartsWith("ruines", System.StringComparison.Ordinal)) Herd(place, StyleSlot.Crow, Mathf.RoundToInt(3 * k), 0.32f, StyleSound.Caw);
                else if (key.StartsWith("foret", System.StringComparison.Ordinal)) _farmCalls.Add((StyleSound.Owl, PlaceCenter(place)));
            }
            _callIn = new float[_farmCalls.Count];
            for (int i = 0; i < _callIn.Length; i++) _callIn[i] = 5f + 3f * i;
        }

        private static Vector3 PlaceCenter(MapPlace p) => new Vector3((float)(p.MinX + p.MaxX) * 0.5f, 0f, (float)(p.MinZ + p.MaxZ) * 0.5f);

        /// <summary>
        /// <paramref name="count"/> bêtes de <paramref name="slot"/> dans le lieu, à des places tirées de son nom (hors des obstacles, des
        /// champs cultivés pour les vaches et les chevaux, des routes), de hauteur <paramref name="height"/> ; leur cri est noté.
        /// </summary>
        private void Herd(MapPlace place, StyleSlot slot, int count, float height, StyleSound call)
        {
            // Sous-lot V2.9.2 : sans bête posée (téléphone, Livestock bas), le coq et les poules chantent encore du milieu du hameau, comme avant.
            if (count <= 0)
            {
                if (call == StyleSound.Rooster || call == StyleSound.Hens) _farmCalls.Add((call, PlaceCenter(place)));
                return;
            }
            StyleCatalog catalog = StyleCatalog.Loaded;
            GameObject prefab = catalog != null ? catalog.Model(slot, null) : null;
            uint seed = (uint)StyleRules.Variant(place.Id + slot, 1 << 30) + 7u;
            float Next()
            {
                seed = seed * 1664525u + 1013904223u;
                return (seed >> 8) / 16777216f;
            }
            int placed = 0;
            for (int attempt = 0; attempt < count * 30 && placed < count; attempt++)
            {
                float x = Mathf.Lerp((float)place.MinX + 2f, (float)place.MaxX - 2f, Next());
                float z = Mathf.Lerp((float)place.MinZ + 2f, (float)place.MaxZ - 2f, Next());
                bool grazer = slot == StyleSlot.Cow || slot == StyleSlot.Horse || slot == StyleSlot.Donkey;
                if (InObstacle(x, z, slot == StyleSlot.Crow ? 0.5f : 2f) || (grazer && InField(x, z, 0.5f)) || TownRules.RoadAt(x, z, _roads).Edge < 1.2) continue;
                var at = new Vector3(x, StyleWorld.StandHeight(x, z), z);
                if (placed == 0) _farmCalls.Add((call, at));
                placed++;
                if (prefab == null) continue;
                GameObject look = StyleLook.Fit(prefab, height * (0.92f + 0.16f * Next()), 0f, null, false, out float bottom, out GameObject _, true);
                look.name = slot + " " + placed + " (V2.9)";
                float s = look.transform.localScale.y;
                look.transform.SetParent(_root.transform, false);
                look.transform.position = at + Vector3.up * (-bottom * s);
                look.transform.rotation = Quaternion.Euler(0f, 360f * Next(), 0f);
                float yaw = 360f * Next();
                look.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                var beast = new Beast
                {
                    Slot = slot, Root = look.transform, Animation = look.GetComponentInChildren<Animation>(), Next = 2f + 8f * Next(), Home = look.transform.position,
                    MinX = (float)place.MinX + 1.5f, MinZ = (float)place.MinZ + 1.5f, MaxX = (float)place.MaxX - 1.5f, MaxZ = (float)place.MaxZ - 1.5f,
                    Radius = slot == StyleSlot.Dog ? 12f : slot == StyleSlot.Crow ? 2.5f : slot == StyleSlot.Horse ? 7f : slot == StyleSlot.Hen ? 4f
                        : slot == StyleSlot.Rooster ? 5f : 6f,
                    Speed = slot == StyleSlot.Dog ? 1.8f : slot == StyleSlot.Crow ? 1.0f : slot == StyleSlot.Horse ? 1.1f : slot == StyleSlot.Donkey ? 0.9f
                        : slot == StyleSlot.Hen || slot == StyleSlot.Rooster ? 0.5f : 0.7f,
                    Height = height, Lift = -bottom * s, Yaw = yaw, Grazer = grazer, RestUntil = 3f + 10f * Next(), Wander = placed * 131,
                };
                if (beast.Animation != null)
                {
                    foreach (AnimationState state in beast.Animation)
                    {
                        string name = state.name;
                        int bar = name.LastIndexOf('|');
                        string tail = StyleRules.Key(bar >= 0 ? name.Substring(bar + 1) : name);
                        if (tail == "idle") beast.Idle = name;
                        else if (tail == "idle2") beast.Idle2 = name;
                        else if (tail == "eat" || tail == "sit" || (tail == "lie" && beast.Rest == null)) beast.Rest = name;
                        else if (tail == "takeoff" || tail == "flyup") beast.TakeOff = name;   // V2.11 : « FlyUp » du corbeau animé
                        else if (tail == "fly") beast.Fly = name;
                        else if (tail == "walk") beast.Walk = name;
                    }
                    beast.Idle = beast.Idle ?? beast.Rest ?? beast.TakeOff;
                }
                if (beast.Walk == null && slot != StyleSlot.Crow) Legs(beast, look);
                if (beast.Animation != null)
                {
                    foreach (AnimationState state in beast.Animation) state.wrapMode = state.name == beast.TakeOff ? WrapMode.ClampForever : WrapMode.Loop;
                    // Sous-lot V2.11 : le corbeau animé a une vraie attente (il regarde autour de lui) : elle tourne ; l'ancien, sans elle, garde la
                    // première image de l'envol, figée.
                    if (slot == StyleSlot.Crow && beast.TakeOff != null && beast.Idle == beast.TakeOff)
                    {
                        // Posé : la première image de l'envol, figée.
                        AnimationState perch = beast.Animation[beast.TakeOff];
                        if (perch != null)
                        {
                            beast.Animation.Play(beast.TakeOff);
                            perch.time = 0f;
                            perch.speed = 0f;
                        }
                    }
                    else if (beast.Idle != null)
                    {
                        beast.Animation.Play(beast.Idle);
                        AnimationState idle = beast.Animation[beast.Idle];
                        if (idle != null) idle.time = idle.length * Next();
                    }
                }
                // Sous-lot V2.15 : sa tête suit le héros proche ou regarde autour (graine : son nom et sa place).
                if (slot != StyleSlot.Crow)
                    beast.Head = HeadTurn.Attach(look.transform, look.transform, Locomotion.Seed(look.name) ^ (uint)Mathf.RoundToInt(at.x * 100f + at.z * 7f), true);
                _beasts.Add(beast);
            }
        }

        /// <summary>
        /// À chaque image : les bêtes changent d'attitude de temps en temps (attente, manger, s'asseoir), les corbeaux s'envolent quand
        /// <paramref name="camera"/> (le personnage, à peu près) passe à moins de 7 m, et les cris de la ferme suivent l'heure du monde.
        /// </summary>
        public void Life(Vector3 player, int minute, float deltaTime)
        {
            _farmVoices?.Update();
            _hooves?.Update();
            if (deltaTime <= 0f) return;
            _beastClock += deltaTime;
            for (int i = 0; i < _beasts.Count; i++)
            {
                Beast b = _beasts[i];
                // Revue Codex de la PR #114 (tour 2) : une bête sans composant Animation erre quand même (marche faite par le code).
                if (b.Root == null) continue;
                if (b.Head != null)
                {
                    // Sous-lot V2.15 : à moins de 8 m, la tête suit le héros ; sinon elle regarde autour (moins en marchant).
                    b.Head.HasTarget = (b.Root.position - player).sqrMagnitude < 64f;
                    b.Head.Target = player;
                    b.Head.Glance = b.Moving ? 0.3f : 1f;
                }
                if (b.Slot == StyleSlot.Crow)
                {
                    Crow(b, player);
                    if (b.Flown) continue;
                }
                Roam(b, i, deltaTime);
                Hooves(b, i, player, deltaTime);
                if (b.Moving || b.Slot == StyleSlot.Crow || _beastClock < b.Next) continue;
                b.Step++;
                b.Next = _beastClock + 8f + 10f * (BeastCalls.Pick(b.Step + i * 31, 100) / 100f);
                int pick = BeastCalls.Pick(b.Step + i * 17, 3);
                string clip = pick == 0 && b.Rest != null ? b.Rest : pick == 1 && b.Idle2 != null ? b.Idle2 : b.Idle;
                if (clip != null && b.Animation != null) b.Animation.CrossFade(clip, 0.5f);
            }
            for (int i = 0; i < _farmCalls.Count && i < _callIn.Length; i++)
            {
                _callIn[i] -= deltaTime;
                if (_callIn[i] > 0f) continue;
                (StyleSound sound, Vector3 at) = _farmCalls[i];
                bool time = BeastCalls.CallTime(sound, minute);
                float reach = StyleCatalog.Loaded != null ? StyleCatalog.Loaded.BeastHearing : 60f;
                // Sous-lot V2.9.2 : l'âne sans fichier de braiment prend le hennissement, plus grave.
                bool bray = sound == StyleSound.Bray && (StyleCatalog.Loaded == null || StyleCatalog.Loaded.Sound(StyleSound.Bray, 0) == null);
                if (time && (at - player).sqrMagnitude < reach * reach * 2.25f && _farmVoices != null)
                    _farmVoices.Play(bray ? StyleSound.Neigh : sound, _calls, at, sound == StyleSound.Hens || sound == StyleSound.Sheep ? 5f : 3f,
                        sound == StyleSound.Rooster || sound == StyleSound.Owl ? 0.7f : 0.5f, 1.5f, true, bray ? 0.72f : 1f);
                _callIn[i] = (float)BeastCalls.Interval(sound, _calls++);
            }
        }

        /// <summary>
        /// Sous-lot V2.9.2 (3e vague) : un cheval ou un âne qui marche à moins de 25 m fait claquer ses sabots (un passage de 2,4 s de
        /// l'enregistrement des pavés, toujours un autre, relancé tant qu'il marche).
        /// </summary>
        private void Hooves(Beast b, int index, Vector3 player, float deltaTime)
        {
            if (_hooves == null || (b.Slot != StyleSlot.Horse && b.Slot != StyleSlot.Donkey)) return;
            if (!b.Moving)
            {
                b.HoofIn = 0f;
                return;
            }
            b.HoofIn -= deltaTime;
            if (b.HoofIn > 0f) return;
            b.HoofIn = 2.3f;
            if ((b.Root.position - player).sqrMagnitude > 25f * 25f) return;
            _hooves.Play(StyleSound.Hooves, index * 7 + b.Wander, b.Root.position, 2.4f, b.Slot == StyleSlot.Donkey ? 0.3f : 0.4f, 0.4f, true,
                b.Slot == StyleSlot.Donkey ? 1.12f : 1f);
        }

        /// <summary>
        /// Errance d'une bête de la ferme : au repos jusqu'à <see cref="Beast.RestUntil"/>, puis un but autour de sa place
        /// (<see cref="BeastCalls.WanderTarget"/>), pris seulement s'il est dans son aire, hors des maisons et des obstacles (le chemin en
        /// ligne droite est sondé tous les mètres), hors des champs cultivés pour les vaches et les chevaux ; sinon un autre, puis le repos.
        /// Elle y marche (clip de marche s'il existe, sinon un pas balancé par le code), se tourne en douceur, puis se repose. Sans allocation.
        /// </summary>
        private void Roam(Beast b, int index, float deltaTime)
        {
            if (b.Root == null) return;
            Vector3 at = b.Root.position;
            if (!b.Moving)
            {
                if (_beastClock < b.RestUntil) return;
                for (int k = 0; k < 4 && !b.Moving; k++)
                {
                    (double tx, double tz) = BeastCalls.WanderTarget(b.Wander++ + index * 977, b.Home.x, b.Home.z, b.Radius);
                    if (Reachable(b, at.x, at.z, (float)tx, (float)tz))
                    {
                        b.Target = new Vector3((float)tx, 0f, (float)tz);
                        b.Moving = true;
                        b.Route.Reset();
                    }
                }
                if (!b.Moving)
                {
                    b.RestUntil = _beastClock + 4f;
                    return;
                }
                if (b.Walk != null && b.Animation != null) b.Animation.CrossFade(b.Walk, 0.3f);
                else if (b.Legs != null && b.Legs.Length > 0)
                {
                    // Marche faite par le code : l'animation d'attente est arrêtée (elle réécrirait les pattes), la pose du moment sert de base.
                    if (b.Animation != null) b.Animation.Stop();
                    for (int l = 0; l < b.Legs.Length; l++) b.LegBase[l] = b.Legs[l].localRotation;
                    b.Stepping = true;
                }
            }
            // Sous-lot V2.8 (main) : le point de passage suivant du trajet trouvé par la recherche de chemin (le trajet n'est calculé qu'au
            // changement de but) ; sans chemin, la bête renonce et se repose.
            (double wx, double wz) = b.Route.Next(Paths, at.x, at.z, b.Target.x, b.Target.z);
            if (b.Route.Blocked)
            {
                Settle(b, index);
                return;
            }
            // Sous-lot V2.15 : allure lissée (départ au pas qui accélère, ralenti avant le but), presque à l'arrêt tant qu'elle n'est pas
            // tournée vers où elle va (une bête ne pivote pas sur place, elle tourne en marchant), virage borné et lissé.
            float aim = (wx - at.x) * (wx - at.x) + (wz - at.z) * (wz - at.z) > 1e-8 ? (float)StyleRules.FaceYaw(at.x, at.z, wx, wz) : b.Yaw;
            double factor = Locomotion.TurnSpeedFactor(aim - b.Yaw);
            float left = Mathf.Sqrt((b.Target.x - at.x) * (b.Target.x - at.x) + (b.Target.z - at.z) * (b.Target.z - at.z));
            double wanted = System.Math.Max(Locomotion.ArriveSpeed(b.Speed * factor * factor, left, 0.8), System.Math.Min(0.1, b.Speed));
            b.Pace = (float)Locomotion.Accelerate(b.Pace, wanted, 0.7, 1.2, deltaTime);
            (double x, double z, double _, bool reached) = BeastCalls.Step(at.x, at.z, wx, wz, b.Pace, deltaTime);
            bool arrived = reached && Mathf.Abs((float)wx - b.Target.x) < 0.06f && Mathf.Abs((float)wz - b.Target.z) < 0.06f;
            float moved = Mathf.Sqrt(((float)x - at.x) * ((float)x - at.x) + ((float)z - at.z) * ((float)z - at.z));
            b.Yaw = (float)Locomotion.TurnStep(b.Yaw, aim, Locomotion.MaxTurnRate(b.Pace, true), 0.2, deltaTime);
            // Sous-lot V2.15 : la cadence du clip de marche suit l'allure (plus de sabots qui glissent).
            if (b.Walk != null && b.Animation != null && b.Slot != StyleSlot.Crow)
            {
                AnimationState walking = b.Animation[b.Walk];
                if (walking != null)
                    walking.speed = (float)Locomotion.PlaybackRate(b.Pace, Locomotion.NaturalSpeed(LocomotionGait.Walk, b.Height, true), 0.35, 1.6);
            }
            float px = (float)x, pz = (float)z, bob = 0f, pitch = 0f, roll = 0f, sway = 0f;
            if (b.Slot == StyleSlot.Crow) bob = 0.12f * Mathf.Abs(Mathf.Sin(_beastClock * 9f));   // le corbeau sautille
            else if (b.Walk == null)
            {
                // Pas de clip de marche (vache, chien) : le corps se balance au pas, comme le sanglier sans squelette.
                (double gb, double gp, double gr, double gy) = BeastCalls.StaticGait(b.Pace, _beastClock, null);
                bob = (float)gb * b.Height;
                pitch = (float)gp;
                roll = (float)gr;
                sway = (float)gy;
            }
            if (b.Stepping)
            {
                b.Stride += moved;
                // Les pattes ont leur propre pas : le corps ne fait plus qu'un léger tangage et un petit rebond.
                roll *= 0.4f;
                sway *= 0.4f;
                Vector3 right = b.Root.right;
                for (int l = 0; l < b.Legs.Length; l++)
                {
                    Transform leg = b.Legs[l];
                    if (leg == null) continue;
                    leg.localRotation = b.LegBase[l];
                    leg.rotation = Quaternion.AngleAxis((float)BeastCalls.LegSwing(b.Stride, 1.1f * b.Height, b.LegFront[l], b.LegLeft[l]), right) * leg.rotation;
                }
            }
            if (arrived)
            {
                Settle(b, index);
                bob = pitch = roll = sway = 0f;
            }
            b.Root.position = new Vector3(px, StyleWorld.StandHeight(px, pz) + b.Lift + bob, pz);
            b.Root.rotation = Quaternion.Euler(pitch, b.Yaw + sway, roll);
        }

        /// <summary>
        /// Revue Codex de la PR #114 (tour 2) : hauts de pattes du squelette (<see cref="BeastCalls.LegOf"/>), quatre au plus ; aucun si le
        /// modèle n'a pas d'os reconnus (le corps seul se balance alors).
        /// </summary>
        private static void Legs(Beast b, GameObject look)
        {
            var legs = new List<Transform>();
            var front = new List<bool>();
            var left = new List<bool>();
            foreach (Transform t in look.GetComponentsInChildren<Transform>(true))
            {
                if (legs.Count >= 4) break;
                (bool Front, bool Left)? leg = BeastCalls.LegOf(t.name);
                if (leg == null) continue;
                legs.Add(t);
                front.Add(leg.Value.Front);
                left.Add(leg.Value.Left);
            }
            b.Legs = legs.ToArray();
            b.LegFront = front.ToArray();
            b.LegLeft = left.ToArray();
            b.LegBase = new Quaternion[legs.Count];
        }

        /// <summary>Fin d'une marche (arrivée ou chemin introuvable) : pattes et attente rendues, repos de 6 à 16 s.</summary>
        private void Settle(Beast b, int index)
        {
            b.Moving = false;
            b.Pace = 0f;
            if (b.Stepping)
            {
                for (int l = 0; l < b.Legs.Length; l++)
                    if (b.Legs[l] != null) b.Legs[l].localRotation = b.LegBase[l];
                b.Stepping = false;
            }
            b.RestUntil = _beastClock + (float)BeastCalls.RestSeconds(b.Wander + index);
            b.Next = _beastClock;   // une attitude de repos tout de suite
            if (b.Animation != null && b.Idle != null && b.Slot != StyleSlot.Crow) b.Animation.CrossFade(b.Idle, 0.4f);
        }

        /// <summary>
        /// Le but convient-il : dans l'aire de la bête, hors des maisons et des obstacles, hors des champs cultivés pour les vaches et les
        /// chevaux, hors des routes (sauf le chien) ? Le chemin lui-même vient de la recherche de chemin (sous-lot V2.8 sur main), qui
        /// contourne maisons, murs et rochers ; une ligne droite qui couperait un champ reste refusée aux vaches et aux chevaux.
        /// </summary>
        private bool Reachable(Beast b, float x0, float z0, float x1, float z1)
        {
            if (x1 < b.MinX || x1 > b.MaxX || z1 < b.MinZ || z1 > b.MaxZ) return false;
            float margin = b.Slot == StyleSlot.Crow ? 0.4f : 1.2f;
            if (InObstacle(x1, z1, margin)) return false;
            if (b.Slot != StyleSlot.Dog && TownRules.RoadAt(x1, z1, _roads).Edge < 0.8) return false;
            if (!b.Grazer) return true;
            float dx = x1 - x0, dz = z1 - z0, length = Mathf.Sqrt(dx * dx + dz * dz);
            int steps = Mathf.Max(1, Mathf.CeilToInt(length));
            for (int i = 1; i <= steps; i++)
                if (InField(x0 + dx * i / steps, z0 + dz * i / steps, 0.3f)) return false;
            return true;
        }

        private void Crow(Beast b, Vector3 player)
        {
            if (b.TakeOff == null) return;
            AnimationState state = b.Animation[b.TakeOff];
            if (state == null) return;
            if (!b.Flown)
            {
                Vector3 d = b.Root.position - player;
                d.y = 0f;
                if (d.sqrMagnitude > 49f) return;
                b.Flown = true;
                b.Back = _beastClock + 60f;
                state.time = 0f;
                state.speed = 1f;
                // Sous-lot V2.11 : le corbeau animé décolle (« FlyUp »), puis vole (« Fly ») trois secondes avant de disparaître.
                b.FlightEnd = _beastClock + (b.Fly != null ? Mathf.Max(3f, state.length) : state.length);
                if (b.Idle != b.TakeOff)
                {
                    b.Animation.Play(b.TakeOff);
                    if (b.Fly != null) b.Animation.CrossFadeQueued(b.Fly, 0.2f);
                }
                if (_farmVoices != null) _farmVoices.Play(StyleSound.Caw, _calls++, b.Root.position, 2.5f, 0.5f, 1f);
                return;
            }
            // Envolé : il monte et s'éloigne, puis disparaît à la fin de l'envol ; il revient se poser une minute plus tard.
            if (_beastClock < b.FlightEnd) b.Root.position += (b.Root.forward * 2.5f + Vector3.up * 1.2f) * Time.deltaTime;
            else if (b.Root.gameObject.activeSelf) b.Root.gameObject.SetActive(false);
            if (_beastClock < b.Back) return;
            b.Flown = false;
            b.Moving = false;
            b.Root.position = b.Home;
            b.Root.gameObject.SetActive(true);
            state.time = 0f;
            state.speed = 0f;
            if (b.Idle != null && b.Idle != b.TakeOff) b.Animation.Play(b.Idle);
        }

        /// <summary>
        /// Sous-lot V2.9 (2e vague) : le bruit d'un pas en (<paramref name="x"/>, <paramref name="z"/>) : plancher dans une maison, gravier
        /// sur une route ou un sol nu, feuilles en forêt (<paramref name="forest"/>), herbe ailleurs (<see cref="StyleRules.StepSound"/>).
        /// </summary>
        public StyleSound StepSound(float x, float z, bool forest)
        {
            if (_indoor) return StyleSound.StepWood;
            RoadHit road = TownRules.RoadAt(x, z, _roads);
            double grass = TownRules.RoadMix(_spots.Mix(x, z), x, z, road).Grass;
            return StyleRules.StepSound(false, road.Edge, grass, forest);
        }
    }
}
