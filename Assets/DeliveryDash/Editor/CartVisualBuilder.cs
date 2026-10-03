using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DeliveryDash.Editor
{
    /// <summary>
    /// Editor-time cart geometry. Coordinates are relative to the grounded cart root:
    /// forward is +Z, and the four tire contact patches are at Y = 0.
    /// </summary>
    public static class CartVisualBuilder
    {
        const string AssetRoot = "Assets/DeliveryDash/Art/Cart";

        public static GameObject Build(Transform parent)
        {
            if (!AssetDatabase.IsValidFolder(AssetRoot))
                AssetDatabase.CreateFolder("Assets/DeliveryDash/Art", "Cart");

            Material steel = Material("Brushed steel", "#8E9998", 0.42f);
            Material brightSteel = Material("Rim highlights", "#BCC2B8", 0.48f);
            Material shadowSteel = Material("Recessed steel", "#505D5C", 0.22f);
            Material handleRed = Material("Baked red handle", "#AB3933", 0.22f);
            Material tire = Material("Warm charcoal tire", "#292D2D", 0.06f);

            var root = new GameObject("Shopping cart | formed steel and four casters");
            root.transform.SetParent(parent, false);

            var frame = new TubeBatch();
            var wire = new TubeBatch();
            var rim = new TubeBatch();
            var accent = new TubeBatch();
            var dark = new TubeBatch();

            // A tapered basket reads as a real shopping cart from the chase camera.
            Vector3 rearTopL = P(-0.90f, 1.06f, -1.04f);
            Vector3 rearTopR = P(0.90f, 1.06f, -1.04f);
            Vector3 frontTopL = P(-0.81f, 0.98f, 0.99f);
            Vector3 frontTopR = P(0.81f, 0.98f, 0.99f);
            Vector3 rearFloorL = P(-0.64f, 0.47f, -0.79f);
            Vector3 rearFloorR = P(0.64f, 0.47f, -0.79f);
            Vector3 frontFloorL = P(-0.58f, 0.47f, 0.74f);
            Vector3 frontFloorR = P(0.58f, 0.47f, 0.74f);

            rim.Path(0.036f, rearTopL, frontTopL, frontTopR, rearTopR);
            rim.Path(0.033f, rearFloorL, frontFloorL, frontFloorR, rearFloorR, rearFloorL);
            frame.Path(0.032f, rearTopL, rearFloorL, frontFloorL, frontTopL);
            frame.Path(0.032f, rearTopR, rearFloorR, frontFloorR, frontTopR);

            // The wire panels have deliberate negative space so the seated courier
            // remains visible. Cross rails make the silhouette hold up at 720p.
            for (int i = 1; i <= 12; i++)
            {
                float t = i / 13f;
                wire.Path(0.0095f, Vector3.Lerp(rearFloorL, frontFloorL, t),
                    Vector3.Lerp(rearTopL, frontTopL, t));
                wire.Path(0.0095f, Vector3.Lerp(rearFloorR, frontFloorR, t),
                    Vector3.Lerp(rearTopR, frontTopR, t));
            }

            for (int i = 1; i <= 10; i++)
            {
                float t = i / 11f;
                wire.Path(0.0095f, Vector3.Lerp(rearFloorL, rearFloorR, t),
                    Vector3.Lerp(rearTopL, rearTopR, t));
                wire.Path(0.0095f, Vector3.Lerp(frontFloorL, frontFloorR, t),
                    Vector3.Lerp(frontTopL, frontTopR, t));
            }

            foreach (float height in new[] { 0.30f, 0.63f })
            {
                Vector3 l0 = Vector3.Lerp(rearFloorL, rearTopL, height);
                Vector3 l1 = Vector3.Lerp(frontFloorL, frontTopL, height);
                Vector3 r0 = Vector3.Lerp(rearFloorR, rearTopR, height);
                Vector3 r1 = Vector3.Lerp(frontFloorR, frontTopR, height);
                wire.Path(0.011f, l0, l1, r1, r0, l0);
            }

            // Open lattice below the rider, with two heavier transverse supports.
            for (int i = 0; i <= 9; i++)
            {
                float t = i / 9f;
                wire.Path(0.010f, Vector3.Lerp(rearFloorL, rearFloorR, t),
                    Vector3.Lerp(frontFloorL, frontFloorR, t));
            }
            for (int i = 1; i <= 8; i++)
            {
                float t = i / 9f;
                wire.Path(0.010f, Vector3.Lerp(rearFloorL, frontFloorL, t),
                    Vector3.Lerp(rearFloorR, frontFloorR, t));
            }
            frame.Path(0.023f, P(-0.64f, 0.46f, -0.46f), P(0.64f, 0.46f, -0.46f));
            frame.Path(0.023f, P(-0.60f, 0.46f, 0.46f), P(0.60f, 0.46f, 0.46f));

            // The sloping lower frame and diagonal braces connect the basket
            // visibly to each caster instead of leaving it floating on four posts.
            for (int side = -1; side <= 1; side += 2)
            {
                float sx = side;
                frame.Path(0.039f, P(sx * 0.64f, 0.46f, -0.76f),
                    P(sx * 0.66f, 0.24f, -0.90f), P(sx * 0.66f, 0.24f, 0.85f),
                    P(sx * 0.58f, 0.47f, 0.75f));
                dark.Path(0.026f, P(sx * 0.62f, 0.44f, 0.02f),
                    P(sx * 0.66f, 0.24f, 0.57f));
                dark.Path(0.026f, P(sx * 0.63f, 0.43f, -0.38f),
                    P(sx * 0.66f, 0.24f, -0.70f));
                frame.Path(0.037f, P(sx * 0.90f, 1.05f, -1.04f),
                    P(sx * 0.97f, 1.15f, -1.13f));
            }
            frame.Path(0.038f, P(-0.66f, 0.24f, -0.89f), P(0.66f, 0.24f, -0.89f));
            frame.Path(0.038f, P(-0.66f, 0.24f, 0.84f), P(0.66f, 0.24f, 0.84f));

            // Single warm red hand bar is the cart's visual punctuation.
            accent.Path(0.047f, P(-0.98f, 1.15f, -1.13f),
                P(-0.88f, 1.15f, -1.13f), P(0.88f, 1.15f, -1.13f),
                P(0.98f, 1.15f, -1.13f));
            rim.Path(0.052f, P(-1.02f, 1.15f, -1.13f), P(-0.97f, 1.15f, -1.13f));
            rim.Path(0.052f, P(0.97f, 1.15f, -1.13f), P(1.02f, 1.15f, -1.13f));

            AddMesh(root.transform, "Continuous tubular chassis", frame.Mesh("Cart chassis"), steel);
            AddMesh(root.transform, "Basket wire lattice", wire.Mesh("Basket lattice"), steel);
            AddMesh(root.transform, "Basket upper rolled rim", rim.Mesh("Rolled rim"), brightSteel);
            AddMesh(root.transform, "Red rubber hand bar", accent.Mesh("Red hand bar"), handleRed);
            AddMesh(root.transform, "Underframe bracing", dark.Mesh("Underframe braces"), shadowSteel);

            Mesh wheelRubber = WheelMesh("Caster rubber", 0.19f, 0.115f, 0.055f, 32);
            Mesh wheelHub = SolidWheelMesh("Caster pressed hub", 0.103f, 0.132f, 32);
            for (int side = -1; side <= 1; side += 2)
                for (int end = -1; end <= 1; end += 2)
            {
                string id = (end > 0 ? "F" : "R") + (side > 0 ? "R" : "L");
                Vector3 center = P(side * (end > 0 ? 0.80f : 0.72f), 0.19f, end > 0 ? 0.92f : -0.88f);
                var pivot = new GameObject("CasterPivot_" + id);
                pivot.transform.SetParent(root.transform, false);
                pivot.transform.localPosition = center;
                var fork = new TubeBatch();
                fork.Path(0.027f, P(-0.093f, 0.022f, 0f),
                    P(-0.093f, 0.145f, -0.035f), P(0f, 0.177f, -0.035f),
                    P(0.093f, 0.145f, -0.035f), P(0.093f, 0.022f, 0f));
                fork.Path(0.034f, P(0f, 0.177f, -0.035f), P(0f, 0.235f, -0.035f));
                AddMesh(pivot.transform, "Pressed caster fork", fork.Mesh("Fork " + id), shadowSteel);

                var spin = new GameObject("WheelSpin_" + id);
                spin.transform.SetParent(pivot.transform, false);
                AddMesh(spin.transform, "Rubber tread", wheelRubber, tire);
                AddMesh(spin.transform, "Pressed steel hub", wheelHub, brightSteel);
            }

            return root;
        }

        static Vector3 P(float x, float y, float z) => new Vector3(x, y, z);

        static Material Material(string name, string hex, float smoothness)
        {
            string path = AssetRoot + "/" + name.Replace(' ', '_') + ".mat";
            var asset = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (asset != null) return asset;
            Shader shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
            var color = ColorUtility.TryParseHtmlString(hex, out Color parsed) ? parsed : Color.white;
            asset = new Material(shader) { name = name, color = color };
            if (asset.HasProperty("_Glossiness")) asset.SetFloat("_Glossiness", smoothness);
            if (asset.HasProperty("_Smoothness")) asset.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void AddMesh(Transform parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        static Mesh SaveMesh(string name, List<Vector3> vertices, List<int> triangles)
        {
            string path = AssetRoot + "/" + name.Replace(' ', '_') + ".asset";
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old != null) AssetDatabase.DeleteAsset(path);
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static Mesh WheelMesh(string name, float outerRadius, float innerRadius, float halfWidth, int sides)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                v.Add(P(-halfWidth, c * outerRadius, s * outerRadius));
                v.Add(P(halfWidth, c * outerRadius, s * outerRadius));
                v.Add(P(-halfWidth, c * innerRadius, s * innerRadius));
                v.Add(P(halfWidth, c * innerRadius, s * innerRadius));
            }
            for (int i = 0; i < sides; i++)
            {
                int a = i * 4, b = ((i + 1) % sides) * 4;
                Quad(t, a, b, b + 1, a + 1);
                Quad(t, a + 2, a + 3, b + 3, b + 2);
                Quad(t, a, a + 2, b + 2, b);
                Quad(t, a + 1, b + 1, b + 3, a + 3);
            }
            return SaveMesh(name, v, t);
        }

        static Mesh SolidWheelMesh(string name, float radius, float width, int sides)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            float half = width * 0.5f;
            v.Add(P(-half, 0f, 0f));
            v.Add(P(half, 0f, 0f));
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                v.Add(P(-half, Mathf.Cos(a) * radius, Mathf.Sin(a) * radius));
                v.Add(P(half, Mathf.Cos(a) * radius, Mathf.Sin(a) * radius));
            }
            for (int i = 0; i < sides; i++)
            {
                int a = 2 + i * 2, b = 2 + ((i + 1) % sides) * 2;
                t.Add(0); t.Add(b); t.Add(a);
                t.Add(1); t.Add(a + 1); t.Add(b + 1);
                Quad(t, a, b, b + 1, a + 1);
            }
            return SaveMesh(name, v, t);
        }

        static void Quad(List<int> triangles, int a, int b, int c, int d)
        {
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
            triangles.Add(a); triangles.Add(c); triangles.Add(d);
        }

        sealed class TubeBatch
        {
            readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<int> triangles = new List<int>();

            public void Path(float radius, params Vector3[] points)
            {
                const int sides = 8;
                if (points.Length < 2) return;
                int start = vertices.Count;
                for (int i = 0; i < points.Length; i++)
                {
                    Vector3 previous = points[Mathf.Max(0, i - 1)];
                    Vector3 next = points[Mathf.Min(points.Length - 1, i + 1)];
                    Vector3 tangent = (next - previous).normalized;
                    Vector3 reference = Mathf.Abs(Vector3.Dot(tangent, Vector3.up)) > 0.88f
                        ? Vector3.forward : Vector3.up;
                    Vector3 right = Vector3.Cross(tangent, reference).normalized;
                    Vector3 up = Vector3.Cross(right, tangent).normalized;
                    for (int s = 0; s < sides; s++)
                    {
                        float angle = s * Mathf.PI * 2f / sides;
                        vertices.Add(points[i] + radius * (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)));
                    }
                    if (i == 0) continue;
                    int prev = start + (i - 1) * sides;
                    int curr = start + i * sides;
                    for (int s = 0; s < sides; s++)
                    {
                        int sn = (s + 1) % sides;
                        Quad(triangles, prev + s, curr + s, curr + sn, prev + sn);
                    }
                }
            }

            public Mesh Mesh(string name) => SaveMesh(name, vertices, triangles);
        }
    }
}

