using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using EdS.Data.Feasts;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Zones;
using EdS.Runtime;
using EdS.World;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot F4 (les 12 fêtes d'Orsédia dans le jeu Unity) : l'étal de Maître Florentin (écran d'achat, rattrapage, monnaie de la fête), les
/// activités auprès des invités (réponses du dialogue), les annonces des gains, la monnaie dans la bourse, l'étal posé sur la place avec les
/// décorations du mois (modèles Tripo), les icônes. Cœur testé hors Unity, et contrôles de source pour Unity.
/// </summary>
[Trait("Category", "World")]
public sealed class FetesF4EtalTests
{
    private const string Carnaval = "festival.carnaval";
    private const long CarnavalDay = 13;
    private static readonly Lazy<StoryBook> Story = new(() => Book(Zone()));
    private static readonly Lazy<FeastBook> Feasts = new(() => FeastJsonReader.ReadFile(FeastJsonReader.PathIn(DataDirectory), Story.Value, Texts).Book!);
    private static string Root => Lot93UnityPackageTests.Root;
    private static string Unity(string relative) => Path.Combine(Root, "unity", "Assets", "EdS", relative);
    private static string Source(string relative) => File.ReadAllText(Unity(relative));

    private static long TickAt(long day, int hour) => WorldEventSchedule.TickAt(new WorldClock(50), day * 1440 + hour * 60);

    /// <summary>Aventure sur les données livrées, le personnage arrivé en (<paramref name="x"/>, <paramref name="z"/>) (point d'entrée déplacé).</summary>
    private static T WithAdventure<T>(double x, double z, long day, int hour, SavedFeasts feasts, Func<AdventureSession, T> act)
    {
        string folder = Path.Combine(Path.GetTempPath(), "eds-f4-" + Guid.NewGuid().ToString("N"));
        try
        {
            string data = Path.Combine(folder, "data", "v0"), scenarios = Path.Combine(folder, "scenarios");
            foreach ((string from, string to) in new[] { (DataDirectory, data), (ScenariosDirectory, scenarios) })
                foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
                {
                    string target = Path.Combine(to, Path.GetRelativePath(from, file));
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(file, target, true);
                }
            string zonePath = ZoneJsonReader.PathIn(data, "zone.vallons");
            JsonObject root = JsonNode.Parse(File.ReadAllText(zonePath))!.AsObject();
            root["data"]!["safePoints"]![0]!["position"] = new JsonArray(x, z);
            File.WriteAllText(zonePath, root.ToJsonString());
            string save = Path.Combine(folder, "sauvegarde.json");
            SaveStore.Write(save, new WorldSave("zone.vallons", TickAt(day, hour), 0, new[] { PlayerCharacters.PlacesKey }, Hero().WithFeasts(feasts)));
            return act(Adventure(save, data, scenarios));
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Etal_du_forain_ouvert_depuis_son_dialogue_achats_rattrapage_et_monnaie()
    {
        WithAdventure(-1088.5, -36.2, CarnavalDay, 12, new SavedFeasts(new[] { new SavedFeast(Carnaval, 100) }), a =>
        {
            Assert.Equal(Carnaval, a.FeastNow?.Festival);
            a.DrainMessages();
            Assert.Empty(a.FeastAnswers);   // aucun dialogue ouvert
            Assert.Equal(DialogueRefusal.None, a.World.Talk("npc.forain", out _));
            IReadOnlyList<string> answers = a.DialogueAnswers;
            Assert.Equal("[Fête] Voir l'étal de fête.", answers[^1]);
            Assert.Equal(DialogueRefusal.None, a.Choose(answers.Count - 1));
            Assert.True(a.FeastStallOpen);
            Assert.Null(a.Dialogue);
            Assert.True(a.TakeFeastStallRequest());
            Assert.False(a.TakeFeastStallRequest());   // une seule fois : Échap ne le rouvre pas
            Assert.Equal("Étal de fête", a.FeastStallTitle);
            Assert.Contains("Carnaval", a.FeastStallSubtitle);

            IReadOnlyList<FeastShopLine> lines = a.FeastShopLines();
            Assert.Equal(new[] { "F03_D01", "F03_D02", "F03_D03", "F03_D04", "F03_D05", "F03_C01", "F03_P01", "F03_R01" }, lines.Select(l => l.Code));
            FeastShopLine reward = lines.Single(l => l.Category == FeastCategory.Reward);
            Assert.False(reward.Buyable);
            Assert.Equal("Rien à rattraper", reward.PriceText);   // aucune édition d'avant
            Assert.Equal("Récompense de l'an dernier (rattrapage)", reward.Kind);
            FeastShopLine costume = lines.Single(l => l.Category == FeastCategory.Costume);
            Assert.True(costume.TooDear);   // 120 pour 100 pièces
            Assert.False(costume.Buyable);
            Assert.All(lines, l => Assert.Equal("fete_" + l.Code.ToLowerInvariant(), l.Icon));

            Assert.Equal("Obole du Masque · 100 / 999", a.FeastWalletText);
            Assert.True(a.BuyFeastOffer("F03_D01"));
            Assert.Contains(a.DrainMessages(), m => m.StartsWith("Acheté : ") && m.Contains("Obole du Masque"));
            Assert.Equal("Acquis", a.FeastShopLines().Single(l => l.Code == "F03_D01").PriceText);
            Assert.False(a.BuyFeastOffer("F03_D01"));
            Assert.Contains("Vous l'avez déjà.", a.DrainMessages());
            Assert.False(a.BuyFeastOffer("F03_C01"));
            Assert.Contains("Obole du Masque : pas assez de pièces.", a.DrainMessages());
            Assert.False(a.BuyFeastOffer("F04_D01"));   // une référence d'une autre fête : refusée, rien ne change
            long left = 100 - Feasts.Value.Ref("F03_D01")!.Price!.Value;
            Assert.Equal(left, a.World.FeastState.Of(Carnaval).Coins);
            FeastWalletLine wallet = a.FeastWallets[0];
            Assert.True(wallet.Current);
            Assert.Equal(left + " / 999", wallet.CoinsText);

            // Le bouton Parler ferme l'étal et ouvre le dialogue du forain.
            Assert.True(a.TalkToFeastMerchant());
            Assert.False(a.FeastStallOpen);
            Assert.NotNull(a.Dialogue);
            return 0;
        });
    }

    /// <summary>Aventure ouverte dans la zone <paramref name="zone"/> au jour et à l'heure donnés (données livrées).</summary>
    private static AdventureSession In(string zone, long day, int hour)
    {
        string save = Path.Combine(Path.GetTempPath(), "eds-f4-" + Guid.NewGuid().ToString("N") + ".json");
        SaveStore.Write(save, new WorldSave(zone, TickAt(day, hour), 0, new[] { PlayerCharacters.PlacesKey }, Hero()));
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
    public void Etal_jamais_en_interieur_ni_hors_de_la_zone_de_l_histoire()
    {
        // Relecture indépendante de la PR #218 : le jour du Carnaval, dans l'auberge, ni étal ni décor (le cœur et la vue le refusent).
        AdventureSession inside = In("zone.auberge", CarnavalDay, 12);
        Assert.True(inside.Indoors);
        Assert.False(inside.World.InStoryZone);
        Assert.Null(inside.World.FeastStall);
        Assert.Null(inside.FeastStallLook);
        Assert.Null(inside.FeastStallLayout);
        AdventureSession outside = In("zone.vallons", CarnavalDay, 12);
        Assert.NotNull(outside.FeastStallLook);
        var layout = outside.FeastStallLayout!.Value;
        Assert.Equal(Feasts.Value.Feast(Carnaval).Stall.Select(r => r.Code), layout.Pieces.Select(p => p.Code));
        Assert.Contains("FeastStallLook look = adventure.Indoors ? null : adventure.FeastStallLook;", Source("Game/WorldMarkers.Feasts.cs"));
    }

    [Fact]
    public void Enveloppe_X_largeur_Y_profondeur_Z_hauteur_comme_les_modeles()
    {
        // Revue Codex de la PR #218 (P2, sans suite) : la profondeur d'une décoration est bien SizeY. Convention du dossier des fêtes et des
        // modèles Tripo (face en -Y, Z vers le haut dans Blender) : X largeur vue de face, Y profondeur, Z hauteur. Relevés des exports de jeu
        // (Blender, dimensions x, y, z en m) : la profondeur des modèles est celle des données (y), la hauteur jamais (z).
        var measured = new Dictionary<string, (double X, double Y, double Z)>
        {
            ["F03_D01"] = (0.625, 0.08, 0.863), ["F06_D05"] = (0.622, 0.08, 0.87), ["F01_D05"] = (1.644, 0.45, 1.548),
            ["F02_D03"] = (1.553, 0.541, 0.95), ["F09_D04"] = (1.591, 0.784, 1.1),
        };
        foreach ((string code, (double mx, double my, double mz)) in measured)
        {
            FeastSize size = Feasts.Value.Ref(code)!.Size;
            double y = size.Y.Raw / 10000.0, z = size.Z.Raw / 10000.0;
            Assert.True(Math.Abs(my - y) <= 0.15, $"{code} : profondeur du modèle {my}, données y {y}");
            Assert.True(Math.Abs(my - z) > 0.3, $"{code} : la hauteur (z) n'est pas la profondeur");
        }
        // Une bannière (F03_D01, 8 cm d'épaisseur, 1,7 m de haut) : la pose de l'étal la tient pour mince et large (X, Y), pas pour haute.
        FeastRef banner = Feasts.Value.Ref("F03_D01")!;
        var layout = FeastLooks.Stall(-1088, -37, 0, new[] { (banner.Code, banner.Size.X.Raw / 10000.0, banner.Size.Y.Raw / 10000.0, banner.Size.Z.Raw / 10000.0) },
            Array.Empty<IReadOnlyList<(double X, double Z)>>());
        FeastPiece piece = Assert.Single(layout.Pieces);
        Assert.Equal((0.8, 0.1), (piece.Width, piece.Depth));   // 0,08 m relevé à 0,1 m au moins
    }

    [Fact]
    public void Toutes_les_autres_monnaies_visibles_a_l_etal()
    {
        // Revue Codex de la PR #218 (P2) : onze autres monnaies au plus, toutes affichées (deux colonnes de six).
        string panel = Source("Game/AdventureBook.Feast.cs");
        Assert.Contains("private const int FeastWalletRows = 12, FeastWalletLines = 6;", panel);
        Assert.True(12 >= Feasts.Value.Feasts.Count - 1);
        AdventureSession a = WithAdventure(-1088.5, -36.2, CarnavalDay, 12,
            new SavedFeasts(Feasts.Value.Feasts.Select(f => new SavedFeast(f.Festival, 5))), x => x);
        Assert.Equal(12, a.FeastWallets.Count);
        Assert.Equal(11, a.FeastWallets.Count(w => !w.Current));
    }

    [Fact]
    public void Double_clic_sur_Acheter_un_seul_achat_jusqu_au_rafraichissement()
    {
        // Relecture indépendante de la PR #218 : un plat s'achète plusieurs fois : un double clic n'en achète qu'un.
        string panel = Source("Game/AdventureBook.Feast.cs");
        Assert.Contains("if (_session == null || row >= _feastLines.Count || _feastBuyLocked) return;", panel);
        Assert.Contains("_feastBuyLocked = true;", panel);
        Assert.True(panel.IndexOf("_feastBuyLocked = false;", StringComparison.Ordinal) < panel.IndexOf("_feastLines = a.FeastShopLines();", StringComparison.Ordinal));
    }

    [Fact]
    public void Rattrapage_de_l_an_dernier_a_prix_plein_sans_titre()
    {
        // L'an d'après (édition 1) : la récompense de l'édition 0, ratée, s'achète à prix plein.
        WithAdventure(-1088.5, -36.2, CarnavalDay + WorldCalendar.DaysPerYear, 12, new SavedFeasts(new[] { new SavedFeast(Carnaval, 200) }), a =>
        {
            Assert.Equal(DialogueRefusal.None, a.World.Talk("npc.forain", out _));
            Assert.Equal(DialogueRefusal.None, a.Choose(a.DialogueAnswers.Count - 1));
            FeastShopLine reward = a.FeastShopLines().Single(l => l.Category == FeastCategory.Reward);
            Assert.True(reward.Buyable);
            Assert.Equal("150 Obole du Masque", reward.PriceText);
            a.DrainMessages();
            Assert.True(a.BuyFeastOffer(reward.Code));
            Assert.Contains("Récompense de l'an dernier rattrapée : Masque de l'Autre Rôle.", a.DrainMessages());
            Assert.Empty(a.World.FeastTitles);   // sans le titre
            return 0;
        });
    }

    [Fact]
    public void Activite_aupres_d_un_invite_gain_annonce_puis_deja_faite()
    {
        WithAdventure(-1089.8, -31.4, CarnavalDay, 12, SavedFeasts.Empty, a =>
        {
            a.DrainMessages();
            Assert.Equal(DialogueRefusal.None, a.World.Talk("npc.lison", out _));
            string label = "[Fête] Le défilé des masques (+10 Obole du Masque)";
            Feast carnaval = Feasts.Value.Feast(Carnaval);
            FeastActivity masques = carnaval.Activity("masques")!;
            label = $"[Fête] {Texts.Get(masques.Name)} (+10 Obole du Masque)";
            int index = a.DialogueAnswers.ToList().IndexOf(label);
            Assert.True(index >= 0, string.Join(" | ", a.DialogueAnswers));
            Assert.Equal(DialogueRefusal.None, a.Choose(index));
            IReadOnlyList<string> messages = a.DrainMessages();
            Assert.Contains("+10 Obole du Masque (10 / 999).", messages);
            Assert.Equal(10, a.World.FeastState.Of(Carnaval).Coins);
            Assert.StartsWith("Activités de la fête : 1 / 2", a.FeastProgressText);
            // Même jour : la réponse le dit, et la choisir est refusé sans rien changer.
            if (a.Dialogue is null) a.World.Talk("npc.lison", out _);
            Assert.Contains($"[Fête] {Texts.Get(masques.Name)} (déjà fait aujourd'hui)", a.FeastAnswers);
            int again = a.DialogueAnswers.ToList().IndexOf($"[Fête] {Texts.Get(masques.Name)} (déjà fait aujourd'hui)");
            Assert.Equal(DialogueRefusal.None, a.Choose(again));
            Assert.Contains("Déjà fait aujourd'hui : revenez demain, tant que dure la fête.", a.DrainMessages());
            Assert.Equal(10, a.World.FeastState.Of(Carnaval).Coins);
            // L'inventaire montre la monnaie de la fête en cours à la suite de la bourse.
            Assert.Equal("Obole du Masque", a.FeastWallets[0].Name);
            return 0;
        });
    }

    [Fact]
    public void Annonces_plafond_atteint_et_fete_gagnee()
    {
        Feast carnaval = Feasts.Value.Feast(Carnaval);
        string[] done = carnaval.Activities.Where(x => x.Host != null).Select(x => x.Id).ToArray();
        // 995 pièces et une activité déjà faite dans l'édition 0 : la seconde rapporte 4 (6 perdues), gagne la fête et son titre.
        var state = new SavedFeasts(new[] { new SavedFeast(Carnaval, 995, 0, new Dictionary<string, long> { [done[1]] = CarnavalDay - 1 }) });
        WithAdventure(-1089.8, -31.4, CarnavalDay, 12, state, a =>
        {
            a.DrainMessages();
            Assert.True(a.JoinFeastActivity("masques"));
            IReadOnlyList<string> messages = a.DrainMessages();
            Assert.Contains("+4 Obole du Masque (999 / 999).", messages);
            Assert.Contains("Obole du Masque : réserve pleine (999 au plus), 6 pièce(s) perdue(s). L'activité compte quand même.", messages);
            Assert.True(messages.Any(m => m.Contains("gagnée") && m.Contains("Masque de l'Autre Rôle") && m.Contains("Visage du Carnaval")), string.Join(" | ", messages));
            Assert.Equal("Fête gagnée cette année : Masque de l'Autre Rôle.", a.FeastProgressText);
            return 0;
        });
    }

    [Fact]
    public void Etal_pose_sur_la_place_avec_les_decorations_du_mois_sans_rien_bloquer()
    {
        // La place du forain (données) et son regard au repos (vers le point sûr le plus proche, comme WorldMarkers).
        JsonElement story = JsonDocument.Parse(File.ReadAllText(StoryJsonReader.StoryPathIn(DataDirectory))).RootElement.GetProperty("data");
        JsonElement forain = story.GetProperty("npcs").EnumerateArray().Single(n => n.GetProperty("id").GetString() == "npc.forain");
        double fx = forain.GetProperty("position")[0].GetDouble(), fz = forain.GetProperty("position")[1].GetDouble();
        JsonElement zone = JsonDocument.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons"))).RootElement.GetProperty("data");
        (double X, double Z) home = zone.GetProperty("safePoints").EnumerateArray()
            .Select(s => (X: s.GetProperty("position")[0].GetDouble(), Z: s.GetProperty("position")[1].GetDouble()))
            .Where(s => (s.X - fx) * (s.X - fx) + (s.Z - fz) * (s.Z - fz) > 0.01).OrderBy(s => (s.X - fx) * (s.X - fx) + (s.Z - fz) * (s.Z - fz)).First();
        double face = StyleRules.FaceYaw(fx, fz, home.X, home.Z);
        JsonElement scenario = JsonDocument.Parse(File.ReadAllText(Path.Combine(ScenariosDirectory, "zones", "vallons.json"))).RootElement;
        var obstacles = Obstacles(scenario).Select(o => (IReadOnlyList<(double X, double Z)>)o.EnumerateArray()
            .Select(p => (p[0].GetDouble(), p[1].GetDouble())).ToList()).ToList();
        Assert.True(obstacles.Count > 400);
        var roads = zone.GetProperty("roads").EnumerateArray().Select(r => new StyleRoad(r.GetProperty("id").GetString()!,
            r.GetProperty("points").EnumerateArray().Select(p => (p[0].GetDouble(), p[1].GetDouble())).ToList(), r.GetProperty("width").GetDouble(), false)).ToList();
        var streets = FeastLooks.Streets(roads)!;   // le dallage de la place n'est pas une rue
        Assert.Contains(roads, r => r.Id == "place_du_bourg");
        Assert.DoesNotContain(streets, r => r.Id.StartsWith("place_"));
        // Les autres PNJ de la place et les portes : jamais sous une décoration ni sous l'étal.
        var people = story.GetProperty("npcs").EnumerateArray().Where(n => n.GetProperty("id").GetString() != "npc.forain" && n.TryGetProperty("position", out _))
            .Select(n => (n.GetProperty("position")[0].GetDouble(), n.GetProperty("position")[1].GetDouble()))
            .Concat(zone.GetProperty("doors").EnumerateArray().Select(d => (d.GetProperty("position")[0].GetDouble(), d.GetProperty("position")[1].GetDouble())))
            .Concat(story.GetProperty("festivals").EnumerateArray().Where(f => f.TryGetProperty("guests", out _))
                .SelectMany(f => f.GetProperty("guests").EnumerateArray()).Select(g => (g.GetProperty("position")[0].GetDouble(), g.GetProperty("position")[1].GetDouble())))
            .ToList();
        foreach (Feast feast in Feasts.Value.Feasts.Where(f => f.Stall.Count > 0))
        {
            var decor = feast.Stall.Select(r => (r.Code, r.Size.X.Raw / 10000.0, r.Size.Y.Raw / 10000.0, r.Size.Z.Raw / 10000.0)).ToList();
            (double sx, double sz, double yaw, IReadOnlyList<FeastPiece> pieces) = FeastLooks.Stall(fx, fz, face, decor, obstacles, roads, people);
            Assert.InRange(Math.Sqrt((sx - fx) * (sx - fx) + (sz - fz) * (sz - fz)), FeastLooks.StallDistance - 1e-9, FeastLooks.StallDistance + 2 + 1e-9);
            Assert.Equal(feast.Stall.Select(r => r.Code), pieces.Select(p => p.Code));   // toutes posées, dans l'ordre des données
            var stall = FeastLooks.Box(sx, sz, yaw, FeastLooks.StallWidth, FeastLooks.StallDepth);
            // Relecture indépendante de la PR #218 : l'étal lui-même (son emprise entière) hors des obstacles et des routes, comme les décorations.
            foreach ((double cx, double cz, double r) in FeastLooks.Footprint(sx, sz, yaw, FeastLooks.StallWidth, FeastLooks.StallDepth))
            {
                Assert.All(obstacles, o => Assert.True(StyleRules.InsideDistance(cx, cz, o) <= -r + 1e-9, feast.Festival + " : étal sur un obstacle"));
                Assert.True(TownRules.RoadAt(cx, cz, streets).Edge >= r - FeastLooks.Margin, feast.Festival + " : étal sur une route");
                Assert.All(people, q => Assert.True(Math.Sqrt((cx - q.Item1) * (cx - q.Item1) + (cz - q.Item2) * (cz - q.Item2)) >= r, feast.Festival + " : étal sur un PNJ"));
            }
            foreach (FeastPiece p in pieces)
            {
                Assert.True(Math.Max(p.Width, p.Depth) <= 2.5, p.Code);   // tient sur un étal (2,5 m au plus)
                Assert.Equal(yaw, p.Yaw);   // l'avant vers les visiteurs, comme l'étal
                foreach ((double cx, double cz, double r) in p.Footprint)
                {
                    Assert.All(obstacles, o => Assert.True(StyleRules.InsideDistance(cx, cz, o) <= -r + 1e-9, p.Code + " dans un obstacle"));
                    Assert.True(StyleRules.InsideDistance(cx, cz, stall) <= -r + 1e-9, p.Code + " sur l'étal");
                    Assert.True(TownRules.RoadAt(cx, cz, streets).Edge >= r - FeastLooks.Margin, p.Code + " sur une route");
                    Assert.True(Math.Sqrt((cx - fx) * (cx - fx) + (cz - fz) * (cz - fz)) >= r, p.Code + " sur le forain");
                    Assert.All(people, q => Assert.True(Math.Sqrt((cx - q.Item1) * (cx - q.Item1) + (cz - q.Item2) * (cz - q.Item2)) >= r,
                        $"{feast.Festival} : {p.Code} sur un PNJ ou une porte ({q.Item1}, {q.Item2})"));
                    foreach (FeastPiece o in pieces.Where(o => o.Code != p.Code))
                        Assert.True(StyleRules.InsideDistance(cx, cz, FeastLooks.Box(o.X, o.Z, o.Yaw, o.Width, o.Depth)) <= -r + 1e-9, $"{p.Code} sur {o.Code}");
                }
            }
        }
    }

    private static IEnumerable<JsonElement> Obstacles(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.Object)
        {
            if (e.TryGetProperty("obstacles", out JsonElement list))
                return list.EnumerateArray().Select(o => o.GetProperty("points"));
            foreach (JsonProperty p in e.EnumerateObject())
                if (Obstacles(p.Value).ToList() is { Count: > 0 } found) return found;
        }
        else if (e.ValueKind == JsonValueKind.Array)
            foreach (JsonElement v in e.EnumerateArray())
                if (Obstacles(v).ToList() is { Count: > 0 } found) return found;
        return Array.Empty<JsonElement>();
    }

    [Fact]
    public void Modeles_des_decorations_de_l_etal_seulement_textures_au_budget()
    {
        string folder = Unity("Modeles/Fetes");
        string[] stall = Feasts.Value.Feasts.SelectMany(f => f.Stall).Select(r => r.Code).ToArray();
        Assert.Equal(40, stall.Length);
        // N'ajoute que ce qui sert : un dossier par décoration d'étal, rien d'autre.
        Assert.Equal(stall.OrderBy(c => c, StringComparer.Ordinal), Directory.GetDirectories(folder).Select(Path.GetFileName).OrderBy(c => c, StringComparer.Ordinal));
        long bytes = 0;
        foreach (string code in stall)
        {
            string fbx = Path.Combine(folder, code, code + ".fbx");
            Assert.True(File.Exists(fbx), fbx);
            Assert.Equal(code, FeastLooks.CodeOf(FeastLooks.Folder + "/" + code + "/" + code + ".fbx"));
            Assert.StartsWith("Kaydara FBX Binary", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(fbx), 0, 18));
            (int w, int h) = JpegSize(Path.Combine(folder, code, code + "_couleur.jpg"));
            Assert.True(w == h && (w == 1024 || w == 512), code);
            (int nw, int nh) = PngSize(Path.Combine(folder, code, code + "_normale.png"));
            Assert.Equal((512, 512), (nw, nh));
            bytes += Directory.GetFiles(Path.Combine(folder, code)).Sum(f => new FileInfo(f).Length);
            Assert.Equal(3, Directory.GetFiles(Path.Combine(folder, code)).Length);
        }
        Assert.True(bytes < 40L * 1024 * 1024, $"{bytes} octets");
        Assert.Null(FeastLooks.CodeOf("Assets/EdS/Modeles/LotC/02_Objets_Village/C_OV_A01/C_OV_A01.fbx"));
        Assert.Null(FeastLooks.CodeOf(FeastLooks.Folder + "/F01_D01/F01_D02.fbx"));
    }

    [Fact]
    public void Icones_peintes_des_108_references()
    {
        string folder = Unity("Interface/Resources/EdsInterface/Peintes");
        foreach (FeastRef r in Feasts.Value.Refs)
            Assert.Equal((128, 128), PngSize(Path.Combine(folder, FeastLooks.IconOf(r.Code) + ".png")));
        Assert.Equal(108, Directory.GetFiles(folder, "fete_*.png").Length);
    }

    [Fact]
    public void Textes_de_l_interface_des_fetes_tous_presents()
    {
        string code = File.ReadAllText(Path.Combine(Root, "src", "EdS.Runtime", "AdventureSession.Feasts.cs"));
        var keys = System.Text.RegularExpressions.Regex.Matches(code, "\"(text\\.ui\\.fete\\.[a-z_.]+)\"").Select(m => m.Groups[1].Value).Distinct().ToList();
        Assert.True(keys.Count > 25);
        Assert.All(keys, k => Assert.NotEqual(k, Texts.Get(k)));
        Assert.All(new[] { "text.ui.fete.offers", "text.ui.fete.buy", "text.ui.fete.talk", "text.ui.fete.wallet", "text.ui.fete.wallets", "text.ui.fete.rules", "text.ui.fete.no_wallet" },
            k => Assert.Contains("\"" + k + "\"", Source("Game/AdventureBook.Feast.cs")));
    }

    [Fact]
    public void Unity_panneau_de_l_etal_etal_sur_la_place_et_modeles_prepares()
    {
        // Le panneau : une page du livre, ouverte une seule fois depuis le dialogue, fermée par Échap, jeu figé (pas de fuite d'entrées).
        Assert.Contains("Merchant, Guild, Feast }", Source("Game/AdventureScreens.cs"));
        string book = Source("Game/AdventureBook.cs");
        Assert.Contains("BuildFeast(Page(AdventureScreens.Panel.Feast));", book);
        Assert.Contains("if (screens.Open == AdventureScreens.Panel.Feast) RefreshFeast(a);", book);
        string game = Source("Game/EdsGame.cs");
        Assert.Contains("if (a.TakeFeastStallRequest()) _screens.Show(AdventureScreens.Panel.Feast);", game);
        Assert.Contains("else if (_screens.Open != AdventureScreens.Panel.Feast && a.FeastStallOpen) a.CloseFeastStall();", game);
        Assert.Contains("a.GuildOpen || a.FeastStallOpen;", game);
        // Boutons uGUI (souris et toucher), pas d'entrée lue au clavier dans le panneau.
        string panel = Source("Game/AdventureBook.Feast.cs");
        Assert.Contains("UiKit.Button(row, \"Acheter\"", panel);
        Assert.Contains("_session?.TalkToFeastMerchant()", panel);
        Assert.DoesNotContain("Input.", panel);
        Assert.Contains("+ FeastPurse(a);", Source("Game/AdventureBook.Merchant.cs"));
        // L'étal sur la place : refait quand la fête change, retiré après elle.
        Assert.Contains("SyncFeastStall(adventure);", Source("Game/WorldMarkers.cs"));
        string stall = Source("Game/WorldMarkers.Feasts.cs");
        Assert.Contains("adventure.FeastStallLayout", stall);   // pose calculée par le cœur (FeastLooks.Stall)
        Assert.Contains("if (key == _feastStallKey) return;", stall);
        Assert.Contains("Object.Destroy(_feastStall);", stall);
        Assert.Contains("LotCLook.PlaceFeast(", stall);
        // Modèles : préparés par le menu comme ceux du lot C (matière propre), rangés au catalogue.
        Assert.Contains("catalog.Feasts = FeastPieces(notes);", Source("Editor/EdsStyle.cs"));
        Assert.Contains("public GameObject FeastModel(string code)", Source("Game/StyleCatalog.cs"));
        Assert.Contains("catalog.FeastModel(code)", Source("Game/LotCLook.cs"));
    }

    private static (int, int) PngSize(string path)
    {
        byte[] b = File.ReadAllBytes(path);
        int Be(int i) => (b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3];
        return (Be(16), Be(20));
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
