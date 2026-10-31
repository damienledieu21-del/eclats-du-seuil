using System;
using System.Collections.Generic;
using System.Linq;

namespace EdS.Runtime
{
    /// <summary>Intérieurs : genre d'une pièce (d'après son lieu : auberge, chapelle, sinon maison).</summary>
    public enum InteriorStyle
    {
        House = 0,
        Tavern = 1,
        Chapel = 2,
    }

    /// <summary>Intérieurs : ce que représente un obstacle d'un intérieur, d'après le début de son identifiant.</summary>
    public enum InteriorFurniture
    {
        None = 0,
        Counter = 1,
        Barrel = 2,
        Hearth = 3,
        Table = 4,
        Stairs = 5,
        Altar = 6,
        Pew = 7,
        Candelabra = 8,
        Bed = 9,
        Chest = 10,
        Shelf = 11,
        Crates = 12,
        Cabinet = 13,
        SpinningWheel = 14,
        Statue = 15,
    }

    /// <summary>
    /// Intérieurs : un objet posé (meuble, vaisselle, décor), dont le modèle vient du catalogue (modèles de Poly Haven rangés dans
    /// <c>Assets/Style/PolyHaven/Interieurs</c>, reconnus par leur nom : <see cref="InteriorRules.PropOf"/>) ; sans modèle, une forme
    /// simple le remplace.
    /// </summary>
    public enum InteriorProp
    {
        None = 0,
        Table, Bench, Stool, Chair, Throne, Bed, Cabinet, Commode, Shelves, Drawers, Chest, Crate, Barrel, Bucket,
        Lantern, HangingLantern, Chandelier, Candlestick, Candelabra,
        Bowl, Plate, Goblets, Pot, Bottles, ClayPot, Basket, CuttingBoard, Spoon, Apple, Onion,
        Shield, Sword, WarHammer, Axe, Hatchet, Broom, Painting, SpinningWheel, Door, Statue,
    }

    /// <summary>Intérieurs : sorte d'une lumière (feu de cheminée, bougie, lanterne, jour d'une fenêtre).</summary>
    public enum InteriorLightKind
    {
        Fire = 0,
        Candle = 1,
        Lantern = 2,
        Window = 3,
    }

    /// <summary>
    /// Intérieurs : un objet posé. Sur le sol ou à la hauteur <see cref="Y"/> (<see cref="Parent"/> &lt; 0), ou sur le dessus de l'objet
    /// <see cref="Parent"/> (rang dans la liste). Mis à l'échelle pour que son plus grand côté au sol fasse <see cref="Fit"/> mètres, ou, si
    /// <see cref="Fit"/> est nul, pour que sa hauteur fasse <see cref="Height"/> mètres. Accroché à un mur : <see cref="OnWall"/> (dos au mur,
    /// face vers <see cref="Yaw"/>).
    /// </summary>
    public sealed class InteriorPiece
    {
        public InteriorPiece(string id, InteriorProp prop, double x, double z, double yaw, double fit, double height = 0, double y = 0, int parent = -1,
            bool onWall = false)
        {
            Id = id;
            Prop = prop;
            X = x;
            Z = z;
            Yaw = yaw;
            Fit = fit;
            Height = height;
            Y = y;
            Parent = parent;
            OnWall = onWall;
        }

        public string Id { get; }
        public InteriorProp Prop { get; }
        public double X { get; }
        public double Z { get; }

        /// <summary>Orientation (degrés, 0 : face vers +z).</summary>
        public double Yaw { get; }

        public double Fit { get; }
        public double Height { get; }
        public double Y { get; }
        public int Parent { get; }
        public bool OnWall { get; }
    }

    /// <summary>Intérieurs : une lumière (place en mètres, portée, intensité).</summary>
    public sealed class InteriorLight
    {
        public InteriorLight(InteriorLightKind kind, double x, double y, double z, double range, double intensity)
        {
            Kind = kind;
            X = x;
            Y = y;
            Z = z;
            Range = range;
            Intensity = intensity;
        }

        public InteriorLightKind Kind { get; }
        public double X { get; }
        public double Y { get; }
        public double Z { get; }
        public double Range { get; }
        public double Intensity { get; }
    }

    /// <summary>Intérieurs : une fenêtre dans un mur (centre au sol, mur : côté de la pièce, largeur, bas et haut en mètres).</summary>
    public sealed class InteriorWindow
    {
        public InteriorWindow(double x, double z, (int X, int Z) wall, double width, double bottom, double top)
        {
            X = x;
            Z = z;
            Wall = wall;
            Width = width;
            Bottom = bottom;
            Top = top;
        }

        public double X { get; }
        public double Z { get; }
        public (int X, int Z) Wall { get; }
        public double Width { get; }
        public double Bottom { get; }
        public double Top { get; }
    }

    /// <summary>Intérieurs : l'aménagement d'une pièce (genre, hauteur des murs, objets, lumières, fenêtres, poutres, villageois).</summary>
    public sealed class InteriorLayout
    {
        internal InteriorLayout(InteriorStyle style, double wallHeight, IReadOnlyList<InteriorPiece> pieces, IReadOnlyList<InteriorLight> lights,
            IReadOnlyList<InteriorWindow> windows, IReadOnlyList<double> beams, bool beamsAlongX, IReadOnlyList<StyleVillager> villagers)
        {
            Style = style;
            WallHeight = wallHeight;
            Pieces = pieces;
            Lights = lights;
            Windows = windows;
            Beams = beams;
            BeamsAlongX = beamsAlongX;
            Villagers = villagers;
        }

        public InteriorStyle Style { get; }

        /// <summary>Hauteur des murs (m).</summary>
        public double WallHeight { get; }

        public IReadOnlyList<InteriorPiece> Pieces { get; }
        public IReadOnlyList<InteriorLight> Lights { get; }
        public IReadOnlyList<InteriorWindow> Windows { get; }

