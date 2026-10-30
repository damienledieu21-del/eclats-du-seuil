using System.Collections.Generic;
using EdS.Runtime;
using EdS.World;
using UnityEngine;
using UnityEngine.UI;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V1.1 : la carte (L) propose le voyage rapide vers un point sûr découvert. Une colonne à gauche de la carte peinte : un bouton
    /// par point sûr découvert (grisé là où l'on est, ou en plein combat, avec la raison), dans l'ordre des données. Le voyage passe par
    /// <see cref="AdventureSession.Travel"/> (sauvegarde, puis nouvelle session au point d'arrivée) ; réussi, le panneau se ferme.
    /// </summary>
    public sealed partial class AdventureBook
    {
        private const int TravelButtons = 8;

        private readonly List<UiButton> _travel = new List<UiButton>();
        private IReadOnlyList<TravelDestination> _destinations = new TravelDestination[0];
        private RectTransform _travelPanel;
        private UiText _travelTitle, _travelHint, _travelState;

        private void BuildTravel(RectTransform page)
        {
            _travelPanel = UiKit.Node(page, "Voyage rapide");
            UiKit.Place(_travelPanel, new Vector2(50f, 166f), new Vector2(236f, 620f), Vector2.zero);
            UiKit.Fill(UiKit.Image(_travelPanel, "Dalle", UiKit.Sprite("dalle", 40f), new Color(1f, 1f, 1f, 0.92f), Image.Type.Sliced).rectTransform);
            _travelTitle = Text(_travelPanel, UiFace.Title, 20f, Color.white, TextAnchor.MiddleLeft, 18f, 30f, 210f, 26f, 0f, 0.5f, 2f);
            _travelTitle.Gold();
            _travelHint = Text(_travelPanel, UiFace.Body, 14f, UiKit.Muted, TextAnchor.UpperLeft, 18f, 50f, 204f, 40f, 0f, 0f);
            _travelHint.Wrap();
            for (int i = 0; i < TravelButtons; i++)
            {
                int index = i;
                UiButton go = UiKit.Button(_travelPanel, "Voyager", "bouton_sobre", "", UiFace.Bold, 14f, UiKit.Ink, () => Travel(index));
                UiKit.Place(go.Rect, new Vector2(118f, 104f + i * 50f), new Vector2(204f, 42f), new Vector2(0.5f, 0f));
                _travel.Add(go);
            }
            _travelState = Text(_travelPanel, UiFace.Body, 14f, UiKit.Endurance, TextAnchor.UpperLeft, 18f, 510f, 204f, 90f, 0f, 0f);
            _travelState.Wrap();
        }

        private void Travel(int index)
        {
            if (_session == null || index >= _destinations.Count) return;
            TravelDestination d = _destinations[index];
            if (_session.Travel(d.Zone, d.SafePoint) == TravelRefusal.None) _request = Request.Close;
        }

        /// <summary>Une image de la colonne du voyage rapide : points sûrs découverts, et la raison si le voyage n'est pas possible.</summary>
        private void RefreshTravel(AdventureSession a)
        {
            _destinations = a.TravelDestinations();
            _travelTitle.Value = a.Text("text.ui.travel.title");
            _travelHint.Value = a.Text("text.ui.travel.hint");
            TravelRefusal blocked = TravelRefusal.None;
            for (int i = 0; i < _travel.Count; i++)
            {
                bool used = i < _destinations.Count;
                UiKit.Show(_travel[i].Rect, used);
                if (!used) continue;
                TravelDestination d = _destinations[i];
                TravelRefusal refusal = a.CanTravel(d.Zone, d.SafePoint);
                if (refusal != TravelRefusal.None && blocked == TravelRefusal.None) blocked = refusal;
                _travel[i].Text.Value = d.Here ? d.Name + " · " + a.Text("text.ui.travel.here") : d.Name;
                _travel[i].Enable(refusal == TravelRefusal.None && !d.Here);
            }
            _travelState.Value = blocked == TravelRefusal.None ? "" : a.Text("text.ui.travel." + blocked.ToString().ToLowerInvariant());
        }
    }
}
