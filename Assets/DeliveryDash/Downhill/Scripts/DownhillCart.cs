using UnityEngine;

namespace DeliveryDash.Downhill
{
    public sealed class DownhillCart : MonoBehaviour
    {
        [SerializeField] private float startSpeed = 9f;
        [SerializeField] private float maximumSpeed = 22f;
        [SerializeField] private float wheelbase = 1.25f;
        [SerializeField] private float halfTrack = .55f;
        [SerializeField] private float rideHeight = .09f;
        [SerializeField] private float centerOfMassHeight = .75f;
        [SerializeField] private bool readKeyboard = true;
        private CourseGraph course;
        private Vector3 velocity;
        private float heading;
        private float steering;
        private float compression;
        private float compressionVelocity;
        private float furthest;
        private float condition = 100f;
        private float eventDistance = -100f;
        private bool running;
        private bool airborne;
        private Vector3 groundNormal = Vector3.up;
        private float bodyRoll, rollVelocity, lateralAcceleration, toppleTime, airborneTime;
        private bool hopRequested;
        private float hopReadyTime;
        private float spinTime,spinStartHeading,spinDirection,spinReadyTime,lastSteeringDirection=1f;
        private const float SpinDuration=1.25f;
        private readonly Vector3[] wheelPoints = new Vector3[4];

        public float Speed => new Vector2(velocity.x, velocity.z).magnitude;
        public float Steer => steering;
        public float Slip { get; private set; }
        public float Compression => compression;
        public bool Airborne => airborne;
        public float Progress => course == null ? 0f : Mathf.Clamp01(furthest / course.Length);
        public float Condition => condition;
        public Vector3 Velocity => velocity;
        public float Distance => furthest;
        public bool Running => running;
        public int GroundContacts { get; private set; }
        public int Recoveries { get; private set; }
        public int ResetVersion { get; private set; }
        public int Landings { get; private set; }
        public int Scrapes { get; private set; }
        public bool Scraping { get; private set; }
        public float LandingImpact { get; private set; }
        public float LongestScrapeSeconds { get; private set; }
        public int Launches { get; private set; }
        public int Hops { get; private set; }
        public float HopCooldown => Mathf.Max(0f, hopReadyTime - SimulationTime);
        public CourseGraph Course => course;
        public bool Crashed { get; private set; }
        public float CrashSpeed { get; private set; }
        public float CrashSide { get; private set; }
        public float SimulationTime { get; private set; }
        public float Heading => heading;
        public int RouteId { get; private set; }
        public float ObstacleImpact { get; private set; }
        public float ObstacleSide { get; private set; }
        public int ObstacleHits { get; private set; }
        public float BodyRoll => bodyRoll;
        public float TipAmount => Mathf.Clamp01(Mathf.Abs(bodyRoll) / TipAngle);
        public string CrashReason { get; private set; } = "";
        public string LastObstacleId { get; private set; } = "";
        public int Spins { get; private set; }
        public bool Spinning => spinTime>0;
        public float SpinProgress => Spinning?Mathf.Clamp01((SpinDuration-spinTime)/SpinDuration):0;
        private float TipAngle => Mathf.Atan2(halfTrack, centerOfMassHeight) * Mathf.Rad2Deg;

