using System.Collections.Generic;
using GameLogic.Data;
using GameLogic.Save;
using UnityEngine;

// Aliased, not imported: this file uses UnityEngine's [Min] and [Range].
// See CLAUDE.md - "Gaskellgames" for the project-wide rule.
using GG = Gaskellgames;

namespace GameLogic.Flow
{
    /// <summary>
    /// Decides whether a day ends with a Short VDO, a Minigame, or nothing at all.
    ///
    /// Deliberately query-only: it picks and reports, it never loads a scene or plays anything.
    /// GameFlowManager asks it via TryGetDayEndEvent and owns what happens next.
    /// </summary>
    public class RandomEventDirector : MonoBehaviour
    {
        [Header("Stage 1 - does an event happen at all?")]
        [GG.InfoBox("Index 0 = day 1. A day past the end of this list reuses the last entry, so a 7-entry list covers the whole campaign.")]
        [Tooltip("Chance (0-1) that ANY day-end event plays, per day.")]
        [SerializeField] private float[] eventChancePerDay = { 0f, 0.5f, 0.5f, 0.6f, 0.6f, 0.7f, 1f };

        [Header("Stage 2 - which kind?")]
        [Tooltip("Relative weight of a Short VDO against a Minigame. 1 = VDO only, 0 = Minigame only.")]
        [Range(0f, 1f)] [SerializeField] private float shortVdoWeight = 0.5f;

        [Header("Pools")]
        [Tooltip("Every Short VDO that may play. Each one plays at most once per playthrough.")]
        [SerializeField] private List<ShortVDOData> shortVdoPool = new List<ShortVDOData>();

        [Tooltip("Every Minigame that may play. Each one plays at most once per playthrough.")]
        [SerializeField] private List<MinigameData> minigamePool = new List<MinigameData>();

        [Header("Debug")]
        [Tooltip("Forces the stage-1 roll to always succeed. Stage 2 and the pools still behave normally.")]
        [SerializeField] private bool alwaysRollEvent;
        [SerializeField] private bool showDebugInfo;

        /// <summary>Chance an event fires on the given day, clamped to the authored list.</summary>
        public float ChanceForDay(int day)
        {
            if (eventChancePerDay == null || eventChancePerDay.Length == 0) return 0f;

            int index = Mathf.Clamp(day - 1, 0, eventChancePerDay.Length - 1);
            return Mathf.Clamp01(eventChancePerDay[index]);
        }

        /// <summary>
        /// Rolls for this day's end-of-day event.
        ///
        /// Returns false (with type None) when the stage-1 roll fails, when the rolled pool is
        /// empty, and when the rolled pool has been fully consumed. That last case deliberately
        /// does NOT fall back to the other pool or replay a consumed item - running out means
        /// nothing plays.
        /// </summary>
        public bool TryGetDayEndEvent(int day, out DayEventType type, out DayEventData eventData)
        {
            type = DayEventType.None;
            eventData = null;

            float chance = ChanceForDay(day);
            bool happens = alwaysRollEvent || Random.value < chance;

            if (!happens)
            {
                Log($"day {day}: no event (rolled against {chance:P0}).");
                return false;
            }

            // Stage 2 picks the KIND first and commits to it. Re-rolling to the other kind when
            // this one is exhausted would quietly break the "no forced fallback" rule.
            bool wantVdo = Random.value < shortVdoWeight;
            type = wantVdo ? DayEventType.ShortVDO : DayEventType.Minigame;

            eventData = PickUnconsumed(wantVdo);

            if (eventData == null)
            {
                Log($"day {day}: rolled {type} but that pool is empty or fully consumed - nothing plays.");
                type = DayEventType.None;
                return false;
            }

            Log($"day {day}: {type} -> '{eventData.Label}'.");
            return true;
        }

