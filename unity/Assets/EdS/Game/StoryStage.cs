using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Story;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.World;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V1.9.8 (demande de Damien du 5 novembre 2026 : « une ouverture marquante », chapitre 1 enrichi, PR B) : la mise en scène
    /// de l'histoire du chapitre 1, par-dessus l'aventure. Le cœur (<see cref="AdventureSession"/>) reste seul juge : cette classe ne fait
    /// que montrer ce qu'il dit, et n'appelle que ses commandes publiques (<see cref="AdventureSession.EnterScene"/>,
    /// <see cref="AdventureSession.SkipScene"/>, <see cref="AdventureSession.LeaveScene"/>). Aucun fichier payant : formes, lumières, sons
    /// faits par le code.
    /// <list type="bullet">
    /// <item>Le réveil (ouverture « Le réveil », nouvelle partie) : écran noir, titre, première phrase, les yeux qui s'ouvrent ; le personnage
    /// couché sur la place sous une couverture se relève ; puis une bulle de tutoriel par étape, le bouton au doigt de l'étape mis en valeur.</item>
    /// <item>Le souvenir de la Rupture : proposition (revivre ou passer), fondu blanc, ambiance de nuit et de brume
    /// (<see cref="StyleWorld.Memory"/>), chant des Seuillards (fait par le code), la porte de lumière (arche), sept Seuillards, la reine
    /// Ysolde et son linceul, la lune qui se fend quand la porte s'ouvre, l'Écho de la Rupture en silhouette de lumière, Galeran (lance trop
    /// lourde, épaulières neuves), puis le retour au pré en fondu blanc avec le texte du retour.</item>
    /// <item>Les Vallons : Croc-Gris hurle quand il passe à sa seconde forme ; la silhouette en manteau sombre se montre un instant à la
    /// lisière nord du pré ; la pierre gravée au milieu du rond de champignons ; le collier de Croc-Gris porté au cou ; « Quête passée » en
    /// grand.</item>
    /// </list>
    /// </summary>
    public sealed class StoryStage
    {
        // --- Identifiants des données (chapitre 1, sous-lots V1.9.6 et V1.9.7) -----------------------------------------------------

        private const string WakeStep = "odile";
        private const string GateFact = "fact.ch1.souvenir.porte";
        private const string EchoId = "echo_de_la_rupture";
        private const string VoicesId = "voix_de_la_rupture";   // sous-lot V1.9.9 : la seconde forme de l'écho
        private const string YsoldeId = "npc.ysolde";
        private const string RagingCroc = "croc_gris_enrage";
        private const string SilhouetteDiscovery = "discovery.ch1.silhouette";
        private const string SilhouetteText = "text.ch1.discovery.silhouette";
        private const string CollarItem = "item.collier_de_croc_gris";
        private const string ChapterName = "text.quest.traces.name";
        private const string OpeningName = "text.ch1.quest.reveil.name";
        private const string MeadowPlace = "pre_nord";
        private const string Vallons = "zone.vallons";
        private const string PromptTitle = "text.ui.souvenir.prompt";
        private const string PromptCall = "text.ui.souvenir.call";
        private const string SkipHint = "text.ui.reveil.aguerri";

        /// <summary>Étapes de l'ouverture, dans l'ordre (pour « étape 2/6 »).</summary>
        private static readonly string[] OpeningSteps = { "odile", "frapper", "garder", "parer", "boire", "retour" };

        /// <summary>La porte de lumière du souvenir (obstacle <c>autel_porte_de_lumiere</c>) : dessinée en arche, pas en autel.</summary>
        public static bool IsLightGate(string id) => id != null && id.StartsWith("autel_porte_de_lumiere", StringComparison.Ordinal);

        // --- Temps du réveil (secondes) ------------------------------------------------------------------------------------------

        private const float TitleAt = 0.8f, LineAt = 3.6f, EyesAt = 7f, StandAt = 8.6f, WakeEnd = 10.2f;

        private readonly Transform _ear;
        private readonly List<Material> _owned = new List<Material>();
        private string _zone;

        // Réveil.
        private float _wake = -1f;
        private string _wakeLine, _chapter;
        private GameObject _blanket;
        private Vector3 _dropApplied;
        private bool _dropActive;
        private Transform _dropped;

        // Voile blanc (entrée et sortie du souvenir, porte qui s'ouvre).
        private float _white, _whiteTarget, _whiteRate = 1f;

        // Légende (texte du retour, de repli, de la silhouette) et bandeau (« Quête passée »).
        private string _captionTitle, _captionText;
        private float _captionBorn = -99f, _captionFor;
        private string _banner, _bannerDetail;
        private float _bannerBorn = -99f;

        // Tutoriel.
        private string _shownStep, _stepHeader, _stepText, _stepKeys;
        private float _stepBorn = -99f;

        // Souvenir.
        private GameObject _memory;
        private Vector3 _gate;
        private float _gateHalf = 1.8f;
        private bool _gateOpen;
        private float _gateOpenedAt = -99f;
        private Light _gateLight;
        private Material _veil, _floorGlow;
        private Transform _veilBody;
        private readonly List<Transform> _singers = new List<Transform>();
        private readonly List<float> _singerBase = new List<float>();
        private Material _singerRobe;
        private Transform _moon, _moonLeft, _moonRight, _crack;
        private readonly List<(Transform Body, Vector3 Origin)> _mist = new List<(Transform, Vector3)>();
        private Transform _echoHalo;
        private Light _echoLight;
        private readonly List<Transform> _motes = new List<Transform>();
        private bool _sceneDone;
        private float _leaveAt = -1f;
        private string _backKey;
        private string _returnCaption;

        // Galeran et le collier : décor accroché au joueur (refait quand l'arène refait le joueur).
        private GameObject _galeran, _pendant;
        private Transform _galeranOwner, _pendantOwner;

        // Sous-lot V2.11 (5e vague) : Galeran en vrai chevalier (le corps du joueur est caché le temps du souvenir).
        private readonly List<Renderer> _hiddenForGaleran = new List<Renderer>();
        private Animation _galeranAnimation;
        private Transform _galeranModel;
        private Vector3 _galeranLast;
        private float _galeranStep;
        private float _galeranBaseY;   // relecture : la hauteur calée à la pose (pieds au sol), gardée sous le balancement

        // Les Vallons.
        private GameObject _vallons;
        private Material _glyphs;
        private GameObject _silhouette;
        private float _silhouetteBorn;
        private bool _silhouetteKnown;
        private float _discoveryCheck;
        private bool _ragingSeen;
        private float _callAt;

        // Sons faits par le code.
        private AudioSource _choir, _voices, _oneShot;
        private AudioClip _howl;

        private GUIStyle _title, _line, _small, _gold;

        public StoryStage(Transform ear) => _ear = ear;

        // --- Cycle de vie ----------------------------------------------------------------------------------------------------------

        /// <summary>Une aventure s'ouvre : le réveil si le personnage en est à la toute première étape de l'ouverture.</summary>
        public void Begin(AdventureSession a)
        {
            Reset();
            Narrative story = a.World.Narrative;
            QuestStep step = story?.CurrentStep(StoryConversions.Opening);
            // Une seule fois : sur une partie dont l'horloge du monde n'a jamais avancé (nouvelle partie ; le monde attend pendant le
            // réveil). Dès qu'on a joué, la sauvegarde garde un temps du monde positif : le réveil ne se rejoue plus (aucun format changé).
            if (step != null && step.Id == WakeStep && !a.InScene && a.World.Zone.StartWorldTick == 0)
            {
                _wake = 0f;
                _wakeLine = FirstSentence(a.Text(step.Text));
                _chapter = a.Text(ChapterName);
            }
            _silhouetteKnown = story != null && story.Discoveries.Contains(SilhouetteDiscovery);
        }

        /// <summary>L'aventure se ferme (entraînement, réseau) : tout le décor et les sons disparaissent, l'ambiance revient.</summary>
        public void Reset()
        {
            ClearZone();
            StyleWorld.SetMemory(false);
            DestroyGear();
            if (_blanket != null) Object.Destroy(_blanket);
            _blanket = null;
            _dropped = null;
            _dropActive = false;
            _wake = -1f;
            _zone = null;
            _white = _whiteTarget = 0f;
            _captionText = null;
            _banner = null;
            _shownStep = null;
            _ragingSeen = false;
            _returnCaption = null;
            if (_choir != null) _choir.volume = 0f;
            if (_voices != null) _voices.volume = 0f;
        }

        /// <summary>
        /// Une session de zone vient d'être affichée. Si la zone a changé : le décor de la zone (souvenir ou Vallons) est refait ; vrai quand
        /// on entre dans un souvenir ou qu'on en sort (le fondu blanc remplace alors le fondu au noir des portes).
        /// </summary>
        public bool ZoneChanged(AdventureSession a)
        {
            string zone = a.CurrentZone;
            if (zone == _zone) return false;
            _zone = zone;
            ClearZone();
            if (a.InScene)
            {
                StyleWorld.SetMemory(true);
                _backKey = a.CurrentScene?.Back;
                // Relecture de la PR #96 : une partie reprise dans le souvenir déjà fini (l'Écho vaincu, puis quittée) rentre au pré seule.
                _sceneDone = false;
                _leaveAt = a.SceneFinished ? Time.unscaledTime + 1.5f : -1f;
                BuildMemory(a);
                Fade(1f, 0f, 2.8f);
                Caption(a.ZoneName, a.SceneActor, 5f);
                UiSounds.Play(UiCue.Open);
                return true;
            }
            bool fromMemory = StyleWorld.Memory;
            StyleWorld.SetMemory(false);
            if (zone == Vallons) BuildVallons(a);
            if (!fromMemory) return false;
            Fade(1f, 0f, 3.2f);
            string back = _returnCaption ?? (_sceneDone && _backKey != null ? a.Text(_backKey) : null);
            if (back != null) Caption(null, back, 0f);
            _returnCaption = null;
            _sceneDone = false;
            return true;
        }

        /// <summary>Couleur de la forme simple d'un combattant : l'Écho est une silhouette de lumière ; null : la couleur habituelle.</summary>
        public Color? Paint(string id) => id == EchoId || id == VoicesId ? new Color(0.85f, 0.9f, 1f) : (Color?)null;

        /// <summary>Le réveil occupe l'écran : le monde attend (pas de marche ni de combat).</summary>
        public bool Blocking => _wake >= 0f && _wake < StandAt + 0.8f;

        /// <summary>La proposition du souvenir est à l'écran : le curseur doit rester libre pour ses boutons.</summary>
        public bool WantsCursor(AdventureSession a) => a != null && Prompting(a, true);

        /// <summary>Le souvenir se joue sans la musique de l'aventure (le chant des Seuillards), jusqu'à ce que la porte s'ouvre.</summary>
        public bool Hush(AdventureSession a) => a != null && a.InScene && !_gateOpen;

        /// <summary>Nom à montrer au-dessus du personnage pendant le souvenir (« Galeran »), ou null.</summary>
        public string Actor(AdventureSession a) => a != null && a.InScene ? a.SceneActor : null;

        /// <summary>Libellé du bouton au doigt à mettre en valeur pour l'étape de l'ouverture en cours, ou null.</summary>
        public string TouchHighlight(AdventureSession a)
        {
            if (a == null || _wake >= 0f || a.InScene) return null;
            switch (a.World.Narrative?.CurrentStep(StoryConversions.Opening)?.Id)
            {
                case "odile":
                case "retour": return "Agir";
                case "frapper": return "1";
                case "garder": return "Garde";
                case "parer": return "Parade";
                case "boire": return "Potion";
                default: return null;
            }
        }

        // --- Commandes ---------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Touches de la mise en scène, avant le reste de l'image : passer le réveil (n'importe quelle touche, après une seconde), V revivre
        /// le souvenir proposé, X le passer (proposé, ou pendant qu'on le joue). <paramref name="free"/> : aucun dialogue ni écran ouvert.
        /// </summary>
        public void ReadInput(AdventureSession a, bool free, bool calm)
        {
            // Relecture de la PR #96 : Échap met en pause (EdsGame) ; il ne passe pas aussi l'écran noir.
            if (_wake >= 1f && _wake < EyesAt && (Input.anyKeyDown && !Input.GetKeyDown(KeyCode.Escape) || Input.touchCount > 0)) _wake = EyesAt;
            // Pendant le souvenir, X le passe à tout moment, même pendant un dialogue (pas en pause ni sur un écran : <paramref name="calm"/>).
            if (calm && a.InScene && !a.SceneFinished && Input.GetKeyDown(KeyCode.X))
            {
                Skip(a);
                return;
            }
            if (!free || _wake >= 0f && _wake < StandAt) return;
            if (Prompting(a, true))
            {
                if (Input.GetKeyDown(KeyCode.V)) Relive(a);
                else if (Input.GetKeyDown(KeyCode.X)) Skip(a);
            }
        }

        private bool Prompting(AdventureSession a, bool free) =>
            free && a.PendingScene != null && !a.InScene && !a.MustContinue && a.Dialogue == null && (_wake < 0f || _wake >= WakeEnd);

        private void Relive(AdventureSession a)
        {
            // Refusé (ennemi proche, défaite…) : le cœur le dit (« porte bloquée ») ; la proposition reste.
            if (a.EnterScene()) UiSounds.Play(UiCue.Confirm);
            else UiSounds.Play(UiCue.Error);
        }

        private void Skip(AdventureSession a)
        {
            Scene scene = a.CurrentScene ?? a.PendingScene;
            if (scene == null) return;
            bool inside = a.InScene;
            // Revue Codex de la PR #96 : la conversation n'est fermée qu'une fois le souvenir passé ; si la sortie échoue (sauvegarde
            // impossible), rien ne change (dialogue compris) et le cœur affiche son message d'échec (« text.ui.save_failed »).
            if (!a.SkipScene())
            {
                UiSounds.Play(UiCue.Error);
                return;
            }
            if (a.Dialogue != null) a.CloseDialogue();
            UiSounds.Play(UiCue.Close);
            // Le texte de repli est long : il reste à l'écran le temps d'être lu (le message, lui, ne dure que trois secondes).
            _returnCaption = null;
            _sceneDone = false;
            Caption(null, a.Text(scene.Text), 0f);
            if (inside) Fade(1f, 0f, 2f);
        }

        /// <summary>Un message du cœur vient d'être affiché : « Quête passée : … » s'annonce aussi en grand.</summary>
        public void Notice(AdventureSession a, string message)
        {
            if (message == null) return;
            string format = a.Text("text.ui.quest_skipped");
            int hole = format.IndexOf("{0}", StringComparison.Ordinal);
            if (hole <= 0) return;
            string prefix = format.Substring(0, hole);
            if (!message.StartsWith(prefix, StringComparison.Ordinal)) return;
            _banner = prefix.TrimEnd(' ', ':', ' ');
            _bannerDetail = message.Substring(prefix.Length);
            _bannerBorn = Time.unscaledTime;
            UiSounds.Play(UiCue.QuestDone);
        }

        // --- Image ---------------------------------------------------------------------------------------------------------------------

        /// <summary>Avant que l'arène place le joueur : on retire l'abaissement du corps couché (l'arène glisse depuis la place affichée).</summary>
        public void BeforeSync()
        {
            if (_dropped != null && _dropActive) _dropped.position += _dropApplied;
            _dropActive = false;
        }

        /// <summary>Après que l'arène a placé les corps : réveil, décor du souvenir, Croc-Gris, silhouette, collier ; fin du souvenir.</summary>
        public void AfterSync(AdventureSession a, ICombatClient combat, ArenaView arena, Effects effects, CameraRig camera, float deltaTime, bool free)
        {
            float now = Time.unscaledTime;
            _white = Mathf.MoveTowards(_white, _whiteTarget, _whiteRate * deltaTime);
            Wake(arena, deltaTime);
            Gear(a, arena);
            if (_memory != null) Memory(a, arena, effects, camera, now, free);
            if (_vallons != null) Meadow(a, combat, arena, effects, camera, now);
            Voices(a, deltaTime);
            // L'Éclat brûle dans la paume tant qu'un souvenir attend d'être revécu.
            if (a.PendingScene != null && !a.InScene && effects != null && now >= _callAt && combat != null && arena.Feet(combat.PlayerId) is Vector3 feet)
            {
                effects.Cast(feet, Effects.Teal);
                _callAt = now + 2.6f;
            }
        }

        // --- Réveil ----------------------------------------------------------------------------------------------------------------

        private void Wake(ArenaView arena, float deltaTime)
        {
            if (_wake < 0f) return;
            _wake += deltaTime;
            Transform player = arena.Player;
            if (_wake >= WakeEnd)
            {
                _wake = -1f;
                _stepBorn = Time.unscaledTime;
                return;
            }
            if (player == null) return;
            // Couché sur le dos jusqu'à l'ouverture des yeux, puis il se relève ; la couverture reste dans l'herbe.
            float lie = 1f - Smooth((_wake - StandAt) / 1.3f);
            Vector3 flat = player.position;
            if (_blanket == null)
            {
                _blanket = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _blanket.name = "Couverture d'Odile (V1.9.8)";
                Object.Destroy(_blanket.GetComponent<Collider>());
                // Matériau propre à la couverture (pas dans _owned : elle survit au décor de la zone jusqu'à la porte suivante).
                _blanket.GetComponent<Renderer>().material = ArenaView.MakeMaterial(new Color(0.48f, 0.2f, 0.14f));
                _blanket.transform.localScale = new Vector3(1.1f, 0.07f, 1.5f);
            }
            float yaw = player.eulerAngles.y;
            Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            if (lie > 0f)
            {
                player.rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(-85f * lie, 0f, 0f);
                _dropApplied = Vector3.up * (0.68f * lie);
                _dropped = player;
                _dropActive = true;
                player.position = flat - _dropApplied;
            }
            // Sur le corps couché (les jambes), puis à terre, un peu de côté, quand il se lève.
            float onBody = Mathf.Clamp01(lie);
            _blanket.transform.position = new Vector3(flat.x, flat.y - ArenaView.FeetOffset + 0.04f + 0.36f * onBody, flat.z) + forward * (0.35f * onBody) + Vector3.Cross(Vector3.up, forward) * (0.8f * (1f - onBody));
            _blanket.transform.rotation = Quaternion.Euler(0f, yaw + 12f * (1f - onBody), 0f);
        }

        // --- Galeran et le collier -------------------------------------------------------------------------------------------------

        private void Gear(AdventureSession a, ArenaView arena)
        {
            Transform player = arena.Player;
            bool galeran = a.InScene && player != null;
            if (!galeran || _galeranOwner != player)
            {
                if (_galeran != null) Object.Destroy(_galeran);
                _galeran = null;
                _galeranOwner = null;
                ShowPlayerAgain();
            }
            if (galeran && _galeran == null)
            {
                _galeran = Holder(player, "Galeran (V1.9.8)");
                _galeranOwner = player;
                if (!KnightGaleran(player)) PlainGaleran();
            }
            if (_galeran != null) AnimateGaleran(player);

            bool worn = !a.InScene && player != null && Wears(a, CollarItem);
            if (!worn || _pendantOwner != player)
            {
                if (_pendant != null) Object.Destroy(_pendant);
                _pendant = null;
                _pendantOwner = null;
            }
            if (worn && _pendant == null) Pendant(player);
        }

        /// <summary>
        /// Sous-lot V2.11 (5e vague) : Galeran est le chevalier en armure à la lance (ou le roi en armure) ; le corps du joueur est caché le temps
        /// du souvenir. Faux sans le modèle : les formes simples restent.
        /// </summary>
        private bool KnightGaleran(Transform player)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            Wave5Look knight = catalog != null ? catalog.Look(Wave5Role.Galeran) : null;
            if (knight == null) return false;
            // Le support est au centre de la capsule (1 m au-dessus du sol) : les pieds du chevalier à -1 m.
            GameObject model = Wave5View.Place(_galeran.transform, Wave5Role.Galeran, "Galeran", player.position + Vector3.down * 1f, player.eulerAngles.y);
            if (model == null) return false;
            model.transform.SetParent(_galeran.transform, true);
            _galeranModel = model.transform;
            _galeranBaseY = model.transform.localPosition.y;
            _galeranAnimation = model.GetComponentInChildren<Animation>();
            _galeranLast = player.position;
            foreach (Renderer r in player.GetComponentsInChildren<Renderer>())
            {
                if (r.transform.IsChildOf(_galeran.transform) || !r.enabled) continue;
                r.enabled = false;
                _hiddenForGaleran.Add(r);
            }
            return true;
        }

        /// <summary>Sous-lot V2.11 : le chevalier marche (s'il en a l'animation) ou se balance un peu quand le joueur avance, sinon il attend.</summary>
        private void AnimateGaleran(Transform player)
        {
            if (_galeranModel == null) return;
            float dt = Mathf.Max(0.0001f, Time.deltaTime);
            Vector3 d = player.position - _galeranLast;
            d.y = 0f;
            _galeranLast = player.position;
            bool moving = d.magnitude / dt > 0.4f;
            string wanted = moving ? "Walk" : "Idle";
            if (_galeranAnimation != null)
            {
                string clip = StyleRules.Clip(ClipNames(_galeranAnimation), moving ? StyleMotion.Walk : StyleMotion.Idle);
                if (clip != null && !_galeranAnimation.IsPlaying(clip)) _galeranAnimation.CrossFade(clip, 0.25f);
                if (clip != null && StyleRules.Key(clip).Contains(StyleRules.Key(wanted))) moving = false;   // vraie marche : pas de balancement
            }
            _galeranStep = moving ? _galeranStep + dt * 9f : 0f;
            Vector3 p = _galeranModel.localPosition;
            _galeranModel.localPosition = new Vector3(p.x, _galeranBaseY + (moving ? 0.035f * Mathf.Abs(Mathf.Sin(_galeranStep)) : 0f), p.z);
        }

        private static List<string> ClipNames(Animation animation)
        {
            var names = new List<string>();
            foreach (AnimationState state in animation) names.Add(state.name);
            return names;
        }

        private void ShowPlayerAgain()
        {
            foreach (Renderer r in _hiddenForGaleran) if (r != null) r.enabled = true;
            _hiddenForGaleran.Clear();
            _galeranModel = null;
            _galeranAnimation = null;
        }

        /// <summary>Galeran en formes simples (sans le chevalier) : la lance trop lourde, l'armure neuve, et le heaume de la 5e vague s'il est là.</summary>
        private void PlainGaleran()
        {
            {
                Material wood = Owned(ArenaView.MakeMaterial(new Color(0.42f, 0.3f, 0.18f)));
                Material steel = Owned(Shiny(new Color(0.82f, 0.85f, 0.9f)));
                Transform t = _galeran.transform;
                // Une lance plus haute que lui (2,7 m), tenue droite, un peu penchée en avant : trop lourde pour un garde de son âge.
                GameObject shaft = Part(t, "Lance (hampe)", PrimitiveType.Cylinder, new Vector3(0.42f, 0.25f, 0.2f), new Vector3(0.05f, 1.35f, 0.05f), wood);
                shaft.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
                GameObject head = Part(t, "Lance (fer)", PrimitiveType.Cube, new Vector3(0.42f, 1.62f, 0.39f), new Vector3(0.09f, 0.34f, 0.025f), steel);
                head.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
                // Armure neuve : épaulières et plastron qui brillent.
                Part(t, "Épaulière gauche", PrimitiveType.Sphere, new Vector3(-0.27f, 0.48f, 0f), new Vector3(0.3f, 0.18f, 0.32f), steel);
                Part(t, "Épaulière droite", PrimitiveType.Sphere, new Vector3(0.27f, 0.48f, 0f), new Vector3(0.3f, 0.18f, 0.32f), steel);
                Part(t, "Plastron", PrimitiveType.Cube, new Vector3(0f, 0.3f, 0.17f), new Vector3(0.42f, 0.4f, 0.06f), steel);
                // Sous-lot V2.11 : le heaume de bingiz sur la tête (sans lui, tête nue comme avant).
                GameObject helmet = Wave5View.Place(t, Wave5Role.Helmet, "Heaume de Galeran", t.position + new Vector3(0f, 0.62f, 0f), t.eulerAngles.y);
                if (helmet != null) helmet.transform.SetParent(t, true);
            }
        }

        private void Pendant(Transform player)
        {
            {
                // Le collier de Croc-Gris : un croc d'ivoire au bout d'un lacet de cuir, sur la poitrine.
                _pendant = Holder(player, "Collier de Croc-Gris (V1.9.8)");
                _pendantOwner = player;
                Material ivory = Owned(Shiny(new Color(0.92f, 0.88f, 0.76f)));
                Material leather = Owned(ArenaView.MakeMaterial(new Color(0.22f, 0.14f, 0.08f)));
                Transform t = _pendant.transform;
                GameObject cord = Part(t, "Lacet", PrimitiveType.Cylinder, new Vector3(0f, 0.58f, 0.05f), new Vector3(0.32f, 0.008f, 0.3f), leather);
                cord.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
                GameObject fang = Part(t, "Croc", PrimitiveType.Capsule, new Vector3(0f, 0.4f, 0.21f), new Vector3(0.06f, 0.09f, 0.06f), ivory);
                fang.transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
            }
        }

        private static bool Wears(AdventureSession a, string item)
        {
            // Relecture de la PR #96 : appelé à chaque image : les emplacements fixes du catalogue, sans LINQ ni allocation.
            Inventory inventory = a.World.Inventory;
            var places = inventory.Catalog.Places;
            for (int i = 0; i < places.Count; i++)
                if (inventory.WornAt(places[i].Id) == item) return true;
            return false;
        }

        private void DestroyGear()
        {
            ShowPlayerAgain();
            if (_galeran != null) Object.Destroy(_galeran);
            if (_pendant != null) Object.Destroy(_pendant);
            _galeran = _pendant = null;
            _galeranOwner = _pendantOwner = null;
        }

        /// <summary>Support sans échelle accroché au corps (la capsule du joueur est mise à l'échelle de son rayon).</summary>
        private static GameObject Holder(Transform body, string name)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(body, false);
            Vector3 s = body.localScale;
            holder.transform.localScale = new Vector3(1f / Mathf.Max(0.01f, s.x), 1f / Mathf.Max(0.01f, s.y), 1f / Mathf.Max(0.01f, s.z));
            holder.transform.localPosition = Vector3.zero;
            return holder;
        }

        // --- Le souvenir de la Rupture ---------------------------------------------------------------------------------------------

        private void BuildMemory(AdventureSession a)
        {
            _memory = new GameObject("Souvenir de la Rupture (V1.9.8)");
            Transform root = _memory.transform;
            _gateOpen = a.World.Narrative?.Knows(GateFact) == true;
            _gateOpenedAt = _gateOpen ? -99f : -1f;

            // La porte de lumière, à la place de l'obstacle (le fond de la salle).
            _gate = new Vector3(0f, 0f, 5.5f);
            foreach (SpaceObstacle o in a.Map.Obstacles)
            {
                if (!IsLightGate(o.Id)) continue;
                float x0 = o.Points.Min(p => p.X.Raw) / 10000f, x1 = o.Points.Max(p => p.X.Raw) / 10000f;
                float z0 = o.Points.Min(p => p.Z.Raw) / 10000f, z1 = o.Points.Max(p => p.Z.Raw) / 10000f;
                _gate = new Vector3((x0 + x1) / 2f, 0f, (z0 + z1) / 2f);
                _gateHalf = Mathf.Max(1.2f, (x1 - x0) / 2f - 0.1f);
            }
            Material light = Owned(Glow(new Color(0.85f, 0.9f, 1f), 4f));
            const float pillar = 3.3f;
            Part(root, "Porte de lumière (montant gauche)", PrimitiveType.Cube, _gate + new Vector3(-_gateHalf, pillar / 2f, 0f), new Vector3(0.26f, pillar, 0.3f), light, true);
            Part(root, "Porte de lumière (montant droit)", PrimitiveType.Cube, _gate + new Vector3(_gateHalf, pillar / 2f, 0f), new Vector3(0.26f, pillar, 0.3f), light, true);
            for (int i = 0; i <= 10; i++)
            {
                float angle = Mathf.PI * i / 10f;
                var at = _gate + new Vector3(Mathf.Cos(angle) * _gateHalf, pillar + Mathf.Sin(angle) * _gateHalf * 0.8f, 0f);
                GameObject stone = Part(root, "Porte de lumière (arche)", PrimitiveType.Cube, at, new Vector3(0.3f, 0.3f, 0.32f), light, true);
                stone.transform.rotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
            }
            // Le voile de lumière entre les montants, et la lumière qui coule sur les dalles.
            _veil = Additive(new Color(0.75f, 0.85f, 1f, 0.35f));
            if (_veil != null)
            {
                GameObject veil = Part(root, "Porte de lumière (voile)", PrimitiveType.Quad, _gate + new Vector3(0f, (pillar + _gateHalf * 0.8f) / 2f, -0.02f),
                    new Vector3(_gateHalf * 2f - 0.2f, pillar + _gateHalf * 0.8f - 0.2f, 1f), _veil, true);
                _veilBody = veil.transform;
                _floorGlow = Additive(new Color(0.7f, 0.8f, 1f, 0.25f));
                GameObject pool = Part(root, "Lumière sur les dalles", PrimitiveType.Quad, _gate + new Vector3(0f, 0.03f, -1.6f), new Vector3(_gateHalf * 2.4f, 3.4f, 1f), _floorGlow, true);
                pool.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                // Brume au ras du sol, qui dérive lentement.
                Material mist = Additive(new Color(0.42f, 0.5f, 0.78f, 0.09f));
                for (int i = 0; i < 9; i++)
                {
                    var at = new Vector3(-3.2f + (i % 3) * 3.2f, 0.12f + 0.05f * i, -6f + (i / 3) * 5.5f);
                    GameObject sheet = Part(root, "Brume du souvenir", PrimitiveType.Quad, at, new Vector3(5.5f, 5.5f, 1f), mist, true);
                    sheet.transform.rotation = Quaternion.Euler(90f, 37f * i, 0f);
                    _mist.Add((sheet.transform, at));
                }
            }
            _gateLight = Lamp(root, "Lumière de la porte", _gate + new Vector3(0f, 2.2f, -1f), new Color(0.78f, 0.86f, 1f), 11f, 3f);

            // Sept Seuillards en robe sombre, en demi-cercle autour de la porte, qui chantent (ils se balancent).
            _singerRobe = Owned(Glow(new Color(0.15f, 0.16f, 0.24f), 0.25f));
            Material hood = Owned(ArenaView.MakeMaterial(new Color(0.08f, 0.08f, 0.12f)));
            Material face = Owned(Glow(new Color(0.7f, 0.78f, 1f), 1.5f));
            for (int i = 0; i < 7; i++)
            {
                float angle = Mathf.Lerp(-0.3f, Mathf.PI + 0.3f, i / 6f);
                var at = _gate + new Vector3(Mathf.Cos(angle) * (_gateHalf + 1.6f), 0f, Mathf.Sin(angle) * 1.9f);
                var singer = new GameObject("Seuillard " + (i + 1));
                singer.transform.SetParent(root, false);
                singer.transform.position = at;
                Vector3 look = _gate - at;
                look.y = 0f;
                if (look.sqrMagnitude > 0.01f) singer.transform.rotation = Quaternion.LookRotation(look);
                Part(singer.transform, "Robe", PrimitiveType.Cylinder, new Vector3(0f, 0.8f, 0f), new Vector3(0.6f, 0.8f, 0.6f), _singerRobe, false, true);
                Part(singer.transform, "Capuchon", PrimitiveType.Sphere, new Vector3(0f, 1.72f, 0f), new Vector3(0.44f, 0.46f, 0.44f), hood, false, true);
                // Le chant : une lueur sous le capuchon.
                Part(singer.transform, "Chant", PrimitiveType.Sphere, new Vector3(0f, 1.66f, 0.18f), new Vector3(0.12f, 0.07f, 0.06f), face, false, true);
                _singers.Add(singer.transform);
                _singerBase.Add(i * 0.9f);
            }

            // La lune, haute derrière la porte ; elle se fendra quand la porte s'ouvre.
            Material moon = Owned(Glow(new Color(0.88f, 0.9f, 1f), 1.6f));
            _moon = Part(root, "Lune", PrimitiveType.Sphere, _gate + new Vector3(0f, 10f, 17f), new Vector3(3.4f, 3.4f, 3.4f), moon, true).transform;
            _moonLeft = Part(root, "Lune (moitié gauche)", PrimitiveType.Sphere, _moon.position, new Vector3(1.7f, 3.4f, 3.4f), moon, true).transform;
            _moonRight = Part(root, "Lune (moitié droite)", PrimitiveType.Sphere, _moon.position, new Vector3(1.7f, 3.4f, 3.4f), moon, true).transform;
            _crack = Part(root, "Lune (fêlure)", PrimitiveType.Cube, _moon.position, new Vector3(0.12f, 3.8f, 0.6f), Owned(Glow(new Color(0.75f, 0.9f, 1f), 7f)), true).transform;
            MoonSplit(_gateOpen ? 1f : 0f);

            // La reine Ysolde : robe sombre, cercle d'or, un petit linceul blanc dans les bras (dessinée ici : pas de paysan à sa place).
            foreach (WorldMarker npc in a.Npcs())
            {
                if (npc.Id != YsoldeId) continue;
                GameObject marker = GameObject.Find(npc.Id);
                if (marker != null && marker.GetComponent<Renderer>() != null) marker.GetComponent<Renderer>().enabled = false;
                var queen = new GameObject("Reine Ysolde (V1.9.8)");
                queen.transform.SetParent(root, false);
                queen.transform.position = new Vector3((float)npc.X, 0f, (float)npc.Z);
                Vector3 toGate = _gate - queen.transform.position;
                toGate.y = 0f;
                // Tournée de trois quarts vers l'entrée de la salle, la porte dans le dos.
                if (toGate.sqrMagnitude > 0.01f) queen.transform.rotation = Quaternion.LookRotation(toGate * -1f) * Quaternion.Euler(0f, 35f, 0f);
                Material robe = Owned(ArenaView.MakeMaterial(new Color(0.1f, 0.06f, 0.14f)));
                Material skin = Owned(ArenaView.MakeMaterial(new Color(0.86f, 0.74f, 0.66f)));
                Material gold = Owned(Shiny(new Color(0.95f, 0.75f, 0.3f)));
                Material linen = Owned(Glow(new Color(0.95f, 0.95f, 0.92f), 0.6f));
                Transform q = queen.transform;
                Part(q, "Robe (traîne)", PrimitiveType.Cylinder, new Vector3(0f, 0.45f, -0.05f), new Vector3(0.8f, 0.45f, 0.9f), robe, false, true);
                Part(q, "Robe (buste)", PrimitiveType.Cylinder, new Vector3(0f, 1.15f, 0f), new Vector3(0.46f, 0.3f, 0.4f), robe, false, true);
                Part(q, "Visage", PrimitiveType.Sphere, new Vector3(0f, 1.68f, 0f), new Vector3(0.26f, 0.3f, 0.26f), skin, false, true);
                Part(q, "Cercle d'or", PrimitiveType.Cylinder, new Vector3(0f, 1.82f, 0f), new Vector3(0.29f, 0.025f, 0.29f), gold, false, true);
                GameObject shroud = Part(q, "Linceul", PrimitiveType.Capsule, new Vector3(0f, 1.18f, 0.28f), new Vector3(0.2f, 0.22f, 0.2f), linen, false, true);
                shroud.transform.localRotation = Quaternion.Euler(0f, 0f, 80f);
                Lamp(q, "Lueur du linceul", new Vector3(0f, 1.3f, 0.6f), new Color(0.9f, 0.92f, 1f), 3f, 0.8f, true);
            }

            // L'Écho de la Rupture : une silhouette de lumière (la forme simple brille), un halo et des voix qui tournent autour de lui.
            Material halo = Additive(new Color(0.7f, 0.82f, 1f, 0.3f));
            if (halo != null)
            {
                _echoHalo = Part(root, "Écho (halo)", PrimitiveType.Sphere, Vector3.zero, new Vector3(1.9f, 2.6f, 1.9f), halo, true).transform;
            }
            _echoLight = Lamp(root, "Écho (lumière)", Vector3.zero, new Color(0.7f, 0.82f, 1f), 6f, 2.4f);
            Material voice = Owned(Glow(new Color(0.8f, 0.9f, 1f), 5f));
            for (int i = 0; i < 6; i++) _motes.Add(Part(root, "Écho (voix)", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.1f, 0.1f, 0.1f), voice, true).transform);
        }

        private void Memory(AdventureSession a, ArenaView arena, Effects effects, CameraRig camera, float now, bool free)
        {
            // La porte s'ouvre (dernière réponse d'Ysolde) : éclair blanc, des milliers de voix, la lune se fend.
            if (!_gateOpen && a.World.Narrative?.Knows(GateFact) == true)
            {
                _gateOpen = true;
                _gateOpenedAt = now;
                Fade(0.9f, 0f, 1.6f);
                camera?.Shake(0.55);
                effects?.LevelUp(_gate);
                UiSounds.Play(UiCue.LevelUp);
            }
            float open = _gateOpen ? (_gateOpenedAt < 0f ? 1f : Smooth((now - _gateOpenedAt) / 2.2f)) : 0f;
            float beat = Mathf.Sin(now * 1.7f);
            if (_gateLight != null)
            {
                _gateLight.intensity = Mathf.Lerp(2.6f, 7.5f, open) + 0.4f * beat;
                _gateLight.range = Mathf.Lerp(10f, 16f, open);
            }
            if (_veil != null) _veil.SetColor("_BaseColor", new Color(0.75f, 0.85f, 1f, Mathf.Lerp(0.28f, 0.85f, open) + 0.06f * beat));
            if (_floorGlow != null) _floorGlow.SetColor("_BaseColor", new Color(0.7f, 0.8f, 1f, Mathf.Lerp(0.18f, 0.6f, open)));
            if (_veilBody != null) _veilBody.localScale = new Vector3(_gateHalf * 2f - 0.2f, 3.3f + _gateHalf * 0.8f - 0.2f, 1f) * (1f + 0.12f * open);
            MoonSplit(open);
            for (int i = 0; i < _singers.Count; i++)
            {
                Transform s = _singers[i];
                Vector3 p = s.position;
                // Ils chantent en se balançant ; quand la porte s'ouvre, ils se penchent en arrière, saisis.
                s.position = new Vector3(p.x, 0.04f * Mathf.Sin(now * 1.9f + _singerBase[i]), p.z);
                Vector3 look = _gate - new Vector3(p.x, 0f, p.z);
                if (look.sqrMagnitude > 0.01f) s.rotation = Quaternion.LookRotation(look) * Quaternion.Euler(-14f * open + 3f * Mathf.Sin(now * 1.3f + _singerBase[i]), 0f, 0f);
            }
            for (int i = 0; i < _mist.Count; i++)
            {
                (Transform sheet, Vector3 origin) = _mist[i];
                sheet.position = origin + new Vector3(Mathf.Sin(now * 0.11f + i) * 1.4f, 0f, Mathf.Cos(now * 0.07f + i * 1.7f) * 0.9f);
            }

            // L'Écho : la forme simple brille, le halo et les voix le suivent ; à sa chute, une colonne de lumière, puis le retour.
            // Sous-lot V1.9.9 : la seconde forme (les Voix de la Rupture) brille de même, et le halo la suit dès qu'elle est là.
            arena.Glow(EchoId, new Color(1.6f, 1.8f, 2.4f));
            arena.Glow(VoicesId, new Color(1.6f, 1.8f, 2.4f));
            Vector3? feet = arena.Feet(VoicesId) ?? arena.Feet(EchoId);
            bool shown = feet.HasValue && !a.SceneFinished;
            if (_echoHalo != null) _echoHalo.gameObject.SetActive(shown);
            if (_echoLight != null) _echoLight.gameObject.SetActive(shown);
            for (int i = 0; i < _motes.Count; i++)
            {
                _motes[i].gameObject.SetActive(shown);
                if (!shown) continue;
                float angle = now * (1.2f + 0.25f * i) + i * 1.05f;
                _motes[i].position = feet.Value + new Vector3(Mathf.Cos(angle) * 0.9f, 1.1f + 0.6f * Mathf.Sin(now * 1.7f + i), Mathf.Sin(angle) * 0.9f);
            }
            if (shown)
            {
                if (_echoHalo != null) _echoHalo.position = feet.Value + Vector3.up * 1.15f;
                if (_echoLight != null)
                {
                    _echoLight.transform.position = feet.Value + Vector3.up * 1.4f;
                    _echoLight.intensity = 2.2f + 0.8f * Mathf.PerlinNoise(now * 4f, 0.3f);
                }
            }

            if (a.SceneFinished && !_sceneDone)
            {
                _sceneDone = true;
                if (_leaveAt < 0f) _leaveAt = now + 2.6f;
                if (feet.HasValue) effects?.LevelUp(feet.Value);
                camera?.Shake(0.6);
            }
            if (_leaveAt > 0f)
            {
                // Le blanc monte, puis le pré revient (le cœur dit le texte du retour ; la légende le garde à l'écran).
                if (now >= _leaveAt - 1.5f && _whiteTarget < 1f) Fade(_white, 1f, 1.5f);
                if (now >= _leaveAt && free && a.InScene && a.Dialogue == null && !a.MustContinue)
                {
                    _returnCaption = _backKey != null ? a.Text(_backKey) : null;
                    if (!a.LeaveScene())
                    {
                        // Sauvegarde impossible : le message du cœur s'affiche ; on réessaie un peu plus tard.
                        _returnCaption = null;
                        _leaveAt = now + 3f;
                        Fade(_white, 0f, 1f);
                    }
                }
            }
        }

        private void MoonSplit(float open)
        {
            if (_moon == null) return;
            bool split = open > 0.001f;
            _moon.gameObject.SetActive(!split);
            _moonLeft.gameObject.SetActive(split);
            _moonRight.gameObject.SetActive(split);
            _crack.gameObject.SetActive(split);
            Vector3 c = _moon.position;
            float gap = 0.45f + 0.9f * open;
            _moonLeft.position = c + new Vector3(-gap, -0.25f * open, 0f);
            _moonRight.position = c + new Vector3(gap, 0.2f * open, 0f);
            _moonLeft.rotation = Quaternion.Euler(0f, 0f, 9f * open);
            _moonRight.rotation = Quaternion.Euler(0f, 0f, -7f * open);
            _crack.localScale = new Vector3(0.12f + 1.2f * open, 3.8f, 0.6f);
        }

        // --- Les Vallons ----------------------------------------------------------------------------------------------------------

        private void BuildVallons(AdventureSession a)
        {
            MapPlace meadow = a.Map.Places.FirstOrDefault(p => p.Id == MeadowPlace);
            if (meadow == null) return;
            _vallons = new GameObject("Chapitre 1 (V1.9.8)");
            // La pierre gravée, au milieu du rond de champignons (près de la porte cachée du souvenir) : trop usée pour être lue.
            var at = new Vector3(-18f, 0f, 24.2f);
            at.y = (float)GroundRelief.For(a.Map).Height(at.x, at.z);   // V2.24 : sur le sol affiché (replat de la porte du souvenir)
            Material rock = StyleWorld.Rock(out float _) ?? Owned(ArenaView.MakeMaterial(new Color(0.46f, 0.45f, 0.42f)));
            Part(_vallons.transform, "Pierre gravée du pré", PrimitiveType.Cylinder, at + Vector3.up * 0.09f, new Vector3(1.3f, 0.09f, 0.9f), rock, true);
            _glyphs = Owned(Glow(new Color(0.45f, 0.85f, 0.9f), 1.2f));
            for (int i = 0; i < 5; i++)
            {
                GameObject sign = Part(_vallons.transform, "Signe des Seuillards", PrimitiveType.Cube, at + new Vector3(-0.4f + 0.2f * i, 0.185f, 0.05f * (i % 2)),
                    new Vector3(0.04f, 0.01f, 0.28f - 0.04f * (i % 3)), _glyphs, true);
                sign.transform.rotation = Quaternion.Euler(0f, 18f * (i - 2), 0f);
            }
        }

        private void Meadow(AdventureSession a, ICombatClient combat, ArenaView arena, Effects effects, CameraRig camera, float now)
        {
            if (_glyphs != null) _glyphs.SetColor("_EmissionColor", new Color(0.45f, 0.85f, 0.9f) * new Color(0.6f + 0.6f * Mathf.Sin(now * 0.8f), 0.6f + 0.6f * Mathf.Sin(now * 0.8f), 0.6f + 0.6f * Mathf.Sin(now * 0.8f)));
            // Croc-Gris passe à sa seconde forme : un hurlement, la caméra tremble.
            if (combat != null)
            {
                bool raging = false;
                foreach (OtherState o in combat.Others())
                    if (o.Id.StartsWith(RagingCroc, StringComparison.Ordinal) && o.View.IsPresent && !o.View.IsDead && arena.Shown(o.Id))
                    {
                        raging = true;
                        if (!_ragingSeen && arena.Feet(o.Id) is Vector3 wolf)
                        {
                            Howl();
                            camera?.Shake(0.7);
                            effects?.Slam(wolf, 3f);
                        }
                    }
                _ragingSeen = raging;
            }
            // La silhouette en manteau sombre, à la lisière nord du pré, au moment de la découverte (après Croc-Gris).
            if (now >= _discoveryCheck)
            {
                _discoveryCheck = now + 0.5f;
                bool known = a.World.Narrative != null && a.World.Narrative.Discoveries.Contains(SilhouetteDiscovery);
                if (known && !_silhouetteKnown) ShowSilhouette(a, arena, now);
                _silhouetteKnown = known;
            }
            if (_silhouette != null)
            {
                float age = now - _silhouetteBorn;
                if (arena.Player != null)
                {
                    Vector3 look = arena.Player.position - _silhouette.transform.position;
                    look.y = 0f;
                    if (look.sqrMagnitude > 0.01f) _silhouette.transform.rotation = Quaternion.LookRotation(look);
                }
                float size = Smooth(age / 0.5f) * (1f - Smooth((age - 4.2f) / 0.35f));
                _silhouette.transform.localScale = new Vector3(1f, Mathf.Max(0.01f, size), 1f);
                if (age >= 4.6f)
                {
                    effects?.Death(_silhouette.transform.position);
                    Object.Destroy(_silhouette);
                    _silhouette = null;
                }
            }
        }

        private void ShowSilhouette(AdventureSession a, ArenaView arena, float now)
        {
            MapPlace meadow = a.Map.Places.FirstOrDefault(p => p.Id == MeadowPlace);
            if (meadow == null || _vallons == null) return;
            float x = arena.Player != null ? Mathf.Clamp(arena.Player.position.x, (float)meadow.MinX + 2f, (float)meadow.MaxX - 2f) : (float)meadow.CenterX;
            if (_silhouette != null) Object.Destroy(_silhouette);
            _silhouette = new GameObject("Silhouette en manteau sombre (V1.9.8)");
            _silhouette.transform.SetParent(_vallons.transform, false);
            _silhouette.transform.position = StyleWorld.OnGround(new Vector3(x, 0f, (float)meadow.MaxZ + 1.5f));   // V2.24
            _silhouetteBorn = now;
            Material cloak = Owned(ArenaView.MakeMaterial(new Color(0.05f, 0.05f, 0.07f)));
            Material shine = Owned(Glow(new Color(0.55f, 0.95f, 1f), 6f));
            Transform t = _silhouette.transform;
            Part(t, "Manteau", PrimitiveType.Cylinder, new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.6f), cloak, false, true);
            Part(t, "Capuchon", PrimitiveType.Sphere, new Vector3(0f, 1.95f, 0f), new Vector3(0.42f, 0.5f, 0.42f), cloak, false, true);
            Part(t, "Éclat à la main", PrimitiveType.Sphere, new Vector3(0.38f, 0.95f, 0.22f), new Vector3(0.09f, 0.14f, 0.09f), shine, false, true);
            Lamp(t, "Lueur de l'éclat", new Vector3(0.38f, 1f, 0.4f), new Color(0.55f, 0.95f, 1f), 3f, 1.2f, true);
            Caption(null, a.Text(SilhouetteText), 0f);
        }

        private void ClearZone()
        {
            // La couverture reste sur la place jusqu'à ce qu'on change de zone.
            if (_blanket != null) Object.Destroy(_blanket);
            _blanket = null;
            if (_memory != null) Object.Destroy(_memory);
            if (_vallons != null) Object.Destroy(_vallons);
            _memory = _vallons = null;
            _silhouette = null;
            _singers.Clear();
            _singerBase.Clear();
            _mist.Clear();
            _motes.Clear();
            _moon = _moonLeft = _moonRight = _crack = null;
            _echoHalo = null;
            _echoLight = null;
            _gateLight = null;
            _veil = _floorGlow = _glyphs = _singerRobe = null;
            _veilBody = null;
            _gateOpen = false;
            foreach (Material m in _owned)
                if (m != null) Object.Destroy(m);
            _owned.Clear();
            // Le décor accroché au joueur garde ses matériaux : il est refait avec la zone.
            DestroyGear();
        }

        // --- Sons ----------------------------------------------------------------------------------------------------------------

        private void Voices(AdventureSession a, float deltaTime)
        {
            bool inScene = a.InScene;
            if (inScene && _choir == null && _ear != null)
            {
                // Faits une fois, à la première entrée : le chant des Seuillards (ré mineur, voix qui ondulent) et, porte ouverte, des milliers
                // de voix. Boucles de huit secondes sans couture (fréquences multiples de 1/8 Hz).
                var go = new GameObject("Chant du souvenir (V1.9.8)");
                go.transform.SetParent(_ear, false);
                _choir = Source(go, Choir("Chant des Seuillards", new[] { 146.875f, 220f, 293.625f, 349.25f, 440f }, 1));
                _voices = Source(go, Choir("Milliers de voix", new[] { 146.875f, 147.125f, 220f, 220.25f, 293.625f, 293.875f, 349.25f, 440f, 440.5f, 587.25f, 698.5f, 880f }, 2));
            }
            if (_choir == null) return;
            float choir = inScene ? (_gateOpen ? 0.12f : 0.3f) : 0f;
            float voices = inScene && _gateOpen ? 0.32f : 0f;
            _choir.volume = Mathf.MoveTowards(_choir.volume, choir, deltaTime * 0.25f);
            _voices.volume = Mathf.MoveTowards(_voices.volume, voices, deltaTime * (voices > 0f ? 0.6f : 0.3f));
            Keep(_choir);
            Keep(_voices);
        }

        /// <summary>Relecture de la PR #96 : une seule source des sons ponctuels (hurlement), créée une fois, sur la caméra.</summary>
        private bool EnsureOneShot()
        {
            if (_oneShot != null) return true;
            if (_ear == null) return false;
            var go = new GameObject("Sons ponctuels de l'histoire (V1.9.8)");
            go.transform.SetParent(_ear, false);
            _oneShot = go.AddComponent<AudioSource>();
            _oneShot.playOnAwake = false;
            _oneShot.spatialBlend = 0f;
            return true;
        }

        private static void Keep(AudioSource source)
        {
            if (source.volume > 0f && !source.isPlaying) source.Play();
            else if (source.volume <= 0f && source.isPlaying) source.Stop();
        }

        private static AudioSource Source(GameObject go, AudioClip clip)
        {
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.clip = clip;
            return source;
        }

        private void Howl()
        {
            if (!EnsureOneShot()) return;
            if (_howl == null) _howl = HowlClip();
            if (_howl != null) _oneShot.PlayOneShot(_howl, 0.9f);
        }

        /// <summary>
        /// Chœur fait par le code : chaque voix est une fondamentale et deux harmoniques (un « a » sourd), avec un vibrato et un souffle
        /// lents ; tout se répète exactement en huit secondes (la boucle n'a pas de couture). <paramref name="seed"/> décale les phases.
        /// </summary>
        private static AudioClip Choir(string name, float[] frequencies, int seed)
        {
            const int rate = 16000, seconds = 8;
            int n = rate * seconds;
            var data = new float[n];
            for (int k = 0; k < frequencies.Length; k++)
            {
                double f = frequencies[k];
                double vibratoRate = 4.875 + 0.125 * ((k + seed) % 3);   // 39, 40 ou 41 battements en huit secondes
                double swell = 0.25 * (1 + (k + seed) % 2);              // 2 ou 4 souffles en huit secondes
                double gain = 1.0 / (k + 2.0);
                double phase = k * 0.7 + seed;
                for (int i = 0; i < n; i++)
                {
                    double t = (double)i / rate;
                    phase += 2 * Math.PI * (f + 0.0045 * f * Math.Sin(2 * Math.PI * vibratoRate * t + k)) / rate;
                    double amp = gain * (0.7 + 0.3 * Math.Sin(2 * Math.PI * swell * t + k * 1.3));
                    data[i] += (float)(amp * (Math.Sin(phase) + 0.35 * Math.Sin(2 * phase) + 0.15 * Math.Sin(3 * phase)));
                }
            }
            Normalize(data, 0.8f);
            AudioClip clip = AudioClip.Create(name, n, 1, rate, false);
            if (clip != null) clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Hurlement de loup fait par le code : une voix qui monte, tient, puis retombe, avec un vibrato.</summary>
        private static AudioClip HowlClip()
        {
            const int rate = 22050;
            const float seconds = 2.2f;
            int n = (int)(rate * seconds);
            var data = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / rate;
                double rise = Math.Min(1.0, t / 0.45);
                double fall = t > 1.3 ? (t - 1.3) / (seconds - 1.3) : 0;
                double f = 280 + 300 * rise - 170 * fall + 9 * Math.Sin(2 * Math.PI * 6 * t);
                phase += 2 * Math.PI * f / rate;
                double env = Math.Min(1.0, t / 0.12) * Math.Max(0.0, 1 - fall);
                data[i] = (float)(env * (Math.Sin(phase) + 0.45 * Math.Sin(2 * phase) + 0.2 * Math.Sin(3 * phase)));
            }
            Normalize(data, 0.85f);
            AudioClip clip = AudioClip.Create("Hurlement de Croc-Gris", n, 1, rate, false);
            if (clip != null) clip.SetData(data, 0);
            return clip;
        }

        private static void Normalize(float[] data, float peak)
        {
            float max = 0f;
            foreach (float v in data) max = Mathf.Max(max, Mathf.Abs(v));
            if (max <= 0f) return;
            float k = peak / max;
            for (int i = 0; i < data.Length; i++) data[i] *= k;
        }

        // --- Dessin (OnGUI, écran virtuel de 1080 pixels de haut) -------------------------------------------------------------------

        /// <summary>
        /// Dessine la mise en scène par-dessus l'aventure : voile blanc, réveil, proposition du souvenir (boutons), bulle du tutoriel,
        /// bandeau et légende. <paramref name="free"/> : aucun dialogue ni écran ouvert.
        /// </summary>
        public void Draw(AdventureSession a, bool free, bool calm, bool touch)
        {
            Styles();
            float w = Hud.Width, h = Hud.VirtualHeight, now = Time.unscaledTime;
            if (_white > 0.001f) Hud.Fill(new Rect(0f, 0f, w, h), new Color(1f, 1f, 1f, _white));
            if (_wake >= 0f)
            {
                DrawWake(w, h);
                return;
            }
            if (Prompting(a, free)) DrawPrompt(a, w, h, now);
            if (calm && a.InScene && !a.SceneFinished)
            {
                // Relecture de la PR #96 : l'indice reste visible pendant un dialogue ; au doigt, un bouton (pas de touche X).
                if (touch)
                {
                    if (Hud.Button(new Rect(w / 2f - 150f, 120f, 300f, 54f), "Passer le souvenir", true)) Skip(a);
                }
                else Hud.Centered(new Rect(w / 2f - 300f, h - 64f, 600f, 30f), "X : passer le souvenir", new Color(0.85f, 0.9f, 1f, 0.75f));
            }
            if (free && !a.InScene) DrawTutorial(a, now);
            DrawBanner(w, now);
            DrawCaption(w, h, now);
        }

        private void DrawWake(float w, float h)
        {
            float t = _wake;
            if (t < EyesAt)
            {
                Hud.Fill(new Rect(0f, 0f, w, h), Color.black);
                float title = Smooth((t - TitleAt) / 1.2f) * (1f - Smooth((t - 5.8f) / 1f));
                Label(new Rect(0f, h * 0.36f, w, 80f), "ÉCLATS DU SEUIL", _gold, title);
                if (!string.IsNullOrEmpty(_chapter)) Label(new Rect(0f, h * 0.36f + 84f, w, 40f), "Chapitre 1 · " + _chapter, _line, title * 0.85f);
                float line = Smooth((t - LineAt) / 1f) * (1f - Smooth((t - 6.4f) / 0.6f));
                if (!string.IsNullOrEmpty(_wakeLine)) Label(new Rect(w * 0.15f, h * 0.56f, w * 0.7f, 120f), _wakeLine, _line, line);
                if (t >= 1f) Label(new Rect(0f, h - 70f, w, 30f), "Une touche : passer", _small, 0.45f);
                return;
            }
            // Les yeux s'ouvrent : un clignement, puis la place du village, encore un peu blanche.
            float o = t < 7.6f ? 0.35f * Smooth((t - EyesAt) / 0.6f)
                : t < 7.9f ? Mathf.Lerp(0.35f, 0.08f, Smooth((t - 7.6f) / 0.3f))
                : Mathf.Lerp(0.08f, 1f, Smooth((t - 7.9f) / 1.4f));
            float lid = (1f - o) * h / 2f;
            Hud.Fill(new Rect(0f, 0f, w, lid), Color.black);
            Hud.Fill(new Rect(0f, h - lid, w, lid), Color.black);
            for (int i = 1; i <= 6; i++)
            {
                float band = 18f * i, alpha = 0.75f * (1f - i / 7f) * (1f - o * 0.6f);
                Hud.Fill(new Rect(0f, lid, w, band), new Color(0f, 0f, 0f, alpha / 6f));
                Hud.Fill(new Rect(0f, h - lid - band, w, band), new Color(0f, 0f, 0f, alpha / 6f));
            }
            Hud.Fill(new Rect(0f, 0f, w, h), new Color(1f, 0.97f, 0.9f, 0.3f * (1f - o)));
        }

        private void DrawPrompt(AdventureSession a, float w, float h, float now)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(now * 2.4f);
            var panel = new Rect(w / 2f - 330f, h - 330f, 660f, 160f);
            Hud.Fill(new Rect(panel.x - 8f, panel.y - 8f, panel.width + 16f, panel.height + 16f), new Color(0.55f, 0.75f, 1f, 0.12f + 0.12f * pulse));
            Hud.Panel(panel, new Color(0.05f, 0.06f, 0.12f, 0.9f));
            Label(new Rect(panel.x, panel.y + 12f, panel.width, 40f), a.Text(PromptTitle), _gold, 1f);
            Label(new Rect(panel.x, panel.y + 50f, panel.width, 28f), a.Text(PromptCall), _small, 0.9f, TextAnchor.MiddleCenter);
            if (Hud.Button(new Rect(panel.x + 30f, panel.y + 96f, 330f, 46f), "Revivre le souvenir (V)", true)) Relive(a);
            if (Hud.Button(new Rect(panel.x + 380f, panel.y + 96f, 250f, 46f), "Passer (X)", true)) Skip(a);
        }

        private void DrawTutorial(AdventureSession a, float now)
        {
            QuestStep step = a.World.Narrative?.CurrentStep(StoryConversions.Opening);
            if (step == null || a.Dialogue != null)
            {
                if (step == null) _shownStep = null;
                return;
            }
            int index = Array.IndexOf(OpeningSteps, step.Id);
            if (step.Id != _shownStep)
            {
                // Relecture de la PR #96 : les chaînes de la bulle ne sont faites qu'au changement d'étape.
                if (_shownStep != null) UiSounds.Play(UiCue.SkillReady);
                _shownStep = step.Id;
                _stepBorn = now;
                _stepHeader = a.Text(OpeningName) + (index >= 0 ? " · étape " + (index + 1) + "/" + OpeningSteps.Length : "");
                _stepText = a.Text(step.Text);
                _stepKeys = KeysOf(step.Id);
            }
            float pop = Smooth((now - _stepBorn) / 0.35f);
            var panel = new Rect(24f - 40f * (1f - pop), 400f, 600f, 250f);
            Hud.Panel(panel, new Color(0.07f, 0.06f, 0.05f, 0.88f * pop));
            Label(new Rect(panel.x + 20f, panel.y + 12f, panel.width - 40f, 34f), _stepHeader, _gold, pop, TextAnchor.MiddleLeft);
            Label(new Rect(panel.x + 20f, panel.y + 50f, panel.width - 40f, 130f), _stepText, _small, pop, TextAnchor.UpperLeft);
            string keys = _stepKeys;
            if (keys != null)
            {
                float glow = 0.5f + 0.5f * Mathf.Sin(now * 5f);
                var badge = new Rect(panel.x + 20f, panel.y + 186f, 260f, 40f);
                Hud.Fill(badge, new Color(1f, 0.78f, 0.3f, (0.25f + 0.25f * glow) * pop));
                HudArt.Frame(badge);
                Label(badge, keys, _line, pop);
            }
            if (index >= 1 && index <= 4)
                Label(new Rect(panel.x + 296f, panel.y + 180f, panel.width - 316f, 56f), a.Text(SkipHint), _small, 0.7f * pop, TextAnchor.MiddleLeft);
        }

        /// <summary>Touches du PC de l'étape (le doigt a son bouton mis en valeur).</summary>
        private static string KeysOf(string step)
        {
            switch (step)
            {
                case "odile":
                case "retour": return "F : parler";
                case "frapper": return "1 · 2 · 3 · 4";
                case "garder": return "Clic droit / Maj";
                case "parer": return "Espace";
                case "boire": return "P : potion";
                default: return null;
            }
        }

        private void DrawBanner(float w, float now)
        {
            if (_banner == null) return;
            float age = now - _bannerBorn;
            if (age > 3.8f)
            {
                _banner = null;
                return;
            }
            float alpha = Smooth(age / 0.3f) * (1f - Smooth((age - 3.1f) / 0.7f));
            var band = new Rect(w / 2f - 420f, 300f, 840f, 110f);
            Hud.Fill(band, new Color(0.05f, 0.04f, 0.03f, 0.75f * alpha));
            Label(new Rect(band.x, band.y + 8f, band.width, 56f), _banner, _gold, alpha);
            Label(new Rect(band.x, band.y + 62f, band.width, 36f), _bannerDetail, _line, alpha);
        }

        private void DrawCaption(float w, float h, float now)
        {
            if (_captionText == null && _captionTitle == null) return;
            float age = now - _captionBorn;
            if (age > _captionFor)
            {
                _captionText = _captionTitle = null;
                return;
            }
            float alpha = Smooth(age / 0.8f) * (1f - Smooth((age - _captionFor + 1f) / 1f));
            if (_captionTitle != null)
            {
                // Titre d'entrée du souvenir : le lieu, puis le nom du personnage qu'on est (« Galeran »).
                Label(new Rect(0f, h * 0.3f, w, 80f), _captionTitle, _gold, alpha);
                if (_captionText != null) Label(new Rect(0f, h * 0.3f + 82f, w, 44f), "— " + _captionText + " —", _line, alpha);
                return;
            }
            var panel = new Rect(w / 2f - 520f, h * 0.58f, 1040f, 190f);
            Hud.Panel(panel, new Color(0.04f, 0.04f, 0.07f, 0.82f * alpha));
            Label(new Rect(panel.x + 30f, panel.y + 16f, panel.width - 60f, panel.height - 32f), _captionText, _line, alpha);
        }

        private void Caption(string title, string text, float seconds)
        {
            _captionTitle = title;
            _captionText = text;
            _captionBorn = Time.unscaledTime;
            int length = text != null ? text.Length : 0;
            _captionFor = seconds > 0f ? seconds : Mathf.Clamp(4f + length * 0.05f, 7f, 18f);
        }

        private void Fade(float from, float to, float seconds)
        {
            _white = from;
            _whiteTarget = to;
            _whiteRate = Mathf.Abs(to - from) / Mathf.Max(0.05f, seconds);
        }

        private void Styles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 44, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, richText = false };
            _title.normal.textColor = HudArt.Gold;
            if (HudArt.Font != null) _title.font = HudArt.Font;
            _gold = new GUIStyle(_title) { fontSize = 40 };
            _line = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter, richText = false };
            _line.normal.textColor = HudArt.Parchment;
            _small = new GUIStyle(_line) { fontSize = 19, alignment = TextAnchor.UpperLeft };
        }

        private static void Label(Rect rect, string text, GUIStyle style, float alpha, TextAnchor? anchor = null)
        {
            if (alpha <= 0.001f || string.IsNullOrEmpty(text)) return;
            Color previous = GUI.color;
            TextAnchor before = style.alignment;
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            if (anchor.HasValue) style.alignment = anchor.Value;
            // Ombre portée : lisible sur le blanc du souvenir comme sur l'herbe.
            Color ink = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
            style.normal.textColor = ink;
            GUI.Label(rect, Hud.Plain(text), style);
            style.alignment = before;
            GUI.color = previous;
        }

        // --- Petits outils ---------------------------------------------------------------------------------------------------------

        private static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        /// <summary>Première phrase d'un texte (jusqu'au premier point suivi d'un espace), pour l'écran du réveil.</summary>
        private static string FirstSentence(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            int end = text.IndexOf(". ", StringComparison.Ordinal);
            return end > 0 ? text.Substring(0, end + 1) : text;
        }

        private Material Owned(Material material)
        {
            if (material != null) _owned.Add(material);
            return material;
        }

        private static Material Glow(Color color, float strength)
        {
            Material material = ArenaView.MakeMaterial(color);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", new Color(color.r * strength, color.g * strength, color.b * strength));
            }
            return material;
        }

        private static Material Shiny(Color color)
        {
            Material material = ArenaView.MakeMaterial(color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.85f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.75f);
            return material;
        }

        /// <summary>Matériau additif et transparent (voile, brume, halo) ; null sans le shader des particules d'URP.</summary>
        private Material Additive(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) return null;
            var material = new Material(shader);
            ParticleBlend blend = ParticleRules.Blend(4);   // additif (source × alpha + destination)
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", blend.Blend);
            material.SetFloat("_SrcBlend", blend.Src);
            material.SetFloat("_DstBlend", blend.Dst);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = ParticleRules.TransparentQueue;
            material.SetColor("_BaseColor", color);
            return Owned(material);
        }

        /// <summary>Forme simple sans collision ; <paramref name="world"/> : position dans le monde, sinon locale ; sans ombre si demandé.</summary>
        private static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, bool world = false,
            bool shadows = false)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            if (world) go.transform.position = position;
            else go.transform.localPosition = position;
            go.transform.localScale = scale;
            Renderer renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            if (!shadows) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        private static Light Lamp(Transform parent, string name, Vector3 position, Color color, float range, float intensity, bool local = false)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(parent, false);
            if (local) light.transform.localPosition = position;
            else light.transform.position = position;
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            return light;
        }
    }
}
