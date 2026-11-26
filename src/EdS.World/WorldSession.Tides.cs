using EdS.Data.Zones;
using EdS.Domain.Time;

namespace EdS.World
{
    /// <summary>
    /// V2.50.1 (grottes noyées du lac) : marée et plongée dans la session. La marée vient de l'heure du monde au tick courant
    /// (<see cref="Tides.At"/>) ; elle reste basse quand le lac est gelé (glace praticable à la date fournie par l'hôte ; sans contenu
    /// saisonnier ni date, le lac n'est pas gelé). Le souffle vient du sac (<see cref="Tides.Breath"/>). Une porte d'eau se juge à la demande
    /// (<see cref="DoorWaterPass"/>), sans rien changer : l'aventure refuse la porte noyée avant toute sauvegarde.
    /// </summary>
    public sealed partial class WorldSession
    {
        /// <summary>Le lac des Aubes est gelé (glace praticable) à la date fournie ; faux sans contenu saisonnier ni date.</summary>
        public bool LakeFrozen => _seasons != null && _civilSeason?.Ice == IcePhase.Walkable;

        /// <summary>Marée du lac au tick courant du monde.</summary>
        public TideLevel Tide => Tides.At(Zone.Clock.At(Zone.WorldTick), LakeFrozen);

        /// <summary>Minutes du monde avant que la marée tourne (null : lac gelé).</summary>
        public int? TideTurnsIn => Tides.MinutesToTurn(Zone.Clock.At(Zone.WorldTick), LakeFrozen);

        /// <summary>Souffle du personnage (ms) : de base, plus la coquille de souffle s'il l'a dans son sac.</summary>
        public long BreathMs => Tides.Breath(Inventory.Count(Tides.ShellItem));

        /// <summary>
        /// Ce que fait la porte <paramref name="door"/> de la zone en cours à cette marée, avec ce souffle (porte inconnue ou sans eau : à
        /// sec). Ne change rien.
        /// </summary>
        public WaterPass DoorWaterPass(string door) =>
            Definition?.DoorOf(door) is Door d ? Tides.Check(d.Water, Tide, BreathMs) : WaterPass.Dry;
    }
}
