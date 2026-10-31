using System.Collections.Generic;
using System.Linq;
using EdS.Data.Items;
using EdS.Runtime;
using EdS.World;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V1.2 : l'écran du personnage aux 18 places (16 du combat autour du sceau, l'Éclat du Seuil au centre, l'outil de métier en
    /// dessous), les filtres et le rangement du sac, et la comparaison au survol d'un objet : chaque bonus et, pour un bouclier, la garde
    /// (part bloquée, endurance par blocage, fenêtre de parade), avec l'écart en vert (mieux) ou en rouge (moins bien). Clic sur un objet
    /// porté : l'ôter ; clic droit sur un anneau ou une relique : l'échanger avec l'autre place de sa sorte. Sous-lot V1.3 : la fiche compte
    /// les propriétés tirées de l'instance survolée et montre ses détails : propriétés, effet, ensemble (pièces portées sur le total) et
    /// paliers, en vert ou en rouge selon ce que le port changerait.
    /// </summary>
    public sealed partial class AdventureBook
    {
        /// <summary>Places de l'écran : place, position (haut gauche de la case), libellé à droite (vrai) ou à gauche.</summary>
        private static readonly (string Slot, float X, float Y, bool LabelRight)[] SlotPlaces =
        {
            ("head", 60f, 160f, true), ("shoulders", 60f, 222f, true), ("cloak", 60f, 284f, true), ("chest", 60f, 346f, true),
            ("bracers", 60f, 408f, true), ("hands", 60f, 470f, true),
            ("belt", 576f, 160f, false), ("legs", 576f, 222f, false), ("feet", 576f, 284f, false), ("neck", 576f, 346f, false),
            ("ring_1", 576f, 408f, false), ("ring_2", 576f, 470f, false),
            ("weapon", 222f, 190f, false), ("shield", 412f, 190f, true), ("relic_1", 222f, 380f, false), ("relic_2", 412f, 380f, true),
            ("shard", 317f, 285f, true), ("tool", 317f, 470f, true),
        };

        private const float SlotSize = 54f;

        private static readonly InventoryFilter[] Filters =
            { InventoryFilter.All, InventoryFilter.Equipment, InventoryFilter.Wearable, InventoryFilter.Consumables, InventoryFilter.Materials };

        private InventoryFilter _filter = InventoryFilter.All;
        private InventorySort _sort = InventorySort.Type;
        private readonly List<UiButton> _filterButtons = new List<UiButton>();
        private UiButton _sortButton;

        /// <summary>Place du sac montrée par chaque case (selon le filtre).</summary>
        private IReadOnlyList<int> _visible = new int[0];

        /// <summary>Lignes de la comparaison dans la fiche : libellé, valeurs, écart.</summary>
        private readonly List<(UiText Label, UiText Values, UiText Delta)> _compareRows = new List<(UiText, UiText, UiText)>();
        private UiText _tipNotions;

        /// <summary>
        /// Sous-lot V1.3 : lignes de détail de la fiche : propriétés tirées, effet (vert s'il s'ajoute, rouge s'il se perd), ensemble (pièces
        /// portées sur le total) et ses paliers (atténués tant qu'ils ne valent pas).
        /// </summary>
        private readonly List<UiText> _detailRows = new List<UiText>();

        /// <summary>Largeur utile d'une ligne de détail et nombre de caractères qui y tiennent environ (police de 14).</summary>
        private const float DetailWidth = 422f;
        private const int DetailCharsPerLine = 58;

        /// <summary>Relecture de la PR #78 : lignes de détail de la fiche (la dernière annonce « … et N de plus » s'il en manque).</summary>
        private const int DetailRows = 14;

        private static readonly Color Better = new Color(0.45f, 0.9f, 0.5f), Worse = new Color(0.95f, 0.38f, 0.33f);

        private void BuildSlots(RectTransform page)
        {
            foreach ((string slot, float x, float y, bool right) in SlotPlaces)
            {
                Cell cell = MakeCell(page, x, y, SlotSize, false);
                cell.Label = UiText.Create(cell.Rect, "Place", UiFace.Bold, 12f, UiKit.Muted, right ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight, 8f, true);
                UiKit.Place(cell.Label.Rect, new Vector2(right ? SlotSize + 8f : -8f, SlotSize / 2f - 9f), new Vector2(120f, 16f), new Vector2(right ? 0f : 1f, 0.5f));
                cell.Name = UiText.Create(cell.Rect, "Objet", UiFace.Bold, 14f, UiKit.Ink, right ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
                UiKit.Place(cell.Name.Rect, new Vector2(right ? SlotSize + 8f : -8f, SlotSize / 2f + 9f), new Vector2(150f, 18f), new Vector2(right ? 0f : 1f, 0.5f));
                string s = slot;
                UiPointer pointer = UiKit.Pointer(cell.Slab);
                pointer.Left = () =>
                {
                    if (_session != null && _session.World.Inventory.Equipped.ContainsKey(s)) _session.Unequip(s);
                };
                pointer.Right = () => SwapSibling(s);
                pointer.Enter = () =>
                {
                    string worn = null;
                    if (_session != null) _session.World.Inventory.Equipped.TryGetValue(s, out worn);
                    ShowTip(worn, cell.Rect, true, s);
                };
                pointer.Exit = () => HideTip(cell.Rect);
                _slots[slot] = cell;
            }
        }

        /// <summary>Clic droit : échange un anneau ou une relique avec l'autre place de sa sorte.</summary>
        private void SwapSibling(string slot)
        {
            if (_session == null || !_session.World.Inventory.Catalog.TryGetPlace(slot, out EquipmentPlace place)) return;
            foreach (EquipmentPlace other in _session.World.Inventory.Catalog.PlacesFor(place.Accepts))
                if (other.Id != slot)
                {
                    _session.Swap(slot, other.Id);
                    return;
                }
        }

        private void BuildBagTools(RectTransform page, float y)
        {
            float x = 730f;
            for (int i = 0; i < Filters.Length; i++)
            {
                InventoryFilter filter = Filters[i];
                UiButton b = UiKit.Button(page, "Filtre", "bouton_sobre", "", UiFace.Bold, 12f, UiKit.Muted, () => _filter = filter);
                UiKit.Place(b.Rect, new Vector2(x, y), new Vector2(104f, 26f), Vector2.zero);
                _filterButtons.Add(b);
                x += 110f;
            }
            _sortButton = UiKit.Button(page, "Ranger", "bouton_sobre", "", UiFace.Bold, 12f, UiKit.Dawn, () =>
            {
                if (_session == null) return;
                _session.SortInventory(_sort);
                _sort = (InventorySort)(((int)_sort + 1) % 3);
            });
            UiKit.Place(_sortButton.Rect, new Vector2(x + 12f, y), new Vector2(150f, 26f), Vector2.zero);
        }

        private void RefreshBagTools(AdventureSession a)
        {
            for (int i = 0; i < Filters.Length; i++)
            {
                _filterButtons[i].Text.Value = a.Text("text.ui.filter." + Filters[i].ToString().ToLowerInvariant());
                _filterButtons[i].Text.Color = Filters[i] == _filter ? UiKit.Dawn : UiKit.Muted;
            }
            _sortButton.Text.Value = a.Text("text.ui.sort." + _sort.ToString().ToLowerInvariant());
            _visible = a.InventoryPlaces(_filter);
        }

        /// <summary>Place réelle du sac montrée par la case <paramref name="cell"/>, ou −1.</summary>
        private int BagPlace(int cell) => cell < _visible.Count ? _visible[cell] : -1;

        /// <summary>Relecture de la PR #75 : comme <see cref="BagPlace"/>, d'après le sac tel qu'il est maintenant (pour un clic), ou −1.</summary>
        private int CurrentBagPlace(int cell)
        {
            if (_session == null) return -1;
            IReadOnlyList<int> visible = _session.InventoryPlaces(_filter);
            return cell >= 0 && cell < visible.Count ? visible[cell] : -1;
        }

        private void BuildCompare()
        {
            _tipNotions = Text(_tip, UiFace.Body, 14f, UiKit.Muted, TextAnchor.UpperLeft, 24f, 92f, 360f, 36f, 0f, 0f);
            _tipNotions.Wrap();
            for (int i = 0; i < 11; i++)
            {
                UiText label = Text(_tip, UiFace.Body, 15f, UiKit.Ink, TextAnchor.MiddleLeft, 24f, 0f, 190f, 20f, 0f, 0.5f);
                UiText values = Text(_tip, UiFace.Number, 15f, Color.white, TextAnchor.MiddleRight, 300f, 0f, 120f, 20f, 1f, 0.5f);
                UiText delta = Text(_tip, UiFace.Bold, 15f, Color.white, TextAnchor.MiddleRight, 376f, 0f, 70f, 20f, 1f, 0.5f);
                _compareRows.Add((label, values, delta));
            }
            for (int i = 0; i < DetailRows; i++)
            {
                UiText detail = Text(_tip, UiFace.Body, 14f, UiKit.Ink, TextAnchor.UpperLeft, 24f, 0f, DetailWidth, 20f, 0f, 0f);
                detail.Wrap();
                _detailRows.Add(detail);
            }
        }

        /// <summary>
        /// Remplit la comparaison de <paramref name="item"/> (porté à <paramref name="wornPlace"/>, ou dans le sac) ; rend la hauteur occupée
        /// sous la ligne des notions, ou −1 s'il n'y a rien à comparer (l'objet ne se porte pas).
        /// </summary>
        private float RefreshCompare(AdventureSession a, string item, string wornPlace, int bagPlace, float top)
        {
            // Sous-lot V1.3 : l'instance elle-même (propriétés tirées comprises), au sac ou portée ; sinon l'objet de base (offre, rachat).
            ItemComparison c = null;
            if (item != null)
            {
                if (wornPlace != null) c = a.CompareInstance(-1, wornPlace);
                else if (bagPlace >= 0 && bagPlace < a.World.Inventory.Stacks.Count && a.World.Inventory.Stacks[bagPlace].Item == item) c = a.CompareInstance(bagPlace);
                if (c == null || c.Candidate.Id != item) c = a.Compare(item, wornPlace);
            }
            int shown = 0;
            if (c != null)
            {
                ItemDefinition d = c.Candidate;
                var notions = new List<string> { string.Format(a.Text("text.ui.level_required"), d.RequiredLevel) };
                if (d.Fits.Count > 0) notions.Add("pour " + string.Join(", ", d.Fits.Select(TreeName)));
                notions.Add(c.IsWorn ? "porté" : c.Worn != null ? "à la place de « " + c.Worn.Name + " »" : "place libre");
                _tipNotions.Value = string.Join(" · ", notions);
                _tipNotions.Color = a.World.Level < d.RequiredLevel ? UiKit.Endurance : UiKit.Muted;
                foreach (CompareLine line in c.Lines)
                {
                    if (shown >= _compareRows.Count) break;
                    var row = _compareRows[shown];
                    float y = top + 40f + shown * 22f;
                    foreach (UiText t in new[] { row.Label, row.Values, row.Delta }) t.Show(true);
                    UiKit.Place(row.Label.Rect, new Vector2(24f, y), new Vector2(190f, 20f), new Vector2(0f, 0.5f));
                    UiKit.Place(row.Values.Rect, new Vector2(300f, y), new Vector2(150f, 20f), new Vector2(1f, 0.5f));
                    UiKit.Place(row.Delta.Rect, new Vector2(376f, y), new Vector2(70f, 20f), new Vector2(1f, 0.5f));
                    row.Label.Value = line.Label;
                    row.Values.Value = c.IsWorn ? line.Candidate : line.Worn + " → " + line.Candidate;
                    row.Delta.Value = c.IsWorn ? "" : line.Delta;
                    row.Delta.Color = line.Trend == CompareTrend.Better ? Better : line.Trend == CompareTrend.Worse ? Worse : UiKit.Muted;
                    shown++;
                }
            }
            else _tipNotions.Value = "";
            for (int i = shown; i < _compareRows.Count; i++)
            {
                _compareRows[i].Label.Show(false);
                _compareRows[i].Values.Show(false);
                _compareRows[i].Delta.Show(false);
            }
            // Sous-lot V1.3 : détails sous les écarts (propriétés, effets, ensemble), en vert ou en rouge comme la comparaison.
            // Relecture de la PR #78 : au-delà des lignes de la fiche, la dernière dit combien de détails ne sont pas montrés (rien n'est
            // coupé sans prévenir).
            float detailsHeight = 0f;
            int details = 0;
            if (c != null)
            {
                int total = c.Details.Count;
                int room = total > _detailRows.Count ? _detailRows.Count - 1 : total;
                for (int k = 0; k <= room && k < total; k++)
                {
                    bool more = k == room;
                    string text = more ? string.Format("… et {0} de plus", total - room) : c.Details[k].Text;
                    UiText row = _detailRows[details++];
                    float lines = Mathf.Ceil(Mathf.Max(1, text.Length) / (float)DetailCharsPerLine);
                    float h = 18f * lines + 2f;
                    row.Show(true);
                    UiKit.Place(row.Rect, new Vector2(24f, top + 44f + shown * 22f + detailsHeight), new Vector2(DetailWidth, h), Vector2.zero);
                    row.Value = text;
                    DetailLine line = more ? null : c.Details[k];
                    row.Color = line == null ? UiKit.Muted
                        : line.Trend == CompareTrend.Better ? Better : line.Trend == CompareTrend.Worse ? Worse : line.Active ? UiKit.Ink : UiKit.Muted;
                    detailsHeight += h;
                }
            }
            for (int i = details; i < _detailRows.Count; i++) _detailRows[i].Show(false);
            return c == null ? -1f : 40f + shown * 22f + (details > 0 ? detailsHeight + 6f : 0f);
        }

        /// <summary>Nom montré d'un arbre de talents (« tree.bastion.rempart » → « Rempart »).</summary>
        private static string TreeName(string tree)
        {
            string last = tree.Substring(tree.LastIndexOf('.') + 1);
            return last.Length == 0 ? tree : char.ToUpperInvariant(last[0]) + last.Substring(1);
        }
    }
}
