using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// Holds the level's runways, draws them, and decides whether a line lands (see <see cref="LandingRules"/>).
    /// <see cref="SetReversed"/> flips every runway's landing direction (Island wind rule, C1): the arrows and the
    /// touchdown zone move to the other end.
    /// </summary>
    public sealed class LandingSystem : MonoBehaviour
    {
        readonly List<Runway> normalRunways = new List<Runway>();
        readonly List<Runway> runways = new List<Runway>();
        readonly List<Transform> views = new List<Transform>();

        public IReadOnlyList<Runway> Runways => runways;

        /// <summary>True when every runway lands the opposite way to its level data.</summary>
        public bool IsReversed { get; private set; }

        public void Initialise(IEnumerable<RunwayDefinition> definitions, Material material)
        {
            foreach (RunwayDefinition definition in definitions)
            {
                Runway runway = definition.ToRunway();
                normalRunways.Add(runway);
                runways.Add(runway);

                // The mesh is drawn with the landing direction along local +x, so turning the view
                // through 180 degrees moves the arrows and the zone to the other end.
                GameObject view = MeshBuilder.CreateObject("Runway", MapShapes.CreateRunwayMesh(runway), material,
                    MapShapes.RunwaySortingOrder, runway.Centre, runway.LandingHeadingDegrees);
                view.transform.SetParent(transform, true);
                views.Add(view.transform);
            }
        }

        public void SetReversed(bool reversed)
        {
            IsReversed = reversed;
            for (int i = 0; i < normalRunways.Count; i++)
            {
                runways[i] = reversed ? normalRunways[i].Reversed() : normalRunways[i];
                views[i].rotation = Quaternion.Euler(0f, 0f, runways[i].LandingHeadingDegrees);
            }
        }

        /// <summary>True if the line lands. <paramref name="runway"/> is the runway it lands on.</summary>
        public bool TryLand(IReadOnlyList<Vector2> line, out Runway runway)
        {
            int index = LandingRules.FindLandingRunway(runways, line);
            runway = index >= 0 ? runways[index] : default;
            return index >= 0;
        }
    }
}
