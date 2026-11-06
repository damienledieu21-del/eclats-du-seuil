using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Items;
using EdS.Data.Saves;
using EdS.World;

namespace EdS.Runtime
{
    /// <summary>Sous-lot V2.2.1 : silhouette d'un compagnon d'affichage (formes simples faites par le code sous Unity).</summary>
    public enum CompanionShape
    {
        /// <summary>Un chat : corps, tête ronde, oreilles pointues, longue queue.</summary>
        Cat = 0,
    }

    /// <summary>Sous-lot V2.2.1 : ce que fait un compagnon d'affichage à cet instant (Unity en tire la posture).</summary>
    public enum CompanionPose
    {
        /// <summary>Il marche ou trotte derrière le joueur (allure : <see cref="CompanionFollower.Gait"/>).</summary>
        Walk = 0,

        /// <summary>Debout, à l'arrêt, juste avant de s'asseoir.</summary>
        Stand = 1,

        /// <summary>Assis, tourné vers le joueur.</summary>
        Sit = 2,

        /// <summary>Assis, il fait sa toilette (la tête vers la patte).</summary>
        Groom = 3,

        /// <summary>Assis, il regarde autour de lui (la tête tourne).</summary>
        Look = 4,

        /// <summary>Il s'étire (l'avant bas, l'arrière haut), puis se rassoit.</summary>
        Stretch = 5,

        /// <summary>Couché en boule, quand le joueur reste longtemps sans bouger.</summary>
        Curl = 6,
    }

    /// <summary>Sous-lot V2.2.1 : un son à jouer pendant cette image (au plus un).</summary>
    public enum CompanionCue
    {
        None = 0,

        /// <summary>Il vient d'apparaître (l'objet est entré dans le sac) : un petit miaulement.</summary>
        Appear = 1,

        /// <summary>Un miaulement pendant une pause.</summary>
        Meow = 2,

        /// <summary>Il ronronne, assis près du joueur immobile (une fois par pause).</summary>
        Purr = 3,
    }

    /// <summary>
    /// Sous-lot V2.2.1 : un compagnon d'affichage lié à un objet. Tant que l'objet <see cref="ItemId"/> est dans le sac, la bête suit le
    /// joueur à l'écran ; elle ne combat pas, n'est pas sauvegardée et ne change aucun format (la sauvegarde ne connaît que l'objet).
    /// Définition immuable.
    /// </summary>
    public sealed class CompanionDefinition
    {
        public CompanionDefinition(string id, string itemId, string name, CompanionShape shape, StyleTint coat, StyleTint marks, StyleTint eyes,
            double length, double followDistance, double combatDistance, double maxSpeed)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Identifiant vide.", nameof(id));
            if (string.IsNullOrEmpty(itemId)) throw new ArgumentException("Objet vide.", nameof(itemId));
            if (!(length > 0) || !(followDistance > 0) || !(combatDistance >= followDistance) || !(maxSpeed > 0))
                throw new ArgumentOutOfRangeException(nameof(length), "Tailles, distances et vitesse doivent être positives.");
            Id = id;
            ItemId = itemId;
            Name = name ?? string.Empty;
            Shape = shape;
            Coat = coat;
            Marks = marks;
            Eyes = eyes;
            Length = length;
            FollowDistance = followDistance;
            CombatDistance = combatDistance;
            MaxSpeed = maxSpeed;
        }

        public string Id { get; }

        /// <summary>L'objet du sac qui fait venir la bête.</summary>
        public string ItemId { get; }

        public string Name { get; }

        public CompanionShape Shape { get; }

        /// <summary>Couleur du pelage.</summary>
        public StyleTint Coat { get; }

        /// <summary>Couleur des rayures, du ventre et du bout de la queue.</summary>
        public StyleTint Marks { get; }

        /// <summary>Couleur des yeux (ils brillent un peu la nuit).</summary>
        public StyleTint Eyes { get; }

        /// <summary>Longueur du corps (m), sans la queue.</summary>
        public double Length { get; }

        /// <summary>Distance (le long du chemin du joueur) où la bête s'arrête derrière lui (m).</summary>
        public double FollowDistance { get; }

        /// <summary>Distance gardée pendant un combat : elle s'écarte en douceur jusque-là (m).</summary>
        public double CombatDistance { get; }

