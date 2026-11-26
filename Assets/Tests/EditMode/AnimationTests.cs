using NUnit.Framework;
using UnityEngine;

namespace AirTrafficControl.Tests
{
    /// <summary>Map art and animation rules (M3 step 2, blueprint 15.3).</summary>
    public class AnimationTests
    {
        [TearDown]
        public void TearDown() => Animations.Use(null);

        [Test]
        public void Flash_IsOnlyAtTheStartOfEachPeriod()
        {
            Assert.IsTrue(AnimationMath.Flash(0.02f, 1.2f, 0.08f));
            Assert.IsFalse(AnimationMath.Flash(0.5f, 1.2f, 0.08f));
            Assert.IsTrue(AnimationMath.Flash(1.25f, 1.2f, 0.08f), "Repeats");
            Assert.IsFalse(AnimationMath.Flash(1f, 0f, 0.08f), "No period, no flash");
        }

        [Test]
        public void Wrap_LoopsAcrossTheScreen()
        {
            Assert.AreEqual(-7f, AnimationMath.Wrap(-7f, -7f, 7f), 1e-5f);
            Assert.AreEqual(-6f, AnimationMath.Wrap(8f, -7f, 7f), 1e-5f);
            Assert.AreEqual(6f, AnimationMath.Wrap(-8f, -7f, 7f), 1e-5f);
        }

        [Test]
        public void Sway_StaysWithinItsAmplitude()
        {
            for (float t = 0f; t < 10f; t += 0.13f)
                Assert.LessOrEqual(Mathf.Abs(AnimationMath.Sway(t, 6f, 3.2f)), 6f + 1e-4f);
        }

        [Test]
        public void OneSwitch_TurnsEveryAnimationOff()
        {
            var settings = ScriptableObject.CreateInstance<AnimationSettings>();
            Animations.Use(settings);
            Assert.IsTrue(Animations.On);

            settings.animationsOn = false; // the asset's master switch (the Anims button was removed, 25 Nov 2026)
            Assert.IsFalse(Animations.On);
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void AircraftLights_AreWhite_NeverStatusColours()
        {
            Color c = AircraftVisuals.LightColor;
            Assert.AreEqual(c.r, c.g, 1e-5f);
            Assert.AreEqual(c.g, c.b, 1e-5f);
            Assert.AreEqual(1f, c.r, 1e-5f);
            Assert.AreEqual(SceneryShapes.LightOn.r, SceneryShapes.LightOn.b, 1e-5f, "Runway and deck lights are white too");
        }

        [Test]
        public void StatusRings_AreDrawnAboveAllScenery()
        {
            // Rings are drawn at 8 (status) and 9 (danger); the aircraft body at 10. Everything decorative is below
            // the path lines (0), so the rings stay the clearest thing on screen.
            int[] scenery =
            {
                SceneryShapes.SeaOrder, SceneryShapes.FoamOrder, SceneryShapes.BeachDetailOrder, SceneryShapes.ApronOrder,
                SceneryShapes.TaxiwayOrder, SceneryShapes.BuildingOrder, SceneryShapes.PalmOrder, SceneryShapes.WindsockOrder,
                SceneryShapes.BoatOrder, SceneryShapes.RunwayLightOrder, SceneryShapes.CloudShadowOrder, SceneryShapes.CloudOrder,
                SceneryShapes.BirdOrder, AircraftVisuals.ShadowOrder,
            };
            foreach (int order in scenery)
                Assert.Less(order, 0, $"Scenery order {order} must be below the path lines and rings");
        }

        [Test]
        public void RunwayLights_ShowOnlyWhileTheRunwayIsBusy()
        {
            for (float t = 0f; t < 2f; t += 0.07f)
                Assert.AreEqual(0f, RunwayLights.EdgeAlpha(false, true, t, 0.4f), "Hidden when not busy: no pulsing lights");
            Assert.AreEqual(1f, RunwayLights.EdgeAlpha(true, true, 0.1f, 0.4f));
            Assert.AreEqual(0.3f, RunwayLights.EdgeAlpha(true, true, 0.5f, 0.4f), 1e-5f, "Flashes while busy");
            Assert.AreEqual(1f, RunwayLights.EdgeAlpha(true, false, 0.5f, 0.4f), "Steady when animations are off");
        }

        [Test]
        public void OnlyInformativeAnimationsAreLeft()
        {
            // Owner, 25 Nov 2026: slow, subtle rotors and a short smoke puff; the rest was removed.
            var settings = ScriptableObject.CreateInstance<AnimationSettings>();
            Assert.LessOrEqual(settings.rotorDegreesPerSecond, 200f, "Slow rotors");
            Assert.LessOrEqual(settings.smokeSeconds, 1.2f, "Short smoke puff");
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationSettings>("Assets/Config/AnimationSettings.asset");
            Assert.IsNotNull(asset);
            Assert.LessOrEqual(asset.rotorDegreesPerSecond, 200f, "The asset in the game uses slow rotors too");
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void Shapes_AreBuiltWithoutErrors()
        {
            Assert.Greater(SceneryShapes.SeaWaves(new Vector2(16f, 11f), 20, 1).vertexCount, 0);
            Assert.Greater(SceneryShapes.FoamRing(new Vector2(3.8f, 2.1f)).vertexCount, 0);
            Assert.Greater(SceneryShapes.PalmFronds(3).vertexCount, 0);
            Assert.Greater(SceneryShapes.WindsockCone().vertexCount, 0);
            Assert.Greater(AircraftShape.HelicopterRotor(Color.white, 1f).vertexCount, 0);
            Assert.AreEqual(4, AircraftShape.DroneHubs(0.6f).Length);
        }
    }
}
