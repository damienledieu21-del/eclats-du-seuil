using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Saves;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;

namespace EdS.World
{
    /// <summary>Genre d'événement consommable d'une zone (ARCHITECTURE_MONDE.md, section 6).</summary>
    public enum ZoneEventKind
    {
        /// <summary>Mort d'un combattant de la zone (une seule par vie ; la clé sert à n'attribuer une récompense qu'une fois).</summary>
        Death = 0,
    }

    /// <summary>
    /// Événement consommable : clé unique dans le monde (<c>zone.vallons/3/death/loup1</c> : zone, session, genre, combattant) ;
    /// <see cref="FirstTime"/> est faux si la clé figurait déjà au registre (sauvegarde rechargée, message en double).
    /// </summary>
    public sealed class ZoneEvent
    {
        public ZoneEventKind Kind { get; }
        public string Key { get; }
        public string Combatant { get; }
        public long Tick { get; }
        public bool FirstTime { get; }

        internal ZoneEvent(ZoneEventKind kind, string key, string combatant, long tick, bool firstTime)
        {
            Kind = kind;
            Key = key;
            Combatant = combatant;
            Tick = tick;
            FirstTime = firstTime;
        }
    }

    /// <summary>
    /// Rencontre en cours (ARCHITECTURE_MONDE.md, section 2) : des créatures engagées et les personnages qu'elles poursuivent, regroupés
    /// de proche en proche. Ce n'est pas un combat séparé : un regroupement que la zone suit pour ses règles (laisse, réinitialisation,
    /// crédit des actions : sous-lots suivants).
    /// </summary>
    public sealed class Encounter
    {
        /// <summary>Créatures engagées, vivantes et présentes, par identifiant.</summary>
        public IReadOnlyList<string> Creatures { get; }

        /// <summary>Personnages poursuivis, vivants et présents, par identifiant.</summary>
        public IReadOnlyList<string> Characters { get; }

        internal Encounter(IEnumerable<string> creatures, IEnumerable<string> characters)
        {
            Creatures = creatures.OrderBy(i => i, StringComparer.Ordinal).ToArray();
            Characters = characters.OrderBy(i => i, StringComparer.Ordinal).ToArray();
        }
    }

    /// <summary>
    /// Sous-lot 12.1 (ARCHITECTURE_MONDE.md, sections 4 à 6) : une session de zone, simulée par le moteur de combat en mode zone (pas de fin
    /// quand une équipe tombe). En solo, la zone tourne sur le poste. Elle tient le temps du monde (ticks de la zone ajoutés à ceux des
    /// sessions précédentes), fait entrer et sortir les personnages (arrivée pilotée et départ du moteur), relève les morts comme événements
    /// consommables à clé unique (registre gardé dans la sauvegarde) et regroupe les rencontres en cours. Déterministe : les mêmes entrées
    /// donnent la même zone, tick après tick.
    /// </summary>
    public sealed class Zone
    {
        private readonly CombatEngine _engine;
        private readonly HashSet<string> _consumed;
        private readonly HashSet<string> _dead = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Identifiant de la zone (« zone.vallons »).</summary>
        public string Id { get; }

        /// <summary>Numéro de cette session de zone (1 pour la première partie, puis à chaque reprise).</summary>
        public long Session { get; }

        /// <summary>Temps du monde au début de la session, en ticks.</summary>
        public long StartWorldTick { get; }

        public WorldClock Clock { get; }

        /// <summary>Moteur de la zone, pour l'affichage et le pilotage (pont temps réel) ; ne pas le faire avancer à côté de <see cref="Step"/>.</summary>
        public CombatEngine Engine => _engine;

