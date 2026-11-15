using System;
using System.Linq;
using System.Text.Json.Nodes;
using EdS.Data.Allies;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Data.Validation;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Domain.Stats;
using EdS.Simulation.Engine;
using Xunit;

namespace EdS.Data.Tests;

/// <summary>
/// Compagnons : catalogue des compagnons (<c>kind: "companions"</c>, schéma 1), un seul validateur (<see cref="CompanionValidator"/>), et
/// passage d'une fiche à un niveau vers un assaillant compagnon du moteur (<see cref="CompanionCombat"/>). Fiches d'essai : un héros, un
/// mercenaire et une bête à trois stades.
/// </summary>
[Trait("Category", "Companion")]
[Trait("Category", "Data")]
public class CompagnonsCatalogueTests
{
    internal const string Catalogue = """
    {
      "schemaVersion": 1, "kind": "companions", "contentVersion": "0.1.0-test",
      "data": {
        "bond": { "windowMs": 3000, "coordinatedGain": 4, "protectGain": 6, "campGain": 10 },
        "slots": { "initial": 1, "max": 2, "secondSlotLevel": 12, "secondSlotQuest": "quest.vallons.lien_au_feu", "worldBossInCombat": 1 },
        "traits": [
          { "id": "courageux", "name": "Courageux", "gesture": "Menton levé, pied avancé.", "line": "Derrière moi." },
          { "id": "peureux", "name": "Peureux", "gesture": "Regarde la sortie.", "line": "On ne traîne pas." }
        ],
        "companions": [
          { "id": "companion.nyx", "name": "Nyx", "kind": "hero", "role": "assaut dans l'ombre", "summary": "Ancienne passeuse.",
            "recruit": { "source": "story" }, "gift": { "id": "pas_feutre", "name": "Pas feutré", "effect": "flank", "description": "Signale une approche latérale." },
            "stages": [ { "id": "nyx", "name": "Nyx", "fromLevel": 1, "health": { "base": 300, "perLevel": 30 }, "armor": { "base": 10, "perLevel": 2 },
              "magicResistance": { "base": 5, "perLevel": 1 }, "radius": 0.45, "speed": 5, "maxRange": 0.6, "firstAttackMs": 500, "intervalMs": 1000,
              "followDistance": 1.5, "leash": 12,
              "attacks": [ { "id": "seax", "damage": { "base": 20, "perLevel": 3 }, "damageType": "physique", "range": 1.2 } ],
              "duo": { "id": "contre_jour", "name": "Contre-jour", "damage": { "base": 80, "perLevel": 12 }, "damageType": "physique", "telegraphMs": 800, "recoveryMs": 1500, "range": 2.5 } } ] },
          { "id": "companion.aveline", "name": "Aveline Dorne", "kind": "mercenary", "role": "lame", "summary": "Ancienne escorte de convois.",
            "recruit": { "source": "guild", "guildRank": "e", "price": 120 }, "randomTrait": true,
            "gift": { "id": "coeur_ferme", "name": "Cœur ferme", "effect": "steadfast", "description": "Maintient sa protection face à la peur." },
            "stages": [ { "id": "aveline", "name": "Aveline", "fromLevel": 1, "health": { "base": 350, "perLevel": 35 }, "armor": { "base": 20, "perLevel": 3 },
              "magicResistance": { "base": 5, "perLevel": 1 }, "radius": 0.45, "speed": 4.5, "maxRange": 0.6, "firstAttackMs": 500, "intervalMs": 1200,
              "followDistance": 1.5, "leash": 12, "attacks": [ { "id": "taille", "damage": { "base": 22, "perLevel": 3 }, "damageType": "physique", "range": 1.3 } ] } ] },
          { "id": "companion.cendre", "name": "Cendre", "kind": "beast", "role": "pression de flanc", "summary": "Louveteau des Vallons.",
            "recruit": { "source": "story" }, "gift": { "id": "piste_partagee", "name": "Piste partagée", "effect": "track", "description": "Indique une trace proche." },
            "stages": [
              { "id": "louveteau", "name": "Louveteau", "fromLevel": 1, "health": { "base": 120, "perLevel": 10 }, "armor": { "base": 0, "perLevel": 1 },
                "magicResistance": { "base": 0, "perLevel": 1 }, "radius": 0.3, "speed": 5, "maxRange": 0.5, "firstAttackMs": 500, "intervalMs": 1500,
                "followDistance": 1, "leash": 8, "attacks": [ { "id": "mordille", "damage": { "base": 6, "perLevel": 1 }, "damageType": "physique", "range": 0.8 } ] },
              { "id": "jeune_loup", "name": "Jeune loup", "fromLevel": 8, "health": { "base": 200, "perLevel": 20 }, "armor": { "base": 5, "perLevel": 1 },
                "magicResistance": { "base": 2, "perLevel": 1 }, "radius": 0.5, "speed": 6, "maxRange": 0.5, "firstAttackMs": 500, "intervalMs": 1200,
                "followDistance": 1.2, "leash": 10, "attacks": [ { "id": "morsure", "damage": { "base": 14, "perLevel": 2 }, "damageType": "physique", "range": 1 } ],
                "duo": { "id": "appel_de_meute", "name": "Appel de meute", "damage": { "base": 40, "perLevel": 6 }, "damageType": "physique", "telegraphMs": 700, "recoveryMs": 1300, "range": 2 } },
              { "id": "loup_dresse", "name": "Loup dressé", "fromLevel": 18, "health": { "base": 320, "perLevel": 30 }, "armor": { "base": 10, "perLevel": 2 },
                "magicResistance": { "base": 5, "perLevel": 1 }, "radius": 0.6, "speed": 6.5, "maxRange": 0.5, "firstAttackMs": 500, "intervalMs": 1000,
                "followDistance": 1.2, "leash": 12, "attacks": [ { "id": "morsure", "damage": { "base": 24, "perLevel": 3 }, "damageType": "physique", "range": 1 } ],
                "duo": { "id": "appel_de_meute", "name": "Appel de meute", "damage": { "base": 90, "perLevel": 12 }, "damageType": "physique", "telegraphMs": 800, "recoveryMs": 1500, "range": 2.5 } }
            ] }
        ]
      }
    }
    """;

