using System;
using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// Where everything is on one painted map picture (blueprint 16, 25 Nov 2026): runways with their angle and the
    /// touchdown zones on the white threshold stripes, runway intersections, the helipad and balloon field, stands,
    /// taxi routes, hold lines, crossing points and map-rule areas. One asset per picture, under Assets/Maps/Layouts.
    ///
    /// Every value is in picture pixels (x from the left edge, y down from the top), measured from the picture, so a
    /// test can check that each one still lands on the painted feature. <see cref="World(Vector2)"/> converts to world
    /// units, with the picture centred on the origin. If a picture changes, measure it again and update the asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Air Traffic Control/Picture Layout", fileName = "PictureLayout")]
    public sealed class PictureLayout : ScriptableObject
    {
        [Tooltip("File name of the picture under Assets/Art/Maps, for example 02_island_draw.png.")]
        public string pictureFile;
        public Sprite picture;
        public GameMode mode;

        public Vector2 pictureSize = new Vector2(1536f, 1024f);

        [Tooltip("Picture pixels per world unit. 102.4 makes a 1024-pixel-high picture 10 units high, the game's world scale.")]
        public float pixelsPerUnit = 102.4f;

        public List<PictureRunway> runways = new List<PictureRunway>();
        public List<PictureIntersection> intersections = new List<PictureIntersection>();
        public List<PicturePad> pads = new List<PicturePad>();
        public List<PictureStand> stands = new List<PictureStand>();
        public List<PicturePath> taxiRoutes = new List<PicturePath>();
        public List<PictureHoldLine> holdLines = new List<PictureHoldLine>();
        public List<PictureCrossing> crossings = new List<PictureCrossing>();
        public List<PictureArea> ruleAreas = new List<PictureArea>();

        public bool hasWindsock;
        public Vector2 windsock;

        [Tooltip("Picture area (pixels, x and y of the top-left corner, then width and height) that the camera always " +
                 "shows: the airport and every landing place. On a 4:3 iPad the camera crops plain ground or sea at the " +
                 "picture's left and right edges, never this area.")]
        public Rect keepVisiblePixels;

        // ---- Conversions ----

        public Vector2 World(Vector2 pixel) => CameraFraming.PixelToWorld(pixel, pictureSize, pixelsPerUnit);
        public Vector2 World(float x, float y) => World(new Vector2(x, y));
        public Vector2 Pixel(Vector2 world) => CameraFraming.WorldToPixel(world, pictureSize, pixelsPerUnit);
        public float Units(float pixels) => pixels / Mathf.Max(0.001f, pixelsPerUnit);

        /// <summary>The picture's area in world units, centred on the origin.</summary>
        public Rect WorldRect
        {
            get
            {
                Vector2 size = pictureSize / Mathf.Max(0.001f, pixelsPerUnit);
                return new Rect(-size / 2f, size);
            }
        }

        /// <summary>The area the camera always shows, in world units.</summary>
        public Rect KeepVisibleWorld
        {
            get
            {
                Vector2 a = World(new Vector2(keepVisiblePixels.xMin, keepVisiblePixels.yMax));
                Vector2 b = World(new Vector2(keepVisiblePixels.xMax, keepVisiblePixels.yMin));
                return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
            }
        }

        public PictureRunway FindRunway(string name) => runways.Find(r => r.name == name);
        public PicturePath FindRoute(string name) => taxiRoutes.Find(r => r.name == name);
        public PictureArea FindArea(string kind) => ruleAreas.Find(a => a.kind == kind);

        /// <summary>The runway in game terms: landing from end A towards end B (the wind rule may reverse it).</summary>
        public RunwayDefinition ToRunwayDefinition(PictureRunway runway)
        {
            Vector2 a = World(runway.endA);
            Vector2 b = World(runway.endB);
            Vector2 along = b - a;
            // One touchdown zone length serves both ends (Runway.Reversed keeps it), so use the part of the stripes
            // that both ends share.
            float start = Mathf.Max(runway.zoneA.x, runway.zoneB.x);
            float end = Mathf.Min(runway.zoneA.y, runway.zoneB.y);
            return new RunwayDefinition
            {
                centre = (a + b) / 2f,
                landingHeadingDegrees = Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg,
                length = along.magnitude,
                width = Units(runway.width),
                touchdownZoneStart = Units(start),
                touchdownZoneLength = Units(Mathf.Max(1f, end - start)),
            };
        }

        /// <summary>
        /// Fills a level's map from this picture: the picture, the area the camera keeps, runways, helipad and balloon
        /// field, the windsock, the carrier lane, spawn points at the screen edges and (when the picture has it) the
        /// ground control routes. Aircraft kinds, goals and rules stay as they are in the level.
        /// </summary>
        public void ApplyTo(LevelData level)
        {
            level.layout = this;
            level.mode = mode;
            level.mapArt.sprite = picture;
            level.mapArt.pixelsPerUnit = pixelsPerUnit;
            level.mapArt.keepVisible = KeepVisibleWorld;

            level.runways = new List<RunwayDefinition>();
            foreach (PictureRunway runway in runways)
                level.runways.Add(ToRunwayDefinition(runway));

            level.landingPads = new List<LandingPadDefinition>();
            foreach (PicturePad pad in pads)
                level.landingPads.Add(new LandingPadDefinition(pad.kind, World(pad.centre), Units(pad.radius)) { drawH = pad.drawH });

            if (hasWindsock)
                level.scenery.windsockPosition = World(windsock);

            Rect pictureRect = WorldRect;
            PictureArea lane = FindArea(PictureArea.CarrierLane);
            if (lane != null && lane.points.Count > 0)
            {
                float top = float.MaxValue, bottom = float.MinValue;
                foreach (Vector2 p in lane.points)
                {
                    top = Mathf.Min(top, p.y);
                    bottom = Mathf.Max(bottom, p.y);
                }
                level.carrier.laneY = World(0f, (top + bottom) / 2f).y;
                level.carrier.highestLaneY = World(0f, top).y - level.carrier.shipWidth / 2f;
            }
            else
            {
                level.carrier.hasCarrier = false;
            }

            // Aircraft enter near the screen edges (kept just inside the screen at run time).
            float x = pictureRect.width / 2f - 1f;
            level.spawnPoints = new List<SpawnPoint>
            {
                new SpawnPoint(new Vector2(-x, 3.6f), -15f),
                new SpawnPoint(new Vector2(x, 3.6f), 195f),
                new SpawnPoint(new Vector2(-x, -3.4f), 15f),
                new SpawnPoint(new Vector2(x, -3.4f), 165f),
            };

            // The code-drawn ground of the old prototype is not used on a picture: Tower mode's ground control works on
            // the picture's own taxi routes (TaxiNetwork).
            level.ground.mainTaxiway = new List<Vector2>();
            level.ground.gates = new List<GateDefinition>();
        }
    }

    /// <summary>A runway in picture pixels: the centreline's two ends (outer edge of the painted runway) and its width.</summary>
    [Serializable]
    public sealed class PictureRunway
    {
        public string name;
        public Vector2 endA;
        public Vector2 endB;
        public float width;

        [Tooltip("The white threshold stripes at end A, as distances in pixels from end A along the runway (from, to).")]
        public Vector2 zoneA;

        [Tooltip("The white threshold stripes at end B, as distances in pixels from end B (from, to).")]
        public Vector2 zoneB;

        public float Length => Vector2.Distance(endA, endB);
        public Vector2 Along => (endB - endA).normalized;
    }

    [Serializable]
    public sealed class PictureIntersection
    {
        public string runwayA;
        public string runwayB;
        public Vector2 point;
    }

    [Serializable]
    public sealed class PicturePad
    {
        public LandingZoneKind kind = LandingZoneKind.Helipad;
        public Vector2 centre;
        public float radius;

        [Tooltip("True when the picture shows a plain ring here, so the game draws an H on top (08_mountain_draw).")]
        public bool drawH;
    }

    [Serializable]
    public sealed class PictureStand
    {
        public string name;
        public Vector2 position;

        [Tooltip("Direction a parked aircraft faces, in degrees on screen: 0 = right, 90 = up.")]
        public float headingDegrees = 90f;
    }

    /// <summary>A taxi route along a painted taxiway's centre line.</summary>
    [Serializable]
    public sealed class PicturePath
    {
        public string name;
        public List<Vector2> points = new List<Vector2>();
    }

    /// <summary>A painted hold line: where an aircraft waits before entering or crossing a runway.</summary>
    [Serializable]
    public sealed class PictureHoldLine
    {
        public Vector2 point;
        public string runway;

        [Tooltip("End A, End B (waiting to line up at that end) or Crossing (waiting to cross).")]
        public string kind;

        public const string EndA = "End A";
        public const string EndB = "End B";
        public const string Crossing = "Crossing";
    }

    /// <summary>Where a taxi route crosses a runway (on the runway's centreline).</summary>
    [Serializable]
    public sealed class PictureCrossing
    {
        public Vector2 point;
        public string runway;
        public string route;
    }

    /// <summary>An area used by the map's rule (C1, blueprint 16.2), as a polygon in pixels.</summary>
    [Serializable]
    public sealed class PictureArea
    {
        public const string CarrierLane = "CarrierLane";
        public const string Mountain = "Mountain";

        public string name;
        public string kind;
        public List<Vector2> points = new List<Vector2>();

        public bool Contains(Vector2 pixel)
        {
            bool inside = false;
            for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
            {
                Vector2 a = points[i], b = points[j];
                if ((a.y > pixel.y) != (b.y > pixel.y) && pixel.x < (b.x - a.x) * (pixel.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }
    }
}
