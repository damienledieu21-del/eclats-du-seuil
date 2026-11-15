using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EdS.Data.Saves;
using EdS.Domain.Combat;
using EdS.Domain.Space;

namespace EdS.Data.Allies
{
    /// <summary>Issue d'une opération sur la troupe : acceptée (nouvelle troupe) ou refusée (motif, troupe inchangée).</summary>
    public sealed class RosterResult
    {
        public bool Accepted => Reason is null;

        /// <summary>Motif du refus (identifiant stable, pour l'interface) ; null si l'opération est acceptée.</summary>
        public string? Reason { get; }

        /// <summary>La troupe après l'opération (la même instance si elle est refusée).</summary>
        public SavedCompanions Roster { get; }

        /// <summary>Recrutement d'un mercenaire : paie due (cuivre) ; 0 sinon.</summary>
        public long Cost { get; }

        private RosterResult(string? reason, SavedCompanions roster, long cost)
        {
            Reason = reason;
            Roster = roster;
            Cost = cost;
        }

        public static RosterResult Ok(SavedCompanions roster, long cost = 0) => new RosterResult(null, roster, cost);
        public static RosterResult Refused(string reason, SavedCompanions unchanged) => new RosterResult(reason, unchanged, 0);
    }

    /// <summary>Ce que le jeu sait au moment d'un recrutement.</summary>
    public sealed class RecruitContext
    {
        /// <summary>L'histoire a amené ce compagnon (héros, Cendre) ou le dressage est fait (bête apprivoisée).</summary>
        public bool Available { get; }

        /// <summary>Rang de Guilde du personnage (mercenaires) ; null s'il n'est pas inscrit.</summary>
        public string? GuildRank { get; }

        /// <summary>Bourse du personnage, en cuivre.</summary>
        public long Money { get; }

        /// <summary>Graine du tirage du trait d'un mercenaire (par exemple le temps du monde) : même graine, même trait.</summary>
        public ulong Seed { get; }

        public RecruitContext(bool available, string? guildRank, long money, ulong seed)
        {
            Available = available;
            GuildRank = guildRank;
            Money = money;
            Seed = seed;
        }
    }

    /// <summary>
    /// Compagnons : règles de progression de la troupe (décisions de Damien du 14 novembre 2026), fonctions pures sur la sauvegarde. Toute
    /// opération contrôle d'abord, sans rien modifier, puis rend une nouvelle troupe (refus atomique : motif et troupe inchangée).
    /// <list type="bullet">
    /// <item><b>Recrutement</b> : un héros ou une bête quand l'histoire l'amène, un mercenaire à la Guilde selon le rang et contre sa paie ; le
    /// trait d'un mercenaire est tiré au hasard (déterministe, d'après une graine) parmi les traits du catalogue. Un nouveau compagnon prend
    /// une place libre s'il y en a une.</item>
    /// <item><b>Places</b> : une au départ, la deuxième s'ouvre par la quête de lien au feu de camp de la fin du chapitre 1, à partir du niveau
    /// minimum doux (environ 12) ; pendant le boss du monde, un seul compagnon au combat (le second attend au point de repli).</item>
    /// <item><b>Niveau</b> : celui du personnage (le compagnon monte de niveau avec lui).</item>
    /// <item><b>Hors combat sans mort</b> : un compagnon tombé reste dans la troupe, hors combat, jusqu'à la prochaine halte au feu de camp,
    /// qui le relève et rapproche (jauge de lien) les compagnons actifs.</item>
    /// <item><b>Estime</b> : il aime ou déteste les choix du joueur (de −100 à 100) ; elle ne change jamais son obéissance au combat.</item>
    /// </list>
    /// </summary>
    public static class CompanionRules
    {
        /// <summary>Accord d'une troupe relue avec le catalogue : compagnons connus, traits connus et seulement chez les mercenaires, places au plus celles du catalogue.</summary>
        public static IReadOnlyList<string> Check(CompanionCatalog catalog, SavedCompanions roster)
        {
            if (catalog is null) throw new ArgumentNullException(nameof(catalog));
            if (roster is null) throw new ArgumentNullException(nameof(roster));
            var problems = new List<string>();
            if (roster.Slots > catalog.Slots.Max) problems.Add($"{roster.Slots} places, au plus {catalog.Slots.Max}.");
            foreach (SavedCompanion c in roster.Recruited)
            {
                if (!catalog.TryGet(c.Id, out CompanionSheet sheet)) { problems.Add($"compagnon inconnu « {c.Id} »."); continue; }
                if (sheet.RandomTrait != (c.Trait != null)) problems.Add($"« {c.Id} » : trait {(sheet.RandomTrait ? "manquant" : "en trop")}.");
                if (c.Trait != null && catalog.Trait(c.Trait) is null) problems.Add($"« {c.Id} » : trait inconnu « {c.Trait} ».");
            }
            return problems;
        }

