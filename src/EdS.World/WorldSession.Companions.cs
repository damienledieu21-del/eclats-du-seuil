using System;
using System.Collections.Generic;
using EdS.Data.Allies;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Simulation.Randomness;

namespace EdS.World
{
    /// <summary>
    /// Compagnons (décisions de Damien du 14 novembre 2026) : la troupe du personnage dans la partie. Recruter un compagnon et ouvrir la
    /// deuxième place passent par les règles pures (<see cref="CompanionRules"/>) ; un refus rend son motif et ne change rien (atomique).
    /// Le premier recrutement apprend le fait <see cref="StoryValidator.CompanionRecruitedFact"/>, qui ouvre la quête de lien au feu.
    /// </summary>
    public sealed partial class WorldSession
    {
        /// <summary>La troupe du personnage (compagnons recrutés, places, compagnons actifs).</summary>
        public SavedCompanions Companions => _character.Companions;

        /// <summary>
        /// Recruter le compagnon <paramref name="companionId"/> : un mercenaire selon le rang de Guilde et la bourse (sa paie est retirée), un
        /// héros ou une bête si l'histoire l'a amené (<paramref name="available"/>). Le trait d'un mercenaire est tiré par un Pcg32
        /// initialisé par la clé du recrutement (personnage, compagnon, temps du monde). Rend null, ou le motif du refus (rien ne change alors).
        /// </summary>
        public string? RecruitCompanion(CompanionCatalog catalog, string companionId, bool available, List<StoryEvent>? events = null)
        {
            if (catalog is null) throw new ArgumentNullException(nameof(catalog));
            // Revue Codex de la PR #172 : le trait est tiré par un Pcg32 injecté, initialisé par la clé stable du recrutement (personnage,
            // compagnon, temps du monde), comme les autres tirages du monde (Loot, Guild, Workshop).
            string key = string.Concat(_character.Id, "/", companionId, "/", Zone.WorldTick.ToString(System.Globalization.CultureInfo.InvariantCulture));
            var random = new Pcg32(Loot.Fnv1a(key), Loot.Fnv1a("companion:trait"));
            var context = new RecruitContext(available, Narrative?.GuildRankId, _character.Money, random.NextBounded);
            RosterResult result = CompanionRules.Recruit(catalog, _character.Companions, companionId, context);
            if (!result.Accepted) return result.Reason;
            _character = _character.WithCompanions(result.Roster).WithMoney(checked(_character.Money - result.Cost));
            NoteRecruitedCompanions(events);
            return null;
        }

        /// <summary>
        /// Ouvrir la deuxième place : la quête de lien finie (lue dans l'histoire de la partie) et le niveau minimum doux atteint. Rend null,
        /// ou le motif du refus (rien ne change alors).
        /// </summary>
        public string? OpenSecondCompanionSlot(CompanionCatalog catalog)
        {
            if (catalog is null) throw new ArgumentNullException(nameof(catalog));
            SavedStory story = Narrative?.ToSaved() ?? _character.Story;
            RosterResult result = CompanionRules.OpenSecondSlot(catalog, _character.Companions, Level, story);
            if (!result.Accepted) return result.Reason;
            _character = _character.WithCompanions(result.Roster);
            return null;
        }

        private void NoteRecruitedCompanions(List<StoryEvent>? events = null)
        {
            if (Narrative is null || _character.Companions.Recruited.Count == 0) return;
            Narrative.Learn(StoryValidator.CompanionRecruitedFact, events ?? new List<StoryEvent>());
        }
    }
}
