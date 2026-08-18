using UnityEngine;

// Aliased, not imported: Gaskellgames ships its own Min/Range attributes.
using GG = Gaskellgames;

namespace GameLogic.Data
{
    /// <summary>One minigame that can play at the end of a day.</summary>
    [CreateAssetMenu(fileName = "Minigame_", menuName = "Give Me A Sign/Day Event/Minigame")]
    public class MinigameData : DayEventData
    {
        [Tooltip("Prefab instantiated to run the minigame. Leave empty if this one loads a scene instead.")]
        public GameObject prefab;

        [Tooltip("Scene loaded to run the minigame. Leave empty if this one uses a prefab instead. Must be in Build Settings.")]
        public string sceneName = "";

        [GG.InfoBox("Assign EITHER a prefab OR a scene name - not both, and not neither.", GG.InfoMessageType.Warning)]
        [Tooltip("Hard cap on how long the minigame may run. 0 = no limit.")]
        [Min(0f)] public float timeLimitSeconds;

        public override DayEventType EventType => DayEventType.Minigame;

        /// <summary>Exactly one of prefab/sceneName must be set - both or neither is an authoring error.</summary>
        public override bool IsPlayable()
        {
            if (!base.IsPlayable()) return false;

            bool hasPrefab = prefab != null;
            bool hasScene = !string.IsNullOrWhiteSpace(sceneName);
            return hasPrefab ^ hasScene;
        }
    }
}
