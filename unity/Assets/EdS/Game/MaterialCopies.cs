using System.Collections.Generic;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Relecture de la PR #117 : copies de matériaux faites pour un corps (teinte de mort ou d'étourdissement, <see cref="StyleBody"/>),
    /// détruites avec lui (Unity ne libère pas seul un matériau copié par <c>Renderer.materials</c>).
    /// </summary>
    public sealed class MaterialCopies : MonoBehaviour
    {
        internal readonly List<Material> Copies = new List<Material>();

        private void OnDestroy()
        {
            foreach (Material m in Copies)
                if (m != null) Destroy(m);
            Copies.Clear();
        }
    }
}
