using System;
using System.Collections.Generic;
using EdS.Domain.Collections;
using EdS.Domain.Combat;

namespace EdS.Net
{
    /// <summary>
    /// Lot 10 : constantes du protocole réseau. Un message est un objet JSON strict (analyseur du sous-lot 9.1) précédé de sa longueur sur
    /// 4 octets (gros-boutiste). Les tailles sont bornées par sens : un client n'envoie que de petits messages (intentions, présentation),
    /// le serveur peut envoyer l'état courant d'un combat. Version 2 depuis le sous-lot 11.7 : marche dans les intentions, déplacements et
    /// sélections dans les commandes, état courant (<see cref="StateMessage"/>, instantané du moteur) à la place de l'historique. Version 3
    /// depuis le sous-lot 11.9 : marche à reculons (<c>backward</c>) dans les intentions et les commandes de marche. Version 4 depuis le
    /// sous-lot V1.8 : six places actives et l'ultime (<see cref="MaxSlot"/> 6 : l'emplacement 4 est désormais la 5e active, plus
    /// l'ultime) ; un client d'une autre version est refusé dès la présentation.
    /// Détail : <c>docs/RESEAU.md</c>.
    /// </summary>
    public static class Protocol
    {
        public const int Version = 4;

        /// <summary>Port TCP par défaut du serveur (hôte Unity ou serveur dédié).</summary>
        public const int DefaultPort = 7777;

        /// <summary>Taille maximale d'un message reçu par le serveur (envoyé par un client) : 8 Kio.</summary>
        public const int MaxClientMessageBytes = 8 * 1024;

        /// <summary>Taille maximale d'un message reçu par un client (envoyé par le serveur) : 4 Mio, assez pour l'état courant d'un combat.</summary>
        public const int MaxServerMessageBytes = 4 * 1024 * 1024;

        /// <summary>Longueur maximale d'un nom de joueur (caractères UTF-16).</summary>
        public const int MaxNameLength = 24;

        /// <summary>Longueur maximale d'un identifiant (build, talent, combattant, compétence).</summary>
        public const int MaxIdLength = 128;

        /// <summary>Début du refus envoyé à chaque joueur quand le combat ne peut pas commencer (revue Codex de la PR #21, tour 4).</summary>
        public const string CombatRefused = "combat refusé : ";

        /// <summary>Talents au plus dans un build envoyé par un client.</summary>
        public const int MaxTalents = 256;

        /// <summary>Commandes au plus dans un tick (deux joueurs, plafond absolu par acteur et par tick : 64).</summary>
        public const int MaxCommandsPerTick = 256;

        /// <summary>Places au plus dans une salle.</summary>
        public const int MaxSeats = 8;

        /// <summary>Longueur d'un jeton de reconnexion : 32 chiffres hexadécimaux (128 bits).</summary>
        public const int TokenLength = 32;

        /// <summary>Longueur maximale d'un texte libre (raison d'un refus, d'une fin de combat).</summary>
        public const int MaxReasonLength = 512;

        /// <summary>
        /// Emplacement le plus haut d'une compétence (actives 0 à 5, ultime 6 : <c>RealtimeCombat.UltimateSlot</c>). Protocole 4 (sous-lot
        /// V1.8) : six actives et l'ultime ; au protocole 3, 4 était l'ultime (un client de cette version est refusé à la présentation).
        /// </summary>
        public const int MaxSlot = EdS.Runtime.RealtimeCombat.UltimateSlot;

