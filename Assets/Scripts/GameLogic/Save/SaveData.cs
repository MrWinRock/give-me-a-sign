using System.Collections.Generic;

namespace GameLogic.Save
{
    /// <summary>
    /// Everything that survives quitting the game. Plain serializable data - no Unity object
    /// references, so JsonUtility can round-trip it.
    /// </summary>
    [System.Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;

        public int currentDay = 1;

        // Ids, not indices: reordering a pool in the Inspector must not silently mark a
        // different event as already watched.
        public List<string> consumedEventIds = new List<string>();

        public List<string> readEmailIds = new List<string>();

        // Separate from readEmailIds: found = the player clicked the pickup in the world,
        // read = the player has opened it in the Mail window. A found-but-unread document
        // still shows up bold in the inbox.
        public List<string> foundMailIds = new List<string>();

        public bool IsConsumed(string eventId) =>
            !string.IsNullOrEmpty(eventId) && consumedEventIds.Contains(eventId);

        public void MarkConsumed(string eventId)
        {
            if (string.IsNullOrEmpty(eventId) || consumedEventIds.Contains(eventId)) return;
            consumedEventIds.Add(eventId);
        }

        public bool IsEmailRead(string emailId) =>
            !string.IsNullOrEmpty(emailId) && readEmailIds.Contains(emailId);

        public void MarkEmailRead(string emailId)
        {
            if (string.IsNullOrEmpty(emailId) || readEmailIds.Contains(emailId)) return;
            readEmailIds.Add(emailId);
        }

        public bool IsMailFound(string emailId) =>
            !string.IsNullOrEmpty(emailId) && foundMailIds.Contains(emailId);

        public void MarkMailFound(string emailId)
        {
            if (string.IsNullOrEmpty(emailId) || foundMailIds.Contains(emailId)) return;
            foundMailIds.Add(emailId);
        }
    }
}
