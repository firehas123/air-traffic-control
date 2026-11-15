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

        public event Action<Vector2> PressStarted;
        public event Action<Vector2> Dragged;
        public event Action<Vector2> PressEnded;

        /// <summary>True while a finger (or mouse button) is held down. Used later for C4 (slow time while drawing).</summary>
        public bool IsPressing { get; private set; }

        public void Initialise(Camera camera)
        {
            worldCamera = camera;
        }

        void Update()
        {
            // Pointer.current is whichever pointer was used last: the touchscreen on a tablet or phone,
            // the mouse in the Unity editor. For a touchscreen it reports the primary (first) finger.
            Pointer pointer = Pointer.current;
            if (pointer == null || worldCamera == null)
                return;

            bool pressed = pointer.press.isPressed;

            if (pressed)
            {
                lastWorldPosition = ScreenToWorld(pointer.position.ReadValue());
                if (!IsPressing)
                {
                    IsPressing = true;
                    PressStarted?.Invoke(lastWorldPosition);
                }
                else
                {
                    Dragged?.Invoke(lastWorldPosition);
                }
            }
            else if (IsPressing)
            {
                // A released touch may not report a fresh position, so reuse the last one seen.
                IsPressing = false;
                PressEnded?.Invoke(lastWorldPosition);
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
