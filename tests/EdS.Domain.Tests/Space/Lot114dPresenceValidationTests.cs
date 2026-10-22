using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Domain.Stats;
using EdS.Domain.Tests.Builds;

namespace EdS.Domain.Tests.Space;

/// <summary>
/// Sous-lot 11.4d : validation des arrivées en cours de combat (<c>arrivalMs</c>) et du départ (<c>leave</c>) : seul validateur, chemins de
/// la forme du fichier ; compte des événements initiaux et combat complété.
/// </summary>
[Trait("Category", "Presence")]
public class Lot114dPresenceValidationTests
{
    private static readonly CombatDataIdentity Identity = new(new DataFileIdentity("rules", "0.1.0-test", "sha256:r"), Array.Empty<DataFileIdentity>());

    private static CombatantPlacement At(string x, string z, string radius = "0.5") => new(GroundVector.Parse(x, z), Fixed.Parse(radius));

    private static CharacterSetup Hero(string id, long? arrivalMs = null, CombatantPlacement? placement = null) =>
        new(id, "joueurs", FictionalClass.Definition, FictionalClass.Build(1), placement: placement, arrivalMs: arrivalMs);

    private static AssailantSetup Ours(long? arrivalMs = null, CombatantPlacement? placement = null, long firstAttackMs = 1000) =>
        new("ours", "cibles", Fixed.Zero, Fixed.Zero, "bastion", firstAttackMs, 3000, new[] { new AssailantAttack("coup", 10, DamageType.Physique, SkillTargeting.Enemy, false, null) },
            placement: placement, arrivalMs: arrivalMs);

    private static CombatSetup Fight(IEnumerable<CombatantSetup> combatants, IEnumerable<CombatCommand>? commands = null, bool spatial = false) =>
        new(1, 10_000, StatRules.Provisional, DamageRules.Provisional, ProgressionRules.Provisional, CombatRules.Provisional, combatants,
            commands ?? Array.Empty<CombatCommand>(), Identity,
            spatial ? new CombatSpace(GroundVector.Parse("-10", "-10"), GroundVector.Parse("10", "10"), Array.Empty<SpaceObstacle>()) : null);

    private static string Only(IReadOnlyList<RuleViolation> errors) => Assert.Single(Assert.Single(errors).Fields);

    [Theory]
    [InlineData(0L)]
    [InlineData(50L)]
    [InlineData(9950L)]
    public void Arrivee_admise_au_debut_d_un_tick_du_combat(long arrivalMs) =>
        Assert.Empty(Fight(new CombatantSetup[] { Hero("bastion"), Hero("renfort", arrivalMs), Ours() }).Validate());

    [Theory]
    [InlineData(-50L)]
    [InlineData(10_000L)]   // la durée : après le dernier tick
    [InlineData(75L)]       // pas un multiple du tick
    public void Arrivee_refusee_hors_du_combat_ou_hors_tick(long arrivalMs) =>
        Assert.Equal("combatants[1].arrivalMs", Only(Fight(new CombatantSetup[] { Hero("bastion"), Hero("renfort", arrivalMs), Ours() }).Validate()));

    [Fact]
    public void Une_arrivee_n_est_pas_comparee_aux_places_du_depart()
    {
        // Deux créatures qui se chevauchent : refusé au départ ; si l'une arrive en route, c'est le moteur qui juge sa place à ce moment-là.
        var still = new CombatantSetup[] { Hero("bastion", placement: At("-5", "0")), Ours(placement: At("0", "0", "1")),
            new AssailantSetup("loup", "cibles", Fixed.Zero, Fixed.Zero, "bastion", 1000, 3000, new[] { new AssailantAttack("coup", 10, DamageType.Physique, SkillTargeting.Enemy, false, null) },
                placement: At("0.5", "0", "1")) };
        Assert.Equal("combatants[2].position", Only(Fight(still, spatial: true).Validate()));
        var arriving = still.Take(2).Append(new AssailantSetup("loup", "cibles", Fixed.Zero, Fixed.Zero, "bastion", 1000, 3000,
            new[] { new AssailantAttack("coup", 10, DamageType.Physique, SkillTargeting.Enemy, false, null) }, placement: At("0.5", "0", "1"), arrivalMs: 1000));
        Assert.Empty(Fight(arriving, spatial: true).Validate());
        // L'aire et le décor restent vérifiés : une arrivée hors de l'aire est refusée.
        var outside = still.Take(2).Append(Hero("renfort", 1000, At("9.8", "0")));
        Assert.Equal("combatants[2].position", Only(Fight(outside, spatial: true).Validate()));
    }

    [Fact]
    public void Depart_sans_competence_ni_cible_et_sans_espace()
    {
        Assert.Empty(Fight(new CombatantSetup[] { Hero("bastion"), Ours() }, new[] { new CombatCommand(1000, "bastion", CombatAction.Leave) }).Validate());
        Assert.Empty(Fight(new CombatantSetup[] { Hero("bastion"), Ours() }, new[] { new CombatCommand(1000, "ours", CombatAction.Leave) }).Validate());
        Assert.Equal("leave", CombatActionIds.Of(CombatAction.Leave));
        Assert.True(CombatActionIds.TryParseNamed("leave", out CombatAction leave) && leave == CombatAction.Leave);
        Assert.False(CombatActionIds.TryParseDefensive("leave", out _));   // pas transmis par le protocole du lot 10
    }

    [Fact]
    public void Evenements_initiaux_une_arrivee_compte_pour_son_premier_tour()
    {
        CombatSetup present = Fight(new CombatantSetup[] { Hero("bastion"), Ours() });
        CombatSetup arriving = Fight(new CombatantSetup[] { Hero("bastion"), Hero("renfort", 500), Ours(arrivalMs: 2000) });
        Assert.Equal(1, present.InitialEventCount());    // le premier tour de l'ours
        Assert.Equal(2, arriving.InitialEventCount());   // deux arrivées ; le premier tour de l'ours viendra à son arrivée
    }

    [Fact]
    public void Combat_complete_des_arrivees_et_des_commandes()
    {
        CombatSetup start = Fight(new CombatantSetup[] { Hero("bastion"), Ours() }, new[] { new CombatCommand(0, "bastion", CombatAction.GuardStart) });
        CombatSetup completed = start.Completed(new[] { Hero("renfort", 1000) }, new[] { new CombatCommand(1000, "renfort", CombatAction.GuardStart) });
        Assert.Equal(new[] { "bastion", "ours", "renfort" }, completed.Combatants.Select(c => c.Id));
        Assert.Equal(new[] { "bastion", "renfort" }, completed.Commands.Select(c => c.Actor));
        Assert.Equal((start.Seed, start.DurationMs, start.Space), (completed.Seed, completed.DurationMs, completed.Space));
        Assert.Empty(completed.Validate());
    }
}
