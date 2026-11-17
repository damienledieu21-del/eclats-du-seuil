using System;

namespace EdS.Runtime
{
    /// <summary>
    /// Sous-lot V2.54 (revue Codex de la PR #208) : la coupure d'un composant d'entrée (l'<c>EventSystem</c> de l'uGUI) pendant un écran passager,
    /// sans Unity. Un composant désactivé n'est plus « le courant » de son système : on garde donc <b>exactement celui qu'on a coupé</b> et c'est lui
    /// qu'on rend, sans en chercher un autre ni en recréer. <typeparamref name="T"/> : le composant (référence).
    /// </summary>
    public sealed class InputLock<T> where T : class
    {
        private T? _held;

        /// <summary>Un composant est actuellement coupé par ce verrou.</summary>
        public bool Held => _held != null;

        /// <summary>
        /// Demande la coupure : <paramref name="current"/> est le composant courant (null : aucun, pas encore créé ; on réessaiera). Rend le composant
        /// à désactiver (une seule fois ; null s'il n'y a rien à faire).
        /// </summary>
        public T? Lock(T? current)
        {
            if (_held != null || current == null) return null;
            _held = current;
            return current;
        }

        /// <summary>Demande la remise en service : rend le composant coupé à réactiver (celui qu'on a gardé, même s'il n'est plus le courant) ; null s'il n'y en a pas.</summary>
        public T? Unlock()
        {
            T? held = _held;
            _held = null;
            return held;
        }
    }
}
