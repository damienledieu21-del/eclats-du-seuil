using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Domain.Stats;

namespace EdS.Domain.Tests.Space;

/// <summary>
/// Sous-lot 11.4b : validation des portées (seul validateur, chemins de la forme du fichier) et géométrie de la ligne de vue (segment
/// contre segment, segment contre polygone convexe), comparée à un oracle indépendant en nombres rationnels exacts.
/// </summary>
[Trait("Category", "Space")]
public class Lot114bRangeValidationTests
{
    private static Fixed F(string value) => Fixed.Parse(value);

    private static IReadOnlyList<RuleViolation> ClassErrors(params SkillDefinition[] skills) =>
        new ClassDefinition("class.essai", "Essai",
            new[] { "a", "b", "c" }.Select(k => new TalentTreeDefinition($"tree.essai.{k}", k, new[] { new TalentDefinition($"talent.essai.{k}.t", "T", 5) })).ToArray(),
            skills).Validate(ProgressionRules.Provisional);

    private static SkillDefinition Skill(SkillCombatDefinition combat) => new("skill.essai.s", "S", SkillKind.Active, combat: combat);

    private static SkillCombatDefinition Strike(SkillTargeting targeting, string? range, params StatusApplication[] applies) =>
        targeting is SkillTargeting.Enemy or SkillTargeting.AllEnemies
            ? new(PowerType.Physique, DamageType.Physique, F("1"), 0, 0, targeting, applies: applies, range: range is null ? null : F(range))
            : new(null, null, null, 0, 0, targeting, applies: applies, range: range is null ? null : F(range));

    private static string Only(IReadOnlyList<RuleViolation> errors) => Assert.Single(Assert.Single(errors).Fields);

    [Theory]
    [InlineData("0.1")]
    [InlineData("2.5")]
    [InlineData("100")]
    public void Portee_de_competence_admise_dans_ses_bornes(string range)
    {
        foreach (SkillTargeting targeting in new[] { SkillTargeting.Enemy, SkillTargeting.AllEnemies, SkillTargeting.Ally, SkillTargeting.AllAllies })
            Assert.Empty(ClassErrors(Skill(Strike(targeting, range))));
        Assert.Equal(F(range), Strike(SkillTargeting.Enemy, range).Range);
        Assert.Null(Strike(SkillTargeting.Enemy, null).Range);
    }

    [Theory]
    [InlineData("0.0999")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("100.0001")]
    public void Portee_de_competence_hors_bornes_refusee(string range)
    {
        RuleViolation error = Assert.Single(ClassErrors(Skill(Strike(SkillTargeting.Enemy, range))));
        Assert.Equal((RuleViolationKind.NumericLimit, "skills[0].combat.range"), (error.Kind, error.Fields.Single()));
        Assert.Contains("[0.1, 100]", error.Message);
    }

    [Fact]
    public void Competence_sur_soi_sans_portee()
    {
        Assert.Empty(ClassErrors(Skill(Strike(SkillTargeting.Self, null))));
        RuleViolation error = Assert.Single(ClassErrors(Skill(Strike(SkillTargeting.Self, "5"))));
        Assert.Equal("skills[0].combat.range", error.Fields.Single());
        Assert.Contains("sur soi", error.Message);
    }

    [Fact]
    public void Rayon_d_application_seulement_pour_une_cible_propre_de_groupe()
    {
        Assert.Empty(ClassErrors(Skill(Strike(SkillTargeting.Self, null,
            new StatusApplication("bastionAbsolute", target: ApplicationTarget.AllAllies, range: F("15")),
            new StatusApplication("bastionAbsoluteTaunt", target: ApplicationTarget.AllEnemies, range: F("15"))))));
        Assert.Equal("skills[0].combat.applies[0].range",
            Only(ClassErrors(Skill(Strike(SkillTargeting.Enemy, null, new StatusApplication("stun", range: F("5")))))));
        Assert.Equal("skills[0].combat.applies[0].range",
            Only(ClassErrors(Skill(Strike(SkillTargeting.Self, null, new StatusApplication("bastionAbsoluteGuard", target: ApplicationTarget.Self, range: F("5")))))));
        RuleViolation bounds = Assert.Single(ClassErrors(Skill(Strike(SkillTargeting.Self, null,
            new StatusApplication("bastionAbsolute", target: ApplicationTarget.AllAllies, range: F("100.0001"))))));
        Assert.Equal((RuleViolationKind.NumericLimit, "skills[0].combat.applies[0].range"), (bounds.Kind, bounds.Fields.Single()));
    }

