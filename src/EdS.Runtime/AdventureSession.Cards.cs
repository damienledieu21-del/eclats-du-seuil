using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EdS.Data.Saves;
using EdS.World;

namespace EdS.Runtime
{
    /// <summary>Sous-lot V2.32 : une carte du jeu des Trois Feux, telle que l'écran la montre (texte déjà traduit).</summary>
    public sealed class CardFace
    {
        public CardSuit Suit { get; }
        public int Value { get; }

        /// <summary>« Soleil 7 ».</summary>
        public string Label { get; }

        internal CardFace(Card card, string label)
        {
            Suit = card.Suit;
            Value = card.Value;
            Label = label;
        }
    }

    /// <summary>Sous-lot V2.32 : la table des Trois Feux telle que l'écran la montre (textes déjà traduits).</summary>
    public sealed class CardTableView
    {
        /// <summary>« Contre Mathurin · mise : 10 cuivre ».</summary>
        public string Header { get; }

        /// <summary>« Manches : toi 2, Mathurin 1 ».</summary>
        public string Score { get; }

        /// <summary>Ce qu'il faut faire, ou l'issue de la partie finie.</summary>
        public string Prompt { get; }

        /// <summary>La dernière manche jouée (« Tu as posé Soleil 7, Mathurin Brume 4 : manche gagnée. »), ou vide.</summary>
        public string LastRound { get; }

        /// <summary>Rappel des règles (couleurs et +3).</summary>
        public string Rules { get; }

        /// <summary>Cartes encore en main (vide quand la partie est finie).</summary>
        public IReadOnlyList<CardFace> Hand { get; }

        /// <summary>La partie est finie : l'écran montre l'issue jusqu'à ce que le joueur se lève (<see cref="AdventureSession.LeaveCardTable"/>).</summary>
        public bool Finished { get; }

        internal CardTableView(string header, string score, string prompt, string lastRound, string rules, IReadOnlyList<CardFace> hand, bool finished)
        {
            Header = header;
            Score = score;
            Prompt = prompt;
            LastRound = lastRound;
            Rules = rules;
            Hand = hand;
            Finished = finished;
        }
    }

    /// <summary>
    /// Sous-lot V2.32 : les cartes à la taverne vues par le client. Le dialogue d'un PNJ qui joue (à la taverne, le soir :
    /// <see cref="WorldSession.PlaysCardsNow"/>) propose, après les réponses de l'histoire, de s'asseoir pour 5, 10 ou 25 cuivre ; la table
    /// montre la main, les manches et l'issue. Le cœur (<see cref="WorldSession.SitDownAtCards"/>, <see cref="WorldSession.PlayCard"/>) reste
    /// seul juge ; ici, rien que l'affichage et les messages.
    /// </summary>
    public sealed partial class AdventureSession
    {
        /// <summary>Titre « As des Trois Feux » : identifiant de la ligne de la carte d'aventurier (jamais un titre de la guilde).</summary>
        public const string CardAceTitle = CardMatch.AceTitle;

        /// <summary>
        /// Source de l'entropie qui devient, la première fois qu'on s'assoit, la graine secrète des donnes (fournie par l'hôte, jamais
        /// montrée). Par défaut, tirée d'un identifiant unique ; les essais la fixent.
        /// </summary>
        public Func<ulong> CardEntropy { get; set; } = () => BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0);

        /// <summary>Issue de la dernière partie, montrée jusqu'à ce que le joueur se lève (affichage seulement, rien n'est sauvegardé).</summary>
        private (string Npc, int Stake, CardMatch Match, CardGameEnd End)? _cardEnd;

        /// <summary>
        /// Relecture indépendante de la PR #193 : la table vue, gardée d'une image à l'autre (la rejouer coûte), refaite après s'être assis,
        /// avoir posé une carte, s'être levé ou avoir demandé à quitter ; une nouvelle session (chargement) part sans.
        /// </summary>
        private CardTableView? _cardView;
        private bool _cardViewFresh;

        /// <summary>« Quitter la table » demandé une fois en pleine partie : la seconde fois, la partie et la mise sont perdues.</summary>
        private bool _leaveAsked;

