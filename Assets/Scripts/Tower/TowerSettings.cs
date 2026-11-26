using System;
using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// Tower mode data of a level (blueprint 16.1, phase 3, 26 Nov 2026): where arrivals enter on their fixed approach
    /// routes, where aircraft hold, how they fly the approach and go around, and how they move on the ground. Starting
    /// values chosen by Claude; the owner tunes them after playing. Filled from the picture by <see cref="TowerModeSetup"/>.
    /// </summary>
    [Serializable]
    public sealed class TowerSettings
    {
        [Tooltip("Where arrivals enter at the screen edge, one per runway end (its fixed approach route). Pad kinds use any.")]
        public List<TowerApproach> approaches = new List<TowerApproach>();

        [Tooltip("Places near the screen edge where aircraft circle on Hold.")]
        public List<Vector2> holdFixes = new List<Vector2>();

        [Tooltip("Straight final before the runway end, in world units: the approach is lined up with the runway for this long.")]
        [Min(0.2f)] public float finalLength = 0.9f;

        [Tooltip("Turn rate on the approach routes, in degrees per second: the turn radius is the aircraft's speed divided by it.")]
        [Min(10f)] public float turnRateDegrees = 50f;

        [Tooltip("No turn is tighter than this radius, in world units.")]
        [Min(0.1f)] public float minTurnRadius = 0.35f;

        [Tooltip("Go around: about this many game seconds after the order, the aircraft is back on its final approach.")]
        [Min(3f)] public float goAroundSeconds = 10f;

        [Tooltip("Hold: radius of the circle, in world units (at least the turn radius).")]
        [Min(0.3f)] public float holdRadius = 0.75f;

        [Tooltip("Landing roll at normal speed, in world units. Fast rolls longer (blueprint 15.4); ice makes Fast slide further.")]
        [Min(0.3f)] public float rollDistance = 2.4f;

        [Tooltip("Speed along the runway after the landing roll, to the exit and off the runway, in world units per second at 1x.")]
        [Min(0.05f)] public float exitSpeed = 0.5f;

        [Tooltip("Taxi speed in world units per second at 1x (slower than any aircraft that taxis flies).")]
        [Min(0.05f)] public float taxiSpeed = 0.42f;

        [Tooltip("Arctic: an aircraft that slid on ice after a Fast landing stands still this long before it can leave the runway.")]
        [Min(0f)] public float slideStopSeconds = 2.5f;

        [Tooltip("A taxiing aircraft stops this far behind the one in front of it.")]
        [Min(0.1f)] public float queueSpacing = 0.75f;

        [Tooltip("A parked arrival stays at its stand this long (passengers off), then the stand is free again.")]
        [Min(0f)] public float parkedSeconds = 2f;

        [Tooltip("Mountain Valley: aircraft may only land on runways along the valley (east and west), within this many degrees.")]
        public bool valleyOnly;
        [Range(0f, 90f)] public float valleyMaxAngle = 30f;

        [Tooltip("First-time help (Island levels 1 and 2): teach Go around in this level.")]
        public bool teachGoAround;

        [Tooltip("First-time help: teach Hold in this level.")]
        public bool teachHold;

        /// <summary>Turn radius for an aircraft flying at <paramref name="speed"/>.</summary>
        public float TurnRadius(float speed) => Mathf.Max(minTurnRadius, speed / (turnRateDegrees * Mathf.Deg2Rad));

        /// <summary>True when a runway with this landing direction may be used (Mountain Valley allows east and west only).</summary>
        public bool AllowsRunway(Runway runway)
        {
            if (!valleyOnly)
                return true;
            float off = Mathf.Abs(Mathf.DeltaAngle(runway.LandingHeadingDegrees, 0f));
            off = Mathf.Min(off, 180f - off);
            return off <= valleyMaxAngle + 0.01f;
        }
    }

    /// <summary>One fixed approach: an entry point at the screen edge and the runway end it leads to.</summary>
    [Serializable]
    public sealed class TowerApproach
    {
        [Tooltip("Runway index in the level (A = 0, B = 1, C = 2).")]
        public int runway;

        [Tooltip("True for the approach to end B (landing from end B towards end A, the reversed direction).")]
        public bool endB;

        public Vector2 entry;

        [Tooltip("Direction the aircraft flies when it appears, in degrees: 0 = right, 90 = up.")]
        public float headingDegrees;
    }

    /// <summary>Runway letters for the buttons and the map: A, B, C in the level's order.</summary>
    public static class RunwayNames
    {
        public static string Letter(int index) => index >= 0 && index < 26 ? ((char)('A' + index)).ToString() : "?";
    }
}
