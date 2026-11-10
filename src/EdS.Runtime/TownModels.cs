using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Space;

namespace EdS.Runtime
{
    /// <summary>
    /// Essai réaliste 8 : un modèle de bâtiment posé sur l'emprise d'un obstacle : rotation (degrés, autour de la verticale, la face du
    /// modèle étant vers +z avant rotation), échelles le long des axes du monde (x, z) et de la hauteur, part de l'emprise couverte.
    /// </summary>
    public readonly struct ModelFit
    {
        public ModelFit(double yaw, double scaleX, double scaleZ, double fill, double cover, bool fits)
        {
            Yaw = yaw;
            ScaleX = scaleX;
            ScaleZ = scaleZ;
            ScaleY = Math.Sqrt(scaleX * scaleZ);
            Fill = fill;
            Cover = cover;
            Fits = fits;
        }

        /// <summary>Rotation (degrés) qui tourne la face du modèle (+z) vers la route.</summary>
        public double Yaw { get; }

        /// <summary>Échelle le long de l'axe x du monde (après rotation).</summary>
        public double ScaleX { get; }

        /// <summary>Échelle le long de l'axe z du monde (après rotation).</summary>
        public double ScaleZ { get; }

        /// <summary>Échelle de la hauteur : moyenne géométrique des deux autres.</summary>
        public double ScaleY { get; }

        /// <summary>Part de l'emprise couverte par le pied du modèle (1 : toute ; plus de 1 : il déborde, avant-toits compris).</summary>
        public double Fill { get; }

        /// <summary>
        /// Relecture indépendante de l'essai 8 : part de l'emprise couverte le long de son axe le moins couvert (1 : jusqu'au bord ; pour le
        /// puits et la tour, voulus plus petits que leur emprise, jusqu'à leur taille voulue). Une pose peut couvrir 70 % de la surface en
        /// laissant plus d'un mètre de vide d'un côté : le joueur y buterait contre un mur invisible.
        /// </summary>
        public double Cover { get; }

        /// <summary>
        /// Le modèle tient-il sur l'emprise sans être trop réduit, trop grossi ou trop déformé, et jusqu'à ses bords
        /// (<see cref="TownModels.MinCover"/>) ?
        /// </summary>
        public bool Fits { get; }
    }

    /// <summary>Ce que fait un villageois du bourg (essai réaliste 8) : il attend, parle, regarde autour de lui, cueille ou marche.</summary>
    public enum VillagerActivity
    {
        Idle = 0,
        Talk = 1,
        Look = 2,
        Gather = 3,
        Walk = 4,
    }

    /// <summary>
    /// Essai réaliste 8 : un villageois d'affichage (le moteur ne le connaît pas, il n'arrête rien) : personnage du catalogue
    /// (<see cref="Character"/> : dossier de Mixamo), activité, place, orientation (degrés) ; un marcheur va et vient jusqu'à
    /// (<see cref="ToX"/>, <see cref="ToZ"/>).
    /// </summary>
    public readonly struct StyleVillager
    {
        public StyleVillager(string id, string character, VillagerActivity activity, double x, double z, double yaw, double toX = double.NaN,
            double toZ = double.NaN)
        {
            Id = id;
            Character = character;
            Activity = activity;
            X = x;
            Z = z;
            Yaw = yaw;
            ToX = toX;
            ToZ = toZ;
        }

        public string Id { get; }
        public string Character { get; }
        public VillagerActivity Activity { get; }
        public double X { get; }
        public double Z { get; }
        public double Yaw { get; }
        public double ToX { get; }
        public double ToZ { get; }

        /// <summary>Un marcheur (avec une seconde extrémité) ?</summary>
        public bool Walks => Activity == VillagerActivity.Walk && !double.IsNaN(ToX) && !double.IsNaN(ToZ);
    }

    /// <summary>
    /// Essai réaliste 8 (demande de Damien du 28 octobre 2026 : « je veux que tu ajoutes les points 1 à 5 » : de vrais bâtiments en 3D et
    /// des villageois dans le bourg) : règles sans Unity pour poser les modèles de bâtiments de Sketchfab sur les emprises des obstacles
    /// (genre d'après le dossier, choix stable d'un modèle qui tient, rotation vers la route) et placer les villageois d'affichage. Le
    /// moteur ne change pas : mêmes obstacles, mêmes emprises.
    /// </summary>
    public static class TownModels
    {
        /// <summary>Échelle la plus petite admise (en dessous, les portes deviennent plus petites que les personnages).</summary>
        public const double MinScale = 0.62;

        /// <summary>Échelle la plus grande admise.</summary>
        public const double MaxScale = 1.4;

        /// <summary>Écart admis entre les échelles des deux axes du sol (un modèle étiré d'au plus 25 %).</summary>
        public const double MaxStretch = 1.25;

