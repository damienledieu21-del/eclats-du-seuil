using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;

namespace EdS.Runtime
{
    /// <summary>Compagnons : ce que l'interface sait d'un compagnon du joueur (vue du moteur au début du tick).</summary>
    public sealed class CompanionState
    {
        public string Id { get; }
        public CombatantView View { get; }

        /// <summary>Ordre en cours (suivre par défaut).</summary>
        public CompanionOrder Order => View.CompanionOrder ?? CompanionOrder.Follow;

        /// <summary>Jauge de lien, de 0 à 1 (0 à 100 dans le moteur).</summary>
        public double Bond => View.CompanionBond / (double)CompanionSetup.BondMax;

        /// <summary>Une attaque à deux serait admise maintenant (hors portée de la cible, jugée par le moteur).</summary>
        public bool DuoReady => View.DuoAvailable;

        /// <summary>Hors combat (tombé) : plus d'ordre ni d'attaque à deux.</summary>
        public bool Down => View.IsDead;

        public CompanionState(CombatantView view)
        {
            View = view ?? throw new ArgumentNullException(nameof(view));
            Id = view.Id;
        }
    }

    /// <summary>
    /// Compagnons : un client de combat qui sait commander les compagnons du joueur (le pont temps réel local ; pas encore le client réseau, dont
    /// le protocole n'a pas de commande de compagnon). Les demandes partent au prochain tick ; le moteur reste seul juge (un refus donne un
    /// retour <see cref="FeedbackKind.Rejected"/>).
    /// </summary>
    public interface ICompanionCommands
    {
        /// <summary>Compagnons du joueur, dans l'ordre des identifiants.</summary>
        IReadOnlyList<CompanionState> Companions();

        /// <summary>Ordre simple à un compagnon ; « cibler » vise la cible sélectionnée. Faux si la demande ne peut pas partir (sans cible, compagnon inconnu ou tombé).</summary>
        bool Order(string companion, CompanionOrder order);

        /// <summary>Attaque à deux avec un compagnon sur la cible sélectionnée ; faux sans cible ou jauge pas pleine (aucune commande).</summary>
        bool Duo(string companion);
    }

    /// <summary>Action de l'interface des compagnons : un des cinq ordres, ou l'attaque à deux.</summary>
    public enum CompanionAction
    {
        Follow = 0, Wait = 1, Target = 2, Protect = 3, Return = 4, Duo = 5,
    }

    /// <summary>
    /// Compagnons : correspondance entre l'interface et les commandes (décision de Damien : ordres simples ; dossier p. 35 : cinq ordres au plus
    /// dans le menu rapide, chacun avec une icône et un mot). Au clavier : <b>O</b> ouvre ou ferme le menu des ordres, puis <b>1 à 5</b> choisit
    /// (les compétences ne partent pas tant qu'il est ouvert) ; <b>Y</b> lance l'attaque à deux quand la jauge est pleine. Au doigt : le panneau
    /// du compagnon ouvre le même menu, chaque case donne son ordre, la case « À deux » l'attaque à deux. Avec deux compagnons, l'ordre va au
    /// compagnon choisi (Maj + O change de compagnon). Pur et sans état d'affichage : testé hors Unity.
    /// </summary>
    public sealed class CompanionControls
    {
        public const string MenuKey = "O", DuoKey = "Y", NextKey = "Maj + O";

        /// <summary>Les mots courts des cases (un mot avec l'icône, jamais la couleur seule).</summary>
        public static IReadOnlyList<string> Labels { get; } = new[] { "Suivre", "Attendre", "Cibler", "Protéger", "Revenir", "À deux" };

        public bool MenuOpen { get; private set; }

        /// <summary>Indice du compagnon commandé parmi ceux du joueur.</summary>
        public int Selected { get; private set; }

        public void ToggleMenu() => MenuOpen = !MenuOpen;

        public void CloseMenu() => MenuOpen = false;

        /// <summary>Passe au compagnon suivant (avec deux compagnons) ; rien avec un seul.</summary>
        public void NextCompanion(int count)
        {
            if (count <= 0) Selected = 0;
            else Selected = (Selected + 1) % count;
        }

        /// <summary>Action d'une touche chiffre du menu (1 à 5) ; null si le menu est fermé ou la touche hors du menu.</summary>
        public CompanionAction? DigitAction(int digit) => MenuOpen && digit >= 1 && digit <= 5 ? (CompanionAction)(digit - 1) : (CompanionAction?)null;

        /// <summary>
        /// Exécute une action sur le compagnon choisi : un ordre ferme le menu ; l'attaque à deux part seulement si la vue la dit admissible.
        /// Faux si rien n'est parti (aucun compagnon, compagnon tombé, sans cible, jauge pas pleine).
        /// </summary>
        public bool Perform(ICompanionCommands? client, CompanionAction action)
        {
            if (client is null) return false;
            IReadOnlyList<CompanionState> companions = client.Companions();
            if (companions.Count == 0) return false;
            CompanionState c = companions[Math.Min(Selected, companions.Count - 1)];
            if (c.Down) return false;
            bool sent = action == CompanionAction.Duo ? c.DuoReady && client.Duo(c.Id) : client.Order(c.Id, (CompanionOrder)(int)action);
            if (sent && action != CompanionAction.Duo) MenuOpen = false;
            return sent;
        }
    }
}
