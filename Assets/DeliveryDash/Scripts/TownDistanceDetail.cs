using System.Linq;
using UnityEngine;
namespace DeliveryDash
{
    // Keeps expensive articulated characters near the player; simulation continues independently.
    public sealed class TownDistanceDetail : MonoBehaviour
    {
        public float visibleDistance=85;
        Transform target;Renderer[] renderers;float nextCheck;
        void Start(){target=FindFirstObjectByType<CartFeelController>()?.transform;renderers=GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled).ToArray();}
        void LateUpdate()
        {
            if(target==null||Time.time<nextCheck)return;nextCheck=Time.time+.3f;
            bool show=(target.position-transform.position).sqrMagnitude<visibleDistance*visibleDistance;
            foreach(var renderer in renderers)if(renderer!=null)renderer.enabled=show;
        }
    }
}
