using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// Frames a painted map (owner request, 20 Nov 2026): on the 12.9" iPad in landscape (4:3) the island fills the
    /// screen; on wider screens the picture is cropped at its top and bottom (sea), never the airport. The view never
    /// shows past the picture's edges unless the airport could not fit otherwise. Plain C#, so it can be unit tested.
    /// </summary>
    public static class CameraFraming
    {
        /// <summary>The camera's view (centre and height) for a screen of <paramref name="aspect"/> (width / height).</summary>
        public static Rect View(Rect picture, Rect keepVisible, float aspect)
        {
            aspect = Mathf.Max(0.1f, aspect);

            // Largest view of this shape that stays inside the picture.
            float height = Mathf.Min(picture.height, picture.width / aspect);
            float width = height * aspect;

            // Never crop the airport: grow the view if it would not fit (only on extreme screen shapes).
            if (width < keepVisible.width)
            {
                width = keepVisible.width;
                height = width / aspect;
            }
            if (height < keepVisible.height)
            {
                height = keepVisible.height;
                width = height * aspect;
            }

            // Centre on the airport, then slide inside the picture where there is room.
            Vector2 centre = keepVisible.center;
            centre.x = ClampCentre(centre.x, width, picture.xMin, picture.xMax, keepVisible.xMin, keepVisible.xMax);
            centre.y = ClampCentre(centre.y, height, picture.yMin, picture.yMax, keepVisible.yMin, keepVisible.yMax);
            return new Rect(centre.x - width / 2f, centre.y - height / 2f, width, height);
        }

        // Keeps the view inside [min, max] when it fits, and always over [keepMin, keepMax].
        static float ClampCentre(float centre, float size, float min, float max, float keepMin, float keepMax)
        {
            float half = size / 2f;
            if (size <= max - min)
                centre = Mathf.Clamp(centre, min + half, max - half);
            else
                centre = (min + max) / 2f;
            // The airport always wins.
            centre = Mathf.Clamp(centre, keepMax - half, keepMin + half);
            return centre;
        }

        public static void Apply(Camera camera, Rect view)
        {
            camera.orthographic = true;
            camera.orthographicSize = view.height / 2f;
            Vector3 position = camera.transform.position;
            camera.transform.position = new Vector3(view.center.x, view.center.y, position.z);
        }

        /// <summary>The camera's current view in world units.</summary>
        public static Rect CurrentView(Camera camera)
        {
            float height = camera.orthographicSize * 2f;
            float width = height * camera.aspect;
            Vector2 centre = camera.transform.position;
            return new Rect(centre.x - width / 2f, centre.y - height / 2f, width, height);
        }

        /// <summary>A picture pixel (x right, y down from the top) to world units, for a picture centred on the origin.</summary>
        public static Vector2 PixelToWorld(Vector2 pixel, Vector2 pictureSize, float pixelsPerUnit) =>
            new Vector2(pixel.x - pictureSize.x / 2f, pictureSize.y / 2f - pixel.y) / pixelsPerUnit;

        public static Vector2 WorldToPixel(Vector2 world, Vector2 pictureSize, float pixelsPerUnit) =>
            new Vector2(world.x * pixelsPerUnit + pictureSize.x / 2f, pictureSize.y / 2f - world.y * pixelsPerUnit);
    }
}
