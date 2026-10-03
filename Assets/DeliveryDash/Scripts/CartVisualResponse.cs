using UnityEngine;

namespace DeliveryDash
{
    /// <summary>
    /// Restrained, deterministic follow-through for the cart, courier and supported pizza.
    /// This component never changes the vehicle path. It only moves visual children.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CartFeelController))]
    public sealed class CartVisualResponse : MonoBehaviour
    {
        [Header("Visual hierarchy")]
        [SerializeField] private Transform cartVisualRoot;
        [SerializeField] private Transform riderReactionRoot;
        [Tooltip("Place this pivot at the center underside of the box, where the fingertips support it.")]
        [SerializeField] private Transform pizzaBoxPivot;
        [SerializeField] private Transform[] wheelSpin = new Transform[0];
        [SerializeField] private Transform[] casterYaw = new Transform[0];

        [Header("Motion response")]
        [SerializeField, Min(0.03f)] private float wheelRadius = 0.17f;
        [SerializeField, Range(0f, 12f)] private float maximumCartRoll = 7f;
        [SerializeField, Range(0f, 12f)] private float maximumPizzaTilt = 5f;
        [SerializeField, Min(0.02f)] private float frameResponse = 0.11f;
        [SerializeField, Min(0.02f)] private float riderResponse = 0.18f;
        [SerializeField, Min(0.02f)] private float pizzaResponse = 0.27f;

        private CartFeelController drive;
        private Vector3 frameRestPosition;
        private Quaternion frameRestRotation;
        private Quaternion riderRestRotation;
        private Quaternion pizzaRestRotation;
        private Quaternion[] wheelRestRotations = new Quaternion[0];
        private Quaternion[] casterRestRotations = new Quaternion[0];
        private Vector3 previousPosition;
        private float previousYaw;
        private float previousVerticalSpeed;
        private float previousScrapeTime = float.NegativeInfinity;
        private bool wasGrounded;
        private bool hasSample;
        private float wheelAngle;
        private float frameRoll;
        private float frameRollVelocity;
        private float framePitch;
        private float framePitchVelocity;
        private float frameYaw;
        private float frameYawVelocity;
        private float riderRoll;
        private float riderRollVelocity;
        private float riderPitch;
        private float riderPitchVelocity;
        private float pizzaRoll;
        private float pizzaRollVelocity;
        private float pizzaPitch;
        private float pizzaPitchVelocity;
        private float slopePitch;
        private float landingCompression;
        private float scrapePulse;

        public void Configure(
            Transform frame,
            Transform rider,
            Transform pizza,
            Transform[] wheels,
            Transform[] casters,
            float radius)
        {
            cartVisualRoot = frame;
            riderReactionRoot = rider;
            pizzaBoxPivot = pizza;
            wheelSpin = wheels ?? new Transform[0];
            casterYaw = casters ?? new Transform[0];
            wheelRadius = Mathf.Max(0.03f, radius);
            CacheRestPose();
        }

        private void Awake()
        {
            drive = GetComponent<CartFeelController>();
            CacheRestPose();
        }

        private void OnEnable()
        {
            if (drive == null)
                drive = GetComponent<CartFeelController>();
            previousPosition = transform.position;
            previousYaw = transform.eulerAngles.y;
            previousVerticalSpeed = drive != null ? drive.VerticalSpeed : 0f;
            previousScrapeTime = drive != null ? drive.LastScrapeTime : float.NegativeInfinity;
            wasGrounded = drive != null && drive.IsGrounded;
            hasSample = false;
        }

        private void CacheRestPose()
        {
            if (cartVisualRoot != null)
            {
                frameRestPosition = cartVisualRoot.localPosition;
                frameRestRotation = cartVisualRoot.localRotation;
            }
            if (riderReactionRoot != null)
                riderRestRotation = riderReactionRoot.localRotation;
            if (pizzaBoxPivot != null)
                pizzaRestRotation = pizzaBoxPivot.localRotation;

            wheelRestRotations = new Quaternion[wheelSpin != null ? wheelSpin.Length : 0];
            for (int i = 0; i < wheelRestRotations.Length; i++)
                if (wheelSpin[i] != null)
                    wheelRestRotations[i] = wheelSpin[i].localRotation;

            casterRestRotations = new Quaternion[casterYaw != null ? casterYaw.Length : 0];
            for (int i = 0; i < casterRestRotations.Length; i++)
                if (casterYaw[i] != null)
                    casterRestRotations[i] = casterYaw[i].localRotation;
        }

        private void LateUpdate()
        {
            if (drive == null || cartVisualRoot == null)
                return;

            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0f)
                return;

            Vector3 position = transform.position;
            float yaw = transform.eulerAngles.y;
            float turnRate = hasSample ? Mathf.DeltaAngle(previousYaw, yaw) / dt : 0f;
            Vector3 travel = hasSample ? position - previousPosition : Vector3.zero;
            float horizontalTravel = Vector3.ProjectOnPlane(travel, Vector3.up).magnitude;
            bool grounded = drive.IsGrounded;

