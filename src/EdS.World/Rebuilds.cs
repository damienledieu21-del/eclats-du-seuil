using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Rebuilds;
using EdS.Data.Saves;
using EdS.Data.Validation;

namespace EdS.World
{
    /// <summary>Lieux reconstruits : état d'un lieu (décisions de Damien du 19 novembre 2026).</summary>
    public enum RebuildPhase
    {
        /// <summary>Rien n'a encore été apporté.</summary>
        Ruin = 0,

        /// <summary>Chantier ouvert : quelque chose a été apporté, tous les paliers ne sont pas finis (échafaudages).</summary>
        Building = 1,

        /// <summary>Tous les paliers sont finis : le lieu est reconstruit, pour toujours.</summary>
        Rebuilt = 2,
    }

    /// <summary>
    /// Lieux reconstruits (sous-lot V2.43.1) : règles pures des chantiers, sans état. Un apport est plafonné à ce qui reste du besoin ; un
    /// palier dont tous les besoins sont remplis est fini, et le suivant commence à vide ; un lieu reconstruit le reste (aucune règle ne
    /// fait reculer un chantier : décision de Damien du 19 novembre 2026, « une reconstruction n'est jamais perdue »).
    /// </summary>
    public static class RebuildRules
    {
        public static RebuildPhase PhaseOf(SavedRebuild? saved, RebuildSite site) =>
            saved is null ? RebuildPhase.Ruin : saved.Stage >= site.Stages.Count ? RebuildPhase.Rebuilt : RebuildPhase.Building;

        /// <summary>Le palier en cours, ou null (lieu reconstruit).</summary>
        public static RebuildStage? CurrentStage(SavedRebuild? saved, RebuildSite site)
        {
            int stage = saved?.Stage ?? 0;
            return stage < site.Stages.Count ? site.Stages[stage] : null;
        }

        /// <summary>Ce qui reste à apporter au besoin <paramref name="need"/> du palier en cours.</summary>
        public static long Remaining(SavedRebuild? saved, RebuildNeed need) => need.Amount - (saved?.Of(need.Id) ?? 0);

        /// <summary>
        /// Pourquoi les chantiers gardés ne vont pas avec le catalogue (premier écart, voir <see cref="Check"/>), ou null s'ils vont.
        /// </summary>
        public static string? Mismatch(SavedRebuilds saved, RebuildBook book)
        {
            var report = new ValidationReport();
            Check(saved, book, report, "$.data.character.rebuilds");
            return report.IsValid ? null : report.Errors[0].Message;
        }

        /// <summary>
        /// Accord des chantiers gardés avec le catalogue, chaque écart avec son chemin et son code : lieu inconnu (<c>UnknownReference</c>),
        /// palier au-delà du dernier (<c>OutOfRange</c>), apport à un lieu reconstruit (<c>InvalidValue</c>), à un besoin qui n'est pas celui du
        /// palier en cours (<c>UnknownReference</c>) ou au-delà de ce qui est demandé (<c>OutOfRange</c>). Le catalogue ne fait que grandir
        /// (DONNEES.md) : un écart vient d'un lieu ou d'un palier retiré, ou d'une sauvegarde d'un autre catalogue.
        /// </summary>
        public static void Check(SavedRebuilds saved, RebuildBook book, ValidationReport report, string path)
        {
            if (saved is null) throw new ArgumentNullException(nameof(saved));
            if (book is null) throw new ArgumentNullException(nameof(book));
            if (report is null) throw new ArgumentNullException(nameof(report));
            foreach (SavedRebuild r in saved.Entries)
            {
                string p = path + "." + r.Site;
                if (!(book.Of(r.Site) is RebuildSite site)) { report.Add(p, ValidationCode.UnknownReference, $"lieu « {r.Site} » inconnu du catalogue des chantiers."); continue; }
                if (r.Stage > site.Stages.Count)
                { report.Add(p + ".stage", ValidationCode.OutOfRange, $"« {r.Site} » : {r.Stage} paliers finis, il n'y en a que {site.Stages.Count}."); continue; }
                RebuildStage? stage = CurrentStage(r, site);
                foreach (KeyValuePair<string, long> g in r.Given)
                {
                    string gp = p + ".given." + g.Key;
                    if (stage is null) report.Add(gp, ValidationCode.InvalidValue, $"« {r.Site} » : apport à un lieu déjà reconstruit.");
                    else if (!(stage.Need(g.Key) is RebuildNeed need))
                        report.Add(gp, ValidationCode.UnknownReference, $"« {r.Site} » : besoin « {g.Key} » hors du palier « {stage.Id} ».");
                    else if (g.Value > need.Amount)
                        report.Add(gp, ValidationCode.OutOfRange, $"« {r.Site} » : {g.Value} apportés au besoin « {g.Key} », qui en demande {need.Amount}.");
                }
            }
        }

