using System.Collections.Generic;
using EdS.Runtime;
using EdS.World;
using UnityEngine;
using UnityEngine.UI;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V1.7 : la Guilde des aventuriers dans le grand panneau de l'aventure, au style « Fragments du Seuil ». Le **tableau** s'ouvre en
    /// parlant à la maîtresse de guilde (F) : à gauche, la carte (rang, points vers le rang suivant, épreuve) avec S'inscrire et Passer
    /// l'épreuve ; à droite, les contrats du jour (Prendre, grisé avec la raison quand ce n'est pas possible) ; en bas, les contrats en cours
    /// (Abandonner) et le bouton Parler (son dialogue). La **carte d'aventurier** est un onglet du journal (J, pas de touche de plus) : rang,
    /// points, contrats réussis, épreuve, titres (Afficher) et contrats en cours. Tout passe par <see cref="AdventureSession"/> : un refus ne
    /// change rien et sa raison s'affiche dans les messages du jeu.
    /// </summary>
    public sealed partial class AdventureBook
    {
        private const int BoardRows = 6, ActiveRows = 3, TitleRows = 10;

        private sealed class ContractRow
        {
            public RectTransform Row;
            public UiText Kind, Name, Objective, Reward, Note;
            public Image Bar;
            public UiButton Button;
            public string Contract;
        }

        // Tableau (chez la maîtresse).
        private UiText _boardRank, _boardPoints, _boardTrial, _boardContracts, _boardActiveTitle, _boardHint;
        private Image _boardBar;
        private UiButton _boardJoin, _boardTrialButton, _boardTalk;
        private readonly List<ContractRow> _boardRows = new List<ContractRow>();
        private readonly List<ContractRow> _boardActive = new List<ContractRow>();
        private Pager _boardPager;
        private IReadOnlyList<ContractEntry> _boardData = new ContractEntry[0];

        // Carte d'aventurier (onglet du journal).
        private RectTransform _card;
        private UiText _cardTab, _cardRank, _cardTitle, _cardPoints, _cardContracts, _cardTrial, _cardTitlesHeader, _cardActiveTitle;
        private Image _cardBar;
        private readonly List<(UiText Name, UiButton Show, string Id)> _cardTitles = new List<(UiText, UiButton, string)>();
        private readonly List<ContractRow> _cardActive = new List<ContractRow>();
        private bool _cardShown;
        private readonly string[] _titleIds = new string[TitleRows];

        // --- Tableau de la guilde -----------------------------------------------------------------------------------------------

        private void BuildGuild(RectTransform page)
        {
            _boardRank = Text(page, UiFace.Title, 44f, Color.white, TextAnchor.MiddleLeft, 60f, 196f, 620f, 56f, 0f, 0.5f, 3f);
            _boardRank.Gold();
            _boardPoints = Text(page, UiFace.Bold, 17f, UiKit.Ink, TextAnchor.MiddleLeft, 60f, 246f, 640f, 24f, 0f, 0.5f);
            Image track = UiKit.Image(page, "Fond", UiKit.Sprite("cible_remplissage"), new Color(0.06f, 0.08f, 0.11f, 1f));
            UiKit.Place(track.rectTransform, new Vector2(60f, 266f), new Vector2(620f, 12f), Vector2.zero);
            _boardBar = UiKit.Image(page, "Points", UiKit.Sprite("cible_remplissage"), UiKit.Dawn, Image.Type.Filled);
            UiKit.Place(_boardBar.rectTransform, new Vector2(60f, 266f), new Vector2(620f, 12f), Vector2.zero);
            _boardBar.fillMethod = Image.FillMethod.Horizontal;
            _boardContracts = Text(page, UiFace.Body, 17f, UiKit.Muted, TextAnchor.MiddleLeft, 60f, 300f, 620f, 22f, 0f, 0.5f);
            _boardTrial = Text(page, UiFace.Body, 18f, UiKit.Ink, TextAnchor.UpperLeft, 60f, 324f, 620f, 56f, 0f, 0f);
            _boardTrial.Wrap();
            _boardJoin = UiKit.Button(page, "S'inscrire", "bouton", "", UiFace.Bold, 16f, new Color(0.1f, 0.05f, 0.02f), () => _session?.JoinGuild());
            UiKit.Place(_boardJoin.Rect, new Vector2(60f, 390f), new Vector2(240f, 44f), new Vector2(0f, 0f));
            _boardTrialButton = UiKit.Button(page, "Épreuve", "bouton", "", UiFace.Bold, 16f, new Color(0.1f, 0.05f, 0.02f), () => _session?.StartTrial());
            UiKit.Place(_boardTrialButton.Rect, new Vector2(320f, 390f), new Vector2(260f, 44f), new Vector2(0f, 0f));

            _boardActiveTitle = Text(page, UiFace.Bold, 15f, UiKit.Muted, TextAnchor.MiddleLeft, 60f, 470f, 620f, 20f, 0f, 0.5f, 14f, true);
            for (int i = 0; i < ActiveRows; i++)
            {
                int index = i;
                ContractRow row = MakeContractRow(page, 60f, 494f + i * 96f, 640f, 88f, () => AbandonRow(_boardActive, index));
                _boardActive.Add(row);
            }

            Text(page, UiFace.Bold, 15f, UiKit.Muted, TextAnchor.MiddleLeft, 740f, 168f, 480f, 20f, 0f, 0.5f, 14f, true).Value = "Contrats du jour";
            _boardPager = new Pager(page, BoardRows, 1300f, 168f, 30f);
            for (int i = 0; i < BoardRows; i++)
            {
                int index = i;
                ContractRow row = MakeContractRow(page, 740f, 192f + i * 100f, 700f, 92f, () => TakeRow(index));
                _boardRows.Add(row);
            }
            _boardHint = Text(page, UiFace.Body, 15f, UiKit.Muted, TextAnchor.UpperLeft, 740f, 796f, 560f, 40f, 0f, 0f);
            _boardHint.Wrap();
            _boardTalk = UiKit.Button(page, "Parler", "bouton_sobre", "", UiFace.Bold, 16f, UiKit.Ink, () => _session?.TalkToGuildmistress());
            UiKit.Place(_boardTalk.Rect, new Vector2(1440f, 834f), new Vector2(200f, 44f), new Vector2(1f, 0.5f));
        }

        /// <summary>Une ligne de contrat : sorte et rang, nom, objectif, récompense, une note (raison, avancement), une barre et un bouton.</summary>
        private ContractRow MakeContractRow(Transform parent, float x, float y, float w, float h, System.Action click)
        {
            var row = new ContractRow { Row = UiKit.Node(parent, "Contrat") };
            UiKit.Place(row.Row, new Vector2(x, y), new Vector2(w, h), Vector2.zero);
            Image back = UiKit.Image(row.Row, "Fond", UiKit.Sprite("bouton_sobre", 12f), new Color(1f, 1f, 1f, 0.5f), Image.Type.Sliced);
            UiKit.Fill(back.rectTransform);
            row.Kind = Text(row.Row, UiFace.Bold, 13f, UiKit.Teal, TextAnchor.MiddleLeft, 14f, 14f, 300f, 18f, 0f, 0.5f, 12f, true);
            row.Name = Text(row.Row, UiFace.Bold, 19f, UiKit.Ink, TextAnchor.MiddleLeft, 14f, 36f, w - 200f, 24f, 0f, 0.5f);
            row.Objective = Text(row.Row, UiFace.Body, 15f, UiKit.Ink, TextAnchor.MiddleLeft, 14f, 58f, w - 200f, 20f, 0f, 0.5f);
            row.Reward = Text(row.Row, UiFace.Bold, 15f, UiKit.Dawn, TextAnchor.MiddleLeft, 14f, 78f, w - 200f, 20f, 0f, 0.5f);
            row.Note = Text(row.Row, UiFace.Body, 14f, UiKit.Muted, TextAnchor.MiddleRight, w - 14f, 66f, 180f, 36f, 1f, 0.5f);
            row.Note.Wrap();
            Image track = UiKit.Image(row.Row, "Fond", UiKit.Sprite("cible_remplissage"), new Color(0.06f, 0.08f, 0.11f, 1f));
            UiKit.Place(track.rectTransform, new Vector2(w - 174f, 74f), new Vector2(160f, 8f), Vector2.zero);
            row.Bar = UiKit.Image(row.Row, "Avancée", UiKit.Sprite("cible_remplissage"), UiKit.Teal, Image.Type.Filled);
            UiKit.Place(row.Bar.rectTransform, new Vector2(w - 174f, 74f), new Vector2(160f, 8f), Vector2.zero);
            row.Bar.fillMethod = Image.FillMethod.Horizontal;
            row.Button = UiKit.Button(row.Row, "Action", "bouton", "", UiFace.Bold, 14f, new Color(0.1f, 0.05f, 0.02f), click);
            UiKit.Place(row.Button.Rect, new Vector2(w - 14f, 24f), new Vector2(160f, 34f), new Vector2(1f, 0.5f));
            return row;
        }

        private void TakeRow(int row)
        {
            int index = _boardPager.First + row;
            if (_session != null && index < _boardData.Count) _session.TakeContract(_boardData[index].Id);
        }

        private void AbandonRow(List<ContractRow> rows, int index)
        {
            if (_session != null && index < rows.Count && rows[index].Contract != null) _session.AbandonContract(rows[index].Contract);
        }

        /// <summary>Une image du tableau de la guilde ouvert.</summary>
        private void RefreshGuild(AdventureSession a)
        {
            if (!a.GuildOpen) return;
            GuildCardView card = a.GuildCard();
            _title.Value = a.Text("text.ui.guild.title");
            _subtitle.Value = a.Text("text.npc.maitresse_guilde.name");
            _boardRank.Value = card.Member ? card.RankName : a.Text("text.ui.guild.card");
            _boardPoints.Value = card.PointsText;
            _boardBar.fillAmount = (float)card.Progress;
            UiKit.Show(_boardBar, card.Member);
            _boardContracts.Value = card.Member ? card.ContractsText + (card.Title != null ? " · " + card.Title : "") : "";
            _boardTrial.Value = card.Member ? card.TrialText : "";
            UiKit.Show(_boardJoin.Rect, !card.Member);
            _boardJoin.Text.Value = a.Text("text.ui.guild.join");
            UiKit.Show(_boardTrialButton.Rect, card.Member);
            _boardTrialButton.Text.Value = a.Text("text.ui.guild.trial_start");
            _boardTrialButton.Enable(card.TrialReady);

            IReadOnlyList<ContractEntry> active = a.ActiveContracts();
            _boardActiveTitle.Value = string.Format(a.Text("text.ui.guild.active"), active.Count, a.Guild.MaxActive);
            FillActive(a, _boardActive, active);

            _boardData = a.GuildBoard();
            _boardPager.Refresh(_boardData.Count);
            for (int i = 0; i < _boardRows.Count; i++)
            {
                ContractRow row = _boardRows[i];
                int index = _boardPager.First + i;
                bool used = index < _boardData.Count;
                UiKit.Show(row.Row, used);
                if (!used) continue;
                ContractEntry c = _boardData[index];
                row.Contract = c.Id;
                Fill(row, c);
                UiKit.Show(row.Bar, false);
                row.Button.Text.Value = a.Text("text.ui.guild.take");
                row.Button.Enable(c.Refusal == GuildRefusal.None);
                row.Note.Value = c.Refusal == GuildRefusal.None ? "" : c.Refusal == GuildRefusal.AlreadyTaken ? a.Text("text.ui.guild.taken")
                    : a.Text("text.ui.guild." + c.Refusal.ToString().ToLowerInvariant());
            }
            _boardHint.Value = a.Text("text.ui.guild.board_hint");
            _boardTalk.Text.Value = a.Text("text.ui.guild.talk");
        }

        private static void Fill(ContractRow row, ContractEntry c)
        {
            row.Kind.Value = c.Kind + " · " + c.Rank;
            row.Name.Value = c.Name;
            row.Objective.Value = c.Objective;
            row.Reward.Value = c.Reward;
        }

        private void FillActive(AdventureSession a, List<ContractRow> rows, IReadOnlyList<ContractEntry> active)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                ContractRow row = rows[i];
                bool used = i < active.Count;
                UiKit.Show(row.Row, used);
                row.Contract = used ? active[i].Id : null;
                if (!used) continue;
                ContractEntry c = active[i];
                Fill(row, c);
                UiKit.Show(row.Bar, true);
                row.Bar.fillAmount = (float)c.Fraction;
                row.Note.Value = c.Progress ?? "";
                row.Button.Text.Value = a.Text("text.ui.guild.abandon");
                row.Button.Enable(true);
            }
        }

        // --- Carte d'aventurier (onglet du journal) -----------------------------------------------------------------------------

        private void BuildCard(RectTransform page)
        {
            _cardTab = Text(page, UiFace.Bold, 16f, UiKit.Muted, TextAnchor.MiddleLeft, 460f, 168f, 300f, 24f, 0f, 0.5f, 14f, true);
            Image hit = UiKit.Image(_cardTab.Rect, "Zone", null, new Color(1f, 1f, 1f, 0f));
            UiKit.Fill(hit.rectTransform, -6f);
            UiKit.Pointer(hit).Left = () => _cardShown = true;

            _card = UiKit.Node(page, "Carte d'aventurier");
            UiKit.Place(_card, Vector2.zero, new Vector2(PanelWidth, PanelHeight), Vector2.zero);
            _cardRank = Text(_card, UiFace.Title, 48f, Color.white, TextAnchor.MiddleLeft, 60f, 222f, 600f, 60f, 0f, 0.5f, 3f);
            _cardRank.Gold();
            _cardTitle = Text(_card, UiFace.Bold, 18f, UiKit.Teal, TextAnchor.MiddleLeft, 60f, 270f, 800f, 24f, 0f, 0.5f, 6f);
            _cardPoints = Text(_card, UiFace.Bold, 17f, UiKit.Ink, TextAnchor.MiddleLeft, 60f, 306f, 800f, 24f, 0f, 0.5f);
            Image track = UiKit.Image(_card, "Fond", UiKit.Sprite("cible_remplissage"), new Color(0.06f, 0.08f, 0.11f, 1f));
            UiKit.Place(track.rectTransform, new Vector2(60f, 326f), new Vector2(800f, 12f), Vector2.zero);
            _cardBar = UiKit.Image(_card, "Points", UiKit.Sprite("cible_remplissage"), UiKit.Dawn, Image.Type.Filled);
            UiKit.Place(_cardBar.rectTransform, new Vector2(60f, 326f), new Vector2(800f, 12f), Vector2.zero);
            _cardBar.fillMethod = Image.FillMethod.Horizontal;
            _cardContracts = Text(_card, UiFace.Body, 17f, UiKit.Muted, TextAnchor.MiddleLeft, 60f, 360f, 800f, 22f, 0f, 0.5f);
            _cardTrial = Text(_card, UiFace.Body, 18f, UiKit.Ink, TextAnchor.UpperLeft, 60f, 384f, 800f, 50f, 0f, 0f);
            _cardTrial.Wrap();
            _cardTitlesHeader = Text(_card, UiFace.Bold, 15f, UiKit.Muted, TextAnchor.MiddleLeft, 60f, 450f, 400f, 20f, 0f, 0.5f, 14f, true);
            for (int i = 0; i < TitleRows; i++)
            {
                int index = i;
                UiText name = Text(_card, UiFace.Body, 18f, UiKit.Ink, TextAnchor.MiddleLeft, 60f, 488f + i * 38f, 520f, 30f, 0f, 0.5f);
                UiButton show = UiKit.Button(_card, "Afficher", "bouton_sobre", "", UiFace.Bold, 14f, UiKit.Muted, () => ToggleTitle(index));
                UiKit.Place(show.Rect, new Vector2(600f, 488f + i * 38f), new Vector2(180f, 32f), new Vector2(0f, 0.5f));
                _cardTitles.Add((name, show, null));
            }
            _cardActiveTitle = Text(_card, UiFace.Bold, 15f, UiKit.Muted, TextAnchor.MiddleLeft, 900f, 168f, 500f, 20f, 0f, 0.5f, 14f, true);
            for (int i = 0; i < ActiveRows; i++)
            {
                int index = i;
                _cardActive.Add(MakeContractRow(_card, 900f, 196f + i * 100f, 540f, 92f, () => AbandonRow(_cardActive, index)));
            }
        }

        private void ToggleTitle(int index)
        {
            if (_session == null || _titleIds[index] == null) return;
            // Un titre déjà affiché se retire ; un autre s'affiche à sa place.
            bool shown = _session.World.Narrative != null && _session.World.Narrative.ShownTitle == _titleIds[index];
            _session.ShowTitle(shown ? null : _titleIds[index]);
        }

        /// <summary>Une image de l'onglet « Carte d'aventurier » du journal (onglets des quêtes cachés).</summary>
        private void RefreshCard(AdventureSession a)
        {
            _cardTab.Value = a.Text("text.ui.journal.card");
            _cardTab.Color = _cardShown ? UiKit.Dawn : UiKit.Muted;
            UiKit.Show(_card, _cardShown);
            if (!_cardShown) return;
            GuildCardView card = a.GuildCard();
            _cardRank.Value = card.Member ? card.RankName : a.Text("text.ui.guild.card");
            _cardTitle.Value = card.Member ? string.Format(a.Text("text.ui.guild.shown_title"), card.Title ?? a.Text("text.ui.guild.no_title")) : "";
            _cardPoints.Value = card.PointsText;
            _cardBar.fillAmount = (float)card.Progress;
            UiKit.Show(_cardBar, card.Member);
            _cardContracts.Value = card.ContractsText;
            _cardTrial.Value = card.Member ? card.TrialText : "";
            _cardTitlesHeader.Value = card.Member ? a.Text("text.ui.guild.titles") + " · " + card.Titles.Count : "";
            for (int i = 0; i < _cardTitles.Count; i++)
            {
                bool used = i < card.Titles.Count;
                UiKit.Show(_cardTitles[i].Name.Rect, used);
                UiKit.Show(_cardTitles[i].Show.Rect, used);
                _titleIds[i] = used ? card.Titles[i].Id : null;
                if (!used) continue;
                TitleEntry t = card.Titles[i];
                _cardTitles[i].Name.Value = t.Name;
                _cardTitles[i].Name.Color = t.Shown ? UiKit.Dawn : UiKit.Ink;
                _cardTitles[i].Show.Text.Value = a.Text(t.Shown ? "text.ui.guild.hide" : "text.ui.guild.show");
            }
            IReadOnlyList<ContractEntry> active = a.ActiveContracts();
            _cardActiveTitle.Value = card.Member
                ? (active.Count == 0 ? a.Text("text.ui.guild.active_none") : string.Format(a.Text("text.ui.guild.active"), active.Count, a.Guild.MaxActive)) : "";
            FillActive(a, _cardActive, active);
        }
    }
}
