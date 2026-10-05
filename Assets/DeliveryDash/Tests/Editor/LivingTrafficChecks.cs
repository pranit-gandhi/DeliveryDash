using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace DeliveryDash.Editor
{
    public static class LivingTrafficChecks
    {
        static TownAmbientActor actor;static TownDeliveryWorld world;static GameObject obstruction;static double began;static int recovered;static bool background;
        [MenuItem("DeliveryDash/Checks/Exercise traffic collision and recovery")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Play first");
            world=UnityEngine.Object.FindFirstObjectByType<TownDeliveryWorld>();
            actor=UnityEngine.Object.FindObjectsByType<TownAmbientActor>(FindObjectsSortMode.None).Where(a=>a.kind==1).OrderBy(a=>Vector3.Distance(a.transform.position,world.Runner.transform.position)).First();
            var runner=world.Runner;Vector3 original=runner.transform.position;Quaternion rotation=runner.transform.rotation;
            runner.enabled=false;runner.ResetAt(actor.transform.position-actor.transform.forward*2.7f+Vector3.up*.08f,actor.transform.rotation);Physics.SyncTransforms();runner.GetComponent<CharacterController>().Move(actor.transform.forward*2);
            bool bumped=actor.IsReacting;runner.ResetAt(original,rotation);
            if(!bumped)throw new InvalidOperationException("Controller impact did not trigger comic reaction");
            obstruction=GameObject.CreatePrimitive(PrimitiveType.Cube);obstruction.name="Temporary traffic recovery probe";obstruction.transform.position=actor.transform.position+actor.transform.forward*3+Vector3.up;obstruction.transform.rotation=actor.transform.rotation;obstruction.transform.localScale=new Vector3(6,2,1);Physics.SyncTransforms();
            recovered=actor.Recoveries;began=EditorApplication.timeSinceStartup;background=Application.runInBackground;Application.runInBackground=true;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying){Cleanup();return;}
            if(actor.Recoveries>recovered)
            {
                Directory.CreateDirectory("Captures");File.WriteAllText("Captures/living-traffic-checks.txt","PASS: actual CharacterController collision triggers comic vehicle reaction. Deliberately blocked vehicle recovers through the bounded pool instead of remaining jammed.\n");Debug.Log("PASS traffic: real controller collision reaction and timed jam recovery.");Cleanup();return;
            }
            if(EditorApplication.timeSinceStartup-began>30){Debug.LogError("Traffic recovery probe timed out");Cleanup();}
        }
        static void Cleanup(){EditorApplication.update-=Tick;if(obstruction!=null)UnityEngine.Object.DestroyImmediate(obstruction);Application.runInBackground=background;if(world!=null){world.Runner.enabled=true;world.Runner.GetComponent<TownRideSelector>().Open();}}
    }
}
