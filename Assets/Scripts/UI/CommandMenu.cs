using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AirTrafficControl
{
    /// <summary>
    /// Option buttons beside a tapped aircraft (blueprint 15.4): Slow / Fast in the air; Taxi, Cross or Take off on the
    /// ground. Only one aircraft's buttons are open at a time; they follow the aircraft, and close after 3 real seconds
    /// or on a tap elsewhere. The canvas is sized in physical millimetres, so each button is 9 mm high on every screen:
    /// at least 44 points on any iPhone or iPad, and readable on an iPhone-sized screen. No game logic: it shows the
    /// commands it is given and reports which one is chosen.
    /// </summary>
    public sealed class CommandMenu : MonoBehaviour
    {
        public const float ButtonHeightMm = 9f;
        public const float ButtonWidthMm = 19f;
        public const float AutoCloseSeconds = 3f;

        static readonly Color ButtonColor = new Color(0.05f, 0.12f, 0.2f, 0.92f);
        static readonly Color ActiveColor = new Color(0.55f, 0.95f, 1f, 0.95f); // pale cyan, not a status colour
        static readonly Color Ink = new Color(0.08f, 0.14f, 0.2f);

        Canvas canvas;
        RectTransform root;
        Font font;
        Camera worldCamera;
        readonly List<RectTransform> buttons = new List<RectTransform>();
        float openedAt;

        public AircraftController Target { get; private set; }
        public bool IsOpen => Target != null;

        /// <summary>Raised when a button is pressed, with the aircraft and the command.</summary>
        public event Action<AircraftController, AircraftCommand> CommandChosen;

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
            root.pivot = new Vector2(0f, 0.5f);
        }

        public void Open(AircraftController aircraft, IReadOnlyList<AircraftCommand> commands, AircraftSpeed activeSpeed)
        {
            Close();
            if (aircraft == null || commands.Count == 0)
                return;
            Target = aircraft;
            openedAt = Time.unscaledTime;

            for (int i = 0; i < commands.Count; i++)
            {
                AircraftCommand command = commands[i];
                bool active = (command == AircraftCommand.Slow && activeSpeed == AircraftSpeed.Slow)
                           || (command == AircraftCommand.Fast && activeSpeed == AircraftSpeed.Fast);
                buttons.Add(CreateButton(command, active, i));
            }
            Follow();
        }

        public void Close()
        {
            foreach (RectTransform button in buttons)
                if (button != null)
                    Destroy(button.gameObject);
            buttons.Clear();
            Target = null;
        }

        /// <summary>True if the screen point is on one of the buttons, so the tap is not passed to the game.</summary>
        public bool IsOverButton(Vector2 screenPoint)
        {
            foreach (RectTransform button in buttons)
                if (button != null && RectTransformUtility.RectangleContainsScreenPoint(button, screenPoint, null))
                    return true;
            return false;
        }

        RectTransform CreateButton(AircraftCommand command, bool active, int index)
        {
            var go = new GameObject(command.ToString(), typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(root, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(ButtonWidthMm, ButtonHeightMm);
            rect.anchoredPosition = new Vector2(0f, index * -(ButtonHeightMm + 1.5f));
            go.GetComponent<Image>().color = active ? ActiveColor : ButtonColor;
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(() => Choose(command));

            var label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.rectTransform.SetParent(rect, false);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.font = font;
            label.fontSize = 4; // millimetres: about 11 pt, readable on an iPhone
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = active ? Ink : Color.white;
            label.text = AircraftCommands.Label(command);
            label.raycastTarget = false;
            return rect;
        }

        void Choose(AircraftCommand command)
        {
            AircraftController target = Target;
            Close();
            if (target != null)
                CommandChosen?.Invoke(target, command);
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

        // Beside the aircraft, to its right, kept on screen.
        void Follow()
        {
            if (Target == null || worldCamera == null)
                return;
            Vector3 screen = worldCamera.WorldToScreenPoint(Target.transform.position);
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local);
            Vector2 size = canvasRect.rect.size;
            float offset = 8f; // mm to the right of the aircraft centre
            float height = buttons.Count * (ButtonHeightMm + 1.5f);
            float x = local.x + size.x / 2f + offset;
            if (x + ButtonWidthMm > size.x - 2f)
                x = local.x + size.x / 2f - offset - ButtonWidthMm; // flip to the left near the right edge
            float y = Mathf.Clamp(local.y + size.y / 2f + height / 2f - ButtonHeightMm / 2f, height, size.y - ButtonHeightMm);
            root.anchoredPosition = new Vector2(x, y);
        }
    }
}
