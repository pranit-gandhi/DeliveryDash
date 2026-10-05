using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace DeliveryDash
{
    // Explicit opt-in diagnostic, also usable in a standalone player with --town-benchmark.
    public sealed class TownBenchmark : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot(){if(Environment.GetCommandLineArgs().Contains("--town-benchmark"))new GameObject("Standalone town benchmark").AddComponent<TownBenchmark>();}
        IEnumerator Start()
        {
            Application.runInBackground=true;
            for(int i=0;i<20;i++)yield return null;
            var world=FindFirstObjectByType<TownDeliveryWorld>();var game=world.GetComponent<TownDeliveryGame>();game.Restart(6553);
            world.Runner.GetComponent<TownRideSelector>().Choose(0);world.Runner.enabled=false;
            int oldSync=QualitySettings.vSyncCount,oldRate=Application.targetFrameRate;QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
            var camera=new GameObject("1280x720 benchmark camera").AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=62;camera.farClipPlane=280;
            var shop=world.Network.Address(new TownAddress(Vector2Int.zero,1));camera.transform.position=shop.road.Point(Mathf.Max(.05f,shop.t-.12f),world.Origin)+Vector3.up*2.3f;camera.transform.LookAt(shop.road.Point(Mathf.Min(.95f,shop.t+.22f),world.Origin)+Vector3.up*2.1f);
            camera.targetTexture=new RenderTexture(1280,720,24);
            var samples=new List<double>();var frames=new List<float>();
            for(int i=0;i<210;i++)
            {
                yield return null;
                var timer=System.Diagnostics.Stopwatch.StartNew();camera.Render();timer.Stop();
                if(i>=30){samples.Add(timer.Elapsed.TotalMilliseconds);frames.Add(Time.unscaledDeltaTime*1000);}
            }
            frames.Sort();samples.Sort();string platform=Application.isEditor?"editor":"standalone";
            string directory=Application.isEditor?"Captures":Path.Combine(Application.dataPath,"../../../../Captures");
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--report-dir");if(index>=0&&index+1<args.Length)directory=args[index+1];Directory.CreateDirectory(directory);
            string report=$"{platform}, Unity {Application.unityVersion}, {SystemInfo.graphicsDeviceName}\nSeed 6553; fixed 1280x720 offscreen camera, FOV62, far280; live bounded population, stationary player; 30 warmup + 180 measured frames.\nCamera.Render CPU wall time mean {samples.Average():F2} ms, p95 {samples[(int)(samples.Count*.95)]:F2} ms.\nWhole loop interval mean {frames.Average():F2} ms, p95 {frames[(int)(frames.Count*.95)]:F2} ms. Includes the normal game camera and, in Editor, editor overhead. Not isolated GPU timing or a driving FPS benchmark.\n";
            File.WriteAllText(Path.Combine(directory,"town-"+platform+"-benchmark.txt"),report);Debug.Log(report);
            camera.targetTexture.Release();Destroy(camera.targetTexture);Destroy(camera.gameObject);QualitySettings.vSyncCount=oldSync;Application.targetFrameRate=oldRate;
            if(!Application.isEditor)Application.Quit();else {game.Restart(6553);world.Runner.GetComponent<TownRideSelector>().Open();Destroy(gameObject);}
        }
    }
}