    private static CompanionLoadResult Mutated(Action<JsonObject> mutate)
    {
        JsonObject root = JsonNode.Parse(Catalogue)!.AsObject();
        mutate(root);
        return CompanionJsonReader.Read(root.ToJsonString());
    }

    /// <summary>Le combat de référence des compagnons, avec <paramref name="companion"/> à la place de Nyx et sans commande.</summary>
    private static CombatSetup WithCompanion(AssailantSetup companion)
    {
        CombatSetup s = ScenarioJsonReader.ReadFile(ScenarioTestData.ScenarioPath("espace_compagnon"), ScenarioTestData.ShippedData).Setup!;
        return new CombatSetup(s.Seed, 6000, s.Stats, s.Damage, s.Progression, s.Combat, s.Combatants.Where(c => c.Id != "nyx").Append(companion),
            Array.Empty<CombatCommand>(), s.Data, s.Space);
    }

    private static JsonObject Comp(JsonObject r, int i) => r["data"]!["companions"]![i]!.AsObject();
    private static JsonObject Stage(JsonObject r, int i, int s) => Comp(r, i)["stages"]![s]!.AsObject();

    private static void AssertError(CompanionLoadResult result, string path)
    {
        Assert.False(result.IsValid);
        Assert.Contains(result.Report.Errors, e => e.Path == path);
    }

