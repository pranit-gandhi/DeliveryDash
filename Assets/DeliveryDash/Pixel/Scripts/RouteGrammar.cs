using System;
using System.Collections.Generic;

namespace DeliveryDash.Pixel
{
    public enum RouteBeatKind
    {
        Start,
        Bend,
        Bump,
        Ramp,
        Conveyor,
        Slick,
        Scrape,
        Fork,
        Recovery,
        Finish
    }

    public enum RouteSurface
    {
        Stone,
        Wood,
        SlickStone,
        Conveyor
    }

    public sealed class RouteBeat
    {
        public int Id;
        public RouteBeatKind Kind;
        public float Start;
        public float End;
        public float Offset;
        public float Width;
        public float Severity;

        public float Center { get { return (Start + End) * 0.5f; } }
    }

    // The two corridors split, remain distinct, then rejoin on the same falling road.
    public sealed class RouteBranch
    {
        public int Id;
        public float SplitStart;
        public float ChoiceAt;
        public float SplitEnd;
        public float MergeStart;
        public float MergeEnd;
        public int RiskySide;
        public float SideOffset;
        public float CorridorWidth;
        public float SafeCorridorWidth;
        public float SafeExpectedSeconds;
        public float RiskyExpectedSeconds;
        public float RiskyConditionCost;

        public float RiskyCenterOffset(float distance)
        {
            return RiskySide * SideOffset * SplitAmount(distance);
        }

        public float SafeCenterOffset(float distance)
        {
            return 0f;
        }

        public float SplitAmount(float distance)
        {
            if (distance <= SplitStart || distance >= MergeEnd) return 0f;
            if (distance < SplitEnd) return RouteMath.Smooth01((distance - SplitStart) / (SplitEnd - SplitStart));
            if (distance <= MergeStart) return 1f;
            return 1f - RouteMath.Smooth01((distance - MergeStart) / (MergeEnd - MergeStart));
        }

        public bool Contains(float distance) { return distance >= SplitStart && distance <= MergeEnd; }
        public bool IsRisky(float distance, float lateralOffset)
        {
            return distance >= SplitEnd && distance <= MergeStart &&
                   Math.Abs(lateralOffset - RiskyCenterOffset(distance)) <= CorridorWidth * 0.5f;
        }
        public bool IsSafe(float distance, float lateralOffset)
        {
            return distance >= SplitEnd && distance <= MergeStart &&
                   Math.Abs(lateralOffset) <= SafeCorridorWidth * 0.5f;
        }
    }

    public sealed class RouteProp
    {
        public int Id;
        public string Type;
        public float Distance;
        public float LateralOffset;
        public float Width;
        public bool AffectsMotion;
    }

    public sealed class RoutePlan
    {
        public int Seed;
        public float LengthMeters;
        public float DeadlineSeconds;
        public float ExpectedSeconds;
        public float DropPerMeter;
        public bool UsedFallback;
        public readonly List<RouteBeat> Beats = new List<RouteBeat>();
        public readonly List<RouteBranch> Branches = new List<RouteBranch>();
        public readonly List<RouteProp> Props = new List<RouteProp>();
        internal readonly List<RouteCurve> Curves = new List<RouteCurve>();

        public float CenterX(float distance)
        {
            float x = 0f;
            for (int i = 0; i < Curves.Count; i++)
                x += Curves[i].OffsetAt(distance);
            return x;
        }

        // Elevation is a strictly falling continuous function, including branch paths.
        public float Elevation(float distance) { return -DropPerMeter * RouteMath.Clamp(distance, 0f, LengthMeters); }

        public float Width(float distance)
        {
            float width = 12.5f;
            for (int i = 0; i < Branches.Count; i++)
                width = Math.Max(width, 2f * (Branches[i].SideOffset * Branches[i].SplitAmount(distance) + Branches[i].CorridorWidth * 0.5f) + 1f);
            return width;
        }

        public RouteBeat BeatAt(float distance)
        {
            RouteBeat nearest = null;
            for (int i = 0; i < Beats.Count; i++)
            {
                RouteBeat beat = Beats[i];
                if (distance >= beat.Start && distance <= beat.End)
                {
                    if (nearest == null || beat.Severity >= nearest.Severity) nearest = beat;
                }
            }
            return nearest;
        }

