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
        /// a rien à gagner ; <paramref name="learned"/> dit ce qui a été équipé, dans l'ordre de la progression. Cette forme ne connaît pas le
        /// registre du monde : elle place toute compétence avec sa place qui manque, à chaque appel ; la partie utilise la forme avec le
        /// registre, où une compétence sans place libre va dans la réserve pour de bon (donnée).
        /// Réponse de Damien du 30 octobre 2026 : seules les compétences qui viennent avec leur place (<see cref="SkillUnlock.WithPlace"/>)
        /// sont équipées ainsi ; les autres (Provocation, Contrecoup) vont dans la réserve, même si la barre a de la place : le joueur les
        /// place lui-même avec l'écran des compétences.
        /// </summary>
        public static SavedCharacter Learn(SavedCharacter character, CharacterProgression progression, ProgressionRules rules,
            out IReadOnlyList<SkillUnlock> learned) =>
            Learn(character, progression, rules, _ => false, out learned, out _);

        /// <summary>
        /// Comme <see cref="Learn(SavedCharacter, CharacterProgression, ProgressionRules, out IReadOnlyList{SkillUnlock})"/>, sans redonner une
        /// compétence déjà donnée (réponse de Damien du 30 octobre 2026 : une place peut rester vide) : <paramref name="given"/> dit si une clé
        /// <see cref="SkillUnlock.GivenKey"/> est au registre du monde ; une compétence déjà donnée n'est plus placée d'office.
        /// <paramref name="handed"/> rend les compétences avec leur place, gagnées jusqu'au niveau du personnage et pas encore données (placées
        /// ici, déjà dans la barre, ou en réserve faute de place) : leurs clés sont à inscrire au registre. Rien n'est modifié ici.
        /// </summary>
        public static SavedCharacter Learn(SavedCharacter character, CharacterProgression progression, ProgressionRules rules, Func<string, bool> given,
            out IReadOnlyList<SkillUnlock> learned, out IReadOnlyList<SkillUnlock> handed)
        {
            if (character is null) throw new ArgumentNullException(nameof(character));
            if (progression is null) throw new ArgumentNullException(nameof(progression));
            if (rules is null) throw new ArgumentNullException(nameof(rules));
            if (given is null) throw new ArgumentNullException(nameof(given));
            int level = Level(character, progression);
            var actives = character.ActiveSkills.ToList();
            var passives = character.PassiveSkills.ToList();
            string? ultimate = character.Ultimate;
            int activeSlots = rules.ActiveSlotLevels.Count(l => l <= level), passiveSlots = rules.PassiveSlotLevels.Count(l => l <= level);
            var gained = new List<SkillUnlock>();
            var handing = new List<SkillUnlock>();
            foreach (SkillUnlock u in progression.ForClass(character.ClassId).SkillUnlocks)
            {
                if (u.Level > level || !u.WithPlace || given(u.GivenKey)) continue;
                handing.Add(u);
                if (actives.Contains(u.Skill) || passives.Contains(u.Skill) || ultimate == u.Skill) continue;
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
                        continue;   // emplacement pris : la compétence va dans la réserve (donnée, la halte ne la placera plus)
                }
                gained.Add(u);
            }
            learned = gained;
            handed = handing;
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
        /// <item>le validateur unique des builds (sorte de la place, doublon, niveau minimal, talents exigés).</item>
        /// </list>
        /// Une place ouverte peut rester vide (réponse de Damien du 30 octobre 2026 : « oui, permis ») : la règle « barre pas pire » de la
        /// relecture de la PR #71 est retirée ; la halte ne remplit plus une place vidée (<see cref="SkillUnlock.GivenKey"/>, inscrite par
        /// <c>WorldSession.SetSkills</c>). Rien n'est modifié ici.
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

            // 3. Le validateur unique des builds. Une place ouverte peut rester vide (réponse de Damien du 30 octobre 2026).
            return violations.Count > 0 ? violations[0] : null;
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
        /// <param name="shield">Sous-lot V1.2 : garde du bouclier porté (<see cref="Inventory.Shield"/>) ; aucune par défaut (celle des règles).</param>
        /// <param name="equipment">Sous-lot V1.3 : effets de l'équipement porté (<see cref="Inventory.Effects"/>) ; aucun par défaut.</param>
        public static CharacterSetup Setup(SavedCharacter character, CharacterProgression progression, GameData data, CombatantPlacement placement,
            string team = "joueurs", GearBonus? gear = null, IEnumerable<ConsumableSetup>? consumables = null, ShieldGuard? shield = null,
            IEnumerable<EquipmentEffect>? equipment = null)
        {
            if (data is null) throw new ArgumentNullException(nameof(data));
            if (!data.TryGetClass(character.ClassId, out LoadedClass loaded))
                throw new ArgumentException($"Classe « {character.ClassId} » absente des données.", nameof(character));
            CharacterBuild build = Build(character, progression);
            IReadOnlyList<BuildViolation> violations = BuildValidator.Validate(build, loaded.Definition, data.Rules.Progression);
            if (violations.Count > 0)
                throw new ArgumentException($"Build du personnage invalide au niveau {build.Level} : {violations[0].Subject} : {violations[0].Message}", nameof(character));
            return new CharacterSetup(character.Id, team, loaded.Definition, build, 0, placement, null, gear, consumables, shield, equipment);
        }
    }
}
