using System;
using System.Linq;
using System.Text.Json.Nodes;
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
/// Sous-lot V1.9.10 (condition « déjà rencontré ») : le premier dialogue avec un PNJ apprend le fait <c>fact.met.&lt;pnj&gt;</c> (sauvegarde
/// v12 inchangée, un fait de plus), inconnu des conditions tant que ce dialogue est ouvert ; le premier accueil (présentation) diffère des
/// suivants (réplique « retrouvailles »), et les répliques du temps et du moment ne remplacent jamais la présentation d'un PNJ jamais rencontré.
/// </summary>
[Trait("Category", "World")]
public sealed class V1910DejaRencontreTests
{
    private const string Vallons = "zone.vallons";

    private static readonly string[] WithReunion =
    {
        "odile", "armurier", "herboriste", "epiciere", "aubergiste", "pretre", "habitant", "maitresse_guilde", "archer_garde", "albe", "renart", "capitaine",
    };

    private static WorldSession Near(string npc, WorldSave? save = null)
    {
        GroundVector at = Book(Zone()).Npc("npc." + npc).Position;
        ZoneDefinition zone = Zone(Math.Round(at.X.Raw / 10000.0, 2), Math.Round(at.Z.Raw / 10000.0 - 1.5, 2));
        return new WorldSession(save ?? new WorldSave(Vallons, 0, 0, new[] { PlayerCharacters.PlacesKey }, Hero(100)), zone, ShippedData, Progression,
            ItemsCatalog, Book(zone), market: new Market(ShippedMerchants));
    }

    [Fact]
    public void Premier_dialogue_presentation_puis_retrouvailles()
    {
        WorldSession w = Near("renart");
        w.Step();
        Assert.False(w.Narrative!.Knows("fact.met.renart"));
        Assert.Equal(DialogueRefusal.None, w.Talk("npc.renart", out DialogueView? first));
        Assert.Equal("ch2_accueil", first!.Node);
        Assert.Equal("text.ch2.renart.accueil", first.Text);
        // Le fait est appris, mais le premier accueil dure tout ce dialogue.
        Assert.True(w.Narrative.Knows("fact.met.renart"));
        Assert.Equal(first.Text, w.Narrative.Open!.Text);
        w.Narrative.Close();
        for (int i = 0; i < 2; i++)
        {
            Assert.Equal(DialogueRefusal.None, w.Talk("npc.renart", out DialogueView? next));
            Assert.Equal("text.v1910.renart.retrouvailles", next!.Text);
            Assert.Equal(first.Options.Count, next.Options.Count);   // mêmes réponses que l'accueil
            w.Narrative.Close();
        }
    }

    [Fact]
    public void Un_pnj_ne_prend_pas_le_fait_d_un_autre()
    {
        WorldSession w = Near("capitaine");
        w.Step();
        Assert.Equal(DialogueRefusal.None, w.Talk("npc.capitaine", out DialogueView? _));
        Assert.True(w.Narrative!.Knows("fact.met.capitaine"));
        Assert.False(w.Narrative.Knows("fact.met.renart") || w.Narrative.Knows("fact.met.archer_garde"));
    }

    [Fact]
    public void Apres_un_rechargement_le_pnj_est_deja_rencontre_et_les_autres_non()
    {
        WorldSession w = Near("renart");
        w.Step();
        Assert.Equal(DialogueRefusal.None, w.Talk("npc.renart", out DialogueView? _));
        w.Narrative!.Close();
        string text = SaveJsonReader.Write(w.Save());
        Assert.Contains("fact.met.renart", text, StringComparison.Ordinal);
        Assert.Equal(12, SaveValidator.CurrentSchemaVersion);   // format de la sauvegarde inchangé
        WorldSave back = SaveJsonReader.Read(text).Save!;

        WorldSession again = Near("renart", back);
        again.Step();
        Assert.True(again.Narrative!.Knows("fact.met.renart"));
        Assert.Equal(DialogueRefusal.None, again.Talk("npc.renart", out DialogueView? view));
        Assert.Equal("text.v1910.renart.retrouvailles", view!.Text);

        // Un PNJ jamais abordé avant l'enregistrement se présente toujours.
        WorldSession other = Near("armurier", back);
        other.Step();
        Assert.False(other.Narrative!.Knows("fact.met.armurier"));
        Assert.Equal(DialogueRefusal.None, other.Talk("npc.armurier", out DialogueView? presentation));
        Assert.Equal("text.armurier.accueil", presentation!.Text);
    }

    [Fact]
    public void Une_sauvegarde_en_plein_premier_dialogue_garde_le_fait()
    {
        // Limite connue : le dialogue ouvert n'est pas sauvegardé ; le fait l'est (la présentation n'est dite qu'une fois).
        WorldSession w = Near("renart");
        w.Step();
        Assert.Equal(DialogueRefusal.None, w.Talk("npc.renart", out DialogueView? _));
        Assert.Contains("fact.met.renart", SaveJsonReader.Write(w.Save()), StringComparison.Ordinal);
    }

