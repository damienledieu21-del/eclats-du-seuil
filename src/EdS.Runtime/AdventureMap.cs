using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Space;

namespace EdS.Runtime
{
    /// <summary>Sorte d'un repère de la carte (sous-lot 12.9d).</summary>
    public enum MapMarkerKind
    {
        Player = 0,
        Npc = 1,
        SafePoint = 2,
        Workshop = 3,
        Node = 4,

        /// <summary>Où mène l'étape en cours de la mission (lieu à atteindre, PNJ à qui parler, créature à vaincre).</summary>
        Objective = 5,

        /// <summary>Intérieurs : une porte vers une autre zone (auberge, chapelle, maison ; dans un intérieur, la sortie).</summary>
        Door = 6,
    }

    /// <summary>Intérieurs : une porte de la carte (nom de ce qu'il y a derrière, bâtiment dont c'est la porte, place en mètres).</summary>
    public sealed class MapDoor
    {
        public string Id { get; }
        public string Name { get; }

        /// <summary>Obstacle (bâtiment) dont c'est la porte, ou null.</summary>
        public string? Building { get; }

        public double X { get; }
        public double Z { get; }

        public MapDoor(string id, string name, string? building, double x, double z)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Building = building;
            X = x;
            Z = z;
        }
    }

    /// <summary>Un repère de la carte : sorte, nom (traduit), position au sol en mètres.</summary>
    public sealed class MapMarker
    {
        public MapMarkerKind Kind { get; }
        public string Name { get; }
        public double X { get; }
        public double Z { get; }

        public MapMarker(MapMarkerKind kind, string name, double x, double z)
        {
            Kind = kind;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            X = x;
            Z = z;
        }
    }

    /// <summary>Un lieu nommé de la carte : nom et rectangle (mètres).</summary>
    public sealed class MapPlace
    {
        public string Id { get; }
        public string Name { get; }
        public double MinX { get; }
        public double MinZ { get; }
        public double MaxX { get; }
        public double MaxZ { get; }
        public double CenterX => (MinX + MaxX) / 2;
        public double CenterZ => (MinZ + MaxZ) / 2;

        public MapPlace(string id, string name, double minX, double minZ, double maxX, double maxZ)
        {
            Id = id;
            Name = name;
            MinX = minX;
            MinZ = minZ;
            MaxX = maxX;
            MaxZ = maxZ;
        }
    }

    /// <summary>
    /// Sous-lot 12.9d (essai de Damien du 25 octobre 2026 : « une mini carte en haut à droite qu'on puisse agrandir ») : la carte d'une zone,
    /// vue de dessus, nord en haut. Le décor (aire praticable, obstacles, rivière et murets en bleu) vient de l'espace de la session (une porte
    /// ouverte n'y est plus) ; l'image se calcule ici, sans Unity (<see cref="Render"/>), pour être vérifiée par les tests.
    /// </summary>
    public sealed class AdventureMap
    {
        public double MinX { get; }
        public double MinZ { get; }
        public double MaxX { get; }
        public double MaxZ { get; }
        public double Width => MaxX - MinX;
        public double Depth => MaxZ - MinZ;
        public IReadOnlyList<SpaceObstacle> Obstacles { get; }
        public IReadOnlyList<MapPlace> Places { get; }

        /// <summary>Vallons agrandis : routes et chemins de la zone (décor : on y marche comme ailleurs), vide sans routes.</summary>
        public IReadOnlyList<StyleRoad> Roads { get; }

        /// <summary>Vallons agrandis : places à garder dégagées des champs (PNJ, points sûrs, ateliers, nœuds de récolte), en mètres.</summary>
        public IReadOnlyList<(double X, double Z)> Clearings { get; }

        /// <summary>Intérieurs : portes de la zone (vide sans porte).</summary>
        public IReadOnlyList<MapDoor> Doors { get; }

        /// <summary>Intérieurs : la zone est un intérieur (une pièce : l'aire en est le sol, ses bords les murs).</summary>
        public bool Indoor { get; }

        public AdventureMap(CombatSpace space, IEnumerable<MapPlace> places, IEnumerable<StyleRoad>? roads = null,
            IEnumerable<(double X, double Z)>? clearings = null, IEnumerable<MapDoor>? doors = null, bool indoor = false)
        {
            if (space is null) throw new ArgumentNullException(nameof(space));
            MinX = Meters(space.Min.X.Raw);
            MinZ = Meters(space.Min.Z.Raw);
            MaxX = Meters(space.Max.X.Raw);
            MaxZ = Meters(space.Max.Z.Raw);
            Obstacles = space.Obstacles;
            Places = (places ?? Array.Empty<MapPlace>()).ToList();
            Roads = (roads ?? Array.Empty<StyleRoad>()).ToList();
            Clearings = (clearings ?? Array.Empty<(double X, double Z)>()).ToList();
            Doors = (doors ?? Array.Empty<MapDoor>()).ToList();
            Indoor = indoor;
            var key = new System.Text.StringBuilder();
            if (Indoor) key.Append("intérieur:");
            key.Append(FormattableString.Invariant($"{MinX}/{MinZ}/{MaxX}/{MaxZ}"));
            foreach (SpaceObstacle o in Obstacles)
            {
                key.Append('|').Append(o.Id).Append(o.Low ? "~" : ":");
                foreach (GroundVector p in o.Points) key.Append(p.X.Raw).Append(',').Append(p.Z.Raw).Append(';');
            }
            foreach (StyleRoad r in Roads)
            {
                key.Append('/').Append(r.Id).Append(r.Paved ? "#" : "=").Append(FormattableString.Invariant($"{r.Width}"));
                foreach ((double x, double z) in r.Points) key.Append(FormattableString.Invariant($"{x},{z};"));
            }
            foreach ((double x, double z) in Clearings) key.Append(FormattableString.Invariant($"o{x},{z}"));
            foreach (MapDoor d in Doors) key.Append(FormattableString.Invariant($"+{d.Id}@{d.Building}:{d.X},{d.Z}"));
            Signature = key.ToString();
        }

        /// <summary>
        /// Vallons agrandis : ce que la carte peinte montre (aire, obstacles, routes, places dégagées), en texte. Deux cartes de même signature
        /// donnent la même image : une halte ne la repeint pas (elle prend quelques secondes sur la grande carte) ; une porte ouverte la change.
        /// </summary>
        public string Signature { get; }

        private static double Meters(long raw) => raw / 10000.0;

        /// <summary>Position sur la carte, de 0 à 1 : <c>U</c> de l'ouest à l'est, <c>V</c> du nord (haut, 0) au sud (bas, 1).</summary>
        public (double U, double V) ToMap(double x, double z) => ((x - MinX) / Width, (MaxZ - z) / Depth);

        /// <summary>Couleurs de l'image (RVBA).</summary>
        public static readonly byte[] Ground = { 52, 74, 46, 255 }, Outside = { 20, 22, 20, 255 }, Solid = { 112, 98, 82, 255 }, Water = { 54, 104, 168, 255 };

        /// <summary>
        /// Image de la carte, <paramref name="width"/> × <paramref name="height"/> pixels (1 à 2048), en RVBA 8 bits, lignes du **sud au nord**
        /// (la première ligne est le bas de l'image, comme une texture Unity) : sol praticable, obstacles solides, obstacles bas (rivière) en
        /// bleu. Chaque pixel prend la couleur de son centre.
        /// </summary>
        public byte[] Render(int width, int height)
        {
            if (width < 1 || width > 2048) throw new ArgumentOutOfRangeException(nameof(width));
            if (height < 1 || height > 2048) throw new ArgumentOutOfRangeException(nameof(height));
            var pixels = new byte[checked(width * height * 4)];
            var shapes = Obstacles.Select(o => (Points: o.Points.Select(p => (X: Meters(p.X.Raw), Z: Meters(p.Z.Raw))).ToArray(), o.Low)).ToList();
            var boxes = shapes.Select(s => (MinX: s.Points.Min(p => p.X), MaxX: s.Points.Max(p => p.X), MinZ: s.Points.Min(p => p.Z), MaxZ: s.Points.Max(p => p.Z))).ToList();
            for (int row = 0; row < height; row++)
            {
                double z = MinZ + (row + 0.5) * Depth / height;
                for (int col = 0; col < width; col++)
                {
                    double x = MinX + (col + 0.5) * Width / width;
                    byte[] color = Ground;
                    for (int i = 0; i < shapes.Count; i++)
                    {
                        var b = boxes[i];
                        if (x < b.MinX || x > b.MaxX || z < b.MinZ || z > b.MaxZ || !Inside(shapes[i].Points, x, z)) continue;
                        color = shapes[i].Low ? Water : Solid;
                        if (!shapes[i].Low) break;   // un obstacle solide l'emporte sur l'eau
                    }
                    Buffer.BlockCopy(color, 0, pixels, (row * width + col) * 4, 4);
                }
            }
            return pixels;
        }

        /// <summary>Point dans un polygone convexe donné dans un sens ou dans l'autre (bords compris).</summary>
        public static bool Inside(IReadOnlyList<(double X, double Z)> points, double x, double z)
        {
            int sign = 0;
            for (int i = 0; i < points.Count; i++)
            {
                (double ax, double az) = points[i];
                (double bx, double bz) = points[(i + 1) % points.Count];
                double cross = (bx - ax) * (z - az) - (bz - az) * (x - ax);
                if (cross == 0) continue;
                int s = cross > 0 ? 1 : -1;
                if (sign == 0) sign = s;
                else if (s != sign) return false;
            }
            return true;
        }
    }
}
