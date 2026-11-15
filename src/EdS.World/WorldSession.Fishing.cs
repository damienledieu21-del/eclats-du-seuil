using System;
using System.Linq;
using EdS.Data.Fishing;
using EdS.Data.Saves;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Randomness;

namespace EdS.World
{
    /// <summary>Pêche et carnet : refus d'un geste de pêche (un refus ne change rien).</summary>
    public enum FishingRefusal
    {
        None = 0,

        /// <summary>Pas de pêche dans cette partie, ou poste inconnu.</summary>
        Unknown = 1,

        /// <summary>Trop loin du poste (personnage absent ou mort compris).</summary>
        NotHere = 2,

        /// <summary>Une ligne est déjà à l'eau.</summary>
        AlreadyCasting = 3,

        /// <summary>Aucune ligne à l'eau.</summary>
        NotCasting = 4,

        /// <summary>Le sac ne peut pas recevoir la prise (refusé : on peut faire de la place et ferrer pendant la fenêtre).</summary>
        Full = 5,

        /// <summary>En combat : on ne pêche pas.</summary>
        InCombat = 6,
    }

    /// <summary>Pêche et carnet : issue d'un coup de ferrage.</summary>
    public enum FishingOutcome
    {
        /// <summary>Prise (voir <see cref="FishingCatch"/>).</summary>
        Caught = 0,

        /// <summary>Ferré avant la touche : le poisson s'est méfié.</summary>
        TooEarly = 1,

        /// <summary>Ferré après la fenêtre : il a filé.</summary>
        TooLate = 2,
    }

    /// <summary>Pêche et carnet : une prise : espèce (objet), taille (cm), et si c'est la plus grande du carnet pour cette espèce.</summary>
    public sealed class FishingCatch
    {
        public string Fish { get; }
        public int Cm { get; }
        public bool Record { get; }

        internal FishingCatch(string fish, int cm, bool record)
        {
            Fish = fish;
            Cm = cm;
            Record = record;
        }
    }

    /// <summary>
    /// Pêche et carnet (V4.1 du plan de l'alpha : pêche au lac avec un petit jeu, carnet des prises) : on lance sa ligne depuis un poste
    /// (<see cref="Cast"/>), la touche vient après un délai tiré au sort, puis on ferre (<see cref="Strike"/>) pendant la fenêtre : trop tôt
    /// ou trop tard, le lancer est perdu. L'espèce (parmi celles du poste qui mordent en cette saison et à ce moment de la journée, selon
    /// leur poids) et la taille sont tirées par un <c>Pcg32</c> initialisé par la clé (personnage, numéro du lancer) : le même lancer donne
    /// toujours la même chose ; chaque lancer réglé (prise, trop tôt, trop tard, ligne ramenée) fait avancer le compteur, gardé dans la
    /// sauvegarde (v21) avec le carnet. Les temps sont des ticks du monde, jamais une horloge réelle.
    /// </summary>
    public sealed partial class WorldSession
    {
        private FishingBook? _fishing;
        private FishingSpot? _castSpot;
        private long _biteTick, _windowEndTick;

        /// <summary>Pêche en jeu (null : aucune).</summary>
        public FishingBook? FishingBook => _fishing;

        public void UseFishing(FishingBook? book)
        {
            _fishing = book;
            _castSpot = null;
        }

        /// <summary>Pêche et carnet du personnage (lancers, carnet des prises).</summary>
        public SavedFishing Fishing => _character.Fishing;

        /// <summary>Poste d'où la ligne est lancée, ou null.</summary>
        public FishingSpot? CastSpot => _castSpot;

        /// <summary>Ça mord : la fenêtre pour ferrer est ouverte.</summary>
        public bool IsBiting => _castSpot != null && Zone.WorldTick >= _biteTick && Zone.WorldTick <= _windowEndTick;

        /// <summary>Tick du monde où la touche vient (ligne à l'eau seulement).</summary>
        public long? BiteTick => _castSpot is null ? (long?)null : _biteTick;

        /// <summary>Poste à portée du personnage (le plus proche), ou null.</summary>
        public FishingSpot? FishingSpotNear(GroundVector? player)
        {
            if (_fishing is null || !(player is GroundVector p)) return null;
            FishingBook b = _fishing;
            return b.Spots.Where(s => !ZonePopulations.Beyond(p, Fixed.Zero, s.Position, Fixed.Zero, b.Range))
                .OrderBy(s => Planar.LengthSquared(checked(s.Position.X.Raw - p.X.Raw), checked(s.Position.Z.Raw - p.Z.Raw))).FirstOrDefault();
        }

