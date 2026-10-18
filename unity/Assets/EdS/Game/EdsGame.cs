using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EdS.Data.Builds;
using EdS.Domain.Builds;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot 9.3 : boucle du prototype. Charge les données du dépôt (<c>data/v0</c>) et un scénario (<c>scenarios/</c>), joue le
    /// combat en temps réel avec le pont <see cref="RealtimeCombat"/> (sous-lot 9.2), transmet les touches, affiche l'arène, la caméra,
    /// l'interface et les retours. Le cœur reste seul juge du combat : cette classe ne calcule aucune règle.
    /// </summary>
    public sealed class EdsGame : MonoBehaviour
    {
        private static readonly string[] ScenarioFiles =
        {
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
        private RealtimeCombat _combat;
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

        private void Start()
        {
            // Projet Unity dans unity/ : le dépôt est le dossier parent.
            _repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            _camera = CameraRig.Create();
            _arena = new ArenaView();
            Load(0, null);
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
            _talents = new TalentScreen(_session.CreateTalentEditor(), _session.AvailableBuilds());
        }

        private void CloseTalents()
        {
            _talents = null;
            _paused = _pausedBeforeTalents;
        }

        /// <summary>Joue le build composé : nouvelle séance (même scénario) avec ce build, relancée du début.</summary>
        private void PlayTalents()
        {
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
            _messages.Add(new KeyValuePair<float, string>(Time.time, message));
            if (_messages.Count > 5) _messages.RemoveAt(0);
        }

        private void Update()
        {
            // Écran des talents ouvert : le combat est en pause, seules T et Échap (fermer) comptent ; la souris sert à l'écran.
            if (_talents != null)
            {
                if (Input.GetKeyDown(KeyCode.T) || Input.GetKeyDown(KeyCode.Escape)) CloseTalents();
                return;
            }
            if (Input.GetKeyDown(KeyCode.H)) _help = !_help;
            if (Input.GetKeyDown(KeyCode.N)) Load((_scenario + 1) % ScenarioFiles.Length, null);
            if (_combat == null) return;

            if (Input.GetKeyDown(KeyCode.T))
            {
                OpenTalents();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape)) _paused = !_paused;
            if (Input.GetKeyDown(KeyCode.Backspace)) Restart();
            if (Input.GetKeyDown(KeyCode.C)) _camera.Toggle(_playerYaw);
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
                    Say("Fin de la séance : Retour arrière pour recommencer.");
                    _paused = true;
                }
            }

            player = _combat.Player();
            _arena.Sync(_combat, player);
            if (_arena.Player != null) _camera.Follow(_arena.Player.position, _playerYaw, _moving, Time.deltaTime);
        }

        private void ReadCombatInput()
        {
            _combat.SetGuard(Input.GetMouseButton(1) || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
            if (Input.GetKeyDown(KeyCode.Space)) _combat.Parry();
            KeyCode[] slots = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4 };
            KeyCode[] pad = { KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.Keypad4 };
            for (int i = 0; i < slots.Length; i++)
                if (Input.GetKeyDown(slots[i]) || Input.GetKeyDown(pad[i])) _combat.UseSlot(i);
            if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) _combat.UseSlot(RealtimeCombat.UltimateSlot);
            if (Input.GetKeyDown(KeyCode.Tab)) _combat.NextTarget();
        }

        /// <summary>Déplacement purement visuel, relatif à la caméra (ZQSD ou flèches) ; impossible hors de combat ou étourdi.</summary>
        private void Move(PlayerState player)
        {
            float x = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                - (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            float z = (Input.GetKey(KeyCode.Z) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f)
                - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
            bool blocked = player.View.IsDead || player.View.States.Contains("stun") || _paused;
            _moving = !blocked && (x != 0f || z != 0f);
            Vector3 delta = Vector3.zero;
            if (_moving)
            {
                Vector3 direction = Quaternion.Euler(0f, _camera.Yaw, 0f) * new Vector3(x, 0f, z).normalized;
                delta = direction * MoveSpeed * Time.deltaTime;
                _playerYaw = Quaternion.LookRotation(direction).eulerAngles.y;
            }
            else if (player.TargetId != null && _arena.Head(player.TargetId) is Vector3 target && _arena.Player != null)
            {
                Vector3 toTarget = target - _arena.Player.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.01f) _playerYaw = Quaternion.LookRotation(toTarget).eulerAngles.y;
            }
            _arena.MovePlayer(delta, _playerYaw);
        }

        private void Show(CombatFeedback feedback)
        {
            if (feedback.Kind == FeedbackKind.Unavailable || feedback.Kind == FeedbackKind.Rejected)
            {
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

        private void OnGUI()
        {
            Hud.Begin();
            if (_error != null)
            {
                Hud.Fill(new Rect(40, 40, Hud.Width - 80, 260), new Color(0.3f, 0f, 0f, 0.85f));
                Hud.Text(new Rect(60, 60, Hud.Width - 120, 220), _error);
                return;
            }
            if (_combat == null) return;

            PlayerState player = _combat.Player();
            Camera camera = _camera.Camera;
            foreach (OtherState other in _combat.Others())
            {
                Vector3? head = _arena.Head(other.Id);
                if (head != null) Hud.Overhead(camera.WorldToScreenPoint(head.Value), other, other.Id == player.TargetId);
            }

            _floating.RemoveAll(f => Time.time - f.Born > FloatingSeconds);
            foreach (Floating f in _floating)
            {
                float age = Time.time - f.Born;
                Vector3 screen = camera.WorldToScreenPoint(f.World + Vector3.up * age * 0.9f);
                if (screen.z <= 0f) continue;
                float x = screen.x / Hud.Scale, y = (Screen.height - screen.y) / Hud.Scale;
                Color color = f.Color;
                color.a = 1f - age / FloatingSeconds;
                Hud.Centered(new Rect(x - 150, y - 15, 300, 30), f.Text, color);
            }

            Hud.Player(player);
            Hud.Target(_combat.Others().FirstOrDefault(o => o.Id == player.TargetId));
            if (_help) Hud.Help(ScenarioName(), BuildName(), _camera.Describe(), _paused);

            _messages.RemoveAll(m => Time.time - m.Key > MessageSeconds);
            for (int i = 0; i < _messages.Count; i++)
                Hud.Centered(new Rect(Hud.Width / 2f - 400, 80 + i * 30, 800, 28), _messages[i].Value, new Color(1f, 0.95f, 0.8f));

            if (_talents != null)
            {
                TalentScreen.Action action = _talents.Draw();
                if (action == TalentScreen.Action.Play) PlayTalents();
                else if (action == TalentScreen.Action.Close) CloseTalents();
                return;
            }

            float elapsed = _combat.Tick * (float)_combat.TickSeconds;
            Hud.Centered(new Rect(Hud.Width / 2f - 150, 20, 300, 40), $"{(int)elapsed / 60}:{(int)elapsed % 60:00}", Color.white);
        }
    }
}
