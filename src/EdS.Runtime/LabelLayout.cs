using System;
using System.Collections.Generic;

namespace EdS.Runtime
{
    /// <summary>
    /// Sous-lot V2.12.6 (essai du 9 novembre 2026 : à la forge, « Maëlle, la forgeronne » deux fois, « Forge de Maëlle », « Colombe… » et
    /// « Place du village » empilés et illisibles ; sur la carte, les noms du bourg l'un sur l'autre) : disposition des étiquettes, sans
    /// Unity. Une seule étiquette par clé (un PNJ) ou par texte ; les plus prioritaires d'abord ; une étiquette qui en recouvre une déjà
    /// posée est décalée d'un cran (au plus <c>maxShifts</c>) puis, toujours gênée, n'est pas montrée. Rectangles centrés, dans
    /// n'importe quel repère (le décalage <c>shiftStep</c> porte son signe). Réutilisable d'une image à l'autre sans allocation (une fois
    /// ses tampons à la bonne taille).
    /// </summary>
    public sealed class LabelLayout
    {
        private struct Item
        {
            public string? Key, Text;
            public double X, Y, W, H, Priority, Dy, WorldX, WorldZ;
            public bool Shown;
        }

        private Item[] _items = new Item[16];
        private int[] _order = new int[16];
        private int _count;

        /// <summary>Nombre d'étiquettes ajoutées depuis <see cref="Clear"/>.</summary>
        public int Count => _count;

        public void Clear() => _count = 0;

        /// <summary>
        /// Ajoute une étiquette de centre (<paramref name="x"/>, <paramref name="y"/>) et de taille <paramref name="width"/> ×
        /// <paramref name="height"/> ; rend son numéro. <paramref name="key"/> (l'identifiant d'un PNJ) ou, à défaut, le texte la rend
        /// unique.
        /// </summary>
        /// <remarks>
        /// Relecture de la PR #128 : deux clés différentes de même texte (deux « Villageois ») restent deux étiquettes, sauf à moins de
        /// <see cref="SameSpot"/> m l'une de l'autre dans le monde (<paramref name="worldX"/>, <paramref name="worldZ"/> ; NaN : inconnu).
        /// </remarks>
        public int Add(string? key, string? text, double x, double y, double width, double height, double priority, double worldX = double.NaN,
            double worldZ = double.NaN)
        {
            if (_count == _items.Length)
            {
                Array.Resize(ref _items, _count * 2);
                Array.Resize(ref _order, _count * 2);
            }
            _items[_count] = new Item
            {
                Key = key, Text = text, X = x, Y = y, W = Math.Max(0, width), H = Math.Max(0, height),
                Priority = double.IsNaN(priority) ? double.MinValue : priority, WorldX = worldX, WorldZ = worldZ,
            };
            return _count++;
        }

        /// <summary>
        /// Pose les étiquettes. Ordre : priorité décroissante, puis ordre d'ajout (stable). Une étiquette sans texte, ou de même clé ou de
        /// même texte qu'une plus prioritaire, n'est pas montrée.
        /// </summary>
        public void Solve(double shiftStep, int maxShifts = 1)
        {
            for (int i = 0; i < _count; i++) _order[i] = i;
            // Tri par insertion (quelques dizaines d'étiquettes au plus) : stable, sans allocation.
            for (int i = 1; i < _count; i++)
            {
                int v = _order[i], j = i - 1;
                while (j >= 0 && _items[_order[j]].Priority < _items[v].Priority)
                {
                    _order[j + 1] = _order[j];
                    j--;
                }
                _order[j + 1] = v;
            }
            for (int n = 0; n < _count; n++)
            {
                int i = _order[n];
                ref Item it = ref _items[i];
                it.Shown = false;
                it.Dy = 0;
                if (string.IsNullOrEmpty(it.Text)) continue;
                if (Duplicate(n, i)) continue;
                for (int s = 0; s <= Math.Max(0, maxShifts); s++)
                {
                    double dy = s * shiftStep;
                    if (Overlaps(n, it.X, it.Y + dy, it.W, it.H)) continue;
                    it.Shown = true;
                    it.Dy = dy;
                    break;
                }
            }
        }

        /// <summary>L'étiquette <paramref name="index"/> est-elle montrée (après <see cref="Solve"/>) ?</summary>
        public bool Shown(int index) => index >= 0 && index < _count && _items[index].Shown;

        /// <summary>Décalage de l'étiquette <paramref name="index"/> le long de l'axe vertical du repère (0 : à sa place).</summary>
        public double OffsetY(int index) => index >= 0 && index < _count ? _items[index].Dy : 0;

        private bool Duplicate(int n, int i)
        {
            ref Item it = ref _items[i];
            for (int m = 0; m < n; m++)
            {
                ref Item other = ref _items[_order[m]];
                if (string.IsNullOrEmpty(other.Text)) continue;
                if (it.Key != null && other.Key != null)
                {
                    if (string.Equals(it.Key, other.Key, StringComparison.Ordinal)) return true;
                    if (!string.Equals(it.Text, other.Text, StringComparison.Ordinal)) continue;
                    double dx = it.WorldX - other.WorldX, dz = it.WorldZ - other.WorldZ;
                    if (dx * dx + dz * dz < SameSpot * SameSpot) return true;   // NaN : faux, deux étiquettes
                    continue;
                }
                if (string.Equals(it.Text, other.Text, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>Distance (m) sous laquelle deux étiquettes de même texte et de clés différentes ne font qu'une.</summary>
        public const double SameSpot = 2.0;

        private bool Overlaps(int n, double x, double y, double w, double h)
        {
            for (int m = 0; m < n; m++)
            {
                ref Item o = ref _items[_order[m]];
                if (!o.Shown) continue;
                if (Math.Abs(x - o.X) * 2 < w + o.W && Math.Abs(y - (o.Y + o.Dy)) * 2 < h + o.H) return true;
            }
            return false;
        }

        /// <summary>Largeur approchée d'un texte (gras) : 0,56 fois la taille de police par caractère, plus une marge.</summary>
        public static double TextWidth(string? text, double fontSize) => ((text?.Length ?? 0) * 0.56 + 1) * Math.Max(0, fontSize);
    }
}
