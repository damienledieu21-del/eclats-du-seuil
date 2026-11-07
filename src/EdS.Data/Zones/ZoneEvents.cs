using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Scenarios;
using EdS.Domain.Collections;
using EdS.Domain.Numerics;
using EdS.Domain.Space;

namespace EdS.Data.Zones
{
    // Sous-lot V2.5 (schéma 10, événements du monde) : forme lue (avant validation) et forme validée des événements d'une zone.

    /// <summary>Sous-lot V2.5 (schéma 10) : un événement du monde tel que lu dans le fichier, avant validation.</summary>
    public sealed class WorldEventData
    {
        public string? Id { get; set; }
        public string? Kind { get; set; }
        public int? EveryDays { get; set; }
        public int? ChancePercent { get; set; }
        public int? FirstDay { get; set; }
        public string? Start { get; set; }
        public int? SpreadMinutes { get; set; }
        public int? AnnounceMinutes { get; set; }
        public int? DurationMinutes { get; set; }
        public int? AftermathMinutes { get; set; }
        public WeatherData? Weather { get; set; }
        public string? Place { get; set; }
        public PointData? Refuge { get; set; }
        public List<string?>? Villagers { get; set; }
        public List<WorldEventGroupData?>? Groups { get; set; }
        public int? Waves { get; set; }
        public long? WaveMs { get; set; }
        public WorldEventRewardData? Reward { get; set; }
        public string? LostFact { get; set; }

        /// <summary>Choix de Damien du 7 novembre 2026 : niveau minimum du personnage (facultatif, 1) ; en dessous, l'événement n'a pas lieu pour lui.</summary>
        public int? MinLevel { get; set; }

        /// <summary>Sous-lot V2.6 (schéma 11) : cicatrice laissée par une faille restée ouverte (faille seulement, facultatif).</summary>
        public WorldEventScarData? Scar { get; set; }
    }

    /// <summary>Sous-lot V2.6 (schéma 11) : la cicatrice d'une faille, telle que lue, avant validation.</summary>
    public sealed class WorldEventScarData
    {
        public int? GrowDays { get; set; }
        public int? MaxStage { get; set; }
        public Fixed? Radius { get; set; }
        public Fixed? RadiusPerStage { get; set; }
        public int? StrengthPercent { get; set; }
        public int? HauntStage { get; set; }
        public int? HauntRespawnMinutes { get; set; }
        public string? PurifyItem { get; set; }
        public long? PurifyXp { get; set; }
        public string? BornFact { get; set; }
        public string? DeepFact { get; set; }
        public string? PurifiedFact { get; set; }

        /// <summary>Sous-lot V2.6.1 (schéma 12) : propagation au lieu voisin (palier, délai en jours du monde, fait de la première), facultative.</summary>
        public int? SpreadStage { get; set; }
        public int? SpreadDays { get; set; }
        public string? SpreadFact { get; set; }
    }

    /// <summary>Sous-lot V2.5 : une variante d'un événement (les bandits ou les loups ; la faille des ruines ou celle du lac).</summary>
    public sealed class WorldEventGroupData
    {
        public string? Id { get; set; }
        public int? Weight { get; set; }

        /// <summary>Faille : où elle s'ouvre (obligatoire pour une faille, refusé ailleurs).</summary>
        public PointData? Position { get; set; }

        public List<WorldEventCreatureData?>? Creatures { get; set; }

        /// <summary>Sous-lot V2.6 (schéma 11) : échos qui rôdent autour de la cicatrice de cette faille (faille avec cicatrice seulement).</summary>
        public List<WorldEventCreatureData?>? Haunts { get; set; }

        /// <summary>Sous-lot V2.6.1 (schéma 12) : variantes voisines où sa cicatrice se propage, dans l'ordre (faille à propagation seulement).</summary>
        public List<string?>? SpreadTo { get; set; }
    }

    public sealed class WorldEventCreatureData
    {
        /// <summary>Population de la zone dont la créature prend le modèle (chiffres, famille, butin, laisse).</summary>
        public string? Like { get; set; }

        public PointData? Position { get; set; }
    }

    public sealed class WorldEventRewardData
    {
        public long? Xp { get; set; }
        public long? Money { get; set; }
        public List<RewardData?>? Items { get; set; }
        public string? Fact { get; set; }
    }