    [Fact]
    public void Catalogue_d_essai_lu_et_valide()
    {
        CompanionLoadResult result = CompanionJsonReader.Read(Catalogue);
        Assert.True(result.IsValid, result.Report.ToString());
        CompanionCatalog c = result.Catalog!;
        Assert.Equal((1, 2, 12, 1), (c.Slots.Initial, c.Slots.Max, c.Slots.SecondSlotLevel, c.Slots.WorldBossInCombat));
        Assert.Equal(new[] { "companion.nyx", "companion.aveline", "companion.cendre" }, c.Companions.Select(x => x.Id));
        Assert.True(c.TryGet("companion.aveline", out CompanionSheet aveline));
        Assert.Equal((CompanionKind.Mercenary, true, "e", 120L), (aveline.Kind, aveline.RandomTrait, aveline.Recruit.GuildRank, aveline.Recruit.Price));
        Assert.True(c.TryGet("companion.cendre", out CompanionSheet cendre));
        Assert.Equal(new[] { "louveteau", "louveteau", "jeune_loup", "jeune_loup", "loup_dresse" },
            new[] { 1, 7, 8, 17, 18 }.Select(l => cendre.StageAt(l).Id));
        Assert.Null(cendre.StageAt(1).Duo);   // louveteau : pas encore d'attaque à deux
    }

    [Fact]
    public void Formes_et_regles_refusees_avec_chemin_precis()
    {
        AssertError(Mutated(r => r["schemaVersion"] = 2), "$.schemaVersion");
        AssertError(Mutated(r => r["kind"] = "guild"), "$.kind");
        AssertError(Mutated(r => r["data"]!.AsObject().Remove("bond")), "$.data.bond");
        AssertError(Mutated(r => r["data"]!["slots"]!["max"] = 3), "$.data.slots.max");
        AssertError(Mutated(r => r["data"]!["slots"]!["worldBossInCombat"] = 3), "$.data.slots.worldBossInCombat");
        AssertError(Mutated(r => r["data"]!["bond"]!["coordinatedGain"] = 101), "$.data.bond.coordinatedGain");
        AssertError(Mutated(r => r["data"]!["bond"]!["windowMs"] = 1234), "$.data.bond.windowMs");
        AssertError(Mutated(r => r["data"]!["traits"]![1]!["id"] = "courageux"), "$.data.traits[1].id");
        AssertError(Mutated(r => Comp(r, 0)["id"] = "nyx"), "$.data.companions[0].id");
        AssertError(Mutated(r => Comp(r, 1)["id"] = "companion.nyx"), "$.data.companions[1].id");
        AssertError(Mutated(r => Comp(r, 0)["kind"] = "dragon"), "$.data.companions[0].kind");
        AssertError(Mutated(r => Comp(r, 0)["randomTrait"] = true), "$.data.companions[0].randomTrait");   // un héros n'a pas de trait au hasard
        AssertError(Mutated(r => Comp(r, 1).Remove("randomTrait")), "$.data.companions[1].randomTrait");   // un mercenaire en a un
        AssertError(Mutated(r => Comp(r, 1)["recruit"]!["source"] = "story"), "$.data.companions[1].recruit.source");
        AssertError(Mutated(r => Comp(r, 1)["recruit"]!["guildRank"] = "z"), "$.data.companions[1].recruit.guildRank");
        AssertError(Mutated(r => Comp(r, 0)["recruit"]!["price"] = 10), "$.data.companions[0].recruit.price");
        AssertError(Mutated(r => Comp(r, 0)["gift"]!["effect"] = "invulnerable"), "$.data.companions[0].gift.effect");
        AssertError(Mutated(r => Stage(r, 2, 0)["fromLevel"] = 2), "$.data.companions[2].stages[0].fromLevel");
        AssertError(Mutated(r => Stage(r, 2, 2)["fromLevel"] = 8), "$.data.companions[2].stages[2].fromLevel");
        AssertError(Mutated(r => Stage(r, 2, 2)["id"] = "jeune_loup"), "$.data.companions[2].stages[2].id");
        AssertError(Mutated(r => Stage(r, 0, 0)["health"]!["base"] = 0), "$.data.companions[0].stages[0].health.base");
        AssertError(Mutated(r => Stage(r, 0, 0)["health"]!["perLevel"] = 100_000_000), "$.data.companions[0].stages[0].health");
        AssertError(Mutated(r => Stage(r, 0, 0)["leash"] = 1), "$.data.companions[0].stages[0].leash");
        AssertError(Mutated(r => Stage(r, 0, 0)["intervalMs"] = 1010), "$.data.companions[0].stages[0].intervalMs");
        AssertError(Mutated(r => Stage(r, 0, 0)["duo"]!["recoveryMs"] = 800), "$.data.companions[0].stages[0].duo.recoveryMs");
        AssertError(Mutated(r => Stage(r, 0, 0)["duo"]!["id"] = "seax"), "$.data.companions[0].stages[0].duo.id");
        AssertError(Mutated(r => Stage(r, 0, 0)["attacks"]![0]!["range"] = 0), "$.data.companions[0].stages[0].attacks[0].range");
        AssertError(Mutated(r => Stage(r, 0, 0)["inconnu"] = 1), "$.data.companions[0].stages[0].inconnu");
        AssertError(Mutated(r => Stage(r, 0, 0)["attacks"] = new JsonArray()), "$.data.companions[0].stages[0].attacks");
    }

