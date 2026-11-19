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
    /// When the line reaches a zone that matches the aircraft kind in a way that lands (see <see cref="LandingRules"/>), it connects:
    /// the line ends there, the rest of the stroke is ignored, and the aircraft shows it is cleared to land.
    /// </summary>
    public sealed class PathPlanner : MonoBehaviour
    {
        // Generous touch target (blueprint section 4: accessibility). About 1.6 cm on a 12.9" iPad.
        [SerializeField] float selectRadius = 0.8f;

        // Leave out repeat samples closer than this (about 0.2 mm on a 12.9" iPad).
        [SerializeField] float minSampleDistance = 0.01f;

        readonly List<Vector2> newPoints = new List<Vector2>();

        /// <summary>Raised when a line connects to a landing zone or the carrier deck (for sound and first-time help).</summary>
        public event System.Action<AircraftController> LineConnected;
        InputAdapter input;
        LandingSystem landing;
        CarrierSystem carrier;
        PathRecorder recorder;
        AircraftController selected;

        /// <summary>True while the player is drawing an aircraft's line (used to slow time, C4).</summary>
        public bool IsDrawing => selected != null && recorder != null && recorder.IsRecording;

        /// <summary>The aircraft whose line is being drawn, or null.</summary>
        public AircraftController DrawingFor => IsDrawing ? selected : null;

        public void Initialise(InputAdapter inputAdapter, LandingSystem landingSystem, CarrierSystem carrierSystem = null)
        {
            landing = landingSystem;
            carrier = carrierSystem;
            recorder = new PathRecorder(minSampleDistance);
            input = inputAdapter;
            input.DragStarted += OnPressStarted; // a line starts only on a drag; a tap opens commands instead
            input.Dragged += OnDragged;
            input.PressEnded += OnPressEnded;
        }

        void OnDestroy()
        {
            if (input == null)
                return;
            input.DragStarted -= OnPressStarted;
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

            if (landing != null && landing.TryLand(recorder.Points, selected.Type.landsOn, out _))
            {
                // Connected to a matching zone: end the line here and ignore the rest of the stroke.
                selected.SetClearedToLand(true);
                LineConnected?.Invoke(selected);
                EndStroke();
            }
            else if (carrier != null && selected.Type.landsOnCarrier && carrier.TryConnect(recorder.Points))
            {
                // Connected to the carrier deck (blueprint 13.2): the line ends here; a tether follows the ship.
                selected.ConnectToCarrier(carrier.CurrentPass);
                LineConnected?.Invoke(selected);
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
