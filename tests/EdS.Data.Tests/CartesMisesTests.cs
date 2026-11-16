using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Validation;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Cartes à la taverne, les mises (décisions de Damien du 16 novembre 2026) : mises de 5, 10 ou 25 cuivre, 3 parties par PNJ et par jour,
/// jeu rare et titre « As des Trois Feux » après 10 victoires, partie en cours enregistrée dans la sauvegarde (v25), partie nulle : mise
/// rendue, plafond de gains de la taverne : 150 cuivre par jour. Refus atomiques, déterminisme.
/// </summary>
[Trait("Category", "World")]
public class CartesMisesTests
{
    private const string Mathurin = "npc.aubergiste", Other = "npc.forgeron";
    private const ulong Seed = 0x5EED_CAFE_0000_0001UL;

    private static WorldSession Table(long money = 500, SavedCards? cards = null, params ItemStack[] items)
    {
        SavedCharacter hero = Hero(money, items);
        if (cards != null) hero = hero.WithCards(cards);
        return Play(Zone(), hero);
    }

    /// <summary>Joue toujours la première carte de la main ; rend la fin de partie.</summary>
    private static CardGameEnd PlayOut(WorldSession w)
    {
        CardGameEnd? end = null;
        for (int i = 0; i < CardMatch.HandSize; i++)
            Assert.Equal(CardTableRefusal.None, w.PlayCard(0, out _, out end));
        return end!;
    }

    /// <summary>Issue d'une partie jouée « toujours la première carte », calculée à part (sans la session).</summary>
    private static int OutcomeOf(ulong secret, string npc, long day, int game)
    {
        var m = new CardMatch(CardMatch.Key(secret, "bastion", npc, day, game), CardMatch.StyleOf(npc));
        while (!m.IsOver) m.Play(0, out _);
        return m.Outcome;
    }

    /// <summary>Une graine dont la première partie du jour contre Mathurin finit par <paramref name="outcome"/>.</summary>
    private static ulong SecretFor(int outcome, long day)
    {
        for (ulong s = 1; s < 5000; s++)
            if (OutcomeOf(s, Mathurin, day, 0) == outcome) return s;
        throw new InvalidOperationException("Aucune graine trouvée.");
    }

    [Fact]
    public void Decisions_de_Damien_du_16_novembre_2026()
    {
        Assert.Equal(new[] { 5, 10, 25 }, CardMatch.Stakes);
        Assert.Equal(CardMatch.Stakes, SavedCards.Stakes);
        Assert.Equal((3, 3), (CardMatch.MaxGamesPerDay, SavedCards.MaxGamesPerDay));
        Assert.Equal((150, 150), (CardMatch.DailyCap, SavedCards.DailyCap));
        Assert.Equal((10, 10), (CardMatch.VictoriesForReward, SavedCards.VictoriesForReward));
        Assert.Equal((CardMatch.HandSize, 25), (SavedCards.HandSize, SaveValidator.CardsSince));
        Assert.Equal(CardStyle.Cunning, CardMatch.StyleOf(Mathurin));
        Assert.Equal(CardMatch.StyleOf(Other), CardMatch.StyleOf(Other));
        Assert.True(ItemsCatalog.TryGetItem(CardMatch.RareDeck, out _));
    }

