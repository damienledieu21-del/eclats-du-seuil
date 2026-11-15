using System;
using System.Collections.Generic;
using EdS.Domain.Collections;
using EdS.Domain.Stats;
using EdS.Domain.Numerics;

namespace EdS.Domain.Combat
{
    // Compagnons (sujet 11 de la feuille de route, décisions de Damien du 14 novembre 2026, CADRAGE.md § 7 bis) : un compagnon est un
    // assaillant allié, guidé par l'ordinateur, attaché à un personnage (son « Éveillé »). Le joueur lui donne des ordres simples ; sinon il
    // se débrouille seul. Une jauge de lien (0 à 100) se remplit par les actions coordonnées ; pleine, elle permet une attaque à deux,
    // jamais déclenchée automatiquement.

    /// <summary>Ordre simple donné à un compagnon (décision de Damien : suivre, attendre, cibler, protéger, revenir).</summary>
    public enum CompanionOrder
    {
        /// <summary>Suivre (ordre par défaut) : reste près de l'Éveillé et attaque l'ennemi le plus proche à portée de laisse de l'Éveillé.</summary>
        Follow = 0,

        /// <summary>Attendre : reste à la place où l'ordre l'a trouvé et ne frappe que ce qui vient à lui.</summary>
        Wait = 1,

        /// <summary>Cibler : attaque l'ennemi désigné ; quand il tombe ou part, l'ordre revient à « suivre ».</summary>
        Target = 2,

        /// <summary>Protéger : s'en prend à l'ennemi le plus proche de l'Éveillé et reste près de lui.</summary>
        Protect = 3,

        /// <summary>
        /// Revenir : annule une attaque annoncée qui n'a pas encore frappé, n'attaque plus et rejoint l'Éveillé ; arrivé près de lui, l'ordre
        /// revient à « suivre ».
        /// </summary>
        Return = 4,
    }

    /// <summary>Identifiants des ordres dans les fichiers et le journal.</summary>
    public static class CompanionOrderIds
    {
        public const string Follow = "follow";
        public const string Wait = "wait";
        public const string Target = "target";
        public const string Protect = "protect";
        public const string Return = "return";

        /// <summary>Les cinq ordres, dans l'ordre du menu rapide (au plus cinq, dossier p. 35).</summary>
        public static IReadOnlyList<string> All { get; } = Frozen.List(new[] { Follow, Wait, Target, Protect, Return }, nameof(All));

        public static string Of(CompanionOrder order) => order switch
        {
            CompanionOrder.Follow => Follow,
            CompanionOrder.Wait => Wait,
            CompanionOrder.Target => Target,
            CompanionOrder.Protect => Protect,
            CompanionOrder.Return => Return,
            _ => throw new ArgumentOutOfRangeException(nameof(order), order, null),
        };

        public static bool TryParse(string? id, out CompanionOrder order)
        {
            switch (id)
            {
                case Follow: order = CompanionOrder.Follow; return true;
                case Wait: order = CompanionOrder.Wait; return true;
                case Target: order = CompanionOrder.Target; return true;
                case Protect: order = CompanionOrder.Protect; return true;
                case Return: order = CompanionOrder.Return; return true;
                default: order = default; return false;
            }
        }
    }

    /// <summary>
    /// Attaque à deux d'un compagnon et de son Éveillé (dossier p. 34 et 42) : déclenchée par le joueur seulement (commande <c>duo</c>), quand
    /// la jauge de lien est pleine, les deux partenaires présents et la cible commune à portée de chacun. La jauge est dépensée au départ ;
    /// après l'annonce (<see cref="TelegraphMs"/>), le coup part du compagnon ; l'Éveillé ne lance aucune compétence jusqu'à la fin de la
    /// récupération (<see cref="RecoveryMs"/>, comptée depuis le départ).
    /// </summary>
    public sealed class CompanionDuo
    {
        /// <summary>Annonce et récupération au plus 10 s (borne technique ; le dossier propose 0,6 à 1 s d'annonce et 1 à 2 s de récupération).</summary>
        public const long MaxPhaseMs = 10_000;

