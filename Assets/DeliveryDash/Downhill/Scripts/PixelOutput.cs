using UnityEngine;

namespace DeliveryDash.Downhill
{
    [RequireComponent(typeof(Camera))]
    public sealed class PixelOutput : MonoBehaviour
    {
        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            var small = RenderTexture.GetTemporary(640, 360, 0, source.format);
            small.filterMode = FilterMode.Point;
            Graphics.Blit(source, small);
            Graphics.Blit(small, destination);
            RenderTexture.ReleaseTemporary(small);
        }
    }
}
