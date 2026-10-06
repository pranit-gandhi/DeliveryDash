using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeliveryDash.Downhill
{
    public enum CourseSurface { Stone, Slick, WoodRamp, Conveyor }
    public enum CourseEvent { None, Bump, Ramp, Conveyor, Scrape, Finish }

    public struct CourseSample
    {
        public float Distance;
        public Vector3 Position;
        public Vector3 Forward;
        public float Width;
        public float Grip;
        public CourseSurface Surface;
        public CourseEvent Event;
        public float LaunchCue;
        public int RouteId;
    }

    // World +Z is travel direction. Position.Y falls at every metre, including the lip.
    public sealed class CourseGraph
    {
        public const float FeelLength = 480f;
        public readonly List<CourseSample> Samples = new List<CourseSample>(481);
        public readonly List<RouteModule> Modules = new List<RouteModule>(12);
        public readonly List<CourseBranch> Branches = new List<CourseBranch>();
        public readonly List<CourseObstacle> Obstacles = new List<CourseObstacle>();
        readonly Dictionary<List<CourseSample>,ContactStrip[]> contactPaths = new Dictionary<List<CourseSample>,ContactStrip[]>();
        readonly Dictionary<int,List<ContactTriangle>> outsideGround = new Dictionary<int,List<ContactTriangle>>();
        public IReadOnlyList<CourseObstacle> CourseObstacles => Obstacles;
        public float Length { get; internal set; }
        public int Seed { get; internal set; }
        public bool Generated { get; internal set; }
        public bool UsedFallback { get; internal set; }
        public int GenerationAttempts { get; internal set; }
        public string GenerationReport { get; internal set; }
        public string ActionSequence => string.Join(",", Modules.ConvertAll(m => m.Beat.ToString() + (m.Pattern != HazardPattern.None ? ":" + m.Pattern : "")).ToArray());
        public float EstimatedSeconds => PathLength(Samples) / 22f + 5f;
        public static CourseGraph CreateGenerated(int seed) => CourseGenerator.Create(seed);
        public static CourseGraph CreateValidatedFallback(int requestSeed = 73191) => CourseGenerator.CreateFallback(requestSeed);
        public static string ValidateSeedSweep(int count = 1000) => CourseGenerator.ValidateSeeds(count);

        public static CourseGraph CreateFeel(int seed = 2647)
        {
            CourseGraph course = new CourseGraph { Seed = seed, Length = FeelLength };
            for (int d = 0; d <= FeelLength; d++) course.Samples.Add(course.Evaluate(d));
            course.BuildFeelModules();
            course.AddObstacle(112f, 0f, ObstacleKind.Crates);
            course.AddObstacle(194f, 2.5f, ObstacleKind.MarketTrolley);
            course.AddObstacle(352f, -2.4f, ObstacleKind.Barrels);
            course.AddObstacle(433f, .7f, ObstacleKind.Crates);
            return course;
        }

        public CourseSample Sample(float distance)
        {
            float d = Mathf.Clamp(distance, 0f, Length);
            int lo = Mathf.Min(Mathf.FloorToInt(d), Samples.Count - 1);
            int hi = Mathf.Min(lo + 1, Samples.Count - 1);
            if (lo == hi) return Samples[lo];
            float t = d - lo;
            CourseSample a = Samples[lo];
            CourseSample b = Samples[hi];
            return new CourseSample
            {
                Distance = d,
                Position = Vector3.Lerp(a.Position, b.Position, t),
                Forward = Vector3.Slerp(a.Forward, b.Forward, t).normalized,
                Width = Mathf.Lerp(a.Width, b.Width, t),
                Grip = Mathf.Lerp(a.Grip, b.Grip, t),
                // Each rendered strip uses its entry sample for its surface and action.
                Surface = a.Surface,
                Event = a.Event,
                LaunchCue = Mathf.Lerp(a.LaunchCue, b.LaunchCue, t)
            };
        }

        public CourseSample Sample(float distance, int routeId)
        {
            if (routeId > 0 && routeId <= Branches.Count)
            {
                CourseBranch branch = Branches[routeId - 1];
                if (distance >= branch.StartDistance && distance <= branch.EndDistance)
                    return SamplePath(branch.Samples, distance);
            }
            return Sample(distance);
        }

        public Vector3 GuidancePosition(float distance, bool risky)
        {
            if (risky) for (int i = 0; i < Branches.Count; i++)
                if (distance >= Branches[i].StartDistance && distance <= Branches[i].EndDistance)
                    return Sample(distance, i + 1).Position;
            return Sample(distance).Position;
        }

        internal static CourseSample SamplePath(List<CourseSample> list, float distance)
        {
            float d = Mathf.Clamp(distance, list[0].Distance, list[list.Count - 1].Distance);
            int lo = Mathf.Clamp(Mathf.FloorToInt(d - list[0].Distance), 0, list.Count - 1);
            if (lo == list.Count - 1) return list[lo];
            CourseSample a = list[lo], b = list[lo + 1];
            float t = d - a.Distance;
            a.Distance = d; a.Position = Vector3.Lerp(a.Position, b.Position, t);
            a.Forward = Vector3.Lerp(a.Forward, b.Forward, t).normalized;
            a.Width = Mathf.Lerp(a.Width, b.Width, t);
            a.LaunchCue = Mathf.Lerp(a.LaunchCue, b.LaunchCue, t);
            return a;
        }

        public CourseSample Closest(Vector3 point)
        {
            int center = Mathf.Clamp(Mathf.RoundToInt(point.z), 0, Samples.Count - 1);
            int from = Mathf.Max(0, center - 20);
            int to = Mathf.Min(Samples.Count - 2, center + 20);
            float bestDistance = center;
            float bestSquared = float.MaxValue;
            Vector2 target = new Vector2(point.x, point.z);
            for (int i = from; i <= to; i++)
            {
                Vector2 a = new Vector2(Samples[i].Position.x, Samples[i].Position.z);
                Vector2 b = new Vector2(Samples[i + 1].Position.x, Samples[i + 1].Position.z);
                Vector2 segment = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(target - a, segment) / segment.sqrMagnitude);
                float squared = (target - (a + segment * t)).sqrMagnitude;
                if (squared < bestSquared) { bestSquared = squared; bestDistance = i + t; }
            }
            CourseSample nearest = Sample(bestDistance);
            foreach (CourseBranch branch in Branches)
            {
                if (point.z < branch.StartDistance - 12 || point.z > branch.EndDistance + 12) continue;
                int branchCenter = Mathf.Clamp(Mathf.FloorToInt(point.z - branch.StartDistance), 0, branch.Samples.Count - 2);
                for (int i = Mathf.Max(0, branchCenter - 20); i <= Mathf.Min(branch.Samples.Count - 2, branchCenter + 20); i++)
                {
                    Vector2 a = new Vector2(branch.Samples[i].Position.x, branch.Samples[i].Position.z);
                    Vector2 b = new Vector2(branch.Samples[i + 1].Position.x, branch.Samples[i + 1].Position.z);
                    Vector2 segment = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(target - a, segment) / segment.sqrMagnitude);
                    float squared = (target - a - segment * t).sqrMagnitude;
                    if (squared < bestSquared) { bestSquared = squared; nearest = SamplePath(branch.Samples, branch.StartDistance + i + t); }
                }
            }
            return nearest;
        }

        public static Vector3 RoadEdge(CourseSample sample, int side)
        {
            return sample.Position + Vector3.Cross(Vector3.up, sample.Forward).normalized
                * (sample.Width * .5f * side);
        }

        // Matches the two rendered triangles, including height changes across a bend.
        public void RegisterOutsideGround(Mesh mesh)
        {
            Vector3[] vertices=mesh.vertices;int[] indices=mesh.triangles;
            for(int i=0;i<indices.Length;i+=3)
            {
                Vector3 a=vertices[indices[i]],b=vertices[indices[i+1]],c=vertices[indices[i+2]];
                var triangle=new ContactTriangle(a,b,c);
                int lo=Mathf.FloorToInt(Mathf.Min(a.z,Mathf.Min(b.z,c.z))/16f);
                int hi=Mathf.FloorToInt(Mathf.Max(a.z,Mathf.Max(b.z,c.z))/16f);
                for(int cell=lo;cell<=hi;cell++)
                {
                    if(!outsideGround.TryGetValue(cell,out List<ContactTriangle> list))
                        outsideGround[cell]=list=new List<ContactTriangle>();
                    list.Add(triangle);
                }
            }
        }

        public float OutsideGroundHeight(Vector3 point)
        {
            float height=DownhillBackdrop.GroundHeight(this,point.x,point.z);
            if(outsideGround.TryGetValue(Mathf.FloorToInt(point.z/16f),out List<ContactTriangle> triangles))
                foreach(var triangle in triangles)
                    if(triangle.Height(point,out float candidate,out Vector3 normal))height=Mathf.Max(height,candidate);
            return height;
        }

        public bool TrySurfaceHeight(Vector3 point, out float height, out Vector3 normal)
        {
            bool found = TryBranchHeight(Samples, point, out height, out normal);
            foreach (CourseBranch branch in Branches)
            {
                if (point.z < branch.StartDistance - 12 || point.z > branch.EndDistance + 12) continue;
                if (TryBranchHeight(branch.Samples, point, out float branchHeight, out Vector3 branchNormal)
                    && (!found || branchHeight > height))
                { found = true; height = branchHeight; normal = branchNormal; }
            }
            if (found) return true;
            CourseSample nearest = Closest(point);
            height = nearest.Position.y;
            normal = Vector3.ProjectOnPlane(Vector3.up, nearest.Forward).normalized;
            return false;
        }

        private bool TryBranchHeight(List<CourseSample> list, Vector3 point, out float height, out Vector3 normal)
        {
            if(!contactPaths.TryGetValue(list,out ContactStrip[] strips)||strips.Length!=list.Count-1)
            {
                strips=new ContactStrip[list.Count-1];
                for(int i=0;i<strips.Length;i++)
                {
                    Vector3 a=RoadEdge(list[i],-1),b=RoadEdge(list[i],1);
                    Vector3 c=RoadEdge(list[i+1],-1),d=RoadEdge(list[i+1],1);
                    strips[i]=new ContactStrip{first=new ContactTriangle(a,c,b),second=new ContactTriangle(b,c,d)};
                }
                contactPaths[list]=strips;
            }
            int center = Mathf.Clamp(Mathf.FloorToInt(point.z - list[0].Distance), 0, list.Count - 2);
            for (int i = Mathf.Max(0, center - 12); i <= Mathf.Min(list.Count - 2, center + 12); i++)
            {
                if (strips[i].first.Height(point,out height,out normal)
                    ||strips[i].second.Height(point,out height,out normal)) return true;
            }
            height = 0; normal = Vector3.up; return false;
        }

        // The generated graph is immutable during a run. Cache the exact rendered
        // triangles rather than rebuilding their edges for every wheel every step.
        struct ContactStrip { public ContactTriangle first,second; }
        struct ContactTriangle
        {
            Vector3 origin,u,v,normal;
            float determinant;
            public ContactTriangle(Vector3 a,Vector3 b,Vector3 c)
            {
                origin=a;u=b-a;v=c-a;
                determinant=u.x*v.z-u.z*v.x;
                normal=Vector3.Cross(u,v).normalized;
                if(normal.y<0)normal=-normal;
            }
            public bool Height(Vector3 point,out float height,out Vector3 surfaceNormal)
            {
                height=origin.y;surfaceNormal=normal;
                if(Mathf.Abs(determinant)<.000001f)return false;
                float x=point.x-origin.x,z=point.z-origin.z;
                float s=(x*v.z-z*v.x)/determinant;
                float t=(u.x*z-u.z*x)/determinant;
                if(s<-.0001f||t<-.0001f||s+t>1.0001f)return false;
                height=origin.y+s*u.y+t*v.y;
                return true;
            }
        }

        private static bool TriangleHeight(Vector3 point, Vector3 a, Vector3 b, Vector3 c,
            out float height, out Vector3 normal)
        {
            Vector2 u = new Vector2(b.x - a.x, b.z - a.z);
            Vector2 v = new Vector2(c.x - a.x, c.z - a.z);
            Vector2 p = new Vector2(point.x - a.x, point.z - a.z);
            float determinant = u.x * v.y - u.y * v.x;
            height = a.y;
            normal = Vector3.up;
            if (Mathf.Abs(determinant) < .000001f) return false;
            float s = (p.x * v.y - p.y * v.x) / determinant;
            float t = (u.x * p.y - u.y * p.x) / determinant;
            if (s < -.0001f || t < -.0001f || s + t > 1.0001f) return false;
            height = a.y + s * (b.y - a.y) + t * (c.y - a.y);
            normal = Vector3.Cross(b - a, c - a).normalized;
            if (normal.y < 0f) normal = -normal;
            return true;
        }

        public bool Validate(out string diagnostics)
        {
            List<string> errors = new List<string>();
            if (Samples.Count != Mathf.RoundToInt(Length) + 1)
                errors.Add("Sample length mismatch");
            if (Samples.Count < 2)
            {
                diagnostics = "Empty course";
                return false;
            }
            for (int i = 1; i < Samples.Count; i++)
            {
                CourseSample a = Samples[i - 1];
                CourseSample b = Samples[i];
                if (b.Position.y >= a.Position.y) errors.Add("Uphill segment at " + i);
                if (Vector3.Distance(a.Position, b.Position) > 1.7f) errors.Add("Route seam at " + i);
                if (b.Width < 9f) errors.Add("Narrow route at " + i);
                if (!Finite(a.Position) || !Finite(b.Position) || !Finite(b.Forward))
                    errors.Add("Nonfinite geometry at " + i);
                if (b.Distance <= a.Distance || b.Position.z <= a.Position.z)
                    errors.Add("Invalid sample ordering at " + i);
                float horizontal = new Vector2(b.Position.x - a.Position.x, b.Position.z - a.Position.z).magnitude;
                float grade = (a.Position.y - b.Position.y) / Mathf.Max(.001f, horizontal);
                if (grade < .009f || grade > .62f) errors.Add("Slope envelope at " + i);
                Vector3 af = a.Forward; af.y = 0f;
                Vector3 bf = b.Forward; bf.y = 0f;
                float curvature = Vector3.Angle(af, bf) * Mathf.Deg2Rad / Mathf.Max(.001f, horizontal);
                if (curvature * 81f > 9.5f * Mathf.Min(a.Grip, b.Grip))
                    errors.Add("Minimum-speed steering demand at " + i);
                if (Generated && curvature * 484f > 9.5f * Mathf.Min(a.Grip, b.Grip) * .95f)
                    errors.Add("Safe route 22mps steering demand at " + i);
                if (curvature * b.Width * .5f > .35f)
                    errors.Add("Ribbon folding at " + i);
                for (int side = -1; side <= 1; side += 2)
                    if (RoadEdge(b, side).y >= RoadEdge(a, side).y)
                        errors.Add("Uphill road edge at " + i);
            }
            if (!Generated && Sample(231f).LaunchCue <= 0.9f) errors.Add("Missing ramp launch cue");
            if (Modules.Count == 0) errors.Add("No route modules");
            for (int i = 0; i < Modules.Count; i++)
            {
                RouteModule module = Modules[i];
                if (module.EndDistance <= module.StartDistance)
                    errors.Add("Empty module " + module.Id);
                if (module.Entry.Width < 9f || module.Exit.Width < 9f)
                    errors.Add("Narrow socket " + module.Id);
                if (module.Entry.Clearance < 3f || module.Exit.Clearance < 3f)
                    errors.Add("Low clearance " + module.Id);
                if (module.Entry.MinimumSpeed > module.Entry.MaximumSpeed ||
                    module.Exit.MinimumSpeed > module.Exit.MaximumSpeed)
                    errors.Add("Invalid speed envelope " + module.Id);
                if (i == 0 && Mathf.Abs(module.StartDistance) > 0.001f)
                    errors.Add("Route does not start at zero");
                if (i > 0)
                {
                    RouteModule previous = Modules[i - 1];
                    if (Mathf.Abs(previous.EndDistance - module.StartDistance) > 0.001f ||
                        Vector3.Distance(previous.Exit.Position, module.Entry.Position) > 0.001f ||
                        Vector3.Angle(previous.Exit.Forward, module.Entry.Forward) > 0.1f ||
                        Mathf.Abs(previous.Exit.Width - module.Entry.Width) > .01f ||
                        Mathf.Min(previous.Exit.MaximumSpeed, module.Entry.MaximumSpeed) <
                            Mathf.Max(previous.Exit.MinimumSpeed, module.Entry.MinimumSpeed))
                        errors.Add("Module seam at " + module.Id);
                }
            }
            if (Modules.Count > 0 && Mathf.Abs(Modules[Modules.Count - 1].EndDistance - Length) > 0.001f)
                errors.Add("Route does not reach delivery");
            foreach (CourseBranch branch in Branches)
            {
                if (branch.PreviewDistance < 66f) errors.Add("Short fork preview " + branch.Id);
                if (branch.SafeLength < branch.RiskyLength + 15f) errors.Add("No physical shortcut payoff " + branch.Id);
                if (Vector3.Distance(branch.Samples[0].Position, Sample(branch.StartDistance).Position) > .01f
                    || Vector3.Distance(branch.Samples[branch.Samples.Count - 1].Position, Sample(branch.EndDistance).Position) > .01f)
                    errors.Add("Disconnected fork " + branch.Id);
                for (int i = 1; i < branch.Samples.Count; i++)
                {
                    CourseSample a = branch.Samples[i - 1], b = branch.Samples[i];
                    float grade = (a.Position.y - b.Position.y) / Vector3.Distance(new Vector3(a.Position.x, 0, a.Position.z), new Vector3(b.Position.x, 0, b.Position.z));
                    if (grade < .009f || grade > .62f || b.Width < 9f) errors.Add("Shortcut slope or width " + branch.Id);
                }
            }
            foreach (RouteModule module in Modules)
                if (module.LaunchDistance > 0 && (module.PreviewDistance < 66 || module.LandingEnd - module.LaunchDistance < 40))
                    errors.Add("Short landing corridor " + module.Id);
            foreach (CourseObstacle item in Obstacles)
            {
                if (item.PreviewDistance < 72) errors.Add("Short obstacle preview " + item.Id);
                if (OpenCorridor(item.Distance, item.RouteId) < 3.5f) errors.Add("Blocked obstacle corridor " + item.Id);
            }
            if (Generated && (Branches.Count != 5 || EstimatedSeconds < 115 || EstimatedSeconds > 155)) errors.Add("Mission envelope");
            diagnostics = errors.Count == 0 ? "OK" : string.Join("; ", errors.ToArray());
            return errors.Count == 0;
        }

        private static bool Finite(Vector3 vector)
        {
            return !float.IsNaN(vector.x) && !float.IsNaN(vector.y) && !float.IsNaN(vector.z)
                && !float.IsInfinity(vector.x) && !float.IsInfinity(vector.y) && !float.IsInfinity(vector.z);
        }

        internal static float PathLength(List<CourseSample> samples)
        {
            float length = 0;
            for (int i = 1; i < samples.Count; i++) length += Vector3.Distance(samples[i - 1].Position, samples[i].Position);
            return length;
        }

        public CourseObstacle FluidAt(Vector3 point,float radius)
        {
            foreach(var item in Obstacles)
            {
                if(item.Kind!=ObstacleKind.Fluid||Mathf.Abs(point.z-item.Distance)>item.Size.z+2)continue;
                Vector3 local=Quaternion.Inverse(item.Rotation)*(point-item.Position);
                if(Mathf.Abs(local.x)<=item.Size.x*.5f+radius&&Mathf.Abs(local.z)<=item.Size.z*.5f+radius)
                    return item;
            }
            return null;
        }

        public bool TryObstacleHit(Vector3 from, Vector3 to, float radius, float elapsed,
            out CourseObstacle obstacle, out Vector3 normal, out Vector3 correctedPosition)
        {
            obstacle = null; normal = Vector3.zero; correctedPosition = to;
            float earliest = 2;
            foreach (CourseObstacle item in Obstacles)
            {
                if(item.Kind==ObstacleKind.Fluid)continue;
                if (Mathf.Abs(to.z - item.Distance) > item.Size.z + radius + 4) continue;
                Vector3 center = item.PositionAt(elapsed) + item.Rotation * Vector3.up * item.Size.y * .5f;
                Quaternion inv = Quaternion.Inverse(item.Rotation);
                Vector3 a = inv * (from - center), b = inv * (to - center), delta = b - a;
                Vector3 half = item.Size * .5f;
                Vector3 expanded = half + Vector3.one * radius;
                if (Mathf.Abs(a.x) <= expanded.x && Mathf.Abs(a.y) <= expanded.y && Mathf.Abs(a.z) <= expanded.z)
                {
                    // An initial overlap is already contact, even when the next
                    // position leaves it. Solid obstacles always end the run.
                    float px = expanded.x - Mathf.Abs(a.x), pz = expanded.z - Mathf.Abs(a.z);
                    Vector3 outward = px < pz ? Vector3.right * (a.x < 0 ? -1 : 1)
                        : Vector3.forward * (a.z < 0 ? -1 : 1);
                    float penetration = Mathf.Min(px, pz);
                    Vector3 escaped = b + outward * Mathf.Max(0f, penetration - Vector3.Dot(delta, outward) + .025f);
                    earliest = 0f;
                    obstacle = item;
                    normal = item.Rotation * outward;
                    correctedPosition = center + item.Rotation * escaped;
                    continue;
                }
                float enter = 0, exit = 1; Vector3 hitNormal = Vector3.zero;
                if (!Slab(a.x, delta.x, half.x + radius, Vector3.right, ref enter, ref exit, ref hitNormal)
                    || !Slab(a.y, delta.y, half.y + radius, Vector3.up, ref enter, ref exit, ref hitNormal)
                    || !Slab(a.z, delta.z, half.z + radius, Vector3.forward, ref enter, ref exit, ref hitNormal)) continue;
                if (enter > earliest) continue;
                earliest = enter; obstacle = item;
                if (hitNormal.sqrMagnitude < .1f) hitNormal = delta.sqrMagnitude > .001f ? -delta.normalized : Vector3.back;
                normal = item.Rotation * hitNormal;
                correctedPosition = Vector3.Lerp(from, to, Mathf.Max(0, enter - .01f));
            }
            return obstacle != null;
        }

        private static bool Slab(float start, float delta, float half, Vector3 axis,
            ref float enter, ref float exit, ref Vector3 normal)
        {
            if (Mathf.Abs(delta) < .00001f) return Mathf.Abs(start) <= half;
            float lo = (-half - start) / delta, hi = (half - start) / delta;
            Vector3 n = -axis;
            if (lo > hi) { float temp = lo; lo = hi; hi = temp; n = axis; }
            if (lo >= enter) { enter = lo; normal = n; }
            exit = Mathf.Min(exit, hi);
            return enter <= exit && exit >= 0 && enter <= 1;
        }

        internal void AddObstacle(float station, float lateral, ObstacleKind kind, int routeId = 0)
        {
            CourseSample sample = Sample(station, routeId);
            Vector3 right = Vector3.Cross(Vector3.up, sample.Forward).normalized;
            Obstacles.Add(new CourseObstacle { Id = "obstacle-" + Obstacles.Count, Kind = kind, Distance = station, RouteId = routeId,
                Position = sample.Position + right * lateral, Rotation = Quaternion.LookRotation(sample.Forward, Vector3.up),
                Size = kind == ObstacleKind.ParkedCar ? new Vector3(2.35f, 1.7f, 4.4f) :
                    kind == ObstacleKind.CrossingCar ? new Vector3(2.1f, 1.6f, 3.7f) :
                    kind == ObstacleKind.MarketTrolley ? new Vector3(1.4f, 1.3f, 2.2f) :
                    kind == ObstacleKind.Crates ? new Vector3(1.9f, 1.6f, 1.5f) :
                    kind == ObstacleKind.Fluid ? new Vector3(3.1f,.08f,5.5f) :
                    kind == ObstacleKind.Spikes ? new Vector3(2.6f,.72f,2.4f) : new Vector3(1.5f, 1.2f, 1.5f),
                Severity=kind==ObstacleKind.Fluid?0:kind==ObstacleKind.Spikes?3:1,
                MotionAxis = right, MotionAmplitude = kind == ObstacleKind.MarketTrolley || kind == ObstacleKind.CrossingCar ? .65f : 0f,
                MotionPeriod = kind == ObstacleKind.CrossingCar ? 7.5f : 6f });
        }

        private float OpenCorridor(float station, int routeId)
        {
            CourseSample s = Sample(station, routeId);
            Vector3 right = Vector3.Cross(Vector3.up, s.Forward).normalized;
            List<Vector2> intervals = new List<Vector2>();
            foreach (CourseObstacle item in Obstacles)
            {
                if (item.RouteId != routeId || Mathf.Abs(item.Distance - station) > item.Size.z * .5f + 1) continue;
                float offset = Vector3.Dot(item.Position - s.Position, right);
                float half = item.Size.x * .5f + item.MotionAmplitude;
                intervals.Add(new Vector2(offset - half, offset + half));
            }
            intervals.Sort((a,b) => a.x.CompareTo(b.x));
            float cursor = -s.Width * .5f, widest = 0;
            foreach (Vector2 interval in intervals)
            { widest = Mathf.Max(widest, interval.x - cursor); cursor = Mathf.Max(cursor, interval.y); }
            return Mathf.Max(widest, s.Width * .5f - cursor);
        }

        private void BuildFeelModules()
        {
            Modules.Add(new RouteModule("launch", RouteBeat.Launch, this, 0f, 55f, 9f, 22f, 72f, 0f));
            Modules.Add(new RouteModule("sweep-and-bump", RouteBeat.Bump, this, 55f, 105f, 12f, 22f, 48f, 0.15f));
            Modules.Add(new RouteModule("slick-sweep", RouteBeat.Slide, this, 105f, 184f, 13f, 23f, 52f, 0.3f));
            Modules.Add(new RouteModule("settle", RouteBeat.Recovery, this, 184f, 209f, 10f, 22f, 30f, 0f));
            Modules.Add(new RouteModule("table-drop", RouteBeat.Jump, this, 209f, 251f, 9f, 22f, 72f, 0.35f) { LaunchDistance = 231f, LandingEnd = 291f });
            Modules.Add(new RouteModule("canal-belt", RouteBeat.MovingSurface, this, 251f, 344f, 11f, 24f, 45f, 0.2f));
            Modules.Add(new RouteModule("wall-and-recovery", RouteBeat.Scrape, this, 344f, 427f, 10f, 23f, 42f, 0.2f));
            Modules.Add(new RouteModule("delivery", RouteBeat.Delivery, this, 427f, FeelLength, 8f, 22f, 45f, 0f));
        }

        private CourseSample Evaluate(float d)
        {
            float x = CenterX(d);
            float y = Height(d);
            Vector3 before = new Vector3(CenterX(Mathf.Max(0f, d - 0.5f)),
                Height(Mathf.Max(0f, d - 0.5f)), Mathf.Max(0f, d - 0.5f));
            Vector3 after = new Vector3(CenterX(Mathf.Min(Length, d + 0.5f)),
                Height(Mathf.Min(Length, d + 0.5f)), Mathf.Min(Length, d + 0.5f));
            CourseSurface surface = CourseSurface.Stone;
            float grip = 1f;
            if (d >= 143f && d <= 183f) { surface = CourseSurface.Slick; grip = 0.62f; }
            else if (d >= 209f && d <= 250f) { surface = CourseSurface.WoodRamp; grip = 1.08f; }
            else if (d >= 291f && d <= 338f) { surface = CourseSurface.Conveyor; grip = 0.85f; }
            CourseEvent eventKind = CourseEvent.None;
            if (d >= 75f && d <= 86f) eventKind = CourseEvent.Bump;
            else if (d >= 216f && d <= 235f) eventKind = CourseEvent.Ramp;
            else if (d >= 291f && d <= 338f) eventKind = CourseEvent.Conveyor;
            else if (d >= 389f && d <= 404f) eventKind = CourseEvent.Scrape;
            else if (d >= 474f) eventKind = CourseEvent.Finish;
            return new CourseSample
            {
                Distance = d,
                Position = new Vector3(x, y, d),
                Forward = (after - before).normalized,
                Width = 11.5f,
                Grip = grip,
                Surface = surface,
                Event = eventKind,
                LaunchCue = d >= 228f && d <= 233f ?
                    1f - Mathf.Abs(d - 231f) / 3f : 0f
            };
        }

        private static float CenterX(float d)
        {
            return 15f * Smooth((d - 38f) / 92f)
                - 28f * Smooth((d - 143f) / 112f)
                + 20f * Smooth((d - 303f) / 118f);
        }

        private static float Height(float d)
        {
            float baseline;
            if (d <= 218f) baseline = -0.18f * d;
            else if (d <= 231f) baseline = -0.18f * 218f - 0.025f * (d - 218f);
            else if (d <= 245f) baseline = -0.18f * 218f - 0.025f * 13f - 0.50f * (d - 231f);
            else baseline = -0.18f * 218f - 0.025f * 13f - 0.50f * 14f - 0.18f * (d - 245f);
            float bump = d >= 72f && d <= 90f ?
                0.62f * Mathf.Sin(Mathf.PI * (d - 72f) / 18f) : 0f;
            // A shallow descending takeoff meets a steeper drop, without an uphill ramp.
            return baseline + bump;
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
