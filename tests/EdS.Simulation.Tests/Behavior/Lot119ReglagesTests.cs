using System.Text.Json;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;
using static EdS.Simulation.Tests.Engine.TestCombat;
using static EdS.Simulation.Tests.Space.SpaceTestKit;

namespace EdS.Simulation.Tests.Behavior;

/// <summary>
/// Sous-lot 11.9 : réglages du moteur demandés par Damien après l'essai du 22 octobre 2026 : obstacle bas (arrête la marche, pas la vue),
/// bande qui repousse, étourdissement qui annule l'attaque annoncée, arrêt pendant l'annonce réglable par attaque, meute réglable par
/// ennemi.
/// </summary>
[Trait("Category", "Behavior")]
public class Lot119ReglagesTests
{
    private static readonly CombatRules Rules = new() { GuardUpkeepPerSecond = 0, TelegraphMinReactionMs = 0 };

    private static Fixed F(string v) => Fixed.Parse(v);

    private static CombatSetup Fight(IEnumerable<CombatantSetup> combatants, IEnumerable<CombatCommand>? commands = null, long durationMs = 2000,
        CombatSpace? space = null) =>
        Spatial(space ?? Arena(20), combatants, commands ?? Array.Empty<CombatCommand>(), Rules, durationMs);

    private static AssailantAttack Band(string range = "8", string? push = "2", bool charge = false, long raw = 50) =>
        new("charge", raw, EdS.Domain.Stats.DamageType.Physique, SkillTargeting.Enemy, false, null, AttackShape.Line, 1000, range: F(range), width: F("1"),
            charge: charge, push: push is null ? null : F(push));

    private static AssailantSetup Boar(string x, string z, params AssailantAttack[] attacks) =>
        Lot115BehaviorTests.Hunter("sanglier", At(x, z), "20", firstMs: 0, attacks: attacks);

    private static double X(JsonElement line) => JsonDocument.Parse(line.GetProperty("position").GetRawText()).RootElement[0].GetDouble();

    // --- Obstacle bas -------------------------------------------------------------------------------

    [Fact]
    public void Un_obstacle_bas_arrete_la_marche_mais_pas_la_vue()
    {
        // La même paroi entre le lanceur et le mannequin : haute, elle coupe la vue (noLineOfSight) ; basse (rivière), la compétence part.
        string appel = EdS.Simulation.Tests.Space.Lot114bRangeTests.Appel;
        SpaceObstacle Wall(bool low) => new("riviere", new[] { V("5", "-2"), V("6", "-2"), V("6", "2"), V("5", "2") }, low);
        MemoryCombatLog Cast(bool low) => Run(Spatial(Arena(30, Wall(low)), new CombatantSetup[]
            {
                EdS.Simulation.Tests.Space.Lot114bRangeTests.Hero("bastion", At("0", "0")), PlacedDummy("mannequin", At("10", "0")),
            }, new[] { Cmd(0, appel) }, NoUpkeep, 3000)).Log;
        Assert.Equal("noLineOfSight", Assert.Single(Lines(Cast(false), "commandRejected")).Str("reason"));
        MemoryCombatLog over = Cast(true);
        Assert.Empty(Lines(over, "commandRejected"));
        Assert.Contains(Lines(over, "damage"), d => d.Str("target") == "mannequin");
        // La marche, elle, bute sur la rivière basse comme sur un mur : le Bastion s'arrête avant x = 5 (son rayon : 0,5 m).
        var (track, _, _) = Trace(Spatial(Arena(30, Wall(true)), new CombatantSetup[] { Player("bastion", At("0", "0")), PlacedDummy("mannequin", At("-20", "20")) },
            new[] { Move(0, "bastion", "1", "0") }, NoUpkeep, 3000));
        GroundVector end = track.After("bastion", 59);
        Assert.InRange(end.X.Raw, 44_000, 45_000);
        Assert.Equal(Fixed.Zero, end.Z);
    }

