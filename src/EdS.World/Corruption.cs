using System;
using System.Collections.Generic;
using EdS.Data.Corruption;
using EdS.Data.Saves;
using EdS.Data.Zones;
using EdS.Domain.Numerics;
using EdS.Domain.Space;

namespace EdS.World
{
    /// <summary>
    /// Sous-lot V2.42.1 : règles de la corruption qui s'étend et recule, recalculées du temps du monde (jours) et des autels purifiés, sans
    /// horloge réelle ni autre état. La source est corrompue depuis le jour 0 ; une terre l'est <see cref="CorruptionBook.SpreadDays"/> jours
    /// après la plus tôt corrompue de ses terres d'origine, et pas avant son tour (la n-ième terre du fichier : jour n × spreadDays), mais jamais avant la fin du répit de son autel (jour de sa dernière purification
    /// plus <see cref="CorruptionBook.RespiteDays"/>). La corruption est <b>tenue par la source</b> : une terre coupée de toutes ses
    /// origines par des autels purifiés recule elle aussi, et revient quand le froid la rejoint de nouveau (recommandation de Claude,
    /// question posée à Damien dans la PR).
    /// </summary>
    public static class CorruptionRules
    {
        /// <summary>
        /// Jour du monde où chaque terre de <paramref name="book"/> (dans l'ordre du fichier) est corrompue, d'après les autels purifiés de
        /// <paramref name="saved"/> (une terre absente du catalogue n'y compte pas). Un seul passage : chaque terre vient après ses origines.
        /// </summary>
        public static long[] CorruptedSince(CorruptionBook book, SavedCorruption saved)
        {
            if (book is null) throw new ArgumentNullException(nameof(book));
            if (saved is null) throw new ArgumentNullException(nameof(saved));
            // Sous-lot V2.42.3 : après une purification de la source, la corruption repart du cercle à la fin du répit de la région, une terre
            // tous les spreadDays jours comme au début (sans purification de la source : jour 0, exactement comme avant).
            long start = SourceSince(book, saved);
            var since = new Dictionary<string, long>(StringComparer.Ordinal) { [book.Source.Id] = start };
            var result = new long[book.Areas.Count];
            for (int i = 0; i < book.Areas.Count; i++)
            {
                CorruptionArea a = book.Areas[i];
                long feed = long.MaxValue;
                foreach (string f in a.From) feed = Math.Min(feed, since[f]);
                // Revue de la PR #242 (décision de Damien : « une terre gagnée tous les 2 jours ») : la n-ième terre du fichier (n ≥ 1)
                // n'est jamais gagnée avant le jour n × spreadDays : sans purification, une seule terre de plus tous les spreadDays jours,
                // dans l'ordre du fichier.
                long c = Math.Max(checked(feed + book.SpreadDays), checked(start + (long)(i + 1) * book.SpreadDays));
                if (saved.DayOf(a.Id) is long purified) c = Math.Max(c, checked(purified + book.RespiteDays));
                result[i] = c;
                since[a.Id] = c;
            }
            return result;
        }

        /// <summary>
        /// Sous-lot V2.42.3 : jour du monde où la source est corrompue : 0, ou, après la défaite de son gardien (jour gardé sous l'identifiant
        /// de la source, sauvegarde v33), ce jour plus <see cref="CorruptionChallenge.RespiteDays"/> (répit de toute la région). Sans défi dans
        /// le catalogue, un jour gardé pour la source est sans effet.
        /// </summary>
        public static long SourceSince(CorruptionBook book, SavedCorruption saved)
        {
            if (book is null) throw new ArgumentNullException(nameof(book));
            if (saved is null) throw new ArgumentNullException(nameof(saved));
            if (book.Challenge is null || !(saved.DayOf(book.Source.Id) is long day)) return 0;
            return checked(day + book.Challenge.RespiteDays);
        }

        /// <summary>Sous-lot V2.42.3 : la région est-elle dans le répit d'une purification de la source au jour <paramref name="day"/> ?</summary>
        public static bool SourceResting(CorruptionBook book, SavedCorruption saved, long day) => day < SourceSince(book, saved);

        /// <summary>
        /// Sous-lot V2.42.3 : autels dans leur répit au jour <paramref name="day"/> (purifiés depuis moins de <see cref="CorruptionBook.RespiteDays"/>
        /// jours), terres du catalogue seulement.
        /// </summary>
        public static int AltarsResting(CorruptionBook book, SavedCorruption saved, long day)
        {
            if (book is null) throw new ArgumentNullException(nameof(book));
            if (saved is null) throw new ArgumentNullException(nameof(saved));
            int n = 0;
            foreach (CorruptionArea a in book.Areas)
                if (saved.DayOf(a.Id) is long d && d <= day && day < checked(d + book.RespiteDays)) n++;
            return n;
        }

