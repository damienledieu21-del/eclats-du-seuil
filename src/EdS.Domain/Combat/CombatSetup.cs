using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EdS.Domain;
using EdS.Domain.Builds;
using EdS.Domain.Collections;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Domain.Stats;

namespace EdS.Domain.Combat
{
    // Description d'un combat (participants, commandes, règles, identité des données), consommée par le moteur
    // d'EdS.Simulation. Placée dans le domaine pour que la couche de données construise un combat sans dépendre
    // du moteur (lot 6).

    /// <summary>Participant d'un combat. Les identifiants et équipes sont en minuscules (<c>bastion</c>, <c>joueurs</c>).</summary>
    public abstract class CombatantSetup
    {
        public string Id { get; }
        public string Team { get; }

        /// <summary>Ordre des commandes simultanées : la plus grande initiative agit d'abord, puis l'identifiant (ordinal).</summary>
        public int Initiative { get; }

        /// <summary>
        /// Sous-lot 11.4 : place de départ dans un combat positionné (<see cref="CombatSetup.Space"/>) ; null dans un combat sans espace.
        /// Tous les combattants d'un combat positionné en ont une, aucun d'un combat sans espace.
        /// </summary>
        public CombatantPlacement? Placement { get; }

        /// <summary>
        /// Sous-lot 11.4d : instant d'arrivée en cours de combat (multiple du tick, dans [0, durée[) ; null pour un combattant présent dès
        /// le départ. Avant son arrivée, le combattant est absent : il n'agit pas, n'est ni ciblé, ni solide, ni compté dans un groupe. Le
        /// moteur l'admet au début du tick d'arrivée (phase <c>setup</c>), à sa place, si aucune emprise solide ne l'occupe.
        /// </summary>
        public long? ArrivalMs { get; }

        protected CombatantSetup(string id, string team, int initiative, CombatantPlacement? placement = null, long? arrivalMs = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Team = team ?? throw new ArgumentNullException(nameof(team));
            Initiative = initiative;
            Placement = placement;
            ArrivalMs = arrivalMs;
        }
    }

    /// <summary>Personnage joué : sa classe et son build (attributs explicites, talents, compétences équipées).</summary>
    public sealed class CharacterSetup : CombatantSetup
    {
        public ClassDefinition Class { get; }
        public CharacterBuild Build { get; }

        public CharacterSetup(string id, string team, ClassDefinition definition, CharacterBuild build, int initiative = 0, CombatantPlacement? placement = null,
            long? arrivalMs = null)
            : base(id, team, initiative, placement, arrivalMs)
        {
            Class = definition ?? throw new ArgumentNullException(nameof(definition));
            Build = build ?? throw new ArgumentNullException(nameof(build));
        }

        /// <summary>
        /// Lot 7.3 : déclencheurs permanents actifs avec ce build : talents de rang ≥ 1 (valeurs à leur rang), passifs équipés,
        /// actives et ultime équipées (Q2). Dans l'ordre de la classe ; le moteur trie les abonnés selon l'ordre total (§5.4).
        /// Une référence inconnue (build invalide) est ignorée : la validation du build la signale.
        /// </summary>
        public IEnumerable<BoundTrigger> ActiveTriggers()
        {
            foreach (TalentTreeDefinition tree in Class.Trees)
                foreach (TalentDefinition talent in tree.Talents)
                    if (Build.TalentRanks.TryGetValue(talent.Id, out int rank) && rank >= 1)
                        for (int k = 0; k < talent.Triggers.Count; k++)
                            yield return new BoundTrigger(TriggerOrigin.Talent, talent.Id, k, rank, talent.Triggers[k]);
            var equipped = new HashSet<string>(Build.ActiveSkills.Concat(Build.PassiveSkills), StringComparer.Ordinal);
            if (Build.Ultimate != null) equipped.Add(Build.Ultimate);
            foreach (SkillDefinition skill in Class.Skills)
                if (equipped.Contains(skill.Id))
                    for (int k = 0; k < skill.Triggers.Count; k++)
                        yield return new BoundTrigger(skill.Kind == SkillKind.Passive ? TriggerOrigin.Passive : TriggerOrigin.Skill, skill.Id, k, 1, skill.Triggers[k]);
        }

        /// <summary>
        /// Sous-lot 8.1 : modificateurs actifs avec ce build : talents de rang ≥ 1 (valeurs à leur rang), puis compétences
        /// <b>passives</b> équipées (rang 1), dans l'ordre de la classe. Une référence inconnue (build invalide) est ignorée : la
        /// validation du build la signale. Agrégés par <see cref="ModifierSet.For"/>, sans effet d'ordre.
        /// </summary>
        public IEnumerable<BoundModifier> ActiveModifiers()
        {
            foreach (TalentTreeDefinition tree in Class.Trees)
                foreach (TalentDefinition talent in tree.Talents)
                    if (Build.TalentRanks.TryGetValue(talent.Id, out int rank) && rank >= 1)
                        for (int k = 0; k < talent.Modifiers.Count; k++)
                            yield return new BoundModifier(talent.Id, k, rank, talent.Modifiers[k]);
            var passives = new HashSet<string>(Build.PassiveSkills, StringComparer.Ordinal);
            foreach (SkillDefinition skill in Class.Skills)
                if (skill.Kind == SkillKind.Passive && passives.Contains(skill.Id))
                    for (int k = 0; k < skill.Modifiers.Count; k++)
                        yield return new BoundModifier(skill.Id, k, 1, skill.Modifiers[k]);
        }
    }

