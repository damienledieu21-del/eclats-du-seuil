using System.Text.Json.Nodes;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Domain.Combat;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Chantier V2.45.1 (étages, PLAN_GRANDE_CARTE.md § 3) : zones en schéma 15. Étage d'une zone (<c>floor</c> : surface ou dessous), genre
/// d'une porte (<c>kind</c>), créatures d'un dessous à 8 m au moins de chaque porte, portes qui se répondent du même genre, bornes des portes
/// et des zones reliées relevées ; la version 14 reste lue avec ses bornes.
/// </summary>
[Trait("Category", "World")]
public class V2451EtagesTests
{
    private static string Scenarios => Path.Combine(AppContext.BaseDirectory, "scenarios");

    private static ItemCatalog Items => ItemJsonReader.ReadFile(ItemJsonReader.PathIn(DataDirectory), ShippedData).Catalog!;

    private static readonly Lazy<CombatSetup> Block = new(() => ReadVallonsBlock().Setup!);

    private static GroundVector Wolf => Block.Value.Combatants.Single(c => c.Id == "loup1").Placement!.Position;

    private static readonly GroundVector Place = new(Fixed.FromInt(-18), Fixed.FromInt(-12));

    private static PointData Point(GroundVector p) => new() { X = p.X, Z = p.Z };

    private static DoorData DoorTo(string id, string to, string toDoor, GroundVector? at = null, string? kind = null) =>
        new() { Id = id, Name = "Passage", Position = Point(at ?? Place), To = to, ToDoor = toDoor, Kind = kind };

    /// <summary>Zone d'essai sur le scénario des Vallons d'un bloc (le loup peuplé), sans région.</summary>
    private static ZoneDocument Dto(string id = "zone.grotte", Action<ZoneData>? edit = null, int schema = ZoneValidator.CurrentSchemaVersion)
    {
        var data = new ZoneData
        {
            Id = id,
            Name = "Grotte",
            Scenario = "zones/vallons",
            Entry = "place",
            SafePoints = new List<SafePointData?> { new() { Id = "place", Name = "Place", Position = Point(Place) } },
            Places = new List<PlaceData?>(),
            Populations = new List<PopulationData?> { new() { Creature = "loup1", RespawnMs = 90000, LeashRange = Fixed.FromInt(20) } },
            Passages = new List<PassageData?>(),
        };
        edit?.Invoke(data);
        return new ZoneDocument { SchemaVersion = schema, Kind = ZoneValidator.Kind, ContentVersion = "0.6.0", Data = data };
    }

    private static ZoneLoadResult Validate(ZoneDocument document) => ZoneValidator.Validate(document, Block.Value, Items);

    private static void AssertError(ZoneLoadResult result, string path, ValidationCode code) =>
        Assert.Contains(result.Report.Errors, e => e.Path == path && e.Code == code);

    [Fact]
    public void Versions_et_bornes()
    {
        Assert.Equal((16, 14, 13), (ZoneValidator.CurrentSchemaVersion, ZoneValidator.TerritorySchemaVersion, ZoneValidator.RegionSchemaVersion));
        Assert.Equal((64, 16, 256), (ZoneValidator.MaxDoors, ZoneValidator.MaxDoorsBefore15, ZoneValidator.MaxLinkedZones));
        Assert.Equal((512, 256, 32, 16), (ZoneValidator.MaxRegionDoors, ZoneValidator.MaxRegionDoorsBefore15, ZoneValidator.MaxDoorsPerCell, ZoneValidator.MaxDoorsPerCellBefore15));
        Assert.Equal(Fixed.FromInt(8), ZoneValidator.MinDoorClearance);
    }

