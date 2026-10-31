using System;
using System.Collections.Generic;
using EdS.Domain.Builds;
using EdS.Domain.Numerics;
using EdS.Domain.Time;

namespace EdS.Domain.Combat
{
    /// <summary>
    /// Paramètres du moteur de combat : rythme et endurance (lot 5, A3 et A6), limites de simulation, garde,
    /// parade, Charges de rempart, Résonance et états défensifs (lot 6, A1, A2, A4, A5 et A19), catalogue des altérations (lot 7.2).
    /// Valeurs par défaut <b>provisoires</b> ; toutes les durées sont en millisecondes et doivent tomber
    /// exactement sur un tick.
    /// </summary>
    public sealed class CombatRules
    {
        public static readonly CombatRules Provisional = new CombatRules();

        /// <summary>Durée d'un tick logique (50 ms validés ; 20 ms à l'essai plus tard).</summary>
        public int TickMilliseconds { get; init; } = TickRate.DefaultMillisecondsPerTick;

        /// <summary>Temps global de recharge après chaque compétence lancée (A6). 0 = aucun ; au plus <see cref="AbsoluteMaxDurationMs"/>.</summary>
        public long GlobalCooldownMs { get; init; } = 1000;

        /// <summary>Endurance regagnée par seconde (A3), en points entiers.</summary>
        public int EnduranceRegenPerSecond { get; init; } = 10;

        /// <summary>Temps sans dépense d'endurance avant que la régénération démarre (A3) ; au plus <see cref="AbsoluteMaxDurationMs"/>.</summary>
        public long EnduranceRegenDelayMs { get; init; } = 1000;

        /// <summary>Durée maximale d'un combat simulé : borne le nombre de ticks.</summary>
        public long MaxDurationMs { get; init; } = 600_000;

        /// <summary>
        /// Nombre maximal d'événements de simulation comptés en un tick (phases 1 à 6, hors initialisation et finalisation) ;
        /// au-delà le combat est interrompu proprement. Au plus <see cref="MaxWorkPerTick"/> : chaque écriture coûte une unité de travail.
        /// </summary>
        public int MaxEventsPerTick { get; init; } = 1000;

        // Lot 7.1 : admission des scénarios et budgets d'exécution (docs/LIMITES_LOT_7.md). Valeurs configurées provisoires,
        // bornées par des plafonds techniques absolus.

        /// <summary>
        /// Nombre maximal de combattants d'un scénario (refus à la validation). 128 depuis le 29 octobre 2026 (décision de Damien : plus de
        /// réapparitions par session dans les Vallons) ; 64 avant.
        /// </summary>
        public int MaxCombatants { get; init; } = 128;

        /// <summary>Nombre maximal de commandes d'un scénario (refus à la validation).</summary>
        public int MaxCommands { get; init; } = 20_000;

        /// <summary>Nombre maximal de commandes d'un même acteur au même tick (refus à la validation).</summary>
        public int MaxCommandsPerActorPerTick { get; init; } = 4;

        /// <summary>Nombre maximal d'événements en attente dans la file ; au-delà, arrêt <c>queueLimitExceeded</c>.</summary>
        public int MaxPendingEvents { get; init; } = 100_000;

        /// <summary>Budget de travail par tick (unités, voir docs/MOTEUR.md) ; au-delà, arrêt <c>workBudgetExceeded</c>.</summary>
        public int MaxWorkPerTick { get; init; } = 10_000;

        /// <summary>
        /// Profondeur maximale d'une chaîne de réactions (lot 7.3, C6) : une réaction n'est créée que si la profondeur de l'effet
        /// observé + 1 ne la dépasse pas ; sinon <c>triggerSuppressed</c>. 0 supprime toute réaction.
        /// </summary>
        public int MaxTriggerDepth { get; init; } = 4;

        /// <summary>
        /// Sous-lot 8.4 (L8-D2) : recharge interne minimale d'une riposte automatique de zone, c'est-à-dire d'un déclencheur dont un effet
        /// <c>strike</c> vise <c>allEnemies</c> (<c>combat.triggers.areaStrikeMinCooldownMs</c>, 3 s). Un déclencheur en dessous est refusé à
        /// la validation du combat. Une valeur plus basse est une configuration explicite de test ; 0 lève la contrainte.
        /// </summary>
        public long AreaStrikeMinCooldownMs { get; init; } = 3000;