    /// <summary>Sous-lot 8.1 : modificateur rattaché à son talent ou à sa compétence passive, avec le rang qui fixe sa valeur.</summary>
    public readonly struct BoundModifier
    {
        public string DefinitionId { get; }
        public int Index { get; }
        public int Rank { get; }
        public ModifierDefinition Definition { get; }

        public BoundModifier(string definitionId, int index, int rank, ModifierDefinition definition)
        {
            DefinitionId = definitionId ?? throw new ArgumentNullException(nameof(definitionId));
            Index = index;
            Rank = rank;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        /// <summary>Valeur au rang du build (arithmétique vérifiée, bornée par la validation de la classe).</summary>
        public Fixed Value => Definition.Add.At(Rank);
    }

    /// <summary>
    /// Lot 7.3 : déclencheur rattaché à son origine (talent, compétence, assaillant) avec le rang qui fixe ses valeurs. L'identité
    /// journalisée est <c>définition/indice@porteur#instance</c> ; l'instance n'est connue qu'en combat (altérations).
    /// </summary>
    public readonly struct BoundTrigger
    {
        public TriggerOrigin Origin { get; }
        public string DefinitionId { get; }
        public int Index { get; }
        public int Rank { get; }
        public TriggerDefinition Definition { get; }

        public BoundTrigger(TriggerOrigin origin, string definitionId, int index, int rank, TriggerDefinition definition)
        {
            Origin = origin;
            DefinitionId = definitionId ?? throw new ArgumentNullException(nameof(definitionId));
            Index = index;
            Rank = rank;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        public string Label => $"{DefinitionId}/{Index}";
    }

    /// <summary>Mannequin d'entraînement : PV infinis, défenses configurables, n'agit jamais.</summary>
    public sealed class DummySetup : CombatantSetup
    {
        public Fixed Armor { get; }
        public Fixed MagicResistance { get; }

        /// <summary>Résistance aux altérations (lot 7.2, D12) : réduit la durée des contrôles ; 0 par défaut.</summary>
        public Fixed StatusResistance { get; }

        public DummySetup(string id, string team, Fixed armor, Fixed magicResistance, int initiative = 0, Fixed statusResistance = default,
            CombatantPlacement? placement = null, long? arrivalMs = null)
            : base(id, team, initiative, placement, arrivalMs)
        {
            Armor = armor;
            MagicResistance = magicResistance;
            StatusResistance = statusResistance;
        }
    }

    /// <summary>Attaque d'un assaillant d'entraînement : dégâts bruts (avant défense), sans critique.</summary>
    public sealed class AssailantAttack
    {
        public string Id { get; }

        /// <summary>Dégâts bruts avant armure ou résistance magique (A21 : 100 en référence).</summary>
        public long RawDamage { get; }

        public DamageType DamageType { get; }

        /// <summary><see cref="SkillTargeting.Enemy"/> : la cible de l'assaillant ; <see cref="SkillTargeting.AllEnemies"/> : tous les ennemis vivants.</summary>
        public SkillTargeting Targeting { get; }

        /// <summary>Imparable : ignore garde et parade.</summary>
        public bool Unblockable { get; }

        /// <summary>
        /// Altérations appliquées (ou renouvelées) sur chaque cible touchée, après les dégâts de l'impact (lot 7.2 ; remplace le
        /// booléen <c>appliesFragility</c>). Pour un DOT, la puissance de l'assaillant est ses dégâts bruts.
        /// </summary>
        public IReadOnlyList<StatusApplication> Applies { get; }

        /// <summary>
        /// Sous-lot 11.4c : forme dans un combat positionné ; null : attaque historique (la cible, ou tous les ennemis, sans géométrie).
        /// Voir <see cref="AttackShape"/>.
        /// </summary>
        public AttackShape? Shape { get; }

        /// <summary>Sous-lot 11.4c : durée de l'annonce, en ms (multiple du tick) ; 0 : l'impact suit le tour, comme avant.</summary>
        public long TelegraphMs { get; }

        /// <summary>Coup dirigé : portée entre les bords des emprises (null : sans limite) ; cône et bande : longueur depuis l'assaillant.</summary>
        public Fixed? Range { get; }

        /// <summary>Cercle et zone persistante : rayon.</summary>
        public Fixed? Radius { get; }

        /// <summary>Cône : ouverture en degrés, de 1 à 180.</summary>
        public int? Angle { get; }

        /// <summary>Bande : largeur.</summary>
        public Fixed? Width { get; }

        /// <summary>Cercle et zone persistante : centre (la cible, ou l'assaillant), verrouillé au début de l'annonce.</summary>
        public ShapeCenter Center { get; }

        /// <summary>Zone persistante : durée en ms (multiple du tick) et cadence de ses impacts (<c>tickMs</c> des données).</summary>
        public long PersistentMs { get; }
        public long PulseMs { get; }

        /// <summary>Valeurs données de <c>guardable</c> et <c>parryable</c> (null : valeur par défaut de la forme).</summary>
        public bool? GuardableOverride { get; }
        public bool? ParryableOverride { get; }

        /// <summary>
        /// Sous-lot 11.4c : la garde réduit-elle les dégâts ? Par défaut : oui pour un coup dirigé ou une attaque historique non imparable,
        /// non pour une zone (« souffle ou onde : garde seulement si l'attaque le dit ») ; jamais pour une zone persistante.
        /// </summary>
        public bool Guardable => GuardableOverride ?? (!Unblockable && !IsZone);

        /// <summary>La parade parfaite s'applique-t-elle ? Par défaut : oui pour un coup dirigé non imparable ; jamais pour une zone.</summary>
        public bool Parryable => ParryableOverride ?? (!Unblockable && !IsZone);

        /// <summary>Cône, cercle, bande ou zone persistante : frappe ce qui est dedans à l'impact.</summary>
        public bool IsZone => Shape is AttackShape.Cone or AttackShape.Circle or AttackShape.Line or AttackShape.Persistent;

        public AssailantAttack(string id, long rawDamage, DamageType damageType = DamageType.Physique,
            SkillTargeting targeting = SkillTargeting.Enemy, bool unblockable = false, IEnumerable<StatusApplication>? applies = null,
            AttackShape? shape = null, long telegraphMs = 0, Fixed? range = null, Fixed? radius = null, int? angle = null, Fixed? width = null,
            ShapeCenter center = ShapeCenter.Target, long persistentMs = 0, long pulseMs = 0, bool? guardable = null, bool? parryable = null,
            bool charge = false)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Charge = charge;
            RawDamage = rawDamage;
            DamageType = damageType;
            Targeting = targeting;
            Unblockable = unblockable;
            Applies = Frozen.ListOrEmpty(applies, nameof(applies));
            Shape = shape;
            TelegraphMs = telegraphMs;
            Range = range;
            Radius = radius;
            Angle = angle;
            Width = width;
            Center = center;
            PersistentMs = persistentMs;
            PulseMs = pulseMs;
            GuardableOverride = guardable;
            ParryableOverride = parryable;
        }

        /// <summary>Nombre d'impacts d'une zone persistante (à l'impact, puis toutes les <see cref="PulseMs"/> pendant <see cref="PersistentMs"/>).</summary>
        public long Pulses => Shape == AttackShape.Persistent && PulseMs > 0 ? (PersistentMs - 1) / PulseMs + 1 : 1;

        /// <summary>
        /// Sous-lot 11.5 : charge. Une bande (<see cref="AttackShape.Line"/>) seulement : à l'impact, après avoir frappé la bande, l'ennemi
        /// fonce le long de son axe jusqu'au bout, ou jusqu'au premier obstacle ou à la première emprise solide (il s'arrête au contact).
        /// </summary>
        public bool Charge { get; }
    }

    /// <summary>
    /// Sous-lot 11.5 : comportement d'un ennemi dans un combat positionné (PLAN_LOT_11.md, 11.5). Sans comportement, l'ennemi ne bouge que
    /// par les commandes du scénario, comme avant. Avec, il choisit sa cible (provocation, sinon sa cible de scénario présente et vivante,
    /// sinon l'ennemi le plus proche), l'engage quand un ennemi entre dans <see cref="AggroRange"/> (ou qu'on le frappe, ou qu'on le
    /// provoque), puis se place entre <see cref="MinRange"/> et <see cref="MaxRange"/> de sa cible (distance entre les bords) : il
    /// approche, poursuit, recule ; avec <see cref="Encircle"/>, la meute qui vise la même cible se répartit autour d'elle. Il s'arrête
    /// pendant l'annonce d'une de ses attaques.
    /// </summary>
    public sealed class AssailantBehavior
    {
        /// <summary>Distance maximale à garder avec sa cible, entre les bords des emprises (m) : au-delà, il approche.</summary>
        public Fixed MaxRange { get; }

        /// <summary>Distance minimale (m) : en deçà, il recule (un tireur garde ses distances) ; 0 par défaut.</summary>
        public Fixed MinRange { get; }

        /// <summary>Distance d'engagement (m) : il attend qu'un ennemi y entre ; null : engagé dès le départ.</summary>
        public Fixed? AggroRange { get; }

        /// <summary>Se répartir autour de la cible avec les autres ennemis qui la visent et encerclent (meute).</summary>
        public bool Encircle { get; }

        /// <summary>Borne technique des distances d'un comportement (m).</summary>
        public static readonly Fixed MaxDistance = Fixed.FromInt(100);

        public AssailantBehavior(Fixed maxRange, Fixed? minRange = null, Fixed? aggroRange = null, bool encircle = false)
        {
            MaxRange = maxRange;
            MinRange = minRange ?? Fixed.Zero;
            AggroRange = aggroRange;
            Encircle = encircle;
        }
    }

    /// <summary>
    /// Assaillant : n'obéit à aucune commande, attaque à intervalle fixe en enchaînant ses attaques dans l'ordre (puis recommence).
    /// Première attaque à <see cref="FirstAttackMs"/>. Sans <see cref="Health"/>, c'est l'assaillant d'entraînement (A21) aux PV infinis ;
    /// avec, il meurt à 0 PV comme un personnage (sous-lot 11.1).
    /// </summary>
    public sealed class AssailantSetup : CombatantSetup
    {
        /// <summary>PV maximaux d'un assaillant mortel (10⁹), très au-delà du jeu prévu.</summary>
        public const long MaxHealth = 1_000_000_000;

        /// <summary>
        /// Sous-lot 11.1 : PV de départ et maximaux, dans [1, <see cref="MaxHealth"/>] ; null : PV infinis (assaillant d'entraînement, A21).
        /// À 0 PV, l'assaillant meurt : plus aucune attaque, plus une cible valide, une seule ligne <c>died</c>.
        /// </summary>
        public long? Health { get; }

        public Fixed Armor { get; }
        public Fixed MagicResistance { get; }

        /// <summary>Résistance aux altérations (lot 7.2, D12) : réduit la durée des contrôles ; 0 par défaut.</summary>
        public Fixed StatusResistance { get; }

        /// <summary>Cible des attaques <see cref="SkillTargeting.Enemy"/> ; null si toutes ses attaques sont de zone.</summary>
        public string? Target { get; }

        public long FirstAttackMs { get; }
        public long IntervalMs { get; }
        public IReadOnlyList<AssailantAttack> Attacks { get; }

        /// <summary>Déclencheurs déclarés par le scénario (lot 7.3, Q4), pour tester sans classe ; définition <c>assailant.{id}</c>.</summary>
        public IReadOnlyList<TriggerDefinition> Triggers { get; }

        /// <summary>Identifiant de définition des déclencheurs d'un assaillant, dans leur identité journalisée.</summary>
        public string TriggerDefinitionId => "assailant." + Id;

        /// <summary>Sous-lot 11.5 : comportement dans un combat positionné ; null : il ne bouge que par les commandes du scénario.</summary>
        public AssailantBehavior? Behavior { get; }

        public AssailantSetup(string id, string team, Fixed armor, Fixed magicResistance, string? target, long firstAttackMs, long intervalMs,
            IEnumerable<AssailantAttack> attacks, int initiative = 0, Fixed statusResistance = default, IEnumerable<TriggerDefinition>? triggers = null,
            long? health = null, CombatantPlacement? placement = null, long? arrivalMs = null, AssailantBehavior? behavior = null)
            : base(id, team, initiative, placement, arrivalMs)
        {
            Behavior = behavior;
            Health = health;
            Triggers = Frozen.ListOrEmpty(triggers, nameof(triggers));
            Armor = armor;
            MagicResistance = magicResistance;
            StatusResistance = statusResistance;
            Target = target;
            FirstAttackMs = firstAttackMs;
            IntervalMs = intervalMs;
            Attacks = Frozen.List(attacks, nameof(attacks));
        }
    }

    /// <summary>Nature d'une commande.</summary>
    public enum CombatAction
    {
        /// <summary>Lancer une compétence équipée.</summary>
        UseSkill = 0,

        /// <summary>Prendre la posture de garde (A1).</summary>
        GuardStart = 1,

        /// <summary>Quitter la garde.</summary>
        GuardStop = 2,

        /// <summary>Tenter une parade parfaite (A2).</summary>
        Parry = 3,

        /// <summary>Sous-lot 11.4 : marcher dans une direction, jusqu'à nouvel ordre (intention de déplacement d'un combat positionné).</summary>
        Move = 4,

        /// <summary>Sous-lot 11.4 : s'arrêter.</summary>
        Stop = 5,

        /// <summary>
        /// Sous-lot 11.4c : sélectionner une cible (ou effacer la sélection, sans cible). En garde, un personnage se tourne vers sa cible
        /// sélectionnée (COMBAT_ESPACE.md, section 6, retenu par Damien le 20/10/2026) ; combat positionné seulement.
        /// </summary>
        Select = 6,

        /// <summary>
        /// Sous-lot 11.4d : quitter le combat (changement de zone, départ d'un joueur, retrait d'un ennemi). Le combattant devient absent
        /// pour la suite : ses états sont retirés (<c>left</c>), ses provocations et protections sur les autres aussi (<c>sourceLeft</c>).
        /// </summary>
        Leave = 7,
    }

    /// <summary>Identifiants stables des actions dans les fichiers et le journal.</summary>
    public static class CombatActionIds
    {
        public const string UseSkill = "skill";
        public const string GuardStart = "guardStart";
        public const string GuardStop = "guardStop";
        public const string Parry = "parry";
        public const string Move = "move";
        public const string Stop = "stop";
        public const string Select = "select";
        public const string Leave = "leave";

        /// <summary>Actions défensives (garde et parade), seules transmises par le protocole du lot 10.</summary>
        public static IReadOnlyList<string> Defensive { get; } = Frozen.List(new[] { GuardStart, GuardStop, Parry }, nameof(Defensive));

        /// <summary>
        /// Actions qu'on écrit dans le champ <c>action</c> d'une commande de scénario (la compétence s'écrit avec <c>skill</c>) : les
        /// défensives, puis le déplacement (sous-lot 11.4).
        /// </summary>
        public static IReadOnlyList<string> Named { get; } = Frozen.List(new[] { GuardStart, GuardStop, Parry, Move, Stop, Select, Leave }, nameof(Named));

        public static string Of(CombatAction action) => action switch
        {
            CombatAction.UseSkill => UseSkill,
            CombatAction.GuardStart => GuardStart,
            CombatAction.GuardStop => GuardStop,
            CombatAction.Parry => Parry,
            CombatAction.Move => Move,
            CombatAction.Stop => Stop,
            CombatAction.Select => Select,
            CombatAction.Leave => Leave,
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
        };

        /// <summary>Action nommée d'une commande de scénario (<see cref="Named"/>) ; faux pour une autre valeur.</summary>
        public static bool TryParseNamed(string? id, out CombatAction action)
        {
            switch (id)
            {
                case Move: action = CombatAction.Move; return true;
                case Stop: action = CombatAction.Stop; return true;
                case Select: action = CombatAction.Select; return true;
                case Leave: action = CombatAction.Leave; return true;
                default: return TryParseDefensive(id, out action);
            }
        }

        public static bool TryParseDefensive(string? id, out CombatAction action)
        {
            switch (id)
            {
                case GuardStart: action = CombatAction.GuardStart; return true;
                case GuardStop: action = CombatAction.GuardStop; return true;
                case Parry: action = CombatAction.Parry; return true;
                default: action = default; return false;
            }
        }
    }

    /// <summary>Commande horodatée en millisecondes depuis le début du combat : compétence, garde, parade ou déplacement (sous-lot 11.4).</summary>
    public sealed class CombatCommand
    {
        public long AtMs { get; }
        public string Actor { get; }
        public CombatAction Action { get; }

        /// <summary>Compétence lancée ; null pour une garde, une parade ou un déplacement.</summary>
        public string? Skill { get; }

        /// <summary>Cible désignée ; null pour une compétence qui choisit ses cibles (zone), une garde, une parade ou un déplacement.</summary>
        public string? Target { get; }

        /// <summary>
        /// Sous-lot 11.4 : direction de marche d'un <see cref="CombatAction.Move"/>, telle que donnée (composantes dans [−1, 1], non toutes
        /// deux nulles ; le moteur la normalise) ; null pour toute autre commande.
        /// </summary>
        public GroundVector? Direction { get; }

        /// <summary>Commande « utiliser une compétence ».</summary>
        public CombatCommand(long atMs, string actor, string skill, string? target = null)
        {
            AtMs = atMs;
            Actor = actor ?? throw new ArgumentNullException(nameof(actor));
            Action = CombatAction.UseSkill;
            Skill = skill ?? throw new ArgumentNullException(nameof(skill));
            Target = target;
        }

        /// <summary>Commande de garde, de parade ou d'arrêt (sans compétence, cible ni direction).</summary>
        public CombatCommand(long atMs, string actor, CombatAction action)
        {
            if (action == CombatAction.UseSkill) throw new ArgumentException("Une compétence se lance avec le constructeur qui la nomme.", nameof(action));
            if (action == CombatAction.Move) throw new ArgumentException("Un déplacement se donne avec le constructeur qui porte sa direction.", nameof(action));
            AtMs = atMs;
            Actor = actor ?? throw new ArgumentNullException(nameof(actor));
            Action = action;
        }

        /// <summary>Sous-lot 11.4 : commande « marcher dans la direction <paramref name="direction"/> », jusqu'à un arrêt ou une autre direction.</summary>
        public CombatCommand(long atMs, string actor, GroundVector direction)
        {
            AtMs = atMs;
            Actor = actor ?? throw new ArgumentNullException(nameof(actor));
            Action = CombatAction.Move;
            Direction = direction;
        }

        private CombatCommand(long atMs, string actor, string? target, CombatAction action)
        {
            AtMs = atMs;
            Actor = actor ?? throw new ArgumentNullException(nameof(actor));
            Action = action;
            Target = target;
        }

        /// <summary>Sous-lot 11.4c : commande « sélectionner <paramref name="target"/> » ; null efface la sélection.</summary>
        public static CombatCommand SelectTarget(long atMs, string actor, string? target) => new(atMs, actor, target, CombatAction.Select);
    }

    /// <summary>Identité d'un fichier de données utilisé (version de contenu et empreinte).</summary>
    public sealed class DataFileIdentity
    {
        public string Id { get; }
        public string ContentVersion { get; }
        public string Fingerprint { get; }

        public DataFileIdentity(string id, string contentVersion, string fingerprint)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            ContentVersion = contentVersion ?? throw new ArgumentNullException(nameof(contentVersion));
            Fingerprint = fingerprint ?? throw new ArgumentNullException(nameof(fingerprint));
        }
    }

    /// <summary>
    /// Identité des données d'un combat, inscrite dans l'en-tête du journal : un journal n'est rejouable
    /// qu'avec des données de même empreinte.
    /// </summary>
    public sealed class CombatDataIdentity
    {
        public DataFileIdentity Rules { get; }

        /// <summary>Classes utilisées, triées par identifiant.</summary>
        public IReadOnlyList<DataFileIdentity> Classes { get; }

