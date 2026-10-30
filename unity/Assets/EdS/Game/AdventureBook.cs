using System.Collections.Generic;
using System.Linq;
using EdS.Data.Items;
using EdS.Data.Saves;
using EdS.Domain.Builds;
using EdS.Domain.Stats;
using EdS.Runtime;
using EdS.World;
using UnityEngine;
using UnityEngine.UI;

namespace EdS.Game
{
    /// <summary>
    /// Essai réaliste 6, étape 2 (essai de Damien du 27 octobre 2026 : « l'interface est mieux mais c'est pas ce que je préfère pareil pour
    /// l'inventaire, l'aide tout ces petits détails ») : les écrans de l'aventure en uGUI, dans le style « Fragments du Seuil ». Un grand
    /// panneau à onglets : **Personnage** (I : équipement autour du sceau du Bastion, statut, sac, expérience, métiers), **Arbre des
    /// Éclats** (ouvre l'arbre des talents), **Journal** (J : quêtes et découvertes), **Atelier** (K : recettes et métiers), **Carte**
    /// (L : la carte peinte en grand, lieux et repères) ; et le dialogue avec un PNJ, en bas de l'écran. Sous-lot V1.5 : l'onglet
    /// **Compétences** (B, <c>AdventureBook.Skills.cs</c>). Toute action passe par <see cref="AdventureSession"/>, donc par le cœur : un
    /// refus ne change rien et sa raison s'affiche (messages du jeu).
    /// </summary>
    public sealed partial class AdventureBook
    {
        /// <summary>Ce que le panneau demande au jeu : rien, fermer, ou ouvrir l'arbre des Éclats.</summary>
        public enum Request { None, Close, Talents }

        private const float PanelWidth = 1500f, PanelHeight = 880f;

        /// <summary>
        /// Onglets, de gauche à droite : écran ouvert (<see cref="AdventureScreens.Panel.None"/> : l'arbre des Éclats, qui s'ouvre à part),
        /// clé du texte, largeur. Sous-lot V1.5 : les compétences à côté du personnage.
        /// </summary>
        private static readonly (AdventureScreens.Panel Panel, string Key, float Width)[] Tabs =
        {
            (AdventureScreens.Panel.Inventory, "text.ui.tab.character", 150f), (AdventureScreens.Panel.Skills, "text.ui.tab.skills", 165f),
            (AdventureScreens.Panel.None, "text.ui.tab.talents", 210f), (AdventureScreens.Panel.Journal, "text.ui.tab.journal", 130f),
            (AdventureScreens.Panel.Crafting, "text.ui.tab.crafting", 130f), (AdventureScreens.Panel.Map, "text.ui.tab.map", 120f),
        };

        private sealed class Cell
        {
            public RectTransform Rect;
            public Image Slab, Icon, Edge;
            public UiText Count, Label, Name;
        }

        private readonly Canvas _canvas;
        private readonly CanvasGroup _fade;
        private readonly RectTransform _book, _panel;
        private bool _bookShown;
        private readonly UiText _title, _subtitle, _close;
        private readonly List<UiText> _tabs = new List<UiText>();
        private readonly Dictionary<AdventureScreens.Panel, RectTransform> _pages = new Dictionary<AdventureScreens.Panel, RectTransform>();
        private AdventureSession _session;
        private Request _request;
        private float _time;

        // Personnage.
        private readonly Dictionary<string, Cell> _slots = new Dictionary<string, Cell>();
        private readonly List<Cell> _bag = new List<Cell>();
        private UiText _bagTitle, _xpValue, _xpNote, _health, _endurance, _points;
        private SavedCharacter _pointsFor;
        private Image _xpBar;
        private readonly List<UiText> _attributes = new List<UiText>();
        private readonly List<(RectTransform Card, Image Icon, UiText Name, UiText Level)> _professions = new List<(RectTransform, Image, UiText, UiText)>();
        private RectTransform _tip;
        private Image _tipIcon;
        private UiText _tipName, _tipKind, _tipLines, _tipAction;
        private string _tipItem;
        private RectTransform _tipAnchor;

        // Journal.
        private readonly List<(RectTransform Block, UiText Name, UiText Step, Image Bar, UiText Progress)> _quests = new List<(RectTransform, UiText, UiText, Image, UiText)>();
        private readonly List<UiText> _discoveries = new List<UiText>();
        private UiText _journalEmpty, _discoveriesTitle;

        // Atelier.
        private readonly List<(RectTransform Card, Image Icon, UiText Name, UiText Kind, UiText Recipe, UiText State, UiButton Craft)> _recipes =
            new List<(RectTransform, Image, UiText, UiText, UiText, UiText, UiButton)>();
        private UiText _craftProfessions;
        private IReadOnlyList<RecipeEntry> _recipeData = new RecipeEntry[0];

        // Carte.
        private RawImage _map;
        private RectTransform _mapMarkers;
        private Image _mapYou;
        private readonly List<Image> _markerPool = new List<Image>();
        private readonly List<UiText> _namePool = new List<UiText>();
        private int _markerCount, _nameCount;
        private UiText _legend;

        // Dialogue.
        private readonly RectTransform _dialogue;
        private readonly UiText _npc, _speech;
        private readonly List<UiButton> _answers = new List<UiButton>();
        private readonly UiButton _leave;
        private string _shownDialogue;

