using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace DeliveryDash.Editor
{
    public static class LivingTownChecks
    {
        static TownAmbientActor filmActor;static int rideCapture=-1,rideWait;
        static TownDeliveryWorld world;static TownLivingTown living;static TownDeliveryGame game;
        static double began,nextCapture;static int frames;static float distance;static bool oldBackground;static Camera camera;static GameObject review;
        [MenuItem("DeliveryDash/Checks/Review living town")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play mode first");
            world=UnityEngine.Object.FindFirstObjectByType<TownDeliveryWorld>();living=world.GetComponent<TownLivingTown>();game=world.GetComponent<TownDeliveryGame>();game.Restart(6553);
            Physics.SyncTransforms();
            var scenery=world.GetComponentsInChildren<Transform>().Where(t=>t.name=="Living neighborhood static scenery").SelectMany(t=>t.GetComponentsInChildren<Collider>()).Where(c=>c.bounds.max.y>.5f).ToArray();
            int bays=0;for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)for(int side=-1;side<=1;side+=2)
            {var stop=world.Position(new TownAddress(new Vector2Int(x,z),side));var bay=new Bounds(stop+Vector3.up*.65f,new Vector3(2.3f,1.2f,2.3f));if(scenery.Any(c=>c.bounds.Intersects(bay)))throw new InvalidOperationException("New scenery blocked bay "+x+","+z);bays++;}
            Debug.Log("PASS living scenery: "+bays+" clear delivery bays");
            var rides=world.Runner.GetComponent<TownRideSelector>();
            var drive=typeof(CartFeelController).GetMethod("DriveTown",BindingFlags.NonPublic|BindingFlags.Instance);
            var original=world.Runner.transform.position;var rotation=world.Runner.transform.rotation;
            for(int i=0;i<3;i++)
            {
                rides.Choose(i);world.Runner.ResetAt(new Vector3(0,1000,0),Quaternion.identity);
                for(int f=0;f<150;f++)drive.Invoke(world.Runner,new object[]{.02f,true});
                if(world.Runner.Speed<world.Runner.CruiseSpeed+1)throw new InvalidOperationException("Ride did not accelerate "+i);
                float fast=world.Runner.Speed;
                for(int f=0;f<100;f++)drive.Invoke(world.Runner,new object[]{.02f,false});
                if(world.Runner.Speed>=fast||Mathf.Abs(world.Runner.Speed-world.Runner.CruiseSpeed)>.1f)throw new InvalidOperationException($"Ride {i} release: fast={fast}, now={world.Runner.Speed}, cruise={world.Runner.CruiseSpeed}, steer={world.Runner.Steering}, position={world.Runner.transform.position}");
                world.Runner.BeginService(world.Runner.transform.position,2);
                drive.Invoke(world.Runner,new object[]{.02f,true});if(!world.Runner.IsHandingOff||world.Runner.Speed>.001f)throw new InvalidOperationException("Throttle interrupted handoff");
                world.Runner.CancelService();
            }
            rides.Choose(0);world.Runner.ResetAt(original,rotation);world.Runner.enabled=false;
            var renderers=world.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();
            File.WriteAllText("Captures/living-renderer-audit.txt",string.Join("\n",renderers.OrderByDescending(r=>r.sharedMaterials.Length).Take(20).Select(r=>$"{r.name}: {r.sharedMaterials.Length} materials"))+"\nEnabled renderers: "+renderers.Length+"\nTotal material slots: "+renderers.Sum(r=>r.sharedMaterials.Length));
            oldBackground=Application.runInBackground;Application.runInBackground=true;
            rideCapture=-1;filmActor=null;began=EditorApplication.timeSinceStartup;nextCapture=began;frames=0;distance=0;
            review=new GameObject("Living town review camera");camera=review.AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=62;camera.farClipPlane=280;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying){Cleanup();return;}
            try
            {
                if(rideCapture>=0)
                {
                    if(rideWait-->0)return;
                    BuildLookDev.Capture(camera,$"Captures/living-ride-{rideCapture}.png");
                    if(++rideCapture<3){world.Runner.GetComponent<TownRideSelector>().Choose(rideCapture);world.Runner.enabled=false;rideWait=2;return;}
                    Debug.Log("PASS living town: all three rides accelerate/release/dock; pedestrians, cars and cyclists move; bounded pool and traffic recovery. Captures saved.");Cleanup();game.Restart(6553);world.Runner.GetComponent<TownRideSelector>().Open();return;
                }
                double now=EditorApplication.timeSinceStartup;
                var shop=world.Network.Address(new TownAddress(Vector2Int.zero,1));
                camera.transform.position=shop.road.Point(Mathf.Max(.05f,shop.t-.12f),world.Origin)+Vector3.up*2.3f;
                camera.transform.LookAt(shop.road.Point(Mathf.Min(.95f,shop.t+.22f),world.Origin)+Vector3.up*2.1f);
                if(filmActor==null||!filmActor.gameObject.activeSelf)filmActor=UnityEngine.Object.FindObjectsByType<TownAmbientActor>(FindObjectsSortMode.None).FirstOrDefault(a=>a.kind==2);
                if(now>=nextCapture&&frames<24&&filmActor!=null){var p=camera.transform.position;var q=camera.transform.rotation;camera.transform.position=filmActor.transform.position-filmActor.transform.forward*5+filmActor.transform.right*4+Vector3.up*2.5f;camera.transform.LookAt(filmActor.transform.position+Vector3.up*.8f);var visible=filmActor.GetComponentsInChildren<Renderer>();var states=visible.Select(r=>r.enabled).ToArray();foreach(var r in visible)r.enabled=true;BuildLookDev.Capture(camera,$"Captures/living-frame-{frames:000}.png");for(int v=0;v<visible.Length;v++)visible[v].enabled=states[v];camera.transform.SetPositionAndRotation(p,q);frames++;nextCapture=now+.3;}
                if(now-began<10)return;
                var actors=UnityEngine.Object.FindObjectsByType<TownAmbientActor>(FindObjectsSortMode.None);
                if(living.ActivePeople<12||living.ActiveTraffic<4)throw new InvalidOperationException("Population missing");
                if(!actors.Any(a=>a.kind==0&&a.Travelled>1)||!actors.Any(a=>a.kind==1&&a.Travelled>3)||!actors.Any(a=>a.kind==2&&a.Travelled>2))throw new InvalidOperationException("A population type failed to move");
                distance=actors.Sum(a=>a.Travelled);
                BuildLookDev.Capture(camera,"Captures/living-town-driving.png");
                camera.transform.position=shop.road.Point(shop.t,world.Origin)+new Vector3(30,95,-45);camera.transform.LookAt(shop.road.Point(shop.t,world.Origin));
                bool fog=RenderSettings.fog;RenderSettings.fog=false;BuildLookDev.Capture(camera,"Captures/living-town-overhead.png");
                var park=world.GetComponentsInChildren<Transform>().First(t=>t.name=="Public garden and walking loop");camera.transform.position=park.position+new Vector3(10,6,11);camera.transform.LookAt(park.position+Vector3.up);BuildLookDev.Capture(camera,"Captures/living-town-park.png");RenderSettings.fog=fog;
                var rides=world.Runner.GetComponent<TownRideSelector>();world.Runner.enabled=false;
                rides.Choose(0);world.Runner.enabled=false;camera.transform.position=world.Runner.transform.position+new Vector3(3,2,3);camera.transform.LookAt(world.Runner.transform.position+Vector3.up*.8f);rideCapture=0;rideWait=2;
                int count=actors.Length;living.Recover(actors.First(a=>a.kind==1));
                // Pool stays bounded; no new object is required after forced traffic recovery.
                if(UnityEngine.Object.FindObjectsByType<TownAmbientActor>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length>36)throw new InvalidOperationException("Population pool unbounded");
                File.WriteAllText("Captures/living-town-checks.txt",$"PASS: three ride acceleration/deceleration profiles; held throttle cannot interrupt handoff.\nActive pedestrians {living.ActivePeople}; traffic/cyclists {living.ActiveTraffic}; total travel {distance:F1} m; pool {count}/36; forced traffic recovery {living.RecoveryCount}.\nMotion clip is a time-sampled Unity render sequence, not a real-time FPS demonstration.\n");
                
            }
            catch(Exception e){Debug.LogException(e);Cleanup();}
        }
        static void Cleanup(){EditorApplication.update-=Tick;if(review!=null)UnityEngine.Object.DestroyImmediate(review);Application.runInBackground=oldBackground;if(world!=null)world.Runner.enabled=true;}
    }
}
