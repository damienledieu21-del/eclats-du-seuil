using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Builds;
using EdS.Data.Classes;
using EdS.Data.Saves;
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

        /// <summary>Sous-lot 12.9b : compétences gagnées en montant de niveau, dans l'ordre des niveaux (ordre du fichier à niveau égal).</summary>
        public IReadOnlyList<SkillUnlock> SkillUnlocks { get; }

        internal ClassProgression(string classId, IEnumerable<Attributes> attributes, IEnumerable<string> actives, IEnumerable<string> passives,
            IEnumerable<SkillUnlock> unlocks)
        {
            ClassId = classId;
            Attributes = Frozen.List(attributes, nameof(attributes));
            StartActiveSkills = Frozen.List(actives, nameof(actives));
            StartPassiveSkills = Frozen.List(passives, nameof(passives));
            SkillUnlocks = Frozen.List(unlocks, nameof(unlocks));
        }

        /// <summary>Attributs de base au niveau <paramref name="level"/> (1 à <see cref="CharacterProgression.MaxLevel"/>).</summary>
        public Attributes At(int level) =>
            level >= 1 && level <= Attributes.Count ? Attributes[level - 1] : throw new ArgumentOutOfRangeException(nameof(level));
    }

    /// <summary>Sous-lot 12.9b : une compétence de la classe gagnée au niveau <see cref="Level"/>, avec sa sorte (active, ultime, passive).</summary>
    public sealed class SkillUnlock
    {
        public int Level { get; }
        public string Skill { get; }
        public SkillKind Kind { get; }

        /// <summary>
        /// Réponse de Damien du 30 octobre 2026 : la compétence vient avec sa place, et la halte l'y met d'elle-même (12.9b) ; sinon, elle va
        /// dans la réserve : le joueur la place lui-même avec l'écran des compétences. Sous-lot V1.8 (barre « 4 + ultime dès le début ») :
        /// « avec sa place » se lit « une place de sa sorte est ouverte à son niveau et pas encore promise » : les places ouvertes à ce
        /// niveau (règles) sont plus nombreuses que les compétences de départ de cette sorte et celles gagnées plus tôt avec leur place
        /// (<see cref="HasFreePlace"/>). Avec les règles d'avant (places aux niveaux 1, 1, 5, 10, ultime au 10), cela donne exactement la
        /// réponse de Damien (Barrière d'acier au niveau 5, Onde de rupture et Bastion absolu au niveau 10 ; Provocation et Contrecoup en
        /// réserve). Avec celles de V1.8 (1, 1, 1, 1, 10, 20, ultime au 1) : Provocation (niveau 3) et Barrière d'acier (5) prennent les
        /// places 3 et 4, Onde de rupture (10) la 5e qui s'ouvre avec elle, Bastion absolu (10) la place de l'ultime ; Contrecoup (7) va
        /// dans la réserve (barre pleine), et la 6e place (niveau 20) l'attend.
        /// </summary>
        public bool WithPlace { get; }

        internal SkillUnlock(int level, string skill, SkillKind kind, bool withPlace)
        {
            Level = level;
            Skill = skill;
            Kind = kind;
            WithPlace = withPlace;
        }

        /// <summary>
        /// Réponse de Damien du 30 octobre 2026 (une place peut rester vide) : clé du registre du monde (événements consommés, gardé dans la
        /// sauvegarde) qui dit que la halte a déjà donné cette compétence (placée, ou en réserve faute de place) ou que le joueur a changé sa
        /// barre après l'avoir vue. La halte ne la remet plus d'office : une place vidée reste vide. Une sauvegarde a un seul personnage.
        /// </summary>
        public string GivenKey => GivenKeyPrefix + Skill;

        /// <summary>Début des clés <see cref="GivenKey"/> (« character/skill/skill.bastion.barriere_d_acier »).</summary>
        public const string GivenKeyPrefix = "character/skill/";

        /// <summary>
        /// Sous-lot V1.8 : reste-t-il une place de la sorte <paramref name="kind"/> ouverte au niveau <paramref name="level"/> (règles) quand
        /// <paramref name="promised"/> places de cette sorte sont déjà promises (compétences de départ et gagnées plus tôt avec leur place) ?
        /// </summary>
        public static bool HasFreePlace(SkillKind kind, int level, int promised, ProgressionRules rules)
        {
            if (rules is null) throw new ArgumentNullException(nameof(rules));
            if (promised < 0) throw new ArgumentOutOfRangeException(nameof(promised));
            return kind switch
            {
                SkillKind.Active => rules.ActiveSlotLevels.Count(l => l <= level) > promised,
                SkillKind.Passive => rules.PassiveSlotLevels.Count(l => l <= level) > promised,
                SkillKind.Ultimate => level >= rules.UltimateUnlockLevel && promised == 0,
                _ => false,
            };
        }
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
        /// <summary>Niveau le plus haut décrit par les données (sous-lot V1.8 : 30, provisoire au-delà du plafond).</summary>
        public int MaxLevel { get; }

        /// <summary>
        /// Sous-lot V1.8 (sujet 54, « plafond qui monte ») : niveau le plus haut qu'un personnage atteint aujourd'hui (25 dans les Vallons, en
        /// alpha), au plus <see cref="MaxLevel"/>. L'expérience continue de s'accumuler au plafond : quand il montera, elle comptera.
        /// </summary>
        public int LevelCap { get; }

        /// <summary>Sous-lot V1.8 : premier niveau de chaque chapitre de la quête principale (1, 6, 11, 16, 21), strictement croissant.</summary>
        public IReadOnlyList<int> ChapterStartLevels { get; }

        /// <summary>Expérience cumulée qu'il faut pour être à chaque niveau ; indice 0 : niveau 1 (0).</summary>
        public IReadOnlyList<long> XpToLevel { get; }

        public long KillXpPerHealth { get; }

        /// <summary>Classes, par identifiant.</summary>
        public IReadOnlyList<ClassProgression> Classes { get; }

        internal CharacterProgression(int schemaVersion, string contentVersion, int maxLevel, int levelCap, IEnumerable<int> chapterStartLevels,
            IEnumerable<long> xpToLevel, long killXpPerHealth, IEnumerable<ClassProgression> classes)
        {
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion;
            MaxLevel = maxLevel;
            LevelCap = levelCap;
            ChapterStartLevels = Frozen.List(chapterStartLevels, nameof(chapterStartLevels));
            XpToLevel = Frozen.List(xpToLevel, nameof(xpToLevel));
            KillXpPerHealth = killXpPerHealth;
            Classes = Frozen.List(classes.OrderBy(c => c.ClassId, StringComparer.Ordinal), nameof(classes));
        }

        public ClassProgression ForClass(string classId) =>
            Classes.FirstOrDefault(c => c.ClassId == classId) ?? throw new ArgumentException($"Classe « {classId} » sans progression.", nameof(classId));

        /// <summary>Niveau atteint avec <paramref name="xp"/> d'expérience cumulée (au plus <see cref="LevelCap"/>, sous-lot V1.8).</summary>
        public int LevelFor(long xp)
        {
            if (xp < 0) throw new ArgumentOutOfRangeException(nameof(xp));
            int level = 1;
            while (level < LevelCap && xp >= XpToLevel[level]) level++;
            return level;
        }

        /// <summary>Sous-lot V1.8 : chapitre (1 à <see cref="ChapterStartLevels"/>.Count) d'un niveau de 1 à <see cref="MaxLevel"/>.</summary>
        public int ChapterOf(int level)
        {
            if (level < 1 || level > MaxLevel) throw new ArgumentOutOfRangeException(nameof(level));
            int chapter = 1;
            while (chapter < ChapterStartLevels.Count && level >= ChapterStartLevels[chapter]) chapter++;
            return chapter;
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

        /// <summary>
        /// Version 1 (sous-lot 12.2) : <c>maxLevel</c>, <c>xpToLevel</c>, <c>killXpPerHealth</c>, <c>classes[]</c>. Version 2 (sous-lot 12.9b) :
        /// <c>classes[].skillUnlocks</c> (compétences gagnées en montant de niveau). Version 3 (sous-lot V1.8) : <c>levelCap</c> (plafond de
        /// niveau actuel, sujet 54) et <c>chapterStartLevels</c> (premier niveau de chaque chapitre).
        /// </summary>
        public const int CurrentSchemaVersion = 3;

        /// <summary>Sous-lot V1.8 : au plus 64 chapitres.</summary>
        public const int MaxChapters = 64;

        /// <summary>Au plus 64 compétences gagnées par classe.</summary>
        public const int MaxSkillUnlocks = 64;

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
            // Sous-lot V1.8 : plafond actuel et chapitres.
            if (d.LevelCap is null) Missing(report, "$.data.levelCap", "plafond de niveau actuel");
            else if (max > 0 && (d.LevelCap < 1 || d.LevelCap > max))
                report.Add("$.data.levelCap", ValidationCode.OutOfRange, $"plafond hors de [1, {max}] (niveau maximal des données).");
            int cap = d.LevelCap is int lc && lc >= 1 && lc <= max ? lc : 0;
            if (d.ChapterStartLevels is null) Missing(report, "$.data.chapterStartLevels", "premier niveau de chaque chapitre");
            else if (d.ChapterStartLevels.Count == 0) report.Add("$.data.chapterStartLevels", ValidationCode.InvalidValue, "au moins un chapitre.");
            else
                for (int i = 0; i < d.ChapterStartLevels.Count; i++)
                {
                    string path = $"$.data.chapterStartLevels[{i}]";
                    if (d.ChapterStartLevels[i] is not int start) { Missing(report, path, "premier niveau du chapitre"); continue; }
                    if (i == 0 && start != 1) report.Add(path, ValidationCode.InvalidValue, "le premier chapitre commence au niveau 1.");
                    else if (cap > 0 && (start < 1 || start > cap)) report.Add(path, ValidationCode.OutOfRange, $"niveau hors de [1, {cap}] (plafond).");
                    else if (i > 0 && d.ChapterStartLevels[i - 1] is int before && start <= before)
                        report.Add(path, ValidationCode.InvalidValue, "les chapitres commencent à des niveaux strictement croissants.");
                }
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
                    if (CheckClass(c, path, max, cap, data, seen, report) is { } loaded) classes.Add(loaded);
                }
                // Revue Codex de la PR #43 : chaque classe chargée a sa progression ; sinon l'erreur ne se verrait qu'à la création d'un personnage.
                foreach (LoadedClass loaded in data.Classes)
                    if (!seen.Contains(loaded.Definition.Id))
                        report.Add("$.data.classes", ValidationCode.MissingField, $"progression absente pour la classe « {loaded.Definition.Id} ».");
            }
            if (!report.IsValid || document.ContentVersion is null || max == 0 || cap == 0) return new ProgressionLoadResult(report, null);
            return new ProgressionLoadResult(report, new CharacterProgression(document.SchemaVersion!.Value, document.ContentVersion, max, cap,
                d.ChapterStartLevels!.Select(x => x!.Value), d.XpToLevel!.Select(x => x!.Value), d.KillXpPerHealth!.Value, classes));
        }

        private static ClassProgression? CheckClass(ClassProgressionData c, string path, int max, int cap, GameData data, HashSet<string> seen, ValidationReport report)
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
            if (c.SkillUnlocks is null) Missing(report, path + ".skillUnlocks", "compétences gagnées en montant de niveau, éventuellement aucune");
            if (loaded is null || !report.IsValid || levels.Count == 0) return null;
            // Le build de départ doit être valide au niveau 1 (emplacements, compétences de la classe).
            var start = new CharacterBuild(loaded.Definition.Id, 1, levels[0], new Dictionary<string, int>(),
                c.StartActiveSkills!.Select(s => s!).ToArray(), null, c.StartPassiveSkills!.Select(s => s!).ToArray());
            foreach (BuildViolation v in BuildValidator.Validate(start, loaded.Definition, data.Rules.Progression))
                report.Add(v.Subject.StartsWith("activeSkills", StringComparison.Ordinal) ? path + ".startActiveSkills"
                    : v.Subject.StartsWith("passiveSkills", StringComparison.Ordinal) ? path + ".startPassiveSkills" : path,
                    ValidationCode.InvalidValue, $"build de départ invalide au niveau 1 ({v.Subject}) : {v.Message} [{v.Code}]");
            if (!report.IsValid) return null;
            List<SkillUnlock>? unlocks = CheckUnlocks(c, path + ".skillUnlocks", loaded, levels, cap, data, report);
            return unlocks != null && report.IsValid
                ? new ClassProgression(loaded.Definition.Id, levels, c.StartActiveSkills!.Select(s => s!), c.StartPassiveSkills!.Select(s => s!), unlocks)
                : null;
        }

        /// <summary>
        /// Sous-lot 12.9b : chaque compétence gagnée est une compétence de la classe, pas déjà connue (départ ou gagnée plus tôt), à un niveau de
        /// 2 au niveau maximal, au moins son niveau minimal, les niveaux dans l'ordre ; à chaque niveau, le build de départ et tout ce qui est
        /// gagné jusque-là <b>avec sa place</b> (<see cref="SkillUnlock.WithPlace"/>) doit être valide pour le validateur des builds
        /// (emplacements ouverts, sans talent). Réponse de Damien du 30 octobre 2026 : une compétence gagnée à un niveau où aucune place de sa
        /// sorte ne s'ouvre va dans la réserve ; elle n'a pas à tenir dans la barre.
        /// Relecture indépendante de la PR #82 : à un même niveau, les compétences sont rangées de façon déterministe (par sorte : active,
        /// ultime, passive ; puis par identifiant, ordre ordinal) avant de leur donner les places libres, quel que soit leur ordre dans le
        /// fichier ; <see cref="ClassProgression.SkillUnlocks"/> garde cet ordre. Une compétence sans place libre va dans la réserve
        /// (<see cref="SkillUnlock.WithPlace"/> faux) : c'est permis, avec un avertissement (le joueur devra la placer lui-même). Une
        /// compétence gagnée au-dessus du plafond de niveau (<paramref name="cap"/>) reçoit aussi un avertissement : personne ne l'apprend
        /// tant que le plafond ne monte pas. Comme les places sont données dans cet ordre, le build de chaque niveau tient toujours dans la
        /// barre (contrôle gardé par sûreté).
        /// </summary>
        private static List<SkillUnlock>? CheckUnlocks(ClassProgressionData c, string path, LoadedClass loaded, List<Attributes> levels, int cap, GameData data,
            ValidationReport report)
        {
            var known = new HashSet<string>(c.StartActiveSkills!.Select(s => s!).Concat(c.StartPassiveSkills!.Select(s => s!)), StringComparer.Ordinal);
            var unlocks = new List<SkillUnlock>();
            var candidates = new List<(int Level, string Skill, SkillKind Kind, string Path)>();
            int previous = 1;
            // Sous-lot V1.8 : places déjà promises par sorte (départ, puis compétences gagnées avec leur place).
            var promised = new Dictionary<SkillKind, int>
            {
                [SkillKind.Active] = c.StartActiveSkills!.Count,
                [SkillKind.Passive] = c.StartPassiveSkills!.Count,
                [SkillKind.Ultimate] = 0,
            };
            for (int i = 0; i < c.SkillUnlocks!.Count; i++)
            {
                string at = $"{path}[{i}]";
                if (c.SkillUnlocks[i] is not SkillUnlockData u) { Missing(report, at, "niveau et compétence"); continue; }
                if (u.Level is null) Missing(report, at + ".level", "niveau où la compétence est gagnée");
                if (u.Skill is null) Missing(report, at + ".skill", "identifiant de compétence");
                if (u.Level is null || u.Skill is null) continue;
                int lvl = u.Level.Value;
                if (lvl < 2 || lvl > levels.Count)
                {
                    report.Add(at + ".level", ValidationCode.OutOfRange, $"niveau hors de [2, {levels.Count}] (le niveau 1 est le build de départ).");
                    continue;
                }
                if (lvl < previous)
                {
                    report.Add(at + ".level", ValidationCode.InvalidValue, "les compétences gagnées suivent l'ordre des niveaux.");
                    continue;
                }
                previous = lvl;
                if (!loaded.Definition.TryGetSkill(u.Skill, out SkillDefinition skill))
                {
                    report.Add(at + ".skill", ValidationCode.UnknownReference, $"compétence inconnue de la classe « {u.Skill} ».");
                    continue;
                }
                if (!known.Add(u.Skill))
                {
                    report.Add(at + ".skill", ValidationCode.DuplicateId, $"compétence « {u.Skill} » déjà connue (départ ou gagnée plus tôt).");
                    continue;
                }
                if (lvl < skill.MinLevel)
                {
                    report.Add(at + ".level", ValidationCode.InvalidValue, $"« {u.Skill} » demande le niveau {skill.MinLevel}.");
                    continue;
                }
                candidates.Add((lvl, u.Skill, skill.Kind, at));
            }
            if (!report.IsValid) return null;
            // Même niveau : par sorte, puis par identifiant (ordre déterministe des places données).
            candidates = candidates.OrderBy(x => x.Level).ThenBy(x => (int)x.Kind).ThenBy(x => x.Skill, StringComparer.Ordinal).ToList();
            foreach ((int lvl, string id, SkillKind kind, string at) in candidates)
            {
                promised.TryGetValue(kind, out int taken);
                var unlock = new SkillUnlock(lvl, id, kind, SkillUnlock.HasFreePlace(kind, lvl, taken, data.Rules.Progression));
                if (unlock.WithPlace) promised[kind] = taken + 1;
                if (unlock.WithPlace && !SaveValidator.IsEventKey(unlock.GivenKey))
                {
                    report.Add(at + ".skill", ValidationCode.InvalidValue, $"identifiant trop long ou hors format pour la clé du registre « {unlock.GivenKey} ».");
                    continue;
                }
                if (!unlock.WithPlace)
                    report.AddWarning(at + ".skill", ValidationCode.InvalidValue,
                        $"« {id} » arrive au niveau {lvl} sans place libre de sa sorte : elle va dans la réserve (le joueur la place lui-même).");
                if (cap > 0 && lvl > cap)
                    report.AddWarning(at + ".level", ValidationCode.OutOfRange,
                        $"« {id} » se gagne au niveau {lvl}, au-dessus du plafond actuel ({cap}) : personne ne l'apprend tant que le plafond ne monte pas.");
                unlocks.Add(unlock);
            }
            if (!report.IsValid) return null;
            for (int level = 2; level <= levels.Count; level++)
            {
                var gained = unlocks.Where(u => u.WithPlace && u.Level <= level).ToList();   // la réserve n'a pas à tenir dans la barre
                if (gained.Count == 0 || gained.All(u => u.Level < level)) continue;   // rien de nouveau : déjà vérifié au niveau d'avant
                string? ultimate = gained.Where(u => u.Kind == SkillKind.Ultimate).Select(u => u.Skill).FirstOrDefault();
                if (gained.Count(u => u.Kind == SkillKind.Ultimate) > 1)
                {
                    report.Add(path, ValidationCode.InvalidValue, $"plus d'une ultime gagnée au niveau {level} (un seul emplacement d'ultime).");
                    return null;
                }
                var build = new CharacterBuild(loaded.Definition.Id, level, levels[level - 1], new Dictionary<string, int>(),
                    c.StartActiveSkills!.Select(s => s!).Concat(gained.Where(u => u.Kind == SkillKind.Active).Select(u => u.Skill)).ToArray(), ultimate,
                    c.StartPassiveSkills!.Select(s => s!).Concat(gained.Where(u => u.Kind == SkillKind.Passive).Select(u => u.Skill)).ToArray());
                foreach (BuildViolation v in BuildValidator.Validate(build, loaded.Definition, data.Rules.Progression))
                    report.Add(path, ValidationCode.InvalidValue, $"build invalide au niveau {level} avec les compétences gagnées ({v.Subject}) : {v.Message} [{v.Code}]");
                if (!report.IsValid) return null;
            }
            return unlocks;
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
