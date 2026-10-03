using UnityEditor;
using UnityEngine;

namespace DeliveryDash.Editor
{
    /// <summary>
    /// Authored downhill street and its immediate district. The road grade is a
    /// single function, so every drivable metre falls toward the delivery door.
    /// No random prop placement is used in this feel playground.
    /// </summary>
    public static class TownVisualBuilder
    {
        public const float Grade = 0.055f;
        public const float StartZ = -8f;
        public const float FinishZ = 116f;
        public const float RoadHalfWidth = 6.15f;

        static Material road, roadEdge, joint, plaster, cream, rose, terracotta;
        static Material ochre, slate, darkWindow, timber, brass, canvas, canvasRed;
        static Material hedge, leaf, canal, door, earth, shadowStone, distantHill;

        public static float SurfaceY(float z) => 0.04f - Grade * z;

        public static float CenterX(float z)
        {
            // A slow bend is visible from the start and leaves time to steer.
            return 2.15f * Mathf.Sin(z * 0.032f) + 0.45f * Mathf.Sin(z * 0.105f);
        }

        public static GameObject Build(Transform parent = null)
        {
            LoadMaterials();
            GameObject district = new GameObject("Downhill market district");
            if (parent != null) district.transform.SetParent(parent, false);
            Transform root = district.transform;

            TerrainAndDistance(root);
            RoadRibbon(root);
            StreetEdges(root);
            BuiltTerraces(root);
            MarketStalls(root);
            SortingBelt(root);
            CanalEdge(root);
            DeliveryLandmark(root);
            return district;
        }

        static void LoadMaterials()
        {
            road = Mat("Street stone", "#8A8276");
            roadEdge = Mat("Street pale edge", "#B9A58A");
            joint = Mat("Street joint", "#6E6A62");
            plaster = Mat("Town chalk plaster", "#D9CAB0");
            cream = Mat("Town cream plaster", "#E4D8BF");
            rose = Mat("Town faded coral", "#B97D6B");
            terracotta = Mat("Terracotta roofs", "#A85E4A");
            ochre = Mat("Town ochre", "#BE9469");
            slate = Mat("Slate blue shutters", "#586A6A");
            darkWindow = Mat("Recessed window", "#425658");
            timber = Mat("Old warm timber", "#735840");
            brass = Mat("Warm metal", "#A58658");
            canvas = Mat("Canvas linen", "#E1CFAC");
            canvasRed = Mat("Canvas brick stripe", "#9E5748");
            hedge = Mat("Terrace olive", "#667259");
            leaf = Mat("Plane tree leaves", "#788063");
            canal = Mat("Canal muted teal", "#668D8B");
            door = Mat("Delivery door coral", "#B34539");
            earth = Mat("Warm hillside earth", "#A48766");
            shadowStone = Mat("Terrace shadow stone", "#746E66");
            distantHill = Mat("Distant blue hills", "#75888A");
        }

        static void TerrainAndDistance(Transform root)
        {
            MeshObject("Ground below street terraces", root,
                Ribbon(-52f, 52f, -3.8f), earth, false);
        }

        static void SortingBelt(Transform root)
        {
            Transform belt = Group("Market sorting conveyor", root, Vector3.zero);
            const float start = 18f, end = 29f;
            for (float z = start; z < end; z += 0.55f)
            {
                var slat = Box("Rubber conveyor tread", belt,
                    new Vector3(CenterX(z) + 2.9f, SurfaceY(z) + 0.035f, z),
                    new Vector3(3.6f, 0.06f, 0.51f), slate);
                slat.transform.localRotation = Quaternion.Euler(Mathf.Atan(Grade) * Mathf.Rad2Deg, 0f, 0f);
            }
        }

        static Material Mat(string name, string html)
        {
            const string folder = "Assets/DeliveryDash/Art/";
            System.IO.Directory.CreateDirectory(folder);
            string path = folder + name.Replace(' ', '_') + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            ColorUtility.TryParseHtmlString(html, out Color color);
            Shader shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader) { name = name, color = color };
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.04f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static Transform Group(string name, Transform parent, Vector3 localPosition)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }

        static GameObject Box(string name, Transform parent, Vector3 localPosition,
            Vector3 size, Material material, bool solid = false)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static GameObject MeshObject(string name, Transform parent, Mesh mesh,
            Material material, bool solid)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (solid) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        static Mesh Ribbon(float offsetA, float offsetB, float heightOffset)
        {
            const float step = 1.25f;
            int count = Mathf.CeilToInt((FinishZ - StartZ) / step) + 1;
            Vector3[] vertices = new Vector3[count * 2];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[(count - 1) * 6];
            for (int i = 0; i < count; i++)
            {
                float z = Mathf.Lerp(StartZ, FinishZ, i / (float)(count - 1));
                float x = CenterX(z);
                float y = SurfaceY(z) + heightOffset;
                vertices[2 * i] = new Vector3(x + offsetA, y, z);
                vertices[2 * i + 1] = new Vector3(x + offsetB, y, z);
                uv[2 * i] = new Vector2(0f, z * 0.17f);
                uv[2 * i + 1] = new Vector2(1f, z * 0.17f);
                if (i == count - 1) continue;
                int t = i * 6;
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                triangles[t] = a; triangles[t + 1] = c; triangles[t + 2] = b;
                triangles[t + 3] = b; triangles[t + 4] = c; triangles[t + 5] = d;
            }
            Mesh mesh = new Mesh { name = "Continuous downhill band" };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void RoadRibbon(Transform root)
        {
            Transform route = Group("Playable descending road", root, Vector3.zero);
            MeshObject("One continuous stone road collider", route,
                Ribbon(-RoadHalfWidth, RoadHalfWidth, 0f), road, true);
            MeshObject("Left pale gutter", route,
                Ribbon(-RoadHalfWidth, -RoadHalfWidth + 0.19f, 0.007f), roadEdge, false);
            MeshObject("Right pale gutter", route,
                Ribbon(RoadHalfWidth - 0.19f, RoadHalfWidth, 0.007f), roadEdge, false);
            // Broken paving joints imply hand-set stone without striping the whole road.
            for (int i = 0; i < 25; i++)
            {
                float z = -3f + i * 4.7f;
                float x = CenterX(z);
                float y = SurfaceY(z) + 0.013f;
                float width = i % 3 == 0 ? 3.8f : 2.4f;
                float side = i % 2 == 0 ? -2.7f : 2.7f;
                GameObject seam = Box("Short masonry joint", route,
                    new Vector3(x + side, y, z), new Vector3(width, 0.012f, 0.045f), joint);
                seam.transform.localRotation = Quaternion.Euler(-Mathf.Atan(Grade) * Mathf.Rad2Deg, 0f, 0f);
            }
        }

        static void StreetEdges(Transform root)
        {
            Transform edges = Group("Stone sidewalks and parapets", root, Vector3.zero);
            MeshObject("Left raised sidewalk", edges, Ribbon(-8.55f, -6.15f, 0.13f), roadEdge, true);
            MeshObject("Right raised sidewalk", edges, Ribbon(6.15f, 8.55f, 0.13f), roadEdge, true);
            for (float z = -7f; z < FinishZ; z += 5.5f)
            {
                float next = Mathf.Min(z + 5.5f, FinishZ);
                float zm = (z + next) * 0.5f;
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = CenterX(zm) + side * 6.22f;
                    float y = SurfaceY(zm) + 0.11f;
                    Box("Chamfered street curb", edges, new Vector3(x, y, zm),
                        new Vector3(0.25f, 0.23f, next - z + 0.04f), plaster, true);
                    GameObject wall = Box("Low terrace parapet", edges,
                        new Vector3(CenterX(zm) + side * 8.47f, SurfaceY(zm) + 0.40f, zm),
                        new Vector3(0.27f, 0.45f, next - z + 0.04f), plaster, true);
                    wall.transform.localRotation = Quaternion.Euler(Mathf.Atan(Grade) * Mathf.Rad2Deg, 0f, 0f);
                }
            }
            for (float z = -3f; z < FinishZ - 4f; z += 10.5f)
            {
                float y = SurfaceY(z) + 0.42f;
                float x = CenterX(z);
                // Low stone posts feel like an old town edge. The clear roadway stays 12 m wide.
                Box("Left parapet pier", edges, new Vector3(x - 8.45f, y, z),
                    new Vector3(0.58f, 0.75f, 0.62f), plaster, true);
                Box("Right parapet pier", edges, new Vector3(x + 8.45f, y, z),
                    new Vector3(0.58f, 0.75f, 0.62f), plaster, true);
                Box("Left pier cap", edges, new Vector3(x - 8.45f, y + 0.41f, z),
                    new Vector3(0.74f, 0.11f, 0.79f), roadEdge);
                Box("Right pier cap", edges, new Vector3(x + 8.45f, y + 0.41f, z),
                    new Vector3(0.74f, 0.11f, 0.79f), roadEdge);
            }
        }