        /// <summary>Plafond technique de <see cref="MaxTriggerDepth"/> (Q10) : la chaîne causale reste une liste de 8 maillons au plus.</summary>
        public const int AbsoluteMaxTriggerDepth = 8;

        /// <summary>Plafonds techniques absolus des limites (enveloppes de pré-alpha, pas des capacités mesurées).</summary>
        public const int AbsoluteMinCombatants = 2, AbsoluteMaxCombatants = 256;
        public const int AbsoluteMaxCommands = 1_000_000;
        public const int AbsoluteMaxCommandsPerActorPerTick = 64;
        public const int AbsoluteMaxPendingEvents = 10_000_000;
        public const int AbsoluteMaxWorkPerTick = 10_000_000;

        /// <summary>Réduction des dégâts directs bloquables, après armure, pendant la garde (A1 : 0.6 = −60 %).</summary>
        public Fixed GuardDamageReduction { get; init; } = Fixed.FromRatio(6, 10);

        /// <summary>
        /// Sous-lot 8.1 (B1) : plafond de la réduction de garde, modificateurs compris (<c>combat.guard.maxDamageReduction</c>, 0.75,
        /// valeur d'équilibrage provisoire). Paramètre des règles depuis la décision du 18/10/2026 (schéma de règles 6) ; la réduction de
        /// base y est tenue par <see cref="Validate"/>.
        /// </summary>
        public Fixed MaxGuardDamageReduction { get; init; } = Fixed.FromRatio(3, 4);

        /// <summary>
        /// Sous-lot 8.1 (B2) : plafond de la réduction totale des dégâts subis (garde, modificateurs et renforcements de dégâts subis,
        /// multiplicatifs), avant l'absorption par un bouclier (sous-lot 8.2) ; <c>combat.damageTaken.maxReduction</c>, 0.85, provisoire.
        /// La parade (0,9) est hors plafond. Paramètre des règles depuis la décision du 18/10/2026 (schéma de règles 6).
        /// </summary>
        public Fixed MaxTotalDamageReduction { get; init; } = Fixed.FromRatio(85, 100);

        /// <summary>Endurance consommée par seconde de garde (A1), prélevée au fil des ticks sans dérive d'arrondi.</summary>
        public int GuardUpkeepPerSecond { get; init; } = 2;

        /// <summary>Endurance payée par impact bloqué (A1) ; si elle manque, la garde est brisée avant la réduction.</summary>
        public int GuardCostPerBlock { get; init; } = 10;

        /// <summary>
        /// Altération appliquée par la rupture de garde (A1, A19 ; lot 7.2) : sa définition dans <see cref="Statuses"/> fixe la
        /// durée et les blocages (ni garde ni parade, pas de régénération).
        /// </summary>
        public string GuardBreakStatus { get; init; } = "guardBreak";

        /// <summary>Fenêtre d'une parade parfaite (A2) : ticks [commande, commande + fenêtre[.</summary>
        public long ParryWindowMs { get; init; } = 150;

        /// <summary>Réduction des dégâts directs éligibles par une parade parfaite (A2 : 0.9 = −90 %).</summary>
        public Fixed ParryDamageReduction { get; init; } = Fixed.FromRatio(9, 10);

        /// <summary>Délai après une parade ratée avant de pouvoir parer de nouveau (A2), compté depuis la fin de la fenêtre.</summary>
        public long ParryFailureRecoveryMs { get; init; } = 500;

        /// <summary>Charges de rempart gagnées par parade parfaite réussie (A2, A4).</summary>
        public int RampartChargesPerParry { get; init; } = 1;

        /// <summary>Maximum de Charges de rempart (A4) ; l'excédent est perdu.</summary>
        public int RampartChargesMax { get; init; } = 5;

        /// <summary>Maximum de Résonance (A5).</summary>
        public int ResonanceMax { get; init; } = 100;