        private Pcg32 FishingRandom() =>
            new Pcg32(Loot.Fnv1a($"fishing:{_character.Id}:{_character.Fishing.Casts}"), Loot.Fnv1a("fishing:" + _character.Id));

        private long Ticks(long ms) => (ms + Zone.Engine.TickMilliseconds - 1) / Zone.Engine.TickMilliseconds;

        /// <summary>Lance la ligne depuis le poste <paramref name="spot"/> : la touche viendra après un délai tiré au sort.</summary>
        public FishingRefusal Cast(string spot)
        {
            if (_fishing is null || spot is null || !(_fishing.Spot(spot) is FishingSpot s)) return FishingRefusal.Unknown;
            if (_castSpot != null) return FishingRefusal.AlreadyCasting;
            if (InCombat) return FishingRefusal.InCombat;
            if (!(((INarrativeWorld)this).PlayerPosition is GroundVector p) || ZonePopulations.Beyond(p, Fixed.Zero, s.Position, Fixed.Zero, _fishing.Range))
                return FishingRefusal.NotHere;
            Pcg32 random = FishingRandom();
            long wait = checked(_fishing.BiteMinMs + random.NextBounded((uint)(_fishing.BiteMaxMs - _fishing.BiteMinMs + 1)));
            _castSpot = s;
            _biteTick = checked(Zone.WorldTick + Ticks(wait));
            _windowEndTick = checked(_biteTick + Ticks(_fishing.WindowMs));
            return FishingRefusal.None;
        }

        /// <summary>
        /// Ferre : pendant la fenêtre, une prise (dans le sac et au carnet) ; trop tôt ou trop tard, le lancer est perdu. Refus (rien ne change) :
        /// aucune ligne à l'eau, trop loin du poste, ou sac plein pendant la fenêtre.
        /// </summary>
        public FishingRefusal Strike(out FishingOutcome outcome, out FishingCatch? caught)
        {
            outcome = FishingOutcome.TooEarly;
            caught = null;
            if (_fishing is null || !(_castSpot is FishingSpot s)) return FishingRefusal.NotCasting;
            if (!(((INarrativeWorld)this).PlayerPosition is GroundVector p) || ZonePopulations.Beyond(p, Fixed.Zero, s.Position, Fixed.Zero, _fishing.Range))
                return FishingRefusal.NotHere;
            long now = Zone.WorldTick;
            if (now < _biteTick || now > _windowEndTick)
            {
                outcome = now < _biteTick ? FishingOutcome.TooEarly : FishingOutcome.TooLate;
                _character = _character.WithFishing(_character.Fishing.WithCast());
                _castSpot = null;
                return FishingRefusal.None;
            }
            // Le même flux que le délai : l'espèce puis la taille.
            Pcg32 random = FishingRandom();
            random.NextBounded((uint)(_fishing.BiteMaxMs - _fishing.BiteMinMs + 1));
            string phase = Narrative.PhaseId(Zone.Time.Phase);
            FishKind[] biting = s.Fish.Select(f => _fishing.Kind(f)).Where(f => f.Bites(SeasonIndex, phase)).ToArray();
            if (biting.Length == 0) biting = new[] { _fishing.Kind(s.Fish[0]) };   // rien ne mord à cette heure : la première espèce du poste
            int total = biting.Sum(f => f.Weight);
            int roll = (int)random.NextBounded((uint)total);
            FishKind kind = biting[0];
            foreach (FishKind f in biting)
            {
                if (roll < f.Weight) { kind = f; break; }
                roll -= f.Weight;
            }
            int cm = kind.MinCm + (int)random.NextBounded((uint)(kind.MaxCm - kind.MinCm + 1));
            if (!Inventory.CanAdd(kind.Item, 1)) return FishingRefusal.Full;
            Inventory.Add(kind.Item, 1);
            bool record = !(_character.Fishing.Of(kind.Item) is SavedCatch old) || cm > old.BestCm;
            _character = _character.WithFishing(_character.Fishing.WithCast().WithCatch(kind.Item, cm));
            _castSpot = null;
            outcome = FishingOutcome.Caught;
            caught = new FishingCatch(kind.Item, cm, record);
            return FishingRefusal.None;
        }

        /// <summary>Ramène la ligne (le lancer compte, sans prise).</summary>
        public FishingRefusal CancelCast()
        {
            if (_castSpot is null) return FishingRefusal.NotCasting;
            _character = _character.WithFishing(_character.Fishing.WithCast());
            _castSpot = null;
            return FishingRefusal.None;
        }
    }
}
