using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Saves;

namespace EdS.Runtime
{
    /// <summary>
    /// Pâtisseries de Firmin (dossier de Damien « Les pains et pâtisseries », V1.0, 19 novembre 2026) : un produit de la boulangerie, son
    /// nom court (icône peinte et textes : <c>text.firmin.&lt;court&gt;</c>), et ses modèles Tripo (<see cref="LotCModels"/>, catégorie
    /// <see cref="LotCModels.FirminCategory"/>) : un seul, ou trois niveaux de réussite (N1 raté, N2 correct, N3 maîtrisé) pour les trois
    /// pièces de concours.
    /// </summary>
    public sealed class FirminPastry
    {
        internal FirminPastry(string item, string code, bool levels, string name)
        {
            Item = item;
            Code = code;
            Levels = levels;
            Name = name;
        }

        /// <summary>Objet du catalogue (« item.couronne_des_moissons »).</summary>
        public string Item { get; }

        /// <summary>Code du dossier (« FIR01 ») : le modèle lui-même sans niveaux, sinon la racine des trois (« FIR01_N1 »…).</summary>
        public string Code { get; }

        /// <summary>Pièce de concours à trois niveaux de réussite.</summary>
        public bool Levels { get; }

        /// <summary>Nom du dossier (pour les modèles du lot C).</summary>
        public string Name { get; }

        /// <summary>Nom court : l'identifiant sans « item. » (icône peinte de base, clé des textes).</summary>
        public string Short => Item.Substring("item.".Length);

        /// <summary>Code du modèle du niveau <paramref name="level"/> (1 à 3), ou le modèle unique.</summary>
        public string ModelCode(int level) => Levels ? Code + "_N" + Math.Max(1, Math.Min(3, level)) : Code;
    }

    /// <summary>
    /// Pâtisseries de Firmin : table du dossier, niveau de réussite d'une pièce (sa qualité de fabrication et sa marque), modèle, icône et
    /// texte qui vont avec, et ce que montre l'étal du fournil. Affichage seulement : le moteur, les récompenses et les données ne
    /// dépendent pas de cette table. Sans Unity.
    /// </summary>
    public static class FirminPastries
    {
        /// <summary>La boulangerie de Firmin (offres du jour : <c>merchants.json</c>).</summary>
        public const string Bakery = "merchant.boulangerie";

        /// <summary>Pièces posées sur l'étal du fournil au plus (trois de front, deux rangs).</summary>
        public const int ShowcaseSlots = 6;

        /// <summary>Les dix produits du dossier, dans l'ordre de l'étal : les pièces de fête, les viennoiseries, les pains, puis la pâte et le levain.</summary>
        public static IReadOnlyList<FirminPastry> All { get; } = new[]
        {
            new FirminPastry("item.couronne_des_moissons", "FIR01", true, "Couronne des moissons"),
            new FirminPastry("item.fouace_des_semailles", "FIR03", true, "Fouace des Semailles"),
            new FirminPastry("item.tarte_renversee_aux_pommes", "FIR02", true, "Tarte renversée aux pommes"),
            new FirminPastry("item.brioche_doree", "FIR10", false, "Brioche dorée"),
            new FirminPastry("item.croissant_au_beurre", "FIR08", false, "Croissant au beurre"),
            new FirminPastry("item.chausson_aux_pommes", "FIR09", false, "Chausson aux pommes du verger"),
            new FirminPastry("item.pain_de_campagne", "FIR05", false, "Pain de campagne au levain"),
            new FirminPastry("item.miche_de_seigle", "FIR06", false, "Miche de seigle au levain"),
            new FirminPastry("item.pate_feuilletee", "FIR07", false, "Pâte feuilletée"),
            new FirminPastry("item.levain", "FIR04", false, "Levain chef dans son bocal"),
        };

        private static readonly Dictionary<string, FirminPastry> ByItem = All.ToDictionary(p => p.Item, StringComparer.Ordinal);

