using System;
using System.Collections.Generic;
using EdS.Data.Seasons;

namespace EdS.Runtime
{
    /// <summary>Rendu d'un rang de vigne pour un état (couleur des feuilles, part de feuillage, grappes).</summary>
    public readonly struct VineRowLook : IEquatable<VineRowLook>
    {
        public VineRowLook(float r, float g, float b, float leaves, bool grapes)
        {
            R = r;
            G = g;
            B = b;
            Leaves = leaves;
            Grapes = grapes;
        }

        public float R { get; }
        public float G { get; }
        public float B { get; }

        /// <summary>Part du feuillage montrée (0 : cep nu, seuls les piquets et le fil ; 1 : rang plein).</summary>
        public float Leaves { get; }

        /// <summary>Grappes mûres sur le rang (vendanges).</summary>
        public bool Grapes { get; }

        public bool Equals(VineRowLook o) => R == o.R && G == o.G && B == o.B && Leaves == o.Leaves && Grapes == o.Grapes;
        public override bool Equals(object? obj) => obj is VineRowLook l && Equals(l);
        public override int GetHashCode() => (int)(R * 1000) ^ ((int)(Leaves * 100) << 8) ^ (Grapes ? 1 << 20 : 0);
    }

    /// <summary>Objet de vendange posé près des rangs (hotte, panier) : clé, place absolue (m), quart de tour.</summary>
    public readonly struct VintageProp
    {
        public VintageProp(string key, double x, double z, double yaw)
        {
            Key = key;
            X = x;
            Z = z;
            Yaw = yaw;
        }

        public string Key { get; }
        public double X { get; }
        public double Z { get; }
        public double Yaw { get; }
    }

    /// <summary>
    /// Sous-lot V2.44.4 (vendanges, décisions de Damien du 20 novembre 2026) : règles portables de l'affichage de la vigne des coteaux du
    /// château, appliquées par Unity (<c>SpaceView.Vines</c>) : les 24 rangs (obstacles « haie_vigne_vignes_… », jusqu'ici habillés en murets
    /// de pierres sèches) deviennent des rangs de vigne (piquets, fil, feuillage) en trois états : vert (feuillage plein), mûr (feuillage
    /// sombre et grappes, septembre), vendangé (feuillage d'automne clairsemé en octobre et novembre, cep nu l'hiver et jusqu'au vert
    /// d'avril). Pendant les vendanges, la hotte et le panier du lot C attendent au bout des rangs. Modèles Tripo des rangs en trois états
    /// (90 crédits, dépense validée par Damien le 20 novembre 2026, <see cref="Models"/>) ; sans eux, formes faites par le code. Affichage
    /// seulement.
    /// </summary>
    public static class VineLook
    {
        /// <summary>Début de l'identifiant des rangs (le même que <c>vine.rows</c> du contenu saisonnier).</summary>
        public const string RowPrefix = "haie_vigne_vignes_";

        /// <summary>Écart des piquets le long d'un rang (m).</summary>
        public const double StakeSpacing = 5.0;

        /// <summary>Écart des grappes le long d'un rang mûr (m).</summary>
        public const double GrapeSpacing = 2.5;

        public static bool IsRow(string? id) => id != null && id.StartsWith(RowPrefix, StringComparison.Ordinal);

        /// <summary>
        /// Rendu du rang pour l'état <paramref name="state"/> de la vigne et la saison <paramref name="season"/> (0 printemps … 3 hiver) ;
        /// sans état (date non fournie) : vert.
        /// </summary>
        public static VineRowLook Look(VineState? state, int season)
        {
            switch (state)
            {
                case VineState.Ripe: return new VineRowLook(0.20f, 0.33f, 0.13f, 1f, true);
                case VineState.Harvested:
                    // Vendangé : l'automne, feuilles dorées et rousses qui tombent ; l'hiver et la fin de l'hiver, cep nu.
                    return season == 2 ? new VineRowLook(0.72f, 0.48f, 0.16f, 0.6f, false) : new VineRowLook(0f, 0f, 0f, 0f, false);
                default: return new VineRowLook(0.27f, 0.47f, 0.17f, 1f, false);
            }
        }

