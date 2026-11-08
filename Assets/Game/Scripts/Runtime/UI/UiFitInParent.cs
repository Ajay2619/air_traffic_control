using UnityEngine;

namespace ClearToLand
{
    /// <summary>
    /// Uniformly scales a fixed-size, center-anchored panel down so it fits inside its parent rect (minus
    /// <see cref="Padding"/>). Re-fits when the parent size changes (resize, rotation, safe-area change).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiFitInParent : MonoBehaviour
    {
        private RectTransform _rect;
        private Vector2 _lastParentSize = new Vector2(-1f, -1f);
        private Vector2 _lastSize = new Vector2(-1f, -1f);

        /// <summary>Space kept free around the panel, reference px.</summary>
        public float Padding { get; set; } = 24f;

        /// <summary>Largest allowed scale (1 = never enlarge).</summary>
        public float MaxScale { get; set; } = 1f;

        private void Awake() => _rect = (RectTransform)transform;

        private void OnEnable() => Refit();

        private void LateUpdate()
        {
            RectTransform parent = _rect.parent as RectTransform;
            if (parent == null) return;
            if (parent.rect.size != _lastParentSize || _rect.rect.size != _lastSize) Refit();
        }

        /// <summary>Recomputes the scale immediately.</summary>
        public void Refit()
        {
            if (_rect == null) _rect = (RectTransform)transform;
            RectTransform parent = _rect.parent as RectTransform;
            if (parent == null) return;

            Vector2 parentSize = parent.rect.size;
            Vector2 size = _rect.rect.size;
            _lastParentSize = parentSize;
            _lastSize = size;
            if (size.x <= 0f || size.y <= 0f || parentSize.x <= 0f || parentSize.y <= 0f) return;

            float availW = Mathf.Max(1f, parentSize.x - Padding * 2f);
            float availH = Mathf.Max(1f, parentSize.y - Padding * 2f);
            float s = Mathf.Min(MaxScale, availW / size.x, availH / size.y);
            s = Mathf.Max(0.2f, s);
            _rect.localScale = new Vector3(s, s, 1f);
        }
    }
}
