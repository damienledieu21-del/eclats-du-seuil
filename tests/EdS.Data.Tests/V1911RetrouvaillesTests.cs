using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using EdS.Data.Guild;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.World;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// V1.9.11 : tous les PNJ qui parlent ont une réplique de retrouvailles, sur le mécanisme de V1.9.10 (fait <c>fact.met.&lt;pnj&gt;</c>,
/// dernière réplique de l'accueil, mêmes réponses que l'accueil). Seuls Firmin, Maëlle et Gaspard (dernier nœud déjà un accueil de retour)
/// et la reine Ysolde (figure d'un souvenir de la Rupture, pas une habitante) n'en ont pas.
/// </summary>
[Trait("Category", "World")]
public sealed class V1911RetrouvaillesTests
{
    private const string Vallons = "zone.vallons";

    private static readonly string[] Without = { "boulanger", "maelle", "gaspard", "ysolde" };

    private static string Short(Npc npc) => npc.Id.Substring("npc.".Length);

    private static WorldSession Near(string npc)
    {
        GroundVector at = Book(Zone()).Npc("npc." + npc).Position;
        ZoneDefinition zone = Zone(Math.Round(at.X.Raw / 10000.0, 2), Math.Round(at.Z.Raw / 10000.0 - 1.5, 2));
        return new WorldSession(new WorldSave(Vallons, 0, 0, new[] { PlayerCharacters.PlacesKey }, Hero(100)), zone, ShippedData, Progression,
            ItemsCatalog, Book(zone), market: new Market(ShippedMerchants));
    }

    private static DialogueLine Reunion(Dialogue d, string node) => d.Node(node).Lines.Last();

    [Fact]
    public void Chaque_pnj_qui_parle_a_une_replique_de_retrouvailles_derniere_de_son_accueil()
    {
        StoryBook book = Book(Zone());
        int with = 0;
        foreach (Npc npc in book.Npcs)
        {
            Dialogue d = book.Dialogue(npc.Dialogue);
            string last = d.Entries.Last(e => e.Conditions.Count == 0).Node;
            string k = Short(npc);
            if (Without.Contains(k))
            {
                Assert.DoesNotContain(d.Nodes.SelectMany(n => n.Lines), l => l.Text.EndsWith(".retrouvailles", StringComparison.Ordinal));
                continue;
            }
            with++;
            DialogueLine line = d.Node(last).Lines.LastOrDefault()!;
            Assert.True(line != null && line.Text.EndsWith(".retrouvailles", StringComparison.Ordinal), $"{npc.Id} : pas de retrouvailles en dernière réplique de {last}");
            Assert.Equal(new[] { StoryValidator.MetFact(npc.Id) }, line.Conditions.Select(c => c.Id));
            string text = Texts.Get(line.Text);
            Assert.InRange(text.Length, 40, 200);
            Assert.NotEqual(d.Node(last).Text, line.Text);
        }
        Assert.Equal(book.Npcs.Count - Without.Length, with);
    }

    [Fact]
    public void L_intendant_de_la_guilde_se_souvient_aussi_une_fois_membre()
    {
        StoryBook book = Book(Zone());
        Dialogue d = book.Dialogue(book.Npc("npc.intendant_guilde").Dialogue);
        Assert.Equal("text.v1911.intendant_guilde.retrouvailles", Reunion(d, "membre").Text);
        Assert.Equal("text.v1911.intendant_guilde.retrouvailles", Reunion(d, "accueil").Text);
    }

    [Theory]
    [InlineData("fermiere")]
    [InlineData("thibaut")]
    [InlineData("deserteur")]
    [InlineData("ysarn")]
    [InlineData("lison")]
    [InlineData("apprentie")]
    [InlineData("doyen")]
    [InlineData("herboriste_hameau")]
    [InlineData("fermier")]
    [InlineData("veuve")]
    public void Premier_dialogue_presentation_puis_retrouvailles_aux_suivants(string k)
    {
        WorldSession w = Near(k);
        w.Step();
        string met = "fact.met." + k;
        Assert.False(w.Narrative!.Knows(met));
        Assert.Equal(DialogueRefusal.None, w.Talk("npc." + k, out DialogueView? first));
        string reunion = $"text.v1911.{k}.retrouvailles";
        Assert.NotEqual(reunion, first!.Text);
        Assert.Equal(first.Text, w.Narrative.Open!.Text);   // tout le premier dialogue reste la présentation
        w.Narrative.Close();
        for (int i = 0; i < 2; i++)
        {
            Assert.Equal(DialogueRefusal.None, w.Talk("npc." + k, out DialogueView? next));
            Assert.Equal(reunion, next!.Text);
            Assert.Equal(first.Options.Count, next.Options.Count);   // mêmes réponses que l'accueil
            w.Narrative.Close();
        }
    }

    [Theory]
    [InlineData("intendant_guilde", "zone.guilde", 0.0, -1.6)]
    [InlineData("archiviste", "zone.archives", -1.4, 0.0)]
    public void Premier_dialogue_puis_retrouvailles_dans_les_maisons_de_la_guilde(string k, string interior, double x, double z)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(StoryJsonReader.StoryPathIn(DataDirectory)))!.AsObject();
        foreach (JsonNode? n in root["data"]!["npcs"]!.AsArray())
            if ((string)n!["id"]! == "npc." + k) V23TestData.Move(n!, new JsonArray(x, z));
        ZoneDefinition vallons = Zone();
        StoryBook story = StoryJsonReader.ReadStory(root.ToJsonString(), Texts, vallons, ItemsCatalog, V16TestData.Interiors).Story!;
        var all = new List<ZoneDefinition> { vallons };
        all.AddRange(V16TestData.Interiors);
        GuildBook guild = GuildJsonReader.ReadFile(GuildJsonReader.PathIn(DataDirectory), Texts, story, vallons, all, ItemsCatalog, ShippedMerchants).Book!;
        var w = new WorldSession(new WorldSave(interior, 0, 0, System.Array.Empty<string>(), Hero(100)), V16TestData.Interiors.Single(i => i.Id == interior),
            ShippedData, Progression, ItemsCatalog, story, door: "sortie", storyZone: Vallons, guild: guild);
        w.Step();
        Assert.Equal(DialogueRefusal.None, w.Talk("npc." + k, out DialogueView? first));
        string reunion = $"text.v1911.{k}.retrouvailles";
        Assert.NotEqual(reunion, first!.Text);
        w.Narrative!.Close();
        Assert.Equal(DialogueRefusal.None, w.Talk("npc." + k, out DialogueView? next));
        Assert.Equal(reunion, next!.Text);
        Assert.Equal(first.Options.Count, next.Options.Count);
    }

    [Fact]
    public void Retrouvailles_de_rosalie_chaleureuses_et_neutres_vis_a_vis_du_chat()
    {
        string text = Texts.Get("text.v1910.habitant.retrouvailles");
        Assert.DoesNotContain("salopé", text, StringComparison.Ordinal);
        Assert.DoesNotContain("chat", text, StringComparison.Ordinal);
    }
}