    [Fact]
    public void La_mise_est_prise_en_s_asseyant_et_les_refus_ne_changent_rien()
    {
        WorldSession w = Table(30);
        SavedCharacter before = w.Character;
        Assert.Equal(CardTableRefusal.NoSuchStake, w.SitDownAtCards(Mathurin, 7, Seed));
        Assert.Equal(CardTableRefusal.UnknownNpc, w.SitDownAtCards("aubergiste", 5, Seed));
        Assert.Equal(CardTableRefusal.NoGame, w.PlayCard(0, out _, out _));
        Assert.Equal(CardTableRefusal.NoGame, w.ForfeitCards());
        Assert.Same(before, w.Character);
        Assert.Null(w.Cards);
        Assert.Equal(CardTableRefusal.NotEnoughMoney, Table(20).SitDownAtCards(Mathurin, 25, Seed));
        Assert.Equal(CardTableRefusal.None, w.SitDownAtCards(Mathurin, 25, Seed));
        Assert.Equal(5L, w.Money);
        Assert.Equal(Seed, w.Cards!.Secret);
        Assert.NotNull(w.CardGame);
        before = w.Character;
        Assert.Equal(CardTableRefusal.GameInProgress, w.SitDownAtCards(Other, 5, Seed + 1));
        Assert.Equal(CardTableRefusal.NoSuchCard, w.PlayCard(5, out _, out _));
        Assert.Equal(CardTableRefusal.NoSuchCard, w.PlayCard(-1, out _, out _));
        Assert.Same(before, w.Character);
    }

    [Fact]
    public void Victoire_mise_doublee_nulle_mise_rendue_defaite_mise_perdue()
    {
        long day = Table().Day;
        foreach ((int outcome, long purse) in new[] { (1, 120L), (0, 110L), (-1, 100L) })
        {
            WorldSession w = Table(110, SavedCards.Fresh(SecretFor(outcome, day)));
            Assert.Equal(CardTableRefusal.None, w.SitDownAtCards(Mathurin, 10, 0));
            Assert.Equal(100L, w.Money);
            CardGameEnd end = PlayOut(w);
            Assert.Equal((outcome, purse), (end.Outcome, w.Money));
            Assert.Equal((outcome > 0 ? 1L : 0L, outcome > 0 ? 10 : 0, (SavedCardGame?)null), (w.Cards!.Wins, w.Cards.Gained, w.Cards.Game));
        }
    }

    [Fact]
    public void Meme_graine_memes_choix_meme_partie()
    {
        WorldSession a = Table(), b = Table();
        a.SitDownAtCards(Mathurin, 5, Seed);
        b.SitDownAtCards(Mathurin, 5, Seed);
        Assert.Equal(a.CardGame!.PlayerHand, b.CardGame!.PlayerHand);
        for (int i = 0; i < CardMatch.HandSize; i++)
        {
            a.PlayCard(CardMatch.HandSize - 1 - i, out CardRound? ra, out _);
            b.PlayCard(CardMatch.HandSize - 1 - i, out CardRound? rb, out _);
            Assert.Equal((ra!.Player, ra.Npc, ra.Winner), (rb!.Player, rb.Npc, rb.Winner));
        }
        Assert.Equal(a.Character.Cards, b.Character.Cards);
        // Une autre graine (un autre personnage) : une autre donne.
        WorldSession c = Table();
        c.SitDownAtCards(Mathurin, 5, Seed + 1);
        Assert.NotEqual(Table().Also(x => x.SitDownAtCards(Mathurin, 5, Seed)).CardGame!.PlayerHand, c.CardGame!.PlayerHand);
    }

