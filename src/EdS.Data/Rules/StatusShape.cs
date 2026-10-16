using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EdS.Data.Classes;
using EdS.Data.Validation;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;

namespace EdS.Data.Rules
{
    /// <summary>Définition brute d'une altération (bloc <c>statuses</c> de <c>rules.json</c>), avant validation.</summary>
    public sealed class StatusData
    {
        public string? Id { get; set; }
        public string? Category { get; set; }
        public long? DurationMs { get; set; }
        public long? PeriodMs { get; set; }
        public string? Stacking { get; set; }
        public string? Resistance { get; set; }
        public string? DamageType { get; set; }
        public List<string?>? Blocks { get; set; }
        public List<string?>? Interrupts { get; set; }
        public string? BlockReason { get; set; }
        public Fixed? DamageTakenIncrease { get; set; }
        public Fixed? MovementSpeedModifier { get; set; }
        public string? GrantsOnExpiry { get; set; }
        public List<string?>? ImmuneTo { get; set; }

        /// <summary>Déclencheurs portés tant qu'une instance est active (schéma 5, lot 7.3) ; facultatifs.</summary>
        public List<Triggers.TriggerData?>? Triggers { get; set; }
    }

    /// <summary>
    /// Forme du bloc <c>statuses</c> (lot 7.2) : une liste ordonnée de définitions. Champs obligatoires : <c>id</c>,
    /// <c>category</c>, <c>durationMs</c>, <c>stacking</c>, <c>resistance</c> ; facultatifs : <c>periodMs</c> et
    /// <c>damageType</c> (DOT), <c>blocks</c>, <c>interrupts</c>, <c>blockReason</c>, <c>damageTakenIncrease</c>,
    /// <c>movementSpeedModifier</c>, <c>grantsOnExpiry</c>, <c>immuneTo</c>. La cohérence est vérifiée par
    /// <see cref="StatusCatalog.Validate"/>, seule validation de gameplay.
    /// </summary>
    public static class StatusShape
    {
        public const string Root = "$.data.statuses";

        public static IReadOnlyList<string> FieldNames { get; } = Array.AsReadOnly(new[]
        {
            "id", "category", "durationMs", "periodMs", "stacking", "resistance", "damageType", "blocks", "interrupts",
            "blockReason", "damageTakenIncrease", "movementSpeedModifier", "grantsOnExpiry", "immuneTo", "triggers",
        });

        public static void Check(List<StatusData?>? statuses, ValidationReport report)
        {
            bool Present(object? value, string path, string description)
            {
                if (value != null) return true;
                if (!report.HasErrorAtOrAbove(path)) report.Add(path, ValidationCode.MissingField, $"champ obligatoire absent ({description}).");
                return false;
            }
            void Token<T>(string? value, string path, string description, IReadOnlyDictionary<string, T> allowed, bool required = true)
            {
                if (value is null)
                {
                    if (required) Present(null, path, description);
                    return;
                }
                if (!allowed.ContainsKey(value))
                    report.Add(path, ValidationCode.UnknownReference, $"valeur inconnue « {value} » ; valeurs possibles : {string.Join(", ", allowed.Keys)}.");
            }
            void Tokens<T>(List<string?>? values, string path, IReadOnlyDictionary<string, T> allowed)
            {
                if (values is null) return;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (int k = 0; k < values.Count; k++)
                {
                    string itemPath = $"{path}[{k}]";
                    if (values[k] is not { } v)
                    {
                        if (!report.HasErrorAtOrAbove(itemPath)) report.Add(itemPath, ValidationCode.WrongType, "identifiant attendu.");
                    }
                    else if (allowed.Count > 0 && !allowed.ContainsKey(v))
                        report.Add(itemPath, ValidationCode.UnknownReference, $"valeur inconnue « {v} » ; valeurs possibles : {string.Join(", ", allowed.Keys)}.");
                    else if (!seen.Add(v))
                        report.Add(itemPath, ValidationCode.DuplicateId, $"« {v} » déjà cité.");
                }
            }

            if (!Present(statuses, Root, "liste des définitions d'altérations")) return;
            for (int i = 0; i < statuses!.Count; i++)
            {
                string p = $"{Root}[{i}]";
                if (statuses[i] is not { } s)
                {
                    if (!report.HasErrorAtOrAbove(p)) report.Add(p, ValidationCode.WrongType, "objet attendu.");
                    continue;
                }
                Present(s.Id, p + ".id", "identifiant de l'altération");
                Token(s.Category, p + ".category", string.Join(", ", StatusIds.Categories.Keys), StatusIds.Categories);
                Present(s.DurationMs, p + ".durationMs", "durée en millisecondes");
                Token(s.Stacking, p + ".stacking", string.Join(", ", StatusIds.Stackings.Keys), StatusIds.Stackings);
                Token(s.Resistance, p + ".resistance", string.Join(", ", StatusIds.Resistances.Keys), StatusIds.Resistances);
                Token(s.DamageType, p + ".damageType", "physique ou magique", ClassValidator.DamageTypes, required: false);
                Tokens(s.Blocks, p + ".blocks", StatusIds.Blocks);
                Tokens(s.Interrupts, p + ".interrupts", StatusIds.Interrupts);
                Tokens(s.ImmuneTo, p + ".immuneTo", new Dictionary<string, int>());
                Triggers.TriggerShape.Check(s.Triggers, p + ".triggers", report);
            }
        }

