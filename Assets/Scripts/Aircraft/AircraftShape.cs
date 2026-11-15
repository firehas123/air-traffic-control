using System.Collections.Generic;
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
            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            // Fuselage and nose.
            AddQuad(vertices, triangles, new Vector2(-0.32f, -0.06f), new Vector2(0.22f, -0.06f), new Vector2(0.22f, 0.06f), new Vector2(-0.32f, 0.06f));
            AddTriangle(vertices, triangles, new Vector2(0.22f, -0.06f), new Vector2(0.38f, 0f), new Vector2(0.22f, 0.06f));

            // Swept main wings.
            AddQuad(vertices, triangles, new Vector2(0.08f, 0.05f), new Vector2(-0.06f, 0.34f), new Vector2(-0.14f, 0.34f), new Vector2(-0.06f, 0.05f));
            AddQuad(vertices, triangles, new Vector2(0.08f, -0.05f), new Vector2(-0.06f, -0.34f), new Vector2(-0.14f, -0.34f), new Vector2(-0.06f, -0.05f));

            // Tail.
            AddQuad(vertices, triangles, new Vector2(-0.22f, 0.05f), new Vector2(-0.29f, 0.16f), new Vector2(-0.34f, 0.16f), new Vector2(-0.32f, 0.05f));
            AddQuad(vertices, triangles, new Vector2(-0.22f, -0.05f), new Vector2(-0.29f, -0.16f), new Vector2(-0.34f, -0.16f), new Vector2(-0.32f, -0.05f));

            var colors = new Color[vertices.Count];
            for (int i = 0; i < colors.Length; i++)
                colors[i] = color;

            var mesh = new Mesh { name = "AircraftPlaceholder" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.colors = colors;
            mesh.RecalculateBounds();
            return mesh;
        }

        static void AddTriangle(List<Vector3> vertices, List<int> triangles, Vector2 a, Vector2 b, Vector2 c)
        {
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }

        static void AddQuad(List<Vector3> vertices, List<int> triangles, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            AddTriangle(vertices, triangles, a, b, c);
            AddTriangle(vertices, triangles, a, c, d);
        }
    }
}
