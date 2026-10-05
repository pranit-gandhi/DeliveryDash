using UnityEngine;
namespace DeliveryDash
{
    public sealed class TownTrafficBumper : MonoBehaviour
    {
        void OnControllerColliderHit(ControllerColliderHit hit)
        {hit.collider.GetComponentInParent<TownAmbientActor>()?.Bump();}
    }
}
