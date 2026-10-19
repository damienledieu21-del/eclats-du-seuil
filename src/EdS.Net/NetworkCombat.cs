using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data;
using EdS.Data.Json;
using EdS.Domain.Combat;
using EdS.Runtime;
using EdS.Simulation.Engine;

namespace EdS.Net
{
    /// <summary>
    /// Sous-lot 10.3 : combat vu par un client réseau. Le moteur local rejoue, tick après tick, les commandes admises par le serveur
    /// (<see cref="TickMessage"/>) et compare l'empreinte de son journal à celle du serveur : à la moindre divergence, ou à un tick manquant,
    /// il demande l'historique complet et se reconstruit (<see cref="ResyncMessage"/>). Il ne prédit rien : l'affichage montre le dernier
    /// tick reçu du serveur, seul juge. Les intentions du joueur (garde, parade, compétence, cible, allié) partent vers le serveur par
    /// <see cref="NetworkClient"/>. Même interface que le combat solo (<see cref="ICombatClient"/>) : le même écran sert aux deux.
    /// </summary>
    public sealed class NetworkCombat : ICombatClient
    {
        private readonly NetworkClient _client;
        private CombatEngine _engine;
        private HashingCombatLog _log;
        private readonly FeedbackTracker _feedback;
        private double _sinceTick;
        private bool _guardHeld;
        private bool _parry;
        private int? _slot;
        private bool _inputDirty;

        // Revue Codex de la PR #20 (tour 4) : commandes des ticks acceptés (empreinte du serveur retrouvée), dans l'ordre, ticks vides
        // exclus ; elles servent à remettre le moteur au dernier tick accepté quand un tick rejoué est refusé.
        private readonly List<TickCommands> _accepted = new List<TickCommands>();

        private NetworkCombat(NetworkClient client, string header, CombatSetup setup, string playerId)
        {
            _client = client;
            Header = header;
            Setup = setup;
            PlayerId = playerId;
            _log = new HashingCombatLog();
            _engine = new CombatEngine(setup, _log);
            _feedback = new FeedbackTracker(_engine);
            TargetId = CombatDisplay.LivingEnemies(_engine, PlayerId).FirstOrDefault()?.Id;
            AllyId = CombatDisplay.LivingAllies(_engine, PlayerId).FirstOrDefault()?.Id;
        }

        /// <summary>Combat lu dans l'en-tête de journal envoyé par le serveur ; null avec la raison s'il est illisible ou sans la place du joueur.</summary>
        internal static NetworkCombat? Create(NetworkClient client, GameData data, string header, string playerId, out string? error)
        {
            CombatSetup? setup = CombatLogReplayer.ReadSetup(header, data, out IReadOnlyList<string> problems);
            if (setup is null)
            {
                error = "début de combat illisible : " + string.Join(" ; ", problems.Take(3));
                return null;
            }
            if (!setup.Combatants.OfType<CharacterSetup>().Any(c => c.Id == playerId))
            {
                error = $"début de combat sans le personnage « {playerId} ».";
                return null;
            }
            // Revue Codex de la PR #20 (tour 4) : tout combattant peut être nommé dans un message (cible, allié) ; un identifiant que le
            // lecteur strict du serveur refuse ferait fermer la connexion au premier envoi. L'en-tête est refusé avant le combat.
            for (int i = 0; i < setup.Combatants.Count; i++)
                if (!MessageCodec.IsId(setup.Combatants[i].Id))
                {
                    error = $"début de combat refusé : $.scenario.combatants[{i}].id, identifiant incompatible avec le protocole (1 à " +
                        $"{Protocol.MaxIdLength} caractères parmi a-z, A-Z, 0-9, « . », « _ », « - »).";
                    return null;
                }
            try
            {
                var combat = new NetworkCombat(client, header, setup, playerId);
                error = null;
                return combat;
            }
            catch (Exception e)
            {
                // Données reçues : aucune exception ne remonte au jeu.
                error = "début de combat refusé par le moteur : " + e.Message;
                return null;
            }
        }

        /// <summary>En-tête du journal du serveur (combat de départ, sans commande).</summary>
        public string Header { get; }

        public CombatSetup Setup { get; }

        public string PlayerId { get; }

        public string? TargetId { get; private set; }

        /// <summary>Personnage allié visé par les soutiens à cible alliée ; null : choix du serveur (l'autre joueur, sinon soi).</summary>
        public string? AllyId { get; private set; }

