using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace EdS.Data.Saves
{
    /// <summary>
    /// Lieux reconstruits (sous-lot V2.43.1, sauvegarde v28 ; décisions de Damien du 19 novembre 2026) : où en est le chantier d'un lieu.
    /// <see cref="Stage"/> est le nombre de paliers finis (0 : le premier palier est en cours) ; <see cref="Given"/> ce qui a déjà été apporté
    /// aux besoins du palier en cours (cuivre, objets, créatures vaincues, découverte : 1), jamais vide pour un besoin cité. Un lieu absent est
    /// en ruine ; un lieu dont tous les paliers sont finis est reconstruit et le reste (jamais perdu). L'accord avec le catalogue
    /// (<c>rebuilds.json</c>) est vérifié quand la partie reprend.
    /// </summary>
    public sealed class SavedRebuild
    {
        /// <summary>Paliers au plus d'un chantier (borne technique, la même que celle du catalogue).</summary>
        public const int MaxStages = 8;

        /// <summary>Besoins au plus d'un palier (borne technique, la même que celle du catalogue).</summary>
        public const int MaxNeeds = 8;

        /// <summary>Quantité au plus apportée à un besoin (la bourse entière, en cuivre).</summary>
        public const long MaxAmount = Coins.Max;

        private static readonly Regex LocalId = new Regex(@"^[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant);

        /// <summary>Identifiant de lieu ou de besoin bien formé (minuscules, chiffres, « _ », 64 caractères au plus).</summary>
        public static bool IsLocalId(string? id) => id != null && id.Length <= 64 && LocalId.IsMatch(id);

        public string Site { get; }
        public int Stage { get; }

        /// <summary>
        /// Le lieu est reconstruit, pour toujours (revue Codex de la PR #221) : un palier ajouté plus tard au catalogue ne le rouvre jamais
        /// (décision de Damien du 19 novembre 2026 : une reconstruction n'est jamais perdue).
        /// </summary>
        public bool Done { get; }
        public IReadOnlyDictionary<string, long> Given { get; }

        /// <exception cref="ArgumentException">Identifiants mal formés, quantités hors de [1, <see cref="MaxAmount"/>], trop de besoins, ou chantier vide.</exception>
        /// <param name="done">Revue Codex de la PR #221 : le lieu est reconstruit, pour toujours (même si le catalogue lui ajoute un palier
        /// plus tard) ; alors aucun apport en cours.</param>
        public SavedRebuild(string site, int stage, IEnumerable<KeyValuePair<string, long>>? given = null, bool done = false)
        {
            if (!IsLocalId(site)) throw new ArgumentException($"Lieu « {site} » mal formé.", nameof(site));
            if (stage < 0 || stage > MaxStages) throw new ArgumentOutOfRangeException(nameof(stage));
            var map = new SortedDictionary<string, long>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, long> g in given ?? Array.Empty<KeyValuePair<string, long>>())
            {
                if (!IsLocalId(g.Key)) throw new ArgumentException($"Besoin « {g.Key} » mal formé.", nameof(given));
                if (g.Value < 1 || g.Value > MaxAmount) throw new ArgumentOutOfRangeException(nameof(given));
                if (map.ContainsKey(g.Key)) throw new ArgumentException($"Besoin « {g.Key} » en double.", nameof(given));
                map.Add(g.Key, g.Value);
            }
            if (map.Count > MaxNeeds) throw new ArgumentException($"au plus {MaxNeeds} besoins.", nameof(given));
            if (stage == 0 && map.Count == 0 && !done) throw new ArgumentException("Chantier vide : un lieu en ruine est absent.", nameof(given));
            if (done && map.Count > 0) throw new ArgumentException("Lieu reconstruit : aucun apport en cours.", nameof(given));
            Done = done;
            Site = site;
            Stage = stage;
            Given = new System.Collections.ObjectModel.ReadOnlyDictionary<string, long>(map);   // ordre ordinal (écriture canonique)
        }

        /// <summary>Ce qui a été apporté au besoin <paramref name="need"/> du palier en cours (0 si rien).</summary>
        public long Of(string need) => Given.TryGetValue(need, out long v) ? v : 0;
    }

    /// <summary>Lieux reconstruits (version 28) : les chantiers du personnage, rangés par lieu (forme canonique).</summary>
    public sealed class SavedRebuilds
    {
        /// <summary>Chantiers au plus (borne technique : 5 lieux au départ, d'autres viendront, H12).</summary>
        public const int MaxEntries = 64;

        public static readonly SavedRebuilds Empty = new SavedRebuilds(Array.Empty<SavedRebuild>());

        public IReadOnlyList<SavedRebuild> Entries { get; }

        public bool IsEmpty => Entries.Count == 0;

        /// <exception cref="ArgumentException">Lieu en double ou plus de <see cref="MaxEntries"/> chantiers.</exception>
        public SavedRebuilds(IEnumerable<SavedRebuild> entries)
        {
            if (entries is null) throw new ArgumentNullException(nameof(entries));
            List<SavedRebuild> list = entries.OrderBy(e => e.Site, StringComparer.Ordinal).ToList();
            for (int i = 1; i < list.Count; i++)
                if (list[i].Site == list[i - 1].Site) throw new ArgumentException($"Lieu « {list[i].Site} » en double.", nameof(entries));
            if (list.Count > MaxEntries) throw new ArgumentException($"au plus {MaxEntries} chantiers.", nameof(entries));
            Entries = list.AsReadOnly();
        }

        /// <summary>Le chantier du lieu <paramref name="site"/>, ou null (en ruine).</summary>
        public SavedRebuild? Of(string site)
        {
            for (int i = 0; i < Entries.Count; i++) if (Entries[i].Site == site) return Entries[i];
            return null;
        }

        /// <summary>Mêmes chantiers, celui de <paramref name="rebuild"/> remplacé (ou ajouté).</summary>
        public SavedRebuilds With(SavedRebuild rebuild)
        {
            if (rebuild is null) throw new ArgumentNullException(nameof(rebuild));
            return new SavedRebuilds(Entries.Where(e => e.Site != rebuild.Site).Append(rebuild));
        }
    }
}
