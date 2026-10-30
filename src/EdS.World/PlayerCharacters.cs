using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data;
using EdS.Data.Characters;
using EdS.Data.Classes;
using EdS.Data.Saves;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Space;
using EdS.Domain.Stats;

namespace EdS.World
{
    /// <summary>
    /// Sous-lot 12.2 (cadrage Q3 et Q20) : le personnage persistant. Son niveau se déduit de son expérience, ses attributs de sa classe et de
    /// son niveau (table de la progression, pas de répartition manuelle) ; son build (talents, compétences) est gardé tel quel et validé contre
    /// la classe et le niveau par le validateur unique des builds.
    /// </summary>
    public static class PlayerCharacters
    {
        /// <summary>Nouveau personnage de niveau 1, avec les compétences de départ de sa classe.</summary>
        public static SavedCharacter Create(string id, string classId, CharacterProgression progression)
        {
            if (progression is null) throw new ArgumentNullException(nameof(progression));
            ClassProgression p = progression.ForClass(classId);
            return new SavedCharacter(id, classId, 0, new Dictionary<string, int>(), p.StartActiveSkills, null, p.StartPassiveSkills);
        }

        /// <summary>
        /// Sous-lot 12.9b (essai du 25 octobre 2026, choix de Damien) : équipe les compétences gagnées jusqu'au niveau du personnage
        /// (<see cref="ClassProgression.SkillUnlocks"/>) qu'il n'a pas encore, chacune s'il lui reste un emplacement de sa sorte ouvert à ce niveau
        /// (règles : <see cref="ProgressionRules.ActiveSlotLevels"/>, <see cref="ProgressionRules.PassiveSlotLevels"/>,
        /// <see cref="ProgressionRules.UltimateUnlockLevel"/>). Une active prend l'emplacement libre suivant. Rend le personnage tel quel s'il n'y
        /// a rien à gagner ; <paramref name="learned"/> dit ce qui a été équipé, dans l'ordre de la progression.
        /// </summary>
        public static SavedCharacter Learn(SavedCharacter character, CharacterProgression progression, ProgressionRules rules,
            out IReadOnlyList<SkillUnlock> learned)
        {
            if (character is null) throw new ArgumentNullException(nameof(character));
            if (progression is null) throw new ArgumentNullException(nameof(progression));
            if (rules is null) throw new ArgumentNullException(nameof(rules));
            int level = Level(character, progression);
            var actives = character.ActiveSkills.ToList();
            var passives = character.PassiveSkills.ToList();
            string? ultimate = character.Ultimate;
            int activeSlots = rules.ActiveSlotLevels.Count(l => l <= level), passiveSlots = rules.PassiveSlotLevels.Count(l => l <= level);
            var gained = new List<SkillUnlock>();
            foreach (SkillUnlock u in progression.ForClass(character.ClassId).SkillUnlocks)
            {
                if (u.Level > level || actives.Contains(u.Skill) || passives.Contains(u.Skill) || ultimate == u.Skill) continue;
                switch (u.Kind)
                {
                    case SkillKind.Active when actives.Count < activeSlots:
                        actives.Add(u.Skill);
                        break;
                    case SkillKind.Passive when passives.Count < passiveSlots:
                        passives.Add(u.Skill);
                        break;
                    case SkillKind.Ultimate when ultimate is null && level >= rules.UltimateUnlockLevel:
                        ultimate = u.Skill;
                        break;
                    default:
                        continue;   // emplacement pris (choix futur du joueur) : la compétence attend
                }
                gained.Add(u);
            }
            learned = gained;
            return gained.Count == 0 ? character : character.WithSkills(actives, ultimate, passives);
        }

        /// <summary>
        /// Sous-lot V1.5 (écran des compétences) : compétences apprises par le personnage à son niveau actuel (d'après son expérience) : celles
        /// de départ de sa classe (actives puis passives), puis celles gagnées jusqu'à son niveau (<see cref="ClassProgression.SkillUnlocks"/>),
        /// dans l'ordre de la progression, sans doublon.
        /// </summary>
        public static IReadOnlyList<string> Learned(SavedCharacter character, CharacterProgression progression)
        {
            if (character is null) throw new ArgumentNullException(nameof(character));
            if (progression is null) throw new ArgumentNullException(nameof(progression));
            int level = Level(character, progression);
            ClassProgression p = progression.ForClass(character.ClassId);
            var learned = new List<string>();
            foreach (string skill in p.StartActiveSkills.Concat(p.StartPassiveSkills).Concat(p.SkillUnlocks.Where(u => u.Level <= level).Select(u => u.Skill)))
                if (!learned.Contains(skill)) learned.Add(skill);
            return learned;
        }