        /// <summary>Fin du combat : moteur local terminé, ou fin annoncée par le serveur (<see cref="EndReason"/>).</summary>
        public bool IsFinished => _engine.IsFinished || EndReason != null;

        /// <summary>
        /// Résultat du combat local une fois terminé ; null avant, et null aussi si la fin n'a pas pu être vérifiée (<see cref="EndProblem"/>) :
        /// un résultat divergent n'est jamais montré comme celui du combat.
        /// </summary>
        public CombatResult? Result => EndProblem is null ? _engine.Result : null;

        /// <summary>
        /// Revue Codex de la PR #20 : raison pour laquelle la fin annoncée par le serveur n'a pas pu être vérifiée (divergence pas encore
        /// réparée, tick de fin différent, combat local pas terminé) ; null si la fin concorde ou n'est pas encore annoncée.
        /// </summary>
        public string? EndProblem { get; private set; }

        /// <summary>Raison de fin envoyée par le serveur ; null tant qu'il ne l'a pas annoncée.</summary>
        public string? EndReason { get; private set; }

        /// <summary>Tick du serveur à la fin annoncée.</summary>
        public long? EndTick { get; private set; }

        public long Tick => _engine.CurrentTick;

        public double TickSeconds => _engine.TickMilliseconds / 1000.0;

        public long DurationMs => Setup.DurationMs;

        public double TickProgress => Math.Min(1.0, _sinceTick / TickSeconds);

        /// <summary>Empreinte du journal local ; égale à celle du serveur au même tick.</summary>
        public string Hash => _log.Hash;

        /// <summary>Historique demandé, en attente : les ticks reçus entre-temps sont ignorés (ils sont dans l'historique).</summary>
        public bool AwaitingResync { get; private set; }

        /// <summary>Instant où redemander l'historique s'il n'est toujours pas arrivé (ou s'il n'a pas suffi).</summary>
        internal double ResyncRetryAt { get; private set; }

        /// <summary>Demandes d'historique consécutives restées sans accord d'empreinte.</summary>
        internal int ResyncAttempts { get; private set; }

        /// <summary>Divergences détectées (empreinte différente, tick manquant, commande refusée localement).</summary>
        public int Desyncs { get; private set; }

        /// <summary>Reconstructions réussies depuis l'historique du serveur.</summary>
        public int Resyncs { get; private set; }

        /// <summary>Dernière divergence détectée.</summary>
        public string? LastDesync { get; private set; }

        // --- Intentions du joueur ----------------------------------------------------------------------------------------

        public bool GuardHeld => _guardHeld;

        public void SetGuard(bool held)
        {
            if (held == _guardHeld) return;
            _guardHeld = held;
            _inputDirty = true;
        }

        public void Parry()
        {
            _parry = true;
            _inputDirty = true;
        }

        /// <summary>
        /// Même contrôle que le combat solo sur l'état reçu : un emplacement vide ou visiblement indisponible donne un retour
        /// <see cref="FeedbackKind.Unavailable"/> sans rien envoyer ; sinon la demande part au serveur, seul juge. Seule la dernière demande
        /// avant l'envoi compte.
        /// </summary>
        public bool UseSlot(int slot)
        {
            _slot = null;
            if (AwaitingResync)
            {
                // L'état affiché vient d'un tick refusé : rien n'est demandé sur cette base (revue Codex de la PR #20).
                _feedback.Add(new CombatFeedback(FeedbackKind.Unavailable, PlayerId, 0, "resynchronisation en cours"));
                return false;
            }
            SkillSlot? info = Player().Slots.FirstOrDefault(s => s.Slot == slot);
            if (info is null)
            {
                _feedback.Add(new CombatFeedback(FeedbackKind.Unavailable, PlayerId, 0, "emplacement vide"));
                return false;
            }
            if (info.Unavailable != null)
            {
                _feedback.Add(new CombatFeedback(FeedbackKind.Unavailable, PlayerId, 0, $"{info.Name} : {info.Unavailable}"));
                return false;
            }
            _slot = slot;
            _inputDirty = true;
            return true;
        }

        public void NextTarget()
        {
            List<OtherState> enemies = CombatDisplay.LivingEnemies(_engine, PlayerId).ToList();
            string? next = enemies.Count == 0 ? null : enemies[(enemies.FindIndex(e => e.Id == TargetId) + 1) % enemies.Count].Id;
            SetTarget(next);
        }

