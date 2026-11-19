using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>A five-pointed star drawn into a texture in code (no art yet), for the result screen.</summary>
    public static class StarSprite
    {
        static Sprite cached;

        public static Sprite Get()
        {
            if (cached != null)
                return cached;

            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var points = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float angle = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float radius = i % 2 == 0 ? 0.48f : 0.2f;
                points[i] = new Vector2(0.5f + radius * Mathf.Cos(angle), 0.5f + radius * Mathf.Sin(angle)) * size;
            }

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 2x2 samples per pixel for a softer edge.
                    int inside = 0;
                    for (int sy = 0; sy < 2; sy++)
                        for (int sx = 0; sx < 2; sx++)
                            if (Inside(points, new Vector2(x + 0.25f + 0.5f * sx, y + 0.25f + 0.5f * sy))) inside++;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(inside * 255 / 4));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            cached = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return cached;
        }

        // Even-odd point-in-polygon test.
        static bool Inside(Vector2[] polygon, Vector2 point)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                if ((polygon[i].y > point.y) != (polygon[j].y > point.y) &&
                    point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x)
                    inside = !inside;
            }
            return inside;
        }
    }
}
