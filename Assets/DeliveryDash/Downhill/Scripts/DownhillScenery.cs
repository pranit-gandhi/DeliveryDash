using System.Collections.Generic;
using UnityEngine;

namespace DeliveryDash.Downhill
{
    public enum ScenerySeason { Summer, Spring, Sunset, Winter }
    public static class DownhillScenery
    {
        static Material skyMaterial;
        public static ScenerySeason Choose(int seed)
        {
            uint hash=unchecked((uint)seed*747796405u+2891336453u);
            hash=((hash>>(int)((hash>>28)+4))^hash)*277803737u;
            uint pick=((hash>>22)^hash)%100;
            return pick<38?ScenerySeason.Summer:pick<70?ScenerySeason.Spring:pick<95?ScenerySeason.Sunset:ScenerySeason.Winter;
        }
        static Color Hex(string s){ColorUtility.TryParseHtmlString(s,out Color c);return c;}
        public static void Apply(CourseGraph course)
        {
            var season=Choose(course.Seed);
            bool sunset=season==ScenerySeason.Sunset,winter=season==ScenerySeason.Winter,spring=season==ScenerySeason.Spring;
            Color sky=Hex(sunset?"#D9A18C":winter?"#B4C5D0":spring?"#9EC4CF":"#81B8D2");
            Color ground=Hex(winter?"#DDDCD1":spring?"#96AC70":sunset?"#A9A071":"#AAA56B");
            Color leaves=Hex(winter?"#CCD7CC":spring?"#9EA879":sunset?"#8B9363":"#789256");
            if(skyMaterial==null)skyMaterial=new Material(Shader.Find("DeliveryDash/Season sky")){name="Seasonal sky"};
            skyMaterial.SetColor("_Top",Hex(sunset?"#916E91":winter?"#8EA8C1":"#5394C0"));
            skyMaterial.SetColor("_Horizon",sky);
            skyMaterial.SetColor("_SunColor",Hex(sunset?"#FFD496":"#FFF0CB"));
            RenderSettings.skybox=skyMaterial;
            if(Camera.main!=null){Camera.main.backgroundColor=sky;Camera.main.farClipPlane=900;Camera.main.clearFlags=CameraClearFlags.Skybox;}
            RenderSettings.fogColor=sky;RenderSettings.fogStartDistance=170;RenderSettings.fogEndDistance=800;
            RenderSettings.ambientSkyColor=Hex(sunset?"#796B79":winter?"#728393":"#708C92");
            RenderSettings.ambientEquatorColor=Hex(sunset?"#665D52":"#566455");
            RenderSettings.ambientGroundColor=Hex("#343D37");
            foreach(var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))if(light.type==LightType.Directional)
            {
                light.color=Hex(sunset?"#FFC48A":winter?"#E8F3FF":"#FFF0C7");
                light.intensity=winter?.68f:sunset?.92f:.82f;
                light.transform.rotation=Quaternion.Euler(sunset?18:49,(course.Seed%37)-40,0);
                Vector3 ahead=course.Sample(0).Forward;
                ahead.y=0;
                skyMaterial.SetVector("_SunDirection",(ahead.normalized+Vector3.left*.38f+Vector3.up*(sunset?.18f:.38f)).normalized);
            }
            var seen=new HashSet<Material>();
            foreach(string name in new[]{"Downhill course","Terraced market street","Town backdrop"})
            {
                var world=GameObject.Find(name);if(world==null)continue;
                foreach(var renderer in world.GetComponentsInChildren<Renderer>())foreach(var material in renderer.sharedMaterials)
                {
                    if(material==null||!seen.Add(material))continue;
                    string label=material.name.ToLowerInvariant();
                    if(label.Contains("valley ground")||label.Contains("terraced earth")||label.Contains("distant hill")||label.Contains("sage ridge"))
                    {material.mainTexture=null;material.color=ground;}
                    else if(label.Contains("leaves"))material.color=leaves;
                    else if(label.Contains("roof"))material.color=Hex(winter?"#D5D6C8":sunset?"#B17658":"#A26449");
                    else if(label.Contains("rose limewash"))material.color=Hex(spring?"#DDB1A0":"#BC9780");
                }
            }
        }
    }
}
