using UnityEngine;

namespace ClearToLand
{
    /// <summary>
    /// Mid-air collision effect: white-hot flash, fireball, spinning debris and drifting smoke. Runs on unscaled time
    /// (so it plays through slow motion and the game-over freeze) and destroys itself when finished.
    /// </summary>
    public static class ExplosionEffect
    {
        /// <summary>Spawns the effect at <paramref name="position"/>. <paramref name="config"/> may be null (nothing visible).</summary>
        public static void Play(Vector2 position, GameConfig config)
        {
            GameObject go = new GameObject("Explosion");
            go.transform.position = new Vector3(position.x, position.y, 0f);
            ExplosionEffectRunner runner = go.AddComponent<ExplosionEffectRunner>();
            runner.Build(config);
        }
    }
}
