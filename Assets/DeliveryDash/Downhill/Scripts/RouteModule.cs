using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeliveryDash.Downhill
{
    public enum RouteBeat
    {
        Launch, Sweep, Bump, Slide, Recovery, Jump, MovingSurface, Scrape, Delivery,
        Fork, Combination, FinalApproach
    }
    public enum HazardPattern { None, LoadingBay, Weave, Traffic, Spill, OilSlick, SpikeLane }

    // A socket is expressed in the same world coordinates as CourseSample and the road mesh.
    [Serializable]
    public struct ModuleSocket
    {
        public Vector3 Position;
        public Vector3 Forward;
        public float Width;
        public float Elevation;
        public CourseSurface Surface;
        public float Clearance;
        public float MinimumSpeed;
        public float MaximumSpeed;
        public bool SpeedRangePlaytested;

        public static ModuleSocket FromSample(CourseSample sample, float clearance,
            float minimumSpeed, float maximumSpeed)
        {
            return new ModuleSocket
            {
                Position = sample.Position,
                Forward = sample.Forward,
                Width = sample.Width,
                Elevation = sample.Position.y,
                Surface = sample.Surface,
                Clearance = clearance,
                MinimumSpeed = minimumSpeed,
                MaximumSpeed = maximumSpeed
            };
        }
    }

    // A route module owns a closed interval of the course sampler. Neighboring modules
    // share one boundary sample; their sockets must therefore join without a road seam.
    [Serializable]
    public sealed class RouteModule
    {
        public string Id;
        public RouteBeat Beat;
        public float StartDistance;
        public float EndDistance;
        public ModuleSocket Entry;
        public ModuleSocket Exit;
        public float PreviewDistance;
        public float Risk;
        public bool Mandatory;
        public float LaunchDistance;
        public float LandingEnd;
        public float Bend;
        public CourseSurface Surface;
        public float Grade;
        public HazardPattern Pattern;

        public RouteModule(string id, RouteBeat beat, CourseGraph course, float start,
            float end, float minSpeed, float maxSpeed, float preview, float risk,
            bool mandatory = true)
        {
            Id = id;
            Beat = beat;
            StartDistance = start;
            EndDistance = end;
            if (course != null)
            {
                Entry = ModuleSocket.FromSample(course.Sample(start), 4.5f, minSpeed, maxSpeed);
                Exit = ModuleSocket.FromSample(course.Sample(end), 4.5f, minSpeed, maxSpeed);
            }
            PreviewDistance = preview;
            Risk = risk;
            Mandatory = mandatory;
        }
    }

    [Serializable]
    public sealed class CourseBranch
    {
        public string Id;
        public float StartDistance;
        public float EndDistance;
        public float SafeLength;
        public float RiskyLength;
        public float PreviewDistance = 72f;
        public readonly System.Collections.Generic.List<CourseSample> Samples =
            new System.Collections.Generic.List<CourseSample>();
        public ModuleSocket Entry;
        public ModuleSocket Exit;
    }

    // Finite action grammar, deterministic placement, bounded repair and fixed fallback.
    internal static class CourseGenerator
    {
        public static CourseGraph Create(int seed)
        {
            List<string> rejects = new List<string>();
            for (int attempt = 0; attempt < 4; attempt++)
            {
                CourseGraph candidate = Candidate(seed, attempt, false);
                candidate.GenerationAttempts = attempt + 1;
                if (candidate.Validate(out string report))
                {
                    candidate.GenerationReport = "Geometry validated; attempts " + (attempt + 1)
                        + (rejects.Count > 0 ? "; rejected " + string.Join(" | ", rejects.ToArray()) : "");
                    return candidate;
                }
                rejects.Add(report);
            }
            CourseGraph fallback = CreateFallback(seed);
            fallback.GenerationAttempts = 4;
            fallback.GenerationReport = "Fixed fallback independently validated; rejected " + string.Join(" | ", rejects.ToArray());
            return fallback;
        }

        public static CourseGraph CreateFallback(int requestedSeed)
        {
            CourseGraph fallback = Candidate(73191, 0, true);
            if (!fallback.Validate(out string report)) throw new InvalidOperationException("Fallback validation failed: " + report);
            fallback.Seed = requestedSeed; fallback.UsedFallback = true;
            fallback.GenerationReport = "Fixed authored fallback geometry validated separately";
            return fallback;
        }

        private static CourseGraph Candidate(int seed, int attempt, bool fallback)
        {
            System.Random rng = new System.Random(unchecked(seed * 397 + attempt * 7919));
            CourseGraph c = new CourseGraph { Seed = seed, Generated = true };
            RouteBeat simple = fallback ? RouteBeat.Bump : new[] { RouteBeat.Bump, RouteBeat.Slide, RouteBeat.MovingSurface }[rng.Next(3)];
            RouteBeat combo = fallback ? RouteBeat.Jump : new[] { RouteBeat.Jump, RouteBeat.Combination, RouteBeat.Slide }[rng.Next(3)];
            RouteBeat earlySweep = fallback ? RouteBeat.Sweep : new[] { RouteBeat.Sweep, RouteBeat.Bump, RouteBeat.Slide }[rng.Next(3)];
            RouteBeat lateSweep = fallback ? RouteBeat.Sweep : new[] { RouteBeat.Sweep, RouteBeat.Bump, RouteBeat.MovingSurface }[rng.Next(3)];
            if (earlySweep == RouteBeat.Slide && simple == RouteBeat.Slide) simple = RouteBeat.MovingSurface;
            RouteBeat extraAction = fallback ? RouteBeat.Bump : new[] { RouteBeat.Bump, RouteBeat.MovingSurface, RouteBeat.Sweep }[rng.Next(3)];
            RouteBeat[] grammar = { RouteBeat.Launch, earlySweep, RouteBeat.Fork, RouteBeat.Recovery,
                simple, combo, RouteBeat.Recovery, RouteBeat.Fork, lateSweep,
                RouteBeat.Recovery, RouteBeat.Fork, RouteBeat.Recovery, extraAction,
                RouteBeat.Fork, RouteBeat.Recovery, RouteBeat.Fork, RouteBeat.Recovery,
                RouteBeat.FinalApproach, RouteBeat.Delivery };
            int[] sizes = { 85, 80, 280, 65, 100, 115, 70, 280, 100, 70, 280, 70, 100, 280, 70, 280, 70, 125, 20 };
            float station = 0;
            for (int i = 0; i < grammar.Length; i++)
            {
                RouteBeat beat = grammar[i];
                int size = sizes[i] + (fallback || beat == RouteBeat.Delivery ? 0 : rng.Next(-10, 11));
                RouteModule m = new RouteModule(beat.ToString().ToLowerInvariant() + "-" + i, beat, null,
                    station, station + size, 9, 22, 72, 0);
                m.Grade = fallback ? .19f : .17f + (float)rng.NextDouble() * .07f;
                m.Bend = (rng.Next(2) == 0 ? -1 : 1) * (beat == RouteBeat.Fork ? Mathf.Min(54f, size * size * .00064f) :
                    i == 1 || i == 8 || i == 12 ? Mathf.Min(size * size * .00050f,
                        beat == RouteBeat.Slide ? 3.4f : 5f + (float)rng.NextDouble() * 2f) : 0);
                m.Surface = beat == RouteBeat.Slide ? CourseSurface.Slick : beat == RouteBeat.MovingSurface ? CourseSurface.Conveyor :
                    beat == RouteBeat.Jump || beat == RouteBeat.Combination ? CourseSurface.WoodRamp : CourseSurface.Stone;
                if (beat == RouteBeat.Jump || beat == RouteBeat.Combination || beat == RouteBeat.FinalApproach)
                { m.LaunchDistance = station + 35; m.LandingEnd = station + size; }
                m.Pattern = beat == RouteBeat.Recovery || beat == RouteBeat.Delivery || beat == RouteBeat.Launch || beat == RouteBeat.FinalApproach
                    ? HazardPattern.None : fallback ? HazardPattern.LoadingBay : (HazardPattern)rng.Next(1,7);
                c.Modules.Add(m); station += size;
            }
            c.Length = station;
            float y = 0;
            int moduleIndex = 0;
            for (int d = 0; d <= station; d++)
            {
                while (moduleIndex < c.Modules.Count - 1 && d >= c.Modules[moduleIndex].EndDistance) moduleIndex++;
                RouteModule m = c.Modules[moduleIndex];
                float t = (d - m.StartDistance) / (m.EndDistance - m.StartDistance);
                if (d > 0)
                {
                    float grade = m.Grade;
                    if (m.LaunchDistance > 0 && d > m.LaunchDistance - 10 && d <= m.LaunchDistance) grade = .025f;
                    else if (m.LaunchDistance > 0 && d > m.LaunchDistance && d <= m.LaunchDistance + 14) grade = .5f;
                    y -= grade;
                }
                float bumpStart = m.Beat == RouteBeat.Combination ? .68f : .25f;
                float bumpEnd = m.Beat == RouteBeat.Combination ? .88f : .45f;
                bool bump = (m.Beat == RouteBeat.Bump || m.Beat == RouteBeat.Combination) && t >= bumpStart && t <= bumpEnd;
                float bumpHeight = bump ? .55f * Mathf.Sin(Mathf.PI * (t - bumpStart) / (bumpEnd - bumpStart)) : 0;
                CourseEvent action = m.Beat == RouteBeat.Delivery ? CourseEvent.Finish : m.Beat == RouteBeat.MovingSurface ? CourseEvent.Conveyor :
                    m.LaunchDistance > 0 && Mathf.Abs(d - m.LaunchDistance) < 12 ? CourseEvent.Ramp : bump ? CourseEvent.Bump : CourseEvent.None;
                c.Samples.Add(new CourseSample { Distance = d, Position = new Vector3(m.Bend * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Clamp01(t)), 2), y + bumpHeight, d),
                    Width = m.Beat == RouteBeat.Fork ? 14f : 12f, Surface = m.Surface, Grip = m.Surface == CourseSurface.Slick ? .62f : 1f,
                    Event = action, LaunchCue = m.LaunchDistance > 0 ? Cue(d, m.LaunchDistance) : 0, Forward = Vector3.forward });
            }
            Tangents(c.Samples);
            foreach (RouteModule m in c.Modules)
            {
                m.Entry = ModuleSocket.FromSample(c.Sample(m.StartDistance), 4.5f, 9, 22);
                m.Exit = ModuleSocket.FromSample(c.Sample(m.EndDistance), 4.5f, 9, 22);
                if (m.Beat == RouteBeat.Fork)
                {
                    // The first shortcut always teaches a natural launch. Later choices
                    // vary the actual risk rather than repeating the same barrel and car.
                    int risk = c.Branches.Count < 3 ? c.Branches.Count
                        : fallback ? c.Branches.Count % 4 : rng.Next(4);
                    AddBranch(c, m, risk, rng.Next(2) == 0 ? -1f : 1f);
                }
                float side = fallback ? -1 : rng.Next(2) == 0 ? -1 : 1;
                float span = m.EndDistance - m.StartDistance;
                if (m.Beat == RouteBeat.Launch)
                {
                    c.AddObstacle(m.StartDistance + 72, -2.4f, ObstacleKind.Crates);
                    c.AddObstacle(m.StartDistance + 86, -2.6f, ObstacleKind.Barrels);
                }
                else if (m.Beat == RouteBeat.Fork)
                    PopulateCluster(c,m,side,.43f,.72f);
                else if (m.Beat == RouteBeat.FinalApproach)
                {
                    c.AddObstacle(m.StartDistance + span * .35f, 2.3f, ObstacleKind.Crates);
                    c.AddObstacle(m.StartDistance + span * .74f, -2.1f, ObstacleKind.MarketTrolley);
                }
                else if (m.Beat == RouteBeat.Bump || m.Beat == RouteBeat.Slide || m.Beat == RouteBeat.MovingSurface
                    || m.Beat == RouteBeat.Jump || m.Beat == RouteBeat.Combination || m.Beat == RouteBeat.Sweep)
                {
                    PopulateCluster(c,m,side,m.LaunchDistance > 0 ? .68f : .38f,.88f);
                }
            }
            return c;
        }

        private static void PopulateCluster(CourseGraph course, RouteModule module, float side, float lo, float hi)
        {
            float span=module.EndDistance-module.StartDistance;
            float first=module.StartDistance+span*lo, last=module.StartDistance+span*hi;
            if(module.Pattern==HazardPattern.Weave)
            {
                course.AddObstacle(first,-1.8f,ObstacleKind.Crates);
                course.AddObstacle(last,1.8f,ObstacleKind.Crates);
            }
            else if(module.Pattern==HazardPattern.Traffic)
            {
                course.AddObstacle(first,.9f,ObstacleKind.CrossingCar);
                course.AddObstacle(last,2.5f,ObstacleKind.MarketTrolley);
            }
            else if(module.Pattern==HazardPattern.OilSlick)
            {
                course.AddObstacle(first,side*2.5f,ObstacleKind.Fluid);
                // Separation leaves time to regain heading before the next demand.
                // Both hazards occupy the same side, retaining a clear escape lane.
                course.AddObstacle(last,side*2.5f,ObstacleKind.Barrels);
            }
            else if(module.Pattern==HazardPattern.SpikeLane)
            {
                course.AddObstacle(first,side*2.5f,ObstacleKind.Spikes);
                course.AddObstacle(last,side*2.5f,ObstacleKind.Fluid);
            }
            else
            {
                course.AddObstacle(first,side*2.5f,ObstacleKind.ParkedCar);
                course.AddObstacle(last,side*2.5f,ObstacleKind.Crates);
                if(module.Pattern==HazardPattern.Spill)
                    course.AddObstacle(Mathf.Lerp(first,last,.5f),-side*2.6f,ObstacleKind.Barrels);
            }
        }

        private static void AddBranch(CourseGraph c, RouteModule m, int risk, float side)
        {
            bool jump = risk == 0;
            CourseBranch branch = new CourseBranch { Id = m.Id + "-shortcut", StartDistance = m.StartDistance,
                EndDistance = m.EndDistance, Entry = m.Entry, Exit = m.Exit };
            float lip = m.StartDistance + 110;
            int routeId = c.Branches.Count + 1;
            for (int d = (int)m.StartDistance; d <= m.EndDistance; d++)
            {
                CourseSample s = c.Sample(d);
                float local = d - m.StartDistance, tail = m.EndDistance - d;
                float width = local < 35 ? Mathf.Lerp(m.Entry.Width, 9.5f, Smooth(local / 35)) :
                    tail < 35 ? Mathf.Lerp(m.Exit.Width, 9.5f, Smooth(tail / 35)) : 9.5f;
                float lift = 0;
                if (jump)
                {
                    float rate = m.Grade - .025f;
                    if (d >= lip - 10 && d <= lip) lift = rate * (d - lip + 10);
                    else if (d > lip && d <= lip + 7) lift = rate * 10 * (1 - (d - lip) / 7);
                }
                s.RouteId = routeId;
                s.Position = new Vector3(m.Entry.Position.x, s.Position.y + lift, d);
                s.Width = width;
                s.Surface = local < 40 || tail < 40 ? CourseSurface.Stone : jump ? CourseSurface.WoodRamp : CourseSurface.Stone;
                s.Grip = 1f;
                s.LaunchCue = jump ? Cue(d, lip) : 0;
                s.Event = jump && Mathf.Abs(d - lip) < 12 ? CourseEvent.Ramp : CourseEvent.None;
                branch.Samples.Add(s);
            }
            Tangents(branch.Samples);
            branch.SafeLength = CourseGraph.PathLength(c.Samples.GetRange((int)m.StartDistance, (int)(m.EndDistance - m.StartDistance) + 1));
            branch.RiskyLength = CourseGraph.PathLength(branch.Samples);
            c.Branches.Add(branch);
            if (risk == 1)
            {
                c.AddObstacle(m.StartDistance + 115f, side*2.2f, ObstacleKind.Fluid, routeId);
                c.AddObstacle(m.StartDistance + 195f, side*2.2f, ObstacleKind.Fluid, routeId);
            }
            else if (risk == 2)
            {
                c.AddObstacle(m.StartDistance + 115f, side*2.4f, ObstacleKind.Spikes, routeId);
                c.AddObstacle(m.StartDistance + 195f, side*2.4f, ObstacleKind.Spikes, routeId);
            }
            else if (risk == 3)
            {
                c.AddObstacle(m.StartDistance + 135f, side*.8f, ObstacleKind.CrossingCar, routeId);
                c.AddObstacle(m.StartDistance + 200f, side*2.4f, ObstacleKind.MarketTrolley, routeId);
            }
            else
                c.AddObstacle(m.StartDistance + 195f, side*2.4f, ObstacleKind.Barrels, routeId);
        }

        internal static void Tangents(List<CourseSample> samples)
        {
            for (int i = 0; i < samples.Count; i++)
            { CourseSample s = samples[i]; s.Forward = (samples[Mathf.Min(i + 1, samples.Count - 1)].Position - samples[Mathf.Max(0, i - 1)].Position).normalized; samples[i] = s; }
        }
        private static float Cue(float d, float lip) => Mathf.Abs(d - lip) <= 3 ? 1 - Mathf.Abs(d - lip) / 3 : 0;
        private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }
        public static string ValidateSeeds(int count)
        {
            int failures = 0, fallbacks = 0;
            HashSet<string> actions = new HashSet<string>();
            float min = 1000, max = 0;
            for (int seed = 0; seed < count; seed++)
            {
                CourseGraph c = Create(seed);
                if (!c.Validate(out string report)) failures++;
                if (c.UsedFallback) fallbacks++;
                actions.Add(c.ActionSequence); min = Mathf.Min(min, c.EstimatedSeconds); max = Mathf.Max(max, c.EstimatedSeconds);
            }
            return "Seeds " + count + "; geometry failures " + failures + "; fallbacks " + fallbacks + "; action sequences " + actions.Count
                + "; estimated seconds " + min.ToString("0.0") + " to " + max.ToString("0.0") + ". Geometry filter only; controller and keyboard testing required.";
        }
    }
}
