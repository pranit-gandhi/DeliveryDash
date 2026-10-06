using UnityEngine;

namespace DeliveryDash.Pixel
{
    [RequireComponent(typeof(PixelRunSession))]
    public sealed class PixelMenuView : MonoBehaviour
    {
        private static readonly Color Paper = new Color(.98f, .91f, .74f);
        private static readonly Color Ink = new Color(.20f, .24f, .23f);
        private static readonly Color Red = new Color(.70f, .20f, .13f);
        private static readonly Color Faint = new Color(.56f, .49f, .36f);
        private PixelRunSession run;
        private PixelSessionState previousState;
        private int selected;
        private GUIStyle title, body, small, action, number;

        private void Awake() => run = GetComponent<PixelRunSession>();

        private void Update()
        {
            if (run == null || run.ManualStepMode) return;
            if (run.State != previousState)
            {
                previousState = run.State;
                selected = 0;
            }
            if (run.State == PixelSessionState.Running) return;
            int count = run.State == PixelSessionState.Ready ? 2 : 4;
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.Tab))
                selected = (selected + 1) % count;
            if (Input.GetKeyDown(KeyCode.UpArrow)) selected = (selected + count - 1) % count;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Activate(selected);
        }

        private void Activate(int choice)
        {
            if (run.State == PixelSessionState.Ready)
            {
                if (choice == 0) run.StartRun();
                else run.ToggleMute();
            }
            else if (run.State == PixelSessionState.Paused)
            {
                if (choice == 0) run.Resume();
                else if (choice == 1) run.RetrySameSeed();
                else if (choice == 2) run.ToggleMute();
                else run.ReturnToMenu();
            }
            else
            {
                if (choice == 0) run.NewSeed();
                else if (choice == 1) run.RetrySameSeed();
                else if (choice == 2) run.ToggleMute();
                else run.ReturnToMenu();
            }
        }

        private void OnGUI()
        {
            if (run == null || run.Plan == null) return;
            EnsureStyles();
            Matrix4x4 savedMatrix = GUI.matrix;
            Color savedColor = GUI.color;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280f * scale) * .5f,
                (Screen.height - 720f * scale) * .5f, 0f), Quaternion.identity,
                new Vector3(scale, scale, 1f));
            GUI.color = Color.white;
            if (run.State == PixelSessionState.Running) DrawHud();
            else DrawMenu();
            GUI.matrix = savedMatrix;
            GUI.color = savedColor;
        }

        private void DrawHud()
        {
            // Keep the road preview clear. Both readouts live at the outer corners.
            Ticket(new Rect(28, 24, 196, 70), false);
            Label(new Rect(44, 32, 94, 18), "TIME LEFT", small, Faint);
            Label(new Rect(44, 50, 94, 34), Clock(run.SecondsRemaining), number, Ink);
            if (PlainButton(new Rect(143, 39, 66, 38), "Pause")) run.Pause();
            Ticket(new Rect(1024, 24, 228, 70), false);
            Label(new Rect(1040, 32, 110, 18), "PIZZA", small, Faint);
            for (int i = 0; i < 5; i++)
            {
                Color fill = run.Condition > i * 20f ? Red : new Color(.78f, .73f, .62f);
                Fill(new Rect(1040 + i * 25, 59, 19, 15), fill);
            }
            Label(new Rect(1172, 32, 65, 18), "ROUTE", small, Faint);
            Label(new Rect(1172, 52, 65, 27), Mathf.CeilToInt(run.Progress * 100f) + "%", body, Ink);
            if (run.SecondsElapsed < 6f)
            {
                Ticket(new Rect(483, 640, 314, 52), false);
                Label(new Rect(503, 649, 274, 32), "A   D     steer", action, Ink);
            }
        }

        private void DrawMenu()
        {
            bool ready = run.State == PixelSessionState.Ready;
            bool paused = run.State == PixelSessionState.Paused;
            bool delivered = run.State == PixelSessionState.Delivered;
            Fill(new Rect(0, 0, 1280, 720), new Color(.13f, .19f, .20f, ready ? .12f : .38f));
            Rect ticket = new Rect(62, 62, 394, 596);
            Ticket(ticket, true);
            Label(new Rect(94, 94, 330, 23), "ONE PIZZA. ALL DOWNHILL.", small, Red);
            Label(new Rect(90, 131, 336, 130), ready ? "DELIVERY\nDASH" : paused ? "TAKE A\nBREATHER" :
                delivered ? "PIZZA\nDELIVERED!" : "ONE MORE\nTRY?", title, Ink);
            Rule(94, 279, 330);
            if (ready)
            {
                Label(new Rect(94, 301, 330, 29), "Get it there in one piece.", body, Ink);
                Key(94, 353, "A");
                Key(148, 353, "D");
                Label(new Rect(216, 353, 200, 38), "Steer", body, Ink);
                Label(new Rect(94, 406, 320, 26), "Arrow keys work too.", small, Faint);
                Choice(0, new Rect(94, 457, 330, 58), "LET'S GO", true);
                Choice(1, new Rect(94, 531, 330, 40), SoundLabel, false);
            }
            else
            {
                string detail = paused ? "Your pizza can wait." : delivered ?
                    Clock(run.SecondsElapsed) + "     " + Mathf.RoundToInt(run.Condition) + "% intact" :
                    "The next one is on us.";
                Label(new Rect(94, 300, 330, 30), detail, body, Ink);
                Choice(0, new Rect(94, 362, 330, 58), paused ? "KEEP GOING" : "NEW DELIVERY", true);
                Choice(1, new Rect(94, 436, 330, 40), "Try this road again", false);
                Choice(2, new Rect(94, 486, 330, 40), SoundLabel, false);
                Choice(3, new Rect(94, 536, 330, 40), "Back to menu", false);
            }
            Label(new Rect(94, 606, 330, 23), "ENTER TO CHOOSE", small, Faint);
        }

        private string SoundLabel => run.IsMuted ? "Sound off" : "Sound on";

        private void Choice(int index, Rect bounds, string text, bool primary)
        {
            if (Event.current.type == EventType.MouseMove && bounds.Contains(Event.current.mousePosition))
                selected = index;
            bool highlighted = selected == index || bounds.Contains(Event.current.mousePosition);
            if (primary)
            {
                Fill(new Rect(bounds.x + 3, bounds.y + 4, bounds.width, bounds.height), new Color(.49f, .36f, .23f));
                Fill(bounds, highlighted ? Red : new Color(.59f, .20f, .14f));
                Label(new Rect(bounds.x + 18, bounds.y + 10, bounds.width - 36, bounds.height - 16), text, action, Paper);
            }
            else
            {
                if (highlighted) Fill(new Rect(bounds.x, bounds.y + 8, 4, 24), Red);
                Label(new Rect(bounds.x + 18, bounds.y, bounds.width - 36, bounds.height), text, body, highlighted ? Red : Ink);
            }
            if (GUI.Button(bounds, GUIContent.none, GUIStyle.none)) { selected = index; Activate(index); }
        }

        private bool PlainButton(Rect bounds, string text)
        {
            bool hover = bounds.Contains(Event.current.mousePosition);
            Label(bounds, text, small, hover ? Red : Ink);
            return GUI.Button(bounds, GUIContent.none, GUIStyle.none);
        }

        private static string Clock(float seconds)
        {
            int value = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            return (value / 60) + ":" + (value % 60).ToString("00");
        }

        private void Key(float x, float y, string letter)
        {
            Fill(new Rect(x, y + 3, 42, 38), Faint);
            Fill(new Rect(x, y, 42, 35), new Color(1f, .97f, .86f));
            Label(new Rect(x + 12, y + 3, 30, 31), letter, action, Ink);
        }

        private static void Ticket(Rect bounds, bool checker)
        {
            Fill(new Rect(bounds.x + 6, bounds.y + 8, bounds.width, bounds.height), new Color(.12f, .16f, .16f, .35f));
            Fill(bounds, Paper);
            if (!checker) return;
            for (int i = 0; i < 33; i++)
                for (int j = 0; j < 2; j++)
                    if ((i + j) % 2 == 0)
                        Fill(new Rect(bounds.x + i * 12, bounds.y + j * 8, Mathf.Min(12, bounds.width - i * 12), 8), Red);
            for (int i = 0; i < 32; i++)
                Fill(new Rect(bounds.x + 3 + i * 12, bounds.yMax, 6, 4), Paper);
        }

        private static void Rule(float x, float y, float width)
        {
            for (float i = 0; i < width; i += 11f) Fill(new Rect(x + i, y, 6f, 1f), Faint);
        }

        private static void Fill(Rect bounds, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(bounds, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private static void Label(Rect bounds, string text, GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            GUI.Label(bounds, text, style);
        }

        private void EnsureStyles()
        {
            if (title != null) return;
            title = Style(43, FontStyle.Bold);
            body = Style(20, FontStyle.Normal);
            small = Style(13, FontStyle.Bold);
            action = Style(23, FontStyle.Bold);
            number = Style(28, FontStyle.Bold);
        }

        private static GUIStyle Style(int size, FontStyle weight) => new GUIStyle(GUI.skin.label)
        {
            fontSize = size,
            fontStyle = weight,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(0, 0, 0, 0),
            wordWrap = false
        };
    }
}
