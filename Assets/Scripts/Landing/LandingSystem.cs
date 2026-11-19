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

        readonly List<LandingPad> pads = new List<LandingPad>();
        readonly List<GameObject> padGlows = new List<GameObject>();

        LandingZoneKind? highlighted;

        public IReadOnlyList<Runway> Runways => runways;
        public IReadOnlyList<LandingPad> Pads => pads;

        /// <summary>True when every runway lands the opposite way to its level data.</summary>
        public bool IsReversed { get; private set; }

        public void Initialise(IEnumerable<RunwayDefinition> runwayDefinitions, IEnumerable<LandingPadDefinition> padDefinitions, Material material)
        {
            foreach (RunwayDefinition definition in runwayDefinitions)
            {
                Runway runway = definition.ToRunway();
                normalRunways.Add(runway);
                runways.Add(runway);

                // The mesh is drawn with the landing direction along local +x, so turning the view
                // through 180 degrees moves the arrows, the zone and its glow to the other end.
                GameObject view = MeshBuilder.CreateObject("Runway", MapShapes.CreateRunwayMesh(runway), material,
                    MapShapes.RunwaySortingOrder, runway.Centre, runway.LandingHeadingDegrees);
                view.transform.SetParent(transform, true);
                runwayViews.Add(view.transform);

                GameObject glow = MeshBuilder.CreateObject("Glow", MapShapes.CreateTouchdownZoneGlowMesh(runway), material,
                    MapShapes.GlowSortingOrder, Vector2.zero, 0f);
                glow.transform.SetParent(view.transform, false);
                glow.SetActive(false);
                runwayGlows.Add(glow);
            }

            if (padDefinitions != null)
            {
                foreach (LandingPadDefinition definition in padDefinitions)
                {
                    LandingPad pad = definition.ToPad();
                    if (pad.Kind == LandingZoneKind.Runway)
                        continue; // runways are set up separately
                    pads.Add(pad);

                    Mesh mesh = pad.Kind == LandingZoneKind.Helipad
                        ? MapShapes.CreateHelipadMesh(pad.Radius)
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

        /// <summary>True if the line lands for an aircraft that uses zones of <paramref name="kind"/>.</summary>
        public bool TryLand(IReadOnlyList<Vector2> line, LandingZoneKind kind, out Touchdown touchdown) =>
            LandingRules.TryFindTouchdown(runways, pads, line, kind, out touchdown);

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
