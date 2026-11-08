using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClearToLand
{
    /// <summary>
    /// One landing spot on a map. For directional kinds (runways, water lanes) <see cref="position"/> is the
    /// touchdown threshold at the approach end and <see cref="angleDeg"/> is the landing/roll direction
    /// (0 = +X, counter-clockwise). For helipads <see cref="position"/> is the pad center and
    /// <see cref="width"/> its diameter.
    /// </summary>
    [Serializable]
    public sealed class LandingSpotDef
    {
        public string name = "Runway";
        public LandingKind kind = LandingKind.LongRunway;
        public Vector2 position;
        public float angleDeg;
        [Tooltip("Runway / lane length in world units (ignored for helipads).")]
        public float length = 5f;
        [Tooltip("Runway / lane width, or helipad diameter, in world units.")]
        public float width = 0.7f;

        public Vector2 Direction
        {
            get
            {
                float r = angleDeg * Mathf.Deg2Rad;
                return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
            }
        }

        /// <summary>Far (stop) end of a directional spot; equals <see cref="position"/> for helipads.</summary>
        public Vector2 FarEnd => LandingColors.IsDirectional(kind) ? position + Direction * length : position;

        /// <summary>Center of the spot (midpoint of a runway, pad center for helipads).</summary>
        public Vector2 Center => LandingColors.IsDirectional(kind) ? position + Direction * (length * 0.5f) : position;

        public LandingSpotDef Clone() => (LandingSpotDef)MemberwiseClone();
    }

    [Serializable]
    public sealed class SpawnEntry
    {
        public AircraftType type;
        [Tooltip("Relative spawn weight.")]
        public float weight = 1f;
        [Tooltip("Seconds of play before this type can spawn.")]
        public float unlockTime;
    }

    /// <summary>A playable map: background art, landing spots and spawn/difficulty tuning.</summary>
    [CreateAssetMenu(menuName = "Clear to Land/Map Definition", fileName = "Map")]
    public sealed class MapDefinition : ScriptableObject
    {
        [Tooltip("Stable id used for high scores and art file names, e.g. green_valley")]
        public string id = "map";
        public string displayName = "Map";
        [TextArea] public string tagline = "";
        [Range(1, 4)] public int difficulty = 1;

        [Header("Visuals")]
        [Tooltip("Background sprite, BackgroundWidth x BackgroundHeight world units, centered on the origin.")]
        public Sprite background;
        [Tooltip("Camera clear color behind the background.")]
        public Color cameraColor = new Color(0.2f, 0.35f, 0.2f);

        [Header("Layout")]
        public List<LandingSpotDef> landingSpots = new List<LandingSpotDef>();

        [Header("Spawning")]
        public List<SpawnEntry> spawnTable = new List<SpawnEntry>();
        [Tooltip("Seconds between spawns at the start.")]
        public float initialSpawnInterval = 7.5f;
        [Tooltip("Seconds between spawns once fully ramped.")]
        public float minSpawnInterval = 2.6f;
        [Tooltip("Seconds of play to go from initial to min spawn interval.")]
        public float rampDuration = 360f;
        [Tooltip("Max simultaneous airborne aircraft at the start.")]
        public int initialMaxAircraft = 3;
        [Tooltip("Hard cap on simultaneous airborne aircraft.")]
        public int maxAircraftCap = 12;
        [Tooltip("Every N seconds the simultaneous-aircraft limit grows by one.")]
        public float maxAircraftStepSeconds = 35f;
        [Tooltip("Multiplies every aircraft's speed on this map.")]
        public float speedMultiplier = 1f;
    }
}
