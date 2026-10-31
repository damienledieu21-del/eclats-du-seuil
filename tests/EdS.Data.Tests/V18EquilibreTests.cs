using System.Text.Json.Nodes;
using EdS.Data.Characters;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Zones;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;
using EdS.Simulation.Pilot;
using EdS.World;
using Xunit.Abstractions;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V1.8b, difficulté normale (« on meurt parfois si on joue mal ; le Gardien demande garde et parade ») : combats simulés par le
/// moteur, un par chapitre de la quête principale, avec un Bastion typique du dernier niveau du chapitre (compétences gagnées en montant,
/// meilleur équipement ordinaire de son niveau, sans talent ni objet nommé) contre des créatures des Vallons montées à son niveau (zones en
/// mélange). Le joueur est le pilote de mesure du moteur (<see cref="CombatPilot"/> : garde tenue, parades selon le profil, compétences par
/// priorité), qui marche vers sa cible. Ces tests échouent si un combat devient trop facile ou impossible.
/// </summary>
[Trait("Category", "World")]
public class V18EquilibreTests
{
    private readonly ITestOutputHelper _out;

    public V18EquilibreTests(ITestOutputHelper output) => _out = output;

    private static ItemCatalog Items => V11TestData.ItemsCatalog;
    private static CharacterProgression Progression => V11TestData.Progression;

    /// <summary>Créature typique de chaque chapitre (son lieu dans les Vallons).</summary>
    public static readonly string[] Creatures = { "loup1", "archer", "loup7", "bandit_epee1", "rodeur" };

    /// <summary>
    /// Équipement typique du niveau <paramref name="level"/> : pour chaque place du combat, l'objet ordinaire (ni nommé, ni à effet, au plus
    /// très rare) du plus haut niveau requis qui ne le dépasse pas (le premier du catalogue à niveau égal).
    /// </summary>
    public static Dictionary<string, string> TypicalGear(int level)
    {
        var worn = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (EquipmentPlace place in Items.Places.Where(p => p.Group == PlaceGroup.Combat))
        {
            ItemDefinition? best = Items.Items.Where(i => i.Type == ItemType.Equipment && i.Slot == place.Accepts && i.RequiredLevel <= level && !i.Named
                    && i.Effect is null && i.Set is null && i.Rarity <= ItemRarity.VeryRare && i.Id != "item.epee_du_novice" && i.Id != "item.ecu_du_novice")
                .OrderByDescending(i => i.RequiredLevel).FirstOrDefault();
            if (best != null) worn[place.Id] = best.Id;
        }
        return worn;
    }

    /// <summary>Bastion typique du niveau : compétences gagnées (la 6e place reçoit Contrecoup dès le niveau 20), équipement typique, potions.</summary>
    public static SavedCharacter Typical(int level, int potions)
    {
        SavedCharacter hero = PlayerCharacters.Learn(PlayerCharacters.Create("bastion", "class.bastion", Progression).WithXp(Progression.XpToLevel[level - 1]),
            Progression, ShippedData.Rules.Progression, out _);
        if (level >= 20) hero = hero.WithSkills(hero.ActiveSkills.Append("skill.bastion.contrecoup").ToArray(), hero.Ultimate, hero.PassiveSkills);
        Dictionary<string, string> worn = TypicalGear(level);
        var stacks = worn.Values.Select(i => new ItemStack(i, 1)).ToList();
        if (potions > 0) stacks.Add(new ItemStack("item.potion_de_soin", potions));
        return hero.WithItems(stacks, worn);
    }