        /// <param name="setup">Simulation de la zone (<see cref="CombatSetup.Zone"/> vrai) ; validée par le moteur.</param>
        /// <param name="consumed">Événements déjà consommés (sauvegarde) : une mort de même clé n'est pas attribuée une seconde fois.</param>
        /// <exception cref="ArgumentException">Identifiant de zone invalide, session ou temps hors bornes, ou combat fermé au lieu d'une zone.</exception>
        public Zone(string id, CombatSetup setup, long session, long startWorldTick, IEnumerable<string>? consumed = null, ICombatLog? log = null,
            WorldClock? clock = null)
        {
            if (!SaveValidator.IsZoneId(id)) throw new ArgumentException($"Identifiant de zone « {id} » invalide : « zone.<nom> » attendu.", nameof(id));
            if (setup is null) throw new ArgumentNullException(nameof(setup));
            if (!setup.Zone) throw new ArgumentException("Une zone se simule en mode zone (scénario « zone »: true).", nameof(setup));
            if (session < 1 || session > SaveValidator.MaxWorldTick) throw new ArgumentOutOfRangeException(nameof(session));
            // Revue Codex de la PR #42 : la session entière doit tenir sous la borne des sauvegardes (sa durée réservée d'avance), sinon
            // sa sauvegarde finale serait refusée à la lecture.
            long sessionTicks = checked(setup.DurationMs / setup.Combat.TickMilliseconds + 1);
            if (startWorldTick < 0 || startWorldTick > SaveValidator.MaxWorldTick - sessionTicks)
                throw new ArgumentOutOfRangeException(nameof(startWorldTick), startWorldTick,
                    $"Temps du monde de départ hors de [0, {SaveValidator.MaxWorldTick - sessionTicks}] : la session ({sessionTicks} ticks au plus) doit tenir sous {SaveValidator.MaxWorldTick}.");
            Id = id;
            Session = session;
            StartWorldTick = startWorldTick;
            _consumed = new HashSet<string>(consumed ?? Array.Empty<string>(), StringComparer.Ordinal);
            if (_consumed.Any(k => !SaveValidator.IsEventKey(k))) throw new ArgumentException("Clé d'événement consommé invalide.", nameof(consumed));
            _engine = new CombatEngine(setup, log ?? NullCombatLog.Instance);
            Clock = clock ?? new WorldClock(_engine.TickMilliseconds);
            if (Clock.TickMilliseconds != _engine.TickMilliseconds) throw new ArgumentException("Horloge du monde d'un autre tick que la zone.", nameof(clock));
            foreach (string combatant in _engine.CombatantIds)
            {
                // Revue Codex de la PR #42 : la clé de mort de chaque combattant doit tenir dans le format des sauvegardes.
                if (!DeathKeyFits(combatant))
                    throw new ArgumentException($"Clé de mort de « {combatant} » trop longue ou hors du format des sauvegardes.", nameof(setup));
                _keyed.Add(combatant);
                if (_engine.View(combatant).IsDead) _dead.Add(combatant);
            }
        }

        private readonly HashSet<string> _keyed = new HashSet<string>(StringComparer.Ordinal);

        private bool DeathKeyFits(string combatant) => SaveValidator.IsEventKey(EventKey(ZoneEventKind.Death, combatant));

        /// <summary>Ticks déjà simulés dans cette session (le moteur ne compte plus après son dernier tick : il est compris ici).</summary>
        public long ElapsedTicks => _engine.IsFinished ? checked(_engine.CurrentTick + 1) : _engine.CurrentTick;

        /// <summary>Temps du monde courant, en ticks (début de session plus ticks déjà simulés).</summary>
        public long WorldTick => checked(StartWorldTick + ElapsedTicks);

        public WorldTime Time => Clock.At(WorldTick);

        /// <summary>La session est terminée (au bout de sa durée, ou arrêt de sécurité du moteur).</summary>
        public bool IsOver => _engine.IsFinished;

        /// <summary>Clés des événements consommés, dans l'ordre ordinal.</summary>
        public IReadOnlyList<string> Consumed => _consumed.OrderBy(k => k, StringComparer.Ordinal).ToArray();

        /// <summary>Clé d'un événement de cette session (« zone.vallons/3/death/loup1 »).</summary>
        public string EventKey(ZoneEventKind kind, string combatant) =>
            $"{Id}/{Session}/{(kind == ZoneEventKind.Death ? "death" : throw new ArgumentOutOfRangeException(nameof(kind)))}/{combatant}";

        /// <summary>
        /// Simule un tick de la zone et rend ses événements consommables, dans l'ordre des identifiants ; aucun si la session est terminée.
        /// Chaque mort est inscrite au registre : <see cref="ZoneEvent.FirstTime"/> dit si elle l'est pour la première fois.
        /// </summary>
        /// <exception cref="InvalidOperationException">Registre trop plein pour les morts possibles du tick, ou clé de mort d'un combattant arrivé
        /// hors du format ; rien n'a bougé.</exception>
        public IReadOnlyList<ZoneEvent> Step()
        {
            if (_engine.IsFinished) return Array.Empty<ZoneEvent>();
            // Revue Codex de la PR #42 : place réservée au registre avant le tick (chaque combattant pas encore mort peut mourir), et clé de mort
            // vérifiée pour un combattant arrivé par le moteur ; sinon rien ne bouge, et la sauvegarde reste possible.
            IReadOnlyList<string> ids = _engine.CombatantIds;
            foreach (string combatant in ids)
                if (!_keyed.Contains(combatant))
                {
                    if (!DeathKeyFits(combatant))
                        throw new InvalidOperationException($"Clé de mort de « {combatant} » trop longue ou hors du format des sauvegardes.");
                    _keyed.Add(combatant);
                }
            if (checked(_consumed.Count + ids.Count - _dead.Count) > SaveValidator.MaxConsumed)
                throw new InvalidOperationException($"Registre des événements presque plein ({_consumed.Count} sur {SaveValidator.MaxConsumed}) : plus de place pour les morts possibles de ce tick.");
            long tick = _engine.CurrentTick;
            _engine.Step();
            var events = new List<ZoneEvent>();
            foreach (string combatant in _engine.CombatantIds.OrderBy(i => i, StringComparer.Ordinal))
            {
                if (_dead.Contains(combatant) || !_engine.View(combatant).IsDead) continue;
                _dead.Add(combatant);
                string key = EventKey(ZoneEventKind.Death, combatant);
                events.Add(new ZoneEvent(ZoneEventKind.Death, key, combatant, tick, _consumed.Add(key)));
            }
            return events;
        }

