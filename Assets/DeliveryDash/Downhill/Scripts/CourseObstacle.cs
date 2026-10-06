using System;
using UnityEngine;

namespace DeliveryDash.Downhill
{
    public enum ObstacleKind { Barrels, Crates, MarketTrolley, ParkedCar, CrossingCar, Fluid, Spikes }

    [Serializable]
    public sealed class CourseObstacle
    {
        public string Id;
        public ObstacleKind Kind;
        public Vector3 Position;
        public Vector3 Size;
        public Quaternion Rotation;
        public float Distance;
        public int RouteId;
        public float Severity = 1f;
        public float PreviewDistance = 72f;
        public Vector3 MotionAxis;
        public float MotionAmplitude;
        public float MotionPeriod = 6f;
        public Vector3 PositionAt(float time)
        {
            return Position + MotionAxis * (MotionAmplitude * Mathf.Sin(time * Mathf.PI * 2f / MotionPeriod));
        }
    }
}
