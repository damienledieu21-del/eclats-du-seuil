using System.Collections.Generic;
using EdS.Data.Builds;
using EdS.Domain.Builds;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot 9.4 : écran des talents (IMGUI, provisoire). Trois colonnes, une par arbre ; chaque talent montre son rang, ses
    /// prérequis et des boutons « − » et « + ». Toute modification passe par <see cref="TalentEditor"/> (donc par le validateur du cœur) :
    /// un point refusé ne change rien et sa raison s'affiche. On peut partir d'un build de référence ou de zéro, puis jouer le build composé.
    /// </summary>
    public sealed class TalentScreen
    {
        public enum Action { None, Play, Close }

        private const float PanelWidth = 1560f;
        private const float ColumnWidth = 500f;
        private const float RowHeight = 66f;

        private readonly TalentEditor _editor;
        private readonly IReadOnlyList<ReferenceBuild> _references;
        private string _message = "Clique sur + ou − pour placer ou retirer un point.";
        private bool _error;

        // Raisons de refus par talent, recalculées seulement quand le build change (OnGUI dessine plusieurs fois par image).
        private CharacterBuild _cachedFor;
        private readonly Dictionary<string, BuildViolation> _whyNotAdd = new Dictionary<string, BuildViolation>();
        private readonly Dictionary<string, BuildViolation> _whyNotRemove = new Dictionary<string, BuildViolation>();

        /// <param name="referenceId">Build de référence dont part l'éditeur (null : inconnu).</param>
        public TalentScreen(TalentEditor editor, IReadOnlyList<ReferenceBuild> references, string referenceId = null)
        {
            _editor = editor;
            _references = references;
            ReferenceId = referenceId;
        }

        public TalentEditor Editor => _editor;

        /// <summary>
        /// Sous-lot 10.4 : build de référence dont part le build composé (le dernier chargé par « Partir de »). En réseau, le serveur
        /// reçoit cette référence et les rangs de talents (attributs et compétences sont ceux de la référence).
        /// </summary>
        public string ReferenceId { get; private set; }

        /// <summary>Message affiché en bas de l'écran (par exemple un refus au moment de jouer le build).</summary>
        public void Say(string message, bool error)
        {
            _message = message;
            _error = error;
        }

        private void Result(BuildViolation why, string done)
        {
            if (why == null) Say(done, false);
            else Say("Refusé : " + why.Message, true);
        }

        private void Refresh()
        {
            if (ReferenceEquals(_cachedFor, _editor.Build)) return;
            _cachedFor = _editor.Build;
            _whyNotAdd.Clear();
            _whyNotRemove.Clear();
            foreach (TalentTreeDefinition tree in _editor.Trees)
                foreach (TalentDefinition talent in tree.Talents)
                {
                    _whyNotAdd[talent.Id] = _editor.WhyNotAdd(talent.Id);
                    _whyNotRemove[talent.Id] = _editor.WhyNotRemove(talent.Id);
                }
        }

        /// <summary>Dessine l'écran ; rend l'action demandée par un bouton du bas.</summary>
        public Action Draw()
        {
            Refresh();
            float x = Mathf.Max(10f, (Hud.Width - PanelWidth) / 2f), y = 70f;
            Hud.Fill(new Rect(x, y, PanelWidth, 900f), new Color(0.05f, 0.05f, 0.08f, 0.92f));
            Hud.Title(new Rect(x + 20, y + 12, 900, 36), $"Talents — niveau {_editor.Level}");
            Hud.Text(new Rect(x + 20, y + 52, 1500, 28),
                $"Points : {_editor.PointsSpent} / {_editor.PointsAvailable} dépensés  ·  <b>{_editor.PointsRemaining} restant(s)</b>  ·  " +
                $"talent majeur (★) : {_editor.MajorTalentThreshold} points dans son arbre, un seul par build");

            // Point de départ : un build de référence, ou aucun talent.
            Hud.Small(new Rect(x + 20, y + 92, 140, 30), "Partir de :");
            float bx = x + 130;
            foreach (ReferenceBuild reference in _references)
            {
                if (Hud.Button(new Rect(bx, y + 88, 210, 34), reference.Name, true))
                {
                    _editor.Load(reference.Build);
                    ReferenceId = reference.Id;
                    Say($"Build de référence « {reference.Name} » chargé.", false);
                }
                bx += 218;
            }
            if (Hud.Button(new Rect(bx, y + 88, 150, 34), "Tout retirer", true))
                Result(_editor.Clear(), "Tous les points sont retirés.");

            for (int i = 0; i < _editor.Trees.Count; i++)
                DrawTree(_editor.Trees[i], x + 20 + i * (ColumnWidth + 10), y + 140);

            Color color = _error ? new Color(1f, 0.55f, 0.45f) : new Color(0.75f, 0.95f, 0.75f);
            Hud.Centered(new Rect(x + 20, y + 760, PanelWidth - 40, 40), _message, color);
            Action action = Action.None;
            if (Hud.Button(new Rect(x + PanelWidth / 2 - 330, y + 820, 320, 50), "Jouer ce build", true)) action = Action.Play;
            if (Hud.Button(new Rect(x + PanelWidth / 2 + 10, y + 820, 320, 50), "Fermer (T ou Échap)", true)) action = Action.Close;
            return action;
        }

        private void DrawTree(TalentTreeDefinition tree, float x, float y)
        {
            Hud.Fill(new Rect(x, y, ColumnWidth, 600), new Color(1f, 1f, 1f, 0.05f));
            Hud.Title(new Rect(x + 12, y + 8, ColumnWidth - 24, 36), $"{tree.Name} — {_editor.PointsInTree(tree)} pt");
            float row = y + 56;
            foreach (TalentDefinition talent in tree.Talents)
            {
                int rank = _editor.Rank(talent.Id);
                Hud.Fill(new Rect(x + 6, row, ColumnWidth - 12, RowHeight - 6), rank > 0 ? new Color(0.25f, 0.4f, 0.6f, 0.45f) : new Color(0f, 0f, 0f, 0.3f));
                string name = (talent.IsMajor ? "★ " : "") + talent.Name;
                Hud.Text(new Rect(x + 16, row + 4, 330, 28), rank > 0 ? $"<b>{name}</b>" : name);
                Hud.Small(new Rect(x + 16, row + 32, 330, 24), Detail(talent));
                Hud.Centered(new Rect(x + 330, row + 14, 60, 30), $"{rank} / {talent.MaxRank}", Color.white);

                if (Hud.Button(new Rect(x + 394, row + 12, 44, 36), "−", _whyNotRemove[talent.Id] == null))
                    Result(_editor.Remove(talent.Id), $"« {talent.Name} » : un point retiré.");
                if (Hud.Button(new Rect(x + 444, row + 12, 44, 36), "+", _whyNotAdd[talent.Id] == null))
                    Result(_editor.Add(talent.Id), $"« {talent.Name} » : un point ajouté.");
                row += RowHeight;
            }
        }

        /// <summary>Prérequis et condition du majeur, en une ligne.</summary>
        private string Detail(TalentDefinition talent)
        {
            var parts = new List<string>();
            foreach (TalentRequirement requirement in talent.Prerequisites)
            {
                string required = _editor.Class.TryGetTalent(requirement.TalentId, out TalentDefinition other, out _) ? other.Name : requirement.TalentId;
                bool met = _editor.Rank(requirement.TalentId) >= requirement.MinRank;
                parts.Add($"<color={(met ? "#9be59b" : "#c8c8c8")}>exige {required} {requirement.MinRank}</color>");
            }
            if (talent.IsMajor) parts.Add($"majeur : {_editor.MajorTalentThreshold} pts dans l'arbre");
            return parts.Count == 0 ? "" : string.Join("  ·  ", parts.ToArray());
        }
    }
}