        /// <summary>Palier d'une terre corrompue depuis le jour <paramref name="since"/>, au jour <paramref name="day"/> : 0 avant (propre), puis 1, plus un tous les <see cref="CorruptionBook.GrowDays"/> jours, au plus le maximal.</summary>
        public static int StageAt(long since, CorruptionBook book, long day)
        {
            if (book is null) throw new ArgumentNullException(nameof(book));
            if (day < since) return 0;
            return (int)Math.Min(book.MaxStage, checked(1 + (day - since) / book.GrowDays));
        }

        /// <summary>
        /// Décision de Damien du 21 novembre 2026 : force des créatures au point <paramref name="point"/> le jour <paramref name="day"/>, en
        /// pourcentage : <c>100 + strengthPercent × palier</c> de la terre la plus corrompue qui le contient (la source comprise ; bord du
        /// disque compris) ; 100 hors de toute terre corrompue. Calcul vérifié.
        /// </summary>
        public static long StrengthAt(CorruptionBook book, long[] since, long day, GroundVector point) => StrengthAt(book, since, day, point, 0);

        /// <summary>Sous-lot V2.42.3 : la même, la source corrompue depuis le jour <paramref name="sourceSince"/> (propre avant : répit de la région).</summary>
        public static long StrengthAt(CorruptionBook book, long[] since, long day, GroundVector point, long sourceSince)
        {
            if (book is null) throw new ArgumentNullException(nameof(book));
            if (since is null || since.Length != book.Areas.Count) throw new ArgumentException("Un jour par terre attendu.", nameof(since));
            int stage = day < sourceSince || CorruptionValidator.Beyond(point, book.Source.Position, book.Source.Radius) ? 0 : book.MaxStage;
            for (int i = 0; i < book.Areas.Count && stage < book.MaxStage; i++)
                if (!CorruptionValidator.Beyond(point, book.Areas[i].Position, book.Areas[i].Radius)) stage = Math.Max(stage, StageAt(since[i], book, day));
            return checked(100 + (long)book.StrengthPercent * stage);
        }

        /// <summary>
        /// Décision de Damien du 21 novembre 2026 : <paramref name="zone"/> (la zone de la corruption) avec ses créatures renforcées selon le
        /// palier de leur terre au jour <paramref name="day"/> (<see cref="ZoneDefinition.Strengthened"/>) ; une autre zone telle quelle. Pris
        /// à l'ouverture d'une partie : une créature garde la force du début de la partie jusqu'à la suivante.
        /// </summary>
        public static ZoneDefinition Strengthen(ZoneDefinition zone, CorruptionBook? book, SavedCorruption saved, long day)
        {
            if (zone is null) throw new ArgumentNullException(nameof(zone));
            // V2.51 (retenu par Damien le 26 novembre 2026) : l'Envers d'une zone corrompue reflète sa corruption, au même point.
            if (book is null || (zone.Id != book.ZoneId && zone.Mirror?.Of != book.ZoneId) || book.StrengthPercent == 0) return zone;
            long[] since = CorruptedSince(book, saved);
            long source = SourceSince(book, saved);
            return zone.Strengthened(p => StrengthAt(book, since, day, p, source));
        }

        /// <summary>Jour du monde où la terre change de palier après <paramref name="day"/> (gagnée ou plus forte), ou null au palier maximal.</summary>
        public static long? NextChangeDay(long since, CorruptionBook book, long day)
        {
            if (day < since) return since;
            int stage = StageAt(since, book, day);
            if (stage >= book.MaxStage) return null;
            return checked(since + (long)stage * book.GrowDays);
        }
    }

    /// <summary>Sous-lot V2.42.1 : une terre (ou la source) et son état au jour du monde, pour l'affichage et les textes.</summary>
    public sealed class CorruptionView
    {
        public CorruptionArea Area { get; }
        public bool IsSource { get; }

        /// <summary>Palier d'aujourd'hui : 0 (propre) à <see cref="MaxStage"/> ; la source est toujours au maximal.</summary>
        public int Stage { get; }
        public int MaxStage { get; }

        /// <summary>Jour du monde où la corruption y est (ou y sera) arrivée.</summary>
        public long Since { get; }

        /// <summary>Jour de la dernière purification de son autel, ou null.</summary>
        public long? PurifiedDay { get; }

        /// <summary>Jour du prochain changement de palier, ou null (maximal, ou la source).</summary>
        public long? NextChangeDay { get; }

        public bool IsCorrupted => Stage > 0;
        public GroundVector Position => Area.Position;
        public Fixed Radius => Area.Radius;
        public GroundVector Altar => Area.Altar;

        internal CorruptionView(CorruptionArea area, bool source, int stage, int maxStage, long since, long? purified, long? next)
        {
            Area = area;
            IsSource = source;
            Stage = stage;
            MaxStage = maxStage;
            Since = since;
            PurifiedDay = purified;
            NextChangeDay = next;
        }
    }

