using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AirTrafficControl.Tests
{
    public class SeparationTests
    {
        const float Speed = 1.2f;
        static readonly Vector2 East = new Vector2(Speed, 0f);
        static readonly Vector2 West = new Vector2(-Speed, 0f);
        static readonly Vector2 North = new Vector2(0f, Speed);

        [Test]
        public void ClosestApproach_HeadOn_MeetsAtTheMidpointTime()
        {
            // B is 6 ahead of A, closing at 2.4: they meet in 2.5 s.
            Vector2 relativePosition = new Vector2(6f, 0f);
            Vector2 relativeVelocity = West - East;

            Assert.AreEqual(2.5f, SeparationRules.TimeOfClosestApproach(relativePosition, relativeVelocity), 1e-4f);
            Assert.AreEqual(0f, SeparationRules.ClosestApproachDistance(relativePosition, relativeVelocity, 10f), 1e-4f);
        }

        [Test]
        public void ClosestApproach_IsLimitedToTheLookAheadTime()
        {
            Vector2 relativePosition = new Vector2(6f, 0f);
            Vector2 relativeVelocity = West - East;

            // After 1 s they are still 3.6 apart.
            Assert.AreEqual(3.6f, SeparationRules.ClosestApproachDistance(relativePosition, relativeVelocity, 1f), 1e-4f);
        }

        [Test]
        public void ClosestApproach_MovingApart_IsNow()
        {
            Vector2 relativePosition = new Vector2(2f, 0f);
            Vector2 relativeVelocity = East - West; // B moving away from A

            Assert.AreEqual(0f, SeparationRules.TimeOfClosestApproach(relativePosition, relativeVelocity));
            Assert.AreEqual(2f, SeparationRules.ClosestApproachDistance(relativePosition, relativeVelocity, 5f), 1e-4f);
        }

        [Test]
        public void ClosestApproach_SameVelocity_StaysTheSame()
        {
            Assert.AreEqual(0f, SeparationRules.TimeOfClosestApproach(new Vector2(1.5f, 0f), Vector2.zero));
            Assert.AreEqual(1.5f, SeparationRules.ClosestApproachDistance(new Vector2(1.5f, 0f), Vector2.zero, 5f), 1e-4f);
        }

        [Test]
        public void Classify_FarApartAndNotConverging_IsClear()
        {
            Assert.AreEqual(SeparationState.Clear,
                SeparationRules.Classify(Vector2.zero, East, new Vector2(0f, 3f), East));
        }

        [Test]
        public void Classify_WithinProximityDistance_IsWarning()
        {
            Assert.AreEqual(SeparationState.Warning,
                SeparationRules.Classify(Vector2.zero, East, new Vector2(0f, 1.1f), East));
        }

        [Test]
        public void Classify_WithinCollisionDistance_IsCollision()
        {
            Assert.AreEqual(SeparationState.Collision,
                SeparationRules.Classify(Vector2.zero, East, new Vector2(0.4f, 0.2f), West));
        }

        [Test]
        public void Classify_HeadOnAndFarApart_WarnsByPrediction()
        {
            // 4 apart, closing at 2.4: they would meet in 1.7 s, inside the 2 s look-ahead.
            Assert.AreEqual(SeparationState.Warning,
                SeparationRules.Classify(Vector2.zero, East, new Vector2(4f, 0f), West));
        }

        [Test]
        public void Classify_CrossingButPassingWellApart_IsClear()
        {
            // B crosses A's track 2 units behind A.
            Assert.AreEqual(SeparationState.Clear,
                SeparationRules.Classify(Vector2.zero, East, new Vector2(-2f, -2f), North));
        }

        [Test]
        public void Classify_MovingApart_IsClear()
        {
            Assert.AreEqual(SeparationState.Clear,
                SeparationRules.Classify(Vector2.zero, West, new Vector2(1.5f, 0f), East));
        }

        [Test]
        public void Evaluate_GivesEachAircraftItsWorstState_AndEachCollisionOnce()
        {
            var positions = new List<Vector2> { Vector2.zero, new Vector2(0.3f, 0f), new Vector2(1.2f, 0f), new Vector2(10f, 10f) };
            var velocities = new List<Vector2> { East, West, East, North };
            var states = new SeparationState[4];
            var collisions = new List<Vector2Int>();

            SeparationRules.Evaluate(positions, velocities, states, collisions);

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

            SeparationRules.Evaluate(new List<Vector2> { Vector2.zero }, new List<Vector2> { East }, states, collisions);

            Assert.AreEqual(SeparationState.Clear, states[0]);
            Assert.IsEmpty(collisions, "Old results are cleared");
        }

        [Test]
        public void HeadOnFlight_WarnsWellBeforeTheCollision()
        {
            var bounds = new Rect(-100f, -100f, 200f, 200f);
            var a = new AircraftMotion(new Vector2(-5f, 0f), 0f, Speed, 200f);
            var b = new AircraftMotion(new Vector2(5f, 0f), 180f, Speed, 200f);
            float firstWarning = -1f;
            float firstCollision = -1f;
            const float dt = 1f / 60f;

            for (int i = 0; i < 60 * 10 && firstCollision < 0f; i++)
            {
                a.Step(dt, bounds);
                b.Step(dt, bounds);
                SeparationState state = SeparationRules.Classify(a.Position, a.Velocity, b.Position, b.Velocity);
                float t = (i + 1) * dt;
                if (state == SeparationState.Warning && firstWarning < 0f) firstWarning = t;
                if (state == SeparationState.Collision) firstCollision = t;
            }

            Assert.Greater(firstWarning, 0f, "Should warn");
            Assert.Greater(firstCollision, 0f, "Should collide when nobody steers");
            Assert.GreaterOrEqual(firstCollision - firstWarning, 1.5f, "Warning gives at least 1.5 s to react");
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
