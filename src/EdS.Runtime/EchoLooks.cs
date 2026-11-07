using System;
using System.Collections.Generic;
using EdS.Data.Zones;
using EdS.World;

namespace EdS.Runtime
{
    /// <summary>
    /// Sous-lot V2.9.1 : la sorte d'écho d'un combattant, pour l'affichage (Unity : <c>EchoGhosts</c>). Règle de Damien du 5 novembre 2026
    /// (MONDE.md) : les bêtes qui reviennent après leur mort sont des échos, comme le loup blanc ; s'y ajoutent les échos de la nuit
    /// (V2.1), ceux des failles et des cicatrices (V2.5, V2.6) et l'Écho de la Rupture (V1.9.8).
    /// </summary>
    public enum EchoKind
    {
        /// <summary>Pas un écho : rendu ordinaire.</summary>
        None = 0,

        /// <summary>Une bête revenue après sa mort (« loup3_v2 », « sanglier_v4 »), un chef déjà tué qu'on recombat : gris-bleu pâle, la plus fragile.</summary>
        Returned = 1,

        /// <summary>Un écho de la nuit (« loup_echo3 ») : bleu froid.</summary>
        Night = 2,

        /// <summary>
        /// Un écho craché par une faille ou rôdant autour d'une cicatrice (« loup_echo1_ev4 », ou toute créature sortie du Seuil, gibier
        /// compris) : violet du Seuil.
        /// </summary>
        Rift = 3,

        /// <summary>Le loup blanc (« le même loup qui revient ») : blanc d'argent, presque solide.</summary>
        WhiteWolf = 4,

        /// <summary>L'Écho de la Rupture et sa seconde forme, les Voix de la Rupture : or pâle de l'aube, le plus solide.</summary>
        Rupture = 5,
    }

    /// <summary>Réglages d'affichage d'une sorte d'écho (couleurs de 0 à 1, sauf l'éclat du contour qui peut dépasser 1).</summary>
    public readonly struct EchoStyle
    {
        public EchoStyle((double R, double G, double B) body, (double R, double G, double B) rim, (double R, double G, double B) ember,
            double solidity, double rimPower, double flicker)
        {
            Body = body;
            Rim = rim;
            Ember = ember;
            Solidity = solidity;
            RimPower = rimPower;
            Flicker = flicker;
        }

        /// <summary>Teinte du corps (prise sur le grain de la texture du modèle).</summary>
        public (double R, double G, double B) Body { get; }

        /// <summary>Contour lumineux (fresnel).</summary>
        public (double R, double G, double B) Rim { get; }

        /// <summary>Braises de la dissolution, lueurs de la traînée.</summary>
        public (double R, double G, double B) Ember { get; }

        /// <summary>Solidité de base, de 0 (invisible) à 1 (opaque).</summary>
        public double Solidity { get; }

        /// <summary>Finesse du contour : plus grand, plus fin.</summary>
        public double RimPower { get; }

        /// <summary>Force du scintillement, de 0 à 1.</summary>
        public double Flicker { get; }
    }

    /// <summary>
    /// Sous-lot V2.9.1 : ce que Unity montre des échos et des failles, en code pur (testé hors de Unity). Affichage seulement : rien ne change
    /// dans le monde ni dans le combat (aucun format touché).
    /// </summary>
    public static class EchoLooks
    {
        /// <summary>Durées (secondes) de la dissolution à l'apparition et à la mort.</summary>
        public const double AppearSeconds = 1.2, VanishSeconds = 1.6;

        /// <summary>Plancher de solidité : un écho reste lisible en combat, même blessé, de jour, loin d'une cicatrice.</summary>
        public const double MinSolidity = 0.35;

