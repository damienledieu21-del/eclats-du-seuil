using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Classes;
using EdS.Data.Validation;
using EdS.Domain.Builds;

namespace EdS.Data.Builds
{
    /// <summary>Rôle d'un build de référence dans les mesures (feuille de route, « Builds et scénarios de référence »).</summary>
    public enum ReferenceBuildCategory
    {
        /// <summary>Build de spécialisation (Rempart, Contrecoup, Gardien) : chacun doit exceller sur sa mesure.</summary>
        Reference = 0,

        /// <summary>Build de contrôle (hybride sans majeur, niveau 10).</summary>
        Control = 1,
    }

    /// <summary>Build de référence validé : identifiant stable, nom, rôle et build complet (attributs compris).</summary>
    public sealed class ReferenceBuild
    {
        public string Id { get; }
        public string Name { get; }
        public ReferenceBuildCategory Category { get; }
        public CharacterBuild Build { get; }

        internal ReferenceBuild(string id, string name, ReferenceBuildCategory category, CharacterBuild build)
        {
            Id = id;
            Name = name;
            Category = category;
            Build = build;
        }
    }

    /// <summary>Fichier de builds de référence validé contre sa classe et la progression des règles.</summary>
    public sealed class LoadedBuildSet
    {
        public int SchemaVersion { get; }
        public string ContentVersion { get; }
        public string ClassId { get; }

        /// <summary>Builds dans l'ordre du fichier.</summary>
        public IReadOnlyList<ReferenceBuild> Builds { get; }

        internal LoadedBuildSet(int schemaVersion, string contentVersion, string classId, IEnumerable<ReferenceBuild> builds)
        {
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion;
            ClassId = classId;
            Builds = EdS.Domain.Collections.Frozen.List(builds, nameof(builds));
        }
    }

    /// <summary>Résultat d'un chargement de builds : les builds validés, ou le rapport qui explique pourquoi pas.</summary>
    public sealed class BuildSetLoadResult
    {
        public ValidationReport Report { get; }
        public LoadedBuildSet? BuildSet { get; }
        public bool IsValid => BuildSet != null;

        internal BuildSetLoadResult(ValidationReport report, LoadedBuildSet? buildSet)
        {
            Report = report;
            BuildSet = buildSet;
        }
    }

    /// <summary>
    /// Validateur des fichiers de builds de référence (sous-lot 8.5, décision A12 : builds de référence livrés en données). En-tête,
    /// présence et forme des champs (forme du build commune aux scénarios), classe chargée, identifiants stables « build.… » uniques,
    /// puis le validateur unique des builds (<see cref="BuildValidator"/>) contre la classe et la progression des règles.
    /// </summary>
    public static class BuildSetValidator
    {
        public const string Kind = "builds";

        /// <summary>Version 1 (sous-lot 8.5) : <c>data.class</c> et <c>data.builds[]</c> (<c>id</c>, <c>name</c>, <c>category</c>, <c>build</c>).</summary>
        public const int CurrentSchemaVersion = DataFormat.BuildsSchemaVersion;

        /// <summary>Plafond technique de builds par fichier (enveloppe pré-alpha, comme les combattants d'un scénario).</summary>
        public const int MaxBuilds = 64;

        public static IReadOnlyDictionary<string, ReferenceBuildCategory> Categories { get; } =
            new Dictionary<string, ReferenceBuildCategory>(StringComparer.Ordinal)
            {
                ["reference"] = ReferenceBuildCategory.Reference,
                ["control"] = ReferenceBuildCategory.Control,
            };

        public static string Id(ReferenceBuildCategory category) => Categories.First(p => p.Value == category).Key;

