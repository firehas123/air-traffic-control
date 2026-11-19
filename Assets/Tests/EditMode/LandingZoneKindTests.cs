using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AirTrafficControl.Tests
{
    /// <summary>Helipads, balloon fields, and "a line lands only on a zone that matches the aircraft kind" (blueprint 13.1).</summary>
    public class LandingZoneKindTests
    {
        // Runway as in the other landing tests: touchdown zone from x = -0.4 to 0.3 at y = -1.
        static readonly Runway Eastbound = new Runway(new Vector2(1f, -1f), 0f, 3f, 0.6f, 0.1f, 0.7f);
        static readonly LandingPad Helipad = new LandingPad(LandingZoneKind.Helipad, new Vector2(-2f, 1f), 0.45f);
        static readonly LandingPad BalloonField = new LandingPad(LandingZoneKind.BalloonField, new Vector2(3f, 1f), 0.55f);

        static readonly List<Runway> Runways = new List<Runway> { Eastbound };
        static readonly List<LandingPad> Pads = new List<LandingPad> { Helipad, BalloonField };

        static List<Vector2> Line(params Vector2[] points) => new List<Vector2>(points);

        static readonly List<Vector2> ToRunwayArrows = Line(new Vector2(-3f, -1f), new Vector2(0f, -1f));
        static readonly List<Vector2> ToHelipad = Line(new Vector2(-4f, 3f), new Vector2(-2f, 1.05f));
        static readonly List<Vector2> ToBalloonField = Line(new Vector2(5f, 3f), new Vector2(3.1f, 1f));

        static bool Lands(List<Vector2> line, LandingZoneKind kind) =>
            LandingRules.TryFindTouchdown(Runways, Pads, line, kind, out _);

        [Test]
        public void EachKind_LandsOnlyOnItsOwnZone()
        {
            Assert.IsTrue(Lands(ToRunwayArrows, LandingZoneKind.Runway));
            Assert.IsFalse(Lands(ToRunwayArrows, LandingZoneKind.Helipad), "A helicopter cannot land on the runway");
            Assert.IsFalse(Lands(ToRunwayArrows, LandingZoneKind.BalloonField));

            Assert.IsTrue(Lands(ToHelipad, LandingZoneKind.Helipad));
            Assert.IsFalse(Lands(ToHelipad, LandingZoneKind.Runway), "An airliner cannot land on the helipad");
            Assert.IsFalse(Lands(ToHelipad, LandingZoneKind.BalloonField), "A balloon cannot land on the helipad");

            Assert.IsTrue(Lands(ToBalloonField, LandingZoneKind.BalloonField));
            Assert.IsFalse(Lands(ToBalloonField, LandingZoneKind.Helipad), "A drone cannot land on the balloon field");
        }

        [Test]
        public void Touchdown_SaysWhichZone()
        {
            Assert.IsTrue(LandingRules.TryFindTouchdown(Runways, Pads, ToHelipad, LandingZoneKind.Helipad, out Touchdown pad));
            Assert.IsFalse(pad.IsRunway);
            Assert.AreEqual(Helipad.Centre, pad.Pad.Centre);

            Assert.IsTrue(LandingRules.TryFindTouchdown(Runways, Pads, ToRunwayArrows, LandingZoneKind.Runway, out Touchdown runway));
            Assert.IsTrue(runway.IsRunway);
            Assert.AreEqual(0f, runway.Runway.LandingHeadingDegrees, 1e-4f);
        }

        [TestCase(0f)]
        [TestCase(90f)]
        [TestCase(200f)]
        [TestCase(315f)]
        public void Pad_LandsFromAnyDirection(float approachHeading)
        {
            float radians = approachHeading * Mathf.Deg2Rad;
            Vector2 end = Helipad.Centre;
            Vector2 start = end - new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * 2f;
            Assert.IsTrue(LandingRules.CanLandOnPad(Helipad, Line(start, end)));
        }

        [Test]
        public void Pad_TheLineMustOverlapIt_NotJustTouchItsEdge()
        {
            Vector2 start = Helipad.Centre + new Vector2(-2f, 0f);
            Vector2 justInsideEdge = Helipad.Centre + new Vector2(-(Helipad.Radius - 0.05f), 0f);
            Vector2 wellInside = Helipad.Centre + new Vector2(-(Helipad.Radius - LandingRules.ConnectInset - 0.02f), 0f);

            Assert.IsFalse(LandingRules.CanLandOnPad(Helipad, Line(start, justInsideEdge)));
            Assert.IsTrue(LandingRules.CanLandOnPad(Helipad, Line(start, wellInside)));
        }

        [Test]
        public void Pad_LineEndingOutside_DoesNotLand()
        {
            Assert.IsFalse(LandingRules.CanLandOnPad(Helipad, Line(new Vector2(-5f, 1f), new Vector2(-2.6f, 1f))));
            Assert.IsFalse(LandingRules.CanLandOnPad(Helipad, Line(Helipad.Centre)), "No length");
        }

        [Test]
        public void Pad_Contains_UsesTheInset()
        {
            Assert.IsTrue(Helipad.Contains(Helipad.Centre + new Vector2(0.44f, 0f)));
            Assert.IsFalse(Helipad.Contains(Helipad.Centre + new Vector2(0.44f, 0f), LandingRules.ConnectInset));
            Assert.IsFalse(Helipad.Contains(Helipad.Centre + new Vector2(0.46f, 0f)));
        }

        [Test]
        public void SecondPadOfTheSameKind_IsFound()
        {
            var second = new LandingPad(LandingZoneKind.Helipad, new Vector2(0f, 3f), 0.4f);
            var pads = new List<LandingPad> { Helipad, BalloonField, second };
            var line = Line(new Vector2(0f, 0f), new Vector2(0f, 3f));

            Assert.IsTrue(LandingRules.TryFindTouchdown(Runways, pads, line, LandingZoneKind.Helipad, out Touchdown touchdown));
            Assert.AreEqual(second.Centre, touchdown.Pad.Centre);
        }

        [Test]
        public void AgreedKindValues_MatchTheBlueprint()
        {
            // Blueprint 13.1: speeds relative to the airliner, and where each kind lands.
            AssertKind(AircraftSilhouette.HotAirBalloon, 0.35f, LandingZoneKind.BalloonField);
            AssertKind(AircraftSilhouette.Helicopter, 0.6f, LandingZoneKind.Helipad);
            AssertKind(AircraftSilhouette.Drone, 0.8f, LandingZoneKind.Helipad);
            AssertKind(AircraftSilhouette.Airliner, 1f, LandingZoneKind.Runway);
            AssertKind(AircraftSilhouette.JumboJet, 0.85f, LandingZoneKind.Runway);
            AssertKind(AircraftSilhouette.FighterJet, 1.6f, LandingZoneKind.Runway);
        }

        static void AssertKind(AircraftSilhouette silhouette, float speed, LandingZoneKind landsOn)
        {
            var type = ScriptableObject.CreateInstance<AircraftType>();
            type.SetDefaults(silhouette);
            Assert.AreEqual(speed, type.speedMultiplier, 1e-5f, silhouette.ToString());
            Assert.AreEqual(landsOn, type.landsOn, silhouette.ToString());
            Assert.AreEqual(silhouette == AircraftSilhouette.FighterJet, type.landsOnCarrier, "Only fighters use the carrier");
            Assert.IsFalse(IsStatusColour(type.color), $"{silhouette} must not be green, yellow or red");
            Object.DestroyImmediate(type);
        }

        // Green, yellow and red mean cleared, needs a line, and danger.
        static bool IsStatusColour(Color c)
        {
            Color.RGBToHSV(c, out float hue, out float saturation, out _);
            if (saturation < 0.35f)
                return false; // white or grey
            float degrees = hue * 360f;
            bool red = degrees < 15f || degrees > 345f;
            bool yellow = degrees > 45f && degrees < 70f;
            bool green = degrees > 80f && degrees < 160f;
            return red || yellow || green;
        }
    }
}