        /// <summary>Niveau d'un compagnon : celui du personnage, borné à [1, <see cref="CompanionCombat.MaxLevel"/>].</summary>
        public static int LevelOf(int characterLevel) => Math.Max(1, Math.Min(CompanionCombat.MaxLevel, characterLevel));

        /// <summary>Trait tiré pour un mercenaire : FNV-1a 64 de la graine et de son identifiant, modulo le nombre de traits (même graine, même trait).</summary>
        public static string DrawTrait(CompanionCatalog catalog, string companionId, ulong seed)
        {
            if (catalog is null) throw new ArgumentNullException(nameof(catalog));
            ulong hash = 14695981039346656037UL;
            unchecked
            {
                for (int i = 0; i < 8; i++) hash = (hash ^ (byte)(seed >> (8 * i))) * 1099511628211UL;
                foreach (byte b in Encoding.UTF8.GetBytes(companionId ?? throw new ArgumentNullException(nameof(companionId)))) hash = (hash ^ b) * 1099511628211UL;
            }
            return catalog.Traits[(int)(hash % (ulong)catalog.Traits.Count)].Id;
        }

        public static RosterResult Recruit(CompanionCatalog catalog, SavedCompanions roster, string companionId, RecruitContext context)
        {
            if (catalog is null) throw new ArgumentNullException(nameof(catalog));
            if (roster is null) throw new ArgumentNullException(nameof(roster));
            if (context is null) throw new ArgumentNullException(nameof(context));
            if (!catalog.TryGet(companionId, out CompanionSheet sheet)) return RosterResult.Refused("unknownCompanion", roster);
            if (roster.Find(companionId) != null) return RosterResult.Refused("alreadyRecruited", roster);
            if (roster.Recruited.Count >= SavedCompanions.MaxRecruited) return RosterResult.Refused("rosterFull", roster);
            long cost = 0;
            if (sheet.Kind == CompanionKind.Mercenary)
            {
                int need = Guild.GuildRanks.IndexOf(sheet.Recruit.GuildRank), have = Guild.GuildRanks.IndexOf(context.GuildRank);
                if (have < 0 || have < need) return RosterResult.Refused("guildRankTooLow", roster);
                cost = sheet.Recruit.Price;
                if (context.Money < cost) return RosterResult.Refused("notEnoughMoney", roster);
            }
            else if (!context.Available) return RosterResult.Refused("notAvailable", roster);
            string? trait = sheet.RandomTrait ? DrawTrait(catalog, sheet.Id, context.Seed) : null;
            var recruited = roster.Recruited.Append(new SavedCompanion(sheet.Id, 0, 0, trait, false));
            IEnumerable<string> active = roster.Active.Count < roster.Slots ? roster.Active.Append(sheet.Id) : roster.Active;
            return RosterResult.Ok(new SavedCompanions(roster.Slots, recruited, active), cost);
        }