        /// <summary>
        /// La sorte d'écho de <paramref name="id"/> (identité du combattant dans le moteur), ou <see cref="EchoKind.None"/>. Décision de Damien
        /// du 7 novembre 2026 (22 h 45, PR #115) : est un écho ce qui revient après sa mort ou ce qui sort du Seuil.
        /// <list type="bullet">
        /// <item><paramref name="fromSeuil"/> : la créature sort d'une faille ou rôde autour d'une cicatrice (créatures de la faille, échos
        /// rôdeurs) : écho du Seuil, quelle qu'elle soit (gibier compris).</item>
        /// <item><paramref name="slainBefore"/> : un chef (ou le Gardien) déjà tué qu'on recombat (son fait de victoire est connu,
        /// <see cref="ChiefSlain"/>) : écho. Un chef contre qui le joueur a perdu n'est jamais mort : pas un écho.</item>
        /// <item>Une vie suivante (« _v2 », réapparition des populations) d'un loup ou d'un sanglier : écho. Le gibier (cerfs, lièvres) qui
        /// revient n'en est pas un (la forêt se repeuple), ni les bandits et les rôdeurs (des hommes). Une forme de chef « _v2 » vient d'une
        /// arrivée refusée (<c>BossFight</c>), pas d'une mort : pas un écho.</item>
        /// </list>
        /// </summary>
        public static EchoKind Of(string? id, bool fromSeuil = false, bool slainBefore = false)
        {
            string key = (id ?? string.Empty).Trim().ToLowerInvariant();
            if (key.Length == 0) return EchoKind.None;
            if (key.StartsWith("echo_de_la_rupture", StringComparison.Ordinal) || key.StartsWith("voix_de_la_rupture", StringComparison.Ordinal))
                return EchoKind.Rupture;
            if (key.StartsWith("loup_blanc", StringComparison.Ordinal)) return EchoKind.WhiteWolf;
            if (key.StartsWith("loup_echo", StringComparison.Ordinal)) return HasSuffix(key, "_ev") ? EchoKind.Rift : EchoKind.Night;
            if (fromSeuil) return EchoKind.Rift;
            if (slainBefore) return EchoKind.Returned;
            if (HasSuffix(key, "_v") && IsBeast(key)) return EchoKind.Returned;
            return EchoKind.None;
        }

        /// <summary>Vrai pour un écho, quelle que soit sa sorte.</summary>
        public static bool IsEcho(string? id, bool fromSeuil = false, bool slainBefore = false) => Of(id, fromSeuil, slainBefore) != EchoKind.None;

        /// <summary>
        /// Vrai si <paramref name="id"/> est une forme d'un chef de <paramref name="zone"/> (ou de son Gardien) dont le fait de victoire est
        /// connu (<paramref name="knows"/>) : il a été tué, et il revient. Une vie « _v2 » d'une forme compte comme la forme.
        /// </summary>
        public static bool ChiefSlain(ZoneDefinition? zone, Func<string, bool>? knows, string? id)
        {
            if (zone == null || knows == null || string.IsNullOrEmpty(id)) return false;
            string key = id!;
            int life = key.LastIndexOf("_v", StringComparison.Ordinal);
            if (life > 0 && HasSuffix(key, "_v")) key = key.Substring(0, life);
            if (zone.Boss is Boss boss && IsForm(boss, key)) return knows(boss.Fact);
            for (int i = 0; i < zone.Chiefs.Count; i++)
                if (IsForm(zone.Chiefs[i], key)) return knows(zone.Chiefs[i].Fact);
            return false;
        }

        private static bool IsForm(Boss chief, string key)
        {
            for (int i = 0; i < chief.Forms.Count; i++)
                if (string.Equals(chief.Forms[i].Id, key, StringComparison.Ordinal)) return true;
            return false;
        }

        // Bêtes de chair qui reviennent en écho (pas Croc-Gris : ses « _v2 » viennent d'une arrivée refusée, voir ChiefSlain).
        private static bool IsBeast(string key) =>
            (key.StartsWith("loup", StringComparison.Ordinal) || key.StartsWith("sanglier", StringComparison.Ordinal));

        /// <summary>Vrai si <paramref name="key"/> finit par <paramref name="marker"/> suivi d'au moins un chiffre (« loup3_v2 », « loup_echo1_ev4 »).</summary>
        private static bool HasSuffix(string key, string marker)
        {
            int at = key.LastIndexOf(marker, StringComparison.Ordinal);
            if (at <= 0 || at + marker.Length >= key.Length) return false;
            for (int i = at + marker.Length; i < key.Length; i++)
                if (key[i] < '0' || key[i] > '9') return false;
            return true;
        }

        /// <summary>Réglages de chaque sorte d'écho (<see cref="EchoKind.None"/> : ceux d'un écho revenu, jamais employés).</summary>
        public static EchoStyle Style(EchoKind kind) => kind switch
        {
            EchoKind.Night => new EchoStyle((0.42, 0.6, 0.95), (0.45, 0.75, 1.6), (0.55, 0.8, 1.0), 0.55, 2.2, 0.45),
            EchoKind.Rift => new EchoStyle((0.5, 0.32, 0.85), (1.1, 0.55, 1.8), (0.85, 0.5, 1.0), 0.55, 2.0, 0.6),
            EchoKind.WhiteWolf => new EchoStyle((0.88, 0.92, 1.0), (1.4, 1.5, 1.8), (0.9, 0.95, 1.0), 0.8, 2.6, 0.25),
            EchoKind.Rupture => new EchoStyle((1.0, 0.88, 0.62), (1.8, 1.5, 0.9), (1.0, 0.8, 0.45), 0.85, 2.4, 0.3),
            _ => new EchoStyle((0.62, 0.74, 0.9), (0.6, 0.8, 1.3), (0.7, 0.85, 1.0), 0.5, 2.4, 0.5),
        };

