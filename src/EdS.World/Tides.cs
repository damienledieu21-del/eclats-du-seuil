using System;
using EdS.Data.Zones;

namespace EdS.World
{
    /// <summary>V2.50.1 : niveau de la marée du lac des Aubes.</summary>
    public enum TideLevel
    {
        /// <summary>Marée basse : les passages de marée sont à sec.</summary>
        Low = 0,

        /// <summary>Marée haute : les passages de marée sont noyés.</summary>
        High = 1,
    }

    /// <summary>V2.50.1 : ce que fait une porte avec de l'eau (<see cref="DoorWater"/>) à cette marée, avec ce souffle.</summary>
    public enum WaterPass
    {
        /// <summary>À sec (porte sans eau, ou passage de marée à marée basse) : elle se passe à pied.</summary>
        Dry = 0,

        /// <summary>Noyée, mais le souffle suffit : elle se passe en plongée.</summary>
        Dive = 1,

        /// <summary>Noyée et sans plongée possible (passage de marée sans durée de plongée, à marée haute) : refusée, rien ne change.</summary>
        Drowned = 2,

        /// <summary>Noyée et la plongée dure plus que le souffle : refusée avant de plonger, rien ne change.</summary>
        NoBreath = 3,
    }

    /// <summary>
    /// V2.50.1 (grottes noyées du lac, donjon décidé au sujet 27) : marée et plongée, calculs purs. La marée vient de l'heure du monde
    /// (<see cref="WorldClock"/>, jamais d'une horloge réelle) : basse pendant <see cref="HalfCycleMinutes"/> minutes du monde, haute pendant
    /// autant, et ainsi de suite, la même chaque jour (basse de 0 h à 3 h, de 6 h à 9 h, de 12 h à 15 h, de 18 h à 21 h). Quand le lac est
    /// gelé (glace praticable, saisons), la glace retient l'eau : la marée reste basse. Le souffle du personnage vaut
    /// <see cref="BaseBreathMs"/>, plus <see cref="ShellBonusMs"/> s'il porte la <see cref="ShellItem"/> dans son sac. Décisions de Damien du
    /// 26 novembre 2026 : marée de 3 h, souffle de 15 s (30 s avec la coquille), refus avant de plonger s'il manque d'air, marée basse figée
    /// quand le lac gèle.
    /// </summary>
    public static class Tides
    {
        /// <summary>Durée d'une marée basse, puis d'une marée haute : 3 h du monde (22 min 30 s de jeu avec des journées de 3 h).</summary>
        public const int HalfCycleMinutes = 180;

        /// <summary>Souffle de base : 15 s sous l'eau.</summary>
        public const long BaseBreathMs = 15_000;

        /// <summary>Souffle en plus avec la coquille de souffle : 15 s (30 s en tout).</summary>
        public const long ShellBonusMs = 15_000;

        /// <summary>Objet qui donne du souffle, porté dans le sac (récompense du capitaine des pilleurs d'épaves).</summary>
        public const string ShellItem = "item.coquille_de_souffle";

        /// <summary>Marée à l'heure <paramref name="time"/> du monde ; basse quand le lac est gelé (<paramref name="frozen"/>).</summary>
        public static TideLevel At(WorldTime time, bool frozen) =>
            frozen || time.Minute / HalfCycleMinutes % 2 == 0 ? TideLevel.Low : TideLevel.High;

        /// <summary>
        /// Minutes du monde avant que la marée tourne, de 1 à <see cref="HalfCycleMinutes"/> ; null quand le lac est gelé (elle ne tourne
        /// plus).
        /// </summary>
        public static int? MinutesToTurn(WorldTime time, bool frozen) =>
            frozen ? (int?)null : HalfCycleMinutes - time.Minute % HalfCycleMinutes;

        /// <summary>Souffle (ms) d'un personnage qui porte <paramref name="shells"/> coquilles de souffle (une seule compte).</summary>
        /// <exception cref="ArgumentOutOfRangeException">Nombre négatif.</exception>
        public static long Breath(int shells)
        {
            if (shells < 0) throw new ArgumentOutOfRangeException(nameof(shells));
            return shells > 0 ? checked(BaseBreathMs + ShellBonusMs) : BaseBreathMs;
        }

        /// <summary>La porte d'eau <paramref name="water"/> (null : à sec) est noyée à cette marée.</summary>
        public static bool Flooded(DoorWater? water, TideLevel tide) => water != null && (!water.Tide || tide == TideLevel.High);

        /// <summary>
        /// Ce que fait la porte d'eau <paramref name="water"/> (null : à sec) à la marée <paramref name="tide"/>, avec un souffle de
        /// <paramref name="breathMs"/> : à sec, plongée, noyée sans plongée, ou souffle trop court (une plongée égale au souffle passe).
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Souffle négatif.</exception>
        public static WaterPass Check(DoorWater? water, TideLevel tide, long breathMs)
        {
            if (breathMs < 0) throw new ArgumentOutOfRangeException(nameof(breathMs));
            if (!Flooded(water, tide)) return WaterPass.Dry;
            if (!(water!.DiveMs is long dive)) return WaterPass.Drowned;
            return dive <= breathMs ? WaterPass.Dive : WaterPass.NoBreath;
        }
    }
}
