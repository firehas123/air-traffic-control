using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>Placeholder shape drawn for a kind of aircraft (no art yet).</summary>
    public enum AircraftSilhouette
    {
        Airliner,
        JumboJet,
        FighterJet,
    }

    /// <summary>
    /// Which kind of landing zone an aircraft kind uses. Only runways exist so far (M2 step 1);
    /// helipad, balloon field and carrier come in M2 steps 2 and 3.
    /// </summary>
    public enum LandingZoneKind
    {
        Runway,
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

        [Tooltip("Drawn size relative to the airliner. Airliner 1.0.")]
        [Min(0.1f)] public float size = 1f;

        [Tooltip("Collision radius in world units (blueprint 14.1). Two aircraft collide when their centres are " +
                 "closer than their two radii added together. Airliner 0.25, so two airliners collide at 0.5. " +
                 "Warning distances grow with the radii too.")]
        [Min(0.05f)] public float collisionRadius = AirlinerCollisionRadius;

        [Tooltip("Body colour. Never green, yellow or red.")]
        public Color color = new Color(0.96f, 0.96f, 0.96f);

        public AircraftSilhouette silhouette = AircraftSilhouette.Airliner;

        public LandingZoneKind landsOn = LandingZoneKind.Runway;

        /// <summary>Fills in the agreed starting values for a kind (blueprint 13.1 and 14.1).</summary>
        public void SetDefaults(AircraftSilhouette kind)
        {
            silhouette = kind;
            landsOn = LandingZoneKind.Runway;
            switch (kind)
            {
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
                    break;
                default:
                    displayName = "Airliner";
                    speedMultiplier = 1f;
                    size = 1f;
                    collisionRadius = AirlinerCollisionRadius;
                    color = new Color(0.96f, 0.96f, 0.96f);          // white
                    break;
            }
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