        /// <summary>Part de l'emprise qu'un modèle doit couvrir au moins.</summary>
        public const double MinFill = 0.7;

        /// <summary>
        /// Couverture minimale de l'emprise le long de chaque axe pour qu'une pose tienne (relecture indépendante de l'essai 8 : l'auberge,
        /// porte vers la route, laissait 1,4 m de vide de chaque côté, où le joueur butait contre l'obstacle).
        /// </summary>
        public const double MinCover = 0.85;

        /// <summary>Distance (m) gardée entre un villageois et un obstacle.</summary>
        public const double VillagerMargin = 0.6;

        /// <summary>Distance (m) gardée entre un villageois et un repère (PNJ, point sûr, atelier, nœud de récolte).</summary>
        public const double ClearingMargin = 2.5;

        /// <summary>Distance (m) gardée entre deux villageois.</summary>
        public const double VillagerSpacing = 1.2;

        /// <summary>
        /// Genre de bâtiment d'un modèle rangé dans « …/Batiments/&lt;Genre&gt;/&lt;Modèle&gt;/fichier » (Maison, Auberge, Chapelle,
        /// Grange, Puits, Etal, Tour, Tente) ; <see cref="StyleBuilding.None"/> ailleurs.
        /// </summary>
        public static StyleBuilding KindOfModel(string path)
        {
            // V2.19 : un modèle de l'atelier (« …/Modeles/Batiments/auberge/… ») est dédié à son obstacle, pas un genre de Sketchfab.
            if (OwnBuildingOf(path) != null) return StyleBuilding.None;
            string[] parts = (path ?? string.Empty).Replace('\\', '/').Split('/').Select(StyleRules.Key).ToArray();
            for (int i = 0; i + 2 < parts.Length; i++)
            {
                if (parts[i] != "batiments" && parts[i] != "buildings") continue;
                switch (parts[i + 1])
                {
                    case "maison": case "maisons": return StyleBuilding.House;
                    case "auberge": return StyleBuilding.Inn;
                    case "chapelle": return StyleBuilding.Chapel;
                    case "grange": return StyleBuilding.Barn;
                    case "puits": return StyleBuilding.Well;
                    case "etal": case "etals": return StyleBuilding.Stall;
                    case "tour": return StyleBuilding.Tower;
                    case "tente": case "tentes": return StyleBuilding.Tent;
                }
            }
            return StyleBuilding.None;
        }

