using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EdS.Data.Guild;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.World;

namespace EdS.Runtime
{
    /// <summary>Sous-lot V1.7 : un titre gagné, tel que la carte d'aventurier le montre.</summary>
    public sealed class TitleEntry
    {
        public string Id { get; }
        public string Name { get; }
        public bool Shown { get; }

        internal TitleEntry(string id, string name, bool shown)
        {
            Id = id;
            Name = name;
            Shown = shown;
        }
    }

    /// <summary>Sous-lot V1.7 : la carte d'aventurier telle que l'écran la montre (textes déjà traduits).</summary>
    public sealed class GuildCardView
    {
        /// <summary>Le personnage est inscrit.</summary>
        public bool Member { get; }

        /// <summary>Identifiant du rang (« g »…), ou null sans carte.</summary>
        public string? Rank { get; }

        /// <summary>Nom du rang (« Rang G »), ou vide sans carte.</summary>
        public string RankName { get; }

        /// <summary>Ligne des points (« Points de guilde : 40 / 100 pour le Rang F »).</summary>
        public string PointsText { get; }

        /// <summary>Part des points du rang actuel vers le suivant (0 à 1 ; 1 au plus haut rang ouvert).</summary>
        public double Progress { get; }

        public string ContractsText { get; }

        /// <summary>Titre affiché (traduit), ou null.</summary>
        public string? Title { get; }

        public IReadOnlyList<TitleEntry> Titles { get; }

        /// <summary>Où en est l'épreuve du rang suivant (traduit).</summary>
        public string TrialText { get; }

        /// <summary>L'épreuve peut commencer auprès de la maîtresse (<see cref="AdventureSession.StartTrial"/>).</summary>
        public bool TrialReady { get; }

        internal GuildCardView(bool member, string? rank, string rankName, string pointsText, double progress, string contractsText, string? title,
            IReadOnlyList<TitleEntry> titles, string trialText, bool trialReady)
        {
            Member = member;
            Rank = rank;
            RankName = rankName;
            PointsText = pointsText;
            Progress = progress;
            ContractsText = contractsText;
            Title = title;
            Titles = titles;
            TrialText = trialText;
            TrialReady = trialReady;
        }
    }

    /// <summary>Sous-lot V1.7 : un contrat, tel que le tableau ou la carte le montre (textes déjà traduits).</summary>
    public sealed class ContractEntry
    {
        public string Id { get; }
        public string Name { get; }

        /// <summary>Sorte (« Chasse », « Livraison », « Prime ») et rang (« Rang F »).</summary>
        public string Kind { get; }

        public string Rank { get; }
        public string Text { get; }

        /// <summary>Objectif (« Vaincre : loup ×3 », « Apporter à Dame Brunehilde : Herbe de l'aube ×3 »).</summary>
        public string Objective { get; }

        /// <summary>Récompense (« 1 argent 50 cuivre · 12 points · 40 expérience »).</summary>
        public string Reward { get; }

        /// <summary>Avancement d'un contrat en cours (« 2 / 3 », « prêt à remettre ») ; null au tableau.</summary>
        public string? Progress { get; }

        /// <summary>Part de l'objectif atteinte (0 à 1) d'un contrat en cours.</summary>
        public double Fraction { get; }

        /// <summary>Au tableau : refus qu'aurait la prise maintenant (<see cref="GuildRefusal.None"/> : possible).</summary>
        public GuildRefusal Refusal { get; }

        internal ContractEntry(string id, string name, string kind, string rank, string text, string objective, string reward, string? progress, double fraction,
            GuildRefusal refusal)
        {
            Id = id;
            Name = name;
            Kind = kind;
            Rank = rank;
            Text = text;
            Objective = objective;
            Reward = reward;
            Progress = progress;
            Fraction = fraction;
            Refusal = refusal;
        }
    }

    /// <summary>
    /// Sous-lot V1.7 : la Guilde des aventuriers dans l'aventure. Parler à la maîtresse de guilde (F) ouvre son tableau
    /// (<see cref="GuildOpen"/>) : inscription, contrats du jour, contrats en cours, épreuve de promotion ; le bouton Parler ouvre son dialogue
    /// de l'histoire. La carte d'aventurier (rang, points, titres) se lit partout (journal). Tout passe par la session
    /// (<see cref="WorldSession.JoinGuild"/>…), seule juge : un refus ne change rien et son motif s'affiche (textes <c>text.ui.guild.…</c>).
    /// </summary>
    public sealed partial class AdventureSession
    {
        private readonly GuildBook _guild;

        /// <summary>La Guilde des aventuriers chargée.</summary>
        public GuildBook Guild => _guild;

        /// <summary>Le tableau de la maîtresse de guilde est ouvert.</summary>
        public bool GuildOpen { get; private set; }

