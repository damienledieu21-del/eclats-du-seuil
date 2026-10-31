using System;
using System.Collections.Generic;
using EdS.Data.Guild;
using EdS.Data.Items;
using EdS.Data.Json.Text;
using EdS.Data.Merchants;
using EdS.Data.Story;
using EdS.Data.Validation;
using EdS.Data.Zones;

namespace EdS.Data.Json
{
    /// <summary>
    /// Sous-lot V1.7 : lecture stricte de la Guilde des aventuriers (<c>kind: "guild"</c>, <c>data/v0/world/guild.json</c> : rangs, titres,
    /// contrats, portes ouvertes par le rang, points des accomplissements). La forme seulement ; le reste va au validateur unique
    /// <see cref="GuildValidator"/>.
    /// </summary>
    public static class GuildJsonReader
    {
        public static string PathIn(string dataDirectory) => System.IO.Path.Combine(dataDirectory, "world", "guild.json");

        public static GuildLoadResult ReadFile(string path, TextCatalog texts, StoryBook story, ZoneDefinition zone, IReadOnlyList<ZoneDefinition> zones,
            ItemCatalog items, MerchantBook? merchants = null)
        {
            var report = new ValidationReport();
            return GuildValidator.Validate(ReadDocument(JsonInput.ReadFile(path, report), report), texts, story, zone, zones, items, merchants, report);
        }

        public static GuildLoadResult Read(string json, TextCatalog texts, StoryBook story, ZoneDefinition zone, IReadOnlyList<ZoneDefinition> zones,
            ItemCatalog items, MerchantBook? merchants = null)
        {
            var report = new ValidationReport();
            return GuildValidator.Validate(ReadDocument(JsonInput.Utf8(json, report), report), texts, story, zone, zones, items, merchants, report);
        }

        /// <summary>Document lu sans validation (tests : document retouché en objets publics, puis validé).</summary>
        internal static GuildDocument ReadDocument(byte[]? json, ValidationReport report)
        {
            var document = new GuildDocument();
            if (json is null) return document;
            JsonHeader header = JsonInput.Parse(json, report, (input, element) =>
            {
                var d = new GuildData();
                bool ok = input.ReadFields(element, "$.data", new Dictionary<string, Action<JsonElement, string>>
                {
                    ["npc"] = (v, p) => d.Npc = input.Text(v, p),
                    ["maxActive"] = (v, p) => d.MaxActive = input.Int32(v, p),
                    ["daily"] = (v, p) => d.Daily = input.Int32(v, p),
                    ["rushDaily"] = (v, p) => d.RushDaily = input.Int32(v, p),   // réponses du 31 octobre 2026 (schéma 2)
                    ["ranks"] = (v, p) => d.Ranks = input.ReadLimitedList(v, p, 16, n => $"{n} rangs, au plus 16.", (e, ip) => ReadRank(input, e, ip)),
                    ["titles"] = (v, p) => d.Titles = input.ReadLimitedList(v, p, GuildValidator.MaxTitles,
                        n => $"{n} titres, au plus {GuildValidator.MaxTitles}.", (e, ip) => ReadTitle(input, e, ip)),
                    ["contracts"] = (v, p) => d.Contracts = input.ReadLimitedList(v, p, GuildValidator.MaxContracts,
                        n => $"{n} contrats, au plus {GuildValidator.MaxContracts}.", (e, ip) => ReadContract(input, e, ip)),
                    ["gates"] = (v, p) => d.Gates = input.ReadLimitedList(v, p, GuildValidator.MaxGates,
                        n => $"{n} portes, au plus {GuildValidator.MaxGates}.", (e, ip) => ReadGate(input, e, ip)),
                    ["points"] = (v, p) => d.Points = ReadPoints(input, v, p),
                });
                document.Data = ok ? d : null;
            });
            document.SchemaVersion = header.SchemaVersion;
            document.Kind = header.Kind;
            document.ContentVersion = header.ContentVersion;
            return document;
        }

