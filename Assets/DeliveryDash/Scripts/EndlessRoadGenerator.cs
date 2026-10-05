using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeliveryDash
{
    [DefaultExecutionOrder(-100)]
    public sealed class EndlessRoadGenerator : MonoBehaviour
    {
        [SerializeField] private int seed = 6553;
        [SerializeField] private CartFeelController runner;
        [SerializeField] private GameObject authoredDistrict;
        [SerializeField] private Transform[] houses = new Transform[0];
        [SerializeField] private Material roadMaterial, edgeMaterial, obstacleMaterial;
        [SerializeField, Range(4, 8)] private int sectionsAhead = 6;
        private const int SectionsBehind = 2;
        private readonly Dictionary<long, Section> sections = new Dictionary<long, Section>();
        private readonly List<long> expired = new List<long>();
        private double origin;
        private ChaseCamera chase;
        private CartVisualResponse response;
        private DeliveryShift shift;
        private Material customerMaterial;
        private bool districtWasActive;
        public int Seed => seed;
        public double Distance => runner == null ? 0 : Math.Max(0, origin + runner.transform.position.z);
        public int LoadedSections => sections.Count;
        public double Origin => origin;

        private sealed class Section
        {
            public GameObject root;
            public readonly List<Mesh> meshes = new List<Mesh>();
        }

        public void Configure(CartFeelController cart, GameObject district, Transform[] templates,
            Material road, Material edge, Material obstacle)
        {
            runner = cart; authoredDistrict = district; houses = templates;
            roadMaterial = road; edgeMaterial = edge; obstacleMaterial = obstacle;
        }

        private void Start()
        {
            if (runner == null || roadMaterial == null || edgeMaterial == null || obstacleMaterial == null)
            {
                Debug.LogError("Endless road needs a runner and materials. Use DeliveryDash/Enable endless road.", this);
                enabled = false;
                return;
            }
            chase = FindFirstObjectByType<ChaseCamera>();
            response = runner.GetComponent<CartVisualResponse>();
            shift = GetComponent<DeliveryShift>();
            customerMaterial = new Material(edgeMaterial) { name = "Customer gate green", color = new Color(.18f, .75f, .48f) };
            if (authoredDistrict != null)
            {
                districtWasActive = authoredDistrict.activeSelf;
                authoredDistrict.SetActive(false);
            }
            Restart(seed);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R)) Restart(seed);
            else if (Input.GetKeyDown(KeyCode.N)) Restart(unchecked(seed * 1664525 + 1013904223));
            MaintainRoad();
        }

        public void Restart(int newSeed)
        {
            foreach (Section section in sections.Values) Release(section);
            sections.Clear();
            origin = 0;
            seed = newSeed;
            if (shift != null) shift.BeginRun();
            runner.ResetAt(new Vector3(0, .12f, 0), Quaternion.identity);
            MaintainRoad();
            Physics.SyncTransforms();
            if (response != null) response.ResetMotion();
            if (chase != null) chase.SetTarget(runner.transform);
        }

        public void MaintainRoad()
        {
            if (runner == null) return;
            if (runner.transform.position.z >= 768f)
            {
                double shift = Math.Floor(runner.transform.position.z / 768f) * 768;
                Vector3 delta = new Vector3(0, (float)(-EndlessRoadLayout.Grade * shift), (float)shift);
                origin += shift;
                foreach (Section section in sections.Values) section.root.transform.position -= delta;
                runner.ShiftWorld(delta);
                if (response != null) response.ShiftWorld(delta);
                if (chase != null) chase.ShiftWorld(delta);
                Physics.SyncTransforms();
            }
            double station = origin + runner.transform.position.z;
            long current = (long)Math.Floor(station / EndlessRoadLayout.Length);
            long first = Math.Max(-1, current - SectionsBehind);
            long last = current + sectionsAhead;
            expired.Clear();
            foreach (var entry in sections)
                if (entry.Key < first || entry.Key > last) expired.Add(entry.Key);
            foreach (long key in expired) { Release(sections[key]); sections.Remove(key); }
            for (long i = first; i <= last; i++)
                if (!sections.ContainsKey(i)) sections.Add(i, BuildSection(i));

            Vector3 road = EndlessRoadLayout.Sample(seed, station, origin, out float width);
            if (runner.transform.position.y < road.y - 3f || Math.Abs(runner.transform.position.x - road.x) > width + 4)
                RecoverToRoad();
        }

        public void RecoverToRoad()
        {
            // Move past the obstacle instead of repeatedly resetting into its face.
            double station = Distance + 5;
            Vector3 road = EndlessRoadLayout.Sample(seed, station, origin, out float width);
            var kind = EndlessRoadLayout.Kind(seed, (long)Math.Floor(station / EndlessRoadLayout.Length));
            if (kind == EndlessRoadLayout.Beat.Median) road.x += width * .60f;
            runner.ResetAt(road + Vector3.up * .15f, Quaternion.identity);
            if (response != null) response.ResetMotion();
            if (chase != null) chase.SetTarget(runner.transform);
        }

        private Section BuildSection(long index)
        {
            var section = new Section { root = new GameObject($"Road {index} - {EndlessRoadLayout.Kind(seed, index)}") };
            section.root.transform.SetParent(transform, false);
            double start = index * (double)EndlessRoadLayout.Length;
            // Local vertices remain small even after many origin shifts.
            section.root.transform.position = new Vector3(0, (float)(-EndlessRoadLayout.Grade * (start - origin)), (float)(start - origin));
            AddRibbon(section, index, -1, 1, 0, 0, roadMaterial, true, "Road");
            AddRibbon(section, index, -1, -1, -1.8f, 0, edgeMaterial, true, "Left sidewalk", .12f);
            AddRibbon(section, index, 1, 1, 0, 1.8f, edgeMaterial, true, "Right sidewalk", .12f);
            AddRibbon(section, index, -1, -1, -1.9f, -1.65f, edgeMaterial, true, "Left parapet", .9f, true);
            AddRibbon(section, index, 1, 1, 1.65f, 1.9f, edgeMaterial, true, "Right parapet", .9f, true);
            AddRibbon(section, index, -1, 1, -32, 32, edgeMaterial, false, "Hillside", -1.5f);

            var kind = EndlessRoadLayout.Kind(seed, index);
            if (index > 0 && index % 6 == 0)
            {
                Vector3 gate = LocalPoint(start, start, out float gateWidth);
                Box(section, "Customer gate left", gate + new Vector3(-gateWidth - .45f, 2.3f, 0), new Vector3(.45f, 4.6f, .45f), customerMaterial);
                Box(section, "Customer gate right", gate + new Vector3(gateWidth + .45f, 2.3f, 0), new Vector3(.45f, 4.6f, .45f), customerMaterial);
                Box(section, "Customer gate arch", gate + new Vector3(0, 4.5f, 0), new Vector3(gateWidth * 2 + 1.4f, .5f, .45f), customerMaterial);
                var sign = new GameObject("Customer sign");
                sign.transform.SetParent(section.root.transform, false);
                sign.transform.localPosition = gate + new Vector3(0, 4.5f, -.26f);
                sign.transform.localRotation = Quaternion.identity;
                var label = sign.AddComponent<TextMesh>();
                label.text = "CUSTOMER / +15s";
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
                label.anchor = TextAnchor.MiddleCenter;
                label.fontSize = 48; label.characterSize = .085f; label.color = Color.white;
            }
            if (kind == EndlessRoadLayout.Beat.Market)
            {
                int side = (EndlessRoadLayout.Hash(seed, index, 85) & 1) == 0 ? -1 : 1;
                for (int j = 0; j < 2; j++)
                {
                    double s = start + 17 + j * 17;
                    Vector3 point = LocalPoint(s, start, out float width);
                    point.x += side * (width - 1.2f);
                    Box(section, "Market crate barrier", point + Vector3.up * .55f, new Vector3(2.3f, 1.1f, 2), obstacleMaterial);
                    for (int slat = 0; slat < 3; slat++)
                        Box(section, "Crate wooden slat", point + new Vector3(0, .2f + slat * .35f, -1.01f),
                            new Vector3(2.32f, .075f, .04f), edgeMaterial, false);
                    for (int sidePost = -1; sidePost <= 1; sidePost += 2)
                        Box(section, "Crate corner brace", point + new Vector3(sidePost * 1.04f, .55f, -1.03f),
                            new Vector3(.12f, 1.12f, .06f), edgeMaterial, false);
                    side = -side;
                }
            }
            else if (kind == EndlessRoadLayout.Beat.Median)
            {
                for (int j = 0; j < 3; j++)
                {
                    Vector3 point = LocalPoint(start + 20 + j * 3, start, out _);
                    Box(section, "Split passage planter", point + Vector3.up * .45f, new Vector3(1.5f, .9f, 2.8f), obstacleMaterial);
                    Box(section, "Planter rim", point + Vector3.up * .85f, new Vector3(1.6f, .13f, 2.9f), edgeMaterial, false);
                    Box(section, "Trimmed planter hedge", point + Vector3.up * 1.05f, new Vector3(1.3f, .4f, 2.5f), customerMaterial, false);
                }
            }
            // Reuse the authored facades and shared materials, keeping the current art style.
            if (houses != null && houses.Length > 0)
            {
                for (int j = 0; j < 4; j++)
                {
                    int side = j % 2 == 0 ? -1 : 1;
                    uint hash = EndlessRoadLayout.Hash(seed, index, (uint)(200 + j));
                    Transform template = houses[(int)(hash % (uint)houses.Length)];
                    if (template == null) continue;
                    Vector3 point = LocalPoint(start + 10 + (j / 2) * 24, start, out float width);
                    point.x += side * (width + 7);
                    Transform house = Instantiate(template, section.root.transform);
                    house.localPosition = point;
                    house.localRotation = Quaternion.Euler(0, Math.Sign(template.localPosition.x) == side ? 0 : 180, 0);
                    house.gameObject.SetActive(true);
                }
            }
            return section;
        }

        private Vector3 LocalPoint(double station, double start, out float width) =>
            EndlessRoadLayout.Sample(seed, station, start, out width);

        private void AddRibbon(Section section, long index, float leftFactor, float rightFactor,
            float leftExtra, float rightExtra, Material material, bool solid, string label, float lift = 0, bool wall = false)
        {
            const int steps = 32;
            int stride = wall ? 4 : 2;
            var vertices = new Vector3[(steps + 1) * stride];
            var uv = new Vector2[vertices.Length];
            var triangles = new List<int>();
            double start = index * (double)EndlessRoadLayout.Length;
            for (int i = 0; i <= steps; i++)
            {
                Vector3 p = LocalPoint(start + i * (EndlessRoadLayout.Length / steps), start, out float width);
                vertices[i * stride] = p + new Vector3(leftFactor * width + leftExtra, lift, 0);
                vertices[i * stride + 1] = p + new Vector3(rightFactor * width + rightExtra, lift, 0);
                if (wall)
                {
                    vertices[i * stride + 2] = vertices[i * stride] - Vector3.up * lift;
                    vertices[i * stride + 3] = vertices[i * stride + 1] - Vector3.up * lift;
                }
                for (int v = 0; v < stride; v++) uv[i * stride + v] = new Vector2(v % 2, i * .25f);
                if (i == steps) continue;
                int a = i * stride, b = a + stride;
                Quad(triangles, a, b, a + 1, b + 1);
                if (wall)
                {
                    Quad(triangles, a + 2, b + 2, a, b);
                    Quad(triangles, a + 1, b + 1, a + 3, b + 3);
                }
            }
            Mesh mesh = new Mesh { name = label + " streamed mesh" };
            mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            section.meshes.Add(mesh);
            var go = new GameObject(label);
            go.transform.SetParent(section.root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (solid) go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        static void Quad(List<int> t, int a, int b, int c, int d)
        {
            t.Add(a); t.Add(b); t.Add(c); t.Add(c); t.Add(b); t.Add(d);
        }

        static void Box(Section section, string label, Vector3 position, Vector3 scale, Material material, bool solid = true)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = label; go.transform.SetParent(section.root.transform, false);
            go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid)
            {
                var collider = go.GetComponent<Collider>();
                collider.enabled = false;
                Destroy(collider);
            }
        }

        static void Release(Section section)
        {
            section.root.SetActive(false);
            Destroy(section.root);
            foreach (Mesh mesh in section.meshes) Destroy(mesh);
        }

        private void OnDestroy()
        {
            foreach (Section section in sections.Values)
                foreach (Mesh mesh in section.meshes) if (mesh != null) Destroy(mesh);
            if (authoredDistrict != null) authoredDistrict.SetActive(districtWasActive);
            if (customerMaterial != null) Destroy(customerMaterial);
        }

        private void OnGUI()
        {
            if (shift != null) return;
            GUI.Box(new Rect(16, 16, 300, 76), "Endless delivery route");
            GUI.Label(new Rect(28, 40, 285, 22), $"Distance {Distance:N0} m   |   Seed {seed}");
            GUI.Label(new Rect(28, 62, 285, 22), "A/D steer   R restart   N new route");
        }
    }
}