        /// <summary>Poutres du plafond : leur place (m) le long de l'axe qu'elles coupent.</summary>
        public IReadOnlyList<double> Beams { get; }

        /// <summary>Les poutres courent le long de x (elles sont placées en z) ; sinon le long de z.</summary>
        public bool BeamsAlongX { get; }

        /// <summary>Villageois d'affichage de la pièce (aubergiste, clients, fidèle, habitant).</summary>
        public IReadOnlyList<StyleVillager> Villagers { get; }
    }

    /// <summary>
    /// Intérieurs (choix de Damien du 28 octobre 2026 : « on fait la façon 1 », puis « je veux vraiment de beaux intérieurs bien détaillés,
    /// le mieux que tu puisses faire ») : règles sans Unity qui aménagent une pièce à partir de son espace. Chaque obstacle est un meuble
    /// (comptoir, tables et bancs, tonneaux, cheminée, escalier, autel, bancs d'église, lit, coffre, étagères…) ; sur et autour d'eux, la
    /// vaisselle et les objets ; aux murs, armes, bouclier, tableaux, lanternes et bougies ; au plafond, poutres et lustres ; des fenêtres
    /// dans les murs ; quelques villageois. Toujours le même aménagement pour la même pièce. Affichage seulement : le moteur ne connaît que
    /// les obstacles.
    /// </summary>
    public static class InteriorRules
    {
        /// <summary>Hauteur (m) du dessus d'un comptoir, d'un autel, du linteau d'une cheminée faits par le code.</summary>
        public const double CounterHeight = 1.05, AltarHeight = 1.0, MantelHeight = 1.45;

        /// <summary>
        /// Revue Codex de la PR #67 : places des montants d'un comptoir de longueur <paramref name="length"/> (m), le long de son grand
        /// côté : à 0,1 m des bouts et environ tous les 0,9 m entre les deux, en nombre entier borné (65 au plus). Un comptoir de 0,2 m
        /// ou moins n'a qu'un montant, au milieu (un pas nul faisait tourner la boucle sans fin) ; une longueur qui n'est pas un nombre
        /// positif n'en a aucun.
        /// </summary>
        public static IReadOnlyList<double> CounterPosts(double length)
        {
            if (double.IsNaN(length) || double.IsInfinity(length) || length <= 0) return Array.Empty<double>();
            if (length <= 0.2) return new[] { length / 2 };
            int gaps = (int)Math.Min(64, Math.Max(1, Math.Round((length - 0.2) / 0.9)));
            var posts = new double[gaps + 1];
            for (int i = 0; i <= gaps; i++) posts[i] = 0.1 + (length - 0.2) * i / gaps;
            return posts;
        }

        /// <summary>Hauteur (m) où sont accrochés les objets aux murs.</summary>
        public const double WallDecorHeight = 1.75;

        /// <summary>Lumières d'une pièce, au plus (le rendu en dessine un nombre limité par objet).</summary>
        public const int MaxLights = 7;

        /// <summary>Genre d'une pièce d'après ses lieux (« auberge », « chapelle », sinon maison).</summary>
        public static InteriorStyle StyleOf(AdventureMap map)
        {
            string key = string.Concat((map?.Places ?? Array.Empty<MapPlace>()).Select(p => StyleRules.Key(p.Id)));
            if (key.Contains("auberge") || key.Contains("taverne")) return InteriorStyle.Tavern;
            if (key.Contains("chapelle") || key.Contains("eglise")) return InteriorStyle.Chapel;
            return InteriorStyle.House;
        }

        private static readonly (string Prefix, InteriorFurniture Kind)[] FurnitureWords =
        {
            ("comptoir", InteriorFurniture.Counter), ("tonneau", InteriorFurniture.Barrel), ("cheminee", InteriorFurniture.Hearth),
            ("table", InteriorFurniture.Table), ("escalier", InteriorFurniture.Stairs), ("autel", InteriorFurniture.Altar),
            ("banc", InteriorFurniture.Pew), ("chandelier", InteriorFurniture.Candelabra), ("lit", InteriorFurniture.Bed),
            ("coffre", InteriorFurniture.Chest), ("etagere", InteriorFurniture.Shelf), ("caisse", InteriorFurniture.Crates),
            ("armoire", InteriorFurniture.Cabinet), ("rouet", InteriorFurniture.SpinningWheel), ("statue", InteriorFurniture.Statue),
        };

        /// <summary>Meuble que représente l'obstacle <paramref name="id"/> d'un intérieur (début de son identifiant), ou <see cref="InteriorFurniture.None"/>.</summary>
        public static InteriorFurniture FurnitureFor(string id)
        {
            string key = StyleRules.Key(id);
            foreach ((string prefix, InteriorFurniture kind) in FurnitureWords)
                if (key.StartsWith(prefix, StringComparison.Ordinal)) return kind;
            return InteriorFurniture.None;
        }

