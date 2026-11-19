using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// Builds placeholder aircraft silhouettes (no art), pointing along +x. At size 1 each is about
    /// 0.7 world units long; <paramref name="size"/> scales it.
    /// </summary>
    public static class AircraftShape
    {
        public static Mesh CreateMesh(AircraftSilhouette silhouette, Color color, float size = 1f) =>
            CreateMesh(silhouette, color, color, size);

        public static Mesh CreateMesh(AircraftSilhouette silhouette, Color color, Color accent, float size)
        {
            var b = new MeshBuilder();
            switch (silhouette)
            {
                case AircraftSilhouette.JumboJet: Jumbo(b, color, size); break;
                case AircraftSilhouette.FighterJet: Fighter(b, color, size); break;
                case AircraftSilhouette.Helicopter: Helicopter(b, color, size); break;
                case AircraftSilhouette.Drone: Drone(b, color, size); break;
                case AircraftSilhouette.HotAirBalloon: Balloon(b, color, accent, size); break;
                default: Airliner(b, color, size); break;
            }
            return b.Build(silhouette.ToString());
        }

        static Vector2 P(float x, float y, float s) => new Vector2(x * s, y * s);

        static void Airliner(MeshBuilder b, Color c, float s)
        {
            // Fuselage and nose.
            b.Quad(P(-0.32f, -0.06f, s), P(0.22f, -0.06f, s), P(0.22f, 0.06f, s), P(-0.32f, 0.06f, s), c);
            b.Triangle(P(0.22f, -0.06f, s), P(0.38f, 0f, s), P(0.22f, 0.06f, s), c);
            // Swept main wings.
            b.Quad(P(0.08f, 0.05f, s), P(-0.06f, 0.34f, s), P(-0.14f, 0.34f, s), P(-0.06f, 0.05f, s), c);
            b.Quad(P(0.08f, -0.05f, s), P(-0.06f, -0.34f, s), P(-0.14f, -0.34f, s), P(-0.06f, -0.05f, s), c);
            // Tail.
            b.Quad(P(-0.22f, 0.05f, s), P(-0.29f, 0.16f, s), P(-0.34f, 0.16f, s), P(-0.32f, 0.05f, s), c);
            b.Quad(P(-0.22f, -0.05f, s), P(-0.29f, -0.16f, s), P(-0.34f, -0.16f, s), P(-0.32f, -0.05f, s), c);
        }

        // Wider body, longer wings with four engine pods.
        static void Jumbo(MeshBuilder b, Color c, float s)
        {
            Color pod = Color.Lerp(c, Color.black, 0.25f);
            b.Quad(P(-0.33f, -0.075f, s), P(0.2f, -0.075f, s), P(0.2f, 0.075f, s), P(-0.33f, 0.075f, s), c);
            b.Triangle(P(0.2f, -0.075f, s), P(0.37f, 0f, s), P(0.2f, 0.075f, s), c);
            b.Quad(P(0.1f, 0.06f, s), P(-0.08f, 0.36f, s), P(-0.16f, 0.36f, s), P(-0.06f, 0.06f, s), c);
            b.Quad(P(0.1f, -0.06f, s), P(-0.08f, -0.36f, s), P(-0.16f, -0.36f, s), P(-0.06f, -0.06f, s), c);
            foreach (float y in new[] { 0.15f, 0.26f, -0.15f, -0.26f })
            {
                float x = y > 0 ? 0.06f - y * 0.55f : 0.06f + y * 0.55f;
                b.Rect(P(x + 0.04f, y, s), P(0.09f, 0.04f, s), pod);
            }
            b.Quad(P(-0.22f, 0.06f, s), P(-0.3f, 0.18f, s), P(-0.35f, 0.18f, s), P(-0.33f, 0.06f, s), c);
            b.Quad(P(-0.22f, -0.06f, s), P(-0.3f, -0.18f, s), P(-0.35f, -0.18f, s), P(-0.33f, -0.06f, s), c);
        }

        // Round cabin, tail boom and a two-blade main rotor seen from above.
        static void Helicopter(MeshBuilder b, Color c, float s)
        {
            Color rotor = Color.Lerp(c, Color.black, 0.45f);
            b.Bar(P(-0.05f, 0f, s), P(-0.34f, 0f, s), 0.05f * s, c);
            b.Bar(P(-0.34f, -0.09f, s), P(-0.34f, 0.09f, s), 0.04f * s, c);
            b.Ellipse(P(0.06f, 0f, s), P(0.17f, 0.12f, s), 24, c);
            b.Bar(P(-0.24f, -0.24f, s), P(0.3f, 0.24f, s), 0.035f * s, rotor);
            b.Bar(P(-0.24f, 0.24f, s), P(0.3f, -0.24f, s), 0.035f * s, rotor);
            b.Ellipse(P(0.03f, 0f, s), P(0.04f, 0.04f, s), 12, rotor);
        }

        // Four rotors on an X frame.
        static void Drone(MeshBuilder b, Color c, float s)
        {
            Color rotor = Color.Lerp(c, Color.white, 0.35f);
            b.Bar(P(-0.2f, -0.2f, s), P(0.2f, 0.2f, s), 0.06f * s, c);
            b.Bar(P(-0.2f, 0.2f, s), P(0.2f, -0.2f, s), 0.06f * s, c);
            foreach (Vector2 corner in new[] { new Vector2(0.2f, 0.2f), new Vector2(0.2f, -0.2f), new Vector2(-0.2f, 0.2f), new Vector2(-0.2f, -0.2f) })
                b.Ring(corner * s, 0.07f * s, 0.12f * s, 16, rotor);
            b.Rect(Vector2.zero, P(0.16f, 0.12f, s), c);
            b.Triangle(P(0.08f, -0.05f, s), P(0.16f, 0f, s), P(0.08f, 0.05f, s), c); // nose, so the heading shows
        }

        // A balloon seen from above: a round envelope with stripes (gores) in two colours.
        static void Balloon(MeshBuilder b, Color c, Color accent, float s)
        {
            const int gores = 10;
            float radius = 0.3f * s;
            for (int i = 0; i < gores; i++)
            {
                float a0 = 2f * Mathf.PI * i / gores;
                float a1 = 2f * Mathf.PI * (i + 1) / gores;
                b.Triangle(Vector2.zero,
                           new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius,
                           new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius,
                           i % 2 == 0 ? c : accent);
            }
            b.Ellipse(Vector2.zero, new Vector2(0.07f, 0.07f) * s, 16, accent);
        }

        // Thin body with delta wings.
        static void Fighter(MeshBuilder b, Color c, float s)
        {
            b.Quad(P(-0.32f, -0.045f, s), P(0.24f, -0.045f, s), P(0.24f, 0.045f, s), P(-0.32f, 0.045f, s), c);
            b.Triangle(P(0.24f, -0.045f, s), P(0.4f, 0f, s), P(0.24f, 0.045f, s), c);
            b.Triangle(P(0.14f, 0.04f, s), P(-0.22f, 0.28f, s), P(-0.22f, 0.04f, s), c);
            b.Triangle(P(0.14f, -0.04f, s), P(-0.22f, -0.28f, s), P(-0.22f, -0.04f, s), c);
            b.Triangle(P(-0.2f, 0.04f, s), P(-0.32f, 0.13f, s), P(-0.32f, 0.04f, s), c);
            b.Triangle(P(-0.2f, -0.04f, s), P(-0.32f, -0.13f, s), P(-0.32f, -0.04f, s), c);
        }
    }
}
