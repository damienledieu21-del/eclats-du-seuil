using System.Text.Json.Nodes;
using EdS.Data.Crafting;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.Simulation.Engine;
using EdS.Simulation.Pilot;
using EdS.World;
using Xunit.Abstractions;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V1.9.6 (PR A1 du chapitre 1 enrichi) : l'ouverture (le réveil, tutoriel : marcher, frapper, se garder, parer, boire), l'histoire
/// en schéma 6 (objectif <c>practice</c>, scènes jouées), le souvenir de la Rupture jouable (une scène dans un intérieur, avec son repli),
/// Croc-Gris, le chef de la meute du pré (combat d'équilibrage : un niveau 4 bien joué gagne, un niveau 2 perd), et les indices du mystère.
/// Les pilotes jouent chaque partie de bout en bout ; les contournements sont dits en commentaire.
/// </summary>
[Trait("Category", "World")]
public class V196OuvertureTests
{
    private readonly ITestOutputHelper _out;

    public V196OuvertureTests(ITestOutputHelper output) => _out = output;

    private const string Opening = "quest.reveil", Mission = "quest.traces_sous_le_moulin", Scene = "scene.souvenir_de_la_rupture";

    private static readonly (double X, double Z) Odile = (-9, 2), Moulin = (-8, 13);

    private static CraftingBook Crafting(ZoneDefinition zone) => CraftingJsonReader.ReadFile(CraftingJsonReader.PathIn(DataDirectory), Texts, zone, ItemsCatalog).Book!;

    private static WorldSession Open((double X, double Z) at, SavedCharacter hero, IEnumerable<string>? consumed = null)
    {
        ZoneDefinition zone = Zone(at.X, at.Z);
        var session = new WorldSession(new WorldSave("zone.vallons", 0, 0, (consumed ?? Array.Empty<string>()).Append(PlayerCharacters.PlacesKey)
            .Distinct(StringComparer.Ordinal), hero), zone, ShippedData, Progression, ItemsCatalog, Book(zone), Crafting(zone), market: new Market(ShippedMerchants));
        session.Step();
        return session;
    }

    private static WorldSession Go(WorldSession from, (double X, double Z) at)
    {
        WorldSave save = SaveJsonReader.Read(SaveJsonReader.Write(from.Save())).Save!;
        return Open(at, save.Character!, save.Consumed);
    }

    private static List<string> Ticks(WorldSession w, int ticks = 3)
    {
        var events = new List<string>();
        for (int i = 0; i < ticks; i++) events.AddRange(w.Step().Story.Select(e => e.ToString()));
        return events;
    }

    private static DialogueView Say(WorldSession w, string npc, string node)
    {
        Assert.Equal(DialogueRefusal.None, w.Talk(npc, out DialogueView? view));
        Assert.Equal(node, view!.Node);
        return view;
    }

    private static IReadOnlyList<string> Choose(WorldSession w, int index, string? next = null)
    {
        Assert.Equal(DialogueRefusal.None, w.Choose(index, out DialogueView? view, out IReadOnlyList<StoryEvent> events));
        Assert.Equal(next, view?.Node);
        return events.Select(e => e.ToString()).ToList();
    }

    /// <summary>Nouveau personnage, comme l'aventure le crée : l'ouverture commencée, l'équipement de départ, et les objets donnés.</summary>
    private static SavedCharacter Fresh(StoryBook book, params ItemStack[] items) =>
        Inventory.Outfit(PlayerCharacters.Create("bastion", "class.bastion", Progression).WithStory(StoryConversions.NewGame(book))
            .WithItems(items, new Dictionary<string, string>(StringComparer.Ordinal)), ItemsCatalog);

    private static double Distance(GroundVector a, double x, double z) => Math.Sqrt(Math.Pow(a.X.Raw / 1e4 - x, 2) + Math.Pow(a.Z.Raw / 1e4 - z, 2));

    private static double Distance(CombatantView a, CombatantView b) =>
        a.Position is GroundVector p && b.Position is GroundVector q ? Distance(p, q.X.Raw / 1e4, q.Z.Raw / 1e4) : double.MaxValue;

    /// <summary>Le personnage marche jusqu'à (x, z) (à 0,6 m près), au plus <paramref name="ticks"/> ticks ; puis s'arrête.</summary>
    private static void Walk(WorldSession w, double x, double z, int ticks = 600)
    {
        CombatEngine e = w.Zone.Engine;
        for (int i = 0; i < ticks; i++)
        {
            GroundVector p = e.View("bastion").Position!.Value;
            double dx = x - p.X.Raw / 1e4, dz = z - p.Z.Raw / 1e4, d = Math.Sqrt(dx * dx + dz * dz);
            long now = e.CurrentTick * e.TickMilliseconds;
            if (d < 0.6)
            {
                e.Submit(new CombatCommand(now, "bastion", CombatAction.Stop));
                w.Step();
                return;
            }
            e.Submit(new CombatCommand(now, "bastion", new GroundVector(Fixed.FromRaw((long)(dx / d * 1e4)), Fixed.FromRaw((long)(dz / d * 1e4)))));
            w.Step();
        }
        Assert.Fail($"({x}, {z}) pas atteint");
    }