        /// <summary>
        /// Mots des modèles (nom du fichier, réduit), dans l'ordre où ils sont essayés : le premier qui va l'emporte (« WoodenTable_01 »,
        /// une table basse et usée, sert de banc ; « WoodenChair_01 », gothique, de trône du prêtre ; « ceramic_pot » est une jarre). Un mot
        /// qui commence par « ^ » doit ouvrir le nom (« small_wooden_table_01 » reste une table).
        /// </summary>
        private static readonly (string Word, InteriorProp Prop)[] PropWords =
        {
            ("^woodentable01", InteriorProp.Bench), ("bench", InteriorProp.Bench), ("^woodenchair01", InteriorProp.Throne), ("throne", InteriorProp.Throne),
            ("table", InteriorProp.Table), ("stool", InteriorProp.Stool), ("chair", InteriorProp.Chair), ("bed", InteriorProp.Bed),
            ("commode", InteriorProp.Commode), ("cabinet", InteriorProp.Cabinet), ("drawer", InteriorProp.Drawers), ("shel", InteriorProp.Shelves),
            ("chest", InteriorProp.Chest), ("crate", InteriorProp.Crate), ("barrel", InteriorProp.Barrel), ("bucket", InteriorProp.Bucket),
            ("lanternchandelier", InteriorProp.HangingLantern), ("chandelier", InteriorProp.Chandelier), ("lantern", InteriorProp.Lantern),
            ("candleholder", InteriorProp.Candelabra), ("candelabr", InteriorProp.Candelabra), ("candlestick", InteriorProp.Candlestick),
            ("bowl", InteriorProp.Bowl), ("plate", InteriorProp.Plate), ("goblet", InteriorProp.Goblets), ("ceramicpot", InteriorProp.ClayPot),
            ("pot", InteriorProp.Pot), ("bottle", InteriorProp.Bottles), ("basket", InteriorProp.Basket), ("cuttingboard", InteriorProp.CuttingBoard),
            ("spoon", InteriorProp.Spoon), ("apple", InteriorProp.Apple), ("onion", InteriorProp.Onion), ("shield", InteriorProp.Shield),
            ("estoc", InteriorProp.Sword), ("sword", InteriorProp.Sword), ("warhammer", InteriorProp.WarHammer), ("hatchet", InteriorProp.Hatchet),
            ("axe", InteriorProp.Axe), ("broom", InteriorProp.Broom), ("pictureframe", InteriorProp.Painting), ("painting", InteriorProp.Painting),
            ("spinningwheel", InteriorProp.SpinningWheel), ("door", InteriorProp.Door), ("statue", InteriorProp.Statue),
        };

        /// <summary>Le fichier est-il un objet d'intérieur (rangé dans un dossier « Interieurs ») ?</summary>
        public static bool IsInterior(string path) =>
            ("/" + (path ?? string.Empty).Replace('\\', '/').ToLowerInvariant() + "/").Contains("/interieurs/");

        /// <summary>Objet d'intérieur que représente le modèle <paramref name="path"/> (nom du fichier), ou <see cref="InteriorProp.None"/>.</summary>
        public static InteriorProp PropOf(string path)
        {
            string file = StyleRules.Key(System.IO.Path.GetFileNameWithoutExtension((path ?? string.Empty).Replace('\\', '/')));
            foreach ((string word, InteriorProp prop) in PropWords)
                if (word[0] == '^' ? file.StartsWith(word.Substring(1), StringComparison.Ordinal) : file.Contains(word)) return prop;
            return InteriorProp.None;
        }

        /// <summary>
        /// Rotation ajoutée par défaut (degrés) au modèle d'un objet, pour que sa face regarde +z une fois importé (réglable ensuite dans le
        /// catalogue) : 0 tant que l'essai n'a rien montré d'autre.
        /// </summary>
        public static double DefaultYaw(InteriorProp prop) => 0;

        /// <summary>Hauteur des murs (m) d'une pièce de ce genre.</summary>
        public static double WallHeight(InteriorStyle style) => style == InteriorStyle.Chapel ? 6.5 : style == InteriorStyle.Tavern ? 3.6 : 3.0;

        private sealed class Box
        {
            public string Id = string.Empty;
            public InteriorFurniture Kind;
            public double MinX, MinZ, MaxX, MaxZ;
            public double CX => (MinX + MaxX) / 2;
            public double CZ => (MinZ + MaxZ) / 2;
            public double W => MaxX - MinX;
            public double D => MaxZ - MinZ;
            public bool AlongX => W >= D;
            public double Long => Math.Max(W, D);
            public double Short => Math.Min(W, D);
        }

