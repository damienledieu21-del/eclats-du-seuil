using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V2.54 (sujet 13, « L'âme du jeu » : logo vivant) : l'ouverture du logo au lancement. Les règles (durées, révélation de la
    /// fêlure, pulsation, choix de version) sont dans le cœur (<see cref="LogoIntro"/>, testé) ; ici, seulement l'affichage : le logo sans fêlure
    /// (02), puis le calque de la fêlure (03) révélé de haut en bas le long de la fêlure avec une lueur qui pulse, puis la version avec devise (06).
    /// Temps d'affichage (<c>unscaledDeltaTime</c>), aucune allocation par image (textures mises en cache, rectangles et couleurs sont des valeurs).
    /// Un geste après 0,4 s l'abrège. Sans les images, rien n'est montré.
    /// </summary>
    public sealed class LogoView
    {
        private float _elapsed = -1f;
        private Texture2D _plain, _crack, _motto, _complete;

        /// <summary>L'ouverture est montrée (elle couvre le jeu, qui attend).</summary>
        public bool Active => LogoIntro.Active(_elapsed);

        /// <summary>Lance l'ouverture depuis le début (les images sont chargées une fois).</summary>
        public void Start()
        {
            DeviceTier tier = DeviceQuality.Tier;
            _plain = UiKit.Texture(LogoIntro.ResourcePath(LogoVersion.Plain, tier));
            _crack = UiKit.Texture(LogoIntro.ResourcePath(LogoVersion.Crack, tier));
            _motto = UiKit.Texture(LogoIntro.ResourcePath(LogoVersion.Motto, tier));
            _complete = UiKit.Texture(LogoIntro.ResourcePath(LogoVersion.Complete, tier));
            // Sans le logo livré, pas d'ouverture (le jeu ne l'attend pas).
            _elapsed = _plain != null && _crack != null ? 0f : -1f;
        }

        /// <summary>Avance le temps d'affichage ; un geste (touche, clic, doigt) abrège.</summary>
        public void Tick(float unscaledDelta)
        {
            if (!Active) return;
            _elapsed += unscaledDelta;
            if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.touchCount > 0) _elapsed = LogoIntro.AfterGesture(_elapsed);
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
            Texture2D image = UiKit.Texture(LogoIntro.ResourcePath(version, DeviceQuality.Tier));
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
