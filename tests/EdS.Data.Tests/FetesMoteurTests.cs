using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Feasts;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Zones;
using EdS.World;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Les 12 fêtes d'Orsédia, sous-lot F3 (moteur de fête ; décisions de Damien du 17 novembre 2026) : les neuf fêtes du calendrier seulement
/// mettent le monde en fête (invités, repli sur le calendrier du jeu sans date), une activité de fête générique auprès d'un invité rapporte la
/// monnaie de la fête, le marchand de fête (Maître Anseau) échange la monnaie contre décorations, costume, plat et récompense rattrapée, et
/// les concours de l'histoire de Semailles, Moissons et Veillée rapportent leur monnaie sans rien changer d'autre.
/// </summary>
[Trait("Category", "World")]
public class FetesMoteurTests
{
    private const string Carnaval = "festival.carnaval";
    private static readonly WorldClock Clock = new(50);
    private static readonly Lazy<StoryBook> Story = new(() => Book(Zone()));
    private static readonly Lazy<FeastBook> Feasts = new(() => FeastJsonReader.ReadFile(FeastJsonReader.PathIn(DataDirectory), Story.Value, Texts).Book!);

    private static long At(long day, int hour) => WorldEventSchedule.TickAt(Clock, day * 1440 + hour * 60);

    /// <summary>Session du calendrier du jeu (aucune date fournie), au jour <paramref name="day"/> à <paramref name="hour"/> h, au point (x, z).</summary>
    private static WorldSession Session(long day, int hour, double x, double z, SavedFeasts? feasts = null, bool catalog = true)
    {
        ZoneDefinition zone = Zone(x, z);
        var w = new WorldSession(new WorldSave("zone.vallons", At(day, hour), 0, new[] { PlayerCharacters.PlacesKey }, Hero().WithFeasts(feasts ?? SavedFeasts.Empty)),
            zone, ShippedData, Progression, ItemsCatalog, Book(zone), market: new Market(ShippedMerchants));
        w.Step();
        if (catalog) w.UseFeasts(Feasts.Value);
        return w;
    }

    // Le carnaval (calendrier du jeu : printemps, jour 13, de 10 h à 22 h) : Lison mène le défilé des masques près de l'étal du colporteur.
    private const long CarnavalDay = 13;
    private const double NearX = -1089.8, NearZ = -31.4;

    // Sous-lot F3 (décision de Damien du 18 novembre 2026) : l'étal de fête du forain, sur la place.
    private const double StallX = -1088.5, StallZ = -36.2;

    [Fact]
    public void Les_douze_fetes_mettent_le_monde_en_fete()
    {
        StoryBook story = Story.Value;
        Assert.Equal("0.40.0-provisoire", story.ContentVersion);   // V2.40 : passerelle des Ronces
        FeastBook b = Feasts.Value;
        Assert.All(b.Feasts, f =>
        {
            Festival fest = story.Festival(f.Festival);
            Assert.False(fest.CalendarOnly, f.Festival);
            Assert.True(fest.HasDate && fest.Season != null && fest.Day != null, f.Festival);
            Assert.NotEmpty(f.Activities);
            Assert.InRange(f.RewardAfter, 1, f.Activities.Count);
            Assert.All(f.Activities.Where(a => a.Host != null), a => Assert.NotNull(fest.Guest(a.Host!)));
            Assert.Contains(f.Festival, story.Npc(b.Merchant).Festivals);
            Assert.InRange(f.Stall.Count, 1, 5);
        });
        Assert.Equal(9 * 3 + 4 + 4 + 3, b.Feasts.Sum(f => f.Activities.Count));   // Semailles et Moissons en ont 4, la Veillée 3, les neuf autres 3
        // Une fête par jour du calendrier du jeu au plus, comme avant.
        Assert.Equal(14, story.Festivals.Count);
        Assert.Equal("npc.forain", b.Merchant);
        Assert.Equal(new[] { "festival.moissons", "festival.veillee", "festival.foire", "festival.marche", "festival.marche_mercredi" },
            story.Npc("npc.colporteur").Festivals);   // le colporteur garde ses fêtes d'avant   // les 12 fêtes et les deux marchés
        Assert.Equal(Carnaval, story.FestivalOn(CarnavalDay)!.Id);
    }

