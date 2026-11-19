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

        [Header("Helipads and balloon fields")]
        [Tooltip("Round landing zones. Helicopters and drones land on helipads, hot air balloons on balloon fields.")]
        public List<LandingPadDefinition> landingPads = new List<LandingPadDefinition>
        {
            new LandingPadDefinition(LandingZoneKind.Helipad, new Vector2(-1.8f, 0.15f), 0.45f),
            new LandingPadDefinition(LandingZoneKind.BalloonField, new Vector2(3.55f, 0.2f), 0.55f),
        };

        [Header("Level feature: carrier ship (blueprint 13.2)")]
        public CarrierSettings carrier = new CarrierSettings();

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

    [Serializable]
    public sealed class LandingPadDefinition
    {
        [Tooltip("Helipad or BalloonField. (A runway is set up under Runways.)")]
        public LandingZoneKind kind = LandingZoneKind.Helipad;
        public Vector2 centre;
        [Min(0.1f)] public float radius = 0.45f;

        public LandingPadDefinition() { }

        public LandingPadDefinition(LandingZoneKind kind, Vector2 centre, float radius)
        {
            this.kind = kind;
            this.centre = centre;
            this.radius = radius;
        }

        public LandingPad ToPad() => new LandingPad(kind, centre, radius);
    }

    /// <summary>
    /// The carrier ship level feature (blueprint 13.2): a ship crosses the sea from time to time and fighter jets can
    /// land on its moving deck while it is fully on screen. Starting values chosen by Claude; the owner tunes them.
    /// </summary>
    [Serializable]
    public sealed class CarrierSettings
    {
        [Tooltip("Whether this level has the carrier at all.")]
        public bool hasCarrier = true;

        [Tooltip("Seconds into the level before the first ship. Kept short in the test level so the owner sees it early.")]
        [Min(0f)] public float firstShipAfterSeconds = 10f;

        [Tooltip("A new ship every this many seconds, counted from the previous ship's appearance (13.2: 40 to 60).")]
        [Min(1f)] public float minSecondsBetweenShips = 40f;
        [Min(1f)] public float maxSecondsBetweenShips = 60f;

        [Tooltip("Seconds from just off one side of the screen to just off the other (13.2: about 20).")]
        [Min(1f)] public float secondsToCross = 20f;

        [Tooltip("Height of the sea lane the ship sails along, below the island.")]
        public float laneY = -4.1f;

        public float shipLength = 2.6f;
        public float shipWidth = 0.7f;

        [Tooltip("The deck is a small runway on the ship, with its own touchdown zone and arrows.")]
        public float deckLength = 2.1f;
        public float deckWidth = 0.5f;
        public float touchdownZoneStart = 0.1f;
        public float touchdownZoneLength = 0.7f;
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
