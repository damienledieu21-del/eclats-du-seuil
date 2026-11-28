using System.Text.Json.Nodes;
using EdS.Data.Corruption;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Space;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// V2.57 : quêtes et dialogues des Vallons. Quatorze quêtes secondaires (égouts, grottes noyées, grotte de la cascade, mines, cryptes,
/// galeries, Envers, corruption), PNJ muets qui parlent (saisons, heures, temps, corruption, progression), fragments de lore ; histoire en
/// schéma 17 (conditions <c>season</c>, <c>corruptionAtLeast</c> et <c>corruptionBelow</c>, objectifs dans les zones reliées, faits des
/// chefs des dessous et de la première fouille d'un secret). Les victoires sur les chefs sont annoncées par leur fait (comme le fait le
/// monde à la récompense), sans livrer le combat.
/// </summary>
[Trait("Category", "World")]
public class V257QuetesTests
{
    private const string Vallons = "zone.vallons", Envers = "zone.envers_vallons";

    private static readonly Lazy<StoryBook> Story = new(() => Book(Zone()));

    private static readonly long[] XpTable = { 0, 80, 180, 300, 440, 740, 1080, 1460, 1880, 2350, 2880, 3470, 4130, 4870, 5700, 6630, 7670, 8840, 10150,
        11610, 13250, 15080, 17130, 19430, 22010 };

    private static readonly Lazy<CorruptionBook> Corruption = new(() =>
        CorruptionJsonReader.Read(File.ReadAllText(CorruptionJsonReader.PathIn(DataDirectory)), Zone(), ItemsCatalog, ScenariosDirectory, ShippedData).Book!);

    private static SavedCharacter HeroAt(int level, IEnumerable<string>? facts = null, IEnumerable<string>? done = null) =>
        Hero(0).WithXp(XpTable[level - 1]).WithStory(new SavedStory(facts ?? Array.Empty<string>(),
            (done ?? Array.Empty<string>()).ToDictionary(q => q, _ => new QuestProgress(QuestProgress.Done), StringComparer.Ordinal)));

    private static long NoonOf(long day) => WorldEventSchedule.TickAt(new WorldClock(50), day * 1440 + 12 * 60);

    /// <summary>Une partie qui passe d'une zone à l'autre par la sauvegarde (comme aux portes du jeu), toujours à la même heure du monde.</summary>
    private sealed class Partie
    {
        public SavedCharacter Hero;
        private IReadOnlyList<string> _consumed = new[] { PlayerCharacters.PlacesKey };
        private readonly long _tick;
        public WorldSession W = null!;
        public bool Corrupt;

        public Partie(SavedCharacter hero, long tick = 0)
        {
            Hero = hero;
            _tick = tick;
        }

        private void Keep()
        {
            if (W is null) return;
            WorldSave s = SaveJsonReader.Read(SaveJsonReader.Write(W.Save())).Save!;
            Hero = s.Character!;
            _consumed = s.Consumed;
        }

        /// <summary>Le personnage gagne des niveaux entre deux zones.</summary>
        public void Level(int level)
        {
            Keep();
            Hero = Hero.WithXp(XpTable[level - 1]);
            W = null!;
        }

        public WorldSession Open(string zoneId, (double X, double Z)? at = null)
        {
            Keep();
            ZoneDefinition zone = zoneId == Vallons ? (at is { } v ? Zone(v.X, v.Z) : Zone()) : ZoneAt(zoneId, at);
            W = new WorldSession(new WorldSave(zone.Id, _tick, 0, _consumed, Hero), zone, ShippedData, Progression, ItemsCatalog, Story.Value,
                storyZone: Vallons);
            if (Corrupt) W.UseCorruption(Corruption.Value);
            Ticks();
            return W;
        }

        public List<string> Ticks(int n = 3)
        {
            var events = new List<string>();
            for (int i = 0; i < n; i++) events.AddRange(W.Step().Story.Select(e => e.ToString()));
            return events;
        }

        /// <summary>
        /// Se rend dans un lieu : le point sûr de la zone y est posé (là où il est déjà s'il y est, sinon au premier point praticable d'une
        /// grille du lieu, du milieu vers les bords).
        /// </summary>
        public void Reach(string zoneId, string place)
        {
            ZoneDefinition plain = zoneId == Vallons ? Zone() : ZoneAt(zoneId, null);
            Place p = plain.Places.Single(x => x.Id == place);
            if (p.Contains(plain.SafePoints[0].Position)) Open(zoneId);
            else
            {
                double x0 = p.Min.X.ToDouble(), x1 = p.Max.X.ToDouble(), z0 = p.Min.Z.ToDouble(), z1 = p.Max.Z.ToDouble();
                double cx = (x0 + x1) / 2, cz = (z0 + z1) / 2;
                var spots = from i in Enumerable.Range(1, 9) from j in Enumerable.Range(1, 9)
                            let x = x0 + (x1 - x0) * i / 10 let z = z0 + (z1 - z0) * j / 10
                            orderby (x - cx) * (x - cx) + (z - cz) * (z - cz), x, z select (x, z);
                (double X, double Z) at = spots.First(s => zoneId == Vallons ? TryVallons(s) : TryZoneAt(zoneId, s) != null);
                Open(zoneId, at);
            }
            Assert.True(((INarrativeWorld)W).InPlace(place), $"pas dans « {place} »");
            Ticks();
        }

        private void Near(string npc)
        {
            if (W is null) Open(Vallons);
            EdS.Data.Story.Npc n = Story.Value.Npc(npc);
            string zoneId = W!.NpcZone(n) ?? Vallons;
            GroundVector pos = W.NpcPosition(n);
            double x = pos.X.ToDouble(), z = pos.Z.ToDouble();
            // À un mètre du PNJ, du côté praticable (un PNJ d'intérieur peut se tenir contre un mur).
            (double X, double Z) at = new[] { (x + 1, z), (x - 1, z), (x, z + 1), (x, z - 1), (x, z) }
                .First(s => zoneId == Vallons ? TryVallons(s) : TryZoneAt(zoneId, s) != null);
            Open(zoneId, at);
        }

