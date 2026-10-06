using System.Collections.Generic;
using UnityEngine;

namespace DeliveryDash.Downhill
{
    // Builds one physically collidable road mesh from the exact samples used by cart motion.
    public static class CourseMeshBuilder
    {
        private static Material stone, slick, wood, conveyor, curb, wall, soil, water, black, yellow, grass, fluid, fluidGlint, spikeMetal;

        public static GameObject Build(CourseGraph course, Transform parent)
        {
            EnsureMaterials();
            GameObject root = new GameObject("Downhill course");
            if (parent != null) root.transform.SetParent(parent, false);
            BuildRoad(course, root.transform);
            BuildSidewalk(course, root.transform, -1);
            BuildSidewalk(course, root.transform, 1);
            BuildWalls(course, root.transform, -1);
            BuildWalls(course, root.transform, 1);
            BuildTerrain(course, root.transform, -1);
            BuildTerrain(course, root.transform, 1);
            BuildMarks(course, root.transform);
            foreach (CourseBranch branch in course.Branches)
            {
                BuildRoad(course, root.transform, branch.Samples);
                BuildShortcutEdges(branch, root.transform);
                BuildBranchBed(course, branch, root.transform);
            }
            BuildObstacles(course, root.transform);
            BuildApproachSkirt(course, root.transform);
            return root;
        }

        private static void BuildRoad(CourseGraph course, Transform parent, List<CourseSample> source = null)
        {
            List<CourseSample> samples = source ?? course.Samples;
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<int>[] triangles = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
            for (int i = 0; i < samples.Count; i++)
            {
                CourseSample sample = samples[i];
                vertices.Add(CourseGraph.RoadEdge(sample, -1));
                vertices.Add(CourseGraph.RoadEdge(sample, 1));
                uv.Add(new Vector2(0f, sample.Distance * 0.25f));
                uv.Add(new Vector2(2.9f, sample.Distance * 0.25f));
                if (i == 0) continue;
                int materialIndex = SurfaceIndex(samples[i - 1].Surface);
                int previous = (i - 1) * 2;
                int current = i * 2;
                triangles[materialIndex].Add(previous);
                triangles[materialIndex].Add(current);
                triangles[materialIndex].Add(previous + 1);
                triangles[materialIndex].Add(previous + 1);
                triangles[materialIndex].Add(current);
                triangles[materialIndex].Add(current + 1);
            }
            Mesh mesh = NewMesh("Stone road and interaction surfaces", vertices, uv, triangles);
            GameObject road = MakeMeshObject("Drivable downhill road", parent, mesh,
                new[] { stone, slick, wood, conveyor }, true);
            road.layer = 0;
        }

        private static int SurfaceIndex(CourseSurface surface)
        {
            switch (surface)
            {
                case CourseSurface.Slick: return 1;
                case CourseSurface.WoodRamp: return 2;
                case CourseSurface.Conveyor: return 3;
                default: return 0;
            }
        }

        private static void BuildSidewalk(CourseGraph course, Transform parent, int side)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<int>[] tris = { new List<int>(), new List<int>() };
            for (int i = 1; i < course.Samples.Count; i++)
            {
                CourseSample a = course.Samples[i - 1];
                CourseSample b = course.Samples[i];
                if (ForkJunction(course, (a.Distance + b.Distance) * .5f)) continue;
                AddStripSegment(vertices, uv, tris[0], a, b, side * 5.75f, side * 6.15f,
                    0.17f, 0.17f, 0.8f);
                AddStripSegment(vertices, uv, tris[1], a, b, side * 6.15f, side * 7.8f,
                    0.10f, 0.10f, 1.8f);
                AddVerticalSegment(vertices, uv, tris[0], a, b, side * 5.75f, 0f, .17f, side < 0);
                AddVerticalSegment(vertices, uv, tris[0], a, b, side * 6.15f, .10f, .17f, side > 0);
            }
            Mesh mesh = NewMesh(side < 0 ? "Left curb and walk" : "Right curb and walk", vertices, uv, tris);
            MakeMeshObject(side < 0 ? "Left stone curb" : "Right stone curb", parent,
                mesh, new[] { curb, wall }, true);
        }

