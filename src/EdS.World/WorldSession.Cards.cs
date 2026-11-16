using System;
using System.Linq;
using EdS.Data.Saves;

namespace EdS.World
{
    /// <summary>Cartes à la taverne : refus d'un geste à la table (un refus ne change rien : ni bourse, ni compteurs, ni partie).</summary>
    public enum CardTableRefusal
    {
        None = 0,

        /// <summary>PNJ mal formé (« npc.… »).</summary>
        UnknownNpc = 1,

        /// <summary>Mise hors de 5, 10 ou 25 cuivre.</summary>
        NoSuchStake = 2,

        /// <summary>La bourse ne couvre pas la mise.</summary>
        NotEnoughMoney = 3,

        /// <summary>Déjà <see cref="CardMatch.MaxGamesPerDay"/> parties contre ce PNJ aujourd'hui.</summary>
        NoMoreGamesToday = 4,

        /// <summary>Une victoire ferait passer les gains du jour au-delà de <see cref="CardMatch.DailyCap"/> cuivre.</summary>
        DailyCapReached = 5,

        /// <summary>Une partie est déjà en cours (on la finit d'abord).</summary>
        GameInProgress = 6,

        /// <summary>Aucune partie en cours.</summary>
        NoGame = 7,

        /// <summary>Aucune carte à cette place de la main.</summary>
        NoSuchCard = 8,

        /// <summary>On ne joue pas aux cartes en plein combat.</summary>
        InCombat = 9,

        /// <summary>La bourse ne peut pas recevoir le gain (plafond technique) : la dernière carte n'est pas jouée.</summary>
        PurseFull = 10,

        /// <summary>Rien à remettre (jeu rare déjà remis, ou moins de dix victoires), ou sac plein.</summary>
        NothingOwed = 11,
    }

    /// <summary>Fin d'une partie des Trois Feux : issue (+1 gagnée, 0 nulle, −1 perdue), cuivre rendu, et récompense rare.</summary>
    public sealed class CardGameEnd
    {
        public int Outcome { get; }

        /// <summary>Cuivre rendu à la fin : mise doublée si gagnée, mise rendue si nulle, rien si perdue.</summary>
        public int Payout { get; }

        /// <summary>Cette victoire est la dixième : titre « As des Trois Feux » gagné.</summary>
        public bool BecameAce { get; }

        /// <summary>Le jeu de cartes rare vient d'entrer dans le sac (faux si le sac était plein : il reste dû, <see cref="WorldSession.ClaimRareDeck"/>).</summary>
        public bool DeckGiven { get; }

        internal CardGameEnd(int outcome, int payout, bool becameAce, bool deckGiven)
        {
            Outcome = outcome;
            Payout = payout;
            BecameAce = becameAce;
            DeckGiven = deckGiven;
        }
    }

    public sealed partial class WorldSession
    {
        /// <summary>Cartes à la taverne (sauvegarde v25) : l'état des cartes du personnage, ou null s'il ne s'est jamais assis.</summary>
        public SavedCards? Cards => _character.Cards;

        /// <summary>Titre « As des Trois Feux » (dix victoires ; décision de Damien du 15 novembre 2026).</summary>
        public bool IsCardAce => _character.Cards?.IsAce == true;

        /// <summary>
        /// La partie en cours, rejouée depuis la sauvegarde (même donne, mêmes cartes jouées : décision de Damien du 16 novembre 2026, la
        /// partie en cours est enregistrée), ou null.
        /// </summary>
        public CardMatch? CardGame => _character.Cards is SavedCards c && c.Game is SavedCardGame g ? Replay(c, g) : null;

        /// <summary>Parties encore possibles contre <paramref name="npc"/> aujourd'hui.</summary>
        public int CardGamesLeft(string npc) => CardMatch.MaxGamesPerDay - (_character.Cards?.PlayedOn(Day, npc) ?? 0);

        /// <summary>Cuivre que la taverne peut encore faire gagner aujourd'hui (plafond de 150 cuivre par jour).</summary>
        public int CardGainsLeft => CardMatch.DailyCap - (_character.Cards?.GainedOn(Day) ?? 0);

        private CardMatch Replay(SavedCards cards, SavedCardGame game)
        {
            var match = new CardMatch(CardMatch.Key(cards.Secret, _character.Id, game.Npc, game.Day, game.Game), (CardStyle)game.Style);
            foreach (int move in game.Moves)
                if (match.Play(move, out _) != CardRefusal.None) throw new InvalidOperationException("Partie de la sauvegarde impossible à rejouer.");
            return match;
        }

