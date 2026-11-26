using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AirTrafficControl
{
    /// <summary>
    /// Tower mode (blueprint 16.1): a letter badge on each runway (A, B, C), matching the Land on runway A/B/C and Taxi to
    /// runway buttons. Each badge sits on the runway's centreline where it is farthest from any crossing runway, so it never
    /// hides an intersection. The badges are drawn on a screen canvas, sized in millimetres like the buttons, so they read
    /// on an iPhone.
    /// </summary>
    public sealed class RunwayLabels : MonoBehaviour
    {
        const float BadgeMm = 7f;

        Camera worldCamera;
        Canvas canvas;
        readonly List<RectTransform> badges = new List<RectTransform>();
        readonly List<Vector2> positions = new List<Vector2>();

        public IReadOnlyList<Vector2> Positions => positions;

        public void Initialise(LandingSystem landing, PictureLayout layout, Camera camera)
        {
            worldCamera = camera;
            var canvasObject = new GameObject("RunwayLabels", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1; // below the HUD and the buttons
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPhysicalSize;
            scaler.physicalUnit = CanvasScaler.Unit.Millimeters;
            scaler.fallbackScreenDPI = 132f;
            scaler.defaultSpriteDPI = 132f;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var runways = new List<Runway>(landing.Runways);
            for (int i = 0; i < runways.Count; i++)
            {
                positions.Add(BadgePosition(runways, i));
                var badge = new GameObject($"Runway {RunwayNames.Letter(i)}", typeof(RectTransform), typeof(Image));
                var rect = badge.GetComponent<RectTransform>();
                rect.SetParent(canvasObject.transform, false);
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(BadgeMm, BadgeMm);
                Image image = badge.GetComponent<Image>();
                image.color = new Color(0.05f, 0.12f, 0.2f, 0.85f);
                image.raycastTarget = false;
                var text = new GameObject("Letter", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
                text.rectTransform.SetParent(rect, false);
                text.rectTransform.anchorMin = Vector2.zero;
                text.rectTransform.anchorMax = Vector2.one;
                text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
                text.font = font;
                text.fontSize = 5;
                text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.white;
                text.text = RunwayNames.Letter(i);
                text.raycastTarget = false;
                badges.Add(rect);
            }
            Place();
        }

        /// <summary>The point between 25% and 75% of the runway's length that is farthest from every other runway.</summary>
        public static Vector2 BadgePosition(IReadOnlyList<Runway> runways, int index)
        {
            Runway runway = runways[index];
            Vector2 best = runway.Centre;
            float bestClearance = float.MinValue;
            for (int k = 0; k <= 20; k++)
            {
                float along = Mathf.Lerp(0.25f, 0.75f, k / 20f) * runway.Length;
                Vector2 p = runway.Threshold + runway.Direction * along;
                float clearance = float.MaxValue;
                for (int j = 0; j < runways.Count; j++)
                    if (j != index)
                        clearance = Mathf.Min(clearance, DistanceToRunway(runways[j], p));
                if (clearance > bestClearance + 1e-4f)
                {
                    bestClearance = clearance;
                    best = p;
                }
            }
            return best;
        }

        static float DistanceToRunway(Runway runway, Vector2 p)
        {
            Vector2 local = runway.ToLocal(p);
            float dx = Mathf.Max(0f, Mathf.Abs(local.x) - runway.Length / 2f);
            float dy = Mathf.Max(0f, Mathf.Abs(local.y) - runway.Width / 2f);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        void Place()
        {
            if (worldCamera == null || canvas == null)
                return;
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            for (int i = 0; i < badges.Count; i++)
            {
                Vector3 screen = worldCamera.WorldToScreenPoint(positions[i]);
                Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, uiCamera, out Vector2 local);
                badges[i].anchoredPosition = local + canvasRect.rect.size / 2f;
            }
        }

        void LateUpdate() => Place(); // the screen can turn or resize
    }
}
