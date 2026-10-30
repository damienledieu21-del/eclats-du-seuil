using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Stats;

namespace EdS.Domain.Builds
{
    /// <summary>Raison stable d'un refus de build.</summary>
    public enum BuildViolationCode
    {
        WrongClass,
        LevelOutOfRange,
        UnknownTalent,
        RankOutOfRange,
        TooManyPoints,
        PrerequisiteNotMet,
        MajorThresholdNotMet,
        TooManyMajorTalents,
        UnknownSkill,
        WrongSlotKind,
        DuplicateSkill,
        TooManySkills,
        SkillLevelTooLow,
        SkillRequirementNotMet,
        UltimateLocked,

        /// <summary>Règles de progression incohérentes : le build ne peut pas être jugé (correctif 6.1).</summary>
        InvalidProgressionRules,

        /// <summary>Attribut hors de [0, <see cref="BuildValidator.MaxAttributeValue"/>] (correctif 6.1).</summary>
        AttributeOutOfRange,

        /// <summary>
        /// Sous-lot V1.5 (écran des compétences) : compétence pas encore apprise par le personnage. Jugé par la partie (EdS.World, qui
        /// connaît la progression), jamais par ce validateur, qui ne connaît que la classe et les règles.
        /// </summary>
        SkillNotLearned,

        /// <summary>
        /// Sous-lot V1.5 : une place ouverte de la barre reste vide alors qu'une compétence apprise de sa sorte attend. Plus jamais rendu
        /// depuis la réponse de Damien du 30 octobre 2026 (une place peut rester vide) ; gardé pour ne pas déplacer les valeurs du code.
        /// </summary>
        SlotLeftEmpty,
    }

    /// <summary>Une raison de refus, rattachée à l'élément concerné (talent, compétence, emplacement).</summary>
    public sealed class BuildViolation
    {
        public BuildViolationCode Code { get; }

        /// <summary>Élément concerné : identifiant de talent ou de compétence, ou nom d'emplacement (« level », « activeSkills[2] »).</summary>
        public string Subject { get; }

        public string Message { get; }

        public BuildViolation(BuildViolationCode code, string subject, string message)
        {
            Code = code;
            Subject = subject;
            Message = message;
        }

        public override string ToString() => $"{Subject} : {Message} [{Code}]";
    }

    /// <summary>
    /// Validateur de builds, générique : il ne connaît aucune classe en particulier.
    /// Toutes les erreurs sont rapportées ; un build est valide si la liste est vide.
    /// La validation est statique : on vérifie l'allocation finale, en tenant compte de l'ordre d'acquisition
    /// imposé par les prérequis (un talent qui dépend d'un majeur ne compte pas dans le seuil de ce majeur).
    /// </summary>
    public static class BuildValidator
    {
        /// <summary>
        /// Plafond de chaque attribut (correctif 6.1, M03). Très au-delà du jeu prévu, il garantit que les statistiques
        /// dérivées restent calculables ; au-delà, le build est refusé avec un diagnostic, jamais par une exception.
        /// </summary>
        public const int MaxAttributeValue = 1_000_000;