        /// <summary>Le PNJ <paramref name="npc"/> est la maîtresse de guilde.</summary>
        public bool IsGuildmistress(string npc) => npc != null && npc == _guild.Npc;

        /// <summary>
        /// Ouvre le tableau de la guilde, si la maîtresse est à portée ; ses objectifs « parler à » et « livrer à » (quêtes, contrats) sont
        /// remplis comme en lui parlant. Sinon le motif s'affiche et rien ne change.
        /// </summary>
        public bool OpenGuild()
        {
            GuildRefusal refusal = World.VisitGuild();
            if (refusal != GuildRefusal.None)
            {
                Refused(refusal);
                return false;
            }
            CloseDialogue();
            Shop = null;
            GuildOpen = true;
            return true;
        }

        public void CloseGuild() => GuildOpen = false;

        /// <summary>Ferme le tableau et parle à la maîtresse (son dialogue de l'histoire) ; faux si elle n'a rien à dire.</summary>
        public bool TalkToGuildmistress()
        {
            GuildOpen = false;
            DialogueRefusal refusal = World.Talk(_guild.Npc, out _);
            if (refusal == DialogueRefusal.NothingToSay) Say("text.ui.nothing_to_say", Text(_story.Npc(_guild.Npc).Name));
            else if (refusal == DialogueRefusal.TooFar) Say("text.ui.too_far");
            return refusal == DialogueRefusal.None;
        }

        /// <summary>Nom traduit du rang <paramref name="rank"/> (« Rang F »).</summary>
        public string RankName(string rank) => Text(_guild.Rank(rank).Name);

        /// <summary>Nom traduit du titre affiché, ou null (le titre sous le nom du personnage).</summary>
        public string? ShownTitle => World.Narrative?.ShownTitle is string id && _guild.TryGetTitle(id, out GuildTitle t) ? Text(t.Name) : null;

        /// <summary>La carte d'aventurier, pour l'écran.</summary>
        public GuildCardView GuildCard()
        {
            Narrative? n = World.Narrative;
            if (n is null || !n.IsMember)
                return new GuildCardView(false, null, "", Text("text.ui.guild.not_member"), 0, "", null, Array.Empty<TitleEntry>(), Text("text.ui.guild.not_member"), false);
            GuildRank rank = _guild.Ranks[n.GuildRankIndex];
            GuildRank? next = n.NextRank;
            bool openNext = next != null && next.Open;
            string points = openNext ? Format("text.ui.guild.points_next", n.GuildPoints, next!.Points, Text(next.Name)) : Format("text.ui.guild.points_top", n.GuildPoints);
            double progress = !openNext ? 1 : next!.Points <= rank.Points ? 1
                : Math.Max(0, Math.Min(1, (double)(n.GuildPoints - rank.Points) / (next.Points - rank.Points)));
            GuildRefusal trial = n.CanStartTrial();
            string trialText = trial switch
            {
                GuildRefusal.None => Format("text.ui.guild.trial_ready", Text(next!.Name)),
                GuildRefusal.TrialActive => Format("text.ui.guild.trial_running", Text(_story.Quest(next!.Trial!).Name)),
                GuildRefusal.NotEnoughPoints => Format("text.ui.guild.trial_points", next!.Points, Text(next.Name)),
                GuildRefusal.Locked => Format("text.ui.guild.trial_locked", Text(next!.Name)),   // relecture de la PR #79 : conditions de la quête
                _ => Text("text.ui.guild.trial_top"),
            };
            var titles = n.Titles.Select(id => new TitleEntry(id, _guild.TryGetTitle(id, out GuildTitle t) ? Text(t.Name) : id, n.ShownTitle == id)).ToList();
            return new GuildCardView(true, rank.Id, Text(rank.Name), points, progress, Format("text.ui.guild.contracts_done", n.ContractsDone), ShownTitle, titles,
                trialText, trial == GuildRefusal.None);
        }

        /// <summary>Contrats du tableau du jour, avec le refus qu'aurait leur prise maintenant ; rien ne change.</summary>
        public IReadOnlyList<ContractEntry> GuildBoard() =>
            World.Board().Select(c => Entry(c, null, World.CanTakeContract(c.Id))).ToList();

        /// <summary>Contrats en cours, avec leur avancement.</summary>
        public IReadOnlyList<ContractEntry> ActiveContracts()
        {
            Narrative? n = World.Narrative;
            if (n is null) return Array.Empty<ContractEntry>();
            return n.ActiveContracts.Where(a => _guild.TryGetContract(a.Contract, out _)).Select(a => Entry(_guild.Contract(a.Contract), a.Progress, GuildRefusal.None)).ToList();
        }