        public void SelectTarget(string id)
        {
            if (AwaitingResync) return;
            if (!CombatDisplay.LivingEnemies(_engine, PlayerId).Any(e => e.Id == id)) throw new ArgumentException($"« {id} » n'est pas un ennemi vivant.", nameof(id));
            SetTarget(id);
        }

        /// <summary>Allié suivant parmi les personnages alliés vivants.</summary>
        public void NextAlly()
        {
            List<OtherState> allies = CombatDisplay.LivingAllies(_engine, PlayerId).ToList();
            string? next = allies.Count == 0 ? null : allies[(allies.FindIndex(a => a.Id == AllyId) + 1) % allies.Count].Id;
            SetAlly(next);
        }

        public void SelectAlly(string id)
        {
            if (AwaitingResync) return;
            if (!CombatDisplay.LivingAllies(_engine, PlayerId).Any(a => a.Id == id)) throw new ArgumentException($"« {id} » n'est pas un allié vivant.", nameof(id));
            SetAlly(id);
        }

        private void SetTarget(string? id)
        {
            if (id == TargetId || AwaitingResync) return;   // pas de choix fondé sur un tick refusé
            TargetId = id;
            _inputDirty = true;
        }

        private void SetAlly(string? id)
        {
            if (id == AllyId || AwaitingResync) return;
            AllyId = id;
            _inputDirty = true;
        }

        /// <summary>Intentions à envoyer s'il y a du nouveau ; parade et compétence sont des demandes ponctuelles, vidées une fois prises.</summary>
        internal InputMessage? TakeInput()
        {
            if (!_inputDirty || IsFinished) return null;
            var input = new InputMessage(_guardHeld, _parry, _slot, TargetId, AllyId);
            _parry = false;
            _slot = null;
            _inputDirty = false;
            return input;
        }

        /// <summary>Après une reconnexion : la garde du personnage a été relâchée par le serveur, les demandes ponctuelles sont périmées.</summary>
        internal void PrepareReconnect()
        {
            // Revue Codex de la PR #21 : la garde tenue avant la coupure est oubliée (le serveur l'a relâchée) ; le jeu la redonne en
            // lisant la touche une fois le combat repris, au lieu de renvoyer une garde peut-être lâchée pendant l'absence.
            _guardHeld = false;
            _parry = false;
            _slot = null;
            _inputDirty = true;
            // Le serveur renvoie l'historique de lui-même au retour ; s'il se perd, il est redemandé après le délai d'attente.
            AwaitingResync = true;
            ResyncRetryAt = _client.Now + _client.Options.ResyncTimeoutSeconds;
        }

        // --- Temps --------------------------------------------------------------------------------------------------------

        /// <summary>Avance le client réseau (réception, envois, ticks reçus) ; rend le nombre de ticks rejoués.</summary>
        public int Advance(double seconds) => _client.Advance(seconds);

        internal void Elapse(double seconds) => _sinceTick += seconds;

        public IReadOnlyList<CombatFeedback> DrainFeedback() => _feedback.Drain();

        internal void AddFeedback(CombatFeedback feedback) => _feedback.Add(feedback);

        public PlayerState Player() => CombatDisplay.Player(_engine, Setup.Combat, PlayerId, TargetId);

        /// <summary>
        /// Sous-lot 10.6 (option B de la parade, décision du 19/10/2026) : avance de la télégraphie, en secondes. L'affichage a environ une
        /// latence de retard sur le serveur et une parade met une latence à l'atteindre : montrer le coup un temps aller-retour plus tôt
        /// fait arriver une parade pressée « à vue » au moment qui réussit en solo. Temps aller-retour lissé, plafonné
        /// (<see cref="NetworkClientOptions.MaxTelegraphLeadSeconds"/>) ; 0 avant la première mesure ou si l'option est coupée.
        /// </summary>
        public double TelegraphLeadSeconds =>
            _client.Options.TelegraphAdvance && _client.SmoothedRttMs is double rtt
                ? Math.Min(_client.Options.MaxTelegraphLeadSeconds, Math.Max(0, rtt / 1000))
                : 0;

