using UnityEngine;

namespace DeliveryDash.Pixel
{
    public enum PixelSurface
    {
        Road,
        Wood,
        Slick,
        Conveyor
    }

    // The route owns distance and elevation. This class owns the cart's predictable response.
    public sealed class PixelRunController : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float cruiseSpeed = 11.5f;
        [SerializeField, Min(1f)] private float launchAcceleration = 4.8f;
        [SerializeField, Min(1f)] private float steeringResponse = 9f;
        [SerializeField, Min(1f)] private float lateralResponse = 5.5f;

        private float lateralVelocity;
        private float pizzaVelocity;
        private float cartLeanVelocity;
        private float bumpTime;
        private float bumpDuration;
        private float bumpHeight;
        private float jumpTime;
        private float jumpDuration;
        private float jumpHeight;
        private float scrapeCooldown;
        private float offRoadDamageClock;
        private float conveyorTime;
        private float conveyorBoost;

        public float DistanceMeters { get; private set; }
        public float LateralOffset { get; private set; }
        public float SpeedMetersPerSecond { get; private set; }
        public float Steer { get; private set; }
        public float CartLean { get; private set; }
        public float RiderLag { get; private set; }
        public float PizzaTilt { get; private set; }
        public float BumpDisplacement { get; private set; }
        public float Condition { get; private set; } = 100f;
        public float LastImpactStrength { get; private set; }
        public bool IsAirborne => jumpTime > 0f;
        public bool IsOffRoad { get; private set; }
        public bool IsScraping => scrapeCooldown > 0.25f;
        public float CruiseSpeed => cruiseSpeed;

        public void ResetRun()
        {
            DistanceMeters = 0f;
            LateralOffset = 0f;
            SpeedMetersPerSecond = 2.5f;
            Steer = 0f;
            CartLean = 0f;
            RiderLag = 0f;
            PizzaTilt = 0f;
            BumpDisplacement = 0f;
            Condition = 100f;
            LastImpactStrength = 0f;
            lateralVelocity = 0f;
            pizzaVelocity = 0f;
            cartLeanVelocity = 0f;
            bumpTime = 0f;
            jumpTime = 0f;
            scrapeCooldown = 0f;
            conveyorTime = 0f;
            conveyorBoost = 0f;
            offRoadDamageClock = 0f;
            IsOffRoad = false;
        }

        public void Step(float deltaTime, float steeringInput, float oldRoadCenter, float newRoadCenter,
            float roadWidth, PixelSurface surface)
        {
            float dt = Mathf.Clamp(deltaTime, 0f, 0.05f);
            if (dt <= 0f) return;

            scrapeCooldown = Mathf.Max(0f, scrapeCooldown - dt);
            LastImpactStrength = Mathf.MoveTowards(LastImpactStrength, 0f, 2.5f * dt);
            conveyorTime = Mathf.Max(0f, conveyorTime - dt);
            if (conveyorTime <= 0f) conveyorBoost = 0f;

            float grip = surface == PixelSurface.Slick ? 2.2f : lateralResponse;
            Steer = Mathf.MoveTowards(Steer, Mathf.Clamp(steeringInput, -1f, 1f), steeringResponse * dt);
            float targetLateralVelocity = Steer * Mathf.Max(5f, SpeedMetersPerSecond) * 0.43f;
            lateralVelocity = Mathf.Lerp(lateralVelocity, targetLateralVelocity, 1f - Mathf.Exp(-grip * dt));
            LateralOffset += lateralVelocity * dt + oldRoadCenter - newRoadCenter;

            float halfWidth = Mathf.Max(2.5f, roadWidth * 0.5f);
            float edge = halfWidth - 0.45f;
            IsOffRoad = Mathf.Abs(LateralOffset) > edge;
            if (IsOffRoad)
            {
                float beyond = Mathf.Abs(LateralOffset) - edge;
                float sign = Mathf.Sign(LateralOffset);
                lateralVelocity -= sign * Mathf.Min(10f, 3.2f + beyond * 2f) * dt;
                LateralOffset = Mathf.MoveTowards(LateralOffset, sign * edge, 0.75f * dt);
                offRoadDamageClock += dt;
                if (offRoadDamageClock >= 0.8f)
                {
                    Condition = Mathf.Max(0f, Condition - 1.5f);
                    offRoadDamageClock = 0f;
                }
            }
            else offRoadDamageClock = 0f;

            if (Mathf.Abs(LateralOffset) > halfWidth + 0.8f)
            {
                LateralOffset = Mathf.Sign(LateralOffset) * (halfWidth + 0.8f);
                Scrape(0.65f);
            }

            float surfaceSpeed = cruiseSpeed + conveyorBoost;
            if (surface == PixelSurface.Slick) surfaceSpeed *= 0.96f;
            if (IsOffRoad) surfaceSpeed *= 0.58f;
            float rate = SpeedMetersPerSecond < surfaceSpeed ? launchAcceleration : 9f;
            SpeedMetersPerSecond = Mathf.MoveTowards(SpeedMetersPerSecond, surfaceSpeed, rate * dt);
            float forwardSpeed = SpeedMetersPerSecond * (IsAirborne ? 1.03f : 1f);
            DistanceMeters += forwardSpeed * dt;

            float curveForce = (newRoadCenter - oldRoadCenter) / dt;
            float targetLean = Mathf.Clamp(-Steer * 8f - curveForce * 0.45f, -13f, 13f);
            CartLean = Mathf.SmoothDamp(CartLean, targetLean, ref cartLeanVelocity, 0.12f, 100f, dt);
            RiderLag = Mathf.Lerp(RiderLag, -CartLean * 0.62f - lateralVelocity * 0.22f,
                1f - Mathf.Exp(-4.5f * dt));
            float targetPizzaTilt = Mathf.Clamp(-CartLean * 0.28f - RiderLag * 0.2f, -9f, 9f);
            float spring = (targetPizzaTilt - PizzaTilt) * 32f - pizzaVelocity * 8f;
            pizzaVelocity += spring * dt;
            PizzaTilt = Mathf.Clamp(PizzaTilt + pizzaVelocity * dt, -24f, 24f);
            UpdateVerticalMotion(dt);
        }

