using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Domain.Builds;
using EdS.Domain.Stats;

namespace EdS.Data.Builds
{
    /// <summary>
    /// Forme d'un build de personnage (<c>level</c>, <c>attributes</c>, <c>talents</c>, <c>activeSkills</c>, <c>ultimate</c>,
    /// <c>passiveSkills</c>), commune au personnage d'un scénario et aux builds de référence (sous-lot 8.5) : présence des champs,
    /// attributs connus et positifs, puis conversion en <see cref="CharacterBuild"/>. Les règles du build (points, prérequis,
    /// emplacements) restent au validateur unique <see cref="BuildValidator"/>.
    /// </summary>
    internal static class BuildShape
    {
        public static readonly IReadOnlyDictionary<string, AttributeKind> AttributeIds = new System.Collections.ObjectModel.ReadOnlyDictionary<string, AttributeKind>(
            ((AttributeKind[])Enum.GetValues(typeof(AttributeKind))).ToDictionary(Rules.RulesCatalog.Id, k => k, StringComparer.Ordinal));

        private static bool Present(object? value, string path, string description, ValidationReport report)
        {
            if (value != null) return true;
            if (!report.HasErrorAtOrAbove(path))
                report.Add(path, ValidationCode.MissingField, $"champ obligatoire absent ({description}).");
            return false;
        }

        private static void Names(List<string?>? list, string path, string description, ValidationReport report)
        {
            if (!Present(list, path, description, report)) return;
            for (int i = 0; i < list!.Count; i++)
            {
                string itemPath = $"{path}[{i}]";
                if (list[i] is null && !report.HasErrorAtOrAbove(itemPath)) report.Add(itemPath, ValidationCode.WrongType, "valeur attendue.");
            }
        }

        /// <summary>Contrôle de forme ; le build n'est converti que si tout le rapport est valide (comme un scénario).</summary>
        public static void Check(BuildData b, string path, ValidationReport report)
        {
            Present(b.Level, path + ".level", "niveau", report);
            if (Present(b.Attributes, path + ".attributes", "les six attributs", report))
            {
                foreach (var attribute in b.Attributes!)
                {
                    string attributePath = path + ".attributes." + attribute.Key;
                    if (!AttributeIds.ContainsKey(attribute.Key))
                        report.Add(attributePath, ValidationCode.UnknownReference,
                            $"attribut inconnu « {attribute.Key} » ; valeurs possibles : {string.Join(", ", AttributeIds.Keys)}.");
                    else if (attribute.Value is int value && value < 0)
                        report.Add(attributePath, ValidationCode.InvalidValue, "un attribut ne peut pas être négatif.");
                    else
                        Present(attribute.Value, attributePath, "entier", report);
                }
                foreach (string id in AttributeIds.Keys.Where(id => b.Attributes.All(a => a.Key != id)))
                    Present(null, path + ".attributes." + id, "entier positif ou nul", report);
            }
            if (Present(b.Talents, path + ".talents", "rangs par talent, éventuellement vide", report))
                foreach (var talent in b.Talents!) Present(talent.Value, path + ".talents." + talent.Key, "rang", report);
            Names(b.ActiveSkills, path + ".activeSkills", "liste, éventuellement vide", report);
            Names(b.PassiveSkills, path + ".passiveSkills", "liste, éventuellement vide", report);
        }

        /// <summary>Build d'un fichier dont la forme a été vérifiée par <see cref="Check"/>.</summary>
        public static CharacterBuild ToCharacterBuild(string classId, BuildData b)
        {
            var values = b.Attributes!.ToDictionary(a => AttributeIds[a.Key], a => a.Value!.Value);
            var attributes = new Attributes(values[AttributeKind.Force], values[AttributeKind.Dexterite], values[AttributeKind.Intelligence],
                values[AttributeKind.Vitalite], values[AttributeKind.Volonte], values[AttributeKind.Endurance]);
            var talents = b.Talents!.ToDictionary(t => t.Key, t => t.Value!.Value, StringComparer.Ordinal);
            return new CharacterBuild(classId, b.Level!.Value, attributes, talents,
                b.ActiveSkills!.Select(s => s!).ToArray(), b.Ultimate, b.PassiveSkills!.Select(s => s!).ToArray());
        }
    }
}