    [Fact]
    public void Etage_surface_par_defaut_dessous_abrite()
    {
        ZoneDefinition plain = Validate(Dto()).Zone!;
        Assert.Equal(ZoneFloor.Surface, plain.Floor);
        Assert.False(plain.Underground || plain.Sheltered);

        ZoneLoadResult surface = Validate(Dto(edit: d => d.Floor = "surface"));
        Assert.True(surface.IsValid, surface.Report.ToString());
        Assert.Equal(ZoneFloor.Surface, surface.Zone!.Floor);

        ZoneLoadResult under = Validate(Dto(edit: d => d.Floor = "dessous"));
        Assert.True(under.IsValid, under.Report.ToString());
        Assert.True(under.Zone!.Underground && under.Zone.Sheltered && !under.Zone.Indoor);
        Assert.Single(under.Zone.Populations);   // un dessous a ses créatures
        // L'étage survit aux copies de la zone (niveau du joueur).
        Assert.Equal(ZoneFloor.Underground, under.Zone.ForLevel(5).Floor);

        ZoneDefinition indoor = Validate(Dto(edit: d => d.Indoor = true)).Zone!;
        Assert.True(indoor.Sheltered && !indoor.Underground);
    }

    [Fact]
    public void Etage_refuse()
    {
        AssertError(Validate(Dto(edit: d => d.Floor = "envers")), "$.data.floor", ValidationCode.InvalidValue);   // l'Envers viendra au V2.51
        AssertError(Validate(Dto(edit: d => d.Floor = "Dessous")), "$.data.floor", ValidationCode.InvalidValue);
        AssertError(Validate(Dto(edit: d => d.Floor = "dessous", schema: 14)), "$.data.floor", ValidationCode.InvalidValue);
        AssertError(Validate(Dto(edit: d => d.Floor = "surface", schema: 14)), "$.data.floor", ValidationCode.InvalidValue);
        ZoneLoadResult both = Validate(Dto(edit: d => { d.Floor = "dessous"; d.Indoor = true; }));
        AssertError(both, "$.data.indoor", ValidationCode.InvalidValue);
        Assert.Null(both.Zone);   // refus entier
        Assert.True(Validate(Dto(edit: d => { d.Floor = "dessous"; d.Indoor = false; })).IsValid);
    }

    [Fact]
    public void Genre_de_porte()
    {
        ZoneDefinition plain = Validate(Dto(edit: d => d.Doors = new List<DoorData?> { DoorTo("bas", "zone.cave", "haut") })).Zone!;
        Assert.Equal(DoorKind.Door, plain.Doors[0].Kind);
        string[] names = { "porte", "escalier", "puits", "trappe", "faille" };
        DoorKind[] kinds = { DoorKind.Door, DoorKind.Stairs, DoorKind.Shaft, DoorKind.Hatch, DoorKind.Rift };
        for (int i = 0; i < names.Length; i++)
        {
            ZoneLoadResult r = Validate(Dto(edit: d => d.Doors = new List<DoorData?> { DoorTo("bas", "zone.cave", "haut", kind: names[i]) }));
            Assert.True(r.IsValid, r.Report.ToString());
            Assert.Equal(kinds[i], r.Zone!.Doors[0].Kind);
            Assert.Equal(names[i], ZoneValidator.KindName(kinds[i]));
        }
        AssertError(Validate(Dto(edit: d => d.Doors = new List<DoorData?> { DoorTo("bas", "zone.cave", "haut", kind: "echelle") })),
            "$.data.doors[0].kind", ValidationCode.InvalidValue);
        AssertError(Validate(Dto(edit: d => d.Doors = new List<DoorData?> { DoorTo("bas", "zone.cave", "haut", kind: "escalier") }, schema: 14)),
            "$.data.doors[0].kind", ValidationCode.InvalidValue);
        Assert.True(Validate(Dto(edit: d => d.Doors = new List<DoorData?> { DoorTo("bas", "zone.cave", "haut") }, schema: 14)).IsValid);   // sans genre : lu en 14
    }

