using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AirTrafficControl
{
    /// <summary>
    /// Option buttons beside a tapped aircraft (blueprint 15.4, 16.1): Slow / Fast in Draw mode; in Tower mode Land on
    /// runway A/B/C, Go around, Hold, Slow, Fast in the air and Taxi, Cross, Take off or Taxi to stand on the ground.
    /// Full-width buttons get a row each; half-width ones (Go around and Hold, Slow and Fast) share a row. A button that
    /// cannot be pressed now (a closed runway) is shown dimmed. Only one aircraft's buttons are open at a time; they follow
    /// the aircraft, and close after a few real seconds or on a tap elsewhere. The canvas is sized in physical millimetres,
    /// so each button is 9 mm high on every screen: at least 44 points on any iPhone or iPad, and readable on an
    /// iPhone-sized screen. No game logic: it shows the options it is given and reports which one is chosen.
    /// </summary>
    public sealed class CommandMenu : MonoBehaviour
    {
        public const float ButtonHeightMm = 9f;
        public const float ButtonWidthMm = 19f;
        public const float FullWidthMm = 46f;
        public const float GapMm = 1.5f;
        public const float AutoCloseSeconds = 4f;

        static readonly Color ButtonColor = new Color(0.05f, 0.12f, 0.2f, 0.92f);
        static readonly Color ActiveColor = new Color(0.55f, 0.95f, 1f, 0.95f); // pale cyan, not a status colour
        static readonly Color Ink = new Color(0.08f, 0.14f, 0.2f);
        static readonly Color DisabledColor = new Color(0.25f, 0.28f, 0.32f, 0.75f);
        static readonly Color DisabledInk = new Color(0.7f, 0.72f, 0.75f);

        Canvas canvas;
        RectTransform root;
        Font font;
        Camera worldCamera;
        readonly List<RectTransform> buttons = new List<RectTransform>();
        float openedAt;
        float menuWidth;
        float menuHeight;

        public AircraftController Target { get; private set; }
        public bool IsOpen => Target != null;

        /// <summary>Raised when a button is pressed, with the aircraft and the option.</summary>
        public event Action<AircraftController, CommandOption> OptionChosen;

        /// <summary>The options shown now (for tests).</summary>
        public IReadOnlyList<CommandOption> Options => shown;
        readonly List<CommandOption> shown = new List<CommandOption>();

        public void Initialise(Camera camera)
        {
            worldCamera = camera;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasObject = new GameObject("CommandMenu", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5; // above the HUD's top bar, below the end panel (which freezes play anyway)
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPhysicalSize;
            scaler.physicalUnit = CanvasScaler.Unit.Millimeters;
            scaler.fallbackScreenDPI = 132f; // iPad's points per inch, used if the device does not report its DPI
            scaler.defaultSpriteDPI = 132f;

            root = new GameObject("Buttons", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(canvasObject.transform, false);
            root.anchorMin = root.anchorMax = Vector2.zero;
            root.pivot = new Vector2(0f, 1f);
        }

        public void Open(AircraftController aircraft, IReadOnlyList<CommandOption> options)
        {
            Close();
            if (aircraft == null || options.Count == 0)
                return;
            Target = aircraft;
            openedAt = Time.unscaledTime;
            Build(options);
            Follow();
        }

        /// <summary>Shows new options for the same aircraft (a runway closed, a speed changed) without restarting the close timer.</summary>
        public void Refresh(IReadOnlyList<CommandOption> options)
        {
            if (Target == null)
                return;
            ClearButtons();
            Build(options);
            Follow();
        }

        void Build(IReadOnlyList<CommandOption> options)
        {
            shown.Clear();
            shown.AddRange(options);
            bool anyFull = false;
            foreach (CommandOption o in options)
                anyFull |= !o.Half;
            float full = anyFull ? FullWidthMm : 2f * ButtonWidthMm + GapMm;
            float half = (full - GapMm) / 2f;
            float y = 0f;
            int inRow = 0;
            for (int i = 0; i < options.Count; i++)
            {
                CommandOption option = options[i];
                if (!option.Half || inRow == 2)
                {
                    if (inRow > 0)
                        y -= ButtonHeightMm + GapMm;
                    inRow = 0;
                }
                float x = option.Half ? inRow * (half + GapMm) : 0f;
                buttons.Add(CreateButton(option, new Vector2(x, y), option.Half ? half : full));
                if (option.Half)
                {
                    inRow++;
                    if (inRow == 2 && i + 1 < options.Count)
                    {
                        y -= ButtonHeightMm + GapMm;
                        inRow = 0;
                    }
                }
                else
                {
                    y -= ButtonHeightMm + GapMm;
                }
            }
            if (inRow > 0)
                y -= ButtonHeightMm + GapMm;
            menuWidth = full;
            menuHeight = -y - GapMm;
        }

        public void Close()
        {
            ClearButtons();
            shown.Clear();
            Target = null;
        }

        void ClearButtons()
        {
            foreach (RectTransform button in buttons)
                if (button != null)
                    Destroy(button.gameObject);
            buttons.Clear();
        }

        /// <summary>True if the screen point is on one of the buttons, so the tap is not passed to the game.</summary>
        public bool IsOverButton(Vector2 screenPoint)
        {
            foreach (RectTransform button in buttons)
                if (button != null && RectTransformUtility.RectangleContainsScreenPoint(button, screenPoint, null))
                    return true;
            return false;
        }

        RectTransform CreateButton(CommandOption option, Vector2 position, float width)
        {
            var go = new GameObject(option.Label, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(root, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, ButtonHeightMm);
            rect.anchoredPosition = position;
            go.GetComponent<Image>().color = !option.Enabled ? DisabledColor : option.Active ? ActiveColor : ButtonColor;
            var button = go.GetComponent<Button>();
            button.interactable = option.Enabled;
            if (option.Enabled)
                button.onClick.AddListener(() => Choose(option));
            // The label is laid out at ten times the size and scaled down, so its font size can be set in tenths of a
            // millimetre: one line, 4 mm (about 11 pt) when it fits, a little smaller for a long label.
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.rectTransform.SetParent(rect, false);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            label.rectTransform.sizeDelta = new Vector2((width - 2f) * LabelScale, ButtonHeightMm * LabelScale);
            label.rectTransform.localScale = Vector3.one / LabelScale;
            label.font = font;
            label.fontSize = Mathf.RoundToInt(LabelFontMm(option.Label, width - 2f) * LabelScale);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = !option.Enabled ? DisabledInk : option.Active ? Ink : Color.white;
            label.text = option.Label;
            label.raycastTarget = false;
            return rect;
        }

        const float LabelScale = 10f;

        /// <summary>Font size in millimetres for a one-line label: 4 mm, smaller (down to 3 mm) when it would not fit.</summary>
        public static float LabelFontMm(string text, float widthMm)
        {
            float perCharacter = 0.6f; // average width of a bold character, in font sizes
            float fit = widthMm / Mathf.Max(1, text.Length) / perCharacter;
            return Mathf.Clamp(fit, 3f, 4f);
        }

        void Choose(CommandOption option)
        {
            AircraftController target = Target;
            Close();
            if (target != null)
                OptionChosen?.Invoke(target, option);
        }

        /// <summary>Presses a shown option as if tapped (for tests).</summary>
        public void Press(int index)
        {
            if (index >= 0 && index < shown.Count && shown[index].Enabled)
                Choose(shown[index]);
        }

        void Update()
        {
            if (!IsOpen)
                return;
            if (Target == null || Time.unscaledTime - openedAt >= AutoCloseSeconds)
            {
                Close();
                return;
            }
            Follow();
        }

        // Beside the aircraft, to its right (to its left near the right edge), kept on screen.
        void Follow()
        {
            if (Target == null || worldCamera == null)
                return;
            Vector3 screen = worldCamera.WorldToScreenPoint(Target.transform.position);
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, uiCamera, out Vector2 local);
            Vector2 size = canvasRect.rect.size;
            float offset = 8f; // mm from the aircraft centre
            float x = local.x + size.x / 2f + offset;
            if (x + menuWidth > size.x - 2f)
                x = local.x + size.x / 2f - offset - menuWidth;
            x = Mathf.Clamp(x, 2f, Mathf.Max(2f, size.x - menuWidth - 2f));
            // Top edge of the menu, centred on the aircraft, kept between the screen's top and bottom.
            float top = local.y + size.y / 2f + menuHeight / 2f;
            top = Mathf.Clamp(top, Mathf.Min(menuHeight + 2f, size.y - 2f), size.y - 2f);
            root.anchoredPosition = new Vector2(x, top);
        }
    }
}
