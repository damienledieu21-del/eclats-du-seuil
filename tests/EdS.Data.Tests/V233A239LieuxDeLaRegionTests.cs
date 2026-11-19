using EdS.Data.Json;
using EdS.Data.Seasons;
using EdS.Data.Zones;
using EdS.Domain.Numerics;
using static EdS.Data.Tests.GrandeCarteLieux;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lots V2.33 à V2.39 de la grande carte, livrés « habités » : coteaux et château viticole (enceinte de haies, château, pressoir, chai,
/// jardin, rangs de vigne), hautes collines (bergerie, enclos, cairns, tour de signal), abbaye en ruine (enceinte, nef éventrée, cloître,
/// dortoir, jardin, statues, point sûr du parvis), marais du lac (cabanes sur pilotis, roselières, île des saules dormants), carrière
/// devenue mine (village des mineurs, gradins, treuil, bouche fermée), fort des bandits (palissade, tours, donjon, tentes, cage, tour de guet
/// isolée), terres corrompues (cercle de pierres levées, autels, éclats, arbres morts, failles).
/// </summary>
[Trait("Category", "World")]
public class V233A239LieuxDeLaRegionTests
{
    public static IEnumerable<object[]> Lieux() => new[]
    {
        new object[] { "_vignes", new[] { "coteaux_du_chateau" }, 13 },
        new object[] { "_bergers", new[] { "vallee_de_la_riviere", "foret_des_loups_profonde", "hautes_collines" }, 10 },
        new object[] { "_abbaye", new[] { "abbaye_en_ruine" }, 20 },
        new object[] { "_marais", new[] { "rives_du_lac", "grand_lac" }, 8 },
        new object[] { "_mine", new[] { "grande_carriere", "plaine_de_l_est", "village_des_mineurs" }, 9 },
        new object[] { "_fort", new[] { "fort_des_bandits", "route_du_col" }, 21 },
        new object[] { "_corrompu", new[] { "terres_corrompues" }, 22 },
    };

    [Theory]
    [MemberData(nameof(Lieux))]
    public void Obstacles_dans_leurs_lieux_et_routes_degagees(string tag, string[] places, int level)
    {
        InPlaces(tag, places);
        RoadsClear(tag);
        Assert.All(Tagged(tag), o => Assert.InRange(Vallons.Value.MinLevelAt(o.Points[0]), level, 22));
    }

    [Fact]
    public void Points_surs_et_lieux_nommes()
    {
        SafePointClear("parvis_de_l_abbaye");
        // Décision de Damien du 18 novembre 2026 : des points sûrs devant l'entrée de la mine et devant le fort.
        SafePointClear("bouche_de_la_mine");
        SafePointClear("devant_le_fort");
        Assert.Contains(Vallons.Value.PlacesAt(Vallons.Value.SafePoints.Single(s => s.Id == "bouche_de_la_mine").Position), p => p.Id == "carreau_de_la_mine");
        Assert.True(Walks((1100, 860), (1300, 1080), SafePoint("devant_le_fort"), (1220, 1028)));
        Assert.True(Walks((1000, 300), (1220, 400), SafePoint("bouche_de_la_mine"), (1185, 356)));
        ZoneDefinition zone = Vallons.Value;
        foreach ((string id, int level) in new[] { ("domaine_du_chateau", 13), ("vignes_du_chateau", 13), ("bergerie_des_collines", 10),
            ("cairns_de_la_crete", 17), ("tour_de_signal", 17), ("nef_de_l_abbaye", 20), ("cloitre_de_l_abbaye", 20), ("cabanes_sur_pilotis", 8),
            ("ile_des_saules_dormants", 11), ("village_des_mineurs", 9), ("carreau_de_la_mine", 18), ("enceinte_du_fort", 21),
            ("tour_des_bandits", 22), ("cercle_des_pierres_levees", 22) })
            Assert.Equal(level, zone.Places.Single(p => p.Id == id).MinLevel);
        Assert.Equal(24, Tagged("haie_vigne").Count);
    }

    [Fact]
    public void Chateau_fort_abbaye_et_mine_s_atteignent_a_pied_depuis_leur_chemin()
    {
        // Château : du point sûr, par le portail, jusqu'au logis.
        Assert.True(Walks((-1400, -760), (-1260, -640), SafePoint("chateau_viticole"), (-1330, -718)));
        // Fort : du pied du fort, par la porte sud, jusqu'au pied du donjon.
        Assert.True(Walks((1100, 860), (1300, 1080), SafePoint("pied_du_fort"), (1220, 1028)));
        // Abbaye : du parvis, dans la nef (portail ouest) et dans le préau du cloître (par la porterie sud ou la nef éventrée).
        Assert.True(Walks((1600, 60), (1820, 230), SafePoint("parvis_de_l_abbaye"), (1740, 146)));
        Assert.True(Walks((1600, 60), (1820, 230), SafePoint("parvis_de_l_abbaye"), (1735, 110)));
        // Mine : de la route d'Ambre au carreau, devant la bouche fermée ; la bouche elle-même est close.
        Assert.True(Walks((1000, 300), (1220, 400), (1050, 350), (1185, 356)));
        // Le point de vue du sommet reste dégagé (visite à 12 m) malgré la tour de signal.
        Assert.True(Walks((150, 1320), (240, 1400), (180, 1355), (180, 1360), 0.5));
    }

    [Fact]
    public void Ile_des_saules_dans_le_lac_et_verrou_d_hiver_accroche()
    {
        ZoneDefinition zone = Vallons.Value;
        Place ile = zone.Places.Single(p => p.Id == "ile_des_saules_dormants");
        // L'île n'est pas atteignable à pied hors de l'hiver : elle est dans l'eau du grand lac (le passage d'hiver est saisonnier).
        Assert.False(Walks((-300, -1360), (1150, -840), SafePoint("pontons_du_lac"), (180, -1230), 2));
        SeasonLoadResult seasons = SeasonJsonReader.ReadFile(SeasonJsonReader.PathIn(DataDirectory), zone);
        Assert.True(seasons.IsValid, seasons.Report.ToString());
        Assert.True(ile.Contains(seasons.Book!.Lock("ile_des_saules_dormants").Anchor!.Value));
    }
    [Fact]
    public void Creatures_des_lieux_du_bestiaire_existant()
    {
        // Décision de Damien du 16 novembre 2026 (vers 23 h) : les lieux sont peuplés avec le bestiaire existant, toujours là.
        ZoneDefinition zone = Vallons.Value;
        var expected = new (string Prefix, int Count, string Team, int Level)[]
        {
            ("sanglier_vignes", 2, "gibier", 13), ("cerf_bergers", 2, "gibier", 10), ("loup_crete_bergers", 3, "meute", 17),
            ("echo_abbaye", 3, "echos", 20), ("loup_marais", 2, "meute", 8), ("bandit_mine", 1, "bandits", 18), ("bandit_arc_mine", 1, "bandits", 18),
            ("bandit_fort", 3, "bandits", 21), ("bandit_arc_fort", 2, "bandits", 21), ("archer_tour_fort", 1, "bandits", 22), ("echo_corrompu", 3, "echos", 22),
        };
        foreach (var (prefix, count, team, level) in expected)
        {
            var found = zone.Setup.Combatants.OfType<EdS.Domain.Combat.AssailantSetup>().Where(c => c.Id.StartsWith(prefix, StringComparison.Ordinal)
                && c.Id.Length > prefix.Length && char.IsDigit(c.Id[prefix.Length])).ToList();
            Assert.True(count == found.Count, $"{prefix} : {found.Count}");
            Assert.All(found, c => Assert.Equal(team, c.Team));
            Assert.All(found, c => Assert.Equal(level, zone.MinLevelAt(c.Placement!.Position)));
            Assert.All(found, c => Assert.Null(zone.Populations.Single(p => p.Creature == c.Id).Hours));
        }
        Assert.Equal(43, zone.Populations.Count(p => IsLieu(p.Creature)));
    }

    [Fact]
    public void Creatures_de_nuit_des_lieux()
    {
        // Décision de Damien du 17 novembre 2026 (vers 1 h 10) : des créatures de nuit dans les lieux, avec le bestiaire de nuit existant
        // (loups de la nuit, échos) : la nuit seulement, sans annonce de plus.
        ZoneDefinition zone = Vallons.Value;
        var night = zone.Populations.Where(p => IsLieu(p.Creature) && p.Hours != null).ToList();
        Assert.Equal(13, night.Count);
        int nightOnly = zone.Populations.Single(q => q.Creature == "loup_nuit2").Hours!.Mask;
        Assert.All(night, p => Assert.Equal(nightOnly, p.Hours!.Mask));
        Assert.All(night, p => Assert.Null(p.Hours!.Sign));
        Assert.All(night, p => Assert.Contains(p.Family, new[] { "loup", "echo" }));
        foreach (string lieu in new[] { "_foret", "_chute", "_vignes", "_bergers", "_abbaye", "_marais", "_corrompu" })
            Assert.Contains(night, p => p.Creature.Contains(lieu, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(1800014400L, true)]    // 15 janvier 2027 : glace praticable
    [InlineData(1815652800L, false)]   // 15 juillet 2027 : eau
    public void Ile_des_saules_atteinte_a_pied_sur_la_glace_l_hiver_seulement(long utc, bool winter)
    {
        // Revue Codex de la PR #206 : un vrai chemin d'hiver : la bande de glace (passage saisonnier « glace_du_lac ») est retirée des
        // sessions de secteur ouvertes pendant que la glace est praticable ; l'été, elle reste de l'eau et l'île est hors d'atteinte.
        ZoneDefinition zone = Vallons.Value;
        Assert.Equal("riviere_glace_du_lac_marais", zone.Passages.Single(p => p.Id == "glace_du_lac").Obstacle);
        var w = new EdS.World.WorldSession(new EdS.Data.Saves.WorldSave(zone.Id, 0, 0, new[] { EdS.World.PlayerCharacters.PlacesKey }, V11TestData.Hero()),
            zone, ShippedData, V11TestData.Progression, V11TestData.ItemsCatalog, safePoint: "pontons_du_lac");
        w.UseSeasons(SeasonJsonReader.ReadFile(SeasonJsonReader.PathIn(DataDirectory), zone).Book);
        w.SetCivilTime(utc);
        w.Step();
        Assert.Equal(winter, w.PassageOpen("glace_du_lac"));
        Assert.Equal(winter, w.LockOpen("ile_des_saules_dormants"));
        V228b2PassageTests.WalkTo(w, 160, -840);
        if (winter)
        {
            V228b2PassageTests.WalkTo(w, 160, -1225);
            Assert.Contains(zone.PlacesAt(G(165, -1230)), p => p.Id == "ile_des_saules_dormants");
            Assert.False(w.IsPassageOpen("glace_du_lac"));   // jamais au registre : la saison passée, la glace redevient de l'eau
            Assert.DoesNotContain(w.Save().Consumed, k => k.Contains("glace_du_lac", StringComparison.Ordinal));
        }
        else Assert.Throws<Xunit.Sdk.XunitException>(() => V228b2PassageTests.WalkTo(w, 160, -1225));
    }

    [Fact]
    public void Fonte_et_gel_en_direct_le_joueur_sur_la_glace_va_sur_la_rive()
    {
        // Revue Codex de la PR #206 (P1) : au passage de février à mars, un joueur sur la glace : la session ouverte est renouvelée avec la
        // bande d'eau remise, et le joueur est porté au bout le plus proche de la bande (la rive nord) ; il n'atteint plus l'île. Au gel, une
        // session ouverte l'été s'ouvre de même en direct.
        ZoneDefinition zone = Vallons.Value;
        var seasons = SeasonJsonReader.ReadFile(SeasonJsonReader.PathIn(DataDirectory), zone).Book;
        EdS.World.WorldSession Open(long utc)
        {
            var w = new EdS.World.WorldSession(new EdS.Data.Saves.WorldSave(zone.Id, 0, 0, new[] { EdS.World.PlayerCharacters.PlacesKey }, V11TestData.Hero()),
                zone, ShippedData, V11TestData.Progression, V11TestData.ItemsCatalog, safePoint: "pontons_du_lac");
            w.UseSeasons(seasons);
            w.SetCivilTime(utc);
            w.Step();
            return w;
        }
        (double X, double Z) At(EdS.World.WorldSession w) => P(w.Zone.Engine.View(w.CombatantId).Position!.Value);
        // Fonte : sur la glace le 15 janvier, puis le 15 mars.
        var melt = Open(1800014400L);
        V228b2PassageTests.WalkTo(melt, 160, -840);
        V228b2PassageTests.WalkTo(melt, 160, -1030);
        melt.SetCivilTime(1805112000L);   // 15 mars 2027
        Assert.False(melt.PassageOpen("glace_du_lac"));
        melt.Step();
        melt.Step();
        (double x, double z) = At(melt);
        Assert.InRange(z, -849.9, -846);   // rive nord, au bout le plus proche de la bande
        Assert.InRange(x, 150, 170);
        Assert.Contains(melt.Zone.Setup.Space!.Obstacles, o => o.Id == "riviere_glace_du_lac_marais");
        Assert.Throws<Xunit.Sdk.XunitException>(() => V228b2PassageTests.WalkTo(melt, 160, -1225));
        // Gel : au bord de l'eau le 15 juillet, puis le 15 janvier : la glace porte, en direct.
        var freeze = Open(1815652800L);
        V228b2PassageTests.WalkTo(freeze, 160, -840);
        Assert.Contains(freeze.Zone.Setup.Space!.Obstacles, o => o.Id == "riviere_glace_du_lac_marais");
        freeze.SetCivilTime(1800014400L);
        freeze.Step();
        Assert.DoesNotContain(freeze.Zone.Setup.Space!.Obstacles, o => o.Id == "riviere_glace_du_lac_marais");
        V228b2PassageTests.WalkTo(freeze, 160, -1225);
    }

    [Fact]
    public void Fonte_le_joueur_reste_dans_le_secteur_ou_il_etait()
    {
        // Revue Codex de la PR #206 (2e tour) : à (160, −1 080), le joueur est dans le secteur s08_02 ; le bout sud de la bande (z ≈ −1 213)
        // est plus proche mais hors de ce secteur : il est porté au bout nord, qui y reste.
        ZoneDefinition zone = Vallons.Value;
        var map = EdS.World.SectorMap.For(zone);
        Assert.Equal("s08_02", map.OwnerOf(G(160, -1080)));
        Assert.NotEqual("s08_02", map.OwnerOf(G(160, -1213.6)));
        var w = new EdS.World.WorldSession(new EdS.Data.Saves.WorldSave(zone.Id, 0, 0, new[] { EdS.World.PlayerCharacters.PlacesKey }, V11TestData.Hero()),
            zone, ShippedData, V11TestData.Progression, V11TestData.ItemsCatalog, safePoint: "pontons_du_lac");
        w.UseSeasons(SeasonJsonReader.ReadFile(SeasonJsonReader.PathIn(DataDirectory), zone).Book);
        w.SetCivilTime(1800014400L);   // 15 janvier 2027
        w.Step();
        V228b2PassageTests.WalkTo(w, 160, -840);
        V228b2PassageTests.WalkTo(w, 160, -1080);
        Assert.Equal("s08_02", w.Sector);
        w.SetCivilTime(1805112000L);   // 15 mars 2027 : fonte
        w.Step();
        w.Step();
        (double x, double z) = P(w.Zone.Engine.View(w.CombatantId).Position!.Value);
        Assert.Equal("s08_02", w.Sector);
        Assert.InRange(z, -849.9, -846);   // bout nord de la bande, dans le même secteur
        Assert.Equal("s08_02", map.OwnerOf(G(x, z)));
        Assert.Contains(w.Zone.Setup.Space!.Obstacles, o => o.Id == "riviere_glace_du_lac_marais");
    }
}