    private static SkillDefinition Passive(params TriggerEffect[] effects) =>
        new("skill.essai.p", "P", SkillKind.Passive, triggers: new[] { new TriggerDefinition(TriggerEvent.PerfectParry, effects, TriggerRole.Target, cooldownMs: 3000) });

    [Fact]
    public void Portee_d_un_effet_de_reaction_dirige_vers_un_autre_combattant()
    {
        Assert.Empty(ClassErrors(Passive(TriggerEffect.Strike(TriggerTarget.EventSource, Fixed.One, range: F("2.5")))));
        Assert.Empty(ClassErrors(Passive(TriggerEffect.Strike(TriggerTarget.AllEnemies, Fixed.One, range: F("5")))));
        Assert.Empty(ClassErrors(Passive(TriggerEffect.ApplyStatus(TriggerTarget.EventSource, "oathTaunt", range: F("15")))));
        Assert.Empty(ClassErrors(Passive(TriggerEffect.ApplyStatus(TriggerTarget.EventTarget, "oathTaunt", range: F("0.1")))));
        Assert.Equal("skills[0].triggers[0].effects[0].range",
            Only(ClassErrors(Passive(TriggerEffect.ApplyStatus(TriggerTarget.Bearer, "reactiveAegis", range: F("5"))))));
        Assert.Equal("skills[0].triggers[0].effects[1].range",
            Only(ClassErrors(Passive(TriggerEffect.Strike(TriggerTarget.EventSource, Fixed.One),
                new TriggerEffect(TriggerEffectKind.GainResource, resource: TriggerResource.Resonance, amount: Fixed.One, range: F("5"))))));
        RuleViolation bounds = Assert.Single(ClassErrors(Passive(TriggerEffect.Strike(TriggerTarget.EventSource, Fixed.One, range: F("0.0999")))));
        Assert.Equal((RuleViolationKind.NumericLimit, "skills[0].triggers[0].effects[0].range"), (bounds.Kind, bounds.Fields.Single()));
    }

    [Fact]
    public void Portee_d_une_protection_seulement()
    {
        StatusDefinition Protection(string? range) => new("lien", StatusCategory.Protection, 6000, StatusStacking.PerSourceKeepStronger,
            redirectRatio: F("0.25"), range: range is null ? null : F(range));
        Assert.Empty(new StatusCatalog(new[] { Protection("15") }).Validate(50));
        Assert.Empty(new StatusCatalog(new[] { Protection(null) }).Validate(50));
        Assert.Equal("statuses[0].range", Only(new StatusCatalog(new[] { Protection("100.5") }).Validate(50)));
        Assert.Equal("statuses[0].range", Only(new StatusCatalog(new[]
            { new StatusDefinition("provoc", StatusCategory.Taunt, 3000, StatusStacking.PerSourceKeepStronger, range: F("15")) }).Validate(50)));
        Assert.True(CombatRules.Provisional.Statuses.TryGet("protectiveLink", out StatusDefinition link));
        Assert.Equal(F("15"), link.Range);
        Assert.All(CombatRules.Provisional.Statuses.Definitions.Where(d => d.Id != "protectiveLink"), d => Assert.Null(d.Range));
    }

    // --- Géométrie de la ligne de vue ---------------------------------------------------------------

    /// <summary>
    /// Oracle exact (rationnels) : le segment [a, b] coupe le polygone convexe fermé si un point a + t(b − a), t dans [0, 1], vérifie
    /// toutes les inégalités des côtés (découpage de Cyrus et Beck sur l'intervalle de t, bornes en fractions entières).
    /// </summary>
    private static bool Oracle(long ax, long az, long bx, long bz, IReadOnlyList<(long X, long Z)> polygon)
    {
        // Sens trigonométrique imposé par l'oracle : l'intérieur est à gauche de chaque côté.
        long area = 0;
        for (int i = 0; i < polygon.Count; i++)
        {
            var (x0, z0) = polygon[i];
            var (x1, z1) = polygon[(i + 1) % polygon.Count];
            area += x0 * z1 - x1 * z0;
        }
        var pts = area > 0 ? polygon : polygon.Reverse().ToList();
        // Intervalle [lo, hi] de t en fractions (num, den > 0), comparées par produits croisés (Int128 : sans débordement).
        (Int128 N, Int128 D) lo = (0, 1), hi = (1, 1);
        for (int i = 0; i < pts.Count; i++)
        {
            var (x0, z0) = pts[i];
            var (x1, z1) = pts[(i + 1) % pts.Count];
            // côté (x0,z0)→(x1,z1) ; intérieur : cross(e, p − v0) ≥ 0 avec p = a + t(b − a) → c + t·k ≥ 0
            Int128 ex = x1 - x0, ez = z1 - z0;
            Int128 c = ex * (az - z0) - ez * (ax - x0);
            Int128 k = ex * (bz - az) - ez * (bx - ax);
            if (k == 0)
            {
                if (c < 0) return false;
                continue;
            }
            // t ≥ −c / k si k > 0, t ≤ −c / k si k < 0
            (Int128 N, Int128 D) bound = k > 0 ? (-c, k) : (c, -k);
            if (k > 0) { if (bound.N * lo.D > lo.N * bound.D) lo = bound; }
            else if (bound.N * hi.D < hi.N * bound.D) hi = bound;
            if (lo.N * hi.D > hi.N * lo.D) return false;
        }
        return true;
    }