    [Fact]
    public void Les_repliques_du_temps_et_du_moment_exigent_d_avoir_deja_rencontre_le_pnj()
    {
        StoryBook book = Book(Zone());
        foreach (Npc npc in book.Npcs)
        {
            string met = StoryValidator.MetFact(npc.Id);
            Dialogue d = book.Dialogue(npc.Dialogue);
            foreach (DialogueEntry e in d.Entries.Where(x => x.Conditions.Any(c => c.Kind == ConditionKind.Weather)))
                Assert.Contains(e.Conditions, c => c.Kind == ConditionKind.Fact && c.Id == met);
            foreach (DialogueNode n in d.Nodes)
                foreach (DialogueLine l in n.Lines)
                    Assert.Contains(l.Conditions, c => c.Kind == ConditionKind.Fact && c.Id == met);
        }
    }

    [Fact]
    public void Retrouvailles_des_pnj_du_bourg_courtes_et_differentes_de_la_presentation()
    {
        StoryBook book = Book(Zone());
        foreach (string k in WithReunion)
        {
            string key = $"text.v1910.{k}.retrouvailles";
            string text = Texts.Get(key);
            Assert.InRange(text.Length, 40, 200);
            Dialogue d = book.Dialogue(book.Npc("npc." + k).Dialogue);
            string last = d.Entries.Last(e => e.Conditions.Count == 0).Node;
            // La retrouvaille est la dernière réplique de l'accueil : le moment et le temps (plus précis) passent avant.
            DialogueLine line = d.Node(last).Lines.Last();
            Assert.Equal(key, line.Text);
            Assert.Equal(new[] { StoryValidator.MetFact("npc." + k) }, line.Conditions.Select(c => c.Id));
            Assert.NotEqual(d.Node(last).Text, key);
        }
    }

    // --- Format : schéma 9 inchangé, fait réservé ----------------------------------------------------------------------------

    private static JsonObject StoryJson() => JsonNode.Parse(System.IO.File.ReadAllText(StoryJsonReader.StoryPathIn(DataDirectory)))!.AsObject();

    private static StoryLoadResult Read(JsonObject root) => StoryJsonReader.ReadStory(root.ToJsonString(), Texts, Zone(), ItemsCatalog, V16TestData.Interiors);

    [Fact]
    public void Le_fait_est_lisible_par_les_conditions_mais_jamais_pose_par_un_effet()
    {
        Assert.Equal(9, StoryValidator.CurrentSchemaVersion);   // pas de nouveau schéma
        Assert.Equal("fact.met.renart", StoryValidator.MetFact("npc.renart"));
        Assert.True(Read(StoryJson()).IsValid);

        JsonObject effect = StoryJson();
        JsonNode choice = effect["data"]!["dialogues"]![0]!["nodes"]![0]!["choices"]![0]!;
        choice["effects"]!.AsArray().Add(new JsonObject { ["do"] = "setFact", ["id"] = "fact.met.renart" });
        StoryLoadResult refused = Read(effect);
        Assert.Contains(refused.Report.Errors, x => x.Code == ValidationCode.InvalidValue && x.Message.Contains("réservé", StringComparison.Ordinal));

        JsonObject unknown = StoryJson();
        unknown["data"]!["dialogues"]![0]!["entries"]![0]!["conditions"]!.AsArray().Add(new JsonObject { ["if"] = "fact", ["id"] = "fact.met.personne" });
        int last = unknown["data"]!["dialogues"]![0]!["entries"]![0]!["conditions"]!.AsArray().Count - 1;
        Assert.Contains(Read(unknown).Report.Errors, x => x.Code == ValidationCode.UnknownReference && x.Path == $"$.data.dialogues[0].entries[0].conditions[{last}].id");
    }

