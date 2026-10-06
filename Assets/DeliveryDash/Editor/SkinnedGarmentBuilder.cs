using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DeliveryDash.Editor
{
    /// <summary>
    /// Tailored cloth follows the courier rig, with eased surfaces and sewn edges.
    /// Cuts interpolate skin weights so cuffs stay clean when the limbs bend.
    /// </summary>
    public static class SkinnedGarmentBuilder
    {
        const string MeshFolder = "Assets/DeliveryDash/Art/Generated";
        enum Garment { Shirt, Sleeves, Shorts }

        struct ClothVertex
        {
            public Vector3 position;
            public Vector3 normal;
            public BoneWeight weight;
            public ClothVertex(Vector3 p, Vector3 n, BoneWeight w)
            { position = p; normal = n; weight = w; }
        }

        public static void Build(SkinnedMeshRenderer body, Material shirt, Material sleeve, Material shorts)
        {
            if (body == null || body.sharedMesh == null || body.bones == null || body.bones.Length == 0)
            { Debug.LogError("Courier garments need a skinned body mesh and bones."); return; }
            Mesh source = body.sharedMesh;
            if(!source.isReadable||source.vertices.Length==0||source.normals.Length!=source.vertexCount||source.triangles.Length==0)
                throw new InvalidOperationException("Courier garment source must contain readable full body geometry");
            if (source.boneWeights.Length != source.vertexCount)
            { Debug.LogError("Courier body mesh has no usable skin weights."); return; }
            var bones = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < body.bones.Length; i++)
                if (body.bones[i] != null) bones[body.bones[i].name] = i;
            Vector3[] smooth = SmoothAnatomy(source);
            BuildPart(body, source, smooth, bones, Garment.Shirt, "Tailored red tee", "Courier_Tee.asset", shirt);
            BuildPart(body, source, smooth, bones, Garment.Sleeves, "Black half sleeves", "Courier_HalfSleeves.asset", sleeve);
            BuildPart(body, source, smooth, bones, Garment.Shorts, "Black cuffed shorts", "Courier_Shorts.asset", shorts);
            MaskCoveredSkin(body, source, bones);
        }

        static void MaskCoveredSkin(SkinnedMeshRenderer body, Mesh source, Dictionary<string, int> bones)
        {
            Vector3[] positions = source.vertices;
            BoneWeight[] weights = source.boneWeights;
            int[] original = source.triangles;
            var visible = new List<int>(original.Length);
            for (int i = 0; i < original.Length; i += 3)
            {
                int a = original[i], b = original[i + 1], c = original[i + 2];
                Vector3 center = (positions[a] + positions[b] + positions[c]) / 3f;
                float torso = (Influence(weights[a], bones, "pelvis", "spine_01", "spine_02", "spine_03") +
                    Influence(weights[b], bones, "pelvis", "spine_01", "spine_02", "spine_03") +
                    Influence(weights[c], bones, "pelvis", "spine_01", "spine_02", "spine_03")) / 3f;
                float upperArm = (Influence(weights[a], bones, "upperarm_l", "upperarm_r") +
                    Influence(weights[b], bones, "upperarm_l", "upperarm_r") +
                    Influence(weights[c], bones, "upperarm_l", "upperarm_r")) / 3f;
                float thigh = (Influence(weights[a], bones, "thigh_l", "thigh_r") +
                    Influence(weights[b], bones, "thigh_l", "thigh_r") +
                    Influence(weights[c], bones, "thigh_l", "thigh_r")) / 3f;
                bool shirtCovered = center.z > 1.015f && center.z < 1.535f &&
                    Mathf.Abs(center.x) < .32f && torso > .35f;
                bool sleeveCovered = center.z > 1.31f && center.z < 1.59f &&
                    Mathf.Abs(center.x) > .20f && Mathf.Abs(center.x) < .45f && upperArm > .42f;
                bool shortsCovered = center.z > .73f && center.z < 1.055f &&
                    Mathf.Abs(center.x) < .29f && (torso + thigh) > .4f;
                if (shirtCovered || sleeveCovered || shortsCovered) continue;
                visible.Add(a); visible.Add(b); visible.Add(c);
            }
            Mesh cut = UnityEngine.Object.Instantiate(source);
            cut.name = "Exposed courier skin";
            cut.SetTriangles(visible, 0);
            cut.RecalculateBounds();
            body.sharedMesh = SaveMesh(cut, "Courier_ExposedSkin.asset");
        }

        // Weld only for smoothing. The imported body has duplicate vertices at UV seams.
        static Vector3[] SmoothAnatomy(Mesh source)
        {
            Vector3[] raw = source.vertices;
            var lookup = new Dictionary<Vector3Int, int>();
            var unique = new List<Vector3>();
            int[] map = new int[raw.Length];
            for (int i = 0; i < raw.Length; i++)
            {
                Vector3Int key = Key(raw[i]);
                if (!lookup.TryGetValue(key, out int id))
                { id = unique.Count; lookup.Add(key, id); unique.Add(raw[i]); }
                map[i] = id;
            }
            var neighbors = new HashSet<int>[unique.Count];
            for (int i = 0; i < neighbors.Length; i++) neighbors[i] = new HashSet<int>();
            int[] tris = source.triangles;
            for (int i = 0; i < tris.Length; i += 3)
                for (int j = 0; j < 3; j++)
                {
                    int a = map[tris[i+j]], b = map[tris[i+(j+1)%3]];
                    if (a != b) { neighbors[a].Add(b); neighbors[b].Add(a); }
                }
            Vector3[] current = unique.ToArray();
            for (int iteration = 0; iteration < 16; iteration++)
            {
                var next = new Vector3[current.Length];
                for (int i = 0; i < current.Length; i++)
                {
                    Vector3 average = Vector3.zero;
                    foreach (int j in neighbors[i]) average += current[j];
                    next[i] = neighbors[i].Count == 0 ? current[i] :
                        Vector3.Lerp(current[i], average / neighbors[i].Count, .40f);
                }
                current = next;
            }
            var result = new Vector3[raw.Length];
            for (int i = 0; i < result.Length; i++) result[i] = current[map[i]];
            return result;
        }

        static void BuildPart(SkinnedMeshRenderer body, Mesh source, Vector3[] smooth,
            Dictionary<string, int> bones, Garment part, string name, string assetName, Material material)
        {
            Vector3[] raw = source.vertices, normals = source.normals;
            BoneWeight[] weights = source.boneWeights;
            int[] sourceTriangles = source.triangles;
            var vertices = new List<Vector3>();
            var skinWeights = new List<BoneWeight>();
            var triangles = new List<int>();
            var welded = new Dictionary<Vector3Int, int>();

            for (int t = 0; t < sourceTriangles.Length; t += 3)
            {
                var polygon = new List<ClothVertex>(6);
                for (int corner = 0; corner < 3; corner++)
                {
                    int id = sourceTriangles[t+corner];
                    polygon.Add(new ClothVertex(raw[id], normals[id], weights[id]));
                }
                // Clip actual triangles, rather than accepting whole triangle centers.
                // This is what removes sawtooth collar and sleeve borders.
                if (part == Garment.Shirt)
                {
                    Clip(polygon, v => v.position.z - 1.005f);
                    Clip(polygon, v => .231f - Mathf.Abs(v.position.x));
                    Clip(polygon, v => CollarHeight(v.position) - v.position.z);
                    Clip(polygon, v => Influence(v.weight, bones, "pelvis", "spine_01", "spine_02", "spine_03", "clavicle_l", "clavicle_r") - .16f);
                }
                else if (part == Garment.Sleeves)
                {
                    Clip(polygon, v => Mathf.Abs(v.position.x) - .218f);
                    Clip(polygon, v => .442f - Mathf.Abs(v.position.x));
                    Clip(polygon, v => v.position.z - 1.275f);
                    Clip(polygon, v => 1.62f - v.position.z);
                    Clip(polygon, v => Influence(v.weight, bones, "upperarm_l", "upperarm_r", "clavicle_l", "clavicle_r") - .18f);
                }
                else
                {
                    Clip(polygon, v => v.position.z - .690f);
                    Clip(polygon, v => 1.064f - v.position.z);
                    Clip(polygon, v => .272f - Mathf.Abs(v.position.x));
                }
                if (polygon.Count < 3) continue;
                for (int i = 1; i < polygon.Count-1; i++)
                {
                    Add(polygon[0]); Add(polygon[i]); Add(polygon[i+1]);
                }
            }

            if (vertices.Count == 0) throw new InvalidOperationException("No cloth surface for " + name);
            AddFoldedEdges(part, vertices, skinWeights, triangles);
            var garmentMesh = new Mesh { name = name + " sewn skinned cloth" };
            garmentMesh.SetVertices(vertices);
            garmentMesh.SetTriangles(triangles, 0);
            garmentMesh.boneWeights = skinWeights.ToArray();
            garmentMesh.bindposes = source.bindposes;
            garmentMesh.RecalculateNormals();
            garmentMesh.RecalculateBounds();
            Mesh savedMesh = SaveMesh(garmentMesh, assetName);
            var garment = new GameObject(name);
            garment.transform.SetParent(body.transform, false);
            var skin = garment.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = savedMesh;
            skin.sharedMaterial = material;
            skin.bones = body.bones;
            skin.rootBone = body.rootBone;
            skin.quality = body.quality;
            skin.updateWhenOffscreen = body.updateWhenOffscreen;
            skin.shadowCastingMode = body.shadowCastingMode;
            skin.receiveShadows = body.receiveShadows;
            skin.localBounds = savedMesh.bounds;

            void Add(ClothVertex vertex)
            {
                Vector3 p = EaseCloth(vertex, part);
                Vector3Int key = Key(p);
                if (!welded.TryGetValue(key, out int index))
                {
                    index = vertices.Count;
                    welded.Add(key, index);
                    vertices.Add(p);
                    skinWeights.Add(vertex.weight);
                }
                triangles.Add(index);
            }

            Vector3 EaseCloth(ClothVertex vertex, Garment garment)
            {
                Vector3 p = vertex.position;
                // Interpolate the smooth source by nearest vertices. The cloth remains
                // outside the original skin even over the shoulder muscles.
                float best = float.PositiveInfinity;
                int closest = 0;
                for (int i = 0; i < raw.Length; i++)
                {
                    float distance = (raw[i] - p).sqrMagnitude;
                    if (distance < best) { best = distance; closest = i; }
                }
                Vector3 original = p;
                Vector3 cloth = p + (smooth[closest] - raw[closest]) * .62f + vertex.normal.normalized * (garment == Garment.Shirt ? .040f : .035f);
                if (garment == Garment.Shirt)
                {
                    // A relaxed tee hangs from the shoulders. It bridges the body relief
                    // instead of tracing every muscle, while retaining a tapered waist.
                    float chest = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.15f, 1.37f, p.z));
                    float width = Mathf.Lerp(.220f, .260f, chest);
                    float front = Mathf.Lerp(.165f, .205f, chest);
                    float back = Mathf.Lerp(.185f, .225f, chest);
                    float center = .021f;
                    float depth = p.y < center ? front : back;
                    float angle = Mathf.Atan2((p.y-center)/depth, p.x/width);
                    Vector3 drape = new Vector3(Mathf.Cos(angle)*width, center+Mathf.Sin(angle)*depth, p.z);
                    float amount = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.36f, 1.49f, p.z));
                    cloth = Vector3.Lerp(cloth, drape, amount * .90f);
                    // Quiet diagonal tension folds, only a few millimetres deep.
                    float fold = .003f*Mathf.Sin(p.z*41f + p.x*20f) * Mathf.Clamp01((1.36f-p.z)*5f);
                    cloth.y += Mathf.Sign(p.y-center)*fold;
                }
                else if (garment == Garment.Shorts && p.z < .92f)
                {
                    float side = Mathf.Sign(p.x);
                    Vector2 delta = new Vector2(p.x-side*.105f, p.y-.033f);
                    float angle = Mathf.Atan2(delta.y/.116f, delta.x/.101f);
                    Vector3 eased = new Vector3(side*.105f+Mathf.Cos(angle)*.116f,
                        .033f+Mathf.Sin(angle)*.139f, p.z);
                    float lower = 1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.83f,.94f,p.z));
                    cloth = Vector3.Lerp(cloth,eased,lower*.76f);
                }
                // Preserve exact hem and cuff planes after easing.

                if (garment == Garment.Shorts && Mathf.Abs(original.z-.690f)<.0002f) cloth.z=.690f;
                if (garment == Garment.Sleeves && Mathf.Abs(Mathf.Abs(original.x)-.442f)<.0002f) cloth.x=Mathf.Sign(original.x)*.442f;
                if (garment == Garment.Shirt && Mathf.Abs(original.z-1.005f)<.0002f) cloth.z=1.005f;
                return cloth;
            }
        }

        static float CollarHeight(Vector3 p)
        {
            float shoulder = Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.072f,.205f,Mathf.Abs(p.x)));
            return Mathf.Lerp(1.535f,1.604f,shoulder);
        }

        static void Clip(List<ClothVertex> polygon, Func<ClothVertex,float> distance)
        {
            if (polygon.Count==0) return;
            var output=new List<ClothVertex>(polygon.Count+2);
            ClothVertex previous=polygon[polygon.Count-1];
            float dp=distance(previous);
            foreach(ClothVertex current in polygon)
            {
                float dc=distance(current);
                if ((dp>=0f)!=(dc>=0f))
                {
                    float t=Mathf.Clamp01(dp/(dp-dc));
                    output.Add(new ClothVertex(Vector3.Lerp(previous.position,current.position,t),
                        Vector3.Lerp(previous.normal,current.normal,t).normalized,
                        BlendWeights(previous.weight,current.weight,t)));
                }
                if(dc>=0f) output.Add(current);
                previous=current; dp=dc;
            }
            polygon.Clear(); polygon.AddRange(output);
        }

        static BoneWeight BlendWeights(BoneWeight a,BoneWeight b,float t)
        {
            var values=new Dictionary<int,float>();
            Accumulate(a,1f-t);Accumulate(b,t);
            var ids=new List<int>(values.Keys);
            ids.Sort((x,y)=>values[y].CompareTo(values[x]));
            float sum=0f;for(int i=0;i<Mathf.Min(4,ids.Count);i++)sum+=values[ids[i]];
            var result=new BoneWeight();
            if(ids.Count>0){result.boneIndex0=ids[0];result.weight0=values[ids[0]]/sum;}
            if(ids.Count>1){result.boneIndex1=ids[1];result.weight1=values[ids[1]]/sum;}
            if(ids.Count>2){result.boneIndex2=ids[2];result.weight2=values[ids[2]]/sum;}
            if(ids.Count>3){result.boneIndex3=ids[3];result.weight3=values[ids[3]]/sum;}
            return result;
            void Accumulate(BoneWeight w,float scale)
            {Put(w.boneIndex0,w.weight0*scale);Put(w.boneIndex1,w.weight1*scale);Put(w.boneIndex2,w.weight2*scale);Put(w.boneIndex3,w.weight3*scale);}
            void Put(int id,float weight)
            {if(weight<=0f)return;if(values.ContainsKey(id))values[id]+=weight;else values.Add(id,weight);}
        }

        static float Influence(BoneWeight w,Dictionary<string,int> bones,params string[] names)
        {
            float sum=0f;
            foreach(string name in names)
            {
                if(!bones.TryGetValue(name,out int id))continue;
                if(w.boneIndex0==id)sum+=w.weight0;
                if(w.boneIndex1==id)sum+=w.weight1;
                if(w.boneIndex2==id)sum+=w.weight2;
                if(w.boneIndex3==id)sum+=w.weight3;
            }
            return sum;
        }

        static void AddFoldedEdges(Garment part,List<Vector3> vertices,List<BoneWeight> weights,List<int> triangles)
        {
            var counts=new Dictionary<ulong,int>();
            var directed=new Dictionary<ulong,Vector2Int>();
            int count=triangles.Count;
            for(int i=0;i<count;i+=3)
                for(int edge=0;edge<3;edge++)
                {
                    int a=triangles[i+edge],b=triangles[i+(edge+1)%3];
                    ulong key=((ulong)(uint)Mathf.Min(a,b)<<32)|(uint)Mathf.Max(a,b);
                    if(counts.ContainsKey(key))counts[key]++;else{counts[key]=1;directed[key]=new Vector2Int(a,b);}
                }
            foreach(var pair in counts)
            {
                if(pair.Value!=1)continue;
                Vector2Int edge=directed[pair.Key];
                Vector3 a=vertices[edge.x],b=vertices[edge.y],mid=(a+b)*.5f;
                bool cuff=part==Garment.Shorts?Mathf.Abs(mid.z-.690f)<.002f:
                    part==Garment.Sleeves?Mathf.Abs(Mathf.Abs(mid.x)-.442f)<.002f:
                    Mathf.Abs(mid.z-1.005f)<.002f;
                if(!cuff)continue;
                Vector3 along=part==Garment.Sleeves?new Vector3(-Mathf.Sign(mid.x),0,0):Vector3.forward;
                Vector3 radial=part==Garment.Sleeves?new Vector3(0,mid.y-.04f,mid.z-1.46f).normalized:
                    new Vector3(mid.x-(part==Garment.Shorts?Mathf.Sign(mid.x)*.105f:0f),mid.y-.025f,0).normalized;
                int start=vertices.Count;
                Vector3 edgeLift=radial*.006f;
                vertices.Add(a+edgeLift);vertices.Add(b+edgeLift);
                vertices.Add(a+along*.024f+edgeLift);vertices.Add(b+along*.024f+edgeLift);
                weights.Add(weights[edge.x]);weights.Add(weights[edge.y]);weights.Add(weights[edge.x]);weights.Add(weights[edge.y]);
                triangles.Add(start);triangles.Add(start+1);triangles.Add(start+2);
                triangles.Add(start+1);triangles.Add(start+3);triangles.Add(start+2);
            }
        }

        static Vector3Int Key(Vector3 p)=>new Vector3Int(Mathf.RoundToInt(p.x*100000f),Mathf.RoundToInt(p.y*100000f),Mathf.RoundToInt(p.z*100000f));

        static Mesh SaveMesh(Mesh generated,string assetName)
        {
            if(!AssetDatabase.IsValidFolder("Assets/DeliveryDash/Art"))AssetDatabase.CreateFolder("Assets/DeliveryDash","Art");
            if(!AssetDatabase.IsValidFolder(MeshFolder))AssetDatabase.CreateFolder("Assets/DeliveryDash/Art","Generated");
            string path=MeshFolder+"/"+assetName;
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing!=null){EditorUtility.CopySerialized(generated,existing);EditorUtility.SetDirty(existing);UnityEngine.Object.DestroyImmediate(generated);return existing;}
            AssetDatabase.CreateAsset(generated,path);return generated;
        }
    }
}