        private void ForgetCardView()
        {
            _cardViewFresh = false;
            _cardView = null;
        }

        /// <summary>Le jeu de cartes rare est dû (sac plein à la dixième victoire).</summary>
        public bool RareDeckOwed => World.Cards is SavedCards c && c.IsAce && !c.DeckGiven;

        /// <summary>Le PNJ du dialogue ouvert joue aux cartes maintenant (taverne, soir) et aucune partie n'est en cours.</summary>
        private string? CardPartner =>
            Dialogue is EdS.World.DialogueView d && World.CardGame is null && _cardEnd is null && World.PlaysCardsNow(d.Npc) ? d.Npc : null;

        /// <summary>
        /// Réponses de cartes ajoutées au dialogue, après celles de l'histoire (<see cref="DialogueAnswers"/>, même numérotation que
        /// <see cref="Choose"/>) : une par mise, puis la remise du jeu rare s'il est dû.
        /// </summary>
        public IReadOnlyList<string> CardAnswers
        {
            get
            {
                if (CardPartner is null) return Array.Empty<string>();
                var list = CardMatch.Stakes.Select(s => string.Format(CultureInfo.InvariantCulture, Text("text.ui.cards.answer"), s)).ToList();
                if (RareDeckOwed) list.Add(Text("text.ui.cards.claim"));
                return list;
            }
        }

        /// <summary>Réponse de cartes de rang <paramref name="index"/> (après celles de l'histoire) : s'asseoir, ou réclamer le jeu rare.</summary>
        private DialogueRefusal ChooseCards(int index)
        {
            if (!(CardPartner is string npc) || index < 0) return DialogueRefusal.NoSuchChoice;
            if (index < CardMatch.Stakes.Count)
            {
                SitAtCards(npc, CardMatch.Stakes[index]);
                return DialogueRefusal.None;
            }
            if (index == CardMatch.Stakes.Count && RareDeckOwed)
            {
                if (World.ClaimRareDeck() == CardTableRefusal.None)
                {
                    _dirty = true;
                    Say("text.ui.cards.deck_given");
                }
                else Say("text.ui.no_room");
                return DialogueRefusal.None;
            }
            return DialogueRefusal.NoSuchChoice;
        }

        private void SitAtCards(string npc, int stake)
        {
            CardTableRefusal refusal = World.SitDownAtCards(npc, stake, CardEntropy());
            string name = Text(_story.Npc(npc).Name);
            switch (refusal)
            {
                case CardTableRefusal.None:
                    _dirty = true;
                    _leaveAsked = false;
                    ForgetCardView();
                    CloseDialogue();
                    return;
                case CardTableRefusal.PurseFull: Say("text.ui.purse_full"); return;
                case CardTableRefusal.NotEnoughMoney: Say("text.ui.not_enough_money"); return;
                case CardTableRefusal.NoMoreGamesToday: Say("text.ui.cards.refused.games", name); return;
                case CardTableRefusal.DailyCapReached: Say("text.ui.cards.refused.cap", World.CardGainsLeft); return;
                case CardTableRefusal.NotAtTavern: Say("text.ui.cards.refused.tavern", name); return;
                case CardTableRefusal.TooFar: Say("text.ui.too_far"); return;
                case CardTableRefusal.InCombat: Say("text.ui.cards.refused.combat"); return;
                default: Say("text.ui.cards.refused.other"); return;
            }
        }

        /// <summary>La table ouverte (partie en cours, ou issue de la dernière partie à lire), ou null.</summary>
        public CardTableView? CardTable
        {
            get
            {
                if (_cardViewFresh) return _cardView;
                if (World.Cards is SavedCards cards && cards.Game is SavedCardGame game && World.CardGame is CardMatch match)
                    _cardView = View(game.Npc, game.Stake, match, null);
                else _cardView = _cardEnd is { } end ? View(end.Npc, end.Stake, end.Match, end.End) : null;
                _cardViewFresh = true;
                return _cardView;
            }
        }