    [Fact]
    public void Partie_en_cours_enregistree_dans_la_sauvegarde_v25_et_reprise_a_l_identique()
    {
        WorldSession straight = Table(), saved = Table();
        foreach (WorldSession w in new[] { straight, saved }) w.SitDownAtCards(Mathurin, 25, Seed);
        straight.PlayCard(2, out _, out _);
        straight.PlayCard(0, out _, out _);
        saved.PlayCard(2, out _, out _);
        saved.PlayCard(0, out _, out _);
        string text = SaveJsonReader.Write(saved.Save());
        Assert.Contains($"\"cards\": {{\"secret\": \"{SavedCards.SecretText(Seed)}\", \"wins\": 0, \"deck\": false, \"day\": {saved.Day}, \"played\": {{\"{Mathurin}\": 1}}, \"gained\": 0, \"game\": {{\"npc\": \"{Mathurin}\", \"style\": 2, \"day\": {saved.Day}, \"game\": 0, \"stake\": 25, \"moves\": [2, 0]}}}}", text);
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "save.schema.json")).IsValid(System.Text.Json.JsonDocument.Parse(text).RootElement));
        SaveLoadResult read = SaveJsonReader.Read(text);
        Assert.True(read.IsValid, string.Join("; ", read.Report.Errors.Select(e => e.Path + " " + e.Message)));
        Assert.Equal(saved.Character.Cards, read.Save!.Character!.Cards);
        Assert.Equal(text, SaveJsonReader.Write(read.Save));
        WorldSession resumed = Play(Zone(), read.Save.Character!);
        Assert.Equal(straight.CardGame!.PlayerHand, resumed.CardGame!.PlayerHand);
        Assert.Equal(straight.CardGame.Rounds.Select(r => (r.Player, r.Npc)), resumed.CardGame.Rounds.Select(r => (r.Player, r.Npc)));
        for (int i = 0; i < 3; i++)
        {
            straight.PlayCard(0, out _, out CardGameEnd? e1);
            resumed.PlayCard(0, out _, out CardGameEnd? e2);
            Assert.Equal(e1?.Outcome, e2?.Outcome);
        }
        Assert.Equal((straight.Money, straight.Character.Cards), (resumed.Money, resumed.Character.Cards));
        // Sans table, pas de champ ; une sauvegarde v24 qui en porte un est refusée (champ inconnu).
        Assert.DoesNotContain("\"cards\"", SaveJsonReader.Write(Table().Save()));
        SaveLoadResult v24 = SaveJsonReader.Read(text.Replace(SaveVersion.Current, "\"schemaVersion\": 24"));
        Assert.False(v24.IsValid);
        Assert.Contains(v24.Report.Errors, e => e.Path.EndsWith(".cards") && e.Code == ValidationCode.UnknownField);
    }

    [Theory]
    [InlineData("\"secret\": \"5eedcafe00000001\"", "\"secret\": \"5EEDCAFE00000001\"", ".cards.secret")]
    [InlineData("\"stake\": 25", "\"stake\": 20", ".cards.game.stake")]
    [InlineData("\"moves\": [2, 0]", "\"moves\": [2, 4]", ".cards.game.moves")]
    [InlineData("\"moves\": [2, 0]", "\"moves\": [0, 0, 0, 0, 0]", ".cards.game.moves")]
    [InlineData("\"gained\": 0", "\"gained\": 155", ".cards.gained")]
    [InlineData("\"deck\": false", "\"deck\": true", ".cards.deck")]
    [InlineData("\"style\": 2", "\"style\": 3", ".cards.game.style")]
    [InlineData("\"game\": 0", "\"game\": 1", ".cards.game")]
    public void Validateur_chemins_precis(string from, string to, string path)
    {
        WorldSession w = Table();
        w.SitDownAtCards(Mathurin, 25, Seed);
        w.PlayCard(2, out _, out _);
        w.PlayCard(0, out _, out _);
        string text = SaveJsonReader.Write(w.Save());
        Assert.Contains(from, text);
        SaveLoadResult bad = SaveJsonReader.Read(text.Replace(from, to));
        Assert.False(bad.IsValid);
        Assert.Contains(bad.Report.Errors, e => e.Path.EndsWith(path));
    }

    [Fact]
    public void Trois_parties_par_PNJ_et_par_jour()
    {
        WorldSession w = Table(1000);
        for (int g = 0; g < 3; g++)
        {
            Assert.Equal(3 - g, w.CardGamesLeft(Mathurin));
            Assert.Equal(CardTableRefusal.None, w.SitDownAtCards(Mathurin, 5, Seed));
            Assert.Equal(g, w.Cards!.Game!.Game);
            Assert.Equal(CardTableRefusal.None, w.ForfeitCards());   // quitter en pleine partie : mise perdue, partie comptée
        }
        SavedCharacter before = w.Character;
        Assert.Equal(CardTableRefusal.NoMoreGamesToday, w.SitDownAtCards(Mathurin, 5, Seed));
        Assert.Same(before, w.Character);
        Assert.Equal(985L, w.Money);
        Assert.Equal(CardTableRefusal.None, w.SitDownAtCards(Other, 5, Seed));   // un autre PNJ : ses trois parties à lui
        // Le lendemain, les compteurs repartent de zéro.
        var tomorrow = new SavedCards(Seed, 0, false, w.Day, new Dictionary<string, int> { [Mathurin] = 3 }, 150, null);
        Assert.Equal(0, tomorrow.PlayedOn(w.Day + 1, Mathurin));
        Assert.Equal(0, tomorrow.GainedOn(w.Day + 1));
        SavedCards next = tomorrow.WithStart(new SavedCardGame(Mathurin, 2, w.Day + 1, 0, 5, Array.Empty<int>()));
        Assert.Equal((1, 1, 0), (next.Played[Mathurin], next.Played.Count, next.Gained));
    }

    [Fact]
    public void Plafond_de_150_cuivre_de_gains_par_jour()
    {
        long day = Table().Day;
        var cards = new SavedCards(SecretFor(1, day), 0, false, day, new Dictionary<string, int> { [Other] = 3 }, 140, null);
        WorldSession w = Table(100, cards);
        Assert.Equal(10, w.CardGainsLeft);
        SavedCharacter before = w.Character;
        Assert.Equal(CardTableRefusal.DailyCapReached, w.SitDownAtCards(Mathurin, 25, 0));
        Assert.Same(before, w.Character);
        Assert.Equal(CardTableRefusal.None, w.SitDownAtCards(Mathurin, 10, 0));
        Assert.Equal(1, PlayOut(w).Outcome);
        Assert.Equal((150, 0, 110L), (w.Cards!.Gained, w.CardGainsLeft, w.Money));
        Assert.Equal(CardTableRefusal.DailyCapReached, w.SitDownAtCards(Mathurin, 5, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SavedCards(Seed, 0, false, day, null, 151, null));
    }

    [Fact]
    public void Dixieme_victoire_titre_et_jeu_rare_ou_du_si_le_sac_est_plein()
    {
        long day = Table().Day;
        ulong secret = SecretFor(1, day);
        var nine = new SavedCards(secret, 9, false, 0, null, 0, null);
        WorldSession w = Table(100, nine);
        Assert.False(w.IsCardAce);
        w.SitDownAtCards(Mathurin, 5, 0);
        CardGameEnd end = PlayOut(w);
        Assert.True(end.BecameAce && end.DeckGiven && w.IsCardAce && w.Cards!.DeckGiven);
        Assert.Equal(1, w.Inventory.Count(CardMatch.RareDeck));
        Assert.Equal(CardTableRefusal.NothingOwed, w.ClaimRareDeck());

        // Sac plein : le titre est gagné, le jeu rare reste dû, et se réclame plus tard.
        ItemStack[] full = Enumerable.Range(0, 24).Select(_ => new ItemStack("item.gambison", 1)).ToArray();
        WorldSession f = Table(100, nine, full);
        f.SitDownAtCards(Mathurin, 5, 0);
        end = PlayOut(f);
        Assert.True(end.BecameAce && !end.DeckGiven && f.IsCardAce && !f.Cards!.DeckGiven);
        SavedCharacter before = f.Character;
        Assert.Equal(CardTableRefusal.NothingOwed, f.ClaimRareDeck());
        Assert.Same(before, f.Character);
        f.Inventory.Remove("item.gambison", 1);
        Assert.Equal(CardTableRefusal.None, f.ClaimRareDeck());
        Assert.True(f.Cards!.DeckGiven);
        Assert.Throws<ArgumentException>(() => new SavedCards(Seed, 9, true, 0, null, 0, null));
    }
}

internal static class CartesTestExtensions
{
    public static T Also<T>(this T value, Action<T> action)
    {
        action(value);
        return value;
    }
}
