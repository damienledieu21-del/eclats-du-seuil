using System;
using System.Collections.Generic;
using EdS.Data.Allies;
using EdS.Data.Json.Text;
using EdS.Data.Validation;

namespace EdS.Data.Json
{
    /// <summary>
    /// Compagnons : lecture stricte du catalogue des compagnons (<c>kind: "companions"</c>, <c>data/v0/world/companions.json</c>). La forme
    /// seulement ; le reste va au validateur unique <see cref="CompanionValidator"/>.
    /// </summary>
    public static class CompanionJsonReader
    {
        public static string PathIn(string dataDirectory) => System.IO.Path.Combine(dataDirectory, "world", "companions.json");

        /// <param name="story">Histoire du monde : la quête de lien doit y exister ; null : non vérifiée.</param>
        public static CompanionLoadResult ReadFile(string path, EdS.Data.Story.StoryBook? story = null)
        {
            var report = new ValidationReport();
            return CompanionValidator.Validate(ReadDocument(JsonInput.ReadFile(path, report), report), report, story);
        }

        public static CompanionLoadResult Read(string json, EdS.Data.Story.StoryBook? story = null)
        {
            var report = new ValidationReport();
            return CompanionValidator.Validate(ReadDocument(JsonInput.Utf8(json, report), report), report, story);
        }

        internal static CompanionDocument ReadDocument(byte[]? json, ValidationReport report)
        {
            var document = new CompanionDocument();
            if (json is null) return document;
            JsonHeader header = JsonInput.Parse(json, report, (input, element) =>
            {
                var d = new CompanionCatalogData();
                bool ok = input.ReadFields(element, "$.data", new Dictionary<string, Action<JsonElement, string>>
                {
                    ["bond"] = (v, p) => d.Bond = ReadBond(input, v, p),
                    ["slots"] = (v, p) => d.Slots = ReadSlots(input, v, p),
                    ["traits"] = (v, p) => d.Traits = input.ReadLimitedList(v, p, CompanionValidator.MaxTraits,
                        n => $"{n} traits, au plus {CompanionValidator.MaxTraits}.", (e, ip) => ReadTrait(input, e, ip)),
                    ["companions"] = (v, p) => d.Companions = input.ReadLimitedList(v, p, CompanionValidator.MaxCompanions,
                        n => $"{n} compagnons, au plus {CompanionValidator.MaxCompanions}.", (e, ip) => ReadCompanion(input, e, ip)),
                });
                document.Data = ok ? d : null;
            });
            document.SchemaVersion = header.SchemaVersion;
            document.Kind = header.Kind;
            document.ContentVersion = header.ContentVersion;
            return document;
        }

