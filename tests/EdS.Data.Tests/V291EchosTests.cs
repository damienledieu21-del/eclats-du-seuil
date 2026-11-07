using System;
using System.IO;
using System.Linq;
using EdS.Data.Json;
using EdS.Data.Zones;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.World;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.9.1 (échos fantômes et failles, affichage seulement) : la sorte d'écho de chaque combattant connu des Vallons et du
/// souvenir, et les réglages purs de l'affichage (solidité, dissolution, failles).
/// </summary>
[Trait("Category", "World")]
public sealed class V291EchosTests
{
    [Theory]
    [InlineData("loup_echo1", EchoKind.Night)]
    [InlineData("loup_echo6_v3", EchoKind.Night)]
    [InlineData("loup_echo1_ev4", EchoKind.Rift)]
    [InlineData("LOUP_ECHO1_EV12", EchoKind.Rift)]
    [InlineData("loup_blanc", EchoKind.WhiteWolf)]
    [InlineData("loup_blanc_v2", EchoKind.WhiteWolf)]
    [InlineData("echo_de_la_rupture", EchoKind.Rupture)]
    [InlineData("voix_de_la_rupture", EchoKind.Rupture)]
    [InlineData("loup3_v2", EchoKind.None)]   // revue Codex de la PR #115 : le suffixe seul ne dit pas une mort
    [InlineData("sanglier_v4", EchoKind.None)]
    [InlineData("croc_gris_v2", EchoKind.None)]
    [InlineData("croc_gris_enrage_v3", EchoKind.None)]
    [InlineData("loup_nuit1_v2", EchoKind.None)]   // première arrivée d'un loup de la nuit, partie commencée de jour
    [InlineData("loup3", EchoKind.None)]
    [InlineData("loup_nuit5_ev2", EchoKind.None)]
    [InlineData("croc_gris", EchoKind.None)]
    [InlineData("bandit_epee3_v2", EchoKind.None)]
    [InlineData("rodeur_v2", EchoKind.None)]
    [InlineData("cerf4_v2", EchoKind.None)]
    [InlineData("lievre1_v3", EchoKind.None)]
    [InlineData("loup_v", EchoKind.None)]
    [InlineData("loup3_vx", EchoKind.None)]
    [InlineData("gardien_du_seuil", EchoKind.None)]
    [InlineData("", EchoKind.None)]
    [InlineData(null, EchoKind.None)]
    public void Sorte_d_echo_d_apres_l_identite(string? id, EchoKind expected)
    {
        Assert.Equal(expected, EchoLooks.Of(id));
        Assert.Equal(expected != EchoKind.None, EchoLooks.IsEcho(id));
    }

    [Fact]
    public void Decision_du_7_novembre_ce_qui_sort_du_Seuil_et_le_chef_deja_tue()
    {
        // Le gibier qui revient n'est pas un écho (la forêt se repeuple) ; sorti d'une faille ou d'une cicatrice, si.
        Assert.Equal(EchoKind.None, EchoLooks.Of("cerf2_v3"));
        Assert.Equal(EchoKind.Rift, EchoLooks.Of("cerf2_ev1", fromSeuil: true));
        Assert.Equal(EchoKind.Rift, EchoLooks.Of("lievre1", fromSeuil: true));
        Assert.Equal(EchoKind.Rift, EchoLooks.Of("loup_nuit5_ev2", fromSeuil: true));
        Assert.Equal(EchoKind.None, EchoLooks.Of("loup_nuit5_ev2"));   // attaque du hameau, chasse de l'aube : pas du Seuil
        // Un chef déjà tué qu'on recombat : écho ; un chef contre qui on a perdu (jamais mort) : non.
        Assert.Equal(EchoKind.Returned, EchoLooks.Of("croc_gris", slainBefore: true));
        Assert.Equal(EchoKind.None, EchoLooks.Of("croc_gris", slainBefore: false));
        // Les sortes propres gardent la leur.
        Assert.Equal(EchoKind.WhiteWolf, EchoLooks.Of("loup_blanc", true, true));
        Assert.Equal(EchoKind.Rupture, EchoLooks.Of("echo_de_la_rupture", true, true));
        Assert.True(EchoLooks.IsEcho("croc_gris", slainBefore: true) && !EchoLooks.IsEcho("croc_gris"));
        // Revue Codex de la PR #115 : une bête revenue après une vraie mort (WorldSession.ReturnedFromDeath) : écho ; pas le gibier ni les hommes.
        Assert.Equal(EchoKind.Returned, EchoLooks.Of("loup3_v2", afterDeath: true));
        Assert.Equal(EchoKind.Returned, EchoLooks.Of("sanglier_v4", afterDeath: true));
        Assert.Equal(EchoKind.Returned, EchoLooks.Of("loup_nuit5_v2", afterDeath: true));
        Assert.Equal(EchoKind.None, EchoLooks.Of("cerf4_v2", afterDeath: true));
        Assert.Equal(EchoKind.None, EchoLooks.Of("bandit_epee3_v2", afterDeath: true));
        Assert.Equal(EchoKind.None, EchoLooks.Of("loup_nuit5_v2"));
    }

