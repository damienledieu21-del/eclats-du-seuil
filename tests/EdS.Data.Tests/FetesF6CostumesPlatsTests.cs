using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EdS.Data.Feasts;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Runtime;
using EdS.World;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot F6 : tenues des habitants aux teintes de la fête, plats de fête qui se mangent (réconfort sans statistique de combat, sauvegarde
/// inchangée), ambiance de chaque fête, images d'ambiance comme écrans de chargement pendant la fête.
/// </summary>
[Trait("Category", "World")]
public sealed class FetesF6CostumesPlatsTests
{
    private const string Carnaval = "festival.carnaval";
    private static readonly Lazy<FeastBook> Feasts = new(() => FeastJsonReader.ReadFile(FeastJsonReader.PathIn(DataDirectory), Book(Zone()), Texts).Book!);
    private static string Root => Lot93UnityPackageTests.Root;
    private static string Unity(string relative) => Path.Combine(Root, "unity", "Assets", "EdS", relative);

    private static AdventureSession In(string zone, long day, SavedFeasts feasts)
    {
        string save = Path.Combine(Path.GetTempPath(), "eds-f6-" + Guid.NewGuid().ToString("N") + ".json");
        SaveStore.Write(save, new WorldSave(zone, WorldEventSchedule.TickAt(new WorldClock(50), day * 1440 + 12 * 60), 0,
            new[] { PlayerCharacters.PlacesKey }, Hero().WithFeasts(feasts)));
        try
        {
            return Adventure(save);
        }
        finally
        {
            File.Delete(save);
        }
    }

    [Fact]
    public void Manger_un_plat_retire_un_du_compteur_sans_rien_d_autre()
    {
        FeastBook book = Feasts.Value;
        var two = new SavedFeasts(new[] { new SavedFeast(Carnaval, 7, dishes: 2) });
        Assert.Equal(FeastRefusal.None, FeastRules.Eat(two, book, Carnaval, out SavedFeasts one));
        Assert.Equal(1, one.Of(Carnaval).Dishes);
        Assert.Equal(7, one.Of(Carnaval).Coins);
        Assert.Equal(FeastRefusal.None, FeastRules.Eat(one, book, Carnaval, out SavedFeasts none));
        Assert.Equal(0, none.Of(Carnaval).Dishes);
        Assert.Equal(FeastRefusal.NoDish, FeastRules.Eat(none, book, Carnaval, out SavedFeasts same));
        Assert.Same(none, same);   // refus : rien ne change
        Assert.Equal(FeastRefusal.NoFeast, FeastRules.Eat(two, book, "festival.marche", out SavedFeasts unchanged));
        Assert.Same(two, unchanged);
        // Le dernier plat mangé d'une fête sans rien d'autre : l'entrée disparaît (comme une sauvegarde sans fête).
        Assert.True(new SavedFeasts(new[] { new SavedFeast(Carnaval, dishes: 1) }) is var last
            && FeastRules.Eat(last, book, Carnaval, out SavedFeasts emptied) == FeastRefusal.None && emptied.IsEmpty);
    }

    [Fact]
    public void Plat_mange_dans_le_jeu_reconfort_affiche_sans_effet_de_combat_et_sauvegarde_v27()
    {
        AdventureSession a = In("zone.vallons", 15, new SavedFeasts(new[] { new SavedFeast(Carnaval, dishes: 2) }));
        Assert.Null(a.FeastNow);   // un plat se mange aussi hors fête
        var dish = Assert.Single(a.FeastDishes);
        Assert.Equal((Carnaval, 2, "fete_f03_p01"), (dish.Festival, dish.Count, dish.Icon));
        Assert.Equal(Texts.Get(Feasts.Value.Feast(Carnaval).Dish.Name), dish.Name);
        var me = a.World.Zone.Engine.View(a.World.CombatantId);
        string before = $"{me.Health}|{me.Endurance}|{me.MaxEndurance}|{a.World.Money}";
        a.DrainMessages();
        Assert.Null(a.ComfortText);
        Assert.True(a.EatFeastDish(Carnaval));
        Assert.Contains($"Vous mangez : {dish.Name}. Un moment de réconfort.", a.DrainMessages());
        Assert.Equal($"Réconforté ({dish.Name})", a.ComfortText);
        var after = a.World.Zone.Engine.View(a.World.CombatantId);
        Assert.Equal(before, $"{after.Health}|{after.Endurance}|{after.MaxEndurance}|{a.World.Money}");   // aucune statistique, aucun soin
        Assert.Equal(1, a.World.FeastState.Of(Carnaval).Dishes);
        Assert.True(a.EatFeastDish(Carnaval));
        Assert.Empty(a.FeastDishes);
        Assert.False(a.EatFeastDish(Carnaval));
        Assert.Contains("Vous n'avez plus de ce plat.", a.DrainMessages());
        Assert.Equal(27, SaveValidator.CurrentSchemaVersion);   // format de sauvegarde inchangé
    }

    [Fact]
    public void Tenues_aux_teintes_de_la_fete()
    {
        HumanLook look = HumanLooks.For("npc.habitant", "pnj");
        for (int feast = 1; feast <= 12; feast++)
        {
            int[] tints = HumanWardrobe.FeastTints(feast);
            Assert.Equal(2, tints.Length);
            Assert.All(tints, t => Assert.InRange(t, 16, HumanWardrobe.Tints.Length - 1));
            WardrobePlan plan = HumanWardrobe.Plan("npc.habitant", "pnj", look, WardrobeSeason.Winter, false, true, 5, feast);
            Assert.Contains(plan.Tint, tints);
        }
        Assert.Equal(24, Enumerable.Range(1, 12).SelectMany(HumanWardrobe.FeastTints).Distinct().Count());
        // Hors fête d'Orsédia : les teintes de fête d'avant ; un jour ordinaire, rien ne change.
        Assert.Equal(HumanWardrobe.Plan("npc.habitant", "pnj", look, WardrobeSeason.Winter, false, true, 5).Tint,
            HumanWardrobe.Plan("npc.habitant", "pnj", look, WardrobeSeason.Winter, false, true, 5, 0).Tint);
        Assert.Equal(HumanWardrobe.Plan("npc.habitant", "pnj", look, WardrobeSeason.Winter, false, false, 5).Tint,
            HumanWardrobe.Plan("npc.habitant", "pnj", look, WardrobeSeason.Winter, false, false, 5, 12).Tint);
        string bodies = File.ReadAllText(Unity("Game/HumanBodies.cs"));
        Assert.Contains("HumanWardrobe.MaxOptional(Application.isMobilePlatform), Festival ? Feast : 0);", bodies);
        Assert.Contains("HumanBodies.Feast = a.FeastNumber;", File.ReadAllText(Unity("Game/EdsGame.cs")));
    }

    [Fact]
    public void Ambiance_de_chaque_fete_et_ecrans_de_chargement()
    {
        for (int feast = 1; feast <= 12; feast++)
            Assert.False(FeastMood.Of(feast).Equals(FeastMood.None), feast.ToString());
        Assert.Equal(FeastMood.None, FeastMood.Of(0));
        Assert.True(FeastMood.Of(12).FogEnd < 1 && FeastMood.Of(10).FogEnd < 1);   // brume du Givre et des Échos
        Assert.Equal(1.0, FeastMood.Of(1).Lamps);   // lanternes

        AdventureSession fete = In("zone.vallons", 13, SavedFeasts.Empty);   // le Carnaval
        Assert.Equal(FeastMood.Of(3), fete.FeastMood);
        LoadingScreen screen = fete.LoadingHere();
        Assert.Equal("ch_fete_03", screen.File);
        Assert.Equal(Feasts.Value.Feast(Carnaval).Motto, screen.TextKey);
        Assert.Equal("EdsInterface/Chargements/1920/ch_fete_03", LoadingScreens.ResourcePath(screen, DeviceTier.Full));
        AdventureSession plain = In("zone.vallons", 15, SavedFeasts.Empty);
        Assert.Equal(FeastMood.None, plain.FeastMood);
        Assert.Null(plain.LoadingHere().File);
        AdventureSession inside = In("zone.auberge", 13, SavedFeasts.Empty);
        Assert.Equal(FeastMood.None, inside.FeastMood);   // l'ambiance est celle du dehors

        foreach ((string folder, int w, int h) in new[] { ("1920", 1920, 1080), ("1280", 1280, 720) })
            for (int feast = 1; feast <= 12; feast++)
                Assert.Equal((w, h), JpegSize(Unity($"Interface/Resources/EdsInterface/Chargements/{folder}/ch_fete_{feast:00}.jpg")));
        string world = File.ReadAllText(Unity("Game/StyleWorld.cs"));
        Assert.Contains("Tint(_feast.AmbientTint)", world);
        Assert.Contains("(float)_feast.FogEnd", world);
        Assert.Contains("StyleWorld.SetFeast(a.FeastMood);", File.ReadAllText(Unity("Game/EdsGame.cs")));
        Assert.Contains("string comfort = a.ComfortText;", File.ReadAllText(Unity("Game/HudView.cs")));
        Assert.Contains("_session.EatFeastDish(_dishes[index].Festival)", File.ReadAllText(Unity("Game/AdventureBook.Feast.cs")));
    }

    private static (int, int) JpegSize(string path)
    {
        byte[] b = File.ReadAllBytes(path);
        int i = 2;
        while (i < b.Length)
        {
            int marker = b[i + 1], len = (b[i + 2] << 8) | b[i + 3];
            if (marker >= 0xC0 && marker <= 0xC2) return ((b[i + 7] << 8) | b[i + 8], (b[i + 5] << 8) | b[i + 6]);
            i += 2 + len;
        }
        throw new InvalidDataException(path);
    }
}
