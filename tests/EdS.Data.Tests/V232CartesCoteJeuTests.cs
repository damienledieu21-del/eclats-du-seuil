using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.World;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.32, cartes côté jeu : le PNJ ne joue qu'à la taverne le soir (décision de Damien du 16 novembre 2026, vers 14 h : de 18 h à
/// minuit ; emplois du temps livrés), les mises proposées dans son dialogue, la table vue par le client (gardée d'une image à l'autre,
/// Échap avec confirmation), la bourse qui doit pouvoir recevoir le gain dès l'assise, et le titre actif (sauvegarde v26 : « As des Trois
/// Feux » choisi parmi les titres obtenus et affiché sous le nom).
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

    private static bool Plays(string npc, int hour, int minute = 0) =>
        V231TestData.Inside("zone.auberge", At(hour, minute), 0, 0, Shipped.Value).PlaysCardsNow(npc);

    private static SavedCards Ace(bool shown = false) => new SavedCards(0x1234UL, 10, CardMatch.VictoriesForReward, true, 0, null, 0, null, shown);

    [Fact]
    public void De_18_h_a_minuit_a_la_taverne_seulement_d_apres_les_emplois_du_temps()
    {
        Assert.Equal((18 * 60, 24 * 60), (CardMatch.EveningStart, CardMatch.EveningEnd));
        Assert.Equal("zone.auberge", CardMatch.Tavern);
        // Mathurin : 17 h 59, au comptoir mais pas encore le soir ; 18 h et 18 h 30, dans la salle ; 21 h 10, il fume sa pipe dehors ;
        // 21 h 40 et 23 h 59, au comptoir le soir ; minuit : le lendemain commence.
        Assert.False(Plays(Mathurin, 17, 59));
        Assert.True(Plays(Mathurin, 18));
        Assert.True(Plays(Mathurin, 18, 30));
        Assert.False(Plays(Mathurin, 21, 10));
        Assert.True(Plays(Mathurin, 21, 40));
        Assert.True(Plays(Mathurin, 23, 59));
        Assert.True(Plays("npc.boulanger", 18, 30));   // Firmin arrive à la taverne à 18 h 30
        // Les habitués du soir (taverne dans l'auberge) jouent ; l'armurier, à l'auberge seulement à midi, non ; Odile jamais.
        WorldSession evening = V231TestData.Inside("zone.auberge", At(20), 0, 0, Shipped.Value);
        foreach (string npc in new[] { "npc.maelle", "npc.epiciere", "npc.boulanger", "npc.renart", "npc.fermier" })
            Assert.True(evening.PlaysCardsNow(npc), npc);
        foreach (string npc in new[] { "npc.armurier", "npc.odile", "npc.gaspard", "npc.inconnu", "pas un pnj" })
            Assert.False(evening.PlaysCardsNow(npc), npc);
        Assert.False(Plays("npc.armurier", 12, 15));
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

        WorldSession evening = NextTo(Mathurin, At(18, 30), Hero(500));
        Assert.Equal(CardTableRefusal.None, evening.SitDownAtCards(Mathurin, 10, 7));
        Assert.Equal(490, evening.Money);
        Assert.NotNull(evening.CardGame);
    }

    [Fact]
    public void Bourse_qui_ne_peut_pas_recevoir_le_gain_refusee_des_l_assise()
    {
        // Relecture indépendante de la PR #193 : plutôt que de bloquer la dernière carte, on ne s'assoit pas.
        WorldSession w = NextTo(Mathurin, At(19, 30), Hero(Coins.Max - 10));
        string before = Snapshot(w);
        Assert.Equal(CardTableRefusal.PurseFull, w.SitDownAtCards(Mathurin, 25, 7));
        Assert.Equal(before, Snapshot(w));
        Assert.Null(w.Cards);
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
    public void Titre_actif_As_des_Trois_Feux_choisi_et_affiche_sous_le_nom()
    {
        AdventureSession plain = Adventure(At(10), Hero(100));
        Assert.DoesNotContain(plain.GuildCard().Titles, t => t.Id == AdventureSession.CardAceTitle);
        Assert.Empty(plain.CardAnswers);   // aucun dialogue ouvert
        Assert.Null(plain.CardTable);
        // Pas un As : refusé sans rien changer.
        Assert.Equal(GuildRefusal.UnknownTitle, plain.World.ShowTitle(CardMatch.AceTitle, out _));
        Assert.Null(plain.ShownTitle);

        AdventureSession a = Adventure(At(10), Hero(100).WithCards(Ace()));
        Assert.True(a.World.IsCardAce);
        TitleEntry title = Assert.Single(a.GuildCard().Titles, t => t.Id == AdventureSession.CardAceTitle);
        Assert.Equal("As des Trois Feux", title.Name);
        Assert.False(title.Shown);
        a.DrainMessages();
        // Non-membre de la guilde : le titre de la taverne s'affiche quand même sous le nom.
        Assert.Equal(GuildRefusal.None, a.ShowTitle(CardMatch.AceTitle));
        Assert.Equal("As des Trois Feux", a.ShownTitle);
        Assert.Equal(CardMatch.AceTitle, a.World.ActiveTitle);
        Assert.True(a.World.Cards!.Shown);
        Assert.True(Assert.Single(a.GuildCard().Titles).Shown);
        Assert.Equal("As des Trois Feux", a.GuildCard().Title);
        Assert.Contains(a.DrainMessages(), m => m.Contains("As des Trois Feux", StringComparison.Ordinal));
        Assert.Equal(GuildRefusal.None, a.ShowTitle(null));
        Assert.Null(a.ShownTitle);
        Assert.False(a.World.Cards!.Shown);
    }

    [Fact]
    public void Un_seul_titre_affiche_entre_la_guilde_et_la_taverne()
    {
        var card = new SavedGuildCard("g", titles: new[] { "title.guilde.recrue" }, title: "title.guilde.recrue");
        AdventureSession a = Adventure(At(10), Hero(100).WithStory(new SavedStory(guild: card)).WithCards(Ace()));
        Assert.Equal("title.guilde.recrue", a.World.ActiveTitle);
        Assert.Equal(GuildRefusal.None, a.ShowTitle(CardMatch.AceTitle));
        Assert.Equal(CardMatch.AceTitle, a.World.ActiveTitle);
        Assert.Null(a.World.Narrative!.ShownTitle);   // le titre de guilde s'efface
        Assert.Equal(GuildRefusal.None, a.ShowTitle("title.guilde.recrue"));
        Assert.Equal("title.guilde.recrue", a.World.ActiveTitle);
        Assert.False(a.World.Cards!.Shown);   // et celui de la taverne aussi
        // Un titre de guilde non gagné est refusé sans rien changer (le titre de la taverne reste).
        Assert.Equal(GuildRefusal.None, a.ShowTitle(CardMatch.AceTitle));
        Assert.NotEqual(GuildRefusal.None, a.World.ShowTitle("title.guilde.inconnu", out _));
        Assert.Equal(CardMatch.AceTitle, a.World.ActiveTitle);
    }

    [Fact]
    public void Sauvegarde_v26_titre_actif_et_migration_depuis_v25()
    {
        Assert.Equal((25, 26, 26), (SaveValidator.CardsSince, SaveValidator.CardTitleSince, SaveValidator.CurrentSchemaVersion));
        string v25 = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sauvegardes", "v25-avec-cartes.json"));
        Assert.Contains("\"schemaVersion\": 25", v25);
        Assert.DoesNotContain("\"shown\"", v25);
        SaveLoadResult read = SaveJsonReader.Read(v25);
        Assert.True(read.IsValid, string.Join("; ", read.Report.Errors.Select(e => e.Path + " " + e.Message)));
        SavedCards cards = read.Save!.Character!.Cards!;
        Assert.Equal((10L, true, false, 1), (cards.Wins, cards.DeckGiven, cards.Shown, cards.Game!.Moves.Count));
        // v25 → v26 : même texte, seule la version change (aucun titre des cartes affiché).
        string v26 = SaveJsonReader.Write(read.Save);
        Assert.Equal(v25.Replace("\"schemaVersion\": 25", SaveVersion.Current), v26);
        // Titre affiché : écrit, relu, aller-retour octet par octet.
        WorldSave s = read.Save;
        string shown = SaveJsonReader.Write(new WorldSave(s.Zone, s.WorldTick, s.Sessions, s.Consumed, s.Character!.WithCards(cards.WithShown(true))));
        Assert.Contains("\"gained\": 10, \"shown\": true, \"game\"", shown);
        SaveLoadResult again = SaveJsonReader.Read(shown);
        Assert.True(again.IsValid);
        Assert.True(again.Save!.Character!.Cards!.Shown);
        Assert.Equal(shown, SaveJsonReader.Write(again.Save));
        // Refus : « shown » en v25 (champ inconnu), avant dix victoires, ou avec un titre de guilde affiché.
        Assert.Contains(SaveJsonReader.Read(shown.Replace(SaveVersion.Current, "\"schemaVersion\": 25")).Report.Errors,
            e => e.Path.EndsWith(".cards.shown", StringComparison.Ordinal) && e.Code == ValidationCode.UnknownField);
        Assert.Contains(SaveJsonReader.Read(shown.Replace("\"wins\": 10, \"deck\": true", "\"wins\": 9, \"deck\": false")).Report.Errors,
            e => e.Path.EndsWith(".cards.shown", StringComparison.Ordinal) && e.Code == ValidationCode.InvalidValue);
        Assert.Throws<ArgumentException>(() => new SavedCards(1, 0, 9, false, 0, null, 0, null, shown: true));
        var card = new SavedGuildCard("g", titles: new[] { "title.guilde.recrue" }, title: "title.guilde.recrue");
        Assert.Contains(".cards.shown", Assert.Throws<ArgumentException>(() => SaveJsonReader.Write(new WorldSave(s.Zone, s.WorldTick, s.Sessions, s.Consumed,
            s.Character!.WithStory(new SavedStory(guild: card)).WithCards(cards.WithShown(true))))).Message, StringComparison.Ordinal);
        string guild = SaveJsonReader.Write(new WorldSave(s.Zone, s.WorldTick, s.Sessions, s.Consumed, s.Character!.WithStory(new SavedStory(guild: card))));
        Assert.True(SaveJsonReader.Read(guild).IsValid);
        string both = guild.Replace("\"gained\": 10, ", "\"gained\": 10, \"shown\": true, ");
        Assert.NotEqual(guild, both);
        Assert.Contains(SaveJsonReader.Read(both).Report.Errors, e => e.Path.EndsWith(".cards.shown", StringComparison.Ordinal) && e.Code == ValidationCode.InvalidValue);
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
                     "text.ui.cards.leave_confirm", "text.ui.cards.refused.games", "text.ui.cards.refused.cap", "text.ui.cards.refused.tavern",
                     "text.ui.cards.refused.combat", "text.ui.cards.refused.other" })
            Assert.NotEqual(key, a.Text(key));
        Assert.Equal("Soleil 7", a.CardLabel(new Card(CardSuit.Sun, 7)));
        Assert.Equal("Brume 1", a.CardLabel(new Card(CardSuit.Mist, 1)));
    }

    [Fact]
    public void Une_partie_complete_vue_par_le_monde()
    {
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

    [Fact]
    public void Table_du_client_gardee_et_echap_avec_confirmation()
    {
        // Une partie en cours dans la sauvegarde : l'aventure la rouvre (la table ne dépend pas de la place du joueur).
        var game = new SavedCardGame(Mathurin, SavedCards.StyleOf(Mathurin), 0, 0, 10, new[] { 1 });
        var cards = new SavedCards(0x77UL, 1, 0, false, 0, new Dictionary<string, int> { [Mathurin] = 1 }, 0, game);
        AdventureSession a = Adventure(At(19, 30), Hero(200).WithCards(cards));
        CardTableView table = a.CardTable!;
        Assert.Same(table, a.CardTable);   // gardée d'une image à l'autre
        Assert.Equal(CardMatch.HandSize - 1, table.Hand.Count);
        Assert.False(table.Finished);
        // Échap (ou le bouton) une fois : la table demande confirmation, rien n'est perdu.
        a.LeaveCardTable();
        Assert.NotNull(a.World.CardGame);
        Assert.Equal(a.Text("text.ui.cards.leave_confirm"), a.CardTable!.Prompt);
        // Poser une carte annule la demande et refait la table.
        Assert.Equal(CardTableRefusal.None, a.PlayCardAt(0));
        Assert.NotSame(table, a.CardTable);
        Assert.NotEqual(a.Text("text.ui.cards.leave_confirm"), a.CardTable!.Prompt);
        Assert.Equal(CardMatch.HandSize - 2, a.CardTable!.Hand.Count);
        // Deux fois : la partie et la mise sont perdues, la table se ferme.
        a.LeaveCardTable();
        a.LeaveCardTable();
        Assert.Null(a.World.CardGame);
        Assert.Null(a.CardTable);
        Assert.Equal(200, a.World.Money);   // la mise avait été prise en s'asseyant (avant la sauvegarde)
        Assert.Contains(a.DrainMessages(), m => m == a.Text("text.ui.cards.left"));
    }
}
