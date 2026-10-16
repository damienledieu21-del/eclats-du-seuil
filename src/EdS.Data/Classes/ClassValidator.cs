using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using EdS.Data.Triggers;
using EdS.Data.Validation;
using EdS.Domain;
using EdS.Domain.Builds;
using EdS.Domain.Stats;

namespace EdS.Data.Classes
{
    /// <summary>Classe validée, prête pour le validateur de builds, avec son identité (versions, empreinte).</summary>
    public sealed class LoadedClass
    {
        public int SchemaVersion { get; }
        public string ContentVersion { get; }
        public string Fingerprint { get; }
        public ClassDefinition Definition { get; }

        internal LoadedClass(int schemaVersion, string contentVersion, string fingerprint, ClassDefinition definition)
        {
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion;
            Fingerprint = fingerprint;
            Definition = definition;
        }
    }

    /// <summary>Résultat d'un chargement de classe : une définition utilisable, ou le rapport qui explique pourquoi pas.</summary>
    public sealed class ClassLoadResult
    {
        public ValidationReport Report { get; }
        public LoadedClass? Class { get; }
        public bool IsValid => Class != null;

        internal ClassLoadResult(ValidationReport report, LoadedClass? loaded)
        {
            Report = report;
            Class = loaded;
        }
    }

    /// <summary>
    /// Validateur commun des fichiers de classe, partagé par tous les adaptateurs. Il vérifie l'en-tête et la présence
    /// de chaque champ, convertit en <see cref="ClassDefinition"/>, puis délègue toute la cohérence de gameplay
    /// (identifiants, références, rangs, cycles, faisabilité des talents majeurs) à <see cref="ClassDefinition.Validate"/>.
    /// </summary>
    public static class ClassValidator
    {
        public const string Kind = "class";
        public const int CurrentSchemaVersion = DataFormat.ClassSchemaVersion;

        public static IReadOnlyDictionary<string, SkillKind> SkillKinds { get; } = new System.Collections.ObjectModel.ReadOnlyDictionary<string, SkillKind>(new Dictionary<string, SkillKind>(StringComparer.Ordinal)
        {
            ["active"] = SkillKind.Active,
            ["ultimate"] = SkillKind.Ultimate,
            ["passive"] = SkillKind.Passive,
        });

        public static IReadOnlyDictionary<string, PowerType> PowerTypes { get; } = new System.Collections.ObjectModel.ReadOnlyDictionary<string, PowerType>(new Dictionary<string, PowerType>(StringComparer.Ordinal)
        {
            ["physique"] = PowerType.Physique,
            ["agile"] = PowerType.Agile,
            ["magique"] = PowerType.Magique,
        });

        public static IReadOnlyDictionary<string, DamageType> DamageTypes { get; } = new System.Collections.ObjectModel.ReadOnlyDictionary<string, DamageType>(new Dictionary<string, DamageType>(StringComparer.Ordinal)
        {
            ["physique"] = DamageType.Physique,
            ["magique"] = DamageType.Magique,
        });

        public static IReadOnlyDictionary<string, SkillTargeting> Targetings { get; } = new System.Collections.ObjectModel.ReadOnlyDictionary<string, SkillTargeting>(new Dictionary<string, SkillTargeting>(StringComparer.Ordinal)
        {
            ["enemy"] = SkillTargeting.Enemy,
            ["allEnemies"] = SkillTargeting.AllEnemies,
        });