        /// <summary>Résonance gagnée par attaque directe réussie, une fois par action quel que soit le nombre de cibles (A5).</summary>
        public int ResonancePerDirectHit { get; init; } = 3;

        /// <summary>Résonance gagnée par parade parfaite réussie (A5).</summary>
        public int ResonancePerPerfectParry { get; init; } = 8;

        /// <summary>Résonance gagnée par blocage normal réussi (A5).</summary>
        public int ResonancePerBlock { get; init; } = 2;

        /// <summary>Résonance dépensée par l'ultime (A5).</summary>
        public int UltimateResonanceCost { get; init; } = 100;

        /// <summary>
        /// Sous-lot 11.4 : vitesse de marche de base d'un combattant positionné, en m/s (<c>combat.movement.speed</c>). Valeur de départ
        /// retenue le 20/10/2026 : 5 m/s, celle du prototype Unity ; réglée à l'essai 11.9. Dans [0,1 ; 20] m/s (borne technique).
        /// </summary>
        public Fixed MovementSpeed { get; init; } = Fixed.FromInt(5);

        /// <summary>
        /// Sous-lot 11.4 (L11-D3) : facteur de vitesse pendant la garde (<c>combat.movement.guardSpeedFactor</c>). Valeur de départ retenue :
        /// la moitié ; la valeur finale est fixée à l'essai 11.9. Dans [0, 1].
        /// </summary>
        public Fixed GuardSpeedFactor { get; init; } = Fixed.FromRatio(1, 2);

        /// <summary>
        /// Sous-lot 11.4 (L11-D5) : règle de contact entre joueurs et créatures (<c>combat.contact.playerCreature</c>), A ou B, choisie par
        /// Damien après l'essai du 11.6. Le moteur réalise les deux ; la valeur livrée n'est qu'un point de départ.
        /// </summary>
        public ContactRule PlayerCreatureContact { get; init; } = ContactRule.Solid;

        /// <summary>
        /// Sous-lot 11.4, règle B : facteur de vitesse d'un joueur qui traverse une créature (<c>combat.contact.crossingSpeedFactor</c>).
        /// Proposition de départ : 0,3 (« ralentit fortement »). Dans ]0, 1] : un joueur n'est jamais immobilisé par un contact.
        /// </summary>
        public Fixed CrossingSpeedFactor { get; init; } = Fixed.FromRatio(3, 10);

        /// <summary>
        /// Sous-lot 11.4 : rayon d'emprise à partir duquel une créature est « grande » (<c>combat.contact.largeCreatureRadius</c>) : elle
        /// reste solide pour les joueurs quelle que soit la règle (plan du lot 11, section 4.1). Proposition de départ : 1 m (un chef fait
        /// 1,2 m). Dans [0,1 ; 5] m.
        /// </summary>
        public Fixed LargeCreatureRadius { get; init; } = Fixed.One;

        /// <summary>
        /// Sous-lot 11.4c (L11-D3, retenu par Damien le 20/10/2026) : ouverture, en degrés, de l'arc frontal du bouclier
        /// (<c>combat.guard.arcDegrees</c>) : garde et parade parfaite ne valent que contre un coup venu de cet arc, centré sur
        /// l'orientation du défenseur. Valeur retenue : 180. Dans [1, 360] (360 : protection de tous côtés).
        /// </summary>
        public int GuardArcDegrees { get; init; } = 180;

        /// <summary>
        /// Sous-lot 11.4c : temps de réaction minimal d'une annonce (<c>combat.telegraph.minReactionMs</c>), ajouté au temps de sortie
        /// de la forme pour juger qu'une zone est évitable. Valeur de départ retenue : 300 ms, réglée à l'essai 11.9. Dans [0, 10 000] ms.
        /// </summary>
        public long TelegraphMinReactionMs { get; init; } = 300;

        /// <summary>Plafond technique de <see cref="TelegraphMinReactionMs"/>.</summary>
        public const long MaxTelegraphReactionMs = 10_000;

        // Sous-lot V1.2 (schéma de règles 13, bloc combat.shield) : bornes de jeu de la garde propre d'un bouclier (ShieldGuard). Un bouclier
        // lourd bloque plus et coûte plus d'endurance ; un bouclier léger rend la parade un peu plus facile. Valeurs provisoires.

