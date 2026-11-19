using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AirTrafficControl
{
    /// <summary>
    /// Turns device input into three commands in world coordinates: press started, dragged, press ended.
    /// Game code listens to these events and never reads the touchscreen directly, so mouse
    /// (already working through <see cref="Pointer"/>) and a gamepad cursor can be added later here alone.
    /// </summary>
    public sealed class InputAdapter : MonoBehaviour
    {
        Camera worldCamera;
        Vector2 lastWorldPosition;
        Func<Vector2, bool> isScreenPointBlocked;
        bool ignoringThisPress;
        readonly GestureClassifier gesture = new GestureClassifier();

        /// <summary>A press has become a drag (moved 0.15 units or held 0.25 s), at the point where it started.</summary>
        public event Action<Vector2> DragStarted;

        /// <summary>The finger moved while dragging.</summary>
        public event Action<Vector2> Dragged;

        /// <summary>A drag ended where the finger lifted.</summary>
        public event Action<Vector2> PressEnded;

        /// <summary>A tap: released within 0.25 s and moved less than 0.15 units (blueprint 15.4), at the point pressed.</summary>
        public event Action<Vector2> Tapped;

        /// <summary>True while a finger (or mouse button) is held down.</summary>
        public bool IsPressing { get; private set; }

        public void Initialise(Camera camera)
        {
            worldCamera = camera;
        }

        /// <summary>
        /// A press that starts where <paramref name="blocked"/> returns true (a screen point on a UI button)
        /// is ignored until it is released, so tapping the UI never selects an aircraft or draws.
        /// </summary>
        public void SetScreenBlocker(Func<Vector2, bool> blocked)
        {
            isScreenPointBlocked = blocked;
        }

        void OnDisable()
        {
            // A press in progress when input is switched off does not carry over.
            IsPressing = false;
            ignoringThisPress = false;
            gesture.Cancel();
        }

        void Update()
        {
            // Pointer.current is whichever pointer was used last: the touchscreen on a tablet or phone,
            // the mouse in the Unity editor. For a touchscreen it reports the primary (first) finger.
            Pointer pointer = Pointer.current;
            if (pointer == null || worldCamera == null)
                return;

            bool pressed = pointer.press.isPressed;
            float now = Time.unscaledTime; // real time, so slow draw does not change what counts as a tap

            if (ignoringThisPress)
            {
                if (!pressed)
                    ignoringThisPress = false;
                return;
            }

            if (pressed && !IsPressing && isScreenPointBlocked != null && isScreenPointBlocked(pointer.position.ReadValue()))
            {
                ignoringThisPress = true;
                return;
            }

            if (pressed)
            {
                lastWorldPosition = ScreenToWorld(pointer.position.ReadValue());
                if (!IsPressing)
                {
                    IsPressing = true;
                    gesture.Press(lastWorldPosition, now);
                }
                else if (gesture.IsDrag)
                {
                    Dragged?.Invoke(lastWorldPosition);
                }
                else if (gesture.Hold(lastWorldPosition, now))
                {
                    DragStarted?.Invoke(gesture.Start);
                    Dragged?.Invoke(lastWorldPosition);
                }
            }
            else if (IsPressing)
            {
                // A released touch may not report a fresh position, so reuse the last one seen.
                IsPressing = false;
                bool wasDrag = gesture.IsDrag;
                GestureEnd end = gesture.Release(lastWorldPosition, now);
                if (end == GestureEnd.Tap)
                {
                    Tapped?.Invoke(gesture.Start);
                }
                else if (end == GestureEnd.DragEnded)
                {
                    if (!wasDrag)
                    {
                        // A slow small press: not a tap, so it is a (tiny) drag.
                        DragStarted?.Invoke(gesture.Start);
                    }
                    PressEnded?.Invoke(lastWorldPosition);
                }
            }
        }

        Vector2 ScreenToWorld(Vector2 screenPosition)
        {
            Vector3 world = worldCamera.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, -worldCamera.transform.position.z));
            return world;
        }
    }
}
