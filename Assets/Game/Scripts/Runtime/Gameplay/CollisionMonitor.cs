using System.Collections.Generic;
using UnityEngine;

namespace ClearToLand
{
    /// <summary>
    /// Checks every pair of airborne, visible aircraft after they moved: overlap -> crash, near miss -> warning
    /// rings + repeating beep. Allocation-free.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class CollisionMonitor : MonoBehaviour
    {
        private GameManager _gm;
        private bool[] _warn = new bool[32];

        /// <summary>True when at least one pair was within warning distance on the last check.</summary>
        public bool AnyWarning { get; private set; }

        /// <summary>Binds the monitor to the game.</summary>
        public void Init(GameManager gm)
        {
            _gm = gm;
        }

        private void LateUpdate()
        {
            if (_gm == null || _gm.State != GameState.Playing) return;

            IReadOnlyList<Aircraft> list = _gm.ActiveAircraft;
            int n = list.Count;
            if (_warn.Length < n)
            {
                int size = _warn.Length;
                while (size < n) size *= 2;
                _warn = new bool[size];
            }
            for (int i = 0; i < n; i++) _warn[i] = false;

            GameConfig cfg = _gm.Config;
            float margin = cfg != null ? cfg.proximityWarnMargin : 0.9f;
            bool any = false;

            for (int i = 0; i < n; i++)
            {
                Aircraft a = list[i];
                if (a == null || !a.IsAirborne || !a.IsVisible) continue;
                Vector2 pa = a.Position;
                float ra = a.Radius;

                for (int j = i + 1; j < n; j++)
                {
                    Aircraft b = list[j];
                    if (b == null || !b.IsAirborne || !b.IsVisible) continue;

                    ProximityLevel level = ProximityMath.Evaluate(pa, ra, b.Position, b.Radius, margin);
                    if (level == ProximityLevel.Collision)
                    {
                        AnyWarning = false;
                        _gm.TriggerCrash(a, b);
                        return;
                    }
                    if (level == ProximityLevel.Warning)
                    {
                        _warn[i] = true;
                        _warn[j] = true;
                        any = true;
                    }
                }
            }

            for (int i = 0; i < n; i++)
            {
                Aircraft a = list[i];
                if (a != null) a.SetProximityWarning(_warn[i]);
            }

            AnyWarning = any;
            AudioManager.Instance.SetWarningActive(any);
        }
    }
}
