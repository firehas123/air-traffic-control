using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// Builds a placeholder airplane silhouette (no art) pointing along +x, about 0.7 world units long.
    /// </summary>
    public static class AircraftShape
    {
        public static Mesh CreateMesh(Color color)
        {
            return new MeshBuilder()
                // Fuselage and nose.
                .Quad(new Vector2(-0.32f, -0.06f), new Vector2(0.22f, -0.06f), new Vector2(0.22f, 0.06f), new Vector2(-0.32f, 0.06f), color)
                .Triangle(new Vector2(0.22f, -0.06f), new Vector2(0.38f, 0f), new Vector2(0.22f, 0.06f), color)
                // Swept main wings.
                .Quad(new Vector2(0.08f, 0.05f), new Vector2(-0.06f, 0.34f), new Vector2(-0.14f, 0.34f), new Vector2(-0.06f, 0.05f), color)
                .Quad(new Vector2(0.08f, -0.05f), new Vector2(-0.06f, -0.34f), new Vector2(-0.14f, -0.34f), new Vector2(-0.06f, -0.05f), color)
                // Tail.
                .Quad(new Vector2(-0.22f, 0.05f), new Vector2(-0.29f, 0.16f), new Vector2(-0.34f, 0.16f), new Vector2(-0.32f, 0.05f), color)
                .Quad(new Vector2(-0.22f, -0.05f), new Vector2(-0.29f, -0.16f), new Vector2(-0.34f, -0.16f), new Vector2(-0.32f, -0.05f), color)
                .Build("AircraftPlaceholder");
        }
    }
}