        /// <summary>
        /// Sous-lot 10.2 : texte libre rendu acceptable par <see cref="MessageCodec.IsText"/> (raison d'un refus, nom de build) : caractères
        /// de contrôle et demi-paires remplacés par une espace, espaces de bord retirées, longueur ramenée à <paramref name="max"/> sans couper
        /// une paire de substitution ; « ? » si rien ne reste.
        /// </summary>
        public static string Text(string? text, int max = MaxReasonLength)
        {
            if (string.IsNullOrEmpty(text)) return "?";
            var clean = new System.Text.StringBuilder(text!.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    clean.Append(c).Append(text[++i]);
                    continue;
                }
                clean.Append(char.IsControl(c) || char.IsSurrogate(c) ? ' ' : c);
            }
            string result = clean.ToString().Trim();
            if (result.Length > max)
            {
                int cut = char.IsHighSurrogate(result[max - 1]) ? max - 1 : max;
                result = result.Substring(0, cut).TrimEnd();
            }
            return result.Length == 0 ? "?" : result;
        }
    }

    /// <summary>Sens d'un message : un serveur n'accepte que les messages de client, un client que ceux du serveur.</summary>
    public enum MessageDirection
    {
        ClientToServer,
        ServerToClient,
    }

    /// <summary>Message du protocole ; <see cref="Type"/> est le champ <c>type</c> de l'objet JSON.</summary>
    public abstract class NetMessage
    {
        public abstract string Type { get; }
        public abstract MessageDirection Direction { get; }
    }

    /// <summary>Build demandé par un joueur : un build de référence, éventuellement avec ses propres rangs de talents (écran des talents).</summary>
    public sealed class BuildChoice
    {
        public string Reference { get; }

        /// <summary>Rangs de talents qui remplacent ceux du build de référence ; null pour le build de référence tel quel.</summary>
        public IReadOnlyDictionary<string, int>? Talents { get; }

        public BuildChoice(string reference, IReadOnlyDictionary<string, int>? talents = null)
        {
            Reference = reference ?? throw new ArgumentNullException(nameof(reference));
            Talents = talents is null ? null : Frozen.Dictionary(talents, nameof(talents));
        }
    }

    // --- Client → serveur ----------------------------------------------------------------------------------------------------

    /// <summary>Première demande d'un joueur : version du protocole, nom, build voulu.</summary>
    public sealed class HelloMessage : NetMessage
    {
        public const string Id = "hello";
        public override string Type => Id;
        public override MessageDirection Direction => MessageDirection.ClientToServer;
        public int ProtocolVersion { get; }
        public string Name { get; }
        public BuildChoice Build { get; }

        public HelloMessage(int protocolVersion, string name, BuildChoice build)
        {
            ProtocolVersion = protocolVersion;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Build = build ?? throw new ArgumentNullException(nameof(build));
        }
    }

    /// <summary>Retour d'un joueur déconnecté, avec le jeton reçu dans <see cref="WelcomeMessage"/>.</summary>
    public sealed class ReconnectMessage : NetMessage
    {
        public const string Id = "reconnect";
        public override string Type => Id;
        public override MessageDirection Direction => MessageDirection.ClientToServer;
        public int ProtocolVersion { get; }
        public string Token { get; }

        public ReconnectMessage(int protocolVersion, string token)
        {
            ProtocolVersion = protocolVersion;
            Token = token ?? throw new ArgumentNullException(nameof(token));
        }
    }

    /// <summary>Changement de build dans la salle d'attente.</summary>
    public sealed class BuildMessage : NetMessage
    {
        public const string Id = "build";
        public override string Type => Id;
        public override MessageDirection Direction => MessageDirection.ClientToServer;
        public BuildChoice Build { get; }

        public BuildMessage(BuildChoice build) => Build = build ?? throw new ArgumentNullException(nameof(build));
    }

    /// <summary>Prêt (ou plus prêt) à commencer le combat.</summary>
    public sealed class ReadyMessage : NetMessage
    {
        public const string Id = "ready";
        public override string Type => Id;
        public override MessageDirection Direction => MessageDirection.ClientToServer;
        public bool Ready { get; }

        public ReadyMessage(bool ready) => Ready = ready;
    }

    /// <summary>
    /// Intentions du joueur : garde tenue, parade demandée, compétence d'un emplacement (null : aucune), cible ennemie et allié visé
    /// (null : choix du serveur), cap de marche (sous-lot 11.7). Le serveur les applique au prochain tick qu'il simule ; la garde et la
    /// marche sont des états, parade et compétence des demandes ponctuelles.
    /// </summary>
    public sealed class InputMessage : NetMessage
    {
        public const string Id = "input";
        public override string Type => Id;
        public override MessageDirection Direction => MessageDirection.ClientToServer;
        public bool Guard { get; }
        public bool Parry { get; }
        public int? Slot { get; }
        public string? Target { get; }
        public string? Ally { get; }

        /// <summary>Sous-lot 11.7 : cap de marche voulu (0 à 63, <c>MoveInput</c>), null : arrêt ; sans effet hors combat positionné.</summary>
        public int? Heading { get; }

        /// <summary>Sous-lot 11.9 (protocole 3) : marche à reculons (le personnage garde son orientation) ; faux à l'arrêt.</summary>
        public bool Backward { get; }

        public InputMessage(bool guard, bool parry, int? slot, string? target, string? ally, int? heading = null, bool backward = false)
        {
            Guard = guard;
            Parry = parry;
            Slot = slot;
            Target = target;
            Ally = ally;
            Heading = heading;
            Backward = heading != null && backward;
        }
    }

    /// <summary>Mesure du temps aller-retour : le serveur renvoie <see cref="ClientTime"/> tel quel.</summary>
    public sealed class PingMessage : NetMessage
    {
        public const string Id = "ping";
        public override string Type => Id;
        public override MessageDirection Direction => MessageDirection.ClientToServer;
        public long ClientTime { get; }

        public PingMessage(long clientTime) => ClientTime = clientTime;
    }

    /// <summary>Demande de l'historique complet du combat (empreinte différente, ticks manquants).</summary>
    public sealed class ResyncRequestMessage : NetMessage
    {
        public const string Id = "resyncRequest";
        public override string Type => Id;
        public override MessageDirection Direction => MessageDirection.ClientToServer;
    }

    /// <summary>Départ volontaire (la place est libérée sans délai de reconnexion).</summary>
    public sealed class ByeMessage : NetMessage
    {
        public const string Id = "bye";
        public override string Type => Id;
        public override MessageDirection Direction => MessageDirection.ClientToServer;
    }

    // --- Serveur → client ----------------------------------------------------------------------------------------------------

    /// <summary>Place attribuée (identifiant du personnage joué), jeton de reconnexion, durée du tick.</summary>
    public sealed class WelcomeMessage : NetMessage
    {
        public const string Id = "welcome";
        public override string Type => Id;
        public override MessageDirection Direction => MessageDirection.ServerToClient;
        public int ProtocolVersion { get; }
        public string Seat { get; }
        public string Token { get; }
        public int TickMs { get; }

        public WelcomeMessage(int protocolVersion, string seat, string token, int tickMs)
        {
            ProtocolVersion = protocolVersion;
            Seat = seat ?? throw new ArgumentNullException(nameof(seat));
            Token = token ?? throw new ArgumentNullException(nameof(token));
            TickMs = tickMs;
        }
    }

    /// <summary>État d'une place de la salle.</summary>
    public sealed class SeatInfo
    {
        /// <summary>Identifiant du personnage de la place dans le scénario.</summary>
        public string Id { get; }

        /// <summary>Nom du joueur ; null pour une place libre.</summary>
        public string? Player { get; }
        public bool Connected { get; }
        public bool Ready { get; }

        /// <summary>Nom du build choisi ; null pour une place libre.</summary>
        public string? Build { get; }

        public SeatInfo(string id, string? player, bool connected, bool ready, string? build)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Player = player;
            Connected = connected;
            Ready = ready;
            Build = build;
        }
    }

    /// <summary>Phase de la salle.</summary>
    public enum RoomPhase
    {
        /// <summary>Salle d'attente : les joueurs arrivent, choisissent leur build et se déclarent prêts.</summary>
        Lobby,

        /// <summary>Combat en cours.</summary>
        Combat,

        /// <summary>Combat terminé ; la salle revient en attente quand tous les joueurs sont de nouveau prêts.</summary>
        Ended,
    }

    /// <summary>Places et phase de la salle, envoyé à chaque changement.</summary>
    public sealed class LobbyMessage : NetMessage
    {
        public const string Id = "lobby";
        public override string Type => Id;
        public override MessageDirection Direction => MessageDirection.ServerToClient;
        public RoomPhase Phase { get; }
        public IReadOnlyList<SeatInfo> Seats { get; }

        public LobbyMessage(RoomPhase phase, IReadOnlyList<SeatInfo> seats)
        {
            Phase = phase;
            Seats = Frozen.List(seats, nameof(seats));
        }
    }

    /// <summary>
    /// Début du combat : la ligne d'en-tête du journal du serveur (règles, empreintes des données, scénario avec les builds choisis).
    /// Le client en reconstruit le même combat avec ses propres données, après les mêmes contrôles d'identité qu'un rejeu.
    /// </summary>
    public sealed class StartMessage : NetMessage
    {
        public const string Id = "start";
        public override string Type => Id;
        public override MessageDirection Direction => MessageDirection.ServerToClient;
        public string Header { get; }

        public StartMessage(string header) => Header = header ?? throw new ArgumentNullException(nameof(header));
    }

    /// <summary>Commandes admises par le serveur pour un tick, dans l'ordre de leur soumission.</summary>
    public sealed class TickCommands
    {
        public long Tick { get; }
        public IReadOnlyList<CombatCommand> Commands { get; }

        public TickCommands(long tick, IReadOnlyList<CombatCommand> commands)
        {
            Tick = tick;
            Commands = Frozen.List(commands, nameof(commands));
        }
    }

    /// <summary>
    /// Tick simulé par le serveur : ses commandes admises et l'empreinte du journal jusqu'à ce tick compris (<c>fnv1a64:…</c>). Le client
    /// soumet les mêmes commandes, simule le tick et compare l'empreinte.
    /// </summary>
    public sealed class TickMessage : NetMessage
    {
        public const string Id = "tick";
        public override string Type => Id;
        public override MessageDirection Direction => MessageDirection.ServerToClient;
        public TickCommands Commands { get; }
        public string Hash { get; }

        public TickMessage(TickCommands commands, string hash)
        {
            Commands = commands ?? throw new ArgumentNullException(nameof(commands));
            Hash = hash ?? throw new ArgumentNullException(nameof(hash));
        }
    }

    /// <summary>
    /// Sous-lot 11.7 : état courant du combat, sans son passé : en-tête du combat complété à cet instant (scénario, arrivées et commandes
    /// admises), instantané du moteur entre deux ticks (<c>CombatEngine.Snapshot</c>) et empreinte du journal à ce point. Envoyé à un joueur
    /// qui arrive pendant le combat, à tous quand un joueur arrive, au retour d'un joueur et à la demande d'un client désynchronisé ; le
    /// client reconstruit le moteur sans rien rejouer (<c>CombatEngine.Restore</c>) et ne réattribue aucun événement passé.
    /// </summary>
    public sealed class StateMessage : NetMessage
    {
        public const string Id = "state";
        public override string Type => Id;
        public override MessageDirection Direction => MessageDirection.ServerToClient;
        public string Header { get; }
        public string Snapshot { get; }
        public string Hash { get; }

        public StateMessage(string header, string snapshot, string hash)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            Hash = hash ?? throw new ArgumentNullException(nameof(hash));
        }
    }

    /// <summary>Intention refusée par le serveur (plafond de commandes, file pleine…) : rien n'a été soumis au moteur.</summary>
    public sealed class RejectedMessage : NetMessage
    {
        public const string Id = "rejected";
        public override string Type => Id;
        public override MessageDirection Direction => MessageDirection.ServerToClient;
        public string Reason { get; }

        public RejectedMessage(string reason) => Reason = reason ?? throw new ArgumentNullException(nameof(reason));
    }

    /// <summary>Réponse à <see cref="PingMessage"/>.</summary>
    public sealed class PongMessage : NetMessage
    {
        public const string Id = "pong";
        public override string Type => Id;
        public override MessageDirection Direction => MessageDirection.ServerToClient;
        public long ClientTime { get; }

        public PongMessage(long clientTime) => ClientTime = clientTime;
    }

    /// <summary>Fin du combat : dernier tick simulé et raison (durée atteinte, équipe vaincue).</summary>
    public sealed class EndMessage : NetMessage
    {
        public const string Id = "end";
        public override string Type => Id;
        public override MessageDirection Direction => MessageDirection.ServerToClient;
        public long Tick { get; }
        public string Reason { get; }

        public EndMessage(long tick, string reason)
        {
            Tick = tick;
            Reason = reason ?? throw new ArgumentNullException(nameof(reason));
        }
    }

    /// <summary>Refus définitif (version, salle pleine, message invalide…) : le serveur ferme ensuite la connexion.</summary>
    public sealed class ErrorMessage : NetMessage
    {
        public const string Id = "error";
        public override string Type => Id;
        public override MessageDirection Direction => MessageDirection.ServerToClient;
        public string Reason { get; }

        public ErrorMessage(string reason) => Reason = reason ?? throw new ArgumentNullException(nameof(reason));
    }
}
