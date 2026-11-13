using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Crafting;
using EdS.Data.Saves;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Randomness;

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

        /// <summary>
        /// Sous-lot V1.4 : notes des frappes refusées : une recette à qualité attend exactement les frappes du petit jeu
        /// (<see cref="ForgeRules.Strikes"/>), chacune de 0 à <see cref="ForgeStrikes.Perfect"/> ; une autre recette n'en prend pas.
        /// </summary>
        BadStrikes = 7,
    }

    public enum CraftEventKind
    {
        Gathered = 0,
        Crafted = 1,

        /// <summary>Sous-lot V1.4 : recette au-dessus du niveau du métier, ratée : rien n'est fabriqué, une partie des matériaux est rendue.</summary>
        Failed = 2,
    }

    /// <summary>
    /// Sous-lot V1.4 (choix de Damien : un petit jeu de forge dès la vague 1, « trois frappes au bon moment ») : notes des frappes du petit
    /// jeu, de 0 (raté) à <see cref="Perfect"/> (en plein dans la cible), envoyées au cœur par le client ; c'est une entrée du joueur. Leur
    /// moyenne est l'adresse (<see cref="Skill"/>), qui fait monter la chance d'une qualité et baisser celle de rater ; le reste est tiré au
    /// hasard. Immuable.
    /// </summary>
    public sealed class ForgeStrikes
    {
        /// <summary>Note d'une frappe parfaite.</summary>
        public const int Perfect = 100;

        public IReadOnlyList<int> Notes { get; }

        public ForgeStrikes(IEnumerable<int> notes)
        {
            Notes = EdS.Domain.Collections.Frozen.List(notes ?? throw new ArgumentNullException(nameof(notes)), nameof(notes));
        }

        public ForgeStrikes(params int[] notes) : this((IEnumerable<int>)notes) { }

        /// <summary>Les notes sont admises pour <paramref name="strikes"/> frappes : autant de notes, chacune de 0 à <see cref="Perfect"/>.</summary>
        public bool IsValidFor(int strikes) => Notes.Count == strikes && Notes.All(n => n >= 0 && n <= Perfect);

        /// <summary>Adresse : moyenne des notes, arrondie vers le bas (0 sans note).</summary>
        public int Skill => Notes.Count == 0 ? 0 : (int)(Notes.Sum(n => (long)n) / Notes.Count);
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

        /// <summary>Sous-lot V1.4 : qualité de l'objet fabriqué (normale hors des recettes à qualité et pour un échec).</summary>
        public ItemQuality Quality { get; }

        /// <summary>Sous-lot V1.4 : matériaux rendus par un échec, dans l'ordre des entrées de la recette (vide sinon).</summary>
        public IReadOnlyList<(string Item, int Count)> Refunded { get; }

        /// <summary>Sous-lot V1.4 : chance de rater (pour cent) qu'avait la fabrication, et la clé de son tirage (vide pour une récolte).</summary>
        public int FailPercent { get; }

        public string Key { get; }

        internal CraftEvent(CraftEventKind kind, string source, string profession, string item, int count, long xp, int before, int after,
            ItemQuality quality = ItemQuality.Normal, IReadOnlyList<(string, int)>? refunded = null, int failPercent = 0, string key = "")
        {
            Quality = quality;
            Refunded = refunded ?? Array.Empty<(string, int)>();
            FailPercent = failPercent;
            Key = key;
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
        /// <param name="tick">Revue Codex de la PR #157 (2e tour) : tick du <b>monde</b> (les échéances sont en ticks du monde, jamais en ticks
        /// d'une session). Non sauvegardées : une partie reprise trouve tous les nœuds prêts, comme avant.</param>
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

        /// <summary>
        /// Clé stable du tirage d'une fabrication (sous-lot V1.4) : personnage, recette, jour du monde, compteur des fabrications du
        /// personnage (<c>bastion/craft/recipe.epee_de_garde/day/3/12</c>).
        /// </summary>
        public static string CraftKey(string character, string recipe, long day, long counter) =>
            (character ?? throw new ArgumentNullException(nameof(character))) + "/craft/" + (recipe ?? throw new ArgumentNullException(nameof(recipe)))
            + "/day/" + day.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/" + counter.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>
        /// Sous-lot V1.4 : niveaux de métier qui manquent pour la recette (0 si le niveau suffit) ; au-delà de
        /// <see cref="FailureRules.MaxLevels"/>, la recette est refusée (<see cref="CraftRefusal.LevelTooLow"/>).
        /// </summary>
        public int LevelGap(Recipe recipe) => Math.Max(0, (recipe ?? throw new ArgumentNullException(nameof(recipe))).RequiredLevel - Level(recipe.Profession));

        /// <summary>
        /// Sous-lot V1.4 : chances de la recette pour une adresse <paramref name="skill"/> (0 à 100) : rater (au-dessus de son niveau),
        /// chef-d'œuvre et qualité supérieure (recette à qualité, si elle réussit). Pour l'affichage de l'atelier et du petit jeu.
        /// </summary>
        public (int Fail, int Masterwork, int Superior) Chances(Recipe recipe, int skill)
        {
            int gap = LevelGap(recipe);
            // Sans petit jeu (recette sans qualité), pas d'adresse : la chance de rater est celle d'une adresse nulle.
            int fail = _book.Failure.FailPercent(gap, recipe.Quality ? skill : 0);
            if (!recipe.Quality) return (fail, 0, 0);
            (int master, int superior) = _book.Forge.Chances(skill, Math.Max(0, Level(recipe.Profession) - recipe.RequiredLevel));
            return (fail, master, superior);
        }

        /// <summary>
        /// Fabrique <paramref name="recipe"/>. Sous-lot V1.4 : une recette à qualité prend les notes des frappes du petit jeu
        /// (<paramref name="strikes"/>, exactement <see cref="ForgeRules.Strikes"/>) ; une autre n'en prend pas. Une recette jusqu'à
        /// <see cref="FailureRules.MaxLevels"/> niveaux au-dessus du métier se tente et peut rater : les entrées sont prises, la part
        /// <see cref="FailureRules.RefundPercent"/> de chacune est rendue, rien n'est fabriqué ni appris. Réussie, une recette à qualité donne
        /// une instance de qualité normale, supérieure ou chef-d'œuvre (signé <paramref name="character"/>). Les deux tirages (échec, puis
        /// qualité) sont faits par <see cref="Pcg32"/> sur la clé stable <see cref="CraftKey"/> (recette, jour <paramref name="day"/>, compteur
        /// <paramref name="counter"/>), toujours dans cet ordre : la même entrée donne toujours le même résultat. Tout ou rien : refusée, rien
        /// ne change.
        /// </summary>
        internal CraftRefusal Craft(string recipe, bool inPlace, Inventory inventory, ForgeStrikes? strikes, string character, long day, long counter,
            out CraftEvent? done)
        {
            done = null;
            CraftRefusal refusal = Prepare(recipe, inPlace, inventory, strikes, out Recipe? r);
            if (refusal != CraftRefusal.None) return refusal;
            int skill = strikes?.Skill ?? 0;
            string key = CraftKey(character, r!.Id, day, counter);
            var random = new Pcg32(Loot.Fnv1a(key), Loot.Fnv1a("craft:" + r.Id));
            int gap = LevelGap(r);
            (int fail, int master, int superior) = Chances(r, skill);
            bool failed = (int)random.NextBounded(100) < fail;
            int roll = (int)random.NextBounded(100);
            Inventory trial = inventory.Clone();
            foreach ((string item, int count) in r.Inputs)
                if (trial.Remove(item, count) != InventoryRefusal.None) return CraftRefusal.NotEnough;
            if (failed)
            {
                var refunded = new List<(string, int)>();
                foreach ((string item, int count) in r.Inputs)
                {
                    int back = _book.Failure.Refund(count);
                    if (back <= 0) continue;
                    if (trial.Add(item, back) != InventoryRefusal.None) return CraftRefusal.Full;   // ce qui vient d'être pris tient toujours
                    refunded.Add((item, back));
                }
                inventory.CopyFrom(trial);
                // Sous-lot V1.8 (réponse de Damien du 31 octobre 2026) : une recette ratée donne un quart de l'expérience de métier.
                CraftEvent learnt = Gain(CraftEventKind.Failed, r.Id, r.Profession, r.Output.Item, 0, _book.Failure.FailedXp(r.Xp));
                done = new CraftEvent(CraftEventKind.Failed, r.Id, r.Profession, r.Output.Item, 0, learnt.Xp, learnt.LevelBefore, learnt.LevelAfter,
                    ItemQuality.Normal, refunded, fail, key);
                return CraftRefusal.None;
            }
            ItemQuality quality = !r.Quality ? ItemQuality.Normal : roll < master ? ItemQuality.Masterwork : roll < master + superior ? ItemQuality.Superior : ItemQuality.Normal;
            ItemStack product = new ItemStack(r.Output.Item, r.Output.Count, null, quality, quality == ItemQuality.Masterwork ? character : null);
            if (trial.Add(product) != InventoryRefusal.None) return CraftRefusal.Full;
            inventory.CopyFrom(trial);
            int gapAbove = Level(r.Profession) - r.RequiredLevel;
            long xp = Math.Max(1, gapAbove >= 4 ? r.Xp / 4 : gapAbove >= 2 ? r.Xp / 2 : r.Xp);
            CraftEvent gained = Gain(CraftEventKind.Crafted, r.Id, r.Profession, r.Output.Item, r.Output.Count, xp);
            done = new CraftEvent(CraftEventKind.Crafted, r.Id, r.Profession, r.Output.Item, r.Output.Count, gained.Xp, gained.LevelBefore, gained.LevelAfter,
                quality, null, gap > 0 ? fail : 0, key);
            return CraftRefusal.None;
        }

        /// <summary>
        /// Sous-lot 12.8 : refus qu'aurait la fabrication (rien ne change), pour l'affichage de l'atelier. Sous-lot V1.4 : sans les frappes
        /// (le petit jeu se joue après) ; une recette à qualité est jugée comme si elles étaient données.
        /// </summary>
        internal CraftRefusal Check(string recipe, bool inPlace, Inventory inventory)
        {
            Recipe? r = _book.Recipes.FirstOrDefault(x => x.Id == recipe);
            ForgeStrikes? strikes = r is { Quality: true } ? new ForgeStrikes(new int[_book.Forge.Strikes]) : null;
            return Prepare(recipe, inPlace, inventory, strikes, out _);
        }

        /// <summary>
        /// Contrôles de la fabrication, sur une copie de l'inventaire : recette connue, atelier, niveau (au plus
        /// <see cref="FailureRules.MaxLevels"/> niveaux manquants), frappes, entrées, place du produit (une réussite comme un échec tient
        /// toujours : un échec rend moins que ce qu'il prend).
        /// </summary>
        private CraftRefusal Prepare(string recipe, bool inPlace, Inventory inventory, ForgeStrikes? strikes, out Recipe? r)
        {
            r = _book.Recipes.FirstOrDefault(x => x.Id == recipe);
            if (r is null) return CraftRefusal.Unknown;
            if (!inPlace) return CraftRefusal.NotHere;
            if (LevelGap(r) > _book.Failure.MaxLevels) return CraftRefusal.LevelTooLow;
            if (r.Quality ? strikes is null || !strikes.IsValidFor(_book.Forge.Strikes) : strikes != null && strikes.Notes.Count > 0) return CraftRefusal.BadStrikes;
            Inventory copy = inventory.Clone();
            foreach ((string item, int count) in r.Inputs)
                if (copy.Remove(item, count) != InventoryRefusal.None) return CraftRefusal.NotEnough;
            // Un produit de qualité est unique (une place) : la place est vérifiée pour l'instance la plus exigeante.
            ItemStack product = r.Quality ? new ItemStack(r.Output.Item, r.Output.Count, null, ItemQuality.Superior) : new ItemStack(r.Output.Item, r.Output.Count);
            if (copy.Add(product) != InventoryRefusal.None) return CraftRefusal.Full;
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
