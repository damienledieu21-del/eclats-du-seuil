using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// V2.25.2 : la barrière du moulin de l'atelier : ses deux vantaux tournent sur leurs axes (<see cref="AtelierDecor.GatePivot"/>) vers
    /// le moulin quand la quête ouvre le passage (<see cref="Open"/>), en <see cref="AtelierDecor.GateSeconds"/> s, quelle que soit la cadence
    /// d'images ; la chaîne fermée disparaît, la chaîne pendante (sur le vantail gauche) et son cadenas ouvert paraissent. Rien n'est alloué
    /// à chaque image. Affichage seulement.
    /// </summary>
    public sealed class GateSwing : MonoBehaviour
    {
        internal Transform LeafG, LeafD;
        internal GameObject ChainClosed, ChainOpen;

        /// <summary>Passage ouvert (la vue le relit au registre du monde).</summary>
        public bool Open;

        private float _progress;
        private float _shown = -1f;

        /// <summary>Pose la barrière ouverte ou fermée d'un coup (à la construction du décor).</summary>
        internal void Snap(bool open)
        {
            Open = open;
            _progress = open ? 1f : 0f;
            Apply();
        }

        private void Update()
        {
            float target = Open ? 1f : 0f;
            if (_progress == target && _shown == _progress) return;
            _progress = (float)AtelierDecor.GateProgress(_progress, Open, Time.deltaTime);
            Apply();
        }

        private void Apply()
        {
            _shown = _progress;
            float angle = (float)AtelierDecor.GateAngle(_progress);
            if (LeafG != null) LeafG.localRotation = Quaternion.Euler(0f, AtelierDecor.GateLeafSign("G") * angle, 0f);
            if (LeafD != null) LeafD.localRotation = Quaternion.Euler(0f, AtelierDecor.GateLeafSign("D") * angle, 0f);
            bool closed = !Open && _progress <= 0f;
            if (ChainClosed != null && ChainClosed.activeSelf != closed) ChainClosed.SetActive(closed);
            if (ChainOpen != null && ChainOpen.activeSelf == closed) ChainOpen.SetActive(!closed);
        }
    }
}
