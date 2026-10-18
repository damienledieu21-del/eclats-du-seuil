using System.Text.Json;
using EdS.Domain;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Modifiers;

/// <summary>
/// Sous-lot 8.1, constats de la revue adversariale : rapport des erreurs de références au combat (plafond global signalé seul, aucun
/// modificateur signalé deux fois), auteur des durées pour un déclencheur porté par une altération, écrêtage à 24 h d'un DOT dont la
/// période ne divise pas 24 h, et blocage à coût nul (comportement du moteur 0.9.1 conservé).
/// </summary>
[Trait("Category", "Modifier")]
[Trait("Category", "Limits")]
public class Lot81ReviewTests
{
    private static CombatSetup Solo(ClassDefinition definition) =>
        Setup(new[] { Cmd(0, Frappe) }, new CombatantSetup[] { Character(definition: definition), Dummy() });

    /// <summary>Classe d'essai dont le premier arbre reçoit <paramref name="talents"/> talents de plus, à 8 modificateurs d'altérations inconnues chacun.</summary>
    private static ClassDefinition Crowded(int talents)
    {
        TalentTreeDefinition alpha = Essai.Trees[0];
        var crowded = new TalentTreeDefinition(alpha.Id, alpha.Name, alpha.Talents.Concat(Enumerable.Range(0, talents)
            .Select(t => new TalentDefinition($"talent.essai.alpha.t{t}", "T", 5,
                modifiers: Enumerable.Range(0, 8).Select(j => Mod($"status.x{t}n{j}.durationTaken", "100")).ToArray())))
            .ToArray());
        return new ClassDefinition("class.essai", "Essai", new[] { crowded }.Concat(Essai.Trees.Skip(1)).ToArray(), Essai.Skills);
    }

    [Fact]
    public void Plafond_global_des_modificateurs_signale_seul_au_combat()
    {
        // 65 × 8 = 520 modificateurs : la classe s'arrête à son plafond (512), et le combat n'examine pas leurs références (520
        // altérations inconnues) : une seule violation.
        RuleViolation violation = Assert.Single(Solo(Crowded(65)).Validate());
        Assert.Equal((RuleViolationKind.LimitExceeded, "combatants[0].class:skills"), (violation.Kind, Assert.Single(violation.Fields)));
        Assert.Contains("520 modificateurs", violation.Message);
        // Témoin : 64 × 8 = 512, au plafond, chaque altération inconnue est signalée.
        IReadOnlyList<RuleViolation> errors = Solo(Crowded(64)).Validate();
        Assert.True(errors.Count == 512, string.Join("\n", errors.Where(e => e.Kind != RuleViolationKind.UnknownReference)));
        Assert.All(errors, e => Assert.Equal(RuleViolationKind.UnknownReference, e.Kind));
    }

    [Fact]
    public void Modificateurs_d_une_active_signales_une_seule_fois()
    {
        // La classe refuse tout modificateur sur une active sans en examiner le contenu ; le combat n'y revient pas (l'altération
        // inconnue n'est pas signalée en plus).
        ClassDefinition definition = Class(modifiers: new Dictionary<string, ModifierDefinition[]> { [Frappe] = new[] { Mod("status.inconnue.durationTaken", "100") } });
        RuleViolation violation = Assert.Single(Solo(definition).Validate());
        Assert.Equal((RuleViolationKind.InvalidValue, "combatants[0].class:skills[0].modifiers"), (violation.Kind, Assert.Single(violation.Fields)));
    }

    [Fact]
    public void Liste_refusee_par_la_classe_non_reexaminee_au_combat_les_autres_le_sont()
    {
        // alpha : une valeur hors borne (classe) et une altération inconnue (combat) dans la même liste : seule l'erreur de classe.
        // beta : une altération inconnue seule, toujours signalée au combat (chemin interne dans le message, comme les déclencheurs).
        ClassDefinition definition = Class(modifiers: new Dictionary<string, ModifierDefinition[]>
        {
            // Décision du 18/10/2026 : la classe seule borne une réduction de garde à 1 (fraction) ; 1,1 reste refusé par la classe.
            [TalentAlpha] = new[] { Mod("status.inconnue.durationTaken", "100"), Mod("guard.damageReduction", "1.1") },
            [TalentBeta] = new[] { Mod("status.autre.durationTaken", "100") },
        });
        IReadOnlyList<RuleViolation> errors = Solo(definition).Validate();
        Assert.Equal(2, errors.Count);
        Assert.Equal((RuleViolationKind.InvalidValue, "combatants[0].class:trees[0].talents[0].modifiers[1].add"), (errors[0].Kind, Assert.Single(errors[0].Fields)));
        Assert.Equal((RuleViolationKind.UnknownReference, "combatants[0].class"), (errors[1].Kind, Assert.Single(errors[1].Fields)));
        Assert.Equal("classe « class.essai », trees[1].talents[0].modifiers[0].key : altération inconnue « autre ».", errors[1].Message);
    }

