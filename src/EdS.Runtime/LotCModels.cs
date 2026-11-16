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

        /// <summary>Le modèle de ce code, ou null.</summary>
        public static LotCModel? Find(string? code) => code == null ? null : All.FirstOrDefault(m => m.Code == code);

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

        private static string? Target(string target) => All.FirstOrDefault(m => m.Targets.Contains(target))?.Code;

        /// <summary>Modèle du nœud de récolte <paramref name="nodeId"/> (« filon_ouest »), ou null.</summary>
        public static string? ForNode(string? nodeId) => nodeId == null ? null : Target("node:" + nodeId);

        /// <summary>Modèle de l'objet <paramref name="itemId"/> du catalogue (« item.cepe »), ou null.</summary>
        public static string? ForItem(string? itemId) => itemId == null ? null : Target("item:" + itemId);

        /// <summary>
        /// Modèle d'un décor du village posé par le jeu (<paramref name="key"/> : « wave5/FlourSack », « prop/FirePit », « forge/enclume »),
        /// ou null. Plusieurs modèles pour un même décor (les trois sacs de farine) : celui tiré de <paramref name="id"/>.
        /// </summary>
        public static string? ForVillage(string? key, string? id)
        {
            if (key == null) return null;
            string target = "village:" + key;
            List<LotCModel> list = All.Where(m => m.Targets.Contains(target)).ToList();
            return list.Count == 0 ? null : list[StyleRules.Variant(id ?? string.Empty, list.Count)].Code;
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
        public static bool Replaces(string obstacleId, InteriorPiece piece) =>
            piece != null && (piece.Id == obstacleId || piece.Id.StartsWith(obstacleId + "/", StringComparison.Ordinal)) && piece.Parent < 0
            && Array.IndexOf(Body, piece.Prop) >= 0;

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
            double roomMaxX, double roomMaxZ, double modelW, double modelD)
        {
            if (!(modelW > 0 && modelD > 0 && maxX > minX && maxZ > minZ)) return Array.Empty<LotCCopy>();
            double w = maxX - minX, d = maxZ - minZ, cx = (minX + maxX) / 2, cz = (minZ + maxZ) / 2;
            // Dos au mur le plus proche : l'avant regarde l'intérieur de la pièce.
            double west = minX - roomMinX, east = roomMaxX - maxX, south = minZ - roomMinZ, north = roomMaxZ - maxZ;
            double near = Math.Min(Math.Min(west, east), Math.Min(south, north));
            double yaw = near == south ? 0 : near == north ? 180 : near == west ? 90 : 270;
            // Côtés du modèle une fois tourné : à 90 ou 270°, sa largeur va selon z.
            bool turned = yaw == 90 || yaw == 270;
            double alongX = turned ? modelD : modelW, alongZ = turned ? modelW : modelD;
            bool boxAlongX = w >= d, modelAlongX = alongX >= alongZ;
            if (boxAlongX != modelAlongX && Math.Abs(alongX - alongZ) > 0.05)
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
            // Le grand côté du modèle suit celui de l'emprise : l'amincissement porte sur son petit côté (x ou z du modèle).
            double thinX = modelW < modelD ? thin : 1, thinZ = modelW < modelD ? 1 : thin;
            var copies = new List<LotCCopy>();
            double step = boxLong / count;
            for (int i = 0; i < count; i++)
            {
                double t = -boxLong / 2 + step * (i + 0.5);
                copies.Add(new LotCCopy(boxAlongX ? cx + t : cx, boxAlongX ? cz : cz + t, yaw, scale, thinX, thinZ));
            }
            return copies;
        }

        /// <summary>Décors du village que le jeu pose à des places fixes (sans rôle ni objet) et qu'un modèle du lot C peut remplacer.</summary>
        public static IReadOnlyList<string> VillageSpots { get; } = new[] { "forge/enclume" };

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
            const string forge = "pièce de forge que le jeu ne pose pas encore (la forge de Maëlle n'a que le foyer, l'enclume, le marteau, un tonneau et une caisse)";
            const string light = "éclairage de nuit fait par le code (NightLamps : hauteur de la lumière réglée sur ses lanternes) : à brancher avec un essai dans Unity";
            const string street = "aucune place dans les données des Vallons pour ce mobilier de rue";
            const string market = "aucune place dans les données ni dans les décors du marché posés par le jeu";
            const string feast = "décor de fête sans place dans les données ni parmi les décors de fête posés par le jeu (guirlande, bannières)";
            yield return Waiting("C_OV_A01", c, "Auvent de la forge", forge);
            yield return Linked("C_OV_A02", c, "Enclume sur billot", "village:forge/enclume");
            yield return Linked("C_OV_A03", c, "Foyer de forge en pierre", "village:prop/FirePit");
            yield return Waiting("C_OV_A04", c, "Soufflet manuel", forge);
            yield return Waiting("C_OV_A05", c, "Établi avec outils", forge);
            yield return Waiting("C_OV_A06_01", c, "Petit marteau", forge);
            yield return Linked("C_OV_A06_02", c, "Marteau moyen", "village:prop/Hammer");
            yield return Waiting("C_OV_A06_03", c, "Grand marteau", forge);
            yield return Waiting("C_OV_A06_04", c, "Petite pince", forge);
            yield return Waiting("C_OV_A06_05", c, "Pince moyenne", forge);
            yield return Waiting("C_OV_A06_06", c, "Grande pince", forge);
            yield return Waiting("C_OV_A06_07", c, "Petite lime", forge);
            yield return Waiting("C_OV_A06_08", c, "Grande lime", forge);
            yield return Waiting("C_OV_A07", c, "Bac de trempe", forge);
            yield return Waiting("C_OV_A08", c, "Râtelier d'armes", forge);
            yield return Waiting("C_OV_A09", c, "Présentoir de fers à cheval", forge);
            yield return Waiting("C_OV_A10", c, "Tas de charbon", forge);
            yield return Waiting("C_OV_A11", c, "Meule à aiguiser à pédale", forge);
            yield return Waiting("C_OV_B01", c, "Réverbère en fer forgé", light);
            yield return Waiting("C_OV_B02", c, "Lanterne sur poteau de bois", light);
            yield return Waiting("C_OV_B03", c, "Lanterne murale", light);
            yield return Waiting("C_OV_B04", c, "Brasero", street);
            yield return Waiting("C_OV_B05", c, "Torche murale", light);
            yield return Waiting("C_OV_B06_01", c, "Petite bougie sur bougeoir", "bougies des intérieurs : reliées avec les intérieurs (lot C, PR suivantes) si une pièce en a la place");
            yield return Waiting("C_OV_B06_02", c, "Bougie moyenne sur bougeoir", "bougies des intérieurs : reliées avec les intérieurs (lot C, PR suivantes) si une pièce en a la place");
            yield return Waiting("C_OV_B06_03", c, "Grande bougie sur bougeoir", "bougies des intérieurs : reliées avec les intérieurs (lot C, PR suivantes) si une pièce en a la place");
            yield return Waiting("C_OV_B07", c, "Lanterne de fête en papier", feast);
            yield return Waiting("C_OV_C01", c, "Banc de pierre", street);
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
            const string inn = "chambres, dortoir, réserve et cuisine de l'auberge : pas dans les données (l'auberge est une seule salle, une pièce n'a qu'un niveau)";
            const string bakery = "pas de place pour ce meuble dans la boulangerie des données (comptoir, four, pétrin, étagère à pains, farine, tonneau)";
            const string lodging = "logement de Firmin : pas dans les données";
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
            yield return Waiting("C_IN1_A0_11", c, "Foyer de la cuisine", inn);
            yield return Waiting("C_IN1_A0_12", c, "Table de la cuisine", inn);
            yield return Linked("C_IN1_A0_13", c, "Étagère de la cuisine", "room:auberge/etagere");
            yield return Waiting("C_IN1_A0_14", c, "Peau de loup au mur", wall);
            yield return Waiting("C_IN1_A1_01", c, "Lit de chambre", inn);
            yield return Waiting("C_IN1_A1_02", c, "Coffre de chambre 1", inn);
            yield return Waiting("C_IN1_A1_04", c, "Coffre de chambre 2", inn);
            yield return Waiting("C_IN1_A1_06", c, "Coffre de chambre 3", inn);
            yield return Waiting("C_IN1_A1_07", c, "Lit de la chambre 4", inn);
            yield return Waiting("C_IN1_A2_01", c, "Lit de Mathurin", inn);
            yield return Waiting("C_IN1_A2_02", c, "Coffre de Mathurin", inn);
            yield return Waiting("C_IN1_A2_03", c, "Bureau de Mathurin", inn);
            yield return Waiting("C_IN1_A2_04", c, "Chaise", inn);
            yield return Waiting("C_IN1_A2_05", c, "Lit de dortoir", inn);
            yield return Waiting("C_IN1_A2_10", c, "Échelle des combles", inn);
            yield return Waiting("C_IN1_A3_01", c, "Rayonnage à linge", inn);
            yield return Waiting("C_IN1_A3_02", c, "Coffre à draps", inn);
            yield return Waiting("C_IN1_A3_03", c, "Étagère de la réserve", inn);
            yield return Linked("C_IN1_A3_04", c, "Caisses", "room:auberge/caisses", "room:guilde/caisses");
            yield return Waiting("C_IN1_B0_01", c, "Comptoir de la boulangerie", "plus profond (1,07 m) que le comptoir des données (0,6 m) : le comptoir de l'auberge (0,55 m) y est posé à sa place");
            yield return Linked("C_IN1_B0_02", c, "Étagère à pains", "room:boulangerie/etagere_pains");
            yield return Waiting("C_IN1_B0_03", c, "Présentoir", bakery);
            yield return Waiting("C_IN1_B0_05", c, "Table à façonner", bakery);
            yield return Linked("C_IN1_B0_06", c, "Pétrin", "room:boulangerie/petrin");
            yield return Waiting("C_IN1_B0_07", c, "Banneton sur support", bakery);
            yield return Linked("C_IN1_B0_08", c, "Sacs de farine", "room:boulangerie/caisse_farine");
            yield return Waiting("C_IN1_B0_09", c, "Râtelier de pelles", bakery);
            yield return Waiting("C_IN1_B0_10", c, "Gueule et avant-four", "la gueule seule : le four voûté fait par le code reste (sa bouche rougeoyante et sa lumière sont réglées sur lui)");
            yield return Waiting("C_IN1_B1_01", c, "Table du logement", lodging);
            yield return Waiting("C_IN1_B1_02", c, "Banc du logement", lodging);
            yield return Waiting("C_IN1_B1_03", c, "Lit", lodging);
            yield return Waiting("C_IN1_B1_04", c, "Coffre", lodging);
            yield return Waiting("C_IN1_B1_05", c, "Armoire", lodging);
            yield return Waiting("C_IN1_B1_07", c, "Étagère à linge", lodging);
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
    }
}