        private AdventureBook()
        {
            UiKit.EnsureEvents();
            _canvas = UiKit.CreateCanvas("Écrans de l'aventure", 15);
            Transform root = _canvas.transform;
            _book = UiKit.Fill(UiKit.Node(root, "Livre"));
            _fade = _book.gameObject.AddComponent<CanvasGroup>();
            Image dim = UiKit.Image(_book, "Voile", null, new Color(0.02f, 0.03f, 0.05f, 0.62f));
            UiKit.Fill(dim.rectTransform);
            dim.raycastTarget = true;
            _panel = UiKit.Node(_book, "Panneau");
            _panel.anchorMin = new Vector2(0.5f, 0.5f);
            _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.pivot = new Vector2(0.5f, 0.5f);
            _panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            _panel.anchoredPosition = new Vector2(0f, -10f);
            UiKit.Fill(UiKit.Image(_panel, "Dalle", UiKit.Sprite("dalle", 40f), Color.white, Image.Type.Sliced).rectTransform);
            _title = Text(_panel, UiFace.Title, 40f, Color.white, TextAnchor.MiddleLeft, 60f, 62f, 600f, 50f, 0f, 0.5f, 3f);
            _title.Gold();
            _subtitle = Text(_panel, UiFace.Bold, 16f, UiKit.Muted, TextAnchor.MiddleLeft, 62f, 104f, 900f, 22f, 0f, 0.5f, 12f, true);
            float tx = 540f;
            for (int i = 0; i < Tabs.Length; i++)
            {
                int index = i;
                UiText tab = Text(_panel, UiFace.Bold, 18f, UiKit.Muted, TextAnchor.MiddleLeft, tx, 72f, Tabs[i].Width - 12f, 30f, 0f, 0.5f, 12f, true);
                Image hit = UiKit.Image(tab.Rect, "Zone", null, new Color(1f, 1f, 1f, 0f));
                UiKit.Fill(hit.rectTransform, -6f);
                UiKit.Pointer(hit).Left = () => Tab(index);
                _tabs.Add(tab);
                tx += Tabs[i].Width;
            }
            _close = Text(_panel, UiFace.Bold, 15f, UiKit.Muted, TextAnchor.MiddleRight, 1440f, 106f, 300f, 24f, 1f, 0.5f, 12f, true);
            Image closeHit = UiKit.Image(_close.Rect, "Zone", null, new Color(1f, 1f, 1f, 0f));
            UiKit.Fill(closeHit.rectTransform, -6f);
            UiKit.Pointer(closeHit).Left = () => _request = Request.Close;
            Image rule = UiKit.Image(_panel, "Filet", null, UiKit.Alpha(UiKit.Dawn, 0.55f));
            UiKit.Place(rule.rectTransform, new Vector2(40f, 132f), new Vector2(PanelWidth - 80f, 2f), Vector2.zero);
            BuildMerchant(Page(AdventureScreens.Panel.Merchant));   // sous-lot V1.1 : la boutique (AdventureBook.Merchant.cs)

            BuildCharacter(Page(AdventureScreens.Panel.Inventory));
            BuildJournal(Page(AdventureScreens.Panel.Journal));
            BuildCrafting(Page(AdventureScreens.Panel.Crafting));
            BuildMap(Page(AdventureScreens.Panel.Map));
            BuildSkills(Page(AdventureScreens.Panel.Skills));   // sous-lot V1.5
            BuildTip();

            // Dialogue : en bas de l'écran, hors du panneau.
            _dialogue = UiKit.Node(root, "Dialogue");
            _dialogue.anchorMin = new Vector2(0.5f, 0f);
            _dialogue.anchorMax = new Vector2(0.5f, 0f);
            _dialogue.pivot = new Vector2(0.5f, 0f);
            _dialogue.sizeDelta = new Vector2(1300f, 300f);
            _dialogue.anchoredPosition = new Vector2(0f, 28f);
            UiKit.Fill(UiKit.Image(_dialogue, "Dalle", UiKit.Sprite("dalle", 40f), Color.white, Image.Type.Sliced).rectTransform);
            _npc = Text(_dialogue, UiFace.Title, 28f, Color.white, TextAnchor.MiddleLeft, 40f, 44f, 900f, 36f, 0f, 0.5f, 2f);
            _npc.Gold();
            _speech = Text(_dialogue, UiFace.Body, 21f, UiKit.Ink, TextAnchor.UpperLeft, 40f, 70f, 1220f, 80f, 0f, 0f);
            _speech.Wrap();
            _leave = UiKit.Button(_dialogue, "Fermer", "bouton_sobre", "", UiFace.Bold, 15f, UiKit.Muted, () => _session?.CloseDialogue());
            UiKit.Place(_leave.Rect, new Vector2(1260f, 24f), new Vector2(220f, 40f), new Vector2(1f, 0f));
            UiKit.Show(_canvas, false);
        }

        public static AdventureBook Create() => new AdventureBook();

        private static UiText Text(Transform parent, UiFace face, float size, Color color, TextAnchor anchor, float x, float y, float w, float h, float px, float py,
            float spacing = 0f, bool upper = false)
        {
            UiText text = UiText.Create(parent, "Texte", face, size, color, anchor, spacing, upper);
            UiKit.Place(text.Rect, new Vector2(x, y), new Vector2(w, h), new Vector2(px, py));
            return text;
        }

        private RectTransform Page(AdventureScreens.Panel panel)
        {
            RectTransform page = UiKit.Fill(UiKit.Node(_panel, panel.ToString()));
            _pages[panel] = page;
            return page;
        }

        private static Cell MakeCell(Transform parent, float x, float y, float size, bool labels)
        {
            var cell = new Cell { Rect = UiKit.Node(parent, "Case") };
            UiKit.Place(cell.Rect, new Vector2(x, y), new Vector2(size, size), Vector2.zero);
            cell.Slab = UiKit.Image(cell.Rect, "Fond", UiKit.Sprite("noeud_fond"), new Color(0.75f, 0.8f, 0.9f, 1f));
            UiKit.Fill(cell.Slab.rectTransform);
            cell.Icon = UiKit.Image(cell.Rect, "Icône", null, Color.white);
            UiKit.Fill(cell.Icon.rectTransform, size * 0.08f);
            cell.Edge = UiKit.Image(cell.Rect, "Bord", UiKit.Sprite("bouton_sobre", 12f), Color.white, Image.Type.Sliced);
            cell.Edge.fillCenter = false;   // relecture : le centre opaque du dessin cachait l'icône
            UiKit.Fill(cell.Edge.rectTransform, -2f);
            cell.Count = UiText.Create(cell.Rect, "Nombre", UiFace.Bold, 17f, Color.white, TextAnchor.LowerRight);
            UiKit.Place(cell.Count.Rect, new Vector2(size - 6f, size - 4f), new Vector2(60f, 22f), new Vector2(1f, 1f));
            if (labels)
            {
                cell.Label = UiText.Create(cell.Rect, "Emplacement", UiFace.Bold, 13f, UiKit.Muted, TextAnchor.MiddleCenter, 12f, true);
                UiKit.Place(cell.Label.Rect, new Vector2(size / 2f, size + 16f), new Vector2(220f, 18f), new Vector2(0.5f, 0.5f));
                cell.Name = UiText.Create(cell.Rect, "Objet", UiFace.Bold, 17f, UiKit.Ink, TextAnchor.MiddleCenter);
                UiKit.Place(cell.Name.Rect, new Vector2(size / 2f, size + 38f), new Vector2(240f, 22f), new Vector2(0.5f, 0.5f));
            }
            return cell;
        }

