using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Interface V2.1 (dossier du 12 novembre 2026, charte pages 1 à 5) : matières du HUD dessinées par le code, donc faites pour le jeu
    /// (aucun fichier tiers, aucune licence) : cuir sombre pour les panneaux actifs, filet d'or patiné d'une unité au bord, liseré intérieur
    /// discontinu à 4 unités, coins fixes de 12 (construction 9-slice : le motif de coin ne s'étire pas) ; boutons tactiles en cuir avec
    /// l'état appuyé en bronze ; rail de barre sombre ; remplissage clair à teinter. Pas de halo permanent, pas de texture sous les valeurs.
    /// Couleurs de la charte : suie #171B1B, cuir #342A24, fer #7A827B, or patiné #B59A61, parchemin #E5D8B8, sang #A3433D, or vif #D6AF5F.
    /// </summary>
    public static class UiV21Art
    {
        public static readonly Color Soot = Hex(0x17, 0x1B, 0x1B), Leather = Hex(0x34, 0x2A, 0x24), Iron = Hex(0x7A, 0x82, 0x7B),
            Gold = Hex(0xB5, 0x9A, 0x61), Parchment = Hex(0xE5, 0xD8, 0xB8), Blood = Hex(0xA3, 0x43, 0x3D), BrightGold = Hex(0xD6, 0xAF, 0x5F),
            Stamina = Hex(0xE1, 0xBC, 0x67), Seuil = Hex(0xBB, 0xC2, 0xFF);

        /// <summary>Bord gardé entier des dessins (pixels de la texture = unités du canevas, à 100 pixels par unité de sprite).</summary>
        public const int Corner = 12;

        private const float PixelsPerUnit = 100f;

        private static Texture2D _panel, _button, _buttonDown, _track, _plain, _ring, _knob;
        private static Sprite _panelSprite, _buttonSprite, _trackSprite, _plainSprite;
        private static GUIStyle _panelStyle, _buttonStyle, _buttonDownStyle;

        private static Color Hex(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);

        private delegate Color32 Painter(int x, int y, int w, int h);

        private static Texture2D Make(int w, int h, Painter paint)
        {
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    pixels[y * w + x] = paint(x, y, w, h);
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            texture.SetPixels32(pixels);
            texture.Apply(false);
            return texture;
        }

        /// <summary>Grain du cuir : bruit fixe (hachage des coordonnées), contraste local faible comme le veut la charte.</summary>
        private static float Grain(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h >> 16) & 0xFF) / 255f - 0.5f;
            }
        }

        private static Color32 Lerp(Color a, Color b, float t, float alpha) { Color c = Color.Lerp(a, b, t); c.a = alpha; return c; }

        /// <summary>Cuir à filet d'or : <paramref name="fill"/> teinte du cuir, <paramref name="alpha"/> son opacité.</summary>
        private static Color32 Framed(int x, int y, int w, int h, Color fill, float alpha, Color line)
        {
            int edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(w - 1 - x, h - 1 - y));
            if (edge == 0) return Lerp(line, line, 0f, 1f);
            if (edge == 1) return Lerp(Soot, Soot, 0f, 0.9f);
            if (edge == 4 && ((x + y) / 2) % 2 == 0) return Lerp(line, fill, 0.35f, 0.75f);
            float shade = 0.06f * Grain(x, y) - 0.05f * (1f - y / (float)(h - 1));
            return Lerp(fill, shade > 0 ? Color.white : Color.black, Mathf.Abs(shade), alpha);
        }

        private static void Load()
        {
            if (_panel != null) return;
            int s = 2 * Corner + 16;
            _panel = Make(s, s, (x, y, w, h) => Framed(x, y, w, h, Color.Lerp(Leather, Soot, 0.45f), 0.86f, Gold));
            _button = Make(s, s, (x, y, w, h) => Framed(x, y, w, h, Leather, 0.92f, Gold));
            _buttonDown = Make(s, s, (x, y, w, h) => Framed(x, y, w, h, Color.Lerp(Leather, BrightGold, 0.45f), 0.95f, BrightGold));
            _track = Make(16, 16, (x, y, w, h) =>
            {
                int edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(w - 1 - x, h - 1 - y));
                return edge == 0 ? Lerp(Soot, Soot, 0f, 1f) : Lerp(Soot, Leather, 0.3f, 0.75f);
            });
            _plain = Make(8, 8, (x, y, w, h) =>
            {
                // Remplissage à teinter : un peu plus clair en haut (lecture de la barre), aucune texture.
                float t = 0.82f + 0.18f * (y / 7f);
                return new Color(t, t, t, 1f);
            });
            // Joystick : cercle de cuir sombre cerclé d'or (le pouce lit le bord), bouton central plein.
            _ring = Make(128, 128, (x, y, w, h) => Disc(x, y, w, 0.42f, 3f));
            _knob = Make(64, 64, (x, y, w, h) => Disc(x, y, w, 0.9f, 2f));
            _panelSprite = Sliced(_panel, Corner);
            _buttonSprite = Sliced(_button, Corner);
            _trackSprite = Sliced(_track, 2);
            _plainSprite = Sliced(_plain, 0);
            _panelStyle = Style(_panel);
            _buttonStyle = Style(_button);
            _buttonDownStyle = Style(_buttonDown);
        }

        private static Color32 Disc(int x, int y, int size, float fillAlpha, float rim)
        {
            float c = (size - 1) / 2f, r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)), outer = size / 2f - 0.5f;
            float inside = Mathf.Clamp01(outer - r + 0.5f);
            if (inside <= 0f) return new Color32(0, 0, 0, 0);
            bool edge = r > outer - rim;
            Color col = edge ? Gold : Color.Lerp(Leather, Soot, 0.5f);
            col.a = (edge ? 1f : fillAlpha) * inside;
            return col;
        }

        private static Sprite Sliced(Texture2D texture, int border) =>
            Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0u, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));

        private static GUIStyle Style(Texture2D texture)
        {
            var style = new GUIStyle(GUI.skin.box) { border = new RectOffset(Corner, Corner, Corner, Corner) };
            style.normal.background = texture;
            return style;
        }

        /// <summary>Panneau de HUD (cuir sombre, filet d'or), 9-slice.</summary>
        public static Sprite Panel { get { Load(); return _panelSprite; } }

        /// <summary>Case de compétence ou bouton (cuir, filet d'or), 9-slice.</summary>
        public static Sprite ButtonSprite { get { Load(); return _buttonSprite; } }

        /// <summary>Rail d'une barre (fond sombre, bord de suie).</summary>
        public static Sprite Track { get { Load(); return _trackSprite; } }

        /// <summary>Remplissage clair à teinter (barres, recharge, voile).</summary>
        public static Sprite Plain { get { Load(); return _plainSprite; } }

        /// <summary>IMGUI (commandes au doigt) : panneau de cuir, bouton au repos, bouton appuyé.</summary>
        public static void DrawPanel(Rect rect)
        {
            Load();
            GUI.Box(rect, GUIContent.none, _panelStyle);
        }

        public static void DrawButton(Rect rect, bool pressed)
        {
            Load();
            GUI.Box(rect, GUIContent.none, pressed ? _buttonDownStyle : _buttonStyle);
        }

        /// <summary>IMGUI : cercle du joystick (<paramref name="knob"/> : le bouton central, plein).</summary>
        public static void DrawRing(Rect rect, bool knob)
        {
            Load();
            GUI.DrawTexture(rect, knob ? _knob : _ring);
        }
    }
}
