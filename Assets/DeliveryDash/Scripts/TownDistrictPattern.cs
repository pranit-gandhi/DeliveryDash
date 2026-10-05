using UnityEngine;
namespace DeliveryDash
{
    public static class TownDistrictPattern
    {
        // Smooth land use spans multiple streamed cells; no checkerboard districts.
        public static float Commercial(int seed,Vector2Int key)
        {
            float value=Mathf.PerlinNoise(key.x*.19f+(seed&1023)*.13f,key.y*.19f+((seed>>10)&1023)*.17f);
            return Mathf.Clamp01((value-.25f)*1.8f);
        }
    }
}