    [Fact]
    public void Activite_de_fete_aupres_d_un_invite()
    {
        WorldSession w = Session(CarnavalDay, 12, NearX, NearZ);
        Assert.Equal(Carnaval, w.FeastNow!.Festival);
        Assert.Equal(((long)0, CarnavalDay), w.FeastDay(Carnaval)!.Value);   // sans date : année du jeu, jour du monde
        Assert.Equal(FeastRefusal.None, w.JoinFeast("masques", out FeastGain g, out IReadOnlyList<StoryEvent> news));
        Assert.Equal(10, g.Gained);
        Assert.Equal(10, w.Character.Feasts.Of(Carnaval).Coins);
        Assert.Equal(new[] { $"FeastCoins {Carnaval}/masques 10" }, news.Select(e => e.ToString()));
        // Une fois par jour de fête ; refus sans rien changer.
        SavedFeasts before = w.Character.Feasts;
        Assert.Equal(FeastRefusal.AlreadyDone, w.JoinFeast("masques", out _));
        Assert.Equal(FeastRefusal.TooFar, w.JoinFeast("roles", out _));   // Odile est plus loin
        Assert.Equal(FeastRefusal.UnknownActivity, w.JoinFeast("tir", out _));   // activité d'une autre fête
        Assert.Same(before, w.Character.Feasts);
        // Hors de la fête, sans catalogue.
        Assert.Equal(FeastRefusal.NotNow, Session(CarnavalDay, 8, NearX, NearZ).JoinFeast("masques", out _));   // avant 10 h
        Assert.Equal(FeastRefusal.NotNow, Session(CarnavalDay + 2, 12, NearX, NearZ).JoinFeast("masques", out _));
        Assert.Equal(FeastRefusal.NoFeast, Session(CarnavalDay, 12, NearX, NearZ, catalog: false).JoinFeast("masques", out _));
    }

    [Fact]
    public void Marchand_de_fete_et_rattrapage()
    {
        FeastBook b = Feasts.Value;
        WorldSession poor = Session(CarnavalDay, 12, StallX, StallZ);
        Assert.Equal(FeastRefusal.None, poor.FeastShopOpen(out Feast? f));
        Assert.Equal(Carnaval, f!.Festival);
        // Un marchand dédié, pas le colporteur : Maître Florentin et son étal, dont le décor est celui de la fête du mois.
        var stall = poor.FeastStall!.Value;
        Assert.Equal(("npc.forain", Carnaval), (stall.Merchant, stall.Feast.Festival));
        Assert.Equal(b.Feast(Carnaval).Stall, stall.Decor);
        Assert.All(stall.Decor, d => Assert.Equal((FeastCategory.Decoration, 3), (d.Category, d.Feast)));
        Assert.True(poor.NpcAway(Story.Value.Npc("npc.colporteur")));   // le colporteur ne vient pas au carnaval
        Assert.Null(Session(CarnavalDay + 2, 12, StallX, StallZ).FeastStall);
        IReadOnlyList<WorldSession.FeastOffer> offers = poor.FeastShop();
        Assert.Equal(new[] { "F03_D01", "F03_D02", "F03_D03", "F03_D04", "F03_D05", "F03_C01", "F03_P01", "F03_R01" }, offers.Select(o => o.Ref.Code));
        Assert.All(offers.Take(7), o => Assert.Equal(FeastRefusal.NotEnoughCoins, o.Status));
        Assert.Equal(FeastRefusal.NothingToCatchUp, offers[7].Status);   // première année du jeu : pas d'édition d'avant
        Assert.Equal(FeastRefusal.NotEnoughCoins, poor.BuyFeast("F03_C01"));
        // Avec de la monnaie de la fête : le costume, puis plus rien du même ; la monnaie d'une autre fête ne sert à rien.
        var rich = new SavedFeasts(new[] { new SavedFeast(Carnaval, 130), new SavedFeast("festival.foire", 999) });
        WorldSession w = Session(CarnavalDay, 12, StallX, StallZ, rich);
        Assert.Equal(FeastRefusal.None, w.BuyFeast("F03_C01"));
        Assert.Equal(130 - b.Ref("F03_C01")!.Price!.Value, w.Character.Feasts.Of(Carnaval).Coins);
        Assert.Equal(999, w.Character.Feasts.Of("festival.foire").Coins);
        Assert.Equal(FeastRefusal.AlreadyOwned, w.BuyFeast("F03_C01"));
        Assert.Equal(FeastRefusal.UnknownRef, w.BuyFeast("F04_D01"));   // pas à cette fête
        Assert.Equal(FeastRefusal.NotForSale, w.BuyFeast("F03_M01"));
        // Rattrapage : la deuxième année du jeu, la récompense de la première, ratée, s'achète à prix plein.
        WorldSession next = Session(CarnavalDay + WorldCalendar.DaysPerYear, 12, StallX, StallZ, new SavedFeasts(new[] { new SavedFeast(Carnaval, 150) }));
        Assert.Equal(FeastRefusal.None, next.BuyFeast("F03_R01"));
        Assert.Equal((0L, 1L), (next.Character.Feasts.Of(Carnaval).Coins, next.Character.Feasts.Of(Carnaval).Bought));
        // Le marchand n'est là que pendant la fête ; trop loin, il ne vend pas.
        Assert.Equal(FeastRefusal.NotNow, Session(CarnavalDay + 2, 12, StallX, StallZ, rich).BuyFeast("F03_D01"));
        Assert.Equal(FeastRefusal.TooFar, Session(CarnavalDay, 12, StallX - 12, StallZ, rich).BuyFeast("F03_D01"));
    }

