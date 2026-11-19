using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// Every animation value for the map and aircraft (blueprint 15.3: "animation values are data and can be turned
    /// off for comparison"). One asset, Assets/Config/AnimationSettings.asset, tunable in the Inspector. The master
    /// switch turns all movement and effects off; the scenery itself stays, still.
    /// </summary>
    [CreateAssetMenu(menuName = "Air Traffic Control/Animation Settings", fileName = "AnimationSettings")]
    public sealed class AnimationSettings : ScriptableObject
    {
        [Tooltip("Master switch. Off: no movement or effects, for comparison. The in-game Anims button flips it too.")]
        public bool animationsOn = true;

        [Header("Sea")]
        public float waveDriftSpeed = 0.12f;          // world units per second
        public float foamPulse = 0.015f;               // fraction of the island's size
        public float foamPulseSeconds = 4f;

        [Header("Island")]
        public float palmSwayDegrees = 6f;
        public float palmSwaySeconds = 3.2f;
        public float windsockFlutterDegrees = 7f;
        public float windsockTurnDegreesPerSecond = 90f;

        [Header("Runway lights")]
        [Tooltip("Seconds for one pulse of the approach lights to run towards the touchdown zone.")]
        public float approachLightSequenceSeconds = 1.2f;
        [Tooltip("Edge lights flash at this rate while the runway is busy (M3 step 3).")]
        public float busyFlashSeconds = 0.4f;

        [Header("Traffic on the sea and in the sky")]
        public float boatSpeed = 0.18f;
        public float cloudSpeed = 0.1f;
        public Vector2 cloudShadowOffset = new Vector2(0.45f, -0.35f);
        public float birdSpeed = 0.5f;
        public float birdFlapSeconds = 0.5f;

        [Header("Aircraft")]
        public Vector2 shadowOffset = new Vector2(0.12f, -0.12f);
        [Range(0f, 1f)] public float shadowAlpha = 0.22f;
        public float strobeEverySeconds = 1.2f;
        public float strobeFlashSeconds = 0.08f;
        public float contrailSeconds = 0.8f;
        public float fighterTrailSeconds = 0.35f;
        public float afterburnerFlicker = 0.35f;       // fraction of the flame length
        [Tooltip("How flat the fighter gets while it turns, as a fraction of its width (1 = no lean).")]
        public float fighterLeanScale = 0.8f;
        public float rotorDegreesPerSecond = 900f;
        public float tailRotorDegreesPerSecond = 1500f;
        public float droneBlinkSeconds = 0.6f;
        public float balloonBobAmount = 0.035f;
        public float balloonBobSeconds = 2.6f;
        public float balloonBurnerEverySeconds = 3.5f;
        public float helicopterWobbleDegrees = 7f;

        [Header("Events")]
        public float shakeStrength = 0.12f;
        public float shakeSeconds = 0.4f;
        public int smokePuffs = 7;
        public float smokeSeconds = 1.6f;
        public float coinPopHeight = 0.6f;
        public float coinPopSeconds = 0.9f;
        public float deckLightBlinkSeconds = 0.5f;
    }

    /// <summary>
    /// The animation settings in use, and the runtime switch. Everything that animates asks <see cref="On"/> each frame,
    /// so the Anims button takes effect at once.
    /// </summary>
    public static class Animations
    {
        static AnimationSettings settings;
        static AnimationSettings fallback;

        /// <summary>The runtime switch (the Anims button). Starts as the asset's master switch.</summary>
        public static bool RuntimeOn { get; set; } = true;

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

        public static bool On => RuntimeOn && Settings.animationsOn;

        public static void Use(AnimationSettings asset)
        {
            settings = asset;
            RuntimeOn = asset == null || asset.animationsOn;
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

        /// <summary>
        /// Which of <paramref name="lights"/> approach lights is lit: the pulse runs from the far one (0) towards the
        /// runway (lights - 1) once per <paramref name="sequenceSeconds"/>.
        /// </summary>
        public static int ApproachLight(float time, int lights, float sequenceSeconds)
        {
            if (lights <= 0 || sequenceSeconds <= 0f)
                return -1;
            int index = Mathf.FloorToInt(Mathf.Repeat(time, sequenceSeconds) / sequenceSeconds * lights);
            return Mathf.Clamp(index, 0, lights - 1);
        }

        /// <summary>Moves a value along a looping strip from <paramref name="min"/> to <paramref name="max"/> (boats, clouds, birds).</summary>
        public static float Wrap(float value, float min, float max) => min + Mathf.Repeat(value - min, max - min);

        /// <summary>Screen-shake strength left after <paramref name="elapsed"/> seconds of a shake lasting <paramref name="duration"/>.</summary>
        public static float ShakeStrength(float elapsed, float duration, float strength)
        {
            if (duration <= 0f || elapsed >= duration || elapsed < 0f)
                return 0f;
            float left = 1f - elapsed / duration;
            return strength * left * left;
        }

        /// <summary>How much a turning fighter leans: 1 when straight, down to <paramref name="leanScale"/> in a hard turn.</summary>
        public static float Lean(float turnDegreesPerSecond, float leanScale)
        {
            float amount = Mathf.Clamp01(Mathf.Abs(turnDegreesPerSecond) / 240f);
            return Mathf.Lerp(1f, leanScale, amount);
        }
    }
}
