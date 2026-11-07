using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EdS.Data;
using EdS.Data.Builds;
using EdS.Data.Json;
using EdS.Data.Scenarios;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using EdS.Net;
using EdS.Runtime;
using EdS.World;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot 9.3 : boucle du prototype. Charge les données du dépôt (<c>data/v0</c>) et un scénario (<c>scenarios/</c>), joue le
    /// combat en temps réel avec le pont <see cref="RealtimeCombat"/> (sous-lot 9.2), transmet les touches, affiche l'arène, la caméra,
    /// l'interface et les retours. Le cœur reste seul juge du combat : cette classe ne calcule aucune règle. Sous-lot 10.4 : jeu en
    /// réseau (touche M) : héberger, avec un équipier robot si l'on veut, ou rejoindre ; le combat en coopération est mené par le serveur
    /// (<see cref="NetworkSession"/>), le même écran l'affiche. Sous-lot 12.8 : le jeu s'ouvre sur l'aventure dans les Vallons d'Aube
    /// (<see cref="AdventureSession"/> : zone du monde, PNJ, quêtes, objets, métiers, chef, sauvegarde locale) ; N passe aux scènes
    /// d'entraînement, puis revient à l'aventure.
    /// </summary>
    public sealed class EdsGame : MonoBehaviour
    {
        private static readonly string[] ScenarioFiles =
        {
            "vallons_demo.json",      // sous-lot 11.6 : coin des Vallons, combat positionné (déplacement simulé par le moteur)
            "meute_a_vaincre.json",   // sous-lot 11.1 : loups mortels, le combat se gagne
            "entrainement_garde_parade.json",
            "mesures/s1_mannequin.json",
            "mesures/s2_duel.json",
            "mesures/s3_meute.json",
            "mesures/s4_protection.json",
            "mesures/s5_briseur.json",
        };

        private const float MoveSpeed = 5f;
        private const float FloatingSeconds = 1.2f;
        private const float MessageSeconds = 3f;

        private sealed class Floating
        {
            public Vector3 World;
            public string Text;
            public Color Color;
            public float Born;
        }

        private string _repoRoot;
        private TrainingSession _session;
        private ICombatClient _combat;
        private int _scenario;
        private string _error;
        private bool _paused;
        private bool _help = true;
        private ArenaView _arena;
        private CameraRig _camera;
        private float _playerYaw;
        private bool _moving;
        private readonly List<Floating> _floating = new List<Floating>();
        private readonly List<KeyValuePair<float, string>> _messages = new List<KeyValuePair<float, string>>();

        // Sous-lot 9.4 : écran des talents ouvert (null sinon), pause d'avant son ouverture, build composé joué (null : build de référence).
        private TalentScreen _talents;
        private bool _pausedBeforeTalents;
        private CharacterBuild _customBuild;

        // Sous-lot 10.4 : jeu en réseau. Session (null : solo), menu ouvert, combat affiché, build demandé au serveur.
        private const string CoopScenario = "coop/meute_a_vaincre.json";   // sous-lot 11.1 : la meute meurt, la coopération se gagne
        private const string DuoScenario = "coop/vallons_duo.json";         // sous-lot 11.7 : les Vallons à deux, arrivée en plein combat
        private const double BotDelaySeconds = 10;
        private const string RobotBuild = "build.bastion.gardien";
        private NetworkSession _net;
        private readonly NetworkScreen _netScreen = new NetworkScreen();
        private bool _menuOpen;

        /// <summary>Caméra à la souris libre (curseur capturé) à cette image.</summary>
        private bool _freeLook;

        /// <summary>Curseur rendu par Échap (pour quitter le jeu dans l'éditeur, par exemple) ; un clic dans le jeu le reprend.</summary>
        private bool _cursorReleased;
        private NetworkCombat _shownCombat;
        private int _shownRoster;
        private string _netBuildId = "build.bastion.rempart";
        private bool _netTalentScreen;
        private string _lastCombat;

        private bool Network => _net != null;

        // Sous-lot 12.8 : aventure (null : entraînement ou réseau), ses écrans et ses repères, session de zone affichée.
        private AdventureSession _adventure;
        private readonly AdventureScreens _screens = new AdventureScreens();
        private WorldMarkers _markers;
        private int _shownSession;

        /// <summary>Distance au-delà de laquelle une créature de l'aventure n'a pas de barre au-dessus d'elle.</summary>
        private const float OverheadRange = 25f;

        /// <summary>
        /// Sauvegarde locale de l'aventure (cadrage Q10), hors du dossier des données recopiées au lancement sur Android (il est remplacé
        /// quand le jeu change).
        /// </summary>
        private static string SavePath => Path.Combine(Application.persistentDataPath, "sauvegardes", "vallons.json");

        // Sous-lot 11.8 : commandes au doigt (téléphone, simulateur d'appareils de l'éditeur, F8 pour les forcer) ; données prêtes.
        private readonly TouchControls _touch = new TouchControls();
        private bool _ready;

        // Essai réaliste 6 : l'interface en jeu en uGUI (« Fragments du Seuil »), et l'aide de l'aventure découpée en touches.
        private HudView _hud;
        private IReadOnlyList<HelpKey> _adventureHelp;

        // Essai réaliste 6, étape 2 : l'arbre des Éclats (talents) ouvrable dans l'aventure (T), et les écrans de l'aventure en uGUI.
        private TalentTreeView _talentView;
        private AdventureBook _book;

        /// <summary>Essai réaliste 6, étape 3 : niveau du personnage à l'image précédente (l'annonce d'un niveau gagné), 0 : inconnu.</summary>
        private int _shownLevel;

        // Essai réaliste 7 (demandes de Damien du 28 octobre 2026) : effets en particules, musique et ambiances, Charges et quêtes vues à
        // l'image précédente (l'éclat d'or, la fanfare).
        private Effects _effects;
        private StyleAudio _music;
        private Footsteps _footsteps;   // sous-lot V2.9 (2e vague)

        /// <summary>Sous-lot V2.2 : pluie, éclairs et tonnerre de l'aventure.</summary>
        private StyleWeather _weatherFx;
        private WeatherState _weatherState;
        private WeatherLook _weatherLook = WeatherLook.Clear;
        private long _weatherKey = long.MinValue;
        private WeatherKind? _weatherForced;
        private int _shownCharges = -1;

        /// <summary>Relecture de l'essai 7 : lieu et combat pour la musique, relus quatre fois par seconde.</summary>
        private AdventureMood _mood;
        private float _moodAge = 99f;
        private int _shownQuests, _shownLoot;
        private string _startupError;

        /// <summary>
        /// Sous-lot V1.9.8 : mise en scène du chapitre 1 (réveil, tutoriel, souvenir de la Rupture, Croc-Gris, silhouette, collier, « Quête
        /// passée »).
        /// </summary>
        private StoryStage _stage;

        /// <summary>Sous-lot V1.9.8 : Galeran ne porte pas l'équipement du joueur (sa lance et son armure neuve sont dessinées à part).</summary>
        private static readonly VisibleGear NoGear = new VisibleGear(WeaponLook.None, EdS.Data.Items.ItemRarity.Common, ShieldLook.None,
            EdS.Data.Items.ItemRarity.Common, false, EdS.Data.Items.ItemRarity.Common);

        private IEnumerator Start()
        {
            // Correctif du sous-lot 11.8 (essai sur le Note8) : une erreur de démarrage (shader absent du jeu construit…) s'affiche au lieu
            // de laisser l'écran bloqué sur « Préparation des données du jeu… ».
            try
            {
                ApplyMobileSettings();
                _camera = CameraRig.Create();
                _arena = new ArenaView();
                _hud = HudView.Create();
                // Essai réaliste 9 : réglages de la caméra dans la pause (souris libre ou clic maintenu, haut-bas inversé, sensibilité).
                _hud.OnSettings(() => _camera.SetClickLook(!_camera.ClickLook), () => _camera.SetInvertY(!_camera.InvertY),
                    step => _camera.SetSensitivity(_camera.Sensitivity + step));
                _talentView = TalentTreeView.Create();
                _book = AdventureBook.Create();
                _effects = Effects.Create();   // essai réaliste 7 : rien sans le shader des particules
                _arena.Effects = _effects;   // sous-lot V2.9.1 : braises et traînée des échos
                StyleWorld.Ambience(_camera.Camera);   // essai réaliste : ciel, lumière, brume, couleurs (rien sans catalogue)
                _stage = new StoryStage(_camera.Camera.transform);   // sous-lot V1.9.8
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _startupError = "Démarrage du jeu impossible : " + e.Message;
                yield break;
            }
            // Sous-lot 11.8 : sur Android, les données emportées sont d'abord recopiées dans le dossier de l'application.
            string root = null, problem = null;
            yield return GameFiles.Prepare(FindRepoRoot(), (r, e) =>
            {
                root = r;
                problem = e;
            });
            _repoRoot = root ?? FindRepoRoot();
            _ready = true;
            if (problem != null)
            {
                Load(0, null);
                _error = "Impossible de préparer les données du jeu : " + problem;
            }
            else StartAdventure();
        }

        /// <summary>
        /// Sous-lot 11.8 : réglages allégés sur téléphone : 60 images/s au plus, écran toujours allumé pendant le jeu, téléphone tenu à
        /// l'horizontale (dans les deux sens), sans ombres ni anticrénelage.
        /// </summary>
        private static void ApplyMobileSettings()
        {
            if (!Application.isMobilePlatform) return;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
            QualitySettings.shadows = ShadowQuality.Disable;
            QualitySettings.antiAliasing = 0;
        }

        /// <summary>
        /// Dossier du dépôt (données <c>data/v0</c> et scénarios) : le parent du projet Unity dans l'éditeur ; pour un jeu construit
        /// (sous-lot 10.4, essai à deux postes), le premier dossier parent qui les contient (construire dans <c>unity/Build/</c>).
        /// </summary>
        private static string FindRepoRoot()
        {
            string fallback = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            for (int i = 0; i < 6 && !string.IsNullOrEmpty(dir); i++)
            {
                if (Directory.Exists(Path.Combine(dir, "data", "v0")) && Directory.Exists(Path.Combine(dir, "scenarios"))) return dir;
                dir = Path.GetDirectoryName(dir);
            }
            return fallback;
        }

        private void OnDestroy() => StopNetwork();

        private void OnApplicationQuit()
        {
            SaveAdventure(false);
            StopNetwork();
        }

        // --- Aventure (sous-lot 12.8) --------------------------------------------------------------------------------------------

        /// <summary>Ouvre l'aventure (sauvegarde reprise, ou nouvelle partie) ; si le monde ne se charge pas, l'entraînement à la place.</summary>
        private void StartAdventure()
        {
            _error = null;
            try
            {
                _adventure = AdventureSession.Load(Path.Combine(_repoRoot, "data", "v0"), Path.Combine(_repoRoot, "scenarios"), SavePath);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _adventure = null;
                Load(0, null);
                Say("Aventure indisponible : " + e.Message);
                return;
            }
            _markers = new WorldMarkers(_adventure);
            _shownZone = _adventure.CurrentZone;
            _adventureHelp = null;
            _shownLevel = 0;
            _shownQuests = _adventure.QuestsAnnounced;
            _shownLoot = _adventure.LootAnnounced;
            if (_music == null && _camera != null)
            {
                // Revue Codex de la PR #63 : la musique part dès l'ouverture de l'aventure ; l'écouteur de la caméra doit exister avant elle
                // (sinon elle reste muette jusqu'au premier bruitage).
                EnsureListener();
                _music = StyleAudio.Create(_camera.Camera.transform);   // essai réaliste 7
            }
            if (_weatherFx == null) _weatherFx = StyleWeather.Create();   // sous-lot V2.2 : pluie, éclairs, tonnerre
            _stage?.Begin(_adventure);   // sous-lot V1.9.8 : le réveil d'une nouvelle partie
            ShowAdventureSession();
            if (_adventure.SaveProblem != null) Say("Sauvegarde illisible, nouvelle partie : " + _adventure.SaveProblem);
        }

        /// <summary>Affiche la session de zone en cours (au départ, après une halte ou une reprise).</summary>
        private void ShowAdventureSession()
        {
            // Intérieurs : une autre zone (une porte passée) a ses propres repères ; un fondu au noir marque le passage.
            if (_markers != null && _shownZone != _adventure.CurrentZone)
            {
                _markers.Destroy();
                _markers = new WorldMarkers(_adventure);
                PaintedMap.Forget();
                if (_shownZone != null) _fade = 1f;
            }
            _shownZone = _adventure.CurrentZone;
            // Sous-lot V1.1 : un voyage rapide passe aussi par un fondu au noir.
            if (_adventure.Travels != _shownTravels) _fade = 1f;
            _shownTravels = _adventure.Travels;
            _combat = _adventure.Combat;
            // Sous-lot V1.9.8 : l'Écho de la Rupture est une silhouette de lumière (avant la couleur des chefs).
            _arena.Paint = id => _adventure == null ? (Color?)null
                : _stage?.Paint(id) ?? (_adventure.IsBoss(id) ? new Color(0.55f, 0.2f, 0.75f) : (Color?)null);
            _arena.Populate(_combat, _adventure.Map);   // Vallons agrandis : routes et champs du décor
            // Sous-lot V1.9.8 : entrer dans le souvenir ou en sortir passe par un fondu blanc (la mise en scène), pas par le noir des portes.
            if (_stage != null && _stage.ZoneChanged(_adventure)) _fade = 0f;
            _shownSession = _adventure.Opened;
            _shownCharges = -1;   // relecture de l'essai 7 : pas d'éclat d'or au début d'une session
            _moodAge = 99f;
            _paused = false;
            _floating.Clear();
            _playerYaw = 0f;
        }

        /// <summary>Intérieurs : zone affichée (une autre après une porte : repères refaits, fondu au noir).</summary>
        private string _shownZone;

        /// <summary>Sous-lot V2.2.1 : les compagnons d'affichage (la bête liée à un objet du sac).</summary>
        private CompanionsView _companions;

        /// <summary>Sous-lot V2.5 : failles du Seuil, hameau pillé, cloche (affichage des événements du monde).</summary>
        private WorldEventsView _worldEvents;

        // Sous-lot V2.9.1 : créatures sorties du Seuil (reprises à chaque image, sans allocation), chefs déjà tués.
        private readonly HashSet<string> _fromSeuil = new HashSet<string>();
        private Func<string, bool> _slainChief;

        private bool KnowsFact(string fact) => _adventure != null && _adventure.World.Narrative != null && _adventure.World.Narrative.Knows(fact);

        private void SeuilCreatures(AdventureSession a)
        {
            _fromSeuil.Clear();
            foreach (WorldEventView v in a.World.WorldEvents)
                if (v.Rift != null)
                    for (int i = 0; i < v.Creatures.Count; i++) _fromSeuil.Add(v.Creatures[i]);
            IReadOnlyList<ScarView> scars = a.Scars;
            for (int i = 0; i < scars.Count; i++)
                for (int j = 0; j < scars[i].Haunts.Count; j++) _fromSeuil.Add(scars[i].Haunts[j]);
        }

        /// <summary>Sous-lot V2.6 : cicatrices du Seuil (couleurs fanées, brume froide, pointes sombres).</summary>
        private ScarsView _scars;

        /// <summary>Relecture de la PR #102 : liste des compagnons à montrer, réutilisée à chaque image.</summary>
        private readonly List<CompanionDefinition> _companionList = new List<CompanionDefinition>();

        /// <summary>Sous-lot V1.1 : voyages rapides déjà montrés (un fondu au noir à chaque nouveau).</summary>
        private int _shownTravels;

        /// <summary>Intérieurs : opacité du fondu au noir après une porte (de 1 à 0 en <see cref="FadeSeconds"/>).</summary>
        private float _fade;

        private const float FadeSeconds = 0.7f;

        /// <summary>Un écran (sac, journal, atelier) ou un dialogue de l'aventure couvre les commandes tactiles (revue Codex de la PR #49).</summary>
        private bool AdventureCovered => _adventure != null && (_screens.Open != AdventureScreens.Panel.None || _adventure.Dialogue != null || TalentsOpen);

        /// <summary>Essai réaliste 6, étape 2 : l'arbre des Éclats est ouvert.</summary>
        private bool TalentsOpen => _talentView != null && _talentView.IsOpen;

        /// <summary>
        /// Salle d'attente du réseau affichée (avant, entre et après les combats) : ses boutons (« Je suis prêt », « Quitter la partie »)
        /// se cliquent, la caméra à la souris libre n'y prend pas le curseur (revue Codex de la PR #57). Même condition que son affichage.
        /// </summary>
        private bool LobbyShown => Network && _talents == null && (_net.Client == null || _net.Client.Status != ClientStatus.Combat
            && _net.Client.Status != ClientStatus.Disconnected && _net.Client.Status != ClientStatus.Reconnecting);

        /// <summary>Quitte l'aventure (sauvegardée) : l'entraînement ou le réseau prend la place.</summary>
        private void LeaveAdventure()
        {
            if (_adventure == null) return;
            SaveAdventure(false);
            _markers?.Destroy();
            _markers = null;
            _shownZone = null;
            _adventure = null;
            // Relecture de l'essai 7 : la musique est détruite ; la prochaine aventure la recrée (ses ambiances comprises).
            _music?.Destroy();
            _music = null;
            _footsteps?.Destroy();
            _footsteps = null;
            _weatherFx?.Destroy();   // sous-lot V2.2
            _companions?.Destroy();   // sous-lot V2.2.1
            _companions = null;
            _worldEvents?.Destroy();   // sous-lot V2.5
            _worldEvents = null;
            _scars?.Destroy();   // sous-lot V2.6
            _scars = null;
            _weatherFx = null;
            StyleWorld.SetWeather(null);
            _talentView?.Close();
            PaintedMap.Forget();
            _screens.Close();
            _stage?.Reset();   // sous-lot V1.9.8 : décor, sons et ambiance du souvenir rendus
            _touch.Highlight = null;
            _arena.Paint = null;
            _combat = null;
            _camera.Indoors = false;   // essai réaliste 9 (relecture) : l'entraînement et le réseau retrouvent la distance de dehors
        }

        /// <summary>
        /// Sauvegarde l'aventure en cours, même après une défaite pas encore reprise (revue Codex de la PR #49 : les acquis d'avant la chute
        /// ne se perdent pas) ; une erreur s'affiche.
        /// </summary>
        private void SaveAdventure(bool announce)
        {
            if (_adventure == null) return;
            try
            {
                _adventure.Save();
                if (announce) Say(_adventure.Text("text.ui.saved"));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Say("Sauvegarde impossible : " + e.Message);
            }
        }

        /// <summary>Touche de chiffre 1 à 9 pressée à cette image (rang 0 à 8), ou -1.</summary>
        private static int DigitPressed()
        {
            KeyCode[] digits = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9 };
            KeyCode[] pad = { KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.Keypad4, KeyCode.Keypad5, KeyCode.Keypad6, KeyCode.Keypad7, KeyCode.Keypad8, KeyCode.Keypad9 };
            for (int i = 0; i < digits.Length; i++)
                if (Input.GetKeyDown(digits[i]) || Input.GetKeyDown(pad[i])) return i;
            return -1;
        }

        /// <summary>
        /// Une image de l'aventure. Le monde s'arrête (jeu en solo) pendant un dialogue, un écran ouvert, la pause ou après une défaite ;
        /// sinon les touches de combat, la marche et les actions (F parler ou récolter, P potion) passent au cœur, et le temps avance.
        /// </summary>
        /// <summary>Relecture de la PR #75 : dernier équipement visible calculé (rendu tel quel tant qu'il ne change pas).</summary>
        private VisibleGear _visibleGear;

        /// <summary>
        /// Sous-lot V2.2 : pluie, éclairs et tonnerre de l'aventure. Revue Codex de la PR #101 : appelée aussi quand l'écran des talents ou le
        /// menu couvre l'aventure (<paramref name="covered"/>, sons plus bas), sans faire avancer le monde : un éclair s'éteint, la pluie suit
        /// son temps.
        /// </summary>
        private void UpdateWeatherFx(bool covered)
        {
            AdventureSession a = _adventure;
            if (a == null || _weatherFx == null || _camera == null) return;
            _weatherFx.Update(_weatherLook, _weatherState, _weatherKey, _camera.Camera.transform.position, a.Indoors,
                StyleWorld.Memory || _stage != null && _stage.Hush(a), covered, Time.deltaTime);
        }

        private void UpdateAdventure()
        {
            AdventureSession a = _adventure;
            if (Input.GetKeyDown(KeyCode.N) || _touch.NextScenePressed)
            {
                LeaveAdventure();
                Load(0, null);
                return;
            }
            // Revue Codex de la PR #49 : une image où un dialogue était ouvert ne lit pas les commandes de combat, même si la réponse choisie
            // l'a fermé (la touche 1 à 4 de la réponse ne lance pas aussi une compétence).
            bool talking = a.Dialogue != null;
            if (talking)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) a.CloseDialogue();
                else if (DigitPressed() is int answer && answer >= 0) a.Choose(answer);
            }
            else if (TalentsOpen)
            {
                // Essai réaliste 6, étape 2 : T ou Échap ferme l'arbre sans garder les points.
                if (Input.GetKeyDown(KeyCode.T) || Input.GetKeyDown(KeyCode.Escape)) _talentView.Cancel();
            }
            else if (_screens.Open != AdventureScreens.Panel.None)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) _screens.Close();
            }
            else if (Input.GetKeyDown(KeyCode.Escape) || _touch.PausePressed) _paused = !_paused;
            // Sous-lot V1.9.8 : passer le réveil, revivre (V) ou passer (X) le souvenir.
            bool calm = !TalentsOpen && !_paused && _screens.Open == AdventureScreens.Panel.None;   // un dialogue reste possible
            _stage?.ReadInput(a, calm && !talking, calm);
            if (!TalentsOpen)
            {
                if (Input.GetKeyDown(KeyCode.I) || _touch.BagPressed) _screens.Toggle(AdventureScreens.Panel.Inventory);
                if (Input.GetKeyDown(KeyCode.J) || _touch.JournalPressed) _screens.Toggle(AdventureScreens.Panel.Journal);
                if (Input.GetKeyDown(KeyCode.K) || _touch.WorkshopPressed) _screens.Toggle(AdventureScreens.Panel.Crafting);
                if (Input.GetKeyDown(KeyCode.L) || _touch.MapPressed) _screens.Toggle(AdventureScreens.Panel.Map);   // sous-lot 12.9d
                // Sous-lot V1.5 : B ouvre l'écran des compétences (K est l'atelier) ; elles se changent hors combat, effet à la halte.
                if (Input.GetKeyDown(KeyCode.B)) _screens.Toggle(AdventureScreens.Panel.Skills);
                // Essai réaliste 6, étape 2 (essai de Damien du 27 octobre 2026 : « l'arbre des compétences […] je ne peux pas l'ouvrir ») :
                // T ouvre l'arbre des Éclats sur les talents du personnage à son niveau ; ils se gardent hors combat, effet à la halte.
                if (!talking && !a.MustContinue && (Input.GetKeyDown(KeyCode.T) || _touch.TalentsPressed)) OpenAdventureTalents(a);
            }
            if (Input.GetKeyDown(KeyCode.F6)) SaveAdventure(true);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || _touch.RestartPressed)
            {
                // La halte et la reprise sauvegardent : un disque plein ou inaccessible s'affiche, la partie reste où elle en est.
                try
                {
                    if (a.MustContinue) a.Continue();
                    else if (!a.Rest()) Say(a.Text("text.ui.cannot_rest"));
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is NotSupportedException)
                {
                    Debug.LogException(e);
                    Say(string.Format(a.Text("text.ui.save_failed"), e.Message));
                }
            }
            if (a.Opened != _shownSession) ShowAdventureSession();
            if (_fade > 0f) _fade = Mathf.Max(0f, _fade - Time.deltaTime / FadeSeconds);
            // Sous-lot V1.1 : la boutique suit l'écran (Échap, I, J, K, L la ferment) ; le bouton Parler la ferme pour le dialogue.
            if (_screens.Open == AdventureScreens.Panel.Merchant && a.Shop == null) _screens.Close();
            else if (_screens.Open != AdventureScreens.Panel.Merchant && a.Shop != null) a.CloseShop();
            // Sous-lot V1.7 : de même pour le tableau de la guilde (le bouton Parler le ferme pour le dialogue de la maîtresse).
            if (_screens.Open == AdventureScreens.Panel.Guild && !a.GuildOpen) _screens.Close();
            else if (_screens.Open != AdventureScreens.Panel.Guild && a.GuildOpen) a.CloseGuild();
            bool frozen = _paused || talking || a.Dialogue != null || _screens.Open != AdventureScreens.Panel.None || a.MustContinue || TalentsOpen;
            frozen = frozen || _stage != null && _stage.Blocking;   // sous-lot V1.9.8 : le réveil
            if (!frozen && (Input.GetKeyDown(KeyCode.F) || _touch.ActPressed)) a.Interact();
            if (a.Shop != null) _screens.Show(AdventureScreens.Panel.Merchant);   // sous-lot V1.1 : F près d'un marchand ouvre sa boutique
            if (a.GuildOpen) _screens.Show(AdventureScreens.Panel.Guild);   // sous-lot V1.7 : F près de la maîtresse ouvre son tableau
            if (!frozen && (Input.GetKeyDown(KeyCode.P) || _touch.PotionPressed) && a.Drink())
            {
                StyleLook.Play(Audio, StyleSound.Potion);   // essai de style : son
                if (_arena.Feet(_combat.PlayerId) is Vector3 feet) _effects?.Healed(feet);   // essai réaliste 7 : éclats verts
            }
            if (Input.GetKeyDown(KeyCode.C) || _touch.ViewPressed) _camera.Toggle(_playerYaw);
            _camera.Nudge(_touch.YawDelta, _touch.ViewDelta, _touch.PitchDelta);

            PlayerState player = _combat.Player();
            frozen = frozen || a.Dialogue != null || a.Shop != null || a.GuildOpen;   // un dialogue (ou une boutique, ou le tableau de la guilde) ouvert par F à cette image
            if (frozen)
            {
                _combat.SetGuard(false);
                _combat.SetMove(0f, 0f);
                _moving = false;
            }
            else
            {
                ReadCombatInput();
                Move(player);
                a.Advance(Time.deltaTime);
                foreach (CombatFeedback feedback in _combat.DrainFeedback()) Show(feedback);
            }
            foreach (string message in a.DrainMessages())
            {
                Say(message);
                _stage?.Notice(a, message);   // sous-lot V1.9.8 : « Quête passée » en grand
            }
            // Essai réaliste 7 : petite fanfare quand une quête est accomplie.
            if (a.QuestsAnnounced > _shownQuests) UiSounds.Play(UiCue.QuestDone);
            _shownQuests = a.QuestsAnnounced;
            if (a.LootAnnounced > _shownLoot) StyleLook.Play(Audio, StyleSound.Coin, 0.6f);   // et le bruit des pièces pour un butin
            _shownLoot = a.LootAnnounced;
            _markers.Sync(a);
            _markers.Walk(a, Time.deltaTime, _arena);   // sous-lot V2.1 : les villageois rentrent chez eux la nuit ; V2.3 : et suivent leur journée, corps des pièces compris
            player = _combat.Player();
            _stage?.BeforeSync();
            // Sous-lot V2.9.1 : échos plus vifs la nuit et sur les cicatrices ; décision de Damien du 7 novembre 2026 : ce qui sort du Seuil
            // (créatures des failles, échos rôdeurs) et les chefs déjà tués sont des échos.
            SeuilCreatures(a);
            if (_slainChief == null) _slainChief = id => _adventure != null && EchoLooks.ChiefSlain(_adventure.World.Definition, KnowsFact, id);
            _arena.Echoes(a.IsNight, a.InScene ? null : a.Scars, _fromSeuil, _slainChief);
            _arena.Sync(_combat, player, null);
            // Sous-lot V1.2 : arme, bouclier et cape portés. Relecture de la PR #75 : le même objet tant que rien ne change (pas d'allocation
            // à chaque image).
            _visibleGear = GearLooks.For(a.World.Inventory, _visibleGear);
            _arena.ShowGear(a.InScene ? NoGear : _visibleGear);
            // Sous-lot V1.9.8 : réveil, souvenir, Croc-Gris, silhouette, collier ; la fin du souvenir (retour au pré).
            _stage?.AfterSync(a, _combat, _arena, _effects, _camera, Time.deltaTime,
                !_paused && !TalentsOpen && _screens.Open == AdventureScreens.Panel.None && a.Dialogue == null);
            _touch.Highlight = _stage?.TouchHighlight(a);
            // Essai réaliste (27 octobre 2026) : les PNJ se tournent vers le joueur qui approche.
            if (_arena.Player != null) _markers.Face(_arena.Player.position, Time.deltaTime);
            _camera.Indoors = a.Indoors;   // essai réaliste 9 : dans une pièce, la caméra reste dans la maison
            _arena.Village(a.World.Zone.Time.Minute);   // sous-lot V2.3 : les villageois du décor font leurs courses (avant SyncSpace, qui les fait marcher)
            if (_arena.Player != null) _camera.Follow(_arena.Player.position, _playerYaw, _moving, Time.deltaTime, _freeLook);
            // Sous-lot V2.2.1 : le compagnon d'affichage (Pain-d'Épice suit tant qu'il est dans le sac ; ni combat, ni sauvegarde).
            if (_companions == null) _companions = new CompanionsView();
            Companions.Shown(a.World.Inventory, CompanionTrial.Forced, _companionList);   // relecture de la PR #102 : sans allocation
            _companions.Sync(_companionList, _arena.Player, a.InScene, a.World.InCombat, a.IsNight,
                _shownZone + "/" + _shownTravels, _paused ? 0f : Time.deltaTime);
            // Sous-lot V2.5 : événements du monde (faille, pillage, cloche).
            if (_worldEvents == null) _worldEvents = new WorldEventsView { Effects = _effects };   // V2.9.1 : lueurs aspirées
            _worldEvents.Sync(a, _paused ? 0f : Time.deltaTime);
            // Sous-lot V2.6 : cicatrices du Seuil, couleurs fanées autour de la caméra.
            if (_scars == null) _scars = new ScarsView();
            _scars.Sync(a, _camera.Camera.transform.position, _paused ? 0f : Time.deltaTime);
            _arena.SyncSpace(_combat, _camera.Camera.transform.position, Time.deltaTime);
            _arena.Weather(_weatherLook);   // sous-lot V2.2 : flaques, villageois qui s'abritent (avant la nuit, qui les fait rentrer)
            _arena.Night(_camera.Camera.transform.position, a.IsNight, Time.deltaTime);   // sous-lot V2.1 : lanternes, fenêtres, feux follets
            UpdateWeatherFx(_paused);   // sous-lot V2.2 : pluie, éclairs, tonnerre
            SyncGestures();
            // Sous-lot V2.9 : voix des bêtes (hurlements la nuit et par brume, brame à l'aube et au crépuscule, sanglier, lièvre), muettes en
            // pause, pendant une scène et dans une maison.
            _arena.Voices(_camera.Camera.transform.position, a.World.Zone.Time.Minute, a.IsNight, _weatherState.Kind == WeatherKind.Mist,
                _paused || a.InScene || a.Indoors ? 0f : Time.deltaTime);
            // Sous-lot V2.9 (2e vague) : bêtes de la ferme et leurs cris ; pas du personnage selon le sol.
            if (_arena.Player != null)
            {
                _arena.Life(_arena.Player.position, a.World.Zone.Time.Minute, _paused || a.InScene ? 0f : Time.deltaTime);
                if (_footsteps == null && StyleCatalog.Loaded != null) _footsteps = new Footsteps(_camera.Camera.transform);
                _footsteps?.Update(_arena, _arena.Player.position - Vector3.up * ArenaView.FeetOffset, (_mood.Places & AmbiencePlace.Forest) != 0,
                    !_paused && !a.InScene, Time.deltaTime);
            }
            // Essai réaliste 7 : musique et ambiances d'après le lieu et le combat (plus bas pendant la pause ou un écran ouvert). Relecture :
            // le lieu et le combat sont relus quatre fois par seconde (les fondus durent deux secondes et demie), les volumes à chaque image.
            if (_music != null)
            {
                _moodAge += Time.deltaTime;
                if (_moodAge >= 0.25f)
                {
                    _mood = a.Mood().WithRain(_weatherLook.Rain);   // relecture de la PR #101 : la pluie montrée (essai forcé compris)
                    _moodAge = 0f;
                }
                _music.Hushed = _stage != null && _stage.Hush(a);   // sous-lot V1.9.8 : le chant du souvenir
                _music.Story = a.InScene;   // sous-lot V2.9 (2e vague) : la musique des scènes de l'histoire
                _music.Update(_mood, _paused || _screens.Open != AdventureScreens.Panel.None || TalentsOpen, Time.deltaTime);
            }
        }

        /// <summary>Indication de l'action possible ici (parler, récolter, faire halte, reprendre), ou null.</summary>
        private string AdventureHint()
        {
            AdventureSession a = _adventure;
            if (a.MustContinue) return string.Format(a.Text("text.ui.continue_hint"), a.LastSafePoint);
            if (a.Dialogue != null || _screens.Open != AdventureScreens.Panel.None) return null;
            if (a.RiftInReach != null) return a.Text("text.ui.event.rift.close");   // sous-lot V2.5 : refermer la faille
            if (a.ScarInReach != null) return a.Text("text.ui.scar.purify");   // sous-lot V2.6 : purifier une cicatrice
            if (a.NpcInReach is EdS.Data.Story.Npc npc)
                return string.Format(a.Text(a.IsGuildmistress(npc.Id) ? "text.ui.guild_hint" : a.IsMerchant(npc.Id) ? "text.ui.trade_hint" : "text.ui.talk_hint"), a.Text(npc.Name));
            if (a.NodeInReach is EdS.Data.Crafting.GatheringNode node) return string.Format(a.Text("text.ui.gather_hint"), a.Item(node.Item).Name);
            if (a.DoorInReach is EdS.Data.Zones.Door door) return string.Format(a.Text("text.ui.door_hint"), door.Name);   // intérieurs
            if (a.CanRest) return string.Format(a.Text("text.ui.rest_hint"), a.LastSafePoint);
            if (a.WorkshopHere is string workshop) return string.Format(a.Text("text.ui.craft_hint"), workshop);
            return null;
        }

        private void Load(int scenario, string buildId)
        {
            _scenario = scenario;
            _error = null;
            _floating.Clear();
            try
            {
                string data = Path.Combine(_repoRoot, "data", "v0");
                string file = Path.Combine(_repoRoot, "scenarios", ScenarioFiles[scenario]);
                if (buildId != null) _customBuild = null;   // un build de référence choisi (F1 à F5) remplace le build composé
                _session = TrainingSession.Load(data, file, buildId ?? _session?.Build?.Id ?? "build.bastion.rempart");
                if (_customBuild != null) _session = _session.WithCustomBuild(_customBuild, TalentScreenName);
                Restart();
            }
            catch (Exception e)
            {
                _combat = null;
                _error = $"Impossible de charger le jeu : {e.Message}\n\nDépôt attendu : {_repoRoot}\n(le projet Unity doit rester dans le dossier unity/ du dépôt)";
                Debug.LogException(e);
            }
        }

        /// <summary>Sous-lot 12.8 : scène d'entraînement suivante ; après la dernière, retour à l'aventure.</summary>
        private void NextScene()
        {
            if (_scenario + 1 >= ScenarioFiles.Length) StartAdventure();
            else Load(_scenario + 1, null);
        }

        private void Restart()
        {
            _paused = false;
            _combat = _session.Start();
            _arena.Populate(_combat);
            _playerYaw = 0f;
            Say($"Séance : {ScenarioName()} — build {BuildName()}");
        }

        private string ScenarioName() => Path.GetFileNameWithoutExtension(ScenarioFiles[_scenario]);

        private string BuildName() => _session?.BuildName ?? "du scénario";

        /// <summary>Nom du build composé dans l'écran des talents.</summary>
        private const string TalentScreenName = "Personnalisé";

        private void OpenTalents()
        {
            _pausedBeforeTalents = _paused;
            _paused = true;
            _netTalentScreen = false;
            _talents = new TalentScreen(_session.CreateTalentEditor(), _session.AvailableBuilds(), _session.Build?.Id);
        }

        /// <summary>Sous-lot 10.4 : talents du build demandé au serveur (salle d'attente), à partir de sa référence.</summary>
        private void OpenNetworkTalents()
        {
            try
            {
                // Revue Codex de la PR #21 (tour 4) : on repart du build que le serveur a accepté, jamais d'une demande refusée.
                BuildChoice accepted = AcceptedNetworkBuild;
                TrainingSession reference = TrainingSession.Create(_session.Data, _session.Setup, accepted.Reference, _session.PlayerId);
                if (accepted.Talents != null) reference = reference.WithCustomBuild(WithTalents(reference.Player.Build, accepted.Talents), TalentScreenName);
                _talents = new TalentScreen(reference.CreateTalentEditor(), reference.AvailableBuilds(), accepted.Reference);
                _netTalentScreen = true;
            }
            catch (Exception e)
            {
                Say("Talents indisponibles : " + e.Message);
                Debug.LogException(e);
            }
        }

        private static CharacterBuild WithTalents(CharacterBuild build, IReadOnlyDictionary<string, int> talents) =>
            new CharacterBuild(build.ClassId, build.Level, build.Attributes, talents, build.ActiveSkills, build.Ultimate, build.PassiveSkills);

        private void CloseTalents()
        {
            _talents = null;
            if (!_netTalentScreen) _paused = _pausedBeforeTalents;
            _netTalentScreen = false;
        }

        /// <summary>Joue le build composé : nouvelle séance (même scénario) avec ce build, relancée du début ; en réseau, demandé au serveur.</summary>
        private void PlayTalents()
        {
            if (_netTalentScreen)
            {
                string reference = _talents.ReferenceId ?? AcceptedNetworkBuild.Reference;
                // Revue Codex de la PR #21 : la demande ne part qu'en salle d'attente ; le serveur l'accepte ou la refuse (tour 4 : le build
                // retenu ne change qu'à son accord, voir AcceptedNetworkBuild).
                if (!SetNetworkBuild(reference, new Dictionary<string, int>(_talents.Editor.Build.TalentRanks), out string why))
                {
                    _talents.Say(why, true);
                    return;
                }
                CloseTalents();
                return;
            }
            CharacterBuild build = _talents.Editor.Build;
            try
            {
                _session = _session.WithCustomBuild(build, TalentScreenName);
            }
            catch (Exception e)
            {
                _talents.Say("Build refusé : " + e.Message, true);
                return;
            }
            _customBuild = build;
            _talents = null;
            Restart();
        }

        private void Say(string message)
        {
            // Les messages peuvent porter du texte venu du serveur ou de l'autre joueur : jamais de balise de texte enrichi.
            _messages.Add(new KeyValuePair<float, string>(Time.time, Hud.Plain(message)));
            if (_messages.Count > 5) _messages.RemoveAt(0);
        }

        /// <summary>
        /// Revue Codex de la PR #36 : jeu suspendu ou passé en arrière-plan (verrouillage, accueil, autre application) : les commandes au
        /// doigt repartent de zéro, rien ne reste tenu (marche, garde) au retour.
        /// </summary>
        private void OnApplicationPause(bool paused)
        {
            if (!paused) return;
            _touch.Reset();
            SaveAdventure(false);   // sous-lot 12.8 : un téléphone peut fermer le jeu en arrière-plan sans prévenir
        }

        private void OnApplicationFocus(bool focus)
        {
            if (!focus) _touch.Reset();
        }

        /// <summary>Le curseur est rendu quand le jeu s'arrête (caméra à la souris libre).</summary>
        private void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>Relecture de la PR #99 : minute du monde de la dernière lumière posée (jour × 1440 + minute ; -1 hors de l'aventure).</summary>
        private long _lightKey = long.MinValue;

        private void Update()
        {
            if (!_ready || _startupError != null) return;   // sous-lot 11.8 : données en préparation, ou démarrage impossible
            // Essai réaliste : les curseurs du catalogue réglés pendant le jeu. Sous-lot V2.1 : à chaque image, la lumière de l'heure du monde
            // (nuit bleue, lanternes, aube et crépuscule) dans l'aventure ; le plein jour ailleurs (entraînement, réseau).
            // Relecture de la PR #99 : la lumière n'est recalculée que quand la minute du monde change (7,5 s de jeu), ou à chaque image dans
            // l'éditeur (curseurs du catalogue) ; -1 hors de l'aventure (plein jour).
            long lightKey = _adventure != null ? _adventure.World.Zone.Time.Day * 1440 + _adventure.World.Zone.Time.Minute : -1;
            // Sous-lot V2.2 : le temps, relu seulement quand la minute du monde change (ou le temps forcé par le menu d'essai).
            if (lightKey != _weatherKey || StyleWeather.Forced != _weatherForced)
            {
                _weatherKey = lightKey;
                _weatherForced = StyleWeather.Forced;
                if (_adventure == null) _weatherLook = WeatherLook.Clear;
                else
                {
                    _weatherState = StyleWeather.Forced is WeatherKind forced ? WeatherCycle.Typical(forced) : _adventure.Weather();
                    _weatherLook = WeatherCycle.Look(_weatherState, _adventure.Light());
                }
                StyleWorld.SetWeather(_adventure != null ? _weatherLook : (WeatherLook?)null);
                _lightKey = long.MinValue;   // la lumière est réappliquée avec le nouveau temps
            }
            if (Application.isEditor || lightKey != _lightKey)
            {
                _lightKey = lightKey;
                StyleWorld.SetDayLight(_adventure != null ? _adventure.Light() : (DayLight?)null);
            }
            if (Input.GetKeyDown(KeyCode.F8) && _touch.ToggleForced()) Say(_touch.Active ? "Commandes tactiles affichées (F8 : cacher)." : "Commandes tactiles cachées.");
            _touch.Update(Network, _adventure != null, AdventureCovered);
            // Caméra à la souris libre (choix de Damien du 26 octobre 2026, « comme ESO ») : hors des écrans et des menus, la souris tourne la
            // caméra, curseur caché ; Alt tenu rend le curseur pour cliquer.
            // Échap rend le curseur (dans l'éditeur, pour arrêter le jeu) ; un clic dans le jeu le reprend.
            bool covered = _talents != null || _menuOpen || _paused || AdventureCovered || LobbyShown;
            covered = covered || _stage != null && _stage.WantsCursor(_adventure);   // sous-lot V1.9.8 : boutons du souvenir proposé
            if (Input.GetKeyDown(KeyCode.Escape)) _cursorReleased = true;
            else if (_cursorReleased && !covered && Input.GetMouseButtonDown(0)) _cursorReleased = false;
            // Essai réaliste 9 (relecture) : réglage « clic maintenu » (World of Warcraft) : le curseur reste visible, il n'est caché que
            // pendant que le clic gauche tourne la vue.
            bool cursorWanted = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt) || _cursorReleased || _camera.ClickLook;
            _freeLook = PointerInput.FreeLook(Input.touchCount, _touch.Active, cursorWanted, covered, Application.isFocused);
            _camera.Blocked = covered || _touch.Active;
            bool hidden = _freeLook || _camera.Dragging;
            Cursor.lockState = hidden ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !hidden;
            // Écran des talents ouvert : le combat est en pause, seules T et Échap (fermer) comptent ; la souris sert à l'écran. En réseau,
            // la partie continue d'avancer (le serveur ne s'arrête pas), sans entrée de combat.
            if (_talents != null)
            {
                if (Input.GetKeyDown(KeyCode.T) || Input.GetKeyDown(KeyCode.Escape)) CloseTalents();
                if (Network) UpdateNetwork(false);
                UpdateWeatherFx(true);   // revue Codex de la PR #101 : l'éclair s'éteint même écran ouvert
                return;
            }
            // Sous-lot 10.4 : menu du réseau (M) ; ouvert, il prend la souris et le clavier, la partie en réseau continue.
            // Une lettre M tapée dans un champ du menu (nom, adresse) ne le ferme pas.
            // Relecture de l'essai 6 : pas de menu par-dessus l'arbre des Éclats (ses clics iraient aussi à l'arbre).
            if (!TalentsOpen && ((Keys.MenuPressed && !(_menuOpen && GUIUtility.keyboardControl != 0)) || (_touch.MenuPressed && !_menuOpen))) _menuOpen = !_menuOpen;
            if (_menuOpen)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) _menuOpen = false;
                if (Network) UpdateNetwork(false);
                UpdateWeatherFx(true);   // revue Codex de la PR #101 : l'éclair s'éteint même menu ouvert
                return;
            }
            if (Input.GetKeyDown(KeyCode.H) || _touch.HelpPressed) _help = !_help;
            if (_adventure != null)
            {
                UpdateAdventure();
                return;
            }
            if (Network)
            {
                UpdateNetwork(true);
                return;
            }
            if (Input.GetKeyDown(KeyCode.N))
            {
                NextScene();
                return;
            }
            if (_combat == null) return;

            if (Input.GetKeyDown(KeyCode.T))
            {
                OpenTalents();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape) || _touch.PausePressed) _paused = !_paused;
            if (Input.GetKeyDown(KeyCode.Backspace) || _touch.RestartPressed) Restart();
            if (_touch.NextScenePressed)
            {
                NextScene();
                return;
            }
            if (Input.GetKeyDown(KeyCode.C) || _touch.ViewPressed) _camera.Toggle(_playerYaw);
            _camera.Nudge(_touch.YawDelta, _touch.ViewDelta, _touch.PitchDelta);
            IReadOnlyList<ReferenceBuild> builds = _session.AvailableBuilds();
            KeyCode[] buildKeys = { KeyCode.F1, KeyCode.F2, KeyCode.F3, KeyCode.F4, KeyCode.F5 };
            for (int i = 0; i < buildKeys.Length && i < builds.Count; i++)
                if (Input.GetKeyDown(buildKeys[i])) Load(_scenario, builds[i].Id);
            if (_combat == null) return;

            PlayerState player = _combat.Player();
            // Revue Codex de la PR #14 : en pause (ou séance finie), aucune entrée de combat n'est enregistrée ; sinon une parade ou une
            // compétence pressée pendant la pause partirait au premier tick de la reprise, avec un timing préparé à l'arrêt.
            if (!_paused) ReadCombatInput();
            Move(player);

            if (!_paused)
            {
                _combat.Advance(Time.deltaTime);
                foreach (CombatFeedback feedback in _combat.DrainFeedback()) Show(feedback);
                if (_combat.IsFinished && !_paused)
                {
                    Say($"{Outcome(_combat.Result, _combat.Player().View.Team)} Retour arrière pour recommencer.");
                    _paused = true;
                }
            }

            player = _combat.Player();
            _arena.Sync(_combat, player, null);
            if (_arena.Player != null) _camera.Follow(_arena.Player.position, _playerYaw, _moving, Time.deltaTime, _freeLook);
            _arena.SyncSpace(_combat, _camera.Camera.transform.position, Time.deltaTime);
            SyncGestures();
        }

        /// <summary>
        /// Sous-lot 11.1 : issue d'un combat vue du joueur. Les ennemis mortels peuvent tous tomber : victoire ; sinon défaite, ou temps
        /// écoulé contre des ennemis d'entraînement aux PV infinis.
        /// </summary>
        private static string Outcome(CombatResult result, string team)
        {
            if (result == null) return "Combat terminé.";
            if (result.Winner == team) return "Victoire : tous les ennemis sont vaincus !";
            if (result.Winner != null) return "Défaite.";
            return result.EndReason == CombatEndReason.DurationReached ? "Temps écoulé : fin de la séance." : "Combat terminé, sans vainqueur.";
        }

        private void ReadCombatInput()
        {
            // Essai du 12.9 (Damien, 25 octobre 2026) : sur le téléphone, deux doigts posés (stick et Agir) font un « clic droit » simulé
            // par Unity, qui levait la garde. Le clic droit ne compte que sans doigt sur l'écran ; au doigt, seul le bouton Garde garde.
            _combat.SetGuard(PointerInput.Guard(Input.touchCount, Input.GetMouseButton(1),
                Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift), _touch.GuardHeld));
            if (Input.GetKeyDown(KeyCode.Space) || _touch.ParryPressed) _combat.Parry();
            // Sous-lot 11.8 : boutons tactiles (compétences 1 à 6 depuis V1.8, et ultime, cible suivante, allié visé en réseau).
            // Essai de style : le modèle du joueur n'attaque que si la compétence part (revue Codex de la PR #55 : UseSlot rend faux pour une
            // compétence en recharge, un emplacement vide ou un étourdissement, sans rien envoyer).
            // Sous-lot V1.8 : six places et l'ultime (SlotPressed rend déjà RealtimeCombat.UltimateSlot pour l'ultime).
            if (_touch.SlotPressed >= 0 && _combat.UseSlot(_touch.SlotPressed)) StrikeSlot(_touch.SlotPressed);
            if (_touch.TargetPressed) _combat.NextTarget();
            if (_touch.AllyPressed && _combat is NetworkCombat ally) ally.NextAlly();
            // Sous-lot V1.8 : touches 1 à 6 pour les six places actives que les règles ouvrent, R pour l'ultime ; 5 n'est plus l'ultime.
            // Réponse de Damien du 31 octobre 2026 : le pont et le protocole connaissent huit places (0 à 7) et l'ultime (8), mais les
            // touches 7 et 8 ne servent pas encore (aucune règle n'ouvre la 7e ni la 8e place) ; R reste l'ultime.
            KeyCode[] slots = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6 };
            KeyCode[] pad = { KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.Keypad4, KeyCode.Keypad5, KeyCode.Keypad6 };
            for (int i = 0; i < slots.Length && i < RealtimeCombat.MaxActiveSlots; i++)
                if ((Input.GetKeyDown(slots[i]) || Input.GetKeyDown(pad[i])) && _combat.UseSlot(i)) StrikeSlot(i);
            if (Input.GetKeyDown(KeyCode.R) && _combat.UseSlot(RealtimeCombat.UltimateSlot)) StrikeSlot(RealtimeCombat.UltimateSlot);
            if (Input.GetKeyDown(KeyCode.Tab)) _combat.NextTarget();
            if (Input.GetKeyDown(KeyCode.G) && _combat is NetworkCombat network) network.NextAlly();
        }

        /// <summary>
        /// Essai réaliste 7 (demande de Damien du 28 octobre 2026 : « coups variés », effets des compétences) : la compétence de
        /// l'emplacement <paramref name="slot"/> est partie. Le modèle joue son geste ; une frappe fend l'air (arc de lumière, son de
        /// l'épée), un sort ou un soutien dessine son cercle au sol (lueur, son du sort).
        /// </summary>
        private void StrikeSlot(int slot)
        {
            SkillSlot skill = _combat.Player().Slots.FirstOrDefault(s => slot == RealtimeCombat.UltimateSlot ? s.IsUltimate : !s.IsUltimate && s.Slot == slot);
            if (skill == null)
            {
                _arena.Strike();
                return;
            }
            _arena.Strike(skill.SkillId, skill.IsUltimate);
            bool spell = StyleRules.SkillAnimation(skill.SkillId, skill.IsUltimate).Motion == StyleMotion.Cast;
            StyleLook.Play(Audio, spell ? StyleSound.Cast : StyleSound.Swing, spell ? 0.7f : 0.55f);
            if (_effects == null || !(_arena.Feet(_combat.PlayerId) is Vector3 feet)) return;
            if (spell) _effects.Cast(feet, skill.IsUltimate ? Effects.Gold : Effects.Teal);
            else _effects.Slash(feet, _arena.Height(_combat.PlayerId), _playerYaw, Effects.Hot);
        }

        /// <summary>
        /// Essai réaliste 7 : les adversaires jouent le geste de leurs attaques (annoncées, ou immédiates à portée), avec leur cri (loup,
        /// sanglier, Gardien) ; l'onde du Gardien (son cri) fait trembler la caméra et frapper le sol. Puis l'éclat d'or d'une Charge de
        /// Rempart gagnée.
        /// </summary>
        private void SyncGestures()
        {
            if (_combat == null) return;
            foreach ((string actor, string attackId) in _arena.PlayAttacks(_combat))
            {
                StyleSlot slot = StyleRules.ForCombatant(actor, false);
                bool boss = _adventure != null && _adventure.IsBoss(actor);
                if (slot == StyleSlot.Wolf) StyleLook.Play(Audio, StyleSound.Growl, 0.6f);
                else if (slot == StyleSlot.Boar) StyleLook.Play(Audio, StyleSound.Grunt, 0.6f);
                else if (boss && StyleRules.AttackAnimation(attackId).Motion == StyleMotion.Cast)
                {
                    StyleLook.Play(Audio, StyleSound.Roar, 0.8f);
                    _camera.Shake(0.3);
                    if (_effects != null && _arena.Feet(actor) is Vector3 at) _effects.Slam(at, 3f);
                }
            }
            CombatantView me = _combat.Player().View;
            if (_shownCharges >= 0 && me.RampartCharges > _shownCharges && _effects != null && _arena.Feet(_combat.PlayerId) is Vector3 feet)
                _effects.Charge(feet, _arena.Height(_combat.PlayerId));
            _shownCharges = me.RampartCharges;
        }

        // --- Jeu en réseau (sous-lot 10.4) ---------------------------------------------------------------------------------------

        private void StartNetwork(NetworkScreen.Action action)
        {
            StopNetwork();
            _netScreen.Message = null;
            // Sous-lot 12.8 : l'aventure est sauvegardée et quittée ; le réseau part des données de l'entraînement.
            if (_adventure != null)
            {
                LeaveAdventure();
                if (_session == null) Load(0, null);
            }
            try
            {
                if (_session == null) throw new InvalidOperationException("données du jeu non chargées (voir le message d'erreur du solo).");
                GameData data = _session.Data;
                string name = (_netScreen.PlayerName ?? "").Trim();
                if (name.Length == 0) name = "Joueur";
                _netBuildId = _session.Build?.Id ?? _netBuildId;
                var build = new BuildChoice(_netBuildId);
                if (action == NetworkScreen.Action.Join)
                {
                    ParseAddress(_netScreen.Address, out string host, out int port);
                    _net = NetworkSession.Join(data, host, port, name, build);
                }
                else
                {
                    bool vallons = action == NetworkScreen.Action.HostVallons || action == NetworkScreen.Action.HostVallonsWithBot;
                    bool bot = action == NetworkScreen.Action.HostWithBot || action == NetworkScreen.Action.HostVallonsWithBot;
                    ScenarioLoadResult coop = ScenarioJsonReader.ReadFile(Path.Combine(_repoRoot, "scenarios", vallons ? DuoScenario : CoopScenario), data);
                    if (coop.Setup == null) throw new InvalidOperationException("scénario de coopération refusé : " + coop.Report);
                    // Sous-lot 11.7 : dans les Vallons, le combat commence dès que les joueurs présents sont prêts, et l'autre (ou le robot,
                    // 10 s plus tard) le rejoint en cours.
                    _net = NetworkSession.HostGame(data, coop.Setup, name, build, Protocol.DefaultPort, true, bot ? new BuildChoice(RobotBuild) : null,
                        vallons ? new RoomOptions { JoinDuringCombat = true } : null, vallons && bot ? BotDelaySeconds : 0);
                }
                _menuOpen = false;
                _paused = false;
                _combat = null;
                _shownCombat = null;
                _lastCombat = null;
                Say(action == NetworkScreen.Action.Join ? "Connexion à " + _net.Address : "Partie hébergée sur ce poste.");
            }
            catch (Exception e)
            {
                StopNetwork();
                _netScreen.Message = "Impossible : " + e.Message;
                Debug.LogException(e);
            }
        }

        /// <summary>« hôte » ou « hôte:port » (port 7777 par défaut).</summary>
        private static void ParseAddress(string text, out string host, out int port)
        {
            host = (text ?? "").Trim();
            port = Protocol.DefaultPort;
            int colon = host.LastIndexOf(':');
            if (colon > 0 && host.IndexOf(':') == colon)
            {
                if (!int.TryParse(host.Substring(colon + 1), out port) || port < 1 || port > 65535) throw new ArgumentException("port invalide.");
                host = host.Substring(0, colon);
            }
            if (host.Length == 0) throw new ArgumentException("adresse vide.");
        }

        private void StopNetwork()
        {
            if (_net == null) return;
            try
            {
                _net.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            _net = null;
            _shownCombat = null;
            if (!(_combat is RealtimeCombat)) _combat = null;
        }

        /// <summary>
        /// Quitte le réseau et reprend la séance solo du début. Revue Codex de la PR #49 : depuis l'aventure (le réseau la quitte toujours avant
        /// de démarrer), le jeu solo est l'aventure elle-même : elle continue telle quelle, sans combat d'entraînement substitué en dessous.
        /// </summary>
        private void BackToSolo()
        {
            StopNetwork();
            if (_adventure != null) return;
            if (_session != null) Restart();
            else Load(_scenario, null);
        }

        /// <summary>
        /// Revue Codex de la PR #21 (tour 4) : build retenu en réseau = celui que le serveur a accepté (suivi par le client : une demande
        /// refusée, par exemple arrivée au serveur après le début du combat, ne le change pas).
        /// </summary>
        private BuildChoice AcceptedNetworkBuild => _net?.Client?.AcceptedBuild ?? new BuildChoice(_netBuildId);

        /// <summary>
        /// Demande un build au serveur (salle d'attente seulement, une demande à la fois) ; rend faux avec la raison si elle n'est pas
        /// partie. Le build retenu ne change qu'une fois la demande acceptée par le serveur.
        /// </summary>
        private bool SetNetworkBuild(string referenceId, IReadOnlyDictionary<string, int> talents, out string why)
        {
            NetworkClient client = _net?.Client;
            why = null;
            if (client == null || client.Status != ClientStatus.Lobby)
                why = "Le combat a commencé : build non envoyé (à refaire en salle d'attente).";
            else if (client.PendingBuild != null)
                why = "Le build précédent attend encore la réponse du serveur.";
            if (why != null)
            {
                Say(why);
                return false;
            }
            try
            {
                if (client.SetBuild(new BuildChoice(referenceId, talents))) return true;
                why = "Build non envoyé (à refaire en salle d'attente).";
            }
            catch (Exception e)
            {
                why = "Build refusé : " + e.Message;
            }
            Say(why);
            return false;
        }

        private string NetworkBuildName()
        {
            NetworkClient client = _net?.Client;
            SeatInfo seat = client?.Lobby?.Seats.FirstOrDefault(s => s.Id == client.SeatId);
            return Hud.Plain(seat?.Build ?? AcceptedNetworkBuild.Reference);
        }

        private void UpdateNetwork(bool input)
        {
            try
            {
                _net.Advance(Time.deltaTime);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _netScreen.Message = "Partie arrêtée : " + e.Message;
                BackToSolo();
                return;
            }
            NetworkClient client = _net.Client;
            NetworkCombat combat = client?.Combat;
            if (combat != null && combat != _shownCombat)
            {
                // Revue Codex de la PR #21 : l'écran des talents ouvert en salle d'attente se ferme au début du combat (le build ne
                // pourrait plus partir) ; le joueur le retrouve au retour en salle d'attente.
                if (_netTalentScreen)
                {
                    CloseTalents();
                    Say("Combat commencé : écran des talents fermé, build inchangé.");
                }
                _shownCombat = combat;
                _shownRoster = combat.RosterVersion;
                _combat = combat;
                _arena.Populate(combat);
                _playerYaw = 0f;
                _floating.Clear();
                Say($"Combat en coopération : tu joues {client.SeatId}.");
            }
            else if (combat != null && combat.RosterVersion != _shownRoster)
            {
                // Revue Codex de la PR #35 : un joueur est arrivé pendant l'affrontement (état courant) : l'arène est repeuplée, pour
                // qu'il ait son corps.
                _shownRoster = combat.RosterVersion;
                _arena.Populate(combat);
            }
            if (input && (Input.GetKeyDown(KeyCode.C) || _touch.ViewPressed)) _camera.Toggle(_playerYaw);
            if (input) _camera.Nudge(_touch.YawDelta, _touch.ViewDelta, _touch.PitchDelta);
            if (input && client != null && client.Status == ClientStatus.Lobby)
            {
                IReadOnlyList<ReferenceBuild> builds = _session.AvailableBuilds();
                KeyCode[] buildKeys = { KeyCode.F1, KeyCode.F2, KeyCode.F3, KeyCode.F4, KeyCode.F5 };
                for (int i = 0; i < buildKeys.Length && i < builds.Count; i++)
                    if (Input.GetKeyDown(buildKeys[i])) SetNetworkBuild(builds[i].Id, null, out _);
                if (Input.GetKeyDown(KeyCode.T))
                {
                    OpenNetworkTalents();
                    return;
                }
            }
            if (_combat == null) return;

            PlayerState player = _combat.Player();
            bool fighting = client != null && client.Status == ClientStatus.Combat && !_combat.IsFinished;
            if (input && fighting) ReadCombatInput();
            else if (fighting) _combat.SetGuard(false);   // menu ou talents ouverts : garde relâchée, rien d'autre n'est envoyé
            // Revue Codex de la PR #21 : coupé ou en retour, la garde mémorisée est oubliée ; la touche est relue une fois le combat repris.
            else if (combat != null && !combat.IsFinished) combat.SetGuard(false);
            // Sous-lot 11.7 : dans un combat positionné, la marche n'est lue qu'en jouant (menu, talents, coupure : arrêt demandé).
            if (_arena.Spatial && !(input && fighting)) _combat.SetMove(0f, 0f);
            else Move(player);
            foreach (CombatFeedback feedback in _combat.DrainFeedback()) Show(feedback);
            if (combat != null && combat.IsFinished && _lastCombat == null)
            {
                CombatResult result = combat.Result;
                _lastCombat = result == null ? $"Dernier combat : {combat.EndReason ?? "terminé."}" : "Dernier combat : " + Outcome(result, _combat.Player().View.Team);
                Say("Fin du combat : retour en salle d'attente.");
            }
            if (combat != null && !combat.IsFinished) _lastCombat = null;

            player = _combat.Player();
            _arena.Sync(_combat, player, combat?.AllyId);
            if (_arena.Player != null) _camera.Follow(_arena.Player.position, _playerYaw, _moving, Time.deltaTime, _freeLook);
            _arena.SyncSpace(_combat, _camera.Camera.transform.position, Time.deltaTime);
            SyncGestures();
        }

        /// <summary>
        /// Déplacement relatif à la caméra (ZQSD en AZERTY, WASD en QWERTY, ou flèches : voir <see cref="Keys"/>). Sous-lot 11.6 : dans un
        /// combat positionné, la direction est envoyée au moteur, qui déplace le personnage (décor, emprises, étourdissement ; rien pour un
        /// absent) ; sinon le déplacement est purement visuel, impossible hors de combat ou étourdi. Revue Codex de la PR #32 (30e tour) : un
        /// joueur absent (avant son arrivée, après son départ) ne bouge ni ne se tourne : il réapparaît là où il était.
        /// </summary>
        private void Move(PlayerState player)
        {
            float x = (Input.GetKey(Keys.Right) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                - (Input.GetKey(Keys.Left) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            float z = (Input.GetKey(Keys.Forward) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f)
                - (Input.GetKey(Keys.Back) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
            // Sous-lot 11.8 : stick du pouce gauche (relatif à la caméra, comme le clavier) ; le clavier l'emporte s'il est utilisé.
            if (x == 0f && z == 0f)
            {
                x = _touch.Move.x;
                z = _touch.Move.y;
            }
            // Un pouce à peine posé (sous la zone morte du stick) ne marche pas ; au-delà, la marche est à pleine vitesse.
            if (x * x + z * z < (float)(MoveInput.DeadZone * MoveInput.DeadZone))
            {
                x = 0f;
                z = 0f;
            }
            // Sous-lot 11.9 (essai de Damien : « le Bastion recule en regardant devant lui ») : en vue 2 (caméra derrière le personnage),
            // reculer (touche arrière, ou stick vers le bas, même en biais) le fait marcher à reculons sans se retourner ; la caméra reste
            // derrière lui. En vue 1, il se tourne vers sa marche, comme avant.
            bool backward = _camera.Setting > 0.5f && z < 0f;
            if (_arena.Spatial)
            {
                // Revue Codex de la PR #34 : en pause, la marche voulue n'est pas touchée (les entrées de combat sont ignorées ; l'intention
                // d'avant la pause reprend telle quelle).
                if (_paused)
                {
                    _moving = false;
                    return;
                }
                Vector3 wish = x != 0f || z != 0f ? Quaternion.Euler(0f, _camera.Yaw, 0f) * new Vector3(x, 0f, z).normalized : Vector3.zero;
                _combat.SetMove(wish.x, wish.z, backward);
                _moving = player.View.IsPresent && !player.View.IsDead && (x != 0f || z != 0f);
                if (player.View.Facing is EdS.Domain.Space.GroundVector f) _playerYaw = Mathf.Atan2(f.X.Raw, f.Z.Raw) * Mathf.Rad2Deg;
                return;
            }
            if (!player.View.IsPresent)
            {
                _moving = false;
                return;
            }
            bool blocked = player.View.IsDead || player.View.States.Contains("stun") || _paused;
            _moving = !blocked && (x != 0f || z != 0f);
            Vector3 delta = Vector3.zero;
            if (_moving)
            {
                Vector3 direction = Quaternion.Euler(0f, _camera.Yaw, 0f) * new Vector3(x, 0f, z).normalized;
                delta = direction * MoveSpeed * Time.deltaTime;
                if (!backward) _playerYaw = Quaternion.LookRotation(direction).eulerAngles.y;
            }
            else if (player.TargetId != null && _arena.Head(player.TargetId) is Vector3 target && _arena.Player != null)
            {
                Vector3 toTarget = target - _arena.Player.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.01f) _playerYaw = Quaternion.LookRotation(toTarget).eulerAngles.y;
            }
            _arena.MovePlayer(delta, _playerYaw);
        }

        /// <summary>
        /// Essai réaliste 7 (demande de Damien du 28 octobre 2026 : impacts, étincelles, caméra qui réagit) : ce qu'un retour de combat
        /// fait sentir. Coup reçu : étincelles et éclair (plus forts selon la part de vie perdue), le modèle accuse le coup ; coup gardé :
        /// étincelles froides, le bouclier encaisse ; parade parfaite : éclair d'or, emblème, onde ; garde brisée : éclats rouges ; mort :
        /// poussière. La caméra tremble pour les coups que le joueur reçoit ou pare (<see cref="GameFeel.Shock"/>).
        /// </summary>
        private void Feel(CombatFeedback feedback)
        {
            string id = feedback.CombatantId;
            bool mine = id == _combat.PlayerId;
            bool boss = _adventure != null && _adventure.IsBoss(id);
            long maxHealth = 0;
            if (mine) maxHealth = _combat.Player().View.MaxHealth ?? 0;
            double shock = GameFeel.Shock(feedback.Kind, mine, feedback.Amount, maxHealth, boss);
            if (shock > 0) _camera.Shake(shock);
            if (feedback.Kind == FeedbackKind.DamageTaken) _arena.Hurt(id, false);
            else if (feedback.Kind == FeedbackKind.Blocked) _arena.Hurt(id, true);
            // Relecture de l'essai 7 : un coup critique du joueur éclate en or sur sa cible (le retour est celui du joueur, pas de la cible).
            if (feedback.Kind == FeedbackKind.CriticalHit && mine && _effects != null && _combat.TargetId is string target
                && _arena.Feet(target) is Vector3 struck)
                _effects.Hit(struck, _arena.Height(target), 0.5f, true);
            if (_effects == null || !(_arena.Feet(id) is Vector3 feet)) return;
            float height = _arena.Height(id);
            switch (feedback.Kind)
            {
                case FeedbackKind.DamageTaken:
                    float part = mine && maxHealth > 0 ? feedback.Amount / (float)maxHealth : Mathf.Clamp01(feedback.Amount / 120f);
                    _effects.Hit(feet, height, Mathf.Clamp01(part * 3f), false);
                    break;
                case FeedbackKind.Blocked: _effects.Blocked(feet, height); break;
                case FeedbackKind.PerfectParry: _effects.Parry(feet, height); break;
                case FeedbackKind.GuardBroken: _effects.GuardBroken(feet, height); break;
                case FeedbackKind.Died:
                    if (!_arena.IsGhost(id)) _effects.Death(feet);   // sous-lot V2.9.1 : un écho se défait en braises (EchoGhosts)
                    break;
            }
        }

        /// <summary>Essai de style : source des sons, sur la caméra (créée au premier son).</summary>
        private AudioSource _audio;

        private AudioSource Audio
        {
            get
            {
                if (_audio == null && _camera != null && StyleCatalog.Loaded != null)
                {
                    _audio = _camera.Camera.gameObject.AddComponent<AudioSource>();
                    EnsureListener();
                }
                return _audio;
            }
        }

        /// <summary>
        /// Revue Codex de la PR #55 : la scène de démarrage est vide et la caméra du jeu est créée par le code : sans écouteur, aucun son ne
        /// s'entendrait. Un seul écouteur : on n'en ajoute pas si la scène en a déjà un (relecture de l'essai 7 : celui posé par les sons de
        /// l'interface faute de caméra est retiré d'abord). Appelé au premier bruitage et, depuis la revue Codex de la PR #63, avant la musique.
        /// </summary>
        private void EnsureListener()
        {
            if (_camera == null) return;
            UiSounds.ReleaseListener();
            if (UnityEngine.Object.FindFirstObjectByType<AudioListener>() == null) _camera.Camera.gameObject.AddComponent<AudioListener>();
        }

        private void Show(CombatFeedback feedback)
        {
            // Essai de style : un son par retour de combat (rien sans catalogue).
            if (feedback.Kind == FeedbackKind.DamageTaken) StyleLook.Play(Audio, StyleSound.Hit, 0.6f);
            else if (feedback.Kind == FeedbackKind.Blocked) StyleLook.Play(Audio, StyleSound.Block);
            else if (feedback.Kind == FeedbackKind.PerfectParry) StyleLook.Play(Audio, StyleSound.Parry, 1f);
            else if (feedback.Kind == FeedbackKind.Died) StyleLook.Play(Audio, StyleSound.Death);
            Feel(feedback);
            if (feedback.Kind == FeedbackKind.Unavailable || feedback.Kind == FeedbackKind.Rejected)
            {
                if (feedback.Kind == FeedbackKind.Rejected) UiSounds.Play(UiCue.Error);
                Say(feedback.Kind == FeedbackKind.Rejected ? "Refusé" + (feedback.Detail != null ? " : " + feedback.Detail : "") : feedback.Detail);
                return;
            }
            string text = Labels.Floating(feedback);
            Vector3? head = _arena.Head(feedback.CombatantId);
            if (text == null || head == null) return;
            // Petit décalage pour que les textes d'un même tick ne se recouvrent pas.
            float offset = _floating.Count(f => Time.time - f.Born < 0.2f) * 0.35f;
            _floating.Add(new Floating
            {
                World = head.Value + Vector3.up * offset,
                Text = text,
                Color = Labels.ColorOf(feedback, feedback.CombatantId == _combat.PlayerId),
                Born = Time.time,
            });
        }

        /// <summary>
        /// Sous-lot 12.8 : commandes tactiles de l'aventure (IMGUI). Essai réaliste 6 : le lieu, l'heure, l'indication d'action, la défaite,
        /// la pause et la petite carte sont dans l'interface uGUI (<see cref="UpdateHud"/>) ; les écrans et le dialogue aussi
        /// (<see cref="AdventureBook"/>).
        /// </summary>
        private void DrawAdventure() => _touch.Draw(false, true, AdventureCovered);

        /// <summary>Essai réaliste 6, étape 2 : écrans et dialogue de l'aventure (uGUI) ; un onglet peut ouvrir l'arbre des Éclats.</summary>
        private void UpdateBook(float deltaTime, bool shown)
        {
            if (_book == null) return;
            if (!shown || _adventure == null || TalentsOpen)
            {
                _book.Hide();
                return;
            }
            AdventureBook.Request request = _book.Update(_adventure, _screens, AdventureEnemies(), deltaTime);
            if (request == AdventureBook.Request.Close) _screens.Close();
            else if (request == AdventureBook.Request.Talents) OpenAdventureTalents(_adventure);
        }

        /// <summary>
        /// Essai réaliste 6, étape 2 (essai de Damien du 27 octobre 2026 : « l'arbre des compétences […] je ne peux pas l'ouvrir ») : l'arbre
        /// des Éclats sur les talents du personnage à son niveau ; ils se gardent hors combat, effet à la halte.
        /// </summary>
        private void OpenAdventureTalents(AdventureSession a)
        {
            if (a.Dialogue != null || a.MustContinue) return;
            _screens.Close();
            try
            {
                _talentView.Open(a.CreateTalentEditor(), a.Text, a.SetTalents);
            }
            catch (ArgumentException e)
            {
                Debug.LogException(e);
                Say("Talents indisponibles : " + e.Message);
            }
        }

        /// <summary>Sous-lot 12.9d : ennemis à montrer sur la carte (en vue seulement, comme à l'écran).</summary>
        private List<MapEnemy> AdventureEnemies()
        {
            var enemies = new List<MapEnemy>();
            foreach (OtherState o in _combat.Others())
                if (o.IsEnemy && o.View.IsPresent && !o.View.IsDead && _arena.Shown(o.Id) && o.View.Position is EdS.Domain.Space.GroundVector at)
                    enemies.Add(new MapEnemy(at.X.Raw / 10000f, at.Z.Raw / 10000f, _adventure.IsBoss(o.Id)));
            return enemies;
        }

        /// <summary>
        /// Essai réaliste 6 : l'interface en jeu (uGUI) suit l'état de cette image, après le déplacement et la caméra. Cachée sous ce qui
        /// reste dessiné en plein écran par IMGUI : erreurs, préparation, menu et salle d'attente du réseau, écran des talents.
        /// </summary>
        private void LateUpdate()
        {
            if (_hud == null) return;
            bool shown = _ready && _startupError == null && !(_error != null && !Network) && !_menuOpen && !LobbyShown && _talents == null && _combat != null
                && !TalentsOpen;
            _talentView?.Update(Time.unscaledDeltaTime);
            bool screens = _ready && _startupError == null && !_menuOpen && !LobbyShown && _talents == null && _combat != null;
            UpdateBook(Time.unscaledDeltaTime, screens);
            if (!shown)
            {
                _hud.Hide();
                return;
            }
            UpdateHud(Time.unscaledDeltaTime);
        }

        private void UpdateHud(float deltaTime)
        {
            _hud.Begin(deltaTime);
            PlayerState player = _combat.Player();
            Camera camera = _camera.Camera;
            foreach (OtherState other in _combat.Others())
            {
                if (!other.View.IsPresent) continue;   // revue Codex de la PR #32 (16e tour) : un absent n'a pas de barre
                Vector3? head = _arena.Head(other.Id);
                if (head == null) continue;
                // Sous-lot 12.8 : dans une zone du monde, seules les créatures proches ont leur barre.
                if (_adventure != null && _arena.Player != null && (head.Value - _arena.Player.position).sqrMagnitude > OverheadRange * OverheadRange) continue;
                _hud.Overhead(camera.WorldToScreenPoint(head.Value), other, other.Id == player.TargetId);
            }
            _floating.RemoveAll(f => Time.time - f.Born > FloatingSeconds);
            foreach (Floating f in _floating)
            {
                float age = Time.time - f.Born;
                _hud.Floating(camera.WorldToScreenPoint(f.World + Vector3.up * age * 0.9f), f.Text, f.Color, age / FloatingSeconds);
            }
            if (_adventure != null && _arena.Player != null) _markers?.Labels(camera, _arena.Player.position, _hud);
            // Sous-lot V1.7 : le titre de guilde affiché, au-dessus du personnage. Sous-lot V1.9.8 : dans le souvenir, le nom de celui qu'on
            // est (« Galeran »).
            string overhead = _stage?.Actor(_adventure) ?? _adventure?.ShownTitle;
            if (_adventure != null && _arena.Player != null && overhead != null)
                _hud.Label(camera.WorldToScreenPoint(_arena.Player.position + Vector3.up * 2.4f), overhead, UiKit.Dawn);
            _hud.Player(player, _touch.Active, deltaTime);   // plus serré quand les commandes tactiles occupent les coins
            // Revue Codex de la PR #34 : la fiche (nom, PV, annonce) apparaît et disparaît avec le corps de la cible, vue jugée à l'écran.
            _hud.Target(_combat.Others().FirstOrDefault(o => o.Id == player.TargetId && o.View.IsPresent && _arena.Shown(o.Id)), deltaTime);
            _messages.RemoveAll(m => Time.time - m.Key > MessageSeconds);
            foreach (KeyValuePair<float, string> message in _messages) _hud.Message(message.Value);

            if (_adventure != null)
            {
                AdventureSession a = _adventure;
                // Étape 3 : un niveau gagné s'annonce en grand (il prend effet à la prochaine halte).
                int level = a.World.Level;
                if (_shownLevel > 0 && level > _shownLevel)
                {
                    _hud.LevelUp(level, level > a.World.EnteredLevel ? "Effet à ta prochaine halte" : "");
                    if (_arena.Feet(_combat.PlayerId) is Vector3 feet) _effects?.LevelUp(feet);   // essai réaliste 7 : colonne de lumière
                }
                _shownLevel = level;
                bool open = a.Dialogue == null && _screens.Open == AdventureScreens.Panel.None;
                double facing = _arena.Player != null ? _arena.Player.eulerAngles.y : _playerYaw;
                _hud.Adventure(a, AdventureEnemies(), facing, open, _touch.Active);
                string notice = !a.MustContinue ? null
                    : a.Text(a.World.IsDefeated ? "text.ui.defeated" : a.Stranded ? "text.ui.stranded" : "text.ui.session_over");
                _hud.Center(_paused && open ? "Pause" : null, notice, AdventureHint());
                _hud.Settings(_paused && open && notice == null, _camera.ClickLook, _camera.InvertY, _camera.Sensitivity);
                _hud.Clock(a.EventBanner ?? a.ScarLabel);   // V2.5 : « En cours : … » ; V2.6 : « Cicatrice du Seuil · palier 2 sur 4 »
                if (_adventureHelp == null) _adventureHelp = HelpKeys.Adventure(a.Text("text.ui.help"));
                _hud.Help(_help && open, a.PlaceName, "Aventure · " + a.TimeOfDay + " · " + _camera.Describe(), _adventureHelp);
            }
            else
            {
                _hud.Adventure(null, null, 0, false, _touch.Active);
                float elapsed = _combat.Tick * (float)_combat.TickSeconds;
                _hud.Center(_paused ? "Pause" : null, null, null);
                _hud.Settings(_paused && !_combat.IsFinished, _camera.ClickLook, _camera.InvertY, _camera.Sensitivity);   // pas sur l'écran de fin
                _hud.Clock($"{(int)elapsed / 60}:{(int)elapsed % 60:00}");
                if (Network)
                    _hud.Help(_help, "Coopération", (_net.Host != null ? "Tu héberges" : _net.Address) + " · " + NetworkBuildName() + " · " + _camera.Describe(), HelpKeys.Network);
                else _hud.Help(_help, ScenarioName(), "Build : " + BuildName() + " · " + _camera.Describe(), HelpKeys.Training);
            }
            _hud.End(deltaTime);
        }

        private void DrawTalents()
        {
            TalentScreen.Action action = _talents.Draw();
            if (action == TalentScreen.Action.Play) PlayTalents();
            else if (action == TalentScreen.Action.Close) CloseTalents();
        }

        private void OnGUI()
        {
            Hud.Begin();
            if (_startupError != null)
            {
                Hud.Panel(new Rect(40, 40, Hud.Width - 80, 260), new Color(0.3f, 0f, 0f, 0.85f));
                Hud.Text(new Rect(60, 60, Hud.Width - 120, 220), _startupError);
                return;
            }
            if (!_ready)
            {
                Hud.Centered(new Rect(Hud.Width / 2f - 300, Hud.VirtualHeight / 2f - 20, 600, 40), "Préparation des données du jeu…", Color.white);
                return;
            }
            if (_error != null && !Network)
            {
                Hud.Panel(new Rect(40, 40, Hud.Width - 80, 260), new Color(0.3f, 0f, 0f, 0.85f));
                Hud.Text(new Rect(60, 60, Hud.Width - 120, 220), _error);
                return;
            }
            if (_menuOpen)
            {
                NetworkScreen.Action menu = _netScreen.DrawMenu();
                if (menu == NetworkScreen.Action.Host || menu == NetworkScreen.Action.HostWithBot || menu == NetworkScreen.Action.HostVallons
                    || menu == NetworkScreen.Action.HostVallonsWithBot || menu == NetworkScreen.Action.Join) StartNetwork(menu);
                else if (menu == NetworkScreen.Action.Solo)
                {
                    _menuOpen = false;
                    BackToSolo();
                }
                else if (menu == NetworkScreen.Action.Close) _menuOpen = false;
                return;
            }
            if (LobbyShown)
            {
                // Salle d'attente (avant, entre et après les combats) ; l'arène du dernier combat reste visible derrière.
                NetworkScreen.Action lobby = _netScreen.DrawLobby(_net, NetworkBuildName(), _lastCombat);
                if (lobby == NetworkScreen.Action.Ready) _net.Client?.SetReady(true);
                else if (lobby == NetworkScreen.Action.NotReady) _net.Client?.SetReady(false);
                else if (lobby == NetworkScreen.Action.Leave)
                {
                    _netScreen.Message = _net.Error ?? _net.Client?.Error;
                    BackToSolo();
                    _menuOpen = true;
                }
                return;
            }
            if (_talents != null && _combat == null)
            {
                DrawTalents();
                return;
            }
            if (_combat == null) return;

            // Essai réaliste 6 : barres, sceau, compétences, cible, aide, messages et textes flottants sont dans l'interface uGUI (LateUpdate).
            if (Network) NetworkScreen.DrawCombat(_net, _shownCombat?.AllyId);

            if (_talents != null)
            {
                DrawTalents();
                return;
            }

            if (_adventure != null)
            {
                DrawAdventure();
                // Intérieurs : fondu au noir après une porte.
                if (_fade > 0f) Hud.Fill(new Rect(0f, 0f, Hud.Width, Hud.VirtualHeight), new Color(0f, 0f, 0f, _fade));
                // Sous-lot V1.9.8 : réveil, souvenir (proposition, fondu blanc, titre), bulle du tutoriel, « Quête passée », légendes.
                bool calm = _screens.Open == AdventureScreens.Panel.None && !TalentsOpen && !_paused;
                _stage?.Draw(_adventure, calm && _adventure.Dialogue == null, calm, _touch.Active);
                return;
            }
            _touch.Draw(Network);   // sous-lot 11.8
        }
    }

    /// <summary>
    /// Touches dont la lettre change entre AZERTY et QWERTY (essai de Damien du 19/10/2026 : les touches répondaient en QWERTY). Unity 6
    /// lit les lettres par leur position physique (réglage « Use Physical Keys » du gestionnaire d'entrées, actif par défaut) : un
    /// <see cref="KeyCode"/> désigne la touche placée à cet endroit sur un clavier QWERTY. Les touches sont donc choisies par position :
    /// Z Q S D, A / E et M sur un clavier AZERTY, ce qui donne W A S D, Q / E et « ; » en QWERTY (M y est aussi accepté pour le menu).
    /// Les autres touches du jeu (E, R, T, G, H, N, C, S, D, chiffres, F1 à F5…) sont au même endroit sur les deux claviers.
    /// </summary>
    internal static class Keys
    {
        /// <summary>Avancer : Z en AZERTY, W en QWERTY.</summary>
        public const KeyCode Forward = KeyCode.W;

        /// <summary>Aller à gauche : Q en AZERTY, A en QWERTY.</summary>
        public const KeyCode Left = KeyCode.A;

        public const KeyCode Back = KeyCode.S;
        public const KeyCode Right = KeyCode.D;

        /// <summary>Tourner la caméra isométrique vers la gauche : A en AZERTY, Q en QWERTY.</summary>
        public const KeyCode TurnLeft = KeyCode.Q;

        public const KeyCode TurnRight = KeyCode.E;

        /// <summary>Menu du réseau : M en AZERTY (position du « ; » en QWERTY), et M en QWERTY.</summary>
        public static bool MenuPressed => Input.GetKeyDown(KeyCode.Semicolon) || Input.GetKeyDown(KeyCode.M);
    }
}