        /// <summary>
        /// Aménagement d'une pièce (<paramref name="map"/> d'un intérieur). Mêmes données, même aménagement : chaque variante est tirée de
        /// l'identifiant de l'objet (<see cref="StyleRules.Variant"/>).
        /// </summary>
        public static InteriorLayout Layout(AdventureMap map)
        {
            if (map is null) throw new ArgumentNullException(nameof(map));
            InteriorStyle style = StyleOf(map);
            double height = WallHeight(style);
            var boxes = map.Obstacles.Select(o => new Box
            {
                Id = o.Id, Kind = FurnitureFor(o.Id),
                MinX = o.Points.Min(p => p.X.Raw) / 10000.0, MinZ = o.Points.Min(p => p.Z.Raw) / 10000.0,
                MaxX = o.Points.Max(p => p.X.Raw) / 10000.0, MaxZ = o.Points.Max(p => p.Z.Raw) / 10000.0,
            }).ToList();
            var pieces = new List<InteriorPiece>();
            var lights = new List<InteriorLight>();
            double cx = (map.MinX + map.MaxX) / 2, cz = (map.MinZ + map.MaxZ) / 2;

            int Add(InteriorPiece piece)
            {
                pieces.Add(piece);
                return pieces.Count - 1;
            }

            void Light(InteriorLightKind kind, double x, double y, double z, double range, double intensity)
            {
                if (lights.Count < MaxLights) lights.Add(new InteriorLight(kind, x, y, z, range, intensity));
            }

            // Côté du mur le plus proche d'une boîte (le meuble y est adossé) : vecteur vers l'intérieur de la pièce.
            (int X, int Z) Back(Box b)
            {
                double w = b.MinX - map.MinX, e = map.MaxX - b.MaxX, s = b.MinZ - map.MinZ, n = map.MaxZ - b.MaxZ;
                double min = Math.Min(Math.Min(w, e), Math.Min(s, n));
                return min == s ? (0, 1) : min == n ? (0, -1) : min == w ? (1, 0) : (-1, 0);
            }

            double Yaw((int X, int Z) toward) => Math.Atan2(toward.X, toward.Z) * 180 / Math.PI;
            double AxisYaw(Box b) => b.AlongX ? 0 : 90;

            // Le feu de la cheminée d'abord : la lumière principale.
            foreach (Box b in boxes.Where(b => b.Kind == InteriorFurniture.Hearth))
            {
                (int X, int Z) f = Back(b);
                double fx = b.CX + f.X * (b.Short / 2 + 0.35), fz = b.CZ + f.Z * (b.Short / 2 + 0.35);
                Light(InteriorLightKind.Fire, fx, 0.6, fz, style == InteriorStyle.Tavern ? 10 : 7.5, 3.2);
                Add(new InteriorPiece(b.Id + "/marmite", InteriorProp.Pot, b.CX + f.X * 0.05, b.CZ + f.Z * 0.05, Yaw(f), 0.42, y: 0.18));
                // Hachette posée au pied d'un bout de la cheminée, juste à côté du jambage (relecture : pas dedans, ni sur les bûches).
                (int X, int Z) along = (f.Z, f.X);
                double end = b.Long / 2 + 0.25;
                Add(new InteriorPiece(b.Id + "/hachette", InteriorProp.Hatchet, b.CX + along.X * end + f.X * 0.2, b.CZ + along.Z * end + f.Z * 0.2,
                    Yaw(f) + 20, 0, height: 0.32, y: 0.0));
            }

            // Au plafond : lanternes suspendues (auberge, maison), lustres (chapelle), le long du milieu de la pièce. Leurs lumières passent
            // avant celles des meubles et des murs (relecture : au-delà de MaxLights, seules des bougies murales restent sans lumière).
            bool alongX = map.Width >= map.Depth;
            double length = alongX ? map.Width : map.Depth;
            int hanging = style == InteriorStyle.House ? 1 : 2;
            for (int i = 0; i < hanging; i++)
            {
                double t = -length / 2 + length * (i + 0.5) / hanging;
                double x = alongX ? cx + t : cx, z = alongX ? cz : cz + t;
                InteriorProp prop = style == InteriorStyle.Chapel ? InteriorProp.Chandelier : InteriorProp.HangingLantern;
                Add(new InteriorPiece($"plafond/{i}", prop, x, z, 0, 0, height: style == InteriorStyle.Chapel ? 1.2 : 0.9,
                    y: height - (style == InteriorStyle.Chapel ? 2.4 : 1.25)));
                Light(style == InteriorStyle.Chapel ? InteriorLightKind.Candle : InteriorLightKind.Lantern, x, height - (style == InteriorStyle.Chapel ? 2.2 : 1.1), z,
                    style == InteriorStyle.Chapel ? 9 : 7, style == InteriorStyle.Chapel ? 1.8 : 1.5);
            }

            foreach (Box b in boxes)
            {
                (int X, int Z) f = Back(b);
                int v = StyleRules.Variant(b.Id, 1000);
                switch (b.Kind)
                {
                    case InteriorFurniture.Table:
                    {
                        int table = Add(new InteriorPiece(b.Id, InteriorProp.Table, b.CX, b.CZ, AxisYaw(b) + (v % 2 == 0 ? 0 : 180),
                            style == InteriorStyle.Tavern ? b.Long * 0.92 : b.Long * 0.95));
                        double across = b.Short / 2 - 0.2;
                        (double ax, double az) = b.AlongX ? (0.0, 1.0) : (1.0, 0.0);
                        (double lx, double lz) = b.AlongX ? (1.0, 0.0) : (0.0, 1.0);
                        if (style == InteriorStyle.Tavern && b.Short >= 1.4)
                        {
                            Add(new InteriorPiece(b.Id + "/banc1", InteriorProp.Bench, b.CX + ax * across, b.CZ + az * across, AxisYaw(b), b.Long * 0.86));
                            Add(new InteriorPiece(b.Id + "/banc2", InteriorProp.Bench, b.CX - ax * across, b.CZ - az * across, AxisYaw(b) + 180, b.Long * 0.86));
                        }
                        else
                        {
                            double along = b.Long / 2 - 0.15;
                            Add(new InteriorPiece(b.Id + "/tabouret1", InteriorProp.Stool, b.CX + lx * along, b.CZ + lz * along, 0, 0.42));
                            Add(new InteriorPiece(b.Id + "/tabouret2", InteriorProp.Stool, b.CX - lx * along, b.CZ - lz * along, 35, 0.42));
                        }
                        // Sur la table : une bougie au milieu, de la vaisselle à chaque bout, parfois des bouteilles ou des pommes.
                        double reach = b.Long * (style == InteriorStyle.Tavern ? 0.27 : 0.22);
                        Add(new InteriorPiece(b.Id + "/bougie", InteriorProp.Candlestick, b.CX, b.CZ, 0, 0, height: 0.24, parent: table));
                        Add(new InteriorPiece(b.Id + "/gobelets", InteriorProp.Goblets, b.CX + lx * reach + ax * 0.12, b.CZ + lz * reach + az * 0.12,
                            v % 360, 0.3, parent: table));
                        Add(new InteriorPiece(b.Id + "/assiette", InteriorProp.Plate, b.CX - lx * reach - ax * 0.1, b.CZ - lz * reach - az * 0.1, 0, 0.27,
                            parent: table));
                        Add(new InteriorPiece(b.Id + "/bol", InteriorProp.Bowl, b.CX - lx * (reach - 0.3) + ax * 0.15, b.CZ - lz * (reach - 0.3) + az * 0.15,
                            0, 0.2, parent: table));
                        if (v % 3 == 0)
                            Add(new InteriorPiece(b.Id + "/bouteilles", InteriorProp.Bottles, b.CX + lx * (reach * 0.35) - ax * 0.15,
                                b.CZ + lz * (reach * 0.35) - az * 0.15, AxisYaw(b), 0.34, parent: table));
                        else
                            Add(new InteriorPiece(b.Id + "/pomme", InteriorProp.Apple, b.CX - lx * reach + ax * 0.2, b.CZ - lz * reach + az * 0.2, v % 90, 0.085,
                                parent: table));
                        if (style == InteriorStyle.House)
                        {
                            Add(new InteriorPiece(b.Id + "/planche", InteriorProp.CuttingBoard, b.CX + lx * reach, b.CZ + lz * reach, AxisYaw(b) + 15, 0.4, parent: table));
                            Add(new InteriorPiece(b.Id + "/oignon", InteriorProp.Onion, b.CX + lx * (reach + 0.08), b.CZ + lz * (reach + 0.08) + 0.05, 30, 0.08,
                                parent: table));
                        }
                        break;
                    }
                    case InteriorFurniture.Counter:
                    {
                        // Comptoir fait par le code ; dessus : bouteilles, gobelets, lanterne allumée, planche et oignons, panier de pommes.
                        (double lx, double lz) = b.AlongX ? (1.0, 0.0) : (0.0, 1.0);
                        double half = b.Long / 2;
                        double y = CounterHeight;
                        Add(new InteriorPiece(b.Id + "/bouteilles", InteriorProp.Bottles, b.CX - lx * half * 0.7, b.CZ - lz * half * 0.7, AxisYaw(b), 0.36, y: y));
                        Add(new InteriorPiece(b.Id + "/gobelets", InteriorProp.Goblets, b.CX - lx * half * 0.25, b.CZ - lz * half * 0.25, 20, 0.3, y: y));
                        Add(new InteriorPiece(b.Id + "/lanterne", InteriorProp.Lantern, b.CX + lx * half * 0.15, b.CZ + lz * half * 0.15, 0, 0, height: 0.42, y: y));
                        Light(InteriorLightKind.Lantern, b.CX + lx * half * 0.15, y + 0.3, b.CZ + lz * half * 0.15, 4.5, 1.4);
                        Add(new InteriorPiece(b.Id + "/planche", InteriorProp.CuttingBoard, b.CX + lx * half * 0.5, b.CZ + lz * half * 0.5, AxisYaw(b) + 10, 0.42, y: y));
                        Add(new InteriorPiece(b.Id + "/oignon", InteriorProp.Onion, b.CX + lx * half * 0.55, b.CZ + lz * half * 0.55, 0, 0.08, y: y + 0.03));
                        Add(new InteriorPiece(b.Id + "/panier", InteriorProp.Basket, b.CX + lx * half * 0.82, b.CZ + lz * half * 0.82, 15, 0.36, y: y));
                        break;
                    }
                    case InteriorFurniture.Shelf:
                    {
                        // Étagères et commodes le long du mur, garnies de bouteilles, de jarres et de paniers.
                        (double lx, double lz) = b.AlongX ? (1.0, 0.0) : (0.0, 1.0);
                        int count = Math.Max(1, (int)Math.Round(b.Long / 1.3));
                        double step = b.Long / count;
                        InteriorProp[] kinds = style == InteriorStyle.House
                            ? new[] { InteriorProp.Shelves, InteriorProp.Commode }
                            : new[] { InteriorProp.Drawers, InteriorProp.Shelves, InteriorProp.Cabinet, InteriorProp.Shelves };
                        for (int i = 0; i < count; i++)
                        {
                            double t = -b.Long / 2 + step * (i + 0.5);
                            InteriorProp kind = kinds[(i + v) % kinds.Length];
                            int shelf = Add(new InteriorPiece($"{b.Id}/{i}", kind, b.CX + lx * t, b.CZ + lz * t, Yaw(f), step * 0.96));
                            InteriorProp top = (i + v) % 3 == 0 ? InteriorProp.Bottles : (i + v) % 3 == 1 ? InteriorProp.ClayPot : InteriorProp.Basket;
                            Add(new InteriorPiece($"{b.Id}/{i}/dessus", top, b.CX + lx * t, b.CZ + lz * t, Yaw(f), top == InteriorProp.Bottles ? 0.34 : 0.3,
                                parent: shelf));
                        }
                        break;
                    }
                    case InteriorFurniture.Barrel:
                    {
                        int barrel = Add(new InteriorPiece(b.Id, InteriorProp.Barrel, b.CX, b.CZ, v % 360, b.Short * 1.02));
                        if (style == InteriorStyle.Tavern && v % 2 == 0)
                            Add(new InteriorPiece(b.Id + "/gobelets", InteriorProp.Goblets, b.CX, b.CZ, v % 90, 0.28, parent: barrel));
                        break;
                    }
                    case InteriorFurniture.Crates:
                    {
                        // Deux caisses empilées contre un bout de l'emprise ; dans la place qui reste, un tonneau s'il tient, sinon un seau
                        // (relecture : rien ne se chevauche ni ne passe dans le mur).
                        (double lx, double lz) = b.AlongX ? (1.0, 0.0) : (0.0, 1.0);
                        double half = b.Long / 2;
                        double size = Math.Min(b.Short * 0.9, b.Long * 0.9);
                        double at = -half + size / 2 + 0.02, rest = b.Long - size - 0.06;
                        int crate = Add(new InteriorPiece(b.Id + "/caisse1", InteriorProp.Crate, b.CX + lx * at, b.CZ + lz * at, Yaw(f), size));
                        Add(new InteriorPiece(b.Id + "/caisse2", InteriorProp.Crate, b.CX + lx * at, b.CZ + lz * at, Yaw(f) + 14, size * 0.8, parent: crate));
                        double free = half - rest / 2 - 0.02;
                        if (rest >= 0.6)
                            Add(new InteriorPiece(b.Id + "/tonneau", InteriorProp.Barrel, b.CX + lx * free, b.CZ + lz * free, v % 360, Math.Min(b.Short * 0.8, rest)));
                        else if (rest >= 0.2)
                            Add(new InteriorPiece(b.Id + "/seau", InteriorProp.Bucket, b.CX + lx * free, b.CZ + lz * free, v % 360, Math.Min(0.32, rest)));
                        break;
                    }
                    case InteriorFurniture.Bed:
                        Add(new InteriorPiece(b.Id, InteriorProp.Bed, b.CX, b.CZ, b.AlongX ? 90 : 0, b.Long * 0.98));   // le lit est long selon z
                        break;
                    case InteriorFurniture.Chest:
                        Add(new InteriorPiece(b.Id, InteriorProp.Chest, b.CX, b.CZ, Yaw(f), b.Long * 0.95));
                        break;
                    case InteriorFurniture.Cabinet:
                        Add(new InteriorPiece(b.Id, InteriorProp.Cabinet, b.CX, b.CZ, Yaw(f), b.Long * 0.98));
                        break;
                    case InteriorFurniture.SpinningWheel:
                        Add(new InteriorPiece(b.Id, InteriorProp.SpinningWheel, b.CX, b.CZ, Yaw(f) + 30, b.Long * 0.95));
                        Add(new InteriorPiece(b.Id + "/panier", InteriorProp.Basket, b.CX + f.X * 0.2 + 0.25, b.CZ + f.Z * 0.2, 0, 0.3));
                        break;
                    case InteriorFurniture.Altar:
                    {
                        // Autel de pierre fait par le code ; dessus : les chandeliers de laiton et deux bougeoirs ; la lumière des bougies.
                        (double lx, double lz) = b.AlongX ? (1.0, 0.0) : (0.0, 1.0);
                        Add(new InteriorPiece(b.Id + "/chandeliers", InteriorProp.Candelabra, b.CX, b.CZ, Yaw(f), b.Long * 0.6, y: AltarHeight));
                        Add(new InteriorPiece(b.Id + "/bougie1", InteriorProp.Candlestick, b.CX + lx * b.Long * 0.42, b.CZ + lz * b.Long * 0.42, 0, 0, height: 0.3,
                            y: AltarHeight));
                        Add(new InteriorPiece(b.Id + "/bougie2", InteriorProp.Candlestick, b.CX - lx * b.Long * 0.42, b.CZ - lz * b.Long * 0.42, 0, 0, height: 0.3,
                            y: AltarHeight));
                        Light(InteriorLightKind.Candle, b.CX + f.X * 0.6, AltarHeight + 0.7, b.CZ + f.Z * 0.6, 6, 1.6);
                        // Le siège du prêtre, derrière l'autel et de côté, tourné vers la nef (relecture : plus sur le pied d'un chandelier).
                        Add(new InteriorPiece(b.Id + "/trone", InteriorProp.Throne, b.CX + lx * (b.Long / 2 + 0.5) - f.X * (b.Short / 2 + 0.9),
                            b.CZ + lz * (b.Long / 2 + 0.5) - f.Z * (b.Short / 2 + 0.9), Yaw(f), 0, height: 1.9));
                        break;
                    }
                    case InteriorFurniture.Candelabra:
                        // Pied de fer fait par le code ; la bougie au sommet, allumée.
                        Add(new InteriorPiece(b.Id + "/bougie", InteriorProp.Candlestick, b.CX, b.CZ, 0, 0, height: 0.28, y: 1.45));
                        Light(InteriorLightKind.Candle, b.CX, 1.95, b.CZ, 4.5, 1.1);
                        break;
                    case InteriorFurniture.Statue:
                        Add(new InteriorPiece(b.Id, InteriorProp.Statue, b.CX, b.CZ, Yaw(f), 0, height: 1.9, y: 0.45));
                        break;
                }
            }

            // Aux murs : objets accrochés le long de chaque mur, hors de la porte, des fenêtres et des meubles adossés ; lanternes allumées.
            IReadOnlyList<InteriorWindow> windows = Windows(map, boxes, style);
            InteriorProp[] decor = style == InteriorStyle.Tavern
                ? new[] { InteriorProp.Shield, InteriorProp.Lantern, InteriorProp.Painting, InteriorProp.Axe, InteriorProp.Lantern, InteriorProp.Sword, InteriorProp.WarHammer }
                : style == InteriorStyle.Chapel
                    ? new[] { InteriorProp.Candlestick, InteriorProp.Painting, InteriorProp.Candlestick }
                    : new[] { InteriorProp.Painting, InteriorProp.Lantern, InteriorProp.Axe };
            int slot = 0;
            foreach (((int X, int Z) wall, double t) in WallSpots(map, boxes, windows, style == InteriorStyle.Chapel ? 2.6 : 1.7))
            {
                InteriorProp prop = decor[slot++ % decor.Length];
                (double x, double z) = WallPoint(map, wall, t, 0.06);
                double yaw = Math.Atan2(wall.X, wall.Z) * 180 / Math.PI;
                // Relecture indépendante : identifiant indépendant de la langue du système (il choisit le modèle de l'objet).
                string id = FormattableString.Invariant($"mur/{wall.X},{wall.Z}/{t:0.00}");
                switch (prop)
                {
                    case InteriorProp.Lantern:
                        Add(new InteriorPiece(id, InteriorProp.Lantern, x + wall.X * 0.12, z + wall.Z * 0.12, yaw, 0, height: 0.42, y: WallDecorHeight + 0.15, onWall: true));
                        Light(InteriorLightKind.Lantern, x + wall.X * 0.35, WallDecorHeight + 0.4, z + wall.Z * 0.35, 5, 1.3);
                        break;
                    case InteriorProp.Candlestick:
                        Add(new InteriorPiece(id, InteriorProp.Candlestick, x + wall.X * 0.12, z + wall.Z * 0.12, yaw, 0, height: 0.3, y: 1.9, onWall: true));
                        Light(InteriorLightKind.Candle, x + wall.X * 0.3, 2.3, z + wall.Z * 0.3, 4, 0.9);
                        break;
                    case InteriorProp.Painting:
                        Add(new InteriorPiece(id, InteriorProp.Painting, x, z, yaw, style == InteriorStyle.Chapel ? 1.1 : 0.75, y: WallDecorHeight - 0.2, onWall: true));
                        break;
                    case InteriorProp.Sword:
                    case InteriorProp.WarHammer:
                    case InteriorProp.Axe:
                        Add(new InteriorPiece(id, prop, x, z, yaw, 0, height: prop == InteriorProp.Sword ? 1.1 : 0.75, y: WallDecorHeight - 0.45, onWall: true));
                        break;
                    default:
                        Add(new InteriorPiece(id, prop, x, z, yaw, 0, height: 0.8, y: WallDecorHeight - 0.3, onWall: true));
                        break;
                }
            }

            // La porte, côté intérieur, contre son mur.
            foreach (MapDoor door in map.Doors)
            {
                (int X, int Z) wall = NearestWall(map, door.X, door.Z);
                (double x, double z) = WallPoint(map, wall, wall.X != 0 ? door.Z - cz : door.X - cx, 0.02);
                Add(new InteriorPiece("porte/" + door.Id, InteriorProp.Door, x, z, Math.Atan2(wall.X, wall.Z) * 180 / Math.PI, 0, height: 2.4, onWall: true));
            }

            // Les poutres du plafond coupent la plus grande longueur, tous les 2,2 m environ.
            int beams = Math.Max(1, (int)Math.Round(length / 2.2));
            var beamAt = new List<double>();
            for (int i = 1; i < beams; i++) beamAt.Add((alongX ? map.MinX : map.MinZ) + length * i / beams);

            return new InteriorLayout(style, height, pieces, lights, windows, beamAt, !alongX, Villagers(map, boxes, style));
        }