    [Fact]
    public void Segment_contre_polygone_convexe_conforme_a_l_oracle_exact()
    {
        var random = new Random(1142);
        int crossing = 0, clear = 0;
        for (int n = 0; n < 20_000; n++)
        {
            // Petits entiers bruts : beaucoup de contacts exacts (sommets, côtés colinéaires).
            int scale = random.Next(2) == 0 ? 8 : 200_000;
            long R() => random.Next(-scale, scale + 1);
            var raw = Enumerable.Range(0, random.Next(3, 7)).Select(_ => GroundVector.FromRaw(R(), R())).ToList();
            if (!ConvexPolygon.IsStrictlyConvex(raw, out _)) continue;
            var polygon = new ConvexPolygon(raw);
            long ax = R(), az = R(), bx = R(), bz = R();
            bool expected = Oracle(ax, az, bx, bz, raw.Select(p => (p.X.Raw, p.Z.Raw)).ToList());
            Assert.Equal(expected, polygon.IntersectsSegment(ax, az, bx, bz));
            Assert.Equal(expected, polygon.IntersectsSegment(bx, bz, ax, az));
            if (expected) crossing++; else clear++;
        }
        Assert.True(crossing > 2000 && clear > 2000, $"{crossing} / {clear}");
    }

    [Fact]
    public void Segments_qui_se_touchent_se_coupent()
    {
        Assert.True(Planar.SegmentsIntersect(0, 0, 10, 0, 5, 0, 5, 5));      // extrémité sur l'autre segment
        Assert.True(Planar.SegmentsIntersect(0, 0, 10, 0, 10, 0, 20, 7));    // extrémités communes
        Assert.True(Planar.SegmentsIntersect(0, 0, 10, 0, 4, 0, 12, 0));     // colinéaires qui se recouvrent
        Assert.False(Planar.SegmentsIntersect(0, 0, 10, 0, 11, 0, 12, 0));   // colinéaires disjoints
        Assert.False(Planar.SegmentsIntersect(0, 0, 10, 0, 5, 1, 5, 5));     // passe juste au-dessus
        Assert.True(Planar.SegmentsIntersect(0, 0, 10, 10, 0, 10, 10, 0));   // croisement franc
        long big = SpaceLimits.MaxCoordinate.Raw;
        Assert.True(Planar.SegmentsIntersect(-big, -big, big, big, -big, big, big, -big));   // bornes de l'aire : sans débordement
    }

    [Fact]
    public void Polygone_et_segment_cas_limites()
    {
        var square = new ConvexPolygon(new[] { GroundVector.FromRaw(0, 0), GroundVector.FromRaw(10, 0), GroundVector.FromRaw(10, 10), GroundVector.FromRaw(0, 10) });
        Assert.True(square.IntersectsSegment(2, 2, 3, 3));        // entièrement dedans
        Assert.True(square.IntersectsSegment(-5, 0, 15, 0));      // le long d'un côté
        Assert.True(square.IntersectsSegment(-5, -5, 0, 0));      // finit sur un sommet
        Assert.True(square.IntersectsSegment(-1, 5, 11, 5));      // traverse
        Assert.False(square.IntersectsSegment(-5, -1, 15, -1));   // longe à distance
        Assert.False(square.IntersectsSegment(11, 0, 20, 10));    // à côté
        Assert.True(square.IntersectsSegment(5, 5, 5, 5));        // point intérieur
        Assert.False(square.IntersectsSegment(11, 11, 11, 11));   // point extérieur
    }
}
