using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeliveryDash.Downhill
{
    // Decorative geometry only. Road, wheel contact and gameplay RNG remain in CourseGraph.
    public static class DownhillBackdrop
    {
        const string ResourceRoot = "DeliveryDashBackdrop/";
        static readonly Dictionary<string, Mesh> sourceMeshes = new Dictionary<string, Mesh>();
        static Material land, olive, rock, roof, chalk, opening, foliage, groveLeaves, bark, city, peach, ochre;
        static Mesh cube, ridge;
        static readonly float[] terrainOffsets = { -540, -400, -290, -220, -165, -120, -85, -58, -36, -18, 0,
            18, 36, 58, 85, 120, 165, 220, 290, 400, 540 };

        public static GameObject Build(CourseGraph course, Transform parent = null)
        {
            Materials();
            var root = new GameObject("Town backdrop");
            if (parent != null) root.transform.SetParent(parent, false);
            var terrain = new GameObject("Continuous valley landscape");
            terrain.transform.SetParent(root.transform, false);
            terrain.AddComponent<MeshFilter>().sharedMesh = Landscape(course);
            terrain.AddComponent<MeshRenderer>().sharedMaterial = land;
            var groups = new Dictionary<Material, Batch>();
            var groveExclusions = new List<GroveExclusion>();
            var rng = new System.Random(course.Seed ^ 0x4b19a);
            string[] houses = { "building-type-a", "building-type-h", "building-type-p", "building-type-t" };
            string[] trees = { "tree_oak", "tree_palm", "tree_pineRoundB" };

            // Nested, course-aligned districts stay visible through turns without hiding choices.
            for (float station = 55; station < course.Length + 170; station += 96)
            {
                CourseSample sample = ExtendedSample(course, station);
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int row = 0; row < 3; row++)
                    {
                        float offset = side * (52 + row * 43 + Next(rng, -5, 5));
                        int count = row == 0 ? 5 : 7;
                        for (int i = 0; i < count; i++)
                        {
                            float z = station + (i - count * .5f) * 13.5f;
                            var road = ExtendedSample(course, z);
                            Vector3 p = new Vector3(road.Position.x + offset + Next(rng, -3, 3), 0, z);
                            if (!Clear(course, p, 16)) continue;
                            float width = Next(rng, 7.4f, 10.7f);
                            float yaw = side < 0 ? 80 : -95;
                            yaw += Next(rng, -11, 11);
                            Quaternion rotation = Quaternion.Euler(0, yaw, 0);
                            p.y = GroundHeight(course, p.x, p.z);
                            if (row == 0)
                            {
                                Mesh model = LoadObj(houses[rng.Next(houses.Length)], false);
                                if (model == null) continue;
                                float factor = width / Mathf.Max(model.bounds.size.x, model.bounds.size.z);
                                Vector3 scale = new Vector3(factor, factor * Next(rng, .86f, 1.3f), factor);
                                // Match the imported model's actual base to the terrace, not its origin.
                                p.y -= model.bounds.min.y * scale.y;
                                Add(groups, city, model, Matrix4x4.TRS(p, rotation, scale));
                            }
                            else FarHouse(groups, p, rotation, width, Next(rng, 6.0f, 11.5f), i + row);
                            float foundationHeight = Mathf.Max(1.2f, Next(rng, 2, 3.4f));
                            AddBox(groups, rock, p + Vector3.down * foundationHeight * .5f,
                                new Vector3(width + 1.0f, foundationHeight, width * .83f), rotation);
                            groveExclusions.Add(new GroveExclusion(p, width * (row == 0 ? .86f : 1.08f)));
                        }
                    }

                    // Gardens occupy the strip between street buildings and distant neighbourhoods.
                    for (int i = 0; i < 7; i++)
                    {
                        float z = station + (i - 3) * 11f + Next(rng, -3, 3);
                        var road = ExtendedSample(course, z);
                        float offset = side * Next(rng, 29, 46);
                        Vector3 p = new Vector3(road.Position.x + offset, 0, z);
                        if (!Clear(course, p, 17)) continue;
                        Mesh model = LoadObj(trees[(i + (side > 0 ? 1 : 0)) % trees.Length], true);
                        if (model == null) continue;
                        float height = Next(rng, 5.0f, 8.5f);
                        float factor = height / model.bounds.size.y;
                        p.y = GroundHeight(course, p.x, p.z) - model.bounds.min.y * factor;
                        var matrix = Matrix4x4.TRS(p, Quaternion.Euler(0, Next(rng, 0, 360), 0), Vector3.one * factor);
                        Add(groups, foliage, model, matrix, 0);
                        if (model.subMeshCount > 1) Add(groups, bark, model, matrix, 1);
                    }

                    // Long stone terrace edges connect the distant buildings to their ground.
                    float terraceX = sample.Position.x + side * 69;
                    Vector3 center = new Vector3(terraceX, GroundHeight(course, terraceX, station) - 1.6f, station);
                    AddBox(groups, rock, center, new Vector3(1.3f, 3.4f, 74), Quaternion.identity);
                    if (((int)station / 96 + (side > 0 ? 1 : 0)) % 3 == 0)
                    {
                        Landmark(course, groups, sample.Position.x + side * 132, station + 23, rng);
                        groveExclusions.Add(new GroveExclusion(new Vector3(sample.Position.x + side * 132 + 4, 0, station + 23), 18));
                    }
                }
            }
            Groves(course, groups, groveExclusions);
            Mountains(course, groups);
            foreach (var pair in groups) pair.Value.Flush(root.transform, pair.Key);
            return root;
        }

        static CourseSample ExtendedSample(CourseGraph course, float station)
        {
            CourseSample sample = course.Sample(Mathf.Clamp(station, 0, course.Length));
            if (station < 0 || station > course.Length)
            {
                float extension = station < 0 ? station : station - course.Length;
                sample.Position += new Vector3(0, -.14f * extension, extension);
            }
            return sample;
        }

        static bool Clear(CourseGraph course, Vector3 position, float clearance)
        {
            CourseSample nearest = course.Closest(position);
            if (position.z < -20 || position.z > course.Length + 25) return true;
            return new Vector2(position.x - nearest.Position.x, position.z - nearest.Position.z).magnitude
                > nearest.Width * .5f + clearance;
        }

        public static float GroundHeight(CourseGraph course, float x, float z)
        {
            // Query the very same two triangles as Landscape, including its 12 metre sampling.
            int rows = Mathf.CeilToInt((course.Length + 720) / 12f) + 1;
            if (z < -100 || z > -100 + (rows - 1) * 12) return TerrainVertexHeight(course, x, z);
            int row = Mathf.Clamp(Mathf.FloorToInt((z + 100) / 12), 0, rows - 2);
            float za = -100 + row * 12, zb = za + 12, t = (z - za) / 12;
            float xa = ExtendedSample(course, za).Position.x, xb = ExtendedSample(course, zb).Position.x;
            float offset = x - Mathf.Lerp(xa, xb, t);
            if (offset < terrainOffsets[0] || offset > terrainOffsets[terrainOffsets.Length - 1])
                return TerrainVertexHeight(course, x, z);
            int column = 0;
            while (column < terrainOffsets.Length - 2 && offset > terrainOffsets[column + 1]) column++;
            float left = terrainOffsets[column], right = terrainOffsets[column + 1];
            float u = Mathf.InverseLerp(left, right, offset);
            float a = TerrainVertexHeight(course, xa + left, za), b = TerrainVertexHeight(course, xa + right, za);
            float c = TerrainVertexHeight(course, xb + left, zb), d = TerrainVertexHeight(course, xb + right, zb);
            return u + t <= 1 ? a + (b - a) * u + (c - a) * t
                : d + (b - d) * (1 - t) + (c - d) * (1 - u);
        }

        static float TerrainVertexHeight(CourseGraph course, float x, float z)
        {
            var sample = ExtendedSample(course, z);
            float distance = Mathf.Abs(x - sample.Position.x);
            if (z >= 0 && z <= course.Length)
            {
                var nearest = course.Closest(new Vector3(x, 0, z));
                distance = new Vector2(x - nearest.Position.x, z - nearest.Position.z).magnitude;
                sample.Position.y = nearest.Position.y;
            }
            float lift = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(24, 220, distance)) * 64;
            float ripple = (Mathf.Sin(x * .024f + z * .013f) + Mathf.Sin(x * .011f - z * .022f)) * 4;
            // The central valley always remains below both the safe and shortcut road meshes.
            return sample.Position.y - 4.0f + lift + ripple * Mathf.InverseLerp(35, 130, distance);
        }

        static Mesh Landscape(CourseGraph course)
        {
            float[] offsets = terrainOffsets;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            int rows = Mathf.CeilToInt((course.Length + 720) / 12f) + 1;
            for (int row = 0; row < rows; row++)
            {
                float z = -100 + row * 12;
                CourseSample sample = ExtendedSample(course, z);
                for (int column = 0; column < offsets.Length; column++)
                {
                    float x = sample.Position.x + offsets[column];
                    vertices.Add(new Vector3(x, TerrainVertexHeight(course, x, z), z));
                    if (row == 0 || column == 0) continue;
                    int a = (row - 1) * offsets.Length + column - 1;
                    int b = a + 1, c = row * offsets.Length + column - 1, d = c + 1;
                    triangles.AddRange(new[] { a, c, b, b, c, d });
                }
            }
            var mesh = new Mesh { name = "Course aligned olive valley", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        static void Landmark(CourseGraph course, Dictionary<Material, Batch> groups, float x, float z, System.Random rng)
        {
            Vector3 p = new Vector3(x, GroundHeight(course, x, z), z);
            float height = Next(rng, 17, 23);
            AddBox(groups, chalk, p + Vector3.up * (height * .5f), new Vector3(5.4f, height, 5.6f), Quaternion.identity);
            AddBox(groups, rock, p + Vector3.up * 1.2f, new Vector3(6.2f, 2.4f, 6.4f), Quaternion.identity);
            AddBox(groups, chalk, p + Vector3.up * (height + .15f), new Vector3(6.2f, .8f, 6.4f), Quaternion.identity);
            Add(groups, roof, ridge, Matrix4x4.TRS(p + Vector3.up * (height + .5f), Quaternion.identity, new Vector3(6.5f, 3.2f, 6.6f)));
            foreach (int side in new[] { -1, 1 })
            {
                AddBox(groups, opening, p + new Vector3(side * 2.73f, height - 2.3f, 0), new Vector3(.08f, 2.6f, 1.5f), Quaternion.identity);
                AddBox(groups, opening, p + new Vector3(0, height - 2.3f, side * 2.83f), new Vector3(1.5f, 2.6f, .08f), Quaternion.identity);
            }
            AddBox(groups, chalk, p + new Vector3(8, 3.5f, 0), new Vector3(11, 7, 10), Quaternion.identity);
            Add(groups, roof, ridge, Matrix4x4.TRS(p + new Vector3(8, 7, 0), Quaternion.identity, new Vector3(12, 2.8f, 11)));
        }

        static void FarHouse(Dictionary<Material, Batch> groups, Vector3 p, Quaternion rotation, float width, float height, int variant)
        {
            Material plaster = variant % 3 == 0 ? peach : variant % 3 == 1 ? ochre : chalk;
            float depth = width * .78f;
            AddBox(groups, plaster, p + Vector3.up * (height * .5f), new Vector3(width, height, depth), rotation);
            Add(groups, roof, ridge, Matrix4x4.TRS(p + Vector3.up * height, rotation,
                new Vector3(width + .65f, width * .24f, depth + .6f)));
            if (variant % 3 == 0)
            {
                Vector3 wing = p + rotation * new Vector3(width * .48f, 0, -.7f);
                AddBox(groups, chalk, wing + Vector3.up * (height * .34f), new Vector3(width * .57f, height * .68f, depth * .86f), rotation);
                Add(groups, roof, ridge, Matrix4x4.TRS(wing + Vector3.up * height * .68f, rotation,
                    new Vector3(width * .6f, width * .15f, depth * .91f)));
            }
            for (int floor = 0; floor < (height > 8 ? 3 : 2); floor++)
                for (int column = -1; column <= 1; column++)
                    AddBox(groups, opening, p + rotation * new Vector3(column * width * .27f, 2.0f + floor * 2.8f, depth * .5f + .03f),
                        new Vector3(.65f, 1.2f, .06f), rotation);
            if (variant % 2 == 0) AddBox(groups, rock, p + rotation * new Vector3(-width * .3f, height + 1.0f, -.6f),
                new Vector3(.7f, 2.1f, .7f), rotation);
        }

        static void Mountains(CourseGraph course, Dictionary<Material, Batch> groups)
        {
            // Broad, faceted ridges beyond the city, visible through several neighbourhood layers.
            int count = Mathf.CeilToInt(course.Length / 210) + 3;
            for (int i = 0; i < count; i++) for (int side = -1; side <= 1; side += 2)
            {
                float z = i * 210 + 60;
                var sample = ExtendedSample(course, z);
                float x = sample.Position.x + side * (360 + (i % 3) * 25);
                Vector3 p = new Vector3(x, GroundHeight(course, x, z) - 6, z);
                Add(groups, olive, ridge, Matrix4x4.TRS(p, Quaternion.Euler(0, i * 19, 0),
                    new Vector3(220, 43 + i % 3 * 12, 285)));
            }
        }

        static float Next(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        struct GroveExclusion
        {
            public Vector2 Center;
            public float Radius;
            public GroveExclusion(Vector3 p, float radius) { Center = new Vector2(p.x, p.z); Radius = radius; }
        }

        static bool GroveClear(CourseGraph course, List<GroveExclusion> exclusions, Vector3 p, float radius)
        {
            if (!Clear(course, p, 28 + radius)) return false;
            Vector2 point = new Vector2(p.x, p.z);
            foreach (var exclusion in exclusions)
                if ((point - exclusion.Center).sqrMagnitude < Mathf.Pow(exclusion.Radius + radius, 2)) return false;
            return true;
        }

        static void Groves(CourseGraph course, Dictionary<Material, Batch> groups, List<GroveExclusion> exclusions)
        {
            Mesh oak = LoadObj("tree_oak", true);
            if (oak == null) return;
            var rng = new System.Random(course.Seed ^ 0x176b2);
            float[] bands = { 112, 166, 194 };
            for (float station = 75; station < course.Length + 150; station += 96)
                for (int side = -1; side <= 1; side += 2)
                    for (int band = 0; band < bands.Length; band++)
                    {
                        float z = station + (band == 1 ? 16 : -8);
                        CourseSample road = ExtendedSample(course, z);
                        Vector3 center = new Vector3(road.Position.x + side * bands[band], 0, z);
                        // A local contour tangent keeps each small planted group level across the hill.
                        float dx = GroundHeight(course, center.x + 1, center.z) - GroundHeight(course, center.x - 1, center.z);
                        float dz = GroundHeight(course, center.x, center.z + 1) - GroundHeight(course, center.x, center.z - 1);
                        Vector2 contour = new Vector2(-dz, dx).normalized;
                        if (contour.sqrMagnitude < .1f) contour = Vector2.right;
                        Vector2 crossContour = new Vector2(-contour.y, contour.x);
                        int planted = 0;
                        for (int tree = 0; tree < 5; tree++)
                        {
                            float along = (tree - 2) * 6.4f;
                            float stagger = tree % 2 == 0 ? -2.4f : 2.4f;
                            Vector2 offset = contour * along + crossContour * stagger;
                            Vector3 p = center + new Vector3(offset.x, 0, offset.y);
                            float height = Next(rng, 8.6f, 11.2f);
                            float factor = height / oak.bounds.size.y;
                            float crownRadius = Mathf.Max(oak.bounds.extents.x, oak.bounds.extents.z) * factor;
                            if (!GroveClear(course, exclusions, p, crownRadius + .9f)) continue;
                            p.y = GroundHeight(course, p.x, p.z) - oak.bounds.min.y * factor;
                            var matrix = Matrix4x4.TRS(p, Quaternion.Euler(0, Next(rng, 0, 360), 0), Vector3.one * factor);
                            Add(groups, groveLeaves, oak, matrix, 0);
                            Add(groups, bark, oak, matrix, 1);
                            planted++;
                        }
                        if (planted < 2) continue;
                        Quaternion direction = Quaternion.LookRotation(new Vector3(contour.x, 0, contour.y), Vector3.up);
                        for (int segment = 0; segment < 5; segment++)
                        {
                            Vector2 offset = contour * ((segment - 2) * 5.6f) + crossContour * 5.4f;
                            Vector3 p = center + new Vector3(offset.x, 0, offset.y);
                            if (!GroveClear(course, exclusions, p, 4)) continue;
                            p.y = GroundHeight(course, p.x, p.z);
                            AddBox(groups, rock, p + Vector3.down * .27f, new Vector3(.75f, .8f, 5.7f), direction);
                            AddBox(groups, groveLeaves, p + Vector3.up * .03f,
                                new Vector3(3.7f, .07f, 5.7f), direction);
                        }
                    }
        }

        static void AddBox(Dictionary<Material, Batch> groups, Material material, Vector3 p, Vector3 scale, Quaternion rotation)
            => Add(groups, material, cube, Matrix4x4.TRS(p, rotation, scale));

        static void Add(Dictionary<Material, Batch> groups, Material material, Mesh mesh, Matrix4x4 matrix, int submesh = 0)
        {
            if (!groups.TryGetValue(material, out Batch batch)) groups[material] = batch = new Batch();
            batch.Parts.Add(new CombineInstance { mesh = mesh, transform = matrix, subMeshIndex = submesh });
        }

        sealed class Batch
        {
            public readonly List<CombineInstance> Parts = new List<CombineInstance>();
            public void Flush(Transform parent, Material material)
            {
                // Bound each renderer so long downhill runs can cull distant districts.
                for (int start = 0; start < Parts.Count; start += 35)
                {
                    int count = Mathf.Min(35, Parts.Count - start);
                    var mesh = new Mesh { name = "Background " + material.name, indexFormat = IndexFormat.UInt32 };
                    mesh.CombineMeshes(Parts.GetRange(start, count).ToArray(), true, true);
                    mesh.RecalculateBounds();
                    var go = new GameObject(material.name + " district " + start / 35);
                    go.transform.SetParent(parent, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true;
                }
            }
        }

        static Mesh LoadObj(string name, bool tree)
        {
            if (sourceMeshes.TryGetValue(name, out Mesh loaded) && loaded != null) return loaded;
            var asset = Resources.Load<TextAsset>(ResourceRoot + name);
            if (asset == null) { Debug.LogWarning("Missing licensed background model: " + name); return null; }
            var source = new List<Vector3>(); var normals = new List<Vector3>(); var coords = new List<Vector2>();
            var vertices = new List<Vector3>(); var outputNormals = new List<Vector3>(); var uv = new List<Vector2>();
            var leaves = new List<int>(); var wood = new List<int>(); bool woodPart = false;
            foreach (string raw in asset.text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                string[] words = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (words[0] == "v") source.Add(new Vector3(Number(words[1]), Number(words[2]), Number(words[3])));
                else if (words[0] == "vn") normals.Add(new Vector3(Number(words[1]), Number(words[2]), Number(words[3])));
                else if (words[0] == "vt") coords.Add(new Vector2(Number(words[1]), Number(words[2])));
                else if (words[0] == "usemtl") woodPart = words[1].StartsWith("wood", StringComparison.OrdinalIgnoreCase);
                else if (words[0] == "f")
                {
                    int first = vertices.Count;
                    for (int i = 1; i < words.Length; i++)
                    {
                        string[] indices = words[i].Split('/');
                        vertices.Add(source[Index(indices[0], source.Count)]);
                        uv.Add(indices.Length > 1 && indices[1].Length > 0 ? coords[Index(indices[1], coords.Count)] : Vector2.zero);
                        outputNormals.Add(indices.Length > 2 && indices[2].Length > 0 ? normals[Index(indices[2], normals.Count)] : Vector3.up);
                    }
                    List<int> triangles = tree && woodPart ? wood : leaves;
                    for (int i = 2; i < words.Length - 1; i++) triangles.AddRange(new[] { first, first + i - 1, first + i });
                }
            }
            var mesh = new Mesh { name = "Kenney " + name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetNormals(outputNormals);
            mesh.subMeshCount = tree ? 2 : 1; mesh.SetTriangles(leaves, 0);
            if (tree) mesh.SetTriangles(wood, 1);
            mesh.RecalculateBounds(); sourceMeshes[name] = mesh;
            return mesh;
        }

        static float Number(string value) => float.Parse(value, CultureInfo.InvariantCulture);
        static int Index(string value, int length) { int parsed = int.Parse(value, CultureInfo.InvariantCulture); return parsed < 0 ? length + parsed : parsed - 1; }

        static void Materials()
        {
            if (land != null) return;
            land = Mat("Olive valley ground", "#8C9568"); olive = Mat("Distant sage ridges", "#929B80");
            rock = Mat("Terrace limestone", "#A7A38B"); roof = Mat("Skyline terracotta", "#A46E52");
            chalk = Mat("Skyline warm plaster", "#D1BD97"); opening = Mat("Skyline shadowed openings", "#465652");
            peach = Mat("Far rose limewash", "#BC9780"); ochre = Mat("Far ochre limewash", "#C6AD7E");
            foliage = Mat("Garden olive leaves", "#647D55"); bark = Mat("Garden tree bark", "#8B7153");
            groveLeaves = Mat("Terraced olive leaves", "#4C6749");
            // Seasonal scenery sets the leaf hue; this neutral mask preserves a darker grove layer.
            var groveShade = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "Olive grove shade", filterMode = FilterMode.Point };
            groveShade.SetPixels(new[] { new Color(.65f,.68f,.62f),new Color(.67f,.70f,.64f),
                new Color(.67f,.70f,.64f),new Color(.65f,.68f,.62f) });
            groveShade.Apply(); groveLeaves.mainTexture = groveShade;
            city = Mat("Kenney distant neighbourhood", "#EEDBC1");
            var palette = Resources.Load<Texture2D>(ResourceRoot + "city-colormap");
            if (palette != null) { palette.filterMode = FilterMode.Point; city.mainTexture = palette; }
            cube = MakeCube(); ridge = MakeRidge();
        }

        static Material Mat(string name, string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color color);
            var result = new Material(Shader.Find("Standard")) { name = name, color = color, enableInstancing = true };
            result.SetFloat("_Glossiness", .03f); return result;
        }

        static Mesh MakeCube()
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            Vector3[] corners = { new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(-.5f,.5f,-.5f),new Vector3(.5f,.5f,-.5f),
                new Vector3(-.5f,-.5f,.5f),new Vector3(.5f,-.5f,.5f),new Vector3(-.5f,.5f,.5f),new Vector3(.5f,.5f,.5f) };
            int[] faces = { 0,2,3,1, 4,5,7,6, 0,4,6,2, 1,3,7,5, 2,6,7,3, 0,1,5,4 };
            for (int f = 0; f < 6; f++)
            {
                int first = vertices.Count; for (int i = 0; i < 4; i++) vertices.Add(corners[faces[f * 4 + i]]);
                triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            }
            var mesh = new Mesh { name = "Backdrop authored cube" }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); return mesh;
        }

        static Mesh MakeRidge()
        {
            // Unit pitched form with separate faces for readable flat lighting.
            Vector3[] source = { new Vector3(-.5f,0,-.5f),new Vector3(.5f,0,-.5f),new Vector3(0,1,-.5f),
                new Vector3(-.5f,0,.5f),new Vector3(.5f,0,.5f),new Vector3(0,1,.5f) };
            int[] indices = { 0,2,1,3,4,5,0,3,5,0,5,2,2,5,4,2,4,1 };
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int i = 0; i < indices.Length; i++) { vertices.Add(source[indices[i]]); triangles.Add(i); }
            var mesh = new Mesh { name = "Backdrop shaped roof and ridge" }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); return mesh;
        }
    }
}
