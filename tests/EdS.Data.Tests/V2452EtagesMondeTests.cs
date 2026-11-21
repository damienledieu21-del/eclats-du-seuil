using EdS.Data.Characters;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Scenarios;
using EdS.Data.Zones;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Chantier V2.45.2 (étages, PLAN_GRANDE_CARTE.md § 3) : un dessous dans le monde. Ses créatures sont là et vivent comme dehors, ses points
/// sûrs se découvrent et servent à la reprise, mais le voyage rapide n'y mène pas et l'on n'y entre pas à cheval (à l'abri, comme une pièce).
/// </summary>
[Trait("Category", "World")]
public class V2452EtagesMondeTests
{
    private static ItemCatalog Items => ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!;

    private static CharacterProgression Progression => ProgressionJsonReader.ReadFile(ProgressionJsonReader.PathIn(DataDirectory), ShippedData).Progression!;

    private static readonly Lazy<CombatSetup> Block = new(() => ReadVallonsBlock().Setup!);

    private static PointData Point(int x, int z) => new() { X = Fixed.FromInt(x), Z = Fixed.FromInt(z) };

    /// <summary>Zone d'essai sur le scénario des Vallons d'un bloc (le loup peuplé), sans région, à l'étage <paramref name="floor"/>.</summary>
    private static ZoneDefinition Zone(string floor, bool indoor = false)
    {
        var data = new ZoneData
        {
            Id = "zone.grotte",
            Name = "Grotte",
            Scenario = "zones/vallons",
            Entry = "place",
            Floor = floor,
            Indoor = indoor,
            SafePoints = new List<SafePointData?> { new() { Id = "place", Name = "Camp des mineurs", Position = Point(-18, -12) } },
            Places = new List<PlaceData?>(),
            Populations = new List<PopulationData?> { new() { Creature = "loup1", RespawnMs = 90000, LeashRange = Fixed.FromInt(20) } },
            Passages = new List<PassageData?>(),
        };
        var document = new ZoneDocument { SchemaVersion = ZoneValidator.CurrentSchemaVersion, Kind = ZoneValidator.Kind, ContentVersion = "0.6.0", Data = data };
        ZoneLoadResult r = ZoneValidator.Validate(document, Block.Value, Items);
        Assert.True(r.IsValid, r.Report.ToString());
        return r.Zone!;
    }

    private static WorldSession Open(ZoneDefinition zone) =>
        new(new WorldSave(zone.Id, 0, 0, Array.Empty<string>(),
            PlayerCharacters.Create("bastion", "class.bastion", Progression).WithMount(PlayerCharacters.Horse)), zone, ShippedData, Progression, Items);

    [Fact]
    public void Dessous_avec_ses_creatures_et_ses_points_surs()
    {
        ZoneDefinition under = Zone("dessous");
        WorldSession session = Open(under);
        Assert.True(session.Zone.Engine.View("loup1").IsPresent);   // les créatures du dessous sont là
        Assert.True(session.IsDiscovered(under, "place"));          // son point sûr se découvre
        Assert.Equal("place", session.LastSafePoint);               // et sert à la reprise (V2.28)
        for (int i = 0; i < 40; i++) session.Step();
        Assert.False(session.Zone.Engine.View("loup1").IsDead);
    }

    [Fact]
    public void Pas_de_cheval_au_dessous()
    {
        Assert.True(Open(Zone("surface")).CanCallMount);
        Assert.False(Open(Zone("dessous")).CanCallMount);
        Assert.False(Open(Zone("surface", indoor: true)).CanCallMount);   // comme avant dans une pièce
    }

    [Fact]
    public void Le_voyage_rapide_ne_mene_pas_au_dessous()
    {
        ZoneDefinition surface = Zone("surface"), under = Zone("dessous");
        WorldSession top = Open(surface);
        top.Step();   // le personnage entre au premier tick
        Assert.Equal(TravelRefusal.AlreadyHere, top.CanTravel(surface, "place"));
        WorldSession below = Open(under);
        below.Step();
        Assert.True(below.IsDiscovered(under, "place"));
        Assert.Equal(TravelRefusal.UnknownPlace, below.CanTravel(under, "place"));   // découvert, mais pas une destination
        Assert.True(under.Sheltered && !surface.Sheltered);
    }
}
