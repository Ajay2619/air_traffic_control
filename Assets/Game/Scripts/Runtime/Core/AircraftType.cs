using UnityEngine;

namespace ClearToLand
{
    /// <summary>
    /// Static description of one kind of aircraft. Sprites are authored in light grayscale with the nose
    /// pointing +X (right) and are tinted at runtime with <see cref="Color"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Clear to Land/Aircraft Type", fileName = "AircraftType")]
    public sealed class AircraftType : ScriptableObject
    {
        [Tooltip("Stable id: prop, airliner, fighter, helicopter, seaplane")]
        public string id = "prop";
        public string displayName = "Prop plane";
        public LandingKind landingKind = LandingKind.ShortRunway;

        [Header("Visuals")]
        [Tooltip("Body sprite, nose pointing +X, light grayscale (tinted with the landing color).")]
        public Sprite bodySprite;
        [Tooltip("Optional spinning rotor drawn above the body (helicopters).")]
        public Sprite rotorSprite;

        [Header("Flight")]
        [Tooltip("Cruise speed in world units per second (before the map's speed multiplier).")]
        public float speed = 0.75f;
        [Tooltip("Collision radius in world units.")]
        public float radius = 0.25f;

        [Header("Scoring")]
        public int scoreValue = 1;

        public Color Color => LandingColors.For(landingKind);
        public bool IsHelicopter => landingKind == LandingKind.Helipad;
    }
}
