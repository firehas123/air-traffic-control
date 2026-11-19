using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AirTrafficControl.Tests
{
    public class SeparationTests
    {
        // Collision radii from blueprint 14.1 (the AircraftType starting values).
        const float Airliner = 0.25f;
        const float Jumbo = 0.35f;
        const float Fighter = 0.18f;
        const float Drone = 0.18f;

        const float Speed = 1.2f;
        static readonly Vector2 East = new Vector2(Speed, 0f);
        static readonly Vector2 West = new Vector2(-Speed, 0f);
        static readonly Vector2 North = new Vector2(0f, Speed);

        static SeparationState Classify(Vector2 a, Vector2 velocityA, Vector2 b, Vector2 velocityB, float radiusA = Airliner, float radiusB = Airliner) =>
            SeparationRules.Classify(a, velocityA, radiusA, b, velocityB, radiusB);

        // ---- Closest approach ----

        [Test]
        public void ClosestApproach_HeadOn_MeetsAtTheMidpointTime()
        {
            Vector2 relativePosition = new Vector2(6f, 0f);
            Vector2 relativeVelocity = West - East;

            Assert.AreEqual(2.5f, SeparationRules.TimeOfClosestApproach(relativePosition, relativeVelocity), 1e-4f);
            Assert.AreEqual(0f, SeparationRules.ClosestApproachDistance(relativePosition, relativeVelocity, 10f), 1e-4f);
        }

        [Test]
        public void ClosestApproach_IsLimitedToTheLookAheadTime()
        {
            Assert.AreEqual(3.6f, SeparationRules.ClosestApproachDistance(new Vector2(6f, 0f), West - East, 1f), 1e-4f);
        }

        [Test]
        public void ClosestApproach_MovingApart_IsNow()
        {
            Vector2 relativePosition = new Vector2(2f, 0f);
            Assert.AreEqual(0f, SeparationRules.TimeOfClosestApproach(relativePosition, East - West));
            Assert.AreEqual(2f, SeparationRules.ClosestApproachDistance(relativePosition, East - West, 5f), 1e-4f);
        }

        [Test]
        public void ClosestApproach_SameVelocity_StaysTheSame()
        {
            Assert.AreEqual(0f, SeparationRules.TimeOfClosestApproach(new Vector2(1.5f, 0f), Vector2.zero));
            Assert.AreEqual(1.5f, SeparationRules.ClosestApproachDistance(new Vector2(1.5f, 0f), Vector2.zero, 5f), 1e-4f);
        }

        // ---- Two airliners keep the M1 numbers ----

        [Test]
        public void TwoAirliners_KeepTheM1Distances()
        {
            Assert.AreEqual(0.5f, SeparationRules.CollisionDistance(Airliner, Airliner), 1e-6f);
            Assert.AreEqual(1.2f, SeparationRules.ProximityWarningDistance(Airliner, Airliner), 1e-6f);
            Assert.AreEqual(0.6f, SeparationRules.PredictedMissDistance(Airliner, Airliner), 1e-6f);
            Assert.AreEqual(0.6f, SeparationRules.WarningRingRadius(Airliner), 1e-6f, "Two rings touch at the warning distance");
        }

        [Test]
        public void Classify_FarApartAndNotConverging_IsClear()
        {
            Assert.AreEqual(SeparationState.Clear, Classify(Vector2.zero, East, new Vector2(0f, 3f), East));
        }

        [Test]
        public void Classify_WithinProximityDistance_IsWarning()
        {
            Assert.AreEqual(SeparationState.Warning, Classify(Vector2.zero, East, new Vector2(0f, 1.1f), East));
        }

        [Test]
        public void Classify_WithinCollisionDistance_IsCollision()
        {
            Assert.AreEqual(SeparationState.Collision, Classify(Vector2.zero, East, new Vector2(0.4f, 0.2f), West));
        }

        [Test]
        public void Classify_HeadOnAndFarApart_WarnsByPrediction()
        {
            Assert.AreEqual(SeparationState.Warning, Classify(Vector2.zero, East, new Vector2(4f, 0f), West));
        }

        [Test]
        public void Classify_CrossingButPassingWellApart_IsClear()
        {
            Assert.AreEqual(SeparationState.Clear, Classify(Vector2.zero, East, new Vector2(-2f, -2f), North));
        }

        [Test]
        public void Classify_MovingApart_IsClear()
        {
            Assert.AreEqual(SeparationState.Clear, Classify(Vector2.zero, West, new Vector2(1.5f, 0f), East));
        }

        // ---- Collision size per kind (blueprint 14.1) ----

        [TestCase(Airliner, Airliner, 0.50f)]
        [TestCase(Jumbo, Drone, 0.53f)]
        [TestCase(Fighter, Jumbo, 0.53f)]
        [TestCase(Fighter, Airliner, 0.43f)]
        public void Pairs_CollideExactlyInsideTheirTwoRadii(float radiusA, float radiusB, float sum)
        {
            Vector2 justInside = new Vector2(sum - 0.01f, 0f);
            Vector2 justOutside = new Vector2(sum + 0.01f, 0f);

            Assert.AreEqual(SeparationState.Collision, Classify(Vector2.zero, Vector2.zero, justInside, Vector2.zero, radiusA, radiusB));
            Assert.AreNotEqual(SeparationState.Collision, Classify(Vector2.zero, Vector2.zero, justOutside, Vector2.zero, radiusA, radiusB));
        }

        [Test]
        public void BiggerAircraft_NeedMoreRoom()
        {
            // 0.52 apart and not moving: two airliners are fine, a jumbo and an airliner touch.
            var gap = new Vector2(0.52f, 0f);
            Assert.AreEqual(SeparationState.Warning, Classify(Vector2.zero, Vector2.zero, gap, Vector2.zero, Airliner, Airliner));
            Assert.AreEqual(SeparationState.Collision, Classify(Vector2.zero, Vector2.zero, gap, Vector2.zero, Jumbo, Airliner));
        }

        [Test]
        public void WarningDistance_GrowsWithTheRadii()
        {
            // 1.6 apart, side by side: clear for two airliners (warning at 1.2), a warning for two jumbos (1.68).
            var gap = new Vector2(0f, 1.6f);
            Assert.AreEqual(SeparationState.Clear, Classify(Vector2.zero, East, gap, East, Airliner, Airliner));
            Assert.AreEqual(SeparationState.Warning, Classify(Vector2.zero, East, gap, East, Jumbo, Jumbo));
        }

        [Test]
        public void PredictedPass_GrowsWithTheRadii()
        {
            // Parallel tracks 0.7 apart, closing head-on from far away: no warning for two airliners (0.6),
            // a warning for two jumbos (0.84).
            var b = new Vector2(3f, 0.7f);
            Assert.AreEqual(SeparationState.Clear, Classify(Vector2.zero, East, b, West, Airliner, Airliner));
            Assert.AreEqual(SeparationState.Warning, Classify(Vector2.zero, East, b, West, Jumbo, Jumbo));
        }

        [Test]
        public void Evaluate_UsesEachAircraftsRadius()
        {
            // A jumbo and an airliner 0.55 apart collide (0.6); two airliners 0.55 apart do not (0.5).
            var positions = new List<Vector2> { Vector2.zero, new Vector2(0.55f, 0f), new Vector2(10f, 0f), new Vector2(10.55f, 0f) };
            var velocities = new List<Vector2> { Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero };
            var radii = new List<float> { Jumbo, Airliner, Airliner, Airliner };
            var states = new SeparationState[4];
            var collisions = new List<Vector2Int>();

            SeparationRules.Evaluate(positions, velocities, radii, states, collisions);

            Assert.AreEqual(SeparationState.Collision, states[0]);
            Assert.AreEqual(SeparationState.Collision, states[1]);
            Assert.AreEqual(SeparationState.Warning, states[2]);
            Assert.AreEqual(SeparationState.Warning, states[3]);
            CollectionAssert.AreEqual(new[] { new Vector2Int(0, 1) }, collisions);
        }

        [Test]
        public void Evaluate_GivesEachAircraftItsWorstState_AndEachCollisionOnce()
        {
            var positions = new List<Vector2> { Vector2.zero, new Vector2(0.3f, 0f), new Vector2(1.2f, 0f), new Vector2(10f, 10f) };
            var velocities = new List<Vector2> { East, West, East, North };
            var radii = new List<float> { Airliner, Airliner, Airliner, Airliner };
            var states = new SeparationState[4];
            var collisions = new List<Vector2Int>();

            SeparationRules.Evaluate(positions, velocities, radii, states, collisions);

            Assert.AreEqual(SeparationState.Collision, states[0]);
            Assert.AreEqual(SeparationState.Collision, states[1]);
            Assert.AreEqual(SeparationState.Warning, states[2], "0.9 from aircraft 1");
            Assert.AreEqual(SeparationState.Clear, states[3]);
            CollectionAssert.AreEqual(new[] { new Vector2Int(0, 1) }, collisions);
        }

        [Test]
        public void Evaluate_OneAircraft_IsClear()
        {
            var states = new SeparationState[1];
            var collisions = new List<Vector2Int> { new Vector2Int(5, 6) };

            SeparationRules.Evaluate(new List<Vector2> { Vector2.zero }, new List<Vector2> { East }, new List<float> { Airliner }, states, collisions);

            Assert.AreEqual(SeparationState.Clear, states[0]);
            Assert.IsEmpty(collisions, "Old results are cleared");
        }

        // ---- Flights: the warning comes before the collision, for every pair ----

        struct FlightResult
        {
            public float FirstWarning;
            public float FirstCollision;
            public float DistanceAtCollision;
        }

        /// <summary>Flies two aircraft straight (no paths) and records when they are first warned and first collide.</summary>
        static FlightResult Fly(Vector2 startA, float headingA, float speedA, float radiusA,
                                Vector2 startB, float headingB, float speedB, float radiusB, float dt = 1f / 60f)
        {
            var bounds = new Rect(-100f, -100f, 200f, 200f);
            var a = new AircraftMotion(startA, headingA, speedA, 200f);
            var b = new AircraftMotion(startB, headingB, speedB, 200f);
            var result = new FlightResult { FirstWarning = -1f, FirstCollision = -1f };

            for (int i = 1; i <= 60 * 30 && result.FirstCollision < 0f; i++)
            {
                a.Step(dt, bounds);
                b.Step(dt, bounds);
                SeparationState state = SeparationRules.Classify(a.Position, a.Velocity, radiusA, b.Position, b.Velocity, radiusB);
                float t = i * dt;
                if (state != SeparationState.Clear && result.FirstWarning < 0f)
                    result.FirstWarning = t;
                if (state == SeparationState.Collision)
                {
                    result.FirstCollision = t;
                    result.DistanceAtCollision = Vector2.Distance(a.Position, b.Position);
                }
            }
            return result;
        }

        static void AssertWarnedThenCollided(FlightResult result, float radiusA, float radiusB, float minimumLeadSeconds)
        {
            Assert.Greater(result.FirstCollision, 0f, "They should collide when nobody steers");
            Assert.Greater(result.FirstWarning, 0f, "They should be warned");
            Assert.GreaterOrEqual(result.FirstCollision - result.FirstWarning, minimumLeadSeconds,
                $"Warning at {result.FirstWarning:F2} s should come at least {minimumLeadSeconds} s before the collision at {result.FirstCollision:F2} s");
            Assert.Less(result.DistanceAtCollision, radiusA + radiusB);
            Assert.Greater(result.DistanceAtCollision, radiusA + radiusB - 0.1f, "Detected as they touch, not after passing through");
        }

        // Speeds at 1x from blueprint 13.1: 0.6 x the kind's multiplier.
        const float AirlinerSpeed = 0.6f;
        const float JumboSpeed = 0.6f * 0.85f;
        const float FighterSpeed = 0.6f * 1.6f;
        const float DroneSpeed = 0.6f * 0.8f;

        [Test]
        public void AirlinerHeadOnAirliner_WarnsWellBefore()
        {
            FlightResult r = Fly(new Vector2(-5f, 0f), 0f, AirlinerSpeed, Airliner, new Vector2(5f, 0f), 180f, AirlinerSpeed, Airliner);
            AssertWarnedThenCollided(r, Airliner, Airliner, 1.5f);
        }

        [Test]
        public void JumboHeadOnDrone_WarnsWellBefore()
        {
            FlightResult r = Fly(new Vector2(-5f, 0.1f), 0f, JumboSpeed, Jumbo, new Vector2(5f, 0f), 180f, DroneSpeed, Drone);
            AssertWarnedThenCollided(r, Jumbo, Drone, 1.5f);
        }

        [Test]
        public void FighterCrossingJumbo_WarnsBefore()
        {
            // Both reach the crossing point (0, 0) at the same moment: the fighter from the left, the jumbo from below.
            float t = 5f;
            FlightResult r = Fly(new Vector2(-FighterSpeed * t, 0f), 0f, FighterSpeed, Fighter,
                                 new Vector2(0f, -JumboSpeed * t), 90f, JumboSpeed, Jumbo);
            AssertWarnedThenCollided(r, Fighter, Jumbo, 1f);
        }

        [TestCase(1f / 60f, 2f)]
        [TestCase(1f / 30f, 2f)] // a dropped frame rate
        [TestCase(1f / 60f, 1f)]
        public void FastFighterCrossingAirliner_IsCaughtAndWarnedBefore(float dt, float gameSpeed)
        {
            // A fighter at 2x game speed (the fastest case) crosses an airliner's track at right angles.
            float fighter = FighterSpeed * gameSpeed;
            float airliner = AirlinerSpeed * gameSpeed;
            float t = 4f;
            FlightResult r = Fly(new Vector2(-fighter * t, 0f), 0f, fighter, Fighter,
                                 new Vector2(0f, -airliner * t), 90f, airliner, Airliner, dt);
            AssertWarnedThenCollided(r, Fighter, Airliner, 0.5f);
        }
    }

    public class SpawnRulesTests
    {
        static readonly List<Vector2> Points = new List<Vector2> { new Vector2(-5f, 3f), new Vector2(5f, 3f), new Vector2(-5f, -3f) };

        [Test]
        public void NoAircraft_UsesTheRequestedPoint()
        {
            Assert.AreEqual(1, SpawnRules.ChooseClearPoint(Points, new List<Vector2>(), 1));
        }

        [Test]
        public void BusyPoint_IsSkipped()
        {
            var aircraft = new List<Vector2> { new Vector2(5.5f, 3f) };
            Assert.AreEqual(2, SpawnRules.ChooseClearPoint(Points, aircraft, 1));
        }

        [Test]
        public void WrapsRoundToTheFirstPoint()
        {
            var aircraft = new List<Vector2> { new Vector2(-5f, -3f) };
            Assert.AreEqual(0, SpawnRules.ChooseClearPoint(Points, aircraft, 2));
            Assert.AreEqual(0, SpawnRules.ChooseClearPoint(Points, aircraft, 5), "Index past the end wraps round");
        }

        [Test]
        public void EveryPointBusy_ReturnsMinusOne()
        {
            var aircraft = new List<Vector2> { new Vector2(-5f, 3f), new Vector2(5f, 3f), new Vector2(-5f, -3f) };
            Assert.AreEqual(-1, SpawnRules.ChooseClearPoint(Points, aircraft, 0));
        }
    }
}
