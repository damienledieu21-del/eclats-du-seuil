using System;
using EdS.Domain.Combat;
using System.Collections.Generic;
using System.Linq;

namespace EdS.Simulation.Engine
{
    /// <summary>Résultat de la comparaison d'un journal original et d'un journal rejoué.</summary>
    public sealed class ReplayComparison
    {
        public bool Identical => FirstDifferentLine == null;

        /// <summary>Numéro (à partir de 1) de la première ligne différente ; null si les journaux sont identiques.</summary>
        public int? FirstDifferentLine { get; }

        public string? Expected { get; }
        public string? Actual { get; }

        internal ReplayComparison(int? line, string? expected, string? actual)
        {
            FirstDifferentLine = line;
            Expected = expected;
            Actual = actual;
        }
    }

    /// <summary>
    /// Contrôles du rejeu. Un journal n'est reproductible qu'avec le même moteur et des données de même empreinte :
    /// <see cref="CheckIdentity"/> se fait <b>avant</b> de rejouer, et un écart interdit le rejeu.
    /// </summary>
    public static class CombatReplay
    {
        /// <summary>Écarts entre l'identité inscrite au journal et celle des données et du moteur actuels ; vide si le rejeu est permis.</summary>
        public static IReadOnlyList<string> CheckIdentity(string loggedEngineVersion, int loggedFormatVersion, int loggedTickMs,
            CombatDataIdentity logged, CombatDataIdentity current, int currentTickMs)
        {
            if (logged is null) throw new ArgumentNullException(nameof(logged));
            if (current is null) throw new ArgumentNullException(nameof(current));
            var problems = new List<string>();
            if (loggedFormatVersion != CombatEngine.LogFormatVersion)
                problems.Add($"format de journal {loggedFormatVersion}, ce moteur lit le format {CombatEngine.LogFormatVersion}.");
            if (loggedEngineVersion != CombatEngine.Version)
                problems.Add($"journal produit par le moteur {loggedEngineVersion}, moteur actuel {CombatEngine.Version}.");
            if (loggedTickMs != currentTickMs)
                problems.Add($"tick de {loggedTickMs} ms dans le journal, {currentTickMs} ms dans les règles actuelles.");
            Compare("rules.json", logged.Rules, current.Rules, problems);
            foreach (DataFileIdentity expected in logged.Classes)
            {
                DataFileIdentity? actual = current.Classes.FirstOrDefault(c => c.Id == expected.Id);
                if (actual is null) problems.Add($"classe « {expected.Id} » absente des données actuelles.");
                else Compare($"classe « {expected.Id} »", expected, actual, problems);
            }
            return problems;
        }

        private static void Compare(string label, DataFileIdentity expected, DataFileIdentity actual, List<string> problems)
        {
            if (expected.Fingerprint != actual.Fingerprint)
                problems.Add($"{label} modifié : empreinte {expected.Fingerprint} dans le journal, {actual.Fingerprint} actuellement.");
            if (expected.ContentVersion != actual.ContentVersion)
                problems.Add($"{label} : version de contenu {expected.ContentVersion} dans le journal, {actual.ContentVersion} actuellement.");
        }

        /// <summary>Compare deux journaux ligne à ligne, octet par octet.</summary>
        public static ReplayComparison Compare(IReadOnlyList<string> original, IReadOnlyList<string> replayed)
        {
            if (original is null) throw new ArgumentNullException(nameof(original));
            if (replayed is null) throw new ArgumentNullException(nameof(replayed));
            int count = Math.Max(original.Count, replayed.Count);
            for (int i = 0; i < count; i++)
            {
                string? expected = i < original.Count ? original[i] : null;
                string? actual = i < replayed.Count ? replayed[i] : null;
                if (!string.Equals(expected, actual, StringComparison.Ordinal)) return new ReplayComparison(i + 1, expected, actual);
            }
            return new ReplayComparison(null, null, null);
        }
    }
}