        /// <summary>Part bloquée la plus faible d'un bouclier (<c>combat.shield.minDamageReduction</c>, 0,45).</summary>
        public Fixed ShieldMinDamageReduction { get; init; } = Fixed.FromRatio(45, 100);

        /// <summary>
        /// Part bloquée la plus forte d'un bouclier (<c>combat.shield.maxDamageReduction</c>, 0,7), sous le plafond de la garde livré
        /// (<see cref="MaxGuardDamageReduction"/>, 0,75) : les talents de garde gardent de la place au-dessus ; au-delà, la garde effective
        /// reste écrêtée à ce plafond.
        /// </summary>
        public Fixed ShieldMaxDamageReduction { get; init; } = Fixed.FromRatio(7, 10);

        /// <summary>Coût d'un blocage le plus bas d'un bouclier (<c>combat.shield.minCostPerBlock</c>, 4).</summary>
        public int ShieldMinCostPerBlock { get; init; } = 4;

        /// <summary>Coût d'un blocage le plus haut d'un bouclier (<c>combat.shield.maxCostPerBlock</c>, 20).</summary>
        public int ShieldMaxCostPerBlock { get; init; } = 20;

        /// <summary>
        /// Écart le plus grand entre la fenêtre de parade d'un bouclier et celle des règles (<c>combat.shield.parryWindowShiftMs</c>, 50 ms :
        /// un tick), dans un sens ou dans l'autre ; multiple du tick.
        /// </summary>
        public long ShieldParryWindowShiftMs { get; init; } = 50;

        /// <summary>Plafond technique des coûts de blocage d'un bouclier.</summary>
        public const int MaxShieldCostPerBlock = 1_000_000;

        // Sous-lot V1.3 (schéma de règles 14, bloc combat.equipment) : bornes de jeu des effets d'équipement (EquipmentEffect : objets nommés,
        // paliers d'ensemble). Une seule fonction les vérifie (EquipmentEffect.Check), pour le catalogue des objets et pour la validation du
        // combat. Valeurs provisoires, réglées à l'équilibrage (V1.8).

        /// <summary>Bonus de dégâts infligés d'un modificateur d'équipement, au plus (<c>combat.equipment.maxDamageDealt</c>, 0,25).</summary>
        public Fixed EquipmentMaxDamageDealt { get; init; } = Fixed.FromRatio(1, 4);

        /// <summary>Réduction des dégâts subis d'un modificateur d'équipement, au plus (<c>combat.equipment.maxDamageTaken</c>, 0,15).</summary>
        public Fixed EquipmentMaxDamageTaken { get; init; } = Fixed.FromRatio(15, 100);

        /// <summary>Pourcentage de PV maximaux d'un modificateur d'équipement, au plus (<c>combat.equipment.maxHealthPercent</c>, 0,15).</summary>
        public Fixed EquipmentMaxHealthPercent { get; init; } = Fixed.FromRatio(15, 100);

        /// <summary>Régénération d'endurance ajoutée par un modificateur d'équipement, au plus par seconde (<c>combat.equipment.maxEnduranceRegen</c>, 3).</summary>
        public Fixed EquipmentMaxEnduranceRegen { get; init; } = Fixed.FromInt(3);

        /// <summary>Allongement d'une altération posée (<c>status.&lt;id&gt;.durationDealt</c>), au plus (<c>combat.equipment.maxStatusDurationMs</c>, 1 000 ms).</summary>
        public long EquipmentMaxStatusDurationMs { get; init; } = 1000;

        /// <summary>Coefficient d'une frappe de réaction d'équipement, au plus (<c>combat.equipment.maxStrikeCoefficient</c>, 1).</summary>
        public Fixed EquipmentMaxStrikeCoefficient { get; init; } = Fixed.One;

        /// <summary>Gain de ressource d'une réaction d'équipement, au plus (<c>combat.equipment.maxGain</c>, 2).</summary>
        public int EquipmentMaxGain { get; init; } = 2;