        /// <summary>
        /// Toutes les raisons de refus. Aucune entrée (rangs, niveau, attributs, règles de progression), même extrême
        /// (<c>int.MinValue</c>, <c>int.MaxValue</c>), ne provoque d'exception : les sommes sont calculées sur 64 bits.
        /// </summary>
        public static IReadOnlyList<BuildViolation> Validate(CharacterBuild build, ClassDefinition definition, ProgressionRules progression)
        {
            if (build is null) throw new ArgumentNullException(nameof(build));
            if (definition is null) throw new ArgumentNullException(nameof(definition));
            if (progression is null) throw new ArgumentNullException(nameof(progression));

            var errors = new List<BuildViolation>();
            IReadOnlyList<RuleViolation> rules = progression.Validate();
            if (rules.Count > 0)
            {
                errors.Add(new BuildViolation(BuildViolationCode.InvalidProgressionRules, "progression",
                    "règles de progression incohérentes : " + string.Join(" ; ", rules.Select(r => r.ToString())) + "."));
                return errors;   // niveaux, points et emplacements en dépendent
            }
            if (build.ClassId != definition.Id)
                errors.Add(new BuildViolation(BuildViolationCode.WrongClass, "classId",
                    $"build de la classe « {build.ClassId} » validé avec la définition « {definition.Id} »."));

            if (!progression.IsValidLevel(build.Level))
            {
                errors.Add(new BuildViolation(BuildViolationCode.LevelOutOfRange, "level",
                    $"niveau {build.Level} hors de [1, {progression.MaxLevel}]."));
                return errors;   // les limites suivantes dépendent du niveau
            }

            foreach (AttributeKind kind in (AttributeKind[])Enum.GetValues(typeof(AttributeKind)))
                if (build.Attributes[kind] > MaxAttributeValue)
                    errors.Add(new BuildViolation(BuildViolationCode.AttributeOutOfRange, "attributes." + kind.ToString().ToLowerInvariant(),
                        $"attribut {build.Attributes[kind]} hors de [0, {MaxAttributeValue}]."));

            ValidateTalents(build, definition, progression, errors);
            ValidateSkills(build, definition, progression, errors);
            return errors;
        }

        public static bool IsValid(CharacterBuild build, ClassDefinition definition, ProgressionRules progression) =>
            Validate(build, definition, progression).Count == 0;

        /// <summary>Points investis dans une branche (somme des rangs positifs de ses talents, sur 64 bits : jamais de dépassement).</summary>
        public static long PointsInTree(CharacterBuild build, TalentTreeDefinition tree) =>
            tree.Talents.Sum(t => build.TalentRanks.TryGetValue(t.Id, out int rank) && rank > 0 ? (long)rank : 0L);

        private static int Rank(CharacterBuild build, string talentId) =>
            build.TalentRanks.TryGetValue(talentId, out int rank) ? rank : 0;

        private static void ValidateTalents(CharacterBuild build, ClassDefinition definition, ProgressionRules progression, List<BuildViolation> errors)
        {
            long spent = 0;
            var majors = new List<TalentDefinition>();

            foreach (var entry in build.TalentRanks.OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                if (!definition.TryGetTalent(entry.Key, out TalentDefinition talent, out TalentTreeDefinition tree))
                {
                    errors.Add(new BuildViolation(BuildViolationCode.UnknownTalent, entry.Key, "talent inconnu pour cette classe."));
                    continue;
                }
                if (entry.Value < 1 || entry.Value > talent.MaxRank)
                {
                    errors.Add(new BuildViolation(BuildViolationCode.RankOutOfRange, entry.Key,
                        $"rang {entry.Value} hors de [1, {talent.MaxRank}] pour « {talent.Name} »."));
                    if (entry.Value < 1) continue;
                }
                spent += entry.Value;

                foreach (TalentRequirement p in talent.Prerequisites)
                    if (Rank(build, p.TalentId) < p.MinRank)
                        errors.Add(new BuildViolation(BuildViolationCode.PrerequisiteNotMet, entry.Key,
                            $"« {talent.Name} » exige {Describe(definition, p)} (rang actuel {Rank(build, p.TalentId)})."));

                if (talent.IsMajor)
                {
                    majors.Add(talent);
                    ISet<string> dependents = definition.DependentsOf(talent.Id);
                    long invested = tree.Talents
                        .Where(t => t.Id != talent.Id && !dependents.Contains(t.Id))
                        .Sum(t => (long)Math.Max(0, Rank(build, t.Id)));
                    if (invested < progression.MajorTalentThreshold)
                        errors.Add(new BuildViolation(BuildViolationCode.MajorThresholdNotMet, entry.Key,
                            $"talent majeur « {talent.Name} » : {invested} point(s) investi(s) dans « {tree.Name} », {progression.MajorTalentThreshold} requis avant de l'acquérir."));
                }
            }

            long available = progression.TalentPoints(build.Level);   // règles validées : borné
            if (spent > available)
                errors.Add(new BuildViolation(BuildViolationCode.TooManyPoints, "talents",
                    $"{spent} point(s) dépensé(s) pour {available} disponible(s) au niveau {build.Level}."));

            if (majors.Count > progression.MaxMajorTalents)
                errors.Add(new BuildViolation(BuildViolationCode.TooManyMajorTalents, "talents",
                    $"{majors.Count} talents majeurs ({string.Join(", ", majors.Select(m => m.Name))}) ; {progression.MaxMajorTalents} au plus."));
        }

