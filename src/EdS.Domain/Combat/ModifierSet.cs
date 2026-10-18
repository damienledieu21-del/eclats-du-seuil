using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Collections;
using EdS.Domain.Numerics;

namespace EdS.Domain.Combat
{
    /// <summary>Somme des modificateurs conditionnels (X2) de même clé et de mêmes conditions.</summary>
    public readonly struct ConditionalModifier
    {
        public ModifierField Field { get; }
        public ModifierConditions Conditions { get; }
        public Fixed Value { get; }

        public ConditionalModifier(ModifierField field, ModifierConditions conditions, Fixed value)
        {
            Field = field;
            Conditions = conditions;
            Value = value;
        }
    }

    /// <summary>
    /// Sous-lot 8.1 : modificateurs actifs d'un combattant, agrégés (X1, X2). Chaque clé reçoit la somme exacte des valeurs de ses
    /// sources ; les conditionnels sont sommés par (clé, ensemble de conditions). Bornes de la validation : |valeur| ≤ 8,64·10⁷ (brut
    /// 8,64·10¹¹) et au plus (256 talents + 256 compétences) × 8 = 4 096 modificateurs par combattant, donc |somme| &lt; 3,6·10¹⁵ &lt; 2⁶³ :
    /// l'addition vérifiée ne déborde jamais, elle est associative et commutative, et l'ordre des sources est sans effet. Immuable.
    /// </summary>
    public sealed class ModifierSet
    {
        public static readonly ModifierSet Empty = new ModifierSet(new Dictionary<ModifierKey, Fixed>(), Array.Empty<ConditionalModifier>());

        private readonly IReadOnlyDictionary<ModifierKey, Fixed> _sums;

        /// <summary>Conditionnels, triés par clé puis par ensemble de conditions (ordre fixe, journal et évaluation).</summary>
        public IReadOnlyList<ConditionalModifier> Conditional { get; }

        /// <summary>Clés permanentes ciblées par au moins un modificateur actif, en ordre ordinal du texte de la clé.</summary>
        public IReadOnlyList<ModifierKey> Keys { get; }

        private ModifierSet(Dictionary<ModifierKey, Fixed> sums, IEnumerable<ConditionalModifier> conditional)
        {
            _sums = new System.Collections.ObjectModel.ReadOnlyDictionary<ModifierKey, Fixed>(sums);
            Keys = Frozen.List(sums.Keys.OrderBy(k => k.ToString(), StringComparer.Ordinal), "keys");
            Conditional = Frozen.List(conditional, nameof(conditional));
        }

        /// <summary>Agrège les modificateurs actifs d'un personnage (<see cref="CharacterSetup.ActiveModifiers"/>).</summary>
        public static ModifierSet For(CharacterSetup character) =>
            From((character ?? throw new ArgumentNullException(nameof(character))).ActiveModifiers());

        /// <summary>
        /// Agrège des modificateurs rattachés. Une clé illisible est ignorée (la validation de la classe la refuse avant tout combat).
        /// Le résultat ne dépend pas de l'ordre des sources.
        /// </summary>
        public static ModifierSet From(IEnumerable<BoundModifier> modifiers)
        {
            var sums = new Dictionary<ModifierKey, Fixed>();
            var conditional = new Dictionary<(ModifierField, ModifierConditions), Fixed>();
            foreach (BoundModifier bound in modifiers)
            {
                if (!ModifierKeys.TryParse(bound.Definition.Key, out ModifierKey key)) continue;
                Fixed value = bound.Value;
                ModifierConditions conditions = bound.Definition.ConditionSet;
                if (conditions == ModifierConditions.None)
                {
                    sums.TryGetValue(key, out Fixed sum);
                    sums[key] = sum + value;
                }
                else
                {
                    conditional.TryGetValue((key.Field, conditions), out Fixed sum);
                    conditional[(key.Field, conditions)] = sum + value;
                }
            }
            if (sums.Count == 0 && conditional.Count == 0) return Empty;
            return new ModifierSet(sums, conditional
                .OrderBy(e => (int)e.Key.Item1).ThenBy(e => (int)e.Key.Item2)
                .Select(e => new ConditionalModifier(e.Key.Item1, e.Key.Item2, e.Value)));
        }

        /// <summary>Somme des modificateurs permanents d'une clé (0 si aucun).</summary>
        public Fixed Sum(ModifierKey key) => _sums.TryGetValue(key, out Fixed sum) ? sum : Fixed.Zero;

        public Fixed Sum(ModifierField field, string? parameter = null) => Sum(new ModifierKey(field, parameter));

        /// <summary>Vrai si au moins un modificateur permanent actif cible cette clé.</summary>
        public bool Targets(ModifierKey key) => _sums.ContainsKey(key);

        /// <summary>Drapeau actif : au moins une source le déclare.</summary>
        public bool Flag(ModifierField field) => Targets(new ModifierKey(field));
    }
}
