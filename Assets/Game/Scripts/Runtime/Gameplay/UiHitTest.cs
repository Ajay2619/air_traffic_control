using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ClearToLand
{
    /// <summary>Tells whether a screen position is over a raycast-target UI element (so world input should ignore it).</summary>
    public static class UiHitTest
    {
        private static readonly List<RaycastResult> s_results = new List<RaycastResult>(16);
        private static PointerEventData s_eventData;
        private static EventSystem s_eventDataOwner;

        /// <summary>True when any EventSystem raycaster hits UI at <paramref name="screenPosition"/>; false if there is no EventSystem.</summary>
        public static bool IsOverUi(Vector2 screenPosition)
        {
            EventSystem es = EventSystem.current;
            if (es == null) return false;

            if (s_eventData == null || s_eventDataOwner != es)
            {
                s_eventData = new PointerEventData(es);
                s_eventDataOwner = es;
            }

            s_eventData.Reset();
            s_eventData.position = screenPosition;
            s_results.Clear();
            es.RaycastAll(s_eventData, s_results);

            bool hit = false;
            for (int i = 0; i < s_results.Count; i++)
            {
                if (s_results[i].gameObject != null)
                {
                    hit = true;
                    break;
                }
            }
            s_results.Clear();
            return hit;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_eventData = null;
            s_eventDataOwner = null;
            s_results.Clear();
        }
    }
}
