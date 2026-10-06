using UnityEngine;

namespace DeliveryDash.Downhill
{
    public sealed class DownhillSession : MonoBehaviour
    {
        public DownhillCart Cart;
        public int Seed = 2647;
        public bool UseGenerated;
        public CourseGraph Course { get; private set; }
        public string State { get; private set; } = "Ready";
        public float Elapsed { get; private set; }
        public int Score { get; private set; }
        public float Deadline => UseGenerated ? 180f : 55f;
        bool muted;
        string resumeState;
        DownhillCrash crash;
        DownhillDelivery delivery;
        public bool Muted => muted;
        public void ToggleMute() { muted = !muted; AudioListener.pause = muted || State == "Paused"; }
        void Start()
        {
            // A local browser test can reproduce a road without changing the build.
            if (!string.IsNullOrEmpty(Application.absoluteURL))
            {
                var uri = new System.Uri(Application.absoluteURL);
                foreach (string pair in uri.Query.TrimStart('?').Split('&'))
                {
                    string[] value = pair.Split('=');
                    if (value.Length == 2 && value[0] == "seed" && int.TryParse(value[1], out int seed))
                    { Seed = seed; UseGenerated = true; }
                }
            }
            crash = Cart.GetComponent<DownhillCrash>();
            delivery = Cart.GetComponent<DownhillDelivery>();
            Course = UseGenerated ? CourseGraph.CreateGenerated(Seed) : CourseGraph.CreateFeel(Seed);
            RebuildWorld();
            Cart.Configure(Course);
        }
        public void Go()
        {
            if (Course == null)
            {
                Course = UseGenerated ? CourseGraph.CreateGenerated(Seed) : CourseGraph.CreateFeel(Seed);
                RebuildWorld();
            }
            Time.timeScale = 1;
            Elapsed = 0;
            Score = 0;
            if (delivery != null) delivery.ResetDelivery();
            if (crash != null) crash.ResetEffects();
            Cart.Configure(Course);
            Cart.Begin();
            State = "Running";
            AudioListener.pause = muted;
        }
        public void NewRoad()
        {
            if (State == "Running" || State == "Crashing" || State == "Delivering") return;
            Seed = unchecked(Seed * 1664525 + 1013904223) & int.MaxValue;
            Course = CourseGraph.CreateGenerated(Seed);
            UseGenerated = true;
            RebuildWorld();
            Go();
        }
        void RebuildWorld()
        {
            // Saved preview meshes can become stale after a generator edit. Build the
            // visible road from the exact graph queried by every wheel and collision.
            ReplaceWorld("Downhill course");
            ReplaceWorld("Terraced market street");
            ReplaceWorld("Town backdrop");
            DownhillWorldResources.Track(CourseMeshBuilder.Build(Course, null));
            DownhillWorldResources.Track(DownhillTown.Build(Course, Seed));
            DownhillWorldResources.Track(DownhillBackdrop.Build(Course));
            DownhillScenery.Apply(Course);
        }
        static void ReplaceWorld(string name)
        {
            var world = GameObject.Find(name);
            if (world != null) { world.SetActive(false); Destroy(world); }
        }
        void Update()
        {
            if (State == "Running" && Course == null) { Cart.Stop(); State = "Ready"; return; }
            if (Input.GetKeyDown(KeyCode.Return))
            {
                if (State == "Paused") Resume();
                else if (State == "Ready" || State == "Game over" || State == "Delivered") Go();
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (State == "Running" || State == "Crashing" || State == "Delivering") Pause();
                else if (State == "Paused") Resume();
            }
            if (State == "Crashing")
            {
                if (crash == null || crash.Elapsed >= 1.42f) State = "Game over";
                return;
            }
            if (State == "Delivering")
            {
                if (delivery == null || delivery.Elapsed >= 1.1f) State = "Delivered";
                return;
            }
            if (State != "Running") return;
            Elapsed += Time.deltaTime;
            Score = Mathf.FloorToInt(Cart.Distance * 10f) + Cart.Landings * 100;
            if (Cart.Crashed)
            {
                State = "Crashing";
                if (crash != null) crash.Begin();
                return;
            }
            if (Cart.Condition <= 0f || Elapsed > Deadline)
            { State = "Game over"; Cart.Stop(); return; }
            if (Cart.Distance >= Course.Length - 2f)
            {
                Score += Mathf.FloorToInt(Cart.Condition * 25f) + Mathf.CeilToInt((Deadline - Elapsed) * 10f);
                State = "Delivering";
                Cart.Stop();
                if (delivery != null)
                {
                    Cart.GetComponent<DownhillVisuals>().enabled = false;
                    delivery.Begin(Course);
                }
            }
        }
        public void Pause()
        {
            if (State != "Running" && State != "Crashing" && State != "Delivering") return;
            resumeState = State;
            State = "Paused";
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }
        public void Resume()
        {
            if (State != "Paused") return;
            State = resumeState;
            Time.timeScale = 1f;
            AudioListener.pause = muted;
        }
        void OnApplicationFocus(bool focus) { if (!focus) Pause(); }
        void OnApplicationPause(bool pause) { if (pause) Pause(); }
        void OnDestroy() { Time.timeScale = 1; AudioListener.pause = false; }
        void OnGUI()
        {
            if (Cart == null) return;
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / 1280f, Screen.height / 720f, 1));
            if (crash != null) crash.DrawSplat();
            DownhillMenus.Draw(this);
            GUI.matrix = previous;
        }
    }
}
