using System.Linq;
using UnityEngine;
namespace DeliveryDash
{
    public sealed class TownAmbientActor : MonoBehaviour
    {
        public int kind,index;
        TownLivingTown town;TownDeliveryWorld world;TownRoadNetwork.Road road;
        float t,pace,waited,travel;int direction=1;Vector2Int origin;bool park,seated,crossing;float crossingOffset;int crossingDirection=-1;float crossingWait;
        Vector3 parkCenter;bool gathering;float sidewalkOffset;
        Transform[] wheels;TownCitizenMotion citizen;Transform model;Quaternion modelRotation;float bumpUntil;TextMesh reaction;
        public float Travelled=>travel;
        public int Recoveries {get;set;}
        public bool IsReacting=>Time.time<bumpUntil;
        public void Initialize(TownLivingTown manager,TownDeliveryWorld map,int type,int id,GameObject visual)
        {
            town=manager;world=map;model=visual.transform;modelRotation=model.localRotation;kind=type;index=id;pace=kind==0?1.05f+(id%4)*.28f:kind==1?4.5f+(id%3):3.5f;
            wheels=visual.GetComponentsInChildren<Transform>().Where(x=>x.name.StartsWith("SpinWheel")).ToArray();citizen=visual.GetComponentInChildren<TownCitizenMotion>();
            if(kind==0){var capsule=gameObject.AddComponent<CapsuleCollider>();capsule.center=Vector3.up*.85f;capsule.height=1.7f;capsule.radius=.24f;}
            if(kind==1){var box=gameObject.AddComponent<BoxCollider>();box.center=new Vector3(0,.65f,0);box.size=new Vector3(1.35f,1.3f,2.6f);}
            else if(kind==2){var box=gameObject.AddComponent<BoxCollider>();box.center=new Vector3(0,.7f,0);box.size=new Vector3(.65f,1.4f,1.7f);}
        }
        public void BeginRoad(TownRoadNetwork.Road value,float start){road=value;pace=kind==0?1.05f+(index%4)*.28f:kind==1?4.5f+(index%3):3.5f;t=start;direction=index%2==0?1:-1;park=false;gathering=false;sidewalkOffset=0;crossing=kind==0&&index%6==0;if(crossing){t=(world.Network.JunctionRadius(road.a)+4)/road.Length;crossingOffset=road.Width(t)+1;crossingDirection=-1;crossingWait=0;}waited=0;origin=world.Origin;transform.position=Point(t);transform.rotation=Quaternion.LookRotation(road.Direction(t)*direction);gameObject.SetActive(true);}
        public void BeginPark(Vector3 center,bool sit){park=true;gathering=false;seated=sit;pace=sit?0:index%2==0?2.6f:1.35f;parkCenter=center;origin=world.Origin;waited=0;t=index;transform.position=center+new Vector3(sit?4:0,sit?-.42f:0,sit?2.3f:0);transform.rotation=Quaternion.Euler(0,180,0);gameObject.SetActive(true);}
        public void BeginGather(Vector3 position,Vector3 facing){BeginPark(position,false);gathering=true;transform.position=position;transform.rotation=Quaternion.LookRotation(facing);}
        Vector3 Point(float at)
        {
            var forward=road.Direction(at)*direction;var right=Vector3.Cross(Vector3.up,kind==0?road.Direction(at)*(index%2==0?1:-1):forward);
            float offset=kind==0?road.Width(at)+1+sidewalkOffset:kind==2?road.Width(at)-.8f:road.Width(at)*.43f;
            if(kind!=0)offset*=Mathf.Clamp01(Mathf.Min(at,1-at)*road.Length/12);
            return road.Point(at,world.Origin)+right*offset+Vector3.up*.06f;
        }
        public void Bump()
        {
            if(Time.time<bumpUntil)return;bumpUntil=Time.time+1.5f;
            if(reaction==null){var label=new GameObject("Comic traffic reaction");label.transform.SetParent(transform,false);label.transform.localPosition=Vector3.up*2.3f;reaction=label.AddComponent<TextMesh>();reaction.anchor=TextAnchor.MiddleCenter;reaction.characterSize=.12f;reaction.fontSize=40;reaction.color=new Color(1,.86f,.22f);}
            reaction.text=kind==1?"PIZZA PRIORITY!":kind==2?"SAVE THE CRUST!":"MAMMA MIA!";reaction.gameObject.SetActive(true);
        }
        void Update()
        {
            if(world.Network==null)return;
            if(origin!=world.Origin){var shift=TownRoadNetwork.Offset(world.Origin,origin);transform.position-=shift;parkCenter-=shift;origin=world.Origin;}
            if(Vector3.Distance(transform.position,world.Runner.transform.position)>230){town.Recover(this);return;}
            if(reaction!=null){bool reacting=Time.time<bumpUntil;reaction.gameObject.SetActive(reacting);if(Camera.main!=null)reaction.transform.rotation=Camera.main.transform.rotation;model.localRotation=modelRotation*Quaternion.Euler(0,0,reacting?Mathf.Sin(Time.time*24)*7:0);if(reacting){citizen?.Pose(0,kind==0?0:1);return;}}
            float dt=Mathf.Min(Time.deltaTime,.05f);float move=pace*dt;bool stopped=false;
            if(crossing&&!park)
            {
                var r=Vector3.Cross(Vector3.up,road.Direction(t));var center=road.Point(t,world.Origin);
                if(crossingWait>0){crossingWait-=dt;citizen?.Pose(0,0);return;}
                if(town.TrafficAhead(this,transform.position,road.Direction(t),12)||town.TrafficAhead(this,transform.position,-road.Direction(t),12)){citizen?.Pose(0,0);return;}
                crossingOffset+=move*crossingDirection;transform.position=center+r*crossingOffset+Vector3.up*.06f;transform.rotation=Quaternion.LookRotation(r*crossingDirection);travel+=move;citizen?.Pose(pace,0);
                if(Mathf.Abs(crossingOffset)>road.Width(t)+1){crossingOffset=Mathf.Clamp(crossingOffset,-road.Width(t)-1,road.Width(t)+1);crossingDirection=-crossingDirection;crossingWait=3;}return;
            }
            if(park)
            {
                if(gathering){citizen?.Pose(0,4);return;}
                if(!seated){Vector3 dest=parkCenter+Vector3.right*(direction*5.5f);Vector3 delta=dest-transform.position;if(delta.sqrMagnitude>.01f)transform.rotation=Quaternion.LookRotation(delta.normalized);transform.position=Vector3.MoveTowards(transform.position,dest,move);if(Vector3.Distance(transform.position,dest)<.15f)direction=-direction;}
                citizen?.Pose(seated?0:pace,seated?2:0);return;
            }
            var forward=road.Direction(t)*direction;
            stopped=town.TrafficAhead(this,transform.position,forward,kind==0?1.4f:5);
            float nodeDistance=(direction>0?1-t:t)*road.Length;
            if(kind!=0)move*=Mathf.Lerp(.45f,1,Mathf.Clamp01(nodeDistance/18));
            if(kind!=0&&nodeDistance<14&&town.JunctionBusy(this,world.Network.Node(direction>0?road.b:road.a,world.Origin)))stopped=true;
            float next=t+direction*move/road.Length;
            Vector3 target=Point(Mathf.Clamp01(next));
            // Static geometry probes keep walkers and riders out of buildings/furniture.
            Vector3 deltaMove=target-transform.position;
            if(deltaMove.magnitude>.01f)
                foreach(var hit in Physics.SphereCastAll(transform.position+Vector3.up*.65f,kind==0?.27f:.45f,deltaMove.normalized,deltaMove.magnitude+.45f))
                    if(!hit.collider.isTrigger&&hit.collider.GetComponentInParent<TownAmbientActor>()!=this&&hit.collider.bounds.max.y>.4f)stopped=true;
            if(stopped&&kind==0)
            {
                // Try either side within the pavement before turning back.
                float oldOffset=sidewalkOffset;
                foreach(float candidate in new[]{-.6f,.6f})
                {
                    sidewalkOffset=candidate;var detour=Point(Mathf.Clamp01(next));var step=detour-transform.position;
                    bool blocked=Physics.SphereCastAll(transform.position+Vector3.up*.65f,.27f,step.normalized,step.magnitude+.3f).Any(h=>!h.collider.isTrigger&&h.collider.bounds.max.y>.4f&&h.collider.GetComponentInParent<TownAmbientActor>()!=this);
                    if(!blocked&&!town.TrafficAhead(this,transform.position,step.normalized,1.4f)){target=detour;stopped=false;break;}
                    sidewalkOffset=oldOffset;
                }
            }
            if(stopped)
            {
                waited+=dt;citizen?.Pose(0,kind==0?0:1);
                if(kind==0&&waited>1.2f){direction=-direction;waited=0;}
                if(waited>7){town.Recover(this);}return;
            }
            waited=0;var before=transform.position;
            transform.position=Vector3.MoveTowards(transform.position,target,move);transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(deltaMove.sqrMagnitude>.001f?deltaMove.normalized:forward),dt*5);t=next;
            travel+=Vector3.Distance(before,transform.position);citizen?.Pose(pace,kind==0?0:kind==2?3:1);
            foreach(var wheel in wheels)wheel.Rotate(Vector3.right,move*Mathf.Rad2Deg/.32f,Space.Self);
            if(kind==0&&(t>.83f||t<.17f)){direction=-direction;return;}
            if(t>=1||t<=0)
            {
                Vector2Int node=direction>0?road.b:road.a;var previous=direction>0?road.a:road.b;var choices=world.Network.Neighbors(node).Where(c=>town.ActivityCell(c)&&c!=previous).ToArray();
                if(choices.Length==0){direction=-direction;t=Mathf.Clamp01(t);return;}
                var destination=choices[(index+Mathf.FloorToInt(travel/40))%choices.Length];road=world.Network.Edge(node,destination);direction=road.a==node?1:-1;t=direction>0?0:1;
            }
        }
    }
}