    /// <summary>
    /// Combat simulé, comme <see cref="V18EquilibreTests.Fight"/> : le pilote de mesure du moteur garde et pare selon
    /// <paramref name="profile"/> (null : ni l'un ni l'autre), le Bastion marche vers l'ennemi que donne <paramref name="target"/>, lance ses
    /// compétences à tour de rôle et boit une potion sous 40 % de ses PV. S'arrête quand <paramref name="over"/> est vrai, à sa mort, ou au bout
    /// de <paramref name="seconds"/> secondes. Rend les PV restants (0 s'il tombe).
    /// </summary>
    private static long Battle(WorldSession s, Func<string?> target, Func<SessionStep, bool> over, PilotProfile? profile, int seconds = 300)
    {
        CombatEngine e = s.Zone.Engine;
        CombatPilot? pilot = profile is PilotProfile p0 ? new CombatPilot(e, "bastion", p0) : null;
        int perTick = ShippedData.Rules.Combat.MaxCommandsPerActorPerTick;
        for (int i = 0; i < seconds * 20; i++)
        {
            CombatantView me = e.View("bastion");
            if (me.IsDead) break;
            long now = e.CurrentTick * 50;
            var commands = (pilot?.Decide() ?? Array.Empty<CombatCommand>())
                .Where(c => c.Action == CombatAction.Parry || c.Action == CombatAction.GuardStart || c.Action == CombatAction.GuardStop).ToList();
            CombatantView? it = target() is string id ? e.View(id) : null;
            if (it != null && !it.IsDead && it.IsPresent && me.Position is GroundVector a && it.Position is GroundVector b)
            {
                string[] skills = s.Character.ActiveSkills.Append(s.Character.Ultimate).OfType<string>().ToArray();
                if (commands.All(c => c.Action != CombatAction.Parry) && commands.Count < perTick)
                {
                    string skill = skills[i % skills.Length];
                    commands.Add(new CombatCommand(now, "bastion", skill, skill == "skill.bastion.barriere_d_acier" ? "bastion" : it.Id));
                }
                double dx = (b.X.Raw - a.X.Raw) / 1e4, dz = (b.Z.Raw - a.Z.Raw) / 1e4, d = Math.Sqrt(dx * dx + dz * dz);
                if (commands.Count < perTick)
                    commands.Add(d > 2.2
                        ? new CombatCommand(now, "bastion", new GroundVector(Fixed.FromRaw((long)(dx / d * 1e4)), Fixed.FromRaw((long)(dz / d * 1e4))))
                        : new CombatCommand(now, "bastion", CombatAction.Stop));
            }
            int sent = 0;
            foreach (CombatCommand c in commands)
                try { e.Submit(c); sent++; } catch (ArgumentException) { } catch (InvalidOperationException) { }
            if (sent < perTick && me.Health < me.MaxHealth * 4 / 10 && me.ConsumableCharges.TryGetValue("item.potion_de_soin", out int left) && left > 0)
                try { s.Drink("item.potion_de_soin"); } catch (ArgumentException) { } catch (InvalidOperationException) { }
            if (over(s.Step())) break;
        }
        return e.View("bastion").IsDead ? 0 : e.View("bastion").Health ?? 0;
    }

    // --- Formats : histoire en schéma 6 ------------------------------------------------------------------------------

    private static JsonObject StoryJson() => JsonNode.Parse(File.ReadAllText(StoryJsonReader.StoryPathIn(DataDirectory)))!.AsObject();

    private static StoryLoadResult Read(JsonObject root) => StoryJsonReader.ReadStory(root.ToJsonString(), Texts, Zone(), ItemsCatalog, V16TestData.Interiors);

    [Fact]
    public void Histoire_en_schema_6_livree()
    {
        StoryBook book = Book(Zone());
        // Sous-lot V1.9.7 : l'histoire passe au schéma 7 (effet skipQuest) ; ce qui est livré ici ne change pas.
        Assert.Equal((9, 9), (StoryValidator.CurrentSchemaVersion, book.SchemaVersion));   // V2.2 : schéma 8 (weather) ; V2.3 : schéma 9 (emplois du temps)
        Scene scene = Assert.Single(book.Scenes);
        Assert.Equal((Scene, "zone.souvenir_rupture", "fact.ch1.souvenir.a_revivre", "fact.ch1.souvenir.vecu", "text.ch1.discovery.souvenir"),
            (scene.Id, scene.Zone, scene.Start, scene.End, scene.Text));
        Assert.Equal("Galeran", Texts.Get(scene.Actor));
        // L'ouverture : six étapes, dont quatre gestes de combat.
        Quest opening = book.Quest(Opening);
        Assert.Equal(QuestCategory.Main, opening.Category);
        Assert.Equal(new[] { "odile", "frapper", "garder", "parer", "boire", "retour" }, opening.Steps.Select(s => s.Id));
        Assert.Equal(new[] { ("strike", 3L), ("guard", 2L), ("parry", 1L), ("drink", 1L) },
            opening.Steps.Where(s => s.Objective.Kind == ObjectiveKind.Practice).Select(s => (s.Objective.Id, s.Objective.Value)));
        // Ajoutée à la fin : les chemins d'avant restent (sous-lot V1.9.7 : cinq quêtes du village la suivent).
        Assert.Equal(book.Quests.Count - 6, book.Quests.ToList().FindIndex(q => q.Id == Opening));
        // Le texte de chaque étape nomme les touches du PC et les gestes au doigt.
        foreach ((string step, string pc, string touch) in new[] { ("odile", "Z Q S D", "pouce"), ("frapper", "touches 1 à 4", "boutons 1 à 4"),
                     ("garder", "clic droit ou Maj", "Garde"), ("parer", "Espace", "Parade"), ("boire", "(P", "Potion") })
        {
            string text = Texts.Get(opening.Steps.Single(s => s.Id == step).Text);
            Assert.Contains(pc, text);
            Assert.Contains(touch, text);
        }
        MiniJsonSchema schema = MiniJsonSchema.Load(Path.Combine(AppContext.BaseDirectory, "data", "schemas", "story.schema.json"));
        Assert.True(schema.IsValid(System.Text.Json.JsonDocument.Parse(File.ReadAllText(StoryJsonReader.StoryPathIn(DataDirectory))).RootElement));
    }

