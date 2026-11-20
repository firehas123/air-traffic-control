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

                GameObject glow = MeshBuilder.CreateObject("Glow", MapShapes.CreateTouchdownZoneGlowMesh(runway), material,
                    MapShapes.GlowSortingOrder, Vector2.zero, 0f);
                glow.transform.SetParent(view.transform, false);
                glow.SetActive(false);
                runwayGlows.Add(glow);

                var lights = new GameObject("Lights").AddComponent<RunwayLights>();
                lights.transform.SetParent(view.transform, false);
                lights.Initialise(runway, scenery != null ? scenery.approachLights : 6, scenery != null ? scenery.approachLightSpacing : 0.22f, material);
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
                    Mesh mesh = paintedMap ? new Mesh { name = "PaintedPad" }
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
        /// A point in the middle of the first zone of <paramref name="kind"/>: the runway's current touchdown zone,
        /// or the pad's centre. Null if the level has none. Used by the first-time help demo.
        /// </summary>
        public Vector2? ZonePoint(LandingZoneKind kind)
        {
            if (kind == LandingZoneKind.Runway)
            {
                if (runways.Count == 0)
                    return null;
                Runway runway = runways[0];
                return runway.Threshold + runway.Direction * (runway.TouchdownZoneStart + runway.TouchdownZoneLength / 2f);
            }
            foreach (LandingPad pad in pads)
                if (pad.Kind == kind)
                    return pad.Centre;
            return null;
        }

        /// <summary>True if the line lands for an aircraft that uses zones of <paramref name="kind"/>.</summary>
        public bool TryLand(IReadOnlyList<Vector2> line, LandingZoneKind kind, out Touchdown touchdown) =>
            LandingRules.TryFindTouchdown(runways, pads, line, kind, out touchdown);

        /// <summary>Flashes every runway's edge lights while it is busy (blueprint 15.4).</summary>
        public void SetRunwayBusy(bool busy)
        {
            foreach (RunwayLights lights in runwayLights)
                lights.SetBusy(busy);
        }

        public bool IsRunwayBusyShown => runwayLights.Count > 0 && runwayLights[0].IsBusy;

        /// <summary>Makes the zones of <paramref name="kind"/> glow, or none when it is null (blueprint 13.1).</summary>
        public void SetHighlight(LandingZoneKind? kind)
        {
            if (highlighted == kind)
                return;
            highlighted = kind;

            foreach (GameObject glow in runwayGlows)
                glow.SetActive(kind == LandingZoneKind.Runway);
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
