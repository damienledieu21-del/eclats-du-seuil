using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Zones;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;

namespace EdS.World
{
    public enum PopulationEventKind
    {
        /// <summary>Une nouvelle vie de la créature est arrivée à sa place (réapparition, ou retour au calme après la laisse).</summary>
        Respawned = 0,

        /// <summary>La créature a lâché sa cible, trop distancée : elle quitte la zone et une vie calme et soignée la remplace à sa place.</summary>
        Leashed = 1,

        /// <summary>Plus de place pour une nouvelle vie dans cette session (plafond des combattants) : elle reviendra à la session suivante.</summary>
        Exhausted = 2,

        /// <summary>
        /// Réponses de Damien du 31 octobre 2026 : signe d'une créature rare de la nuit (« Un hurlement glacé monte du pré… ») ; elle arrive
        /// après le délai de son signe (<see cref="NightSpawn.SignMs"/>). <see cref="PopulationEvent.Creature"/> : la clé du texte du signe.
        /// </summary>
        Sign = 3,

        /// <summary>Réponses du 31 octobre 2026 : une créature rare de la nuit, au calme, s'en va (le jour se lève, la quête est finie).</summary>
        Withdrew = 4,
    }

    public sealed class PopulationEvent
    {
        public PopulationEventKind Kind { get; }

        /// <summary>Créature modèle (population).</summary>
        public string Population { get; }

        /// <summary>Vie concernée (combattant).</summary>
        public string Creature { get; }

        public long Tick { get; }

        internal PopulationEvent(PopulationEventKind kind, string population, string creature, long tick)
        {
            Kind = kind;
            Population = population;
            Creature = creature;
            Tick = tick;
        }
    }

    /// <summary>
    /// Sous-lot 12.4 (ARCHITECTURE_MONDE.md, section 2 : les identités ne sont jamais réutilisées) : vie des populations d'une session de
    /// zone. Chaque population suit la vie en cours de sa créature :
    /// <list type="bullet">
    /// <item><b>réapparition</b> : morte, elle revient après son délai, à sa place, sous une nouvelle identité (<c>loup1_v2</c>, <c>_v3</c>…),
    /// calme et soignée ; sa mort garde sa clé unique (butin et expérience une seule fois) ;</item>
    /// <item><b>laisse</b> (essai du 22 octobre 2026 : « il faut suffisamment s'éloigner pour que l'ennemi lâche ») : engagée, si l'écart entre
    /// les bords de son emprise et de sa cible dépasse sa laisse, elle quitte la zone (pas de mort : ni butin ni expérience) et une nouvelle
    /// vie calme et soignée la remplace à sa place au tick suivant ;</item>
    /// <item>une place occupée retarde l'arrivée d'une seconde ; au plafond des combattants de la session, la population attend la session
    /// suivante (<see cref="PopulationEventKind.Exhausted"/>).</item>
    /// </list>
    /// Tout se décide sur l'état du moteur, tick après tick : la même partie donne les mêmes vies.
    /// </summary>
    internal sealed class ZonePopulations
    {
        private sealed class Life
        {
            public Population Population = null!;
            public string Current = string.Empty;
            public int Number = 1;
            public long? RespawnAt;
            public bool Leashing;
            public bool Exhausted;

            /// <summary>Créature rare de la nuit absente de la zone (pas encore venue, partie ou morte).</summary>
            public bool Dormant;

            /// <summary>Créature de la nuit annoncée par son signe : tick de son arrivée.</summary>
            public long? ArriveAt;

            /// <summary>Créature de la nuit qui s'en va d'elle-même (pas une laisse) : elle ne revient pas aussitôt.</summary>
            public bool Withdrawing;

            /// <summary>Sous-lot V2.1 : créature à heures morte à ses heures : elle revient après son délai, sans annoncer son retour.</summary>
            public bool Slain;

            /// <summary>
            /// Sous-lot V2.9.1 (revue Codex de la PR #115) : la vie en cours est morte ; la suivante revient après une mort (un écho). Une
            /// première arrivée, une laisse ou un retour à ses heures ne passent pas par là.
            /// </summary>
            public bool Died;
        }