        static void BuiltTerraces(Transform root)
        {
            Transform town = Group("Stepped hillside houses", root, Vector3.zero);
            House(town, -13.9f, 8f, 9.4f, 8.4f, plaster, terracotta, 0);
            House(town, 15.0f, 17f, 9.8f, 9.4f, rose, terracotta, 1);
            House(town, -15.0f, 31f, 11f, 7.6f, ochre, timber, 2);
            House(town, 15.6f, 43f, 10.2f, 8.7f, cream, terracotta, 0);
            House(town, -18.0f, 58f, 10.4f, 10.4f, rose, terracotta, 1);
            House(town, 15.4f, 70f, 10.5f, 7.5f, plaster, terracotta, 2);
            House(town, -15.0f, 85f, 11.5f, 9.3f, cream, timber, 0);
            House(town, 15.8f, 98f, 11.2f, 9.8f, ochre, terracotta, 1);
            // Receding roofs step down the hill and expose the canal beyond the right bank.
            House(town, -28f, 41f, 11f, 10.5f, plaster, terracotta, 2);
            House(town, 39f, 55f, 12f, 9.6f, rose, terracotta, 0);
            House(town, -27f, 91f, 13f, 11f, ochre, terracotta, 1);
            House(town, 42f, 102f, 12f, 10.3f, cream, terracotta, 2);
        }

        static void House(Transform root, float x, float z, float frontage, float height,
            Material wall, Material roof, int variation)
        {
            float y = SurfaceY(z) - 0.15f;
            Transform house = Group("Town house with deep windows", root, new Vector3(x, y, z));
            float depth = variation == 1 ? 7.8f : 7.0f;
            Box("Plaster body", house, new Vector3(0, height * 0.5f, 0),
                new Vector3(depth, height, frontage), wall);
            Box("Stone base course", house, new Vector3(0, 0.54f, 0),
                new Vector3(depth + 0.10f, 1.08f, frontage + 0.08f), roadEdge);
            float roadFace = x < 0f ? depth * 0.5f + 0.025f : -depth * 0.5f - 0.025f;
            float faceDirection = x < 0f ? 1f : -1f;
            // Roof silhouettes vary with one raised ridge, avoiding flat box tops.
            Roof(house, depth + 0.65f, frontage + 0.8f, height, roof);
            int bays = frontage > 10f ? 3 : 2;
            for (int floor = 0; floor < 2; floor++)
            {
                float wy = 2.75f + floor * 2.75f;
                for (int bay = 0; bay < bays; bay++)
                {
                    float wz = (bay - (bays - 1) * 0.5f) * (frontage / (bays + 0.42f));
                    float xx = roadFace + faceDirection * 0.017f;
                    Box("Window dark recess", house, new Vector3(xx, wy, wz),
                        new Vector3(0.055f, 1.48f, 1.13f), darkWindow);
                    Box("Stone window lintel", house, new Vector3(xx + faceDirection * 0.07f, wy + 0.82f, wz),
                        new Vector3(0.20f, 0.19f, 1.41f), roadEdge);
                    Box("Stone window sill", house, new Vector3(xx + faceDirection * 0.15f, wy - 0.81f, wz),
                        new Vector3(0.34f, 0.16f, 1.46f), roadEdge);
                    Box("Crossbar", house, new Vector3(xx + faceDirection * 0.07f, wy, wz),
                        new Vector3(0.08f, 0.09f, 1.06f), slate);
                    Box("Window upright", house, new Vector3(xx + faceDirection * 0.07f, wy, wz),
                        new Vector3(0.08f, 1.42f, 0.07f), slate);
                    if ((variation + bay + floor) % 3 == 0)
                    {
                        Box("Open wooden shutter", house,
                            new Vector3(xx + faceDirection * 0.15f, wy, wz - 0.75f),
                            new Vector3(0.10f, 1.41f, 0.35f), timber);
                    }
                }
            }
            float doorZ = variation == 1 ? frontage * 0.23f : -frontage * 0.26f;
            Box("Deep door opening", house, new Vector3(roadFace + faceDirection * 0.05f, 1.13f, doorZ),
                new Vector3(0.12f, 2.25f, 1.35f), timber);
            Box("Door threshold", house, new Vector3(roadFace + faceDirection * 0.31f, 0.17f, doorZ),
                new Vector3(0.65f, 0.22f, 1.9f), plaster);
            Box("Cornice shadow", house, new Vector3(0, height - 0.15f, 0),
                new Vector3(depth + 0.35f, 0.25f, frontage + 0.27f), timber);
        }

