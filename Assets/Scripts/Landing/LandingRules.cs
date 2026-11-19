using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// Decides whether a line lands on a runway (owner decisions, 15 Nov 2026).
    /// The drawn line must end inside the active touchdown zone (the highlighted area with the arrows),
    /// overlapping it by at least <see cref="ConnectInset"/>.
    /// The direction the line arrives from does not matter: the owner asked for this flexibility so that
    /// an aircraft coming from the far side can be brought straight onto the arrows. The aircraft lines up
    /// with the runway at touchdown. Lines that do not land leave the aircraft carrying straight on (C5).
    /// Plain C#, no Unity components, so it can be unit tested.
    /// </summary>
    public static class LandingRules
    {
        /// <summary>
        /// Largest difference between the approach and the landing direction that still lands.
        /// 180 means any direction (owner decision, 15 Nov 2026; it was 30 before).
        /// </summary>
        public const float MaxAlignmentErrorDegrees = 180f;

        /// <summary>
        /// The approach direction is measured over this much of the end of the line, so a twitch of the
        /// finger as it lifts does not decide the landing.
        /// </summary>
        public const float ApproachSampleLength = 0.3f;

        /// <summary>
        /// The line must reach at least this far inside the touchdown zone, on every side, before it lands
        /// (about 2.4 mm on a 12.9" iPad; the runway is 0.6 wide). Owner decision 15 Nov 2026: the line should
        /// overlap the runway, not connect as soon as it touches the zone's edge. A line ending before the
        /// runway never lands.
        /// </summary>
        public const float ConnectInset = 0.12f;

        /// <summary>
        /// The direction of travel over the last <see cref="ApproachSampleLength"/> of the line
        /// (or the whole line if it is shorter). False if the line has no length.
        /// </summary>
        public static bool TryGetApproachHeading(IReadOnlyList<Vector2> line, out float headingDegrees)
        {
            headingDegrees = 0f;
            if (line == null || line.Count < 2)
                return false;

            Vector2 end = line[line.Count - 1];
            Vector2 from = end;
            float measured = 0f;
            for (int i = line.Count - 2; i >= 0 && measured < ApproachSampleLength; i--)
            {
                measured += Vector2.Distance(line[i], line[i + 1]);
                from = line[i];
            }

            Vector2 direction = end - from;
            if (direction.sqrMagnitude <= 0f)
                return false;

            headingDegrees = Mathf.Repeat(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, 360f);
            return true;
        }

        public static bool CanLand(Runway runway, IReadOnlyList<Vector2> line) =>
            CanLand(runway, line, MaxAlignmentErrorDegrees);

        /// <summary>As <see cref="CanLand(Runway, IReadOnlyList{Vector2})"/>, with a stricter approach angle if wanted.</summary>
        public static bool CanLand(Runway runway, IReadOnlyList<Vector2> line, float maxAlignmentErrorDegrees)
        {
            if (line == null || line.Count < 2)
                return false;
            if (!runway.InTouchdownZone(line[line.Count - 1], ConnectInset))
                return false;
            if (!TryGetApproachHeading(line, out float heading))
                return false;
            return Mathf.Abs(Mathf.DeltaAngle(heading, runway.LandingHeadingDegrees)) <= maxAlignmentErrorDegrees;
        }

        /// <summary>Index of the first runway the line lands on, or -1.</summary>
        public static int FindLandingRunway(IReadOnlyList<Runway> runways, IReadOnlyList<Vector2> line)
        {
            for (int i = 0; i < runways.Count; i++)
            {
                if (CanLand(runways[i], line))
                    return i;
            }
            return -1;
        }

        /// <summary>True if the line ends inside the pad, overlapping it by at least <see cref="ConnectInset"/>, from any direction.</summary>
        public static bool CanLandOnPad(LandingPad pad, IReadOnlyList<Vector2> line)
        {
            if (line == null || line.Count < 2)
                return false;
            Vector2 end = line[line.Count - 1];
            if (line[line.Count - 2] == end && line.Count == 2)
                return false; // a line with no length
            return pad.Contains(end, ConnectInset);
        }

        /// <summary>
        /// Finds where a line lands for an aircraft that uses zones of <paramref name="kind"/> (blueprint 13.1: a line
        /// lands only on a zone that matches the aircraft kind). False if it lands nowhere.
        /// </summary>
        public static bool TryFindTouchdown(IReadOnlyList<Runway> runways, IReadOnlyList<LandingPad> pads,
                                            IReadOnlyList<Vector2> line, LandingZoneKind kind, out Touchdown touchdown)
        {
            if (kind == LandingZoneKind.Runway)
            {
                int index = FindLandingRunway(runways, line);
                touchdown = index >= 0 ? Touchdown.OnRunway(runways[index]) : default;
                return index >= 0;
            }

            for (int i = 0; i < pads.Count; i++)
            {
                if (pads[i].Kind == kind && CanLandOnPad(pads[i], line))
                {
                    touchdown = Touchdown.OnPad(pads[i]);
                    return true;
                }
            }
            touchdown = default;
            return false;
        }
    }
}