        /// <summary>Renvoyer un mercenaire (un héros ou une bête reste : leur histoire continue) ; il quitte sa place.</summary>
        public static RosterResult Dismiss(CompanionCatalog catalog, SavedCompanions roster, string companionId)
        {
            if (catalog is null) throw new ArgumentNullException(nameof(catalog));
            if (roster is null) throw new ArgumentNullException(nameof(roster));
            if (roster.Find(companionId) is null) return RosterResult.Refused("notRecruited", roster);
            if (!catalog.TryGet(companionId, out CompanionSheet sheet) || sheet.Kind != CompanionKind.Mercenary) return RosterResult.Refused("notMercenary", roster);
            return RosterResult.Ok(new SavedCompanions(roster.Slots, roster.Recruited.Where(c => c.Id != companionId), roster.Active.Where(a => a != companionId)));
        }

        /// <summary>Choisir les compagnons actifs, dans l'ordre des places : recrutés, distincts, au plus les places ouvertes.</summary>
        public static RosterResult SetActive(SavedCompanions roster, IReadOnlyList<string> active)
        {
            if (roster is null) throw new ArgumentNullException(nameof(roster));
            if (active is null) throw new ArgumentNullException(nameof(active));
            if (active.Count > roster.Slots) return RosterResult.Refused("noFreeSlot", roster);
            if (active.Distinct(StringComparer.Ordinal).Count() != active.Count) return RosterResult.Refused("duplicate", roster);
            if (active.Any(a => roster.Find(a) is null)) return RosterResult.Refused("notRecruited", roster);
            return RosterResult.Ok(new SavedCompanions(roster.Slots, roster.Recruited, active));
        }

        /// <summary>
        /// Ouvrir la deuxième place (décision de Damien du 14 novembre 2026, 23 h 43) : la quête de lien au feu de camp est finie et le
        /// personnage a au moins le niveau minimum doux du catalogue.
        /// </summary>
        public static RosterResult OpenSecondSlot(CompanionCatalog catalog, SavedCompanions roster, int characterLevel, bool bondQuestDone)
        {
            if (catalog is null) throw new ArgumentNullException(nameof(catalog));
            if (roster is null) throw new ArgumentNullException(nameof(roster));
            if (roster.Slots >= catalog.Slots.Max) return RosterResult.Refused("allSlotsOpen", roster);
            if (!bondQuestDone) return RosterResult.Refused("bondQuestNotDone", roster);
            if (characterLevel < catalog.Slots.SecondSlotLevel) return RosterResult.Refused("levelTooLow", roster);
            return RosterResult.Ok(new SavedCompanions(roster.Slots + 1, roster.Recruited, roster.Active));
        }

        /// <summary>
        /// Compagnons qui se battent : les actifs qui ne sont pas hors combat, dans l'ordre des places ; pendant le boss du monde, au plus
        /// <see cref="SlotRules.WorldBossInCombat"/> (les autres attendent au point de repli de l'événement).
        /// </summary>
        public static IReadOnlyList<SavedCompanion> Fighters(CompanionCatalog catalog, SavedCompanions roster, bool worldBoss)
        {
            if (catalog is null) throw new ArgumentNullException(nameof(catalog));
            if (roster is null) throw new ArgumentNullException(nameof(roster));
            IEnumerable<SavedCompanion> ready = roster.Active.Select(a => roster.Find(a)!).Where(c => !c.Down);
            return (worldBoss ? ready.Take(catalog.Slots.WorldBossInCombat) : ready).ToArray();
        }

        /// <summary>Identifiant de combattant d'un compagnon : son identifiant sans « companion. » (« companion.nyx » → « nyx »).</summary>
        public static string CombatantId(string companionId) =>
            companionId != null && companionId.StartsWith("companion.", StringComparison.Ordinal) ? companionId.Substring("companion.".Length)
                : throw new ArgumentException($"Compagnon « {companionId} » hors du format.", nameof(companionId));

