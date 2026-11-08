using System.Collections.Generic;
using UnityEngine;

namespace ClearToLand
{
    /// <summary>
    /// The playable map in the scene: background sprite plus one <see cref="LandingSpot"/> per
    /// <see cref="MapDefinition.landingSpots"/> entry (spot index == list index).
    /// </summary>
    public sealed class MapRuntime : MonoBehaviour
    {
        private static readonly LandingSpot[] NoSpots = new LandingSpot[0];

        private readonly List<LandingSpot> _spots = new List<LandingSpot>(8);
        private LandingSpot[] _byIndex = NoSpots;

        /// <summary>The map this runtime was built from (null before <see cref="Build"/>).</summary>
        public MapDefinition Map { get; private set; }

        /// <summary>All landing spots (never contains null).</summary>
        public IReadOnlyList<LandingSpot> Spots => _spots;

        /// <summary>The background renderer, or null when the map has no background sprite.</summary>
        public SpriteRenderer Background { get; private set; }

        /// <summary>
        /// Builds (or rebuilds) the background and landing spots. <paramref name="config"/> may be null
        /// (spots then have no highlight visuals).
        /// </summary>
        public void Build(MapDefinition map, GameConfig config)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                GameplayUtil.DestroySafe(transform.GetChild(i).gameObject);
            _spots.Clear();
            _byIndex = NoSpots;
            Background = null;

            Map = map;
            if (map == null) return;

            if (map.background != null)
            {
                SpriteRenderer bg = GameplayUtil.CreateSprite(transform, "Background", map.background, Color.white, SortOrder.Background);
                bg.transform.localPosition = Vector3.zero;
                // Generated backgrounds are exactly BackgroundWidth x BackgroundHeight; scale anything else to cover that area.
                Vector3 size = map.background.bounds.size;
                if (size.x > 1e-4f && size.y > 1e-4f)
                {
                    float sx = GameConstants.BackgroundWidth / size.x;
                    float sy = GameConstants.BackgroundHeight / size.y;
                    float s = Mathf.Max(sx, sy);
                    if (Mathf.Abs(s - 1f) > 0.01f) bg.transform.localScale = new Vector3(s, s, 1f);
                }
                Background = bg;
            }

            List<LandingSpotDef> defs = map.landingSpots;
            int count = defs != null ? defs.Count : 0;
            _byIndex = count > 0 ? new LandingSpot[count] : NoSpots;
            for (int i = 0; i < count; i++)
            {
                LandingSpotDef def = defs[i];
                if (def == null) continue;
                GameObject go = new GameObject("Spot_" + i + "_" + (string.IsNullOrEmpty(def.name) ? def.kind.ToString() : def.name));
                go.transform.SetParent(transform, false);
                LandingSpot spot = go.AddComponent<LandingSpot>();
                spot.Init(i, def, config);
                _byIndex[i] = spot;
                _spots.Add(spot);
            }
        }

        /// <summary>The spot for a MapDefinition.landingSpots index, or null if out of range.</summary>
        public LandingSpot GetSpot(int index)
        {
            return index >= 0 && index < _byIndex.Length ? _byIndex[index] : null;
        }

        /// <summary>First spot that accepts <paramref name="type"/> and whose capture zone the point/heading satisfies.</summary>
        public LandingSpot FindCapture(AircraftType type, Vector2 point, Vector2 heading, float maxAngleDeg)
        {
            if (type == null) return null;
            for (int i = 0; i < _spots.Count; i++)
            {
                LandingSpot s = _spots[i];
                if (s == null) continue;
                if (s.Accepts(type) && s.Captures(point, heading, maxAngleDeg)) return s;
            }
            return null;
        }

        /// <summary>Highlights every spot accepting <paramref name="type"/> as Candidate and clears the rest; null clears all.</summary>
        public void SetCandidateHighlight(AircraftType type)
        {
            for (int i = 0; i < _spots.Count; i++)
            {
                LandingSpot s = _spots[i];
                if (s == null) continue;
                s.SetHighlight(type != null && s.Accepts(type) ? SpotHighlight.Candidate : SpotHighlight.None);
            }
        }
    }
}
