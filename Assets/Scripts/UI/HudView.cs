using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AirTrafficControl
{
    /// <summary>
    /// Placeholder in-game UI, built in code (no art), following the screen mockups (mockups/index.html):
    /// the landing counter, the Speed and Wind buttons, the wind-change countdown, and the collision panel
    /// with Retry. Shows what <see cref="LevelDirector"/> tells it; no game logic.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        static readonly Color PillColor = new Color(0.05f, 0.12f, 0.2f, 0.75f);
        static readonly Color PanelColor = new Color(0.97f, 0.97f, 0.95f, 0.97f);
        static readonly Color Ink = new Color(0.08f, 0.14f, 0.2f);
        static readonly Color AlertRed = new Color(0.8f, 0.15f, 0.15f);
        static readonly Color ButtonColor = new Color(1f, 0.82f, 0.15f);

        Text landedText;
        Text speedLabel;
        Text windLabel;
        GameObject windButton;
        GameObject windWarning;
        Text windWarningText;
        GameObject collisionPanel;
        Text collisionDetail;
        Font font;

        readonly System.Collections.Generic.List<RectTransform> touchBlockers = new System.Collections.Generic.List<RectTransform>();

        /// <summary>Raised when the player taps Retry.</summary>
        public event Action RetryPressed;

        /// <summary>Raised when the player taps the Speed button.</summary>
        public event Action SpeedPressed;

        /// <summary>Raised when the player taps the Wind button.</summary>
        public event Action WindPressed;

        public void Initialise()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasObject = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1366f, 1024f); // 12.9" iPad in points, landscape
            scaler.matchWidthOrHeight = 0.5f;

            // Buttons need an event system. This project uses the new Input System only.
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            // Landing counter, top left.
            RectTransform pill = CreateBox("LandedPill", canvasObject.transform, PillColor);
            pill.anchorMin = pill.anchorMax = pill.pivot = new Vector2(0f, 1f);
            pill.anchoredPosition = new Vector2(24f, -24f);
            pill.sizeDelta = new Vector2(220f, 64f);
            landedText = CreateText("Landed", pill, 34, Color.white, FontStyle.Bold);

            // Speed and Wind buttons, top right.
            speedLabel = CreateTopRightButton("SpeedButton", canvasObject.transform, 0, () => SpeedPressed?.Invoke(), out _);
            windLabel = CreateTopRightButton("WindButton", canvasObject.transform, 1, () => WindPressed?.Invoke(), out windButton);

            // Wind-change countdown, top centre, hidden until a change is near.
            RectTransform warning = CreateBox("WindWarning", canvasObject.transform, PillColor);
            warning.anchorMin = warning.anchorMax = warning.pivot = new Vector2(0.5f, 1f);
            warning.anchoredPosition = new Vector2(0f, -24f);
            warning.sizeDelta = new Vector2(420f, 64f);
            windWarningText = CreateText("Text", warning, 32, ButtonColor, FontStyle.Bold);
            windWarning = warning.gameObject;
            windWarning.SetActive(false);

            // Collision panel, centred, hidden until a collision.
            RectTransform panel = CreateBox("CollisionPanel", canvasObject.transform, PanelColor);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(640f, 380f);
            collisionPanel = panel.gameObject;

            Text title = CreateText("Title", panel, 48, AlertRed, FontStyle.Bold);
            title.text = "Two aircraft collided";
            Place(title.rectTransform, new Vector2(0f, 110f), new Vector2(600f, 70f));

            collisionDetail = CreateText("Detail", panel, 30, Ink, FontStyle.Normal);
            Place(collisionDetail.rectTransform, new Vector2(0f, 25f), new Vector2(580f, 90f));

            RectTransform buttonBox = CreateBox("RetryButton", panel, ButtonColor);
            Place(buttonBox, new Vector2(0f, -110f), new Vector2(320f, 110f));
            var button = buttonBox.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonBox.GetComponent<Image>();
            button.onClick.AddListener(() => RetryPressed?.Invoke());
            touchBlockers.Add(panel); // the whole panel, so a tap beside Retry cannot grab an aircraft
            Text buttonText = CreateText("Label", buttonBox, 46, Ink, FontStyle.Bold);
            buttonText.text = "Retry";

            collisionPanel.SetActive(false);
            SetLanded(0);
        }

        public void SetLanded(int count)
        {
            landedText.text = $"Landed {count}";
        }

        public void SetSpeedLabel(string label)
        {
            speedLabel.text = label;
        }

        /// <summary>Shows the Wind button, labelled on or off. Hidden on levels without the wind rule.</summary>
        public void SetWind(bool levelHasWind, bool on)
        {
            windButton.SetActive(levelHasWind);
            windLabel.text = on ? "Wind on" : "Wind off";
        }

        /// <summary>Shows "Wind changing in N", or hides it when <paramref name="secondsLeft"/> is 0 or less.</summary>
        public void SetWindWarning(int secondsLeft)
        {
            bool show = secondsLeft > 0;
            if (windWarning.activeSelf != show)
                windWarning.SetActive(show);
            if (show)
                windWarningText.text = $"Wind changing in {secondsLeft}";
        }

        /// <summary>
        /// True if the screen point is on a visible button or panel. The input adapter uses this so that a tap
        /// on the UI never selects an aircraft or starts drawing.
        /// </summary>
        public bool IsOverInteractive(Vector2 screenPoint)
        {
            foreach (RectTransform rect in touchBlockers)
            {
                if (rect.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, null))
                    return true;
            }
            return false;
        }

        public void ShowCollision(float elapsedSeconds, int landedCount)
        {
            string landings = landedCount == 1 ? "1 aircraft" : $"{landedCount} aircraft";
            collisionDetail.text = $"after {LevelSession.FormatTime(elapsedSeconds)}. You had landed {landings}.";
            collisionPanel.SetActive(true);
        }

        public void HideCollision()
        {
            collisionPanel.SetActive(false);
        }

        // Buttons along the top right, from the right edge inwards: slot 0 is rightmost.
        Text CreateTopRightButton(string name, Transform parent, int slot, Action onClick, out GameObject buttonObject)
        {
            RectTransform box = CreateBox(name, parent, PillColor);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(1f, 1f);
            box.anchoredPosition = new Vector2(-24f - slot * 220f, -24f);
            box.sizeDelta = new Vector2(200f, 64f);
            var button = box.gameObject.AddComponent<Button>();
            button.targetGraphic = box.GetComponent<Image>();
            button.onClick.AddListener(() => onClick());
            touchBlockers.Add(box);
            buttonObject = box.gameObject;
            return CreateText("Label", box, 30, Color.white, FontStyle.Bold);
        }

        static RectTransform CreateBox(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go.GetComponent<RectTransform>();
        }

        Text CreateText(string name, RectTransform parent, int size, Color color, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return text;
        }

        static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