        /// <summary>Recharge interne d'un déclencheur d'équipement, au moins (<c>combat.equipment.minTriggerCooldownMs</c>, 1 000 ms).</summary>
        public long EquipmentMinTriggerCooldownMs { get; init; } = 1000;

        /// <summary>
        /// Définitions des altérations (lot 7.2, bloc <c>statuses</c> de <c>rules.json</c>) : DOT, contrôles, Fragilité, rupture
        /// de garde, immunités. Validées par <see cref="StatusCatalog.Validate"/>, chemins rattachés à <c>statuses[i]</c>.
        /// </summary>
        public StatusCatalog Statuses { get; init; } = StatusCatalog.Provisional;

        /// <summary>
        /// Plafond absolu (24 h) de <see cref="MaxDurationMs"/> et de toutes les durées des règles et des compétences, pour que
        /// les compteurs de ticks restent petits. Enveloppe technique provisoire de pré-alpha, pas une valeur d'équilibrage.
        /// </summary>
        public const long AbsoluteMaxDurationMs = 86_400_000;

        public TickRate Tick => new TickRate(TickMilliseconds);

        /// <summary>
        /// Sous-lot 11.4 (L11-D5) : deux combattants positionnés sont-ils solides l'un pour l'autre ? Joueurs (personnages) entre eux :
        /// jamais, ni blocage ni poussée. Créatures (assaillants, mannequins) entre elles : toujours, chacune évite l'autre sans la pousser.
        /// Joueur et créature : selon <see cref="PlayerCreatureContact"/> (A solide, B traversable), sauf une grande créature
        /// (<see cref="LargeCreatureRadius"/>), toujours solide.
        /// </summary>
        public bool AreSolid(bool firstIsPlayer, Fixed firstRadius, bool secondIsPlayer, Fixed secondRadius)
        {
            if (firstIsPlayer && secondIsPlayer) return false;
            if (!firstIsPlayer && !secondIsPlayer) return true;
            Fixed creature = firstIsPlayer ? secondRadius : firstRadius;
            return PlayerCreatureContact == ContactRule.Solid || creature >= LargeCreatureRadius;
        }

