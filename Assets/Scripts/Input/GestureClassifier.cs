using UnityEngine;

namespace AirTrafficControl
{
    public enum GestureEnd
    {
        None,
        Tap,
        DragEnded,
    }

    /// <summary>
    /// Tells a tap from a drag (blueprint 15.4). A press that is released within <see cref="TapSeconds"/> and moved
    /// less than <see cref="TapMoveLimit"/> world units is a tap; anything else becomes a drag the moment it moves that
    /// far or is held that long. Times are real seconds, so slow draw does not stretch them. Plain C#, so it can be
    /// unit tested; <see cref="InputAdapter"/> feeds it.
    /// </summary>
    public sealed class GestureClassifier
    {
        public const float TapSeconds = 0.25f;
        public const float TapMoveLimit = 0.15f;

        float startTime;

        public bool IsPressed { get; private set; }

        /// <summary>True once the press has become a drag.</summary>
        public bool IsDrag { get; private set; }

        public Vector2 Start { get; private set; }

        public void Press(Vector2 world, float time)
        {
            IsPressed = true;
            IsDrag = false;
            Start = world;
            startTime = time;
        }

        /// <summary>The finger is still down at <paramref name="world"/>. Returns true at the moment the press becomes a drag.</summary>
        public bool Hold(Vector2 world, float time)
        {
            if (!IsPressed || IsDrag)
                return false;
            if (Vector2.Distance(world, Start) >= TapMoveLimit || time - startTime >= TapSeconds)
            {
                IsDrag = true;
                return true;
            }
            return false;
        }

        /// <summary>The finger lifted at <paramref name="world"/>.</summary>
        public GestureEnd Release(Vector2 world, float time)
        {
            if (!IsPressed)
                return GestureEnd.None;
            IsPressed = false;
            if (IsDrag)
                return GestureEnd.DragEnded;
            bool quick = time - startTime < TapSeconds;
            bool still = Vector2.Distance(world, Start) < TapMoveLimit;
            return quick && still ? GestureEnd.Tap : GestureEnd.DragEnded;
        }

        public void Cancel()
        {
            IsPressed = false;
            IsDrag = false;
        }
    }
}
