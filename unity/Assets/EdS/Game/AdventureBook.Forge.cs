using System.Collections.Generic;
using EdS.Runtime;
using EdS.World;
using UnityEngine;
using UnityEngine.UI;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V1.4 : la forge de Maëlle dans l'atelier. Une recette à qualité (armes, boucliers, pièces de métal) ouvre le <b>petit jeu
    /// de forge</b> : une barre, un curseur qui va et vient, une cible au milieu ; trois frappes (Espace, clic sur la barre ou bouton
    /// Frapper) au bon moment font monter la chance d'une belle pièce. Les notes partent au cœur (<see cref="AdventureSession.Forge"/>), qui
    /// seul tire le résultat. Le bouton <b>Réparer</b> (à la forge ici, chez l'armurier dans sa boutique) répare tout l'équipement entamé
    /// contre son prix. La logique du petit jeu est dans <see cref="ForgeGame"/> (sans Unity, testée).
    /// </summary>
    public sealed partial class AdventureBook
    {
        private const float ForgeBarWidth = 900f;

        private RectTransform _forge, _forgeCursor;
        private UiText _forgeTitle, _forgeHint, _forgeChances, _forgeNotes;
        private UiButton _forgeStrike, _forgeCancel, _craftRepair, _shopRepair;
        private ForgeGame _forgeGame;
        private string _forgeRecipe;

        /// <summary>Le petit jeu, par-dessus les recettes de l'atelier (caché tant qu'aucune recette à qualité n'est lancée).</summary>
        private void BuildForge(RectTransform page)
        {
            _craftRepair = UiKit.Button(page, "Réparer", "bouton_sobre", "", UiFace.Bold, 15f, UiKit.Ink, RepairHere);
            UiKit.Place(_craftRepair.Rect, new Vector2(1440f, 836f), new Vector2(300f, 40f), new Vector2(1f, 0.5f));

            _forge = UiKit.Node(page, "Petit jeu de forge");
            UiKit.Place(_forge, new Vector2(200f, 260f), new Vector2(1100f, 360f), Vector2.zero);
            UiKit.Fill(UiKit.Image(_forge, "Dalle", UiKit.Sprite("dalle", 40f), Color.white, Image.Type.Sliced).rectTransform);
            _forgeTitle = Text(_forge, UiFace.Title, 30f, Color.white, TextAnchor.MiddleLeft, 60f, 50f, 980f, 40f, 0f, 0.5f, 2f);
            _forgeTitle.Gold();
            _forgeHint = Text(_forge, UiFace.Body, 18f, UiKit.Ink, TextAnchor.MiddleLeft, 60f, 92f, 980f, 26f, 0f, 0.5f);

            // La barre, sa cible parfaite au milieu, et le curseur. Un clic sur la barre frappe aussi (toucher compris).
            Image bar = UiKit.Image(_forge, "Barre", UiKit.Sprite("cible_remplissage"), new Color(0.06f, 0.08f, 0.11f, 1f));
            UiKit.Place(bar.rectTransform, new Vector2(100f, 150f), new Vector2(ForgeBarWidth, 46f), Vector2.zero);
            UiKit.Pointer(bar).Left = Strike;
            float target = (float)(ForgeGame.PerfectHalfWidth * 2.0) * ForgeBarWidth;
            Image good = UiKit.Image(_forge, "Bonne zone", null, UiKit.Alpha(UiKit.Dawn, 0.25f));
            UiKit.Place(good.rectTransform, new Vector2(100f + ForgeBarWidth * 0.3f, 150f), new Vector2(ForgeBarWidth * 0.4f, 46f), Vector2.zero);
            Image perfect = UiKit.Image(_forge, "Cible", null, UiKit.Alpha(UiKit.Dawn, 0.85f));
            UiKit.Place(perfect.rectTransform, new Vector2(100f + ForgeBarWidth * 0.5f - target / 2f, 150f), new Vector2(target, 46f), Vector2.zero);
            Image cursor = UiKit.Image(_forge, "Curseur", null, Color.white);
            _forgeCursor = cursor.rectTransform;
            UiKit.Place(_forgeCursor, new Vector2(100f, 140f), new Vector2(8f, 66f), new Vector2(0.5f, 0f));

            _forgeNotes = Text(_forge, UiFace.Bold, 20f, UiKit.Dawn, TextAnchor.MiddleLeft, 100f, 236f, 600f, 28f, 0f, 0.5f, 4f);
            _forgeChances = Text(_forge, UiFace.Body, 17f, UiKit.Muted, TextAnchor.MiddleLeft, 100f, 270f, 900f, 24f, 0f, 0.5f);
            _forgeStrike = UiKit.Button(_forge, "Frapper", "bouton", "", UiFace.Bold, 18f, new Color(0.1f, 0.05f, 0.02f), Strike);
            UiKit.Place(_forgeStrike.Rect, new Vector2(1000f, 320f), new Vector2(200f, 48f), new Vector2(1f, 0.5f));
            _forgeCancel = UiKit.Button(_forge, "Laisser", "bouton_sobre", "", UiFace.Bold, 15f, UiKit.Muted, CloseForge);
            UiKit.Place(_forgeCancel.Rect, new Vector2(780f, 320f), new Vector2(160f, 40f), new Vector2(1f, 0.5f));
            UiKit.Show(_forge, false);
        }

        /// <summary>Bouton Fabriquer d'une recette à qualité : le petit jeu s'ouvre (le cœur juge la recette quand les notes arrivent).</summary>
        private void OpenForge(string recipe)
        {
            if (_session == null) return;
            _forgeGame = _session.NewForgeGame();
            _forgeRecipe = recipe;
        }

        private void CloseForge()
        {
            _forgeGame = null;
            _forgeRecipe = null;
        }

        /// <summary>Une frappe ; à la dernière, les notes vont au cœur et le petit jeu se ferme.</summary>
        private void Strike()
        {
            if (_forgeGame == null || _forgeGame.Done) return;
            _forgeGame.Strike();
            UiSounds.Play(UiCue.Tab);
            if (!_forgeGame.Done) return;
            if (_session != null) _session.Forge(_forgeRecipe, _forgeGame.Notes);
            CloseForge();
        }

        /// <summary>Une image du petit jeu (dans l'atelier ouvert) et du bouton Réparer de la forge.</summary>
        private void RefreshForge(AdventureSession a, float deltaTime)
        {
            bool open = _forgeGame != null && _forgeRecipe != null;
            UiKit.Show(_forge, open);
            RefreshRepairButton(a, _craftRepair, false);
            if (!open) return;
            _forge.SetAsLastSibling();
            _forgeGame.Advance(deltaTime);
            if (Input.GetKeyDown(KeyCode.Space)) Strike();
            if (_forgeGame == null) return;
            UiKit.Place(_forgeCursor, new Vector2(100f + (float)_forgeGame.Position * ForgeBarWidth, 140f), new Vector2(8f, 66f), new Vector2(0.5f, 0f));
            string name = _forgeRecipe;
            foreach (RecipeEntry r in _recipeData)
                if (r.Id == _forgeRecipe) name = r.Name;
            _forgeTitle.Value = string.Format(a.Text("text.ui.forge.title"), name);
            _forgeHint.Value = string.Format(a.Text("text.ui.forge.hint"), _forgeGame.Strikes);
            var notes = new List<string>();
            for (int i = 0; i < _forgeGame.Strikes; i++) notes.Add(i < _forgeGame.Notes.Count ? _forgeGame.Notes[i].ToString() : "·");
            _forgeNotes.Value = string.Join("   ", notes);
            (int fail, int master, int superior) = a.ForgeChances(_forgeRecipe, _forgeGame.Skill);
            _forgeChances.Value = string.Format(a.Text("text.ui.forge.chances"), master, superior, fail);
            _forgeStrike.Text.Value = a.Text("text.ui.forge.strike");
            _forgeCancel.Text.Value = a.Text("text.ui.forge.cancel");
        }

        /// <summary>Bouton Réparer : montré où l'on peut réparer (forge, ou boutique d'un marchand qui répare), avec son prix.</summary>
        private void RefreshRepairButton(AdventureSession a, UiButton button, bool shop)
        {
            bool here = a.CanRepair(out string merchant) && (shop ? merchant != null : merchant == null);
            UiKit.Show(button.Rect, here);
            if (!here) return;
            long price = a.RepairPrice;
            button.Text.Value = a.DamagedPieces == 0 ? a.Text("text.ui.repair_nothing") : string.Format(a.Text("text.ui.repair"), a.MoneyText(price));
            button.Enable(a.DamagedPieces > 0 && a.Money >= price);
        }

        private void RepairHere() => _session?.Repair();

        /// <summary>Le bouton Réparer de la boutique (l'armurier).</summary>
        private void BuildShopRepair(RectTransform page)
        {
            _shopRepair = UiKit.Button(page, "Réparer", "bouton_sobre", "", UiFace.Bold, 15f, UiKit.Ink, RepairHere);
            UiKit.Place(_shopRepair.Rect, new Vector2(1220f, 834f), new Vector2(300f, 44f), new Vector2(1f, 0.5f));
        }
    }
}
