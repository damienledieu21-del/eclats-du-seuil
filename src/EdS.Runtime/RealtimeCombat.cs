using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Space;
using EdS.Simulation.Engine;

namespace EdS.Runtime
{
    /// <summary>
    /// Sous-lot 9.2 : pont temps réel entre un client (Unity) et le moteur déterministe. Le client avance le temps réel écoulé
    /// (<see cref="Advance"/>) ; le pont le découpe en ticks de 50 ms, soumet avant chaque tick les intentions du joueur saisies entre-temps
    /// (garde tenue, parade, compétence) et simule le tick. L'état à afficher vient des vues du moteur (<see cref="Player"/>,
    /// <see cref="Others"/>) et les retours (dégâts, blocage, parade…) de la différence des compteurs d'un tick à l'autre
    /// (<see cref="DrainFeedback"/>). Le moteur reste seul juge : une commande refusée l'est par lui, et le combat complété des commandes
    /// soumises (<see cref="CompletedSetup"/>) se rejoue à l'identique.
    /// </summary>
    public sealed class RealtimeCombat : ICombatClient
    {
        /// <summary>Emplacement de l'ultime dans <see cref="UseSlot"/> (les actives occupent 0 à 3).</summary>
        public const int UltimateSlot = 4;

        /// <summary>Retours conservés au plus (les plus anciens sont abandonnés si le client ne les lit pas).</summary>
        public const int MaxPendingFeedback = FeedbackTracker.MaxPending;

        private readonly CombatSetup _setup;
        private readonly CombatEngine _engine;
        private readonly long _tickMs;
        private readonly FeedbackTracker _feedback;
        private double _accumulated;
        private bool _guardHeld;
        private bool _parryRequested;
        private int? _slotRequested;

        // Sous-lot 11.6 : combat positionné. Décor (ligne de vue de l'affichage) ; marche voulue et cible sélectionnée (sous-lot 11.7 :
        // partagées avec la salle du serveur).
        private readonly IReadOnlyList<ConvexPolygon>? _obstacles;
        private readonly MovementIntent _movement = new MovementIntent();
        private readonly TargetKeeper _targets = new TargetKeeper();

        /// <summary>Ticks simulés au plus par appel à <see cref="Advance"/> : une image très lente ne fait pas rattraper des secondes d'un coup.</summary>
        public int MaxTicksPerAdvance { get; set; } = 5;

        /// <summary>
        /// Partie interactive sans journal : le journal écrit au fil de la partie aurait un en-tête sans les commandes du joueur, donc
        /// impossible à rejouer (revue Codex de la PR #13). Le journal d'une partie terminée s'obtient par <see cref="WriteLog"/>.
        /// </summary>
        public RealtimeCombat(CombatSetup setup, string playerId)
        {
            _setup = setup ?? throw new ArgumentNullException(nameof(setup));
            PlayerId = playerId ?? throw new ArgumentNullException(nameof(playerId));
            _engine = new CombatEngine(setup, NullCombatLog.Instance);
            if (_engine.View(playerId).Kind != CombatantKind.Character)
                throw new ArgumentException($"« {playerId} » n'est pas un personnage.", nameof(playerId));
            _tickMs = _engine.TickMilliseconds;
            _feedback = new FeedbackTracker(_engine);
            _obstacles = CombatDisplay.SightObstacles(setup.Space);   // sous-lot 11.9 : la vue (obstacles bas exclus)
            TargetId = Enemies().FirstOrDefault()?.Id;
        }

        public string PlayerId { get; }

        /// <summary>
        /// Cible des compétences à cible ennemie unique : un ennemi vivant et, dans un combat positionné, en vue du joueur ; null s'il n'y en
        /// a aucun. Une cible qui passe derrière le décor est remplacée par le premier ennemi en vue (ou par aucune) ; sous-lot 11.9, après
        /// un court délai (<see cref="TargetKeeper"/>).
        /// </summary>
        public string? TargetId { get; private set; }

        public bool IsFinished => _engine.IsFinished;

        /// <summary>Résultat du combat une fois terminé (durée atteinte ou une équipe vaincue).</summary>
        public CombatResult? Result => _engine.Result;

        /// <summary>Prochain tick à simuler ; temps de combat écoulé = <c>Tick × TickSeconds</c>.</summary>
        public long Tick => _engine.CurrentTick;

        public double TickSeconds => _tickMs / 1000.0;

        public long DurationMs => _setup.DurationMs;

        /// <summary>Fraction (0 à 1) du tick en cours déjà écoulée, pour interpoler l'affichage entre deux ticks.</summary>
        public double TickProgress => Math.Min(1.0, _accumulated / TickSeconds);

