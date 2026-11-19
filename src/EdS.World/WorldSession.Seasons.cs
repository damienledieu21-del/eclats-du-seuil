using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Crafting;
using EdS.Data.Seasons;
using EdS.Data.Zones;
using EdS.Domain.Space;
using EdS.Data.Story;
using EdS.Domain.Numerics;
using EdS.Domain.Time;

namespace EdS.World
{
    /// <summary>
    /// Saisons (décisions de Damien du 14 et du 15 novembre 2026) : la saison du monde vient de la date réelle, fournie par l'hôte ou le
    /// serveur (<see cref="SetCivilTime"/>, instant UTC ramené à l'heure de Paris), jamais d'une horloge lue par le cœur. Sans date fournie,
    /// la saison reste celle du calendrier du jeu (<see cref="WorldCalendar"/>, comme avant) et rien de saisonnier n'est ouvert : nœuds de
    /// saison fermés, passage du lac gelé fermé, île verrouillée (option prudente), contenu saisonnier en jeu ou non. La neige et l'hiver blanc restent un effet visuel : aucune
    /// règle de combat ne lit la saison.
    /// </summary>
    public sealed partial class WorldSession
    {
        private SeasonBook? _seasons;
        private SeasonState? _civilSeason;

        /// <summary>Contenu saisonnier en jeu (null : aucun ; aucun passage saisonnier ouvert).</summary>
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
            var before = Definition is ZoneDefinition zone ? SeasonalPassageKeys(zone).ToList() : null;
            _civilSeason = state;
            CivilUtcSeconds = unixSeconds;
            _civilTick = Zone.WorldTick;
            // Revue Codex de la PR #206 : gel ou fonte en direct : les sessions ouvertes que touche un passage saisonnier qui change sont
            // renouvelées au prochain tick avec l'aire de la saison nouvelle ; à la fonte, un joueur sur la glace va sur la rive la plus proche.
            if (_host != null && before != null && Definition is ZoneDefinition z)
            {
                var after = SeasonalPassageKeys(z).ToList();
                var changed = z.Passages.Where(p => before.Contains(z.PassageKey(p.Id)) != after.Contains(z.PassageKey(p.Id))).Select(p => p.Obstacle).ToList();
                if (changed.Count > 0) _seasonChanged = (_seasonChanged ?? new List<string>()).Concat(changed).Distinct(StringComparer.Ordinal).ToList();
            }
        }

        /// <summary>
        /// Revue Codex de la PR #206 : position libre la plus proche quand un passage saisonnier (une bande, comme la glace du lac) redevient
        /// un obstacle sous un joueur : le long du grand axe de la bande, à son bout le plus proche, 1 m au-delà de son rayon ; ailleurs, la
        /// position ne change pas. Déterministe (virgule fixe, ordre de la zone).
        /// </summary>
        internal static GroundVector OffSeasonalStrip(ZoneDefinition zone, SeasonBook? seasons, GroundVector at, Fixed radius)
        {
            if (seasons is null) return at;
            foreach (Passage p in zone.Passages)
            {
                if (!seasons.Passages.Any(s => s.Id == p.Id)) continue;   // seulement les passages saisonniers
                if (!(zone.Setup.Space?.Obstacles.FirstOrDefault(o => o.Id == p.Obstacle) is SpaceObstacle strip)) continue;
                Fixed minX = strip.Points.Min(q => q.X), maxX = strip.Points.Max(q => q.X), minZ = strip.Points.Min(q => q.Z), maxZ = strip.Points.Max(q => q.Z);
                Fixed m = radius + Fixed.FromInt(1);
                if (at.X < minX - m || at.X > maxX + m || at.Z < minZ - m || at.Z > maxZ + m) continue;
                bool alongZ = maxZ - minZ >= maxX - minX;
                if (alongZ)
                {
                    Fixed z = at.Z - minZ <= maxZ - at.Z ? minZ - m : maxZ + m;
                    Fixed x = at.X < minX + radius ? minX + radius : at.X > maxX - radius ? maxX - radius : at.X;
                    return new GroundVector(x, z);
                }
                Fixed nx = at.X - minX <= maxX - at.X ? minX - m : maxX + m;
                Fixed nz = at.Z < minZ + radius ? minZ + radius : at.Z > maxZ - radius ? maxZ - radius : at.Z;
                return new GroundVector(nx, nz);
            }
            return at;
        }

