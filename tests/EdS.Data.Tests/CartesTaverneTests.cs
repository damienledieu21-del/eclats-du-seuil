using EdS.World;

namespace EdS.Data.Tests;

/// <summary>
/// Vague 4 (3), cartes à la taverne (proposition de Claude, en attente de l'arbitrage de Damien) : le jeu des Trois Feux, déterministe,
/// contre un PNJ (<see cref="CardMatch"/>).
/// </summary>
[Trait("Category", "World")]
public class CartesTaverneTests
{
    private static string AnyKey(int i) => CardMatch.Key("bastion", "npc.aubergiste", 40, i);

    [Fact]
    public void Paquet_de_27_cartes_mele_toujours_de_meme_pour_la_meme_cle()
    {
        IReadOnlyList<Card> deck = CardMatch.Shuffled(AnyKey(0));
        Assert.Equal(CardMatch.DeckSize, deck.Count);
        Assert.Equal(27, deck.Distinct().Count());
        Assert.Equal(deck, CardMatch.Shuffled(AnyKey(0)));
        Assert.NotEqual(deck, CardMatch.Shuffled(AnyKey(1)));
        var match = new CardMatch(AnyKey(0), CardStyle.Prudent);
        Assert.Equal(deck.Take(5), match.PlayerHand);
        Assert.Equal(deck.Skip(5).Take(5), match.NpcHand);
        Assert.Empty(match.PlayerHand.Intersect(match.NpcHand));
    }

    [Fact]
    public void Soleil_sur_brume_brume_sur_lune_lune_sur_soleil()
    {
        Assert.True(CardMatch.Beats(CardSuit.Sun, CardSuit.Mist));
        Assert.True(CardMatch.Beats(CardSuit.Mist, CardSuit.Moon));
        Assert.True(CardMatch.Beats(CardSuit.Moon, CardSuit.Sun));
        foreach (CardSuit s in Enum.GetValues<CardSuit>()) Assert.False(CardMatch.Beats(s, s));
        // Un 5 de soleil (5 + 3) bat un 7 de brume ; un 6 de lune bat de justesse le 8 de soleil (9 contre 8) ; même couleur : la valeur.
        Assert.Equal((8, 7), (CardMatch.Score(new Card(CardSuit.Sun, 5), new Card(CardSuit.Mist, 7)), CardMatch.Score(new Card(CardSuit.Mist, 7), new Card(CardSuit.Sun, 5))));
        Assert.Equal((9, 8), (CardMatch.Score(new Card(CardSuit.Moon, 6), new Card(CardSuit.Sun, 8)), CardMatch.Score(new Card(CardSuit.Sun, 8), new Card(CardSuit.Moon, 6))));
        Assert.Equal(4, CardMatch.Score(new Card(CardSuit.Moon, 4), new Card(CardSuit.Moon, 9)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Card(CardSuit.Sun, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Card(CardSuit.Sun, 10));
    }

    [Fact]
    public void Cinq_manches_puis_la_partie_est_finie_et_un_coup_refuse_ne_change_rien()
    {
        var match = new CardMatch(AnyKey(3), CardStyle.Bold);
        Card[] before = match.PlayerHand.ToArray();
        Assert.Equal(CardRefusal.NoSuchCard, match.Play(5, out CardRound? none));
        Assert.Equal(CardRefusal.NoSuchCard, match.Play(-1, out _));
        Assert.Null(none);
        Assert.Equal(before, match.PlayerHand);
        Assert.Empty(match.Rounds);
        for (int k = 0; k < CardMatch.HandSize; k++)
        {
            Assert.Equal(0, match.Outcome);
            Assert.Equal(CardRefusal.None, match.Play(0, out CardRound? r));
            Assert.Equal(Math.Sign(r!.PlayerScore - r.NpcScore), r.Winner);
        }
        Assert.True(match.IsOver);
        Assert.Empty(match.PlayerHand);
        Assert.Empty(match.NpcHand);
        Assert.Equal(Math.Sign(match.PlayerWins - match.NpcWins), match.Outcome);
        Assert.Equal(CardRefusal.Finished, match.Play(0, out _));
        Assert.Equal(5, match.Rounds.Count);
    }

    [Fact]
    public void Le_PNJ_joue_selon_sa_maniere_sans_voir_la_carte_du_joueur()
    {
        var hand = new[] { new Card(CardSuit.Mist, 4), new Card(CardSuit.Sun, 9), new Card(CardSuit.Moon, 2), new Card(CardSuit.Sun, 2) };
        Assert.Equal(3, CardMatch.NpcChoice(hand, CardStyle.Prudent, 0));   // le 2 de soleil avant le 2 de lune
        Assert.Equal(1, CardMatch.NpcChoice(hand, CardStyle.Bold, 0));
        Assert.Equal(1, CardMatch.NpcChoice(hand, CardStyle.Cunning, -1));   // mené : sa plus forte
        Assert.Equal(3, CardMatch.NpcChoice(hand, CardStyle.Cunning, 0));    // à égalité ou devant : sa plus faible
        // Le choix du PNJ ne dépend pas de la carte jouée par le joueur.
        var a = new CardMatch(AnyKey(7), CardStyle.Cunning);
        var b = new CardMatch(AnyKey(7), CardStyle.Cunning);
        a.Play(0, out CardRound? ra);
        b.Play(4, out CardRound? rb);
        Assert.Equal(ra!.Npc, rb!.Npc);
    }

    [Theory]
    [InlineData(CardStyle.Prudent)]
    [InlineData(CardStyle.Bold)]
    [InlineData(CardStyle.Cunning)]
    public void Aucune_maniere_imbattable_ni_donnee(CardStyle style)
    {
        // Le joueur joue toujours sa plus forte carte : sur 400 donnes, il gagne parfois, perd parfois ; même partie pour mêmes choix.
        int won = 0, lost = 0;
        for (int i = 0; i < 400; i++)
        {
            var m = new CardMatch(AnyKey(i), style);
            while (!m.IsOver) m.Play(CardMatch.NpcChoice(m.PlayerHand, CardStyle.Bold, 0), out _);
            if (m.Outcome > 0) won++;
            else if (m.Outcome < 0) lost++;
        }
        Assert.InRange(won, 80, 320);
        Assert.InRange(lost, 80, 320);
    }
}