        private static RankData? ReadRank(JsonInput input, JsonElement element, string path)
        {
            var x = new RankData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => x.Id = input.Text(v, p),
                ["name"] = (v, p) => x.Name = input.Text(v, p),
                ["title"] = (v, p) => x.Title = input.Text(v, p),
                ["points"] = (v, p) => x.Points = input.Int64(v, p),
                ["open"] = (v, p) => x.Open = input.Boolean(v, p),
                ["trial"] = (v, p) =>
                {
                    x.HasTrial = true;
                    x.Trial = input.NullableText(v, p);
                },
            }) ? x : null;
        }

        private static TitleData? ReadTitle(JsonInput input, JsonElement element, string path)
        {
            var x = new TitleData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => x.Id = input.Text(v, p),
                ["name"] = (v, p) => x.Name = input.Text(v, p),
            }) ? x : null;
        }

        private static ContractData? ReadContract(JsonInput input, JsonElement element, string path)
        {
            var x = new ContractData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["id"] = (v, p) => x.Id = input.Text(v, p),
                ["rank"] = (v, p) => x.Rank = input.Text(v, p),
                ["kind"] = (v, p) => x.Kind = input.Text(v, p),
                ["name"] = (v, p) => x.Name = input.Text(v, p),
                ["text"] = (v, p) => x.Text = input.Text(v, p),
                // Même forme qu'un objectif de quête (kind, id, value, npc), lu par le lecteur de l'histoire.
                ["objective"] = (v, p) => x.Objective = StoryJsonReader.Term(input, v, p, "kind"),
                ["money"] = (v, p) => x.Money = input.Int64(v, p),
                ["points"] = (v, p) => x.Points = input.Int64(v, p),
                ["xp"] = (v, p) => x.Xp = input.Int64(v, p),
                ["rush"] = (v, p) => x.Rush = input.Boolean(v, p),   // réponses du 31 octobre 2026 (schéma 2)
            }) ? x : null;
        }

        private static GateData? ReadGate(JsonInput input, JsonElement element, string path)
        {
            var x = new GateData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["zone"] = (v, p) => x.Zone = input.Text(v, p),
                ["door"] = (v, p) => x.Door = input.Text(v, p),
                ["rank"] = (v, p) => x.Rank = input.Text(v, p),
            }) ? x : null;
        }

        private static PointsData? ReadPoints(JsonInput input, JsonElement element, string path)
        {
            var x = new PointsData();
            return input.ReadFields(element, path, new Dictionary<string, Action<JsonElement, string>>
            {
                ["mainQuest"] = (v, p) => x.MainQuest = input.Int64(v, p),
                ["sideQuest"] = (v, p) => x.SideQuest = input.Int64(v, p),
                ["discovery"] = (v, p) => x.Discovery = input.Int64(v, p),
                ["rare"] = (v, p) => x.Rare = input.ReadLimitedList(v, p, GuildValidator.MaxRare, n => $"{n} créatures, au plus {GuildValidator.MaxRare}.",
                    (e, ip) =>
                    {
                        var c = new RarePointsData();
                        return input.ReadFields(e, ip, new Dictionary<string, Action<JsonElement, string>>
                        {
                            ["creature"] = (cv, cp) => c.Creature = input.Text(cv, cp),
                            ["points"] = (cv, cp) => c.Points = input.Int64(cv, cp),
                        }) ? c : null;
                    }),
                ["facts"] = (v, p) => x.Facts = input.ReadLimitedList(v, p, GuildValidator.MaxFacts, n => $"{n} faits, au plus {GuildValidator.MaxFacts}.",
                    (e, ip) =>
                    {
                        var f = new FactPointsData();
                        return input.ReadFields(e, ip, new Dictionary<string, Action<JsonElement, string>>
                        {
                            ["fact"] = (fv, fp) => f.Fact = input.Text(fv, fp),
                            ["points"] = (fv, fp) => f.Points = input.Int64(fv, fp),
                        }) ? f : null;
                    }),
            }) ? x : null;
        }
    }
}
