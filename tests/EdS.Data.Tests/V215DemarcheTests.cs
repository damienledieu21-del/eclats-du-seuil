using System;
using System.Linq;
using EdS.Runtime;
using Xunit;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.15 (démarche réaliste) : règles pures de l'allure affichée (<see cref="Locomotion"/>) : cadence réglée sur la vitesse,
/// mélange marche ↔ course, lissages, virages, allures des bêtes, attentes et regards désynchronisés, pieds sur le relief. Affichage seulement.
/// </summary>
[Trait("Category", "Runtime")]
public sealed class V215DemarcheTests
{
    [Fact]
    public void Vitesses_naturelles_plausibles_et_croissantes()
    {
        double walk = Locomotion.NaturalSpeed(LocomotionGait.Walk, 1.8, false);
        double run = Locomotion.NaturalSpeed(LocomotionGait.Trot, 1.8, false);
        double sprint = Locomotion.NaturalSpeed(LocomotionGait.Gallop, 1.8, false);
        Assert.InRange(walk, 1.2, 1.6);   // un humain marche vers 1,4 m/s
        Assert.InRange(run, 3.2, 4.2);
        Assert.True(sprint > run && run > walk);
        // Un cheval (1,6 m) marche plus vite qu'un lièvre (0,35 m) ; la vitesse mesurée l'emporte si elle est plausible.
        Assert.True(Locomotion.NaturalSpeed(LocomotionGait.Walk, 1.6, true) > Locomotion.NaturalSpeed(LocomotionGait.Walk, 0.35, true));
        Assert.Equal(1.7, Locomotion.NaturalSpeed(LocomotionGait.Walk, 1.8, false, 1.7));
        Assert.Equal(walk, Locomotion.NaturalSpeed(LocomotionGait.Walk, 1.8, false, 0.05));
        Assert.Equal(0.0, Locomotion.NaturalSpeed(LocomotionGait.Stand, 1.8, false));
    }

    [Fact]
    public void Cadence_suit_la_vitesse_et_reste_bornee()
    {
        Assert.Equal(1.0, Locomotion.PlaybackRate(1.4, 1.4), 6);
        Assert.Equal(0.5, Locomotion.PlaybackRate(0.1, 1.4));
        Assert.Equal(1.6, Locomotion.PlaybackRate(9.0, 1.4));
        Assert.Equal(1.0, Locomotion.PlaybackRate(3.0, 0.0));
        Assert.Equal(1.0, Locomotion.PlaybackRate(double.NaN, 1.4));
    }

    [Fact]
    public void Melange_marche_course_synchronise_les_pas()
    {
        Assert.Equal(0.0, Locomotion.RunBlend(1.0, 1.4, 3.7));
        Assert.Equal(1.0, Locomotion.RunBlend(5.0, 1.4, 3.7));
        Assert.Equal(0.5, Locomotion.RunBlend(2.55, 1.4, 3.7), 6);
        // Mi-chemin : les deux cycles (1,1 s et 0,7 s) durent alors 0,9 s ; à la vitesse naturelle du mélange, cadence d'ensemble 1.
        (double w, double r) = Locomotion.BlendRates(1.1, 0.7, 0.5, 2.55, 1.4, 3.7);
        Assert.Equal(1.1 / w, 0.7 / r, 6);   // même durée de cycle : les pieds tombent ensemble
        Assert.Equal(0.9, 1.1 / w, 6);
    }

    [Fact]
    public void Lissages_demarrage_doux_arret_vif()
    {
        double up = Locomotion.Ease(0.0, 2.0, 0.2, 0.05, 0.05);
        double down = Locomotion.Ease(2.0, 0.0, 0.2, 0.05, 0.05);
        Assert.True(up > 0.0 && up < 2.0);
        Assert.True(2.0 - down > up);   // la baisse est plus rapide que la hausse
        Assert.Equal(1.5, Locomotion.Ease(1.5, 9.0, 0.2, 0.05, 0.0));
        // Indépendant de la cadence d'images : deux pas de 25 ms valent un pas de 50 ms.
        double twice = Locomotion.Ease(Locomotion.Ease(0.0, 2.0, 0.2, 0.05, 0.025), 2.0, 0.2, 0.05, 0.025);
        Assert.Equal(up, twice, 9);
        Assert.Equal(0.5, Locomotion.Accelerate(0.0, 2.0, 1.0, 3.0, 0.5));
        Assert.Equal(0.5, Locomotion.Accelerate(2.0, 0.0, 1.0, 3.0, 0.5));
        Assert.Equal(0.0, Locomotion.ArriveSpeed(1.2, 0.0, 2.0));
        Assert.Equal(1.2, Locomotion.ArriveSpeed(1.2, 10.0, 2.0));
    }