        /// <summary>Parle au PNJ (rejoint sa zone et sa place), vérifie le nœud, puis prend les réponses données par leur clé.</summary>
        public List<string> Talk(string npc, string node, params string[] answers)
        {
            Near(npc);
            Assert.Equal(DialogueRefusal.None, W.Talk(npc, out DialogueView? view));
            Assert.Equal(node, view!.Node);
            var events = new List<string>();
            foreach (string answer in answers)
            {
                DialogueOption option = view!.Options.FirstOrDefault(o => o.Text == answer)
                    ?? throw new Xunit.Sdk.XunitException($"réponse « {answer} » absente de {view.Node} : {string.Join(", ", view.Options.Select(o => o.Text))}");
                Assert.Equal(DialogueRefusal.None, W.Choose(option.Index, out view, out IReadOnlyList<StoryEvent> ev));
                events.AddRange(ev.Select(e => e.ToString()));
            }
            if (view != null) W.Narrative!.Close();
            events.AddRange(Ticks());
            return events;
        }

        public DialogueView View(string npc)
        {
            Near(npc);
            Assert.Equal(DialogueRefusal.None, W.Talk(npc, out DialogueView? view));
            W.Narrative!.Close();
            return view!;
        }

        public string NodeOf(string npc) => View(npc).Node;

        /// <summary>Le monde annonce une victoire sur un chef, ou une stèle lue : son fait, comme <c>WorldSession</c>.</summary>
        public List<string> Win(string fact)
        {
            Assert.True(W.Narrative!.Learn(fact, new List<StoryEvent>()));
            return Ticks();
        }

        public List<string> Kill(string population, string family, int count)
        {
            for (int i = 0; i < count; i++) W.Narrative!.Defeated(population, family);
            return Ticks();
        }

        public bool Done(string quest) => W.Narrative!.IsDone(quest);
        public string? Step(string quest) => W.Narrative!.CurrentStep(quest)?.Id;
    }

