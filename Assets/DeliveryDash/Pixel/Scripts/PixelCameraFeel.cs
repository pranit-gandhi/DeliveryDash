using UnityEngine;

namespace DeliveryDash.Pixel
{
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Camera))]
    public sealed class PixelCameraFeel : MonoBehaviour
    {
        private PixelRunSession run;
        private Camera lens;
        private float roll;
        private float rollVelocity;
        private Vector3 velocity;
        private void Awake() { lens = GetComponent<Camera>(); }
        private void LateUpdate()
        {
            if (run == null) run = FindFirstObjectByType<PixelRunSession>();
            if (run == null || run.Plan == null || run.Cart == null) return;
            var cart = run.Cart;
            float curve = run.Plan.CenterX(cart.DistanceMeters + 24f) - run.Plan.CenterX(cart.DistanceMeters);
            float targetRoll = Mathf.Clamp(cart.Steer * 1.35f + curve * .32f, -2.4f, 2.4f);
            float dt = Mathf.Min(Time.deltaTime, .05f);
            roll = Mathf.SmoothDamp(roll, targetRoll, ref rollVelocity, .34f, 12f, dt);
            transform.rotation = Quaternion.Euler(0f,0f,roll);
            Vector3 target = new Vector3(cart.Steer * .10f + curve*.022f,
                cart.BumpDisplacement * .10f - cart.LastImpactStrength*.025f, -10f);
            transform.position = Vector3.SmoothDamp(transform.position,target,ref velocity,.24f,5f,dt);
            lens.orthographicSize = Mathf.Lerp(lens.orthographicSize,
                4.5f + Mathf.Clamp01((cart.SpeedMetersPerSecond-6f)/12f)*.08f,1f-Mathf.Exp(-2f*dt));
        }
    }
}
