using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    public enum SeparationState
    {
        Clear = 0,
        Warning = 1,
        Collision = 2,
    }

    /// <summary>
    /// Separation maths for AirspaceSafety. Plain C#, no Unity components, so it can be unit tested.
    ///
    /// Every distance comes from the two aircraft's collision radii (blueprint 14.1, AGREED 19 Nov 2026), so
    /// bigger aircraft need more room. With R = radius A + radius B, two aircraft are in:
    /// - Collision when their centres are closer than R.
    /// - Warning when they are within <see cref="ProximityWarningFactor"/> x R, or when, flying on as they are
    ///   now heading, they would pass within <see cref="PredictedMissFactor"/> x R in the next
    ///   <see cref="LookAheadSeconds"/>. The prediction gives a warning well before a head-on collision.
    /// Two airliners (radius 0.25 each, R = 0.5) give the M1 values: collision 0.5, warning 1.2, predicted 0.6.
    /// </summary>
    public static class SeparationRules
    {
        /// <summary>Always a warning within this many times the summed radii (1.2 for two airliners).</summary>
        public const float ProximityWarningFactor = 2.4f;

        /// <summary>A predicted pass within this many times the summed radii is a warning (0.6 for two airliners).</summary>
        public const float PredictedMissFactor = 1.2f;

        /// <summary>How far ahead the straight-line prediction looks.</summary>
        public const float LookAheadSeconds = 2f;

        public static float CollisionDistance(float radiusA, float radiusB) => radiusA + radiusB;
        public static float ProximityWarningDistance(float radiusA, float radiusB) => ProximityWarningFactor * (radiusA + radiusB);
        public static float PredictedMissDistance(float radiusA, float radiusB) => PredictedMissFactor * (radiusA + radiusB);

        /// <summary>
        /// Radius of the red danger ring drawn around one aircraft, so that two rings touch exactly when the
        /// proximity warning starts.
        /// </summary>
        public static float WarningRingRadius(float radius) => ProximityWarningFactor * radius;

        /// <summary>
        /// Time from now until two aircraft are closest, if both keep their current velocity. Zero if they
        /// are already moving apart or are not moving relative to each other.
        /// </summary>
        public static float TimeOfClosestApproach(Vector2 relativePosition, Vector2 relativeVelocity)
        {
            float speedSquared = relativeVelocity.sqrMagnitude;
            if (speedSquared < 1e-8f)
                return 0f;
            return Mathf.Max(0f, -Vector2.Dot(relativePosition, relativeVelocity) / speedSquared);
        }

        /// <summary>The closest the two aircraft come within the next <paramref name="maxSeconds"/>.</summary>
        public static float ClosestApproachDistance(Vector2 relativePosition, Vector2 relativeVelocity, float maxSeconds)
        {
            float t = Mathf.Min(TimeOfClosestApproach(relativePosition, relativeVelocity), maxSeconds);
            return (relativePosition + relativeVelocity * t).magnitude;
        }

        public static SeparationState Classify(Vector2 positionA, Vector2 velocityA, float radiusA,
                                               Vector2 positionB, Vector2 velocityB, float radiusB)
        {
            Vector2 relativePosition = positionB - positionA;
            float distance = relativePosition.magnitude;

            if (distance < CollisionDistance(radiusA, radiusB))
                return SeparationState.Collision;
            if (distance <= ProximityWarningDistance(radiusA, radiusB))
                return SeparationState.Warning;
            if (ClosestApproachDistance(relativePosition, velocityB - velocityA, LookAheadSeconds) <= PredictedMissDistance(radiusA, radiusB))
                return SeparationState.Warning;
            return SeparationState.Clear;
        }

        /// <summary>
        /// Checks every pair. <paramref name="states"/> gets, for each aircraft, the worst state it is in with
        /// any other. <paramref name="collisions"/> gets each colliding pair once, as (lower index, higher index).
        /// </summary>
        public static void Evaluate(IReadOnlyList<Vector2> positions, IReadOnlyList<Vector2> velocities, IReadOnlyList<float> radii,
                                    SeparationState[] states, List<Vector2Int> collisions)
        {
            int count = positions.Count;
            for (int i = 0; i < count; i++)
                states[i] = SeparationState.Clear;
            collisions.Clear();

            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    SeparationState state = Classify(positions[i], velocities[i], radii[i], positions[j], velocities[j], radii[j]);
                    if (state > states[i]) states[i] = state;
                    if (state > states[j]) states[j] = state;
                    if (state == SeparationState.Collision)
                        collisions.Add(new Vector2Int(i, j));
                }
            }
        }
    }
}
