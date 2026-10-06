using UnityEngine;

namespace DeliveryDash.Downhill
{
    public sealed class CourseObstacleMotion : MonoBehaviour
    {
        public CourseObstacle Obstacle;
        private DownhillCart cart;
        void Start() { cart = FindFirstObjectByType<DownhillCart>(); }
        void Update()
        {
            if (Obstacle != null) transform.position = Obstacle.PositionAt(cart != null ? cart.SimulationTime : 0);
        }
    }
}
