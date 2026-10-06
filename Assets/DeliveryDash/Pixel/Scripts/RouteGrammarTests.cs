using System;
using System.Collections.Generic;

namespace DeliveryDash.Pixel
{
    // Callable from editor automation without entering Play mode.
    public static class RouteGrammarTests
    {
        public sealed class SweepReport
        {
            public int SeedsChecked;
            public int Failures;
            public int Fallbacks;
            public int UniqueActionSequences;
            public float ShortestExpectedSeconds = float.MaxValue;
            public float LongestExpectedSeconds;
            public string FirstFailure;
            public override string ToString()
            {
                return "Checked " + SeedsChecked + " seeds; failures " + Failures +
                    "; fallbacks " + Fallbacks +
                    "; action sequences " + UniqueActionSequences +
                    "; expected seconds " + ShortestExpectedSeconds.ToString("F1") +
                    " to " + LongestExpectedSeconds.ToString("F1") +
                    (FirstFailure == null ? "" : "; first failure: " + FirstFailure);
            }
        }

        public static SweepReport RunSweep(int firstSeed = 0, int count = 500)
        {
            SweepReport report = new SweepReport();
            HashSet<string> sequences = new HashSet<string>();
            for (int seed = firstSeed; seed < firstSeed + count; seed++)
            {
                RoutePlan plan = RouteGrammar.Generate(seed);
                report.SeedsChecked++;
                if (plan.UsedFallback)
                {
                    report.Fallbacks++;
                    Fail(report, seed, "Generator used fallback");
                }
                string diagnostics;
                if (!plan.Validate(out diagnostics)) Fail(report, seed, diagnostics);
                RoutePlan repeat = RouteGrammar.Generate(seed);
                if (Fingerprint(plan) != Fingerprint(repeat)) Fail(report, seed, "Replay changed route data");
                for (int i = 0; i < plan.Branches.Count; i++)
                {
                    RouteBranch branch = plan.Branches[i];
                    float middle = (branch.SplitEnd + branch.MergeStart) * 0.5f;
                    if (!branch.IsRisky(middle, branch.RiskyCenterOffset(middle)) ||
                        !branch.IsSafe(middle, branch.SafeCenterOffset(middle)))
                        Fail(report, seed, "Branch corridor cannot be entered");
                    if (Math.Abs(branch.RiskyCenterOffset(branch.MergeEnd)) > 0.001f ||
                        Math.Abs(branch.SafeCenterOffset(branch.SplitStart)) > 0.001f)
                        Fail(report, seed, "Branch does not rejoin");
                }
                report.ShortestExpectedSeconds = Math.Min(report.ShortestExpectedSeconds, plan.ExpectedSeconds);
                report.LongestExpectedSeconds = Math.Max(report.LongestExpectedSeconds, plan.ExpectedSeconds);
                sequences.Add(ActionSequence(plan));
            }
            report.UniqueActionSequences = sequences.Count;
            if (count >= 100 && report.UniqueActionSequences < 8)
                Fail(report, firstSeed, "Insufficient action sequence variety");
            return report;
        }

        private static void Fail(SweepReport report, int seed, string reason)
        {
            report.Failures++;
            if (report.FirstFailure == null) report.FirstFailure = "seed " + seed + ": " + reason;
        }

        private static string ActionSequence(RoutePlan plan)
        {
            string sequence = "";
            for (int i = 0; i < plan.Beats.Count; i++)
            {
                RouteBeat beat = plan.Beats[i];
                if (beat.Kind == RouteBeatKind.Bump || beat.Kind == RouteBeatKind.Ramp ||
                    beat.Kind == RouteBeatKind.Conveyor || beat.Kind == RouteBeatKind.Slick ||
                    beat.Kind == RouteBeatKind.Scrape)
                    sequence += (int)beat.Kind + ":" + Math.Sign(beat.Offset) + ",";
            }
            for (int i = 0; i < plan.Branches.Count; i++) sequence += "F" + plan.Branches[i].RiskySide;
            return sequence;
        }

        private static string Fingerprint(RoutePlan plan)
        {
            string value = plan.Seed + ":" + plan.LengthMeters + ":";
            for (int i = 0; i < plan.Beats.Count; i++)
            {
                RouteBeat beat = plan.Beats[i];
                value += beat.Id + "/" + beat.Kind + "/" + beat.Start + "/" + beat.End +
                    "/" + beat.Offset + "/" + beat.Severity + ";";
            }
            for (int i = 0; i < plan.Branches.Count; i++)
            {
                RouteBranch branch = plan.Branches[i];
                value += branch.RiskySide + "/" + branch.SplitStart + "/" + branch.MergeEnd + ";";
            }
            for (int d = 0; d < plan.LengthMeters; d += 10) value += plan.CenterX(d) + ",";
            return value;
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("DeliveryDash/Pixel/Validate 500 Route Seeds")]
        private static void RunEditorSweep()
        {
            SweepReport report = RunSweep();
            if (report.Failures > 0) UnityEngine.Debug.LogError(report.ToString());
            else UnityEngine.Debug.Log(report.ToString());
        }
#endif
    }
}
