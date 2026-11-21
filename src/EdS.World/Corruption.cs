using System;
using System.Collections.Generic;
using EdS.Data.Corruption;
using EdS.Data.Saves;
using EdS.Domain.Numerics;
using EdS.Domain.Space;

namespace EdS.World
{
    /// <summary>
    /// Sous-lot V2.42.1 : règles de la corruption qui s'étend et recule, recalculées du temps du monde (jours) et des autels purifiés, sans
    /// horloge réelle ni autre état. La source est corrompue depuis le jour 0 ; une terre l'est <see cref="CorruptionBook.SpreadDays"/> jours
    /// après la plus tôt corrompue de ses terres d'origine, mais jamais avant la fin du répit de son autel (jour de sa dernière purification
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
            var since = new Dictionary<string, long>(StringComparer.Ordinal) { [book.Source.Id] = 0 };
            var result = new long[book.Areas.Count];
            for (int i = 0; i < book.Areas.Count; i++)
            {
                CorruptionArea a = book.Areas[i];
                long feed = long.MaxValue;
                foreach (string f in a.From) feed = Math.Min(feed, since[f]);
                long c = checked(feed + book.SpreadDays);
                if (saved.DayOf(a.Id) is long purified) c = Math.Max(c, checked(purified + book.RespiteDays));
                result[i] = c;
                since[a.Id] = c;
            }
            return result;
        }

        /// <summary>Palier d'une terre corrompue depuis le jour <paramref name="since"/>, au jour <paramref name="day"/> : 0 avant (propre), puis 1, plus un tous les <see cref="CorruptionBook.GrowDays"/> jours, au plus le maximal.</summary>
        public static int StageAt(long since, CorruptionBook book, long day)
        {
            if (book is null) throw new ArgumentNullException(nameof(book));
            if (day < since) return 0;
            return (int)Math.Min(book.MaxStage, checked(1 + (day - since) / book.GrowDays));
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
    }
}
