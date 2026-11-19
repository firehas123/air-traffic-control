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

        public const int WindIndicatorSortingOrder = -5;
        public const int GlowSortingOrder = -9;

        /// <summary>Glow colour for landing zones that match the aircraft being drawn. Not green, yellow or red.</summary>
        public static readonly Color Glow = new Color(0.55f, 0.95f, 1f, 0.95f);

        static readonly Color PadAsphalt = new Color(0.25f, 0.27f, 0.3f);
        static readonly Color FieldGrass = new Color(0.62f, 0.84f, 0.45f);

        static readonly Color IndicatorDisc = new Color(0.08f, 0.16f, 0.2f, 0.55f);

        /// <summary>
        /// Wind indicator from the screen mockups: a dark disc with a white arrow along local +x, the landing
        /// direction. Turn the object to point it.
        /// </summary>
        public static Mesh CreateWindIndicatorMesh()
        {
            return new MeshBuilder()
                .Ellipse(Vector2.zero, new Vector2(0.45f, 0.45f), 40, IndicatorDisc)
                .Bar(new Vector2(-0.27f, 0f), new Vector2(0.12f, 0f), 0.09f, Marking)
                .Triangle(new Vector2(0.08f, -0.15f), new Vector2(0.3f, 0f), new Vector2(0.08f, 0.15f), Marking)
                .Build("WindIndicator");
        }

        /// <summary>Helipad: a dark circle with a white ring and a white H, centred on the origin.</summary>
        public static Mesh CreateHelipadMesh(float radius)
        {
            float h = radius * 0.42f;
            return new MeshBuilder()
                .Ellipse(Vector2.zero, new Vector2(radius, radius), 40, PadAsphalt)
                .Ring(Vector2.zero, radius * 0.82f, radius * 0.9f, 40, Marking)
                .Bar(new Vector2(-h * 0.6f, -h), new Vector2(-h * 0.6f, h), 0.06f, Marking)
                .Bar(new Vector2(h * 0.6f, -h), new Vector2(h * 0.6f, h), 0.06f, Marking)
                .Bar(new Vector2(-h * 0.6f, 0f), new Vector2(h * 0.6f, 0f), 0.06f, Marking)
                .Build("Helipad");
        }

        /// <summary>Balloon field: a light grass circle with a white border and a small cross at its centre.</summary>
        public static Mesh CreateBalloonFieldMesh(float radius)
        {
            float c = radius * 0.25f;
            return new MeshBuilder()
                .Ellipse(Vector2.zero, new Vector2(radius, radius), 40, FieldGrass)
                .Ring(Vector2.zero, radius * 0.9f, radius, 40, Marking)
                .Bar(new Vector2(-c, 0f), new Vector2(c, 0f), 0.05f, Marking)
                .Bar(new Vector2(0f, -c), new Vector2(0f, c), 0.05f, Marking)
                .Build("BalloonField");
        }

        /// <summary>A glowing ring just outside a pad of the given radius.</summary>
        public static Mesh CreatePadGlowMesh(float radius)
        {
            return new MeshBuilder().Ring(Vector2.zero, radius + 0.04f, radius + 0.12f, 40, Glow).Build("PadGlow");
        }

        /// <summary>A glowing outline around a runway's touchdown zone, in the runway's local coordinates.</summary>
        public static Mesh CreateTouchdownZoneGlowMesh(Runway runway)
        {
            float x0 = -runway.Length / 2f + runway.TouchdownZoneStart - 0.06f;
            float x1 = x0 + runway.TouchdownZoneLength + 0.12f;
            float y = runway.Width / 2f + 0.06f;
            const float t = 0.07f;
            return new MeshBuilder()
                .Bar(new Vector2(x0, -y), new Vector2(x1, -y), t, Glow)
                .Bar(new Vector2(x0, y), new Vector2(x1, y), t, Glow)
                .Bar(new Vector2(x0, -y), new Vector2(x0, y), t, Glow)
                .Bar(new Vector2(x1, -y), new Vector2(x1, y), t, Glow)
                .Build("TouchdownZoneGlow");
        }

        static readonly Color Hull = new Color(0.42f, 0.46f, 0.5f);
        static readonly Color HullEdge = new Color(0.3f, 0.33f, 0.37f);

        /// <summary>Carrier hull seen from above, bow along local +x: a long grey hull with a pointed bow and an island tower.</summary>
        public static Mesh CreateCarrierHullMesh(float length, float width)
        {
            float half = length / 2f;
            float w = width / 2f;
            float bowStart = half - width * 0.7f;
            return new MeshBuilder()
                .Quad(new Vector2(-half, -w), new Vector2(bowStart, -w), new Vector2(bowStart, w), new Vector2(-half, w), HullEdge)
                .Triangle(new Vector2(bowStart, -w), new Vector2(half, 0f), new Vector2(bowStart, w), HullEdge)
                .Quad(new Vector2(-half + 0.05f, -w + 0.05f), new Vector2(bowStart, -w + 0.05f), new Vector2(bowStart, w - 0.05f), new Vector2(-half + 0.05f, w - 0.05f), Hull)
                .Triangle(new Vector2(bowStart, -w + 0.05f), new Vector2(half - 0.08f, 0f), new Vector2(bowStart, w - 0.05f), Hull)
                .Rect(new Vector2(0.25f, w - 0.06f), new Vector2(0.35f, 0.12f), HullEdge) // island tower on the starboard edge
                .Build("CarrierHull");
        }

        /// <summary>Dashes from <paramref name="from"/> to <paramref name="to"/>, for the carrier tether (blueprint 14.2).</summary>
        public static void BuildDashes(MeshBuilder builder, Vector2 from, Vector2 to, Color color,
                                       float dash = 0.12f, float gap = 0.08f, float thickness = 0.06f)
        {
            float length = Vector2.Distance(from, to);
            if (length <= 1e-4f)
                return;
            Vector2 dir = (to - from) / length;
            for (float d = 0f; d < length; d += dash + gap)
                builder.Bar(from + dir * d, from + dir * Mathf.Min(d + dash, length), thickness, color);
        }

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
