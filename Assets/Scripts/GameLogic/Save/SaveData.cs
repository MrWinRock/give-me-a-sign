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
    }
}
