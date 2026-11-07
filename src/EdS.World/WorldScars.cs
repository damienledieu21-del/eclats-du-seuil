using System;
using System.Collections.Generic;
using EdS.Data.Saves;
using EdS.Data.Zones;
using EdS.Domain.Numerics;
using EdS.Domain.Space;

namespace EdS.World
{
    /// <summary>
    /// Sous-lot V2.6 (« le Seuil qui grandit ») : règles des cicatrices, recalculées du temps du monde (jours), sans horloge réelle ni état.
    /// </summary>
    public static class WorldScarRules
    {
        /// <summary>Clé d'une cicatrice dans la sauvegarde : <c>zone.vallons/faille_du_seuil/ruines</c>.</summary>
        public static string Key(string zone, string eventId, string group) => ZoneValidator.ScarKey(zone, eventId, group);

        /// <summary>
        /// Palier de la cicatrice <paramref name="scar"/> au jour du monde <paramref name="day"/> : son palier gardé, plus un tous les
        /// <see cref="WorldEventScar.GrowDays"/> jours depuis <see cref="SavedScar.Since"/>, au plus <see cref="WorldEventScar.MaxStage"/>
        /// (jamais moins de 1 ; un jour d'avant <c>Since</c> ne retire rien).
        /// </summary>
        public static int StageAt(SavedScar scar, WorldEventScar rule, long day)
        {
            if (scar is null) throw new ArgumentNullException(nameof(scar));
            if (rule is null) throw new ArgumentNullException(nameof(rule));
            long grown = Math.Max(0, checked(day - scar.Since)) / rule.GrowDays;
            return (int)Math.Max(1, Math.Min(rule.MaxStage, checked(scar.Stage + grown)));
        }

        /// <summary>
        /// Sous-lot V2.6.1 : jour du monde où la cicatrice <paramref name="scar"/> atteint (ou a atteint) le palier de propagation de
        /// <paramref name="rule"/> : <see cref="SavedScar.Since"/> si elle y est déjà, sinon <c>Since + (spreadStage − Stage) × growDays</c> ;
        /// null sans propagation.
        /// </summary>
        public static long? SpreadReachDay(SavedScar scar, WorldEventScar rule)
        {
            if (!rule.Spreads) return null;
            return scar.Stage >= rule.SpreadStage ? scar.Since : checked(scar.Since + (long)(rule.SpreadStage - scar.Stage) * rule.GrowDays);
        }

        /// <summary>
        /// Sous-lot V2.6.1 (relecture de la PR #112) : la cicatrice <paramref name="scar"/>, son délai de propagation reparti du jour
        /// <paramref name="day"/> (une propagation faite, ou le répit d'une purification), <b>sans toucher à sa croissance</b> : au palier
        /// maximal (palier figé), <c>Since</c> devient <paramref name="day"/> ; en dessous, <c>Since</c> n'avance que de paliers entiers
        /// (<c>k × growDays</c>, <c>Stage + k</c>), si bien que <see cref="StageAt"/> rend le même palier à tout jour à partir de
        /// <paramref name="day"/> ; le délai repart alors du dernier palier gagné (au plus <c>growDays − 1</c> jours plus tôt, jamais avant la
        /// propagation précédente). Le format de la sauvegarde ne change pas.
        /// </summary>
        public static SavedScar RestartSpread(SavedScar scar, WorldEventScar rule, long day)
        {
            if (scar is null) throw new ArgumentNullException(nameof(scar));
            if (rule is null) throw new ArgumentNullException(nameof(rule));
            if (day < scar.Since) return scar;
            int stage = StageAt(scar, rule, day);
            if (stage >= rule.MaxStage) return new SavedScar(scar.Id, rule.MaxStage, day, scar.Born);
            long k = checked(day - scar.Since) / rule.GrowDays;   // stage < MaxStage : Stage + k = stage, au plus 8
            return new SavedScar(scar.Id, stage, checked(scar.Since + k * rule.GrowDays), scar.Born);
        }

        /// <summary>Jour du monde où la cicatrice gagnera son prochain palier, ou null si elle est au palier maximal.</summary>
        public static long? NextStageDay(SavedScar scar, WorldEventScar rule, long day)
        {
            if (StageAt(scar, rule, day) >= rule.MaxStage) return null;
            long since = Math.Max(0, checked(day - scar.Since));
            return checked(scar.Since + (since / rule.GrowDays + 1) * rule.GrowDays);
        }
    }

    /// <summary>Sous-lot V2.6 : une cicatrice de la zone, pour l'affichage (couleurs fanées, brume froide, échos rôdeurs).</summary>
    public sealed class ScarView
    {
        /// <summary>Clé de la sauvegarde (<c>zone.vallons/faille_du_seuil/ruines</c>).</summary>
        public string Key { get; }

        public WorldEventDefinition Event { get; }
        public WorldEventGroup Group { get; }
        public GroundVector Position { get; }
        public int Stage { get; }
        public int MaxStage { get; }
        public Fixed Radius { get; }
        public long Born { get; }

        /// <summary>Jour du monde du prochain palier, ou null au palier maximal.</summary>
        public long? NextStageDay { get; }

        /// <summary>Échos rôdeurs de la cicatrice debout dans la zone.</summary>
        public IReadOnlyList<string> Haunts { get; }

        /// <summary>Une faille est ouverte à sa place (elle ne se purifie pas pendant ce temps).</summary>
        public bool RiftOpen { get; }

        internal ScarView(string key, WorldEventDefinition e, WorldEventGroup group, GroundVector position, int stage, Fixed radius, long born, long? next,
            IReadOnlyList<string> haunts, bool riftOpen)
        {
            Key = key;
            Event = e;
            Group = group;
            Position = position;
            Stage = stage;
            MaxStage = e.Scar!.MaxStage;
            Radius = radius;
            Born = born;
            NextStageDay = next;
            Haunts = haunts;
            RiftOpen = riftOpen;
        }
    }

    /// <summary>Sous-lot V2.6 : refus de <see cref="WorldSession.PurifyScar"/> (refus atomique : rien ne change).</summary>
    public enum ScarRefusal
    {
        None = 0,

        /// <summary>Aucune cicatrice de cette clé dans la zone.</summary>
        NoScar = 1,

        /// <summary>Le personnage n'est pas assez près du cœur de la cicatrice, ou n'est pas là.</summary>
        TooFar = 2,

        /// <summary>Une faille est ouverte à sa place : la refermer d'abord.</summary>
        RiftOpen = 3,

        /// <summary>Des échos rôdeurs de la cicatrice sont encore debout (ou vont arriver).</summary>
        HauntsRemain = 4,

        /// <summary>Pas d'objet de purification (un éclat de Seuil) disponible dans le sac.</summary>
        NoItem = 5,

        /// <summary>Le personnage est tombé.</summary>
        Defeated = 6,
    }
}
