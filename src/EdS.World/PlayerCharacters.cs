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
