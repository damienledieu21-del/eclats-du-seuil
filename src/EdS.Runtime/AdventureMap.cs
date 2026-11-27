using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Space;
using EdS.Data.Zones;

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

        /// <summary>
        /// Chantier V2.46 : la porte mène dehors (une zone ni intérieure ni dessous) : dans une grotte, le rideau d'eau y est posé.
        /// </summary>
        public bool Outside { get; }

        /// <summary>
        /// Chantier V2.47.2 : genre de la porte (porte, escalier, puits…) : dans la mine, l'ascenseur est posé sur un puits, les échelles sur
        /// un escalier. Affichage seulement.
        /// </summary>
        public DoorKind Kind { get; }

        /// <summary>V2.50.1b : eau de la porte (passage noyé : marée, plongée), ou null. Affichage seulement (l'eau devant le passage).</summary>
        public DoorWater? Water { get; }

        public MapDoor(string id, string name, string? building, double x, double z, bool outside = false, DoorKind kind = DoorKind.Door, DoorWater? water = null)
        {
            Water = water;
            Outside = outside;
            Kind = kind;
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
        /// <summary>
        /// Sous-lot V2.28c : marge d'une fenêtre autour du carré d'un secteur (m). Elle couvre la session du secteur, marge de 32 m et
        /// hystérésis de 8 m comprises, et la petite carte (20 m autour du personnage) qui y reste toujours.
        /// </summary>
        public const int WindowMarginMeters = 64;

        /// <summary>
        /// Sous-lot V2.28c : la carte de l'aire entière dont celle-ci est une fenêtre (elle-même sinon). Le relief d'affichage et l'ombrage
        /// de l'image peinte se calculent sur elle : aucune couture d'une fenêtre à l'autre.
        /// </summary>
        public AdventureMap Whole
        {
            get => _whole ?? this;
            set => _whole = value is null || ReferenceEquals(value, this) ? null : value;
        }

        private AdventureMap? _whole;

        private BourgDress? _bourg;

        /// <summary>
        /// V2.30.2 (relecture indépendante des PR #195 à #199) : habillage du Bourg fortifié calculé une seule fois sur la carte de l'aire
        /// entière (<see cref="Whole"/>) : le même d'une fenêtre à l'autre (centre et faces de l'enceinte, portes, accessoires) ; une
        /// fenêtre n'en pose que ce qui la touche (<see cref="BourgDress.Near"/>).
        /// </summary>
        public BourgDress Bourg
        {
            get
            {
                AdventureMap whole = Whole;
                if (!ReferenceEquals(whole, this)) return whole.Bourg;
                return _bourg ??= BourgModels.Dress(Obstacles, Roads);
            }
        }

        private string? _fieldsKey;
        private IReadOnlyList<StyleField>? _fields;

        /// <summary>
        /// Revue Codex de la PR #166 (2e tour) : les champs cultivés de l'aire entière (<see cref="TownRules.Fields"/> d'après l'aire entière :
        /// les mêmes d'une fenêtre à l'autre, aucune couture), calculés une fois et gardés par la carte de l'aire entière (clé : routes et places
        /// dégagées).
        /// </summary>
        public IReadOnlyList<StyleField> AreaFields(IReadOnlyList<StyleRoad> roads)
        {
            if (roads is null) throw new ArgumentNullException(nameof(roads));
            AdventureMap whole = Whole;
            if (!ReferenceEquals(whole, this)) return whole.AreaFields(roads);
            long key = 17;
            foreach ((double cx, double cz) in Clearings) key = unchecked(key * 31 + (long)Math.Round(cx * 10) * 131 + (long)Math.Round(cz * 10));
            foreach (StyleRoad r in roads) key = unchecked(key * 31 + r.Id.GetHashCode());
            string k = key.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/" + roads.Count;
            if (_fields != null && _fieldsKey == k) return _fields;
            var boxes = Obstacles.Select(o => (o.Points.Min(p => p.X.Raw) / 10000.0, o.Points.Min(p => p.Z.Raw) / 10000.0,
                o.Points.Max(p => p.X.Raw) / 10000.0, o.Points.Max(p => p.Z.Raw) / 10000.0)).ToList();
            _fields = Places.Where(p => TownRules.Farmland(p.Id))
                .SelectMany(p => TownRules.Fields(p.MinX, p.MinZ, p.MaxX, p.MaxZ, boxes, roads, Clearings, (MinX, MinZ, MaxX, MaxZ))).ToList();
            _fieldsKey = k;
            return _fields;
        }

        /// <summary>Revue Codex de la PR #166 (2e tour) : les champs de l'aire entière qui touchent cette carte (une fenêtre ne construit que les siens).</summary>
        public IEnumerable<StyleField> WindowFields(IReadOnlyList<StyleRoad> roads) =>
            AreaFields(roads).Where(f => f.MaxX >= MinX && f.MinX <= MaxX && f.MaxZ >= MinZ && f.MinZ <= MaxZ);

        /// <summary>
        /// Relecture indépendante de la PR #166 : hystérésis de la fenêtre (m). La fenêtre d'un secteur reste tant que le personnage est à moins
        /// de cette distance de son carré : la petite carte (20 m autour de lui) y tient encore, et elle ne change qu'une fois le personnage
        /// bien avancé dans le secteur voisin (pas au passage, à 8 m du bord).
        /// </summary>
        public const int WindowKeepMeters = 40;

        /// <summary>Le point <paramref name="p"/> est-il à moins de <paramref name="meters"/> m du rectangle <paramref name="rect"/> (bornes brutes) ?</summary>
        public static bool Within((long MinX, long MinZ, long MaxX, long MaxZ) rect, GroundVector p, int meters)
        {
            long m = checked((long)meters * 10_000);
            return p.X.Raw >= rect.MinX - m && p.X.Raw <= rect.MaxX + m && p.Z.Raw >= rect.MinZ - m && p.Z.Raw <= rect.MaxZ + m;
        }

        /// <summary>Sous-lot V2.28c : côté d'une fenêtre (un secteur de 256 m et sa marge des deux côtés).</summary>
        public const int WindowMeters = RegionGrid.SectorMeters + 2 * WindowMarginMeters;

        /// <summary>
        /// Sous-lot V2.28c : l'aire dépasse-t-elle deux fenêtres (768 m) de côté ? La carte et le décor sont alors limités à la fenêtre du
        /// secteur du personnage (la surface étendue des Vallons) ; en deçà, ceux de l'aire entière, gardés d'un passage à l'autre (V2.28b (3)).
        /// </summary>
        public static bool Wide(CombatSpace area)
        {
            if (area is null) throw new ArgumentNullException(nameof(area));
            long side = checked(2L * WindowMeters * 10_000);
            return checked(area.Max.X.Raw - area.Min.X.Raw) > side || checked(area.Max.Z.Raw - area.Min.Z.Raw) > side;
        }

        /// <summary>
        /// Sous-lot V2.28c : fenêtre de l'aire autour du rectangle d'un secteur (bornes brutes) : le rectangle et
        /// <see cref="WindowMarginMeters"/> m autour, coupés aux bornes de l'aire ; les obstacles de l'aire dont la boîte la touche, dans leur
        /// ordre. Au plus <see cref="WindowMeters"/> m de côté : l'image peinte et le décor sont bornés quelle que soit l'aire.
        /// </summary>
        public static CombatSpace WindowOf(CombatSpace area, (long MinX, long MinZ, long MaxX, long MaxZ) sector)
        {
            if (area is null) throw new ArgumentNullException(nameof(area));
            long m = checked((long)WindowMarginMeters * 10_000);
            long minX = Math.Max(checked(sector.MinX - m), area.Min.X.Raw), minZ = Math.Max(checked(sector.MinZ - m), area.Min.Z.Raw);
            long maxX = Math.Min(checked(sector.MaxX + m), area.Max.X.Raw), maxZ = Math.Min(checked(sector.MaxZ + m), area.Max.Z.Raw);
            if (minX >= maxX || minZ >= maxZ) throw new ArgumentException("Secteur hors de l'aire.", nameof(sector));
            var obstacles = area.Obstacles.Where(o =>
                o.Points.Max(p => p.X.Raw) >= minX && o.Points.Min(p => p.X.Raw) <= maxX && o.Points.Max(p => p.Z.Raw) >= minZ && o.Points.Min(p => p.Z.Raw) <= maxZ);
            return new CombatSpace(new GroundVector(EdS.Domain.Numerics.Fixed.FromRaw(minX), EdS.Domain.Numerics.Fixed.FromRaw(minZ)),
                new GroundVector(EdS.Domain.Numerics.Fixed.FromRaw(maxX), EdS.Domain.Numerics.Fixed.FromRaw(maxZ)), obstacles);
        }

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

        /// <summary>
        /// Chantier V2.46 : la zone est un dessous (grotte, mine…) : à l'abri comme une pièce (ni pluie, ni neige, ni glace, pénombre), mais
        /// avec ses créatures et ses obstacles en rochers ; jamais un intérieur.
        /// </summary>
        public bool Underground { get; }

        /// <summary>
        /// Sous-lot V1.6 : PNJ qui parlent dans cette pièce (identifiant, place en mètres) : le décor des intérieurs leur donne un corps
        /// (<see cref="InteriorRules.Layout"/>). Vide dehors.
        /// </summary>
        public IReadOnlyList<(string Id, double X, double Z)> Speakers { get; }

        /// <summary>
        /// V2.25.2 : obstacles des passages de la zone (la porte du moulin), tels que la zone les définit, même ouverts (absents de
        /// <see cref="Obstacles"/>) : le décor y pose la barrière, fermée ou ouverte. Vide sans passage.
        /// </summary>
        public IReadOnlyList<SpaceObstacle> Gates { get; }

        /// <summary>
        /// Sous-lot V2.27 : grille de la région de la zone (cases de 128 m, schéma de zone 13), ou null (intérieur, arène, zone sans région) :
        /// le terrain de la région est alors chargé par cases autour de la caméra (<see cref="RegionTerrain"/>).
        /// </summary>
        public RegionGrid? Region { get; }

        /// <summary>
        /// V2.51 : la carte est celle de l'Envers d'une région (reflet de la surface : mêmes obstacles, autre lumière, autre ciel, matériaux
        /// sombres ; voir <see cref="EnversLook"/>).
        /// </summary>
        public bool Envers { get; }

        /// <summary>
        /// Sous-lot V2.28b (3) : espace d'où la carte est tirée (celui de la session, ou l'aire entière d'une zone à plusieurs secteurs) ;
        /// le décor (<c>SpaceView</c>) est construit sur lui, pas sur l'espace du secteur où est le personnage.
        /// </summary>
        public CombatSpace Space { get; }

        public AdventureMap(CombatSpace space, IEnumerable<MapPlace> places, IEnumerable<StyleRoad>? roads = null,
            IEnumerable<(double X, double Z)>? clearings = null, IEnumerable<MapDoor>? doors = null, bool indoor = false,
            IEnumerable<(string Id, double X, double Z)>? speakers = null, IEnumerable<SpaceObstacle>? gates = null, RegionGrid? region = null,
            bool underground = false, bool envers = false)
        {
            Envers = envers && !indoor;
            Region = region;
            Space = space;
            Gates = (gates ?? Array.Empty<SpaceObstacle>()).ToList();
            Speakers = (speakers ?? Array.Empty<(string, double, double)>()).ToList();
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
            Underground = underground && !indoor;
            var key = new System.Text.StringBuilder();
            if (Indoor) key.Append("intérieur:");
            if (Underground) key.Append("dessous:");
            if (Envers) key.Append("envers:");   // V2.51 : la même aire que la surface, mais un autre décor
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
            if (Region != null) key.Append(FormattableString.Invariant($"#région:{Region.OriginX}/{Region.OriginZ}/{Region.Columns}/{Region.Rows}"));
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