    /// <summary>Sous-lot V2.42.1 : ce qui arrive à une terre (annoncé au tick où on le voit).</summary>
    public enum CorruptionNoticeKind
    {
        /// <summary>La corruption gagne une terre propre.</summary>
        Spread = 1,
        /// <summary>Une terre corrompue gagne un palier.</summary>
        Grew = 2,
        /// <summary>Une terre recule (palier plus bas, ou propre) sans qu'on y ait purifié l'autel : la source ne la nourrit plus aussi tôt.</summary>
        Receded = 3,
        /// <summary>L'autel d'une terre est purifié (<see cref="CorruptionNotice.Xp"/> : l'expérience gagnée).</summary>
        Purified = 4,

        /// <summary>Sous-lot V2.42.3 : le défi de la source commence (le gardien se lève ; <see cref="CorruptionNotice.Area"/> : la source).</summary>
        ChallengeStarted = 5,

        /// <summary>
        /// Sous-lot V2.42.3 : le gardien est vaincu, la source purifiée : toute la région respire (<see cref="CorruptionNotice.Stage"/> : jours
        /// de répit ; <see cref="CorruptionNotice.Xp"/> : l'expérience gagnée).
        /// </summary>
        SourcePurified = 6,

        /// <summary>Sous-lot V2.42.3 : le défi est perdu (personnage tombé, parti trop loin ou sorti de la session) : le gardien s'en va.</summary>
        ChallengeFailed = 7,

        /// <summary>
        /// Sous-lot V2.42.3 : le gardien est vaincu mais la source n'a pu être purifiée (sauvegarde pleine de terres que le catalogue n'a plus) :
        /// rien ne change.
        /// </summary>
        SourceBlocked = 8,
    }

    public sealed class CorruptionNotice
    {
        public CorruptionNoticeKind Kind { get; }
        public string Area { get; }

        /// <summary>Palier après le changement (avant, pour une purification).</summary>
        public int Stage { get; }
        public long Day { get; }
        public long Xp { get; }

        internal CorruptionNotice(CorruptionNoticeKind kind, string area, int stage, long day, long xp = 0)
        {
            Kind = kind;
            Area = area;
            Stage = stage;
            Day = day;
            Xp = xp;
        }
    }

    /// <summary>Sous-lot V2.42.3 : refus de <see cref="WorldSession.ChallengeSource"/> (refus atomique : rien ne change).</summary>
    public enum SourceChallengeRefusal
    {
        None = 0,
        /// <summary>Pas de défi de la source dans cette zone (catalogue sans défi, ou autre zone).</summary>
        NoChallenge = 1,
        /// <summary>Le personnage est tombé.</summary>
        Defeated = 2,
        /// <summary>Le personnage n'est pas dans le disque de la source, ou pas dans la session du secteur du gardien.</summary>
        TooFar = 3,
        /// <summary>Le défi est déjà en cours.</summary>
        Underway = 4,
        /// <summary>La région est encore dans le répit de la dernière purification de la source (rien à purifier).</summary>
        Resting = 5,
        /// <summary>Pas assez d'autels dans leur répit (<see cref="CorruptionChallenge.MinAltars"/>).</summary>
        TooFewAltars = 6,
        /// <summary>La sauvegarde garde déjà <see cref="SavedCorruption.MaxEntries"/> terres et pas la source.</summary>
        Full = 7,
        /// <summary>La simulation refuse l'arrivée du gardien (place prise, session pleine).</summary>
        NoRoom = 8,
        /// <summary>
        /// Revue Codex de la PR #252 (3e tour) : le registre du monde n'a pas la place des morts possibles de toutes les formes du gardien
        /// (en gardant celle des morts possibles d'un tick).
        /// </summary>
        RegistryFull = 9,
        /// <summary>
        /// Revue Codex de la PR #252 (4e tour) : la session n'a pas les capacités de toutes les formes du gardien (identités, réserve des
        /// quêtes comprise ; place libre ; combat complété de leurs arrivées : identités de la session, file d'événements, précontrôle).
        /// </summary>
        Capacity = 10,
    }

    /// <summary>Sous-lot V2.42.1 : refus de <see cref="WorldSession.PurifyAltar"/> (refus atomique : rien ne change).</summary>
    public enum CorruptionRefusal
    {
        None = 0,
        /// <summary>Aucune terre de cet identifiant dans la zone (ou pas de corruption dans cette zone ; la source ne se purifie pas).</summary>
        NoArea = 1,
        /// <summary>Le personnage n'est pas assez près de l'autel, ou n'est pas là.</summary>
        TooFar = 2,
        /// <summary>La terre n'est pas corrompue aujourd'hui (rien à purifier ; l'autel est peut-être encore dans son répit).</summary>
        NotCorrupted = 3,
        /// <summary>Pas d'objet de purification (un éclat de Seuil) disponible dans le sac.</summary>
        NoItem = 4,
        /// <summary>Le personnage est tombé.</summary>
        Defeated = 5,
        /// <summary>
        /// Revue Codex de la PR #242 : la sauvegarde garde déjà <see cref="SavedCorruption.MaxEntries"/> autels purifiés et pas celui-ci
        /// (des terres que le catalogue n'a plus) : rien ne change, l'éclat reste.
        /// </summary>
        Full = 6,
    }
}
