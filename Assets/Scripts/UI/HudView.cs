using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AirTrafficControl
{
    /// <summary>
    /// Placeholder in-game UI, built in code (no art), following the screen mockups (mockups/index.html):
    /// - top left: "Landed N of M" and the level timer
    /// - top right: Speed (1x / 2x), Wind (C1 switch) and Slow draw (C4 switch) buttons
    /// - top centre: the wind-change countdown
    /// - centre: the end panel, either "Two aircraft collided" with Retry or "Level cleared" with Play again
    /// Shows what <see cref="LevelDirector"/> tells it; no game logic.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        static readonly Color PillColor = new Color(0.05f, 0.12f, 0.2f, 0.75f);
        static readonly Color PanelColor = new Color(0.97f, 0.97f, 0.95f, 0.97f);
        static readonly Color Ink = new Color(0.08f, 0.14f, 0.2f);
        static readonly Color AlertRed = new Color(0.8f, 0.15f, 0.15f);
        static readonly Color SuccessGreen = new Color(0.12f, 0.55f, 0.25f);
        static readonly Color ButtonColor = new Color(1f, 0.82f, 0.15f);

        readonly List<RectTransform> touchBlockers = new List<RectTransform>();

        Font font;
        Text landedText;
        Text timeText;
        int shownSeconds = -1;
        Text speedLabel;
        Text windLabel;
        GameObject windButton;
        Text slowDrawLabel;
        GameObject windWarning;
        Text windWarningText;
        GameObject carrierCountdown;
        Text carrierCountdownText;
        GameObject endPanel;
        Text endTitle;
        Text endDetail;
        Text endButtonLabel;
        readonly Image[] endStars = new Image[3];
        static readonly Color StarEarned = new Color(1f, 0.78f, 0.1f);
        static readonly Color StarMissing = new Color(0.78f, 0.8f, 0.82f);

        /// <summary>Raised when the player taps Retry or Play again.</summary>
        public event Action RetryPressed;

        /// <summary>Raised when the player taps the Speed button.</summary>
        public event Action SpeedPressed;

        /// <summary>Raised when the player taps the Wind button.</summary>
        public event Action WindPressed;

        /// <summary>Raised when the player taps the Slow draw button.</summary>
        public event Action SlowDrawPressed;

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
            Transform root = canvasObject.transform;

            // Buttons need an event system. This project uses the new Input System only.
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            // Top left: landing counter and timer.
            landedText = CreateText("Landed", TopLeftPill("LandedPill", root, 24f, 260f), 32, Color.white, FontStyle.Bold);
            timeText = CreateText("Time", TopLeftPill("TimePill", root, 300f, 120f), 32, Color.white, FontStyle.Bold);

            // Top right: Speed, Wind, Slow draw.
            speedLabel = CreateTopRightButton("SpeedButton", root, 0, () => SpeedPressed?.Invoke(), out _);
            windLabel = CreateTopRightButton("WindButton", root, 1, () => WindPressed?.Invoke(), out windButton);
            slowDrawLabel = CreateTopRightButton("SlowDrawButton", root, 2, () => SlowDrawPressed?.Invoke(), out _);

            // Wind-change countdown, top centre below the buttons, hidden until a change is near.
            RectTransform warning = CreateBox("WindWarning", root, PillColor);
            warning.anchorMin = warning.anchorMax = warning.pivot = new Vector2(0.5f, 1f);
            warning.anchoredPosition = new Vector2(0f, -110f);
            warning.sizeDelta = new Vector2(420f, 64f);
            windWarningText = CreateText("Text", warning, 32, ButtonColor, FontStyle.Bold);
            windWarning = warning.gameObject;
            windWarning.SetActive(false);

            // Carrier countdown (blueprint 13.2), below the wind countdown, while a ship can be reached.
            RectTransform countdown = CreateBox("CarrierCountdown", root, PillColor);
            countdown.anchorMin = countdown.anchorMax = countdown.pivot = new Vector2(0.5f, 1f);
            countdown.anchoredPosition = new Vector2(0f, -186f);
            countdown.sizeDelta = new Vector2(420f, 64f);
            carrierCountdownText = CreateText("Text", countdown, 32, Color.white, FontStyle.Bold);
            carrierCountdown = countdown.gameObject;
            carrierCountdown.SetActive(false);

            // End panel, centred, hidden until the level ends.
            RectTransform panel = CreateBox("EndPanel", root, PanelColor);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(660f, 460f);
            endPanel = panel.gameObject;
            touchBlockers.Add(panel); // the whole panel, so a tap beside the button cannot grab an aircraft

            endTitle = CreateText("Title", panel, 48, AlertRed, FontStyle.Bold);
            Place(endTitle.rectTransform, new Vector2(0f, 170f), new Vector2(620f, 70f));

            // Stars (result screen only).
            for (int i = 0; i < 3; i++)
            {
                var starObject = new GameObject("Star" + i, typeof(RectTransform), typeof(Image));
                starObject.transform.SetParent(panel, false);
                Image star = starObject.GetComponent<Image>();
                star.sprite = StarSprite.Get();
                star.raycastTarget = false;
                Place(star.rectTransform, new Vector2((i - 1) * 90f, 95f), new Vector2(78f, 78f));
                endStars[i] = star;
            }

            endDetail = CreateText("Detail", panel, 30, Ink, FontStyle.Normal);
            endDetail.lineSpacing = 1.15f;
            Place(endDetail.rectTransform, new Vector2(0f, -5f), new Vector2(620f, 120f));

            RectTransform buttonBox = CreateBox("EndButton", panel, ButtonColor);
            Place(buttonBox, new Vector2(0f, -150f), new Vector2(320f, 110f));
            var button = buttonBox.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonBox.GetComponent<Image>();
            button.onClick.AddListener(() => RetryPressed?.Invoke());
            endButtonLabel = CreateText("Label", buttonBox, 46, Ink, FontStyle.Bold);

            endPanel.SetActive(false);
            SetLanded(0, 0);
            SetTime(0f);
        }

        /// <summary>"Landed N of M", or "Landed N" when the level has no landing target.</summary>
        public void SetLanded(int count, int target)
        {
            landedText.text = target > 0 ? $"Landed {count} of {target}" : $"Landed {count}";
        }

        public void SetTime(float elapsedSeconds)
        {
            int whole = Mathf.FloorToInt(elapsedSeconds);
            if (whole == shownSeconds)
                return;
            shownSeconds = whole;
            timeText.text = LevelSession.FormatTime(elapsedSeconds);
        }

        public void SetSpeedLabel(string label) => speedLabel.text = label;

        public void SetSlowDrawLabel(string label) => slowDrawLabel.text = label;

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

        /// <summary>Shows "Carrier leaving in N", or hides it when <paramref name="secondsLeft"/> is 0 or less.</summary>
        public void SetCarrierCountdown(int secondsLeft)
        {
            bool show = secondsLeft > 0;
            if (carrierCountdown.activeSelf != show)
                carrierCountdown.SetActive(show);
            if (show)
                carrierCountdownText.text = $"Carrier leaving in {secondsLeft}";
        }

        public void ShowCollision(float elapsedSeconds, int landedCount, int target)
        {
            string landed = target > 0 ? $"{landedCount} of {target}" : (landedCount == 1 ? "1 aircraft" : $"{landedCount} aircraft");
            ShowEnd("Two aircraft collided", AlertRed,
                    $"after {LevelSession.FormatTime(elapsedSeconds)}. You had landed {landed}.", "Retry");
            foreach (Image star in endStars)
                star.gameObject.SetActive(false);
        }

        /// <summary>
        /// The level result screen (C10): landed, time, closest pass, stars and coins, with one button onward.
        /// There is only one level so far, so the button plays it again.
        /// </summary>
        public void ShowCleared(float elapsedSeconds, int landedCount, int target, float closestGap, int stars, int coins)
        {
            ShowEnd("Level cleared", SuccessGreen,
                    $"Landed {landedCount} of {target} in {LevelSession.FormatTime(elapsedSeconds)}\n" +
                    $"{LevelResult.ClosestPassText(closestGap)}   ·   +{coins} coins", "Play again");
            for (int i = 0; i < endStars.Length; i++)
            {
                endStars[i].gameObject.SetActive(true);
                endStars[i].color = i < stars ? StarEarned : StarMissing;
            }
        }

        public void HideEnd()
        {
            endPanel.SetActive(false);
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

        void ShowEnd(string title, Color titleColor, string detail, string buttonLabel)
        {
            endTitle.text = title;
            endTitle.color = titleColor;
            endDetail.text = detail;
            endButtonLabel.text = buttonLabel;
            endPanel.SetActive(true);
        }

        RectTransform TopLeftPill(string name, Transform parent, float x, float width)
        {
            RectTransform pill = CreateBox(name, parent, PillColor);
            pill.anchorMin = pill.anchorMax = pill.pivot = new Vector2(0f, 1f);
            pill.anchoredPosition = new Vector2(x, -24f);
            pill.sizeDelta = new Vector2(width, 64f);
            return pill;
        }

        // Buttons along the top right, from the right edge inwards: slot 0 is rightmost.
        Text CreateTopRightButton(string name, Transform parent, int slot, Action onClick, out GameObject buttonObject)
        {
            RectTransform box = CreateBox(name, parent, PillColor);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(1f, 1f);
            box.anchoredPosition = new Vector2(-24f - slot * 230f, -24f);
            box.sizeDelta = new Vector2(214f, 64f);
            var button = box.gameObject.AddComponent<Button>();
            button.targetGraphic = box.GetComponent<Image>();
            button.onClick.AddListener(() => onClick());
            touchBlockers.Add(box);
            buttonObject = box.gameObject;
            return CreateText("Label", box, 28, Color.white, FontStyle.Bold);
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
