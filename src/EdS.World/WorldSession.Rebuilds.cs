using System;
using System.Collections.Generic;
using EdS.Data.Rebuilds;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;

namespace EdS.World
{
    /// <summary>Lieux reconstruits : raison d'un apport refusé (rien ne change).</summary>
    public enum RebuildRefusal
    {
        None = 0,

        /// <summary>Aucun catalogue des chantiers en jeu, ou il est d'une autre zone.</summary>
        NoRebuilds = 1,

        /// <summary>Lieu inconnu du catalogue.</summary>
        UnknownSite = 2,

        /// <summary>Le lieu est déjà reconstruit.</summary>
        Rebuilt = 3,

        /// <summary>Besoin inconnu dans le palier en cours.</summary>
        UnknownNeed = 4,

        /// <summary>Combats et découvertes comptent d'eux-mêmes : ils ne s'apportent pas.</summary>
        NotByHand = 5,

        /// <summary>Ce besoin est déjà rempli.</summary>
        NeedMet = 6,

        /// <summary>Le personnage n'est pas dans le lieu ni autour (ou il est tombé, ou absent).</summary>
        NotThere = 7,

        /// <summary>En plein combat.</summary>
        InCombat = 8,

        /// <summary>Rien à donner : bourse vide ou aucun de ces matériaux disponible.</summary>
        NothingToGive = 9,
    }

    /// <summary>
    /// Lieux reconstruits (sous-lot V2.43.1, sauvegarde v28 ; décisions de Damien du 19 novembre 2026) : le catalogue des chantiers en jeu
    /// et l'état gardé du personnage (<see cref="SavedCharacter.Rebuilds"/>). On apporte de l'or et des matériaux de métier sur place
    /// (<see cref="GiveToRebuild"/>) ; les créatures vaincues dans le lieu ou autour et les découvertes comptent d'elles-mêmes. Chaque étape
    /// est un fait de l'histoire (<see cref="RebuildSite.StartedFact"/>, un fait par palier fini, <see cref="RebuildSite.DoneFact"/>), que
    /// le contenu lit pour le décor, les PNJ et les services. En solo, l'état vit dans la sauvegarde ; au lot 13, dans l'état de région.
    /// </summary>
    public sealed partial class WorldSession
    {
        private RebuildBook? _rebuilds;
        private long _rebuildRevision = -1;

        /// <summary>Le catalogue des chantiers en jeu, ou null.</summary>
        public RebuildBook? RebuildBook => _rebuilds;

        /// <summary>Les chantiers du personnage.</summary>
        public SavedRebuilds RebuildState => _character.Rebuilds;

        /// <summary>
        /// Met le catalogue des chantiers en jeu (null : aucun). L'état gardé doit aller avec lui (<see cref="RebuildRules.Mismatch"/>),
        /// sinon <see cref="ArgumentException"/> et rien ne change.
        /// </summary>
        public void UseRebuilds(RebuildBook? book)
        {
            if (book != null && RebuildRules.Mismatch(_character.Rebuilds, book) is string why)
                throw new ArgumentException($"Chantiers sauvegardés hors du catalogue des chantiers : {why}.", nameof(book));
            _rebuilds = book;
            _rebuildRevision = -1;
        }

        /// <summary>État du lieu <paramref name="site"/> (en ruine s'il est inconnu ou sans catalogue).</summary>
        public RebuildPhase RebuildPhaseOf(string site) =>
            _rebuilds?.Of(site) is RebuildSite s ? RebuildRules.PhaseOf(_character.Rebuilds.Of(site), s) : RebuildPhase.Ruin;

        /// <summary>Catalogue en jeu et de cette zone, ou null.</summary>
        private RebuildBook? RebuildsHere => _rebuilds != null && Definition != null && _rebuilds.ZoneId == Definition.Id ? _rebuilds : null;

        private bool Discovered(RebuildNeed need) => need.Kind == RebuildNeedKind.Discover && Narrative != null && Narrative.HasDiscovered(need.Target!);

