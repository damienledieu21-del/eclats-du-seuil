using System.Collections.Generic;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V2.55 (sujet 13, « L'âme du jeu » : écran titre vivant) : l'écran titre au lancement, après l'ouverture du logo (V2.54). Les règles
    /// (décor de la saison et de l'heure du monde avec fondu, Envers, crépuscule, placement du personnage, du logo et du menu, fondus) sont dans le
    /// cœur (<see cref="TitleScreen"/>) et l'état du menu dans <see cref="TitleMenu"/> (testés) ; ici, seulement l'affichage et la lecture des
    /// appuis : le décor (deux peintures mélangées, dérive très lente), le héros du joueur (son vrai modèle, rendu par une caméra à part dans une
    /// texture transparente, éclairé de la teinte du décor par une lumière qui ne touche que lui), le logo avec la devise, et le menu (Continuer s'il y
    /// a une sauvegarde, Nouvelle partie, Réglages, Quitter) au clavier (flèches, Entrée, Échap), à la souris et au doigt. Le jeu attend dessous, inerte.
    /// Aucune allocation par image (chemins calculés une fois, textures en cache) ; les images et la caméra sont libérées à la fin.
    /// </summary>
    public sealed class TitleView
    {
        /// <summary>Couche d'Unity prise par le héros pendant l'écran titre (la caméra et la lumière du héros ne voient que celle-ci) ; rendue ensuite à ses couches.</summary>
        private const int HeroLayer = 31;

        private bool _pending;
        private float _shown = -1f, _leaving = -1f, _rescan;
        private TitleMix _mix;
        private TitleMenu _menu;
        private TitleAction _choice;
        private Vector3 _lastMouse;
        private string _qualityRow = "", _brightnessRow = "";
        private readonly string[] _paths = new string[18];
        private readonly List<Transform> _parts = new List<Transform>();
        private readonly List<int> _layers = new List<int>();
        private Transform _hero;
        private GameObject _rig;
        private Camera _camera;
        private Light _light;
        private RenderTexture _texture;

        /// <summary>L'écran titre n'est pas terminé (il couvre le jeu, qui attend).</summary>
        public bool Pending => _pending;

        /// <summary>Prépare l'écran : le jeu attend jusqu'à ce que le joueur ait choisi.</summary>
        public void Begin() => _pending = true;

        /// <summary>Annule l'écran (pas de partie à montrer, ou démarrage en erreur) et libère tout.</summary>
        public void Cancel()
        {
            _pending = false;
            Hide();
            Release();
        }

        /// <summary>Le choix fait (Continuer ou Nouvelle partie), une seule fois ; <see cref="TitleAction.None"/> sinon.</summary>
        public TitleAction TakeChoice()
        {
            TitleAction c = _choice;
            _choice = TitleAction.None;
            return c;
        }

        /// <summary>
        /// Avance l'écran. <paramref name="canShow"/> : les données sont prêtes et le logo d'ouverture est fini ; <paramref name="hero"/> : le héros du
        /// joueur (null : sans héros) ; <paramref name="mix"/> : le décor du moment ; <paramref name="hasSave"/> : une sauvegarde existe (Continuer).
        /// </summary>
        public void Tick(float unscaledDelta, bool canShow, Transform hero, TitleMix mix, bool hasSave)
        {
            if (!_pending) return;
            _mix = mix;
            if (_shown < 0f && canShow)
            {
                _shown = 0f;
                _menu = new TitleMenu(hasSave, !Application.isMobilePlatform);
                _lastMouse = Input.mousePosition;
                RefreshRows();
                SetUpHero(hero);
            }
            if (_shown < 0f) return;
            _shown += unscaledDelta;
            if (_leaving >= 0f)
            {
                _leaving += unscaledDelta;
                if (TitleScreen.Finished(_leaving)) Cancel();
                return;
            }
            if (TitleScreen.GestureStarts(_shown)) ReadMenu();
            Relayer(unscaledDelta);
            UpdateLight();
        }

        // --- Menu -----------------------------------------------------------------------------------------------------------------

        private void ReadMenu()
        {
            // Souris : la rangée survolée est choisie quand la souris bouge ; un clic la valide. Doigt : un nouvel appui valide la rangée touchée.
            int pressedRow = -1;
            float pressX = 0f;
            bool pressed = false;
            Vector3 mouse = Input.mousePosition;
            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch t = Input.GetTouch(i);
                    if (t.phase != TouchPhase.Began) continue;
                    pressed = true;
                    pressedRow = RowUnder(t.position.x, t.position.y);
                    pressX = t.position.x;
                    break;
                }
            }
            else
            {
                if (mouse.x != _lastMouse.x || mouse.y != _lastMouse.y)
                {
                    int over = RowUnder(mouse.x, mouse.y);
                    if (over >= 0) _menu.Hover(over);
                }
                if (Input.GetMouseButtonDown(0))
                {
                    pressed = true;
                    pressedRow = RowUnder(mouse.x, mouse.y);
                    pressX = mouse.x;
                }
            }
            _lastMouse = mouse;
            if (pressed && pressedRow >= 0)
            {
                _menu.Hover(pressedRow);
                // Réglage de luminosité : la moitié gauche de la rangée diminue, la droite augmente (au doigt, sans flèches de clavier ; revue Codex de la PR #210).
                bool left = _menu.Page == TitlePage.Settings && _menu.ItemAt(pressedRow) == TitleAction.Brightness && PressedLeftHalf(pressX);
                Do(left ? _menu.Adjust(-1) : _menu.Activate());
                return;
            }
            if (Input.GetKeyDown(KeyCode.UpArrow)) _menu.Move(-1);
            else if (Input.GetKeyDown(KeyCode.DownArrow)) _menu.Move(1);
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) Do(_menu.Activate());
            else if (Input.GetKeyDown(KeyCode.LeftArrow)) Do(_menu.Adjust(-1));
            else if (Input.GetKeyDown(KeyCode.RightArrow)) Do(_menu.Adjust(1));
            else if (Input.GetKeyDown(KeyCode.Escape)) _menu.Back();
        }

        /// <summary>Rangée sous un point de l'écran (pixels, origine en bas à gauche) ; -1 si aucune.</summary>
        private int RowUnder(float pixelX, float pixelY)
        {
            float x = pixelX / Hud.Scale, y = (Screen.height - pixelY) / Hud.Scale;
            TitleRect first = TitleScreen.MenuRow(0, Hud.Width, Hud.VirtualHeight);
            if (x < first.X || x > first.X + first.Width) return -1;
            return _menu.RowAt(y, first.Y, first.Height);
        }

        /// <summary>L'appui (pixels) est-il sur la moitié gauche des rangées du menu ?</summary>
        private static bool PressedLeftHalf(float pixelX)
        {
            TitleRect first = TitleScreen.MenuRow(0, Hud.Width, Hud.VirtualHeight);
            return pixelX / Hud.Scale < first.X + first.Width * 0.5f;
        }

        private void Do(TitleAction action)
        {
            switch (action)
            {
                case TitleAction.Continue:
                case TitleAction.NewGame:
                    _choice = action;
                    _leaving = 0f;
                    break;
                case TitleAction.Quit:
                    Application.Quit();
                    break;
                case TitleAction.Textures:
                    DeviceQuality.Cycle();
                    RefreshRows();
                    break;
                case TitleAction.Brightness:
                    BrightnessSetting.Step(_menu.Direction);
                    RefreshRows();
                    break;
            }
        }

        private void RefreshRows()
        {
            _qualityRow = DeviceQuality.Label;   // « Qualité : … » (21 novembre 2026)
            _brightnessRow = "‹   Luminosité : " + BrightnessSetting.Label + "   ›";
        }

        private string RowText(int index)
        {
            TitleAction a = _menu.ItemAt(index);
            if (a == TitleAction.Textures) return _qualityRow;
            if (a == TitleAction.Brightness) return _brightnessRow;
            if (a == TitleAction.Back && _menu.Page == TitlePage.ConfirmNew) return "Non, garder ma partie";
            return TitleMenu.Label(a);
        }

        // --- Dessin ---------------------------------------------------------------------------------------------------------------

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
            Hud.Fill(full, new Color(0.04f, 0.035f, 0.06f, alpha));
            Color previous = GUI.color;
            TitleScreen.Cover(width, height, out float scale, out float ox, out float oy);
            float drift = TitleScreen.Drift(_shown);
            float w = TitleScreen.ReferenceWidth * scale * drift, h = TitleScreen.ReferenceHeight * scale * drift;
            Rect back = new Rect(ox + (TitleScreen.ReferenceWidth * scale - w) * 0.5f, oy + (TitleScreen.ReferenceHeight * scale - h) * 0.5f, w, h);
            Texture2D first = UiKit.Texture(PathOf(_mix.Primary));
            GUI.color = new Color(1f, 1f, 1f, alpha);
            if (first != null) GUI.DrawTexture(back, first);
            if (_mix.Mix > 0f)
            {
                Texture2D second = UiKit.Texture(PathOf(_mix.Secondary));
                if (second != null)
                {
                    GUI.color = new Color(1f, 1f, 1f, alpha * _mix.Mix);
                    GUI.DrawTexture(back, second);
                }
            }
            // Le héros, dans l'enveloppe prévue par la peinture.
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
            DrawMenu(alpha);
        }

        private void DrawMenu(float alpha)
        {
            if (_menu == null) return;
            float width = Hud.Width, height = Hud.VirtualHeight;
            TitleRect top = TitleScreen.MenuRow(0, width, height);
            float rowH = top.Height;
            // Fond sombre derrière le titre de page et les rangées (lisible sur toutes les peintures).
            Hud.Panel(new Rect(top.X - 24f, top.Y - rowH * 0.95f, top.Width + 48f, rowH * (_menu.Count + 1.1f)), new Color(0.04f, 0.035f, 0.06f, 0.55f * alpha));
            if (_menu.Heading.Length > 0)
                Hud.Centered(new Rect(top.X, top.Y - rowH * 0.9f, top.Width, rowH * 0.8f), _menu.Heading, new Color(HudArt.Gold.r, HudArt.Gold.g, HudArt.Gold.b, alpha));
            for (int i = 0; i < _menu.Count; i++)
            {
                TitleRect row = TitleScreen.MenuRow(i, width, height);
                Rect rect = new Rect(row.X, row.Y + 4f, row.Width, row.Height - 8f);
                bool selected = i == _menu.Selected;
                if (selected) Hud.Fill(rect, new Color(HudArt.Gold.r, HudArt.Gold.g, HudArt.Gold.b, 0.2f * alpha));
                Color c = selected ? HudArt.Gold : HudArt.Parchment;
                Hud.Centered(rect, RowText(i), new Color(c.r, c.g, c.b, alpha));
            }
        }

        private string PathOf(TitleDecor decor)
        {
            string path = _paths[decor.Number];
            if (path == null) path = _paths[decor.Number] = TitleScreen.ResourcePath(decor, DeviceQuality.Tier);
            return path;
        }

        /// <summary>Libère les peintures, les images du logo et leurs entrées du cache (mémoire rendue en fin d'écran).</summary>
        private void Release()
        {
            for (int i = 0; i < _paths.Length; i++)
            {
                UiKit.Forget(_paths[i]);
                _paths[i] = null;
            }
            LogoView.ReleaseStatic();
        }

        // --- Héros ----------------------------------------------------------------------------------------------------------------

        private void SetUpHero(Transform hero)
        {
            if (hero == null) return;
            _hero = hero;
            Relayer(0.5f);
            bool light = DeviceQuality.Tier != DeviceTier.Full;
            // Rapport de l'enveloppe (640 × 720) ; un peu moins de pixels sur téléphone et PC modeste.
            _texture = light ? new RenderTexture(320, 360, 16) : new RenderTexture(640, 720, 24);
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
            // ArenaView.Player est le centre de la capsule, FeetOffset (1 m) au-dessus des pieds (revue Codex de la PR #210) : on cadre depuis les pieds.
            Vector3 feet = hero.position - Vector3.up * ArenaView.FeetOffset;
            Vector3 front = (hero.forward * 0.9f + hero.right * 0.45f).normalized;
            _rig.transform.position = feet + front * 3.7f + Vector3.up * 1.0f;
            _rig.transform.LookAt(feet + Vector3.up * 0.95f);
            // La lumière du décor : elle n'éclaire que la couche du héros (le reste du jeu, caché dessous, ne la reçoit pas).
            _light = _rig.AddComponent<Light>();
            _light.type = LightType.Directional;
            _light.shadows = LightShadows.None;
            _light.cullingMask = 1 << HeroLayer;
            UpdateLight();
        }

        /// <summary>
        /// Met sur la couche du héros toutes les parties du modèle, y compris l'équipement ajouté après la mise en place (relu deux fois par
        /// seconde ; une pièce déjà sur la couche n'est pas touchée). <paramref name="delta"/> : temps écoulé depuis la dernière relecture.
        /// </summary>
        private void Relayer(float delta)
        {
            if (_hero == null) return;
            _rescan += delta;
            if (_rescan < 0.5f) return;
            _rescan = 0f;
            Transform[] all = _hero.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                GameObject part = all[i].gameObject;
                if (part.layer == HeroLayer) continue;
                _parts.Add(all[i]);
                _layers.Add(part.layer);
                part.layer = HeroLayer;
            }
        }

        private void UpdateLight()
        {
            if (_light == null) return;
            TitleScreen.LightFor(_mix, out float r, out float g, out float b, out float intensity);
            _light.color = new Color(r, g, b, 1f);
            _light.intensity = intensity;
        }

        /// <summary>Rend ses couches au héros, détruit la caméra, la lumière et la texture. Appelé à la fin de l'écran et à la désactivation du jeu.</summary>
        public void Hide()
        {
            for (int i = 0; i < _parts.Count; i++)
                if (_parts[i] != null) _parts[i].gameObject.layer = _layers[i];
            _parts.Clear();
            _layers.Clear();
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
