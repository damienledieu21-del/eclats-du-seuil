using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Crafting;
using EdS.Domain.Numerics;
using EdS.Domain.Space;

namespace EdS.World
{
    public enum CraftRefusal
    {
        None = 0,

        /// <summary>Recette ou nœud inconnu.</summary>
        Unknown = 1,

        /// <summary>Trop loin du nœud, ou hors de l'atelier de la recette (personnage absent ou mort compris).</summary>
        NotHere = 2,

        /// <summary>Nœud déjà récolté, pas encore repoussé.</summary>
        Depleted = 3,

        /// <summary>Niveau de métier insuffisant.</summary>
        LevelTooLow = 4,

        /// <summary>Entrées manquantes.</summary>
        NotEnough = 5,

        /// <summary>Le produit ne tient pas dans l'inventaire.</summary>
        Full = 6,
    }

    public enum CraftEventKind
    {
        Gathered = 0,
        Crafted = 1,
    }

    /// <summary>Récolte ou fabrication réussie : objet obtenu, expérience de métier, niveaux avant et après.</summary>
    public sealed class CraftEvent
    {
        public CraftEventKind Kind { get; }

        /// <summary>Nœud ou recette.</summary>
        public string Source { get; }

        public string Profession { get; }
        public string Item { get; }
        public int Count { get; }
        public long Xp { get; }
        public int LevelBefore { get; }
        public int LevelAfter { get; }
        public bool LeveledUp => LevelAfter > LevelBefore;

        internal CraftEvent(CraftEventKind kind, string source, string profession, string item, int count, long xp, int before, int after)
        {
            Kind = kind;
            Source = source;
            Profession = profession;
            Item = item;
            Count = count;
            Xp = xp;
            LevelBefore = before;
            LevelAfter = after;
        }
    }

    /// <summary>
    /// Sous-lot 12.6 (plan : métiers, XP de métier séparée) : récolte et fabrication d'un personnage. Chaque métier a sa propre expérience :
    /// récolter et fabriquer n'en donnent qu'au métier concerné, jamais au combat, et combattre n'en donne à aucun métier. Une fabrication
    /// bien en dessous du niveau du métier forme moins (moitié à 2 niveaux d'écart, quart à 4). Un nœud récolté repousse après son délai,
    /// dans la session. Chaque opération est atomique.
    /// </summary>
    public sealed class Workshop
    {
        /// <summary>Distance de récolte : écart au nœud, au plus (provisoire, comme la parole).</summary>
        public static readonly Fixed GatherRange = Fixed.FromInt(3);

        private readonly CraftingBook _book;
        private readonly SortedDictionary<string, long> _xp;
        private readonly Dictionary<string, long> _readyTick = new Dictionary<string, long>(StringComparer.Ordinal);

        public CraftingBook Book => _book;

        internal Workshop(CraftingBook book, IReadOnlyDictionary<string, long> saved)
        {
            _book = book ?? throw new ArgumentNullException(nameof(book));
            CheckSaved(book, saved);
            _xp = new SortedDictionary<string, long>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, long> p in saved) _xp[p.Key] = p.Value;
        }

        /// <summary>
        /// Revue Codex de la PR #47 : l'expérience sauvegardée va avec ces métiers (métier connu, expérience de 1 à
        /// <see cref="CraftingValidator.MaxXp"/>), y compris pour un personnage construit hors du lecteur JSON ; contrôlé avant l'ouverture de
        /// la session, pour qu'aucune récolte ni fabrication n'échoue à moitié.
        /// </summary>
        /// <exception cref="ArgumentException">Sauvegarde qui ne va pas avec ces métiers.</exception>
        internal static void CheckSaved(CraftingBook book, IReadOnlyDictionary<string, long> saved)
        {
            if (book is null) throw new ArgumentNullException(nameof(book));
            foreach (KeyValuePair<string, long> p in saved ?? throw new ArgumentNullException(nameof(saved)))
            {
                if (!book.HasProfession(p.Key)) throw new ArgumentException($"Métier sauvegardé « {p.Key} » absent des métiers.", nameof(saved));
                if (p.Value < 1 || p.Value > CraftingValidator.MaxXp)
                    throw new ArgumentException($"Expérience du métier « {p.Key} » hors de [1, {CraftingValidator.MaxXp}] : {p.Value}.", nameof(saved));
            }
        }

        public long Xp(string profession) => _xp.TryGetValue(profession, out long xp) ? xp : 0;
        public int Level(string profession) => _book.LevelFor(Xp(profession));

        /// <summary>Expérience de chaque métier (non nulle), pour la sauvegarde.</summary>
        public IReadOnlyDictionary<string, long> Professions => new Dictionary<string, long>(_xp, StringComparer.Ordinal);

        /// <summary>Le nœud peut être récolté à ce tick de la session.</summary>
        public bool IsReady(string node, long tick) => !_readyTick.TryGetValue(node, out long ready) || tick >= ready;

        internal CraftRefusal Gather(string node, long tick, long tickMs, GroundVector? player, Inventory inventory, out CraftEvent? done)
        {
            done = null;
            GatheringNode? n = _book.Nodes.FirstOrDefault(x => x.Id == node);
            if (n is null) return CraftRefusal.Unknown;
            if (player is not GroundVector p || ZonePopulations.Beyond(p, Fixed.Zero, n.Position, Fixed.Zero, GatherRange)) return CraftRefusal.NotHere;
            if (!IsReady(node, tick)) return CraftRefusal.Depleted;
            if (inventory.Add(n.Item, n.Count) != InventoryRefusal.None) return CraftRefusal.Full;
            _readyTick[node] = checked(tick + n.RespawnMs / tickMs);
            done = Gain(CraftEventKind.Gathered, n.Id, n.Profession, n.Item, n.Count, n.Xp);
            return CraftRefusal.None;
        }

        internal CraftRefusal Craft(string recipe, bool inPlace, Inventory inventory, out CraftEvent? done)
        {
            done = null;
            Recipe? r = _book.Recipes.FirstOrDefault(x => x.Id == recipe);
            if (r is null) return CraftRefusal.Unknown;
            if (!inPlace) return CraftRefusal.NotHere;
            int level = Level(r.Profession);
            if (level < r.RequiredLevel) return CraftRefusal.LevelTooLow;
            Inventory trial = inventory.Clone();
            foreach ((string item, int count) in r.Inputs)
                if (trial.Remove(item, count) != InventoryRefusal.None) return CraftRefusal.NotEnough;
            if (trial.Add(r.Output.Item, r.Output.Count) != InventoryRefusal.None) return CraftRefusal.Full;
            inventory.CopyFrom(trial);
            int gap = level - r.RequiredLevel;
            long xp = Math.Max(1, gap >= 4 ? r.Xp / 4 : gap >= 2 ? r.Xp / 2 : r.Xp);
            done = Gain(CraftEventKind.Crafted, r.Id, r.Profession, r.Output.Item, r.Output.Count, xp);
            return CraftRefusal.None;
        }

        /// <summary>Ajoute l'expérience (sous le plafond) ; l'événement annonce celle réellement ajoutée (comme le combat, revue Codex de la PR #43).</summary>
        private CraftEvent Gain(CraftEventKind kind, string source, string profession, string item, int count, long xp)
        {
            int before = Level(profession);
            long had = Xp(profession);
            long total = Math.Min(CraftingValidator.MaxXp, had + xp);   // had ≤ 10^9 et xp ≤ 10^6 : pas de débordement
            if (total > had) _xp[profession] = total;
            return new CraftEvent(kind, source, profession, item, count, total - had, before, Level(profession));
        }
    }
}
