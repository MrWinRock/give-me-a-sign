using UnityEngine;

// Aliased, not imported: Gaskellgames ships its own Min/Range attributes.
using GG = Gaskellgames;

namespace GameLogic.Data
{
    /// <summary>
    /// One document/email the player can find during a shift (via a MailPickup placed in the
    /// GamePlay scene) and read later from the mailbox on the desktop. Found/read state lives in
    /// the save file (SaveData.foundMailIds / readEmailIds), same pattern as DayEventData's
    /// consumed-event list - the asset stays read-only.
    /// </summary>
    [CreateAssetMenu(fileName = "Mail_", menuName = "Give Me A Sign/Mail/Document")]
    public class MailData : ScriptableObject
    {
        [GG.InfoBox("emailId is written into the save file as 'already read'. Renaming it makes existing saves show it unread again.", GG.InfoMessageType.Warning)]
        [GG.Required]
        [Tooltip("Permanent unique key for this document.")]
        public string emailId = "";

        [Tooltip("Shift this document's MailPickup is active during (1 = the first shift). It only shows up in the mailbox once actually clicked in the world that day - see MailPickup.")]
        [Min(1)] public int unlockDay = 1;

        [Header("Header")]
        [GG.Required] public string sender = "";
        [GG.Required] public string subject = "";
        [Tooltip("Shown next to the sender. Leave empty to auto-fill as 'Night {unlockDay}'.")]
        public string dateLabel = "";

        [Header("Body")]
        [TextArea(4, 20)] public string body = "";

        [Tooltip("Optional scanned image shown as an attachment in the reading pane.")]
        public Sprite attachment;

        /// <summary>dateLabel, falling back to a generated "Night N" so the header is never blank.</summary>
        public string DateLabel => string.IsNullOrWhiteSpace(dateLabel) ? $"Night {unlockDay}" : dateLabel;

        /// <summary>False when the asset is missing the data it needs to display.</summary>
        public bool IsPlayable() =>
            !string.IsNullOrWhiteSpace(emailId) &&
            !string.IsNullOrWhiteSpace(sender) &&
            !string.IsNullOrWhiteSpace(subject);

        /// <summary>True while the player is on the shift this document's MailPickup can be found during.</summary>
        public bool IsScheduledForDay(int currentDay) => currentDay == unlockDay;
    }
}
