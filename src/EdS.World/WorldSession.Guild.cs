using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Guild;
using EdS.Data.Story;
using EdS.Domain.Numerics;
using EdS.Domain.Space;

namespace EdS.World
{
    /// <summary>
    /// Sous-lot V1.7 : la Guilde des aventuriers dans la partie. On s'inscrit, on prend les contrats du jour et on commence une épreuve de
    /// promotion auprès de la maîtresse de guilde (à <see cref="Narrative.TalkRange"/> d'elle, dans sa pièce) ; on abandonne un contrat et on
    /// choisit son titre affiché partout. Chaque opération est atomique : un refus rend son motif (<see cref="GuildRefusal"/>) et ne change rien.
    /// La carte, ses contrats et ses points vivent dans l'histoire (<see cref="Narrative"/>), gardés dans la sauvegarde.
    /// </summary>
    public sealed partial class WorldSession
    {
        /// <summary>La Guilde des aventuriers de la partie, ou null (sans guilde, ou sans histoire).</summary>
        public GuildBook? Guild => Narrative?.Guild;

        /// <summary>Jour du monde (tableau des contrats).</summary>
        public long Day => Zone.Time.Day;

        /// <summary>
        /// La maîtresse de guilde est à portée : guilde chargée, personnage présent et vivant dans la zone de la maîtresse, à
        /// <see cref="Narrative.TalkRange"/> d'elle.
        /// </summary>
        public GuildRefusal CanUseGuild()
        {
            if (!(Guild is GuildBook guild) || Narrative is null) return GuildRefusal.NoGuild;
            Npc npc = Narrative.Book.Npc(guild.Npc);
            if (!(((INarrativeWorld)this).PlayerPositionIn(npc.Zone) is GroundVector p) || ZonePopulations.Beyond(p, Fixed.Zero, npc.Position, Fixed.Zero, Narrative.TalkRange))
                return GuildRefusal.TooFar;
            return GuildRefusal.None;
        }

        /// <summary>
        /// On s'adresse à la maîtresse de guilde (son tableau) : à portée, ses objectifs « parler à » et « livrer à » (quêtes et contrats) sont
        /// remplis comme en lui parlant ; rend le refus de <see cref="CanUseGuild"/>.
        /// </summary>
        public GuildRefusal VisitGuild()
        {
            GuildRefusal open = CanUseGuild();
            if (open == GuildRefusal.None) Narrative!.Visit(Guild!.Npc);
            return open;
        }

        /// <summary>S'inscrit à la guilde (carte au rang G et son titre), auprès de la maîtresse.</summary>
        public GuildRefusal JoinGuild(out IReadOnlyList<StoryEvent> events) => AtDesk(n => n.Join, out events);

        /// <summary>Contrats du tableau du jour pour le rang du personnage (ceux du rang G pour qui n'est pas inscrit) ; vide sans guilde.</summary>
        public IReadOnlyList<GuildContract> Board() =>
            Guild is GuildBook guild ? GuildBoard.Of(guild, Math.Max(0, Narrative!.GuildRankIndex), Day) : Array.Empty<GuildContract>();

        /// <summary>Refus qu'aurait la prise du contrat <paramref name="contract"/> maintenant (rien ne change).</summary>
        public GuildRefusal CanTakeContract(string contract)
        {
            GuildRefusal open = CanUseGuild();
            if (open != GuildRefusal.None) return open;
            Narrative n = Narrative!;
            if (!n.IsMember) return GuildRefusal.NotMember;
            if (contract is null || Board().All(c => c.Id != contract)) return GuildRefusal.NotOnBoard;
            if (n.ActiveContracts.Any(a => a.Contract == contract) || n.TakenOn(contract, Day)) return GuildRefusal.AlreadyTaken;
            if (n.ActiveContracts.Count >= Guild!.MaxActive) return GuildRefusal.TooManyContracts;
            return GuildRefusal.None;
        }

        /// <summary>Prend un contrat du tableau du jour, auprès de la maîtresse.</summary>
        public GuildRefusal TakeContract(string contract, out IReadOnlyList<StoryEvent> events)
        {
            long day = Day;
            return AtDesk(n => list => n.TakeContract(contract, day, list), out events);
        }

        /// <summary>Abandonne un contrat en cours (où que l'on soit) ; il ne se reprend pas avant le lendemain.</summary>
        public GuildRefusal AbandonContract(string contract, out IReadOnlyList<StoryEvent> events)
        {
            var list = new List<StoryEvent>();
            GuildRefusal refusal = Narrative is null ? GuildRefusal.NoGuild : Narrative.AbandonContract(contract, list);
            events = list;
            return refusal;
        }

        /// <summary>Refus qu'aurait l'épreuve du rang suivant maintenant (à portée de la maîtresse, assez de points…) ; rien ne change.</summary>
        public GuildRefusal CanStartTrial()
        {
            GuildRefusal open = CanUseGuild();
            return open != GuildRefusal.None ? open : Narrative!.CanStartTrial();
        }

        /// <summary>Commence l'épreuve de promotion du rang suivant (sa quête), auprès de la maîtresse.</summary>
        public GuildRefusal StartTrial(out IReadOnlyList<StoryEvent> events) => AtDesk(n => n.StartTrial, out events);

        /// <summary>Choisit le titre affiché (un titre gagné), ou plus aucun (null), où que l'on soit.</summary>
        public GuildRefusal ShowTitle(string? title, out IReadOnlyList<StoryEvent> events)
        {
            var list = new List<StoryEvent>();
            GuildRefusal refusal = Narrative is null ? GuildRefusal.NoGuild : Narrative.ShowTitle(title, list);
            events = list;
            return refusal;
        }

        /// <summary>
        /// Rang de la guilde requis pour passer la porte <paramref name="door"/> de la zone en cours (« accès à des lieux ») et que le personnage
        /// n'a pas, ou null (porte libre, ou rang atteint).
        /// </summary>
        public string? DoorNeedsRank(string door)
        {
            if (Definition is null || !(Guild is GuildBook guild) || !(guild.GateOf(Definition.Id, door) is string rank)) return null;
            return HasRank(rank) ? null : rank;
        }

        private GuildRefusal AtDesk(Func<Narrative, Func<List<StoryEvent>, GuildRefusal>> operation, out IReadOnlyList<StoryEvent> events)
        {
            var list = new List<StoryEvent>();
            events = list;
            GuildRefusal open = CanUseGuild();
            if (open != GuildRefusal.None) return open;
            return operation(Narrative!)(list);
        }
    }
}