        // --- Personnage ------------------------------------------------------------------------------------------------------------

        private void BuildCharacter(RectTransform page)
        {
            // Sceau du Bastion au milieu de l'équipement.
            RectTransform emblem = UiKit.Node(page, "Sceau");
            UiKit.Place(emblem, new Vector2(345f, 310f), new Vector2(290f, 320f), new Vector2(0.5f, 0.5f));
            emblem.localScale = new Vector3(0.8f, 0.8f, 1f);
            foreach (string part in new[] { "sceau_fond", "sceau_fissure", "sceau_contour" })
                UiKit.Fill(UiKit.Image(emblem, part, UiKit.Sprite(part), Color.white).rectTransform);
            UiText caption = Text(page, UiFace.Title, 18f, Color.white, TextAnchor.MiddleCenter, 345f, 150f, 300f, 26f, 0.5f, 0.5f, 3f);
            caption.Gold();
            caption.Value = "Sceau du Bastion";
            BuildSlots(page);   // sous-lot V1.2 : les 18 places (AdventureBook.Equipment.cs)

            Text(page, UiFace.Bold, 15f, UiKit.Muted, TextAnchor.MiddleLeft, 60f, 532f, 300f, 20f, 0f, 0.5f, 14f, true).Value = "Statut";
            for (int i = 0; i < 6; i++)
            {
                float x = 60f + (i % 3) * 210f, y = 572f + (i / 3) * 40f;
                UiText label = Text(page, UiFace.Body, 19f, UiKit.Ink, TextAnchor.MiddleLeft, x, y, 150f, 28f, 0f, 0.5f);
                label.Value = new[] { "Force", "Dextérité", "Intelligence", "Vitalité", "Volonté", "Endurance" }[i];
                UiText value = Text(page, UiFace.Number, 26f, Color.white, TextAnchor.MiddleRight, x + 185f, y, 60f, 30f, 1f, 0.5f);
                _attributes.Add(value);
                Image line = UiKit.Image(page, "Filet", null, UiKit.Alpha(UiKit.Muted, 0.2f));
                UiKit.Place(line.rectTransform, new Vector2(x, y + 18f), new Vector2(185f, 1f), Vector2.zero);
            }
            string[] big = { "Points de vie", "Endurance", "Points d'Éclat" };
            var values = new UiText[3];
            for (int i = 0; i < 3; i++)
            {
                float x = 60f + i * 210f;
                Text(page, UiFace.Bold, 13f, UiKit.Muted, TextAnchor.MiddleLeft, x, 676f, 200f, 18f, 0f, 0.5f, 12f, true).Value = big[i];
                values[i] = Text(page, UiFace.Number, 34f, i == 2 ? UiKit.Dawn : Color.white, TextAnchor.MiddleLeft, x, 710f, 200f, 40f, 0f, 0.5f);
            }
            (_health, _endurance, _points) = (values[0], values[1], values[2]);

            _bagTitle = Text(page, UiFace.Bold, 15f, UiKit.Muted, TextAnchor.MiddleLeft, 730f, 168f, 400f, 20f, 0f, 0.5f, 14f, true);
            BuildPurse(page);   // sous-lot V1.1 : la bourse à droite du sac
            for (int i = 0; i < 24; i++)
            {
                int place = i;
                Cell cell = MakeCell(page, 730f + (i % 8) * 90f, 192f + (i / 8) * 90f, 82f, false);
                UiPointer pointer = UiKit.Pointer(cell.Slab);
                pointer.Left = () => UseBag(BagPlace(place));   // sous-lot V1.2 : la case montre une place selon le filtre
                pointer.Enter = () =>
                {
                    int real = BagPlace(place);
                    ShowTip(_session != null && real >= 0 && real < _session.World.Inventory.Stacks.Count ? _session.World.Inventory.Stacks[real].Item : null, cell.Rect, false);
                };
                pointer.Exit = () => HideTip(cell.Rect);
                _bag.Add(cell);
            }
            BuildBagTools(page, 460f);   // sous-lot V1.2 : filtres et rangement
            Text(page, UiFace.Bold, 15f, UiKit.Muted, TextAnchor.MiddleLeft, 730f, 506f, 300f, 20f, 0f, 0.5f, 14f, true).Value = "Expérience";
            _xpValue = Text(page, UiFace.Bold, 17f, UiKit.Ink, TextAnchor.MiddleRight, 1442f, 506f, 300f, 22f, 1f, 0.5f, 6f);
            Image track = UiKit.Image(page, "Fond de l'expérience", UiKit.Sprite("cible_remplissage"), new Color(0.06f, 0.08f, 0.11f, 1f));
            UiKit.Place(track.rectTransform, new Vector2(730f, 522f), new Vector2(712f, 14f), Vector2.zero);
            _xpBar = UiKit.Image(page, "Expérience", UiKit.Sprite("cible_remplissage"), UiKit.Rose, Image.Type.Filled);
            UiKit.Place(_xpBar.rectTransform, new Vector2(730f, 522f), new Vector2(712f, 14f), Vector2.zero);
            _xpBar.fillMethod = Image.FillMethod.Horizontal;
            _xpNote = Text(page, UiFace.Body, 16f, UiKit.Muted, TextAnchor.MiddleLeft, 730f, 552f, 712f, 22f, 0f, 0.5f);

            Text(page, UiFace.Bold, 15f, UiKit.Muted, TextAnchor.MiddleLeft, 730f, 600f, 300f, 20f, 0f, 0.5f, 14f, true).Value = "Métiers";
            for (int i = 0; i < 3; i++)
            {
                RectTransform card = UiKit.Node(page, "Métier");
                UiKit.Place(card, new Vector2(730f + i * 240f, 622f), new Vector2(228f, 66f), Vector2.zero);
                UiKit.Fill(UiKit.Image(card, "Fond", UiKit.Sprite("bouton_sobre", 12f), Color.white, Image.Type.Sliced).rectTransform);
                Image edge = UiKit.Image(card, "Liseré", null, UiKit.Dawn);
                UiKit.Place(edge.rectTransform, new Vector2(4f, 6f), new Vector2(3f, 54f), Vector2.zero);
                Image icon = UiKit.Image(card, "Icône", null, Color.white);
                UiKit.Place(icon.rectTransform, new Vector2(16f, 9f), new Vector2(48f, 48f), Vector2.zero);
                UiText name = Text(card, UiFace.Bold, 18f, UiKit.Ink, TextAnchor.MiddleLeft, 74f, 24f, 150f, 24f, 0f, 0.5f);
                UiText level = Text(card, UiFace.Bold, 13f, UiKit.Muted, TextAnchor.MiddleLeft, 74f, 46f, 150f, 18f, 0f, 0.5f, 10f, true);
                _professions.Add((card, icon, name, level));
            }
            Text(page, UiFace.Body, 15f, UiKit.Muted, TextAnchor.MiddleLeft, 730f, 712f, 712f, 40f, 0f, 0.5f).Value =
                "Clic sur un objet du sac : le porter · clic sur un objet porté : le retirer · clic droit sur un anneau ou une relique : l'échanger avec l'autre · "
                + "survol : comparaison (vert : mieux, rouge : moins bien) · hors combat, effet à ta prochaine halte.";
        }

