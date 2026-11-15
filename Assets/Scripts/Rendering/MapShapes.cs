using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>Placeholder map art (no textures): the island and the runway.</summary>
    public static class MapShapes
    {
        public const int IslandSortingOrder = -20;
        public const int RunwaySortingOrder = -10;

        static readonly Color Sand = new Color(0.90f, 0.84f, 0.62f);
        static readonly Color Grass = new Color(0.47f, 0.70f, 0.38f);
        static readonly Color Asphalt = new Color(0.28f, 0.29f, 0.31f);
        static readonly Color Marking = new Color(0.95f, 0.95f, 0.95f);
        static readonly Color Arrow = new Color(1f, 0.82f, 0.15f);
        static readonly Color ZoneFill = new Color(1f, 0.82f, 0.15f, 0.3f);

        public static Mesh CreateIslandMesh(Vector2 radii)
        {
            return new MeshBuilder()
                .Ellipse(Vector2.zero, radii, 64, Sand)
                .Ellipse(Vector2.zero, radii - new Vector2(0.3f, 0.3f), 64, Grass)
                .Build("Island");
        }

        /// <summary>
        /// Runway in local coordinates: centred on the origin, landing direction along +x.
        /// The touchdown zone (where lines must end to land) is highlighted, and yellow arrows inside it
        /// point the way aircraft must land.
        /// </summary>
        public static Mesh CreateRunwayMesh(Runway runway)
        {
            float length = runway.Length;
            float width = runway.Width;
            float halfLength = length / 2f;
            float halfWidth = width / 2f;
            float zoneStart = -halfLength + runway.TouchdownZoneStart;
            float zoneLength = runway.TouchdownZoneLength;
            float zoneEnd = zoneStart + zoneLength;

            var builder = new MeshBuilder().Rect(Vector2.zero, new Vector2(length, width), Asphalt);

            // Highlighted touchdown zone: tinted fill with a solid edge at each end.
            builder.Rect(new Vector2(zoneStart + zoneLength / 2f, 0f), new Vector2(zoneLength, width), ZoneFill);
            const float edge = 0.04f;
            builder.Rect(new Vector2(zoneStart + edge / 2f, 0f), new Vector2(edge, width), Arrow);
            builder.Rect(new Vector2(zoneEnd - edge / 2f, 0f), new Vector2(edge, width), Arrow);

            // Two landing-direction arrows inside the zone.
            float arrowLength = Mathf.Min(0.28f, zoneLength * 0.4f);
            float arrowHalfHeight = halfWidth * 0.6f;
            for (int i = 0; i < 2; i++)
            {
                float back = zoneStart + zoneLength * (0.1f + 0.45f * i);
                builder.Triangle(new Vector2(back, -arrowHalfHeight),
                                 new Vector2(back + arrowLength, 0f),
                                 new Vector2(back, arrowHalfHeight), Arrow);
            }

            // Dashed centreline after the zone.
            const float dash = 0.2f;
            const float gap = 0.15f;
            for (float x = zoneEnd + 0.15f; x + dash <= halfLength - 0.15f; x += dash + gap)
                builder.Rect(new Vector2(x + dash / 2f, 0f), new Vector2(dash, 0.04f), Marking);

            return builder.Build("Runway");
        }
    }
}
