using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V2.55 (sujet 13, « L'âme du jeu » : écran titre vivant) : l'écran titre au lancement, après l'ouverture du logo (V2.54). Les règles
    /// (décor de la saison et de l'heure du monde avec fondu, Envers, placement du personnage et du logo dans le repère des peintures, fondus) sont dans
    /// le cœur (<see cref="TitleScreen"/>, testé) ; ici, seulement l'affichage : le décor (deux peintures mélangées, dérive très lente), le héros du
    /// joueur (son vrai modèle, rendu par une caméra à part dans une texture transparente, posé au point des pieds de la peinture, éclairé de la teinte du
    /// décor ; il reste dans sa pose de repos), le logo avec la devise en haut à droite, et l'invite. Un geste lance le jeu (fondu de sortie).
    /// Le jeu attend dessous, gelé. Sans les peintures, un fond sombre ; sans héros, le décor seul. Aucune allocation par image (textures en cache,
    /// rectangles et couleurs par valeur) ; temps d'affichage réel.
    /// </summary>
    public sealed class TitleView
    {
        /// <summary>Couche d'Unity prise par le héros pendant l'écran titre (la caméra du héros ne voit que celle-ci) ; rendue ensuite à ses couches.</summary>
        private const int HeroLayer = 31;

        private bool _pending;
        private float _shown = -1f, _leaving = -1f;
        private TitleMix _mix;
        private Transform _hero;
        private Transform[] _heroParts;
        private int[] _heroLayers;
        private GameObject _rig;
        private Camera _camera;
        private Light _light;
        private RenderTexture _texture;

        /// <summary>L'écran titre n'est pas terminé (il couvre le jeu, qui attend).</summary>
        public bool Pending => _pending;

        /// <summary>Prépare l'écran : le jeu attend jusqu'à ce que le joueur le lance.</summary>
        public void Begin() => _pending = true;

        /// <summary>Annule l'écran (pas de partie à montrer, ou démarrage en erreur).</summary>
        public void Cancel()
        {
            _pending = false;
            Hide();
        }

        /// <summary>
        /// Avance l'écran. <paramref name="canShow"/> : les données sont prêtes et le logo d'ouverture est fini ; <paramref name="hero"/> : le héros du
        /// joueur (null : sans héros) ; <paramref name="mix"/> : le décor du moment.
        /// </summary>
        public void Tick(float unscaledDelta, bool canShow, Transform hero, TitleMix mix)
        {
            if (!_pending) return;
            _mix = mix;
            if (_shown < 0f && canShow)
            {
                _shown = 0f;
                SetUpHero(hero);
            }
            if (_shown < 0f) return;
            _shown += unscaledDelta;
            if (_leaving >= 0f)
            {
                _leaving += unscaledDelta;
                if (TitleScreen.Finished(_leaving)) Cancel();
            }
            else if (TitleScreen.GestureStarts(_shown) && (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.touchCount > 0)) _leaving = 0f;
            UpdateLight();
        }

        /// <summary>Dessine l'écran (espace virtuel du HUD, hauteur 1080) au-dessus du jeu, sous le logo d'ouverture.</summary>
        public void Draw()
        {
            if (!_pending) return;
            float width = Hud.Width, height = Hud.VirtualHeight;
            Rect full = new Rect(0f, 0f, width, height);
            // Avant l'apparition : un fond sombre (le logo d'ouverture est dessus).
            if (_shown < 0f)
            {
                Hud.Fill(full, new Color(0.04f, 0.035f, 0.06f, 1f));
                return;
            }
            float alpha = TitleScreen.Opacity(_shown, _leaving);
            DeviceTier tier = DeviceQuality.Tier;
            Hud.Fill(full, new Color(0.04f, 0.035f, 0.06f, alpha));
            Color previous = GUI.color;
            TitleScreen.Cover(width, height, out float scale, out float ox, out float oy);
            float drift = TitleScreen.Drift(_shown);
            float w = TitleScreen.ReferenceWidth * scale * drift, h = TitleScreen.ReferenceHeight * scale * drift;
            Rect back = new Rect(ox + (TitleScreen.ReferenceWidth * scale - w) * 0.5f, oy + (TitleScreen.ReferenceHeight * scale - h) * 0.5f, w, h);
            Texture2D first = UiKit.Texture(TitleScreen.ResourcePath(_mix.Primary, tier));
            GUI.color = new Color(1f, 1f, 1f, alpha);
            if (first != null) GUI.DrawTexture(back, first);
            if (_mix.Mix > 0f)
            {
                Texture2D second = UiKit.Texture(TitleScreen.ResourcePath(_mix.Secondary, tier));
                if (second != null)
                {
                    GUI.color = new Color(1f, 1f, 1f, alpha * _mix.Mix);
                    GUI.DrawTexture(back, second);
                }
            }
            // Le héros, dans l'enveloppe prévue par la peinture (la dérive du décor est trop lente pour le décoller du sol).
            if (_texture != null)
            {
                TitleRect r = TitleScreen.Place(TitleScreen.HeroX, TitleScreen.HeroY, TitleScreen.HeroWidth, TitleScreen.HeroHeight, width, height);
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.DrawTexture(new Rect(r.X, r.Y, r.Width, r.Height), _texture);
            }
            // Le logo avec la devise, en haut à droite (jamais plus étroit que 420 pixels d'écran).
            TitleRect l = TitleScreen.LogoRect(width, height, 420f / Hud.Scale);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            LogoView.DrawStatic(new Rect(l.X, l.Y, l.Width, l.Height), false);
            GUI.color = previous;
            // L'invite, en bas de la bande de sécurité, qui pulse doucement.
            float y = Mathf.Min(height - 130f, oy + TitleScreen.SafeBottom * scale - 110f);
            float pulse = TitleScreen.PromptPulse(_shown);
            Hud.Centered(new Rect(0f, y, width, 56f), Application.isMobilePlatform ? "Touchez l'écran" : "Appuyez sur une touche",
                new Color(0.96f, 0.92f, 0.82f, alpha * pulse));
        }

        private void SetUpHero(Transform hero)
        {
            if (hero == null) return;
            _hero = hero;
            _heroParts = hero.GetComponentsInChildren<Transform>(true);
            _heroLayers = new int[_heroParts.Length];
            for (int i = 0; i < _heroParts.Length; i++)
            {
                _heroLayers[i] = _heroParts[i].gameObject.layer;
                _heroParts[i].gameObject.layer = HeroLayer;
            }
            bool phone = DeviceQuality.Tier != DeviceTier.Full;
            // Rapport de l'enveloppe (640 × 720) ; un peu moins de pixels sur téléphone et PC modeste.
            _texture = phone ? new RenderTexture(320, 360, 16) : new RenderTexture(640, 720, 24);
            _rig = new GameObject("Écran titre : caméra du héros");
            _camera = _rig.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _camera.cullingMask = 1 << HeroLayer;
            _camera.targetTexture = _texture;
            _camera.fieldOfView = 30f;
            _camera.nearClipPlane = 0.3f;
            _camera.farClipPlane = 20f;
            // Trois quarts, de face, à 3,7 m : le héros (1,8 m) remplit l'enveloppe, les pieds en bas.
            Vector3 feet = hero.position;
            Vector3 front = (hero.forward * 0.9f + hero.right * 0.45f).normalized;
            _rig.transform.position = feet + front * 3.7f + Vector3.up * 1.0f;
            _rig.transform.LookAt(feet + Vector3.up * 0.95f);
            // La lumière du décor, sur le héros seulement (le jeu est caché dessous).
            _light = _rig.AddComponent<Light>();
            _light.type = LightType.Directional;
            _light.shadows = LightShadows.None;
            _rig.transform.LookAt(feet + Vector3.up * 0.95f);
            UpdateLight();
        }

        private void UpdateLight()
        {
            if (_light == null) return;
            TitleScreen.LightFor(_mix, out float r, out float g, out float b, out float intensity);
            _light.color = new Color(r, g, b, 1f);
            _light.intensity = intensity;
        }

        private void Hide()
        {
            if (_heroParts != null)
                for (int i = 0; i < _heroParts.Length; i++)
                    if (_heroParts[i] != null) _heroParts[i].gameObject.layer = _heroLayers[i];
            _heroParts = null;
            _heroLayers = null;
            _hero = null;
            if (_rig != null) Object.Destroy(_rig);
            _rig = null;
            _camera = null;
            _light = null;
            if (_texture != null)
            {
                _texture.Release();
                Object.Destroy(_texture);
            }
            _texture = null;
        }
    }
}
