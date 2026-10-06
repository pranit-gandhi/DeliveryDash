using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeliveryDash.Downhill
{
    // Runtime dressing follows the same world-space course. It never changes gameplay RNG.
    public static class DownhillTown
    {
        static Material[] plaster;
        static Material roof, stone, recess, trim, green, red, cream, wood, hillNear, hillFar;
        public static GameObject Build(CourseGraph course, int seed)
        {
            Materials();
            var district = new GameObject("Terraced market street");
            var rng = new System.Random(seed ^ 0x527a);
            Transform cluster = null;
            int count = Mathf.CeilToInt(course.Length / 23f);
            for (int i = 0; i < count; i++)
            {
                if (i % 12 == 0) { if (cluster != null) Combine(cluster); cluster = new GameObject("Street block " + i / 12).transform; cluster.SetParent(district.transform, false); }
                CourseSample sample = course.Sample(10f + i * 23f);
                int side = i % 2 == 0 ? -1 : 1;
                Vector3 right = Vector3.Cross(Vector3.up, sample.Forward).normalized;
                Vector3 position = sample.Position + right * side * (sample.Width * .5f + 5.5f);
                CourseSample nearest = course.Closest(position);
                if (Vector3.Distance(new Vector3(position.x, 0, position.z), new Vector3(nearest.Position.x, 0, nearest.Position.z)) < nearest.Width * .5f + 2.8f) continue;
                var house = new GameObject("Market house " + i).transform;
                house.SetParent(cluster, false);
                house.position = position;
                house.rotation = Quaternion.LookRotation(-side * right, Vector3.up);
                int variant = rng.Next(6);
                float[] widths = { 7.8f,5.8f,4.4f,9.2f,6.6f,5.5f };
                float[] heights = { 3.5f,6.2f,7.0f,3.7f,5.7f,3.9f };
                float w = widths[variant] + (float)rng.NextDouble() * .9f;
                float h = heights[variant] + (float)rng.NextDouble() * .55f;
                float depth = 4.8f + (float)rng.NextDouble() * 1.1f;
                GroundFoundation(course,house,w*(variant==1||variant==4||variant==5?1.65f:1f),depth);
                if (variant == 3)
                {
                    Box(house,new Vector3(0,h*.5f,-depth*.16f),new Vector3(w,h,depth*.68f),plaster[(i+variant)%plaster.Length]);
                    for(int bay=0;bay<4;bay++) Box(house,new Vector3((bay-1.5f)*w/3.4f,1.2f,depth*.45f),new Vector3(.28f,2.4f,.46f),stone);
                    Box(house,new Vector3(0,2.5f,depth*.45f),new Vector3(w,.36f,.54f),trim);
                }
                else Box(house, new Vector3(0, h * .5f, 0), new Vector3(w, h, depth), plaster[(i+variant) % plaster.Length]);
                if(variant==0||variant==3||variant==4) HipRoof(house,w+.6f,depth+.7f,h,variant==4?1.4f:.9f);
                else Roof(house, w + .6f, depth + .7f, h, variant == 2 ? 1.75f : 1.1f);
                if (variant == 1 || variant == 4 || variant == 5)
                {
                    float wingHeight = h * .68f;
                    Box(house, new Vector3(w * .46f, wingHeight * .5f, -.4f), new Vector3(w * .7f, wingHeight, depth * .86f), plaster[(i + 1) % plaster.Length]);
                    var wing = new GameObject("Stepped roof").transform; wing.SetParent(house, false); wing.localPosition = new Vector3(w * .46f, 0, -.4f);
                    Roof(wing, w * .7f + .4f, depth * .86f + .6f, wingHeight, .8f);
                }
                float front = depth * .5f + .02f;
                int columns = variant == 2 || variant == 5 ? 2 : 3;
                int floors = h > 5.4f ? 2 : 1;
                for (int floor = 0; floor < floors; floor++) for (int col = 0; col < columns; col++)
                {
                    float x = (col - (columns - 1) * .5f) * w / (columns + .5f);
                    Vector3 p = new Vector3(x, 2.6f + floor * 2.3f, front);
                    Box(house, p, new Vector3(.68f, 1.16f, .09f), recess);
                    for(int edge=-1;edge<=1;edge+=2) Box(house,p+new Vector3(edge*.39f,0,.09f),new Vector3(.10f,1.32f,.22f),trim);
                    Box(house,p+new Vector3(0,.64f,.08f),new Vector3(.85f,.1f,.20f),trim);
                    Box(house, p + new Vector3(0, -.65f, .14f), new Vector3(.95f, .13f, .32f), trim);
                    for (int s = -1; s <= 1; s += 2) Box(house, p + new Vector3(s * .51f, 0, .09f), new Vector3(.25f, 1.21f, .14f), green);
                }
                float doorFront=variant==3?front-depth*.20f:front;
                Box(house, new Vector3(-w * .2f, 1.04f, doorFront), new Vector3(1.15f, 2.08f, .10f), recess);
                Box(house, new Vector3(-w * .2f, .08f, front + .14f), new Vector3(1.4f, .16f, .48f), trim);
                for(int step=0;step<4;step++)
                {
                    Vector3 p=new Vector3(-w*.2f,0,front+.35f+step*.24f);
                    float bottom=course.Closest(house.TransformPoint(p)).Position.y-.95f-house.position.y;
                    float top=-step*.18f;
                    Box(house,new Vector3(p.x,(bottom+top)*.5f,p.z),new Vector3(1.5f,Mathf.Max(.12f,top-bottom),.38f),stone);
                }
                if (variant == 0 || variant == 3 || variant == 4)
                {
                    var canopy = new GameObject("Striped shop awning").transform; canopy.SetParent(house, false); canopy.localPosition = new Vector3(0, 2.05f, front + .64f); canopy.localRotation = Quaternion.Euler(12f, 0, 0);
                    for (int stripe = 0; stripe < 10; stripe++) Box(canopy, new Vector3((stripe - 4.5f) * w / 10, 0, 0), new Vector3(w / 10, .10f, 1.52f), stripe % 2 == 0 ? red : cream);
                }
                if (variant == 1 || variant == 4)
                {
                    float balcony=h*.64f;
                    Box(house, new Vector3(w * .22f, balcony, front + .40f), new Vector3(w * .55f, .18f, 1.1f), trim);
                    for (int rail = 0; rail < 6; rail++) Box(house, new Vector3(w * .22f + (rail - 2.5f) * w * .1f, balcony+.37f, front + .95f), new Vector3(.055f, .7f, .055f), green);
                    Box(house, new Vector3(w * .22f, balcony+.72f, front + .95f), new Vector3(w * .6f, .055f, .055f), green);
                }
                if (variant == 2) Box(house, new Vector3(-w * .38f, h + .8f, -.8f), new Vector3(.65f, 1.7f, .65f), stone);
            }
            if (cluster != null) Combine(cluster);
            // The continuous backdrop owns the hills and district foundations.
            ForkLandmarks(course,district.transform);
            Destination(course, district.transform);
            return district;
        }

        static void GroundFoundation(CourseGraph course,Transform house,float width,float depth)
        {
            float low=float.MaxValue,high=float.MinValue;
            for(int x=-1;x<=1;x+=2) for(int z=-1;z<=1;z+=2)
            {
                Vector3 corner=house.TransformPoint(new Vector3(x*width*.5f,0,z*depth*.5f));
                float ground=course.Closest(corner).Position.y-.9f;
                low=Mathf.Min(low,ground);high=Mathf.Max(high,ground);
            }
            Vector3 position=house.position;position.y=high+.14f;house.position=position;
            float bottom=low-house.position.y-.35f;
            Box(house,new Vector3(0,(bottom+.06f)*.5f,0),new Vector3(width+.6f,.06f-bottom,depth+.45f),stone);
        }

        static void Hills(CourseGraph course,Transform parent)
        {
            var landscape=new GameObject("Distant shaped hills").transform;landscape.SetParent(parent,false);
            int count=Mathf.CeilToInt(course.Length/240f)+2;
            for(int i=0;i<count;i++) for(int side=-1;side<=1;side+=2)
            {
                float station=Mathf.Min(course.Length,90+i*240);
                CourseSample road=course.Sample(station);
                Vector3 center=road.Position+new Vector3(side*(135+(i%3)*17),-12,110);
                const int ringCount=12;
                var vertices=new List<Vector3>();var triangles=new List<int>();
                float height=35+(i%3)*8;
                for(int ring=0;ring<3;ring++) for(int point=0;point<ringCount;point++)
                {
                    float angle=point*Mathf.PI*2/ringCount;
                    float radius=ring==0?1f:ring==1?.68f:.22f;
                    float ripple=1+.12f*Mathf.Sin(point*2.1f+i*.7f);
                    vertices.Add(center+new Vector3(Mathf.Cos(angle)*76*radius*ripple,
                        ring==0?-8:ring==1?height*.32f:height*.82f,Mathf.Sin(angle)*110*radius*ripple));
                }
                vertices.Add(center+new Vector3(-5,height,12));
                for(int ring=0;ring<2;ring++)for(int point=0;point<ringCount;point++)
                {
                    int a=ring*ringCount+point,b=ring*ringCount+(point+1)%ringCount,c=a+ringCount,d=b+ringCount;
                    triangles.AddRange(new[]{a,c,b,b,c,d});
                }
                for(int point=0;point<ringCount;point++)triangles.AddRange(new[]{24+point,36,24+(point+1)%ringCount});
                var mesh=new Mesh{name="Town hill silhouette"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
                var hill=new GameObject("Layered hillside "+i+" "+side);hill.transform.SetParent(landscape,false);
                hill.AddComponent<MeshFilter>().sharedMesh=mesh;hill.AddComponent<MeshRenderer>().sharedMaterial=i%2==0?hillNear:hillFar;
            }
        }

        static void ForkLandmarks(CourseGraph course,Transform parent)
        {
            foreach(CourseBranch branch in course.Branches)
            {
                float station=branch.StartDistance+22;
                CourseSample safe=course.Sample(station),shortcut=course.Sample(station,course.Branches.IndexOf(branch)+1);
                float outer=Mathf.Min(CourseGraph.RoadEdge(safe,-1).x,CourseGraph.RoadEdge(shortcut,-1).x)-6;
                var kiosk=new GameObject("Fork loading kiosk").transform;kiosk.SetParent(parent,false);
                kiosk.position=new Vector3(outer,Mathf.Min(safe.Position.y,shortcut.Position.y),station);
                kiosk.rotation=Quaternion.LookRotation(Vector3.right,Vector3.up);
                GroundFoundation(course,kiosk,3.2f,3.4f);
                Box(kiosk,new Vector3(0,1.05f,0),new Vector3(3.1f,2.1f,3.2f),plaster[2]);
                Box(kiosk,new Vector3(0,1.22f,1.63f),new Vector3(2.3f,1.3f,.06f),recess);
                Box(kiosk,new Vector3(0,.58f,1.92f),new Vector3(2.6f,.16f,.68f),wood);
                HipRoof(kiosk,3.8f,3.9f,2.1f,1.05f);
                for(int stripe=0;stripe<6;stripe++)Box(kiosk,new Vector3((stripe-2.5f)*.5f,1.96f,2),new Vector3(.5f,.10f,1.10f),stripe%2==0?green:cream);
                Box(kiosk,new Vector3(1.2f,3.6f,.3f),new Vector3(.13f,2.2f,.13f),wood);
                Box(kiosk,new Vector3(1.72f,4.48f,.3f),new Vector3(1.1f,.52f,.10f),red);
                Combine(kiosk);
            }
        }

        static void HipRoof(Transform parent,float width,float depth,float y,float rise)
        {
            var mesh=new Mesh{name="Shaped pitched roof"};
            mesh.vertices=new[]{new Vector3(-width*.5f,y,-depth*.5f),new Vector3(width*.5f,y,-depth*.5f),
                new Vector3(-width*.5f,y,depth*.5f),new Vector3(width*.5f,y,depth*.5f),
                new Vector3(0,y+rise,-depth*.22f),new Vector3(0,y+rise,depth*.22f)};
            mesh.triangles=new[]{0,4,1,2,3,5,0,2,5,0,5,4,1,4,5,1,5,3};mesh.RecalculateNormals();mesh.RecalculateBounds();
            var go=new GameObject("Hipped terracotta roof");go.transform.SetParent(parent,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=roof;
        }

        static void Destination(CourseGraph course, Transform parent)
        {
            var end = course.Sample(course.Length - 4f);
            var goal = new GameObject("Customer destination").transform;
            goal.SetParent(parent, false); goal.position = end.Position; goal.rotation = Quaternion.LookRotation(end.Forward, Vector3.up);
            Box(goal, new Vector3(0, .015f, 0), new Vector3(end.Width, .04f, 3.2f), cream);
            Box(goal, new Vector3(end.Width * .5f + 1.5f, 1.7f, 0), new Vector3(3, 3.4f, 3f), plaster[0]);
            Box(goal, new Vector3(end.Width * .5f + .0f, 1.2f, -.02f), new Vector3(.08f, 2.4f, 1.1f), green);
            Roof(goal, 3.6f, 3.6f, 3.4f, .8f, new Vector3(end.Width * .5f + 1.5f, 0, 0));
            // Customer silhouette with a visible raised hand, grounded beside the finish.
            var customer = new GameObject("Waiting customer").transform; customer.SetParent(goal, false); customer.localPosition = new Vector3(end.Width * .5f - .6f, .15f, 1.2f);
            Box(customer, new Vector3(0, .96f, 0), new Vector3(.47f, .7f, .27f), green);
            Box(customer, new Vector3(0, 1.48f, 0), new Vector3(.28f, .32f, .26f), cream);
            for (int s = -1; s <= 1; s += 2) Box(customer, new Vector3(s * .13f, .37f, 0), new Vector3(.17f, .7f, .19f), recess);
            Box(customer, new Vector3(-.34f, 1.48f, 0), new Vector3(.16f, .7f, .16f), cream);
            Combine(goal);
        }
        static void Materials()
        {
            if (roof != null) return;
            plaster = new[] { Mat("Peach limewash", "#CE9877"), Mat("Warm chalk", "#D4C8A4"), Mat("Ochre plaster", "#C9AB7B"), Mat("Rose limewash", "#B88370") };
            roof = Mat("Terracotta roofs", "#964D36"); stone = Mat("Stone foundations", "#9A9B80"); recess = Mat("Recessed openings", "#263C3D"); trim = Mat("Limestone trim", "#DFD0AC");
            green = Mat("Deep teal shutters", "#315C54"); red = Mat("Canvas red", "#AF4435"); cream = Mat("Canvas cream", "#E3D6B6"); wood = Mat("Market timber", "#795233");
            hillNear=Mat("Olive distant hills","#74856A");hillFar=Mat("Muted distant hills","#909B84");
        }
        static Material Mat(string name, string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color color);
            var material = new Material(Shader.Find("Standard")) { name = name, color = color, enableInstancing = true };
            material.SetFloat("_Glossiness", .06f); return material;
        }
        static void Box(Transform root, Vector3 p, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.transform.SetParent(root, false); go.transform.localPosition = p; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = mat;
            go.GetComponent<Collider>().enabled = false;
        }
        static void Roof(Transform root, float w, float d, float y, float rise, Vector3 offset = default)
        {
            var mesh = new Mesh { name = "Shaped pitched roof" };
            mesh.vertices = new[] { new Vector3(-w*.5f,y,-d*.5f),new Vector3(w*.5f,y,-d*.5f),new Vector3(0,y+rise,-d*.5f),new Vector3(-w*.5f,y,d*.5f),new Vector3(w*.5f,y,d*.5f),new Vector3(0,y+rise,d*.5f) };
            mesh.triangles = new[] { 0,2,1,3,4,5,0,3,5,0,5,2,2,5,4,2,4,1,0,1,4,0,4,3 };
            mesh.RecalculateNormals();
            var go = new GameObject("Roof with ridge"); go.transform.SetParent(root,false); go.transform.localPosition=offset; go.AddComponent<MeshFilter>().sharedMesh=mesh; go.AddComponent<MeshRenderer>().sharedMaterial=roof;
        }
        static void Combine(Transform root)
        {
            var groups = new Dictionary<Material,List<CombineInstance>>();
            var filters = root.GetComponentsInChildren<MeshFilter>();
            foreach (var filter in filters)
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null) continue;
                Material mat = renderer.sharedMaterial;
                if (!groups.TryGetValue(mat,out var list)) { list=new List<CombineInstance>(); groups.Add(mat,list); }
                list.Add(new CombineInstance { mesh=filter.sharedMesh, transform=root.worldToLocalMatrix*filter.transform.localToWorldMatrix });
                renderer.enabled=false;
            }
            foreach (var group in groups)
            {
                var mesh=new Mesh { name="Street block "+group.Key.name,indexFormat=IndexFormat.UInt32 };
                mesh.CombineMeshes(group.Value.ToArray(),true,true);
                var go=new GameObject(group.Key.name); go.transform.SetParent(root,false); go.AddComponent<MeshFilter>().sharedMesh=mesh; go.AddComponent<MeshRenderer>().sharedMaterial=group.Key;
            }
            // Keep structural transforms, but discard the thousands of primitive render objects.
            foreach (var filter in filters)
            {
                if (Application.isPlaying)
                {
                    if (filter.sharedMesh != null && filter.sharedMesh.name == "Shaped pitched roof") Object.Destroy(filter.sharedMesh);
                    Object.Destroy(filter.gameObject);
                }
                else Object.DestroyImmediate(filter.gameObject);
            }
        }
    }
}
