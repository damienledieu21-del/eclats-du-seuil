using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Story;
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

        /// <summary>
        /// Le personnage est déjà à ce point sûr (même zone, à <see cref="WorldSession.DiscoverRange"/>) : le voyage n'y mènerait nulle part
        /// et ne servirait qu'à rendre les PV (la halte est faite pour cela).
        /// </summary>
        AlreadyHere = 6,
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
        /// <summary>Sous-lot V2.7 : fait qui révèle le point sûr secret <paramref name="safePoint"/> de la zone de l'histoire, ou null.</summary>
        private string? SecretFact(string safePoint)
        {
            if (Narrative is null || !InStoryZone) return null;
            foreach ((string id, string fact) in Narrative.Book.Reputation.Hidden)
                if (id == safePoint) return fact;
            return null;
        }

        /// <summary>Sous-lot V2.7 : le point sûr <paramref name="safePoint"/> est secret et pas encore révélé (ni carte, ni repos, ni retour).</summary>
        public bool IsSecret(string safePoint) => SecretFact(safePoint) is string fact && !Narrative!.Knows(fact);

        /// <summary>
        /// Sous-lot V2.7 (réponse de Damien du 7 novembre 2026) : le couvre-feu tombe : la faction du couvre-feu hait le personnage, il est
        /// dans son lieu à l'un de ses moments, présent, vivant et hors combat ; le point sûr où il est reconduit, sinon null.
        /// </summary>
        public string? CurfewSafePoint
        {
            get
            {
                if (!(Narrative is Narrative n) || !(n.Book.Reputation.Curfew is ReputationCurfew c) || !InStoryZone || _defeated) return null;
                if (n.StandingIndex(c.Faction) != 0 || !c.Phases.Contains(Narrative.PhaseId(Zone.Time.Phase))) return null;
                if (!((INarrativeWorld)this).InPlace(c.Place) || InCombat) return null;
                return c.SafePoint;
            }
        }

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
            {
                // Sous-lot V2.7 : un point sûr secret (le passage des bandits) ne se découvre qu'avec le fait qui le révèle, où que l'on soit.
                if (SecretFact(s.Id) is string fact)
                {
                    if (Narrative != null && Narrative.Knows(fact) && Discover(s.Id)) events.Add(new StoryEvent(StoryEventKind.SafePointDiscovered, s.Id));
                    continue;
                }
                if (!ZonePopulations.Beyond(p, Fixed.Zero, s.Position, Fixed.Zero, DiscoverRange) && Discover(s.Id))
                    events.Add(new StoryEvent(StoryEventKind.SafePointDiscovered, s.Id));
            }
        }

        /// <summary>
        /// Le personnage est en plein combat : absent ou tombé, un ennemi vivant et présent à moins de <see cref="CombatRange"/> (écart entre
        /// les centres), ou un ennemi engagé qui le vise, où qu'il soit (un archer qui tire de loin).
        /// <para>Revue Codex de la PR #104 (tour 3) : une bête paisible qui fuit (cerf, lièvre) ne compte jamais ; un ennemi engagé qui le vise
        /// ne compte qu'à sa laisse (écart entre les bords, comme la laisse du monde) : au-delà, il n'est plus en combat avec lui (un loup de la
        /// brume engagé qui part à la levée, une bête restée loin). Une créature sans population (une forme de chef) compte où qu'elle soit,
        /// comme avant.</para>
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
                    if (v.Flees) continue;   // revue Codex de la PR #104 (tour 3) : un cerf ou un lièvre n'est pas un combat
                    if (v.Engaged && v.CurrentTarget == _character.Id && WithinLeash(v, me)) return true;
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
        /// Revue Codex de la PR #104 (tour 3) : <paramref name="creature"/> est à sa laisse du personnage (écart entre les bords) ; toujours
        /// vrai pour une créature sans population (forme de chef) ou sans place.
        /// </summary>
        private bool WithinLeash(CombatantView creature, CombatantView me)
        {
            if (!(_populations?.Of(creature.Id) is Population population)) return true;
            if (!(creature.Position is GroundVector a && creature.Radius is Fixed ra && me.Position is GroundVector b && me.Radius is Fixed rb)) return true;
            return !ZonePopulations.Beyond(a, ra, b, rb, population.LeashRange);
        }

        /// <summary>
        /// Le personnage, présent et vivant, est au point sûr <paramref name="safePoint"/> de <paramref name="zone"/> : zone de la partie, à
        /// <see cref="DiscoverRange"/> du point (écart entre les centres). Faux pour un point inconnu ou une autre zone.
        /// </summary>
        public bool IsAt(ZoneDefinition zone, string safePoint)
        {
            if (zone is null || Definition is null || zone.Id != Definition.Id) return false;
            SafePoint? s = Definition.SafePoints.FirstOrDefault(x => x.Id == safePoint);
            if (s is null) return false;
            CombatantView me = Zone.Engine.View(_character.Id);
            return me.IsPresent && !me.IsDead && me.Position is GroundVector p && !ZonePopulations.Beyond(p, Fixed.Zero, s.Position, Fixed.Zero, DiscoverRange);
        }

        /// <summary>
        /// Le voyage rapide vers le point sûr <paramref name="safePoint"/> de <paramref name="zone"/> est possible maintenant : zone du dehors
        /// et point sûr connus, découvert, personnage présent et vivant (sinon <see cref="TravelRefusal.Unavailable"/>), hors combat, pas déjà
        /// à ce point (<see cref="TravelRefusal.AlreadyHere"/>, relecture de la PR #72 : le voyage n'est pas une halte gratuite ; vient en
        /// dernier, car il ne concerne que cette destination).
        /// </summary>
        public TravelRefusal CanTravel(ZoneDefinition zone, string safePoint)
        {
            if (zone is null || zone.Indoor || safePoint is null || zone.SafePoints.All(s => s.Id != safePoint)) return TravelRefusal.UnknownPlace;
            if (!IsDiscovered(zone, safePoint)) return TravelRefusal.NotDiscovered;
            if (_defeated || !Zone.Engine.View(_character.Id).IsPresent || Zone.Engine.View(_character.Id).IsDead) return TravelRefusal.Unavailable;
            if (InCombat) return TravelRefusal.InCombat;
            return IsAt(zone, safePoint) ? TravelRefusal.AlreadyHere : TravelRefusal.None;
        }
    }
}
