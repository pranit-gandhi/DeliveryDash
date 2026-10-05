using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace DeliveryDash
{
    public sealed class TownStaticGeometry : MonoBehaviour
    {
        readonly List<Mesh> generated=new List<Mesh>();
        public static void Combine(Transform district)
        {
            var parts=district.GetComponentsInChildren<MeshFilter>().Where(f=>f.GetComponent<TextMesh>()==null&&f.sharedMesh!=null&&f.sharedMesh.isReadable&&f.GetComponent<MeshRenderer>()!=null&&f.GetComponent<MeshRenderer>().enabled&&f.GetComponentInParent<TownShopOwner>()==null&&f.GetComponentInParent<TownCitizenMotion>()==null&&!Dynamic(f.transform)).ToArray();
            var owner=district.gameObject.AddComponent<TownStaticGeometry>();
            var batches=new Dictionary<Material,List<CombineInstance>>();
            foreach(var f in parts)
            {
                var mats=f.GetComponent<MeshRenderer>().sharedMaterials;
                for(int i=0;i<f.sharedMesh.subMeshCount&&i<mats.Length;i++)
                {if(mats[i]==null)continue;if(!batches.ContainsKey(mats[i]))batches[mats[i]]=new List<CombineInstance>();batches[mats[i]].Add(new CombineInstance{mesh=f.sharedMesh,subMeshIndex=i,transform=district.worldToLocalMatrix*f.transform.localToWorldMatrix});}
                f.GetComponent<MeshRenderer>().enabled=false;
            }
            foreach(var pair in batches)
            {
                var m=new Mesh{name="District batch "+pair.Key.name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};m.CombineMeshes(pair.Value.ToArray());owner.generated.Add(m);
                var o=new GameObject(m.name);o.transform.SetParent(district,false);o.AddComponent<MeshFilter>().sharedMesh=m;o.AddComponent<MeshRenderer>().sharedMaterial=pair.Key;
            }
        }
        static bool Dynamic(Transform t)
        {for(var p=t;p!=null;p=p.parent)if(p.name=="Pizza pickup zone"||p.name=="Assigned customer zone"||p.name.StartsWith("Neighborhood "))return true;return false;}
        void OnDestroy(){foreach(var m in generated)if(m!=null)Destroy(m);}
    }
}