        public void Configure(CourseGraph graph)
        {
            course = graph;
            Crashed = false;
            CrashReason = "";
            CrashSpeed = CrashSide = 0f;
            running = false;
            steering = 0f;
            compression = compressionVelocity = 0f;
            furthest = 0f;
            eventDistance = -100f;
            condition = 100f;
            Recoveries = 0;
            airborne = false;
            Scraping = false;
            LandingImpact = 0f;
            LongestScrapeSeconds = 0f;
            Scrapes = Launches = Landings = 0;
            ObstacleHits = 0;
            RouteId = 0;
            SimulationTime = ObstacleImpact = ObstacleSide = 0f;
            bodyRoll = rollVelocity = lateralAcceleration = toppleTime = airborneTime = 0f;
            hopRequested = false;
            hopReadyTime = 0f;
            Hops = 0;
            Spins=0;spinTime=spinReadyTime=0;spinDirection=lastSteeringDirection=1;
            LastObstacleId="";
            GroundContacts = graph == null ? 0 : 4;
            Slip = 0f;
            velocity = Vector3.zero;
            groundNormal = Vector3.up;
            ResetVersion++;
            if (graph == null) return;
            rideHeight = Mathf.Max(.09f, rideHeight);
            ConfigureWheelPoints();
            // All four wheels start inside the ribbon rather than hanging behind its first strip.
            CourseSample start = graph.Sample(Mathf.Min(1f, graph.Length));
            furthest = start.Distance;
            Vector3 forward = Flat(start.Forward);
            heading = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            transform.position = start.Position + Vector3.up * rideHeight;
            transform.rotation = Quaternion.LookRotation(start.Forward, Vector3.up);
            groundNormal = Vector3.ProjectOnPlane(Vector3.up, start.Forward).normalized;
            velocity = forward * startSpeed;
            velocity.y = start.Forward.y / Mathf.Max(.1f, FlatMagnitude(start.Forward)) * startSpeed;
        }

        public void Begin() { if (course != null && !Crashed) running = true; }
        public void Stop() => running = false;
        public void SetKeyboardControl(bool enabled) => readKeyboard = enabled;

        void Update()
        {
            if (readKeyboard && Time.timeScale > 0f && Input.GetKeyDown(KeyCode.Space)) RequestHop();
        }

        public bool RequestHop()
        {
            if (!running || Crashed || airborne || hopRequested || GroundContacts < 2 || Speed < 5f || HopCooldown > 0f) return false;
            hopRequested = true;
            hopReadyTime = SimulationTime + 3.2f;
            return true;
        }

