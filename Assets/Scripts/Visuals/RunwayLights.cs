using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// A runway's edge lights (blueprint 15.4): white lights along both sides that flash only while the runway is busy
    /// (an aircraft landing, rolling, taking off or crossing), so the danger is visible. Hidden otherwise: the painted
    /// map shows the runway, and the pulsing approach lights were removed by the owner (25 Nov 2026). Children of the
    /// runway view, so they turn with it when the wind changes. All white.
    /// </summary>
    public sealed class RunwayLights : MonoBehaviour
    {
        Renderer edges;
        bool busy;

        public bool IsBusy => busy;

        /// <summary>Alpha of the edge lights at a moment: 0 when not busy; flashing between bright and dim when busy.</summary>
        public static float EdgeAlpha(bool busy, bool animationsOn, float time, float flashSeconds)
        {
            if (!busy)
                return 0f;
            if (!animationsOn)
                return 1f; // steady bright lights still show that the runway is busy
            return AnimationMath.Flash(time, flashSeconds * 2f, flashSeconds) ? 1f : 0.3f;
        }

        public void Initialise(Runway runway, Material material)
        {
            float threshold = -runway.Length / 2f;
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
            Tint.SetAlpha(edges, 0f);
        }

        public void SetBusy(bool isBusy)
        {
            busy = isBusy;
        }

        void Update()
        {
            Tint.SetAlpha(edges, EdgeAlpha(busy, Animations.On, Time.unscaledTime, Animations.Settings.busyFlashSeconds));
        }
    }
}