    [Fact]
    public void Histoire_en_schema_5_encore_lue_sans_practice_ni_scenes()
    {
        JsonObject root = StoryJson();
        V23TestData.WithoutSchedules(root);   // V2.3 : sans emplois du temps ni répliques du moment (schéma 9)
        root["schemaVersion"] = 5;
        StoryLoadResult refused = Read(root);
        Assert.Contains(refused.Report.Errors, e => e.Path == "$.data.scenes" && e.Code == ValidationCode.InvalidValue);
        Assert.Contains(refused.Report.Errors, e => e.Path.EndsWith(".objective.kind", StringComparison.Ordinal) && e.Message.Contains("practice"));
        // Sans l'ouverture ni la scène, un fichier en version 5 reste lu.
        root["data"]!.AsObject().Remove("scenes");
        JsonArray quests = root["data"]!["quests"]!.AsArray();
        quests.Remove(quests.Single(q => q!["id"]!.GetValue<string>() == Opening));
        foreach (JsonNode? dialogue in root["data"]!["dialogues"]!.AsArray())
        {
            JsonArray entries = dialogue!["entries"]!.AsArray();
            for (int i = entries.Count - 1; i >= 0; i--)
                if (entries[i]!.ToJsonString().Contains("quest.reveil") || entries[i]!.ToJsonString().Contains("\"weather\"")) entries.RemoveAt(i);   // V2.2 : sans les répliques du temps (schéma 8)
            // Sous-lot V1.9.7 : « Je sais me battre » (skipQuest, schéma 7) passe l'ouverture ; sans elle, la réponse part aussi.
            foreach (JsonNode? node in dialogue["nodes"]!.AsArray())
            {
                JsonArray choices = node!["choices"]!.AsArray();
                for (int i = choices.Count - 1; i >= 0; i--)
                    if (choices[i]!.ToJsonString().Contains("skipQuest")) choices.RemoveAt(i);
            }
        }
        StoryLoadResult read = Read(root);
        Assert.True(read.IsValid, read.Report.ToString());
        Assert.Equal(5, read.Story!.SchemaVersion);
        Assert.Empty(read.Story.Scenes);
    }

    public static IEnumerable<object[]> Mauvaises_scenes()
    {
        yield return new object[] { "id", "souvenir", ".id", ValidationCode.InvalidValue };
        yield return new object[] { "zone", "zone.inconnue", ".zone", ValidationCode.UnknownReference };
        yield return new object[] { "zone", "zone.auberge", ".end", ValidationCode.UnknownReference };   // aucun chef de l'auberge n'y apprend la fin
        yield return new object[] { "zone", "zone.archives", ".zone", ValidationCode.InvalidValue };   // pas de porte vers les Vallons
        yield return new object[] { "actor", "text.absent", ".actor", ValidationCode.UnknownReference };
        yield return new object[] { "start", "souvenir", ".start", ValidationCode.InvalidValue };
        yield return new object[] { "start", "fact.jamais.appris", ".start", ValidationCode.UnknownReference };
        yield return new object[] { "end", "fact.ch1.souvenir.a_revivre", ".end", ValidationCode.InvalidValue };
        yield return new object[] { "end", "fact.croc_gris.vaincu", ".end", ValidationCode.UnknownReference };
        yield return new object[] { "back", null!, ".back", ValidationCode.MissingField };
    }

    [Theory]
    [MemberData(nameof(Mauvaises_scenes))]
    public void Scene_refusee_au_chemin_precis(string field, string? value, string suffix, ValidationCode code)
    {
        JsonObject root = StoryJson();
        JsonObject scene = root["data"]!["scenes"]![0]!.AsObject();
        if (value is null) scene.Remove(field);
        else scene[field] = value;
        StoryLoadResult r = Read(root);
        Assert.False(r.IsValid);
        Assert.Contains(r.Report.Errors, e => e.Path == "$.data.scenes[0]" + suffix && e.Code == code);
    }

    [Theory]
    [InlineData("jump", 1, ".id", ValidationCode.UnknownReference)]
    [InlineData("strike", 0, ".value", ValidationCode.OutOfRange)]
    [InlineData("strike", 101, ".value", ValidationCode.OutOfRange)]
    public void Objectif_practice_refuse_au_chemin_precis(string action, int value, string suffix, ValidationCode code)
    {
        JsonObject root = StoryJson();
        JsonArray quests = root["data"]!["quests"]!.AsArray();
        int opening = quests.ToList().FindIndex(q => q!["id"]!.GetValue<string>() == Opening);
        JsonObject objective = quests[opening]!["steps"]![1]!["objective"]!.AsObject();
        objective["id"] = action;
        objective["value"] = value;
        StoryLoadResult r = Read(root);
        Assert.Contains(r.Report.Errors, e => e.Path == $"$.data.quests[{opening}].steps[1].objective" + suffix && e.Code == code);
    }

    [Fact]
    public void Un_contrat_ne_demande_pas_de_geste()
    {
        // La guilde garde ses trois objectifs (defeat, defeatFamily, deliver) : practice n'est pas un contrat.
        Assert.Contains("practice", StoryValidator.Objectives.Keys);
        JsonObject guild = JsonNode.Parse(File.ReadAllText(EdS.Data.Json.GuildJsonReader.PathIn(DataDirectory)))!.AsObject();
        Assert.DoesNotContain("\"practice\"", guild.ToJsonString());
    }

    // --- L'ouverture --------------------------------------------------------------------------------------------------

