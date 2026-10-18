using System;
using System.Collections.Generic;
using EdS.Domain.Combat;
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
            {
                w.BeginObject().Field("atMs", c.AtMs).Field("actor", c.Actor);
                if (c.Action == CombatAction.UseSkill) w.Field("skill", c.Skill).Field("target", c.Target);
                else w.Field("action", CombatActionIds.Of(c.Action));
                w.EndObject();
            }
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

        /// <summary>Identifiant d'un type de dégâts dans les données (« physique », « magique »).</summary>
        public static string DamageTypeId(DamageType type) => type.ToString().ToLowerInvariant();

        /// <summary>Identifiant d'un ciblage dans les données (« enemy », « allEnemies »).</summary>
        public static string TargetingId(Domain.Builds.SkillTargeting targeting) =>
            targeting == Domain.Builds.SkillTargeting.Enemy ? "enemy" : "allEnemies";

        /// <summary>Altérations portées (lot 7.2), sous la forme du fichier de scénario : le coefficient n'est écrit que s'il existe.</summary>
        internal static void WriteApplies(JsonLineWriter w, IReadOnlyList<StatusApplication> applies)
        {
            w.Name("applies").BeginArray();
            foreach (StatusApplication a in applies)
            {
                w.BeginObject().Field("status", a.Status);
                if (a.Coefficient is { } coefficient) w.Field("coefficient", coefficient);
                if (ApplicationTargets.Of(a.Target) is { } target) w.Field("target", target);   // sous-lot 8.3 : cible propre seulement
                w.EndObject();
            }
            w.EndArray();
        }

        /// <summary>Déclencheurs (lot 7.3) sous la forme des fichiers de données ; les champs absents ou neutres ne sont pas écrits.</summary>
        internal static void WriteTriggers(JsonLineWriter w, IReadOnlyList<TriggerDefinition> triggers)
        {
            void Ranked(string name, RankedValue? value)
            {
                if (value is not { } v) return;
                if (v.PerRank == Domain.Numerics.Fixed.Zero) w.Field(name, v.Base);
                else w.Name(name).BeginObject().Field("base", v.Base).Field("perRank", v.PerRank).EndObject();
            }
            w.Name("triggers").BeginArray();
            foreach (TriggerDefinition t in triggers)
            {
                w.BeginObject().Field("on", TriggerIds.Of(t.On));
                if (t.Role is { } role) w.Field("role", TriggerIds.Of(role));
                if (!t.Filter.IsEmpty)
                {
                    TriggerFilter f = t.Filter;
                    w.Name("filter").BeginObject();
                    if (f.Skills.Count > 0)
                    {
                        w.Name("skills").BeginArray();
                        foreach (string s in f.Skills) w.Value(s);
                        w.EndArray();
                    }
                    if (f.DamageType is { } dt) w.Field("damageType", DamageTypeId(dt));
                    if (f.Defenses.Count > 0)
                    {
                        w.Name("defense").BeginArray();
                        foreach (HitDefense d in f.Defenses) w.Value(TriggerIds.Of(d));
                        w.EndArray();
                    }
                    if (f.Status != null) w.Field("status", f.Status);
                    if (f.Category is { } c) w.Field("category", StatusIds.Of(c));
                    if (f.Resource is { } r) w.Field("resource", TriggerIds.Of(r));
                    if (f.Renewed is { } renewed) w.Field("renewed", renewed);
                    w.EndObject();
                }
                if (t.CooldownMs != 0) w.Field("cooldownMs", t.CooldownMs);
                w.Name("effects").BeginArray();
                foreach (TriggerEffect e in t.Effects)
                {
                    w.BeginObject().Field("type", TriggerIds.Of(e.Kind));
                    if (e.Target is { } target) w.Field("target", TriggerIds.Of(target));
                    Ranked("coefficient", e.Coefficient);
                    if (e.PowerType is { } power) w.Field("powerType", power.ToString().ToLowerInvariant());
                    if (e.DamageType is { } type) w.Field("damageType", DamageTypeId(type));
                    if (e.Unblockable) w.Field("unblockable", true);
                    if (e.Status != null) w.Field("status", e.Status);
                    if (e.Resource is { } resource) w.Field("resource", TriggerIds.Of(resource));
                    Ranked("amount", e.Amount);
                    w.EndObject();
                }
                w.EndArray().EndObject();
            }
            w.EndArray();
        }

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
                        .Field("armor", dummy.Armor).Field("magicResistance", dummy.MagicResistance).Field("statusResistance", dummy.StatusResistance);
                    break;
                case AssailantSetup assailant:
                    w.Field("type", "assailant").Field("team", c.Team).Field("initiative", c.Initiative)
                        .Field("armor", assailant.Armor).Field("magicResistance", assailant.MagicResistance).Field("statusResistance", assailant.StatusResistance)
                        .Field("target", assailant.Target).Field("firstAttackMs", assailant.FirstAttackMs).Field("intervalMs", assailant.IntervalMs);
                    w.Name("attacks").BeginArray();
                    foreach (AssailantAttack attack in assailant.Attacks)
                    {
                        w.BeginObject().Field("id", attack.Id).Field("rawDamage", attack.RawDamage)
                            .Field("damageType", DamageTypeId(attack.DamageType)).Field("targeting", TargetingId(attack.Targeting))
                            .Field("unblockable", attack.Unblockable);
                        WriteApplies(w, attack.Applies);
                        w.EndObject();
                    }
                    w.EndArray();
                    // Lot 7.3 (Q4) : écrits seulement s'il y en a, sous la forme du fichier de scénario.
                    if (assailant.Triggers.Count > 0) WriteTriggers(w, assailant.Triggers);
                    break;
            }
            w.EndObject();
        }
    }
}