        /// <summary>
        /// V2.17 (décision de Damien du 9 novembre 2026 : « créer nous-mêmes les modèles 3D ») : identifiant de l'obstacle auquel est
        /// dédié un modèle de l'atelier rangé dans « …/Modeles/Batiments/&lt;id&gt;/&lt;fichier&gt;.fbx » (casse et « \ » indifférents) ;
        /// null ailleurs. L'identifiant est rendu en minuscules, tel qu'écrit dans le chemin.
        /// </summary>
        public static string? OwnBuildingOf(string? path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string[] parts = path!.Replace('\\', '/').Split('/');
            int n = parts.Length;
            if (n < 4) return null;
            string file = parts[n - 1], id = parts[n - 2];
            if (!string.Equals(parts[n - 3], "Batiments", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(parts[n - 4], "Modeles", StringComparison.OrdinalIgnoreCase)) return null;
            if (id.Length == 0 || file.Length <= 4 || !file.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) return null;
            return id.ToLowerInvariant();
        }

        /// <summary>
        /// V2.17 : haut de la cheminée d'un modèle de l'atelier (m, repère du modèle dans Unity : façade vers +z, origine du FBX), là où
        /// sort la fumée du fournil ; null si le modèle n'a pas de cheminée connue (alors pas de fumée faite par le code sur lui).
        /// </summary>
        public static (double X, double Y, double Z)? OwnChimney(string? model)
        {
            switch (StyleRules.Key(model ?? string.Empty))
            {
                // V2.18, modèle validé par Damien le 10 novembre 2026 (PLAN_COORDONNEES.md, cheminée C1 du fournil : X 11,75 – 12,55,
                // Y 7,10 – 8,00 dans Blender, mitre à 7,45 m). Exporté recentré sur l'emprise des murs (X − 7,25, Y − 5,5), façade
                // (Y = 0, regardant −Y dans Blender) vers +z. Unity lit le FBX (droitier) en gaucher en changeant le signe de x : la
                // droite de la façade vue de dehors (+X de Blender) est −x. Donc x = 7,25 − X, y = Z, z = 5,5 − Y.
                case "boulangeriefirmin": return (-4.9, 7.45, -2.05);
                // V2.19 : auberge des Vallons (PLAN_COORDONNEES.md, cheminée C2 de la cuisine, la seule qui fume : centre X 7,40, Y 6,00,
                // sommet 13,45). Exportée recentrée sur le pied des murs, corps et abri (X 0 – 11,50, Y 0 – 8,00) : X − 5,75, Y − 4,00.
                case "aubergevallons": return (5.75 - 7.40, 13.45, 4.0 - 6.0);
                // V2.21 : épicerie des Vallons (PLAN_COORDONNEES.md, cheminée unique : centre X 8,90, Y 4,70, sommet du mitron 11,55),
                // feu d'âtre pendant les heures d'ouverture (décision de Damien du 10 novembre 2026 au soir). Exportée recentrée sur le
                // pied des murs (X 0 – 10,80, Y 0 – 9,40) : X − 5,40, Y − 4,70.
                case "epicerievallons": return (5.40 - 8.90, 11.55, 4.70 - 4.70);
                // V2.22 : herboristerie de Sauge (fiche : cheminée à X 2,25, Y 4,20, sommet Z 6,05), feu de tisanes permanent (décision de
                // Damien du 10 novembre 2026 au soir). Exportée recentrée sur le pied des murs (X 0 – 4,50, Y 0 – 5,20) : X − 2,25, Y − 2,60.
                case "herboristeriesauge": return (2.25 - 2.25, 6.05, 2.60 - 4.20);
                // V2.22 : la mercerie (cheminée à X 3,20, Y 4,50, sommet 7,00) et la Guilde (deux souches, sommet 12,40) n'ont aucun feu
                // tenu dans les données (ni fournée, ni cuisine, ni âtre, ni heures pour la Guilde) : pas de cheminée connue, pas de fumée.
                default: return null;
            }
        }

        /// <summary>
        /// V2.18 : lumière de la lanterne de porte d'un modèle de l'atelier (m, même repère que <see cref="OwnChimney"/>) ; null si le
        /// modèle n'en a pas de connue (alors <see cref="OwnDoorLantern"/> la place d'après la porte et l'emprise).
        /// </summary>
        public static (double X, double Y, double Z)? OwnLantern(string? model)
        {
            switch (StyleRules.Key(model ?? string.Empty))
            {
                // V2.18 : lanterne de l'entrée à droite de la porte P1 (porte centrée à X = 7,25 ; lanterne à X = 8,55, flamme vers 1,94 m,
                // 0,40 m devant la façade dans Blender ; mêmes conversions que la cheminée).
                case "boulangeriefirmin": return (-1.3, 1.94, 5.9);
                // V2.19 : lanterne de l'auberge au-dessus de la porte P1 (Prop_Lanterne_Porte : X 5,75, console à Z 3,55, corps de la
                // lanterne vers Z 3,25 et Y −0,55, devant l'étage en encorbellement ; mêmes conversions que la cheminée).
                case "aubergevallons": return (0.0, 3.25, 4.0 + 0.55);
                // V2.21 : lanterne de l'épicerie sous l'auvent, à droite de la porte (décision de Damien du 10 novembre 2026 au soir ;
                // Prop_Lanterne : console à X 6,75, lumière à Z 1,99 et Y −0,30 ; mêmes conversions : X − 5,40, Y − 4,70).
                case "epicerievallons": return (5.40 - 6.75, 1.99, 4.70 + 0.30);
                // V2.21 : lanterne de l'herboristerie de Sauge, à droite de la porte (Prop_Lanterne : X 3,30, Y −0,22, lumière à Z 1,925).
                // Exportée recentrée sur le pied des murs (X 0 – 4,50, Y 0 – 5,20) : X − 2,25, Y − 2,60.
                case "herboristeriesauge": return (2.25 - 3.30, 1.925, 2.60 + 0.22);
                // V2.22 : lanterne murale de la mercerie, entre la porte P1 (centre X 3,20) et la vitrine des rubans (Lumiere_Lanterne :
                // X 4,00, Y −0,22, Z 1,925). Exportée recentrée sur le pied des murs (X 0 – 6,40, Y 0 – 5,80) : X − 3,20, Y − 2,90.
                case "mercerievallons": return (3.20 - 4.00, 1.925, 2.90 + 0.22);
                // V2.22 : la Guilde n'a pas de lanterne (fiche : le blason et les bannières sont ses seuls accessoires) : OwnDoorLantern.
                default: return null;
            }
        }

        /// <summary>
        /// V2.21 : vrai si la lanterne connue du modèle (<see cref="OwnLantern"/>) reste allumée jour et nuit (décision de Damien du
        /// 10 novembre 2026 : Sauge, l'herboriste, reste ouverte jour et nuit et sa lanterne reste allumée) ; les autres s'allument au
        /// crépuscule et s'éteignent à l'aube.
        /// </summary>
        public static bool OwnLanternAlwaysLit(string? model) => StyleRules.Key(model ?? string.Empty) == "herboristeriesauge";

        /// <summary>
        /// V2.21 : force (0 à 1) de la lumière d'une lanterne quand celle des lanternes du monde est <paramref name="lamps"/>
        /// (<c>StyleWorld.Lamps</c> : 0 le jour, 1 la nuit) : 1 pour une lanterne toujours allumée, sinon <paramref name="lamps"/>.
        /// </summary>
        public static double LanternLevel(bool alwaysLit, double lamps) => alwaysLit ? 1.0 : Math.Max(0.0, Math.Min(1.0, lamps));

        /// <summary>
        /// V2.22 (relecture de la PR #140) : ordre des lanternes qui reçoivent les <paramref name="count"/> lumières de la vue. Tant que
        /// les lanternes du monde ne sont pas pleinement allumées (<paramref name="lamps"/> &lt; 1 : le jour et au crépuscule), les
        /// lanternes toujours allumées passent d'abord (la lanterne de Sauge garde sa lumière le jour, même loin de la caméra), puis
        /// les autres ; à chaque fois de la plus proche à la plus éloignée (<paramref name="distances"/> : carrés des distances à la
        /// caméra), à égalité dans l'ordre des indices. La nuit, seule la distance compte. Rend les indices retenus.
        /// </summary>
        public static IReadOnlyList<int> LampOrder(IReadOnlyList<double> distances, IReadOnlyList<bool> alwaysLit, double lamps, int count)
        {
            if (distances.Count != alwaysLit.Count) throw new ArgumentException("autant de distances que de lanternes", nameof(alwaysLit));
            bool day = lamps < 1.0;
            return Enumerable.Range(0, distances.Count)
                .OrderBy(i => day && alwaysLit[i] ? 0 : 1)
                .ThenBy(i => distances[i])
                .ThenBy(i => i)
                .Take(Math.Max(0, count))
                .ToList();
        }

        /// <summary>
        /// V2.22 : force (0 à 1) de la fumée d'un feu (<see cref="ShopChimney"/>, d'après son identifiant) : 0,35 pour le feu de tisanes
        /// d'une herboristerie (décision de Damien du 10 novembre 2026 au soir : fumée légère), 1 pour les autres (fournils, cuisines,
        /// âtres).
        /// </summary>
        public static double SmokeStrength(string? chimneyId) =>
            chimneyId != null && chimneyId.StartsWith(AdventureSession.TisanePrefix, StringComparison.Ordinal) ? 0.35 : 1.0;

        /// <summary>
        /// V2.17 : place dans le monde d'un point (<paramref name="px"/>, <paramref name="py"/>, <paramref name="pz"/>) du repère d'un modèle
        /// posé comme le fait la vue (sans miroir) : le pied du modèle (centre <paramref name="offX"/>, <paramref name="offZ"/>, bas
        /// <paramref name="offY"/>, mesurés à l'échelle 1) au centre (<paramref name="cx"/>, <paramref name="cz"/>) de l'emprise, au sol,
        /// mis à l'échelle de <paramref name="fit"/> dans les axes du modèle, puis tourné de <see cref="ModelFit.Yaw"/> comme
        /// <c>Quaternion.Euler(0, yaw, 0)</c> d'Unity (le +z du modèle va vers <paramref name="facing"/>).
        /// </summary>
        public static (double X, double Y, double Z) OwnPoint(double cx, double cz, (int X, int Z) facing, ModelFit fit, double offX, double offY,
            double offZ, double px, double py, double pz)
        {
            bool across = facing.X != 0;
            double lx = (px - offX) * (across ? fit.ScaleZ : fit.ScaleX), ly = (py - offY) * fit.ScaleY, lz = (pz - offZ) * (across ? fit.ScaleX : fit.ScaleZ);
            double a = fit.Yaw * Math.PI / 180, cos = Math.Cos(a), sin = Math.Sin(a);
            return (cx + lx * cos + lz * sin, ly, cz - lx * sin + lz * cos);
        }

        /// <summary>
        /// V2.18 (décision technique du 10 novembre 2026, prise pour Damien) : un modèle de l'atelier est dessiné au mètre pour son
        /// emprise et posé à l'**échelle 1** (une échelle non uniforme déformerait portes et pains) : seulement tourné vers
        /// <paramref name="facing"/>, son pivot (centre du pied des murs, au sol, fixé à l'export) au centre de l'emprise. Les débords
        /// (toit, enseigne, appentis du four) peuvent sortir de l'emprise : c'est voulu.
        /// </summary>
        public static ModelFit OwnFit((int X, int Z) facing) => new ModelFit(FacingYaw(facing), 1, 1, 1, 1, true);

        /// <summary>
        /// V2.18 : place dans le monde d'un point du repère d'un modèle de l'atelier (<see cref="OwnChimney"/>, <see cref="OwnLantern"/>)
        /// posé par <see cref="OwnFit"/> au centre (<paramref name="cx"/>, <paramref name="cz"/>) de l'emprise.
        /// </summary>
        public static (double X, double Y, double Z) OwnPlace(double cx, double cz, (int X, int Z) facing, double px, double py, double pz) =>
            OwnPoint(cx, cz, facing, OwnFit(facing), 0, 0, 0, px, py, pz);

        /// <summary>Écart (m) de la lumière de porte d'un bâtiment de l'atelier : à droite de la porte, devant la façade.</summary>
        public const double OwnLanternAside = 0.4, OwnLanternAhead = 0.4;

        /// <summary>
        /// V2.17 : la lumière de la lanterne de porte d'un bâtiment de l'atelier (la lanterne elle-même est dans le modèle) : à
        /// <see cref="OwnLanternAside"/> m à droite de la porte (vue de dehors), à <see cref="NightLamps.DoorLanternHeight"/> m de haut,
        /// <see cref="OwnLanternAhead"/> m devant la façade (le bord de l'emprise du côté de <paramref name="facing"/>) ; sans porte, à droite
        /// du milieu de la façade.
        /// </summary>
        public static NightLamp OwnDoorLantern(string id, double x0, double z0, double x1, double z1, (int X, int Z) facing, double? doorX, double? doorZ)
        {
            double cx = (x0 + x1) / 2, cz = (z0 + z1) / 2;
            double fx = Math.Sign(facing.X), fz = facing.X != 0 ? 0 : Math.Sign(facing.Z == 0 ? 1 : facing.Z);
            double ax = -fz, az = fx;   // la droite quand on regarde la façade de dehors (comme NightLamps.DoorLantern)
            double x, z;
            if (fx != 0)
            {
                x = (fx > 0 ? x1 : x0) + fx * OwnLanternAhead;
                z = (doorZ ?? cz) + az * OwnLanternAside;
            }
            else
            {
                z = (fz > 0 ? z1 : z0) + fz * OwnLanternAhead;
                x = (doorX ?? cx) + ax * OwnLanternAside;
            }
            return new NightLamp(id + "/lanterne", NightLampKind.DoorLantern, x, NightLamps.DoorLanternHeight, z, StyleRules.FaceYaw(cx, cz, cx + fx, cz + fz));
        }

        /// <summary>
        /// Part de l'emprise que le pied d'un modèle peut déborder : les avant-toits des maisons (6 %), les cordes de la tente (60 %) ; le
        /// puits, rond, tient dans son octogone (85 %), la tour carrée aussi (95 %).
        /// </summary>
        public static double Overflow(StyleBuilding kind)
        {
            switch (kind)
            {
                case StyleBuilding.Tent: return 1.6;
                case StyleBuilding.Well: return 0.85;
                case StyleBuilding.Tower: return 0.95;
                case StyleBuilding.Stall: return 1.1;
                default: return 1.06;
            }
        }

        /// <summary>Rotation (degrés) qui tourne la face +z d'un modèle vers <paramref name="facing"/> (un des quatre axes).</summary>
        public static double FacingYaw((int X, int Z) facing) => Math.Atan2(facing.X, facing.Z) * 180 / Math.PI;

        /// <summary>
        /// Pose d'un modèle de pied <paramref name="modelX"/> × <paramref name="modelZ"/> (m, face vers +z) sur une emprise de
        /// <paramref name="footX"/> × <paramref name="footZ"/> m, face tournée vers <paramref name="facing"/> : chaque axe du sol remplit
        /// l'emprise (débordement <paramref name="overflow"/>), sans être étiré de plus de <see cref="MaxStretch"/> par rapport à l'autre.
        /// </summary>
        public static ModelFit Fit(double footX, double footZ, (int X, int Z) facing, double modelX, double modelZ, double overflow)
        {
            if (!(footX > 0) || !(footZ > 0) || !(modelX > 0) || !(modelZ > 0) || !(overflow > 0)) return new ModelFit(0, 1, 1, 0, 0, false);
            double yaw = FacingYaw(facing);
            bool turned = facing.X != 0;
            double wx = turned ? modelZ : modelX, dz = turned ? modelX : modelZ;
            double sx = footX * overflow / wx, sz = footZ * overflow / dz;
            double low = Math.Min(sx, sz);
            sx = Math.Min(sx, low * MaxStretch);
            sz = Math.Min(sz, low * MaxStretch);
            double fill = wx * sx * dz * sz / (footX * footZ);
            // Le puits rond et la tour carrée sont voulus un peu plus petits que leur emprise (Overflow) : leur couverture se juge par
            // rapport à cette taille voulue.
            double reach = Math.Min(overflow, 1);
            double cover = Math.Min(wx * sx / (footX * reach), dz * sz / (footZ * reach));
            bool fits = fill >= MinFill && cover >= MinCover && Math.Min(sx, sz) >= MinScale && Math.Max(sx, sz) <= MaxScale;
            return new ModelFit(yaw, sx, sz, fill, cover, fits);
        }

        /// <summary>
        /// Note d'une pose : celles qui tiennent passent devant ; sinon la plus couvrante le long de son axe le moins couvert, pénalisée si
        /// elle est trop réduite ou trop grossie.
        /// </summary>
        private static double Quality(ModelFit fit) =>
            fit.Fits ? 2 + Math.Min(fit.Cover, 1)
                : Math.Min(fit.Cover, 1) * Math.Min(1, Math.Min(fit.ScaleX, fit.ScaleZ) / MinScale) * Math.Min(1, MaxScale / Math.Max(fit.ScaleX, fit.ScaleZ));

        /// <summary>
        /// Relecture indépendante de l'essai 8 : une pose de repli (aucun modèle ne tient) reste montrée si elle couvre au moins les trois
        /// quarts de l'emprise le long de chaque axe sans être réduite ou grossie de plus de moitié ; sinon le bâtiment fait par le code reste
        /// (un modèle de 3 m grossi trois fois sur une emprise de 8 m serait pire).
        /// </summary>
        public static bool Usable(ModelFit fit) =>
            fit.Fits || fit.Cover >= 0.75 && Math.Min(fit.ScaleX, fit.ScaleZ) >= 0.5 && Math.Max(fit.ScaleX, fit.ScaleZ) <= 1.5;

        /// <summary>
        /// Choix stable du modèle d'un bâtiment <paramref name="id"/> parmi <paramref name="models"/> (pieds, m) et de sa face : d'abord
        /// vers la route (<paramref name="preferred"/>), parmi les modèles qui y tiennent (<see cref="StyleRules.Variant"/> de l'identifiant) ;
        /// sinon de côté (une chapelle longue sur une emprise longue) ; sinon le modèle qui couvre le mieux. (-1, …) sans modèle.
        /// </summary>
        /// <param name="locked">Intérieurs : la façade reste vers <paramref name="preferred"/> (le bâtiment a une porte, on doit la voir
        /// où l'on entre) : ni de côté, ni le meilleur compromis d'un autre côté.</param>
        public static (int Model, (int X, int Z) Facing, ModelFit Fit) Pick(string id, double footX, double footZ, (int X, int Z) preferred,
            IReadOnlyList<(double X, double Z)> models, double overflow, bool locked = false)
        {
            if (models == null || models.Count == 0) return (-1, preferred, default);
            var facings = locked ? new List<(int X, int Z)> { preferred } : new List<(int X, int Z)> { preferred, (preferred.Z, -preferred.X), (-preferred.Z, preferred.X) };
            foreach ((int X, int Z) facing in facings)
            {
                var good = new List<int>();
                for (int i = 0; i < models.Count; i++)
                    if (Fit(footX, footZ, facing, models[i].X, models[i].Z, overflow).Fits) good.Add(i);
                if (good.Count == 0) continue;
                int chosen = good[StyleRules.Variant(id ?? string.Empty, good.Count)];
                return (chosen, facing, Fit(footX, footZ, facing, models[chosen].X, models[chosen].Z, overflow));
            }
            int best = 0;
            (int X, int Z) bestFacing = preferred;
            double bestQuality = double.NegativeInfinity;
            foreach ((int X, int Z) facing in facings)
                for (int i = 0; i < models.Count; i++)
                {
                    double q = Quality(Fit(footX, footZ, facing, models[i].X, models[i].Z, overflow));
                    if (q > bestQuality + 1e-9)
                    {
                        bestQuality = q;
                        best = i;
                        bestFacing = facing;
                    }
                }
            return (best, bestFacing, Fit(footX, footZ, bestFacing, models[best].X, models[best].Z, overflow));
        }

        /// <summary>Le bâtiment <paramref name="id"/> est-il montré en miroir (moitié des maisons, pour varier un même modèle) ?</summary>
        public static bool Mirrored(string id) => StyleRules.Variant((id ?? string.Empty) + "/miroir", 2) == 1;

        /// <summary>
        /// Essai réaliste 8 : les villageois du bourg, à <paramref name="max"/> au plus, toujours les mêmes : deux qui bavardent près de chaque
        /// puits, un marchand derrière chaque étal, un garde de chaque côté de chaque porte (côté bourg), une sentinelle devant l'auberge, un
        /// paysan qui cueille dans chacun des premiers champs, un passant qui va et vient le long de la rue pavée. Chacun hors des obstacles
        /// (<see cref="VillagerMargin"/>), loin des PNJ, points sûrs, ateliers et nœuds (<see cref="ClearingMargin"/>) et des autres
        /// villageois ; le passant, tout le long de son chemin.
        /// </summary>
        public static IReadOnlyList<StyleVillager> Villagers(AdventureMap map, IReadOnlyList<StyleField>? fields, int max)
        {
            var result = new List<StyleVillager>();
            if (map is null || max <= 0) return result;
            var shapes = map.Obstacles.Select(o => (o.Id, Points: (IReadOnlyList<(double X, double Z)>)o.Points
                .Select(p => (p.X.Raw / 10000.0, p.Z.Raw / 10000.0)).ToList())).ToList();
            var boxes = shapes.Select(s => (s.Id, MinX: s.Points.Min(p => p.X), MinZ: s.Points.Min(p => p.Z), MaxX: s.Points.Max(p => p.X),
                MaxZ: s.Points.Max(p => p.Z), s.Points)).ToList();

            bool Free(double x, double z)
            {
                if (x < map.MinX + 1 || x > map.MaxX - 1 || z < map.MinZ + 1 || z > map.MaxZ - 1) return false;
                foreach (var b in boxes)
                    if (x > b.MinX - VillagerMargin - 0.01 && x < b.MaxX + VillagerMargin + 0.01 && z > b.MinZ - VillagerMargin - 0.01
                        && z < b.MaxZ + VillagerMargin + 0.01 && StyleRules.InsideDistance(x, z, b.Points) > -VillagerMargin) return false;
                foreach ((double cx, double cz) in map.Clearings)
                    if ((cx - x) * (cx - x) + (cz - z) * (cz - z) < ClearingMargin * ClearingMargin) return false;
                foreach (StyleVillager v in result)
                    if ((v.X - x) * (v.X - x) + (v.Z - z) * (v.Z - z) < VillagerSpacing * VillagerSpacing) return false;
                return true;
            }

            void Add(string id, string character, VillagerActivity activity, double x, double z, double yaw)
            {
                if (result.Count < max && Free(x, z)) result.Add(new StyleVillager(id, character, activity, x, z, yaw));
            }

            foreach (var b in boxes)
            {
                StyleBuilding kind = TownRules.BuildingFor(b.Id);
                double cx = (b.MinX + b.MaxX) / 2, cz = (b.MinZ + b.MaxZ) / 2;
                (int X, int Z) f = TownRules.FacingOf(b.Id, b.MinX, b.MinZ, b.MaxX, b.MaxZ, map.Roads, map.Doors);
                (int X, int Z) side = (-f.Z, f.X);
                double half = f.X != 0 ? (b.MaxX - b.MinX) / 2 : (b.MaxZ - b.MinZ) / 2;
                double across = f.X != 0 ? (b.MaxZ - b.MinZ) / 2 : (b.MaxX - b.MinX) / 2;
                if (kind == StyleBuilding.Well)
                {
                    // Deux qui bavardent, côté route, face à face.
                    double ax = cx + f.X * (half + 1.1) + side.X * 0.75, az = cz + f.Z * (half + 1.1) + side.Z * 0.75;
                    double bx = cx + f.X * (half + 1.1) - side.X * 0.75, bz = cz + f.Z * (half + 1.1) - side.Z * 0.75;
                    Add(b.Id + "/bavard1", "paysan", VillagerActivity.Talk, ax, az, StyleRules.FaceYaw(ax, az, bx, bz));
                    Add(b.Id + "/bavard2", "garde", VillagerActivity.Idle, bx, bz, StyleRules.FaceYaw(bx, bz, ax, az));
                }
                else if (kind == StyleBuilding.Stall)
                {
                    // Le marchand, derrière son comptoir, regarde la route.
                    double x = cx - f.X * (half + 0.8), z = cz - f.Z * (half + 0.8);
                    Add(b.Id + "/marchand", "paysan", VillagerActivity.Talk, x, z, TownModels.FacingYaw(f));
                }
                else if (kind == StyleBuilding.Inn)
                {
                    double x = cx + f.X * (half + 1.3) + side.X * across * 0.55, z = cz + f.Z * (half + 1.3) + side.Z * across * 0.55;
                    Add(b.Id + "/sentinelle", "sentinelle", VillagerActivity.Look, x, z, TownModels.FacingYaw(f));
                }
                else if (StyleRules.Key(b.Id).StartsWith("porte", StringComparison.Ordinal))
                {
                    // Un garde de chaque côté de la porte, du côté du bourg (le lieu habité le plus proche).
                    MapPlace? town = map.Places.Where(p => TownRules.Inhabited(p.Id))
                        .OrderBy(p => (p.CenterX - cx) * (p.CenterX - cx) + (p.CenterZ - cz) * (p.CenterZ - cz)).FirstOrDefault();
                    if (town is null) continue;
                    bool alongX = b.MaxX - b.MinX >= b.MaxZ - b.MinZ;
                    int toward = alongX ? Math.Sign(town.CenterZ - cz) : Math.Sign(town.CenterX - cx);
                    if (toward == 0) toward = -1;
                    double length = alongX ? (b.MaxX - b.MinX) / 2 : (b.MaxZ - b.MinZ) / 2;
                    double depth = alongX ? (b.MaxZ - b.MinZ) / 2 : (b.MaxX - b.MinX) / 2;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        double x = alongX ? cx + s * (length - 0.6) : cx + toward * (depth + 1.0);
                        double z = alongX ? cz + toward * (depth + 1.0) : cz + s * (length - 0.6);
                        double yaw = alongX ? (toward > 0 ? 0 : 180) : (toward > 0 ? 90 : -90);
                        Add(b.Id + (s < 0 ? "/garde1" : "/garde2"), s < 0 ? "garde" : "sentinelle", VillagerActivity.Look, x, z, yaw);
                    }
                }
            }

            // Un paysan qui cueille au bord de chacun des premiers champs, tourné vers son milieu.
            int gathering = 0;
            foreach (StyleField field in fields ?? Array.Empty<StyleField>())
            {
                if (gathering >= 3) break;
                double mx = (field.MinX + field.MaxX) / 2, mz = (field.MinZ + field.MaxZ) / 2;
                double x = field.RowsAlongX ? field.MinX + 1.2 : mx, z = field.RowsAlongX ? mz : field.MinZ + 1.2;
                int before = result.Count;
                Add("champ" + gathering + "/paysan", "paysan", VillagerActivity.Gather, x, z, StyleRules.FaceYaw(x, z, mx, mz));
                if (result.Count > before) gathering++;
            }

            // Un passant le long de la rue pavée la plus longue, sur le côté (le milieu reste à ceux qui passent).
            StyleRoad? street = map.Roads.Where(r => r.Paved).OrderByDescending(Length).ThenBy(r => r.Id, StringComparer.Ordinal).FirstOrDefault();
            if (street != null && result.Count < max)
            {
                var a = street.Points[0];
                var b = street.Points[street.Points.Count - 1];
                double len = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z));
                if (len > 6)
                {
                    double ux = (b.X - a.X) / len, uz = (b.Z - a.Z) / len;
                    foreach (double offset in new[] { -street.Width * 0.3, street.Width * 0.3 })
                    {
                        double x0 = a.X + ux * 2 - uz * offset, z0 = a.Z + uz * 2 + ux * offset;
                        double x1 = b.X - ux * 2 - uz * offset, z1 = b.Z - uz * 2 + ux * offset;
                        bool clear = true;
                        for (int i = 0; i <= 40 && clear; i++)
                        {
                            double t = i / 40.0;
                            clear = Free(x0 + (x1 - x0) * t, z0 + (z1 - z0) * t);
                        }
                        if (!clear) continue;
                        result.Add(new StyleVillager(street.Id + "/passant", "paysan", VillagerActivity.Walk, x0, z0, StyleRules.FaceYaw(x0, z0, x1, z1), x1, z1));
                        break;
                    }
                }
            }
            return result;
        }

        private static double Length(StyleRoad road)
        {
            double total = 0;
            for (int i = 1; i < road.Points.Count; i++)
                total += Math.Sqrt((road.Points[i].X - road.Points[i - 1].X) * (road.Points[i].X - road.Points[i - 1].X)
                    + (road.Points[i].Z - road.Points[i - 1].Z) * (road.Points[i].Z - road.Points[i - 1].Z));
            return total;
        }

        /// <summary>
        /// Animation d'un villageois parmi celles de son personnage (« Paysan Talking Idle », « Garde Looking Around Idle », « Paysan Walk ») :
        /// celle de son activité (parler, regarder, cueillir — une variante stable d'après <paramref name="variant"/> —, marcher), sinon son
        /// attente la plus simple (le nom le plus court qui contient « idle ») ; null sans animation.
        /// </summary>
        public static string? VillagerClip(IEnumerable<string> clips, VillagerActivity activity, int variant)
        {
            var all = (clips ?? Array.Empty<string>()).Where(c => !string.IsNullOrEmpty(c)).OrderBy(c => c, StringComparer.Ordinal).ToList();
            string[] words;
            switch (activity)
            {
                case VillagerActivity.Talk: words = new[] { "talk", "convers", "argu" }; break;
                case VillagerActivity.Look: words = new[] { "look", "around" }; break;
                case VillagerActivity.Gather: words = new[] { "pull", "pick", "plant", "harvest", "gather", "dig", "fruit" }; break;
                case VillagerActivity.Walk: words = new[] { "walk" }; break;
                default: words = new string[0]; break;
            }
            var matching = all.Where(c => words.Any(w => StyleRules.Key(c).Contains(w))).ToList();
            if (matching.Count > 0) return matching[Math.Abs(variant) % matching.Count];
            string[] busy = { "talk", "convers", "argu", "look", "around", "pull", "pick", "plant", "harvest", "gather", "dig", "fruit", "walk" };
            return all.Where(c => StyleRules.Key(c).Contains("idle") && !busy.Any(w => StyleRules.Key(c).Contains(w)))
                .OrderBy(c => c.Length).ThenBy(c => c, StringComparer.Ordinal).FirstOrDefault()
                ?? all.FirstOrDefault(c => StyleRules.Key(c).Contains("idle"));
        }
    }
}
