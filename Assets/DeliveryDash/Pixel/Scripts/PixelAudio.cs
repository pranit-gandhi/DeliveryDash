using UnityEngine;

namespace DeliveryDash.Pixel
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class PixelAudio : MonoBehaviour
    {
        private AudioSource source;
        private AudioClip bump;
        private AudioClip scrape;
        private AudioClip ramp;
        private AudioClip finish;
        private bool muted;

        public bool Muted => muted;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0.32f;
            bump = CreateTone("Cart bump", 0.14f, 155f, 70f, 0.28f);
            scrape = CreateTone("Cart scrape", 0.23f, 115f, 48f, 0.18f);
            ramp = CreateTone("Cart ramp", 0.18f, 235f, 340f, 0.17f);
            finish = CreateTone("Delivery", 0.42f, 460f, 710f, 0.16f);
        }

        public void SetMuted(bool value)
        {
            muted = value;
            if (source != null) source.mute = value;
        }

        public void ToggleMute() => SetMuted(!muted);
        public void PlayBump() => Play(bump);
        public void PlayScrape() => Play(scrape);
        public void PlayRamp() => Play(ramp);
        public void PlayFinish() => Play(finish);

        private void Play(AudioClip clip)
        {
            if (!muted && source != null && clip != null) source.PlayOneShot(clip);
        }

        private static AudioClip CreateTone(string clipName, float seconds, float startFrequency,
            float endFrequency, float amplitude)
        {
            const int sampleRate = 22050;
            int count = Mathf.CeilToInt(seconds * sampleRate);
            float[] samples = new float[count];
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                float frequency = Mathf.Lerp(startFrequency, endFrequency, t);
                phase += frequency * Mathf.PI * 2f / sampleRate;
                float envelope = (1f - t) * (1f - t) * Mathf.Min(1f, t * 35f);
                float body = Mathf.Sin(phase) + 0.18f * Mathf.Sin(phase * 2.03f);
                samples[i] = Mathf.Clamp(body * envelope * amplitude, -1f, 1f);
            }
            AudioClip clip = AudioClip.Create(clipName, count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