        // Sous-lot V2.9.1 : vies revenues après la mort de la précédente (affichage des échos ; pas sauvegardé : une session nouvelle repart
        // des créatures du scénario, sans vie suivante).
        private readonly HashSet<string> _afterDeath = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// Sous-lot V2.9.1 (revue Codex de la PR #115) : vrai si <paramref name="combatant"/> est une vie revenue après la mort de la précédente
        /// (pas une première arrivée, une laisse ni un retour à ses heures sans mort). Affichage seulement.
        /// </summary>
        public bool ReturnedFromDeath(string combatant) => combatant != null && _afterDeath.Contains(combatant);

        private readonly List<Life> _lives;
        private readonly Dictionary<string, Population> _byCreature = new Dictionary<string, Population>(StringComparer.Ordinal);

        /// <param name="phase">Sous-lot V2.1 : moment de la journée au début de la session (null : toutes les créatures à heures sont là).</param>
        /// <param name="weather">Sous-lot V2.4 : temps au début de la session (null : toutes les créatures à temps sont là).</param>
        public ZonePopulations(ZoneDefinition zone, DayPhase? phase = null, WeatherKind? weather = null)
        {
            // Réponses du 31 octobre 2026 : une créature rare de la nuit n'est pas là au début de la session (ZoneSetups la retire).
            // Sous-lot V2.1 : une créature à heures non plus, hors de ses heures.
            _lives = zone.Populations.Select(p => new Life { Population = p, Current = p.Creature, Dormant = !PresentAtStart(p, phase, weather) }).ToList();
            foreach (Population p in zone.Populations) _byCreature[p.Creature] = p;
        }

        /// <summary>
        /// La créature de <paramref name="population"/> est là au début d'une session qui commence au moment <paramref name="phase"/> : jamais
        /// une créature rare de la nuit ; une créature à heures seulement à ses heures (toujours si le moment n'est pas donné) ; les autres toujours.
        /// </summary>
        /// <remarks>Sous-lot V2.4 : une créature qui dépend du temps (<see cref="Population.Weather"/>) n'est là d'emblée que par son temps
        /// (<paramref name="weather"/>, le temps au début de la session ; null : toujours, comme un moment non donné).</remarks>
        internal static bool PresentAtStart(Population population, DayPhase? phase, WeatherKind? weather = null) =>
            population.Night is null
            && (population.Hours is not PopulationHours hours || phase is not DayPhase p || hours.IsActive((int)p))
            && (population.Weather is not PopulationWeather w || weather is not WeatherKind k || Fits(w, k));

        /// <summary>Sous-lot V2.4 : le temps <paramref name="kind"/> est-il l'un de ceux de <paramref name="weather"/> (« rain » : toute pluie) ?</summary>
        internal static bool Fits(PopulationWeather weather, WeatherKind kind)
        {
            foreach (string id in weather.Kinds)
                if (WorldWeather.Matches(kind, id)) return true;
            return false;
        }

        /// <summary>Population d'une vie (combattant), ou null.</summary>
        public Population? Of(string combatant) => _byCreature.TryGetValue(combatant, out Population p) ? p : null;

        /// <summary>Vie en cours d'une population.</summary>
        public string CurrentOf(string population) => _lives.First(l => l.Population.Creature == population).Current;

