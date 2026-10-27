using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Builds;
using EdS.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace EdS.Game
{
    /// <summary>
    /// Essai réaliste 6, étape 2 (essai de Damien du 27 octobre 2026 : « il y a aussi l'arbre des compétences a retravailler mais je ne peux pas
    /// l'ouvrir ») : l'« Arbre des Éclats » en uGUI, ouvrable dans l'aventure (T). Une constellation par arbre : une graine de lumière en
    /// bas, les talents en éclats hexagonaux (icônes peintes, rangs en losanges), la chaîne qui monte vers le talent majeur, rond, entouré
    /// de l'anneau des points placés dans l'arbre. Clic : un point ; clic droit : un point de moins ; au doigt, les boutons + et − de la
    /// fiche du talent. Tout passe par <see cref="TalentEditor"/> (donc par le validateur unique) : un point refusé ne change rien et sa
    /// raison s'affiche. « Valider » garde les talents (effet à la prochaine halte, comme l'équipement) ; « Annuler » ou T ferme sans rien
    /// garder.
    /// </summary>
    public sealed class TalentTreeView
    {
        public enum Result { None, Validated, Cancelled }

        private const float NodeSize = 92f, MajorSize = 120f, ColumnGap = 560f;

        private sealed class Node
        {
            public TalentDefinition Talent;
            public TalentTreeDefinition Tree;
            public RectTransform Rect;
            public Image Glow, Icon, Frame, Ring;
            public readonly List<Image> Pips = new List<Image>();
            public UiText Name;
            public Vector2 Center;
            public float Size;
            public float RingShown;
        }

        private sealed class Link
        {
            public Image Line;
            public Node From, To;
            public bool FromSeed;
        }

        private sealed class Column
        {
            public TalentTreeDefinition Tree;
            public UiText Name, Sub, Major;
            public Image Seed;
        }

        private readonly Canvas _canvas;
        private readonly RectTransform _content;
        private readonly List<Node> _nodes = new List<Node>();
        private readonly List<Link> _links = new List<Link>();
        private readonly List<Column> _columns = new List<Column>();
        private readonly UiText _title, _header, _message, _close;
        private readonly RectTransform _detail;
        private readonly Image _detailIcon;
        private readonly UiText _detailName, _detailRank, _detailText, _detailRule;
        private readonly UiButton _plus, _minus, _validate, _cancel, _reset;

        private TalentEditor _editor;
        private System.Func<string, string> _text;
        private System.Func<CharacterBuild, string> _apply;
        private CharacterBuild _shownFor;

        // Points admis en plus ou en moins pour chaque talent, recalculés quand le build change (le validateur ne tourne pas à chaque image).
        private readonly Dictionary<string, bool> _addable = new Dictionary<string, bool>();
        private readonly Dictionary<string, bool> _removable = new Dictionary<string, bool>();
        private Node _hover, _selected;
        private Result _result;
        private float _time;

        private TalentTreeView()
        {
            UiKit.EnsureEvents();
            _canvas = UiKit.CreateCanvas("Arbre des Éclats", 20);
            Transform root = _canvas.transform;
            Image dim = UiKit.Image(root, "Voile", null, new Color(0.02f, 0.03f, 0.05f, 0.9f));
            UiKit.Fill(dim.rectTransform);
            dim.raycastTarget = true;   // le jeu derrière ne reçoit pas les clics
            Image veil = UiKit.Image(root, "Lueur", UiKit.Sprite("braise"), UiKit.Alpha(UiKit.Rose, 0.08f));
            UiKit.Fill(veil.rectTransform, -200f);
            _content = UiKit.Node(root, "Contenu");
            _content.anchorMin = new Vector2(0.5f, 0.5f);
            _content.anchorMax = new Vector2(0.5f, 0.5f);
            _content.pivot = new Vector2(0.5f, 0.5f);
            _content.sizeDelta = new Vector2(UiKit.ReferenceWidth, UiKit.ReferenceHeight);
            _content.anchoredPosition = Vector2.zero;

            _title = UiText.Create(_content, "Titre", UiFace.Title, 50f, Color.white, TextAnchor.MiddleCenter, 4f);
            Place(_title.Rect, 960f, 58f, 1200f, 64f);
            _title.Gold();
            _header = UiText.Create(_content, "Niveau et points", UiFace.Bold, 19f, UiKit.Dawn, TextAnchor.MiddleCenter, 14f, true);
            Place(_header.Rect, 960f, 106f, 1400f, 26f);
            _header.Gradient(UiKit.Dawn, UiKit.Teal);
            _close = UiText.Create(_content, "Fermer", UiFace.Bold, 15f, UiKit.Muted, TextAnchor.MiddleRight, 12f, true);
            Place(_close.Rect, 1760f, 40f, 320f, 22f);
            _message = UiText.Create(_content, "Message", UiFace.Body, 18f, UiKit.Ink, TextAnchor.MiddleCenter);
            Place(_message.Rect, 960f, 884f, 1500f, 26f);

            _detail = UiKit.Node(_content, "Fiche du talent");
            Place(_detail, 960f, 958f, 1180f, 100f);
            UiKit.Fill(UiKit.Image(_detail, "Dalle", UiKit.Sprite("dalle", 40f), Color.white, Image.Type.Sliced).rectTransform);
            RectTransform iconMask = UiKit.Mask(_detail, "Forme", UiKit.Sprite("noeud_masque"));
            UiKit.Place(iconMask, new Vector2(28f, 14f), new Vector2(72f, 72f), Vector2.zero);
            _detailIcon = UiKit.Image(iconMask, "Icône", null, Color.white);
            UiKit.Fill(_detailIcon.rectTransform);
            _detailName = UiText.Create(_detail, "Nom", UiFace.Title, 22f, Color.white, TextAnchor.MiddleLeft, 2f);
            UiKit.Place(_detailName.Rect, new Vector2(118f, 12f), new Vector2(520f, 28f), Vector2.zero);
            _detailName.Gold();
            _detailRank = UiText.Create(_detail, "Rang", UiFace.Bold, 15f, UiKit.Muted, TextAnchor.MiddleLeft, 10f, true);
            UiKit.Place(_detailRank.Rect, new Vector2(640f, 14f), new Vector2(300f, 24f), Vector2.zero);
            _detailText = UiText.Create(_detail, "Effet", UiFace.Body, 17f, UiKit.Ink, TextAnchor.UpperLeft);
            UiKit.Place(_detailText.Rect, new Vector2(118f, 42f), new Vector2(820f, 44f), Vector2.zero);
            _detailText.Wrap();
            _detailRule = UiText.Create(_detail, "Condition", UiFace.Bold, 14f, UiKit.Endurance, TextAnchor.MiddleLeft, 6f);
            UiKit.Place(_detailRule.Rect, new Vector2(118f, 78f), new Vector2(820f, 20f), Vector2.zero);
            _minus = UiKit.Button(_detail, "Retirer", "bouton_sobre", "−", UiFace.Title, 26f, UiKit.Ink, () => Change(_selected, -1));
            UiKit.Place(_minus.Rect, new Vector2(1000f, 28f), new Vector2(70f, 44f), Vector2.zero);
            _plus = UiKit.Button(_detail, "Placer", "bouton", "+", UiFace.Title, 26f, new Color(0.1f, 0.05f, 0.02f), () => Change(_selected, +1));
            UiKit.Place(_plus.Rect, new Vector2(1080f, 28f), new Vector2(70f, 44f), Vector2.zero);

            _validate = UiKit.Button(_content, "Valider", "bouton", "", UiFace.Bold, 18f, new Color(0.1f, 0.05f, 0.02f), Validate);
            Place(_validate.Rect, 830f, 1040f, 300f, 46f);
            _cancel = UiKit.Button(_content, "Annuler", "bouton_sobre", "", UiFace.Bold, 18f, UiKit.Ink, () => _result = Result.Cancelled);
            Place(_cancel.Rect, 1080f, 1040f, 180f, 46f);
            _reset = UiKit.Button(_content, "Tout retirer", "bouton_sobre", "", UiFace.Bold, 16f, UiKit.Muted, Reset);
            Place(_reset.Rect, 1300f, 1040f, 200f, 46f);
            UiKit.Show(_canvas, false);
        }

        public static TalentTreeView Create() => new TalentTreeView();

        public bool IsOpen => _editor != null;

        /// <summary>Place un rectangle par son centre, en pixels de l'écran de référence (1920 × 1080, origine en haut à gauche).</summary>
        private static RectTransform Place(RectTransform rect, float x, float y, float w, float h) =>
            UiKit.Place(rect, new Vector2(x, y), new Vector2(w, h), new Vector2(0.5f, 0.5f));

        /// <summary>
        /// Ouvre l'arbre sur <paramref name="editor"/> (talents du personnage à son niveau) ; <paramref name="text"/> : textes du monde ;
        /// <paramref name="apply"/> : garde le build validé (null si c'est fait, sinon le motif du refus).
        /// </summary>
        public void Open(TalentEditor editor, System.Func<string, string> text, System.Func<CharacterBuild, string> apply)
        {
            _editor = editor;
            _text = text;
            _apply = apply;
            _result = Result.None;
            _shownFor = null;
            _hover = null;
            _selected = null;
            Build();
            _title.Value = text("text.ui.talents.title");
            _close.Value = "T · " + text("text.ui.close");
            _validate.Text.Value = text("text.ui.talents.validate");
            _cancel.Text.Value = text("text.ui.talents.cancel");
            _reset.Text.Value = text("text.ui.talents.reset");
            Say(text("text.ui.talents.controls") + " · " + text("text.ui.talents.next_rest"), false);
            _selected = _nodes.FirstOrDefault();
            UiKit.Show(_canvas, true);
        }

        public void Close()
        {
            _editor = null;
            UiKit.Show(_canvas, false);
        }

        /// <summary>Une image : effets, état des talents ; rend ce que le joueur a décidé (une fois), puis l'arbre se ferme.</summary>
        public Result Update(float deltaTime)
        {
            if (_editor == null) return Result.None;
            _time += deltaTime;
            if (!ReferenceEquals(_shownFor, _editor.Build)) Refresh();
            Animate(deltaTime);
            Result result = _result;
            _result = Result.None;
            if (result != Result.None) Close();
            return result;
        }

        /// <summary>Annule et ferme (T, Échap).</summary>
        public void Cancel() => _result = Result.Cancelled;

        // --- Construction ----------------------------------------------------------------------------------------------------------

        private void Build()
        {
            foreach (Node n in _nodes) Object.Destroy(n.Rect.gameObject);
            foreach (Link l in _links) Object.Destroy(l.Line.gameObject);
            foreach (Column c in _columns)
            {
                Object.Destroy(c.Name.Rect.gameObject);
                Object.Destroy(c.Sub.Rect.gameObject);
                Object.Destroy(c.Major.Rect.gameObject);
                Object.Destroy(c.Seed.gameObject);
            }
            _nodes.Clear();
            _links.Clear();
            _columns.Clear();
            IReadOnlyList<TalentTreeDefinition> trees = _editor.Trees;
            for (int i = 0; i < trees.Count; i++)
            {
                float cx = 960f + (i - (trees.Count - 1) / 2f) * ColumnGap;
                BuildTree(trees[i], cx);
            }
            // La fiche et les boutons restent au-dessus des éclats.
            _detail.SetAsLastSibling();
            _validate.Rect.SetAsLastSibling();
            _cancel.Rect.SetAsLastSibling();
            _reset.Rect.SetAsLastSibling();
        }

        /// <summary>
        /// Places d'un arbre : le majeur en haut, la chaîne de ses prérequis en dessous (du plus proche au plus lointain), les autres talents
        /// de part et d'autre, la graine en bas ; chaque talent sans prérequis part de la graine.
        /// </summary>
        private void BuildTree(TalentTreeDefinition tree, float cx)
        {
            var column = new Column { Tree = tree };
            column.Name = UiText.Create(_content, "Arbre", UiFace.Title, 32f, Color.white, TextAnchor.MiddleCenter, 3f);
            Place(column.Name.Rect, cx, 166f, 520f, 40f);
            column.Name.Gold();
            column.Name.Value = tree.Name;
            column.Sub = UiText.Create(_content, "Points", UiFace.Bold, 15f, UiKit.Muted, TextAnchor.MiddleCenter, 12f, true);
            Place(column.Sub.Rect, cx, 200f, 520f, 22f);
            column.Major = UiText.Create(_content, "Majeur", UiFace.Bold, 14f, UiKit.Muted, TextAnchor.MiddleCenter, 12f, true);
            Place(column.Major.Rect, cx, 236f, 520f, 20f);
            column.Seed = UiKit.Image(_content, "Graine", UiKit.Sprite("graine"), Color.white);
            Place(column.Seed.rectTransform, cx, 840f, 56f, 56f);
            _columns.Add(column);

            List<TalentDefinition> talents = tree.Talents.ToList();
            TalentDefinition major = talents.FirstOrDefault(t => t.IsMajor);
            var chain = new List<TalentDefinition>();
            for (TalentDefinition t = major; t != null && !chain.Contains(t); )
            {
                chain.Add(t);
                string required = t.Prerequisites.Select(p => p.TalentId).FirstOrDefault();
                t = required == null ? null : talents.FirstOrDefault(x => x.Id == required);
            }
            var others = talents.Where(t => !chain.Contains(t)).ToList();
            var places = new Dictionary<TalentDefinition, Vector2>();
            float[] chainY = { 340f, 530f, 700f, 790f };
            for (int i = 0; i < chain.Count; i++) places[chain[i]] = new Vector2(cx, chainY[System.Math.Min(i, chainY.Length - 1)] + (i >= chainY.Length ? 60f * (i - chainY.Length + 1) : 0f));
            Vector2[] around = { new Vector2(cx - 165f, 660f), new Vector2(cx + 165f, 660f), new Vector2(cx + 160f, 480f), new Vector2(cx - 160f, 480f) };
            for (int i = 0; i < others.Count; i++)
                places[others[i]] = i < around.Length ? around[i] : new Vector2(cx - 220f + 110f * (i - around.Length), 780f);

            var nodes = new Dictionary<string, Node>();
            foreach (TalentDefinition t in talents) nodes[t.Id] = BuildNode(tree, t, places[t]);
            // Liens : chaque prérequis, et la graine vers chaque talent sans prérequis (dessinés sous les éclats).
            foreach (TalentDefinition t in talents)
            {
                if (t.Prerequisites.Count == 0) AddLink(null, nodes[t.Id], new Vector2(cx, 840f));
                foreach (TalentRequirement r in t.Prerequisites)
                    if (nodes.TryGetValue(r.TalentId, out Node from)) AddLink(from, nodes[t.Id], from.Center);
            }
            foreach (Node n in nodes.Values) n.Rect.SetAsLastSibling();
            column.Seed.rectTransform.SetAsLastSibling();
        }

        private void AddLink(Node from, Node to, Vector2 start)
        {
            Image line = UiKit.Image(_content, "Lien", null, Color.white);
            Vector2 end = to.Center, d = end - start;
            float length = Mathf.Sqrt(d.x * d.x + d.y * d.y);
            Place(line.rectTransform, (start.x + end.x) / 2f, (start.y + end.y) / 2f, length, 3f);
            line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            _links.Add(new Link { Line = line, From = from, To = to, FromSeed = from == null });
        }

        private Node BuildNode(TalentTreeDefinition tree, TalentDefinition talent, Vector2 center)
        {
            bool major = talent.IsMajor;
            float size = major ? MajorSize : NodeSize;
            var node = new Node { Talent = talent, Tree = tree, Center = center, Size = size };
            node.Rect = UiKit.Node(_content, talent.Name);
            Place(node.Rect, center.x, center.y, size, size);
            node.Glow = UiKit.Image(node.Rect, "Lueur", UiKit.Sprite("braise"), UiKit.Alpha(UiKit.Dawn, 0f));
            UiKit.Fill(node.Glow.rectTransform, -size * 0.45f);
            if (major)
            {
                node.Ring = UiKit.Image(node.Rect, "Anneau", UiKit.Sprite("anneau_progression"), UiKit.Rose, Image.Type.Filled);
                UiKit.Fill(node.Ring.rectTransform, -18f);
                node.Ring.fillMethod = Image.FillMethod.Radial360;
                node.Ring.fillOrigin = (int)Image.Origin360.Top;
                node.Ring.fillClockwise = true;
                node.Ring.fillAmount = 0f;
            }
            RectTransform mask = UiKit.Fill(UiKit.Mask(node.Rect, "Forme", UiKit.Sprite(major ? "majeur_masque" : "noeud_masque")));
            UiKit.Fill(UiKit.Image(mask, "Fond", UiKit.Sprite("noeud_fond"), Color.white).rectTransform);
            node.Icon = UiKit.Image(mask, "Icône", UiKit.Icon("t_" + ShortId(talent.Id)), Color.white);
            UiKit.Fill(node.Icon.rectTransform, major ? 10f : 6f);
            node.Frame = UiKit.Image(node.Rect, "Cadre", UiKit.Sprite(major ? "majeur_cadre" : "noeud_cadre_sobre"), Color.white);
            UiKit.Fill(node.Frame.rectTransform, major ? -18f : -12f);
            int pips = System.Math.Min(talent.MaxRank, 5);
            for (int i = 0; i < pips && !major; i++)
            {
                Image pip = UiKit.Image(node.Rect, "Rang", UiKit.Sprite("pip_off"), Color.white);
                UiKit.Place(pip.rectTransform, new Vector2(size / 2f + (i - (pips - 1) / 2f) * 15f, size + 10f), new Vector2(13f, 13f), new Vector2(0.5f, 0.5f));
                node.Pips.Add(pip);
            }
            node.Name = UiText.Create(node.Rect, "Nom", UiFace.Bold, major ? 19f : 16f, UiKit.Ink, TextAnchor.MiddleCenter, 4f);
            UiKit.Place(node.Name.Rect, new Vector2(size / 2f, size + (major ? 34f : 30f)), new Vector2(240f, 22f), new Vector2(0.5f, 0.5f));
            node.Name.Value = talent.Name;
            // Toute la place de l'éclat est cliquable (image transparente).
            Image hit = UiKit.Image(node.Rect, "Zone", null, new Color(1f, 1f, 1f, 0f));
            UiKit.Fill(hit.rectTransform, -6f);
            UiPointer pointer = UiKit.Pointer(hit);
            pointer.Left = () =>
            {
                _selected = node;
                Change(node, +1);
            };
            pointer.Right = () =>
            {
                _selected = node;
                Change(node, -1);
            };
            pointer.Enter = () => _hover = node;
            pointer.Exit = () =>
            {
                if (_hover == node) _hover = null;
            };
            _nodes.Add(node);
            return node;
        }

        private static string ShortId(string id) => id.Substring(id.LastIndexOf('.') + 1);

        // --- Actions ---------------------------------------------------------------------------------------------------------------

        private void Change(Node node, int delta)
        {
            if (_editor == null || node == null) return;
            BuildViolation why = delta > 0 ? _editor.Add(node.Talent.Id) : _editor.Remove(node.Talent.Id);
            if (why != null) Say(why.Message, true);
            else Say(string.Format(_text("text.ui.talents.rank"), node.Talent.Name, _editor.Rank(node.Talent.Id), node.Talent.MaxRank), false);
        }

        private void Reset()
        {
            if (_editor == null) return;
            BuildViolation why = _editor.Clear();
            Say(why != null ? why.Message : _text("text.ui.talents.controls"), why != null);
        }

        private void Validate()
        {
            if (_editor == null) return;
            string refusal = _apply(_editor.Build);
            if (refusal == null) _result = Result.Validated;
            else Say(refusal, true);
        }

        private void Say(string message, bool error)
        {
            _message.Value = message;
            _message.Color = error ? UiKit.Danger : UiKit.Ink;
        }

        // --- État ------------------------------------------------------------------------------------------------------------------

        private void Refresh()
        {
            _shownFor = _editor.Build;
            _header.Value = string.Format(_text("text.ui.talents.header"), _editor.Level, _editor.PointsRemaining);
            foreach (Column column in _columns)
            {
                long points = _editor.PointsInTree(column.Tree);
                column.Sub.Value = (_text("text.tree." + ShortId(column.Tree.Id)) is string sub && !sub.StartsWith("text.") ? sub + " · " : "")
                    + string.Format(_text("text.ui.talents.tree_points"), points);
                column.Major.Value = string.Format(_text("text.ui.talents.major"), points, _editor.MajorTalentThreshold);
            }
            foreach (Node node in _nodes)
            {
                int rank = _editor.Rank(node.Talent.Id);
                bool addable = _editor.WhyNotAdd(node.Talent.Id) == null;
                _addable[node.Talent.Id] = addable;
                _removable[node.Talent.Id] = _editor.WhyNotRemove(node.Talent.Id) == null;
                node.Icon.color = rank > 0 ? Color.white : addable ? new Color(0.78f, 0.8f, 0.85f) : new Color(0.32f, 0.34f, 0.4f);
                if (!node.Talent.IsMajor) node.Frame.sprite = UiKit.Sprite(rank > 0 ? "noeud_cadre" : "noeud_cadre_sobre");
                node.Frame.color = rank > 0 || addable ? Color.white : new Color(1f, 1f, 1f, 0.55f);
                for (int i = 0; i < node.Pips.Count; i++) node.Pips[i].sprite = UiKit.Sprite(i < rank ? "pip_on" : "pip_off");
                node.Name.Color = rank > 0 ? UiKit.Dawn : addable ? UiKit.Ink : UiKit.Muted;
            }
            foreach (Link link in _links)
            {
                bool lit = _editor.Rank(link.To.Talent.Id) > 0 && (link.FromSeed || _editor.Rank(link.From.Talent.Id) > 0);
                link.Line.color = lit ? UiKit.Alpha(UiKit.Dawn, 0.9f) : UiKit.Alpha(UiKit.Muted, 0.22f);
                link.Line.rectTransform.sizeDelta = new Vector2(link.Line.rectTransform.sizeDelta.x, lit ? 3.5f : 2f);
            }
            _validate.Enable(true);
            _reset.Enable(_editor.PointsSpent > 0);
        }

        private void Animate(float deltaTime)
        {
            foreach (Column column in _columns)
            {
                float pulse = 0.85f + 0.15f * Mathf.Sin(_time * 2.4f + column.Tree.Id.Length);
                column.Seed.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
            }
            foreach (Node node in _nodes)
            {
                int rank = _editor.Rank(node.Talent.Id);
                float glow = rank > 0 ? 0.35f + 0.15f * Mathf.Sin(_time * 2f + node.Center.x * 0.01f) : node == (_hover ?? _selected) ? 0.25f : 0f;
                node.Glow.color = UiKit.Alpha(node.Talent.IsMajor ? UiKit.Rose : UiKit.Dawn, glow);
                float scale = node == _hover ? 1.08f : 1f;
                node.Rect.localScale = new Vector3(scale, scale, 1f);
                if (node.Ring != null)
                {
                    float target = Mathf.Clamp01(_editor.PointsInTree(node.Tree) / (float)System.Math.Max(1, _editor.MajorTalentThreshold));
                    node.RingShown = Mathf.MoveTowards(node.RingShown, target, deltaTime * 1.5f);
                    node.Ring.fillAmount = node.RingShown;
                    node.Ring.color = target >= 1f ? UiKit.Teal : UiKit.Rose;
                }
            }
            Detail(_hover ?? _selected);
        }

        /// <summary>Fiche du talent survolé (sinon choisi) : icône, nom, arbre et rang, effet, condition, boutons + et −.</summary>
        private void Detail(Node node)
        {
            UiKit.Show(_detail, node != null);
            if (node == null) return;
            TalentDefinition t = node.Talent;
            int rank = _editor.Rank(t.Id);
            _detailIcon.sprite = UiKit.Icon("t_" + ShortId(t.Id));
            _detailName.Value = t.Name;
            _detailRank.Value = string.Format(_text("text.ui.talents.rank"), node.Tree.Name, rank, t.MaxRank);
            string effect = _text("text.talent." + ShortId(t.Id));
            _detailText.Value = effect.StartsWith("text.") ? "" : effect;
            if (t.IsMajor) _detailRule.Value = string.Format(_text("text.ui.talents.major_rule"), _editor.MajorTalentThreshold);
            else if (t.Prerequisites.Count > 0)
            {
                TalentRequirement r = t.Prerequisites[0];
                string required = _editor.Class.TryGetTalent(r.TalentId, out TalentDefinition other, out _) ? other.Name : r.TalentId;
                _detailRule.Value = string.Format(_text("text.ui.talents.requires"), required, r.MinRank);
                _detailRule.Color = _editor.Rank(r.TalentId) >= r.MinRank ? UiKit.Teal : UiKit.Endurance;
            }
            else _detailRule.Value = "";
            if (t.IsMajor) _detailRule.Color = _editor.PointsInTree(node.Tree) >= _editor.MajorTalentThreshold ? UiKit.Teal : UiKit.Endurance;
            // Les boutons + et − agissent sur le talent choisi (le dernier touché) : grisés s'il ne peut pas changer.
            Node chosen = _selected ?? node;
            _plus.Enable(_addable.TryGetValue(chosen.Talent.Id, out bool add) && add);
            _minus.Enable(_removable.TryGetValue(chosen.Talent.Id, out bool remove) && remove);
        }
    }
}
