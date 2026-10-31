using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Items;
using EdS.Data.Saves;
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
    /// Sous-lot V1.3 : une ligne de détail de la fiche d'un objet : propriété tirée, effet, ensemble ou palier d'ensemble. Le sens dit ce que
    /// le port de l'objet changerait (vert : gagné, rouge : perdu) ; <see cref="Active"/> faux : un palier d'ensemble qui ne vaudrait pas.
    /// </summary>
    public sealed class DetailLine
    {
        public string Text { get; }
        public CompareTrend Trend { get; }
        public bool Active { get; }

        internal DetailLine(string text, CompareTrend trend, bool active = true)
        {
            Text = text;
            Trend = trend;
            Active = active;
        }

        public override string ToString() => Text;
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

        /// <summary>Sous-lot V1.3 : instance survolée (propriétés tirées comprises).</summary>
        public ItemStack CandidateInstance { get; }

        /// <summary>Sous-lot V1.3 : instance portée à cette place, ou null.</summary>
        public ItemStack? WornInstance { get; }

        /// <summary>
        /// Sous-lot V1.3 : détails de l'objet survolé : ses propriétés tirées, son effet (vert s'il s'ajoute), l'effet de l'objet porté qui serait
        /// perdu (rouge), son ensemble (pièces portées sur le total, avant et après) et chaque palier (vert s'il s'ouvre, rouge s'il se ferme),
        /// et l'ensemble de l'objet porté qui perdrait une pièce.
        /// </summary>
        public IReadOnlyList<DetailLine> Details { get; }

        private ItemComparison(string place, ItemDefinition? worn, ItemDefinition candidate, IReadOnlyList<CompareLine> lines, bool isWorn,
            ItemStack candidateInstance, ItemStack? wornInstance, IReadOnlyList<DetailLine> details)
        {
            Place = place;
            Worn = worn;
            Candidate = candidate;
            Lines = lines;
            IsWorn = isWorn;
            CandidateInstance = candidateInstance;
            WornInstance = wornInstance;
            Details = details;
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
            if (item is null) return null;
            ItemStack instance = wornPlace != null && inventory.WornInstanceAt(wornPlace) is { } w && w.Item == item ? w : new ItemStack(item, 1);
            return For(inventory, instance, wornPlace);
        }

        /// <summary>
        /// Sous-lot V1.3 : comparaison d'une instance (propriétés tirées comprises) avec l'équipement de <paramref name="inventory"/> : les
        /// bonus comptent les propriétés tirées des deux objets ; les détails disent les effets, les ensembles et leurs paliers.
        /// </summary>
        public static ItemComparison? For(Inventory inventory, ItemStack instance, string? wornPlace = null)
        {
            if (inventory is null) throw new ArgumentNullException(nameof(inventory));
            if (instance is null) throw new ArgumentNullException(nameof(instance));
            ItemCatalog catalog = inventory.Catalog;
            if (!catalog.TryGetItem(instance.Item, out ItemDefinition candidate) || candidate.Type != ItemType.Equipment) return null;
            string? place = wornPlace ?? inventory.PlaceFor(instance.Item);
            if (place is null) return null;
            bool isWorn = wornPlace != null;
            ItemStack? wornInstance = !isWorn ? inventory.WornInstanceAt(place) : null;
            ItemDefinition? worn = wornInstance is null ? null : catalog.Item(wornInstance.Item);
            GearBonus candidateBonus = catalog.BonusOf(instance), wornBonus = wornInstance is null ? GearBonus.None : catalog.BonusOf(wornInstance);
            var lines = new List<CompareLine>();
            for (int f = 0; f < GearBonus.Fields.Count; f++)
            {
                int before = wornBonus[f], after = candidateBonus[f];
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
            return new ItemComparison(place, worn, candidate, lines, isWorn, instance, wornInstance, BuildDetails(inventory, candidate, instance, worn, place, isWorn));
        }

        /// <summary>Sous-lot V1.3 : lignes de détail (propriétés tirées, effets, ensembles), voir <see cref="Details"/>.</summary>
        private static IReadOnlyList<DetailLine> BuildDetails(Inventory inventory, ItemDefinition candidate, ItemStack instance, ItemDefinition? worn, string place,
            bool isWorn)
        {
            ItemCatalog catalog = inventory.Catalog;
            var details = new List<DetailLine>();
            // Sous-lot V1.4 : qualité de fabrication (signature d'un chef-d'œuvre) et usure de l'instance, d'abord.
            if (instance.Quality == ItemQuality.Masterwork)
                details.Add(new DetailLine($"Chef-d'œuvre, signé {DisplayNames.Of(instance.SignedBy ?? "")} : +{catalog.MasterworkPercent} % au bonus de base", CompareTrend.Better));
            else if (instance.Quality == ItemQuality.Superior)
                details.Add(new DetailLine($"Qualité supérieure : +{catalog.SuperiorPercent} % au bonus de base", CompareTrend.Better));
            // Une pièce neuve ne dit rien de sa solidité ; une pièce entamée la montre, une pièce usée à zéro dit qu'elle ne donne plus rien.
            if (catalog.Wears(candidate) && instance.Wear is int)
            {
                int left = catalog.WearLeft(instance);
                details.Add(left == 0
                    ? new DetailLine($"Usé : solidité 0 / {catalog.WearMax}, plus aucun bonus (à réparer)", CompareTrend.Worse)
                    : new DetailLine($"Solidité {left} / {catalog.WearMax} (à réparer)", CompareTrend.Worse, true));
            }
            foreach (RolledProperty r in instance.Properties)
                if (catalog.TryGetProperty(r.Id, out PropertyDefinition property))
                    details.Add(new DetailLine($"{property.Name} : +{r.Value} {BonusLabels[property.Stat].ToLowerInvariant()}", CompareTrend.Same));
            // Objets portés après l'échange (une même pièce à deux places compte une fois).
            var now = new HashSet<string>(inventory.Equipped.Values, StringComparer.Ordinal);
            var after = new HashSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> e in inventory.Equipped)
                if (isWorn || e.Key != place) after.Add(e.Value);
            after.Add(candidate.Id);
            if (candidate.Effect is ItemEffect effect)
                details.Add(new DetailLine("Effet : " + effect.Text, isWorn || now.Contains(candidate.Id) ? CompareTrend.Same : CompareTrend.Better));
            if (!isWorn && worn?.Effect is ItemEffect lost && !after.Contains(worn.Id))
                details.Add(new DetailLine("Effet perdu : " + lost.Text, CompareTrend.Worse));
            void SetLines(ItemSet set)
            {
                int before = set.Pieces.Count(now.Contains), later = set.Pieces.Count(after.Contains);
                CompareTrend trend = later == before ? CompareTrend.Same : later > before ? CompareTrend.Better : CompareTrend.Worse;
                string count = isWorn || later == before ? $"{later}/{set.Pieces.Count}" : $"{before} → {later}/{set.Pieces.Count}";
                details.Add(new DetailLine($"Ensemble « {set.Name} » : {count} pièces", trend));
                foreach (SetBonus bonus in set.Bonuses)
                {
                    bool was = bonus.Pieces <= before, will = bonus.Pieces <= later;
                    details.Add(new DetailLine($"({bonus.Pieces}) {bonus.Text}", was == will ? CompareTrend.Same : will ? CompareTrend.Better : CompareTrend.Worse, will));
                }
            }
            if (candidate.Set is string setId && catalog.TryGetSet(setId, out ItemSet candidateSet)) SetLines(candidateSet);
            if (!isWorn && worn?.Set is string wornSet && wornSet != candidate.Set && catalog.TryGetSet(wornSet, out ItemSet lostSet)) SetLines(lostSet);
            return details;
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
