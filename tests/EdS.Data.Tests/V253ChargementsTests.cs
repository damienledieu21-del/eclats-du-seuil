using System.Text.Json.Nodes;
using EdS.Data.Json;
using EdS.Data.Zones;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Runtime;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.53 (sujet 13, écrans de chargement peints) : le choix de l'image et de la phrase selon la destination, les douze phrases dans les
/// textes du jeu, les images livrées (deux tailles) et le fondu. Sans Unity. Affichage seulement.
/// </summary>
[Trait("Category", "Runtime")]
public sealed class V253ChargementsTests
{
    private static ZoneDefinition Zone() => ZoneJsonReader.ReadFile(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons"), Path.Combine(AppContext.BaseDirectory, "scenarios"),
        ShippedData, ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!).Zone!;

    private static string Id(ZoneDefinition zone, string safePoint) =>
        LoadingScreens.For(zone, zone.SafePoints.Single(s => s.Id == safePoint).Position).Id;

    [Fact]
    public void Les_points_sürs_menent_au_grand_lieu_de_leur_destination()
    {
        ZoneDefinition zone = Zone();
        Assert.Equal("village", Id(zone, "place_du_village"));
        Assert.Equal("bourg_aube", Id(zone, "place_du_bourg"));
        Assert.Equal("bourg_aube", Id(zone, "place_du_marche_de_la_ville"));
        Assert.Equal("bourg_aube", Id(zone, "porte_est_du_bourg"));
        Assert.Equal("village", Id(zone, "ferme_des_champs"));
        Assert.Equal("carriere_mine", Id(zone, "camp_de_la_carriere"));
        Assert.Equal("carriere_mine", Id(zone, "hameau_de_la_carriere"));
        Assert.Equal("carriere_mine", Id(zone, "hameau_des_mineurs"));
        Assert.Equal("hautes_collines", Id(zone, "planque_des_contrebandiers"));
        Assert.Equal("marais_lac", Id(zone, "pontons_du_lac"));
        Assert.Equal("fort_bandits", Id(zone, "pied_du_fort"));
        Assert.Equal("vallee_cascade", Id(zone, "gue_de_la_vallee"));
        Assert.Equal("terres_seuil", Id(zone, "lisiere_des_terres_grises"));
        Assert.Equal("chateau_coteaux", Id(zone, "chateau_viticole"));
    }

    [Fact]
    public void Chaque_lieu_des_donnees_a_une_image_ou_laisse_le_suivant_decider_et_chaque_image_est_atteignable()
    {
        ZoneDefinition zone = Zone();
        string[] noOwnImage = { "plaine_est", "plaine_de_l_est", "plaine_de_l_ouest" };
        foreach (Place place in zone.Places)
            Assert.True(LoadingScreens.ForPlace(place.Id) != null || noOwnImage.Contains(place.Id), place.Id + " n'a pas d'écran de chargement");
        foreach (LoadingScreen screen in LoadingScreens.All)
            Assert.Contains(zone.Places, p => LoadingScreens.ForPlace(p.Id)?.Id == screen.Id);
        Assert.Equal(12, LoadingScreens.All.Select(s => s.Id).Distinct().Count());
    }

    [Fact]
    public void Sans_lieu_connu_le_village()
    {
        ZoneDefinition zone = Zone();
        Assert.Same(LoadingScreens.Default, LoadingScreens.For(zone, null));
        Assert.Equal("village", LoadingScreens.For(zone, new GroundVector(Fixed.Parse("100000"), Fixed.Parse("100000"))).Id);
        Assert.Equal("village", LoadingScreens.Default.Id);
        Assert.Null(LoadingScreens.ForPlace("lieu_inconnu"));
    }

