using System;
using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// Draw mode's tap buttons (blueprint 15.4): a tap on an aircraft in the air opens Slow and Fast beside it, and the
    /// chosen speed is given to that aircraft. Draw mode has no ground control (phase 2); Tower mode's buttons are in
    /// <see cref="TowerControl"/>. Input stays in <see cref="InputAdapter"/> and the buttons in <see cref="CommandMenu"/>.
    /// </summary>
    public sealed class GroundControl : MonoBehaviour
    {
        /// <summary>Generous tap target around an aircraft, the same as for drawing.</summary>
        public const float TapRadius = 0.8f;

        InputAdapter input;
        CommandMenu menu;
        List<CommandOption> openOptions = new List<CommandOption>();

        /// <summary>Raised when buttons open for an aircraft, with the options shown (for first-time help).</summary>
        public event Action<AircraftController, IReadOnlyList<CommandOption>> MenuOpened;

        /// <summary>Raised when a command has been given.</summary>
        public event Action<AircraftController, AircraftCommand> CommandGiven;

        public bool IsMenuOpen => menu != null && menu.IsOpen;

        public void Initialise(InputAdapter inputAdapter, CommandMenu commandMenu)
        {
            input = inputAdapter;
            menu = commandMenu;
            input.Tapped += OnTapped;
            input.DragStarted += OnDragStarted;
            menu.OptionChosen += OnOptionChosen;
        }

        void OnDestroy()
        {
            if (input != null)
            {
                input.Tapped -= OnTapped;
                input.DragStarted -= OnDragStarted;
            }
            if (menu != null)
                menu.OptionChosen -= OnOptionChosen;
        }

        public static List<CommandOption> OptionsFor(AircraftController aircraft) =>
            AircraftCommands.DrawOptions(aircraft != null && aircraft.IsControllableInAir && !aircraft.IsLanding,
                                         aircraft != null ? aircraft.SpeedSetting : AircraftSpeed.Normal);

        void OnTapped(Vector2 world)
        {
            AircraftController aircraft = AircraftController.FindNearestForTap(world, TapRadius);
            List<CommandOption> options = OptionsFor(aircraft);
            if (aircraft == null || options.Count == 0)
            {
                menu.Close(); // a tap elsewhere closes the buttons
                return;
            }
            openOptions = options;
            menu.Open(aircraft, options);
            MenuOpened?.Invoke(aircraft, options);
        }

        // Drawing a line somewhere also closes any open buttons.
        void OnDragStarted(Vector2 world) => menu.Close();

        void OnOptionChosen(AircraftController aircraft, CommandOption option)
        {
            if (aircraft == null || !aircraft.IsControllableInAir)
                return;
            if (option.Command == AircraftCommand.Slow)
                aircraft.ToggleSpeed(AircraftSpeed.Slow);
            else if (option.Command == AircraftCommand.Fast)
                aircraft.ToggleSpeed(AircraftSpeed.Fast);
            else
                return;
            CommandGiven?.Invoke(aircraft, option.Command);
        }

        void Update()
        {
            // Close the buttons if the aircraft no longer offers them (it landed or crashed).
            if (menu.IsOpen)
            {
                List<CommandOption> now = OptionsFor(menu.Target);
                if (menu.Target == null || now.Count == 0)
                    menu.Close();
                else if (!AircraftCommands.Same(now, openOptions))
                {
                    openOptions = now;
                    menu.Refresh(now);
                }
            }
        }
    }
}