        private ContractEntry Entry(GuildContract c, long? progress, GuildRefusal refusal)
        {
            Objective o = c.Objective;
            string target = o.Kind == ObjectiveKind.Deliver ? ItemName(o.Id) : Creature(o.Id);
            string objective = o.Kind == ObjectiveKind.Deliver
                ? Format("text.ui.guild.objective.deliver", target, o.Value, Text(_story.Npc(o.Npc!).Name))
                : Format("text.ui.guild.objective.defeat", target, o.Value);
            string money = c.Money > 0 ? MoneyText(c.Money) : "";
            string reward = (c.Xp > 0 ? Format("text.ui.guild.reward_xp", money, c.Points, c.Xp) : Format("text.ui.guild.reward", money, c.Points)).TrimStart(' ', '·');
            string? shown = null;
            double fraction = 0;
            if (progress is long p)
            {
                bool deliver = o.Kind == ObjectiveKind.Deliver;
                shown = deliver ? (p >= 1 ? Text("text.ui.guild.delivered") : Format("text.ui.guild.progress", World.Inventory.Available(o.Id), o.Value))
                    : Format("text.ui.guild.progress", p, o.Value);
                fraction = deliver ? (p >= 1 ? 1 : Math.Min(1, (double)World.Inventory.Available(o.Id) / o.Value)) : Math.Min(1, (double)p / o.Value);
            }
            return new ContractEntry(c.Id, Text(c.Name), Text("text.ui.guild.kind." + KindKey(c.Kind)), RankName(c.Rank), Text(c.Text), objective, reward, shown,
                fraction, refusal);
        }

        private static string KindKey(ContractKind kind) => kind switch
        {
            ContractKind.Delivery => "delivery",
            ContractKind.Bounty => "bounty",
            _ => "hunt",
        };

        /// <summary>Nom montré d'une créature ou d'une famille (« loup », « Le Borgne »).</summary>
        private static string Creature(string id) => DisplayNames.Of(id);

        public GuildRefusal JoinGuild() => GuildDone(World.JoinGuild(out IReadOnlyList<StoryEvent> events), events);

        public GuildRefusal TakeContract(string contract) => GuildDone(World.TakeContract(contract, out IReadOnlyList<StoryEvent> events), events);

        public GuildRefusal AbandonContract(string contract) => GuildDone(World.AbandonContract(contract, out IReadOnlyList<StoryEvent> events), events);

        public GuildRefusal StartTrial() => GuildDone(World.StartTrial(out IReadOnlyList<StoryEvent> events), events);

        /// <summary>Affiche le titre <paramref name="title"/> (un titre gagné), ou plus aucun (null).</summary>
        public GuildRefusal ShowTitle(string? title) => GuildDone(World.ShowTitle(title, out IReadOnlyList<StoryEvent> events), events);

        private GuildRefusal GuildDone(GuildRefusal refusal, IReadOnlyList<StoryEvent> events)
        {
            foreach (StoryEvent e in events) Story(e);
            if (events.Count > 0) _dirty = true;
            if (refusal != GuildRefusal.None) Refused(refusal);
            return refusal;
        }

        private GuildRefusal Refused(GuildRefusal refusal)
        {
            Say("text.ui.guild." + refusal.ToString().ToLowerInvariant());
            return refusal;
        }

        private string Format(string key, params object[] args) => string.Format(CultureInfo.InvariantCulture, Text(key), args);

        /// <summary>Messages des événements de la guilde.</summary>
        private void GuildMessage(StoryEvent e)
        {
            switch (e.Kind)
            {
                case StoryEventKind.GuildJoined:
                    Say("text.ui.guild.joined");
                    break;
                case StoryEventKind.GuildPoints:
                    Say("text.ui.guild.points_gained", e.Value);
                    break;
                case StoryEventKind.GuildPromoted:
                    GuildRank rank = _guild.Rank(e.Id);
                    Say("text.ui.guild.promoted", Text(rank.Name), _guild.TryGetTitle(rank.Title, out GuildTitle t) ? Text(t.Name) : rank.Title);
                    break;
                case StoryEventKind.ContractTaken:
                    Say("text.ui.guild.contract_taken", ContractName(e.Id));
                    break;
                case StoryEventKind.ContractAbandoned:
                    Say("text.ui.guild.contract_abandoned", ContractName(e.Id));
                    break;
                case StoryEventKind.ContractDone:
                    Say("text.ui.guild.contract_done", ContractName(e.Id));
                    break;
                case StoryEventKind.TitleShown:
                    if (e.Id.Length == 0) Say("text.ui.guild.title_hidden");
                    else Say("text.ui.guild.title_shown", _guild.TryGetTitle(e.Id, out GuildTitle shown) ? Text(shown.Name) : e.Id);
                    break;
            }
        }

        private string ContractName(string id) => _guild.TryGetContract(id, out GuildContract c) ? Text(c.Name) : id;
    }
}
