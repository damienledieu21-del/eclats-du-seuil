using System.Text.Json.Nodes;
using EdS.Data.Crafting;
using EdS.Data.Items;
using EdS.Data.Json;
using EdS.Data.Saves;
using EdS.Data.Validation;
using EdS.Data.Zones;
using EdS.Domain.Combat;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V1.8b : zones en mélange (zones en schéma 7), usure selon le niveau et la rareté (catalogue en version 6), réparation pièce
/// par pièce, vente d'une pièce usée, expérience d'une recette ratée (métiers en version 3).
/// </summary>
[Trait("Category", "World")]
public class V18bTests
{
    private static ZoneDefinition Vallons => Zone();

    private static ZoneLoadResult ZoneMutated(Action<JsonObject> mutate)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!.AsObject();
        mutate(root);
        return ZoneJsonReader.Read(root.ToJsonString(), ScenariosDirectory, ShippedData, ItemsCatalog);
    }

    // --- Zones en mélange --------------------------------------------------------------------------------------------------------

    [Fact]
    public void Zones_en_schema_7_niveaux_minimums_et_reglage()
    {
        ZoneDefinition z = Vallons;
        Assert.Equal((12, 12), (z.SchemaVersion, ZoneValidator.CurrentSchemaVersion));   // V2.6.1 : schéma 12 (propagation) ; V2.6 : schéma 11 (cicatrices) ; V2.5 : schéma 10 (événements)   // V2.4 : schéma 9 (temps) ; V2.1 : schéma 8 (jour et nuit)
        ZoneScaling s = z.Scaling!;
        Assert.Equal((1, 5, 12, 30, 2), (s.BaseLevel, s.BossBaseLevel, s.HealthPerLevelPercent, s.DamagePerLevelPercent, s.EaseLevels));
        int Min(string place) => z.Places.Single(p => p.Id == place).MinLevel;
        Assert.Equal((1, 1, 6, 11, 16, 21), (Min("village"), Min("moulin"), Min("ruines"), Min("foret_des_loups"), Min("camp_des_rodeurs"), Min("seuil")));
        // Un niveau par chapitre ; sous-lot V1.9.5 : plus le cœur du Seuil (23), où se tient le Gardien de la fin du chapitre 5.
        Assert.Equal(new[] { 1, 6, 11, 16, 21, 23 }, z.Places.Select(p => p.MinLevel).Distinct().OrderBy(l => l));
        Assert.Equal(23, z.MinLevelAt(z.Bosses.Single(b => b.Id == "gardien_du_seuil").Forms[0].Placement!.Position));
        // Le plus haut des lieux qui contiennent le point ; 1 hors de tout lieu.
        Assert.Equal(16, z.MinLevelAt(z.PopulationOf("bandit_epee1")!.Template.Placement!.Position));
        Assert.Equal(1, z.MinLevelAt(new EdS.Domain.Space.GroundVector(EdS.Domain.Numerics.Fixed.FromInt(10), EdS.Domain.Numerics.Fixed.FromInt(-6))));
        // Relecture de la PR #83 (M7) : chaque créature est dans un lieu (loup6 dans la forêt des Loups, le sanglier dans les ruines).
        foreach (Population p in z.Populations)
            Assert.True(z.Places.Any(pl => pl.Contains(p.Template.Placement!.Position)), $"{p.Creature} hors de tout lieu");
        Assert.Equal((11, 6), (z.MinLevelAt(z.PopulationOf("loup6")!.Template.Placement!.Position), z.MinLevelAt(z.PopulationOf("sanglier")!.Template.Placement!.Position)));
        // Relecture de la PR #83 : niveau propre d'une population (loup blanc au chapitre 1, Borgne au chapitre 2).
        Assert.Equal(((int?)1, (int?)6), (z.PopulationOf("loup_blanc")!.Level, z.PopulationOf("le_borgne")!.Level));
        Assert.Equal((1, 6), (z.OwnMinLevel("loup_blanc", z.PopulationOf("loup_blanc")!.Template.Placement!.Position),
            z.OwnMinLevel("le_borgne", z.PopulationOf("le_borgne")!.Template.Placement!.Position)));
        // Les intérieurs n'ont pas de mélange.
        Assert.Null(ZoneJsonReader.ReadFile(ZoneJsonReader.PathIn(DataDirectory, "zone.auberge"), ScenariosDirectory, ShippedData, ItemsCatalog).Zone!.Scaling);
    }

    /// <summary>Vallons lus avec un document de zone et un scénario retouchés (sans exiger qu'ils soient admis).</summary>
    private static ZoneLoadResult VallonsMutated(Action<JsonObject>? zone = null, Action<JsonObject>? scenario = null)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ZoneJsonReader.PathIn(DataDirectory, "zone.vallons")))!.AsObject();
        zone?.Invoke(root);
        string directory = Path.Combine(Path.GetTempPath(), "eds_v18b_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(directory, "zones"));
            JsonObject s = JsonNode.Parse(File.ReadAllText(ScenarioPath("zones/vallons")))!.AsObject();
            scenario?.Invoke(s);
            File.WriteAllText(Path.Combine(directory, "zones", "vallons.json"), s.ToJsonString());
            File.Copy(ScenarioPath("zones/vallons_chef"), Path.Combine(directory, "zones", "vallons_chef.json"));
            File.Copy(ScenarioPath("zones/vallons_bandits"), Path.Combine(directory, "zones", "vallons_bandits.json"), true);
            CopyMissingZoneScenarios(directory);
            return ZoneJsonReader.Read(root.ToJsonString(), directory, ShippedData, ItemsCatalog);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Niveau_propre_d_une_population_controle_et_applique()
    {
        // Relecture de la PR #83 : le loup blanc garde le niveau 1 (chapitre 1), le Borgne le niveau 6 (chapitre 2), au lieu de 11 et 16.
        ZoneDefinition z = Vallons;
        AssailantSetup white = z.PopulationOf("loup_blanc")!.Template, borgne = z.PopulationOf("le_borgne")!.Template;
        Assert.Same(white, z.ForLevel(3).PopulationOf("loup_blanc")!.Template);   // joueur 3 : niveau 1, tel qu'écrit
        Assert.Equal(white.Health!.Value * 112 / 100, z.ForLevel(4).PopulationOf("loup_blanc")!.Template.Health);   // joueur 4 : niveau 2
        Assert.Equal(borgne.Health!.Value * 160 / 100, z.ForLevel(3).PopulationOf("le_borgne")!.Template.Health);   // niveau 6 au moins
        Assert.Equal(borgne.Health!.Value * 160 / 100, z.ForLevel(8).PopulationOf("le_borgne")!.Template.Health);
        Assert.Equal(white.Health!.Value * (100 + 12 * 22) / 100, z.ForLevel(25).PopulationOf("loup_blanc")!.Template.Health);   // monte quand même : 23
        // Les autres créatures de la forêt gardent le niveau de leur lieu.
        Assert.Equal(z.PopulationOf("loup7")!.Template.Health!.Value * (100 + 12 * 10) / 100, z.ForLevel(3).PopulationOf("loup7")!.Template.Health);
        foreach (int bad in new[] { 0, 31 })
        {
            ZoneLoadResult r = VallonsMutated(zone: root =>
                root["data"]!["populations"]!.AsArray().First(p => (string)p!["creature"]! == "loup_blanc")!["level"] = bad);
            Assert.False(r.IsValid);
            Assert.Contains(r.Report.Errors, e => e.Path.EndsWith("].level", StringComparison.Ordinal) && e.Code == ValidationCode.OutOfRange);
        }
    }

    [Theory]
    [InlineData("health", "$.data.scaling.healthPerLevelPercent")]
    [InlineData("rawDamage", "$.data.scaling.damagePerLevelPercent")]
    public void Precontrole_numerique_de_la_mise_a_l_echelle(string field, string path)
    {
        // Relecture de la PR #83 (M2) : une créature portée au niveau maximal (30) ne dépasse ni 10⁹ PV ni 10⁹ dégâts bruts.
        ZoneLoadResult r = VallonsMutated(scenario: s =>
        {
            JsonObject wolf = s["data"]!["combatants"]!.AsArray().Select(c => c!.AsObject()).Single(c => (string)c["id"]! == "loup1");
            if (field == "health") wolf["health"] = 300_000_000;
            else wolf["attacks"]![0]!["rawDamage"] = 300_000_000;
        });
        Assert.False(r.IsValid);
        ValidationError e = Assert.Single(r.Report.Errors);
        Assert.Equal((path, ValidationCode.OutOfRange), (e.Path, e.Code));
        Assert.Contains("loup1", e.Message);
        // Sans mélange (zone sans scaling), les mêmes chiffres sont admis.
        Assert.True(VallonsMutated(zone: root => root["data"]!.AsObject().Remove("scaling"), scenario: s =>
        {
            JsonObject wolf = s["data"]!["combatants"]!.AsArray().Select(c => c!.AsObject()).Single(c => (string)c["id"]! == "loup1");
            if (field == "health") wolf["health"] = 300_000_000;
            else wolf["attacks"]![0]!["rawDamage"] = 300_000_000;
        }).IsValid);
    }

    [Fact]
    public void Ancienne_sauvegarde_la_zone_suit_le_niveau_garde_apres_la_conversion()
    {
        // Remarque de Codex sur la PR #83 : 1 620 points d'avant V1.8 (niveau 10 ; niveau 8 aux nouveaux seuils sans conversion). Les
        // créatures sont réglées pour le niveau 10, celui que le personnage garde, comme après une halte.
        ZoneDefinition z = Vallons;
        long At(int level) => z.ForLevel(level).PopulationOf("loup1")!.Template.Health!.Value;
        Assert.NotEqual(At(8), At(10));
        var old = new WorldSession(new WorldSave("zone.vallons", 0, 0, Array.Empty<string>(), Hero().WithXp(1620)), z, ShippedData, Progression, ItemsCatalog);
        Assert.True(old.XpMigrated);
        Assert.Equal((10, 10), (old.Level, old.EnteredLevel));
        Assert.Equal(At(10), old.Definition!.PopulationOf("loup1")!.Template.Health);
        var halt = new WorldSession(old.Save(), z, ShippedData, Progression, ItemsCatalog);
        Assert.False(halt.XpMigrated);
        Assert.Equal(At(10), halt.Definition!.PopulationOf("loup1")!.Template.Health);
        // Sans la place au registre pour la conversion : ni conversion ni mise à l'échelle du niveau 10 (même règle aux deux endroits).
        string[] full = Enumerable.Range(0, SaveValidator.MaxConsumed - 200).Select(i => $"probe/k{i:D6}").ToArray();
        var crowded = new WorldSession(new WorldSave("zone.vallons", 0, 0, full, Hero().WithXp(1620)), z, ShippedData, Progression, ItemsCatalog);
        Assert.False(crowded.XpMigrated);
        Assert.Equal((8, 8), (crowded.Level, crowded.EnteredLevel));
        Assert.Equal(At(8), crowded.Definition!.PopulationOf("loup1")!.Template.Health);
    }

    [Fact]
    public void Les_creatures_montent_au_niveau_du_joueur_moins_deux()
    {
        ZoneDefinition z = Vallons;
        ZoneScaling s = z.Scaling!;
        Assert.Equal((1, 1, 3, 23), (s.CreatureLevel(1, 1), s.CreatureLevel(1, 3), s.CreatureLevel(1, 5), s.CreatureLevel(1, 25)));
        Assert.Equal((16, 16, 18), (s.CreatureLevel(16, 1), s.CreatureLevel(16, 18), s.CreatureLevel(16, 20)));
        AssailantSetup wolf = z.PopulationOf("loup1")!.Template;
        Assert.Equal((150L, 10L), (wolf.Health!.Value, wolf.Attacks[0].RawDamage));
        // Joueur de niveau 3 : le loup du moulin reste au niveau 1, tel qu'écrit (même objet).
        Assert.Same(wolf, z.ForLevel(3).PopulationOf("loup1")!.Template);
        // Niveau 12 : loup de niveau 10, +108 % de PV, +270 % de dégâts (arrondis vers le bas).
        AssailantSetup ten = z.ForLevel(12).PopulationOf("loup1")!.Template;
        Assert.Equal((312L, 37L), (ten.Health!.Value, ten.Attacks[0].RawDamage));
        // Le bandit du camp (niveau 16 au moins) : un joueur de niveau 5 le trouve au niveau 16.
        AssailantSetup bandit = z.PopulationOf("bandit_epee1")!.Template, at5 = z.ForLevel(5).PopulationOf("bandit_epee1")!.Template;
        Assert.Equal(bandit.Health!.Value * 280 / 100, at5.Health!.Value);
        // Le scénario de départ de la session suit (créatures du scénario), et les formes des chefs, faites pour le niveau 5.
        Assert.Equal(312L, ((AssailantSetup)z.ForLevel(12).Setup.Combatants.Single(c => c.Id == "loup1")).Health!.Value);
        AssailantSetup gardien = z.Boss!.Forms[0], g25 = z.ForLevel(25).Boss!.Forms[0];
        Assert.Equal(gardien.Health!.Value * (100 + 12 * 18) / 100, g25.Health!.Value);   // niveau 23 : 18 niveaux au-dessus de 5
        // L'expérience suit les PV.
        Assert.True(Progression.KillXp(ten.Health.Value) > Progression.KillXp(wolf.Health.Value));
        // Rien n'est modifié : la zone d'origine garde ses chiffres.
        Assert.Equal(150L, z.PopulationOf("loup1")!.Template.Health);
    }

    [Fact]
    public void La_session_prend_la_zone_au_niveau_du_personnage()
    {
        SavedCharacter hero = PlayerCharacters.Create("bastion", "class.bastion", Progression).WithXp(Progression.XpToLevel[11]);   // niveau 12
        WorldSession w = Play(Vallons, hero);
        Assert.Equal(312L, w.Zone.Engine.View("loup1").MaxHealth);
        Assert.Equal(312L, w.Definition!.PopulationOf("loup1")!.Template.Health);
        // Un personnage de niveau 1 : les loups du moulin tels qu'écrits.
        Assert.Equal(150L, Play(Vallons, PlayerCharacters.Create("bastion", "class.bastion", Progression)).Zone.Engine.View("loup1").MaxHealth);
    }

    [Theory]
    [InlineData("baseLevel", 0, ValidationCode.OutOfRange)]
    [InlineData("bossBaseLevel", 31, ValidationCode.OutOfRange)]
    [InlineData("healthPerLevelPercent", 101, ValidationCode.OutOfRange)]
    [InlineData("damagePerLevelPercent", -1, ValidationCode.OutOfRange)]
    [InlineData("easeLevels", 11, ValidationCode.OutOfRange)]
    public void Reglage_du_melange_borne(string field, int value, ValidationCode code)
    {
        ZoneLoadResult r = ZoneMutated(root => root["data"]!["scaling"]![field] = value);
        Assert.False(r.IsValid);
        Assert.Contains(r.Report.Errors, e => e.Path == "$.data.scaling." + field && e.Code == code);
        ZoneLoadResult missing = ZoneMutated(root => root["data"]!["scaling"]!.AsObject().Remove(field));
        Assert.Contains(missing.Report.Errors, e => e.Path == "$.data.scaling." + field && e.Code == ValidationCode.MissingField);
    }

    [Fact]
    public void Niveau_minimum_d_un_lieu_borne()
    {
        ZoneLoadResult r = ZoneMutated(root => root["data"]!["places"]![0]!["minLevel"] = 31);
        Assert.Contains(r.Report.Errors, e => e.Path == "$.data.places[0].minLevel" && e.Code == ValidationCode.OutOfRange);
        ZoneLoadResult zero = ZoneMutated(root => root["data"]!["places"]![0]!["minLevel"] = 0);
        Assert.Contains(zero.Report.Errors, e => e.Path == "$.data.places[0].minLevel");
        // Sans niveau minimum, un lieu est au niveau 1 ; sans réglage, pas de mélange.
        ZoneLoadResult plain = ZoneMutated(root =>
        {
            root["data"]!.AsObject().Remove("scaling");
            foreach (JsonNode? p in root["data"]!["places"]!.AsArray()) p!.AsObject().Remove("minLevel");
        });
        Assert.True(plain.IsValid, plain.Report.ToString());
        Assert.All(plain.Zone!.Places, p => Assert.Equal(1, p.MinLevel));
        Assert.Same(plain.Zone, plain.Zone.ForLevel(25));
        ZoneLoadResult old = ZoneMutated(root => root["schemaVersion"] = 6);
        Assert.Contains(old.Report.Errors, e => e.Path == "$.schemaVersion" && e.Code == ValidationCode.UnsupportedSchemaVersion);
    }

    // --- Usure, réparation, vente --------------------------------------------------------------------------------------------

    [Fact]
    public void Usure_selon_le_niveau_et_la_rarete()
    {
        ItemCatalog c = ItemsCatalog;
        Assert.Equal((7, "0.16.0-provisoire"), (c.SchemaVersion, c.ContentVersion));   // V2.6.1 : 0.14.0 (chapelet de l'Aube) ; V1.9.9 : 0.13.0 (poussière d'écho, couronne du pré) ; V1.9.7 : 0.12.0 (collier de Croc-Gris, chapeau-de-lune, objets de quête du village) ; V1.9.6 : 0.11.0 (croc de Croc-Gris) ; V1.9.4 : 0.10.0 (objets de quête du chapitre 4) ; V1.9.3 : 0.9.0 (objets de quête du chapitre 3) ; V1.9.2 : 0.8.0 (objets de quête du chapitre 2) ; V1.9.1 : 0.7.0 (miche du matin) ; V1.8c : version 7 (objets de bonus)
        Assert.Equal((100, 3, 1), (c.WearMax, c.WearPerFullHealth, c.WearLevelResistPercent));
        Assert.Equal(new[] { 0, 20, 40, 60, 80, 100 }, c.WearRarityResistPercent);
        Assert.Equal(0, c.WearResistPercent(c.Item("item.epee_du_novice")));            // commune de niveau 1
        Assert.Equal(9 + 20, c.WearResistPercent(c.Item("item.epee_longue_d_aube")));   // rare de niveau 10
        Assert.Equal(17 + 40, c.WearResistPercent(c.Item("item.lame_de_la_tour")));     // très rare de niveau 18
        // Rythme de référence : un combat difficile (dégâts égaux aux PV maximaux) use une pièce commune de son niveau de 3 points sur 100 :
        // environ 33 combats ; plus haut et plus rare, plus longtemps.
        int Fights(string item)
        {
            long resist = 100 + c.WearResistPercent(c.Item(item)), carry = 0;
            int left = c.WearMax, fights = 0;
            while (left > 0)
            {
                long n = c.WearPerFullHealth * 100L + carry;
                left -= (int)(n / resist);
                carry = n % resist;
                fights++;
            }
            return fights;
        }
        Assert.InRange(Fights("item.epee_du_novice"), 30, 40);
        Assert.True(Fights("item.epee_longue_d_aube") > Fights("item.epee_du_novice"));
        Assert.True(Fights("item.lame_de_la_tour") > Fights("item.epee_longue_d_aube"));
        // Relecture de la PR #83 (M4) : une seule formule d'usure, celle de la partie (WorldSession.PieceLosses) ; plus d'arrondi à part.
        Assert.Equal(0, c.WearResistPercent(c.Item("item.epee_du_novice")));
        Assert.True(c.WearResistPercent(c.Item("item.lame_de_la_tour")) > c.WearResistPercent(c.Item("item.epee_longue_d_aube")));
    }

    [Theory]
    [InlineData("levelResistPercent", 101, "$.data.wear.levelResistPercent")]
    [InlineData("levelResistPercent", -1, "$.data.wear.levelResistPercent")]
    public void Resistance_a_l_usure_bornee(string field, int value, string path)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(ItemJsonReader.PathIn(DataDirectory)))!.AsObject();
        root["data"]!["wear"]![field] = value;
        ItemCatalogLoadResult r = ItemJsonReader.Read(root.ToJsonString(), ShippedData);
        Assert.Contains(r.Report.Errors, e => e.Path == path);
        root = JsonNode.Parse(File.ReadAllText(ItemJsonReader.PathIn(DataDirectory)))!.AsObject();
        root["data"]!["wear"]!["rarityResistPercent"] = new JsonArray(0, 20, 10, 60, 80, 100);   // en baisse
        Assert.Contains(ItemJsonReader.Read(root.ToJsonString(), ShippedData).Report.Errors, e => e.Path == "$.data.wear.rarityResistPercent[2]");
        root["data"]!["wear"]!["rarityResistPercent"] = new JsonArray(0, 20);   // pas six valeurs
        Assert.Contains(ItemJsonReader.Read(root.ToJsonString(), ShippedData).Report.Errors, e => e.Path == "$.data.wear.rarityResistPercent");
        root["data"]!["wear"]!.AsObject().Remove("rarityResistPercent");
        Assert.Contains(ItemJsonReader.Read(root.ToJsonString(), ShippedData).Report.Errors,
            e => e.Path == "$.data.wear.rarityResistPercent" && e.Code == ValidationCode.MissingField);
        // Relecture de la PR #83 (M5) : le validateur contrôle aussi le nombre de valeurs (document construit hors du lecteur JSON).
        foreach (int count in new[] { 2, 7 })
        {
            var report = new ValidationReport();
            ItemCatalogDocument document = ItemJsonReader.ReadDocument(File.ReadAllBytes(ItemJsonReader.PathIn(DataDirectory)), report);
            document.Data!.Wear!.RarityResistPercent = Enumerable.Range(0, count).Select(i => (int?)(i * 10)).ToList();
            ValidationError e = Assert.Single(ItemValidator.Validate(document, ShippedData, report).Report.Errors);
            Assert.Equal(("$.data.wear.rarityResistPercent", ValidationCode.InvalidValue), (e.Path, e.Code));
        }
    }

    [Fact]
    public void Une_piece_usee_se_vend_moins_cher_au_moins_un_cuivre()
    {
        WorldSession w = Play(Vallons, Hero(0));
        Assert.Equal(300, w.WornPrice(300, new ItemStack("item.epee_du_milicien", 1)));                 // neuve : prix plein
        Assert.Equal(120, w.WornPrice(300, new ItemStack("item.epee_du_milicien", 1, wear: 40)));       // 40 % de solidité
        Assert.Equal(1, w.WornPrice(300, new ItemStack("item.epee_du_milicien", 1, wear: 0)));          // usée à zéro : 1 cuivre
        Assert.Equal(1, w.WornPrice(2, new ItemStack("item.epee_du_milicien", 1, wear: 10)));           // au moins 1
        Assert.Equal(0, w.WornPrice(0, new ItemStack("item.epee_du_milicien", 1, wear: 10)));           // sans valeur : rien
    }

    [Fact]
    public void Vente_d_une_piece_usee_au_marchand()
    {
        SavedCharacter hero = Hero(0).WithItems(new[] { new ItemStack("item.epee_du_milicien", 1, wear: 50) }, new Dictionary<string, string>());
        WorldSession w = At("merchant.armurier", hero);
        long full = w.Market!.UnitPrice("merchant.armurier", "item.epee_du_milicien");
        int place = w.Inventory.Stacks.ToList().FindIndex(s => s.Item == "item.epee_du_milicien");
        Assert.Equal(TradeRefusal.None, w.SellPlace("merchant.armurier", place, 1));
        Assert.Equal(full / 2, w.Money);
        // Le rachat se fait au prix payé, et rend la pièce telle quelle (usée).
        Assert.Equal(TradeRefusal.None, w.BuyBack("merchant.armurier", 0));
        Assert.Equal(0L, w.Money);
        Assert.Equal(50, w.Inventory.Stacks.Single(s => s.Item == "item.epee_du_milicien").Wear);
    }

    // --- Recette ratée ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void Metiers_en_version_3_un_quart_de_l_experience_pour_un_echec()
    {
        CraftingBook book = CraftingJsonReader.ReadFile(CraftingJsonReader.PathIn(DataDirectory), Texts, Vallons, ItemsCatalog).Book!;
        Assert.Equal((3, "0.7.0-provisoire", 25), (book.SchemaVersion, book.ContentVersion, book.Failure.XpPercent));
        Assert.Equal((15L, 1L, 0L, 1L), (book.Failure.FailedXp(60), book.Failure.FailedXp(2), book.Failure.FailedXp(0), book.Failure.FailedXp(4)));
        JsonObject root = JsonNode.Parse(File.ReadAllText(CraftingJsonReader.PathIn(DataDirectory)))!.AsObject();
        root["data"]!["failure"]!["xpPercent"] = 101;
        CraftingLoadResult bad = CraftingJsonReader.Read(root.ToJsonString(), Texts, Vallons, ItemsCatalog);
        Assert.Contains(bad.Report.Errors, e => e.Path == "$.data.failure.xpPercent");
        root["data"]!["failure"]!.AsObject().Remove("xpPercent");
        Assert.Contains(CraftingJsonReader.Read(root.ToJsonString(), Texts, Vallons, ItemsCatalog).Report.Errors,
            e => e.Path == "$.data.failure.xpPercent" && e.Code == ValidationCode.MissingField);
        root["schemaVersion"] = 2;
        Assert.Contains(CraftingJsonReader.Read(root.ToJsonString(), Texts, Vallons, ItemsCatalog).Report.Errors, e => e.Path == "$.schemaVersion");
    }
}
