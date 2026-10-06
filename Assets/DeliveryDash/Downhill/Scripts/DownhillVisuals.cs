using System.Collections.Generic;
using UnityEngine;

namespace DeliveryDash.Downhill
{
    public sealed class DownhillVisuals : MonoBehaviour
    {
        public Transform Frame, Rider, Pizza;

        private DownhillCart cart;
        private readonly List<Transform> wheels = new List<Transform>();
        private readonly List<Quaternion> wheelRest = new List<Quaternion>();
        private Transform grip, upperArm, lowerArm, leftHand;
        private Quaternion frameRest, riderRest, upperRest, lowerRest;
        private Vector3 framePosition, riderPosition;
        private float spin, roll, riderLag, pizzaRoll, lastDistance;
        private bool wasRunning;
        private int resetVersion = -1;
        private bool wasAirborne;
        private Transform cap;
        private Vector3 capPosition, pizzaPosition;
        private Quaternion capRotation;
        private float riderLift, pizzaLift, pizzaLiftVelocity, capLift, capLiftVelocity;
        public float LeftGripGap { get; private set; }

        private void Start()
        {
            cart = GetComponent<DownhillCart>();
            if (Frame == null || Rider == null || Pizza == null) { enabled = false; return; }
            frameRest = Frame.localRotation;
            framePosition = Frame.localPosition;
            riderRest = Rider.localRotation;
            riderPosition = Rider.localPosition;
            pizzaPosition = Pizza.localPosition;
            foreach (Transform child in Frame.GetComponentsInChildren<Transform>())
            {
                if (child.name.StartsWith("WheelSpin_"))
                {
                    wheels.Add(child);
                    wheelRest.Add(child.localRotation);
                }
                else if (child.name.StartsWith("Shopping cart |")) grip = child;
                else if (child.name == "upperarm_l") upperArm = child;
                else if (child.name == "lowerarm_l") lowerArm = child;
                else if (child.name == "hand_l") leftHand = child;
                else if (child.name == "Backward red baseball cap") cap = child;
            }
            if (cap != null) { capPosition = cap.localPosition; capRotation = cap.localRotation; }
            if (upperArm != null && lowerArm != null)
            {
                upperRest = upperArm.localRotation;
                lowerRest = lowerArm.localRotation;
            }
        }

        private void LateUpdate()
        {
            if (cart == null || cart.Crashed) return;
            float dt = Mathf.Min(Time.deltaTime, .05f);
            if (cart.ResetVersion != resetVersion || (cart.Running && !wasRunning) || cart.Distance + 5f < lastDistance)
            {
                spin = roll = riderLag = pizzaRoll = 0f;
                riderLift = pizzaLift = pizzaLiftVelocity = capLift = capLiftVelocity = 0f;
                wasAirborne = false;
                resetVersion = cart.ResetVersion;
                Frame.localRotation = frameRest;
                Frame.localPosition = framePosition;
                Rider.localRotation = riderRest;
                Rider.localPosition = riderPosition;
                Pizza.localPosition = pizzaPosition;
                if (cap != null) { cap.localPosition = capPosition; cap.localRotation = capRotation; }
            }
            lastDistance = cart.Distance;
            wasRunning = cart.Running;

            float lean = -cart.Steer * Mathf.Clamp(cart.Speed * .09f, 0f, 2f);
            if (cart.Scraping) lean += Mathf.Sign(cart.Slip) * 2f;
            lean += cart.ObstacleSide * Mathf.Min(2f, cart.ObstacleImpact * .2f);
            roll = Mathf.Lerp(roll, lean, 1f - Mathf.Exp(-12f * dt));
            Frame.localRotation = frameRest * Quaternion.Euler(cart.Airborne ? -2f : cart.Compression * 3f, 0f, roll);
            Frame.localPosition = framePosition + Vector3.down * cart.Compression * .06f;

            // The body reacts independently while a two-bone correction keeps the
            // left palm on the red handle. The box stays parented to the right hand.
            float targetLag = Mathf.Clamp(-cart.Slip * .075f, -2.5f, 2.5f);
            if(cart.Spinning)targetLag+=Mathf.Sin(cart.SpinProgress*Mathf.PI*4f)*3f;
            if (cart.LandingImpact > 1f) targetLag += Mathf.Min(2f, cart.LandingImpact * .2f);
            riderLag = Mathf.Lerp(riderLag, targetLag, 1f - Mathf.Exp(-7f * dt));
            if (cart.Airborne && !wasAirborne)
            {
                pizzaLiftVelocity = 2.3f;
                capLiftVelocity = 3.3f;
            }
            wasAirborne = cart.Airborne;
            riderLift = Mathf.Lerp(riderLift, cart.Airborne ? .19f : 0f, 1f - Mathf.Exp(-12f * dt));
            AdvanceBounce(ref pizzaLift, ref pizzaLiftVelocity, dt, 8f);
            AdvanceBounce(ref capLift, ref capLiftVelocity, dt, 8.5f);
            Rider.localRotation = riderRest * Quaternion.Euler(cart.Airborne ? -2f : cart.Compression * 2f,
                0f, -roll * .3f + riderLag - cart.Steer * Mathf.Clamp(cart.Speed * .3f, 0f, 6f));
            Rider.localPosition = riderPosition + Vector3.up * (riderLift - Mathf.Min(.025f, cart.Compression * .02f));
            KeepLeftGrip();
            if (cap != null)
            {
                cap.position = cap.parent.TransformPoint(capPosition) + Vector3.up * capLift;
                cap.localRotation = capRotation * Quaternion.Euler(-capLift * 25f, 0f, capLift * 16f);
            }

            float desiredPizzaRoll = Mathf.Clamp(-cart.Slip * .08f - roll * .18f, -4f, 4f);
            pizzaRoll = Mathf.Lerp(pizzaRoll, desiredPizzaRoll, 1f - Mathf.Exp(-6f * dt));
            Vector3 horizontalForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Quaternion level = Quaternion.LookRotation(horizontalForward, Vector3.up)
                * Quaternion.Euler(0f, 0f, pizzaRoll);
            // Setting world rotation leaves the fingertip support point attached.
            Pizza.rotation = level;
            Pizza.position = Pizza.parent.TransformPoint(pizzaPosition) + Vector3.up * pizzaLift;
            if (cart.Running) spin += cart.Speed / .145f * Mathf.Rad2Deg * dt;
            for (int i = 0; i < wheels.Count; i++)
                wheels[i].localRotation = wheelRest[i] * Quaternion.Euler(spin, 0f, 0f);
        }

