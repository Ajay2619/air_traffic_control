using UnityEngine;

namespace ClearToLand
{
    /// <summary>Small allocation-free helpers shared by the gameplay scripts (sprites, easing, angles).</summary>
    internal static class GameplayUtil
    {
        /// <summary>Creates a child GameObject with a SpriteRenderer. A null sprite is allowed (renders nothing).</summary>
        public static SpriteRenderer CreateSprite(Transform parent, string name, Sprite sprite, Color color, int sortingOrder)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return sr;
        }

        /// <summary>World width of a sprite at scale 1 (0 when null).</summary>
        public static float SpriteWidth(Sprite sprite)
        {
            return sprite != null ? sprite.bounds.size.x : 0f;
        }

        /// <summary>Uniform scale that makes <paramref name="sprite"/> <paramref name="worldWidth"/> units wide (1 when null).</summary>
        public static float ScaleForWidth(Sprite sprite, float worldWidth)
        {
            float w = SpriteWidth(sprite);
            return w > 1e-5f ? worldWidth / w : 1f;
        }

        public static float AngleDeg(Vector2 v)
        {
            return Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        }

        public static Vector2 FromAngleDeg(float deg)
        {
            float r = deg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
        }

        /// <summary>Rotates unit vector <paramref name="from"/> toward <paramref name="to"/> by at most <paramref name="maxDeg"/>.</summary>
        public static Vector2 RotateTowards(Vector2 from, Vector2 to, float maxDeg)
        {
            if (to.sqrMagnitude < 1e-10f) return from;
            if (from.sqrMagnitude < 1e-10f) return to.normalized;
            float a = AngleDeg(from);
            float b = AngleDeg(to);
            return FromAngleDeg(Mathf.MoveTowardsAngle(a, b, maxDeg));
        }

        /// <summary>
        /// Moves an edge point (with its outward normal) onto the matching edge of <paramref name="view"/>, keeping the
        /// position along the edge clamped inside the view. Keeps edge warnings and spawns on the edge after a resize.
        /// </summary>
        public static Vector2 ProjectOntoEdge(Rect view, Vector2 point, Vector2 outwardNormal)
        {
            point.x = Mathf.Clamp(point.x, view.xMin, view.xMax);
            point.y = Mathf.Clamp(point.y, view.yMin, view.yMax);
            if (outwardNormal.x < -0.5f) point.x = view.xMin;
            else if (outwardNormal.x > 0.5f) point.x = view.xMax;
            if (outwardNormal.y < -0.5f) point.y = view.yMin;
            else if (outwardNormal.y > 0.5f) point.y = view.yMax;
            return point;
        }

        public static float SmoothStep01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        public static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;
            return 1f - u * u * u;
        }

        public static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        public static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        /// <summary>Destroy in play mode, DestroyImmediate in edit mode.</summary>
        public static void DestroySafe(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Object.Destroy(obj);
            else Object.DestroyImmediate(obj);
        }
    }
}