    [Fact]
    public void Creatures_du_dessous_loin_des_portes()
    {
        GroundVector wolf = Wolf;
        GroundVector At(string dz) => new(wolf.X, wolf.Z + Fixed.Parse(dz));
        ZoneDocument Near(string dz, string floor) => Dto(edit: d =>
        {
            d.Floor = floor;
            d.Doors = new List<DoorData?> { DoorTo("haut", "zone.vallons", "bas", At(dz)) };
        });
        // Frontière exacte : 8 m entre la place du loup et la porte passent, 7,9999 m non.
        ZoneLoadResult exact = Validate(Near("8", "dessous"));
        Assert.True(exact.IsValid, exact.Report.ToString());
        ZoneLoadResult close = Validate(Near("7.9999", "dessous"));
        AssertError(close, "$.data.populations[0].creature", ValidationCode.OutOfRange);
        Assert.Null(close.Zone);
        ZoneLoadResult west = Validate(Dto(edit: d =>
        {
            d.Floor = "dessous";
            d.Doors = new List<DoorData?> { DoorTo("haut", "zone.vallons", "bas", new GroundVector(wolf.X - Fixed.Parse("7.9999"), wolf.Z)) };
        }));
        AssertError(west, "$.data.populations[0].creature", ValidationCode.OutOfRange);
        // À la surface, la règle d'avant ne change pas.
        Assert.True(Validate(Near("7.9999", "surface")).IsValid);
        Assert.True(ZoneValidator.Closer(Place, new GroundVector(Place.X + Fixed.FromRaw(79_999), Place.Z), ZoneValidator.MinDoorClearance));
        Assert.False(ZoneValidator.Closer(Place, new GroundVector(Place.X + Fixed.FromRaw(80_000), Place.Z), ZoneValidator.MinDoorClearance));
    }

    [Fact]
    public void Bornes_des_portes_par_schema()
    {
        List<DoorData?> Many(int n) => Enumerable.Range(0, n).Select(i => (DoorData?)DoorTo("porte_" + i, "zone.cave_" + i, "haut")).ToList();
        Assert.True(Validate(Dto(edit: d => d.Doors = Many(ZoneValidator.MaxDoors))).IsValid);
        AssertError(Validate(Dto(edit: d => d.Doors = Many(ZoneValidator.MaxDoors + 1))), "$.data.doors", ValidationCode.LimitExceeded);
        Assert.True(Validate(Dto(edit: d => d.Doors = Many(ZoneValidator.MaxDoorsBefore15), schema: 14)).IsValid);
        AssertError(Validate(Dto(edit: d => d.Doors = Many(ZoneValidator.MaxDoorsBefore15 + 1), schema: 14)), "$.data.doors", ValidationCode.LimitExceeded);
    }

    /// <summary>Relecture indépendante : la règle des 8 m vaut aussi pour la forme de départ du boss et des chefs.</summary>
    [Theory]
    [InlineData("boss", "zones/vallons_chef", "gardien_du_seuil", "$.data.boss.forms[0]")]
    [InlineData("chef", "zones/vallons_bandits", "chef_des_bandits", "$.data.chiefs[0].forms[0]")]
    public void Chef_et_boss_du_dessous_loin_des_portes(string kind, string scenarioPath, string form, string errorPath)
    {
        CombatSetup forms = ScenarioJsonReader.ReadFile(ScenarioPath(scenarioPath), ShippedData).Setup!;
        GroundVector start = forms.Combatants.OfType<AssailantSetup>().Single(c => c.Id == form).Placement!.Position;
        ZoneDocument Near(string dx, string floor) => Dto(edit: d =>
        {
            d.Floor = floor;
            var data = new BossData { Id = "chef_d_essai", Scenario = scenarioPath, Forms = new List<string?> { form }, Fact = "fact.chef.vaincu", Rewards = new List<RewardData?>() };
            if (kind == "boss") d.Boss = data; else d.Chiefs = new List<BossData?> { data };
            // Porte du côté opposé au loup peuplé, à dx de la forme de départ du chef.
            d.Doors = new List<DoorData?> { DoorTo("haut", "zone.vallons", "bas", new GroundVector(start.X + Fixed.Parse(dx), start.Z)) };
        });
        ZoneLoadResult Check(ZoneDocument doc) => ZoneValidator.Validate(doc, Block.Value, Items, null,
            kind == "boss" ? forms : null, kind == "boss" ? null : new[] { (CombatSetup?)forms });
        ZoneLoadResult exact = Check(Near("8", "dessous"));
        Assert.True(exact.IsValid, exact.Report.ToString());
        ZoneLoadResult close = Check(Near("7.9999", "dessous"));
        AssertError(close, errorPath, ValidationCode.OutOfRange);
        Assert.Null(close.Zone);
        Assert.True(Check(Near("7.9999", "surface")).IsValid);   // à la surface, la règle d'avant ne change pas
    }