        /// <summary>Après un tick de la zone : laisse, morts et départs à remplacer, arrivées dues au tick suivant.</summary>
        /// <param name="reserved">Revue Codex de la PR #48 : places de combattants gardées pour d'autres arrivées (formes du chef encore à venir) :
        /// les populations ne les prennent pas.</param>
        /// <param name="wanted">Réponses de Damien du 31 octobre 2026 : une créature rare de la nuit (<see cref="Population.Night"/>) est-elle
        /// voulue dans la zone au prochain tick (<c>(population, tick)</c>) ? Null : jamais.</param>
        public IReadOnlyList<PopulationEvent> Step(Zone zone, int reserved = 0, Func<Population, long, bool>? wanted = null)
        {
            CombatEngine engine = zone.Engine;
            if (engine.IsFinished) return Array.Empty<PopulationEvent>();
            long now = engine.CurrentTick;   // prochain tick simulé
            long tickMs = engine.TickMilliseconds;
            List<PopulationEvent>? events = null;
            foreach (Life life in _lives)
            {
                if (life.Exhausted) continue;
                if (life.Population.Night is NightSpawn night)
                {
                    PopulationEvent? n = StepNight(zone, life, night.SignMs, night.Sign, false, now, tickMs, reserved, wanted);
                    if (n != null) (events ??= new List<PopulationEvent>()).Add(n);
                    continue;
                }
                if (life.Population.Timed)
                {
                    // Sous-lot V2.1 : même vie qu'une créature rare de la nuit, voulue à ses heures ; elle arrive aussitôt (sans délai de signe).
                    // Sous-lot V2.4 : de même pour une créature voulue par son temps (la brume).
                    PopulationEvent? n = StepNight(zone, life, 0, life.Population.TimedSign, true, now, tickMs, reserved, wanted);
                    if (n != null) (events ??= new List<PopulationEvent>()).Add(n);
                    continue;
                }
                if (life.RespawnAt is null)
                {
                    CombatantView v = engine.View(life.Current);
                    bool refused = !v.IsPresent && !v.HasLeft && !v.IsDead && v.ArrivalTick is long at && at < now;
                    if (v.IsDead)
                    {
                        life.RespawnAt = checked(now - 1 + life.Population.RespawnMs / tickMs);
                        life.Died = true;   // sous-lot V2.9.1
                    }
                    else if (v.HasLeft || refused) life.RespawnAt = now;   // laisse (ou arrivée refusée) : une vie calme la remplace aussitôt
                    else if (!life.Leashing && !Flees(life.Population) && v.IsPresent && v.Engaged && v.CurrentTarget is string target
                        && Beyond(v, engine.View(target), life.Population.LeashRange))
                    {
                        // Revue Codex de la PR #45 : un départ refusé par le pilote (plafond de commandes, refus atomique) ne fait pas échouer le
                        // tick ; la laisse est retentée au tick suivant.
                        if (TryLeave(zone, life.Current))
                        {
                            life.Leashing = true;
                            (events ??= new List<PopulationEvent>()).Add(new PopulationEvent(PopulationEventKind.Leashed, life.Population.Creature, life.Current, now));
                        }
                    }
                }
                if (life.RespawnAt is long due && due <= now)
                {
                    PopulationEvent? e = Respawn(zone, life, now, tickMs, reserved);
                    if (e != null) (events ??= new List<PopulationEvent>()).Add(e);
                }
            }
            return events ?? (IReadOnlyList<PopulationEvent>)Array.Empty<PopulationEvent>();
        }

