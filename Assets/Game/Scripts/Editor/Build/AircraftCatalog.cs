using UnityEditor;
using UnityEngine;

namespace ClearToLand.EditorTools
{
    /// <summary>
    /// Source of truth for the five aircraft types (landing kind, display name, flight stats, sprites).
    /// <see cref="ProjectBuilder"/> applies it to Assets/Game/Data/Aircraft/{id}.asset; tests apply it to
    /// in-memory instances (sprites may be null there).
    /// </summary>
    public static class AircraftCatalog
    {
        /// <summary>Stable aircraft ids, in GameConfig order.</summary>
        public static readonly string[] Ids = { "prop", "airliner", "fighter", "helicopter", "seaplane" };

        /// <summary>Sprite key of the helicopter main rotor.</summary>
        public const string RotorSpriteKey = "aircraft_rotor";

        /// <summary>Sprite key of an aircraft body ("aircraft_" + id).</summary>
        public static string BodySpriteKey(string id) => "aircraft_" + id;

        /// <summary>
        /// Overwrites <paramref name="t"/> with the catalog entry for <paramref name="id"/>: id, display name, landing kind,
        /// speed, radius, score value, body sprite and (helicopter only) rotor sprite. Unknown ids or a null target only log
        /// a warning. Missing sprites are left null silently (the builder reports them).
        /// </summary>
        public static void Apply(AircraftType t, string id)
        {
            if (t == null)
            {
                Debug.LogWarning("[ClearToLand] AircraftCatalog.Apply: target AircraftType is null.");
                return;
            }

            LandingKind kind;
            float speed;
            float radius;
            string displayName;
            if (!TryGetStats(id, out kind, out speed, out radius, out displayName))
            {
                Debug.LogWarning($"[ClearToLand] AircraftCatalog.Apply: unknown aircraft id '{id}'. Known ids: {string.Join(", ", Ids)}.");
                return;
            }

            t.id = id;
            t.displayName = displayName;
            t.landingKind = kind;
            t.speed = speed;
            t.radius = radius;
            t.scoreValue = 1;
            t.bodySprite = ArtGenerator.LoadSprite(BodySpriteKey(id));
            t.rotorSprite = id == "helicopter" ? ArtGenerator.LoadSprite(RotorSpriteKey) : null;

            if (EditorUtility.IsPersistent(t)) EditorUtility.SetDirty(t);
            else if (string.IsNullOrEmpty(t.name)) t.name = id;
        }

        private static bool TryGetStats(string id, out LandingKind kind, out float speed, out float radius, out string displayName)
        {
            switch (id)
            {
                case "prop":
                    kind = LandingKind.ShortRunway; speed = 0.72f; radius = 0.24f; displayName = "Prop plane";
                    return true;
                case "airliner":
                    kind = LandingKind.LongRunway; speed = 0.9f; radius = 0.36f; displayName = "Airliner";
                    return true;
                case "fighter":
                    kind = LandingKind.LongRunway; speed = 1.15f; radius = 0.28f; displayName = "Jet fighter";
                    return true;
                case "helicopter":
                    kind = LandingKind.Helipad; speed = 0.55f; radius = 0.26f; displayName = "Helicopter";
                    return true;
                case "seaplane":
                    kind = LandingKind.WaterLane; speed = 0.68f; radius = 0.27f; displayName = "Seaplane";
                    return true;
                default:
                    kind = LandingKind.ShortRunway; speed = 0f; radius = 0f; displayName = null;
                    return false;
            }
        }
    }
}
