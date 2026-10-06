using UnityEngine;

namespace DeliveryDash.Downhill
{
    // A bounded, repeatable crash beat. Driving remains fixed-step; this is presentation.
    [DefaultExecutionOrder(100)]
    public sealed class DownhillCrash : MonoBehaviour
    {
        public float Elapsed { get; private set; }
        public bool Active { get; private set; }
        public float SplatAmount => Active ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.82f, 1.12f, Elapsed)) : 0f;
        DownhillVisuals visuals;
        DownhillChaseCamera chase;
        Transform pizzaParent, cap, capParent;
        Vector3 pizzaRest, pizzaScale, capRest, riderRest, frameRest;
        Quaternion pizzaRotation, capRotation, riderRotation, frameRotation;
        Vector3 pizzaStart, capStart;
        Quaternion pizzaStartRotation, capStartRotation;
        Texture2D mustard;
        AudioSource sound;
        AudioClip thud;

        void Awake()
        {
            visuals = GetComponent<DownhillVisuals>();
            if (visuals == null) { enabled = false; return; }
            pizzaParent = visuals.Pizza.parent;
            pizzaRest = visuals.Pizza.localPosition;
            pizzaRotation = visuals.Pizza.localRotation;
            pizzaScale = visuals.Pizza.localScale;
            riderRest = visuals.Rider.localPosition;
            riderRotation = visuals.Rider.localRotation;
            frameRest = visuals.Frame.localPosition;
            frameRotation = visuals.Frame.localRotation;
            foreach (Transform child in visuals.Rider.GetComponentsInChildren<Transform>())
                if (child.name == "Backward red baseball cap") cap = child;
            if (cap != null)
            {
                capParent = cap.parent;
                capRest = cap.localPosition;
                capRotation = cap.localRotation;
            }
            if (Camera.main != null) chase = Camera.main.GetComponent<DownhillChaseCamera>();
            BuildMustard();
            sound = gameObject.AddComponent<AudioSource>();
            sound.playOnAwake = false;
            sound.volume = .28f;
            // Locally synthesized impact, no external audio or license dependency.
            const int rate = 22050;
            var samples = new float[rate / 4];
            uint noise = 1847;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                noise = noise * 1664525u + 1013904223u;
                float grit = ((noise >> 8) / 16777215f * 2f - 1f) * .22f;
                samples[i] = (Mathf.Sin(t * (135f - 180f * t) * Mathf.PI * 2f) + grit) * Mathf.Exp(-t * 22f) * .7f;
            }
            thud = AudioClip.Create("Cart edge impact", samples.Length, 1, rate, false);
            thud.SetData(samples, 0);
        }

        public void Begin()
        {
            if (Active || visuals == null) return;
            Active = true;
            Elapsed = 0f;
            pizzaStart = visuals.Pizza.position;
            pizzaStartRotation = visuals.Pizza.rotation;
            visuals.Pizza.SetParent(null, true);
            if (cap != null)
            {
                capStart = cap.position;
                capStartRotation = cap.rotation;
                cap.SetParent(null, true);
            }
            if (chase != null) chase.enabled = false;
            sound.PlayOneShot(thud);
        }

        public void ResetEffects()
        {
            Active = false;
            Elapsed = 0f;
            if (visuals == null) return;
            visuals.Pizza.SetParent(pizzaParent, false);
            visuals.Pizza.localPosition = pizzaRest;
            visuals.Pizza.localRotation = pizzaRotation;
            visuals.Pizza.localScale = pizzaScale;
            visuals.Rider.localPosition = riderRest;
            visuals.Rider.localRotation = riderRotation;
            visuals.Frame.localPosition = frameRest;
            visuals.Frame.localRotation = frameRotation;
            if (cap != null)
            {
                cap.SetParent(capParent, false);
                cap.localPosition = capRest;
                cap.localRotation = capRotation;
            }
            if (chase != null) { chase.enabled = true; chase.Snap(); }
        }

        void LateUpdate()
        {
            if (!Active) return;
            Elapsed += Time.deltaTime;
            float fall = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Elapsed / .64f));
            visuals.Frame.localRotation = frameRotation * Quaternion.Euler(8f * fall, 0f, -GetComponent<DownhillCart>().CrashSide * 5f * fall);
            // Fall over the front rim instead of rotating inside the seat.
            visuals.Rider.localPosition = riderRest + new Vector3(0f, Mathf.Sin(fall * Mathf.PI) * .42f - fall * .16f, fall * 1.1f);
            visuals.Rider.localRotation = riderRotation * Quaternion.Euler(86f * fall, 0f, 0f);
            var camera = Camera.main;
            if (camera != null)
            {
                float flight = Mathf.Clamp01(Elapsed / .88f);
                Vector3 end = camera.transform.position + camera.transform.forward * .7f - camera.transform.up * .04f;
                Vector3 middle = Vector3.Lerp(pizzaStart, end, .45f) + Vector3.up * 1.55f;
                visuals.Pizza.position = (1f - flight) * (1f - flight) * pizzaStart + 2f * (1f - flight) * flight * middle + flight * flight * end;
                visuals.Pizza.rotation = Quaternion.Slerp(pizzaStartRotation,
                    camera.transform.rotation * Quaternion.Euler(72f, 12f, -18f), flight);
            }
            if (cap != null)
            {
                float t = Mathf.Min(Elapsed, 1.1f);
                cap.position = capStart + transform.forward * t * 1.6f + Vector3.up * (t * 3.8f - 4.9f * t * t);
                cap.rotation = capStartRotation * Quaternion.Euler(t * 280f, t * 90f, t * 70f);
            }
        }

        public void DrawSplat()
        {
            if (SplatAmount <= 0f || mustard == null) return;
            var previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, .97f);
            float width = Mathf.Lerp(180f, 1850f, SplatAmount);
            float height = width * 9f / 16f;
            GUI.DrawTexture(new Rect(640f - width * .5f, 360f - height * .5f, width, height), mustard);
            GUI.color = previous;
        }

        void BuildMustard()
        {
            mustard = new Texture2D(320, 180, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[320 * 180];
            for (int y = 0; y < 180; y++) for (int x = 0; x < 320; x++)
            {
                float px = (x - 160f) / 160f, py = (y - 90f) / 90f;
                float angle = Mathf.Atan2(py, px);
                float edge = .92f + .12f * Mathf.Sin(angle * 7f) + .055f * Mathf.Cos(angle * 13f);
                float radius = Mathf.Sqrt(px * px + py * py);
                bool covered = radius < edge;
                // A few deliberate drips and droplets keep the wipe readable at the pixel grid.
                covered |= Mathf.Pow((px + .63f) / .07f, 2f) + Mathf.Pow((py - .72f) / .19f, 2f) < 1f;
                int shade = Mathf.RoundToInt(12f * Mathf.Sin(x * .12f + y * .16f));
                pixels[y * 320 + x] = covered ? new Color32((byte)(229 + shade / 3), (byte)(175 + shade / 2), 31, 255) : new Color32(0, 0, 0, 0);
            }
            mustard.SetPixels32(pixels);
            mustard.Apply();
        }

        void OnDestroy()
        {
            if (Active && visuals != null && visuals.Pizza != null && visuals.Pizza.parent == null) Destroy(visuals.Pizza.gameObject);
            if (Active && cap != null && cap.parent == null) Destroy(cap.gameObject);
            if (mustard != null) Destroy(mustard);
            if (thud != null) Destroy(thud);
        }
    }
}