        /// <summary>
        /// Les compagnons qui se battent, en assaillants compagnons de <paramref name="owner"/> au niveau du personnage, jauge de lien de la
        /// sauvegarde ; <paramref name="placement"/> donne la place de départ du i-ème.
        /// </summary>
        public static IReadOnlyList<AssailantSetup> Combatants(CompanionCatalog catalog, SavedCompanions roster, int characterLevel, string owner, string team,
            bool worldBoss, Func<int, CombatantPlacement> placement, long? arrivalMs = null)
        {
            if (placement is null) throw new ArgumentNullException(nameof(placement));
            IReadOnlyList<SavedCompanion> fighters = Fighters(catalog, roster, worldBoss);
            var result = new List<AssailantSetup>();
            for (int i = 0; i < fighters.Count; i++)
            {
                SavedCompanion c = fighters[i];
                if (!catalog.TryGet(c.Id, out CompanionSheet sheet)) throw new ArgumentException($"Compagnon inconnu « {c.Id} ».", nameof(roster));
                result.Add(CompanionCombat.Build(catalog, sheet, LevelOf(characterLevel), CombatantId(c.Id), owner, team, c.Bond, placement(i), arrivalMs));
            }
            return result;
        }

        /// <summary>
        /// Après un combat : la jauge de lien de fin de combat de chaque compagnon qui s'est battu (bornée), et hors combat ceux qui sont tombés
        /// (jamais retirés de la troupe). Un compagnon absent des issues ne change pas.
        /// </summary>
        public static SavedCompanions AfterCombat(SavedCompanions roster, IEnumerable<(string Id, int Bond, bool Fallen)> outcomes)
        {
            if (roster is null) throw new ArgumentNullException(nameof(roster));
            Dictionary<string, (int Bond, bool Fallen)> byId = (outcomes ?? throw new ArgumentNullException(nameof(outcomes)))
                .GroupBy(o => o.Id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => (g.Last().Bond, g.Last().Fallen), StringComparer.Ordinal);
            var recruited = roster.Recruited.Select(c => byId.TryGetValue(c.Id, out var o)
                ? c.With(bond: Math.Max(0, Math.Min(SavedCompanion.MaxBond, o.Bond)), down: c.Down || o.Fallen) : c);
            return new SavedCompanions(roster.Slots, recruited, roster.Active);
        }

        /// <summary>Halte au feu de camp : chaque compagnon hors combat se relève ; chaque compagnon actif gagne le lien d'une halte (borné).</summary>
        public static SavedCompanions Camp(CompanionCatalog catalog, SavedCompanions roster)
        {
            if (catalog is null) throw new ArgumentNullException(nameof(catalog));
            if (roster is null) throw new ArgumentNullException(nameof(roster));
            var recruited = roster.Recruited.Select(c => c.With(
                bond: roster.Active.Contains(c.Id) ? Math.Min(SavedCompanion.MaxBond, c.Bond + catalog.Bond.CampGain) : c.Bond, down: false));
            return new SavedCompanions(roster.Slots, recruited, roster.Active);
        }

        /// <summary>Un choix du joueur que le compagnon aime (delta positif) ou déteste (négatif) : estime bornée à [−100, 100].</summary>
        public static RosterResult React(SavedCompanions roster, string companionId, int delta)
        {
            if (roster is null) throw new ArgumentNullException(nameof(roster));
            if (roster.Find(companionId) is not { } c) return RosterResult.Refused("notRecruited", roster);
            long approval = Math.Max(-SavedCompanion.MaxApproval, Math.Min(SavedCompanion.MaxApproval, (long)c.Approval + delta));
            return RosterResult.Ok(new SavedCompanions(roster.Slots, roster.Recruited.Select(x => x.Id == companionId ? x.With(approval: (int)approval) : x), roster.Active));
        }
    }
}
