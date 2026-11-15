using System;
using EdS.Data.Seasons;
using EdS.Data.Story;
using EdS.Domain.Numerics;
using EdS.Domain.Time;

namespace EdS.World
{
    /// <summary>
    /// Saisons (décisions de Damien du 14 et du 15 novembre 2026) : la saison du monde vient de la date réelle, fournie par l'hôte ou le
    /// serveur (<see cref="SetCivilTime"/>, instant UTC ramené à l'heure de Paris), jamais d'une horloge lue par le cœur. Sans date fournie,
    /// la saison reste celle du calendrier du jeu (<see cref="WorldCalendar"/>, comme avant) et rien de saisonnier n'est ouvert : nœuds de
    /// saison fermés, passage du lac gelé fermé, île verrouillée (option prudente). La neige et l'hiver blanc restent un effet visuel : aucune
    /// règle de combat ne lit la saison.
    /// </summary>
    public sealed partial class WorldSession
    {
        private SeasonBook? _seasons;
        private SeasonState? _civilSeason;

        /// <summary>Contenu saisonnier en jeu (null : aucun ; tout se récolte toute l'année, aucun passage saisonnier).</summary>
        public SeasonBook? Seasons => _seasons;

        /// <summary>Instant UTC (secondes) fourni en dernier par l'hôte ou le serveur, ou null.</summary>
        public long? CivilUtcSeconds { get; private set; }

        /// <summary>Saison réelle (heure de Paris) au dernier instant fourni, ou null si l'hôte n'a pas fourni de date.</summary>
        public SeasonState? CivilSeason => _civilSeason;

        /// <summary>Met en jeu le contenu saisonnier validé (null : aucun).</summary>
        public void UseSeasons(SeasonBook? book) => _seasons = book;

        /// <summary>
        /// L'hôte ou le serveur fournit l'instant UTC <paramref name="unixSeconds"/> (secondes depuis le 1er janvier 1970) ; le serveur fait
        /// foi. Instant hors bornes : refusé, rien ne change.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Instant hors de [<see cref="ParisTime.MinUnixSeconds"/>, <see cref="ParisTime.MaxUnixSeconds"/>].</exception>
        public void SetCivilTime(long unixSeconds)
        {
            SeasonState state = SeasonCalendar.AtUtc(unixSeconds);   // lève avant toute mutation
            _civilSeason = state;
            CivilUtcSeconds = unixSeconds;
        }

        /// <summary>Rang de la saison du monde (0 : printemps … 3 : hiver) : la saison réelle si une date est fournie, sinon celle du calendrier du jeu.</summary>
        public int SeasonIndex => _civilSeason is SeasonState s ? (int)s.Season : WorldCalendar.SeasonIndexOf(Zone.Time.Day);

        /// <summary>Le nœud de récolte <paramref name="node"/> se récolte en cette saison (nœud de toute l'année : toujours).</summary>
        public bool NodeInSeason(string node) => _seasons is null || _seasons.NodeInSeason(node, _civilSeason?.Season);

        /// <summary>Le passage saisonnier <paramref name="passage"/> est ouvert (le lac gelé : glace praticable).</summary>
        /// <exception cref="InvalidOperationException">Aucun contenu saisonnier.</exception>
        public bool PassageOpen(string passage) => Need().Passage(passage).IsOpen(_civilSeason?.Ice);

        /// <summary>Le verrou saisonnier <paramref name="id"/> (l'île secrète) est ouvert : son passage l'est.</summary>
        public bool LockOpen(string id) => Need().LockOpen(id, _civilSeason?.Ice);

        /// <summary>
        /// Taux de présence d'un Écho dans la brume en cette saison (borné par le validateur), ou null sans contenu saisonnier ni date
        /// fournie. Le tirage, par volume de brume et au plus <see cref="SeasonBook.MaxEchoesPerVolume"/> Échos, revient à qui fait apparaître
        /// les Échos de la brume.
        /// </summary>
        public Fixed? MistEchoPresence => _seasons != null && _civilSeason is SeasonState s ? _seasons.MistEchoPresence(s.Season) : (Fixed?)null;

        private SeasonBook Need() => _seasons ?? throw new InvalidOperationException("Partie sans contenu saisonnier.");
    }
}
