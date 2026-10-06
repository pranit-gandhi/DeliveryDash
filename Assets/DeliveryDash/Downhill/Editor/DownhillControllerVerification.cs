using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using DeliveryDash.Downhill;

namespace DeliveryDash.Editor
{
    // Guided source filters run the real controller. They do not replace keyboard playtesting.
    public static class DownhillControllerVerification
    {
        [Serializable] public sealed class RunResult
        {
            public int Seed, FrameRate, Launches, Landings, ObstacleHits;
            public bool Risky, Passed, Crashed;
            public float Seconds, Condition, MaximumSlip, MaximumAirTime, Distance;
            public string CrashReason,ObstacleId;
            public Vector3 FailurePosition;
        }
        [Serializable] public sealed class Report
        {
            public string TimestampUtc, Kind = "Guided controller simulation, not keyboard play";
            public int Seeds, StructuralFailures, Fallbacks, UniqueActionSequences, CompletedRuns, FailedRuns;
            public int RequestedSeeds, FullyTestedSeeds;
            public bool Finalized;
            public string LastCheckpointUtc;
            public bool ResponsePassed, EdgeFailurePassed, RetryPassed;
            public bool HopPassed, HopSpamRejected, HopResetPassed;
            public float HopAirTime;
            public float ResponseSeconds;
            public bool TopplePassed, CountersteerRecoveryPassed;
            public bool RecoveryWarningReached, StaticOverlapFallPassed, MovingOverlapFallPassed, OutwardOverlapFallPassed;
            public bool AirborneBoundsPassed, ReturnToRoadPassed, OutsideLandingFallPassed, ObstacleFallPassed;
            public bool FluidSpinRightPassed,FluidSpinLeftPassed,FluidHopClearPassed,SpinRetryPassed,SpikeFallPassed;
            public bool EarlyRecoveryWarningReached, EarlyCountersteerPassed;
            public bool SlickCatchTopplePassed;
            public float ToppleWindupSeconds, PeakRecoveryTip;
            public float ToppleStartSpeed, TopplePeakRoll, TopplePeakSlip;
            public float RecoveryStartTip, SlickCatchPeakRoll, SlickCatchPeakSlip;
            public float EarlyRecoveryStartTip, EarlyRecoveryPeakTip, EarlyReleaseSlip, EarlyFinalSlip, EarlyFinalTip;
            public string ToppleTestReason, EdgeTestReason;
            public string SlickCatchReason;
            public string GeometrySeedSweep;
            public List<string> Diagnostics = new List<string>();
            public List<RunResult> Runs = new List<RunResult>();
        }
        static readonly int[] rates = { 30, 60, 120 };
        static readonly int[] representativeSeeds = { 2647, 12, 93, 441, 817, 991 };
        static readonly HashSet<string> sequences = new HashSet<string>();
        static Report report;
        static int seedIndex, totalSeeds, rateIndex, branchIndex;
        static CourseGraph activeCourse;
        static bool fullSuite;
        static string checkpointPath;
        public static bool Running => report != null;

