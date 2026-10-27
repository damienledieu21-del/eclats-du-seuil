using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EdS.Game
{
    /// <summary>
    /// Essai réaliste 6 (choix de Damien du 27 octobre 2026 : « je veux vraiment le meilleur », interface « Fragments du Seuil ») : outils
    /// de l'interface uGUI construite par le code. Un canevas plein écran (écran de référence 1920 × 1080, mis à l'échelle sur la
    /// hauteur), des images tirées des dessins exportés des maquettes (<c>Resources/EdsInterface/Hud</c>, deux fois la taille d'affichage)
    /// et des icônes peintes (<c>Resources/EdsInterface/Peintes</c>). Coordonnées en pixels de l'écran de référence, origine en haut à
    /// gauche comme l'ancienne interface (IMGUI).
    /// </summary>
    public static class UiKit
    {
        public const float ReferenceWidth = 1920f, ReferenceHeight = 1080f;

        /// <summary>Les dessins sont exportés au double de leur taille d'affichage.</summary>
        private const float SpritePixelsPerUnit = 200f;

        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();

        /// <summary>Couleurs de l'interface : lumière du Seuil (aube : or, rose, turquoise), or fin, texte, gris bleuté.</summary>
        public static readonly Color Dawn = new Color(1f, 0.847f, 0.541f), Rose = new Color(1f, 0.616f, 0.722f), Teal = new Color(0.561f, 0.961f, 0.91f),
            Gold = new Color(0.886f, 0.706f, 0.353f), Ink = new Color(0.933f, 0.953f, 0.973f), Muted = new Color(0.624f, 0.698f, 0.769f),
            Night = new Color(0.043f, 0.063f, 0.094f), Health = new Color(0.89f, 0.196f, 0.29f), Endurance = new Color(1f, 0.706f, 0.247f),
            Resonance = new Color(0.706f, 0.549f, 1f), Danger = new Color(1f, 0.29f, 0.37f);

        /// <summary>Canevas plein écran par-dessus le jeu (sous l'ancienne interface IMGUI, qui reste dessinée au-dessus).</summary>
        public static Canvas CreateCanvas(string name, int order)
        {
            var go = new GameObject(name);
            Object.DontDestroyOnLoad(go);
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;   // même hauteur partout, comme l'écran virtuel de 1080 de haut d'avant
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>Largeur de l'écran de référence à la hauteur 1080 (plus de 1920 sur un écran plus large).</summary>
        public static float Width => Screen.height > 0 ? ReferenceHeight * Screen.width / Screen.height : ReferenceWidth;

        /// <summary>Texture d'un fichier de <c>Resources</c> (null s'il manque), gardée.</summary>
        public static Texture2D Texture(string path)
        {
            if (Textures.TryGetValue(path, out Texture2D known)) return known;
            Texture2D texture = Resources.Load<Texture2D>(path);
            if (texture != null) texture.wrapMode = TextureWrapMode.Clamp;
            Textures[path] = texture;
            return texture;
        }

        /// <summary>
        /// Image d'interface (<c>Resources/EdsInterface/Hud/name</c>) ; <paramref name="border"/> : bords à garder entiers quand on l'étire
        /// (pixels d'affichage), 0 pour une image simple. Null si le fichier manque (le jeu continue sans cette image).
        /// </summary>
        public static Sprite Sprite(string name, float border = 0f)
        {
            string key = name + "|" + border;
            if (Sprites.TryGetValue(key, out Sprite known)) return known;
            Texture2D texture = Texture("EdsInterface/Hud/" + name);
            Sprite sprite = texture == null ? null : UnityEngine.Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f),
                SpritePixelsPerUnit, 0u, SpriteMeshType.FullRect, new Vector4(border, border, border, border) * 2f);
            Sprites[key] = sprite;
            return sprite;
        }

        /// <summary>Icône peinte (<c>Resources/EdsInterface/Peintes/name</c>) ; null si elle manque.</summary>
        public static Sprite Icon(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string key = "icone|" + name;
            if (Sprites.TryGetValue(key, out Sprite known)) return known;
            Texture2D texture = Texture("EdsInterface/Peintes/" + name);
            Sprite sprite = texture == null ? null : UnityEngine.Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            Sprites[key] = sprite;
            return sprite;
        }

        /// <summary>Nœud vide (rectangle) dans <paramref name="parent"/>.</summary>
        public static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>
        /// Place un rectangle de taille <paramref name="size"/> dont le point <paramref name="pivot"/> (0 à 1, origine en haut à gauche)
        /// est en <paramref name="position"/> pixels depuis le coin haut gauche du parent.
        /// </summary>
        public static RectTransform Place(RectTransform rect, Vector2 position, Vector2 size, Vector2 pivot)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(pivot.x, 1f - pivot.y);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(position.x, -position.y);
            return rect;
        }

        /// <summary>Étire un rectangle sur tout son parent (marges en pixels).</summary>
        public static RectTransform Fill(RectTransform rect, float margin = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(margin, margin);
            rect.offsetMax = new Vector2(-margin, -margin);
            return rect;
        }

        /// <summary>Image (sprite) teintée ; type simple, étiré en 9 parties si le sprite a des bords, ou à remplissage.</summary>
        public static Image Image(Transform parent, string name, Sprite sprite, Color color, Image.Type type = UnityEngine.UI.Image.Type.Simple)
        {
            RectTransform rect = Node(parent, name);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = sprite == null ? UnityEngine.UI.Image.Type.Simple : type;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Image brute (texture entière, ou une partie par <see cref="RawImage.uvRect"/>).</summary>
        public static RawImage Raw(Transform parent, string name, Texture texture, Color color)
        {
            RectTransform rect = Node(parent, name);
            RawImage image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// Masque à la forme de <paramref name="sprite"/> (sa transparence) : ses enfants ne se dessinent que dedans ; la forme elle-même
        /// ne se voit pas. Sans dessin, un masque rectangulaire.
        /// </summary>
        public static RectTransform Mask(Transform parent, string name, Sprite sprite)
        {
            if (sprite == null)
            {
                RectTransform plain = Node(parent, name);
                plain.gameObject.AddComponent<RectMask2D>();
                return plain;
            }
            Image shape = Image(parent, name, sprite, Color.white);
            Mask mask = shape.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            return shape.rectTransform;
        }

        /// <summary>Montre ou cache sans détruire (évite un SetActive par image quand rien ne change).</summary>
        public static void Show(Component c, bool shown)
        {
            if (c != null && c.gameObject.activeSelf != shown) c.gameObject.SetActive(shown);
        }

        /// <summary>Couleur avec une autre opacité.</summary>
        public static Color Alpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        /// <summary>
        /// Essai réaliste 6, étape 2 : système d'évènements de uGUI (clics et survol des écrans), créé une fois s'il n'y en a pas ; il lit
        /// la souris et le doigt par l'ancien gestionnaire d'entrées, celui du jeu.
        /// </summary>
        public static void EnsureEvents()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("Évènements de l'interface");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        /// <summary>
        /// Rend <paramref name="target"/> cliquable : clic (gauche ou doigt), clic droit, survol ; le graphisme reçoit les rayons (une image
        /// transparente aussi).
        /// </summary>
        public static UiPointer Pointer(Graphic target)
        {
            target.raycastTarget = true;
            UiPointer pointer = target.gameObject.GetComponent<UiPointer>();
            return pointer != null ? pointer : target.gameObject.AddComponent<UiPointer>();
        }

        /// <summary>Bouton : dessin étiré en 9 parties, texte au milieu, action au clic ; plus clair au survol.</summary>
        public static UiButton Button(Transform parent, string name, string sprite, string label, UiFace face, float size, Color textColor, System.Action click)
        {
            Image plate = Image(parent, name, Sprite(sprite, 12f), Color.white, UnityEngine.UI.Image.Type.Sliced);
            UiText text = UiText.Create(plate.rectTransform, "Texte", face, size, textColor, TextAnchor.MiddleCenter, 10f, true);
            Fill(text.Rect);
            text.Value = label;
            var button = new UiButton(plate, text);
            button.Enable(true);
            UiPointer pointer = Pointer(plate);
            pointer.Left = () =>
            {
                if (button.Enabled) click?.Invoke();
            };
            pointer.Enter = () => button.Hover(true);
            pointer.Exit = () => button.Hover(false);
            return button;
        }
    }

    /// <summary>Essai réaliste 6, étape 2 : clic, clic droit et survol d'un élément de l'interface uGUI.</summary>
    public sealed class UiPointer : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public System.Action Left, Right, Enter, Exit;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right) Right?.Invoke();
            else if (eventData.button == PointerEventData.InputButton.Left) Left?.Invoke();
        }

        public void OnPointerEnter(PointerEventData eventData) => Enter?.Invoke();

        public void OnPointerExit(PointerEventData eventData) => Exit?.Invoke();
    }

    /// <summary>Essai réaliste 6, étape 2 : un bouton de l'interface (plaque, texte, actif ou grisé, éclairci au survol).</summary>
    public sealed class UiButton
    {
        private bool _hover;

        public Image Plate { get; }
        public UiText Text { get; }
        public bool Enabled { get; private set; } = true;
        public RectTransform Rect => Plate.rectTransform;

        public UiButton(Image plate, UiText text)
        {
            Plate = plate;
            Text = text;
        }

        public void Enable(bool enabled)
        {
            Enabled = enabled;
            Paint();
        }

        public void Hover(bool hover)
        {
            _hover = hover;
            Paint();
        }

        // Relecture : uGUI ramène les couleurs à 1 (sommets en Color32) : un peu plus sombre au repos, pleine lumière au survol.
        private void Paint() => Plate.color = !Enabled ? new Color(0.8f, 0.8f, 0.8f, 0.35f) : _hover ? Color.white : new Color(0.84f, 0.84f, 0.86f, 1f);
    }
}
