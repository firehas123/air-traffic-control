using System;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// When the Island wind changes (C1: changing wind reverses the runway's landing direction).
    /// The wind changes after a random interval between the minimum and maximum, and a warning runs for the
    /// last few seconds before each change. Runs on game time. Plain C#, no Unity components, so it can be unit tested.
    /// </summary>
    public sealed class WindSchedule
    {
        readonly float minInterval;
        readonly float maxInterval;
        readonly System.Random random;

        public WindSchedule(float minSecondsBetweenChanges, float maxSecondsBetweenChanges, float warningSeconds, int seed)
        {
            minInterval = Mathf.Max(1f, minSecondsBetweenChanges);
            maxInterval = Mathf.Max(minInterval, maxSecondsBetweenChanges);
            WarningSeconds = Mathf.Clamp(warningSeconds, 0f, minInterval);
            random = new System.Random(seed);
            Reset();
        }

        public float WarningSeconds { get; }

        /// <summary>True when the landing direction is the reverse of the runway's normal one.</summary>
        public bool Reversed { get; private set; }

        public float SecondsUntilChange { get; private set; }

        /// <summary>True during the last <see cref="WarningSeconds"/> before a change.</summary>
        public bool IsWarning => SecondsUntilChange <= WarningSeconds;

        /// <summary>Whole seconds left, for the warning text ("Wind changing in 3").</summary>
        public int WholeSecondsUntilChange => Mathf.Max(1, Mathf.CeilToInt(SecondsUntilChange));

        /// <summary>Back to the normal direction, with a fresh interval before the first change.</summary>
        public void Reset()
        {
            Reversed = false;
            SecondsUntilChange = NextInterval();
        }

        /// <summary>Advances by <paramref name="deltaTime"/> seconds. Returns true if the wind changed.</summary>
        public bool Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
                return false;

            SecondsUntilChange -= deltaTime;
            if (SecondsUntilChange > 0f)
                return false;

            Reversed = !Reversed;
            SecondsUntilChange += NextInterval();
            return true;
        }

        float NextInterval() => minInterval + (float)random.NextDouble() * (maxInterval - minInterval);
    }
}
