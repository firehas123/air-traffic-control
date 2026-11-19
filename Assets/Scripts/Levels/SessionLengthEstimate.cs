using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// A rough estimate of how long a level takes to clear at 1x speed, from its data (C3: levels last 60 to 120 s).
    /// It is a check on the level's numbers, not a replacement for timing real play on the iPad: the result screen
    /// shows the real time. Plain C#, so it can be unit tested.
    ///
    /// Each landing takes the flight from a spawn point to the kind's landing zone (straight-line distance times a
    /// detour factor, because drawn lines curve around traffic), plus the player's time to notice and draw, the
    /// touchdown roll, and the wait before the next aircraft appears. With several aircraft in the air these overlap.
    /// </summary>
    public static class SessionLengthEstimate
    {
        public const float DetourFactor = 1.25f;
        public const float PlayerSecondsPerLine = 2.5f;
        public const float TouchdownSeconds = 1f;
        public const float RespawnSeconds = 1f;

        public static float Seconds(LevelData level, float airlinerSpeed)
        {
            List<AircraftType> kinds = level.aircraftTypes;
            if (kinds == null || kinds.Count == 0 || level.spawnPoints.Count == 0 || level.landingsToClear <= 0)
                return 0f;

            float total = 0f;
            int samples = 0;
            foreach (AircraftType kind in kinds)
            {
                if (kind == null)
                    continue;
                Vector2? zone = ZoneCentre(level, kind.landsOn);
                if (!zone.HasValue)
                    continue;
                foreach (SpawnPoint spawn in level.spawnPoints)
                {
                    float flight = Vector2.Distance(spawn.position, zone.Value) * DetourFactor / (airlinerSpeed * kind.speedMultiplier);
                    total += flight + PlayerSecondsPerLine + TouchdownSeconds + RespawnSeconds;
                    samples++;
                }
            }
            if (samples == 0)
                return 0f;

            float secondsPerLanding = total / samples;
            int inTheAir = Mathf.Max(1, level.aircraftInAir);
            return secondsPerLanding * level.landingsToClear / inTheAir;
        }

        static Vector2? ZoneCentre(LevelData level, LandingZoneKind kind)
        {
            if (kind == LandingZoneKind.Runway)
            {
                if (level.runways.Count == 0)
                    return null;
                Runway runway = level.runways[0].ToRunway();
                return runway.Threshold + runway.Direction * (runway.TouchdownZoneStart + runway.TouchdownZoneLength / 2f);
            }
            foreach (LandingPadDefinition pad in level.landingPads)
                if (pad.kind == kind)
                    return pad.centre;
            return null;
        }
    }
}