        /// <summary>
        /// Fait entrer un personnage dans la zone au tick courant, à sa place (arrivée pilotée du moteur : refusée au début du tick si une
        /// emprise solide occupe la place, comme une arrivée du scénario).
        /// </summary>
        /// <exception cref="ArgumentException">Identifiant déjà pris, ou arrivée refusée par la validation (motif dans le message).</exception>
        /// <exception cref="InvalidOperationException">Session terminée.</exception>
        public void Enter(CharacterSetup character)
        {
            if (character is null) throw new ArgumentNullException(nameof(character));
            if (!DeathKeyFits(character.Id))
                throw new ArgumentException($"Clé de mort de « {character.Id} » trop longue ou hors du format des sauvegardes.", nameof(character));
            long now = checked(_engine.CurrentTick * _engine.TickMilliseconds);
            _engine.Join(new CharacterSetup(character.Id, character.Team, character.Class, character.Build, character.Initiative, character.Placement, now, character.Gear));
        }

        /// <summary>Fait sortir un combattant (commande <c>leave</c> au tick courant, effective en fin de tick).</summary>
        /// <exception cref="ArgumentException">Commande refusée par le pilote (combattant inconnu, plafond de commandes).</exception>
        public void Leave(string combatant) =>
            _engine.Submit(new CombatCommand(checked(_engine.CurrentTick * _engine.TickMilliseconds), combatant ?? throw new ArgumentNullException(nameof(combatant)),
                CombatAction.Leave));

        /// <summary>Inscrit un événement au registre ; faux s'il y figurait déjà (une seule attribution).</summary>
        /// <exception cref="InvalidOperationException">Registre plein (<see cref="SaveValidator.MaxConsumed"/>).</exception>
        public bool Consume(string key)
        {
            if (!SaveValidator.IsEventKey(key)) throw new ArgumentException($"Clé d'événement « {key} » invalide.", nameof(key));
            if (_consumed.Contains(key)) return false;
            if (_consumed.Count >= SaveValidator.MaxConsumed)
                throw new InvalidOperationException($"Registre des événements plein ({SaveValidator.MaxConsumed}).");
            return _consumed.Add(key);
        }

        public bool IsConsumed(string key) => _consumed.Contains(key);

        /// <summary>
        /// Rencontres en cours : chaque créature engagée, vivante et présente, est reliée à la cible qu'elle poursuit (vivante et présente) ;
        /// les rencontres sont les groupes ainsi reliés de proche en proche (deux créatures sur le même personnage sont dans la même).
        /// Ordre : par plus petit identifiant de créature.
        /// </summary>
        public IReadOnlyList<Encounter> Encounters()
        {
            var parent = new Dictionary<string, string>(StringComparer.Ordinal);
            string Find(string x)
            {
                while (parent[x] != x) x = parent[x] = parent[parent[x]];
                return x;
            }
            void Union(string a, string b)
            {
                if (!parent.ContainsKey(a)) parent[a] = a;
                if (!parent.ContainsKey(b)) parent[b] = b;
                string ra = Find(a), rb = Find(b);
                if (ra != rb) parent[string.CompareOrdinal(ra, rb) < 0 ? rb : ra] = string.CompareOrdinal(ra, rb) < 0 ? ra : rb;
            }
            var views = _engine.CombatantIds.ToDictionary(i => i, i => _engine.View(i), StringComparer.Ordinal);
            bool Alive(CombatantView v) => v.IsPresent && !v.IsDead;
            foreach (CombatantView v in views.Values.OrderBy(v => v.Id, StringComparer.Ordinal))
            {
                if (v.Kind != CombatantKind.Assailant || !Alive(v) || !v.Engaged || v.CurrentTarget is not string target) continue;
                if (!views.TryGetValue(target, out CombatantView? t) || !Alive(t)) continue;
                Union(v.Id, target);
            }
            return parent.Keys.GroupBy(Find, StringComparer.Ordinal)
                .Select(g => new Encounter(g.Where(i => views[i].Kind == CombatantKind.Assailant), g.Where(i => views[i].Kind == CombatantKind.Character)))
                .Where(e => e.Creatures.Count > 0)
                .OrderBy(e => e.Creatures[0], StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>Sauvegarde du monde à ce tick : zone, temps du monde, sessions jouées (celle-ci comprise), registre des événements.</summary>
        public WorldSave Save() => new WorldSave(Id, WorldTick, Session, _consumed);
    }
}