        private void UseBag(int place)
        {
            if (_session == null || place < 0 || place >= _session.World.Inventory.Stacks.Count) return;
            ItemDefinition item = _session.Item(_session.World.Inventory.Stacks[place].Item);
            if (item.Type == ItemType.Equipment) _session.Equip(place);
        }

        private void RefreshCharacter(AdventureSession a)
        {
            WorldSession w = a.World;
            CharacterBuild build = w.Build;
            Attributes at = build.Attributes;
            int[] numbers = { at.Force, at.Dexterite, at.Intelligence, at.Vitalite, at.Volonte, at.Endurance };
            for (int i = 0; i < 6; i++) _attributes[i].Value = numbers[i].ToString();
            (long health, long endurance) = a.Maxima;
            _health.Value = health.ToString();
            _endurance.Value = endurance.ToString();
            // Points d'Éclat à placer : recalculés quand le personnage change (le validateur ne tourne pas à chaque image).
            if (!ReferenceEquals(_pointsFor, w.Character))
            {
                _pointsFor = w.Character;
                try
                {
                    _points.Value = a.CreateTalentEditor().PointsRemaining.ToString();
                }
                catch (System.ArgumentException)
                {
                    _points.Value = "–";
                }
            }
            foreach (KeyValuePair<string, Cell> entry in _slots)
            {
                Cell cell = entry.Value;
                cell.Label.Value = a.Text("text.ui.slot." + entry.Key);
                bool worn = w.Inventory.Equipped.TryGetValue(entry.Key, out string id);
                ItemDefinition item = worn ? a.Item(id) : null;
                cell.Icon.sprite = item != null ? UiKit.Icon(item.Icon) : null;
                UiKit.Show(cell.Icon, cell.Icon.sprite != null);
                cell.Name.Value = item != null ? item.Name : a.Text("text.ui.empty_slot");
                cell.Name.Color = item != null ? RarityColor(item.Rarity) : UiKit.Muted;
                cell.Edge.color = item != null ? RarityColor(item.Rarity) : UiKit.Alpha(Color.white, 0.5f);
                cell.Count.Value = "";
            }
            IReadOnlyList<ItemStack> stacks = w.Inventory.Stacks;
            _bagTitle.Value = $"Sac · {stacks.Count} / {w.Inventory.Catalog.InventorySize}";
            RefreshPurse(a);
            RefreshBagTools(a);   // sous-lot V1.2 : filtre du sac
            for (int c = 0; c < _bag.Count; c++)
            {
                Cell cell = _bag[c];
                int i = BagPlace(c);
                bool used = i >= 0 && i < stacks.Count && i < w.Inventory.Catalog.InventorySize;
                UiKit.Show(cell.Rect, c < w.Inventory.Catalog.InventorySize);
                ItemDefinition item = used ? a.Item(stacks[i].Item) : null;
                cell.Icon.sprite = item != null ? UiKit.Icon(item.Icon) : null;
                UiKit.Show(cell.Icon, cell.Icon.sprite != null);
                cell.Count.Value = used && stacks[i].Count > 1 ? stacks[i].Count.ToString() : "";
                bool wearable = item != null && item.Type == ItemType.Equipment;
                cell.Edge.color = item == null ? UiKit.Alpha(Color.white, 0.35f) : wearable ? RarityColor(item.Rarity) : UiKit.Alpha(Color.white, 0.6f);
                cell.Icon.color = item != null && wearable && w.Level < item.RequiredLevel ? new Color(0.55f, 0.45f, 0.45f) : Color.white;
            }
            long xp = w.Character.Xp;
            (long from, long? to) = a.LevelXp;
            _xpValue.Value = to is long next ? $"{xp} / {next}" : xp.ToString();
            _xpBar.fillAmount = to is long top && top > from ? Mathf.Clamp01((xp - from) / (float)(top - from)) : 1f;
            _xpNote.Value = w.Level > w.EnteredLevel ? $"Niveau {w.Level} à ta prochaine halte (en combat : niveau {w.EnteredLevel})."
                : to != null ? $"Niveau {w.Level} · niveau {w.Level + 1} à {to} points d'expérience." : $"Niveau {w.Level} (maximal).";
            IReadOnlyList<(string Name, int Level, long Xp)> jobs = a.Professions();
            for (int i = 0; i < _professions.Count; i++)
            {
                bool used = i < jobs.Count;
                UiKit.Show(_professions[i].Card, used);
                if (!used) continue;
                _professions[i].Name.Value = jobs[i].Name;
                _professions[i].Level.Value = $"Niveau {jobs[i].Level} · {jobs[i].Xp} pts";
                _professions[i].Icon.sprite = UiKit.Icon(ProfessionIcon(jobs[i].Name));
            }
            RefreshTip(a);
        }

        private static string ProfessionIcon(string name)
        {
            string n = (name ?? "").ToLowerInvariant();
            if (n.Contains("forge")) return "marteau_du_carrier";
            if (n.Contains("herb")) return "herbe_de_l_aube";
            if (n.Contains("extract") || n.Contains("min")) return "minerai_de_fer";
            return "coffre";
        }