        /// <summary>
        /// Places le long du grand axe d'un rang (rectangle <paramref name="minX"/> à <paramref name="maxZ"/>) tous les
        /// <paramref name="spacing"/> m, à mi-largeur, les deux bouts compris : (x, z).
        /// </summary>
        public static IReadOnlyList<(double X, double Z)> Along(double minX, double minZ, double maxX, double maxZ, double spacing)
        {
            if (!(spacing > 0)) throw new ArgumentOutOfRangeException(nameof(spacing));
            bool alongX = maxX - minX >= maxZ - minZ;
            double length = alongX ? maxX - minX : maxZ - minZ, mid = alongX ? (minZ + maxZ) / 2 : (minX + maxX) / 2;
            int count = Math.Max(1, (int)Math.Floor(length / spacing + 1e-9));
            var spots = new List<(double, double)>(count + 1);
            for (int i = 0; i <= count; i++)
            {
                double t = (alongX ? minX : minZ) + length * i / count;
                spots.Add(alongX ? (t, mid) : (mid, t));
            }
            return spots;
        }

        /// <summary>
        /// Hotte et panier de vendange (C_RE_S07, C_RE_S08) au bout des rangs ouest (allée entre les rangs 2 et 3) et est (entre les rangs
        /// 17 et 18), sur le passage libre de 3,5 m entre deux rangs, hors des nœuds de cueillette ; montrés pendant les vendanges seulement.
        /// </summary>
        public static IReadOnlyList<VintageProp> Props { get; } = new[]
        {
            new VintageProp("vendanges/hotte", -1412.5, -694.75, 90),
            new VintageProp("vendanges/panier", -1247.5, -684.25, 0),
        };

        /// <summary>
        /// Modèles Tripo des rangs en trois états (Decor/vendanges, 20 novembre 2026, 90 crédits), avec leur taille d'export (m, longueur X,
        /// profondeur Z, hauteur) : posés par modules de <see cref="ModuleLength"/> m le long de chaque rang, étirés à sa longueur et à sa
        /// largeur.
        /// </summary>
        public static IReadOnlyDictionary<string, (double W, double D, double H)> Models { get; } = new Dictionary<string, (double, double, double)>(StringComparer.Ordinal)
        {
            ["rang_vigne_vert"] = (3.19, 1.84, 1.6),
            ["rang_vigne_mur"] = (2.4, 2.13, 1.61),
            ["rang_vigne_vendange"] = (4.27, 0.84, 1.6),
        };

        /// <summary>Longueur (m) d'un module de rang (8 par rang de 40 m).</summary>
        public const double ModuleLength = 5.0;

        /// <summary>Largeur (m) d'un rang de modèle (le rang du moteur fait 0,8 m).</summary>
        public const double ModuleWidth = 0.9;

        /// <summary>
        /// Pièce de modèle du rang pour l'état <paramref name="state"/> et la saison <paramref name="season"/> : vert, mûr, vendangé (l'automne),
        /// ou null (cep nu de l'hiver et de la fin de l'hiver : seuls les piquets et le fil).
        /// </summary>
        public static string? ModelPiece(VineState? state, int season) => state switch
        {
            VineState.Ripe => "rang_vigne_mur",
            VineState.Harvested => season == 2 ? "rang_vigne_vendange" : null,
            _ => "rang_vigne_vert",
        };

        /// <summary>Échelle (x, y, z) d'un module de la pièce <paramref name="piece"/> étiré à <paramref name="length"/> m de long.</summary>
        public static (double X, double Y, double Z) ModuleScale(string piece, double length)
        {
            if (!Models.TryGetValue(piece, out (double W, double D, double H) size)) throw new ArgumentException("Pièce de rang inconnue " + piece, nameof(piece));
            if (!(length > 0)) throw new ArgumentOutOfRangeException(nameof(length));
            return (length / size.W, 1.0, ModuleWidth / size.D);
        }

        /// <summary>Les objets de vendange sont posés : vigne mûre (septembre).</summary>
        public static bool PropsShown(VineState? state) => state == VineState.Ripe;
    }
}
