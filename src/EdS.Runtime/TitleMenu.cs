using System;
using System.Collections.Generic;

namespace EdS.Runtime
{
    /// <summary>Sous-lot V2.55 : ce qu'une rangée du menu de l'écran titre déclenche.</summary>
    public enum TitleAction
    {
        None = 0,
        Continue,
        NewGame,
        Settings,
        Quit,

        /// <summary>Confirmation : effacer la sauvegarde et recommencer.</summary>
        ConfirmNew,

        /// <summary>Retour à la page précédente.</summary>
        Back,

        /// <summary>Réglage : qualité des textures (change au suivant).</summary>
        Textures,

        /// <summary>Réglage : luminosité (plus ou moins, selon <see cref="TitleMenu.Direction"/>).</summary>
        Brightness,
    }

    /// <summary>Page du menu de l'écran titre.</summary>
    public enum TitlePage
    {
        Main,
        ConfirmNew,
        Settings,
    }

    /// <summary>
    /// Sous-lot V2.55 (décision de Damien du 17 novembre 2026) : l'état du menu de l'écran titre, sans Unity : Continuer (seulement s'il y a une
    /// sauvegarde), Nouvelle partie (avec une confirmation s'il faut effacer une sauvegarde), Réglages, Quitter (pas sur téléphone). Clavier, souris
    /// et doigt n'ont qu'à appeler <see cref="Move"/>, <see cref="Hover"/>, <see cref="Activate"/>, <see cref="Adjust"/> et <see cref="Back"/> : les règles
    /// sont ici. Affichage seulement : rien ici ne touche à la partie.
    /// </summary>
    public sealed class TitleMenu
    {
        private readonly bool _hasSave;
        private readonly bool _canQuit;
        private readonly List<TitleAction> _items = new List<TitleAction>();

        public TitleMenu(bool hasSave, bool canQuit)
        {
            _hasSave = hasSave;
            _canQuit = canQuit;
            Show(TitlePage.Main);
        }

        public TitlePage Page { get; private set; }

        /// <summary>Rangée choisie (0 à <see cref="Count"/> - 1).</summary>
        public int Selected { get; private set; }

        /// <summary>Sens du dernier réglage (-1 ou 1), pour <see cref="TitleAction.Brightness"/>.</summary>
        public int Direction { get; private set; }

        public int Count => _items.Count;

        public TitleAction ItemAt(int index) => _items[index];

        /// <summary>Titre de la page (vide sur la page principale).</summary>
        public string Heading => Page == TitlePage.Settings ? "Réglages" : Page == TitlePage.ConfirmNew ? "Effacer la sauvegarde ?" : "";

        private void Show(TitlePage page)
        {
            Page = page;
            _items.Clear();
            switch (page)
            {
                case TitlePage.Main:
                    if (_hasSave) _items.Add(TitleAction.Continue);
                    _items.Add(TitleAction.NewGame);
                    _items.Add(TitleAction.Settings);
                    if (_canQuit) _items.Add(TitleAction.Quit);
                    Selected = 0;
                    break;
                case TitlePage.ConfirmNew:
                    _items.Add(TitleAction.Back);   // par défaut : ne rien effacer
                    _items.Add(TitleAction.ConfirmNew);
                    Selected = 0;
                    break;
                default:
                    _items.Add(TitleAction.Textures);
                    _items.Add(TitleAction.Brightness);
                    _items.Add(TitleAction.Back);
                    Selected = 0;
                    break;
            }
        }

        /// <summary>Descend (1) ou monte (-1) d'une rangée, en bouclant.</summary>
        public void Move(int step)
        {
            if (_items.Count == 0) return;
            Selected = (((Selected + step) % _items.Count) + _items.Count) % _items.Count;
        }

        /// <summary>La souris survole la rangée <paramref name="index"/> (hors liste : sans effet).</summary>
        public void Hover(int index)
        {
            if (index >= 0 && index < _items.Count) Selected = index;
        }

        /// <summary>Rangée sous la position verticale <paramref name="y"/> (-1 si aucune), les rangées étant définies par <paramref name="rowTop"/> et <paramref name="rowHeight"/>.</summary>
        public int RowAt(float y, float rowTop, float rowHeight)
        {
            if (y < rowTop || rowHeight <= 0f) return -1;
            int row = (int)((y - rowTop) / rowHeight);
            return row < _items.Count ? row : -1;
        }

        /// <summary>
        /// Valide la rangée choisie. Rend l'action que l'hôte doit faire (Continuer, Nouvelle partie, Quitter, ou un réglage) ; <see cref="TitleAction.None"/> quand
        /// le menu a seulement changé de page (Réglages, confirmation, retour). Nouvelle partie demande d'abord confirmation s'il y a une sauvegarde.
        /// </summary>
        public TitleAction Activate()
        {
            if (_items.Count == 0) return TitleAction.None;
            Direction = 1;
            TitleAction action = _items[Selected];
            switch (action)
            {
                case TitleAction.NewGame:
                    if (_hasSave)
                    {
                        Show(TitlePage.ConfirmNew);
                        return TitleAction.None;
                    }
                    return TitleAction.NewGame;
                case TitleAction.Settings:
                    Show(TitlePage.Settings);
                    return TitleAction.None;
                case TitleAction.Back:
                    GoMain();
                    return TitleAction.None;
                case TitleAction.ConfirmNew:
                    return TitleAction.NewGame;   // l'hôte efface la sauvegarde (copie gardée) avant de recommencer
                default:
                    return action;
            }
        }

        /// <summary>Flèche gauche (-1) ou droite (1) sur un réglage ; sans effet ailleurs. Rend l'action du réglage.</summary>
        public TitleAction Adjust(int direction)
        {
            if (Page != TitlePage.Settings || direction == 0) return TitleAction.None;
            TitleAction action = _items[Selected];
            if (action != TitleAction.Brightness && action != TitleAction.Textures) return TitleAction.None;
            Direction = direction < 0 ? -1 : 1;
            return action;
        }

        /// <summary>Échap ou retour : quitte la page de réglages ou de confirmation (la page principale ne se quitte pas).</summary>
        public bool Back()
        {
            if (Page == TitlePage.Main) return false;
            GoMain();
            return true;
        }

        // Retour à la page principale, sur la rangée d'où l'on venait.
        private void GoMain()
        {
            TitleAction from = Page == TitlePage.Settings ? TitleAction.Settings : TitleAction.NewGame;
            Show(TitlePage.Main);
            Selected = Math.Max(0, _items.IndexOf(from));
        }

        /// <summary>Libellé fixe d'une action (les réglages ajoutent leur valeur côté hôte).</summary>
        public static string Label(TitleAction action)
        {
            switch (action)
            {
                case TitleAction.Continue: return "Continuer";
                case TitleAction.NewGame: return "Nouvelle partie";
                case TitleAction.Settings: return "Réglages";
                case TitleAction.Quit: return "Quitter";
                case TitleAction.ConfirmNew: return "Oui, effacer et recommencer";
                case TitleAction.Back: return "Retour";
                case TitleAction.Textures: return "Textures";
                case TitleAction.Brightness: return "Luminosité";
                default: return "";
            }
        }
    }
}