        /// <summary>
        /// Solidité affichée d'un écho, de <see cref="MinSolidity"/> à 1 : celle de sa sorte, plus forte près d'une cicatrice (+0,08 par
        /// palier : les échos y frappent plus fort) et la nuit (+0,1), moins forte quand il est blessé (au plus 40 % de moins à vie nulle).
        /// <paramref name="health"/> : part de vie de 0 à 1 ; <paramref name="night"/> : de 0 (jour) à 1 (nuit).
        /// </summary>
        public static double Solidity(EchoKind kind, double health, int scarStage, double night)
        {
            double h = Clamp01(health), n = Clamp01(night);
            double s = (Style(kind).Solidity + 0.08 * Math.Max(0, scarStage) + 0.1 * n) * (0.6 + 0.4 * h);
            return Math.Max(MinSolidity, Math.Min(1, s));
        }

        /// <summary>Éclat du contour : plus vif la nuit (jusqu'à deux fois) et près d'une cicatrice (+20 % par palier).</summary>
        public static double Glow(int scarStage, double night) => (1 + Clamp01(night)) * (1 + 0.2 * Math.Max(0, scarStage));

        /// <summary>
        /// Dissolution de 0 (entier) à 1 (dissous) : <paramref name="sinceAppear"/> secondes depuis son apparition, <paramref name="sinceDeath"/>
        /// depuis sa mort (null : vivant). Il se forme en <see cref="AppearSeconds"/>, se défait en <see cref="VanishSeconds"/>.
        /// </summary>
        public static double Dissolve(double sinceAppear, double? sinceDeath)
        {
            if (sinceDeath is double d) return Clamp01(Math.Max(0, d) / VanishSeconds);
            return 1 - Smooth(Clamp01(Math.Max(0, sinceAppear) / AppearSeconds));
        }

        /// <summary>
        /// Palier de la cicatrice la plus forte qui couvre la place (<paramref name="x"/>, <paramref name="z"/>, en mètres), ou 0 hors de toute
        /// cicatrice.
        /// </summary>
        public static int ScarStageAt(IReadOnlyList<ScarView>? scars, double x, double z)
        {
            if (scars == null) return 0;
            int best = 0;
            for (int i = 0; i < scars.Count; i++)
            {
                ScarView s = scars[i];
                double dx = x - s.Position.X.Raw / 10000.0, dz = z - s.Position.Z.Raw / 10000.0, r = s.Radius.Raw / 10000.0;
                if (dx * dx + dz * dz <= r * r && s.Stage > best) best = s.Stage;
            }
            return best;
        }

        /// <summary>Lueurs de traînée par seconde d'un écho qui bouge : discrètes (moitié moins sur téléphone), plus nombreuses la nuit.</summary>
        public static double TrailRate(EchoKind kind, double night, bool mobile)
        {
            if (kind == EchoKind.None) return 0;
            double rate = (kind == EchoKind.Rupture || kind == EchoKind.WhiteWolf ? 5 : 3) * (1 + Clamp01(night));
            return mobile ? rate * 0.5 : rate;
        }

        /// <summary>
        /// Allure de la déchirure d'une faille selon le palier de la cicatrice où elle s'ouvre (0 : sans cicatrice) : taille (1 au palier 0,
        /// +30 % par palier), vitesse du battement de la lumière (par seconde), particules aspirées par seconde (moitié moins sur téléphone),
        /// force de la lumière.
        /// </summary>
        public static (double Scale, double PulseHz, double PullRate, double Light) Rift(int stage, bool mobile)
        {
            int s = Math.Max(0, Math.Min(8, stage));
            double pull = 10 + 6 * s;
            return (1 + 0.3 * s, 0.6 + 0.2 * s, mobile ? pull * 0.5 : pull, 2.2 + 0.6 * s);
        }

        /// <summary>Battement de la lumière d'une faille, de 0,6 à 1,4, à l'instant <paramref name="time"/> (secondes).</summary>
        public static double Pulse(double time, double hz) => 1 + 0.4 * Math.Sin(time * hz * 2 * Math.PI) * (0.7 + 0.3 * Math.Sin(time * 5.3));

        /// <summary>
        /// Ouverture de la déchirure dormante d'une cicatrice (0 à 1), plus large à chaque palier : 0,25 au palier 1, 0,7 au palier 4.
        /// </summary>
        public static double ScarOpening(int stage) => Math.Max(0, Math.Min(1, 0.1 + 0.15 * stage));

        private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;

        private static double Smooth(double t) => t * t * (3 - 2 * t);
    }
}