        /// <summary>Le produit de Firmin de l'objet <paramref name="itemId"/>, ou null.</summary>
        public static FirminPastry? Of(string? itemId) => itemId != null && ByItem.TryGetValue(itemId, out FirminPastry? p) ? p : null;

        /// <summary>L'objet a-t-il son icône peinte du dossier (elle passe alors devant le pack d'icônes de l'Asset Store) ?</summary>
        public static bool HasOwnIcon(string? itemId) => Of(itemId) != null;

        /// <summary>
        /// Niveau de réussite (1 raté, 2 correct, 3 maîtrisé) d'une pièce de qualité <paramref name="quality"/> : une pièce de la main du
        /// joueur (<paramref name="signed"/>) suit sa qualité (normale : 1, supérieure : 2, chef-d'œuvre : 3) ; une pièce sans marque
        /// vient de l'étal de Firmin, maître boulanger : 3. Proposition à valider par Damien (question de la PR).
        /// </summary>
        public static int Level(ItemQuality quality, bool signed) =>
            !signed ? 3 : quality == ItemQuality.Masterwork ? 3 : quality == ItemQuality.Superior ? 2 : 1;

        /// <summary>Qualité d'un nom de sauvegarde (« normal », « superior », « masterwork »), ou null.</summary>
        public static ItemQuality? QualityOf(string? name) => name switch
        {
            "normal" => ItemQuality.Normal,
            "superior" => ItemQuality.Superior,
            "masterwork" => ItemQuality.Masterwork,
            _ => null,
        };

        /// <summary>Modèle du lot C d'une pièce (niveau tiré de sa qualité et de sa marque), ou null hors du dossier.</summary>
        public static string? Model(string? itemId, ItemQuality quality, bool signed) => Of(itemId)?.ModelCode(Level(quality, signed));

        /// <summary>
        /// Icône peinte d'une pièce : <paramref name="icon"/> (celle du catalogue : le niveau maîtrisé), ou « &lt;icône&gt;_n1 » et
        /// « &lt;icône&gt;_n2 » pour une pièce de concours ratée ou correcte. Hors du dossier : <paramref name="icon"/> tel quel.
        /// </summary>
        public static string Icon(string? itemId, string icon, ItemQuality quality, bool signed)
        {
            FirminPastry? p = Of(itemId);
            if (p is null || !p.Levels || string.IsNullOrEmpty(icon)) return icon;
            int level = Level(quality, signed);
            return level == 3 ? icon : icon + "_n" + level;
        }

        /// <summary>
        /// Clé du texte d'une pièce (ce qui se lit dans la pièce, d'après le dossier) : <c>text.firmin.&lt;court&gt;</c>, ou, pour une pièce
        /// de concours, <c>text.firmin.&lt;court&gt;.n1</c> à <c>.n3</c> ; null hors du dossier.
        /// </summary>
        public static string? TextKey(string? itemId, ItemQuality quality, bool signed)
        {
            FirminPastry? p = Of(itemId);
            if (p is null) return null;
            return "text.firmin." + p.Short + (p.Levels ? ".n" + Level(quality, signed) : string.Empty);
        }

        /// <summary>
        /// Modèles posés sur l'étal du fournil : un par produit du dossier en vente (<paramref name="onSale"/> : objets encore en vente
        /// aujourd'hui à la boulangerie, dans n'importe quel ordre), dans l'ordre de <see cref="All"/>, au plus <see cref="ShowcaseSlots"/>.
        /// Les pièces de concours de l'étal sont celles de Firmin (niveau maîtrisé).
        /// </summary>
        public static IReadOnlyList<string> Showcase(IEnumerable<string>? onSale)
        {
            var sold = new HashSet<string>(onSale ?? Array.Empty<string>(), StringComparer.Ordinal);
            return All.Where(p => sold.Contains(p.Item)).Select(p => p.ModelCode(3)).Take(ShowcaseSlots).ToList().AsReadOnly();
        }
    }
}
