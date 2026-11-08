using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ClearToLand
{
    /// <summary>
    /// Receives pointer clicks on its own graphic. With no listener it simply swallows clicks so they do not bubble
    /// up to a parent handler (e.g. a panel inside a "click outside to close" dim).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiClickCatcher : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>Raised on a primary-button click / tap.</summary>
        public event Action Clicked;

        /// <inheritdoc/>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            Clicked?.Invoke();
        }
    }
}
