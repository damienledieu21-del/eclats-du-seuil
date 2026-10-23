using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EdS.Data.Validation;
using EdS.Domain.Collections;

namespace EdS.Data.Saves
{
    /// <summary>
    /// Sous-lot 12.1 (ARCHITECTURE_MONDE.md, section 6 ; cadrage Q10, tranche : fichier local) : sauvegarde du monde validée. Le personnage,
    /// l'inventaire et les quêtes s'y ajoutent aux sous-lots suivants (version de format incrémentée à chaque ajout).
    /// </summary>
    public sealed class WorldSave
    {
        /// <summary>Zone où se trouve le joueur.</summary>
        public string Zone { get; }

        /// <summary>Temps du monde en ticks (le jour et la nuit s'en déduisent, jamais de l'horloge réelle).</summary>
        public long WorldTick { get; }

        /// <summary>Sessions de zone déjà jouées.</summary>
        public long Sessions { get; }

        /// <summary>Clés des événements consommables déjà attribués, triées (ordre ordinal), sans doublon.</summary>
        public IReadOnlyList<string> Consumed { get; }

        public WorldSave(string zone, long worldTick, long sessions, IEnumerable<string> consumed)
        {
            Zone = zone ?? throw new ArgumentNullException(nameof(zone));
            WorldTick = worldTick;
            Sessions = sessions;
            Consumed = Frozen.List((consumed ?? throw new ArgumentNullException(nameof(consumed))).Distinct(StringComparer.Ordinal)
                .OrderBy(k => k, StringComparer.Ordinal), nameof(consumed));
        }
    }

    /// <summary>Résultat d'une lecture de sauvegarde : la sauvegarde validée, ou le rapport qui explique pourquoi pas.</summary>
    public sealed class SaveLoadResult
    {
        public ValidationReport Report { get; }
        public WorldSave? Save { get; }
        public bool IsValid => Save != null;

        internal SaveLoadResult(ValidationReport report, WorldSave? save)
        {
            Report = report;
            Save = save;
        }
    }

    /// <summary>
    /// Validateur unique des sauvegardes (<c>kind: "save"</c>, sous-lot 12.1). Une version antérieure ou postérieure est refusée avec son
    /// chemin (pré-alpha : la tranche n'a pas encore de joueurs à migrer ; les migrations arriveront avec les premiers testeurs, Q10).
    /// </summary>
    public static class SaveValidator
    {
        public const string Kind = "save";

        /// <summary>Version 1 (sous-lot 12.1) : <c>data.zone</c>, <c>data.worldTick</c>, <c>data.sessions</c>, <c>data.consumed</c>.</summary>
        public const int CurrentSchemaVersion = 1;

        /// <summary>Plafond des événements consommés gardés dans une sauvegarde (enveloppe de la tranche).</summary>
        public const int MaxConsumed = 100_000;

        /// <summary>Plafond technique du temps du monde : un an de jeu en ticks de 50 ms.</summary>
        public const long MaxWorldTick = 365L * 24 * 3600 * 20;

        // Identifiants de zone (« zone.vallons ») et clés d'événements (« zone.vallons/1/death/loup1 ») : ASCII, sans espace.
        private static readonly Regex ZoneId = new Regex(@"^zone\.[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant);
        private static readonly Regex EventKey = new Regex(@"^[a-z0-9_.]+(/[a-z0-9_.]+)+\z", RegexOptions.CultureInvariant);

        /// <summary>Identifiant de zone « zone.&lt;nom&gt; », 64 caractères au plus (revue Codex de la PR #42 : les clés d'événement restent bornées).</summary>
        public static bool IsZoneId(string? id) => id != null && id.Length <= 64 && ZoneId.IsMatch(id);

        public static bool IsEventKey(string? key) => key != null && key.Length <= 200 && EventKey.IsMatch(key);

        public static SaveLoadResult Validate(SaveDocument document, ValidationReport? report = null)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));
            report ??= new ValidationReport();
            if (!HeaderValidator.Validate(document.SchemaVersion, document.Kind, document.ContentVersion, Kind, CurrentSchemaVersion, report))
                return new SaveLoadResult(report, null);
            SaveData? data = document.Data;
            if (data is null)
            {
                if (!report.HasErrorAtOrAbove("$.data")) report.Add("$.data", ValidationCode.MissingField, "champ obligatoire absent (zone, temps du monde, sessions, événements consommés).");
                return new SaveLoadResult(report, null);
            }
            if (data.Zone is null) Missing(report, "$.data.zone", "identifiant de zone, par exemple « zone.vallons »");
            else if (!IsZoneId(data.Zone)) report.Add("$.data.zone", ValidationCode.InvalidValue, $"identifiant de zone « {data.Zone} » invalide : « zone.<nom> » attendu.");
            if (data.WorldTick is null) Missing(report, "$.data.worldTick", "temps du monde en ticks");
            else if (data.WorldTick < 0 || data.WorldTick > MaxWorldTick)
                report.Add("$.data.worldTick", ValidationCode.OutOfRange, $"temps du monde hors de [0, {MaxWorldTick}] ticks.");
            if (data.Sessions is null) Missing(report, "$.data.sessions", "nombre de sessions jouées");
            else if (data.Sessions < 0 || data.Sessions > MaxWorldTick)
                report.Add("$.data.sessions", ValidationCode.OutOfRange, $"nombre de sessions hors de [0, {MaxWorldTick}].");
            if (data.Consumed is null) Missing(report, "$.data.consumed", "liste des événements consommés (vide au départ)");
            else
            {
                if (data.Consumed.Count > MaxConsumed)
                    report.Add("$.data.consumed", ValidationCode.LimitExceeded, $"{data.Consumed.Count} événements consommés, au plus {MaxConsumed}.");
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < data.Consumed.Count; i++)
                {
                    string? key = data.Consumed[i];
                    string path = $"$.data.consumed[{i}]";
                    if (key is null) { if (!report.HasErrorAtOrAbove(path)) report.Add(path, ValidationCode.InvalidValue, "clé d'événement attendue."); }
                    else if (!IsEventKey(key)) report.Add(path, ValidationCode.InvalidValue, $"clé d'événement « {key} » invalide : segments [a-z0-9_.] séparés par « / ».");
                    else if (!seen.Add(key)) report.Add(path, ValidationCode.DuplicateId, $"événement « {key} » déjà consommé plus haut : une seule attribution.");
                    else if (i > 0 && data.Consumed[i - 1] is string previous && string.CompareOrdinal(previous, key) > 0)
                        report.Add(path, ValidationCode.InvalidValue, "événements consommés hors de l'ordre ordinal (forme canonique).");
                }
            }
            if (!report.IsValid || document.ContentVersion is null) return new SaveLoadResult(report, null);
            return new SaveLoadResult(report, new WorldSave(data.Zone!, data.WorldTick!.Value, data.Sessions!.Value, data.Consumed!.Select(k => k!)));
        }

        private static void Missing(ValidationReport report, string path, string what)
        {
            if (!report.HasErrorAtOrAbove(path)) report.Add(path, ValidationCode.MissingField, $"champ obligatoire absent ({what}).");
        }
    }
}
