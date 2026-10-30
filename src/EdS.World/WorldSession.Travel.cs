using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Zones;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;

namespace EdS.World
{
    /// <summary>Motif d'un refus de voyage rapide (rien n'a changé).</summary>
    public enum TravelRefusal
    {
        None = 0,

        /// <summary>En plein combat : un ennemi vivant à moins de <see cref="WorldSession.CombatRange"/>, ou un ennemi engagé qui vise le personnage.</summary>
        InCombat = 1,

        /// <summary>Destination inconnue : zone ou point sûr absent des données, ou intérieur (on y entre par sa porte).</summary>
        UnknownPlace = 2,

        /// <summary>Point sûr pas encore découvert.</summary>
        NotDiscovered = 3,

        /// <summary>Personnage absent ou tombé, dialogue ouvert, ou reprise à faire.</summary>
        Unavailable = 4,

        /// <summary>La sauvegarde du départ a échoué (disque plein) : on reste où l'on est.</summary>
        SaveFailed = 5,
    }

    /// <summary>
    /// Sous-lot V1.1 : points sûrs découverts et voyage rapide. Un point sûr d'une zone du dehors est découvert quand le personnage, présent
    /// et vivant, s'en approche à <see cref="DiscoverRange"/> (celui où commence une session l'est d'emblée) ; sa clé
    /// (<see cref="ZoneDefinition.SafePointKey"/>) entre au registre du monde, gardé dans la sauvegarde. Le voyage rapide mène à un point sûr
    /// découvert, hors combat (<see cref="InCombat"/>, la règle de la halte et des talents) ; il reprend le chemin d'une porte (sauvegarde,
    /// puis nouvelle session au point d'arrivée : <c>AdventureSession.Travel</c>). Gratuit dans la vague 1 (le prix éventuel sera un réglage).
    /// </summary>
    public sealed partial class WorldSession
    {
        /// <summary>Distance d'un point sûr qui le fait découvrir (provisoire : celle de la halte).</summary>
        public static readonly Fixed DiscoverRange = Fixed.FromInt(5);

        /// <summary>Pas de voyage avec un ennemi vivant à moins de cette distance (règle de la halte et des talents, provisoire).</summary>
        public static readonly Fixed CombatRange = Fixed.FromInt(12);

        /// <summary>Le point sûr <paramref name="safePoint"/> de <paramref name="zone"/> est découvert (clé au registre du monde).</summary>
        public bool IsDiscovered(ZoneDefinition zone, string safePoint) =>
            Zone.IsConsumed((zone ?? throw new ArgumentNullException(nameof(zone))).SafePointKey(safePoint ?? throw new ArgumentNullException(nameof(safePoint))));

        /// <summary>
        /// Inscrit la découverte d'un point sûr de la zone de la partie (une zone du dehors) ; faux s'il l'était déjà, si la zone est un
        /// intérieur, ou si le registre n'a plus la place des morts possibles d'un tick en plus de cette clé (la découverte attend).
        /// </summary>
        private bool Discover(string safePoint)
        {
            if (Definition is null || Definition.Indoor) return false;
            string key = Definition.SafePointKey(safePoint);
            if (Zone.IsConsumed(key) || Zone.RegistryRoom <= Zone.Engine.CombatantIds.Count) return false;
            return Zone.Consume(key);
        }

        /// <summary>Points sûrs approchés à ce tick (personnage présent et vivant), découverts pour de bon.</summary>
        private void DiscoverSafePoints(List<StoryEvent> events)
        {
            if (Definition is null || Definition.Indoor) return;
            CombatantView me = Zone.Engine.View(_character.Id);
            if (!me.IsPresent || me.IsDead || !(me.Position is GroundVector p)) return;
            foreach (SafePoint s in Definition.SafePoints)
                if (!ZonePopulations.Beyond(p, Fixed.Zero, s.Position, Fixed.Zero, DiscoverRange) && Discover(s.Id))
                    events.Add(new StoryEvent(StoryEventKind.SafePointDiscovered, s.Id));
        }

        /// <summary>
        /// Le personnage est en plein combat : absent ou tombé, un ennemi vivant et présent à moins de <see cref="CombatRange"/> (écart entre
        /// les centres), ou un ennemi engagé qui le vise, où qu'il soit (un archer qui tire de loin).
        /// </summary>
        public bool InCombat
        {
            get
            {
                CombatantView me = Zone.Engine.View(_character.Id);
                if (!me.IsPresent || me.IsDead || !(me.Position is GroundVector p)) return true;
                long limit = checked(CombatRange.Raw * CombatRange.Raw);
                foreach (string id in Zone.Engine.CombatantIds)
                {
                    if (id == _character.Id) continue;
                    CombatantView v = Zone.Engine.View(id);
                    if (v.Team == me.Team || !v.IsPresent || v.IsDead) continue;
                    if (v.Engaged && v.CurrentTarget == _character.Id) return true;
                    if (v.Position is GroundVector q)
                    {
                        long dx = checked(q.X.Raw - p.X.Raw), dz = checked(q.Z.Raw - p.Z.Raw);
                        if (checked(dx * dx + dz * dz) <= limit) return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Le voyage rapide vers le point sûr <paramref name="safePoint"/> de <paramref name="zone"/> est possible maintenant : zone du dehors
        /// et point sûr connus, découvert, personnage présent et vivant (sinon <see cref="TravelRefusal.Unavailable"/>), hors combat.
        /// </summary>
        public TravelRefusal CanTravel(ZoneDefinition zone, string safePoint)
        {
            if (zone is null || zone.Indoor || safePoint is null || zone.SafePoints.All(s => s.Id != safePoint)) return TravelRefusal.UnknownPlace;
            if (!IsDiscovered(zone, safePoint)) return TravelRefusal.NotDiscovered;
            if (_defeated || !Zone.Engine.View(_character.Id).IsPresent || Zone.Engine.View(_character.Id).IsDead) return TravelRefusal.Unavailable;
            return InCombat ? TravelRefusal.InCombat : TravelRefusal.None;
        }
    }
}
