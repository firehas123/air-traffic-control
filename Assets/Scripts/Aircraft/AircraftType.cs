using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>Placeholder shape drawn for a kind of aircraft (no art yet).</summary>
    public enum AircraftSilhouette
    {
        Airliner,
        JumboJet,
        FighterJet,
        Helicopter,
        Drone,
        HotAirBalloon,
    }

    /// <summary>Speed setting from the Slow / Fast buttons (blueprint 15.4).</summary>
    public enum AircraftSpeed
    {
        Normal,
        Slow,
        Fast,
    }

    /// <summary>The Slow / Fast rules (blueprint 15.4). Plain C#, so it can be unit tested.</summary>
    public static class SpeedRules
    {
        /// <summary>Pressing a speed button: the requested setting, or back to normal if it is already active.</summary>
        public static AircraftSpeed Toggle(AircraftSpeed current, AircraftSpeed requested) =>
            current == requested ? AircraftSpeed.Normal : requested;

        /// <summary>The factor on the kind's speed for a setting.</summary>
        public static float Factor(AircraftType type, AircraftSpeed setting) =>
            setting == AircraftSpeed.Slow ? type.slowFactor : setting == AircraftSpeed.Fast ? type.fastFactor : 1f;
    }

    /// <summary>Which kind of landing zone an aircraft kind uses (blueprint 13.1). A line lands only on a matching zone.</summary>
    public enum LandingZoneKind
    {
        Runway,
        Helipad,
        BalloonField,
    }

    /// <summary>
    /// One kind of aircraft as data (blueprint 13.1, AGREED). Edit the assets in Assets/Aircraft in the Inspector.
    /// Kinds never use green, yellow or red: those colours mean cleared, needs a line, and danger.
    /// </summary>
    [CreateAssetMenu(menuName = "Air Traffic Control/Aircraft Type", fileName = "AircraftType")]
    public sealed class AircraftType : ScriptableObject
    {
        public const float AirlinerCollisionRadius = 0.25f;

        public string displayName = "Airliner";

        [Tooltip("Speed relative to the airliner at 1x game speed (0.6 world units per second). Airliner 1.0.")]
        [Min(0.05f)] public float speedMultiplier = 1f;

        [Tooltip("Slow button (blueprint 15.4): this times the kind's speed.")]
        [Min(0.1f)] public float slowFactor = 0.6f;

        [Tooltip("Fast button (blueprint 15.4): this times the kind's speed. Fast lands sooner but rolls further.")]
        [Min(0.1f)] public float fastFactor = 1.4f;

        [Tooltip("Drawn size relative to the airliner. Airliner 1.0.")]
        [Min(0.1f)] public float size = 1f;

        [Tooltip("Collision radius in world units (blueprint 14.1). Two aircraft collide when their centres are " +
                 "closer than their two radii added together. Airliner 0.25, so two airliners collide at 0.5. " +
                 "Warning distances grow with the radii too.")]
        [Min(0.05f)] public float collisionRadius = AirlinerCollisionRadius;

        [Tooltip("Body colour. Never green, yellow or red.")]
        public Color color = new Color(0.96f, 0.96f, 0.96f);

        [Tooltip("Second colour, used by shapes with two colours (the balloon's stripes).")]
        public Color accentColor = new Color(0.96f, 0.96f, 0.96f);

        public AircraftSilhouette silhouette = AircraftSilhouette.Airliner;

        public LandingZoneKind landsOn = LandingZoneKind.Runway;

        [Tooltip("Can also land on the carrier ship's deck when a level has one (blueprint 13.2: fighter jets only).")]
        public bool landsOnCarrier;

        [Tooltip("Keep the shape upright instead of turning it to the heading (the balloon drifts, it does not steer nose first).")]
        public bool keepsUpright;

        /// <summary>Fills in the agreed starting values for a kind (blueprint 13.1 and 14.1).</summary>
        public void SetDefaults(AircraftSilhouette kind)
        {
            silhouette = kind;
            landsOn = LandingZoneKind.Runway;
            landsOnCarrier = false;
            keepsUpright = false;
            switch (kind)
            {
                case AircraftSilhouette.Helicopter:
                    displayName = "Helicopter";
                    speedMultiplier = 0.6f;
                    size = 1f;
                    collisionRadius = AirlinerCollisionRadius;
                    color = new Color(0.95f, 0.5f, 0.12f);           // orange
                    landsOn = LandingZoneKind.Helipad;
                    break;
                case AircraftSilhouette.Drone:
                    displayName = "Drone";
                    speedMultiplier = 0.8f;
                    size = 0.6f;
                    collisionRadius = 0.18f;
                    color = new Color(0.6f, 0.36f, 0.86f);           // purple
                    landsOn = LandingZoneKind.Helipad;
                    break;
                case AircraftSilhouette.HotAirBalloon:
                    displayName = "Hot air balloon";
                    speedMultiplier = 0.35f;
                    size = 1f;
                    collisionRadius = 0.28f;                          // round, a little wider than an airliner
                    color = new Color(1f, 0.52f, 0.76f);             // pink
                    accentColor = new Color(0.55f, 0.3f, 0.82f);     // purple stripes
                    landsOn = LandingZoneKind.BalloonField;
                    keepsUpright = true;
                    break;
                case AircraftSilhouette.JumboJet:
                    displayName = "Jumbo jet";
                    speedMultiplier = 0.85f;
                    size = 1.4f;
                    collisionRadius = AirlinerCollisionRadius * 1.4f; // 0.35
                    color = new Color(0.52f, 0.78f, 0.98f);          // sky blue
                    break;
                case AircraftSilhouette.FighterJet:
                    displayName = "Fighter jet";
                    speedMultiplier = 1.6f;
                    size = 0.75f;
                    collisionRadius = 0.18f;
                    color = new Color(0.36f, 0.38f, 0.42f);          // dark grey
                    landsOnCarrier = true;
                    break;
                default:
                    displayName = "Airliner";
                    speedMultiplier = 1f;
                    size = 1f;
                    collisionRadius = AirlinerCollisionRadius;
                    color = new Color(0.96f, 0.96f, 0.96f);          // white
                    break;
            }
            if (kind != AircraftSilhouette.HotAirBalloon)
                accentColor = color;
        }

        /// <summary>An airliner made in code, used if a level lists no kinds.</summary>
        public static AircraftType CreateDefaultAirliner()
        {
            var type = CreateInstance<AircraftType>();
            type.name = "Airliner (default)";
            type.SetDefaults(AircraftSilhouette.Airliner);
            return type;
        }
    }
}
