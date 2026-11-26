using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// The one event effect left (owner, 25 Nov 2026): a short smoke puff where two aircraft collide. It runs on real
    /// time, because a collision freezes game time. The landing sparkle, coin pop and collision screen shake were
    /// removed as distracting. Skipped when animations are off.
    /// </summary>
    public sealed class EffectsSystem : MonoBehaviour
    {
        const int EffectOrder = 12;
        static readonly Color Smoke = new Color(0.35f, 0.37f, 0.4f, 0.7f);

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
        Mesh puffMesh;

        public int ActiveEffects => effects.Count;

        public void Initialise(Camera camera, Material shapeMaterial)
        {
            material = shapeMaterial;
            puffMesh = new MeshBuilder().Ellipse(Vector2.zero, new Vector2(0.22f, 0.22f), 18, Smoke).Build("SmokePuff");
        }

        public void Collision(Vector2 at)
        {
            if (!Animations.On)
                return;
            AnimationSettings a = Animations.Settings;
            for (int i = 0; i < a.smokePuffs; i++)
            {
                float angle = i * 2f * Mathf.PI / Mathf.Max(1, a.smokePuffs) + Random.value * 0.5f;
                var drift = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (0.2f + Random.value * 0.2f);
                Spawn(puffMesh, at + drift * 0.2f, drift, 0.6f, 1.4f, a.smokeSeconds);
            }
        }

        void Spawn(Mesh mesh, Vector2 at, Vector2 drift, float startScale, float endScale, float duration)
        {
            GameObject go = MeshBuilder.CreateObject("Effect", mesh, material, EffectOrder, at, 0f);
            go.transform.SetParent(transform, true);
            go.transform.localScale = Vector3.one * startScale;
            effects.Add(new Effect
            {
                transform = go.transform, renderer = go.GetComponent<Renderer>(), start = at, drift = drift,
                startScale = startScale, endScale = endScale, duration = Mathf.Max(0.05f, duration), born = Time.unscaledTime,
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
        }
    }
}
