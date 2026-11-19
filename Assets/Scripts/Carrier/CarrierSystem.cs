using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// The carrier ship level feature (blueprint 13.2): sends ships across the sea on a schedule, draws them, and tells
    /// fighters whether their line connects to the deck. A level without a carrier simply never starts one.
    /// Runs on game time, so it pauses and speeds up with the rest of the game.
    /// </summary>
    public sealed class CarrierSystem : MonoBehaviour
    {
        public const int HullSortingOrder = -13;
        public const int DeckSortingOrder = -12;

        CarrierSettings settings;
        Camera worldCamera;
        Material material;
        CarrierSchedule schedule;
        GameObject shipView;
        GameObject deckGlow;
        bool highlighted;

        /// <summary>The ship crossing now, or null.</summary>
        public CarrierPass CurrentPass { get; private set; }

        public bool HasCarrier => settings != null && settings.hasCarrier;

        /// <summary>A ship is fully on screen, so fighters can connect to it.</summary>
        public bool CanConnectNow => CurrentPass != null && CurrentPass.IsFullyOnScreen;

        public void Initialise(CarrierSettings carrierSettings, Camera camera, Material shapeMaterial)
        {
            settings = carrierSettings;
            worldCamera = camera;
            material = shapeMaterial;
        }

        /// <summary>Clears any ship and starts the schedule again, for a new attempt.</summary>
        public void ResetForNewAttempt(int seed)
        {
            RemoveShip();
            schedule = HasCarrier
                ? new CarrierSchedule(settings.firstShipAfterSeconds, settings.minSecondsBetweenShips, settings.maxSecondsBetweenShips, seed)
                : null;
        }

        /// <summary>True if a fighter's line connects to the deck now.</summary>
        public bool TryConnect(IReadOnlyList<Vector2> line) => CarrierRules.CanConnect(CurrentPass, line);

        /// <summary>Makes the deck's touchdown zone glow while a fighter's line is drawn and the ship can be reached.</summary>
        public void SetHighlight(bool on)
        {
            highlighted = on && CanConnectNow;
            if (deckGlow != null && deckGlow.activeSelf != highlighted)
                deckGlow.SetActive(highlighted);
        }

        void Update()
        {
            if (schedule == null)
                return;

            float dt = Time.deltaTime;
            if (CurrentPass != null)
            {
                CurrentPass.Tick(dt);
                if (CurrentPass.IsFinished)
                    RemoveShip();
            }

            if (schedule.Tick(dt, CurrentPass != null))
                StartShip();

            if (CurrentPass != null)
            {
                shipView.transform.SetPositionAndRotation(CurrentPass.Centre, Quaternion.Euler(0f, 0f, CurrentPass.HeadingDegrees));
                if (deckGlow.activeSelf)
                    deckGlow.transform.localScale = new Vector3(1f, 1f + 0.04f * Mathf.Sin(Time.unscaledTime * 6f), 1f);
                if (highlighted && !CurrentPass.IsFullyOnScreen)
                    SetHighlight(false);
            }
        }

        void StartShip()
        {
            float height = worldCamera.orthographicSize * 2f;
            float width = height * worldCamera.aspect;
            float cameraX = worldCamera.transform.position.x;

            CurrentPass = new CarrierPass(cameraX - width / 2f, cameraX + width / 2f, settings.laneY, schedule.NextFromLeft,
                                          settings.secondsToCross, settings.shipLength, settings.shipWidth,
                                          settings.deckLength, settings.deckWidth,
                                          settings.touchdownZoneStart, settings.touchdownZoneLength);
            schedule.ShipStarted();

            shipView = MeshBuilder.CreateObject("Carrier", MapShapes.CreateCarrierHullMesh(CurrentPass.ShipLength, CurrentPass.ShipWidth),
                material, HullSortingOrder, CurrentPass.Centre, CurrentPass.HeadingDegrees);
            shipView.transform.SetParent(transform, true);

            GameObject deck = MeshBuilder.CreateObject("Deck", MapShapes.CreateRunwayMesh(CurrentPass.DeckLocal), material,
                DeckSortingOrder, Vector2.zero, 0f);
            deck.transform.SetParent(shipView.transform, false);

            deckGlow = MeshBuilder.CreateObject("Glow", MapShapes.CreateTouchdownZoneGlowMesh(CurrentPass.DeckLocal), material,
                MapShapes.GlowSortingOrder, Vector2.zero, 0f);
            deckGlow.transform.SetParent(shipView.transform, false);
            deckGlow.SetActive(false);
            highlighted = false;
        }

        void RemoveShip()
        {
            // Fighters keep their own reference to the pass, so one still touching down finishes cleanly.
            CurrentPass = null;
            highlighted = false;
            if (shipView != null)
                Destroy(shipView);
            shipView = null;
            deckGlow = null;
        }
    }
}