        /// <summary>Vitesse de course la plus grande (m/s), un peu plus que celle du joueur pour le rattraper.</summary>
        public double MaxSpeed { get; }
    }

    /// <summary>
    /// Sous-lot V2.2.1 : le catalogue des compagnons d'affichage (dans le code, comme le style : aucun format de données nouveau) et le
    /// choix de ceux qui suivent, d'après le sac.
    /// </summary>
    public static class Companions
    {
        /// <summary>Compagnons montrés en même temps au plus (les premiers du catalogue).</summary>
        public const int MaxShown = 3;

        /// <summary>Pain-d'Épice, le chat roux de Rosalie (objet de quête <c>item.chat_de_rosalie</c>, chapitre 1).</summary>
        public static readonly CompanionDefinition PainDEpice = new CompanionDefinition(
            "companion.pain_d_epice", "item.chat_de_rosalie", "Pain-d'Épice", CompanionShape.Cat,
            coat: new StyleTint(0.78, 0.45, 0.2), marks: new StyleTint(0.55, 0.28, 0.1), eyes: new StyleTint(0.55, 0.85, 0.3),
            length: 0.48, followDistance: 1.7, combatDistance: 6.0, maxSpeed: 9.0);

        /// <summary>Tous les compagnons, dans l'ordre où ils se montrent.</summary>
        public static IReadOnlyList<CompanionDefinition> All { get; } = new[] { PainDEpice };

        /// <summary>Le compagnon <paramref name="id"/>, ou null.</summary>
        public static CompanionDefinition? Find(string? id) => All.FirstOrDefault(c => c.Id == id);

        /// <summary>
        /// Compagnons à montrer : ceux dont l'objet est dans le sac (<paramref name="countOf"/> &gt; 0), plus <paramref name="forced"/> (essai
        /// sous Unity, affichage seulement), dans l'ordre du catalogue, <see cref="MaxShown"/> au plus.
        /// </summary>
        public static IReadOnlyList<CompanionDefinition> Shown(Func<string, int> countOf, string? forced = null)
        {
            var into = new List<CompanionDefinition>();
            Shown(All, countOf, forced, into);
            return into;
        }

        /// <summary>Compagnons à montrer d'après le sac (objets portés exclus : un objet de quête n'est jamais porté).</summary>
        public static IReadOnlyList<CompanionDefinition> Shown(Inventory inventory, string? forced = null)
        {
            if (inventory is null) throw new ArgumentNullException(nameof(inventory));
            return Shown(inventory.Count, forced);
        }

        /// <summary>
        /// Relecture de la PR #102 : même choix, sans allocation (appelé à chaque image sous Unity) : <paramref name="into"/> est vidée puis
        /// remplie.
        /// </summary>
        public static void Shown(Inventory inventory, string? forced, List<CompanionDefinition> into)
        {
            if (inventory is null) throw new ArgumentNullException(nameof(inventory));
            if (into is null) throw new ArgumentNullException(nameof(into));
            into.Clear();
            for (int i = 0; i < All.Count && into.Count < MaxShown; i++)
            {
                CompanionDefinition c = All[i];
                if (c.Id == forced || InBag(inventory, c.ItemId)) into.Add(c);
            }
        }

        /// <summary>L'objet est-il au sac ? (boucle simple sur les piles : <see cref="Inventory.Count"/> passe par LINQ.)</summary>
        private static bool InBag(Inventory inventory, string item)
        {
            IReadOnlyList<ItemStack> stacks = inventory.Stacks;
            for (int i = 0; i < stacks.Count; i++)
                if (stacks[i].Item == item && stacks[i].Count > 0) return true;
            return false;
        }

        /// <summary>Choix dans un catalogue donné (tests de la borne <see cref="MaxShown"/>) ; <paramref name="into"/> est vidée puis remplie.</summary>
        public static void Shown(IReadOnlyList<CompanionDefinition> catalog, Func<string, int> countOf, string? forced, List<CompanionDefinition> into)
        {
            if (catalog is null) throw new ArgumentNullException(nameof(catalog));
            if (countOf is null) throw new ArgumentNullException(nameof(countOf));
            if (into is null) throw new ArgumentNullException(nameof(into));
            into.Clear();
            for (int i = 0; i < catalog.Count && into.Count < MaxShown; i++)
            {
                CompanionDefinition c = catalog[i];
                if (c.Id == forced || countOf(c.ItemId) > 0) into.Add(c);
            }
        }

        /// <summary>Erreurs du catalogue (identifiants en double, objet absent du catalogue d'objets) ; vide s'il est juste.</summary>
        public static IReadOnlyList<string> Check(ItemCatalog items)
        {
            if (items is null) throw new ArgumentNullException(nameof(items));
            var errors = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < All.Count; i++)
            {
                CompanionDefinition c = All[i];
                if (!seen.Add(c.Id)) errors.Add($"companions[{i}].id : « {c.Id} » en double.");
                if (!items.TryGetItem(c.ItemId, out _)) errors.Add($"companions[{i}].itemId : objet « {c.ItemId} » inconnu.");
            }
            return errors;
        }
    }

    /// <summary>
    /// Sous-lot V2.2.1 : le mouvement d'un compagnon d'affichage, sans Unity (testé en C#). La bête <b>suit le chemin du joueur</b> (une
    /// traîne de points, bornée) : elle contourne donc les maisons comme lui, sans collision ni physique. Elle s'arrête à
    /// <see cref="CompanionDefinition.FollowDistance"/> derrière lui (en combat, elle s'écarte jusqu'à la distance de combat), accélère et freine en douceur, se tourne sans
    /// à-coup, s'assoit quand le joueur s'arrête, fait sa toilette, regarde autour d'elle, s'étire, miaule, ronronne, puis se couche en boule
    /// si l'arrêt dure. Trop loin (porte, voyage, chargement) : elle reparaît derrière le joueur. Tout est tiré de l'identifiant et du
    /// nombre de pauses (aucune horloge, aucun hasard) : la même suite d'appels donne toujours le même mouvement. Affichage seulement.
    /// </summary>
    public sealed class CompanionFollower
    {
        /// <summary>Au-delà (m, à vol d'oiseau), la bête reparaît derrière le joueur au lieu de courir.</summary>
        public const double TeleportDistance = 14.0;

        /// <summary>Écart entre deux points de la traîne (m).</summary>
        public const double TrailStep = 0.35;

        /// <summary>Points de la traîne au plus (environ 30 m de chemin).</summary>
        public const int MaxTrail = 90;

        /// <summary>Plus grand pas de temps pris en compte (s) : une image très lente ne fait pas sauter la bête.</summary>
        public const double MaxStep = 0.1;

        /// <summary>Accélération (m/s²) et freinage (m/s²).</summary>
        public const double Acceleration = 10.0;

        public const double Braking = 14.0;

        /// <summary>Vitesse de rotation la plus grande (degrés par seconde).</summary>
        public const double TurnRate = 420.0;

        /// <summary>Temps à l'arrêt avant de s'asseoir (s).</summary>
        public const double SitDelay = 0.7;

        /// <summary>Temps assis (joueur immobile) avant de se coucher en boule (s).</summary>
        public const double CurlDelay = 30.0;

        /// <summary>Temps assis avant le ronronnement (s), si le joueur est tout près.</summary>
        public const double PurrDelay = 9.0;

        /// <summary>En combat, elle s'écarte quand elle est plus près que la distance de combat moins cette marge (m).</summary>
        public const double BackMargin = 0.3;

        /// <summary>Vitesse la plus grande quand elle s'écarte d'un combat (m/s) : un trot, pas une fuite.</summary>
        public const double BackSpeed = 3.0;

        /// <summary>Durée de l'apparition et de la disparition (s).</summary>
        public const double FadeSeconds = 0.6;

        private readonly CompanionDefinition _definition;
        private readonly List<(double X, double Z)> _trail = new List<(double, double)>();
        private readonly uint _seed;
        private bool _placed;
        private bool _leaving;
        private double _lastPlayerX;
        private double _lastPlayerZ;
        private double _rest;
        private double _nextAct;
        private double _sinceAct;   // revue Codex de la PR #102 : temps depuis le dernier acte (ou le retour assis), pas PoseSeconds
        private double _actLeft;
        private int _acts;
        private bool _purred;
        private bool _appearPending;

        public CompanionFollower(CompanionDefinition definition)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _seed = Hash(definition.Id);
        }

        public CompanionDefinition Definition => _definition;

        /// <summary>Place au sol (m) et cap (degrés, 0 vers +Z, 90 vers +X, comme Unity).</summary>
        public double X { get; private set; }

        public double Z { get; private set; }

        public double Yaw { get; private set; }

        /// <summary>Vitesse (m/s).</summary>
        public double Speed { get; private set; }

        /// <summary>Allure de 0 (arrêt) à 1 (course la plus rapide).</summary>
        public double Gait => Speed / _definition.MaxSpeed;

        /// <summary>Chemin parcouru (m) : la phase des pattes.</summary>
        public double Stride { get; private set; }

        public CompanionPose Pose { get; private set; } = CompanionPose.Stand;

        /// <summary>Temps passé dans la posture (s).</summary>
        public double PoseSeconds { get; private set; }

        /// <summary>Présence de 0 (invisible) à 1 (là) : la bête grandit en apparaissant et rapetisse en partant.</summary>
        public double Presence { get; private set; }

        /// <summary>Son de cette image.</summary>
        public CompanionCue Cue { get; private set; }

        /// <summary>Le joueur ne bouge plus depuis (s).</summary>
        public double PlayerStill { get; private set; }

        /// <summary>Points de la traîne (pour les tests).</summary>
        public int TrailCount => _trail.Count;

        /// <summary>La bête a fini de partir : elle peut être détruite.</summary>
        public bool Gone => _leaving && Presence <= 0;

        public bool Leaving => _leaving;

        /// <summary>La bête a une place (sinon : à ne pas montrer).</summary>
        public bool Placed => _placed;

        /// <summary>L'objet a quitté le sac : la bête s'en va (elle rapetisse sur place).</summary>
        public void Leave() => _leaving = true;

        /// <summary>L'objet est revenu avant la fin du départ : elle reste.</summary>
        public void Stay() => _leaving = false;

        /// <summary>Elle reparaîtra derrière le joueur à la prochaine image (porte, voyage, chargement).</summary>
        public void Replace() => _placed = false;

        /// <summary>
        /// Une image : joueur en (<paramref name="playerX"/>, <paramref name="playerZ"/>), tourné vers <paramref name="playerYaw"/> (degrés) ;
        /// <paramref name="deltaTime"/> en secondes (0 : pause, rien ne bouge) ; <paramref name="combat"/> : la bête se tient à l'écart.
        /// </summary>
        public void Step(double playerX, double playerZ, double playerYaw, double deltaTime, bool combat)
        {
            Cue = CompanionCue.None;
            if (!IsFinite(playerX) || !IsFinite(playerZ) || !IsFinite(playerYaw) || !IsFinite(deltaTime)) return;
            // Relecture de la PR #102 : la place se prend même en pause (deltaTime 0), pour ne jamais montrer la bête à une place périmée.
            if (!_placed || Distance(X, Z, playerX, playerZ) > TeleportDistance)
            {
                bool first = !_placed && Presence <= 0;
                Place(playerX, playerZ, playerYaw, combat);
                if (first && !_leaving && deltaTime > 0) Cue = CompanionCue.Appear;
                _appearPending = first && !_leaving && deltaTime <= 0;
                return;
            }
            if (deltaTime <= 0) return;
            if (_appearPending)
            {
                _appearPending = false;
                if (!_leaving) Cue = CompanionCue.Appear;   // le miaulement d'arrivée attend la fin de la pause
            }
            double dt = Math.Min(deltaTime, MaxStep);
            Presence = _leaving ? Math.Max(0, Presence - dt / FadeSeconds) : Math.Min(1, Presence + dt / FadeSeconds);
            if (_leaving)
            {
                Speed = 0;
                return;   // elle part sur place (rapetisse), sans courir
            }

            // Le joueur bouge-t-il ? (plus de 0,25 m/s)
            double moved = Distance(playerX, playerZ, _lastPlayerX, _lastPlayerZ);
            PlayerStill = moved / dt > 0.25 ? 0 : PlayerStill + dt;
            _lastPlayerX = playerX;
            _lastPlayerZ = playerZ;

            // La traîne : un point tous les TrailStep mètres du joueur, bornée.
            if (_trail.Count == 0 || Distance(_trail[_trail.Count - 1].X, _trail[_trail.Count - 1].Z, playerX, playerZ) >= TrailStep)
            {
                _trail.Add((playerX, playerZ));
                if (_trail.Count > MaxTrail) _trail.RemoveAt(0);
            }
            // Raccourci : un point plus récent déjà tout près de la bête rend inutiles ceux d'avant (elle ne fait pas demi-tour).
            for (int i = _trail.Count - 1; i > 0; i--)
                if (Distance(X, Z, _trail[i].X, _trail[i].Z) < TrailStep * 0.8)
                {
                    _trail.RemoveRange(0, i);
                    break;
                }

            double keep = combat ? _definition.CombatDistance : _definition.FollowDistance;
            double direct = Distance(X, Z, playerX, playerZ);
            if (combat && direct < keep - BackMargin)
            {
                // Relecture de la PR #102 : un combat commence tout près : elle s'écarte en douceur, droit loin du joueur, jusqu'à la
                // distance de combat (la traîne est oubliée : le chemin d'avant ne compte plus).
                _trail.Clear();
                double wanted = Math.Min(BackSpeed, (keep - direct) * 1.5);
                Speed = Speed < wanted ? Math.Min(wanted, Speed + Acceleration * dt) : Math.Max(wanted, Speed - Braking * dt);
                double ax = direct > 1e-6 ? (X - playerX) / direct : -Math.Sin(playerYaw * Math.PI / 180);
                double az = direct > 1e-6 ? (Z - playerZ) / direct : -Math.Cos(playerYaw * Math.PI / 180);
                double back = Math.Min(Speed * dt, keep - direct);
                X += ax * back;
                Z += az * back;
                Stride += back;
                if (back > 1e-4) TurnTo(Math.Atan2(ax, az) * 180 / Math.PI, dt);
                Postures(dt, back / dt, Distance(X, Z, playerX, playerZ), combat);
                return;
            }
            double remaining = PathLength(playerX, playerZ);
            double desired = remaining > keep ? Math.Min(_definition.MaxSpeed, (remaining - keep) * 2.4) : 0;
            if (desired < 0.15) desired = 0;   // pas de pas de fourmi : elle reste assise pour quelques centimètres
            Speed = Speed < desired ? Math.Min(desired, Speed + Acceleration * dt) : Math.Max(desired, Speed - Braking * dt);

            double startX = X, startZ = Z;
            double budget = Math.Min(Speed * dt, Math.Max(0, remaining - keep));
            while (budget > 1e-9)
            {
                bool toPlayer = _trail.Count == 0;
                double tx = toPlayer ? playerX : _trail[0].X;
                double tz = toPlayer ? playerZ : _trail[0].Z;
                double d = Distance(X, Z, tx, tz);
                if (d <= budget)
                {
                    X = tx;
                    Z = tz;
                    budget -= d;
                    if (toPlayer) break;
                    _trail.RemoveAt(0);
                }
                else
                {
                    X += (tx - X) / d * budget;
                    Z += (tz - Z) / d * budget;
                    budget = 0;
                }
            }
            double step = Distance(startX, startZ, X, Z);
            Stride += step;

            // Le cap : vers où elle va ; à l'arrêt, vers le joueur (sauf quand elle regarde ailleurs ou s'étire).
            if (step > 1e-4) TurnTo(Math.Atan2(X - startX, Z - startZ) * 180 / Math.PI, dt);
            else if (Pose != CompanionPose.Look && Pose != CompanionPose.Curl && Distance(X, Z, playerX, playerZ) > 0.05)
                TurnTo(Math.Atan2(playerX - X, playerZ - Z) * 180 / Math.PI, dt * 0.35);

            Postures(dt, step / dt, Distance(X, Z, playerX, playerZ), combat);
        }

        private void Postures(double dt, double speed, double near, bool combat)
        {
            CompanionPose before = Pose;
            if (speed > 0.05)
            {
                Pose = CompanionPose.Walk;
                _rest = 0;
                _actLeft = 0;
                _purred = false;
            }
            else
            {
                _rest += dt;
                if (Pose == CompanionPose.Walk) Pose = CompanionPose.Stand;
                if (Pose == CompanionPose.Stand && _rest >= SitDelay)
                {
                    Pose = CompanionPose.Sit;
                    _nextAct = 2.5 + Unit(_acts * 2 + 1) * 4.0;   // première petite occupation dans 2,5 à 6,5 s
                    _sinceAct = 0;
                }
                else if (Pose == CompanionPose.Groom || Pose == CompanionPose.Look || Pose == CompanionPose.Stretch)
                {
                    _actLeft -= dt;
                    if (_actLeft <= 0)
                    {
                        Pose = CompanionPose.Sit;
                        _sinceAct = 0;
                    }
                }
                else if (Pose == CompanionPose.Sit)
                {
                    _sinceAct += dt;
                    if (!combat && PlayerStill >= CurlDelay && _rest >= CurlDelay) Pose = CompanionPose.Curl;
                    else if (!_purred && !combat && _rest >= PurrDelay && near <= _definition.FollowDistance + 0.6)
                    {
                        _purred = true;
                        Cue = CompanionCue.Purr;
                    }
                    else if (_sinceAct >= _nextAct) Act(combat);
                }
            }
            PoseSeconds = Pose == before ? PoseSeconds + dt : 0;
        }

        /// <summary>Une petite occupation pendant une pause, tirée du nombre de pauses (toujours la même suite pour une même bête).</summary>
        private void Act(bool combat)
        {
            int roll = (int)(Unit(_acts * 2) * 100);
            _acts++;
            _nextAct = 4.0 + Unit(_acts * 2 + 1) * 5.0;   // la suivante dans 4 à 9 s (comptés depuis le retour assis, ou depuis le miaulement)
            _sinceAct = 0;
            if (combat || roll < 30)
            {
                Pose = CompanionPose.Look;
                _actLeft = 2.2;
            }
            else if (roll < 60)
            {
                Pose = CompanionPose.Groom;
                _actLeft = 3.0;
            }
            else if (roll < 78)
            {
                Pose = CompanionPose.Stretch;
                _actLeft = 1.6;
            }
            else
            {
                Cue = CompanionCue.Meow;   // il miaule assis, tourné vers le joueur
            }
        }

        private void Place(double playerX, double playerZ, double playerYaw, bool combat)
        {
            double rad = playerYaw * Math.PI / 180;
            double back = combat ? _definition.CombatDistance : _definition.FollowDistance;
            // Derrière le joueur, un peu sur le côté (on la voit à côté de lui, pas cachée derrière).
            X = playerX - Math.Sin(rad) * back + Math.Cos(rad) * 0.45;
            Z = playerZ - Math.Cos(rad) * back - Math.Sin(rad) * 0.45;
            Yaw = Normalize(playerYaw);
            Speed = 0;
            _trail.Clear();
            _lastPlayerX = playerX;
            _lastPlayerZ = playerZ;
            _rest = 0;
            _actLeft = 0;
            _purred = false;
            Pose = CompanionPose.Stand;
            PoseSeconds = 0;
            _placed = true;
        }

        private double PathLength(double playerX, double playerZ)
        {
            double length = 0, x = X, z = Z;
            foreach ((double px, double pz) in _trail)
            {
                length += Distance(x, z, px, pz);
                x = px;
                z = pz;
            }
            return length + Distance(x, z, playerX, playerZ);
        }

        private void TurnTo(double target, double dt)
        {
            double delta = Normalize(target - Yaw);
            if (delta > 180) delta -= 360;
            double max = TurnRate * dt;
            Yaw = Normalize(Yaw + Math.Max(-max, Math.Min(max, delta)));
        }

        /// <summary>Nombre de [0, 1[ tiré de l'identifiant de la bête et de <paramref name="n"/> (même suite à chaque partie).</summary>
        private double Unit(int n)
        {
            unchecked
            {
                uint h = _seed ^ ((uint)n * 2654435761u);
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (double)0x1000000;
            }
        }

        /// <summary>FNV-1a sur les caractères (stable d'une exécution à l'autre, contrairement à <c>string.GetHashCode</c>).</summary>
        internal static uint Hash(string text)
        {
            unchecked
            {
                uint h = 2166136261u;
                foreach (char c in text)
                {
                    h ^= c;
                    h *= 16777619u;
                }
                return h;
            }
        }

        private static double Normalize(double degrees)
        {
            double d = degrees % 360;
            return d < 0 ? d + 360 : d;
        }

        private static double Distance(double x0, double z0, double x1, double z1)
        {
            double dx = x1 - x0, dz = z1 - z0;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        private static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
    }
}
