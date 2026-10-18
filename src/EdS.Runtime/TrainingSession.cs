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

        /// <summary>Build de référence joué ; null pour le build écrit dans le scénario.</summary>
        public ReferenceBuild? Build { get; }

        private TrainingSession(GameData data, CombatSetup setup, string playerId, ReferenceBuild? build)
        {
            Data = data;
            Setup = setup;
            PlayerId = playerId;
            Build = build;
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
            long duration = durationMs ?? scenario.Combat.MaxDurationMs;
            IEnumerable<CombatantSetup> combatants = scenario.Combatants.Select(c => c == player && build != null
                ? new CharacterSetup(player.Id, player.Team, player.Class, build.Build, player.Initiative)
                : c);
            var setup = new CombatSetup(scenario.Seed, duration, scenario.Stats, scenario.Damage, scenario.Progression, scenario.Combat, combatants,
                Array.Empty<CombatCommand>(), scenario.Data);
            IReadOnlyList<RuleViolation> violations = setup.Validate();
            if (violations.Count > 0) throw new CombatSetupException(violations);
            return new TrainingSession(data, setup, playerId, build);
        }

        /// <summary>Builds de référence de la classe du personnage joué, dans l'ordre de leur fichier.</summary>
        public IReadOnlyList<ReferenceBuild> AvailableBuilds()
        {
            string classId = Setup.Combatants.OfType<CharacterSetup>().Single(c => c.Id == PlayerId).Class.Id;
            return Data.BuildSets.Where(b => b.ClassId == classId).SelectMany(b => b.Builds).ToList();
        }

        /// <summary>La même séance avec un autre build de référence.</summary>
        public TrainingSession WithBuild(string buildId) => Create(Data, Setup, buildId, PlayerId, Setup.DurationMs);

        /// <summary>Nouveau combat depuis le début de la séance (même graine, mêmes adversaires).</summary>
        public RealtimeCombat Start(ICombatLog? log = null) => new RealtimeCombat(Setup, PlayerId, log);

        private static string Describe(ValidationReport report) =>
            string.Join(" ; ", report.Errors.Take(5).Select(e => e.ToString())) + (report.Errors.Count > 5 ? $" (+{report.Errors.Count - 5})" : "");
    }
}
