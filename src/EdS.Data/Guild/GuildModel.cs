using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using EdS.Data.Items;
using EdS.Data.Merchants;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Collections;

namespace EdS.Data.Guild
{
    /// <summary>
    /// Sous-lot V1.7 : les rangs de la Guilde des aventuriers, du plus bas au plus haut (feuille de route : G, F, E, D, C, B, A, S, SS, SSS).
    /// La liste est fixe : l'histoire et les marchands nomment un rang sans dépendre du fichier de la guilde ; c'est le fichier de la guilde
    /// qui dit quels rangs sont ouverts (G, F et E dans les Vallons, choix de Damien du 29 octobre 2026).
    /// </summary>
    public static class GuildRanks
    {
        /// <summary>Identifiants des rangs, du plus bas au plus haut.</summary>
        public static IReadOnlyList<string> Ids { get; } = new ReadOnlyCollection<string>(new[] { "g", "f", "e", "d", "c", "b", "a", "s", "ss", "sss" });

        /// <summary>Points de guilde donnés par un seul effet ou un seul accomplissement, au plus.</summary>
        public const long MaxAward = 100_000;

        /// <summary>Rang de <paramref name="id"/> dans <see cref="Ids"/> (0 : G) ; -1 pour un identifiant inconnu.</summary>
        public static int IndexOf(string? id)
        {
            for (int i = 0; i < Ids.Count; i++)
                if (Ids[i] == id) return i;
            return -1;
        }

        public static bool IsRank(string? id) => IndexOf(id) >= 0;

        /// <summary>Texte des rangs possibles, pour les messages d'erreur.</summary>
        public static string Known => string.Join(", ", Ids);
    }

    // --- Documents (avant validation) ------------------------------------------------------------------

    /// <summary>Sous-lot V1.7 : la guilde telle que lue (<c>kind: "guild"</c>, <c>data/v0/world/guild.json</c>).</summary>
    public sealed class GuildDocument
    {
        public int? SchemaVersion { get; set; }
        public string? Kind { get; set; }
        public string? ContentVersion { get; set; }
        public GuildData? Data { get; set; }
    }

    public sealed class GuildData
    {
        /// <summary>PNJ de l'histoire qui tient la guilde (la maîtresse de guilde) : on lui parle pour ouvrir le tableau des contrats.</summary>
        public string? Npc { get; set; }

        /// <summary>Contrats en cours à la fois, au plus.</summary>
        public int? MaxActive { get; set; }

        /// <summary>Contrats tirés chaque jour du monde, pour chaque rang ouvert.</summary>
        public int? Daily { get; set; }

        public List<RankData?>? Ranks { get; set; }
        public List<TitleData?>? Titles { get; set; }
        public List<ContractData?>? Contracts { get; set; }
        public List<GateData?>? Gates { get; set; }
        public PointsData? Points { get; set; }
    }

    public sealed class RankData
    {
        public string? Id { get; set; }

        /// <summary>Clé du nom du rang (« Rang F »).</summary>
        public string? Name { get; set; }

        /// <summary>Titre gagné en atteignant ce rang (identifiant « title.… »).</summary>
        public string? Title { get; set; }

        /// <summary>Points de guilde qui ouvrent l'épreuve de ce rang (0 pour G, gagné à l'inscription).</summary>
        public long? Points { get; set; }

        /// <summary>Rang ouvert dans le jeu actuel (G, F, E dans les Vallons) ; les autres viendront avec les régions suivantes.</summary>
        public bool? Open { get; set; }

        /// <summary>Quête de l'histoire qui est l'épreuve de promotion de ce rang (null pour G et pour un rang fermé).</summary>
        public string? Trial { get; set; }

        /// <summary>Le champ <c>trial</c> est présent (null compris).</summary>
        public bool HasTrial { get; set; }
    }

