using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// Sound and vibration for the key moments (M2 step 4, C10): a line connecting, a touchdown, a danger warning and
    /// a collision. Sounds are made in code by <see cref="ToneSynth"/>; vibration goes through <see cref="Haptics"/>.
    /// Uses real time for its limits, so slow draw and 2x speed do not change how often it sounds.
    /// </summary>
    public sealed class FeedbackSystem : MonoBehaviour
    {
        /// <summary>At most one warning sound in this many real seconds, so close traffic does not beep constantly.</summary>
        const float WarningRepeatSeconds = 0.5f;

        AudioSource source;
        AudioClip connectClip;
        AudioClip landClip;
        AudioClip warningClip;
        AudioClip crashClip;
        float lastWarningTime = -10f;

        /// <summary>Switch for sound. On by default.</summary>
        public bool SoundOn { get; set; } = true;

        public void Initialise()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;

            connectClip = ToneSynth.ToClip("Connect", ToneSynth.Tone(1318.5f, 0.09f, 0.35f, 30f));
            landClip = ToneSynth.ToClip("Land", ToneSynth.Sequence(
                ToneSynth.Tone(1046.5f, 0.35f, 0.4f, 7f),   // C6
                ToneSynth.Tone(1318.5f, 0.45f, 0.4f, 6f),   // E6
                0.11f));
            warningClip = ToneSynth.ToClip("Warning", ToneSynth.Sequence(
                ToneSynth.Tone(880f, 0.08f, 0.35f, 10f),
                ToneSynth.Tone(880f, 0.08f, 0.35f, 10f),
                0.12f));
            crashClip = ToneSynth.ToClip("Crash", ToneSynth.Crash(0.7f, 0.8f));
        }

        public void LineConnected() => Play(connectClip);

        public void TouchedDown()
        {
            Play(landClip);
            Haptics.Light();
        }

        public void WarningStarted()
        {
            if (Time.unscaledTime - lastWarningTime < WarningRepeatSeconds)
                return;
            lastWarningTime = Time.unscaledTime;
            Play(warningClip);
            Haptics.Warning();
        }

        public void Collision()
        {
            Play(crashClip);
            Haptics.Error();
        }

        void Play(AudioClip clip)
        {
            if (SoundOn && source != null && clip != null)
                source.PlayOneShot(clip);
        }
    }
}