        [MenuItem("DeliveryDash/Verify controller smoke filters")]
        public static void Smoke() => Start(false);
        [MenuItem("DeliveryDash/Verify 1000 seed controller filters")]
        public static void Thousand() => Start(true);
        [MenuItem("DeliveryDash/Stop controller filters")]
        public static void Stop()
        {
            if (report == null) return;
            report.Diagnostics.Add("Stopped before the requested suite completed");
            Finish();
        }
        static void Start(bool all)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Controller filters require Edit mode and coordinated Editor access");
            if (report != null) throw new InvalidOperationException("Controller filters are already running");
            fullSuite = all;
            totalSeeds = all ? 1000 : representativeSeeds.Length;
            seedIndex = rateIndex = branchIndex = 0;
            activeCourse = null;
            sequences.Clear();
            report = new Report { TimestampUtc = DateTime.UtcNow.ToString("O"), RequestedSeeds = totalSeeds };
            checkpointPath = "Captures/controller-filters-checkpoint-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".json";
            try
            {
                report.GeometrySeedSweep = CourseGraph.ValidateSeedSweep(1000);
                CheckResponseAndFailure();
                CheckToppleAndRecovery();
                report.StaticOverlapFallPassed = CheckOverlapFall(false);
                report.MovingOverlapFallPassed = CheckOverlapFall(true);
                report.OutwardOverlapFallPassed = CheckOverlapFall(false, true);
                CheckSlickCatch();
                CheckHop();
                CheckJumpBounds();
                CheckObstacleFall();
                CheckFluidAndSpikes();
            }
            catch (Exception error)
            {
                report.Diagnostics.Add(error.Message);
                Finish();
                throw;
            }
            EditorApplication.update += Tick;
            Debug.Log("Controller filters started: " + totalSeeds + " seeds, both routes, 30/60/120 frame rates");
        }
        static void Tick()
        {
            if (report == null) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                report.Diagnostics.Add("Aborted because Play mode was requested");
                Finish();
                return;
            }
            try
            {
                if (activeCourse == null)
                {
                    int seed = fullSuite ? seedIndex : representativeSeeds[seedIndex];
                    activeCourse = CourseGraph.CreateGenerated(seed);
                    report.Seeds++;
                    if (!activeCourse.Validate(out string diagnostics))
                    {
                        report.StructuralFailures++;
                        report.Diagnostics.Add("Seed " + seed + ": " + diagnostics);
                    }
                    if (activeCourse.UsedFallback) report.Fallbacks++;
                    sequences.Add(activeCourse.ActionSequence);
                }
                RunResult result = Simulate(activeCourse, rates[rateIndex], branchIndex == 1);
                report.Runs.Add(result);
                if (result.Passed) report.CompletedRuns++; else report.FailedRuns++;
                branchIndex++;
                if (branchIndex == 2) { branchIndex = 0; rateIndex++; }
                if (rateIndex == rates.Length)
                {
                    rateIndex = 0; activeCourse = null; seedIndex++;
                    if (seedIndex % 10 == 0)
                    {
                        SaveCheckpoint();
                        Debug.Log("Controller filter seeds: " + seedIndex + "/" + totalSeeds);
                    }
                }
                if (seedIndex >= totalSeeds) Finish();
            }
            catch (Exception exception)
            {
                report.Diagnostics.Add(exception.ToString());
                Finish();
                throw;
            }
        }
        static DownhillCart NewCart(CourseGraph course)
        {
            GameObject sceneCart = GameObject.Find("Courier cart");
            var go = new GameObject("Controller filter") { hideFlags = HideFlags.HideAndDontSave };
            string[] names = { "WheelSpin_FL", "WheelSpin_FR", "WheelSpin_RL", "WheelSpin_RR" };
            Vector3[] points = { new Vector3(-.536f, 0f, .6164f), new Vector3(.536f, 0f, .6164f),
                new Vector3(-.4824f, 0f, -.5896f), new Vector3(.4824f, 0f, -.5896f) };
            if (sceneCart != null)
                foreach (Transform child in sceneCart.GetComponentsInChildren<Transform>())
                    for (int i = 0; i < names.Length; i++)
                        if (child.name == names[i]) points[i] = sceneCart.transform.InverseTransformPoint(child.position);
            for (int i = 0; i < names.Length; i++)
            {
                var wheel = new GameObject(names[i]) { hideFlags = HideFlags.HideAndDontSave };
                wheel.transform.SetParent(go.transform, false);
                wheel.transform.localPosition = points[i];
            }
            var cart = go.AddComponent<DownhillCart>();
            cart.SetKeyboardControl(false);
            cart.Configure(course);
            cart.Begin();
            return cart;
        }
        static RunResult Simulate(CourseGraph course, int frameRate, bool risky)
        {
            DownhillCart cart = NewCart(course);
            var result = new RunResult { Seed = course.Seed, FrameRate = frameRate, Risky = risky };
            float dt = 1f / frameRate, currentAir = 0f;
            var guidance = new GuidanceState(course, risky, dt);
            try
            {
                while (cart.Running && cart.Condition > 0f && cart.SimulationTime < 190f)
                {
                    float lead = Mathf.Clamp(cart.Speed * .65f, 6f, 14f);
                    Vector3 aim = Guidance(course, cart, lead, risky, guidance) - cart.transform.position;
                    float targetHeading = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg;
                    float steer = Mathf.Clamp(Mathf.DeltaAngle(cart.Heading, targetHeading) / 12f, -1f, 1f);
                    cart.Step(dt, steer);
                    result.MaximumSlip = Mathf.Max(result.MaximumSlip, Mathf.Abs(cart.Slip));
                    currentAir = cart.Airborne ? currentAir + dt : 0f;
                    result.MaximumAirTime = Mathf.Max(result.MaximumAirTime, currentAir);
                    Vector3 p = cart.transform.position;
                    if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z)) break;
                }
                result.Seconds = cart.SimulationTime;
                result.Condition = cart.Condition;
                result.Distance = cart.Distance;
                result.Crashed = cart.Crashed;
                result.CrashReason = cart.CrashReason;
                result.ObstacleId=cart.LastObstacleId;result.FailurePosition=cart.transform.position;
                result.Launches = cart.Launches;
                result.Landings = cart.Landings;
                result.ObstacleHits = cart.ObstacleHits;
                result.Passed = !cart.Crashed && cart.Distance >= course.Length - 2f
                    && cart.Condition > 0f && cart.SimulationTime <= 190f;
                return result;
            }
            finally { UnityEngine.Object.DestroyImmediate(cart.gameObject); }
        }
        sealed class GuidanceState
        {
            public CourseObstacle Hazard;
            public float Offset;
            public float ClearStation;
            public readonly float StepSeconds;
            public readonly List<GuidanceHazard> Hazards = new List<GuidanceHazard>();
            public GuidanceState(CourseGraph course, bool risky, float stepSeconds)
            {
                StepSeconds = stepSeconds;
                foreach (CourseObstacle obstacle in course.Obstacles)
                {
                    HazardInterval(course, obstacle, risky, out float left, out float right,
                        out float front, out float rear);
                    Hazards.Add(new GuidanceHazard { Obstacle = obstacle, Left = left, Right = right,
                        Front = front, Rear = rear,
                        LaneLimit = SelectedPath(course, obstacle.Distance, risky).Width * .5f - 1.15f });
                }
                Hazards.Sort((a, b) => a.Obstacle.Distance.CompareTo(b.Obstacle.Distance));
            }
        }
        sealed class GuidanceHazard
        {
            public CourseObstacle Obstacle;
            public float Left, Right, Front, Rear, LaneLimit;
        }

        static CourseSample SelectedPath(CourseGraph course, float station, bool risky)
        {
            if (risky)
                for (int i = 0; i < course.Branches.Count; i++)
                    if (station >= course.Branches[i].StartDistance && station <= course.Branches[i].EndDistance)
                        return course.Sample(station, i + 1);
            return course.Sample(station);
        }

        // A filter has access to the graph; a player still has to read the road.
        // Keep the complete moving envelope clear, rather than dodging a favourable
        // animation frame. Other-road hazards count only when their world bounds
        // actually reach the selected road, including fork entry and join unions.
        static void HazardInterval(CourseGraph course, CourseObstacle obstacle, bool risky,
            out float left, out float right, out float front, out float rear)
        {
            CourseSample path = SelectedPath(course, obstacle.Distance, risky);
            Vector3 across = Vector3.Cross(Vector3.up, path.Forward).normalized;
            Vector3 along = new Vector3(path.Forward.x, 0f, path.Forward.z).normalized;
            Vector3 localAcross = obstacle.Rotation * Vector3.right;
            Vector3 localAlong = obstacle.Rotation * Vector3.forward;
            float halfAcross = Mathf.Abs(Vector3.Dot(localAcross, across)) * obstacle.Size.x * .5f
                + Mathf.Abs(Vector3.Dot(localAlong, across)) * obstacle.Size.z * .5f;
            float halfAlong = Mathf.Abs(Vector3.Dot(localAcross, along)) * obstacle.Size.x * .5f
                + Mathf.Abs(Vector3.Dot(localAlong, along)) * obstacle.Size.z * .5f;
            float motionAcross = obstacle.MotionAmplitude * Mathf.Abs(Vector3.Dot(obstacle.MotionAxis, across));
            float motionAlong = obstacle.MotionAmplitude * Mathf.Abs(Vector3.Dot(obstacle.MotionAxis, along));
            // The real swept sphere is .62 m. Extra allowance covers pursuit's
            // inward curve deviation and the chassis response before its next aim.
            float cartAllowance = obstacle.Kind == ObstacleKind.Fluid ? 1.05f : 1.6f;
            float lateral = Vector3.Dot(obstacle.Position - path.Position, across);
            left = lateral - halfAcross - motionAcross - cartAllowance;
            right = lateral + halfAcross + motionAcross + cartAllowance;
            float stationOffset = Vector3.Dot(obstacle.Position - path.Position, along);
            front = obstacle.Distance + stationOffset - halfAlong - motionAlong - 1.4f;
            rear = obstacle.Distance + stationOffset + halfAlong + motionAlong + 2.2f;
        }

        static Vector3 Guidance(CourseGraph course, DownhillCart cart, float lead, bool risky,
            GuidanceState state)
        {
            float station = cart.Distance;
            float preview = Mathf.Max(60f, cart.Speed * 3.5f);
            if (state.Hazard != null && station > state.ClearStation) state.Hazard = null;
            CourseObstacle next = state.Hazard;
            float first = next == null ? float.MaxValue : next.Distance;
            foreach (GuidanceHazard item in state.Hazards)
            {
                CourseObstacle obstacle = item.Obstacle;
                if (item.Rear < station || item.Front > station + preview) continue;
                if (item.Right < -item.LaneLimit || item.Left > item.LaneLimit) continue;
                if (obstacle.Distance >= first) continue;
                next = obstacle; first = obstacle.Distance;
            }
            if (next != null && next != state.Hazard)
            {
                HazardInterval(course, next, risky, out float left, out float right,
                    out float front, out float rear);
                CourseSample hazardPath = SelectedPath(course, next.Distance, risky);
                float laneLimit = hazardPath.Width * .5f - 1.15f;
                float best = float.MaxValue, chosen = state.Offset;
                Vector3 currentRight = Vector3.Cross(Vector3.up, SelectedPath(course, station, risky).Forward).normalized;
                float currentLane = Vector3.Dot(cart.transform.position - SelectedPath(course, station, risky).Position,
                    currentRight);
                float lateralSpeed = Vector3.Dot(cart.Velocity, currentRight);
                float arrival = Mathf.Max(0f, next.Distance - station) / Mathf.Max(5f, cart.Speed);
                float projectedLane = currentLane + lateralSpeed * Mathf.Min(.5f, arrival);
                // Test both escape corridors. Persisting the previous lane prevents
                // side oscillation as moving cars cross the centre of the viewport.
                for (float offset = -laneLimit; offset <= laneLimit + .001f; offset += .2f)
                {
                    if (offset > left && offset < right) continue;
                    float cost = offset * offset * .12f
                        + Mathf.Pow(offset - state.Offset, 2f) * .65f
                        + Mathf.Pow(offset - projectedLane, 2f) * .08f;
                    // Obstacles occupying the same longitudinal demand must all
                    // have a clear shared lane. Later demands remain separate turns.
                    foreach (GuidanceHazard other in state.Hazards)
                    {
                        if (other.Obstacle == next) continue;
                        if (other.Front > rear + 8f || other.Rear < front - 8f) continue;
                        if (offset > other.Left && offset < other.Right) cost += 10000f;
                    }
                    if (cost >= best) continue;
                    best = cost; chosen = offset;
                }
                state.Hazard = next;
                state.ClearStation = rear;
                state.Offset = chosen;
            }
            else if (next == null) state.Offset = Mathf.MoveTowards(state.Offset, 0f, state.StepSeconds * .8f);
            CourseSample aim = SelectedPath(course, Mathf.Min(course.Length, station + lead), risky);
            Vector3 aimRight = Vector3.Cross(Vector3.up, aim.Forward).normalized;
            float aimOffset = Mathf.Clamp(state.Offset, -aim.Width * .5f + 1.15f, aim.Width * .5f - 1.15f);
            return aim.Position + aimRight * aimOffset;
        }
        static void CheckResponseAndFailure()
        {
            CourseGraph course = CourseGraph.CreateFeel();
            DownhillCart cart = NewCart(course);
            try
            {
                float originalHeading = cart.Heading;
                while (cart.SimulationTime < .2f && Mathf.Abs(Mathf.DeltaAngle(originalHeading, cart.Heading)) < 1f)
                    cart.Step(.01f, 1f);
                report.ResponseSeconds = cart.SimulationTime;
                report.ResponsePassed = report.ResponseSeconds <= .15f;
                cart.Configure(course); cart.Begin();
                while (cart.Running && cart.SimulationTime < 8f) cart.Step(.02f, .18f);
                report.EdgeTestReason = cart.CrashReason;
                report.EdgeFailurePassed = cart.Crashed && cart.CrashReason == "Edge" && cart.CrashSpeed > 0f;
                int version = cart.ResetVersion;
                cart.Configure(course);
                report.RetryPassed = !cart.Crashed && !cart.Running && cart.Condition == 100f
                    && cart.ResetVersion == version + 1 && cart.SimulationTime == 0f
                    && Mathf.Abs(cart.Speed - 9f) < .001f;
            }
            finally { UnityEngine.Object.DestroyImmediate(cart.gameObject); }
        }
        static void CheckToppleAndRecovery()
        {
            // This synthetic wide straight removes edge collisions from the COM test.
            CourseGraph course = WideStraight();
            DownhillCart cart = NewCart(course);
            try
            {
                while (cart.Running && cart.Speed < 21.9f && cart.SimulationTime < 30f) cart.Step(.02f, 0f);
                report.ToppleStartSpeed = cart.Speed;
                float onset = cart.SimulationTime;
                while (cart.Running && cart.SimulationTime - onset < 4f)
                {
                    cart.Step(.02f, 1f);
                    report.TopplePeakRoll = Mathf.Max(report.TopplePeakRoll, Mathf.Abs(cart.BodyRoll));
                    report.TopplePeakSlip = Mathf.Max(report.TopplePeakSlip, Mathf.Abs(cart.Slip));
                }
                report.ToppleTestReason = cart.CrashReason;
                report.ToppleWindupSeconds = cart.SimulationTime - onset;
                report.TopplePassed = cart.Crashed && cart.CrashReason == "Topple"
                    && report.ToppleWindupSeconds > .35f;
                cart.Configure(course); cart.Begin();
                while (cart.Running && cart.Speed < 21.9f && cart.SimulationTime < 30f) cart.Step(.02f, 0f);
                onset = cart.SimulationTime;
                while (cart.Running && cart.TipAmount < .6f && cart.SimulationTime - onset < 2f) cart.Step(.02f, 1f);
                report.RecoveryStartTip = cart.TipAmount;
                report.RecoveryWarningReached = cart.TipAmount >= .6f;
                float recovery = cart.SimulationTime;
                while (cart.Running && cart.SimulationTime - recovery < 1.2f)
                {
                    cart.Step(.02f, Mathf.Abs(cart.Slip) < 3f ? 0f : Mathf.Clamp(cart.Slip / 12f, -1f, 1f));
                    report.PeakRecoveryTip = Mathf.Max(report.PeakRecoveryTip, cart.TipAmount);
                }
                report.CountersteerRecoveryPassed = report.RecoveryWarningReached
                    && !cart.Crashed && cart.TipAmount < .6f;
                // Keep the late .6 case above. This separate case intervenes at
                // the first substantial wheel-lift warning, before COM crossing.
                cart.Configure(course); cart.Begin();
                while (cart.Running && cart.Speed < 21.9f && cart.SimulationTime < 30f) cart.Step(.02f, 0f);
                onset = cart.SimulationTime;
                while (cart.Running && cart.TipAmount < .35f && cart.SimulationTime - onset < 2f) cart.Step(.02f, 1f);
                report.EarlyRecoveryStartTip = cart.TipAmount;
                report.EarlyRecoveryWarningReached = cart.TipAmount >= .35f;
                report.EarlyReleaseSlip = cart.Slip;
                recovery = cart.SimulationTime;
                while (cart.Running && cart.SimulationTime - recovery < 1.2f)
                {
                    cart.Step(.02f, Mathf.Abs(cart.Slip) < 3f ? 0f : Mathf.Clamp(cart.Slip / 12f, -1f, 1f));
                    report.EarlyRecoveryPeakTip = Mathf.Max(report.EarlyRecoveryPeakTip, cart.TipAmount);
                }
                report.EarlyFinalSlip = cart.Slip;
                report.EarlyFinalTip = cart.TipAmount;
                report.EarlyCountersteerPassed = report.EarlyRecoveryWarningReached && !cart.Crashed
                    && cart.TipAmount < .35f && Mathf.Abs(cart.Slip) < 6f;
            }
            finally { UnityEngine.Object.DestroyImmediate(cart.gameObject); }
        }
        static void CheckHop()
        {
            var cart = NewCart(WideStraight());
            try
            {
                bool accepted = cart.RequestHop();
                bool repeated = cart.RequestHop();
                cart.Step(.02f, .25f);
                float air = 0;
                while (cart.Running && cart.Airborne && cart.SimulationTime < 3f)
                { cart.Step(.02f, 0f); air += .02f; }
                report.HopAirTime = air;
                report.HopPassed = accepted && cart.Hops == 1 && cart.Landings == 1 && air > .65f && !cart.Crashed;
                report.HopSpamRejected = !repeated && !cart.RequestHop() && cart.HopCooldown > 0;
                cart.Configure(WideStraight());
                report.HopResetPassed = cart.Hops == 0 && cart.HopCooldown == 0f && !cart.Airborne;
            }
            finally { UnityEngine.Object.DestroyImmediate(cart.gameObject); }
        }

        static void CheckFluidAndSpikes()
        {
            var course=WideStraight();
            course.Obstacles.Add(new CourseObstacle{Id="fluid-test",Kind=ObstacleKind.Fluid,
                Position=new Vector3(0,-.36f,2),Size=new Vector3(3.1f,.08f,5.5f),Rotation=Quaternion.identity,
                Distance=2,Severity=0});
            var cart=NewCart(course);
            try
            {
                for(int direction=-1;direction<=1;direction+=2)
                {
                    cart.Configure(course);cart.Begin();float heading=cart.Heading;
                    cart.Step(.02f,direction);
                    while(cart.Running&&cart.Spinning&&cart.SimulationTime<2)cart.Step(.02f,0);
                    bool passed=!cart.Crashed&&cart.Spins==1&&direction*(cart.Heading-heading)>350;
                    if(direction<0)report.FluidSpinLeftPassed=passed;else report.FluidSpinRightPassed=passed;
                }
                cart.Configure(course);
                report.SpinRetryPassed=cart.Spins==0&&!cart.Spinning;
                cart.Begin();cart.RequestHop();cart.Step(.02f,0);
                // Enter the patch during an established flight, clear it before landing.
                Vector3 p=cart.transform.position;p.x=20;cart.transform.position=p;
                cart.Step(.02f,0);p=cart.transform.position;p.x=0;cart.transform.position=p;
                while(cart.Running&&cart.Airborne&&cart.SimulationTime<2)cart.Step(.02f,0);
                report.FluidHopClearPassed=cart.Spins==0&&!cart.Crashed&&cart.Landings==1;
                course.Obstacles.Clear();
                course.Obstacles.Add(new CourseObstacle{Id="spike-test",Kind=ObstacleKind.Spikes,
                    Position=new Vector3(0,-1.8f,10),Size=new Vector3(2.6f,.72f,2.4f),Rotation=Quaternion.identity,
                    Distance=10,Severity=3});
                cart.Configure(course);cart.Begin();
                while(cart.Running&&cart.SimulationTime<3)cart.Step(.02f,0);
                report.SpikeFallPassed=cart.Crashed&&cart.CrashReason=="Impact";
            }
            finally{UnityEngine.Object.DestroyImmediate(cart.gameObject);}
        }

        static CourseGraph WideStraight()
        {
            CourseGraph course = CourseGraph.CreateFeel();
            course.Obstacles.Clear();
            for (int i = 0; i < course.Samples.Count; i++)
            {
                CourseSample sample = course.Samples[i];
                sample.Position = new Vector3(0f, -.18f * i, i);
                sample.Forward = new Vector3(0f, -.18f, 1f).normalized;
                sample.Width = 100f; sample.Grip = 1f; sample.Surface = CourseSurface.Stone;
                sample.Event = CourseEvent.None; sample.LaunchCue = 0f;
                course.Samples[i] = sample;
            }
            return course;
        }
        static void CheckJumpBounds()
        {
            CourseGraph course=CourseGraph.CreateFeel();
            course.Obstacles.Clear();
            var cart=NewCart(course);
            try
            {
                for(int run=0;run<2;run++)
                {
                    cart.Configure(course);cart.Begin();cart.RequestHop();cart.Step(.02f,0);
                    var start=course.Sample(cart.Distance);
                    Vector3 p=cart.transform.position;
                    p.x=start.Position.x+start.Width*.5f+2;
                    p.y+=1;
                    cart.transform.position=p;
                    for(int i=0;i<12&&cart.Running;i++)cart.Step(.02f,0);
                    report.AirborneBoundsPassed=cart.Running&&cart.Airborne&&!cart.Crashed;
                    if(run==0)
                    {
                        p=cart.transform.position;p.x=course.Sample(cart.Distance).Position.x;
                        cart.transform.position=p;
                    }
                    while(cart.Running&&cart.Airborne&&cart.SimulationTime<4)cart.Step(.02f,0);
                    if(run==0)report.ReturnToRoadPassed=!cart.Crashed&&!cart.Airborne&&cart.Landings==1;
                    else report.OutsideLandingFallPassed=cart.Crashed&&!cart.Airborne&&cart.CrashReason=="Edge";
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(cart.gameObject);}
        }

        static void CheckObstacleFall()
        {
            var course=WideStraight();
            course.Obstacles.Add(new CourseObstacle{Id="fatal-crate",Kind=ObstacleKind.Crates,
                Position=new Vector3(0,-1.8f,10),Size=new Vector3(2,2,2),Rotation=Quaternion.identity,
                Distance=10,Severity=1});
            var cart=NewCart(course);
            try
            {
                while(cart.Running&&cart.SimulationTime<3)cart.Step(.02f,0);
                report.ObstacleFallPassed=cart.Crashed&&cart.CrashReason=="Impact"&&cart.ObstacleHits==1;
            }
            finally{UnityEngine.Object.DestroyImmediate(cart.gameObject);}
        }

        static bool CheckOverlapFall(bool moving, bool outward = false)
        {
            CourseGraph course = WideStraight();
            var obstacle = new CourseObstacle
            {
                Id = outward ? "outward-overlap-test" : moving ? "moving-overlap-test" : "static-overlap-test",
                Kind = moving ? ObstacleKind.MarketTrolley : ObstacleKind.Crates,
                Position = new Vector3(0f, -18f, 100f), Size = new Vector3(1.9f, 1.6f, 1.5f),
                Rotation = Quaternion.identity, Distance = 100f, Severity = 1f,
                MotionAxis = Vector3.right, MotionAmplitude = moving ? .65f : 0f
            };
            course.Obstacles.Add(obstacle);
            DownhillCart cart = NewCart(course);
            try
            {
                // Owner requires every physical obstacle contact to trigger a fall.
                cart.transform.position = obstacle.Position + Vector3.up * .09f;
                if (outward)
                {
                    // Begin just inside the forward swept-sphere face. The cart's
                    // launch velocity exits this face on its first integration step;
                    // an outward escape still counts as an existing fatal contact.
                    cart.transform.position += Vector3.forward * (obstacle.Size.z * .5f + .62f - .03f);
                    cart.Step(.02f, 0f);
                    return cart.Crashed && cart.CrashReason == "Impact" && cart.ObstacleHits == 1
                        && cart.LastObstacleId == obstacle.Id;
                }
                while (cart.Running && cart.Condition > 0f && cart.SimulationTime < 2f) cart.Step(.02f, 0f);
                return cart.Crashed&&cart.CrashReason=="Impact"&&cart.ObstacleHits==1;
            }
            finally { UnityEngine.Object.DestroyImmediate(cart.gameObject); }
        }
        static void CheckSlickCatch()
        {
            CourseGraph course = WideStraight();
            for (int i = 0; i < 180; i++)
            {
                CourseSample sample = course.Samples[i];
                sample.Grip = .48f; sample.Surface = CourseSurface.Slick;
                course.Samples[i] = sample;
            }
            DownhillCart cart = NewCart(course);
            try
            {
                while (cart.Running && cart.Distance < 170f) cart.Step(.02f, 0f);
                float onset = cart.SimulationTime;
                while (cart.Running && cart.SimulationTime - onset < 4f)
                {
                    cart.Step(.02f, 1f);
                    report.SlickCatchPeakRoll = Mathf.Max(report.SlickCatchPeakRoll, Mathf.Abs(cart.BodyRoll));
                    report.SlickCatchPeakSlip = Mathf.Max(report.SlickCatchPeakSlip, Mathf.Abs(cart.Slip));
                }
                report.SlickCatchReason = cart.CrashReason;
                report.SlickCatchTopplePassed = cart.Crashed && cart.CrashReason == "Topple";
            }
            finally { UnityEngine.Object.DestroyImmediate(cart.gameObject); }
        }
        static void SaveCheckpoint()
        {
            report.FullyTestedSeeds = seedIndex;
            report.UniqueActionSequences = sequences.Count;
            report.LastCheckpointUtc = DateTime.UtcNow.ToString("O");
            Directory.CreateDirectory("Captures");
            string temporary = checkpointPath + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(report, true));
            if (File.Exists(checkpointPath)) File.Replace(temporary, checkpointPath, null);
            else File.Move(temporary, checkpointPath);
        }
        static void Finish()
        {
            EditorApplication.update -= Tick;
            report.FullyTestedSeeds = seedIndex;
            report.Finalized = true;
            report.UniqueActionSequences = sequences.Count;
            Directory.CreateDirectory("Captures");
            string path = "Captures/controller-filters-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".json";
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            Debug.Log("Controller filter report: " + path + "; completed " + report.CompletedRuns
                + "; failed " + report.FailedRuns + "; structural failures " + report.StructuralFailures);
            report = null; activeCourse = null;
        }
    }
}