        private static void BuildWalls(CourseGraph course, Transform parent, int side)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<int>[] tris = { new List<int>(), new List<int>() };
            for (int i = 1; i < course.Samples.Count; i++)
            {
                float d = i - 0.5f;
                if (!HasWall(side, d) || ForkJunction(course, d)) continue;
                CourseSample a = course.Samples[i - 1];
                CourseSample b = course.Samples[i];
                float height = d > 205f && d < 250f ? 0.55f : 1.20f;
                float inner = side * 7.8f;
                float outer = side * 8.25f;
                AddVerticalSegment(vertices, uv, tris[0], a, b, inner, -0.65f, height, side < 0);
                AddStripSegment(vertices, uv, tris[1], a, b, inner, outer,
                    height, height, 0.48f);
            }
            Mesh mesh = NewMesh(side < 0 ? "Left retaining wall" : "Right retaining wall",
                vertices, uv, tris);
            MakeMeshObject(side < 0 ? "Left retaining wall" : "Right retaining wall",
                parent, mesh, new[] { wall, curb }, true);
        }

        private static bool HasWall(int side, float d)
        {
            if (side < 0) return d < 137f || (d > 184f && d < 286f) || d > 367f;
            return d < 73f || (d > 115f && d < 315f) || d > 351f;
        }

        private static bool ForkJunction(CourseGraph course, float distance)
        {
            foreach (CourseBranch branch in course.Branches)
                if ((distance >= branch.StartDistance && distance < branch.StartDistance + 60)
                    || (distance > branch.EndDistance - 60 && distance <= branch.EndDistance)) return true;
            return false;
        }

        private static void BuildShortcutEdges(CourseBranch branch, Transform parent)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<int>[] triangles = { new List<int>() };
            for (int i = 1; i < branch.Samples.Count; i++)
            {
                CourseSample a = branch.Samples[i - 1], b = branch.Samples[i];
                if (a.Distance < branch.StartDistance + 60 || b.Distance > branch.EndDistance - 60) continue;
                foreach (int side in new[] { -1, 1 })
                {
                    Vector3 ar = Vector3.Cross(Vector3.up, a.Forward).normalized;
                    Vector3 br = Vector3.Cross(Vector3.up, b.Forward).normalized;
                    Vector3 ea = CourseGraph.RoadEdge(a, side), eb = CourseGraph.RoadEdge(b, side);
                    Vector3 oa = ea + ar * side * .22f + Vector3.up * .16f;
                    Vector3 ob = eb + br * side * .22f + Vector3.up * .16f;
                    if (side > 0)
                    {
                        AddQuad(vertices, uv, triangles[0], ea, eb, oa, ob, new Vector2(0,a.Distance), new Vector2(1,b.Distance));
                        AddQuad(vertices, uv, triangles[0], ea, eb, ea - Vector3.up * 2.2f, eb - Vector3.up * 2.2f,
                            new Vector2(0,a.Distance), new Vector2(1,b.Distance));
                    }
                    else
                    {
                        AddQuad(vertices, uv, triangles[0], oa, ob, ea, eb, new Vector2(0,a.Distance), new Vector2(1,b.Distance));
                        AddQuad(vertices, uv, triangles[0], ea - Vector3.up * 2.2f, eb - Vector3.up * 2.2f,
                            ea, eb, new Vector2(0,a.Distance), new Vector2(1,b.Distance));
                    }
                }
            }
            MakeMeshObject(branch.Id + " grounded bridge edges", parent,
                NewMesh(branch.Id + " bridge fascia", vertices, uv, triangles), new[] { wood }, false);
        }

        private static void BuildTerrain(CourseGraph course, Transform parent, int side)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<int>[] tris = { new List<int>(), new List<int>() };
            for (int i = 1; i < course.Samples.Count; i++)
            {
                CourseSample a = course.Samples[i - 1];
                CourseSample b = course.Samples[i];
                if (!course.Generated && side < 0 && i > 109 && i < 347)
                    AddStripSegment(vertices, uv, tris[1], a, b, -8.3f, -31f,
                        -3.4f, -3.4f, 5f);
                else
                    AddStripSegment(vertices, uv, tris[0], a, b, side * 8.25f, side * 31f,
                        -0.8f, -1.8f, 5f);
            }
            Mesh mesh = NewMesh(side < 0 ? "Canal and left terrace" : "Right terraced ground",
                vertices, uv, tris);
            course.RegisterOutsideGround(mesh);
            MakeMeshObject(side < 0 ? "Canal and left terrace" : "Right terraced ground",
                parent, mesh, new[] { soil, water }, false);
        }

        private static void BuildBranchBed(CourseGraph course, CourseBranch branch, Transform parent)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<int>[] triangles = { new List<int>() };
            for (int i = 1; i < branch.Samples.Count; i++)
            {
                CourseSample a = branch.Samples[i - 1], b = branch.Samples[i];
                CourseSample ma = course.Sample(a.Distance), mb = course.Sample(b.Distance);
                float leftA = Mathf.Min(CourseGraph.RoadEdge(a,-1).x, CourseGraph.RoadEdge(ma,-1).x) - 14f;
                float rightA = Mathf.Max(CourseGraph.RoadEdge(a,1).x, CourseGraph.RoadEdge(ma,1).x) + 14f;
                float leftB = Mathf.Min(CourseGraph.RoadEdge(b,-1).x, CourseGraph.RoadEdge(mb,-1).x) - 14f;
                float rightB = Mathf.Max(CourseGraph.RoadEdge(b,1).x, CourseGraph.RoadEdge(mb,1).x) + 14f;
                AddQuad(vertices, uv, triangles[0], new Vector3(leftA,ma.Position.y-4,a.Distance),
                    new Vector3(leftB,mb.Position.y-4,b.Distance), new Vector3(rightA,ma.Position.y-4,a.Distance),
                    new Vector3(rightB,mb.Position.y-4,b.Distance), new Vector2(0,a.Distance*.1f),new Vector2(1,b.Distance*.1f));
                if (i % 14 == 0 && i > 60 && i < branch.Samples.Count - 60)
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 edge = CourseGraph.RoadEdge(a,side);
                        float baseY = ma.Position.y-4;
                        float height = edge.y-baseY;
                        GameObject post = new GameObject("Bridge foundation");
                        post.transform.SetParent(parent,false);
                        Part(post.transform, PrimitiveType.Cube,
                            new Vector3(edge.x+side*.17f,baseY+height*.5f,edge.z),new Vector3(.28f,height,.36f),wall);
                    }
            }
            Mesh bed=NewMesh(branch.Id+" lower bed",vertices,uv,triangles);
            course.RegisterOutsideGround(bed);
            MakeMeshObject(branch.Id+" canal and ground bed", parent,bed,
                new[]{course.Branches.IndexOf(branch)==0?water:grass},false);
        }

        private static void BuildMarks(CourseGraph course, Transform parent)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<int>[] tris = { new List<int>(), new List<int>() };
            for (int d = 1; d < course.Length - 1; d++)
            {
                CourseSample sample = course.Sample(d);
                bool start = sample.Event == CourseEvent.Bump && course.Sample(d - 1).Event != CourseEvent.Bump;
                if (start || sample.LaunchCue > .9f || (sample.Event == CourseEvent.Finish && course.Sample(d - 1).Event != CourseEvent.Finish))
                    AddStripedMark(course, vertices, uv, tris, d, .6f, sample.Width - 1);
                if (sample.LaunchCue > .9f) AddArrow(vertices, uv, tris[1], course.Sample(d - 9));
            }
            foreach (CourseBranch branch in course.Branches)
                foreach (CourseSample sample in branch.Samples)
                    if (sample.LaunchCue > .9f)
                    {
                        CourseSample end = CourseGraph.SamplePath(branch.Samples, sample.Distance + .6f);
                        AddStripSegment(vertices, uv, tris[0], sample, end, -3.5f, 3.5f, .025f, .025f, 1);
                        AddArrow(vertices, uv, tris[1], CourseGraph.SamplePath(branch.Samples, sample.Distance - 9));
                    }
            Mesh mesh = NewMesh("Bump ramp and finish markings", vertices, uv, tris);
            MakeMeshObject("Roadside interaction cues", parent, mesh, new[] { black, yellow }, false);
        }

        private static void AddArrow(List<Vector3> vertices, List<Vector2> uv, List<int> triangles, CourseSample sample)
        {
            Vector3 right = Vector3.Cross(Vector3.up, sample.Forward).normalized;
            Vector3 center = sample.Position + Vector3.up * .03f;
            Vector3 tip = center + sample.Forward * .8f;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 tail = center + right * side * 1.6f - sample.Forward * 1.6f;
                Vector3 across = Vector3.Cross(Vector3.up, tip - tail).normalized * .18f;
                AddQuad(vertices, uv, triangles, tail - across, tip - across, tail + across, tip + across,
                    Vector2.zero, Vector2.one);
            }
        }

        private static void BuildApproachSkirt(CourseGraph course, Transform parent)
        {
            CourseSample start = course.Sample(0);
            Vector3 forward = start.Forward;
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<int>[] triangles = { new List<int>(), new List<int>() };
            CourseSample behind = start;
            behind.Position -= forward * 20;
            behind.Distance = -20f * forward.z;
            vertices.AddRange(new[] { CourseGraph.RoadEdge(behind,-1),CourseGraph.RoadEdge(behind,1),
                CourseGraph.RoadEdge(start,-1),CourseGraph.RoadEdge(start,1) });
            uv.AddRange(new[]{new Vector2(0,behind.Distance*.25f),new Vector2(2.9f,behind.Distance*.25f),Vector2.zero,new Vector2(2.9f,0)});
            triangles[0].AddRange(new[]{0,2,1,1,2,3});
            AddStripSegment(vertices, uv, triangles[1], behind, start, -31f, -5.75f, -.6f, -.16f, 5);
            AddStripSegment(vertices, uv, triangles[1], behind, start, 5.75f, 31f, -.16f, -.6f, 5);
            MakeMeshObject("Grounded launch approach", parent, NewMesh("Launch skirt", vertices, uv, triangles), new[] { stone, soil }, false);
        }

        private static void BuildObstacles(CourseGraph course, Transform parent)
        {
            foreach (CourseObstacle obstacle in course.Obstacles)
            {
                GameObject go = new GameObject(obstacle.Kind + " " + obstacle.Id);
                go.transform.SetParent(parent, false);
                go.transform.position = obstacle.Position;
                go.transform.rotation = obstacle.Rotation;
                BoxCollider collider = go.AddComponent<BoxCollider>();
                collider.size = obstacle.Size; collider.center = Vector3.up * obstacle.Size.y * .5f;
                if (obstacle.Kind == ObstacleKind.Fluid)
                {
                    // Rendering and trigger use the identical oriented footprint.
                    // The amber film preserves the paving below and reads as a spill,
                    // rather than replacing an entire section of road with blue ground.
                    collider.isTrigger = true;
                    GameObject film = Part(go.transform, PrimitiveType.Cube,
                        Vector3.up * .035f, new Vector3(obstacle.Size.x, .018f, obstacle.Size.z), fluid);
                    film.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    film.GetComponent<Renderer>().receiveShadows = false;
                    for (int stripe = 0; stripe < 4; stripe++)
                    {
                        float z = (stripe - 1.5f) * obstacle.Size.z * .18f;
                        Part(go.transform, PrimitiveType.Cube,
                            new Vector3(stripe % 2 == 0 ? -.35f : .35f, .047f, z),
                            new Vector3(obstacle.Size.x * .54f, .008f, .075f), fluidGlint);
                    }
                }
                else if (obstacle.Kind == ObstacleKind.Spikes)
                {
                    Part(go.transform, PrimitiveType.Cube, Vector3.up * .035f,
                        new Vector3(obstacle.Size.x, .07f, obstacle.Size.z), black);
                    for (int side = -1; side <= 1; side += 2)
                        Part(go.transform, PrimitiveType.Cube,
                            new Vector3(side * (obstacle.Size.x * .5f - .09f), .077f, 0),
                            new Vector3(.13f, .025f, obstacle.Size.z), yellow);
                    BuildSpikes(go.transform, obstacle.Size);
                }
                else if (obstacle.Kind == ObstacleKind.Barrels)
                {
                    Part(go.transform, PrimitiveType.Cylinder, new Vector3(-.35f,.52f,0), new Vector3(.65f,.52f,.65f), wood);
                    Part(go.transform, PrimitiveType.Cylinder, new Vector3(.35f,.52f,.25f), new Vector3(.65f,.52f,.65f), wood);
                    for (int i = 0; i < 2; i++)
                        Part(go.transform, PrimitiveType.Cylinder, new Vector3(-.35f,.24f+i*.55f,0), new Vector3(.68f,.04f,.68f), black);
                }
                else if (obstacle.Kind == ObstacleKind.Crates)
                {
                    Part(go.transform, PrimitiveType.Cube, new Vector3(-.42f,.42f,0), new Vector3(.95f,.82f,1.35f), wood);
                    Part(go.transform, PrimitiveType.Cube, new Vector3(.48f,.38f,.05f), new Vector3(.88f,.72f,1.25f), wood);
                    Part(go.transform, PrimitiveType.Cube, new Vector3(-.35f,1.17f,0), new Vector3(.90f,.66f,1.12f), wood);
                    for (int z = -1; z <= 1; z += 2)
                        for (int x = -1; x <= 1; x += 2)
                            Part(go.transform, PrimitiveType.Cube, new Vector3(x*.75f,.50f,z*.65f), new Vector3(.1f,1.05f,.13f), yellow);
                }
                else if (obstacle.Kind == ObstacleKind.ParkedCar || obstacle.Kind == ObstacleKind.CrossingCar)
                {
                    float scale = obstacle.Kind == ObstacleKind.CrossingCar ? .86f : 1f;
                    Part(go.transform, PrimitiveType.Cube, new Vector3(0,.50f,0), new Vector3(2.14f,.36f,3.96f) * scale, yellow);
                    Part(go.transform, PrimitiveType.Cube, new Vector3(0,.82f,1.26f) * scale, new Vector3(2.06f,.38f,1.28f) * scale, yellow);
                    Part(go.transform, PrimitiveType.Cube, new Vector3(0,1.13f,-.30f) * scale, new Vector3(1.87f,.87f,2.02f) * scale, black);
                    Part(go.transform, PrimitiveType.Cube, new Vector3(0,1.61f,-.35f) * scale, new Vector3(1.94f,.15f,1.78f) * scale, yellow);
                    Part(go.transform, PrimitiveType.Cube, new Vector3(0,1.31f,.76f) * scale, new Vector3(1.73f,.52f,.05f) * scale, slick)
                        .transform.localRotation = Quaternion.Euler(-16,0,0);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Part(go.transform, PrimitiveType.Cube, new Vector3(side*.944f,1.29f,-.35f) * scale,
                            new Vector3(.05f,.5f,1.45f) * scale, slick);
                        Part(go.transform, PrimitiveType.Cube, new Vector3(side*.70f,.75f,1.93f) * scale,
                            new Vector3(.4f,.19f,.06f) * scale, curb);
                        for (int axle = -1; axle <= 1; axle += 2)
                            Part(go.transform, PrimitiveType.Cylinder, new Vector3(side*1.06f,.35f,axle*1.33f) * scale,
                                new Vector3(.71f,.13f,.71f) * scale, black).transform.localRotation = Quaternion.Euler(0,0,90);
                    }
                    Part(go.transform, PrimitiveType.Cube, new Vector3(0,.49f,2.08f) * scale, new Vector3(2.12f,.16f,.11f) * scale, black);
                    Part(go.transform, PrimitiveType.Cube, new Vector3(0,.49f,-2.08f) * scale, new Vector3(2.12f,.16f,.11f) * scale, black);
                    if (obstacle.Kind == ObstacleKind.CrossingCar) go.AddComponent<CourseObstacleMotion>().Obstacle = obstacle;
                }
                else
                {
                    Part(go.transform, PrimitiveType.Cube, new Vector3(0,.25f,0), new Vector3(1.3f,.18f,2f), black);
                    for (int x = -1; x <= 1; x += 2)
                    {
                        Part(go.transform, PrimitiveType.Cube, new Vector3(x*.62f,.75f,-.75f), new Vector3(.08f,1.05f,.08f), curb);
                        for (int z = -1; z <= 1; z += 2)
                            Part(go.transform, PrimitiveType.Sphere, new Vector3(x*.59f,.13f,z*.75f), new Vector3(.23f,.23f,.23f), black);
                    }
                    Part(go.transform, PrimitiveType.Cube, new Vector3(0,1.2f,-.75f), new Vector3(1.3f,.08f,.08f), curb);
                    Part(go.transform, PrimitiveType.Cube, new Vector3(0,.65f,.25f), new Vector3(1.12f,.7f,1.23f), wood);
                    Part(go.transform, PrimitiveType.Cube, new Vector3(0,1.05f,.25f), new Vector3(1.18f,.15f,1.28f), yellow);
                    go.AddComponent<CourseObstacleMotion>().Obstacle = obstacle;
                }
            }
        }

        private static void BuildSpikes(Transform parent, Vector3 size)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<int> triangles = new List<int>();
            for (int row = 0; row < 3; row++)
            for (int column = 0; column < 3; column++)
            {
                Vector3 center = new Vector3((column - 1) * size.x * .30f, .07f,
                    (row - 1) * size.z * .30f);
                Vector3 tip = new Vector3(center.x, size.y, center.z);
                float radius = Mathf.Min(size.x, size.z) * .125f;
                for (int face = 0; face < 4; face++)
                {
                    float a = face * Mathf.PI * .5f, b = (face + 1) * Mathf.PI * .5f;
                    int start = vertices.Count;
                    vertices.Add(center + new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius));
                    vertices.Add(tip);
                    vertices.Add(center + new Vector3(Mathf.Cos(b) * radius, 0, Mathf.Sin(b) * radius));
                    uv.Add(Vector2.zero); uv.Add(Vector2.up); uv.Add(Vector2.right);
                    triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                }
            }
            MakeMeshObject("Nine visible steel spikes", parent,
                NewMesh("Road spike cluster", vertices, uv, new[] { triangles }), new[] { spikeMetal }, false);
        }

        private static GameObject Part(Transform parent, PrimitiveType type, Vector3 position, Vector3 size, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = go.GetComponent<Collider>();
            if (Application.isPlaying) Object.Destroy(collider); else Object.DestroyImmediate(collider);
            return go;
        }

        private static void AddStripedMark(CourseGraph course, List<Vector3> vertices,
            List<Vector2> uv, List<int>[] tris, float start, float length, float width)
        {
            CourseSample a = course.Sample(start);
            CourseSample b = course.Sample(start + length);
            for (int col = 0; col < 10; col++)
            {
                float lo = -width * 0.5f + width * col / 10f;
                float hi = -width * 0.5f + width * (col + 1) / 10f;
                AddStripSegment(vertices, uv, tris[col % 2], a, b, lo, hi,
                    0.025f, 0.025f, 1f);
            }
        }

        private static void AddStripSegment(List<Vector3> vertices, List<Vector2> uv,
            List<int> triangles, CourseSample a, CourseSample b, float offsetA,
            float offsetB, float heightA, float heightB, float tileScale)
        {
            if (offsetA > offsetB)
            {
                float offset = offsetA; offsetA = offsetB; offsetB = offset;
                float height = heightA; heightA = heightB; heightB = height;
            }
            Vector3 ar = Vector3.Cross(Vector3.up, a.Forward).normalized;
            Vector3 br = Vector3.Cross(Vector3.up, b.Forward).normalized;
            // Sidewalk, canal and wall offsets are measured from the actual road edge.
            float adjustA = a.Width * .5f - 5.75f;
            float adjustB = b.Width * .5f - 5.75f;
            bool outside = Mathf.Abs(offsetA) >= 5.75f && Mathf.Abs(offsetB) >= 5.75f;
            float aLo = offsetA + (outside ? Mathf.Sign(offsetA) * adjustA : 0f);
            float aHi = offsetB + (outside ? Mathf.Sign(offsetB) * adjustA : 0f);
            float bLo = offsetA + (outside ? Mathf.Sign(offsetA) * adjustB : 0f);
            float bHi = offsetB + (outside ? Mathf.Sign(offsetB) * adjustB : 0f);
            Vector3 p0 = a.Position + ar * aLo + Vector3.up * heightA;
            Vector3 p1 = a.Position + ar * aHi + Vector3.up * heightB;
            Vector3 p2 = b.Position + br * bLo + Vector3.up * heightA;
            Vector3 p3 = b.Position + br * bHi + Vector3.up * heightB;
            AddQuad(vertices, uv, triangles, p0, p2, p1, p3,
                new Vector2(0f, a.Distance / tileScale), new Vector2(1f, b.Distance / tileScale));
        }

        private static void AddVerticalSegment(List<Vector3> vertices, List<Vector2> uv,
            List<int> triangles, CourseSample a, CourseSample b, float offset,
            float low, float high, bool reverse)
        {
            Vector3 ar = Vector3.Cross(Vector3.up, a.Forward).normalized;
            Vector3 br = Vector3.Cross(Vector3.up, b.Forward).normalized;
            float offsetA = offset + Mathf.Sign(offset) * (a.Width * .5f - 5.75f);
            float offsetB = offset + Mathf.Sign(offset) * (b.Width * .5f - 5.75f);
            Vector3 p0 = a.Position + ar * offsetA + Vector3.up * low;
            Vector3 p1 = a.Position + ar * offsetA + Vector3.up * high;
            Vector3 p2 = b.Position + br * offsetB + Vector3.up * low;
            Vector3 p3 = b.Position + br * offsetB + Vector3.up * high;
            if (reverse)
                AddQuad(vertices, uv, triangles, p1, p3, p0, p2,
                    new Vector2(0f, a.Distance * 0.15f), new Vector2(1f, b.Distance * 0.15f));
            else
                AddQuad(vertices, uv, triangles, p0, p2, p1, p3,
                    new Vector2(0f, a.Distance * 0.15f), new Vector2(1f, b.Distance * 0.15f));
        }

        private static void AddQuad(List<Vector3> vertices, List<Vector2> uv,
            List<int> triangles, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3,
            Vector2 uv0, Vector2 uv1)
        {
            int index = vertices.Count;
            vertices.Add(p0); vertices.Add(p1); vertices.Add(p2); vertices.Add(p3);
            uv.Add(new Vector2(uv0.x, uv0.y));
            uv.Add(new Vector2(uv0.x, uv1.y));
            uv.Add(new Vector2(uv1.x, uv0.y));
            uv.Add(new Vector2(uv1.x, uv1.y));
            triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
            triangles.Add(index + 2); triangles.Add(index + 1); triangles.Add(index + 3);
        }

        private static Mesh NewMesh(string name, List<Vector3> vertices, List<Vector2> uv,
            List<int>[] submeshes)
        {
            Mesh mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = submeshes.Length;
            for (int i = 0; i < submeshes.Length; i++) mesh.SetTriangles(submeshes[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static GameObject MakeMeshObject(string name, Transform parent, Mesh mesh,
            Material[] materials, bool collider)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        private static void EnsureMaterials()
        {
            if (stone != null) return;
            stone = MakeMaterial("Stone paving", StoneTexture(), Color.white, 0.04f);
            slick = MakeMaterial("Wet stone", SolidTexture(new Color32(79, 120, 136, 255)), Color.white, 0.55f);
            wood = MakeMaterial("Market planks", WoodTexture(), Color.white, 0.08f);
            conveyor = MakeMaterial("Moving belt", BeltTexture(), Color.white, 0.25f);
            curb = MakeMaterial("Limestone curb", SolidTexture(new Color32(211, 190, 163, 255)), Color.white, 0.02f);
            wall = MakeMaterial("Warm masonry", StoneTexture(), new Color(1f, 0.86f, 0.71f), 0.01f);
            soil = MakeMaterial("Terraced earth", SolidTexture(new Color32(144, 112, 91, 255)), Color.white, 0f);
            water = MakeMaterial("Deep canal", SolidTexture(new Color32(49, 117, 137, 255)), Color.white, 0.35f);
            grass = MakeMaterial("Olive terraces", SolidTexture(new Color32(117, 135, 99, 255)), Color.white, 0f);
            black = MakeMaterial("Hazard charcoal", SolidTexture(new Color32(49, 53, 58, 255)), Color.white, 0f);
            yellow = MakeMaterial("Hazard ochre", SolidTexture(new Color32(231, 169, 58, 255)), Color.white, 0f);
            fluid = MakeMaterial("Amber oil film", SolidTexture(new Color32(104, 75, 38, 185)),
                new Color(1f, 1f, 1f, .72f), .88f);
            fluid.SetFloat("_Mode", 3f);
            fluid.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            fluid.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            fluid.SetInt("_ZWrite", 0);
            fluid.DisableKeyword("_ALPHATEST_ON");
            fluid.EnableKeyword("_ALPHABLEND_ON");
            fluid.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            fluid.renderQueue = 3000;
            fluidGlint = MakeMaterial("Oil reflected sunlight", SolidTexture(new Color32(224, 183, 102, 255)), Color.white, .9f);
            spikeMetal = MakeMaterial("Steel spike faces", SolidTexture(new Color32(161, 170, 166, 255)), Color.white, .42f);
            spikeMetal.SetFloat("_Metallic", .38f);
        }

        private static Material MakeMaterial(string name, Texture2D texture, Color tint, float gloss)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");
            Material material = new Material(shader) { name = name, mainTexture = texture, color = tint };
            material.SetFloat("_Glossiness", gloss);
            return material;
        }

        private static Texture2D SolidTexture(Color32 color)
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Color32[] pixels = { color, color, color, color };
            texture.SetPixels32(pixels);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.Apply();
            return texture;
        }

        private static Texture2D StoneTexture()
        {
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[size * size];
            Color32[] palette = {
                new Color32(137, 133, 133, 255), new Color32(154, 145, 139, 255),
                new Color32(125, 129, 137, 255), new Color32(169, 154, 144, 255),
                new Color32(142, 137, 146, 255)
            };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int row = y / 8;
                int shifted = (x + (row % 2) * 4) % size;
                int column = shifted / 8;
                int selector = (row * 7 + column * 11 + row * column * 3) % palette.Length;
                Color32 color = palette[selector];
                int inX = shifted % 8;
                int inY = y % 8;
                if (inX == 0 || inY == 0) color = new Color32(105, 104, 109, 255);
                else if (inY == 1) color = Lighten(color, 11);
                else if (inY == 7 || inX == 7) color = Lighten(color, -10);
                pixels[y * size + x] = color;
            }
            texture.SetPixels32(pixels);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.Apply();
            return texture;
        }

        private static Texture2D WoodTexture()
        {
            Texture2D texture = new Texture2D(32, 64, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[32 * 64];
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 32; x++)
            {
                int row = y / 9;
                Color32 color = row % 3 == 0 ? new Color32(157, 99, 58, 255) :
                    row % 3 == 1 ? new Color32(177, 116, 67, 255) : new Color32(139, 88, 58, 255);
                if (y % 9 == 0) color = new Color32(75, 56, 51, 255);
                else if ((x + row * 7) % 17 == 0) color = Lighten(color, -14);
                pixels[y * 32 + x] = color;
            }
            texture.SetPixels32(pixels);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.Apply();
            return texture;
        }

        private static Texture2D BeltTexture()
        {
            Texture2D texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[32 * 32];
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
                pixels[y * 32 + x] = y % 8 == 0 ? new Color32(175, 165, 141, 255) :
                    new Color32(57, 73, 77, 255);
            texture.SetPixels32(pixels);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.Apply();
            return texture;
        }

        private static Color32 Lighten(Color32 color, int change)
        {
            return new Color32((byte)Mathf.Clamp(color.r + change, 0, 255),
                (byte)Mathf.Clamp(color.g + change, 0, 255),
                (byte)Mathf.Clamp(color.b + change, 0, 255), 255);
        }
    }
}