        public RouteSurface SurfaceAt(float distance)
        {
            RouteBeat beat = BeatAt(distance);
            if (beat == null) return RouteSurface.Stone;
            if (beat.Kind == RouteBeatKind.Conveyor) return RouteSurface.Conveyor;
            if (beat.Kind == RouteBeatKind.Slick) return RouteSurface.SlickStone;
            if (beat.Kind == RouteBeatKind.Ramp) return RouteSurface.Wood;
            return RouteSurface.Stone;
        }

        public RouteBranch NextDecision(float distance)
        {
            for (int i = 0; i < Branches.Count; i++)
                if (Branches[i].ChoiceAt >= distance) return Branches[i];
            return null;
        }

        public RouteBranch BranchAt(float distance)
        {
            for (int i = 0; i < Branches.Count; i++)
                if (Branches[i].Contains(distance)) return Branches[i];
            return null;
        }

        public bool Validate(out string diagnostics)
        {
            List<string> errors = new List<string>();
            if (LengthMeters < 1400f || LengthMeters > 1800f) errors.Add("Route length outside target band");
            if (ExpectedSeconds < 120f || ExpectedSeconds > 180f) errors.Add("Estimated run time outside target band");
            if (DeadlineSeconds < ExpectedSeconds + 30f) errors.Add("Delivery deadline has insufficient recovery margin");
            if (DropPerMeter < 0.035f) errors.Add("Road descent is too shallow");
            if (Branches.Count < 1 || Branches.Count > 2) errors.Add("Expected one or two choices");
            int rampCount = 0, bumpCount = 0, recoveryCount = 0;
            for (int i = 0; i < Beats.Count; i++)
            {
                RouteBeat beat = Beats[i];
                if (beat.Start < 0f || beat.End > LengthMeters || beat.Start >= beat.End) errors.Add("Invalid beat extent " + beat.Id);
                if (beat.Kind == RouteBeatKind.Ramp) rampCount++;
                if (beat.Kind == RouteBeatKind.Bump) bumpCount++;
                if (beat.Kind == RouteBeatKind.Recovery) recoveryCount++;
            }
            if (rampCount < 1 || bumpCount < 1 || recoveryCount < 2) errors.Add("Missing core route beats");
            for (int i = 0; i < Branches.Count; i++)
            {
                RouteBranch branch = Branches[i];
                if (!(branch.SplitStart < branch.ChoiceAt && branch.ChoiceAt < branch.SplitEnd &&
                      branch.SplitEnd < branch.MergeStart && branch.MergeStart < branch.MergeEnd &&
                      branch.MergeEnd < LengthMeters - 100f)) errors.Add("Invalid fork geometry " + branch.Id);
                if (branch.RiskyExpectedSeconds >= branch.SafeExpectedSeconds) errors.Add("Risky corridor lacks a time payoff " + branch.Id);
                if (branch.CorridorWidth < 3.2f || branch.SafeCorridorWidth < 9f)
                    errors.Add("Fork corridor too narrow " + branch.Id);
                if (i > 0 && branch.SplitStart - Branches[i - 1].MergeEnd < 80f) errors.Add("Insufficient recovery between forks");
            }
            for (float d = 1f; d <= LengthMeters; d += 2f)
            {
                if (Elevation(d) >= Elevation(d - 1f)) { errors.Add("Road fails downhill check at " + d); break; }
                if (Width(d) < 10f) { errors.Add("Road width too narrow at " + d); break; }
                // A broad curve at this scale leaves time to steer before the next beat.
                float centerDelta = Math.Abs(CenterX(d + 2f) - CenterX(d - 2f));
                if (centerDelta > 1.15f) { errors.Add("Curve exceeds steering envelope at " + d); break; }
            }
            diagnostics = errors.Count == 0 ? "OK" : string.Join("; ", errors.ToArray());
            return errors.Count == 0;
        }
    }