    [Fact]
    public void Chef_tue_d_apres_son_fait_de_victoire()
    {
        ZoneDefinition zone = Vallons();
        Func<string, bool> slain = fact => fact == "fact.croc_gris.vaincu" || fact == "fact.chef.vaincu";
        Func<string, bool> none = _ => false;
        Assert.True(EchoLooks.ChiefSlain(zone, slain, "croc_gris"));
        Assert.True(EchoLooks.ChiefSlain(zone, slain, "croc_gris_enrage"));
        Assert.True(EchoLooks.ChiefSlain(zone, slain, "croc_gris_enrage_v2"));
        Assert.True(EchoLooks.ChiefSlain(zone, slain, "gardien_eveille"));
        Assert.False(EchoLooks.ChiefSlain(zone, slain, "chef_des_bandits"));   // son fait n'est pas connu
        Assert.False(EchoLooks.ChiefSlain(zone, none, "croc_gris"));          // perdu contre lui : jamais mort
        Assert.False(EchoLooks.ChiefSlain(zone, slain, "loup3"));
        Assert.False(EchoLooks.ChiefSlain(zone, slain, "croc_gris_v"));
        Assert.False(EchoLooks.ChiefSlain(null, slain, "croc_gris"));
        Assert.False(EchoLooks.ChiefSlain(zone, null, "croc_gris"));
        Assert.False(EchoLooks.ChiefSlain(zone, slain, null));
    }

    [Fact]
    public void Les_echos_de_la_nuit_restent_ceux_de_la_V21()
    {
        foreach (string id in new[] { "loup_echo1", "loup_echo2_v3", "loup_echo1_ev2" })
            Assert.True(NightLamps.IsEcho(id) && EchoLooks.IsEcho(id), id);
    }

    [Fact]
    public void Solidite_bornee_plus_forte_la_nuit_pres_d_une_cicatrice_et_en_pleine_vie()
    {
        foreach (EchoKind kind in Enum.GetValues(typeof(EchoKind)))
            for (int stage = 0; stage <= 6; stage++)
                for (double h = -0.5; h <= 1.5; h += 0.25)
                    for (double n = -1; n <= 2; n += 0.5)
                    {
                        double s = EchoLooks.Solidity(kind, h, stage, n);
                        Assert.InRange(s, EchoLooks.MinSolidity, 1.0);
                    }
        Assert.True(EchoLooks.Solidity(EchoKind.Night, 1, 0, 1) > EchoLooks.Solidity(EchoKind.Night, 1, 0, 0));
        Assert.True(EchoLooks.Solidity(EchoKind.Rift, 1, 3, 0) > EchoLooks.Solidity(EchoKind.Rift, 1, 0, 0));
        Assert.True(EchoLooks.Solidity(EchoKind.Returned, 1, 0, 0) > EchoLooks.Solidity(EchoKind.Returned, 0.1, 0, 0));
        Assert.True(EchoLooks.Solidity(EchoKind.Rupture, 1, 0, 0) > EchoLooks.Solidity(EchoKind.Returned, 1, 0, 0));
        Assert.True(EchoLooks.Glow(2, 1) > EchoLooks.Glow(0, 0));
    }

