using EdS.World;

namespace EdS.Data.Tests;

/// <summary>
/// Vague 4 (3), cartes à la taverne (règles retenues par Damien le 15 novembre 2026) : le jeu des Trois Feux, déterministe, contre un PNJ
/// (<see cref="CardMatch"/>). Relecture indépendante de la PR #187 : clé à graine secrète et champs préfixés, cas limites, ordre optimal.
/// </summary>
[Trait("Category", "World")]
public class CartesTaverneTests
{
    private static string AnyKey(int i) => CardMatch.Key(0x5EED_0000UL + (ulong)i, "bastion", "npc.aubergiste", 40, i % CardMatch.MaxGamesPerDay);

    private static CardMatch Rigged(CardStyle style, Card[] player, Card[] npc) => CardMatch.ForTest(style, player, npc);

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

    // --- Relecture indépendante de la PR #187 ---------------------------------------------------------------------------------------

    [Fact]
    public void Cle_a_graine_secrete_numero_borne_et_champs_sans_ambiguite()
    {
        // Sans la graine du personnage, on ne refait pas la donne : deux graines, deux donnes.
        Assert.NotEqual(CardMatch.Shuffled(CardMatch.Key(1, "bastion", "npc.aubergiste", 40, 0)), CardMatch.Shuffled(CardMatch.Key(2, "bastion", "npc.aubergiste", 40, 0)));
        // Trois parties par PNJ et par jour (décision de Damien) : numéros 0 à 2.
        Assert.Throws<ArgumentOutOfRangeException>(() => CardMatch.Key(1, "bastion", "npc.aubergiste", 40, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => CardMatch.Key(1, "bastion", "npc.aubergiste", 40, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => CardMatch.Key(1, "bastion", "npc.aubergiste", -1, 0));
        Assert.Throws<ArgumentNullException>(() => CardMatch.Key(1, null!, "npc.aubergiste", 40, 0));
        // Champs préfixés de leur longueur : « a:b » + « c » ne se confond pas avec « a » + « b:c ».
        Assert.NotEqual(CardMatch.Key(1, "a:b", "c", 0, 0), CardMatch.Key(1, "a", "b:c", 0, 0));
        Assert.Equal((5, 10, 25), (CardMatch.Stakes[0], CardMatch.Stakes[1], CardMatch.Stakes[2]));
        Assert.Equal((3, 10), (CardMatch.MaxGamesPerDay, CardMatch.VictoriesForReward));
    }

    [Fact]
    public void Entrees_hors_du_jeu_refusees()
    {
        Assert.Throws<ArgumentNullException>(() => new CardMatch(null!, CardStyle.Prudent));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CardMatch(AnyKey(0), (CardStyle)7));
        Assert.Throws<ArgumentException>(() => CardMatch.NpcChoice(Array.Empty<Card>(), CardStyle.Bold, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => CardMatch.NpcChoice(new[] { new Card(CardSuit.Sun, 3) }, (CardStyle)(-1), 0));
        Assert.False(default(Card).IsValid);
        Assert.Throws<ArgumentException>(() => CardMatch.Score(default, new Card(CardSuit.Sun, 3)));
        Assert.Throws<ArgumentException>(() => CardMatch.NpcChoice(new[] { default(Card) }, CardStyle.Bold, 0));
    }

    [Fact]
    public void Manche_nulle_partie_nulle_et_victoire_cinq_a_zero()
    {
        // Manche nulle : 9 de soleil (9) contre 6 de lune (6 + 3).
        Assert.Equal(0, new CardRound(new Card(CardSuit.Sun, 9), new Card(CardSuit.Moon, 6)).Winner);
        // Partie nulle 2-2 : le PNJ hardi joue sa plus forte ; le joueur suit l'ordre de sa main : nulle, gagnée, gagnée, perdue, perdue.
        CardMatch draw = Rigged(CardStyle.Bold,
            new[] { new Card(CardSuit.Sun, 9), new Card(CardSuit.Sun, 8), new Card(CardSuit.Sun, 7), new Card(CardSuit.Mist, 1), new Card(CardSuit.Mist, 2) },
            new[] { new Card(CardSuit.Moon, 6), new Card(CardSuit.Mist, 5), new Card(CardSuit.Mist, 4), new Card(CardSuit.Sun, 3), new Card(CardSuit.Sun, 2) });
        while (!draw.IsOver) draw.Play(0, out _);
        Assert.Equal(new[] { 0, 1, 1, -1, -1 }, draw.Rounds.Select(r => r.Winner));
        Assert.Equal((2, 2, 0), (draw.PlayerWins, draw.NpcWins, draw.Outcome));
        // Victoire 5-0.
        CardMatch sweep = Rigged(CardStyle.Bold,
            new[] { new Card(CardSuit.Moon, 9), new Card(CardSuit.Moon, 8), new Card(CardSuit.Moon, 7), new Card(CardSuit.Moon, 6), new Card(CardSuit.Moon, 5) },
            new[] { new Card(CardSuit.Sun, 1), new Card(CardSuit.Sun, 2), new Card(CardSuit.Sun, 3), new Card(CardSuit.Sun, 4), new Card(CardSuit.Sun, 5) });
        while (!sweep.IsOver) sweep.Play(0, out _);
        Assert.Equal((5, 0, 1), (sweep.PlayerWins, sweep.NpcWins, sweep.Outcome));
    }

    [Fact]
    public void Le_ruse_bascule_quand_il_est_mene()
    {
        // Le rusé commence par sa plus faible ; mené après la première manche, il joue sa plus forte ; revenu à égalité, sa plus faible.
        CardMatch m = Rigged(CardStyle.Cunning,
            new[] { new Card(CardSuit.Moon, 9), new Card(CardSuit.Moon, 1), new Card(CardSuit.Moon, 2), new Card(CardSuit.Moon, 3), new Card(CardSuit.Moon, 4) },
            new[] { new Card(CardSuit.Moon, 5), new Card(CardSuit.Moon, 8), new Card(CardSuit.Moon, 6), new Card(CardSuit.Moon, 7), new Card(CardSuit.Sun, 9) });
        m.Play(0, out CardRound? first);
        Assert.Equal((5, 1), (first!.Npc.Value, first.Winner));
        m.Play(0, out CardRound? second);
        Assert.Equal((CardSuit.Sun, 9, -1), (second!.Npc.Suit, second.Npc.Value, second.Winner));   // mené : sa plus forte
        m.Play(0, out CardRound? third);
        Assert.Equal(6, third!.Npc.Value);                           // à égalité : sa plus faible
    }

    /// <summary>Meilleur résultat possible pour le joueur qui connaît tout (main et manière du PNJ) : recherche sur les 120 ordres.</summary>
    private static int Oracle(string key, CardStyle style)
    {
        int best = -1;
        foreach (int[] order in Orders(new List<int> { 0, 1, 2, 3, 4 }))
        {
            var m = new CardMatch(key, style);
            Card[] hand = m.PlayerHand.ToArray();
            foreach (int k in order) m.Play(m.PlayerHand.ToList().IndexOf(hand[k]), out _);
            best = Math.Max(best, m.Outcome);
            if (best == 1) break;
        }
        return best;
    }

    private static IEnumerable<int[]> Orders(List<int> left)
    {
        if (left.Count == 0) { yield return Array.Empty<int>(); yield break; }
        foreach (int x in left)
            foreach (int[] rest in Orders(left.Where(y => y != x).ToList()))
                yield return new[] { x }.Concat(rest).ToArray();
    }

    [Theory]
    [InlineData(CardStyle.Prudent)]
    [InlineData(CardStyle.Bold)]
    [InlineData(CardStyle.Cunning)]
    public void Sans_connaitre_la_main_du_PNJ_pas_de_victoire_assuree(CardStyle style)
    {
        // Trois façons de jouer sans rien savoir de la main du PNJ (forte d'abord, faible d'abord, en alternance) : aucune ne gagne plus de
        // deux parties sur trois. Qui connaît tout gagne bien plus souvent : d'où la graine secrète (la donne ne se calcule pas d'avance).
        const int deals = 150;
        int[] blind = new int[3];
        int oracle = 0;
        for (int i = 0; i < deals; i++)
        {
            string key = AnyKey(i);
            for (int s = 0; s < 3; s++)
            {
                var m = new CardMatch(key, style);
                int k = 0;
                while (!m.IsOver)
                {
                    CardStyle mine = s == 0 ? CardStyle.Bold : s == 1 ? CardStyle.Prudent : (k++ % 2 == 0 ? CardStyle.Bold : CardStyle.Prudent);
                    m.Play(CardMatch.NpcChoice(m.PlayerHand, mine, 0), out _);
                }
                if (m.Outcome > 0) blind[s]++;
            }
            if (Oracle(key, style) > 0) oracle++;
        }
        Assert.All(blind, w => Assert.True(w <= deals * 2 / 3, $"{style} : {string.Join(", ", blind)} sur {deals}"));
        Assert.True(oracle > blind.Max(), $"{style} : oracle {oracle}, sans savoir {string.Join(", ", blind)}");
        Assert.True(oracle < deals, $"{style} : oracle {oracle} sur {deals} (certaines donnes ne se gagnent pas)");
    }
}
