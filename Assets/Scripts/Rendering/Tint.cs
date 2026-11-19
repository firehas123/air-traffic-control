using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>Fades code-drawn art through the shader's _Tint, without rebuilding meshes (used by effects).</summary>
    public static class Tint
    {
        static readonly int TintId = Shader.PropertyToID("_Tint");
        static MaterialPropertyBlock block;

        public static void Set(Renderer renderer, Color tint)
        {
            if (renderer == null)
                return;
            if (block == null)
                block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor(TintId, tint);
            renderer.SetPropertyBlock(block);
        }

        public static void SetAlpha(Renderer renderer, float alpha) => Set(renderer, new Color(1f, 1f, 1f, alpha));
    }
}
