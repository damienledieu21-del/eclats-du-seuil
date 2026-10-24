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