    [Fact]
    public void Dissolution_a_l_apparition_puis_a_la_mort()
    {
        Assert.Equal(1.0, EchoLooks.Dissolve(0, null), 6);
        Assert.Equal(1.0, EchoLooks.Dissolve(-3, null), 6);
        Assert.Equal(0.0, EchoLooks.Dissolve(EchoLooks.AppearSeconds, null), 6);
        Assert.Equal(0.0, EchoLooks.Dissolve(100, null), 6);
        Assert.InRange(EchoLooks.Dissolve(EchoLooks.AppearSeconds / 2, null), 0.4, 0.6);
        Assert.Equal(0.0, EchoLooks.Dissolve(100, 0), 6);
        Assert.Equal(1.0, EchoLooks.Dissolve(100, EchoLooks.VanishSeconds), 6);
        Assert.Equal(1.0, EchoLooks.Dissolve(100, 60), 6);
        double last = -1;
        for (double t = 0; t <= EchoLooks.VanishSeconds; t += 0.1)
        {
            double d = EchoLooks.Dissolve(5, t);
            Assert.True(d >= last);
            last = d;
        }
    }

    [Fact]
    public void Failles_et_cicatrices_grandissent_avec_le_palier()
    {
        Assert.Equal(0, EchoLooks.ScarStageAt(null, 0, 0));
        Assert.Equal(0, EchoLooks.ScarStageAt(Array.Empty<ScarView>(), 0, 0));
        var previous = EchoLooks.Rift(0, false);
        Assert.Equal(1.0, previous.Scale, 6);
        for (int stage = 1; stage <= 4; stage++)
        {
            var r = EchoLooks.Rift(stage, false);
            Assert.True(r.Scale > previous.Scale && r.PulseHz > previous.PulseHz && r.PullRate > previous.PullRate && r.Light > previous.Light);
            Assert.Equal(r.PullRate / 2, EchoLooks.Rift(stage, true).PullRate, 6);
            Assert.True(EchoLooks.ScarOpening(stage) > EchoLooks.ScarOpening(stage - 1));
            previous = r;
        }
        Assert.Equal(EchoLooks.Rift(8, false), EchoLooks.Rift(50, false));
        Assert.Equal(EchoLooks.Rift(0, false), EchoLooks.Rift(-2, false));
        for (double t = 0; t < 10; t += 0.05) Assert.InRange(EchoLooks.Pulse(t, 1.4), 0.6 - 1e-9, 1.4 + 1e-9);
        Assert.InRange(EchoLooks.ScarOpening(10), 0, 1);
    }

    [Fact]
    public void Trainee_discrete_moitie_moins_sur_telephone()
    {
        Assert.Equal(0, EchoLooks.TrailRate(EchoKind.None, 1, false));
        Assert.Equal(EchoLooks.TrailRate(EchoKind.Night, 0.5, false) / 2, EchoLooks.TrailRate(EchoKind.Night, 0.5, true), 6);
        Assert.True(EchoLooks.TrailRate(EchoKind.Night, 1, false) > EchoLooks.TrailRate(EchoKind.Night, 0, false));
        Assert.InRange(EchoLooks.TrailRate(EchoKind.Rupture, 1, false), 1, 10);
    }

    private static ZoneDefinition Vallons() => ZoneJsonReader.ReadFile(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons"),
        Path.Combine(AppContext.BaseDirectory, "scenarios"), ShippedData, ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!).Zone!;