        /// <summary>Mur le plus proche d'un point : vecteur vers l'intérieur de la pièce.</summary>
        public static (int X, int Z) NearestWall(AdventureMap map, double x, double z)
        {
            double w = x - map.MinX, e = map.MaxX - x, s = z - map.MinZ, n = map.MaxZ - z;
            double min = Math.Min(Math.Min(w, e), Math.Min(s, n));
            return min == s ? (0, 1) : min == n ? (0, -1) : min == w ? (1, 0) : (-1, 0);
        }

        /// <summary>
        /// Point du mur <paramref name="wall"/> (vecteur vers l'intérieur) à l'abscisse <paramref name="t"/> le long du mur, comptée depuis
        /// le milieu de la pièce, décalé de <paramref name="inset"/> m vers l'intérieur.
        /// </summary>
        public static (double X, double Z) WallPoint(AdventureMap map, (int X, int Z) wall, double t, double inset)
        {
            double cx = (map.MinX + map.MaxX) / 2, cz = (map.MinZ + map.MaxZ) / 2;
            if (wall.X != 0) return (wall.X > 0 ? map.MinX + inset : map.MaxX - inset, cz + t);
            return (cx + t, wall.Z > 0 ? map.MinZ + inset : map.MaxZ - inset);
        }

        private static readonly (int X, int Z)[] Walls = { (0, -1), (-1, 0), (0, 1), (1, 0) };

