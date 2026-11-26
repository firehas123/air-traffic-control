using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// Holds the level's landing zones (runways, helipads, balloon fields), draws them, and decides where a line
    /// lands for a kind of aircraft (see <see cref="LandingRules"/>). A line lands only on a zone that matches the
    /// aircraft kind, and the matching zones glow while the player draws (blueprint 13.1).
    /// <see cref="SetReversed"/> flips every runway's landing direction (Island wind rule, C1): the arrows and the
    /// touchdown zone move to the other end. Wind does not affect pads (14.3).
    /// </summary>
    public sealed class LandingSystem : MonoBehaviour
    {
        readonly List<Runway> normalRunways = new List<Runway>();
        readonly List<Runway> runways = new List<Runway>();
        readonly List<Transform> runwayViews = new List<Transform>();
        readonly List<GameObject> runwayGlows = new List<GameObject>();
        readonly List<RunwayLights> runwayLights = new List<RunwayLights>();
        readonly List<RunwayState> runwayStates = new List<RunwayState>();
        readonly List<RunwayClosedMark> closedMarks = new List<RunwayClosedMark>();
        readonly List<GameObject> runwayOverlays = new List<GameObject>();
        readonly List<Runway> openRunways = new List<Runway>();

        readonly List<LandingPad> pads = new List<LandingPad>();
        readonly List<GameObject> padGlows = new List<GameObject>();

        LandingZoneKind? highlighted;

        public IReadOnlyList<Runway> Runways => runways;
        public IReadOnlyList<LandingPad> Pads => pads;

        /// <summary>True when every runway lands the opposite way to its level data.</summary>
        public bool IsReversed { get; private set; }

        /// <param name="paintedMap">
        /// True when the level has a painted map: the picture shows the runway, helipad and balloon field, so only the
        /// overlays are drawn (touchdown-zone highlight and arrows, glows, lights).
        /// </param>
        public void Initialise(IEnumerable<RunwayDefinition> runwayDefinitions, IEnumerable<LandingPadDefinition> padDefinitions,
                               Material material, SceneryLayout scenery = null, bool paintedMap = false)
        {
            foreach (RunwayDefinition definition in runwayDefinitions)
            {
                Runway runway = definition.ToRunway();
                normalRunways.Add(runway);
                runways.Add(runway);

                // The mesh is drawn with the landing direction along local +x, so turning the view
                // through 180 degrees moves the arrows, the zone and its glow to the other end.
                Mesh runwayMesh = paintedMap ? MapShapes.CreateRunwayOverlayMesh(runway) : MapShapes.CreateRunwayMesh(runway);
                GameObject view = MeshBuilder.CreateObject("Runway", runwayMesh, material,
                    MapShapes.RunwaySortingOrder, runway.Centre, runway.LandingHeadingDegrees);
                view.transform.SetParent(transform, true);
                runwayViews.Add(view.transform);
                runwayOverlays.Add(view);
                runwayStates.Add(RunwayState.Open);

                // The clear X of a closed runway (City and Desert rules, blueprint 16.2). Not a child of the view, so the
                // overlay can be hidden while the X shows.
                var mark = new GameObject("ClosedMark").AddComponent<RunwayClosedMark>();
                mark.transform.SetParent(transform, false);
                mark.Initialise(runway, material);
                closedMarks.Add(mark);

                GameObject glow = MeshBuilder.CreateObject("Glow", MapShapes.CreateTouchdownZoneGlowMesh(runway), material,
                    MapShapes.GlowSortingOrder, Vector2.zero, 0f);
                glow.transform.SetParent(view.transform, false);
                glow.SetActive(false);
                runwayGlows.Add(glow);

                var lights = new GameObject("Lights").AddComponent<RunwayLights>();
                lights.transform.SetParent(transform, false);
                lights.transform.SetPositionAndRotation(runway.Centre, Quaternion.Euler(0f, 0f, runway.LandingHeadingDegrees));
                lights.Initialise(runway, material);
                runwayLights.Add(lights);
            }

            if (padDefinitions != null)
            {
                foreach (LandingPadDefinition definition in padDefinitions)
                {
                    LandingPad pad = definition.ToPad();
                    if (pad.Kind == LandingZoneKind.Runway)
                        continue; // runways are set up separately
                    pads.Add(pad);

                    // On a painted map the pad itself is in the picture; only its glow is drawn.
                    Mesh mesh = paintedMap ? (definition.drawH ? MapShapes.CreateHelipadLetterMesh(pad.Radius) : new Mesh { name = "PaintedPad" })
                        : pad.Kind == LandingZoneKind.Helipad ? MapShapes.CreateHelipadMesh(pad.Radius)
                        : MapShapes.CreateBalloonFieldMesh(pad.Radius);
                    GameObject view = MeshBuilder.CreateObject(pad.Kind.ToString(), mesh, material,
                        MapShapes.RunwaySortingOrder, pad.Centre, 0f);
                    view.transform.SetParent(transform, true);

                    GameObject glow = MeshBuilder.CreateObject("Glow", MapShapes.CreatePadGlowMesh(pad.Radius), material,
                        MapShapes.GlowSortingOrder, Vector2.zero, 0f);
                    glow.transform.SetParent(view.transform, false);
                    glow.SetActive(false);
                    padGlows.Add(glow);
                }
            }
        }

        public void SetReversed(bool reversed)
        {
            IsReversed = reversed;
            for (int i = 0; i < normalRunways.Count; i++)
            {
                runways[i] = reversed ? normalRunways[i].Reversed() : normalRunways[i];
                runwayViews[i].rotation = Quaternion.Euler(0f, 0f, runways[i].LandingHeadingDegrees);
            }
        }

        /// <summary>
        /// Closes or opens a runway, or marks it as closing soon (City and Desert rules, blueprint 16.2). A line cannot
        /// land on a closed runway; its touchdown zone and arrows are hidden and a clear X covers it. A runway that is
        /// closing soon still takes landings; its X blinks.
        /// </summary>
        public void SetRunwayState(int index, RunwayState state)
        {
            if (index < 0 || index >= runwayStates.Count)
                return;
            runwayStates[index] = state;
            closedMarks[index].SetState(state);
            runwayOverlays[index].SetActive(state != RunwayState.Closed);
        }

        public RunwayState StateOf(int index) => index >= 0 && index < runwayStates.Count ? runwayStates[index] : RunwayState.Open;

        public bool IsOpen(int index) => StateOf(index) != RunwayState.Closed;

        /// <summary>Index of the runway the touchdown is on (either landing direction), or -1.</summary>
        public int IndexOf(Runway runway)
        {
            for (int i = 0; i < normalRunways.Count; i++)
                if (Vector2.Distance(normalRunways[i].Centre, runway.Centre) < 1e-3f
                    && Mathf.Abs(Mathf.DeltaAngle(normalRunways[i].LandingHeadingDegrees, runway.LandingHeadingDegrees)) % 180f < 0.5f)
                    return i;
            return -1;
        }

        /// <summary>
        /// A point in the middle of the first open zone of <paramref name="kind"/>: a runway's current touchdown zone,
        /// or the pad's centre. Null if the level has none. Used by the first-time help demo.
        /// </summary>
        public Vector2? ZonePoint(LandingZoneKind kind)
        {
            if (kind == LandingZoneKind.Runway)
            {
                for (int i = 0; i < runways.Count; i++)
                {
                    if (!IsOpen(i))
                        continue;
                    Runway runway = runways[i];
                    return runway.Threshold + runway.Direction * (runway.TouchdownZoneStart + runway.TouchdownZoneLength / 2f);
                }
                return null;
            }
            foreach (LandingPad pad in pads)
                if (pad.Kind == kind)
                    return pad.Centre;
            return null;
        }

        /// <summary>True if the line lands for an aircraft that uses zones of <paramref name="kind"/>.</summary>
        /// <remarks>Closed runways are left out (blueprint 16.2: lines cannot end on a closed runway).</remarks>
        public bool TryLand(IReadOnlyList<Vector2> line, LandingZoneKind kind, out Touchdown touchdown) =>
            LandingRules.TryFindTouchdown(OpenRunways(), pads, line, kind, out touchdown);

        IReadOnlyList<Runway> OpenRunways()
        {
            openRunways.Clear();
            for (int i = 0; i < runways.Count; i++)
                if (IsOpen(i))
                    openRunways.Add(runways[i]);
            return openRunways;
        }

        /// <summary>Flashes every runway's edge lights while it is busy (blueprint 15.4).</summary>
        public void SetRunwayBusy(bool busy)
        {
            foreach (RunwayLights lights in runwayLights)
                lights.SetBusy(busy);
        }

        /// <summary>Flashes one runway's edge lights while it is busy.</summary>
        public void SetRunwayBusy(int index, bool busy)
        {
            if (index >= 0 && index < runwayLights.Count)
                runwayLights[index].SetBusy(busy);
        }

        public bool IsRunwayBusyShown => runwayLights.Count > 0 && runwayLights[0].IsBusy;

        public bool IsRunwayBusyShownAt(int index) => index >= 0 && index < runwayLights.Count && runwayLights[index].IsBusy;

        /// <summary>Index of the runway whose surface contains the point (with a small margin), or -1.</summary>
        public int RunwayAt(Vector2 point, float margin = 0.1f)
        {
            for (int i = 0; i < runways.Count; i++)
                if (runways[i].Contains(point, margin))
                    return i;
            return -1;
        }

        /// <summary>Makes the zones of <paramref name="kind"/> glow, or none when it is null (blueprint 13.1).</summary>
        public void SetHighlight(LandingZoneKind? kind)
        {
            if (highlighted == kind)
                return;
            highlighted = kind;

            for (int i = 0; i < runwayGlows.Count; i++)
                runwayGlows[i].SetActive(kind == LandingZoneKind.Runway && IsOpen(i));
            for (int i = 0; i < pads.Count; i++)
                padGlows[i].SetActive(kind == pads[i].Kind);
        }

        void Update()
        {
            if (!highlighted.HasValue)
                return;
            // Gentle pulse in real time, so it still pulses while slow draw slows the game.
            float pulse = 1f + 0.04f * Mathf.Sin(Time.unscaledTime * 6f);
            foreach (GameObject glow in runwayGlows)
                if (glow.activeSelf) glow.transform.localScale = new Vector3(1f, pulse, 1f);
            foreach (GameObject glow in padGlows)
                if (glow.activeSelf) glow.transform.localScale = Vector3.one * pulse;
        }
    }
}
