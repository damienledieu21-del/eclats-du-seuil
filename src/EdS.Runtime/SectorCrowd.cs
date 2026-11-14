using System;
using System.Collections.Generic;
using EdS.Domain.Space;
using EdS.Simulation.Engine;
using EdS.World;

namespace EdS.Runtime
{
    /// <summary>
    /// Sous-lot V2.28b (3) : place affichée d'un corps qui glisse d'un tick à l'autre (en mètres) : <see cref="From"/> est la place du tick
    /// précédent, <see cref="To"/> celle du tick courant ; entre deux ticks, l'affichage prend <see cref="X"/> et <see cref="Z"/> à la
    /// fraction écoulée du tick. Valeur sans allocation.
    /// </summary>
    public struct GlideTrack
    {
        public double FromX, FromZ, ToX, ToZ;

        /// <summary>Faux tant qu'aucune place n'a été donnée (ou après <see cref="Forget"/>).</summary>
        public bool Known;

        /// <summary>Place posée d'un coup (première vue, session nouvelle) : pas de glissement.</summary>
        public void Snap(double x, double z)
        {
            FromX = ToX = x;
            FromZ = ToZ = z;
            Known = true;
        }

        /// <summary>Tick suivant : la place du tick courant devient celle d'où l'on part.</summary>
        public void Next(double x, double z)
        {
            if (!Known)
            {
                Snap(x, z);
                return;
            }
            FromX = ToX;
            FromZ = ToZ;
            ToX = x;
            ToZ = z;
        }

        public void Forget() => Known = false;

        /// <summary>Abscisse affichée à la fraction <paramref name="t"/> (bornée entre 0 et 1) du tick.</summary>
        public double X(double t) => FromX + (ToX - FromX) * Clamp(t);

        public double Z(double t) => FromZ + (ToZ - FromZ) * Clamp(t);

        private static double Clamp(double t) => double.IsNaN(t) || t < 0 ? 0 : t > 1 ? 1 : t;
    }

    /// <summary>
    /// Sous-lot V2.28b (3) : le déplacement du personnage montré sans saut à travers un passage de secteur (ou un renouvellement de session).
    /// Au passage, le personnage quitte la session de départ en fin de tick et arrive dans celle d'arrivée au tick suivant, à la place où il
    /// est parti (<see cref="SectorWorld"/>) : pendant ce tick, son nouveau combattant n'est pas encore présent. La vue le montre quand même,
    /// à cette place d'arrivée (<see cref="Arriving"/>), et son glissement continue d'un moteur à l'autre comme d'un tick au suivant : la
    /// place du dernier tick de l'ancien moteur est celle d'où il part, la place d'arrivée celle où il va. Si la place d'arrivée n'est pas
    /// connue, elle est prédite en prolongeant le dernier pas. Une session nouvelle (première vue, halte, reprise) pose la place d'un coup.
    /// </summary>
    public sealed class PassageGlide
    {
        private GlideTrack _track;
        private string? _id;
        private long _tick = long.MinValue;
        private int _predicted;

        /// <summary>Plus de ticks prédits de suite que cela : le personnage est caché (reprise sans combattant, attente d'une place).</summary>
        public const int MaxPredictedTicks = 2;

        /// <summary>Glissement en cours (lecture).</summary>
        public GlideTrack Track => _track;

        /// <summary>Le personnage était-il montré à la dernière image ?</summary>
        public bool Shown { get; private set; }

        /// <summary>
        /// Combattant à montrer à ce tick bien qu'absent : son arrivée (passage de secteur, renouvellement) est programmée au tick courant
        /// du moteur, à une place connue ; ni mort, ni parti.
        /// </summary>
        public static bool Arriving(CombatantView view, long tick) =>
            view != null && !view.IsPresent && !view.IsDead && !view.HasLeft && view.ArrivalTick == tick && view.Position.HasValue;

        /// <summary>Combattant montré : présent, ou arrivant (<see cref="Arriving"/>).</summary>
        public static bool Shows(CombatantView view, long tick) => view != null && (view.IsPresent || Arriving(view, tick));

        /// <summary>Oublie tout : la prochaine place sera posée d'un coup (session nouvelle).</summary>
        public void Reset()
        {
            _track.Forget();
            _id = null;
            _tick = long.MinValue;
            _predicted = 0;
            Shown = false;
        }