        /// <summary>Valide un fichier de builds contre les classes et la progression de <paramref name="data"/> (ses propres builds sont ignorés).</summary>
        public static BuildSetLoadResult Validate(BuildSetDocument document, GameData data, ValidationReport? report = null)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));
            if (data is null) throw new ArgumentNullException(nameof(data));
            report ??= new ValidationReport();
            if (!HeaderValidator.Validate(document.SchemaVersion, document.Kind, document.ContentVersion, Kind, CurrentSchemaVersion, report))
                return new BuildSetLoadResult(report, null);

            BuildSetData? set = document.Data;
            LoadedClass? loaded = null;
            if (set is null)
            {
                if (!report.HasErrorAtOrAbove("$.data")) report.Add("$.data", ValidationCode.MissingField, "champ obligatoire absent (classe et liste des builds).");
            }
            else
            {
                if (set.Class is null)
                {
                    if (!report.HasErrorAtOrAbove("$.data.class")) report.Add("$.data.class", ValidationCode.MissingField, "champ obligatoire absent (identifiant de classe).");
                }
                else if (!data.TryGetClass(set.Class, out LoadedClass found))
                    report.Add("$.data.class", ValidationCode.UnknownReference,
                        $"classe inconnue « {set.Class} » ; classes chargées : {string.Join(", ", data.Classes.Select(c => c.Definition.Id))}.");
                else loaded = found;
                CheckBuilds(set.Builds, report);
            }
            if (report.IsValid && loaded != null)
            {
                // Les builds d'un fichier sont ceux de sa classe : « build.bastion.… » pour « class.bastion ».
                string prefix = "build." + loaded.Definition.Id.Substring(loaded.Definition.Id.IndexOf('.') + 1) + ".";
                for (int i = 0; i < set!.Builds!.Count; i++)
                    if (!set.Builds[i]!.Id!.StartsWith(prefix, StringComparison.Ordinal))
                        report.Add($"$.data.builds[{i}].id", ValidationCode.InvalidValue,
                            $"identifiant « {set.Builds[i]!.Id} » hors de la classe « {loaded.Definition.Id} » : « {prefix}… » attendu.");
            }
            if (!report.IsValid || loaded is null || document.ContentVersion is null) return new BuildSetLoadResult(report, null);

            var builds = new List<ReferenceBuild>();
            for (int i = 0; i < set!.Builds!.Count; i++)
            {
                ReferenceBuildData b = set.Builds[i]!;
                string path = $"$.data.builds[{i}].build";
                CharacterBuild build = BuildShape.ToCharacterBuild(loaded.Definition.Id, b.Build!);
                foreach (BuildViolation v in BuildValidator.Validate(build, loaded.Definition, data.Rules.Progression))
                    report.Add(path, v.Code is BuildViolationCode.UnknownTalent or BuildViolationCode.UnknownSkill ? ValidationCode.UnknownReference : ValidationCode.InvalidValue,
                        $"build invalide ({v.Subject}) : {v.Message} [{v.Code}]");
                builds.Add(new ReferenceBuild(b.Id!, b.Name!, Categories[b.Category!], build));
            }
            return new BuildSetLoadResult(report,
                report.IsValid ? new LoadedBuildSet(document.SchemaVersion!.Value, document.ContentVersion, loaded.Definition.Id, builds) : null);
        }

        private static void CheckBuilds(List<ReferenceBuildData?>? builds, ValidationReport report)
        {
            const string path = "$.data.builds";
            if (builds is null)
            {
                if (!report.HasErrorAtOrAbove(path)) report.Add(path, ValidationCode.MissingField, "champ obligatoire absent (liste des builds de référence).");
                return;
            }
            if (builds.Count == 0)
            {
                report.Add(path, ValidationCode.InvalidValue, "au moins un build attendu.");
                return;
            }
            if (builds.Count > MaxBuilds)
            {
                report.Add(path, ValidationCode.LimitExceeded, $"{builds.Count} builds, au plus {MaxBuilds} par fichier.");
                return;
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < builds.Count; i++)
            {
                string p = $"{path}[{i}]";
                if (builds[i] is not { } b)
                {
                    if (!report.HasErrorAtOrAbove(p)) report.Add(p, ValidationCode.WrongType, "valeur attendue.");
                    continue;
                }
                if (Present(b.Id, p + ".id", "identifiant stable « build.… »", report))
                {
                    if (!StableId.HasPrefix(b.Id, "build"))
                        report.Add(p + ".id", ValidationCode.InvalidValue, $"identifiant « {b.Id} » invalide : « build.<classe>.<nom> » attendu (minuscules, chiffres, « _ »).");
                    else if (!seen.Add(b.Id!))
                        report.Add(p + ".id", ValidationCode.DuplicateId, $"build « {b.Id} » déjà défini dans ce fichier.");
                }
                if (Present(b.Name, p + ".name", "nom affiché", report) && b.Name!.Trim().Length == 0)
                    report.Add(p + ".name", ValidationCode.InvalidValue, "nom vide.");
                if (Present(b.Category, p + ".category", string.Join(" ou ", Categories.Keys), report) && !Categories.ContainsKey(b.Category!))
                    report.Add(p + ".category", ValidationCode.UnknownReference,
                        $"valeur inconnue « {b.Category} » ; valeurs possibles : {string.Join(", ", Categories.Keys)}.");
                if (Present(b.Build, p + ".build", "build du personnage", report))
                    BuildShape.Check(b.Build!, p + ".build", report);
            }
        }

        private static bool Present(object? value, string path, string description, ValidationReport report)
        {
            if (value != null) return true;
            if (!report.HasErrorAtOrAbove(path))
                report.Add(path, ValidationCode.MissingField, $"champ obligatoire absent ({description}).");
            return false;
        }
    }
}
