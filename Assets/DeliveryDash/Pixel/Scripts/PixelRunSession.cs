using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeliveryDash.Pixel
{
    public enum PixelSessionState
    {
        Ready,
        Running,
        Paused,
        Delivered,
        Failed
    }

    [RequireComponent(typeof(PixelRunController))]
    public sealed class PixelRunSession : MonoBehaviour
    {
        [SerializeField] private int initialSeed;
        [SerializeField] private bool autoStart;
        [SerializeField, Range(1f, 100f)] private float minimumDeliveryCondition = 20f;

        private PixelRunController cart;
        private PixelAudio audioFeedback;
        private readonly HashSet<int> triggeredBeats = new HashSet<int>();
        private readonly HashSet<int> riskyBranches = new HashSet<int>();
        private float branchScrapeTimer;
        private float elapsed;
        private bool focusPause;
        private bool manualStepMode;

        public PixelRunController Cart => cart;
        public RoutePlan Plan { get; private set; }
        public PixelSessionState State { get; private set; } = PixelSessionState.Ready;
        public int Seed => Plan == null ? 0 : Plan.Seed;
        public float SecondsElapsed => elapsed;
        public float SecondsRemaining => Plan == null ? 0f : Mathf.Max(0f, Plan.DeadlineSeconds - elapsed);
        public float Progress => Plan == null || cart == null ? 0f : Mathf.Clamp01(cart.DistanceMeters / Plan.LengthMeters);
        public float Condition => cart == null ? 100f : cart.Condition;
        public int RiskyBranchesTaken => riskyBranches.Count;
        public bool IsMuted => audioFeedback != null && audioFeedback.Muted;
        public bool ManualStepMode => manualStepMode;
        public string ResultText => State == PixelSessionState.Delivered ? "Pizza delivered" :
            State == PixelSessionState.Failed ? "Try again" : string.Empty;

        private void Awake()
        {
            cart = GetComponent<PixelRunController>();
            audioFeedback = GetComponent<PixelAudio>();
            if (audioFeedback == null) audioFeedback = gameObject.AddComponent<PixelAudio>();
            if (GetComponent<PixelMenuView>() == null) gameObject.AddComponent<PixelMenuView>();
        }

        private void Start()
        {
            int seed = initialSeed != 0 ? initialSeed : Environment.TickCount;
            BeginRun(seed, autoStart);
        }

        private void Update()
        {
            if (manualStepMode) return;
            if (Input.GetKeyDown(KeyCode.M)) ToggleMute();
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (State == PixelSessionState.Running) Pause();
                else if (State == PixelSessionState.Paused) Resume();
            }
            if (State == PixelSessionState.Delivered || State == PixelSessionState.Failed)
            {
                if (Input.GetKeyDown(KeyCode.R)) RetrySameSeed();
                else if (Input.GetKeyDown(KeyCode.N)) NewSeed();
            }
            if (State != PixelSessionState.Running || Plan == null || cart == null) return;

            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0f) return;
            float input = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) input -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) input += 1f;

            Tick(dt, input);
        }

        // Play-mode automation uses the same tick as normal keyboard gameplay.
        // Enable manual stepping before beginning a seeded test run.
        public void SetManualStepMode(bool enabled)
        {
            manualStepMode = enabled;
            if (enabled) focusPause = false;
        }

        public float StepForTest(float seconds, float steering)
        {
            if (!Application.isPlaying || !manualStepMode || State != PixelSessionState.Running)
                return 0f;
            float remaining = Mathf.Clamp(seconds, 0f, 300f);
            float simulated = 0f;
            float input = Mathf.Clamp(steering, -1f, 1f);
            const float step = 1f / 60f;
            while (remaining > 0.00001f && State == PixelSessionState.Running)
            {
                float dt = Mathf.Min(step, remaining);
                Tick(dt, input);
                remaining -= dt;
                simulated += dt;
            }
            return simulated;
        }

        private void Tick(float dt, float input)
        {
            if (State != PixelSessionState.Running || Plan == null || cart == null) return;

            float oldDistance = cart.DistanceMeters;
            float predictedDistance = Mathf.Min(Plan.LengthMeters, oldDistance +
                Mathf.Max(5f, cart.SpeedMetersPerSecond) * dt);
            RouteSurface routeSurface = SurfaceUnderCart(oldDistance, cart.LateralOffset);
            cart.Step(dt, input, Plan.CenterX(oldDistance), Plan.CenterX(predictedDistance),
                Plan.Width(oldDistance), ToPixelSurface(routeSurface));
            elapsed += dt;
            ApplyRouteEvents(oldDistance, cart.DistanceMeters);
            ApplyBranchEffects(dt);

            if (cart.DistanceMeters >= Plan.LengthMeters)
            {
                State = cart.Condition >= minimumDeliveryCondition ? PixelSessionState.Delivered : PixelSessionState.Failed;
                if (State == PixelSessionState.Delivered) audioFeedback.PlayFinish();
            }
            else if (elapsed >= Plan.DeadlineSeconds || cart.Condition <= 0f)
            {
                State = PixelSessionState.Failed;
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (manualStepMode) return;
            if (!hasFocus && State == PixelSessionState.Running)
            {
                focusPause = true;
                Pause();
            }
            else if (hasFocus) focusPause = false;
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (manualStepMode) return;
            if (isPaused && State == PixelSessionState.Running)
            {
                focusPause = true;
                Pause();
            }
            else if (!isPaused) focusPause = false;
        }

        public void BeginRun(int seed, bool startImmediately = true)
        {
            Plan = RouteGrammar.Generate(seed);
            triggeredBeats.Clear();
            riskyBranches.Clear();
            branchScrapeTimer = 0f;
            elapsed = 0f;
            focusPause = false;
            if (cart == null) cart = GetComponent<PixelRunController>();
            cart.ResetRun();
            State = startImmediately ? PixelSessionState.Running : PixelSessionState.Ready;
        }

        public void StartRun()
        {
            if (State == PixelSessionState.Ready) State = PixelSessionState.Running;
        }

        public void Pause()
        {
            if (State == PixelSessionState.Running) State = PixelSessionState.Paused;
        }

        public void Resume()
        {
            if (State == PixelSessionState.Paused && !focusPause) State = PixelSessionState.Running;
        }

        public void RetrySameSeed() => BeginRun(Seed);
        public void NewSeed() => BeginRun(unchecked(Seed * 1664525 + 1013904223));
        public void ReturnToMenu() => BeginRun(Seed, false);
        public void ToggleMute()
        {
            if (audioFeedback != null) audioFeedback.ToggleMute();
        }

        private RouteSurface SurfaceUnderCart(float distance, float offset)
        {
            for (int i = 0; i < Plan.Beats.Count; i++)
            {
                RouteBeat beat = Plan.Beats[i];
                if (distance < beat.Start || distance > beat.End ||
                    Mathf.Abs(offset - beat.Offset) > beat.Width * 0.5f) continue;
                if (beat.Kind == RouteBeatKind.Slick) return RouteSurface.SlickStone;
                if (beat.Kind == RouteBeatKind.Conveyor) return RouteSurface.Conveyor;
                if (beat.Kind == RouteBeatKind.Ramp) return RouteSurface.Wood;
            }
            return RouteSurface.Stone;
        }

        private static PixelSurface ToPixelSurface(RouteSurface surface)
        {
            switch (surface)
            {
                case RouteSurface.Wood: return PixelSurface.Wood;
                case RouteSurface.SlickStone: return PixelSurface.Slick;
                case RouteSurface.Conveyor: return PixelSurface.Conveyor;
                default: return PixelSurface.Road;
            }
        }

        private void ApplyRouteEvents(float from, float to)
        {
            for (int i = 0; i < Plan.Beats.Count; i++)
            {
                RouteBeat beat = Plan.Beats[i];
                if (triggeredBeats.Contains(beat.Id) || beat.Start > to || beat.End < from) continue;
                if (beat.Kind != RouteBeatKind.Bump && beat.Kind != RouteBeatKind.Ramp &&
                    beat.Kind != RouteBeatKind.Conveyor && beat.Kind != RouteBeatKind.Scrape) continue;
                if (Mathf.Abs(cart.LateralOffset - beat.Offset) > beat.Width * 0.5f) continue;
                triggeredBeats.Add(beat.Id);
                switch (beat.Kind)
                {
                    case RouteBeatKind.Bump:
                        cart.Bump(beat.Severity);
                        audioFeedback.PlayBump();
                        break;
                    case RouteBeatKind.Ramp:
                        cart.Ramp(beat.Severity);
                        audioFeedback.PlayRamp();
                        break;
                    case RouteBeatKind.Conveyor:
                        cart.Conveyor(beat.Severity);
                        break;
                    case RouteBeatKind.Scrape:
                        cart.Scrape(beat.Severity);
                        audioFeedback.PlayScrape();
                        break;
                }
            }
        }

        private void ApplyBranchEffects(float dt)
        {
            RouteBranch branch = Plan.BranchAt(cart.DistanceMeters);
            if (branch == null || cart.DistanceMeters < branch.SplitEnd ||
                cart.DistanceMeters > branch.MergeStart)
            {
                branchScrapeTimer = 0f;
                return;
            }
            if (branch.IsRisky(cart.DistanceMeters, cart.LateralOffset))
            {
                riskyBranches.Add(branch.Id);
                cart.Conveyor(0.65f);
                branchScrapeTimer = 0f;
            }
            else if (branch.IsSafe(cart.DistanceMeters, cart.LateralOffset)) branchScrapeTimer = 0f;
            else
            {
                // The joining shoulder is forgiving, but holding the gap has a cost.
                branchScrapeTimer += dt;
                if (branchScrapeTimer > 1.1f)
                {
                    cart.Scrape(0.35f);
                    audioFeedback.PlayScrape();
                    branchScrapeTimer = 0f;
                }
            }
        }
    }
}
