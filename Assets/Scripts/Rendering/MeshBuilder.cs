using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// Builds flat coloured meshes from simple shapes, for placeholder art drawn with the
    /// unlit vertex-colour material. Shapes added later are drawn on top of earlier ones.
    /// </summary>
    public sealed class MeshBuilder
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> triangles = new List<int>();

        public MeshBuilder Triangle(Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            return this;
        }

        public MeshBuilder Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            Triangle(a, b, c, color);
            return Triangle(a, c, d, color);
        }

        public MeshBuilder Rect(Vector2 centre, Vector2 size, Color color)
        {
            Vector2 h = size / 2f;
            return Quad(centre + new Vector2(-h.x, -h.y), centre + new Vector2(h.x, -h.y),
                        centre + new Vector2(h.x, h.y), centre + new Vector2(-h.x, h.y), color);
        }

        public MeshBuilder Ellipse(Vector2 centre, Vector2 radii, int segments, Color color)
        {
            for (int i = 0; i < segments; i++)
            {
                float a0 = 2f * Mathf.PI * i / segments;
                float a1 = 2f * Mathf.PI * (i + 1) / segments;
                Triangle(centre,
                         centre + new Vector2(Mathf.Cos(a0) * radii.x, Mathf.Sin(a0) * radii.y),
                         centre + new Vector2(Mathf.Cos(a1) * radii.x, Mathf.Sin(a1) * radii.y),
                         color);
            }
            return this;
        }

        /// <summary>A flat ring between <paramref name="innerRadius"/> and <paramref name="outerRadius"/>.</summary>
        public MeshBuilder Ring(Vector2 centre, float innerRadius, float outerRadius, int segments, Color color)
        {
            for (int i = 0; i < segments; i++)
            {
                float a0 = 2f * Mathf.PI * i / segments;
                float a1 = 2f * Mathf.PI * (i + 1) / segments;
                var d0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0));
                var d1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));
                Quad(centre + d0 * innerRadius, centre + d0 * outerRadius,
                     centre + d1 * outerRadius, centre + d1 * innerRadius, color);
            }
            return this;
        }

        /// <summary>A straight bar from <paramref name="from"/> to <paramref name="to"/>.</summary>
        public MeshBuilder Bar(Vector2 from, Vector2 to, float thickness, Color color)
        {
            Vector2 along = to - from;
            if (along.sqrMagnitude <= 0f)
                return this;
            Vector2 side = new Vector2(-along.y, along.x).normalized * (thickness / 2f);
            return Quad(from - side, to - side, to + side, from + side, color);
        }

        /// <summary>Replaces the contents of an existing mesh, for shapes rebuilt every frame (the carrier tether).</summary>
        public void Fill(Mesh mesh)
        {
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Creates a scene object that shows <paramref name="mesh"/>.</summary>
        public static GameObject CreateObject(string name, Mesh mesh, Material material, int sortingOrder,
                                              Vector2 position, float rotationDegrees)
        {
            var go = new GameObject(name);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, rotationDegrees));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;
            return go;
        }
    }
}
