using System;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace GameLogic
{
    /// <summary>Shows today's date from the player's own computer (DateTime.Now) in a TMP label, e.g. the DayPanel's DayText.</summary>
    public class DayPanelDate : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI text;

        [Tooltip("Standard .NET date format. \"MMM. d yyyy\" -> OCT. 6 2026.")]
        [SerializeField] private string format = "MMM. d yyyy";
        [SerializeField] private bool uppercase = true;
        [Tooltip("Format with the invariant culture so a Thai (Buddhist calendar) PC still shows 2026 and English month names. Off uses the player's own culture.")]
        [SerializeField] private bool invariantCulture = true;
        [Tooltip("How often to check whether the date changed (midnight rollover), in real seconds.")]
        [Min(0.5f)] [SerializeField] private float checkIntervalSeconds = 30f;

        private DateTime _shownDate = DateTime.MinValue;
        private float _nextCheck;

        void OnEnable() => Refresh(force: true);

        void Update()
        {
            if (Time.unscaledTime < _nextCheck) return; // unscaled: still ticks while paused

            _nextCheck = Time.unscaledTime + checkIntervalSeconds;
            Refresh(force: false);
        }

        public void Refresh(bool force)
        {
            if (text == null) return;

            var today = DateTime.Now.Date;
            if (!force && today == _shownDate) return;
            _shownDate = today;

            var culture = invariantCulture ? CultureInfo.InvariantCulture : CultureInfo.CurrentCulture;
            string label = today.ToString(format, culture);
            text.text = uppercase ? label.ToUpper(culture) : label;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (!Application.isPlaying) Refresh(force: true);
        }
#endif
    }
}
