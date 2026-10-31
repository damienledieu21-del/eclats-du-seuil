using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EdS.Data.Items;
using EdS.Data.Scenarios;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Collections;
using EdS.Domain.Space;

namespace EdS.Data.Crafting
{
    // --- Documents (avant validation) ------------------------------------------------------------------

    /// <summary>Sous-lot 12.6 : métiers, recettes et nœuds de récolte tels que lus (<c>kind: "crafting"</c>).</summary>
    public sealed class CraftingDocument
    {
        public int? SchemaVersion { get; set; }
        public string? Kind { get; set; }
        public string? ContentVersion { get; set; }
        public CraftingData? Data { get; set; }
    }

    public sealed class CraftingData
    {
        public int? MaxLevel { get; set; }
        public List<long?>? XpToLevel { get; set; }
        public List<ProfessionData?>? Professions { get; set; }
        public List<RecipeData?>? Recipes { get; set; }
        public List<NodeData?>? Nodes { get; set; }

        /// <summary>Sous-lot V1.4 (version 2) : petit jeu de forge (frappes) et chances de la qualité de fabrication.</summary>
        public ForgeData? Forge { get; set; }

        /// <summary>Sous-lot V1.4 (version 2) : recette au-dessus de son niveau, qui peut rater (une partie des matériaux rendue).</summary>
        public FailureData? Failure { get; set; }

        /// <summary>Sous-lot V1.4 (version 2) : réparation de l'équipement usé contre de l'or (prix, lieux, marchands).</summary>
        public RepairData? Repair { get; set; }
    }

    /// <summary>Sous-lot V1.4 : chance d'une qualité, en pour cent : base, part de l'adresse (frappes), part de chaque niveau au-dessus du requis.</summary>
    public sealed class ChanceData
    {
        public int? Base { get; set; }
        public int? PerSkill { get; set; }
        public int? PerLevel { get; set; }
    }

    public sealed class ForgeData
    {
        /// <summary>Nombre de frappes du petit jeu (trois : choix de Damien).</summary>
        public int? Strikes { get; set; }

        public ChanceData? Superior { get; set; }
        public ChanceData? Masterwork { get; set; }
    }

    public sealed class FailureData
    {
        /// <summary>Niveaux au-dessus du sien qu'un métier peut tenter, au plus.</summary>
        public int? MaxLevels { get; set; }

        /// <summary>Chance de rater par niveau manquant, en pour cent.</summary>
        public int? PercentPerLevel { get; set; }

        /// <summary>Chance de rater retirée par une adresse parfaite (trois frappes parfaites), en pour cent.</summary>
        public int? SkillReduction { get; set; }

        /// <summary>Part de chaque matériau rendue en cas d'échec, en pour cent (arrondie vers le bas).</summary>
        public int? RefundPercent { get; set; }

        /// <summary>Sous-lot V1.8 (version 3, réponse de Damien du 31 octobre 2026) : part de l'expérience de la recette gagnée en cas d'échec, en pour cent.</summary>
        public int? XpPercent { get; set; }
    }

    public sealed class RepairData
    {
        /// <summary>Prix d'une réparation complète, en pour cent de la valeur de l'objet.</summary>
        public int? Percent { get; set; }

        /// <summary>Prix d'une réparation, au moins (cuivre, par pièce entamée).</summary>
        public long? Minimum { get; set; }

        /// <summary>Lieux de la zone où l'on répare soi-même (la forge).</summary>
        public List<string?>? Places { get; set; }

        /// <summary>Marchands qui réparent (l'armurier).</summary>
        public List<string?>? Merchants { get; set; }
    }

