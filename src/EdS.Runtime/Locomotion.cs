using System;

namespace EdS.Runtime
{
    /// <summary>Sous-lot V2.15 (démarche réaliste) : allure montrée d'un corps (arrêt, pas, trot ou course, galop ou sprint).</summary>
    public enum LocomotionGait
    {
        Stand = 0,
        Walk = 1,
        Trot = 2,
        Gallop = 3,
    }

    /// <summary>
    /// Sous-lot V2.15 (retour de Damien du 9 novembre 2026 : « la façon de marcher des personnages et des animaux manque de réalisme ») :
    /// règles pures de la démarche affichée. Cadence des cycles réglée sur la vitesse réelle (nombre de Froude : vitesse naturelle d'une
    /// allure d'après la longueur de jambe), mélange marche ↔ course, démarrages et arrêts lissés, virages à vitesse angulaire bornée avec
    /// penché, pieds posés sur le relief, attentes variées et regards désynchronisés (graine tirée de l'identifiant, aucun aléa). Affichage
    /// seulement : rien ne revient vers le moteur, aucune place déterministe ne change.
    /// </summary>
    public static class Locomotion
    {
        /// <summary>Pesanteur (m/s²).</summary>
        public const double Gravity = 9.81;

        /// <summary>Vitesse (m/s) au-dessus de laquelle un corps arrêté se met en marche.</summary>
        public const double StartSpeed = 0.3;

        /// <summary>Vitesse (m/s) sous laquelle un corps en marche peut s'arrêter (hystérésis : pas de va-et-vient attente/marche).</summary>
        public const double StopSpeed = 0.15;

        /// <summary>Durée (s) sous <see cref="StopSpeed"/> avant l'arrêt : un tick du moteur en retard ne fait pas piétiner.</summary>
        public const double StopDelay = 0.12;

        /// <summary>Longueur de jambe (m) d'un corps de <paramref name="height"/> m : 0,53 de la taille (humain), 0,6 de la hauteur (bête).</summary>
        public static double LegLength(double height, bool quadruped) => Math.Max(0.05, height) * (quadruped ? 0.6 : 0.53);

        /// <summary>Nombre de Froude de chaque allure (v² / g·L) : pas 0,2 (bête 0,25), trot ou petite course 1,0 (humain 1,5), galop ou sprint 3,5.</summary>
        public static double Froude(LocomotionGait gait, bool quadruped)
        {
            switch (gait)
            {
                case LocomotionGait.Walk: return quadruped ? 0.25 : 0.2;
                case LocomotionGait.Trot: return quadruped ? 1.0 : 1.5;
                case LocomotionGait.Gallop: return 3.5;
                default: return 0.0;
            }
        }

        /// <summary>
        /// Vitesse naturelle (m/s) du cycle de <paramref name="gait"/> : celle mesurée dans l'animation (<paramref name="measured"/>, son
        /// déplacement moyen) si elle est plausible (au moins 0,3 m/s), sinon celle du nombre de Froude pour cette taille.
        /// </summary>
        public static double NaturalSpeed(LocomotionGait gait, double height, bool quadruped, double measured = 0.0)
        {
            if (gait == LocomotionGait.Stand) return 0.0;
            if (measured >= 0.3 && measured < 30.0) return measured;
            return Math.Sqrt(Froude(gait, quadruped) * Gravity * LegLength(height, quadruped));
        }

        /// <summary>
        /// Cadence de lecture d'un cycle dont la vitesse naturelle est <paramref name="natural"/> pour avancer à <paramref name="speed"/> :
        /// le rapport des deux, borné (un pied ne glisse plus tant qu'on reste dans les bornes) ; 1 sans vitesse naturelle.
        /// </summary>
        public static double PlaybackRate(double speed, double natural, double min = 0.5, double max = 1.6)
        {
            if (natural <= 0.0 || double.IsNaN(speed)) return 1.0;
            return Clamp(speed / natural, min, max);
        }

