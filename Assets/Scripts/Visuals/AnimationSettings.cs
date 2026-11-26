using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// Every animation value (blueprint 15.3: "animation values are data"), in one asset,
    /// Assets/Config/AnimationSettings.asset, tunable in the Inspector.
    ///
    /// On 20 and 25 Nov 2026 the owner removed the distracting animations (clouds and their shadows, birds, boats,
    /// swaying palms, foam, wave marks, contrails, the fighter's speed trail and lean, balloon bobbing and burner
    /// puffs, strobes, the landing sparkle and coin pop, the collision screen shake, pulsing approach lights and the
    /// Anims button). Only what gives information is left: status rings, lines, zone glow, touchdown highlight and
    /// arrows, runway lights that flash while a runway is busy, the windsock, the carrier with its countdown, slow
    /// subtle rotors, aircraft shadows and a short smoke puff at a collision.
    /// </summary>
    [CreateAssetMenu(menuName = "Air Traffic Control/Animation Settings", fileName = "AnimationSettings")]
    public sealed class AnimationSettings : ScriptableObject
    {
        [Tooltip("Master switch. Off: no movement or effects at all (rotors still, no smoke), for comparison.")]
        public bool animationsOn = true;

        [Header("Windsock and runway")]
        public float windsockTurnDegreesPerSecond = 90f;
        [Tooltip("Edge lights flash at this rate while the runway is busy (M3 step 3); they are hidden otherwise.")]
        public float busyFlashSeconds = 0.4f;

        [Header("Aircraft")]
        public Vector2 shadowOffset = new Vector2(0.12f, -0.12f);
        [Range(0f, 1f)] public float shadowAlpha = 0.22f;
        [Tooltip("Slow and subtle (owner, 25 Nov 2026).")]
        public float rotorDegreesPerSecond = 160f;
        public float tailRotorDegreesPerSecond = 260f;

        [Header("Events")]
        public int smokePuffs = 5;
        public float smokeSeconds = 1f;
        public float deckLightBlinkSeconds = 0.5f;
    }

    /// <summary>
    /// The animation settings in use. Everything that animates asks <see cref="On"/> each frame.
    /// </summary>
    public static class Animations
    {
        static AnimationSettings settings;
        static AnimationSettings fallback;

        public static AnimationSettings Settings
        {
            get
            {
                if (settings != null)
                    return settings;
                if (fallback == null)
                    fallback = ScriptableObject.CreateInstance<AnimationSettings>();
                return fallback;
            }
        }

        public static bool On => Settings.animationsOn;

        public static void Use(AnimationSettings asset)
        {
            settings = asset;
        }
    }

    /// <summary>Small timing maths shared by the animations. Plain C#, so it can be unit tested.</summary>
    public static class AnimationMath
    {
        /// <summary>True during the short flash at the start of each period (strobes, blinking lights).</summary>
        public static bool Flash(float time, float everySeconds, float flashSeconds, float phase = 0f)
        {
            if (everySeconds <= 0f)
                return false;
            return Mathf.Repeat(time + phase, everySeconds) < flashSeconds;
        }

        /// <summary>A smooth back-and-forth between -amplitude and +amplitude.</summary>
        public static float Sway(float time, float amplitude, float periodSeconds, float phase = 0f) =>
            periodSeconds <= 0f ? 0f : amplitude * Mathf.Sin(2f * Mathf.PI * (time / periodSeconds + phase));

        /// <summary>Moves a value along a looping strip from <paramref name="min"/> to <paramref name="max"/> </summary>
        public static float Wrap(float value, float min, float max) => min + Mathf.Repeat(value - min, max - min);
    }
}
