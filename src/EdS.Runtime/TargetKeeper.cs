using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Space;
using EdS.Simulation.Engine;

namespace EdS.Runtime
{
    /// <summary>
    /// Sous-lot 11.9 (essai de Damien du 22 octobre 2026 : cible perdue de vue remplacée « après un court délai ») : garde de la cible du
    /// joueur. Une cible vivante qui passe derrière le décor reste la cible pendant <see cref="LostDelayMs"/> (un loup qui passe derrière
    /// un tronc ne fait plus changer de cible) ; au-delà, ou si elle meurt ou part, elle est remplacée par le premier ennemi vivant en vue
    /// (ou par aucune). Partagé par le pont solo et le client réseau ; compté en ticks du moteur, sans horloge réelle. Pendant le délai,
    /// l'ennemi caché ne s'affiche pas (sa fiche non plus) et une compétence lancée sur lui est refusée par le moteur (hors de vue).
    /// </summary>
    public sealed class TargetKeeper
    {
        /// <summary>Délai avant de remplacer une cible vivante perdue de vue.</summary>
        public const long LostDelayMs = 1000;

        private string? _lost;
        private long _lostSinceTick;

        /// <summary>Oublie la cible perdue (nouveau combat, choix du joueur).</summary>
        public void Reset() => _lost = null;

        /// <summary>
        /// Cible à garder au tick <paramref name="tick"/> : <paramref name="current"/> si elle est en vue, ou vivante et perdue de vue depuis
        /// moins de <see cref="LostDelayMs"/> ; sinon le premier ennemi vivant en vue (ou null).
        /// </summary>
        public string? Keep(CombatEngine engine, string playerId, IReadOnlyList<ConvexPolygon>? sight, string? current)
        {
            List<OtherState> seen = CombatDisplay.LivingEnemies(engine, playerId, sight).ToList();
            if (current != null && seen.Any(e => e.Id == current))
            {
                _lost = null;
                return current;
            }
            long tick = engine.CurrentTick;
            if (current != null && sight != null && CombatDisplay.LivingEnemies(engine, playerId, null).Any(e => e.Id == current))
            {
                if (_lost != current)
                {
                    _lost = current;
                    _lostSinceTick = tick;
                }
                if (checked((tick - _lostSinceTick) * engine.TickMilliseconds) < LostDelayMs) return current;
            }
            _lost = null;
            return seen.FirstOrDefault()?.Id;
        }
    }
}
