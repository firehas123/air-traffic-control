using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AirTrafficControl
{
    /// <summary>
    /// Level select for one map and mode (M3 step 1, blueprint 16.3): five tiles with the level number and its stars;
    /// locked levels show a padlock and cannot be chosen. A Home button goes back to the map list. Placeholder UI built
    /// in code, following the screen mockups. No game logic: it shows what the game flow tells it.
    /// </summary>
    public sealed class LevelSelectView : MonoBehaviour
    {
        static readonly Color Background = new Color(0.12f, 0.36f, 0.48f, 1f);
        static readonly Color TileOpen = new Color(0.97f, 0.97f, 0.95f);
        static readonly Color TileLocked = new Color(0.55f, 0.62f, 0.66f);
        static readonly Color Ink = new Color(0.08f, 0.14f, 0.2f);
        static readonly Color StarEarned = new Color(1f, 0.78f, 0.1f);
        static readonly Color StarMissing = new Color(0.78f, 0.8f, 0.82f);

        Font font;
        GameObject canvasObject;
        Text coinsText;
        Text titleText;
        readonly List<GameObject> tiles = new List<GameObject>();

        /// <summary>Raised with the level index when the player taps an unlocked level.</summary>
        public event Action<int> LevelChosen;

        /// <summary>Raised when the player taps Home.</summary>
        public event Action HomePressed;

        public void Initialise()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            canvasObject = new GameObject("LevelSelect", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1366f, 1024f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform root = Box("Background", canvasObject.transform, Background);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;

            titleText = Label("Title", root, 64, Color.white, FontStyle.Bold);
            Anchor(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(1000f, 90f));

            RectTransform home = Box("HomeButton", root, new Color(0.08f, 0.14f, 0.2f, 0.85f));
            Anchor(home, new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(160f, 64f));
            var homeButton = home.gameObject.AddComponent<Button>();
            homeButton.targetGraphic = home.GetComponent<Image>();
            homeButton.onClick.AddListener(() => HomePressed?.Invoke());
            Text homeLabel = Label("Label", home, 30, Color.white, FontStyle.Bold);
            homeLabel.text = "Home";
            homeLabel.rectTransform.anchorMin = Vector2.zero;
            homeLabel.rectTransform.anchorMax = Vector2.one;
            homeLabel.rectTransform.offsetMin = homeLabel.rectTransform.offsetMax = Vector2.zero;

            coinsText = Label("Coins", root, 34, Color.white, FontStyle.Bold);
            Anchor(coinsText.rectTransform, new Vector2(1f, 1f), new Vector2(-170f, -60f), new Vector2(300f, 60f));

            Text credit = Label("Credit", root, 24, new Color(1f, 1f, 1f, 0.75f), FontStyle.Normal);
            credit.text = "Developed by MHC Studio";
            Anchor(credit.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(800f, 40f));
        }

        public bool IsShown => canvasObject != null && canvasObject.activeSelf;

        /// <summary>Shows one map's levels in one mode with their stars and locks, and the coin total.</summary>
        public void Show(Progression progression, int mapIndex, string mapName, GameMode mode, IReadOnlyList<LevelEntry> levels)
        {
            canvasObject.SetActive(true);
            titleText.text = $"{mapName} · {mode}";
            coinsText.text = $"Coins {progression.Coins}";

            foreach (GameObject tile in tiles)
                Destroy(tile);
            tiles.Clear();

            RectTransform root = canvasObject.transform.GetChild(0) as RectTransform;
            const int perRow = 5;
            const float size = 190f;
            const float gap = 36f;
            for (int i = 0; i < levels.Count; i++)
            {
                int row = i / perRow;
                int column = i % perRow;
                float x = (column - (perRow - 1) / 2f) * (size + gap);
                float y = 90f - row * (size + gap + 20f);
                tiles.Add(CreateTile(root, i, progression.IsLevelUnlocked(mapIndex, mode, i), progression.StarsFor(levels[i].id), new Vector2(x, y), size));
            }
        }

        public void Hide()
        {
            if (canvasObject != null)
                canvasObject.SetActive(false);
        }

        GameObject CreateTile(RectTransform parent, int index, bool unlocked, int stars, Vector2 position, float size)
        {
            RectTransform tile = Box($"Level{index + 1}", parent, unlocked ? TileOpen : TileLocked);
            Anchor(tile, new Vector2(0.5f, 0.5f), position, new Vector2(size, size));

            if (unlocked)
            {
                var button = tile.gameObject.AddComponent<Button>();
                button.targetGraphic = tile.GetComponent<Image>();
                int chosen = index;
                button.onClick.AddListener(() => LevelChosen?.Invoke(chosen));

                Text number = Label("Number", tile, 76, Ink, FontStyle.Bold);
                number.text = (index + 1).ToString();
                Anchor(number.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 22f), new Vector2(size, 100f));

                for (int s = 0; s < 3; s++)
                {
                    var starObject = new GameObject("Star" + s, typeof(RectTransform), typeof(Image));
                    starObject.transform.SetParent(tile, false);
                    Image star = starObject.GetComponent<Image>();
                    star.sprite = StarSprite.Get();
                    star.color = s < stars ? StarEarned : StarMissing;
                    star.raycastTarget = false;
                    Anchor(star.rectTransform, new Vector2(0.5f, 0.5f), new Vector2((s - 1) * 50f, -55f), new Vector2(44f, 44f));
                }
            }
            else
            {
                var lockObject = new GameObject("Lock", typeof(RectTransform), typeof(Image));
                lockObject.transform.SetParent(tile, false);
                Image padlock = lockObject.GetComponent<Image>();
                padlock.sprite = LockSprite.Get();
                padlock.color = new Color(0.2f, 0.25f, 0.3f);
                padlock.raycastTarget = false;
                Anchor(padlock.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90f, 90f));
            }
            return tile.gameObject;
        }

        static RectTransform Box(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go.GetComponent<RectTransform>();
        }

        Text Label(string name, Transform parent, int size, Color color, FontStyle style)
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
            return text;
        }

        static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }

    /// <summary>A padlock drawn into a texture in code (no art yet), for locked levels.</summary>
    public static class LockSprite
    {
        static Sprite cached;

        public static Sprite Get()
        {
            if (cached != null)
                return cached;
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = (x + 0.5f) / size;
                    float py = (y + 0.5f) / size;
                    bool body = px > 0.22f && px < 0.78f && py > 0.12f && py < 0.55f;
                    float dx = px - 0.5f;
                    float dy = py - 0.55f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    bool shackle = dy >= 0f && r > 0.17f && r < 0.25f;
                    bool shackleLegs = py > 0.48f && py <= 0.55f && Mathf.Abs(Mathf.Abs(dx) - 0.21f) < 0.04f;
                    bool keyhole = Mathf.Sqrt(dx * dx + (py - 0.36f) * (py - 0.36f)) < 0.05f;
                    bool on = (body && !keyhole) || shackle || shackleLegs;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(on ? 255 : 0));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            cached = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return cached;
        }
    }
}
