using System;
using System.Linq;
using EdS.Domain.Stats;

namespace EdS.Simulation.Engine
{
    /// <summary>
    /// Première ligne du journal : format, version du moteur, identité des données (versions et empreintes) et
    /// scénario complet (graine, participants, commandes). Le scénario y est écrit sous la même forme que le champ
    /// <c>data</c> d'un fichier de scénario : on peut rejouer le combat à partir du journal seul.
    /// </summary>
    public static class CombatLogHeader
    {
        public static string Write(CombatSetup setup)
        {
            if (setup is null) throw new ArgumentNullException(nameof(setup));
            var w = new JsonLineWriter().BeginObject()
                .Field("type", "header")
                .Field("format", CombatEngine.LogFormat)
                .Field("formatVersion", CombatEngine.LogFormatVersion)
                .Field("engineVersion", CombatEngine.Version)
                .Field("tickMs", setup.Combat.TickMilliseconds);

            w.Name("data").BeginObject();
            WriteIdentity(w.Name("rules"), setup.Data.Rules, withId: false);
            w.Name("classes").BeginArray();
            foreach (DataFileIdentity c in setup.Data.Classes) WriteIdentity(w, c, withId: true);
            w.EndArray().EndObject();

            w.Name("scenario").BeginObject()
                .Field("seed", setup.Seed)
                .Field("durationMs", setup.DurationMs);
            w.Name("combatants").BeginArray();
            foreach (CombatantSetup c in setup.Combatants) WriteCombatant(w, c);
            w.EndArray();
            w.Name("commands").BeginArray();
            foreach (CombatCommand c in setup.Commands)
                w.BeginObject().Field("atMs", c.AtMs).Field("actor", c.Actor).Field("skill", c.Skill).Field("target", c.Target).EndObject();
            w.EndArray().EndObject();
            return w.EndObject().ToString();
        }

        private static void WriteIdentity(JsonLineWriter w, DataFileIdentity identity, bool withId)
        {
            w.BeginObject();
            if (withId) w.Field("id", identity.Id);
            w.Field("contentVersion", identity.ContentVersion).Field("fingerprint", identity.Fingerprint).EndObject();
        }

        /// <summary>Identifiant d'un attribut dans les données (« force », « dexterite »…), comme dans rules.json.</summary>
        public static string AttributeId(AttributeKind kind) => kind.ToString().ToLowerInvariant();

        private static void WriteCombatant(JsonLineWriter w, CombatantSetup c)
        {
            w.BeginObject().Field("id", c.Id);
            switch (c)
            {
                case CharacterSetup character:
                    w.Field("type", "character").Field("team", c.Team).Field("initiative", c.Initiative).Field("class", character.Class.Id);
                    var build = character.Build;
                    w.Name("build").BeginObject().Field("level", build.Level);
                    w.Name("attributes").BeginObject();
                    foreach (AttributeKind kind in (AttributeKind[])Enum.GetValues(typeof(AttributeKind)))
                        w.Field(AttributeId(kind), build.Attributes[kind]);
                    w.EndObject();
                    w.Name("talents").BeginObject();
                    foreach (var talent in build.TalentRanks.OrderBy(t => t.Key, StringComparer.Ordinal)) w.Field(talent.Key, talent.Value);
                    w.EndObject();
                    w.Name("activeSkills").BeginArray();
                    foreach (string skill in build.ActiveSkills) w.Value(skill);
                    w.EndArray();
                    w.Field("ultimate", build.Ultimate);
                    w.Name("passiveSkills").BeginArray();
                    foreach (string skill in build.PassiveSkills) w.Value(skill);
                    w.EndArray().EndObject();
                    break;
                case DummySetup dummy:
                    w.Field("type", "dummy").Field("team", c.Team).Field("initiative", c.Initiative)
                        .Field("armor", dummy.Armor).Field("magicResistance", dummy.MagicResistance);
                    break;
            }
            w.EndObject();
        }
    }
}