        private static void ValidateSkills(CharacterBuild build, ClassDefinition definition, ProgressionRules progression, List<BuildViolation> errors)
        {
            var equipped = new HashSet<string>(StringComparer.Ordinal);

            void Check(string skillId, SkillKind slotKind, string slot)
            {
                if (!equipped.Add(skillId))
                {
                    errors.Add(new BuildViolation(BuildViolationCode.DuplicateSkill, slot, $"« {skillId} » est déjà équipée dans un autre emplacement."));
                    return;
                }
                if (!definition.TryGetSkill(skillId, out SkillDefinition skill))
                {
                    errors.Add(new BuildViolation(BuildViolationCode.UnknownSkill, slot, $"compétence inconnue « {skillId} »."));
                    return;
                }
                if (skill.Kind != slotKind)
                    errors.Add(new BuildViolation(BuildViolationCode.WrongSlotKind, slot,
                        $"« {skill.Name} » est une compétence {Label(skill.Kind)}, l'emplacement attend une compétence {Label(slotKind)}."));
                if (build.Level < skill.MinLevel)
                    errors.Add(new BuildViolation(BuildViolationCode.SkillLevelTooLow, slot,
                        $"« {skill.Name} » exige le niveau {skill.MinLevel} (niveau actuel {build.Level})."));
                foreach (TalentRequirement r in skill.TalentRequirements)
                    if (Rank(build, r.TalentId) < r.MinRank)
                        errors.Add(new BuildViolation(BuildViolationCode.SkillRequirementNotMet, slot,
                            $"« {skill.Name} » exige {Describe(definition, r)}."));
            }

            int activeSlots = progression.ActiveSlots(build.Level);
            if (build.ActiveSkills.Count > activeSlots)
                errors.Add(new BuildViolation(BuildViolationCode.TooManySkills, "activeSkills",
                    $"{build.ActiveSkills.Count} compétences actives pour {activeSlots} emplacement(s) au niveau {build.Level}."));
            for (int i = 0; i < build.ActiveSkills.Count; i++)
                Check(build.ActiveSkills[i], SkillKind.Active, $"activeSkills[{i}]");

            if (build.Ultimate != null)
            {
                if (!progression.IsUltimateUnlocked(build.Level))
                    errors.Add(new BuildViolation(BuildViolationCode.UltimateLocked, "ultimate",
                        $"l'emplacement d'ultime s'ouvre au niveau {progression.UltimateUnlockLevel} (niveau actuel {build.Level})."));
                Check(build.Ultimate, SkillKind.Ultimate, "ultimate");
            }

            int passiveSlots = progression.PassiveSlots(build.Level);
            if (build.PassiveSkills.Count > passiveSlots)
                errors.Add(new BuildViolation(BuildViolationCode.TooManySkills, "passiveSkills",
                    $"{build.PassiveSkills.Count} passifs pour {passiveSlots} emplacement(s) au niveau {build.Level}."));
            for (int i = 0; i < build.PassiveSkills.Count; i++)
                Check(build.PassiveSkills[i], SkillKind.Passive, $"passiveSkills[{i}]");
        }

        private static string Describe(ClassDefinition definition, TalentRequirement r) =>
            definition.TryGetTalent(r.TalentId, out TalentDefinition t, out _) ? $"« {t.Name} » rang {r.MinRank}" : $"« {r.TalentId} » rang {r.MinRank}";

        private static string Label(SkillKind kind) => kind switch
        {
            SkillKind.Active => "active",
            SkillKind.Ultimate => "ultime",
            _ => "passive",
        };
    }
}
