using GameLogic;
using GameLogic.SpawnAndTime;
using Score;
using UnityEngine;

namespace Report
{
    public class GlitchStateSource : MonoBehaviour, IGlitchStateSource
    {
        [SerializeField] private NightTimer nightTimer;
        [SerializeField] private int consecutiveFailures;

        public int ReportCount =>
            ScoreManager.Instance != null ? ScoreManager.Instance.GetCurrentScore() : 0;

        public int GameHour =>
            nightTimer != null ? Mathf.FloorToInt(nightTimer.GetGameTimeHours()) : 0;

        public float ShiftProgress01 =>
            nightTimer != null ? nightTimer.GetNormalizedTime() : 0f;

        public int ConsecutiveFailures => consecutiveFailures;

        public GlitchAnomalyState AnomalyState
        {
            get
            {
                // Active = กำลังพุ่งเข้าหาผู้เล่น (ต้องสวดไล่), Passive = อยู่เฉยๆ ให้ตรวจเจอ
                foreach (var a in Anomaly.ActiveAnomalies)
                {
                    if (a != null && a.CanBePrayerBanished())
                        return GlitchAnomalyState.Active;
                }
                return Anomaly.ActiveAnomalies.Count > 0
                    ? GlitchAnomalyState.Passive
                    : GlitchAnomalyState.None;
            }
        }

        public void RegisterReportResult(bool success) =>
            consecutiveFailures = success ? 0 : consecutiveFailures + 1;
    }
}