        private CardTableView View(string npc, int stake, CardMatch match, CardGameEnd? end)
        {
            string name = Text(_story.Npc(npc).Name);
            string prompt = end is null
                ? Format("text.ui.cards.round", match.Rounds.Count + 1, CardMatch.HandSize)
                : end.Outcome > 0 ? Format("text.ui.cards.won", end.Payout) : end.Outcome == 0 ? Text("text.ui.cards.draw") : Format("text.ui.cards.lost", name);
            if (end != null && end.BecameAce) prompt += " " + Text("text.ui.cards.ace");
            if (end is null && _leaveAsked) prompt = Text("text.ui.cards.leave_confirm");
            string last = "";
            if (match.Rounds.Count > 0)
            {
                CardRound r = match.Rounds[match.Rounds.Count - 1];
                string verdict = Text(r.Winner > 0 ? "text.ui.cards.round_won" : r.Winner < 0 ? "text.ui.cards.round_lost" : "text.ui.cards.round_tie");
                last = Format("text.ui.cards.last", CardLabel(r.Player), name, CardLabel(r.Npc), verdict);
            }
            IReadOnlyList<CardFace> hand = end is null ? match.PlayerHand.Select(c => new CardFace(c, CardLabel(c))).ToList() : (IReadOnlyList<CardFace>)Array.Empty<CardFace>();
            return new CardTableView(Format("text.ui.cards.against", name, stake), Format("text.ui.cards.score", match.PlayerWins, name, match.NpcWins), prompt, last,
                Text("text.ui.cards.rules"), hand, end != null);
        }

        /// <summary>« Soleil 7 ».</summary>
        public string CardLabel(Card card) =>
            Format("text.ui.cards.card", Text(card.Suit == CardSuit.Sun ? "text.ui.cards.suit.sun" : card.Suit == CardSuit.Moon ? "text.ui.cards.suit.moon" : "text.ui.cards.suit.mist"), card.Value);

        /// <summary>Pose la carte de la place <paramref name="index"/> de la main ; à la dernière manche, l'issue est annoncée et reste à l'écran.</summary>
        public CardTableRefusal PlayCardAt(int index)
        {
            SavedCardGame? game = World.Cards?.Game;
            CardTableRefusal refusal = World.PlayCard(index, out _, out CardGameEnd? end);
            _leaveAsked = false;
            ForgetCardView();
            if (refusal == CardTableRefusal.None) _dirty = true;
            else if (refusal == CardTableRefusal.PurseFull) Say("text.ui.purse_full");
            else if (refusal == CardTableRefusal.InCombat) Say("text.ui.cards.refused.combat");
            if (end != null && game != null)
            {
                _cardEnd = (game.Npc, game.Stake, end.Match, end);
                if (end.Payout > 0) Say("text.ui.cards.coins", end.Payout);
                if (end.BecameAce) Say("text.ui.cards.ace");
                if (end.DeckGiven) Say("text.ui.cards.deck_given");
                else if (end.BecameAce) Say("text.ui.cards.deck_owed");
            }
            return refusal;
        }

        /// <summary>
        /// Se lever (bouton, ou Échap : relecture indépendante de la PR #193) : après l'issue, ferme la table ; en pleine partie, la première
        /// fois, demande confirmation (la table le dit) ; la seconde, quitte la table (partie et mise perdues). Poser une carte annule la demande.
        /// </summary>
        public void LeaveCardTable()
        {
            ForgetCardView();
            if (_cardEnd != null)
            {
                _cardEnd = null;
                return;
            }
            if (World.CardGame is null) return;
            if (!_leaveAsked)
            {
                _leaveAsked = true;
                return;
            }
            _leaveAsked = false;
            if (World.ForfeitCards() == CardTableRefusal.None)
            {
                _dirty = true;
                Say("text.ui.cards.left");
            }
        }

        /// <summary>Ligne du titre « As des Trois Feux » sur la carte d'aventurier (titre actif possible depuis la sauvegarde v26 ; pas de la guilde), ou null.</summary>
        private TitleEntry? CardAceEntry => World.IsCardAce ? new TitleEntry(CardAceTitle, Text("text.ui.cards.ace_title"), World.Cards!.Shown) : null;
    }
}
