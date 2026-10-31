using System;
using System.Collections.Generic;
using EdS.Domain.Combat;
using System.Linq;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
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
            if (setup.Zone) w.Field("zone", true);   // sous-lot 12.1 (format 16) : seulement pour une zone
            if (setup.Space is { } space) WriteSpace(w, space);   // sous-lot 11.4 (format 10) : combat positionné seulement
            w.Name("combatants").BeginArray();
            foreach (CombatantSetup c in setup.Combatants) WriteCombatant(w, c);
            w.EndArray();
            w.Name("commands").BeginArray();
            foreach (CombatCommand c in setup.Commands)
            {
                w.BeginObject().Field("atMs", c.AtMs).Field("actor", c.Actor);
                if (c.Action == CombatAction.UseSkill) w.Field("skill", c.Skill).Field("target", c.Target);
                else w.Field("action", CombatActionIds.Of(c.Action));
                if (c.Action == CombatAction.Select && c.Target != null) w.Field("target", c.Target);   // sous-lot 11.4c
                if (c.Direction is { } direction) WritePoint(w.Name("direction"), direction);   // sous-lot 11.4 : telle que donnée
                if (c.Backward) w.Field("backward", true);   // sous-lot 11.9 (format 15) : seulement à reculons
                if (c.Item != null) w.Field("item", c.Item);   // sous-lot 12.6 (format 18) : objet de soin d'un consume
                w.EndObject();
            }
            w.EndArray().EndObject();
            return w.EndObject().ToString();
        }

        /// <summary>Sous-lot 11.4c : forme, annonce, dimensions et défenses d'une attaque, sous la forme du fichier (champs absents omis).</summary>
        private static void WriteForm(JsonLineWriter w, AssailantAttack attack)
        {
            if (attack.Shape is { } shape)
            {
                w.Field("shape", AttackShapeIds.Of(shape));
                if (attack.TelegraphMs != 0) w.Field("telegraphMs", attack.TelegraphMs);
                if (attack.Range is { } range) w.Field("range", range);
                if (attack.Radius is { } radius) w.Field("radius", radius);
                if (attack.Angle is { } angle) w.Field("angle", angle);
                if (attack.Width is { } width) w.Field("width", width);
                if (attack.Center != ShapeCenter.Target) w.Field("center", AttackShapeIds.Of(attack.Center));
                if (attack.PersistentMs != 0) w.Field("persistentMs", attack.PersistentMs);
                if (attack.PulseMs != 0) w.Field("tickMs", attack.PulseMs);
            }
            if (attack.GuardableOverride is { } guardable) w.Field("guardable", guardable);
            if (attack.ParryableOverride is { } parryable) w.Field("parryable", parryable);
            if (attack.Charge) w.Field("charge", true);   // sous-lot 11.5 (format 14)
            if (attack.Push is { } push) w.Field("push", push);   // sous-lot 11.9 (format 15) : seulement les champs donnés
            if (!attack.HoldPosition) w.Field("holdPosition", false);
        }

        /// <summary>Sous-lot 11.4 : point ou direction sous la forme des fichiers de scénario, <c>[x, z]</c>.</summary>
        internal static JsonLineWriter WritePoint(JsonLineWriter w, GroundVector p) => w.BeginArray().Value(p.X).Value(p.Z).EndArray();

        private static void WriteSpace(JsonLineWriter w, CombatSpace space)
        {
            w.Name("space").BeginObject();
            w.Name("bounds").BeginObject();
            WritePoint(w.Name("min"), space.Min);
            WritePoint(w.Name("max"), space.Max);
            w.EndObject();
            w.Name("obstacles").BeginArray();
            foreach (SpaceObstacle obstacle in space.Obstacles)
            {
                w.BeginObject().Field("id", obstacle.Id);
                w.Name("points").BeginArray();
                foreach (GroundVector p in obstacle.Points) WritePoint(w, p);
                w.EndArray();
                if (obstacle.Low) w.Field("low", true);   // sous-lot 11.9 (format 15) : seulement s'il est bas
                w.EndObject();
            }
            w.EndArray().EndObject();
        }

        /// <summary>Sous-lot 11.4 : place de départ, sous la forme du fichier de scénario ; la vitesse seulement si elle est donnée.</summary>
        private static void WritePlacement(JsonLineWriter w, CombatantPlacement p)
        {
            WritePoint(w.Name("position"), p.Position);
            WritePoint(w.Name("facing"), p.Facing);
            w.Field("radius", p.Radius);
            if (p.Speed is Fixed speed) w.Field("speed", speed);
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
                    // Sous-lot 12.6 (format 18) : objets de soin emportés, seulement s'il y en a.
                    if (character.Consumables.Count > 0)
                    {
                        w.Name("consumables").BeginArray();
                        foreach (ConsumableSetup consumable in character.Consumables)
                            w.BeginObject().Field("id", consumable.Id).Field("heal", consumable.Heal).Field("charges", consumable.Charges)
                                .Field("cooldownMs", consumable.CooldownMs).EndObject();
                        w.EndArray();
                    }
                    // Sous-lot 12.3 (format 17) : bonus de l'équipement porté, seulement ses champs non nuls, et seulement s'il y en a.
                    if (!character.Gear.IsNone)
                    {
                        w.Name("gear").BeginObject();
                        for (int g = 0; g < Domain.Stats.GearBonus.Fields.Count; g++)
                            if (character.Gear[g] != 0) w.Field(Domain.Stats.GearBonus.Fields[g], character.Gear[g]);
                        w.EndObject();
                    }
                    // Sous-lot V1.2 (format 19) : garde du bouclier porté, seulement s'il y en a un, sous la forme du fichier de scénario.
                    if (character.Shield is { } shield)
                        w.Name("shield").BeginObject().Field(ShieldGuard.DamageReductionId, shield.DamageReduction)
                            .Field(ShieldGuard.CostPerBlockId, shield.CostPerBlock).Field(ShieldGuard.ParryWindowMsId, shield.ParryWindowMs).EndObject();
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
                        WriteForm(w, attack);   // sous-lot 11.4c (format 12) : seulement les champs donnés
                        w.EndObject();
                    }
                    w.EndArray();
                    // Lot 7.3 (Q4) : écrits seulement s'il y en a, sous la forme du fichier de scénario.
                    if (assailant.Triggers.Count > 0) WriteTriggers(w, assailant.Triggers);
                    // Sous-lot 11.1 : PV d'un assaillant mortel, écrits seulement s'il en a (format 9), comme dans le fichier de scénario.
                    if (assailant.Health is long health) w.Field("health", health);
                    // Sous-lot 11.5 (format 14) : comportement, seulement s'il en a un, sous la forme du fichier de scénario.
                    if (assailant.Behavior is { } behavior)
                    {
                        w.Name("behavior").BeginObject().Field("maxRange", behavior.MaxRange);
                        if (behavior.MinRange != Domain.Numerics.Fixed.Zero) w.Field("minRange", behavior.MinRange);
                        if (behavior.AggroRange is { } aggro) w.Field("aggroRange", aggro);
                        if (behavior.Encircle) w.Field("encircle", true);
                        if (!behavior.Pack) w.Field("pack", false);   // sous-lot 11.9 (format 15)
                        w.EndObject();
                    }
                    break;
            }
            if (c.Placement is { } placement) WritePlacement(w, placement);
            if (c.ArrivalMs is long arrivalMs) w.Field("arrivalMs", arrivalMs);   // sous-lot 11.4d (format 13) : seulement s'il arrive en route
            w.EndObject();
        }
    }
}