    /// <summary>Sous-lot V2.5 : sorte d'événement du monde.</summary>
    public enum WorldEventKind
    {
        /// <summary>Attaque d'un lieu (le hameau) : à défendre en venant à bout des assaillants avant la fin.</summary>
        Raid = 0,

        /// <summary>Faille du Seuil : des échos en sortent par vagues ; on la referme quand plus aucun n'est debout.</summary>
        Rift = 1,

        /// <summary>Chasse de bêtes entre elles (la meute qui attaque les sangliers à l'aube) : ni récompense ni registre.</summary>
        Hunt = 2,
    }

    /// <summary>Sous-lot V2.5 : une créature d'un événement, sur le modèle d'une population de la zone.</summary>
    public sealed class WorldEventCreature
    {
        public string Like { get; }
        public GroundVector Position { get; }

        internal WorldEventCreature(string like, GroundVector position)
        {
            Like = like;
            Position = position;
        }
    }

    /// <summary>Sous-lot V2.5 : variante d'un événement, tirée selon son poids.</summary>
    public sealed class WorldEventGroup
    {
        public string Id { get; }
        public int Weight { get; }

        /// <summary>Faille : où elle s'ouvre ; null pour une attaque ou une chasse.</summary>
        public GroundVector? Position { get; }

        public IReadOnlyList<WorldEventCreature> Creatures { get; }

        /// <summary>Sous-lot V2.6 : échos qui rôdent autour de la cicatrice de cette faille, dans l'ordre où ils viennent (vide sans cicatrice).</summary>
        public IReadOnlyList<WorldEventCreature> Haunts { get; }

        /// <summary>
        /// Sous-lot V2.6.1 : variantes voisines (même faille) où la cicatrice de celle-ci se propage, dans l'ordre où elle les gagne (la plus
        /// proche d'abord) ; vide sans propagation.
        /// </summary>
        public IReadOnlyList<string> SpreadTo { get; }

        internal WorldEventGroup(string id, int weight, GroundVector? position, IEnumerable<WorldEventCreature> creatures, IEnumerable<WorldEventCreature>? haunts = null,
            IEnumerable<string>? spreadTo = null)
        {
            SpreadTo = Frozen.List(spreadTo ?? Array.Empty<string>(), nameof(spreadTo));
            Id = id;
            Weight = weight;
            Position = position;
            Creatures = Frozen.List(creatures, nameof(creatures));
            Haunts = Frozen.List(haunts ?? Array.Empty<WorldEventCreature>(), nameof(haunts));
        }

        /// <summary>Sous-lot V2.6.1 : la même variante, avec ses voisines de propagation (validateur seulement).</summary>
        internal WorldEventGroup WithSpreadTo(IEnumerable<string?> spreadTo) => new WorldEventGroup(Id, Weight, Position, Creatures, Haunts, spreadTo.Select(x => x!));
    }

    /// <summary>Sous-lot V2.5 : récompense de la première réussite d'un personnage (donnée une seule fois, registre de la sauvegarde).</summary>
    public sealed class WorldEventReward
    {
        public long Xp { get; }
        public long Money { get; }
        public IReadOnlyList<(string Item, int Count)> Items { get; }

        /// <summary>Fait appris à la première réussite (remerciements des PNJ), ou null.</summary>
        public string? Fact { get; }

        internal WorldEventReward(long xp, long money, IEnumerable<(string, int)> items, string? fact)
        {
            Xp = xp;
            Money = money;
            Items = Frozen.List(items, nameof(items));
            Fact = fact;
        }
    }

    /// <summary>
    /// Sous-lot V2.5 (schéma 10) : événement du monde d'une zone, déclenché par le temps du monde, de façon déterministe. Les jours du monde
    /// sont pris par fenêtres de <see cref="EveryDays"/> jours ; dans chaque fenêtre, l'événement a sa chance (<see cref="ChancePercent"/>), un
    /// jour, une minute de départ (de <see cref="StartMinute"/> à <see cref="StartMinute"/> + <see cref="SpreadMinutes"/>) et une variante,
    /// tirés par un <c>Pcg32</c> initialisé par la clé de la fenêtre (<c>EdS.World.WorldEventSchedule</c>).
    /// </summary>
    public sealed class WorldEventDefinition
    {
        public string Id { get; }
        public WorldEventKind Kind { get; }
        public int EveryDays { get; }
        public int ChancePercent { get; }

