using System;
using TMPro;
using UnityEngine;

namespace MainMenu
{
    /// <summary>
    /// The system-tray clock. Shows in-fiction time by default - the desktop is meant to read
    /// as 2:34 AM on the night of the shift, not whatever time the player happens to launch at.
    /// Flip <see cref="useRealTime"/> if the real wall clock is ever wanted instead.
    /// </summary>
    public class TaskbarClock : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI clockText;

        [Tooltip("The time shown when useRealTime is off. This is the fiction's clock, not the player's.")]
        [SerializeField] private string fictionalTime = "2:34 AM";

        [SerializeField] private bool useRealTime;
        [Tooltip("DateTime format used when useRealTime is on.")]
        [SerializeField] private string realTimeFormat = "h:mm tt";

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

            clockText.text = useRealTime
                ? DateTime.Now.ToString(realTimeFormat)
                : fictionalTime;
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