    internal struct RouteCurve
    {
        public float Start, End, Delta;
        public float OffsetAt(float distance)
        {
            return Delta * RouteMath.Smooth01((distance - Start) / (End - Start));
        }
    }

    internal static class RouteMath
    {
        public static float Clamp(float v, float min, float max) { return Math.Max(min, Math.Min(max, v)); }
        public static float Smooth01(float v) { v = Clamp(v, 0f, 1f); return v * v * (3f - 2f * v); }
    }

    // This generator uses only a local integer RNG. Cosmetic changes cannot alter gameplay seeds.
    public static class RouteGrammar
    {
        public static RoutePlan Generate(int seed)
        {
            RoutePlan candidate = Build(seed);
            string diagnostics;
            if (candidate.Validate(out diagnostics)) return candidate;
            return BuildFallback(seed);
        }

        private static RoutePlan Build(int seed)
        {
            RouteRandom random = new RouteRandom(seed);
            RoutePlan plan = new RoutePlan();
            plan.Seed = seed;
            plan.LengthMeters = 1530f + random.Range(0, 15) * 12f;
            plan.DropPerMeter = 0.052f;
            plan.ExpectedSeconds = plan.LengthMeters / 11.5f + 6f;
            plan.DeadlineSeconds = plan.ExpectedSeconds + 47f;
            int id = 1;

            AddBeat(plan, ref id, RouteBeatKind.Start, 0f, 72f, 0f, 12.5f, 0f);
            AddBeat(plan, ref id, RouteBeatKind.Bump, 91f, 99f, 0f, 7f, 0.28f);
            AddBeat(plan, ref id, RouteBeatKind.Bend, 146f, 245f, 0f, 12.5f, 0.32f);
            RouteBeatKind first = random.NextBool() ? RouteBeatKind.Conveyor : RouteBeatKind.Slick;
            AddBeat(plan, ref id, first, 262f, 307f, random.NextSign() * 2.2f, 4.2f, 0.36f);
            AddBeat(plan, ref id, RouteBeatKind.Recovery, 311f, 409f, 0f, 12.5f, 0f);

            int forkSide = -1;
            AddBranch(plan, ref id, 431f, 460f, 486f, 563f, 602f, forkSide, 8.8f, 3.7f, 20f, 15.5f, 0.06f);
            AddBeat(plan, ref id, RouteBeatKind.Fork, 431f, 602f, 0f, 18f, 0.42f);
            // The shortcut uses a broad bridge or a small ramp. Both have a landing and a safe parallel way.
            AddBeat(plan, ref id, RouteBeatKind.Ramp,
                511f, 541f, forkSide * 8.8f, 3.7f, 0.49f);
            AddBeat(plan, ref id, RouteBeatKind.Recovery, 610f, 705f, 0f, 12.5f, 0f);

            int order = random.Range(0, 3);
            RouteBeatKind middle = order == 0 ? RouteBeatKind.Slick : order == 1 ? RouteBeatKind.Bump : RouteBeatKind.Conveyor;
            AddBeat(plan, ref id, middle, 731f, middle == RouteBeatKind.Bump ? 741f : 777f,
                random.NextSign() * 1.9f, 4f, 0.39f);
            AddBeat(plan, ref id, RouteBeatKind.Bend, 801f, 900f, 0f, 12.5f, 0.43f);
            AddBeat(plan, ref id, RouteBeatKind.Recovery, 908f, 976f, 0f, 12.5f, 0f);

            // A second choice changes both lateral steering and the type of stunt on different seeds.
            forkSide = 1;
            AddBranch(plan, ref id, 995f, 1024f, 1051f, 1148f, 1190f, forkSide, 8.8f, 3.7f, 24f, 18.5f, 0.085f);
            AddBeat(plan, ref id, RouteBeatKind.Fork, 995f, 1190f, 0f, 18f, 0.55f);
            AddBeat(plan, ref id, order == 2 ? RouteBeatKind.Slick : RouteBeatKind.Ramp,
                1086f, 1123f, forkSide * 8.8f, 3.7f, 0.55f);
            AddBeat(plan, ref id, RouteBeatKind.Recovery, 1198f, 1300f, 0f, 12.5f, 0f);
            AddBeat(plan, ref id, RouteBeatKind.Scrape, 1331f, 1357f, random.NextSign() * 5.1f, 1.1f, 0.24f);
            AddBeat(plan, ref id, RouteBeatKind.Finish, plan.LengthMeters - 75f, plan.LengthMeters, 0f, 12.5f, 0f);

            // Smooth town bends. Every bend takes at least 110 m to change direction.
            AddCurve(plan, 126f, 257f, random.NextSign() * random.Range(5f, 10f));
            AddCurve(plan, 390f, 531f, random.NextSign() * random.Range(6f, 10f));
            AddCurve(plan, 692f, 853f, random.NextSign() * random.Range(7f, 12f));
            AddCurve(plan, 868f, 1012f, random.NextSign() * random.Range(5f, 10f));
            AddCurve(plan, 1193f, 1339f, random.NextSign() * random.Range(6f, 11f));

            AddFunctionalProps(plan);
            return plan;
        }

