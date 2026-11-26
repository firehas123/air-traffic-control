using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AirTrafficControl.Tests
{
    static class TowerTestData
    {
        public static readonly string[] Maps = { "island", "city", "desert", "mountain", "arctic" };

        public static PictureLayout Layout(string map)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MapCatalog>("Assets/Maps/MapCatalog.asset");
            return catalog.Find(map).towerLayout;
        }

        public static LevelData Level(string map, int number) =>
            AssetDatabase.LoadAssetAtPath<LevelData>($"Assets/Levels/Tower/{map}-tower-0{number}.asset");

        public static List<Runway> Runways(PictureLayout layout)
        {
            var list = new List<Runway>();
            foreach (PictureRunway r in layout.runways)
                list.Add(layout.ToRunwayDefinition(r).ToRunway());
            return list;
        }

        public static Rect IPadView(PictureLayout layout) => TowerModeSetup.IPadView(layout);
    }

    /// <summary>Dubins paths: the smooth routes of Tower mode reach the target pose and never turn tighter than the radius.</summary>
    public class DubinsTests
    {
        [Test]
        public void Paths_ReachTheTargetPose_WithinTheTurnRadius()
        {
            var random = new System.Random(7);
            for (int n = 0; n < 300; n++)
            {
                var from = new Vector2((float)random.NextDouble() * 12f - 6f, (float)random.NextDouble() * 8f - 4f);
                var to = new Vector2((float)random.NextDouble() * 12f - 6f, (float)random.NextDouble() * 8f - 4f);
                float h0 = (float)random.NextDouble() * 360f, h1 = (float)random.NextDouble() * 360f;
                float radius = 0.3f + (float)random.NextDouble();
                List<Vector2> path = Dubins.Path(from, h0, to, h1, radius, 0.05f);
                Assert.AreEqual(from, path[0]);
                Assert.Less(Vector2.Distance(to, path[path.Count - 1]), 1e-4f, $"case {n}");
                Vector2 last = path[path.Count - 1] - path[path.Count - 2];
                Assert.Less(Mathf.Abs(Mathf.DeltaAngle(Mathf.Atan2(last.y, last.x) * Mathf.Rad2Deg, h1)), 8f, $"case {n}: arrives heading the right way");
                float length = 0f;
                for (int i = 1; i < path.Count; i++)
                {
                    float step = Vector2.Distance(path[i - 1], path[i]);
                    Assert.LessOrEqual(step, 0.0505f, $"case {n}: evenly sampled");
                    length += step;
                    if (i >= 2)
                    {
                        Vector2 a = path[i - 1] - path[i - 2], b = path[i] - path[i - 1];
                        if (a.sqrMagnitude > 1e-6f && b.sqrMagnitude > 1e-6f)
                        {
                            float turn = Vector2.Angle(a, b) * Mathf.Deg2Rad;
                            Assert.LessOrEqual(turn, (a.magnitude + b.magnitude) / 2f / radius * 1.05f + 1e-3f, $"case {n}: no turn tighter than the radius");
                        }
                    }
                }
                Assert.GreaterOrEqual(length + 1e-3f, Vector2.Distance(from, to));
                Assert.AreEqual(Dubins.Length(from, h0, to, h1, radius), length, 0.02f + 0.002f * length, $"case {n}");
            }
        }
    }

    /// <summary>Approach, landing, go-around and hold paths (blueprint 16.1).</summary>
    public class TowerFlightTests
    {
        static readonly Runway Main = new Runway(Vector2.zero, 0f, 8f, 0.9f, 0.12f, 0.74f);
        const float Radius = 0.69f;

        [Test]
        public void Approach_WithoutClearance_StopsAtTheThreshold_AndDoesNotLand()
        {
            ApproachTarget target = TowerFlightPlanner.ForRunway(Main, 0, 0.9f);
            List<Vector2> path = TowerFlightPlanner.Approach(new Vector2(-2f, 4f), -90f, target, Radius, false);
            Assert.Less(Vector2.Distance(path[path.Count - 1], Main.Threshold), 1e-4f);
            Assert.IsFalse(LandingRules.TryFindTouchdown(new List<Runway> { Main }, new List<LandingPad>(), path, LandingZoneKind.Runway, out _),
                "An approach without a clearance never lands by itself");
        }

        [Test]
        public void Approach_WithClearance_IsLinedUpForTheFinal_AndLandsInTheTouchdownZone()
        {
            ApproachTarget target = TowerFlightPlanner.ForRunway(Main, 0, 0.9f);
            foreach (var (from, heading) in new[] { (new Vector2(-2f, 4f), -90f), (new Vector2(5f, -4f), 90f), (new Vector2(-6f, 0.5f), 0f) })
            {
                List<Vector2> path = TowerFlightPlanner.Approach(from, heading, target, Radius, true);
                Assert.IsTrue(LandingRules.TryFindTouchdown(new List<Runway> { Main }, new List<LandingPad>(), path, LandingZoneKind.Runway, out _));
                // From the final approach fix on, every point is on the extended centreline: lined up with the runway.
                int fix = path.FindLastIndex(p => Vector2.Distance(p, target.Fix) < 1e-3f);
                Assert.GreaterOrEqual(fix, 0, "Passes the final approach fix");
                for (int i = fix; i < path.Count; i++)
                    Assert.AreEqual(0f, Main.ToLocal(path[i]).y, 0.01f, $"from {from}: lined up on the final");
                Assert.AreEqual(0.9f, Vector2.Distance(target.Fix, Main.Threshold), 1e-3f);
            }
        }

        [Test]
        public void GoAround_RejoinsTheFinal_AboutTenSecondsLater()
        {
            ApproachTarget target = TowerFlightPlanner.ForRunway(Main, 0, 0.9f);
            var view = new Rect(-6.67f, -5f, 13.33f, 10f);
            // Over the runway end, on short final, or just after touchdown: the usual places to go around.
            foreach (float speed in new[] { 0.6f * 0.85f, 0.6f, 0.6f * 1.6f })
                foreach (var (from, heading) in new[] { (Main.Threshold + Vector2.right * 0.5f, 0f), (target.Fix - Vector2.right * 0.5f, 0f),
                                                        (Main.Threshold + Vector2.right * 0.05f, 0f) })
                {
                    float radius = Mathf.Max(0.35f, speed / (50f * Mathf.Deg2Rad));
                    List<Vector2> path = TowerFlightPlanner.GoAround(from, heading, speed, target, radius, view, 10f);
                    float seconds = TowerFlightPlanner.LengthTo(path, target.Fix) / speed;
                    Assert.That(seconds, Is.InRange(8.5f, 11.5f), $"speed {speed} from {from}: back on final after {seconds:F1} s");
                    Assert.Less(Vector2.Distance(path[path.Count - 1], target.DecisionPoint), 1e-4f, "Ends at the threshold, uncleared");
                    // It climbs straight ahead first.
                    Vector2 first = path[1] - path[0];
                    Assert.Less(Vector2.Angle(first, new Vector2(Mathf.Cos(heading * Mathf.Deg2Rad), Mathf.Sin(heading * Mathf.Deg2Rad))), 1f);
                }

            // Far away and flying the other way it cannot be back sooner than it can fly there, but never much sooner than 10 s.
            foreach (var (from, heading) in new[] { (new Vector2(0f, 3f), 30f), (new Vector2(4f, -3f), 200f) })
            {
                List<Vector2> path = TowerFlightPlanner.GoAround(from, heading, 0.6f, target, Radius, view, 10f);
                Assert.GreaterOrEqual(TowerFlightPlanner.LengthTo(path, target.Fix) / 0.6f, 8.5f);
            }
        }

        [Test]
        public void Hold_CirclesTheFix()
        {
            var fix = new Vector2(-4.5f, 3f);
            List<Vector2> path = TowerFlightPlanner.Hold(new Vector2(0f, 0f), 0f, fix, 0.75f, Radius);
            int onCircle = 0;
            foreach (Vector2 p in path)
                if (Mathf.Abs(Vector2.Distance(p, fix) - 0.75f) < 0.01f)
                    onCircle++;
            Assert.Greater(onCircle, 60, "A full circle round the hold fix");
            Assert.Less(Mathf.Abs(Vector2.Distance(path[path.Count - 1], fix) - 0.75f), 0.01f);
            int before = path.Count;
            TowerFlightPlanner.AppendHoldCircle(path, fix, 0.75f);
            Assert.Greater(path.Count, before, "Another circle can be added before it runs out");
        }

        [Test]
        public void PadApproach_LandsOnThePad_OnlyWhenCleared()
        {
            var pad = new LandingPad(LandingZoneKind.Helipad, new Vector2(3f, 2f), 0.45f);
            ApproachTarget target = TowerFlightPlanner.ForPad(pad, new Vector2(-3f, -2f));
            List<Vector2> no = TowerFlightPlanner.Approach(new Vector2(-3f, -2f), 0f, target, 0.46f, false);
            List<Vector2> yes = TowerFlightPlanner.Approach(new Vector2(-3f, -2f), 0f, target, 0.46f, true);
            var pads = new List<LandingPad> { pad };
            Assert.IsFalse(LandingRules.TryFindTouchdown(new List<Runway>(), pads, no, LandingZoneKind.Helipad, out _));
            Assert.IsTrue(LandingRules.TryFindTouchdown(new List<Runway>(), pads, yes, LandingZoneKind.Helipad, out _));
        }
    }

    /// <summary>The painted taxiways of the five Tower pictures as a network.</summary>
    public class TaxiNetworkTests
    {
        [Test]
        public void EveryStand_ReachesEveryDepartureRunwayEnd_AndBack()
        {
            foreach (string map in TowerTestData.Maps)
            {
                PictureLayout layout = TowerTestData.Layout(map);
                TaxiNetwork network = TaxiNetwork.Build(layout);
                Assert.GreaterOrEqual(network.StandCount, 4, map);
                int ends = 0;
                for (int r = 0; r < layout.runways.Count; r++)
                    foreach (bool endB in new[] { false, true })
                    {
                        List<int> holds = network.EndHoldNodes(r, endB);
                        if (holds.Count == 0)
                            continue;
                        ends++;
                        for (int s = 0; s < network.StandCount; s++)
                        {
                            Assert.IsNotNull(network.FindRoute(network.StandNode(s), holds), $"{map}: stand {s + 1} to runway {RunwayNames.Letter(r)} end {(endB ? "B" : "A")}");
                        }
                    }
                Assert.GreaterOrEqual(ends, 2, $"{map}: departures can use at least two runway ends");
            }
        }

        [Test]
        public void EveryLandingRunway_HasExitsThatLeadToAStand()
        {
            foreach (string map in TowerTestData.Maps)
            {
                PictureLayout layout = TowerTestData.Layout(map);
                TaxiNetwork network = TaxiNetwork.Build(layout);
                List<Runway> runways = TowerTestData.Runways(layout);
                for (int r = 0; r < runways.Count; r++)
                {
                    bool valley = map == "mountain";
                    var settings = new TowerSettings { valleyOnly = valley };
                    if (!settings.AllowsRunway(runways[r]))
                        continue;
                    Assert.Greater(network.Exits(r).Count, 0, $"{map} runway {RunwayNames.Letter(r)} has an exit");
                    foreach (Runway direction in new[] { runways[r], runways[r].Reversed() })
                    {
                        // A landing that rolls 2.4 units from the touchdown zone finds its way to a stand.
                        float stop = direction.TouchdownZoneStart + direction.TouchdownZoneLength / 2f + 2.4f;
                        List<Vector2> exit = network.ExitPath(r, direction, stop, null, out int node, out float rollEnd);
                        Assert.GreaterOrEqual(node, 0, $"{map} {RunwayNames.Letter(r)}");
                        Assert.IsFalse(direction.Contains(exit[exit.Count - 1], -0.02f), $"{map} {RunwayNames.Letter(r)}: waits clear of the runway");
                        Assert.IsNotNull(network.FindRoute(node, network.StandNodes()), $"{map} {RunwayNames.Letter(r)}: from the exit to a stand");
                    }
                }
            }
        }

        [Test]
        public void Island_RemoteStand_CrossesTheMainRunway_AtItsHoldLine()
        {
            PictureLayout layout = TowerTestData.Layout("island");
            TaxiNetwork network = TaxiNetwork.Build(layout);
            int remote = layout.stands.FindIndex(s => s.name == "Remote stand");
            TaxiRoute route = network.FindRoute(network.StandNode(remote), network.EndHoldNodes(0, false));
            Assert.IsNotNull(route);
            Assert.GreaterOrEqual(route.crossings.Count, 1, "The route crosses runway A");
            TaxiRoute.Stop first = route.crossings[0];
            Assert.AreEqual(0, first.Runway);
            Vector2 wait = layout.Pixel(route.points[first.PointIndex]);
            Assert.AreEqual(1056f, wait.x, 3f);
            Assert.AreEqual(563f, wait.y, 3f, "Waits at the painted hold bars south of the runway");
            Runway main = TowerTestData.Runways(layout)[0];
            Assert.IsFalse(main.Contains(route.points[first.ClearPointIndex], -0.02f), "Clear of the runway on the far side");
        }

        [Test]
        public void City_TaxiwayCrossesTheDiagonalRunway_BetweenItsHoldBars()
        {
            PictureLayout layout = TowerTestData.Layout("city");
            TaxiNetwork network = TaxiNetwork.Build(layout);
            // From the apron to runway A's end A (west) the shortest way crosses runway C (the diagonal).
            TaxiRoute route = network.FindRoute(network.StandNode(0), network.EndHoldNodes(0, false));
            Assert.IsNotNull(route);
            Assert.IsTrue(route.crossings.Exists(c => c.Runway == 2), "Crosses the diagonal runway C");
        }
    }

    /// <summary>Ground movement in Tower mode: departures, crossings, take-off, and arrivals leaving the runway.</summary>
    public class TowerGroundTests
    {
        const float Dt = 1f / 60f;

        static void Run(GroundMovement g, float seconds, System.Func<bool> stop = null)
        {
            for (float t = 0f; t < seconds && (stop == null || !stop()); t += Dt)
                g.Step(Dt, false);
        }

        [Test]
        public void Departure_StopsBeforeTheRunway_UntilCross_ThenTaxisOnAndTakesOff()
        {
            PictureLayout layout = TowerTestData.Layout("island");
            TaxiNetwork network = TaxiNetwork.Build(layout);
            List<Runway> runways = TowerTestData.Runways(layout);
            int remote = layout.stands.FindIndex(s => s.name == "Remote stand");
            int standNode = network.StandNode(remote);
            var g = GroundMovement.AtStand(network.Nodes[standNode].position, 90f, remote, standNode, 0.42f, 0.6f);
            Assert.AreEqual(GroundPhase.AtStand, g.Phase);
            Assert.IsTrue(g.FollowRoute(network.FindRoute(standNode, network.EndHoldNodes(0, false)), GroundPhase.HoldingShort));
            Run(g, 30f, () => g.Phase == GroundPhase.HoldingAtCrossing);
            Assert.AreEqual(GroundPhase.HoldingAtCrossing, g.Phase, "Waits at the hold line for Cross");
            Assert.IsFalse(runways[0].Contains(g.Position), "Clear of the runway while waiting");
            Run(g, 5f);
            Assert.AreEqual(GroundPhase.HoldingAtCrossing, g.Phase, "Still waiting: only Cross moves it on");

            Assert.IsTrue(g.Cross());
            bool onRunway = false, busyIndex = false;
            Run(g, 40f, () =>
            {
                onRunway |= runways[0].Contains(g.Position);
                busyIndex |= g.RunwayIndex == 0 && g.UsesRunway;
                return g.Phase == GroundPhase.HoldingShort || g.Phase == GroundPhase.HoldingAtCrossing;
            });
            Assert.IsTrue(onRunway && busyIndex, "It crossed runway A, which counted as in use");
            // The main taxiway then crosses runway B (the crossing runway) on the way to end A.
            if (g.Phase == GroundPhase.HoldingAtCrossing)
            {
                Assert.IsTrue(g.Cross());
                Run(g, 40f, () => g.Phase == GroundPhase.HoldingShort);
            }
            Assert.AreEqual(GroundPhase.HoldingShort, g.Phase, "At the hold line of runway A's end A");

            Vector2 lineUp = GroundMovement.LineUpPoint(runways[0], g.Position);
            Assert.IsTrue(g.TakeOff(runways[0], 0, lineUp, GroundMovement.LiftOffDistance(runways[0], lineUp)));
            Run(g, 40f, () => g.Phase == GroundPhase.Airborne);
            Assert.AreEqual(GroundPhase.Airborne, g.Phase);
            Assert.AreEqual(runways[0].LandingHeadingDegrees, Mathf.Repeat(g.HeadingDegrees, 360f), 0.5f, "Lifts off in the runway direction");
        }

        [Test]
        public void Arrival_RollsOut_LeavesAtTheNextExit_AndWaitsClearOfTheRunway()
        {
            PictureLayout layout = TowerTestData.Layout("island");
            TaxiNetwork network = TaxiNetwork.Build(layout);
            Runway main = TowerTestData.Runways(layout)[0];
            Vector2 touchdown = TowerFlightPlanner.TouchdownPoint(main);
            float along = Vector2.Dot(touchdown - main.Threshold, main.Direction);
            List<Vector2> exit = network.ExitPath(0, main, along + 2.4f, null, out int node, out float rollEnd);
            var g = GroundMovement.Landed(touchdown, main, 0, 0.6f, rollEnd - along, exit, node, 0.5f, 0.42f);
            Assert.AreEqual(GroundPhase.Rollout, g.Phase);
            bool slowed = false;
            Run(g, 60f, () =>
            {
                slowed |= g.Phase == GroundPhase.Rollout && g.CurrentSpeed < 0.6f;
                return g.Phase == GroundPhase.WaitingForStand;
            });
            Assert.IsTrue(slowed, "It slows down on the runway");
            Assert.AreEqual(GroundPhase.WaitingForStand, g.Phase);
            Assert.IsFalse(main.Contains(g.Position), "Waits clear of the runway");
            Assert.AreEqual(node, g.CurrentNode);
            // Island runway A from the west: the first exit after the roll is the taxiway at x 1056 (the north side leads to
            // the terminal, the south side to the remote stand; it takes the side nearer a stand).
            Assert.AreEqual(1056f, layout.Pixel(g.Position).x, 3f);

            int stand = network.StandNode(0);
            Assert.IsTrue(g.FollowRoute(network.FindRoute(g.CurrentNode, stand), GroundPhase.Parked));
            Run(g, 60f, () => g.Phase == GroundPhase.Parked);
            Assert.AreEqual(GroundPhase.Parked, g.Phase, "Taxied to the stand and parked");
        }

        [Test]
        public void FastLanding_RollsFurther_AndSlidesFurtherStillOnIce()
        {
            var ice = new IceSettings { enabled = true, fastSlideFactor = 2f };
            Assert.AreEqual(1f, IceRules.RollFactor(ice, AircraftSpeed.Normal));
            Assert.AreEqual(1f, IceRules.RollFactor(ice, AircraftSpeed.Slow));
            Assert.AreEqual(2f, IceRules.RollFactor(ice, AircraftSpeed.Fast));
            Assert.AreEqual(1f, IceRules.RollFactor(null, AircraftSpeed.Fast));

            // On ice the sliding roll (when the whole runway is blocked) lasts longer: twice the distance, then a stop.
            PictureLayout layout = TowerTestData.Layout("arctic");
            TaxiNetwork network = TaxiNetwork.Build(layout);
            Runway main = TowerTestData.Runways(layout)[0];
            float RolloutSeconds(float speed, float roll, float stop, out bool waitsClear)
            {
                Vector2 td = TowerFlightPlanner.TouchdownPoint(main);
                float along = Vector2.Dot(td - main.Threshold, main.Direction);
                List<Vector2> exit = network.ExitPath(0, main, along + roll, null, out int node, out float rollEnd);
                var g = GroundMovement.Landed(td, main, 0, speed, rollEnd - along, exit, node, 0.5f, 0.42f, stop);
                float t = 0f, rollout = 0f;
                while (g.Phase != GroundPhase.WaitingForStand && t < 120f)
                {
                    if (g.Phase == GroundPhase.Rollout) rollout += Dt;
                    g.Step(Dt, false);
                    t += Dt;
                }
                waitsClear = g.Phase == GroundPhase.WaitingForStand && !main.Contains(g.Position);
                return rollout;
            }
            float normal = RolloutSeconds(0.6f, 2.4f, 0f, out bool clearA);
            float sliding = RolloutSeconds(0.84f, 2.4f * 1.4f * IceRules.RollFactor(ice, AircraftSpeed.Fast), 2.5f, out bool clearB);
            Assert.IsTrue(clearA && clearB, "Both leave the runway at an exit");
            Assert.Greater(sliding, normal * 1.5f, "A Fast landing on ice slides and blocks the whole runway for longer");
        }

        [Test]
        public void TaxiingAircraft_QueueBehind_AndPassHeadOn()
        {
            var route = new TaxiRoute();
            route.points.Add(Vector2.zero);
            route.points.Add(new Vector2(5f, 0f));
            route.nodes.Add(0);
            route.nodes.Add(1);
            var front = GroundMovement.AtStand(new Vector2(1f, 0f), 0f, -1, 0, 0.42f, 0.6f);
            var behind = GroundMovement.AtStand(new Vector2(0.5f, 0f), 0f, -1, 0, 0.42f, 0.6f);
            var r1 = new TaxiRoute();
            r1.points.Add(new Vector2(1f, 0f)); r1.points.Add(new Vector2(5f, 0f)); r1.nodes.Add(0); r1.nodes.Add(1);
            var r2 = new TaxiRoute();
            r2.points.Add(new Vector2(0.5f, 0f)); r2.points.Add(new Vector2(5f, 0f)); r2.nodes.Add(0); r2.nodes.Add(1);
            front.FollowRoute(r1, GroundPhase.HoldingShort);
            behind.FollowRoute(r2, GroundPhase.HoldingShort);
            Assert.IsTrue(TaxiQueue.Blocks(behind, front, 0.75f), "Waits behind the aircraft in front");
            Assert.IsFalse(TaxiQueue.Blocks(front, behind, 0.75f), "Nothing in front of the first one");

            // Run both with the queue rule: they never get closer than the spacing.
            for (float t = 0f; t < 15f; t += Dt)
            {
                front.Step(Dt, TaxiQueue.Blocks(front, behind, 0.75f));
                behind.Step(Dt, TaxiQueue.Blocks(behind, front, 0.75f));
                if (front.Phase == GroundPhase.HoldingShort && behind.Phase == GroundPhase.HoldingShort)
                    break;
                Assert.Greater(Vector2.Distance(front.Position, behind.Position), 0.4f, "Queued, never touching");
            }
            Assert.AreEqual(GroundPhase.HoldingShort, front.Phase);
            Assert.AreEqual(GroundPhase.Taxiing, behind.Phase, "The second one waits behind the first at the hold line");

            // Head-on: two moving towards each other do not wait for each other forever.
            var east = GroundMovement.AtStand(new Vector2(0f, 1f), 0f, -1, 0, 0.42f, 0.6f);
            var west = GroundMovement.AtStand(new Vector2(0.5f, 1f), 180f, -1, 0, 0.42f, 0.6f);
            var re = new TaxiRoute(); re.points.Add(new Vector2(0f, 1f)); re.points.Add(new Vector2(4f, 1f)); re.nodes.Add(0); re.nodes.Add(1);
            var rw = new TaxiRoute(); rw.points.Add(new Vector2(0.5f, 1f)); rw.points.Add(new Vector2(-4f, 1f)); rw.nodes.Add(0); rw.nodes.Add(1);
            east.FollowRoute(re, GroundPhase.HoldingShort);
            west.FollowRoute(rw, GroundPhase.HoldingShort);
            east.Step(Dt, false);
            west.Step(Dt, false);
            for (float t = 0f; t < 15f; t += Dt)
            {
                east.Step(Dt, TaxiQueue.Blocks(east, west, 0.75f));
                west.Step(Dt, TaxiQueue.Blocks(west, east, 0.75f));
            }
            Assert.AreEqual(GroundPhase.HoldingShort, east.Phase);
            Assert.AreEqual(GroundPhase.HoldingShort, west.Phase);
        }
    }

    /// <summary>Busy runways and intersections (blueprint 15.4, 16.1).</summary>
    public class RunwayIntersectionTests
    {
        const float R = AircraftType.AirlinerCollisionRadius;

        [Test]
        public void AnAircraftInTheIntersection_MakesBothRunwaysBusy()
        {
            PictureLayout layout = TowerTestData.Layout("island");
            List<Runway> runways = TowerTestData.Runways(layout);
            Vector2 intersection = layout.World(layout.intersections[0].point);
            var busy = new bool[2];

            RunwayRules.BusyRunways(runways, new List<RunwayOccupant> { new RunwayOccupant(intersection, R, RunwayUse.Landing) },
                                    new List<int> { 0 }, busy);
            Assert.IsTrue(busy[0] && busy[1], "Rolling through the intersection: both runways busy");

            Vector2 farEast = runways[0].Threshold + runways[0].Direction * (runways[0].Length - 1f);
            RunwayRules.BusyRunways(runways, new List<RunwayOccupant> { new RunwayOccupant(farEast, R, RunwayUse.Landing) },
                                    new List<int> { 0 }, busy);
            Assert.IsTrue(busy[0]);
            Assert.IsFalse(busy[1], "Far from the intersection only its own runway is busy");

            RunwayRules.BusyRunways(runways, new List<RunwayOccupant> { new RunwayOccupant(farEast, R, RunwayUse.None) },
                                    new List<int> { -1 }, busy);
            Assert.IsFalse(busy[0] || busy[1], "Nobody using a runway: both free");
        }

        [Test]
        public void DesertTriangle_EachIntersectionMakesItsTwoRunwaysBusy()
        {
            PictureLayout layout = TowerTestData.Layout("desert");
            List<Runway> runways = TowerTestData.Runways(layout);
            foreach (PictureIntersection crossing in layout.intersections)
            {
                var busy = new bool[3];
                RunwayRules.BusyRunways(runways, new List<RunwayOccupant> { new RunwayOccupant(layout.World(crossing.point), R, RunwayUse.Crossing) },
                                        new List<int> { -1 }, busy);
                int a = layout.runways.FindIndex(r => r.name == crossing.runwayA), b = layout.runways.FindIndex(r => r.name == crossing.runwayB);
                for (int i = 0; i < 3; i++)
                    Assert.AreEqual(i == a || i == b, busy[i], $"{crossing.runwayA} x {crossing.runwayB}: runway {RunwayNames.Letter(i)}");
            }
        }

        [Test]
        public void LandingRollThroughTheIntersection_MeetsAnAircraftCrossingTheOtherRunway()
        {
            PictureLayout layout = TowerTestData.Layout("island");
            Vector2 intersection = layout.World(layout.intersections[0].point);
            var collisions = new List<Vector2Int>();
            RunwayRules.FindCollisions(new List<RunwayOccupant>
            {
                new RunwayOccupant(intersection + Vector2.left * 0.2f, R, RunwayUse.Landing),   // rolling on runway A
                new RunwayOccupant(intersection + Vector2.up * 0.2f, R, RunwayUse.TakeOff),     // taking off on runway B
            }, collisions);
            Assert.AreEqual(1, collisions.Count, "A landing and a take-off meeting in the intersection collide");

            RunwayRules.FindCollisions(new List<RunwayOccupant>
            {
                new RunwayOccupant(intersection, R, RunwayUse.Crossing),
                new RunwayOccupant(intersection + Vector2.right * 0.3f, R, RunwayUse.Crossing),
            }, collisions);
            Assert.IsEmpty(collisions, "Two at taxi speed are not a collision");
        }
    }

    /// <summary>The option buttons of Tower mode.</summary>
    public class TowerCommandTests
    {
        [Test]
        public void InTheAir_LandOnEachRunway_GoAroundHoldSlowFast()
        {
            var runways = new List<RunwayChoice> { new RunwayChoice(0, true, true), new RunwayChoice(1, true, false), new RunwayChoice(2, true, true) };
            List<CommandOption> options = AircraftCommands.TowerAir(LandingZoneKind.Runway, runways, false, 2, false, false, AircraftSpeed.Fast);
            CollectionAssert.AreEqual(new[] { "Land on runway A", "Land on runway B", "Land on runway C", "Go around", "Hold", "Slow", "Fast" },
                                      options.ConvertAll(o => o.Label));
            Assert.IsFalse(options[1].Enabled, "A closed runway's Land button is off");
            Assert.IsTrue(options[2].Active, "Cleared for runway C");
            Assert.IsTrue(options[6].Active, "Flying Fast");
            Assert.IsTrue(options[3].Half && options[4].Half && options[5].Half && options[6].Half);
        }

        [Test]
        public void PadKinds_GetTheirPad_AndTheValleyHidesTheCrossRunway()
        {
            Assert.AreEqual("Land on helipad", AircraftCommands.TowerAir(LandingZoneKind.Helipad, new List<RunwayChoice>(), true, -1, false, false, AircraftSpeed.Normal)[0].Label);
            Assert.AreEqual("Land on balloon field", AircraftCommands.TowerAir(LandingZoneKind.BalloonField, new List<RunwayChoice>(), true, -1, false, false, AircraftSpeed.Normal)[0].Label);

            List<CommandOption> valley = AircraftCommands.TowerAir(LandingZoneKind.Runway,
                new List<RunwayChoice> { new RunwayChoice(0, true, true), new RunwayChoice(1, false, true) }, false, -1, false, false, AircraftSpeed.Normal);
            Assert.IsFalse(valley.Exists(o => o.Label == "Land on runway B"));
        }

        [Test]
        public void OnTheGround_TaxiCrossTakeOffTaxiToStand()
        {
            var runways = new List<RunwayChoice> { new RunwayChoice(0, true, true), new RunwayChoice(1, true, false) };
            List<CommandOption> stand = AircraftCommands.TowerGround(GroundPhase.AtStand, runways, -1, false, true);
            CollectionAssert.AreEqual(new[] { "Taxi to runway A", "Taxi to runway B" }, stand.ConvertAll(o => o.Label));
            Assert.IsFalse(stand[1].Enabled, "Closed runway B");

            List<CommandOption> hold = AircraftCommands.TowerGround(GroundPhase.HoldingShort,
                new List<RunwayChoice> { new RunwayChoice(0, false, true), new RunwayChoice(1, true, true) }, 0, false, true);
            Assert.AreEqual("Take off from A", hold[0].Label);
            Assert.IsFalse(hold[0].Enabled, "Take off is off while the runway is closed");
            Assert.AreEqual("Taxi to runway B", hold[1].Label);

            Assert.AreEqual("Cross", AircraftCommands.TowerGround(GroundPhase.HoldingAtCrossing, runways, -1, false, true)[0].Label);
            List<CommandOption> landed = AircraftCommands.TowerGround(GroundPhase.WaitingForStand, runways, -1, false, true);
            Assert.AreEqual("Taxi to stand", landed[0].Label);
            Assert.IsFalse(AircraftCommands.TowerGround(GroundPhase.WaitingForStand, runways, -1, false, false)[0].Enabled, "Stands full");
            Assert.IsEmpty(AircraftCommands.TowerGround(GroundPhase.Rollout, runways, -1, false, true), "No buttons while rolling out");
        }
    }

    /// <summary>Each map's rule in Tower mode (blueprint 16.2, adapted for Tower mode).</summary>
    public class TowerMapRuleTests
    {
        [Test]
        public void Island_WindSwitchesTheActiveEnds_ForApproachesAndDepartures()
        {
            LevelData level = TowerTestData.Level("island", 2);
            Assert.IsTrue(level.wind.hasWind);
            Runway normal = level.runways[0].ToRunway(), reversed = normal.Reversed();
            ApproachTarget a = TowerFlightPlanner.ForRunway(normal, 0, level.tower.finalLength);
            ApproachTarget b = TowerFlightPlanner.ForRunway(reversed, 0, level.tower.finalLength);
            Assert.Greater(Vector2.Distance(a.Touchdown, b.Touchdown), 10f, "The landing end moves to the other end of runway A");
            Assert.IsTrue(level.tower.approaches.Exists(x => x.runway == 0 && !x.endB) && level.tower.approaches.Exists(x => x.runway == 0 && x.endB),
                          "An approach route for each end");
            TaxiNetwork network = TaxiNetwork.Build(level.layout);
            Assert.Greater(network.EndHoldNodes(0, false).Count, 0);
            Assert.Greater(network.EndHoldNodes(0, true).Count, 0, "Departures can be sent to either end");
            // A departure waiting at end A taxis to end B by itself when the wind turns (here: the route exists).
            Assert.IsNotNull(network.FindRoute(network.EndHoldNodes(0, false)[0], network.EndHoldNodes(0, true)));
        }

        [Test]
        public void City_ClosuresAreOnFromLevel2_AndTurnOffThatRunwaysButtons()
        {
            Assert.IsFalse(TowerTestData.Level("city", 1).rules.closures.enabled);
            for (int n = 2; n <= 5; n++)
                Assert.IsTrue(TowerTestData.Level("city", n).rules.closures.enabled, $"city {n}");
            var schedule = new RunwayClosureSchedule(TowerTestData.Level("city", 2).rules.closures, 3, 5);
            bool closed = false;
            for (float t = 0f; t < 60f && !closed; t += 0.1f)
            {
                schedule.Tick(0.1f);
                closed = schedule.CurrentPhase == RunwayClosureSchedule.Phase.Closed;
            }
            Assert.IsTrue(closed);
            var choices = new List<RunwayChoice>();
            for (int r = 0; r < 3; r++)
                choices.Add(new RunwayChoice(r, true, schedule.StateOf(r) != RunwayState.Closed));
            List<CommandOption> air = AircraftCommands.TowerAir(LandingZoneKind.Runway, choices, false, -1, false, false, AircraftSpeed.Normal);
            Assert.IsFalse(air[schedule.Runway].Enabled, "Land on the closed runway is off");
            List<CommandOption> ground = AircraftCommands.TowerGround(GroundPhase.HoldingShort, choices, schedule.Runway, false, true);
            Assert.IsFalse(ground[0].Enabled, "Take off from the closed runway is off");
        }

        [Test]
        public void Desert_LowFuelFromLevel2_AndSandstormFromLevel3_ClosesTheRunwaysItCovers()
        {
            Assert.IsFalse(TowerTestData.Level("desert", 1).rules.fuel.enabled);
            Assert.IsTrue(TowerTestData.Level("desert", 2).rules.fuel.enabled);
            Assert.IsFalse(TowerTestData.Level("desert", 2).rules.sandstorm.enabled);
            LevelData level = TowerTestData.Level("desert", 3);
            Assert.IsTrue(level.rules.sandstorm.enabled && level.rules.fuel.enabled);
            var fuel = new FuelRules(level.rules.fuel, 3);
            var airliner = ScriptableObject.CreateInstance<AircraftType>();
            airliner.SetDefaults(AircraftSilhouette.Airliner);
            int low = 0;
            for (int i = 0; i < 9; i++)
                if (fuel.NextAircraft(airliner).HasValue)
                    low++;
            Assert.AreEqual(3, low, "Every third arrival is low on fuel");
            Object.DestroyImmediate(airliner);

            // The storm over the middle of the airport closes every runway it covers.
            Runway south = level.runways[0].ToRunway();
            Assert.IsTrue(Sandstorm.CircleTouchesRunway(south.Centre, level.rules.sandstorm.radius, south));
        }

        [Test]
        public void Mountain_ApproachesOnlyAlongTheValley_FromLevel2()
        {
            LevelData one = TowerTestData.Level("mountain", 1);
            Assert.IsFalse(one.tower.valleyOnly);
            for (int n = 2; n <= 5; n++)
            {
                LevelData level = TowerTestData.Level("mountain", n);
                Assert.IsTrue(level.tower.valleyOnly, $"mountain {n}");
                Assert.IsTrue(level.tower.AllowsRunway(level.runways[0].ToRunway()), "Runway A runs along the valley");
                Assert.IsFalse(level.tower.AllowsRunway(level.runways[1].ToRunway()), "Runway B crosses the valley: no landings");
                Rect view = TowerTestData.IPadView(level.layout);
                foreach (TowerApproach approach in level.tower.approaches)
                {
                    Assert.AreEqual(0, approach.runway);
                    bool west = Mathf.Abs(approach.entry.x - view.xMin) < 0.8f, east = Mathf.Abs(approach.entry.x - view.xMax) < 0.8f;
                    Assert.IsTrue(west || east, "Enters from the west or east edge");
                    float off = Mathf.Abs(Mathf.DeltaAngle(approach.headingDegrees, west ? 0f : 180f));
                    Assert.Less(off, 1f, "Flying along the valley");
                    Assert.IsFalse(level.layout.FindArea(PictureArea.Mountain) != null
                                   && level.layout.ruleAreas.Exists(a => a.kind == PictureArea.Mountain && a.Contains(level.layout.Pixel(approach.entry))),
                                   "Never inside a mountain");
                }
                foreach (Vector2 fix in level.tower.holdFixes)
                    foreach (PictureArea area in level.layout.ruleAreas)
                        if (area.kind == PictureArea.Mountain)
                            Assert.IsFalse(area.Contains(level.layout.Pixel(fix)), "Hold fixes are in the valley");
            }
        }

        [Test]
        public void Arctic_IceFromLevel2_FastSlides_AndASlidingRollBlocksTheRunway()
        {
            Assert.IsFalse(TowerTestData.Level("arctic", 1).rules.ice.enabled);
            LevelData level = TowerTestData.Level("arctic", 2);
            Assert.IsTrue(level.rules.ice.enabled);
            Assert.AreEqual(2f, IceRules.RollFactor(level.rules.ice, AircraftSpeed.Fast));
            Assert.AreEqual(1f, IceRules.RollFactor(level.rules.ice, AircraftSpeed.Slow));
            Assert.AreEqual(0, IceRules.TouchdownConflict(1, new List<int> { 1 }), "A touchdown on a runway another aircraft slides on is a collision");
            Assert.AreEqual(-1, IceRules.TouchdownConflict(0, new List<int> { 1 }));
        }
    }

    /// <summary>The 25 Tower levels as data (blueprint 16.3).</summary>
    public class TowerLevelDataTests
    {
        [Test]
        public void TwentyFiveLevels_OnTheTowerPictures_InTheCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MapCatalog>("Assets/Maps/MapCatalog.asset");
            foreach (string map in TowerTestData.Maps)
            {
                MapEntry entry = catalog.Find(map);
                Assert.AreEqual(5, entry.towerLevels.Count, map);
                for (int n = 1; n <= 5; n++)
                {
                    LevelData level = entry.towerLevels[n - 1].data;
                    Assert.IsNotNull(level, $"{map} {n}");
                    Assert.AreSame(TowerTestData.Level(map, n), level);
                    Assert.AreEqual(GameMode.Tower, level.mode);
                    Assert.AreSame(entry.towerLayout, level.layout);
                    Assert.AreSame(entry.towerPicture, level.mapArt.sprite);
                    Assert.Greater(level.landingsToClear, 0);
                    Assert.Greater(level.departuresToClear, 0, "Tower levels count landings and departures");
                    Assert.IsFalse(level.carrier.hasCarrier);
                    Assert.Greater(level.tower.approaches.Count, 0);
                    Assert.GreaterOrEqual(level.tower.holdFixes.Count, 4);
                    Assert.AreEqual(level.tower.approaches.Count, level.spawnPoints.Count);
                }
            }
        }

        [Test]
        public void EveryLevel_IsEstimatedAt60To120Seconds()
        {
            foreach (string map in TowerTestData.Maps)
                for (int n = 1; n <= 5; n++)
                {
                    LevelData level = TowerTestData.Level(map, n);
                    float seconds = TowerModeSetup.EstimateSeconds(level);
                    TestContext.WriteLine($"{map}-tower-0{n}: land {level.landingsToClear}, launch {level.departuresToClear}, {seconds:F0} s");
                    Assert.That(seconds, Is.InRange(60f, 120f), $"{map} {n}: {seconds:F0} s");
                }
        }

        [Test]
        public void GoAroundAndHold_AreTaughtInIslandLevels1And2()
        {
            Assert.IsTrue(TowerTestData.Level("island", 1).tower.teachGoAround);
            Assert.IsTrue(TowerTestData.Level("island", 2).tower.teachHold);
            foreach (string map in TowerTestData.Maps)
                for (int n = 1; n <= 5; n++)
                {
                    if (map == "island" && n <= 2)
                        continue;
                    Assert.IsFalse(TowerTestData.Level(map, n).tower.teachGoAround || TowerTestData.Level(map, n).tower.teachHold, $"{map} {n}");
                }

            var help = new FirstTimeHelp(0, tower: true);
            help.Tick(0.1f, true);
            Assert.AreEqual(HelpHint.TapForOrders, help.Current, "The first lesson in Tower mode: tap and Land");
            help.GoAroundUseful();
            Assert.AreEqual(HelpHint.TapForOrders, help.Current, "Later hints wait for the first lesson");
            help.LandOrderGiven();
            help.Tick(FirstTimeHelp.ShortHintSeconds + 0.1f, true);
            help.GoAroundUseful();
            Assert.AreEqual(HelpHint.GoAround, help.Current);
            help.Tick(FirstTimeHelp.LongHintSeconds + 0.1f, true);
            help.HoldUseful();
            Assert.AreEqual(HelpHint.Hold, help.Current);
            StringAssert.Contains("Go around", FirstTimeHelp.Text(HelpHint.GoAround, true));
            StringAssert.Contains("Hold", FirstTimeHelp.Text(HelpHint.Hold, true));
        }

        [Test]
        public void ApproachesAndHoldFixes_AreOnTheIPadScreen()
        {
            foreach (string map in TowerTestData.Maps)
                for (int n = 1; n <= 5; n++)
                {
                    LevelData level = TowerTestData.Level(map, n);
                    Rect view = TowerTestData.IPadView(level.layout);
                    foreach (TowerApproach a in level.tower.approaches)
                    {
                        Assert.IsTrue(view.Contains(a.entry), $"{map} {n}: entry {a.entry} on screen");
                        Assert.LessOrEqual(a.entry.y, view.yMax - TowerModeSetup.TopBarHeight + 1e-3f, $"{map} {n}: not under the top bar");
                    }
                    foreach (Vector2 fix in level.tower.holdFixes)
                    {
                        Assert.IsTrue(view.Contains(fix + Vector2.one * level.tower.holdRadius) && view.Contains(fix - Vector2.one * level.tower.holdRadius),
                                      $"{map} {n}: hold circle at {fix} on screen");
                    }
                }
        }
    }
}
