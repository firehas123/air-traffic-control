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
        GameObject hintBox;
        Text hintText;
        GameObject endPanel;
        Text endTitle;
        Text endDetail;
        Text primaryLabel;
        bool primaryIsNextLevel;
        GameObject playAgainButton;
        readonly Image[] endStars = new Image[3];
        static readonly Color StarEarned = new Color(1f, 0.78f, 0.1f);
        static readonly Color StarMissing = new Color(0.78f, 0.8f, 0.82f);

        /// <summary>Raised when the player taps Retry or Play again.</summary>
        public event Action RetryPressed;

        /// <summary>Raised when the player taps Next level on the result screen.</summary>
        public event Action NextLevelPressed;

        /// <summary>Raised when the player taps Levels (during play or on an end panel).</summary>
        public event Action LevelsPressed;

        /// <summary>Raised when the owner taps the Zones debug button (outlines the level data on the map).</summary>
        public event Action ZonesPressed;

        /// <summary>Raised when the player taps the Speed button.</summary>
        public event Action SpeedPressed;

        /// <summary>Raised when the player taps the Wind button.</summary>
        public event Action WindPressed;

        /// <summary>Raised when the player taps the Slow draw button.</summary>
        public event Action SlowDrawPressed;


        /// <summary>Raised when the player taps "?" to see the first-time help again.</summary>
        public event Action HelpPressed;

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

            // Top left: the level goal and timer, and a Levels button below them.
            landedText = CreateText("Landed", TopLeftPill("LandedPill", root, 24f, 340f), 28, Color.white, FontStyle.Bold);
            timeText = CreateText("Time", TopLeftPill("TimePill", root, 376f, 100f), 30, Color.white, FontStyle.Bold);

            RectTransform levels = CreateBox("LevelsButton", root, PillColor);
            levels.anchorMin = levels.anchorMax = levels.pivot = new Vector2(0f, 1f);
            levels.anchoredPosition = new Vector2(24f, -100f);
            levels.sizeDelta = new Vector2(140f, 56f);
            var levelsButton = levels.gameObject.AddComponent<Button>();
            levelsButton.targetGraphic = levels.GetComponent<Image>();
            levelsButton.onClick.AddListener(() => LevelsPressed?.Invoke());
            touchBlockers.Add(levels);
            CreateText("Label", levels, 26, Color.white, FontStyle.Bold).text = "Levels";

            // Debug: Zones outlines every zone, gate and route on the painted map.
            RectTransform zones = CreateBox("ZonesButton", root, PillColor);
            zones.anchorMin = zones.anchorMax = zones.pivot = new Vector2(0f, 1f);
            zones.anchoredPosition = new Vector2(176f, -100f);
            zones.sizeDelta = new Vector2(140f, 56f);
            var zonesButton = zones.gameObject.AddComponent<Button>();
            zonesButton.targetGraphic = zones.GetComponent<Image>();
            zonesButton.onClick.AddListener(() => ZonesPressed?.Invoke());
            touchBlockers.Add(zones);
            CreateText("Label", zones, 26, Color.white, FontStyle.Bold).text = "Zones";

            // Top right: Speed, Wind, Slow draw.
            speedLabel = CreateTopRightButton("SpeedButton", root, 0, () => SpeedPressed?.Invoke(), out _);
            windLabel = CreateTopRightButton("WindButton", root, 1, () => WindPressed?.Invoke(), out windButton);
            slowDrawLabel = CreateTopRightButton("SlowDrawButton", root, 2, () => SlowDrawPressed?.Invoke(), out _);

            // "?" replays the first-time help, top right after the other buttons.
            RectTransform helpButton = CreateBox("HelpButton", root, PillColor);
            helpButton.anchorMin = helpButton.anchorMax = helpButton.pivot = new Vector2(1f, 1f);
            helpButton.anchoredPosition = new Vector2(-24f - 3 * TopRightSlot, -24f);
            helpButton.sizeDelta = new Vector2(64f, 64f);
            var help = helpButton.gameObject.AddComponent<Button>();
            help.targetGraphic = helpButton.GetComponent<Image>();
            help.onClick.AddListener(() => HelpPressed?.Invoke());
            touchBlockers.Add(helpButton);
            CreateText("Label", helpButton, 34, Color.white, FontStyle.Bold).text = "?";

            // First-time help caption, top centre below the countdowns (the bottom is the carrier's sea lane).
            RectTransform hint = CreateBox("Hint", root, PillColor);
            hint.anchorMin = hint.anchorMax = hint.pivot = new Vector2(0.5f, 1f);
            hint.anchoredPosition = new Vector2(0f, -262f);
            hint.sizeDelta = new Vector2(820f, 72f);
            hintText = CreateText("Text", hint, 34, Color.white, FontStyle.Bold);
            hintBox = hint.gameObject;
            hintBox.SetActive(false);

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
            panel.sizeDelta = new Vector2(760f, 520f);
            endPanel = panel.gameObject;
            touchBlockers.Add(panel); // the whole panel, so a tap beside the button cannot grab an aircraft

            endTitle = CreateText("Title", panel, 48, AlertRed, FontStyle.Bold);
            Place(endTitle.rectTransform, new Vector2(0f, 200f), new Vector2(720f, 70f));

            // Stars (result screen only).
            for (int i = 0; i < 3; i++)
            {
                var starObject = new GameObject("Star" + i, typeof(RectTransform), typeof(Image));
                starObject.transform.SetParent(panel, false);
                Image star = starObject.GetComponent<Image>();
                star.sprite = StarSprite.Get();
                star.raycastTarget = false;
                Place(star.rectTransform, new Vector2((i - 1) * 90f, 125f), new Vector2(78f, 78f));
                endStars[i] = star;
            }

            endDetail = CreateText("Detail", panel, 27, Ink, FontStyle.Normal);
            endDetail.lineSpacing = 1.15f;
            endDetail.resizeTextForBestFit = true; // three lines (goal, coins, XP) always fit
            endDetail.resizeTextMinSize = 18;
            endDetail.resizeTextMaxSize = 27;
            Place(endDetail.rectTransform, new Vector2(0f, 25f), new Vector2(720f, 110f));

            // One large button onward (C10), and two smaller ones: Play again and Levels.
            RectTransform buttonBox = CreateBox("PrimaryButton", panel, ButtonColor);
            Place(buttonBox, new Vector2(0f, -120f), new Vector2(340f, 100f));
            var button = buttonBox.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonBox.GetComponent<Image>();
            button.onClick.AddListener(() =>
            {
                if (primaryIsNextLevel) NextLevelPressed?.Invoke();
                else RetryPressed?.Invoke();
            });
            primaryLabel = CreateText("Label", buttonBox, 42, Ink, FontStyle.Bold);

            playAgainButton = CreateSecondaryButton(panel, "Play again", new Vector2(-120f, -205f), () => RetryPressed?.Invoke());
            CreateSecondaryButton(panel, "Levels", new Vector2(120f, -205f), () => LevelsPressed?.Invoke());

            endPanel.SetActive(false);
            SetLanded(0, 0);
            SetTime(0f);
        }

        /// <summary>"Landed N of M", or "Landed N" when the level has no landing target.</summary>
        public void SetLanded(int count, int target) => SetGoal(count, target, 0, 0);

        /// <summary>"Landed 2 of 6 · Launched 1 of 3"; the launched part only when the level has departures.</summary>
        public void SetGoal(int landed, int landingTarget, int departed, int departureTarget)
        {
            string landing = landingTarget > 0 ? $"Landed {landed} of {landingTarget}" : $"Landed {landed}";
            landedText.text = departureTarget > 0 ? $"{landing} · Launched {departed} of {departureTarget}" : landing;
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

        /// <summary>Shows one line of first-time help at the bottom, or hides it when <paramref name="text"/> is null.</summary>
        public void SetHint(string text)
        {
            bool show = !string.IsNullOrEmpty(text);
            if (hintBox.activeSelf != show)
                hintBox.SetActive(show);
            if (show && hintText.text != text)
                hintText.text = text;
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

        public void ShowCollision(LevelOutcome outcome)
        {
            ShowEnd("Two aircraft collided", AlertRed,
                    $"after {LevelSession.FormatTime(outcome.Seconds)}. You had {GoalText(outcome)}.", "Retry", false, false);
            foreach (Image star in endStars)
                star.gameObject.SetActive(false);
        }

        /// <summary>
        /// The level result screen (C10): stars, landed and launched, time, closest pass and coins, with one large
        /// button onward. Next level when there is one, otherwise Play again.
        /// </summary>
        public void ShowCleared(LevelOutcome outcome, LevelRewards rewards, int totalCoins, bool hasNextLevel)
        {
            string best = rewards.NewBestTime ? "  (best)" : "";
            string stars = rewards.NewStars > 0 ? $" ({rewards.NewStars} new star{(rewards.NewStars == 1 ? "" : "s")})" : "";
            ShowEnd("Level cleared", SuccessGreen,
                    $"{Capitalise(GoalText(outcome))} in {LevelSession.FormatTime(outcome.Seconds)}{best}\n" +
                    $"{LevelResult.ClosestPassText(outcome.ClosestGap)}   ·   +{rewards.CoinsEarned} coins{stars}   ·   {totalCoins} total\n" +
                    $"+{rewards.XpEarned} XP{(outcome.Warnings == 0 ? " (with the no-warning bonus)" : "")}",
                    hasNextLevel ? "Next level" : "Play again", hasNextLevel, hasNextLevel);
            for (int i = 0; i < endStars.Length; i++)
            {
                endStars[i].gameObject.SetActive(true);
                endStars[i].color = i < outcome.Stars ? StarEarned : StarMissing;
            }
        }

        static string GoalText(LevelOutcome o)
        {
            string landed = o.LandingTarget > 0 ? $"landed {o.Landed} of {o.LandingTarget}" : $"landed {o.Landed}";
            return o.DepartureTarget > 0 ? $"{landed} and launched {o.Departed} of {o.DepartureTarget}" : landed;
        }

        static string Capitalise(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);

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

        void ShowEnd(string title, Color titleColor, string detail, string primary, bool primaryIsNext, bool showPlayAgain)
        {
            endTitle.text = title;
            endTitle.color = titleColor;
            endDetail.text = detail;
            primaryLabel.text = primary;
            primaryIsNextLevel = primaryIsNext;
            playAgainButton.SetActive(showPlayAgain);
            endPanel.SetActive(true);
        }

        GameObject CreateSecondaryButton(RectTransform parent, string label, Vector2 position, Action onClick)
        {
            RectTransform box = CreateBox(label + "Button", parent, new Color(0.87f, 0.91f, 0.91f));
            Place(box, position, new Vector2(210f, 64f));
            var button = box.gameObject.AddComponent<Button>();
            button.targetGraphic = box.GetComponent<Image>();
            button.onClick.AddListener(() => onClick());
            CreateText("Label", box, 28, Ink, FontStyle.Bold).text = label;
            return box.gameObject;
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
            box.anchoredPosition = new Vector2(-24f - slot * TopRightSlot, -24f);
            box.sizeDelta = new Vector2(TopRightSlot - 12f, 64f);
            var button = box.gameObject.AddComponent<Button>();
            button.targetGraphic = box.GetComponent<Image>();
            button.onClick.AddListener(() => onClick());
            touchBlockers.Add(box);
            buttonObject = box.gameObject;
            return CreateText("Label", box, 24, Color.white, FontStyle.Bold);
        }

        const float TopRightSlot = 192f;

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