        public static Color RarityColor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Rare: return new Color(0.56f, 0.76f, 1f);
                case ItemRarity.VeryRare: return UiKit.Dawn;
                case ItemRarity.Epic: return new Color(0.89f, 0.71f, 1f);
                case ItemRarity.Legendary: return new Color(1f, 0.62f, 0.36f);
                default: return new Color(0.78f, 0.82f, 0.87f);
            }
        }

        private static string RarityName(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Rare: return "rare";
                case ItemRarity.VeryRare: return "très rare";
                case ItemRarity.Epic: return "épique";
                case ItemRarity.Legendary: return "légendaire";
                default: return "commun";
            }
        }

        private static string ShortId(string id) => string.IsNullOrEmpty(id) ? id : id.Substring(id.LastIndexOf('.') + 1);

        // --- Fiche d'un objet ------------------------------------------------------------------------------------------------------

        private void BuildTip()
        {
            _tip = UiKit.Node(_panel, "Fiche de l'objet");
            UiKit.Place(_tip, Vector2.zero, new Vector2(TipWidth, 210f), Vector2.zero);
            UiKit.Fill(UiKit.Image(_tip, "Dalle", UiKit.Sprite("dalle", 40f), Color.white, Image.Type.Sliced).rectTransform);
            _tipIcon = UiKit.Image(_tip, "Icône", null, Color.white);
            UiKit.Place(_tipIcon.rectTransform, new Vector2(22f, 22f), new Vector2(56f, 56f), Vector2.zero);
            _tipName = Text(_tip, UiFace.Title, 20f, Color.white, TextAnchor.MiddleLeft, 90f, 36f, 270f, 26f, 0f, 0.5f, 2f);
            _tipKind = Text(_tip, UiFace.Bold, 13f, UiKit.Muted, TextAnchor.MiddleLeft, 90f, 62f, 270f, 18f, 0f, 0.5f, 10f, true);
            _tipLines = Text(_tip, UiFace.Body, 17f, UiKit.Ink, TextAnchor.UpperLeft, 24f, 92f, 330f, 80f, 0f, 0f);
            _tipLines.Wrap();
            _tipAction = Text(_tip, UiFace.Bold, 14f, UiKit.Teal, TextAnchor.MiddleLeft, 24f, 186f, 330f, 20f, 0f, 0.5f, 8f, true);
            BuildCompare();   // sous-lot V1.2 : comparaison
            UiKit.Show(_tip, false);
        }

        /// <summary>Sous-lot V1.2 : fiche élargie pour la comparaison.</summary>
        private const float TipWidth = 470f;

        private void ShowTip(string item, RectTransform anchor, bool worn, string wornPlace = null)
        {
            _tipItem = item;
            _tipAnchor = anchor;
            _tipWorn = worn;
            _tipPlace = worn ? wornPlace : null;
            if (_session != null) RefreshTip(_session);
        }

        private bool _tipWorn;
        private string _tipPlace;

        private void HideTip(RectTransform anchor)
        {
            if (_tipAnchor != anchor) return;
            _tipItem = null;
            _tipAnchor = null;
            UiKit.Show(_tip, false);
        }

        private void RefreshTip(AdventureSession a)
        {
            if (_tipItem == null || _tipAnchor == null)
            {
                UiKit.Show(_tip, false);
                return;
            }
            ItemDefinition item = a.Item(_tipItem);
            UiKit.Show(_tip, true);
            _tip.SetAsLastSibling();
            _tipIcon.sprite = UiKit.Icon(item.Icon);
            _tipName.Value = item.Name;
            _tipName.Color = RarityColor(item.Rarity);
            string kind = item.Type == ItemType.Equipment ? a.Text("text.ui.slot." + item.Slot) : item.Heal > 0 ? "Soin" : "Matériau";
            _tipKind.Value = kind + " · " + RarityName(item.Rarity);
            var lines = new List<string>();
            // Sous-lot V1.2 : un équipement montre sa comparaison avec ce qui est porté à sa place (vert ou rouge, avec les chiffres).
            float compared = RefreshCompare(a, item.Type == ItemType.Equipment ? item.Id : null, _tipPlace, 92f);
            GearBonus b = compared >= 0f ? GearBonus.None : item.Bonus;
            void Add(int value, string label)
            {
                if (value != 0) lines.Add((value > 0 ? "+" : "") + value + " " + label);
            }
            Add(b.PhysicalPower, "puissance physique");
            Add(b.AgilePower, "puissance agile");
            Add(b.MagicPower, "puissance magique");
            Add(b.Armor, "armure");
            Add(b.MagicResistance, "résistance magique");
            Add(b.MaxHealth, "PV maximaux");
            Add(b.MaxEndurance, "endurance maximale");
            Add(b.StatusResistance, "résistance aux altérations");
            if (item.Heal > 0) lines.Add($"Rend {item.Heal} PV (P en combat)");
            _tipLines.Value = string.Join("\n", lines);
            _tipLines.Color = item.Type == ItemType.Equipment && a.World.Level < item.RequiredLevel ? UiKit.Endurance : UiKit.Ink;
            _tipAction.Value = item.Type != ItemType.Equipment ? "" : _tipWorn ? "Clic : retirer" : "Clic : porter";
            float height = compared >= 0f ? 92f + compared + 44f : 210f;
            UiKit.Place(_tipAction.Rect, new Vector2(24f, height - 24f), new Vector2(TipWidth - 48f, 20f), new Vector2(0f, 0.5f));
            // À droite de la case, ou à sa gauche près du bord du panneau.
            Vector2 p = _tipAnchor.anchoredPosition;   // coin haut gauche de la case (origine en haut à gauche du panneau)
            float x = p.x + _tipAnchor.sizeDelta.x + 12f;
            if (x + TipWidth > PanelWidth - 20f) x = p.x - TipWidth - 12f;
            float y = Mathf.Min(-p.y, PanelHeight - height - 20f);
            UiKit.Place(_tip, new Vector2(x, y), new Vector2(TipWidth, height), Vector2.zero);
        }

        // --- Journal ---------------------------------------------------------------------------------------------------------------

        private void BuildJournal(RectTransform page)
        {
            Text(page, UiFace.Bold, 15f, UiKit.Muted, TextAnchor.MiddleLeft, 60f, 168f, 400f, 20f, 0f, 0.5f, 14f, true).Value = "Quêtes";
            _journalEmpty = Text(page, UiFace.Body, 19f, UiKit.Muted, TextAnchor.UpperLeft, 60f, 196f, 800f, 60f, 0f, 0f);
            _journalEmpty.Wrap();
            for (int i = 0; i < 6; i++)
            {
                RectTransform block = UiKit.Node(page, "Quête");
                UiKit.Place(block, new Vector2(60f, 196f + i * 108f), new Vector2(840f, 100f), Vector2.zero);
                Image edge = UiKit.Image(block, "Liseré", null, UiKit.Alpha(UiKit.Dawn, 0.8f));
                UiKit.Place(edge.rectTransform, new Vector2(0f, 4f), new Vector2(3f, 86f), Vector2.zero);
                UiText name = Text(block, UiFace.Title, 22f, Color.white, TextAnchor.MiddleLeft, 18f, 16f, 800f, 28f, 0f, 0.5f, 2f);
                name.Gold();
                UiText step = Text(block, UiFace.Body, 18f, UiKit.Ink, TextAnchor.UpperLeft, 18f, 34f, 640f, 48f, 0f, 0f);
                step.Wrap();
                Image track = UiKit.Image(block, "Fond", UiKit.Sprite("cible_remplissage"), new Color(0.06f, 0.08f, 0.11f, 1f));
                UiKit.Place(track.rectTransform, new Vector2(680f, 44f), new Vector2(150f, 10f), Vector2.zero);
                Image bar = UiKit.Image(block, "Avancée", UiKit.Sprite("cible_remplissage"), UiKit.Teal, Image.Type.Filled);
                UiKit.Place(bar.rectTransform, new Vector2(680f, 44f), new Vector2(150f, 10f), Vector2.zero);
                bar.fillMethod = Image.FillMethod.Horizontal;
                UiText progress = Text(block, UiFace.Bold, 17f, UiKit.Dawn, TextAnchor.MiddleRight, 830f, 28f, 120f, 22f, 1f, 0.5f);
                _quests.Add((block, name, step, bar, progress));
            }
            _discoveriesTitle = Text(page, UiFace.Bold, 15f, UiKit.Muted, TextAnchor.MiddleLeft, 960f, 168f, 400f, 20f, 0f, 0.5f, 14f, true);
            for (int i = 0; i < 9; i++)
            {
                UiText line = Text(page, UiFace.Body, 17f, UiKit.Ink, TextAnchor.UpperLeft, 960f, 196f + i * 70f, 480f, 64f, 0f, 0f);
                line.Wrap();
                _discoveries.Add(line);
            }
        }

        private void RefreshJournal(AdventureSession a)
        {
            IReadOnlyList<JournalEntry> quests = a.Journal();
            _journalEmpty.Value = quests.Count == 0 ? a.Text("text.ui.journal_empty") : "";
            for (int i = 0; i < _quests.Count; i++)
            {
                bool used = i < quests.Count;
                UiKit.Show(_quests[i].Block, used);
                if (!used) continue;
                JournalEntry q = quests[i];
                _quests[i].Name.Value = q.Name + (q.Done ? " · " + a.Text("text.ui.quest_done_mark") : "");
                _quests[i].Step.Value = q.Step ?? "";
                _quests[i].Progress.Value = q.Progress ?? (q.Done ? "fait" : "");
                _quests[i].Bar.fillAmount = q.Done ? 1f : HudView.Progress(q.Progress);
                UiKit.Show(_quests[i].Bar, q.Progress != null || q.Done);
            }
            IReadOnlyList<string> found = a.Discoveries();
            _discoveriesTitle.Value = a.Text("text.ui.title.discoveries") + " · " + found.Count;
            for (int i = 0; i < _discoveries.Count; i++)
                _discoveries[i].Value = i < found.Count ? "– " + found[i] : "";
        }

        // --- Atelier ---------------------------------------------------------------------------------------------------------------

        private void BuildCrafting(RectTransform page)
        {
            for (int i = 0; i < 8; i++)
            {
                int index = i;
                RectTransform card = UiKit.Node(page, "Recette");
                UiKit.Place(card, new Vector2(60f + (i % 2) * 700f, 160f + (i / 2) * 160f), new Vector2(680f, 148f), Vector2.zero);
                UiKit.Fill(UiKit.Image(card, "Fond", UiKit.Sprite("bouton_sobre", 12f), Color.white, Image.Type.Sliced).rectTransform);
                Image icon = UiKit.Image(card, "Icône", null, Color.white);
                UiKit.Place(icon.rectTransform, new Vector2(18f, 18f), new Vector2(64f, 64f), Vector2.zero);
                UiText name = Text(card, UiFace.Title, 21f, Color.white, TextAnchor.MiddleLeft, 98f, 30f, 560f, 26f, 0f, 0.5f, 2f);
                name.Gold();
                UiText kind = Text(card, UiFace.Bold, 13f, UiKit.Muted, TextAnchor.MiddleLeft, 98f, 56f, 560f, 18f, 0f, 0.5f, 10f, true);
                UiText recipe = Text(card, UiFace.Body, 17f, UiKit.Ink, TextAnchor.UpperLeft, 98f, 70f, 380f, 46f, 0f, 0f);
                recipe.Wrap();
                UiText state = Text(card, UiFace.Body, 15f, UiKit.Teal, TextAnchor.MiddleLeft, 98f, 126f, 380f, 20f, 0f, 0.5f);
                UiButton craft = UiKit.Button(card, "Fabriquer", "bouton", "", UiFace.Bold, 16f, new Color(0.1f, 0.05f, 0.02f), () => Craft(index));
                UiKit.Place(craft.Rect, new Vector2(660f, 132f), new Vector2(170f, 42f), new Vector2(1f, 1f));
                _recipes.Add((card, icon, name, kind, recipe, state, craft));
            }
            _craftProfessions = Text(page, UiFace.Bold, 16f, UiKit.Muted, TextAnchor.MiddleLeft, 60f, 836f, 1380f, 22f, 0f, 0.5f, 6f);
        }

        private void Craft(int index)
        {
            if (_session != null && index < _recipeData.Count) _session.Craft(_recipeData[index].Id);
        }

        private void RefreshCrafting(AdventureSession a)
        {
            _recipeData = a.Recipes();
            for (int i = 0; i < _recipes.Count; i++)
            {
                bool used = i < _recipeData.Count;
                UiKit.Show(_recipes[i].Card, used);
                if (!used) continue;
                RecipeEntry r = _recipeData[i];
                bool possible = r.Refusal == CraftRefusal.None;
                var card = _recipes[i];
                card.Name.Value = r.Name;
                card.Kind.Value = r.Profession + " · " + string.Format(a.Text("text.ui.level_required"), r.RequiredLevel);
                card.Recipe.Value = r.Inputs + "  »  " + r.Output;
                card.State.Value = a.Text("text.ui.craft." + r.Refusal.ToString().ToLowerInvariant());
                card.State.Color = possible ? UiKit.Teal : UiKit.Endurance;
                card.Craft.Text.Value = a.Text("text.ui.craft");
                card.Craft.Enable(possible);
                card.Icon.sprite = UiKit.Icon(ProfessionIcon(r.Profession));
            }
            _craftProfessions.Value = a.Text("text.ui.title.professions") + " : "
                + string.Join("  ·  ", a.Professions().Select(p => $"{p.Name} niveau {p.Level} ({p.Xp} pts)"));
        }

        // --- Carte -----------------------------------------------------------------------------------------------------------------

        private const float MapX = 40f, MapY = 156f, MapW = 1420f, MapH = 640f;

        private void BuildMap(RectTransform page)
        {
            RectTransform area = UiKit.Node(page, "Carte");
            UiKit.Place(area, new Vector2(MapX, MapY), new Vector2(MapW, MapH), Vector2.zero);
            _map = UiKit.Raw(area, "Carte peinte", null, Color.white);
            _mapMarkers = UiKit.Node(area, "Repères");
            UiKit.Place(_mapMarkers, Vector2.zero, new Vector2(MapW, MapH), Vector2.zero);
            _mapYou = UiKit.Image(area, "Toi", UiKit.Sprite("repere_joueur"), Color.white);
            _legend = Text(page, UiFace.Body, 16f, UiKit.Muted, TextAnchor.MiddleCenter, PanelWidth / 2f, 830f, 1400f, 22f, 0.5f, 0.5f);
            BuildTravel(page);   // sous-lot V1.1 : voyage rapide (AdventureBook.Travel.cs)
        }

        private void RefreshMap(AdventureSession a, IReadOnlyList<MapEnemy> enemies)
        {
            AdventureMap map = a.Map;
            Texture2D painted = PaintedMap.For(map);
            float ratio = (float)(map.Width / map.Depth), w = MapW, h = w / ratio;
            if (h > MapH)
            {
                h = MapH;
                w = h * ratio;
            }
            float x0 = (MapW - w) / 2f, y0 = (MapH - h) / 2f;
            UiKit.Place(_map.rectTransform, new Vector2(x0, y0), new Vector2(w, h), Vector2.zero);
            _map.texture = painted;
            _map.color = painted != null ? Color.white : UiKit.Night;
            _map.uvRect = PaintedMap.Area(map);
            _markerCount = 0;
            _nameCount = 0;
            Vector2 At(double x, double z)
            {
                (double u, double v) = map.ToMap(x, z);
                return new Vector2(x0 + (float)u * w, y0 + (float)v * h);
            }
            foreach (MapPlace place in map.Places) Name(At(place.CenterX, place.CenterZ), place.Name, 18f, UiKit.Alpha(Color.white, 0.7f), 0f);
            foreach (MapEnemy e in enemies)
                Marker("repere_ennemi", At(e.X, e.Z), e.Boss ? 20f : 14f, e.Boss ? new Color(0.78f, 0.45f, 1f) : Color.white);
            MapMarker me = null;
            foreach (MapMarker m in a.MapMarkers())
            {
                Vector2 p = At(m.X, m.Z);
                switch (m.Kind)
                {
                    case MapMarkerKind.Player: me = m; continue;
                    case MapMarkerKind.Npc: Marker("repere_rond", p, 14f, Color.white); break;
                    case MapMarkerKind.SafePoint: Marker("repere_rond", p, 16f, UiKit.Teal); break;
                    case MapMarkerKind.Workshop: Marker("repere_rond", p, 15f, UiKit.Dawn); break;
                    case MapMarkerKind.Node: Marker("repere_rond", p, 9f, new Color(0.65f, 0.95f, 0.55f)); continue;
                    case MapMarkerKind.Door: Marker("repere_rond", p, 13f, new Color(0.95f, 0.66f, 0.28f)); break;   // intérieurs : une porte
                    case MapMarkerKind.Objective: Marker("repere_objectif", p, 30f, Color.white); Name(p, m.Name, 16f, UiKit.Dawn, -26f); continue;
                }
                Name(p, m.Name, 15f, UiKit.Ink, 18f);
            }
            for (int i = _markerCount; i < _markerPool.Count; i++) UiKit.Show(_markerPool[i], false);
            for (int i = _nameCount; i < _namePool.Count; i++) _namePool[i].Show(false);
            UiKit.Show(_mapYou, me != null);
            if (me != null)
            {
                Vector2 p = At(me.X, me.Z);
                UiKit.Place(_mapYou.rectTransform, p, new Vector2(22f, 26f), new Vector2(0.5f, 0.5f));
                _mapYou.rectTransform.SetAsLastSibling();
            }
            _legend.Value = "Flèche : toi  ·  losange : ennemi en vue  ·  rond blanc : PNJ  ·  turquoise : point sûr  ·  or : atelier  ·  vert : récolte  ·  "
                + "losange doré : objectif";
            RefreshTravel(a);
        }

        private void Marker(string sprite, Vector2 p, float size, Color color)
        {
            if (_markerCount == _markerPool.Count) _markerPool.Add(UiKit.Image(_mapMarkers, "Repère", null, Color.white));
            Image marker = _markerPool[_markerCount++];
            UiKit.Show(marker, true);
            marker.sprite = UiKit.Sprite(sprite);
            marker.color = color;
            UiKit.Place(marker.rectTransform, p, new Vector2(size, size), new Vector2(0.5f, 0.5f));
        }

        private void Name(Vector2 p, string text, float size, Color color, float dy)
        {
            if (_nameCount == _namePool.Count) _namePool.Add(UiText.Create(_mapMarkers, "Nom", UiFace.Bold, size, color, TextAnchor.MiddleCenter, 4f));
            UiText name = _namePool[_nameCount++];
            name.Show(true);
            name.Size = size;
            name.Color = color;
            name.Value = text;
            UiKit.Place(name.Rect, new Vector2(p.x, p.y + dy), new Vector2(280f, 24f), new Vector2(0.5f, 0.5f));
        }

        // --- Mise à jour -----------------------------------------------------------------------------------------------------------

        /// <summary>Cache les écrans (menus, entraînement, arbre des Éclats).</summary>
        public void Hide()
        {
            LeaveSkills();
            UiKit.Show(_canvas, false);
        }

        /// <summary>Essai réaliste 7 : panneau montré à l'image précédente (son d'onglet quand il change).</summary>
        private AdventureScreens.Panel _shownPanel;

        /// <summary>
        /// Une image : l'écran ouvert (<paramref name="screens"/>) et le dialogue ; rend ce que le joueur demande (fermer, ouvrir l'arbre
        /// des Éclats).
        /// </summary>
        public Request Update(AdventureSession a, AdventureScreens screens, IReadOnlyList<MapEnemy> enemies, float deltaTime)
        {
            _session = a;
            _time += deltaTime;
            bool book = a != null && screens.Open != AdventureScreens.Panel.None;
            bool talk = a != null && a.Dialogue != null;
            UiKit.Show(_canvas, book || talk);
            UiKit.Show(_book, book);
            // Étape 3 : le panneau apparaît en fondu, légèrement grossi. Essai réaliste 7 : il s'ouvre et se ferme en sonnant, et un
            // changement d'onglet aussi.
            if (book && !_bookShown) _fade.alpha = 0f;
            if (book != _bookShown) UiSounds.Play(book ? UiCue.Open : UiCue.Close);
            else if (book && screens.Open != _shownPanel) UiSounds.Play(UiCue.Tab);
            _shownPanel = book ? screens.Open : AdventureScreens.Panel.None;
            _bookShown = book;
            _fade.alpha = Mathf.MoveTowards(_fade.alpha, 1f, deltaTime * 6f);
            float zoom = Mathf.Lerp(0.97f, 1f, _fade.alpha);
            _panel.localScale = new Vector3(zoom, zoom, 1f);
            UiKit.Show(_dialogue, talk);
            if (talk) RefreshDialogue(a);
            if (!book || screens.Open != AdventureScreens.Panel.Skills) LeaveSkills();   // sous-lot V1.5 : glissé, choix et message ne survivent pas à l'écran
            if (book)
            {
                _title.Value = "Le Bastion";
                _subtitle.Value = $"Chevalier gardien · niveau {a.World.Level} · {a.PlaceName}";
                _close.Value = KeyOf(screens.Open) + " · " + a.Text("text.ui.close");
                for (int i = 0; i < _tabs.Count; i++)
                {
                    _tabs[i].Value = a.Text(Tabs[i].Key);
                    bool active = Tabs[i].Panel == screens.Open && Tabs[i].Panel != AdventureScreens.Panel.None;
                    _tabs[i].Color = active ? UiKit.Dawn : UiKit.Muted;
                }
                foreach (KeyValuePair<AdventureScreens.Panel, RectTransform> page in _pages) UiKit.Show(page.Value, page.Key == screens.Open);
                if (screens.Open != AdventureScreens.Panel.Inventory && screens.Open != AdventureScreens.Panel.Merchant) UiKit.Show(_tip, false);
                if (screens.Open == AdventureScreens.Panel.Merchant) RefreshMerchant(a);   // sous-lot V1.1
                switch (screens.Open)
                {
                    case AdventureScreens.Panel.Inventory: RefreshCharacter(a); break;
                    case AdventureScreens.Panel.Journal: RefreshJournal(a); break;
                    case AdventureScreens.Panel.Crafting: RefreshCrafting(a); break;
                    case AdventureScreens.Panel.Map: RefreshMap(a, enemies); break;
                    case AdventureScreens.Panel.Skills: RefreshSkills(a); break;
                }
            }
            if (_pendingTab >= 0)
            {
                int tab = _pendingTab;
                _pendingTab = -1;
                if (Tabs[tab].Panel == AdventureScreens.Panel.None) _request = Request.Talents;
                else screens.Show(Tabs[tab].Panel);
            }
            Request request = _request;
            _request = Request.None;
            return request;
        }

        private int _pendingTab = -1;

        private void Tab(int index) => _pendingTab = index;

        private static string KeyOf(AdventureScreens.Panel panel)
        {
            switch (panel)
            {
                case AdventureScreens.Panel.Inventory: return "I";
                case AdventureScreens.Panel.Journal: return "J";
                case AdventureScreens.Panel.Crafting: return "K";
                case AdventureScreens.Panel.Map: return "L";
                case AdventureScreens.Panel.Skills: return "B";
                default: return "Échap";
            }
        }

        private void RefreshDialogue(AdventureSession a)
        {
            IReadOnlyList<string> answers = a.DialogueAnswers;
            string key = a.DialogueNpc + "|" + a.DialogueText + "|" + string.Join("|", answers);
            if (key == _shownDialogue) return;
            _shownDialogue = key;
            _npc.Value = a.DialogueNpc;
            _speech.Value = a.DialogueText;
            _leave.Text.Value = a.Text("text.ui.close");
            while (_answers.Count < answers.Count)
            {
                int index = _answers.Count;
                UiButton answer = UiKit.Button(_dialogue, "Réponse", "bouton_sobre", "", UiFace.Body, 19f, UiKit.Ink, () => _session?.Choose(index));
                _answers.Add(answer);
            }
            float height = 170f + answers.Count * 50f + 50f;
            _dialogue.sizeDelta = new Vector2(1300f, height);
            for (int i = 0; i < _answers.Count; i++)
            {
                bool used = i < answers.Count;
                UiKit.Show(_answers[i].Rect, used);
                if (!used) continue;
                _answers[i].Text.Value = string.Format(a.Text("text.ui.answer"), i + 1, answers[i]);
                UiKit.Place(_answers[i].Rect, new Vector2(650f, 160f + i * 50f), new Vector2(1220f, 44f), new Vector2(0.5f, 0f));
            }
            UiKit.Place(_leave.Rect, new Vector2(1260f, height - 54f), new Vector2(220f, 40f), new Vector2(1f, 0f));
        }
    }
}
