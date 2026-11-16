using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.World;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.32, cartes côté jeu : le PNJ ne joue qu'à la taverne le soir (emplois du temps livrés), les mises proposées dans son
/// dialogue, la table vue par le client, le titre « As des Trois Feux » sur la carte d'aventurier.
/// </summary>
[Trait("Category", "World")]
public class V232CartesCoteJeuTests
{
    private const string Mathurin = "npc.aubergiste";
    private static readonly Lazy<StoryBook> Shipped = new(() => Book(Zone()));

    /// <summary>Dans l'auberge à ce tick, le joueur posé juste à côté de <paramref name="npc"/> (sa place à cette heure).</summary>
    private static WorldSession NextTo(string npc, long tick, SavedCharacter? hero = null)
    {
        WorldSession probe = V231TestData.Inside("zone.auberge", tick, 0, 0, Shipped.Value, hero);
        GroundVector at = probe.NpcPosition(Shipped.Value.Npc(npc));
        return V231TestData.Inside("zone.auberge", tick, at.X.Raw / 10000.0 + 0.5, at.Z.Raw / 10000.0, Shipped.Value, hero);
    }

    private static long At(int hour, int minute = 0) => CartesMisesTests.TickAt(0, hour * 60 + minute);

    [Fact]
    public void Le_soir_a_la_taverne_seulement_d_apres_les_emplois_du_temps()
    {
        // Le soir, c'est la tranche « Dusk » de l'horloge du monde : de 19 h à 22 h.
        Assert.Equal(DayPhase.Dusk, WorldTime.PhaseOf(19 * 60));
        Assert.Equal(DayPhase.Day, WorldTime.PhaseOf(19 * 60 - 1));
        Assert.Equal(DayPhase.Night, WorldTime.PhaseOf(22 * 60));
        Assert.Equal("zone.auberge", CardMatch.Tavern);
        // Mathurin : à 19 h 30, dans la salle (oui) ; à 18 h 30, dans la salle mais pas encore le soir ; à 21 h 10, il fume sa pipe dehors.
        Assert.True(V231TestData.Inside("zone.auberge", At(19, 30), 0, 0, Shipped.Value).PlaysCardsNow(Mathurin));
        Assert.False(V231TestData.Inside("zone.auberge", At(18, 30), 0, 0, Shipped.Value).PlaysCardsNow(Mathurin));
        Assert.False(V231TestData.Inside("zone.auberge", At(21, 10), 0, 0, Shipped.Value).PlaysCardsNow(Mathurin));
        Assert.True(V231TestData.Inside("zone.auberge", At(21, 40), 0, 0, Shipped.Value).PlaysCardsNow(Mathurin));
        // Les habitués du soir (taverne dans l'auberge) jouent ; l'armurier, à l'auberge seulement à midi, non ; Odile jamais.
        WorldSession evening = V231TestData.Inside("zone.auberge", At(20), 0, 0, Shipped.Value);
        foreach (string npc in new[] { "npc.maelle", "npc.epiciere", "npc.boulanger", "npc.renart", "npc.fermier" })
            Assert.True(evening.PlaysCardsNow(npc), npc);
        foreach (string npc in new[] { "npc.armurier", "npc.odile", "npc.gaspard", "npc.inconnu", "pas un pnj" })
            Assert.False(evening.PlaysCardsNow(npc), npc);
        Assert.False(V231TestData.Inside("zone.auberge", At(12, 15), 0, 0, Shipped.Value).PlaysCardsNow("npc.armurier"));
        Assert.False(evening.PlaysCardsNow(null));
    }

    [Fact]
    public void Hors_du_soir_l_assise_est_refusee_sans_rien_changer()
    {
        WorldSession morning = NextTo(Mathurin, At(10), Hero(500));
        Assert.True(((EdS.World.Narrative)morning.Narrative!).CanReach(Mathurin));   // à portée de parole, mais le matin
        string before = Snapshot(morning);
        Assert.Equal(CardTableRefusal.NotAtTavern, morning.SitDownAtCards(Mathurin, 10, 7));
        Assert.Equal(before, Snapshot(morning));
        Assert.Null(morning.Cards);

        WorldSession evening = NextTo(Mathurin, At(19, 30), Hero(500));
        Assert.Equal(CardTableRefusal.None, evening.SitDownAtCards(Mathurin, 10, 7));
        Assert.Equal(490, evening.Money);
        Assert.NotNull(evening.CardGame);
    }

