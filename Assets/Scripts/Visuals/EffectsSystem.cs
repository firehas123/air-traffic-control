using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// Short event effects (blueprint 15.3): smoke and a short screen shake on a collision, a small sparkle and a coin
    /// pop on each landing. They run on real time, because a collision freezes game time. Skipped when animations are
    /// off. Only a handful exist at once, so they cost little.
    /// </summary>
    public sealed class EffectsSystem : MonoBehaviour
    {
        const int EffectOrder = 12;
        static readonly Color Smoke = new Color(0.35f, 0.37f, 0.4f, 0.7f);
        static readonly Color Coin = new Color(0.98f, 0.72f, 0.2f, 1f);
        static readonly Color CoinEdge = new Color(0.85f, 0.55f, 0.12f, 1f);

        struct Effect
        {
            public Transform transform;
            public Renderer renderer;
            public Vector2 start;
            public Vector2 drift;
            public float startScale;
            public float endScale;
            public float duration;
            public float born;
        }

        readonly List<Effect> effects = new List<Effect>();
        Material material;
        Camera worldCamera;
        Vector3 cameraHome;
        float shakeStarted = -10f;
        bool shaking;
        Mesh puffMesh;
        Mesh coinMesh;
        Mesh sparkleMesh;

        public void Initialise(Camera camera, Material shapeMaterial)
        {
            worldCamera = camera;
            cameraHome = camera.transform.position;
            material = shapeMaterial;
            puffMesh = new MeshBuilder().Ellipse(Vector2.zero, new Vector2(0.22f, 0.22f), 18, Smoke).Build("SmokePuff");
            coinMesh = new MeshBuilder()
                .Ellipse(Vector2.zero, new Vector2(0.11f, 0.11f), 18, CoinEdge)
                .Ellipse(Vector2.zero, new Vector2(0.085f, 0.085f), 18, Coin)
                .Build("Coin");
            var sparkle = new MeshBuilder();
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4f;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                sparkle.Bar(dir * 0.12f, dir * (i % 2 == 0 ? 0.32f : 0.24f), 0.03f, Color.white);
            }
            sparkleMesh = sparkle.Build("Sparkle");
        }

        public void Collision(Vector2 at)
        {
            if (!Animations.On)
                return;
            AnimationSettings a = Animations.Settings;
            for (int i = 0; i < a.smokePuffs; i++)
            {
                float angle = i * 2f * Mathf.PI / Mathf.Max(1, a.smokePuffs) + Random.value * 0.5f;
                var drift = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (0.25f + Random.value * 0.35f);
                Spawn(puffMesh, at + drift * 0.2f, drift, 0.6f, 1.8f + Random.value, a.smokeSeconds);
            }
            shakeStarted = Time.unscaledTime;
            shaking = true;
        }

        public void Landing(Vector2 at)
        {
            if (!Animations.On)
                return;
            AnimationSettings a = Animations.Settings;
            Spawn(sparkleMesh, at, Vector2.zero, 0.4f, 1.3f, 0.45f);
            Spawn(coinMesh, at, new Vector2(0f, a.coinPopHeight / Mathf.Max(0.1f, a.coinPopSeconds)), 1f, 1f, a.coinPopSeconds);
        }

        void Spawn(Mesh mesh, Vector2 at, Vector2 drift, float startScale, float endScale, float duration)
        {
            GameObject go = MeshBuilder.CreateObject("Effect", mesh, material, EffectOrder, at, 0f);
            go.transform.SetParent(transform, true);
            go.transform.localScale = Vector3.one * startScale;
            effects.Add(new Effect
            {
                transform = go.transform, renderer = go.GetComponent<Renderer>(), start = at, drift = drift,
                startScale = startScale, endScale = endScale, duration = duration, born = Time.unscaledTime,
            });
        }

        void Update()
        {
            float now = Time.unscaledTime;
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                Effect e = effects[i];
                float t = (now - e.born) / e.duration;
                if (t >= 1f || e.transform == null)
                {
                    if (e.transform != null)
                        Destroy(e.transform.gameObject);
                    effects.RemoveAt(i);
                    continue;
                }
                float eased = 1f - (1f - t) * (1f - t);
                e.transform.position = e.start + e.drift * e.duration * eased;
                e.transform.localScale = Vector3.one * Mathf.Lerp(e.startScale, e.endScale, eased);
                Tint.SetAlpha(e.renderer, 1f - t);
            }

            if (shaking)
            {
                AnimationSettings a = Animations.Settings;
                float strength = AnimationMath.ShakeStrength(now - shakeStarted, a.shakeSeconds, a.shakeStrength);
                if (strength <= 0f || !Animations.On)
                {
                    shaking = false;
                    worldCamera.transform.position = cameraHome;
                }
                else
                {
                    worldCamera.transform.position = cameraHome + (Vector3)(Random.insideUnitCircle * strength);
                }
            }
        }

        void OnDestroy()
        {
            if (worldCamera != null)
                worldCamera.transform.position = cameraHome;
        }
    }
}
