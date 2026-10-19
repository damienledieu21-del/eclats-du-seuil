using System;
using System.Linq;
using EdS.Data;
using EdS.Data.Builds;
using EdS.Domain.Builds;

namespace EdS.Net
{
    /// <summary>
    /// Sous-lot 10.2 : build demandé par un joueur (build de référence, rangs de talents éventuels), résolu et validé par le serveur avec
    /// <see cref="BuildValidator"/>, le seul validateur. Un client ne peut donc jouer qu'un build valide de la classe de sa place.
    /// </summary>
    public static class BuildChoices
    {
        /// <summary>Préfixe du nom d'un build composé (écran des talents).</summary>
        public const string CustomPrefix = "Personnalisé";

        /// <summary>Build résolu et son nom ; null avec la raison du refus.</summary>
        public static CharacterBuild? Resolve(BuildChoice choice, GameData data, ClassDefinition definition, ProgressionRules progression,
            out string name, out string? error)
        {
            if (choice is null) throw new ArgumentNullException(nameof(choice));
            if (data is null) throw new ArgumentNullException(nameof(data));
            if (definition is null) throw new ArgumentNullException(nameof(definition));
            if (progression is null) throw new ArgumentNullException(nameof(progression));
            name = string.Empty;
            if (!data.TryGetBuild(choice.Reference, out ReferenceBuild reference))
            {
                error = $"build de référence inconnu : « {choice.Reference} ».";
                return null;
            }
            if (reference.Build.ClassId != definition.Id)
            {
                error = $"build « {choice.Reference} » de la classe « {reference.Build.ClassId} », place de la classe « {definition.Id} ».";
                return null;
            }
            CharacterBuild build = choice.Talents is null
                ? reference.Build
                : new CharacterBuild(reference.Build.ClassId, reference.Build.Level, reference.Build.Attributes, choice.Talents,
                    reference.Build.ActiveSkills, reference.Build.Ultimate, reference.Build.PassiveSkills);
            BuildViolation? why = BuildValidator.Validate(build, definition, progression).FirstOrDefault();
            if (why != null)
            {
                error = "build refusé : " + why;
                return null;
            }
            name = choice.Talents is null ? reference.Name : $"{CustomPrefix} ({reference.Name})";
            error = null;
            return build;
        }
    }
}