        public void SettleForDelivery(float dt)
        {
            float blend = 1f - Mathf.Exp(-16f * dt);
            riderLift = pizzaLift = pizzaLiftVelocity = capLift = capLiftVelocity = 0f;
            roll = riderLag = pizzaRoll = 0f;
            Frame.localRotation = Quaternion.Slerp(Frame.localRotation, frameRest, blend);
            Frame.localPosition = Vector3.Lerp(Frame.localPosition, framePosition, blend);
            Rider.localRotation = Quaternion.Slerp(Rider.localRotation, riderRest, blend);
            Rider.localPosition = Vector3.Lerp(Rider.localPosition, riderPosition, blend);
            Pizza.localPosition = Vector3.Lerp(Pizza.localPosition, pizzaPosition, blend);
            Pizza.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, Vector3.up), Vector3.up);
            if (cap != null)
            {
                cap.localPosition = Vector3.Lerp(cap.localPosition, capPosition, blend);
                cap.localRotation = Quaternion.Slerp(cap.localRotation, capRotation, blend);
            }
            KeepLeftGrip();
        }

        private static void AdvanceBounce(ref float lift, ref float velocity, float dt, float gravity)
        {
            if (lift <= 0f && velocity <= 0f) { lift = velocity = 0f; return; }
            velocity -= gravity * dt;
            lift += velocity * dt;
            if (lift <= 0f) { lift = velocity = 0f; }
        }

        private void KeepLeftGrip()
        {
            if (grip == null || upperArm == null || lowerArm == null || leftHand == null)
            {
                LeftGripGap = float.PositiveInfinity;
                return;
            }
            upperArm.localRotation = upperRest;
            lowerArm.localRotation = lowerRest;
            Vector3 target = grip.TransformPoint(new Vector3(-.88f, 1.15f, -1.13f));
            Vector3 shoulder = upperArm.position;
            float upperLength = Vector3.Distance(upperArm.position, lowerArm.position);
            float lowerLength = Vector3.Distance(lowerArm.position, leftHand.position);
            Vector3 toTarget = target - shoulder;
            float distance = Mathf.Clamp(toTarget.magnitude, .02f, upperLength + lowerLength - .002f);
            Vector3 direction = toTarget.normalized;
            float along = (upperLength * upperLength - lowerLength * lowerLength + distance * distance)
                / (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
            Vector3 bend = Vector3.ProjectOnPlane(lowerArm.position - shoulder, direction).normalized;
            if (bend.sqrMagnitude < .01f) bend = Vector3.ProjectOnPlane(Vector3.forward, direction).normalized;
            Vector3 elbow = shoulder + direction * along + bend * height;
            upperArm.rotation = Quaternion.FromToRotation(lowerArm.position - shoulder, elbow - shoulder) * upperArm.rotation;
            lowerArm.rotation = Quaternion.FromToRotation(leftHand.position - lowerArm.position,
                target - lowerArm.position) * lowerArm.rotation;
            LeftGripGap = Vector3.Distance(leftHand.position, target);
        }
    }
}
