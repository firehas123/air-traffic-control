using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// A runway as a rectangle with a landing direction. Aircraft land travelling along
    /// <see cref="LandingHeadingDegrees"/>. They touch down in the touchdown zone, the highlighted area
    /// with the arrows near the <see cref="Threshold"/>, and roll towards the other end. Plain C#, no Unity components, so it can be unit tested.
    /// </summary>
    public readonly struct Runway
    {
        public const float DefaultTouchdownZoneStart = 0.1f;
        public const float DefaultTouchdownZoneLength = 0.7f;

        public Runway(Vector2 centre, float landingHeadingDegrees, float length, float width,
                      float touchdownZoneStart = DefaultTouchdownZoneStart,
                      float touchdownZoneLength = DefaultTouchdownZoneLength)
        {
            Centre = centre;
            LandingHeadingDegrees = Mathf.Repeat(landingHeadingDegrees, 360f);
            Length = Mathf.Max(0.01f, length);
            Width = Mathf.Max(0.01f, width);
            TouchdownZoneStart = Mathf.Clamp(touchdownZoneStart, 0f, Length - 0.01f);
            TouchdownZoneLength = Mathf.Clamp(touchdownZoneLength, 0.01f, Length - TouchdownZoneStart);
        }

        public Vector2 Centre { get; }
        public float LandingHeadingDegrees { get; }
        public float Length { get; }
        public float Width { get; }

        /// <summary>Distance from the threshold to where the touchdown zone starts.</summary>
        public float TouchdownZoneStart { get; }

        /// <summary>Length of the touchdown zone along the landing direction.</summary>
        public float TouchdownZoneLength { get; }

        /// <summary>Unit vector of the landing direction.</summary>
        public Vector2 Direction
        {
            get
            {
                float radians = LandingHeadingDegrees * Mathf.Deg2Rad;
                return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            }
        }

        /// <summary>The touchdown end of the runway, at the start of the landing direction.</summary>
        public Vector2 Threshold => Centre - Direction * (Length / 2f);

        /// <summary>Same runway, landing the opposite way. Used later by the Island wind rule (C1).</summary>
        public Runway Reversed() => new Runway(Centre, LandingHeadingDegrees + 180f, Length, Width, TouchdownZoneStart, TouchdownZoneLength);

        /// <summary>
        /// Position in runway coordinates: x along the landing direction, y to its left,
        /// both measured from the centre.
        /// </summary>
        public Vector2 ToLocal(Vector2 world)
        {
            Vector2 offset = world - Centre;
            Vector2 along = Direction;
            var left = new Vector2(-along.y, along.x);
            return new Vector2(Vector2.Dot(offset, along), Vector2.Dot(offset, left));
        }

        /// <summary>True if the point is on the runway, allowing <paramref name="margin"/> extra on every side.</summary>
        public bool Contains(Vector2 world, float margin = 0f)
        {
            Vector2 local = ToLocal(world);
            return Mathf.Abs(local.x) <= Length / 2f + margin && Mathf.Abs(local.y) <= Width / 2f + margin;
        }

        /// <summary>
        /// True if the point is in the touchdown zone. The zone starts inside the runway, so a point before
        /// the runway never counts. A positive <paramref name="inset"/> shrinks the zone by that much on every
        /// side, so the point must be well inside it rather than on its edge.
        /// </summary>
        public bool InTouchdownZone(Vector2 world, float inset = 0f)
        {
            Vector2 local = ToLocal(world);
            float intoZone = local.x + Length / 2f - TouchdownZoneStart;
            return intoZone >= inset && intoZone <= TouchdownZoneLength - inset
                   && Mathf.Abs(local.y) <= Width / 2f - inset;
        }
    }
}
