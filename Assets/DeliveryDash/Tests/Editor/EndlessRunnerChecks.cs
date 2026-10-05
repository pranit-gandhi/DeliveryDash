using System;
using UnityEditor;
using UnityEngine;

namespace DeliveryDash.Editor
{
    public static class EndlessRunnerChecks
    {
        static void Require(bool condition, string reason)
        {
            if (!condition) throw new InvalidOperationException("Endless runner check failed: " + reason);
        }

        [MenuItem("DeliveryDash/Checks/Validate 500 seeds and shift rules")]
        public static void Validate()
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            for (int seed = 0; seed < 500; seed++)
            {
                for (int section = 0; section < 80; section++)
                {
                    double start = section * (double)DeliveryDash.EndlessRoadLayout.Length;
                    Vector3 previous = DeliveryDash.EndlessRoadLayout.Sample(seed, start, 0, out _);
                    for (int step = 1; step <= 32; step++)
                    {
                        double station = start + step * 1.5;
                        Vector3 point = DeliveryDash.EndlessRoadLayout.Sample(seed, station, 0, out float width);
                        Require(point.y < previous.y, "road must always descend");
                        Require(Mathf.Abs(point.x - previous.x) / 1.5f < .23f, "bend exceeds steering margin");
                        Require(width >= 4.8f, "minimum drivable width");
                        Require(width - .75f >= 4.05f - .001f, "median passage must fit cart plus clearance");
                        Require(point == DeliveryDash.EndlessRoadLayout.Sample(seed, station, 0, out _), "seed determinism");
                        Vector3 shifted = DeliveryDash.EndlessRoadLayout.Sample(seed, station, 768, out _);
                        Require(Mathf.Abs(point.y + 768 * DeliveryDash.EndlessRoadLayout.Grade - shifted.y) < .001f, "origin shift height continuity");
                        previous = point;
                    }
                    double boundary = start + DeliveryDash.EndlessRoadLayout.Length;
                    Vector3 left = DeliveryDash.EndlessRoadLayout.Sample(seed, boundary - .0001, 0, out float wl);
                    Vector3 right = DeliveryDash.EndlessRoadLayout.Sample(seed, boundary + .0001, 0, out float wr);
                    Require(Vector3.Distance(left, right) < .001f && Mathf.Abs(wl - wr) < .001f, "section join continuity");
                    if (section % 2 == 0) Require(DeliveryDash.EndlessRoadLayout.Kind(seed, section) == DeliveryDash.EndlessRoadLayout.Beat.Cruise, "recovery beat spacing");
                }
            }
            Require(DeliveryDash.EndlessRoadLayout.Hash(6553, 12) == 3661049828u, "golden seed hash");
            var rules = new DeliveryDash.DeliveryShiftRules();
            rules.Tick(10, 288, false);
            Require(rules.Deliveries == 1 && rules.Tips == 10 && rules.TimeLeft == 65, "delivery bonus and tip");
            rules.Tick(0, 289, false);
            Require(rules.Deliveries == 1, "no duplicate delivery");
            rules.Reset();
            Require(rules.Tick(1, 0, true) && rules.TimeLeft == 56 && rules.Condition == 80, "collision penalty");
            Require(!rules.Tick(.1f, 0, true) && rules.Condition == 80, "sustained scrape cooldown");
            rules.Tick(0, 288, false);
            Require(rules.Tips == 8 && rules.Condition == 100, "damaged tip and fresh pizza");
            rules.Reset(); rules.Tick(60, 288, false);
            Require(rules.Finished && rules.Deliveries == 0, "expiry cannot deliver or revive");
            rules.Tick(0, 576, false);
            Require(rules.Finished && rules.Deliveries == 0, "finished rules immutable");
            rules.Reset(); Require(!rules.Finished && rules.TimeLeft == 60 && rules.Deliveries == 0, "restart");
            Debug.Log($"PASS: 500 seeds x 80 sections, downhill grade, steering slope, passage clearance, deterministic joins and rebasing; shift rules. {clock.ElapsedMilliseconds} ms.");
        }