        public CombatDataIdentity(DataFileIdentity rules, IEnumerable<DataFileIdentity> classes)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Classes = Frozen.List((classes ?? throw new ArgumentNullException(nameof(classes))).OrderBy(c => c.Id, StringComparer.Ordinal), nameof(classes));
        }
    }

    /// <summary>
    /// Tout ce qui définit un combat : règles, participants, commandes, graine et identité des données.
    /// Immuable ; le moteur n'en modifie rien, si bien qu'un même objet peut servir à plusieurs combats.
    /// </summary>
    public sealed class CombatSetup
    {
        public long Seed { get; }
        public long DurationMs { get; }
        public StatRules Stats { get; }
        public DamageRules Damage { get; }
        public ProgressionRules Progression { get; }
        public CombatRules Combat { get; }
        public IReadOnlyList<CombatantSetup> Combatants { get; }
        public IReadOnlyList<CombatCommand> Commands { get; }
        public CombatDataIdentity Data { get; }

        /// <summary>
        /// Sous-lot 11.4 : espace d'un combat positionné (aire praticable, décor) ; null pour un combat sans positions, simulé exactement
        /// comme avant le sous-lot.
        /// </summary>
        public CombatSpace? Space { get; }

        public CombatSetup(long seed, long durationMs, StatRules stats, DamageRules damage, ProgressionRules progression,
            CombatRules combat, IEnumerable<CombatantSetup> combatants, IEnumerable<CombatCommand> commands, CombatDataIdentity data,
            CombatSpace? space = null)
        {
            Space = space;
            Seed = seed;
            DurationMs = durationMs;
            Stats = stats ?? throw new ArgumentNullException(nameof(stats));
            Damage = damage ?? throw new ArgumentNullException(nameof(damage));
            Progression = progression ?? throw new ArgumentNullException(nameof(progression));
            Combat = combat ?? throw new ArgumentNullException(nameof(combat));
            // Copies figées (M05) : la liste validée est exactement celle que le moteur simulera.
            Combatants = Frozen.List(combatants, nameof(combatants));
            Commands = Frozen.List(commands, nameof(commands));
            Data = data ?? throw new ArgumentNullException(nameof(data));
        }

        // Correctif 6.1 : \z exige la fin de la chaîne (« $ » acceptait « bastion\n »).
        private static readonly Regex NamePattern = new Regex(@"^[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant);

        /// <summary>Identifiant de combattant ou d'équipe valide : minuscules, chiffres et « _ ».</summary>
        public static bool IsValidName(string? name) => name != null && NamePattern.IsMatch(name);

        /// <summary>
        /// Incohérences qui empêchent de lancer le combat (références inconnues, durées hors tick, builds invalides…).
        /// Les chemins ont la forme du fichier de scénario (<c>combatants[1].id</c>, <c>commands[3].atMs</c>).
        /// Ce qui relève du jeu (recharge, endurance, cible morte) n'est pas une erreur ici : la commande est refusée en combat.
        /// </summary>
        public IReadOnlyList<RuleViolation> Validate() => Validate(queueBound: true);

        /// <summary>
        /// Variante réservée aux tests du filet de sécurité <c>queueLimitExceeded</c> : sans le majorant de saturation, un combat
        /// valide peut atteindre la capacité de la file en cours de simulation.
        /// </summary>
        internal IReadOnlyList<RuleViolation> Validate(bool queueBound) => Validate(queueBound, numericLimits: true);

        /// <summary>
        /// Correctif 7.3.1 : <paramref name="numericLimits"/> faux omet aussi le précontrôle numérique, pour exercer en test la
        /// défense en profondeur du moteur (mutations préparées dans des variables vérifiées, arrêt <c>overflow</c> sans effet partiel).
        /// </summary>
        internal IReadOnlyList<RuleViolation> Validate(bool queueBound, bool numericLimits)
        {
            var errors = new List<RuleViolation>();
            foreach (RuleViolation v in Combat.Validate()) errors.Add(new RuleViolation(v.Kind, v.Message, v.Fields.Select(f => "rules.combat." + f).ToArray()));
            foreach (RuleViolation v in Stats.Validate()) errors.Add(new RuleViolation(v.Kind, v.Message, v.Fields.Select(f => "rules.stats." + f).ToArray()));
            foreach (RuleViolation v in Damage.Validate()) errors.Add(new RuleViolation(v.Kind, v.Message, v.Fields.Select(f => "rules.damage." + f).ToArray()));
            foreach (RuleViolation v in Progression.Validate()) errors.Add(new RuleViolation(v.Kind, v.Message, v.Fields.Select(f => "rules.progression." + f).ToArray()));
            if (Combat.Statuses != null)
                foreach (RuleViolation v in Combat.Statuses.Validate(Combat.TickMilliseconds)) errors.Add(new RuleViolation(v.Kind, v.Message, v.Fields.Select(f => "rules." + f).ToArray()));
            if (errors.Count == 0)
                foreach (RuleViolation v in Combat.Statuses!.ValidateTriggers(Combat.TickMilliseconds, Combat.GuardBreakStatus, Combat.AreaStrikeMinCooldownMs))
                    errors.Add(new RuleViolation(v.Kind, v.Message, v.Fields.Select(f => "rules." + f).ToArray()));
            if (errors.Count > 0) return errors;   // la suite dépend de règles cohérentes

            // Lot 7.1 : admission. Un scénario hors limites de volume est refusé sans analyser ses éléments un par un.
            bool oversized = false;
            if (Combatants.Count > Combat.MaxCombatants)
            {
                errors.Add(new RuleViolation(RuleViolationKind.LimitExceeded,
                    $"{Combatants.Count} combattants, au plus {Combat.MaxCombatants} (combat.limits.maxCombatants).", "combatants"));
                oversized = true;
            }
            if (Commands.Count > Combat.MaxCommands)
            {
                errors.Add(new RuleViolation(RuleViolationKind.LimitExceeded,
                    $"{Commands.Count} commandes, au plus {Combat.MaxCommands} (combat.limits.maxCommands).", "commands"));
                oversized = true;
            }
            if (oversized) return errors;

            long tick = Combat.TickMilliseconds;
            if (Seed < 0) errors.Add(new RuleViolation("la graine doit être positive ou nulle.", "seed"));
            if (DurationMs <= 0 || DurationMs > Combat.MaxDurationMs)
                errors.Add(new RuleViolation($"durée hors de ]0, {Combat.MaxDurationMs}] ms (limite des règles).", "durationMs"));
            else if (DurationMs % tick != 0)
                errors.Add(new RuleViolation($"{DurationMs} ms n'est pas un multiple de la durée du tick ({tick} ms).", "durationMs"));

            var ids = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < Combatants.Count; i++)
            {
                CombatantSetup c = Combatants[i];
                string path = $"combatants[{i}]";
                if (!IsValidName(c.Id))
                    errors.Add(new RuleViolation($"identifiant « {c.Id} » invalide : minuscules, chiffres et « _ », commençant par une lettre.", path + ".id"));
                else if (ids.ContainsKey(c.Id))
                    errors.Add(new RuleViolation(RuleViolationKind.DuplicateId, $"identifiant « {c.Id} » déjà utilisé (combatants[{ids[c.Id]}]).", path + ".id"));
                else
                    ids[c.Id] = i;
                if (!IsValidName(c.Team))
                    errors.Add(new RuleViolation($"équipe « {c.Team} » invalide : minuscules, chiffres et « _ », commençant par une lettre.", path + ".team"));
                // Sous-lot 11.4d : arrivée en cours de combat, au début d'un tick du combat.
                if (c.ArrivalMs is long arrival)
                {
                    if (arrival < 0 || (DurationMs > 0 && arrival >= DurationMs))
                        errors.Add(new RuleViolation($"arrivée à {arrival} ms hors du combat [0, {DurationMs}[ ms.", path + ".arrivalMs"));
                    else if (arrival % tick != 0)
                        errors.Add(new RuleViolation($"{arrival} ms n'est pas un multiple de la durée du tick ({tick} ms).", path + ".arrivalMs"));
                }

                switch (c)
                {
                    case CharacterSetup character:
                        ValidateCharacter(character, path, tick, errors);
                        break;
                    case DummySetup dummy:
                        Defense(dummy.Armor, "l'armure du mannequin", path + ".armor", errors);
                        Defense(dummy.MagicResistance, "la résistance magique du mannequin", path + ".magicResistance", errors);
                        Defense(dummy.StatusResistance, "la résistance aux altérations du mannequin", path + ".statusResistance", errors);
                        break;
                    case AssailantSetup assailant:
                        ValidateAssailantShape(assailant, path, tick, errors);
                        ValidateBehavior(assailant, path, errors);   // sous-lot 11.5
                        break;
                    default:
                        errors.Add(new RuleViolation($"type de combattant non pris en charge : {c.GetType().Name}.", path));
                        break;
                }
            }
            for (int i = 0; i < Combatants.Count; i++)
                if (Combatants[i] is AssailantSetup { Target: { } targetId } assailant)
                {
                    CombatantSetup? target = Combatants.FirstOrDefault(c => c.Id == targetId);
                    if (target is null)
                        errors.Add(new RuleViolation(RuleViolationKind.UnknownReference, $"combattant inconnu « {targetId} ».", $"combatants[{i}].target"));
                    else if (target.Team == assailant.Team)
                        errors.Add(new RuleViolation("la cible de l'assaillant doit être un ennemi.", $"combatants[{i}].target"));
                }
            ValidateSpace(errors);
            if (errors.Count == 0) ValidateTriggerCount(errors);
            if (errors.Count == 0 && numericLimits) ValidateNumericLimits(errors);
            if (Combatants.Select(c => c.Team).Distinct(StringComparer.Ordinal).Count() < 2)
                errors.Add(new RuleViolation("un combat oppose au moins deux équipes.", "combatants"));

            var perActorTick = new Dictionary<(string Actor, long Tick), int>();
            for (int i = 0; i < Commands.Count; i++)
            {
                CombatCommand command = Commands[i];
                string path = $"commands[{i}]";
                if (command.AtMs < 0 || (DurationMs > 0 && command.AtMs >= DurationMs))
                    errors.Add(new RuleViolation($"instant {command.AtMs} ms hors du combat [0, {DurationMs}[ ms.", path + ".atMs"));
                else if (command.AtMs % tick != 0)
                    errors.Add(new RuleViolation($"{command.AtMs} ms n'est pas un multiple de la durée du tick ({tick} ms).", path + ".atMs"));
                else
                {
                    // Une seule erreur par couple (acteur, tick), sur la première commande en excès, dans l'ordre du fichier.
                    var key = (command.Actor, command.AtMs / tick);
                    perActorTick.TryGetValue(key, out int count);
                    perActorTick[key] = ++count;
                    if (count == Combat.MaxCommandsPerActorPerTick + 1)
                        errors.Add(new RuleViolation(RuleViolationKind.LimitExceeded,
                            $"{count}ᵉ commande de « {command.Actor} » au tick {key.Item2} ({command.AtMs} ms), au plus {Combat.MaxCommandsPerActorPerTick} (combat.limits.maxCommandsPerActorPerTick).",
                            path + ".atMs"));
                }
                if (!ids.ContainsKey(command.Actor))
                    errors.Add(new RuleViolation(RuleViolationKind.UnknownReference, $"combattant inconnu « {command.Actor} ».", path + ".actor"));
                if (command.Target != null && !ids.ContainsKey(command.Target))
                    errors.Add(new RuleViolation(RuleViolationKind.UnknownReference, $"combattant inconnu « {command.Target} ».", path + ".target"));
                if (!Enum.IsDefined(typeof(CombatAction), command.Action))
                    errors.Add(new RuleViolation("action inconnue.", path + ".action"));
                else if (command.Action == CombatAction.UseSkill)
                {
                    if (string.IsNullOrEmpty(command.Skill)) errors.Add(new RuleViolation("compétence obligatoire.", path + ".skill"));
                }
                else
                {
                    if (command.Skill != null) errors.Add(new RuleViolation("une garde, une parade, un déplacement, une sélection ou un départ ne nomme pas de compétence.", path + ".skill"));
                    if (command.Target != null && command.Action != CombatAction.Select)
                        errors.Add(new RuleViolation("une garde, une parade, un déplacement ou un départ n'a pas de cible.", path + ".target"));
                }
                // Sous-lot 11.4 : déplacement seulement dans un combat positionné ; une direction seulement pour un déplacement. Sous-lot
                // 11.4c : la sélection aussi (elle oriente la garde).
                bool moving = command.Action == CombatAction.Move || command.Action == CombatAction.Stop;
                if (moving && Space is null)
                    errors.Add(new RuleViolation("déplacement dans un combat sans espace (bloc space absent).", path + ".action"));
                // Sous-lot 11.5 : un ennemi qui a un comportement se déplace seul.
                if (moving && ids.TryGetValue(command.Actor, out int actorIndex) && Combatants[actorIndex] is AssailantSetup { Behavior: not null })
                    errors.Add(new RuleViolation("un ennemi qui a un comportement se déplace seul (pas de move ni de stop).", path + ".action"));
                if (command.Action == CombatAction.Select && Space is null)
                    errors.Add(new RuleViolation("sélection de cible dans un combat sans espace (bloc space absent).", path + ".action"));
                if (command.Action == CombatAction.Move)
                {
                    if (command.Direction is not { } direction || !SpaceLimits.IsDirection(direction))
                        errors.Add(new RuleViolation("direction obligatoire : composantes dans [−1, 1], non toutes deux nulles.", path + ".direction"));
                }
                else if (command.Direction != null)
                    errors.Add(new RuleViolation("seul un déplacement (move) a une direction.", path + ".direction"));
            }
            ValidateInitialQueue(errors);
            if (queueBound && errors.Count == 0) ValidateQueueSaturation(errors);
            return errors;
        }

        /// <summary>
        /// Sous-lot 11.4 : cohérence des positions. Sans espace, aucun combattant n'a de position. Avec un espace valide, chaque combattant en a
        /// une : rayon dans [0,1 ; 5] m, orientation non nulle, vitesse propre réservée aux assaillants, emprise entière dans l'aire praticable,
        /// sans chevaucher un obstacle ni un combattant qui lui est solide (<see cref="CombatRules.AreSolid"/>, selon la règle de contact).
        /// L'état de départ respecte ainsi les invariants que le déplacement maintient ensuite.
        /// </summary>
        private void ValidateSpace(List<RuleViolation> errors)
        {
            if (Space is null)
            {
                for (int i = 0; i < Combatants.Count; i++)
                    if (Combatants[i].Placement != null)
                        errors.Add(new RuleViolation("position dans un combat sans espace : ajouter le bloc space ou retirer la position.", $"combatants[{i}].position"));
                return;
            }
            int first = errors.Count;
            errors.AddRange(Space.Validate());
            bool spaceValid = errors.Count == first;
            ConvexPolygon[] polygons = spaceValid ? Space.Obstacles.Select(o => new ConvexPolygon(o.Points)).ToArray() : Array.Empty<ConvexPolygon>();
            bool placementsValid = true;
            for (int i = 0; i < Combatants.Count; i++)
            {
                CombatantSetup c = Combatants[i];
                string path = $"combatants[{i}]";
                if (c.Placement is not { } p)
                {
                    errors.Add(new RuleViolation("position obligatoire dans un combat positionné (bloc space).", path + ".position"));
                    placementsValid = false;
                    continue;
                }
                int before = errors.Count;
                bool radiusValid = p.Radius >= SpaceLimits.MinRadius && p.Radius <= SpaceLimits.MaxRadius;
                if (!radiusValid)
                    errors.Add(new RuleViolation(RuleViolationKind.NumericLimit,
                        $"rayon d'emprise hors de [{SpaceLimits.MinRadius}, {SpaceLimits.MaxRadius}] m (borne technique).", path + ".radius"));
                if (!SpaceLimits.IsDirection(p.Facing))
                    errors.Add(new RuleViolation("orientation : composantes dans [−1, 1], non toutes deux nulles.", path + ".facing"));
                if (p.Speed is { } speed)
                {
                    if (c is not AssailantSetup)
                        errors.Add(new RuleViolation("vitesse propre réservée aux assaillants (les personnages marchent à combat.movement.speed).", path + ".speed"));
                    else if (speed < SpaceLimits.MinSpeed || speed > SpaceLimits.MaxSpeed)
                        errors.Add(new RuleViolation(RuleViolationKind.NumericLimit,
                            $"vitesse hors de [{SpaceLimits.MinSpeed}, {SpaceLimits.MaxSpeed}] m/s (borne technique).", path + ".speed"));
                }
                if (!SpaceLimits.InRange(p.Position))
                    errors.Add(new RuleViolation(RuleViolationKind.NumericLimit, $"position hors de ±{SpaceLimits.MaxCoordinate} m (borne technique).", path + ".position"));
                else if (spaceValid && radiusValid)
                {
                    if (p.Position.X - p.Radius < Space.Min.X || p.Position.X + p.Radius > Space.Max.X
                        || p.Position.Z - p.Radius < Space.Min.Z || p.Position.Z + p.Radius > Space.Max.Z)
                        errors.Add(new RuleViolation("emprise hors de l'aire praticable (space.bounds).", path + ".position"));
                    else
                        for (int k = 0; k < polygons.Length; k++)
                            if (polygons[k].OverlapsCircle(p.Position.X.Raw, p.Position.Z.Raw, p.Radius.Raw))
                            {
                                errors.Add(new RuleViolation($"emprise qui chevauche l'obstacle « {Space.Obstacles[k].Id} ».", path + ".position"));
                                break;
                            }
                }
                if (errors.Count != before) placementsValid = false;
            }
            if (!spaceValid || !placementsValid) return;
            // Sous-lot 11.4d : un combattant qui arrive en cours de combat n'est comparé à personne ici ; le moteur refuse son arrivée
            // (arrivalRefused, occupied) si une emprise solide occupe sa place à ce moment-là.
            for (int j = 1; j < Combatants.Count; j++)
                for (int i = 0; i < j; i++)
                {
                    CombatantSetup a = Combatants[i], b = Combatants[j];
                    if (a.ArrivalMs != null || b.ArrivalMs != null) continue;
                    CombatantPlacement pa = a.Placement!, pb = b.Placement!;
                    if (Combat.AreSolid(a is CharacterSetup, pa.Radius, b is CharacterSetup, pb.Radius)
                        && Planar.CirclesOverlap(pa.Position.X.Raw, pa.Position.Z.Raw, pa.Radius.Raw, pb.Position.X.Raw, pb.Position.Z.Raw, pb.Radius.Raw))
                    {
                        errors.Add(new RuleViolation($"emprise qui chevauche celle de « {a.Id} » (combatants[{i}]), solide pour elle.", $"combatants[{j}].position"));
                        break;
                    }
                }
        }

        /// <summary>
        /// Correctif 7.2 : contrôle prudent de saturation de la file. Le majorant <see cref="ComputeQueueBound"/> doit tenir dans
        /// <c>combat.limits.maxPendingEvents</c> ; sinon le scénario est refusé avant tout journal (<c>LimitExceeded</c> sur
        /// <c>combatants</c>), au lieu d'un arrêt <c>queueLimitExceeded</c> en cours de combat.
        /// </summary>
        private void ValidateQueueSaturation(List<RuleViolation> errors)
        {
            if (Combat.TickMilliseconds <= 0 || DurationMs <= 0) return;
            QueueBound bound = ComputeQueueBound();
            if (bound.Total > Combat.MaxPendingEvents)
                errors.Add(new RuleViolation(RuleViolationKind.LimitExceeded,
                    $"majorant prudent de la file {bound.Total} (événements initiaux {bound.Initial}, instances de DOT {bound.Dots}, " +
                    $"échéances d'altérations {bound.Expiries}{(bound.Transient > 0 ? $", terme transitoire des déclencheurs {bound.Transient}" : "")}) " +
                    $"au-delà de sa capacité {Combat.MaxPendingEvents} (combat.limits.maxPendingEvents).",
                    "combatants"));
        }

        /// <summary>
        /// Détail du majorant de la file (correctif 7.2 ; terme transitoire et <see cref="Guaranteed"/> au sous-lot 7.3, C12).
        /// </summary>
        public readonly struct QueueBound
        {
            public long Initial { get; }
            public long Dots { get; }
            public long Expiries { get; }
            /// <summary>Places des notifications et réactions des déclencheurs (0 sans déclencheur).</summary>
            public long Transient { get; }
            /// <summary>Sous-lot 11.4c : impacts annoncés en attente et zones persistantes vivantes (une place chacun).</summary>
            public long Attacks { get; }
            /// <summary>
            /// Vrai sans déclencheur : le majorant borne la file. Faux dès qu'un déclencheur existe : les échéances et ticks de DOT
            /// créés par des réactions ne sont pas majorés, et l'arrêt <c>queueLimitExceeded</c> reste un filet atteignable (Q11).
            /// </summary>
            public bool Guaranteed { get; }
            public long Total => checked(Initial + Dots + Expiries + Transient + Attacks);

            public QueueBound(long initial, long dots, long expiries, long transient = 0, bool guaranteed = true, long attacks = 0)
            {
                Initial = initial;
                Dots = dots;
                Expiries = expiries;
                Transient = transient;
                Guaranteed = guaranteed;
                Attacks = attacks;
            }

            /// <summary>Résumé pour les rapports de la CLI (validate, simulate).</summary>
            public string Describe(long capacity) => Guaranteed
                ? $"majorant prudent {Total}/{capacity}, garanti"
                : $"majorant prudent {Total}/{capacity} dont {Transient} transitoires, NON garanti : les altérations posées par des déclencheurs ne sont pas majorées (arrêt queueLimitExceeded possible)";
        }

        /// <summary>
        /// Correctif 7.2 : majorant du nombre d'éléments que la file peut contenir à tout instant du combat, pour un combat valide.
        /// <list type="bullet">
        /// <item>Événements initiaux (commandes, premiers tours) : chaque commande retirée en programme au plus un (recharge ou
        /// fin de fenêtre de parade) et chaque tour d'assaillant le suivant ; leur nombre ne croît donc pas.</item>
        /// <item>DOT : un seul tick programmé par instance, une instance par (source, cible, altération) : somme, sur chaque
        /// porteur et chaque DOT qu'il porte, de ses cibles possibles.</item>
        /// <item>Altérations à échéance : une application mise en file vit au plus la durée de base D de l'altération. Pour
        /// <c>ignore</c>, une seule échéance vivante par cible ; pour <c>refresh</c> et <c>keepLonger</c>, au plus les applications
        /// faites pendant D ticks : par assaillant, ses tours dans cette fenêtre (cadence, cycle d'attaques) × ses cibles par
        /// impact ; par personnage, ses commandes de la compétence, bornées par sa recharge et le GCD ; pour <c>keepLonger</c>,
        /// au plus un renouvellement par cible et par tick. Une altération jamais appliquée (aucune commande, aucune attaque) ne
        /// compte pas. La rupture de garde (rien en mode ignore : elle remplace une commande guardStart retirée) et les états accordés à
        /// l'expiration (une échéance vivante par cible pour <c>ignore</c>, D par cible sinon) s'y ajoutent.</item>
        /// </list>
        /// Seules les compétences effectivement commandées sont comptées. Suppose un combat par ailleurs valide.
        /// </summary>
        public QueueBound ComputeQueueBound()
        {
            long tick = Combat.TickMilliseconds;
            long durationTicks = DurationMs / tick;
            long gcdTicks = Combat.GlobalCooldownMs / tick;
            StatusCatalog catalog = Combat.Statuses;
            var targets = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);   // cibles possibles par altération
            var applications = new Dictionary<string, long>(StringComparer.Ordinal);         // applications en D ticks par altération
            long dots = 0;
            long pendingAttacks = 0;   // sous-lot 11.4c

            // Appelé une fois par porteur et par altération : ses cibles possibles et ses applications en D ticks.
            void Carry(StatusDefinition d, IEnumerable<string> possibleTargets, long inWindow)
            {
                if (inWindow == 0) return;   // jamais appliquée : ni cible, ni instance
                if (!targets.TryGetValue(d.Id, out HashSet<string>? set)) targets[d.Id] = set = new HashSet<string>(StringComparer.Ordinal);
                var hit = new HashSet<string>(possibleTargets, StringComparer.Ordinal);
                if (d.IsDot) dots = checked(dots + hit.Count);   // une instance par (porteur, cible)
                set.UnionWith(hit);
                applications.TryGetValue(d.Id, out long n);
                applications[d.Id] = checked(n + inWindow);
            }

            // Sous-lot 8.1 : règles effectives des personnages. Une application vit au plus base + le plus grand ajout positif
            // d'un auteur (durationDealt) + le plus grand ajout positif d'une cible (durationTaken), écrêtée à 24 h comme au moteur ;
            // un raccourci ne fait que réduire. Les mannequins et assaillants n'ont pas de modificateur.
            var characterRules = new List<CombatantRules>();
            foreach (CombatantSetup c in Combatants)
                if (c is CharacterSetup ch) characterRules.Add(CombatantRules.For(Combat, ModifierSet.For(ch)));
            long Window(StatusDefinition d)
            {
                long dealt = 0, taken = 0;
                foreach (CombatantRules r in characterRules)
                {
                    dealt = Math.Max(dealt, r.DurationDealtMs(d.Id));
                    taken = Math.Max(taken, r.DurationTakenMs(d.Id));
                }
                long ms = dealt == 0 && taken == 0 ? d.DurationMs : Math.Min(CombatRules.AbsoluteMaxDurationMs, checked(d.DurationMs + dealt + taken));
                return Math.Max(1, ms / tick);
            }
            var enemies = Combatants.ToDictionary(c => c.Id, c => Combatants.Where(o => o.Team != c.Team).Select(o => o.Id).ToArray(), StringComparer.Ordinal);
            // Sous-lot 8.3 (X5) : alliés de chacun (lui compris), pour les compétences de soutien et les cibles propres d'application.
            var allies = Combatants.ToDictionary(c => c.Id, c => Combatants.Where(o => o.Team == c.Team).Select(o => o.Id).ToArray(), StringComparer.Ordinal);
            // Sous-lot 8.3 (X7) : une attaque à cible unique d'un assaillant provoqué vise la source de la provocation, un ennemi
            // quelconque : avec une provocation au catalogue, toutes les cibles ennemies sont possibles.
            bool taunts = catalog.Definitions.Any(d => d.IsTaunt);
            // Cibles possibles et impacts par action d'une application, selon le ciblage de l'action et la cible propre de l'application.
            (string[] Set, long Hits) Reach(string actor, SkillTargeting targeting, string? singleTarget, ApplicationTarget target)
            {
                switch (target)
                {
                    case ApplicationTarget.Self: return (new[] { actor }, 1);
                    case ApplicationTarget.AllAllies: return (allies[actor], allies[actor].Length);
                    case ApplicationTarget.AllEnemies: return (enemies[actor], enemies[actor].Length);
                }
                return targeting switch
                {
                    SkillTargeting.AllEnemies => (enemies[actor], enemies[actor].Length),
                    SkillTargeting.Self => (new[] { actor }, 1),
                    SkillTargeting.Ally => (allies[actor], 1),
                    SkillTargeting.AllAllies => (allies[actor], allies[actor].Length),
                    _ => singleTarget is null || taunts ? (enemies[actor], 1) : (new[] { singleTarget }, 1),
                };
            }

            foreach (CombatantSetup combatant in Combatants)
            {
                switch (combatant)
                {
                    case AssailantSetup a:
                    {
                        long interval = Math.Max(1, a.IntervalMs / tick);
                        long first = a.FirstAttackMs / tick;
                        long turns = first < durationTicks ? (durationTicks - 1 - first) / interval + 1 : 0;
                        int n = a.Attacks.Count;
                        // Sous-lot 11.4c : un tour d'attaque annoncée laisse un impact en file jusqu'à son tick, une zone persistante une
                        // place jusqu'à sa fin ; au plus autant que de tours dans la plus longue annonce + durée de zone.
                        long lasting = a.Attacks.Select(x => (x.TelegraphMs + (x.Shape == AttackShape.Persistent ? x.PersistentMs : 0)) / tick).DefaultIfEmpty(0).Max();
                        if (lasting > 0) pendingAttacks = checked(pendingAttacks + Math.Min(turns, lasting / interval + 1));
                        foreach (string status in a.Attacks.SelectMany(x => x.Applies).Select(x => x.Status).Distinct(StringComparer.Ordinal))
                        {
                            if (!catalog.TryGet(status, out StatusDefinition? d)) continue;
                            // Sous-lot 11.4c : une annonce retarde l'impact, une zone persistante répète ses impacts : la fenêtre s'allonge d'autant.
                            long late = a.Attacks.Where(x => x.Applies.Any(ap => ap.Status == status))
                                .Select(x => (x.TelegraphMs + (x.Shape == AttackShape.Persistent ? x.PersistentMs : 0)) / tick).DefaultIfEmpty(0).Max();
                            long k = Math.Min(turns, (checked(Window(d) + late) - 1) / interval + 1);   // tours dans une fenêtre de D ticks
                            long perAttack = (k + n - 1) / n;                            // tours d'une même attaque parmi k consécutifs
                            var carrying = a.Attacks.Where(x => x.Applies.Any(ap => ap.Status == status)).ToList();
                            // Sous-lot 8.3 : cibles possibles de chaque application (cible de l'attaque, ou cible propre), et le plus
                            // grand nombre d'applications par tour.
                            var hit = new HashSet<string>(StringComparer.Ordinal);
                            long hits = 0;
                            foreach (AssailantAttack x in carrying)
                                foreach (StatusApplication ap in x.Applies.Where(ap => ap.Status == status))
                                {
                                    // Sous-lot 11.4c : une zone atteint tous les ennemis, à chacun de ses impacts.
                                    // Sous-lot 11.5 : un ennemi qui a un comportement peut viser n'importe quel ennemi (le plus proche).
                                    var (set, count) = Reach(a.Id, x.IsZone ? SkillTargeting.AllEnemies : x.Targeting, a.Behavior is null ? a.Target : null, ap.Target);
                                    hit.UnionWith(set);
                                    hits = Math.Max(hits, checked(count * x.Pulses));
                                }
                            Carry(d, hit, checked(Math.Min(k, perAttack * carrying.Count) * hits));
                        }
                        break;
                    }
                    case CharacterSetup c:
                    {
                        CombatantRules rules = CombatantRules.For(Combat, ModifierSet.For(c));
                        foreach (string status in c.Class.Skills.Where(sk => sk.Combat != null).SelectMany(sk => sk.Combat!.Applies)
                                     .Select(x => x.Status).Distinct(StringComparer.Ordinal))
                        {
                            if (!catalog.TryGet(status, out StatusDefinition? d)) continue;
                            long inWindow = 0;
                            var reached = new HashSet<string>(StringComparer.Ordinal);
                            foreach (SkillDefinition skill in c.Class.Skills.Where(sk => sk.Combat != null && sk.Combat.Applies.Any(ap => ap.Status == status)))
                            {
                                long commands = Commands.LongCount(cmd => cmd.Actor == c.Id && cmd.Action == CombatAction.UseSkill && cmd.Skill == skill.Id);
                                long spacing = Math.Max(gcdTicks, rules.CooldownMs(skill, skill.Combat!) / tick);   // recharge effective (8.1)
                                long rate = spacing == 0 ? checked(Combat.MaxCommandsPerActorPerTick * Window(d)) : (Window(d) - 1) / spacing + 1;
                                // Sous-lot 8.3 : cibles possibles et applications par lancer, selon le ciblage et la cible propre.
                                var (set, hits) = Reach(c.Id, skill.Combat!.Targeting, null, skill.Combat.Applies.First(ap => ap.Status == status).Target);
                                reached.UnionWith(set);
                                inWindow = checked(inWindow + checked(Math.Min(commands, rate) * hits));
                            }
                            Carry(d, reached, inWindow);
                        }
                        break;
                    }
                }
            }

            long expiries = 0;
            var granted = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            void Grant(string status, IEnumerable<string> on)
            {
                if (!granted.TryGetValue(status, out HashSet<string>? set)) granted[status] = set = new HashSet<string>(StringComparer.Ordinal);
                set.UnionWith(on);
            }
            foreach (var (status, set) in targets)
            {
                StatusDefinition d = catalog[status];
                if (d.GrantsOnExpiry is { } g) Grant(g, set);
                if (d.IsDot) continue;
                long window = Window(d);
                expiries = checked(expiries + d.Stacking switch
                {
                    StatusStacking.Ignore => set.Count,
                    StatusStacking.KeepLonger => Math.Min(applications[status], checked(set.Count * window)),
                    _ => applications[status],
                });
            }
            if (catalog.TryGet(Combat.GuardBreakStatus, out StatusDefinition? guardBreak))
            {
                // Seul un personnage qui prend la garde peut la voir brisée. En mode ignore, son unique échéance vivante occupe la
                // place d'une commande guardStart déjà retirée, qui ne programme rien : elle n'ajoute rien au majorant.
                string[] characters = Combatants.OfType<CharacterSetup>()
                    .Where(c => Commands.Any(cmd => cmd.Actor == c.Id && cmd.Action == CombatAction.GuardStart)).Select(c => c.Id).ToArray();
                if (guardBreak.Stacking != StatusStacking.Ignore)
                    expiries = checked(expiries + checked(characters.Length * Window(guardBreak)));
                if (guardBreak.GrantsOnExpiry is { } g) Grant(g, characters);
            }
            foreach (var (status, set) in granted)
                if (catalog.TryGet(status, out StatusDefinition? d))
                    expiries = checked(expiries + (d.Stacking == StatusStacking.Ignore ? set.Count : checked(set.Count * Window(d))));
            // Sous-lot 7.3 (§6.4, C12) : chaque mise en file coûte une unité de travail. Les notifications et réactions d'un tick
            // sont toutes retirées avant la fin de leur phase, sauf les réactions reportées au tick suivant : à tout instant, au
            // plus celles mises en file pendant le tick courant et celles reportées du tick précédent, soit 2 × maxWorkPerTick.
            // Les échéances et ticks de DOT créés par des réactions ne sont pas majorés : le majorant n'est plus une garantie.
            if (HasTriggers) return new QueueBound(InitialEventCount(), dots, expiries, checked(2L * Combat.MaxWorkPerTick), guaranteed: false, attacks: pendingAttacks);
            return new QueueBound(InitialEventCount(), dots, expiries, attacks: pendingAttacks);
        }

        /// <summary>
        /// Lot 7.1 : événements programmés à l'initialisation (une entrée par commande, un premier tour par assaillant qui
        /// attaque avant la fin) comparés à la capacité réelle de la file. Le moteur refait le même compte à sa construction ;
        /// un dépassement est refusé ici, avant tout journal.
        /// </summary>
        private void ValidateInitialQueue(List<RuleViolation> errors)
        {
            if (Combat.TickMilliseconds <= 0 || DurationMs <= 0) return;
            long initial = InitialEventCount();
            if (initial > Combat.MaxPendingEvents)
                errors.Add(new RuleViolation(RuleViolationKind.LimitExceeded,
                    $"{initial} événements à programmer avant le premier tick (commandes et premiers tours d'assaillant), capacité de la file {Combat.MaxPendingEvents} (combat.limits.maxPendingEvents).",
                    "commands"));
        }

        /// <summary>
        /// Nombre d'événements que le moteur met en file à sa construction : une entrée par commande, un premier tour par assaillant présent
        /// dès le départ qui attaque avant la fin, et (sous-lot 11.4d) une arrivée par combattant qui arrive en cours de combat (son premier
        /// tour éventuel prend la place de son arrivée dans la file).
        /// </summary>
        public long InitialEventCount()
        {
            long durationTicks = DurationMs / Combat.TickMilliseconds;
            long assailantTurns = Combatants.OfType<AssailantSetup>().LongCount(a => a.ArrivalMs is null && a.FirstAttackMs >= 0 && a.FirstAttackMs / Combat.TickMilliseconds < durationTicks);
            long arrivals = Combatants.LongCount(c => c.ArrivalMs != null);
            return checked(Commands.Count + assailantTurns + arrivals);
        }

        /// <summary>
        /// Sous-lot 11.4d : le combat complété de combattants arrivés en cours de route (après ceux du combat, dans l'ordre d'arrivée) et de
        /// commandes soumises (après celles du combat) ; même graine, mêmes règles, même espace. Un combat piloté se rejoue ainsi à l'identique.
        /// </summary>
        public CombatSetup Completed(IEnumerable<CombatantSetup> joined, IEnumerable<CombatCommand> submitted) =>
            new CombatSetup(Seed, DurationMs, Stats, Damage, Progression, Combat, Combatants.Concat(joined ?? throw new ArgumentNullException(nameof(joined))),
                Commands.Concat(submitted ?? throw new ArgumentNullException(nameof(submitted))), Data, Space);

        /// <summary>
        /// Sous-lot 11.5 : comportement d'un ennemi, dans un combat positionné seulement : distances dans [0, 100] m (engagement dans
        /// ]0, 100]), minimale au plus égale à la maximale. Un ennemi qui a un comportement se déplace seul : pas de commande move ni stop
        /// (contrôlé avec les commandes).
        /// </summary>
        private void ValidateBehavior(AssailantSetup a, string path, List<RuleViolation> errors)
        {
            if (a.Behavior is not { } b) return;
            string at = path + ".behavior";
            if (Space is null) errors.Add(new RuleViolation("comportement dans un combat sans espace (bloc space absent).", at));
            void Distance(Fixed value, string field, bool positive)
            {
                if (value < Fixed.Zero || value > AssailantBehavior.MaxDistance || (positive && value == Fixed.Zero))
                    errors.Add(new RuleViolation(RuleViolationKind.NumericLimit,
                        $"distance hors de {(positive ? "]" : "[")}0, {AssailantBehavior.MaxDistance}] m (borne technique).", $"{at}.{field}"));
            }
            Distance(b.MaxRange, "maxRange", positive: false);
            Distance(b.MinRange, "minRange", positive: false);
            if (b.AggroRange is { } aggro) Distance(aggro, "aggroRange", positive: true);
            if (b.MinRange > b.MaxRange && b.MaxRange >= Fixed.Zero && b.MaxRange <= AssailantBehavior.MaxDistance)
                errors.Add(new RuleViolation("distance minimale plus grande que la maximale.", at + ".minRange"));
        }

        private void ValidateAssailantShape(AssailantSetup a, string path, long tick, List<RuleViolation> errors)
        {
            // Sous-lot 11.1 : un assaillant mortel commence vivant ; null garde les PV infinis de l'entraînement (A21).
            if (a.Health is long health && (health < 1 || health > AssailantSetup.MaxHealth))
                errors.Add(new RuleViolation($"PV hors de [1, {AssailantSetup.MaxHealth}].", path + ".health"));
            Defense(a.Armor, "l'armure de l'assaillant", path + ".armor", errors);
            Defense(a.MagicResistance, "la résistance magique de l'assaillant", path + ".magicResistance", errors);
            Defense(a.StatusResistance, "la résistance aux altérations de l'assaillant", path + ".statusResistance", errors);
            if (a.FirstAttackMs < 0 || a.FirstAttackMs > Combat.MaxDurationMs)
                errors.Add(new RuleViolation($"instant hors de [0, {Combat.MaxDurationMs}] ms.", path + ".firstAttackMs"));
            else if (a.FirstAttackMs % tick != 0)
                errors.Add(new RuleViolation($"{a.FirstAttackMs} ms n'est pas un multiple de la durée du tick ({tick} ms).", path + ".firstAttackMs"));
            if (a.IntervalMs <= 0 || a.IntervalMs > Combat.MaxDurationMs)
                errors.Add(new RuleViolation($"intervalle hors de ]0, {Combat.MaxDurationMs}] ms.", path + ".intervalMs"));
            else if (a.IntervalMs % tick != 0)
                errors.Add(new RuleViolation($"{a.IntervalMs} ms n'est pas un multiple de la durée du tick ({tick} ms).", path + ".intervalMs"));
            if (a.Attacks.Count == 0) errors.Add(new RuleViolation("au moins une attaque.", path + ".attacks"));
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int k = 0; k < a.Attacks.Count; k++)
            {
                AssailantAttack attack = a.Attacks[k];
                string attackPath = $"{path}.attacks[{k}]";
                if (!IsValidName(attack.Id))
                    errors.Add(new RuleViolation($"identifiant « {attack.Id} » invalide : minuscules, chiffres et « _ », commençant par une lettre.", attackPath + ".id"));
                else if (!seen.Add(attack.Id))
                    errors.Add(new RuleViolation(RuleViolationKind.DuplicateId, $"attaque « {attack.Id} » déjà définie.", attackPath + ".id"));
                if (attack.RawDamage < 0 || attack.RawDamage > CombatantStats.MaxRawDamage)
                    errors.Add(new RuleViolation($"dégâts bruts hors de [0, {CombatantStats.MaxRawDamage}].", attackPath + ".rawDamage"));
                if (!Enum.IsDefined(typeof(DamageType), attack.DamageType)) errors.Add(new RuleViolation("type de dégâts inconnu.", attackPath + ".damageType"));
                if (!Enum.IsDefined(typeof(SkillTargeting), attack.Targeting)) errors.Add(new RuleViolation("ciblage inconnu.", attackPath + ".targeting"));
                // Sous-lot 8.3 : une attaque d'assaillant frappe ; seules les compétences ont un ciblage de soutien.
                else if (attack.Targeting != SkillTargeting.Enemy && attack.Targeting != SkillTargeting.AllEnemies)
                    errors.Add(new RuleViolation("une attaque d'assaillant vise un ennemi ou tous les ennemis (enemy, allEnemies).", attackPath + ".targeting"));
                ValidateForm(attack, attackPath, tick, errors);   // sous-lot 11.4c
                StatusApplication.ValidateList(attack.Applies, attackPath + ".applies", errors);
                for (int m = 0; m < attack.Applies.Count; m++)
                    if (Combat.Statuses.CheckApplication(attack.Applies[m], Combat.GuardBreakStatus) is { } problem)
                        errors.Add(new RuleViolation(problem.StartsWith("altération inconnue", StringComparison.Ordinal) ? RuleViolationKind.UnknownReference : RuleViolationKind.InvalidValue,
                            problem, $"{attackPath}.applies[{m}].status"));
            }
            // Sous-lot 11.5 : un ennemi qui a un comportement choisit sa cible (le plus proche) ; sa cible de scénario est facultative.
            if (a.Target is null && a.Behavior is null && a.Attacks.Any(x => x.Targeting == SkillTargeting.Enemy))
                errors.Add(new RuleViolation("une attaque à cible unique exige une cible pour l'assaillant (ou un comportement, qui choisit le plus proche).", path + ".target"));
            // Lot 7.3 (Q4) : déclencheurs d'un assaillant de scénario, forme et références.
            TriggerDefinition.ValidateList(a.Triggers, path + ".triggers", 1, false, errors);
            TriggerDefinition.ValidateReferences(a.Triggers, path + ".triggers", Combat.Statuses, Combat.GuardBreakStatus, (int)tick, Combat.AreaStrikeMinCooldownMs, errors);
        }

        /// <summary>
        /// Sous-lot 11.4c (COMBAT_ESPACE.md, sections 5 et 9) : forme, annonce et défenses d'une attaque d'assaillant. Une forme n'existe que
        /// dans un combat positionné et vise la cible de l'assaillant ; chaque forme a ses dimensions, et seulement elles. Une zone ne se pare
        /// jamais, une zone persistante ne se garde pas, une attaque imparable ne se garde ni ne se pare. <b>Annonce évitable</b> : une zone
        /// doit laisser, au pas de base (<c>combat.movement.speed</c>), le temps de réaction minimal (<c>combat.telegraph.minReactionMs</c>)
        /// plus le temps de sortir de la forme au pire pour le plus grand joueur du combat.
        /// </summary>
        private void ValidateForm(AssailantAttack attack, string path, long tick, List<RuleViolation> errors)
        {
            int before = errors.Count;
            void Forbidden(bool present, string field, string why)
            {
                if (present) errors.Add(new RuleViolation(why, $"{path}.{field}"));
            }
            void Length(Fixed? value, string field, bool required, string what)
            {
                if (value is { } v)
                {
                    if (!SpaceLimits.IsRange(v))
                        errors.Add(new RuleViolation(RuleViolationKind.NumericLimit,
                            $"{what} hors de [{SpaceLimits.MinRange}, {SpaceLimits.MaxRange}] m (borne technique).", $"{path}.{field}"));
                }
                else if (required) errors.Add(new RuleViolation($"{what} obligatoire pour cette forme.", $"{path}.{field}"));
            }
            void Duration(long ms, string field, bool positive)
            {
                if (ms < (positive ? 1 : 0) || ms > Combat.MaxDurationMs)
                    errors.Add(new RuleViolation($"durée hors de [{(positive ? 1 : 0)}, {Combat.MaxDurationMs}] ms.", $"{path}.{field}"));
                else if (tick > 0 && ms % tick != 0)
                    errors.Add(new RuleViolation($"{ms} ms n'est pas un multiple de la durée du tick ({tick} ms).", $"{path}.{field}"));
            }

            if (attack.Unblockable && (attack.GuardableOverride == true || attack.ParryableOverride == true))
                errors.Add(new RuleViolation("une attaque imparable ne se garde ni ne se pare (guardable, parryable).",
                    $"{path}.{(attack.GuardableOverride == true ? "guardable" : "parryable")}"));
            if (attack.Shape is not { } shape)
            {
                const string historical = "seule une attaque qui a une forme (shape) a une annonce et des dimensions.";
                Forbidden(attack.TelegraphMs != 0, "telegraphMs", historical);
                Forbidden(attack.Range != null, "range", historical);
                Forbidden(attack.Radius != null, "radius", historical);
                Forbidden(attack.Angle != null, "angle", historical);
                Forbidden(attack.Width != null, "width", historical);
                Forbidden(attack.Center != ShapeCenter.Target, "center", historical);
                Forbidden(attack.PersistentMs != 0, "persistentMs", historical);
                Forbidden(attack.PulseMs != 0, "tickMs", historical);
                Forbidden(attack.Charge, "charge", historical);
                return;
            }
            if (!Enum.IsDefined(typeof(AttackShape), shape))
            {
                errors.Add(new RuleViolation("forme inconnue.", path + ".shape"));
                return;
            }
            if (Space is null) errors.Add(new RuleViolation("forme d'attaque dans un combat sans espace (bloc space absent).", path + ".shape"));
            if (attack.Targeting != SkillTargeting.Enemy)
                errors.Add(new RuleViolation("une attaque qui a une forme vise la cible de l'assaillant (targeting enemy).", path + ".targeting"));
            Duration(attack.TelegraphMs, "telegraphMs", positive: false);
            string name = AttackShapeIds.Of(shape);
            string notFor = $"champ sans objet pour la forme {name}.";
            bool round = shape is AttackShape.Circle or AttackShape.Persistent;
            Length(attack.Range, "range", shape is AttackShape.Cone or AttackShape.Line, shape == AttackShape.Target ? "portée" : "longueur");
            Forbidden(round && attack.Range != null, "range", notFor);
            Length(attack.Radius, "radius", round, "rayon");
            Forbidden(!round && attack.Radius != null, "radius", notFor);
            Forbidden(!round && attack.Center != ShapeCenter.Target, "center", notFor);
            if (!Enum.IsDefined(typeof(ShapeCenter), attack.Center)) errors.Add(new RuleViolation("centre inconnu.", path + ".center"));
            if (shape == AttackShape.Cone)
            {
                if (attack.Angle is not { } angle) errors.Add(new RuleViolation("ouverture obligatoire pour un cône.", path + ".angle"));
                else if (angle < 1 || angle > 180) errors.Add(new RuleViolation("ouverture hors de [1, 180] degrés.", path + ".angle"));
            }
            else Forbidden(attack.Angle != null, "angle", notFor);
            Length(attack.Width, "width", shape == AttackShape.Line, "largeur");
            Forbidden(shape != AttackShape.Line && attack.Width != null, "width", notFor);
            if (shape == AttackShape.Persistent)
            {
                Duration(attack.PersistentMs, "persistentMs", positive: true);
                Duration(attack.PulseMs, "tickMs", positive: true);
                if (attack.PersistentMs > 0 && attack.PulseMs > attack.PersistentMs)
                    errors.Add(new RuleViolation("cadence plus longue que la durée de la zone.", path + ".tickMs"));
                Forbidden(attack.GuardableOverride == true, "guardable", "une zone persistante ne se garde pas : il faut en sortir.");
            }
            else
            {
                Forbidden(attack.PersistentMs != 0, "persistentMs", notFor);
                Forbidden(attack.PulseMs != 0, "tickMs", notFor);
            }
            if (attack.IsZone) Forbidden(attack.ParryableOverride == true, "parryable", "une zone ne se pare pas (seul un coup dirigé se pare).");
            Forbidden(attack.Charge && shape != AttackShape.Line, "charge", "seule une bande (shape line) peut être une charge (sous-lot 11.5).");

            // Annonce évitable : réaction minimale + sortie de la forme, au pas de base, pour le plus grand joueur.
            if (!attack.IsZone || errors.Count > before) return;
            long speed = Combat.MovementSpeed.Raw;
            long largest = Combatants.OfType<CharacterSetup>().Select(c => c.Placement?.Radius.Raw ?? 0).DefaultIfEmpty(0).Max();
            if (largest == 0 || speed <= 0) return;
            long reach = (attack.Range ?? attack.Radius)!.Value.Raw;
            long escape = AttackZone.EscapeDistance(shape, reach, attack.Angle ?? 0, (attack.Width?.Raw ?? 0) / 2, largest);
            long escapeMs = checked(escape * 1000 + speed - 1) / speed;
            long needed = checked(Combat.TelegraphMinReactionMs + escapeMs);
            if (attack.TelegraphMs < needed)
                errors.Add(new RuleViolation(
                    $"annonce de {attack.TelegraphMs} ms trop courte pour une zone évitable : au moins {needed} ms (réaction {Combat.TelegraphMinReactionMs} ms, " +
                    $"combat.telegraph.minReactionMs, + {escapeMs} ms pour sortir de la forme au pas de base de {Combat.MovementSpeed} m/s).", path + ".telegraphMs"));
        }

        private void ValidateCharacter(CharacterSetup character, string path, long tick, List<RuleViolation> errors)
        {
            IReadOnlyList<RuleViolation> classViolations = character.Class.Validate(Progression);
            foreach (RuleViolation v in classViolations)
                errors.Add(new RuleViolation(v.Kind, $"classe « {character.Class.Id} » incohérente : {v.Message}", v.Fields.Select(f => path + ".class:" + f).ToArray()));
            foreach (BuildViolation v in BuildValidator.Validate(character.Build, character.Class, Progression))
                errors.Add(new RuleViolation($"build invalide ({v.Subject}) : {v.Message} [{v.Code}]", path + ".build"));
            foreach (SkillDefinition skill in character.Class.Skills)
            {
                if (skill.Combat is not { } combat) continue;
                if (combat.CooldownMs % tick != 0)
                    errors.Add(new RuleViolation($"recharge de « {skill.Name} » ({combat.CooldownMs} ms) non multiple de la durée du tick ({tick} ms).", path + ".class"));
                // Lot 7.2 : les altérations des compétences désignent le catalogue des règles, connu seulement ici.
                foreach (StatusApplication application in combat.Applies)
                    if (Combat.Statuses.CheckApplication(application, Combat.GuardBreakStatus) is { } problem)
                        errors.Add(new RuleViolation(problem.StartsWith("altération inconnue", StringComparison.Ordinal) ? RuleViolationKind.UnknownReference : RuleViolationKind.InvalidValue,
                            $"compétence « {skill.Id} » : {problem}", path + ".class"));
            }
            // Lot 7.3 : les déclencheurs de la classe désignent le catalogue et le tick des règles, connus seulement ici. Les chemins
            // internes à la classe sont rattachés au combattant, comme les autres incohérences classe-règles.
            var problems = new List<RuleViolation>();
            for (int t = 0; t < character.Class.Trees.Count; t++)
                for (int i = 0; i < character.Class.Trees[t].Talents.Count; i++)
                    TriggerDefinition.ValidateReferences(character.Class.Trees[t].Talents[i].Triggers, $"trees[{t}].talents[{i}].triggers", Combat.Statuses, Combat.GuardBreakStatus, (int)tick, Combat.AreaStrikeMinCooldownMs, problems);
            for (int k = 0; k < character.Class.Skills.Count; k++)
                TriggerDefinition.ValidateReferences(character.Class.Skills[k].Triggers, $"skills[{k}].triggers", Combat.Statuses, Combat.GuardBreakStatus, (int)tick, Combat.AreaStrikeMinCooldownMs, problems);
            // Sous-lot 8.1 : les modificateurs désignent aussi le catalogue (durées d'altération) et le tick (valeurs en ms). Une classe
            // arrêtée à un plafond global n'est pas examinée plus loin (plafond signalé seul) ; une liste déjà refusée par la classe
            // (modificateurs d'une active, valeur hors borne…) ne l'est pas non plus : un même modificateur n'est pas signalé deux fois.
            bool capped = classViolations.Any(v => v.Kind == RuleViolationKind.LimitExceeded && v.Fields.Any(f => f == "trees" || f == "skills"));
            bool Refused(string list) => classViolations.Any(v => v.Fields.Any(f =>
                f == list || f.StartsWith(list + "[", StringComparison.Ordinal) || f.StartsWith(list + ".", StringComparison.Ordinal)));
            // Décision du 18/10/2026 : le plafond de la réduction de garde est un paramètre des règles, vérifié ici aussi.
            void CheckModifiers(IReadOnlyList<ModifierDefinition> modifiers, string list, int maxRank, bool ranked)
            {
                if (modifiers.Count == 0 || capped || Refused(list)) return;
                ModifierDefinition.ValidateReferences(modifiers, list, Combat.Statuses, Combat.GuardBreakStatus, tick, problems);
                ModifierDefinition.ValidateCaps(modifiers, list, maxRank, ranked, Combat.MaxGuardDamageReduction, problems);
            }
            for (int t = 0; t < character.Class.Trees.Count; t++)
                for (int i = 0; i < character.Class.Trees[t].Talents.Count; i++)
                    CheckModifiers(character.Class.Trees[t].Talents[i].Modifiers, $"trees[{t}].talents[{i}].modifiers", character.Class.Trees[t].Talents[i].MaxRank, true);
            for (int k = 0; k < character.Class.Skills.Count; k++)
                CheckModifiers(character.Class.Skills[k].Modifiers, $"skills[{k}].modifiers", 1, false);
            foreach (RuleViolation v in problems)
                errors.Add(new RuleViolation(v.Kind, $"classe « {character.Class.Id} », {string.Join(", ", v.Fields)} : {v.Message}", path + ".class"));
        }

        /// <summary>Nombre de déclencheurs que ce combat peut activer (ceux de <see cref="TriggerWarnings"/>), sans les nommer.</summary>
        private long ActiveTriggerCount()
        {
            long count = Combat.Statuses.Definitions.Sum(d => (long)d.Triggers.Count);
            foreach (CombatantSetup c in Combatants)
                count += c switch
                {
                    CharacterSetup ch => ch.ActiveTriggers().LongCount(),
                    AssailantSetup a => a.Triggers.Count,
                    _ => 0,
                };
            return count;
        }

        /// <summary>
        /// Correctif 7.3.1 (A73-M03) : plafond global des déclencheurs actifs du combat (<see cref="TriggerGraph.MaxTriggers"/>), vérifié
        /// avant l'analyse statique des cycles et le précontrôle numérique.
        /// </summary>
        private void ValidateTriggerCount(List<RuleViolation> errors)
        {
            long count = ActiveTriggerCount();
            if (count > TriggerGraph.MaxTriggers)
                errors.Add(new RuleViolation(RuleViolationKind.LimitExceeded,
                    $"{count} déclencheurs actifs dans le combat (personnages, assaillants, altérations), au plus {TriggerGraph.MaxTriggers} (plafond technique provisoire).",
                    "combatants"));
        }

        /// <summary>Lot 7.3 : y a-t-il au moins un déclencheur susceptible d'être actif dans ce combat ?</summary>
        public bool HasTriggers =>
            Combatants.Any(c => c is AssailantSetup { Triggers: { Count: > 0 } } || (c is CharacterSetup ch && ch.ActiveTriggers().Any()))
            || Combat.Statuses.Definitions.Any(d => d.Triggers.Count > 0);

        /// <summary>
        /// Lot 7.3 (A15, Q1) : avertissements du contrôle statique des cycles, pour tous les déclencheurs que ce combat peut activer
        /// (talents et compétences équipées des personnages, assaillants, altérations du catalogue). Jamais une erreur.
        /// </summary>
        public IReadOnlyList<RuleViolation> TriggerWarnings()
        {
            if (ActiveTriggerCount() > TriggerGraph.MaxTriggers) return Array.Empty<RuleViolation>();   // refusé par Validate
            var named = new List<NamedTrigger>();
            for (int i = 0; i < Combatants.Count; i++)
            {
                switch (Combatants[i])
                {
                    case CharacterSetup c:
                        foreach (BoundTrigger b in c.ActiveTriggers())
                            named.Add(new NamedTrigger($"{b.Label}@{c.Id}", $"combatants[{i}].class", b.Definition));
                        break;
                    case AssailantSetup a:
                        for (int k = 0; k < a.Triggers.Count; k++)
                            named.Add(new NamedTrigger($"{a.TriggerDefinitionId}/{k}@{a.Id}", $"combatants[{i}].triggers[{k}]", a.Triggers[k]));
                        break;
                }
            }
            foreach (NamedTrigger t in Combat.Statuses.NamedTriggers())
                named.Add(new NamedTrigger(t.Label, "rules." + t.Path, t.Definition));
            return TriggerGraph.Cycles(named, Combat.Statuses);
        }

        private static void Defense(Fixed value, string label, string path, List<RuleViolation> errors)
        {
            if (value < Fixed.Zero || value > CombatantStats.MaxDefense)
                errors.Add(new RuleViolation($"{label} doit être comprise entre 0 et {CombatantStats.MaxDefense}.", path));
        }

        /// <summary>
        /// Profil offensif d'un combattant : ce qui entre dans la formule de dégâts pour une de ses attaques. Correctif 7.3.1 (A73-M01) :
        /// un impact direct passe par <see cref="DamageCalculator.Compute"/> (bonus de catégorie, critique) ; un tick de DOT
        /// (<see cref="Periodic"/>) par <see cref="DamageCalculator.ComputePeriodic"/>, avec la puissance figée réelle et le mode de
        /// résistance de l'altération. La formule directe n'est pas un majorant du DOT : un multiplicateur de catégorie maximal
        /// inférieur à 1 (jusqu'à 0) la rend plus petite.
        /// </summary>
        private readonly struct Offense
        {
            public readonly string Label;
            public readonly DerivedStats Attacker;
            public readonly PowerType Power;
            public readonly DamageType Type;
            public readonly Fixed Coefficient;
            public readonly bool CanCrit;
            public readonly bool Periodic;
            public readonly bool ReducedByDefense;

            /// <summary>Sous-lot 8.1 : majorant du bonus de dégâts infligés (catégorie talent) de l'attaquant ; impacts directs seulement.</summary>
            public readonly Fixed Talent;

            public Offense(string label, DerivedStats attacker, PowerType power, DamageType type, Fixed coefficient, bool canCrit, Fixed talent = default)
                : this(label, attacker, power, type, coefficient, canCrit, false, false, talent) { }

            private Offense(string label, DerivedStats attacker, PowerType power, DamageType type, Fixed coefficient, bool canCrit,
                bool periodic, bool reducedByDefense, Fixed talent = default)
            {
                Talent = talent;
                Label = label;
                Attacker = attacker;
                Power = power;
                Type = type;
                Coefficient = coefficient;
                CanCrit = canCrit;
                Periodic = periodic;
                ReducedByDefense = reducedByDefense;
            }

            /// <summary>Tick d'un DOT : puissance figée = puissance du type de l'altération × coefficient de l'application.</summary>
            public static Offense Dot(string label, DerivedStats attacker, StatusDefinition definition, Fixed coefficient)
            {
                DamageType type = definition.DamageType!.Value;
                return new Offense(label, attacker, CombatantStats.PowerOf(type), type, coefficient, false, true,
                    definition.Resistance == StatusResistanceMode.Defense);
            }
        }

        /// <summary>
        /// Précontrôle numérique (correctif 6.1, M04 ; complété au correctif 6.1.1, CA-M01). Calcule à l'avance, pour chaque
        /// combattant, tout ce que le moteur calculera à sa construction (statistiques, PV, endurance, défenses, puissance de
        /// chaque attaque), puis vérifie, avant le premier tick et sans rien journaliser :
        /// <list type="number">
        /// <item>les défenses effectives réelles de <b>chaque</b> participant, personnages compris, pour les deux types de dégâts :
        /// au plus <see cref="CombatantStats.MaxDefense"/>, et dénominateur constante × 10 000 + défense brute représentable ;</item>
        /// <item>le pire impact de chaque attaque contre chaque défenseur ennemi réel (critique, Fragilité, sans garde ni parade) ;</item>
        /// <item>le pire impact contre une cible sans défense, qui majore tous les autres, cumulé sur toute la durée au rythme
        /// maximal d'événements comptés.</item>
        /// </list>
        /// Un dépassement produit un diagnostic <see cref="RuleViolationKind.NumericLimit"/> rattaché au combattant concerné.
        /// Ce précontrôle couvre les calculs connus du moteur ; un débordement non anticipé reste arrêté proprement en simulation
        /// (<c>simulationAborted</c>, raison <c>overflow</c>).
        /// </summary>
        private void ValidateNumericLimits(List<RuleViolation> errors)
        {
            int count = Combatants.Count;
            var defenses = new DerivedStats?[count];
            var offenses = new List<Offense>[count];
            for (int i = 0; i < count; i++)
            {
                CombatantSetup combatant = Combatants[i];
                string path = $"combatants[{i}]";
                offenses[i] = new List<Offense>();
                // Champ et message d'un dépassement : les attributs du build, sauf pendant l'application des modificateurs permanents aux
                // statistiques d'un personnage (sous-lot 8.1), rattachée à sa classe.
                string overflowPath = combatant is CharacterSetup ? path + ".build.attributes" : path;
                string overflowMessage = "statistiques dérivées hors des limites numériques.";
                try
                {
                    switch (combatant)
                    {
                        case CharacterSetup character:
                            // Sous-lot 8.1 : statistiques et règles effectives par les fonctions partagées avec le moteur (modificateurs
                            // permanents) ; coefficient effectif de chaque compétence ; bonus talent du pire impact direct (permanent et
                            // conditionnels positifs à leur facteur maximal), compétences et frappes de réaction.
                            ModifierSet modifiers = ModifierSet.For(character);
                            DerivedStats stats = CombatantStats.ForCharacter(Stats, character.Build, ModifierSet.Empty);
                            stats.MaxHealth.RoundToInt(RoundingMode.Floor);
                            stats.MaxEndurance.RoundToInt(RoundingMode.Floor);
                            overflowPath = path + ".class";
                            overflowMessage = "statistiques dérivées hors des limites numériques avec les modificateurs permanents de la classe (maxHealth, endurance.max, statusResistance).";
                            stats = CombatantStats.ForCharacter(Stats, character.Build, modifiers);
                            stats.MaxHealth.RoundToInt(RoundingMode.Floor);
                            stats.MaxEndurance.RoundToInt(RoundingMode.Floor);
                            overflowPath = path + ".build.attributes";
                            overflowMessage = "statistiques dérivées hors des limites numériques.";
                            defenses[i] = stats;
                            CombatantRules rules = CombatantRules.For(Combat, modifiers);
                            // Sous-lot 8.2 (X4) : valeur d'un bouclier posé par ce personnage, au coefficient maximal d'une application,
                            // avec sa puissance de boucliers (même fonction que le moteur).
                            if (Combat.Statuses.HasShields)
                            {
                                overflowPath = path + ".class";
                                overflowMessage = $"valeur d'un bouclier hors des limites numériques (PV max × coefficient {SkillCombatDefinition.MaxCoefficient} × (1 + shield.potencyDealt)).";
                                Shields.Value(stats.MaxHealth.RoundToInt(RoundingMode.Floor), SkillCombatDefinition.MaxCoefficient, rules.ShieldPotencyDealt);
                                overflowPath = path + ".build.attributes";
                                overflowMessage = "statistiques dérivées hors des limites numériques.";
                            }
                            Fixed talent = rules.WorstDamageDealt(Combat.ResonanceMax);
                            foreach (SkillDefinition skill in character.Class.Skills)
                                if (skill.Combat is { } combat)
                                {
                                    // Sous-lot 8.3 : une compétence de soutien n'a pas d'impact ; ses DOT éventuels comptent.
                                    // Sous-lot 8.4 (X8) : coefficient au plus de Charges dépensables (toutes les Charges, au plus le maximum
                                    // effectif du personnage, ou le coût fixe), croissant avec elles.
                                    int spendable = combat.RampartChargeSpendAll ? Math.Max(rules.RampartChargesMax, combat.RampartChargeCost) : combat.RampartChargeCost;
                                    if (combat.HasDamage)
                                        offenses[i].Add(new Offense($"compétence « {skill.Id} »", stats, combat.PowerType, combat.DamageType,
                                            rules.Coefficient(skill, combat, spendable), true, talent));
                                    AddDotOffenses(offenses[i], $"compétence « {skill.Id} »", stats, combat.Applies);
                                }
                            // Lot 7.3 (§4.2) : frappes et DOT des réactions, à la puissance du porteur. Un personnage peut porter toute
                            // altération du catalogue : ses déclencheurs comptent pour chacun.
                            foreach (BoundTrigger bound in character.ActiveTriggers())
                                AddTriggerOffenses(offenses[i], $"déclencheur « {bound.Label} »", stats, bound.Definition, bound.Rank, talent);
                            foreach (NamedTrigger named in Combat.Statuses.NamedTriggers())
                                AddTriggerOffenses(offenses[i], $"déclencheur « {named.Label} »", stats, named.Definition, 1, talent);
                            break;
                        case DummySetup dummy:
                            defenses[i] = CombatantStats.ForDefenses(Stats, dummy.Armor, dummy.MagicResistance, dummy.StatusResistance);
                            // Lot 7.3 : un mannequin peut porter une altération à déclencheurs ; ses réactions frappent à sa puissance de base.
                            foreach (NamedTrigger named in Combat.Statuses.NamedTriggers())
                                AddTriggerOffenses(offenses[i], $"déclencheur « {named.Label} »", defenses[i]!, named.Definition, 1);
                            break;
                        case AssailantSetup assailant:
                            defenses[i] = CombatantStats.ForDefenses(Stats, assailant.Armor, assailant.MagicResistance, assailant.StatusResistance);
                            foreach (AssailantAttack attack in assailant.Attacks)
                            {
                                DerivedStats attackStats = CombatantStats.ForAssailantAttack(Stats, attack.RawDamage);
                                offenses[i].Add(new Offense($"attaque « {attack.Id} »", attackStats,
                                    CombatantStats.PowerOf(attack.DamageType), attack.DamageType, Fixed.One, false));
                                AddDotOffenses(offenses[i], $"attaque « {attack.Id} »", attackStats, attack.Applies);
                            }
                            // Lot 7.3 : déclencheurs de l'assaillant et des altérations qu'il peut porter, à sa puissance de base.
                            for (int k = 0; k < assailant.Triggers.Count; k++)
                                AddTriggerOffenses(offenses[i], $"déclencheur « {assailant.TriggerDefinitionId}/{k} »", defenses[i]!, assailant.Triggers[k], 1);
                            foreach (NamedTrigger named in Combat.Statuses.NamedTriggers())
                                AddTriggerOffenses(offenses[i], $"déclencheur « {named.Label} »", defenses[i]!, named.Definition, 1);
                            break;
                    }
                }
                catch (OverflowException)
                {
                    errors.Add(new RuleViolation(RuleViolationKind.NumericLimit, overflowMessage, overflowPath));
                    defenses[i] = null;
                    offenses[i].Clear();
                }
            }

            // 1. Défenses réelles de chaque participant et dénominateur de la formule, pour les deux types de dégâts.
            var usable = new bool[count];
            for (int i = 0; i < count; i++)
            {
                if (defenses[i] is not { } stats) continue;
                usable[i] = true;
                foreach (DamageType type in new[] { DamageType.Physique, DamageType.Magique })
                {
                    Fixed effective = stats.GetEffectiveDefense(type);
                    long constant = type == DamageType.Physique ? Damage.ArmorConstant : Damage.MagicResistanceConstant;
                    string field = DefenseField(i, type);
                    string label = type == DamageType.Physique ? "armure" : "résistance magique";
                    if (effective > CombatantStats.MaxDefense)
                    {
                        errors.Add(new RuleViolation(RuleViolationKind.NumericLimit,
                            $"{label} effective calculée ({effective}) au-delà de la limite technique {CombatantStats.MaxDefense}" +
                            (Combatants[i] is CharacterSetup ? " (valeur de base des règles et apports du personnage)." : "."), field));
                        usable[i] = false;
                    }
                    else if (!Representable(() => checked(constant * Fixed.Scale + effective.Raw)))
                    {
                        errors.Add(new RuleViolation(RuleViolationKind.NumericLimit,
                            $"dénominateur de défense non représentable (constante {constant}, {label} {effective}).", field));
                        usable[i] = false;
                    }
                }
                // Lot 7.2 : résistance aux altérations, même enveloppe que les défenses ; le facteur K / (K + résistance) a le même
                // dénominateur que la formule de défense. La durée réduite (ticks de base ≤ 86 400 000 × facteur brut ≤ 10⁴) reste < 10¹².
                Fixed statusResistance = stats.StatusResistance;
                string statusField = Combatants[i] is CharacterSetup ? $"combatants[{i}]" : $"combatants[{i}].statusResistance";
                if (statusResistance > CombatantStats.MaxDefense)
                    errors.Add(new RuleViolation(RuleViolationKind.NumericLimit,
                        $"résistance aux altérations effective calculée ({statusResistance}) au-delà de la limite technique {CombatantStats.MaxDefense}.", statusField));
                else if (!Representable(() => checked(Stats.StatusResistanceConstant * Fixed.Scale + statusResistance.Raw)))
                    errors.Add(new RuleViolation(RuleViolationKind.NumericLimit,
                        $"dénominateur de résistance aux altérations non représentable (constante {Stats.StatusResistanceConstant}, résistance {statusResistance}).", statusField));
            }

            // 2. Pire impact de chaque attaque contre chaque défenseur ennemi réel ; 3. majorant sans défense, cumulé sur la durée.
            DerivedStats undefended = CombatantStats.ForDefenses(Stats, Fixed.Zero, Fixed.Zero);
            // Lot 7.2 : majorant des bonus de dégâts reçus, toutes les altérations du catalogue actives à la fois. Sous-lot 8.2 (X3) : plus
            // les dégâts infligés de tous les renforcements du catalogue, même catégorie « altération ».
            DamageBonuses worstBonuses = DamageBonuses.None.Add(BonusCategory.Alteration,
                Combat.Statuses.TotalDamageTakenIncrease() + Combat.Statuses.TotalBuffDamageDealt());
            long maxImpacts = checked(DurationMs / Combat.TickMilliseconds * Combat.MaxEventsPerTick);
            for (int i = 0; i < count; i++)
            {
                string path = $"combatants[{i}]";
                long worstHit = 0;
                string? worstLabel = null;
                bool failed = false;
                foreach (Offense offense in offenses[i])
                {
                    if (failed) break;
                    if (!TryWorstHit(offense, undefended, worstBonuses, out long hit))
                    {
                        errors.Add(new RuleViolation(RuleViolationKind.NumericLimit, offense.Periodic
                            ? $"pire tick de {offense.Label} hors des limites numériques du moteur (formule périodique, puissance figée, aucune défense)."
                            : $"pire impact de {offense.Label} hors des limites numériques du moteur (critique, Fragilité, aucune défense).", path));
                        failed = true;
                        break;
                    }
                    if (hit > worstHit || worstLabel is null)
                    {
                        worstHit = Math.Max(worstHit, hit);
                        worstLabel = (offense.Periodic ? "tick de " : "impact de ") + offense.Label;
                    }
                    for (int j = 0; j < count; j++)
                    {
                        if (!usable[j] || Combatants[j].Team == Combatants[i].Team) continue;
                        if (!TryWorstHit(offense, defenses[j]!, worstBonuses, out _))
                        {
                            errors.Add(new RuleViolation(RuleViolationKind.NumericLimit,
                                $"pire impact de {offense.Label} contre « {Combatants[j].Id} » hors des limites numériques du moteur.", path));
                            failed = true;
                            break;
                        }
                    }
                }
                if (!failed && !Representable(() => checked(worstHit * maxImpacts)))
                    errors.Add(new RuleViolation(RuleViolationKind.NumericLimit,
                        $"cumul des dégâts sur la durée du combat hors des limites numériques (pire impact {worstHit}, {worstLabel}, × {maxImpacts} événements comptés au plus).", path));
            }
        }

        /// <summary>
        /// Lot 7.2 : chaque tick d'un DOT est un impact sans critique dont la puissance figée vaut la puissance du type de l'altération
        /// (dégâts bruts pour un assaillant) × le coefficient de l'application. Correctif 7.3.1 (A73-M01) : son pire impact est calculé
        /// avec la formule périodique du moteur (<see cref="Offense.Dot"/>), pas avec celle des dégâts directs.
        /// </summary>
        private void AddDotOffenses(List<Offense> offenses, string owner, DerivedStats attacker, IReadOnlyList<StatusApplication> applies)
        {
            foreach (StatusApplication application in applies)
                if (Combat.Statuses.TryGet(application.Status, out StatusDefinition? definition) && definition.IsDot
                    && definition.DamageType is { } type && application.Coefficient is { } coefficient)
                    offenses.Add(Offense.Dot($"altération « {definition.Id} » de {owner}", attacker, definition, coefficient));
        }

        /// <summary>
        /// Lot 7.3 : une frappe de réaction est un impact sans critique (Q7) de coefficient × puissance du porteur ; un DOT appliqué par
        /// réaction a pour puissance figée celle du porteur. Un porteur sans puissance (mannequin, assaillant) inflige le minimum de
        /// dégâts des règles : il ne majore rien.
        /// </summary>
        private void AddTriggerOffenses(List<Offense> offenses, string owner, DerivedStats attacker, TriggerDefinition trigger, int rank, Fixed talent = default)
        {
            foreach (TriggerEffect e in trigger.Effects)
            {
                if (e.Kind == TriggerEffectKind.Strike && e.Coefficient is { } coefficient && e.PowerType is { } power && e.DamageType is { } type)
                    offenses.Add(new Offense($"frappe du {owner}", attacker, power, type, coefficient.At(rank), false, talent));
                else if (e.Kind == TriggerEffectKind.ApplyStatus && e.Status != null && e.Coefficient is { } dot
                    && Combat.Statuses.TryGet(e.Status, out StatusDefinition? definition) && definition.IsDot && definition.DamageType != null)
                    offenses.Add(Offense.Dot($"altération « {definition.Id} » du {owner}", attacker, definition, dot.At(rank)));
            }
        }

        private string DefenseField(int index, DamageType type) => Combatants[index] is CharacterSetup
            ? $"combatants[{index}]"
            : $"combatants[{index}]." + (type == DamageType.Physique ? "armor" : "magicResistance");

        /// <summary>
        /// Pire impact d'une offensive contre un défenseur, calculé par la formule exacte du moteur : <see cref="DamageCalculator.Compute"/>
        /// pour un impact direct (bonus de catégorie, critique, sans garde ni parade) ; pour un tick de DOT, puissance figée calculée
        /// comme à l'application (puissance × coefficient, même arrondi) puis <see cref="DamageCalculator.ComputePeriodic"/>, sans bonus
        /// ni critique, réduite par la défense seulement si l'altération l'est.
        /// </summary>
        private bool TryWorstHit(Offense offense, DerivedStats defender, DamageBonuses bonuses, out long hit)
        {
            try
            {
                hit = offense.Periodic
                    ? DamageCalculator.ComputePeriodic(offense.Attacker.GetPower(offense.Power) * offense.Coefficient, defender, offense.Type,
                        offense.ReducedByDefense, Damage)
                    : DamageCalculator.Compute(new DamageInput(offense.Attacker, defender, offense.Power, offense.Type, offense.Coefficient,
                        offense.Talent == Fixed.Zero ? bonuses : bonuses.Add(BonusCategory.Talent, offense.Talent), offense.CanCrit, Fixed.Zero), Damage).FinalDamage;
                return true;
            }
            catch (OverflowException)
            {
                hit = 0;
                return false;
            }
        }

        private static bool Representable(Func<long> computation)
        {
            try
            {
                computation();
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
        }
    }

    /// <summary>Levée quand on construit un moteur avec un combat invalide ; <see cref="Violations"/> donne le détail.</summary>
    public sealed class CombatSetupException : ArgumentException
    {
        public IReadOnlyList<RuleViolation> Violations { get; }

        public CombatSetupException(IReadOnlyList<RuleViolation> violations)
            : base("Combat invalide : " + string.Join(" ; ", violations.Select(v => v.ToString())))
        {
            Violations = violations;
        }
    }
}
