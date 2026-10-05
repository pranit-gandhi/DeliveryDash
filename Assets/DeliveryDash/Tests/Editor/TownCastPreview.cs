using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DeliveryDash.Editor
{
    public static class TownCastPreview
    {
        [MenuItem("DeliveryDash/Checks/Capture customer cast")]
        public static void Capture()
        {
            if(!EditorApplication.isPlaying) throw new InvalidOperationException("Enter town Play mode first");
            var world=UnityEngine.Object.FindFirstObjectByType<TownDeliveryWorld>();
            var root=new GameObject("Temporary cast preview"); root.transform.position=new Vector3(0,100,0);
            bool fog=RenderSettings.fog;
            try
            {
                RenderSettings.fog=false;
                var people=world.GetComponentsInChildren<TownShopOwner>();
                for(int i=0;i<5;i++)
                {
                    string name=TownShopOwner.CustomerNames[i];
                    var source=people.First(p=>p.name.StartsWith("Customer "+name));
                    var actor=UnityEngine.Object.Instantiate(source.transform.GetChild(0),root.transform);
                    actor.localPosition=new Vector3((i-2)*1.35f,0,0); actor.localRotation=Quaternion.Euler(0,180,0);
                    var label=new GameObject(name); label.transform.SetParent(root.transform,false);
                    label.transform.localPosition=new Vector3((i-2)*1.35f,-.3f,0);
                    var text=label.AddComponent<TextMesh>(); text.text=name.Replace(" ","\n"); text.characterSize=.033f; text.fontSize=48;
                    text.anchor=TextAnchor.MiddleCenter; text.alignment=TextAlignment.Center;
                    text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.GetComponent<MeshRenderer>().sharedMaterial=text.font.material;
                }
                var lightGo=new GameObject("Portrait fill"); lightGo.transform.SetParent(root.transform,false); lightGo.transform.localPosition=new Vector3(0,3,-5); lightGo.transform.localRotation=Quaternion.Euler(15,0,0); var fill=lightGo.AddComponent<Light>(); fill.type=LightType.Directional; fill.intensity=.45f;
                var camGo=new GameObject("Cast preview camera"); camGo.transform.SetParent(root.transform,false);
                camGo.transform.localPosition=new Vector3(0,1.1f,-14); camGo.transform.LookAt(root.transform.position+Vector3.up*.95f);
                var camera=camGo.AddComponent<Camera>(); camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.15f,.19f,.22f); camera.orthographic=true; camera.orthographicSize=2.05f; camera.farClipPlane=25;
                BuildLookDev.Capture(camera,"Captures/customer-cast.png");
                for(int i=0;i<5;i++)
                {
                    camera.orthographicSize=.23f;
                    camGo.transform.localPosition=new Vector3((i-2)*1.35f,1.69f,-2);
                    camGo.transform.localRotation=Quaternion.identity;
                    BuildLookDev.Capture(camera,"Captures/customer-"+TownShopOwner.CustomerNames[i].ToLowerInvariant().Replace(' ','-')+"-unity.png");
                }
                Debug.Log("PASS: all five customer variants present; captured Captures/customer-cast.png");
            }
            finally { RenderSettings.fog=fog; UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
