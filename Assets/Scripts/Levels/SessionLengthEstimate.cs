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

        /// <summary>
        /// Each departure (blueprint 15.4) adds the player's taps (Taxi, Take off, sometimes Cross) and its time on the
        /// runway, which other traffic has to wait for. Its taxiing overlaps the arrivals.
        /// </summary>
        public const float PlayerSecondsPerCommand = 1.5f;
        public const float CommandsPerDeparture = 2.5f;
        public const float TakeOffSeconds = 3f;

        /// <summary>
        /// Map rules that make landing slower (blueprint 16.2): lines go round the mountains, and closed runways and the
        /// sandstorm send aircraft to a farther runway or make them wait. Rough factors chosen by Claude, 26 Nov 2026.
        /// </summary>
        public const float NoFlyFactor = 1.15f;
        public const float ClosureFactor = 1.08f;
        public const float SandstormFactor = 1.08f;

        public static float Seconds(LevelData level, float airlinerSpeed)
        {
            List<AircraftType> kinds = level.aircraftTypes;
            float departures = Mathf.Max(0, level.departuresToClear) * (PlayerSecondsPerCommand * CommandsPerDeparture + TakeOffSeconds);
            if (kinds == null || kinds.Count == 0 || level.spawnPoints.Count == 0 || level.landingsToClear <= 0)
                return departures;

            float total = 0f;
            int samples = 0;
            foreach (AircraftType kind in kinds)
            {
                if (kind == null)
                    continue;
                foreach (SpawnPoint spawn in level.spawnPoints)
                {
                    float? distance = DistanceToNearestZone(level, kind.landsOn, spawn.position);
                    if (!distance.HasValue)
                        break;
                    float flight = distance.Value * DetourFactor * RuleFactor(level) / (airlinerSpeed * kind.speedMultiplier);
                    total += flight + PlayerSecondsPerLine + TouchdownSeconds + RespawnSeconds;
                    samples++;
                }
            }
            if (samples == 0)
                return departures;

            float secondsPerLanding = total / samples;
            int inTheAir = Mathf.Max(1, level.aircraftInAir);
            return secondsPerLanding * level.landingsToClear / inTheAir + departures;
        }

        static float RuleFactor(LevelData level)
        {
            float factor = 1f;
            if (level.rules != null)
            {
                if (level.rules.noFly.enabled) factor *= NoFlyFactor;
                if (level.rules.closures.enabled) factor *= ClosureFactor;
                if (level.rules.sandstorm.enabled) factor *= SandstormFactor;
            }
            return factor;
        }

        /// <summary>
        /// Straight-line distance from a spawn point to the nearest zone the kind lands on: every runway, from either end
        /// (the wind may reverse it, and the player picks the closest), or the nearest matching pad. Null if there is none.
        /// </summary>
        public static float? DistanceToNearestZone(LevelData level, LandingZoneKind kind, Vector2 from)
        {
            float best = float.MaxValue;
            if (kind == LandingZoneKind.Runway)
            {
                foreach (RunwayDefinition definition in level.runways)
                    foreach (Runway runway in new[] { definition.ToRunway(), definition.ToRunway().Reversed() })
                    {
                        Vector2 zone = runway.Threshold + runway.Direction * (runway.TouchdownZoneStart + runway.TouchdownZoneLength / 2f);
                        best = Mathf.Min(best, Vector2.Distance(from, zone));
                    }
            }
            else
            {
                foreach (LandingPadDefinition pad in level.landingPads)
                    if (pad.kind == kind)
                        best = Mathf.Min(best, Vector2.Distance(from, pad.centre));
            }
            return best < float.MaxValue ? best : (float?)null;
        }
    }
}
