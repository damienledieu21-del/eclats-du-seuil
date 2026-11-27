using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Zones;
using EdS.World;

namespace EdS.Runtime
{
    /// <summary>V2.51b : pièce de l'Envers d'après l'identifiant d'un obstacle (retouches de l'Envers, préfixe « envers_ »).</summary>
    public enum EnversPiece
    {
        None = 0,
        /// <summary>Arbre mort aux racines tournées vers le ciel (« envers_arbre_mort_… »).</summary>
        DeadTree,
        /// <summary>Ruine inversée (« envers_ruine_inversee_… »).</summary>
        InvertedRuin,
        /// <summary>Éclat d'obsidienne veiné de violet (« envers_eclat_noir_… »).</summary>
        BlackShard,
        /// <summary>Autel de l'Aube, point sûr de l'Envers (« envers_autel_de_l_aube »).</summary>
        DawnAltar,
        /// <summary>V2.51b (kit du 26 novembre 2026, prêt pour le V2.52) : statue pendue tête en bas (« envers_statue_inversee_… »).</summary>
        InvertedStatue,
        /// <summary>Maison effondrée (« envers_maison_effondree_… »).</summary>
        CollapsedHouse,
        /// <summary>Pierre levée fendue d'une lueur violette (« envers_pierre_levee_fendue_… »).</summary>
        SplitStone,
        /// <summary>Brasier aux flammes violettes (« envers_brasier_violet_… »).</summary>
        VioletBrazier,
    }

    /// <summary>V2.51b : un rocher flottant du ciel de l'Envers (décor seulement, sans collision).</summary>
    public readonly struct FloatingRock
    {
        public double X { get; }
        public double Y { get; }
        public double Z { get; }
        /// <summary>Taille (m) du plus grand côté.</summary>
        public double Size { get; }
        /// <summary>Lacet (degrés).</summary>
        public double Yaw { get; }
        /// <summary>Modèle : rocher, débris ou arche brisée (<see cref="EnversLook.FloatingModels"/>).</summary>
        public int Model { get; }
        /// <summary>Lente oscillation verticale : phase (radians).</summary>
        public double Phase { get; }

        public FloatingRock(double x, double y, double z, double size, double yaw, int model, double phase)
        {
            X = x;
            Y = y;
            Z = z;
            Size = size;
            Yaw = yaw;
            Model = model;
            Phase = phase;
        }
    }

    /// <summary>V2.51b : la lumière de l'Envers à une minute du jour (couleurs de 0 à 1, facteurs, angles en degrés).</summary>
    public readonly struct EnversLight
    {
        public (double R, double G, double B) Sun { get; }
        public double SunIntensity { get; }
        /// <summary>Hauteur du « soleil noir » au-dessus de l'horizon (degrés), toujours bas.</summary>
        public double SunHeight { get; }
        /// <summary>Direction (degrés) : à l'opposé du soleil de la surface.</summary>
        public double SunTurn { get; }
        public (double R, double G, double B) AmbientSky { get; }
        public (double R, double G, double B) AmbientHorizon { get; }
        public (double R, double G, double B) AmbientGround { get; }
        public (double R, double G, double B) Fog { get; }
        public double FogStart { get; }
        public double FogEnd { get; }
        /// <summary>Teinte du ciel et son exposition.</summary>
        public (double R, double G, double B) SkyTint { get; }
        public double SkyExposure { get; }
        /// <summary>Filtre de couleur de l'image (post-traitement), saturation et contraste ajoutés.</summary>
        public (double R, double G, double B) Filter { get; }
        public double Saturation { get; }
        public double Contrast { get; }

        public EnversLight((double, double, double) sun, double sunIntensity, double sunHeight, double sunTurn, (double, double, double) ambientSky,
            (double, double, double) ambientHorizon, (double, double, double) ambientGround, (double, double, double) fog, double fogStart, double fogEnd,
            (double, double, double) skyTint, double skyExposure, (double, double, double) filter, double saturation, double contrast)
        {
            Sun = sun;
            SunIntensity = sunIntensity;
            SunHeight = sunHeight;
            SunTurn = sunTurn;
            AmbientSky = ambientSky;
            AmbientHorizon = ambientHorizon;
            AmbientGround = ambientGround;
            Fog = fog;
            FogStart = fogStart;
            FogEnd = fogEnd;
            SkyTint = skyTint;
            SkyExposure = skyExposure;
            Filter = filter;
            Saturation = saturation;
            Contrast = contrast;
        }
    }