        /// <summary>
        /// Suit le personnage à chaque image : <paramref name="id"/> son identité du moment, <paramref name="tick"/> le tick courant de son
        /// moteur, <paramref name="view"/> sa vue. Un tick nouveau (ou une identité nouvelle : passage, renouvellement) fait glisser ; rend
        /// vrai si le personnage est montré. Sans allocation.
        /// </summary>
        public bool Follow(string id, long tick, CombatantView view)
        {
            if (view is null) throw new ArgumentNullException(nameof(view));
            return Follow(id, tick, view.IsPresent, view.IsDead, view.HasLeft, view.ArrivalTick, view.Position);
        }

        /// <summary>Même suivi, d'après les champs de la vue (présent, mort, parti, tick d'arrivée, place).</summary>
        public bool Follow(string id, long tick, bool present, bool dead, bool left, long? arrivalTick, GroundVector? position)
        {
            if (id is null) throw new ArgumentNullException(nameof(id));
            bool fresh = tick != _tick || !string.Equals(id, _id, StringComparison.Ordinal);
            if (!fresh) return Shown;
            _tick = tick;
            _id = id;
            if (dead && position is GroundVector corpse)
            {
                _predicted = 0;
                _track.Next(corpse.X.Raw / 10000.0, corpse.Z.Raw / 10000.0);
                return Shown = true;
            }
            // Relecture indépendante de la PR #162 : un combattant parti (sortie, passage dont l'arrivée a échoué) est caché tout de suite,
            // sans prolongation.
            if (left && !present)
            {
                _predicted = 0;
                _track.Forget();
                return Shown = false;
            }
            bool arriving = !present && !dead && arrivalTick == tick && position.HasValue;
            if ((present || arriving) && position is GroundVector p)
            {
                _predicted = 0;
                _track.Next(p.X.Raw / 10000.0, p.Z.Raw / 10000.0);
                return Shown = true;
            }
            // Absent sans arrivée connue : on prolonge le dernier pas, un temps borné, puis on cache (et la place suivante sera posée d'un coup).
            if (_track.Known && _predicted < MaxPredictedTicks)
            {
                _predicted++;
                _track.Next(_track.ToX + (_track.ToX - _track.FromX), _track.ToZ + (_track.ToZ - _track.FromZ));
                return Shown = true;
            }
            _track.Forget();
            return Shown = false;
        }
    }

    /// <summary>
    /// Sous-lot V2.28b (3) : les créatures de <b>tous les secteurs ouverts</b> autres que celui du personnage, pour l'affichage (SpaceView et
    /// la carte). Relu une fois par tick du monde (<see cref="Zone.WorldTick"/>, l'horloge commune des sessions de secteur, pas le tick d'un
    /// moteur) ; entre deux ticks, chaque créature glisse (<see cref="GlideTrack"/>) à la fraction du tick du personnage (toutes les sessions
    /// avancent au même tick du monde). Une créature montrée est présente, vivante et a un corps ; les personnages n'y sont pas (le
    /// combattant que le personnage a quitté est parti). Lecture seule : ne change rien à la partie.
    /// <para>
    /// Sans allocation par image ni par tick une fois les créatures vues : la liste des secteurs n'est recopiée que lorsqu'elle change
    /// (<see cref="WorldSession.SectorsVersion"/>), les places sont lues par <see cref="CombatEngine.PlaceAt"/>, une fiche n'est créée qu'à la
    /// première vue d'une créature. La partie est donnée à chaque appel (jamais retenue dans un rappel) : une partie remplacée (halte,
    /// reprise, voyage) repart de zéro.
    /// </para>
    /// </summary>
    public sealed class SectorCrowd
    {
        /// <summary>Une créature d'un autre secteur ouvert.</summary>
        public sealed class Member
        {
            internal Member(string sector, string id, string key)
            {
                Sector = sector;
                Id = id;
                Key = key;
            }

            /// <summary>Secteur de sa session.</summary>
            public string Sector { get; }

            /// <summary>Identité dans sa session (deux secteurs peuvent avoir la même).</summary>
            public string Id { get; }

            /// <summary>Clé d'affichage unique, « secteur/identité ».</summary>
            public string Key { get; }

            public string Team { get; internal set; } = string.Empty;
            public CombatantKind Kind { get; internal set; }

            /// <summary>D'un autre camp que le personnage.</summary>
            public bool Enemy { get; internal set; }

