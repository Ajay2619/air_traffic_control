using UnityEngine;

namespace ClearToLand.EditorTools
{
    /// <summary>Small color / vector helpers shared by the sprite painters.</summary>
    internal static class ArtKit
    {
        /// <summary>Fully transparent black.</summary>
        public static readonly Color Transparent = new Color(0f, 0f, 0f, 0f);

        /// <summary>Shorthand for <c>new Vector2(x, y)</c>.</summary>
        public static Vector2 V(float x, float y) => new Vector2(x, y);

        /// <summary>Gray of value <paramref name="v"/> (clamped to 0..1) with alpha <paramref name="a"/>.</summary>
        public static Color Gray(float v, float a = 1f)
        {
            v = Mathf.Clamp01(v);
            return new Color(v, v, v, Mathf.Clamp01(a));
        }

        /// <summary>White with alpha <paramref name="a"/>.</summary>
        public static Color White(float a) => new Color(1f, 1f, 1f, Mathf.Clamp01(a));

        /// <summary>Cubic smoothstep of <paramref name="t"/> clamped to 0..1.</summary>
        public static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Unit vector at <paramref name="degrees"/> (0 = +X, counter-clockwise).</summary>
        public static Vector2 Dir(float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
        }
    }
}
