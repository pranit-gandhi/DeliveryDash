using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace DeliveryDash
{
    [DefaultExecutionOrder(80)]
    public sealed class TownLivingTown : MonoBehaviour
    {
        public GameObject[] buildings,trees,vehicles,people;
        public GameObject bicycle;
        [Range(0,48)] public int pedestrianLimit=24;
        [Range(0,16)] public int vehicleLimit=8;
        [Range(0,12)] public int cyclistLimit=4;
        readonly List<TownAmbientActor> pool=new List<TownAmbientActor>();
        readonly Dictionary<Vector2Int,List<Vector3>> parks=new Dictionary<Vector2Int,List<Vector3>>();
        readonly Dictionary<Vector2Int,List<(Vector3 position,Vector3 facing)>> gathering=new Dictionary<Vector2Int,List<(Vector3,Vector3)>>();
        TownDeliveryWorld world;
        float nextPopulation;TownRoadNetwork activeNetwork;
        public int ActivePeople=>pool.Count(a=>a.gameObject.activeSelf&&a.kind==0);
        public int ActiveTraffic=>pool.Count(a=>a.gameObject.activeSelf&&a.kind!=0);
        public int RecoveryCount {get;private set;}
        public int RegisteredDistricts=>parks.Count;
        // Activity follows the player; scenery is generated for every streamed district.
        public bool ActivityCell(Vector2Int c)
        {
            var center=World.CurrentBlock;
            return Mathf.Abs(c.x-center.x)<=1&&Mathf.Abs(c.y-center.y)<=1&&World.IsDistrictLoaded(c);
        }
        public void ReleaseDistrict(Vector2Int key){parks.Remove(key);gathering.Remove(key);}
        public void ResetActivity()
        {
            foreach(var actor in pool)actor.gameObject.SetActive(false);
            parks.Clear();gathering.Clear();nextPopulation=0;
        }
        TownDeliveryWorld World=>world!=null?world:(world=GetComponent<TownDeliveryWorld>());
        public void BuildDistrict(Transform root,Vector2Int key)
        {
            if(buildings==null||buildings.Length==0)return;
            var group=new GameObject("Living neighborhood static scenery").transform;group.SetParent(root,false);
            var meet=new List<(Vector3,Vector3)>();gathering[key]=meet;
            var occupied=new List<Vector3>();var network=World.Network;var rng=new System.Random(World.Seed^key.x*7919^key.y*104729);
            float urban=TownDistrictPattern.Commercial(World.Seed,key);
            var details=GetComponent<TownEnvironmentDetails>();
            var paths=new List<(Vector3 a,Vector3 b)>();
            bool Owned(Vector3 p,float margin=7) => Mathf.Abs(p.x-root.position.x)<TownRoadNetwork.CellX*.5f-margin && Mathf.Abs(p.z-root.position.z)<TownRoadNetwork.CellZ*.5f-margin;
            bool PathClear(Vector3 p,float radius)
            {
                foreach(var path in paths){var d=path.b-path.a;var q=path.a+d*Mathf.Clamp01(Vector3.Dot(p-path.a,d)/d.sqrMagnitude);if(Vector3.Distance(p,q)<radius+1.2f)return false;}return true;
            }
            void Path(Transform parent,Vector3 a,Vector3 b)
            {
                var d=b-a;if(d.magnitude<.1f)return;
                var paving=details.Place(parent,"Footpath",Vector3.zero,Quaternion.identity,Vector3.one);
                if(paving!=null){paving.position=(a+b)*.5f; paving.rotation=Quaternion.LookRotation(d);paving.localScale=new Vector3(1.55f,1,d.magnitude);}
                paths.Add((a,b));
            }
            // Reserve a connected public space before filling street frontages.
            var saved=new List<Vector3>();parks[key]=saved;
            for(int attempt=0;attempt<90 && saved.Count<1;attempt++)
            {
                var p=root.position+new Vector3((float)rng.NextDouble()*72-36,0,(float)rng.NextDouble()*64-32);
                if(!Free(p,12,occupied,false))continue;
                var edge=network.NearestRoad(p,World.Origin,out float at,out var q);
                var entry=q+(p-q).normalized*(edge.Width(at)+1);
                // Reject connectors through delivery buildings or another street.
                bool clear=true;
                for(int j=1;j<10;j++)
                {
                    var v=Vector3.Lerp(entry,p,j/10f);
                    foreach(int side in new[]{-1,1})for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)
                        if(Vector3.Distance(v,network.Address(new TownAddress(key+new Vector2Int(x,z),side)).Building(World.Origin))<9)clear=false;
                }
                if(!clear)continue;
                saved.Add(p+TownRoadNetwork.Offset(World.Origin,Vector2Int.zero));occupied.Add(p);
                var park=new GameObject(urban>.57f?"Landscaped neighborhood square":"Public garden and walking loop").transform;park.SetParent(group,false);park.position=p;
                Path(group,entry,p);Path(park,p+Vector3.left*11,p+Vector3.right*11);
                Path(park,p+Vector3.back*10,p+Vector3.forward*10);
                var corners=new[]{p+Vector3.left*11,p+Vector3.forward*10,p+Vector3.right*11,p+Vector3.back*10};
                for(int loop=0;loop<4;loop++)Path(park,corners[loop],corners[(loop+1)%4]);
                for(int i=0;i<6;i++)
                {float angle=i*Mathf.PI/3+.3f;var v=new Vector3(Mathf.Cos(angle)*8,0,Mathf.Sin(angle)*8);var tree=Instantiate(trees[i%trees.Length],park).transform;tree.localPosition=v;tree.localScale*=.65f+(i%3)*.15f;occupied.Add(p+v);}
                for(int i=-1;i<=1;i+=2)
                {
                    details.Place(park,"Bench",new Vector3(i*4,0,2.3f),Quaternion.Euler(0,180,0),Vector3.one);
                    details.Place(park,"Garden bed",new Vector3(i*4,0,-3.5f),Quaternion.identity,Vector3.one);
                    if(urban<.45f)for(int row=0;row<3;row++)details.Place(park,"Garden bed",new Vector3(i*4,0,-5-row*1.1f),Quaternion.identity,new Vector3(1,1,.65f));
                }
                details.Place(park,"Bin",new Vector3(0,0,3),Quaternion.identity,Vector3.one);
            }
            // Use deep parcels as housing courts, with a reserved entrance alley.
            for(int attempt=0;attempt<60;attempt++)
            {
                var p=root.position+new Vector3((float)rng.NextDouble()*70-35,0,(float)rng.NextDouble()*60-30);
                if(!PathClear(p,16)||!Free(p,17,occupied,true))continue;
                var road=network.NearestRoad(p,World.Origin,out float at,out var q);
                var toward=(q-p).normalized;q-=toward*(road.Width(at)+2.6f);var across=Vector3.Cross(Vector3.up,toward);
                bool clear=true;
                for(int j=1;j<8;j++)if(!Free(Vector3.Lerp(p,q,j/8f),1.5f,occupied,false))clear=false;
                if(!clear)continue;
                var court=new GameObject("Residential courtyard cluster").transform;court.SetParent(group,false);court.position=p;
                Path(group,p,q+toward*1.6f);
                details.Place(court,"Plaza paving",Vector3.zero,Quaternion.identity,new Vector3(.65f,1,.65f));
                foreach(int side in new[]{-1,1})for(int row=0;row<2;row++)
                {
                    var pos=p+across*side*9+toward*(row*9-4.5f);
                    var house=Instantiate(buildings[rng.Next(Mathf.Min(11,buildings.Length))],court).transform;house.position=pos;house.rotation=Quaternion.LookRotation(-across*side);house.localScale=new Vector3(.78f,.85f+(float)rng.NextDouble()*.7f,.78f);occupied.Add(pos);
                }
                occupied.Add(p);details.Place(court,"Bench",new Vector3(0,0,-2),Quaternion.identity,Vector3.one);break;
            }
            // Frontages follow actual curves. Adjacent commercial parcels share a
            // building line; residential parcels vary by coherent neighborhood.
            foreach(var road in network.RoadsNear(key,1))
            {
                float spacing=urban>.5f?11.3f:10.2f;
                for(float t=.13f;t<.9f;t+=spacing/road.Length)for(int side=-1;side<=1;side+=2)
                {
                    var direction=road.Direction(t);var right=Vector3.Cross(Vector3.up,direction)*side;
                    var p=road.Point(t,World.Origin)+right*(road.Width(t)+6.7f+(urban>.5f?0:1.2f*(float)rng.NextDouble()));
                    if(!Owned(p) || !PathClear(p,6) || !Free(p,4.8f,occupied,true))continue;
                    bool shop=rng.NextDouble()<urban;
                    int index=shop&&buildings.Length>11?11+rng.Next(buildings.Length-11):rng.Next(Mathf.Min(11,buildings.Length));
                    var house=Instantiate(buildings[index],group).transform;house.position=p;house.rotation=Quaternion.LookRotation(-right);
                    house.localScale=new Vector3(shop?1:.9f,.85f+(float)rng.NextDouble()*.55f,shop?1:.9f);occupied.Add(p);
                    var front=p-right*(shop?4.3f:4.7f);
                    Path(group,front,road.Point(t,World.Origin)+right*(road.Width(t)+.9f));
                    if(shop)
                    {
                        meet.Add((front+TownRoadNetwork.Offset(World.Origin,Vector2Int.zero),-right));
                        if((index-11==1 || index-11==5) && rng.Next(2)==0)World.BuildObstacles(group,key,road,t,side);
                        if(rng.Next(3)==0){details.Place(house,"Cafe table",new Vector3(3,0,5.3f),Quaternion.identity,Vector3.one);details.Place(house,"Cafe chair",new Vector3(2,0,5.3f),Quaternion.Euler(0,90,0),Vector3.one);}
                        if(rng.Next(4)==0)details.Place(house,"Parked bicycle",new Vector3(-3,0,5),Quaternion.Euler(0,90,0),Vector3.one);
                    }
                    else if(rng.Next(3)==0)details.Place(house,"Garden bed",new Vector3(2,0,5),Quaternion.identity,Vector3.one);
                    if(rng.Next(7)==0 && vehicles.Length>0)
                    {var car=Instantiate(vehicles[rng.Next(vehicles.Length)],group).transform;car.position=p-right*4.6f+direction*2.6f;car.rotation=Quaternion.LookRotation(direction);var collider=car.gameObject.AddComponent<BoxCollider>();collider.center=new Vector3(0,.65f,0);collider.size=new Vector3(1.3f,1.3f,2.8f);}
                }
            }
            // Remaining large spaces become groves, with a varied understory.
            int woodlandAttempts=urban>.65f?180:360;
            for(int i=0;i<woodlandAttempts;i++)
            {
                var p=root.position+new Vector3((float)rng.NextDouble()*104-52,0,(float)rng.NextDouble()*88-44);
                if(!PathClear(p,2)||!Free(p,2.2f,occupied,false))continue;
                var tree=Instantiate(trees[rng.Next(trees.Length)],group).transform;tree.position=p;tree.rotation=Quaternion.Euler(0,rng.Next(360),0);tree.localScale*=.65f+(float)rng.NextDouble()*1.1f;occupied.Add(p);
                if(i%3==1)details.Place(tree,"Woodland understory",Vector3.zero,Quaternion.Euler(0,rng.Next(360),0),Vector3.one);
                if(i%3==0)details.Place(tree,"Garden bed",new Vector3(1.8f,0,0),Quaternion.identity,Vector3.one*.45f);
            }
        }
        bool Free(Vector3 p,float radius,List<Vector3> occupied,bool building)
        {
            var road=World.Network.NearestRoad(p,World.Origin,out float t,out var nearest);
            if(Vector3.Distance(p,nearest)<road.Width(t)+radius+1)return false;
            var cell=World.Network.CellAt(p,World.Origin);
            for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)for(int side=-1;side<=1;side+=2)
            {var lot=World.Network.Address(new TownAddress(cell+new Vector2Int(x,z),side));if(Vector3.Distance(p,lot.Building(World.Origin))<radius+8 || Vector3.Distance(p,lot.Stop(World.Origin))<radius+5)return false;}
            foreach(var q in occupied)if(Vector3.Distance(p,q)<radius+(building?5.1f:3.2f))return false;
            foreach(var c in Physics.OverlapSphere(p+Vector3.up*2,radius))
                if(!c.isTrigger&&!(c is CharacterController)&&c.bounds.max.y>.8f && c.GetComponentInParent<TownAmbientActor>()==null)return false;
            return true;
        }
        void Update()
        {
            if(World.Network==null)return;
            if(activeNetwork!=World.Network){foreach(var a in pool)a.gameObject.SetActive(false);activeNetwork=World.Network;}
            if(Time.time<nextPopulation)return;nextPopulation=Time.time+1;
            
            int total=pedestrianLimit+vehicleLimit+cyclistLimit;
            while(pool.Count<total)
            {
                int i=pool.Count,kind=i<pedestrianLimit?0:i<pedestrianLimit+vehicleLimit?1:2;
                var asset=kind==0?people[i%people.Length]:kind==1?vehicles[i%vehicles.Length]:bicycle;
                var go=new GameObject("Ambient "+(kind==0?"pedestrian":kind==1?"traffic":"cyclist")+" "+i);go.transform.SetParent(transform,false);
                var visual=Instantiate(asset,go.transform);visual.AddComponent<TownDistanceDetail>();var actor=go.AddComponent<TownAmbientActor>();actor.Initialize(this,World,kind,i,visual);go.SetActive(false);pool.Add(actor);
            }
            foreach(var a in pool)
            {
                if(a.gameObject.activeSelf&&!ActivityCell(World.Network.CellAt(a.transform.position,World.Origin)))a.gameObject.SetActive(false);
                if(!a.gameObject.activeSelf)Spawn(a);
            }
        }
        bool SafeSpawn(Vector3 p)
        {
            if(Vector3.Distance(p,World.Runner.transform.position)<22)return false;
            var camera=Camera.main;if(camera==null)return true;
            var v=camera.WorldToViewportPoint(p+Vector3.up);
            return v.z<=0 || v.x<-.1f || v.x>1.1f || v.y<-.1f || v.y>1.1f;
        }
        public void Spawn(TownAmbientActor actor)
        {
            var choices=World.Network.RoadsNear(World.CurrentBlock,1).Where(r=>ActivityCell(r.a)&&ActivityCell(r.b)).OrderBy(r=>(r.Point(.5f,World.Origin)-World.Runner.transform.position).sqrMagnitude).Take(12).ToArray();
            if(choices.Length==0)return;
            for(int attempt=0;attempt<choices.Length;attempt++)
            {
                var road=choices[(actor.index*7+attempt+RecoveryCount)%choices.Length];float t=.22f+(actor.index%5)*.12f;
                if(!SafeSpawn(road.Point(t,World.Origin)))continue;
                if(actor.kind==0&&(actor.index%7==4||actor.index%7==5))
                {
                    var spots=gathering.Where(pair=>ActivityCell(pair.Key)).SelectMany(pair=>pair.Value).ToArray();
                    if(spots.Length>0){var spot=spots[(actor.index/7)%spots.Length];var p=spot.position-TownRoadNetwork.Offset(World.Origin,Vector2Int.zero)+Vector3.Cross(Vector3.up,spot.facing)*(actor.index%7==4?-.55f:.55f);if(SafeSpawn(p)){actor.BeginGather(p,spot.facing);return;}}
                }
                if(actor.kind==0 && actor.index%5>=3)
                {
                    var park=parks.Where(pair=>ActivityCell(pair.Key)).SelectMany(pair=>pair.Value).ToArray();
                    if(park.Length>0){Vector3 center=park[actor.index%park.Length]-TownRoadNetwork.Offset(World.Origin,Vector2Int.zero);if(SafeSpawn(center)){actor.BeginPark(center,actor.index%5==4);return;}}
                }
                actor.BeginRoad(road,t);if(!SafeSpawn(actor.transform.position)){actor.gameObject.SetActive(false);continue;}return;
            }
        }
        public bool TrafficAhead(TownAmbientActor actor,Vector3 p,Vector3 direction,float gap)
        {
            foreach(var other in pool)
            {if(other==actor||!other.gameObject.activeSelf)continue;Vector3 delta=other.transform.position-p;float ahead=Vector3.Dot(delta,direction);if(ahead>0&&ahead<gap&&Vector3.Cross(delta,direction).magnitude<(other.kind==0?1.5f:2.4f))return true;}
            var d=World.Runner.transform.position-p;
            return Vector3.Dot(d,direction)>-1&&Vector3.Dot(d,direction)<gap+2&&Vector3.Cross(d,direction).magnitude<2.3f;
        }
        public bool JunctionBusy(TownAmbientActor actor,Vector3 node)
        {
            foreach(var other in pool)if(other!=actor&&other.kind!=0&&other.gameObject.activeSelf&&Vector3.Distance(other.transform.position,node)<11&&(Vector3.Distance(other.transform.position,node)<Vector3.Distance(actor.transform.position,node)-1||other.index<actor.index))return true;
            return Vector3.Distance(World.Runner.transform.position,node)<9;
        }
        public void Recover(TownAmbientActor a){RecoveryCount++;a.Recoveries++;a.gameObject.SetActive(false);}
    }
}