        /// <summary>
        /// Apporte au besoin <paramref name="need"/> du palier en cours du lieu <paramref name="site"/> tout ce que le personnage peut, au plus
        /// ce qui reste : du cuivre de sa bourse, ou des matériaux de son inventaire. Le personnage est dans le lieu ou autour, vivant, hors
        /// combat. <paramref name="given"/> reçoit la quantité apportée ; <paramref name="events"/> les faits appris (chantier ouvert, paliers
        /// finis, lieu reconstruit). Refus : rien ne change.
        /// </summary>
        public RebuildRefusal GiveToRebuild(string site, string need, out long given, out IReadOnlyList<StoryEvent> events)
        {
            given = 0;
            events = Array.Empty<StoryEvent>();
            if (!(RebuildsHere is RebuildBook book)) return RebuildRefusal.NoRebuilds;
            if (!(book.Of(site) is RebuildSite s)) return RebuildRefusal.UnknownSite;
            SavedRebuild? saved = _character.Rebuilds.Of(site);
            if (!(RebuildRules.CurrentStage(saved, s) is RebuildStage stage)) return RebuildRefusal.Rebuilt;
            if (!(stage.Need(need) is RebuildNeed n)) return RebuildRefusal.UnknownNeed;
            if (n.Kind != RebuildNeedKind.Coins && n.Kind != RebuildNeedKind.Item) return RebuildRefusal.NotByHand;
            long left = RebuildRules.Remaining(saved, n);
            if (left <= 0) return RebuildRefusal.NeedMet;
            CombatantView me = Zone.Engine.View(CombatantId);
            if (!me.IsPresent || me.IsDead || !(me.Position is GroundVector p) || !s.Around(p)) return RebuildRefusal.NotThere;
            if (InCombat) return RebuildRefusal.InCombat;
            long have = n.Kind == RebuildNeedKind.Coins ? _character.Money : Math.Max(0, Inventory.Available(n.Target!));
            long put = Math.Min(left, have);
            if (put <= 0) return RebuildRefusal.NothingToGive;

            // Tout est calculé avant la moindre mutation (refus atomiques).
            SavedRebuild next = RebuildRules.Give(saved, s, n.Id, put, Discovered, out IReadOnlyList<RebuildStage> finished);
            if (n.Kind == RebuildNeedKind.Item)
            {
                if (Inventory.Remove(n.Target!, (int)put) != InventoryRefusal.None) return RebuildRefusal.NothingToGive;
            }
            else _character = _character.WithMoney(checked(_character.Money - put));
            _character = _character.WithRebuilds(_character.Rebuilds.With(next));
            var story = new List<StoryEvent>();
            RebuildFacts(s, saved is null, finished, f => Narrative!.Learn(f, story));
            given = put;
            events = story;
            return RebuildRefusal.None;
        }

        /// <summary>
        /// Une créature vaincue compte pour les besoins de combat des chantiers où se tient le personnage (une fois par chantier). Choix
        /// assumés (relecture indépendante de la PR #221) : deux lieux dont les marges se recouvrent comptent chacun la même mort ; un
        /// personnage tombé au même tick que sa victime ne la compte pas (il doit être vivant et présent, comme pour les apports).
        /// </summary>
        private void RebuildDefeat()
        {
            if (!(RebuildsHere is RebuildBook book)) return;
            CombatantView me = Zone.Engine.View(CombatantId);
            if (!me.IsPresent || me.IsDead || !(me.Position is GroundVector p)) return;
            for (int i = 0; i < book.Sites.Count; i++)
            {
                RebuildSite s = book.Sites[i];
                if (!s.Around(p)) continue;
                SavedRebuild? saved = _character.Rebuilds.Of(s.Id);
                if (!(RebuildRules.CurrentStage(saved, s) is RebuildStage stage)) continue;
                foreach (RebuildNeed n in stage.Needs)
                {
                    if (n.Kind != RebuildNeedKind.Defeat || RebuildRules.Remaining(saved, n) <= 0) continue;
                    SavedRebuild next = RebuildRules.Give(saved, s, n.Id, 1, Discovered, out IReadOnlyList<RebuildStage> finished);
                    _character = _character.WithRebuilds(_character.Rebuilds.With(next));
                    RebuildFacts(s, saved is null, finished, f => Narrative!.LearnQueued(f));
                    break;
                }
            }
        }

        /// <summary>
        /// À chaque tick, si l'histoire a changé : les découvertes déjà faites remplissent les besoins d'exploration des paliers en cours
        /// (et d'un palier qui commence), puis les paliers complets se finissent.
        /// </summary>
        private void StepRebuilds(List<StoryEvent> story)
        {
            ReachRebuilds(story);
            if (!(RebuildsHere is RebuildBook book) || Narrative is null || Narrative.Revision == _rebuildRevision) return;
            _rebuildRevision = Narrative.Revision;
            for (int i = 0; i < book.Sites.Count; i++)
            {
                RebuildSite s = book.Sites[i];
                SavedRebuild? saved = _character.Rebuilds.Of(s.Id);
                if (RebuildRules.CurrentStage(saved, s) is null) continue;
                SavedRebuild? next = RebuildRules.Settle(saved, s, Discovered, out IReadOnlyList<RebuildStage> finished);
                if (next is null || (saved != null && next.Stage == saved.Stage && next.Given.Count == saved.Given.Count)) continue;
                _character = _character.WithRebuilds(_character.Rebuilds.With(next));
                RebuildFacts(s, saved is null, finished, f => Narrative.Learn(f, story));
            }
            _rebuildRevision = Narrative.Revision;
        }

