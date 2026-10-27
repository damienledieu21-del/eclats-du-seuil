using System;
using System.Threading.Tasks;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Essai réaliste 6 (essai de Damien du 27 octobre 2026 : « la mini Map […] ça fait vraiment schéma ») : texture de la carte peinte
    /// d'une zone (<see cref="MapPainter"/>), calculée hors de l'image en cours (tâche de fond : le jeu ne se fige pas) puis envoyée à la
    /// carte graphique ; partagée par la petite carte et la grande (L). Null tant qu'elle se calcule.
    /// </summary>
    public static class PaintedMap
    {
        private static AdventureMap _source;
        private static Task<byte[]> _work;
        private static Texture2D _texture;
        private static int _width, _height;

        /// <summary>Texture de la carte <paramref name="map"/>, ou null si elle n'est pas encore prête (ou impossible).</summary>
        public static Texture2D For(AdventureMap map)
        {
            if (map == null) return null;
            if (!ReferenceEquals(map, _source))
            {
                _source = map;
                if (_texture != null) UnityEngine.Object.Destroy(_texture);
                _texture = null;
                (_width, _height) = MapPainter.Size(map);
                int w = _width, h = _height;
                _work = Task.Run(() => MapPainter.Paint(map, w, h));
            }
            if (_texture == null && _work != null && _work.IsCompleted)
            {
                if (_work.Exception != null) Debug.LogException(_work.Exception.GetBaseException());
                else
                {
                    _texture = new Texture2D(_width, _height, TextureFormat.RGBA32, false);
                    _texture.filterMode = FilterMode.Bilinear;
                    _texture.wrapMode = TextureWrapMode.Clamp;
                    _texture.LoadRawTextureData(_work.Result);
                    _texture.Apply(false);
                }
                _work = null;
            }
            return _texture;
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

        /// <summary>Oublie la carte (nouvelle aventure).</summary>
        public static void Forget()
        {
            _source = null;
            _work = null;
            if (_texture != null) UnityEngine.Object.Destroy(_texture);
            _texture = null;
        }
    }
}