        /// <summary>Premier jour du monde où il peut avoir lieu (le jour 0 commence à 8 h : rien avant la première vraie journée).</summary>
        public int FirstDay { get; }

        /// <summary>Minute du monde (0 à 1 439) du départ au plus tôt.</summary>
        public int StartMinute { get; }

        public int SpreadMinutes { get; }

        /// <summary>Annonce, en minutes du monde avant le départ (0 : aucune).</summary>
        public int AnnounceMinutes { get; }

        public int DurationMinutes { get; }

        /// <summary>Conséquences visibles d'un échec, en minutes du monde après la fin (0 : aucune).</summary>
        public int AftermathMinutes { get; }

        /// <summary>Temps où il peut commencer (celui du départ), ou null : par tous les temps.</summary>
        public PopulationWeather? Weather { get; }

        /// <summary>Lieu de la zone concerné (le hameau), ou null.</summary>
        public string? Place { get; }

        /// <summary>Attaque : où se réfugient les villageois (<see cref="Villagers"/>), ou null.</summary>
        public GroundVector? Refuge { get; }

        public IReadOnlyList<string> Villagers { get; }
        public IReadOnlyList<WorldEventGroup> Groups { get; }

        /// <summary>Faille : nombre de vagues d'échos (1 ailleurs).</summary>
        public int Waves { get; }

        /// <summary>Faille : écart entre deux vagues, en ms (0 ailleurs).</summary>
        public long WaveMs { get; }

        /// <summary>Récompense de la première réussite, ou null (une chasse n'en a pas).</summary>
        public WorldEventReward? Reward { get; }

        /// <summary>Fait appris au premier échec (les PNJ s'en souviennent), ou null.</summary>
        public string? LostFact { get; }

        /// <summary>
        /// Choix de Damien du 7 novembre 2026 (question 2 de la PR #107) : niveau minimum du personnage. En dessous, l'événement n'a pas lieu
        /// pour lui : ni annonce, ni arrivée, ni issue (ni pillage, ni pillage pendant l'absence) ; ses occurrences sont seulement notées
        /// au registre, sans issue.
        /// </summary>
        public int MinLevel { get; }

        /// <summary>Sous-lot V2.6 (schéma 11) : cicatrice laissée par une occurrence restée ouverte (faille seulement), ou null.</summary>
        public WorldEventScar? Scar { get; }

        /// <summary>L'événement a une issue gardée au registre du personnage (attaque, faille ; pas une chasse).</summary>
        public bool Settles => Kind != WorldEventKind.Hunt;

        public int TotalWeight { get; }

        internal WorldEventDefinition(string id, WorldEventKind kind, int everyDays, int chancePercent, int firstDay, int startMinute, int spreadMinutes,
            int announceMinutes, int durationMinutes, int aftermathMinutes, PopulationWeather? weather, string? place, GroundVector? refuge,
            IEnumerable<string> villagers, IEnumerable<WorldEventGroup> groups, int waves, long waveMs, WorldEventReward? reward, string? lostFact, int minLevel = 1, WorldEventScar? scar = null)
        {
            MinLevel = minLevel;
            Scar = scar;
            Id = id;
            Kind = kind;
            EveryDays = everyDays;
            ChancePercent = chancePercent;
            FirstDay = firstDay;
            StartMinute = startMinute;
            SpreadMinutes = spreadMinutes;
            AnnounceMinutes = announceMinutes;
            DurationMinutes = durationMinutes;
            AftermathMinutes = aftermathMinutes;
            Weather = weather;
            Place = place;
            Refuge = refuge;
            Villagers = Frozen.List(villagers, nameof(villagers));
            Groups = Frozen.List(groups, nameof(groups));
            Waves = waves;
            WaveMs = waveMs;
            Reward = reward;
            LostFact = lostFact;
            int total = 0;
            foreach (WorldEventGroup g in Groups) total = checked(total + g.Weight);
            TotalWeight = total;
        }

        public static readonly IReadOnlyList<string> KindNames = Array.AsReadOnly(new[] { "raid", "rift", "hunt" });
    }

