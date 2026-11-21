using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace EdS.Data.Saves
{
    /// <summary>Sous-lot V2.5 : issue de la dernière occurrence d'un événement du monde réglée pour un personnage.</summary>
    public enum EventOutcome
    {
        /// <summary>Aucune encore (le registre a seulement pris note du moment où le personnage a connu l'événement).</summary>
        None = 0,

        /// <summary>Réussie : le hameau défendu, la faille refermée.</summary>
        Won = 1,

        /// <summary>Manquée : personne n'est venu à temps.</summary>
        Lost = 2,
    }

    /// <summary>
    /// Sous-lot V2.5 (sauvegarde v13) : une ligne du <b>registre des événements</b> d'un personnage : pour un événement du monde
    /// (<c>zone.vallons/attaque_hameau</c>), les occurrences réussies et manquées, le jour du monde de la dernière réglée (chaque occurrence
    /// est réglée une seule fois : celles d'un jour plus ancien ou égal ne comptent plus) et son issue, et la récompense de la première
    /// réussite si elle attend encore une place dans le sac (<see cref="Owed"/>). La récompense de la première réussite est donnée une
    /// seule fois par personnage : quand <see cref="Won"/> passe de 0 à 1.
    /// </summary>
    public sealed class SavedEvent
    {
        public string Id { get; }
        public long Won { get; }
        public long Lost { get; }

        /// <summary>Jour du monde de la dernière occurrence réglée, -1 si aucune.</summary>
        public long Last { get; }

        public EventOutcome Outcome { get; }

        /// <summary>La récompense de la première réussite attend une place dans le sac.</summary>
        public bool Owed { get; }

        /// <summary>
        /// Revue Codex de la PR #240 (sauvegarde v30) : jour du monde de la dernière occurrence <b>manquée</b> (pillage d'une caravane,
        /// hameau pillé), -1 si inconnu. Gardé quand une occurrence plus récente est réussie ou passe, pour que le contrecoup des marchands
        /// dure ses jours. Une ligne lue d'une sauvegarde antérieure n'en a pas (-1) : le contrecoup suit alors la dernière issue.
        /// </summary>
        public long LostDay { get; }

        /// <summary>
        /// Sous-lot V2.41.3 (sauvegarde v31) : coffres repris au fort après le pillage du jour <see cref="LostDay"/> (masque : bit k pour le
        /// coffre k de la quête de reprise), 0 si aucun. Remis à 0 à chaque nouveau pillage. Chaque coffre repris enlève un jour au contrecoup.
        /// </summary>
        public int Recovered { get; }

        /// <summary>
        /// Sous-lot V2.41.3 (sauvegarde v31 ; décision de Damien du 21 novembre 2026, anti-triche) : une reprise complète a déjà été faite :
        /// les suivantes ne donnent plus que la prime (sans expérience). Gardé d'un pillage à l'autre.
        /// </summary>
        public bool RecoveredOnce { get; }

        /// <summary>Masque des coffres repris, au plus (huit coffres).</summary>
        public const int MaxRecovered = 255;

        /// <summary>Compteurs et jours, au plus (bornes techniques).</summary>
        public const long MaxCount = 1_000_000, MaxDay = 100_000_000;

        /// <summary>Lignes du registre, au plus.</summary>
        public const int MaxEntries = 64;

        /// <summary>
        /// Revue Codex de la PR #110 (tour 3) : longueur au plus d'une clé du registre ou d'une cicatrice dans la sauvegarde (une seule borne,
        /// celle du lecteur et de l'écrivain ; <c>ZoneValidator</c> vérifie que les clés composées des zones la respectent).
        /// </summary>
        public const int MaxKeyLength = 128;

        public static readonly IReadOnlyList<string> OutcomeNames = Array.AsReadOnly(new[] { "none", "won", "lost" });

        public SavedEvent(string id, long won, long lost, long last, EventOutcome outcome, bool owed = false, long lostDay = -1, int recovered = 0, bool recoveredOnce = false)
        {
            if (!SaveValidator.IsEventKey(id) || id.Length > MaxKeyLength) throw new ArgumentException($"Événement « {id} » hors du format des sauvegardes.", nameof(id));
            if (won < 0 || won > MaxCount) throw new ArgumentOutOfRangeException(nameof(won));
            if (lost < 0 || lost > MaxCount) throw new ArgumentOutOfRangeException(nameof(lost));
            if (last < -1 || last > MaxDay) throw new ArgumentOutOfRangeException(nameof(last));
            if (outcome != EventOutcome.None && last < 0) throw new ArgumentException("Une issue sans jour.", nameof(outcome));
            if (owed && won < 1) throw new ArgumentException("Une récompense due sans réussite.", nameof(owed));
            if (lostDay < -1 || lostDay > last) throw new ArgumentOutOfRangeException(nameof(lostDay), "Jour d'un échec entre -1 et le dernier jour réglé.");
            if (lostDay >= 0 && lost < 1) throw new ArgumentException("Un jour d'échec sans échec.", nameof(lostDay));
            if (recovered < 0 || recovered > MaxRecovered) throw new ArgumentOutOfRangeException(nameof(recovered));
            if (recovered > 0 && lostDay < 0) throw new ArgumentException("Des coffres repris sans jour de pillage.", nameof(recovered));
            if (recoveredOnce && lost < 1) throw new ArgumentException("Une reprise sans pillage.", nameof(recoveredOnce));
            Recovered = recovered;
            RecoveredOnce = recoveredOnce;
            Id = id;
            Won = won;
            Lost = lost;
            Last = last;
            Outcome = outcome;
            Owed = owed;
            LostDay = lostDay;
        }

        /// <summary>Lignes rangées par identifiant (forme canonique), une seule par identifiant.</summary>
        public static IReadOnlyList<SavedEvent> Canonical(IEnumerable<SavedEvent>? events)
        {
            var list = (events ?? Array.Empty<SavedEvent>()).ToList();
            if (list.Any(e => e is null)) throw new ArgumentNullException(nameof(events));
            if (list.Count > MaxEntries) throw new ArgumentException($"{list.Count} lignes du registre des événements, au plus {MaxEntries}.", nameof(events));
            if (list.Select(e => e.Id).Distinct(StringComparer.Ordinal).Count() != list.Count) throw new ArgumentException("Événement en double au registre.", nameof(events));
            return new ReadOnlyCollection<SavedEvent>(list.OrderBy(e => e.Id, StringComparer.Ordinal).ToList());
        }
    }

    /// <summary>Sous-lot V2.5 (version 13) : une ligne du registre des événements telle que lue, avant validation.</summary>
    public sealed class SavedEventData
    {
        public string? Id { get; set; }
        public long? Won { get; set; }
        public long? Lost { get; set; }
        public long? Last { get; set; }
        public string? Outcome { get; set; }
        public bool? Owed { get; set; }

        /// <summary>Version 30 : jour de la dernière occurrence manquée (facultatif).</summary>
        public long? LostDay { get; set; }

        /// <summary>Version 31 : coffres repris après le dernier pillage (facultatif, masque de 1 à 255).</summary>
        public long? Recovered { get; set; }

        /// <summary>Version 31 : une reprise complète déjà faite (facultatif, écrit seulement vrai).</summary>
        public bool? RecoveredOnce { get; set; }
    }
}
