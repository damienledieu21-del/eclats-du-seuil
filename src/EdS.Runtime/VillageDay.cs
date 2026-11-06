using System;
using System.Collections.Generic;
using System.Linq;

namespace EdS.Runtime
{
    /// <summary>Sous-lot V2.3 : la course d'un villageois du décor dans sa journée.</summary>
    public enum VillagerErrand
    {
        /// <summary>Il reste à son poste toute la journée (les marcheurs, et un villageois sur quatre).</summary>
        None = 0,

        /// <summary>Le matin, il va au puits de la place (le marché, les nouvelles), puis revient.</summary>
        Market = 1,

        /// <summary>À midi, il rentre manger chez lui (il disparaît dans la maison la plus proche), puis revient.</summary>
        Lunch = 2,

        /// <summary>Le soir, il va boire un coup devant l'auberge, puis revient (et rentre pour la nuit, comme en V2.1).</summary>
        Tavern = 3,
    }

    /// <summary>
    /// Sous-lot V2.3 : où va un villageois du décor à cette minute : la place (<see cref="X"/>, <see cref="Z"/>), l'orientation à l'arrivée
    /// (degrés), s'il est à son poste, s'il entre dans une maison à l'arrivée (<see cref="Indoors"/> : il disparaît), et son activité là-bas.
    /// </summary>
    public readonly struct VillagerGoal
    {
        public VillagerGoal(double x, double z, double yaw, bool atPost, bool indoors, VillagerActivity activity)
        {
            X = x;
            Z = z;
            Yaw = yaw;
            AtPost = atPost;
            Indoors = indoors;
            Activity = activity;
        }

        public double X { get; }
        public double Z { get; }
        public double Yaw { get; }
        public bool AtPost { get; }
        public bool Indoors { get; }
        public VillagerActivity Activity { get; }
    }

    /// <summary>Sous-lot V2.3 : les places du bourg où vont les villageois du décor (null : pas de telle place dans la zone).</summary>
    public sealed class VillageSpots
    {
        public VillageSpots((double X, double Z)? tavern, (double X, double Z)? tavernOut, (double X, double Z)? well, double wellRadius,
            IReadOnlyList<(double MinX, double MinZ, double MaxX, double MaxZ)> houses)
        {
            Tavern = tavern;
            TavernOut = tavernOut;
            Well = well;
            WellRadius = wellRadius;
            Houses = houses ?? throw new ArgumentNullException(nameof(houses));
        }

        /// <summary>Porte de l'auberge, et la direction qui s'en éloigne (vers la rue, longueur 1).</summary>
        public (double X, double Z)? Tavern { get; }
        public (double X, double Z)? TavernOut { get; }

        /// <summary>Centre du puits de la place et son demi-côté (m).</summary>
        public (double X, double Z)? Well { get; }
        public double WellRadius { get; }

        /// <summary>Emprises des maisons (où l'on rentre manger).</summary>
        public IReadOnlyList<(double MinX, double MinZ, double MaxX, double MaxZ)> Houses { get; }
    }

    /// <summary>
    /// Sous-lot V2.3 (villageois qui suivent leur journée) : allers-retours des villageois du décor, sans Unity ni état : tout est recalculé
    /// de la minute du monde et de l'identifiant du villageois (toujours la même course, au même moment, pour une même partie). Le moteur ne
    /// les connaît pas : ils n'arrêtent rien. Les PNJ de l'histoire ont, eux, leur emploi du temps dans l'histoire (schéma 9).
    /// </summary>
    public static class VillageDay
    {
        /// <summary>Fenêtres des courses (minutes du jour du monde), avant le décalage propre à chacun.</summary>
        public const int MarketFrom = 9 * 60, MarketTo = 10 * 60 + 30, LunchFrom = 12 * 60, LunchTo = 13 * 60 + 15, TavernFrom = 19 * 60, TavernTo = 21 * 60 + 45;

        /// <summary>Décalage de chacun (± minutes) : ils ne partent pas tous ensemble.</summary>
        public const int Spread = 20;

        /// <summary>Course de la journée d'un villageois (toujours la même) ; les marcheurs gardent leur allée.</summary>
        public static VillagerErrand ErrandOf(StyleVillager villager)
        {
            if (villager.Walks || villager.Activity == VillagerActivity.Walk) return VillagerErrand.None;
            return (VillagerErrand)StyleRules.Variant((villager.Id ?? string.Empty) + "/journee", 4);
        }

        /// <summary>Décalage propre au villageois, de −<see cref="Spread"/> à +<see cref="Spread"/> minutes.</summary>
        public static int Offset(StyleVillager villager) => StyleRules.Variant((villager.Id ?? string.Empty) + "/heure", 2 * Spread + 1) - Spread;

