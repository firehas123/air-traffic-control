using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// Holds the level's runways, draws them, and decides whether a finished path lands
    /// (see <see cref="LandingRules"/>).
    /// </summary>
    public sealed class LandingSystem : MonoBehaviour
    {
        readonly List<Runway> runways = new List<Runway>();

        public IReadOnlyList<Runway> Runways => runways;

        public void Initialise(IEnumerable<RunwayDefinition> definitions, Material material)
        {
            foreach (RunwayDefinition definition in definitions)
            {
                Runway runway = definition.ToRunway();
                runways.Add(runway);

                GameObject view = MeshBuilder.CreateObject("Runway",
                    MapShapes.CreateRunwayMesh(runway), material,
                    MapShapes.RunwaySortingOrder, runway.Centre, runway.LandingHeadingDegrees);
                view.transform.SetParent(transform, true);
            }
        }

        /// <summary>True if the finished line lands. <paramref name="runway"/> is the runway it lands on.</summary>
        public bool TryLand(IReadOnlyList<Vector2> line, out Runway runway)
        {
            int index = LandingRules.FindLandingRunway(runways, line);
            runway = index >= 0 ? runways[index] : default;
            return index >= 0;
        }
    }
}
