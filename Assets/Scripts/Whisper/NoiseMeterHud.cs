using DG.Tweening;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Whisper
{
    /// <summary>
    /// CCTV-style Noise Meter OSD (the NoiseMeter prefab, bottom-centre of the gameplay Canvas): "NOISE" + zone name,
    /// a row of segments that light left to right, and LOW / NORMAL / LOUD under their zones. It only DISPLAYS what
    /// <see cref="NoiseMeter"/> measures - the same value and thresholds the voice rules judge with - and glitch
    /// effects on it are purely visual.
    /// </summary>
    public class NoiseMeterHud : MonoBehaviour
    {
        [Header("Refs (set in the prefab)")]
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TextMeshProUGUI labelText;
        [SerializeField] private TextMeshProUGUI stateText;
        [SerializeField] private Image[] segments = new Image[0];
        [SerializeField] private RectTransform zoneLabelRow;
        [SerializeField] private TextMeshProUGUI lowZoneLabel;
        [SerializeField] private TextMeshProUGUI normalZoneLabel;
        [SerializeField] private TextMeshProUGUI loudZoneLabel;
        [SerializeField] private TextMeshProUGUI debugText;

        [Header("Text")]
        [SerializeField] private string headerLabel = "NOISE";
        [SerializeField] private string standbyLabel = "STANDBY";
        [SerializeField] private string lowName = "LOW";
        [SerializeField] private string normalName = "NORMAL";
        [SerializeField] private string loudName = "LOUD";

        [Header("Colours")]
        [SerializeField] private Color lowColor = new Color32(0x9F, 0xB0, 0x9F, 0xFF);
        [SerializeField] private Color normalColor = new Color32(0xE8, 0xE8, 0xE0, 0xFF);
        [Tooltip("Same red as the REC dot (XPTheme.recDotColor).")]
        [SerializeField] private Color loudColor = new Color32(0xCC, 0x22, 0x22, 0xFF);
        [SerializeField] private Color unlitColor = new Color32(0x24, 0x27, 0x24, 0xFF);
        [SerializeField] private Color headerColor = new Color32(0xB8, 0xB8, 0xB0, 0xFF);
        [SerializeField] private Color standbyColor = new Color32(0x8A, 0x8A, 0x84, 0xFF);
        [SerializeField] private Color zoneLabelColor = new Color32(0x8A, 0x8A, 0x84, 0xFF);
        [SerializeField] private Color feedbackColor = new Color32(0xFF, 0xB0, 0x40, 0xFF);

        [Header("Behaviour")]
        [Tooltip("Dim but visible while V is not held, so the player learns where the meter is.")]
        [Range(0f, 1f)] [SerializeField] private float standbyAlpha = 0.45f;
        [SerializeField] private bool hideWhenStandby;
        [Tooltip("How long the highest recent segment stays lit before it starts to fall.")]
        [Min(0f)] [SerializeField] private float peakHoldSeconds = 0.6f;
        [Tooltip("How fast the held peak falls afterwards (meter units per second).")]
        [Min(0.05f)] [SerializeField] private float peakFallSpeed = 0.5f;
        [Tooltip("How long a TOO LOUD / TOO QUIET message replaces the zone name.")]
        [Min(0.2f)] [SerializeField] private float feedbackSeconds = 1.5f;
        [Tooltip("Unlit segments of the zone the current threat asks for are lit this much, so the target is visible.")]
        [Range(0f, 1f)] [SerializeField] private float requirementTint = 0.3f;

        [Header("Debug")]
        [Tooltip("Shows the live value, zone and last utterance's stats so the thresholds can be tuned in Play Mode.")]
        [SerializeField] private bool showDebugOverlay;

        public bool ShowDebugOverlay
        {
            get => showDebugOverlay;
            set => showDebugOverlay = value;
        }

        private NoiseMeter _meter;
        private float _peak;
        private float _peakTimer;
        private float _feedbackUntil;
        private string _feedbackText;
        private bool _feedbackBlink;
        private Tween _feedbackTween;

        void Awake()
        {
            if (labelText != null) { labelText.text = headerLabel; labelText.color = headerColor; }
            if (lowZoneLabel != null) lowZoneLabel.text = lowName;
            if (normalZoneLabel != null) normalZoneLabel.text = normalName;
            if (loudZoneLabel != null) loudZoneLabel.text = loudName;
            if (debugText != null) debugText.gameObject.SetActive(false);
        }

        void OnDisable()
        {
            if (_meter != null) _meter.VolumeMissed -= OnVolumeMissed;
            _meter = null;
            KillFeedback();
        }

        void Update()
        {
            Bind();

            if (_meter == null)
            {
                if (group != null) group.alpha = 0f;
                return;
            }

            bool active = _meter.IsActive;
            bool visible = _meter.HasMicrophone && !(hideWhenStandby && !active);
            if (group != null) group.alpha = !visible ? 0f : active ? 1f : standbyAlpha;
            if (!visible) return;

            float low = _meter.WhisperEdge, high = _meter.ShoutEdge;
            LayoutZoneLabels(low, high);
            DrawSegments(active, low, high);
            DrawState(active);
            DrawDebug();
        }

        // The meter can appear after this HUD wakes (scene order), so connect lazily.
        private void Bind()
        {
            var current = NoiseMeter.Instance;
            if (current == _meter) return;

            if (_meter != null) _meter.VolumeMissed -= OnVolumeMissed;
            _meter = current;
            if (_meter != null) _meter.VolumeMissed += OnVolumeMissed;
        }

        private void DrawSegments(bool active, float low, float high)
        {
            int count = segments.Length;
            if (count == 0) return;

            float position = active ? _meter.Normalized : 0f;
            int lit = Mathf.Clamp(Mathf.CeilToInt(position * count - 0.001f), 0, count);

            // Peak hold: the highest recent segment stays lit, then falls slowly.
            if (!active) { _peak = 0f; _peakTimer = 0f; }
            else if (position >= _peak) { _peak = position; _peakTimer = peakHoldSeconds; }
            else if (_peakTimer > 0f) _peakTimer -= Time.unscaledDeltaTime;
            else _peak = Mathf.Max(position, _peak - peakFallSpeed * Time.unscaledDeltaTime);
            int peakIndex = Mathf.Clamp(Mathf.CeilToInt(_peak * count - 0.001f) - 1, -1, count - 1);

            var required = _meter.CurrentRequirementNow;

            for (int i = 0; i < count; i++)
            {
                if (segments[i] == null) continue;

                var zone = ZoneOf(i, count, low, high);
                var zoneColor = ColorOf(zone);

                if (i < lit) segments[i].color = zoneColor;
                else if (i == peakIndex && active) segments[i].color = Color.Lerp(unlitColor, zoneColor, 0.85f);
                else segments[i].color = IsRequired(zone, required) ? Color.Lerp(unlitColor, zoneColor, requirementTint) : unlitColor;
            }
        }

        private void DrawState(bool active)
        {
            if (stateText == null) return;

            if (Time.unscaledTime < _feedbackUntil)
            {
                stateText.text = _feedbackText;
                stateText.color = feedbackColor;
                return;
            }

            if (_feedbackTween != null) KillFeedback();

            if (!active)
            {
                stateText.text = standbyLabel;
                stateText.color = standbyColor;
                return;
            }

            stateText.text = NameOf(_meter.Level);
            stateText.color = ColorOf(_meter.Level);
        }

        private void OnVolumeMissed(bool tooLoud)
        {
            if (stateText == null) return;

            var id = tooLoud ? MessageId.ReportTooLoud : MessageId.ReportTooQuiet;
            _feedbackText = PlayerMessages.Text(id);
            _feedbackUntil = Time.unscaledTime + feedbackSeconds;

            KillFeedback();
            _feedbackBlink = PlayerMessages.Blink(id);
            if (_feedbackBlink) _feedbackTween = TextBlink.Start(stateText);
        }

        private void KillFeedback()
        {
            _feedbackTween?.Kill();
            _feedbackTween = null;
            if (stateText != null) stateText.alpha = 1f;
        }

        // Zone labels sit centred under their zones, placed from the same thresholds as the segments.
        private void LayoutZoneLabels(float low, float high)
        {
            Center(lowZoneLabel, 0f, low);
            Center(normalZoneLabel, low, high);
            Center(loudZoneLabel, high, 1f);
        }

        private static void Center(TextMeshProUGUI label, float from, float to)
        {
            if (label == null) return;

            var rect = label.rectTransform;
            float x = (from + to) * 0.5f;
            rect.anchorMin = new Vector2(x, 0f);
            rect.anchorMax = new Vector2(x, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
        }

        private void DrawDebug()
        {
            if (debugText == null) return;

            debugText.gameObject.SetActive(showDebugOverlay);
            if (!showDebugOverlay) return;

            var last = _meter.LastUtterance;
            debugText.text =
                $"N {_meter.Normalized:0.00}  {_meter.Level}  zones {_meter.WhisperEdge:0.00}/{_meter.ShoutEdge:0.00}  floor {MicCalibration.NoiseFloor:0.0000}  active {_meter.IsActive}\n" +
                $"last: peak {last.peak:0.00} avg {last.average:0.00} {last.duration:0.0}s  need {_meter.CurrentRequirementNow}";
        }

        private static NoiseLevel ZoneOf(int index, int count, float low, float high)
        {
            float centre = (index + 0.5f) / count;
            return centre < low ? NoiseLevel.Low : centre < high ? NoiseLevel.Normal : NoiseLevel.Loud;
        }

        // StayQuiet tints nothing: no zone is safe, the stealth anomaly wants no voice at all.
        private static bool IsRequired(NoiseLevel zone, NoiseMeter.Requirement required) =>
            required == NoiseMeter.Requirement.BeLoud && zone == NoiseLevel.Loud;

        private Color ColorOf(NoiseLevel level) =>
            level == NoiseLevel.Low ? lowColor : level == NoiseLevel.Normal ? normalColor : loudColor;

        private string NameOf(NoiseLevel level) =>
            level == NoiseLevel.Low ? lowName : level == NoiseLevel.Normal ? normalName : loudName;
    }
}
