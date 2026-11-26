using System;
using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// Runs the Draw-mode map rules other than the Island wind (blueprint 16.2), on game time:
    /// City: one runway at a time closes (<see cref="RunwayClosureSchedule"/>), with a 5 s warning and a clear X.
    /// Desert: low-fuel aircraft with a visible timer (<see cref="FuelRules"/>) and a drifting sandstorm that closes the
    /// runways it covers (<see cref="Sandstorm"/>).
    /// Mountain Valley: no-fly mountains (<see cref="NoFlyZones"/>), given to the path planner and to every aircraft.
    /// Arctic: icy runways (<see cref="IceRules"/>): Fast landings slide, and the whole runway is busy while one rolls.
    ///
    /// The <see cref="LevelDirector"/> asks it each frame whether the open runways changed, and rechecks every
    /// aircraft's clearance around <see cref="ApplyRunwayStates"/> (with the 1.5 s grace of blueprint 14.3).
    /// </summary>
    public sealed class MapRuleSystem : MonoBehaviour
    {
        const int StormSortingOrder = -6;
        const int FlashSortingOrder = -3;
        const float FlashSeconds = 0.9f;

        LevelData level;
        MapRuleSettings rules;
        LandingSystem landing;
        PathPlanner planner;
        Camera worldCamera;
        Material material;

        RunwayClosureSchedule closures;
        Sandstorm storm;
        FuelRules fuel;
        NoFlyZones noFly;

        readonly List<RunwayState> wanted = new List<RunwayState>();
        readonly List<int> rollingRunways = new List<int>();
        readonly List<AircraftController> rolling = new List<AircraftController>();

        Transform stormView;
        GameObject flashView;
        Renderer flashRenderer;
        float flashTime;

        public NoFlyZones NoFly => noFly;
        public Sandstorm Storm => storm;
        public RunwayClosureSchedule Closures => closures;

        /// <summary>Raised when a low-fuel aircraft's timer runs out before it touched down.</summary>
        public event Action<AircraftController> FuelRanOut;

        /// <summary>Raised when an aircraft touches down on an icy runway another aircraft is still rolling on.</summary>
        public event Action<AircraftController, AircraftController> RunwayConflict;

        /// <summary>Raised when a line was stopped at a mountain.</summary>
        public event Action LineBlocked;

        public void Initialise(LevelData levelData, LandingSystem landingSystem, PathPlanner pathPlanner, Camera camera, Material shapeMaterial)
        {
            level = levelData;
            rules = level.rules ?? new MapRuleSettings();
            landing = landingSystem;
            planner = pathPlanner;
            worldCamera = camera;
            material = shapeMaterial;

            if (rules.noFly.enabled && rules.noFly.areas.Count > 0)
            {
                noFly = new NoFlyZones(rules.noFly.areas);
                if (planner != null)
                {
                    planner.NoFly = noFly;
                    planner.LineBlocked += OnLineBlocked;
                }
            }

            if (rules.sandstorm.enabled)
            {
                var b = new MeshBuilder();
                // A soft round cloud of sand: rings that fade towards the edge, so the covered area reads at a glance.
                for (int i = 0; i < 6; i++)
                {
                    float r = rules.sandstorm.radius * (1f - i * 0.12f);
                    b.Ellipse(Vector2.zero, Vector2.one * r, 48, new Color(0.8f, 0.62f, 0.38f, 0.16f));
                }
                b.Ring(Vector2.zero, rules.sandstorm.radius - 0.03f, rules.sandstorm.radius, 64, new Color(0.95f, 0.85f, 0.7f, 0.6f));
                GameObject view = MeshBuilder.CreateObject("Sandstorm", b.Build("Sandstorm"), material, StormSortingOrder, Vector2.zero, 0f);
                view.transform.SetParent(transform, true);
                stormView = view.transform;
                view.SetActive(false);
            }
        }

        void OnDestroy()
        {
            if (planner != null)
                planner.LineBlocked -= OnLineBlocked;
        }

        /// <summary>A fresh attempt: all runways open, schedules restarted with a new seed.</summary>
        public void ResetForNewAttempt(int seed)
        {
            closures = rules.closures.enabled ? new RunwayClosureSchedule(rules.closures, landing.Runways.Count, seed) : null;
            Rect view = CameraFraming.CurrentView(worldCamera);
            Rect airport = level.mapArt.HasPicture ? level.mapArt.keepVisible : view;
            storm = rules.sandstorm.enabled ? new Sandstorm(rules.sandstorm, view, airport, unchecked(seed * 31 + 7)) : null;
            fuel = rules.fuel.enabled ? new FuelRules(rules.fuel, unchecked(seed * 17 + 3)) : null;
            for (int i = 0; i < landing.Runways.Count; i++)
                landing.SetRunwayState(i, RunwayState.Open);
            if (stormView != null)
                stormView.gameObject.SetActive(false);
            if (flashView != null)
                flashView.SetActive(false);
        }

        /// <summary>Gives a new aircraft the map's rules: its fuel, the ice and the no-fly areas.</summary>
        public void OnSpawned(AircraftController aircraft)
        {
            aircraft.Motion.NoFly = noFly;
            aircraft.Ice = rules.ice.enabled ? rules.ice : null;
            aircraft.TouchedDown += OnTouchedDown;
            if (fuel != null)
            {
                float? seconds = fuel.NextAircraft(aircraft.Type);
                if (seconds.HasValue)
                {
                    aircraft.SetLowFuel(seconds.Value, material);
                    aircraft.RanOutOfFuel += OnRanOutOfFuel;
                }
            }
        }

        void OnRanOutOfFuel(AircraftController aircraft)
        {
            aircraft.RanOutOfFuel -= OnRanOutOfFuel;
            FuelRanOut?.Invoke(aircraft);
        }

        // Arctic: the whole icy runway is busy while an aircraft rolls or slides on it.
        void OnTouchedDown(AircraftController aircraft)
        {
            aircraft.TouchedDown -= OnTouchedDown;
            if (!rules.ice.enabled || aircraft.LandingRunwayIndex < 0)
                return;
            rolling.Clear();
            rollingRunways.Clear();
            foreach (AircraftController other in AircraftController.Active)
            {
                if (other == aircraft || other.IsCrashed || !other.IsLanding || other.LandingRunwayIndex < 0)
                    continue;
                rolling.Add(other);
                rollingRunways.Add(other.LandingRunwayIndex);
            }
            int hit = IceRules.TouchdownConflict(aircraft.LandingRunwayIndex, rollingRunways);
            if (hit >= 0)
                RunwayConflict?.Invoke(aircraft, rolling[hit]);
        }

        void OnLineBlocked(Vector2 point)
        {
            int area = noFly != null ? noFly.IndexAt(point) : -1;
            if (area < 0 && noFly != null)
            {
                // The segment crossed a thin part: outline the nearest area.
                float best = float.MaxValue;
                for (int i = 0; i < noFly.Count; i++)
                    foreach (Vector2 p in noFly.Area(i))
                    {
                        float d = (p - point).sqrMagnitude;
                        if (d < best) { best = d; area = i; }
                    }
            }
            if (area >= 0)
                Flash(area);
            LineBlocked?.Invoke();
        }

        // Outlines the mountain in white for a moment, so the player sees why the line stopped.
        void Flash(int area)
        {
            IReadOnlyList<Vector2> points = noFly.Area(area);
            var b = new MeshBuilder();
            for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
                b.Bar(points[j], points[i], 0.07f, new Color(1f, 1f, 1f, 0.9f));
            if (flashView == null)
            {
                flashView = MeshBuilder.CreateObject("NoFlyFlash", b.Build("NoFlyFlash"), material, FlashSortingOrder, Vector2.zero, 0f);
                flashView.transform.SetParent(transform, true);
                flashRenderer = flashView.GetComponent<Renderer>();
            }
            else
            {
                b.Fill(flashView.GetComponent<MeshFilter>().sharedMesh);
            }
            flashView.SetActive(true);
            flashTime = 0f;
        }

        /// <summary>
        /// Advances the closures and the storm and works out each runway's state. Returns true when a runway opens or
        /// closes, so the director rechecks clearances around <see cref="ApplyRunwayStates"/>.
        /// </summary>
        public bool Tick(float deltaTime)
        {
            closures?.Tick(deltaTime);
            storm?.Tick(deltaTime);

            if (stormView != null)
            {
                bool show = storm != null && storm.IsActive;
                if (stormView.gameObject.activeSelf != show)
                    stormView.gameObject.SetActive(show);
                if (show)
                    stormView.position = storm.Centre;
            }

            wanted.Clear();
            bool openChanged = false;
            for (int i = 0; i < landing.Runways.Count; i++)
            {
                RunwayState state = RunwayState.Open;
                if (closures != null)
                    state = Worst(state, closures.StateOf(i));
                if (storm != null)
                    state = Worst(state, storm.StateOf(landing.Runways[i]));
                wanted.Add(state);
                openChanged |= (state == RunwayState.Closed) != !landing.IsOpen(i);
            }
            return openChanged;
        }

        static RunwayState Worst(RunwayState a, RunwayState b) => (RunwayState)Mathf.Max((int)a, (int)b);

        /// <summary>Applies the states worked out by the last <see cref="Tick"/> to the landing zones and their X.</summary>
        public void ApplyRunwayStates()
        {
            for (int i = 0; i < wanted.Count; i++)
                if (landing.StateOf(i) != wanted[i])
                    landing.SetRunwayState(i, wanted[i]);
        }

        void Update()
        {
            if (flashView != null && flashView.activeSelf)
            {
                flashTime += Time.unscaledDeltaTime;
                Tint.SetAlpha(flashRenderer, Mathf.Clamp01(1f - flashTime / FlashSeconds));
                if (flashTime >= FlashSeconds)
                    flashView.SetActive(false);
            }
        }

        /// <summary>The rule's countdown for the top of the screen, or null.</summary>
        public string WarningText
        {
            get
            {
                if (closures != null && closures.CurrentPhase == RunwayClosureSchedule.Phase.Warning)
                    return $"Runway closing in {closures.WholeSecondsLeft}";
                if (closures != null && closures.CurrentPhase == RunwayClosureSchedule.Phase.Closed)
                    return $"Runway closed for {closures.WholeSecondsLeft}";
                if (storm != null && storm.IsActive)
                    for (int i = 0; i < landing.Runways.Count; i++)
                        if (landing.StateOf(i) == RunwayState.Closing)
                            return "Sandstorm heading for a runway";
                return null;
            }
        }

        /// <summary>True while any runway is closing soon or closed because of the City closures.</summary>
        public bool ClosureWarning => closures != null && closures.CurrentPhase != RunwayClosureSchedule.Phase.Open;

        /// <summary>True while the storm covers or is about to cover a runway.</summary>
        public bool StormNearRunway
        {
            get
            {
                if (storm == null || !storm.IsActive)
                    return false;
                foreach (Runway runway in landing.Runways)
                    if (storm.StateOf(runway) != RunwayState.Open)
                        return true;
                return false;
            }
        }

        public bool HasNoFly => noFly != null;
        public bool HasIce => rules.ice.enabled;
    }
}