        /// <summary>
        /// Réponses de Damien du 31 octobre 2026 : vie d'une créature rare de la nuit. Absente, elle vient quand elle est voulue
        /// (<paramref name="wanted"/>) : d'abord son signe, puis son arrivée après <see cref="NightSpawn.SignMs"/> (si elle est toujours voulue) ;
        /// morte, pas avant son délai de réapparition, et seulement si elle est voulue de nouveau ; présente et plus voulue, elle s'en va dès
        /// qu'elle est au calme (pas engagée). La laisse reste celle des autres populations.
        /// </summary>
        /// <remarks>
        /// Sous-lot V2.1 : aussi la vie d'une créature à heures (<paramref name="hours"/>) : voulue à ses heures, elle arrive sans délai ; son
        /// signe, s'il y en a un, s'annonce à son retour au début de ses heures, pas quand elle revient après sa mort. Hors de ses heures, elle
        /// s'en va dès qu'elle est au calme (elle rentre, elle dort) : un combat en cours n'est jamais coupé.
        /// </remarks>
        private PopulationEvent? StepNight(Zone zone, Life life, long signMs, string? sign, bool hours, long now, long tickMs, int reserved,
            Func<Population, long, bool>? wanted)
        {
            CombatEngine engine = zone.Engine;
            bool want = wanted != null && wanted(life.Population, now);
            if (life.Dormant)
            {
                if (life.RespawnAt is long due && due > now) return null;   // morte : son délai d'abord
                if (hours && life.ArriveAt is null && want && (sign is null || life.Slain))
                {
                    life.RespawnAt = null;
                    life.ArriveAt = now;   // à ses heures : arrivée aussitôt, sans signe
                }
                if (life.ArriveAt is long at)
                {
                    if (at > now) return null;
                    if (!want)
                    {
                        life.ArriveAt = null;   // plus voulue entre le signe et l'arrivée (le jour s'est levé) : elle ne vient pas
                        return null;
                    }
                    PopulationEvent? arrived = Respawn(zone, life, now, tickMs, reserved);
                    if (arrived != null && arrived.Kind == PopulationEventKind.Respawned)
                    {
                        life.Dormant = false;
                        life.ArriveAt = null;
                        life.Slain = false;
                    }
                    else if (arrived is null) life.ArriveAt = life.RespawnAt;   // place occupée : nouvel essai une seconde plus tard
                    return arrived;
                }
                if (!want)
                {
                    life.Slain = false;   // ses heures sont finies : à leur retour, elle s'annonce de nouveau
                    return null;
                }
                life.RespawnAt = null;
                life.ArriveAt = checked(now + signMs / tickMs);
                return new PopulationEvent(PopulationEventKind.Sign, life.Population.Creature, sign!, now);
            }
            CombatantView v = engine.View(life.Current);
            bool refused = !v.IsPresent && !v.HasLeft && !v.IsDead && v.ArrivalTick is long arrival && arrival < now;
            if (v.IsDead)
            {
                life.Dormant = true;
                life.Withdrawing = false;
                life.Slain = hours;
                life.Died = true;   // sous-lot V2.9.1
                life.RespawnAt = checked(now - 1 + life.Population.RespawnMs / tickMs);
                return null;
            }
            if (v.HasLeft || refused)
            {
                if (life.Withdrawing || refused || !want)
                {
                    // Partie d'elle-même (ou arrivée refusée) : absente jusqu'à ce qu'on la veuille de nouveau. Relecture de la PR #81 : une
                    // laisse quand elle n'est plus voulue (le jour levé, l'objectif rempli) compte comme un départ, pas comme une vie à remplacer.
                    life.Dormant = true;
                    life.Withdrawing = false;
                    life.Leashing = false;
                    life.RespawnAt = null;
                    return null;
                }
                // Laisse : une vie calme la remplace aussitôt, comme les autres populations (place occupée : une seconde plus tard).
                if (life.RespawnAt is long wait && wait > now) return null;
                return Respawn(zone, life, now, tickMs, reserved);
            }
            if (life.Withdrawing || life.Leashing || !v.IsPresent) return null;
            // Relecture de la PR #104 : une bête qui fuit n'a pas de laisse (elle reste où sa fuite l'a menée : aucune vie usée).
            if (!Flees(life.Population) && v.Engaged && v.CurrentTarget is string target && Beyond(v, engine.View(target), life.Population.LeashRange))
            {
                if (TryLeave(zone, life.Current))
                {
                    life.Leashing = true;
                    return new PopulationEvent(PopulationEventKind.Leashed, life.Population.Creature, life.Current, now);
                }
                return null;
            }
            // Relecture de la PR #104 : hors de ses heures ou de son temps, elle s'en va aussi d'un combat de bêtes (ou d'une fuite) ; un combat
            // contre un joueur la retient, comme avant.
            if (!want && !HeldByFight(engine, v, life.Population) && TryLeave(zone, life.Current))
            {
                life.Withdrawing = true;
                return new PopulationEvent(PopulationEventKind.Withdrew, life.Population.Creature, life.Current, now);
            }
            return null;
        }

        /// <summary>
        /// Relecture de la PR #104 : places de combattants qu'une bête d'ambiance (<see cref="IsAmbient"/>) ne prend jamais : elles restent aux
        /// autres créatures (celles des quêtes, des contrats et des chasses). Une bête d'ambiance qui n'a plus de place attend la session
        /// suivante, sans annonce.
        /// </summary>
        public const int QuestReserve = 64;

        /// <summary>
        /// Relecture de la PR #104 : bête d'ambiance : une bête qui fuit (cerf, lièvre) ou une créature qui ne vient que par son temps (la brume).
        /// Aucune quête ne les demande ; leurs arrivées n'entament jamais <see cref="QuestReserve"/>.
        /// </summary>
        internal static bool IsAmbient(Population population) => Flees(population) || population.Weather != null;

        private static bool Flees(Population population) => population.Template.Behavior?.Flee == true;