            /// <summary>Montrée à ce tick (présente, vivante, dans un secteur ouvert autre que celui du personnage).</summary>
            public bool Shown { get; internal set; }

            /// <summary>Glissement entre le tick précédent et le tick courant.</summary>
            public GlideTrack Track;

            /// <summary>Orientation (en dix-millièmes) et rayon de l'emprise (en mètres).</summary>
            public long FacingX { get; internal set; }

            public long FacingZ { get; internal set; }
            public double Radius { get; internal set; }

            internal Zone? Zone;
            internal long Round = -1;
        }

        /// <summary>Au plus tant de fiches gardées (4 secteurs pleins) ; au-delà, une créature nouvelle n'est pas montrée.</summary>
        public const int MaxMembers = 4096;

        private readonly List<string> _names = new List<string>();
        private readonly List<Zone> _zones = new List<Zone>();
        private readonly Dictionary<(string Sector, string Id), Member> _byKey = new Dictionary<(string Sector, string Id), Member>();
        private readonly List<Member> _members = new List<Member>();
        private WorldSession? _world;
        private Zone? _own;
        private long _version = -1, _tick = long.MinValue, _round;
        private string _team = string.Empty;

        /// <summary>Fiches de toutes les créatures vues (montrées ou non), dans l'ordre de leur première vue.</summary>
        public IReadOnlyList<Member> Members => _members;

        /// <summary>Secteurs ouverts (ordre des noms) à la dernière relecture.</summary>
        public IReadOnlyList<string> Sectors => _names;

        /// <summary>Secteur du personnage à la dernière relecture (chaîne vide hors de l'hôte).</summary>
        public string OwnSector { get; private set; } = string.Empty;

        /// <summary>Nombre de relectures faites (une par tick du monde au plus).</summary>
        public long Reads { get; private set; }

        /// <summary>
        /// Change quand des fiches sont oubliées (partie remplacée, secteur fermé) : l'affichage libère alors les corps des fiches disparues.
        /// </summary>
        public long Generation { get; private set; }

        /// <summary>Nombre de créatures montrées à la dernière relecture.</summary>
        public int ShownCount { get; private set; }

        /// <summary>Oublie tout (autre partie, autre zone).</summary>
        public void Clear()
        {
            _names.Clear();
            _zones.Clear();
            _byKey.Clear();
            if (_members.Count > 0) Generation = checked(Generation + 1);
            _members.Clear();
            _world = null;
            _own = null;
            _version = -1;
            _tick = long.MinValue;
            _team = string.Empty;
            OwnSector = string.Empty;
            ShownCount = 0;
        }

        /// <summary>
        /// À chaque image : relit les créatures des autres secteurs ouverts si le tick du monde, la liste des secteurs ou la session du
        /// personnage ont changé ; rend vrai s'il y a eu relecture. Ne lève pas sur une partie sans combattant (reprise, refus).
        /// </summary>
        public bool Refresh(WorldSession world)
        {
            if (world is null) throw new ArgumentNullException(nameof(world));
            if (!ReferenceEquals(world, _world))
            {
                Clear();
                _world = world;
            }
            Zone own = world.Zone;
            // Relecture indépendante de la PR #162 : cadencé sur le tick de l'hôte (la session du personnage peut être fermée pendant qu'il
            // attend une place : son tick ne bouge plus, les autres secteurs si).
            long tick = world.SectorsTick;
            bool listChanged = world.SectorsVersion != _version || !ReferenceEquals(own, _own);
            if (!listChanged && tick == _tick) return false;
            if (listChanged) Relist(world, own);
            else if (_team.Length == 0) _team = TeamOf(own.Engine, world.CombatantId);   // combattant du personnage pas encore là au relevé
            _tick = tick;
            _round = checked(_round + 1);
            Reads = checked(Reads + 1);
            int shown = 0;
            for (int i = 0; i < _zones.Count; i++)
            {
                Zone zone = _zones[i];
                if (ReferenceEquals(zone, own)) continue;
                CombatEngine engine = zone.Engine;
                string sector = _names[i];
                int count = engine.CombatantCount;
                for (int j = 0; j < count; j++)
                {
                    CombatantPlace place = engine.PlaceAt(j);
                    if (!place.IsPresent || place.IsDead || !place.HasBody || place.Kind == CombatantKind.Character) continue;
                    Member? m = Find(sector, place.Id, true);
                    if (m is null) continue;   // plafond atteint
                    double x = place.X / 10000.0, z = place.Z / 10000.0;
                    bool continued = m.Round == checked(_round - 1) && ReferenceEquals(m.Zone, zone) && m.Track.Known;
                    if (continued) m.Track.Next(x, z);
                    else m.Track.Snap(x, z);
                    m.Zone = zone;
                    m.Round = _round;
                    m.Team = place.Team;
                    m.Kind = place.Kind;
                    // Camp du personnage inconnu (son combattant manque) : personne n'est montré en ennemi plutôt que tout le monde.
                    m.Enemy = _team.Length > 0 && !string.Equals(place.Team, _team, StringComparison.Ordinal);
                    m.FacingX = place.FacingX;
                    m.FacingZ = place.FacingZ;
                    m.Radius = place.Radius / 10000.0;
                    m.Shown = true;
                    shown++;
                }
            }
            for (int i = 0; i < _members.Count; i++)
                if (_members[i].Round != _round) _members[i].Shown = false;
            ShownCount = shown;
            return true;
        }

