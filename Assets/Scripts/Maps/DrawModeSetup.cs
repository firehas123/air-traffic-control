using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// Fits a Draw-mode level to its map picture (blueprint 16.1, phase 2, 26 Nov 2026): every runway, both of its ends,
    /// the helipad and the balloon field from the measured layout; no ground control (an aircraft that lands is done);
    /// landing targets big enough for a finger; aircraft entering where they can fly (the valley on Mountain Valley); the
    /// mountains as no-fly areas (used when the level switches the rule on).
    ///
    /// The landing sizes are Claude's starting values: the painted Draw pictures show the airport small, so the painted
    /// touchdown stripes are only about 0.4 units long and the runways 0.22 to 0.43 units wide, which a line cannot hit
    /// reliably with the 0.12 overlap rule (LandingRules.ConnectInset, owner decision 15 Nov 2026). Plain C#, so it can be
    /// unit tested.
    /// </summary>
    public static class DrawModeSetup
    {
        /// <summary>A touchdown zone is at least this long (world units), measured from the start of the painted stripes.</summary>
        public const float MinTouchdownZoneLength = 0.9f;

        /// <summary>A line lands within this width across the runway, even where the painted runway is narrower.</summary>
        public const float MinLandingWidth = 0.5f;

        /// <summary>Helipads and balloon fields take lines ending within this radius of their centre, at least.</summary>
        public const float MinPadRadius = 0.34f;

        public static void Apply(LevelData level, PictureLayout layout)
        {
            layout.ApplyTo(level);
            level.mode = GameMode.Draw;

            // No ground control in Draw mode.
            level.ground.mainTaxiway = new List<Vector2>();
            level.ground.gates = new List<GateDefinition>();
            level.departuresToClear = 0;

            for (int i = 0; i < level.runways.Count; i++)
            {
                RunwayDefinition runway = level.runways[i];
                float longest = Mathf.Min(runway.length / 2f, ClearLengthFromEnds(layout, i)) - runway.touchdownZoneStart;
                runway.touchdownZoneLength = Mathf.Max(runway.touchdownZoneLength, Mathf.Min(MinTouchdownZoneLength, longest));
                runway.landingWidth = Mathf.Max(runway.width, MinLandingWidth);
            }
            foreach (LandingPadDefinition pad in level.landingPads)
                pad.radius = Mathf.Max(pad.radius, MinPadRadius);

            // Mountains from the picture, as no-fly areas in world units.
            level.rules.noFly.areas = new List<NoFlyArea>();
            foreach (PictureArea area in layout.ruleAreas)
                if (area.kind == PictureArea.Mountain)
                    level.rules.noFly.areas.Add(new NoFlyArea { name = area.name, points = area.points.ConvertAll(layout.World) });

            Rect picture = layout.WorldRect;
            float x = picture.width / 2f - 1f;
            if (level.rules.noFly.areas.Count > 0)
            {
                // Mountain Valley: aircraft enter along the valley, never inside a mountain.
                level.spawnPoints = new List<SpawnPoint>
                {
                    new SpawnPoint(new Vector2(-x, 1.1f), -8f),
                    new SpawnPoint(new Vector2(x, 1.1f), 188f),
                    new SpawnPoint(new Vector2(-x, -1.5f), 8f),
                    new SpawnPoint(new Vector2(x, -1.5f), 172f),
                };
            }

            // The wind indicator sits just off the top right corner of the airport.
            Rect airport = layout.KeepVisibleWorld;
            level.wind.indicatorPosition = new Vector2(airport.xMax + 0.6f, airport.yMax - 0.2f);
        }

        // How far in from either end the runway is clear of crossing runways (world units): a touchdown zone stops short
        // of an intersection, so it never covers another runway.
        static float ClearLengthFromEnds(PictureLayout layout, int index)
        {
            PictureRunway runway = layout.runways[index];
            float clear = float.MaxValue;
            foreach (PictureIntersection crossing in layout.intersections)
            {
                string other = crossing.runwayA == runway.name ? crossing.runwayB : crossing.runwayB == runway.name ? crossing.runwayA : null;
                if (other == null)
                    continue;
                PictureRunway otherRunway = layout.FindRunway(other);
                float otherHalfWidth = otherRunway != null ? otherRunway.width / 2f : 0f;
                float fromA = Vector2.Distance(runway.endA, crossing.point);
                float fromB = Vector2.Distance(runway.endB, crossing.point);
                clear = Mathf.Min(clear, layout.Units(Mathf.Min(fromA, fromB) - otherHalfWidth) - 0.08f);
            }
            return clear;
        }

        /// <summary>The middle of a runway's touchdown zone for one landing direction (reversed = from end B).</summary>
        public static Vector2 ZoneCentre(RunwayDefinition definition, bool reversed)
        {
            Runway runway = reversed ? definition.ToRunway().Reversed() : definition.ToRunway();
            return runway.Threshold + runway.Direction * (runway.TouchdownZoneStart + runway.TouchdownZoneLength / 2f);
        }
    }
}
