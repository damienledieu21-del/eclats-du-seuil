using EdS.Data.Zones;
using static EdS.Data.Tests.GrandeCarteLieux;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.40 de la grande carte : les trois passages vers les régions voisines et la route de l'Est, fermés par une barrière expliquée
/// (passages de zone jamais ouverts pour l'instant) : éboulement du col des Crêtes de Fer (arche, refuge et son point sûr), péage d'Havre
/// d'Ambre, pont des Murmures gardé au bord de la gorge, pont effondré de la route de l'Est vers les Ruines d'Ysoria. Porte nord du Bourg
/// (décision de Damien du 16 novembre 2026) et chemin du pont des Murmures, coupé au ravin des Ronces par la passerelle effondrée : le verrou
/// du chapitre 1 tient.
/// </summary>
[Trait("Category", "World")]
public class V240PassagesTests
{
    private static readonly string[] Barrieres = { "passerelle_des_ronces", "pont_des_murmures", "col_des_cretes_de_fer", "peage_d_havre", "pont_effondre" };

    [Fact]
    public void Passages_fermes_lieux_et_routes()
    {
        ZoneDefinition zone = Vallons.Value;
        foreach (string id in Barrieres)
        {
            Passage p = zone.Passages.Single(x => x.Id == id);
            Assert.EndsWith("_passage", p.Obstacle, StringComparison.Ordinal);
            Assert.Contains("(", p.Name, StringComparison.Ordinal);   // la raison de la fermeture, dans le nom
        }
        InPlaces("_passage", "foret_des_loups_profonde", "passerelle_des_ronces", "pont_des_murmures", "col_des_cretes_de_fer", "poste_de_peage",
            "pont_effondre", "route_de_l_est", "route_du_col", "route_d_havre_d_ambre");
        RoadsClear("_passage");
        foreach (string id in new[] { "rue_de_la_porte_nord", "chemin_du_pont_des_murmures", "route_de_l_est" }) Assert.Contains(zone.Roads, r => r.Id == id);
    }

    [Fact]
    public void Porte_nord_du_marche_au_ravin_mais_pas_dans_la_foret()
    {
        // De la place du marché, par la rue de la porte nord, jusqu'au bord du ravin des Ronces ; la passerelle fermée ne laisse pas passer,
        // ouverte elle mène à la forêt (le jour où elle s'ouvrira).
        (double X, double Z) min = (-1100, -120), max = (-880, 320);
        Assert.True(Walks(min, max, SafePoint("place_du_marche_de_la_ville"), (-990, 270)));
        Assert.False(Walks(min, max, SafePoint("place_du_marche_de_la_ville"), (-990, 290)));
        Assert.True(Walks(min, max, SafePoint("place_du_marche_de_la_ville"), (-990, 290), 1, "ravin_passerelle_des_ronces_passage"));
    }

    [Fact]
    public void Barrieres_fermees_sur_les_routes()
    {
        // Chaque route vers un bord (et le chemin du pont) est coupée par sa barrière : un point de la route est dans l'obstacle du passage.
        foreach ((string road, string gate) in new[] { ("route_d_ambre_est", "eboulement_col_passage"), ("route_d_ambre_ouest", "porte_peage_passage"),
            ("route_de_l_est", "ravin_pont_effondre_passage"), ("chemin_du_pont_des_murmures", "porte_pont_des_murmures_passage"),
            ("chemin_du_pont_des_murmures", "ravin_passerelle_des_ronces_passage") })
        {
            var shape = Shape(Vallons.Value.Setup.Space!.Obstacles.Single(o => o.Id == gate));
            var pts = Vallons.Value.Roads.Single(r => r.Id == road).Points.Select(P).ToList();
            bool cut = false;
            for (int i = 0; i + 1 < pts.Count && !cut; i++)
                for (double t = 0; t <= 1 && !cut; t += 0.002)
                    cut = EdS.Runtime.GroundRelief.PolygonDistance(shape, pts[i].X + (pts[i + 1].X - pts[i].X) * t, pts[i].Z + (pts[i + 1].Z - pts[i].Z) * t) == 0;
            Assert.True(cut, $"{road} n'est pas coupée par {gate}");
        }
    }

    [Fact]
    public void Creatures_des_passages()
    {
        // Décisions de Damien du 16 novembre 2026 (23 h) et du 17 novembre (1 h 10) : bestiaire existant, de jour et de nuit.
        ZoneDefinition zone = Vallons.Value;
        var mine = zone.Populations.Where(p => p.Creature.Contains("_passage", StringComparison.Ordinal)).ToList();
        Assert.Equal(9, mine.Count);
        Assert.Equal(3, mine.Count(p => p.Hours != null));
        Assert.All(mine, p => Assert.InRange(zone.MinLevelAt(zone.Setup.Combatants.Single(c => c.Id == p.Creature).Placement!.Position), 7, 22));
    }

    [Fact]
    public void Point_sur_des_lacets_du_col_et_passerelle_ouverte_a_la_fin_du_chapitre_1()
    {
        // Décisions de Damien du 18 novembre 2026 : un point sûr plus bas sur la route du col ; la passerelle des Ronces s'ouvre avec la
        // quête qui termine le chapitre 1 (une sauvegarde où elle est déjà finie l'ouvre en reprenant, comme le pont suspendu).
        SafePointClear("lacets_du_col");
        ZoneDefinition zone = Vallons.Value;
        EdS.Data.Story.StoryBook book = V11TestData.Book(zone);
        Assert.Contains(book.Quests.Single(q => q.Id == "quest.traces_sous_le_moulin").Effects,
            e => e.Kind == EdS.Data.Story.EffectKind.OpenPassage && e.Id == "passerelle_des_ronces");
        var quests = new Dictionary<string, EdS.Data.Saves.QuestProgress>(StringComparer.Ordinal)
        {
            ["quest.traces_sous_le_moulin"] = new(EdS.Data.Saves.QuestProgress.Done),
        };
        var hero = V11TestData.Hero().WithStory(new EdS.Data.Saves.SavedStory(quests: quests));
        var save = new EdS.Data.Saves.WorldSave(zone.Id, 0, 0, new[] { EdS.World.PlayerCharacters.PlacesKey }, hero);
        var w = new EdS.World.WorldSession(save, zone, ShippedData, V11TestData.Progression, V11TestData.ItemsCatalog, book, safePoint: "place_du_marche_de_la_ville");
        w.Step();
        Assert.True(w.IsPassageOpen("passerelle_des_ronces"));
        Assert.Contains(zone.PassageKey("passerelle_des_ronces"), w.Save().Consumed);
        var fresh = new EdS.World.WorldSession(new EdS.Data.Saves.WorldSave(zone.Id, 0, 0, new[] { EdS.World.PlayerCharacters.PlacesKey }, V11TestData.Hero()),
            zone, ShippedData, V11TestData.Progression, V11TestData.ItemsCatalog, book, safePoint: "place_du_marche_de_la_ville");
        fresh.Step();
        Assert.False(fresh.IsPassageOpen("passerelle_des_ronces"));
    }
}
