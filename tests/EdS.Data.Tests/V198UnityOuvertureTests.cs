using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V1.9.8 (PR B du chapitre 1 enrichi) : la mise en scène Unity (<c>StoryStage</c>) nomme des identifiants des données (étapes
/// de l'ouverture, PNJ, chef, faits, découverte, objet, lieu) ; ce test vérifie qu'ils existent toujours, et que le jeu passe par les
/// commandes du cœur (aucune règle dans Unity). Le rendu lui-même ne se vérifie qu'à l'écran (docs/UNITY.md, « V1.9.8 »).
/// </summary>
[Trait("Category", "World")]
public class V198UnityOuvertureTests
{
    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Lot93UnityPackageTests.Root }.Concat(parts).ToArray()));

    private static string Stage => Read("unity", "Assets", "EdS", "Game", "StoryStage.cs");

    private static string Const(string name)
    {
        Match m = Regex.Match(Stage, "const string " + name + " = \"([^\"]+)\";");
        Assert.True(m.Success, name);
        return m.Groups[1].Value;
    }

    [Fact]
    public void Les_identifiants_de_la_mise_en_scene_existent_dans_les_donnees()
    {
        JsonNode story = JsonNode.Parse(Read("data", "v0", "world", "story.json"))!["data"]!;
        JsonArray quests = story["quests"]!.AsArray();
        JsonNode opening = quests.First(q => (string?)q!["id"] == "quest.reveil")!;
        // Les étapes de l'ouverture, dans l'ordre de la bulle « étape n/6 ».
        string[] steps = opening["steps"]!.AsArray().Select(s => (string)s!["id"]!).ToArray();
        Match order = Regex.Match(Stage, "OpeningSteps = \\{ ([^}]+) \\}");
        Assert.True(order.Success);
        Assert.Equal(steps, order.Groups[1].Value.Split(',').Select(x => x.Trim().Trim('"')).ToArray());
        Assert.Equal(steps[0], Const("WakeStep"));

        Assert.Contains(story["npcs"]!.AsArray(), n => (string?)n!["id"] == Const("YsoldeId"));
        Assert.Contains(story["discoveries"]!.AsArray(), d => (string?)d!["id"] == Const("SilhouetteDiscovery")
            && (string?)d["text"] == Const("SilhouetteText"));
        Assert.Contains("\"" + Const("GateFact") + "\"", Read("data", "v0", "world", "story.json"), StringComparison.Ordinal);
        Assert.Contains("\"" + Const("CollarItem") + "\"", Read("data", "v0", "world", "items.json"), StringComparison.Ordinal);
        string texts = Read("data", "v0", "world", "texts", "fr.json");
        foreach (string key in new[] { Const("ChapterName"), Const("OpeningName"), Const("SilhouetteText"), Const("PromptTitle"), Const("PromptCall"), Const("SkipHint"), "text.ui.quest_skipped" })
            Assert.Contains("\"" + key + "\":", texts, StringComparison.Ordinal);
        Assert.Contains("\"id\": \"" + Const("MeadowPlace") + "\"", Read("data", "v0", "world", "zones", "vallons.json"), StringComparison.Ordinal);
        Assert.Contains("\"id\": \"" + Const("Vallons") + "\"", Read("data", "v0", "world", "zones", "vallons.json"), StringComparison.Ordinal);
        // L'Écho, Croc-Gris enragé et la porte de lumière des scénarios de zone.
        Assert.Contains("\"" + Const("EchoId") + "\"", Read("scenarios", "zones", "souvenir_rupture_echo.json"), StringComparison.Ordinal);
        Assert.Contains("\"" + Const("RagingCroc") + "\"", Read("scenarios", "zones", "vallons_croc_gris.json"), StringComparison.Ordinal);
        Assert.Contains("\"autel_porte_de_lumiere\"", Read("scenarios", "zones", "souvenir_rupture.json"), StringComparison.Ordinal);
        Assert.Contains("StartsWith(\"autel_porte_de_lumiere\"", Stage, StringComparison.Ordinal);
    }

    [Fact]
    public void Le_jeu_passe_par_les_commandes_du_coeur()
    {
        string stage = Stage, game = Read("unity", "Assets", "EdS", "Game", "EdsGame.cs");
        Assert.Contains("a.EnterScene()", stage, StringComparison.Ordinal);
        Assert.Contains("a.SkipScene()", stage, StringComparison.Ordinal);
        Assert.Contains("a.LeaveScene()", stage, StringComparison.Ordinal);
        // Le souvenir change l'ambiance, le pré la rend.
        Assert.Contains("StyleWorld.SetMemory(true);", stage, StringComparison.Ordinal);
        Assert.Contains("StyleWorld.SetMemory(false);", stage, StringComparison.Ordinal);
        // Branché dans la boucle de l'aventure : début, zone, touches, image, messages, dessin, sortie.
        foreach (string call in new[] { "_stage?.Begin(_adventure);", "_stage.ZoneChanged(_adventure)", "_stage?.ReadInput(", "_stage?.AfterSync(", "_stage?.Notice(a, message);",
                     "_stage?.Draw(_adventure,", "_stage?.Reset();", "_stage?.BeforeSync();" })
            Assert.Contains(call, game, StringComparison.Ordinal);
        // La porte de lumière n'est pas un autel, et le souvenir n'a pas de villageois.
        string interior = Read("unity", "Assets", "EdS", "Game", "SpaceView.Interior.cs");
        Assert.Contains("if (StoryStage.IsLightGate(o.Id)) continue;", interior, StringComparison.Ordinal);
        Assert.Contains("if (!space.Obstacles.Any(o => StoryStage.IsLightGate(o.Id))) Villagers(layout.Villagers);", interior, StringComparison.Ordinal);
    }
}
