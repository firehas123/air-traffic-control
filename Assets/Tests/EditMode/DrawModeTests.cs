using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AirTrafficControl.Tests
{
    /// <summary>Shared access to the Draw levels of phase 2 (blueprint 16): 5 levels on each of maps 1 to 5.</summary>
    static class DrawLevels
    {
        public static readonly string[] Maps = { "island", "city", "desert", "mountain", "arctic" };

        public static MapCatalog Catalog() => AssetDatabase.LoadAssetAtPath<MapCatalog>("Assets/Maps/MapCatalog.asset");

        public static LevelData Level(string map, int number)
        {
            string id = MapInfo.LevelId(map, GameMode.Draw, number);
            var level = AssetDatabase.LoadAssetAtPath<LevelData>($"Assets/Levels/Draw/{id}.asset");
            Assert.IsNotNull(level, $"{id} exists (Tools > Air Traffic Control > Build Draw Levels)");
            return level;
        }

        public static IEnumerable<LevelData> All()
        {
            foreach (string map in Maps)
                for (int n = 1; n <= 5; n++)
                    yield return Level(map, n);
        }
    }

    /// <summary>The 25 Draw levels as data: catalog, rules from level 2, kinds introduced gradually, C3 length.</summary>
    public class DrawLevelDataTests
    {
        [Test]
        public void Catalog_HasFiveDrawLevelsWithData_OnEachOfTheFirstFiveMaps()
        {
            MapCatalog catalog = DrawLevels.Catalog();
            for (int m = 0; m < DrawLevels.Maps.Length; m++)
            {
                MapEntry map = catalog.maps[m];
                Assert.AreEqual(DrawLevels.Maps[m], map.id);
                Assert.AreEqual(5, map.drawLevels.Count);
                for (int n = 0; n < 5; n++)
                {
                    LevelEntry entry = map.drawLevels[n];
                    Assert.IsNotNull(entry.data, entry.id);
                    Assert.AreEqual(entry.id, entry.data.name, "The asset is named by its stable id");
                    Assert.AreEqual(GameMode.Draw, entry.data.mode, entry.id);
                    Assert.AreEqual(map.id, entry.data.mapId, entry.id);
                    Assert.AreSame(map.drawLayout, entry.data.layout, $"{entry.id} is on the map's Draw picture");
                    Assert.AreSame(map.drawPicture, entry.data.mapArt.sprite, entry.id);
                    Assert.IsFalse(string.IsNullOrEmpty(entry.title), entry.id);
                }
            }
            for (int m = 5; m < catalog.maps.Count; m++)
                Assert.IsTrue(catalog.maps[m].drawLevels.TrueForAll(l => l.data == null), "Maps 6 and 7 are not built");
        }

        [Test]
        public void NoGroundControl_InDrawMode()
        {
            foreach (LevelData level in DrawLevels.All())
            {
                Assert.AreEqual(0, level.departuresToClear, level.name);
                Assert.AreEqual(0, level.ground.gates.Count, level.name);
                Assert.Greater(level.landingsToClear, 0, level.name);
            }
        }

        [Test]
        public void EachMapsRule_IsOffOnLevel1_AndOnFromLevel2()
        {
            foreach (string map in DrawLevels.Maps)
                for (int n = 1; n <= 5; n++)
                {
                    LevelData level = DrawLevels.Level(map, n);
                    bool on = n >= 2;
                    Assert.AreEqual(map == "island" && on, level.wind.hasWind, $"{level.name} wind");
                    Assert.AreEqual(map == "city" && on, level.rules.closures.enabled, $"{level.name} closures");
                    Assert.AreEqual(map == "desert" && on, level.rules.fuel.enabled, $"{level.name} low fuel");
                    Assert.AreEqual(map == "desert" && n >= 3, level.rules.sandstorm.enabled, $"{level.name} sandstorm");
                    Assert.AreEqual(map == "mountain" && on, level.rules.noFly.enabled, $"{level.name} no-fly");
                    Assert.AreEqual(map == "arctic" && on, level.rules.ice.enabled, $"{level.name} ice");
                    Assert.AreEqual(map == "island" && n == 5, level.carrier.hasCarrier, $"{level.name} carrier");
                }
        }

        [Test]
        public void MapRuleValues_FollowTheSpec()
        {
            LevelData island = DrawLevels.Level("island", 2);
            Assert.AreEqual(5f, island.wind.warningSeconds, "Wind: 5 s warning");
            Assert.AreEqual(1.5f, island.wind.landingGraceSeconds, "Wind: 1.5 s grace (14.3)");

            RunwayClosureSettings city = DrawLevels.Level("city", 2).rules.closures;
            Assert.AreEqual(5f, city.warningSeconds);
            Assert.AreEqual(10f, city.minClosedSeconds);
            Assert.AreEqual(15f, city.maxClosedSeconds);

            LevelData carrier = DrawLevels.Level("island", 5);
            Assert.IsNotNull(carrier.carrier.picture, "The carrier uses 15_carrier.png");
            Assert.AreEqual("15_carrier", carrier.carrier.picture.name);

            NoFlySettings mountains = DrawLevels.Level("mountain", 2).rules.noFly;
            Assert.AreEqual(2, mountains.areas.Count, "North and south mountains from the picture");
        }

        [Test]
        public void AircraftKinds_AreIntroducedGradually()
        {
            // Blueprint 14.4: airliner first, then jumbo, helicopter, balloon and drone, the fighter and carrier last.
            var seen = new HashSet<AircraftType>();
            string[] islandNew = { "Airliner", "Jumbo jet", "Helicopter", "Hot air balloon|Drone", "Fighter jet" };
            for (int n = 1; n <= 5; n++)
            {
                LevelData level = DrawLevels.Level("island", n);
                var kinds = new HashSet<AircraftType>(level.aircraftTypes);
                Assert.IsTrue(kinds.IsSupersetOf(seen), $"{level.name} keeps the kinds already learnt");
                var added = new List<string>();
                foreach (AircraftType kind in kinds)
                    if (!seen.Contains(kind))
                        added.Add(kind.displayName);
                foreach (string name in added)
                    StringAssert.Contains(name, islandNew[n - 1], $"{level.name} brings in {name}");
                Assert.LessOrEqual(added.Count, 2, $"{level.name}: at most two new kinds");
                seen.UnionWith(kinds);
            }
            Assert.AreEqual(1, new HashSet<AircraftType>(DrawLevels.Level("island", 1).aircraftTypes).Count, "Island 1: airliners only");

            foreach (string map in DrawLevels.Maps)
            {
                if (map == "island")
                    continue;
                // Later maps: the first level never uses all six kinds; the kinds grow through the map.
                int previous = 0;
                for (int n = 1; n <= 5; n++)
                {
                    int count = new HashSet<AircraftType>(DrawLevels.Level(map, n).aircraftTypes).Count;
                    Assert.GreaterOrEqual(count, previous, $"{map} {n}");
                    previous = count;
                }
                Assert.Less(new HashSet<AircraftType>(DrawLevels.Level(map, 1).aircraftTypes).Count, 6, $"{map} 1");
            }
        }

        [Test]
        public void EveryKind_HasSomewhereToLand_OnEveryLevel()
        {
            foreach (LevelData level in DrawLevels.All())
                foreach (AircraftType kind in level.aircraftTypes)
                    Assert.IsTrue(SessionLengthEstimate.DistanceToNearestZone(level, kind.landsOn, Vector2.zero).HasValue,
                                  $"{level.name}: {kind.displayName}");
        }

        [Test]
        public void EveryLevel_IsEstimatedToTake60To120Seconds()
        {
            // C3: levels last 60 to 120 seconds (estimate at 1x; the owner times real play on the iPad).
            var lines = new List<string>();
            var wrong = new List<string>();
            foreach (LevelData level in DrawLevels.All())
            {
                float seconds = SessionLengthEstimate.Seconds(level, 0.6f);
                lines.Add($"{level.name}: {seconds:F0} s (2 stars within {level.result.twoStarSeconds:F0} s)");
                if (seconds < 60f || seconds > 120f || level.result.twoStarSeconds < seconds)
                    wrong.Add(lines[lines.Count - 1]);
            }
            TestContext.WriteLine(string.Join("\n", lines));
            Assert.IsEmpty(wrong, "Each level 60 to 120 s, with 2 stars reachable at the estimate:\n" + string.Join("\n", wrong));
        }

        [Test]
        public void LevelsGetHarderThroughEachMap()
        {
            foreach (string map in DrawLevels.Maps)
                for (int n = 2; n <= 5; n++)
                {
                    LevelData before = DrawLevels.Level(map, n - 1), level = DrawLevels.Level(map, n);
                    Assert.GreaterOrEqual(level.landingsToClear, before.landingsToClear, level.name);
                    Assert.GreaterOrEqual(level.aircraftInAir, before.aircraftInAir, level.name);
                }
        }

        [Test]
        public void SpawnPoints_AreOnThePicture_AndNeverInsideAMountain()
        {
            foreach (LevelData level in DrawLevels.All())
            {
                var mountains = new NoFlyZones(level.rules.noFly.areas);
                Rect picture = level.mapArt.WorldRect;
                foreach (SpawnPoint spawn in level.spawnPoints)
                {
                    Assert.IsTrue(picture.Contains(spawn.position), $"{level.name} {spawn.position}");
                    Assert.IsFalse(mountains.Contains(spawn.position), $"{level.name} {spawn.position} is in the valley");
                    float r = spawn.headingDegrees * Mathf.Deg2Rad;
                    Vector2 ahead = spawn.position + new Vector2(Mathf.Cos(r), Mathf.Sin(r)) * 2f;
                    Assert.IsFalse(mountains.Contains(ahead), $"{level.name}: it does not fly straight into a mountain");
                }
            }
        }
    }

    /// <summary>Every runway end and pad of each Draw picture takes a landing (blueprint 16.1).</summary>
    public class DrawLandingTargetTests
    {
        static List<Vector2> LineTo(Vector2 from, Vector2 to) => new List<Vector2> { from, Vector2.Lerp(from, to, 0.5f), to };

        [Test]
        public void EveryRunwayEnd_OfEveryDrawPicture_TakesALanding_FromAnyDirection()
        {
            foreach (string map in DrawLevels.Maps)
            {
                LevelData level = DrawLevels.Level(map, 1);
                PictureLayout layout = level.layout;
                Assert.AreEqual(layout.runways.Count, level.runways.Count, $"{map}: every painted runway is a target");
                for (int i = 0; i < level.runways.Count; i++)
                {
                    foreach (bool reversed in new[] { false, true })
                    {
                        Runway runway = reversed ? level.runways[i].ToRunway().Reversed() : level.runways[i].ToRunway();
                        var active = new List<Runway>();
                        foreach (RunwayDefinition d in level.runways)
                            active.Add(reversed ? d.ToRunway().Reversed() : d.ToRunway());
                        Vector2 zone = DrawModeSetup.ZoneCentre(level.runways[i], reversed);
                        string what = $"{map} {layout.runways[i].name} end {(reversed ? "B" : "A")}";

                        // From straight ahead, from the side and from beyond the far end: all land (any direction, 15 Nov 2026).
                        Vector2 side = new Vector2(-runway.Direction.y, runway.Direction.x);
                        foreach (Vector2 from in new[] { zone - runway.Direction * 3f, zone + side * 3f, zone - side * 3f, zone + runway.Direction * 3f })
                        {
                            Assert.IsTrue(LandingRules.TryFindTouchdown(active, new List<LandingPad>(), LineTo(from, zone), LandingZoneKind.Runway, out Touchdown t),
                                          $"{what} from {from}");
                            Assert.AreEqual(runway.Centre, t.Runway.Centre, what);
                        }

                        // The zone is on the painted runway, at this end, clear of any crossing runway.
                        PictureRunway painted = layout.runways[i];
                        Vector2 pixel = layout.Pixel(zone);
                        Vector2 a = reversed ? painted.endB : painted.endA;
                        Vector2 along = ((reversed ? painted.endA : painted.endB) - a).normalized;
                        float into = Vector2.Dot(pixel - a, along);
                        float across = Mathf.Abs(Vector2.Dot(pixel - a, new Vector2(-along.y, along.x)));
                        Assert.That(into, Is.InRange(0f, painted.Length / 2f), $"{what} zone is at this end");
                        Assert.Less(across, painted.width / 2f, $"{what} zone centre is on the asphalt");
                        foreach (PictureIntersection x in layout.intersections)
                            if (x.runwayA == painted.name || x.runwayB == painted.name)
                                Assert.IsFalse(runway.InTouchdownZone(layout.World(x.point)), $"{what} zone stops before the intersection");
                    }
                }
            }
        }

        [Test]
        public void AFingerWideMiss_StillLands_ButALineBesideTheRunwayDoesNot()
        {
            foreach (string map in DrawLevels.Maps)
            {
                LevelData level = DrawLevels.Level(map, 1);
                foreach (RunwayDefinition d in level.runways)
                {
                    Runway runway = d.ToRunway();
                    Vector2 zone = DrawModeSetup.ZoneCentre(d, false);
                    Vector2 side = new Vector2(-runway.Direction.y, runway.Direction.x);
                    var runways = new List<Runway> { runway };
                    Assert.IsTrue(LandingRules.TryFindTouchdown(runways, new List<LandingPad>(),
                        LineTo(zone + side * 3f, zone + side * 0.12f), LandingZoneKind.Runway, out _), $"{map}: 0.12 off the centreline lands");
                    Assert.IsFalse(LandingRules.TryFindTouchdown(runways, new List<LandingPad>(),
                        LineTo(zone + side * 3f, zone + side * 0.6f), LandingZoneKind.Runway, out _), $"{map}: 0.6 beside the runway does not");
                }
            }
        }

        [Test]
        public void EveryPad_TakesItsKind_AndOnlyItsKind()
        {
            foreach (string map in DrawLevels.Maps)
            {
                LevelData level = DrawLevels.Level(map, 1);
                var pads = new List<LandingPad>();
                foreach (LandingPadDefinition d in level.landingPads)
                    pads.Add(d.ToPad());
                Assert.IsTrue(pads.Exists(p => p.Kind == LandingZoneKind.Helipad), $"{map} helipad");
                Assert.IsTrue(pads.Exists(p => p.Kind == LandingZoneKind.BalloonField), $"{map} balloon field");
                foreach (LandingPad pad in pads)
                {
                    List<Vector2> line = LineTo(pad.Centre + new Vector2(2f, 2f), pad.Centre + new Vector2(0.1f, 0f));
                    Assert.IsTrue(LandingRules.TryFindTouchdown(new List<Runway>(), pads, line, pad.Kind, out _), $"{map} {pad.Kind}");
                    LandingZoneKind other = pad.Kind == LandingZoneKind.Helipad ? LandingZoneKind.BalloonField : LandingZoneKind.Helipad;
                    Assert.IsFalse(LandingRules.TryFindTouchdown(new List<Runway>(), pads, line, other, out _), $"{map} {pad.Kind} is not for {other}");
                    Assert.GreaterOrEqual(pad.Radius, DrawModeSetup.MinPadRadius);
                }
            }
        }
    }

    /// <summary>The landing system with closed runways (City and Desert rules).</summary>
    public class ClosedRunwayLandingTests
    {
        LandingSystem landing;
        LevelData level;

        [SetUp]
        public void SetUp()
        {
            level = DrawLevels.Level("city", 2);
            landing = new GameObject("Landing").AddComponent<LandingSystem>();
            landing.Initialise(level.runways, level.landingPads, null, null, true);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(landing.gameObject);
        }

        List<Vector2> LineToRunway(int index)
        {
            Vector2 zone = DrawModeSetup.ZoneCentre(level.runways[index], false);
            return new List<Vector2> { zone + new Vector2(0f, 3f), zone };
        }

        [Test]
        public void ALine_CannotEndOnAClosedRunway_ButStillLandsOnTheOthers()
        {
            Assert.AreEqual(3, landing.Runways.Count, "City: two parallel runways and a diagonal");
            landing.SetRunwayState(0, RunwayState.Closed);
            Assert.IsFalse(landing.TryLand(LineToRunway(0), LandingZoneKind.Runway, out _), "Closed runway");
            Assert.IsTrue(landing.TryLand(LineToRunway(1), LandingZoneKind.Runway, out _), "The other runways stay open");
            Assert.IsTrue(landing.TryLand(LineToRunway(2), LandingZoneKind.Runway, out _));

            landing.SetRunwayState(0, RunwayState.Closing);
            Assert.IsTrue(landing.TryLand(LineToRunway(0), LandingZoneKind.Runway, out _), "Closing soon still takes landings");

            landing.SetRunwayState(0, RunwayState.Open);
            Assert.IsTrue(landing.TryLand(LineToRunway(0), LandingZoneKind.Runway, out Touchdown t));
            Assert.AreEqual(0, landing.IndexOf(t.Runway));
        }

        [Test]
        public void ClosedRunway_ShowsItsX_AndHidesItsTouchdownZone()
        {
            landing.SetRunwayState(1, RunwayState.Closed);
            var marks = landing.GetComponentsInChildren<RunwayClosedMark>(true);
            Assert.AreEqual(3, marks.Length);
            Assert.AreEqual(RunwayState.Closed, marks[1].State);
            Assert.AreEqual(RunwayState.Open, marks[0].State);
            Assert.AreEqual(1f, RunwayClosedMark.Alpha(RunwayState.Closed, 0.2f));
            Assert.AreNotEqual(RunwayClosedMark.Alpha(RunwayState.Closing, 0.1f), RunwayClosedMark.Alpha(RunwayState.Closing, 0.5f), "Closing: the X blinks");
        }

        [Test]
        public void HelpDemo_PointsAtAnOpenRunway()
        {
            landing.SetRunwayState(0, RunwayState.Closed);
            Vector2? point = landing.ZonePoint(LandingZoneKind.Runway);
            Assert.IsTrue(point.HasValue);
            Assert.IsTrue(landing.Runways[1].InTouchdownZone(point.Value), "First open runway");
        }
    }

    /// <summary>City rule: one runway at a time closes for 10 to 15 s after a 5 s warning.</summary>
    public class RunwayClosureScheduleTests
    {
        static RunwayClosureSettings Settings() => new RunwayClosureSettings
        {
            enabled = true, firstWarningAfterSeconds = 12f, warningSeconds = 5f, minClosedSeconds = 10f, maxClosedSeconds = 15f,
            minOpenSeconds = 10f, maxOpenSeconds = 18f,
        };

        [Test]
        public void Warns5Seconds_ThenClosesOneRunwayFor10To15Seconds()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                var schedule = new RunwayClosureSchedule(Settings(), 3, seed);
                const float dt = 0.05f;
                float t = 0f, warningStart = -1f, closedStart = -1f;
                int closingRunway = -1;
                while (t < 200f)
                {
                    schedule.Tick(dt);
                    t += dt;
                    int closedCount = 0, closingCount = 0;
                    for (int r = 0; r < 3; r++)
                    {
                        if (schedule.StateOf(r) == RunwayState.Closed) closedCount++;
                        if (schedule.StateOf(r) == RunwayState.Closing) closingCount++;
                    }
                    Assert.LessOrEqual(closedCount + closingCount, 1, "One runway at a time");

                    if (schedule.CurrentPhase == RunwayClosureSchedule.Phase.Warning && warningStart < 0f)
                    {
                        warningStart = t;
                        closingRunway = schedule.Runway;
                    }
                    if (schedule.CurrentPhase == RunwayClosureSchedule.Phase.Closed && closedStart < 0f)
                    {
                        closedStart = t;
                        Assert.AreEqual(5f, closedStart - warningStart, 0.11f, "5 s warning");
                        Assert.AreEqual(closingRunway, schedule.Runway, "The warned runway is the one that closes");
                    }
                    if (schedule.CurrentPhase == RunwayClosureSchedule.Phase.Open && closedStart >= 0f)
                    {
                        Assert.That(t - closedStart, Is.InRange(9.9f, 15.1f), "Closed for 10 to 15 s");
                        warningStart = closedStart = -1f;
                    }
                }
            }
        }

        [Test]
        public void EveryRunway_TakesATurn_BeforeOneClosesTwice()
        {
            var schedule = new RunwayClosureSchedule(Settings(), 3, 5);
            var order = new List<int>();
            int last = -1;
            for (float t = 0f; t < 400f && order.Count < 6; t += 0.05f)
            {
                schedule.Tick(0.05f);
                if (schedule.CurrentPhase == RunwayClosureSchedule.Phase.Closed && schedule.Runway != last)
                    order.Add(last = schedule.Runway);
                if (schedule.CurrentPhase == RunwayClosureSchedule.Phase.Open)
                    last = -1;
            }
            Assert.AreEqual(6, order.Count);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, order.GetRange(0, 3));
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, order.GetRange(3, 3));
        }

        [Test]
        public void ASingleRunway_NeverCloses()
        {
            var schedule = new RunwayClosureSchedule(Settings(), 1, 1);
            for (int i = 0; i < 4000; i++)
                schedule.Tick(0.05f);
            Assert.AreEqual(RunwayState.Open, schedule.StateOf(0));
        }

        [Test]
        public void Tick_ReportsEachCloseAndOpen()
        {
            var schedule = new RunwayClosureSchedule(Settings(), 2, 3);
            int changes = 0;
            for (float t = 0f; t < 100f; t += 0.05f)
                if (schedule.Tick(0.05f))
                    changes++;
            Assert.GreaterOrEqual(changes, 4, "At least two closures, each closing and opening");
        }
    }

    /// <summary>Desert rules: the sandstorm and low fuel.</summary>
    public class DesertRuleTests
    {
        static SandstormSettings Storm() => new SandstormSettings
        {
            enabled = true, radius = 0.95f, speed = 0.2f, firstStormAfterSeconds = 4f, minSecondsBetweenStorms = 6f,
            maxSecondsBetweenStorms = 12f, warningSeconds = 5f,
        };

        static readonly Rect View = new Rect(-6.67f, -5f, 13.33f, 10f);

        [Test]
        public void Sandstorm_ClosesTheRunwayItCovers_AndWarnsBeforehand()
        {
            var runway = new Runway(Vector2.zero, 0f, 6f, 0.3f);
            var storm = new Sandstorm(Storm(), View, new Rect(-3f, -2f, 6f, 4f), 1);
            storm.Start(new Vector2(0f, -4f), Vector2.zero); // drifting straight up towards the runway
            Assert.AreEqual(RunwayState.Open, storm.StateOf(runway), "2.85 units away: more than 5 s");
            for (int i = 0; i < 100; i++) storm.Tick(0.1f); // 10 s: 2 units closer, 0.7 from the edge (3.5 s)
            Assert.AreEqual(RunwayState.Closing, storm.StateOf(runway), "Within 5 s: the X blinks");
            for (int i = 0; i < 100; i++) storm.Tick(0.1f);
            Assert.AreEqual(RunwayState.Closed, storm.StateOf(runway), "Covered: closed");
            Assert.IsTrue(storm.Covers(runway));
        }

        [Test]
        public void Sandstorm_DriftsAcrossAndOff_ThenAnotherComes()
        {
            var storm = new Sandstorm(Storm(), View, new Rect(-3f, -2f, 6f, 4f), 7);
            Assert.IsFalse(storm.IsActive);
            for (int i = 0; i < 41; i++) storm.Tick(0.1f);
            Assert.IsTrue(storm.IsActive, "First storm after 4 s");
            Vector2 start = storm.Centre;
            Assert.IsFalse(View.Contains(start), "It comes in from off screen");
            storm.Tick(1f);
            Assert.AreEqual(0.2f, Vector2.Distance(start, storm.Centre), 1e-3f, "It drifts slowly (0.2 units per second)");

            bool left = false, cameBack = false;
            for (float t = 0f; t < 400f; t += 0.1f)
            {
                storm.Tick(0.1f);
                if (!storm.IsActive) left = true;
                else if (left) { cameBack = true; break; }
            }
            Assert.IsTrue(left, "It leaves the screen");
            Assert.IsTrue(cameBack, "Then another storm comes");
        }

        [Test]
        public void CircleTouchesRunway_UsesTheWholeRectangle()
        {
            var runway = new Runway(new Vector2(1f, 1f), 45f, 4f, 0.4f);
            Assert.IsTrue(Sandstorm.CircleTouchesRunway(new Vector2(1f, 1f), 0.1f, runway));
            Vector2 end = runway.Centre + runway.Direction * 2.5f;
            Assert.IsTrue(Sandstorm.CircleTouchesRunway(end, 0.6f, runway), "Reaches the end");
            Assert.IsFalse(Sandstorm.CircleTouchesRunway(end, 0.4f, runway), "Stops short of it");
        }

        static AircraftType Kind(AircraftSilhouette silhouette)
        {
            var kind = ScriptableObject.CreateInstance<AircraftType>();
            kind.SetDefaults(silhouette);
            return kind;
        }

        [Test]
        public void LowFuel_TheSecondAircraftThenEveryThird_WithTimeForItsSpeed()
        {
            var settings = new FuelSettings { enabled = true, lowFuelEvery = 3, minFuelSeconds = 36f, maxFuelSeconds = 46f, slowestKind = 0.5f };
            var fuel = new FuelRules(settings, 4);
            AircraftType airliner = Kind(AircraftSilhouette.Airliner);
            var low = new List<int>();
            for (int i = 1; i <= 11; i++)
            {
                float? seconds = fuel.NextAircraft(airliner);
                if (seconds.HasValue)
                {
                    low.Add(i);
                    Assert.That(seconds.Value, Is.InRange(36f, 46f));
                }
            }
            CollectionAssert.AreEqual(new[] { 2, 5, 8, 11 }, low);

            var helicopterFuel = new FuelRules(settings, 4);
            AircraftType helicopter = Kind(AircraftSilhouette.Helicopter);
            helicopterFuel.NextAircraft(helicopter);
            float? heli = helicopterFuel.NextAircraft(helicopter);
            Assert.That(heli.Value, Is.InRange(36f / 0.6f, 46f / 0.6f), "Slower kinds get more fuel in proportion");

            var balloonFuel = new FuelRules(settings, 4);
            AircraftType balloon = Kind(AircraftSilhouette.HotAirBalloon);
            for (int i = 0; i < 10; i++)
                Assert.IsNull(balloonFuel.NextAircraft(balloon), "The balloon never runs low");

            Assert.IsNull(new FuelRules(new FuelSettings { enabled = false }, 1).NextAircraft(airliner));
            Object.DestroyImmediate(airliner);
            Object.DestroyImmediate(helicopter);
            Object.DestroyImmediate(balloon);
        }

        [Test]
        public void LowFuel_LeavesTimeToFlyToTheFarthestRunwayAndBack()
        {
            // Fairness: even the least fuel lasts more than twice the flight from any spawn point to the nearest runway.
            for (int n = 2; n <= 5; n++)
            {
                LevelData level = DrawLevels.Level("desert", n);
                foreach (AircraftType kind in level.aircraftTypes)
                {
                    if (kind.speedMultiplier < level.rules.fuel.slowestKind)
                        continue;
                    float least = level.rules.fuel.minFuelSeconds / kind.speedMultiplier;
                    foreach (SpawnPoint spawn in level.spawnPoints)
                    {
                        float flight = SessionLengthEstimate.DistanceToNearestZone(level, kind.landsOn, spawn.position).Value
                                       / (0.6f * kind.speedMultiplier);
                        Assert.Greater(least, 2f * flight, $"{kind.displayName} from {spawn.position}");
                    }
                }
            }
        }
    }

    /// <summary>Mountain Valley rule: no-fly mountains from the picture.</summary>
    public class NoFlyTests
    {
        static NoFlyZones Square() => new NoFlyZones(new[]
        {
            new List<Vector2> { new Vector2(0f, 0f), new Vector2(2f, 0f), new Vector2(2f, 2f), new Vector2(0f, 2f) },
        });

        [Test]
        public void ALine_CannotEnter_OrCrossAThinPart_ButMayLeaveFromInside()
        {
            NoFlyZones zones = Square();
            Assert.IsTrue(zones.SegmentEnters(new Vector2(-1f, 1f), new Vector2(1f, 1f)), "Ends inside");
            Assert.IsTrue(zones.SegmentEnters(new Vector2(-1f, 1f), new Vector2(3f, 1f)), "Crosses right through");
            Assert.IsFalse(zones.SegmentEnters(new Vector2(-1f, 1f), new Vector2(-1f, 3f)), "Passes beside it");
            Assert.IsFalse(zones.SegmentEnters(new Vector2(1f, 1f), new Vector2(3f, 1f)), "Starts inside: may leave");
        }

        [Test]
        public void TurnBackHeading_MirrorsOffTheEdge()
        {
            NoFlyZones zones = Square();
            // Flying right and a little up at the left wall: mirrored to left and a little up.
            float? heading = zones.TurnBackHeading(new Vector2(-0.5f, 1f), 20f, 1f);
            Assert.IsTrue(heading.HasValue);
            Assert.AreEqual(160f, heading.Value, 0.5f);
            Assert.IsFalse(zones.TurnBackHeading(new Vector2(-0.5f, 1f), 180f, 1f).HasValue, "Flying away: nothing ahead");
            float? headOn = zones.TurnBackHeading(new Vector2(-0.5f, 1f), 0f, 1f);
            Assert.AreEqual(180f, headOn.Value, 0.5f, "Head on: straight back");
        }

        [Test]
        public void AircraftFlyingStraight_TurnBackAtTheMountains_AndNeverEnterThem()
        {
            // The real mountains of 08_mountain_draw; every kind, at Fast speed, in many directions (C5 at the mountains).
            LevelData level = DrawLevels.Level("mountain", 2);
            var zones = new NoFlyZones(level.rules.noFly.areas);
            Rect screen = new Rect(-6.67f + 0.8f, -5f + 0.8f, 13.33f - 1.6f, 10f - 1.6f);
            foreach (float speedMultiplier in new[] { 0.35f, 0.6f, 1f, 1.6f })
                foreach (float factor in new[] { 1f, 1.4f })
                    for (int h = 0; h < 360; h += 15)
                    {
                        var motion = new AircraftMotion(new Vector2(-2f, -0.2f), h, 0.6f * speedMultiplier * factor, 200f) { NoFly = zones };
                        var trail = new List<string>();
                        for (float t = 0f; t < 40f; t += 1f / 60f)
                        {
                            motion.Step(1f / 60f, screen);
                            trail.Add($"{t:F2} {motion.Position.x:F3},{motion.Position.y:F3} h{motion.HeadingDegrees:F0}");
                            if (trail.Count > 90) trail.RemoveAt(0);
                            if (zones.Contains(motion.Position))
                                Assert.Fail($"speed {speedMultiplier}x{factor}, heading {h}: entered at {motion.Position} after {t:F1} s\n" + string.Join("\n", trail));
                        }
                    }
        }

        [Test]
        public void WithoutNoFly_AnAircraftFliesStraightIn()
        {
            LevelData level = DrawLevels.Level("mountain", 1);
            var zones = new NoFlyZones(level.rules.noFly.areas); // the areas are there; level 1 just does not use them
            Assert.IsFalse(level.rules.noFly.enabled);
            var motion = new AircraftMotion(new Vector2(0f, 1f), 90f, 0.6f, 200f);
            bool entered = false;
            for (float t = 0f; t < 6f; t += 1f / 60f)
            {
                motion.Step(1f / 60f, new Rect(-100f, -100f, 200f, 200f));
                entered |= zones.Contains(motion.Position);
            }
            Assert.IsTrue(entered);
        }

        [Test]
        public void ALineFollowedByAnAircraft_IsNeverChanged_ByTheNoFlyTurnBack()
        {
            NoFlyZones zones = Square();
            var motion = new AircraftMotion(new Vector2(-3f, 1f), 0f, 0.6f, 200f) { NoFly = zones };
            motion.BeginPath();
            motion.AppendWaypoint(new Vector2(-1.2f, 1f));
            for (int i = 0; i < 60; i++)
                motion.Step(1f / 60f, new Rect(-100f, -100f, 200f, 200f));
            Assert.AreEqual(0f, motion.HeadingDegrees, 0.01f, "On its line it flies the line, exactly");
            Assert.AreEqual(1f, motion.Position.y, 1e-4f);
        }
    }

    /// <summary>Arctic rule: icy runways.</summary>
    public class IceTests
    {
        [Test]
        public void LandingOnFast_SlidesTwiceAsLong_SlowAndNormalAreSafe()
        {
            var ice = new IceSettings { enabled = true, fastSlideFactor = 2f };
            Assert.AreEqual(2f, IceRules.RollFactor(ice, AircraftSpeed.Fast));
            Assert.AreEqual(1f, IceRules.RollFactor(ice, AircraftSpeed.Slow));
            Assert.AreEqual(1f, IceRules.RollFactor(ice, AircraftSpeed.Normal));
            Assert.AreEqual(1f, IceRules.RollFactor(new IceSettings { enabled = false, fastSlideFactor = 2f }, AircraftSpeed.Fast));
            Assert.AreEqual(1f, IceRules.RollFactor(null, AircraftSpeed.Fast));
        }

        [Test]
        public void ATouchdown_OnARunwayStillBusy_IsAConflict()
        {
            Assert.AreEqual(1, IceRules.TouchdownConflict(0, new[] { 1, 0 }));
            Assert.AreEqual(-1, IceRules.TouchdownConflict(2, new[] { 1, 0 }));
            Assert.AreEqual(-1, IceRules.TouchdownConflict(-1, new[] { -1 }), "Pads never conflict");
        }

        [Test]
        public void ArcticLevels_SlideTwiceAsLong()
        {
            Assert.AreEqual(2f, DrawLevels.Level("arctic", 2).rules.ice.fastSlideFactor);
        }
    }

    /// <summary>Island rule: wind switches the active end of each runway.</summary>
    public class IslandWindEndTests
    {
        [Test]
        public void Wind_MovesEveryRunwaysTouchdownZone_ToTheOtherEnd()
        {
            LevelData level = DrawLevels.Level("island", 2);
            Assert.AreEqual(2, level.runways.Count, "Main and crossing runway");
            for (int i = 0; i < level.runways.Count; i++)
            {
                Runway normal = level.runways[i].ToRunway();
                Runway reversed = normal.Reversed();
                Vector2 endA = DrawModeSetup.ZoneCentre(level.runways[i], false);
                Vector2 endB = DrawModeSetup.ZoneCentre(level.runways[i], true);
                var line = new List<Vector2> { endA + new Vector2(0f, 3f), endA };
                Assert.IsTrue(LandingRules.CanLand(normal, line), "End A before the change");
                Assert.IsFalse(LandingRules.CanLand(reversed, line), "Not after it");
                Assert.IsTrue(LandingRules.CanLand(reversed, new List<Vector2> { endB + new Vector2(0f, 3f), endB }), "End B after it");
            }
        }
    }
}
