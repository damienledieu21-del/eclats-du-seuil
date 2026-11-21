using System;
using System.Collections.Generic;
using System.Linq;

namespace EdS.Runtime
{
    /// <summary>
    /// Lot C (modèles générés par Tripo d'après les images commandées par Damien, intégrés le 16 novembre 2026) : un modèle du jeu, son code
    /// (« C_RE_S01 »), sa catégorie (dossier de l'atelier, « 08_Recoltes »), son nom, et ce qu'il représente dans les données : des cibles
    /// préfixées, <c>item:</c> (objet du catalogue), <c>node:</c> (nœud de récolte de <c>crafting.json</c>), <c>room:</c> (meuble d'un
    /// intérieur : « lieu/obstacle »), <c>piece:</c> (objet posé par l'aménagement d'un intérieur : « lieu/sorte »), <c>village:</c> (décor
    /// du village posé par le jeu). Sans cible, <see cref="Pending"/> dit pourquoi (aucune donnée ne le décrit encore : rien n'est inventé).
    /// </summary>
    public sealed class LotCModel
    {
        internal LotCModel(string code, string category, string name, IReadOnlyList<string> targets, string? pending)
        {
            Code = code;
            Category = category;
            Name = name;
            Targets = targets;
            Pending = pending;
        }

        public string Code { get; }
        public string Category { get; }
        public string Name { get; }
        public IReadOnlyList<string> Targets { get; }

        /// <summary>Pourquoi le modèle n'est encore relié à rien (null s'il l'est).</summary>
        public string? Pending { get; }

        /// <summary>Objet porté en main (arbalètes, armes des butins) : son export de jeu garde le LOD0 (moins de 20 000 triangles).</summary>
        public bool HandHeld => LotCModels.IsHandHeld(Code);

        /// <summary>Relié à au moins une donnée.</summary>
        public bool Linked => Targets.Count > 0;
    }

    /// <summary>Lot C : une copie d'un meuble posée dans l'emprise de son obstacle (centre, lacet en degrés, échelle, écrasement de la profondeur).</summary>
    public readonly struct LotCCopy
    {
        public LotCCopy(double x, double z, double yaw, double scale, double thinX, double thinZ)
        {
            X = x;
            Z = z;
            Yaw = yaw;
            Scale = scale;
            ThinX = thinX;
            ThinZ = thinZ;
        }

        public double X { get; }
        public double Z { get; }

        /// <summary>Degrés, comme <c>Quaternion.Euler(0, yaw, 0)</c> d'Unity (l'avant +z du modèle va vers (sin, cos)).</summary>
        public double Yaw { get; }

        /// <summary>Échelle uniforme (hauteur et largeur).</summary>
        public double Scale { get; }

        /// <summary>Facteurs de plus sur les côtés x et z du modèle (1 : aucun ; moins de 1 : son petit côté est aminci pour tenir dans l'obstacle).</summary>
        public double ThinX { get; }

        public double ThinZ { get; }
    }

    /// <summary>Pièce posée autour d'un décor du village (<see cref="LotCModels.ForgeYard"/>) : clé du décor, décalage (m) et quart de tour.</summary>
    public sealed class LotCYardPiece
    {
        public LotCYardPiece(string key, double dx, double dz, double yaw)
        {
            Key = key;
            Dx = dx;
            Dz = dz;
            Yaw = yaw;
        }

        public string Key { get; }
        public double Dx { get; }
        public double Dz { get; }
        public double Yaw { get; }
    }

    /// <summary>
    /// Lot C : table des modèles intégrés et de ce qu'ils représentent ; fichiers dans
    /// « Assets/EdS/Modeles/LotC/&lt;catégorie&gt;/&lt;code&gt;/&lt;code&gt;.fbx » (un seul maillage, échelle réelle en mètres, pivot au sol), avec
    /// « &lt;code&gt;_couleur.jpg » et « &lt;code&gt;_normale.png ». Affichage seulement : le moteur et les données ne changent pas. Sans Unity.
    /// </summary>
    public static partial class LotCModels
    {
        /// <summary>Dossier Unity des modèles du lot C.</summary>
        public const string Folder = "Assets/EdS/Modeles/LotC";

        /// <summary>Tous les modèles intégrés, dans l'ordre des catégories puis des codes.</summary>
        public static IReadOnlyList<LotCModel> All { get; } = Build();

        private static IReadOnlyList<LotCModel> Build()
        {
            var all = new List<LotCModel>();
            all.AddRange(Equipment());
            all.AddRange(Loot());
            all.AddRange(Harvest());
            all.AddRange(Village());
            all.AddRange(InteriorsBag());
            all.AddRange(InteriorsVillage());
            all.AddRange(NewPlaces());
            all.AddRange(Firmin());   // pâtisseries de Firmin (19 novembre 2026)
            var codes = new HashSet<string>(StringComparer.Ordinal);
            foreach (LotCModel m in all)
                if (!codes.Add(m.Code)) throw new InvalidOperationException("Lot C : code en double " + m.Code + ".");
            return all.OrderBy(m => m.Category, StringComparer.Ordinal).ThenBy(m => m.Code, StringComparer.Ordinal).ToList();
        }

        /// <summary>Modèle relié à des données.</summary>
        private static LotCModel Linked(string code, string category, string name, params string[] targets) =>
            new LotCModel(code, category, name, targets, null);

        /// <summary>Modèle livré qu'aucune donnée ne décrit encore (<paramref name="why"/>).</summary>
        private static LotCModel Waiting(string code, string category, string name, string why) =>
            new LotCModel(code, category, name, Array.Empty<string>(), why);

        /// <summary>Le modèle de ce code, ou null (index construit une fois).</summary>
        public static LotCModel? Find(string? code) => code != null && Index.ByCode.TryGetValue(code, out LotCModel? m) ? m : null;

        /// <summary>
        /// Index construits une seule fois, au premier usage (classe imbriquée : après <see cref="All"/>) : modèle par code, et code du
        /// modèles (dans l'ordre de <see cref="All"/>) qui portent chaque cible.
        /// </summary>
        private static class Index
        {
            public static readonly Dictionary<string, LotCModel> ByCode = BuildByCode();
            public static readonly Dictionary<string, List<string>> ByTarget = BuildByTarget();

            private static Dictionary<string, LotCModel> BuildByCode()
            {
                var d = new Dictionary<string, LotCModel>(StringComparer.Ordinal);
                foreach (LotCModel m in All) d[m.Code] = m;
                return d;
            }

            private static Dictionary<string, List<string>> BuildByTarget()
            {
                var d = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                foreach (LotCModel m in All)
                    foreach (string t in m.Targets)
                    {
                        if (!d.TryGetValue(t, out List<string>? codes)) d.Add(t, codes = new List<string>());
                        if (!codes.Contains(m.Code)) codes.Add(m.Code);
                    }
                return d;
            }
        }

        /// <summary>Objet porté en main : arbalètes (« C_EQ… ») et armes des butins (épieux, couteaux, lances, dagues : « C_BU_Lnn_01/02 »).</summary>
        public static bool IsHandHeld(string code) =>
            code.StartsWith("C_EQ", StringComparison.Ordinal)
            || (code.StartsWith("C_BU_L", StringComparison.Ordinal) && (code.EndsWith("_01", StringComparison.Ordinal) || code.EndsWith("_02", StringComparison.Ordinal)));

