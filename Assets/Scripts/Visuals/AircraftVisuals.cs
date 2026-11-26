using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// What an aircraft shows on top of its image. Only what gives information is kept (owner, 20 and 25 Nov 2026):
    /// - every kind: a soft shadow on the ground that closes in under it as it lands (shows height);
    /// - helicopter and drone: slow, subtle rotors (show that it hovers and lands on a pad).
    /// Strobes, contrails, the fighter's afterburner, speed trail and lean, the balloon's bobbing and burner puffs,
    /// the helicopter's wobble and the drone's blinking light were removed as distracting.
    /// </summary>
    public sealed class AircraftVisuals : MonoBehaviour
    {
        public const int ShadowOrder = -2;
        public const int BodyDetailOrder = 11; // rotors, just above the body (10)

        /// <summary>Lights are white, never red, green or yellow (blueprint 15.3).</summary>
        public static readonly Color LightColor = new Color(1f, 1f, 1f, 1f);
        public static readonly Color ShadowColor = new Color(0f, 0.05f, 0.1f, 1f);

        AircraftController aircraft;
        AircraftType type;
        Transform body;
        Transform shadow;
        Transform mainRotor;
        Transform tailRotor;
        readonly List<Transform> droneBlades = new List<Transform>();

        /// <summary>0 on the ground, 1 in the air. The shadow sits further from the aircraft the higher it is.</summary>
        public float Altitude { get; set; } = 1f;

        /// <summary>True while a helicopter settles onto its pad (its rotors keep full speed).</summary>
        public bool Settling { get; set; }

        public Transform Body => body;

        public void Initialise(AircraftController owner, AircraftType kind, Transform bodyTransform, Mesh shadowMesh, Material material)
        {
            aircraft = owner;
            type = kind;
            body = bodyTransform;

            // Shadow: a world-space copy of the shape, in soft dark, so it can sit below and offset from the aircraft.
            // With a painted image it is the same image, tinted dark.
            if (kind.sprite != null && AircraftController.SpriteMaterial != null)
            {
                shadow = new GameObject("Shadow").transform;
                shadow.position = owner.transform.position;
                SpriteRenderer shadowSprite = AircraftController.CreateSpriteChild(shadow, kind, AircraftController.SpriteMaterial, ShadowOrder);
                shadowSprite.color = new Color(0f, 0.05f, 0.1f, Animations.Settings.shadowAlpha);
            }
            else
            {
                shadow = MeshBuilder.CreateObject("Shadow", shadowMesh, material, ShadowOrder, owner.transform.position, 0f).transform;
                Tint.Set(shadow.GetComponent<Renderer>(), new Color(1f, 1f, 1f, Animations.Settings.shadowAlpha));
            }

            float s = kind.size;
            switch (kind.silhouette)
            {
                case AircraftSilhouette.Helicopter:
                    Vector2[] hubs = Points(kind.rotorPoints, new[] { AircraftShape.HelicopterRotorHub(s), AircraftShape.HelicopterTailHub(s) });
                    mainRotor = Child("Rotor", AircraftShape.HelicopterRotor(kind.color, s), hubs[0], material).transform;
                    tailRotor = Child("TailRotor", AircraftShape.HelicopterTailRotor(kind.color, s), hubs.Length > 1 ? hubs[1] : AircraftShape.HelicopterTailHub(s), material).transform;
                    break;

                case AircraftSilhouette.Drone:
                    foreach (Vector2 hub in Points(kind.rotorPoints, AircraftShape.DroneHubs(s)))
                        droneBlades.Add(Child("Blade", AircraftShape.DroneBlade(kind.color, s), hub, material).transform);
                    break;
            }
        }

        // Points measured on the painted image (scaled by the kind's size) when there is one; otherwise the code shape's.
        Vector2[] Points(Vector2[] painted, Vector2[] codeShape)
        {
            if (type.sprite == null || painted == null || painted.Length == 0)
                return codeShape;
            var result = new Vector2[painted.Length];
            for (int i = 0; i < painted.Length; i++)
                result[i] = painted[i] * type.size;
            return result;
        }

        GameObject Child(string name, Mesh mesh, Vector2 localPosition, Material material)
        {
            GameObject go = MeshBuilder.CreateObject(name, mesh, material, BodyDetailOrder, Vector2.zero, 0f);
            go.transform.SetParent(body, false);
            go.transform.localPosition = localPosition;
            return go;
        }

        void LateUpdate()
        {
            AnimationSettings a = Animations.Settings;
            bool on = Animations.On;
            bool flying = aircraft.IsAirborne;
            float dt = Time.deltaTime;

            // Shadow: same shape and turn as the body, offset by the altitude.
            Transform root = aircraft.transform;
            shadow.SetPositionAndRotation((Vector2)root.position + a.shadowOffset * Mathf.Clamp01(Altitude), body.rotation);
            shadow.localScale = Vector3.Scale(root.localScale, body.localScale);

            if (!on)
                return;
            float spin = Settling || flying ? 1f : 0.3f;
            if (mainRotor != null)
            {
                mainRotor.Rotate(0f, 0f, a.rotorDegreesPerSecond * spin * dt);
                tailRotor.Rotate(0f, 0f, a.tailRotorDegreesPerSecond * spin * dt);
            }
            for (int i = 0; i < droneBlades.Count; i++)
                droneBlades[i].Rotate(0f, 0f, a.rotorDegreesPerSecond * spin * dt * (i % 2 == 0 ? 1f : -1f));
        }

        void OnDestroy()
        {
            if (shadow != null)
                Destroy(shadow.gameObject);
        }
    }
}