    [Fact]
    public void Virages_bornes_sans_demi_tour_instantane()
    {
        double yaw = 0.0;
        for (int i = 0; i < 5; i++) yaw = Locomotion.TurnStep(yaw, 180.0, 360.0, 0.0, 0.05);
        Assert.Equal(90.0, Math.Abs(yaw), 6);   // 0,25 s à 360°/s : un quart de tour, pas un demi
        Assert.Equal(-170.0, Locomotion.TurnStep(170.0, -170.0, 1000.0, 0.0, 1.0), 6);   // par le plus court, à travers ±180
        Assert.True(Locomotion.MaxTurnRate(0.0, false) > Locomotion.MaxTurnRate(5.0, false));
        Assert.True(Locomotion.MaxTurnRate(1.0, true) < Locomotion.MaxTurnRate(1.0, false));
        Assert.Equal(1.0, Locomotion.TurnSpeedFactor(10.0));
        Assert.Equal(0.25, Locomotion.TurnSpeedFactor(-170.0));
        Assert.True(Locomotion.Lean(90.0, 3.0, 8.0) > 0.0 && Locomotion.Lean(-90.0, 3.0, 8.0) < 0.0);
        Assert.Equal(8.0, Locomotion.Lean(720.0, 8.0, 8.0));
        Assert.Equal(0.0, Locomotion.Lean(300.0, 0.0, 8.0));
    }

    [Fact]
    public void Arret_avec_hysteresis_et_pas_sur_place()
    {
        Assert.False(Locomotion.Moving(false, 0.2, 0.0));
        Assert.True(Locomotion.Moving(false, 0.4, 0.0));
        Assert.True(Locomotion.Moving(true, 0.2, 1.0));    // entre les seuils : il continue
        Assert.True(Locomotion.Moving(true, 0.05, 0.05));  // un tick en retard ne l'arrête pas
        Assert.False(Locomotion.Moving(true, 0.05, 0.2));
        Assert.True(Locomotion.TurningInPlace(0.0, 90.0));
        Assert.False(Locomotion.TurningInPlace(1.0, 90.0));
        Assert.False(Locomotion.TurningInPlace(0.0, 10.0));
    }

    [Fact]
    public void Allures_des_betes_avec_hysteresis_et_repli()
    {
        const double h = 1.5;
        double walk = Locomotion.NaturalSpeed(LocomotionGait.Walk, h, true);
        double trot = Locomotion.NaturalSpeed(LocomotionGait.Trot, h, true);
        double gallop = Locomotion.NaturalSpeed(LocomotionGait.Gallop, h, true);
        Assert.Equal(LocomotionGait.Walk, Locomotion.ChooseGait(LocomotionGait.Walk, walk, h, true, true, true));
        Assert.Equal(LocomotionGait.Trot, Locomotion.ChooseGait(LocomotionGait.Walk, trot, h, true, true, true));
        Assert.Equal(LocomotionGait.Gallop, Locomotion.ChooseGait(LocomotionGait.Trot, gallop, h, true, true, true));
        double mid = 0.5 * (walk + trot);
        // Au seuil même, chacun garde son allure (pas de va-et-vient).
        Assert.Equal(LocomotionGait.Walk, Locomotion.ChooseGait(LocomotionGait.Walk, mid, h, true, true, true));
        Assert.Equal(LocomotionGait.Trot, Locomotion.ChooseGait(LocomotionGait.Trot, mid, h, true, true, true));
        // Sans galop : le trot ; sans trot ni galop : le pas.
        Assert.Equal(LocomotionGait.Trot, Locomotion.ChooseGait(LocomotionGait.Walk, gallop, h, true, true, false));
        Assert.Equal(LocomotionGait.Walk, Locomotion.ChooseGait(LocomotionGait.Walk, gallop, h, true, false, false));
        Assert.Equal(LocomotionGait.Gallop, Locomotion.ChooseGait(LocomotionGait.Walk, gallop, h, true, false, true));
    }

    [Fact]
    public void Attentes_et_regards_desynchronises_et_reproductibles()
    {
        uint a = Locomotion.Seed("bourg/bavard1"), b = Locomotion.Seed("bourg/bavard2");
        Assert.NotEqual(a, b);
        Assert.Equal(a, Locomotion.Seed("bourg/bavard1"));
        Assert.InRange(Locomotion.IdleRate(a), 0.88, 1.12);
        Assert.InRange(Locomotion.IdlePhase(a), 0.0, 1.0);
        var seeds = Enumerable.Range(0, 200).Select(i => Locomotion.Seed("pnj" + i)).ToArray();
        // Des départs variés : pas deux fois sur dix le même dixième de cycle.
        Assert.True(seeds.Select(s => (int)(Locomotion.IdlePhase(s) * 10)).Distinct().Count() == 10);
        // Variantes : l'ordinaire la plus fréquente, les autres présentes ; même résultat à chaque appel.
        int ordinary = 0, other = 0;
        for (int t = 0; t < 2000; t += 13)
        {
            (int v, double until) = Locomotion.IdleVariant(a, t, 3);
            Assert.True(until > t);
            Assert.InRange(v, 0, 2);
            Assert.Equal((v, until), Locomotion.IdleVariant(a, t, 3));
            if (v == 0) ordinary++;
            else other++;
        }
        Assert.True(ordinary > other && other > 0);
        Assert.Equal(0, Locomotion.IdleVariant(a, 50.0, 1).Variant);
        // Regard : borné et continu (pas de saut d'une image à l'autre).
        double last = Locomotion.Glance(a, 0.0).Yaw;
        for (double t = 0.02; t < 120.0; t += 0.02)
        {
            (double y, double p) = Locomotion.Glance(a, t);
            Assert.InRange(y, -35.0, 35.0);
            Assert.InRange(p, -8.0, 8.0);
            Assert.True(Math.Abs(y - last) < 2.0);
            last = y;
        }
    }