        /// <summary>
        /// Code du modèle rangé en « …/Modeles/LotC/&lt;catégorie&gt;/&lt;code&gt;/&lt;code&gt;.fbx » (casse du dossier et « \ » indifférents),
        /// s'il est dans la table ; null ailleurs.
        /// </summary>
        public static string? CodeOf(string? path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string[] parts = path!.Replace('\\', '/').Split('/');
            int n = parts.Length;
            if (n < 5 || !parts[n - 1].EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) return null;
            if (!string.Equals(parts[n - 4], "LotC", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(parts[n - 5], "Modeles", StringComparison.OrdinalIgnoreCase)) return null;
            string code = parts[n - 1].Substring(0, parts[n - 1].Length - 4);
            LotCModel? model = Find(code);
            return model != null && parts[n - 2] == code && parts[n - 3] == model.Category ? code : null;
        }

        private static string? Target(string target) => Index.ByTarget.TryGetValue(target, out List<string>? codes) ? codes[0] : null;

        /// <summary>Modèle du nœud de récolte <paramref name="nodeId"/> (« filon_ouest »), ou null.</summary>
        public static string? ForNode(string? nodeId) => nodeId == null ? null : Target("node:" + nodeId);

        /// <summary>Modèle de l'objet <paramref name="itemId"/> du catalogue (« item.cepe »), ou null.</summary>
        public static string? ForItem(string? itemId) => itemId == null ? null : Target("item:" + itemId);

        /// <summary>
        /// Modèle d'un décor du village posé par le jeu (<paramref name="key"/> : « wave5/FlourSack », « prop/FirePit », « forge/enclume »),
        /// ou null. Plusieurs modèles pour un même décor (les trois sacs de farine) : celui tiré de <paramref name="id"/>.
        /// </summary>
        public static string? ForVillage(string? key, string? id) => ForVillage(key, id, null);

        /// <summary>
        /// Comme <see cref="ForVillage(string?, string?)"/>, mais <paramref name="rank"/>, s'il est donné, choisit parmi plusieurs modèles
        /// (rang demandé par l'appelant, modulo leur nombre) au lieu de le tirer de <paramref name="id"/>. Index construit une fois.
        /// </summary>
        public static string? ForVillage(string? key, string? id, int? rank)
        {
            if (key == null || !Index.ByTarget.TryGetValue("village:" + key, out List<string>? list)) return null;
            int n = list.Count;
            return list[rank.HasValue ? ((rank.Value % n) + n) % n : StyleRules.Variant(id ?? string.Empty, n)];
        }

        /// <summary>
        /// Modèle d'un obstacle d'une zone du dehors (« panneau_hameau ») : cible exacte <c>obstacle:panneau_hameau</c>, ou préfixe
        /// <c>obstacle:panneau_*</c> (tous les panneaux) ; null sinon.
        /// </summary>
        public static string? ForObstacle(string? obstacleId)
        {
            if (string.IsNullOrEmpty(obstacleId)) return null;
            foreach (LotCModel m in All)
                foreach (string t in m.Targets)
                {
                    if (!t.StartsWith("obstacle:", StringComparison.Ordinal)) continue;
                    string want = t.Substring("obstacle:".Length);
                    if (want.EndsWith("*", StringComparison.Ordinal) ? obstacleId!.StartsWith(want.Substring(0, want.Length - 1), StringComparison.Ordinal) : obstacleId == want)
                        return m.Code;
                }
            return null;
        }

        /// <summary>Modèle du meuble <paramref name="obstacleId"/> de l'intérieur dont le lieu est <paramref name="room"/> (« auberge », « comptoir »), ou null.</summary>
        public static string? ForRoom(string? room, string? obstacleId) =>
            room == null || obstacleId == null ? null : Target("room:" + room + "/" + obstacleId);

        /// <summary>Modèle des objets de sorte <paramref name="prop"/> posés par l'aménagement de l'intérieur <paramref name="room"/>, ou null.</summary>
        public static string? ForPiece(string? room, InteriorProp prop) =>
            room == null || prop == InteriorProp.None ? null : Target("piece:" + room + "/" + prop);

        /// <summary>Sortes d'objets d'aménagement qui font le corps d'un meuble (remplacées par le modèle du meuble, les autres restent).</summary>
        private static readonly InteriorProp[] Body =
        {
            InteriorProp.Table, InteriorProp.Bed, InteriorProp.Chest, InteriorProp.Cabinet, InteriorProp.Commode, InteriorProp.Drawers,
            InteriorProp.Shelves, InteriorProp.Crate, InteriorProp.Barrel, InteriorProp.Bucket, InteriorProp.SpinningWheel, InteriorProp.Statue,
        };

        /// <summary>
        /// L'objet d'aménagement <paramref name="piece"/> fait-il partie du corps du meuble <paramref name="obstacleId"/> (son identifiant ou
        /// « obstacle/… », posé au sol, d'une sorte de <see cref="Body"/>) ? Alors le modèle du lot C du meuble le remplace ; les sièges, la
        /// vaisselle et ce qui est posé dessus restent (posés sur le modèle).
        /// </summary>
        public static bool Replaces(string obstacleId, InteriorPiece piece) => Replaces(obstacleId, piece, null);

        /// <summary>
        /// Comme <see cref="Replaces(string, InteriorPiece)"/> ; revue Codex de la PR #200 : avec l'aménagement <paramref name="pieces"/>, une
        /// pièce du corps posée sur une autre de même sorte (la caisse empilée sur une caisse) est remplacée elle aussi quand celle du dessous
        /// l'est : le modèle Tripo porte déjà tout le meuble.
        /// </summary>
        public static bool Replaces(string obstacleId, InteriorPiece piece, IReadOnlyList<InteriorPiece>? pieces)
        {
            if (piece == null || !(piece.Id == obstacleId || piece.Id.StartsWith(obstacleId + "/", StringComparison.Ordinal)) || Array.IndexOf(Body, piece.Prop) < 0)
                return false;
            if (piece.Parent < 0) return true;
            if (pieces == null || piece.Parent >= pieces.Count) return false;
            InteriorPiece under = pieces[piece.Parent];
            return under.Prop == piece.Prop && Replaces(obstacleId, under, pieces);
        }

        /// <summary>
        /// Revue Codex de la PR #200 : objets d'aménagement allongés le long d'une table (bancs) : leur modèle du lot C est tourné pour que
        /// son grand côté suive l'axe de la pièce (<see cref="AlongX"/>), quel que soit l'axe de l'export.
        /// </summary>
        public static bool AlongTable(InteriorProp prop) => prop == InteriorProp.Bench;

        /// <summary>Un objet tourné de <paramref name="yaw"/> degrés a-t-il son grand côté selon x (0 ou 180 ; sinon selon z) ?</summary>
        public static bool AlongX(double yaw)
        {
            double a = ((yaw % 180) + 180) % 180;
            return a < 45 || a >= 135;
        }

        /// <summary>
        /// Revue Codex de la PR #201 : quart de tour imposé au meuble <paramref name="obstacleId"/> de la pièce <paramref name="room"/>, ou null
        /// (dos au mur le plus proche). Les bancs de nef de la chapelle (C_IN2_C0_01 : dossier côté −x de l'export, l'avant côté +x de
        /// Blender, −x dans Unity) sont tous tournés de 90° : leur assise regarde +z, l'autel au nord de la nef.
        /// </summary>
        public static double? YawFor(string? room, string? obstacleId) =>
            room == "chapelle" && obstacleId != null && obstacleId.StartsWith("banc", StringComparison.Ordinal) ? 90 : (double?)null;

        /// <summary>Échelle la plus grande donnée à un meuble du lot C (les modèles sont à l'échelle réelle des fiches).</summary>
        public const double MaxScale = 1.15;

        /// <summary>
        /// Pose d'un meuble du lot C (empreinte <paramref name="modelW"/> × <paramref name="modelD"/> m, avant vers +z) dans l'emprise de son
        /// obstacle (<paramref name="minX"/>…<paramref name="maxZ"/>) d'une pièce (<paramref name="roomMinX"/>…<paramref name="roomMaxZ"/>) :
        /// l'avant tourné vers la pièce (dos au mur le plus proche), le grand côté le long du grand côté de l'emprise (sinon tourné d'un quart) ;
        /// échelle jusqu'à <see cref="MaxScale"/>, réduite pour tenir dans la longueur ; profondeur amincie si elle dépasse ; copies côte à côte
        /// le long d'une emprise longue (comptoirs, étagères). Toute copie tient dans l'emprise : le meuble ne déborde jamais de son obstacle,
        /// donc ne bloque ni porte ni passage. Déterministe.
        /// </summary>
        public static IReadOnlyList<LotCCopy> Fit(double minX, double minZ, double maxX, double maxZ, double roomMinX, double roomMinZ,
            double roomMaxX, double roomMaxZ, double modelW, double modelD) =>
            Fit(minX, minZ, maxX, maxZ, roomMinX, roomMinZ, roomMaxX, roomMaxZ, modelW, modelD, null);

        /// <summary>
        /// Comme <see cref="Fit(double, double, double, double, double, double, double, double, double, double)"/> ; <paramref name="fixedYaw"/>
        /// (0, 90, 180 ou 270), s'il est donné, impose le quart de tour au lieu du mur le plus proche (revue Codex de la PR #201 : les bancs de
        /// nef regardent tous l'autel, <see cref="YawFor"/>).
        /// </summary>
        public static IReadOnlyList<LotCCopy> Fit(double minX, double minZ, double maxX, double maxZ, double roomMinX, double roomMinZ,
            double roomMaxX, double roomMaxZ, double modelW, double modelD, double? fixedYaw)
        {
            if (!(modelW > 0 && modelD > 0 && maxX > minX && maxZ > minZ)) return Array.Empty<LotCCopy>();
            double w = maxX - minX, d = maxZ - minZ, cx = (minX + maxX) / 2, cz = (minZ + maxZ) / 2;
            // Dos au mur le plus proche : l'avant regarde l'intérieur de la pièce.
            double west = minX - roomMinX, east = roomMaxX - maxX, south = minZ - roomMinZ, north = roomMaxZ - maxZ;
            double near = Math.Min(Math.Min(west, east), Math.Min(south, north));
            double yaw = fixedYaw ?? (near == south ? 0 : near == north ? 180 : near == west ? 90 : 270);
            // Côtés du modèle une fois tourné : à 90 ou 270°, sa largeur va selon z.
            bool turned = yaw == 90 || yaw == 270;
            double alongX = turned ? modelD : modelW, alongZ = turned ? modelW : modelD;
            bool boxAlongX = w >= d, modelAlongX = alongX >= alongZ;
            if (fixedYaw is null && boxAlongX != modelAlongX && Math.Abs(alongX - alongZ) > 0.05)
            {
                yaw = (yaw + 90) % 360;
                turned = !turned;
                (alongX, alongZ) = (alongZ, alongX);
            }
            double boxLong = boxAlongX ? w : d, boxShort = boxAlongX ? d : w;
            double modelLong = boxAlongX ? alongX : alongZ, modelShort = boxAlongX ? alongZ : alongX;
            double scale = Math.Min(MaxScale, Math.Max(boxShort / modelShort, 0.85));
            int count = Math.Max(1, (int)Math.Floor((boxLong + 0.05) / (modelLong * scale)));
            if (count * modelLong * scale > boxLong) scale = boxLong / (count * modelLong);
            double thin = modelShort * scale > boxShort ? boxShort / (modelShort * scale) : 1;
            // L'amincissement porte sur l'axe du modèle posé le long du petit côté de l'emprise (relecture de la PR #200 : même pour un
            // modèle presque carré, resté sans quart de tour) : tourné d'un quart, son x va selon z du monde.
            bool thinOnX = boxAlongX == turned;
            double thinX = thinOnX ? thin : 1, thinZ = thinOnX ? 1 : thin;
            var copies = new List<LotCCopy>();
            double step = boxLong / count;
            for (int i = 0; i < count; i++)
            {
                double t = -boxLong / 2 + step * (i + 0.5);
                copies.Add(new LotCCopy(boxAlongX ? cx + t : cx, boxAlongX ? cz : cz + t, yaw, scale, thinX, thinZ));
            }
            return copies;
        }

        /// <summary>
        /// Décision de Damien du 16 novembre 2026 : toutes les pièces de forge du lot C autour de la forge en plein air de Maëlle, sans
        /// bloquer le passage. Place de chacune par rapport au centre du lieu « forge » (le marqueur de l'atelier) : décalage en mètres
        /// (x vers l'est, z vers le nord), quart de tour, et, pour les outils, rang du modèle (<see cref="ForVillage(string?, string?, int?)"/>)
        /// posé sur l'établi. Mesures des modèles : <see cref="ForgeYardSize"/>. Tenu à l'écart de Maëlle et de son apprentie, de l'enclume,
        /// du foyer, du tonneau et de la caisse déjà posés, du chemin qui longe la forge au nord et des obstacles, avec un passage libre du sud jusqu'à Maëlle
        /// (<c>LotCModelsTests.Forge_de_Maelle_toutes_les_pieces_sans_bloquer_le_passage</c>). Affichage seulement, sans collision.
        /// </summary>
        public static IReadOnlyList<LotCYardPiece> ForgeYard { get; } = new[]
        {
            new LotCYardPiece("forge/soufflet", 2.4, -0.3, 0),
            new LotCYardPiece("forge/bac", 1.15, -0.9, 0),
            new LotCYardPiece("forge/charbon", 2.8, -1.6, 0),
            new LotCYardPiece("forge/meule", -1.1, -3.1, 0),
            new LotCYardPiece("forge/ratelier", 2.0, -3.24, 90),
            new LotCYardPiece("forge/fers", 3.47, 1.4, 0),
            new LotCYardPiece("forge/etabli", -3.15, -0.65, 0),
        };

        /// <summary>
        /// Décision de Damien du 17 novembre 2026 (vers 0 h 10) : l'auvent de la forge (C_OV_A01, 6 × 3,6 m, égout à 2,65 m) au-dessus de la
        /// forge en plein air, sur Maëlle et la cour de travail ; ses quatre poteaux (aux coins) hors des pièces, du chemin, du passage et à
        /// l'écart des forgerons ; aucune pièce plus haute que l'égout dessous (le foyer et sa hotte restent juste au nord).
        /// </summary>
        public static LotCYardPiece ForgeAwning { get; } = new LotCYardPiece("forge/auvent", 0.65, -1.6, 0);

        /// <summary>Hauteur de l'égout de l'auvent de la forge (fiche A01 des objets du village), en mètres.</summary>
        public const double ForgeAwningEave = 2.65;

        /// <summary>Outils (« forge/outils ») posés sur l'établi de la forge, un par modèle, dans l'ordre de la table.</summary>
        public const string ForgeTools = "forge/outils";

        /// <summary>
        /// Décors du village que le jeu pose à des places fixes (sans rôle ni objet) et qu'un modèle du lot C peut remplacer (déclaré après
        /// <see cref="ForgeYard"/>, dont il reprend les clés).
        /// </summary>
        public static IReadOnlyList<string> VillageSpots { get; } = new[] { "forge/enclume", ForgeTools, ForgeAwning.Key }.Concat(ForgeYard.Select(p => p.Key)).ToArray();

        /// <summary>Emprise au sol (x, z en mètres, sans quart de tour) des modèles de la forge de Maëlle, d'après leurs exports.</summary>
        public static (double W, double D) ForgeYardSize(string code) => code switch
        {
            "C_OV_A02" => (0.596, 0.891), "C_OV_A03" => (1.854, 2.291), "C_OV_A04" => (1.15, 0.563), "C_OV_A05" => (1.141, 2.2),
            "C_OV_A07" => (0.831, 1.251), "C_OV_A08" => (1.104, 2.309), "C_OV_A09" => (0.587, 1.351), "C_OV_A10" => (1.122, 1.693),
            "C_OV_A11" => (0.894, 1.196), "C_OV_C04" => (0.775, 0.741), "C_OV_C07_02" => (0.45, 0.448), "C_OV_A01" => (6.014, 3.618),
            _ => (0, 0),
        };

        /// <summary>Codes des outils de la forge (« forge/outils »), dans l'ordre où l'établi les reçoit.</summary>
        public static IReadOnlyList<string> ForgeToolCodes =>
            Index.ByTarget.TryGetValue("village:" + ForgeTools, out List<string>? codes) ? codes : (IReadOnlyList<string>)Array.Empty<string>();

        /// <summary>
        /// Échelle d'affichage d'un modèle sur un nœud de récolte : les plantes (une vingtaine de centimètres) sont grossies 2 fois pour
        /// qu'on les voie, comme la pervenche qu'elles remplacent (2,4) ; les filons restent à l'échelle réelle.
        /// </summary>
        public static double NodeScale(string code) => code.StartsWith("C_RE_MI_", StringComparison.Ordinal) ? 1.0 : 2.0;

        // --- 01 : équipement ------------------------------------------------------------------------------------------------------

        private const string NoCrossbow = "aucune arbalète dans le catalogue des objets ni dans les classes (arme du rôdeur à venir)";

        private static IEnumerable<LotCModel> Equipment()
        {
            const string c = "01_Equipement";
            yield return Waiting("C_EQ051", c, "Arbalète (commune)", NoCrossbow);
            yield return Waiting("C_EQ052", c, "Arbalète (peu commune)", NoCrossbow);
            yield return Waiting("C_EQ053", c, "Arbalète (rare)", NoCrossbow);
            yield return Waiting("C_EQ054", c, "Arbalète (épique)", NoCrossbow);
            yield return Waiting("C_EQ055", c, "Arbalète (légendaire)", NoCrossbow);
        }

        // --- 07 : butins des grottes noyées et du Grand Solitaire --------------------------------------------------------------

        private const string NoDrowned = "les grottes noyées (zone, ennemis, table de butin, objets) ne sont pas encore dans les données";

        private static IEnumerable<LotCModel> Loot()
        {
            const string c = "07_Butins";
            yield return Linked("C_BU_GS_A08", c, "Trophée des Trois Bornes", "item:item.trophee_des_trois_bornes");
            string[] names =
            {
                "Épieu de batelier", "Couteau de corde", "Gilet de rive", "Cape courte de toile", "Nasse miniature",
                "Épieu des passeurs", "Couteau des passeurs", "Brigandine légère", "Cape de passeur", "Roue de jauge",
                "Lance-écluse", "Dague de rive", "Cuirasse des écluses", "Manteau de jauge", "Moulage de la pince de Broie-Écluse",
                "Lance contre-courant", "Dague contre-courant", "Harnois de reflux", "Manteau du Cœur-Lac", "Avale-Marée sculpté",
                "Lance de la rive retrouvée", "Dague de la rive retrouvée", "Armure de la rive retrouvée", "Manteau du Gardien aux trois souffles",
                "Double coquille de Nacre-Muette",
            };
            for (int i = 0; i < names.Length; i++)
                yield return Waiting($"C_BU_L0{i / 5 + 1}_0{i % 5 + 1}", c, names[i], NoDrowned);
        }

        // --- 08 : récoltes (plantes, filons, amas) ---------------------------------------------------------------------------------

        private static IEnumerable<LotCModel> Harvest()
        {
            const string c = "08_Recoltes";
            const string dishes = "plats des quatre saisons absents du catalogue des objets et des recettes";
            const string mine = "la mine et ses dessous (nœuds, objets) ne sont pas encore dans les données";
            const string vintage = "vendanges des coteaux (château viticole) pas encore dans les données";
            yield return Waiting("C_RE_C01", c, "Bouillon des sources", dishes);
            yield return Waiting("C_RE_C02", c, "Galette des moissons", dishes);
            yield return Waiting("C_RE_C03", c, "Poêlée du sous-bois", dishes);
            yield return Waiting("C_RE_C04", c, "Marmite du pêcheur", dishes);
            yield return Linked("C_RE_F01", c, "Herbe de l'Aube", "item:item.herbe_de_l_aube", "node:herbes_village", "node:herbes_riviere",
                "node:herbes_pre", "node:herbes_foret", "node:herbes_lac", "node:herbes_verger");
            yield return Waiting("C_RE_MI_C14", c, "Champignons souterrains", mine);
            yield return Waiting("C_RE_MI_C16", c, "Cristaux de roche ordinaires", mine);
            // Filons de fer : trois tailles pour les sept filons des Vallons (taille choisie une fois pour toutes, affichage seulement).
            yield return Linked("C_RE_MI_D01", c, "Filon de fer (petit)", "node:filon_ouest", "node:filon_tour");
            yield return Linked("C_RE_MI_D02", c, "Filon de fer (moyen)", "node:filon_centre", "node:filon_pierres");
            yield return Linked("C_RE_MI_D03", c, "Filon de fer (grand)", "node:filon_est");
            yield return Waiting("C_RE_MI_D04", c, "Filon de cuivre", mine);
            yield return Waiting("C_RE_MI_D05", c, "Filon d'argent", mine);
            yield return Waiting("C_RE_MI_D06", c, "Amas du Seuil (petit)", mine);
            yield return Waiting("C_RE_MI_D07", c, "Amas du Seuil (moyen)", mine);
            yield return Waiting("C_RE_MI_D08", c, "Amas du Seuil (grand)", mine);
            yield return Waiting("C_RE_O11", c, "Cresson des suintements", NoDrowned);
            yield return Waiting("C_RE_O12_A", c, "Amas de lamelles d'Éclat", NoDrowned);
            yield return Waiting("C_RE_O12_B", c, "Lamelle d'Éclat", NoDrowned);
            yield return Linked("C_RE_S01", c, "Primevère argentée", "item:item.primevere_argentee", "node:primevere_ruisseau");
            yield return Waiting("C_RE_S02", c, "Cresson des sources", "aucun cresson dans le catalogue ni parmi les nœuds de saison");
            yield return Linked("C_RE_S03", c, "Sauge de pluie", "item:item.sauge_de_pluie", "node:sauge_verger");
            yield return Linked("C_RE_S04", c, "Cèpes", "item:item.cepe", "node:cepes_foret");
            yield return Linked("C_RE_S05", c, "Trompettes du sous-bois", "item:item.trompette_du_sous_bois", "node:trompettes_foret");
            yield return Waiting("C_RE_S06", c, "Grappe de raisin", vintage);
            yield return Waiting("C_RE_S07", c, "Hotte de vendange", vintage);
            yield return Waiting("C_RE_S08", c, "Panier de vendange", vintage);
        }

        // --- 02 : objets du village (forge, éclairage, mobilier de rue, marché, fêtes) ----------------------------------------------

        private static IEnumerable<LotCModel> Village()
        {
            const string c = "02_Objets_Village";
            const string light = "éclairage de nuit fait par le code (NightLamps : hauteur de la lumière réglée sur ses lanternes) : à brancher avec un essai dans Unity";
            const string street = "aucune place dans les données des Vallons pour ce mobilier de rue";
            const string market = "aucune place dans les données ni dans les décors du marché posés par le jeu";
            const string feast = "décor de fête sans place dans les données ni parmi les décors de fête posés par le jeu (guirlande, bannières)";
            yield return Linked("C_OV_A01", c, "Auvent de la forge", "village:forge/auvent");
            yield return Linked("C_OV_A02", c, "Enclume sur billot", "village:forge/enclume");
            yield return Linked("C_OV_A03", c, "Foyer de forge en pierre", "village:prop/FirePit");
            yield return Linked("C_OV_A04", c, "Soufflet manuel", "village:forge/soufflet");
            yield return Linked("C_OV_A05", c, "Établi avec outils", "village:forge/etabli", "item:item.meuble_etabli");
            yield return Linked("C_OV_A06_01", c, "Petit marteau", "village:forge/outils");
            yield return Linked("C_OV_A06_02", c, "Marteau moyen", "village:prop/Hammer");
            yield return Linked("C_OV_A06_03", c, "Grand marteau", "village:forge/outils");
            yield return Linked("C_OV_A06_04", c, "Petite pince", "village:forge/outils");
            yield return Linked("C_OV_A06_05", c, "Pince moyenne", "village:forge/outils");
            yield return Linked("C_OV_A06_06", c, "Grande pince", "village:forge/outils");
            yield return Linked("C_OV_A06_07", c, "Petite lime", "village:forge/outils");
            yield return Linked("C_OV_A06_08", c, "Grande lime", "village:forge/outils");
            yield return Linked("C_OV_A07", c, "Bac de trempe", "village:forge/bac");
            yield return Linked("C_OV_A08", c, "Râtelier d'armes", "village:forge/ratelier");
            yield return Linked("C_OV_A09", c, "Présentoir de fers à cheval", "village:forge/fers");
            yield return Linked("C_OV_A10", c, "Tas de charbon", "village:forge/charbon");
            yield return Linked("C_OV_A11", c, "Meule à aiguiser à pédale", "village:forge/meule");
            yield return Waiting("C_OV_B01", c, "Réverbère en fer forgé", light);
            yield return Waiting("C_OV_B02", c, "Lanterne sur poteau de bois", light);
            yield return Waiting("C_OV_B03", c, "Lanterne murale", light);
            yield return Waiting("C_OV_B04", c, "Brasero", street);
            yield return Waiting("C_OV_B05", c, "Torche murale", light);
            yield return Waiting("C_OV_B06_01", c, "Petite bougie sur bougeoir", "bougies des intérieurs : reliées avec les intérieurs (lot C, PR suivantes) si une pièce en a la place");
            yield return Waiting("C_OV_B06_02", c, "Bougie moyenne sur bougeoir", "bougies des intérieurs : reliées avec les intérieurs (lot C, PR suivantes) si une pièce en a la place");
            yield return Waiting("C_OV_B06_03", c, "Grande bougie sur bougeoir", "bougies des intérieurs : reliées avec les intérieurs (lot C, PR suivantes) si une pièce en a la place");
            yield return Waiting("C_OV_B07", c, "Lanterne de fête en papier", feast);
            yield return Linked("C_OV_C01", c, "Banc de pierre", "item:item.meuble_banc_de_pierre");   // V2.53.2 : meuble de la maison du joueur
            yield return Waiting("C_OV_C02", c, "Banc de bois", street);
            yield return Waiting("C_OV_C03_01", c, "Table de taverne", "la terrasse de la taverne est faite par le code ; aucune table dans les données");
            yield return Waiting("C_OV_C03_02", c, "Banc de table de taverne", "la terrasse de la taverne est faite par le code ; aucun banc dans les données");
            yield return Linked("C_OV_C04", c, "Tonneau debout", "village:prop/Barrel");
            yield return Waiting("C_OV_C05", c, "Tonneau couché", street);
            yield return Waiting("C_OV_C06", c, "Tonneaux empilés", street);
            yield return Waiting("C_OV_C07_01", c, "Grande caisse fermée", street);
            yield return Linked("C_OV_C07_02", c, "Caisse moyenne fermée", "village:prop/Crate");
            yield return Waiting("C_OV_C07_03", c, "Petite caisse ouverte", street);
            yield return Waiting("C_OV_C08_01", c, "Grand seau", street);
            yield return Waiting("C_OV_C08_02", c, "Petit seau", street);
            yield return Waiting("C_OV_C09", c, "Échelle", street);
            yield return Waiting("C_OV_C10", c, "Billot avec hache", street);
            yield return Waiting("C_OV_C11", c, "Tas de bûches", street);
            yield return Waiting("C_OV_C12", c, "Charrette à bras", street);
            yield return Linked("C_OV_C13", c, "Charrette à cheval", "village:wave5/Cart");
            yield return Linked("C_OV_C14", c, "Panneau de bois sur poteau", "obstacle:panneau_*");
            yield return Waiting("C_OV_C15", c, "Panneau de bois accroché", "enseignes des boutiques faites par le code (étiquettes) ; aucune place dans les données");
            yield return Waiting("C_OV_C16", c, "Abreuvoir", street);
            yield return Waiting("C_OV_C17", c, "Fontaine de place", street);
            yield return Waiting("C_OV_C18", c, "Pilori", street);
            yield return Waiting("C_OV_C19", c, "Puits couvert", "le puits des Vallons a déjà son modèle de l'atelier (puits_vallons) ; celui du Bourg est au chantier du Bourg fortifié");
            yield return Waiting("C_OV_D01", c, "Étal de marché couvert", "les étals des Vallons ont déjà leurs modèles de l'atelier ; ceux du Bourg sont au chantier du Bourg fortifié");
            yield return Linked("C_OV_D02_01", c, "Grand sac de farine", "village:wave5/FlourSack");
            yield return Linked("C_OV_D02_02", c, "Sac moyen de farine", "village:wave5/FlourSack");
            yield return Linked("C_OV_D02_03", c, "Petit sac de farine", "village:wave5/FlourSack");
            yield return Linked("C_OV_D03", c, "Table de pains", "village:wave5/BakeryTable");
            yield return Linked("C_OV_D04", c, "Caisse de poires", "village:wave5/PearCrate");
            yield return Waiting("C_OV_D05_01", c, "Panier moyen", market);
            yield return Waiting("C_OV_D05_02", c, "Petit panier", market);
            yield return Waiting("C_OV_D05_03", c, "Grand panier", market);
            yield return Waiting("C_OV_D06_01", c, "Grande jarre avec couvercle", market);
            yield return Waiting("C_OV_D06_02", c, "Jarre moyenne ouverte", market);
            yield return Waiting("C_OV_D06_03", c, "Petite jarre ouverte", market);
            yield return Waiting("C_OV_D07", c, "Rouet", market);
            yield return Waiting("C_OV_D08", c, "Métier à tisser de rue", market);
            yield return Waiting("C_OV_D09", c, "Tréteaux de tailleur de pierre", market);
            yield return Waiting("C_OV_D10", c, "Étal de poissonnier", market);
            yield return Waiting("C_OV_E01", c, "Guirlande de fanions", "la guirlande de Tripo n'a pas de mâts : la guirlande posée par le jeu est au sol (celle des packs a ses poteaux) ; à poser entre deux supports");
            yield return Waiting("C_OV_E02", c, "Guirlande de feuillage", feast);
            yield return Linked("C_OV_E03", c, "Bannières de tissu", "village:wave5/Banner");
            yield return Waiting("C_OV_E04", c, "Mât de fête avec rubans", feast);
            yield return Waiting("C_OV_E05", c, "Feu de joie", feast);
            yield return Waiting("C_OV_E06_01", c, "Table de banquet", feast);
            yield return Waiting("C_OV_E06_02", c, "Banc de banquet", feast);
            yield return Waiting("C_OV_E07", c, "Lanternes suspendues", feast);
            yield return Waiting("C_OV_E08", c, "Décorations d'hiver", feast);
        }

        // --- 03 : intérieurs de l'auberge, de la boulangerie et de la guilde -------------------------------------------------------

        private static IEnumerable<LotCModel> InteriorsBag()
        {
            const string c = "03_Interieurs_BAG";
            const string bakery = "pas de place pour ce meuble dans la boulangerie des données (comptoir, four, pétrin, étagère à pains, farine, tonneau)";
            const string guild = "pas de place pour ce meuble dans la maison de la Guilde des données (comptoir, étagère, cheminée, deux tables, caisses)";
            const string archives = "pas de place pour cet objet dans les archives des données (deux étagères, table, caisse, cheminée)";
            const string wall = "objet de mur : l'aménagement ne pose aux murs que boucliers, armes, tableaux, lanternes et bougies";
            const string small = "petit objet que l'aménagement de ces pièces ne pose pas";
            const string rooms = "auberge guilde archives boulangerie";
            string[] Pieces(string prop, string where = rooms) => where.Split(' ').Select(r => "piece:" + r + "/" + prop).ToArray();
            yield return Linked("C_IN1_A0_01", c, "Âtre monumental", "room:auberge/cheminee");
            yield return Linked("C_IN1_A0_02", c, "Table commune", "room:auberge/table1", "room:auberge/table2", "room:auberge/table3", "room:auberge/table4");
            yield return Linked("C_IN1_A0_03", c, "Banc", "piece:auberge/Bench");
            yield return Linked("C_IN1_A0_08", c, "Comptoir", "room:auberge/comptoir", "room:guilde/comptoir", "room:boulangerie/comptoir");
            yield return Linked("C_IN1_A0_09", c, "Tonneaux", "room:auberge/tonneau1", "room:auberge/tonneau2");
            yield return Linked("C_IN1_A0_11", c, "Foyer de la cuisine", "room:cuisine_auberge/cheminee_cuisine", "item:item.meuble_foyer");
            yield return Linked("C_IN1_A0_12", c, "Table de la cuisine", "room:cuisine_auberge/table_cuisine");
            yield return Linked("C_IN1_A0_13", c, "Étagère de la cuisine", "room:auberge/etagere", "room:cuisine_auberge/etagere_cuisine");
            yield return Waiting("C_IN1_A0_14", c, "Peau de loup au mur", wall);
            yield return Linked("C_IN1_A1_01", c, "Lit de chambre", "room:chambres_auberge/lit_1", "room:chambres_auberge/lit_2", "room:chambres_auberge/lit_3");
            yield return Linked("C_IN1_A1_02", c, "Coffre de chambre 1", "room:chambres_auberge/coffre_1");
            yield return Linked("C_IN1_A1_04", c, "Coffre de chambre 2", "room:chambres_auberge/coffre_2", "room:chambres_auberge/coffre_4");
            yield return Linked("C_IN1_A1_06", c, "Coffre de chambre 3", "room:chambres_auberge/coffre_3");
            yield return Linked("C_IN1_A1_07", c, "Lit de la chambre 4", "room:chambres_auberge/lit_4");
            yield return Linked("C_IN1_A2_01", c, "Lit de Mathurin", "room:logement_auberge/lit_mathurin");
            yield return Linked("C_IN1_A2_02", c, "Coffre de Mathurin", "room:logement_auberge/coffre_mathurin");
            yield return Linked("C_IN1_A2_03", c, "Bureau de Mathurin", "room:logement_auberge/bureau_mathurin");
            yield return Linked("C_IN1_A2_04", c, "Chaise", "room:logement_auberge/chaise_mathurin", "item:item.meuble_chaise");
            yield return Linked("C_IN1_A2_05", c, "Lit de dortoir", "room:logement_auberge/lit_dortoir_1", "room:logement_auberge/lit_dortoir_2", "room:logement_auberge/lit_dortoir_3", "room:logement_auberge/lit_dortoir_4", "room:dortoir_abbaye/lit_dortoir_1", "room:dortoir_abbaye/lit_dortoir_2", "room:dortoir_abbaye/lit_dortoir_3", "room:dortoir_abbaye/lit_dortoir_4");
            yield return Linked("C_IN1_A2_10", c, "Échelle des combles", "room:logement_auberge/echelle_combles");
            yield return Linked("C_IN1_A3_01", c, "Rayonnage à linge", "room:combles_auberge/rayonnage_linge");
            yield return Linked("C_IN1_A3_02", c, "Coffre à draps", "room:combles_auberge/coffre_draps");
            yield return Linked("C_IN1_A3_03", c, "Étagère de la réserve", "room:combles_auberge/etagere_reserve");
            yield return Linked("C_IN1_A3_04", c, "Caisses", "room:auberge/caisses", "room:guilde/caisses", "room:combles_auberge/caisses");
            yield return Waiting("C_IN1_B0_01", c, "Comptoir de la boulangerie", "plus profond (1,07 m) que le comptoir des données (0,6 m) : le comptoir de l'auberge (0,55 m) y est posé à sa place");
            yield return Linked("C_IN1_B0_02", c, "Étagère à pains", "room:boulangerie/etagere_pains");
            yield return Waiting("C_IN1_B0_03", c, "Présentoir", bakery);
            yield return Waiting("C_IN1_B0_05", c, "Table à façonner", bakery);
            yield return Linked("C_IN1_B0_06", c, "Pétrin", "room:boulangerie/petrin");
            yield return Waiting("C_IN1_B0_07", c, "Banneton sur support", bakery);
            yield return Linked("C_IN1_B0_08", c, "Sacs de farine", "room:boulangerie/caisse_farine");
            yield return Waiting("C_IN1_B0_09", c, "Râtelier de pelles", bakery);
            yield return Waiting("C_IN1_B0_10", c, "Gueule et avant-four", "la gueule seule : le four voûté fait par le code reste (sa bouche rougeoyante et sa lumière sont réglées sur lui)");
            yield return Linked("C_IN1_B1_01", c, "Table du logement", "item:item.meuble_table");   // V2.53.2 : meuble de la maison du joueur
            yield return Linked("C_IN1_B1_02", c, "Banc du logement", "item:item.meuble_banc");   // V2.53.2 : meuble de la maison du joueur
            yield return Linked("C_IN1_B1_03", c, "Lit", "item:item.meuble_lit");   // V2.53.2 : meuble de la maison du joueur
            yield return Linked("C_IN1_B1_04", c, "Coffre", "item:item.meuble_coffre");   // V2.53.2 : meuble de la maison du joueur
            yield return Linked("C_IN1_B1_05", c, "Armoire", "item:item.meuble_armoire");   // V2.53.2 : meuble de la maison du joueur
            yield return Linked("C_IN1_B1_07", c, "Étagère à linge", "item:item.meuble_etagere");   // V2.53.2 : meuble de la maison du joueur
            yield return Linked("C_IN1_G0_01", c, "Table des contrats", "room:guilde/table1", "room:guilde/table2", "room:archives/table");
            yield return Waiting("C_IN1_G0_02", c, "Banc des contrats", guild);
            yield return Waiting("C_IN1_G0_03", c, "Tableau des contrats", wall);
            yield return Linked("C_IN1_G0_04", c, "Cheminée", "room:guilde/cheminee", "room:archives/cheminee");
            yield return Waiting("C_IN1_G0_07", c, "Bureau de Brunehilde", guild);
            yield return Waiting("C_IN1_G0_08", c, "Fauteuil", guild);
            yield return Waiting("C_IN1_G0_09", c, "Armoire de la réserve", guild);
            yield return Waiting("C_IN1_G0_10", c, "Coffres d'Aubin", guild);
            yield return Waiting("C_IN1_G0_11", c, "Trophées sur console", guild);
            yield return Linked("C_IN1_G_1_01", c, "Rayonnage", "room:archives/etagere1", "room:archives/etagere2", "room:guilde/etagere");
            yield return Waiting("C_IN1_G_1_06", c, "Lutrin", archives);
            yield return Waiting("C_IN1_G_1_07", c, "Carte des rangs", wall);
            yield return Linked("C_IN1_G_1_09", c, "Coffre des archives", "room:archives/caisse");
            yield return Waiting("C_IN1_G_1_10", c, "Registres en pile", archives);
            yield return Waiting("C_IN1_P01", c, "Banneton", small);
            yield return Linked("C_IN1_P02", c, "Pelle à pain", "piece:boulangerie/Broom");
            yield return Linked("C_IN1_P03", c, "Miche", "piece:boulangerie/Bread");
            yield return Waiting("C_IN1_P04", c, "Sac de farine", small);
            yield return Waiting("C_IN1_P05", c, "Torchon", small);
            yield return Linked("C_IN1_P06", c, "Écuelle", Pieces("Bowl", "auberge guilde"));
            yield return Linked("C_IN1_P07", c, "Gobelet", Pieces("Goblets", "auberge guilde"));
            yield return Linked("C_IN1_P08", c, "Marmite", Pieces("Pot", "auberge guilde archives"));
            yield return Linked("C_IN1_P09", c, "Cruche", Pieces("Bottles", "auberge guilde archives"));
            yield return Waiting("C_IN1_P10", c, "Couverture", small);
            yield return Waiting("C_IN1_P11", c, "Oreiller", small);
            yield return Waiting("C_IN1_P12", c, "Registre de la Guilde", small);
            yield return Waiting("C_IN1_P13", c, "Encrier", small);
            yield return Waiting("C_IN1_P14", c, "Registre des archives", small);
            yield return Waiting("C_IN1_P15", c, "Carte des rangs (roulée)", small);
            yield return Linked("C_IN1_P16", c, "Lanterne", Pieces("Lantern", "auberge guilde boulangerie").Concat(Pieces("HangingLantern", "auberge guilde boulangerie")).ToArray());
            yield return Linked("C_IN1_P17", c, "Bougeoir", Pieces("Candlestick", "auberge guilde archives"));
        }

        // --- 04 : intérieurs du village (chapelle, épicerie, herboristerie, mercerie, maison de Rosalie…) ------------------------------

        private static IEnumerable<LotCModel> InteriorsVillage()
        {
            const string c = "04_Interieurs_Village";
            const string chapel = "pas de place pour ce meuble dans la chapelle des données (autel, huit bancs, deux chandeliers, statue)";
            const string crypt = "crypte de la chapelle : pas dans les données";
            const string grocer = "pas de place pour ce meuble dans l'épicerie des données (comptoir, deux étagères, tonneau, caisse)";
            const string herb = "pas de place pour ce meuble dans l'herboristerie des données (comptoir, deux étagères, table de séchage, tonneau)";
            const string tailor = "pas de place pour ce meuble dans la mercerie des données (comptoir, étagère à toiles, rouet, table de coupe, coffre)";
            const string small = "petit objet que l'aménagement de ces pièces ne pose pas";
            const string attic = "grenier de la maison paysanne (fiche V1) : pas de pièce dans les données (on y monte par l'échelle, un seul niveau joué)";
            const string rosalie = "les tables de la maison ont des tabourets (aménagement) ; aucun banc dans les données";
            const string upstairs = "étage de la maison de Rosalie : pas dans les données (une pièce n'a qu'un niveau)";
            yield return Linked("C_IN2_C0_01", c, "Banc de nef", "room:chapelle/banc1", "room:chapelle/banc2", "room:chapelle/banc3", "room:chapelle/banc4", "room:chapelle/banc5", "room:chapelle/banc6", "room:chapelle/banc7", "room:chapelle/banc8");
            yield return Linked("C_IN2_C0_09", c, "Autel", "room:chapelle/autel");
            yield return Waiting("C_IN2_C0_10", c, "Reliquaire (logement vide de l'Éclat)", chapel);
            yield return Waiting("C_IN2_C0_11", c, "Armoire de sacristie", chapel);
            yield return Waiting("C_IN2_C_1_02", c, "Table votive", crypt);
            yield return Waiting("C_IN2_C_1_03", c, "Coffre scellé", crypt);
            yield return Linked("C_IN2_C_1_04", c, "Support de cierges", "room:chapelle/chandelier1", "room:chapelle/chandelier2");
            yield return Linked("C_IN2_E0_01", c, "Comptoir de vente", "room:epicerie/comptoir", "room:mercerie/comptoir");
            yield return Linked("C_IN2_E0_02", c, "Rayonnage de pots", "room:epicerie/etagere_ouest");
            yield return Linked("C_IN2_E0_03", c, "Étagères à denrées", "room:epicerie/etagere_fond");
            yield return Waiting("C_IN2_E0_04", c, "Table de fromages", grocer);
            yield return Linked("C_IN2_E0_05", c, "Tonneaux de sel", "room:epicerie/tonneau");
            yield return Linked("C_IN2_E0_06", c, "Sacs de réserve", "room:epicerie/caisse");
            yield return Waiting("C_IN2_E0_07", c, "Rayonnage de la réserve", grocer);
            yield return Waiting("C_IN2_E0_08", c, "Bureau de Lucie", grocer);
            yield return Linked("C_IN2_F0_01", c, "Foyer avec hotte", "room:forge_couverte/cheminee_forge");
            yield return Linked("C_IN2_F0_02", c, "Soufflet", "room:forge_couverte/soufflet");
            yield return Linked("C_IN2_F0_03", c, "Enclume sur billot", "room:forge_couverte/enclume", "room:atelier_du_charron/enclume");
            yield return Linked("C_IN2_F0_04", c, "Établi", "room:forge_couverte/etabli", "room:atelier_du_charron/etabli");
            yield return Linked("C_IN2_F0_05", c, "Bac de trempe", "room:forge_couverte/bac_de_trempe");
            yield return Linked("C_IN2_F0_06", c, "Râtelier de pinces", "room:forge_couverte/ratelier_pinces");
            yield return Linked("C_IN2_G0_01", c, "Charrette agricole", "room:grange/charrette");
            yield return Linked("C_IN2_G0_02", c, "Tas de foin", "room:grange/foin");
            yield return Linked("C_IN2_G0_03", c, "Râtelier agricole", "room:grange/ratelier_agricole");
            yield return Linked("C_IN2_G0_04", c, "Coffre à outils", "room:grange/coffre_outils");
            yield return Linked("C_IN2_G0_05", c, "Échelle de travail rangée", "room:grange/echelle");
            yield return Linked("C_IN2_H0_01", c, "Table de tri de Tiphaine", "room:herboristerie/table_sechage");
            yield return Waiting("C_IN2_H0_02", c, "Alambic sur foyer", herb);
            yield return Linked("C_IN2_H0_03", c, "Étagère de bocaux", "room:herboristerie/etagere_fond");
            yield return Linked("C_IN2_H0_04", c, "Étagère interdite en hauteur", "room:herboristerie/etagere_est");
            yield return Linked("C_IN2_H0_05", c, "Petit comptoir", "room:herboristerie/comptoir");
            yield return Waiting("C_IN2_H0_06", c, "Mortier sur billot", herb);
            yield return Waiting("C_IN2_M0_01", c, "Métier à tisser", tailor);
            yield return Linked("C_IN2_M0_02", c, "Table de coupe", "room:mercerie/table_coupe", "room:foulon/table_coupe");
            yield return Linked("C_IN2_M0_03", c, "Rouet", "room:mercerie/rouet", "room:maison/rouet", "room:foulon/rouet");
            yield return Waiting("C_IN2_M0_04", c, "Mannequin de couturière (fait main, sans Tripo)", tailor);
            yield return Linked("C_IN2_M0_05", c, "Rouleaux et étagère", "room:mercerie/etagere_toiles");
            yield return Linked("C_IN2_M0_06", c, "Tabouret", "piece:mercerie/Stool", "piece:maison/Stool");
            yield return Linked("C_IN2_O0_01", c, "Rouet et lanterne", "room:salle_du_moulin/rouet_moulin");
            yield return Linked("C_IN2_O0_02", c, "Bac à farine", "room:salle_du_moulin/bac_a_farine");
            yield return Linked("C_IN2_O0_03", c, "Sacs de grain", "room:salle_du_moulin/sacs_de_grain", "room:etage_des_meules/sacs_en_attente");
            yield return Linked("C_IN2_O0_04", c, "Échelle de meunier", "room:salle_du_moulin/echelle_meunier", "room:etage_des_meules/echelle_trappe");
            yield return Linked("C_IN2_O0_05", c, "Pupitre de pesée", "room:salle_du_moulin/pupitre_pesee");
            yield return Linked("C_IN2_O1_01", c, "Meules avec archure et trémie", "room:etage_des_meules/meules");
            yield return Linked("C_IN2_O1_04", c, "Caisse à grain", "room:etage_des_meules/caisse_a_grain");
            yield return Linked("C_IN2_P01", c, "Cierge", "piece:chapelle/Candlestick");
            yield return Linked("C_IN2_P02", c, "Bougeoir", "piece:maison/Candlestick");
            yield return Waiting("C_IN2_P03", c, "Livre de la chapelle", small);
            yield return Waiting("C_IN2_P04", c, "Pot de miel", small);
            yield return Waiting("C_IN2_P05", c, "Fromage", small);
            yield return Waiting("C_IN2_P06", c, "Tresse d'ail", small);
            yield return Linked("C_IN2_P08", c, "Bocal", "piece:herboristerie/ClayPot", "piece:epicerie/ClayPot");
            yield return Waiting("C_IN2_P09", c, "Mortier", small);
            yield return Waiting("C_IN2_P10", c, "Bouquet d'herbes", small);
            yield return Linked("C_IN2_P11", c, "Fiole", "piece:herboristerie/Bottles");
            yield return Waiting("C_IN2_P12", c, "Ciseaux", small);
            yield return Linked("C_IN2_P13", c, "Rouleau de lin", "piece:mercerie/Cloth");
            yield return Waiting("C_IN2_P14", c, "Écheveau", small);
            yield return Waiting("C_IN2_P15", c, "Bobine", small);
            yield return Linked("C_IN2_P16", c, "Assiette", "piece:maison/Plate");
            yield return Linked("C_IN2_P17", c, "Cruche", "piece:maison/Bottles", "piece:epicerie/Bottles");
            yield return Linked("C_IN2_P18", c, "Panier", "piece:maison/Basket", "piece:epicerie/Basket", "piece:herboristerie/Basket", "piece:mercerie/Basket");
            yield return Linked("C_IN2_P20", c, "Écuelle", "piece:maison/Bowl");
            yield return Waiting("C_IN2_P21", c, "Couverture", small);
            yield return Waiting("C_IN2_P22", c, "Oreiller", small);
            yield return Waiting("C_IN2_P23", c, "Pelle à grain", small);
            yield return Waiting("C_IN2_P25", c, "Tamis", small);
            yield return Waiting("C_IN2_P26", c, "Pinces", small);
            yield return Waiting("C_IN2_P27", c, "Marteau", small);
            yield return Waiting("C_IN2_P28", c, "Râteau", small);
            yield return Waiting("C_IN2_P29", c, "Fourche", small);
            yield return Waiting("C_IN2_P30", c, "Registre", small);
            yield return Linked("C_IN2_R0_01", c, "Lit clos", "room:maison/lit");
            yield return Linked("C_IN2_R0_02", c, "Cheminée domestique", "room:maison/cheminee");
            yield return Linked("C_IN2_R0_03", c, "Table familiale", "room:maison/table");
            yield return Waiting("C_IN2_R0_04", c, "Banc", rosalie);
            yield return Linked("C_IN2_R0_05", c, "Coffre", "room:maison/coffre");
            yield return Linked("C_IN2_R0_07", c, "Étagère à vaisselle", "room:maison/etagere");
            yield return Waiting("C_IN2_R1_02", c, "Coffre à linge", upstairs);
            yield return Waiting("C_IN2_R1_03", c, "Étagère à paniers", upstairs);
            yield return Linked("C_IN2_T0_01", c, "Couchettes superposées", "room:salle_de_garde/lit_gardes", "room:garde_du_signal/lit_gardes");
            yield return Linked("C_IN2_T0_02", c, "Table du registre", "room:salle_de_garde/table_registre", "room:garde_du_signal/table_registre");
            yield return Linked("C_IN2_T0_03", c, "Râtelier d'armes", "room:salle_de_garde/ratelier_armes", "room:garde_du_signal/ratelier_armes");
            yield return Linked("C_IN2_T0_05", c, "Coffre des gardes", "room:salle_de_garde/coffre_gardes", "room:garde_du_signal/coffre_gardes");
            yield return Linked("C_IN2_T0_06", c, "Lanterne suspendue", "piece:maison/HangingLantern", "piece:epicerie/HangingLantern", "piece:herboristerie/HangingLantern", "piece:mercerie/HangingLantern", "piece:salle_de_garde/HangingLantern");
            yield return Linked("C_IN2_V0_01", c, "Lit paysan", "room:maison_paysanne/lit", "room:relais_du_pont_interieur/lit");
            yield return Linked("C_IN2_V0_02", c, "Cheminée", "room:maison_paysanne/cheminee");
            yield return Linked("C_IN2_V0_03", c, "Table simple", "room:maison_paysanne/table", "room:relais_du_pont_interieur/table");
            yield return Linked("C_IN2_V0_04", c, "Coffre", "room:mercerie/coffre", "room:maison_paysanne/coffre", "room:atelier_du_charron/coffre", "room:dortoir_abbaye/coffre");
            yield return Linked("C_IN2_V0_05", c, "Étagère", "room:maison_paysanne/etagere");
            yield return Linked("C_IN2_V0_06", c, "Échelle du grenier", "room:maison_paysanne/echelle_grenier");
            yield return Waiting("C_IN2_V1_02", c, "Caisses basses", attic);
            yield return Waiting("C_IN2_V1_03", c, "Ballot de linge", attic);
        }

        // --- 05 et 06 : intérieurs des nouveaux lieux (fiches I01 à I21) ---------------------------------------------------------------

        private static IEnumerable<LotCModel> NewPlaces()
        {
            const string none = "intérieur d'un nouveau lieu (fiches I01 à I21) : ni la pièce ni le lieu ne sont encore dans les données (V2.43.4 et V2.56.2 : dix-neuf meubles reliés aux intérieurs des lieux reconstruits ; les 10 autres, trop grands ou sans place qui leur convienne, attendent leur lieu)";
            yield return Linked("C_IN3_I01a_05", "05_Nouveaux_Lieux_1", "Buste sur socle", "room:dortoir_abbaye/statue_buste");   // V2.56.2 : meuble posé dans un lieu reconstruit
            yield return Waiting("C_IN3_I02a_05", "05_Nouveaux_Lieux_1", "Forge maçonnée", none);
            yield return Waiting("C_IN3_I03a_02", "05_Nouveaux_Lieux_1", "Chevalet à écharner", none);
            yield return Linked("C_IN3_I03a_05", "05_Nouveaux_Lieux_1", "Peaux pliées sur claie", "room:foulon/ratelier_peaux");   // V2.56.2 : meuble posé dans un lieu reconstruit
            yield return Linked("C_IN3_I04a_04", "05_Nouveaux_Lieux_1", "Étagère à bocaux", "room:atelier_du_charron/etagere_bocaux");   // V2.56.2 : meuble posé dans un lieu reconstruit
            yield return Linked("C_IN3_I05a_06", "05_Nouveaux_Lieux_1", "Étagère à étoffes", "room:foulon/etagere_etoffes");   // V2.43.4 : intérieur du lieu reconstruit
            yield return Waiting("C_IN3_I06b_02", "05_Nouveaux_Lieux_1", "Foyer de cuisson", none);
            yield return Waiting("C_IN3_I06b_04", "05_Nouveaux_Lieux_1", "Dressoir", none);
            yield return Linked("C_IN3_I06b_05", "05_Nouveaux_Lieux_1", "Évier de pierre", "room:relais_du_pont_interieur/bac_evier");   // V2.56.2 : meuble posé dans un lieu reconstruit
            yield return Linked("C_IN3_I07d_03", "05_Nouveaux_Lieux_1", "Armoire à vêtements", "room:dortoir_abbaye/armoire_vetements");   // V2.56.2 : meuble posé dans un lieu reconstruit
            yield return Linked("C_IN3_I09c_01", "05_Nouveaux_Lieux_1", "Cloche suspendue / projection", "room:garde_du_signal/cloche_signal");   // V2.56.2 : meuble posé dans un lieu reconstruit
            yield return Linked("C_IN3_I10b_02", "05_Nouveaux_Lieux_1", "Seau couvert", "room:foulon/bac_eau");   // V2.43.4 : intérieur du lieu reconstruit
            yield return Linked("C_IN3_I11a_03", "06_Nouveaux_Lieux_2", "Bois sur tasseaux", "room:atelier_du_charron/ratelier_bois");   // V2.43.4 : intérieur du lieu reconstruit
            yield return Linked("C_IN3_I12b_02", "06_Nouveaux_Lieux_2", "Dressoir", "room:relais_du_pont_interieur/etagere_dressoir");   // V2.56.2 : meuble posé dans un lieu reconstruit
            yield return Linked("C_IN3_I12d_04", "06_Nouveaux_Lieux_2", "Garde-robe", "room:dortoir_abbaye/armoire_garde_robe");   // V2.56.2 : meuble posé dans un lieu reconstruit
            yield return Waiting("C_IN3_I13a_05", "06_Nouveaux_Lieux_2", "Âtre du logis", none);
            yield return Waiting("C_IN3_I13c_04", "06_Nouveaux_Lieux_2", "Garde-robe", none);
            yield return Waiting("C_IN3_I14a_01", "06_Nouveaux_Lieux_2", "Table et deux bancs", none);
            yield return Waiting("C_IN3_I15b_01", "06_Nouveaux_Lieux_2", "Claie à fromages", none);
            yield return Linked("C_IN3_I15b_02", "06_Nouveaux_Lieux_2", "Table d'égouttage", "room:foulon/table_egouttage");   // V2.56.2 : meuble posé dans un lieu reconstruit
            yield return Linked("C_IN3_I16a_01", "06_Nouveaux_Lieux_2", "Banc de sas", "room:garde_du_signal/banc_sas");   // V2.56.2 : meuble posé dans un lieu reconstruit
            yield return Linked("C_IN3_I16b_05", "06_Nouveaux_Lieux_2", "Foyer protégé", "room:garde_du_signal/cheminee");   // V2.43.4 : intérieur du lieu reconstruit
            yield return Linked("C_IN3_I16b_07", "06_Nouveaux_Lieux_2", "Réserve de couvertures", "room:dortoir_abbaye/etagere_couvertures");   // V2.43.4 : intérieur du lieu reconstruit
            yield return Linked("C_IN3_I17a_01", "06_Nouveaux_Lieux_2", "Comptoir bas", "room:relais_du_pont_interieur/comptoir");   // V2.43.4 : intérieur du lieu reconstruit
            yield return Linked("C_IN3_I17a_04", "06_Nouveaux_Lieux_2", "Âtre", "room:relais_du_pont_interieur/cheminee");   // V2.43.4 : intérieur du lieu reconstruit
            yield return Linked("C_IN3_I18a_04", "06_Nouveaux_Lieux_2", "Râtelier de haches", "room:atelier_du_charron/ratelier_haches");   // V2.56.2 : meuble posé dans un lieu reconstruit
            yield return Waiting("C_IN3_I19a_04", "06_Nouveaux_Lieux_2", "Filet sur traverse", none);
            yield return Waiting("C_IN3_I20a_03", "06_Nouveaux_Lieux_2", "Étagère de bocaux", none);
            yield return Linked("C_IN3_I21a_04", "06_Nouveaux_Lieux_2", "Tableau des équipes", "room:garde_du_signal/tableau_equipes");   // V2.56.2 : meuble posé dans un lieu reconstruit
        }
    }
}