    /// <summary>Relecture indépendante : plafonds de portes d'une zone à région, au total puis par case, schéma 14 contre schéma 15.</summary>
    [Theory]
    [InlineData(ZoneValidator.MaxRegionDoorsBefore15, 14, true)]
    [InlineData(ZoneValidator.MaxRegionDoorsBefore15 + 1, 14, false)]
    [InlineData(ZoneValidator.MaxRegionDoors, 15, true)]
    [InlineData(ZoneValidator.MaxRegionDoors + 1, 15, false)]
    public void Bornes_des_portes_d_une_region_au_total(int count, int schema, bool valid)
    {
        // Portes réparties sur les cases, une par case (la borne par case ne gêne pas), en évitant les obstacles de la zone.
        RegionData Region() => new() { Origin = new PointData { X = Fixed.FromInt(-1908), Z = Fixed.FromInt(-1614) }, Columns = 32, Rows = 24 };
        GroundVector Centre(int cell) => new(Fixed.FromInt(-1908 + 64 + 128 * (cell % 32)), Fixed.FromInt(-1614 + 64 + 128 * (cell / 32)));
        var free = new List<GroundVector>();
        for (int first = 0; first < 32 * 24; first += 256)
        {
            int n = Math.Min(256, 32 * 24 - first);
            ZoneLoadResult probe = Validate(Dto(edit: d =>
            {
                d.Region = Region();
                d.Doors = Enumerable.Range(0, n).Select(i => (DoorData?)DoorTo("porte_" + i, "zone.cave_" + i, "haut", Centre(first + i))).ToList();
            }));
            var bad = probe.Report.Errors.Where(e => e.Path.StartsWith("$.data.doors[")).Select(e => e.Path).ToHashSet();
            for (int i = 0; i < n; i++) if (!bad.Contains($"$.data.doors[{i}].position")) free.Add(Centre(first + i));
        }
        Assert.True(free.Count > ZoneValidator.MaxRegionDoors + 1, "assez de cases libres");
        ZoneLoadResult r = Validate(Dto(edit: d =>
        {
            d.Region = Region();
            d.Doors = Enumerable.Range(0, count).Select(i => (DoorData?)DoorTo("porte_" + i, "zone.cave_" + i, "haut", free[i])).ToList();
        }, schema: schema));
        if (valid) Assert.True(r.IsValid, r.Report.ToString());
        else AssertError(r, "$.data.doors", ValidationCode.LimitExceeded);
    }

    [Theory]
    [InlineData(ZoneValidator.MaxDoorsPerCellBefore15, 14, true)]
    [InlineData(ZoneValidator.MaxDoorsPerCellBefore15 + 1, 14, false)]
    [InlineData(ZoneValidator.MaxDoorsPerCell, 15, true)]
    [InlineData(ZoneValidator.MaxDoorsPerCell + 1, 15, false)]
    public void Bornes_des_portes_d_une_region_par_case(int count, int schema, bool valid)
    {
        ZoneLoadResult r = Validate(Dto(edit: d =>
        {
            d.Region = new RegionData { Origin = new PointData { X = Fixed.FromInt(-1908), Z = Fixed.FromInt(-1614) }, Columns = 32, Rows = 24 };
            d.Doors = Enumerable.Range(0, count).Select(i => (DoorData?)DoorTo("porte_" + i, "zone.cave_" + i, "haut")).ToList();   // toutes dans la case de Place
        }, schema: schema));
        if (valid) Assert.True(r.IsValid, r.Report.ToString());
        else Assert.Contains(r.Report.Errors, e => e.Path == $"$.data.doors[{count - 1}]" && e.Code == ValidationCode.LimitExceeded);
    }