        /// <summary>
        /// Les autres combattants ; la télégraphie des assaillants est avancée de <see cref="TelegraphLeadSeconds"/> et s'écoule en
        /// continu dans le tick en cours (<see cref="TickProgress"/>, plafonné à un tick si le suivant tarde).
        /// </summary>
        public IReadOnlyList<OtherState> Others() => CombatDisplay.Others(_engine, PlayerId, TelegraphLeadSeconds, TickProgress * TickSeconds);

        // --- Commandes du serveur -------------------------------------------------------------------------------------------

        /// <summary>Un tick du serveur ; rend vrai s'il a été rejoué. Un tick déjà connu est ignoré ; un trou ou une divergence demande l'historique.</summary>
        internal bool ApplyTick(TickMessage message)
        {
            if (AwaitingResync || _engine.IsFinished) return false;
            long tick = message.Commands.Tick;
            if (tick < _engine.CurrentTick) return false;
            if (tick > _engine.CurrentTick)
            {
                Diverged($"tick {tick} reçu, tick {_engine.CurrentTick} attendu.");
                return false;
            }
            if (!Replay(message.Commands, out string? refused))
            {
                RestoreTo(tick);
                Diverged($"tick {tick} : commandes du serveur refusées par le moteur local ({refused}).");
                return false;
            }
            // Revue Codex de la PR #20 : l'empreinte est vérifiée avant de publier quoi que ce soit du tick. Un tick refusé ne produit ni
            // retour (pas de dégâts fantômes), ni changement de cible ou d'allié ; l'affichage et les choix attendent l'historique.
            if (_log.Hash != message.Hash)
            {
                string local = _log.Hash;
                // Tour 4 : le moteur, déjà avancé par ce tick, est remis au dernier tick accepté ; l'affichage n'en montre rien.
                RestoreTo(tick);
                Diverged($"tick {tick} : empreinte {local} au lieu de {message.Hash}.");
                return false;
            }
            if (message.Commands.Commands.Count > 0) _accepted.Add(message.Commands);
            _feedback.Collect(_engine);
            _sinceTick = 0;
            KeepTargets();
            return true;
        }

        /// <summary>
        /// Historique complet : le combat est reconstruit depuis son début ; rend vrai si l'empreinte finale est celle du serveur, avec le
        /// nombre de ticks rejoués (revue Codex de la PR #20 : compté dans ce que rend <see cref="NetworkClient.Advance"/>).
        /// </summary>
        internal bool ApplyResync(ResyncMessage message, out int replayed)
        {
            replayed = 0;
            var log = new HashingCombatLog();
            var engine = new CombatEngine(Setup, log);
            var byTick = new Dictionary<long, TickCommands>();
            foreach (TickCommands t in message.History) byTick[t.Tick] = t;
            var empty = Array.Empty<CombatCommand>();
            int count = 0;
            // Borné par la durée du combat : un nombre de ticks démesuré s'arrête à la fin du moteur.
            while (engine.CurrentTick < message.Ticks && !engine.IsFinished)
            {
                TickCommands commands = byTick.TryGetValue(engine.CurrentTick, out TickCommands? t) ? t : new TickCommands(engine.CurrentTick, empty);
                if (!Replay(engine, commands, out string? refused))
                {
                    ResyncFailed($"historique : commandes du tick {engine.CurrentTick} refusées par le moteur local ({refused}).");
                    return false;
                }
                count = checked(count + 1);
            }
            if (engine.CurrentTick != message.Ticks || log.Hash != message.Hash)
            {
                ResyncFailed($"historique : tick {engine.CurrentTick} et empreinte {log.Hash}, le serveur annonce le tick {message.Ticks} et {message.Hash}.");
                return false;
            }
            _engine = engine;
            _log = log;
            _accepted.Clear();
            _accepted.AddRange(byTick.Values.Where(t => t.Tick < message.Ticks && t.Commands.Count > 0).OrderBy(t => t.Tick));   // ceux rejoués
            _feedback.Reset(_engine);
            _sinceTick = 0;
            AwaitingResync = false;
            ResyncAttempts = 0;
            Resyncs++;
            KeepTargets();
            replayed = count;
            return true;
        }

