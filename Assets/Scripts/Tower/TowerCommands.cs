using System.Collections.Generic;

namespace AirTrafficControl
{
    public enum AircraftCommand
    {
        Slow,
        Fast,
        Taxi,
        Cross,
        TakeOff,
        // Tower mode (blueprint 16.1). Added at the end so the numbers of the first five stay the same.
        Land,
        GoAround,
        Hold,
        TaxiToStand,
    }

    /// <summary>One button of the option menu: the command, its runway (for Land and Taxi), its label and whether it can be pressed.</summary>
    public readonly struct CommandOption
    {
        public CommandOption(AircraftCommand command, string label, int runway = -1, bool enabled = true, bool active = false, bool half = false)
        {
            Command = command;
            Label = label;
            Runway = runway;
            Enabled = enabled;
            Active = active;
            Half = half;
        }

        public AircraftCommand Command { get; }
        public string Label { get; }

        /// <summary>Runway index for Land and Taxi, -1 otherwise (and for a pad).</summary>
        public int Runway { get; }

        /// <summary>False when the button shows but cannot be pressed now (a closed runway, no free stand).</summary>
        public bool Enabled { get; }

        /// <summary>The current setting (Slow, Fast, Hold, or the runway it is cleared to): drawn highlighted.</summary>
        public bool Active { get; }

        /// <summary>Half width: two of these share a row (Go around and Hold, Slow and Fast).</summary>
        public bool Half { get; }

        public override string ToString() => $"{Label}{(Enabled ? "" : " (off)")}{(Active ? " (on)" : "")}";
    }

    /// <summary>Where a runway stands for the buttons: shown at all, and usable now.</summary>
    public readonly struct RunwayChoice
    {
        public RunwayChoice(int index, bool shown, bool usable)
        {
            Index = index;
            Shown = shown;
            Usable = usable;
        }

        public int Index { get; }

        /// <summary>False when the runway is never offered here (Mountain Valley's cross-valley runway, a runway with no hold line at its active end).</summary>
        public bool Shown { get; }

        /// <summary>False while it is closed (City closures, the Desert sandstorm): the button shows but cannot be pressed.</summary>
        public bool Usable { get; }
    }

    /// <summary>
    /// Which option buttons a tapped aircraft shows (blueprint 15.4 and 16.1). Draw mode: Slow and Fast in the air.
    /// Tower mode, in the air: Land on each runway it may use now (or its helipad or balloon field), Go around, Hold, Slow,
    /// Fast. On the ground: Taxi to a runway from the stand, Cross at a hold line before a runway, Take off at the runway
    /// end, Taxi to stand after landing. Plain C#, so it can be unit tested.
    /// </summary>
    public static class AircraftCommands
    {
        static readonly AircraftCommand[] Air = { AircraftCommand.Slow, AircraftCommand.Fast };
        static readonly AircraftCommand[] None = new AircraftCommand[0];

        /// <summary>Draw mode: Slow and Fast for an aircraft in the air under control, nothing otherwise.</summary>
        public static AircraftCommand[] For(bool flying) => flying ? Air : None;

        public static List<CommandOption> DrawOptions(bool flying, AircraftSpeed speed)
        {
            var list = new List<CommandOption>();
            if (flying)
                AddSpeed(list, speed);
            return list;
        }

        /// <summary>
        /// Tower mode, an arrival in the air. Runway kinds get one Land button per runway shown (pressable when usable);
        /// pad kinds get Land on helipad or Land on balloon field.
        /// </summary>
        public static List<CommandOption> TowerAir(LandingZoneKind landsOn, IReadOnlyList<RunwayChoice> runways, bool hasPad,
                                                   int clearedRunway, bool clearedPad, bool holding, AircraftSpeed speed)
        {
            var list = new List<CommandOption>();
            if (landsOn == LandingZoneKind.Runway)
            {
                foreach (RunwayChoice r in runways)
                    if (r.Shown)
                        list.Add(new CommandOption(AircraftCommand.Land, $"Land on runway {RunwayNames.Letter(r.Index)}", r.Index,
                                                   r.Usable, clearedRunway == r.Index));
            }
            else if (hasPad)
            {
                string label = landsOn == LandingZoneKind.Helipad ? "Land on helipad" : "Land on balloon field";
                list.Add(new CommandOption(AircraftCommand.Land, label, -1, true, clearedPad));
            }
            list.Add(new CommandOption(AircraftCommand.GoAround, "Go around", half: true));
            list.Add(new CommandOption(AircraftCommand.Hold, "Hold", active: holding, half: true));
            AddSpeed(list, speed);
            return list;
        }

