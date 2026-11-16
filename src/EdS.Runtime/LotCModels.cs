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
    }
}
