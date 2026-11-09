using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V2.15 (démarche réaliste) : la tête (et le cou, s'il est trouvé) d'un personnage ou d'une bête, tournée après l'animation :
    /// vers un point suivi (le héros qui passe près) dans la limite du cou (<see cref="Locomotion.Watch"/>), sinon un regard qui erre, propre
    /// à chacun (<see cref="Locomotion.Glance"/>, graine du corps). Ajouté par-dessus l'animation, lissé ; quelques opérations par image.
    /// </summary>
    public sealed class HeadTurn : MonoBehaviour
    {
        internal Transform Head;
        internal Transform Neck;
        internal Transform Body;
        internal uint Seed;

        /// <summary>Point suivi (tête du héros), s'il y en a un.</summary>
        internal bool HasTarget;
        internal Vector3 Target;

        /// <summary>Lacet relatif maximal (degrés) : 70 pour un humain, 50 pour une bête.</summary>
        internal float MaxYaw = 70f;

        /// <summary>Part du regard qui erre (1 au repos, moins en marche, 0 pendant un geste).</summary>
        internal float Glance = 1f;

        private float _yaw;
        private float _pitch;
        private float _clock;

        /// <summary>Tête et cou de <paramref name="root"/> (premiers os reconnus), et le composant posé sur la racine ; null sans tête.</summary>
        internal static HeadTurn Attach(Transform root, Transform body, uint seed, bool beast)
        {
            if (root == null) return null;
            Transform head = null, neck = null;
            foreach (Transform t in root.gameObject.GetComponentsInChildren<Transform>(true))
            {
                if (head == null && Locomotion.IsHeadBone(t.name)) head = t;
                else if (neck == null && Locomotion.IsNeckBone(t.name)) neck = t;
                if (head != null && neck != null) break;
            }
            if (head == null) return null;
            // Un cou qui n'est pas au-dessus de la tête dans la hiérarchie ne sert pas (il tournerait à part).
            if (neck != null && !head.IsChildOf(neck)) neck = null;
            HeadTurn turn = root.gameObject.AddComponent<HeadTurn>();
            turn.Head = head;
            turn.Neck = neck;
            turn.Body = body != null ? body : root;
            turn.Seed = seed;
            turn.MaxYaw = beast ? 50f : 70f;
            return turn;
        }

        private void LateUpdate()
        {
            if (Head == null || Body == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _clock += dt;
            double yaw = 0.0, pitch = 0.0;
            if (HasTarget)
            {
                Vector3 d = Target - Head.position;
                (yaw, pitch) = Locomotion.Watch(d.x, d.y, d.z, Body.eulerAngles.y, MaxYaw);
            }
            if (yaw == 0.0 && pitch == 0.0 && Glance > 0f)
            {
                (double gy, double gp) = Locomotion.Glance(Seed, _clock);
                yaw = gy * Glance;
                pitch = gp * Glance;
            }
            float ease = 1f - Mathf.Exp(-4f * dt);
            _yaw = Mathf.Lerp(_yaw, (float)yaw, ease);
            _pitch = Mathf.Lerp(_pitch, (float)pitch, ease);
            if (Mathf.Abs(_yaw) < 0.05f && Mathf.Abs(_pitch) < 0.05f) return;
            Vector3 right = Body.right;
            // Le cou prend la moitié du mouvement, la tête le reste (jamais une tête qui tourne seule sur un cou figé).
            float share = Neck != null ? 0.5f : 1f;
            if (Neck != null) Neck.rotation = Quaternion.AngleAxis(_yaw * share, Vector3.up) * Quaternion.AngleAxis(_pitch * share, right) * Neck.rotation;
            Head.rotation = Quaternion.AngleAxis(_yaw * share, Vector3.up) * Quaternion.AngleAxis(_pitch * share, right) * Head.rotation;
        }
    }
}