        /// <summary>Part de la course (0 à 1) dans le mélange marche ↔ course à <paramref name="speed"/>.</summary>
        public static double RunBlend(double speed, double walkSpeed, double runSpeed)
        {
            if (runSpeed <= walkSpeed) return speed >= runSpeed ? 1.0 : 0.0;
            return Clamp((speed - walkSpeed) / (runSpeed - walkSpeed), 0.0, 1.0);
        }

        /// <summary>
        /// Cadences des deux cycles mêlés (<paramref name="walkLength"/>, <paramref name="runLength"/> : leurs durées) : tous deux font un
        /// pas dans le même temps (durée de cycle commune, mêlée), puis le tout est réglé sur la vitesse (<paramref name="speed"/> contre la
        /// vitesse naturelle du mélange) : les pieds des deux cycles tombent ensemble, sans glisser.
        /// </summary>
        public static (double Walk, double Run) BlendRates(double walkLength, double runLength, double blend, double speed, double walkSpeed,
            double runSpeed, double min = 0.5, double max = 1.6)
        {
            double t = Clamp(blend, 0.0, 1.0);
            double natural = walkSpeed + (runSpeed - walkSpeed) * t;
            double scale = PlaybackRate(speed, natural, min, max);
            if (walkLength <= 0.0 || runLength <= 0.0) return (scale, scale);
            double cycle = walkLength + (runLength - walkLength) * t;
            return (scale * walkLength / cycle, scale * runLength / cycle);
        }

        /// <summary>
        /// Lissage exponentiel indépendant de la cadence d'images, plus vif à la baisse (<paramref name="fall"/>, secondes) qu'à la hausse
        /// (<paramref name="rise"/>) : on démarre en douceur, on s'arrête sans glisser sur place.
        /// </summary>
        public static double Ease(double current, double target, double rise, double fall, double deltaTime)
        {
            if (deltaTime <= 0.0) return current;
            double tau = target >= current ? rise : fall;
            if (tau <= 0.0) return target;
            return target + (current - target) * Math.Exp(-deltaTime / tau);
        }

        /// <summary>Vitesse qui rejoint <paramref name="target"/> avec une accélération et une décélération bornées (m/s²).</summary>
        public static double Accelerate(double current, double target, double accel, double decel, double deltaTime)
        {
            if (deltaTime <= 0.0) return current;
            if (target > current) return Math.Min(target, current + Math.Abs(accel) * deltaTime);
            return Math.Max(target, current - Math.Abs(decel) * deltaTime);
        }

        /// <summary>Vitesse d'approche d'un but à <paramref name="distance"/> m : on ralentit pour s'arrêter dessus (v² = 2·a·d).</summary>
        public static double ArriveSpeed(double cruise, double distance, double decel) =>
            Math.Min(Math.Max(0.0, cruise), Math.Sqrt(2.0 * Math.Abs(decel) * Math.Max(0.0, distance)));

        /// <summary>
        /// Part de la vitesse gardée pendant un virage de <paramref name="angle"/> degrés restant à faire : toute jusqu'à 30°, puis de moins
        /// en moins (un quart au-delà de 120°) : un demi-tour se fait en marchant lentement en arc, jamais en pivotant sur place.
        /// </summary>
        public static double TurnSpeedFactor(double angle)
        {
            double a = Math.Abs(Wrap(angle));
            if (a <= 30.0) return 1.0;
            if (a >= 120.0) return 0.25;
            return 1.0 - 0.75 * (a - 30.0) / 90.0;
        }

        /// <summary>
        /// Vitesse angulaire maximale (degrés par seconde) : un humain pivote vite à l'arrêt (360°/s) et moins en courant (220°/s) ; une
        /// bête tourne lentement (150°/s au pas, 90°/s au galop).
        /// </summary>
        public static double MaxTurnRate(double speed, bool quadruped)
        {
            double s = Clamp(speed / 5.0, 0.0, 1.0);
            return quadruped ? 150.0 - 60.0 * s : 360.0 - 140.0 * s;
        }

