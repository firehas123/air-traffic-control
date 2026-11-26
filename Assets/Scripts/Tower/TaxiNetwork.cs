using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// The painted taxiways of a Tower picture as a network (blueprint 16.1, phase 3): every measured taxi route, joined
    /// where routes meet or cross, with a node at every hold line, runway crossing point and stand. Routes between nodes
    /// follow the painted centre lines. An edge that lies on a runway is a runway crossing: a route stops before it and
    /// waits for Cross. Everything is in world units. Plain C#, so it can be unit tested.
    /// </summary>
    public sealed class TaxiNetwork
    {
        /// <summary>Route ends and hold lines closer than this to a route (picture pixels) join it.</summary>
        public const float JoinPixels = 9f;

        /// <summary>Extra cost (world units) of crossing a runway, so routes avoid crossings when another way exists.</summary>
        public const float CrossingPenalty = 6f;

        public enum NodeKind { Junction, Hold, Crossing, Stand }

        public sealed class Node
        {
            public Vector2 position;
            public NodeKind kind;

            /// <summary>For a hold line: its runway (index in the layout), else -1.</summary>
            public int runway = -1;

            /// <summary>For a hold line: End A, End B or Crossing (<see cref="PictureHoldLine"/>).</summary>
            public string holdKind;

            /// <summary>For a stand: its index in the layout, else -1.</summary>
            public int stand = -1;

            public readonly List<Edge> edges = new List<Edge>();
        }

        public readonly struct Edge
        {
            public Edge(int to, float length, int runway)
            {
                To = to;
                Length = length;
                Runway = runway;
            }

            public int To { get; }
            public float Length { get; }

            /// <summary>The runway this edge lies on (a crossing), or -1 for a taxiway.</summary>
            public int Runway { get; }
        }

        readonly List<Node> nodes = new List<Node>();
        readonly List<Runway> runways = new List<Runway>();
        readonly List<int> standNodes = new List<int>();

        public IReadOnlyList<Node> Nodes => nodes;
        public IReadOnlyList<Runway> Runways => runways;
        public int StandCount => standNodes.Count;
        public int StandNode(int stand) => standNodes[stand];

        /// <summary>Builds the network of a picture's measured layout.</summary>
        public static TaxiNetwork Build(PictureLayout layout)
        {
            var network = new TaxiNetwork();
            foreach (PictureRunway runway in layout.runways)
                network.runways.Add(layout.ToRunwayDefinition(runway).ToRunway());

            // Polylines in pixels; joins are found in pixels, where the measuring was done.
            var lines = new List<List<Vector2>>();
            foreach (PicturePath route in layout.taxiRoutes)
                if (route.points.Count >= 2)
                    lines.Add(new List<Vector2>(route.points));

            // Every route end and hold line that lies on another route splits it there; two routes that cross get a
            // shared point.
            var splitPoints = new List<Vector2>();
            foreach (List<Vector2> line in lines)
            {
                splitPoints.Add(line[0]);
                splitPoints.Add(line[line.Count - 1]);
            }
            foreach (PictureHoldLine hold in layout.holdLines)
                splitPoints.Add(hold.point);
            foreach (PictureCrossing crossing in layout.crossings)
                splitPoints.Add(crossing.point);
            for (int i = 0; i < lines.Count; i++)
                for (int j = i + 1; j < lines.Count; j++)
                    AddCrossingPoints(lines[i], lines[j], splitPoints);
            foreach (List<Vector2> line in lines)
                foreach (Vector2 point in splitPoints)
                    InsertOnLine(line, point);

            // Nodes: route points, merged when they are within the join distance.
            var nodeOfPoint = new Dictionary<Vector2, int>();
            int NodeAt(Vector2 pixel)
            {
                if (nodeOfPoint.TryGetValue(pixel, out int found))
                    return found;
                for (int n = 0; n < network.nodes.Count; n++)
                    if (Vector2.Distance(layout.Pixel(network.nodes[n].position), pixel) <= JoinPixels)
                    {
                        nodeOfPoint[pixel] = n;
                        return n;
                    }
                network.nodes.Add(new Node { position = layout.World(pixel), kind = NodeKind.Junction });
                nodeOfPoint[pixel] = network.nodes.Count - 1;
                return network.nodes.Count - 1;
            }

            foreach (List<Vector2> line in lines)
                for (int k = 1; k < line.Count; k++)
                    network.Connect(NodeAt(line[k - 1]), NodeAt(line[k]));

            foreach (PictureHoldLine hold in layout.holdLines)
            {
                int n = NodeAt(hold.point);
                Node node = network.nodes[n];
                node.kind = NodeKind.Hold;
                node.runway = layout.runways.FindIndex(r => r.name == hold.runway);
                node.holdKind = hold.kind;
            }
            foreach (PictureCrossing crossing in layout.crossings)
            {
                int n = NodeAt(crossing.point);
                if (network.nodes[n].kind == NodeKind.Junction)
                    network.nodes[n].kind = NodeKind.Crossing;
            }

            // Stands: parked on the apron, joined by a straight line to the nearest taxiway point.
            for (int s = 0; s < layout.stands.Count; s++)
            {
                Vector2 position = layout.World(layout.stands[s].position);
                int nearest = network.Nearest(position, n => network.nodes[n].kind != NodeKind.Stand);
                network.nodes.Add(new Node { position = position, kind = NodeKind.Stand, stand = s });
                int standNode = network.nodes.Count - 1;
                network.standNodes.Add(standNode);
                if (nearest >= 0)
                    network.Connect(standNode, nearest);
            }
            return network;
        }

        void Connect(int a, int b)
        {
            if (a == b)
                return;
            foreach (Edge e in nodes[a].edges)
                if (e.To == b)
                    return;
            Vector2 pa = nodes[a].position, pb = nodes[b].position;
            int runway = RunwayUnder(pa, pb);
            float length = Vector2.Distance(pa, pb);
            nodes[a].edges.Add(new Edge(b, length, runway));
            nodes[b].edges.Add(new Edge(a, length, runway));
        }

        // The runway an edge lies on: its middle is on the runway's surface.
        int RunwayUnder(Vector2 a, Vector2 b)
        {
            Vector2 middle = (a + b) / 2f;
            for (int r = 0; r < runways.Count; r++)
                if (runways[r].Contains(middle, -0.02f))
                    return r;
            return -1;
        }

        static void AddCrossingPoints(List<Vector2> a, List<Vector2> b, List<Vector2> into)
        {
            for (int i = 1; i < a.Count; i++)
                for (int j = 1; j < b.Count; j++)
                    if (SegmentIntersection(a[i - 1], a[i], b[j - 1], b[j], out Vector2 p))
                        into.Add(p);
        }

        static bool SegmentIntersection(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, out Vector2 point)
        {
            point = Vector2.zero;
            Vector2 r = p2 - p1, s = p4 - p3;
            float denominator = r.x * s.y - r.y * s.x;
            if (Mathf.Abs(denominator) < 1e-6f)
                return false;
            Vector2 qp = p3 - p1;
            float t = (qp.x * s.y - qp.y * s.x) / denominator;
            float u = (qp.x * r.y - qp.y * r.x) / denominator;
            const float inside = 0.02f; // strictly inside both: ends are handled as join points
            if (t <= inside || t >= 1f - inside || u <= inside || u >= 1f - inside)
                return false;
            point = p1 + r * t;
            return true;
        }

        // Puts the point into the polyline where it lies on (or within the join distance of) a segment.
        static void InsertOnLine(List<Vector2> line, Vector2 point)
        {
            for (int k = 0; k < line.Count; k++)
                if (Vector2.Distance(line[k], point) <= JoinPixels)
                    return; // already a point of the line (or close enough to merge)
            for (int k = 1; k < line.Count; k++)
            {
                Vector2 a = line[k - 1], ab = line[k] - a;
                float t = Vector2.Dot(point - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude);
                if (t <= 0f || t >= 1f)
                    continue;
                if (Vector2.Distance(a + ab * t, point) <= JoinPixels)
                {
                    line.Insert(k, a + ab * t);
                    return;
                }
            }
        }

        /// <summary>The node nearest to <paramref name="position"/> that passes <paramref name="allowed"/>, or -1.</summary>
        public int Nearest(Vector2 position, System.Predicate<int> allowed = null)
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int n = 0; n < nodes.Count; n++)
            {
                if (allowed != null && !allowed(n))
                    continue;
                float d = Vector2.Distance(nodes[n].position, position);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = n;
                }
            }
            return best;
        }

        /// <summary>Hold-line nodes of a runway (index in the layout): its ends and its crossing points.</summary>
        public List<int> HoldNodes(int runway)
        {
            var list = new List<int>();
            for (int n = 0; n < nodes.Count; n++)
                if (nodes[n].kind == NodeKind.Hold && nodes[n].runway == runway)
                    list.Add(n);
            return list;
        }

        /// <summary>Hold lines where a departure waits to line up at one end of a runway (end B when <paramref name="endB"/>).</summary>
        public List<int> EndHoldNodes(int runway, bool endB)
        {
            string kind = endB ? PictureHoldLine.EndB : PictureHoldLine.EndA;
            var list = new List<int>();
            for (int n = 0; n < nodes.Count; n++)
                if (nodes[n].kind == NodeKind.Hold && nodes[n].runway == runway && nodes[n].holdKind == kind)
                    list.Add(n);
            return list;
        }

        /// <summary>
        /// The shortest route from node <paramref name="from"/> to any of <paramref name="goals"/>, along the taxiways.
        /// Crossing a runway costs <see cref="CrossingPenalty"/> extra. Null when none is reachable.
        /// </summary>
        public TaxiRoute FindRoute(int from, IReadOnlyCollection<int> goals)
        {
            if (from < 0 || goals == null || goals.Count == 0)
                return null;
            var goalSet = new HashSet<int>(goals);
            var cost = new float[nodes.Count];
            var previous = new int[nodes.Count];
            var done = new bool[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
            {
                cost[i] = float.PositiveInfinity;
                previous[i] = -1;
            }
            cost[from] = 0f;
            while (true)
            {
                int current = -1;
                for (int i = 0; i < nodes.Count; i++)
                    if (!done[i] && !float.IsPositiveInfinity(cost[i]) && (current < 0 || cost[i] < cost[current]))
                        current = i;
                if (current < 0)
                    return null;
                if (goalSet.Contains(current))
                    return BuildRoute(from, current, previous);
                done[current] = true;
                // Stands are places to park, never a way through.
                if (current != from && nodes[current].kind == NodeKind.Stand)
                    continue;
                foreach (Edge edge in nodes[current].edges)
                {
                    float c = cost[current] + edge.Length + (edge.Runway >= 0 ? CrossingPenalty : 0f);
                    if (c < cost[edge.To])
                    {
                        cost[edge.To] = c;
                        previous[edge.To] = current;
                    }
                }
            }
        }

        public TaxiRoute FindRoute(int from, int to) => FindRoute(from, new[] { to });

        TaxiRoute BuildRoute(int from, int to, int[] previous)
        {
            var path = new List<int>();
            for (int n = to; n >= 0; n = n == from ? -1 : previous[n])
                path.Add(n);
            path.Reverse();
            var route = new TaxiRoute();
            foreach (int n in path)
            {
                route.nodes.Add(n);
                route.points.Add(nodes[n].position);
            }
            // Edge e joins points e and e + 1. A run of edges on one runway is one crossing: wait at its first point,
            // clear of the runway at the point after its last edge.
            for (int e = 0; e < path.Count - 1; e++)
            {
                int runway = EdgeRunway(path[e], path[e + 1]);
                if (runway < 0 || (e > 0 && EdgeRunway(path[e - 1], path[e]) == runway))
                    continue;
                int last = e;
                while (last + 1 < path.Count - 1 && EdgeRunway(path[last + 1], path[last + 2]) == runway)
                    last++;
                route.crossings.Add(new TaxiRoute.Stop(e, runway, last + 1));
            }
            return route;
        }

        /// <summary>Stand nodes, for routes to any stand.</summary>
        public List<int> StandNodes()
        {
            var list = new List<int>();
            for (int s = 0; s < standNodes.Count; s++)
                list.Add(standNodes[s]);
            return list;
        }

        /// <summary>The exits of a runway: its hold lines from which a stand can be reached.</summary>
        public List<int> Exits(int runway)
        {
            if (exitCache.TryGetValue(runway, out List<int> cached))
                return cached;
            var exits = new List<int>();
            List<int> stands = StandNodes();
            foreach (int hold in HoldNodes(runway))
                if (FindRoute(hold, stands) != null)
                    exits.Add(hold);
            exitCache[runway] = exits;
            return exits;
        }

        readonly Dictionary<int, List<int>> exitCache = new Dictionary<int, List<int>>();
        readonly Dictionary<int, float> standDistanceCache = new Dictionary<int, float>();

        /// <summary>Route length from a node to the nearest stand (crossings count extra), or 1000 when none is reachable.</summary>
        public float StandDistance(int node)
        {
            if (standDistanceCache.TryGetValue(node, out float d))
                return d;
            TaxiRoute route = FindRoute(node, StandNodes());
            d = route != null ? route.Length + route.crossings.Count * CrossingPenalty : 1000f;
            standDistanceCache[node] = d;
            return d;
        }

        /// <summary>
        /// The way off runway <paramref name="index"/> (flown in the direction of <paramref name="runway"/>) for an aircraft
        /// that has slowed down <paramref name="stopAlong"/> from the threshold: the next exit ahead, whose hold line leads to
        /// a stand. After the roll (<paramref name="rollEnd"/> = <paramref name="stopAlong"/>) it carries on along the runway
        /// until it is level with that exit, then turns off to the hold line. Of two exits level with each other (both sides of the runway) it takes the one closer to the
        /// stands, and avoids one where another aircraft already waits. With no exit ahead it turns back to the nearest one
        /// behind. The returned points start where the roll ends.
        /// </summary>
        public List<Vector2> ExitPath(int index, Runway runway, float stopAlong, System.Func<int, bool> occupied, out int exitNode, out float rollEnd)
        {
            exitNode = -1;
            rollEnd = stopAlong;
            var points = new List<Vector2>();
            int best = -1;
            float bestAlong = float.MaxValue, bestTie = float.MaxValue;
            int behind = -1;
            float behindAlong = float.MinValue;
            foreach (int node in Exits(index))
            {
                Vector2 p = nodes[node].position;
                float along = Vector2.Dot(p - runway.Threshold, runway.Direction);
                float tie = StandDistance(node) + (occupied != null && occupied(node) ? 50f : 0f);
                if (along >= stopAlong - 0.1f)
                {
                    bool clearlyCloser = along < bestAlong - 0.4f;
                    bool level = Mathf.Abs(along - bestAlong) <= 0.4f;
                    if (best < 0 || clearlyCloser || (level && tie < bestTie))
                    {
                        best = node;
                        bestAlong = clearlyCloser || best < 0 ? along : Mathf.Min(along, bestAlong);
                        bestTie = tie;
                    }
                }
                else if (along > behindAlong)
                {
                    behind = node;
                    behindAlong = along;
                }
            }
            Vector2 stop = runway.Threshold + runway.Direction * stopAlong;
            if (best >= 0)
            {
                exitNode = best;
                float along = Mathf.Clamp(Vector2.Dot(nodes[best].position - runway.Threshold, runway.Direction), stopAlong, runway.Length);
                points.Add(stop);
                if (along - stopAlong > 1e-3f)
                    points.Add(runway.Threshold + runway.Direction * along);
                points.Add(nodes[best].position);
            }
            else if (behind >= 0)
            {
                exitNode = behind;
                points.Add(stop);
                points.Add(runway.Threshold + runway.Direction * behindAlong);
                points.Add(nodes[behind].position);
            }
            else
            {
                points.Add(stop); // no exit at all: it stops on the runway (a layout without hold lines)
            }
            return points;
        }

        public int EdgeRunway(int a, int b)
        {
            foreach (Edge e in nodes[a].edges)
                if (e.To == b)
                    return e.Runway;
            return -1;
        }
    }

    /// <summary>A route along the taxiways: its points (world units), the nodes they are, and where it must wait for Cross.</summary>
    public sealed class TaxiRoute
    {
        public readonly struct Stop
        {
            public Stop(int pointIndex, int runway, int clearPointIndex)
            {
                PointIndex = pointIndex;
                Runway = runway;
                ClearPointIndex = clearPointIndex;
            }

            /// <summary>The route point where it waits; the segment after it is on the runway.</summary>
            public int PointIndex { get; }
            public int Runway { get; }

            /// <summary>The first route point clear of the runway on the far side.</summary>
            public int ClearPointIndex { get; }

            public Stop Shifted(int by) => new Stop(PointIndex + by, Runway, ClearPointIndex + by);
        }

        public readonly List<Vector2> points = new List<Vector2>();
        public readonly List<int> nodes = new List<int>();
        public readonly List<Stop> crossings = new List<Stop>();

        public int LastNode => nodes.Count > 0 ? nodes[nodes.Count - 1] : -1;

        public float Length
        {
            get
            {
                float total = 0f;
                for (int i = 1; i < points.Count; i++)
                    total += Vector2.Distance(points[i - 1], points[i]);
                return total;
            }
        }
    }
}
