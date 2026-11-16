using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V2.53 (sujet 13, « L'âme du jeu ») : l'écran de chargement peint. Le choix de l'image et de la phrase est fait par le cœur
    /// (<see cref="LoadingScreens"/>, testé) ; ici, seulement l'affichage : image plein écran recadrée sans déformation (tout ratio), phrase d'Orsédia
    /// en bas, fine barre de temps, fondu d'entrée et de sortie. Un geste après une seconde l'abrège. Sans l'image, un fond sombre et la phrase.
    /// </summary>
    public sealed class LoadingView
    {
        private LoadingScreen _screen;
        private string _text = "";
        private float _elapsed = -1f;

        /// <summary>L'écran est montré (il couvre le jeu).</summary>
        public bool Active => _elapsed >= 0f && _elapsed < LoadingScreens.HoldSeconds;

        /// <summary>Montre l'écran <paramref name="screen"/> avec la phrase <paramref name="text"/>, depuis le début.</summary>
        public void Start(LoadingScreen screen, string text)
        {
            _screen = screen;
            _text = text ?? "";
            _elapsed = 0f;
        }

        /// <summary>Avance le temps (hors du temps de jeu) ; un geste après une seconde passe au fondu de sortie.</summary>
        public void Tick(float unscaledDelta)
        {
            if (!Active) return;
            _elapsed += unscaledDelta;
            bool gesture = Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.touchCount > 0;
            float exit = LoadingScreens.HoldSeconds - LoadingScreens.FadeSeconds;
            if (gesture && _elapsed > 1f && _elapsed < exit) _elapsed = exit;
        }

        /// <summary>Dessine l'écran (dans l'espace virtuel du HUD, hauteur 1080).</summary>
        public void Draw()
        {
            if (!Active || _screen == null) return;
            float alpha = LoadingScreens.Opacity(_elapsed);
            Rect full = new Rect(0f, 0f, Hud.Width, Hud.VirtualHeight);
            Texture2D image = UiKit.Texture(LoadingScreens.ResourcePath(_screen, DeviceQuality.Tier));
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            if (image != null) GUI.DrawTexture(full, image, ScaleMode.ScaleAndCrop);
            else Hud.Fill(full, new Color(0.05f, 0.04f, 0.07f, alpha));
            GUI.color = previous;
            // Quart inférieur calme : la phrase sur une bande sombre légère, puis la barre.
            Hud.Fill(new Rect(0f, 880f, Hud.Width, 120f), new Color(0f, 0f, 0f, 0.35f * alpha));
            Hud.Centered(new Rect(Hud.Width * 0.1f, 890f, Hud.Width * 0.8f, 100f), Hud.Plain(_text), new Color(0.96f, 0.92f, 0.82f, alpha));
            float width = Mathf.Min(360f, Hud.Width * 0.4f);
            float x = (Hud.Width - width) * 0.5f;
            Hud.Fill(new Rect(x, 1030f, width, 3f), new Color(1f, 1f, 1f, 0.15f * alpha));
            Hud.Fill(new Rect(x, 1030f, width * Mathf.Clamp01(_elapsed / LoadingScreens.HoldSeconds), 3f), UiKit.Alpha(UiKit.Teal, 0.9f * alpha));
        }
    }
}
