using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using EdS.Data.Classes;
using EdS.Data.Validation;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;

namespace EdS.Data.Triggers
{
    /// <summary>
    /// Déclencheur brut (lot 7.3), même forme dans les classes (<c>talents[].triggers</c>, <c>skills[].triggers</c>), les règles
    /// (<c>statuses[].triggers</c>) et les scénarios (assaillants) :
    /// <c>{ "on": "onHitTaken", "role": "target", "filter": { … }, "cooldownMs": 0, "effects": [ … ] }</c>.
    /// </summary>
    public sealed class TriggerData
    {
        public string? On { get; set; }
        public string? Role { get; set; }
        public TriggerFilterData? Filter { get; set; }
        public long? CooldownMs { get; set; }
        public List<TriggerEffectData?>? Effects { get; set; }
    }

    public sealed class TriggerFilterData
    {
        public List<string?>? Skills { get; set; }
        public string? DamageType { get; set; }
        public List<string?>? Defense { get; set; }
        public string? Status { get; set; }
        public string? Category { get; set; }
        public string? Resource { get; set; }
        public bool? Renewed { get; set; }
    }

    public sealed class TriggerEffectData
    {
        /// <summary>« strike », « applyStatus » ou « gainResource ».</summary>
        public string? Type { get; set; }

        public string? Target { get; set; }
        public RankedData? Coefficient { get; set; }
        public string? PowerType { get; set; }
        public string? DamageType { get; set; }
        public bool? Unblockable { get; set; }
        public string? Status { get; set; }
        public string? Resource { get; set; }
        public RankedData? Amount { get; set; }
    }

    /// <summary>Valeur par rang : un nombre décimal, ou <c>{ "base": x, "perRank": y }</c> (perRank facultatif, 0 par défaut).</summary>
    public sealed class RankedData
    {
        public Fixed? Base { get; set; }
        public Fixed? PerRank { get; set; }
    }

    /// <summary>
    /// Forme des déclencheurs (lot 7.3) : présence des champs obligatoires et identifiants des énumérations. Toute la cohérence de
    /// gameplay (type, rôle et filtre compatibles, bornes, références) est vérifiée par <see cref="TriggerDefinition.ValidateList"/>
    /// et <see cref="TriggerDefinition.ValidateReferences"/>, seule validation de gameplay.
    /// </summary>
    public static class TriggerShape
    {
        /// <summary>Vérifie une liste facultative (null = absente, aucun déclencheur).</summary>
        public static void Check(List<TriggerData?>? triggers, string path, ValidationReport report)
        {
            if (triggers is null) return;
            bool Present(object? value, string p, string description)
            {
                if (value != null) return true;
                if (!report.HasErrorAtOrAbove(p)) report.Add(p, ValidationCode.MissingField, $"champ obligatoire absent ({description}).");
                return false;
            }
            void Token<T>(string? value, string p, string description, IReadOnlyDictionary<string, T> allowed, bool required)
            {
                if (value is null)
                {
                    if (required) Present(null, p, description);
                    return;
                }
                if (!allowed.ContainsKey(value))
                    report.Add(p, ValidationCode.UnknownReference, $"valeur inconnue « {value} » ; valeurs possibles : {string.Join(", ", allowed.Keys)}.");
            }
            void Items(List<string?>? values, string p, IReadOnlyDictionary<string, HitDefense>? allowed)
            {
                if (values is null) return;
                for (int k = 0; k < values.Count; k++)
                {
                    string ip = $"{p}[{k}]";
                    if (values[k] is not { } v)
                    {
                        if (!report.HasErrorAtOrAbove(ip)) report.Add(ip, ValidationCode.WrongType, "identifiant attendu.");
                    }
                    else if (allowed != null && !allowed.ContainsKey(v))
                        report.Add(ip, ValidationCode.UnknownReference, $"valeur inconnue « {v} » ; valeurs possibles : {string.Join(", ", allowed.Keys)}.");
                }
            }
            void Ranked(RankedData? value, string p)
            {
                if (value != null) Present(value.Base, p + ".base", "nombre décimal");
            }

            for (int i = 0; i < triggers.Count; i++)
            {
                string p = $"{path}[{i}]";
                if (triggers[i] is not { } t)
                {
                    if (!report.HasErrorAtOrAbove(p)) report.Add(p, ValidationCode.WrongType, "objet attendu.");
                    continue;
                }
                Token(t.On, p + ".on", string.Join(", ", TriggerIds.Events.Keys), TriggerIds.Events, true);
                Token(t.Role, p + ".role", string.Join(", ", TriggerIds.Roles.Keys), TriggerIds.Roles, false);
                if (t.Filter is { } f)
                {
                    string fp = p + ".filter";
                    Items(f.Skills, fp + ".skills", null);
                    Token(f.DamageType, fp + ".damageType", "physique ou magique", ClassValidator.DamageTypes, false);
                    Items(f.Defense, fp + ".defense", TriggerIds.Defenses);
                    Token(f.Category, fp + ".category", string.Join(", ", StatusIds.Categories.Keys), StatusIds.Categories, false);
                    Token(f.Resource, fp + ".resource", string.Join(", ", TriggerIds.Resources.Keys), TriggerIds.Resources, false);
                }
                if (!Present(t.Effects, p + ".effects", "liste de 1 à 4 effets")) continue;
                for (int k = 0; k < t.Effects!.Count; k++)
                {
                    string ep = $"{p}.effects[{k}]";
                    if (t.Effects[k] is not { } e)
                    {
                        if (!report.HasErrorAtOrAbove(ep)) report.Add(ep, ValidationCode.WrongType, "objet attendu.");
                        continue;
                    }
                    Token(e.Type, ep + ".type", string.Join(", ", TriggerIds.Effects.Keys), TriggerIds.Effects, true);
                    Token(e.Target, ep + ".target", string.Join(", ", TriggerIds.Targets.Keys), TriggerIds.Targets, false);
                    Token(e.PowerType, ep + ".powerType", "physique, agile ou magique", ClassValidator.PowerTypes, false);
                    Token(e.DamageType, ep + ".damageType", "physique ou magique", ClassValidator.DamageTypes, false);
                    Token(e.Resource, ep + ".resource", string.Join(", ", TriggerIds.Resources.Keys), TriggerIds.Resources, false);
                    Ranked(e.Coefficient, ep + ".coefficient");
                    Ranked(e.Amount, ep + ".amount");
                }
            }
        }

