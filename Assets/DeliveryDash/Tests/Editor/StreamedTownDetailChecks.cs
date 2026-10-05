using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
namespace DeliveryDash.Editor
{
    // Exercise the streamed town beyond the old origin-only preview, in both directions.
    public static class StreamedTownDetailChecks
    {
        static readonly Vector2Int[] Stops={Vector2Int.zero,new Vector2Int(7,6),new Vector2Int(-8,-7),new Vector2Int(7,6)};
        static TownDeliveryWorld world;static TownLivingTown living;static TownDeliveryGame game;
        static int stage;static double arrived;static bool background;static StringBuilder report;
        [MenuItem("DeliveryDash/Checks/Verify detailed town streaming")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play mode first");
            world=UnityEngine.Object.FindFirstObjectByType<TownDeliveryWorld>();living=world.GetComponent<TownLivingTown>();game=world.GetComponent<TownDeliveryGame>();
            background=Application.runInBackground;Application.runInBackground=true;game.Restart(6553);world.Runner.GetComponent<TownRideSelector>().Open();
            report=new StringBuilder("Seed 6553, Unity Editor Play-mode streaming check. Repositioning is diagnostic; build times are not player FPS.\n");stage=0;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;Visit();
        }
        static void Visit()
        {
            var watch=System.Diagnostics.Stopwatch.StartNew();
            world.Runner.ResetAt(world.Network.Node(Stops[stage],world.Origin)+Vector3.up*.12f,Quaternion.identity);
            world.RefreshBlocks();Physics.SyncTransforms();watch.Stop();
            if(world.CurrentBlock!=Stops[stage])throw new InvalidOperationException("Wrong streamed center");
            report.AppendLine($"Visit {Stops[stage]}: RefreshBlocks {watch.Elapsed.TotalMilliseconds:F0} ms; origin {world.Origin}.");arrived=EditorApplication.timeSinceStartup;
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying){Cleanup();return;}
            if(EditorApplication.timeSinceStartup-arrived<3)return;
            try
            {
                var roots=world.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Road district ")&&t.gameObject.activeInHierarchy).ToArray();
                if(roots.Length!=25||living.RegisteredDistricts!=25)throw new InvalidOperationException("District scenery/cache does not stay bounded at 25");
                int houses=0,parks=0,facades=0,bays=0;
                foreach(var root in roots)
                {
                    var scenery=root.Find("Living neighborhood static scenery");if(scenery==null)throw new InvalidOperationException("Distant district missing rich scenery: "+root.name);
                    houses+=scenery.GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("House ")||t.name.StartsWith("Connected storefront"));
                    parks+=scenery.GetComponentsInChildren<Transform>().Count(t=>(t.name=="Public garden and walking loop"||t.name=="Landscaped neighborhood square"));
                    if(!root.GetComponentsInChildren<Transform>().Any(t=>t.name=="Neighborhood street details"))throw new InvalidOperationException("Missing street detail layer");
                    facades+=root.GetComponentsInChildren<Transform>().Count(t=>t.name=="Neighborhood facade details");
                }
                if(houses<50||parks<5||facades!=50)throw new InvalidOperationException($"Sparse distant town: houses {houses}, parks {parks}, facades {facades}");
                var solids=roots.SelectMany(r=>r.Find("Living neighborhood static scenery").GetComponentsInChildren<Collider>()).Where(c=>c.bounds.max.y>.5f).ToArray();
                for(int x=-2;x<=2;x++)for(int z=-2;z<=2;z++)for(int side=-1;side<=1;side+=2)
                {
                    var address=new TownAddress(world.CurrentBlock+new Vector2Int(x,z),side);var bay=new Bounds(world.Position(address)+Vector3.up*.65f,new Vector3(2.3f,1.2f,2.3f));
                    if(solids.Any(c=>c.bounds.Intersects(bay)))throw new InvalidOperationException("Scenery blocks delivery bay "+address.Label);bays++;
                }
                var actors=UnityEngine.Object.FindObjectsByType<TownAmbientActor>(FindObjectsSortMode.None);
                if(living.ActivePeople<12||living.ActiveTraffic<4)throw new InvalidOperationException("Activity did not follow player");
                if(UnityEngine.Object.FindObjectsByType<TownAmbientActor>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length>36)throw new InvalidOperationException("Crowd pool grows during streaming");
                if(actors.Any(a=>Vector3.Distance(a.transform.position,world.Runner.transform.position)>250))throw new InvalidOperationException("Actor stranded in old district");
                report.AppendLine($"PASS: {houses} infill buildings; {parks} parks; {facades} detailed facades; {bays} clear delivery bays; {living.ActivePeople} pedestrians and {living.ActiveTraffic} traffic/cyclists; 25 loaded/cached districts; pool <=36.");
                if(stage==1)
                {
                    var go=new GameObject("Distant PCG review camera");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=65;camera.farClipPlane=300;
                    var lot=world.Network.Address(new TownAddress(Stops[stage],1));
                    camera.transform.position=lot.road.Point(Mathf.Max(.06f,lot.t-.12f),world.Origin)+Vector3.up*2.2f;camera.transform.LookAt(lot.Building(world.Origin)+Vector3.up*2.5f);BuildLookDev.Capture(camera,"Captures/streamed-town-distant-street.png");
                    camera.transform.position=world.Network.Node(Stops[stage],world.Origin)+new Vector3(30,105,-50);camera.transform.LookAt(world.Network.Node(Stops[stage],world.Origin));bool fog=RenderSettings.fog;RenderSettings.fog=false;BuildLookDev.Capture(camera,"Captures/streamed-town-distant-overhead.png");RenderSettings.fog=fog;UnityEngine.Object.DestroyImmediate(go);
                }
                if(++stage<Stops.Length){Visit();return;}
                Directory.CreateDirectory("Captures");File.WriteAllText("Captures/streamed-town-checks.txt",report.ToString());Debug.Log("PASS detailed town streaming: origin, distant positive/negative districts and revisit; scenery, activity, bay clearances, bounded caches and pool.");
                game.Restart(6553);world.Runner.GetComponent<TownRideSelector>().Open();Cleanup();
            }
            catch(Exception e){Debug.LogException(e);Directory.CreateDirectory("Captures");File.WriteAllText("Captures/streamed-town-checks.txt",report+"\nFAILED: "+e);Cleanup();}
        }
        static void Cleanup(){EditorApplication.update-=Tick;Application.runInBackground=background;}
    }
}
