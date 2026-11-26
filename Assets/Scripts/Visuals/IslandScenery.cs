using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// The Island map's scenery. With a painted map (art rule of 20 Nov 2026) the picture shows everything, so only the
    /// windsock is drawn: it gives information (the wind, C1). Code-drawn scenery is never drawn over a picture.
    /// Without a picture (old test levels) the code-drawn island, sea marks, rocks, palms, terminal and taxiways are
    /// drawn, standing still. The owner removed the moving decoration on 20 and 25 Nov 2026 (waves, foam, swaying
    /// palms, boats, clouds and birds). Decoration only: it never affects play.
    /// </summary>
    public sealed class IslandScenery : MonoBehaviour
    {
        LandingSystem landing;
        Transform windsock;
        float windsockAngle;

        public void Initialise(LevelData level, Camera camera, LandingSystem landingSystem, Material material, bool paintedMap = false)
        {
            landing = landingSystem;
            SceneryLayout layout = level.scenery;

            if (!paintedMap)
            {
                var seaTile = new Vector2(camera.orthographicSize * camera.aspect * 2f + 2f, camera.orthographicSize * 2f + 1f);
                Create("SeaFar", SceneryShapes.SeaWaves(seaTile, 45, 11), material, SceneryShapes.SeaOrder, Vector2.zero);
                Create("SeaNear", SceneryShapes.SeaWaves(seaTile, 30, 23), material, SceneryShapes.SeaOrder, new Vector2(0.3f, 0.2f));
                Create("Foam", SceneryShapes.FoamRing(level.islandRadii), material, SceneryShapes.FoamOrder, level.islandCentre);
                Create("Rocks", SceneryShapes.BeachRocks(level.islandCentre, level.islandRadii, layout.rocks, 5), material,
                       SceneryShapes.BeachDetailOrder, Vector2.zero);

                Create("Apron", SceneryShapes.ApronMesh(layout.apronSize), material, SceneryShapes.ApronOrder, layout.apronCentre);
                Create("Terminal", SceneryShapes.Terminal(layout.terminalSize), material, SceneryShapes.BuildingOrder, layout.terminalCentre);
                Create("ControlTower", SceneryShapes.ControlTower(), material, SceneryShapes.BuildingOrder + 1, layout.towerPosition);
                foreach (Vector2 stand in layout.parkedPlanes)
                {
                    GameObject parked = Create("ParkedPlane", SceneryShapes.ParkedPlane(), material, SceneryShapes.BuildingOrder, stand);
                    parked.transform.rotation = Quaternion.Euler(0f, 0f, 270f);
                }

                BuildTaxiways(level, material);

                for (int i = 0; i < layout.palms.Count; i++)
                {
                    Create("PalmTrunk", SceneryShapes.PalmTrunk(), material, SceneryShapes.PalmOrder, layout.palms[i]);
                    Create("PalmFronds", SceneryShapes.PalmFronds(100 + i), material, SceneryShapes.PalmOrder + 1, layout.palms[i]);
                }
            }

            // Only on maps with the wind rule: a pole and a cone that points the way the wind blows.
            if (level.wind.hasWind)
            {
                Create("WindsockPole", SceneryShapes.WindsockPole(), material, SceneryShapes.WindsockOrder + 1, layout.windsockPosition);
                windsock = Create("Windsock", SceneryShapes.WindsockCone(), material, SceneryShapes.WindsockOrder, layout.windsockPosition).transform;
                windsockAngle = WindBlowsTowards();
                windsock.rotation = Quaternion.Euler(0f, 0f, windsockAngle);
            }
        }

        void BuildTaxiways(LevelData level, Material material)
        {
            GroundLayout ground = level.ground;
            const float width = 0.26f;
            var b = new MeshBuilder();
            if (ground.mainTaxiway.Count >= 2)
                SceneryShapes.AddTaxiway(b, ground.mainTaxiway, width);
            foreach (GateDefinition gate in ground.gates)
            {
                if (gate.connector.Count >= 2)
                    SceneryShapes.AddTaxiway(b, gate.connector, width);
                b.Ellipse(gate.position, new Vector2(0.32f, 0.32f), 20, new Color(0.5f, 0.52f, 0.55f));
            }

            // Hold lines where the main taxiway meets each runway end, and before each crossing point.
            if (ground.mainTaxiway.Count >= 2 && level.runways.Count > 0)
            {
                Runway runway = level.runways[0].ToRunway();
                foreach (Vector2 end in new[] { ground.mainTaxiway[0], ground.mainTaxiway[ground.mainTaxiway.Count - 1] })
                {
                    Vector2 towardRunway = (new Vector2(end.x, runway.Centre.y) - end).normalized;
                    SceneryShapes.AddTaxiway(b, new[] { end, new Vector2(end.x, runway.Centre.y) }, width);
                    SceneryShapes.AddHoldLine(b, end + towardRunway * 0.12f, towardRunway, width);
                }
            }
            foreach (GateDefinition gate in ground.gates)
            {
                if (gate.crossingIndex >= 0 && gate.crossingIndex + 1 < gate.connector.Count)
                {
                    Vector2 at = gate.connector[gate.crossingIndex];
                    Vector2 along = (gate.connector[gate.crossingIndex + 1] - at).normalized;
                    SceneryShapes.AddHoldLine(b, at + along * 0.1f, along, width);
                }
            }
            Create("Taxiways", b.Build("Taxiways"), material, SceneryShapes.TaxiwayOrder, Vector2.zero);
        }

        GameObject Create(string name, Mesh mesh, Material material, int order, Vector2 position)
        {
            GameObject go = MeshBuilder.CreateObject(name, mesh, material, order, position, 0f);
            go.transform.SetParent(transform, true);
            return go;
        }

        // Wind blows opposite to the landing direction: aircraft land into the wind.
        float WindBlowsTowards() => landing != null && landing.Runways.Count > 0 ? landing.Runways[0].LandingHeadingDegrees + 180f : 180f;

        void Update()
        {
            if (windsock == null)
                return;
            // The windsock always shows the current wind, and swings round smoothly when it changes.
            float target = WindBlowsTowards();
            windsockAngle = Animations.On
                ? Mathf.MoveTowardsAngle(windsockAngle, target, Animations.Settings.windsockTurnDegreesPerSecond * Time.deltaTime)
                : target;
            windsock.rotation = Quaternion.Euler(0f, 0f, windsockAngle);
        }
    }
}