            // Read the actual drop of the cart over the road. The visual frame follows the
            // downhill grade without changing the CharacterController's steering plane.
            if (grounded && horizontalTravel > 0.015f && wasGrounded)
            {
                float observedPitch = Mathf.Atan2(-travel.y, horizontalTravel) * Mathf.Rad2Deg;
                observedPitch = Mathf.Clamp(observedPitch, -4f, 16f);
                slopePitch = Mathf.MoveTowards(slopePitch, observedPitch, 85f * dt);
            }

            if (hasSample && !wasGrounded && grounded)
            {
                float fallSpeed = Mathf.Max(0f, -previousVerticalSpeed);
                landingCompression = Mathf.Clamp(fallSpeed / 8f, 0.2f, 1f);
            }
            if (drive.LastScrapeTime > previousScrapeTime && !float.IsInfinity(drive.LastScrapeTime))
                scrapePulse = Mathf.Sign(drive.Steering + 0.01f) * 3f;

            float speed01 = drive.SpeedNormalized;
            float lateral = Mathf.Clamp(drive.LateralSpeed, -7f, 7f);
            float desiredRoll = Mathf.Clamp(-drive.Steering * 3.8f - lateral * 0.45f - turnRate * 0.017f,
                -maximumCartRoll, maximumCartRoll);
            float desiredPitch = grounded ? slopePitch + 0.9f * speed01 : -2f;
            desiredPitch += landingCompression * 2.7f;
            float desiredYaw = Mathf.Clamp(-lateral * 0.42f, -4f, 4f);

            frameRoll = Mathf.SmoothDamp(frameRoll, desiredRoll + scrapePulse, ref frameRollVelocity, frameResponse, 100f, dt);
            framePitch = Mathf.SmoothDamp(framePitch, desiredPitch, ref framePitchVelocity, frameResponse, 100f, dt);
            frameYaw = Mathf.SmoothDamp(frameYaw, desiredYaw, ref frameYawVelocity, frameResponse, 100f, dt);
            cartVisualRoot.localPosition = frameRestPosition + Vector3.down * (landingCompression * 0.075f);
            cartVisualRoot.localRotation = frameRestRotation * Quaternion.Euler(framePitch, frameYaw, frameRoll);

            if (riderReactionRoot != null)
            {
                riderRoll = Mathf.SmoothDamp(riderRoll, -frameRoll * 0.58f, ref riderRollVelocity, riderResponse, 100f, dt);
                riderPitch = Mathf.SmoothDamp(riderPitch, -landingCompression * 5f - turnRate * 0.015f,
                    ref riderPitchVelocity, riderResponse, 100f, dt);
                riderReactionRoot.localRotation = riderRestRotation * Quaternion.Euler(riderPitch, 0f, riderRoll);
            }

            if (pizzaBoxPivot != null)
            {
                // The pivot stays at the palm-up support point. Rotation only moves the
                // box around that point, so it still reads as balanced on the fingers.
                float desiredPizzaRoll = Mathf.Clamp(-(frameRoll + riderRoll) * 0.75f,
                    -maximumPizzaTilt, maximumPizzaTilt);
                float desiredPizzaPitch = Mathf.Clamp(-riderPitch * 0.45f - landingCompression * 1.7f,
                    -maximumPizzaTilt, maximumPizzaTilt);
                pizzaRoll = Mathf.SmoothDamp(pizzaRoll, desiredPizzaRoll, ref pizzaRollVelocity, pizzaResponse, 100f, dt);
                pizzaPitch = Mathf.SmoothDamp(pizzaPitch, desiredPizzaPitch, ref pizzaPitchVelocity, pizzaResponse, 100f, dt);
                pizzaBoxPivot.localRotation = pizzaRestRotation * Quaternion.Euler(pizzaPitch, 0f, pizzaRoll);
            }

            wheelAngle = Mathf.Repeat(wheelAngle + drive.Speed / wheelRadius * Mathf.Rad2Deg * dt, 360f);
            for (int i = 0; i < wheelRestRotations.Length; i++)
                if (wheelSpin[i] != null)
                    wheelSpin[i].localRotation = wheelRestRotations[i] * Quaternion.Euler(wheelAngle, 0f, 0f);

            float casterAngle = Mathf.Clamp(drive.Steering * 17f + lateral * 0.6f, -24f, 24f);
            for (int i = 0; i < casterRestRotations.Length; i++)
                if (casterYaw[i] != null)
                    casterYaw[i].localRotation = casterRestRotations[i] * Quaternion.Euler(0f, casterAngle, 0f);

            landingCompression = Mathf.MoveTowards(landingCompression, 0f, dt / 0.16f);
            scrapePulse = Mathf.MoveTowards(scrapePulse, 0f, dt * 18f);
            previousPosition = position;
            previousYaw = yaw;
            previousVerticalSpeed = drive.VerticalSpeed;
            previousScrapeTime = drive.LastScrapeTime;
            wasGrounded = grounded;
            hasSample = true;
        }

        private void OnValidate()
        {
            wheelRadius = Mathf.Max(0.03f, wheelRadius);
            frameResponse = Mathf.Max(0.02f, frameResponse);
            riderResponse = Mathf.Max(0.02f, riderResponse);
            pizzaResponse = Mathf.Max(0.02f, pizzaResponse);
        }
    }
}