        /// <summary>Catalogue du domaine ; suppose <see cref="Check"/> sans erreur. Les champs facultatifs absents prennent leur valeur neutre.</summary>
        public static StatusCatalog Build(List<StatusData?> statuses) =>
            new StatusCatalog(statuses.Select(s => s!).Select(s => new StatusDefinition(
                s.Id!, StatusIds.Categories[s.Category!], s.DurationMs!.Value, StatusIds.Stackings[s.Stacking!], StatusIds.Resistances[s.Resistance!],
                s.PeriodMs ?? 0, s.DamageType is null ? null : ClassValidator.DamageTypes[s.DamageType],
                (s.Blocks ?? new List<string?>()).Aggregate(StatusBlocks.None, (acc, b) => acc | StatusIds.Blocks[b!]),
                (s.Interrupts ?? new List<string?>()).Aggregate(StatusInterrupts.None, (acc, b) => acc | StatusIds.Interrupts[b!]),
                s.BlockReason, s.DamageTakenIncrease ?? Fixed.Zero, s.MovementSpeedModifier ?? Fixed.Zero, s.GrantsOnExpiry,
                (s.ImmuneTo ?? new List<string?>()).Select(x => x!), Triggers.TriggerShape.Build(s.Triggers))).ToArray());

        /// <summary>
        /// Écriture canonique pour l'empreinte des règles : dans l'ordre du fichier (il fixe la priorité des motifs de refus), chaque
        /// champ, les listes de drapeaux dans l'ordre de déclaration. Correctif 7.3.1 (A73-M02) : un champ facultatif absent s'écrit comme
        /// absent (voir <see cref="CanonicalWriter"/>) ; <c>grantsOnExpiry</c> absent et l'altération <c>none</c> ne se confondent plus.
        /// </summary>
        public static void AppendCanonical(StringBuilder text, StatusCatalog catalog)
        {
            var w = new CanonicalWriter(text);
            w.Value("statuses.count", catalog.Definitions.Count);
            for (int i = 0; i < catalog.Definitions.Count; i++)
            {
                StatusDefinition d = catalog.Definitions[i];
                string p = $"statuses[{i}]";
                w.Value(p + ".id", d.Id);
                w.Value(p + ".category", StatusIds.Of(d.Category));
                w.Value(p + ".durationMs", d.DurationMs);
                w.Value(p + ".periodMs", d.PeriodMs);
                w.Value(p + ".stacking", StatusIds.Of(d.Stacking));
                w.Value(p + ".resistance", StatusIds.Of(d.Resistance));
                w.Optional(p + ".damageType", d.DamageType is { } type ? RulesCatalog.Id(type) : null);
                w.List(p + ".blocks", StatusIds.Of(d.Blocks));
                w.List(p + ".interrupts", StatusIds.Of(d.Interrupts));
                w.Optional(p + ".blockReason", d.BlockReason);
                w.Value(p + ".damageTakenIncrease", d.DamageTakenIncrease.ToString());
                w.Value(p + ".movementSpeedModifier", d.MovementSpeedModifier.ToString());
                w.Optional(p + ".grantsOnExpiry", d.GrantsOnExpiry);
                w.List(p + ".immuneTo", d.ImmuneTo);
                Triggers.TriggerShape.AppendCanonical(text, p + ".triggers", d.Triggers);
            }
        }
    }
}
