using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace DeliveryDash.Editor
{
    // Diagnostic steering follows the real route; normal controller movement,
    // colliders, traffic, deadlines and service run unchanged. No player teleport.
    public static class TownDrivingRunCheck
    {
        static TownDeliveryGame game;static TownDeliveryWorld world;static CartFeelController cart;
        static List<Vector3> route;static int waypoint,phase;static double began;static bool background;
        static readonly FieldInfo Heading=typeof(CartFeelController).GetField("heading",BindingFlags.NonPublic|BindingFlags.Instance);
        static float travelled;static Vector3 before;static Vector2Int origin;
        [MenuItem("DeliveryDash/Checks/Drive complete pickup delivery route")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Play first");
            game=UnityEngine.Object.FindFirstObjectByType<TownDeliveryGame>();world=game.GetComponent<TownDeliveryWorld>();cart=world.Runner;
            game.Restart(6553);cart.GetComponent<TownRideSelector>().Choose(0);game.Select(new TownAddress(Vector2Int.zero,1));
            route=world.Network.Route(cart.transform.position,world.Position(game.Selected),world.Origin);waypoint=0;phase=0;travelled=0;origin=world.Origin;before=cart.transform.position;
            began=EditorApplication.timeSinceStartup;background=Application.runInBackground;Application.runInBackground=true;EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying){End();return;}
            try
            {
                if(EditorApplication.timeSinceStartup-began>180)throw new InvalidOperationException("Full driving run timed out at "+cart.transform.position+", phase "+phase);
                if(origin!=world.Origin){var shift=TownRoadNetwork.Offset(world.Origin,origin);before-=shift;for(int i=0;i<route.Count;i++)route[i]-=shift;origin=world.Origin;}
                travelled+=Vector3.Distance(before,cart.transform.position);before=cart.transform.position;
                if(phase==0&&game.Book.Orders.Count>0)
                {
                    phase=1;game.Select(game.Book.Orders[0].destination);route=world.Network.Route(cart.transform.position,world.Position(game.Selected),world.Origin);waypoint=0;
                }
                if(phase==1&&game.Book.Delivered>0)
                {
                    string report=$"PASS: full physical pickup-to-delivery run, {travelled:F1} m travelled in {EditorApplication.timeSinceStartup-began:F1} wall seconds; {game.Book.Delivered} delivery; balance ${game.Book.Money}. Diagnostic heading follows road waypoints; normal movement/collisions/traffic/orders/handoffs. No test teleport between pickup and delivery. Not human keyboard playtesting.\n";
                    File.WriteAllText("Captures/full-driving-run.txt",report);Debug.Log(report);End();cart.GetComponent<TownRideSelector>().Open();return;
                }
                if(cart.IsServicing)return;
                while(waypoint<route.Count-1&&Vector3.Distance(cart.transform.position,route[waypoint])<3.4f)waypoint++;
                var d=route[waypoint]-cart.transform.position;d.y=0;
                if(d.sqrMagnitude>.1f)Heading.SetValue(cart,Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg);
            }
            catch(Exception e){File.WriteAllText("Captures/full-driving-run.txt","FAILED: "+e);Debug.LogException(e);End();}
        }
        static void End(){EditorApplication.update-=Tick;Application.runInBackground=background;}
    }
}
