namespace AirTrafficControl
{
    /// <summary>The short lines of first-time help (M2 step 5). One at a time, never a wall of text.</summary>
    public enum HelpHint
    {
        None,
        DrawToLandingZone,
        ClearedToLand,
        DangerRing,
        WindChanging,
        CarrierDeck,
        SlowFast,
        Taxi,
        TakeOff,
        Cross,

        // Map rules of Draw mode (blueprint 16.2), each explained once. Added at the end so saved flags keep their meaning.
        RunwayClosing,
        LowFuel,
        Sandstorm,
        Mountains,
        IcyRunway,

        // Tower mode (blueprint 16.1, phase 3). Added at the end so saved flags keep their meaning.
        TapForOrders,
        GoAround,
        Hold,
        TaxiToStand,
    }

    /// <summary>
    /// First-time help that teaches by doing (blueprint 13.3 step 5). The first hint shows until the player has done
    /// it: draw a line from an aircraft to where it lands. The others appear once, the first time their situation
    /// happens, and fade after a few seconds. What has been shown is remembered (saved on the device, offline), so
    /// each hint appears once per install unless the player asks to see them again.
    /// Plain C#, no Unity components, so it can be unit tested.
    /// </summary>
    public sealed class FirstTimeHelp
    {
        public const float ShortHintSeconds = 2.5f;
        public const float LongHintSeconds = 4f;

        float hintTimeLeft;

        public FirstTimeHelp(int alreadySeen = 0, bool tower = false)
        {
            Seen = alreadySeen;
            Tower = tower;
            FirstLesson = tower ? HelpHint.TapForOrders : HelpHint.DrawToLandingZone;
        }

        /// <summary>Tower mode: the first lesson is to tap a plane and give it Land, not to draw (blueprint 16.1).</summary>
        public bool Tower { get; }

        /// <summary>The lesson that stays up until it is done: drawing a line (Draw mode) or a Land order (Tower mode).</summary>
        public HelpHint FirstLesson { get; }

        /// <summary>Bit flags of the hints already shown, for saving.</summary>
        public int Seen { get; private set; }

        public HelpHint Current { get; private set; }

        /// <summary>True when the hint changed or one ended, so the saved flags should be written.</summary>
        public bool SeenChanged { get; private set; }

        public bool HasSeen(HelpHint hint) => (Seen & Bit(hint)) != 0;

        static int Bit(HelpHint hint) => 1 << (int)hint;

        /// <summary>Forgets everything shown, so the help plays again from the start.</summary>
        public void Reset()
        {
            Seen = 0;
            Current = HelpHint.None;
            hintTimeLeft = 0f;
            SeenChanged = true;
        }

        /// <summary>
        /// Advances the clock (real seconds, so slow draw does not stretch the hints). Shows the drawing hint while
        /// the player has never connected a line and an aircraft is in the air.
        /// </summary>
        public void Tick(float realSeconds, bool aircraftInTheAir)
        {
            SeenChanged = false;
            if (Current != HelpHint.None && Current != FirstLesson)
            {
                hintTimeLeft -= realSeconds;
                if (hintTimeLeft <= 0f)
                    Current = HelpHint.None;
            }

            if (Current == HelpHint.None && !HasSeen(FirstLesson) && aircraftInTheAir)
                Current = FirstLesson;
            if (Current == FirstLesson && !aircraftInTheAir)
                Current = HelpHint.None;
        }

        /// <summary>The player connected a line to a landing zone: the first lesson is learnt.</summary>
        public void LineConnected()
        {
            if (HasSeen(HelpHint.DrawToLandingZone))
                return;
            MarkSeen(HelpHint.DrawToLandingZone);
            Show(HelpHint.ClearedToLand, ShortHintSeconds);
        }

        /// <summary>Tower mode: the player gave a Land order: the first lesson is learnt.</summary>
        public void LandOrderGiven()
        {
            if (HasSeen(HelpHint.TapForOrders))
                return;
            MarkSeen(HelpHint.TapForOrders);
            Show(HelpHint.ClearedToLand, ShortHintSeconds);
        }

        /// <summary>Tower mode (Island level 1): an arrival comes in to a busy runway, or went around by itself.</summary>
        public void GoAroundUseful() => ShowOnce(HelpHint.GoAround, LongHintSeconds);

        /// <summary>Tower mode (Island level 2): several arrivals wait for orders at once.</summary>
        public void HoldUseful() => ShowOnce(HelpHint.Hold, LongHintSeconds);

        /// <summary>Tower mode: an arrival has left the runway and waits for Taxi to stand.</summary>
        public void ArrivalWaitingForStand() => ShowOnce(HelpHint.TaxiToStand, LongHintSeconds);

        public void WarningStarted() => ShowOnce(HelpHint.DangerRing, LongHintSeconds);

        public void WindChangeComing() => ShowOnce(HelpHint.WindChanging, LongHintSeconds);

