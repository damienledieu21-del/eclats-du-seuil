using System.Text.Json;
using EdS.Domain;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;
using EdS.TestSupport;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Engine;

/// <summary>
/// Sous-lot V1.8d (décision de Damien : effet immédiat des bonus) : commande « bonus modifié » (<c>gear</c>) du moteur. Traitée au début de
/// son tick (phase <c>setup</c>), refus atomiques, règle proportionnelle des PV et de l'endurance, précontrôle numérique sur l'enveloppe des
/// bonus, journal au format 21 rejoué octet par octet, instantané au format 4.
/// </summary>
[Trait("Category", "Engine")]
public class V18dGearChangeTests
{
    private static readonly GearBonus Entry = new(armor: 20, maxHealth: 100);

    private static CharacterSetup Hero(GearBonus? gear = null, string id = "bastion", Attributes? attributes = null) =>
        new(id, "joueurs", Essai, new CharacterBuild("class.essai", 10, attributes ?? Attrs(vitalite: 20, endurance: 10), null,
            new[] { Frappe, Heurt, Onde, Gratuite }, Ultime), gear: gear ?? Entry);

    private static CombatCommand Gear(long atMs, GearBonus gear, string actor = "bastion") => CombatCommand.SetGear(atMs, actor, gear);

    private static CombatSetup With(IEnumerable<CombatCommand> commands, CombatantSetup? hero = null, long durationMs = 10_000, CombatRules? rules = null,
        StatRules? stats = null, params CombatantSetup[] others) =>
        new(42, durationMs, stats ?? StatRules.Provisional, DamageRules.Provisional, ProgressionRules.Provisional, rules ?? CombatRules.Provisional,
            new[] { hero ?? Hero() }.Concat(others.Length == 0 ? new CombatantSetup[] { Dummy() } : others), commands, Identity);

    private static DerivedStats StatsWith(GearBonus gear) => CombatantStats.ForCharacter(StatRules.Provisional, Hero().Build, ModifierSet.For(Hero()), gear);

    private static long MaxHp(GearBonus gear) => StatsWith(gear).MaxHealth.RoundToInt(EdS.Domain.Numerics.RoundingMode.Floor);

    // --- Effet, phase, journal -----------------------------------------------------------------------------------------------------

    [Fact]
    public void Le_bonus_change_au_debut_de_son_tick_avant_tout_le_reste()
    {
        var more = new GearBonus(physicalPower: 50, armor: 20, maxHealth: 140);
        // Une frappe au même tick que le bonus modifié : ses dégâts comptent déjà le nouveau bonus (phase setup avant les commandes).
        var (_, log) = Run(With(new[] { Gear(1000, more), Cmd(1000, Gratuite) }));
        var (_, plain) = Run(With(new[] { Cmd(1000, Gratuite) }));
        List<JsonElement> events = Events(log);
        JsonElement changed = Assert.Single(events.OfKind("gearChanged"));
        Assert.Equal((20L, "setup"), (changed.Tick(), changed.Str("phase")));
        Assert.Equal(50, changed.GetProperty("gear").GetProperty("physicalPower").GetInt32());
        Assert.False(changed.GetProperty("gear").TryGetProperty("agilePower", out _));   // champs non nuls seulement
        Assert.Equal(MaxHp(more), changed.Int("maxHealth"));
        // Avant les expirations et les commandes de son tick : la première ligne du tick 20.
        Assert.Equal("gearChanged", events.First(e => e.Tick() == 20).Str("event"));
        long hit = events.OfKind("damage").First().Int("amount"), hitPlain = Events(plain).OfKind("damage").First().Int("amount");
        Assert.True(hit > hitPlain, $"{hit} contre {hitPlain}");
    }

    [Fact]
    public void Vue_du_pilote_le_bonus_en_vigueur_des_le_tick_suivant()
    {
        var more = new GearBonus(physicalPower: 7, armor: 20, maxHealth: 140);
        var engine = new CombatEngine(With(Array.Empty<CombatCommand>()), NullCombatLog.Instance);
        for (int i = 0; i < 5; i++) engine.Step();
        Assert.Equal(Entry, engine.View("bastion").Gear);
        Assert.Null(engine.View("mannequin").Gear);
        engine.Submit(Gear(engine.CurrentTick * 50, more));
        Assert.Equal(Entry, engine.View("bastion").Gear);   // pas encore simulé
        engine.Step();
        CombatantView me = engine.View("bastion");
        Assert.Equal(more, me.Gear);
        Assert.Equal(MaxHp(more), me.MaxHealth);
        Assert.Equal(me.MaxHealth, me.Health);   // pleine : reste pleine
    }

