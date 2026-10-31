using System.Collections.Generic;
using System.Globalization;
using EdS.Domain.Builds;
using EdS.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V1.5 (décisions de Damien : changer ses compétences « partout hors combat » ; la barre grandit avec le niveau) : l'onglet
    /// **Compétences** du panneau de l'aventure (touche B). À gauche, la barre telle qu'elle sera à la prochaine halte : les places
    /// actives en éclats, comme en combat (touches 1 à 4), l'ultime (R), les passives en nœuds ; une place fermée montre le niveau qui
    /// l'ouvre. À droite, les compétences apprises (avec leur place, ou en réserve) et celles à apprendre plus haut. On glisse une
    /// compétence sur une place, à la souris ou au doigt, ou on la touche puis on touche la place ; deux places s'échangent de même ; clic
    /// droit sur une place : la vider (permis depuis la réponse de Damien du 30 octobre 2026). Tout passe par <see cref="AdventureSession.PlaceSkill"/> et <see cref="AdventureSession.RemoveSkill"/>,
    /// donc par le juge unique du cœur : un refus ne change rien et son motif s'affiche sous la barre.
    /// </summary>
    public sealed partial class AdventureBook
    {
        private const float SkillSize = 112f, PassiveSize = 92f, SkillStep = 128f, SkillBarWidth = 760f;
        private const int SkillRows = 7, UpcomingLines = 4;

        /// <summary>Une place de la barre à l'écran.</summary>
        private sealed class SkillCell
        {
            public SkillKind Kind;
            public int Index;
            public RectTransform Rect;
            public Image Hit, Icon, Frame, Glow;
            public UiText Key, Name, Lock;
            public string Skill;
        }

        /// <summary>Une compétence apprise dans la liste de droite.</summary>
        private sealed class SkillRow
        {
            public RectTransform Rect;
            public Image Slab, Icon, Edge;
            public UiText Name, Line, Badge;
            public string Skill;
        }

        private RectTransform _skillsPage;
        private readonly List<SkillCell> _skillCells = new List<SkillCell>();
        private readonly List<SkillRow> _skillRows = new List<SkillRow>();
        private readonly List<UiText> _upcoming = new List<UiText>();
        private readonly List<string> _upcomingSkills = new List<string>();
        private UiText _skillsLearned, _skillsUpcoming, _skillsStatus;
        private RectTransform _skillDetail;
        private Image _skillDetailIcon;
        private UiText _skillDetailName, _skillDetailKind, _skillDetailStats, _skillDetailText;
        private Image _skillGhost;

        // Écran calculé pour un personnage, une session et un tick (le monde est arrêté pendant qu'un écran est ouvert : calculé une fois).
        private SkillBook _skillBook;
        private object _skillBookCharacter, _skillBookWorld;
        private long _skillBookTick = -1;

        private string _selectedSkill, _hoveredSkill, _draggedSkill, _skillMessage;

        /// <summary>Place ou ligne d'où part le glissé : un lâcher sur elle-même ne fait rien (relecture indépendante de la PR #71).</summary>
        private object _dragSource;
        private bool _skillMessageError;

        private void BuildSkills(RectTransform page)
        {
            _skillsPage = page;
            Text(page, UiFace.Bold, 15f, UiKit.Muted, TextAnchor.MiddleLeft, 60f, 168f, 500f, 20f, 0f, 0.5f, 14f, true).Value = "Barre de combat";
            Text(page, UiFace.Bold, 15f, UiKit.Muted, TextAnchor.MiddleLeft, 60f, 392f, 500f, 20f, 0f, 0.5f, 14f, true).Value = "Passives";
            Text(page, UiFace.Body, 16f, UiKit.Muted, TextAnchor.MiddleLeft, 60f, 586f, SkillBarWidth, 22f, 0f, 0.5f).Value =
                "Glisse une compétence sur une place (ou touche-la, puis la place) : deux compétences s'échangent · effet à la halte.";
            _skillsStatus = Text(page, UiFace.Bold, 17f, UiKit.Teal, TextAnchor.MiddleLeft, 60f, 614f, SkillBarWidth, 24f, 0f, 0.5f);

            // Fiche de la compétence survolée, glissée ou choisie.
            _skillDetail = UiKit.Node(page, "Fiche de la compétence");
            UiKit.Place(_skillDetail, new Vector2(48f, 644f), new Vector2(SkillBarWidth + 24f, 200f), Vector2.zero);
            UiKit.Fill(UiKit.Image(_skillDetail, "Dalle", UiKit.Sprite("dalle", 40f), Color.white, Image.Type.Sliced).rectTransform);
            RectTransform iconMask = UiKit.Mask(_skillDetail, "Forme", UiKit.Sprite("noeud_masque"));
            UiKit.Place(iconMask, new Vector2(26f, 24f), new Vector2(72f, 72f), Vector2.zero);
            _skillDetailIcon = UiKit.Image(iconMask, "Icône", null, Color.white);
            UiKit.Fill(_skillDetailIcon.rectTransform);
            _skillDetailName = Text(_skillDetail, UiFace.Title, 24f, Color.white, TextAnchor.MiddleLeft, 116f, 42f, 620f, 30f, 0f, 0.5f, 2f);
            _skillDetailName.Gold();
            _skillDetailKind = Text(_skillDetail, UiFace.Bold, 14f, UiKit.Muted, TextAnchor.MiddleLeft, 116f, 72f, 620f, 20f, 0f, 0.5f, 10f, true);
            _skillDetailStats = Text(_skillDetail, UiFace.Bold, 17f, UiKit.Dawn, TextAnchor.MiddleLeft, 116f, 98f, 620f, 22f, 0f, 0.5f, 4f);
            _skillDetailText = Text(_skillDetail, UiFace.Body, 18f, UiKit.Ink, TextAnchor.UpperLeft, 26f, 120f, SkillBarWidth - 30f, 64f, 0f, 0f);
            _skillDetailText.Wrap();

            _skillsLearned = Text(page, UiFace.Bold, 15f, UiKit.Muted, TextAnchor.MiddleLeft, 880f, 168f, 560f, 20f, 0f, 0.5f, 14f, true);
            for (int i = 0; i < SkillRows; i++) _skillRows.Add(MakeSkillRow(page, 880f, 192f + i * 72f));
            _skillsUpcoming = Text(page, UiFace.Bold, 15f, UiKit.Muted, TextAnchor.MiddleLeft, 880f, 712f, 560f, 20f, 0f, 0.5f, 14f, true);
            for (int i = 0; i < UpcomingLines; i++)
            {
                UiText line = Text(page, UiFace.Body, 17f, UiKit.Ink, TextAnchor.MiddleLeft, 880f, 740f + i * 26f, 560f, 24f, 0f, 0.5f);
                _upcoming.Add(line);
                _upcomingSkills.Add(null);
                // Une compétence à apprendre se survole (souris) ou se touche (doigt) : la fiche dit ce qu'elle fera et à quel niveau.
                int at = i;
                Image hit = UiKit.Image(line.Rect, "Zone", null, new Color(1f, 1f, 1f, 0f));
                UiKit.Fill(hit.rectTransform, -1f);
                UiPointer pointer = UiKit.Pointer(hit);
                pointer.Left = () => Select(_upcomingSkills[at]);
                pointer.Enter = () => _hoveredSkill = _upcomingSkills[at];
                pointer.Exit = () =>
                {
                    if (_hoveredSkill == _upcomingSkills[at]) _hoveredSkill = null;
                };
            }

            // L'icône qui suit le pointeur pendant un glissé, au-dessus de tout le panneau.
            _skillGhost = UiKit.Image(_panel, "Compétence glissée", null, UiKit.Alpha(Color.white, 0.85f));
            RectTransform ghost = _skillGhost.rectTransform;
            ghost.anchorMin = new Vector2(0.5f, 0.5f);
            ghost.anchorMax = new Vector2(0.5f, 0.5f);
            ghost.pivot = new Vector2(0.5f, 0.5f);
            ghost.sizeDelta = new Vector2(84f, 84f);
            UiKit.Show(_skillGhost, false);
        }

        /// <summary>Les places, une fois les règles connues (leur nombre vient des règles de progression).</summary>
        private void EnsureSkillCells(SkillBook book)
        {
            if (_skillCells.Count == book.Places.Count) return;
            foreach (SkillCell old in _skillCells) Object.Destroy(old.Rect.gameObject);
            _skillCells.Clear();
            int actives = 0;
            foreach (SkillPlace p in book.Places) if (p.Kind == SkillKind.Active) actives++;
            // Les actives et l'ultime tiennent sur une ligne : l'écart se resserre si la barre grandit.
            float step = Mathf.Min(SkillStep, (SkillBarWidth - SkillSize - 24f) / Mathf.Max(1, actives));
            float size = Mathf.Min(SkillSize, step - 16f);
            foreach (SkillPlace p in book.Places)
            {
                switch (p.Kind)
                {
                    case SkillKind.Active: _skillCells.Add(MakeSkillCell(p, 60f + p.Index * step, 196f, size)); break;
                    case SkillKind.Ultimate: _skillCells.Add(MakeSkillCell(p, 60f + actives * step + 24f, 196f, size)); break;
                    default: _skillCells.Add(MakeSkillCell(p, 70f + p.Index * SkillStep, 418f, PassiveSize)); break;
                }
            }
        }

        private SkillCell MakeSkillCell(SkillPlace place, float x, float y, float size)
        {
            var cell = new SkillCell { Kind = place.Kind, Index = place.Index, Rect = UiKit.Node(_skillsPage, "Place") };
            UiKit.Place(cell.Rect, new Vector2(x, y), new Vector2(size, size), Vector2.zero);
            bool passive = place.Kind == SkillKind.Passive;
            // Les mêmes éclats que la barre de combat (les actives tournent sur les quatre premiers, depuis V1.8 six places ; le cinquième
            // pour l'ultime) ; les passives en nœuds, comme l'arbre des Éclats.
            string shape = passive ? "noeud" : "eclat_" + (place.Kind == SkillKind.Ultimate ? 5 : place.Index % 4 + 1);
            cell.Glow = UiKit.Image(cell.Rect, "Lueur", UiKit.Sprite("braise"), UiKit.Alpha(UiKit.Dawn, 0f));
            UiKit.Fill(cell.Glow.rectTransform, -size * 0.3f);
            cell.Hit = UiKit.Image(cell.Rect, "Fond", UiKit.Sprite(passive ? "noeud_fond" : shape + "_masque"), new Color(0.09f, 0.12f, 0.17f, 0.95f));
            UiKit.Fill(cell.Hit.rectTransform);
            RectTransform mask = UiKit.Fill(UiKit.Mask(cell.Rect, "Forme", UiKit.Sprite(shape + "_masque")));
            cell.Icon = UiKit.Image(mask, "Icône", null, Color.white);
            UiKit.Fill(cell.Icon.rectTransform);
            cell.Frame = UiKit.Image(cell.Rect, "Cadre", UiKit.Sprite(shape + "_cadre"), Color.white);
            UiKit.Fill(cell.Frame.rectTransform);
            cell.Lock = UiText.Create(cell.Rect, "Niveau", UiFace.Bold, 16f, UiKit.Muted, TextAnchor.MiddleCenter, 6f, true);
            UiKit.Fill(cell.Lock.Rect);
            if (!passive)
            {
                Image plate = UiKit.Image(cell.Rect, "Touche", UiKit.Sprite("touche", 6f), Color.white, Image.Type.Sliced);
                UiKit.Place(plate.rectTransform, new Vector2(size / 2f, size - 4f), new Vector2(38f, 30f), new Vector2(0.5f, 0.5f));
                cell.Key = UiText.Create(plate.rectTransform, "Lettre", UiFace.Bold, 18f, UiKit.Dawn, TextAnchor.MiddleCenter);
                UiKit.Fill(cell.Key.Rect);
            }
            cell.Name = UiText.Create(cell.Rect, "Nom", UiFace.Bold, 16f, UiKit.Ink, TextAnchor.UpperCenter);
            UiKit.Place(cell.Name.Rect, new Vector2(size / 2f, size + 18f), new Vector2(SkillStep - 8f, 44f), new Vector2(0.5f, 0f));
            cell.Name.Wrap();

            SkillCell c = cell;
            UiPointer pointer = UiKit.Pointer(cell.Hit);
            pointer.Left = () => ClickPlace(c);
            pointer.Right = () => ClearPlace(c);
            pointer.Enter = () => _hoveredSkill = c.Skill;
            pointer.Exit = () =>
            {
                if (_hoveredSkill == c.Skill) _hoveredSkill = null;
            };
            UiKit.Drop(cell.Hit).Dropped = () => DropOn(c);
            UiDrag drag = UiKit.Drag(cell.Hit);
            drag.Begin = p => BeginSkillDrag(c.Skill, c, p);
            drag.Move = MoveSkillDrag;
            drag.End = p => EndSkillDrag();
            return cell;
        }

        private SkillRow MakeSkillRow(RectTransform page, float x, float y)
        {
            var row = new SkillRow { Rect = UiKit.Node(page, "Compétence apprise") };
            UiKit.Place(row.Rect, new Vector2(x, y), new Vector2(560f, 64f), Vector2.zero);
            row.Slab = UiKit.Image(row.Rect, "Fond", UiKit.Sprite("bouton_sobre", 12f), Color.white, Image.Type.Sliced);
            UiKit.Fill(row.Slab.rectTransform);
            row.Edge = UiKit.Image(row.Rect, "Liseré", null, UiKit.Dawn);
            UiKit.Place(row.Edge.rectTransform, new Vector2(4f, 6f), new Vector2(3f, 52f), Vector2.zero);
            RectTransform mask = UiKit.Mask(row.Rect, "Forme", UiKit.Sprite("noeud_masque"));
            UiKit.Place(mask, new Vector2(16f, 6f), new Vector2(52f, 52f), Vector2.zero);
            row.Icon = UiKit.Image(mask, "Icône", null, Color.white);
            UiKit.Fill(row.Icon.rectTransform);
            row.Name = Text(row.Rect, UiFace.Bold, 19f, UiKit.Ink, TextAnchor.MiddleLeft, 80f, 21f, 340f, 24f, 0f, 0.5f);
            row.Line = Text(row.Rect, UiFace.Body, 15f, UiKit.Muted, TextAnchor.MiddleLeft, 80f, 45f, 380f, 20f, 0f, 0.5f);
            row.Badge = Text(row.Rect, UiFace.Bold, 15f, UiKit.Dawn, TextAnchor.MiddleRight, 546f, 32f, 130f, 22f, 1f, 0.5f, 10f, true);

            SkillRow r = row;
            UiPointer pointer = UiKit.Pointer(row.Slab);
            pointer.Left = () => Select(r.Skill);
            pointer.Enter = () => _hoveredSkill = r.Skill;
            pointer.Exit = () =>
            {
                if (_hoveredSkill == r.Skill) _hoveredSkill = null;
            };
            UiDrag drag = UiKit.Drag(row.Slab);
            drag.Begin = p => BeginSkillDrag(r.Skill, r, p);
            drag.Move = MoveSkillDrag;
            drag.End = p => EndSkillDrag();
            return row;
        }

        // --- Actions --------------------------------------------------------------------------------------------------------------

        private void Select(string skill)
        {
            if (skill == null) return;
            _selectedSkill = _selectedSkill == skill ? null : skill;
            UiSounds.Play(UiCue.Click);
        }

        /// <summary>Une compétence choisie va dans la place touchée ; sans choix, toucher une place choisit sa compétence.</summary>
        private void ClickPlace(SkillCell cell)
        {
            if (_session == null) return;
            if (_selectedSkill != null)
            {
                string skill = _selectedSkill;
                _selectedSkill = null;
                Apply(() => _session.PlaceSkill(cell.Kind, cell.Index, skill));
            }
            else if (cell.Skill != null) Select(cell.Skill);
        }

        private void ClearPlace(SkillCell cell)
        {
            if (_session == null || cell.Skill == null) return;
            _selectedSkill = null;
            Apply(() => _session.RemoveSkill(cell.Kind, cell.Index));
        }

        /// <summary>
        /// Lâcher sur une place. Sur la place d'où part le glissé, rien (relecture indépendante de la PR #71 : uGUI envoie aussi le lâcher
        /// quand le doigt se lève sur l'image de départ ; le choix en cours reste).
        /// </summary>
        private void DropOn(SkillCell cell)
        {
            if (_session == null || _draggedSkill == null) return;
            bool home = ReferenceEquals(_dragSource, cell);
            string skill = _draggedSkill;
            EndSkillDrag();
            if (home) return;
            _selectedSkill = null;
            Apply(() => _session.PlaceSkill(cell.Kind, cell.Index, skill));
        }

        /// <summary>
        /// Fait un changement par le cœur. Gardé (le personnage a changé) : message et son ; refusé : le motif ; sans effet (rien à changer) :
        /// ni message, ni son (relecture indépendante de la PR #71).
        /// </summary>
        private void Apply(System.Func<string> change)
        {
            object before = _session.World.Character;
            string refusal = change();
            _skillBook = null;   // à recalculer
            if (refusal == null && ReferenceEquals(before, _session.World.Character)) return;
            _skillMessage = refusal ?? _session.Text("text.ui.skills.saved");
            _skillMessageError = refusal != null;
            UiSounds.Play(refusal == null ? UiCue.Confirm : UiCue.Error);
        }

        private void BeginSkillDrag(string skill, object source, Vector2 screen)
        {
            if (skill == null) return;
            _draggedSkill = skill;
            _dragSource = source;
            _skillGhost.sprite = SkillIcon(skill);
            UiKit.Show(_skillGhost, true);
            _skillGhost.rectTransform.SetAsLastSibling();
            MoveSkillDrag(screen);
        }

        private void MoveSkillDrag(Vector2 screen)
        {
            if (_draggedSkill == null) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_panel, screen, null, out Vector2 local)) _skillGhost.rectTransform.anchoredPosition = local;
        }

        /// <summary>Fin d'un glissé (lâché ailleurs, écran fermé ou changé) : rien ne change.</summary>
        private void EndSkillDrag()
        {
            _draggedSkill = null;
            _dragSource = null;
            if (_skillGhost != null) UiKit.Show(_skillGhost, false);
        }

        /// <summary>
        /// Écran quitté (fermé, ou autre onglet) : plus de glissé, de choix ni de survol, et le dernier message s'efface (un refus « Pas en
        /// plein combat » ne reste pas affiché à la réouverture, loin des ennemis).
        /// </summary>
        private void LeaveSkills()
        {
            EndSkillDrag();
            _selectedSkill = null;
            _hoveredSkill = null;
            _skillMessage = null;
        }

        // --- Mise à jour ----------------------------------------------------------------------------------------------------------

        private void RefreshSkills(AdventureSession a)
        {
            long tick = a.World.Zone.Engine.CurrentTick;
            if (_skillBook == null || !ReferenceEquals(_skillBookCharacter, a.World.Character) || !ReferenceEquals(_skillBookWorld, a.World) || _skillBookTick != tick)
            {
                _skillBook = a.Skills();
                _skillBookCharacter = a.World.Character;
                _skillBookWorld = a.World;
                _skillBookTick = tick;
            }
            SkillBook book = _skillBook;
            EnsureSkillCells(book);
            string highlighted = _draggedSkill ?? _selectedSkill;
            SkillCard moving = highlighted != null ? book.Card(highlighted) : null;
            foreach (SkillCell cell in _skillCells)
            {
                SkillPlace p = book.Place(cell.Kind, cell.Index);
                cell.Skill = p?.Skill;
                SkillCard card = cell.Skill != null ? book.Card(cell.Skill) : null;
                bool open = p != null && p.IsOpen;
                cell.Icon.sprite = cell.Skill != null ? SkillIcon(cell.Skill) : null;
                UiKit.Show(cell.Icon, cell.Icon.sprite != null);
                cell.Frame.color = open ? Color.white : UiKit.Alpha(Color.white, 0.3f);
                cell.Hit.color = open ? new Color(0.09f, 0.12f, 0.17f, 0.95f) : new Color(0.05f, 0.06f, 0.08f, 0.9f);
                cell.Lock.Value = open ? "" : "Niv. " + p?.OpenLevel;
                if (cell.Key != null) cell.Key.Value = p?.Key ?? "";
                // Une place ouverte vide sans compétence apprise de sa sorte en réserve (aujourd'hui la 2e passive au niveau 5) le dit.
                cell.Name.Value = !open ? "Fermée" : card != null ? card.Name : cell.Skill ?? (book.HasReserve(cell.Kind) ? "Libre" : "Rien à placer pour l'instant");
                cell.Name.Color = card != null ? UiKit.Ink : UiKit.Muted;
                // La place de la compétence choisie luit ; les places ouvertes de sa sorte s'éclairent un peu (là où elle peut aller).
                float glow = cell.Skill != null && cell.Skill == highlighted ? 0.55f : open && moving != null && moving.Kind == cell.Kind ? 0.22f : 0f;
                cell.Glow.color = UiKit.Alpha(UiKit.Dawn, glow + (glow > 0f ? 0.08f * Mathf.Sin(_time * 4f) : 0f));
            }

            _skillsLearned.Value = "Apprises · " + book.Learned.Count;
            for (int i = 0; i < _skillRows.Count; i++)
            {
                SkillRow row = _skillRows[i];
                bool used = i < book.Learned.Count;
                UiKit.Show(row.Rect, used);
                if (!used)
                {
                    row.Skill = null;
                    continue;
                }
                SkillCard card = book.Learned[i];
                row.Skill = card.Id;
                row.Icon.sprite = SkillIcon(card.Id);
                row.Name.Value = card.Name;
                row.Line.Value = Summary(card);
                bool reserve = card.Place == null;
                row.Badge.Value = reserve ? "Réserve" : card.Place.Kind == SkillKind.Passive ? "Passive" : "Touche " + card.Place.Key;
                row.Badge.Color = reserve ? UiKit.Teal : UiKit.Dawn;
                row.Edge.color = card.Id == highlighted ? UiKit.Dawn : UiKit.Alpha(Color.white, 0.25f);
            }
            _skillsUpcoming.Value = book.Upcoming.Count > 0 ? "À apprendre" : "";
            for (int i = 0; i < _upcoming.Count; i++)
            {
                bool used = i < book.Upcoming.Count;
                _upcoming[i].Value = used ? $"Niveau {book.Upcoming[i].Level} · {book.Upcoming[i].Name} · {KindName(book.Upcoming[i].Kind)}" : "";
                _upcomingSkills[i] = used ? book.Upcoming[i].Id : null;
                _upcoming[i].Color = used && book.Upcoming[i].Id == (_hoveredSkill ?? _selectedSkill) ? UiKit.Dawn : UiKit.Ink;
            }

            if (_skillMessage != null)
            {
                _skillsStatus.Value = _skillMessage;
                _skillsStatus.Color = _skillMessageError ? UiKit.Endurance : UiKit.Teal;
            }
            else
            {
                _skillsStatus.Value = book.Locked ?? "";
                _skillsStatus.Color = UiKit.Endurance;
            }
            RefreshSkillDetail(book, _hoveredSkill ?? _draggedSkill ?? _selectedSkill);
        }

        /// <summary>Fiche : icône, nom, sorte et place (ou réserve, ou niveau où elle s'apprend), coût et recharge, description.</summary>
        private void RefreshSkillDetail(SkillBook book, string skill)
        {
            SkillCard card = skill != null ? book.Card(skill) : null;
            UiKit.Show(_skillDetail, card != null);
            if (card == null) return;
            _skillDetailIcon.sprite = SkillIcon(card.Id);
            _skillDetailName.Value = card.Name;
            string where = !card.Learned ? $"s'apprend au niveau {card.Level}"
                : card.Place == null ? "en réserve"
                : card.Place.Kind == SkillKind.Passive ? $"place passive {card.Place.Index + 1}" : $"touche {card.Place.Key}";
            _skillDetailKind.Value = KindName(card.Kind) + " · " + where;
            _skillDetailStats.Value = Summary(card);
            _skillDetailText.Value = card.Description;
        }

        private static string KindName(SkillKind kind) => kind == SkillKind.Active ? "active" : kind == SkillKind.Ultimate ? "ultime" : "passive";

        /// <summary>« 10 endurance · recharge 6 s · 2,5 m » pour une active ; l'ultime part à Résonance pleine ; une passive agit toujours.</summary>
        private static string Summary(SkillCard card)
        {
            if (card.Kind == SkillKind.Passive) return "Passive · toujours en effet";
            if (card.Kind == SkillKind.Ultimate) return "Ultime · à Résonance pleine";
            string line = $"{card.EnduranceCost} endurance · recharge {Number(card.CooldownSeconds)} s";
            return card.RangeMeters is double range ? line + " · " + Number(range) + " m" : line;
        }

        private static string Number(double value) => value.ToString("0.#", CultureInfo.InvariantCulture).Replace('.', ',');

        /// <summary>Icône peinte d'une compétence (fin de son identifiant) ; un éclat du Seuil pour celles qui n'en ont pas (les passives).</summary>
        private static Sprite SkillIcon(string skill)
        {
            Sprite painted = UiKit.Icon(HudView.IconName(skill));
            return painted != null ? painted : UiKit.Icon("eclat_de_seuil");
        }
    }
}
