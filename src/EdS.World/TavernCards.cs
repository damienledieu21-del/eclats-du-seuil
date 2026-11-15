using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Simulation.Randomness;

namespace EdS.World
{
    /// <summary>Vague 4 (3), cartes à la taverne : les trois couleurs du jeu des Trois Feux.</summary>
    public enum CardSuit
    {
        /// <summary>Le soleil dissipe la brume.</summary>
        Sun = 0,

        /// <summary>La lune éclipse le soleil.</summary>
        Moon = 1,

        /// <summary>La brume voile la lune.</summary>
        Mist = 2,
    }

    /// <summary>Une carte : une couleur et une valeur de 1 à <see cref="CardMatch.MaxValue"/>.</summary>
    public readonly struct Card : IEquatable<Card>
    {
        public CardSuit Suit { get; }
        public int Value { get; }

        public Card(CardSuit suit, int value)
        {
            if (!Enum.IsDefined(typeof(CardSuit), suit)) throw new ArgumentOutOfRangeException(nameof(suit));
            if (value < 1 || value > CardMatch.MaxValue) throw new ArgumentOutOfRangeException(nameof(value), value, $"Valeur de 1 à {CardMatch.MaxValue}.");
            Suit = suit;
            Value = value;
        }

        public bool Equals(Card other) => other.Suit == Suit && other.Value == Value;
        public override bool Equals(object? obj) => obj is Card c && Equals(c);
        public override int GetHashCode() => (int)Suit * 16 + Value;
        public override string ToString() => $"{Suit} {Value}";
    }

    /// <summary>Manière de jouer d'un PNJ (déterministe : il ne voit jamais la carte du joueur avant de choisir).</summary>
    public enum CardStyle
    {
        /// <summary>Garde ses fortes cartes : joue sa plus faible.</summary>
        Prudent = 0,

        /// <summary>Attaque d'entrée : joue sa plus forte.</summary>
        Bold = 1,

        /// <summary>Joue sa plus forte quand il est mené, sa plus faible quand il mène ou à égalité.</summary>
        Cunning = 2,
    }

    public enum CardRefusal
    {
        None = 0,

        /// <summary>La partie est finie : plus rien ne se joue.</summary>
        Finished = 1,

        /// <summary>Aucune carte à cette place de la main.</summary>
        NoSuchCard = 2,
    }

    /// <summary>Une manche jouée : les deux cartes, leurs forces, et le gagnant (+1 le joueur, −1 le PNJ, 0 personne).</summary>
    public sealed class CardRound
    {
        public Card Player { get; }
        public Card Npc { get; }
        public int PlayerScore { get; }
        public int NpcScore { get; }
        public int Winner { get; }

        internal CardRound(Card player, Card npc)
        {
            Player = player;
            Npc = npc;
            PlayerScore = CardMatch.Score(player, npc);
            NpcScore = CardMatch.Score(npc, player);
            Winner = Math.Sign(PlayerScore - NpcScore);
        }
    }

    /// <summary>
    /// Vague 4 (3), cartes à la taverne (proposition de Claude, en attente de l'arbitrage de Damien) : le jeu des <b>Trois Feux</b>, une
    /// partie contre un PNJ. Paquet de 27 cartes (trois couleurs, valeurs 1 à 9), mêlé par <see cref="Pcg32"/> sur une clé stable (personnage,
    /// PNJ, jour du monde, compteur : <see cref="Key"/>) ; cinq cartes chacun ; cinq manches : le joueur choisit une carte, le PNJ la sienne selon
    /// sa manière (<see cref="CardStyle"/>, sans voir celle du joueur), puis on les retourne. Force d'une carte : sa valeur, plus
    /// <see cref="SuitBonus"/> si sa couleur l'emporte sur l'autre (soleil sur brume, brume sur lune, lune sur soleil ; même couleur : rien).
    /// La plus forte prend la manche, égalité : personne. Au bout des cinq manches, celui qui en a pris le plus gagne la partie (égalité :
    /// partie nulle). Tout est déterministe : la même clé et les mêmes choix donnent toujours la même partie. Un coup refusé ne change rien.
    /// </summary>
    public sealed class CardMatch
    {
        public const int MaxValue = 9, HandSize = 5, SuitBonus = 3;

        /// <summary>Nombre de cartes du paquet (trois couleurs de <see cref="MaxValue"/> cartes).</summary>
        public const int DeckSize = 3 * MaxValue;

        private readonly List<Card> _player, _npc;
        private readonly List<CardRound> _rounds = new List<CardRound>();

        public CardStyle Style { get; }
        public IReadOnlyList<Card> PlayerHand => _player.AsReadOnly();

