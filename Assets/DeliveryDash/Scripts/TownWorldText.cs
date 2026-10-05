using UnityEngine;
namespace DeliveryDash
{
    public sealed class TownWorldText : MonoBehaviour
    {
        public Material sharedFontMaterial;
        Font font;
        void OnEnable(){var text=GetComponent<TextMesh>();font=text.font;if(font==null)font=text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");GetComponent<Renderer>().sharedMaterial=sharedFontMaterial;UpdateAtlas(font);Font.textureRebuilt+=UpdateAtlas;}
        void Start(){OnDisable();OnEnable();}
        void UpdateAtlas(Font changed){if(changed==font&&sharedFontMaterial!=null)sharedFontMaterial.mainTexture=font.material.mainTexture;}
        void OnDisable(){Font.textureRebuilt-=UpdateAtlas;}
    }
}
