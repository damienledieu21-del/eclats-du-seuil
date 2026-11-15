using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EdS.Runtime
{
    /// <summary>
    /// Sous-lot V2.28c, relecture indépendante de la PR #166 : les images peintes des cartes (petite carte, grande carte), calculées en tâche
    /// de fond, avec une mémoire bornée. Règles :
    /// <list type="bullet">
    /// <item>une carte de l'aire entière (<see cref="AdventureMap.Whole"/> elle-même), la plus chère, est gardée en priorité : au plus une ;
    /// une autre aire entière (autre zone) la remplace ;</item>
    /// <item>une fenêtre : la nouvelle se peint pendant que l'ancienne reste montrée (<see cref="Shown"/>) ; quand elle est prête, les
    /// fenêtres plus anciennes sont oubliées ; au plus <see cref="MaxWindows"/> fenêtres à la fois (la plus ancienne qui n'est pas prête est
    /// annulée en premier) ;</item>
    /// <item>une image oubliée en cours de calcul est annulée (son résultat est ignoré) ; <see cref="Evicted"/> rend la main à l'affichage
    /// pour libérer sa texture.</item>
    /// </list>
    /// Au plus trois images à la fois (une aire entière et deux fenêtres, pendant un passage), de 2 048 pixels de côté au plus : 48 Mo au plus
    /// en RVBA, 32 Mo hors passage.
    /// </summary>
    public sealed class MapImages
    {
        /// <summary>Une image : sa carte, sa taille, son calcul, et ce que l'affichage y attache (sa texture).</summary>
        public sealed class Entry
        {
            internal Entry(AdventureMap source, int width, int height, long created)
            {
                Source = source;
                Width = width;
                Height = height;
                Created = created;
            }

            public AdventureMap Source { get; internal set; }
            public int Width { get; }
            public int Height { get; }
            internal long Created { get; }
            internal Task<byte[]>? Work { get; set; }
            internal CancellationTokenSource Cancel { get; } = new CancellationTokenSource();

            /// <summary>
            /// Pixels peints (RVBA, comme <see cref="MapPainter.Paint(AdventureMap, int, int, double)"/>), null tant que le calcul n'est pas fini ;
            /// l'affichage les libère (<see cref="Release"/>) une fois sa texture faite.
            /// </summary>
            public byte[]? Pixels { get; internal set; }

            /// <summary>Ce que l'affichage y attache (une texture), libre.</summary>
            public object? Tag { get; set; }

            /// <summary>L'affichage a fait sa texture (<see cref="Tag"/>) : les pixels ne sont plus gardés.</summary>
            public void Release(object tag)
            {
                Tag = tag ?? throw new ArgumentNullException(nameof(tag));
                Pixels = null;
            }

            /// <summary>L'image est-elle prête ?</summary>
            public bool Ready => Pixels != null || Tag != null;

            internal bool IsWindow => !ReferenceEquals(Source.Whole, Source);
        }

        /// <summary>Fenêtres gardées à la fois au plus (l'ancienne, montrée pendant que la nouvelle se peint).</summary>
        public const int MaxWindows = 2;

        private readonly List<Entry> _entries = new List<Entry>();
        private readonly Func<AdventureMap, int, int, CancellationToken, Task<byte[]>> _paint;
        private long _clock;

        /// <param name="paint">Le calcul d'une image (par défaut : <see cref="MapPainter.Paint(AdventureMap, int, int, double, CancellationToken)"/> en tâche de fond).</param>
        public MapImages(Func<AdventureMap, int, int, CancellationToken, Task<byte[]>>? paint = null)
        {
            _paint = paint ?? ((map, w, h, cancel) => Task.Run(() => MapPainter.Paint(map, w, h, MapPainter.Margin, cancel), cancel));
        }

        /// <summary>Appelé pour chaque image oubliée (sa texture est à libérer).</summary>
        public event Action<Entry>? Evicted;

        /// <summary>Images gardées.</summary>
        public IReadOnlyList<Entry> Entries => _entries;

        /// <summary>L'image de <paramref name="map"/> (même carte ou même signature), demandée si elle ne l'est pas encore ; les calculs finis sont relevés.</summary>
        public Entry For(AdventureMap map)
        {
            if (map is null) throw new ArgumentNullException(nameof(map));
            Collect();
            Entry? found = Find(map);
            if (found != null)
            {
                found.Source = map;
                return found;
            }
            var entry = new Entry(map, MapPainter.Size(map).Width, MapPainter.Size(map).Height, ++_clock);
            if (!entry.IsWindow)
            {
                foreach (Entry old in _entries.ToArray())
                    if (!old.IsWindow) Evict(old);   // une seule aire entière
            }
            else
            {
                // Une nouvelle fenêtre : au plus MaxWindows. La plus ancienne qui n'est pas prête part d'abord, sinon la plus ancienne.
                while (Windows() >= MaxWindows) Evict(Oldest(notReady: true) ?? Oldest(notReady: false)!);
            }
            entry.Work = _paint(map, entry.Width, entry.Height, entry.Cancel.Token);
            _entries.Add(entry);
            Collect();
            return entry;
        }

        /// <summary>
        /// L'image à montrer pour <paramref name="map"/> : la sienne si elle est prête ; sinon, pour une fenêtre, la plus récente fenêtre prête
        /// (l'ancienne, pendant que la nouvelle se peint), puis l'aire entière prête ; sinon null. La carte de l'image rendue est sa
        /// <see cref="Entry.Source"/> (à utiliser pour placer l'image).
        /// </summary>
        public Entry? Shown(AdventureMap map)
        {
            Entry wanted = For(map);
            if (wanted.Ready) return wanted;
            Entry? best = null;
            foreach (Entry e in _entries)
                if (e.Ready && e.IsWindow && (best is null || e.Created > best.Created)) best = e;
            if (best != null) return best;
            foreach (Entry e in _entries)
                if (e.Ready && !e.IsWindow && e.Source.Signature == map.Whole.Signature) return e;
            return null;
        }

        /// <summary>Oublie toutes les images (nouvelle aventure).</summary>
        public void Clear()
        {
            foreach (Entry e in _entries.ToArray()) Evict(e);
        }

        private Entry? Find(AdventureMap map)
        {
            foreach (Entry e in _entries)
                if (ReferenceEquals(e.Source, map) || e.Source.Signature == map.Signature) return e;
            return null;
        }

        private int Windows()
        {
            int n = 0;
            foreach (Entry e in _entries)
                if (e.IsWindow) n++;
            return n;
        }

        private Entry? Oldest(bool notReady)
        {
            Entry? oldest = null;
            foreach (Entry e in _entries)
                if (e.IsWindow && (!notReady || !e.Ready) && (oldest is null || e.Created < oldest.Created)) oldest = e;
            return oldest;
        }

        /// <summary>Relève les calculs finis ; une fenêtre prête fait oublier les fenêtres plus anciennes.</summary>
        private void Collect()
        {
            foreach (Entry e in _entries.ToArray())
            {
                if (e.Work is null || !e.Work.IsCompleted) continue;
                if (e.Work.Status == TaskStatus.RanToCompletion) e.Pixels = e.Work.Result;
                e.Work = null;
            }
            Entry? newest = null;
            foreach (Entry e in _entries)
                if (e.IsWindow && e.Ready && (newest is null || e.Created > newest.Created)) newest = e;
            if (newest != null)
                foreach (Entry e in _entries.ToArray())
                    if (e.IsWindow && e.Created < newest.Created) Evict(e);
        }

        private void Evict(Entry e)
        {
            _entries.Remove(e);
            e.Cancel.Cancel();
            e.Work = null;
            e.Pixels = null;
            Evicted?.Invoke(e);
        }
    }
}
