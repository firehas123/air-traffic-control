using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// The first-time help demonstration on the map (M2 step 5): a dashed ghost line from an aircraft to where it lands,
    /// with a white dot travelling along it like a finger drawing. Shown only while the drawing hint is up.
    /// </summary>
    public sealed class HelpDemo : MonoBehaviour
    {
        const int SortingOrder = 7; // above the path lines, below the aircraft
        const float LoopSeconds = 2f;
        static readonly Color GhostColor = new Color(1f, 1f, 1f, 0.55f);
        static readonly Color FingerColor = new Color(1f, 1f, 1f, 0.95f);

        Mesh mesh;
        GameObject view;

        public void Initialise(Material material)
        {
            mesh = new Mesh { name = "HelpDemo" };
            view = MeshBuilder.CreateObject("HelpDemo", mesh, material, SortingOrder, Vector2.zero, 0f);
            view.transform.SetParent(transform, true);
            view.SetActive(false);
        }

        /// <summary>Shows the demo from <paramref name="from"/> to <paramref name="to"/>, or hides it when <paramref name="from"/> is null.</summary>
        public void Show(Vector2? from, Vector2 to)
        {
            if (!from.HasValue)
            {
                if (view.activeSelf)
                    view.SetActive(false);
                return;
            }
            if (!view.activeSelf)
                view.SetActive(true);

            // Real time, so the demo keeps its pace under slow draw and 2x speed.
            float t = Mathf.Repeat(Time.unscaledTime, LoopSeconds) / LoopSeconds;
            Vector2 finger = Vector2.Lerp(from.Value, to, Mathf.SmoothStep(0f, 1f, t));

            var builder = new MeshBuilder();
            MapShapes.BuildDashes(builder, from.Value, to, GhostColor);
            builder.Ellipse(finger, new Vector2(0.14f, 0.14f), 20, FingerColor);
            builder.Ring(to, 0.2f, 0.26f, 28, GhostColor);
            builder.Fill(mesh);
        }

        void OnDestroy()
        {
            if (mesh != null)
                Destroy(mesh);
        }
    }
}