    [Fact]
    public void Aucun_autre_poseur_ne_peut_nommer_le_fait_reserve()
    {
        // Chef de la zone (et chefs des intérieurs) : chemin précis, comme setFact.
        JsonObject zone = JsonNode.Parse(System.IO.File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!.AsObject();
        zone["data"]!["boss"]!["fact"] = "fact.met.renart";
        ZoneLoadResult loaded = ZoneJsonReader.Read(zone.ToJsonString(), ScenariosDirectory, ShippedData, ItemsCatalog);
        Assert.Contains(loaded.Report.Errors, x => x.Code == ValidationCode.InvalidValue && x.Path.EndsWith(".fact", StringComparison.Ordinal)
            && x.Message.Contains("réservé", StringComparison.Ordinal));

        // Fait de fin d'une scène (celui d'un chef) : refusé aussi.
        JsonObject story = StoryJson();
        JsonArray scenes = story["data"]!["scenes"]!.AsArray();
        Assert.NotEmpty(scenes);
        scenes[0]!["end"] = "fact.met.renart";
        Assert.Contains(Read(story).Report.Errors, x => x.Path == "$.data.scenes[0].end" && x.Code == ValidationCode.InvalidValue
            && x.Message.Contains("réservé", StringComparison.Ordinal));
    }

    [Fact]
    public void Histoire_en_schema_8_toujours_lue()
    {
        JsonObject eight = StoryJson();
        V23TestData.WithoutSchedules(eight);
        eight["schemaVersion"] = 8;
        foreach (JsonNode? d in eight["data"]!["dialogues"]!.AsArray())
            foreach (JsonNode? n in d!["nodes"]!.AsArray()) n!.AsObject().Remove("lines");
        StoryLoadResult read = Read(eight);
        Assert.True(read.IsValid, read.Report.ToString());
    }

    // --- Le fait n'est posé que par un vrai dialogue --------------------------------------------------------------------------

    [Fact]
    public void Deux_talk_de_suite_sur_le_meme_pnj_repartent_d_un_dialogue_neuf()
    {
        WorldSession w = Near("renart");
        w.Step();
        Assert.Equal(DialogueRefusal.None, w.Talk("npc.renart", out DialogueView? first));
        // Sans Close : le dialogue déjà ouvert est remplacé ; le PNJ est maintenant rencontré, tout le nouveau dialogue le voit.
        Assert.Equal(DialogueRefusal.None, w.Talk("npc.renart", out DialogueView? second));
        Assert.Equal("text.v1910.renart.retrouvailles", second!.Text);
        Assert.Equal("text.v1910.renart.retrouvailles", w.Narrative!.Open!.Text);
        Assert.Equal(first!.Options.Count, second.Options.Count);
        // Et le premier accueil ne revient pas après un Close.
        w.Narrative.Close();
        Assert.Null(w.Narrative.Open);
        Assert.Equal(DialogueRefusal.None, w.Talk("npc.renart", out DialogueView? third));
        Assert.Equal("text.v1910.renart.retrouvailles", third!.Text);
    }

    [Fact]
    public void Un_dialogue_refuse_ou_vide_ne_pose_pas_le_fait()
    {
        WorldSession w = Near("renart");
        w.Step();
        Assert.Equal(DialogueRefusal.TooFar, w.Talk("npc.armurier", out DialogueView? _));
        Assert.Equal(DialogueRefusal.NothingToSay, w.Talk("npc.personne", out DialogueView? _));
        Assert.DoesNotContain(w.Narrative!.Facts, f => f.StartsWith(StoryValidator.MetFactPrefix, StringComparison.Ordinal));
    }

    [Fact]
    public void Le_tableau_de_la_guilde_ne_pose_pas_le_fait()
    {
        WorldSession w = Near("maitresse_guilde");
        w.Step();
        w.VisitGuild();
        Assert.DoesNotContain(w.Narrative!.Facts, f => f.StartsWith(StoryValidator.MetFactPrefix, StringComparison.Ordinal));
    }

    [Fact]
    public void Un_talk_refuse_laisse_le_premier_accueil_du_dialogue_ouvert()
    {
        WorldSession w = Near("renart");
        w.Step();
        Assert.Equal(DialogueRefusal.None, w.Talk("npc.renart", out DialogueView? first));
        Assert.Equal(DialogueRefusal.TooFar, w.Talk("npc.armurier", out DialogueView? _));
        Assert.Equal(DialogueRefusal.NothingToSay, w.Talk("npc.personne", out DialogueView? _));
        Assert.Equal(first!.Text, w.Narrative!.Open!.Text);   // toujours la présentation
    }

    [Theory]
    [InlineData(195, true)]
    [InlineData(196, false)]
    [InlineData(200, false)]
    public void Id_de_pnj_trop_long_pour_son_fait_rencontre_refuse_au_chemin_precis(int length, bool valid)
    {
        JsonObject root = StoryJson();
        string id = "npc.x" + new string('a', length - 5);
        Assert.Equal(length, id.Length);
        string old = root["data"]!["npcs"]![0]!["id"]!.GetValue<string>();
        string text = root.ToJsonString().Replace("\"" + old + "\"", "\"" + id + "\"");
        StoryLoadResult read = StoryJsonReader.ReadStory(text, Texts, Zone(), ItemsCatalog, V16TestData.Interiors);
        if (valid) Assert.DoesNotContain(read.Report.Errors, x => x.Path == "$.data.npcs[0].id");
        else Assert.Contains(read.Report.Errors, x => x.Path == "$.data.npcs[0].id" && x.Code == ValidationCode.InvalidValue);
    }
}
