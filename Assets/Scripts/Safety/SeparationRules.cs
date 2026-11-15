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
    /// Two aircraft are in:
    /// - Collision when their centres are within <see cref="CollisionDistance"/> (the planes touch).
    /// - Warning when they are within <see cref="ProximityWarningDistance"/>, or when, flying on as they
    ///   are now heading, they would pass within <see cref="PredictedMissDistance"/> in the next
    ///   <see cref="LookAheadSeconds"/>. The prediction gives a warning well before a head-on collision.
    /// </summary>
    public static class SeparationRules
    {
        /// <summary>The planes touch: about the wingspan of the placeholder plane.</summary>
        public const float CollisionDistance = 0.5f;

        /// <summary>Closer than this is always a warning (about 2.4 cm on a 12.9" iPad).</summary>
        public const float ProximityWarningDistance = 1.2f;

        /// <summary>A predicted pass closer than this is a warning.</summary>
        public const float PredictedMissDistance = 0.6f;

        /// <summary>How far ahead the straight-line prediction looks.</summary>
        public const float LookAheadSeconds = 2f;

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

        public static SeparationState Classify(Vector2 positionA, Vector2 velocityA, Vector2 positionB, Vector2 velocityB)
        {
            Vector2 relativePosition = positionB - positionA;
            float distance = relativePosition.magnitude;

            if (distance <= CollisionDistance)
                return SeparationState.Collision;
            if (distance <= ProximityWarningDistance)
                return SeparationState.Warning;
            if (ClosestApproachDistance(relativePosition, velocityB - velocityA, LookAheadSeconds) <= PredictedMissDistance)
                return SeparationState.Warning;
            return SeparationState.Clear;
        }

        /// <summary>
        /// Checks every pair. <paramref name="states"/> gets, for each aircraft, the worst state it is in with
        /// any other. <paramref name="collisions"/> gets each colliding pair once, as (lower index, higher index).
        /// </summary>
        public static void Evaluate(IReadOnlyList<Vector2> positions, IReadOnlyList<Vector2> velocities,
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
                    SeparationState state = Classify(positions[i], velocities[i], positions[j], velocities[j]);
                    if (state > states[i]) states[i] = state;
                    if (state > states[j]) states[j] = state;
                    if (state == SeparationState.Collision)
                        collisions.Add(new Vector2Int(i, j));
                }
            }
        }
    }
}
