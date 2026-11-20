using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// An aircraft's animation (blueprint 15.3), on top of its shape:
    /// - every kind: a soft shadow on the ground that closes in under it as it lands;
    /// - airliner and jumbo jet: blinking white strobes on the wings and tail, and thin contrails;
    /// - fighter jet: a flickering afterburner, a short speed trail, and leaning into turns;
    /// - hot air balloon: gentle bobbing and sway, and a burner puff every few seconds;
    /// - helicopter: spinning main and tail rotors, and a wobble as it settles on the helipad;
    /// - drone: four spinning rotors and a small blinking light.
    /// All lights are white: red and green mean danger and cleared in this game, so real wingtip colours are not used.
    /// The shape lives on a child ("Body"), so leaning and bobbing never stretch the status rings.
    /// </summary>
    public sealed class AircraftVisuals : MonoBehaviour
    {
        public const int ShadowOrder = -2;
        public const int TrailOrder = -1;
        public const int BodyDetailOrder = 11; // rotors, lights and flame, just above the body (10)

        /// <summary>Lights are white, never red, green or yellow (blueprint 15.3).</summary>
        public static readonly Color LightColor = new Color(1f, 1f, 1f, 1f);
        static readonly Color FlameColor = new Color(0.75f, 0.9f, 1f, 0.9f);      // pale blue-white afterburner
        static readonly Color BurnerColor = new Color(1f, 0.9f, 0.75f, 0.9f);    // warm white burner puff
        public static readonly Color ShadowColor = new Color(0f, 0.05f, 0.1f, 1f);

        AircraftController aircraft;
        AircraftType type;
        Transform body;
        Transform shadow;
        Renderer shadowRenderer;
        SpriteRenderer shadowSprite;
        Renderer strobes;
        Transform mainRotor;
        Transform tailRotor;
        readonly List<Transform> droneBlades = new List<Transform>();
        Renderer droneLight;
        Transform flame;
        Renderer burner;
        readonly List<TrailRenderer> trails = new List<TrailRenderer>();
        float lastHeading;
        float lean = 1f;
        float phase;

        /// <summary>0 on the ground, 1 in the air. The shadow sits further from the aircraft the higher it is.</summary>
        public float Altitude { get; set; } = 1f;

        /// <summary>True while a helicopter settles onto its pad (for the wobble).</summary>
        public bool Settling { get; set; }

        public Transform Body => body;

        public void Initialise(AircraftController owner, AircraftType kind, Transform bodyTransform, Mesh shadowMesh, Material material)
        {
            aircraft = owner;
            type = kind;
            body = bodyTransform;
            phase = Random.value * 10f;
            lastHeading = owner.transform.eulerAngles.z;

            // Shadow: a world-space copy of the shape, in soft dark, so it can sit below and offset from the aircraft.
            // With a painted image it is the same image, tinted dark.
            if (kind.sprite != null && AircraftController.SpriteMaterial != null)
            {
                shadow = new GameObject("Shadow").transform;
                shadow.position = owner.transform.position;
                shadowSprite = AircraftController.CreateSpriteChild(shadow, kind, AircraftController.SpriteMaterial, ShadowOrder);
                shadowSprite.color = new Color(0f, 0.05f, 0.1f, Animations.Settings.shadowAlpha);
            }
            else
            {
                shadow = MeshBuilder.CreateObject("Shadow", shadowMesh, material, ShadowOrder, owner.transform.position, 0f).transform;
                shadowRenderer = shadow.GetComponent<Renderer>();
                Tint.Set(shadowRenderer, new Color(1f, 1f, 1f, Animations.Settings.shadowAlpha));
            }

            float s = kind.size;
            switch (kind.silhouette)
            {
                case AircraftSilhouette.Airliner:
                case AircraftSilhouette.JumboJet:
                    var lights = new MeshBuilder();
                    foreach (Vector2 p in Points(kind.lightPoints, AircraftShape.StrobePoints(kind.silhouette, s)))
                        lights.Ellipse(p, new Vector2(0.035f, 0.035f) * Mathf.Max(1f, s), 10, LightColor);
                    strobes = Child("Strobes", lights.Build("Strobes"), Vector2.zero, material).GetComponent<Renderer>();
                    foreach (Vector2 p in Points(kind.enginePoints, AircraftShape.EnginePoints(kind.silhouette, s)))
                        trails.Add(Trail(p, Animations.Settings.contrailSeconds, 0.035f * s, 0.32f, material));
                    break;

                case AircraftSilhouette.FighterJet:
                    Vector2 exhaust = Points(kind.enginePoints, new[] { AircraftShape.FighterExhaust(s) })[0];
                    flame = Child("Afterburner", new MeshBuilder()
                        .Triangle(new Vector2(0f, -0.035f * s), new Vector2(-0.16f * s, 0f), new Vector2(0f, 0.035f * s), FlameColor)
                        .Build("Flame"), exhaust, material).transform;
                    trails.Add(Trail(exhaust, Animations.Settings.fighterTrailSeconds, 0.05f * s, 0.45f, material));
                    break;

                case AircraftSilhouette.Helicopter:
                    Vector2[] hubs = Points(kind.rotorPoints, new[] { AircraftShape.HelicopterRotorHub(s), AircraftShape.HelicopterTailHub(s) });
                    mainRotor = Child("Rotor", AircraftShape.HelicopterRotor(kind.color, s), hubs[0], material).transform;
                    tailRotor = Child("TailRotor", AircraftShape.HelicopterTailRotor(kind.color, s), hubs.Length > 1 ? hubs[1] : AircraftShape.HelicopterTailHub(s), material).transform;
                    break;

                case AircraftSilhouette.Drone:
                    foreach (Vector2 hub in Points(kind.rotorPoints, AircraftShape.DroneHubs(s)))
                        droneBlades.Add(Child("Blade", AircraftShape.DroneBlade(kind.color, s), hub, material).transform);
                    droneLight = Child("Light", SceneryShapes.LightDot(0.035f, LightColor), Vector2.zero, material).GetComponent<Renderer>();
                    break;

                case AircraftSilhouette.HotAirBalloon:
                    burner = Child("Burner", SceneryShapes.LightDot(0.09f * s, BurnerColor), Vector2.zero, material).GetComponent<Renderer>();
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

        TrailRenderer Trail(Vector2 localPosition, float seconds, float width, float alpha, Material material)
        {
            var go = new GameObject("Trail");
            go.transform.SetParent(body, false);
            go.transform.localPosition = localPosition;
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material;
            trail.time = seconds;
            trail.minVertexDistance = 0.05f;
            trail.widthMultiplier = width;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                             new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            trail.sortingOrder = TrailOrder;
            trail.numCapVertices = 2;
            return trail;
        }

        void LateUpdate()
        {
            AnimationSettings a = Animations.Settings;
            bool on = Animations.On;
            bool flying = aircraft.IsAirborne;
            float t = Time.time + phase;
            float dt = Time.deltaTime;

            // Shadow: same shape and turn as the body, offset by the altitude.
            Transform root = aircraft.transform;
            shadow.SetPositionAndRotation((Vector2)root.position + a.shadowOffset * Mathf.Clamp01(Altitude), body.rotation);
            shadow.localScale = Vector3.Scale(root.localScale, body.localScale);

            foreach (TrailRenderer trail in trails)
            {
                bool emit = on && flying;
                if (trail.emitting != emit)
                {
                    trail.emitting = emit;
                    if (!emit && !on)
                        trail.Clear();
                }
            }

            if (strobes != null)
                Tint.SetAlpha(strobes, on && flying && AnimationMath.Flash(t, a.strobeEverySeconds, a.strobeFlashSeconds) ? 1f : 0f);

            if (flame != null)
            {
                bool show = on && flying;
                flame.gameObject.SetActive(show);
                if (show)
                    flame.localScale = new Vector3(1f + a.afterburnerFlicker * (Random.value - 0.5f) * 2f, 1f, 1f);
            }

            // Fighter lean: flatter while turning, back to normal when straight.
            if (type.silhouette == AircraftSilhouette.FighterJet && dt > 0f)
            {
                float heading = root.eulerAngles.z;
                float turnRate = Mathf.DeltaAngle(lastHeading, heading) / dt;
                lastHeading = heading;
                float target = on && flying ? AnimationMath.Lean(turnRate, a.fighterLeanScale) : 1f;
                lean = Mathf.MoveTowards(lean, target, 3f * dt);
                body.localScale = new Vector3(1f, lean, 1f);
            }

            if (mainRotor != null && on)
            {
                float spin = Settling || flying ? 1f : 0.3f;
                mainRotor.Rotate(0f, 0f, a.rotorDegreesPerSecond * spin * dt);
                tailRotor.Rotate(0f, 0f, a.tailRotorDegreesPerSecond * spin * dt);
            }
            if (type.silhouette == AircraftSilhouette.Helicopter)
                body.localRotation = Quaternion.Euler(0f, 0f, on && Settling ? AnimationMath.Sway(t, a.helicopterWobbleDegrees, 0.45f) : 0f);

            if (droneBlades.Count > 0)
            {
                if (on)
                    for (int i = 0; i < droneBlades.Count; i++)
                        droneBlades[i].Rotate(0f, 0f, a.rotorDegreesPerSecond * 1.3f * dt * (i % 2 == 0 ? 1f : -1f));
                Tint.SetAlpha(droneLight, on && AnimationMath.Flash(t, a.droneBlinkSeconds, a.droneBlinkSeconds * 0.4f) ? 1f : on ? 0.15f : 0f);
            }

            if (type.silhouette == AircraftSilhouette.HotAirBalloon)
            {
                float bob = on ? 1f + AnimationMath.Sway(t, a.balloonBobAmount, a.balloonBobSeconds) : 1f;
                body.localScale = new Vector3(bob, bob, 1f);
                body.localPosition = on ? new Vector3(AnimationMath.Sway(t, 0.03f, a.balloonBobSeconds * 1.7f), 0f, 0f) : Vector3.zero;
                float sinceBurn = Mathf.Repeat(t, a.balloonBurnerEverySeconds);
                Tint.SetAlpha(burner, on && sinceBurn < 0.35f ? 1f - sinceBurn / 0.35f : 0f);
            }
        }

        /// <summary>Turns strobes, flame and trails off (after a collision).</summary>
        public void HideLights()
        {
            if (strobes != null) Tint.SetAlpha(strobes, 0f);
            if (flame != null) flame.gameObject.SetActive(false);
            foreach (TrailRenderer trail in trails) trail.emitting = false;
        }

        void OnDestroy()
        {
            if (shadow != null)
                Destroy(shadow.gameObject);
        }
    }
}
