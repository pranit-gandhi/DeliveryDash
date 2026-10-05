using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DeliveryDash
{
    // Reusable street and facade details for every streamed neighborhood.
    public sealed class TownEnvironmentDetails : MonoBehaviour
    {
        public GameObject[] prefabs;
        readonly Dictionary<string,GameObject> library = new Dictionary<string,GameObject>();
        public bool Includes(Vector2Int key) => true;
        Transform Group(Transform parent,string name)
        { var t=new GameObject(name).transform; t.SetParent(parent,false); return t; }
        public Transform Place(Transform parent,string name,Vector3 p,Quaternion rotation,Vector3 scale)
        {
            if(library.Count==0 && prefabs!=null) foreach(var prefab in prefabs) if(prefab!=null) library[prefab.name]=prefab;
            if(!library.TryGetValue(name,out var asset)) return null;
            var t=Instantiate(asset,parent).transform; t.localPosition=p;t.localRotation=rotation;t.localScale=scale;return t;
        }
        Transform Prop(Transform parent,string name,Vector3 p,float yaw=0) => Place(parent,name,p,Quaternion.Euler(0,yaw,0),Vector3.one);
        public void Road(Transform parent,Vector2Int key,TownRoadNetwork network,TownRoadNetwork.Road road)
        {
            if(!Includes(key))return;
            var group=Group(parent,"Neighborhood street details");
            int count=Mathf.CeilToInt(road.Length/2.8f);
            for(int i=0;i<count;i++)
            {
                float t=(i+.5f)/count;var p=road.Point(t,key);var d=road.Direction(t);var r=Vector3.Cross(Vector3.up,d);
                if(Vector3.Distance(p,network.Node(road.a,key))<network.JunctionRadius(road.a)+2 || Vector3.Distance(p,network.Node(road.b,key))<network.JunctionRadius(road.b)+2) continue;
                bool bay=false;
                for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)for(int side=-1;side<=1;side+=2)
                {var lot=network.Address(new TownAddress(key+new Vector2Int(x,z),side)); if(Vector3.Distance(p,lot.road.Point(lot.t,key))<5.3f)bay=true;}
                for(int side=-1;side<=1;side+=2)
                {
                    if(!bay) Place(group,"Curb",p+r*side*(road.Width(t)+.12f),Quaternion.LookRotation(d),new Vector3(1,1,road.Length/count/2.8f));
                    if(i%7==2&&!bay) Place(group,"Drain",p+r*side*(road.Width(t)-.35f)+Vector3.up*.025f,Quaternion.LookRotation(d),Vector3.one);
                    if(i%12==4&&!bay) Place(group,"Streetlight",p+r*side*(road.Width(t)+1.45f),Quaternion.LookRotation(-r*side),Vector3.one);
                }
                if(i%9==3) Place(group,"Road repair",p+r*Mathf.Sin(i*4)*2+Vector3.up*.025f,Quaternion.LookRotation(d)*Quaternion.Euler(0,i%3*6,0),new Vector3(1,1,.8f+i%3*.15f));
            }
            float crossing=(network.JunctionRadius(road.a)+4)/road.Length;
            Vector3 cp=road.Point(crossing,key),cr=Vector3.Cross(Vector3.up,road.Direction(crossing));
            for(float w=-road.Width(crossing)+.8f;w<road.Width(crossing)-.5f;w+=1.4f)
                Place(group,"Crosswalk stripe",cp+cr*w+Vector3.up*.032f,Quaternion.LookRotation(road.Direction(crossing)),Vector3.one);
        }
        public void Lot(Transform frame,Vector2Int key,TownRoadNetwork network,TownAddress address,float front,float scale,uint hash,Transform house)
        {
            if(!Includes(key))return;
            bool shop=TownMap.IsShop(networkSeed,address); // Seed supplied by district call.
            int side=address.side;
            var facade=Group(frame,"Neighborhood facade details");facade.localPosition=new Vector3(side*(front-.12f),0,0);facade.localRotation=Quaternion.Euler(0,-side*90,0);
            // In this frame +Z points away from the building, toward its road.
            Prop(facade,shop?"Shop entrance":"House entrance",new Vector3(0,0,.05f));
            var body=house.Find("Plaster body");
            float breadth=body!=null?body.localScale.z*scale:13*scale;
            float height=body!=null?body.localScale.y*house.localScale.y:8;
            float depth=body!=null?body.localScale.x*scale:7*scale;
            facade.localPosition+=Vector3.right*side*(4.1f*scale-depth*.5f-.03f);
            for(int i=-1;i<=1;i+=2)
            {
                Prop(facade,shop?"Shop window":"House window",new Vector3(i*3.1f,shop?1.35f:1.8f,.05f));
                Prop(facade,"House window",new Vector3(i*3.1f,4.75f,.06f));
                if((hash+i)%3!=0)Prop(facade,"Balcony",new Vector3(i*3.1f,3.8f,.15f));
                Place(facade,"Downpipe",new Vector3(i*(breadth*.5f-.3f),0,.1f),Quaternion.identity,new Vector3(1,height/6.5f,1));
            }
            Place(facade,"Cornice",new Vector3(0,height-.15f,0),Quaternion.identity,new Vector3(breadth,1,1));
            Prop(facade,"Roof vent",new Vector3(2.1f,height+.7f,-2));
            for(float y=7.5f;y<height-1;y+=2.7f)for(int i=-1;i<=1;i+=2)Prop(facade,"House window",new Vector3(i*3.1f,y,.05f));
            for(int edge=-1;edge<=1;edge+=2)
            {
                var sideWall=Group(facade,"Side facade");sideWall.localPosition=new Vector3(edge*(breadth*.5f+.04f),0,-depth*.5f);sideWall.localRotation=Quaternion.Euler(0,edge*90,0);
                for(float y=2;y<height-1;y+=2.7f)for(int col=-1;col<=1;col+=2)Prop(sideWall,"House window",new Vector3(col*depth*.25f,y,0));
                Place(sideWall,"Cornice",new Vector3(0,height-.15f,0),Quaternion.identity,new Vector3(depth,1,1));
            }
            if(shop)
            {
                Prop(facade,"Pizza sign",new Vector3(0,3.7f,.32f));
                Prop(facade,"Striped awning",new Vector3(0,2.95f,0));
                Prop(facade,"Menu board",new Vector3(2.4f,0,1.4f),-12);
            }
            else
            {
                Prop(facade,"Mailbox",new Vector3(2,0,1.2f));
                Prop(facade,"Door planter",new Vector3(-1.25f,0,.75f));
                Prop(facade,"Door planter",new Vector3(1.25f,0,.75f));
            }
            // Outdoor furniture sits beside entrances, never across the handoff corridor.
            for(int s=-1;s<=1;s+=2)
            {
                Vector3 local=new Vector3(s*(breadth*.5f+3),0,1);
                if(Clear(network,facade.TransformPoint(local),key,3))
                {
                    if(shop){Place(facade,"Plaza paving",local,Quaternion.identity,new Vector3(.4f,1,.4f));Prop(facade,"Cafe table",local);Prop(facade,"Cafe chair",local+new Vector3(-1,0,0),90);Prop(facade,"Cafe chair",local+new Vector3(1,0,0),-90);}
                    else{Prop(facade,"Garden bed",local);Prop(facade,"Fence",local+new Vector3(0,0,1.5f));}
                }
            }
            if(shop && (key==Vector2Int.zero || hash%3==0))
            {
                // A small pedestrian plaza adjacent to the shop, with no street-blocking props.
                var plaza=Group(facade,"Neighborhood pocket plaza");plaza.localPosition=new Vector3(breadth*.5f+9,0,-1);
                if(Clear(network,plaza.position,key,6))
                {
                    Place(plaza,"Plaza paving",Vector3.zero,Quaternion.identity,Vector3.one);
                    Prop(plaza,"Tree planter",new Vector3(3,0,-2));Prop(plaza,"Bench",new Vector3(3,0,.8f),180);
                    Prop(plaza,"Tree planter",new Vector3(-3,0,-2));Prop(plaza,"Bench",new Vector3(-3,0,.8f),180);
                    Prop(plaza,"Market stall",new Vector3(0,0,-3),0);Prop(plaza,"Bin",new Vector3(4.3f,0,2));
                    Prop(plaza,"Bicycle rack",new Vector3(-3,0,3));Prop(plaza,"Parked bicycle",new Vector3(-2.5f,0,3),90);
                }
                else Destroy(plaza.gameObject);
            }
        }
        public void FinishDistrict(Transform district)
        {
            foreach(var group in district.GetComponentsInChildren<Transform>().Where(t=>t.name=="Neighborhood facade details"||t.name=="Neighborhood street details").ToArray())
            {
                var filters=group.GetComponentsInChildren<MeshFilter>();
                var lifetime=group.gameObject.AddComponent<NeighborhoodMeshLifetime>();
                foreach(var batch in filters.GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial))
                {
                    var combined=new Mesh {name="Neighborhood shared material batch",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
                    combined.CombineMeshes(batch.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=group.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray());
                    lifetime.meshes.Add(combined);
                    var go=new GameObject("Combined "+batch.Key.name);go.transform.SetParent(group,false);go.AddComponent<MeshFilter>().sharedMesh=combined;go.AddComponent<MeshRenderer>().sharedMaterial=batch.Key;
                }
                foreach(var f in filters)f.GetComponent<MeshRenderer>().enabled=false;
            }
        }
        int networkSeed;
        public void BeginDistrict(int seed) { networkSeed=seed; }
        bool Clear(TownRoadNetwork network,Vector3 world,Vector2Int key,float radius)
        {
            var owner=GetComponent<TownDeliveryWorld>();
            var road=network.NearestRoad(world,owner.Origin,out float t,out var point);
            if(Vector3.Distance(world,point)<road.Width(t)+radius+1)return false;
            // Check other lot footprints as well as roads. Own frontage is placed separately.
            foreach(var cell in new[]{key+Vector2Int.left,key+Vector2Int.right,key+Vector2Int.up,key+Vector2Int.down})
            for(int side=-1;side<=1;side+=2)
            {var lot=network.Address(new TownAddress(cell,side));if(Vector3.Distance(world,lot.Building(owner.Origin))<radius+8)return false;}
            return true;
        }
    }
    public sealed class NeighborhoodMeshLifetime : MonoBehaviour
    {
        public readonly List<Mesh> meshes=new List<Mesh>();
        void OnDestroy(){foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);}
    }
}