    [Fact]
    public void Ouverture_jouee_de_bout_en_bout()
    {
        StoryBook book = Book(Zone());
        // Nouveau personnage : l'ouverture commencée et suivie, rien d'autre.
        SavedStory start = StoryConversions.NewGame(book);
        Assert.Equal(new[] { Opening }, start.Quests.Keys);
        Assert.Equal(("odile", Opening), (start.Quests[Opening].Step, start.Tracked));
        // Il se réveille sur la place du village ; il marche jusqu'à Odile (contournement : le pilote ouvre la partie près d'elle, la marche
        // elle-même est vérifiée par Walk dans le tutoriel des gestes).
        WorldSession w = Open(Odile, Fresh(book, new ItemStack("item.potion_de_soin", 2)));
        Say(w, "npc.odile", "reveil");
        Choose(w, 0, "reveil_eclat");
        // La porte : une réponse de couleur, qui ne bloque rien (indice : les voix derrière la vieille porte).
        Assert.Contains("FactLearned fact.ch1.reveil.porte", Choose(w, 1, "reveil_porte"));
        Choose(w, 0);
        Assert.Contains($"StepCompleted {Opening}/odile", Ticks(w));
        Say(w, "npc.odile", "reveil_en_cours");
        // Les gestes : frapper, se garder, parer, boire (contournement : annoncés à l'histoire ; le test suivant les fait faire au moteur).
        foreach ((string action, string step) in new[] { ("strike", "frapper"), ("guard", "garder"), ("parry", "parer"), ("drink", "boire") })
        {
            Assert.Equal(step, w.Narrative!.CurrentStep(Opening)!.Id);
            w.Narrative.Practiced("parry", 0);   // rien
            w.Narrative.Practiced(action, 5);   // le compteur s'arrête au nombre demandé
            Assert.Contains($"StepCompleted {Opening}/{step}", Ticks(w, 1));
            w = Go(w, Odile);   // sauvegardée, relue, rouverte
        }
        // Retour chez Odile : la mission commence dans la même conversation.
        long xp = w.Character.Xp;
        Say(w, "npc.odile", "reveil_retour");
        Choose(w, 0, "recit");
        Assert.Contains($"QuestStarted {Mission}/enquete", Choose(w, 0));
        List<string> end = Ticks(w);
        Assert.Contains($"QuestCompleted {Opening}", end);
        Assert.Contains($"StepCompleted {Mission}/enquete", end);
        Assert.Equal(xp + 30, w.Character.Xp);
        Assert.True(w.Narrative!.IsDone(Opening));
        Say(w, "npc.odile", "en_cours");
    }

    [Fact]
    public void Les_gestes_comptent_d_apres_le_moteur()
    {
        // Le tutoriel au moteur : contre les loups derrière le moulin, le pilote expert frappe, se garde et pare ; puis il boit une potion.
        StoryBook book = Book(Zone());
        var story = new SavedStory(new[] { "fact.ch1.reveil.porte" }, new Dictionary<string, QuestProgress> { [Opening] = new QuestProgress("frapper") },
            tracked: Opening);
        SavedCharacter hero = Fresh(book, new ItemStack("item.potion_de_soin", 2)).WithStory(story);
        // Contournement : un seul loup derrière le moulin (les deux autres sont écartés), pour que le test porte sur le compte des gestes et pas
        // sur la difficulté : contre les trois à la fois, le pilote de mesure tombe au niveau 1 avec l'équipement de départ (question à Damien).
        ZoneDefinition zone = Zone(-8, 5, root =>
        {
            foreach (JsonNode? c in root["data"]!["combatants"]!.AsArray())
                if ((string)c!["id"]! is "loup2" or "loup3") c["position"] = new JsonArray(-90 + ((string)c["id"]! == "loup2" ? 0 : 3), -60);
        });
        var w = new WorldSession(new WorldSave("zone.vallons", 0, 0, new[] { PlayerCharacters.PlacesKey },
                hero),
            zone, ShippedData, Progression, ItemsCatalog, Book(zone), Crafting(zone), market: new Market(ShippedMerchants));
        w.Step();
        Walk(w, Moulin.X, Moulin.Z);   // devant le moulin, vers la haie du nord : marcher
        string[] wolves = { "loup1" };
        long left = Battle(w, () => wolves.Select(w.CurrentCreature).Select(w.Zone.Engine.View).Where(v => !v.IsDead && v.IsPresent)
                .OrderBy(v => Distance(w.Zone.Engine.View("bastion"), v)).FirstOrDefault()?.Id,
            _ => w.Narrative!.CurrentStep(Opening)?.Id == "boire", PilotProfile.Expert, 120);
        CombatantCounters counted = w.Zone.Engine.View("bastion").Counters;
        _out.WriteLine($"gestes au moteur : {left} PV, étape {w.Narrative!.CurrentStep(Opening)?.Id} ; coups {counted.Hits}, blocages {counted.Blocks}, "
            + $"parades parfaites {counted.PerfectParries}, ratées {counted.FailedParries}");
        Assert.Equal("boire", w.Narrative!.CurrentStep(Opening)!.Id);
        // Boire (les PV ne sont pas pleins après la morsure des loups).
        for (int i = 0; i < 400 && w.Narrative.CurrentStep(Opening)?.Id == "boire"; i++)
        {
            CombatantView me = w.Zone.Engine.View("bastion");
            if (i % 20 == 0 && me.Health < me.MaxHealth && me.ConsumableCharges.TryGetValue("item.potion_de_soin", out int n) && n > 0)
                try { w.Drink("item.potion_de_soin"); } catch (InvalidOperationException) { }
            w.Step();
        }
        Assert.Equal("retour", w.Narrative.CurrentStep(Opening)!.Id);
    }

