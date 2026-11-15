using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EdS.Domain.Collections;

namespace EdS.Data.Saves
{
    /// <summary>
    /// Compagnons (version 20 de la sauvegarde, décisions de Damien du 14 novembre 2026) : un compagnon recruté. Sa jauge de lien (0 à 100)
    /// se garde d'un combat à l'autre ; son estime pour le joueur (de −100 à 100 : il aime ou déteste ses choix) ; son trait (mercenaire :
    /// tiré au hasard au recrutement ; null sinon) ; hors combat (<see cref="Down"/>) après être tombé, jusqu'à la prochaine halte au feu
    /// de camp : jamais mort. Son niveau est celui du personnage (il monte de niveau avec lui) : il n'est pas sauvegardé.
    /// </summary>
    public sealed class SavedCompanion : IEquatable<SavedCompanion>
    {
        public const int MaxBond = 100;
        public const int MaxApproval = 100;

        private static readonly Regex IdPattern = new Regex(@"^companion\.[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant);
        private static readonly Regex TraitPattern = new Regex(@"^[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant);

        public static bool IsId(string? id) => id != null && id.Length <= 64 && IdPattern.IsMatch(id);
        public static bool IsTrait(string? id) => id != null && id.Length <= 64 && TraitPattern.IsMatch(id);

        public string Id { get; }
        public int Bond { get; }
        public int Approval { get; }
        public string? Trait { get; }
        public bool Down { get; }

        public SavedCompanion(string id, int bond = 0, int approval = 0, string? trait = null, bool down = false)
        {
            if (!IsId(id)) throw new ArgumentException($"Compagnon « {id} » hors du format « companion.… ».", nameof(id));
            if (bond < 0 || bond > MaxBond) throw new ArgumentOutOfRangeException(nameof(bond), bond, $"Jauge de lien hors de [0, {MaxBond}].");
            if (approval < -MaxApproval || approval > MaxApproval) throw new ArgumentOutOfRangeException(nameof(approval), approval, $"Estime hors de [−{MaxApproval}, {MaxApproval}].");
            if (trait != null && !IsTrait(trait)) throw new ArgumentException($"Trait « {trait} » mal nommé.", nameof(trait));
            Id = id;
            Bond = bond;
            Approval = approval;
            Trait = trait;
            Down = down;
        }

        public SavedCompanion With(int? bond = null, int? approval = null, bool? down = null) =>
            new SavedCompanion(Id, bond ?? Bond, approval ?? Approval, Trait, down ?? Down);

        public bool Equals(SavedCompanion? other) => other != null && Id == other.Id && Bond == other.Bond && Approval == other.Approval
            && Trait == other.Trait && Down == other.Down;

        public override bool Equals(object? obj) => Equals(obj as SavedCompanion);
        public override int GetHashCode() => (((Id.GetHashCode() * 31 + Bond) * 31 + Approval) * 31 + (Trait?.GetHashCode() ?? 0)) * 31 + (Down ? 1 : 0);
    }

    /// <summary>
    /// Compagnons (version 20) : la troupe du personnage. Places ouvertes (<see cref="Slots"/> : une, puis deux par l'histoire), compagnons
    /// recrutés (rangés par identifiant, uniques), compagnons actifs (dans l'ordre des places, au plus <see cref="Slots"/>, recrutés et
    /// distincts ; un compagnon hors combat peut rester actif : il ne se bat pas avant la halte). Immuable ; toute forme impossible est
    /// refusée à la construction (comme à la lecture d'une sauvegarde).
    /// <para>Anciens mercenaires (<see cref="Former"/>, décision de Damien du 15 novembre 2026) : un mercenaire renvoyé y garde son estime
    /// et son trait, jauge de lien à 0 et jamais hors combat ; réembauché, il les retrouve (seule la jauge repart de 0). Rangés par
    /// identifiant, uniques, jamais en même temps recrutés.</para>
    /// </summary>
    public sealed class SavedCompanions : IEquatable<SavedCompanions>
    {
        public const int MaxSlots = 2;
        public const int MaxRecruited = 64;

        public static SavedCompanions Empty { get; } = new SavedCompanions(1, null, null);

        public int Slots { get; }
        public IReadOnlyList<SavedCompanion> Recruited { get; }
        public IReadOnlyList<string> Active { get; }

        /// <summary>Mercenaires renvoyés (estime et trait gardés, jauge à 0, jamais hors combat), rangés par identifiant.</summary>
        public IReadOnlyList<SavedCompanion> Former { get; }

        public bool IsEmpty => Slots == 1 && Recruited.Count == 0 && Former.Count == 0;

        public SavedCompanions(int slots, IEnumerable<SavedCompanion>? recruited, IEnumerable<string>? active, IEnumerable<SavedCompanion>? former = null)
        {
            if (slots < 1 || slots > MaxSlots) throw new ArgumentOutOfRangeException(nameof(slots), slots, $"Places hors de [1, {MaxSlots}].");
            List<SavedCompanion> list = (recruited ?? Array.Empty<SavedCompanion>()).OrderBy(c => c?.Id, StringComparer.Ordinal).ToList();
            if (list.Any(c => c is null)) throw new ArgumentNullException(nameof(recruited));
            if (list.Count > MaxRecruited) throw new ArgumentException($"{list.Count} compagnons recrutés, au plus {MaxRecruited}.", nameof(recruited));
            for (int i = 1; i < list.Count; i++)
                if (list[i].Id == list[i - 1].Id) throw new ArgumentException($"Compagnon « {list[i].Id} » recruté deux fois.", nameof(recruited));
            List<string> actives = (active ?? Array.Empty<string>()).ToList();
            if (actives.Count > slots) throw new ArgumentException($"{actives.Count} compagnons actifs pour {slots} place(s).", nameof(active));
            if (actives.Distinct(StringComparer.Ordinal).Count() != actives.Count) throw new ArgumentException("Compagnon actif deux fois.", nameof(active));
            foreach (string id in actives)
                if (!list.Any(c => c.Id == id)) throw new ArgumentException($"Compagnon actif « {id} » qui n'est pas recruté.", nameof(active));
            List<SavedCompanion> formers = (former ?? Array.Empty<SavedCompanion>()).OrderBy(c => c?.Id, StringComparer.Ordinal).ToList();
            if (formers.Any(c => c is null)) throw new ArgumentNullException(nameof(former));
            if (formers.Count > MaxRecruited) throw new ArgumentException($"{formers.Count} anciens mercenaires, au plus {MaxRecruited}.", nameof(former));
            for (int i = 0; i < formers.Count; i++)
            {
                if (i > 0 && formers[i].Id == formers[i - 1].Id) throw new ArgumentException($"Ancien mercenaire « {formers[i].Id} » deux fois.", nameof(former));
                if (formers[i].Bond != 0 || formers[i].Down) throw new ArgumentException($"Ancien mercenaire « {formers[i].Id} » : jauge à 0 et jamais hors combat.", nameof(former));
                if (list.Any(c => c.Id == formers[i].Id)) throw new ArgumentException($"Compagnon « {formers[i].Id} » à la fois recruté et renvoyé.", nameof(former));
            }
            Slots = slots;
            Recruited = Frozen.List(list, nameof(recruited));
            Active = Frozen.List(actives, nameof(active));
            Former = Frozen.List(formers, nameof(former));
        }

        public SavedCompanion? Find(string? id) => Recruited.FirstOrDefault(c => c.Id == id);

        /// <summary>Ancien mercenaire renvoyé <paramref name="id"/>, ou null.</summary>
        public SavedCompanion? FindFormer(string? id) => Former.FirstOrDefault(c => c.Id == id);

        public bool Equals(SavedCompanions? other) => other != null && Slots == other.Slots && Recruited.SequenceEqual(other.Recruited)
            && Active.SequenceEqual(other.Active, StringComparer.Ordinal) && Former.SequenceEqual(other.Former);

        public override bool Equals(object? obj) => Equals(obj as SavedCompanions);
        public override int GetHashCode() => Slots * 31 + Recruited.Count * 7 + Active.Count;
    }

    /// <summary>Compagnons (version 20) : troupe telle que lue (avant validation).</summary>
    public sealed class SavedCompanionsData
    {
        public int? Slots { get; set; }
        public List<SavedCompanionData?>? Recruited { get; set; }
        public List<string?>? Active { get; set; }

        /// <summary>Anciens mercenaires (facultatif ; seuls <c>id</c>, <c>approval</c> et <c>trait</c> y sont écrits).</summary>
        public List<SavedCompanionData?>? Former { get; set; }
    }

    /// <summary>Compagnons (version 20) : compagnon recruté tel que lu.</summary>
    public sealed class SavedCompanionData
    {
        public string? Id { get; set; }
        public int? Bond { get; set; }
        public int? Approval { get; set; }
        public string? Trait { get; set; }
        public bool? Down { get; set; }
    }
}
