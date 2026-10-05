using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace DeliveryDash.Editor
{
    public static class LivingTownSetup
    {
        const string Folder="Assets/DeliveryDash/Art/LivingTown";
        static Material Palette(string kit)
        {
            string path=Folder+"/"+kit+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,path);}
            m.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/DeliveryDash/ThirdParty/Kenney/"+kit+"/Textures/colormap.png");m.color=Color.white;m.SetFloat("_Glossiness",.12f);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
        }
        static GameObject Model(string kit,string name,Material mat,float targetLength,bool solid)
        {
            string path="Assets/DeliveryDash/ThirdParty/Kenney/"+kit+"/"+name+".fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.isReadable=true;importer.SaveAndReimport();
            var root=new GameObject(name);var visual=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),root.transform);
            var renderers=visual.GetComponentsInChildren<Renderer>();Bounds bounds=renderers[0].bounds;foreach(var r in renderers){bounds.Encapsulate(r.bounds);r.sharedMaterials=r.sharedMaterials.Select(_=>mat).ToArray();}
            float factor=targetLength/Mathf.Max(bounds.size.x,bounds.size.z);visual.transform.localScale*=factor;visual.transform.localPosition-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*factor;
            if(solid){var c=root.AddComponent<BoxCollider>();c.center=new Vector3(0,bounds.size.y*factor*.5f,0);c.size=new Vector3(bounds.size.x*factor,bounds.size.y*factor,bounds.size.z*factor);}
            Debug.Log(name+" imported size "+bounds.size+" children "+string.Join(",",visual.GetComponentsInChildren<Transform>().Select(t=>t.name).Take(15)));
            return root;
        }
        static GameObject Save(GameObject root,string name){root.name=name;var p=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+name+".prefab");UnityEngine.Object.DestroyImmediate(root);return p;}
        static Material ColorMat(string name,Color color)
        {string p=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(p);if(m==null){m=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,p);}m.color=color;m.enableInstancing=true;EditorUtility.SetDirty(m);return m;}
        static void Wheels(GameObject root,float width,float length,float radius,Material mat)
        {
            var existing=root.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("wheel-")&&t.GetComponent<MeshFilter>()!=null).ToArray();
            if(existing.Length>0){foreach(var t in existing)t.name="SpinWheel "+t.name;return;}
            // Rotating wheels supplement kit bodies whose tires are separate assets.
            foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})
            {
                var pivot=new GameObject("SpinWheel").transform;pivot.SetParent(root.transform,false);pivot.localPosition=new Vector3(x*width,radius,z*length);
                var tire=GameObject.CreatePrimitive(PrimitiveType.Cylinder);tire.transform.SetParent(pivot,false);tire.transform.localRotation=Quaternion.Euler(0,0,90);tire.transform.localScale=new Vector3(radius*2,.10f,radius*2);tire.GetComponent<Renderer>().sharedMaterial=mat;UnityEngine.Object.DestroyImmediate(tire.GetComponent<Collider>());
                var hub=GameObject.CreatePrimitive(PrimitiveType.Cube);hub.transform.SetParent(pivot,false);hub.transform.localPosition=new Vector3(x*.115f,0,0);hub.transform.localScale=new Vector3(.025f,radius*1.25f,.045f);hub.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/DeliveryDash/Art/Neighborhood/Brushed metal.mat");UnityEngine.Object.DestroyImmediate(hub.GetComponent<Collider>());
            }
        }
        static GameObject Citizen(int i)
        {
            var root=new GameObject("Citizen "+i);var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DeliveryDash/ThirdParty/Quaternius/CourierBase.fbx"),root.transform);model.transform.localRotation=Quaternion.Euler(0,180,0);
            var skin=ColorMat("Citizen skin",Color.white);skin.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/DeliveryDash/ThirdParty/Quaternius/CourierBaseColor.png");
            var shirt=ColorMat("Citizen shirt "+i,Color.HSVToRGB(i*.17f,.5f,.55f));var trousers=ColorMat("Citizen trousers",new Color(.13f,.16f,.19f));
            var body=model.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name=="SuperHero_Male");body.sharedMaterial=skin;
            foreach(var item in new[]{("Courier_Tee.asset",shirt),("Courier_HalfSleeves.asset",shirt),("Courier_Shorts.asset",trousers)})
            {
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DeliveryDash/Art/Generated/"+item.Item1);if(mesh==null)continue;
                var o=new GameObject("Citizen clothing");o.transform.SetParent(body.transform.parent,false);o.transform.localPosition=body.transform.localPosition;o.transform.localRotation=body.transform.localRotation;o.transform.localScale=body.transform.localScale;
                var r=o.AddComponent<SkinnedMeshRenderer>();r.sharedMesh=mesh;r.sharedMaterial=item.Item2;r.bones=body.bones;r.rootBone=body.rootBone;r.localBounds=body.localBounds;
            }
            foreach(string side in new[]{"l","r"})
            {
                var foot=model.GetComponentsInChildren<Transform>().First(t=>t.name=="foot_"+side);
                var shoe=new GameObject("Citizen sneaker");shoe.transform.SetParent(root.transform,false);shoe.transform.position=foot.position+new Vector3(0,-.025f,.02f);shoe.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DeliveryDash/Art/Generated/CourierSneaker.asset");shoe.AddComponent<MeshRenderer>().sharedMaterial=trousers;shoe.transform.SetParent(foot,true);
            }
            var head=model.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="head");
            if(head!=null)
            {
                var hat=GameObject.CreatePrimitive(i%3==0?PrimitiveType.Cylinder:PrimitiveType.Sphere);hat.name=i%3==0?"Citizen cap":"Citizen hair";
                hat.transform.position=head.position+Vector3.up*.17f;hat.transform.localScale=i%3==0?new Vector3(.31f,.055f,.32f):new Vector3(.25f,.16f,.27f);hat.transform.SetParent(head,true);
                hat.GetComponent<Renderer>().sharedMaterial=i%3==0?shirt:trousers;UnityEngine.Object.DestroyImmediate(hat.GetComponent<Collider>());
            }
            root.transform.localScale=Vector3.one*(.91f+i*.025f);root.AddComponent<TownCitizenMotion>();
            return root;
        }
        static GameObject Commercial(int variant)
        {
            var root=new GameObject("Neighborhood storefront");
            var plaster=ColorMat("Shop plaster "+variant,new[]{new Color(.72f,.66f,.51f),new Color(.58f,.67f,.68f),new Color(.73f,.54f,.43f)}[variant%3]);
            var trim=ColorMat("Shop limestone",new Color(.85f,.82f,.69f));var glass=ColorMat("Shop glass",new Color(.17f,.28f,.31f));var awning=ColorMat("Shop awning "+variant,Color.HSVToRGB(variant*.22f,.45f,.45f));
            void Part(string name,Vector3 pos,Vector3 size,Material mat){var p=GameObject.CreatePrimitive(PrimitiveType.Cube);p.name=name;p.transform.SetParent(root.transform,false);p.transform.localPosition=pos;p.transform.localScale=size;p.GetComponent<Renderer>().sharedMaterial=mat;UnityEngine.Object.DestroyImmediate(p.GetComponent<Collider>());}
            float height=variant%3==2?11:7;
            Part("Masonry",new Vector3(0,height*.5f,0),new Vector3(10.7f,height,8),plaster);
            Part("Roof cornice",new Vector3(0,height,0),new Vector3(11,.22f,8.3f),trim);
            for(int x=-1;x<=1;x++)
            {
                Part("Recessed display surround",new Vector3(x*3.4f,1.6f,4.04f),new Vector3(2.9f,2.9f,.15f),trim);
                Part("Shop window",new Vector3(x*3.4f,1.6f,4.14f),new Vector3(2.5f,2.5f,.035f),glass);
                Part("Striped awning",new Vector3(x*3.4f,3.35f,4.5f),new Vector3(3.25f,.16f,1.2f),awning);
                for(float y=4.8f;y<height;y+=3.2f){Part("Upper window trim",new Vector3(x*3.4f,y,4.04f),new Vector3(1.9f,1.9f,.12f),trim);Part("Upper glass",new Vector3(x*3.4f,y,4.12f),new Vector3(1.6f,1.6f,.035f),glass);}
            }
            for(int side=-1;side<=1;side+=2)for(float y=2;y<height;y+=3.1f)for(int z=-1;z<=1;z++)
            {
                Part("Side window surround",new Vector3(side*5.39f,y,z*2.5f),new Vector3(.12f,1.8f,1.6f),trim);
                Part("Side window glass",new Vector3(side*5.47f,y,z*2.5f),new Vector3(.035f,1.5f,1.3f),glass);
            }
            Part("Roof access",new Vector3(-2,height+.7f,-1),new Vector3(2,1.4f,2),plaster);
            Part("Roof vent",new Vector3(2,height+.35f,1),new Vector3(1.5f,.7f,1.2f),glass);
            Part("Shop sign",new Vector3(0,3.85f,4.1f),new Vector3(7,.5f,.12f),awning);
            var sign=new GameObject("Store name");sign.transform.SetParent(root.transform,false);sign.transform.localPosition=new Vector3(0,3.83f,4.2f);sign.transform.localRotation=Quaternion.Euler(0,180,0);
            var text=sign.AddComponent<TextMesh>();text.text=new[]{"BAKERY • DAILY BREAD","GROCER • FRESH FOOD","BOOKS • PAPER & INK","CAFE • CORNER CUP","LAUNDRY • SPIN CYCLE","WORKSHOP • REPAIRS"}[variant];text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.characterSize=.2f;text.fontSize=40;text.color=Color.white;text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            string fontPath=Folder+"/World sign text.mat";var fontMat=AssetDatabase.LoadAssetAtPath<Material>(fontPath);if(fontMat==null){fontMat=new Material(Shader.Find("DeliveryDash/Depth tested world text"));AssetDatabase.CreateAsset(fontMat,fontPath);}sign.AddComponent<TownWorldText>().sharedFontMaterial=fontMat;
            var c=root.AddComponent<BoxCollider>();c.center=new Vector3(0,height*.5f,0);c.size=new Vector3(10.7f,height,8);
            return Save(root,"Connected storefront "+variant);
        }
        static void Understory(TownDeliveryWorld world)
        {
            var root=new GameObject("Woodland understory");
            var leaf=ColorMat("Woodland leaves",new Color(.24f,.35f,.2f));var rock=ColorMat("Woodland stone",new Color(.4f,.4f,.34f));
            for(int i=0;i<7;i++)
            {
                var part=GameObject.CreatePrimitive(i%3==0?PrimitiveType.Cube:PrimitiveType.Sphere);part.transform.SetParent(root.transform,false);
                float a=i*2.4f;part.transform.localPosition=new Vector3(Mathf.Cos(a)*1.4f,.2f,Mathf.Sin(a)*1.4f);part.transform.localScale=new Vector3(.55f+i%3*.15f,.35f,.6f);part.transform.localRotation=Quaternion.Euler(i*7,i*51,i*9);
                part.GetComponent<Renderer>().sharedMaterial=i%3==0?rock:leaf;UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            }
            var asset=Save(root,"Woodland understory");
            var path=GameObject.CreatePrimitive(PrimitiveType.Cube);path.name="Footpath";path.transform.localScale=Vector3.one;
            var mesh=path.GetComponent<MeshFilter>().sharedMesh;
            var pathRoot=new GameObject("Footpath");path.transform.SetParent(pathRoot.transform,false);path.transform.localPosition=Vector3.up*-.025f;path.transform.localScale=new Vector3(1,.04f,1);path.GetComponent<Renderer>().sharedMaterial=ColorMat("Footpath stone",new Color(.61f,.58f,.47f));UnityEngine.Object.DestroyImmediate(path.GetComponent<Collider>());
            var pathAsset=Save(pathRoot,"Footpath");var details=world.GetComponent<TownEnvironmentDetails>();
            var serialized=new SerializedObject(details);var list=serialized.FindProperty("prefabs");
            if(list!=null){foreach(var addition in new[]{asset,pathAsset}){bool found=false;for(int i=0;i<list.arraySize;i++)if(list.GetArrayElementAtIndex(i).objectReferenceValue==addition)found=true;if(!found){list.arraySize++;list.GetArrayElementAtIndex(list.arraySize-1).objectReferenceValue=addition;}}serialized.ApplyModifiedPropertiesWithoutUndo();}
        }
        static GameObject Mower(Material palette,Material rubber)
        {
            var root=new GameObject("Original ride-on pizza mower");
            var green=ColorMat("Mower green",new Color(.24f,.48f,.18f));var seat=ColorMat("Mower seat",new Color(.15f,.17f,.18f));
            void Part(string name,Vector3 pos,Vector3 size,Material mat){var p=GameObject.CreatePrimitive(PrimitiveType.Cube);p.name=name;p.transform.SetParent(root.transform,false);p.transform.localPosition=pos;p.transform.localScale=size;p.GetComponent<Renderer>().sharedMaterial=mat;UnityEngine.Object.DestroyImmediate(p.GetComponent<Collider>());}
            Part("Lawn mower deck",new Vector3(0,.3f,0),new Vector3(1.2f,.2f,1.8f),green);
            Part("Rounded engine cover",new Vector3(0,.58f,.53f),new Vector3(.73f,.42f,.7f),green);
            Part("Seat cushion",new Vector3(0,.78f,-.28f),new Vector3(.65f,.14f,.55f),seat);
            Part("Seat back",new Vector3(0,1.02f,-.57f),new Vector3(.65f,.48f,.12f),seat);
            Part("Steering column",new Vector3(0,.84f,.3f),new Vector3(.07f,.6f,.07f),seat);
            Part("Steering bar",new Vector3(0,1.14f,.3f),new Vector3(.6f,.06f,.06f),seat);
            Part("Pizza cargo rack",new Vector3(0,.75f,-.92f),new Vector3(.9f,.07f,.6f),seat);
            Wheels(root,.57f,.63f,.3f,rubber);return root;
        }
        [MenuItem("DeliveryDash/Install living town preview")]
        public static void Install()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode first");Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var town=UnityEngine.Object.FindFirstObjectByType<TownDeliveryWorld>();var living=town.GetComponent<TownLivingTown>();if(living==null)living=town.gameObject.AddComponent<TownLivingTown>();
            Understory(town);
            var carMat=Palette("CarKit");var cityMat=Palette("Suburban");var tire=ColorMat("Rubber",new Color(.035f,.04f,.045f));
            living.buildings="abcdefghijk".Select((c,i)=>{
                var varied=ColorMat("House palette "+i,Color.Lerp(Color.white,new[]{new Color(.75f,.65f,.55f),new Color(.6f,.7f,.8f),new Color(.8f,.7f,.6f)}[i%3],.35f));
                varied.shader=Shader.Find("DeliveryDash/Replaceable house palette");varied.mainTexture=cityMat.mainTexture;
                varied.SetColor("_RoofColor",new[]{new Color(.43f,.22f,.16f),new Color(.29f,.32f,.36f),new Color(.56f,.43f,.29f),new Color(.3f,.4f,.33f)}[i%4]);
                return Save(Model("Suburban","building-type-"+c,varied,9,true),"House "+c);
            }).ToArray();
            living.buildings=living.buildings.Concat(Enumerable.Range(0,6).Select(Commercial)).ToArray();
            living.trees=new[]{Save(Model("Suburban","tree-large",cityMat,3.6f,true),"Tree broad"),Save(Model("Suburban","tree-small",cityMat,2.5f,true),"Tree slender")};
            // Restrict tree collision to trunks rather than foliage bounds.
            foreach(var asset in living.trees){string p=AssetDatabase.GetAssetPath(asset);var instance=PrefabUtility.LoadPrefabContents(p);var c=instance.GetComponent<BoxCollider>();c.center=new Vector3(0,1,0);c.size=new Vector3(.45f,2,.45f);PrefabUtility.SaveAsPrefabAsset(instance,p);PrefabUtility.UnloadPrefabContents(instance);}
            living.people=Enumerable.Range(0,6).Select(i=>Save(Citizen(i),"Citizen "+i)).ToArray();
            var cars=new List<GameObject>();
            foreach(string name in new[]{"sedan","van","delivery"})
            {
                var car=Model("CarKit",name,carMat,2.9f,false);Wheels(car,.65f,.85f,.3f,tire);
                var person=UnityEngine.Object.Instantiate(living.people[cars.Count],car.transform);person.name="Visible driver";person.transform.localPosition=new Vector3(-.2f,.25f,-.1f);person.transform.localScale=Vector3.one*.68f;
                cars.Add(Save(car,"Traffic "+name));
            }
            living.vehicles=cars.ToArray();
            var cycle=new GameObject("Cyclist");var bike=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DeliveryDash/Art/Neighborhood/Parked bicycle.prefab"),cycle.transform);foreach(var c in bike.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(c);
            var rider=UnityEngine.Object.Instantiate(living.people[3],cycle.transform);rider.transform.localPosition=new Vector3(0,-.12f,-.16f);rider.transform.localScale=Vector3.one*.85f;
            // Separate moving rims make pedal travel visible even though the parked-bike mesh is combined.
            foreach(float z in new[]{-.65f,.65f}){var pivot=new GameObject("SpinWheel").transform;pivot.SetParent(cycle.transform,false);pivot.localPosition=new Vector3(0,.36f,z);var spoke=GameObject.CreatePrimitive(PrimitiveType.Cube);spoke.transform.SetParent(pivot,false);spoke.transform.localScale=new Vector3(.06f,.59f,.025f);spoke.GetComponent<Renderer>().sharedMaterial=tire;UnityEngine.Object.DestroyImmediate(spoke.GetComponent<Collider>());}
            living.bicycle=Save(cycle,"Cyclist");
            var kart=Model("CarKit","race",carMat,2,false);Wheels(kart,.62f,.62f,.23f,tire);var kartAsset=Save(kart,"Pizza pocket kart");
            var mower=Mower(carMat,tire);var mowerAsset=Save(mower,"Pizza garden mower");
            if(town.Runner.GetComponent<TownTrafficBumper>()==null)town.Runner.gameObject.AddComponent<TownTrafficBumper>();
            var rides=town.Runner.GetComponent<TownRideSelector>();if(rides==null)rides=town.Runner.gameObject.AddComponent<TownRideSelector>();
            rides.rides=new[]{new TownRideProfile("Shopping cart",8,14,7,100,null),new TownRideProfile("Pocket pizza kart",9,17,9,115,kartAsset),new TownRideProfile("Pizza garden mower",6,11,5,80,mowerAsset)};
            EditorUtility.SetDirty(living);EditorUtility.SetDirty(rides);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(town.gameObject.scene);EditorSceneManager.SaveScene(town.gameObject.scene);
            Debug.Log("Living town installed: 11 house variants, two tree variants, six citizens, three traffic vehicles, cyclists, kart and garden tractor.");
        }
    }
}
