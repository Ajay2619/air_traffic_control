using UnityEngine;

namespace ClearToLand
{
    /// <summary>Camera / play-area helpers. The play area is exactly what the orthographic camera sees.</summary>
    public static class PlayBounds
    {
        /// <summary>Orthographic size that keeps the whole SafeWidth x SafeHeight area visible at this aspect (width/height).</summary>
        public static float FitOrthographicSize(float aspect)
        {
            if (aspect <= 0f) aspect = 16f / 9f;
            return Mathf.Max(GameConstants.SafeHeight * 0.5f, GameConstants.SafeWidth * 0.5f / aspect);
        }

        /// <summary>World rect visible through an orthographic camera.</summary>
        public static Rect ViewRect(Camera cam)
        {
            float h = cam.orthographicSize * 2f;
            float w = h * cam.aspect;
            Vector3 c = cam.transform.position;
            return new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h);
        }

        /// <summary>Rect shrunk by <paramref name="inset"/> on every side (negative grows it). Never returns a negative size.</summary>
        public static Rect Inset(Rect r, float inset)
        {
            float w = Mathf.Max(0f, r.width - inset * 2f);
            float h = Mathf.Max(0f, r.height - inset * 2f);
            return new Rect(r.center.x - w * 0.5f, r.center.y - h * 0.5f, w, h);
        }

        /// <summary>Clamps a point into the rect shrunk by <paramref name="inset"/>.</summary>
        public static Vector2 ClampInside(Rect r, Vector2 p, float inset = 0f)
        {
            Rect i = Inset(r, inset);
            return new Vector2(Mathf.Clamp(p.x, i.xMin, i.xMax), Mathf.Clamp(p.y, i.yMin, i.yMax));
        }
    }
}
