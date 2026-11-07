using System;
using EdS.Domain.Numerics;
using EdS.Domain.Space;

namespace EdS.Simulation.Engine
{
    /// <summary>
    /// Sous-lot V2.8 (recherche de chemin) : les ennemis à comportement contournent le décor au lieu d'aller droit dans un mur.
    /// <list type="bullet">
    /// <item><b>Ligne droite libre</b> : rien ne change (même pas, même journal qu'avant pour ce pas).</item>
    /// <item><b>Ligne droite barrée</b> (approche d'une cible, place d'anneau d'une meute, errance, retour chez soi, file d'une harde) : chemin
    /// A* sur la grille de navigation de sa classe d'emprise (<see cref="Navigator"/>), lissé : il vise la case la plus lointaine qu'il
    /// atteint en ligne droite. Aucun chemin (but enfermé, autre rive sans passage) : il reste sur place (et se tourne vers sa cible).</item>
    /// <item><b>Fuite</b> (bête qui fuit, <c>flee</c>) : si la ligne droite pour s'éloigner est barrée (décor, bord de l'aire), elle essaie de
    /// biais (45°, puis 90°, à gauche d'abord) ; sinon elle fuit droit et glisse comme avant. Le recul d'un tireur ne change pas.</item>
    /// <item><b>Budget</b> : au plus <see cref="MaxPathQueriesPerTick"/> recherches de chemin par tick, dans un ordre qui tourne avec le tick
    /// (aucun combattant toujours servi le dernier) ; au-delà, il va droit et glisse (comme avant V2.8). Chaque recherche développe au plus
    /// <see cref="MaxPathExpanded"/> cases.</item>
    /// </list>
    /// Les réponses sont des fonctions pures de la géométrie, des positions du début de la phase et du tick : le cache des chemins et les
    /// grilles ne sont pas dans l'instantané (un rejeu ou une reprise les refait, avec les mêmes réponses).
    /// </summary>
    public sealed partial class CombatEngine
    {
        /// <summary>Recherches de chemin au plus par tick et par passe.</summary>
        public const int MaxPathQueriesPerTick = 8;

        /// <summary>Cases développées au plus par recherche.</summary>
        public const int MaxPathExpanded = 8_192;

        /// <summary>Chemins gardés au plus en cache.</summary>
        public const int PathCacheSize = 512;

        private Navigator? _navigator;
        private int _pathQueriesLeft;

        /// <summary>Mesures de la recherche de chemin (essai, tests de performance) : sans effet sur le combat.</summary>
        public long PathSearches => _navigator?.Searches ?? 0;
        public long PathExpanded => _navigator?.TotalExpanded ?? 0;
        public int PathLastExpanded => _navigator?.LastExpanded ?? 0;

        private void InitPaths()
        {
            if (_setup.Space is { } space) _navigator = Navigator.ForSpace(space, MaxPathExpanded, PathCacheSize);
        }

        /// <summary>Nouvelle passe de calcul des intentions : le budget de recherches repart.</summary>
        private void ResetPathBudget() => _pathQueriesLeft = MaxPathQueriesPerTick;

        /// <summary>
        /// Direction à prendre pour aller de la place de <paramref name="i"/> vers (gx, gz) : vrai et (ax, az) le point visé (le but si la ligne
        /// droite est libre ou si le budget du tick est épuisé, sinon un point de passage) ; faux s'il n'y a aucun chemin (rester sur place).
        /// <paramref name="stopX"/>, <paramref name="stopZ"/> : point où il s'arrêtera sur la ligne droite (la ligne n'est jugée que jusque-là).
        /// </summary>
        private bool SteerToward(int i, long[] x, long[] z, long gx, long gz, long stopX, long stopZ, out long ax, out long az)
        {
            ax = gx;
            az = gz;
            if (_navigator is null) return true;
            long r = _fightersById[i].Body!.Radius;
            if (_navigator.Clear(x[i], z[i], stopX, stopZ, r)) return true;
            if (_pathQueriesLeft <= 0) return true;
            _pathQueriesLeft--;
            NavSteer steer = _navigator.Steer(r, x[i], z[i], gx, gz, out ax, out az);
            if (steer == NavSteer.None) return false;
            if (ax == x[i] && az == z[i]) return false;
            return true;
        }

        private bool SteerToward(int i, long[] x, long[] z, long gx, long gz, out long ax, out long az) =>
            SteerToward(i, x, z, gx, gz, gx, gz, out ax, out az);

        /// <summary>Sous-pas d'essai d'un recul (au moins 1 m, au plus 4 m).</summary>
        private static readonly long RetreatProbeMin = Fixed.FromInt(1).Raw, RetreatProbeMax = Fixed.FromInt(4).Raw;

        /// <summary>Angles d'essai d'un recul barré, en demi-degrés : droit, 45° à gauche, à droite, 90° à gauche, à droite.</summary>
        private static readonly int[] RetreatAngles = { 0, 90, -90, 180, -180 };

        /// <summary>
        /// Recul (ou fuite) de <paramref name="i"/> dans la direction (ux, uz) (unitaire) : la première direction d'essai dont le sous-pas
        /// d'essai reste dans l'aire et ne touche aucun obstacle : vrai et (dx, dz) si c'est un biais ; faux pour garder la direction d'origine
        /// (libre, ou rien de libre : il glisse, comme avant V2.8).
        /// </summary>
        private bool RetreatDirection(int i, long[] x, long[] z, long ux, long uz, long wanted, out long dx, out long dz)
        {
            dx = ux;
            dz = uz;
            if (_navigator is null) return false;
            Body b = _fightersById[i].Body!;
            long probe = Math.Max(RetreatProbeMin, Math.Min(RetreatProbeMax, wanted));
            foreach (int angle in RetreatAngles)
            {
                Angles.Rotate(ux, uz, angle, out long rx, out long rz);
                long ex = checked(x[i] + rx * probe / Planar.Unit), ez = checked(z[i] + rz * probe / Planar.Unit);
                if (ex < checked(_minX + b.Radius) || ex > checked(_maxX - b.Radius) || ez < checked(_minZ + b.Radius) || ez > checked(_maxZ - b.Radius)) continue;
                if (!_navigator.Clear(x[i], z[i], ex, ez, b.Radius)) continue;
                if (angle == 0) return false;   // droit : la direction d'avant V2.8, à l'identique
                dx = rx;
                dz = rz;
                return true;
            }
            return false;
        }
    }
}
