using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace DeliveryDash
{
    [Serializable] public class TownRideProfile
    {
        public string name;public float cruise,maximum,acceleration,turnRate;public GameObject prefab;
        public TownRideProfile(string n,float c,float m,float a,float turn,GameObject p){name=n;cruise=c;maximum=m;acceleration=a;turnRate=turn;prefab=p;}
    }
    public sealed class TownRideSelector : MonoBehaviour
    {
        public TownRideProfile[] rides;
        public int Selected {get;private set;}
        public bool Choosing {get;private set;}
        readonly Dictionary<Transform,Quaternion> riderRest=new Dictionary<Transform,Quaternion>();
        Transform rider;
        CartFeelController drive;Transform original,visual;GameObject activeRide;Transform[] wheels=Array.Empty<Transform>();
        public string RideName=>rides!=null&&rides.Length>Selected?rides[Selected].name:"Shopping cart";
        void Awake(){drive=GetComponent<CartFeelController>();visual=transform.Find("Cart visual response root");original=visual.GetComponentsInChildren<Transform>(true).First(t=>t.name.StartsWith("Shopping cart |"));rider=visual.Find("Courier seated in basket");foreach(var bone in rider.GetComponentsInChildren<Transform>())riderRest[bone]=bone.localRotation;}
        public void Open(){Choosing=true;drive.CancelService();drive.enabled=false;}
        public void Choose(int index)
        {
            if(rides==null||index<0||index>=rides.Length)return;
            Selected=index;var r=rides[index];drive.ConfigureRide(r.cruise,r.maximum,r.acceleration,r.turnRate);
            if(activeRide!=null){activeRide.SetActive(false);Destroy(activeRide);}original.gameObject.SetActive(index==0);
            wheels=Array.Empty<Transform>();
            if(index>0){activeRide=Instantiate(r.prefab,visual);activeRide.name=r.name;wheels=activeRide.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("SpinWheel")).ToArray();}
            PoseRider(index);FindFirstObjectByType<TownDeliveryGame>()?.RefreshRideCargo();Choosing=false;drive.enabled=true;
        }
        void PoseRider(int index)
        {
            foreach(var pair in riderRest)pair.Key.localRotation=pair.Value;
            if(index==0)return;
            foreach(int side in new[]{-1,1})
            {
                string suffix=side<0?"_l":"_r";
                var bones=rider.GetComponentsInChildren<Transform>();
                var shoulder=bones.First(t=>t.name=="upperarm"+suffix);var elbow=bones.First(t=>t.name=="lowerarm"+suffix);var hand=bones.First(t=>t.name=="hand"+suffix);
                var target=rider.TransformPoint(new Vector3(side*.27f,index==2?1.12f:.94f,.44f));
                float a=Vector3.Distance(shoulder.position,elbow.position),b=Vector3.Distance(elbow.position,hand.position);var delta=target-shoulder.position;float distance=Mathf.Clamp(delta.magnitude,.01f,a+b-.001f);var direction=delta.normalized;
                float along=(a*a-b*b+distance*distance)/(2*distance);float height=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
                var bend=Vector3.ProjectOnPlane(rider.TransformPoint(new Vector3(side*.43f,1,.1f))-shoulder.position,direction).normalized;var joint=shoulder.position+direction*along+bend*height;
                shoulder.rotation=Quaternion.FromToRotation(elbow.position-shoulder.position,joint-shoulder.position)*shoulder.rotation;
                elbow.rotation=Quaternion.FromToRotation(hand.position-elbow.position,target-elbow.position)*elbow.rotation;
            }
        }
        void Update(){if(Choosing){if(Input.GetKeyDown(KeyCode.Alpha1))Choose(0);if(Input.GetKeyDown(KeyCode.Alpha2))Choose(1);if(Input.GetKeyDown(KeyCode.Alpha3))Choose(2);}}
        void LateUpdate(){if(!drive.enabled)return;foreach(var wheel in wheels)wheel.Rotate(Vector3.right,drive.Speed*Time.deltaTime*Mathf.Rad2Deg/.3f,Space.Self);}
        void OnGUI()
        {
            if(!Choosing)return;float scale=Mathf.Min(Screen.width/1100f,Screen.height/680f);var old=GUI.matrix;GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
            var r=new Rect(Screen.width/scale/2-280,Screen.height/scale/2-145,560,290);GUI.Box(r,"CHOOSE YOUR DELIVERY RIDE");
            GUI.Label(new Rect(r.x+25,r.y+35,510,35),"Deliver as many pizzas as you can in one shift.");
            for(int i=0;i<rides.Length;i++){var ride=rides[i];if(GUI.Button(new Rect(r.x+25,r.y+80+i*52,510,44),$"{i+1}. {ride.name}     Cruise {ride.cruise:0} / Fast {ride.maximum:0} m/s"))Choose(i);}
            GUI.Label(new Rect(r.x+25,r.y+247,510,30),"A/D or ←/→ steer     Hold W / ↑ to accelerate");GUI.matrix=old;
        }
    }
}
