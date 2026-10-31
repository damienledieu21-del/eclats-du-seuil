using System;
using System.Collections.Generic;

namespace EdS.Runtime
{
    /// <summary>
    /// Sous-lot V1.4 (choix de Damien : un petit jeu de forge dès la vague 1, « frapper le métal au bon moment, trois frappes ») : le petit
    /// jeu tel que le client le joue, sans Unity. Un curseur va et vient sur une barre (de 0 à 1) ; le joueur frappe (touche ou clic) quand il
    /// passe au milieu, sur la cible. Chaque frappe reçoit une note de 0 à 100 (<see cref="NoteAt"/>), puis le curseur repart un peu plus
    /// vite. Les notes sont l'entrée du joueur envoyée au cœur (<c>WorldSession.Craft</c>) : le cœur seul en tire la chance et le résultat.
    /// Le temps est celui de l'affichage (temps réel) : il ne touche à aucune règle.
    /// </summary>
    public sealed class ForgeGame
    {
        /// <summary>Demi-largeur de la cible parfaite, en part de la barre (note 100).</summary>
        public const double PerfectHalfWidth = 0.04;

        /// <summary>Durée d'un aller (de 0 à 1) du curseur à la première frappe, en secondes.</summary>
        public const double FirstSweepSeconds = 0.9;

        /// <summary>Chaque frappe rend l'aller suivant plus court de cette part (le curseur va plus vite).</summary>
        public const double SpeedUp = 0.2;

        private readonly List<int> _notes = new List<int>();
        private double _sweep;
        private double _position;
        private int _direction = 1;

        /// <summary>Frappes à donner.</summary>
        public int Strikes { get; }

        /// <summary>Position du curseur sur la barre, de 0 à 1 (la cible est au milieu, 0,5).</summary>
        public double Position => _position;

        /// <summary>Notes des frappes déjà données, dans l'ordre.</summary>
        public IReadOnlyList<int> Notes => _notes.AsReadOnly();

        /// <summary>Toutes les frappes sont données.</summary>
        public bool Done => _notes.Count >= Strikes;

        /// <summary>Adresse en cours : moyenne des notes données (arrondie vers le bas), 0 sans note (comme le cœur).</summary>
        public int Skill
        {
            get
            {
                if (_notes.Count == 0) return 0;
                long sum = 0;
                foreach (int n in _notes) sum += n;
                return (int)(sum / _notes.Count);
            }
        }

        public ForgeGame(int strikes)
        {
            if (strikes < 1) throw new ArgumentOutOfRangeException(nameof(strikes));
            Strikes = strikes;
            _sweep = FirstSweepSeconds;
        }

        /// <summary>Fait avancer le curseur de <paramref name="seconds"/> secondes (aller-retour entre 0 et 1) ; rien une fois les frappes données.</summary>
        public void Advance(double seconds)
        {
            if (Done || !(seconds > 0)) return;
            double left = Math.Min(seconds, 10.0) / _sweep;
            while (left > 0)
            {
                double room = _direction > 0 ? 1 - _position : _position;
                if (left < room)
                {
                    _position += _direction * left;
                    break;
                }
                _position = _direction > 0 ? 1 : 0;
                left -= room;
                _direction = -_direction;
            }
        }

        /// <summary>Frappe maintenant : rend la note de la frappe, ou null si toutes sont déjà données.</summary>
        public int? Strike()
        {
            if (Done) return null;
            int note = NoteAt(_position);
            _notes.Add(note);
            _sweep = Math.Max(0.3, _sweep * (1 - SpeedUp));
            return note;
        }

        /// <summary>
        /// Note d'une frappe à la position <paramref name="position"/> de la barre : 100 dans la cible parfaite (à
        /// <see cref="PerfectHalfWidth"/> du milieu), puis de moins en moins, jusqu'à 0 aux deux bouts de la barre.
        /// </summary>
        public static int NoteAt(double position)
        {
            double distance = Math.Abs(Math.Min(1, Math.Max(0, position)) - 0.5);
            if (distance <= PerfectHalfWidth) return 100;
            double part = (distance - PerfectHalfWidth) / (0.5 - PerfectHalfWidth);
            return (int)Math.Max(0, Math.Round(100 * (1 - part)));
        }
    }
}
