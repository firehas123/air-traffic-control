using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// A runway's lights (blueprint 15.3, 15.4): a row of approach lights before the touchdown end whose pulse runs
    /// towards the touchdown zone, and white edge lights along both sides that flash while the runway is busy.
    /// Children of the runway view, so they move to the other end with it when the wind changes. All white.
    /// </summary>
    public sealed class RunwayLights : MonoBehaviour
    {
        readonly List<Renderer> approach = new List<Renderer>();
        Renderer edges;
        bool busy;

        public bool IsBusy => busy;

        public void Initialise(Runway runway, int approachLights, float spacing, Material material)
        {
            float threshold = -runway.Length / 2f;
            for (int i = 0; i < approachLights; i++)
            {
                // Light 0 is furthest out; the last is just before the threshold.
                float x = threshold - spacing * (approachLights - i);
                GameObject light = MeshBuilder.CreateObject("ApproachLight", SceneryShapes.LightDot(0.045f, SceneryShapes.LightOn),
                    material, SceneryShapes.RunwayLightOrder, Vector2.zero, 0f);
                light.transform.SetParent(transform, false);
                light.transform.localPosition = new Vector3(x, 0f, 0f);
                approach.Add(light.GetComponent<Renderer>());
            }

            var b = new MeshBuilder();
            float y = runway.Width / 2f + 0.03f;
            for (float x = threshold; x <= -threshold + 1e-3f; x += 0.4f)
            {
                b.Ellipse(new Vector2(x, y), new Vector2(0.03f, 0.03f), 8, SceneryShapes.LightOn);
                b.Ellipse(new Vector2(x, -y), new Vector2(0.03f, 0.03f), 8, SceneryShapes.LightOn);
            }
            GameObject edgeObject = MeshBuilder.CreateObject("EdgeLights", b.Build("EdgeLights"), material,
                SceneryShapes.RunwayLightOrder, Vector2.zero, 0f);
            edgeObject.transform.SetParent(transform, false);
            edges = edgeObject.GetComponent<Renderer>();
            Tint.SetAlpha(edges, 0.35f);
        }

        /// <summary>The runway is busy (an aircraft landing, rolling, taking off or crossing): the edge lights flash.</summary>
        public void SetBusy(bool isBusy)
        {
            busy = isBusy;
        }

        void Update()
        {
            AnimationSettings a = Animations.Settings;
            float t = Time.unscaledTime;

            int lit = Animations.On ? AnimationMath.ApproachLight(t, approach.Count, a.approachLightSequenceSeconds) : -1;
            for (int i = 0; i < approach.Count; i++)
                Tint.SetAlpha(approach[i], !Animations.On ? 0.6f : i == lit ? 1f : 0.25f);

            // Busy: flash, so the danger is visible (15.4). With animations off, busy shows as steady bright lights.
            float edgeAlpha = busy ? (Animations.On ? (AnimationMath.Flash(t, a.busyFlashSeconds * 2f, a.busyFlashSeconds) ? 1f : 0.3f) : 1f) : 0.35f;
            Tint.SetAlpha(edges, edgeAlpha);
        }
    }
}
