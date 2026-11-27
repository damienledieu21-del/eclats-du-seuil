using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Items;
using EdS.Data.Validation;
using EdS.Domain.Collections;
using EdS.Domain.Combat;
using EdS.Domain.Space;

namespace EdS.Data.Zones
{
    /// <summary>V2.52 (schéma 18) : genre d'un secret.</summary>
    public enum SecretKind
    {
        /// <summary>« coffre » : des récompenses, données à la première fouille.</summary>
        Chest = 0,

        /// <summary>« lore » : une stèle à lire (ses récompenses, s'il y en a, à la première lecture).</summary>
        Lore = 1,
    }

    /// <summary>
    /// V2.52 (schéma 18, le contenu de l'Envers des douze lieux) : secret d'une zone : un coffre ou une stèle de lore, à une place dégagée de
    /// l'aire. On le fouille à portée (<c>WorldSession.OpenSecret</c>) : son texte est montré, ses récompenses données une seule fois, en entier
    /// ou pas du tout ; la clé <see cref="ZoneDefinition.SecretKey"/> entre alors au registre du monde (sauvegarde inchangée). Sans repère sur
    /// la carte : on le trouve en explorant.
    /// </summary>
    public sealed class ZoneSecret
    {
        public string Id { get; }

        public string Name { get; }

        public SecretKind Kind { get; }

        public GroundVector Position { get; }

        /// <summary>Clé du texte montré à la fouille.</summary>
        public string Text { get; }

        /// <summary>Récompenses de la première fouille (au moins une pour un coffre).</summary>
        public IReadOnlyList<(string Item, int Count)> Rewards { get; }

        internal ZoneSecret(string id, string name, SecretKind kind, GroundVector position, string text, IEnumerable<(string, int)> rewards)
        {
            Id = id;
            Name = name;
            Kind = kind;
            Position = position;
            Text = text;
            Rewards = Frozen.List(rewards, nameof(rewards));
        }
    }

    public static partial class ZoneValidator
    {
        /// <summary>V2.52 : nom d'un genre de secret dans les données.</summary>
        public static string SecretKindName(SecretKind kind) => kind == SecretKind.Chest ? "coffre" : "lore";

        /// <summary>
        /// V2.52 (schéma 18) : un secret : identifiant unique, nom, genre (« coffre » ou « lore »), place dans l'aire, dégagée des obstacles
        /// (<see cref="SecretClearance"/>), clé de texte « text.… », récompenses (objets connus, quantités dans leur pile ; au moins une pour un
        /// coffre, au plus <see cref="MaxRewards"/>). Null, avec les erreurs au chemin précis, s'il est refusé.
        /// </summary>
        private static ZoneSecret? CheckSecret(SecretData? s, string path, IReadOnlyList<ZoneSecret> previous, CombatSpace? space, CombatSetup? scenario,
            ItemCatalog items, ValidationReport report)
        {
            if (s is null) { Missing(report, path, "secret"); return null; }
            bool ok = CheckId(s.Id, path + ".id", previous.Select(x => x.Id), report) & CheckName(s.Name, path + ".name", report);
            SecretKind kind = SecretKind.Chest;
            if (s.Kind is null) { Missing(report, path + ".kind", "genre du secret (« coffre » ou « lore »)"); ok = false; }
            else if (s.Kind == "lore") kind = SecretKind.Lore;
            else if (s.Kind != "coffre") { report.Add(path + ".kind", ValidationCode.InvalidValue, $"genre de secret « {s.Kind} » inconnu : « coffre » ou « lore » attendu."); ok = false; }
            if (!CheckPoint(s.Position, path + ".position", "place du secret [x, z]", report)) ok = false;
            else if (space != null && scenario is { Zone: true } && Clear(Point(s.Position!), SecretClearance, space, "du secret") is string why)
            { report.Add(path + ".position", ValidationCode.InvalidValue, why); ok = false; }
            if (s.Text is null) { Missing(report, path + ".text", "clé du texte du secret"); ok = false; }
            else if (s.Text.Length > 200 || !TextKey.IsMatch(s.Text)) { report.Add(path + ".text", ValidationCode.InvalidValue, $"clé de texte « {s.Text} » mal formée : « text.… »."); ok = false; }
            var rewards = new List<(string, int)>();
            if (s.Rewards is null || s.Rewards.Count == 0)
            {
                if (kind == SecretKind.Chest) { Missing(report, path + ".rewards", "récompenses du coffre (au moins une)"); ok = false; }
            }
            else if (s.Rewards.Count > MaxRewards) { report.Add(path + ".rewards", ValidationCode.LimitExceeded, $"au plus {MaxRewards} récompenses."); ok = false; }
            else
                for (int i = 0; i < s.Rewards.Count; i++)
                {
                    string p = $"{path}.rewards[{i}]";
                    if (s.Rewards[i]?.Item is not string item || !items.TryGetItem(item, out ItemDefinition def)) { report.Add(p + ".item", ValidationCode.UnknownReference, $"objet inconnu « {s.Rewards[i]?.Item} »."); ok = false; }
                    else if (s.Rewards[i]!.Count is not int count || count < 1 || count > def.Stack) { report.Add(p + ".count", ValidationCode.OutOfRange, $"quantité hors de [1, {def.Stack}]."); ok = false; }
                    else if (rewards.Any(r => r.Item1 == item)) { report.Add(p + ".item", ValidationCode.DuplicateId, $"objet « {item} » déjà donné par ce secret."); ok = false; }
                    else rewards.Add((item, count));
                }
            return ok ? new ZoneSecret(s.Id!, s.Name!, kind, Point(s.Position!), s.Text!, rewards) : null;
        }
    }
}
