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

        /// <summary>Menu du réseau, au centre de l'écran.</summary>
        public Action DrawMenu()
        {
            float w = 680f, x = Hud.Width / 2f - w / 2f, y = 170f;
            Hud.Fill(new Rect(x - 16, y - 16, w + 32, 520), new Color(0.05f, 0.07f, 0.1f, 0.92f));
            Hud.Title(new Rect(x, y, w, 36), "Jeu en réseau — deux Bastions contre la meute");
            Hud.Small(new Rect(x, y + 40, w, 24), "Coopération sur le réseau local (scénario coop/duo_meute). Échap ou M : fermer.");

            Hud.Text(new Rect(x, y + 84, 160, 30), "Ton nom :");
            PlayerName = Hud.Field(new Rect(x + 170, y + 82, 300, 34), PlayerName, Protocol.MaxNameLength);

            Action action = Action.None;
            if (Hud.Button(new Rect(x, y + 136, w, 44), $"Héberger la partie (port {Protocol.DefaultPort})", true)) action = Action.Host;
            if (Hud.Button(new Rect(x, y + 188, w, 44), "Héberger avec un équipier robot (pour essayer seul)", true)) action = Action.HostWithBot;

            Hud.Text(new Rect(x, y + 252, 160, 30), "Adresse :");
            Address = Hud.Field(new Rect(x + 170, y + 250, 300, 34), Address, 64);
            if (Hud.Button(new Rect(x + 480, y + 248, w - 480, 38), "Rejoindre", true)) action = Action.Join;

            if (Hud.Button(new Rect(x, y + 310, w, 40), "Revenir au jeu solo", true)) action = Action.Solo;
            if (Hud.Button(new Rect(x, y + 358, w, 40), "Fermer ce menu", true)) action = Action.Close;
            if (!string.IsNullOrEmpty(Message)) Hud.Small(new Rect(x, y + 410, w, 80), "<color=#ff9a7a>" + Message + "</color>");
            return action;
        }

        /// <summary>Salle d'attente : places, builds et prêts ; l'adresse à donner quand on héberge.</summary>
        public Action DrawLobby(NetworkSession session, string buildName, string lastCombat)
        {
            float w = 760f, x = Hud.Width / 2f - w / 2f, y = 150f;
            Hud.Fill(new Rect(x - 16, y - 16, w + 32, 560), new Color(0.05f, 0.07f, 0.1f, 0.9f));
            Hud.Title(new Rect(x, y, w, 36), session.Host != null ? "Salle d'attente — tu héberges" : "Salle d'attente");

            float line = y + 44;
            if (session.Host != null)
            {
                IReadOnlyList<string> addresses = NetworkSession.LocalAddresses();
                string where = addresses.Count == 0 ? "adresse de ce poste inconnue (ipconfig)" : string.Join(" ou ", addresses);
                Hud.Small(new Rect(x, line, w, 24), $"Adresse à donner à l'autre joueur : <b>{where}</b> (port {Protocol.DefaultPort})");
                line += 28;
            }
            else if (session.Address != null)
            {
                Hud.Small(new Rect(x, line, w, 24), "Hôte : " + session.Address);
                line += 28;
            }

            NetworkClient client = session.Client;
            Action action = Action.None;
            if (client == null)
            {
                Hud.Text(new Rect(x, line + 10, w, 30), session.Error ?? "Connexion en cours…");
            }
            else if (client.Status == ClientStatus.Connecting)
            {
                Hud.Text(new Rect(x, line + 10, w, 30), "Présentation au serveur…");
            }
            else if (client.Status == ClientStatus.Failed || client.Status == ClientStatus.Left)
            {
                Hud.Text(new Rect(x, line + 10, w, 60), "<color=#ff9a7a>Partie arrêtée : " + (client.Error ?? "départ.") + "</color>");
            }
            else if (client.Lobby != null)
            {
                SeatInfo mine = null;
                foreach (SeatInfo seat in client.Lobby.Seats)
                {
                    bool me = seat.Id == client.SeatId;
                    if (me) mine = seat;
                    string who = seat.Player == null ? "<color=#8a8a8a>place libre</color>" : seat.Player + (me ? " (toi)" : "");
                    string state = seat.Player == null ? "" : !seat.Connected ? "  <color=#ff9a7a>déconnecté</color>"
                        : seat.Ready ? "  <color=#9be59b>prêt</color>" : "  <color=#d8d8d8>en attente</color>";
                    Hud.Text(new Rect(x, line + 10, w, 30), $"{seat.Id} : {who}{(seat.Build != null ? " — " + seat.Build : "")}{state}");
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
                if (client.LastRejected != null) Hud.Small(new Rect(x, line, w, 48), "<color=#ffcf7a>Refusé : " + client.LastRejected + "</color>");
                line += 30;
            }
            if (!string.IsNullOrEmpty(lastCombat)) Hud.Small(new Rect(x, y + 470, w, 24), lastCombat);
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
            float x = Hud.Width / 2f - 300f;
            Hud.Fill(new Rect(x, 62, 600, 60), new Color(0f, 0f, 0f, 0.4f));
            Hud.Small(new Rect(x + 10, 64, 580, 24), $"Réseau : temps aller-retour {ping}  ·  place {client.SeatId}");
            Hud.Small(new Rect(x + 10, 90, 580, 24), $"Allié visé par les soutiens : {allyId ?? "aucun"} (G : changer)");
            if (client.Status == ClientStatus.Disconnected || client.Status == ClientStatus.Reconnecting)
            {
                double left = client.Options.ReconnectSeconds - (client.DisconnectedSeconds ?? 0);
                string text = client.Status == ClientStatus.Reconnecting ? "Retour dans la partie…"
                    : client.CanReconnect ? $"Connexion perdue — nouvelle tentative (encore {System.Math.Max(0, left):0} s)"
                    : "Connexion perdue — retour impossible.";
                Hud.Fill(new Rect(Hud.Width / 2f - 360, 150, 720, 50), new Color(0.35f, 0.05f, 0.05f, 0.85f));
                Hud.Centered(new Rect(Hud.Width / 2f - 360, 150, 720, 50), text, Color.white);
            }
        }
    }
}
