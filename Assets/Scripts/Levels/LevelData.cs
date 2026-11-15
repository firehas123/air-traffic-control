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

        [Header("Island (drawn under the runway)")]
        public Vector2 islandCentre = new Vector2(1f, -0.8f);
        public Vector2 islandRadii = new Vector2(3.8f, 2.1f);

        [Header("Runways")]
        public List<RunwayDefinition> runways = new List<RunwayDefinition>
        {
            new RunwayDefinition(),
        };

        [Header("Aircraft")]
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
