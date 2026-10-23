using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Builds;
using EdS.Data.Classes;
using EdS.Data.Validation;
using EdS.Domain.Builds;
using EdS.Domain.Collections;
using EdS.Domain.Stats;

namespace EdS.Data.Characters
{
    /// <summary>Progression validée d'une classe : attributs de base par niveau et compétences d'un nouveau personnage.</summary>
    public sealed class ClassProgression
    {
        public string ClassId { get; }

        /// <summary>Attributs de base, du niveau 1 (indice 0) au niveau maximal.</summary>
        public IReadOnlyList<Attributes> Attributes { get; }

        public IReadOnlyList<string> StartActiveSkills { get; }
        public IReadOnlyList<string> StartPassiveSkills { get; }

        internal ClassProgression(string classId, IEnumerable<Attributes> attributes, IEnumerable<string> actives, IEnumerable<string> passives)
        {
            ClassId = classId;
            Attributes = Frozen.List(attributes, nameof(attributes));
            StartActiveSkills = Frozen.List(actives, nameof(actives));
            StartPassiveSkills = Frozen.List(passives, nameof(passives));
        }

        /// <summary>Attributs de base au niveau <paramref name="level"/> (1 à <see cref="CharacterProgression.MaxLevel"/>).</summary>
        public Attributes At(int level) =>
            level >= 1 && level <= Attributes.Count ? Attributes[level - 1] : throw new ArgumentOutOfRangeException(nameof(level));
    }

    /// <summary>
    /// Sous-lot 12.2 (cadrage Q3 et Q20) : progression du personnage persistant, validée. Expérience cumulée par niveau (niveaux 1 à
    /// <see cref="MaxLevel"/>), expérience des créatures vaincues, et par classe, les attributs de base par niveau (pas de répartition
    /// manuelle) et les compétences d'un nouveau personnage. Valeurs provisoires.
    /// </summary>
    public sealed class CharacterProgression
    {
        public int SchemaVersion { get; }
        public string ContentVersion { get; }
        public int MaxLevel { get; }

        /// <summary>Expérience cumulée qu'il faut pour être à chaque niveau ; indice 0 : niveau 1 (0).</summary>
        public IReadOnlyList<long> XpToLevel { get; }

        public long KillXpPerHealth { get; }

        /// <summary>Classes, par identifiant.</summary>
        public IReadOnlyList<ClassProgression> Classes { get; }

        internal CharacterProgression(int schemaVersion, string contentVersion, int maxLevel, IEnumerable<long> xpToLevel, long killXpPerHealth,
            IEnumerable<ClassProgression> classes)
        {
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion;
            MaxLevel = maxLevel;
            XpToLevel = Frozen.List(xpToLevel, nameof(xpToLevel));
            KillXpPerHealth = killXpPerHealth;
            Classes = Frozen.List(classes.OrderBy(c => c.ClassId, StringComparer.Ordinal), nameof(classes));
        }

        public ClassProgression ForClass(string classId) =>
            Classes.FirstOrDefault(c => c.ClassId == classId) ?? throw new ArgumentException($"Classe « {classId} » sans progression.", nameof(classId));

        /// <summary>Niveau atteint avec <paramref name="xp"/> d'expérience cumulée (au plus <see cref="MaxLevel"/>).</summary>
        public int LevelFor(long xp)
        {
            if (xp < 0) throw new ArgumentOutOfRangeException(nameof(xp));
            int level = 1;
            while (level < MaxLevel && xp >= XpToLevel[level]) level++;
            return level;
        }

        /// <summary>Expérience d'une créature vaincue de <paramref name="maxHealth"/> PV maximaux : 1 par tranche de <see cref="KillXpPerHealth"/>, au moins 1.</summary>
        public long KillXp(long maxHealth) => Math.Max(1, maxHealth / KillXpPerHealth);
    }

    /// <summary>Résultat d'un chargement de progression : la progression validée, ou le rapport qui explique pourquoi pas.</summary>
    public sealed class ProgressionLoadResult
    {
        public ValidationReport Report { get; }
        public CharacterProgression? Progression { get; }
        public bool IsValid => Progression != null;

        internal ProgressionLoadResult(ValidationReport report, CharacterProgression? progression)
        {
            Report = report;
            Progression = progression;
        }
    }

    /// <summary>
    /// Validateur unique de la progression du personnage persistant (<c>kind: "progression"</c>, sous-lot 12.2), contre les règles et les
    /// classes chargées : niveau maximal au plus celui des règles, expérience cumulée qui part de 0 et croît strictement, une table
    /// d'attributs par niveau pour chaque classe (attributs connus, positifs, jamais en baisse d'un niveau au suivant), et un build de départ
    /// valide au niveau 1 pour le validateur des builds.
    /// </summary>
    public static class ProgressionValidator
    {
        public const string Kind = "progression";