    /// <summary>
    /// Chantier V2.51b : l'Envers des Vallons à l'écran, règles pures (sans Unity, sans horloge réelle, testées). Le même décor que la surface
    /// (cases en reflet), habillé autrement : une lumière propre (crépuscule violet perpétuel qui respire, « soleil noir » bas à l'opposé
    /// du soleil de la surface), un ciel et une brume violets, des matériaux sombres (<see cref="Darken"/> : chaque matériau du décor a sa
    /// variante sombre, désaturée et tirée vers le violet), le temps des Vallons montré à sa façon (<see cref="ShownWeather"/> : rien ne
    /// tombe, des cendres montent), des rochers flottants dans le ciel (<see cref="FloatingRocks"/>, nombre selon la machine), un bourdon
    /// grave en boucle (<see cref="Drone"/>) et la musique du Seuil. Les modèles du kit de l'Envers sont pris dans
    /// <c>Resources/Envers/&lt;nom&gt;</c>, avec des formes simples en repli tant qu'ils ne sont pas générés.
    /// </summary>
    public static class EnversLook
    {
        /// <summary>Dossier des modèles dans le projet Unity : <c>&lt;nom&gt;.fbx</c>, <c>&lt;nom&gt;_couleur.jpg</c>, <c>&lt;nom&gt;_normale.png</c> à plat.</summary>
        public const string AssetFolder = "Assets/EdS/Modeles/Resources/Envers";

        /// <summary>Modèles du palier 2 et du semis au sol (voir l'AssetLab, <c>Envers/modeles.json</c>).</summary>
        public const string StumpModel = "souche_cendre", GrassModel = "herbe_morte", MushroomModel = "champignons_lumineux", StatueModel = "statue_inversee",
            HouseModel = "maison_effondree", RootsModel = "racines_suspendues", StoneModel = "pierre_levee_fendue", BrazierModel = "brasier_violet";

        /// <summary>
        /// Les 14 modèles livrés du kit de l'Envers (Tripo, 26 novembre 2026, 20 crédits chacun), dans l'ordre des fiches de l'atelier.
        /// </summary>
        public static readonly IReadOnlyList<string> Models = Array.AsReadOnly(new[]
        {
            "faille_envers", "arbre_mort_inverse", "rocher_flottant", "ruine_inversee", "eclat_noir", "autel_de_l_aube", StumpModel,
            "debris_flottants", "arche_brisee_flottante", StatueModel, HouseModel, MushroomModel, StoneModel, BrazierModel,
        });

        /// <summary>
        /// Les deux fiches que Tripo n'a pas réussies (deux échecs remboursés, décision du 26 novembre 2026) : toujours dessinées par le code
        /// (touffes d'herbe morte en lames croisées ; racines pendues sous les rochers flottants de repli).
        /// </summary>
        public static readonly IReadOnlyList<string> Procedural = Array.AsReadOnly(new[] { GrassModel, RootsModel });

