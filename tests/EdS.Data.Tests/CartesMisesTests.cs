using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Story;
using EdS.Data.Zones;
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
    private const string Mathurin = "npc.aubergiste", Other = "npc.boulanger";
    private const ulong Seed = 0x5EED_CAFE_0000_0001UL;

    /// <summary>L'histoire livrée, Mathurin posé près de la porte de l'auberge (comme les essais du V1.6), là où arrive le joueur.</summary>
    private static readonly Lazy<StoryBook> Shipped = new Lazy<StoryBook>(() =>
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(StoryJsonReader.StoryPathIn(DataDirectory)))!.AsObject();
        JsonObject n = root["data"]!["npcs"]!.AsArray().Select(x => x!.AsObject()).Single(x => (string?)x["id"] == Mathurin);
        Door door = V16TestData.Interiors.Single(z => z.Id == (string)n["zone"]!).Doors[0];
        V23TestData.Move(n, new JsonArray(Math.Round(door.Position.X.Raw / 10000.0, 4), Math.Round(door.Position.Z.Raw / 10000.0 + 1.5, 4)));
        StoryLoadResult r = StoryJsonReader.ReadStory(root.ToJsonString(), Texts, Zone(), ItemsCatalog, V16TestData.Interiors);
        Assert.True(r.IsValid, r.Report.ToString());
        return r.Story!;
    });

    /// <summary>À l'Auberge de l'Aube, à portée de Mathurin (relecture indépendante de la PR #189 : il faut être à portée de parole).</summary>
    private static WorldSession AtInn(SavedCharacter hero)
    {
        var session = new WorldSession(new WorldSave("zone.auberge", 0, 0, new[] { PlayerCharacters.PlacesKey }, hero),
            V16TestData.Interiors.Single(z => z.Id == "zone.auberge"), ShippedData, Progression, ItemsCatalog, Shipped.Value, door: "sortie", storyZone: "zone.vallons");
        session.Step();
        return session;
    }

    private static WorldSession Table(long money = 500, SavedCards? cards = null, params ItemStack[] items)
    {
        SavedCharacter hero = Hero(money, items);
        if (cards != null) hero = hero.WithCards(cards);
        return AtInn(hero);
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
    private static int OutcomeOf(ulong secret, string npc, long day, int game, long seat = 0)
    {
        var m = new CardMatch(CardMatch.Key(secret, "bastion", npc, day, game, seat), CardMatch.StyleOf(npc));
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
        Assert.Equal(CardTableRefusal.UnknownNpc, w.SitDownAtCards("npc.inconnu", 5, Seed));   // absent de l'histoire
        Assert.Equal(CardTableRefusal.TooFar, w.SitDownAtCards(Other, 5, Seed));   // Firmin n'est pas à l'auberge
        Assert.Equal(CardTableRefusal.TooFar, Play(Zone(), Hero(500)).SitDownAtCards(Mathurin, 5, Seed));   // dehors, loin de Mathurin
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
        Assert.Equal(CardTableRefusal.GameInProgress, w.SitDownAtCards(Mathurin, 5, Seed + 1));
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
        Assert.Contains($"\"cards\": {{\"secret\": \"{SavedCards.SecretText(Seed)}\", \"seats\": 1, \"wins\": 0, \"deck\": false, \"day\": {saved.Day}, \"played\": {{\"{Mathurin}\": 1}}, \"gained\": 0, \"game\": {{\"npc\": \"{Mathurin}\", \"style\": 2, \"day\": {saved.Day}, \"game\": 0, \"stake\": 25, \"moves\": [2, 0]}}}}", text);
        Assert.True(MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "save.schema.json")).IsValid(System.Text.Json.JsonDocument.Parse(text).RootElement));
        SaveLoadResult read = SaveJsonReader.Read(text);
        Assert.True(read.IsValid, string.Join("; ", read.Report.Errors.Select(e => e.Path + " " + e.Message)));
        Assert.Equal(saved.Character.Cards, read.Save!.Character!.Cards);
        Assert.Equal(text, SaveJsonReader.Write(read.Save));
        WorldSession resumed = AtInn(read.Save.Character!);
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
        Assert.Equal((0, 3), (w.CardGamesLeft(Mathurin), w.CardGamesLeft(Other)));   // un autre PNJ : ses trois parties à lui
        // Le lendemain, les compteurs repartent de zéro.
        var tomorrow = new SavedCards(Seed, 0, 0, false, w.Day, new Dictionary<string, int> { [Mathurin] = 3 }, 150, null);
        Assert.Equal(0, tomorrow.PlayedOn(w.Day + 1, Mathurin));
        Assert.Equal(0, tomorrow.GainedOn(w.Day + 1));
        SavedCards next = tomorrow.WithStart(new SavedCardGame(Mathurin, 2, w.Day + 1, 0, 5, Array.Empty<int>()));
        Assert.Equal((1, 1, 0), (next.Played[Mathurin], next.Played.Count, next.Gained));
    }

    [Fact]
    public void Plafond_de_150_cuivre_de_gains_par_jour()
    {
        long day = Table().Day;
        var cards = new SavedCards(SecretFor(1, day), 0, 0, false, day, new Dictionary<string, int> { [Other] = 3 }, 140, null);
        WorldSession w = Table(100, cards);
        Assert.Equal(10, w.CardGainsLeft);
        SavedCharacter before = w.Character;
        Assert.Equal(CardTableRefusal.DailyCapReached, w.SitDownAtCards(Mathurin, 25, 0));
        Assert.Same(before, w.Character);
        Assert.Equal(CardTableRefusal.None, w.SitDownAtCards(Mathurin, 10, 0));
        Assert.Equal(1, PlayOut(w).Outcome);
        Assert.Equal((150, 0, 110L), (w.Cards!.Gained, w.CardGainsLeft, w.Money));
        Assert.Equal(CardTableRefusal.DailyCapReached, w.SitDownAtCards(Mathurin, 5, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SavedCards(Seed, 0, 0, false, day, null, 151, null));
    }

    [Fact]
    public void Dixieme_victoire_titre_et_jeu_rare_ou_du_si_le_sac_est_plein()
    {
        long day = Table().Day;
        ulong secret = SecretFor(1, day);
        var nine = new SavedCards(secret, 0, 9, false, 0, null, 0, null);
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
        Assert.Throws<ArgumentException>(() => new SavedCards(Seed, 0, 9, true, 0, null, 0, null));
    }

    [Fact]
    public void Au_plus_32_adversaires_par_jour_refus_type_sans_rien_changer()
    {
        long day = Table().Day;
        var many = Enumerable.Range(0, SavedCards.MaxOpponents).ToDictionary(i => $"npc.joueur_{i}", _ => 1);
        WorldSession w = Table(100, new SavedCards(Seed, 0, 0, false, day, many, 0, null));
        SavedCharacter before = w.Character;
        Assert.Equal(CardTableRefusal.TooManyOpponents, w.SitDownAtCards(Mathurin, 5, 0));
        Assert.Same(before, w.Character);
        // Un adversaire déjà rencontré aujourd'hui reste possible ; le lendemain, tout repart.
        many[Mathurin] = 1;
        many.Remove("npc.joueur_0");
        Assert.Equal(CardTableRefusal.None, Table(100, new SavedCards(Seed, 0, 0, false, day, many, 0, null)).SitDownAtCards(Mathurin, 5, 0));
        Assert.False(new SavedCards(Seed, 0, 0, false, day, many, 0, null).TooManyOpponents(day + 1, "npc.nouveau"));
    }

    [Fact]
    public void La_maniere_sauvegardee_est_celle_du_PNJ()
    {
        Assert.Equal(2, SavedCards.StyleOf(Mathurin));
        Assert.Equal((int)CardMatch.StyleOf(Other), SavedCards.StyleOf(Other));
        Assert.Throws<ArgumentException>(() => new SavedCardGame(Mathurin, 0, 0, 0, 5, Array.Empty<int>()));
        WorldSession w = Table();
        w.SitDownAtCards(Mathurin, 5, Seed);
        string text = SaveJsonReader.Write(w.Save());
        SaveLoadResult bad = SaveJsonReader.Read(text.Replace("\"style\": 2", "\"style\": 1"));
        Assert.False(bad.IsValid);
        Assert.Contains(bad.Report.Errors, e => e.Path.EndsWith(".cards.game.style") && e.Code == ValidationCode.InvalidValue);
    }

    [Fact]
    public void Victoires_saturees_au_plafond()
    {
        var top = new SavedCards(Seed, 1, SavedCards.MaxWins, true, 0, new Dictionary<string, int> { [Mathurin] = 1 }, 0,
            new SavedCardGame(Mathurin, 2, 0, 0, 5, Array.Empty<int>()));
        Assert.Equal(SavedCards.MaxWins, top.WithEnd(true, 5).Wins);
    }

    /// <summary>
    /// Une vraie sauvegarde v24 (figée, sans cartes) se lit, monte en v25 à la réécriture sans champ « cards » ; la réécriture ne diffère
    /// que par la version (relecture indépendante de la PR #189).
    /// </summary>
    [Fact]
    public void Sauvegarde_v24_sans_cartes_relue_et_reecrite_en_v25_sans_cartes()
    {
        string v24 = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sauvegardes", "v24-sans-cartes.json"));
        Assert.Contains("\"schemaVersion\": 24", v24);
        Assert.DoesNotContain("\"cards\"", v24);
        SaveLoadResult read = SaveJsonReader.Read(v24);
        Assert.True(read.IsValid, string.Join("; ", read.Report.Errors.Select(e => e.Path + " " + e.Message)));
        Assert.Null(read.Save!.Character!.Cards);
        string v25 = SaveJsonReader.Write(read.Save);
        Assert.Equal(v24.Replace("\"schemaVersion\": 24", SaveVersion.Current), v25);
        Assert.Equal(v25, SaveJsonReader.Write(SaveJsonReader.Read(v25).Save!));   // aller-retour v25 octet par octet
        Assert.Null(AtInn(read.Save.Character!).Cards);
    }

    /// <summary>Décision de Damien du 16 novembre 2026 : un compteur d'assises persistant change la donne à chaque fois qu'on s'assoit.</summary>
    [Fact]
    public void La_donne_change_a_chaque_assise_et_se_reproduit_a_l_identique()
    {
        long day = Table().Day;
        IReadOnlyList<Card> HandAt(long seats)
        {
            WorldSession w = Table(100, new SavedCards(Seed, seats, 0, false, day, null, 0, null));
            Assert.Equal(CardTableRefusal.None, w.SitDownAtCards(Mathurin, 5, 0));
            Assert.Equal(seats + 1, w.Cards!.Seats);
            return w.CardGame!.PlayerHand;
        }
        // Même jour, même PNJ, même numéro de partie du jour : seule l'assise change, et la donne avec.
        Assert.NotEqual(HandAt(7), HandAt(8));
        Assert.Equal(HandAt(7), HandAt(7));
        // Rejouer « la même » partie (abandon puis nouvelle assise le même jour) donne une autre main ; aller-retour v25 du compteur.
        WorldSession t = Table();
        t.SitDownAtCards(Mathurin, 5, Seed);
        IReadOnlyList<Card> first = t.CardGame!.PlayerHand;
        t.ForfeitCards();
        string text = SaveJsonReader.Write(t.Save());
        Assert.Contains("\"seats\": 1, ", text);
        WorldSession again = AtInn(SaveJsonReader.Read(text).Save!.Character!);
        Assert.Equal(1, again.Cards!.Seats);
        again.SitDownAtCards(Mathurin, 5, 0);
        Assert.Equal(2, again.Cards.Seats);
        Assert.NotEqual(first, again.CardGame!.PlayerHand);
        // Bornes : « seats » obligatoire, au format ; une partie en cours demande une assise comptée ; plafond : refus typé, rien ne change.
        Assert.False(SaveJsonReader.Read(text.Replace("\"seats\": 1, ", "")).IsValid);
        Assert.Contains(SaveJsonReader.Read(text.Replace("\"seats\": 1, ", "\"seats\": -1, ")).Report.Errors, e => e.Path.EndsWith(".cards.seats"));
        Assert.Throws<ArgumentException>(() => new SavedCards(Seed, 0, 0, false, day, new Dictionary<string, int> { [Mathurin] = 1 }, 0,
            new SavedCardGame(Mathurin, 2, day, 0, 5, Array.Empty<int>())));
        WorldSession full = Table(100, new SavedCards(Seed, SavedCards.MaxSeats, 0, false, day, null, 0, null));
        SavedCharacter before = full.Character;
        Assert.Equal(CardTableRefusal.SeatsExhausted, full.SitDownAtCards(Mathurin, 5, 0));
        Assert.Same(before, full.Character);
    }

    /// <summary>Revue Codex de la PR #189 : la mise en cours est réservée sous le plafond ; une sauvegarde qui la dépasse est refusée.</summary>
    [Fact]
    public void Sauvegarde_gains_plus_mise_en_cours_sous_le_plafond()
    {
        long day = Table().Day;
        var played = new Dictionary<string, int> { [Mathurin] = 1 };
        Assert.Throws<ArgumentException>(() => new SavedCards(Seed, 1, 0, false, day, played, 130, new SavedCardGame(Mathurin, 2, day, 0, 25, Array.Empty<int>())));
        WorldSession w = Table(100, new SavedCards(Seed, 0, 0, false, day, new Dictionary<string, int> { ["npc.boulanger"] = 1 }, 125, null));
        Assert.Equal(CardTableRefusal.None, w.SitDownAtCards(Mathurin, 25, 0));
        string text = SaveJsonReader.Write(w.Save());
        Assert.True(SaveJsonReader.Read(text).IsValid);
        SaveLoadResult bad = SaveJsonReader.Read(text.Replace("\"gained\": 125", "\"gained\": 130"));
        Assert.False(bad.IsValid);
        Assert.Contains(bad.Report.Errors, e => e.Path.EndsWith(".cards.game.stake") && e.Code == ValidationCode.OutOfRange);
    }

    /// <summary>Revue Codex de la PR #189 : une partie commencée hors combat ne se joue pas pendant un combat ; rien ne change.</summary>
    [Fact]
    public void Pas_de_carte_jouee_en_combat()
    {
        WorldSession w = Table();
        Assert.Equal(CardTableRefusal.None, w.SitDownAtCards(Mathurin, 5, Seed));
        w.Zone.Leave(w.CombatantId);   // comme la pêche : hors d'état de jouer, la partie le compte en combat
        w.Step();
        Assert.True(w.InCombat);
        SavedCharacter before = w.Character;
        Assert.Equal(CardTableRefusal.InCombat, w.PlayCard(0, out CardRound? round, out CardGameEnd? end));
        Assert.Null(round);
        Assert.Null(end);
        Assert.Same(before, w.Character);
        Assert.NotNull(w.CardGame);   // la partie reste, à reprendre hors combat
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
