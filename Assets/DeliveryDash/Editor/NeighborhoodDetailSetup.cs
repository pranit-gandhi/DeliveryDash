using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace DeliveryDash.Editor
{
    public static class NeighborhoodDetailSetup
    {
        const string Folder="Assets/DeliveryDash/Art/Neighborhood";
        static Material stone, dark, wood, leaf, leafLight, cream, red, glass, metal, soil, warm;
        static GameObject root;
        static Material Material(string name,Color color,float gloss=.15f)
        {
            string path=Folder+"/"+name+".mat"; var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,path);}
            m.color=color;m.SetFloat("_Glossiness",gloss);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
        }
        static Transform Part(string name,Vector3 p,Vector3 size,Material mat,PrimitiveType type=PrimitiveType.Cube)
        {
            var o=GameObject.CreatePrimitive(type);o.name=name;o.transform.SetParent(root.transform,false);o.transform.localPosition=p;o.transform.localScale=size;
            o.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(o.GetComponent<Collider>());return o.transform;
        }
        static void Box(string name,float x,float y,float z,float sx,float sy,float sz,Material mat) => Part(name,new Vector3(x,y,z),new Vector3(sx,sy,sz),mat);
        static void Bar(Vector3 a,Vector3 b,float thickness,Material mat)
        {var t=Part("Tubular frame",(a+b)/2,new Vector3(thickness,Vector3.Distance(a,b)/2,thickness),mat,PrimitiveType.Cylinder);t.rotation=Quaternion.FromToRotation(Vector3.up,b-a);}
        static void Solid(Vector3 center,Vector3 size){var c=root.AddComponent<BoxCollider>();c.center=center;c.size=size;}
        static void Label(string text,Vector3 p,float size,Color color)
        {
            var o=new GameObject(text);o.transform.SetParent(root.transform,false);o.transform.localPosition=p;o.transform.localRotation=Quaternion.Euler(0,180,0);
            var t=o.AddComponent<TextMesh>();t.text=text;t.fontSize=48;t.characterSize=size;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=color;
            t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");o.GetComponent<MeshRenderer>().sharedMaterial=t.font.material;
        }
        static void Leaves(Vector3 p,float size)
        {
            for(int i=0;i<5;i++)Part("Foliage cluster",p+new Vector3(Mathf.Sin(i*2.4f)*size*.28f,i%2*size*.25f,Mathf.Cos(i*2.4f)*size*.28f),new Vector3(size,size*.8f,size),i%2==0?leaf:leafLight,PrimitiveType.Sphere);
        }
        static GameObject Save(string name,System.Action build)
        {
            root=new GameObject(name);build();
            // Each prefab has one mesh per shared material, plus optional text and simple collision.
            var filters=root.GetComponentsInChildren<MeshFilter>();
            foreach(var group in filters.GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial))
            {
                var mesh=new Mesh {name=name+" "+group.Key.name};
                mesh.CombineMeshes(group.Select(f=>new CombineInstance {mesh=f.sharedMesh,transform=f.transform.localToWorldMatrix}).ToArray());
                string path=Folder+"/"+name+"-"+group.Key.name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing!=null){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,path);
                var child=new GameObject(group.Key.name);child.transform.SetParent(root.transform,false);child.AddComponent<MeshFilter>().sharedMesh=mesh;child.AddComponent<MeshRenderer>().sharedMaterial=group.Key;
            }
            foreach(var f in filters)Object.DestroyImmediate(f.gameObject);
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+name+".prefab");Object.DestroyImmediate(root);return prefab;
        }
        [MenuItem("DeliveryDash/Install neighborhood detail preview")]
        public static void Install()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play mode first");
            System.IO.Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            stone=Material("Warm limestone",new Color(.57f,.53f,.43f));dark=Material("Charcoal iron",new Color(.075f,.095f,.1f),.35f);
            wood=Material("Oiled timber",new Color(.35f,.19f,.095f));leaf=Material("Deep foliage",new Color(.16f,.28f,.11f));leafLight=Material("Sunlit foliage",new Color(.3f,.4f,.16f));
            cream=Material("Ivory paint",new Color(.87f,.81f,.64f));red=Material("Terracotta red",new Color(.55f,.13f,.065f));glass=Material("Recessed blue glass",new Color(.11f,.23f,.27f),.65f);
            metal=Material("Brushed metal",new Color(.42f,.45f,.43f),.55f);soil=Material("Soil and road repairs",new Color(.24f,.23f,.2f));warm=Material("Warm shop windows",new Color(.78f,.49f,.19f));
            warm.EnableKeyword("_EMISSION");warm.SetColor("_EmissionColor",new Color(.26f,.12f,.035f));
            var list=new List<GameObject>();
            list.Add(Save("Curb",()=>{Box("Curb stone",0,.07f,0,.24f,.14f,2.72f,stone);}));
            list.Add(Save("Road repair",()=>{Box("Irregular patch",0,0,0,2.1f,.012f,3.5f,soil);Box("Old repair",.3f,.002f,.3f,1.7f,.012f,3.2f,stone);for(int i=0;i<6;i++)Box("Tar crack",-.8f+i*.29f,.012f,Mathf.Sin(i)*.6f,.03f,.006f,1.4f,dark);}));
            list.Add(Save("Crosswalk stripe",()=>Box("Worn paint",0,0,0,.7f,.008f,2.5f,cream)));
            list.Add(Save("Drain",()=>{Box("Drain recess",0,0,0,.5f,.015f,.85f,dark);for(int i=0;i<7;i++)Box("Grate",0,.015f,-.36f+i*.12f,.49f,.025f,.035f,metal);}));
            list.Add(Save("Streetlight",()=>{Box("Base",0,.18f,0,.35f,.36f,.35f,dark);Bar(new Vector3(0,0,0),new Vector3(0,4.8f,0),.075f,dark);Bar(new Vector3(0,4.7f,0),new Vector3(0,4.7f,.65f),.07f,dark);Box("Lantern",0,4.45f,.65f,.32f,.45f,.32f,dark);Box("Warm lamp",0,4.44f,.82f,.23f,.3f,.02f,warm);Solid(new Vector3(0,2.4f,0),new Vector3(.25f,4.8f,.25f));}));
            list.Add(Save("Bench",()=>{for(int i=0;i<4;i++){Box("Seat slat",0,.53f,-.28f+i*.18f,2.1f,.08f,.13f,wood);Box("Back slat",0,.8f+i*.12f,-.38f,2.1f,.085f,.07f,wood);}for(int i=-1;i<=1;i+=2){Box("Leg",i*.8f,.28f,0,.09f,.5f,.65f,dark);Bar(new Vector3(i*.82f,.55f,-.37f),new Vector3(i*.82f,1.2f,-.37f),.04f,dark);}Solid(new Vector3(0,.55f,0),new Vector3(2.2f,1.1f,.8f));}));
            list.Add(Save("Tree planter",()=>{Box("Stone bed",0,.2f,0,2.5f,.4f,2.5f,stone);Box("Earth",0,.41f,0,2.2f,.03f,2.2f,soil);Bar(new Vector3(0,.4f,0),new Vector3(.12f,3.1f,0),.25f,wood);Bar(new Vector3(.08f,2,0),new Vector3(-.7f,3.4f,.2f),.13f,wood);Leaves(new Vector3(0,3.6f,0),2.6f);Solid(new Vector3(0,.3f,0),new Vector3(2.5f,.6f,2.5f));}));
            list.Add(Save("Door planter",()=>{Part("Terracotta pot",new Vector3(0,.27f,0),new Vector3(.5f,.27f,.5f),red,PrimitiveType.Cylinder);Leaves(new Vector3(0,.65f,0),.5f);Solid(new Vector3(0,.4f,0),new Vector3(.6f,.8f,.6f));}));
            list.Add(Save("Garden bed",()=>{Box("Raised bed",0,.15f,0,3.7f,.3f,2.2f,stone);Box("Soil",0,.31f,0,3.4f,.03f,1.9f,soil);for(int i=-1;i<=1;i++)Leaves(new Vector3(i*1.05f,.65f,0),.85f);Solid(new Vector3(0,.35f,0),new Vector3(3.7f,.7f,2.2f));}));
            list.Add(Save("Fence",()=>{for(int i=0;i<9;i++)Box("Picket",-1.6f+i*.4f,.55f,0,.1f,1.1f,.1f,cream);Box("Rail",0,.3f,0,3.5f,.08f,.12f,cream);Box("Rail",0,.8f,0,3.5f,.08f,.12f,cream);Solid(new Vector3(0,.55f,0),new Vector3(3.5f,1.1f,.15f));}));
            list.Add(Save("Bin",()=>{Part("Bin barrel",new Vector3(0,.48f,0),new Vector3(.65f,.48f,.65f),dark,PrimitiveType.Cylinder);Part("Bin lid",new Vector3(0,.98f,0),new Vector3(.72f,.04f,.72f),metal,PrimitiveType.Cylinder);Solid(new Vector3(0,.5f,0),new Vector3(.7f,1,.7f));}));
            list.Add(Save("Cafe table",()=>{Part("Table top",new Vector3(0,.85f,0),new Vector3(1.2f,.055f,1.2f),wood,PrimitiveType.Cylinder);Bar(Vector3.zero,new Vector3(0,.85f,0),.09f,dark);Box("Foot",0,.05f,0,.8f,.08f,.12f,dark);Box("Foot",0,.05f,0,.12f,.08f,.8f,dark);Part("Table flower pot",new Vector3(0,.98f,0),new Vector3(.15f,.1f,.15f),red,PrimitiveType.Cylinder);Solid(new Vector3(0,.45f,0),new Vector3(1.2f,.9f,1.2f));}));
            list.Add(Save("Cafe chair",()=>{Box("Seat",0,.47f,0,.5f,.06f,.5f,wood);for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Box("Leg",x*.21f,.24f,z*.21f,.04f,.48f,.04f,dark);Box("Back",0,.76f,-.23f,.5f,.4f,.055f,wood);Solid(new Vector3(0,.48f,0),new Vector3(.55f,.96f,.55f));}));
            list.Add(Save("Mailbox",()=>{Bar(Vector3.zero,new Vector3(0,1,0),.065f,dark);Box("Mailbox",0,1.03f,0,.43f,.35f,.3f,red);Box("Slot",0,1.08f,.155f,.27f,.035f,.015f,dark);Solid(new Vector3(0,.65f,0),new Vector3(.5f,1.3f,.4f));}));
            list.Add(Save("House window",()=>Window(false)));
            list.Add(Save("Shop window",()=>Window(true)));
            list.Add(Save("House entrance",()=>Door(false)));
            list.Add(Save("Shop entrance",()=>Door(true)));
            list.Add(Save("Cornice",()=>{Box("Cornice",0,0,0,1,.22f,.42f,cream);Box("Shadow line",0,-.18f,-.05f,1,.06f,.28f,stone);}));
            list.Add(Save("Downpipe",()=>{Bar(new Vector3(0,.2f,0),new Vector3(0,6.5f,0),.065f,dark);for(int i=1;i<7;i++)Box("Pipe bracket",0,i,0,.14f,.05f,.14f,metal);}));
            list.Add(Save("Balcony",()=>{Box("Balcony slab",0,0,.45f,2.8f,.17f,1.2f,stone);for(int i=-5;i<=5;i++)Bar(new Vector3(i*.25f,.1f,1),new Vector3(i*.25f,1.05f,1),.025f,dark);Bar(new Vector3(-1.4f,1.05f,1),new Vector3(1.4f,1.05f,1),.04f,dark);for(int i=-1;i<=1;i+=2)Bar(new Vector3(i*1.35f,1.05f,0),new Vector3(i*1.35f,1.05f,1),.04f,dark);}));
            list.Add(Save("Roof vent",()=>{Box("Chimney",0,.45f,0,.8f,.9f,.8f,red);Box("Chimney cap",0,.95f,0,1,.12f,1,stone);Box("Flue",0,1.02f,0,.5f,.03f,.5f,dark);}));
            list.Add(Save("Pizza sign",()=>{Box("Sign frame",0,0,0,6,.9f,.16f,dark);Box("Painted sign",0,0,.09f,5.8f,.72f,.02f,red);Label("FORNO • PIZZA",new Vector3(0,0,.12f),.14f,new Color(1,.9f,.64f));}));
            list.Add(Save("Striped awning",()=>{for(int i=0;i<14;i++){var t=Part("Canvas stripe",new Vector3(-3.25f+i*.5f,-.12f,1),new Vector3(.5f,.06f,2.1f),i%2==0?red:cream);t.localRotation=Quaternion.Euler(10,0,0);Box("Scalloped valance",-3.25f+i*.5f,-.4f,2,.5f,.25f,.04f,i%2==0?red:cream);}for(int i=-1;i<=1;i+=2)Bar(new Vector3(i*3.4f,0,0),new Vector3(i*3.4f,-.35f,2),.035f,dark);}));
            list.Add(Save("Menu board",()=>{Box("A board",0,.7f,0,.85f,1.3f,.08f,wood);Box("Chalkboard",0,.73f,.05f,.71f,1.08f,.025f,dark);Label("TODAY\nMARGHERITA\nPEPPERONI\nHOT PIZZA",new Vector3(0,.75f,.08f),.038f,Color.white);Solid(new Vector3(0,.7f,0),new Vector3(.9f,1.4f,.4f));}));
            list.Add(Save("Plaza paving",()=>{Box("Plaza base",0,-.02f,0,11,.05f,10,stone);for(int x=-5;x<=5;x++)for(int z=-4;z<=4;z++){var t=Part("Varied paving",new Vector3(x*.96f,.012f,z*1.04f),new Vector3(.91f,.025f,.99f),(x+z)%3==0?cream:stone);t.localRotation=Quaternion.Euler(0,((x*13+z*7)%3-1)*.5f,0);}}));
            list.Add(Save("Market stall",()=>{Box("Counter",0,.8f,0,3,.15f,1.3f,wood);for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Bar(new Vector3(x*1.4f,0,z*.6f),new Vector3(x*1.4f,2.5f,z*.6f),.055f,wood);for(int i=0;i<6;i++)Box("Canvas",-1.25f+i*.5f,2.5f,0,.5f,.1f,1.7f,i%2==0?cream:red);for(int i=-1;i<=1;i++){Box("Produce crate",i*.9f,1,0,.78f,.3f,.95f,wood);for(int j=0;j<5;j++)Part("Produce",new Vector3(i*.9f+Mathf.Sin(j)*.24f,1.2f,Mathf.Cos(j)*.26f),Vector3.one*.2f,i==0?red:leafLight,PrimitiveType.Sphere);}Solid(new Vector3(0,1.25f,0),new Vector3(3,2.5f,1.5f));}));
            list.Add(Save("Bicycle rack",()=>{for(int i=-1;i<=1;i++){Bar(new Vector3(i*.7f,0,-.3f),new Vector3(i*.7f,.75f,-.3f),.04f,metal);Bar(new Vector3(i*.7f,.75f,-.3f),new Vector3(i*.7f,.75f,.3f),.04f,metal);Bar(new Vector3(i*.7f,.75f,.3f),new Vector3(i*.7f,0,.3f),.04f,metal);}Solid(new Vector3(0,.4f,0),new Vector3(1.6f,.8f,.7f));}));
            list.Add(Save("Parked bicycle",()=>{for(int side=-1;side<=1;side+=2){Vector3 c=new Vector3(0,.36f,side*.65f);for(int i=0;i<24;i++){float a=i*Mathf.PI/12,b=(i+1)*Mathf.PI/12;Bar(c+new Vector3(0,Mathf.Sin(a),Mathf.Cos(a))*.34f,c+new Vector3(0,Mathf.Sin(b),Mathf.Cos(b))*.34f,.033f,dark);if(i%3==0)Bar(c,c+new Vector3(0,Mathf.Sin(a),Mathf.Cos(a))*.32f,.009f,metal);}}Vector3 rear=new Vector3(0,.36f,-.65f),pedal=new Vector3(0,.34f,0),seat=new Vector3(0,.85f,-.2f),front=new Vector3(0,.9f,.45f);Bar(rear,pedal,.035f,red);Bar(rear,seat,.035f,red);Bar(seat,pedal,.035f,red);Bar(seat,front,.035f,red);Bar(pedal,front,.035f,red);Bar(front,new Vector3(0,.36f,.65f),.035f,metal);Box("Saddle",0,.9f,-.2f,.24f,.06f,.28f,dark);Bar(new Vector3(-.3f,1,.45f),new Vector3(.3f,1,.45f),.03f,dark);Solid(new Vector3(0,.5f,0),new Vector3(.6f,1,2));}));
            var world=Object.FindFirstObjectByType<TownDeliveryWorld>();var details=world.GetComponent<TownEnvironmentDetails>();if(details==null)details=world.gameObject.AddComponent<TownEnvironmentDetails>();
            details.prefabs=list.ToArray();EditorUtility.SetDirty(details);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(world.gameObject.scene);EditorSceneManager.SaveScene(world.gameObject.scene);
            Debug.Log("Installed neighborhood preview: "+list.Count+" original reusable prefabs, shared materials; limited to districts 0,0 and 1,0.");
        }
        static void Window(bool shop)
        {
            float w=shop?2.5f:1.65f,h=shop?1.9f:1.8f;
            Box("Recess shadow",0,0,0,w+.25f,h+.25f,.05f,dark);Box("Glass",0,0,.035f,w,h,.035f,shop?warm:glass);
            for(int i=-1;i<=1;i+=2){Box("Frame",i*w/2,0,.09f,.09f,h+.16f,.14f,cream);Box("Frame",0,i*h/2,.09f,w+.16f,.09f,.14f,cream);}
            Box("Mullion",0,0,.1f,.055f,h,.12f,cream);Box("Sill",0,-h/2-.1f,.16f,w+.35f,.14f,.45f,stone);
            if(!shop)for(int i=-1;i<=1;i+=2){Box("Shutter",i*(w/2+.3f),0,.07f,.45f,h,.08f,wood);for(int j=0;j<8;j++)Box("Shutter louver",i*(w/2+.3f),-.7f+j*.2f,.12f,.42f,.045f,.035f,stone);}
        }
        static void Door(bool shop)
        {
            Box("Door recess",0,1.22f,0,1.45f,2.44f,.04f,dark);Box("Door leaf",0,1.18f,.04f,1.18f,2.3f,.07f,wood);
            Box("Door glass",0,1.55f,.09f,.83f,1.16f,.02f,shop?warm:glass);Box("Lower panel",0,.45f,.1f,.86f,.5f,.03f,stone);
            for(int i=-1;i<=1;i+=2)Box("Door jamb",i*.72f,1.24f,.12f,.14f,2.48f,.2f,cream);
            Box("Lintel",0,2.48f,.12f,1.58f,.17f,.24f,cream);Box("Door handle",.42f,1.05f,.17f,.045f,.25f,.07f,metal);
        }
    }
}