        /// <summary>Le mur <paramref name="wall"/> est-il libre en <paramref name="t"/> sur ±<paramref name="half"/> m (pas de porte ni de meuble adossé) ?</summary>
        private static bool WallFree(AdventureMap map, IReadOnlyList<Box> boxes, (int X, int Z) wall, double t, double half, double depth)
        {
            double cx = (map.MinX + map.MaxX) / 2, cz = (map.MinZ + map.MaxZ) / 2;
            double span = wall.X != 0 ? map.Depth : map.Width;
            if (Math.Abs(t) + half > span / 2 - 0.6) return false;   // coins
            foreach (MapDoor door in map.Doors)
                if (NearestWall(map, door.X, door.Z) == wall && Math.Abs((wall.X != 0 ? door.Z - cz : door.X - cx) - t) < half + 1.0) return false;
            foreach (Box b in boxes)
            {
                double gap = wall.X > 0 ? b.MinX - map.MinX : wall.X < 0 ? map.MaxX - b.MaxX : wall.Z > 0 ? b.MinZ - map.MinZ : map.MaxZ - b.MaxZ;
                if (gap > depth) continue;
                double lo = wall.X != 0 ? b.MinZ - cz : b.MinX - cx, hi = wall.X != 0 ? b.MaxZ - cz : b.MaxX - cx;
                if (t + half > lo - 0.2 && t - half < hi + 0.2) return false;
            }
            return true;
        }

