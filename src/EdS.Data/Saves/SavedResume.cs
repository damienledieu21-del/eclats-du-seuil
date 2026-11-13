using System;

namespace EdS.Data.Saves
{
    /// <summary>
    /// Sous-lot V2.28 (PLAN_GRANDE_CARTE.md § 3, « reprise d'une partie » ; sauvegarde v19) : où la partie reprend dans la zone de la
    /// sauvegarde : le dernier point sûr approché (halte, voyage, entrée, arrivée par une porte), et le secteur de simulation qui le
    /// contient quand la zone a une région. Le <b>point sûr fait foi</b> : le secteur est recalculé des données à la reprise (un point sûr
    /// déplacé par les données, comme la place du Bourg au V2.30.1, emmène la reprise avec lui) ; il est écrit pour que l'hôte des secteurs
    /// sache lequel ouvrir.
    /// </summary>
    public sealed class SavedResume
    {
        /// <summary>Identifiant du point sûr dans la zone de la sauvegarde (« place_du_bourg »).</summary>
        public string SafePoint { get; }

        /// <summary>Secteur de simulation du point sûr (« s07_06 ») ; null pour une zone sans région (un intérieur).</summary>
        public string? Sector { get; }

        /// <exception cref="ArgumentException">Identifiant de point sûr ou nom de secteur hors du format.</exception>
        public SavedResume(string safePoint, string? sector = null)
        {
            if (!SaveValidator.IsSafePointId(safePoint)) throw new ArgumentException($"Point sûr « {safePoint} » hors du format.", nameof(safePoint));
            if (sector != null && !SaveValidator.IsSectorName(sector)) throw new ArgumentException($"Secteur « {sector} » mal nommé.", nameof(sector));
            SafePoint = safePoint;
            Sector = sector;
        }

        public override string ToString() => Sector is null ? SafePoint : $"{SafePoint} ({Sector})";
    }
}
