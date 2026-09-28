using UnityEngine;
using UnityEngine.Video;

// Aliased, not imported: Gaskellgames ships its own Min/Range attributes.
using GG = Gaskellgames;

namespace GameLogic.Story
{
    /// <summary>What role a cinematic plays in the 3-act campaign timeline.</summary>
    public enum CinematicKind
    {
        /// <summary>Game-opening news broadcast. Plays once, before night 1.</summary>
        Opening,
        /// <summary>Forced act transition (start of nights 3 and 6). Plays once per act switch.</summary>
        ActTransition,
        /// <summary>Staff training video for an anomaly that first appears tonight.</summary>
        Training,
    }

    /// <summary>
    /// One authored cutscene: names the clip and when it plays. StoryCinematicController builds
    /// the per-night queue; CinematicPlayer renders. Played-once cinematics are tracked by
    /// cinematicId in the save file, same convention as DayEventData and MailData.
    /// </summary>
    [CreateAssetMenu(fileName = "Cinematic_", menuName = "Give Me A Sign/Story/Cinematic")]
    public class CinematicData : ScriptableObject
    {
        [GG.InfoBox("cinematicId is written into the save as 'already played'. Renaming it lets existing saves show it again.", GG.InfoMessageType.Warning)]
        [GG.Required]
        [Tooltip("Permanent unique key for this cutscene. Used for the played-once gate.")]
        public string cinematicId = "";

        [Tooltip("What this cinematic is for. Decides which queue it joins.")]
        public CinematicKind kind = CinematicKind.Training;

        [Header("Video")]
        [Tooltip("The video clip. If empty, a black placeholder with the title holds for placeholderHoldSeconds instead - useful before real clips exist.")]
        public VideoClip clip;

        [Tooltip("Let the player skip after this many seconds. 0 = not skippable.")]
        [Min(0f)] public float skippableAfterSeconds = 2f;

        [Header("Placeholder (used only when clip is empty)")]
        [Tooltip("Title shown on the black placeholder screen.")]
        public string placeholderTitle = "";
        [Tooltip("Subtitle/secondary line shown on the placeholder screen.")]
        public string placeholderSubtitle = "";
        [Tooltip("How long an empty-clip placeholder holds before being treated as finished.")]
        [Min(0.1f)] public float placeholderHoldSeconds = 3f;

        [Header("Badge (overlay label while playing, video or placeholder)")]
        [Tooltip("Optional small label in the corner, e.g. 'C.N.A. STAFF BROADCAST'. Empty = hidden.")]
        public string badgeLabel = "";

        public bool IsPlayable() => !string.IsNullOrWhiteSpace(cinematicId);
    }
}