    [Fact]
    public void Remplacement_un_seul_changement_sans_trou_ni_double_compte()
    {
        // Élixir de vigueur (+40 PV) remplacé par l'élixir de garde (+25 armure, +10 résistance) : le total change d'un coup.
        var vigueur = Entry + new GearBonus(maxHealth: 40);
        var garde = Entry + new GearBonus(armor: 25, statusResistance: 10);
        var (_, log) = Run(With(new[] { Gear(1000, vigueur), Gear(2000, garde) }));
        List<JsonElement> changes = Events(log).OfKind("gearChanged").ToList();
        Assert.Equal(new[] { 20L, 40L }, changes.Select(e => e.Tick()));
        JsonElement second = changes[1].GetProperty("gear");
        Assert.Equal((45, 100, 10), (second.GetProperty("armor").GetInt32(), second.GetProperty("maxHealth").GetInt32(), second.GetProperty("statusResistance").GetInt32()));
        Assert.Equal(MaxHp(garde), changes[1].Int("maxHealth"));
        Assert.Equal(MaxHp(Entry), MaxHp(garde));   // le +40 PV est parti, sans rien de plus
    }

    // --- Règle proportionnelle -----------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(100, 100, 140, 140)]   // pleine : pleine
    [InlineData(50, 100, 140, 70)]
    [InlineData(70, 140, 100, 50)]
    [InlineData(1, 100, 10, 1)]        // un vivant garde 1 PV
    [InlineData(0, 100, 140, 0)]
    [InlineData(99, 100, 50, 49)]      // arrondi par défaut
    public void Regle_proportionnelle(long value, long max, long newMax, long expected) =>
        Assert.Equal(expected, CombatEngine.Rescale(value, max, newMax, keepOne: true));

    [Fact]
    public void Endurance_proportionnelle_sans_plancher_et_maximum_nul()
    {
        Assert.Equal(0, CombatEngine.Rescale(0, 100, 140, keepOne: false));
        Assert.Equal(0, CombatEngine.Rescale(1, 100, 10, keepOne: false));
        Assert.Equal(25, CombatEngine.Rescale(0, 0, 25, keepOne: false));   // maximum nul : pleine
    }

    [Theory]
    [InlineData(1, 100, 140)]
    [InlineData(37, 100, 140)]
    [InlineData(99, 100, 141)]
    [InlineData(5000, 9999, 1_000_000)]
    public void Propriete_un_aller_retour_ne_rend_rien(long value, long max, long newMax)
    {
        long up = CombatEngine.Rescale(value, max, newMax, keepOne: true);
        long back = CombatEngine.Rescale(up, newMax, max, keepOne: true);
        Assert.InRange(up, 1, newMax);
        Assert.InRange(back, Math.Max(1, value - 1), value);   // au plus un point perdu à l'arrondi, jamais un point gagné
    }

    [Fact]
    public void Propriete_bornes_sur_un_echantillon()
    {
        var random = new Random(7);
        for (int i = 0; i < 5000; i++)
        {
            long max = random.Next(1, 2_000_000), newMax = random.Next(1, 2_000_000), value = random.Next(0, (int)max + 1);
            long scaled = CombatEngine.Rescale(value, max, newMax, keepOne: true);
            Assert.InRange(scaled, value > 0 ? 1 : 0, newMax);
            if (value == max) Assert.Equal(newMax, scaled);
            if (newMax >= max) Assert.True(scaled >= value);
            else Assert.True(scaled <= value);
        }
    }

    [Fact]
    public void PV_entames_gardent_leur_part_et_la_fin_ne_tue_pas()
    {
        var more = Entry + new GearBonus(maxHealth: 2000);
        // Assaillant qui frappe fort : PV entamés, puis le bonus monte, puis il redescend.
        var hero = Hero();
        var (_, log) = Run(With(new[] { Gear(3000, more), Gear(6000, Entry) }, hero, others: Assailant(firstAttackMs: 500, intervalMs: 1000, attacks: Blow(80))));
        List<JsonElement> events = Events(log);
        JsonElement up = events.OfKind("gearChanged").First(), down = events.OfKind("gearChanged").Last();
        long hpBefore = events.Where(e => e.Tick() < 60).OfKind("damage").Last().Int("health");
        long expectedUp = CombatEngine.Rescale(hpBefore, MaxHp(Entry), MaxHp(more), keepOne: true);
        Assert.Equal(expectedUp, up.Int("health"));
        Assert.True(down.Int("health") >= 1);
        Assert.True(down.Int("health") <= down.Int("maxHealth"));
        Assert.Equal(MaxHp(Entry), down.Int("maxHealth"));
    }

    // --- Refus atomiques --------------------------------------------------------------------------------------------------------