        static void Roof(Transform house, float depth, float frontage, float eave, Material material)
        {
            float rise = Mathf.Min(1.65f, depth * 0.27f);
            Vector3[] vertices =
            {
                new Vector3(-depth * 0.5f, eave, -frontage * 0.5f),
                new Vector3(0, eave + rise, -frontage * 0.5f),
                new Vector3(depth * 0.5f, eave, -frontage * 0.5f),
                new Vector3(-depth * 0.5f, eave, frontage * 0.5f),
                new Vector3(0, eave + rise, frontage * 0.5f),
                new Vector3(depth * 0.5f, eave, frontage * 0.5f)
            };
            int[] triangles = { 0, 4, 1, 0, 3, 4, 1, 4, 5, 1, 5, 2,
                0, 1, 2, 3, 5, 4 };
            Mesh mesh = new Mesh { name = "Pitched terracotta roof" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            MeshObject("Pitched hand-set roof", house, mesh, material, false);
            for (int i = 0; i < 4; i++)
            {
                float z = -frontage * 0.38f + i * frontage * 0.25f;
                Box("Roof tile row", house, new Vector3(0, eave + rise + 0.008f, z),
                    new Vector3(0.055f, 0.035f, 0.06f), roadEdge);
            }
        }

        static void MarketStalls(Transform root)
        {
            Transform market = Group("Market terraces and canvas", root, Vector3.zero);
            Stall(market, -9.2f, 19f, 4.1f, canvas, canvasRed);
            Stall(market, 9.2f, 38f, 4.6f, canvasRed, canvas);
            Stall(market, -9.1f, 69f, 4.5f, canvas, canvasRed);
            Stall(market, 9.3f, 91f, 4.3f, canvas, canvasRed);
            // A few trees are narrow vertical cues on the terrace, never spherical blobs.
            Tree(market, -10.2f, 51f);
            Tree(market, 10.5f, 59f);
            Tree(market, -10.6f, 100f);
        }

        static void Stall(Transform root, float x, float z, float width,
            Material baseCanvas, Material stripeCanvas)
        {
            float y = SurfaceY(z) + 0.15f;
            Transform stall = Group("Canvas market stall", root, new Vector3(x, y, z));
            Box("Wood counter", stall, new Vector3(0, 1.16f, 0),
                new Vector3(2.25f, 0.16f, width), timber);
            for (int side = -1; side <= 1; side += 2)
            {
                float zz = side * width * 0.48f;
                Box("Canopy post", stall, new Vector3(-0.88f, 1.60f, zz),
                    new Vector3(0.10f, 3.1f, 0.10f), timber);
                Box("Canopy post", stall, new Vector3(0.88f, 1.60f, zz),
                    new Vector3(0.10f, 3.1f, 0.10f), timber);
            }
            for (int band = 0; band < 5; band++)
            {
                Material m = band % 2 == 0 ? baseCanvas : stripeCanvas;
                Box("Taut canvas panel", stall,
                    new Vector3(0, 3.07f, -width * 0.4f + band * width * 0.2f),
                    new Vector3(2.62f, 0.07f, width * 0.21f), m);
            }
            Box("Deep valance", stall, new Vector3(-1.30f, 2.86f, 0),
                new Vector3(0.08f, 0.35f, width), baseCanvas);
        }

        static void Tree(Transform root, float x, float z)
        {
            float y = SurfaceY(z) + 0.13f;
            Transform tree = Group("Pruned terrace plane tree", root, new Vector3(x, y, z));
            Box("Square trunk", tree, new Vector3(0, 1.65f, 0),
                new Vector3(0.25f, 3.3f, 0.26f), timber);
            Box("Branch left", tree, new Vector3(-0.52f, 3.15f, 0),
                new Vector3(1.22f, 0.17f, 0.18f), timber);
            Box("Branch right", tree, new Vector3(0.47f, 3.52f, 0),
                new Vector3(1.12f, 0.16f, 0.18f), timber);
            // Clipped leaf masses read as planted town trees, not default spheres.
            Box("Pruned crown", tree, new Vector3(0, 4.0f, 0),
                new Vector3(2.15f, 1.5f, 1.55f), leaf);
            Box("Crown extension", tree, new Vector3(-0.48f, 3.66f, 0.1f),
                new Vector3(1.25f, 0.85f, 1.70f), hedge);
        }

        static void CanalEdge(Transform root)
        {
            Transform side = Group("Canal below the right terrace", root, Vector3.zero);
            MeshObject("Canal surface", side, Ribbon(19f, 32f, -2.60f), canal, false);
            MeshObject("Near stone canal bank", side, Ribbon(18.3f, 19f, -1.18f), shadowStone, false);
            MeshObject("Far canal bank", side, Ribbon(32f, 33.1f, -1.16f), plaster, false);
            for (float z = 6f; z < FinishZ; z += 22f)
            {
                float x = CenterX(z) + 25.4f;
                float y = SurfaceY(z) - 1.55f;
                Box("Stone canal pier", side, new Vector3(x, y, z),
                    new Vector3(0.8f, 2.1f, 1.2f), roadEdge);
            }
        }

        static void DeliveryLandmark(Transform root)
        {
            float z = 113f;
            float y = SurfaceY(z);
            float x = CenterX(z);
            Transform finish = Group("Delivery house landmark", root,
                new Vector3(x, y, z));
            Box("Small limestone delivery house", finish,
                new Vector3(0, 4.1f, 5.0f), new Vector3(10.2f, 8.2f, 6.5f), cream);
            Box("Tall red doorway", finish,
                new Vector3(0, 2.00f, 1.69f), new Vector3(2.45f, 3.90f, 0.10f), door);
            Box("Door light recess", finish,
                new Vector3(0, 4.45f, 1.67f), new Vector3(1.12f, 0.54f, 0.12f), darkWindow);
            Box("Door lintel", finish,
                new Vector3(0, 4.13f, 1.48f), new Vector3(3.10f, 0.32f, 0.5f), plaster);
            Box("Broad threshold", finish,
                new Vector3(0, 0.13f, 0.90f), new Vector3(3.3f, 0.23f, 2.20f), plaster);
            Roof(finish, 11.4f, 7.4f, 8.1f, terracotta);
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Delivery arch pier", finish,
                    new Vector3(side * 2.0f, 2.6f, 1.3f),
                    new Vector3(0.34f, 5.20f, 0.36f), roadEdge);
                Box("Warm porch lamp", finish,
                    new Vector3(side * 2.95f, 3.65f, 1.40f),
                    new Vector3(0.30f, 0.52f, 0.25f), brass);
            }
            // The landmark is visible by its shape and red doorway; no label is needed.
        }
    }
}