        private static BondRulesData? ReadBond(JsonInput input, JsonElement element, string path)
        {
            var b = new BondRulesData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["windowMs"] = (v, p) => b.WindowMs = input.Int64(v, p),
                ["coordinatedGain"] = (v, p) => b.CoordinatedGain = input.Int64(v, p),
                ["protectGain"] = (v, p) => b.ProtectGain = input.Int64(v, p),
                ["campGain"] = (v, p) => b.CampGain = input.Int64(v, p),
            }) ? b : null;
        }

        private static SlotRulesData? ReadSlots(JsonInput input, JsonElement element, string path)
        {
            var s = new SlotRulesData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["initial"] = (v, p) => s.Initial = input.Int64(v, p),
                ["max"] = (v, p) => s.Max = input.Int64(v, p),
                ["secondSlotLevel"] = (v, p) => s.SecondSlotLevel = input.Int64(v, p),
                ["secondSlotQuest"] = (v, p) => s.SecondSlotQuest = input.Text(v, p),
                ["worldBossInCombat"] = (v, p) => s.WorldBossInCombat = input.Int64(v, p),
            }) ? s : null;
        }

        private static TraitData? ReadTrait(JsonInput input, JsonElement element, string path)
        {
            var t = new TraitData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => t.Id = input.Text(v, p),
                ["name"] = (v, p) => t.Name = input.Text(v, p),
                ["gesture"] = (v, p) => t.Gesture = input.Text(v, p),
                ["line"] = (v, p) => t.Line = input.Text(v, p),
            }) ? t : null;
        }

        private static CompanionData? ReadCompanion(JsonInput input, JsonElement element, string path)
        {
            var c = new CompanionData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => c.Id = input.Text(v, p),
                ["name"] = (v, p) => c.Name = input.Text(v, p),
                ["kind"] = (v, p) => c.Kind = input.Text(v, p),
                ["role"] = (v, p) => c.Role = input.Text(v, p),
                ["summary"] = (v, p) => c.Summary = input.Text(v, p),
                ["recruit"] = (v, p) => c.Recruit = ReadRecruit(input, v, p),
                ["randomTrait"] = (v, p) => c.RandomTrait = input.Boolean(v, p),
                ["gift"] = (v, p) => c.Gift = ReadGift(input, v, p),
                ["stages"] = (v, p) => c.Stages = input.ReadLimitedList(v, p, CompanionValidator.MaxStages,
                    n => $"{n} stades, au plus {CompanionValidator.MaxStages}.", (e, ip) => ReadStage(input, e, ip)),
            }) ? c : null;
        }

        private static RecruitData? ReadRecruit(JsonInput input, JsonElement element, string path)
        {
            var r = new RecruitData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["source"] = (v, p) => r.Source = input.Text(v, p),
                ["guildRank"] = (v, p) => r.GuildRank = input.Text(v, p),
                ["price"] = (v, p) => r.Price = input.Int64(v, p),
            }) ? r : null;
        }

        private static GiftData? ReadGift(JsonInput input, JsonElement element, string path)
        {
            var g = new GiftData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => g.Id = input.Text(v, p),
                ["name"] = (v, p) => g.Name = input.Text(v, p),
                ["effect"] = (v, p) => g.Effect = input.Text(v, p),
                ["description"] = (v, p) => g.Description = input.Text(v, p),
            }) ? g : null;
        }

        private static ScaledData? ReadScaled(JsonInput input, JsonElement element, string path)
        {
            var s = new ScaledData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["base"] = (v, p) => s.Base = input.Int64(v, p),
                ["perLevel"] = (v, p) => s.PerLevel = input.Int64(v, p),
            }) ? s : null;
        }

        private static StageData? ReadStage(JsonInput input, JsonElement element, string path)
        {
            var s = new StageData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => s.Id = input.Text(v, p),
                ["name"] = (v, p) => s.Name = input.Text(v, p),
                ["fromLevel"] = (v, p) => s.FromLevel = input.Int64(v, p),
                ["health"] = (v, p) => s.Health = ReadScaled(input, v, p),
                ["armor"] = (v, p) => s.Armor = ReadScaled(input, v, p),
                ["magicResistance"] = (v, p) => s.MagicResistance = ReadScaled(input, v, p),
                ["radius"] = (v, p) => s.Radius = input.Decimal(v, p),
                ["speed"] = (v, p) => s.Speed = input.Decimal(v, p),
                ["maxRange"] = (v, p) => s.MaxRange = input.Decimal(v, p),
                ["minRange"] = (v, p) => s.MinRange = input.Decimal(v, p),
                ["firstAttackMs"] = (v, p) => s.FirstAttackMs = input.Int64(v, p),
                ["intervalMs"] = (v, p) => s.IntervalMs = input.Int64(v, p),
                ["followDistance"] = (v, p) => s.FollowDistance = input.Decimal(v, p),
                ["leash"] = (v, p) => s.Leash = input.Decimal(v, p),
                ["attacks"] = (v, p) => s.Attacks = input.ReadLimitedList(v, p, CompanionValidator.MaxAttacks,
                    n => $"{n} attaques, au plus {CompanionValidator.MaxAttacks}.", (e, ip) => ReadAttack(input, e, ip)),
                ["duo"] = (v, p) => s.Duo = ReadDuo(input, v, p),
            }) ? s : null;
        }

        private static CompanionAttackData? ReadAttack(JsonInput input, JsonElement element, string path)
        {
            var a = new CompanionAttackData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => a.Id = input.Text(v, p),
                ["damage"] = (v, p) => a.Damage = ReadScaled(input, v, p),
                ["damageType"] = (v, p) => a.DamageType = input.Text(v, p),
                ["telegraphMs"] = (v, p) => a.TelegraphMs = input.Int64(v, p),
                ["range"] = (v, p) => a.Range = input.Decimal(v, p),
            }) ? a : null;
        }

        private static DuoDefinitionData? ReadDuo(JsonInput input, JsonElement element, string path)
        {
            var d = new DuoDefinitionData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => d.Id = input.Text(v, p),
                ["name"] = (v, p) => d.Name = input.Text(v, p),
                ["damage"] = (v, p) => d.Damage = ReadScaled(input, v, p),
                ["damageType"] = (v, p) => d.DamageType = input.Text(v, p),
                ["telegraphMs"] = (v, p) => d.TelegraphMs = input.Int64(v, p),
                ["recoveryMs"] = (v, p) => d.RecoveryMs = input.Int64(v, p),
                ["range"] = (v, p) => d.Range = input.Decimal(v, p),
            }) ? d : null;
        }
    }
}