        /// <summary>Commandes soumises au moteur depuis le début, dans l'ordre (le scénario de départ n'en a aucune pour le joueur).</summary>
        public IReadOnlyList<CombatCommand> SubmittedCommands => _engine.SubmittedCommands;

        /// <summary>Le combat de départ complété des commandes soumises : un scénario ordinaire, qui se rejoue à l'identique.</summary>
        public CombatSetup CompletedSetup() => _engine.CompletedSetup();   // sous-lot 11.4d : arrivées comprises

        /// <summary>
        /// Journal rejouable de la partie terminée : le combat complété (<see cref="CompletedSetup"/>) est simulé de nouveau dans
        /// <paramref name="log"/>, en-tête compris, et son résultat (identique à <see cref="Result"/>) est rendu. Refusé avant la fin : le
        /// reste du combat n'aurait pas les commandes à venir du joueur.
        /// </summary>
        public CombatResult WriteLog(ICombatLog log)
        {
            if (log is null) throw new ArgumentNullException(nameof(log));
            if (!IsFinished) throw new InvalidOperationException("Partie en cours : le journal s'écrit une fois le combat terminé.");
            return CombatEngine.Run(CompletedSetup(), log);
        }

        // --- Intentions du joueur, appliquées au prochain tick --------------------------------------------------------------

        /// <summary>Garde tenue (bouton maintenu) ou relâchée ; la garde est reprise au tick qui suit un lancer tant qu'elle est tenue.</summary>
        public void SetGuard(bool held) => _guardHeld = held;

        public bool GuardHeld => _guardHeld;

        /// <summary>Sous-lot 11.6 : aire et décor du combat ; null sans espace.</summary>
        public CombatSpace? Space => _setup.Space;

        /// <summary>
        /// Sous-lot 11.6 : direction de marche voulue (repère du sol), ramenée à un cap de <see cref="MoveInput"/> ; sous la zone morte,
        /// arrêt. Au prochain tick, le pont envoie <c>move</c> seulement si la marche du moteur n'est pas déjà ce cap, et <c>stop</c>
        /// seulement quand un cap tenu est relâché et que le personnage marche encore (revue Codex de la PR #34 : une commande du scénario
        /// qui a changé la marche est corrigée tant que le joueur tient un cap ; sans saisie, elle garde la main). Sans espace, sans effet.
        /// </summary>
        /// <remarks>Sous-lot 11.9 : <paramref name="backward"/>, à reculons (le personnage garde son orientation).</remarks>
        public void SetMove(double x, double z, bool backward = false)
        {
            int? heading = MoveInput.Heading(x, z);
            _movement.Wished = _setup.Space is null ? null : heading;
            _movement.Backward = _movement.Wished != null && backward;
        }

        /// <summary>Cap de marche voulu (null : arrêt), pour les essais et l'affichage.</summary>
        public int? WishedHeading => _movement.Wished;

        /// <summary>Sous-lot 11.6 : attaques annoncées en cours (formes au sol verrouillées, impact à venir), lecture seule.</summary>
        public IReadOnlyList<AnnouncedAttack> Announced() => _engine.Announced();

        /// <summary>Sous-lot 11.7 : joué seul, rien n'est prédit (le moteur tourne sur place).</summary>
        public (double X, double Z)? PredictedPosition() => null;

        /// <summary>Parade parfaite au prochain tick.</summary>
        public void Parry() => _parryRequested = true;

        /// <summary>
        /// Compétence d'un emplacement du build (0 à 3 : actives, <see cref="UltimateSlot"/> : ultime) au prochain tick. Un emplacement vide
        /// ou une compétence visiblement indisponible (recharge, ressources, contrôle) donne un retour <see cref="FeedbackKind.Unavailable"/>
        /// sans commande ; le reste est soumis au moteur, qui reste seul juge (un refus donne <see cref="FeedbackKind.Rejected"/>). Seule la
        /// dernière demande d'un tick compte : une demande refusée ici annule aussi la précédente (revue Codex de la PR #13).
        /// </summary>
        public bool UseSlot(int slot)
        {
            _slotRequested = null;
            SkillSlot? info = Player().Slots.FirstOrDefault(s => s.Slot == slot);
            if (info is null)
            {
                AddFeedback(new CombatFeedback(FeedbackKind.Unavailable, PlayerId, 0, "emplacement vide"));
                return false;
            }
            if (info.Unavailable != null)
            {
                AddFeedback(new CombatFeedback(FeedbackKind.Unavailable, PlayerId, 0, $"{info.Name} : {info.Unavailable}"));
                return false;
            }
            _slotRequested = slot;
            return true;
        }