        /// <summary>
        /// Le chantier après un apport de <paramref name="amount"/> au besoin <paramref name="need"/> du palier en cours (plafonné à ce qui
        /// reste), puis les paliers finis ; <paramref name="finished"/> reçoit les paliers finis par cet apport, dans l'ordre.
        /// </summary>
        /// <exception cref="ArgumentException">Lieu reconstruit, besoin hors du palier en cours, ou quantité non positive.</exception>
        public static SavedRebuild Give(SavedRebuild? saved, RebuildSite site, string need, long amount, Func<RebuildNeed, bool> alreadyMet, out IReadOnlyList<RebuildStage> finished)
        {
            if (site is null) throw new ArgumentNullException(nameof(site));
            if (alreadyMet is null) throw new ArgumentNullException(nameof(alreadyMet));
            if (amount < 1) throw new ArgumentOutOfRangeException(nameof(amount));
            RebuildStage stage = CurrentStage(saved, site) ?? throw new ArgumentException($"« {site.Id} » est déjà reconstruit.", nameof(site));
            RebuildNeed n = stage.Need(need) ?? throw new ArgumentException($"Besoin « {need} » hors du palier « {stage.Id} ».", nameof(need));
            long put = Math.Min(amount, Remaining(saved, n));
            var given = new Dictionary<string, long>(saved?.Given.ToDictionary(g => g.Key, g => g.Value, StringComparer.Ordinal)
                ?? new Dictionary<string, long>(StringComparer.Ordinal), StringComparer.Ordinal);
            if (put > 0) given[n.Id] = checked((given.TryGetValue(n.Id, out long had) ? had : 0) + put);
            return Settle(site, saved?.Stage ?? 0, given, alreadyMet, out finished);
        }

        /// <summary>
        /// Le chantier une fois remplis les besoins déjà acquis (<paramref name="alreadyMet"/> : une découverte déjà faite) et finis les
        /// paliers complets ; null si rien n'a jamais été apporté (lieu en ruine).
        /// </summary>
        public static SavedRebuild? Settle(SavedRebuild? saved, RebuildSite site, Func<RebuildNeed, bool> alreadyMet, out IReadOnlyList<RebuildStage> finished)
        {
            if (site is null) throw new ArgumentNullException(nameof(site));
            if (alreadyMet is null) throw new ArgumentNullException(nameof(alreadyMet));
            if (saved is null && !(CurrentStage(null, site) is RebuildStage first && first.Needs.Any(alreadyMet)))
            {
                finished = Array.Empty<RebuildStage>();
                return null;
            }
            return Settle(site, saved?.Stage ?? 0, saved?.Given.ToDictionary(g => g.Key, g => g.Value, StringComparer.Ordinal)
                ?? new Dictionary<string, long>(StringComparer.Ordinal), alreadyMet, out finished);
        }

        private static SavedRebuild Settle(RebuildSite site, int stage, Dictionary<string, long> given, Func<RebuildNeed, bool> alreadyMet, out IReadOnlyList<RebuildStage> finished)
        {
            var done = new List<RebuildStage>();
            while (stage < site.Stages.Count)
            {
                RebuildStage s = site.Stages[stage];
                foreach (RebuildNeed n in s.Needs)
                    if (n.Kind == RebuildNeedKind.Discover && !given.ContainsKey(n.Id) && alreadyMet(n)) given[n.Id] = 1;
                if (s.Needs.Any(n => (given.TryGetValue(n.Id, out long g) ? g : 0) < n.Amount)) break;
                done.Add(s);
                stage++;
                given.Clear();
            }
            finished = done;
            return new SavedRebuild(site.Id, stage, given);
        }
    }
}
