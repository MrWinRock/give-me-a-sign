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
        public class RowConfig
        {
            public string label = "Master volume";
            public Bus bus = Bus.Master;
        }

        [System.Serializable]
        public class Config
        {
            public RowConfig[] rows =
            {
                new RowConfig { label = "Master volume", bus = Bus.Master },
                new RowConfig { label = "Music", bus = Bus.Music },
                new RowConfig { label = "Sound effects", bus = Bus.Sfx },
            };

            [Tooltip("Slider range in percent.")]
            public float min = 0f;
            public float max = 100f;
            [Tooltip("Values snap to multiples of this (1 = whole numbers).")]
            [Min(1f)] public float step = 1f;

            [Header("Row layout")]
            public float labelWidth = 104f;
            public float valueWidth = 38f;
            public float rowHeight = 22f;
            public float rowSpacing = 6f;
            public float thumbWidth = 11f;
            public float thumbHeight = 21f;
            public string valueFormat = "{0}%";
        }

        private class Row
        {
            public Bus bus;
            public Slider slider;
            public TextMeshProUGUI valueText;
        }

        private Config _config;
        private readonly System.Collections.Generic.List<Row> _rows = new System.Collections.Generic.List<Row>();

        public static AudioSettingsPanel Build(Transform parent, Config config, XPControlStyle style, TMP_FontAsset font)
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
            panel._config = config;

            foreach (var rowConfig in config.rows)
                panel.AddRow(rowConfig, style, font);

            panel.Refresh();
            return panel;
        }

        private void AddRow(RowConfig rowConfig, XPControlStyle style, TMP_FontAsset font)
        {
            var rowGo = new GameObject($"Row_{rowConfig.label}", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            rowGo.transform.SetParent(transform, false);
            XPControls.SetPreferred(rowGo, height: _config.rowHeight);

            var layout = rowGo.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var label = XPControls.Text(rowGo.transform, "Label", font, rowConfig.label, style.labelSize, style.label, false, TextAlignmentOptions.MidlineLeft);
            XPControls.SetPreferred(label.gameObject, width: _config.labelWidth);

            var slider = XPControls.CreateSlider(rowGo.transform, style, _config.min, _config.max, _config.rowHeight, _config.thumbWidth, _config.thumbHeight);

            var value = XPControls.Text(rowGo.transform, "Value", font, "", style.labelSize, style.value, true, TextAlignmentOptions.MidlineRight);
            XPControls.SetPreferred(value.gameObject, width: _config.valueWidth);

            var row = new Row { bus = rowConfig.bus, slider = slider, valueText = value };
            slider.onValueChanged.AddListener(v => OnSliderChanged(row, v));
            _rows.Add(row);
        }

        // Pull the saved volumes into the sliders (call whenever the host window opens).
        public void Refresh()
        {
            var audio = AudioManager.Instance;
            foreach (var row in _rows)
            {
                row.slider.interactable = audio != null;
                float percent = audio != null ? Get(audio, row.bus) * 100f : _config.min;
                row.slider.SetValueWithoutNotify(Snap(percent));
                row.valueText.text = audio != null ? Format(row.slider.value) : "-";
            }
        }

        private void OnSliderChanged(Row row, float value)
        {
            float snapped = Snap(value);
            if (!Mathf.Approximately(snapped, value))
                row.slider.SetValueWithoutNotify(snapped);

            row.valueText.text = Format(snapped);

            var audio = AudioManager.Instance;
            if (audio == null) return;

            Set(audio, row.bus, Mathf.Clamp01(snapped / 100f)); // AudioManager persists + applies
        }

        private float Snap(float value)
        {
            float step = Mathf.Max(1f, _config.step);
            return Mathf.Clamp(Mathf.Round(value / step) * step, _config.min, _config.max);
        }

        private string Format(float value) => string.Format(_config.valueFormat, Mathf.RoundToInt(value));

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
