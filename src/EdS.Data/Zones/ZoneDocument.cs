using System.Collections.Generic;
using EdS.Data.Scenarios;

namespace EdS.Data.Zones
{
    /// <summary>Sous-lot 12.4 : zone du monde telle que lue dans le fichier, avant validation.</summary>
    public sealed class ZoneDocument
    {
        public int? SchemaVersion { get; set; }
        public string? Kind { get; set; }
        public string? ContentVersion { get; set; }
        public ZoneData? Data { get; set; }
    }

    public sealed class ZoneData
    {
        /// <summary>Identifiant de zone (« zone.vallons »).</summary>
        public string? Id { get; set; }

        public string? Name { get; set; }

        /// <summary>Scénario de zone (simulation : espace, créatures), chemin sans extension sous <c>scenarios/</c>.</summary>
        public string? Scenario { get; set; }

        /// <summary>Point sûr d'entrée dans la zone.</summary>
        public string? Entry { get; set; }

        public List<SafePointData?>? SafePoints { get; set; }
        public List<PlaceData?>? Places { get; set; }
        public List<PopulationData?>? Populations { get; set; }

        /// <summary>Sous-lot V1.8 (schéma 7, zones en mélange) : créatures qui montent au niveau du joueur ; facultatif (pas de mélange).</summary>
        public ZoneScalingData? Scaling { get; set; }
        public List<PassageData?>? Passages { get; set; }

        /// <summary>Sous-lot 12.7 (schéma 2) : chef de la zone ; facultatif.</summary>
        public BossData? Boss { get; set; }

        /// <summary>
        /// Sous-lot V1.6 (schéma 5) : autres chefs de la zone (le chef des bandits), chacun à formes comme le chef ; facultatif. Chacun a son
        /// scénario, son fait et sa récompense de première victoire.
        /// </summary>
        public List<BossData?>? Chiefs { get; set; }

        /// <summary>
        /// Vallons agrandis (schéma 3, demande de Damien du 28 octobre 2026 : « des bâtiments, des routes ») : routes et chemins de la
        /// zone, affichage seulement (le moteur ne les connaît pas) ; facultatif.
        /// </summary>
        public List<RoadData?>? Roads { get; set; }

        /// <summary>
        /// Intérieurs (schéma 4, choix de Damien du 28 octobre 2026 : « on fait la façon 1 », une porte qui mène à un intérieur séparé) :
        /// la zone est un intérieur (une pièce : l'aire praticable en est le sol, ses bords les murs) ; facultatif, faux par défaut.
        /// Affichage seulement.
        /// </summary>
        public bool? Indoor { get; set; }

        /// <summary>Intérieurs (schéma 4) : portes vers d'autres zones ; facultatif.</summary>
        public List<DoorData?>? Doors { get; set; }

        /// <summary>
        /// Sous-lot V2.1 (schéma 8, jour et nuit qui changent le monde) : PNJ de la zone qui rentrent chez eux la nuit (de 22 h à 5 h) ; chacun
        /// se tient devant sa porte, où l'on peut toujours lui parler ; facultatif.
        /// </summary>
        public List<NightSpotData?>? NightSpots { get; set; }

        /// <summary>
        /// Sous-lot V2.5 (schéma 10, événements du monde) : attaques, failles du Seuil et chasses de bêtes déclenchées par le temps du monde ;
        /// facultatif.
        /// </summary>
        public List<WorldEventData?>? Events { get; set; }

        /// <summary>
        /// Sous-lot V2.25.3 (schéma 13, grande carte) : repère et grille de la région (cases de 128 m, secteurs de 256 m) ; facultatif.
        /// Données seulement : rien ne change au jeu.
        /// </summary>
        public RegionData? Region { get; set; }
    }

    /// <summary>Sous-lot V2.25.3 (schéma 13) : grille d'une région, depuis son coin sud-ouest.</summary>
    public sealed class RegionData
    {
        /// <summary>Coin sud-ouest [x, z], en mètres entiers.</summary>
        public PointData? Origin { get; set; }

        /// <summary>Colonnes de cases (d'ouest en est), nombre pair.</summary>
        public int? Columns { get; set; }

        /// <summary>Rangées de cases (du sud au nord), nombre pair.</summary>
        public int? Rows { get; set; }
    }