        public void CarrierReachable() => ShowOnce(HelpHint.CarrierDeck, LongHintSeconds);

        /// <summary>Slow / Fast buttons opened for the first time (blueprint 15.4).</summary>
        public void SpeedButtonsShown() => ShowOnce(HelpHint.SlowFast, LongHintSeconds);

        /// <summary>A departure is waiting at a gate for the first time.</summary>
        public void DepartureAtGate() => ShowOnce(HelpHint.Taxi, LongHintSeconds);

        /// <summary>A departure is waiting at the hold line for the first time.</summary>
        public void DepartureAtHoldLine() => ShowOnce(HelpHint.TakeOff, LongHintSeconds);

        /// <summary>A departure is waiting at a crossing point for the first time.</summary>
        public void DepartureAtCrossing() => ShowOnce(HelpHint.Cross, LongHintSeconds);

        /// <summary>City: a runway is about to close for the first time.</summary>
        public void RunwayClosingSoon() => ShowOnce(HelpHint.RunwayClosing, LongHintSeconds);

        /// <summary>Desert: the first low-fuel aircraft is in the air.</summary>
        public void LowFuelAircraft() => ShowOnce(HelpHint.LowFuel, LongHintSeconds);

        /// <summary>Desert: the first sandstorm is closing in on a runway.</summary>
        public void SandstormComing() => ShowOnce(HelpHint.Sandstorm, LongHintSeconds);

        /// <summary>Mountain Valley: the first level with no-fly mountains.</summary>
        public void MountainsAhead() => ShowOnce(HelpHint.Mountains, LongHintSeconds);

        /// <summary>Arctic: the first level with icy runways.</summary>
        public void IcyRunways() => ShowOnce(HelpHint.IcyRunway, LongHintSeconds);

        // A later hint waits until the drawing lesson is done, so the first thing a new player learns is to draw.
        void ShowOnce(HelpHint hint, float seconds)
        {
            if (HasSeen(hint) || !HasSeen(FirstLesson))
                return;
            if (Current != HelpHint.None && Current != HelpHint.ClearedToLand)
                return; // do not cut another hint short
            Show(hint, seconds);
        }

        void Show(HelpHint hint, float seconds)
        {
            Current = hint;
            hintTimeLeft = seconds;
            MarkSeen(hint);
        }

        void MarkSeen(HelpHint hint)
        {
            Seen |= Bit(hint);
            SeenChanged = true;
        }

        /// <summary>The hint's line in the current mode.</summary>
        public string TextNow(HelpHint hint) => Text(hint, Tower);

        public static string Text(HelpHint hint, bool tower)
        {
            if (tower)
            {
                switch (hint)
                {
                    case HelpHint.DangerRing: return "Red ring: Go around, Hold or Slow to keep them apart";
                    case HelpHint.WindChanging: return "Wind is changing: runways switch to their other end";
                    case HelpHint.Taxi: return "Tap the plane at the stand, then Taxi";
                    case HelpHint.RunwayClosing: return "Runway closing: its Land and Take off buttons go off";
                    case HelpHint.Mountains: return "Mountains: planes can land only along the valley";
                }
            }
            return Text(hint);
        }

        public static string Text(HelpHint hint)
        {
            switch (hint)
            {
                case HelpHint.DrawToLandingZone: return "Drag from a plane to where it lands";
                case HelpHint.ClearedToLand: return "Green ring: it will land";
                case HelpHint.DangerRing: return "Red ring: draw a new line to keep them apart";
                case HelpHint.WindChanging: return "Wind is changing: the arrows move to the other end";
                case HelpHint.CarrierDeck: return "Fighter jets can land on the carrier deck";
                case HelpHint.SlowFast: return "Slow or Fast; tap it again for normal speed";
                case HelpHint.Taxi: return "Tap the plane at the gate, then Taxi";
                case HelpHint.TakeOff: return "Runway clear? Tap the plane, then Take off";
                case HelpHint.Cross: return "Runway clear? Tap the plane, then Cross";
                case HelpHint.RunwayClosing: return "A runway is closing: lines cannot end on the X";
                case HelpHint.LowFuel: return "Low fuel: land it before its timer runs out";
                case HelpHint.Sandstorm: return "Sandstorm: it closes every runway it covers";
                case HelpHint.Mountains: return "Lines cannot cross the mountains";
                case HelpHint.IcyRunway: return "Icy runways: land on Slow; Fast slides and blocks it";
                case HelpHint.TapForOrders: return "Tap a plane, then Land on a runway";
                case HelpHint.GoAround: return "Runway busy? Tap the plane, then Go around";
                case HelpHint.Hold: return "Too busy? Tap one, then Hold: it circles until called";
                case HelpHint.TaxiToStand: return "Landed: tap it, then Taxi to stand";
                default: return null;
            }
        }
    }
}
