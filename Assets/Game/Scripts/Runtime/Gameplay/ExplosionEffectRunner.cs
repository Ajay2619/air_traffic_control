using System.Collections.Generic;
using UnityEngine;

namespace ClearToLand
{
    /// <summary>Animates the parts created by <see cref="ExplosionEffect.Play"/> (unscaled time, self-destroying).</summary>
    internal sealed class ExplosionEffectRunner : MonoBehaviour
    {
        private const int DebrisCount = 10;
        private const int SmokeCount = 6;

        private sealed class Part
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public float Delay;
            public float Life;
            public float Size0;
            public float Size1;
            public float UnitScale;
            public Color Color0;
            public Color Color1;
            public float Alpha;
            public float FadeStart;
            public Vector2 Velocity;
            public float Drag;
            public float Spin;
            public float Angle;
        }

        private readonly List<Part> _parts = new List<Part>(1 + 1 + DebrisCount + SmokeCount);
        private float _age;
        private float _totalLife;

        /// <summary>Creates all parts. Missing sprites simply render nothing.</summary>
        public void Build(GameConfig config)
        {
            Sprite soft = config != null ? config.softCircleSprite : null;
            Sprite debris = config != null ? config.debrisSprite : null;
            float softUnit = GameplayUtil.ScaleForWidth(soft, 1f);
            float debrisUnit = GameplayUtil.ScaleForWidth(debris, 1f);
            float debrisNativeWidth = debris != null ? GameplayUtil.SpriteWidth(debris) : 0.16f;

            // Smoke (behind everything else).
            for (int i = 0; i < SmokeCount; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                if (dir.sqrMagnitude < 0.5f) dir = Vector2.up;
                float gray = Random.Range(0.28f, 0.38f);
                Add(soft, softUnit, SortOrder.Effects, new Part
                {
                    Delay = Random.Range(0.05f, 0.3f),
                    Life = Random.Range(1.6f, 2.0f),
                    Size0 = Random.Range(0.4f, 0.55f),
                    Size1 = Random.Range(1.1f, 1.5f),
                    Color0 = new Color(gray, gray, gray + 0.02f, 1f),
                    Color1 = new Color(gray + 0.18f, gray + 0.18f, gray + 0.2f, 1f),
                    Alpha = 0.5f,
                    FadeStart = 0.3f,
                    Velocity = dir * Random.Range(0.15f, 0.35f) + new Vector2(0f, 0.12f),
                    Drag = 0.6f,
                    Spin = 0f,
                    Angle = 0f,
                });
            }

            // Fireball.
            Add(soft, softUnit, SortOrder.Effects + 1, new Part
            {
                Delay = 0f,
                Life = 1.0f,
                Size0 = 0.4f,
                Size1 = 1.0f,
                Color0 = new Color(1f, 0.62f, 0.18f, 1f),
                Color1 = new Color(0.45f, 0.07f, 0.04f, 1f),
                Alpha = 1f,
                FadeStart = 0.35f,
            });

            // Debris shards.
            for (int i = 0; i < DebrisCount; i++)
            {
                float ang = (i + Random.Range(-0.35f, 0.35f)) * (360f / DebrisCount);
                Vector2 dir = GameplayUtil.FromAngleDeg(ang);
                float size = debrisNativeWidth * Random.Range(0.8f, 1.5f);
                float shade = Random.Range(0.16f, 0.28f);
                Color c = new Color(shade, shade, shade + 0.02f, 1f);
                Add(debris, debrisUnit, SortOrder.Effects + 2, new Part
                {
                    Delay = 0f,
                    Life = Random.Range(0.95f, 1.2f),
                    Size0 = size,
                    Size1 = size * 0.85f,
                    Color0 = c,
                    Color1 = c,
                    Alpha = 1f,
                    FadeStart = 0.6f,
                    Velocity = dir * Random.Range(1.3f, 2.6f),
                    Drag = 2.4f,
                    Spin = Random.Range(360f, 900f) * (Random.value < 0.5f ? -1f : 1f),
                    Angle = Random.Range(0f, 360f),
                });
            }

            // Flash (on top).
            Add(soft, softUnit, SortOrder.Effects + 3, new Part
            {
                Delay = 0f,
                Life = 0.35f,
                Size0 = 0.35f,
                Size1 = 1.4f,
                Color0 = Color.white,
                Color1 = new Color(1f, 0.58f, 0.15f, 1f),
                Alpha = 1f,
                FadeStart = 0.25f,
            });

            _totalLife = 0f;
            for (int i = 0; i < _parts.Count; i++)
                _totalLife = Mathf.Max(_totalLife, _parts[i].Delay + _parts[i].Life);

            Animate(0f);
        }

        private void Add(Sprite sprite, float unitScale, int sortingOrder, Part part)
        {
            SpriteRenderer sr = GameplayUtil.CreateSprite(transform, "Part", sprite, part.Color0, sortingOrder);
            sr.enabled = false;
            part.Renderer = sr;
            part.Transform = sr.transform;
            part.UnitScale = unitScale;
            part.Transform.localRotation = Quaternion.Euler(0f, 0f, part.Angle);
            _parts.Add(part);
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            _age += dt;
            Animate(dt);
            if (_age >= _totalLife) Destroy(gameObject);
        }

        private void Animate(float dt)
        {
            for (int i = 0; i < _parts.Count; i++)
            {
                Part p = _parts[i];
                if (p.Renderer == null) continue;

                float t = (_age - p.Delay) / p.Life;
                if (t < 0f || t >= 1f)
                {
                    if (p.Renderer.enabled) p.Renderer.enabled = false;
                    continue;
                }
                if (!p.Renderer.enabled && p.Renderer.sprite != null) p.Renderer.enabled = true;

                float size = Mathf.Lerp(p.Size0, p.Size1, GameplayUtil.EaseOutCubic(t)) * p.UnitScale;
                p.Transform.localScale = new Vector3(size, size, 1f);

                if (dt > 0f && p.Velocity.sqrMagnitude > 0f)
                {
                    p.Velocity /= 1f + p.Drag * dt;
                    Vector3 lp = p.Transform.localPosition;
                    p.Transform.localPosition = new Vector3(lp.x + p.Velocity.x * dt, lp.y + p.Velocity.y * dt, 0f);
                }
                if (p.Spin != 0f)
                {
                    p.Angle += p.Spin * dt;
                    p.Transform.localRotation = Quaternion.Euler(0f, 0f, p.Angle);
                }

                float fade = t <= p.FadeStart ? 1f : 1f - (t - p.FadeStart) / Mathf.Max(1e-4f, 1f - p.FadeStart);
                Color c = Color.Lerp(p.Color0, p.Color1, t);
                c.a = p.Alpha * Mathf.Clamp01(fade);
                p.Renderer.color = c;
            }
        }
    }
}