    public sealed class ProfessionData
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
    }

    public sealed class StackData
    {
        public string? Item { get; set; }
        public int? Count { get; set; }
    }

    public sealed class RecipeData
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Profession { get; set; }
        public int? RequiredLevel { get; set; }

        /// <summary>Lieu de la zone où l'on fabrique (atelier).</summary>
        public string? Place { get; set; }

        public List<StackData?>? Inputs { get; set; }
        public StackData? Output { get; set; }
        public long? Xp { get; set; }

        /// <summary>Sous-lot V1.4 (version 2) : recette de la forge à qualité (petit jeu des frappes ; produit : un équipement).</summary>
        public bool? Quality { get; set; }
    }

    public sealed class NodeData
    {
        public string? Id { get; set; }
        public string? Profession { get; set; }
        public string? Item { get; set; }
        public int? Count { get; set; }
        public PointData? Position { get; set; }
        public long? RespawnMs { get; set; }
        public long? Xp { get; set; }
    }

    // --- Modèle validé ---------------------------------------------------------------------------------

    public sealed class Profession
    {
        public string Id { get; }
        public string Name { get; }

        internal Profession(string id, string name)
        {
            Id = id;
            Name = name;
        }
    }

    public sealed class Recipe
    {
        public string Id { get; }
        public string Name { get; }
        public string Profession { get; }
        public int RequiredLevel { get; }
        public string Place { get; }
        public IReadOnlyList<(string Item, int Count)> Inputs { get; }
        public (string Item, int Count) Output { get; }
        public long Xp { get; }

        /// <summary>Sous-lot V1.4 : recette à qualité : le petit jeu de forge, puis le tirage de la qualité (normale, supérieure, chef-d'œuvre).</summary>
        public bool Quality { get; }

        internal Recipe(string id, string name, string profession, int requiredLevel, string place, IEnumerable<(string, int)> inputs, (string, int) output, long xp,
            bool quality = false)
        {
            Quality = quality;
            Id = id;
            Name = name;
            Profession = profession;
            RequiredLevel = requiredLevel;
            Place = place;
            Inputs = Frozen.List(inputs, nameof(inputs));
            Output = output;
            Xp = xp;
        }
    }

    public sealed class GatheringNode
    {
        public string Id { get; }
        public string Profession { get; }
        public string Item { get; }
        public int Count { get; }
        public GroundVector Position { get; }
        public long RespawnMs { get; }
        public long Xp { get; }

        internal GatheringNode(string id, string profession, string item, int count, GroundVector position, long respawnMs, long xp)
        {
            Id = id;
            Profession = profession;
            Item = item;
            Count = count;
            Position = position;
            RespawnMs = respawnMs;
            Xp = xp;
        }
    }

    /// <summary>Sous-lot V1.4 : chance d'une qualité de fabrication, en pour cent.</summary>
    public sealed class QualityChance
    {
        public int Base { get; }
        public int PerSkill { get; }
        public int PerLevel { get; }

        internal QualityChance(int @base, int perSkill, int perLevel)
        {
            Base = @base;
            PerSkill = perSkill;
            PerLevel = perLevel;
        }

        /// <summary>
        /// Chance pour une adresse <paramref name="skill"/> (0 à 100 : moyenne des frappes) et <paramref name="levelsAbove"/> niveaux de métier
        /// au-dessus du niveau requis : base + part de l'adresse (arrondie vers le bas) + part des niveaux, de 0 à 100.
        /// </summary>
        public int At(int skill, int levelsAbove)
        {
            long chance = checked(Base + (long)PerSkill * Math.Min(100, Math.Max(0, skill)) / 100 + (long)PerLevel * Math.Max(0, levelsAbove));
            return (int)Math.Min(100, Math.Max(0, chance));
        }
    }

    /// <summary>Sous-lot V1.4 : petit jeu de forge et chances des qualités.</summary>
    public sealed class ForgeRules
    {
        /// <summary>Frappes du petit jeu (trois).</summary>
        public int Strikes { get; }

        public QualityChance Superior { get; }
        public QualityChance Masterwork { get; }

        internal ForgeRules(int strikes, QualityChance superior, QualityChance masterwork)
        {
            Strikes = strikes;
            Superior = superior;
            Masterwork = masterwork;
        }

        /// <summary>
        /// Chances (pour cent) d'un chef-d'œuvre et d'une qualité supérieure pour cette adresse et ces niveaux d'avance ; leur somme est au
        /// plus 100 (le chef-d'œuvre d'abord, la qualité supérieure dans ce qui reste) ; le reste donne la qualité normale.
        /// </summary>
        public (int Masterwork, int Superior) Chances(int skill, int levelsAbove)
        {
            int master = Masterwork.At(skill, levelsAbove);
            return (master, Math.Min(100 - master, Superior.At(skill, levelsAbove)));
        }
    }

    /// <summary>Sous-lot V1.4 : recette au-dessus de son niveau.</summary>
    public sealed class FailureRules
    {
        public int MaxLevels { get; }
        public int PercentPerLevel { get; }
        public int SkillReduction { get; }
        public int RefundPercent { get; }

        /// <summary>
        /// Sous-lot V1.8 (réponse de Damien du 31 octobre 2026 : « une recette ratée donne un quart de l'expérience ») : part de l'expérience
        /// de la recette gagnée en cas d'échec, en pour cent (25 dans les données).
        /// </summary>
        public int XpPercent { get; }

        internal FailureRules(int maxLevels, int percentPerLevel, int skillReduction, int refundPercent, int xpPercent = 0)
        {
            MaxLevels = maxLevels;
            PercentPerLevel = percentPerLevel;
            SkillReduction = skillReduction;
            RefundPercent = refundPercent;
            XpPercent = xpPercent;
        }

        /// <summary>Sous-lot V1.8 : expérience d'un échec pour une recette qui en donne <paramref name="xp"/> (part <see cref="XpPercent"/>, arrondie vers le bas, au moins 1 si la part n'est pas nulle).</summary>
        public long FailedXp(long xp) => XpPercent == 0 || xp <= 0 ? 0 : Math.Max(1, checked(xp * XpPercent) / 100);

        /// <summary>
        /// Chance de rater (pour cent) à <paramref name="gap"/> niveaux sous le niveau requis, pour une adresse <paramref name="skill"/> (0 à 100) :
        /// <c>gap × PercentPerLevel − SkillReduction × skill / 100</c>, de 0 à 100 ; 0 quand le niveau suffit.
        /// </summary>
        public int FailPercent(int gap, int skill)
        {
            if (gap <= 0) return 0;
            long chance = checked((long)gap * PercentPerLevel - (long)SkillReduction * Math.Min(100, Math.Max(0, skill)) / 100);
            return (int)Math.Min(100, Math.Max(0, chance));
        }

        /// <summary>Part rendue de <paramref name="count"/> matériaux en cas d'échec (arrondie vers le bas).</summary>
        public int Refund(int count) => (int)(checked((long)count * RefundPercent) / 100);
    }

    /// <summary>Sous-lot V1.4 : réparation contre de l'or.</summary>
    public sealed class RepairRules
    {
        public int Percent { get; }
        public long Minimum { get; }
        public IReadOnlyList<string> Places { get; }
        public IReadOnlyList<string> Merchants { get; }

        internal RepairRules(int percent, long minimum, IEnumerable<string> places, IEnumerable<string> merchants)
        {
            Percent = percent;
            Minimum = minimum;
            Places = Frozen.List(places, nameof(places));
            Merchants = Frozen.List(merchants, nameof(merchants));
        }

        /// <summary>
        /// Prix (cuivre) de la réparation d'une pièce de valeur <paramref name="value"/> (0 : sans valeur) à qui il manque
        /// <paramref name="missing"/> points de solidité sur <paramref name="max"/> : la part <see cref="Percent"/> de la valeur, au prorata
        /// de ce qui manque, arrondie vers le haut, au moins <see cref="Minimum"/> ; 0 si rien ne manque. Arithmétique vérifiée.
        /// </summary>
        public long Price(long value, int missing, int max)
        {
            if (max < 1) throw new ArgumentOutOfRangeException(nameof(max));
            if (missing <= 0) return 0;
            if (missing > max) missing = max;
            long numerator = checked(Math.Max(0, value) * Percent * missing);
            long denominator = checked(100L * max);
            long price = numerator / denominator + (numerator % denominator == 0 ? 0 : 1);
            return Math.Max(Minimum, price);
        }
    }

    /// <summary>
    /// Sous-lot 12.6 (cadrage H10, plan : « XP de métier séparée ») : métiers, avec leur propre expérience et leurs niveaux ; recettes
    /// (entrées, sortie, niveau de métier, atelier) ; nœuds de récolte de la zone. Valeurs provisoires.
    /// </summary>
    public sealed class CraftingBook
    {
        public int SchemaVersion { get; }
        public string ContentVersion { get; }
        public int MaxLevel { get; }
        public IReadOnlyList<long> XpToLevel { get; }
        public IReadOnlyList<Profession> Professions { get; }
        public IReadOnlyList<Recipe> Recipes { get; }
        public IReadOnlyList<GatheringNode> Nodes { get; }

        /// <summary>Sous-lot V1.4 : petit jeu de forge et chances des qualités.</summary>
        public ForgeRules Forge { get; }

        /// <summary>Sous-lot V1.4 : recette au-dessus de son niveau (échec possible, matériaux rendus en partie).</summary>
        public FailureRules Failure { get; }

        /// <summary>Sous-lot V1.4 : réparation contre de l'or.</summary>
        public RepairRules Repair { get; }

        internal CraftingBook(int schemaVersion, string contentVersion, int maxLevel, IEnumerable<long> xpToLevel, IEnumerable<Profession> professions,
            IEnumerable<Recipe> recipes, IEnumerable<GatheringNode> nodes, ForgeRules? forge = null, FailureRules? failure = null, RepairRules? repair = null)
        {
            Forge = forge ?? new ForgeRules(3, new QualityChance(0, 0, 0), new QualityChance(0, 0, 0));
            Failure = failure ?? new FailureRules(0, 0, 0, 0);
            Repair = repair ?? new RepairRules(0, 0, Array.Empty<string>(), Array.Empty<string>());
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion;
            MaxLevel = maxLevel;
            XpToLevel = Frozen.List(xpToLevel, nameof(xpToLevel));
            Professions = Frozen.List(professions, nameof(professions));
            Recipes = Frozen.List(recipes, nameof(recipes));
            Nodes = Frozen.List(nodes, nameof(nodes));
        }

        /// <summary>Niveau d'un métier pour cette expérience (1 à <see cref="MaxLevel"/>).</summary>
        public int LevelFor(long xp)
        {
            if (xp < 0) throw new ArgumentOutOfRangeException(nameof(xp));
            int level = 1;
            while (level < MaxLevel && xp >= XpToLevel[level]) level++;
            return level;
        }

        public Recipe Recipe(string id) => Recipes.FirstOrDefault(r => r.Id == id) ?? throw new ArgumentException($"Recette « {id} » inconnue.", nameof(id));
        public GatheringNode Node(string id) => Nodes.FirstOrDefault(n => n.Id == id) ?? throw new ArgumentException($"Nœud « {id} » inconnu.", nameof(id));
        public bool HasProfession(string id) => Professions.Any(p => p.Id == id);
    }

    public sealed class CraftingLoadResult
    {
        public ValidationReport Report { get; }
        public CraftingBook? Book { get; }
        public bool IsValid => Book != null;

        internal CraftingLoadResult(ValidationReport report, CraftingBook? book)
        {
            Report = report;
            Book = book;
        }
    }

    /// <summary>
    /// Validateur unique des métiers (<c>kind: "crafting"</c>, sous-lot 12.6), contre les textes, la zone et le catalogue des objets :
    /// expérience par niveau qui part de 0 et croît strictement ; métiers, recettes et nœuds d'identifiants uniques ; objets connus, en
    /// quantité d'au plus une pile ; ateliers parmi les lieux de la zone ; nœuds dans l'aire praticable.
    /// </summary>
    public static class CraftingValidator
    {
        public const string Kind = "crafting";

        /// <summary>
        /// Version 1 (sous-lot 12.6) : <c>maxLevel</c>, <c>xpToLevel</c>, <c>professions</c>, <c>recipes</c>, <c>nodes</c>. Version 2 (sous-lot
        /// V1.4) : <c>quality</c> sur une recette (petit jeu de forge et qualité de fabrication ; produit : un seul équipement qui prend une
        /// qualité), <c>forge</c> (frappes, chances de la qualité supérieure et du chef-d'œuvre), <c>failure</c> (recette au-dessus de son
        /// niveau : niveaux tentés au plus, chance de rater, matériaux rendus) et <c>repair</c> (prix, lieux et marchands de la réparation).
        /// Version 3 (sous-lot V1.8) : <c>failure.xpPercent</c> (part de l'expérience gagnée par une recette ratée). Une version antérieure
        /// est refusée : le fichier se met à jour à la main.
        /// </summary>
        public const int CurrentSchemaVersion = 3;

        /// <summary>Sous-lot V1.4 : frappes du petit jeu, au plus ; niveaux tentés au-dessus du sien, au plus ; marchands et lieux de réparation, au plus.</summary>
        public const int MaxStrikes = 5, MaxOverLevels = 10, MaxRepairSpots = 16;

        /// <summary>Sous-lot V1.4 : prix minimal d'une réparation, au plus (cuivre).</summary>
        public const long MaxRepairMinimum = 1_000_000;

        private static readonly Regex MerchantId = new Regex(@"^merchant(\.[a-z0-9_]+)+\z", RegexOptions.CultureInvariant);

        public const int MaxProfessions = 16, MaxRecipes = 256, MaxNodes = 256, MaxInputs = 8, MaxLevels = 100;
        public const long MaxXp = 1_000_000_000, MaxGainXp = 1_000_000, MinRespawnMs = 1000, MaxRespawnMs = 3_600_000;

        private static readonly Regex LocalId = new Regex(@"^[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant);
        private static readonly Regex RecipeId = new Regex(@"^recipe(\.[a-z0-9_]+)+\z", RegexOptions.CultureInvariant);

        public static bool IsProfessionId(string? id) => id != null && id.Length <= 64 && LocalId.IsMatch(id);

        public static CraftingLoadResult Validate(CraftingDocument document, TextCatalog texts, ZoneDefinition zone, ItemCatalog items, ValidationReport? report = null)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));
            if (texts is null) throw new ArgumentNullException(nameof(texts));
            if (zone is null) throw new ArgumentNullException(nameof(zone));
            if (items is null) throw new ArgumentNullException(nameof(items));
            ValidationReport r = report ?? new ValidationReport();
            if (!HeaderValidator.Validate(document.SchemaVersion, document.Kind, document.ContentVersion, Kind, CurrentSchemaVersion, r))
                return new CraftingLoadResult(r, null);
            CraftingData? d = document.Data;
            if (d is null) { Missing(r, "$.data", "métiers, recettes, nœuds"); return new CraftingLoadResult(r, null); }

            int max = 0;
            if (d.MaxLevel is null) Missing(r, "$.data.maxLevel", "niveau maximal d'un métier");
            else if (d.MaxLevel < 1 || d.MaxLevel > MaxLevels) r.Add("$.data.maxLevel", ValidationCode.OutOfRange, $"niveau maximal hors de [1, {MaxLevels}].");
            else max = d.MaxLevel.Value;
            if (d.XpToLevel is null) Missing(r, "$.data.xpToLevel", "expérience cumulée de chaque niveau");
            else if (d.XpToLevel.Count > MaxLevels) r.Add("$.data.xpToLevel", ValidationCode.LimitExceeded, $"au plus {MaxLevels} niveaux.");
            else
            {
                if (max > 0 && d.XpToLevel.Count != max) r.Add("$.data.xpToLevel", ValidationCode.InvalidValue, $"{d.XpToLevel.Count} niveaux, {max} attendus.");
                for (int i = 0; i < d.XpToLevel.Count; i++)
                {
                    string p = $"$.data.xpToLevel[{i}]";
                    if (d.XpToLevel[i] is not long xp) { Missing(r, p, "expérience cumulée"); continue; }
                    if (i == 0 && xp != 0) r.Add(p, ValidationCode.InvalidValue, "le niveau 1 demande 0 expérience.");
                    else if (xp < 0 || xp > MaxXp) r.Add(p, ValidationCode.OutOfRange, $"expérience hors de [0, {MaxXp}].");
                    else if (i > 0 && d.XpToLevel[i - 1] is long prev && xp <= prev) r.Add(p, ValidationCode.InvalidValue, "l'expérience cumulée doit croître strictement.");
                }
            }

            // Même règle que la revue Codex de la PR #44 : bornes et doublons contrôlés aussi pour un document en objets publics ; un
            // identifiant déjà donné est en double même si sa première description est invalide.
            var professions = new List<Profession>();
            var professionIds = new HashSet<string>(StringComparer.Ordinal);
            if (d.Professions is null) Missing(r, "$.data.professions", "métiers");
            else if (d.Professions.Count > MaxProfessions) r.Add("$.data.professions", ValidationCode.LimitExceeded, $"au plus {MaxProfessions} métiers.");
            else
                for (int i = 0; i < d.Professions.Count; i++)
                {
                    string p = $"$.data.professions[{i}]";
                    if (d.Professions[i] is not ProfessionData x) { Missing(r, p, "métier"); continue; }
                    bool ok = true;
                    if (!IsProfessionId(x.Id)) { r.Add(p + ".id", ValidationCode.InvalidValue, $"identifiant « {x.Id} » mal formé."); ok = false; }
                    else if (!professionIds.Add(x.Id!)) { r.Add(p + ".id", ValidationCode.DuplicateId, $"métier « {x.Id} » déjà décrit."); ok = false; }
                    ok &= Text(texts, x.Name, p + ".name", r);
                    if (ok) professions.Add(new Profession(x.Id!, x.Name!));
                }

            bool Known(string? profession, string path)
            {
                if (profession != null && professions.Any(q => q.Id == profession)) return true;
                r.Add(path, ValidationCode.UnknownReference, $"métier inconnu « {profession} ».");
                return false;
            }

            (string, int)? Stack(StackData? s, string path)
            {
                if (s is null) { Missing(r, path, "objet et quantité"); return null; }
                if (s.Item is null || !items.TryGetItem(s.Item, out ItemDefinition item)) { r.Add(path + ".item", ValidationCode.UnknownReference, $"objet inconnu « {s.Item} »."); return null; }
                if (s.Count is null || s.Count < 1 || s.Count > item.Stack) { r.Add(path + ".count", ValidationCode.OutOfRange, $"quantité hors de [1, {item.Stack}] (une pile)."); return null; }
                return (s.Item, s.Count.Value);
            }

            var recipes = new List<Recipe>();
            var recipeIds = new HashSet<string>(StringComparer.Ordinal);
            if (d.Recipes is null) Missing(r, "$.data.recipes", "recettes");
            else
            {
                if (d.Recipes.Count > MaxRecipes) r.Add("$.data.recipes", ValidationCode.LimitExceeded, $"au plus {MaxRecipes} recettes.");
                for (int i = 0; i < d.Recipes.Count; i++)
                {
                    string p = $"$.data.recipes[{i}]";
                    if (d.Recipes[i] is not RecipeData x) { Missing(r, p, "recette"); continue; }
                    bool ok = true;
                    if (x.Id is null || x.Id.Length > 200 || !RecipeId.IsMatch(x.Id)) { r.Add(p + ".id", ValidationCode.InvalidValue, $"identifiant « {x.Id} » mal formé : « recipe.… »."); ok = false; }
                    else if (!recipeIds.Add(x.Id)) { r.Add(p + ".id", ValidationCode.DuplicateId, $"recette « {x.Id} » déjà décrite."); ok = false; }
                    ok &= Text(texts, x.Name, p + ".name", r);
                    ok &= Known(x.Profession, p + ".profession");
                    if (x.RequiredLevel is null || x.RequiredLevel < 1 || (max > 0 && x.RequiredLevel > max))
                    { r.Add(p + ".requiredLevel", ValidationCode.OutOfRange, $"niveau de métier requis hors de [1, {max}]."); ok = false; }
                    if (x.Place is null || zone.Places.All(q => q.Id != x.Place)) { r.Add(p + ".place", ValidationCode.UnknownReference, $"lieu inconnu « {x.Place} »."); ok = false; }
                    var inputs = new List<(string, int)>();
                    if (x.Inputs is null || x.Inputs.Count < 1 || x.Inputs.Count > MaxInputs) { r.Add(p + ".inputs", ValidationCode.OutOfRange, $"de 1 à {MaxInputs} entrées."); ok = false; }
                    else
                        for (int k = 0; k < x.Inputs.Count; k++)
                        {
                            if (Stack(x.Inputs[k], $"{p}.inputs[{k}]") is { } st)
                            {
                                if (inputs.Any(q => q.Item1 == st.Item1)) { r.Add($"{p}.inputs[{k}].item", ValidationCode.DuplicateId, "objet déjà en entrée."); ok = false; }
                                else inputs.Add(st);
                            }
                            else ok = false;
                        }
                    (string, int)? output = Stack(x.Output, p + ".output");
                    if (output is null) ok = false;
                    if (x.Xp is null || x.Xp < 1 || x.Xp > MaxGainXp) { r.Add(p + ".xp", ValidationCode.OutOfRange, $"expérience de métier hors de [1, {MaxGainXp}]."); ok = false; }
                    // Sous-lot V1.4 : une recette à qualité donne un seul équipement qui prend une qualité de fabrication.
                    bool quality = x.Quality ?? false;
                    if (quality && output is { } o && (o.Item2 != 1 || !items.TakesQuality(items.Item(o.Item1))))
                    {
                        r.Add(p + ".quality", ValidationCode.InvalidValue, $"« {o.Item1} » ×{o.Item2} : une recette à qualité donne un seul équipement des places du combat, non nommé.");
                        ok = false;
                    }
                    if (ok) recipes.Add(new Recipe(x.Id!, x.Name!, x.Profession!, x.RequiredLevel!.Value, x.Place!, inputs, output!.Value, x.Xp!.Value, quality));
                }
            }

            var nodes = new List<GatheringNode>();
            var nodeIds = new HashSet<string>(StringComparer.Ordinal);
            long tickMs = zone.Setup.Combat.TickMilliseconds;
            CombatSpace space = zone.Setup.Space!;
            if (d.Nodes is null) Missing(r, "$.data.nodes", "nœuds de récolte");
            else
            {
                if (d.Nodes.Count > MaxNodes) r.Add("$.data.nodes", ValidationCode.LimitExceeded, $"au plus {MaxNodes} nœuds.");
                for (int i = 0; i < d.Nodes.Count; i++)
                {
                    string p = $"$.data.nodes[{i}]";
                    if (d.Nodes[i] is not NodeData x) { Missing(r, p, "nœud"); continue; }
                    bool ok = true;
                    if (x.Id is null || x.Id.Length > 64 || !LocalId.IsMatch(x.Id)) { r.Add(p + ".id", ValidationCode.InvalidValue, $"identifiant « {x.Id} » mal formé."); ok = false; }
                    else if (!nodeIds.Add(x.Id)) { r.Add(p + ".id", ValidationCode.DuplicateId, $"nœud « {x.Id} » déjà décrit."); ok = false; }
                    ok &= Known(x.Profession, p + ".profession");
                    (string, int)? st = Stack(new StackData { Item = x.Item, Count = x.Count }, p);
                    if (st is null) ok = false;
                    if (x.Position is null || x.Position.X is null || x.Position.Z is null) { Missing(r, p + ".position", "[x, z]"); ok = false; }
                    else
                    {
                        var pos = new GroundVector(x.Position.X!.Value, x.Position.Z!.Value);
                        if (pos.X < space.Min.X || pos.X > space.Max.X || pos.Z < space.Min.Z || pos.Z > space.Max.Z)
                        { r.Add(p + ".position", ValidationCode.OutOfRange, "nœud hors de l'aire praticable de la zone."); ok = false; }
                    }
                    if (x.RespawnMs is null || x.RespawnMs < MinRespawnMs || x.RespawnMs > MaxRespawnMs || x.RespawnMs % tickMs != 0)
                    { r.Add(p + ".respawnMs", ValidationCode.OutOfRange, $"repousse hors de [{MinRespawnMs}, {MaxRespawnMs}] ms (multiple du tick, {tickMs} ms)."); ok = false; }
                    if (x.Xp is null || x.Xp < 1 || x.Xp > MaxGainXp) { r.Add(p + ".xp", ValidationCode.OutOfRange, $"expérience de métier hors de [1, {MaxGainXp}]."); ok = false; }
                    if (ok) nodes.Add(new GatheringNode(x.Id!, x.Profession!, st!.Value.Item1, st.Value.Item2,
                        new GroundVector(x.Position!.X!.Value, x.Position.Z!.Value), x.RespawnMs!.Value, x.Xp!.Value));
                }
            }
            ForgeRules? forge = CheckForge(d.Forge, r);
            FailureRules? failure = CheckFailure(d.Failure, max, r);
            RepairRules? repair = CheckRepair(d.Repair, zone, r);
            if (!r.IsValid || document.ContentVersion is null || max == 0) return new CraftingLoadResult(r, null);
            return new CraftingLoadResult(r, new CraftingBook(document.SchemaVersion!.Value, document.ContentVersion, max, d.XpToLevel!.Select(x => x!.Value),
                professions, recipes, nodes, forge, failure, repair));
        }

        private static int? Percent(int? value, string path, string what, ValidationReport r)
        {
            if (value is null) { Missing(r, path, what); return null; }
            if (value < 0 || value > 100) { r.Add(path, ValidationCode.OutOfRange, "pour cent hors de [0, 100]."); return null; }
            return value;
        }

        private static QualityChance? CheckChance(ChanceData? c, string path, ValidationReport r)
        {
            if (c is null) { Missing(r, path, "chance : base, perSkill, perLevel (pour cent)"); return null; }
            int? b = Percent(c.Base, path + ".base", "chance de base", r);
            int? s = Percent(c.PerSkill, path + ".perSkill", "chance ajoutée par une adresse parfaite", r);
            int? l = Percent(c.PerLevel, path + ".perLevel", "chance ajoutée par niveau au-dessus du requis", r);
            return b is int bb && s is int ss && l is int ll ? new QualityChance(bb, ss, ll) : null;
        }

        /// <summary>Sous-lot V1.4 : petit jeu de forge (de 1 à <see cref="MaxStrikes"/> frappes) et chances des qualités (pour cent).</summary>
        private static ForgeRules? CheckForge(ForgeData? f, ValidationReport r)
        {
            const string path = "$.data.forge";
            if (f is null) { Missing(r, path, "petit jeu de forge : strikes, superior, masterwork"); return null; }
            bool ok = true;
            if (f.Strikes is null) { Missing(r, path + ".strikes", "nombre de frappes"); ok = false; }
            else if (f.Strikes < 1 || f.Strikes > MaxStrikes) { r.Add(path + ".strikes", ValidationCode.OutOfRange, $"frappes hors de [1, {MaxStrikes}]."); ok = false; }
            QualityChance? superior = CheckChance(f.Superior, path + ".superior", r);
            QualityChance? masterwork = CheckChance(f.Masterwork, path + ".masterwork", r);
            return ok && superior != null && masterwork != null ? new ForgeRules(f.Strikes!.Value, superior, masterwork) : null;
        }

        /// <summary>Sous-lot V1.4 : recette au-dessus de son niveau (de 0 à <see cref="MaxOverLevels"/> niveaux, sous le niveau maximal).</summary>
        private static FailureRules? CheckFailure(FailureData? f, int max, ValidationReport r)
        {
            const string path = "$.data.failure";
            if (f is null) { Missing(r, path, "recette au-dessus de son niveau : maxLevels, percentPerLevel, skillReduction, refundPercent, xpPercent"); return null; }
            bool ok = true;
            if (f.MaxLevels is null) { Missing(r, path + ".maxLevels", "niveaux tentés au-dessus du sien, au plus"); ok = false; }
            else if (f.MaxLevels < 0 || f.MaxLevels > MaxOverLevels || (max > 0 && f.MaxLevels >= max))
            { r.Add(path + ".maxLevels", ValidationCode.OutOfRange, $"niveaux hors de [0, {Math.Min(MaxOverLevels, Math.Max(0, max - 1))}]."); ok = false; }
            int? per = Percent(f.PercentPerLevel, path + ".percentPerLevel", "chance de rater par niveau manquant", r);
            int? skill = Percent(f.SkillReduction, path + ".skillReduction", "chance retirée par une adresse parfaite", r);
            int? refund = Percent(f.RefundPercent, path + ".refundPercent", "part des matériaux rendue", r);
            int? xp = Percent(f.XpPercent, path + ".xpPercent", "part de l'expérience gagnée par un échec", r);   // sous-lot V1.8 (version 3)
            return ok && per is int p && skill is int s && refund is int d && xp is int x ? new FailureRules(f.MaxLevels!.Value, p, s, d, x) : null;
        }

        /// <summary>Sous-lot V1.4 : réparation (part de la valeur, prix minimal, lieux de la zone, marchands « merchant.… » ; au moins un lieu ou un marchand).</summary>
        private static RepairRules? CheckRepair(RepairData? d, ZoneDefinition zone, ValidationReport r)
        {
            const string path = "$.data.repair";
            if (d is null) { Missing(r, path, "réparation : percent, minimum, places, merchants"); return null; }
            bool ok = true;
            int? percent = Percent(d.Percent, path + ".percent", "prix d'une réparation complète, en pour cent de la valeur", r);
            if (percent is null) ok = false;
            if (d.Minimum is null) { Missing(r, path + ".minimum", "prix minimal (cuivre)"); ok = false; }
            else if (d.Minimum < 0 || d.Minimum > MaxRepairMinimum) { r.Add(path + ".minimum", ValidationCode.OutOfRange, $"prix hors de [0, {MaxRepairMinimum}] cuivre."); ok = false; }
            List<string> Ids(List<string?>? list, string field, Func<string, bool> known, string what)
            {
                var ids = new List<string>();
                if (list is null) { Missing(r, $"{path}.{field}", what + ", éventuellement aucun"); ok = false; return ids; }
                if (list.Count > MaxRepairSpots) { r.Add($"{path}.{field}", ValidationCode.LimitExceeded, $"au plus {MaxRepairSpots}."); ok = false; return ids; }
                for (int i = 0; i < list.Count; i++)
                {
                    string p = $"{path}.{field}[{i}]";
                    if (list[i] is not string id || !known(id)) { r.Add(p, ValidationCode.UnknownReference, $"{what} inconnu « {list[i]} »."); ok = false; }
                    else if (ids.Contains(id)) { r.Add(p, ValidationCode.DuplicateId, $"« {id} » déjà donné."); ok = false; }
                    else ids.Add(id);
                }
                return ids;
            }
            List<string> places = Ids(d.Places, "places", id => zone.Places.Any(q => q.Id == id), "lieu");
            // Les marchands sont vérifiés contre les marchands chargés à l'ouverture de la partie (WorldSession), comme leurs PNJ.
            List<string> merchants = Ids(d.Merchants, "merchants", id => id.Length <= 200 && MerchantId.IsMatch(id), "marchand");
            if (ok && places.Count == 0 && merchants.Count == 0) { r.Add(path, ValidationCode.InvalidValue, "au moins un lieu ou un marchand de réparation."); ok = false; }
            return ok ? new RepairRules(percent!.Value, d.Minimum!.Value, places, merchants) : null;
        }

        private static bool Text(TextCatalog texts, string? key, string path, ValidationReport r)
        {
            if (key != null && texts.Has(key)) return true;
            r.Add(path, ValidationCode.UnknownReference, $"texte « {key} » absent des textes.");
            return false;
        }

        private static void Missing(ValidationReport report, string path, string what)
        {
            if (!report.HasErrorAtOrAbove(path)) report.Add(path, ValidationCode.MissingField, $"champ obligatoire absent ({what}).");
        }
    }
}
