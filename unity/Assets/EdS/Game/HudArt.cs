using System.Collections.Generic;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Essai réaliste 5 (demande de Damien du 27 octobre 2026 : « améliorer complètement l'esthétisme de l'interface », style choisi :
    /// Diablo 4, « au lieu de texte, des icônes ») : dessins de l'interface. Police Cinzel (Google Fonts, licence OFL) et icônes de
    /// game-icons.net (Lorc et Delapouite, licence CC BY 3.0) rangées dans <c>Assets/EdS/Interface/Resources/EdsInterface</c> ; cadres
    /// dorés, globes, cases de compétences et recharge en arc faits par le code (textures créées une fois). Sans police ou sans icônes,
    /// l'interface garde la police de Unity et des cases sans dessin.
    /// </summary>
    public static class HudArt
    {
        /// <summary>Or des cadres et des titres.</summary>
        public static readonly Color Gold = new Color(0.86f, 0.71f, 0.42f);

        /// <summary>Texte clair, couleur de parchemin.</summary>
        public static readonly Color Parchment = new Color(0.93f, 0.88f, 0.76f);

        private static bool _loaded;
        private static Font _font;
        private static readonly Dictionary<string, Texture2D> Icons = new Dictionary<string, Texture2D>();
        private static Texture2D _frame, _shade, _slot, _bar, _diamond, _orbBack, _orbFill, _orbGlass, _orbRim, _button, _buttonHover, _buttonDown, _field;
        private static Texture2D[] _wipes;
        private static GUIStyle _frameStyle;

        /// <summary>Police des titres et des chiffres (Cinzel), ou null.</summary>
        public static Font Font
        {
            get
            {
                Load();
                return _font;
            }
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _font = Resources.Load<Font>("EdsInterface/Cinzel");
            foreach (string name in new[] { "frappe_du_rempart", "heurt_de_bouclier", "provocation", "contrecoup", "barriere_d_acier", "onde_de_rupture",
                         "bastion_absolu", "garde", "parade", "competence" })
            {
                Texture2D icon = Resources.Load<Texture2D>("EdsInterface/Icones/" + name);
                if (icon != null) Icons[name] = icon;
            }
            _frame = Make(48, 48, Frame);
            _shade = Make(4, 64, (x, y, w, h) => new Color32(0, 0, 0, (byte)(90 * (1f - y / (float)(h - 1)))));
            _slot = Make(96, 96, SlotFrame);
            _bar = Make(4, 32, (x, y, w, h) =>
            {
                float t = y / (float)(h - 1);
                float light = t > 0.78f && t < 0.88f ? 1f : 0.62f + 0.33f * t;
                byte v = (byte)(255 * Mathf.Clamp01(light));
                return new Color32(v, v, v, 255);
            });
            _diamond = Make(32, 32, (x, y, w, h) =>
            {
                float d = Mathf.Abs(x - 15.5f) + Mathf.Abs(y - 15.5f);
                byte a = (byte)(255 * Mathf.Clamp01(15f - d));
                return new Color32(255, 255, 255, a);
            });
            _orbBack = Make(256, 256, (x, y, w, h) => Orb(x, y, w, (r, nx, ny) =>
                new Color(0.05f + 0.06f * (1f - r), 0.035f + 0.04f * (1f - r), 0.03f + 0.03f * (1f - r), 1f), 0.97f));
            _orbFill = Make(256, 256, (x, y, w, h) => Orb(x, y, w, (r, nx, ny) =>
            {
                // Liquide : plus clair au centre et en haut, assombri sur le bord (verre épais).
                float light = (0.5f + 0.5f * (1f - r * r)) * (0.8f + 0.2f * ny) * (r > 0.8f ? 1f - (r - 0.8f) * 2.2f : 1f);
                return new Color(light, light, light, 1f);
            }, 0.94f));
            _orbGlass = Make(256, 256, (x, y, w, h) => Orb(x, y, w, (r, nx, ny) =>
            {
                // Reflet du verre en haut à gauche, et léger liseré clair sur le bord bas.
                float dx = (nx + 0.32f) / 0.5f, dy = (ny - 0.45f) / 0.3f;
                float spot = Mathf.Clamp01(1f - (dx * dx + dy * dy));
                float rimLight = r > 0.82f && ny < -0.3f ? (r - 0.82f) * 2f : 0f;
                return new Color(1f, 1f, 1f, 0.42f * spot * spot + rimLight * 0.5f);
            }, 0.94f));
            _orbRim = Make(256, 256, OrbRim);
            _wipes = new Texture2D[33];
            for (int i = 0; i <= 32; i++)
            {
                float covered = i / 32f;
                _wipes[i] = Make(64, 64, (x, y, w, h) =>
                {
                    // Recharge : la part sombre recule dans le sens des aiguilles d'une montre depuis midi.
                    float angle = Mathf.Atan2(x - 31.5f, y - 31.5f) / (2f * Mathf.PI);
                    if (angle < 0f) angle += 1f;
                    return angle >= 1f - covered ? new Color32(0, 0, 0, 165) : new Color32(0, 0, 0, 0);
                });
            }
            _button = Make(48, 48, (x, y, w, h) => Button(x, y, w, h, 0f));
            _buttonHover = Make(48, 48, (x, y, w, h) => Button(x, y, w, h, 0.5f));
            _buttonDown = Make(48, 48, (x, y, w, h) => Button(x, y, w, h, 1f));
            _field = Make(48, 48, (x, y, w, h) =>
            {
                int edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(w - 1 - x, h - 1 - y));
                return edge == 0 ? new Color32(150, 122, 76, 230) : new Color32(14, 13, 12, 235);
            });
            _frameStyle = new GUIStyle(GUI.skin.box) { border = new RectOffset(7, 7, 7, 7) };
            _frameStyle.normal.background = _frame;
        }

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

        private static Color32 Frame(int x, int y, int w, int h)
        {
            int edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(w - 1 - x, h - 1 - y));
            if (edge == 0) return new Color32(8, 7, 6, 230);
            if (edge <= 2)
            {
                // Filet d'or, plus clair en haut.
                float t = y / (float)(h - 1);
                return (Color32)new Color(0.5f + 0.25f * t, 0.39f + 0.2f * t, 0.21f + 0.11f * t, 1f);
            }
            if (edge == 3) return new Color32(10, 9, 8, 200);
            if (edge == 5)
            {
                // Second filet, fin et discret.
                return new Color32(120, 96, 58, 110);
            }
            return new Color32(0, 0, 0, 0);
        }

        private static Color32 SlotFrame(int x, int y, int w, int h)
        {
            int edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(w - 1 - x, h - 1 - y));
            if (edge == 0) return new Color32(6, 5, 4, 255);
            if (edge <= 4)
            {
                // Biseau d'or : clair en haut à gauche (y compte vers le haut), sombre en bas à droite.
                float k = y >= x ? 1f : 0.62f;
                return (Color32)new Color(0.72f * k, 0.57f * k, 0.32f * k, 1f);
            }
            if (edge <= 6) return new Color32(5, 4, 3, 255);
            if (edge <= 9) return new Color32(0, 0, 0, (byte)(150 - 40 * (edge - 7)));
            return new Color32(0, 0, 0, 0);
        }

        private delegate Color OrbPaint(float r, float nx, float ny);

        /// <summary>Disque de rayon <paramref name="radius"/> (en part de la demi-taille), bord adouci ; nx, ny : place de -1 à 1, ny vers le haut.</summary>
        private static Color32 Orb(int x, int y, int size, OrbPaint paint, float radius)
        {
            float nx = (x + 0.5f) / size * 2f - 1f, ny = (y + 0.5f) / size * 2f - 1f;
            float r = Mathf.Sqrt(nx * nx + ny * ny);
            float coverage = Mathf.Clamp01((radius - r) * size * 0.5f);
            if (coverage <= 0f) return new Color32(0, 0, 0, 0);
            Color c = paint(r / radius, nx, ny);
            c.a *= coverage;
            return (Color32)c;
        }

        private static Color32 OrbRim(int x, int y, int w, int h)
        {
            float nx = (x + 0.5f) / w * 2f - 1f, ny = (y + 0.5f) / h * 2f - 1f;
            float r = Mathf.Sqrt(nx * nx + ny * ny);
            float inner = 0.9f, outer = 0.995f;
            if (r < inner - 0.02f || r > outer + 0.01f) return new Color32(0, 0, 0, 0);
            float edgeA = Mathf.Clamp01((r - (inner - 0.02f)) * w * 0.5f), edgeB = Mathf.Clamp01((outer + 0.01f - r) * w * 0.5f);
            float t = (r - inner) / (outer - inner);
            // Anneau d'or bombé, éclairé d'en haut à gauche, avec quatre encoches sombres (midi, trois, six, neuf heures).
            float bulge = 1f - Mathf.Abs(t - 0.45f) * 1.6f;
            float light = 0.55f + 0.35f * Mathf.Clamp01((ny - nx) * 0.5f + 0.5f) + 0.25f * Mathf.Clamp01(bulge);
            float angle = Mathf.Atan2(ny, nx) / (Mathf.PI / 2f);
            float notch = Mathf.Abs(angle - Mathf.Round(angle)) < 0.03f ? 0.45f : 1f;
            if (t < 0.08f || t > 0.92f) light *= 0.35f;
            light *= notch;
            return (Color32)new Color(0.8f * light, 0.63f * light, 0.36f * light, edgeA * edgeB);
        }

        private static Color32 Button(int x, int y, int w, int h, float glow)
        {
            int edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(w - 1 - x, h - 1 - y));
            if (edge == 0) return new Color32(6, 5, 4, 255);
            if (edge <= 2)
            {
                float k = 0.7f + 0.3f * glow;
                return (Color32)new Color(0.72f * k + 0.2f * glow, 0.57f * k + 0.15f * glow, 0.32f * k + 0.08f * glow, 1f);
            }
            float t = y / (float)(h - 1);
            float v = 0.06f + 0.07f * t + 0.05f * glow;
            if (edge == 3 && y > h / 2) v += 0.05f;   // reflet sous le filet du haut
            return (Color32)new Color(v * 1.15f, v, v * 0.85f, 0.95f);
        }

        /// <summary>Habille un style de bouton : cadre d'or, survol plus clair, police Cinzel.</summary>
        public static void StyleButton(GUIStyle style)
        {
            Load();
            style.normal.background = _button;
            style.hover.background = _buttonHover;
            style.active.background = _buttonDown;
            style.focused.background = _buttonHover;
            style.border = new RectOffset(6, 6, 6, 6);
            style.normal.textColor = Parchment;
            style.hover.textColor = Color.white;
            style.active.textColor = Gold;
            style.focused.textColor = Color.white;
            if (_font != null) style.font = _font;
        }

        /// <summary>Style des champs de saisie (fond sombre, filet d'or).</summary>
        public static void StyleField(GUIStyle style)
        {
            Load();
            style.normal.background = _field;
            style.focused.background = _field;
            style.hover.background = _field;
            style.border = new RectOffset(2, 2, 2, 2);
            style.normal.textColor = Parchment;
            style.focused.textColor = Color.white;
        }

        /// <summary>Cadre d'or (filets) autour de <paramref name="rect"/>, avec un léger dégradé sombre vers le bas.</summary>
        public static void Frame(Rect rect)
        {
            Load();
            GUI.DrawTexture(rect, _shade);
            GUI.Box(rect, GUIContent.none, _frameStyle);
        }

        /// <summary>Icône d'une compétence (« skill.bastion.frappe_du_rempart » → frappe_du_rempart), sinon l'icône générique, sinon null.</summary>
        public static Texture2D Icon(string skillId)
        {
            Load();
            string name = skillId ?? "";
            int dot = name.LastIndexOf('.');
            if (dot >= 0) name = name.Substring(dot + 1);
            if (Icons.TryGetValue(name, out Texture2D icon)) return icon;
            return Icons.TryGetValue("competence", out Texture2D any) ? any : null;
        }

        /// <summary>Icône teintée, ou rien sans elle.</summary>
        public static void DrawIcon(Rect rect, Texture2D icon, Color tint)
        {
            if (icon == null) return;
            Color previous = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(rect, icon);
            GUI.color = previous;
        }

        /// <summary>
        /// Globe à la Diablo : fond de verre sombre, liquide de couleur <paramref name="liquid"/> rempli à <paramref name="fraction"/> (surface
        /// plate), reflet, anneau d'or ; <paramref name="value"/> écrit au milieu.
        /// </summary>
        public static void Orb(Vector2 center, float radius, float fraction, Color liquid, string value, GUIStyle text)
        {
            Load();
            var rect = new Rect(center.x - radius, center.y - radius, 2f * radius, 2f * radius);
            Color previous = GUI.color;
            GUI.DrawTexture(rect, _orbBack);
            float f = Mathf.Clamp01(fraction);
            if (f > 0f)
            {
                GUI.color = liquid;
                GUI.DrawTextureWithTexCoords(new Rect(rect.x, rect.y + rect.height * (1f - f), rect.width, rect.height * f), _orbFill, new Rect(0f, 0f, 1f, f));
                // Surface du liquide : un filet plus clair qui ondule à peine.
                if (f < 0.985f)
                {
                    float half = Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(1f - 2f * f, 2f))) * radius * 0.94f;
                    float wave = Mathf.Sin(Time.time * 2.2f) * 0.6f;
                    GUI.color = new Color(Mathf.Min(1f, liquid.r * 1.6f + 0.15f), Mathf.Min(1f, liquid.g * 1.6f + 0.15f), Mathf.Min(1f, liquid.b * 1.6f + 0.15f), 0.75f);
                    GUI.DrawTexture(new Rect(center.x - half, rect.y + rect.height * (1f - f) - 1f + wave, 2f * half, 2f), Texture2D.whiteTexture);
                }
            }
            GUI.color = Color.white;
            GUI.DrawTexture(rect, _orbGlass);
            GUI.DrawTexture(new Rect(rect.x - radius * 0.06f, rect.y - radius * 0.06f, rect.width * 1.06f, rect.height * 1.06f), _orbRim);
            GUI.color = previous;
            if (!string.IsNullOrEmpty(value)) Shadowed(new Rect(rect.x, center.y - 16f, rect.width, 32f), value, text, Parchment);
        }

        /// <summary>
        /// Case de compétence : fond sombre, icône, recharge en arc avec le temps restant, cadre d'or, touche dans le coin haut gauche et coût
        /// d'endurance en bas à droite (rouge s'il manque d'endurance).
        /// </summary>
        public static void Slot(Rect rect, Texture2D icon, string key, double remaining, double cooldown, bool ready, bool ultimate, int cost, bool affordable,
            GUIStyle keyStyle, GUIStyle timeStyle)
        {
            Load();
            Hud.Fill(rect, new Color(0.07f, 0.06f, 0.05f, 0.94f));
            float pad = rect.width * 0.14f;
            var inner = new Rect(rect.x + pad, rect.y + pad, rect.width - 2f * pad, rect.height - 2f * pad);
            Color tint = ready ? (ultimate ? new Color(1f, 0.82f, 0.42f) : new Color(0.95f, 0.9f, 0.78f)) : new Color(0.42f, 0.4f, 0.38f);
            if (ready && ultimate)
            {
                // L'ultime prête luit doucement.
                float pulse = 0.25f + 0.15f * Mathf.Sin(Time.time * 3f);
                Hud.Fill(rect, new Color(0.9f, 0.6f, 0.2f, pulse * 0.5f));
            }
            DrawIcon(inner, icon, tint);
            if (remaining > 0 && cooldown > 0)
            {
                int index = Mathf.Clamp(Mathf.RoundToInt((float)(remaining / cooldown) * 32f), 0, 32);
                GUI.DrawTexture(rect, _wipes[index]);
                Shadowed(rect, remaining >= 10 ? ((int)System.Math.Ceiling(remaining)).ToString() : remaining.ToString("0.0"), timeStyle, Color.white);
            }
            GUI.DrawTexture(rect, _slot);
            var keyRect = new Rect(rect.x + 3f, rect.y + 3f, 22f, 22f);
            Hud.Fill(keyRect, new Color(0f, 0f, 0f, 0.7f));
            Shadowed(keyRect, key, keyStyle, Gold);
            if (cost > 0)
                Shadowed(new Rect(rect.x + rect.width - 34f, rect.y + rect.height - 24f, 30f, 20f), cost.ToString(), keyStyle,
                    affordable ? new Color(1f, 0.82f, 0.4f) : new Color(1f, 0.35f, 0.3f));
        }

        /// <summary>Barre fine à la Diablo : creux sombre, remplissage en dégradé de <paramref name="color"/>, filets d'or.</summary>
        public static void Bar(Rect rect, float fraction, Color color)
        {
            Load();
            Hud.Fill(rect, new Color(0.03f, 0.025f, 0.02f, 0.85f));
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, (rect.width - 4f) * Mathf.Clamp01(fraction), rect.height - 4f), _bar);
            GUI.color = previous;
            GUI.Box(rect, GUIContent.none, _frameStyle);
        }

        /// <summary>Losange (charges de Rempart) : plein et coloré s'il est gagné, sombre sinon.</summary>
        public static void Diamond(Rect rect, bool lit, Color color)
        {
            Load();
            Color previous = GUI.color;
            GUI.color = new Color(Gold.r * 0.8f, Gold.g * 0.8f, Gold.b * 0.8f, 1f);
            GUI.DrawTexture(rect, _diamond);
            GUI.color = lit ? color : new Color(0.12f, 0.12f, 0.14f, 1f);
            GUI.DrawTexture(new Rect(rect.x + 3f, rect.y + 3f, rect.width - 6f, rect.height - 6f), _diamond);
            GUI.color = previous;
        }

        /// <summary>Texte avec une ombre portée (lisible sur le décor).</summary>
        public static void Shadowed(Rect rect, string text, GUIStyle style, Color color)
        {
            Color previous = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            GUI.Label(new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height), text, style);
            style.normal.textColor = color;
            GUI.Label(rect, text, style);
            style.normal.textColor = previous;
        }
    }
}