        private long _civilTick;

        /// <summary>Revue Codex de la PR #206 : obstacles des passages saisonniers changés par la date, à renouveler au prochain tick joué
        /// (après le premier tick de la session : la session de départ a d'abord reçu le personnage).</summary>
        private List<string>? _seasonChanged;

        private void RenewSeasonalSessions()
        {
            if (_seasonChanged is null || _host is null || Zone.Engine.CurrentTick == 0) return;
            _host.RenewForSeason(_seasonChanged);
            _seasonChanged = null;
        }

        /// <summary>
        /// Fêtes aux vraies dates : jour réel (heure de Paris) du jour du monde <paramref name="day"/>, pris à midi du monde, projeté depuis le
        /// dernier instant fourni par l'hôte et le tick du monde où il l'a été (une journée du monde dure <see cref="WorldClock.DayLengthMs"/>
        /// de jeu) ; null sans date fournie ou hors du calendrier.
        /// </summary>
        public CivilDate? CivilDateOfDay(long day)
        {
            if (!(CivilUtcSeconds is long utc) || day < 0) return null;
            long noon = WorldEventSchedule.TickAt(Zone.Clock, checked(day * WorldClock.MinutesPerDay + WorldClock.MinutesPerDay / 2));
            long ms = checked((noon - _civilTick) * Zone.Clock.TickMilliseconds);
            long seconds = checked(utc + (ms >= 0 ? ms / 1000 : -((-ms + 999) / 1000)));
            if (seconds < ParisTime.MinUnixSeconds || seconds > ParisTime.MaxUnixSeconds) return null;
            return ParisTime.ToLocal(seconds).Date;
        }

        /// <summary>Rang de la saison du monde (0 : printemps … 3 : hiver) : la saison réelle si une date est fournie, sinon celle du calendrier du jeu.</summary>
        public int SeasonIndex => _civilSeason is SeasonState s ? (int)s.Season : WorldCalendar.SeasonIndexOf(Zone.Time.Day);

        /// <summary>
        /// Le nœud de récolte <paramref name="node"/> se récolte en cette saison : un nœud de toute l'année toujours ; un nœud de saison (métiers,
        /// version 4) seulement pendant ses saisons, et jamais sans date fournie (option prudente). Nœud inconnu : vrai (la récolte le refuse).
        /// </summary>
        public bool NodeInSeason(string node) =>
            !(Workshop?.Book.Nodes.FirstOrDefault(n => n.Id == node) is GatheringNode g) || g.InSeason(_civilSeason?.Season);

        /// <summary>
        /// Le passage saisonnier <paramref name="passage"/> est ouvert (le lac gelé : glace praticable). Sans contenu saisonnier ou sans date
        /// fournie : fermé (option prudente).
        /// </summary>
        /// <exception cref="ArgumentException">Passage inconnu du contenu saisonnier.</exception>
        public bool PassageOpen(string passage) => _seasons != null && _seasons.Passage(passage).IsOpen(_civilSeason?.Ice);

        /// <summary>Le verrou saisonnier <paramref name="id"/> (l'île secrète) est ouvert : son passage l'est (sans contenu saisonnier : fermé).</summary>
        public bool LockOpen(string id) => _seasons != null && _seasons.LockOpen(id, _civilSeason?.Ice);

        /// <summary>
        /// Taux de présence d'un Écho dans la brume en cette saison (borné par le validateur), ou null sans contenu saisonnier ni date
        /// fournie. Le tirage, par volume de brume et au plus <see cref="SeasonBook.MaxEchoesPerVolume"/> Échos, revient à qui fait apparaître
        /// les Échos de la brume.
        /// </summary>
        public Fixed? MistEchoPresence => _seasons != null && _civilSeason is SeasonState s ? _seasons.MistEchoPresence(s.Season) : (Fixed?)null;
    }
}
