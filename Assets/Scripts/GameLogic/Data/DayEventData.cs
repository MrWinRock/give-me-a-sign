using UnityEngine;

// Aliased, not imported: Gaskellgames ships its own Min/Range attributes.
// See CLAUDE.md - "Gaskellgames" for the project-wide rule.
using GG = Gaskellgames;

namespace GameLogic.Data
{
    /// <summary>Which kind of day-end event was rolled. None = nothing plays today.</summary>
    public enum DayEventType
    {
        None = 0,
        ShortVDO = 1,
        Minigame = 2,
    }

    /// <summary>
    /// Shared identity for anything the RandomEventDirector can hand out at the end of a day.
    /// Consumption is NOT stored here - it lives in the save file, so the asset stays read-only
    /// and a New Game genuinely brings every event back.
    /// </summary>
    public abstract class DayEventData : ScriptableObject
    {
        [GG.InfoBox("eventId is written into the save file as 'already watched'. Renaming it re-unlocks the event for existing saves.", GG.InfoMessageType.Warning)]
        [GG.Required]
        [Tooltip("Permanent unique key for this event.")]
        public string eventId = "";

        [Tooltip("Name shown in debug output only.")]
        public string displayName = "";

        public abstract DayEventType EventType { get; }

        /// <summary>displayName, falling back to the id so logs are never blank.</summary>
        public string Label => string.IsNullOrWhiteSpace(displayName) ? eventId : displayName;

        /// <summary>False when the asset is missing the data it needs to play.</summary>
        public virtual bool IsPlayable() => !string.IsNullOrWhiteSpace(eventId);
    }
}