        public static ClassLoadResult Validate(ClassDocument document, ProgressionRules progression, ValidationReport? report = null)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));
            if (progression is null) throw new ArgumentNullException(nameof(progression));
            report ??= new ValidationReport();

            if (!HeaderValidator.Validate(document.SchemaVersion, document.Kind, document.ContentVersion, Kind, CurrentSchemaVersion, report))
                return new ClassLoadResult(report, null);   // structure d'une autre version : refusée sans analyse du contenu
            var shape = new ShapeChecker(report);
            shape.Check(document.Data);
            if (!report.IsValid) return new ClassLoadResult(report, null);

            ClassDefinition definition = Build(document.Data!);
            foreach (RuleViolation violation in definition.Validate(progression))
            {
                ValidationCode code = violation.Kind switch
                {
                    RuleViolationKind.UnknownReference => ValidationCode.UnknownReference,
                    RuleViolationKind.DuplicateId => ValidationCode.DuplicateId,
                    RuleViolationKind.NumericLimit => ValidationCode.OutOfRange,
                    RuleViolationKind.LimitExceeded => ValidationCode.LimitExceeded,
                    _ => ValidationCode.InvalidValue,
                };
                foreach (string field in violation.Fields)
                    report.Add("$.data." + field, code, violation.Message);
            }
            if (!report.IsValid) return new ClassLoadResult(report, null);
            // Lot 7.3 (A15) : cycles statiques entre déclencheurs de la classe, en avertissements.
            foreach (RuleViolation warning in Domain.Combat.TriggerGraph.Cycles(definition.NamedTriggers(), null))
                foreach (string field in warning.Fields) report.AddWarning("$.data." + field, warning.Message);

            string fingerprint = ContentFingerprint.Compute(CanonicalText(document.SchemaVersion!.Value, document.Data!, definition));
            return new ClassLoadResult(report, new LoadedClass(document.SchemaVersion.Value, document.ContentVersion!, fingerprint, definition));
        }

        /// <summary>
        /// Écriture canonique pour l'empreinte : chaque valeur sous la forme <c>chemin=valeur</c>, dans l'ordre du fichier
        /// (l'ordre des arbres, talents et compétences fait partie du contenu ; l'ordre des clés et les espaces non).
        /// </summary>
        public static string CanonicalText(int schemaVersion, ClassData data, ClassDefinition definition)
        {
            var text = new StringBuilder();
            var w = new CanonicalWriter(text);
            // Correctif 7.3.1 (A73-M02) : une valeur absente s'écrit comme absente, jamais comme une chaîne vide ou « none ».
            void Line(string path, object? value) => w.Optional(path, value is null ? null : Convert.ToString(value, CultureInfo.InvariantCulture));
            void Requirements(string path, List<RequirementData?> list)
            {
                Line(path + ".count", list.Count);
                for (int r = 0; r < list.Count; r++)
                {
                    Line($"{path}[{r}].talent", list[r]!.Talent);
                    Line($"{path}[{r}].rank", list[r]!.Rank);
                }
            }

            text.Append("eds-class\n");
            Line("canonical", CanonicalWriter.Version);
            Line("schemaVersion", schemaVersion);
            Line("id", data.Id);
            Line("name", data.Name);
            Line("trees.count", data.Trees!.Count);
            for (int t = 0; t < data.Trees.Count; t++)
            {
                TreeData tree = data.Trees[t]!;
                Line($"trees[{t}].id", tree.Id);
                Line($"trees[{t}].name", tree.Name);
                Line($"trees[{t}].talents.count", tree.Talents!.Count);
                for (int i = 0; i < tree.Talents.Count; i++)
                {
                    TalentData talent = tree.Talents[i]!;
                    string p = $"trees[{t}].talents[{i}]";
                    Line(p + ".id", talent.Id);
                    Line(p + ".name", talent.Name);
                    Line(p + ".maxRank", talent.MaxRank);
                    Line(p + ".major", talent.Major == true ? "true" : "false");
                    Line(p + ".placeholder", talent.Placeholder == true ? "true" : "false");
                    Requirements(p + ".prerequisites", talent.Prerequisites!);
                    TriggerShape.AppendCanonical(text, p + ".triggers", definition.Trees[t].Talents[i].Triggers);
                }
            }
            Line("skills.count", data.Skills!.Count);
            for (int s = 0; s < data.Skills.Count; s++)
            {
                SkillData skill = data.Skills[s]!;
                string p = $"skills[{s}]";
                Line(p + ".id", skill.Id);
                Line(p + ".name", skill.Name);
                Line(p + ".kind", skill.Kind);
                Line(p + ".minLevel", skill.MinLevel);
                Line(p + ".placeholder", skill.Placeholder == true ? "true" : "false");
                Requirements(p + ".talentRequirements", skill.TalentRequirements!);
                TriggerShape.AppendCanonical(text, p + ".triggers", definition.Skills[s].Triggers);
                if (skill.Combat is not { } combat)
                {
                    Line(p + ".combat", null);
                    continue;
                }
                Line(p + ".combat.powerType", combat.PowerType);
                Line(p + ".combat.damageType", combat.DamageType);
                Line(p + ".combat.coefficient", combat.Coefficient!.Value.ToString());
                Line(p + ".combat.enduranceCost", combat.EnduranceCost);
                Line(p + ".combat.cooldownMs", combat.CooldownMs);
                Line(p + ".combat.targeting", combat.Targeting);
                Line(p + ".combat.unblockable", combat.Unblockable == true ? "true" : "false");
                Line(p + ".combat.rampartChargeCost", combat.RampartChargeCost);
                Line(p + ".combat.rampartChargeGain", combat.RampartChargeGain);
                Line(p + ".combat.provisional", combat.Provisional == true ? "true" : "false");
                Line(p + ".combat.applies.count", combat.Applies!.Count);
                for (int a = 0; a < combat.Applies.Count; a++)
                {
                    Line($"{p}.combat.applies[{a}].status", combat.Applies[a]!.Status);
                    Line($"{p}.combat.applies[{a}].coefficient", combat.Applies[a]!.Coefficient?.ToString());
                }
            }
            return text.ToString();
        }

        /// <summary>Altérations portées, converties pour le domaine (lot 7.2) ; suppose la forme vérifiée.</summary>
        public static IReadOnlyList<Domain.Combat.StatusApplication> Applications(List<StatusApplicationData?> list) =>
            list.Select(a => new Domain.Combat.StatusApplication(a!.Status!, a.Coefficient)).ToArray();

        /// <summary>Vérifie la forme d'une liste d'altérations portées : chaque entrée nomme une altération.</summary>
        public static void CheckApplications(List<StatusApplicationData?>? list, string path, ValidationReport report)
        {
            if (list is null)
            {
                if (!report.HasErrorAtOrAbove(path))
                    report.Add(path, ValidationCode.MissingField, "champ obligatoire absent (liste des altérations appliquées, éventuellement vide).");
                return;
            }
            for (int i = 0; i < list.Count; i++)
            {
                string itemPath = $"{path}[{i}]";
                if (list[i] is not { } a)
                {
                    if (!report.HasErrorAtOrAbove(itemPath)) report.Add(itemPath, ValidationCode.WrongType, "objet attendu.");
                }
                else if (a.Status is null && !report.HasErrorAtOrAbove(itemPath + ".status"))
                    report.Add(itemPath + ".status", ValidationCode.MissingField, "champ obligatoire absent (identifiant de l'altération).");
            }
        }

        private static ClassDefinition Build(ClassData data)
        {
            IReadOnlyList<TalentRequirement> Requirements(List<RequirementData?> list) =>
                list.Select(r => new TalentRequirement(r!.Talent!, r.Rank!.Value)).ToArray();

            var trees = data.Trees!.Select(t => new TalentTreeDefinition(t!.Id!, t.Name!,
                t.Talents!.Select(x => new TalentDefinition(x!.Id!, x.Name!, x.MaxRank!.Value, x.Major!.Value,
                    Requirements(x.Prerequisites!), x.Placeholder!.Value, TriggerShape.Build(x.Triggers))).ToArray())).ToArray();
            SkillCombatDefinition? Combat(SkillCombatData? c) => c is null ? null
                : new SkillCombatDefinition(PowerTypes[c.PowerType!], DamageTypes[c.DamageType!], c.Coefficient!.Value,
                    c.EnduranceCost!.Value, c.CooldownMs!.Value, Targetings[c.Targeting!], c.Provisional!.Value,
                    c.Unblockable!.Value, c.RampartChargeCost!.Value, c.RampartChargeGain!.Value, Applications(c.Applies!));
            var skills = data.Skills!.Select(s => new SkillDefinition(s!.Id!, s.Name!, SkillKinds[s.Kind!], s.MinLevel!.Value,
                Requirements(s.TalentRequirements!), s.Placeholder!.Value, Combat(s.Combat), TriggerShape.Build(s.Triggers))).ToArray();
            return new ClassDefinition(data.Id!, data.Name!, trees, skills);
        }

        /// <summary>Vérifie que chaque champ obligatoire est présent ; ne signale rien là où l'adaptateur a déjà signalé une erreur.</summary>
        private sealed class ShapeChecker
        {
            private readonly ValidationReport _report;

            public ShapeChecker(ValidationReport report) => _report = report;

            private bool Present(object? value, string path, string description)
            {
                if (value != null) return true;
                if (!_report.HasErrorAtOrAbove(path))
                    _report.Add(path, ValidationCode.MissingField, $"champ obligatoire absent ({description}).");
                return false;
            }

            private void Items<T>(List<T?>? list, string path, string description, Action<T, string> check) where T : class
            {
                if (!Present(list, path, description)) return;
                for (int i = 0; i < list!.Count; i++)
                {
                    string itemPath = $"{path}[{i}]";
                    if (list[i] is { } item) check(item, itemPath);
                    else if (!_report.HasErrorAtOrAbove(itemPath)) _report.Add(itemPath, ValidationCode.WrongType, "objet attendu.");
                }
            }

            public void Check(ClassData? data)
            {
                const string root = "$.data";
                if (!Present(data, root, "objet décrivant la classe")) return;
                Present(data!.Id, root + ".id", "identifiant « class.… »");
                Present(data.Name, root + ".name", "nom affiché");
                Items(data.Trees, root + ".trees", "liste des arbres", CheckTree);
                Items(data.Skills, root + ".skills", "liste des compétences", CheckSkill);
            }

            private void CheckTree(TreeData tree, string path)
            {
                Present(tree.Id, path + ".id", "identifiant « tree.… »");
                Present(tree.Name, path + ".name", "nom affiché");
                Items(tree.Talents, path + ".talents", "liste des talents", CheckTalent);
            }

            private void CheckTalent(TalentData talent, string path)
            {
                Present(talent.Id, path + ".id", "identifiant « talent.… »");
                Present(talent.Name, path + ".name", "nom affiché");
                Present(talent.MaxRank, path + ".maxRank", "entier");
                Present(talent.Major, path + ".major", "booléen");
                Present(talent.Placeholder, path + ".placeholder", "booléen");
                Items(talent.Prerequisites, path + ".prerequisites", "liste, éventuellement vide", CheckRequirement);
                TriggerShape.Check(talent.Triggers, path + ".triggers", _report);
            }

            private void CheckSkill(SkillData skill, string path)
            {
                Present(skill.Id, path + ".id", "identifiant « skill.… »");
                Present(skill.Name, path + ".name", "nom affiché");
                if (Present(skill.Kind, path + ".kind", "active, ultimate ou passive") && !SkillKinds.ContainsKey(skill.Kind!))
                    _report.Add(path + ".kind", ValidationCode.UnknownReference,
                        $"type de compétence inconnu « {skill.Kind} » ; valeurs possibles : {string.Join(", ", SkillKinds.Keys)}.");
                Present(skill.MinLevel, path + ".minLevel", "entier");
                Present(skill.Placeholder, path + ".placeholder", "booléen");
                Items(skill.TalentRequirements, path + ".talentRequirements", "liste, éventuellement vide", CheckRequirement);
                if (skill.Combat is { } combat) CheckCombat(combat, path + ".combat");
                TriggerShape.Check(skill.Triggers, path + ".triggers", _report);
            }

            private void Token<T>(string? value, string path, string description, IReadOnlyDictionary<string, T> allowed)
            {
                if (Present(value, path, description) && !allowed.ContainsKey(value!))
                    _report.Add(path, ValidationCode.UnknownReference, $"valeur inconnue « {value} » ; valeurs possibles : {string.Join(", ", allowed.Keys)}.");
            }

            private void CheckCombat(SkillCombatData combat, string path)
            {
                Token(combat.PowerType, path + ".powerType", "physique, agile ou magique", PowerTypes);
                Token(combat.DamageType, path + ".damageType", "physique ou magique", DamageTypes);
                Present(combat.Coefficient, path + ".coefficient", "nombre décimal");
                Present(combat.EnduranceCost, path + ".enduranceCost", "entier");
                Present(combat.CooldownMs, path + ".cooldownMs", "entier, en millisecondes");
                Token(combat.Targeting, path + ".targeting", "enemy ou allEnemies", Targetings);
                Present(combat.Unblockable, path + ".unblockable", "booléen");
                Present(combat.RampartChargeCost, path + ".rampartChargeCost", "entier");
                Present(combat.RampartChargeGain, path + ".rampartChargeGain", "entier");
                Present(combat.Provisional, path + ".provisional", "booléen");
                CheckApplications(combat.Applies, path + ".applies", _report);
            }

            private void CheckRequirement(RequirementData requirement, string path)
            {
                Present(requirement.Talent, path + ".talent", "identifiant de talent");
                Present(requirement.Rank, path + ".rank", "rang minimal");
            }
        }
    }
}
