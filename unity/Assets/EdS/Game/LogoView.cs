using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V2.54 (sujet 13, « L'âme du jeu » : logo vivant) : l'ouverture du logo au lancement. Les règles (durées, révélation de la
    /// fêlure, pulsation, choix de version) sont dans le cœur (<see cref="LogoIntro"/>, testé) ; ici, seulement l'affichage : le logo sans fêlure
    /// (02), puis le calque de la fêlure (03) révélé de haut en bas le long de la fêlure avec une lueur qui pulse, puis la version avec devise (06).
    /// Temps d'affichage (<c>unscaledDeltaTime</c>), aucune allocation par image (textures mises en cache, rectangles et couleurs sont des valeurs).
    /// Un appui après 0,4 s l'abrège par un rattrapage accéléré (rien ne saute). Sans les images, rien n'est montré.
    /// </summary>
    public sealed class LogoView
    {
        private float _elapsed = -1f;
        private bool _skipping;
        private Texture2D _plain, _crack, _motto;
        private string _plainPath, _crackPath, _mottoPath;

        // Chemins de la version fixe, calculés une fois par version (aucune chaîne créée par image).
        private static readonly string[] StaticPaths = new string[7];

        /// <summary>L'ouverture est montrée (elle couvre le jeu, qui attend).</summary>
        public bool Active => LogoIntro.Active(_elapsed);

        /// <summary>Lance l'ouverture depuis le début (les images sont chargées une fois, libérées à la fin).</summary>
        public void Start()
        {
            DeviceTier tier = DeviceQuality.Tier;
            _plainPath = LogoIntro.ResourcePath(LogoVersion.Plain, tier);
            _crackPath = LogoIntro.ResourcePath(LogoVersion.Crack, tier);
            _mottoPath = LogoIntro.ResourcePath(LogoVersion.Motto, tier);
            _plain = UiKit.Texture(_plainPath);
            _crack = UiKit.Texture(_crackPath);
            _motto = UiKit.Texture(_mottoPath);
            _skipping = false;
            // Sans le logo livré, pas d'ouverture (le jeu ne l'attend pas).
            _elapsed = _plain != null && _crack != null ? 0f : -1f;
            if (_elapsed < 0f) Release();
        }

        /// <summary>Avance le temps d'affichage ; un nouvel appui (touche, clic, doigt posé) lance le rattrapage, sans saut d'opacité.</summary>
        public void Tick(float unscaledDelta)
        {
            if (!Active) return;
            if (!_skipping && LogoIntro.CanSkip(_elapsed) && Gestures.Pressed()) _skipping = true;
            _elapsed = LogoIntro.Advance(_elapsed, unscaledDelta, _skipping);
            if (!Active) Release();
        }

        /// <summary>Libère les images de l'ouverture (mémoire rendue en fin d'écran).</summary>
        private void Release()
        {
            _plain = _crack = _motto = null;
            UiKit.Forget(_plainPath);
            UiKit.Forget(_crackPath);
            UiKit.Forget(_mottoPath);
        }

        /// <summary>Libère les images de la version fixe (à la fin de l'écran qui les montrait).</summary>
        public static void ReleaseStatic()
        {
            for (int i = 0; i < StaticPaths.Length; i++)
            {
                UiKit.Forget(StaticPaths[i]);
                StaticPaths[i] = null;
            }
        }

        /// <summary>Dessine l'ouverture (espace virtuel du HUD, hauteur 1080) au-dessus du jeu.</summary>
        public void Draw()
        {
            if (!Active) return;
            float overall = LogoIntro.Overall(_elapsed);
            Rect full = new Rect(0f, 0f, Hud.Width, Hud.VirtualHeight);
            Hud.Fill(full, new Color(0.04f, 0.035f, 0.06f, overall));
            Rect logo = Fit(full);
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, overall);
            GUI.DrawTexture(logo, _plain);
            float reveal = LogoIntro.CrackReveal(_elapsed);
            if (reveal > 0f)
            {
                DrawCrack(logo, reveal, overall);
                // La lueur : le même calque, ajouté faiblement et pulsé (la fêlure paraît respirer).
                float glow = LogoIntro.Glow(_elapsed);
                if (reveal >= 1f)
                {
                    GUI.color = new Color(1f, 1f, 1f, overall * 0.35f * glow);
                    GUI.DrawTexture(logo, _crack);
                }
            }
            float motto = LogoIntro.MottoOpacity(_elapsed);
            Texture2D last = LogoIntro.ShowsMotto(logo.width * Hud.Scale) ? _motto : null;
            if (motto > 0f && last != null)
            {
                GUI.color = new Color(1f, 1f, 1f, overall * motto);
                GUI.DrawTexture(logo, last);
            }
            GUI.color = previous;
        }

        /// <summary>
        /// Logo fixe dans <paramref name="area"/> (menu, petite taille, fond clair) : la version choisie par <see cref="LogoIntro.Fixed"/>,
        /// ajustée sans déformation et centrée. Rien n'est dessiné si l'image manque.
        /// </summary>
        public static void DrawStatic(Rect area, bool lightBackground)
        {
            Rect logo = Fit(area);
            LogoVersion version = LogoIntro.Fixed(lightBackground, logo.width * Hud.Scale);
            string path = StaticPaths[(int)version];
            if (path == null) path = StaticPaths[(int)version] = LogoIntro.ResourcePath(version, DeviceQuality.Tier);
            Texture2D image = UiKit.Texture(path);
            if (image != null) GUI.DrawTexture(logo, image);
        }

        private void DrawCrack(Rect logo, float reveal, float alpha)
        {
            GUI.color = new Color(1f, 1f, 1f, alpha);
            if (reveal >= 1f)
            {
                GUI.DrawTexture(logo, _crack);
                return;
            }
            // Révélation de haut en bas : seule la bande déjà ouverte est dessinée (coordonnées d'image : l'origine est en bas).
            float top = LogoIntro.CrackTop, len = reveal * (LogoIntro.CrackBottom - LogoIntro.CrackTop);
            Rect band = new Rect(logo.x, logo.y + top * logo.height, logo.width, len * logo.height);
            GUI.DrawTextureWithTexCoords(band, _crack, new Rect(0f, 1f - top - len, 1f, len));
        }

        /// <summary>Le plus grand rectangle 2 : 1 dans <paramref name="area"/>, avec 5 % de marge, centré.</summary>
        private static Rect Fit(Rect area)
        {
            float w = Mathf.Min(area.width * 0.9f, area.height * 0.9f * LogoIntro.Aspect);
            float h = w / LogoIntro.Aspect;
            return new Rect(area.x + (area.width - w) * 0.5f, area.y + (area.height - h) * 0.5f, w, h);
        }
    }
}