        /// <summary>
        /// Relecture indépendante des passages : une créature de la session que le personnage vient de quitter (secteur
        /// <paramref name="sector"/>) était montrée par la vue de la session à la place (<paramref name="x"/>, <paramref name="z"/>) du
        /// tick précédent ; sa fiche reprend de là, sans saut, au prochain <see cref="Refresh"/>. Sans effet si le secteur n'est plus ouvert,
        /// si c'est celui du personnage ou si la créature n'y est pas. Appelé seulement au passage (allocation possible à la première vue).
        /// </summary>
        public void Seed(WorldSession world, string sector, string id, double x, double z)
        {
            if (world is null) throw new ArgumentNullException(nameof(world));
            if (sector is null) throw new ArgumentNullException(nameof(sector));
            if (id is null) throw new ArgumentNullException(nameof(id));
            if (!ReferenceEquals(world, _world))
            {
                Clear();
                _world = world;
            }
            Zone own = world.Zone;
            if (world.SectorsVersion != _version || !ReferenceEquals(own, _own)) Relist(world, own);
            int index = _names.IndexOf(sector);
            if (index < 0 || ReferenceEquals(_zones[index], own)) return;
            Member? m = Find(sector, id, true);
            if (m is null) return;
            m.Track.Snap(x, z);
            m.Zone = _zones[index];
            m.Round = _round;   // comme montrée à la relecture précédente : la suivante la fera glisser
        }

        /// <summary>
        /// Place montrée au dernier tick lu d'une créature du secteur <paramref name="sector"/> (pour la reprendre sans saut quand le
        /// personnage entre dans ce secteur et que la vue de sa session la montre) ; faux si elle n'était pas montrée.
        /// </summary>
        public bool TryLast(string sector, string id, out double x, out double z)
        {
            x = z = 0;
            if (sector is null || id is null || !_byKey.TryGetValue((sector, id), out Member? m) || m.Round != _round || !m.Track.Known) return false;
            x = m.Track.ToX;
            z = m.Track.ToZ;
            return true;
        }

        private void Relist(WorldSession world, Zone own)
        {
            world.OpenSectorZones(_names, _zones);
            _version = world.SectorsVersion;
            _own = own;
            OwnSector = world.Sector ?? string.Empty;
            _team = TeamOf(own.Engine, world.CombatantId);
            // Fiches d'un secteur fermé : oubliées (la liste ne grandit pas sans fin).
            for (int i = _members.Count - 1; i >= 0; i--)
                if (!_names.Contains(_members[i].Sector))
                {
                    _byKey.Remove((_members[i].Sector, _members[i].Id));
                    _members.RemoveAt(i);
                    Generation = checked(Generation + 1);
                }
        }

        private static string TeamOf(CombatEngine engine, string id)
        {
            int count = engine.CombatantCount;
            for (int j = 0; j < count; j++)
            {
                CombatantPlace p = engine.PlaceAt(j);
                if (string.Equals(p.Id, id, StringComparison.Ordinal)) return p.Team ?? string.Empty;
            }
            return string.Empty;
        }

        private Member? Find(string sector, string id, bool create)
        {
            if (_byKey.TryGetValue((sector, id), out Member? m)) return m;
            if (!create || _members.Count >= MaxMembers) return null;
            m = new Member(sector, id, sector + "/" + id);
            _byKey[(sector, id)] = m;
            _members.Add(m);
            return m;
        }
    }
}
