using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EdS.Data.Saves;
using EdS.Runtime;
using EdS.World;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.32.1, les vraies cartes des Trois Feux (livraison ChatGPT du 17 novembre 2026) : correspondance carte → fichier selon la
/// couleur, la valeur et l'édition ; les 56 fichiers (27 faces et un dos, ordinaire et doré) existent en 376 × 528 avec transparence ; le
/// tapis et le cycle aux bonnes dimensions ; l'icône du jeu doré ; la table dit quelle édition le joueur emploie.
/// </summary>
[Trait("Category", "World")]
public class V2321CartesImagesTests
{
    private static string Resources => Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Interface", "Resources");

    private static (int Width, int Height, int ColorType) Png(string path)
    {
        byte[] png = File.ReadAllBytes(path);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png.Take(4).ToArray());
        return ((png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19], (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23], png[25]);
    }

    private static (int Width, int Height) Jpeg(string path)
    {
        byte[] b = File.ReadAllBytes(path);
        Assert.Equal((byte)0xFF, b[0]);
        Assert.Equal((byte)0xD8, b[1]);
        for (int i = 2; i + 9 < b.Length;)
        {
            Assert.Equal((byte)0xFF, b[i]);
            byte marker = b[i + 1];
            if (marker >= 0xC0 && marker <= 0xC3) return ((b[i + 7] << 8) | b[i + 8], (b[i + 5] << 8) | b[i + 6]);
            i += 2 + ((b[i + 2] << 8) | b[i + 3]);
        }
        throw new InvalidDataException(path);
    }

    [Fact]
    public void Carte_vers_fichier_selon_couleur_valeur_et_edition()
    {
        Assert.Equal("TF_STD_S07_Soleil", CardArt.Face(CardSuit.Sun, 7, false));
        Assert.Equal("TF_STD_L01_Lune", CardArt.Face(CardSuit.Moon, 1, false));
        Assert.Equal("TF_STD_B09_Brume", CardArt.Face(CardSuit.Mist, 9, false));
        Assert.Equal("TF_OR_S05_Soleil", CardArt.Face(CardSuit.Sun, 5, true));
        Assert.Equal("TF_OR_L09_Lune", CardArt.Face(CardSuit.Moon, 9, true));
        Assert.Equal("TF_OR_B01_Brume", CardArt.Face(CardSuit.Mist, 1, true));
        Assert.Equal("TF_STD_Dos", CardArt.Back(false));
        Assert.Equal("TF_OR_Dos", CardArt.Back(true));
        Assert.Throws<ArgumentOutOfRangeException>(() => CardArt.Face(CardSuit.Sun, 0, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => CardArt.Face(CardSuit.Sun, 10, true));
        // Les 54 faces sont toutes différentes.
        var names = new List<string>();
        foreach (bool gold in new[] { false, true })
            foreach (CardSuit suit in Enum.GetValues<CardSuit>())
                for (int v = 1; v <= CardMatch.MaxValue; v++) names.Add(CardArt.Face(suit, v, gold));
        Assert.Equal(54, names.Distinct().Count());
    }

    [Fact]
    public void Les_56_cartes_existent_en_376_sur_528_avec_transparence_et_rien_de_plus()
    {
        string folder = Path.Combine(Resources, "EdsTroisFeux", "Cartes");
        var expected = new List<string> { CardArt.Back(false), CardArt.Back(true) };
        foreach (bool gold in new[] { false, true })
            foreach (CardSuit suit in Enum.GetValues<CardSuit>())
                for (int v = 1; v <= CardMatch.MaxValue; v++) expected.Add(CardArt.Face(suit, v, gold));
        Assert.Equal(56, expected.Count);
        Assert.Equal("EdsTroisFeux/Cartes/", CardArt.Folder);
        foreach (string name in expected)
            Assert.Equal((CardArt.Width, CardArt.Height, 6), Png(Path.Combine(folder, name + ".png")));   // 6 : couleurs avec transparence
        Assert.Equal(expected.OrderBy(n => n, StringComparer.Ordinal), Directory.GetFiles(folder, "*.png").Select(f => Path.GetFileNameWithoutExtension(f)!)
            .OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal((376, 528), (CardArt.Width, CardArt.Height));
    }

    [Fact]
    public void Tapis_cycle_et_icone_du_jeu_dore_aux_bonnes_dimensions()
    {
        string root = Path.Combine(Resources, "EdsTroisFeux");
        Assert.Equal((1920, 1080), Jpeg(Path.Combine(root, "tapis_1920.jpg")));
        Assert.Equal((1280, 720), Jpeg(Path.Combine(root, "tapis_1280.jpg")));
        Assert.Equal("EdsTroisFeux/tapis_1920", CardArt.Mat(true));
        Assert.Equal("EdsTroisFeux/tapis_1280", CardArt.Mat(false));
        Assert.Equal((256, 256, 6), Png(Path.Combine(root, "cycle.png")));
        Assert.Equal("EdsTroisFeux/cycle", CardArt.Cycle);
        // L'objet décoratif a l'icône du dos doré (128 × 128 comme les autres icônes peintes).
        Assert.Equal((128, 128, 6), Png(Path.Combine(Resources, "EdsInterface", "Peintes", CardArt.RareDeckIcon + ".png")));
        using JsonDocument items = JsonDocument.Parse(File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "data", "v0", "world", "items.json")));
        JsonElement deck = items.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetString() == CardMatch.RareDeck);
        Assert.Equal(CardArt.RareDeckIcon, deck.GetProperty("icon").GetString());
    }

    [Fact]
    public void La_table_dit_l_edition_les_manches_et_les_cartes_posees()
    {
        var game = new SavedCardGame("npc.aubergiste", SavedCards.StyleOf("npc.aubergiste"), 0, 0, 10, new[] { 1 });
        var cards = new SavedCards(0x77UL, 1, 0, false, 0, new Dictionary<string, int> { ["npc.aubergiste"] = 1 }, 0, game);
        string save = Path.Combine(Path.GetTempPath(), "eds-v2321-" + Guid.NewGuid().ToString("N") + ".json");
        SaveStore.Write(save, new WorldSave("zone.vallons", CartesMisesTests.TickAt(0, 19 * 60 + 30), 0, Array.Empty<string>(), Hero(200).WithCards(cards)));
        AdventureSession a;
        try
        {
            a = Adventure(save);
        }
        finally
        {
            File.Delete(save);
        }
        CardTableView table = a.CardTable!;
        Assert.False(table.Gold);   // pas de jeu doré dans le sac : l'édition ordinaire
        Assert.Equal(1, table.RoundsPlayed);
        Assert.Single(table.RoundResults);
        Assert.Equal(CardMatch.HandSize - 1, table.NpcCards);
        Assert.NotNull(table.LastPlayer);
        Assert.NotNull(table.LastNpc);
        Assert.Equal(table.PlayerWins + table.NpcWins + table.RoundResults.Count(r => r == 0), table.RoundsPlayed);
        // Le joueur qui a le jeu rare dans son sac joue avec les cartes dorées.
        Assert.Equal(InventoryRefusal.None, a.World.Inventory.Add(CardMatch.RareDeck, 1));
        a.PlayCardAt(0);
        CardTableView next = a.CardTable!;
        Assert.True(next.Gold);
        Assert.Equal(2, next.RoundsPlayed);
        Assert.Equal(CardMatch.HandSize - 2, next.NpcCards);
    }

    [Fact]
    public void Entrees_de_la_table_relecture_independante_de_la_PR_211()
    {
        string code = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "AdventureBook.Cards.cs"));
        // 1 : l'image d'ouverture est ignorée (le clic qui a ouvert la table ne pose pas de carte).
        Assert.Contains("_cardsInputFrame < Time.frameCount - 1", code, StringComparison.Ordinal);
        Assert.Contains("|| opening) return;", code, StringComparison.Ordinal);
        // 2 : la zone d'une carte au repos commence à sa place ; seule la carte soulevée monte ; le bouton finit avant la carte soulevée.
        Assert.Contains("i == lifted ? BottomRowY - Lift : BottomRowY", code, StringComparison.Ordinal);
        const float bottomRow = 652f, lift = 30f, leaveY = 586f, leaveHeight = 28f;
        Assert.True(leaveY + leaveHeight < bottomRow - lift);
        Assert.Contains("LeaveY = 586f, LeaveHeight = 28f", code, StringComparison.Ordinal);
        Assert.Contains("BottomRowY = 652f", code, StringComparison.Ordinal);
        Assert.Contains("Lift = 30f", code, StringComparison.Ordinal);
        // 3 : les cartes posées sans image ont le repli sombre.
        Assert.Contains("_cardsPlayedPlayer.color = mine != null", code, StringComparison.Ordinal);
        Assert.Contains("_cardsPlayedNpc.color = theirs != null", code, StringComparison.Ordinal);
        // Revue Codex : le score garde sa valeur d'avant tant que la carte du PNJ est face cachée.
        Assert.Contains("if (revealed) _cardsShownScore = table.Score;", code, StringComparison.Ordinal);
        Assert.Contains("_cardsScore.Value = _cardsShownScore;", code, StringComparison.Ordinal);
        Assert.DoesNotContain("_cardsScore.Value = table.Score", code, StringComparison.Ordinal);
        // Revue Codex : pas de nouvelle carte (souris, toucher, touches) pendant le retournement.
        Assert.Contains("public bool CardsFlipping => _cardsLoaded && _cardsFlip < FlipDone;", code, StringComparison.Ordinal);
        Assert.Contains("|| opening || CardsFlipping) return;", code, StringComparison.Ordinal);
        string game = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "EdsGame.cs"));
        Assert.Contains("!_book.CardsFlipping) a.PlayCardAt(card)", game, StringComparison.Ordinal);
        // 4 : dimensions multiples de 4 (compression de Unity sur PC).
        Assert.Equal(0, CardArt.Width % 4);
        Assert.Equal(0, CardArt.Height % 4);
    }
}