    private static ZoneDefinition? TryZoneAt(string zoneId, (double X, double Z) at)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, zoneId)))!.AsObject();
        root["data"]!["safePoints"]![0]!["position"] = new JsonArray(at.X, at.Z);
        ZoneLoadResult loaded = ZoneJsonReader.Read(root.ToJsonString(), ScenariosDirectory, ShippedData, ItemsCatalog);
        return loaded.IsValid ? loaded.Zone : null;
    }

    private static bool TryVallons((double X, double Z) at)
    {
        try { Zone(at.X, at.Z); return true; }
        catch (Xunit.Sdk.XunitException) { return false; }
    }

    private static ZoneDefinition ZoneAt(string zoneId, (double X, double Z)? at)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, zoneId)))!.AsObject();
        if (at is { } p) root["data"]!["safePoints"]![0]!["position"] = new JsonArray(p.X, p.Z);
        ZoneLoadResult loaded = ZoneJsonReader.Read(root.ToJsonString(), ScenariosDirectory, ShippedData, ItemsCatalog);
        Assert.True(loaded.IsValid, loaded.Report.ToString());
        return loaded.Zone!;
    }

    private static bool Says(DialogueView view, string text) => view.Text == text || view.Text == Texts.Get(text);

    // ---------------------------------------------------------------------------------------------------------------------------------
    // Données et format
    // ---------------------------------------------------------------------------------------------------------------------------------

    public static readonly string[] Quetes =
    {
        "quest.v257.sous_les_paves", "quest.v257.ce_qui_remonte", "quest.v257.ce_que_le_lac_garde", "quest.v257.lueur_derriere_l_eau",
        "quest.v257.filon_des_disparus", "quest.v257.le_sacristain", "quest.v257.moines_du_dessous", "quest.v257.ce_que_gardent_les_eclats",
        "quest.v257.pierres_a_l_envers", "quest.v257.voix_du_moulin", "quest.v257.ombre_de_brunot", "quest.v257.reflet_de_machefer",
        "quest.v257.abbe_renverse", "quest.v257.eau_grise",
    };

    [Fact]
    public void Histoire_en_schema_17_avec_les_quetes_ajoutees_a_la_fin()
    {
        StoryBook book = Story.Value;
        Assert.Equal(17, book.SchemaVersion);
        Assert.Equal(17, StoryValidator.CurrentSchemaVersion);
        Assert.Equal("quest.le_cheval_de_la_ferme", book.Quests[46].Id);   // les quêtes d'avant gardent leur rang
        Assert.Equal(Quetes, book.Quests.Skip(47).Select(q => q.Id));
        Assert.All(Quetes, q => Assert.Equal(QuestCategory.Side, book.Quest(q).Category));
        // Chaque quête (sauf celle de la corruption, aux terres corrompues) mène à un donjon ou à l'Envers : un lieu ou un chef d'une zone reliée.
        ZoneDefinition vallons = Zone();
        IReadOnlyList<ZoneDefinition> linked = V16TestData.Interiors;
        foreach (string q in Quetes.Where(q => q != "quest.v257.eau_grise"))
            Assert.Contains(book.Quest(q).Steps, s =>
                (s.Objective.Kind == ObjectiveKind.Reach && vallons.Places.All(p => p.Id != s.Objective.Id))
                || (s.Objective.Kind == ObjectiveKind.Fact && linked.Any(z => z.Bosses.Any(b => b.Fact == s.Objective.Id)
                    || z.Secrets.Any(x => StoryValidator.SecretFact(x.Id) == s.Objective.Id))));
        Assert.Equal(26, book.Discoveries.Count(d => d.Id.StartsWith("discovery.v257.", StringComparison.Ordinal)));
        Assert.All(book.Discoveries, d => Assert.True(Texts.Has(d.Text), d.Text));
        Assert.Equal("Frère Aubin, l'hospitalier", Texts.Get(book.Npc("npc.frere_hospitalier").Name));   // plus deux Frère Anselme
        // Les PNJ qui étaient muets ont des sujets de conversation.
        foreach (string d in new[] { "dialogue.charron", "dialogue.passeuse", "dialogue.foulonniere", "dialogue.guetteuse", "dialogue.hospitalier" })
        {
            DialogueNode accueil = book.Dialogue(d).Node("accueil");
            Assert.True(accueil.Choices.Count >= 2, d);
            Assert.True(accueil.Lines.Count(l => l.Conditions.Any(c => c.Kind == ConditionKind.Season)) == 4, d);
        }
    }

    [Fact]
    public void Faits_des_chefs_des_dessous_et_des_secrets_attendus()
    {
        StoryBook book = Story.Value;
        foreach (ZoneDefinition z in V16TestData.Interiors)
        {
            foreach (Boss b in z.Bosses) Assert.True(book.CanLearn(b.Fact), b.Fact);
            foreach (ZoneSecret s in z.Secrets) Assert.True(book.CanLearn(StoryValidator.SecretFact(s.Id)), s.Id);
        }
    }

    private static StoryLoadResult ReadWith(Action<JsonObject> edit, int? schema = null)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(StoryJsonReader.StoryPathIn(DataDirectory)))!.AsObject();
        if (schema is int v) root["schemaVersion"] = v;
        edit(root["data"]!.AsObject());
        return StoryJsonReader.ReadStory(root.ToJsonString(), Texts, Zone(), ItemsCatalog, V16TestData.Interiors);
    }

    private static void AddArchivistLine(JsonObject data, string condition)
    {
        JsonObject node = data["dialogues"]!.AsArray().Single(x => (string)x!["id"]! == "dialogue.archiviste")!["nodes"]!.AsArray()
            .Single(n => (string)n!["id"]! == "apres")!.AsObject();
        node["lines"]!.AsArray().Insert(0, new JsonObject { ["conditions"] = new JsonArray(JsonNode.Parse(condition)), ["text"] = "text.archiviste.apres" });
    }

    [Theory]
    [InlineData("{\"if\":\"season\",\"id\":\"winter\"}", null)]
    [InlineData("{\"if\":\"season\",\"id\":\"hiver\"}", ValidationCode.UnknownReference)]
    [InlineData("{\"if\":\"season\",\"id\":\"winter\",\"value\":1}", ValidationCode.InvalidValue)]
    [InlineData("{\"if\":\"corruptionAtLeast\",\"value\":3}", null)]
    [InlineData("{\"if\":\"corruptionBelow\",\"value\":0}", null)]
    [InlineData("{\"if\":\"corruptionAtLeast\",\"value\":65}", ValidationCode.OutOfRange)]
    [InlineData("{\"if\":\"corruptionAtLeast\",\"id\":\"landes_grises\",\"value\":1}", ValidationCode.InvalidValue)]
    public void Conditions_de_saison_et_de_corruption_validees(string condition, ValidationCode? code)
    {
        StoryLoadResult r = ReadWith(d => AddArchivistLine(d, condition));
        if (code is null) Assert.True(r.Story != null, r.Report.ToString());
        else
        {
            Assert.Null(r.Story);
            Assert.Contains(r.Report.Errors, i => i.Code == code && i.Path.Contains("dialogues", StringComparison.Ordinal));
        }
    }

    private static void Strip(JsonObject d) => V257TestData.WithoutQuetesIn(d);

    [Fact]
    public void Version_16_refuse_les_conditions_et_les_lieux_du_schema_17()
    {
        StoryLoadResult v16 = ReadWith(Strip, 16);
        Assert.True(v16.Story != null, v16.Report.ToString());
        StoryLoadResult season = ReadWith(d => { Strip(d); AddArchivistLine(d, "{\"if\":\"season\",\"id\":\"winter\"}"); }, 16);
        Assert.Null(season.Story);
        Assert.Contains(season.Report.Errors, i => i.Message.Contains("version 17", StringComparison.Ordinal));
        // Un lieu d'un dessous n'est visé qu'en version 17 ; en 17, oui.
        void Reach(JsonObject d) => d["quests"]!.AsArray()[1]!["steps"]!.AsArray()[0]!["objective"] =
            JsonNode.Parse("{\"kind\":\"defeatFamily\",\"id\":\"rat\",\"value\":2}");
        StoryLoadResult reach = ReadWith(d => { Strip(d); Reach(d); }, 16);
        Assert.Null(reach.Story);
        Assert.Contains(reach.Report.Errors, i => i.Code == ValidationCode.UnknownReference && i.Message.Contains("rat", StringComparison.Ordinal));
        StoryLoadResult reach17 = ReadWith(Reach);
        Assert.True(reach17.Story != null, reach17.Report.ToString());
    }

    // ---------------------------------------------------------------------------------------------------------------------------------
    // Les quêtes, de bout en bout (sauvegarde et rechargement à chaque changement de zone)
    // ---------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Fin commune : quête finie, récompense donnée une seule fois ; rien n'est redonné après rechargement, ni par un fait réappris ;
    /// le donneur ne la repropose pas.</summary>
    private static void FiniUneFois(Partie p, string quest, string giver, long xpBefore, long xpGain, string? lastFact = null)
    {
        Assert.True(p.Done(quest), quest);
        Assert.Equal(xpBefore + xpGain, p.W.Character.Xp);
        long money = p.W.Money;
        p.Open(Vallons);
        Assert.True(p.Done(quest));
        Assert.Equal(xpBefore + xpGain, p.W.Character.Xp);
        Assert.Equal(money, p.W.Money);
        if (lastFact != null)
        {
            Assert.False(p.W.Narrative!.Learn(lastFact, new List<StoryEvent>()));
            p.Ticks();
            Assert.Equal(xpBefore + xpGain, p.W.Character.Xp);
        }
        Assert.DoesNotContain("_offre", p.NodeOf(giver), StringComparison.Ordinal);
        Assert.False(p.W.Narrative!.IsOpen(quest));
    }

    private static long Xp(Partie p) => p.W.Character.Xp;

    [Fact]
    public void Sous_les_paves_choix_livrer_au_bailli()
    {
        const string Q = "quest.v257.sous_les_paves";
        var low = new Partie(HeroAt(6, new[] { "fact.met.capitaine" }, new[] { "quest.premier_contrat" }));
        Assert.DoesNotContain("v257", low.NodeOf("npc.capitaine"), StringComparison.Ordinal);   // niveau 6 : pas encore
        Assert.False(low.W.Narrative!.IsOpen(Q));
        var p = new Partie(HeroAt(7, new[] { "fact.met.capitaine" }, new[] { "quest.premier_contrat" }));
        Assert.Contains($"QuestStarted {Q}/collecteur", p.Talk("npc.capitaine", "v257_egouts_offre", "text.v257.capitaine.egouts.accepter"));
        Assert.Equal("v257_egouts_attente", p.NodeOf("npc.capitaine"));
        long xp = Xp(p);
        p.Reach("zone.egouts_collecteur", "galerie_du_collecteur");
        Assert.Equal("passeur", p.Step(Q));
        p.Win("fact.passeur_des_contrebandiers.vaincu");
        Assert.Contains("discovery.v257.registre_du_passeur", p.W.Narrative!.Discoveries);
        Assert.Equal("choix", p.Step(Q));
        long money = p.W.Money;
        int guard = p.W.Narrative.Reputation("guard");
        p.Talk("npc.capitaine", "v257_egouts_choix", "text.v257.capitaine.egouts_choix.garde");
        Assert.Contains("fact.v257.egouts.livre", p.W.Narrative!.Facts);
        Assert.DoesNotContain("fact.v257.egouts.brule", p.W.Narrative.Facts);
        Assert.True(p.W.Narrative.Reputation("guard") > guard);
        Assert.Equal(money + 150 + 120, p.W.Money);
        FiniUneFois(p, Q, "npc.capitaine", xp, 300, "fact.passeur_des_contrebandiers.vaincu");
    }

    [Fact]
    public void Sous_les_paves_choix_bruler_le_registre()
    {
        const string Q = "quest.v257.sous_les_paves";
        var p = new Partie(HeroAt(7, new[] { "fact.met.capitaine", "fact.passeur_des_contrebandiers.vaincu" }, new[] { "quest.premier_contrat" }));
        p.Talk("npc.capitaine", "v257_egouts_offre", "text.v257.capitaine.egouts.accepter");
        long xp = Xp(p);
        // Le passeur déjà vaincu : l'étape se remplit d'elle-même une fois le collecteur atteint.
        p.Reach("zone.egouts_collecteur", "galerie_du_collecteur");
        Assert.Equal("choix", p.Step(Q));
        p.Talk("npc.capitaine", "v257_egouts_choix", "text.v257.capitaine.egouts_choix.bruler");
        Assert.Contains("fact.v257.egouts.brule", p.W.Narrative!.Facts);
        Assert.Equal(1, p.W.Narrative.Trust("npc.capitaine"));
        FiniUneFois(p, Q, "npc.capitaine", xp, 300);
    }

    [Fact]
    public void Ce_qui_remonte_de_la_citerne()
    {
        const string Q = "quest.v257.ce_qui_remonte";
        var p = new Partie(HeroAt(8, new[] { "fact.met.aubergiste" }, new[] { "quest.peaux_pour_l_auberge", "quest.dette_a_l_auberge" }));
        p.Talk("npc.aubergiste", "v257_citerne_offre", "text.v257.aubergiste.citerne.accepter");
        long xp = Xp(p);
        p.Open("zone.egouts_collecteur");
        p.Kill("rat_egout1", "rat", 5);
        Assert.Equal("rats", p.Step(Q));
        p.Kill("rat_egout2", "rat", 1);
        Assert.Equal("bete", p.Step(Q));
        p.Open("zone.egouts_citerne");
        p.Win("fact.bete_de_la_citerne.vaincue");
        Assert.Equal("retour", p.Step(Q));
        int ragout = p.W.Inventory.Count("item.ragout_de_l_auberge");
        p.Talk("npc.aubergiste", "v257_citerne_retour", "text.v257.aubergiste.citerne_retour.merci");
        Assert.Equal(ragout + 3, p.W.Inventory.Count("item.ragout_de_l_auberge"));
        FiniUneFois(p, Q, "npc.aubergiste", xp, 320, "fact.bete_de_la_citerne.vaincue");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Ce_que_le_lac_garde_verite_ou_silence(bool truth)
    {
        const string Q = "quest.v257.ce_que_le_lac_garde";
        var p = new Partie(HeroAt(12, new[] { "fact.met.veuve", "fact.ch3.lettre.rendue", "fact.ch3.lettre.decidee" }, new[] { "quest.lettre_au_fond_du_lac" }));
        p.Talk("npc.veuve", "v257_lac_offre", "text.v257.veuve.lac.accepter");
        long xp = Xp(p);
        p.Reach("zone.grottes_noyees_bassins", "bassins_des_roseaux");
        p.Win("fact.capitaine_des_pilleurs.vaincu");
        Assert.Equal("cale", p.Step(Q));
        p.Reach("zone.grottes_noyees_epaves", "cale_de_l_epave");
        Assert.Contains("discovery.v257.sacoche_de_guerin", p.W.Narrative!.Discoveries);
        p.Talk("npc.veuve", "v257_lac_choix", truth ? "text.v257.veuve.lac_choix.verite" : "text.v257.veuve.lac_choix.silence");
        Assert.True(p.W.Narrative!.IsChoosingReward(Q));
        // Récompense au choix : une réponse qui n'existe pas est refusée sans rien changer ; puis donnée une fois.
        string before = SaveJsonReader.Write(p.W.Save());
        Assert.NotEqual(DialogueRefusal.None, p.W.ChooseReward(Q, 5, out _));
        Assert.Equal(before, SaveJsonReader.Write(p.W.Save()));
        Assert.Equal(DialogueRefusal.None, p.W.ChooseReward(Q, 0, out _));
        p.Ticks();
        Assert.Equal(1, p.W.Inventory.Count("item.pendentif_du_lac"));
        Assert.NotEqual(DialogueRefusal.None, p.W.ChooseReward(Q, 1, out _));
        Assert.Equal(0, p.W.Inventory.Count("item.rondache_du_lac"));
        FiniUneFois(p, Q, "npc.veuve", xp, 480);
        Assert.Contains(truth ? "fact.v257.lac.verite" : "fact.v257.lac.silence", p.W.Narrative!.Facts);
        Assert.True(Says(p.View("npc.veuve"), truth ? "text.v257.veuve.apres_verite" : "text.v257.veuve.apres_silence"));
    }

    [Fact]
    public void La_lueur_derriere_l_eau()
    {
        const string Q = "quest.v257.lueur_derriere_l_eau";
        var p = new Partie(HeroAt(10, new[] { "fact.met.guetteuse" }));
        p.Talk("npc.guetteuse", "accueil", "text.v257.guetteuse.lueur.demande", "text.v257.guetteuse.lueur.oui");
        Assert.True(p.W.Narrative!.IsActive(Q));
        long xp = Xp(p);
        p.Reach("zone.grotte_cascade", "salle_du_rideau");
        p.Win("fact.gardien_de_la_grotte.vaincu");
        p.Talk("npc.guetteuse", "v257_lueur_retour", "text.v257.guetteuse.lueur_retour.merci");
        FiniUneFois(p, Q, "npc.guetteuse", xp, 340, "fact.gardien_de_la_grotte.vaincu");
        DialogueView after = p.View("npc.guetteuse");
        Assert.DoesNotContain(after.Options, o => o.Text == "text.v257.guetteuse.lueur.demande");
    }

    [Fact]
    public void Le_filon_des_disparus_recompense_refusee_sac_plein_sans_rien_changer()
    {
        const string Q = "quest.v257.filon_des_disparus";
        var p = new Partie(HeroAt(20, new[] { "fact.met.charron_hameau" }));
        p.Talk("npc.charron_hameau", "accueil", "text.v257.charron.sujet.mines", "text.v257.charron.mines.accepter");
        Assert.True(p.W.Narrative!.IsActive(Q));
        long xp = Xp(p);
        p.Reach("zone.mine_galeries", "galerie_d_entree");
        p.Win("fact.chef_des_pilleurs.vaincu");
        p.Reach("zone.mine_filons", "galerie_des_filons");
        Assert.Contains("discovery.v257.lampe_de_mathieu", p.W.Narrative!.Discoveries);
        p.Win("fact.bete_des_eboulis.vaincue");
        p.Reach("zone.mine_cristaux", "coeur_de_cristal");
        Assert.Equal("retour", p.Step(Q));
        p.Talk("npc.charron_hameau", "v257_mines_retour", "text.v257.charron.mines_retour.merci");
        Assert.True(p.W.Narrative!.IsChoosingReward(Q));
        // Sac plein : la récompense au choix est refusée, rien ne change (refus atomique).
        ItemDefinition filler = ItemsCatalog.Items.First(i => i.Stack == 1 && i.Slot is null);
        for (int i = 0; i < 500 && p.W.Inventory.Add(filler.Id, 1) == InventoryRefusal.None; i++) { }
        string full = SaveJsonReader.Write(p.W.Save());
        Assert.Equal(DialogueRefusal.NoRoom, p.W.ChooseReward(Q, 1, out _));
        Assert.Equal(full, SaveJsonReader.Write(p.W.Save()));
        Assert.True(p.W.Narrative.IsChoosingReward(Q));
        while (p.W.Inventory.Count(filler.Id) > 0) p.W.Inventory.Remove(filler.Id, 1);
        Assert.Equal(DialogueRefusal.None, p.W.ChooseReward(Q, 1, out _));
        p.Ticks();
        Assert.Equal(3, p.W.Inventory.Count("item.elixir_de_garde"));
        FiniUneFois(p, Q, "npc.charron_hameau", xp, 1700);
    }

    [Fact]
    public void Le_sacristain()
    {
        const string Q = "quest.v257.le_sacristain";
        var p = new Partie(HeroAt(6, new[] { "fact.met.pretre" }, new[] { "quest.bete_blanche", "quest.cloches_de_la_chapelle" }));
        p.Talk("npc.pretre", "v257_sacristain_offre", "text.v257.pretre.sacristain.accepter");
        long xp = Xp(p);
        p.Reach("zone.crypte_chapelle", "crypte_de_la_chapelle");
        p.Win("fact.sacristain.apaise");
        Assert.Contains("discovery.v257.priere_du_sacristain", p.W.Narrative!.Discoveries);
        int potions = p.W.Inventory.Count("item.potion_de_soin");
        p.Talk("npc.pretre", "v257_sacristain_retour", "text.v257.pretre.sacristain_retour.merci");
        Assert.Equal(potions + 3, p.W.Inventory.Count("item.potion_de_soin"));
        FiniUneFois(p, Q, "npc.pretre", xp, 220, "fact.sacristain.apaise");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Les_moines_du_dessous_puis_l_abbe_renverse(bool name)
    {
        const string Q = "quest.v257.moines_du_dessous", Q2 = "quest.v257.abbe_renverse";
        var p = new Partie(HeroAt(20, new[] { "fact.met.frere_hospitalier" }));
        p.Talk("npc.frere_hospitalier", "accueil", "text.v257.hospitalier.moines.demande", "text.v257.hospitalier.moines.oui");
        long xp = Xp(p);
        p.Reach("zone.crypte_abbaye", "galerie_des_gisants");
        Assert.Contains("discovery.v257.necrologe_efface", p.W.Narrative!.Discoveries);
        p.Win("fact.gardien_de_l_ossuaire.vaincu");
        p.Open("zone.crypte_tombeau");
        p.Win("fact.abbe_sans_repos.apaise");
        Assert.Equal("choix", p.Step(Q));
        p.Talk("npc.frere_hospitalier", "v257_moines_choix",
            name ? "text.v257.hospitalier.moines_choix.nommer" : "text.v257.hospitalier.moines_choix.effacer");
        Assert.Equal(name ? 0 : 1, p.W.Inventory.Count("item.relique_de_la_chapelle"));
        FiniUneFois(p, Q, "npc.frere_hospitalier", xp, 1600);
        Assert.True(Says(p.View("npc.frere_hospitalier"), name ? "text.v257.hospitalier.abbe_nomme" : "text.v257.hospitalier.abbe_efface"));
        // L'abbé renversé : au niveau 25, après les moines.
        Assert.DoesNotContain(p.View("npc.frere_hospitalier").Options, o => o.Text == "text.v257.hospitalier.abbe.demande");
        p.Level(25);
        p.Talk("npc.frere_hospitalier", "accueil", "text.v257.hospitalier.abbe.demande", "text.v257.hospitalier.abbe.oui");
        long xp2 = Xp(p);
        p.Reach(Envers, "envers_de_l_abbaye");
        p.Win("fact.abbe_renverse.vaincu");
        p.Talk("npc.frere_hospitalier", "v257_abbe_retour", "text.v257.hospitalier.abbe_retour.merci");
        FiniUneFois(p, Q2, "npc.frere_hospitalier", xp2, 1800, "fact.abbe_renverse.vaincu");
    }

    [Fact]
    public void Ce_que_gardent_les_eclats()
    {
        const string Q = "quest.v257.ce_que_gardent_les_eclats";
        var p = new Partie(HeroAt(22, new[] { "fact.met.ysarn" }, new[] { "quest.le_seuil" }));
        p.Talk("npc.ysarn", "v257_eclats_offre", "text.v257.ysarn.eclats.accepter");
        long xp = Xp(p);
        p.Reach("zone.galeries_seuil", "galeries_cristallines");
        p.Win("fact.ours_des_eclats.vaincu");
        p.Reach("zone.galeries_eclats", "salle_des_eclats_geants");
        p.Win("fact.veilleur_des_eclats.vaincu");
        p.Talk("npc.ysarn", "v257_eclats_retour", "text.v257.ysarn.eclats_retour.merci");
        Assert.Equal(DialogueRefusal.None, p.W.ChooseReward(Q, 0, out _));
        p.Ticks();
        Assert.Equal(2, p.W.Inventory.Count("item.elixir_du_seuil"));
        FiniUneFois(p, Q, "npc.ysarn", xp, 2000, "fact.veilleur_des_eclats.vaincu");
    }

    [Fact]
    public void Les_pierres_qui_parlent_a_l_envers_dans_n_importe_quel_ordre()
    {
        const string Q = "quest.v257.pierres_a_l_envers";
        // Deux stèles déjà lues avant la quête : leurs étapes se remplissent d'elles-mêmes, dans l'ordre.
        var p = new Partie(HeroAt(24, new[] { "fact.met.archiviste", "fact.secret.stele_du_cercle", "fact.secret.stele_de_la_place" }, new[] { "quest.le_seuil" }));
        p.Talk("npc.archiviste", "v257_steles_offre", "text.v257.archiviste.steles.accepter");
        Assert.Equal("gue", p.Step(Q));
        long xp = Xp(p);
        p.Open(Envers);
        foreach (string s in new[] { "stele_des_noyes", "stele_de_l_arbre", "stele_du_gue" }) p.Win(StoryValidator.SecretFact(s));
        Assert.Equal("rosace", p.Step(Q));
        p.Win("fact.secret.stele_de_la_rosace");
        Assert.Equal("retour", p.Step(Q));
        Assert.All(new[] { "place", "gue", "arbre", "rosace", "noyes", "cercle" }, s => Assert.Contains("discovery.v257.stele_" + s, p.W.Narrative!.Discoveries));
        p.Talk("npc.archiviste", "v257_steles_retour", "text.v257.archiviste.steles_retour.merci");
        FiniUneFois(p, Q, "npc.archiviste", xp, 5 * 100 + 1500, "fact.secret.stele_du_gue");   // la stèle de la place, lue avant, comptée à l'acceptation
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void La_voix_du_moulin(bool truth)
    {
        const string Q = "quest.v257.voix_du_moulin";
        var p = new Partie(HeroAt(24, new[] { "fact.met.albe", "fact.met.odile", "fact.odile.retour" },
            new[] { "quest.le_seuil", "quest.traces_sous_le_moulin", "quest.bourg_d_aube", "quest.pont_des_vallons" }));
        p.Talk("npc.albe", "v257_moulin_offre", "text.v257.albe.moulin.accepter", "text.v257.albe.moulin_depart.partir");
        Assert.True(p.W.Narrative!.IsActive(Q));
        long xp = Xp(p);
        if (p.Step(Q) == "albe") p.Talk("npc.albe", "v257_moulin_depart", "text.v257.albe.moulin_depart.partir");
        Assert.Equal("felure", p.Step(Q));
        p.Open(Envers);   // l'autel de l'Aube est dans la fêlure renversée
        Assert.Equal("gardien", p.Step(Q));
        p.Win("fact.reflet_du_gardien.vaincu");
        p.Win("fact.secret.stele_de_la_rosace");
        Assert.Equal("moulin", p.Step(Q));
        p.Reach(Envers, "envers_du_village");
        Assert.Contains("discovery.v257.moulin_renverse", p.W.Narrative!.Discoveries);
        p.Talk("npc.odile", "v257_moulin_choix", truth ? "text.v257.odile.moulin_choix.verite" : "text.v257.odile.moulin_choix.silence");
        Assert.Equal("fin", p.Step(Q));
        // Un deuxième choix n'est plus possible : Odile ne repose plus la question.
        Assert.NotEqual("v257_moulin_choix", p.NodeOf("npc.odile"));
        p.Talk("npc.albe", "v257_moulin_fin", "text.v257.albe.moulin_fin.merci");
        Assert.Equal(DialogueRefusal.None, p.W.ChooseReward(Q, 2, out _));
        p.Ticks();
        Assert.Equal(3, p.W.Inventory.Count("item.eclat_de_seuil"));
        FiniUneFois(p, Q, "npc.albe", xp, 2400);
        Assert.Contains(truth ? "fact.v257.moulin.verite" : "fact.v257.moulin.silence", p.W.Narrative!.Facts);
        Assert.Equal(truth ? 1 : 0, p.W.Narrative.Trust("npc.odile"));
        Assert.True(Says(p.View("npc.odile"), truth ? "text.v257.odile.moulin_dit" : "text.v257.odile.moulin_tu"));
    }

    [Fact]
    public void L_ombre_du_vieux_Brunot()
    {
        const string Q = "quest.v257.ombre_de_brunot";
        var p = new Partie(HeroAt(25, new[] { "fact.met.gaspard", "fact.croc_gris.vaincu" },
            new[] { "quest.le_vieux_brunot", "quest.traces_sous_le_moulin", "quest.passages_de_la_vallee", "quest.le_loup_blanc", "quest.pierres_gravees", "quest.bete_blanche" }));
        p.Talk("npc.gaspard", "v257_brunot_offre", "text.v257.gaspard.brunot.accepter");
        long xp = Xp(p);
        p.Reach(Envers, "envers_de_la_foret");
        p.Win("fact.ombre_du_vieux_brunot.vaincue");
        p.Talk("npc.gaspard", "v257_brunot_retour", "text.v257.gaspard.brunot_retour.merci");
        FiniUneFois(p, Q, "npc.gaspard", xp, 1800, "fact.ombre_du_vieux_brunot.vaincue");
    }

    [Fact]
    public void Le_reflet_de_Machefer()
    {
        const string Q = "quest.v257.reflet_de_machefer";
        var p = new Partie(HeroAt(25, new[] { "fact.met.capitaine", "fact.ch4.chef.livre", "fact.ch4.chef.decide", "fact.ch4.capitaine.chef" },
            new[] { "quest.tour_et_camp", "quest.tour_silencieuse", "quest.premier_contrat", "quest.v257.sous_les_paves" }));
        DialogueView offer = p.View("npc.capitaine");
        Assert.Equal("v257_machefer_offre", offer.Node);
        Assert.True(Says(offer, "text.v257.capitaine.machefer.livre"));   // Mâchefer livré : le capitaine le sait en geôle
        p.Talk("npc.capitaine", "v257_machefer_offre", "text.v257.capitaine.machefer.accepter");
        long xp = Xp(p);
        p.Reach(Envers, "envers_du_fort");
        p.Win("fact.reflet_de_machefer.vaincu");
        p.Talk("npc.capitaine", "v257_machefer_retour", "text.v257.capitaine.machefer_retour.merci");
        FiniUneFois(p, Q, "npc.capitaine", xp, 1800, "fact.reflet_de_machefer.vaincu");
    }

    [Fact]
    public void L_eau_grise_seulement_quand_la_corruption_gagne()
    {
        const string Q = "quest.v257.eau_grise";
        // Sans corruption : Gerberte n'en parle pas.
        var calm = new Partie(HeroAt(20, new[] { "fact.met.foulonniere" }));
        DialogueView quiet = calm.View("npc.foulonniere");
        Assert.Equal(0, calm.W.CorruptedLands);
        Assert.DoesNotContain(quiet.Options, o => o.Text == "text.v257.foulonniere.eau.accepter");
        Assert.False(calm.W.Narrative!.IsOpen(Q));
        // Au dix-huitième jour, la corruption a gagné ses terres : la question vient.
        var p = new Partie(HeroAt(20, new[] { "fact.met.foulonniere" }), NoonOf(18)) { Corrupt = true };
        p.Open(Vallons);
        Assert.True(p.W.CorruptedLands >= 3, p.W.CorruptedLands.ToString());
        Assert.True(p.W.Narrative!.IsOpen(Q));
        p.Talk("npc.foulonniere", "accueil", "text.v257.foulonniere.eau.accepter", "text.v257.foulonniere.eau.oui");
        long xp = Xp(p);
        p.Reach(Vallons, "cercle_des_pierres_levees");
        Assert.Contains("discovery.v257.eau_grise", p.W.Narrative!.Discoveries);
        p.Kill("echo_corrompu", "echo", 4);
        int shards = p.W.Inventory.Count("item.eclat_de_seuil");
        p.Talk("npc.foulonniere", "v257_eau_retour", "text.v257.foulonniere.eau_retour.merci");
        Assert.Equal(shards + 1, p.W.Inventory.Count("item.eclat_de_seuil"));
        FiniUneFois(p, Q, "npc.foulonniere", xp, 1500);
    }

    // ---------------------------------------------------------------------------------------------------------------------------------
    // Dialogues vivants et mécanismes
    // ---------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0, "text.v257.charron.printemps")]
    [InlineData(1, "text.v257.charron.ete")]
    [InlineData(2, "text.v257.charron.automne")]
    [InlineData(3, "text.v257.charron.hiver")]
    public void Les_repliques_changent_avec_la_saison(int season, string text)
    {
        // Calendrier du jeu (sans date fournie) : la saison du jour du monde, à midi (pas la nuit : la réplique de nuit passerait avant).
        long day = Enumerable.Range(0, 1000).First(d => EdS.Data.Story.WorldCalendar.SeasonIndexOf(d) == season);
        var p = new Partie(HeroAt(5, new[] { "fact.met.charron_hameau" }), NoonOf(day));
        DialogueView view = p.View("npc.charron_hameau");
        Assert.Equal(season, p.W.SeasonIndex);
        Assert.True(Says(view, text), view.Text);
        // Avant la première rencontre : l'accueil ordinaire, en toute saison.
        var stranger = new Partie(HeroAt(5), NoonOf(day));
        Assert.True(Says(stranger.View("npc.charron_hameau"), "text.v243.charron.accueil"));
    }

    [Fact]
    public void Les_sujets_inscrivent_les_fragments_de_lore_une_fois()
    {
        var p = new Partie(HeroAt(5, new[] { "fact.met.passeuse_du_pont" }));
        p.Talk("npc.passeuse_du_pont", "accueil", "text.v257.passeuse.sujet.murmures", "text.v257.passeuse.murmures.autre", "text.v257.passeuse.sujet.ambre");
        Assert.Contains("discovery.v257.pont_des_murmures", p.W.Narrative!.Discoveries);
        Assert.Contains("discovery.v257.route_d_ambre", p.W.Narrative.Discoveries);
        int count = p.W.Narrative.Discoveries.Count;
        p.Talk("npc.passeuse_du_pont", "accueil", "text.v257.passeuse.sujet.murmures");
        Assert.Equal(count, p.W.Narrative!.Discoveries.Count);
        p.Open(Vallons);   // gardés dans la sauvegarde
        Assert.Contains("discovery.v257.route_d_ambre", p.W.Narrative!.Discoveries);
    }

    [Fact]
    public void Une_stele_lue_apprend_son_fait_une_fois_et_la_sauvegarde_le_garde()
    {
        // La stèle de la rosace déplacée à l'autel de l'Aube (point d'arrivée de l'Envers).
        JsonObject root = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, Envers)))!.AsObject();
        root["data"]!["secrets"]!.AsArray().Single(s => (string)s!["id"]! == "stele_de_la_rosace")!["position"] = new JsonArray(1656, -468.5);
        ZoneDefinition zone = ZoneJsonReader.Read(root.ToJsonString(), ScenariosDirectory, ShippedData, ItemsCatalog).Zone!;
        var w = new WorldSession(new WorldSave(zone.Id, 0, 0, new[] { PlayerCharacters.PlacesKey }, HeroAt(24)), zone, ShippedData, Progression, ItemsCatalog,
            Story.Value, storyZone: Vallons);
        w.Step();
        Assert.Equal(SecretRefusal.None, w.OpenSecret("stele_de_la_rosace"));
        Assert.Contains("FactLearned fact.secret.stele_de_la_rosace", w.Step().Story.Select(e => e.ToString()));
        Assert.True(w.Narrative!.Knows("fact.secret.stele_de_la_rosace"));
        Assert.Equal(SecretRefusal.AlreadyOpened, w.OpenSecret("stele_de_la_rosace"));
        Assert.DoesNotContain(w.Step().Story, e => e.Kind == StoryEventKind.FactLearned);
        WorldSave saved = SaveJsonReader.Read(SaveJsonReader.Write(w.Save())).Save!;
        Assert.Contains("fact.secret.stele_de_la_rosace", saved.Character!.Story.Facts);
        // Une stèle lue avant le V2.57 (clé au registre, sans le fait) : relue, elle l'apprend ; rien d'autre ne change.
        var before = new WorldSession(new WorldSave(zone.Id, 0, 0, new[] { PlayerCharacters.PlacesKey, zone.SecretKey("stele_de_la_rosace") }, HeroAt(24)), zone,
            ShippedData, Progression, ItemsCatalog, Story.Value, storyZone: Vallons);
        before.Step();
        Assert.False(before.Narrative!.Knows("fact.secret.stele_de_la_rosace"));
        Assert.Equal(SecretRefusal.AlreadyOpened, before.OpenSecret("stele_de_la_rosace"));
        Assert.Contains("FactLearned fact.secret.stele_de_la_rosace", before.Step().Story.Select(e => e.ToString()));
        // Sans histoire : le fait est gardé pour la sauvegarde, comme celui d'un chef.
        var bare = new WorldSession(new WorldSave(zone.Id, 0, 0, new[] { PlayerCharacters.PlacesKey }, HeroAt(24)), zone, ShippedData, Progression, ItemsCatalog);
        bare.Step();
        Assert.Equal(SecretRefusal.None, bare.OpenSecret("stele_de_la_rosace"));
        Assert.Contains("fact.secret.stele_de_la_rosace", SaveJsonReader.Read(SaveJsonReader.Write(bare.Save())).Save!.Character!.Story.Facts);
    }

    [Fact]
    public void Niveaux_alignes_sur_les_lieux_vises()
    {
        // Décision de Damien du 27 novembre 2026 : le filon et l'eau grise s'ouvrent deux niveaux sous le cœur de cristal et le cercle (22).
        StoryBook book = Story.Value;
        long Level(string q) => book.Quest(q).Requires.Single(c => c.Kind == ConditionKind.LevelAtLeast).Value;
        Assert.Equal(20, Level("quest.v257.filon_des_disparus"));
        Assert.Equal(20, Level("quest.v257.eau_grise"));
        // Chaque quête s'ouvre au plus deux niveaux sous le lieu le plus profond qu'elle vise (dans le plafond de niveau, 25).
        var places = new[] { Zone() }.Concat(V16TestData.Interiors).SelectMany(z => z.Places).ToDictionary(p => p.Id, p => p.MinLevel);
        foreach (string q in Quetes)
        {
            int deepest = book.Quest(q).Steps.Where(s => s.Objective.Kind == ObjectiveKind.Reach).Select(s => places[s.Objective.Id]).DefaultIfEmpty(0).Max();
            if (deepest > 0) Assert.True(Level(q) >= Math.Min(25, deepest - 2) && Level(q) <= deepest, $"{q} : {Level(q)} pour un lieu de niveau {deepest}");
        }
    }

    [Fact]
    public void Un_lieu_dans_deux_zones_reliees_est_refuse()
    {
        // La même zone reliée deux fois : chacun de ses lieux est dans deux zones (InPlace ne connaît que l'identifiant).
        ZoneDefinition sewer = V16TestData.Interiors.Single(z => z.Id == "zone.egouts_collecteur");
        StoryLoadResult r = StoryJsonReader.ReadStoryFile(StoryJsonReader.StoryPathIn(DataDirectory), Texts, Zone(), ItemsCatalog,
            V16TestData.Interiors.Append(sewer).ToList());
        Assert.Null(r.Story);
        Assert.Contains(r.Report.Errors, e => e.Code == ValidationCode.DuplicateId && e.Path == "zone.egouts_collecteur:$.data.places[0].id");
    }

    [Fact]
    public void Le_retour_d_une_autre_quete_reste_accessible_pendant_une_quete_du_V257()
    {
        // La ronde du capitaine à rendre pendant les égouts : son retour passe avant l'attente des égouts.
        var p = new Partie(HeroAt(8, new[] { "fact.met.capitaine" }, new[] { "quest.premier_contrat" }));
        p.Talk("npc.capitaine", "v257_egouts_offre", "text.v257.capitaine.egouts.accepter");
        Assert.Equal("v257_egouts_attente", p.NodeOf("npc.capitaine"));
        p.Hero = p.W.Character.WithStory(new SavedStory(p.W.Character.Story.Facts, p.W.Character.Story.Quests
            .Append(new KeyValuePair<string, QuestProgress>("quest.v27.ronde_du_capitaine", new QuestProgress("retour")))
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal)));
        p.W = null!;
        Assert.Equal("v27_capitaine_merci", p.NodeOf("npc.capitaine"));
        Assert.True(p.W.Narrative!.IsActive("quest.v257.sous_les_paves"));
        // Chaque attente du V2.57 vient après les retours et récompenses du bloc de tête de son PNJ.
        foreach (Dialogue d in Story.Value.Dialogues)
        {
            int head = 0;
            while (head < d.Entries.Count && d.Entries[head].Conditions.Any(c => c.Kind is ConditionKind.QuestAt or ConditionKind.QuestReward or ConditionKind.QuestActive)) head++;
            for (int i = 0; i < head; i++)
                if (d.Entries[i].Node.StartsWith("v257_", StringComparison.Ordinal) && d.Entries[i].Conditions.Any(c => c.Kind == ConditionKind.QuestActive))
                    Assert.DoesNotContain(d.Entries.Skip(i + 1).Take(head - i - 1), e => !e.Node.StartsWith("v257_", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void La_corruption_se_lit_aussi_dans_un_interieur()
    {
        // Sœur Albe, dans la chapelle (un intérieur) : la corruption de la surface compte pour ses répliques.
        var p = new Partie(HeroAt(16, new[] { "fact.met.albe" }, new[] { "quest.traces_sous_le_moulin", "quest.bourg_d_aube", "quest.herbes_pour_la_chapelle" }),
            NoonOf(18)) { Corrupt = true };
        DialogueView view = p.View("npc.albe");
        Assert.Equal("zone.chapelle", p.W.Definition!.Id);
        Assert.Null(p.W.CorruptionBook);   // le catalogue n'est pas en jeu dans la chapelle
        Assert.True(p.W.CorruptedLands >= 4, p.W.CorruptedLands.ToString());
        Assert.Equal("ch2_apres", view.Node);
        Assert.True(Says(view, "text.v257.albe.corruption"), view.Text);
        var calm = new Partie(HeroAt(16, new[] { "fact.met.albe" }, new[] { "quest.traces_sous_le_moulin", "quest.bourg_d_aube", "quest.herbes_pour_la_chapelle" }),
            NoonOf(18));
        Assert.False(Says(calm.View("npc.albe"), "text.v257.albe.corruption"));
        Assert.Equal(0, calm.W.CorruptedLands);
    }

    [Fact]
    public void Repliques_de_corruption_selon_les_terres_corrompues()
    {
        var p = new Partie(HeroAt(16, new[] { "fact.met.charron_hameau" }), NoonOf(18)) { Corrupt = true };
        DialogueView view = p.View("npc.charron_hameau");
        Assert.True(p.W.CorruptedLands >= 4);
        Assert.True(Says(view, "text.v257.charron.corruption"), view.Text);
        // Toutes les terres purifiées le jour même : plus de réplique de corruption.
        p.W.SetCorruptionForTest(new SavedCorruption(Corruption.Value.Areas.Select(a => new KeyValuePair<string, long>(a.Id, 18))));
        Assert.Equal(0, p.W.CorruptedLands);
        Assert.Equal(DialogueRefusal.None, p.W.Talk("npc.charron_hameau", out DialogueView? clean));
        Assert.False(Says(clean!, "text.v257.charron.corruption"));
    }
}
