using System;
using GameLogic.Flow;
using TMPro;
using UnityEngine;

namespace MainMenu
{
    /// <summary>
    /// The system-tray clock. Shows in-fiction time by default - the desktop is meant to read
    /// as 2:34 AM on the night of the shift, not whatever time the player happens to launch at.
    /// Flip <see cref="useRealTime"/> if the real wall clock is ever wanted instead.
    ///
    /// Also appends the campaign day (GameFlowManager.CurrentDay) so the player can tell which
    /// shift they're on without opening a window.
    /// </summary>
    public class TaskbarClock : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI clockText;

        [Tooltip("The time shown when useRealTime is off. This is the fiction's clock, not the player's.")]
        [SerializeField] private string fictionalTime = "2:34 AM";

        [SerializeField] private bool useRealTime;
        [Tooltip("DateTime format used when useRealTime is on.")]
        [SerializeField] private string realTimeFormat = "h:mm tt";

        [Header("Day")]
        [Tooltip("Append the current campaign day next to the time.")]
        [SerializeField] private bool showDay = true;
        [Tooltip("Format for the day label. {0} is replaced with GameFlowManager.CurrentDay.")]
        [SerializeField] private string dayLabelFormat = "Night {0}";
        [Tooltip("Text placed between the time and the day label.")]
        [SerializeField] private string separator = "   ";

        public string FictionalTime
        {
            get => fictionalTime;
            set { fictionalTime = value; Refresh(); }
        }

        void OnEnable()
        {
            Refresh();

            if (useRealTime)
                InvokeRepeating(nameof(Refresh), 1f, 1f);
        }

        void OnDisable()
        {
            CancelInvoke(nameof(Refresh));
        }

        public void Refresh()
        {
            if (clockText == null) return;

            string time = useRealTime
                ? DateTime.Now.ToString(realTimeFormat)
                : fictionalTime;

            clockText.text = showDay ? time + separator + DayLabel() : time;
        }

        // Editor preview (not Application.isPlaying) shows Day 1 instead of touching the save
        // file - reading GameFlowManager.CurrentDay outside Play mode would load it from disk.
        private string DayLabel()
        {
            int day = Application.isPlaying ? GameFlowManager.CurrentDay : 1;
            return string.Format(dayLabelFormat, day);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (!Application.isPlaying)
                Refresh();
        }
#endif
    }
}
