using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace EdS.Data.Saves
{
    /// <summary>
    /// Cartes à la taverne (sauvegarde v25) : la partie des Trois Feux en cours, enregistrée dans la sauvegarde (décision de Damien du
    /// 16 novembre 2026). La donne se recalcule de la clé (graine secrète, personnage, PNJ, jour, numéro de la partie du jour) ; seules les
    /// places jouées par le joueur sont gardées (<see cref="Moves"/>), la partie se rejoue à l'identique au chargement.
    /// </summary>
    public sealed class SavedCardGame
    {
        public string Npc { get; }

        /// <summary>Manière du PNJ (0 prudent, 1 hardi, 2 rusé ; voir <c>EdS.World.CardStyle</c>).</summary>
        public int Style { get; }

        /// <summary>Jour du monde de la donne.</summary>
        public long Day { get; }

        /// <summary>Numéro de la partie du jour contre ce PNJ (de 0 à <see cref="SavedCards.MaxGamesPerDay"/> − 1).</summary>
        public int Game { get; }

        /// <summary>Mise prise dès l'entrée en partie (une de <see cref="SavedCards.Stakes"/>).</summary>
        public int Stake { get; }

        /// <summary>Places jouées par le joueur, dans l'ordre (la i-ème de 0 à <see cref="SavedCards.HandSize"/> − 1 − i) ; au plus 4 : une partie de cinq manches jouées est réglée.</summary>
        public IReadOnlyList<int> Moves { get; }

        public SavedCardGame(string npc, int style, long day, int game, int stake, IEnumerable<int> moves)
        {
            if (!SavedCards.IsNpc(npc)) throw new ArgumentException($"PNJ « {npc} » hors du format des sauvegardes.", nameof(npc));
            if (style < 0 || style > SavedCards.MaxStyle) throw new ArgumentOutOfRangeException(nameof(style), style, $"Manière hors de [0, {SavedCards.MaxStyle}].");
            if (day < 0 || day > SavedEvent.MaxDay) throw new ArgumentOutOfRangeException(nameof(day), day, $"Jour hors de [0, {SavedEvent.MaxDay}].");
            if (game < 0 || game >= SavedCards.MaxGamesPerDay) throw new ArgumentOutOfRangeException(nameof(game), game, $"Partie du jour hors de [0, {SavedCards.MaxGamesPerDay - 1}].");
            if (!SavedCards.Stakes.Contains(stake)) throw new ArgumentOutOfRangeException(nameof(stake), stake, $"Mise : {string.Join(", ", SavedCards.Stakes)} cuivre.");
            var list = (moves ?? throw new ArgumentNullException(nameof(moves))).ToList();
            if (!SavedCards.AreMoves(list)) throw new ArgumentException("Places jouées hors des mains (au plus 4, la i-ème de 0 à 4 − i).", nameof(moves));
            Npc = npc;
            Style = style;
            Day = day;
            Game = game;
            Stake = stake;
            Moves = new ReadOnlyCollection<int>(list);
        }

        /// <summary>La même partie, une place jouée de plus.</summary>
        public SavedCardGame WithMove(int index) => new SavedCardGame(Npc, Style, Day, Game, Stake, Moves.Concat(new[] { index }));

        public override bool Equals(object? obj) => obj is SavedCardGame o && o.Npc == Npc && o.Style == Style && o.Day == Day && o.Game == Game && o.Stake == Stake
            && o.Moves.SequenceEqual(Moves);

        public override int GetHashCode() => unchecked(((Npc.GetHashCode() * 31 + Style) * 31 + (int)Day) * 31 + Game * 7 + Stake + Moves.Count);
    }

    /// <summary>
    /// Cartes à la taverne (sauvegarde v25, <c>character.cards</c> ; décisions de Damien du 15 et du 16 novembre 2026) : graine secrète des
    /// donnes, victoires (10 donnent le jeu de cartes rare et le titre « As des Trois Feux »), jeu rare remis, parties jouées contre chaque PNJ
    /// le jour <see cref="Day"/> (au plus <see cref="MaxGamesPerDay"/>), gains de la taverne ce jour-là (au plus <see cref="DailyCap"/>
    /// cuivre), partie en cours. Les compteurs du jour valent pour <see cref="Day"/> seulement : un autre jour, ils repartent de zéro.
    /// </summary>
    public sealed class SavedCards
    {
        /// <summary>Décision de Damien du 15 novembre 2026, confirmée le 16 : mises possibles, en cuivre.</summary>
        public static readonly IReadOnlyList<int> Stakes = Array.AsReadOnly(new[] { 5, 10, 25 });

        /// <summary>Décision de Damien du 15 novembre 2026, confirmée le 16 : parties au plus par PNJ et par jour du monde.</summary>
        public const int MaxGamesPerDay = 3;

        /// <summary>Décision de Damien du 16 novembre 2026 : gains de la taverne au plus par jour du monde, en cuivre.</summary>
        public const int DailyCap = 150;

        /// <summary>Décision de Damien du 15 novembre 2026, confirmée le 16 : victoires qui donnent le jeu rare et le titre.</summary>
        public const int VictoriesForReward = 10;

        public const int HandSize = 5, MaxStyle = 2, MaxNpcLength = 64, MaxOpponents = 32;
        public const long MaxWins = 1_000_000;

        private static readonly Regex NpcId = new Regex(@"^npc(\.[a-z0-9_]+)+\z", RegexOptions.CultureInvariant);
        private static readonly Regex SecretHex = new Regex(@"^[0-9a-f]{16}\z", RegexOptions.CultureInvariant);

        public static bool IsNpc(string? id) => id != null && id.Length <= MaxNpcLength && NpcId.IsMatch(id);

        /// <summary>La graine au format de la sauvegarde : 16 chiffres hexadécimaux en minuscules.</summary>
        public static bool IsSecret(string? hex) => hex != null && SecretHex.IsMatch(hex);

        public static string SecretText(ulong secret) => secret.ToString("x16", System.Globalization.CultureInfo.InvariantCulture);

        public static ulong ParseSecret(string hex) => IsSecret(hex)
            ? ulong.Parse(hex, System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture)
            : throw new ArgumentException("Graine : 16 chiffres hexadécimaux en minuscules.", nameof(hex));

        /// <summary>Places jouées valides : au plus <see cref="HandSize"/> − 1, la i-ème de 0 à <see cref="HandSize"/> − 1 − i.</summary>
        public static bool AreMoves(IReadOnlyList<int>? moves)
        {
            if (moves is null || moves.Count >= HandSize) return false;
            for (int i = 0; i < moves.Count; i++)
                if (moves[i] < 0 || moves[i] >= HandSize - i) return false;
            return true;
        }

        public ulong Secret { get; }
        public long Wins { get; }

        /// <summary>Le jeu de cartes rare a été remis (il l'est à la dixième victoire, ou plus tard si le sac était plein).</summary>
        public bool DeckGiven { get; }

        /// <summary>Titre « As des Trois Feux » : gagné à la dixième victoire, gardé pour toujours.</summary>
        public bool IsAce => Wins >= VictoriesForReward;

        /// <summary>Jour du monde des compteurs <see cref="Played"/> et <see cref="Gained"/>.</summary>
        public long Day { get; }

        /// <summary>Parties commencées le jour <see cref="Day"/>, par PNJ (de 1 à <see cref="MaxGamesPerDay"/>), rangées par PNJ.</summary>
        public IReadOnlyDictionary<string, int> Played { get; }

        /// <summary>Gains du jour <see cref="Day"/> (la mise de chaque partie gagnée, au-delà de la mise rendue), de 0 à <see cref="DailyCap"/>.</summary>
        public int Gained { get; }

        public SavedCardGame? Game { get; }

        public SavedCards(ulong secret, long wins, bool deckGiven, long day, IReadOnlyDictionary<string, int>? played, int gained, SavedCardGame? game)
        {
            if (wins < 0 || wins > MaxWins) throw new ArgumentOutOfRangeException(nameof(wins), wins, $"Victoires hors de [0, {MaxWins}].");
            if (deckGiven && wins < VictoriesForReward) throw new ArgumentException($"Jeu rare remis avant {VictoriesForReward} victoires.", nameof(deckGiven));
            if (day < 0 || day > SavedEvent.MaxDay) throw new ArgumentOutOfRangeException(nameof(day), day, $"Jour hors de [0, {SavedEvent.MaxDay}].");
            var p = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, int> e in played ?? new Dictionary<string, int>())
            {
                if (!IsNpc(e.Key)) throw new ArgumentException($"PNJ « {e.Key} » hors du format des sauvegardes.", nameof(played));
                if (e.Value < 1 || e.Value > MaxGamesPerDay) throw new ArgumentOutOfRangeException(nameof(played), e.Value, $"Parties hors de [1, {MaxGamesPerDay}].");
                p[e.Key] = e.Value;
            }
            if (p.Count > MaxOpponents) throw new ArgumentException($"{p.Count} adversaires du jour, au plus {MaxOpponents}.", nameof(played));
            if (gained < 0 || gained > DailyCap) throw new ArgumentOutOfRangeException(nameof(gained), gained, $"Gains du jour hors de [0, {DailyCap}].");
            if (game != null && (game.Day != day || !p.TryGetValue(game.Npc, out int n) || n <= game.Game))
                throw new ArgumentException("Partie en cours hors des parties de son jour (même jour, comptée dans « played »).", nameof(game));
            Secret = secret;
            Wins = wins;
            DeckGiven = deckGiven;
            Day = day;
            Played = new ReadOnlyDictionary<string, int>(p);
            Gained = gained;
            Game = game;
        }

        /// <summary>État neuf : la graine <paramref name="secret"/>, rien de joué.</summary>
        public static SavedCards Fresh(ulong secret) => new SavedCards(secret, 0, false, 0, null, 0, null);

        /// <summary>Parties déjà commencées contre <paramref name="npc"/> le jour <paramref name="day"/>.</summary>
        public int PlayedOn(long day, string npc) => day == Day && Played.TryGetValue(npc, out int n) ? n : 0;

        /// <summary>Gains de la taverne le jour <paramref name="day"/>.</summary>
        public int GainedOn(long day) => day == Day ? Gained : 0;

        /// <summary>Une partie commence (compteurs remis à zéro si le jour a changé).</summary>
        public SavedCards WithStart(SavedCardGame game)
        {
            if (game is null) throw new ArgumentNullException(nameof(game));
            var p = game.Day == Day ? Played.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal) : new Dictionary<string, int>(StringComparer.Ordinal);
            p[game.Npc] = checked(PlayedOn(game.Day, game.Npc) + 1);
            return new SavedCards(Secret, Wins, DeckGiven, game.Day, p, GainedOn(game.Day), game);
        }

        /// <summary>La partie en cours avance (même partie, une place jouée de plus).</summary>
        public SavedCards WithGame(SavedCardGame game) => new SavedCards(Secret, Wins, DeckGiven, Day, Played, Gained, game ?? throw new ArgumentNullException(nameof(game)));

        /// <summary>La partie est réglée : plus de partie en cours ; une victoire compte, et ses gains comptent pour le jour <see cref="Day"/>.</summary>
        public SavedCards WithEnd(bool won, int gain) =>
            new SavedCards(Secret, won ? checked(Wins + 1) : Wins, DeckGiven, Day, Played, checked(Gained + gain), null);

        /// <summary>Le jeu de cartes rare est remis.</summary>
        public SavedCards WithDeckGiven() => new SavedCards(Secret, Wins, true, Day, Played, Gained, Game);

        public override bool Equals(object? obj) => obj is SavedCards o && o.Secret == Secret && o.Wins == Wins && o.DeckGiven == DeckGiven && o.Day == Day
            && o.Gained == Gained && o.Played.SequenceEqual(Played) && Equals(o.Game, Game);

        public override int GetHashCode() => unchecked((int)Secret * 31 + (int)Wins * 7 + (int)Day + Gained);
    }
}