    private static AdventureSession Adventure(long tick, SavedCharacter hero)
    {
        string save = Path.Combine(Path.GetTempPath(), "eds-v232-" + Guid.NewGuid().ToString("N") + ".json");
        SaveStore.Write(save, new WorldSave("zone.vallons", tick, 0, Array.Empty<string>(), hero));
        try
        {
            return V11TestData.Adventure(save);
        }
        finally
        {
            File.Delete(save);
        }
    }

    [Fact]
    public void As_des_Trois_Feux_parmi_les_titres_de_la_carte_d_aventurier()
    {
        AdventureSession plain = Adventure(At(10), Hero(100));
        Assert.DoesNotContain(plain.GuildCard().Titles, t => t.Id == AdventureSession.CardAceTitle);
        Assert.Empty(plain.CardAnswers);   // aucun dialogue ouvert
        Assert.Null(plain.CardTable);

        var ace = new SavedCards(0x1234UL, 10, CardMatch.VictoriesForReward, true, 0, null, 0, null);
        AdventureSession a = Adventure(At(10), Hero(100).WithCards(ace));
        Assert.True(a.World.IsCardAce);
        TitleEntry title = Assert.Single(a.GuildCard().Titles, t => t.Id == AdventureSession.CardAceTitle);
        Assert.Equal("As des Trois Feux", title.Name);
        Assert.False(title.Showable);   // un titre de la taverne : pas sous le nom (la guilde garde ses titres)
        Assert.False(title.Shown);
        Assert.False(a.RareDeckOwed);
    }

    [Fact]
    public void Textes_de_la_table_et_des_refus()
    {
        AdventureSession a = Adventure(At(10), Hero(100));
        foreach (string key in new[] { "text.ui.cards.answer", "text.ui.cards.claim", "text.ui.cards.title", "text.ui.cards.against", "text.ui.cards.score",
                     "text.ui.cards.round", "text.ui.cards.last", "text.ui.cards.round_won", "text.ui.cards.round_lost", "text.ui.cards.round_tie",
                     "text.ui.cards.card", "text.ui.cards.suit.sun", "text.ui.cards.suit.moon", "text.ui.cards.suit.mist", "text.ui.cards.rules",
                     "text.ui.cards.won", "text.ui.cards.draw", "text.ui.cards.lost", "text.ui.cards.coins", "text.ui.cards.ace", "text.ui.cards.deck_given",
                     "text.ui.cards.deck_owed", "text.ui.cards.ace_title", "text.ui.cards.leave", "text.ui.cards.stand", "text.ui.cards.left",
                     "text.ui.cards.refused.games", "text.ui.cards.refused.cap", "text.ui.cards.refused.tavern", "text.ui.cards.refused.combat",
                     "text.ui.cards.refused.other" })
            Assert.NotEqual(key, a.Text(key));
        Assert.Equal("Soleil 7", a.CardLabel(new Card(CardSuit.Sun, 7)));
        Assert.Equal("Brume 1", a.CardLabel(new Card(CardSuit.Mist, 1)));
    }

    [Fact]
    public void Une_partie_complete_vue_par_le_client()
    {
        // L'aventure s'ouvre dehors ; la table se joue à travers le monde de l'aventure : on y pose le joueur près de Mathurin.
        WorldSession w = NextTo(Mathurin, At(19, 30), Hero(500));
        Assert.Equal(CardTableRefusal.None, w.SitDownAtCards(Mathurin, 25, 99));
        CardMatch match = w.CardGame!;
        Assert.Equal(CardMatch.HandSize, match.PlayerHand.Count);
        CardGameEnd? end = null;
        for (int i = 0; i < CardMatch.HandSize; i++)
            Assert.Equal(CardTableRefusal.None, w.PlayCard(0, out _, out end));
        Assert.NotNull(end);
        Assert.True(end!.Match.IsOver);
        Assert.Equal(CardMatch.HandSize, end.Match.Rounds.Count);
        Assert.Equal(Math.Sign(end.Match.PlayerWins - end.Match.NpcWins), end.Outcome);
        Assert.Null(w.CardGame);
        Assert.Equal(500 - 25 + end.Payout, w.Money);
    }
}
