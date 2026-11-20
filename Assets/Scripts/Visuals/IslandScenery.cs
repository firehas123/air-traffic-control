using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// The Island map's scenery and its animation (blueprint 15.3): sea waves and beach foam, rocks and swaying palms,
    /// the terminal with its control tower, the apron with parked planes, taxiways and hold lines, a windsock that
    /// shows the wind, boats, drifting clouds with soft shadows, and a few birds. Decoration only: it never affects
    /// play. Each animated group moves as one transform, so it costs little per frame (60 FPS on the iPad).
    ///
    /// With a painted map (art rule of 20 Nov 2026) the picture already shows the sea, island, beach, rocks, palms,
    /// terminal, tower, apron and taxiways, so only the overlays are drawn: windsock, boats, clouds with shadows, birds.
    /// </summary>
    public sealed class IslandScenery : MonoBehaviour
    {
        struct Mover
        {
            public Transform transform;
            public Transform shadow;
            public float speed;
            public float y;
            public float phase;
        }

        Camera worldCamera;
        LandingSystem landing;
        Transform seaNear;
        Transform seaFar;
        Vector2 seaTile;
        Transform foam;
        readonly List<Transform> palmFronds = new List<Transform>();
        Transform windsock;
        float windsockAngle;
        readonly List<Mover> boats = new List<Mover>();
        readonly List<Mover> clouds = new List<Mover>();
        readonly List<Mover> birds = new List<Mover>();
        float screenHalfWidth;
        float time;

        public void Initialise(LevelData level, Camera camera, LandingSystem landingSystem, Material material, bool paintedMap = false)
        {
            worldCamera = camera;
            landing = landingSystem;
            screenHalfWidth = camera.orthographicSize * camera.aspect;
            SceneryLayout layout = level.scenery;

            if (paintedMap)
            {
                BuildOverlays(layout, material);
                Place(0f);
                return;
            }

            // Sea: two layers of wave marks drifting at different speeds give a gentle shimmer.
            seaTile = new Vector2(screenHalfWidth * 2f + 2f, camera.orthographicSize * 2f + 1f);
            seaFar = Create("SeaFar", SceneryShapes.SeaWaves(seaTile, 45, 11), material, SceneryShapes.SeaOrder, Vector2.zero).transform;
            seaNear = Create("SeaNear", SceneryShapes.SeaWaves(seaTile, 30, 23), material, SceneryShapes.SeaOrder, new Vector2(0.3f, 0.2f)).transform;

            foam = Create("Foam", SceneryShapes.FoamRing(level.islandRadii), material, SceneryShapes.FoamOrder, level.islandCentre).transform;

            Create("Rocks", SceneryShapes.BeachRocks(level.islandCentre, level.islandRadii, layout.rocks, 5), material,
                   SceneryShapes.BeachDetailOrder, Vector2.zero);

            // Apron, terminal, tower and parked planes.
            Create("Apron", SceneryShapes.ApronMesh(layout.apronSize), material, SceneryShapes.ApronOrder, layout.apronCentre);
            Create("Terminal", SceneryShapes.Terminal(layout.terminalSize), material, SceneryShapes.BuildingOrder, layout.terminalCentre);
            Create("ControlTower", SceneryShapes.ControlTower(), material, SceneryShapes.BuildingOrder + 1, layout.towerPosition);
            foreach (Vector2 stand in layout.parkedPlanes)
            {
                GameObject parked = Create("ParkedPlane", SceneryShapes.ParkedPlane(), material, SceneryShapes.BuildingOrder, stand);
                parked.transform.rotation = Quaternion.Euler(0f, 0f, 270f);
            }

            BuildTaxiways(level, material);

            // Palms: the trunk stays, the fronds sway.
            for (int i = 0; i < layout.palms.Count; i++)
            {
                Create("PalmTrunk", SceneryShapes.PalmTrunk(), material, SceneryShapes.PalmOrder, layout.palms[i]);
                GameObject fronds = Create("PalmFronds", SceneryShapes.PalmFronds(100 + i), material, SceneryShapes.PalmOrder + 1, layout.palms[i]);
                palmFronds.Add(fronds.transform);
            }

            BuildOverlays(layout, material);
            Place(0f);
        }

        // Windsock, boats, clouds and birds: drawn over a painted map too.
        void BuildOverlays(SceneryLayout layout, Material material)
        {
            // Windsock: a pole and a cone that points the way the wind blows.
            Create("WindsockPole", SceneryShapes.WindsockPole(), material, SceneryShapes.WindsockOrder + 1, layout.windsockPosition);
            windsock = Create("Windsock", SceneryShapes.WindsockCone(), material, SceneryShapes.WindsockOrder, layout.windsockPosition).transform;
            windsockAngle = WindBlowsTowards();
            windsock.rotation = Quaternion.Euler(0f, 0f, windsockAngle);

            Rect view = CameraFraming.CurrentView(worldCamera);
            var random = new System.Random(7);
            for (int i = 0; i < layout.boatLanes.Count; i++)
            {
                GameObject boat = Create("Boat", SceneryShapes.Boat(), material, SceneryShapes.BoatOrder, Vector2.zero);
                bool right = i % 2 == 0;
                boat.transform.rotation = Quaternion.Euler(0f, 0f, right ? 0f : 180f);
                boats.Add(new Mover { transform = boat.transform, y = layout.boatLanes[i], speed = right ? 1f : -1f,
                                      phase = (float)random.NextDouble() * 10f });
            }

            for (int i = 0; i < layout.clouds; i++)
            {
                GameObject shadow = Create("CloudShadow", SceneryShapes.CloudMesh(200 + i, true), material, SceneryShapes.CloudShadowOrder, Vector2.zero);
                GameObject cloud = Create("Cloud", SceneryShapes.CloudMesh(200 + i, false), material, SceneryShapes.CloudOrder, Vector2.zero);
                clouds.Add(new Mover { transform = cloud.transform, shadow = shadow.transform,
                                       y = view.yMin + 1f + (view.height - 2f) * (i + 0.5f) / Mathf.Max(1, layout.clouds),
                                       speed = 0.7f + 0.6f * (float)random.NextDouble(), phase = (float)random.NextDouble() * 20f });
            }

            for (int i = 0; i < layout.birds; i++)
            {
                GameObject bird = Create("Bird", SceneryShapes.BirdMesh(), material, SceneryShapes.BirdOrder, Vector2.zero);
                birds.Add(new Mover { transform = bird.transform, y = view.yMax - 2.4f - i * 0.35f, speed = 0.9f + 0.2f * i,
                                      phase = i * 0.7f });
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
            AnimationSettings a = Animations.Settings;
            bool on = Animations.On;

            // The windsock always shows the current wind; it swings round smoothly when animations are on.
            float target = WindBlowsTowards();
            windsockAngle = on ? Mathf.MoveTowardsAngle(windsockAngle, target, a.windsockTurnDegreesPerSecond * Time.deltaTime) : target;
            float flutter = on ? AnimationMath.Sway(Time.unscaledTime, a.windsockFlutterDegrees, 0.7f) : 0f;
            windsock.rotation = Quaternion.Euler(0f, 0f, windsockAngle + flutter);

            if (!on)
                return;

            time += Time.deltaTime;
            Place(time);
        }

        void Place(float t)
        {
            AnimationSettings a = Animations.Settings;

            if (seaFar != null)
            {
                seaFar.position = new Vector2(AnimationMath.Wrap(t * a.waveDriftSpeed * 0.6f, 0f, seaTile.x), 0f);
                seaNear.position = new Vector2(AnimationMath.Wrap(-t * a.waveDriftSpeed, 0f, seaTile.x) + 0.3f, 0.2f);
            }

            if (foam != null)
            {
                float foamScale = 1f + AnimationMath.Sway(t, a.foamPulse, a.foamPulseSeconds);
                foam.localScale = new Vector3(foamScale, foamScale, 1f);
            }

            for (int i = 0; i < palmFronds.Count; i++)
                palmFronds[i].rotation = Quaternion.Euler(0f, 0f, AnimationMath.Sway(t, a.palmSwayDegrees, a.palmSwaySeconds, i * 0.17f));

            float min = -screenHalfWidth - 1.5f;
            float max = screenHalfWidth + 1.5f;
            foreach (Mover boat in boats)
                boat.transform.position = new Vector2(AnimationMath.Wrap(boat.phase + t * a.boatSpeed * boat.speed, min, max), boat.y);
            foreach (Mover cloud in clouds)
            {
                var p = new Vector2(AnimationMath.Wrap(cloud.phase + t * a.cloudSpeed * cloud.speed, min, max), cloud.y);
                cloud.transform.position = p;
                cloud.shadow.position = p + a.cloudShadowOffset;
            }
            foreach (Mover bird in birds)
            {
                bird.transform.position = new Vector2(AnimationMath.Wrap(bird.phase * 5f + t * a.birdSpeed * bird.speed, min, max),
                                                      bird.y + AnimationMath.Sway(t, 0.08f, 3f, bird.phase));
                float flap = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Mathf.PI * (t / a.birdFlapSeconds + bird.phase)));
                bird.transform.localScale = new Vector3(1f, flap, 1f);
            }
        }
    }
}