        /// <summary>
        /// Fin annoncée par le serveur. Elle n'est acceptée telle quelle que si le combat local concorde : aucune divergence en attente
        /// d'historique, moteur local terminé au même tick (le serveur n'envoie la fin à un joueur présent que quand son moteur a fini).
        /// Sinon la raison est gardée dans <see cref="EndProblem"/> : le serveur ne répond plus aux demandes d'historique après la fin.
        /// </summary>
        internal void End(long tick, string reason)
        {
            EndTick = tick;
            EndReason = reason;
            EndProblem =
                AwaitingResync ? "divergence pas encore réparée à la fin du combat (" + (LastDesync ?? "historique attendu") + ")"
                : !_engine.IsFinished ? $"fin annoncée au tick {tick}, combat local au tick {_engine.CurrentTick} pas terminé"
                : _engine.CurrentTick != tick ? $"fin annoncée au tick {tick}, combat local terminé au tick {_engine.CurrentTick}"
                : null;
            if (EndProblem != null)
            {
                Desyncs++;
                LastDesync = EndProblem;
                AwaitingResync = false;
            }
        }

        /// <summary>Demande d'historique envoyée (ou renvoyée après un délai).</summary>
        internal void MarkResyncRequested()
        {
            AwaitingResync = true;
            ResyncRetryAt = _client.Now + _client.Options.ResyncTimeoutSeconds;
            ResyncAttempts++;
        }

        private void Diverged(string why)
        {
            Desyncs++;
            LastDesync = why;
            _client.RequestResync(this);
        }

        private void ResyncFailed(string why)
        {
            Desyncs++;
            LastDesync = why;
            // Nouvelle demande dès que le serveur l'accepte (une par seconde), ou abandon après trop d'essais.
            AwaitingResync = true;
            ResyncRetryAt = Math.Min(ResyncRetryAt, _client.Now + _client.Options.ResyncRetrySeconds);
        }

        private bool Replay(TickCommands commands, out string? refused) => Replay(_engine, commands, out refused);

        /// <summary>
        /// Revue Codex de la PR #20 (tour 4) : un tick rejoué puis refusé (empreinte différente, commande refusée) a déjà modifié le moteur
        /// local. Il est reconstruit depuis le début jusqu'au tick <paramref name="tick"/> avec les commandes acceptées, pour que
        /// l'affichage (joueur, autres combattants) ne montre jamais un état que le serveur n'a pas confirmé en attendant l'historique.
        /// Rejouer des commandes déjà acceptées redonne le même moteur (déterminisme) ; un échec, impossible, laisse le moteur tel quel.
        /// </summary>
        private void RestoreTo(long tick)
        {
            var log = new HashingCombatLog();
            CombatEngine engine;
            try
            {
                engine = new CombatEngine(Setup, log);
            }
            catch (Exception)
            {
                return;
            }
            var empty = Array.Empty<CombatCommand>();
            int next = 0;
            while (engine.CurrentTick < tick && !engine.IsFinished)
            {
                TickCommands commands = next < _accepted.Count && _accepted[next].Tick == engine.CurrentTick
                    ? _accepted[next++]
                    : new TickCommands(engine.CurrentTick, empty);
                if (!Replay(engine, commands, out _)) return;
            }
            if (engine.CurrentTick != tick) return;
            _engine = engine;
            _log = log;
        }

        /// <summary>
        /// Rejoue un tick. Les commandes viennent du serveur : toute exception du moteur (commande refusée, état incohérent) est une
        /// divergence, jamais une exception pour le jeu ; le moteur fautif est remplacé par l'historique.
        /// </summary>
        private static bool Replay(CombatEngine engine, TickCommands commands, out string? refused)
        {
            try
            {
                if (commands.Commands.Count > 0) engine.SubmitAll(commands.Commands);
                engine.Step();
            }
            catch (Exception e)
            {
                refused = e.Message;
                return false;
            }
            refused = null;
            return true;
        }

        /// <summary>Cible et allié encore valides, sinon le premier vivant (même règle que le serveur).</summary>
        private void KeepTargets()
        {
            if (TargetId is null || !CombatDisplay.LivingEnemies(_engine, PlayerId).Any(e => e.Id == TargetId))
                SetTarget(CombatDisplay.LivingEnemies(_engine, PlayerId).FirstOrDefault()?.Id);
            if (AllyId is null || !CombatDisplay.LivingAllies(_engine, PlayerId).Any(a => a.Id == AllyId))
                SetAlly(CombatDisplay.LivingAllies(_engine, PlayerId).FirstOrDefault()?.Id);
        }
    }
}