        /// <summary>Fenêtre (début compris, fin exclue) de la course <paramref name="errand"/> pour ce villageois.</summary>
        public static (int From, int To) Window(VillagerErrand errand, StyleVillager villager)
        {
            int offset = Offset(villager);
            switch (errand)
            {
                case VillagerErrand.Market: return (MarketFrom + offset, MarketTo + offset);
                case VillagerErrand.Lunch: return (LunchFrom + offset, LunchTo + offset);
                case VillagerErrand.Tavern: return (TavernFrom + offset, TavernTo + offset);
                default: return (0, 0);
            }
        }

        /// <summary>
        /// Où va le villageois à la minute <paramref name="minute"/> du jour (0 à 1439) : à son poste, ou à la place de sa course (autour du
        /// puits, devant l'auberge, ou chez lui). Sans la place voulue dans la zone, il reste à son poste.
        /// </summary>
        public static VillagerGoal Goal(StyleVillager villager, int minute, VillageSpots? spots)
        {
            var post = new VillagerGoal(villager.X, villager.Z, villager.Yaw, true, false, villager.Activity);
            VillagerErrand errand = ErrandOf(villager);
            if (spots is null || errand == VillagerErrand.None) return post;
            (int from, int to) = Window(errand, villager);
            if (minute < from || minute >= to) return post;
            string id = villager.Id ?? string.Empty;
            switch (errand)
            {
                case VillagerErrand.Market when spots.Well is { } well:
                {
                    // Autour du puits, à une place propre à chacun, tourné vers lui.
                    double angle = StyleRules.Variant(id + "/puits", 360) * Math.PI / 180;
                    double r = spots.WellRadius + 1.2 + StyleRules.Variant(id + "/rayon", 8) * 0.1;
                    double x = well.X + Math.Cos(angle) * r, z = well.Z + Math.Sin(angle) * r;
                    return new VillagerGoal(x, z, StyleRules.FaceYaw(x, z, well.X, well.Z), false, false, VillagerActivity.Talk);
                }
                case VillagerErrand.Tavern when spots.Tavern is { } door && spots.TavernOut is { } away:
                {
                    // Devant l'auberge, de part et d'autre de la porte (jamais dessus), tourné vers elle.
                    double along = (StyleRules.Variant(id + "/terrasse", 2) == 0 ? -1 : 1) * (1.8 + StyleRules.Variant(id + "/place", 6) * 0.5);
                    double depth = 1.6 + StyleRules.Variant(id + "/fond", 5) * 0.35;
                    double x = door.X + away.X * depth - away.Z * along, z = door.Z + away.Z * depth + away.X * along;
                    return new VillagerGoal(x, z, StyleRules.FaceYaw(x, z, door.X, door.Z), false, false, VillagerActivity.Talk);
                }
                case VillagerErrand.Lunch when spots.Houses.Count > 0:
                {
                    // À la maison la plus proche : contre son mur, du côté du villageois ; il y entre.
                    var house = spots.Houses.OrderBy(h => Distance2(villager.X, villager.Z, h)).ThenBy(h => h.MinX).ThenBy(h => h.MinZ).First();
                    double x = Math.Max(house.MinX - 0.6, Math.Min(house.MaxX + 0.6, villager.X));
                    double z = Math.Max(house.MinZ - 0.6, Math.Min(house.MaxZ + 0.6, villager.Z));
                    return new VillagerGoal(x, z, StyleRules.FaceYaw(villager.X, villager.Z, x, z), false, true, VillagerActivity.Idle);
                }
                default:
                    return post;
            }
        }

        /// <summary>Carré de la distance du point (x, z) à l'emprise <paramref name="box"/> (0 dedans).</summary>
        private static double Distance2(double x, double z, (double MinX, double MinZ, double MaxX, double MaxZ) box)
        {
            double dx = Math.Max(0, Math.Max(box.MinX - x, x - box.MaxX)), dz = Math.Max(0, Math.Max(box.MinZ - z, z - box.MaxZ));
            return dx * dx + dz * dz;
        }

