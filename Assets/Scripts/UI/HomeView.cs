using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AirTrafficControl
{
    /// <summary>
    /// The home screen (blueprint 16, 25 Nov 2026): the seven maps in order, a Draw and a Tower button for each open
    /// map, what is still missing for each locked one, the XP bar with the controller rank (C8), coins, and
    /// "Developed by MHC Studio". Placeholder UI built in code. No game logic: it shows what the game flow tells it.
    /// </summary>
    public sealed class HomeView : MonoBehaviour
    {
        static readonly Color Background = new Color(0.12f, 0.36f, 0.48f, 1f);
        static readonly Color Card = new Color(0.97f, 0.97f, 0.95f);
        static readonly Color CardLocked = new Color(0.62f, 0.68f, 0.72f);
        static readonly Color Ink = new Color(0.08f, 0.14f, 0.2f);
        static readonly Color DrawButton = new Color(0.16f, 0.45f, 0.75f);
        static readonly Color TowerButton = new Color(0.36f, 0.3f, 0.62f);
        static readonly Color BarBack = new Color(1f, 1f, 1f, 0.25f);
        static readonly Color BarFill = new Color(1f, 0.78f, 0.1f);

        Font font;
        GameObject canvasObject;
        RectTransform root;
        Text coinsText;
        Text rankText;
        Text xpText;
        RectTransform xpFill;
        GameObject notice;
        Text noticeText;
        float noticeUntil;
        readonly List<GameObject> cards = new List<GameObject>();

        /// <summary>Raised with the map index and mode when the player taps Draw or Tower on an open map.</summary>
        public event Action<int, GameMode> ModeChosen;

        public bool IsShown => canvasObject != null && canvasObject.activeSelf;

        public void Initialise()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            canvasObject = new GameObject("Home", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 11;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1366f, 1024f);
            scaler.matchWidthOrHeight = 0.5f;

            root = UiKit.Box("Background", canvasObject.transform, Background);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;

            Text title = UiKit.Label(font, "Title", root, 56, Color.white, FontStyle.Bold);
            title.text = "Air Traffic Control";
            UiKit.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(900f, 80f));

            // Top left: rank and XP bar. Top right: coins.
            rankText = UiKit.Label(font, "Rank", root, 30, Color.white, FontStyle.Bold);
            rankText.alignment = TextAnchor.MiddleLeft;
            UiKit.Anchor(rankText.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(420f, 44f));
            RectTransform bar = UiKit.Box("XpBar", root, BarBack);
            UiKit.Anchor(bar, new Vector2(0f, 1f), new Vector2(40f, -90f), new Vector2(360f, 18f));
            xpFill = UiKit.Box("Fill", bar, BarFill);
            xpFill.anchorMin = Vector2.zero;
            xpFill.anchorMax = new Vector2(0f, 1f);
            xpFill.pivot = new Vector2(0f, 0.5f);
            xpFill.offsetMin = xpFill.offsetMax = Vector2.zero;
            xpText = UiKit.Label(font, "Xp", root, 22, new Color(1f, 1f, 1f, 0.9f), FontStyle.Normal);
            xpText.alignment = TextAnchor.MiddleLeft;
            UiKit.Anchor(xpText.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -114f), new Vector2(520f, 32f));

            coinsText = UiKit.Label(font, "Coins", root, 34, Color.white, FontStyle.Bold);
            coinsText.alignment = TextAnchor.MiddleRight;
            UiKit.Anchor(coinsText.rectTransform, new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(300f, 50f));

            Text credit = UiKit.Label(font, "Credit", root, 24, new Color(1f, 1f, 1f, 0.75f), FontStyle.Normal);
            credit.text = "Developed by MHC Studio";
            UiKit.Anchor(credit.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(800f, 40f));

            // A short message, for example "Tower mode is coming in the next update".
            RectTransform noticeBox = UiKit.Box("Notice", root, new Color(0.08f, 0.14f, 0.2f, 0.92f));
            UiKit.Anchor(noticeBox, new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(760f, 70f));
            noticeText = UiKit.Label(font, "Text", noticeBox, 28, Color.white, FontStyle.Bold);
            UiKit.Stretch(noticeText.rectTransform);
            notice = noticeBox.gameObject;
            notice.SetActive(false);
        }

        /// <summary>Shows the maps with their locks, and the rank, XP and coins.</summary>
        public void Show(Progression progression, IReadOnlyList<MapEntry> maps)
        {
            canvasObject.SetActive(true);
            notice.SetActive(false);

            int xp = progression.Xp;
            coinsText.text = $"Coins {progression.Coins}";
            rankText.text = ControllerRank.Name(xp);
            xpFill.anchorMax = new Vector2(ControllerRank.ProgressToNext(xp), 1f);
            xpFill.offsetMin = xpFill.offsetMax = Vector2.zero;
            int toNext = ControllerRank.XpToNext(xp);
            xpText.text = toNext > 0
                ? $"{xp} XP  ·  {toNext} to {ControllerRank.Names[ControllerRank.Index(xp) + 1]}"
                : $"{xp} XP  ·  top rank";

            foreach (GameObject card in cards)
                Destroy(card);
            cards.Clear();

            // Seven cards in two rows (four, then three), in unlock order.
            const float width = 300f;
            const float height = 300f;
            const float gap = 24f;
            for (int i = 0; i < maps.Count; i++)
            {
                int row = i < 4 ? 0 : 1;
                int inRow = row == 0 ? Mathf.Min(4, maps.Count) : maps.Count - 4;
                int column = row == 0 ? i : i - 4;
                float x = (column - (inRow - 1) / 2f) * (width + gap);
                float y = 150f - row * (height + gap);
                cards.Add(CreateCard(i, maps[i], progression.MapLockFor(i), new Vector2(x, y), new Vector2(width, height)));
            }
        }

        public void Hide()
        {
            if (canvasObject != null)
                canvasObject.SetActive(false);
        }

        /// <summary>Shows a short message at the bottom of the home screen for a few seconds.</summary>
        public void ShowNotice(string message)
        {
            noticeText.text = message;
            notice.SetActive(true);
            noticeUntil = Time.unscaledTime + 3f;
        }

        void Update()
        {
            if (notice != null && notice.activeSelf && Time.unscaledTime > noticeUntil)
                notice.SetActive(false);
        }

        GameObject CreateCard(int index, MapEntry map, MapLock mapLock, Vector2 position, Vector2 size)
        {
            RectTransform card = UiKit.Box($"Map{index + 1}", root, mapLock.Unlocked ? Card : CardLocked);
            UiKit.Anchor(card, new Vector2(0.5f, 0.5f), position, size);

            // The Draw picture as a thumbnail (3:2), dimmed when locked.
            if (map.drawPicture != null)
            {
                var thumbObject = new GameObject("Picture", typeof(RectTransform), typeof(Image));
                thumbObject.transform.SetParent(card, false);
                Image thumb = thumbObject.GetComponent<Image>();
                thumb.sprite = map.drawPicture;
                thumb.preserveAspect = true;
                thumb.raycastTarget = false;
                thumb.color = mapLock.Unlocked ? Color.white : new Color(0.55f, 0.55f, 0.55f);
                UiKit.Anchor(thumb.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(size.x - 20f, 150f));
            }

            Text name = UiKit.Label(font, "Name", card, 28, Ink, FontStyle.Bold);
            name.text = $"{index + 1}. {map.displayName}";
            UiKit.Anchor(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -172f), new Vector2(size.x - 16f, 40f));

            if (mapLock.Unlocked)
            {
                CreateModeButton(card, "Draw", DrawButton, new Vector2(-73f, 20f), () => ModeChosen?.Invoke(index, GameMode.Draw));
                CreateModeButton(card, "Tower", TowerButton, new Vector2(73f, 20f), () => ModeChosen?.Invoke(index, GameMode.Tower));
            }
            else
            {
                var lockObject = new GameObject("Lock", typeof(RectTransform), typeof(Image));
                lockObject.transform.SetParent(card, false);
                Image padlock = lockObject.GetComponent<Image>();
                padlock.sprite = LockSprite.Get();
                padlock.color = new Color(0.2f, 0.25f, 0.3f);
                padlock.raycastTarget = false;
                UiKit.Anchor(padlock.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(70f, 70f));

                Text missing = UiKit.Label(font, "Missing", card, 22, Ink, FontStyle.Normal);
                missing.text = mapLock.Describe(Progression.LevelThatOpensNextMap);
                UiKit.Anchor(missing.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(size.x - 24f, 72f));
            }
            return card.gameObject;
        }

        void CreateModeButton(RectTransform card, string label, Color color, Vector2 position, Action onClick)
        {
            RectTransform box = UiKit.Box(label + "Button", card, color);
            UiKit.Anchor(box, new Vector2(0.5f, 0f), position, new Vector2(132f, 64f));
            var button = box.gameObject.AddComponent<Button>();
            button.targetGraphic = box.GetComponent<Image>();
            button.onClick.AddListener(() => onClick());
            Text text = UiKit.Label(font, "Label", box, 28, Color.white, FontStyle.Bold);
            text.text = label;
            UiKit.Stretch(text.rectTransform);
        }
    }

    /// <summary>Small helpers for the code-built menus (home screen and level select).</summary>
    public static class UiKit
    {
        public static RectTransform Box(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go.GetComponent<RectTransform>();
        }

        public static Text Label(Font font, string name, Transform parent, int size, Color color, FontStyle style)
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
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(12, size / 2);
            text.resizeTextMaxSize = size;
            return text;
        }

        public static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
