using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// The visible fuel timer of a low-fuel aircraft (Desert rule, blueprint 16.2): a dark disc beside the status ring
    /// with the seconds left in white and a white arc that empties as the fuel runs down. Below 10 seconds the disc
    /// pulses. White on dark, never a status colour; it does not turn with the aircraft.
    /// </summary>
    public sealed class FuelGaugeView : MonoBehaviour
    {
        const float Radius = 0.2f;
        const int Segments = 32;
        static readonly Color Back = new Color(0.08f, 0.1f, 0.14f, 0.92f);
        static readonly Color Arc = new Color(1f, 1f, 1f, 0.95f);

        Mesh arcMesh;
        TextMesh label;
        int shownSeconds = -1;
        float shownFraction = -1f;

        public void Initialise(Material material, int sortingOrder)
        {
            GameObject disc = MeshBuilder.CreateObject("FuelDisc", new MeshBuilder().Ellipse(Vector2.zero, Vector2.one * Radius, Segments, Back).Build("FuelDisc"),
                material, sortingOrder, Vector2.zero, 0f);
            disc.transform.SetParent(transform, false);

            arcMesh = new Mesh { name = "FuelArc" };
            GameObject arc = MeshBuilder.CreateObject("FuelArc", arcMesh, material, sortingOrder + 1, Vector2.zero, 0f);
            arc.transform.SetParent(transform, false);

            var text = new GameObject("FuelSeconds");
            text.transform.SetParent(transform, false);
            label = text.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.font = font;
            label.fontSize = 64;
            label.characterSize = 0.035f;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontStyle = FontStyle.Bold;
            label.color = Color.white;
            var renderer = text.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material;
            renderer.sortingOrder = sortingOrder + 2;
        }

        /// <summary>Shows the seconds left and the fraction of the fuel still in the tank.</summary>
        public void Show(float secondsLeft, float fraction)
        {
            int seconds = Mathf.Max(0, Mathf.CeilToInt(secondsLeft));
            if (seconds != shownSeconds)
            {
                shownSeconds = seconds;
                label.text = seconds.ToString();
            }
            fraction = Mathf.Clamp01(fraction);
            if (Mathf.Abs(fraction - shownFraction) > 0.01f)
            {
                shownFraction = fraction;
                BuildArc(fraction);
            }
            float pulse = secondsLeft < 10f ? 1f + 0.12f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f)) : 1f;
            transform.localScale = Vector3.one * pulse;
        }

        // A ring segment from the top, clockwise, covering the fuel left.
        void BuildArc(float fraction)
        {
            var b = new MeshBuilder();
            int count = Mathf.CeilToInt(Segments * fraction);
            for (int i = 0; i < count; i++)
            {
                float a0 = Mathf.PI / 2f - 2f * Mathf.PI * fraction * i / count;
                float a1 = Mathf.PI / 2f - 2f * Mathf.PI * fraction * (i + 1) / count;
                var d0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0));
                var d1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));
                b.Quad(d0 * (Radius - 0.045f), d0 * Radius, d1 * Radius, d1 * (Radius - 0.045f), Arc);
            }
            b.Fill(arcMesh);
        }

        void OnDestroy()
        {
            if (arcMesh != null)
                Destroy(arcMesh);
        }
    }
}
