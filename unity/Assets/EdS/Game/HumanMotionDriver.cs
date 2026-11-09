using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V2.9.2 : fait avancer les fondus d'un corps humain (<see cref="HumanMotion"/>) à chaque image et détruit son graphe
    /// d'animation avec lui. Sous-lot V2.15 : pose ses pieds sur le relief affiché (IK des pieds, hors PC modeste et téléphone).
    /// </summary>
    public sealed class HumanMotionDriver : MonoBehaviour
    {
        internal HumanMotion Motion;
        internal Animator Animator;

        /// <summary>Sous-lot V2.15 : IK des pieds permise (pas sur un PC modeste ni un téléphone : <see cref="DeviceQuality.Light"/>).</summary>
        internal bool Feet;

        /// <summary>Plus haute marche qu'un pied rattrape (m).</summary>
        private const float MaxStep = 0.45f;

        private void Update()
        {
            if (Motion != null) Motion.Tick(Time.deltaTime);
        }

        /// <summary>
        /// Sous-lot V2.15 : chaque pied descend ou monte sur le sol affiché sous lui (<see cref="StyleWorld.StandHeight"/> : le tablier du
        /// pont cintré ; le sol du monde est plat ailleurs, donc rien à faire : coût nul), le bassin descend pour que le pied le plus bas
        /// touche. Le sol est sondé par sa hauteur connue, sans lancer de rayon (le décor n'a pas de collisions) : même effet, moins cher.
        /// </summary>
        private void OnAnimatorIK(int layer)
        {
            if (!Feet || Animator == null) return;
            Vector3 at = transform.position;
            float ground = StyleWorld.StandHeight(at.x, at.z);
            Vector3 left = Animator.GetIKPosition(AvatarIKGoal.LeftFoot);
            Vector3 right = Animator.GetIKPosition(AvatarIKGoal.RightFoot);
            float liftLeft = (float)Locomotion.FootLift(StyleWorld.StandHeight(left.x, left.z), ground, MaxStep);
            float liftRight = (float)Locomotion.FootLift(StyleWorld.StandHeight(right.x, right.z), ground, MaxStep);
            if (Mathf.Abs(liftLeft) < 0.005f && Mathf.Abs(liftRight) < 0.005f)
            {
                Animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0f);
                Animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0f);
                return;
            }
            Animator.bodyPosition = Animator.bodyPosition + Vector3.up * (float)Locomotion.HipDrop(liftLeft, liftRight);
            Animator.SetIKPosition(AvatarIKGoal.LeftFoot, left + Vector3.up * liftLeft);
            Animator.SetIKPosition(AvatarIKGoal.RightFoot, right + Vector3.up * liftRight);
            Animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 1f);
            Animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 1f);
        }

        private void OnDestroy()
        {
            if (Motion != null) Motion.Destroy();
            Motion = null;
        }
    }
}