        /// <summary>Version 1 (sous-lot 12.2) : <c>maxLevel</c>, <c>xpToLevel</c>, <c>killXpPerHealth</c>, <c>classes[]</c>.</summary>
        public const int CurrentSchemaVersion = 1;

        /// <summary>Plafond technique de l'expérience cumulée.</summary>
        public const long MaxXp = 1_000_000_000;

        public static ProgressionLoadResult Validate(ProgressionDocument document, GameData data, ValidationReport? report = null)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));
            if (data is null) throw new ArgumentNullException(nameof(data));
            report ??= new ValidationReport();
            if (!HeaderValidator.Validate(document.SchemaVersion, document.Kind, document.ContentVersion, Kind, CurrentSchemaVersion, report))
                return new ProgressionLoadResult(report, null);
            ProgressionData? d = document.Data;
            if (d is null)
            {
                Missing(report, "$.data", "niveau maximal, expérience par niveau, expérience des créatures, classes");
                return new ProgressionLoadResult(report, null);
            }
            int rulesMax = data.Rules.Progression.MaxLevel;
            if (d.MaxLevel is null) Missing(report, "$.data.maxLevel", "niveau maximal du parcours");
            else if (d.MaxLevel < 1 || d.MaxLevel > rulesMax)
                report.Add("$.data.maxLevel", ValidationCode.OutOfRange, $"niveau maximal hors de [1, {rulesMax}] (progression.maxLevel des règles).");
            int max = d.MaxLevel is int m && m >= 1 && m <= rulesMax ? m : 0;
            if (d.XpToLevel is null) Missing(report, "$.data.xpToLevel", "expérience cumulée de chaque niveau");
            else
            {
                if (max > 0 && d.XpToLevel.Count != max)
                    report.Add("$.data.xpToLevel", ValidationCode.InvalidValue, $"{d.XpToLevel.Count} niveaux, {max} attendus (un par niveau, du niveau 1).");
                for (int i = 0; i < d.XpToLevel.Count; i++)
                {
                    string path = $"$.data.xpToLevel[{i}]";
                    if (d.XpToLevel[i] is not long xp) { Missing(report, path, "expérience cumulée"); continue; }
                    if (i == 0 && xp != 0) report.Add(path, ValidationCode.InvalidValue, "le niveau 1 demande 0 expérience.");
                    else if (xp < 0 || xp > MaxXp) report.Add(path, ValidationCode.OutOfRange, $"expérience hors de [0, {MaxXp}].");
                    else if (i > 0 && d.XpToLevel[i - 1] is long previous && xp <= previous)
                        report.Add(path, ValidationCode.InvalidValue, "l'expérience cumulée doit croître strictement d'un niveau au suivant.");
                }
            }
            if (d.KillXpPerHealth is null) Missing(report, "$.data.killXpPerHealth", "PV maximaux par point d'expérience d'une créature vaincue");
            else if (d.KillXpPerHealth < 1 || d.KillXpPerHealth > 1_000_000_000)
                report.Add("$.data.killXpPerHealth", ValidationCode.OutOfRange, "PV par point d'expérience hors de [1, 10⁹].");

            var classes = new List<ClassProgression>();
            if (d.Classes is null) Missing(report, "$.data.classes", "progression de chaque classe");
            else
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < d.Classes.Count; i++)
                {
                    string path = $"$.data.classes[{i}]";
                    if (d.Classes[i] is not ClassProgressionData c) { Missing(report, path, "progression d'une classe"); continue; }
                    if (CheckClass(c, path, max, data, seen, report) is { } loaded) classes.Add(loaded);
                }
                // Revue Codex de la PR #43 : chaque classe chargée a sa progression ; sinon l'erreur ne se verrait qu'à la création d'un personnage.
                foreach (LoadedClass loaded in data.Classes)
                    if (!seen.Contains(loaded.Definition.Id))
                        report.Add("$.data.classes", ValidationCode.MissingField, $"progression absente pour la classe « {loaded.Definition.Id} ».");
            }
            if (!report.IsValid || document.ContentVersion is null || max == 0) return new ProgressionLoadResult(report, null);
            return new ProgressionLoadResult(report, new CharacterProgression(document.SchemaVersion!.Value, document.ContentVersion, max,
                d.XpToLevel!.Select(x => x!.Value), d.KillXpPerHealth!.Value, classes));
        }

        private static ClassProgression? CheckClass(ClassProgressionData c, string path, int max, GameData data, HashSet<string> seen, ValidationReport report)
        {
            LoadedClass? loaded = null;
            if (c.Class is null) Missing(report, path + ".class", "identifiant de classe");
            else if (!seen.Add(c.Class)) report.Add(path + ".class", ValidationCode.DuplicateId, $"classe « {c.Class} » déjà décrite plus haut.");
            else if (!data.TryGetClass(c.Class, out LoadedClass found))
                report.Add(path + ".class", ValidationCode.UnknownReference, $"classe inconnue « {c.Class} ».");
            else loaded = found;

            var levels = new List<Attributes>();
            if (c.Attributes is null) Missing(report, path + ".attributes", "attributs de base de chaque niveau");
            else
            {
                if (max > 0 && c.Attributes.Count != max)
                    report.Add(path + ".attributes", ValidationCode.InvalidValue, $"{c.Attributes.Count} niveaux, {max} attendus (un par niveau, du niveau 1).");
                for (int l = 0; l < c.Attributes.Count; l++)
                {
                    string levelPath = $"{path}.attributes[{l}]";
                    if (ToAttributes(c.Attributes[l], levelPath, report) is not Attributes a) continue;
                    if (levels.Count == l && l > 0)
                        foreach (AttributeKind kind in (AttributeKind[])Enum.GetValues(typeof(AttributeKind)))
                            if (a[kind] < levels[l - 1][kind])
                                report.Add($"{levelPath}.{Rules.RulesCatalog.Id(kind)}", ValidationCode.InvalidValue, "un attribut de base ne baisse pas en montant de niveau.");
                    if (levels.Count == l) levels.Add(a);
                }
            }
            Names(c.StartActiveSkills, path + ".startActiveSkills", report);
            Names(c.StartPassiveSkills, path + ".startPassiveSkills", report);
            if (loaded is null || !report.IsValid || levels.Count == 0) return null;
            // Le build de départ doit être valide au niveau 1 (emplacements, compétences de la classe).
            var start = new CharacterBuild(loaded.Definition.Id, 1, levels[0], new Dictionary<string, int>(),
                c.StartActiveSkills!.Select(s => s!).ToArray(), null, c.StartPassiveSkills!.Select(s => s!).ToArray());
            foreach (BuildViolation v in BuildValidator.Validate(start, loaded.Definition, data.Rules.Progression))
                report.Add(v.Subject.StartsWith("activeSkills", StringComparison.Ordinal) ? path + ".startActiveSkills"
                    : v.Subject.StartsWith("passiveSkills", StringComparison.Ordinal) ? path + ".startPassiveSkills" : path,
                    ValidationCode.InvalidValue, $"build de départ invalide au niveau 1 ({v.Subject}) : {v.Message} [{v.Code}]");
            return report.IsValid ? new ClassProgression(loaded.Definition.Id, levels, c.StartActiveSkills!.Select(s => s!), c.StartPassiveSkills!.Select(s => s!)) : null;
        }

        private static Attributes? ToAttributes(List<KeyValuePair<string, int?>>? map, string path, ValidationReport report)
        {
            if (map is null) { Missing(report, path, "les six attributs"); return null; }
            bool ok = true;
            foreach (var attribute in map)
            {
                string attributePath = path + "." + attribute.Key;
                if (!BuildShape.AttributeIds.ContainsKey(attribute.Key))
                {
                    report.Add(attributePath, ValidationCode.UnknownReference, $"attribut inconnu « {attribute.Key} ».");
                    ok = false;
                }
                else if (attribute.Value is not int value) { Missing(report, attributePath, "entier"); ok = false; }
                else if (value < 0 || value > BuildValidator.MaxAttributeValue)
                {
                    report.Add(attributePath, ValidationCode.OutOfRange, $"attribut hors de [0, {BuildValidator.MaxAttributeValue}].");
                    ok = false;
                }
            }
            foreach (string id in BuildShape.AttributeIds.Keys.Where(id => map.All(a => a.Key != id)))
            {
                Missing(report, path + "." + id, "entier positif ou nul");
                ok = false;
            }
            if (!ok) return null;
            int V(AttributeKind k) => map.First(a => BuildShape.AttributeIds[a.Key] == k).Value!.Value;
            return new Attributes(V(AttributeKind.Force), V(AttributeKind.Dexterite), V(AttributeKind.Intelligence), V(AttributeKind.Vitalite),
                V(AttributeKind.Volonte), V(AttributeKind.Endurance));
        }

        private static void Names(List<string?>? list, string path, ValidationReport report)
        {
            if (list is null) { Missing(report, path, "liste de compétences, éventuellement vide"); return; }
            for (int i = 0; i < list.Count; i++)
                if (list[i] is null) Missing(report, $"{path}[{i}]", "identifiant de compétence");
        }

        private static void Missing(ValidationReport report, string path, string what)
        {
            if (!report.HasErrorAtOrAbove(path)) report.Add(path, ValidationCode.MissingField, $"champ obligatoire absent ({what}).");
        }
    }
}
