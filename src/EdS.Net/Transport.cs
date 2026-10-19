using System;

namespace EdS.Net
{
    /// <summary>
    /// Lot 10 : connexion à messages, quel que soit le réseau (TCP réel ou réseau simulé des tests). Aucune méthode ne bloque : l'appelant
    /// (boucle de la salle, image de Unity) envoie et relève les messages à son rythme. Les messages arrivent entiers et dans l'ordre
    /// d'envoi ; ceux déjà reçus restent lisibles après la fermeture.
    /// </summary>
    public interface INetConnection : IDisposable
    {
        /// <summary>Vrai tant que la connexion peut envoyer.</summary>
        bool IsOpen { get; }

        /// <summary>Raison de la fermeture ; null tant qu'elle est ouverte.</summary>
        string? CloseReason { get; }

        /// <summary>Adresse de l'autre extrémité, pour les journaux.</summary>
        string RemoteAddress { get; }

        /// <summary>
        /// Envoie un message (contenu seul) ; false si la connexion est fermée. Un message plus grand que ce que l'autre côté accepte
        /// ferme la connexion (faute de protocole) et rend false.
        /// </summary>
        bool Send(byte[] payload);

        /// <summary>Prochain message reçu ; false s'il n'y en a pas pour l'instant.</summary>
        bool TryReceive(out byte[] payload);

        /// <summary>Ferme la connexion : les messages déjà envoyés partent encore, plus rien n'est accepté. Sans effet si déjà fermée.</summary>
        void Close(string reason);
    }

    /// <summary>Écoute de connexions entrantes, sans blocage.</summary>
    public interface INetListener : IDisposable
    {
        /// <summary>Prochaine connexion entrante ; false s'il n'y en a pas pour l'instant.</summary>
        bool TryAccept(out INetConnection connection);
    }

    /// <summary>Envoi et lecture de messages typés sur une connexion.</summary>
    public static class NetConnectionExtensions
    {
        public static bool Send(this INetConnection connection, NetMessage message) =>
            connection.Send(MessageCodec.Encode(message ?? throw new ArgumentNullException(nameof(message))));

        /// <summary>
        /// Prochain message reçu et lu dans le sens <paramref name="direction"/>. Un message illisible ferme la connexion (faute de
        /// protocole) : rend false avec la raison dans <paramref name="error"/>.
        /// </summary>
        public static bool TryReceive(this INetConnection connection, MessageDirection direction, out NetMessage? message, out string? error)
        {
            message = null;
            error = null;
            if (!connection.TryReceive(out byte[] payload)) return false;
            message = MessageCodec.Decode(payload, direction, out error);
            if (message is null) connection.Close("message refusé : " + error);
            return true;
        }
    }
}