        /// <summary>
        /// S'asseoir contre <paramref name="npc"/> pour une mise de <paramref name="stake"/> cuivre (5, 10 ou 25), prise dès maintenant. La
        /// première fois, <paramref name="entropy"/> (fourni par l'hôte, jamais montré) devient la graine secrète des donnes du personnage ;
        /// ensuite, il est ignoré. Refusé sans rien changer : partie déjà en cours, en combat, mise inconnue, bourse trop légère,
        /// trois parties déjà jouées contre ce PNJ aujourd'hui, ou gains du jour qui passeraient le plafond si l'on gagnait.
        /// </summary>
        public CardTableRefusal SitDownAtCards(string npc, int stake, ulong entropy)
        {
            if (!SavedCards.IsNpc(npc)) return CardTableRefusal.UnknownNpc;
            if (!CardMatch.Stakes.Contains(stake)) return CardTableRefusal.NoSuchStake;
            SavedCards cards = _character.Cards ?? SavedCards.Fresh(entropy);
            if (cards.Game != null) return CardTableRefusal.GameInProgress;
            if (InCombat) return CardTableRefusal.InCombat;
            long day = Day;
            if (day < 0 || day > SavedEvent.MaxDay) return CardTableRefusal.NoMoreGamesToday;
            int played = cards.PlayedOn(day, npc);
            if (played >= CardMatch.MaxGamesPerDay) return CardTableRefusal.NoMoreGamesToday;
            if (checked(cards.GainedOn(day) + stake) > CardMatch.DailyCap) return CardTableRefusal.DailyCapReached;
            if (stake > _character.Money) return CardTableRefusal.NotEnoughMoney;
            var game = new SavedCardGame(npc, (int)CardMatch.StyleOf(npc), day, played, stake, Array.Empty<int>());
            _character = _character.WithMoney(checked(_character.Money - stake)).WithCards(cards.WithStart(game));
            return CardTableRefusal.None;
        }

        /// <summary>
        /// Joue la carte de la place <paramref name="index"/> ; le PNJ joue la sienne. À la cinquième manche, la partie est réglée : mise
        /// doublée si gagnée (le gain compte dans le plafond du jour), rendue si nulle, perdue sinon ; la dixième victoire donne le titre et le
        /// jeu de cartes rare (s'il tient dans le sac ; sinon il reste dû). Refusé sans rien changer : aucune partie, place vide, ou bourse
        /// qui ne peut pas recevoir le gain.
        /// </summary>
        public CardTableRefusal PlayCard(int index, out CardRound? round, out CardGameEnd? end)
        {
            round = null;
            end = null;
            if (!(_character.Cards is SavedCards cards) || !(cards.Game is SavedCardGame game)) return CardTableRefusal.NoGame;
            CardMatch match = Replay(cards, game);
            if (index < 0 || index >= match.PlayerHand.Count) return CardTableRefusal.NoSuchCard;
            if (match.Play(index, out CardRound? played) != CardRefusal.None) return CardTableRefusal.NoSuchCard;
            if (!match.IsOver)
            {
                _character = _character.WithCards(cards.WithGame(game.WithMove(index)));
                round = played;
                return CardTableRefusal.None;
            }
            int outcome = match.Outcome;
            int payout = outcome > 0 ? checked(2 * game.Stake) : outcome == 0 ? game.Stake : 0;
            if (!Coins.TryAdd(_character.Money, payout, out long purse)) return CardTableRefusal.PurseFull;
            SavedCards after = cards.WithEnd(outcome > 0, outcome > 0 ? game.Stake : 0);
            bool becameAce = after.IsAce && !cards.IsAce;
            _character = _character.WithMoney(purse).WithCards(after);
            round = played;
            bool deck = after.IsAce && !after.DeckGiven && GiveRareDeck();
            end = new CardGameEnd(outcome, payout, becameAce, deck);
            return CardTableRefusal.None;
        }

        /// <summary>Quitter la table en pleine partie : la partie est perdue (mise perdue), et compte parmi celles du jour.</summary>
        public CardTableRefusal ForfeitCards()
        {
            if (!(_character.Cards is SavedCards cards) || cards.Game is null) return CardTableRefusal.NoGame;
            _character = _character.WithCards(cards.WithEnd(false, 0));
            return CardTableRefusal.None;
        }

        /// <summary>Remet le jeu de cartes rare resté dû (sac plein à la dixième victoire). Refusé sans rien changer s'il n'est pas dû ou si le sac est plein.</summary>
        public CardTableRefusal ClaimRareDeck()
        {
            if (!(_character.Cards is SavedCards cards) || !cards.IsAce || cards.DeckGiven) return CardTableRefusal.NothingOwed;
            return GiveRareDeck() ? CardTableRefusal.None : CardTableRefusal.NothingOwed;
        }

        private bool GiveRareDeck()
        {
            if (!(_character.Cards is SavedCards cards) || !_items.TryGetItem(CardMatch.RareDeck, out _)) return false;
            if (!Inventory.CanAdd(CardMatch.RareDeck, 1) || Inventory.Add(CardMatch.RareDeck, 1) != InventoryRefusal.None) return false;
            _character = _character.WithCards(cards.WithDeckGiven());
            return true;
        }
    }
}
