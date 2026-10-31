using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EdS.Data;
using EdS.Data.Builds;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Domain;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;

namespace EdS.Runtime
{
    /// <summary>
    /// Sous-lot 9.2 : séance d'entraînement jouable. Charge les données (<c>data/v0</c>) et un scénario ordinaire (adversaires, graine),
    /// retire ses commandes (le joueur les donne en temps réel), remplace au besoin le build du personnage joué par un build de
    /// référence et prolonge le combat jusqu'à la durée maximale des règles. <see cref="Start"/> lance un combat ; chaque relance repart
    /// du même état (même graine).
    /// </summary>
    public sealed class TrainingSession
    {
        public GameData Data { get; }

        /// <summary>Scénario de départ du combat : sans commande, build choisi, durée de la séance.</summary>
        public CombatSetup Setup { get; }
        public string PlayerId { get; }

        /// <summary>Build de référence joué ; null pour le build écrit dans le scénario ou un build composé (<see cref="WithCustomBuild"/>).</summary>
        public ReferenceBuild? Build { get; }

        /// <summary>Sous-lot 9.4 : nom du build composé par le joueur ; null pour un build de référence ou celui du scénario.</summary>
        public string? CustomBuildName { get; }

        /// <summary>Nom du build joué, pour l'affichage.</summary>
        public string BuildName => CustomBuildName ?? Build?.Name ?? "du scénario";

        /// <summary>Personnage joué (classe et build effectivement en jeu).</summary>
        public CharacterSetup Player => Setup.Combatants.OfType<CharacterSetup>().Single(c => c.Id == PlayerId);

        private TrainingSession(GameData data, CombatSetup setup, string playerId, ReferenceBuild? build, string? customBuildName)
        {
            Data = data;
            Setup = setup;
            PlayerId = playerId;
            Build = build;
            CustomBuildName = customBuildName;
        }

        /// <summary>
        /// Charge les données de <paramref name="dataDirectory"/> et le scénario <paramref name="scenarioPath"/> ; le personnage
        /// <paramref name="playerId"/> joue le build de référence <paramref name="buildId"/> (null : celui du scénario). La séance dure
        /// <paramref name="durationMs"/> (null : la durée maximale des règles). Toute erreur de données lève <see cref="InvalidDataException"/>
        /// avec les messages du validateur.
        /// </summary>
        public static TrainingSession Load(string dataDirectory, string scenarioPath, string? buildId = null, string playerId = "bastion", long? durationMs = null)
        {
            GameDataLoadResult loaded = GameDataLoader.Load(dataDirectory);
            if (loaded.Data is not { } data)
                throw new InvalidDataException("Données refusées : " + string.Join(" ; ", loaded.Failures.Select(f => $"{f.Path} : {Describe(f.Report)}")));
            ScenarioLoadResult scenario = ScenarioJsonReader.ReadFile(scenarioPath, data);
            if (scenario.Setup is not { } setup)
                throw new InvalidDataException($"Scénario refusé : {scenarioPath} : {Describe(scenario.Report)}");
            return Create(data, setup, buildId, playerId, durationMs);
        }

        /// <summary>Même préparation à partir d'un scénario déjà lu.</summary>
        public static TrainingSession Create(GameData data, CombatSetup scenario, string? buildId = null, string playerId = "bastion", long? durationMs = null)
        {
            if (data is null) throw new ArgumentNullException(nameof(data));
            if (scenario is null) throw new ArgumentNullException(nameof(scenario));
            CharacterSetup player = scenario.Combatants.OfType<CharacterSetup>().SingleOrDefault(c => c.Id == playerId)
                ?? throw new ArgumentException($"Aucun personnage « {playerId} » dans le scénario.", nameof(playerId));
            ReferenceBuild? build = null;
            if (buildId != null)
            {
                if (!data.TryGetBuild(buildId, out ReferenceBuild found))
                    throw new ArgumentException($"Build de référence inconnu : {buildId}.", nameof(buildId));
                if (found.Build.ClassId != player.Class.Id)
                    throw new ArgumentException($"Build « {buildId} » de la classe « {found.Build.ClassId} », personnage de la classe « {player.Class.Id} ».", nameof(buildId));
                build = found;
            }
            return Prepare(data, scenario, player, build?.Build, durationMs ?? scenario.Combat.MaxDurationMs, build, null);
        }

        /// <summary>Scénario de la séance : commandes retirées, build du joueur remplacé (s'il est donné), durée de la séance ; validé.</summary>
        private static TrainingSession Prepare(GameData data, CombatSetup scenario, CharacterSetup player, CharacterBuild? playerBuild, long duration,
            ReferenceBuild? reference, string? customName)
        {
            IEnumerable<CombatantSetup> combatants = scenario.Combatants.Select(c => c == player && playerBuild != null
                ? new CharacterSetup(player.Id, player.Team, player.Class, playerBuild, player.Initiative, player.Placement, player.ArrivalMs, player.Gear, player.Consumables, player.Shield)
                : c);
            var setup = new CombatSetup(scenario.Seed, duration, scenario.Stats, scenario.Damage, scenario.Progression, scenario.Combat, combatants,
                Array.Empty<CombatCommand>(), scenario.Data, scenario.Space, scenario.Zone);
            IReadOnlyList<RuleViolation> violations = setup.Validate();
            if (violations.Count > 0) throw new CombatSetupException(violations);
            return new TrainingSession(data, setup, player.Id, reference, customName);
        }

        /// <summary>Builds de référence de la classe du personnage joué, dans l'ordre de leur fichier.</summary>
        public IReadOnlyList<ReferenceBuild> AvailableBuilds()
        {
            string classId = Setup.Combatants.OfType<CharacterSetup>().Single(c => c.Id == PlayerId).Class.Id;
            return Data.BuildSets.Where(b => b.ClassId == classId).SelectMany(b => b.Builds).ToList();
        }

        /// <summary>La même séance avec un autre build de référence.</summary>
        public TrainingSession WithBuild(string buildId) => Create(Data, Setup, buildId, PlayerId, Setup.DurationMs);

        /// <summary>
        /// Sous-lot 9.4 : la même séance avec un build composé par le joueur (<see cref="TalentEditor"/>), nommé <paramref name="name"/>.
        /// Le build est validé comme tout build de scénario ; refusé, il lève <see cref="CombatSetupException"/> et la séance ne change pas.
        /// </summary>
        public TrainingSession WithCustomBuild(CharacterBuild build, string name)
        {
            if (build is null) throw new ArgumentNullException(nameof(build));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Nom de build vide.", nameof(name));
            return Prepare(Data, Setup, Player, build, Setup.DurationMs, null, name);
        }

        /// <summary>Sous-lot 9.4 : éditeur de talents qui part du build joué, avec la classe du personnage et les règles de progression du combat.</summary>
        public TalentEditor CreateTalentEditor() => new TalentEditor(Player.Build, Player.Class, Setup.Progression);

        /// <summary>Nouveau combat depuis le début de la séance (même graine, mêmes adversaires).</summary>
        public RealtimeCombat Start() => new RealtimeCombat(Setup, PlayerId);

        private static string Describe(ValidationReport report) =>
            string.Join(" ; ", report.Errors.Take(5).Select(e => e.ToString())) + (report.Errors.Count > 5 ? $" (+{report.Errors.Count - 5})" : "");
    }
}