    [Fact]
    public void Les_douze_phrases_sont_dans_les_textes_du_jeu_et_les_images_existent_en_deux_tailles()
    {
        string root = Lot93UnityPackageTests.Root;
        JsonObject entries = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "data", "v0", "world", "texts", "fr.json")))!["data"]!["entries"]!.AsObject();
        foreach (LoadingScreen screen in LoadingScreens.All)
        {
            string text = entries[screen.TextKey]?.GetValue<string>() ?? "";
            Assert.True(text.Length > 20, screen.TextKey);
            Assert.True(text.Split(' ').Length <= 15, screen.TextKey);
            foreach (DeviceTier tier in new[] { DeviceTier.Phone, DeviceTier.Modest, DeviceTier.Full })
            {
                string file = Path.Combine(root, "unity", "Assets", "EdS", "Interface", "Resources", LoadingScreens.ResourcePath(screen, tier) + ".jpg");
                Assert.True(File.Exists(file), file);
                Assert.True(new FileInfo(file).Length < 1_000_000, file);   // JPEG qualité 85, jamais de PNG 4K
                (int w, int h) = JpegSize(file);
                Assert.Equal(LoadingScreens.ResourcePath(screen, tier).Contains("/1280/") ? (1280, 720) : (1920, 1080), (w, h));   // taille et ratio 16:9 réels
            }
        }
    }

    private static (int Width, int Height) JpegSize(string file)
    {
        byte[] b = File.ReadAllBytes(file);
        Assert.True(b[0] == 0xFF && b[1] == 0xD8, file + " n'est pas un JPEG");
        int i = 2;
        while (i + 9 < b.Length)
        {
            Assert.Equal(0xFF, b[i]);
            int marker = b[i + 1];
            int length = (b[i + 2] << 8) | b[i + 3];
            if (marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
                return ((b[i + 7] << 8) | b[i + 8], (b[i + 5] << 8) | b[i + 6]);
            i += 2 + length;
        }
        throw new InvalidDataException(file);
    }

    [Fact]
    public void Sous_l_ecran_de_chargement_aucune_commande_ne_passe()
    {
        // Revue Codex de la PR #207 : les raccourcis (Entrée -> halte, Échap, I/J/K/L, F6, N) passent tous par « !creating » ; l'écran actif
        // compte comme le créateur de personnage, l'interface IMGUI est désactivée sous lui et le temps de jeu est nul.
        string game = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "EdsGame.cs")).Replace("\r\n", "\n");
        Assert.Contains("creating |= _loading.Active;", game, StringComparison.Ordinal);
        int creating = game.IndexOf("creating |= _loading.Active;", StringComparison.Ordinal);
        Assert.True(creating < game.IndexOf("Input.GetKeyDown(KeyCode.N)", creating, StringComparison.Ordinal));
        Assert.True(creating < game.IndexOf("a.Rest()", creating, StringComparison.Ordinal));
        Assert.Contains("else if (_loading.Active)", game, StringComparison.Ordinal);
        Assert.Contains("if (_loading.Active) GUI.enabled = false;", game, StringComparison.Ordinal);
        Assert.Contains("bool frozen = _paused || _loading.Active", game, StringComparison.Ordinal);
    }

    [Fact]
    public void Le_telephone_et_le_pc_modeste_prennent_la_petite_image()
    {
        LoadingScreen screen = LoadingScreens.All[0];
        Assert.Equal("EdsInterface/Chargements/1280/ch_01_bourg_aube", LoadingScreens.ResourcePath(screen, DeviceTier.Phone));
        Assert.Equal("EdsInterface/Chargements/1280/ch_01_bourg_aube", LoadingScreens.ResourcePath(screen, DeviceTier.Modest));
        Assert.Equal("EdsInterface/Chargements/1920/ch_01_bourg_aube", LoadingScreens.ResourcePath(screen, DeviceTier.Full));
    }

    [Fact]
    public void Le_fondu_entre_et_sort_sans_a_coup()
    {
        Assert.Equal(0f, LoadingScreens.Opacity(0f));
        Assert.Equal(1f, LoadingScreens.Opacity(LoadingScreens.FadeSeconds));
        Assert.Equal(1f, LoadingScreens.Opacity(LoadingScreens.HoldSeconds / 2f));
        Assert.InRange(LoadingScreens.Opacity(LoadingScreens.HoldSeconds - LoadingScreens.FadeSeconds / 2f), 0.49f, 0.51f);
        Assert.Equal(0f, LoadingScreens.Opacity(LoadingScreens.HoldSeconds));
        Assert.Equal(0f, LoadingScreens.Opacity(-1f));
    }
}
