using System;
using System.Collections.Generic;

namespace EdS.Runtime
{
    /// <summary>
    /// V2.59 (suite de l'audit des performances, docs/PERFS.md) : construction progressive du Bourg. Les pièces (maisons, palissade,
    /// sols) ne sont plus toutes posées d'un bloc au chargement de la zone : elles le sont par lots à chaque image, les plus proches de la
    /// caméra d'abord, dans un budget de temps (comme les cases du décor, <c>CellStreamView</c>). Règles pures, sans Unity, testées.
    /// </summary>
    public static class BourgBuild
    {
        /// <summary>Pièces posées au plus par image, même si le budget de temps n'est pas atteint.</summary>
        public const int MaxPerFrame = 32;

        /// <summary>
        /// Ordre de pose des pièces placées en <paramref name="spots"/> (x, z) vues d'une caméra en (<paramref name="cameraX"/>,
        /// <paramref name="cameraZ"/>) : de la plus proche à la plus éloignée (carré de la distance horizontale), à égalité dans l'ordre
        /// des indices. Une place non finie passe en dernier. Rend une permutation des indices.
        /// </summary>
        public static int[] Order(IReadOnlyList<(double X, double Z)> spots, double cameraX, double cameraZ)
        {
            if (spots == null) throw new ArgumentNullException(nameof(spots));
            int n = spots.Count;
            var keys = new double[n];
            var order = new int[n];
            for (int i = 0; i < n; i++)
            {
                double dx = spots[i].X - cameraX, dz = spots[i].Z - cameraZ;
                double d = dx * dx + dz * dz;
                keys[i] = double.IsNaN(d) ? double.PositiveInfinity : d;
                order[i] = i;
            }
            Array.Sort(order, (a, b) =>
            {
                int c = keys[a].CompareTo(keys[b]);
                return c != 0 ? c : a.CompareTo(b);
            });
            return order;
        }

        /// <summary>
        /// Poser une pièce de plus dans cette image, après <paramref name="builtThisFrame"/> pièces et <paramref name="elapsedMs"/> ms ?
        /// Toujours la première (la construction avance à chaque image), puis tant que le budget (<paramref name="budgetMs"/>) n'est pas
        /// atteint et que <see cref="MaxPerFrame"/> n'est pas dépassé. Un temps non fini arrête le lot.
        /// </summary>
        public static bool Continue(int builtThisFrame, double elapsedMs, double budgetMs)
        {
            if (builtThisFrame <= 0) return true;
            if (builtThisFrame >= MaxPerFrame) return false;
            return elapsedMs < budgetMs;
        }
    }
}