    [Fact]
    public void Rang_maximal_invalide_n_empeche_pas_l_examen_des_references()
    {
        // Contre-vérification 8.1 : un talent de rang maximal 0 est refusé sur .maxRank, sans erreur de valeur sur ses modificateurs ;
        // leur liste n'est donc pas « refusée par la classe » et le combat examine encore leurs références (altération inconnue).
        TalentTreeDefinition alpha = Essai.Trees[0];
        var zero = new TalentTreeDefinition(alpha.Id, alpha.Name, new[]
        {
            new TalentDefinition(alpha.Talents[0].Id, alpha.Talents[0].Name, 0, modifiers: new[] { Mod("status.inconnue.durationTaken", "100", "50") }),
        }.Concat(alpha.Talents.Skip(1)).ToArray());
        var definition = new ClassDefinition("class.essai", "Essai", new[] { zero }.Concat(Essai.Trees.Skip(1)).ToArray(), Essai.Skills);
        IReadOnlyList<RuleViolation> errors = Solo(definition).Validate();
        Assert.Equal(new[] { (RuleViolationKind.InvalidValue, "combatants[0].class:trees[0].talents[0].maxRank"), (RuleViolationKind.UnknownReference, "combatants[0].class") },
            errors.Select(e => (e.Kind, Assert.Single(e.Fields))));
        Assert.Equal("classe « class.essai », trees[0].talents[0].modifiers[0].key : altération inconnue « inconnue ».", errors[1].Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Debordement_des_statistiques_sans_modificateur_reste_rattache_aux_attributs(bool unrelatedModifier)
    {
        // Le même dépassement, causé par les attributs seuls, reste celui des attributs du build (avant 8.1 comme après), y compris
        // quand le personnage porte un modificateur permanent sans rapport (contre-vérification 8.1 : le chemin ne dépend pas de la
        // seule présence de modificateurs).
        var stats = new StatRules { MaxHealthPerVitalite = Fixed.FromInt(1_000_000_000) };
        ClassDefinition definition = unrelatedModifier
            ? Class(modifiers: new Dictionary<string, ModifierDefinition[]> { [Passif] = new[] { Mod("damageDealt", "0.1") } })
            : Essai;
        CharacterSetup character = Character(attributes: new Attributes(14, 8, 6, 1_000_000, 10, 0), definition: definition,
            passives: unrelatedModifier ? new[] { Passif } : null);
        var setup = new CombatSetup(42, 10_000, stats, DamageRules.Provisional, ProgressionRules.Provisional, CombatRules.Provisional,
            new CombatantSetup[] { character, Dummy() }, new[] { Cmd(0, Frappe) }, Identity);
        RuleViolation violation = Assert.Single(setup.Validate());
        Assert.Equal((RuleViolationKind.NumericLimit, "combatants[0].build.attributes", "statistiques dérivées hors des limites numériques."),
            (violation.Kind, Assert.Single(violation.Fields), violation.Message));
    }

    [Theory]
    [InlineData(false, 80)]    // gel de base : 4 s = 80 ticks
    [InlineData(true, 100)]    // + 1 000 ms du porteur de « epine »
    public void Duree_infligee_par_le_porteur_d_une_alteration_a_declencheur(bool modifier, long ticks)
    {
        // D10 : l'auteur direct d'un effet applyStatus de déclencheur est le porteur du déclencheur. Pour un déclencheur porté par une
        // altération, c'est le combattant qui porte l'altération (ici le Bastion, à qui l'assaillant a posé « epine »), pas celui qui l'a
        // posée : le gel que « epine » renvoie sur l'assaillant reçoit le durationDealt du Bastion.
        var epine = new StatusDefinition("epine", StatusCategory.Modifier, 10_000, StatusStacking.Refresh,
            triggers: new[] { new TriggerDefinition(TriggerEvent.HitTaken, new[] { TriggerEffect.ApplyStatus(TriggerTarget.EventSource, "freeze") }, TriggerRole.Target) });
        var rules = new CombatRules { GuardUpkeepPerSecond = 0, Statuses = new StatusCatalog(StatusCatalog.Provisional.Definitions.Append(epine)) };
        Dictionary<string, ModifierDefinition[]>? mods = modifier
            ? new Dictionary<string, ModifierDefinition[]> { [Passif] = new[] { Mod("status.freeze.durationDealt", "1000") } }
            : null;
        CharacterSetup bastion = Character(attributes: Attrs(vitalite: 200), definition: Class(modifiers: mods), passives: new[] { Passif });
        AssailantSetup assailant = Assailant(attacks: Strike("marque", 10, applies: Apply("epine")));
        var (result, log) = Run(Setup(Array.Empty<CombatCommand>(), new CombatantSetup[] { bastion, assailant }, durationMs: 6000, rules: rules));
        Assert.NotEqual(CombatEndReason.Aborted, result.EndReason);
        JsonElement freeze = Events(log).OfKind("stateApplied").First(e => e.Str("state") == "freeze" && e.Str("combatant") == "assaillant");
        Assert.Equal(("bastion", ticks), (freeze.Str("source")!, freeze.Int("untilTick") - freeze.Tick()));
        Assert.StartsWith("epine/0@bastion#", freeze.Str("trigger"));
    }

    [Fact]
    public void Dot_ecrete_a_24_h_aligne_sur_sa_periode()
    {
        // Période de 7 s (140 ticks), qui ne divise pas 24 h. 7 000 + 86 394 000 (multiple de la période) dépasse 24 h : la durée est
        // écrêtée à 86 400 000 ms = 1 728 000 ticks, puis la fin est ramenée au dernier tick périodique couvert (correctif 7.2) :
        // prochain tick + ⌊(1 728 000 − 140) / 140⌋ × 140 = 140 + 12 341 × 140 = 1 727 880 ticks après l'application.
        var poison = new StatusDefinition("poison", StatusCategory.Dot, 7000, StatusStacking.PerSourceKeepStronger, StatusResistanceMode.Defense,
            periodMs: 7000, damageType: DamageType.Physique);
        var rules = new CombatRules { GuardUpkeepPerSecond = 0, Statuses = new StatusCatalog(StatusCatalog.Provisional.Definitions.Append(poison)) };
        var mods = new Dictionary<string, ModifierDefinition[]> { [Passif] = new[] { Mod("status.poison.durationTaken", "86394000") } };
        CharacterSetup bastion = Character(attributes: Attrs(vitalite: 200), definition: Class(modifiers: mods), passives: new[] { Passif });
        AssailantSetup assailant = Assailant(intervalMs: 60_000, attacks: Strike("piqure", 10, applies: Apply("poison", "0.1")));
        var (result, log) = Run(Setup(Array.Empty<CombatCommand>(), new CombatantSetup[] { bastion, assailant }, durationMs: 2000, rules: rules));
        Assert.NotEqual(CombatEndReason.Aborted, result.EndReason);
        JsonElement applied = Events(log).OfKind("stateApplied").Single(e => e.Str("state") == "poison");
        Assert.Equal((140L, 1_727_880L), (applied.Int("nextTick") - applied.Tick(), applied.Int("untilTick") - applied.Tick()));
    }

    [Fact]
    public void Blocage_a_cout_nul_relance_le_delai_de_regeneration_comme_en_0_9_1()
    {
        // Comportement du moteur 0.9.1 conservé (journaux figés de Lot81EngineBaselineTests, coût de blocage 0) : un blocage, même
        // gratuit, relance le délai de régénération ; seul un blocage affaibli sous guard.noUpkeepBreak qui ne paie rien ne le relance
        // pas (D10). Un coût ramené à 0 par modificateur se comporte exactement comme un coût de base à 0.
        var baseRules = new CombatRules { GuardUpkeepPerSecond = 0, EnduranceRegenDelayMs = 1000 };
        var zeroRules = new CombatRules { GuardUpkeepPerSecond = 0, EnduranceRegenDelayMs = 1000, GuardCostPerBlock = 0 };
        var mods = new Dictionary<string, ModifierDefinition[]> { [Passif] = new[] { Mod("guard.costPerBlock", "-10") } };
        var commands = new[] { Cmd(0, Frappe, target: "assaillant"), GuardStart(50), GuardStop(2050) };
        long FirstRegen(CombatRules rules, Dictionary<string, ModifierDefinition[]>? modifiers, bool attacked)
        {
            CharacterSetup bastion = Character(attributes: Attrs(vitalite: 200), definition: Class(modifiers: modifiers), passives: new[] { Passif });
            CombatantSetup other = attacked ? Assailant(2000, 60_000) : Assailant(60_000, 60_000);
            var (_, log) = Run(Setup(commands, new CombatantSetup[] { bastion, other }, durationMs: 5000, rules: rules));
            List<JsonElement> events = Events(log);
            if (attacked) Assert.Equal(0L, events.OfKind("blocked").Single().Int("enduranceCost"));
            return events.OfKind("enduranceRegenerated").First().Tick();
        }
        long withModifier = FirstRegen(baseRules, mods, true);
        Assert.Equal(FirstRegen(zeroRules, null, true), withModifier);
        Assert.True(withModifier > FirstRegen(baseRules, mods, false));
    }
}
