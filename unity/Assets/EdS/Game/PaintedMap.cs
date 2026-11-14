using System;
using System.Threading.Tasks;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>Un ennemi à montrer sur la carte (en vue seulement : la carte ne montre pas ce que le Bastion ne voit pas).</summary>
    public struct MapEnemy
    {
        public float X, Z;
        public bool Boss;

        public MapEnemy(float x, float z, bool boss)
        {
            X = x;
            Z = z;
            Boss = boss;
        }
    }

    /// <summary>
    /// Essai réaliste 6 (essai de Damien du 27 octobre 2026 : « la mini Map […] ça fait vraiment schéma ») : texture de la carte peinte
    /// d'une zone (<see cref="MapPainter"/>), calculée hors de l'image en cours (tâche de fond : le jeu ne se fige pas) puis envoyée à la
    /// carte graphique ; partagée par la petite carte et la grande (L). Null tant qu'elle se calcule. Essai réaliste 6, étape 2 : l'ancienne
    /// carte en IMGUI (MiniMap, plan du cœur) est retirée ; une autre carte (nouvelle session, ou nouvelle aventure) refait l'image.
    /// </summary>
    public static class PaintedMap
    {
        /// <summary>Une image peinte : sa carte, son calcul en cours, sa texture.</summary>
        private sealed class Slot
        {
            public AdventureMap Source;
            public Task<byte[]> Work;
            public Texture2D Texture;
            public int Width, Height;
            public int Used;
        }

        /// <summary>
        /// Sous-lot V2.28c : au plus deux images à la fois (la fenêtre du secteur pour la petite carte, l'aire entière pour la grande ; une
        /// seule quand elles sont la même carte), chacune d'au plus 2 048 pixels de côté (<see cref="MapPainter.Size"/>) : 32 Mo au plus.
        /// </summary>
        private const int MaxSlots = 2;

        private static readonly System.Collections.Generic.List<Slot> _slots = new System.Collections.Generic.List<Slot>();
        private static int _clock;

        /// <summary>Texture de la carte <paramref name="map"/>, ou null si elle n'est pas encore prête (ou impossible).</summary>
        public static Texture2D For(AdventureMap map)
        {
            if (map == null) return null;
            _clock++;
            Slot slot = null;
            // Vallons agrandis : une nouvelle carte de même signature (une halte) garde l'image ; une porte ouverte la fait repeindre.
            foreach (Slot s in _slots)
                if (ReferenceEquals(s.Source, map) || s.Source.Signature == map.Signature)
                {
                    s.Source = map;
                    slot = s;
                    break;
                }
            if (slot == null)
            {
                if (_slots.Count >= MaxSlots)
                {
                    Slot oldest = _slots[0];
                    foreach (Slot s in _slots)
                        if (s.Used < oldest.Used) oldest = s;
                    if (oldest.Texture != null) UnityEngine.Object.Destroy(oldest.Texture);
                    _slots.Remove(oldest);
                }
                slot = new Slot { Source = map };
                (slot.Width, slot.Height) = MapPainter.Size(map);
                int w = slot.Width, h = slot.Height;
                slot.Work = Task.Run(() => MapPainter.Paint(map, w, h));
                _slots.Add(slot);
            }
            slot.Used = _clock;
            if (slot.Texture == null && slot.Work != null && slot.Work.IsCompleted)
            {
                if (slot.Work.Exception != null) Debug.LogException(slot.Work.Exception.GetBaseException());
                else
                {
                    slot.Texture = new Texture2D(slot.Width, slot.Height, TextureFormat.RGBA32, false);
                    slot.Texture.filterMode = FilterMode.Bilinear;
                    slot.Texture.wrapMode = TextureWrapMode.Clamp;
                    slot.Texture.LoadRawTextureData(slot.Work.Result);
                    slot.Texture.Apply(false, true);   // relecture des Vallons agrandis : pas de copie de l'image (7 Mo) gardée en mémoire
                }
                slot.Work = null;
            }
            return slot.Texture;
        }

        /// <summary>
        /// Partie de l'image (coordonnées de texture, origine en bas à gauche) centrée sur (<paramref name="x"/>, <paramref name="z"/>) et
        /// large de <paramref name="radius"/> mètres de chaque côté.
        /// </summary>
        public static Rect Window(AdventureMap map, double x, double z, double radius)
        {
            (double u, double v) = MapPainter.ToImage(map, x, z);
            double w = 2 * radius / (map.Width + 2 * MapPainter.Margin), h = 2 * radius / (map.Depth + 2 * MapPainter.Margin);
            return new Rect((float)(u - w / 2), (float)(1 - v - h / 2), (float)w, (float)h);
        }

        /// <summary>Partie de l'image qui couvre l'aire seule (sans la marge de forêt), pour la grande carte.</summary>
        public static Rect Area(AdventureMap map)
        {
            double m = MapPainter.Margin, w = map.Width + 2 * m, d = map.Depth + 2 * m;
            return new Rect((float)(m / w), (float)(m / d), (float)(map.Width / w), (float)(map.Depth / d));
        }

        /// <summary>Oublie les cartes (nouvelle aventure).</summary>
        public static void Forget()
        {
            foreach (Slot s in _slots)
                if (s.Texture != null) UnityEngine.Object.Destroy(s.Texture);
            _slots.Clear();
        }
    }
}