    public sealed class TitleData
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
    }

    public sealed class ContractData
    {
        public string? Id { get; set; }
        public string? Rank { get; set; }

        /// <summary>Sorte : « hunt » (chasse), « delivery » (livraison), « bounty » (prime).</summary>
        public string? Kind { get; set; }

        public string? Name { get; set; }

        /// <summary>Clé du texte qui décrit le contrat (« Trois loups rôdent près du pré… »).</summary>
        public string? Text { get; set; }

        /// <summary>Objectif, dans la forme des objectifs de quête (<c>kind</c>, <c>id</c>, <c>value</c>, <c>npc</c>).</summary>
        public TermData? Objective { get; set; }

        /// <summary>Pièces de cuivre données à la réussite (0 : aucune).</summary>
        public long? Money { get; set; }

        /// <summary>Points de guilde donnés à la réussite.</summary>
        public long? Points { get; set; }

        /// <summary>Expérience donnée à la réussite (0 : aucune).</summary>
        public long? Xp { get; set; }
    }

    public sealed class GateData
    {
        /// <summary>Zone de la porte (« zone.guilde »).</summary>
        public string? Zone { get; set; }

        /// <summary>Porte de cette zone qu'on ne passe qu'à partir du rang <see cref="Rank"/>.</summary>
        public string? Door { get; set; }

        public string? Rank { get; set; }
    }

    public sealed class PointsData
    {
        public long? MainQuest { get; set; }
        public long? SideQuest { get; set; }
        public long? Discovery { get; set; }
        public List<RarePointsData?>? Rare { get; set; }
        public List<FactPointsData?>? Facts { get; set; }
    }

    public sealed class RarePointsData
    {
        public string? Creature { get; set; }
        public long? Points { get; set; }
    }

    public sealed class FactPointsData
    {
        public string? Fact { get; set; }
        public long? Points { get; set; }
    }

    // --- Modèle validé ---------------------------------------------------------------------------------

    /// <summary>Sous-lot V1.7 : un rang de la guilde.</summary>
    public sealed class GuildRank
    {
        public string Id { get; }

        /// <summary>Rang dans <see cref="GuildRanks.Ids"/> (0 : G).</summary>
        public int Index { get; }

        public string Name { get; }

        /// <summary>Titre gagné en atteignant ce rang.</summary>
        public string Title { get; }

        /// <summary>Points de guilde qui ouvrent l'épreuve de ce rang (cumulés depuis l'inscription).</summary>
        public long Points { get; }

        public bool Open { get; }

        /// <summary>Quête de l'histoire, épreuve de promotion vers ce rang ; null pour G et pour un rang fermé.</summary>
        public string? Trial { get; }

        internal GuildRank(string id, int index, string name, string title, long points, bool open, string? trial)
        {
            Id = id;
            Index = index;
            Name = name;
            Title = title;
            Points = points;
            Open = open;
            Trial = trial;
        }
    }

    /// <summary>Sous-lot V1.7 : un titre (affiché sous le nom du personnage) et la clé de son texte.</summary>
    public sealed class GuildTitle
    {
        public string Id { get; }
        public string Name { get; }

        internal GuildTitle(string id, string name)
        {
            Id = id;
            Name = name;
        }
    }

    /// <summary>Sous-lot V1.7 : sorte d'un contrat.</summary>
    public enum ContractKind
    {
        /// <summary>Chasse : vaincre des créatures (« hunt »).</summary>
        Hunt = 0,

        /// <summary>Livraison : apporter des objets à un PNJ (« delivery »).</summary>
        Delivery = 1,

        /// <summary>Prime : vaincre un ennemi nommé (« bounty »).</summary>
        Bounty = 2,
    }

    /// <summary>
    /// Sous-lot V1.7 : un contrat de la guilde. Son objectif est un objectif du moteur des quêtes (V1.6) : <c>defeat</c>, <c>defeatFamily</c>
    /// ou <c>deliver</c> ; ses récompenses sont des effets de l'histoire (<c>giveMoney</c>, <c>giveXp</c>, <c>giveGuildPoints</c>), appliqués
    /// en entier ou pas du tout avec la remise des objets livrés.
    /// </summary>
    public sealed class GuildContract
    {
        public string Id { get; }
        public string Rank { get; }
        public ContractKind Kind { get; }
        public string Name { get; }
        public string Text { get; }
        public Objective Objective { get; }
        public long Money { get; }
        public long Points { get; }
        public long Xp { get; }

        /// <summary>Effets de la réussite : pièces et expérience (s'il y en a), puis les points de guilde.</summary>
        public IReadOnlyList<StoryEffect> Rewards { get; }

        internal GuildContract(string id, string rank, ContractKind kind, string name, string text, Objective objective, long money, long points, long xp)
        {
            Id = id;
            Rank = rank;
            Kind = kind;
            Name = name;
            Text = text;
            Objective = objective;
            Money = money;
            Points = points;
            Xp = xp;
            var rewards = new List<StoryEffect>();
            if (money > 0) rewards.Add(new StoryEffect(EffectKind.GiveMoney, string.Empty, money));
            if (xp > 0) rewards.Add(new StoryEffect(EffectKind.GiveXp, string.Empty, xp));
            rewards.Add(new StoryEffect(EffectKind.GiveGuildPoints, string.Empty, points));
            Rewards = Frozen.List(rewards, nameof(rewards));
        }
    }

    /// <summary>Sous-lot V1.7 : une porte qu'on ne passe qu'à partir d'un rang (« accès à des lieux »).</summary>
    public sealed class GuildGate
    {
        public string Zone { get; }
        public string Door { get; }
        public string Rank { get; }

        internal GuildGate(string zone, string door, string rank)
        {
            Zone = zone;
            Door = door;
            Rank = rank;
        }
    }

    /// <summary>
    /// Sous-lot V1.7 : la Guilde des aventuriers validée. Rangs (les dix, dans l'ordre ; G, F et E ouverts dans les Vallons), seuils de
    /// points, épreuves de promotion (quêtes de l'histoire), titres, contrats tirés chaque jour, portes ouvertes par le rang et points donnés
    /// par les accomplissements (quêtes, découvertes, créatures rares, faits).
    /// </summary>
    public sealed class GuildBook
    {
        private readonly IReadOnlyDictionary<string, long> _rare, _facts;

        public int SchemaVersion { get; }
        public string ContentVersion { get; }

        /// <summary>PNJ de la maîtresse de guilde.</summary>
        public string Npc { get; }

        public int MaxActive { get; }
        public int Daily { get; }

        /// <summary>Les dix rangs, du plus bas au plus haut (<see cref="GuildRanks.Ids"/>).</summary>
        public IReadOnlyList<GuildRank> Ranks { get; }

        public IReadOnlyList<GuildTitle> Titles { get; }
        public IReadOnlyList<GuildContract> Contracts { get; }
        public IReadOnlyList<GuildGate> Gates { get; }

        /// <summary>Points d'une quête principale finie, d'une secondaire, d'une découverte.</summary>
        public long MainQuestPoints { get; }

        public long SideQuestPoints { get; }
        public long DiscoveryPoints { get; }

        /// <summary>Points de la première victoire sur chaque créature rare (population).</summary>
        public IReadOnlyDictionary<string, long> RarePoints => _rare;

        /// <summary>Points d'un fait appris (victoire sur un chef…).</summary>
        public IReadOnlyDictionary<string, long> FactPoints => _facts;

        internal GuildBook(int schemaVersion, string contentVersion, string npc, int maxActive, int daily, IEnumerable<GuildRank> ranks,
            IEnumerable<GuildTitle> titles, IEnumerable<GuildContract> contracts, IEnumerable<GuildGate> gates, long mainQuest, long sideQuest,
            long discovery, IEnumerable<KeyValuePair<string, long>> rare, IEnumerable<KeyValuePair<string, long>> facts)
        {
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion;
            Npc = npc;
            MaxActive = maxActive;
            Daily = daily;
            Ranks = Frozen.List(ranks, nameof(ranks));
            Titles = Frozen.List(titles, nameof(titles));
            Contracts = Frozen.List(contracts, nameof(contracts));
            Gates = Frozen.List(gates, nameof(gates));
            MainQuestPoints = mainQuest;
            SideQuestPoints = sideQuest;
            DiscoveryPoints = discovery;
            _rare = Frozen.Dictionary(rare, nameof(rare));
            _facts = Frozen.Dictionary(facts, nameof(facts));
        }

        /// <exception cref="ArgumentException">Rang inconnu.</exception>
        public GuildRank Rank(string id) =>
            Ranks.FirstOrDefault(r => r.Id == id) ?? throw new ArgumentException($"Rang « {id} » inconnu.", nameof(id));

        public bool TryGetTitle(string id, out GuildTitle title)
        {
            title = Titles.FirstOrDefault(t => t.Id == id)!;
            return title != null;
        }

        public bool TryGetContract(string id, out GuildContract contract)
        {
            contract = Contracts.FirstOrDefault(c => c.Id == id)!;
            return contract != null;
        }

        /// <exception cref="ArgumentException">Contrat inconnu.</exception>
        public GuildContract Contract(string id) =>
            Contracts.FirstOrDefault(c => c.Id == id) ?? throw new ArgumentException($"Contrat « {id} » inconnu.", nameof(id));

        /// <summary>Rang dont <paramref name="quest"/> est l'épreuve de promotion, ou null.</summary>
        public GuildRank? TrialOf(string quest) => Ranks.FirstOrDefault(r => r.Trial != null && r.Trial == quest);

        /// <summary>Rang requis pour passer la porte <paramref name="door"/> de la zone <paramref name="zone"/>, ou null (porte libre).</summary>
        public string? GateOf(string zone, string door) => Gates.FirstOrDefault(g => g.Zone == zone && g.Door == door)?.Rank;

        /// <summary>Plus haut rang ouvert (E dans les Vallons).</summary>
        public GuildRank TopOpen => Ranks.Last(r => r.Open);
    }

    public sealed class GuildLoadResult
    {
        public ValidationReport Report { get; }
        public GuildBook? Book { get; }
        public bool IsValid => Book != null;

        internal GuildLoadResult(ValidationReport report, GuildBook? book)
        {
            Report = report;
            Book = book;
        }
    }

    /// <summary>
    /// Sous-lot V1.7 : validateur unique de la guilde (<c>kind: "guild"</c>), contre les textes, l'histoire (PNJ, quêtes des épreuves, faits),
    /// la zone de l'histoire (populations, familles), les zones reliées (portes), le catalogue des objets et les marchands (rang de la
    /// boutique de la guilde). Erreurs avec leur chemin précis.
    /// </summary>
    public static class GuildValidator
    {
        public const string Kind = "guild";

        /// <summary>
        /// Version 1 (sous-lot V1.7) : <c>npc</c>, <c>maxActive</c>, <c>daily</c>, <c>ranks</c> (les dix, dans l'ordre : <c>id</c>, <c>name</c>,
        /// <c>title</c>, <c>points</c>, <c>open</c>, <c>trial</c>), <c>titles</c> (<c>id</c>, <c>name</c>), <c>contracts</c> (<c>id</c>,
        /// <c>rank</c>, <c>kind</c>, <c>name</c>, <c>text</c>, <c>objective</c>, <c>money</c>, <c>points</c>, <c>xp</c>), <c>gates</c>
        /// (<c>zone</c>, <c>door</c>, <c>rank</c>) et <c>points</c> (<c>mainQuest</c>, <c>sideQuest</c>, <c>discovery</c>, <c>rare</c>,
        /// <c>facts</c>).
        /// </summary>
        public const int CurrentSchemaVersion = 1;

        public const int MaxTitles = 64, MaxContracts = 256, MaxGates = 32, MaxRare = 64, MaxFacts = 64;

        /// <summary>Contrats en cours à la fois, et contrats tirés chaque jour pour un rang, au plus.</summary>
        public const int MaxActiveLimit = 8, MaxDaily = 8;

        /// <summary>Seuil de points d'un rang, au plus (celui de la carte : <see cref="Saves.SavedGuildCard.MaxPoints"/>).</summary>
        public const long MaxThreshold = Saves.SavedGuildCard.MaxPoints;

        private static readonly Regex TitleId = new Regex(@"^title(\.[a-z0-9_]+)+\z", RegexOptions.CultureInvariant);
        private static readonly Regex ContractId = new Regex(@"^contract(\.[a-z0-9_]+)+\z", RegexOptions.CultureInvariant);

        public static bool IsTitleId(string? id) => id != null && id.Length <= 200 && TitleId.IsMatch(id);

        public static bool IsContractId(string? id) => id != null && id.Length <= 200 && ContractId.IsMatch(id);

        public static IReadOnlyDictionary<string, ContractKind> Kinds { get; } = new ReadOnlyDictionary<string, ContractKind>(
            new Dictionary<string, ContractKind>(StringComparer.Ordinal) { ["hunt"] = ContractKind.Hunt, ["delivery"] = ContractKind.Delivery, ["bounty"] = ContractKind.Bounty });

        /// <param name="zones">Zones du monde reliées par des portes (celle de l'histoire comprise), pour les portes ouvertes par le rang.</param>
        /// <param name="merchants">Marchands (null : non vérifiés) : un rang demandé par une boutique ou une offre doit être ouvert.</param>
        public static GuildLoadResult Validate(GuildDocument document, TextCatalog texts, StoryBook story, ZoneDefinition zone, IReadOnlyList<ZoneDefinition> zones,
            ItemCatalog items, MerchantBook? merchants = null, ValidationReport? report = null)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));
            if (texts is null) throw new ArgumentNullException(nameof(texts));
            if (story is null) throw new ArgumentNullException(nameof(story));
            if (zone is null) throw new ArgumentNullException(nameof(zone));
            if (zones is null) throw new ArgumentNullException(nameof(zones));
            if (items is null) throw new ArgumentNullException(nameof(items));
            ValidationReport r = report ?? new ValidationReport();
            if (!HeaderValidator.Validate(document.SchemaVersion, document.Kind, document.ContentVersion, Kind, CurrentSchemaVersion, r))
                return new GuildLoadResult(r, null);
            GuildData? d = document.Data;
            if (d is null) { Missing(r, "$.data", "maîtresse, rangs, titres, contrats, portes et points"); return new GuildLoadResult(r, null); }
            var c = new Checker(r, texts, story, zone, zones, items);
            GuildBook? book = c.Run(d, document.SchemaVersion!.Value, document.ContentVersion, merchants);
            return new GuildLoadResult(r, r.IsValid ? book : null);
        }

        private sealed class Checker
        {
            private readonly ValidationReport _r;
            private readonly TextCatalog _texts;
            private readonly StoryBook _story;
            private readonly ZoneDefinition _zone;
            private readonly IReadOnlyList<ZoneDefinition> _zones;
            private readonly ItemCatalog _items;

            public Checker(ValidationReport r, TextCatalog texts, StoryBook story, ZoneDefinition zone, IReadOnlyList<ZoneDefinition> zones, ItemCatalog items)
            {
                _r = r;
                _texts = texts;
                _story = story;
                _zone = zone;
                _zones = zones;
                _items = items;
            }

            public GuildBook? Run(GuildData d, int schemaVersion, string? contentVersion, MerchantBook? merchants)
            {
                bool ok = true;
                if (d.Npc is null) { Missing(_r, "$.data.npc", "PNJ de la maîtresse de guilde"); ok = false; }
                else if (!_story.HasNpc(d.Npc)) { _r.Add("$.data.npc", ValidationCode.UnknownReference, $"PNJ inconnu « {d.Npc} »."); ok = false; }
                if (d.MaxActive is null) { Missing(_r, "$.data.maxActive", "contrats en cours à la fois, au plus"); ok = false; }
                else if (d.MaxActive < 1 || d.MaxActive > MaxActiveLimit) { _r.Add("$.data.maxActive", ValidationCode.OutOfRange, $"hors de [1, {MaxActiveLimit}]."); ok = false; }
                if (d.Daily is null) { Missing(_r, "$.data.daily", "contrats tirés chaque jour pour chaque rang ouvert"); ok = false; }
                else if (d.Daily < 1 || d.Daily > MaxDaily) { _r.Add("$.data.daily", ValidationCode.OutOfRange, $"hors de [1, {MaxDaily}]."); ok = false; }

                List<GuildTitle> titles = CheckTitles(d.Titles);
                List<GuildRank> ranks = CheckRanks(d.Ranks, titles);
                // Rangs ouverts d'après le fichier, même ceux qui ont une autre erreur (pas d'erreurs en cascade sur les contrats et les portes).
                var open = new HashSet<string>(StringComparer.Ordinal);
                if (d.Ranks != null && d.Ranks.Count == GuildRanks.Ids.Count)
                    for (int i = 0; i < d.Ranks.Count; i++)
                        if (d.Ranks[i] is RankData x && x.Open == true && x.Id == GuildRanks.Ids[i]) open.Add(x.Id);
                List<GuildContract> contracts = CheckContracts(d.Contracts, open, d.Npc, d.Daily);
                List<GuildGate> gates = CheckGates(d.Gates, open);
                CheckPoints(d.Points, out long main, out long side, out long discovery, out var rare, out var facts);
                if (merchants != null) CheckMerchants(merchants, open);
                if (!ok || !_r.IsValid || contentVersion is null) return null;
                return new GuildBook(schemaVersion, contentVersion, d.Npc!, d.MaxActive!.Value, d.Daily!.Value, ranks, titles, contracts, gates, main, side,
                    discovery, rare, facts);
            }

            private List<GuildTitle> CheckTitles(List<TitleData?>? list)
            {
                var titles = new List<GuildTitle>();
                const string path = "$.data.titles";
                if (list is null) { Missing(_r, path, "titres"); return titles; }
                if (list.Count < 1 || list.Count > MaxTitles) { _r.Add(path, list.Count < 1 ? ValidationCode.OutOfRange : ValidationCode.LimitExceeded, $"de 1 à {MaxTitles} titres."); return titles; }
                for (int i = 0; i < list.Count; i++)
                {
                    string p = $"{path}[{i}]";
                    if (list[i] is not TitleData t) { Missing(_r, p, "titre"); continue; }
                    bool ok = true;
                    if (t.Id is null) { Missing(_r, p + ".id", "identifiant « title.… »"); ok = false; }
                    else if (!IsTitleId(t.Id)) { _r.Add(p + ".id", ValidationCode.InvalidValue, $"identifiant « {t.Id} » mal formé : « title.… »."); ok = false; }
                    else if (list.Take(i).Any(x => x?.Id == t.Id)) { _r.Add(p + ".id", ValidationCode.DuplicateId, $"titre « {t.Id} » déjà donné plus haut."); ok = false; }
                    ok &= Text(t.Name, p + ".name");
                    if (ok) titles.Add(new GuildTitle(t.Id!, t.Name!));
                }
                return titles;
            }

            private List<GuildRank> CheckRanks(List<RankData?>? list, List<GuildTitle> titles)
            {
                var ranks = new List<GuildRank>();
                const string path = "$.data.ranks";
                if (list is null) { Missing(_r, path, $"les {GuildRanks.Ids.Count} rangs, de G à SSS"); return ranks; }
                if (list.Count != GuildRanks.Ids.Count)
                { _r.Add(path, ValidationCode.OutOfRange, $"{list.Count} rangs : les {GuildRanks.Ids.Count} rangs attendus, dans l'ordre ({GuildRanks.Known})."); return ranks; }
                var trials = new HashSet<string>(StringComparer.Ordinal);
                bool closedSeen = false;
                long previous = -1;
                for (int i = 0; i < list.Count; i++)
                {
                    string p = $"{path}[{i}]";
                    if (list[i] is not RankData x) { Missing(_r, p, "rang"); continue; }
                    bool ok = true;
                    string expected = GuildRanks.Ids[i];
                    if (x.Id is null) { Missing(_r, p + ".id", $"rang « {expected} »"); ok = false; }
                    else if (x.Id != expected) { _r.Add(p + ".id", ValidationCode.InvalidValue, $"rang « {x.Id} » : « {expected} » attendu à cette place (ordre {GuildRanks.Known})."); ok = false; }
                    ok &= Text(x.Name, p + ".name");
                    if (x.Title is null) { Missing(_r, p + ".title", "titre gagné à ce rang"); ok = false; }
                    else if (titles.All(t => t.Id != x.Title)) { _r.Add(p + ".title", ValidationCode.UnknownReference, $"titre inconnu « {x.Title} »."); ok = false; }
                    if (x.Points is null) { Missing(_r, p + ".points", "points qui ouvrent l'épreuve"); ok = false; }
                    else if (i == 0 && x.Points != 0) { _r.Add(p + ".points", ValidationCode.InvalidValue, "le rang G se gagne à l'inscription : 0 point."); ok = false; }
                    else if (x.Points < 0 || x.Points > MaxThreshold) { _r.Add(p + ".points", ValidationCode.OutOfRange, $"hors de [0, {MaxThreshold}]."); ok = false; }
                    else if (x.Points <= previous) { _r.Add(p + ".points", ValidationCode.InvalidValue, "seuils strictement croissants d'un rang au suivant."); ok = false; }
                    if (x.Points is long pts) previous = Math.Max(previous, pts);
                    if (x.Open is null) { Missing(_r, p + ".open", "rang ouvert (vrai) ou à venir (faux)"); ok = false; }
                    else if (i == 0 && x.Open == false) { _r.Add(p + ".open", ValidationCode.InvalidValue, "le rang G est toujours ouvert."); ok = false; }
                    else if (x.Open == true && closedSeen) { _r.Add(p + ".open", ValidationCode.InvalidValue, "un rang ouvert après un rang fermé : les rangs s'ouvrent dans l'ordre."); ok = false; }
                    if (x.Open == false) closedSeen = true;
                    // Épreuve : une quête de l'histoire, pour chaque rang ouvert au-dessus de G ; aucune pour G ni pour un rang fermé.
                    if (!x.HasTrial && x.Trial is null) { Missing(_r, p + ".trial", "quête de l'épreuve, ou null"); ok = false; }
                    else if (x.Trial is null)
                    {
                        if (i > 0 && x.Open == true) { _r.Add(p + ".trial", ValidationCode.MissingField, "un rang ouvert au-dessus de G a son épreuve de promotion."); ok = false; }
                    }
                    else if (i == 0 || x.Open != true) { _r.Add(p + ".trial", ValidationCode.InvalidValue, "pas d'épreuve pour le rang G ni pour un rang fermé."); ok = false; }
                    else if (!_story.HasQuest(x.Trial)) { _r.Add(p + ".trial", ValidationCode.UnknownReference, $"quête inconnue « {x.Trial} »."); ok = false; }
                    else if (!trials.Add(x.Trial)) { _r.Add(p + ".trial", ValidationCode.DuplicateId, $"quête « {x.Trial} » déjà l'épreuve d'un autre rang."); ok = false; }
                    else if (_story.Quest(x.Trial).RewardChoices.Count > 0)
                    { _r.Add(p + ".trial", ValidationCode.InvalidValue, "une épreuve n'a pas de récompense au choix : la promotion vient à sa dernière étape."); ok = false; }
                    else if (StartedByStory(x.Trial) is string where)
                    { _r.Add(p + ".trial", ValidationCode.InvalidValue, $"l'épreuve ne se commence qu'au tableau de la guilde, pas par l'histoire ({where})."); ok = false; }
                    if (ok) ranks.Add(new GuildRank(x.Id!, i, x.Name!, x.Title!, x.Points!.Value, x.Open!.Value, x.Trial));
                }
                return ranks;
            }

            /// <summary>Un effet <c>startQuest</c> de l'histoire qui commence <paramref name="quest"/> (où), ou null.</summary>
            private string? StartedByStory(string quest)
            {
                foreach (Dialogue d in _story.Dialogues)
                    foreach (DialogueNode n in d.Nodes)
                        if (n.Choices.Any(c => c.Effects.Any(e => e.Kind == EffectKind.StartQuest && e.Id == quest))) return $"{d.Id}, nœud « {n.Id} »";
                foreach (Quest q in _story.Quests)
                    if (q.Effects.Concat(q.Steps.SelectMany(s => s.Effects)).Any(e => e.Kind == EffectKind.StartQuest && e.Id == quest)) return q.Id;
                return null;
            }

            private List<GuildContract> CheckContracts(List<ContractData?>? list, HashSet<string> open, string? npc, int? daily)
            {
                var contracts = new List<GuildContract>();
                const string path = "$.data.contracts";
                if (list is null) { Missing(_r, path, "contrats"); return contracts; }
                if (list.Count < 1 || list.Count > MaxContracts) { _r.Add(path, list.Count < 1 ? ValidationCode.OutOfRange : ValidationCode.LimitExceeded, $"de 1 à {MaxContracts} contrats."); return contracts; }
                for (int i = 0; i < list.Count; i++)
                {
                    string p = $"{path}[{i}]";
                    if (list[i] is not ContractData x) { Missing(_r, p, "contrat"); continue; }
                    bool ok = true;
                    if (x.Id is null) { Missing(_r, p + ".id", "identifiant « contract.… »"); ok = false; }
                    else if (!IsContractId(x.Id)) { _r.Add(p + ".id", ValidationCode.InvalidValue, $"identifiant « {x.Id} » mal formé : « contract.… »."); ok = false; }
                    else if (list.Take(i).Any(o => o?.Id == x.Id)) { _r.Add(p + ".id", ValidationCode.DuplicateId, $"contrat « {x.Id} » déjà donné plus haut."); ok = false; }
                    if (x.Rank is null) { Missing(_r, p + ".rank", "rang du contrat"); ok = false; }
                    else if (!GuildRanks.IsRank(x.Rank)) { _r.Add(p + ".rank", ValidationCode.UnknownReference, $"rang inconnu « {x.Rank} » ; possibles : {GuildRanks.Known}."); ok = false; }
                    else if (!open.Contains(x.Rank)) { _r.Add(p + ".rank", ValidationCode.InvalidValue, $"rang « {x.Rank} » fermé : le contrat ne serait jamais proposé."); ok = false; }
                    ContractKind kind = ContractKind.Hunt;
                    if (x.Kind is null) { Missing(_r, p + ".kind", "« hunt », « delivery » ou « bounty »"); ok = false; }
                    else if (!Kinds.TryGetValue(x.Kind, out kind)) { _r.Add(p + ".kind", ValidationCode.InvalidValue, $"sorte inconnue « {x.Kind} » ; possibles : {string.Join(", ", Kinds.Keys)}."); ok = false; }
                    ok &= Text(x.Name, p + ".name");
                    ok &= Text(x.Text, p + ".text");
                    Objective? objective = ObjectiveOf(x.Objective, p + ".objective", x.Kind is null || !Kinds.ContainsKey(x.Kind) ? (ContractKind?)null : kind, npc);
                    ok &= objective != null;
                    ok &= Amount(x.Money, p + ".money", 0, StoryValidator.MaxMoney, "pièces de cuivre");
                    ok &= Amount(x.Points, p + ".points", 1, GuildRanks.MaxAward, "points de guilde");
                    ok &= Amount(x.Xp, p + ".xp", 0, StoryValidator.MaxXp, "expérience");
                    if (ok) contracts.Add(new GuildContract(x.Id!, x.Rank!, kind, x.Name!, x.Text!, objective!, x.Money!.Value, x.Points!.Value, x.Xp!.Value));
                }
                // Chaque rang ouvert a au moins autant de contrats que le tirage du jour en prend.
                if (daily is int n && n >= 1 && n <= MaxDaily && _r.IsValid)
                    foreach (string rank in GuildRanks.Ids.Where(open.Contains))
                    {
                        int count = contracts.Count(x => x.Rank == rank);
                        if (count < n) _r.Add(path, ValidationCode.OutOfRange, $"rang « {rank} » : {count} contrats, au moins {n} (tirage du jour, daily).");
                    }
                return contracts;
            }

            private Objective? ObjectiveOf(TermData? t, string p, ContractKind? kind, string? npc)
            {
                if (t is null) { Missing(_r, p, "objectif"); return null; }
                if (t.Kind is null) { Missing(_r, p + ".kind", "defeat, defeatFamily ou deliver"); return null; }
                if (!StoryValidator.Objectives.TryGetValue(t.Kind, out ObjectiveKind o) || !(o is ObjectiveKind.Defeat or ObjectiveKind.DefeatFamily or ObjectiveKind.Deliver))
                { _r.Add(p + ".kind", ValidationCode.InvalidValue, $"objectif « {t.Kind} » : un contrat demande defeat, defeatFamily ou deliver."); return null; }
                // La sorte et l'objectif vont ensemble : une chasse fait vaincre, une prime vainc un ennemi nommé, une livraison livre.
                bool fits = kind switch
                {
                    ContractKind.Hunt => o is ObjectiveKind.Defeat or ObjectiveKind.DefeatFamily,
                    ContractKind.Bounty => o == ObjectiveKind.Defeat,
                    ContractKind.Delivery => o == ObjectiveKind.Deliver,
                    _ => true,
                };
                if (!fits) { _r.Add(p + ".kind", ValidationCode.InvalidValue, $"objectif « {t.Kind} » contraire à la sorte du contrat."); return null; }
                if (t.Step != null || t.Present.Contains("step")) { _r.Add(p + ".step", ValidationCode.InvalidValue, "pas d'étape dans un objectif."); return null; }
                if (t.Id is null) { Missing(_r, p + ".id", o == ObjectiveKind.Deliver ? "objet" : "créature ou famille"); return null; }
                bool ok = true;
                switch (o)
                {
                    case ObjectiveKind.Defeat when _zone.Populations.All(x => x.Creature != t.Id):
                        _r.Add(p + ".id", ValidationCode.UnknownReference, $"population inconnue « {t.Id} »."); ok = false;
                        break;
                    case ObjectiveKind.DefeatFamily when _zone.Populations.All(x => x.Family != t.Id):
                        _r.Add(p + ".id", ValidationCode.UnknownReference, $"famille de créatures inconnue « {t.Id} »."); ok = false;
                        break;
                    case ObjectiveKind.Deliver when !_items.TryGetItem(t.Id, out _):
                        _r.Add(p + ".id", ValidationCode.UnknownReference, $"objet inconnu « {t.Id} »."); ok = false;
                        break;
                }
                long max = o == ObjectiveKind.Deliver ? StoryValidator.MaxCount : StoryValidator.MaxDefeat;
                if (t.Value is null) { Missing(_r, p + ".value", $"nombre de 1 à {max}"); ok = false; }
                else if (t.Value < 1 || t.Value > max) { _r.Add(p + ".value", ValidationCode.OutOfRange, $"nombre hors de [1, {max}]."); ok = false; }
                // Une livraison se fait à la maîtresse de guilde, sauf autre PNJ nommé ; les autres objectifs ne nomment pas de PNJ.
                string? to = null;
                if (o == ObjectiveKind.Deliver)
                {
                    to = t.Npc ?? npc;
                    if (to is null) { Missing(_r, p + ".npc", "PNJ à qui livrer"); ok = false; }
                    else if (!_story.HasNpc(to)) { _r.Add(p + ".npc", ValidationCode.UnknownReference, $"PNJ inconnu « {to} »."); ok = false; }
                }
                else if (t.Npc != null || t.Present.Contains("npc")) { _r.Add(p + ".npc", ValidationCode.InvalidValue, "seule une livraison nomme un PNJ."); ok = false; }
                return ok ? new Objective(o, t.Id, t.Value!.Value, to) : null;
            }

            private List<GuildGate> CheckGates(List<GateData?>? list, HashSet<string> open)
            {
                var gates = new List<GuildGate>();
                const string path = "$.data.gates";
                if (list is null) { Missing(_r, path, "portes ouvertes par le rang, éventuellement aucune"); return gates; }
                if (list.Count > MaxGates) { _r.Add(path, ValidationCode.LimitExceeded, $"au plus {MaxGates} portes."); return gates; }
                for (int i = 0; i < list.Count; i++)
                {
                    string p = $"{path}[{i}]";
                    if (list[i] is not GateData g) { Missing(_r, p, "porte"); continue; }
                    bool ok = true;
                    ZoneDefinition? zone = null;
                    if (g.Zone is null) { Missing(_r, p + ".zone", "zone de la porte"); ok = false; }
                    else if ((zone = _zones.FirstOrDefault(z => z.Id == g.Zone)) is null) { _r.Add(p + ".zone", ValidationCode.UnknownReference, $"zone inconnue « {g.Zone} »."); ok = false; }
                    if (g.Door is null) { Missing(_r, p + ".door", "porte de la zone"); ok = false; }
                    else if (zone != null && zone.DoorOf(g.Door) is null) { _r.Add(p + ".door", ValidationCode.UnknownReference, $"porte inconnue « {g.Door} » de {g.Zone}."); ok = false; }
                    else if (list.Take(i).Any(x => x?.Zone == g.Zone && x?.Door == g.Door)) { _r.Add(p + ".door", ValidationCode.DuplicateId, "porte déjà gardée plus haut."); ok = false; }
                    if (g.Rank is null) { Missing(_r, p + ".rank", "rang qui ouvre la porte"); ok = false; }
                    else if (!GuildRanks.IsRank(g.Rank)) { _r.Add(p + ".rank", ValidationCode.UnknownReference, $"rang inconnu « {g.Rank} » ; possibles : {GuildRanks.Known}."); ok = false; }
                    else if (GuildRanks.IndexOf(g.Rank) == 0) { _r.Add(p + ".rank", ValidationCode.InvalidValue, "le rang G ne garde rien (il se gagne à l'inscription) : un rang au-dessus."); ok = false; }
                    else if (!open.Contains(g.Rank)) { _r.Add(p + ".rank", ValidationCode.InvalidValue, $"rang « {g.Rank} » fermé : la porte ne s'ouvrirait jamais."); ok = false; }
                    if (ok) gates.Add(new GuildGate(g.Zone!, g.Door!, g.Rank!));
                }
                return gates;
            }

            private void CheckPoints(PointsData? x, out long main, out long side, out long discovery, out List<KeyValuePair<string, long>> rare,
                out List<KeyValuePair<string, long>> facts)
            {
                main = side = discovery = 0;
                rare = new List<KeyValuePair<string, long>>();
                facts = new List<KeyValuePair<string, long>>();
                const string path = "$.data.points";
                if (x is null) { Missing(_r, path, "points des accomplissements"); return; }
                if (Amount(x.MainQuest, path + ".mainQuest", 0, GuildRanks.MaxAward, "points d'une quête principale finie")) main = x.MainQuest!.Value;
                if (Amount(x.SideQuest, path + ".sideQuest", 0, GuildRanks.MaxAward, "points d'une quête secondaire finie")) side = x.SideQuest!.Value;
                if (Amount(x.Discovery, path + ".discovery", 0, GuildRanks.MaxAward, "points d'une découverte")) discovery = x.Discovery!.Value;
                if (x.Rare is null) Missing(_r, path + ".rare", "créatures rares, éventuellement aucune");
                else if (x.Rare.Count > MaxRare) _r.Add(path + ".rare", ValidationCode.LimitExceeded, $"au plus {MaxRare} créatures rares.");
                else
                    for (int i = 0; i < x.Rare.Count; i++)
                    {
                        string p = $"{path}.rare[{i}]";
                        if (x.Rare[i] is not RarePointsData c) { Missing(_r, p, "créature rare"); continue; }
                        bool ok = true;
                        if (c.Creature is null) { Missing(_r, p + ".creature", "population de la zone"); ok = false; }
                        else if (_zone.Populations.All(pop => pop.Creature != c.Creature)) { _r.Add(p + ".creature", ValidationCode.UnknownReference, $"population inconnue « {c.Creature} »."); ok = false; }
                        else if (rare.Any(e => e.Key == c.Creature) || x.Rare.Take(i).Any(o => o?.Creature == c.Creature))
                        { _r.Add(p + ".creature", ValidationCode.DuplicateId, $"créature « {c.Creature} » déjà donnée plus haut."); ok = false; }
                        ok &= Amount(c.Points, p + ".points", 1, GuildRanks.MaxAward, "points");
                        if (ok) rare.Add(new KeyValuePair<string, long>(c.Creature!, c.Points!.Value));
                    }
                if (x.Facts is null) Missing(_r, path + ".facts", "faits, éventuellement aucun");
                else if (x.Facts.Count > MaxFacts) _r.Add(path + ".facts", ValidationCode.LimitExceeded, $"au plus {MaxFacts} faits.");
                else
                    for (int i = 0; i < x.Facts.Count; i++)
                    {
                        string p = $"{path}.facts[{i}]";
                        if (x.Facts[i] is not FactPointsData f) { Missing(_r, p, "fait"); continue; }
                        bool ok = true;
                        if (f.Fact is null) { Missing(_r, p + ".fact", "fait « fact.… »"); ok = false; }
                        else if (!Learnable(f.Fact)) { _r.Add(p + ".fact", ValidationCode.UnknownReference, $"fait « {f.Fact} » jamais appris (ni effet setFact, ni victoire sur un chef)."); ok = false; }
                        else if (x.Facts.Take(i).Any(o => o?.Fact == f.Fact)) { _r.Add(p + ".fact", ValidationCode.DuplicateId, $"fait « {f.Fact} » déjà donné plus haut."); ok = false; }
                        ok &= Amount(f.Points, p + ".points", 1, GuildRanks.MaxAward, "points");
                        if (ok) facts.Add(new KeyValuePair<string, long>(f.Fact!, f.Points!.Value));
                    }
            }

            /// <summary>Le fait s'apprend : par un effet <c>setFact</c> de l'histoire, ou par la victoire sur un chef de la zone.</summary>
            private bool Learnable(string fact)
            {
                if (_zone.Bosses.Any(b => b.Fact == fact)) return true;
                IEnumerable<StoryEffect> effects = _story.Dialogues.SelectMany(d => d.Nodes).SelectMany(n => n.Choices).SelectMany(c => c.Effects)
                    .Concat(_story.Quests.SelectMany(q => q.Effects.Concat(q.Steps.SelectMany(s => s.Effects))));
                return effects.Any(e => e.Kind == EffectKind.SetFact && e.Id == fact);
            }

            /// <summary>Une boutique (ou une offre) qui demande un rang demande un rang ouvert (sinon elle ne s'ouvrirait jamais).</summary>
            private void CheckMerchants(MerchantBook merchants, HashSet<string> open)
            {
                bool Closed(string? rank) => rank != null && !open.Contains(rank);
                foreach (Merchant m in merchants.Merchants)
                {
                    if (Closed(m.Rank)) _r.Add("$.data.ranks", ValidationCode.InvalidValue, $"la boutique « {m.Id} » demande le rang fermé « {m.Rank} ».");
                    foreach (MerchantOffer o in m.Offers.Where(o => Closed(o.Rank)))
                        _r.Add("$.data.ranks", ValidationCode.InvalidValue, $"l'offre « {o.Item} » de « {m.Id} » demande le rang fermé « {o.Rank} ».");
                }
            }

            private bool Amount(long? value, string path, long min, long max, string what)
            {
                if (value is null) { Missing(_r, path, $"{what}, de {min} à {max}"); return false; }
                if (value >= min && value <= max) return true;
                _r.Add(path, ValidationCode.OutOfRange, $"{what} hors de [{min}, {max}].");
                return false;
            }

            private bool Text(string? key, string path)
            {
                if (key is null) { Missing(_r, path, "clé de texte « text.… »"); return false; }
                if (_texts.Has(key)) return true;
                _r.Add(path, ValidationCode.UnknownReference, $"texte « {key} » absent des textes ({_texts.Language}).");
                return false;
            }
        }

        private static void Missing(ValidationReport report, string path, string what)
        {
            if (!report.HasErrorAtOrAbove(path)) report.Add(path, ValidationCode.MissingField, $"champ obligatoire absent ({what}).");
        }
    }
}
