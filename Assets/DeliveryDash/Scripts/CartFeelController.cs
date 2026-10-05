using UnityEngine;

namespace DeliveryDash
{
    // Automatic cruising with optional player acceleration. Service docking takes priority.
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
        [Header("Town driving")]
        [SerializeField] private bool townDriving;
        [SerializeField] private float townTurnRate = 100f;
        [SerializeField] private float maximumSpeed = 14f;
        public bool InputAcceleration => Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.UpArrow);
        public float MaximumSpeed => maximumSpeed;
        public float CruiseSpeed => cruiseSpeed;
        public void ConfigureRide(float cruise,float maximum,float acceleration,float turnRate)
        {cruiseSpeed=cruise;maximumSpeed=Mathf.Max(cruise,maximum);launchAcceleration=acceleration;townTurnRate=turnRate;}
        public static float RequestedSpeed(float cruise,float maximum,bool accelerate,float steer) => (accelerate?maximum:cruise)*(1-Mathf.Abs(steer)*.35f);
        private TownDeliveryWorld town;
        private Vector3 serviceTarget;
        private float serviceHold, serviceYaw, serviceTimeout;
        private int servicePhase;
        public bool IsServicing => servicePhase != 0;
        public bool IsHandingOff => servicePhase == 2;
        public bool ServiceSucceeded { get; private set; }

        private CharacterController body;
        private float speed;
        private float steering;
        private float heading;
        private float lateralSpeed;
        private float verticalSpeed;
        private float lastScrapeTime = float.NegativeInfinity;
        private float stalledFor;
        private Vector3 lastSafePosition;
        private EndlessRoadGenerator endlessRoad;

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
            endlessRoad = FindFirstObjectByType<EndlessRoadGenerator>();
            town = FindFirstObjectByType<TownDeliveryWorld>();
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
            if (townDriving) { DriveTown(Time.fixedDeltaTime); return; }
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
                    if (endlessRoad != null && endlessRoad.isActiveAndEnabled) endlessRoad.RecoverToRoad();
                    else ResetAt(lastSafePosition, Quaternion.identity);
                    stalledFor = 0f;
                }
            }
        }

        public void ResetAt(Vector3 position, Quaternion rotation)
        {
            if (body == null) EnsureBody();
            body.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            body.enabled = true;
            speed = 0f;
            steering = 0f;
            heading = rotation.eulerAngles.y;
            lateralSpeed = 0f;
            verticalSpeed = 0f;
            lastSafePosition = position;
            stalledFor = 0f;
            lastScrapeTime = float.NegativeInfinity;
            servicePhase = 0;
            ServiceSucceeded = false;
        }

        public void ShiftWorld(Vector3 delta)
        {
            body.enabled = false;
            transform.position -= delta;
            lastSafePosition -= delta;
            serviceTarget -= delta;
            body.enabled = true;
        }

        public void ConfigureTownDriving()
        {
            townDriving = true;
            cruiseSpeed = 8f;
        }

        public void BeginService(Vector3 stop, float holdSeconds = 1.6f, Vector3? streetDirection = null)
        {
            if (!townDriving || IsServicing || !enabled) return;
            serviceTarget = stop;
            serviceHold = holdSeconds;
            Vector3 departure = streetDirection ?? Vector3.forward;
            if (Vector3.Dot(departure, transform.forward) < 0) departure = -departure;
            serviceYaw = Quaternion.LookRotation(departure).eulerAngles.y;
            serviceTimeout = 0;
            ServiceSucceeded = false;
            servicePhase = 1;
        }

        public void CancelService()
        {
            servicePhase = 0; ServiceSucceeded = false;
        }

        private void DriveTown(float dt, bool? accelerationInput = null)
        {
            Vector3 before = transform.position;
            Vector3 motion;
            if (IsServicing)
            {
                steering = Mathf.MoveTowards(steering, 0, dt * 8);
                lateralSpeed = 0;
                serviceTimeout += dt;
                Vector3 offset = serviceTarget - before; offset.y = 0;
                if (servicePhase == 1 && offset.magnitude > .06f)
                {
                    float desired = Mathf.Min(cruiseSpeed, Mathf.Sqrt(12f * offset.magnitude));
                    speed = Mathf.MoveTowards(speed, desired, dt * 12);
                    motion = offset.normalized * Mathf.Min(offset.magnitude, Mathf.Max(.4f, speed) * dt);
                    heading = Mathf.MoveTowardsAngle(heading, serviceYaw, dt * 100);
                    if (serviceTimeout > 6) { servicePhase = 0; ServiceSucceeded = false; }
                }
                else
                {
                    servicePhase = 2; speed = 0; motion = Vector3.zero;
                    serviceHold -= dt;
                    heading = Mathf.MoveTowardsAngle(heading, serviceYaw, dt * 100);
                    if (serviceHold <= 0) { servicePhase = 0; ServiceSucceeded = true; }
                }
            }
            else
            {
                float input = 0;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) input -= 1;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) input += 1;
                steering = Mathf.MoveTowards(steering, input, steeringResponse * dt);
                heading = Mathf.Repeat(heading + steering * townTurnRate * dt, 360);
                speed = Mathf.MoveTowards(speed, RequestedSpeed(cruiseSpeed,maximumSpeed,accelerationInput ?? InputAcceleration,steering), launchAcceleration * dt);
                motion = Quaternion.Euler(0, heading, 0) * Vector3.forward * speed * dt;
                lateralSpeed = steering * speed * .2f;
            }
            transform.rotation = Quaternion.Euler(0, heading, 0);
            if (body.isGrounded && verticalSpeed < 0) verticalSpeed = -groundStick;
            verticalSpeed -= gravity * dt;
            motion.y = verticalSpeed * dt;
            CollisionFlags hits = body.Move(motion);
            if ((hits & CollisionFlags.Below) != 0) verticalSpeed = -groundStick;
            if ((hits & CollisionFlags.Sides) != 0) { lastScrapeTime = Time.time; speed = Mathf.Min(speed, 3); }
            float moved = Vector3.ProjectOnPlane(transform.position - before, Vector3.up).magnitude;
            if (!IsServicing && body.isGrounded && moved < .005f && speed > 1)
            {
                stalledFor += dt;
                if (stalledFor > 2 && town != null) town.RecoverRunner();
            }
            else stalledFor = 0;
        }
    }
}
