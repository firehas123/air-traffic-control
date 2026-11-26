using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// The clear X over a closed runway (City and Desert rules, blueprint 16.2): the runway is dimmed and a large white X
    /// with a dark outline sits on each half, like the real closed-runway marking. While the runway is closing soon the X
    /// blinks (in real time, so it still blinks while slow draw slows the game). White, never a status colour.
    /// </summary>
    public sealed class RunwayClosedMark : MonoBehaviour
    {
        public const int SortingOrder = -4; // above the runway overlay and the zone glow, below aircraft and lines
        const float BlinkSeconds = 0.35f;

        static readonly Color Dim = new Color(0.05f, 0.07f, 0.1f, 0.45f);
        static readonly Color Outline = new Color(0.05f, 0.07f, 0.1f, 0.9f);
        static readonly Color Mark = new Color(1f, 1f, 1f, 0.95f);

        Renderer view;
        RunwayState state;

        public RunwayState State => state;

        public void Initialise(Runway runway, Material material)
        {
            // Built in world space: the runway is dimmed along its own direction, while each X stays upright on screen,
            // so it reads as an X on a diagonal runway too (not as a +).
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Vector2 along = runway.Direction * (runway.Length / 2f);
            Vector2 across = new Vector2(-runway.Direction.y, runway.Direction.x) * (runway.Width / 2f);
            Vector2 c0 = runway.Centre;
            var b = new MeshBuilder().Quad(c0 - along - across, c0 + along - across, c0 + along + across, c0 - along + across, Dim);
            // One X on each half of the runway, a little wider than the runway, so it reads on a phone.
            float arm = Mathf.Max(0.42f, runway.Width * 1.1f);
            foreach (float f in new[] { -0.5f, 0.5f })
            {
                Vector2 c = runway.Centre + along * f;
                AddX(b, c, arm, 0.2f, Outline);
                AddX(b, c, arm, 0.11f, Mark);
            }
            GameObject go = MeshBuilder.CreateObject("ClosedX", b.Build("ClosedX"), material, SortingOrder, Vector2.zero, 0f);
            go.transform.SetParent(transform, false);
            view = go.GetComponent<Renderer>();
            SetState(RunwayState.Open);
        }

        static void AddX(MeshBuilder b, Vector2 centre, float arm, float thickness, Color color)
        {
            b.Bar(centre + new Vector2(-arm, -arm) * 0.5f, centre + new Vector2(arm, arm) * 0.5f, thickness, color);
            b.Bar(centre + new Vector2(-arm, arm) * 0.5f, centre + new Vector2(arm, -arm) * 0.5f, thickness, color);
        }

        public void SetState(RunwayState newState)
        {
            state = newState;
            view.gameObject.SetActive(state != RunwayState.Open);
            Tint.SetAlpha(view, 1f);
        }

        /// <summary>Closing soon: the X blinks between full and faint.</summary>
        public static float Alpha(RunwayState state, float time) =>
            state == RunwayState.Closing ? (Mathf.Repeat(time, BlinkSeconds * 2f) < BlinkSeconds ? 1f : 0.25f) : 1f;

        void Update()
        {
            if (state == RunwayState.Closing)
                Tint.SetAlpha(view, Alpha(state, Time.unscaledTime));
        }
    }
}
