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

        internal Recipe(string id, string name, string profession, int requiredLevel, string place, IEnumerable<(string, int)> inputs, (string, int) output, long xp)
        {
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

        internal CraftingBook(int schemaVersion, string contentVersion, int maxLevel, IEnumerable<long> xpToLevel, IEnumerable<Profession> professions,
            IEnumerable<Recipe> recipes, IEnumerable<GatheringNode> nodes)
        {
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

        /// <summary>Version 1 (sous-lot 12.6) : <c>maxLevel</c>, <c>xpToLevel</c>, <c>professions</c>, <c>recipes</c>, <c>nodes</c>.</summary>
        public const int CurrentSchemaVersion = 1;

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

            var professions = new List<Profession>();
            if (d.Professions is null) Missing(r, "$.data.professions", "métiers");
            else
                for (int i = 0; i < d.Professions.Count; i++)
                {
                    string p = $"$.data.professions[{i}]";
                    if (d.Professions[i] is not ProfessionData x) { Missing(r, p, "métier"); continue; }
                    bool ok = true;
                    if (!IsProfessionId(x.Id)) { r.Add(p + ".id", ValidationCode.InvalidValue, $"identifiant « {x.Id} » mal formé."); ok = false; }
                    else if (professions.Any(q => q.Id == x.Id)) { r.Add(p + ".id", ValidationCode.DuplicateId, $"métier « {x.Id} » déjà décrit."); ok = false; }
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
                    else if (recipes.Any(q => q.Id == x.Id)) { r.Add(p + ".id", ValidationCode.DuplicateId, $"recette « {x.Id} » déjà décrite."); ok = false; }
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
                    if (ok) recipes.Add(new Recipe(x.Id!, x.Name!, x.Profession!, x.RequiredLevel!.Value, x.Place!, inputs, output!.Value, x.Xp!.Value));
                }
            }

            var nodes = new List<GatheringNode>();
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
                    else if (nodes.Any(q => q.Id == x.Id)) { r.Add(p + ".id", ValidationCode.DuplicateId, $"nœud « {x.Id} » déjà décrit."); ok = false; }
                    ok &= Known(x.Profession, p + ".profession");
                    (string, int)? st = Stack(new StackData { Item = x.Item, Count = x.Count }, p);
                    if (st is null) ok = false;
                    if (x.Position is null) { Missing(r, p + ".position", "[x, z]"); ok = false; }
                    else
                    {
                        var pos = new GroundVector(x.Position.X!.Value, x.Position.Z!.Value);
                        if (pos.X < space.Min.X || pos.X > space.Max.X || pos.Z < space.Min.Z || pos.Z > space.Max.Z)
                        { r.Add(p + ".position", ValidationCode.OutOfRange, "nœud hors de l'aire praticable de la zone."); ok = false; }
                    }
                    if (x.RespawnMs is null || x.RespawnMs < MinRespawnMs || x.RespawnMs > MaxRespawnMs || x.RespawnMs % 50 != 0)
                    { r.Add(p + ".respawnMs", ValidationCode.OutOfRange, $"repousse hors de [{MinRespawnMs}, {MaxRespawnMs}] ms (multiple de 50)."); ok = false; }
                    if (x.Xp is null || x.Xp < 1 || x.Xp > MaxGainXp) { r.Add(p + ".xp", ValidationCode.OutOfRange, $"expérience de métier hors de [1, {MaxGainXp}]."); ok = false; }
                    if (ok) nodes.Add(new GatheringNode(x.Id!, x.Profession!, st!.Value.Item1, st.Value.Item2,
                        new GroundVector(x.Position!.X!.Value, x.Position.Z!.Value), x.RespawnMs!.Value, x.Xp!.Value));
                }
            }
            if (!r.IsValid || document.ContentVersion is null || max == 0) return new CraftingLoadResult(r, null);
            return new CraftingLoadResult(r, new CraftingBook(document.SchemaVersion!.Value, document.ContentVersion, max, d.XpToLevel!.Select(x => x!.Value),
                professions, recipes, nodes));
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
