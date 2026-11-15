using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// Listens to <see cref="InputAdapter"/>. A press on or near an aircraft selects it and starts a new
    /// line from the aircraft, replacing any old one, so routes can be redrawn in flight. Dragging extends
    /// the line, and releasing finishes it.
    ///
    /// The aircraft follows exactly the line drawn. After every new point, the followed path is checked
    /// against the drawn line, and any difference throws <see cref="PathAlteredException"/>.
    ///
    /// When the line reaches a runway in a way that lands (see <see cref="LandingRules"/>), it connects:
    /// the line ends there, the rest of the stroke is ignored, and the aircraft shows it is cleared to land.
    /// </summary>
    public sealed class PathPlanner : MonoBehaviour
    {
        // Generous touch target (blueprint section 4: accessibility). About 1.6 cm on a 12.9" iPad.
        [SerializeField] float selectRadius = 0.8f;

        // Leave out repeat samples closer than this (about 0.2 mm on a 12.9" iPad).
        [SerializeField] float minSampleDistance = 0.01f;

        readonly List<Vector2> newPoints = new List<Vector2>();
        InputAdapter input;
        LandingSystem landing;
        PathRecorder recorder;
        AircraftController selected;

        /// <summary>True while the player is drawing an aircraft's line (used to slow time, C4).</summary>
        public bool IsDrawing => selected != null && recorder != null && recorder.IsRecording;

        public void Initialise(InputAdapter inputAdapter, LandingSystem landingSystem)
        {
            landing = landingSystem;
            recorder = new PathRecorder(minSampleDistance);
            input = inputAdapter;
            input.PressStarted += OnPressStarted;
            input.Dragged += OnDragged;
            input.PressEnded += OnPressEnded;
        }

        void OnDestroy()
        {
            if (input == null)
                return;
            input.PressStarted -= OnPressStarted;
            input.Dragged -= OnDragged;
            input.PressEnded -= OnPressEnded;
        }

        void OnPressStarted(Vector2 worldPosition)
        {
            selected = AircraftController.FindNearest(worldPosition, selectRadius);
            if (selected == null)
                return;

            // The line starts at the aircraft, so the aircraft is on it from the first frame,
            // then runs to where the finger touched down.
            selected.Motion.BeginPath();
            selected.SetClearedToLand(false);
            recorder.Begin(selected.Motion.Position);
            AddPoint(worldPosition);
        }

        void OnDragged(Vector2 worldPosition)
        {
            AddPoint(worldPosition);
        }

        void OnPressEnded(Vector2 worldPosition)
        {
            AddPoint(worldPosition);
            EndStroke();
        }

        void AddPoint(Vector2 worldPosition)
        {
            if (selected == null)
                return;

            // The aircraft crashed (its path is cleared) or is rolling out: this stroke no longer belongs to anything.
            if (!selected.IsAirborne)
            {
                EndStroke();
                return;
            }

            newPoints.Clear();
            recorder.AddSample(worldPosition, newPoints);
            foreach (Vector2 point in newPoints)
            {
                if (!selected.Motion.AppendWaypoint(point))
                {
                    // The aircraft has flown off the end of the line (C5: it carries straight on).
                    // Stop attaching this stroke. Touch the aircraft again to draw a new line.
                    recorder.End();
                    selected = null;
                    return;
                }
            }

            PathIntegrity.CheckSameLine(recorder.Points, selected.Motion.Path);

            if (landing != null && landing.TryLand(recorder.Points, out _))
            {
                // Connected to the runway: end the line here and ignore the rest of the stroke.
                selected.SetClearedToLand(true);
                EndStroke();
            }
        }

        void EndStroke()
        {
            recorder.End();
            selected = null;
        }
    }
}
