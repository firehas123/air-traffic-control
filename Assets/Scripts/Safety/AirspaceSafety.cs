using System;
using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// Every frame, after all aircraft have moved, checks the separation of every pair of airborne aircraft
    /// (see <see cref="SeparationRules"/>), using each aircraft kind's collision radius. Aircraft in danger show
    /// a warning ring; colliding aircraft crash.
    /// Aircraft rolling out on the runway, or already crashed, are not checked.
    /// </summary>
    public sealed class AirspaceSafety : MonoBehaviour
    {
        readonly List<AircraftController> airborne = new List<AircraftController>();
        readonly List<Vector2> positions = new List<Vector2>();
        readonly List<Vector2> velocities = new List<Vector2>();
        readonly List<float> radii = new List<float>();
        readonly List<Vector2Int> collisions = new List<Vector2Int>();
        SeparationState[] states = new SeparationState[8];

        /// <summary>Raised once for each pair of aircraft that collide.</summary>
        public event Action<AircraftController, AircraftController> Collided;

        void LateUpdate()
        {
            airborne.Clear();
            positions.Clear();
            velocities.Clear();
            radii.Clear();
            foreach (AircraftController aircraft in AircraftController.Active)
            {
                if (!aircraft.IsAirborne)
                    continue;
                airborne.Add(aircraft);
                positions.Add(aircraft.Motion.Position);
                velocities.Add(aircraft.Motion.Velocity);
                radii.Add(aircraft.CollisionRadius);
            }

            if (states.Length < airborne.Count)
                states = new SeparationState[Mathf.NextPowerOfTwo(airborne.Count)];

            SeparationRules.Evaluate(positions, velocities, radii, states, collisions);

            for (int i = 0; i < airborne.Count; i++)
                airborne[i].SetWarning(states[i] == SeparationState.Warning);

            foreach (Vector2Int pair in collisions)
            {
                AircraftController a = airborne[pair.x];
                AircraftController b = airborne[pair.y];
                a.Crash();
                b.Crash();
                Collided?.Invoke(a, b);
            }
        }
    }
}
