using UnityEngine;

namespace DeliveryDash
{
    // The hill supplies the forward motion. The player only steers.
    [RequireComponent(typeof(CharacterController))]
    public sealed class CartFeelController : MonoBehaviour
    {
        [Header("Downhill drive")]
        [SerializeField, Min(1f)] private float cruiseSpeed = 13.5f;
        [SerializeField, Min(1f)] private float launchAcceleration = 8f;
        [SerializeField, Range(5f, 45f)] private float maximumHeading = 31f;
        [SerializeField, Min(1f)] private float steeringResponse = 8f;
        [SerializeField, Min(1f)] private float lateralGrip = 7f;
        [SerializeField, Min(1f)] private float gravity = 32f;
        [SerializeField, Min(0f)] private float groundStick = 4f;

        private CharacterController body;
        private float speed;
        private float steering;
        private float heading;
        private float lateralSpeed;
        private float verticalSpeed;
        private float lastScrapeTime = float.NegativeInfinity;
        private float stalledFor;
        private Vector3 lastSafePosition;

        public float Speed => speed;
        public float SpeedNormalized => Mathf.Clamp01(speed / cruiseSpeed);
        public float Steering => steering;
        public float LateralSpeed => lateralSpeed;
        public float Slip => Mathf.Clamp01(Mathf.Abs(lateralSpeed) / cruiseSpeed);
        public bool IsDrifting => false;
        public bool IsGrounded => body != null && body.isGrounded;
        public float VerticalSpeed => verticalSpeed;
        public float LastScrapeTime => lastScrapeTime;

        private void Awake()
        {
            EnsureBody();
        }

        private void OnEnable()
        {
            EnsureBody();
        }

        private void EnsureBody()
        {
            if (body == null) body = GetComponent<CharacterController>();
            if (body == null) return;
            body.height = 1.65f;
            body.radius = 0.48f;
            body.center = new Vector3(0f, 0.82f, 0f);
            body.stepOffset = 0.28f;
            body.slopeLimit = 60f;
            body.skinWidth = 0.055f;
            lastSafePosition = transform.position;
        }

        private void FixedUpdate()
        {
            if (body == null) EnsureBody();
            if (body == null) return;
            float dt = Time.fixedDeltaTime;
            float input = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) input -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) input += 1f;

            steering = Mathf.MoveTowards(steering, input, steeringResponse * dt);
            float wantedHeading = steering * maximumHeading;
            heading = Mathf.LerpAngle(heading, wantedHeading, 1f - Mathf.Exp(-steeringResponse * 0.7f * dt));
            transform.rotation = Quaternion.Euler(0f, heading, 0f);

            speed = Mathf.MoveTowards(speed, cruiseSpeed, launchAcceleration * dt);
            float targetLateralSpeed = Mathf.Sin(heading * Mathf.Deg2Rad) * speed;
            lateralSpeed = Mathf.Lerp(lateralSpeed, targetLateralSpeed, 1f - Mathf.Exp(-lateralGrip * dt));
            float forwardSpeed = Mathf.Cos(heading * Mathf.Deg2Rad) * speed;

            if (body.isGrounded && verticalSpeed < 0f) verticalSpeed = -groundStick;
            verticalSpeed -= gravity * dt;
            Vector3 before = transform.position;
            CollisionFlags hits = body.Move(new Vector3(lateralSpeed, verticalSpeed, forwardSpeed) * dt);
            if ((hits & CollisionFlags.Below) != 0) verticalSpeed = -groundStick;
            if ((hits & CollisionFlags.Above) != 0 && verticalSpeed > 0f) verticalSpeed = 0f;

            float advance = transform.position.z - before.z;
            if ((hits & CollisionFlags.Sides) != 0)
            {
                lastScrapeTime = Time.time;
                lateralSpeed *= 0.55f;
            }
            if (body.isGrounded && advance > 0.03f)
            {
                stalledFor = 0f;
                if ((hits & CollisionFlags.Sides) == 0) lastSafePosition = transform.position;
            }
            else if (body.isGrounded && speed > 4f)
            {
                stalledFor += dt;
                if (stalledFor > 2f)
                {
                    ResetAt(lastSafePosition, Quaternion.identity);
                    stalledFor = 0f;
                }
            }
        }

        public void ResetAt(Vector3 position, Quaternion rotation)
        {
            body.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            body.enabled = true;
            speed = 0f;
            steering = 0f;
            heading = 0f;
            lateralSpeed = 0f;
            verticalSpeed = 0f;
            lastSafePosition = position;
        }
    }
}
