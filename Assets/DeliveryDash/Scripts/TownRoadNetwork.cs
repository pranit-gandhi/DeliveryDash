using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeliveryDash
{
    // Integer keys are streaming identities, not road coordinates. Roads are a sparse
    // triangular graph with displaced nodes, curved edges and occasional plaza nodes.
    public sealed class TownRoadNetwork
    {
        public const float CellX = 120, CellZ = 104;
        public const int Samples = 32;
        public readonly int Seed;
        readonly Dictionary<string, Road> roads = new Dictionary<string, Road>();
        readonly Dictionary<TownAddress, Lot> lots = new Dictionary<TownAddress, Lot>();
        public TownRoadNetwork(int seed) { Seed = seed; }
        public static int Mod3(int value) => (value % 3 + 3) % 3;
        uint Hash(Vector2Int key, uint salt) => EndlessRoadLayout.Hash(Seed, ((long)key.x << 32) ^ (uint)key.y, salt);
        float Unit(Vector2Int key, uint salt) => (Hash(key, salt) & 0xffffff) / 16777215f;
        public static Vector3 Offset(Vector2Int key, Vector2Int origin) => new Vector3((key.x - origin.x) * CellX, 0, (key.y - origin.y) * CellZ);
        public Vector3 Node(Vector2Int key, Vector2Int origin)
        {
            Vector3 offset = key == Vector2Int.zero ? Vector3.zero : new Vector3(
                ((key.y & 1) == 1 ? CellX * .5f : 0) + (Unit(key, 700) - .5f) * 38,
                0, (Unit(key, 701) - .5f) * 34);
            return Offset(key, origin) + offset;
        }
        public Vector2Int CellAt(Vector3 p, Vector2Int origin)
        {
            int z = origin.y + Mathf.RoundToInt(p.z / CellZ);
            return new Vector2Int(origin.x + Mathf.RoundToInt((p.x - ((z & 1) == 1 ? CellX * .5f : 0)) / CellX), z);
        }
        public bool Plaza(Vector2Int key) => key == Vector2Int.zero || Hash(key, 703) % 9 == 0;
        public float JunctionRadius(Vector2Int key)
        {
            float width=0;foreach(var neighbor in Neighbors(key)){var road=Edge(key,neighbor);width=Mathf.Max(width,road.a==key?road.widthA:road.widthB);}
            return width+(Plaza(key)?2.4f:1.2f);
        }
        public IEnumerable<Vector2Int> Outgoing(Vector2Int key)
        {
            // Guaranteed three-cell backbone keeps every district connected. Missing
            // side streets create T junctions; extra diagonals create alternate loops.
            bool east = Mod3(key.x) != 1 || Mod3(key.y) == 0 || Hash(key, 710) % 4 != 0;
            if (east) yield return key + Vector2Int.right;
            if (!east || Mod3(key.x) == 0 || Hash(key, 711) % 4 != 0) yield return key + Vector2Int.up;
            if (Hash(key, 712) % 100 < 34) yield return key + new Vector2Int((key.y & 1) == 0 ? -1 : 1, 1);
        }
        public IEnumerable<Vector2Int> Neighbors(Vector2Int key)
        {
            foreach (var end in Outgoing(key)) yield return end;
            var possible = new[] { key + Vector2Int.left, key + Vector2Int.down,
                key + new Vector2Int((key.y & 1) == 0 ? -1 : 1, -1) };
            foreach (var start in possible) foreach (var end in Outgoing(start)) if (end == key) yield return start;
        }
        public Road Edge(Vector2Int a, Vector2Int b)
        {
            if (b.y < a.y || (b.y == a.y && b.x < a.x)) { Vector2Int swap = a; a = b; b = swap; }
            string id = a.x + ":" + a.y + "/" + b.x + ":" + b.y;
            if (roads.TryGetValue(id, out var result)) return result;
            // Bound cache growth independently from the streamed scene.
            if (roads.Count > 2048) roads.Clear();
            bool main=(a.y==b.y && Mod3(a.y)==0) || (a.x==b.x && Mod3(a.x)==0);
            bool lane=a.x!=b.x && a.y!=b.y;
            float baseWidth=main?4.2f:lane?2.8f:3.2f;
            result = new Road(a, b, Node(a, a), Node(b, a),
                (Unit(a, (uint)(730 + b.x - a.x)) - .5f) * 18,
                baseWidth + Unit(a, (uint)(740 + b.x - a.x)) * .5f,
                baseWidth + Unit(b, (uint)(750 + b.x - a.x)) * .5f);
            roads[id] = result;
            return result;
        }
        public IEnumerable<Road> RoadsNear(Vector2Int center, int radius)
        {
            for (int x = -radius; x <= radius; x++) for (int z = -radius; z <= radius; z++)
            {
                Vector2Int key = center + new Vector2Int(x, z);
                foreach (var end in Outgoing(key)) yield return Edge(key, end);
            }
        }
        public sealed class Road
        {
            public readonly Vector2Int a, b;
            public readonly Vector3[] points = new Vector3[Samples + 1];
            public readonly float[] distances = new float[Samples + 1];
            public readonly float widthA, widthB;
            public float Length => distances[Samples];
            public Road(Vector2Int a, Vector2Int b, Vector3 start, Vector3 end, float bend, float widthA, float widthB)
            {
                this.a = a; this.b = b; this.widthA = widthA; this.widthB = widthB;
                Vector3 delta = end - start, right = Vector3.Cross(Vector3.up, delta.normalized);
                Vector3 c1 = start + delta * .32f + right * bend, c2 = end - delta * .32f + right * bend;
                for (int i = 0; i <= Samples; i++)
                {
                    float t = i / (float)Samples, u = 1 - t;
                    points[i] = u * u * u * start + 3 * u * u * t * c1 + 3 * u * t * t * c2 + t * t * t * end;
                    if (i > 0) distances[i] = distances[i - 1] + Vector3.Distance(points[i - 1], points[i]);
                }
            }
            public Vector3 Point(float t, Vector2Int origin)
            {
                float index = Mathf.Clamp01(t) * Samples; int i = Mathf.Min(Samples - 1, (int)index);
                return Vector3.Lerp(points[i], points[i + 1], index - i) + Offset(a, origin);
            }
            public Vector3 Direction(float t)
            {
                int i = Mathf.Clamp((int)(t * Samples), 0, Samples - 1);
                return (points[i + 1] - points[i]).normalized;
            }
            public float Width(float t) => Mathf.Lerp(widthA, widthB, Mathf.SmoothStep(0, 1, t));
            public float Arc(float t)
            {
                float i = Mathf.Clamp01(t) * Samples; int j = Mathf.Min(Samples - 1, (int)i);
                return Mathf.Lerp(distances[j], distances[j + 1], i - j);
            }
            public float Project(Vector3 p, Vector2Int origin, out Vector3 nearest)
            {
                Vector3 local = p - Offset(a, origin); float best = float.PositiveInfinity, bestT = 0; nearest = points[0];
                for (int i = 0; i < Samples; i++)
                {
                    Vector3 d = points[i + 1] - points[i];
                    float t = Mathf.Clamp01(Vector3.Dot(local - points[i], d) / d.sqrMagnitude);
                    Vector3 q = points[i] + d * t; float distance = (local - q).sqrMagnitude;
                    if (distance < best) { best = distance; bestT = (i + t) / Samples; nearest = q; }
                }
                nearest += Offset(a, origin); return bestT;
            }
        }
        public sealed class Lot
        {
            public Road road;
            public float t, setback, scale;
            public int side;
            public Vector3 Direction => road.Direction(t);
            public Vector3 Right => Vector3.Cross(Vector3.up, Direction) * side;
            public Vector3 Stop(Vector2Int origin) => road.Point(t, origin) + Right * (road.Width(t) - 1.25f);
            public Vector3 Building(Vector2Int origin) => road.Point(t, origin) + Right * setback;
        }
        public Lot Address(TownAddress address)
        {
            if (lots.TryGetValue(address, out var saved)) return saved;
            if (lots.Count > 1024) lots.Clear();
            var choices = new List<Road>();
            foreach (var neighbor in Outgoing(address.block)) choices.Add(Edge(address.block, neighbor));
            int first = (int)(Hash(address.block, (uint)(810 + address.side)) % (uint)choices.Count);
            Lot best = null; float bestClearance = float.NegativeInfinity;
            var nearby = new List<Road>(RoadsNear(address.block, 1));
            for (int r = 0; r < choices.Count; r++)
            {
                Road road = choices[(r + first) % choices.Count];
                for (int sample = 0; sample < 3; sample++)
                {
                    float t = .37f + sample * .13f + (Unit(address.block, (uint)(830 + address.side)) - .5f) * .07f;
                    var candidate = new Lot { road = road, t = t, side = address.side,
                        setback = road.Width(t) + 9 + Unit(address.block, 831) * 2,
                        scale = .82f + Unit(address.block, (uint)(840 + address.side)) * .15f };
                    Vector3 center = candidate.Building(address.block); float clearance = float.PositiveInfinity;
                    foreach (Road other in nearby)
                    {
                        float at = other.Project(center, address.block, out Vector3 onRoad);
                        clearance = Mathf.Min(clearance, Vector3.Distance(center, onRoad) - other.Width(at));
                    }
                    // A conservative circular lot envelope covers the replaceable house.
                    if (clearance > bestClearance) { best = candidate; bestClearance = clearance; }
                    if (clearance >= 8) { lots[address] = candidate; return candidate; }
                }
            }
            if (bestClearance < 7.5f) throw new InvalidOperationException("No safe roadside lot for " + address.Label + " seed " + Seed);
            lots[address] = best; return best;
        }
        public Road NearestRoad(Vector3 p, Vector2Int origin, out float t, out Vector3 nearest)
        {
            Road best = null; float distance = float.PositiveInfinity; t = 0; nearest = p;
            foreach (Road road in RoadsNear(CellAt(p, origin), 1))
            {
                float at = road.Project(p, origin, out Vector3 q); float d = (p - q).sqrMagnitude;
                if (d < distance) { distance = d; best = road; t = at; nearest = q; }
            }
            return best;
        }
        static void AppendSection(List<Vector3> result, Road road, float from, float to, Vector2Int origin)
        {
            result.Add(road.Point(from, origin));
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(to - from) * Samples));
            for (int i = 1; i <= steps; i++) result.Add(road.Point(Mathf.Lerp(from, to, i / (float)steps), origin));
        }
        public List<Vector3> Route(Vector3 from, Vector3 to, Vector2Int origin)
        {
            from.y = to.y = 0;
            Road start = NearestRoad(from, origin, out float startT, out Vector3 startPoint);
            Road end = NearestRoad(to, origin, out float endT, out Vector3 endPoint);
            var distance = new Dictionary<Vector2Int, float>();
            var parent = new Dictionary<Vector2Int, Vector2Int>();
            var open = new HashSet<Vector2Int>(); var closed = new HashSet<Vector2Int>();
            distance[start.a] = start.Arc(startT); distance[start.b] = start.Length - start.Arc(startT);
            open.Add(start.a); open.Add(start.b);
            Vector2Int goal = end.a; bool found = false;
            for (int iteration = 0; open.Count > 0 && iteration < 4096; iteration++)
            {
                Vector2Int node = default; float best = float.PositiveInfinity;
                foreach (var candidate in open)
                {
                    float score = distance[candidate] + Vector3.Distance(Node(candidate, origin), endPoint);
                    if (score < best || (Mathf.Approximately(score, best) && (candidate.x < node.x || (candidate.x == node.x && candidate.y < node.y)))) { best = score; node = candidate; }
                }
                open.Remove(node);
                if (node == end.a || node == end.b) { goal = node; found = true; break; }
                closed.Add(node);
                foreach (var next in Neighbors(node))
                {
                    if (closed.Contains(next)) continue;
                    float cost = distance[node] + Edge(node, next).Length;
                    if (!distance.TryGetValue(next, out float old) || cost < old)
                    { distance[next] = cost; parent[next] = node; open.Add(next); }
                }
            }
            var result = new List<Vector3> { from, startPoint };
            if (start.a == end.a && start.b == end.b)
            {
                AppendSection(result, start, startT, endT, origin); result.Add(to); return result;
            }
            if (!found) return new List<Vector3>();
            var nodes = new List<Vector2Int> { goal };
            while (parent.TryGetValue(nodes[nodes.Count - 1], out Vector2Int previous)) nodes.Add(previous);
            nodes.Reverse();
            AppendSection(result, start, startT, nodes[0] == start.a ? 0 : 1, origin);
            for (int i = 1; i < nodes.Count; i++)
            {
                Road road = Edge(nodes[i - 1], nodes[i]);
                AppendSection(result, road, road.a == nodes[i - 1] ? 0 : 1, road.a == nodes[i] ? 0 : 1, origin);
            }
            AppendSection(result, end, goal == end.a ? 0 : 1, endT, origin);
            result.Add(endPoint); result.Add(to); return result;
        }
    }
}