        /// <summary>Liste des incohérences ; vide si les règles sont utilisables.</summary>
        public IReadOnlyList<RuleViolation> Validate()
        {
            var errors = new List<RuleViolation>();
            bool tickValid = TickMilliseconds > 0 && 1000 % TickMilliseconds == 0;
            if (!tickValid)
                errors.Add(new RuleViolation("la durée du tick doit être un diviseur strictement positif de 1000 ms.", nameof(TickMilliseconds)));

            void Duration(long value, string name, bool strictlyPositive)
            {
                if (value < 0 || (strictlyPositive && value == 0))
                    errors.Add(new RuleViolation(strictlyPositive ? "doit être strictement positive." : "ne peut pas être négative.", name));
                else if (tickValid && value % TickMilliseconds != 0)
                    errors.Add(new RuleViolation($"{value} ms n'est pas un multiple de la durée du tick ({TickMilliseconds} ms).", name, nameof(TickMilliseconds)));
            }

            Duration(GlobalCooldownMs, nameof(GlobalCooldownMs), false);
            Duration(EnduranceRegenDelayMs, nameof(EnduranceRegenDelayMs), false);
            Duration(MaxDurationMs, nameof(MaxDurationMs), true);
            if (MaxDurationMs > AbsoluteMaxDurationMs)
                errors.Add(new RuleViolation($"ne peut pas dépasser {AbsoluteMaxDurationMs} ms (24 h).", nameof(MaxDurationMs)));
            Duration(ParryWindowMs, nameof(ParryWindowMs), true);
            Duration(ParryFailureRecoveryMs, nameof(ParryFailureRecoveryMs), false);
            Duration(AreaStrikeMinCooldownMs, nameof(AreaStrikeMinCooldownMs), false);
            // Correctif 6.1.1 (CA-M01 C et D) : la recharge globale et le délai de régénération sont bornés comme les autres
            // durées. Avec un tick de 1 ms, dernier tick possible + délai en ticks ≤ 2 × 86 400 000 : très loin de 2⁶³.
            foreach (var (value, name) in new[] { (GlobalCooldownMs, nameof(GlobalCooldownMs)), (EnduranceRegenDelayMs, nameof(EnduranceRegenDelayMs)),
                (ParryWindowMs, nameof(ParryWindowMs)), (ParryFailureRecoveryMs, nameof(ParryFailureRecoveryMs)),
                (AreaStrikeMinCooldownMs, nameof(AreaStrikeMinCooldownMs)) })
                if (value > AbsoluteMaxDurationMs)
                    errors.Add(new RuleViolation($"ne peut pas dépasser {AbsoluteMaxDurationMs} ms (24 h).", name));

            void Fraction(Fixed value, string name)
            {
                if (value < Fixed.Zero || value > Fixed.One)
                    errors.Add(new RuleViolation("doit être comprise entre 0 et 1.", name));
            }
            Fraction(GuardDamageReduction, nameof(GuardDamageReduction));
            Fraction(MaxGuardDamageReduction, nameof(MaxGuardDamageReduction));
            Fraction(MaxTotalDamageReduction, nameof(MaxTotalDamageReduction));
            if (GuardDamageReduction > MaxGuardDamageReduction && GuardDamageReduction <= Fixed.One
                && MaxGuardDamageReduction >= Fixed.Zero && MaxGuardDamageReduction <= Fixed.One)
                errors.Add(new RuleViolation($"ne peut pas dépasser le plafond de la réduction de garde ({MaxGuardDamageReduction}).",
                    nameof(GuardDamageReduction), nameof(MaxGuardDamageReduction)));
            Fraction(ParryDamageReduction, nameof(ParryDamageReduction));
            if (Statuses is null)
                errors.Add(new RuleViolation("catalogue des altérations obligatoire.", nameof(GuardBreakStatus)));
            else if (GuardBreakStatus is null || !Statuses.TryGet(GuardBreakStatus, out StatusDefinition? guardBreak))
                errors.Add(new RuleViolation(RuleViolationKind.UnknownReference, $"altération inconnue « {GuardBreakStatus} ».", nameof(GuardBreakStatus)));
            // Revue Codex de la PR #7 (corrigée au sous-lot 8.4, décision de Damien) : seulement un contrôle ou un modificateur. Les
            // catégories ajoutées depuis (renforcement, bouclier, provocation, protection) seraient refusées ou sans effet à la rupture
            // (provocation et protection exigent une source alliée ou assaillante, un bouclier un coefficient) : la garde se briserait
            // sans poser l'état censé bloquer garde, parade et régénération.
            else if (guardBreak.Category != StatusCategory.Control && guardBreak.Category != StatusCategory.Modifier)
                errors.Add(new RuleViolation($"la rupture de garde doit être un contrôle ou un modificateur (catégorie « {StatusIds.Of(guardBreak.Category)} » refusée).",
                    nameof(GuardBreakStatus)));

            foreach (var (value, name) in new[]
            {
                (GuardUpkeepPerSecond, nameof(GuardUpkeepPerSecond)), (GuardCostPerBlock, nameof(GuardCostPerBlock)),
                (RampartChargesPerParry, nameof(RampartChargesPerParry)), (RampartChargesMax, nameof(RampartChargesMax)),
                (ResonanceMax, nameof(ResonanceMax)), (ResonancePerDirectHit, nameof(ResonancePerDirectHit)),
                (ResonancePerPerfectParry, nameof(ResonancePerPerfectParry)), (ResonancePerBlock, nameof(ResonancePerBlock)),
                (UltimateResonanceCost, nameof(UltimateResonanceCost)),
            })
                if (value < 0) errors.Add(new RuleViolation("ne peut pas être négatif.", name));
            if (UltimateResonanceCost > ResonanceMax)
                errors.Add(new RuleViolation("le coût de l'ultime dépasse la Résonance maximale : l'ultime serait impossible.", nameof(UltimateResonanceCost), nameof(ResonanceMax)));

            if (EnduranceRegenPerSecond < 0)
                errors.Add(new RuleViolation("ne peut pas être négative.", nameof(EnduranceRegenPerSecond)));
            void Range(int value, int min, int max, string name)
            {
                if (value < min || value > max)
                    errors.Add(new RuleViolation($"doit être compris entre {min} et {max} (plafond technique).", name));
            }
            Range(MaxCombatants, AbsoluteMinCombatants, AbsoluteMaxCombatants, nameof(MaxCombatants));
            Range(MaxCommands, 0, AbsoluteMaxCommands, nameof(MaxCommands));
            Range(MaxCommandsPerActorPerTick, 1, AbsoluteMaxCommandsPerActorPerTick, nameof(MaxCommandsPerActorPerTick));
            Range(MaxPendingEvents, 1, AbsoluteMaxPendingEvents, nameof(MaxPendingEvents));
            Range(MaxWorkPerTick, 1, AbsoluteMaxWorkPerTick, nameof(MaxWorkPerTick));
            Range(MaxTriggerDepth, 0, AbsoluteMaxTriggerDepth, nameof(MaxTriggerDepth));
            if (MaxEventsPerTick < 1)
                errors.Add(new RuleViolation("doit valoir au moins 1.", nameof(MaxEventsPerTick)));
            else if (MaxWorkPerTick >= 1 && MaxEventsPerTick > MaxWorkPerTick)
                errors.Add(new RuleViolation("ne peut pas dépasser le budget de travail par tick : chaque événement coûte une unité de travail.",
                    nameof(MaxEventsPerTick), nameof(MaxWorkPerTick)));

            // Sous-lot 11.4 : déplacement et contact.
            if (MovementSpeed < Space.SpaceLimits.MinSpeed || MovementSpeed > Space.SpaceLimits.MaxSpeed)
                errors.Add(new RuleViolation($"doit être comprise entre {Space.SpaceLimits.MinSpeed} et {Space.SpaceLimits.MaxSpeed} m/s (borne technique).", nameof(MovementSpeed)));
            Fraction(GuardSpeedFactor, nameof(GuardSpeedFactor));
            if (CrossingSpeedFactor <= Fixed.Zero || CrossingSpeedFactor > Fixed.One)
                errors.Add(new RuleViolation("doit être comprise dans ]0, 1] : un joueur n'est jamais immobilisé par un contact.", nameof(CrossingSpeedFactor)));
            if (!Enum.IsDefined(typeof(ContactRule), PlayerCreatureContact))
                errors.Add(new RuleViolation("règle de contact inconnue.", nameof(PlayerCreatureContact)));
            if (LargeCreatureRadius < Space.SpaceLimits.MinRadius || LargeCreatureRadius > Space.SpaceLimits.MaxRadius)
                errors.Add(new RuleViolation($"doit être compris entre {Space.SpaceLimits.MinRadius} et {Space.SpaceLimits.MaxRadius} m (borne technique).", nameof(LargeCreatureRadius)));
            // Sous-lot 11.4c : arc du bouclier et réaction minimale des annonces.
            Range(GuardArcDegrees, 1, 360, nameof(GuardArcDegrees));
            if (TelegraphMinReactionMs < 0 || TelegraphMinReactionMs > MaxTelegraphReactionMs)
                errors.Add(new RuleViolation($"doit être compris entre 0 et {MaxTelegraphReactionMs} ms (plafond technique).", nameof(TelegraphMinReactionMs)));
            // Sous-lot V1.2 : bornes de la garde des boucliers.
            Fraction(ShieldMinDamageReduction, nameof(ShieldMinDamageReduction));
            Fraction(ShieldMaxDamageReduction, nameof(ShieldMaxDamageReduction));
            if (ShieldMinDamageReduction > ShieldMaxDamageReduction)
                errors.Add(new RuleViolation("la part bloquée la plus faible dépasse la plus forte.", nameof(ShieldMinDamageReduction), nameof(ShieldMaxDamageReduction)));
            // Une part au-dessus du plafond de la garde (règles d'essai) reste écrêtée par CombatantRules, comme une base des règles.
            Range(ShieldMinCostPerBlock, 0, MaxShieldCostPerBlock, nameof(ShieldMinCostPerBlock));
            Range(ShieldMaxCostPerBlock, 0, MaxShieldCostPerBlock, nameof(ShieldMaxCostPerBlock));
            if (ShieldMinCostPerBlock > ShieldMaxCostPerBlock)
                errors.Add(new RuleViolation("le coût de blocage le plus bas dépasse le plus haut.", nameof(ShieldMinCostPerBlock), nameof(ShieldMaxCostPerBlock)));
            // L'écart n'a pas à tomber sur le tick des règles : la fenêtre d'un bouclier, elle, est un multiple du tick d'au moins un tick
            // (ShieldGuard.Check), si bien que des règles d'essai à un autre tick restent valides.
            if (ShieldParryWindowShiftMs < 0 || ShieldParryWindowShiftMs > CombatantRules.MaxParryWindowMs)
                errors.Add(new RuleViolation($"doit être compris entre 0 et {CombatantRules.MaxParryWindowMs} ms (fenêtre de parade la plus large).", nameof(ShieldParryWindowShiftMs)));
            // Sous-lot V1.3 : bornes des effets d'équipement (enveloppes techniques : celles des clés de modificateur et des déclencheurs).
            void Bounded(Fixed value, Fixed max, string name)
            {
                if (value < Fixed.Zero || value > max)
                    errors.Add(new RuleViolation($"doit être compris entre 0 et {max} (plafond technique).", name));
            }
            Bounded(EquipmentMaxDamageDealt, Fixed.FromInt(10), nameof(EquipmentMaxDamageDealt));
            Fraction(EquipmentMaxDamageTaken, nameof(EquipmentMaxDamageTaken));
            Bounded(EquipmentMaxHealthPercent, Fixed.FromInt(10), nameof(EquipmentMaxHealthPercent));
            Bounded(EquipmentMaxEnduranceRegen, Fixed.FromInt(1_000_000), nameof(EquipmentMaxEnduranceRegen));
            Bounded(EquipmentMaxStrikeCoefficient, SkillCombatDefinition.MaxCoefficient, nameof(EquipmentMaxStrikeCoefficient));
            Range(EquipmentMaxGain, 0, SkillCombatDefinition.MaxCost, nameof(EquipmentMaxGain));
            foreach (var (value, name) in new[] { (EquipmentMaxStatusDurationMs, nameof(EquipmentMaxStatusDurationMs)),
                (EquipmentMinTriggerCooldownMs, nameof(EquipmentMinTriggerCooldownMs)) })
                if (value < 0 || value > AbsoluteMaxDurationMs)
                    errors.Add(new RuleViolation($"doit être compris entre 0 et {AbsoluteMaxDurationMs} ms (24 h).", name));
            return errors;
        }
    }