        /// <summary>Cible suivante parmi les ennemis vivants en vue (ordre des combattants du scénario).</summary>
        public void NextTarget()
        {
            List<OtherState> enemies = Enemies().ToList();
            if (enemies.Count == 0)
            {
                TargetId = null;
                return;
            }
            int index = enemies.FindIndex(e => e.Id == TargetId);
            TargetId = enemies[(index + 1) % enemies.Count].Id;
        }

        public void SelectTarget(string id)
        {
            if (!Enemies().Any(e => e.Id == id)) throw new ArgumentException($"« {id} » n'est pas un ennemi vivant en vue.", nameof(id));
            TargetId = id;
        }

        // --- Temps --------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Avance de <paramref name="seconds"/> secondes de temps réel : simule les ticks échus (au plus <see cref="MaxTicksPerAdvance"/>,
        /// le surplus est abandonné) et rend leur nombre. Un temps négatif ou non fini est refusé.
        /// </summary>
        public int Advance(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (IsFinished) return 0;
            _accumulated += seconds;
            int steps = 0;
            while (_accumulated >= TickSeconds && !IsFinished)
            {
                if (steps >= MaxTicksPerAdvance)
                {
                    _accumulated = 0;
                    break;
                }
                StepOnce();
                _accumulated -= TickSeconds;
                steps++;
            }
            if (IsFinished) _accumulated = 0;
            return steps;
        }

        /// <summary>Simule exactement un tick, intentions comprises (tests, pas à pas).</summary>
        public void StepOnce()
        {
            if (IsFinished) return;
            SubmitIntentions();
            _engine.Step();
            _feedback.Collect(_engine);
            TargetId = _targets.Keep(_engine, PlayerId, _obstacles, TargetId);   // sous-lot 11.9 : cible perdue remplacée après un court délai
        }

        private void SubmitIntentions()
        {
            CombatantView me = _engine.View(PlayerId);
            IReadOnlyList<CombatCommand[]> groups = IntentionPlanner.Plan(me, checked(_engine.CurrentTick * _tickMs), _guardHeld, _parryRequested,
                _slotRequested, TargetFor);
            _slotRequested = null;
            _parryRequested = false;
            int used = 0;
            foreach (CombatCommand[] group in groups)
                if (TrySubmit(group)) used += group.Length;
            // Sous-lot 11.6 : marche et cible sélectionnée (le personnage en garde se tourne vers elle), seulement quand elles diffèrent de
            // celles du moteur (revue Codex de la PR #34 : une commande du scénario peut les avoir changées depuis le dernier envoi), dans la
            // place qui reste au tick (plafond de commandes par acteur) : sinon elles partent au tick suivant. Une cible perdue de vue sans
            // remplaçante est désélectionnée (sélection vide) : la garde ne se tourne pas vers un ennemi caché.
            _movement.Submit(me, _setup.Space != null, checked(_engine.CurrentTick * _tickMs), TargetId, _setup.Combat.MaxCommandsPerActorPerTick - used, TrySubmit);
        }

        /// <summary>Soumet un groupe tout ou rien ; plafond de commandes ou file pleine : abandonné en entier, le combat continue.</summary>
        private bool TrySubmit(CombatCommand[] group)
        {
            try
            {
                _engine.SubmitAll(group);
                return true;
            }
            catch (InvalidOperationException e)
            {
                AddFeedback(new CombatFeedback(FeedbackKind.Rejected, PlayerId, 0, e.Message));
                return false;
            }
        }

        private string? TargetFor(SkillDefinition skill)
        {
            switch (skill.Combat!.Targeting)
            {
                case SkillTargeting.Enemy:
                    return TargetId;
                case SkillTargeting.Ally:
                    return PlayerId;   // entraînement en solo : soutien sur soi
                default:
                    return null;
            }
        }

        // --- Retours ------------------------------------------------------------------------------------------------------

        private void AddFeedback(CombatFeedback feedback) => _feedback.Add(feedback);

        /// <summary>Retours apparus depuis le dernier appel, dans l'ordre ; la liste interne est vidée.</summary>
        public IReadOnlyList<CombatFeedback> DrainFeedback() => _feedback.Drain();

        // --- État à afficher ----------------------------------------------------------------------------------------------

        /// <summary>État du joueur au début du prochain tick.</summary>
        public PlayerState Player() => CombatDisplay.Player(_engine, _setup.Combat, PlayerId, TargetId);

        /// <summary>Les autres combattants (ennemis et alliés), dans l'ordre du scénario ; leur télégraphie s'écoule en continu dans le tick en cours (<see cref="TickProgress"/>).</summary>
        public IReadOnlyList<OtherState> Others() => CombatDisplay.Others(_engine, PlayerId, 0, TickProgress * TickSeconds, _obstacles);

        private IEnumerable<OtherState> Enemies() => CombatDisplay.LivingEnemies(_engine, PlayerId, _obstacles);
    }
}
