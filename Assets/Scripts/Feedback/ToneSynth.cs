using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// Builds the placeholder sound effects in code (M2 step 4), so the game needs no audio files yet and works offline.
    /// The sample maths is plain C# and unit tested; <see cref="ToClip"/> wraps samples in an AudioClip.
    /// </summary>
    public static class ToneSynth
    {
        public const int SampleRate = 44100;

        /// <summary>A sine tone with a quick fade in and an exponential fade out.</summary>
        public static float[] Tone(float frequency, float seconds, float volume, float decay = 6f)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(seconds * SampleRate));
            var samples = new float[count];
            int attack = Mathf.Min(count, SampleRate / 200); // 5 ms, avoids a click
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float envelope = Mathf.Exp(-decay * t) * (i < attack ? (float)i / attack : 1f);
                samples[i] = volume * envelope * Mathf.Sin(2f * Mathf.PI * frequency * t);
            }
            return samples;
        }

        /// <summary>Plays <paramref name="b"/> after <paramref name="a"/>, starting <paramref name="gapSeconds"/> after a starts.</summary>
        public static float[] Sequence(float[] a, float[] b, float gapSeconds)
        {
            int offset = Mathf.RoundToInt(gapSeconds * SampleRate);
            var samples = new float[Mathf.Max(a.Length, offset + b.Length)];
            for (int i = 0; i < a.Length; i++) samples[i] += a[i];
            for (int i = 0; i < b.Length; i++) samples[offset + i] += b[i];
            return Clamp(samples);
        }

        /// <summary>A low thump with a burst of noise, for a collision. Same <paramref name="seed"/>, same sound.</summary>
        public static float[] Crash(float seconds, float volume, int seed = 7)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(seconds * SampleRate));
            var samples = new float[count];
            var random = new System.Random(seed);
            float lowPassed = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                lowPassed += 0.15f * (noise - lowPassed);
                float thump = Mathf.Sin(2f * Mathf.PI * (90f - 40f * t) * t);
                samples[i] = volume * Mathf.Exp(-5f * t) * (0.6f * thump + 0.7f * lowPassed);
            }
            return Clamp(samples);
        }

        static float[] Clamp(float[] samples)
        {
            for (int i = 0; i < samples.Length; i++)
                samples[i] = Mathf.Clamp(samples[i], -1f, 1f);
            return samples;
        }

        public static AudioClip ToClip(string name, float[] samples)
        {
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