        /// <summary>
        /// Retenue par un combat : engagée, sauf une bête qui fuit (elle ne se bat pas) et une bête qui ne vise qu'une autre bête (combat de
        /// bêtes). Engagée contre un joueur, ou sans cible, elle reste, comme avant.
        /// </summary>
        private static bool HeldByFight(CombatEngine engine, CombatantView v, Population population)
        {
            if (!v.Engaged || Flees(population)) return false;
            return !(v.CurrentTarget is string target && engine.View(target).Kind == CombatantKind.Assailant);
        }

        private PopulationEvent? Respawn(Zone zone, Life life, long now, long tickMs, int reserved)
        {
            CombatEngine engine = zone.Engine;
            AssailantSetup t = life.Population.Template;
            if (IsAmbient(life.Population) && checked(engine.CombatantIds.Count + reserved + QuestReserve) >= zone.Setup.Combat.MaxCombatants)
            {
                life.Exhausted = true;   // relecture de la PR #104 : sans annonce ; la réserve reste aux autres créatures
                return null;
            }
            if (checked(engine.CombatantIds.Count + reserved) >= zone.Setup.Combat.MaxCombatants)
            {
                life.Exhausted = true;
                return new PopulationEvent(PopulationEventKind.Exhausted, t.Id, life.Current, now);
            }
            // Place occupée par un combattant présent : nouvel essai une seconde plus tard.
            if (Occupied(engine, t.Placement!))
            {
                life.RespawnAt = checked(now + Math.Max(1, 1000 / tickMs));
                return null;
            }
            // Revue Codex de la PR #45 : l'identité suivante est libre parmi tous les combattants du moteur (personnage, créature du scénario
            // hors population) : une identité prise est sautée, jamais retentée.
            var taken = new HashSet<string>(engine.CombatantIds, StringComparer.Ordinal);
            int number = checked(life.Number + 1);
            string id = $"{t.Id}_v{number}";
            while (taken.Contains(id)) id = $"{t.Id}_v{number = checked(number + 1)}";
            var setup = new AssailantSetup(id, t.Team, t.Armor, t.MagicResistance, t.Target, t.FirstAttackMs, t.IntervalMs, t.Attacks, t.Initiative,
                t.StatusResistance, t.Triggers, t.Health, t.Placement, checked(now * tickMs), t.Behavior);
            try
            {
                engine.Join(setup);
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
            {
                // Arrivée refusée (place prise, file pleine : refus atomique) : nouvel essai une seconde plus tard.
                life.RespawnAt = checked(now + Math.Max(1, 1000 / tickMs));
                return null;
            }
            life.Number = number;
            life.Current = id;
            life.RespawnAt = null;
            life.Leashing = false;
            if (life.Died) _afterDeath.Add(id);   // sous-lot V2.9.1 : un écho
            life.Died = false;
            _byCreature[id] = life.Population;
            return new PopulationEvent(PopulationEventKind.Respawned, t.Id, id, now);
        }

        private static bool TryLeave(Zone zone, string combatant)
        {
            try
            {
                zone.Leave(combatant);
                return true;
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
            {
                return false;
            }
        }

        private static bool Occupied(CombatEngine engine, CombatantPlacement place)
        {
            foreach (string id in engine.CombatantIds)
            {
                CombatantView v = engine.View(id);
                if (!v.IsPresent || v.IsDead || v.Position is not GroundVector p || v.Radius is not Fixed r) continue;
                if (!Beyond(p, r, place.Position, place.Radius, Fixed.Zero)) return true;
            }
            return false;
        }

        private static bool Beyond(CombatantView creature, CombatantView target, Fixed leash) =>
            target.IsPresent && !target.IsDead && creature.Position is GroundVector a && creature.Radius is Fixed ra && target.Position is GroundVector b
            && target.Radius is Fixed rb && Beyond(a, ra, b, rb, leash);

        /// <summary>Écart entre les bords des deux emprises strictement supérieur à <paramref name="gap"/> (calcul exact en valeurs brutes).</summary>
        internal static bool Beyond(GroundVector a, Fixed ra, GroundVector b, Fixed rb, Fixed gap)
        {
            long dx = checked(a.X.Raw - b.X.Raw), dz = checked(a.Z.Raw - b.Z.Raw);
            long reach = checked(gap.Raw + ra.Raw + rb.Raw);
            return checked(dx * dx + dz * dz) > checked(reach * reach);
        }
    }
}
