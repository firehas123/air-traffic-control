using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// The Island level data fitted to the owner's painted map, Assets/Art/Maps/island_day.png (1449 x 1086 pixels,
    /// 20 Nov 2026). Every position below was measured from pixels in that picture (x from the left edge, y down from the
    /// top) and converted to world units at 108.6 pixels per unit, centred on the picture's middle. The pixel values are
    /// kept here so a test can check the level data still lands on the painted features. If the picture changes,
    /// measure again and update these numbers.
    /// </summary>
    public static class IslandPictureLayout
    {
        public static readonly Vector2 PictureSize = new Vector2(1449f, 1086f);
        public const float PixelsPerUnit = 108.6f;

        // ---- Measured in the picture (pixels) ----
        /// <summary>Runway asphalt: x 74 to 1367, y 424 to 510.</summary>
        public static readonly Rect RunwayPixels = new Rect(74f, 424f, 1367f - 74f, 510f - 424f);
        /// <summary>White threshold stripes: west x 89 to 141, east x 1303 to 1353.</summary>
        public static readonly Vector2 WestStripesPixels = new Vector2(89f, 141f);
        public static readonly Vector2 EastStripesPixels = new Vector2(1303f, 1353f);
        public static readonly Vector2 HelipadPixels = new Vector2(293f, 233f);
        public const float HelipadRadiusPixels = 50f;
        public static readonly Vector2 BalloonFieldPixels = new Vector2(1165f, 227f);
        public const float BalloonFieldRadiusPixels = 93f;
        /// <summary>The two parking spots on the apron in front of the terminal.</summary>
        public static readonly Vector2 WestStandPixels = new Vector2(611f, 283f);
        public static readonly Vector2 EastStandPixels = new Vector2(800f, 283f);
        /// <summary>The remote stand south of the runway.</summary>
        public static readonly Vector2 RemoteStandPixels = new Vector2(705f, 640f);
        /// <summary>The south taxiway's hold point just before the runway, where it stops for Cross.</summary>
        public static readonly Vector2 CrossingPixels = new Vector2(705f, 545f);
        /// <summary>The taxi line through the apron and along the north taxiways.</summary>
        public const float NorthTaxiLinePixelsY = 328f;
        /// <summary>The west and east taxiways meet the runway at these x positions.</summary>
        public const float WestTaxiwayPixelsX = 295f;
        public const float EastTaxiwayPixelsX = 1137f;
        public static readonly Vector2 TowerPixels = new Vector2(917f, 100f);

        public static Vector2 World(float px, float py) => CameraFraming.PixelToWorld(new Vector2(px, py), PictureSize, PixelsPerUnit);
        public static Vector2 World(Vector2 pixel) => CameraFraming.PixelToWorld(pixel, PictureSize, PixelsPerUnit);
        public static float Units(float pixels) => pixels / PixelsPerUnit;

        /// <summary>Fills the level data from the measurements above.</summary>
        public static void Apply(LevelData level)
        {
            // Runway, with both touchdown zones on the white threshold stripes (from the runway end to the last stripe).
            Vector2 runwayCentre = World(RunwayPixels.center);
            float zoneStart = Units(Mathf.Min(WestStripesPixels.x - RunwayPixels.xMin, RunwayPixels.xMax - EastStripesPixels.y));
            float zoneLength = Units(Mathf.Max(WestStripesPixels.y - WestStripesPixels.x, EastStripesPixels.y - EastStripesPixels.x));
            level.runways = new List<RunwayDefinition>
            {
                new RunwayDefinition
                {
                    centre = runwayCentre,
                    landingHeadingDegrees = 0f,
                    length = Units(RunwayPixels.width),
                    width = Units(RunwayPixels.height),
                    touchdownZoneStart = zoneStart,
                    touchdownZoneLength = zoneLength,
                },
            };

            level.landingPads = new List<LandingPadDefinition>
            {
                new LandingPadDefinition(LandingZoneKind.Helipad, World(HelipadPixels), Units(HelipadRadiusPixels)),
                new LandingPadDefinition(LandingZoneKind.BalloonField, World(BalloonFieldPixels), Units(BalloonFieldRadiusPixels)),
            };

            // Taxi routes along the painted taxiways. The main taxiway runs from the west hold point (just north of the
            // runway, where the west taxiway meets it) round through the apron to the east hold point.
            const float holdY = 395f;   // aircraft centre clear of the runway edge (y 424)
            const float lineUpY = 467f; // runway centreline
            float north = NorthTaxiLinePixelsY;
            level.ground.mainTaxiway = new List<Vector2>
            {
                World(WestTaxiwayPixelsX, holdY), World(WestTaxiwayPixelsX, 380f), World(299f, 355f), World(310f, 337f),
                World(343f, north), World(1090f, north), World(1118f, 338f), World(1132f, 355f),
                World(EastTaxiwayPixelsX, 380f), World(EastTaxiwayPixelsX, holdY),
            };
            level.ground.useLineUpPoints = true;
            level.ground.normalLineUp = World(WestTaxiwayPixelsX, lineUpY);
            level.ground.reversedLineUp = World(EastTaxiwayPixelsX, lineUpY);
            level.ground.liftOffDistance = 3f;
            level.ground.gates = new List<GateDefinition>
            {
                Gate(WestStandPixels, World(WestStandPixels.x, north)),
                Gate(EastStandPixels, World(EastStandPixels.x, north)),
                new GateDefinition
                {
                    position = World(RemoteStandPixels),
                    headingDegrees = 90f,
                    connector = new List<Vector2>
                    {
                        World(RemoteStandPixels), World(CrossingPixels), World(CrossingPixels.x, holdY), World(CrossingPixels.x, north),
                    },
                    crossingIndex = 1, // stops at the crossing point; the next segment crosses the runway
                },
            };

            // Overlays placed on the picture.
            level.scenery.windsockPosition = World(968f, 150f); // on the grass beside the control tower
            level.scenery.boatLanes = new List<float> { World(0f, 810f).y, World(0f, 1050f).y };
            level.scenery.approachLights = 3;
            level.scenery.approachLightSpacing = 0.2f;
            level.wind.indicatorPosition = World(1392f, 330f);

            // Carrier lane in the open sea at the bottom of the picture; on wider screens it may rise, never above the shore.
            level.carrier.laneY = World(0f, 960f).y;
            level.carrier.highestLaneY = World(0f, 800f).y;

            // Aircraft enter from the screen edges (kept just inside the screen at run time).
            level.spawnPoints = new List<SpawnPoint>
            {
                new SpawnPoint(new Vector2(-6.5f, 3.6f), -15f),
                new SpawnPoint(new Vector2(6.5f, 3.6f), 195f),
                new SpawnPoint(new Vector2(-6.5f, -2.8f), 15f),
                new SpawnPoint(new Vector2(6.5f, -2.8f), 165f),
            };

            // The airport, which the camera never crops: from the west beach to the east beach, the tower's top to
            // just below the remote stand.
            Vector2 a = World(55f, 695f);
            Vector2 b = World(1400f, 60f);
            level.mapArt.keepVisible = Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }

        static GateDefinition Gate(Vector2 standPixels, Vector2 joinWorld) => new GateDefinition
        {
            position = World(standPixels),
            headingDegrees = 90f, // parked nose-in towards the terminal
            connector = new List<Vector2> { World(standPixels), joinWorld },
            crossingIndex = -1,
        };
    }
}