    [Fact]
    public void Une_partie_deja_commencee_n_est_pas_renvoyee_au_tutoriel()
    {
        string dir = Path.Combine(Path.GetTempPath(), "eds_v196_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            // Nouvelle partie (pas de sauvegarde) : l'ouverture, deux potions, la première étape dite.
            AdventureSession fresh = AdventureSession.Load(DataDirectory, ScenariosDirectory, Path.Combine(dir, "neuve.json"));
            Assert.Equal("odile", fresh.World.Narrative!.CurrentStep(Opening)!.Id);
            Assert.Equal(Opening, fresh.World.Narrative.Tracked);
            Assert.Equal(AdventureSession.OpeningPotions, fresh.World.Inventory.Count(AdventureSession.OpeningPotion));
            Assert.Contains(fresh.DrainMessages(), m => m.Contains("Tu te réveilles"));
            // Une sauvegarde déjà commencée, même sans aucune quête (le joueur n'a encore parlé à personne) : pas d'ouverture.
            string old = Path.Combine(dir, "ancienne.json");
            SaveStore.Write(old, new WorldSave("zone.vallons", 7200, 1, new[] { PlayerCharacters.PlacesKey },
                Inventory.Outfit(PlayerCharacters.Create("bastion", "class.bastion", Progression), ItemsCatalog)));
            AdventureSession a = AdventureSession.Load(DataDirectory, ScenariosDirectory, old);
            Assert.False(a.World.Narrative!.IsActive(Opening));
            Assert.Empty(a.World.Narrative.ToSaved().Quests);
            Assert.Equal(0, a.World.Inventory.Count(AdventureSession.OpeningPotion));
            // Relue et réécrite, elle ne l'a toujours pas ; Odile lui raconte ses traces, comme avant.
            a.Save();
            WorldSave again = SaveStore.Read(old).Save!;
            Assert.Empty(again.Character!.Story.Quests);
            Say(Open(Odile, again.Character), "npc.odile", "accueil");
            // Une partie au chapitre 1 non plus.
            var story = new SavedStory(new[] { "fact.traces.recit" }, new Dictionary<string, QuestProgress> { [Mission] = new QuestProgress("traces") });
            Say(Open(Odile, Hero(0).WithStory(story)), "npc.odile", "en_cours");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Une_sauvegarde_sans_personnage_n_a_ni_ouverture_ni_potions()
    {
        // Relecture indépendante de la PR #94 : une sauvegarde valide sans personnage (monde déjà joué) reçoit un personnage neuf, sans
        // l'ouverture ni les deux potions : elles vont à une vraie nouvelle partie seulement.
        string dir = Path.Combine(Path.GetTempPath(), "eds_v196_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "sans_personnage.json");
            SaveStore.Write(path, new WorldSave("zone.vallons", 7200, 1, Array.Empty<string>(), null));
            AdventureSession a = AdventureSession.Load(DataDirectory, ScenariosDirectory, path);
            Assert.Empty(a.World.Narrative!.ToSaved().Quests);
            Assert.Null(a.World.Narrative.Tracked);
            Assert.Equal(0, a.World.Inventory.Count(AdventureSession.OpeningPotion));
            Assert.Equal("item.eclat_du_seuil_endormi", a.World.Inventory.Equipped["shard"]);   // l'équipement de départ, lui, est là
            Assert.DoesNotContain(a.DrainMessages(), m => m.Contains("Tu te réveilles"));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void L_etape_boire_ne_se_bloque_jamais()
    {
        // Relecture indépendante de la PR #94 : les deux potions bues avant l'étape « boire » : sans plus aucun objet de soin, elle est
        // remplie ; Odile ne reste pas sur son « en cours ».
        StoryBook book = Book(Zone());
        var story = new SavedStory(null, new Dictionary<string, QuestProgress> { [Opening] = new QuestProgress("boire") }, tracked: Opening);
        WorldSession dry = Open(Odile, Fresh(book).WithStory(story));
        Assert.Equal("retour", dry.Narrative!.CurrentStep(Opening)!.Id);
        Say(dry, "npc.odile", "reveil_retour");
        // Avec une potion encore emportée, il faut la boire (le geste est demandé).
        WorldSession wet = Open(Odile, Fresh(book, new ItemStack("item.potion_de_soin", 1)).WithStory(story));
        Ticks(wet, 5);
        Assert.Equal("boire", wet.Narrative!.CurrentStep(Opening)!.Id);
    }

    [Fact]
    public void L_ouverture_ne_masque_aucune_offre()
    {
        // Le pain du matin, accepté avant d'aller voir Odile : sa livraison passe avant l'ouverture ; puis l'ouverture reprend.
        StoryBook book = Book(Zone());
        var story = new SavedStory(null, new Dictionary<string, QuestProgress> { [Opening] = new QuestProgress("odile"), ["quest.pain_du_matin"] = new QuestProgress("odile") },
            tracked: Opening);
        WorldSession w = Open(Odile, Fresh(book, new ItemStack("item.miche_du_matin", 2)).WithStory(story));
        Say(w, "npc.odile", "ch1_pain");
        Assert.Equal(DialogueRefusal.None, Deliver(w, "npc.odile"));
        List<string> ticks = Ticks(w);
        Assert.Contains("StepCompleted quest.pain_du_matin/odile", ticks);
        Assert.Contains($"StepCompleted {Opening}/odile", ticks);   // lui avoir parlé remplit aussi l'étape du réveil
        Say(w, "npc.odile", "reveil_en_cours");
    }

    // --- Le souvenir de la Rupture : une scène jouée ----------------------------------------------------------------

    [Fact]
    public void Le_souvenir_se_joue_dans_sa_scene_puis_ramene_au_pre()
    {
        string dir = Path.Combine(Path.GetTempPath(), "eds_v196_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "partie.json");
            var story = new SavedStory(new[] { "fact.traces.recit", "fact.moulin.porte_ouverte", "fact.ch1.souvenir.a_revivre" },
                new Dictionary<string, QuestProgress> { [Mission] = new QuestProgress("croc_gris") }, discoveries: new[] { "discovery.souvenir_de_la_rupture" });
            SaveStore.Write(path, new WorldSave("zone.vallons", 0, 0, new[] { PlayerCharacters.PlacesKey }, Hero(0).WithXp(XpFor(3)).WithStory(story)));
            AdventureSession a = Adventure(path);
            a.DrainMessages();
            // Dehors : la scène est à jouer ; sa porte ne se montre pas (ni à portée, ni sur la carte).
            Assert.Equal(Scene, a.PendingScene!.Id);
            Assert.DoesNotContain(a.Doors(), d => d.Id == "souvenir");
            Assert.DoesNotContain(a.Map.Doors, d => d.Id == "souvenir");
            Assert.False(a.InScene);
            Assert.True(a.EnterScene());
            Assert.True(a.InScene);
            Assert.Equal(("zone.souvenir_rupture", "Galeran"), (a.CurrentZone, a.SceneActor));
            Assert.Null(a.PendingScene);   // on la joue
            Assert.Equal("reveil", Assert.Single(a.Doors()).Id);   // la sortie, vers le pré
            // Dans la scène : quelques pas, la reine Ysolde, la porte de lumière, la lune qui se fend.
            WorldSession w = a.World;
            Walk(w, -1, -4.6);
            Say(w, "npc.ysolde", "ordre");
            Choose(w, 0, "reine");
            Choose(w, 0, "chant");
            Assert.Contains("FactLearned fact.ch1.souvenir.porte", Choose(w, 0));
            Say(w, "npc.ysolde", "porte");
            // L'écho de la Rupture : un combat court (le personnage de niveau 3, pilote expert, sans potion) ; sous-lot V1.9.9 : en deux formes,
            // l'écho puis les Voix de la Rupture (la forme en cours du seul combat de chef de la scène).
            string? echo() => w.BossForms.FirstOrDefault();
            bool won = false, voices = false;
            long left = Battle(w, echo, s =>
            {
                voices |= s.Boss.Any(b => b.Kind == BossEventKind.FormArrived && b.Phase == 1 && b.Form == "voix_de_la_rupture");
                return won |= s.Boss.Any(b => b.Kind == BossEventKind.Defeated && b.Boss == "echo_de_la_rupture");
            }, PilotProfile.Expert, 120);
            _out.WriteLine($"écho de la Rupture : {(won ? "vaincu" : "debout")}, {left} PV");
            Assert.True(voices);   // sous-lot V1.9.9 : les Voix de la Rupture sortent de l'écho
            Assert.True(won);
            Ticks(w, 2);
            Assert.True(a.SceneFinished);
            Walk(w, -1, -4.6);   // sous-lot V1.9.9 : la seconde forme se bat près de la porte ; on revient vers la reine
            Say(w, "npc.ysolde", "apres");
            w.Narrative!.Close();
            // Retour au pré, à la porte cachée, avec le texte du retour ; la scène n'est plus à jouer.
            Assert.True(a.LeaveScene());
            Assert.Equal("zone.vallons", a.CurrentZone);
            Assert.Contains(a.DrainMessages(), m => m.Contains("Au fond du pré, un loup hurle"));
            Assert.Null(a.PendingScene);
            Assert.False(a.EnterScene());
            GroundVector at = a.World.Zone.Engine.View("bastion").Position!.Value;
            Assert.True(Distance(at, -18, 23) < 2, $"retour en ({at.X}, {at.Z})");
            Assert.Contains("pre_nord", a.World.Definition!.PlacesAt(at).Select(p => p.Id));
            Assert.Contains("fact.ch1.souvenir.vecu", SaveStore.Read(path).Save!.Character!.Story.Facts);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Le_souvenir_peut_se_passer_et_son_texte_reste_le_repli()
    {
        string dir = Path.Combine(Path.GetTempPath(), "eds_v196_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "partie.json");
            var story = new SavedStory(new[] { "fact.ch1.souvenir.a_revivre" }, new Dictionary<string, QuestProgress> { [Mission] = new QuestProgress("croc_gris") });
            SaveStore.Write(path, new WorldSave("zone.vallons", 0, 0, new[] { PlayerCharacters.PlacesKey }, Hero(0).WithStory(story)));
            AdventureSession a = Adventure(path);
            a.DrainMessages();
            Assert.True(a.SkipScene());
            Assert.Contains(a.DrainMessages(), m => m.Contains("Souvenir de la Rupture. Le pré s'efface"));
            a.Advance(0.1);
            Assert.True(a.World.Narrative!.Knows("fact.ch1.souvenir.vecu"));
            Assert.Null(a.PendingScene);
            Assert.False(a.SkipScene());
            Assert.False(a.EnterScene());
            // Passée pendant qu'on la joue : on en sort aussitôt, sans le texte du retour.
            SaveStore.Write(path, new WorldSave("zone.vallons", 0, 0, new[] { PlayerCharacters.PlacesKey }, Hero(0).WithStory(story)));
            AdventureSession b = Adventure(path);
            Assert.True(b.EnterScene());
            b.DrainMessages();
            Assert.True(b.SkipScene());
            Assert.Equal("zone.vallons", b.CurrentZone);
            Assert.DoesNotContain(b.DrainMessages(), m => m.Contains("Au fond du pré, un loup hurle"));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    /// <summary>Partie à l'étape de la meute, chargée par l'aventure ; la meute vaincue (annoncée à l'histoire), quelques ticks joués.</summary>
    private static AdventureSession AfterPack(string path, params string[] facts)
    {
        var story = new SavedStory(new[] { "fact.traces.recit", "fact.moulin.porte_ouverte" }.Concat(facts),
            new Dictionary<string, QuestProgress> { [Mission] = new QuestProgress("meute") });
        SaveStore.Write(path, new WorldSave("zone.vallons", 0, 0, new[] { PlayerCharacters.PlacesKey }, Hero(0).WithStory(story)));
        AdventureSession a = Adventure(path);
        a.DrainMessages();
        foreach (string loup in new[] { "loup1", "loup2", "loup3" }) a.World.Narrative!.Defeated(loup, "loup");
        a.Advance(0.2);
        Assert.Contains("discovery.souvenir_de_la_rupture", a.World.Narrative!.Discoveries);   // gardée au journal
        return a;
    }

    private const string Fallback = "Souvenir de la Rupture. Le pré s'efface";

    [Fact]
    public void Le_texte_du_souvenir_ne_s_affiche_qu_en_repli_une_seule_fois()
    {
        // Revue Codex de la PR #94 : la meute finie, la scène est proposée sans afficher le texte du souvenir ; il vient une fois, en la passant.
        string dir = Path.Combine(Path.GetTempPath(), "eds_v196_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            AdventureSession a = AfterPack(Path.Combine(dir, "nouvelle.json"));
            Assert.Equal(Scene, a.PendingScene!.Id);
            var said = a.DrainMessages().ToList();
            Assert.DoesNotContain(said, m => m.Contains(Fallback));
            Assert.Contains(said, m => m.Contains("Croc-Gris"));   // l'étape suivante, elle, s'annonce
            Assert.True(a.SkipScene());
            a.Advance(0.2);
            said.AddRange(a.DrainMessages());
            Assert.Single(said, m => m.Contains(Fallback));
            // Jouée et finie : le texte du retour, jamais le repli (Le_souvenir_se_joue_dans_sa_scene_puis_ramene_au_pre).
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Sans_scene_a_jouer_le_souvenir_s_affiche_comme_avant()
    {
        // Une partie qui n'a pas de scène à jouer (ici : déjà vécue) garde l'affichage du texte à la découverte.
        string dir = Path.Combine(Path.GetTempPath(), "eds_v196_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            AdventureSession a = AfterPack(Path.Combine(dir, "vecue.json"), "fact.ch1.souvenir.vecu");
            Assert.Null(a.PendingScene);
            Assert.Single(a.DrainMessages(), m => m.Contains(Fallback));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Passer_la_scene_de_l_interieur_est_atomique()
    {
        // Revue Codex de la PR #94 : passer le souvenir depuis la scène alors que la sauvegarde ne s'écrit pas : rien ne change (toujours dans la
        // scène, fait de fin pas appris, pas de texte de repli) ; un nouvel essai, l'écriture revenue, réussit.
        string dir = Path.Combine(Path.GetTempPath(), "eds_v196_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "partie.json");
            var story = new SavedStory(new[] { "fact.ch1.souvenir.a_revivre" }, new Dictionary<string, QuestProgress> { [Mission] = new QuestProgress("croc_gris") });
            SaveStore.Write(path, new WorldSave("zone.vallons", 0, 0, new[] { PlayerCharacters.PlacesKey }, Hero(0).WithStory(story)));
            AdventureSession a = Adventure(path);
            Assert.True(a.EnterScene());
            a.DrainMessages();
            Directory.CreateDirectory(path + ".tmp");   // l'écriture du fichier temporaire échoue
            Assert.False(a.SkipScene());
            Assert.True(a.InScene);
            Assert.False(a.World.Narrative!.Knows("fact.ch1.souvenir.vecu"));
            a.Advance(0.2);
            Assert.False(a.World.Narrative!.Knows("fact.ch1.souvenir.vecu"));
            List<string> failed = a.DrainMessages().ToList();
            Assert.Contains(failed, m => m.StartsWith("Sauvegarde impossible", StringComparison.Ordinal));
            Assert.DoesNotContain(failed, m => m.Contains(Fallback));
            Directory.Delete(path + ".tmp");
            Assert.True(a.SkipScene());
            Assert.Equal("zone.vallons", a.CurrentZone);
            a.Advance(0.2);
            Assert.True(a.World.Narrative!.Knows("fact.ch1.souvenir.vecu"));
            Assert.Single(a.DrainMessages(), m => m.Contains(Fallback));
            Assert.Null(a.PendingScene);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Une_ancienne_partie_n_a_pas_de_scene_a_jouer()
    {
        // Une partie passée par la meute avant V1.9.6 a le souvenir en découverte, sans le fait de début : rien à jouer, la porte reste fermée.
        var story = new SavedStory(new[] { "fact.traces.recit", "fact.moulin.porte_ouverte" },
            new Dictionary<string, QuestProgress> { [Mission] = new QuestProgress("gaspard") }, discoveries: new[] { "discovery.souvenir_de_la_rupture" });
        WorldSession w = Open(Odile, Hero(0).WithStory(story));
        Assert.Null(w.PendingScene);
        Door door = w.Definition!.Doors.Single(d => d.Id == "souvenir");
        Assert.True(w.SceneDoorClosed(door));
        Assert.False(w.SkipScene(Scene));
        Assert.All(w.Definition.Doors.Where(d => d.Id != "souvenir"), d => Assert.False(w.SceneDoorClosed(d)));
        Say(w, "npc.odile", "ch1_ferrure");   // ses offres, comme avant
    }

    // --- Croc-Gris -----------------------------------------------------------------------------------------------------

    [Fact]
    public void Croc_Gris_place_au_pre()
    {
        ZoneDefinition zone = Zone();
        Boss croc = zone.Chiefs.Single(c => c.Id == "croc_gris");
        Assert.Equal("fact.croc_gris.vaincu", croc.Fact);
        Assert.Equal(new[] { "croc_gris", "croc_gris_enrage" }, croc.Forms.Select(f => f.Id));
        // Sous-lot V1.9.7 (demande de Damien) : un vrai objet, le collier de Croc-Gris (bijou de niveau 4), au lieu du croc.
        Assert.Equal(("item.collier_de_croc_gris", 1), Assert.Single(croc.Rewards));
        // Une attaque annoncée qu'on pare ou qu'on évite (le bond), une phase de plus (le hurlement).
        Assert.Contains(croc.Forms[0].Attacks, x => x.Id == "bond" && x.TelegraphMs > 0);
        Assert.Contains(croc.Forms[1].Attacks, x => x.Id == "hurlement" && x.TelegraphMs > 0);
        foreach (AssailantSetup form in croc.Forms)
        {
            GroundVector p = form.Placement!.Position;
            double x = p.X.Raw / 1e4, z = p.Z.Raw / 1e4;
            Assert.Contains("pre_nord", zone.PlacesAt(p).Select(pl => pl.Id));
            Assert.InRange(x, -24 + 2, 4 - 2);
            Assert.All(zone.Setup.Space!.Obstacles, o => Assert.False(
                o.Points.Min(q => q.X.Raw) / 1e4 - 1 < x && x < o.Points.Max(q => q.X.Raw) / 1e4 + 1
                && o.Points.Min(q => q.Z.Raw) / 1e4 - 1 < z && z < o.Points.Max(q => q.Z.Raw) / 1e4 + 1, o.Id));
            foreach (Road road in zone.Roads)
                for (int i = 1; i < road.Points.Count; i++)
                    Assert.True(Segment(x, z, road.Points[i - 1], road.Points[i]) > 10, $"{form.Id} à moins de 10 m de {road.Id}");
        }
    }

    private static double Segment(double x, double z, GroundVector a, GroundVector b)
    {
        double ax = a.X.Raw / 1e4, az = a.Z.Raw / 1e4, bx = b.X.Raw / 1e4, bz = b.Z.Raw / 1e4;
        double dx = bx - ax, dz = bz - az, l = dx * dx + dz * dz;
        double t = l == 0 ? 0 : Math.Clamp(((x - ax) * dx + (z - az) * dz) / l, 0, 1);
        return Math.Sqrt(Math.Pow(x - ax - t * dx, 2) + Math.Pow(z - az - t * dz, 2));
    }

    /// <summary>Le pré, l'entrée posée à quelques pas de Croc-Gris (les loups du pré restent hors de portée d'aggro).</summary>
    private static WorldSession CrocArena(SavedCharacter hero)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!.AsObject();
        root["data"]!["safePoints"]![0]!["position"] = new JsonArray(-4, 34);
        ZoneLoadResult loaded = ZoneJsonReader.Read(root.ToJsonString(), ScenariosDirectory, ShippedData, ItemsCatalog);
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        return new WorldSession(new WorldSave("zone.vallons", 0, 0, new[] { PlayerCharacters.PlacesKey }, hero), loaded.Zone!, ShippedData, Progression, ItemsCatalog);
    }

    private (long Left, bool Won) FightCroc(int level, int potions, PilotProfile? profile)
    {
        WorldSession w = CrocArena(V18EquilibreTests.Typical(level, potions));
        bool won = false;
        long left = Battle(w, () => w.BossForms.FirstOrDefault(f => f.StartsWith("croc_gris", StringComparison.Ordinal)),
            s => won |= s.Boss.Any(b => b.Kind == BossEventKind.Defeated && b.Boss == "croc_gris"), profile);
        _out.WriteLine($"Croc-Gris, niveau {level}, {potions} potions, {(profile is null ? "sans défense" : profile == PilotProfile.Expert ? "expert" : "débutant")} : "
            + $"{(won ? "gagné" : "perdu")}, {left}/{w.Zone.Engine.View("bastion").MaxHealth} PV");
        return (left, won);
    }

    /// <summary>
    /// Équilibrage (pilote de mesure du moteur, Bastion typique du niveau, comme <see cref="V18EquilibreTests"/>) : au niveau 4, bien joué
    /// (garde et parades, trois potions), le Bastion gagne avec au moins 10 % de ses PV ; au niveau 2, même bien joué, il perd.
    /// </summary>
    [Fact]
    public void Croc_Gris_niveau_4_bien_joue_gagne_niveau_2_perd()
    {
        (long left4, bool won4) = FightCroc(4, 3, PilotProfile.Expert);
        WorldSession probe = CrocArena(V18EquilibreTests.Typical(4, 3));
        Assert.True(won4 && left4 * 10 >= probe.Zone.Engine.View("bastion").MaxHealth, $"niveau 4 expert : {left4} PV");
        (long left2, bool won2) = FightCroc(2, 3, PilotProfile.Expert);
        Assert.Equal((0L, false), (left2, won2));
    }

    [Fact]
    public void Croc_Gris_dans_l_etape_meute_et_sa_recompense()
    {
        // La meute faite : l'étape de Croc-Gris ; sa victoire (le fait du combat de chef, annoncé au pilote) donne expérience, pièces, la
        // silhouette au bord du bois ; Gaspard en parle (la rumeur des manteaux sombres) et donne un conseil avant le combat.
        var story = new SavedStory(new[] { "fact.traces.recit", "fact.moulin.porte_ouverte", "fact.ch1.souvenir.a_revivre" },
            new Dictionary<string, QuestProgress> { [Mission] = new QuestProgress("croc_gris") });
        WorldSession w = Open((26, -13), Hero(0).WithStory(story));
        Say(w, "npc.gaspard", "ch1_croc");
        w.Narrative!.Close();
        long xp = w.Character.Xp, money = w.Money;
        Assert.True(w.Narrative.Learn("fact.croc_gris.vaincu", new List<StoryEvent>()));
        List<string> done = Ticks(w, 1);
        Assert.Equal(new[] { $"StepCompleted {Mission}/croc_gris", "XpGiven xp 120", "MoneyGiven money 80", "Discovered discovery.ch1.silhouette", "FactLearned fact.ch1.silhouette" },
            done);
        Assert.Equal((xp + 120, money + 80), (w.Character.Xp, w.Money));
        Say(w, "npc.gaspard", "ch1_souvenir");
        Choose(w, 2, "ch1_silhouette");
        Choose(w, 0, "ch1_ordre");
    }

    [Fact]
    public void Indices_du_mystere_au_chapitre_1()
    {
        // Les voix derrière la porte (aux traces), la pierre gravée du pré (au pré), la silhouette (après Croc-Gris) : rien n'est nommé.
        Quest mission = Book(Zone()).Quest(Mission);
        Assert.Contains(mission.Steps.Single(s => s.Id == "traces").Effects, e => e.Id == "discovery.ch1.voix_derriere_la_porte");
        Assert.Contains(mission.Steps.Single(s => s.Id == "pre").Effects, e => e.Id == "discovery.ch1.pierre_du_pre");
        Assert.Contains(mission.Steps.Single(s => s.Id == "croc_gris").Effects, e => e.Id == "discovery.ch1.silhouette");
        foreach (string key in new[] { "text.ch1.discovery.voix_derriere_la_porte", "text.ch1.discovery.pierre_du_pre", "text.ch1.discovery.silhouette", "text.ch1.gaspard.silhouette" })
            foreach (string secret in new[] { "Rassembleur", "Celui-qui-Attend", "Sans-Repos", "Envers" })
                Assert.DoesNotContain(secret, Texts.Get(key));
    }

    private static long XpFor(int level) => Progression.XpToLevel[level - 1];
}
