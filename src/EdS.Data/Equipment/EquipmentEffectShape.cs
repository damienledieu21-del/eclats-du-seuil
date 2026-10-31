using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Modifiers;
using EdS.Data.Triggers;
using EdS.Data.Validation;
using EdS.Domain.Combat;

namespace EdS.Data.Equipment
{
    /// <summary>
    /// Sous-lot V1.3 : effet d'équipement brut, même forme dans les scénarios (<c>combatants[i].equipment[k]</c>, avec <c>id</c>), dans le
    /// catalogue des objets (<c>items[i].effects</c>, l'identifiant est celui de l'objet) et dans ses ensembles
    /// (<c>sets[i].bonuses[j].effects</c>, identifiant « set.….&lt;pièces&gt; ») :
    /// <c>{ "id": "item.lame_du_seuil", "text": "…", "modifiers": [ … ], "triggers": [ … ] }</c>.
    /// </summary>
    public sealed class EquipmentEffectData
    {
        /// <summary>Source (« item.… » ou « set.… ») ; scénarios et journaux seulement.</summary>
        public string? Id { get; set; }

        /// <summary>Texte montré au joueur (catalogue seulement), relu par Damien.</summary>
        public string? Text { get; set; }

        public List<ModifierData?>? Modifiers { get; set; }
        public List<TriggerData?>? Triggers { get; set; }
    }

    /// <summary>
    /// Forme des effets d'équipement : champs présents, modificateurs et déclencheurs bien formés (<see cref="ModifierShape"/>,
    /// <see cref="TriggerShape"/>). Toute la cohérence de gameplay (clés admises, bornes des règles, références) est vérifiée par
    /// <see cref="EquipmentEffect.Check"/>, seule validation de gameplay, appelée par le catalogue des objets et par la validation du combat.
    /// </summary>
    public static class EquipmentEffectShape
    {
        /// <summary>Vérifie la forme d'un effet ; <paramref name="withId"/> : l'identifiant de source est obligatoire (scénario).</summary>
        public static void Check(EquipmentEffectData effect, string path, bool withId, ValidationReport report)
        {
            if (effect is null) throw new ArgumentNullException(nameof(effect));
            if (withId && effect.Id is null && !report.HasErrorAtOrAbove(path + ".id"))
                report.Add(path + ".id", ValidationCode.MissingField, "champ obligatoire absent (source « item.… » ou « set.… »).");
            ModifierShape.Check(effect.Modifiers, path + ".modifiers", report, equipment: true);
            TriggerShape.Check(effect.Triggers, path + ".triggers", report);
        }

        /// <summary>Conversion pour le domaine ; suppose <see cref="Check"/> sans erreur. <paramref name="source"/> : la source (sinon son <c>id</c>).</summary>
        public static EquipmentEffect Build(EquipmentEffectData effect, string? source = null) =>
            new EquipmentEffect(source ?? effect.Id!, ModifierShape.Build(effect.Modifiers), TriggerShape.Build(effect.Triggers));

        /// <summary>
        /// Recopie les erreurs d'une vérification de gameplay (<see cref="EquipmentEffect.Check"/>, chemins déjà complets) dans un rapport ; vrai
        /// s'il n'y en avait aucune.
        /// </summary>
        public static bool Report(IReadOnlyList<Domain.RuleViolation> violations, ValidationReport report)
        {
            foreach (Domain.RuleViolation v in violations)
            {
                ValidationCode code = v.Kind switch
                {
                    Domain.RuleViolationKind.UnknownReference => ValidationCode.UnknownReference,
                    Domain.RuleViolationKind.DuplicateId => ValidationCode.DuplicateId,
                    Domain.RuleViolationKind.LimitExceeded => ValidationCode.LimitExceeded,
                    Domain.RuleViolationKind.NumericLimit => ValidationCode.OutOfRange,
                    _ => ValidationCode.InvalidValue,
                };
                foreach (string field in v.Fields) report.Add(field, code, v.Message);
            }
            return violations.Count == 0;
        }
    }
}
