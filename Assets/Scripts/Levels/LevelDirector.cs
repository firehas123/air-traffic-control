using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AirTrafficControl
{
    /// <summary>How aircraft fly and look. Set on GameFlow in the scene.</summary>
    [Serializable]
    public sealed class AircraftSettings
    {
        public AircraftColors colors = AircraftColors.Default;
        // Airliner speed in world units per second at 1x (owner decision 15 Nov 2026: the earlier 1.2 is now 2x,
        // half of it is normal). Each kind flies at this times its AircraftType.speedMultiplier.
        public float speed = 0.6f;
        public float turnRate = 200f;       // degrees per second, only for the edge turn-back with no path
        public float edgeMargin = 0.8f;     // turn back this far inside the screen edge
    }

    /// <summary>
    /// Runs one level from <see cref="LevelData"/>: keeps the level's number of aircraft in the air, counts
    /// landings, clears the level when the landing target is reached (C2 objective "land a set number"), and
    /// ends the attempt on the first collision (blueprint: collisions restart the level). Retry and Play again
    /// restart at once (C3: under two seconds, no lives, no waiting).
    /// Also runs game time (<see cref="GameClock"/>): the 1x / 2x Speed button, freezing at the end, and slowing
    /// time while the player draws (C4, Slow draw button); and the Island wind rule (C1, <see cref="WindSchedule"/>,
    /// Wind button). Spawn schedules and other objective types will be added once the level data schema is settled.
    /// </summary>
    public sealed class LevelDirector : MonoBehaviour
    {
        /// <summary>The crash stays on screen this long, frozen, before the collision panel appears.</summary>
        const float CrashPauseSeconds = 0.5f;
        const float ClearedPauseSeconds = 0.3f;
        const float RespawnDelay = 1f;      // seconds after a landing
        const float BusyRetryDelay = 0.5f;  // seconds to wait when every spawn point is busy
        const float SpawnInset = 0.35f;     // aircraft appear this far inside the screen edge
        const float TowerSpawnInset = 0.6f; // Tower mode: the whole aircraft is on screen when it appears

        LevelSession session = new LevelSession();
        readonly GameClock clock = new GameClock();
        readonly List<Vector2> spawnPositions = new List<Vector2>();
        readonly List<Vector2> aircraftPositions = new List<Vector2>();
        readonly List<AircraftController> toRemove = new List<AircraftController>();

        LevelData level;
        Camera worldCamera;
        LandingSystem landing;
        AirspaceSafety safety;
        InputAdapter input;
        PathPlanner planner;
        CarrierSystem carrier;
        FeedbackSystem feedback;
        EffectsSystem effects;
        GroundControl groundControl;
        TowerControl tower;
        MapRuleSystem mapRules;
        int departuresSpawned;
        int arrivalsSpawned;
        bool towerMode;
        readonly List<AircraftType> departureKinds = new List<AircraftType>();
        HelpDemo helpDemo;
        FirstTimeHelp help;
        const string HelpSeenKey = "atc.firstTimeHelp.seen";
        HudView hud;
        Material material;
        AircraftSettings aircraftSettings;
        int nextSpawnIndex;
        int attempt;
        System.Random kindPicker = new System.Random(1);
        readonly Dictionary<AircraftController, Touchdown> landingBeforeWindChange = new Dictionary<AircraftController, Touchdown>();

        WindSchedule wind;
        bool windOn;
        Transform windIndicator;

        public LevelSession Session => session;

        /// <summary>Raised when the level is cleared, with its stars; the game flow saves it and shows the result.</summary>
        public event Action<LevelOutcome> Cleared;

        /// <summary>Raised when a collision ends the attempt.</summary>
        public event Action<LevelOutcome> Failed;
        public GameClock Clock => clock;

        public void Initialise(LevelData levelData, Camera camera, LandingSystem landingSystem, AirspaceSafety airspaceSafety,
                               InputAdapter inputAdapter, PathPlanner pathPlanner, CarrierSystem carrierSystem, HudView hudView,
                               Material shapeMaterial, AircraftSettings settings, FeedbackSystem feedbackSystem = null,
                               EffectsSystem effectsSystem = null, GroundControl groundControlSystem = null,
                               MapRuleSystem mapRuleSystem = null, TowerControl towerControl = null)
        {
            tower = towerControl;
            towerMode = tower != null;
            if (tower != null)
            {
                tower.MenuOpened += OnMenuOpened;
                tower.CommandGiven += OnCommandGiven;
                tower.MissedApproach += OnMissedApproach;
            }
            mapRules = mapRuleSystem;
            if (mapRules != null)
            {
                mapRules.FuelRanOut += OnFuelRanOut;
                mapRules.RunwayConflict += OnRunwayConflict;
                mapRules.LineBlocked += OnLineBlocked;
            }
            effects = effectsSystem;
            groundControl = groundControlSystem;
            if (groundControl != null)
            {
                groundControl.MenuOpened += OnMenuOpened;
                groundControl.CommandGiven += OnCommandGiven;
            }
            carrier = carrierSystem;
            feedback = feedbackSystem;

            // First-time help (M2 step 5): what has been shown is saved on the device, offline.
            help = new FirstTimeHelp(PlayerPrefs.GetInt(HelpSeenKey, 0), towerControl != null);
            helpDemo = new GameObject("HelpDemo").AddComponent<HelpDemo>();
            helpDemo.transform.SetParent(transform, false);
            helpDemo.Initialise(shapeMaterial);
            level = levelData;
            session = new LevelSession(level.landingsToClear, level.departuresToClear);
            planner = pathPlanner;
            worldCamera = camera;
            landing = landingSystem;
            safety = airspaceSafety;
            input = inputAdapter;
            hud = hudView;
            material = shapeMaterial;
            aircraftSettings = settings;

            spawnPositions.Clear();
            foreach (SpawnPoint point in level.spawnPoints)
                spawnPositions.Add(point.position);

            WindRuleSettings windRule = level.wind;
            if (windRule.hasWind)
            {
                windOn = windRule.onAtStart;
                GameObject indicator = MeshBuilder.CreateObject("WindIndicator", MapShapes.CreateWindIndicatorMesh(),
                    material, MapShapes.WindIndicatorSortingOrder, windRule.indicatorPosition, 0f);
                indicator.transform.SetParent(transform, true);
                windIndicator = indicator.transform;
            }

            safety.Collided += OnCollided;
            safety.WarningStarted += OnWarningStarted;
            planner.LineConnected += OnLineConnected;
            hud.RetryPressed += OnRetryPressed;
            hud.SpeedPressed += OnSpeedPressed;
            hud.WindPressed += OnWindPressed;
            hud.SlowDrawPressed += OnSlowDrawPressed;
            hud.HelpPressed += OnHelpPressed;
            input.SetScreenBlocker(hud.IsOverInteractive);
            hud.SetSpeedLabel(clock.SpeedLabel);
            hud.SetSlowDrawLabel(towerMode ? clock.SlowTapsLabel : clock.SlowDrawLabel);
            hud.SetWind(windRule.hasWind, windOn);
        }

        void OnDestroy()
        {
            if (safety != null)
            {
                safety.Collided -= OnCollided;
                safety.WarningStarted -= OnWarningStarted;
            }
            if (planner != null) planner.LineConnected -= OnLineConnected;
            if (mapRules != null)
            {
                mapRules.FuelRanOut -= OnFuelRanOut;
                mapRules.RunwayConflict -= OnRunwayConflict;
                mapRules.LineBlocked -= OnLineBlocked;
            }
            if (groundControl != null)
            {
                groundControl.MenuOpened -= OnMenuOpened;
                groundControl.CommandGiven -= OnCommandGiven;
            }
            if (tower != null)
            {
                tower.MenuOpened -= OnMenuOpened;
                tower.CommandGiven -= OnCommandGiven;
                tower.MissedApproach -= OnMissedApproach;
            }
            if (hud != null)
            {
                hud.RetryPressed -= OnRetryPressed;
                hud.SpeedPressed -= OnSpeedPressed;
                hud.WindPressed -= OnWindPressed;
                hud.SlowDrawPressed -= OnSlowDrawPressed;
                hud.HelpPressed -= OnHelpPressed;
            }
            Time.timeScale = 1f;
        }

        /// <summary>Starts a fresh attempt: counter at 0, the level's aircraft spawned, input on.</summary>
        public void StartLevel()
        {
            StopAllCoroutines();
            RemoveAllAircraft();

            session.Restart();
            hud.SetGoal(0, session.LandingsToClear, 0, session.DeparturesToClear);
            hud.SetTime(0f);
            hud.HideEnd();
            clock.Paused = false; // the Speed and Slow draw settings carry over between attempts
            clock.Drawing = false;
            ApplyClock();
            input.enabled = true;
            attempt++;
            kindPicker = new System.Random(unchecked(attempt * 104729 + Environment.TickCount));
            if (carrier != null)
                carrier.ResetForNewAttempt(unchecked(attempt * 15485863 + Environment.TickCount));
            hud.SetCarrierCountdown(0);
            mapRules?.ResetForNewAttempt(unchecked(attempt * 2654435 + Environment.TickCount));
            ResetWind();

            nextSpawnIndex = 0;
            arrivalsSpawned = 0;
            for (int i = 0; i < level.aircraftInAir; i++)
            {
                if (towerMode && level.landingsToClear > 0 && i >= level.landingsToClear)
                    break;
                if (!TrySpawnNow())
                    StartCoroutine(SpawnWhenClear(BusyRetryDelay)); // more aircraft than clear spawn points
            }

            // Departures (Tower mode, blueprint 16.1): only runway kinds, from free stands, as many as the goal needs.
            departuresSpawned = 0;
            departureKinds.Clear();
            foreach (AircraftType kind in level.aircraftTypes)
                if (kind != null && kind.landsOn == LandingZoneKind.Runway)
                    departureKinds.Add(kind);
            if (level.departuresToClear > 0 && tower != null)
                StartCoroutine(DepartureLoop());
        }

        IEnumerator DepartureLoop()
        {
            yield return new WaitForSeconds(level.departures.firstDepartureAfterSeconds);
            while (session.IsPlaying && departuresSpawned < level.departuresToClear)
            {
                if (TrySpawnDeparture())
                {
                    departuresSpawned++;
                    yield return new WaitForSeconds(level.departures.secondsBetweenDepartures);
                }
                else
                {
                    yield return new WaitForSeconds(BusyRetryDelay); // every gate is taken
                }
            }
        }

        bool TrySpawnDeparture()
        {
            AircraftType kind = departureKinds.Count > 0 ? departureKinds[kindPicker.Next(departureKinds.Count)] : PickRunwayKind();
            AircraftController aircraft = tower.SpawnDeparture(kind, aircraftSettings, material);
            if (aircraft == null)
                return false; // every stand is taken
            aircraft.Departed += OnAircraftDeparted;
            mapRules?.OnSpawned(aircraft);
            return true;
        }

        AircraftType PickRunwayKind()
        {
            if (defaultKind == null)
                defaultKind = AircraftType.CreateDefaultAirliner();
            return defaultKind;
        }

        void OnAircraftDeparted(AircraftController aircraft)
        {
            aircraft.Departed -= OnAircraftDeparted;
            bool cleared = session.RecordDeparture();
            hud.SetGoal(session.LandedCount, session.LandingsToClear, session.DepartureCount, session.DeparturesToClear);
            if (cleared)
                OnLevelCleared();
        }

        void OnMenuOpened(AircraftController aircraft, IReadOnlyList<CommandOption> options)
        {
            if (!towerMode && options.Count > 0 && (options[0].Command == AircraftCommand.Slow || options[0].Command == AircraftCommand.Fast))
                help.SpeedButtonsShown();
        }

        void OnCommandGiven(AircraftController aircraft, AircraftCommand command)
        {
            feedback?.LineConnected(); // the same short click confirms a command
            if (command == AircraftCommand.Land)
                help.LandOrderGiven();
        }

        // Island level 1 teaches Go around (blueprint 16.1): an arrival went around by itself, without a clearance.
        void OnMissedApproach(AircraftController aircraft)
        {
            if (level.tower.teachGoAround && session.IsPlaying)
                help.GoAroundUseful();
        }

        void OnWarningStarted(AircraftController aircraft)
        {
            if (!session.IsPlaying)
                return;
            session.RecordWarning();
            feedback?.WarningStarted();
            help.WarningStarted();
        }

        void OnLineConnected(AircraftController aircraft)
        {
            feedback?.LineConnected();
            help.LineConnected();
        }

        void OnHelpPressed()
        {
            help.Reset();
        }

        // Shows the current hint, and the ghost-line demo while the drawing hint is up.
        void UpdateHelp()
        {
            AircraftController demoAircraft = null;
            bool anyAirborne = false;
            foreach (AircraftController aircraft in AircraftController.Active)
            {
                if (!aircraft.IsControllableInAir)
                    continue;
                anyAirborne = true;
                if (demoAircraft == null && !aircraft.IsClearedToLand && !aircraft.IsConnectedToCarrier)
                    demoAircraft = aircraft;
            }

            if (session.IsPlaying)
            {
                if (wind != null && windOn && wind.IsWarning)
                    help.WindChangeComing();
                if (carrier != null && carrier.CanConnectNow && AnyAirborneFighter())
                    help.CarrierReachable();
                if (mapRules != null && anyAirborne)
                {
                    if (mapRules.ClosureWarning) help.RunwayClosingSoon();
                    if (mapRules.StormNearRunway) help.SandstormComing();
                    if (mapRules.HasNoFly) help.MountainsAhead();
                    if (mapRules.HasIce) help.IcyRunways();
                    foreach (AircraftController aircraft in AircraftController.Active)
                        if (aircraft.IsControllableInAir && aircraft.FuelSecondsLeft.HasValue)
                            help.LowFuelAircraft();
                }
                foreach (AircraftController aircraft in AircraftController.Active)
                {
                    if (aircraft.Ground == null || aircraft.IsCrashed)
                        continue;
                    switch (aircraft.Ground.Phase)
                    {
                        case GroundPhase.AtStand: help.DepartureAtGate(); break;
                        case GroundPhase.HoldingShort: help.DepartureAtHoldLine(); break;
                        case GroundPhase.HoldingAtCrossing: help.DepartureAtCrossing(); break;
                        case GroundPhase.WaitingForStand: help.ArrivalWaitingForStand(); break;
                    }
                }
                if (towerMode)
                    UpdateTowerHelp();
            }

            help.Tick(Time.unscaledDeltaTime, anyAirborne && session.IsPlaying);
            if (help.SeenChanged)
            {
                PlayerPrefs.SetInt(HelpSeenKey, help.Seen);
                PlayerPrefs.Save();
            }

            HelpHint hint = session.IsPlaying ? help.Current : HelpHint.None;
            hud.SetHint(help.TextNow(hint));

            Vector2? from = null;
            Vector2 to = Vector2.zero;
            if (hint == HelpHint.DrawToLandingZone && demoAircraft != null && !towerMode)
            {
                Vector2? zone = landing.ZonePoint(demoAircraft.Type.landsOn);
                if (zone.HasValue)
                {
                    from = demoAircraft.Motion.Position;
                    to = zone.Value;
                }
            }
            helpDemo.Show(from, to);
        }

        // Tower mode's teaching moments (Island levels 1 and 2): Go around when an arrival comes in to a busy runway, Hold when
        // several arrivals wait for orders at once. Mountain Valley explains its rule once.
        void UpdateTowerHelp()
        {
            int waiting = 0;
            foreach (AircraftController aircraft in AircraftController.Active)
            {
                TowerControl.Flight flight = tower.FlightOf(aircraft);
                if (flight == null || !aircraft.IsControllableInAir)
                    continue;
                if (flight.state == TowerControl.FlightState.Approaching)
                    waiting++;
                if (level.tower.teachGoAround && flight.target.IsRunway && landing.IsRunwayBusyShownAt(flight.target.Runway)
                    && aircraft.Motion.SecondsToEndOfPath < 8f)
                    help.GoAroundUseful();
            }
            if (level.tower.teachHold && waiting >= 2)
                help.HoldUseful();
            if (level.tower.valleyOnly)
                help.MountainsAhead();
        }

        static bool AnyAirborneFighter()
        {
            foreach (AircraftController aircraft in AircraftController.Active)
                if (aircraft.IsControllableInAir && aircraft.Type.landsOnCarrier)
                    return true;
            return false;
        }

        void OnTouchedDown(AircraftController aircraft)
        {
            aircraft.TouchedDown -= OnTouchedDown;
            if (session.IsPlaying)
            {
                feedback?.TouchedDown();
                // Tower mode: the arrival counts once it is parked, but the next one enters as soon as this one is down.
                if (towerMode && (level.landingsToClear <= 0 || arrivalsSpawned < level.landingsToClear))
                    StartCoroutine(SpawnWhenClear(RespawnDelay));
            }
        }

        void Update()
        {
            session.Tick(Time.deltaTime);
            session.RecordSeparation(safety.ClosestGapThisFrame);
            UpdateHelp();
            hud.SetTime(session.ElapsedSeconds);
            UpdateWind(Time.deltaTime);
            UpdateMapRules(Time.deltaTime);

            // Matching landing zones glow while the player draws (blueprint 13.1).
            AircraftController drawingFor = session.IsPlaying && planner != null ? planner.DrawingFor : null;
            landing.SetHighlight(drawingFor != null ? drawingFor.Type.landsOn : (LandingZoneKind?)null);
            if (carrier != null)
            {
                carrier.SetHighlight(drawingFor != null && drawingFor.Type.landsOnCarrier);
                hud.SetCarrierCountdown(session.IsPlaying && carrier.CanConnectNow
                    ? Mathf.CeilToInt(carrier.CurrentPass.SecondsUntilLeavesScreen) : 0);
            }

            // C4: slow time while the player is drawing an aircraft's line, and while option buttons are open
            // (blueprint 15.4). The Slow draw switch turns both off.
            bool drawing = drawingFor != null || (groundControl != null && groundControl.IsMenuOpen && session.IsPlaying)
                           || (tower != null && tower.IsMenuOpen && session.IsPlaying);
            if (drawing != clock.Drawing)
            {
                clock.Drawing = drawing;
                ApplyClock();
            }
        }

        // City closures and the Desert sandstorm (blueprint 16.2). When a runway opens or closes, every aircraft's
        // clearance is checked again, with the same grace period as the wind (14.3).
        void UpdateMapRules(float deltaTime)
        {
            string warning = wind != null && windOn && wind.IsWarning && session.IsPlaying ? $"Wind changing in {wind.WholeSecondsUntilChange}" : null;
            if (mapRules != null && session.IsPlaying)
            {
                if (mapRules.Tick(deltaTime))
                    ChangeLandingZones(mapRules.ApplyRunwayStates, level.wind.landingGraceSeconds);
                else
                    mapRules.ApplyRunwayStates();
                warning = warning ?? mapRules.WarningText;
            }
            hud.SetRuleWarning(warning);
        }

        void OnLineBlocked()
        {
            help.MountainsAhead();
        }

        // Desert: a low-fuel aircraft's timer ran out. The level ends like a collision.
        void OnFuelRanOut(AircraftController aircraft)
        {
            if (!session.RecordCollision())
                return;
            aircraft.Crash();
            feedback?.Collision();
            effects?.Collision(aircraft.transform.position);
            EndAttempt();
            LevelOutcome outcome = Outcome(false, "Out of fuel");
            StartCoroutine(ShowEndPanel(CrashPauseSeconds, () => Failed?.Invoke(outcome)));
        }

        // Arctic: a touchdown on an icy runway another aircraft is still rolling or sliding on.
        void OnRunwayConflict(AircraftController landed, AircraftController rolling)
        {
            landed.Crash();
            rolling.Crash();
            OnCollided(landed, rolling);
        }

        void OnSlowDrawPressed()
        {
            clock.SlowWhileDrawing = !clock.SlowWhileDrawing;
            hud.SetSlowDrawLabel(towerMode ? clock.SlowTapsLabel : clock.SlowDrawLabel);
            ApplyClock();
        }

        void ApplyClock()
        {
            Time.timeScale = clock.TimeScale;
        }

        void OnSpeedPressed()
        {
            clock.Fast = !clock.Fast;
            hud.SetSpeedLabel(clock.SpeedLabel);
            ApplyClock();
        }

        void OnWindPressed()
        {
            if (!level.wind.hasWind)
                return;
            windOn = !windOn;
            hud.SetWind(true, windOn);
            ResetWind();
        }

        // Fresh wind: normal landing direction and a new interval before the first change.
        // A different seed each attempt, so the changes do not come at the same moments every time.
        void ResetWind()
        {
            hud.SetWindWarning(0);
            if (!level.wind.hasWind)
                return;

            WindRuleSettings rule = level.wind;
            wind = new WindSchedule(rule.minSecondsBetweenChanges, rule.maxSecondsBetweenChanges, rule.warningSeconds,
                                    unchecked(attempt * 7919 + Environment.TickCount));
            SetLandingDirection(false);
        }

        void UpdateWind(float deltaTime)
        {
            if (wind == null || !windOn || !session.IsPlaying)
                return;

            if (wind.Tick(deltaTime))
                SetLandingDirection(wind.Reversed);

            bool warning = wind.IsWarning;
            float pulse = warning ? 1f + 0.15f * Mathf.Abs(Mathf.Sin(Time.time * 6f)) : 1f;
            windIndicator.localScale = Vector3.one * pulse;
        }

        // Turns the runways, points the wind indicator, and rechecks every aircraft's clearance (see ChangeLandingZones).
        void SetLandingDirection(bool reversed)
        {
            ChangeLandingZones(() =>
            {
                if (landing.IsReversed != reversed)
                    landing.SetReversed(reversed);
                if (windIndicator != null && landing.Runways.Count > 0)
                {
                    windIndicator.rotation = Quaternion.Euler(0f, 0f, landing.Runways[0].LandingHeadingDegrees);
                    windIndicator.localScale = Vector3.one;
                }
            }, level.wind.landingGraceSeconds);
        }

        /// <summary>
        /// Changes the landing zones (wind, a runway closing or opening) and rechecks every aircraft's clearance. A line
        /// that ends in an open touchdown zone after the change is cleared. A cleared aircraft whose zone went away loses
        /// its clearance, unless it would reach touchdown within the grace period (blueprint 14.3): then it still lands
        /// there. Carrier connections are left alone.
        /// </summary>
        void ChangeLandingZones(Action change, float grace)
        {
            landingBeforeWindChange.Clear();
            foreach (AircraftController aircraft in AircraftController.Active)
            {
                if (aircraft.IsAirborne && aircraft.Motion.HasPath && aircraft.IsClearedToLand && !aircraft.IsConnectedToCarrier
                    && landing.TryLand(aircraft.Motion.Path, aircraft.Type.landsOn, out Touchdown before))
                    landingBeforeWindChange[aircraft] = before;
            }

            change();

            foreach (AircraftController aircraft in AircraftController.Active)
            {
                if (!aircraft.IsAirborne || !aircraft.Motion.HasPath || aircraft.IsInWindGrace || aircraft.IsConnectedToCarrier)
                    continue;

                bool wasCleared = landingBeforeWindChange.TryGetValue(aircraft, out Touchdown oldTouchdown);
                bool landsNow = landing.TryLand(aircraft.Motion.Path, aircraft.Type.landsOn, out _);
                switch (WindGrace.Decide(wasCleared, aircraft.Motion.SecondsToEndOfPath, landsNow, grace))
                {
                    case ClearanceAfterWindChange.ClearedForNewZone:
                        aircraft.SetClearedToLand(true);
                        break;
                    case ClearanceAfterWindChange.KeepsLandingOnOldZone:
                        aircraft.KeepLandingDuringWindGrace(oldTouchdown);
                        break;
                    default:
                        aircraft.SetClearedToLand(false);
                        break;
                }
            }
            landingBeforeWindChange.Clear();
        }

        void OnCollided(AircraftController a, AircraftController b)
        {
            if (!session.RecordCollision())
                return;

            // Freeze everything so the crash can be seen, and stop drawing while the panel is up.
            feedback?.Collision();
            effects?.Collision(((Vector2)a.transform.position + (Vector2)b.transform.position) / 2f);
            EndAttempt();
            LevelOutcome outcome = Outcome(false);
            StartCoroutine(ShowEndPanel(CrashPauseSeconds, () => Failed?.Invoke(outcome)));
        }

        void OnLevelCleared()
        {
            EndAttempt();
            LevelOutcome outcome = Outcome(true);
            StartCoroutine(ShowEndPanel(ClearedPauseSeconds, () => Cleared?.Invoke(outcome)));
        }

        LevelOutcome Outcome(bool cleared, string failTitle = null) => new LevelOutcome(
            cleared,
            LevelResult.Stars(cleared, session.ElapsedSeconds, session.WarningCount, level.result),
            session.ElapsedSeconds, session.LandedCount, session.LandingsToClear,
            session.DepartureCount, session.DeparturesToClear, session.ClosestGap, session.WarningCount, failTitle);

        void EndAttempt()
        {
            StopAllCoroutines();
            clock.Paused = true;
            clock.Drawing = false;
            ApplyClock();
            input.enabled = false;
            hud.SetWindWarning(0);
            hud.SetCarrierCountdown(0);
        }

        IEnumerator ShowEndPanel(float delay, Action show)
        {
            yield return new WaitForSecondsRealtime(delay);
            show();
        }

        void OnRetryPressed()
        {
            var timer = Stopwatch.StartNew();
            StartLevel();
            // C3: restart takes under two seconds. Shown in the Xcode console.
            Debug.Log($"LevelDirector: restart took {timer.Elapsed.TotalMilliseconds:F1} ms");
        }

        void OnAircraftLanded(AircraftController aircraft)
        {
            aircraft.Landed -= OnAircraftLanded;
            bool cleared = session.RecordLanding();
            hud.SetGoal(session.LandedCount, session.LandingsToClear, session.DepartureCount, session.DeparturesToClear);
            if (cleared)
                OnLevelCleared();
            else if (session.IsPlaying && !towerMode)
                StartCoroutine(SpawnWhenClear(RespawnDelay)); // Tower mode sends the next arrival at touchdown
        }

        /// <summary>Stops the level and removes its aircraft, before the level is left.</summary>
        public void Shutdown()
        {
            StopAllCoroutines();
            RemoveAllAircraft();
            Time.timeScale = 1f;
        }

        void RemoveAllAircraft()
        {
            toRemove.Clear();
            toRemove.AddRange(AircraftController.Active);
            foreach (AircraftController aircraft in toRemove)
            {
                // Deactivate first: it leaves the active list at once, so spawning straight after
                // does not see it. Destroy happens at the end of the frame.
                aircraft.gameObject.SetActive(false);
                Destroy(aircraft.gameObject);
            }
        }

        IEnumerator SpawnWhenClear(float delay)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);
            while (!TrySpawnNow())
                yield return new WaitForSeconds(BusyRetryDelay);
        }

        AircraftType defaultKind;

        // A random kind from the level's list, or a default airliner if the list is empty.
        AircraftType PickKind()
        {
            List<AircraftType> kinds = level.aircraftTypes;
            AircraftType picked = kinds != null && kinds.Count > 0 ? kinds[kindPicker.Next(kinds.Count)] : null;
            if (picked != null)
                return picked;
            if (defaultKind == null)
                defaultKind = AircraftType.CreateDefaultAirliner();
            return defaultKind;
        }

        bool TrySpawnNow()
        {
            if (towerMode)
                return TrySpawnArrivalOnApproach();

            aircraftPositions.Clear();
            foreach (AircraftController other in AircraftController.Active)
                aircraftPositions.Add(other.transform.position);

            // Spawn points stay just inside the current screen edges, whatever the screen shape.
            Rect view = CameraFraming.CurrentView(worldCamera);
            spawnPositions.Clear();
            foreach (SpawnPoint spawn in level.spawnPoints)
                spawnPositions.Add(SpawnRules.ClampToView(spawn.position, view, SpawnInset));

            int index = SpawnRules.ChooseClearPoint(spawnPositions, aircraftPositions, nextSpawnIndex);
            if (index < 0)
                return false;

            nextSpawnIndex = index + 1;
            SpawnPoint point = level.spawnPoints[index];
            AircraftType type = PickKind();
            float speed = aircraftSettings.speed * type.speedMultiplier;
            var motion = new AircraftMotion(spawnPositions[index], point.headingDegrees, speed, aircraftSettings.turnRate);
            AircraftController aircraft = AircraftController.Spawn(type, motion, worldCamera, landing, material,
                                                                   aircraftSettings.colors, aircraftSettings.edgeMargin);
            aircraft.Landed += OnAircraftLanded;
            aircraft.TouchedDown += OnTouchedDown;
            mapRules?.OnSpawned(aircraft);
            return true;
        }

        readonly List<int> approachChoices = new List<int>();
        readonly List<Vector2> approachPositions = new List<Vector2>();

        // Tower mode: an arrival at the screen edge on one of the fixed approach routes to an open runway end (blueprint
        // 16.1). Never more arrivals than the landing goal. Helicopters, drones and balloons use any entry and fly to their pad.
        bool TrySpawnArrivalOnApproach()
        {
            if (level.landingsToClear > 0 && arrivalsSpawned >= level.landingsToClear)
                return true; // nothing more to send
            List<TowerApproach> approaches = level.tower.approaches;
            if (approaches.Count == 0)
                return false;
            AircraftType type = PickKind();
            Rect view = CameraFraming.CurrentView(worldCamera);

            approachChoices.Clear();
            for (int i = 0; i < approaches.Count; i++)
            {
                TowerApproach a = approaches[i];
                bool fits = type.landsOn != LandingZoneKind.Runway
                            || (a.endB == landing.IsReversed && tower.CanLandOn(a.runway));
                if (fits)
                    approachChoices.Add(i);
            }
            if (approachChoices.Count == 0)
                for (int i = 0; i < approaches.Count; i++)
                    approachChoices.Add(i); // every runway closed: it comes in anyway and goes around until one opens

            aircraftPositions.Clear();
            foreach (AircraftController other in AircraftController.Active)
                aircraftPositions.Add(other.transform.position);
            approachPositions.Clear();
            foreach (int i in approachChoices)
                approachPositions.Add(SpawnRules.ClampToView(approaches[i].entry, view, TowerSpawnInset));
            int start = approachChoices.Count > 0 ? kindPicker.Next(approachChoices.Count) : 0;
            int pick = SpawnRules.ChooseClearPoint(approachPositions, aircraftPositions, start);
            if (pick < 0)
                return false;

            TowerApproach approach = approaches[approachChoices[pick]];
            float speed = aircraftSettings.speed * type.speedMultiplier;
            var motion = new AircraftMotion(approachPositions[pick], approach.headingDegrees, speed, aircraftSettings.turnRate);
            AircraftController aircraft = AircraftController.Spawn(type, motion, worldCamera, landing, material,
                                                                   aircraftSettings.colors, aircraftSettings.edgeMargin);
            arrivalsSpawned++;
            aircraft.Landed += OnAircraftLanded;
            aircraft.TouchedDown += OnTouchedDown;
            mapRules?.OnSpawned(aircraft);
            tower.AddArrival(aircraft, approach);
            return true;
        }
    }
}
