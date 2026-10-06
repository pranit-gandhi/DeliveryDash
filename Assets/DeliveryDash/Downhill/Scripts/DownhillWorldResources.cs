using System.Collections.Generic;
using UnityEngine;

namespace DeliveryDash.Downhill
{
    public sealed class DownhillWorldResources : MonoBehaviour
    {
        readonly HashSet<Mesh> meshes = new HashSet<Mesh>();
        public static void Track(GameObject world)
        {
            if (!Application.isPlaying) return;
            var owner = world.AddComponent<DownhillWorldResources>();
            foreach (var filter in world.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh;
                if (mesh == null || mesh.name == "Cube" || mesh.name == "Sphere" ||
                    mesh.name == "Capsule" || mesh.name == "Cylinder" || mesh.name == "Plane" || mesh.name == "Quad") continue;
                owner.meshes.Add(mesh);
            }
        }
        void OnDestroy()
        {
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
        }
    }
}