    [Fact]
    public void Portes_qui_se_repondent_du_meme_genre()
    {
        ZoneDefinition Zone(string id, string floor, string door, string to, string toDoor, string? kind) =>
            Validate(Dto(id, d => { d.Floor = floor; d.Doors = new List<DoorData?> { DoorTo(door, to, toDoor, kind: kind) }; })).Zone!;
        ZoneDefinition top = Zone("zone.haut", "surface", "trappe_cave", "zone.cave", "echelle", "trappe");
        ZoneDefinition cave = Zone("zone.cave", "dessous", "echelle", "zone.haut", "trappe_cave", "trappe");
        Assert.True(ZoneValidator.CheckDoors(new[] { top, cave }).IsValid);

        ZoneDefinition stairs = Zone("zone.cave", "dessous", "echelle", "zone.haut", "trappe_cave", "escalier");
        ValidationReport mixed = ZoneValidator.CheckDoors(new[] { top, stairs });
        Assert.Contains(mixed.Errors, e => e.Path == "$.data.doors[0].kind" && e.Code == ValidationCode.InvalidValue && e.Message.Contains("trappe", StringComparison.Ordinal));
        // Une porte d'avant (sans genre) répond à une porte.
        ZoneDefinition plainTop = Zone("zone.haut", "surface", "trappe_cave", "zone.cave", "echelle", null);
        ZoneDefinition plainCave = Zone("zone.cave", "dessous", "echelle", "zone.haut", "trappe_cave", "porte");
        Assert.True(ZoneValidator.CheckDoors(new[] { plainTop, plainCave }).IsValid);
    }

    [Fact]
    public void Zones_reliees_au_plus_256()
    {
        ZoneDefinition one = Validate(Dto()).Zone!;
        ZoneDefinition[] Zones(int n) => Enumerable.Range(0, n)
            .Select(i => i == 0 ? one : Validate(Dto("zone.z" + i)).Zone!).ToArray();
        Assert.True(ZoneValidator.CheckDoors(Zones(ZoneValidator.MaxLinkedZones)).IsValid);
        Assert.Contains(ZoneValidator.CheckDoors(Zones(ZoneValidator.MaxLinkedZones + 1)).Errors, e => e.Path == "$" && e.Code == ValidationCode.LimitExceeded);
    }

    [Fact]
    public void Lecteur_json_etage_et_genre()
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.grange")))!.AsObject();
        root["schemaVersion"] = 15;
        root["data"]!["doors"]![0]!["kind"] = "trappe";
        ZoneLoadResult read = ZoneJsonReader.Read(root.ToJsonString(), Scenarios, ShippedData, Items);
        Assert.True(read.IsValid, read.Report.ToString());
        Assert.Equal(DoorKind.Hatch, read.Zone!.Doors[0].Kind);

        root["data"]!["floor"] = "dessous";   // la grange est un intérieur
        AssertError(ZoneJsonReader.Read(root.ToJsonString(), Scenarios, ShippedData, Items), "$.data.indoor", ValidationCode.InvalidValue);
        root["data"]!["indoor"] = false;
        ZoneLoadResult under = ZoneJsonReader.Read(root.ToJsonString(), Scenarios, ShippedData, Items);
        Assert.True(under.IsValid, under.Report.ToString());
        Assert.Equal(ZoneFloor.Underground, under.Zone!.Floor);

        root["data"]!["floor"] = 3;
        AssertError(ZoneJsonReader.Read(root.ToJsonString(), Scenarios, ShippedData, Items), "$.data.floor", ValidationCode.WrongType);
        root["data"]!["floor"] = "dessous";
        root["data"]!["doors"]![0]!["kind"] = true;
        AssertError(ZoneJsonReader.Read(root.ToJsonString(), Scenarios, ShippedData, Items), "$.data.doors[0].kind", ValidationCode.WrongType);
    }
}
