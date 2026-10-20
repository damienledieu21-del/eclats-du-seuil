using System;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;

namespace EdS.Runtime
{
    /// <summary>
    /// Sous-lot 11.7 (extrait du pont temps réel du 11.6) : marche voulue et cible sélectionnée d'un joueur dans un combat positionné. Les
    /// commandes <c>move</c>, <c>stop</c> et <c>select</c> ne partent au moteur que quand elles changent, dans la place laissée par le
    /// plafond de commandes du tick (sinon au tick suivant). Partagé par le pont temps réel (solo) et par la salle du serveur (réseau) : un
    /// joueur seul et un joueur en réseau produisent exactement les mêmes commandes.
    /// </summary>
    public sealed class MovementIntent
    {
        private int? _wished;

        /// <summary>Cap de marche voulu (0 à <see cref="MoveInput.Headings"/> − 1), null : arrêt.</summary>
        public int? Wished
        {
            get => _wished;
            set
            {
                if (value is int h && (h < 0 || h >= MoveInput.Headings))
                    throw new ArgumentOutOfRangeException(nameof(value), $"Cap {h} hors de [0, {MoveInput.Headings - 1}].");
                _wished = value;
            }
        }

        /// <summary>Dernier cap envoyé au moteur (null : arrêt envoyé, ou rien encore).</summary>
        public int? SentHeading { get; private set; }

        /// <summary>Dernière cible sélectionnée envoyée au moteur (null : aucune, ou sélection vidée).</summary>
        public string? SentSelection { get; private set; }

        /// <summary>Oublie tout (nouveau combat) : rien n'a encore été envoyé.</summary>
        public void Reset()
        {
            _wished = null;
            SentHeading = null;
            SentSelection = null;
        }

        /// <summary>
        /// Envoie au moteur, si besoin, la marche (ou l'arrêt) puis la cible sélectionnée, dans la limite de <paramref name="room"/>
        /// commandes ; <paramref name="trySubmit"/> soumet un groupe tout ou rien et dit s'il est parti. Rien pour un combattant mort ou hors
        /// d'un combat positionné. Rend le nombre de commandes parties.
        /// </summary>
        public int Submit(CombatantView me, bool positioned, long atMs, string? target, int room, Func<CombatCommand[], bool> trySubmit)
        {
            if (me is null) throw new ArgumentNullException(nameof(me));
            if (trySubmit is null) throw new ArgumentNullException(nameof(trySubmit));
            if (!positioned || me.IsDead || !me.IsPresent) return 0;
            int sent = 0;
            if (room > sent && _wished != SentHeading)
            {
                CombatCommand move = _wished is int heading
                    ? new CombatCommand(atMs, me.Id, MoveInput.Direction(heading))
                    : new CombatCommand(atMs, me.Id, CombatAction.Stop);
                if (trySubmit(new[] { move }))
                {
                    SentHeading = _wished;
                    sent++;
                }
            }
            // Revue Codex de la PR #34 : une cible perdue de vue sans remplaçante est désélectionnée (sélection vide).
            if (room > sent && target != SentSelection && trySubmit(new[] { CombatCommand.SelectTarget(atMs, me.Id, target) }))
            {
                SentSelection = target;
                sent++;
            }
            return sent;
        }
    }
}