        /// <summary>Main du PNJ (cachée au joueur par l'interface ; exposée pour les essais et le serveur).</summary>
        public IReadOnlyList<Card> NpcHand => _npc.AsReadOnly();

        public IReadOnlyList<CardRound> Rounds => _rounds.AsReadOnly();
        public int PlayerWins => _rounds.Count(r => r.Winner > 0);
        public int NpcWins => _rounds.Count(r => r.Winner < 0);
        public bool IsOver => _rounds.Count >= HandSize;

        /// <summary>Issue d'une partie finie : +1 le joueur gagne, −1 le PNJ, 0 nulle (et 0 tant qu'elle n'est pas finie).</summary>
        public int Outcome => IsOver ? Math.Sign(PlayerWins - NpcWins) : 0;

        public CardMatch(string key, CardStyle style)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            if (!Enum.IsDefined(typeof(CardStyle), style)) throw new ArgumentOutOfRangeException(nameof(style));
            Style = style;
            IReadOnlyList<Card> deck = Shuffled(key);
            _player = deck.Take(HandSize).ToList();
            _npc = deck.Skip(HandSize).Take(HandSize).ToList();
        }

        /// <summary>Clé stable d'une partie : le personnage, le PNJ, le jour du monde et le nombre de parties déjà jouées ce jour-là.</summary>
        public static string Key(string character, string npc, long day, int counter) => $"cards:{character}:{npc}:{day}:{counter}";

        /// <summary>Le paquet entier, mêlé (Fisher-Yates) par <see cref="Pcg32"/> sur la clé.</summary>
        public static IReadOnlyList<Card> Shuffled(string key)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            var deck = new List<Card>(DeckSize);
            foreach (CardSuit suit in new[] { CardSuit.Sun, CardSuit.Moon, CardSuit.Mist })
                for (int v = 1; v <= MaxValue; v++) deck.Add(new Card(suit, v));
            var random = new Pcg32(Loot.Fnv1a(key), Loot.Fnv1a("cards:trois_feux"));
            for (int i = deck.Count - 1; i > 0; i--)
            {
                int j = (int)random.NextBounded((uint)(i + 1));
                (deck[i], deck[j]) = (deck[j], deck[i]);
            }
            return deck.AsReadOnly();
        }

        /// <summary>La couleur <paramref name="a"/> l'emporte-t-elle sur <paramref name="b"/> ?</summary>
        public static bool Beats(CardSuit a, CardSuit b) =>
            (a == CardSuit.Sun && b == CardSuit.Mist) || (a == CardSuit.Mist && b == CardSuit.Moon) || (a == CardSuit.Moon && b == CardSuit.Sun);

        /// <summary>Force de <paramref name="mine"/> face à <paramref name="theirs"/>.</summary>
        public static int Score(Card mine, Card theirs) => mine.Value + (Beats(mine.Suit, theirs.Suit) ? SuitBonus : 0);

        /// <summary>
        /// Joue la carte de la place <paramref name="index"/> de la main du joueur ; le PNJ joue la sienne (<see cref="NpcChoice"/>, choisie
        /// sans voir celle du joueur). Refusé sans rien changer si la partie est finie ou si la place est vide.
        /// </summary>
        public CardRefusal Play(int index, out CardRound? round)
        {
            round = null;
            if (IsOver) return CardRefusal.Finished;
            if (index < 0 || index >= _player.Count) return CardRefusal.NoSuchCard;
            int npcIndex = NpcChoice(_npc, Style, NpcWins - PlayerWins);
            Card mine = _player[index], theirs = _npc[npcIndex];
            _player.RemoveAt(index);
            _npc.RemoveAt(npcIndex);
            round = new CardRound(mine, theirs);
            _rounds.Add(round);
            return CardRefusal.None;
        }

        /// <summary>
        /// Place de la carte que joue le PNJ : la plus faible (prudent, rusé qui mène ou à égalité) ou la plus forte (hardi, rusé mené) ;
        /// à valeur égale, la première couleur dans l'ordre soleil, lune, brume, puis la première place.
        /// </summary>
        /// <param name="lead">Manches gagnées par le PNJ moins celles du joueur.</param>
        public static int NpcChoice(IReadOnlyList<Card> hand, CardStyle style, int lead)
        {
            if (hand is null || hand.Count == 0) throw new ArgumentException("Main vide.", nameof(hand));
            bool high = style == CardStyle.Bold || (style == CardStyle.Cunning && lead < 0);
            int best = 0;
            for (int i = 1; i < hand.Count; i++)
            {
                int c = hand[i].Value.CompareTo(hand[best].Value);
                if (high ? c > 0 : c < 0) best = i;
                else if (c == 0 && hand[i].Suit < hand[best].Suit) best = i;
            }
            return best;
        }
    }
}
