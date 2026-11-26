using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// Shortest smooth path between two poses (position and heading) for an aircraft that turns no tighter than a given
    /// radius: a Dubins path, made of up to three parts, each a left turn, a right turn or a straight line. Tower mode
    /// (blueprint 16.1) uses it for the fixed approach routes, go-arounds and holds: the aircraft follows the sampled points
    /// exactly, like a drawn line. Headings are in degrees, 0 = +x, counter-clockwise. Plain C#, so it can be unit tested.
    /// </summary>
    public static class Dubins
    {
        enum Part { L, S, R }

        static readonly Part[][] Words =
        {
            new[] { Part.L, Part.S, Part.L },
            new[] { Part.R, Part.S, Part.R },
            new[] { Part.L, Part.S, Part.R },
            new[] { Part.R, Part.S, Part.L },
            new[] { Part.R, Part.L, Part.R },
            new[] { Part.L, Part.R, Part.L },
        };

        const float TwoPi = Mathf.PI * 2f;

        static float Mod2Pi(float a)
        {
            a %= TwoPi;
            return a < 0f ? a + TwoPi : a;
        }

        /// <summary>
        /// Length of the shortest path from (<paramref name="from"/>, <paramref name="fromHeading"/>) to
        /// (<paramref name="to"/>, <paramref name="toHeading"/>) with turns of radius <paramref name="radius"/>.
        /// </summary>
        public static float Length(Vector2 from, float fromHeading, Vector2 to, float toHeading, float radius)
        {
            return Best(from, fromHeading, to, toHeading, radius, out _, out _, out float length) ? length : float.PositiveInfinity;
        }

        /// <summary>
        /// The shortest path, sampled every <paramref name="step"/> world units. The first point is <paramref name="from"/>
        /// and the last is exactly <paramref name="to"/>. Returns an empty list only for a zero radius or step.
        /// </summary>
        public static List<Vector2> Path(Vector2 from, float fromHeading, Vector2 to, float toHeading, float radius, float step)
        {
            var points = new List<Vector2>();
            AppendPath(points, from, fromHeading, to, toHeading, radius, step);
            return points;
        }

        /// <summary>
        /// Adds the sampled path to <paramref name="points"/>: <paramref name="from"/> itself only when the list is empty,
        /// then every <paramref name="step"/> units, ending exactly on <paramref name="to"/>.
        /// </summary>
        public static void AppendPath(List<Vector2> points, Vector2 from, float fromHeading, Vector2 to, float toHeading,
                                      float radius, float step)
        {
            if (radius <= 0f || step <= 0f)
                return;
            if (points.Count == 0)
                points.Add(from);
            if (!Best(from, fromHeading, to, toHeading, radius, out Part[] word, out Vector3 lengths, out _))
            {
                points.Add(to); // cannot happen for a positive radius; a straight line keeps the aircraft moving
                return;
            }

            Vector2 position = from;
            float heading = fromHeading * Mathf.Deg2Rad;
            for (int i = 0; i < 3; i++)
            {
                float length = lengths[i] * radius;
                int samples = Mathf.Max(1, Mathf.CeilToInt(length / step));
                Vector2 start = position;
                float startHeading = heading;
                for (int k = 1; k <= samples; k++)
                {
                    float s = length * k / samples;
                    Advance(word[i], start, startHeading, s, radius, out position, out heading);
                    if (length > 1e-5f)
                        points.Add(position);
                }
                Advance(word[i], start, startHeading, length, radius, out position, out heading);
            }
            // Float rounding: end exactly on the target.
            if (points.Count > 0)
                points[points.Count - 1] = to;
        }

        static void Advance(Part part, Vector2 start, float heading, float distance, float radius, out Vector2 position, out float newHeading)
        {
            switch (part)
            {
                case Part.S:
                    position = start + new Vector2(Mathf.Cos(heading), Mathf.Sin(heading)) * distance;
                    newHeading = heading;
                    return;
                case Part.L:
                {
                    Vector2 centre = start + new Vector2(-Mathf.Sin(heading), Mathf.Cos(heading)) * radius;
                    newHeading = heading + distance / radius;
                    position = centre + new Vector2(Mathf.Sin(newHeading), -Mathf.Cos(newHeading)) * radius;
                    return;
                }
                default:
                {
                    Vector2 centre = start + new Vector2(Mathf.Sin(heading), -Mathf.Cos(heading)) * radius;
                    newHeading = heading - distance / radius;
                    position = centre + new Vector2(-Mathf.Sin(newHeading), Mathf.Cos(newHeading)) * radius;
                    return;
                }
            }
        }

        // The six path words of a Dubins path; the shortest valid one whose end really reaches the target pose.
        static bool Best(Vector2 from, float fromHeading, Vector2 to, float toHeading, float radius, out Part[] bestWord, out Vector3 bestLengths, out float bestLength)
        {
            bestWord = null;
            bestLengths = Vector3.zero;
            bestLength = float.PositiveInfinity;
            if (radius <= 0f)
                return false;

            Vector2 delta = to - from;
            float d = delta.magnitude / radius;
            float theta = Mod2Pi(Mathf.Atan2(delta.y, delta.x));
            float a = Mod2Pi(fromHeading * Mathf.Deg2Rad - theta);
            float b = Mod2Pi(toHeading * Mathf.Deg2Rad - theta);

            for (int w = 0; w < Words.Length; w++)
            {
                if (!Solve(w, a, b, d, out Vector3 lengths))
                    continue;
                float total = (lengths.x + lengths.y + lengths.z) * radius;
                if (total >= bestLength)
                    continue;
                // Check by walking it: only a path that ends on the target pose counts (guards the formulas).
                Vector2 p = from;
                float h = fromHeading * Mathf.Deg2Rad;
                for (int i = 0; i < 3; i++)
                    Advance(Words[w][i], p, h, lengths[i] * radius, radius, out p, out h);
                float headingError = Mathf.Abs(Mathf.DeltaAngle(h * Mathf.Rad2Deg, toHeading));
                if (Vector2.Distance(p, to) > 1e-3f + 1e-4f * delta.magnitude || headingError > 0.5f)
                    continue;
                bestLength = total;
                bestWord = Words[w];
                bestLengths = lengths;
            }
            return bestWord != null;
        }

        // Normalised lengths (angles in radians for turns, distance / radius for straights) of one word.
        static bool Solve(int word, float a, float b, float d, out Vector3 l)
        {
            l = Vector3.zero;
            float sa = Mathf.Sin(a), sb = Mathf.Sin(b), ca = Mathf.Cos(a), cb = Mathf.Cos(b), cab = Mathf.Cos(a - b);
            switch (word)
            {
                case 0: // LSL
                {
                    float tmp0 = d + sa - sb;
                    float p2 = 2f + d * d - 2f * cab + 2f * d * (sa - sb);
                    if (p2 < 0f) return false;
                    float tmp1 = Mathf.Atan2(cb - ca, tmp0);
                    l = new Vector3(Mod2Pi(-a + tmp1), Mathf.Sqrt(p2), Mod2Pi(b - tmp1));
                    return true;
                }
                case 1: // RSR
                {
                    float tmp0 = d - sa + sb;
                    float p2 = 2f + d * d - 2f * cab + 2f * d * (sb - sa);
                    if (p2 < 0f) return false;
                    float tmp1 = Mathf.Atan2(ca - cb, tmp0);
                    l = new Vector3(Mod2Pi(a - tmp1), Mathf.Sqrt(p2), Mod2Pi(-b + tmp1));
                    return true;
                }
                case 2: // LSR
                {
                    float p2 = -2f + d * d + 2f * cab + 2f * d * (sa + sb);
                    if (p2 < 0f) return false;
                    float p = Mathf.Sqrt(p2);
                    float tmp2 = Mathf.Atan2(-ca - cb, d + sa + sb) - Mathf.Atan2(-2f, p);
                    l = new Vector3(Mod2Pi(-a + tmp2), p, Mod2Pi(-Mod2Pi(b) + tmp2));
                    return true;
                }
                case 3: // RSL
                {
                    float p2 = d * d - 2f + 2f * cab - 2f * d * (sa + sb);
                    if (p2 < 0f) return false;
                    float p = Mathf.Sqrt(p2);
                    float tmp2 = Mathf.Atan2(ca + cb, d - sa - sb) - Mathf.Atan2(2f, p);
                    l = new Vector3(Mod2Pi(a - tmp2), p, Mod2Pi(b - tmp2));
                    return true;
                }
                case 4: // RLR
                {
                    float tmp = (6f - d * d + 2f * cab + 2f * d * (sa - sb)) / 8f;
                    if (Mathf.Abs(tmp) > 1f) return false;
                    float p = Mod2Pi(TwoPi - Mathf.Acos(tmp));
                    float t = Mod2Pi(a - Mathf.Atan2(ca - cb, d - sa + sb) + p / 2f);
                    l = new Vector3(t, p, Mod2Pi(a - b - t + p));
                    return true;
                }
                default: // LRL
                {
                    float tmp = (6f - d * d + 2f * cab + 2f * d * (-sa + sb)) / 8f;
                    if (Mathf.Abs(tmp) > 1f) return false;
                    float p = Mod2Pi(TwoPi - Mathf.Acos(tmp));
                    float t = Mod2Pi(-a - Mathf.Atan2(ca - cb, d + sa - sb) + p / 2f);
                    l = new Vector3(t, p, Mod2Pi(Mod2Pi(b) - a - t + p));
                    return true;
                }
            }
        }
    }
}
