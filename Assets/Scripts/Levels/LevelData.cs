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

        [Tooltip("Draw (lines) or Tower (tap commands), blueprint 16.1.")]
        public GameMode mode = GameMode.Draw;

        [Tooltip("The measured layout of the map picture this level is played on (Assets/Maps/Layouts).")]
        public PictureLayout layout;

        [Header("Objective (C2: land a set number of aircraft)")]
        [Tooltip("Landings needed to clear the level. 0 means no target: the level only ends on a collision.")]
        [Min(0)] public int landingsToClear = 8;

        [Tooltip("Departures needed to clear the level (blueprint 15.4: for example land 6, launch 3). 0 means none.")]
        [Min(0)] public int departuresToClear = 0;

        [Header("Result screen (C10)")]
        public ResultSettings result = new ResultSettings();

        [Header("Painted map (art rule of 20 Nov 2026: maps are painted images under Assets/Art)")]
        public MapArtSettings mapArt = new MapArtSettings();

        [Header("Island (drawn in code only when the level has no painted map)")]
        public Vector2 islandCentre = new Vector2(1f, -0.8f);
        public Vector2 islandRadii = new Vector2(3.8f, 2.1f);

        [Header("Departures (blueprint 15.4): aircraft that start at a gate")]
        public DepartureSettings departures = new DepartureSettings();

        [Header("Scenery (decoration only, it never affects play)")]
        public SceneryLayout scenery = new SceneryLayout();

        [Header("Ground: taxiways and gates (drawn in M3 step 2, driven by ground control in step 3)")]
        public GroundLayout ground = new GroundLayout();

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

        [Header("Map rules of the other Draw maps (blueprint 16.2): City, Desert, Mountain Valley, Arctic")]
        public MapRuleSettings rules = new MapRuleSettings();

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

    /// <summary>
    /// A painted map image as the level's background (art rule changed by the owner on 20 Nov 2026). The sprite is
    /// imported with <see cref="pixelsPerUnit"/> so the picture keeps the game's world scale, centred on the origin.
    /// Every zone, gate and taxi route in the level data is measured from pixel positions in this picture.
    /// </summary>
    [Serializable]
    public sealed class MapArtSettings
    {
        public Sprite sprite;

        [Tooltip("Picture pixels per world unit. 108.6 makes the 1086-pixel-high Island picture 10 units high.")]
        public float pixelsPerUnit = 108.6f;

        [Tooltip("World area that must always be on screen (the airport). On wider screens the camera crops sea at " +
                 "the edges of the picture, never this area.")]
        public Rect keepVisible = new Rect(-6.165f, -1.4f, 12.385f, 5.848f);

        public bool HasPicture => sprite != null;

        /// <summary>The picture's area in world units.</summary>
        public Rect WorldRect
        {
            get
            {
                if (sprite == null)
                    return new Rect();
                Vector2 size = sprite.rect.size / Mathf.Max(1f, pixelsPerUnit);
                return new Rect(-size / 2f, size);
            }
        }
    }

    /// <summary>
    /// When departures appear at the gates. Only runway kinds depart (airliner, jumbo jet, fighter jet); the level
    /// spawns as many as its departure goal (LevelData.departuresToClear), one at a free gate at a time.
    /// </summary>
    [Serializable]
    public sealed class DepartureSettings
    {
        [Min(0f)] public float firstDepartureAfterSeconds = 6f;
        [Min(1f)] public float secondsBetweenDepartures = 15f;
    }

    /// <summary>
    /// Where the Island's code-drawn decoration goes, in world units. Used only by levels without a painted map;
    /// with a picture only the windsock is drawn (code-drawn scenery is never drawn over a picture, 25 Nov 2026).
    /// </summary>
    [Serializable]
    public sealed class SceneryLayout
    {
        public List<Vector2> palms = new List<Vector2>
        {
            new Vector2(-2.3f, -1.3f), new Vector2(-2.05f, -2.05f), new Vector2(-0.8f, -2.3f), new Vector2(0.6f, -2.35f),
            new Vector2(2.6f, -2.25f), new Vector2(3.9f, -1.6f), new Vector2(4.25f, -0.9f), new Vector2(-2.6f, -0.45f),
            new Vector2(-0.35f, 0.9f), new Vector2(2.8f, 0.7f),
        };

        [Tooltip("Rocks are scattered along the beach; this many, placed the same way every time.")]
        [Min(0)] public int rocks = 12;

        [Tooltip("The terminal building with its control tower, north of the apron.")]
        public Vector2 terminalCentre = new Vector2(0.9f, 0.98f);
        public Vector2 terminalSize = new Vector2(1.6f, 0.3f);
        public Vector2 towerPosition = new Vector2(1.95f, 0.98f);

        [Tooltip("The apron (parking area) in front of the terminal.")]
        public Vector2 apronCentre = new Vector2(1.05f, 0.45f);
        public Vector2 apronSize = new Vector2(2.3f, 0.62f);

        [Tooltip("Parked planes for decoration (they never move). Gates for departures are under Ground.")]
        public List<Vector2> parkedPlanes = new List<Vector2> { new Vector2(1.3f, 0.45f), new Vector2(1.9f, 0.45f) };

        public Vector2 windsockPosition = new Vector2(-1.05f, -1.5f);
    }

    /// <summary>
    /// Taxiways and gates (blueprint 15.4). The main taxiway runs beside the runway; its two ends are the hold
    /// lines for the two runway ends (the first point for the runway's normal landing direction, the last point for
    /// the reversed one). Each gate has a connector from the gate to the main taxiway; a connector may cross the
    /// runway, at its crossing point.
    /// </summary>
    [Serializable]
    public sealed class GroundLayout
    {
        public List<Vector2> mainTaxiway = new List<Vector2> { new Vector2(-0.6f, -0.15f), new Vector2(2.6f, -0.15f) };

        public List<GateDefinition> gates = new List<GateDefinition>();

        [Tooltip("Where a departure enters the runway for the normal landing direction (take-off the same way). " +
                 "Used when 'Use Line Up Points' is on; otherwise it lines up just past the threshold.")]
        public bool useLineUpPoints;
        public Vector2 normalLineUp;
        public Vector2 reversedLineUp;

        [Tooltip("Distance along the runway from the line-up point to lift-off. 0 = 70% of the runway that is left.")]
        [Min(0f)] public float liftOffDistance;

        [Tooltip("Taxi speed in world units per second at 1x.")]
        [Min(0.05f)] public float taxiSpeed = 0.35f;

        [Tooltip("A taxiing aircraft stops this far behind the one in front of it.")]
        [Min(0.1f)] public float queueSpacing = 0.75f;
    }

    [Serializable]
    public sealed class GateDefinition
    {
        public Vector2 position;

        [Tooltip("Direction the parked aircraft faces, in degrees: 0 = right, 90 = up.")]
        public float headingDegrees;

        [Tooltip("From the gate to a point on the main taxiway. The first point is the gate, the last is on the main taxiway.")]
        public List<Vector2> connector = new List<Vector2>();

        [Tooltip("Index of the connector point where the aircraft must stop and wait for Cross (the segment after it " +
                 "crosses the runway). -1 when the connector does not cross the runway.")]
        public int crossingIndex = -1;
    }

    /// <summary>Star thresholds (PROPOSED, to be tuned by playing) and coins (C9) for the result screen.</summary>
    [Serializable]
    public sealed class ResultSettings
    {
        [Tooltip("Clear the level within this many seconds for 2 stars (C3 aims for 60 to 120 s per level).")]
        [Min(1f)] public float twoStarSeconds = 100f;

        [Tooltip("3 stars also need no danger warning during the whole level, so stars never reward reckless flying.")]
        public bool threeStarsNeedNoWarnings = true;

        [Min(0)] public int coinsPerLanding = 5;
        [Min(0)] public int coinsPerDeparture = 5;

        [Tooltip("Paid only the first time each star is earned (C9).")]
        [Min(0)] public int coinsPerStar = 10;
    }

    [Serializable]
    public sealed class LandingPadDefinition
    {
        [Tooltip("Helipad or BalloonField. (A runway is set up under Runways.)")]
        public LandingZoneKind kind = LandingZoneKind.Helipad;
        public Vector2 centre;
        [Min(0.1f)] public float radius = 0.45f;

        [Tooltip("Draw a white H on top: the painted pad is a plain ring (08_mountain_draw).")]
        public bool drawH;

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

        [Tooltip("The painted carrier (Assets/Art/Maps/15_carrier.png, bow to the right). Without it the ship is drawn in code.")]
        public Sprite picture;

        [Tooltip("Part of the picture's width that is the ship (15_carrier.png: 1459 of 1536 pixels). The ship is drawn Ship Length long.")]
        [Range(0.1f, 1f)] public float pictureShipFraction = 0.95f;

        [Tooltip("Seconds into the level before the first ship. Kept short in the test level so the owner sees it early.")]
        [Min(0f)] public float firstShipAfterSeconds = 10f;

        [Tooltip("A new ship every this many seconds, counted from the previous ship's appearance (13.2: 40 to 60).")]
        [Min(1f)] public float minSecondsBetweenShips = 40f;
        [Min(1f)] public float maxSecondsBetweenShips = 60f;

        [Tooltip("Seconds from just off one side of the screen to just off the other (13.2: about 20).")]
        [Min(1f)] public float secondsToCross = 20f;

        [Tooltip("Height of the sea lane the ship sails along, below the island.")]
        public float laneY = -4.1f;

        [Tooltip("On screens wider than 4:3 the bottom of the map is cropped; the lane moves up to stay on screen, " +
                 "but never above this height (the island's shore).")]
        public float highestLaneY = 10f;

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

        [Tooltip("Width of the area a line must end in to land. 0 = the runway's width. Draw mode widens narrow painted " +
                 "runways so a finger can hit them.")]
        [Min(0f)] public float landingWidth;

        public Runway ToRunway() => new Runway(centre, landingHeadingDegrees, length, width, touchdownZoneStart, touchdownZoneLength, landingWidth);
    }
}