        /// <summary>Fenêtres : sur chaque mur libre, tous les 3,2 m environ (chapelle : grandes et étroites, sur les longs murs).</summary>
        private static IReadOnlyList<InteriorWindow> Windows(AdventureMap map, IReadOnlyList<Box> boxes, InteriorStyle style)
        {
            var list = new List<InteriorWindow>();
            bool chapel = style == InteriorStyle.Chapel;
            double width = chapel ? 0.85 : 0.95, bottom = chapel ? 2.3 : 1.15, top = chapel ? 5.2 : 2.3, spacing = chapel ? 3.0 : 3.4;
            foreach ((int X, int Z) wall in Walls)
            {
                double span = wall.X != 0 ? map.Depth : map.Width;
                if (chapel && span < Math.Max(map.Width, map.Depth) - 1e-6) continue;   // chapelle : les longs murs seulement
                int count = (int)Math.Floor(span / spacing);
                for (int i = 0; i < count; i++)
                {
                    double t = -span / 2 + span * (i + 0.5) / count;
                    if (!WallFree(map, boxes, wall, t, width / 2 + 0.15, chapel ? 0.2 : 0.9)) continue;
                    (double x, double z) = WallPoint(map, wall, t, 0);
                    list.Add(new InteriorWindow(x, z, wall, width, bottom, top));
                }
            }
            return list;
        }