        /// <summary>
        /// Uniform pick from whatever is left in the pool. Builds the candidate list fresh each
        /// time rather than walking an index, so selection order across days is genuinely random
        /// instead of marching through the list.
        /// </summary>
        private DayEventData PickUnconsumed(bool wantVdo)
        {
            var save = SaveManager.Current;
            var candidates = new List<DayEventData>();

            if (wantVdo)
            {
                foreach (var item in shortVdoPool)
                {
                    if (IsAvailable(item, save)) candidates.Add(item);
                }
            }
            else
            {
                foreach (var item in minigamePool)
                {
                    if (IsAvailable(item, save)) candidates.Add(item);
                }
            }

            if (candidates.Count == 0) return null;
            return candidates[Random.Range(0, candidates.Count)];
        }

        private bool IsAvailable(DayEventData item, SaveData save)
        {
            if (item == null) return false;

            if (!item.IsPlayable())
            {
                Debug.LogWarning($"RandomEventDirector: '{item.name}' is not playable (missing id/clip/prefab) - skipped.", item);
                return false;
            }

            return !save.IsConsumed(item.eventId);
        }

        /// <summary>
        /// Called once the event has actually been watched/played to the end. Nothing is marked
        /// at roll time on purpose: quitting mid-video must not burn the event.
        /// </summary>
        public void MarkConsumed(DayEventData eventData)
        {
            if (eventData == null) return;

            SaveManager.Current.MarkConsumed(eventData.eventId);
            SaveManager.Save();

            Log($"consumed '{eventData.Label}' ({RemainingCount(DayEventType.ShortVDO)} VDO / " +
                $"{RemainingCount(DayEventType.Minigame)} minigame left).");
        }

        /// <summary>How many items of a kind are still unconsumed. Used by the debug readout.</summary>
        public int RemainingCount(DayEventType type)
        {
            var save = SaveManager.Current;
            int count = 0;

            if (type == DayEventType.ShortVDO)
            {
                foreach (var item in shortVdoPool)
                {
                    if (IsAvailableQuiet(item, save)) count++;
                }
            }
            else if (type == DayEventType.Minigame)
            {
                foreach (var item in minigamePool)
                {
                    if (IsAvailableQuiet(item, save)) count++;
                }
            }

            return count;
        }

        // Same test as IsAvailable without the authoring warning - this one runs in loops.
        private static bool IsAvailableQuiet(DayEventData item, SaveData save) =>
            item != null && item.IsPlayable() && !save.IsConsumed(item.eventId);

        private void Log(string message)
        {
            if (showDebugInfo) Debug.Log($"[RandomEventDirector] {message}", this);
        }

        [ContextMenu("Log Pool State")]
        private void LogPoolState()
        {
            Debug.Log(
                $"[RandomEventDirector] VDO {RemainingCount(DayEventType.ShortVDO)}/{shortVdoPool.Count} left, " +
                $"Minigame {RemainingCount(DayEventType.Minigame)}/{minigamePool.Count} left.", this);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Fills both pools with every matching asset in the project, so adding a new event is
        /// "create the asset, press this" rather than remembering to drag it into a list.
        /// Editor-only - the pools are still plain serialized lists you can hand-edit.
        /// </summary>
        [ContextMenu("Find All Events In Project")]
        private void FindAllEventsInProject()
        {
            shortVdoPool.Clear();
            minigamePool.Clear();

            foreach (var guid in UnityEditor.AssetDatabase.FindAssets($"t:{nameof(ShortVDOData)}"))
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<ShortVDOData>(path);
                if (asset != null) shortVdoPool.Add(asset);
            }

            foreach (var guid in UnityEditor.AssetDatabase.FindAssets($"t:{nameof(MinigameData)}"))
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<MinigameData>(path);
                if (asset != null) minigamePool.Add(asset);
            }

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[RandomEventDirector] found {shortVdoPool.Count} VDO and {minigamePool.Count} minigame assets.", this);
        }

        /// <summary>Clears the 'already watched' list in the save so every event can play again.</summary>
        [ContextMenu("Reset Consumed Events (save)")]
        private void ResetConsumedEvents()
        {
            SaveManager.Current.consumedEventIds.Clear();
            SaveManager.Save();
            Debug.Log("[RandomEventDirector] consumed-event history cleared.", this);
        }
#endif
    }
}