        /// <summary>
        /// Lacet suivant vers <paramref name="target"/> : lissé (<paramref name="smooth"/> secondes) et borné à <paramref name="maxRate"/>
        /// degrés par seconde : pas de demi-tour instantané. Rendu dans ]−180, 180].
        /// </summary>
        public static double TurnStep(double yaw, double target, double maxRate, double smooth, double deltaTime)
        {
            if (deltaTime <= 0.0) return Wrap(yaw);
            double delta = Wrap(target - yaw);
            double wanted = smooth > 0.0 ? delta * (1.0 - Math.Exp(-deltaTime / smooth)) : delta;
            double cap = Math.Abs(maxRate) * deltaTime;
            return Wrap(yaw + Clamp(wanted, -cap, cap));
        }

        /// <summary>
        /// Penché dans un virage (degrés, positif vers la droite) : l'angle qui équilibre la force centrifuge (atan(v·ω / g)), borné à
        /// <paramref name="maxLean"/>. <paramref name="yawRate"/> : degrés par seconde, positif vers la droite.
        /// </summary>
        public static double Lean(double yawRate, double speed, double maxLean)
        {
            double omega = yawRate * Math.PI / 180.0;
            double lean = Math.Atan(Math.Max(0.0, speed) * omega / Gravity) * 180.0 / Math.PI;
            return Clamp(lean, -Math.Abs(maxLean), Math.Abs(maxLean));
        }

        /// <summary>
        /// En marche ? Départ au-dessus de <see cref="StartSpeed"/> ; arrêt sous <see cref="StopSpeed"/> tenu <see cref="StopDelay"/>
        /// secondes (<paramref name="stillFor"/>).
        /// </summary>
        public static bool Moving(bool was, double speed, double stillFor)
        {
            if (!was) return speed > StartSpeed;
            return !(speed < StopSpeed && stillFor >= StopDelay);
        }

        /// <summary>Pivote-t-il à l'arrêt (plus de 30°/s sans avancer) ? Il fait alors de petits pas au lieu de glisser sur place.</summary>
        public static bool TurningInPlace(double speed, double yawRate) => speed < StartSpeed && Math.Abs(yawRate) > 30.0;

        /// <summary>
        /// Allure à <paramref name="speed"/> (en marche), avec hystérésis autour des seuils (milieux entre vitesses naturelles, ± 10 %) :
        /// pas, puis trot ou course, puis galop ou sprint s'ils existent (<paramref name="hasTrot"/>, <paramref name="hasGallop"/>).
        /// </summary>
        public static LocomotionGait ChooseGait(LocomotionGait current, double speed, double height, bool quadruped, bool hasTrot, bool hasGallop)
        {
            double walk = NaturalSpeed(LocomotionGait.Walk, height, quadruped);
            double trot = NaturalSpeed(LocomotionGait.Trot, height, quadruped);
            double gallop = NaturalSpeed(LocomotionGait.Gallop, height, quadruped);
            double up1 = 0.5 * (walk + trot) * 1.1, down1 = 0.5 * (walk + trot) * 0.9;
            double up2 = 0.5 * (trot + gallop) * 1.1, down2 = 0.5 * (trot + gallop) * 0.9;
            LocomotionGait gait;
            switch (current)
            {
                case LocomotionGait.Gallop: gait = speed < down2 ? (speed < down1 ? LocomotionGait.Walk : LocomotionGait.Trot) : LocomotionGait.Gallop; break;
                case LocomotionGait.Trot: gait = speed > up2 ? LocomotionGait.Gallop : speed < down1 ? LocomotionGait.Walk : LocomotionGait.Trot; break;
                default: gait = speed > up2 ? LocomotionGait.Gallop : speed > up1 ? LocomotionGait.Trot : LocomotionGait.Walk; break;
            }
            if (gait == LocomotionGait.Gallop && !hasGallop) gait = hasTrot ? LocomotionGait.Trot : LocomotionGait.Walk;
            if (gait == LocomotionGait.Trot && !hasTrot) gait = hasGallop && speed > 0.5 * (walk + gallop) ? LocomotionGait.Gallop : LocomotionGait.Walk;
            return gait;
        }

