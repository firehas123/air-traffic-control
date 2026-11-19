using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// Original placeholder art for the Island scenery (blueprint 15.3), drawn from vector shapes in one flat, soft
    /// style. Scenery colours are soft on purpose, so the status rings stay the clearest thing on screen.
    /// </summary>
    public static class SceneryShapes
    {
        // Sorting: sea marks < foam < island (-20) < ground markings < runway (-10) < glow (-9) ... < clouds < paths (0).
        public const int SeaOrder = -26;
        public const int FoamOrder = -24;
        public const int BeachDetailOrder = -19;
        public const int ApronOrder = -18;
        public const int TaxiwayOrder = -17;
        public const int BuildingOrder = -15;
        public const int PalmOrder = -14;
        public const int WindsockOrder = -14;
        public const int BoatOrder = -22;
        public const int RunwayLightOrder = -8;
        public const int CloudShadowOrder = -7;
        public const int CloudOrder = -4;
        public const int BirdOrder = -3;

        public static readonly Color WaveLight = new Color(0.55f, 0.75f, 0.9f, 0.35f);
        public static readonly Color Foam = new Color(1f, 1f, 1f, 0.55f);
        static readonly Color Rock = new Color(0.6f, 0.58f, 0.55f);
        static readonly Color RockShade = new Color(0.48f, 0.46f, 0.44f);
        static readonly Color Trunk = new Color(0.55f, 0.42f, 0.3f);
        static readonly Color Frond = new Color(0.26f, 0.5f, 0.3f);
        static readonly Color FrondLight = new Color(0.36f, 0.6f, 0.36f);
        static readonly Color Apron = new Color(0.5f, 0.52f, 0.55f);
        static readonly Color Taxiway = new Color(0.4f, 0.42f, 0.45f);
        static readonly Color TaxiLine = new Color(0.88f, 0.9f, 0.9f, 0.8f);
        static readonly Color Building = new Color(0.86f, 0.84f, 0.78f);
        static readonly Color Roof = new Color(0.7f, 0.68f, 0.64f);
        static readonly Color Glass = new Color(0.32f, 0.5f, 0.6f);
        static readonly Color Parked = new Color(0.82f, 0.84f, 0.86f);
        static readonly Color WindsockStripe = new Color(0.98f, 0.6f, 0.3f);
        static readonly Color Hull = new Color(0.95f, 0.95f, 0.93f);
        static readonly Color Deck = new Color(0.6f, 0.46f, 0.33f);
        public static readonly Color Wake = new Color(1f, 1f, 1f, 0.45f);
        static readonly Color Cloud = new Color(1f, 1f, 1f, 0.32f);
        static readonly Color CloudShadow = new Color(0.05f, 0.1f, 0.15f, 0.12f);
        static readonly Color Bird = new Color(0.15f, 0.18f, 0.22f, 0.85f);
        public static readonly Color LightOn = new Color(1f, 1f, 1f, 0.95f);
        public static readonly Color LightOff = new Color(1f, 1f, 1f, 0.28f);

        /// <summary>Short wave marks scattered over a tile of sea, drawn twice side by side so the tile can loop.</summary>
        public static Mesh SeaWaves(Vector2 tileSize, int marks, int seed)
        {
            var b = new MeshBuilder();
            for (int copy = 0; copy < 2; copy++)
            {
                var r = new System.Random(seed);
                for (int i = 0; i < marks; i++)
                {
                    float x = (float)r.NextDouble() * tileSize.x + copy * tileSize.x - tileSize.x;
                    float y = (float)r.NextDouble() * tileSize.y - tileSize.y / 2f;
                    float w = 0.18f + (float)r.NextDouble() * 0.22f;
                    b.Bar(new Vector2(x - w / 2f, y), new Vector2(x, y + 0.03f), 0.025f, WaveLight);
                    b.Bar(new Vector2(x, y + 0.03f), new Vector2(x + w / 2f, y), 0.025f, WaveLight);
                }
            }
            return b.Build("SeaWaves");
        }

        /// <summary>A soft white ring just outside the island's sand, for foam along the beach.</summary>
        public static Mesh FoamRing(Vector2 radii)
        {
            var b = new MeshBuilder();
            const int segments = 72;
            for (int i = 0; i < segments; i++)
            {
                float a0 = 2f * Mathf.PI * i / segments;
                float a1 = 2f * Mathf.PI * (i + 1) / segments;
                Vector2 d0 = new Vector2(Mathf.Cos(a0) * radii.x, Mathf.Sin(a0) * radii.y);
                Vector2 d1 = new Vector2(Mathf.Cos(a1) * radii.x, Mathf.Sin(a1) * radii.y);
                b.Quad(d0 * 0.995f, d0 * 1.035f, d1 * 1.035f, d1 * 0.995f, Foam);
            }
            return b.Build("Foam");
        }

        /// <summary>Rocks along the sand band of the island, placed the same way every time.</summary>
        public static Mesh BeachRocks(Vector2 islandCentre, Vector2 radii, int count, int seed)
        {
            var b = new MeshBuilder();
            var r = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                float angle = (float)(r.NextDouble() * 2.0 * Mathf.PI);
                float band = 0.9f + (float)r.NextDouble() * 0.06f;
                Vector2 p = islandCentre + new Vector2(Mathf.Cos(angle) * radii.x, Mathf.Sin(angle) * radii.y) * band;
                float size = 0.05f + (float)r.NextDouble() * 0.06f;
                b.Ellipse(p, new Vector2(size * 1.3f, size), 10, RockShade);
                b.Ellipse(p + new Vector2(-size * 0.2f, size * 0.15f), new Vector2(size, size * 0.75f), 10, Rock);
            }
            return b.Build("Rocks");
        }

        public static Mesh PalmTrunk() => new MeshBuilder().Ellipse(Vector2.zero, new Vector2(0.05f, 0.05f), 10, Trunk).Build("PalmTrunk");

        /// <summary>Palm fronds seen from above: seven leaves around the top, swaying as one.</summary>
        public static Mesh PalmFronds(int seed)
        {
            var b = new MeshBuilder();
            var r = new System.Random(seed);
            const int leaves = 7;
            float start = (float)r.NextDouble() * 360f;
            for (int i = 0; i < leaves; i++)
            {
                float angle = (start + i * 360f / leaves) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var side = new Vector2(-dir.y, dir.x);
                float length = 0.26f + (float)r.NextDouble() * 0.06f;
                Color c = i % 2 == 0 ? Frond : FrondLight;
                b.Triangle(side * 0.04f, dir * length, -side * 0.04f, c);
                b.Triangle(dir * length * 0.45f + side * 0.06f, dir * length, dir * length * 0.45f - side * 0.02f, c);
            }
            b.Ellipse(Vector2.zero, new Vector2(0.05f, 0.05f), 10, Trunk);
            return b.Build("PalmFronds");
        }

        public static Mesh ApronMesh(Vector2 size) => new MeshBuilder().Rect(Vector2.zero, size, Apron).Build("Apron");

        /// <summary>A taxiway along a polyline, with a light dashed centreline (never yellow: that colour means "needs a line").</summary>
        public static void AddTaxiway(MeshBuilder b, IReadOnlyList<Vector2> points, float width)
        {
            for (int i = 1; i < points.Count; i++)
            {
                b.Bar(points[i - 1], points[i], width, Taxiway);
                b.Ellipse(points[i], new Vector2(width / 2f, width / 2f), 12, Taxiway);
            }
            b.Ellipse(points[0], new Vector2(width / 2f, width / 2f), 12, Taxiway);
            for (int i = 1; i < points.Count; i++)
                MapShapes.BuildDashes(b, points[i - 1], points[i], TaxiLine, 0.1f, 0.1f, 0.025f);
        }

        /// <summary>A hold line: two bars across the taxiway, just before the runway.</summary>
        public static void AddHoldLine(MeshBuilder b, Vector2 at, Vector2 along, float width)
        {
            Vector2 side = new Vector2(-along.y, along.x) * (width / 2f);
            b.Bar(at - side, at + side, 0.03f, TaxiLine);
            b.Bar(at - side + along * 0.06f, at + side + along * 0.06f, 0.03f, TaxiLine);
        }

        public static Mesh Terminal(Vector2 size)
        {
            var b = new MeshBuilder().Rect(Vector2.zero, size, Building);
            b.Rect(new Vector2(0f, size.y * 0.15f), new Vector2(size.x * 0.96f, size.y * 0.35f), Roof);
            for (float x = -size.x / 2f + 0.15f; x < size.x / 2f - 0.1f; x += 0.22f)
                b.Rect(new Vector2(x, -size.y * 0.25f), new Vector2(0.12f, 0.06f), Glass);
            return b.Build("Terminal");
        }

        public static Mesh ControlTower()
        {
            return new MeshBuilder()
                .Ellipse(Vector2.zero, new Vector2(0.17f, 0.17f), 20, Roof)
                .Ring(Vector2.zero, 0.1f, 0.15f, 20, Glass)
                .Ellipse(Vector2.zero, new Vector2(0.08f, 0.08f), 16, Building)
                .Build("ControlTower");
        }

        public static Mesh ParkedPlane() => AircraftShape.CreateMesh(AircraftSilhouette.Airliner, Parked, 0.8f);

        public static Mesh WindsockPole() => new MeshBuilder().Ellipse(Vector2.zero, new Vector2(0.04f, 0.04f), 10, Roof).Build("WindsockPole");

        /// <summary>A windsock cone along local +x, in white and orange stripes.</summary>
        public static Mesh WindsockCone()
        {
            var b = new MeshBuilder();
            const int stripes = 5;
            for (int i = 0; i < stripes; i++)
            {
                float x0 = 0.04f + i * 0.07f;
                float x1 = x0 + 0.07f;
                float h0 = 0.07f - i * 0.009f;
                float h1 = 0.07f - (i + 1) * 0.009f;
                b.Quad(new Vector2(x0, -h0), new Vector2(x1, -h1), new Vector2(x1, h1), new Vector2(x0, h0),
                       i % 2 == 0 ? WindsockStripe : Color.white);
            }
            return b.Build("WindsockCone");
        }

        /// <summary>A small boat along local +x, with a short white wake behind it.</summary>
        public static Mesh Boat()
        {
            var b = new MeshBuilder();
            b.Bar(new Vector2(-0.2f, 0f), new Vector2(-0.6f, -0.12f), 0.025f, Wake);
            b.Bar(new Vector2(-0.2f, 0f), new Vector2(-0.6f, 0.12f), 0.025f, Wake);
            b.Quad(new Vector2(-0.2f, -0.07f), new Vector2(0.12f, -0.07f), new Vector2(0.12f, 0.07f), new Vector2(-0.2f, 0.07f), Hull);
            b.Triangle(new Vector2(0.12f, -0.07f), new Vector2(0.22f, 0f), new Vector2(0.12f, 0.07f), Hull);
            b.Rect(new Vector2(-0.05f, 0f), new Vector2(0.14f, 0.08f), Deck);
            return b.Build("Boat");
        }

        /// <summary>The carrier's wake: a white V spreading behind the stern (local -x).</summary>
        public static Mesh CarrierWake(float shipLength)
        {
            float stern = -shipLength / 2f;
            var b = new MeshBuilder();
            b.Bar(new Vector2(stern, -0.25f), new Vector2(stern - 1.4f, -0.7f), 0.06f, Wake);
            b.Bar(new Vector2(stern, 0.25f), new Vector2(stern - 1.4f, 0.7f), 0.06f, Wake);
            b.Bar(new Vector2(stern, 0f), new Vector2(stern - 1f, 0f), 0.14f, new Color(1f, 1f, 1f, 0.3f));
            return b.Build("CarrierWake");
        }

        public static Mesh CloudMesh(int seed, bool shadow)
        {
            var b = new MeshBuilder();
            var r = new System.Random(seed);
            Color c = shadow ? CloudShadow : Cloud;
            for (int i = 0; i < 5; i++)
            {
                Vector2 p = new Vector2((float)(r.NextDouble() - 0.5) * 1.1f, (float)(r.NextDouble() - 0.5) * 0.4f);
                float s = 0.25f + (float)r.NextDouble() * 0.2f;
                b.Ellipse(p, new Vector2(s * 1.3f, s), 18, c);
            }
            return b.Build(shadow ? "CloudShadow" : "Cloud");
        }

        /// <summary>A bird seen from above: two wings in a shallow V.</summary>
        public static Mesh BirdMesh()
        {
            return new MeshBuilder()
                .Bar(new Vector2(0f, 0f), new Vector2(-0.06f, 0.09f), 0.022f, Bird)
                .Bar(new Vector2(0f, 0f), new Vector2(-0.06f, -0.09f), 0.022f, Bird)
                .Build("Bird");
        }

        public static Mesh LightDot(float radius, Color color) =>
            new MeshBuilder().Ellipse(Vector2.zero, new Vector2(radius, radius), 10, color).Build("Light");
    }
}
