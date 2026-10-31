using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EdS.Data.Triggers;
using EdS.Data.Validation;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;

namespace EdS.Data.Modifiers
{
    /// <summary>
    /// Modificateur brut (sous-lot 8.1, schéma de classe 5), sur un talent ou une compétence passive :
    /// <c>{ "key": "guard.damageReduction", "add": { "base": 0.04, "perRank": 0.04 }, "conditions": ["whileGuarding"] }</c>.
    /// </summary>
    public sealed class ModifierData
    {
        public string? Key { get; set; }
        public RankedData? Add { get; set; }

        /// <summary>Conditions (X2) ; facultatives, null si absentes (modificateur permanent).</summary>
        public List<string?>? Conditions { get; set; }
    }

    /// <summary>
    /// Forme des modificateurs : présence des champs obligatoires et identifiants des conditions. Toute la cohérence de gameplay
    /// (clé connue, doublons, bornes, références) est vérifiée par <see cref="ModifierDefinition.ValidateList"/> et
    /// <see cref="ModifierDefinition.ValidateReferences"/>, seule validation de gameplay.
    /// </summary>
    public static class ModifierShape
    {
        /// <summary>
        /// Vérifie une liste facultative (null = absente, aucun modificateur). Sous-lot V1.3 : la condition <c>targetStunned</c> n'est admise
        /// que pour l'équipement (<paramref name="equipment"/>) : les classes (schéma 9) et les règles ne la connaissent pas, leur forme ne
        /// change pas.
        /// </summary>
        public static void Check(List<ModifierData?>? modifiers, string path, ValidationReport report, bool equipment = false)
        {
            if (modifiers is null) return;
            bool Present(object? value, string p, string description)
            {
                if (value != null) return true;
                if (!report.HasErrorAtOrAbove(p)) report.Add(p, ValidationCode.MissingField, $"champ obligatoire absent ({description}).");
                return false;
            }
            for (int i = 0; i < modifiers.Count; i++)
            {
                string p = $"{path}[{i}]";
                if (modifiers[i] is not { } m)
                {
                    if (!report.HasErrorAtOrAbove(p)) report.Add(p, ValidationCode.WrongType, "objet attendu.");
                    continue;
                }
                Present(m.Key, p + ".key", "clé de la liste fermée");
                if (Present(m.Add, p + ".add", "nombre décimal ou { base, perRank }")) Present(m.Add!.Base, p + ".add.base", "nombre décimal");
                if (m.Conditions is null) continue;
                for (int k = 0; k < m.Conditions.Count; k++)
                {
                    string cp = $"{p}.conditions[{k}]";
                    if (m.Conditions[k] is not { } c)
                    {
                        if (!report.HasErrorAtOrAbove(cp)) report.Add(cp, ValidationCode.WrongType, "identifiant attendu.");
                    }
                    else if (!ModifierKeys.Conditions.TryGetValue(c, out ModifierCondition condition) || (!equipment && condition == ModifierCondition.TargetStunned))
                        report.Add(cp, ValidationCode.UnknownReference, $"valeur inconnue « {c} » ; valeurs possibles : " +
                            $"{string.Join(", ", ModifierKeys.Conditions.Where(k => equipment || k.Value != ModifierCondition.TargetStunned).Select(k => k.Key))}.");
                }
            }
        }

        /// <summary>Conversion pour le domaine ; suppose <see cref="Check"/> sans erreur. Liste absente : aucun modificateur.</summary>
        public static IReadOnlyList<ModifierDefinition> Build(List<ModifierData?>? modifiers) =>
            modifiers is null ? Array.Empty<ModifierDefinition>() : modifiers.Select(m => new ModifierDefinition(m!.Key!,
                new RankedValue(m.Add!.Base!.Value, m.Add.PerRank ?? Fixed.Zero), m.Conditions?.Select(c => ModifierKeys.Conditions[c!]))).ToArray();

        /// <summary>
        /// Écriture canonique pour les empreintes : rien si la liste est absente ou vide (les données sans modificateur n'ajoutent rien),
        /// sinon chaque champ dans un ordre fixe ; une liste de conditions absente et une liste vide s'écrivent de même (même sens), et les
        /// conditions, un ensemble pour le domaine, s'écrivent dans l'ordre fixe de l'énumération quel que soit l'ordre déclaré (données
        /// validées : ni condition inconnue ni doublon). L'ordre des modificateurs d'une liste fait partie du contenu, comme celui des
        /// déclencheurs.
        /// </summary>
        public static void AppendCanonical(StringBuilder text, string path, IReadOnlyList<ModifierDefinition> modifiers)
        {
            if (modifiers.Count == 0) return;
            var w = new CanonicalWriter(text);
            w.Value(path + ".count", modifiers.Count);
            for (int i = 0; i < modifiers.Count; i++)
            {
                ModifierDefinition m = modifiers[i];
                string p = $"{path}[{i}]";
                w.Value(p + ".key", m.Key);
                w.Value(p + ".add", m.Add.PerRank == Fixed.Zero ? m.Add.Base.ToString() : $"{m.Add.Base}+{m.Add.PerRank}/rank");
                w.List(p + ".conditions", ModifierKeys.Of(m.ConditionSet).Select(ModifierKeys.Of));
            }
        }
    }
}