        /// <summary>Graine d'affichage tirée d'un identifiant (FNV-1a) : deux PNJ n'ont pas la même ; la même à chaque partie.</summary>
        public static uint Seed(string? id)
        {
            uint hash = 2166136261;
            foreach (char c in id ?? string.Empty)
            {
                hash ^= c;
                hash = unchecked(hash * 16777619);
            }
            return hash;
        }

        /// <summary>Mélange entier (graine, rang) → 32 bits, sans état : la même entrée rend toujours la même sortie.</summary>
        public static uint Mix(uint seed, int k)
        {
            unchecked
            {
                uint h = seed ^ ((uint)k * 0x9E3779B9u);
                h ^= h >> 16;
                h *= 0x85EBCA6Bu;
                h ^= h >> 13;
                h *= 0xC2B2AE35u;
                h ^= h >> 16;
                return h;
            }
        }

        /// <summary>Réel dans [0, 1[ tiré de (graine, rang).</summary>
        public static double Unit(uint seed, int k) => (Mix(seed, k) >> 8) / 16777216.0;

        /// <summary>Cadence propre de l'attente (0,88 à 1,12) : deux voisins ne respirent pas ensemble.</summary>
        public static double IdleRate(uint seed) => 0.88 + 0.24 * Unit(seed, 1);

        /// <summary>Instant de départ de l'attente, en part de sa durée (0 à 1).</summary>
        public static double IdlePhase(uint seed) => Unit(seed, 2);

        /// <summary>
        /// Variante d'attente (0 : l'attente ordinaire, la plus fréquente, six fois sur dix) parmi <paramref name="count"/> au temps
        /// <paramref name="time"/> (s), et l'instant où elle change : créneaux de 7 à 13 s propres à chacun, décalés par la graine.
        /// </summary>
        public static (int Variant, double Until) IdleVariant(uint seed, double time, int count)
        {
            double slot = 7.0 + 6.0 * Unit(seed, 3);
            double offset = slot * Unit(seed, 4);
            double s = Math.Max(0.0, time + offset) / slot;
            int k = (int)Math.Min(int.MaxValue - 1, Math.Floor(s));
            double until = (k + 1) * slot - offset;
            if (count <= 1) return (0, until);
            int variant = Unit(seed, 100 + k) < 0.6 ? 0 : 1 + (int)(Mix(seed, 200 + k) % (uint)(count - 1));
            return (variant, until);
        }

        /// <summary>
        /// Regard qui erre (degrés : lacet à ± 35°, tangage à ± 8°) : un but par créneau de 3,5 à 6 s (une fois sur trois droit devant),
        /// rejoint en douceur sur le premier tiers du créneau puis tenu ; continu dans le temps.
        /// </summary>
        public static (double Yaw, double Pitch) Glance(uint seed, double time)
        {
            double slot = 3.5 + 2.5 * Unit(seed, 5);
            double s = Math.Max(0.0, time + slot * Unit(seed, 6)) / slot;
            int k = (int)Math.Min(int.MaxValue - 1, Math.Floor(s));
            double f = s - k;
            double w = SmoothStep(Clamp(f / 0.33, 0.0, 1.0));
            (double y0, double p0) = GlanceAt(seed, k - 1);
            (double y1, double p1) = GlanceAt(seed, k);
            return (y0 + (y1 - y0) * w, p0 + (p1 - p0) * w);
        }

        private static (double Yaw, double Pitch) GlanceAt(uint seed, int k)
        {
            if (Unit(seed, 300 + k) < 0.34) return (0.0, 0.0);
            return (-35.0 + 70.0 * Unit(seed, 400 + k), -8.0 + 16.0 * Unit(seed, 500 + k));
        }