        /// <summary>
        /// Tower mode, an aircraft on the ground. <paramref name="taxiRunways"/>: runways it can be sent to (a reachable hold
        /// line at the active end); <paramref name="takeOffRunway"/>: the runway it waits at, with
        /// <paramref name="takeOffUsable"/> false while that runway is closed or the wind has moved its end.
        /// </summary>
        public static List<CommandOption> TowerGround(GroundPhase phase, IReadOnlyList<RunwayChoice> taxiRunways, int takeOffRunway,
                                                      bool takeOffUsable, bool standFree)
        {
            var list = new List<CommandOption>();
            switch (phase)
            {
                case GroundPhase.AtStand:
                    AddTaxi(list, taxiRunways, -1);
                    break;
                case GroundPhase.HoldingAtCrossing:
                    list.Add(new CommandOption(AircraftCommand.Cross, "Cross"));
                    break;
                case GroundPhase.HoldingShort:
                    if (takeOffRunway >= 0)
                        list.Add(new CommandOption(AircraftCommand.TakeOff, $"Take off from {RunwayNames.Letter(takeOffRunway)}", takeOffRunway, takeOffUsable));
                    AddTaxi(list, taxiRunways, -1); // the caller leaves out the runway it already waits at, when that end is right
                    break;
                case GroundPhase.WaitingForStand:
                    list.Add(new CommandOption(AircraftCommand.TaxiToStand, standFree ? "Taxi to stand" : "Stands full", -1, standFree));
                    break;
            }
            return list;
        }

        static void AddTaxi(List<CommandOption> list, IReadOnlyList<RunwayChoice> runways, int except)
        {
            foreach (RunwayChoice r in runways)
                if (r.Shown && r.Index != except)
                    list.Add(new CommandOption(AircraftCommand.Taxi, $"Taxi to runway {RunwayNames.Letter(r.Index)}", r.Index, r.Usable));
        }

        static void AddSpeed(List<CommandOption> list, AircraftSpeed speed)
        {
            list.Add(new CommandOption(AircraftCommand.Slow, "Slow", active: speed == AircraftSpeed.Slow, half: true));
            list.Add(new CommandOption(AircraftCommand.Fast, "Fast", active: speed == AircraftSpeed.Fast, half: true));
        }

        public static bool Same(IReadOnlyList<CommandOption> a, IReadOnlyList<CommandOption> b)
        {
            if (a == null || b == null || a.Count != b.Count)
                return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i].Command != b[i].Command || a[i].Runway != b[i].Runway || a[i].Enabled != b[i].Enabled
                    || a[i].Active != b[i].Active || a[i].Label != b[i].Label)
                    return false;
            return true;
        }

        public static string Label(AircraftCommand command)
        {
            switch (command)
            {
                case AircraftCommand.Slow: return "Slow";
                case AircraftCommand.Fast: return "Fast";
                case AircraftCommand.Taxi: return "Taxi";
                case AircraftCommand.Cross: return "Cross";
                case AircraftCommand.TakeOff: return "Take off";
                case AircraftCommand.Land: return "Land";
                case AircraftCommand.GoAround: return "Go around";
                case AircraftCommand.Hold: return "Hold";
                case AircraftCommand.TaxiToStand: return "Taxi to stand";
                default: return command.ToString();
            }
        }
    }
}