        /// <summary>Sous-lot V1.5 : compétences que le personnage gagnera plus haut (au-delà de son niveau actuel), dans l'ordre de la progression.</summary>
        public static IReadOnlyList<SkillUnlock> Upcoming(SavedCharacter character, CharacterProgression progression)
        {
            if (character is null) throw new ArgumentNullException(nameof(character));
            if (progression is null) throw new ArgumentNullException(nameof(progression));
            int level = Level(character, progression);
            return progression.ForClass(character.ClassId).SkillUnlocks.Where(u => u.Level > level).ToList();
        }

        /// <summary>
        /// Sous-lot V1.5 : motif de refus d'une place fermée au niveau <paramref name="level"/> (rang <paramref name="index"/> dans sa sorte,
        /// 0 pour l'ultime), avec le niveau qui l'ouvre ; null si la place est ouverte.
        /// </summary>
        public static BuildViolation? ClosedPlace(SkillKind kind, int index, int level, ProgressionRules rules)
        {
            if (rules is null) throw new ArgumentNullException(nameof(rules));
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index), index, "Rang de place négatif.");
            if (kind == SkillKind.Ultimate)
            {
                if (index != 0) throw new ArgumentOutOfRangeException(nameof(index), index, "Une seule place d'ultime (rang 0).");
                return rules.IsUltimateUnlocked(level) ? null : new BuildViolation(BuildViolationCode.UltimateLocked, "ultimate",
                    $"place de l'ultime fermée : elle s'ouvre au niveau {rules.UltimateUnlockLevel} (niveau actuel {level}).");
            }
            bool active = kind == SkillKind.Active;
            IReadOnlyList<int> levels = active ? rules.ActiveSlotLevels : rules.PassiveSlotLevels;
            int open = active ? rules.ActiveSlots(level) : rules.PassiveSlots(level);
            if (index < open) return null;
            string subject = (active ? "activeSkills" : "passiveSkills") + $"[{index}]", label = active ? "active" : "passive";
            return new BuildViolation(BuildViolationCode.TooManySkills, subject, index < levels.Count
                ? $"place {label} {index + 1} fermée : elle s'ouvre au niveau {levels[index]} (niveau actuel {level})."
                : $"la barre n'a que {levels.Count} place(s) {label}(s).");
        }

        /// <summary>
        /// Sous-lot V1.5 (écran des compétences, décision de Damien : changer ses compétences « partout hors combat ») : juge la barre
        /// <paramref name="actives"/>, <paramref name="ultimate"/>, <paramref name="passives"/> pour le personnage à son niveau actuel (avec ses
        /// talents) ; rend le premier motif de refus, ou null si elle est admise. Dans l'ordre :
        /// <list type="number">
        /// <item>une place fermée à ce niveau (<see cref="ClosedPlace"/>, avec le niveau qui l'ouvre) ;</item>
        /// <item>une compétence ni apprise (<see cref="Learned"/>) ni déjà dans la barre du personnage (<see cref="BuildViolationCode.SkillNotLearned"/>) ;</item>
        /// <item>le validateur unique des builds (sorte de la place, doublon, niveau minimal, talents exigés) ;</item>
        /// <item>la barre ne devient pas pire (<see cref="BuildViolationCode.SlotLeftEmpty"/>) : pour chaque sorte, le nombre de places
        /// ouvertes vides qu'une compétence apprise de cette sorte, équipable, pourrait remplir ne monte pas par rapport à la barre que le
        /// personnage aura à la prochaine halte (<see cref="Learn"/>). Une barre pleine ne se vide donc pas (comme <see cref="Learn"/> la
        /// remplit à l'entrée, elle ne change pas à la halte suivante), et une barre trouée (sauvegarde d'avant, données changées) se répare
        /// pas à pas, une compétence à la fois (relecture indépendante de la PR #71).</item>
        /// </list>
        /// Rien n'est modifié ici.
        /// </summary>
        /// <exception cref="ArgumentException">Une liste contient null.</exception>
        public static BuildViolation? CheckSkills(SavedCharacter character, IReadOnlyList<string> actives, string? ultimate, IReadOnlyList<string> passives,
            CharacterProgression progression, ClassDefinition definition, ProgressionRules rules)
        {
            if (character is null) throw new ArgumentNullException(nameof(character));
            if (actives is null) throw new ArgumentNullException(nameof(actives));
            if (passives is null) throw new ArgumentNullException(nameof(passives));
            if (progression is null) throw new ArgumentNullException(nameof(progression));
            if (definition is null) throw new ArgumentNullException(nameof(definition));
            if (rules is null) throw new ArgumentNullException(nameof(rules));
            if (actives.Any(s => s is null)) throw new ArgumentException("Compétence active nulle.", nameof(actives));
            if (passives.Any(s => s is null)) throw new ArgumentException("Compétence passive nulle.", nameof(passives));

            int level = Level(character, progression);
            var build = new CharacterBuild(character.ClassId, level, progression.ForClass(character.ClassId).At(level), character.Talents, actives.ToArray(),
                ultimate, passives.ToArray());
            IReadOnlyList<BuildViolation> violations = BuildValidator.Validate(build, definition, rules);
            // Règles incohérentes, autre classe, niveau impossible : rien d'autre ne peut se juger.
            BuildViolation? fatal = violations.FirstOrDefault(v => v.Code == BuildViolationCode.InvalidProgressionRules || v.Code == BuildViolationCode.WrongClass
                || v.Code == BuildViolationCode.LevelOutOfRange);
            if (fatal != null) return fatal;

            // 1. Places fermées : la première place de trop.
            if (rules.ActiveSlots(level) < actives.Count && ClosedPlace(SkillKind.Active, rules.ActiveSlots(level), level, rules) is BuildViolation closedActive)
                return closedActive;
            if (ultimate != null && ClosedPlace(SkillKind.Ultimate, 0, level, rules) is BuildViolation closedUltimate) return closedUltimate;
            if (rules.PassiveSlots(level) < passives.Count && ClosedPlace(SkillKind.Passive, rules.PassiveSlots(level), level, rules) is BuildViolation closedPassive)
                return closedPassive;

            // 2. Compétences apprises (ou déjà dans la barre du personnage).
            IReadOnlyList<string> learned = Learned(character, progression);
            var allowed = new HashSet<string>(learned.Concat(character.ActiveSkills).Concat(character.PassiveSkills), StringComparer.Ordinal);
            if (character.Ultimate != null) allowed.Add(character.Ultimate);
            IReadOnlyList<SkillUnlock> upcoming = Upcoming(character, progression);
            BuildViolation? NotLearned(string skill, string subject)
            {
                if (allowed.Contains(skill)) return null;
                string name = definition.TryGetSkill(skill, out SkillDefinition d) ? d.Name : skill;
                SkillUnlock? later = upcoming.FirstOrDefault(u => u.Skill == skill);
                return new BuildViolation(BuildViolationCode.SkillNotLearned, subject, later != null
                    ? $"« {name} » n'est pas encore apprise : elle s'apprend au niveau {later.Level} (niveau actuel {level})."
                    : $"« {name} » n'est pas apprise.");
            }
            for (int i = 0; i < actives.Count; i++)
                if (NotLearned(actives[i], $"activeSkills[{i}]") is BuildViolation a) return a;
            if (ultimate != null && NotLearned(ultimate, "ultimate") is BuildViolation u) return u;
            for (int i = 0; i < passives.Count; i++)
                if (NotLearned(passives[i], $"passiveSkills[{i}]") is BuildViolation p) return p;

            // 3. Le validateur unique des builds.
            if (violations.Count > 0) return violations[0];

            // 4. Barre pas pire : par sorte, les places ouvertes vides qu'une compétence apprise en attente (équipable) pourrait remplir ne
            //    sont pas plus nombreuses qu'avant le changement (avant : la barre de la prochaine halte, compétences gagnées en route placées).
            SavedCharacter shown = Learn(character, progression, rules, out _);
            bool Equipable(string skill, SkillKind kind) => definition.TryGetSkill(skill, out SkillDefinition d) && d.Kind == kind && d.MinLevel <= level
                && d.TalentRequirements.All(r => character.Talents.TryGetValue(r.TalentId, out int rank) && rank >= r.MinRank);
            List<string> WaitingFor(SkillKind kind, IEnumerable<string> bar)
            {
                var placed = new HashSet<string>(bar, StringComparer.Ordinal);
                return learned.Where(s => !placed.Contains(s) && Equipable(s, kind)).ToList();
            }
            IEnumerable<string> Bar(IEnumerable<string> a, string? u, IEnumerable<string> p) => u is null ? a.Concat(p) : a.Concat(p).Append(u);
            IEnumerable<string> after = Bar(actives, ultimate, passives).ToList(), before = Bar(shown.ActiveSkills, shown.Ultimate, shown.PassiveSkills).ToList();
            BuildViolation? Worse(SkillKind kind, int used, int usedBefore, int open, string subject, string label)
            {
                List<string> waiting = WaitingFor(kind, after);
                int holes = Math.Min(Math.Max(0, open - used), waiting.Count);
                int holesBefore = Math.Min(Math.Max(0, open - usedBefore), WaitingFor(kind, before).Count);
                if (holes <= holesBefore) return null;
                string name = definition.TryGetSkill(waiting[0], out SkillDefinition d) ? d.Name : waiting[0];
                return new BuildViolation(BuildViolationCode.SlotLeftEmpty, subject,
                    $"{label} vide alors que « {name} » attend : une place ouverte ne se vide pas.");
            }
            int openActives = rules.ActiveSlots(level), openUltimate = rules.IsUltimateUnlocked(level) ? 1 : 0, openPassives = rules.PassiveSlots(level);
            return Worse(SkillKind.Active, actives.Count, shown.ActiveSkills.Count, openActives, $"activeSkills[{actives.Count}]", $"place active {actives.Count + 1}")
                ?? Worse(SkillKind.Ultimate, ultimate is null ? 0 : 1, shown.Ultimate is null ? 0 : 1, openUltimate, "ultimate", "place de l'ultime")
                ?? Worse(SkillKind.Passive, passives.Count, shown.PassiveSkills.Count, openPassives, $"passiveSkills[{passives.Count}]",
                    $"place passive {passives.Count + 1}");
        }

        public static int Level(SavedCharacter character, CharacterProgression progression) =>
            (progression ?? throw new ArgumentNullException(nameof(progression))).LevelFor((character ?? throw new ArgumentNullException(nameof(character))).Xp);

        /// <summary>Build du personnage à son niveau : attributs de la table, talents et compétences gardés.</summary>
        public static CharacterBuild Build(SavedCharacter character, CharacterProgression progression)
        {
            int level = Level(character, progression);
            return new CharacterBuild(character.ClassId, level, progression.ForClass(character.ClassId).At(level), character.Talents,
                character.ActiveSkills.ToArray(), character.Ultimate, character.PassiveSkills.ToArray());
        }

        /// <summary>
        /// Combattant du personnage, à <paramref name="placement"/>, dans l'équipe <paramref name="team"/>. Le build est d'abord validé contre
        /// la classe et la progression des règles.
        /// </summary>
        /// <exception cref="ArgumentException">Classe inconnue, ou build invalide à ce niveau (premier motif et son chemin dans le message).</exception>
        /// <param name="gear">Sous-lot 12.3 : bonus de l'équipement porté (<see cref="Inventory.Gear"/>) ; aucun par défaut.</param>
        public static CharacterSetup Setup(SavedCharacter character, CharacterProgression progression, GameData data, CombatantPlacement placement,
            string team = "joueurs", GearBonus? gear = null, IEnumerable<ConsumableSetup>? consumables = null)
        {
            if (data is null) throw new ArgumentNullException(nameof(data));
            if (!data.TryGetClass(character.ClassId, out LoadedClass loaded))
                throw new ArgumentException($"Classe « {character.ClassId} » absente des données.", nameof(character));
            CharacterBuild build = Build(character, progression);
            IReadOnlyList<BuildViolation> violations = BuildValidator.Validate(build, loaded.Definition, data.Rules.Progression);
            if (violations.Count > 0)
                throw new ArgumentException($"Build du personnage invalide au niveau {build.Level} : {violations[0].Subject} : {violations[0].Message}", nameof(character));
            return new CharacterSetup(character.Id, team, loaded.Definition, build, 0, placement, null, gear, consumables);
        }
    }
}