        /// <summary>
        /// Tête tournée vers un point (écart <paramref name="dx"/>, <paramref name="dy"/>, <paramref name="dz"/> depuis la tête) pour un
        /// corps tourné à <paramref name="bodyYaw"/> : lacet relatif borné à ± <paramref name="maxYaw"/>, tangage à ± 25° ; rien (0, 0)
        /// si le point est derrière (au-delà de 110°) : on ne se tord pas le cou.
        /// </summary>
        public static (double Yaw, double Pitch) Watch(double dx, double dy, double dz, double bodyYaw, double maxYaw)
        {
            double flat = Math.Sqrt(dx * dx + dz * dz);
            if (flat < 1e-6) return (0.0, 0.0);
            double yaw = Wrap(Math.Atan2(dx, dz) * 180.0 / Math.PI - bodyYaw);
            if (Math.Abs(yaw) > 110.0) return (0.0, 0.0);
            double pitch = -Math.Atan2(dy, flat) * 180.0 / Math.PI;
            return (Clamp(yaw, -Math.Abs(maxYaw), Math.Abs(maxYaw)), Clamp(pitch, -25.0, 25.0));
        }

        /// <summary>
        /// Montée (m) d'un pied pour se poser sur le sol sous lui (<paramref name="groundAtFoot"/>) quand le corps est posé sur le sol sous
        /// son centre (<paramref name="groundAtBody"/>) ; bornée à ± <paramref name="maxStep"/> (une marche, pas une falaise).
        /// </summary>
        public static double FootLift(double groundAtFoot, double groundAtBody, double maxStep)
        {
            double lift = groundAtFoot - groundAtBody;
            if (double.IsNaN(lift)) return 0.0;
            return Clamp(lift, -Math.Abs(maxStep), Math.Abs(maxStep));
        }

        /// <summary>Descente du bassin (m, négative ou nulle) : assez pour que le pied le plus bas touche son sol, jambes jamais tendues à l'excès.</summary>
        public static double HipDrop(double liftLeft, double liftRight) => Math.Min(0.0, Math.Min(liftLeft, liftRight));

        /// <summary>
        /// Os de la tête d'un squelette (« Head », « mixamorig:Head », « Armature|Head_1 ») ; pas son bout (« HeadTop_End »), ni la mâchoire, les
        /// yeux ou les oreilles.
        /// </summary>
        public static bool IsHeadBone(string? name) => BoneIs(name, "head");

        /// <summary>Os du cou (« Neck », « neck_01 », « mixamorig:Neck »).</summary>
        public static bool IsNeckBone(string? name) => BoneIs(name, "neck");

        private static bool BoneIs(string? name, string word)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.ToLowerInvariant();
            int cut = Math.Max(n.LastIndexOf(':'), n.LastIndexOf('|'));
            if (cut >= 0) n = n.Substring(cut + 1);
            foreach (string bad in new[] { "end", "top", "nub", "jaw", "eye", "ear", "mesh", "geo" })
                if (n.Contains(bad)) return false;
            n = n.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '_', '.', ' ', '-');
            return n.EndsWith(word, StringComparison.Ordinal);
        }

        /// <summary>
        /// Animation qui peut varier l'attente (respirer, regarder autour, changer d'appui, renifler, bras croisés) : ni geste d'un métier ni
        /// objet tenu (torche, lanterne, arme), ni assis, couché ou mort.
        /// </summary>
        public static bool IsIdleVariant(string? name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.ToLowerInvariant();
            foreach (string bad in new[] { "torch", "lantern", "sword", "shield", "seat", "sit", "rail", "talk", "sleep", "lie", "lay", "rest",
                "attack", "death", "die", "hit", "hurt", "walk", "run", "trot", "gallop", "sprint", "eat", "graze", "work", "gather" })
                if (n.Contains(bad)) return false;
            foreach (string good in new[] { "idle", "look", "breath", "sniff", "stand", "fold", "shift" })
                if (n.Contains(good)) return true;
            return false;
        }

        /// <summary>Angle ramené dans ]−180, 180].</summary>
        public static double Wrap(double angle)
        {
            if (double.IsNaN(angle) || double.IsInfinity(angle)) return 0.0;
            double a = angle % 360.0;
            if (a > 180.0) a -= 360.0;
            if (a <= -180.0) a += 360.0;
            return a;
        }

        private static double Clamp(double v, double min, double max) => v < min ? min : v > max ? max : v;

        private static double SmoothStep(double t) => t * t * (3.0 - 2.0 * t);
    }
}