    /// <summary>
    /// Sous-lot 11.4 (L11-D5) : contact entre un joueur et une créature (plan du lot 11, section 4.1). Les joueurs ne se bloquent jamais
    /// entre eux ; les créatures sont solides entre elles ; les grandes créatures restent solides pour les joueurs dans les deux règles.
    /// </summary>
    public enum ContactRule
    {
        /// <summary>Règle A, « volumes solides sans poussée » (<c>solid</c>) : on glisse le long de l'emprise, avec le garde-fou anti-piège.</summary>
        Solid = 0,

        /// <summary>Règle B, « traversée freinée » (<c>slowedCrossing</c>) : chevauchement permis, le joueur qui traverse est ralenti.</summary>
        SlowedCrossing = 1,
    }

    /// <summary>Identifiants stables des règles de contact dans les données et le journal.</summary>
    public static class ContactRuleIds
    {
        public const string Solid = "solid";
        public const string SlowedCrossing = "slowedCrossing";

        public static IReadOnlyList<string> All { get; } = Collections.Frozen.List(new[] { Solid, SlowedCrossing }, nameof(All));

        public static string Of(ContactRule rule) => rule switch
        {
            ContactRule.Solid => Solid,
            ContactRule.SlowedCrossing => SlowedCrossing,
            _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, null),
        };

        public static ContactRule Parse(string id) => id switch
        {
            Solid => ContactRule.Solid,
            SlowedCrossing => ContactRule.SlowedCrossing,
            _ => throw new ArgumentException($"règle de contact inconnue « {id} ».", nameof(id)),
        };
    }
}