    private static ScarView Scar(WorldEventDefinition e, string key, double x, double z, double radius, int stage) =>
        new ScarView(key, e, e.Groups[0], new GroundVector(Fixed.FromRaw((long)(x * 10000)), Fixed.FromRaw((long)(z * 10000))), stage,
            Fixed.FromRaw((long)(radius * 10000)), 0, null, Array.Empty<string>(), false);

    [Fact]
    public void Palier_de_la_cicatrice_la_plus_forte_qui_couvre_la_place()
    {
        WorldEventDefinition rift = Vallons().Events.Single(e => e.Id == "faille_du_seuil");
        // Deux cicatrices qui se recouvrent : (0, 0) rayon 10 au palier 1, (8, 0) rayon 6 au palier 3.
        var scars = new[] { Scar(rift, "a", 0, 0, 10, 1), Scar(rift, "b", 8, 0, 6, 3) };
        Assert.Equal(1, EchoLooks.ScarStageAt(scars, -5, 0));     // dans la première seule
        Assert.Equal(3, EchoLooks.ScarStageAt(scars, 6, 0));      // dans les deux : la plus forte
        Assert.Equal(3, EchoLooks.ScarStageAt(scars.Reverse().ToArray(), 6, 0));   // l'ordre ne compte pas
        Assert.Equal(1, EchoLooks.ScarStageAt(scars, -10, 0));    // au bord du rayon : dedans
        Assert.Equal(3, EchoLooks.ScarStageAt(scars, 14, 0));     // au bord de la seconde
        Assert.Equal(0, EchoLooks.ScarStageAt(scars, -10.01, 0)); // juste dehors
        Assert.Equal(0, EchoLooks.ScarStageAt(scars, 0, 30));     // loin
    }

    private static string Unity(string file) =>
        File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", file));

    private static string Between(string text, string start, string end)
    {
        int i = text.IndexOf(start, StringComparison.Ordinal);
        Assert.True(i >= 0, start);
        int j = text.IndexOf(end, i + start.Length, StringComparison.Ordinal);
        Assert.True(j > i, end);
        return text.Substring(i, j - i);
    }

    [Fact]
    public void Contexte_des_echos_donne_avant_que_l_arene_habille_les_corps()
    {
        // Revue Codex de la PR #115 : Populate habille les échos une seule fois ; le contexte (chefs déjà tués, créatures du Seuil) doit
        // être donné avant, sinon un chef vaincu d'une partie chargée resterait ordinaire.
        string game = Unity("EdsGame.cs");
        string show = Between(game, "_arena.Paint = id => _adventure == null", "_shownSession = _adventure.Opened;");
        int context = show.IndexOf("EchoContext(_adventure);", StringComparison.Ordinal), populate = show.IndexOf("_arena.Populate(_combat, _adventure.Map);", StringComparison.Ordinal);
        Assert.True(context >= 0 && populate > context);
        string frame = Between(game, "_stage?.BeforeSync();", "_arena.Sync(_combat, player, null);");
        Assert.Contains("EchoContext(a);", frame, StringComparison.Ordinal);
        Assert.Contains("_arena.Echoes(a.IsNight, a.InScene ? null : a.Scars, _fromSeuil, _slainChief, _returned);",
            Between(game, "private void EchoContext(AdventureSession a)", "private void SeuilCreatures"), StringComparison.Ordinal);
    }

    [Fact]
    public void Formes_simples_du_gibier_faites_avant_l_habit_d_echo()
    {
        // Revue Codex de la PR #115 : un gibier sorti du Seuil sans modèle reçoit ses formes simples avant Dress (sinon seule sa capsule,
        // cachée ensuite, serait fantôme).
        string look = Between(Unity("ArenaView.cs"), "private void Look(string id, GameObject body, bool player)", "\n        }");
        int wild = look.IndexOf("new WildlifeLook(body, kind)", StringComparison.Ordinal), dress = look.IndexOf("_ghosts.Dress(", StringComparison.Ordinal);
        Assert.True(wild >= 0 && dress > wild);
    }
}
