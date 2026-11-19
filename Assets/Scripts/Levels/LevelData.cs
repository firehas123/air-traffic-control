using System;
using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// One level as data (CLAUDE.md: levels are data, not code). Edit the asset in the Inspector.
    /// For now it holds the map layout, how many aircraft are kept in the air, and where they appear.
    /// Spawn schedules, objectives and rewards come with LevelDirector.
    /// </summary>
    [CreateAssetMenu(menuName = "Air Traffic Control/Level Data", fileName = "LevelData")]
    public sealed class LevelData : ScriptableObject
    {
        public string mapId = "island";

        [Header("Objective (C2: land a set number of aircraft)")]
        [Tooltip("Landings needed to clear the level. 0 means no target: the level only ends on a collision.")]
        [Min(0)] public int landingsToClear = 8;

        [Header("Island (drawn under the runway)")]
        public Vector2 islandCentre = new Vector2(1f, -0.8f);
        public Vector2 islandRadii = new Vector2(3.8f, 2.1f);

        [Header("Runways")]
        public List<RunwayDefinition> runways = new List<RunwayDefinition>
        {
            new RunwayDefinition(),
        };

        [Header("Map rule: wind (C1, Island)")]
        public WindRuleSettings wind = new WindRuleSettings();

        [Header("Aircraft")]
        [Tooltip("Kinds of aircraft this level spawns, picked at random for each new aircraft. Repeat a kind to make it more common.")]
        public List<AircraftType> aircraftTypes = new List<AircraftType>();

        [Tooltip("How many aircraft are kept in the air. A new one appears after each landing or crash.")]
        [Min(1)] public int aircraftInAir = 2;

        [Tooltip("Where new aircraft appear. Each new aircraft uses the next point that no other aircraft is near.")]
        public List<SpawnPoint> spawnPoints = new List<SpawnPoint>
        {
            new SpawnPoint(new Vector2(-5.5f, 3.3f), -10f),  // top left, heading right
            new SpawnPoint(new Vector2(5.5f, 3.3f), 200f),   // top right, heading left and down
            new SpawnPoint(new Vector2(-5.5f, -3.5f), 15f),  // bottom left, heading right and up
            new SpawnPoint(new Vector2(5.5f, -3.6f), 165f),  // bottom right, heading left and up
        };
    }

    /// <summary>The Island map rule (C1): the wind changes and reverses the runway's landing direction.</summary>
    [Serializable]
    public sealed class WindRuleSettings
    {
        [Tooltip("Whether this level has the wind rule at all.")]
        public bool hasWind = true;

        [Tooltip("Whether the wind rule starts switched on. The in-game Wind button switches it for comparison (OPEN: player setting or test switch only).")]
        public bool onAtStart = true;

        public float minSecondsBetweenChanges = 25f;
        public float maxSecondsBetweenChanges = 40f;

        [Tooltip("The wind indicator pulses and a countdown shows for this long before each change.")]
        public float warningSeconds = 5f;

        [Tooltip("Grace period (blueprint 14.3): an aircraft cleared to land that would reach touchdown within this " +
                 "many game seconds when the wind changes still lands at the old end.")]
        [Min(0f)] public float landingGraceSeconds = WindGrace.DefaultGraceSeconds;

        [Tooltip("Where the wind indicator (dark circle with an arrow) is drawn.")]
        public Vector2 indicatorPosition = new Vector2(5.3f, 1.8f);
    }

    [Serializable]
    public sealed class SpawnPoint
    {
        public Vector2 position;

        [Tooltip("Direction the aircraft flies when it appears, in degrees: 0 = right, 90 = up.")]
        public float headingDegrees;

        public SpawnPoint() { }

        public SpawnPoint(Vector2 position, float headingDegrees)
        {
            this.position = position;
            this.headingDegrees = headingDegrees;
        }
    }

    [Serializable]
    public sealed class RunwayDefinition
    {
        public Vector2 centre = new Vector2(1f, -0.8f);

        [Tooltip("Direction aircraft travel when landing, in degrees: 0 = right, 90 = up.")]
        public float landingHeadingDegrees = 0f;

        public float length = 3.2f;
        public float width = 0.6f;

        [Tooltip("Where the highlighted touchdown zone starts, measured from the start of the runway. Lines must end in the zone to land.")]
        public float touchdownZoneStart = Runway.DefaultTouchdownZoneStart;

        [Tooltip("Length of the highlighted touchdown zone. The landing arrows are drawn inside it.")]
        public float touchdownZoneLength = Runway.DefaultTouchdownZoneLength;

        public Runway ToRunway() => new Runway(centre, landingHeadingDegrees, length, width, touchdownZoneStart, touchdownZoneLength);
    }
}
