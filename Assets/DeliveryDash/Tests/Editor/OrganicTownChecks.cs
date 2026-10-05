using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DeliveryDash.Editor
{
    public static class OrganicTownChecks
    {
        static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException("Organic town: " + message); }
        [MenuItem("DeliveryDash/Checks/Validate organic graph topology")]
        public static void Topology()
        {
            int totalRoads = 0, tJunctions = 0, plazas = 0;
            for (int seed = 0; seed < 200; seed++)
            {
                var network = new DeliveryDash.TownRoadNetwork(seed);
                var visited = new HashSet<Vector2Int> { Vector2Int.zero }; var pending = new Queue<Vector2Int>(); pending.Enqueue(Vector2Int.zero);
                while (pending.Count > 0)
                {
                    var node = pending.Dequeue();
                    foreach (var next in network.Neighbors(node))
                    {
                        Check(new List<Vector2Int>(network.Neighbors(next)).Contains(node), "asymmetric road");
                        if (Mathf.Abs(next.x) <= 5 && Mathf.Abs(next.y) <= 5 && visited.Add(next)) pending.Enqueue(next);
                    }
                }
                for (int x = -3; x <= 3; x++) for (int z = -3; z <= 3; z++)
                {
                    var node = new Vector2Int(x, z); Check(visited.Contains(node), "disconnected district");
                    int degree = new List<Vector2Int>(network.Neighbors(node)).Count;
                    if (degree == 3) tJunctions++;
                    if (network.Plaza(node)) plazas++;
                    foreach (var end in network.Outgoing(node))
                    {
                        var road = network.Edge(node, end); totalRoads++;
                        Check(road.Length > 55 && road.Length < 190, "unreadable segment length");
                        Check(Vector3.Distance(road.Point(0, node), network.Node(node, node)) < .001f && Vector3.Distance(road.Point(1, node), network.Node(end, node)) < .001f, "junction endpoint mismatch");
                        for (int i = 1; i < DeliveryDash.TownRoadNetwork.Samples; i++)
                        {
                            float t = i / (float)DeliveryDash.TownRoadNetwork.Samples;
                            Check(road.Width(t) >= 2.8f && road.Width(t) <= 4.7f, "road width bound");
                            Vector3 before = road.points[i] - road.points[i - 1], after = road.points[i + 1] - road.points[i];
                            float curvature = Vector3.Angle(before, after) * Mathf.Deg2Rad / ((before.magnitude + after.magnitude) * .5f);
                            Check(curvature < .035f, "curve too tight for cart");
                        }
                        var other = new DeliveryDash.TownRoadNetwork(seed).Edge(node, end);
                        Check(road.points[16] == other.points[16], "same seed changed curve");
                    }
                    for (int side = -1; side <= 1; side += 2) network.Address(new DeliveryDash.TownAddress(node, side));
                }
                // Connected finite interior has substantially more roads than a tree.
                Check(new List<DeliveryDash.TownRoadNetwork.Road>(network.RoadsNear(Vector2Int.zero, 2)).Count > 30, "missing alternate loops");
            }
            Check(tJunctions > 0 && plazas > 200, "missing junction or plaza variety");
            Debug.Log($"PASS organic graph: 200 seeds, {totalRoads} curves, {tJunctions} T-junctions, {plazas} plazas; connected districts, loops, deterministic curves, bounded widths/curvature, and roadside lots.");
        }

        [MenuItem("DeliveryDash/Checks/Inspect and capture organic town")]
        public static void Capture()
        {
            Check(EditorApplication.isPlaying, "enter Play mode first");
            var world = UnityEngine.Object.FindFirstObjectByType<DeliveryDash.TownDeliveryWorld>();
            var game = UnityEngine.Object.FindFirstObjectByType<DeliveryDash.TownDeliveryGame>();
            Check(world != null && game != null, "open TownDelivery");
            game.Restart(6553); Physics.SyncTransforms(); int samples = 0;
            foreach (var road in world.Network.RoadsNear(world.CurrentBlock, 1))
            {
                for (int i = 0; i <= 100; i++)
                {
                    float t = i / 100f; Vector3 p = road.Point(t, world.Origin);
                    if (Vector3.Distance(p, world.Runner.transform.position) < 3) continue;
                    Check(Physics.Raycast(p + Vector3.up * 3, Vector3.down, out RaycastHit hit, 4), "missing road collision");
                    Check(hit.collider is MeshCollider && hit.point.y > -.02f && hit.point.y < .05f, "road or junction seam missing at " + p + " hit " + hit.collider.name);
                    samples++;
                }
            }
            foreach (var key in new[] { Vector2Int.zero, Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down })
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 p = world.Position(new DeliveryDash.TownAddress(key, side));
                    Check(Physics.Raycast(p + Vector3.up * 3, Vector3.down, out RaycastHit hit, 4) && Mathf.Abs(hit.point.y) < .15f, "inaccessible delivery bay");
                }
            System.IO.Directory.CreateDirectory("Captures");
            var chase = UnityEngine.Object.FindFirstObjectByType<DeliveryDash.ChaseCamera>();
            BuildLookDev.Capture(chase.GetComponent<Camera>(), "Captures/organic-gameplay.png");
            ScreenCapture.CaptureScreenshot("Captures/organic-gameplay-hud.png");
            var go = new GameObject("Temporary layout review camera"); var camera = go.AddComponent<Camera>();
            camera.transform.position = new Vector3(20, 290, -125); camera.transform.LookAt(new Vector3(30, 0, 45));
            camera.fieldOfView = 51; camera.farClipPlane = 700; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.45f,.52f,.55f);
            bool fog = RenderSettings.fog;
            try { RenderSettings.fog = false; BuildLookDev.Capture(camera, "Captures/organic-layout-overview.png"); }
            finally { RenderSettings.fog = fog; UnityEngine.Object.DestroyImmediate(go); }
            Debug.Log($"PASS organic live surfaces: {samples} road/junction collision samples and ten delivery bays. Saved gameplay/HUD and elevated layout captures.");
        }
    }
}