    /// <summary>Sous-lot V2.1 (schéma 8) : place d'un PNJ la nuit, devant sa porte.</summary>
    public sealed class NightSpotData
    {
        /// <summary>PNJ de l'histoire qui vit dans cette zone (« npc.odile »).</summary>
        public string? Npc { get; set; }

        /// <summary>Sa place la nuit, dans l'aire praticable, hors des obstacles.</summary>
        public PointData? Position { get; set; }
    }

    /// <summary>
    /// Intérieurs (schéma 4) : une porte, place où l'on se tient pour passer dans une autre zone, et où l'on arrive en venant d'elle. Elle
    /// mène à la porte <see cref="ToDoor"/> de la zone <see cref="To"/>, qui ramène à celle-ci.
    /// </summary>
    public sealed class DoorData
    {
        public string? Id { get; set; }

        /// <summary>Nom affiché de ce qu'il y a derrière (« Auberge du Pont »).</summary>
        public string? Name { get; set; }

        /// <summary>Place de la porte, où se tient le personnage (et où il arrive) : [x, z].</summary>
        public PointData? Position { get; set; }

        /// <summary>Zone où elle mène (« zone.auberge »).</summary>
        public string? To { get; set; }

        /// <summary>Porte de cette zone où l'on arrive.</summary>
        public string? ToDoor { get; set; }

        /// <summary>Obstacle du scénario (le bâtiment) dont c'est la porte ; facultatif. Affichage : la façade du bâtiment est tournée vers elle.</summary>
        public string? Building { get; set; }
    }

    /// <summary>Vallons agrandis (schéma 3) : une route ou un chemin, ligne brisée de largeur constante.</summary>
    public sealed class RoadData
    {
        public string? Id { get; set; }

        /// <summary>« terre » (chemin de terre battue) ou « pave » (rue pavée).</summary>
        public string? Kind { get; set; }

        /// <summary>Largeur en mètres.</summary>
        public EdS.Domain.Numerics.Fixed? Width { get; set; }

        public List<PointData?>? Points { get; set; }
    }

    /// <summary>Sous-lot 12.7 : chef à formes successives et récompense de la première victoire.</summary>
    public sealed class BossData
    {
        public string? Id { get; set; }

        /// <summary>Scénario qui décrit les formes du chef (assaillants mortels et placés), chemin sous <c>scenarios/</c>.</summary>
        public string? Scenario { get; set; }

        /// <summary>Formes, dans l'ordre : quand l'une tombe, la suivante se relève à sa place.</summary>
        public List<string?>? Forms { get; set; }

        /// <summary>Fait appris à la première victoire (quêtes).</summary>
        public string? Fact { get; set; }

        /// <summary>Récompenses de la première victoire, attribuées une seule fois.</summary>
        public List<RewardData?>? Rewards { get; set; }

        /// <summary>Butin de chaque victoire (dernière forme) ; facultatif.</summary>
        public string? Loot { get; set; }
    }

    public sealed class RewardData
    {
        public string? Item { get; set; }
        public int? Count { get; set; }
    }