    /// <summary>
    /// Vallons livrés ; les créatures données sont amenées près de l'entrée, au village. Le village prend le niveau minimum
    /// <paramref name="placeMin"/> (celui du vrai lieu des créatures ; 1 par défaut : leur niveau est alors celui du joueur moins 2) ; une
    /// créature à niveau propre (loup blanc, Borgne) garde le sien. Avec <paramref name="boss"/>, l'entrée est près du Gardien et la zone est
    /// la vraie (relecture de la PR #83 : plus rien n'est calmé ; le rôdeur du Seuil est hors de portée du Gardien).
    /// </summary>
    public static WorldSession Arena(SavedCharacter hero, string[] creatures, bool boss = false, int placeMin = 1)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!.AsObject();
        foreach (JsonNode? p in root["data"]!["places"]!.AsArray()) if ((string)p!["id"]! == "village") p["minLevel"] = placeMin;
        // Une créature rare de la nuit demandée (le loup blanc) est là dès le début, comme quand une quête la demande.
        foreach (JsonNode? p in root["data"]!["populations"]!.AsArray()) if (creatures.Contains((string)p!["creature"]!)) p.AsObject().Remove("night");
        string directory = Path.Combine(Path.GetTempPath(), "eds_v18_equilibre_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(directory, "zones"));
            JsonObject zone = JsonNode.Parse(File.ReadAllText(ScenarioPath("zones/vallons")))!.AsObject();
            if (boss) root["data"]!["safePoints"]![0]!["position"] = new JsonArray(51.5, 36);
            int k = 0;
            foreach (JsonNode? c in zone["data"]!["combatants"]!.AsArray())
            {
                string id = (string)c!["id"]!;
                if (!boss && creatures.Contains(id)) { c["position"] = new JsonArray(-14 + 2 * k, -12 + (k % 2) * 2); k++; }
            }
            File.WriteAllText(Path.Combine(directory, "zones", "vallons.json"), zone.ToJsonString());
            File.Copy(ScenarioPath("zones/vallons_chef"), Path.Combine(directory, "zones", "vallons_chef.json"));
            File.Copy(ScenarioPath("zones/vallons_bandits"), Path.Combine(directory, "zones", "vallons_bandits.json"), true);
            CopyMissingZoneScenarios(directory);
            ZoneLoadResult loaded = ZoneJsonReader.Read(root.ToJsonString(), directory, ShippedData, Items);
            Assert.True(loaded.IsValid, loaded.Report.ToString());
            // Sauvegarde de V1.8 (relecture de la PR #82).
            return new WorldSession(new WorldSave("zone.vallons", 0, 0, new[] { PlayerCharacters.PlacesKey }, hero), loaded.Zone!, ShippedData, Progression, Items);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Combat simulé : le pilote du moteur décide (garde, parades selon <paramref name="profile"/>, compétences), le Bastion marche vers son
    /// ennemi le plus proche encore debout et boit une potion sous 40 % de ses PV. Rend les PV restants (0 s'il tombe) et si tous les
    /// ennemis donnés sont vaincus.
    /// </summary>
    public static (long Left, bool Won) Fight(WorldSession session, string[] enemies, PilotProfile? profile, int seconds = 300)
    {
        CombatEngine e = session.Zone.Engine;
        // La défense vient du pilote de mesure du moteur (garde tenue, parades parfaites selon le profil) ; null : ni garde ni parade.
        CombatPilot? pilot = profile is PilotProfile p0 ? new CombatPilot(e, "bastion", p0) : null;
        bool boss = enemies.Length == 1 && enemies[0] == "gardien_du_seuil";
        var defeated = new HashSet<string>(StringComparer.Ordinal);
        int perTick = ShippedData.Rules.Combat.MaxCommandsPerActorPerTick;
        for (int i = 0; i < seconds * 20; i++)
        {
            // Une créature partie au bout de sa laisse revient sous un autre nom : on suit la vie en cours de chaque population.
            string[] targets = boss ? (session.BossForm is string form ? new[] { form } : Array.Empty<string>())
                : enemies.Where(p => !defeated.Contains(p)).Select(session.CurrentCreature).ToArray();
            CombatantView me = e.View("bastion");
            if (me.IsDead || (boss ? session.BossDefeated : targets.Length == 0)) break;
            long now = e.CurrentTick * 50;
            var commands = (pilot?.Decide() ?? Array.Empty<CombatCommand>())
                .Where(c => c.Action == CombatAction.Parry || c.Action == CombatAction.GuardStart || c.Action == CombatAction.GuardStop).ToList();
            CombatantView? it = targets.Select(e.View).Where(v => !v.IsDead && v.IsPresent && v.Position != null)
                .OrderBy(v => Distance(me, v)).FirstOrDefault();
            if (it != null && me.Position is GroundVector a && it.Position is GroundVector b)
            {
                // Compétences à tour de rôle sur l'ennemi le plus proche (la Barrière d'acier sur soi), sauf au tick d'une parade.
                string[] skills = session.Character.ActiveSkills.Append(session.Character.Ultimate).OfType<string>().ToArray();
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
                try { session.Drink("item.potion_de_soin"); } catch (ArgumentException) { } catch (InvalidOperationException) { }
            session.Step();
            if (!boss)
                foreach (string p in enemies)
                    if (!defeated.Contains(p) && e.View(session.CurrentCreature(p)).IsDead) defeated.Add(p);
        }
        bool won = boss ? session.BossDefeated : defeated.Count == enemies.Length;
        return (e.View("bastion").IsDead ? 0 : e.View("bastion").Health ?? 0, won);
    }

    private static double Distance(CombatantView a, CombatantView b) =>
        a.Position is GroundVector p && b.Position is GroundVector q
            ? Math.Sqrt(Math.Pow((p.X.Raw - q.X.Raw) / 1e4, 2) + Math.Pow((p.Z.Raw - q.Z.Raw) / 1e4, 2)) : double.MaxValue;

    /// <summary>Groupe typique de chaque chapitre (trois créatures ; deux rôdeurs au chapitre 5).</summary>
    public static string[] Pack(int chapter) => chapter switch
    {
        1 => new[] { "loup1", "loup2", "loup3" },
        2 => new[] { "archer", "sanglier6", "sanglier7" },
        3 => new[] { "loup7", "loup8", "loup9" },
        4 => new[] { "bandit_epee1", "bandit_epee2", "bandit_arc1" },
        _ => new[] { "rodeur", "rodeur2" },
    };

    private void Say(string what, int level, (long Left, bool Won) r, WorldSession w) =>
        _out.WriteLine($"{what} niveau {level} : {(r.Won ? "gagné" : "perdu")}, {r.Left}/{w.Zone.Engine.View("bastion").MaxHealth} PV");

    private static long Max(WorldSession w) => w.Zone.Engine.View("bastion").MaxHealth!.Value;

    /// <summary>
    /// Fin de chapitre (créature deux niveaux sous le joueur) : une créature typique se bat seule, sans garde, sans parade ni potion ; le
    /// Bastion gagne avec plus de la moitié de ses PV, mais elle l'entame (relecture de la PR #83 : au plus 98 % de ses PV à la fin ; le
    /// résultat bouge de quelques points avec des données sans rapport, car le tirage des coups critiques en dépend).
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Une_creature_seule_se_bat_sans_defense(int chapter)
    {
        int level = chapter * 5;
        string c = Creatures[chapter - 1];
        WorldSession w = Arena(Typical(level, 0), new[] { c });
        (long left, bool won) r = Fight(w, new[] { c }, null);
        Say(c, level, r, w);
        Assert.True(r.won, $"chapitre {chapter} : {c} encore debout");
        Assert.True(r.left * 2 >= Max(w), $"chapitre {chapter} : il ne reste que {r.left} PV");
        Assert.True(r.left * 100 <= Max(w) * 98, $"chapitre {chapter} : {c} n'entame pas le Bastion ({r.left}/{Max(w)} PV)");
    }

    /// <summary>
    /// Fin de chapitre, ni trop facile ni impossible : un groupe du chapitre (trois potions) laisse le Bastion qui ne se défend pas entre 34
    /// et 66 % de ses PV (relecture de la PR #83 : les bornes du corps de la PR) ; au chapitre 5, il tombe. Avec la garde et les parades
    /// (pilote expert), il gagne avec au moins 10 % de ses PV.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Un_groupe_demande_de_se_defendre(int chapter)
    {
        int level = chapter * 5;
        string[] pack = Pack(chapter);
        WorldSession bare = Arena(Typical(level, 3), pack);
        (long Left, bool Won) loose = Fight(bare, pack, null);
        Say("groupe sans défense", level, loose, bare);
        long max = Max(bare);
        if (chapter == 5) Assert.Equal(0, loose.Left);   // « on meurt parfois si on joue mal »
        else Assert.InRange(loose.Left * 100 / max, 34, 66);
        WorldSession guarded = Arena(Typical(level, 3), pack);
        (long Left, bool Won) good = Fight(guarded, pack, PilotProfile.Expert);
        Say("groupe avec garde et parade", level, good, guarded);
        Assert.True(good.Won && good.Left * 10 >= Max(guarded), $"chapitre {chapter} : trop dur même en se défendant ({good.Left} PV)");
    }

    /// <summary>
    /// Relecture de la PR #83 : « on meurt parfois si on joue mal, mais on gagne toujours en jouant bien ». Sans potion, un débutant (pilote
    /// <see cref="PilotProfile.Beginner"/> : peu de parades) tombe face au groupe des chapitres 4 et 5 ; un expert, sans potion lui aussi, gagne.
    /// </summary>
    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void Mal_jouer_peut_tuer_bien_jouer_gagne(int chapter)
    {
        int level = chapter * 5;
        string[] pack = Pack(chapter);
        WorldSession beginner = Arena(Typical(level, 0), pack);
        (long Left, bool Won) bad = Fight(beginner, pack, PilotProfile.Beginner);
        Say("groupe, débutant sans potion", level, bad, beginner);
        Assert.Equal((0L, false), (bad.Left, bad.Won));
        WorldSession expert = Arena(Typical(level, 0), pack);
        (long Left, bool Won) good = Fight(expert, pack, PilotProfile.Expert);
        Say("groupe, expert sans potion", level, good, expert);
        Assert.True(good.Won && good.Left > 0);
    }

    /// <summary>
    /// Relecture de la PR #83 : début de chapitre (premier niveau : 1, 6, 11, 16, 21), les créatures au niveau minimum de leur lieu (donc au
    /// niveau du joueur, et non deux niveaux sous lui) : le groupe du chapitre se gagne en jouant bien (pilote expert, trois potions), avec au
    /// moins 10 % des PV ; une créature seule, sans défense ni potion, aussi.
    /// </summary>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 6)]
    [InlineData(3, 11)]
    [InlineData(4, 16)]
    [InlineData(5, 21)]
    public void Debut_de_chapitre_gagnable_en_jouant_bien(int chapter, int level)
    {
        Assert.Equal(level, Progression.ChapterStartLevels[chapter - 1]);
        string[] pack = Pack(chapter);
        WorldSession w = Arena(Typical(level, 3), pack, placeMin: level);
        (long Left, bool Won) good = Fight(w, pack, PilotProfile.Expert);
        Say("début de chapitre, groupe avec garde et parade", level, good, w);
        Assert.True(good.Won && good.Left * 10 >= Max(w), $"chapitre {chapter} au niveau {level} : trop dur en jouant bien ({good.Left}/{Max(w)} PV)");
        string c = Creatures[chapter - 1];
        WorldSession lone = Arena(Typical(level, 0), new[] { c }, placeMin: level);
        (long Left, bool Won) one = Fight(lone, new[] { c }, null);
        Say("début de chapitre, " + c + " seul sans défense", level, one, lone);
        Assert.True(one.Won && one.Left * 2 >= Max(lone));
    }

    /// <summary>
    /// « Le Gardien demande garde et parade », dans la vraie disposition du Seuil (relecture de la PR #83 : rien n'est calmé ; le rôdeur est
    /// hors de portée du Gardien) : au niveau 25, sans défense ni potion, il gagne ; avec garde, parades et cinq potions, le Bastion gagne avec
    /// au moins 10 % de ses PV.
    /// </summary>
    [Fact]
    public void Le_Gardien_demande_garde_et_parade()
    {
        WorldSession bare = Arena(Typical(25, 0), Array.Empty<string>(), boss: true);
        // Le rôdeur du Seuil est à plus de sa portée d'aggro du Gardien et de la place du combat.
        CombatantView rodeur = bare.Zone.Engine.View("rodeur"), gardien = bare.Zone.Engine.View(bare.BossForm!);
        Assert.True(Distance(rodeur, gardien) > 9 + 3, $"rôdeur à {Distance(rodeur, gardien):0.0} m du Gardien");
        (long Left, bool Won) loose = Fight(bare, new[] { "gardien_du_seuil" }, null);
        Say("Gardien sans défense ni potion", 25, loose, bare);
        Assert.False(loose.Won);
        Assert.Equal(0, loose.Left);
        WorldSession ready = Arena(Typical(25, 5), Array.Empty<string>(), boss: true);
        (long Left, bool Won) good = Fight(ready, new[] { "gardien_du_seuil" }, PilotProfile.Expert);
        Say("Gardien avec garde, parade et cinq potions", 25, good, ready);
        Assert.True(good.Won && good.Left * 10 >= Max(ready), $"Gardien : marge trop faible ({good.Left}/{Max(ready)} PV)");
    }

    /// <summary>
    /// Relecture de la PR #83 : le loup blanc (quête <c>quest.bete_blanche</c>, ouverte au niveau 3) reste une bête du chapitre 1 et le Borgne
    /// (contrat <c>contract.prime_du_borgne</c>, rang F) un chef du chapitre 2 (niveau propre de leur population), même dans la forêt des
    /// Loups (11) et le camp des rôdeurs (16) : un joueur expert avec trois potions les bat dès ces niveaux. L'épreuve E (le loup blanc, puis
    /// le Borgne) reste cohérente : au niveau 6, les deux se gagnent.
    /// </summary>
    [Theory]
    [InlineData("loup_blanc", 3)]
    [InlineData("loup_blanc", 6)]
    [InlineData("le_borgne", 6)]
    [InlineData("le_borgne", 8)]
    public void Creatures_rares_au_niveau_de_leur_chapitre(string creature, int level)
    {
        WorldSession w = Arena(Typical(level, 3), new[] { creature });
        ZonePopulation(w, creature, out int own);
        Assert.Equal(creature == "loup_blanc" ? 1 : 6, own);
        (long Left, bool Won) good = Fight(w, new[] { creature }, PilotProfile.Expert);
        Say(creature + " avec garde et parade", level, good, w);
        Assert.True(good.Won && good.Left > 0, $"{creature} au niveau {level} : perdu en jouant bien");
    }

    private static void ZonePopulation(WorldSession w, string creature, out int own) =>
        own = w.Definition!.PopulationOf(creature)!.Level ?? 0;
}
