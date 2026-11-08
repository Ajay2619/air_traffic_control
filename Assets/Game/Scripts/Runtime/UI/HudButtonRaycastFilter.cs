using System.Collections.Generic;
using UnityEngine;

namespace ClearToLand
{
    /// <summary>
    /// Raycast filter for the round HUD buttons (fast-forward, pause) that sit over playable corners of the map.
    /// Only the visible round disc is a hit target (not the transparent square corners of the hit image), and while a
    /// round is being played a press within pick reach of any controllable aircraft (Radius + config.pickPadding, the
    /// same reach PathInputController uses) is left to the world. Graphic.Raycast consults this filter, so the
    /// EventSystem and <see cref="UiHitTest"/> both ignore the button at that spot and the press becomes a path drag.
    /// Allocation free (called for every UI raycast that lands inside the button's rect).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class HudButtonRaycastFilter : MonoBehaviour, ICanvasRaycastFilter
    {
        private const float DefaultPickPadding = 0.45f;

        private RectTransform _rect;
        private GameManager _gm;
        private float _discDiameter;

        /// <summary>
        /// Binds the round (null falls back to <see cref="GameManager.Instance"/>). <paramref name="discDiameter"/> is the
        /// visible disc in the button's local units; 0 or less uses the circle inscribed in the button's rect.
        /// </summary>
        public void Init(GameManager gm, float discDiameter = 0f)
        {
            _gm = gm;
            _discDiameter = discDiameter;
        }

        /// <inheritdoc/>
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (_rect == null) _rect = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, screenPoint, eventCamera, out Vector2 local))
                return false;

            Rect r = _rect.rect;
            float radius = (_discDiameter > 0f ? _discDiameter : Mathf.Min(r.width, r.height)) * 0.5f;
            if ((local - r.center).sqrMagnitude > radius * radius) return false;

            return !IsNearPickableAircraft(screenPoint);
        }

        private bool IsNearPickableAircraft(Vector2 screenPoint)
        {
            GameManager gm = _gm != null ? _gm : GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing) return false; // path input only runs while Playing

            Camera cam = gm.WorldCamera;
            if (cam == null) return false;
            Vector3 w = cam.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, -cam.transform.position.z));
            Vector2 world = new Vector2(w.x, w.y);

            GameConfig cfg = gm.Config;
            float padding = cfg != null ? Mathf.Max(0f, cfg.pickPadding) : DefaultPickPadding;

            IReadOnlyList<Aircraft> list = gm.ActiveAircraft;
            for (int i = 0; i < list.Count; i++)
            {
                Aircraft a = list[i];
                // Aircraft that are being drawn still count: PathInputController (order -50) picks before the EventSystem
                // (order 0) raycasts, so the aircraft this very press just grabbed is already IsBeingDrawn here. Skipping
                // it would let the button take the same press (pressed tint, and a click if released over the disc).
                if (a == null || !a.IsControllable) continue;
                float reach = a.Radius + padding;
                if ((a.Position - world).sqrMagnitude <= reach * reach) return true;
            }
            return false;
        }
    }
}