        private void UpdateVerticalMotion(float dt)
        {
            float y = 0f;
            if (bumpTime > 0f)
            {
                bumpTime = Mathf.Max(0f, bumpTime - dt);
                float phase = 1f - bumpTime / bumpDuration;
                y += Mathf.Sin(phase * Mathf.PI) * bumpHeight;
            }
            if (jumpTime > 0f)
            {
                jumpTime = Mathf.Max(0f, jumpTime - dt);
                float phase = 1f - jumpTime / jumpDuration;
                y += Mathf.Sin(phase * Mathf.PI) * jumpHeight;
                if (jumpTime <= 0f)
                {
                    pizzaVelocity += Mathf.Sign(PizzaTilt == 0f ? 1f : PizzaTilt) * 16f;
                    LastImpactStrength = Mathf.Max(LastImpactStrength, 0.45f);
                }
            }
            BumpDisplacement = y;
        }

        public void Bump(float severity)
        {
            float amount = Mathf.Clamp(severity, 0.2f, 1f);
            bumpDuration = 0.33f + amount * 0.08f;
            bumpTime = bumpDuration;
            bumpHeight = 0.2f + amount * 0.35f;
            pizzaVelocity += (Steer < 0f ? -1f : 1f) * (10f + 12f * amount);
            LastImpactStrength = Mathf.Max(LastImpactStrength, amount * 0.55f);
        }

        public void Ramp(float severity)
        {
            float amount = Mathf.Clamp(severity, 0.2f, 1f);
            jumpDuration = 0.55f + 0.23f * amount;
            jumpTime = jumpDuration;
            jumpHeight = 0.62f + 0.45f * amount;
            pizzaVelocity += Mathf.Sign(Steer == 0f ? 1f : Steer) * 9f;
        }

        public void Conveyor(float severity)
        {
            conveyorBoost = 2f + 2.7f * Mathf.Clamp01(severity);
            conveyorTime = 1.8f;
        }

        public void Scrape(float severity)
        {
            if (scrapeCooldown > 0f) return;
            float amount = Mathf.Clamp(severity, 0.2f, 1f);
            scrapeCooldown = 0.7f;
            SpeedMetersPerSecond = Mathf.Max(5f, SpeedMetersPerSecond * (1f - 0.3f * amount));
            Condition = Mathf.Max(0f, Condition - (2.5f + 5f * amount));
            pizzaVelocity += Mathf.Sign(LateralOffset == 0f ? 1f : LateralOffset) * 26f * amount;
            LastImpactStrength = Mathf.Max(LastImpactStrength, amount);
        }

        public void Repair(float amount)
        {
            Condition = Mathf.Min(100f, Condition + Mathf.Max(0f, amount));
        }

        public void SetDistanceForDebug(float distance)
        {
            DistanceMeters = Mathf.Max(0f, distance);
        }
    }
}
