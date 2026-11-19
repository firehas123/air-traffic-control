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

        public FirstTimeHelp(int alreadySeen = 0)
        {
            Seen = alreadySeen;
        }

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
            if (Current != HelpHint.None && Current != HelpHint.DrawToLandingZone)
            {
                hintTimeLeft -= realSeconds;
                if (hintTimeLeft <= 0f)
                    Current = HelpHint.None;
            }

            if (Current == HelpHint.None && !HasSeen(HelpHint.DrawToLandingZone) && aircraftInTheAir)
                Current = HelpHint.DrawToLandingZone;
            if (Current == HelpHint.DrawToLandingZone && !aircraftInTheAir)
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

        // A later hint waits until the drawing lesson is done, so the first thing a new player learns is to draw.
        void ShowOnce(HelpHint hint, float seconds)
        {
            if (HasSeen(hint) || !HasSeen(HelpHint.DrawToLandingZone))
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
                default: return null;
            }
        }
    }
}
