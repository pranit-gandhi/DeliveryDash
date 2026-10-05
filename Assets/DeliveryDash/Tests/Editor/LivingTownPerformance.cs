using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
namespace DeliveryDash.Editor
{
    public static class LivingTownPerformance
    {
        static Camera camera;static GameObject review;static TownDistanceDetail[] details;
        static int frame,phase;static double total;static float oldScale;static bool background;static string baseline;
        [MenuItem("DeliveryDash/Checks/Profile living town rendering")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play mode first");
            var world=UnityEngine.Object.FindFirstObjectByType<TownDeliveryWorld>();
            oldScale=Time.timeScale;Time.timeScale=0;background=Application.runInBackground;Application.runInBackground=true;
            var shop=world.Network.Address(new TownAddress(Vector2Int.zero,1));
            review=new GameObject("Fixed benchmark camera");camera=review.AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=62;camera.farClipPlane=280;
            camera.targetTexture=new RenderTexture(1280,720,24);camera.transform.position=shop.road.Point(Mathf.Max(.05f,shop.t-.12f),world.Origin)+Vector3.up*2.3f;camera.transform.LookAt(shop.road.Point(Mathf.Min(.95f,shop.t+.22f),world.Origin)+Vector3.up*2.1f);
            details=UnityEngine.Object.FindObjectsByType<TownDistanceDetail>(FindObjectsSortMode.None);
            foreach(var d in details){d.enabled=false;foreach(var r in d.GetComponentsInChildren<Renderer>())r.enabled=true;}
            phase=frame=0;total=0;camera.Render();EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying){Cleanup();return;}
            var timer=Stopwatch.StartNew();camera.Render();timer.Stop();
            if(frame++>=5)total+=timer.Elapsed.TotalMilliseconds;if(frame<35)return;
            string sample=$"Mean Camera.Render CPU wall time over 30 renders after 5 warmups: {total/30:F2} ms; editor reported draws {UnityStats.drawCalls}, triangles {UnityStats.triangles}.";
            if(phase++==0)
            {
                baseline=sample;var player=UnityEngine.Object.FindFirstObjectByType<CartFeelController>().transform;
                foreach(var d in details)foreach(var r in d.GetComponentsInChildren<Renderer>())r.enabled=Vector3.Distance(player.position,d.transform.position)<d.visibleDistance;
                frame=0;total=0;return;
            }
            Directory.CreateDirectory("Captures");File.WriteAllText("Captures/living-performance.txt","Unity Editor diagnostic; not standalone FPS or GPU timings. Fixed seed 6553, same 1280x720 camera, same scene/population, simulation paused in both phases.\nCharacter distance culling OFF: "+baseline+"\nCharacter distance culling ON: "+sample+"\nUnityStats can include other editor views/shadow passes and must not be treated as isolated build counters. Baseline isolates character culling, not the whole environment addition.\n");
            UnityEngine.Debug.Log("Living town fixed-camera profile saved.");Cleanup();
        }
        static void Cleanup(){EditorApplication.update-=Tick;Time.timeScale=oldScale;Application.runInBackground=background;foreach(var d in details??Array.Empty<TownDistanceDetail>())if(d!=null)d.enabled=true;if(camera!=null){camera.targetTexture.Release();UnityEngine.Object.DestroyImmediate(camera.targetTexture);}if(review!=null)UnityEngine.Object.DestroyImmediate(review);}
    }
}