        /// <summary>Places libres aux murs pour les objets accrochés, tous les <paramref name="spacing"/> m, hors des fenêtres.</summary>
        private static IEnumerable<((int X, int Z) Wall, double T)> WallSpots(AdventureMap map, IReadOnlyList<Box> boxes, IReadOnlyList<InteriorWindow> windows,
            double spacing)
        {
            double cx = (map.MinX + map.MaxX) / 2, cz = (map.MinZ + map.MaxZ) / 2;
            foreach ((int X, int Z) wall in Walls)
            {
                double span = wall.X != 0 ? map.Depth : map.Width;
                int count = (int)Math.Floor(span / spacing);
                for (int i = 0; i < count; i++)
                {
                    double t = -span / 2 + span * (i + 0.5) / count;
                    if (!WallFree(map, boxes, wall, t, 0.35, 0.5)) continue;
                    if (windows.Any(w => w.Wall == wall && Math.Abs((wall.X != 0 ? w.Z - cz : w.X - cx) - t) < w.Width / 2 + 0.45)) continue;
                    yield return (wall, t);
                }
            }
        }

        /// <summary>
        /// Villageois d'une pièce : l'aubergiste derrière son comptoir et deux clients devant (auberge), une fidèle devant l'autel
        /// (chapelle), l'habitant près du feu (maison). Hors des meubles.
        /// </summary>
        private static IReadOnlyList<StyleVillager> Villagers(AdventureMap map, IReadOnlyList<Box> boxes, InteriorStyle style)
        {
            var list = new List<StyleVillager>();
            bool Free(double x, double z) =>
                x > map.MinX + 0.4 && x < map.MaxX - 0.4 && z > map.MinZ + 0.4 && z < map.MaxZ - 0.4
                && boxes.All(b => x < b.MinX - 0.35 || x > b.MaxX + 0.35 || z < b.MinZ - 0.35 || z > b.MaxZ + 0.35)
                && map.Clearings.All(c => (c.X - x) * (c.X - x) + (c.Z - z) * (c.Z - z) > 1.2 * 1.2)
                && list.All(v => (v.X - x) * (v.X - x) + (v.Z - z) * (v.Z - z) > 0.9 * 0.9);
            void Add(string id, string who, VillagerActivity activity, double x, double z, double yaw)
            {
                if (Free(x, z)) list.Add(new StyleVillager(id, who, activity, x, z, yaw));
            }
            double cx = (map.MinX + map.MaxX) / 2, cz = (map.MinZ + map.MaxZ) / 2;
            // Sous-lot V1.6 : les PNJ qui parlent (aubergiste, prêtre, habitant) ont leur corps à leur place, tourné vers le milieu de la pièce,
            // avant les villageois de décor (qui ne se posent pas sur eux).
            foreach ((string id, double x, double z) in map.Speakers)
                list.Add(new StyleVillager(id, "paysan", VillagerActivity.Talk, x, z, Math.Atan2(cx - x, cz - z) * 180 / Math.PI));
            foreach (Box b in boxes)
            {
                switch (b.Kind)
                {
                    case InteriorFurniture.Counter when style == InteriorStyle.Tavern:
                    {
                        // Côté du comptoir tourné vers le mur : l'aubergiste ; côté salle : deux clients accoudés.
                        bool alongX = b.AlongX;
                        int toWall = alongX ? (map.MaxZ - b.MaxZ < b.MinZ - map.MinZ ? 1 : -1) : (map.MaxX - b.MaxX < b.MinX - map.MinX ? 1 : -1);
                        double bx = alongX ? b.CX : b.CX + toWall * (b.Short / 2 + 0.55), bz = alongX ? b.CZ + toWall * (b.Short / 2 + 0.55) : b.CZ;
                        Add("aubergiste", "paysan", VillagerActivity.Talk, bx, bz, alongX ? (toWall > 0 ? 180 : 0) : (toWall > 0 ? 270 : 90));
                        for (int s = -1; s <= 1; s += 2)
                        {
                            double x = alongX ? b.CX + s * b.Long * 0.25 : b.CX - toWall * (b.Short / 2 + 0.55);
                            double z = alongX ? b.CZ - toWall * (b.Short / 2 + 0.55) : b.CZ + s * b.Long * 0.25;
                            Add("client" + (s + 3) / 2, s < 0 ? "garde" : "paysan", s < 0 ? VillagerActivity.Idle : VillagerActivity.Talk, x, z,
                                alongX ? (toWall > 0 ? 0 : 180) : (toWall > 0 ? 90 : 270));
                        }
                        break;
                    }
                    case InteriorFurniture.Altar:
                    {
                        (int X, int Z) f = NearestWall(map, b.CX, b.CZ);
                        // De côté (à 1,5 m de l'axe) : le milieu devant l'autel est le chœur, où l'on arrive.
                        double x = b.CX + f.X * (b.Short / 2 + 1.4) + 1.5, z = b.CZ + f.Z * (b.Short / 2 + 1.4);
                        Add("fidele", "paysan", VillagerActivity.Idle, x, z, Math.Atan2(-f.X, -f.Z) * 180 / Math.PI);
                        break;
                    }
                    case InteriorFurniture.Hearth when style == InteriorStyle.House:
                    {
                        (int X, int Z) f = NearestWall(map, b.CX, b.CZ);
                        // À côté du coin du feu (où l'on arrive), pas dessus.
                        double x = b.CX + f.X * (b.Short / 2 + 1.2), z = b.CZ + f.Z * (b.Short / 2 + 1.2) + 1.5;
                        Add("habitant", "paysan", VillagerActivity.Idle, x, z, Math.Atan2(-f.X, -f.Z) * 180 / Math.PI);
                        break;
                    }
                }
            }
            return list;
        }
    }
}
