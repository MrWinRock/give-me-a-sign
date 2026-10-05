using DG.Tweening;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Whisper
{
    /// <summary>Bottom-right mic indicator for <see cref="GlobalPushToTalk"/> (the PushToTalkHud prefab in the gameplay Canvas): a red dot that blinks while talking, the "[V] MIC" / "REC" label, a status line and a hint line.</summary>
    public class PushToTalkHud : MonoBehaviour
    {
        [Header("Refs (set in the prefab)")]
        [SerializeField] private Image dot;
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private TextMeshProUGUI status;
        [SerializeField] private TextMeshProUGUI hint;

        [Header("Look")]
        [Tooltip("The dot is always this red; only its blinking changes.")]
        [SerializeField] private Color dotColor = new Color(0.9f, 0.15f, 0.15f, 1f);
        [SerializeField] private Color idleLabelColor = new Color(1f, 1f, 1f, 0.45f);
        [SerializeField] private Color talkingLabelColor = new Color(0.9f, 0.15f, 0.15f, 1f);
        [SerializeField] private string idleText = "[V] MIC";
        [SerializeField] private string talkingText = "REC";

        [Header("Blink while talking")]
        [Range(0f, 1f)] [SerializeField] private float blinkLowAlpha = 0.25f;
        [Min(0.05f)] [SerializeField] private float blinkSeconds = 0.4f;

        private Tween _pulse;
        private Tween _statusFade;
        private Tween _hintBlink;

        void Awake()
        {
            if (status != null) status.color = new Color(1f, 1f, 1f, 0f);
            if (hint != null) hint.text = "";
            SetTalking(false);
        }

        void OnDisable() => KillTweens();

        void OnDestroy() => KillTweens();

        // The stealth countdown re-sends its text every second - only restart the flash when the blink state changes.
        public void SetHint(string text, bool blink = false)
        {
            if (hint == null) return;

            hint.text = text ?? "";

            bool shouldBlink = blink && !string.IsNullOrEmpty(text);
            if (shouldBlink == (_hintBlink != null)) return;

            _hintBlink?.Kill();
            _hintBlink = null;
            hint.alpha = 1f;
            if (shouldBlink) _hintBlink = TextBlink.Start(hint);
        }

        public void SetTalking(bool talking, string suffix = null)
        {
            _pulse?.Kill();
            _pulse = null;

            if (label != null)
            {
                string text = talking ? talkingText : idleText;
                label.text = talking && !string.IsNullOrEmpty(suffix) ? $"{text} {suffix}" : text;
                label.color = talking ? talkingLabelColor : idleLabelColor;
            }

            if (dot == null) return;

            dot.color = dotColor; // solid red; blinking is the only "talking" signal
            if (talking)
            {
                _pulse = dot.DOFade(blinkLowAlpha, blinkSeconds)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(true)
                    .SetLink(dot.gameObject);
            }
        }

        public void ShowStatus(string text, Color color, bool blink = false)
        {
            if (status == null) return;

            _statusFade?.Kill();
            status.text = text;
            status.color = color;

            if (blink)
            {
                // Flash for ~2.5s, then fade out like a normal message.
                var seq = DOTween.Sequence().SetUpdate(true).SetLink(status.gameObject);
                seq.Append(DOTween.To(() => status.alpha, a => status.alpha = a, 0.15f, TextBlink.DefaultPeriod)
                    .SetEase(Ease.InOutSine).SetLoops(8, LoopType.Yoyo));
                seq.Append(DOTween.To(() => status.alpha, a => status.alpha = a, 0f, 0.5f));
                _statusFade = seq;
                return;
            }

            _statusFade = DOTween.To(() => status.color.a, a => status.color = new Color(color.r, color.g, color.b, a), 0f, 0.8f)
                .SetDelay(1.5f)
                .SetUpdate(true)
                .SetLink(status.gameObject);
        }

        private void KillTweens()
        {
            _pulse?.Kill();
            _pulse = null;
            _hintBlink?.Kill();
            _hintBlink = null;
            _statusFade?.Kill();
            _statusFade = null;
        }
    }
}
