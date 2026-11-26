using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// Fits a Tower-mode level to its map picture (blueprint 16.1, phase 3, 26 Nov 2026): every runway with both ends, the
    /// helipad and balloon field, the stands and taxi routes from the measured layout; one fixed approach route per runway
    /// end, entering at the screen edge; hold fixes near the edges. Plain C#, so it can be unit tested.
    ///
    /// Approach entries: on these pictures most runway ends lie within a fraction of a unit of the screen edge, so a
    /// straight-in final from the edge would leave no time to give an order. Each runway end therefore gets the entry on the
    /// screen edge (as the 4:3 iPad shows it) whose smooth route onto the final is closest to <see cref="WantedApproachLength"/>
    /// and stays on screen; the route is lined up with the runway for its last <see cref="TowerSettings.finalLength"/>.
    /// </summary>
    public static class TowerModeSetup
    {
        /// <summary>About 10 s of flying at the airliner's speed from the edge to the final (Claude's starting value).</summary>
        public const float WantedApproachLength = 6f;

        public const float AirlinerSpeed = 0.6f;
        public const float EdgeInset = 0.6f;

        /// <summary>The top bar of buttons covers about this much of the top of the screen: nothing enters or holds under it.</summary>
        public const float TopBarHeight = 1.7f;
        public const float IPadAspect = 4f / 3f;

        public static void Apply(LevelData level, PictureLayout layout, bool valleyOnly = false)
        {
            layout.ApplyTo(level);
            level.mode = GameMode.Tower;
            level.carrier.hasCarrier = false;
            level.rules.noFly.areas = new List<NoFlyArea>(); // no lines to stop in Tower mode
            level.tower.valleyOnly = valleyOnly;

            Rect view = IPadView(layout);
            level.tower.approaches = Approaches(level, view);
            level.tower.holdFixes = HoldFixes(layout, view, valleyOnly, level.tower.holdRadius);

            level.spawnPoints = new List<SpawnPoint>();
            foreach (TowerApproach approach in level.tower.approaches)
                level.spawnPoints.Add(new SpawnPoint(approach.entry, approach.headingDegrees));

            // The wind indicator sits just off the top right corner of the airport.
            Rect airport = layout.KeepVisibleWorld;
            level.wind.indicatorPosition = new Vector2(Mathf.Min(airport.xMax + 0.3f, view.xMax - 0.5f), Mathf.Min(airport.yMax - 0.2f, view.yMax - 0.6f));
        }

        /// <summary>What a 4:3 iPad shows of the picture.</summary>
        public static Rect IPadView(PictureLayout layout) => CameraFraming.View(layout.WorldRect, layout.KeepVisibleWorld, IPadAspect);

        /// <summary>One approach per runway end that the map allows (Mountain Valley: along the valley only).</summary>
        public static List<TowerApproach> Approaches(LevelData level, Rect view)
        {
            var list = new List<TowerApproach>();
            float radius = level.tower.TurnRadius(AirlinerSpeed);
            for (int r = 0; r < level.runways.Count; r++)
            {
                foreach (bool endB in new[] { false, true })
                {
                    Runway runway = endB ? level.runways[r].ToRunway().Reversed() : level.runways[r].ToRunway();
                    if (!level.tower.AllowsRunway(runway))
                        continue;
                    ApproachTarget target = TowerFlightPlanner.ForRunway(runway, r, level.tower.finalLength);
                    TowerApproach best = BestEntry(target, view, radius, level.tower.valleyOnly);
                    best.runway = r;
                    best.endB = endB;
                    list.Add(best);
                }
            }
            return list;
        }

        static TowerApproach BestEntry(ApproachTarget target, Rect view, float radius, bool valleyOnly)
        {
            Rect inner = Rect.MinMaxRect(view.xMin + EdgeInset, view.yMin + EdgeInset, view.xMax - EdgeInset, view.yMax - TopBarHeight);
            var candidates = new List<(Vector2 p, float inward)>();
            for (float x = inner.xMin; x <= inner.xMax + 1e-3f; x += 0.25f)
            {
                if (valleyOnly)
                    break;
                candidates.Add((new Vector2(x, inner.yMax), -90f));
                candidates.Add((new Vector2(x, inner.yMin), 90f));
            }
            for (float y = inner.yMin; y <= inner.yMax + 1e-3f; y += 0.25f)
            {
                candidates.Add((new Vector2(inner.xMin, y), 0f));
                candidates.Add((new Vector2(inner.xMax, y), 180f));
            }

            TowerApproach best = null;
            float bestScore = float.MaxValue;
            Rect onScreen = new Rect(view.xMin - 0.1f, view.yMin - 0.1f, view.width + 0.2f, view.height + 0.2f);
            foreach ((Vector2 p, float inward) in candidates)
            {
                if (valleyOnly && Mathf.Abs(p.y - target.Fix.y) > 2.2f)
                    continue; // the valley: enter at about the runway's height
                foreach (float turn in valleyOnly ? new[] { 0f } : new[] { 0f, 30f, -30f })
                {
                    float heading = inward + turn;
                    float length = Dubins.Length(p, heading, target.Fix, target.HeadingDegrees, radius);
                    if (float.IsInfinity(length) || length < 2.5f)
                        continue;
                    List<Vector2> path = Dubins.Path(p, heading, target.Fix, target.HeadingDegrees, radius, 0.15f);
                    float outside = 0f;
                    foreach (Vector2 q in path)
                        if (!onScreen.Contains(q))
                            outside += 0.15f;
                    float lineUp = Mathf.Abs(Mathf.DeltaAngle(heading, target.HeadingDegrees)) / 90f;
                    float score = Mathf.Abs(length - WantedApproachLength) + 4f * outside + 0.5f * lineUp;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = new TowerApproach { entry = p, headingDegrees = Mathf.Repeat(heading, 360f) };
                    }
                }
            }
            return best ?? new TowerApproach { entry = target.Fix, headingDegrees = target.HeadingDegrees };
        }

        /// <summary>Hold fixes near the screen edges: the corners and edge middles (Mountain Valley: the valley's two ends).</summary>
        public static List<Vector2> HoldFixes(PictureLayout layout, Rect view, bool valleyOnly, float holdRadius)
        {
            float inset = holdRadius + 0.55f;
            Rect r = Rect.MinMaxRect(view.xMin + inset, view.yMin + inset, view.xMax - inset, view.yMax - TopBarHeight - holdRadius - 0.1f);
            var fixes = new List<Vector2>();
            if (valleyOnly)
            {
                Rect valley = layout.KeepVisibleWorld;
                float yTop = Mathf.Min(r.yMax, valley.center.y + valley.height * 0.25f);
                float yBottom = Mathf.Max(r.yMin, valley.center.y - valley.height * 0.25f);
                fixes.Add(new Vector2(r.xMin, yTop));
                fixes.Add(new Vector2(r.xMin, yBottom));
                fixes.Add(new Vector2(r.xMax, yTop));
                fixes.Add(new Vector2(r.xMax, yBottom));
                return fixes;
            }
            fixes.Add(new Vector2(r.xMin, r.yMax));
            fixes.Add(new Vector2(r.center.x, r.yMax));
            fixes.Add(new Vector2(r.xMax, r.yMax));
            fixes.Add(new Vector2(r.xMax, r.center.y));
            fixes.Add(new Vector2(r.xMax, r.yMin));
            fixes.Add(new Vector2(r.center.x, r.yMin));
            fixes.Add(new Vector2(r.xMin, r.yMin));
            fixes.Add(new Vector2(r.xMin, r.center.y));
            return fixes;
        }

        // ---------------------------------------------------------------- Length estimate (C3: 60 to 120 s)

        /// <summary>The player's time to notice an aircraft and press a button.</summary>
        public const float PlayerSecondsPerCommand = 1.5f;

        /// <summary>A departure's time on the runway (line up, roll, lift-off), which arrivals have to wait for.</summary>
        public const float TakeOffRunwaySeconds = 6f;

        /// <summary>
        /// A rough estimate of a Tower level's length at 1x (C3), from its data: each arrival flies its approach route
        /// (measured), gets Land (one command) and touches down; the next one enters after it. With several in the air these
        /// overlap. The last arrival still rolls out, leaves the runway and taxis to a stand (measured routes). Departures add
        /// their commands (Taxi, Take off, sometimes Cross) and their time on the runway; their taxiing overlaps the arrivals.
        /// A check on the level's numbers, not a replacement for timing real play on the iPad.
        /// </summary>
        public static float EstimateSeconds(LevelData level)
        {
            TowerSettings tower = level.tower;
            float approachSeconds = 0f;
            int samples = 0;
            foreach (AircraftType kind in level.aircraftTypes)
            {
                if (kind == null)
                    continue;
                float speed = AirlinerSpeed * kind.speedMultiplier;
                foreach (TowerApproach approach in tower.approaches)
                {
                    float length;
                    if (kind.landsOn == LandingZoneKind.Runway)
                    {
                        Runway runway = approach.endB ? level.runways[approach.runway].ToRunway().Reversed() : level.runways[approach.runway].ToRunway();
                        ApproachTarget target = TowerFlightPlanner.ForRunway(runway, approach.runway, tower.finalLength);
                        length = Dubins.Length(approach.entry, approach.headingDegrees, target.Fix, target.HeadingDegrees, tower.TurnRadius(speed))
                                 + Vector2.Distance(target.Fix, target.Touchdown);
                    }
                    else
                    {
                        float best = float.MaxValue;
                        foreach (LandingPadDefinition pad in level.landingPads)
                            if (pad.kind == kind.landsOn)
                                best = Mathf.Min(best, Vector2.Distance(approach.entry, pad.centre) * 1.15f);
                        if (best == float.MaxValue)
                            continue;
                        length = best;
                    }
                    approachSeconds += length / speed;
                    samples++;
                }
            }
            if (samples == 0)
                return 0f;
            approachSeconds /= samples;

            int landings = Mathf.Max(0, level.landingsToClear);
            int inAir = Mathf.Max(1, Mathf.Min(level.aircraftInAir, Mathf.Max(1, landings)));
            float perArrival = approachSeconds + PlayerSecondsPerCommand + 1f; // + the wait before the next one enters
            float arrivals = landings * perArrival / inAir;

            // The last arrival on the ground: roll-out and exit, then Taxi to stand along an average route.
            float groundTail = (tower.rollDistance + 2.5f) / ((AirlinerSpeed + tower.taxiSpeed) / 2f)
                               + PlayerSecondsPerCommand + AverageStandRoute(level) / tower.taxiSpeed + tower.parkedSeconds;

            float departures = Mathf.Max(0, level.departuresToClear) * (2.5f * PlayerSecondsPerCommand + TakeOffRunwaySeconds);
            return arrivals + groundTail + departures;
        }

        static float AverageStandRoute(LevelData level)
        {
            if (level.layout == null)
                return 4f;
            TaxiNetwork network = TaxiNetwork.Build(level.layout);
            var stands = new List<int>();
            for (int s = 0; s < network.StandCount; s++)
                stands.Add(network.StandNode(s));
            float total = 0f;
            int count = 0;
            for (int r = 0; r < network.Runways.Count; r++)
                foreach (int hold in network.HoldNodes(r))
                {
                    TaxiRoute route = network.FindRoute(hold, stands);
                    if (route == null)
                        continue;
                    total += route.Length;
                    count++;
                }
            return count > 0 ? total / count : 4f;
        }
    }
}