    /// <summary>Point sûr : lieu d'entrée et de retour (défaite, sous-lot 12.7).</summary>
    public sealed class SafePointData
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public PointData? Position { get; set; }
    }

    /// <summary>Lieu nommé de la zone (village, moulin…) : un rectangle, pour les découvertes et les quêtes.</summary>
    public sealed class PlaceData
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public PointData? Min { get; set; }
        public PointData? Max { get; set; }

        /// <summary>Sous-lot V1.8 (schéma 7) : niveau minimum du lieu ; facultatif (1).</summary>
        public int? MinLevel { get; set; }
    }

    /// <summary>Sous-lot V1.8 (schéma 7) : zones en mélange (sujet 4).</summary>
    public sealed class ZoneScalingData
    {
        public int? BaseLevel { get; set; }
        public int? BossBaseLevel { get; set; }
        public int? HealthPerLevelPercent { get; set; }
        public int? DamagePerLevelPercent { get; set; }
        public int? EaseLevels { get; set; }
    }

    /// <summary>Population : une créature du scénario qui réapparaît, avec sa laisse et son butin.</summary>
    public sealed class PopulationData
    {
        /// <summary>Créature du scénario de zone (modèle de chaque vie).</summary>
        public string? Creature { get; set; }

        /// <summary>Délai de réapparition après sa mort, en ms.</summary>
        public long? RespawnMs { get; set; }

        /// <summary>Laisse : écart (entre les bords des emprises) à sa cible au-delà duquel elle lâche, en mètres.</summary>
        public EdS.Domain.Numerics.Fixed? LeashRange { get; set; }

        /// <summary>Table de butin (catalogue des objets) ; facultative.</summary>
        public string? Loot { get; set; }

        /// <summary>
        /// Sous-lot V1.6 (schéma 5) : famille de la créature (« loup », « bandit ») pour les objectifs <c>defeatFamily</c> ; facultative
        /// (absente : l'identifiant de la créature sans ses chiffres finaux, « loup12 » donne « loup »).
        /// </summary>
        public string? Family { get; set; }

        /// <summary>
        /// Réponses de Damien du 31 octobre 2026 (schéma 6) : créature rare de la nuit (le loup blanc) ; facultatif. Elle n'est pas là au début
        /// d'une session : elle a une chance d'apparaître chaque nuit du monde, après un signe.
        /// </summary>
        public NightData? Night { get; set; }

        /// <summary>
        /// Relecture indépendante de la PR #83 (schéma 7) : niveau propre de la créature, à la place du niveau minimum de son lieu ; facultatif.
        /// Le loup blanc reste une bête du chapitre 1 et le Borgne un chef du chapitre 2, même dans un lieu d'un chapitre plus haut.
        /// </summary>
        public int? Level { get; set; }

        /// <summary>
        /// Sous-lot V2.1 (schéma 8) : moments de la journée où la créature est dans le monde (les loups de la nuit, les échos, les sangliers
        /// qui dorment la nuit) ; facultatif (toujours là). Exclusif de <see cref="Night"/>.
        /// </summary>
        public HoursData? Hours { get; set; }

        /// <summary>
        /// Sous-lot V2.4 (schéma 9) : temps où la créature est dans le monde (les loups et les échos de la brume, les cerfs qui s'abritent sous
        /// l'averse) ; facultatif (par tous les temps). Se combine avec <see cref="Hours"/> ; exclusif de <see cref="Night"/>.
        /// </summary>
        public WeatherData? Weather { get; set; }
    }

    /// <summary>Sous-lot V2.4 (schéma 9) : temps d'une population.</summary>
    public sealed class WeatherData
    {
        /// <summary>Temps où elle est là : « clear », « cloudy », « mist », « drizzle », « shower », « storm » ou « rain » (toute pluie).</summary>
        public List<string?>? Kinds { get; set; }

        /// <summary>Clé du texte annoncé quand elle arrive (« La brume monte… ») ; facultative.</summary>
        public string? Sign { get; set; }
    }

    /// <summary>Sous-lot V2.1 (schéma 8) : heures d'une population.</summary>
    public sealed class HoursData
    {
        /// <summary>Moments où elle est là : « night », « dawn », « day », « dusk » (de 1 à 3, sans doublon).</summary>
        public List<string?>? Phases { get; set; }

        /// <summary>Clé du texte annoncé quand elle arrive (« Des lueurs pâles s'allument… ») ; facultative.</summary>
        public string? Sign { get; set; }
    }

    /// <summary>Schéma 6 : apparition d'une créature rare de la nuit (chance par nuit, délai entre le signe et l'arrivée, texte du signe).</summary>
    public sealed class NightData
    {
        /// <summary>Chance d'apparaître une nuit donnée, en pour cent (1 à 100).</summary>
        public long? ChancePercent { get; set; }

        /// <summary>Délai entre le signe et l'arrivée, en ms (0 à 600 000, multiple du tick).</summary>
        public long? SignMs { get; set; }

        /// <summary>Clé du texte du signe (« Un hurlement glacé monte du pré… »).</summary>
        public string? Sign { get; set; }
    }

    /// <summary>Passage fermé par un obstacle du scénario, ouvert pour de bon par une étape du monde (raccourci du moulin).</summary>
    public sealed class PassageData
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Obstacle { get; set; }
    }
}
