using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// Records the drawn line exactly as the finger moved: every finger sample becomes a point,
    /// with no resampling or smoothing. The only samples left out are repeats closer than
    /// <see cref="MinSampleDistance"/> to the previous point, which would add zero-length segments.
    /// Plain C#, no Unity components, so it can be unit tested.
    /// </summary>
    public sealed class PathRecorder
    {
        readonly List<Vector2> points = new List<Vector2>();

        public PathRecorder(float minSampleDistance)
        {
            MinSampleDistance = Mathf.Max(0f, minSampleDistance);
        }

        /// <summary>Samples closer than this to the previous point are left out, in world units.</summary>
        public float MinSampleDistance { get; }

        public bool IsRecording { get; private set; }

        /// <summary>All points of the current line, starting with the point passed to <see cref="Begin"/>.</summary>
        public IReadOnlyList<Vector2> Points => points;

        public void Begin(Vector2 start)
        {
            points.Clear();
            points.Add(start);
            IsRecording = true;
        }

        /// <summary>
        /// Adds a finger sample as a point, unless it repeats the previous point. If a point is added
        /// it is also added to <paramref name="added"/>, if that is given. Returns how many points were added (0 or 1).
        /// </summary>
        public int AddSample(Vector2 sample, List<Vector2> added = null)
        {
            if (!IsRecording)
                return 0;

            Vector2 last = points[points.Count - 1];
            float distance = Vector2.Distance(last, sample);
            if (distance <= 0f || distance < MinSampleDistance)
                return 0;

            points.Add(sample);
            added?.Add(sample);
            return 1;
        }

        public void End()
        {
            IsRecording = false;
        }
    }
}