        /// <summary>Conversion pour le domaine ; suppose <see cref="Check"/> sans erreur. Liste absente : aucun déclencheur.</summary>
        public static IReadOnlyList<TriggerDefinition> Build(List<TriggerData?>? triggers) =>
            triggers is null ? Array.Empty<TriggerDefinition>() : triggers.Select(t => Build(t!)).ToArray();

        private static TriggerDefinition Build(TriggerData t)
        {
            TriggerFilter filter = t.Filter is not { } f ? TriggerFilter.None : new TriggerFilter(
                f.Skills?.Select(s => s!), f.DamageType is null ? null : ClassValidator.DamageTypes[f.DamageType],
                f.Defense?.Select(d => TriggerIds.Defenses[d!]), f.Status, f.Category is null ? null : StatusIds.Categories[f.Category],
                f.Resource is null ? null : TriggerIds.Resources[f.Resource], f.Renewed);
            RankedValue? Ranked(RankedData? r) => r is null ? null : new RankedValue(r.Base!.Value, r.PerRank ?? Fixed.Zero);
            var effects = t.Effects!.Select(e => new TriggerEffect(TriggerIds.Effects[e!.Type!],
                e.Target is null ? null : TriggerIds.Targets[e.Target], Ranked(e.Coefficient),
                e.PowerType is null ? null : ClassValidator.PowerTypes[e.PowerType], e.DamageType is null ? null : ClassValidator.DamageTypes[e.DamageType],
                e.Unblockable ?? false, e.Status, e.Resource is null ? null : TriggerIds.Resources[e.Resource], Ranked(e.Amount)));
            return new TriggerDefinition(TriggerIds.Events[t.On!], effects, t.Role is null ? null : TriggerIds.Roles[t.Role], filter, t.CooldownMs ?? 0);
        }

        /// <summary>
        /// Écriture canonique pour les empreintes : rien si la liste est absente ou vide (les données sans déclencheur gardent leur
        /// empreinte), sinon chaque champ dans un ordre fixe, avec sa valeur neutre s'il est absent.
        /// </summary>
        public static void AppendCanonical(StringBuilder text, string path, IReadOnlyList<TriggerDefinition> triggers)
        {
            if (triggers.Count == 0) return;
            void Line(string p, string value) => text.Append(p).Append('=').Append(Escape(value)).Append('\n');
            string Ranked(RankedValue? v) => v is { } r ? (r.PerRank == Fixed.Zero ? r.Base.ToString() : $"{r.Base}+{r.PerRank}/rank") : "none";
            Line(path + ".count", triggers.Count.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < triggers.Count; i++)
            {
                TriggerDefinition t = triggers[i];
                string p = $"{path}[{i}]";
                Line(p + ".on", TriggerIds.Of(t.On));
                Line(p + ".role", t.Role is { } role ? TriggerIds.Of(role) : "none");
                TriggerFilter f = t.Filter;
                Line(p + ".filter.skills", string.Join(",", f.Skills));
                Line(p + ".filter.damageType", f.DamageType is { } dt ? Rules.RulesCatalog.Id(dt) : "none");
                Line(p + ".filter.defense", string.Join(",", f.Defenses.Select(TriggerIds.Of)));
                Line(p + ".filter.status", f.Status ?? "none");
                Line(p + ".filter.category", f.Category is { } c ? StatusIds.Of(c) : "none");
                Line(p + ".filter.resource", f.Resource is { } r ? TriggerIds.Of(r) : "none");
                Line(p + ".filter.renewed", f.Renewed is { } b ? (b ? "true" : "false") : "none");
                Line(p + ".cooldownMs", t.CooldownMs.ToString(CultureInfo.InvariantCulture));
                Line(p + ".effects.count", t.Effects.Count.ToString(CultureInfo.InvariantCulture));
                for (int k = 0; k < t.Effects.Count; k++)
                {
                    TriggerEffect e = t.Effects[k];
                    string ep = $"{p}.effects[{k}]";
                    Line(ep + ".type", TriggerIds.Of(e.Kind));
                    Line(ep + ".target", e.Target is { } target ? TriggerIds.Of(target) : "none");
                    Line(ep + ".coefficient", Ranked(e.Coefficient));
                    Line(ep + ".powerType", e.PowerType is { } pt ? ClassValidator.PowerTypes.First(x => x.Value == pt).Key : "none");
                    Line(ep + ".damageType", e.DamageType is { } edt ? Rules.RulesCatalog.Id(edt) : "none");
                    Line(ep + ".unblockable", e.Unblockable ? "true" : "false");
                    Line(ep + ".status", e.Status ?? "none");
                    Line(ep + ".resource", e.Resource is { } res ? TriggerIds.Of(res) : "none");
                    Line(ep + ".amount", Ranked(e.Amount));
                }
            }
        }

        private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\r", "\\r");
    }
}
