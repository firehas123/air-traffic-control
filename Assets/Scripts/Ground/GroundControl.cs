using System;
using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// Hybrid controls (blueprint 15.4), the game side: a tap on an aircraft opens its option buttons (Slow / Fast in
    /// the air; Taxi, Cross or Take off on the ground), and the chosen command is given to that aircraft. Also keeps
    /// taxiing aircraft queued behind each other every frame. Input stays in <see cref="InputAdapter"/> and the buttons
    /// in <see cref="CommandMenu"/>.
    /// </summary>
    public sealed class GroundControl : MonoBehaviour
    {
        /// <summary>Generous tap target around an aircraft, the same as for drawing.</summary>
        public const float TapRadius = 0.8f;

        InputAdapter input;
        CommandMenu menu;
        float queueSpacing = 0.75f;
        readonly List<Vector2> others = new List<Vector2>();
        AircraftCommand[] openCommands = new AircraftCommand[0];

        /// <summary>Raised when buttons open for an aircraft, with the commands shown (for first-time help).</summary>
        public event Action<AircraftController, AircraftCommand[]> MenuOpened;

        /// <summary>Raised when a command has been given.</summary>
        public event Action<AircraftController, AircraftCommand> CommandGiven;

        public bool IsMenuOpen => menu != null && menu.IsOpen;

        public void Initialise(InputAdapter inputAdapter, CommandMenu commandMenu, GroundLayout ground)
        {
            input = inputAdapter;
            menu = commandMenu;
            if (ground != null)
                queueSpacing = ground.queueSpacing;
            input.Tapped += OnTapped;
            input.DragStarted += OnDragStarted;
            menu.CommandChosen += OnCommandChosen;
        }

        void OnDestroy()
        {
            if (input != null)
            {
                input.Tapped -= OnTapped;
                input.DragStarted -= OnDragStarted;
            }
            if (menu != null)
                menu.CommandChosen -= OnCommandChosen;
        }

        public static AircraftCommand[] CommandsFor(AircraftController aircraft) =>
            AircraftCommands.For(aircraft.IsControllableInAir && !aircraft.IsLanding,
                                 aircraft.Ground != null ? aircraft.Ground.Phase : (GroundPhase?)null);

        void OnTapped(Vector2 world)
        {
            AircraftController aircraft = AircraftController.FindNearestForTap(world, TapRadius);
            if (aircraft == null)
            {
                menu.Close(); // a tap elsewhere closes the buttons
                return;
            }
            AircraftCommand[] commands = CommandsFor(aircraft);
            if (commands.Length == 0)
            {
                menu.Close();
                return;
            }
            openCommands = commands;
            menu.Open(aircraft, commands, aircraft.SpeedSetting);
            MenuOpened?.Invoke(aircraft, commands);
        }

        // Drawing a line somewhere also closes any open buttons.
        void OnDragStarted(Vector2 world) => menu.Close();

        void OnCommandChosen(AircraftController aircraft, AircraftCommand command)
        {
            if (aircraft == null)
                return;
            bool done;
            switch (command)
            {
                case AircraftCommand.Slow:
                    aircraft.ToggleSpeed(AircraftSpeed.Slow);
                    done = true;
                    break;
                case AircraftCommand.Fast:
                    aircraft.ToggleSpeed(AircraftSpeed.Fast);
                    done = true;
                    break;
                default:
                    done = aircraft.GiveGroundCommand(command);
                    break;
            }
            if (done)
                CommandGiven?.Invoke(aircraft, command);
        }

        void Update()
        {
            // Close the buttons if the aircraft no longer offers them (it landed, crashed, or moved on).
            if (menu.IsOpen)
            {
                AircraftController target = menu.Target;
                if (target == null || !SameCommands(CommandsFor(target), openCommands))
                    menu.Close();
            }

            // Queueing: a taxiing aircraft waits while another ground aircraft is close in front of it.
            IReadOnlyList<AircraftController> all = AircraftController.Active;
            foreach (AircraftController aircraft in all)
            {
                GroundMovement ground = aircraft.Ground;
                if (ground == null)
                    continue;
                if (!ground.IsTaxiing)
                {
                    aircraft.GroundBlocked = false;
                    continue;
                }
                others.Clear();
                foreach (AircraftController other in all)
                    if (other != aircraft && (other.Ground != null || other.RunwayUse == RunwayUse.LineUp))
                        others.Add(other.transform.position);
                aircraft.GroundBlocked = TaxiQueue.IsBlocked(ground.Position, ground.HeadingVector, others, queueSpacing);
            }
        }

        static bool SameCommands(AircraftCommand[] a, AircraftCommand[] b)
        {
            if (a.Length != b.Length)
                return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i])
                    return false;
            return true;
        }
    }
}