        private void FixedUpdate()
        {
            if (!readKeyboard || !running) return;
            float input = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) input -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) input += 1f;
            Step(Time.fixedDeltaTime, input);
        }

        public void Step(float dt, float steer)
        {
            if (!running || course == null || dt <= 0f) return;
            // Bound integration steps for replay and frame-rate-independent collision recovery.
            dt = Mathf.Min(dt, .25f);
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt / .02f));
            float step = dt / steps;
            for (int i = 0; i < steps && running; i++) Integrate(step, Mathf.Clamp(steer, -1f, 1f));
        }

        private void Integrate(float dt, float input)
        {
            SimulationTime += dt;
            LandingImpact = Mathf.MoveTowards(LandingImpact, 0f, dt * 18f);
            ObstacleImpact = Mathf.MoveTowards(ObstacleImpact, 0f, dt * 16f);
            CourseSample sample = course.Closest(transform.position);
            RouteId = sample.RouteId;
            Vector3 routeForward = Flat(sample.Forward);
            float speed = Mathf.Max(1f, Speed);
            steering = Mathf.MoveTowards(steering, input, dt / .11f);
            if(Mathf.Abs(input)>.15f)lastSteeringDirection=Mathf.Sign(input);
            var fluid=!airborne&&!hopRequested&&!Spinning&&SimulationTime>=spinReadyTime?course.FluidAt(transform.position,.35f):null;
            if(fluid!=null)
            {
                spinStartHeading=heading;spinDirection=lastSteeringDirection;spinTime=SpinDuration;
                spinReadyTime=SimulationTime+3.5f;Spins++;
                Vector3 side=Vector3.Cross(Vector3.up,routeForward);
                velocity+=side*spinDirection*2.1f;
                rollVelocity+=spinDirection*12f;
            }
            if (hopRequested)
            {
                hopRequested = false;
                airborne = true;
                airborneTime = 0f;
                Launches++;
                Hops++;
                eventDistance = sample.Distance;
                // A small suspension hop clears low clutter, but gives up grip and
                // carries the existing slide into landing. Holding Space cannot repeat it.
                float tangent = Vector3.Dot(velocity, routeForward) * sample.Forward.y /
                    Mathf.Max(.1f, FlatMagnitude(sample.Forward));
                velocity.y = tangent + 5.4f;
                velocity += Vector3.Cross(Vector3.up, routeForward) * steering * .45f;
                rollVelocity += steering * 12f;
                condition = Mathf.Max(0f, condition - .6f);
            }
            // Caster steering follows signed travel along the chassis. Spinning the
            // chassis at full road speed while it slides sideways creates 180-degree
            // steering loops and prevents a countersteer from catching the wheels.
            Vector3 chassisForward = Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
            float longitudinalSpeed = Vector3.Dot(velocity, chassisForward);
            float yaw = longitudinalSpeed / wheelbase * Mathf.Tan(steering * 16f * Mathf.Deg2Rad);
            yaw = Mathf.Clamp(yaw, -1.1f, 1.1f);
            if(Spinning)
            {
                spinTime=Mathf.Max(0,spinTime-dt);
                float phase=(SpinDuration-spinTime)/SpinDuration;
                heading=spinStartHeading+spinDirection*360f*Mathf.SmoothStep(0,1,phase);
            }
            else heading += yaw * Mathf.Rad2Deg * dt * (airborne ? .25f : 1f);
            Vector3 forward = Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            float grade = -sample.Forward.y / Mathf.Max(.1f, FlatMagnitude(sample.Forward));
            Vector3 planar = new Vector3(velocity.x, 0f, velocity.z);
            Vector3 previousPlanar = planar;
            if (!airborne)
            {
                // Gravity follows the road gradient, including when the cart crosses it sideways.
                // Rolling resistance and tire scrub remove energy without imposing cruise speed.
                float slopeAcceleration = 9.81f * grade / (1f + grade * grade);
                planar += routeForward * slopeAcceleration * dt;
                if (sample.Surface == CourseSurface.Conveyor) planar += routeForward * 2.8f * dt;
                planar = Vector3.MoveTowards(planar, Vector3.zero,
                    (.12f + planar.sqrMagnitude * .0011f) * dt);
                float sideSpeed = Vector3.Dot(planar, right);
                float grip=sample.Grip*(Spinning?.12f:1f);
                float tireForce = Mathf.Clamp(-sideSpeed * 8f, -9.5f * grip, 9.5f * grip);
                planar += right * tireForce * dt;
                // Strong steering at speed exceeds grip naturally; releasing it allows a clean regrip.
                float velocityHeading = Mathf.Atan2(planar.x, planar.z) * Mathf.Rad2Deg;
                if (!Spinning&&Mathf.Abs(input) < .15f)
                    heading = Mathf.LerpAngle(heading, velocityHeading, 1f - Mathf.Exp(-2.2f * dt));
            }
            if (planar.magnitude > maximumSpeed) planar = planar.normalized * maximumSpeed;
            velocity.x = planar.x;
            velocity.z = planar.z;
            Slip = Vector3.SignedAngle(forward, planar.normalized, Vector3.up);
            UpdateRoll(dt, Vector3.Dot((planar - previousPlanar) / dt, right));
            if (Crashed) return;

            Vector3 next = transform.position + velocity * dt;
            if (!ResolveObstacle(ref next)) return;
            forward = Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
            right = Vector3.Cross(Vector3.up, forward);
            CourseSample nextSample = course.Closest(next);
            RouteId = nextSample.RouteId;
            float groundY = ContactHeight(next, forward, right, out Vector3 normal, out int contacts);
            // Road unions at a fork are valid ground. Nearest-centerline width tests incorrectly
            // reject a wheel which is already over the other branch's rendered triangles.
            if (!airborne && contacts < 4 && nextSample.Distance > wheelbase && nextSample.Distance < course.Length - wheelbase)
            {
                Vector3 routeRight = Vector3.Cross(Vector3.up, Flat(nextSample.Forward));
                Crash(Mathf.Sign(Vector3.Dot(next - nextSample.Position, routeRight)), "Edge");
                return;
            }
            float targetY = groundY + rideHeight + halfTrack * Mathf.Abs(Mathf.Sin(bodyRoll * Mathf.Deg2Rad));
            Vector3 leadingPoint = next + forward * (wheelbase * .5f);
            CourseSample leading = course.Closest(leadingPoint);
            CourseSample approach = course.Sample(Mathf.Max(0f, leading.Distance - 1.5f), leading.RouteId);
            float approachGrade = -approach.Forward.y / Mathf.Max(.1f, FlatMagnitude(approach.Forward));
            float leadingGrade = -leading.Forward.y / Mathf.Max(.1f, FlatMagnitude(leading.Forward));
            float tangentFallSpeed = -approachGrade * Mathf.Max(0f, Vector3.Dot(planar, Flat(approach.Forward)));
            bool lip = leading.LaunchCue > .65f && nextSample.Distance - eventDistance > 24f
                && contacts >= 2 && Vector3.Dot(velocity, routeForward) > 7f
                && leadingGrade > approachGrade + .08f
                && targetY < transform.position.y + tangentFallSpeed * dt - .008f;
            if (lip && !airborne)
            {
                airborne = true;
                airborneTime = 0f;
                Launches++;
                eventDistance = nextSample.Distance;
                // Continue the existing downhill tangent. The steepening road falls away beneath it.
                velocity.y = Mathf.Min(-.02f, tangentFallSpeed);
            }
            if (!airborne && contacts < 2 && targetY < next.y - .18f) airborne = true;
            if (airborne)
            {
                airborneTime += dt;
                velocity.y -= 9.81f * dt;
                next.y = transform.position.y + velocity.y * dt;
                // Leaving the ribbon during flight is allowed. Missing contacts
                // become a failure only when the cart meets actual world ground.
                float outsideFloor=contacts>0?targetY:course.OutsideGroundHeight(next)+rideHeight;
                if(contacts<4&&airborneTime>.08f&&next.y<=outsideFloor
                    &&nextSample.Distance> wheelbase&&nextSample.Distance<course.Length-wheelbase)
                {
                    next.y=outsideFloor;
                    transform.position=next;
                    airborne=false;
                    GroundContacts=contacts;
                    Crash(Mathf.Sign(Vector3.Dot(next-nextSample.Position,right)),"Edge");
                    return;
                }
                if (contacts >= 2 && airborneTime > .08f && next.y <= targetY)
                {
                    float groundFallSpeed = Vector3.Dot(new Vector3(velocity.x, 0f, velocity.z),
                        Flat(nextSample.Forward)) * nextSample.Forward.y /
                        Mathf.Max(.1f, FlatMagnitude(nextSample.Forward));
                    float impact = Mathf.Max(0f, groundFallSpeed - velocity.y);
                    LandingImpact = Mathf.Max(LandingImpact, impact);
                    Landings++;
                    compressionVelocity += Mathf.Min(1.9f, impact * .18f);
                    if (impact > 6f) condition = Mathf.Max(0f, condition - (impact - 6f) * .7f);
                    next.y = targetY;
                    airborne = false;
                    velocity.y = groundFallSpeed;
                }
            }
            else
            {
                velocity.y = (targetY - transform.position.y) / Mathf.Max(.001f, dt);
                next.y = targetY;
            }
            if (nextSample.Event == CourseEvent.Bump && nextSample.Distance - eventDistance > 24f)
            {
                compressionVelocity += 1.15f;
                eventDistance = nextSample.Distance;
            }
            compressionVelocity += (-compression * 90f - compressionVelocity * 12f) * dt;
            compression += compressionVelocity * dt;
            compression = Mathf.Clamp(compression, -.12f, .24f);
            GroundContacts = airborne ? 0 : Mathf.Abs(bodyRoll) > 6f ? Mathf.Min(2, contacts) : contacts;
            groundNormal = airborne ? Vector3.Slerp(groundNormal, normal, 1f - Mathf.Exp(-10f * dt)) : normal;
            transform.position = next;
            Vector3 groundForward = Vector3.ProjectOnPlane(forward, groundNormal).normalized;
            if (airborne)
            {
                Vector3 flightForward = forward * Mathf.Max(1f, Speed) + Vector3.up * velocity.y;
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(flightForward, Vector3.up) * Quaternion.Euler(0f, 0f, bodyRoll),
                    1f - Mathf.Exp(-5f * dt));
            }
            else transform.rotation = Quaternion.LookRotation(groundForward, groundNormal)
                * Quaternion.Euler(0f, 0f, bodyRoll);
            furthest = Mathf.Max(furthest, nextSample.Distance);
            if (furthest >= course.Length - .4f) running = false;
        }

        private void Crash(float side, string reason)
        {
            if (Crashed) return;
            CrashSpeed = Speed;
            CrashSide = side;
            CrashReason = reason;
            Crashed = true;
            Scraping = true;
            Scrapes++;
            running = false;
            velocity = Vector3.zero;
        }

        private void UpdateRoll(float dt, float acceleration)
        {
            lateralAcceleration = Mathf.Lerp(lateralAcceleration, airborne ? 0f : acceleration,
                1f - Mathf.Exp(-10f * dt));
            float criticalAcceleration = 9.81f * halfTrack / Mathf.Max(.4f, centerOfMassHeight);
            float load = Mathf.Abs(lateralAcceleration) / criticalAcceleration;
            float angularAcceleration;
            if (airborne) angularAcceleration = -bodyRoll * 10f - rollVelocity * 4f;
            else if (load > 1f || Mathf.Abs(bodyRoll) > TipAngle * .65f)
            {
                // Once the inner wheels unload, integrate torque about the outer
                // contact line. Gravity restores until the COM passes that line.
                float side = Mathf.Abs(bodyRoll) > .5f ? Mathf.Sign(bodyRoll) : Mathf.Sign(lateralAcceleration);
                float radians = Mathf.Abs(bodyRoll) * Mathf.Deg2Rad;
                float c = Mathf.Cos(radians), s = Mathf.Sin(radians);
                float lateralMoment = lateralAcceleration * (centerOfMassHeight * c + halfTrack * s);
                float gravityMoment = side * 9.81f * (halfTrack * c - centerOfMassHeight * s);
                float inertia = centerOfMassHeight * centerOfMassHeight + halfTrack * halfTrack;
                angularAcceleration = (lateralMoment - gravityMoment) / inertia * Mathf.Rad2Deg
                    - rollVelocity * 7f;
                // Once the tires catch, the seated rider brings their weight back
                // over the frame. A balanced cart settles instead of hovering on
                // two wheels; sustained cornering still crosses the support edge.
                if (load < .7f && Mathf.Abs(Slip) < 9f && Mathf.Abs(bodyRoll) < TipAngle * .98f)
                    angularAcceleration += -bodyRoll * 32f - rollVelocity * 3f;
            }
            else
            {
                float target = lateralAcceleration / 9.81f * 5f;
                angularAcceleration = (target - bodyRoll) * 38f - rollVelocity * 10f;
            }
            if(Spinning)angularAcceleration+=Mathf.Sin(SpinProgress*Mathf.PI*4f)*150f;
            rollVelocity += angularAcceleration * dt;
            float rollLimit=load<.7f&&Mathf.Abs(Slip)<9f&&Mathf.Abs(bodyRoll)<TipAngle*.98f?45f:25f;
            rollVelocity = Mathf.Clamp(rollVelocity, -rollLimit, rollLimit);
            bodyRoll += rollVelocity * dt;
            // Sustained load lifts the inner wheels before the center of mass crosses
            // the support edge. Releasing or countersteering can unwind the spring.
            bool overBalance = !airborne && Mathf.Abs(bodyRoll) > TipAngle + .5f;
            toppleTime = overBalance ? toppleTime + dt : Mathf.Max(0f, toppleTime - dt * 2f);
            if (toppleTime > .22f) Crash(-Mathf.Sign(bodyRoll), "Topple");
        }

        private void ConfigureWheelPoints()
        {
            wheelPoints[0] = new Vector3(-halfTrack, 0f, wheelbase * .5f);
            wheelPoints[1] = new Vector3(halfTrack, 0f, wheelbase * .5f);
            wheelPoints[2] = new Vector3(-halfTrack, 0f, -wheelbase * .5f);
            wheelPoints[3] = new Vector3(halfTrack, 0f, -wheelbase * .5f);
            int found = 0;
            float maximumTrack = 0f, front = 0f, rear = 0f;
            foreach (Transform child in GetComponentsInChildren<Transform>())
            {
                int index = child.name == "WheelSpin_FL" ? 0 : child.name == "WheelSpin_FR" ? 1
                    : child.name == "WheelSpin_RL" ? 2 : child.name == "WheelSpin_RR" ? 3 : -1;
                if (index < 0) continue;
                Vector3 point = transform.InverseTransformPoint(child.position);
                point.y = 0f;
                wheelPoints[index] = point;
                maximumTrack = Mathf.Max(maximumTrack, Mathf.Abs(point.x));
                front = Mathf.Max(front, point.z); rear = Mathf.Min(rear, point.z);
                found++;
            }
            if (found == 4)
            {
                halfTrack = maximumTrack;
                wheelbase = front - rear;
            }
        }

        private bool ResolveObstacle(ref Vector3 next)
        {
            Vector3 centerOffset = Vector3.up * .55f;
            if (!course.TryObstacleHit(transform.position + centerOffset, next + centerOffset,
                .62f, SimulationTime, out CourseObstacle obstacle, out Vector3 normal,
                out Vector3 correctedPosition)) return true;
            normal.y = 0f;
            normal = normal.sqrMagnitude > .001f ? normal.normalized : -Flat(velocity);
            float closingSpeed = Mathf.Max(0f, -Vector3.Dot(velocity, normal));
            Vector3 right = Vector3.Cross(Vector3.up, Flat(course.Closest(next).Forward));
            float side = Mathf.Sign(Vector3.Dot(normal, right));
            ObstacleImpact=Mathf.Max(1f,closingSpeed);
            ObstacleSide=side;
            ObstacleHits++;
            LastObstacleId=obstacle.Id;
            Crash(side,"Impact");
            return false;
        }

        private float ContactHeight(Vector3 position, Vector3 forward, Vector3 right,
            out Vector3 normal, out int contacts)
        {
            float sum = 0f;
            contacts = 0;
            Vector3 surfaceNormals = Vector3.zero;
            Vector3 frontLeft = Vector3.zero, frontRight = Vector3.zero;
            Vector3 rearLeft = Vector3.zero, rearRight = Vector3.zero;
            for (int axle = 0; axle < 2; axle++)
            {
                for (int side = 0; side < 2; side++)
                {
                    Vector3 local = wheelPoints[axle * 2 + side];
                    Vector3 point = position + forward * local.z + right * local.x;
                    // Query the exact two road triangles used by CourseMeshBuilder.
                    // Missing wheels cannot contribute a projected centerline height.
                    if (!course.TrySurfaceHeight(point, out float height, out Vector3 surfaceNormal)) continue;
                    point.y = height;
                    contacts++;
                    sum += height;
                    surfaceNormals += surfaceNormal;
                    if (axle == 0) { if (side == 0) frontLeft = point; else frontRight = point; }
                    else { if (side == 0) rearLeft = point; else rearRight = point; }
                }
            }
            normal = surfaceNormals.normalized;
            if (contacts == 4)
            {
                Vector3 longitudinal = (frontLeft + frontRight - rearLeft - rearRight) * .5f;
                Vector3 across = (frontRight + rearRight - frontLeft - rearLeft) * .5f;
                normal = Vector3.Cross(longitudinal, across).normalized;
            }
            if (normal.y < 0f) normal = -normal;
            if (normal.sqrMagnitude < .5f) normal = groundNormal.sqrMagnitude > .5f ? groundNormal : Vector3.up;
            return contacts > 0 ? sum / contacts : course.Closest(position).Position.y;
        }

        private static float FlatMagnitude(Vector3 vector) => new Vector2(vector.x, vector.z).magnitude;
        private static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector.sqrMagnitude > .001f ? vector.normalized : Vector3.forward;
        }
    }
}
