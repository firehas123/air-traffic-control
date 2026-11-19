using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// Builds placeholder aircraft silhouettes (no art), pointing along +x. At size 1 each is about
    /// 0.7 world units long; <paramref name="size"/> scales it.
    /// </summary>
    public static class AircraftShape
    {
        public static Mesh CreateMesh(AircraftSilhouette silhouette, Color color, float size = 1f)
        {
            var b = new MeshBuilder();
            switch (silhouette)
            {
                case AircraftSilhouette.JumboJet: Jumbo(b, color, size); break;
                case AircraftSilhouette.FighterJet: Fighter(b, color, size); break;
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
