using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>Keeps a TMP label showing a slider's value (scaled, formatted), including after SetValueWithoutNotify.</summary>
    [DisallowMultipleComponent]
    public class SliderValueLabel : MonoBehaviour
    {
        [SerializeField] private Slider slider;
        [SerializeField] private TextMeshProUGUI label;
        [Tooltip("Slider value is multiplied by this before display (100 turns 0..1 into percent).")]
        [SerializeField] private float multiplier = 100f;
        [SerializeField] private string format = "{0}%";

        private float _shown = float.NaN;

        void OnEnable() => Refresh();

        void LateUpdate()
        {
            if (slider != null && !Mathf.Approximately(slider.value, _shown)) Refresh();
        }

        private void Refresh()
        {
            if (slider == null || label == null) return;

            _shown = slider.value;
            label.text = string.Format(format, Mathf.RoundToInt(_shown * multiplier));
        }
    }
}
