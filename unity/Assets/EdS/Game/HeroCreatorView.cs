using System;
using System.Collections.Generic;
using EdS.Data.Saves;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V2.16 (première version du créateur de personnage, souhait de Damien du 9 novembre 2026) : la fenêtre où le joueur choisit
    /// l'aspect de son héros : corps (homme, femme, personnages faits dans MakeHuman), coiffure, barbe, peau, couleur des cheveux et de la
    /// tenue. Les règles (choix possibles, tour des choix, corps absent) sont dans <see cref="HeroLooks"/> ; ici, seulement l'affichage. Le
    /// choix est appliqué et sauvegardé au « Valider » ; « Annuler » (ou Échap) ne change rien. Pas encore d'aperçu en direct : le héros
    /// change à l'écran quand on valide.
    /// </summary>
    public sealed class HeroCreatorView
    {
        private static readonly HeroPart[] Parts =
            { HeroPart.Body, HeroPart.Hair, HeroPart.Beard, HeroPart.Skin, HeroPart.HairTone, HeroPart.Outfit };

        private readonly RectTransform _panel;
        private readonly UiText[] _values = new UiText[6];
        private readonly UiButton[] _previous = new UiButton[6];
        private readonly UiButton[] _next = new UiButton[6];
        private readonly UiText _note;
        private SavedLook _draft;
        private IReadOnlyList<MadeHumanModel> _made;
        private Action<SavedLook> _apply;

        /// <summary>La fenêtre est-elle ouverte ?</summary>
        public bool Open { get; private set; }

        public HeroCreatorView(RectTransform parent)
        {
            _panel = UiKit.Place(UiKit.Node(parent, "Créateur de personnage"), new Vector2(450f, 500f), new Vector2(640f, 480f), new Vector2(0.5f, 0.5f));
            UiKit.Fill(UiKit.Image(_panel, "Dalle", UiKit.WindowSprite(), Color.white, UnityEngine.UI.Image.Type.Sliced).rectTransform);
            UiText title = UiText.Create(_panel, "Titre", UiFace.Title, 26f, Color.white, TextAnchor.MiddleCenter, 4f);
            UiKit.Place(title.Rect, new Vector2(320f, 38f), new Vector2(560f, 36f), new Vector2(0.5f, 0.5f));
            title.Gold();
            title.Value = "Ton personnage";
            for (int i = 0; i < Parts.Length; i++)
            {
                int row = i;
                float y = 100f + i * 52f;
                UiText label = UiText.Create(_panel, "Nom", UiFace.Bold, 18f, UiKit.Muted, TextAnchor.MiddleLeft, 6f, true);
                UiKit.Place(label.Rect, new Vector2(40f, y), new Vector2(150f, 40f), new Vector2(0f, 0.5f));
                label.Value = HeroLooks.PartName(Parts[i]);
                _previous[i] = UiKit.Button(_panel, "Précédent", "bouton_sobre", "<", UiFace.Title, 24f, UiKit.Ink, () => Step(row, -1));
                UiKit.Place(_previous[i].Rect, new Vector2(240f, y), new Vector2(52f, 44f), new Vector2(0.5f, 0.5f));
                _values[i] = UiText.Create(_panel, "Valeur", UiFace.Bold, 18f, UiKit.Dawn, TextAnchor.MiddleCenter, 4f);
                UiKit.Place(_values[i].Rect, new Vector2(400f, y), new Vector2(240f, 40f), new Vector2(0.5f, 0.5f));
                _next[i] = UiKit.Button(_panel, "Suivant", "bouton_sobre", ">", UiFace.Title, 24f, UiKit.Ink, () => Step(row, 1));
                UiKit.Place(_next[i].Rect, new Vector2(560f, y), new Vector2(52f, 44f), new Vector2(0.5f, 0.5f));
            }
            _note = UiText.Create(_panel, "Note", UiFace.Body, 15f, UiKit.Muted, TextAnchor.MiddleCenter);
            UiKit.Place(_note.Rect, new Vector2(320f, 408f), new Vector2(580f, 24f), new Vector2(0.5f, 0.5f));
            UiButton ok = UiKit.Button(_panel, "Valider", "bouton", "Valider", UiFace.Title, 22f, new Color(0.1f, 0.05f, 0.02f), Confirm);
            UiKit.Place(ok.Rect, new Vector2(200f, 442f), new Vector2(220f, 44f), new Vector2(0.5f, 0.5f));
            UiButton cancel = UiKit.Button(_panel, "Annuler", "bouton_sobre", "Annuler", UiFace.Bold, 20f, UiKit.Ink, Close);
            UiKit.Place(cancel.Rect, new Vector2(440f, 442f), new Vector2(220f, 44f), new Vector2(0.5f, 0.5f));
            UiKit.Show(_panel, false);
        }

        /// <summary>
        /// Ouvre la fenêtre sur l'aspect <paramref name="current"/> (null : l'aspect par défaut) ; <paramref name="apply"/> reçoit l'aspect
        /// validé. <paramref name="made"/> : les personnages de MakeHuman prêts (vide sans eux : homme et femme seulement).
        /// </summary>
        public void Show(SavedLook current, IReadOnlyList<MadeHumanModel> made, Action<SavedLook> apply)
        {
            _made = made ?? new List<MadeHumanModel>();
            _draft = HeroLooks.Normalize(current, _made);
            _apply = apply;
            Open = true;
            UiKit.Show(_panel, true);
            Refresh();
        }

        /// <summary>Ferme la fenêtre sans rien changer.</summary>
        public void Close()
        {
            Open = false;
            UiKit.Show(_panel, false);
        }

        private void Confirm()
        {
            SavedLook chosen = _draft;
            Close();
            _apply?.Invoke(chosen);
        }

        private void Step(int row, int step)
        {
            _draft = HeroLooks.Cycle(_draft, Parts[row], step, _made);
            Refresh();
        }

        private void Refresh()
        {
            for (int i = 0; i < Parts.Length; i++)
            {
                bool applies = HeroLooks.Applies(Parts[i], _draft.Body);
                _values[i].Value = HeroLooks.ValueText(_draft, Parts[i], _made);
                _previous[i].Enable(applies);
                _next[i].Enable(applies);
            }
            _note.Value = HeroLooks.IsMade(_draft.Body) ? "Un personnage fait dans MakeHuman a sa peau, ses cheveux et ses vêtements."
                : _made.Count == 0 ? "Les personnages de MakeHuman apparaissent ici une fois importés (docs/CREER_PERSONNAGES.md)." : "";
        }
    }
}