    [Fact]
    public void Concours_de_l_histoire_branche_sur_la_monnaie()
    {
        // Le concours principal des Semailles (calendrier du jeu : printemps, jour 14) rapporte 30 Graines de Griffon, une fois par édition, annoncées.
        WorldSession w = Session(14, 12, NearX, NearZ);
        Assert.Equal("festival.foire", w.Festival!.Id);
        INarrativeWorld n = w;
        var news = new List<StoryEvent>();
        n.ContestHeld("festival.foire", "festival.foire", news);
        Assert.Equal(30, w.Character.Feasts.Of("festival.foire").Coins);
        Assert.Equal(new[] { "FeastCoins festival.foire/concours 30" }, news.Select(e => e.ToString()));
        news.Clear();
        n.ContestHeld("festival.foire", "festival.foire", news);   // déjà fait dans l'édition : rien de plus, rien ne lève
        Assert.Equal(30, w.Character.Feasts.Of("festival.foire").Coins);
        Assert.Empty(news);
        n.ContestHeld("festival.foire", "festival.foire.patisserie", news);
        Assert.Equal(60, w.Character.Feasts.Of("festival.foire").Coins);
        n.ContestHeld("festival.marche", "festival.marche", news);   // le marché n'a pas de monnaie de fête
        Assert.Single(w.Character.Feasts.Entries);
        // Sans catalogue des fêtes, le concours reste ce qu'il était.
        WorldSession bare = Session(14, 12, NearX, NearZ, catalog: false);
        ((INarrativeWorld)bare).ContestHeld("festival.foire", "festival.foire", new List<StoryEvent>());
        Assert.True(bare.Character.Feasts.IsEmpty);
        // Relecture indépendante de la PR #216 : hors des heures de la fête (avant 8 h), rien.
        WorldSession early = Session(14, 6, NearX, NearZ);
        ((INarrativeWorld)early).ContestHeld("festival.foire", "festival.foire", news);
        Assert.True(early.Character.Feasts.IsEmpty);
    }

    [Fact]
    public void Concours_avec_un_portefeuille_presque_plein_annonce_le_plafond()
    {
        int cap = Feasts.Value.WalletCap;
        WorldSession w = Session(14, 12, NearX, NearZ, new SavedFeasts(new[] { new SavedFeast("festival.foire", cap - 5) }));
        var news = new List<StoryEvent>();
        ((INarrativeWorld)w).ContestHeld("festival.foire", "festival.foire", news);
        Assert.Equal(cap, w.Character.Feasts.Of("festival.foire").Coins);
        Assert.Equal(new[] { "FeastCoins festival.foire/concours 5", "FeastWalletFull festival.foire/concours 25" }, news.Select(e => e.ToString()));
        // Portefeuille plein : rien de gagné, la perte est annoncée quand même (l'activité compte).
        news.Clear();
        ((INarrativeWorld)w).ContestHeld("festival.foire", "festival.foire.patisserie", news);
        Assert.Equal(new[] { "FeastWalletFull festival.foire/patisserie 30" }, news.Select(e => e.ToString()));
        Assert.Equal(2, w.Character.Feasts.Of("festival.foire").Done.Count);
        // Compteur des éditions gagnées au plafond technique : refusé, annoncé, rien ne change.
        var full = new SavedFeasts(new[] { new SavedFeast("festival.foire", 0, 0, new Dictionary<string, long> { ["cible"] = 14, ["course"] = 14 }, SavedFeast.MaxCount) });
        WorldSession f = Session(14, 12, NearX, NearZ, full);
        news.Clear();
        ((INarrativeWorld)f).ContestHeld("festival.foire", "festival.foire", news);
        Assert.Equal(new[] { "FeastCountersFull festival.foire/concours" }, news.Select(e => e.ToString()));
        Assert.Equal(full, f.Character.Feasts);
    }

    [Fact]
    public void Fete_gagnee_titre_et_determinisme()
    {
        SavedFeasts Run()
        {
            // Deux activités différentes (rewardAfter 2) : le défilé des masques, puis l'échange des rôles auprès d'Odile.
            WorldSession w = Session(CarnavalDay, 12, NearX, NearZ);
            Assert.Equal(FeastRefusal.None, w.JoinFeast("masques", out _));
            Festival carnaval = Story.Value.Festival(Carnaval);
            var odile = carnaval.Guest("npc.odile")!.Position;
            WorldSession v = Session(CarnavalDay, 12, (double)odile.X.Raw / 10_000 + 0.8, (double)odile.Z.Raw / 10_000, w.Character.Feasts);
            Assert.Equal(FeastRefusal.None, v.JoinFeast("roles", out FeastGain g, out IReadOnlyList<StoryEvent> news));
            Assert.True(g.Won && g.Title);
            Assert.Contains(news, e => e.Kind == StoryEventKind.FeastWon && e.Id == Carnaval && e.Step == "title" && e.Value == 0);
            Assert.Equal(EdS.World.GuildRefusal.None, v.ShowTitle("title.fete.carnaval", out _));
            Assert.Equal("title.fete.carnaval", v.ActiveTitle);
            return v.Character.Feasts;
        }
        SavedFeasts a = Run();
        Assert.Equal(a, Run());
        Assert.Equal((1L, 20L, Carnaval), (a.Of(Carnaval).Wins, a.Of(Carnaval).Coins, a.Shown!));
    }
}
