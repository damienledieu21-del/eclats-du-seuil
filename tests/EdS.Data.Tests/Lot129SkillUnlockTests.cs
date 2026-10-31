using System.Text.Json.Nodes;
using EdS.Data.Characters;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Validation;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.Simulation.Engine;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 12.9b (essai du 25 octobre 2026, choix de Damien : « La 2 pour l'instant et la 3 après ») : le personnage gagne des
/// compétences en montant de niveau, équipées d'elles-mêmes à la halte qui suit, aux niveaux où les règles ouvrent leurs emplacements
/// (Barrière d'acier au niveau 5, Onde de rupture et l'ultime Bastion absolu au niveau 10). Progression en schéma 2.
/// </summary>
[Trait("Category", "World")]
public class Lot129SkillUnlockTests : IDisposable
{
    private const string Barriere = "skill.bastion.barriere_d_acier", Onde = "skill.bastion.onde_de_rupture", Absolu = "skill.bastion.bastion_absolu",
        Provocation = "skill.bastion.provocation", Contrecoup = "skill.bastion.contrecoup";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_competences_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private static string ProgressionPath => ProgressionJsonReader.PathIn(DataDirectory);
    private static CharacterProgression Shipped => ProgressionJsonReader.ReadFile(ProgressionPath, ShippedData).Progression!;
    private static ProgressionRules Rules => ShippedData.Rules.Progression;
    private static ItemCatalog Items => ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!;

    private static ProgressionLoadResult Mutated(Action<JsonArray> mutate)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ProgressionPath))!.AsObject();
        mutate(root["data"]!["classes"]![0]!["skillUnlocks"]!.AsArray());
        return ProgressionJsonReader.Read(root.ToJsonString(), ShippedData);
    }

    private static JsonObject Unlock(int level, string skill) => new() { ["level"] = level, ["skill"] = skill };

    private static (string Path, ValidationCode Code) Error(ProgressionLoadResult result)
    {
        Assert.False(result.IsValid);
        return (result.Report.Errors[0].Path, result.Report.Errors[0].Code);
    }

    private static SavedCharacter Hero(long xp) => PlayerCharacters.Create("bastion", "class.bastion", Shipped).WithXp(xp);

    [Fact]
    public void Competences_gagnees_livrees()
    {
        ClassProgression bastion = Shipped.ForClass("class.bastion");
        // Réponse de Damien du 30 octobre 2026 : Provocation au niveau 3 et Contrecoup au niveau 7. Sous-lot V1.8 (4 places et l'ultime dès
        // le niveau 1, 5e au niveau 10, 6e au niveau 20) : une compétence vient avec sa place quand une place de sa sorte est libre à son
        // niveau ; Contrecoup arrive barre pleine, dans la réserve.
        Assert.Equal(new[] { (3, Provocation, SkillKind.Active, true), (5, Barriere, SkillKind.Active, true), (7, Contrecoup, SkillKind.Active, false),
                (10, Onde, SkillKind.Active, true), (10, Absolu, SkillKind.Ultimate, true) },
            bastion.SkillUnlocks.Select(u => (u.Level, u.Skill, u.Kind, u.WithPlace)));
        Assert.Equal(new[] { 1, 1, 1, 1, 10, 20 }, Rules.ActiveSlotLevels);
        Assert.Equal(1, Rules.UltimateUnlockLevel);
    }

    [Theory]
    [InlineData(0L, 0)]       // niveau 1 : build de départ
    [InlineData(180L, 1)]     // niveau 3 : Provocation, dans la 3e place (sous-lot V1.8)
    [InlineData(300L, 1)]     // niveau 4
    [InlineData(440L, 2)]     // niveau 5 : Barrière d'acier, 4e place
    [InlineData(1080L, 2)]    // niveau 7 : Contrecoup, dans la réserve
    [InlineData(1880L, 2)]    // niveau 9
    [InlineData(2350L, 4)]    // niveau 10 : Onde de rupture (5e place) et Bastion absolu
    public void Le_niveau_fait_gagner_ses_competences(long xp, int count)
    {
        SavedCharacter hero = Hero(xp);
        SavedCharacter learned = PlayerCharacters.Learn(hero, Shipped, Rules, out IReadOnlyList<SkillUnlock> gained);
        Assert.Equal(count, gained.Count);
        var expected = new List<string> { "skill.bastion.frappe_du_rempart", "skill.bastion.heurt_de_bouclier" };
        expected.AddRange(new[] { Provocation, Barriere, Onde }.Take(Math.Min(count, 3)));
        Assert.Equal(expected, learned.ActiveSkills);
        Assert.Equal(count == 4 ? Absolu : null, learned.Ultimate);
        Assert.Equal(new[] { "skill.bastion.endurance_du_roc" }, learned.PassiveSkills);
        if (count == 0) Assert.Same(hero, learned);
        // Une seconde fois : rien de plus (le personnage les a déjà).
        Assert.Same(learned, PlayerCharacters.Learn(learned, Shipped, Rules, out IReadOnlyList<SkillUnlock> again));
        Assert.Empty(again);
        // Le build obtenu est valide à son niveau (entrée en zone possible).
        PlayerCharacters.Setup(learned, Shipped, ShippedData, new CombatantPlacement(GroundVector.Parse("0", "0"), Fixed.Parse("0.5")));
    }

    /// <summary>
    /// Un emplacement déjà pris : rien n'est remplacé. Avec le registre du monde (la forme qu'utilise la partie), la compétence va dans la
    /// réserve pour de bon : elle est donnée, la halte ne la placera plus (relecture indépendante de la PR #74). La forme sans registre
    /// (<see cref="PlayerCharacters.Learn(SavedCharacter, CharacterProgression, ProgressionRules, out IReadOnlyList{SkillUnlock})"/>) ne
    /// se souvient de rien : elle la placerait dès qu'une place se libère.
    /// </summary>
    [Fact]
    public void Un_emplacement_deja_pris_met_la_competence_dans_la_reserve()
    {
        // Sous-lot V1.8 : au niveau 7, le joueur a mis lui-même Contrecoup dans la 4e place (la Barrière n'y était pas encore) : la
        // Barrière ne remplace rien.
        const string Frappe = "skill.bastion.frappe_du_rempart", Heurt = "skill.bastion.heurt_de_bouclier";
        var chosen = new SavedCharacter("bastion", "class.bastion", 1080, null, new[] { Frappe, Heurt, Provocation, Contrecoup }, null,
            new[] { "skill.bastion.endurance_du_roc" });
        var registry = new HashSet<string>(StringComparer.Ordinal);
        Assert.Same(chosen, PlayerCharacters.Learn(chosen, Shipped, Rules, registry.Contains, out IReadOnlyList<SkillUnlock> none,
            out IReadOnlyList<SkillUnlock> handed));
        Assert.Empty(none);
        Assert.Equal(new[] { Provocation, Barriere }, handed.Select(u => u.Skill));   // données : Provocation déjà placée, la Barrière en réserve
        foreach (SkillUnlock u in handed) registry.Add(u.GivenKey);
        // Au niveau 10, avec le registre : la Barrière reste dans la réserve, l'Onde prend la 5e place, l'ultime arrive.
        SavedCharacter tenGiven = PlayerCharacters.Learn(chosen.WithXp(2350), Shipped, Rules, registry.Contains, out IReadOnlyList<SkillUnlock> placed, out _);
        Assert.Equal(new[] { Onde, Absolu }, placed.Select(u => u.Skill));
        Assert.Equal(new[] { Frappe, Heurt, Provocation, Contrecoup, Onde }, tenGiven.ActiveSkills);
        // Sans registre (la fonction seule) :
        Assert.Same(chosen, PlayerCharacters.Learn(chosen, Shipped, Rules, out none));
        Assert.Empty(none);
        // Déjà équipée ailleurs : pas en double.
        var already = new SavedCharacter("bastion", "class.bastion", 1080, null, new[] { Barriere, Heurt, Provocation }, null,
            new[] { "skill.bastion.endurance_du_roc" });
        Assert.Same(already, PlayerCharacters.Learn(already, Shipped, Rules, out none));
        Assert.Empty(none);
        // Sans registre, au niveau 10, dans l'ordre de la progression : la Barrière prend la 5e place, l'Onde ne trouve plus de place,
        // l'ultime arrive ; Contrecoup n'est jamais placé d'office.
        SavedCharacter ten = PlayerCharacters.Learn(chosen.WithXp(2350), Shipped, Rules, out IReadOnlyList<SkillUnlock> gained);
        Assert.Equal(new[] { Barriere, Absolu }, gained.Select(u => u.Skill));
        Assert.Equal(new[] { Frappe, Heurt, Provocation, Contrecoup, Barriere }, ten.ActiveSkills);
        Assert.Equal(Absolu, ten.Ultimate);
    }

    [Fact]
    public void Competences_gagnees_invalides_refusees_avec_leur_chemin()
    {
        const string at = "$.data.classes[0].skillUnlocks";
        Assert.Equal((at + "[0].level", ValidationCode.OutOfRange), Error(Mutated(u => u[0]!["level"] = 1)));
        Assert.Equal((at + "[4].level", ValidationCode.OutOfRange), Error(Mutated(u => u[4]!["level"] = 31)));   // données jusqu'au niveau 30
        Assert.Equal((at + "[0].skill", ValidationCode.UnknownReference), Error(Mutated(u => u[0]!["skill"] = "skill.bastion.inconnue")));
        Assert.Equal((at + "[0].skill", ValidationCode.DuplicateId), Error(Mutated(u => u[0]!["skill"] = "skill.bastion.frappe_du_rempart")));
        Assert.Equal((at + "[1].skill", ValidationCode.DuplicateId), Error(Mutated(u => u[1]!["skill"] = Provocation)));
        Assert.Equal((at + "[1].level", ValidationCode.InvalidValue), Error(Mutated(u => u[1]!["level"] = 2)));   // ordre des niveaux
        Assert.Equal((at + "[4].level", ValidationCode.InvalidValue), Error(Mutated(u => { for (int i = 1; i <= 4; i++) u[i]!["level"] = 5; })));   // l'ultime demande le niveau 10
        // Sous-lot V1.8 : une compétence ne vient avec sa place que si une place est libre : la barre tient toujours, une compétence de trop
        // va dans la réserve. Contrecoup au niveau 5, après la Barrière : barre pleine, réserve.
        ProgressionLoadResult reserve = Mutated(u => u[2]!["level"] = 5);
        Assert.True(reserve.IsValid, reserve.Report.ToString());
        Assert.Equal(new[] { true, true, false, true, true }, reserve.Progression!.ForClass("class.bastion").SkillUnlocks.Select(u => u.WithPlace));
        // Contrecoup au niveau 10, avant l'Onde dans le fichier : il prend la 5e place, l'Onde va dans la réserve.
        ProgressionLoadResult first = Mutated(u => u[2]!["level"] = 10);
        Assert.True(first.IsValid, first.Report.ToString());
        Assert.Equal(new[] { (Contrecoup, true), (Onde, false) },
            first.Progression!.ForClass("class.bastion").SkillUnlocks.Skip(2).Take(2).Select(u => (u.Skill, u.WithPlace)));
        // Barrière au niveau 4 : la 4e place est libre depuis le niveau 1 ; elle vient avec sa place.
        ProgressionLoadResult early = Mutated(u => u[1]!["level"] = 4);
        Assert.True(early.IsValid, early.Report.ToString());
        Assert.True(early.Progression!.ForClass("class.bastion").SkillUnlocks[1].WithPlace);
        Assert.Equal((at + "[1].level", ValidationCode.MissingField), Error(Mutated(u => u[1]!.AsObject().Remove("level"))));
        Assert.Equal((at + "[1].skill", ValidationCode.MissingField), Error(Mutated(u => u[1]!.AsObject().Remove("skill"))));
        Assert.Equal((at + "[1]", ValidationCode.WrongType), Error(Mutated(u => u[1] = null)));
        Assert.Equal(at + "[0].bonus", Error(Mutated(u => u[0]!["bonus"] = 1)).Path);
        Assert.Equal(at + "[0].level", Error(Mutated(u => u[0]!["level"] = "cinq")).Path);
        Assert.Equal(at, Error(Mutated(u => { for (int i = 0; i < 60; i++) u.Add(Unlock(10, "skill.bastion.provocation")); })).Path);   // plus de 64
        // Liste absente : refusée ; vide : admise (aucune compétence gagnée).
        JsonObject root = JsonNode.Parse(File.ReadAllText(ProgressionPath))!.AsObject();
        root["data"]!["classes"]![0]!.AsObject().Remove("skillUnlocks");
        Assert.Equal((at, ValidationCode.MissingField), Error(ProgressionJsonReader.Read(root.ToJsonString(), ShippedData)));
        ProgressionLoadResult empty = Mutated(u => u.Clear());
        Assert.True(empty.IsValid, empty.Report.ToString());
        Assert.Empty(empty.Progression!.ForClass("class.bastion").SkillUnlocks);
    }

    [Fact]
    public void La_session_equipe_les_competences_et_les_sauvegarde()
    {
        // Sauvegarde de V1.8 (relecture de la PR #82) : le personnage a atteint le niveau 5 après V1.8.
        var save = new WorldSave("zone.vallons", 0, 0, new[] { PlayerCharacters.PlacesKey }, Hero(440));   // niveau 5
        var zone = ZoneJsonReader.ReadFile(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons"), Path.Combine(AppContext.BaseDirectory, "scenarios"),
            ShippedData, Items).Zone!;
        var first = new WorldSession(save, zone, ShippedData, Shipped, Items);
        Assert.Equal(new[] { Provocation, Barriere }, first.Learned.Select(u => u.Skill));
        Assert.Contains(Barriere, first.Character.ActiveSkills);
        WorldSave saved = first.Save();
        Assert.Equal(4, saved.Character!.ActiveSkills.Count);
        // Relue : la sauvegarde garde la compétence, la session suivante n'a plus rien à gagner.
        WorldSave reread = SaveJsonReader.Read(SaveJsonReader.Write(saved)).Save!;
        var second = new WorldSession(reread, zone, ShippedData, Shipped, Items);
        Assert.Empty(second.Learned);
        Assert.Equal(saved.Character.ActiveSkills, second.Character.ActiveSkills);
    }

    [Fact]
    public void L_aventure_annonce_le_niveau_puis_la_competence_a_la_halte()
    {
        // Un loup affaibli (1 PV, donc 1 point d'expérience) près de l'entrée ; il manque 1 point pour le niveau 5.
        string scenarios = Path.Combine(_folder, "scenarios");
        Directory.CreateDirectory(Path.Combine(scenarios, "zones"));
        JsonObject vallons = JsonNode.Parse(File.ReadAllText(ScenarioPath("zones/vallons")))!.AsObject();
        foreach (JsonNode? c in vallons["data"]!["combatants"]!.AsArray())
            if ((string)c!["id"]! == "loup1")
            {
                c["position"] = new JsonArray(-15, -12);
                c["health"] = 1;
            }
        File.WriteAllText(Path.Combine(scenarios, "zones", "vallons.json"), vallons.ToJsonString());
        File.Copy(ScenarioPath("zones/vallons_chef"), Path.Combine(scenarios, "zones", "vallons_chef.json"));
        File.Copy(ScenarioPath("zones/vallons_bandits"), Path.Combine(scenarios, "zones", "vallons_bandits.json"), true);   // sous-lot V1.6 : formes du chef des bandits
        CopyMissingZoneScenarios(scenarios);   // intérieurs : reliés aux Vallons par leurs portes
        string savePath = Path.Combine(_folder, "partie", "sauvegarde.json");
        Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
        // Sous-lot V1.2 : le personnage porte son équipement de départ, comme une vraie partie (sinon ses places obligatoires seraient
        // remplies au chargement, avec un message de plus).
        SaveStore.Write(savePath, new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(),
            Inventory.Outfit(PlayerCharacters.Create(AdventureSession.PlayerId, AdventureSession.PlayerClass, Shipped).WithXp(439)   // niveau 4
                .WithSkills(new[] { "skill.bastion.frappe_du_rempart", "skill.bastion.heurt_de_bouclier", Provocation }, null,
                    new[] { "skill.bastion.endurance_du_roc" }), Items)));
        AdventureSession a = AdventureSession.Load(DataDirectory, scenarios, savePath);
        Assert.Equal(new[] { "Partie reprise." }, a.DrainMessages());
        Assert.Empty(a.World.Learned);
        CombatEngine e = a.World.Zone.Engine;
        for (int i = 0; i < 400 && !e.View("loup1").IsDead; i++)
        {
            long now = e.CurrentTick * 50;
            CombatantView me = e.View(AdventureSession.PlayerId), wolf = e.View("loup1");
            if (me.Position is GroundVector p && wolf.Position is GroundVector w && wolf.IsPresent)
            {
                double dx = (w.X.Raw - p.X.Raw) / 1e4, dz = (w.Z.Raw - p.Z.Raw) / 1e4;
                a.Combat.SetMove(Math.Sqrt(dx * dx + dz * dz) > 2 ? dx : 0, Math.Sqrt(dx * dx + dz * dz) > 2 ? dz : 0);
                try { e.Submit(new CombatCommand(now, AdventureSession.PlayerId, "skill.bastion.frappe_du_rempart", "loup1")); }
                catch (ArgumentException) { }
            }
            a.Advance(0.05);
        }
        Assert.True(e.View("loup1").IsDead);
        a.Combat.SetMove(0, 0);
        IReadOnlyList<string> messages = a.DrainMessages();
        Assert.Contains("Niveau 5 ! Il prendra effet à ta prochaine halte.", messages);
        Assert.Contains("À ta prochaine halte, nouvelle compétence : Barrière d'acier.", messages);
        Assert.Equal(3, a.World.Character.ActiveSkills.Count);   // pas avant la halte
        // Retour au point sûr d'entrée, halte : la compétence est équipée sur la touche 4, et sauvegardée.
        for (int i = 0; i < 400 && !a.CanRest; i++)
        {
            GroundVector p = e.View(AdventureSession.PlayerId).Position!.Value;
            a.Combat.SetMove(-18 - p.X.Raw / 1e4, -12 - p.Z.Raw / 1e4);
            a.Advance(0.05);
        }
        a.Combat.SetMove(0, 0);
        Assert.True(a.Rest());
        Assert.Contains("Nouvelle compétence : Barrière d'acier (touche 4).", a.DrainMessages());
        Assert.Equal(Barriere, a.World.Character.ActiveSkills[3]);
        Assert.Contains(Barriere, a.World.Zone.Engine.View(AdventureSession.PlayerId).Character!.Build.ActiveSkills);   // au combat
        a.Save();
        Assert.Contains(Barriere, SaveStore.Read(savePath).Save!.Character!.ActiveSkills);
    }
}
