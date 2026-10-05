using System.Collections.Generic;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Audio
{
    /// <summary>
    /// Master / Music / SFX trackbars with "NN%" readouts, bound to <see cref="AudioManager"/> - the one place
    /// volumes are applied and saved (PlayerPrefs Vol_*), so every screen showing this panel edits the same values.
    /// </summary>
    public class AudioSettingsPanel : MonoBehaviour
    {
        public enum Bus { Master, Music, Sfx }

        [System.Serializable]
        public class Row
        {
            public Bus bus;
            public Slider slider;
            public TextMeshProUGUI valueText;
        }

        [System.Serializable]
        public class RowConfig
        {
            public string label = "Master volume";
            public Bus bus = Bus.Master;
        }

        // Only used when Build() generates the rows (the prefab generator); an authored panel just uses its Rows.
        [System.Serializable]
        public class BuildConfig
        {
            public RowConfig[] rows =
            {
                new RowConfig { label = "Master volume", bus = Bus.Master },
                new RowConfig { label = "Music", bus = Bus.Music },
                new RowConfig { label = "Sound effects", bus = Bus.Sfx },
            };
            public float labelWidth = 104f;
            public float valueWidth = 38f;
            public float rowHeight = 22f;
            public float rowSpacing = 6f;
            public float thumbWidth = 11f;
            public float thumbHeight = 21f;
        }

        [Tooltip("Slider range in percent.")]
        [SerializeField] private float min = 0f;
        [SerializeField] private float max = 100f;
        [Tooltip("Values snap to multiples of this (1 = whole numbers).")]
        [Min(1f)] [SerializeField] private float step = 1f;
        [SerializeField] private string valueFormat = "{0}%";
        [SerializeField] private List<Row> rows = new List<Row>();

        void Awake()
        {
            foreach (var row in rows)
            {
                if (row.slider == null) continue;

                var captured = row;
                row.slider.minValue = min;
                row.slider.maxValue = max;
                row.slider.wholeNumbers = true;
                row.slider.onValueChanged.AddListener(v => OnSliderChanged(captured, v));
            }
        }

        // Every time the host window opens, show what is actually saved.
        void OnEnable() => Refresh();

        public static AudioSettingsPanel Build(Transform parent, BuildConfig config, XPControlStyle style, TMP_FontAsset font)
        {
            var go = new GameObject("AudioSettingsPanel", typeof(RectTransform), typeof(VerticalLayoutGroup));
            go.transform.SetParent(parent, false);

            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.spacing = config.rowSpacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var panel = go.AddComponent<AudioSettingsPanel>();
            foreach (var rowConfig in config.rows)
                panel.rows.Add(CreateRow(go.transform, rowConfig, config, style, font, panel.min, panel.max));

            return panel;
        }

        private static Row CreateRow(Transform parent, RowConfig rowConfig, BuildConfig config, XPControlStyle style, TMP_FontAsset font, float min, float max)
        {
            var rowGo = new GameObject($"Row_{rowConfig.label}", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            rowGo.transform.SetParent(parent, false);
            XPControls.SetPreferred(rowGo, height: config.rowHeight);

            var layout = rowGo.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var label = XPControls.Text(rowGo.transform, "Label", font, rowConfig.label, style.labelSize, style.label, false, TextAlignmentOptions.MidlineLeft);
            XPControls.SetPreferred(label.gameObject, width: config.labelWidth);

            var slider = XPControls.CreateSlider(rowGo.transform, style, min, max, config.rowHeight, config.thumbWidth, config.thumbHeight);

            var value = XPControls.Text(rowGo.transform, "Value", font, "0%", style.labelSize, style.value, true, TextAlignmentOptions.MidlineRight);
            XPControls.SetPreferred(value.gameObject, width: config.valueWidth);

            return new Row { bus = rowConfig.bus, slider = slider, valueText = value };
        }

        public void Refresh()
        {
            var audio = AudioManager.Instance;
            foreach (var row in rows)
            {
                if (row.slider == null) continue;

                row.slider.interactable = audio != null;
                float percent = audio != null ? Get(audio, row.bus) * 100f : min;
                row.slider.SetValueWithoutNotify(Snap(percent));
                if (row.valueText != null) row.valueText.text = audio != null ? Format(row.slider.value) : "-";
            }
        }

        private void OnSliderChanged(Row row, float value)
        {
            float snapped = Snap(value);
            if (!Mathf.Approximately(snapped, value))
                row.slider.SetValueWithoutNotify(snapped);

            if (row.valueText != null) row.valueText.text = Format(snapped);

            var audio = AudioManager.Instance;
            if (audio == null) return;

            Set(audio, row.bus, Mathf.Clamp01(snapped / 100f)); // AudioManager persists + applies
        }

        private float Snap(float value)
        {
            float s = Mathf.Max(1f, step);
            return Mathf.Clamp(Mathf.Round(value / s) * s, min, max);
        }

        private string Format(float value) => string.Format(valueFormat, Mathf.RoundToInt(value));

        private static float Get(AudioManager audio, Bus bus) =>
            bus == Bus.Master ? audio.MasterVolume : bus == Bus.Music ? audio.MusicVolume : audio.SfxVolume;

        private static void Set(AudioManager audio, Bus bus, float value)
        {
            if (bus == Bus.Master) audio.MasterVolume = value;
            else if (bus == Bus.Music) audio.MusicVolume = value;
            else audio.SfxVolume = value;
        }
    }
}
