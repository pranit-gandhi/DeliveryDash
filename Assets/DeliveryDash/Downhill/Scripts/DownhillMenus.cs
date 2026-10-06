using UnityEngine;

namespace DeliveryDash.Downhill
{
    // Painted shop lettering over the town: the scene remains the menu background.
    public static class DownhillMenus
    {
        static readonly Color Cream = new Color(1f, .94f, .78f);
        static readonly Color Ink = new Color(.19f, .15f, .10f, .85f);
        static readonly Color Tomato = new Color(.88f, .30f, .18f);
        static GUIStyle brand, heading, copy, choice, small, number;
        static string lastState;
        static float entered;

        static void Styles()
        {
            if (brand != null) return;
            var regular = Resources.Load<Font>("Fonts/Simonetta-Regular");
            var italic = Resources.Load<Font>("Fonts/Simonetta-Italic");
            var black = Resources.Load<Font>("Fonts/Simonetta-Black");
            var blackItalic = Resources.Load<Font>("Fonts/Simonetta-BlackItalic");
            brand = Style(86, blackItalic);
            heading = Style(54, blackItalic);
            copy = Style(21, regular);
            choice = Style(34, black);
            small = Style(18, italic);
            number = Style(29, regular);
        }

        static GUIStyle Style(int size, Font font)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = size,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(0, 0, 0, 0)
            };
            style.normal.textColor = Cream;
            return style;
        }

        static void Fill(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        static void Letter(Rect rect, string value, GUIStyle style, Color? color = null)
        {
            // A restrained painted edge keeps letters readable on both sky and roofs.
            Color original = style.normal.textColor;
            TextColor(style,Ink);
            GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), value, style);
            TextColor(style,color ?? Cream);
            GUI.Label(rect, value, style);
            TextColor(style,original);
        }

        static void TextColor(GUIStyle style,Color color)
        {
            style.normal.textColor=style.hover.textColor=style.active.textColor=style.focused.textColor=color;
        }

        static void Rule(float x, float y, float width)
        {
            Fill(new Rect(x, y + 1, width, 1), Ink);
            Fill(new Rect(x, y, width, 1), Cream);
            Fill(new Rect(x + width / 2 - 3, y - 2, 6, 5), Cream);
        }

        static bool Action(float x, float y, string label, bool primary = false)
        {
            var rect = new Rect(x, y, 250, 47);
            bool hover = rect.Contains(Event.current.mousePosition);
            Letter(rect, label, primary ? choice : copy, hover ? Cream : primary ? Cream : new Color(.98f, .89f, .69f));
            if (primary || hover)
            {
                float width = (primary ? choice : copy).CalcSize(new GUIContent(label)).x;
                Fill(new Rect(x, y + 39, width, primary ? 2 : 1), hover ? Cream : Tomato);
            }
            if (hover) Letter(new Rect(x - 21, y + 3, 18, 35), "\u203a", copy, Tomato);
            // Enter belongs to the session; Space belongs to the cart. Menu
            // controls receive pointer input without retaining keyboard focus.
            int id=GUIUtility.GetControlID(FocusType.Passive,rect);
            var input=Event.current;
            if(input.type==EventType.MouseDown&&input.button==0&&hover)
            {GUIUtility.hotControl=id;input.Use();}
            if(input.type==EventType.MouseUp&&input.button==0&&GUIUtility.hotControl==id)
            {GUIUtility.hotControl=0;input.Use();return hover;}
            return false;
        }

        public static void Draw(DownhillSession session)
        {
            Styles();
            if (lastState != session.State) { lastState = session.State; entered = Time.unscaledTime; }
            if (session.State == "Running") { Hud(session); return; }
            if (session.State == "Crashing" || session.State == "Delivering") return;

            float ease = Mathf.SmoothStep(0, 1, Mathf.Clamp01((Time.unscaledTime - entered) / .24f));
            float x = 67 - (1 - ease) * 12;
            bool ready = session.State == "Ready", paused = session.State == "Paused";
            if (ready)
            {
                Letter(new Rect(x + 4, 62, 330, 30), "PIZZERIA  \u00b7  DELIVERY", small);
                var matrix = GUI.matrix;
                GUIUtility.RotateAroundPivot(-4, new Vector2(x + 120, 175));
                Letter(new Rect(x, 104, 442, 112), "Delivery", brand);
                Letter(new Rect(x + 65, 182, 345, 112), "Dash", brand);
                GUI.matrix = matrix;
                Rule(x + 7, 307, 238);
                if (Action(x + 11, 332, "Go", true)) session.Go();
                if (Action(x + 11, 388, "New road")) session.NewRoad();
                Letter(new Rect(x + 10, 512, 315, 28), "A  D   Steer     Space   Hop", small);
            }
            else
            {
                float y = paused ? 213 : 157;
                Letter(new Rect(x, y, 415, 77), paused ? "Paused" : session.State == "Delivered" ? "Delivered!" : "Game over", heading);
                Rule(x + 5, y + 90, 237);
                if (paused)
                    Letter(new Rect(x + 7, y + 103, 285, 38), session.Elapsed.ToString("0.0") + " s", number);
                else
                {
                    Letter(new Rect(x + 7, y + 103, 300, 38), "Score   " + session.Score, number);
                    Letter(new Rect(x + 7, y + 145, 330, 28), session.Elapsed.ToString("0.0") + " s", small);
                }
                float row = y + (paused ? 160 : 194);
                if (Action(x + 7, row, paused ? "Resume" : "Retry", true))
                { if (paused) session.Resume(); else session.Go(); }
                if (Action(x + 7, row + 58, "New road")) session.NewRoad();
            }
            GUI.color = Color.white;
        }

        static void Hud(DownhillSession session)
        {
            var cart = session.Cart;
            Letter(new Rect(36, 23, 175, 40), session.Elapsed.ToString("0.0") + " s", number);
            Fill(new Rect(487, 30, 306, 3), new Color(.20f, .16f, .10f, .40f));
            Fill(new Rect(487, 30, 306 * cart.Progress, 3), Cream);
            Letter(new Rect(35, 659, 180, 27), cart.HopCooldown > 0 ? "Hop  " + cart.HopCooldown.ToString("0.0") : "Space   Hop", small);
            Fill(new Rect(36, 692, 97, 2), new Color(.2f, .16f, .1f, .4f));
            Fill(new Rect(36, 692, 97 * Mathf.Clamp01(1 - cart.HopCooldown / 3.2f), 2), Cream);
        }
    }
}
