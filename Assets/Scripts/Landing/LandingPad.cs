using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// A round landing zone: a helipad (circle marked H) or a balloon field (grass circle), blueprint 13.1.
    /// A line lands when it ends inside the circle, from any direction. Wind does not affect pads (14.3).
    /// Plain C#, no Unity components, so it can be unit tested.
    /// </summary>
    public readonly struct LandingPad
    {
        public LandingPad(LandingZoneKind kind, Vector2 centre, float radius)
        {
            Kind = kind;
            Centre = centre;
            Radius = Mathf.Max(0.05f, radius);
        }

        public LandingZoneKind Kind { get; }
        public Vector2 Centre { get; }
        public float Radius { get; }

        /// <summary>True if the point is inside the pad, at least <paramref name="inset"/> in from its edge.</summary>
        public bool Contains(Vector2 point, float inset = 0f) => Vector2.Distance(point, Centre) <= Radius - inset;
    }

    /// <summary>Where and how an aircraft touches down: on a runway (it rolls out) or on a pad (it settles onto it).</summary>
    public readonly struct Touchdown
    {
        Touchdown(LandingZoneKind kind, Runway runway, LandingPad pad)
        {
            Kind = kind;
            Runway = runway;
            Pad = pad;
        }

        public LandingZoneKind Kind { get; }
        public Runway Runway { get; }
        public LandingPad Pad { get; }

        public bool IsRunway => Kind == LandingZoneKind.Runway;

        public static Touchdown OnRunway(Runway runway) => new Touchdown(LandingZoneKind.Runway, runway, default);
        public static Touchdown OnPad(LandingPad pad) => new Touchdown(pad.Kind, default, pad);
    }
}
