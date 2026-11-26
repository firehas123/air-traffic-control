using System;
using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// The map rules of Draw mode other than the Island wind (C1, blueprint 16.2), as level data. Each map uses one
    /// rule, switched on from its level 2. Starting values chosen by Claude (26 Nov 2026); the owner tunes them after
    /// playing. Island's wind is <see cref="WindRuleSettings"/>.
    /// </summary>
    [Serializable]
    public sealed class MapRuleSettings
    {
        [Tooltip("City: one runway at a time closes for a short period, with a warning and a clear X.")]
        public RunwayClosureSettings closures = new RunwayClosureSettings();

        [Tooltip("Desert: some aircraft arrive low on fuel and must land before their timer runs out.")]
        public FuelSettings fuel = new FuelSettings();

        [Tooltip("Desert: a slowly drifting sandstorm closes every runway it covers.")]
        public SandstormSettings sandstorm = new SandstormSettings();

        [Tooltip("Mountain Valley: the mountains are no-fly areas. A line cannot enter them and aircraft turn back at them.")]
        public NoFlySettings noFly = new NoFlySettings();

        [Tooltip("Arctic: icy runways. An aircraft landing on Fast slides, so its runway stays busy longer.")]
        public IceSettings ice = new IceSettings();

        /// <summary>True when any rule of this class is on.</summary>
        public bool Any => closures.enabled || fuel.enabled || sandstorm.enabled || noFly.enabled || ice.enabled;
    }

    /// <summary>City (blueprint 16.2): runways close for short periods. Lines cannot end on a closed runway.</summary>
    [Serializable]
    public sealed class RunwayClosureSettings
    {
        public bool enabled;

        [Tooltip("Seconds into the level before the first warning.")]
        [Min(0f)] public float firstWarningAfterSeconds = 12f;

        [Tooltip("The X blinks and a countdown shows for this long before the runway closes.")]
        [Min(0f)] public float warningSeconds = 5f;

        [Tooltip("A closed runway stays closed for a random time between these.")]
        [Min(1f)] public float minClosedSeconds = 10f;
        [Min(1f)] public float maxClosedSeconds = 15f;

        [Tooltip("After a runway opens again, all runways stay open for a random time between these before the next warning.")]
        [Min(0f)] public float minOpenSeconds = 10f;
        [Min(0f)] public float maxOpenSeconds = 18f;
    }

    /// <summary>Desert (blueprint 16.2): low-fuel aircraft with a visible fuel timer. Running out ends the level like a collision.</summary>
    [Serializable]
    public sealed class FuelSettings
    {
        public bool enabled;

        [Tooltip("Every this many new aircraft, one arrives low on fuel (the first one is the second aircraft).")]
        [Min(1)] public int lowFuelEvery = 3;

        [Tooltip("Game seconds of fuel at the 1x airliner speed; slower kinds get more in proportion, so every kind can make it.")]
        [Min(5f)] public float minFuelSeconds = 36f;
        [Min(5f)] public float maxFuelSeconds = 46f;

        [Tooltip("Kinds slower than this times the airliner never arrive low on fuel (the hot air balloon).")]
        [Min(0f)] public float slowestKind = 0.5f;
    }

    /// <summary>Desert (blueprint 16.2): a sandstorm drifts slowly across the map and closes any runway it covers.</summary>
    [Serializable]
    public sealed class SandstormSettings
    {
        public bool enabled;

        [Tooltip("Radius of the storm in world units (the map is 10 units high).")]
        [Min(0.2f)] public float radius = 0.95f;

        [Tooltip("Drift speed in world units per second at 1x.")]
        [Min(0.01f)] public float speed = 0.2f;

        [Min(0f)] public float firstStormAfterSeconds = 4f;

        [Tooltip("Pause between one storm leaving the screen and the next one appearing.")]
        [Min(0f)] public float minSecondsBetweenStorms = 6f;
        [Min(0f)] public float maxSecondsBetweenStorms = 12f;

        [Tooltip("A runway the storm will reach within this many seconds shows its X blinking, as a warning.")]
        [Min(0f)] public float warningSeconds = 5f;
    }

    /// <summary>Mountain Valley (blueprint 16.2): no-fly areas taken from the picture's measured mountains.</summary>
    [Serializable]
    public sealed class NoFlySettings
    {
        public bool enabled;

        [Tooltip("The areas, in world units (filled from the picture layout's Mountain areas).")]
        public List<NoFlyArea> areas = new List<NoFlyArea>();
    }

    [Serializable]
    public sealed class NoFlyArea
    {
        public string name;
        public List<Vector2> points = new List<Vector2>();
    }

    /// <summary>Arctic (blueprint 16.2): icy runways.</summary>
    [Serializable]
    public sealed class IceSettings
    {
        public bool enabled;

        [Tooltip("An aircraft that lands on Fast slides: its landing roll, and so the time its runway is busy, is this many times longer.")]
        [Min(1f)] public float fastSlideFactor = 2f;
    }
}