    [Fact]
    public void Refuse_a_un_mort_sans_rien_changer()
    {
        var weak = Hero(attributes: Attrs(vitalite: 1));
        // Un allié reste debout : le combat continue après la mort du personnage.
        var (result, log) = Run(With(new[] { Gear(5000, Entry + new GearBonus(maxHealth: 500)) }, weak,
            others: new CombatantSetup[] { Hero(id: "allie", attributes: Attrs(vitalite: 200)), Assailant(firstAttackMs: 500, intervalMs: 500, attacks: Blow(100_000)) }));
        List<JsonElement> events = Events(log);
        Assert.Empty(events.OfKind("gearChanged"));
        JsonElement rejected = Assert.Single(events.OfKind("commandRejected"), e => e.Str("action") == "gear");
        Assert.Equal(("actorDead", "setup"), (rejected.Str("reason"), rejected.Str("phase")));
        Assert.Equal(1, result.Combatants.Single(c => c.Id == "bastion").CommandsRejected);
    }

    [Fact]
    public void Refuse_si_les_PV_maximaux_tomberaient_sous_un()
    {
        // Règles d'essai sans PV de base ni par vitalité : seuls les 100 PV du bonus d'entrée ; un bonus modifié à 0 PV est refusé.
        var stats = new StatRules { BaseMaxHealth = EdS.Domain.Numerics.Fixed.Zero, MaxHealthPerVitalite = EdS.Domain.Numerics.Fixed.Zero };
        var setup = With(new[] { Gear(1000, new GearBonus(armor: 20)) }, stats: stats);
        Assert.Empty(setup.Validate());
        var (_, log) = Run(setup);
        JsonElement rejected = Assert.Single(Events(log).OfKind("commandRejected"));
        Assert.Equal("maxHealthTooLow", rejected.Str("reason"));
        Assert.Empty(Events(log).OfKind("gearChanged"));
    }

    [Fact]
    public void Etourdi_ou_en_garde_le_bonus_change_quand_meme()
    {
        var (_, log) = Run(With(new[] { GuardStart(500), Gear(1000, Entry + new GearBonus(armor: 5)) }, others: Assailant()));
        Assert.Single(Events(log).OfKind("gearChanged"));
    }

    // --- Validation ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Validation_bornes_acteur_et_forme()
    {
        IReadOnlyList<RuleViolation> errors = With(new[] { Gear(500, new GearBonus(armor: -1)), Gear(500, new GearBonus(maxHealth: GearBonus.MaxValue + 1)),
            Gear(500, Entry, actor: "mannequin") }).Validate();
        Assert.Contains(errors, e => e.Fields.Contains("commands[0].gear.armor") && e.Kind == RuleViolationKind.NumericLimit);
        Assert.Contains(errors, e => e.Fields.Contains("commands[1].gear.maxHealth"));
        Assert.Contains(errors, e => e.Fields.Contains("commands[2].actor"));
        Assert.DoesNotContain(With(new[] { Gear(500, new GearBonus(maxHealth: GearBonus.MaxValue)) }).Validate(), e => e.Fields.Any(f => f.StartsWith("commands", StringComparison.Ordinal)));
        Assert.Throws<ArgumentException>(() => new CombatCommand(0, "bastion", CombatAction.Gear));
        Assert.Throws<ArgumentNullException>(() => CombatCommand.SetGear(0, "bastion", null!));
    }

    [Fact]
    public void Precontrole_sur_l_enveloppe_des_bonus()
    {
        var hero = Hero(new GearBonus(maxHealth: 10));
        var setup = With(new[] { Gear(500, new GearBonus(physicalPower: 300, maxHealth: 5)), Gear(1000, new GearBonus(armor: 40)) }, hero);
        Assert.Equal(new GearBonus(physicalPower: 300, armor: 40, maxHealth: 10), setup.WorstGear(hero));
        // Une puissance à la limite du combat partout : le précontrôle la refuse si elle déborde, même venue d'un bonus modifié.
        var huge = new GearBonus(physicalPower: GearBonus.MaxValue, agilePower: GearBonus.MaxValue, magicPower: GearBonus.MaxValue, armor: GearBonus.MaxValue,
            magicResistance: GearBonus.MaxValue, maxHealth: GearBonus.MaxValue, maxEndurance: GearBonus.MaxValue, statusResistance: GearBonus.MaxValue);
        IReadOnlyList<RuleViolation> atEntry = With(Array.Empty<CombatCommand>(), Hero(huge)).Validate();
        IReadOnlyList<RuleViolation> later = With(new[] { Gear(500, huge) }, Hero(GearBonus.None)).Validate();
        Assert.Equal(atEntry.Select(e => e.Kind), later.Select(e => e.Kind));
    }

