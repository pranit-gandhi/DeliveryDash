using System;
using UnityEngine;

namespace DeliveryDash
{
    // Rules separated from the scene so time, scoring, and failure can be tested directly.
    public sealed class DeliveryShiftRules
    {
        public const double DeliverySpacing = 288;
        public const float StartingTime = 60f;
        public const float DeliveryBonus = 15f;
        public const float CollisionPenalty = 3f;
        public float TimeLeft { get; private set; }
        public float Condition { get; private set; }
        public int Deliveries { get; private set; }
        public int Tips { get; private set; }
        public bool Finished => TimeLeft <= 0;
        public double NextCustomer => (Deliveries + 1) * DeliverySpacing;
        private float collisionCooldown;

        public DeliveryShiftRules() { Reset(); }
        public void Reset()
        {
            TimeLeft = StartingTime; Condition = 100; Deliveries = Tips = 0; collisionCooldown = 0;
        }

        // Returns true only when a scrape actually incurs a penalty.
        public bool Tick(float seconds, double distance, bool scraping)
        {
            if (Finished) return false;
            seconds = Mathf.Max(0, seconds);
            TimeLeft = Mathf.Max(0, TimeLeft - seconds);
            collisionCooldown = Mathf.Max(0, collisionCooldown - seconds);
            bool penalty = scraping && collisionCooldown <= 0 && !Finished;
            if (penalty)
            {
                TimeLeft = Mathf.Max(0, TimeLeft - CollisionPenalty);
                Condition = Mathf.Max(0, Condition - 20);
                collisionCooldown = 2f;
            }
            // Expiry wins ties: crossing a checkpoint after time runs out cannot revive a run.
            if (Finished) return penalty;
            while (distance >= NextCustomer)
            {
                Deliveries++;
                Tips += Mathf.RoundToInt(Condition / 10);
                Condition = 100;
                TimeLeft = Mathf.Min(90, TimeLeft + DeliveryBonus);
            }
            return penalty;
        }
    }

    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(EndlessRoadGenerator))]
    public sealed class DeliveryShift : MonoBehaviour
    {
        [SerializeField] private CartFeelController runner;
        private EndlessRoadGenerator road;
        private readonly DeliveryShiftRules rules = new DeliveryShiftRules();
        private float messageUntil;
        private string message = "Pass through the green customer gates to deliver.";
        private GUIStyle titleStyle, bodyStyle, largeStyle;
        public DeliveryShiftRules Rules => rules;

        public void Configure(CartFeelController cart) { runner = cart; }
        private void Awake() { road = GetComponent<EndlessRoadGenerator>(); }

        public void BeginRun()
        {
            rules.Reset();
            if (runner != null) runner.enabled = true;
            message = "Pass through the green customer gates to deliver.";
            messageUntil = Time.time + 7;
        }

        private void Update()
        {
            if (runner == null || road == null || rules.Finished) return;
            int before = rules.Deliveries;
            bool penalty = rules.Tick(Time.deltaTime, road.Distance, Time.time - runner.LastScrapeTime < .1f);
            if (penalty)
            {
                message = "Scrape! -3 seconds / pizza damaged";
                messageUntil = Time.time + 2;
            }
            if (rules.Deliveries > before)
            {
                message = "Delivered! +15 seconds / fresh pizza loaded";
                messageUntil = Time.time + 3;
            }
            if (rules.Finished) runner.enabled = false;
        }

        private void OnGUI()
        {
            if (road == null) return;
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
                bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 16 };
                largeStyle = new GUIStyle(titleStyle) { fontSize = 30, alignment = TextAnchor.MiddleCenter };
            }
            // Constant logical size keeps the HUD usable at smaller browser resolutions.
            Matrix4x4 oldMatrix = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 960f, Screen.height / 540f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float viewWidth = Screen.width / scale, viewHeight = Screen.height / scale;
            GUI.Box(new Rect(16, 16, 410, 139), GUIContent.none);
            GUI.Label(new Rect(30, 25, 390, 30), $"DELIVERY SHIFT   {Mathf.CeilToInt(rules.TimeLeft)}s", titleStyle);
            GUI.Label(new Rect(30, 61, 390, 24), $"Pizzas delivered: {rules.Deliveries}     Tips: ${rules.Tips}", bodyStyle);
            GUI.Label(new Rect(30, 88, 390, 24), $"Pizza: {rules.Condition:0}%     Next customer: {Math.Max(0, rules.NextCustomer - road.Distance):0} m", bodyStyle);
            GUI.Label(new Rect(30, 116, 390, 24), $"Distance: {road.Distance:0} m     Route: {road.Seed}", bodyStyle);
            GUI.Box(new Rect(16, viewHeight - 47, 440, 31), "A/D or arrows: steer    R: retry route    N: new route");
            if (!rules.Finished && Time.time < messageUntil)
            {
                GUI.Box(new Rect((viewWidth - 530) / 2, viewHeight - 91, 530, 33), GUIContent.none);
                GUI.Label(new Rect((viewWidth - 510) / 2, viewHeight - 86, 510, 26), message, bodyStyle);
            }
            if (rules.Finished)
            {
                Rect panel = new Rect((viewWidth - 450) / 2, (viewHeight - 220) / 2, 450, 220);
                GUI.Box(panel, GUIContent.none);
                GUI.Label(new Rect(panel.x, panel.y + 20, 450, 45), "SHIFT COMPLETE", largeStyle);
                GUI.Label(new Rect(panel.x + 45, panel.y + 82, 380, 30), $"{rules.Deliveries} pizzas delivered   /   ${rules.Tips} tips", titleStyle);
                GUI.Label(new Rect(panel.x + 45, panel.y + 119, 380, 28), $"Distance travelled: {road.Distance:0} m", bodyStyle);
                if (GUI.Button(new Rect(panel.x + 35, panel.y + 166, 180, 35), "Retry this route (R)")) road.Restart(road.Seed);
                if (GUI.Button(new Rect(panel.x + 235, panel.y + 166, 180, 35), "New route (N)")) road.Restart(unchecked(road.Seed * 1664525 + 1013904223));
            }
            GUI.matrix = oldMatrix;
        }
    }
}
