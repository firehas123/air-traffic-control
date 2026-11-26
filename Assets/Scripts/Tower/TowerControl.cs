using System;
using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// Tower mode (blueprint 16.1, phase 3), the game side. No drawing: arrivals fly fixed approach routes from the screen
    /// edge onto a runway's final, and the player taps an aircraft for big buttons. In the air: Land on runway A/B/C (or
    /// the helipad or balloon field), Go around, Hold, Slow, Fast. On the ground: Taxi to a runway from the stand, Cross at a
    /// hold line, Take off at the runway end, and Taxi to stand after landing. An arrival that reaches the runway without a
    /// clearance goes around by itself. Taxiing aircraft queue and never collide.
    ///
    /// Input stays in <see cref="InputAdapter"/> and the buttons in <see cref="CommandMenu"/>; the paths come from
    /// <see cref="TowerFlightPlanner"/> and the taxi routes from <see cref="TaxiNetwork"/>.
    /// </summary>
    public sealed class TowerControl : MonoBehaviour
    {
        /// <summary>Generous tap target around an aircraft.</summary>
        public const float TapRadius = 0.8f;

        public enum FlightState { Approaching, Cleared, GoingAround, Holding }

        /// <summary>What an arrival in the air is doing (read by the tests and the first-time help).</summary>
        public sealed class Flight
        {
            public FlightState state;
            public ApproachTarget target;
            public int holdFix = -1;
            public float plannedHeading;
        }

        LevelData level;
        TowerSettings settings;
        InputAdapter input;
        CommandMenu menu;
        LandingSystem landing;
        Camera worldCamera;
        TaxiNetwork network;
        readonly Dictionary<AircraftController, Flight> flights = new Dictionary<AircraftController, Flight>();
        readonly List<AircraftController> scratch = new List<AircraftController>();
        readonly List<Vector2> path = new List<Vector2>();
        List<CommandOption> openOptions = new List<CommandOption>();

        /// <summary>Raised when buttons open for an aircraft, with the options shown (for first-time help).</summary>
        public event Action<AircraftController, IReadOnlyList<CommandOption>> MenuOpened;

        /// <summary>Raised when a command has been given.</summary>
        public event Action<AircraftController, AircraftCommand> CommandGiven;

        /// <summary>Raised when an arrival goes around by itself, without a clearance (for first-time help).</summary>
        public event Action<AircraftController> MissedApproach;

        public bool IsMenuOpen => menu != null && menu.IsOpen;
        public TaxiNetwork Network => network;
        public TowerSettings Settings => settings;

        public Flight FlightOf(AircraftController aircraft) => aircraft != null && flights.TryGetValue(aircraft, out Flight f) ? f : null;

        public void Initialise(LevelData levelData, InputAdapter inputAdapter, CommandMenu commandMenu, LandingSystem landingSystem, Camera camera)
        {
            level = levelData;
            settings = level.tower ?? new TowerSettings();
            input = inputAdapter;
            menu = commandMenu;
            landing = landingSystem;
            worldCamera = camera;
            network = level.layout != null ? TaxiNetwork.Build(level.layout) : new TaxiNetwork();

            if (input != null)
            {
                input.Tapped += OnTapped;
                input.DragStarted += OnDragStarted;
            }
            if (menu != null)
                menu.OptionChosen += OnOptionChosen;
        }

        void OnDestroy()
        {
            if (input != null)
            {
                input.Tapped -= OnTapped;
                input.DragStarted -= OnDragStarted;
            }
            if (menu != null)
                menu.OptionChosen -= OnOptionChosen;
        }

        // ---------------------------------------------------------------- Arrivals in the air

        /// <summary>A new arrival at the screen edge: it flies the fixed approach route to <paramref name="approach"/>'s runway end, with no clearance yet.</summary>
        public void AddArrival(AircraftController aircraft, TowerApproach approach)
        {
            aircraft.LandingAllowed = false;
            aircraft.TowerTouchdown = OnTouchdown;
            aircraft.ParkedSeconds = settings.parkedSeconds;
            var flight = new Flight { state = FlightState.Approaching };
            flights[aircraft] = flight;
            int runway = approach != null ? approach.runway : -1;
            flight.target = DefaultTarget(aircraft, runway);
            FlyApproach(aircraft, flight, false);
        }

        /// <summary>The usable runway nearest to fly to (preferring <paramref name="preferred"/>), or the aircraft's pad.</summary>
        ApproachTarget DefaultTarget(AircraftController aircraft, int preferred)
        {
            Vector2 at = aircraft.Motion.Position;
            if (aircraft.Type.landsOn != LandingZoneKind.Runway)
            {
                LandingPad? pad = NearestPad(aircraft.Type.landsOn, at);
                return pad.HasValue ? TowerFlightPlanner.ForPad(pad.Value, at) : TowerFlightPlanner.ForRunway(landing.Runways[0], 0, settings.finalLength);
            }
            if (preferred >= 0 && preferred < landing.Runways.Count && CanLandOn(preferred))
                return RunwayTarget(preferred);
            int best = -1;
            float bestLength = float.MaxValue;
            float radius = settings.TurnRadius(aircraft.Motion.Speed);
            for (int r = 0; r < landing.Runways.Count; r++)
            {
                if (!CanLandOn(r))
                    continue;
                ApproachTarget t = RunwayTarget(r);
                float length = Dubins.Length(at, aircraft.Motion.HeadingDegrees, t.Fix, t.HeadingDegrees, radius);
                if (length < bestLength)
                {
                    bestLength = length;
                    best = r;
                }
            }
            if (best < 0)
            {
                // Every runway is closed: fly towards the preferred (or first allowed) one; it goes around until one opens.
                best = preferred >= 0 && preferred < landing.Runways.Count ? preferred : 0;
                for (int r = 0; r < landing.Runways.Count && !settings.AllowsRunway(landing.Runways[best]); r++)
                    best = r;
            }
            return RunwayTarget(best);
        }

        ApproachTarget RunwayTarget(int runway) => TowerFlightPlanner.ForRunway(landing.Runways[runway], runway, settings.finalLength);

        LandingPad? NearestPad(LandingZoneKind kind, Vector2 from)
        {
            LandingPad? best = null;
            float bestDistance = float.MaxValue;
            foreach (LandingPad pad in landing.Pads)
            {
                if (pad.Kind != kind)
                    continue;
                float d = Vector2.Distance(pad.Centre, from);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = pad;
                }
            }
            return best;
        }

        /// <summary>True when a runway can take a landing now: open, and allowed by the map (Mountain Valley: along the valley).</summary>
        public bool CanLandOn(int runway) =>
            runway >= 0 && runway < landing.Runways.Count && landing.IsOpen(runway) && settings.AllowsRunway(landing.Runways[runway]);

        void FlyApproach(AircraftController aircraft, Flight flight, bool cleared)
        {
            float radius = settings.TurnRadius(aircraft.Motion.Speed);
            SetPath(aircraft, TowerFlightPlanner.Approach(aircraft.Motion.Position, aircraft.Motion.HeadingDegrees, flight.target, radius, cleared));
            flight.plannedHeading = flight.target.HeadingDegrees;
            SetCleared(aircraft, flight, cleared);
        }

        void SetCleared(AircraftController aircraft, Flight flight, bool cleared)
        {
            aircraft.LandingAllowed = cleared;
            aircraft.SetClearedToLand(cleared);
            aircraft.SetPathFaint(!cleared);
            if (cleared)
                flight.state = FlightState.Cleared;
        }

        void SetPath(AircraftController aircraft, List<Vector2> points)
        {
            aircraft.Motion.BeginPath();
            for (int i = 1; i < points.Count; i++)
                aircraft.Motion.AppendWaypoint(points[i]);
        }

        /// <summary>Land: on runway <paramref name="runway"/>, or (for -1) on the aircraft's helipad or balloon field.</summary>
        public bool Land(AircraftController aircraft, int runway)
        {
            Flight flight = FlightOf(aircraft);
            if (flight == null || !aircraft.IsControllableInAir)
                return false;
            ApproachTarget target;
            if (aircraft.Type.landsOn == LandingZoneKind.Runway)
            {
                if (!CanLandOn(runway))
                    return false;
                target = RunwayTarget(runway);
            }
            else
            {
                LandingPad? pad = NearestPad(aircraft.Type.landsOn, aircraft.Motion.Position);
                if (!pad.HasValue)
                    return false;
                // Already lined up on its pad approach: keep it; otherwise straight at the pad from here.
                target = flight.target.Kind == pad.Value.Kind && !flight.target.IsRunway && flight.state != FlightState.Holding
                    ? flight.target : TowerFlightPlanner.ForPad(pad.Value, aircraft.Motion.Position);
            }

            // Cleared for the runway it is already flying to: the path stays as it is and runs on to the touchdown point.
            bool sameRoute = aircraft.Motion.HasPath && flight.state != FlightState.Holding
                             && SameTarget(flight.target, target) && EndsAt(aircraft, target.DecisionPoint);
            flight.target = target;
            flight.holdFix = -1;
            if (sameRoute)
            {
                aircraft.Motion.AppendWaypoint(target.Touchdown);
                flight.plannedHeading = target.HeadingDegrees;
                SetCleared(aircraft, flight, true);
            }
            else
            {
                FlyApproach(aircraft, flight, true);
            }
            return true;
        }

        static bool SameTarget(ApproachTarget a, ApproachTarget b) =>
            a.Runway == b.Runway && a.Kind == b.Kind && Vector2.Distance(a.DecisionPoint, b.DecisionPoint) < 1e-3f
            && Mathf.Abs(Mathf.DeltaAngle(a.HeadingDegrees, b.HeadingDegrees)) < 0.5f;

        static bool EndsAt(AircraftController aircraft, Vector2 point)
        {
            IReadOnlyList<Vector2> p = aircraft.Motion.Path;
            return p.Count > 0 && Vector2.Distance(p[p.Count - 1], point) < 1e-3f;
        }

        /// <summary>Go around: climbs away, loops and rejoins the approach about <see cref="TowerSettings.goAroundSeconds"/> later, without a clearance.</summary>
        public bool GoAround(AircraftController aircraft)
        {
            Flight flight = FlightOf(aircraft);
            if (flight == null || !aircraft.IsControllableInAir)
                return false;
            StartGoAround(aircraft, flight);
            return true;
        }

        void StartGoAround(AircraftController aircraft, Flight flight)
        {
            // Rejoin the same runway if it can still take landings, else the best one; a pad kind its pad.
            int runway = flight.target.IsRunway ? flight.target.Runway : -1;
            flight.target = DefaultTarget(aircraft, runway);
            if (!flight.target.IsRunway)
            {
                LandingPad? pad = NearestPad(aircraft.Type.landsOn, aircraft.Motion.Position);
                if (pad.HasValue)
                    flight.target = TowerFlightPlanner.ForPad(pad.Value, aircraft.Motion.Position + aircraft.Motion.HeadingVector * 2f);
            }
            float radius = settings.TurnRadius(aircraft.Motion.Speed);
            SetPath(aircraft, TowerFlightPlanner.GoAround(aircraft.Motion.Position, aircraft.Motion.HeadingDegrees, aircraft.Motion.Speed,
                                                          flight.target, radius, CameraFraming.CurrentView(worldCamera), settings.goAroundSeconds));
            flight.state = FlightState.GoingAround;
            flight.holdFix = -1;
            flight.plannedHeading = flight.target.HeadingDegrees;
            SetCleared(aircraft, flight, false);
        }

        /// <summary>Hold: circles at a free hold fix near the edge until it is given a new order.</summary>
        public bool Hold(AircraftController aircraft)
        {
            Flight flight = FlightOf(aircraft);
            if (flight == null || !aircraft.IsControllableInAir)
                return false;
            if (flight.state == FlightState.Holding)
                return true;
            flight.holdFix = ChooseHoldFix(aircraft);
            flight.state = FlightState.Holding;
            SetCleared(aircraft, flight, false);
            float radius = settings.TurnRadius(aircraft.Motion.Speed);
            SetPath(aircraft, TowerFlightPlanner.Hold(aircraft.Motion.Position, aircraft.Motion.HeadingDegrees, HoldFixPosition(flight.holdFix),
                                                      HoldRadius(aircraft), radius));
            return true;
        }

        float HoldRadius(AircraftController aircraft) => Mathf.Max(settings.holdRadius, settings.TurnRadius(aircraft.Motion.Speed));

        // The nearest hold fix no other holding aircraft uses (all used: the nearest).
        int ChooseHoldFix(AircraftController aircraft)
        {
            int best = -1, bestFree = -1;
            float bestDistance = float.MaxValue, bestFreeDistance = float.MaxValue;
            for (int i = 0; i < settings.holdFixes.Count; i++)
            {
                float d = Vector2.Distance(HoldFixPosition(i), aircraft.Motion.Position);
                bool used = false;
                foreach (KeyValuePair<AircraftController, Flight> pair in flights)
                    if (pair.Key != aircraft && pair.Key != null && pair.Value.state == FlightState.Holding && pair.Value.holdFix == i)
                        used = true;
                if (d < bestDistance) { bestDistance = d; best = i; }
                if (!used && d < bestFreeDistance) { bestFreeDistance = d; bestFree = i; }
            }
            return bestFree >= 0 ? bestFree : best;
        }

        /// <summary>A hold fix kept inside the current screen, far enough in for its circle.</summary>
        public Vector2 HoldFixPosition(int index)
        {
            Vector2 fix = index >= 0 && index < settings.holdFixes.Count ? settings.holdFixes[index] : Vector2.zero;
            if (worldCamera == null)
                return fix;
            Rect view = CameraFraming.CurrentView(worldCamera);
            float inset = settings.holdRadius + 0.45f;
            float top = view.yMax - TowerModeSetup.TopBarHeight - settings.holdRadius - 0.1f; // below the top bar of buttons
            return new Vector2(Mathf.Clamp(fix.x, view.xMin + inset, Mathf.Max(view.xMin + inset, view.xMax - inset)),
                               Mathf.Clamp(fix.y, view.yMin + inset, Mathf.Max(view.yMin + inset, top)));
        }

        bool ToggleSpeed(AircraftController aircraft, AircraftSpeed speed)
        {
            if (!aircraft.IsControllableInAir)
                return false;
            aircraft.ToggleSpeed(speed);
            return true;
        }

        // Each frame: aircraft that reached the end of their path without landing go around (or keep holding); a
        // clearance lost to the wind or a closing runway turns into a go-around; uncleared approaches follow the wind.
        void UpdateFlights()
        {
            scratch.Clear();
            scratch.AddRange(flights.Keys);
            foreach (AircraftController aircraft in scratch)
            {
                if (aircraft == null || aircraft.IsCrashed || !aircraft.IsAirborne)
                {
                    flights.Remove(aircraft);
                    continue;
                }
                Flight flight = flights[aircraft];
                if (flight.state == FlightState.Cleared && !aircraft.IsClearedToLand && !aircraft.IsInWindGrace)
                {
                    StartGoAround(aircraft, flight); // the runway closed or the wind moved its end
                    continue;
                }
                if (!aircraft.Motion.HasPath)
                {
                    if (flight.state == FlightState.Holding)
                    {
                        Hold(aircraft);
                    }
                    else
                    {
                        StartGoAround(aircraft, flight);
                        MissedApproach?.Invoke(aircraft);
                    }
                    continue;
                }
                if (flight.state == FlightState.Holding)
                {
                    if (aircraft.Motion.RemainingPathLength < TowerFlightPlanner.HoldCircleLength(HoldRadius(aircraft)) * 0.5f)
                    {
                        // Another circle, as a fresh path from where it is (the path list never grows without end).
                        path.Clear();
                        for (int i = 0; i < aircraft.Motion.RemainingWaypointCount; i++)
                            path.Add(aircraft.Motion.GetRemainingWaypoint(i));
                        path.Insert(0, aircraft.Motion.Position);
                        TowerFlightPlanner.AppendHoldCircle(path, HoldFixPosition(flight.holdFix), HoldRadius(aircraft));
                        SetPath(aircraft, new List<Vector2>(path));
                    }
                    continue;
                }
                // An approach without a clearance follows its runway: the wind moved the end, or it closed.
                if (flight.state != FlightState.Cleared && flight.target.IsRunway)
                {
                    Runway now = landing.Runways[flight.target.Runway];
                    bool moved = Mathf.Abs(Mathf.DeltaAngle(now.LandingHeadingDegrees, flight.plannedHeading)) > 1f;
                    if (moved && flight.state == FlightState.Approaching)
                    {
                        flight.target = DefaultTarget(aircraft, flight.target.Runway);
                        FlyApproach(aircraft, flight, false);
                    }
                    else if (moved)
                    {
                        StartGoAround(aircraft, flight);
                    }
                }
                // A go-around that has rejoined its final counts as an approach again.
                if (flight.state == FlightState.GoingAround && TowerFlightPlanner.IsEstablished(aircraft.Motion.Position, aircraft.Motion.HeadingDegrees, flight.target))
                    flight.state = FlightState.Approaching;
            }
        }

        // ---------------------------------------------------------------- On the ground

        // Touchdown on a runway: roll out (longer on Fast, sliding on ice), then leave at the next exit ahead.
        GroundMovement OnTouchdown(AircraftController aircraft, Touchdown touchdown)
        {
            flights.Remove(aircraft);
            Runway runway = touchdown.Runway;
            int index = landing.IndexOf(runway);
            float speed = aircraft.Motion.Speed;
            float normal = aircraft.Motion.Speed / Mathf.Max(0.01f, SpeedRules.Factor(aircraft.Type, aircraft.SpeedSetting));
            float roll = settings.rollDistance * (normal > 0f ? speed / normal : 1f) * IceRules.RollFactor(aircraft.Ice, aircraft.SpeedSetting);
            Vector2 at = aircraft.Motion.Position;
            float touchdownAlong = Vector2.Dot(at - runway.Threshold, runway.Direction);
            float stopAlong = Mathf.Min(touchdownAlong + roll, runway.Length - 0.3f);

            List<Vector2> exit = ExitPath(index, runway, stopAlong, out int exitNode, out float rollEnd);
            float stop = aircraft.IsSliding ? settings.slideStopSeconds : 0f;
            return GroundMovement.Landed(at, runway, index, speed, Mathf.Max(0.1f, rollEnd - touchdownAlong), exit, exitNode,
                                         settings.exitSpeed, settings.taxiSpeed, stop);
        }

        /// <summary>The way off a runway after the landing roll: see <see cref="TaxiNetwork.ExitPath"/>.</summary>
        public List<Vector2> ExitPath(int index, Runway runway, float stopAlong, out int exitNode, out float rollEnd) =>
            network.ExitPath(index, runway, stopAlong, OccupiedNode, out exitNode, out rollEnd);

        bool OccupiedNode(int node)
        {
            foreach (AircraftController aircraft in AircraftController.Active)
                if (aircraft.Ground != null && Vector2.Distance(aircraft.Ground.Position, network.Nodes[node].position) < 0.3f)
                    return true;
            return false;
        }

        /// <summary>The first stand no aircraft is parked at or heading for, or -1.</summary>
        public int FreeStand()
        {
            for (int s = 0; s < network.StandCount; s++)
            {
                bool taken = false;
                foreach (AircraftController aircraft in AircraftController.Active)
                    if (aircraft.Ground != null && aircraft.Ground.Stand == s && !aircraft.IsCrashed)
                        taken = true;
                if (!taken)
                    return s;
            }
            return -1;
        }

        /// <summary>A departure parked at a free stand, waiting for Taxi. Null when every stand is taken.</summary>
        public AircraftController SpawnDeparture(AircraftType kind, AircraftSettings aircraftSettings, Material material)
        {
            int stand = FreeStand();
            if (stand < 0 || level.layout == null)
                return null;
            PictureStand painted = level.layout.stands[stand];
            Vector2 position = network.Nodes[network.StandNode(stand)].position;
            float airSpeed = aircraftSettings.speed * kind.speedMultiplier;
            GroundMovement ground = GroundMovement.AtStand(position, painted.headingDegrees, stand, network.StandNode(stand), settings.taxiSpeed, airSpeed);
            var motion = new AircraftMotion(position, painted.headingDegrees, airSpeed, aircraftSettings.turnRate);
            return AircraftController.SpawnAtStand(kind, motion, ground, worldCamera, landing, material, aircraftSettings.colors, aircraftSettings.edgeMargin);
        }

        /// <summary>Which end of every runway is active now: true for end B (the wind has reversed the runways).</summary>
        bool ActiveEndB => landing.IsReversed;

        /// <summary>Runways a ground aircraft at <paramref name="from"/> can be sent to: a reachable hold line at the runway's active end.</summary>
        public List<RunwayChoice> TaxiChoices(GroundMovement ground)
        {
            var list = new List<RunwayChoice>();
            int from = ground.NodeAhead;
            for (int r = 0; r < landing.Runways.Count; r++)
            {
                List<int> holds = network.EndHoldNodes(r, ActiveEndB);
                bool reachable = holds.Count > 0 && from >= 0 && network.FindRoute(from, holds) != null;
                bool here = ground.Phase == GroundPhase.HoldingShort && ground.TargetRunway == r && ground.TargetEndB == ActiveEndB;
                list.Add(new RunwayChoice(r, reachable && !here, landing.IsOpen(r)));
            }
            return list;
        }

        /// <summary>Taxi: from the stand (or a hold line) to the hold line of runway <paramref name="runway"/>'s active end.</summary>
        public bool TaxiTo(AircraftController aircraft, int runway)
        {
            GroundMovement ground = aircraft.Ground;
            if (ground == null || ground.IsArrival)
                return false;
            List<int> holds = network.EndHoldNodes(runway, ActiveEndB);
            TaxiRoute route = network.FindRoute(ground.NodeAhead, holds);
            if (route == null || !ground.FollowRoute(route, GroundPhase.HoldingShort))
                return false;
            ground.Stand = -1; // the stand is free once it leaves
            ground.TargetRunway = runway;
            ground.TargetEndB = ActiveEndB;
            aircraft.RefreshStatus();
            return true;
        }

        public bool Cross(AircraftController aircraft)
        {
            bool done = aircraft.Ground != null && aircraft.Ground.Cross();
            aircraft.RefreshStatus();
            return done;
        }

        /// <summary>True when the departure waits at the hold line of its runway's active end and the runway is open.</summary>
        public bool CanTakeOff(GroundMovement ground) =>
            ground != null && ground.Phase == GroundPhase.HoldingShort && ground.TargetRunway >= 0
            && ground.TargetEndB == ActiveEndB && landing.IsOpen(ground.TargetRunway);

        public bool TakeOff(AircraftController aircraft)
        {
            GroundMovement ground = aircraft.Ground;
            if (!CanTakeOff(ground))
                return false;
            Runway runway = landing.Runways[ground.TargetRunway];
            Vector2 lineUp = GroundMovement.LineUpPoint(runway, ground.Position);
            bool done = ground.TakeOff(runway, ground.TargetRunway, lineUp, GroundMovement.LiftOffDistance(runway, lineUp));
            aircraft.RefreshStatus();
            return done;
        }

        /// <summary>Taxi to stand: an arrival waiting clear of the runway taxis to a free stand.</summary>
        public bool TaxiToStand(AircraftController aircraft)
        {
            GroundMovement ground = aircraft.Ground;
            if (ground == null || ground.Phase != GroundPhase.WaitingForStand)
                return false;
            int stand = FreeStand();
            if (stand < 0)
                return false;
            TaxiRoute route = network.FindRoute(ground.NodeAhead, network.StandNode(stand));
            if (route == null)
            {
                // That stand cannot be reached from here: try every free one.
                var free = new List<int>();
                for (int s = 0; s < network.StandCount; s++)
                {
                    bool taken = false;
                    foreach (AircraftController other in AircraftController.Active)
                        if (other.Ground != null && other.Ground.Stand == s)
                            taken = true;
                    if (!taken)
                        free.Add(network.StandNode(s));
                }
                route = network.FindRoute(ground.NodeAhead, free);
                if (route == null)
                    return false;
                stand = network.Nodes[route.LastNode].stand;
            }
            if (!ground.FollowRoute(route, GroundPhase.Parked))
                return false;
            ground.Stand = stand;
            aircraft.RefreshStatus();
            return true;
        }

        // Each frame: departures follow the wind to the other end of their runway; taxiing aircraft queue.
        void UpdateGround()
        {
            IReadOnlyList<AircraftController> all = AircraftController.Active;
            foreach (AircraftController aircraft in all)
            {
                GroundMovement ground = aircraft.Ground;
                if (ground == null || aircraft.IsCrashed)
                    continue;

                // Blueprint 15.4: if the wind reverses while it taxis or waits, it taxis to the other end by itself.
                if (ground.TargetRunway >= 0 && ground.TargetEndB != ActiveEndB
                    && (ground.Phase == GroundPhase.Taxiing || ground.Phase == GroundPhase.HoldingShort))
                {
                    List<int> holds = network.EndHoldNodes(ground.TargetRunway, ActiveEndB);
                    TaxiRoute route = holds.Count > 0 ? network.FindRoute(ground.NodeAhead, holds) : null;
                    if (route != null && ground.FollowRoute(route, GroundPhase.HoldingShort))
                    {
                        ground.TargetEndB = ActiveEndB;
                        aircraft.RefreshStatus();
                    }
                }

                if (!ground.IsTaxiing)
                {
                    aircraft.GroundBlocked = false;
                    continue;
                }
                bool blocked = false;
                foreach (AircraftController other in all)
                {
                    if (other == aircraft || other.IsCrashed || other.Ground == null)
                        continue;
                    GroundMovement o = other.Ground;
                    if (TaxiQueue.Blocks(ground, o, settings.queueSpacing))
                    {
                        blocked = true;
                        break;
                    }
                }
                aircraft.GroundBlocked = blocked;
            }
        }

        // ---------------------------------------------------------------- Buttons

        /// <summary>The buttons for <paramref name="aircraft"/> now.</summary>
        public List<CommandOption> OptionsFor(AircraftController aircraft)
        {
            if (aircraft == null || aircraft.IsCrashed)
                return new List<CommandOption>();
            if (aircraft.Ground != null)
            {
                GroundMovement ground = aircraft.Ground;
                bool standFree = FreeStand() >= 0;
                return AircraftCommands.TowerGround(ground.Phase, TaxiChoices(ground), ground.TargetRunway, CanTakeOff(ground), standFree);
            }
            Flight flight = FlightOf(aircraft);
            if (flight == null || !aircraft.IsControllableInAir)
                return new List<CommandOption>();
            var runways = new List<RunwayChoice>();
            for (int r = 0; r < landing.Runways.Count; r++)
                runways.Add(new RunwayChoice(r, settings.AllowsRunway(landing.Runways[r]), landing.IsOpen(r)));
            bool cleared = flight.state == FlightState.Cleared;
            return AircraftCommands.TowerAir(aircraft.Type.landsOn, runways, NearestPad(aircraft.Type.landsOn, aircraft.Motion.Position).HasValue,
                                             cleared && flight.target.IsRunway ? flight.target.Runway : -1, cleared && !flight.target.IsRunway,
                                             flight.state == FlightState.Holding, aircraft.SpeedSetting);
        }

        void OnTapped(Vector2 world)
        {
            AircraftController aircraft = AircraftController.FindNearestForTap(world, TapRadius);
            List<CommandOption> options = OptionsFor(aircraft);
            if (aircraft == null || options.Count == 0)
            {
                menu.Close();
                return;
            }
            openOptions = options;
            menu.Open(aircraft, options);
            MenuOpened?.Invoke(aircraft, options);
        }

        void OnDragStarted(Vector2 world) => menu.Close();

        void OnOptionChosen(AircraftController aircraft, CommandOption option) => Give(aircraft, option);

        /// <summary>Carries out a button's command. Returns false if it does not apply now.</summary>
        public bool Give(AircraftController aircraft, CommandOption option)
        {
            if (aircraft == null || !option.Enabled)
                return false;
            bool done;
            switch (option.Command)
            {
                case AircraftCommand.Land: done = Land(aircraft, option.Runway); break;
                case AircraftCommand.GoAround: done = GoAround(aircraft); break;
                case AircraftCommand.Hold: done = Hold(aircraft); break;
                case AircraftCommand.Slow: done = ToggleSpeed(aircraft, AircraftSpeed.Slow); break;
                case AircraftCommand.Fast: done = ToggleSpeed(aircraft, AircraftSpeed.Fast); break;
                case AircraftCommand.Taxi: done = TaxiTo(aircraft, option.Runway); break;
                case AircraftCommand.Cross: done = Cross(aircraft); break;
                case AircraftCommand.TakeOff: done = TakeOff(aircraft); break;
                case AircraftCommand.TaxiToStand: done = TaxiToStand(aircraft); break;
                default: done = false; break;
            }
            if (done)
                CommandGiven?.Invoke(aircraft, option.Command);
            return done;
        }

        void Update()
        {
            if (landing == null)
                return;
            UpdateFlights();
            UpdateGround();

            // Keep the open buttons up to date (a runway closes, the aircraft moves on), or close them when none apply.
            if (menu != null && menu.IsOpen)
            {
                List<CommandOption> now = OptionsFor(menu.Target);
                if (menu.Target == null || now.Count == 0)
                    menu.Close();
                else if (!AircraftCommands.Same(now, openOptions))
                {
                    openOptions = now;
                    menu.Refresh(now);
                }
            }
        }
    }
}