    // --- Pilote --------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Pilote_refus_atomiques()
    {
        var engine = new CombatEngine(With(Array.Empty<CombatCommand>()), NullCombatLog.Instance);
        engine.Step();
        long at = engine.CurrentTick * 50;
        string before = engine.Snapshot().Text;
        Assert.Throws<ArgumentException>(() => engine.Submit(Gear(at, new GearBonus(armor: -1))));
        Assert.Throws<ArgumentException>(() => engine.Submit(Gear(at, new GearBonus(armor: GearBonus.MaxValue + 1))));
        Assert.Throws<ArgumentException>(() => engine.Submit(Gear(at, Entry, actor: "mannequin")));
        Assert.Throws<ArgumentException>(() => engine.Submit(Gear(at + 50, Entry)));
        Assert.Equal(before, engine.Snapshot().Text);
        Assert.Empty(engine.SubmittedCommands);
        engine.Submit(Gear(at, Entry + new GearBonus(armor: 1)));
        Assert.Single(engine.SubmittedCommands);
    }

    // --- Rejeu, instantané -------------------------------------------------------------------------------------------------------

    [Fact]
    public void Combat_pilote_rejoue_octet_par_octet()
    {
        var first = new MemoryCombatLog();
        var engine = new CombatEngine(With(Array.Empty<CombatCommand>(), others: Assailant()), first);
        var gears = new[] { Entry + new GearBonus(maxHealth: 40), Entry + new GearBonus(armor: 25, statusResistance: 10), Entry };
        for (int t = 0; engine.Step(); t++)
            if (t % 37 == 36 && t / 37 < gears.Length) engine.Submit(Gear(engine.CurrentTick * 50, gears[t / 37]));
        var (_, again) = Run(engine.CompletedSetup());
        Assert.Equal(first.Lines.Skip(1), again.Lines.Skip(1));
        Assert.Equal(3, Events(again).OfKind("gearChanged").Count());
    }

    [Fact]
    public void Instantane_garde_le_bonus_en_vigueur_et_reprend_a_l_identique()
    {
        var more = Entry + new GearBonus(physicalPower: 30, maxHealth: 900, maxEndurance: 50);
        CombatSetup setup = With(new[] { Gear(1500, more), Cmd(2500, Gratuite, target: "assaillant") }, others: Assailant(firstAttackMs: 500, intervalMs: 500, attacks: Blow(20)));
        var full = new MemoryCombatLog();
        CombatEngine.Run(setup, full);
        var log = new MemoryCombatLog();
        var engine = new CombatEngine(setup, log);
        while (engine.CurrentTick < 40) engine.Step();
        CombatSnapshot snapshot = engine.Snapshot();
        Assert.Contains("\"gear\":[30,0,0,20,0,1000,50,0]", snapshot.Text);
        Assert.StartsWith("{\"snapshot\":4,", snapshot.Text);
        var resumedLog = new MemoryCombatLog();
        CombatEngine resumed = CombatEngine.RestoreVerified(engine.CompletedSetup(), snapshot, resumedLog);
        Assert.Equal(more, resumed.View("bastion").Gear);
        Assert.Equal(engine.View("bastion").MaxHealth, resumed.View("bastion").MaxHealth);
        while (resumed.Step()) { }
        while (engine.Step()) { }
        Assert.Equal(log.Lines.Skip(log.Lines.Count - resumedLog.Lines.Count + 1), resumedLog.Lines.Skip(1));
    }

    [Fact]
    public void Instantane_retouche_refuse()
    {
        CombatSetup setup = With(new[] { Gear(1500, Entry + new GearBonus(maxHealth: 40)) }, others: Assailant(firstAttackMs: 500, intervalMs: 500, attacks: Blow(20)));
        var engine = new CombatEngine(setup, NullCombatLog.Instance);
        while (engine.CurrentTick < 40) engine.Step();
        string text = engine.Snapshot().Text;
        // Un bonus qu'aucune commande n'a donné, puis une variation des PV manquants sans bonus modifié.
        string forged = text.Replace("\"gear\":[0,0,0,20,0,140,0,0]", "\"gear\":[0,0,0,20,0,141,0,0]");
        Assert.NotEqual(text, forged);
        Assert.Throws<FormatException>(() => CombatEngine.Restore(engine.CompletedSetup(), CombatSnapshot.Parse(forged), NullCombatLog.Instance));
        Assert.Throws<FormatException>(() => CombatEngine.Restore(engine.CompletedSetup(), CombatSnapshot.Parse(text.Replace("\"gear\":[0,0,0,20,0,140,0,0]", "\"gear\":[0,0,0,-1,0,140,0,0]")), NullCombatLog.Instance));
        Assert.Throws<FormatException>(() => CombatEngine.Restore(engine.CompletedSetup(), CombatSnapshot.Parse(text.Replace("\"gear\":null", "\"gear\":[0,0,0,0,0,0,0,0]")), NullCombatLog.Instance));
    }
}
