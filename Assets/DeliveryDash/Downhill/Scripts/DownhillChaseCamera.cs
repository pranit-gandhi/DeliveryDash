using UnityEngine;

namespace DeliveryDash.Downhill
{
    [RequireComponent(typeof(Camera))]
    public sealed class DownhillChaseCamera : MonoBehaviour
    {
        [SerializeField] private float followDistance = 4.6f;
        [SerializeField] private float followHeight = 2.7f;
        [SerializeField] private float sideOffset = .55f;
        [SerializeField] private Transform target;
        [SerializeField] private DownhillCart cart;
        private Camera lens;
        private Vector3 springVelocity;
        private Vector3 heading = Vector3.forward;
        private bool initialized;
        private float lastDistance;
        private int resetVersion;

        public void Configure(Transform followTarget, DownhillCart controller)
        {
            target = followTarget;
            cart = controller;
            // Saved feel-scene values predate the tighter live-speed framing.
            followDistance = 4.6f;
            followHeight = 2.7f;
            initialized = false;
            lens = GetComponent<Camera>();
            lens.fieldOfView = 51f;
            Snap();
        }

        public void Snap()
        {
            if (target == null) return;
            followDistance = 4.6f;
            followHeight = 2.7f;
            heading = target.forward;
            heading.y = 0f;
            heading.Normalize();
            springVelocity = Vector3.zero;
            initialized = true;
            lastDistance = cart == null ? 0f : cart.Distance;
            resetVersion = cart == null ? 0 : cart.ResetVersion;
            Follow(0f, true);
        }

        private void LateUpdate()
        {
            if (target == null) return;
            if (!initialized) Snap();
            if (cart != null && (cart.ResetVersion != resetVersion || cart.Distance + 5f < lastDistance)) Snap();
            Follow(Mathf.Min(Time.deltaTime, .05f), false);
            if (cart != null)
            {
                lastDistance = cart.Distance;
            }
        }

        private void Follow(float dt, bool snap)
        {
            if (lens == null) lens = GetComponent<Camera>();
            float desiredFov = cart == null ? 51f : Mathf.Lerp(51f, 54f, Mathf.InverseLerp(12f, 22f, cart.Speed));
            lens.fieldOfView = snap ? desiredFov : Mathf.Lerp(lens.fieldOfView, desiredFov, 1f - Mathf.Exp(-2f * dt));
            Vector3 motion = cart != null && cart.Speed > 1f ? cart.Velocity.normalized : target.forward;
            motion.y = 0f;
            motion.Normalize();
            Vector3 facing = target.forward;
            facing.y = 0f;
            facing.Normalize();
            Vector3 desiredHeading = Vector3.Slerp(facing, motion, cart!=null&&cart.Spinning?1f:.65f);
            heading = snap ? desiredHeading : Vector3.Slerp(heading, desiredHeading, 1f - Mathf.Exp(-4f * dt));
            Vector3 right = Vector3.Cross(Vector3.up, heading);
            Vector3 desired = target.position - heading * followDistance + Vector3.up * followHeight + right * sideOffset;
            if (cart != null) desired.y -= Mathf.Clamp(cart.LandingImpact * .018f, 0f, .16f);
            transform.position = snap ? desired : Vector3.SmoothDamp(transform.position, desired, ref springVelocity, .055f, Mathf.Infinity, dt);
            float preview = cart == null ? 5f : Mathf.Lerp(4f, 7.5f, Mathf.InverseLerp(9f, 22f, cart.Speed));
            Vector3 look = target.position + heading * preview + Vector3.up * .8f;
            if (cart != null && cart.Course != null)
            {
                CourseSample ahead = cart.Course.Sample(Mathf.Min(cart.Course.Length, cart.Distance + preview), cart.RouteId);
                look = Vector3.Lerp(look, ahead.Position + Vector3.up * .8f, .35f);
                // Route lookahead anticipates bends; its small weight retains the courier framing.
                // Visibility at a decision point still requires actual camera and keyboard checks.
                CourseSample decision = cart.Course.Sample(Mathf.Min(cart.Course.Length,
                    cart.Distance + Mathf.Max(27f, cart.Speed * 3f)), cart.RouteId);
                look = Vector3.Lerp(look, decision.Position + Vector3.up * .8f, .16f);
            }
            float bank = cart == null ? 0f : Mathf.Clamp(-cart.Slip * .035f, -.7f, .7f);
            Quaternion desiredRotation = Quaternion.LookRotation(look - transform.position, Vector3.up)
                * Quaternion.Euler(0f, 0f, bank);
            transform.rotation = snap ? desiredRotation : Quaternion.Slerp(transform.rotation, desiredRotation, 1f - Mathf.Exp(-8f * dt));
        }
    }
}