        /// <summary>Identifiant de l'attaque (nom de l'attaque à deux : <c>contre_jour</c>…), distinct des attaques du compagnon.</summary>
        public string Id { get; }

        public long RawDamage { get; }
        public DamageType DamageType { get; }
        public long TelegraphMs { get; }
        public long RecoveryMs { get; }

        /// <summary>Distance entre les bords au plus, de la cible à chacun des deux partenaires.</summary>
        public Fixed Range { get; }

        public CompanionDuo(string id, long rawDamage, DamageType damageType, long telegraphMs, long recoveryMs, Fixed range)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            RawDamage = rawDamage;
            DamageType = damageType;
            TelegraphMs = telegraphMs;
            RecoveryMs = recoveryMs;
            Range = range;
        }
    }

    /// <summary>
    /// Ce qui fait d'un assaillant un compagnon : son Éveillé (<see cref="Owner"/>, un personnage de la même équipe), sa jauge de lien au
    /// départ, ses distances de suite et de laisse, ses gains de lien et son attaque à deux (null : pas encore d'attaque à deux, comme Cendre
    /// louveteau). Un compagnon a toujours un comportement (sans distance d'engagement, ni errance, ni fuite) et des PV : à 0 PV, il est
    /// <b>hors combat</b> (ligne <c>died</c> du moteur), jamais mort pour de bon (règle de la progression).
    /// </summary>
    public sealed class CompanionSetup
    {
        /// <summary>Jauge de lien pleine (dossier p. 34 : de 0 à 100).</summary>
        public const int BondMax = 100;

        /// <summary>Coût d'une attaque à deux : toute la jauge.</summary>
        public const int DuoCost = 100;

        /// <summary>Au plus deux compagnons par Éveillé dans un combat (décision de Damien : un, puis deux).</summary>
        public const int MaxPerOwner = 2;

        /// <summary>Distance de suite au plus (borne technique), en mètres.</summary>
        public static readonly Fixed MaxFollowDistance = Fixed.FromInt(20);

        /// <summary>Fenêtre de coordination au plus (borne technique), en millisecondes.</summary>
        public const long MaxBondWindowMs = 60_000;

        /// <summary>
        /// Gain de lien d'un événement au plus : un quart de la jauge (relecture indépendante de la PR #169 : quatre ticks de coups coordonnés
        /// au moins avant une attaque à deux ; valeur prudente, question posée à Damien).
        /// </summary>
        public const int MaxGain = 25;

        public string Owner { get; }

        /// <summary>Jauge de lien au départ, dans [0, <see cref="BondMax"/>] (elle se garde d'un combat à l'autre, par la sauvegarde).</summary>
        public int Bond { get; }

        /// <summary>« Suivre » et « revenir » : distance entre les bords jusqu'à laquelle il rejoint l'Éveillé.</summary>
        public Fixed FollowDistance { get; }

        /// <summary>« Suivre » et « protéger » : il ne s'en prend qu'aux ennemis à cette distance (bords) de l'Éveillé ; « attendre » : de lui.</summary>
        public Fixed Leash { get; }

        /// <summary>Deux coups des deux partenaires sur la même cible, à au plus cette durée d'écart, sont coordonnés.</summary>
        public long BondWindowMs { get; }

        /// <summary>Gain d'un tick où un coup coordonné a porté (au plus un gain de ce genre par tick).</summary>
        public int CoordinatedGain { get; }

        /// <summary>Gain d'un tick où, sous l'ordre « protéger », il a frappé un ennemi qui visait l'Éveillé (au plus un par tick).</summary>
        public int ProtectGain { get; }

        public CompanionDuo? Duo { get; }

        public CompanionSetup(string owner, int bond, Fixed followDistance, Fixed leash, long bondWindowMs, int coordinatedGain, int protectGain,
            CompanionDuo? duo = null)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Bond = bond;
            FollowDistance = followDistance;
            Leash = leash;
            BondWindowMs = bondWindowMs;
            CoordinatedGain = coordinatedGain;
            ProtectGain = protectGain;
            Duo = duo;
        }
    }
}