    [Theory]
    [InlineData("companion.nyx", 1)]
    [InlineData("companion.nyx", 30)]
    [InlineData("companion.aveline", 12)]
    [InlineData("companion.cendre", 1)]
    [InlineData("companion.cendre", 9)]
    [InlineData("companion.cendre", 100)]
    public void Fiche_a_un_niveau_donne_un_compagnon_valide_qui_se_bat(string id, int level)
    {
        CompanionCatalog catalog = CompanionJsonReader.Read(Catalogue).Catalog!;
        Assert.True(catalog.TryGet(id, out CompanionSheet sheet));
        AssailantSetup companion = CompanionCombat.Build(catalog, sheet, level, "allie", "bastion", "joueurs", 100,
            new CombatantPlacement(GroundVector.Parse("0", "1.5"), Fixed.Parse("0.5")));
        CompanionStage stage = sheet.StageAt(level);
        Assert.Equal(stage.Health.At(level), companion.Health);
        Assert.Equal(stage.Attacks[0].Damage.At(level), companion.Attacks[0].RawDamage);
        Assert.Equal(stage.Radius, companion.Placement!.Radius);
        Assert.Equal((catalog.Bond.WindowMs, catalog.Bond.CoordinatedGain), (companion.Companion!.BondWindowMs, companion.Companion.CoordinatedGain));
        Assert.Equal(stage.Duo is null, companion.Companion.Duo is null);
        // Le compagnon construit passe la validation du combat et se bat aux côtés d'un personnage.
        CombatSetup setup = WithCompanion(companion);
        Assert.Empty(setup.Validate());
        var log = new MemoryCombatLog();
        CombatEngine.Run(setup, log);
        Assert.Contains(log.Lines, l => l.Contains("\"source\":\"allie\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Niveau_hors_bornes_refuse()
    {
        CompanionCatalog catalog = CompanionJsonReader.Read(Catalogue).Catalog!;
        CompanionSheet nyx = catalog.Companions[0];
        var at = new CombatantPlacement(GroundVector.Parse("0", "1.5"), Fixed.Parse("0.5"));
        Assert.Throws<ArgumentOutOfRangeException>(() => CompanionCombat.Build(catalog, nyx, 0, "a", "bastion", "joueurs", 0, at));
        Assert.Throws<ArgumentOutOfRangeException>(() => CompanionCombat.Build(catalog, nyx, CompanionCombat.MaxLevel + 1, "a", "bastion", "joueurs", 0, at));
    }
}