        /// <summary>
        /// Places de la zone <paramref name="map"/> : la porte de l'auberge (et la direction de la rue), le puits de la place, les maisons.
        /// Une zone sans auberge, sans puits ou sans maison n'a pas la course qui va avec (les intérieurs n'en ont aucune).
        /// </summary>
        public static VillageSpots? Spots(AdventureMap map)
        {
            if (map is null) throw new ArgumentNullException(nameof(map));
            if (map.Indoor) return null;
            var boxes = map.Obstacles.Where(o => o.Points.Count >= 3).Select(o => (o.Id, Kind: TownRules.BuildingFor(o.Id),
                MinX: o.Points.Min(p => p.X.Raw) / 10000.0, MinZ: o.Points.Min(p => p.Z.Raw) / 10000.0,
                MaxX: o.Points.Max(p => p.X.Raw) / 10000.0, MaxZ: o.Points.Max(p => p.Z.Raw) / 10000.0)).ToList();
            (double X, double Z)? tavern = null, tavernOut = null, well = null;
            double wellRadius = 0;
            var inn = boxes.Where(b => b.Kind == StyleBuilding.Inn).OrderBy(b => b.Id, StringComparer.Ordinal).ToList();
            MapDoor? door = map.Doors.Where(d => d.Building != null && inn.Any(b => b.Id == d.Building)).OrderBy(d => d.Id, StringComparer.Ordinal).FirstOrDefault();
            if (door != null)
            {
                var b = inn.First(x => x.Id == door.Building);
                // La rue est du côté du mur le plus proche de la porte.
                double west = Math.Abs(door.X - b.MinX), east = Math.Abs(door.X - b.MaxX), south = Math.Abs(door.Z - b.MinZ), north = Math.Abs(door.Z - b.MaxZ);
                double least = Math.Min(Math.Min(west, east), Math.Min(south, north));
                tavern = (door.X, door.Z);
                tavernOut = least == south ? (0.0, -1.0) : least == north ? (0.0, 1.0) : least == west ? (-1.0, 0.0) : (1.0, 0.0);
            }
            var wells = boxes.Where(b => b.Kind == StyleBuilding.Well).OrderBy(b => b.Id, StringComparer.Ordinal).ToList();
            if (wells.Count > 0)
            {
                var w = wells[0];
                well = ((w.MinX + w.MaxX) / 2, (w.MinZ + w.MaxZ) / 2);
                wellRadius = Math.Max(w.MaxX - w.MinX, w.MaxZ - w.MinZ) / 2;
            }
            var houses = boxes.Where(b => b.Kind == StyleBuilding.House).OrderBy(b => b.Id, StringComparer.Ordinal)
                .Select(b => (b.MinX, b.MinZ, b.MaxX, b.MaxZ)).ToList();
            return new VillageSpots(tavern, tavernOut, well, wellRadius, houses);
        }
    }

    /// <summary>
    /// Sous-lot V2.3 : pour le menu « Essai de la journée du village » de l'éditeur : ce que Damien doit voir au moment choisi (d'après les
    /// emplois du temps de l'histoire, contenu 0.15.0 ; à tenir à jour avec eux).
    /// </summary>
    public static class VillageTrial
    {
        public static string Guide(int minute)
        {
            if (minute >= 22 * 60 || minute < 5 * 60)
                return "À voir : chacun rentre. Maëlle, l'apprentie, Garin, Bertille, le fermier et le doyen vont devant leur porte ; Firmin est au fournil (le pain se fait la nuit), Aldric au gué, Lison au puits, Gaspard à son poste. Les villageois du décor sont rentrés.";
            if (minute < 7 * 60)
                return "À voir : Firmin au fournil (sa réplique de l'aube), Odile au moulin, la veuve qui prie devant la chapelle, le fermier aux champs.";
            if (minute < 11 * 60 + 30)
                return "À voir : chacun à son travail, comme au début du jeu (Firmin au fournil, Maëlle à la forge, Garin et Bertille à leur étal). Les villageois du décor : un sur quatre va au puits de la place, puis revient.";
            if (minute < 14 * 60)
                return "À voir : midi. Firmin vend son pain sur la place ; Odile rince ses sacs à la rivière ; Maëlle mange devant chez elle, l'apprentie va au puits du village ; Aldric au tir à l'arc ; le capitaine et Garin mangent à la taverne (devant l'auberge) ; Bertille et Renart aux nouvelles, au puits. Garin vend à la taverne : à son étal, un message dit où il est. Un villageois du décor sur quatre rentre manger (il disparaît dans une maison).";
            if (minute < 18 * 60 + 30)
                return "À voir : l'après-midi. Firmin fait la sieste devant sa maison (« je repose mes yeux »), Sauge à la cueillette dans les champs (de 15 h à 16 h 30), Lison qui joue près de la chapelle, Aldric qui file à la taverne vers 16 h 30.";
            return "À voir : le soir. La taverne (la terrasse devant l'auberge) se remplit : Firmin, Maëlle, Bertille, Renart, le fermier ; Mathurin, à l'intérieur, en parle. Gaspard au coin du feu, le doyen, l'herboriste et la fermière à la veillée du hameau, l'apprentie à l'entraînement près du moulin. Un villageois du décor sur quatre vient devant l'auberge. Sous une averse, chacun s'abrite sous l'auvent.";
        }
    }
}
