using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeliveryDash.Editor
{
    // Observe the real keyboard-controlled session. This never supplies steering.
    [InitializeOnLoad]
    public static class DownhillObservation
    {
        static double nextSample;
        static int reset = -1, launches, landings, scrapes;
        static bool turnCaptured, finishedCaptured;
        static string evidence;
        static DownhillObservation() { EditorApplication.update += Observe; }

        static void Observe()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling ||
                EditorApplication.timeSinceStartup < nextSample) return;
            nextSample = EditorApplication.timeSinceStartup + .1;
            var cart = UnityEngine.Object.FindFirstObjectByType<Downhill.DownhillCart>();
            var session = UnityEngine.Object.FindFirstObjectByType<Downhill.DownhillSession>();
            if (cart == null || session == null) return;
            Directory.CreateDirectory("Captures");
            if (cart.ResetVersion != reset)
            {
                reset = cart.ResetVersion;
                evidence = "Captures/keyboard-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + reset;
                launches = landings = scrapes = 0;
                turnCaptured = finishedCaptured = false;
                File.WriteAllText(evidence + ".csv", "utc,state,elapsed,distance,speed,steering,slip,contacts,airborne,condition,a,d,left,right,scrapes,launches,landings,recoveries,seed,space,hops,hopCooldown,bodyRoll,tip\n");
            }
            string row = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0},{1},{2:F3},{3:F3},{4:F3},{5:F3},{6:F3},{7},{8},{9:F2},{10},{11},{12},{13},{14},{15},{16},{17}\n",
                DateTime.UtcNow.ToString("O"), session.State, session.Elapsed, cart.Distance,
                cart.Speed, cart.Steer, cart.Slip, cart.GroundContacts, cart.Airborne,
                cart.Condition, Input.GetKey(KeyCode.A), Input.GetKey(KeyCode.D),
                Input.GetKey(KeyCode.LeftArrow), Input.GetKey(KeyCode.RightArrow),
                cart.Scrapes, cart.Launches, cart.Landings, cart.Recoveries);
            row = row.TrimEnd('\n') + string.Format(System.Globalization.CultureInfo.InvariantCulture,
                ",{0},{1},{2},{3:F3},{4:F3},{5:F3}\n", session.Seed, Input.GetKey(KeyCode.Space),
                cart.Hops, cart.HopCooldown, cart.BodyRoll, cart.TipAmount);
            File.AppendAllText(evidence + ".csv", row);
            if (!turnCaptured && Mathf.Abs(cart.Steer) > .5f && cart.Distance > 35)
            { CaptureBeat("turn"); turnCaptured = true; }
            if (cart.Launches > launches) CaptureBeat("takeoff");
            if (cart.Landings > landings) CaptureBeat("landing");
            if (cart.Scrapes > scrapes && scrapes == 0) CaptureBeat("scrape");
            launches = cart.Launches; landings = cart.Landings; scrapes = cart.Scrapes;
            if (!finishedCaptured && (session.State == "Delivered" || session.State == "Game over"))
            { CaptureBeat("finish"); finishedCaptured = true; }
        }

        static void CaptureBeat(string beat)
        {
            if (Camera.main != null) BuildLookDev.Capture(Camera.main, evidence + "-" + beat + ".png");
        }

        [MenuItem("DeliveryDash/Capture downhill Game View")]
        public static void CaptureGameView()
        {
            Directory.CreateDirectory("Captures");
            ScreenCapture.CaptureScreenshot("Captures/downhill-gameview-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".png");
        }

        [MenuItem("DeliveryDash/Focus downhill Game View")]
        public static void FocusGameView()
        {
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (type != null) EditorWindow.GetWindow(type).Focus();
        }
    }
}
