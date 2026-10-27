using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EdS.Game
{
    /// <summary>Police d'un texte de l'interface (essai réaliste 6).</summary>
    public enum UiFace
    {
        /// <summary>Cinzel Decorative (titres, noms, chiffres dorés).</summary>
        Title = 0,

        /// <summary>Cinzel (chiffres des jauges).</summary>
        Number = 1,

        /// <summary>Barlow Condensed demi-gras (texte courant, étiquettes).</summary>
        Body = 2,

        /// <summary>Barlow Condensed gras (touches, valeurs).</summary>
        Bold = 3,
    }

    /// <summary>
    /// Essai réaliste 6 : polices de l'interface. TextMeshPro (texte net à toute taille, contour et ombre douce) quand ses ressources
    /// sont dans le projet (importées par le menu de préparation) ; sinon le texte ordinaire de uGUI, pour que l'interface s'affiche
    /// toujours.
    /// </summary>
    public static class UiFonts
    {
        private static readonly string[] Files = { "EdsInterface/CinzelDecorative-Bold", "EdsInterface/Cinzel", "EdsInterface/BarlowCondensed-SemiBold", "EdsInterface/BarlowCondensed-Bold" };
        private static readonly Dictionary<UiFace, Font> Legacy = new Dictionary<UiFace, Font>();
        private static readonly Dictionary<UiFace, TMP_FontAsset> Assets = new Dictionary<UiFace, TMP_FontAsset>();
        private static int _tmp = -1;

        /// <summary>Nom du shader SDF de TextMeshPro employé par les polices créées au lancement.</summary>
        public const string TmpShader = "TextMeshPro/Mobile/Distance Field";

        /// <summary>TextMeshPro utilisable : réglages et shader présents (ressources essentielles importées).</summary>
        public static bool UseTmp
        {
            get
            {
                if (_tmp < 0)
                {
                    bool ready = false;
                    try
                    {
                        ready = Resources.Load<TMP_Settings>("TMP Settings") != null && Shader.Find(TmpShader) != null;
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning("Interface : TextMeshPro indisponible, texte ordinaire. " + e.Message);
                    }
                    _tmp = ready ? 1 : 0;
                    if (!ready) Debug.Log("Interface : ressources de TextMeshPro absentes (menu Éclats du Seuil → Préparer l'interface) ; texte ordinaire.");
                }
                return _tmp == 1;
            }
        }

        /// <summary>Police ordinaire (fichier TTF de <c>Resources</c>), ou celle de Unity si le fichier manque.</summary>
        public static Font Font(UiFace face)
        {
            if (Legacy.TryGetValue(face, out Font known)) return known;
            Font font = Resources.Load<Font>(Files[(int)face]) ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Legacy[face] = font;
            return font;
        }

        /// <summary>Police TextMeshPro créée au lancement d'après le fichier TTF (null si impossible : le texte ordinaire prend le relais).</summary>
        public static TMP_FontAsset Asset(UiFace face)
        {
            if (Assets.TryGetValue(face, out TMP_FontAsset known)) return known;
            TMP_FontAsset asset = null;
            try
            {
                Font font = Font(face);
                if (font != null) asset = TMP_FontAsset.CreateFontAsset(font);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Interface : police TextMeshPro impossible (" + face + "). " + e.Message);
            }
            Assets[face] = asset;
            return asset;
        }
    }

    /// <summary>
    /// Essai réaliste 6 : un texte de l'interface, en TextMeshPro si possible, sinon en texte ordinaire ; mêmes réglages dans les deux cas
    /// (police, taille, couleur, alignement, majuscules, dégradé doré, ombre douce).
    /// </summary>
    public sealed class UiText
    {
        private readonly TextMeshProUGUI _tmp;
        private readonly Text _legacy;
        private readonly bool _upper;
        private string _value = "";

        public RectTransform Rect { get; }

        private UiText(RectTransform rect, TextMeshProUGUI tmp, Text legacy, bool upper)
        {
            Rect = rect;
            _tmp = tmp;
            _legacy = legacy;
            _upper = upper;
        }

        /// <summary>
        /// Texte dans <paramref name="parent"/> : police, taille (pixels de l'écran de référence), couleur, alignement ; <paramref name="spacing"/> :
        /// espacement des lettres (en centièmes de la taille, TextMeshPro seulement) ; <paramref name="upper"/> : en majuscules.
        /// </summary>
        public static UiText Create(Transform parent, string name, UiFace face, float size, Color color, TextAnchor anchor, float spacing = 0f, bool upper = false)
        {
            RectTransform rect = UiKit.Node(parent, name);
            TMP_FontAsset asset = UiFonts.UseTmp ? UiFonts.Asset(face) : null;
            if (asset != null)
            {
                TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
                tmp.font = asset;
                tmp.fontSize = size;
                tmp.color = color;
                tmp.alignment = Align(anchor);
                tmp.characterSpacing = spacing;
                tmp.raycastTarget = false;
                tmp.overflowMode = TextOverflowModes.Overflow;
                tmp.richText = false;   // un nom contenant « < » s'affiche tel quel
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                if (upper) tmp.fontStyle = FontStyles.UpperCase;
                var text = new UiText(rect, tmp, null, false);
                text.Shadow(new Color(0f, 0f, 0f, 0.85f));
                return text;
            }
            Text legacy = rect.gameObject.AddComponent<Text>();
            legacy.font = UiFonts.Font(face);
            legacy.fontSize = Mathf.RoundToInt(size);
            legacy.color = color;
            legacy.alignment = anchor;
            legacy.horizontalOverflow = HorizontalWrapMode.Overflow;
            legacy.verticalOverflow = VerticalWrapMode.Overflow;
            legacy.raycastTarget = false;
            legacy.supportRichText = false;
            UnityEngine.UI.Shadow drop = rect.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            drop.effectColor = new Color(0f, 0f, 0f, 0.85f);
            drop.effectDistance = new Vector2(1.5f, -2f);
            return new UiText(rect, null, legacy, upper);
        }

        private static TextAlignmentOptions Align(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
                default: return TextAlignmentOptions.Center;
            }
        }

        /// <summary>Texte affiché (rien n'est refait s'il ne change pas).</summary>
        public string Value
        {
            get => _value;
            set
            {
                string v = value ?? "";
                if (v == _value) return;
                _value = v;
                if (_tmp != null) _tmp.text = v;
                else _legacy.text = _upper ? v.ToUpperInvariant() : v;
            }
        }

        public Color Color
        {
            get => _tmp != null ? _tmp.color : _legacy.color;
            set
            {
                if (_tmp != null) _tmp.color = value;
                else _legacy.color = value;
            }
        }

        public float Size
        {
            set
            {
                if (_tmp != null) _tmp.fontSize = value;
                else _legacy.fontSize = Mathf.RoundToInt(value);
            }
        }

        /// <summary>Dégradé vertical (du haut vers le bas) : or clair vers or sombre, par défaut.</summary>
        public UiText Gradient(Color top, Color bottom)
        {
            if (_tmp != null)
            {
                _tmp.color = Color.white;
                _tmp.enableVertexGradient = true;
                _tmp.colorGradient = new VertexGradient(top, top, bottom, bottom);
            }
            else
            {
                _legacy.color = Color.white;
                UiGradient gradient = Rect.gameObject.AddComponent<UiGradient>();
                gradient.Top = top;
                gradient.Bottom = bottom;
            }
            return this;
        }

        /// <summary>Or des titres (Cinzel Decorative) : dégradé et contour sombre.</summary>
        public UiText Gold() => Gradient(new Color(1f, 0.97f, 0.85f), new Color(0.79f, 0.56f, 0.18f)).Outline(new Color(0.02f, 0.03f, 0.04f, 1f), 0.12f);

        /// <summary>Contour (épaisseur relative à la taille en TextMeshPro, en pixels pour le texte ordinaire).</summary>
        public UiText Outline(Color color, float width)
        {
            if (_tmp != null)
            {
                _tmp.outlineColor = color;
                _tmp.outlineWidth = width;
            }
            else
            {
                UnityEngine.UI.Outline outline = Rect.gameObject.AddComponent<UnityEngine.UI.Outline>();
                outline.effectColor = color;
                outline.effectDistance = new Vector2(1f, -1f);
            }
            return this;
        }

        /// <summary>Ombre douce sous le texte (TextMeshPro : « underlay » du shader ; texte ordinaire : ombre portée nette).</summary>
        public UiText Shadow(Color color)
        {
            if (_tmp == null) return this;
            Material material = _tmp.fontMaterial;
            if (material == null) return this;
            material.EnableKeyword("UNDERLAY_ON");
            material.SetColor("_UnderlayColor", color);
            material.SetFloat("_UnderlayOffsetX", 0.4f);
            material.SetFloat("_UnderlayOffsetY", -0.6f);
            material.SetFloat("_UnderlayDilate", 0.25f);
            material.SetFloat("_UnderlaySoftness", 0.55f);
            return this;
        }

        /// <summary>Retour à la ligne dans la largeur du rectangle (texte sur plusieurs lignes).</summary>
        public UiText Wrap()
        {
            if (_tmp != null) _tmp.textWrappingMode = TextWrappingModes.Normal;
            else _legacy.horizontalOverflow = HorizontalWrapMode.Wrap;
            return this;
        }

        public void Show(bool shown)
        {
            if (Rect.gameObject.activeSelf != shown) Rect.gameObject.SetActive(shown);
        }
    }

    /// <summary>Essai réaliste 6 : dégradé vertical d'un texte ordinaire (quand TextMeshPro manque).</summary>
    public sealed class UiGradient : BaseMeshEffect
    {
        public Color Top = Color.white;
        public Color Bottom = Color.white;

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            var vertex = new UIVertex();
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                min = Mathf.Min(min, vertex.position.y);
                max = Mathf.Max(max, vertex.position.y);
            }
            float span = Mathf.Max(0.001f, max - min);
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                Color c = Color.Lerp(Bottom, Top, (vertex.position.y - min) / span);
                Color original = vertex.color;
                vertex.color = new Color(c.r * original.r, c.g * original.g, c.b * original.b, c.a * original.a);
                vh.SetUIVertex(vertex, i);
            }
        }
    }
}