    /// <summary>
    /// Sous-lot V2.6 (schéma 11, « le Seuil qui grandit ») : la cicatrice qu'une faille restée ouverte laisse à sa place. Elle naît au palier
    /// 1 et gagne un palier tous les <see cref="GrowDays"/> jours du monde, jusqu'à <see cref="MaxStage"/> ; son rayon grandit avec elle. Les
    /// échos qui sortent d'une faille à sa place, et ceux qui rôdent autour d'elle (à partir de <see cref="HauntStage"/>), ont
    /// <see cref="StrengthPercent"/> % de PV et de dégâts en plus par palier au-delà du premier. Elle recule d'un palier quand on referme une
    /// faille à sa place, et disparaît quand on la purifie avec un <see cref="PurifyItem"/>. Valeurs provisoires.
    /// </summary>
    public sealed class WorldEventScar
    {
        public int GrowDays { get; }
        public int MaxStage { get; }

        /// <summary>Rayon de la cicatrice au palier 1, en mètres.</summary>
        public Fixed Radius { get; }

        /// <summary>Rayon gagné à chaque palier au-delà du premier, en mètres.</summary>
        public Fixed RadiusPerStage { get; }

        public int StrengthPercent { get; }

        /// <summary>Palier à partir duquel des échos rôdent autour de la cicatrice (un de plus par palier, au plus les <c>haunts</c> de la variante).</summary>
        public int HauntStage { get; }

        /// <summary>Minutes du monde avant qu'un écho rôdeur vaincu ou enfui revienne.</summary>
        public int HauntRespawnMinutes { get; }

        public string PurifyItem { get; }
        public long PurifyXp { get; }

        /// <summary>Faits appris : la première cicatrice, la première qui atteint <see cref="HauntStage"/>, la première purifiée (ou null).</summary>
        public string? BornFact { get; }
        public string? DeepFact { get; }
        public string? PurifiedFact { get; }

        /// <summary>
        /// Sous-lot V2.6.1 : palier à partir duquel la cicatrice se propage à ses variantes voisines (<see cref="WorldEventGroup.SpreadTo"/>),
        /// 0 sans propagation ; <see cref="SpreadDays"/> jours du monde après l'avoir atteint, une cicatrice naît (palier 1) chez la première
        /// voisine qui n'en a pas ; le délai repart pour la suivante. <see cref="SpreadFact"/> : fait de la première propagation (ou null).
        /// </summary>
        public int SpreadStage { get; }
        public int SpreadDays { get; }
        public string? SpreadFact { get; }

        /// <summary>Sous-lot V2.6.1 : la cicatrice se propage (schéma 12, <c>spreadStage</c> donné).</summary>
        public bool Spreads => SpreadStage > 0;

        internal WorldEventScar(int growDays, int maxStage, Fixed radius, Fixed radiusPerStage, int strengthPercent, int hauntStage, int hauntRespawnMinutes,
            string purifyItem, long purifyXp, string? bornFact, string? deepFact, string? purifiedFact, int spreadStage = 0, int spreadDays = 0, string? spreadFact = null)
        {
            SpreadStage = spreadStage;
            SpreadDays = spreadDays;
            SpreadFact = spreadFact;
            GrowDays = growDays;
            MaxStage = maxStage;
            Radius = radius;
            RadiusPerStage = radiusPerStage;
            StrengthPercent = strengthPercent;
            HauntStage = hauntStage;
            HauntRespawnMinutes = hauntRespawnMinutes;
            PurifyItem = purifyItem;
            PurifyXp = purifyXp;
            BornFact = bornFact;
            DeepFact = deepFact;
            PurifiedFact = purifiedFact;
        }

        /// <summary>Rayon au palier <paramref name="stage"/> (au moins 1).</summary>
        public Fixed RadiusAt(int stage) => Radius + RadiusPerStage * Fixed.FromInt(Math.Max(0, stage - 1));

        /// <summary>Pour cent des PV et des dégâts des échos au palier <paramref name="stage"/> (100 au palier 1 ou sans cicatrice).</summary>
        public long StrengthAt(int stage) => checked(100L + (long)StrengthPercent * Math.Max(0, stage - 1));

        /// <summary>Échos rôdeurs voulus au palier <paramref name="stage"/> pour une variante qui en a <paramref name="available"/>.</summary>
        public int HauntsAt(int stage, int available) => stage < HauntStage ? 0 : Math.Min(available, stage - HauntStage + 1);
    }
}
