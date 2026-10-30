using System;
using System.Collections.Generic;
using EdS.Data.Items;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;
using EdS.World;

namespace EdS.Runtime
{
    /// <summary>Sous-lot V1.2 : sens d'une ligne de la comparaison (vert : mieux, rouge : moins bien).</summary>
    public enum CompareTrend
    {
        Same = 0,
        Better = 1,
        Worse = 2,
    }

    /// <summary>Une ligne de la comparaison : la valeur portée, celle de l'objet survolé, l'écart affiché et son sens.</summary>
    public sealed class CompareLine
    {
        public string Label { get; }

        /// <summary>Valeur de l'objet porté à cette place (« 60 », « 60 % », « 150 ms »), ou « – » s'il n'y en a pas.</summary>
        public string Worn { get; }

        /// <summary>Valeur de l'objet survolé.</summary>
        public string Candidate { get; }

        /// <summary>Écart signé (« +10 », « −50 ms »), vide s'il n'y en a pas.</summary>
        public string Delta { get; }

        public CompareTrend Trend { get; }

        internal CompareLine(string label, string worn, string candidate, string delta, CompareTrend trend)
        {
            Label = label;
            Worn = worn;
            Candidate = candidate;
            Delta = delta;
            Trend = trend;
        }

        public override string ToString() => $"{Label} : {Worn} → {Candidate} ({Delta})";
    }

    /// <summary>
    /// Sous-lot V1.2 (« comparaison claire, vert ou rouge, avec les chiffres ») : ce que changerait le port d'un objet à la place où il irait
    /// (<see cref="Inventory.PlaceFor"/>), contre l'objet qui y est porté. Une ligne par bonus aux statistiques que l'un ou l'autre donne,
    /// puis, pour un bouclier, la garde : part bloquée (plus haut : mieux), coût d'un blocage (plus bas : mieux) et fenêtre de parade (plus
    /// large : mieux). Affichage seulement : rien ne change. Le niveau requis, la rareté et l'adéquation au build se montrent à part (quatre
    /// notions distinctes, H5).
    /// </summary>
    public sealed class ItemComparison
    {
        /// <summary>Place visée (« ring_1 »…).</summary>
        public string Place { get; }

        /// <summary>Objet porté à cette place, ou null (place vide).</summary>
        public ItemDefinition? Worn { get; }

        public ItemDefinition Candidate { get; }

        public IReadOnlyList<CompareLine> Lines { get; }

        /// <summary>L'objet survolé est celui qui est porté (rien à comparer).</summary>
        public bool IsWorn { get; }

        private ItemComparison(string place, ItemDefinition? worn, ItemDefinition candidate, IReadOnlyList<CompareLine> lines, bool isWorn)
        {
            Place = place;
            Worn = worn;
            Candidate = candidate;
            Lines = lines;
            IsWorn = isWorn;
        }

        /// <summary>Libellés des bonus, dans l'ordre de <see cref="GearBonus.Fields"/>.</summary>
        public static IReadOnlyList<string> BonusLabels { get; } = Array.AsReadOnly(new[]
        {
            "Puissance physique", "Puissance agile", "Puissance magique", "Armure", "Résistance magique", "PV maximaux", "Endurance maximale",
            "Résistance aux altérations",
        });

        /// <summary>
        /// Comparaison de <paramref name="item"/> avec l'équipement de <paramref name="inventory"/> ; <paramref name="wornPlace"/> : la place
        /// où l'objet est porté, s'il l'est (on le compare alors à rien : ses propres valeurs, toutes « en plus »). Null pour un objet qui ne
        /// se porte pas.
        /// </summary>
        public static ItemComparison? For(Inventory inventory, string item, string? wornPlace = null)
        {
            if (inventory is null) throw new ArgumentNullException(nameof(inventory));
            if (!inventory.Catalog.TryGetItem(item, out ItemDefinition candidate) || candidate.Type != ItemType.Equipment) return null;
            string? place = wornPlace ?? inventory.PlaceFor(item);
            if (place is null) return null;
            bool isWorn = wornPlace != null;
            ItemDefinition? worn = !isWorn && inventory.Equipped.TryGetValue(place, out string id) ? inventory.Catalog.Item(id) : null;
            var lines = new List<CompareLine>();
            for (int f = 0; f < GearBonus.Fields.Count; f++)
            {
                int before = worn?.Bonus[f] ?? 0, after = candidate.Bonus[f];
                if (before == 0 && after == 0) continue;
                lines.Add(Line(BonusLabels[f], worn is null ? "–" : before.ToString(), after.ToString(), after - before, "", higherIsBetter: true));
            }
            if (candidate.Guard is ShieldGuard g)
            {
                ShieldGuard? w = worn?.Guard;
                int pct = Percent(g.DamageReduction), wpct = w is null ? 0 : Percent(w.DamageReduction);
                lines.Add(Line("Part bloquée en garde", w is null ? "–" : wpct + " %", pct + " %", w is null ? 0 : pct - wpct, " %", true));
                lines.Add(Line("Endurance par blocage", w is null ? "–" : w.CostPerBlock.ToString(), g.CostPerBlock.ToString(),
                    w is null ? 0 : g.CostPerBlock - w.CostPerBlock, "", false));
                lines.Add(Line("Fenêtre de parade", w is null ? "–" : w.ParryWindowMs + " ms", g.ParryWindowMs + " ms",
                    w is null ? 0 : g.ParryWindowMs - w.ParryWindowMs, " ms", true));
            }
            return new ItemComparison(place, worn, candidate, lines, isWorn);
        }

        /// <summary>Part en pour cent entiers (0,65 → 65), arrondie au plus proche.</summary>
        public static int Percent(Fixed fraction) => (int)(fraction * 100L).RoundToInt(RoundingMode.HalfAwayFromZero);

        private static CompareLine Line(string label, string worn, string candidate, long delta, string unit, bool higherIsBetter)
        {
            CompareTrend trend = delta == 0 ? CompareTrend.Same : (delta > 0) == higherIsBetter ? CompareTrend.Better : CompareTrend.Worse;
            string text = delta == 0 ? "" : (delta > 0 ? "+" : "−") + Math.Abs(delta) + unit;
            return new CompareLine(label, worn, candidate, text, trend);
        }
    }
}
