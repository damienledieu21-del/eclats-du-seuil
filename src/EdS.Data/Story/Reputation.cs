using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace EdS.Data.Story
{
    /// <summary>
    /// Sous-lot V2.7 (réputation) : les factions des Vallons et les paliers de réputation, fixés par le code (les valeurs des gestes, les
    /// liens entre factions et les prix vont dans l'histoire, <see cref="ReputationRules"/>). Valeurs <b>provisoires</b> (docs/MONDE.md,
    /// « Réputation »). Une réputation est un entier de <see cref="Min"/> à <see cref="Max"/>, 0 au départ (neutre).
    /// </summary>
    public static class Reputations
    {
        /// <summary>Bornes d'une réputation (provisoires).</summary>
        public const int Min = -1000, Max = 1000;

        /// <summary>Changement d'un effet <c>reputation</c> au plus (en valeur absolue).</summary>
        public const int MaxEffect = 1000;

        /// <summary>Changement d'un geste (quête finie, attaque repoussée…) au plus, en valeur absolue.</summary>
        public const int MaxAct = 200;

        /// <summary>Part d'un changement reportée sur une faction liée, en pour cent, au plus (en valeur absolue).</summary>
        public const int MaxLinkPercent = 100;

        /// <summary>Prix d'un marchand selon le palier, en pour cent du prix du jour : bornes.</summary>
        public const int MinPricePercent = 50, MaxPricePercent = 200;

        /// <summary>Factions, dans l'ordre d'affichage : les villageois, la garde, les rôdeurs, les bandits.</summary>
        public static IReadOnlyList<string> Factions { get; } = new ReadOnlyCollection<string>(new[] { "villagers", "guard", "rangers", "bandits" });

        /// <summary>Paliers, du plus bas au plus haut : haï, hostile, méfiant, neutre, apprécié, honoré, héros.</summary>
        public static IReadOnlyList<string> Tiers { get; } = new ReadOnlyCollection<string>(new[] { "hated", "hostile", "wary", "neutral", "liked", "honored", "hero" });

        /// <summary>Réputation la plus basse de chaque palier (le premier commence à <see cref="Min"/>).</summary>
        public static IReadOnlyList<int> TierFloors { get; } = new ReadOnlyCollection<int>(new[] { Min, -599, -299, -99, 100, 300, 600 });

        /// <summary>Rang du palier neutre.</summary>
        public const int Neutral = 3;

        /// <summary>Gestes du monde qui changent la réputation (<see cref="ReputationRules.Acts"/>).</summary>
        public static IReadOnlyList<string> Acts { get; } = new ReadOnlyCollection<string>(new[] { "questMain", "questSide", "contract", "scarPurified" });

        public const string QuestMain = "questMain", QuestSide = "questSide", Contract = "contract", ScarPurified = "scarPurified";

        public static bool IsFaction(string? id) => id != null && Factions.Contains(id);

        public static int TierIndex(string tier) => IndexOf(Tiers, tier);

        /// <summary>Rang du palier d'une réputation (bornée d'abord à [<see cref="Min"/>, <see cref="Max"/>]).</summary>
        public static int TierOf(int value)
        {
            int v = Clamp(value);
            int tier = 0;
            for (int i = 1; i < TierFloors.Count; i++)
                if (v >= TierFloors[i]) tier = i;
            return tier;
        }

        public static int Clamp(long value) => (int)Math.Max(Min, Math.Min(Max, value));

        /// <summary>Clé du nom affiché d'une faction (« text.reputation.faction.guard »).</summary>
        public static string FactionText(string faction) => "text.reputation.faction." + faction;

        /// <summary>Clé du nom affiché d'un palier (« text.reputation.tier.hero »).</summary>
        public static string TierText(string tier) => "text.reputation.tier." + tier;

        private static int IndexOf(IReadOnlyList<string> list, string id)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] == id) return i;
            return -1;
        }
    }

    /// <summary>Sous-lot V2.7 : un changement de réputation d'une faction.</summary>
    public readonly struct ReputationChange
    {
        public string Faction { get; }
        public int Value { get; }

        public ReputationChange(string faction, int value)
        {
            Faction = faction ?? throw new ArgumentNullException(nameof(faction));
            Value = value;
        }
    }

    /// <summary>
    /// Sous-lot V2.7 (histoire en schéma 12, section <c>reputation</c>, facultative) : ce qui change la réputation sans effet écrit dans un
    /// dialogue : les gestes du monde (<see cref="Acts"/> : quête principale ou secondaire finie, contrat de guilde réussi, cicatrice purifiée),
    /// les créatures vaincues (<see cref="Defeated"/>, par population ou par famille), les événements du monde réglés (<see cref="Events"/>,
    /// réussite ou échec) ; les liens entre factions (<see cref="Links"/> : la part d'un effet <c>reputation</c> reportée sur d'autres
    /// factions) et les prix des marchands selon le palier des villageois (<see cref="Prices"/>). Absente : aucune de ces règles.
    /// </summary>
    public sealed class ReputationRules
    {
        public static readonly ReputationRules None = new ReputationRules();

        private static readonly IReadOnlyList<ReputationChange> NoChange = Array.AsReadOnly(Array.Empty<ReputationChange>());

        /// <summary>Faction d'origine : (faction liée, part en pour cent), dans l'ordre des factions.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<ReputationChange>> Links { get; }

        public IReadOnlyDictionary<string, IReadOnlyList<ReputationChange>> Acts { get; }

        /// <summary>Population ou famille de créatures : changements à chaque victoire (la population d'abord, sinon sa famille).</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<ReputationChange>> Defeated { get; }

        /// <summary>Événement du monde de la zone : changements à sa réussite (Won) et à son échec (Lost).</summary>
        public IReadOnlyDictionary<string, (IReadOnlyList<ReputationChange> Won, IReadOnlyList<ReputationChange> Lost)> Events { get; }

        /// <summary>Prix d'achat chez les marchands, en pour cent, par palier des villageois ; vide : prix inchangés.</summary>
        public IReadOnlyList<int> Prices { get; }

        private ReputationRules()
            : this(null, null, null, null, null)
        {
        }

        internal ReputationRules(IDictionary<string, IReadOnlyList<ReputationChange>>? links, IDictionary<string, IReadOnlyList<ReputationChange>>? acts,
            IDictionary<string, IReadOnlyList<ReputationChange>>? defeated,
            IDictionary<string, (IReadOnlyList<ReputationChange>, IReadOnlyList<ReputationChange>)>? events, IReadOnlyList<int>? prices)
        {
            Links = Freeze(links);
            Acts = Freeze(acts);
            Defeated = Freeze(defeated);
            Events = new ReadOnlyDictionary<string, (IReadOnlyList<ReputationChange>, IReadOnlyList<ReputationChange>)>(
                new SortedDictionary<string, (IReadOnlyList<ReputationChange>, IReadOnlyList<ReputationChange>)>(
                    events ?? new Dictionary<string, (IReadOnlyList<ReputationChange>, IReadOnlyList<ReputationChange>)>(), StringComparer.Ordinal));
            Prices = Array.AsReadOnly((prices ?? Array.Empty<int>()).ToArray());
        }

        private static IReadOnlyDictionary<string, IReadOnlyList<ReputationChange>> Freeze(IDictionary<string, IReadOnlyList<ReputationChange>>? map) =>
            new ReadOnlyDictionary<string, IReadOnlyList<ReputationChange>>(new SortedDictionary<string, IReadOnlyList<ReputationChange>>(
                (map ?? new Dictionary<string, IReadOnlyList<ReputationChange>>()).ToDictionary(x => x.Key, x => (IReadOnlyList<ReputationChange>)x.Value.ToList().AsReadOnly()),
                StringComparer.Ordinal));

        public bool IsEmpty => Links.Count == 0 && Acts.Count == 0 && Defeated.Count == 0 && Events.Count == 0 && Prices.Count == 0;

        public IReadOnlyList<ReputationChange> Act(string act) => Acts.TryGetValue(act, out IReadOnlyList<ReputationChange> c) ? c : NoChange;

        public IReadOnlyList<ReputationChange> LinksOf(string faction) => Links.TryGetValue(faction, out IReadOnlyList<ReputationChange> c) ? c : NoChange;

        /// <summary>Changements d'une victoire sur <paramref name="population"/> (sinon sur sa famille <paramref name="family"/>).</summary>
        public IReadOnlyList<ReputationChange> DefeatOf(string population, string? family) =>
            Defeated.TryGetValue(population, out IReadOnlyList<ReputationChange> c) ? c
            : family != null && Defeated.TryGetValue(family, out IReadOnlyList<ReputationChange> f) ? f : NoChange;

        public IReadOnlyList<ReputationChange> EventOf(string eventId, bool won) =>
            Events.TryGetValue(eventId, out var e) ? (won ? e.Won : e.Lost) : NoChange;

        /// <summary>Prix d'achat en pour cent au palier <paramref name="tier"/> des villageois (100 sans règle).</summary>
        public int PricePercent(int tier) => Prices.Count == Reputations.Tiers.Count && tier >= 0 && tier < Prices.Count ? Prices[tier] : 100;
    }
}