        static DeliveryDash.EndlessRoadGenerator road;
        static DeliveryDash.CartFeelController cart;
        static int probeStep;
        static double nextProbe;

        [MenuItem("DeliveryDash/Checks/Probe streaming in Play mode")]
        public static void Probe()
        {
            Require(EditorApplication.isPlaying, "enter Play mode first");
            road = UnityEngine.Object.FindFirstObjectByType<DeliveryDash.EndlessRoadGenerator>();
            cart = UnityEngine.Object.FindFirstObjectByType<DeliveryDash.CartFeelController>();
            Require(road != null && cart != null, "open EndlessRun");
            road.Restart(6553);
            probeStep = 0; nextProbe = 0;
            EditorApplication.update -= ProbeTick;
            EditorApplication.update += ProbeTick;
        }

        static void ProbeTick()
        {
            if (!EditorApplication.isPlaying || road == null || cart == null)
            {
                EditorApplication.update -= ProbeTick;
                return;
            }
            if (EditorApplication.timeSinceStartup < nextProbe) return;
            nextProbe = EditorApplication.timeSinceStartup + .15;
            try
            {
                double station = probeStep * 48 + .2;
                Vector3 p = DeliveryDash.EndlessRoadLayout.Sample(road.Seed, station, road.Origin, out _);
                cart.ResetAt(p + Vector3.up * .15f, Quaternion.identity);
                road.MaintainRoad();
                Physics.SyncTransforms();
                Require(road.LoadedSections <= 9, "streamed section count must stay bounded");
                p = DeliveryDash.EndlessRoadLayout.Sample(road.Seed, station, road.Origin, out _);
                Require(Math.Abs(road.Distance - station) < .01, "distance survives rebase");
                Require(Physics.Raycast(p + Vector3.right + Vector3.up * 2, Vector3.down, out RaycastHit hit, 4), "road collider after stream/rebase");
                Require(Mathf.Abs(hit.point.y - p.y) < .02f, "road collider matches analytic height");
                // Both sides of each join have solid road, including after rebasing.
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 seam = DeliveryDash.EndlessRoadLayout.Sample(road.Seed, station - .2 + side * .02, road.Origin, out _);
                    Require(Physics.Raycast(seam + Vector3.right + Vector3.up * 2, Vector3.down, 4), "seam collision coverage");
                }
                probeStep++;
                if (probeStep == 41)
                {
                    Require(road.Origin >= 1536, "at least two origin shifts tested");
                    road.Restart(6553);
                    Require(road.Distance < .01 && road.Origin == 0 && road.LoadedSections == 8, "restart clears old stream");
                    EditorApplication.update -= ProbeTick;
                    Debug.Log("PASS: live streaming probe, 41 sections / 1,920 m, two origin shifts, collider seams, max nine sections, and restart.");
                }
            }
            catch (Exception e)
            {
                EditorApplication.update -= ProbeTick;
                Debug.LogException(e);
            }
        }

        static DeliveryDash.DeliveryShift liveShift;
        static float oldTimeScale;
        static bool oldBackground;
        static double liveStarted, finishCaptured;
        static bool capturedDelivery;

        [MenuItem("DeliveryDash/Checks/Play a complete shift automatically")]
        public static void PlayShift()
        {
            Require(EditorApplication.isPlaying, "enter Play mode first");
            road = UnityEngine.Object.FindFirstObjectByType<DeliveryDash.EndlessRoadGenerator>();
            cart = UnityEngine.Object.FindFirstObjectByType<DeliveryDash.CartFeelController>();
            liveShift = UnityEngine.Object.FindFirstObjectByType<DeliveryDash.DeliveryShift>();
            Require(road != null && cart != null && liveShift != null, "open EndlessRun");
            road.Restart(6553);
            oldTimeScale = Time.timeScale;
            oldBackground = Application.runInBackground;
            Application.runInBackground = true;
            EditorApplication.isPaused = false;
            Time.timeScale = 3;
            liveStarted = EditorApplication.timeSinceStartup;
            finishCaptured = 0; capturedDelivery = false;
            EditorApplication.update -= LiveTick;
            EditorApplication.update += LiveTick;
        }

        static void EndLiveCheck()
        {
            Time.timeScale = oldTimeScale;
            Application.runInBackground = oldBackground;
            EditorApplication.update -= LiveTick;
        }

        [MenuItem("DeliveryDash/Checks/Capture route review in Play mode")]
        public static void CaptureReview()
        {
            Require(EditorApplication.isPlaying, "enter Play mode first");
            var generator = UnityEngine.Object.FindFirstObjectByType<DeliveryDash.EndlessRoadGenerator>();
            var runner = UnityEngine.Object.FindFirstObjectByType<DeliveryDash.CartFeelController>();
            var camera = UnityEngine.Object.FindFirstObjectByType<DeliveryDash.ChaseCamera>();
            Require(generator != null && runner != null && camera != null, "open EndlessRun");
            generator.Restart(6553);
            double[] stations = { 268, 344 };
            string[] paths = { "Captures/endless-customer-gate.png", "Captures/endless-planters.png" };
            for (int i = 0; i < stations.Length; i++)
            {
                Vector3 point = DeliveryDash.EndlessRoadLayout.Sample(generator.Seed, stations[i], generator.Origin, out _);
                runner.ResetAt(point + Vector3.up * .1f, Quaternion.identity);
                generator.MaintainRoad();
                Physics.SyncTransforms();
                camera.SetTarget(runner.transform);
                BuildLookDev.Capture(camera.GetComponent<Camera>(), paths[i]);
            }
            generator.Restart(6553);
            Debug.Log("Saved route review captures; reset to the start.");
        }

        static void LiveTick()
        {
            if (!EditorApplication.isPlaying || road == null || liveShift == null) { EndLiveCheck(); return; }
            try
            {
                Require(EditorApplication.timeSinceStartup - liveStarted < 100, "full shift timed out");
                if (!capturedDelivery && liveShift.Rules.Deliveries > 0)
                {
                    capturedDelivery = true;
                    System.IO.Directory.CreateDirectory("Captures");
                    ScreenCapture.CaptureScreenshot("Captures/endless-first-delivery.png");
                    Debug.Log($"Live delivery reached: {road.Distance:F1} m, {liveShift.Rules.TimeLeft:F1}s left, grounded={cart.IsGrounded}.");
                }
                if (!liveShift.Rules.Finished) return;
                Require(capturedDelivery, "normal forward drive should reach a customer");
                Require(!cart.enabled, "timer expiry stops the cart");
                Require(road.LoadedSections <= 9, "bounded sections during actual motion");
                if (finishCaptured == 0)
                {
                    ScreenCapture.CaptureScreenshot("Captures/endless-shift-results.png");
                    finishCaptured = EditorApplication.timeSinceStartup;
                    Debug.Log($"Live shift ended: {liveShift.Rules.Deliveries} deliveries, {road.Distance:F1} m, ${liveShift.Rules.Tips} tips.");
                    return;
                }
                if (EditorApplication.timeSinceStartup - finishCaptured < .5) return;
                road.Restart(42);
                Require(road.Seed == 42 && road.Distance == 0 && cart.enabled && !liveShift.Rules.Finished && liveShift.Rules.Deliveries == 0, "new-seed restart restores shift and movement");
                road.Restart(6553);
                EndLiveCheck();
                Debug.Log("PASS: actual automatic driving, customer delivery, timer expiry, cart stop, bounded streaming, and new/same-seed restart.");
            }
            catch (Exception e) { EndLiveCheck(); Debug.LogException(e); }
        }
    }
}
