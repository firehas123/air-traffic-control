using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// Debug overlay for the "Zones" button (owner request, 20 Nov 2026): outlines every landing zone, gate, taxi route,
    /// hold and line-up point, crossing point, spawn point, the carrier lane and the area the camera keeps visible, and
    /// everything in the picture's measured layout (stands, taxi routes, hold lines, crossings, runway intersections,
    /// map-rule areas), on top of the painted map, so the owner can check the level data lines up with the picture. Magenta, which is not a
    /// status colour. Off by default; it never affects play.
    /// </summary>
    public sealed class ZoneDebugView : MonoBehaviour
    {
        const int Order = 30;
        static readonly Color Line = new Color(1f, 0.25f, 1f, 0.95f);
        static readonly Color Faint = new Color(1f, 0.25f, 1f, 0.55f);
        const float Thickness = 0.03f;

        LevelData level;
        LandingSystem landing;
        Camera worldCamera;
        GameObject view;
        Mesh mesh;
        float rebuildAt;

        public bool IsShown => view != null && view.activeSelf;

        public void Initialise(LevelData levelData, LandingSystem landingSystem, Camera camera, Material material)
        {
            level = levelData;
            landing = landingSystem;
            worldCamera = camera;
            mesh = new Mesh { name = "ZoneDebug" };
            view = MeshBuilder.CreateObject("ZoneDebug", mesh, material, Order, Vector2.zero, 0f);
            view.transform.SetParent(transform, true);
            view.SetActive(false);
        }

        public void Toggle()
        {
            view.SetActive(!view.activeSelf);
            if (view.activeSelf)
                Rebuild();
        }

        void Update()
        {
            // The touchdown zones move with the wind, so redraw now and then while shown.
            if (IsShown && Time.unscaledTime >= rebuildAt)
                Rebuild();
        }

        void Rebuild()
        {
            rebuildAt = Time.unscaledTime + 0.5f;
            var b = new MeshBuilder();

            foreach (Runway runway in landing.Runways)
            {
                Outline(b, runway.Centre, runway.Direction, runway.Length, runway.Width, Faint);
                ZoneOutline(b, runway, Line, false);
                ZoneOutline(b, runway.Reversed(), Faint, true);
            }
            foreach (LandingPad pad in landing.Pads)
                Circle(b, pad.Centre, pad.Radius, Line);

            GroundLayout ground = level.ground;
            Polyline(b, ground.mainTaxiway, Line);
            if (ground.mainTaxiway.Count > 0)
            {
                Square(b, ground.mainTaxiway[0], 0.1f);
                Square(b, ground.mainTaxiway[ground.mainTaxiway.Count - 1], 0.1f);
            }
            if (ground.useLineUpPoints)
            {
                Diamond(b, ground.normalLineUp, 0.12f);
                Diamond(b, ground.reversedLineUp, 0.12f);
            }
            foreach (GateDefinition gate in ground.gates)
            {
                Polyline(b, gate.connector, Line);
                Circle(b, gate.position, 0.3f, Line);
                float r = gate.headingDegrees * Mathf.Deg2Rad;
                b.Bar(gate.position, gate.position + new Vector2(Mathf.Cos(r), Mathf.Sin(r)) * 0.4f, Thickness, Line);
                if (gate.crossingIndex >= 0 && gate.crossingIndex < gate.connector.Count)
                    Cross(b, gate.connector[gate.crossingIndex], 0.16f);
            }

            Rect cameraView = CameraFraming.CurrentView(worldCamera);
            foreach (SpawnPoint spawn in level.spawnPoints)
                Diamond(b, SpawnRules.ClampToView(spawn.position, cameraView, 0.35f), 0.15f);

            CarrierSettings carrier = level.carrier;
            if (carrier.hasCarrier)
                MapShapes.BuildDashes(b, new Vector2(cameraView.xMin, carrier.laneY), new Vector2(cameraView.xMax, carrier.laneY),
                                      Faint, 0.25f, 0.15f, Thickness);

            Rect keep = level.mapArt.keepVisible;
            if (level.mapArt.HasPicture)
                Outline(b, keep.center, Vector2.right, keep.width, keep.height, Faint);

            if (level.layout != null)
                AddLayout(b, level.layout);

            b.Fill(mesh);
        }

        // Everything measured on the picture (blueprint 16): all taxi routes, stands, hold lines, crossing points,
        // runway intersections and map-rule areas, including the ones the current gameplay does not use yet.
        static void AddLayout(MeshBuilder b, PictureLayout layout)
        {
            foreach (PicturePath route in layout.taxiRoutes)
                Polyline(b, route.points.ConvertAll(layout.World), Faint);
            foreach (PictureStand stand in layout.stands)
            {
                Vector2 at = layout.World(stand.position);
                Circle(b, at, 0.14f, Faint);
                float r = stand.headingDegrees * Mathf.Deg2Rad;
                b.Bar(at, at + new Vector2(Mathf.Cos(r), Mathf.Sin(r)) * 0.22f, Thickness, Faint);
            }
            foreach (PictureHoldLine hold in layout.holdLines)
                Square(b, layout.World(hold.point), 0.06f);
            foreach (PictureCrossing crossing in layout.crossings)
                Cross(b, layout.World(crossing.point), 0.1f);
            foreach (PictureIntersection intersection in layout.intersections)
                Diamond(b, layout.World(intersection.point), 0.1f);
            foreach (PictureArea area in layout.ruleAreas)
            {
                List<Vector2> points = area.points.ConvertAll(layout.World);
                if (points.Count > 1)
                    points.Add(points[0]);
                Polyline(b, points, Faint);
            }
        }

        static void ZoneOutline(MeshBuilder b, Runway runway, Color color, bool dashed)
        {
            float into = runway.TouchdownZoneStart + runway.TouchdownZoneLength / 2f;
            Vector2 centre = runway.Threshold + runway.Direction * into;
            if (!dashed)
            {
                Outline(b, centre, runway.Direction, runway.TouchdownZoneLength, runway.Width, color);
                return;
            }
            Vector2 along = runway.Direction * (runway.TouchdownZoneLength / 2f);
            Vector2 side = new Vector2(-runway.Direction.y, runway.Direction.x) * (runway.Width / 2f);
            Vector2[] c = { centre - along - side, centre + along - side, centre + along + side, centre - along + side };
            for (int i = 0; i < 4; i++)
                MapShapes.BuildDashes(b, c[i], c[(i + 1) % 4], color, 0.06f, 0.05f, Thickness);
        }

        static void Outline(MeshBuilder b, Vector2 centre, Vector2 direction, float length, float width, Color color)
        {
            Vector2 along = direction.normalized * (length / 2f);
            Vector2 side = new Vector2(-direction.y, direction.x).normalized * (width / 2f);
            Vector2[] c = { centre - along - side, centre + along - side, centre + along + side, centre - along + side };
            for (int i = 0; i < 4; i++)
                b.Bar(c[i], c[(i + 1) % 4], Thickness, color);
        }

        static void Polyline(MeshBuilder b, IReadOnlyList<Vector2> points, Color color)
        {
            for (int i = 1; i < points.Count; i++)
                b.Bar(points[i - 1], points[i], Thickness, color);
        }

        static void Circle(MeshBuilder b, Vector2 centre, float radius, Color color) =>
            b.Ring(centre, radius - Thickness / 2f, radius + Thickness / 2f, 40, color);

        static void Square(MeshBuilder b, Vector2 at, float half) =>
            Outline(b, at, Vector2.right, half * 2f, half * 2f, Line);

        static void Diamond(MeshBuilder b, Vector2 at, float r)
        {
            Vector2[] c = { at + new Vector2(r, 0f), at + new Vector2(0f, r), at + new Vector2(-r, 0f), at + new Vector2(0f, -r) };
            for (int i = 0; i < 4; i++)
                b.Bar(c[i], c[(i + 1) % 4], Thickness, Line);
        }

        static void Cross(MeshBuilder b, Vector2 at, float r)
        {
            b.Bar(at + new Vector2(-r, -r), at + new Vector2(r, r), Thickness * 1.5f, Line);
            b.Bar(at + new Vector2(-r, r), at + new Vector2(r, -r), Thickness * 1.5f, Line);
        }
    }
}
