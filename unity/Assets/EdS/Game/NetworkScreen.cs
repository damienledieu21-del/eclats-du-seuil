using System.Collections.Generic;
using System.Linq;
using EdS.Net;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot 10.4 : écrans du jeu en réseau, dessinés avec IMGUI comme le reste de l'interface provisoire : menu (héberger, héberger
    /// avec un équipier robot, rejoindre une adresse, revenir au solo), salle d'attente (places, builds, prêts, adresse à donner à l'autre
    /// joueur), bandeau du combat (temps aller-retour, place, allié visé, coupure et retour).
    /// </summary>
    public sealed class NetworkScreen
    {
        public enum Action
        {
            None,
            Host,
            HostWithBot,
            HostVallons,
            HostVallonsWithBot,
            Join,
            Solo,
            Close,
            Ready,
            NotReady,
            Leave,
        }

        /// <summary>Nom du joueur (1 à 24 caractères).</summary>
        public string PlayerName = "Joueur";

        /// <summary>Adresse de l'hôte à rejoindre.</summary>
        public string Address = "127.0.0.1";

        /// <summary>Message affiché sous le menu (erreur de la dernière tentative).</summary>
        public string Message;

        // Adresses de ce poste, cherchées une fois par partie hébergée (OnGUI dessine plusieurs fois par image).
        private NetworkSession _addressesFor;
        private IReadOnlyList<string> _addresses;

        /// <summary>Menu du réseau, au centre de l'écran.</summary>
        public Action DrawMenu()
        {
            float w = 680f, x = Hud.Width / 2f - w / 2f, y = 120f;
            Hud.Panel(new Rect(x - 16, y - 16, w + 32, 640), new Color(0.05f, 0.045f, 0.04f, 0.92f));
            Hud.Title(new Rect(x, y, w, 36), "Jeu en réseau — deux Bastions contre la meute");
            Hud.Small(new Rect(x, y + 40, w, 24), "Coopération sur le réseau local. Échap ou M : fermer.");

            Hud.Text(new Rect(x, y + 84, 160, 30), "Ton nom :");
            PlayerName = Hud.Field(new Rect(x + 170, y + 82, 300, 34), PlayerName, Protocol.MaxNameLength);

            Action action = Action.None;
            if (Hud.Button(new Rect(x, y + 136, w, 44), $"Héberger la meute à vaincre (port {Protocol.DefaultPort})", true)) action = Action.Host;
            if (Hud.Button(new Rect(x, y + 188, w, 44), "Héberger la meute avec un équipier robot (pour essayer seul)", true)) action = Action.HostWithBot;
            // Sous-lot 11.7 : les Vallons à deux, déplacements résolus par le serveur ; le second joueur peut arriver en plein combat.
            if (Hud.Button(new Rect(x, y + 248, w, 44), "Héberger les Vallons à deux (l'autre peut arriver en plein combat)", true)) action = Action.HostVallons;
            if (Hud.Button(new Rect(x, y + 300, w, 44), "Héberger les Vallons, robot qui arrive après 10 s de combat", true)) action = Action.HostVallonsWithBot;

            Hud.Text(new Rect(x, y + 364, 160, 30), "Adresse :");
            Address = Hud.Field(new Rect(x + 170, y + 362, 300, 34), Address, 64);
            if (Hud.Button(new Rect(x + 480, y + 360, w - 480, 38), "Rejoindre", true)) action = Action.Join;

            if (Hud.Button(new Rect(x, y + 422, w, 40), "Revenir au jeu solo", true)) action = Action.Solo;
            if (Hud.Button(new Rect(x, y + 470, w, 40), "Fermer ce menu", true)) action = Action.Close;
            if (!string.IsNullOrEmpty(Message)) Hud.Small(new Rect(x, y + 522, w, 80), "<color=#ff9a7a>" + Hud.Plain(Message) + "</color>");
            return action;
        }

        /// <summary>Salle d'attente : places, builds et prêts ; l'adresse à donner quand on héberge.</summary>
        public Action DrawLobby(NetworkSession session, string buildName, string lastCombat)
        {
            float w = 760f, x = Hud.Width / 2f - w / 2f, y = 150f;
            Hud.Panel(new Rect(x - 16, y - 16, w + 32, 560), new Color(0.05f, 0.045f, 0.04f, 0.9f));
            Hud.Title(new Rect(x, y, w, 36), session.Host != null ? "Salle d'attente — tu héberges" : "Salle d'attente");

            float line = y + 44;
            if (session.Host != null)
            {
                if (_addressesFor != session)
                {
                    _addressesFor = session;
                    _addresses = NetworkSession.LocalAddresses();
                }
                IReadOnlyList<string> addresses = _addresses;
                string where = addresses.Count == 0 ? "adresse de ce poste inconnue (ipconfig)" : string.Join(" ou ", addresses);
                Hud.Small(new Rect(x, line, w, 24), $"Adresse à donner à l'autre joueur : <b>{where}</b> (port {Protocol.DefaultPort})");
                line += 28;
            }
            else if (session.Address != null)
            {
                Hud.Small(new Rect(x, line, w, 24), "Hôte : " + Hud.Plain(session.Address));
                line += 28;
            }

            NetworkClient client = session.Client;
            Action action = Action.None;
            if (client == null)
            {
                Hud.Text(new Rect(x, line + 10, w, 30), Hud.Plain(session.Error) ?? "Connexion en cours…");
            }
            else if (client.Status == ClientStatus.Connecting)
            {
                Hud.Text(new Rect(x, line + 10, w, 30), "Présentation au serveur…");
            }
            else if (client.Status == ClientStatus.Failed || client.Status == ClientStatus.Left)
            {
                Hud.Text(new Rect(x, line + 10, w, 60), "<color=#ff9a7a>Partie arrêtée : " + Hud.Plain(client.Error ?? "départ.") + "</color>");
            }
            else if (client.Lobby != null)
            {
                SeatInfo mine = null;
                foreach (SeatInfo seat in client.Lobby.Seats)
                {
                    bool me = seat.Id == client.SeatId;
                    if (me) mine = seat;
                    string who = seat.Player == null ? "<color=#8a8a8a>place libre</color>" : Hud.Plain(seat.Player) + (me ? " (toi)" : "");
                    string state = seat.Player == null ? "" : !seat.Connected ? "  <color=#ff9a7a>déconnecté</color>"
                        : seat.Ready ? "  <color=#9be59b>prêt</color>" : "  <color=#d8d8d8>en attente</color>";
                    Hud.Text(new Rect(x, line + 10, w, 30), $"{seat.Id} : {who}{(seat.Build != null ? " — " + Hud.Plain(seat.Build) : "")}{state}");
                    line += 34;
                }
                line += 16;
                bool ready = mine != null && mine.Ready;
                if (Hud.Button(new Rect(x, line, 360, 44), ready ? "Je ne suis plus prêt" : "Je suis prêt", true)) action = ready ? Action.NotReady : Action.Ready;
                if (Hud.Button(new Rect(x + 380, line, w - 380, 44), "Quitter la partie", true)) action = Action.Leave;
                line += 56;
                Hud.Small(new Rect(x, line, w, 24), $"Ton build : {buildName}  ·  F1 à F5 : build de référence  ·  T : talents");
                line += 28;
                Hud.Small(new Rect(x, line, w, 24), "Le combat commence quand les deux joueurs sont prêts.");
                line += 28;
                if (client.LastRejected != null) Hud.Small(new Rect(x, line, w, 48), "<color=#ffcf7a>Refusé : " + Hud.Plain(client.LastRejected) + "</color>");
                line += 30;
                // Fin de combat non vérifiée (divergence non réparée) : à signaler à Claude, le client reste dans la salle.
                if (client.Error != null) Hud.Small(new Rect(x, line, w, 48), "<color=#ff9a7a>" + Hud.Plain(client.Error) + "</color>");
                line += 30;
            }
            // Revue Codex de la PR #21 (tour 6) : erreur de la session (salle hébergée arrêtée, robot qui ne se connecte pas) et arrêt du
            // robot (par exemple build refusé) ; sans cela, sa place restait libre sans explication.
            string problem = session.Error;
            if (problem == null && session.Bot != null && session.Bot.Client.Status == ClientStatus.Failed)
                problem = "robot arrêté : " + (session.Bot.Client.Error ?? "raison inconnue.");
            if (client != null && problem != null) Hud.Small(new Rect(x, y + 440, w, 28), "<color=#ff9a7a>" + Hud.Plain(problem) + "</color>");
            if (!string.IsNullOrEmpty(lastCombat)) Hud.Small(new Rect(x, y + 470, w, 24), Hud.Plain(lastCombat));
            if (client == null || client.Status == ClientStatus.Failed || client.Status == ClientStatus.Left || client.Lobby == null)
                if (Hud.Button(new Rect(x, y + 500, w, 40), "Revenir au menu", true)) action = Action.Leave;
            return action;
        }

        /// <summary>Bandeau du combat en réseau, en haut au centre sous le chrono : temps aller-retour, place, allié ; coupure au centre.</summary>
        public static void DrawCombat(NetworkSession session, string allyId)
        {
            NetworkClient client = session.Client;
            if (client == null) return;
            string ping = client.SmoothedRttMs is double rtt ? $"{rtt:0} ms" : "…";
            // Sous-lot 10.6 (option B de la parade) : la télégraphie des assaillants est avancée du temps aller-retour lissé.
            double lead = client.Combat != null ? client.Combat.TelegraphLeadSeconds * 1000 : 0;
            // Essai réaliste 6 : en haut à droite, sous le minuteur (la cible occupe le haut du milieu).
            float x = Hud.Width - 624f;
            Hud.Panel(new Rect(x, 70, 600, 60), new Color(0.04f, 0.035f, 0.03f, 0.6f));
            Hud.Small(new Rect(x + 10, 72, 580, 24), $"Réseau : temps aller-retour {ping}  ·  télégraphie avancée de {lead:0} ms");
            string robot = session.BotArrivesIn is double inSeconds ? $"  ·  robot dans {inSeconds:0} s" : "";
            Hud.Small(new Rect(x + 10, 98, 580, 24), $"Place {client.SeatId}  ·  allié visé par les soutiens : {allyId ?? "aucun"} (G : changer){robot}");
            if (client.Status == ClientStatus.Disconnected || client.Status == ClientStatus.Reconnecting)
            {
                double left = client.Options.ReconnectSeconds - (client.DisconnectedSeconds ?? 0);
                string text = client.Status == ClientStatus.Reconnecting ? "Retour dans la partie…"
                    : client.CanReconnect ? $"Connexion perdue — nouvelle tentative (encore {System.Math.Max(0, left):0} s)"
                    : "Connexion perdue — retour impossible.";
                Hud.Panel(new Rect(Hud.Width / 2f - 360, 250, 720, 50), new Color(0.35f, 0.05f, 0.05f, 0.85f));
                Hud.Centered(new Rect(Hud.Width / 2f - 360, 250, 720, 50), text, Color.white);
            }
        }
    }
}
