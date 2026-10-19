using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Runtime;

namespace EdS.Net
{
    /// <summary>
    /// Sous-lot 10.4 : équipier robot. Un client ordinaire (mêmes messages, mêmes limites que les joueurs) qui se déclare prêt dans la
    /// salle d'attente et joue simplement : garde levée quand un assaillant va frapper, parade juste avant un coup parable, compétences dès
    /// qu'elles sont prêtes (soutien sur l'autre joueur). Sert à essayer la coopération seul (hôte Unity « avec un robot », serveur dédié
    /// <c>--robot</c>) et aux tests. Déterministe : aucune horloge, aucun hasard. Ce n'est pas une intelligence de jeu.
    /// </summary>
    public sealed class BotPlayer
    {
        /// <summary>Intervalle entre deux décisions.</summary>
        public const double ThinkSeconds = 0.1;

        /// <summary>Garde levée quand un coup arrive dans moins de ce délai.</summary>
        public const double GuardSeconds = 0.8;

        /// <summary>Parade tentée quand un coup parable arrive dans moins de ce délai.</summary>
        public const double ParrySeconds = 0.15;

        private double _nextThought;
        private double _lastReadyAt = double.NegativeInfinity;
        private readonly HashSet<string> _parried = new HashSet<string>(StringComparer.Ordinal);

        public BotPlayer(NetworkClient client) => Client = client ?? throw new ArgumentNullException(nameof(client));

        public NetworkClient Client { get; }

        /// <summary>Décide, puis avance le client (réception, envois).</summary>
        public void Advance(double seconds)
        {
            if (Client.Now >= _nextThought)
            {
                _nextThought = Client.Now + ThinkSeconds;
                Think();
            }
            Client.Advance(seconds);
        }

        private void Think()
        {
            switch (Client.Status)
            {
                case ClientStatus.Lobby:
                    SeatInfo? me = Client.Lobby?.Seats.FirstOrDefault(s => s.Id == Client.SeatId);
                    // Prêt dès que possible ; redemandé au plus une fois par seconde (après un combat, ou un build refusé).
                    if (me != null && !me.Ready && Client.Now - _lastReadyAt >= 1)
                    {
                        Client.SetReady(true);
                        _lastReadyAt = Client.Now;
                    }
                    break;
                case ClientStatus.Combat:
                    if (Client.Combat is { IsFinished: false } combat) Fight(combat);
                    break;
            }
        }

        private void Fight(NetworkCombat combat)
        {
            PlayerState me = combat.Player();
            if (me.View.IsDead) return;
            List<OtherState> threats = combat.Others()
                .Where(o => o.IsEnemy && !o.View.IsDead && o.View.NextAttack != null && o.NextAttackSeconds is double)
                .ToList();
            double soonest = threats.Count == 0 ? double.PositiveInfinity : threats.Min(o => o.NextAttackSeconds!.Value);
            combat.SetGuard(soonest <= GuardSeconds);
            foreach (OtherState threat in threats)
            {
                // Une parade par coup annoncé : la clé est l'assaillant et l'instant de son coup.
                string key = threat.Id + "@" + (combat.Tick + (long)Math.Round(threat.NextAttackSeconds!.Value / combat.TickSeconds));
                if (threat.NextAttackSeconds <= ParrySeconds && !threat.View.NextAttack!.Unblockable && _parried.Add(key))
                {
                    combat.Parry();
                    if (_parried.Count > 64) _parried.Clear();
                }
            }
            // Pas de compétence pendant qu'un coup approche : la garde passe d'abord.
            if (soonest <= GuardSeconds) return;
            SkillSlot? ready = me.Slots.FirstOrDefault(s => s.Unavailable == null);
            if (ready != null) combat.UseSlot(ready.Slot);
        }
    }
}
