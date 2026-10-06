using UnityEngine;

namespace DeliveryDash.Downhill
{
    // Original synthesized cart sounds. No external clips, voice or music.
    public sealed class DownhillAudio : MonoBehaviour
    {
        DownhillCart cart;
        DownhillSession session;
        AudioSource rolling, impacts;
        AudioClip roll, landing, hit, delivery, splash, hop;
        int landings, obstacleHits, reset, spins, hops;
        string lastState;
        void Start()
        {
            cart = GetComponent<DownhillCart>();
            session = Object.FindFirstObjectByType<DownhillSession>();
            rolling = gameObject.AddComponent<AudioSource>();
            impacts = gameObject.AddComponent<AudioSource>();
            rolling.playOnAwake = impacts.playOnAwake = false;
            rolling.loop = true;
            roll = Make("Rolling wheels", .45f, 0);
            landing = Make("Suspension landing", .22f, 1);
            hit = Make("Market collision", .19f, 2);
            delivery = Make("Delivery bell", .55f, 3);
            splash=Make("Wet wheels",.32f,4);
            hop=Make("Suspension spring",.18f,5);
            rolling.clip = roll;
            impacts.volume = .23f;
        }
        void Update()
        {
            if (cart == null || AudioListener.pause) return;
            if (cart.ResetVersion != reset) { reset = cart.ResetVersion; landings = obstacleHits = spins = hops = 0; }
            rolling.volume = cart.Running && !cart.Airborne ? Mathf.Lerp(.018f, .075f, cart.Speed / 22f) : 0f;
            rolling.pitch = Mathf.Lerp(.7f, 1.6f, cart.Speed / 22f);
            if (cart.Running && !rolling.isPlaying) rolling.Play();
            if (!cart.Running && rolling.isPlaying) rolling.Stop();
            if (cart.Landings > landings) impacts.PlayOneShot(landing, Mathf.Clamp(.3f + cart.LandingImpact * .06f, .3f, 1f));
            if (cart.ObstacleHits > obstacleHits) impacts.PlayOneShot(hit);
            if(cart.Spins>spins)impacts.PlayOneShot(splash,.55f);
            if(cart.Hops>hops)impacts.PlayOneShot(hop,.45f);
            if (session != null && session.State == "Delivered" && lastState != "Delivered") impacts.PlayOneShot(delivery, .75f);
            landings = cart.Landings; obstacleHits = cart.ObstacleHits;spins=cart.Spins;hops=cart.Hops;
            if (session != null) lastState = session.State;
        }
        static AudioClip Make(string name, float duration, int kind)
        {
            const int rate = 22050;
            var data = new float[Mathf.CeilToInt(rate * duration)];
            uint noise = 3721;
            float filtered = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)rate;
                noise = noise * 1664525u + 1013904223u;
                float n = (noise >> 8) / 16777215f * 2f - 1f;
                filtered = Mathf.Lerp(filtered, n, kind == 0 ? .12f : .55f);
                float sample;
                if (kind == 0) sample = filtered * (.3f + .18f * Mathf.Sin(t * 160f));
                else if (kind == 3) sample = (Mathf.Sin(t * 660f * Mathf.PI * 2f) + Mathf.Sin(t * 990f * Mathf.PI * 2f) * .24f) * Mathf.Exp(-t * 8f) * .4f;
                else if(kind==4)sample=filtered*Mathf.Sin(t*34f)*Mathf.Exp(-t*9f)*.5f;
                else if(kind==5)sample=Mathf.Sin(t*(140f+220f*t)*Mathf.PI*2f)*Mathf.Exp(-t*22f)*.4f;
                else sample = (Mathf.Sin(t * (kind == 1 ? 85f : 170f) * Mathf.PI * 2f) * .6f + filtered * .4f) * Mathf.Exp(-t * 24f);
                data[i] = sample;
            }
            var clip = AudioClip.Create(name, data.Length, 1, rate, false); clip.SetData(data, 0); return clip;
        }
        void OnDestroy()
        { if (roll != null) Destroy(roll); if (landing != null) Destroy(landing); if (hit != null) Destroy(hit); if (delivery != null) Destroy(delivery);if(splash!=null)Destroy(splash);if(hop!=null)Destroy(hop); }
    }
}
