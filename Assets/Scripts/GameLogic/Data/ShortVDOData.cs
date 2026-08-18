using UnityEngine;
using UnityEngine.Video;

// Aliased, not imported: Gaskellgames ships its own Min/Range attributes.
using GG = Gaskellgames;

namespace GameLogic.Data
{
    /// <summary>One short video that can play at the end of a day.</summary>
    [CreateAssetMenu(fileName = "VDO_", menuName = "Give Me A Sign/Day Event/Short VDO")]
    public class ShortVDOData : DayEventData
    {
        [GG.Required]
        [Tooltip("Clip played fullscreen. The player is considered to have watched it once it reaches the end.")]
        public VideoClip clip;

        [Tooltip("Let the player skip after this many seconds. 0 = not skippable.")]
        [Min(0f)] public float skippableAfterSeconds;

        public override DayEventType EventType => DayEventType.ShortVDO;

        public override bool IsPlayable() => base.IsPlayable() && clip != null;
    }
}