        private static readonly System.Text.RegularExpressions.Regex AssetName =
            new System.Text.RegularExpressions.Regex("^(?<name>[a-z_]+)\\.fbx$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        /// <summary>Nom du modèle du kit (« eclat_noir ») d'un chemin d'asset Unity, ou null s'il n'est pas un modèle livré du kit.</summary>
        public static string? NameOf(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string file = path.Replace('\\', '/');
            int slash = file.LastIndexOf('/');
            var m = AssetName.Match(slash >= 0 ? file.Substring(slash + 1) : file);
            return m.Success && Models.Contains(m.Groups["name"].Value) ? m.Groups["name"].Value : null;
        }

        /// <summary>Dossier des modèles du kit (sous <c>Resources</c>).</summary>
        public const string Folder = "Envers/";

        /// <summary>Modèles du kit, par leur nom (voir l'AssetLab, <c>Envers/modeles.json</c>).</summary>
        public const string RiftModel = "faille_envers", DeadTreeModel = "arbre_mort_inverse", RuinModel = "ruine_inversee", ShardModel = "eclat_noir",
            AltarModel = "autel_de_l_aube", FloatingRockModel = "rocher_flottant", DebrisModel = "debris_flottants", ArchModel = "arche_brisee_flottante";

        /// <summary>Modèles des formes flottantes, dans l'ordre de <see cref="FloatingRock.Model"/>.</summary>
        public static readonly IReadOnlyList<string> FloatingModels = Array.AsReadOnly(new[] { FloatingRockModel, DebrisModel, ArchModel });

        /// <summary>Hauteur et largeur (m) d'une faille dessinée sur une porte de genre faille.</summary>
        public const double RiftHeight = 5.0, RiftWidth = 3.0;

        /// <summary>Pièce de l'Envers d'un obstacle.</summary>
        public static EnversPiece PieceFor(string? id)
        {
            string key = id ?? string.Empty;
            if (key.StartsWith("envers_arbre_mort", StringComparison.Ordinal)) return EnversPiece.DeadTree;
            if (key.StartsWith("envers_ruine_inversee", StringComparison.Ordinal)) return EnversPiece.InvertedRuin;
            if (key.StartsWith("envers_eclat_noir", StringComparison.Ordinal)) return EnversPiece.BlackShard;
            if (key.StartsWith("envers_autel_de_l_aube", StringComparison.Ordinal)) return EnversPiece.DawnAltar;
            if (key.StartsWith("envers_statue_inversee", StringComparison.Ordinal)) return EnversPiece.InvertedStatue;
            if (key.StartsWith("envers_maison_effondree", StringComparison.Ordinal)) return EnversPiece.CollapsedHouse;
            if (key.StartsWith("envers_pierre_levee_fendue", StringComparison.Ordinal)) return EnversPiece.SplitStone;
            if (key.StartsWith("envers_brasier_violet", StringComparison.Ordinal)) return EnversPiece.VioletBrazier;
            return EnversPiece.None;
        }

        public static string? ModelFor(EnversPiece piece) => piece switch
        {
            EnversPiece.DeadTree => DeadTreeModel,
            EnversPiece.InvertedRuin => RuinModel,
            EnversPiece.BlackShard => ShardModel,
            EnversPiece.DawnAltar => AltarModel,
            EnversPiece.InvertedStatue => StatueModel,
            EnversPiece.CollapsedHouse => HouseModel,
            EnversPiece.SplitStone => StoneModel,
            EnversPiece.VioletBrazier => BrazierModel,
            _ => null,
        };

        /// <summary>Hauteur (m) de la pièce posée sur l'emprise de son obstacle.</summary>
        public static double HeightOf(EnversPiece piece) => piece switch
        {
            EnversPiece.DeadTree => 7.0,
            EnversPiece.InvertedRuin => 6.0,
            EnversPiece.BlackShard => 3.0,
            EnversPiece.DawnAltar => 1.6,
            EnversPiece.InvertedStatue => 3.5,
            EnversPiece.CollapsedHouse => 6.0,
            EnversPiece.SplitStone => 4.0,
            EnversPiece.VioletBrazier => 1.4,
            _ => 0.0,
        };

        /// <summary>
        /// Les ruines et les maisons effondrées se répètent en tuiles bout à bout le long d'un obstacle allongé, d'après les bornes réelles du
        /// modèle (<see cref="SewerLooks.WallTiles"/> : sans trou) ; les autres pièces sont posées d'un seul tenant.
        /// </summary>
        public static bool Tiled(EnversPiece piece) => piece == EnversPiece.InvertedRuin || piece == EnversPiece.CollapsedHouse;

        /// <summary>Les grandes pièces (arbres morts, ruines) cachent le personnage : elles s'effacent en fondu (occulteurs).</summary>
        public static bool Occludes(EnversPiece piece) => piece == EnversPiece.DeadTree || piece == EnversPiece.InvertedRuin || piece == EnversPiece.CollapsedHouse
            || piece == EnversPiece.SplitStone || piece == EnversPiece.InvertedStatue;

        /// <summary>Le brasier a sa flamme violette (lumière ponctuelle) ; couleur, intensité, portée.</summary>
        public static bool Glows(EnversPiece piece) => piece == EnversPiece.VioletBrazier || piece == EnversPiece.DawnAltar;

        /// <summary>Lueur de l'autel de l'Aube (couleur, intensité, portée) : la seule lumière chaude de l'Envers.</summary>
        public static readonly (double R, double G, double B) AltarGlow = (1.0, 0.82, 0.52);
        public const double AltarGlowIntensity = 2.2, AltarGlowRange = 9.0;

        /// <summary>Lueur d'une faille (violet).</summary>
        public static readonly (double R, double G, double B) RiftGlow = (0.66, 0.42, 1.0);
        public const double RiftGlowIntensity = 2.6, RiftGlowRange = 10.0;

        // --- Matériaux sombres ------------------------------------------------------------------------------------------

        /// <summary>Part de saturation gardée, assombrissement et part de violet mêlée.</summary>
        public const double KeptSaturation = 0.35, Dimming = 0.55, VioletShare = 0.25;

        /// <summary>Le violet vers lequel tirent les couleurs de l'Envers.</summary>
        public static readonly (double R, double G, double B) Violet = (0.42, 0.34, 0.62);

        /// <summary>
        /// Variante sombre d'une couleur de matériau (de 0 à 1) : désaturée (<see cref="KeptSaturation"/>), assombrie (<see cref="Dimming"/>),
        /// tirée vers le violet (<see cref="VioletShare"/>) ; jamais plus claire que la couleur de départ, toujours dans [0, 1].
        /// </summary>
        public static (double R, double G, double B) Darken(double r, double g, double b)
        {
            r = Clamp01(r);
            g = Clamp01(g);
            b = Clamp01(b);
            double l = Luminance(r, g, b);
            double Channel(double c, double v) => Clamp01(((l + (c - l) * KeptSaturation) * (1 - VioletShare) + v * l * VioletShare * 1.6) * Dimming);
            (double dr, double dg, double db) = (Channel(r, Violet.R), Channel(g, Violet.G), Channel(b, Violet.B));
            // Jamais plus clair que l'original (un gris très clair tiré vers le violet pourrait l'être d'un cheveu).
            double dl = Luminance(dr, dg, db);
            if (dl > l && dl > 0) (dr, dg, db) = (dr * l / dl, dg * l / dl, db * l / dl);
            return (dr, dg, db);
        }

        public static double Luminance(double r, double g, double b) => 0.2126 * r + 0.7152 * g + 0.0722 * b;

        private static double Clamp01(double v) => double.IsNaN(v) ? 0 : v < 0 ? 0 : v > 1 ? 1 : v;

        // --- Lumière et ciel ----------------------------------------------------------------------------------------------

        /// <summary>Respirations de la lumière par jour du monde (un souffle toutes les 8 heures de jeu) et leur amplitude.</summary>
        public const int BreathsPerDay = 3;
        public const double BreathDepth = 0.12;

        /// <summary>
        /// La lumière de l'Envers à la minute <paramref name="minuteOfDay"/> (toute valeur ramenée dans le jour) : un crépuscule violet
        /// perpétuel qui ne suit pas le soleil de la surface mais respire lentement (<see cref="BreathsPerDay"/>), le « soleil noir » bas
        /// (12 à 16°) à l'opposé de celui de la surface ; la brume violette proche (35 à 220 m), un ciel sombre teinté.
        /// </summary>
        public static EnversLight Light(int minuteOfDay)
        {
            int m = ((minuteOfDay % 1440) + 1440) % 1440;
            double breath = Math.Sin(2 * Math.PI * BreathsPerDay * m / 1440.0);   // −1 à 1
            double level = 1 + BreathDepth * breath;
            (double, double, double) Scale((double R, double G, double B) c, double k) => (Clamp01(c.R * k), Clamp01(c.G * k), Clamp01(c.B * k));
            return new EnversLight(
                sun: Scale((0.62, 0.5, 0.86), level),
                sunIntensity: 0.45 * level,
                sunHeight: 14 + 2 * breath,
                sunTurn: 180 + 30 * Math.Sin(2 * Math.PI * m / 1440.0),
                ambientSky: Scale((0.36, 0.3, 0.52), level),
                ambientHorizon: Scale((0.26, 0.2, 0.38), level),
                ambientGround: Scale((0.1, 0.08, 0.14), level),
                fog: Scale((0.24, 0.17, 0.34), level),
                fogStart: 35,
                fogEnd: 220 * (1 + 0.5 * BreathDepth * breath),
                skyTint: Scale((0.5, 0.36, 0.72), level),
                skyExposure: 0.35 * level,
                filter: (0.86, 0.8, 1.0),
                saturation: -35,
                contrast: 12);
        }

        /// <summary>Saison montrée dans l'Envers : aucune neige, aucune glace, un automne mort figé, teinte gris-violet.</summary>
        /// <remarks>Revue Codex de la PR #261 (2e tour) : transition terminée (<c>Blend</c> à 1) : aucune saison d'avant ne se mêle au
        /// feuillage, même quand le décor est reconstruit (<see cref="AtelierTrees.FadingFrom"/>).</remarks>
        public static SeasonLook Season => new SeasonLook(0.42f, 0.36f, 0.5f, 0.75f, 0f, 0f, 2, 1f);

        /// <summary>
        /// Revue Codex de la PR #261 (P2) : saison d'affichage du décor (feuillage des arbres, imposteurs, feuilles au sol) : dans l'Envers,
        /// toujours l'automne mort de <see cref="Season"/> ; ailleurs, la saison réelle <paramref name="season"/> (0 printemps … 3 hiver). Les
        /// tenues des personnages gardent la saison réelle (cette règle ne les touche pas).
        /// </summary>
        public static int DecorSeason(bool envers, int season) => envers ? Season.Season : season;

        // --- Temps ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Le temps montré dans l'Envers d'après celui des Vallons (Recommandation : l'Envers n'a pas de météo propre, il reflète celle de la
        /// surface à sa façon) : rien ne tombe ; clair et couvert donnent un ciel couvert sombre, brume et pluies une brume violette, l'orage
        /// un ciel couvert ; la pluie de la surface monte en cendres (<see cref="AshDensity"/>, l'orage le plus dense).
        /// </summary>
        public static WeatherKind ShownWeather(WeatherKind surface) => surface switch
        {
            WeatherKind.Mist or WeatherKind.Drizzle or WeatherKind.Shower => WeatherKind.Mist,
            _ => WeatherKind.Cloudy,
        };

        /// <summary>Densité des cendres qui montent (0 à 1,5) selon le temps de la surface : la pluie des Vallons monte en cendres dans l'Envers.</summary>
        public static double AshDensity(WeatherKind surface) => surface switch
        {
            WeatherKind.Clear => 0.25,
            WeatherKind.Cloudy => 0.35,
            WeatherKind.Mist => 0.5,
            WeatherKind.Drizzle => 0.75,
            WeatherKind.Shower => 1.0,
            WeatherKind.Storm => 1.5,
            _ => 0.35,
        };

        /// <summary>Cendres au plus, selon la machine (le PC modeste de Damien : 32).</summary>
        public static int MaxAsh(DeviceTier tier) => tier switch
        {
            DeviceTier.Phone => 12,
            DeviceTier.Modest => 32,
            _ => 80,
        };

        /// <summary>Côté (m) de la boîte de cendres autour de la caméra, hauteur (m) et vitesse de montée (m/s).</summary>
        public const double AshBox = 36, AshHeight = 14, AshRise = 0.6;

        /// <summary>
        /// Place de la cendre <paramref name="index"/> à l'instant <paramref name="seconds"/> autour de la caméra : posée par un hachage
        /// fixe de son rang dans une boîte de <see cref="AshBox"/> m qui suit la caméra (chaque cendre reste à sa place dans le monde tant
        /// qu'elle est dans la boîte), elle monte de <see cref="AshRise"/> m/s puis repart du sol. Aucun état.
        /// </summary>
        public static (double X, double Y, double Z) Ash(int index, double cameraX, double cameraZ, double seconds)
        {
            double hx = Hash01(index, 1), hz = Hash01(index, 2), hy = Hash01(index, 3);
            double x = Wrap(hx * AshBox - cameraX, AshBox) + cameraX - AshBox / 2;
            double z = Wrap(hz * AshBox - cameraZ, AshBox) + cameraZ - AshBox / 2;
            double y = Wrap(hy * AshHeight + seconds * AshRise * (0.7 + 0.6 * Hash01(index, 4)), AshHeight);
            return (x, y, z);
        }

        private static double Wrap(double v, double size)
        {
            double r = v % size;
            return r < 0 ? r + size : r;
        }

        // --- Rochers flottants -------------------------------------------------------------------------------------------

        /// <summary>Formes flottantes par case de 128 m, selon la machine (téléphone 1, PC modeste 2, PC fort 4).</summary>
        public static int FloatingPerCell(DeviceTier tier) => tier switch
        {
            DeviceTier.Phone => 1,
            DeviceTier.Modest => 2,
            _ => 4,
        };

        /// <summary>Hauteur (m) la plus basse et la plus haute d'une forme flottante : toujours au-dessus des têtes et des toits.</summary>
        public const double MinFloatHeight = 18, MaxFloatHeight = 42;

        /// <summary>Taille (m) d'une forme flottante.</summary>
        public const double MinFloatSize = 4, MaxFloatSize = 14;

        /// <summary>Amplitude (m) et période (s) de la lente oscillation verticale.</summary>
        public const double BobMeters = 0.8, BobSeconds = 9;

        /// <summary>
        /// Les formes flottantes de la case (<paramref name="column"/>, <paramref name="row"/>) de la grille <paramref name="region"/> :
        /// le même tirage pour la même case (hachage de la case, aucun état), au plus <see cref="FloatingPerCell"/> ; chacune dans sa case,
        /// à 8 m au moins de ses bords, entre <see cref="MinFloatHeight"/> et <see cref="MaxFloatHeight"/> m, la plupart des rochers, parfois
        /// des débris ou une arche brisée. Les machines plus fortes en ont plus : celles d'une machine plus faible en sont les premières.
        /// </summary>
        public static IReadOnlyList<FloatingRock> FloatingRocks(RegionGrid region, int column, int row, DeviceTier tier)
        {
            if (region is null) throw new ArgumentNullException(nameof(region));
            if (column < 0 || column >= region.Columns) throw new ArgumentOutOfRangeException(nameof(column));
            if (row < 0 || row >= region.Rows) throw new ArgumentOutOfRangeException(nameof(row));
            int n = FloatingPerCell(tier);
            var rocks = new List<FloatingRock>(n);
            double x0 = region.OriginX + (double)column * RegionGrid.CellMeters, z0 = region.OriginZ + (double)row * RegionGrid.CellMeters;
            const double inset = 8, span = RegionGrid.CellMeters - 2 * inset;
            for (int i = 0; i < n; i++)
            {
                int seed = unchecked((column * 73856093) ^ (row * 19349663) ^ (i * 83492791));
                double pick = Hash01(seed, 5);
                int model = pick < 0.7 ? 0 : pick < 0.9 ? 1 : 2;
                rocks.Add(new FloatingRock(
                    x0 + inset + Hash01(seed, 6) * span,
                    MinFloatHeight + Hash01(seed, 7) * (MaxFloatHeight - MinFloatHeight),
                    z0 + inset + Hash01(seed, 8) * span,
                    MinFloatSize + Hash01(seed, 9) * (MaxFloatSize - MinFloatSize),
                    Hash01(seed, 10) * 360,
                    model,
                    Hash01(seed, 11) * 2 * Math.PI));
            }
            return rocks;
        }

        /// <summary>Hauteur d'une forme flottante à l'instant <paramref name="seconds"/> (oscillation lente, sans état).</summary>
        public static double BobbedHeight(FloatingRock rock, double seconds) =>
            rock.Y + BobMeters * Math.Sin(2 * Math.PI * seconds / BobSeconds + rock.Phase);

        /// <summary>
        /// Revue Codex de la PR #261 (2e tour) : sol sous une forme flottante : le plus haut du relief affiché (<paramref name="height"/>, la
        /// hauteur du sol en un point) en son centre et aux quatre coins de son emprise : elle n'entre jamais dans une colline.
        /// </summary>
        public static double FloatingGround(FloatingRock rock, Func<double, double, double> height)
        {
            if (height is null) throw new ArgumentNullException(nameof(height));
            double h = rock.Size / 2, best = height(rock.X, rock.Z);
            foreach ((double dx, double dz) in new[] { (-h, -h), (h, -h), (-h, h), (h, h) }) best = Math.Max(best, height(rock.X + dx, rock.Z + dz));
            return best;
        }

        /// <summary>Altitude (m) d'une forme flottante au-dessus du sol <paramref name="ground"/> à l'instant <paramref name="seconds"/>.</summary>
        public static double FloatingAltitude(FloatingRock rock, double ground, double seconds) => ground + BobbedHeight(rock, seconds);

        /// <summary>Bornes verticales (m) de l'occulteur d'une forme flottante : toute son oscillation, sa taille comprise.</summary>
        public static (double Min, double Max) FloatingBounds(FloatingRock rock, double ground) =>
            (ground + rock.Y - rock.Size / 2 - BobMeters, ground + rock.Y + rock.Size / 2 + BobMeters);

        // --- Chargement par cases autour de la caméra --------------------------------------------------------------------

        /// <summary>
        /// Relecture indépendante des PR #260 et #261 (le PC de Damien saccade déjà) : rayon (m) autour de la caméra où les formes flottantes et
        /// le semis de l'Envers sont construits, selon la machine (téléphone 128, PC modeste 192, PC fort 320) ; au-delà de ce rayon plus
        /// <see cref="StreamKeepMargin"/>, ils sont défaits.
        /// </summary>
        public static double StreamRadius(DeviceTier tier) => tier switch
        {
            DeviceTier.Phone => 128,
            DeviceTier.Modest => 192,
            _ => 320,
        };

        /// <summary>Marge (m) gardée au-delà du rayon avant de défaire une case (hystérésis : pas d'allers-retours à la lisière).</summary>
        public const double StreamKeepMargin = 64;

        /// <summary>Cases construites au plus par image (le reste attend les images suivantes).</summary>
        public const int StreamCellsPerFrame = 2;

        /// <summary>Distance (m) du point (<paramref name="x"/>, <paramref name="z"/>) au carré de la case (0 dedans).</summary>
        public static double CellDistance(RegionGrid region, int column, int row, double x, double z)
        {
            if (region is null) throw new ArgumentNullException(nameof(region));
            double x0 = region.OriginX + (double)column * RegionGrid.CellMeters, z0 = region.OriginZ + (double)row * RegionGrid.CellMeters;
            double dx = x < x0 ? x0 - x : x > x0 + RegionGrid.CellMeters ? x - x0 - RegionGrid.CellMeters : 0;
            double dz = z < z0 ? z0 - z : z > z0 + RegionGrid.CellMeters ? z - z0 - RegionGrid.CellMeters : 0;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// Les cases de la grille à <paramref name="radius"/> m au plus du point (bornées à la grille), la plus proche d'abord (puis par rangée
        /// et colonne) : celles à construire autour de la caméra. Fonction pure.
        /// </summary>
        public static IReadOnlyList<(int Column, int Row)> CellsNear(RegionGrid region, double x, double z, double radius)
        {
            if (region is null) throw new ArgumentNullException(nameof(region));
            if (!(radius >= 0)) throw new ArgumentOutOfRangeException(nameof(radius));
            int c0 = Math.Max(0, (int)Math.Floor((x - radius - region.OriginX) / RegionGrid.CellMeters));
            int c1 = Math.Min(region.Columns - 1, (int)Math.Floor((x + radius - region.OriginX) / RegionGrid.CellMeters));
            int r0 = Math.Max(0, (int)Math.Floor((z - radius - region.OriginZ) / RegionGrid.CellMeters));
            int r1 = Math.Min(region.Rows - 1, (int)Math.Floor((z + radius - region.OriginZ) / RegionGrid.CellMeters));
            var cells = new List<(int, int, double)>();
            for (int r = r0; r <= r1; r++)
                for (int c = c0; c <= c1; c++)
                {
                    double d = CellDistance(region, c, r, x, z);
                    if (d <= radius) cells.Add((c, r, d));
                }
            cells.Sort((a, b) => a.Item3 != b.Item3 ? a.Item3.CompareTo(b.Item3) : a.Item2 != b.Item2 ? a.Item2.CompareTo(b.Item2) : a.Item1.CompareTo(b.Item1));
            return cells.Select(t => (t.Item1, t.Item2)).ToList();
        }

        /// <summary>Une case construite reste tant qu'elle est à moins du rayon plus <see cref="StreamKeepMargin"/> de la caméra.</summary>
        public static bool CellKept(RegionGrid region, int column, int row, double x, double z, double radius) =>
            CellDistance(region, column, row, x, z) <= radius + StreamKeepMargin;

        // --- Semis au sol ---------------------------------------------------------------------------------------------------

        /// <summary>Semis au sol par case de 128 m selon la machine (téléphone 2, PC modeste 4, PC fort 10), avant l'écart des obstacles.</summary>
        public static int ScatterPerCell(DeviceTier tier) => tier switch
        {
            DeviceTier.Phone => 2,
            DeviceTier.Modest => 4,
            _ => 10,
        };

        /// <summary>Genres du semis : souche de cendre, champignons lumineux (modèles), herbe morte (toujours par le code).</summary>
        public static readonly IReadOnlyList<string> ScatterModels = Array.AsReadOnly(new[] { StumpModel, MushroomModel, GrassModel });

        /// <summary>
        /// Le semis au sol de la case : places tirées par un hachage de la case (le même à chaque visite), dans la case à 4 m au moins de ses
        /// bords ; genre 0 souche (30 %), 1 champignons (25 %), 2 herbe morte (45 %) ; taille de 0,7 à 1,3 fois celle de la fiche ; lacet.
        /// Les places dans un obstacle ou sur une route sont écartées par le décor. Celles d'une machine plus faible sont les premières.
        /// </summary>
        public static IReadOnlyList<(double X, double Z, int Kind, double Scale, double Yaw)> Scatter(RegionGrid region, int column, int row, DeviceTier tier)
        {
            if (region is null) throw new ArgumentNullException(nameof(region));
            if (column < 0 || column >= region.Columns) throw new ArgumentOutOfRangeException(nameof(column));
            if (row < 0 || row >= region.Rows) throw new ArgumentOutOfRangeException(nameof(row));
            int n = ScatterPerCell(tier);
            var result = new List<(double, double, int, double, double)>(n);
            double x0 = region.OriginX + (double)column * RegionGrid.CellMeters, z0 = region.OriginZ + (double)row * RegionGrid.CellMeters;
            const double inset = 4, span = RegionGrid.CellMeters - 2 * inset;
            for (int i = 0; i < n; i++)
            {
                int seed = unchecked((column * 2654435) ^ (row * 40503) ^ (i * 7919) ^ 0x5EED);
                double pick = Hash01(seed, 21);
                result.Add((x0 + inset + Hash01(seed, 22) * span, z0 + inset + Hash01(seed, 23) * span, pick < 0.3 ? 0 : pick < 0.55 ? 1 : 2,
                    0.7 + 0.6 * Hash01(seed, 24), Hash01(seed, 25) * 360));
            }
            return result;
        }

        // --- Pose des modèles --------------------------------------------------------------------------------------------

        /// <summary>
        /// Échelle (x, y, z, dans les axes du modèle) qui fait tenir un modèle de bornes réelles <paramref name="modelX"/> ×
        /// <paramref name="modelY"/> × <paramref name="modelZ"/> dans une emprise de <paramref name="length"/> (long côté) ×
        /// <paramref name="width"/> × <paramref name="height"/>, appliquée avant le lacet : le long côté du modèle suit le long côté de
        /// l'emprise ; rend aussi s'il faut tourner le modèle d'un quart de tour (son long côté est en z). Une borne nulle laisse l'axe à 1.
        /// </summary>
        public static (double X, double Y, double Z, bool QuarterTurn) FitScale(double modelX, double modelY, double modelZ, double length, double width, double height)
        {
            bool quarter = modelZ > modelX;
            double alongModel = quarter ? modelZ : modelX, acrossModel = quarter ? modelX : modelZ;
            double along = alongModel > 1e-6 ? length / alongModel : 1, across = acrossModel > 1e-6 ? width / acrossModel : 1, up = modelY > 1e-6 ? height / modelY : 1;
            return quarter ? (across, up, along, true) : (along, up, across, false);
        }

        // --- Son -------------------------------------------------------------------------------------------------------

        /// <summary>Fréquences (Hz) du bourdon de l'Envers ; chacune fait un nombre entier de tours en <see cref="DroneSeconds"/> s.</summary>
        public static readonly IReadOnlyList<double> DroneFrequencies = Array.AsReadOnly(new[] { 41.25, 55.0, 82.5, 110.25 });

        public const int DroneSeconds = 4;

        /// <summary>Amplitude la plus forte du bourdon (de 0 à 1).</summary>
        public const double DronePeak = 0.6;

        /// <summary>
        /// Le bourdon grave de l'Envers, une boucle de <see cref="DroneSeconds"/> s à <paramref name="sampleRate"/> échantillons par seconde
        /// (mono) : quatre sinus graves qui battent lentement, sans couture (chaque fréquence et le battement font un nombre entier de tours
        /// dans la boucle), jamais au-delà de <see cref="DronePeak"/>. Fait par le code : aucun fichier son.
        /// </summary>
        public static float[] Drone(int sampleRate)
        {
            if (sampleRate < 1000 || sampleRate > 96000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            int n = checked(sampleRate * DroneSeconds);
            var samples = new float[n];
            double[] weights = { 0.4, 0.3, 0.2, 0.1 };
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / sampleRate, s = 0;
                for (int k = 0; k < DroneFrequencies.Count; k++) s += weights[k] * Math.Sin(2 * Math.PI * DroneFrequencies[k] * t);
                double beat = 0.75 + 0.25 * Math.Sin(2 * Math.PI * t / DroneSeconds);   // un battement par boucle
                samples[i] = (float)(DronePeak * s * beat);
            }
            return samples;
        }

        // --- Outils -----------------------------------------------------------------------------------------------------

        /// <summary>Hachage entier vers [0, 1[ (déterministe, sans état).</summary>
        public static double Hash01(int a, int b)
        {
            unchecked
            {
                uint h = (uint)a * 0x9E3779B1u ^ (uint)b * 0x85EBCA77u;
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                h *= 0x297A2D39u;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / (double)0x1000000;
            }
        }
    }
}