    [Fact]
    public void Tete_vers_un_point_sans_se_tordre_le_cou()
    {
        (double y, double p) = Locomotion.Watch(1.0, 0.0, 1.0, 0.0, 60.0);
        Assert.Equal(45.0, y, 6);
        Assert.Equal(0.0, p, 6);
        Assert.Equal(60.0, Locomotion.Watch(1.0, 0.0, 0.2, 0.0, 60.0).Yaw, 6);
        Assert.Equal((0.0, 0.0), Locomotion.Watch(0.0, 0.0, -1.0, 0.0, 60.0));   // derrière
        Assert.True(Locomotion.Watch(0.0, -1.0, 2.0, 0.0, 60.0).Pitch > 0.0);   // un point plus bas : la tête se baisse
    }

    [Fact]
    public void Pieds_sur_le_relief()
    {
        Assert.Equal(0.2, Locomotion.FootLift(0.5, 0.3, 0.45), 9);
        Assert.Equal(0.45, Locomotion.FootLift(3.0, 0.0, 0.45));
        Assert.Equal(-0.45, Locomotion.FootLift(-3.0, 0.0, 0.45));
        Assert.Equal(0.0, Locomotion.FootLift(double.NaN, 0.0, 0.45));
        Assert.Equal(-0.1, Locomotion.HipDrop(-0.1, 0.2));
        Assert.Equal(0.0, Locomotion.HipDrop(0.1, 0.2));
        Assert.Equal(-170.0, Locomotion.Wrap(190.0), 9);
        Assert.Equal(180.0, Locomotion.Wrap(-180.0), 9);
    }

    [Fact]
    public void Os_de_la_tete_et_attentes_variees_reconnus()
    {
        Assert.True(Locomotion.IsHeadBone("Head"));
        Assert.True(Locomotion.IsHeadBone("mixamorig:Head"));
        Assert.True(Locomotion.IsHeadBone("Armature|Head_1"));
        Assert.False(Locomotion.IsHeadBone("mixamorig:HeadTop_End"));
        Assert.False(Locomotion.IsHeadBone("Head_Jaw"));
        Assert.False(Locomotion.IsHeadBone(null));
        Assert.True(Locomotion.IsNeckBone("neck_01"));
        Assert.False(Locomotion.IsNeckBone("Necklace"));
        Assert.True(Locomotion.IsIdleVariant("Look_FoldArms"));
        Assert.True(Locomotion.IsIdleVariant("Idle2"));
        Assert.False(Locomotion.IsIdleVariant("Idle_Torch"));
        Assert.False(Locomotion.IsIdleVariant("Seated_Rest"));
        Assert.False(Locomotion.IsIdleVariant("Walk"));
    }

    [Fact]
    public void Allure_sans_clip_prend_le_cycle_present_le_plus_proche()
    {
        // Un modèle qui n'a que l'attente, le galop et la mort (repli de StyleRulesTests.Animations_avec_repli) : il galope au ralenti.
        Assert.Equal(LocomotionGait.Gallop, Locomotion.ClosestGait(LocomotionGait.Walk, false, false, true));
        Assert.Equal(LocomotionGait.Gallop, Locomotion.ClosestGait(LocomotionGait.Trot, false, false, true));
        Assert.Equal(LocomotionGait.Walk, Locomotion.ClosestGait(LocomotionGait.Gallop, true, false, false));
        Assert.Equal(LocomotionGait.Walk, Locomotion.ClosestGait(LocomotionGait.Trot, true, false, true));   // à égalité, la plus lente
        Assert.Equal(LocomotionGait.Trot, Locomotion.ClosestGait(LocomotionGait.Walk, false, true, true));
        Assert.Equal(LocomotionGait.Walk, Locomotion.ClosestGait(LocomotionGait.Stand, true, true, true));
        Assert.Equal(LocomotionGait.Stand, Locomotion.ClosestGait(LocomotionGait.Walk, false, false, false));
        // Au ralenti : la cadence du galop à 1 m/s reste dans les bornes (la moitié au moins).
        double gallop = Locomotion.NaturalSpeed(LocomotionGait.Gallop, 1.0, true);
        Assert.Equal(0.5, Locomotion.PlaybackRate(1.0, gallop));
    }
}