        private static RoutePlan BuildFallback(int requestedSeed)
        {
            // A fixed layout with the requested seed kept for replay/reporting.
            RoutePlan plan = Build(14371);
            plan.Seed = requestedSeed;
            plan.UsedFallback = true;
            return plan;
        }

        private static void AddBeat(RoutePlan plan, ref int id, RouteBeatKind kind, float start, float end,
            float offset, float width, float severity)
        {
            plan.Beats.Add(new RouteBeat { Id = id++, Kind = kind, Start = start, End = end,
                Offset = offset, Width = width, Severity = severity });
        }

        private static void AddBranch(RoutePlan plan, ref int id, float splitStart, float choiceAt,
            float splitEnd, float mergeStart, float mergeEnd, int side, float sideOffset,
            float corridorWidth, float safeSeconds, float riskySeconds, float riskCost)
        {
            plan.Branches.Add(new RouteBranch { Id = id++, SplitStart = splitStart, ChoiceAt = choiceAt,
                SplitEnd = splitEnd, MergeStart = mergeStart, MergeEnd = mergeEnd, RiskySide = side,
                SideOffset = sideOffset, CorridorWidth = corridorWidth,
                SafeCorridorWidth = 12.5f,
                SafeExpectedSeconds = safeSeconds, RiskyExpectedSeconds = riskySeconds,
                RiskyConditionCost = riskCost });
        }

        private static void AddCurve(RoutePlan plan, float start, float end, float delta)
        {
            plan.Curves.Add(new RouteCurve { Start = start, End = end, Delta = delta });
        }

        private static void AddFunctionalProps(RoutePlan plan)
        {
            int id = 1;
            for (int i = 0; i < plan.Beats.Count; i++)
            {
                RouteBeat beat = plan.Beats[i];
                string type = null;
                if (beat.Kind == RouteBeatKind.Bump) type = "SpeedBump";
                if (beat.Kind == RouteBeatKind.Ramp) type = "FoldingRamp";
                if (beat.Kind == RouteBeatKind.Conveyor) type = "MarketConveyor";
                if (beat.Kind == RouteBeatKind.Slick) type = "WaterSpill";
                if (beat.Kind == RouteBeatKind.Scrape) type = "SoftMarketCrate";
                if (type != null)
                    plan.Props.Add(new RouteProp { Id = id++, Type = type, Distance = beat.Center,
                        LateralOffset = beat.Offset, Width = beat.Width, AffectsMotion = true });
            }
        }
    }

    internal struct RouteRandom
    {
        private uint state;
        public RouteRandom(int seed) { state = (uint)seed ^ 0xA511E9B3u; if (state == 0) state = 1; }
        private uint Next()
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }
        public int Range(int min, int maxExclusive) { return min + (int)(Next() % (uint)(maxExclusive - min)); }
        public float Range(float min, float max) { return min + (Next() / (float)uint.MaxValue) * (max - min); }
        public bool NextBool() { return (Next() & 1u) != 0; }
        public int NextSign() { return NextBool() ? 1 : -1; }
    }
}
