using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AirTrafficControl
{
    /// <summary>
    /// Placeholder in-game UI, built in code (no art): the landing counter and the collision panel with Retry,
    /// following the screen mockups (mockups/index.html). Shows what <see cref="LevelDirector"/> tells it; no game logic.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        static readonly Color PillColor = new Color(0.05f, 0.12f, 0.2f, 0.75f);
        static readonly Color PanelColor = new Color(0.97f, 0.97f, 0.95f, 0.97f);
        static readonly Color Ink = new Color(0.08f, 0.14f, 0.2f);
        static readonly Color AlertRed = new Color(0.8f, 0.15f, 0.15f);
        static readonly Color ButtonColor = new Color(1f, 0.82f, 0.15f);

        Text landedText;
        GameObject collisionPanel;
        Text collisionDetail;
        Font font;

        /// <summary>Raised when the player taps Retry.</summary>
        public event Action RetryPressed;

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
            Text buttonText = CreateText("Label", buttonBox, 46, Ink, FontStyle.Bold);
            buttonText.text = "Retry";

            collisionPanel.SetActive(false);
            SetLanded(0);
        }

        public void SetLanded(int count)
        {
            landedText.text = $"Landed {count}";
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
