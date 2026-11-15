using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// Decides whether a line lands on a runway (owner decisions, 15 Nov 2026).
    /// The drawn line must end in the runway's touchdown zone (the highlighted area with the arrows),
    /// and the aircraft must be heading along the runway's landing direction. Otherwise the aircraft
    /// carries straight on (C5).
    /// Plain C#, no Unity components, so it can be unit tested.
    /// </summary>
    public static class LandingRules
    {
        /// <summary>Largest difference between the approach and the landing direction that still lands.</summary>
        public const float MaxAlignmentErrorDegrees = 30f;

        /// <summary>
        /// The approach direction is measured over this much of the end of the line, so a twitch of the
        /// finger as it lifts does not decide the landing.
        /// </summary>
        public const float ApproachSampleLength = 0.3f;

        /// <summary>
        /// The line may end this far beside the touchdown zone's side edges and still count (about 1 mm on
        /// a 12.9" iPad). There is no margin before the threshold: a line ending before the runway never lands.
        /// </summary>
        public const float SideMargin = 0.05f;

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

        public static bool CanLand(Runway runway, IReadOnlyList<Vector2> line)
        {
            if (line == null || line.Count < 2)
                return false;
            if (!runway.InTouchdownZone(line[line.Count - 1], SideMargin))
                return false;
            if (!TryGetApproachHeading(line, out float heading))
                return false;
            return Mathf.Abs(Mathf.DeltaAngle(heading, runway.LandingHeadingDegrees)) <= MaxAlignmentErrorDegrees;
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
    }
}
