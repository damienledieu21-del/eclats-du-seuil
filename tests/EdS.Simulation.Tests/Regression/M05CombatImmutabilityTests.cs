using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;
using EdS.Simulation.Tests.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Regression;

/// <summary>
/// Correctif 6.1, M05 côté combat : une description de combat copie ses collections. Les modifier après construction,
/// après validation ou pendant la simulation ne change ni la validation, ni le journal, ni le résultat.
/// </summary>
[Trait("Category", "Engine")]
[Trait("Category", "Regression")]
public class M05CombatImmutabilityTests
{
    private sealed class Sources
    {
        public readonly List<CombatantSetup> Combatants;
        public readonly List<CombatCommand> Commands;
        public readonly AssailantAttack[] Attacks;
        public readonly Dictionary<string, int> Talents = new() { ["talent.essai.alpha.a"] = 1 };
        public readonly List<string> Actives = new() { Frappe, Heurt, Onde, Gratuite };
        public readonly TalentTreeDefinition[] Trees;
        public readonly List<SkillDefinition> Skills;

        public Sources()
        {
            Attacks = new[] { Blow(fragility: true, id: "fele"), Blow(id: "coup") };
            Trees = Essai.Trees.ToArray();
            Skills = Essai.Skills.ToList();
            var definition = new ClassDefinition("class.essai", "Essai", Trees, Skills);
            var build = new CharacterBuild("class.essai", 10, Attrs(vitalite: 200), Talents, Actives, Ultime);
            Combatants = new List<CombatantSetup>
            {
                new CharacterSetup("bastion", "joueurs", definition, build),
                new AssailantSetup("assaillant", "cibles", default, default, "bastion", 1000, 2000, Attacks),
                Dummy(),
            };
            Commands = new List<CombatCommand> { GuardStart(0), GuardStop(1500), Cmd(2000, Frappe), Parry(3000), Cmd(5000, Onde, target: null) };
        }

        public CombatSetup Build() => Setup(Commands, Combatants);

        /// <summary>Tout ce qu'un appelant pourrait modifier après coup, y compris les collections imbriquées.</summary>
        public void MutateEverything()
        {
            Combatants.RemoveAt(2);
            Combatants.Add(new DummySetup("intrus", "cibles", Fixed.Zero, Fixed.Zero));
            Commands.Clear();
            Commands.Add(Cmd(0, Ruineuse));
            Attacks[0] = Blow(CombatantStats.MaxRawDamage, id: "enorme");
            Talents["talent.essai.alpha.a"] = int.MaxValue;
            Actives[0] = Ruineuse;
            Trees[0] = new TalentTreeDefinition("tree.essai.intrus", "Intrus", new[] { new TalentDefinition("talent.essai.intrus.a", "A", 1) });
            Skills.Clear();
        }
    }

    private static string Reference() => Run(new Sources().Build()).Log.ToString();

    [Fact]
    public void Mutation_apres_construction()
    {
        var sources = new Sources();
        CombatSetup setup = sources.Build();
        sources.MutateEverything();
        Assert.Empty(setup.Validate());
        Assert.Equal(3, setup.Combatants.Count);
        Assert.Equal(5, setup.Commands.Count);
        Assert.Equal(Reference(), Run(setup).Log.ToString());
    }

    [Fact]
    public void Mutation_apres_validation()
    {
        var sources = new Sources();
        CombatSetup setup = sources.Build();
        Assert.Empty(setup.Validate());
        sources.MutateEverything();
        Assert.Empty(setup.Validate());
        Assert.Equal(Reference(), Run(setup).Log.ToString());
    }

    [Fact]
    public void Mutation_pendant_la_simulation()
    {
        var sources = new Sources();
        CombatSetup setup = sources.Build();
        var log = new MemoryCombatLog();
        var engine = new CombatEngine(setup, log);
        for (int i = 0; i < 30; i++) engine.Step();
        sources.MutateEverything();
        while (engine.Step()) { }
        Assert.Equal(Reference(), log.ToString());
    }

    [Fact]
    public void Transtypage_des_vues_exposees_impossible()
    {
        CombatSetup setup = new Sources().Build();
        Assert.False(setup.Combatants is CombatantSetup[]);
        Assert.Throws<NotSupportedException>(() => ((IList<CombatCommand>)setup.Commands)[0] = Cmd(0, Ruineuse));
        Assert.Throws<NotSupportedException>(() => ((IList<AssailantAttack>)((AssailantSetup)setup.Combatants[1]).Attacks).Clear());
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, int>)((CharacterSetup)setup.Combatants[0]).Build.TalentRanks)["x"] = 1);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)CombatActionIds.Defensive)[0] = "intrus");
        Assert.Throws<NotSupportedException>(() => ((IList<int>)ProgressionRules.Provisional.ActiveSlotLevels)[0] = 30);

        var (result, log) = Run(setup);
        Assert.Throws<NotSupportedException>(() => ((IList<CombatantResult>)result.Combatants).RemoveAt(0));
        Assert.False(log.Lines is List<string>);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)log.Lines).Add("{}"));
    }

    [Fact]
    public void Un_meme_setup_rejoue_a_l_identique_apres_mutations_repetees()
    {
        var sources = new Sources();
        CombatSetup setup = sources.Build();
        string first = Run(setup).Log.ToString();
        sources.MutateEverything();
        sources.MutateEverything();
        Assert.Equal(first, Run(setup).Log.ToString());
        Assert.Equal(Reference(), first);
    }
}
