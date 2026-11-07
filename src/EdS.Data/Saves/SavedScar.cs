using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace EdS.Data.Saves
{
    /// <summary>
    /// Sous-lot V2.6 (sauvegarde v14, « le Seuil qui grandit ») : une <b>cicatrice</b> laissée par une faille du Seuil restée ouverte, à la
    /// place d'une variante de la faille (<c>zone.vallons/faille_du_seuil/ruines</c>). Son palier se recalcule du temps du monde : au jour
    /// <c>d</c>, <c>min(palier maximal, Stage + (d - Since) / jours par palier)</c> (règles de la zone). <see cref="Stage"/> est le palier au
    /// jour <see cref="Since"/> (naissance, nouvelle faille restée ouverte, faille refermée qui la fait reculer) ; <see cref="Born"/> est le
    /// jour où elle est née. Une cicatrice purifiée ou ramenée à 0 disparaît de la liste.
    /// </summary>
    public sealed class SavedScar
    {
        public string Id { get; }
        public int Stage { get; }
        public long Since { get; }
        public long Born { get; }

        /// <summary>Palier au plus (borne technique ; celui de la zone peut être plus bas).</summary>
        public const int MaxStage = 8;

        /// <summary>Cicatrices gardées au plus.</summary>
        public const int MaxEntries = 32;

        public SavedScar(string id, int stage, long since, long born)
        {
            if (!SaveValidator.IsEventKey(id) || id.Length > SavedEvent.MaxKeyLength) throw new ArgumentException($"Cicatrice « {id} » hors du format des sauvegardes.", nameof(id));
            if (stage < 1 || stage > MaxStage) throw new ArgumentOutOfRangeException(nameof(stage));
            if (since < 0 || since > SavedEvent.MaxDay) throw new ArgumentOutOfRangeException(nameof(since));
            if (born < 0 || born > since) throw new ArgumentOutOfRangeException(nameof(born));
            Id = id;
            Stage = stage;
            Since = since;
            Born = born;
        }

        /// <summary>Lignes rangées par identifiant (forme canonique), une seule par identifiant.</summary>
        public static IReadOnlyList<SavedScar> Canonical(IEnumerable<SavedScar>? scars)
        {
            var list = (scars ?? Array.Empty<SavedScar>()).ToList();
            if (list.Any(e => e is null)) throw new ArgumentNullException(nameof(scars));
            if (list.Count > MaxEntries) throw new ArgumentException($"{list.Count} cicatrices, au plus {MaxEntries}.", nameof(scars));
            if (list.Select(e => e.Id).Distinct(StringComparer.Ordinal).Count() != list.Count) throw new ArgumentException("Cicatrice en double.", nameof(scars));
            return new ReadOnlyCollection<SavedScar>(list.OrderBy(e => e.Id, StringComparer.Ordinal).ToList());
        }
    }

    /// <summary>Sous-lot V2.6 (version 14) : une cicatrice telle que lue, avant validation.</summary>
    public sealed class SavedScarData
    {
        public string? Id { get; set; }
        public int? Stage { get; set; }
        public long? Since { get; set; }
        public long? Born { get; set; }
    }
}
