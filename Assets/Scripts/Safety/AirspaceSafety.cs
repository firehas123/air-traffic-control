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
        readonly HashSet<AircraftController> warnedLastFrame = new HashSet<AircraftController>();
        readonly HashSet<AircraftController> warnedNow = new HashSet<AircraftController>();
        readonly List<AircraftController> onRunway = new List<AircraftController>();
        readonly List<RunwayOccupant> occupants = new List<RunwayOccupant>();
        readonly List<Vector2Int> runwayCollisions = new List<Vector2Int>();
        readonly List<int> usedRunways = new List<int>();
        bool[] busy = new bool[0];
        LandingSystem landing;

        /// <summary>Lets the busy-runway rule (blueprint 15.4) flash the runway's edge lights.</summary>
        public void Initialise(LandingSystem landingSystem)
        {
            landing = landingSystem;
        }

        /// <summary>True while an aircraft lands, lines up, takes off or crosses the runway.</summary>
        public bool RunwayBusy { get; private set; }

        /// <summary>Raised once for each pair of aircraft that collide.</summary>
        public event Action<AircraftController, AircraftController> Collided;

        /// <summary>Raised when an aircraft that was not in danger last frame is now (for sound and the 3-star rule).</summary>
        public event Action<AircraftController> WarningStarted;

        /// <summary>The smallest edge-to-edge gap between two airborne aircraft this frame (infinity with fewer than two).</summary>
        public float ClosestGapThisFrame { get; private set; } = float.PositiveInfinity;

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
            ClosestGapThisFrame = LevelResult.ClosestGap(positions, radii);

            warnedNow.Clear();
            for (int i = 0; i < airborne.Count; i++)
            {
                bool warning = states[i] == SeparationState.Warning;
                airborne[i].SetWarning(warning);
                if (warning)
                {
                    warnedNow.Add(airborne[i]);
                    if (!warnedLastFrame.Contains(airborne[i]))
                        WarningStarted?.Invoke(airborne[i]);
                }
            }
            warnedLastFrame.Clear();
            warnedLastFrame.UnionWith(warnedNow);

            CheckRunway();

            foreach (Vector2Int pair in collisions)
            {
                AircraftController a = airborne[pair.x];
                AircraftController b = airborne[pair.y];
                a.Crash();
                b.Crash();
                Collided?.Invoke(a, b);
            }
        }

        // Busy runway (blueprint 15.4, 16.1): a landing or a take-off that meets another aircraft on a runway or where two
        // runways cross is a collision. Taxiing aircraft never collide: they are not on the runway and they queue.
        void CheckRunway()
        {
            onRunway.Clear();
            occupants.Clear();
            usedRunways.Clear();
            foreach (AircraftController aircraft in AircraftController.Active)
            {
                RunwayUse use = aircraft.RunwayUse;
                if (use == RunwayUse.None)
                    continue;
                onRunway.Add(aircraft);
                occupants.Add(new RunwayOccupant(aircraft.transform.position, aircraft.CollisionRadius, use));
                usedRunways.Add(aircraft.RunwayInUse);
            }

            RunwayBusy = RunwayRules.IsBusy(occupants);
            if (landing != null)
            {
                // Each runway's edge lights flash while that runway is busy; an aircraft in an intersection makes both busy.
                if (busy.Length < landing.Runways.Count)
                    busy = new bool[landing.Runways.Count];
                RunwayRules.BusyRunways(landing.Runways, occupants, usedRunways, busy);
                for (int r = 0; r < landing.Runways.Count; r++)
                    landing.SetRunwayBusy(r, busy[r]);
            }

            RunwayRules.FindCollisions(occupants, runwayCollisions);
            foreach (Vector2Int pair in runwayCollisions)
            {
                AircraftController a = onRunway[pair.x];
                AircraftController b = onRunway[pair.y];
                a.Crash();
                b.Crash();
                Collided?.Invoke(a, b);
            }
        }
    }
}
