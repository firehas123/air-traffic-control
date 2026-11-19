using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// One crossing of the aircraft carrier (blueprint 13.2): the ship enters from one side of the screen, sails along
    /// its sea lane and leaves on the other side. Its deck is a moving runway with a touchdown zone. Plain C#, no Unity
    /// components, so it can be unit tested.
    /// </summary>
    public sealed class CarrierPass
    {
        readonly float startX;
        readonly float direction; // +1 sails right, -1 sails left
        readonly float screenMinX;
        readonly float screenMaxX;
        readonly Runway deckAtOrigin;

        public CarrierPass(float screenMinX, float screenMaxX, float laneY, bool fromLeft, float secondsToCross,
                           float shipLength, float shipWidth, float deckLength, float deckWidth,
                           float touchdownZoneStart, float touchdownZoneLength)
        {
            this.screenMinX = screenMinX;
            this.screenMaxX = screenMaxX;
            LaneY = laneY;
            ShipLength = Mathf.Max(0.5f, shipLength);
            ShipWidth = Mathf.Max(0.2f, shipWidth);
            direction = fromLeft ? 1f : -1f;

            // From just off the screen on one side to just off it on the other.
            startX = fromLeft ? screenMinX - ShipLength / 2f : screenMaxX + ShipLength / 2f;
            float distance = (screenMaxX - screenMinX) + ShipLength;
            SecondsToCross = Mathf.Max(1f, secondsToCross);
            Speed = distance / SecondsToCross;

            deckAtOrigin = new Runway(Vector2.zero, 0f, Mathf.Min(deckLength, ShipLength), Mathf.Min(deckWidth, ShipWidth),
                                      touchdownZoneStart, touchdownZoneLength);
        }

        public float LaneY { get; }
        public float ShipLength { get; }
        public float ShipWidth { get; }
        public float SecondsToCross { get; }

        /// <summary>World units per second, along the lane.</summary>
        public float Speed { get; }

        public float Elapsed { get; private set; }

        public bool SailsRight => direction > 0f;
        public float HeadingDegrees => SailsRight ? 0f : 180f;
        public Vector2 Direction => new Vector2(direction, 0f);

        public Vector2 Centre => new Vector2(startX + direction * Speed * Elapsed, LaneY);

        /// <summary>The deck as a runway at the ship's current position. Aircraft land the way the ship is sailing.</summary>
        public Runway Deck => new Runway(Centre, HeadingDegrees, deckAtOrigin.Length, deckAtOrigin.Width,
                                         deckAtOrigin.TouchdownZoneStart, deckAtOrigin.TouchdownZoneLength);

        /// <summary>The deck in the ship's own coordinates (centred, landing direction along +x), for drawing.</summary>
        public Runway DeckLocal => deckAtOrigin;

        /// <summary>The whole ship is on screen. Fighters can connect only then (blueprint 13.2).</summary>
        public bool IsFullyOnScreen
        {
            get
            {
                float x = Centre.x;
                return x - ShipLength / 2f >= screenMinX && x + ShipLength / 2f <= screenMaxX;
            }
        }

        /// <summary>Seconds until the ship starts to leave the screen (the connection window closes). 0 once it has.</summary>
        public float SecondsUntilLeavesScreen
        {
            get
            {
                float x = Centre.x;
                float remaining = SailsRight ? (screenMaxX - ShipLength / 2f) - x : x - (screenMinX + ShipLength / 2f);
                return Mathf.Max(0f, remaining / Speed);
            }
        }

        /// <summary>The ship has sailed right off the screen.</summary>
        public bool IsFinished => Elapsed >= SecondsToCross;

        public void Tick(float deltaTime)
        {
            if (deltaTime > 0f)
                Elapsed = Mathf.Min(Elapsed + deltaTime, SecondsToCross);
        }

        /// <summary>A world point in the ship's own coordinates (x towards the bow, y to its left).</summary>
        public Vector2 ToShipLocal(Vector2 world)
        {
            Vector2 offset = world - Centre;
            return new Vector2(offset.x * direction, offset.y * direction);
        }

        /// <summary>A point given in the ship's own coordinates, where it is now.</summary>
        public Vector2 ToWorld(Vector2 shipLocal) => Centre + new Vector2(shipLocal.x * direction, shipLocal.y * direction);
    }

    /// <summary>When carriers come (blueprint 13.2: a ship every 40 to 60 s). Plain C#, so it can be unit tested.</summary>
    public sealed class CarrierSchedule
    {
        readonly float minInterval;
        readonly float maxInterval;
        readonly System.Random random;

        public CarrierSchedule(float firstShipAfterSeconds, float minSecondsBetweenShips, float maxSecondsBetweenShips, int seed)
        {
            minInterval = Mathf.Max(1f, minSecondsBetweenShips);
            maxInterval = Mathf.Max(minInterval, maxSecondsBetweenShips);
            random = new System.Random(seed);
            SecondsUntilNextShip = Mathf.Max(0f, firstShipAfterSeconds);
            NextFromLeft = true;
        }

        /// <summary>Seconds until the next ship should appear (counted from the previous ship's appearance).</summary>
        public float SecondsUntilNextShip { get; private set; }

        /// <summary>Ships alternate: the next one comes from the left when true.</summary>
        public bool NextFromLeft { get; private set; }

        /// <summary>
        /// Advances the clock. Returns true when a ship should start now. If a ship is still crossing
        /// (<paramref name="shipCrossing"/>), the next one waits until it has gone.
        /// </summary>
        public bool Tick(float deltaTime, bool shipCrossing)
        {
            if (deltaTime > 0f)
                SecondsUntilNextShip -= deltaTime;
            if (SecondsUntilNextShip > 0f || shipCrossing)
                return false;

            SecondsUntilNextShip = minInterval + (float)random.NextDouble() * (maxInterval - minInterval);
            return true;
        }

        /// <summary>Call when a ship starts: the following one comes from the other side.</summary>
        public void ShipStarted()
        {
            NextFromLeft = !NextFromLeft;
        }
    }

    public enum CarrierApproachState
    {
        /// <summary>Connected; the fighter is still flying the line the player drew.</summary>
        FlyingLine,

        /// <summary>The fighter has reached the end of its line and follows the tether to the moving deck.</summary>
        Tethered,

        /// <summary>It reached the deck.</summary>
        TouchedDown,

        /// <summary>The ship began to leave the screen first; the connection is gone (blueprint 13.2).</summary>
        Lost,
    }

    /// <summary>
    /// A fighter's connection to the carrier deck (blueprint 13.2, 14.2). The line the player drew stays exactly as
    /// drawn; only the tether, a straight segment from the end of the line to the connection point on the deck,
    /// moves with the ship. If the ship starts to leave the screen before touchdown, even at the last moment, the
    /// connection is lost. Plain C#, no Unity components, so it can be unit tested.
    /// </summary>
    public sealed class CarrierApproach
    {
        float progress; // 0 at the end of the drawn line, 1 at the deck

        public CarrierApproach(CarrierPass pass, Vector2 lineEnd)
        {
            Pass = pass;
            LineEnd = lineEnd;
            DeckLocalPoint = pass.ToShipLocal(lineEnd);
            State = CarrierApproachState.FlyingLine;
        }

        public CarrierPass Pass { get; }

        /// <summary>Where the drawn line ends. Fixed: it does not move with the ship.</summary>
        public Vector2 LineEnd { get; }

        /// <summary>The connection point in the ship's own coordinates: where the line touched the deck.</summary>
        public Vector2 DeckLocalPoint { get; }

        public CarrierApproachState State { get; private set; }

        public bool IsConnected => State == CarrierApproachState.FlyingLine || State == CarrierApproachState.Tethered;

        /// <summary>The connection point on the deck now.</summary>
        public Vector2 DeckPoint => Pass.ToWorld(DeckLocalPoint);

        /// <summary>The fighter's position along the tether (valid while tethered).</summary>
        public Vector2 TetherPosition => Vector2.Lerp(LineEnd, DeckPoint, progress);

        /// <summary>Loses the connection if the ship has begun to leave the screen. Returns true if it was lost now.</summary>
        public bool CheckShip()
        {
            if (!IsConnected || Pass.IsFullyOnScreen)
                return false;
            State = CarrierApproachState.Lost;
            return true;
        }

        /// <summary>The fighter has reached the end of its drawn line and starts along the tether.</summary>
        public void StartTether()
        {
            if (State == CarrierApproachState.FlyingLine)
                State = CarrierApproachState.Tethered;
        }

        /// <summary>
        /// Moves the fighter <paramref name="distance"/> along the tether, which stretches as the ship sails on.
        /// Returns true when it reaches the deck.
        /// </summary>
        public bool StepTether(float distance)
        {
            if (State != CarrierApproachState.Tethered)
                return false;

            float length = Vector2.Distance(LineEnd, DeckPoint);
            progress = length > 1e-4f ? Mathf.Min(1f, progress + distance / length) : 1f;
            if (progress >= 1f)
            {
                State = CarrierApproachState.TouchedDown;
                return true;
            }
            return false;
        }

        public float TetherHeadingDegrees
        {
            get
            {
                Vector2 d = DeckPoint - LineEnd;
                return d.sqrMagnitude > 0f ? Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg : Pass.HeadingDegrees;
            }
        }
    }

    public static class CarrierRules
    {
        /// <summary>
        /// A fighter's line connects to the deck when the whole ship is on screen and the line ends in the deck's
        /// touchdown zone, overlapping it like a runway (any direction, <see cref="LandingRules.ConnectInset"/>).
        /// </summary>
        public static bool CanConnect(CarrierPass pass, IReadOnlyList<Vector2> line)
        {
            if (pass == null || !pass.IsFullyOnScreen || line == null || line.Count < 2)
                return false;
            return LandingRules.CanLand(pass.Deck, line);
        }
    }
}
