using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeliveryDash.Editor
{
    public static class CourierVisualBuilder
    {
        const string BasePath = "Assets/DeliveryDash/ThirdParty/Quaternius/";
        const string MeshPath = "Assets/DeliveryDash/Art/Generated/";
        public static Transform Build(Transform parent, out Transform pizzaPivot)
        {
            Directory.CreateDirectory(MeshPath);
            var courier = new GameObject("Courier seated in basket").transform;
            courier.SetParent(parent, false);
            courier.localPosition = new Vector3(0f, 0f, -.14f);
            string sourcePath=BasePath+"CourierBase.fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(sourcePath);
            if(!importer.isReadable){importer.isReadable=true;importer.SaveAndReimport();}
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            model.name = "Courier anatomy";
            model.transform.SetParent(courier, false);
            model.transform.localPosition = new Vector3(0f, -.32f, -.20f);
            model.transform.localRotation = Quaternion.Euler(0f,180f,0f);
            model.transform.localScale = Vector3.one * 1.10f;
            var skin = Material("Courier skin atlas", Color.white, BasePath+"CourierBaseColor.png");
            SkinnedMeshRenderer body=null;
            foreach(var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                renderer.updateWhenOffscreen=true;
                if(renderer.name=="SuperHero_Male") { renderer.sharedMaterial=skin; body=renderer; }
            }
            var red=Material("Courier crimson shirt", new Color(.56f,.055f,.04f));
            var black=Material("Courier charcoal sleeves",new Color(.065f,.07f,.078f));
            var tan=Material("Courier canvas shorts",new Color(.065f,.071f,.080f));
            SkinnedGarmentBuilder.Build(body,red,black,tan);

            var pelvis=Find(model.transform,"pelvis");
            pelvis.rotation=Quaternion.Euler(9f,0f,-5f)*pelvis.rotation;
            // Flexion is applied to the actual two-bone chains, keeping their lengths.
            PoseLimb(model.transform,"thigh_l","calf_l","foot_l",courier.TransformPoint(new Vector3(-.24f,.73f,.27f)),courier.TransformPoint(new Vector3(-.28f,.37f,.34f)));
            PoseLimb(model.transform,"thigh_r","calf_r","foot_r",courier.TransformPoint(new Vector3(.30f,.78f,.26f)),courier.TransformPoint(new Vector3(.40f,.42f,.36f)));
            // BuildDownhill scales the cart shell separately. Its left red grip is
            // at roughly (-.59, 1.01, -.76) in the shared suspension frame.
            PoseLimb(model.transform,"upperarm_l","lowerarm_l","hand_l",courier.TransformPoint(new Vector3(-.47f,1.07f,-.25f)),courier.TransformPoint(new Vector3(-.59f,1.01f,-.62f)));
            PoseLimb(model.transform,"upperarm_r","lowerarm_r","hand_r",courier.TransformPoint(new Vector3(.47f,1.02f,-.06f)),courier.TransformPoint(new Vector3(.69f,1.30f,-.05f)));

            var left=Find(model.transform,"hand_l");
            // Local +Y follows the fingers. Curl around the metal rail.
            left.rotation=Quaternion.LookRotation(Vector3.right,Vector3.back);
            foreach(string finger in new[]{"index","middle","ring","pinky"})
            {
                string[] joints={"_01_l","_02_l","_03_l","_04_leaf_l"};
                Vector3[] bend={new Vector3(0,-.65f,-.76f),new Vector3(0,-1f,.05f),new Vector3(0,-.4f,.92f)};
                for(int i=0;i<3;i++)
                {
                    var f=Find(model.transform,finger+joints[i]); var child=Find(model.transform,finger+joints[i+1]);
                    f.rotation=Quaternion.FromToRotation(child.position-f.position,bend[i])*f.rotation;
                }
            }
            var right=Find(model.transform,"hand_r");
            Vector3 fingerDirection=new Vector3(.76f,.64f,.12f).normalized;
            Vector3 palmNormal=Vector3.ProjectOnPlane(Vector3.up,fingerDirection).normalized;
            right.rotation=Quaternion.LookRotation(Vector3.Cross(palmNormal,fingerDirection),fingerDirection);
            foreach(string finger in new[]{"index","middle","ring","pinky"})
                for(int i=2;i<=3;i++)
                {
                    var f=Find(model.transform,finger+"_0"+i+"_r");
                    if(f!=null) f.localRotation*=Quaternion.Euler(0,0, i==2?-10:18);
                }

            var capRed=Material("Courier cap",new Color(.65f,.055f,.035f));
            var dark=Material("Courier cap underside",new Color(.34f,.035f,.02f));
            var head=Find(model.transform,"Head");
            BuildHair(model.transform,head,courier);
            BuildCap(head,courier,capRed,dark);
            var sole=Material("Shoe warm ivory rubber",new Color(.62f,.59f,.51f));
            foreach(var side in new[]{"l","r"})
            {
                var foot=Find(model.transform,"foot_"+side);
                var ball=Find(model.transform,"ball_"+side); foot.rotation=Quaternion.FromToRotation(ball.position-foot.position,new Vector3(0,-.035f,.18f))*foot.rotation;
                var shoe=new GameObject("Red canvas sneaker "+side).transform;
                shoe.SetParent(courier,false);
                shoe.position=foot.position+new Vector3(0,-.02f,.015f);
                shoe.rotation=courier.rotation*Quaternion.Euler(0,side=="l"?-12:15,0);
                var shoeMesh=ShoeMesh("CourierSneaker",1f);
                MeshObject("Canvas sneaker upper",shoe,shoeMesh,capRed);
                var outsole=MeshObject("Raised rubber sole",shoe,ShoeMesh("CourierSole",.32f),sole);
                outsole.transform.localPosition=new Vector3(0,-.047f,0);
                for(int i=0;i<4;i++)
                    Tube("Cotton lace",shoe,new Vector3(-.06f,.079f,.04f+i*.029f),new Vector3(.06f,.079f,.05f+i*.029f),.007f,sole);
                shoe.SetParent(foot,true);
            }
            var cardboard=Material("Warm cardboard",new Color(.88f,.77f,.58f));
            pizzaPivot=new GameObject("Pizza supported at fingertips").transform;
            pizzaPivot.SetParent(right,true);
            Vector3 support=Vector3.zero;
            foreach(string finger in new[]{"index","middle","ring"}) support+=Find(model.transform,finger+"_03_r").position;
            support/=3f;
            pizzaPivot.position=support+Vector3.up*.016f;
            pizzaPivot.rotation=courier.rotation;
            PizzaBox(pizzaPivot,cardboard);
            return courier;
        }

        static void PoseLimb(Transform root,string a,string b,string c,Vector3 elbow,Vector3 end)
        {
            var upper=Find(root,a);var lower=Find(root,b);var hand=Find(root,c);
            float l1=Vector3.Distance(upper.position,lower.position), l2=Vector3.Distance(lower.position,hand.position);
            Vector3 delta=end-upper.position;float distance=Mathf.Clamp(delta.magnitude,.02f,l1+l2-.005f);
            Vector3 direction=delta.normalized;
            float along=(l1*l1-l2*l2+distance*distance)/(2*distance);
            float height=Mathf.Sqrt(Mathf.Max(0,l1*l1-along*along));
            Vector3 bend=Vector3.ProjectOnPlane(elbow-upper.position,direction).normalized;
            Vector3 joint=upper.position+direction*along+bend*height;
            upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,joint-upper.position)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,end-lower.position)*lower.rotation;
        }
        public static Transform Find(Transform root,string name)
        { foreach(var t in root.GetComponentsInChildren<Transform>(true)) if(t.name==name)return t;return null; }

        static void BuildHair(Transform model,Transform head,Transform courier)
        {
            var root=new GameObject("Sculpted blond nape locks").transform;
            root.SetParent(courier,false);root.position=head.position;root.rotation=courier.rotation;
            var blond=Material("Courier golden hair",new Color(.72f,.48f,.17f));
            var v=new List<Vector3>();var t=new List<int>();
            // Broad, overlapping locks follow the skull and turn at the tips.
            for(int lockIndex=0;lockIndex<9;lockIndex++)
            {
                float angle=Mathf.PI*(1.03f+lockIndex*.117f);
                Vector3 outward=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                Vector3 tangent=new Vector3(-outward.z,0,outward.x);
                int start=v.Count;
                for(int ring=0;ring<7;ring++)
                {
                    float u=ring/6f;
                    Vector3 center=outward*(.144f+.022f*Mathf.Sin(u*Mathf.PI)) + Vector3.up*(.181f-.105f*u);
                    center+=tangent*(.014f*u*u);
                    float width=.049f*Mathf.Sin((u*.86f+.12f)*Mathf.PI);
                    if(ring==6)width=.002f;
                    for(int j=0;j<8;j++)
                    {
                        float a=j*Mathf.PI/4;
                        v.Add(center+tangent*(Mathf.Cos(a)*width)+outward*(Mathf.Sin(a)*.017f*(1-u*.65f)));
                        if(ring>0){int p=start+(ring-1)*8+j,q=start+(ring-1)*8+(j+1)%8,r=start+ring*8+j,n=start+ring*8+(j+1)%8;t.AddRange(new[]{p,r,n,p,n,q});}
                    }
                }
            }
            MeshObject("Blond locks below cap",root,SaveMesh("CourierNapeHair",v,t),blond);
            root.SetParent(head,true);
            root.localScale *= .90f;
        }
        static void BuildCap(Transform head,Transform courier,Material red,Material dark)
        {
            var root=new GameObject("Backward red baseball cap").transform;
            root.SetParent(courier,false);
            root.position=head.position+new Vector3(0,.175f,0);
            root.rotation=courier.rotation;
            var vertices=new List<Vector3>();var triangles=new List<int>();
            const int sides=48,rings=10;
            for(int i=0;i<=rings;i++)
            {
                float angle=i/(float)rings*Mathf.PI*.5f;
                for(int j=0;j<sides;j++)
                {
                    float a=j*Mathf.PI*2/sides;
                    vertices.Add(new Vector3(Mathf.Cos(a)*.167f*Mathf.Cos(angle),Mathf.Sin(angle)*.155f,Mathf.Sin(a)*.176f*Mathf.Cos(angle)));
                    if(i>0){int p=(i-1)*sides+j,q=(i-1)*sides+(j+1)%sides,r=i*sides+j,s=i*sides+(j+1)%sides;triangles.AddRange(new[]{p,r,s,p,s,q});}
                }
            }
            MeshObject("Six sewn panels",root,SaveMesh("CourierCap",vertices,triangles),red);
            // The visor points backward, with a rounded plan and a shallow curve.
            vertices.Clear();triangles.Clear();
            for(int i=0;i<=24;i++)
            {
                float a=Mathf.PI*i/24f;
                float x=Mathf.Cos(a)*.155f;
                vertices.Add(new Vector3(x,.01f-Mathf.Abs(x)*.07f,-.10f));
                vertices.Add(new Vector3(x,.005f-Mathf.Abs(x)*.09f,-.14f-Mathf.Sin(a)*.155f));
                if(i>0){int n=i*2;triangles.AddRange(new[]{n-2,n-1,n+1,n-2,n+1,n});}
            }
            MeshObject("Curved backward visor",root,SaveMesh("CourierVisor",vertices,triangles),red);
            for(int panel=0;panel<6;panel++)
            {
                float a=panel*Mathf.PI/3;
                Vector3 last=new Vector3(Mathf.Cos(a)*.168f,.004f,Mathf.Sin(a)*.177f);
                for(int i=1;i<=8;i++)
                {
                    float b=i/8f*Mathf.PI*.48f;
                    Vector3 next=new Vector3(Mathf.Cos(a)*.168f*Mathf.Cos(b),Mathf.Sin(b)*.156f,Mathf.Sin(a)*.177f*Mathf.Cos(b));
                    Tube("Cap panel seam",root,last,next,.0008f,dark);last=next;
                }
            }
            root.SetParent(head,true);
        }
        static Mesh ShoeMesh(string name,float height)
        {
            var v=new List<Vector3>();var t=new List<int>();
            float[] z={-.12f,-.10f,.02f,.16f,.25f,.285f};
            float[] widths={.05f,.085f,.097f,.09f,.074f,.02f};
            float[] heights={.04f,.10f,.09f,.075f,.048f,.012f};
            for(int r=0;r<z.Length;r++) for(int j=0;j<20;j++)
            {
                float a=j*Mathf.PI*2/20;
                v.Add(new Vector3(Mathf.Cos(a)*widths[r],Mathf.Sin(a)*heights[r]*height,z[r]));
                if(r>0){int p=(r-1)*20+j,q=(r-1)*20+(j+1)%20,n=r*20+j,m=r*20+(j+1)%20;t.AddRange(new[]{p,m,n,p,q,m});}
            }
            return SaveMesh(name,v,t);
        }
        static void PizzaBox(Transform parent,Material mat)
        {
            Box("Corrugated pizza base",parent,new Vector3(0,.018f,0),new Vector3(.63f,.036f,.63f),mat);
            Box("Closed lid",parent,new Vector3(0,.056f,-.005f),new Vector3(.641f,.024f,.63f),mat);
            var ink=Material("Pizza red lid ink",new Color(.62f,.10f,.055f));
            Box("Pizza lid mark",parent,new Vector3(0,.069f,0),new Vector3(.28f,.003f,.25f),ink);
            var fold=Material("Cardboard cut edges",new Color(.57f,.42f,.27f));
            for(int s=-1;s<=1;s+=2)
                Box("Folded lid edge",parent,new Vector3(s*.317f,.032f,0),new Vector3(.012f,.025f,.625f),fold);
            Box("Front lid tab",parent,new Vector3(0,.022f,-.32f),new Vector3(.105f,.025f,.009f),mat);
        }
        static GameObject Box(string name,Transform parent,Vector3 p,Vector3 size,Material mat)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
        static void Tube(string name,Transform parent,Vector3 a,Vector3 b,float radius,Material mat)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=(a+b)*.5f;go.transform.localScale=new Vector3(radius*2,(b-a).magnitude*.5f,radius*2);go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);go.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(go.GetComponent<Collider>());
        }
        static GameObject MeshObject(string name,Transform parent,Mesh mesh,Material material)
        {var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;return go;}
        static Mesh SaveMesh(string name,List<Vector3> v,List<int> t)
        {
            var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            string path=MeshPath+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing!=null){EditorUtility.CopySerialized(mesh,existing);EditorUtility.SetDirty(existing);Object.DestroyImmediate(mesh);return existing;}
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static Material Material(string name,Color color,string texture=null)
        {
            string path="Assets/DeliveryDash/Art/"+name.Replace(' ','_')+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(mat,path);}
            mat.name=name;mat.color=color;mat.mainTexture=texture==null?null:AssetDatabase.LoadAssetAtPath<Texture2D>(texture);mat.SetFloat("_Glossiness",.09f);EditorUtility.SetDirty(mat);return mat;
        }
    }
}


