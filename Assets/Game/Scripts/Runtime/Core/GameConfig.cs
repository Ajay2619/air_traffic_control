using System.Collections.Generic;
using UnityEngine;

namespace ClearToLand
{
    /// <summary>
    /// Root asset referenced by both scenes: content lists, generated sprites and tuning values.
    /// Created and filled by the editor ProjectBuilder (Tools > Clear to Land > Rebuild Everything).
    /// </summary>
    [CreateAssetMenu(menuName = "Clear to Land/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Content")]
        public List<MapDefinition> maps = new List<MapDefinition>();
        public List<AircraftType> aircraftTypes = new List<AircraftType>();

        [Header("Materials")]
        [Tooltip("Unlit vertex-colored material used by path LineRenderers (Sprites/Default shader).")]
        public Material pathLineMaterial;

        [Header("World sprites (white, tinted at runtime)")]
        public Sprite edgeWarningSprite;     // circle with a dark '!' glyph
        public Sprite ringSprite;            // thin ring
        public Sprite softCircleSprite;      // radial soft disc
        public Sprite debrisSprite;          // small shard
        public Sprite spotGlowRunwaySprite;  // soft rounded rect, 9-sliced (use SpriteDrawMode.Sliced)
        public Sprite spotGlowPadSprite;     // soft disc for helipads
        public Sprite chevronSprite;         // landing-direction chevron, points +X

        [Header("UI sprites")]
        public Sprite panelSprite;   // rounded rect, 9-sliced
        public Sprite buttonSprite;  // rounded rect, 9-sliced
        public Sprite cardSprite;    // rounded rect, 9-sliced
        public Sprite circleSprite;  // filled circle
        public Sprite logoSprite;    // title emblem

        [Header("UI icons (white glyphs)")]
        public Sprite iconPause;
        public Sprite iconPlay;
        public Sprite iconFastForward;
        public Sprite iconSoundOn;
        public Sprite iconSoundOff;
        public Sprite iconHome;
        public Sprite iconRestart;
        public Sprite iconHelp;
        public Sprite iconClose;
        public Sprite iconStar;

        [Header("Tuning")]
        public float fastForwardScale = 2f;
        [Tooltip("Spacing between recorded path points, world units.")]
        public float pathPointSpacing = 0.12f;
        [Tooltip("Extra pick distance around an aircraft's radius when touching it.")]
        public float pickPadding = 0.45f;
        [Tooltip("Distance beyond touching (sum of radii) at which the proximity warning shows.")]
        public float proximityWarnMargin = 0.9f;
        [Tooltip("Seconds the edge '!' warning shows before an aircraft enters.")]
        public float spawnWarningLeadTime = 2.2f;
        [Tooltip("Max angle between path heading and runway direction to capture a landing.")]
        public float landingMaxAngleDeg = 60f;
        [Tooltip("Time scale used right after a crash, before the game-over panel.")]
        public float crashSlowMotionScale = 0.25f;
        [Tooltip("Real-time seconds of slow motion after a crash.")]
        public float crashSlowMotionDuration = 1.3f;
        [Tooltip("Degrees per second aircraft turn when steering back from the screen edge.")]
        public float edgeTurnRateDeg = 140f;
        [Tooltip("Shadow offset (world units) for an aircraft at full cruise altitude.")]
        public Vector2 shadowOffset = new Vector2(0.1f, -0.16f);
    }
}
