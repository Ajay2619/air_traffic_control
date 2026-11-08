using UnityEngine;

namespace ClearToLand
{
    /// <summary>Carries the map choice from the main menu into the Game scene.</summary>
    public static class GameSession
    {
        public static MapDefinition SelectedMap { get; set; }

        /// <summary>The selected map, or the first map in the config when none was chosen (e.g. Game scene opened directly).</summary>
        public static MapDefinition ResolveMap(GameConfig config)
        {
            if (SelectedMap != null) return SelectedMap;
            if (config != null && config.maps != null && config.maps.Count > 0) return config.maps[0];
            return null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => SelectedMap = null;
    }
}
