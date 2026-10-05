using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace DeliveryDash.Editor
{
    public static class NeighborhoodDetailChecks
    {
        static List<GameObject> groups;
        static Camera camera;
        static GameObject review;
        static double began;
        static int frames, phase;
        static bool oldBackground;
        static string baseline;
        [MenuItem("DeliveryDash/Checks/Review neighborhood details")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play mode first");
            var world=UnityEngine.Object.FindFirstObjectByType<TownDeliveryWorld>();
            UnityEngine.Object.FindFirstObjectByType<TownDeliveryGame>().Restart(6553);Physics.SyncTransforms();
            var details=world.GetComponent<TownEnvironmentDetails>();
            groups=world.GetComponentsInChildren<Transform>().Where(t=>t.name=="Neighborhood facade details"||t.name=="Neighborhood street details").Select(t=>t.gameObject).ToList();
            if(groups.Count<4)throw new InvalidOperationException("Missing neighborhood details");
            var detailColliders=groups.SelectMany(g=>g.GetComponentsInChildren<Collider>()).ToArray();
            int bays=0;
            foreach(var key in new[]{Vector2Int.zero,Vector2Int.right})for(int side=-1;side<=1;side+=2)
            {
                var address=new TownAddress(key,side);var lot=world.Network.Address(address);var stop=world.Position(address);
                var front=lot.Building(world.Origin)-lot.Right*(4.1f*lot.scale+.45f);
                foreach(var collider in detailColliders)
                {
                    for(int i=0;i<=12;i++)
                    {
                        Vector3 p=Vector3.Lerp(stop,front,i/12f)+Vector3.up*.7f;
                        if(Vector3.Distance(collider.ClosestPoint(p),p)<.55f)throw new InvalidOperationException("Handoff approach blocked by "+collider.name+" at "+address.Label);
                    }
                    var bay=new Bounds(stop+Vector3.up*.65f,new Vector3(2.3f,1.2f,2.3f));
                    if(collider.bounds.Intersects(bay))throw new InvalidOperationException("Delivery bay blocked by "+collider.name);
                }
                bays++;
            }
            var plaza=world.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Neighborhood pocket plaza"&&t.childCount>0);
            if(plaza==null)throw new InvalidOperationException("No furnished plaza in preview");
            foreach(var collider in detailColliders)
            {if(!Physics.Raycast(collider.bounds.center+Vector3.up*(collider.bounds.extents.y+1),Vector3.down,out _,collider.bounds.size.y+2))throw new InvalidOperationException("Missing solid prop collision");}
            var shop=world.Network.Address(new TownAddress(Vector2Int.zero,1));
            review=new GameObject("Neighborhood review camera");camera=review.AddComponent<Camera>();camera.fieldOfView=65;camera.farClipPlane=230;
            camera.transform.position=shop.road.Point(Mathf.Max(.06f,shop.t-.12f),world.Origin)+Vector3.up*2.1f;
            camera.transform.LookAt(shop.Building(world.Origin)+Vector3.up*2.6f);
            BuildLookDev.Capture(camera,"Captures/neighborhood-driving.png");
            camera.transform.position=plaza.position+new Vector3(11,8,14);camera.transform.LookAt(plaza.position+Vector3.up*1.3f);
            BuildLookDev.Capture(camera,"Captures/neighborhood-plaza.png");
            var center=(shop.Building(world.Origin)+plaza.position+world.Network.Node(Vector2Int.zero,world.Origin))/3;
            camera.transform.position=center+new Vector3(30,85,-42);camera.transform.LookAt(center);camera.fieldOfView=65;
            bool fog=RenderSettings.fog;RenderSettings.fog=false;BuildLookDev.Capture(camera,"Captures/neighborhood-overhead.png");RenderSettings.fog=fog;
            camera.transform.position=shop.road.Point(Mathf.Max(.06f,shop.t-.12f),world.Origin)+Vector3.up*2.1f;camera.transform.LookAt(shop.Building(world.Origin)+Vector3.up*2.6f);
            camera.enabled=false;
            Debug.Log($"PASS neighborhood: {groups.Count} detail groups, {detailColliders.Length} solid props, {bays} clear delivery bays and handoff corridors; furnished plaza; three Unity captures.");
            oldBackground=Application.runInBackground;Application.runInBackground=true;
            phase=0;frames=0;began=EditorApplication.timeSinceStartup;foreach(var g in groups)g.SetActive(false);
            EditorApplication.update-=Measure;EditorApplication.update+=Measure;
        }
        static void Measure()
        {
            if(!EditorApplication.isPlaying||review==null){Cleanup();return;}
            if(++frames<45)return;
            double ms=(EditorApplication.timeSinceStartup-began)*1000/frames;
            // Explicit same-camera render counters; editor update timing is not a player benchmark.
            camera.Render();
            string sample=$"Editor update average: {ms:F2} ms; draw calls {UnityStats.drawCalls}; batches {UnityStats.batches}; triangles {UnityStats.triangles}.";
            if(phase==0){baseline=sample;foreach(var g in groups)g.SetActive(true);phase=1;frames=0;began=EditorApplication.timeSinceStartup;return;}
            File.WriteAllText("Captures/neighborhood-performance.txt","Same camera, 45 editor updates per state. This is an editor diagnostic, not a verified build FPS benchmark.\nDetails disabled: "+baseline+"\nDetails enabled: "+sample+"\nAdded renderer count: "+groups.Sum(g=>g.GetComponentsInChildren<Renderer>().Count(r=>r.enabled))+"\n");
            Debug.Log("Neighborhood performance comparison saved to Captures/neighborhood-performance.txt");Cleanup();
        }
        static void Cleanup(){foreach(var g in groups??new List<GameObject>())if(g!=null)g.SetActive(true);if(review!=null)UnityEngine.Object.DestroyImmediate(review);Application.runInBackground=oldBackground;EditorApplication.update-=Measure;}
    }
}
