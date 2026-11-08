using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ClearToLand
{
    /// <summary>
    /// Tactile feedback for buttons: shrinks slightly while pressed and grows slightly on mouse hover.
    /// Uses unscaled time so it works while paused. Must sit on a transform that nothing else scales.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiPressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler,
        IPointerExitHandler
    {
        private Selectable _selectable;
        private bool _pressed;
        private bool _hover;
        private float _scale = 1f;
        private bool _animating;

        /// <summary>Scale while pressed.</summary>
        public float PressedScale { get; set; } = 0.93f;

        /// <summary>Scale while hovered by a mouse.</summary>
        public float HoverScale { get; set; } = 1.035f;

        private void Awake() => _selectable = GetComponent<Selectable>();

        private bool Interactable => _selectable == null || _selectable.IsInteractable();

        /// <inheritdoc/>
        public void OnPointerDown(PointerEventData eventData)
        {
            if (!Interactable) return;
            _pressed = true;
            _animating = true;
        }

        /// <inheritdoc/>
        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;
            _animating = true;
        }

        /// <inheritdoc/>
        public void OnPointerEnter(PointerEventData eventData)
        {
            // Touch "hover" happens only while the finger is down; keep hover growth for mice / pens.
            ExtendedPointerEventData ext = eventData as ExtendedPointerEventData;
            _hover = ext == null || ext.pointerType != UIPointerType.Touch;
            _animating = true;
        }

        /// <inheritdoc/>
        public void OnPointerExit(PointerEventData eventData)
        {
            _hover = false;
            _pressed = false;
            _animating = true;
        }

        private void Update()
        {
            if (!_animating) return;

            float target = _pressed ? PressedScale : (_hover && Interactable ? HoverScale : 1f);
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            _scale = Mathf.Lerp(_scale, target, 1f - Mathf.Exp(-22f * dt));
            if (Mathf.Abs(_scale - target) < 0.001f)
            {
                _scale = target;
                _animating = false;
            }
            transform.localScale = new Vector3(_scale, _scale, 1f);
        }

        private void OnDisable()
        {
            _pressed = false;
            _hover = false;
            _animating = false;
            _scale = 1f;
            transform.localScale = Vector3.one;
        }
    }
}