    [Fact]
    public void Reglages_ecrits_dans_l_en_tete_seulement_s_ils_changent()
    {
        // L'en-tête du journal (format 15) écrit low: true sur un obstacle bas, push sur une bande qui repousse, holdPosition: false et
        // pack: false, chacun seulement s'il s'écarte du défaut : un journal sans ces réglages garde ses lignes. Rejeu : Lot119ReglagesDataTests.
        var space = Arena(20, new SpaceObstacle("riviere", new[] { V("2", "-5"), V("3", "-5"), V("3", "5"), V("2", "5") }, low: true),
            Rect("rocher", "-8", "-1", "-7", "1"));
        var rain = new AssailantAttack("pluie", 10, EdS.Domain.Stats.DamageType.Physique, SkillTargeting.Enemy, false, null, AttackShape.Circle, 1000,
            radius: F("2"), holdPosition: false);
        var loner = new AssailantSetup("sanglier", "meute", Fixed.Zero, Fixed.Zero, null, 0, 1000, new[] { Band(), rain,
            new AssailantAttack("coup", 10, EdS.Domain.Stats.DamageType.Physique, SkillTargeting.Enemy, false, null, AttackShape.Line, 1000, range: F("6"), width: F("1")) },
            placement: At("6", "0"), behavior: new AssailantBehavior(F("20"), pack: false));
        var (_, log) = Run(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), loner }, space: space));
        JsonElement scenario = JsonDocument.Parse(log.Lines[0]).RootElement.GetProperty("scenario");
        JsonElement[] obstacles = scenario.GetProperty("space").GetProperty("obstacles").EnumerateArray().ToArray();
        Assert.True(obstacles[0].GetProperty("low").GetBoolean());
        Assert.False(obstacles[1].TryGetProperty("low", out _));
        JsonElement boar = scenario.GetProperty("combatants").EnumerateArray().Single(c => c.Str("id") == "sanglier");
        Assert.False(boar.GetProperty("behavior").GetProperty("pack").GetBoolean());
        JsonElement[] attacks = boar.GetProperty("attacks").EnumerateArray().ToArray();
        Assert.Equal("2", attacks[0].GetProperty("push").GetRawText());
        Assert.False(attacks[0].TryGetProperty("holdPosition", out _));
        Assert.False(attacks[1].GetProperty("holdPosition").GetBoolean());
        Assert.False(attacks[2].TryGetProperty("push", out _));
        JsonElement bastion = scenario.GetProperty("combatants").EnumerateArray().Single(c => c.Str("id") == "bastion");
        Assert.False(bastion.TryGetProperty("behavior", out _));
        var (_, plain) = Run(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Boar("6", "0", Band(push: null)) }));
        Assert.DoesNotContain("\"pack\"", plain.Lines[0], StringComparison.Ordinal);
        Assert.DoesNotContain("\"low\"", plain.Lines[0], StringComparison.Ordinal);
    }

    // --- Poussée ------------------------------------------------------------------------------------

    [Fact]
    public void La_bande_repousse_le_joueur_touche_le_long_de_son_axe()
    {
        // Sanglier en (0, 0), Bastion en (4, 0) dans la bande de 8 m : touché, puis repoussé de 2 m vers l'est (x = 6).
        var (track, log, _) = Trace(Fight(new CombatantSetup[] { Player("bastion", At("4", "0")), Boar("0", "0", Band()) }));
        JsonElement pushed = Assert.Single(Lines(log, "pushed"));
        Assert.Equal(("sanglier", "charge", "bastion", 20L), (pushed.Str("actor"), pushed.Str("attack"), pushed.Str("target"), pushed.Tick()));
        Assert.Equal("[6,0]", pushed.GetProperty("position").GetRawText());
        Assert.Equal(V("4", "0"), track.After("bastion", 20));
        Assert.Equal(V("6", "0"), track.After("bastion", 21));
        // La poussée suit les coups de la bande.
        List<JsonElement> lines = Events(log).ToList();
        int damage = lines.FindIndex(e => e.Str("event") == "damage" && e.Str("target") == "bastion");
        Assert.True(damage >= 0 && damage < lines.FindIndex(e => e.Str("event") == "pushed"));
        // Sans poussée : rien ne bouge.
        var (still, quiet, _) = Trace(Fight(new CombatantSetup[] { Player("bastion", At("4", "0")), Boar("0", "0", Band(push: null)) }));
        Assert.Empty(Lines(quiet, "pushed"));
        Assert.Equal(V("4", "0"), still.After("bastion", 21));
    }

    [Fact]
    public void Une_charge_qui_repousse_fonce_dans_la_place_degagee()
    {
        // La poussée part avant la charge : le Bastion est à x = 6, le sanglier fonce jusqu'à son contact (bords à moins d'un sous-pas).
        var (_, log, _) = Trace(Fight(new CombatantSetup[] { Player("bastion", At("4", "0")), Boar("0", "0", Band(charge: true)) }));
        List<JsonElement> lines = Events(log).ToList();
        Assert.True(lines.FindIndex(e => e.Str("event") == "pushed") < lines.FindIndex(e => e.Str("event") == "charged"));
        Assert.InRange(X(Assert.Single(Lines(log, "charged"))), 4.75, 5.0);
    }

    [Fact]
    public void Une_poussee_bute_sur_le_decor_et_le_bord_de_l_aire()
    {
        // Un rocher en x = 5 : le Bastion (rayon 0,5) s'arrête contre lui, à x = 4,5 au plus.
        var (_, wall, _) = Trace(Fight(new CombatantSetup[] { Player("bastion", At("4", "0")), Boar("0", "0", Band()) },
            space: Arena(20, Rect("rocher", "5", "-2", "6", "2"))));
        Assert.InRange(X(Assert.Single(Lines(wall, "pushed"))), 4.0, 4.5);
        // Le bord de l'aire (x = 6) aussi : x = 5,5 au plus.
        var (_, edge, _) = Trace(Fight(new CombatantSetup[] { Player("bastion", At("4", "0")), Boar("0", "0", Band()) },
            space: Box("-6", "-6", "6", "6")));
        Assert.InRange(X(Assert.Single(Lines(edge, "pushed"))), 5.25, 5.5);
    }

    [Fact]
    public void Une_cible_tombee_sous_les_coups_n_est_pas_repoussee()
    {
        // Un allié fragile (vitalité 1), tué par la bande : pushBlocked (targetFallen) au lieu de pushed ; le Bastion, lui, est repoussé.
        var weak = Player("cible", At("3", "0.3"), attributes: Attrs(vitalite: 1));
        var (track, log, _) = Trace(Fight(new CombatantSetup[] { Player("bastion", At("6", "-0.2")), weak, Boar("0", "0", Band(range: "10", raw: 400)) }));
        Assert.Contains(Lines(log, "died"), d => d.Str("combatant") == "cible");
        Assert.DoesNotContain(Lines(log, "died"), d => d.Str("combatant") == "bastion");
        JsonElement blocked = Assert.Single(Lines(log, "pushBlocked"));
        Assert.Equal(("cible", 20L), (blocked.Str("target"), blocked.Tick()));
        Assert.Equal("targetFallen", blocked.Str("reason"));
        Assert.Equal("bastion", Assert.Single(Lines(log, "pushed")).Str("target"));
        Assert.Equal(track.After("cible", 20), track.After("cible", 21));
    }

    [Fact]
    public void La_recherche_du_bout_de_la_poussee_compte_son_travail()
    {
        // Comme la charge (revue Codex de la PR #33, tour 10) : chaque sous-pas (au plus la moitié du rayon, 0,25 m) et son contrôle coûtent
        // une unité de travail, réservée avec l'impact : 4 m de poussée coûtent 2 × (16 − 8) unités de plus que 2 m au tick de l'impact.
        long WorkAtImpact(string push)
        {
            var engine = new CombatEngine(Fight(new CombatantSetup[] { Player("bastion", At("4", "0")), Boar("0", "0", Band(push: push)) }), NullCombatLog.Instance);
            while (engine.CurrentTick <= 20) engine.Step();
            return engine.LastTickWork;
        }
        Assert.Equal(2 * (16 - 8), WorkAtImpact("4") - WorkAtImpact("2"));
    }

    [Fact]
    public void Instantane_une_place_repoussee_est_admise_une_fois_la_poussee_possible()
    {
        // Le Bastion n'a jamais marché : sa place n'est bornée par ses pas que tant qu'aucune bande qui repousse n'a pu tomber. Avant
        // l'impact (tick 10), le déplacer est refusé ; après (tick 30), sa place repoussée se relit, et une autre place est admise par la
        // reprise de confiance (la reprise vérifiée par rejeu reste exacte).
        CombatEngine Until(int tick)
        {
            var engine = new CombatEngine(Fight(new CombatantSetup[] { Player("bastion", At("4", "0")), Boar("0", "0", Band()) }), NullCombatLog.Instance);
            while (engine.CurrentTick < tick) engine.Step();
            return engine;
        }
        string Moved(string text, string from, string to)
        {
            string line = text.Split('\n').First(l => l.StartsWith("{\"fighter\":\"bastion\"", StringComparison.Ordinal));
            string changed = line.Replace(from, to, StringComparison.Ordinal);
            Assert.NotEqual(line, changed);
            return text.Replace(line, changed, StringComparison.Ordinal);
        }
        CombatEngine early = Until(10);
        string before = early.Snapshot().Text;
        Assert.Contains("Place de « bastion » hors de portée", Assert.Throws<FormatException>(() =>
            CombatEngine.Restore(early.CompletedSetup(), CombatSnapshot.Parse(Moved(before, "\"x\":40000,", "\"x\":60000,")), NullCombatLog.Instance)).Message);
        CombatEngine late = Until(30);
        string after = late.Snapshot().Text;
        Assert.Contains("\"x\":60000,", after.Split('\n').First(l => l.StartsWith("{\"fighter\":\"bastion\"", StringComparison.Ordinal)));
        CombatSetup setup = late.CompletedSetup();
        Assert.Equal(after, CombatEngine.Restore(setup, CombatSnapshot.Parse(after), NullCombatLog.Instance).Snapshot().Text);
        Assert.Equal(after, CombatEngine.RestoreVerified(setup, CombatSnapshot.Parse(after), NullCombatLog.Instance).Snapshot().Text);
        string forged = Moved(after, "\"x\":60000,", "\"x\":70000,");
        CombatEngine.Restore(setup, CombatSnapshot.Parse(forged), NullCombatLog.Instance);
        Assert.Contains("différent de l'état rejoué", Assert.Throws<FormatException>(() =>
            CombatEngine.RestoreVerified(setup, CombatSnapshot.Parse(forged), NullCombatLog.Instance)).Message);
    }

    // --- Étourdissement pendant l'annonce -----------------------------------------------------------

    [Fact]
    public void Un_etourdissement_pendant_l_annonce_annule_l_attaque()
    {
        // Essai de Damien du 22 octobre 2026 : « l'étourdissement l'annule ». Le sanglier annonce sa bande à 0, s'étourdit lui-même par son
        // cri (tour suivant, 100 ms après) : à l'impact (tick 20), l'attaque est annulée (attackCancelled, raison du contrôle) : ni coups,
        // ni poussée. Une zone persistante déjà tombée, elle, continue (autres tests).
        var cri = new AssailantAttack("cri", 1, EdS.Domain.Stats.DamageType.Physique, SkillTargeting.Enemy, false,
            new[] { new StatusApplication("stun", target: ApplicationTarget.Self) });
        var boar = Lot115BehaviorTests.Hunter("sanglier", At("0", "0"), "20", firstMs: 0, intervalMs: 100, attacks: new[] { Band(), cri });
        var (track, log, _) = Trace(Fight(new CombatantSetup[] { Player("bastion", At("4", "0")), boar }));
        JsonElement cancelled = Assert.Single(Lines(log, "attackCancelled"), c => c.Str("attack") == "charge");
        Assert.Equal(("sanglier", 0L, 20L), (cancelled.Str("actor"), cancelled.GetProperty("pulse").GetInt64(), cancelled.Tick()));
        Assert.False(string.IsNullOrEmpty(cancelled.Str("reason")));
        Assert.DoesNotContain(Lines(log, "attackLanded"), l => l.Str("attack") == "charge");
        Assert.Empty(Lines(log, "pushed"));
        Assert.DoesNotContain(Lines(log, "damage"), d => d.Str("target") == "bastion" && d.Tick() == 20);
        Assert.Equal(V("4", "0"), track.After("bastion", 21));
    }

    // --- Arrêt pendant l'annonce --------------------------------------------------------------------

    [Fact]
    public void Arret_pendant_l_annonce_reglable_par_attaque()
    {
        // Un archer à 8 m annonce sa pluie (cercle, 1,2 s) dès le début et le Bastion s'éloigne : avec holdPosition (par défaut), il reste
        // planté pendant l'annonce ; sans (holdPosition: false), il continue à garder sa distance.
        AssailantSetup Archer(bool hold) => new("archer", "meute", Fixed.Zero, Fixed.Zero, null, 0, 600_000,
            new[] { new AssailantAttack("pluie", 40, EdS.Domain.Stats.DamageType.Physique, SkillTargeting.Enemy, false, null, AttackShape.Circle, 1200,
                radius: F("2"), holdPosition: hold) },
            placement: At("8", "0"), behavior: new AssailantBehavior(F("4"), F("2")));
        Dictionary<string, List<GroundVector>> Walk(bool hold) => Trace(Fight(new CombatantSetup[] { Player("bastion", At("0", "0")), Archer(hold) },
            new[] { Move(0, "bastion", "-1", "0") })).Track;
        Dictionary<string, List<GroundVector>> held = Walk(true), free = Walk(false);
        for (int t = 1; t <= 23; t++) Assert.Equal(held.After("archer", 0), held.After("archer", t));
        Assert.True(free.After("archer", 23).X < free.After("archer", 0).X, $"archer en {free.After("archer", 23)}");
    }

    // --- Meute --------------------------------------------------------------------------------------

    [Fact]
    public void Un_ennemi_hors_meute_ne_suit_pas_la_meute()
    {
        // loup1 s'engage d'emblée (engagement à 30 m) ; ses voisins (à 0,5 m de lui, à 9 m du Bastion, engagement à 1 m) : loup3 suit (pack),
        // loup2 (pack: false) ne bouge pas.
        AssailantSetup Wolf(string id, string x, string z, string aggro, bool pack = true) => new(id, "meute", Fixed.Zero, Fixed.Zero, null, 600_000, 1000,
            new[] { new AssailantAttack("morsure", 10, EdS.Domain.Stats.DamageType.Physique, SkillTargeting.Enemy, false, null, AttackShape.Target, range: F("1")) },
            placement: At(x, z), behavior: new AssailantBehavior(F("0.5"), aggroRange: F(aggro), pack: pack));
        CombatSetup setup = Fight(new CombatantSetup[]
        {
            Player("bastion", At("0", "0")), Wolf("loup1", "10", "0", "30"), Wolf("loup2", "10", "1.5", "1", pack: false), Wolf("loup3", "10", "-1.5", "1"),
        });
        var (track, log, _) = Trace(setup);
        var engaged = Lines(log, "engaged").Select(e => (e.Str("actor")!, e.Str("reason")!)).ToList();
        Assert.Equal(new[] { ("loup1", "aggro"), ("loup3", "pack") }, engaged);
        Assert.Equal(track.After("loup2", 0), track.After("loup2", 39));
        // Instantané : loup2 dit engagé (par la meute) est refusé, aucune cause ne l'a pu ; le vrai instantané se relit.
        var live = new CombatEngine(setup, NullCombatLog.Instance);
        while (live.CurrentTick < 10) live.Step();
        string text = live.Snapshot().Text;
        CombatSetup completed = live.CompletedSetup();
        Assert.Equal(text, CombatEngine.Restore(completed, CombatSnapshot.Parse(text), NullCombatLog.Instance).Snapshot().Text);
        string line = text.Split('\n').First(l => l.StartsWith("{\"fighter\":\"loup2\"", StringComparison.Ordinal));
        string forged = text.Replace(line, line.Replace("\"engaged\":false,\"engagedTick\":null", "\"engaged\":true,\"engagedTick\":5",
            StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.NotEqual(text, forged);
        Assert.Contains("Engagement de « loup2 » au tick 5 sans cause possible", Assert.Throws<FormatException>(() =>
            CombatEngine.Restore(completed, CombatSnapshot.Parse(forged), NullCombatLog.Instance)).Message);
    }
}