        /// <summary>
        /// V2.43.2b : les endroits à atteindre des paliers en cours où se tient le personnage (présent et vivant, à
        /// <see cref="RebuildValidator.ReachRadius"/> près) comptent d'eux-mêmes, une fois chacun.
        /// </summary>
        private void ReachRebuilds(List<StoryEvent> story)
        {
            if (!(RebuildsHere is RebuildBook book)) return;
            CombatantView me = Zone.Engine.View(CombatantId);
            if (!me.IsPresent || me.IsDead || !(me.Position is GroundVector p)) return;
            for (int i = 0; i < book.Sites.Count; i++)
            {
                RebuildSite s = book.Sites[i];
                if (!s.Around(p)) continue;
                SavedRebuild? saved = _character.Rebuilds.Of(s.Id);
                if (!(RebuildRules.CurrentStage(saved, s) is RebuildStage stage)) continue;
                for (int k = 0; k < stage.Needs.Count; k++)
                {
                    RebuildNeed n = stage.Needs[k];
                    if (n.Kind != RebuildNeedKind.Reach || RebuildRules.Remaining(saved, n) <= 0) continue;
                    if (ZonePopulations.Beyond(p, Fixed.Zero, n.Point!.Value, Fixed.Zero, RebuildValidator.ReachRadius)) continue;
                    SavedRebuild next = RebuildRules.Give(saved, s, n.Id, 1, Discovered, out IReadOnlyList<RebuildStage> finished);
                    _character = _character.WithRebuilds(_character.Rebuilds.With(next));
                    if (Narrative != null) RebuildFacts(s, saved is null, finished, f => Narrative.Learn(f, story));
                    break;   // un endroit par tick et par lieu (le palier a pu changer)
                }
            }
        }

        /// <summary>
        /// V2.43.2b : le PNJ <paramref name="npc"/> n'est apporté que par un lieu reconstruit, qui ne l'est pas encore : il n'est pas dans le
        /// monde (comme un PNJ de passage hors de sa fête : on ne lui parle pas, sa boutique est fermée, l'affichage le cache).
        /// </summary>
        public bool AwaitsRebuild(string npc)
        {
            if (_rebuilds is null) return false;
            for (int i = 0; i < _rebuilds.Sites.Count; i++)
            {
                RebuildSite s = _rebuilds.Sites[i];
                for (int k = 0; k < s.RebuiltNpcs.Count; k++)
                    if (s.RebuiltNpcs[k] == npc) return RebuildRules.PhaseOf(_character.Rebuilds.Of(s.Id), s) != RebuildPhase.Rebuilt;
            }
            return false;
        }

        /// <summary>
        /// V2.43.2b : le point sûr <paramref name="safePoint"/> n'est ouvert que par un lieu reconstruit, qui ne l'est pas encore : il reste
        /// fermé comme un point secret (ni carte, ni repos, ni retour, ni découverte, donc ni voyage).
        /// </summary>
        public bool AwaitsRebuildSafePoint(string safePoint)
        {
            if (_rebuilds is null || Definition is null || _rebuilds.ZoneId != Definition.Id) return false;
            for (int i = 0; i < _rebuilds.Sites.Count; i++)
            {
                RebuildSite s = _rebuilds.Sites[i];
                for (int k = 0; k < s.RebuiltSafePoints.Count; k++)
                    if (s.RebuiltSafePoints[k] == safePoint) return RebuildRules.PhaseOf(_character.Rebuilds.Of(s.Id), s) != RebuildPhase.Rebuilt;
            }
            return false;
        }

        /// <summary>Faits de l'histoire d'un apport : chantier ouvert (premier apport), paliers finis, lieu reconstruit.</summary>
        private void RebuildFacts(RebuildSite site, bool opened, IReadOnlyList<RebuildStage> finished, Func<string, bool> learn)
        {
            if (Narrative is null) return;
            if (opened) learn(site.StartedFact);
            for (int i = 0; i < finished.Count; i++) learn(site.StageFact(finished[i]));
            if (finished.Count > 0 && finished[finished.Count - 1] == site.Stages[site.Stages.Count - 1]) learn(site.DoneFact);
        }
    }
}
