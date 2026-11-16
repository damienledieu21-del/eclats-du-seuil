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
    }
}
