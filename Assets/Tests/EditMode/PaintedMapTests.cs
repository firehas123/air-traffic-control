using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AirTrafficControl.Tests
{
    /// <summary>The painted Island map (owner's picture, 20 Nov 2026): camera framing and level data fitted to the picture.</summary>
    public class CameraFramingTests
    {
        static readonly Rect Picture = new Rect(-6.6713f, -5f, 13.3425f, 10f); // 1449 x 1086 at 108.6 px per unit
        static readonly Rect Airport = new Rect(-6.165f, -1.4f, 12.385f, 5.848f);

        static void AssertInside(Rect inner, Rect outer, string what)
        {
            Assert.LessOrEqual(outer.xMin, inner.xMin + 1e-3f, what);
            Assert.GreaterOrEqual(outer.xMax, inner.xMax - 1e-3f, what);
            Assert.LessOrEqual(outer.yMin, inner.yMin + 1e-3f, what);
            Assert.GreaterOrEqual(outer.yMax, inner.yMax - 1e-3f, what);
        }

        [Test]
        public void IPad_ShowsTheWholePicture()
        {
            Rect view = CameraFraming.View(Picture, Airport, 2732f / 2048f);
            Assert.AreEqual(10f, view.height, 0.01f, "The island fills the screen");
            Assert.AreEqual(Picture.width, view.width, 0.02f);
            AssertInside(view, Picture, "Nothing past the picture's edges");
        }

        [TestCase(16f / 9f)]
        [TestCase(19.5f / 9f)]
        [TestCase(2532f / 1170f)]
        public void WiderScreens_CropSeaNeverTheAirport(float aspect)
        {
            Rect view = CameraFraming.View(Picture, Airport, aspect);
            AssertInside(Airport, view, "The airport is always on screen");
            AssertInside(view, Picture, "Only picture is shown, no empty space");
            Assert.Less(view.height, Picture.height, "Some sea is cropped");
        }

        [Test]
        public void ExtremeScreen_StillKeepsTheAirport()
        {
            Rect view = CameraFraming.View(Picture, Airport, 3.5f);
            AssertInside(Airport, view, "The airport wins, even if the view must go past the picture");
        }

        [Test]
        public void PixelAndWorld_RoundTrip()
        {
            var size = new Vector2(1449f, 1086f);
            Vector2 world = CameraFraming.PixelToWorld(new Vector2(293f, 233f), size, 108.6f);
            Vector2 back = CameraFraming.WorldToPixel(world, size, 108.6f);
            Assert.AreEqual(293f, back.x, 1e-3f);
            Assert.AreEqual(233f, back.y, 1e-3f);
            Assert.AreEqual(Vector2.zero, CameraFraming.PixelToWorld(size / 2f, size, 108.6f), "The picture is centred on the origin");
        }

        [Test]
        public void SpawnPoints_StayJustInsideTheScreen()
        {
            var view = new Rect(-6.67f, -1.7f, 13.34f, 6.15f); // a wide phone
            Vector2 p = SpawnRules.ClampToView(new Vector2(-6.5f, -2.8f), view, 0.35f);
            Assert.AreEqual(-6.32f, p.x, 1e-3f);
            Assert.AreEqual(-1.35f, p.y, 1e-3f);
        }
    }

    public class IslandPictureFitTests
    {
        static LevelData Fitted()
        {
            LevelData level = ScriptableObject.CreateInstance<LevelData>();
            IslandPictureLayout.Apply(level);
            return level;
        }

        static Vector2 Pixel(Vector2 world) =>
            CameraFraming.WorldToPixel(world, IslandPictureLayout.PictureSize, IslandPictureLayout.PixelsPerUnit);

        static void AssertNear(Vector2 expectedPixel, Vector2 world, float tolerance, string what)
        {
            Vector2 p = Pixel(world);
            Assert.AreEqual(expectedPixel.x, p.x, tolerance, what + " (x)");
            Assert.AreEqual(expectedPixel.y, p.y, tolerance, what + " (y)");
        }

        [Test]
        public void Runway_SitsOnThePaintedRunway()
        {
            Runway runway = Fitted().runways[0].ToRunway();
            Rect painted = IslandPictureLayout.RunwayPixels;
            AssertNear(painted.center, runway.Centre, 1f, "Runway centre");
            Assert.AreEqual(painted.width, runway.Length * IslandPictureLayout.PixelsPerUnit, 1f);
            Assert.AreEqual(painted.height, runway.Width * IslandPictureLayout.PixelsPerUnit, 1f);
        }

        [Test]
        public void TouchdownZones_AreOnTheWhiteStripesAtBothEnds()
        {
            Runway runway = Fitted().runways[0].ToRunway();
            foreach (Runway direction in new[] { runway, runway.Reversed() })
            {
                Vector2 start = direction.Threshold + direction.Direction * direction.TouchdownZoneStart;
                Vector2 end = start + direction.Direction * direction.TouchdownZoneLength;
                float a = Pixel(start).x;
                float b = Pixel(end).x;
                Vector2 stripes = direction.LandingHeadingDegrees < 90f ? IslandPictureLayout.WestStripesPixels : IslandPictureLayout.EastStripesPixels;
                Assert.AreEqual(stripes.x, Mathf.Min(a, b), 3f, "Zone starts at the first stripe");
                Assert.AreEqual(stripes.y, Mathf.Max(a, b), 3f, "Zone ends at the last stripe");
            }
        }

        [Test]
        public void Pads_AreOnTheHelipadAndTheBalloonField()
        {
            LevelData level = Fitted();
            LandingPadDefinition helipad = level.landingPads.Find(p => p.kind == LandingZoneKind.Helipad);
            LandingPadDefinition field = level.landingPads.Find(p => p.kind == LandingZoneKind.BalloonField);
            AssertNear(IslandPictureLayout.HelipadPixels, helipad.centre, 1f, "Helipad (west)");
            AssertNear(IslandPictureLayout.BalloonFieldPixels, field.centre, 1f, "Balloon field (east)");
            Assert.Less(helipad.centre.x, 0f, "Helipad is west");
            Assert.Greater(field.centre.x, 0f, "Balloon field is east");
        }

        [Test]
        public void Gates_AreTheTwoApronSpotsAndTheRemoteStand()
        {
            LevelData level = Fitted();
            Assert.AreEqual(3, level.ground.gates.Count);
            AssertNear(IslandPictureLayout.WestStandPixels, level.ground.gates[0].position, 1f, "West apron spot");
            AssertNear(IslandPictureLayout.EastStandPixels, level.ground.gates[1].position, 1f, "East apron spot");
            AssertNear(IslandPictureLayout.RemoteStandPixels, level.ground.gates[2].position, 1f, "Remote stand");
            Assert.Less(level.ground.gates[2].position.y, level.runways[0].centre.y, "Remote stand is south of the runway");
        }

        [Test]
        public void EveryConnector_EndsOnTheMainTaxiway()
        {
            LevelData level = Fitted();
            foreach (GateDefinition gate in level.ground.gates)
            {
                Vector2 end = gate.connector[gate.connector.Count - 1];
                Assert.Less(DistanceToPolyline(end, level.ground.mainTaxiway), 0.01f, $"Gate at {gate.position} joins the taxiway");
            }
        }

        [Test]
        public void CrossingPoint_IsWhereTheSouthTaxiwayMeetsTheRunway_AndOffIt()
        {
            LevelData level = Fitted();
            Runway runway = level.runways[0].ToRunway();
            GateDefinition remote = level.ground.gates[2];
            Vector2 crossing = remote.connector[remote.crossingIndex];
            AssertNear(IslandPictureLayout.CrossingPixels, crossing, 1f, "Crossing point");
            Assert.IsFalse(runway.Contains(crossing, AircraftType.AirlinerCollisionRadius), "Waits clear of the runway");

            Vector2 across = remote.connector[remote.crossingIndex + 1];
            Assert.IsFalse(runway.Contains(across, AircraftType.AirlinerCollisionRadius), "Ends clear on the other side");
            Assert.Less(crossing.y, runway.Centre.y);
            Assert.Greater(across.y, runway.Centre.y, "The segment after the crossing point crosses the runway");
        }

        [Test]
        public void HoldPoints_AreClearOfTheRunway_AndLineUpPointsOnItsCentreline()
        {
            LevelData level = Fitted();
            Runway runway = level.runways[0].ToRunway();
            List<Vector2> main = level.ground.mainTaxiway;
            foreach (Vector2 hold in new[] { main[0], main[main.Count - 1] })
                Assert.IsFalse(runway.Contains(hold, AircraftType.AirlinerCollisionRadius), $"Hold point {hold} is clear of the runway");
            foreach (Vector2 lineUp in new[] { level.ground.normalLineUp, level.ground.reversedLineUp })
                Assert.AreEqual(runway.Centre.y, lineUp.y, 0.02f, "Line-up on the centreline");
            Assert.Less(main[0].x, 0f, "First end is the west end (normal direction takes off eastwards from it)");
        }

        [Test]
        public void CarrierLane_IsInTheOpenSeaBelowTheIsland()
        {
            LevelData level = Fitted();
            float lanePixel = Pixel(new Vector2(0f, level.carrier.laneY)).y;
            Assert.Greater(lanePixel - Pixel(new Vector2(0f, level.carrier.laneY + level.carrier.shipWidth / 2f)).y, 0f);
            Assert.Greater(Pixel(new Vector2(0f, level.carrier.laneY + level.carrier.shipWidth / 2f)).y, 790f, "Ship's top edge is below the shore");
            Assert.Less(Pixel(new Vector2(0f, level.carrier.laneY - level.carrier.shipWidth / 2f)).y, 1086f, "Inside the picture");
        }

        [Test]
        public void TheAirportRect_ContainsEverythingThePlayerUses()
        {
            LevelData level = Fitted();
            Rect keep = level.mapArt.keepVisible;
            Runway runway = level.runways[0].ToRunway();
            Assert.IsTrue(keep.Contains(runway.Threshold) && keep.Contains(runway.Reversed().Threshold), "Both runway ends");
            foreach (LandingPadDefinition pad in level.landingPads)
                Assert.IsTrue(keep.Contains(pad.centre), "Pads");
            foreach (GateDefinition gate in level.ground.gates)
                Assert.IsTrue(keep.Contains(gate.position), "Gates");
        }

        [Test]
        public void RemoteDeparture_StopsAtTheCrossing_ThenCrossesThePaintedRunway()
        {
            LevelData level = Fitted();
            GateDefinition remote = level.ground.gates[2];
            Runway runway = level.runways[0].ToRunway();
            var g = new GroundMovement(remote.connector, remote.crossingIndex, level.ground.mainTaxiway, 0.35f, 0.6f,
                                       remote.headingDegrees, false, level.ground.normalLineUp, level.ground.reversedLineUp,
                                       level.ground.liftOffDistance);
            g.Taxi();
            for (float t = 0f; t < 10f; t += 1f / 60f) g.Step(1f / 60f, false);
            Assert.AreEqual(GroundPhase.HoldingAtCrossing, g.Phase);

            g.Cross();
            bool wasOnRunway = false;
            for (float t = 0f; t < 30f && g.Phase != GroundPhase.HoldingShort; t += 1f / 60f)
            {
                g.Step(1f / 60f, false);
                wasOnRunway |= runway.Contains(g.Position);
            }
            Assert.IsTrue(wasOnRunway, "It crossed the runway");
            Assert.AreEqual(GroundPhase.HoldingShort, g.Phase, "Then taxied on to the hold line");

            g.TakeOff(runway);
            for (float t = 0f; t < 30f && g.Phase != GroundPhase.Airborne; t += 1f / 60f)
                g.Step(1f / 60f, false);
            Assert.AreEqual(GroundPhase.Airborne, g.Phase);
            Assert.IsTrue(runway.Contains(g.Position, 0.05f), "Lifts off over the runway, within the lift-off distance");
        }

        [Test]
        public void TheIslandLevelAsset_UsesThePicture_AndAircraftImages()
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelData>("Assets/Levels/IslandPrototype.asset");
            Assert.IsNotNull(level.mapArt.sprite, "island_day.png is the map");
            Assert.AreEqual(10f, level.mapArt.WorldRect.height, 0.01f);
            foreach (AircraftType kind in level.aircraftTypes)
            {
                Assert.IsNotNull(kind.sprite, $"{kind.displayName} has its painted image");
                Assert.AreEqual(kind.silhouette == AircraftSilhouette.HotAirBalloon ? 0f : -90f, kind.spriteRotation, 1e-3f,
                                "Nose-up images are turned so the nose leads; the balloon stays upright");
            }
        }

        static float DistanceToPolyline(Vector2 p, IReadOnlyList<Vector2> line)
        {
            float best = float.MaxValue;
            for (int i = 1; i < line.Count; i++)
            {
                Vector2 a = line[i - 1], b = line[i], ab = b - a;
                float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }
    }
}
