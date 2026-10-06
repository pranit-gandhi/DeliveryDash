using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace DeliveryDash.Downhill
{
    public sealed class DownhillBrowserTelemetry : MonoBehaviour
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void DeliveryDashObserve(string json);
        DownhillSession session;
        DownhillMusic music;
        float next;
        float frameSum, frameMaximum;
        int frameCount;
        void Start() { session = GetComponent<DownhillSession>();music=GetComponent<DownhillMusic>(); }
        void Update()
        {
            frameSum += Time.unscaledDeltaTime;
            frameMaximum = Mathf.Max(frameMaximum, Time.unscaledDeltaTime);
            frameCount++;
            if (session == null || session.Cart == null || Time.unscaledTime < next) return;
            next = Time.unscaledTime + .25f;
            var cart = session.Cart;
            DeliveryDashObserve(JsonUtility.ToJson(new Snapshot {
                seed = session.Seed, state = session.State, score = session.Score,
                elapsed = session.Elapsed, distance = cart.Distance, speed = cart.Speed,
                pizza = cart.Condition, airborne = cart.Airborne, contacts = cart.GroundContacts,
                launches = cart.Launches, landings = cart.Landings, obstacleHits = cart.ObstacleHits,
                hops = cart.Hops, hopCooldown = cart.HopCooldown, bodyRoll = cart.BodyRoll,
                spins=cart.Spins,spinning=cart.Spinning,spinProgress=cart.SpinProgress,lastObstacle=cart.LastObstacleId,
                muted = session.Muted,
                tip = cart.TipAmount, space = Input.GetKey(KeyCode.Space), audioPaused = AudioListener.pause,
                musicTrack = music!=null?music.Track:"", musicTime = music!=null?music.TrackTime:0,
                musicLength = music!=null?music.TrackLength:0, musicPlaying = music!=null&&music.Playing,
                crashReason = cart.CrashReason, steering = cart.Steer, slip = cart.Slip,
                averageFrameMs = frameSum / Mathf.Max(1, frameCount) * 1000f,
                maximumFrameMs = frameMaximum * 1000f
            }));
            frameSum = frameMaximum = 0f;
            frameCount = 0;
        }
        [System.Serializable] sealed class Snapshot
        {
            public int seed, score, contacts, launches, landings, obstacleHits, hops, spins;
            public string state, crashReason, musicTrack, lastObstacle;
            public float elapsed, distance, speed, pizza, steering, slip, hopCooldown, bodyRoll, tip, musicTime, musicLength,spinProgress;
            public float averageFrameMs, maximumFrameMs;
            public bool airborne, muted, space, audioPaused, musicPlaying,spinning;
        }
#endif
    }
}
