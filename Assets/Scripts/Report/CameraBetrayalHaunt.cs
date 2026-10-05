using System.Collections.Generic;
using GameLogic.Data;
using GameLogic.Night;
using UnityEngine;
// Aliased, not imported: this file uses UnityEngine's [Min], and a plain `using Gaskellgames;` makes it ambiguous (CS0104).
using GG = Gaskellgames;

namespace Report
{
    /// <summary>
    /// HL-5 Camera Betrayal. Fires one random <see cref="CameraGlitchType"/> variant through
    /// <see cref="CameraFeedController"/> per beat - the same when/how split as every other haunt
    /// loop, and as FormGlitchController/GlitchDirector before it.
    /// </summary>
    [RequireComponent(typeof(CameraFeedController))]
    public class CameraBetrayalHaunt : MonoBehaviour, IHauntLoop
    {
        [System.Serializable]
        public class VariantWeight
        {
            public CameraGlitchType type;
            public bool enabled = true;
            [Tooltip("How likely this effect is picked, relative to the others (not a duration). 0 = never.")]
            [Min(0f)] public float weight = 1f;
            [Tooltip("How long the effect lasts, in seconds - a random value between the two ends each time.")]
            [GG.MinMaxSlider(0.5f, 15f, true)] public Vector2 durationRange = new Vector2(2f, 4f);
        }

        [Header("Variants: chance (Weight) and how long each lasts (Duration Range, seconds)")]
        [SerializeField]
        private List<VariantWeight> variants = new List<VariantWeight>
        {
            new VariantWeight { type = CameraGlitchType.Loop,      weight = 1.2f, durationRange = new Vector2(4f, 8f) },
            new VariantWeight { type = CameraGlitchType.Frozen,    weight = 1f,   durationRange = new Vector2(2f, 4f) },
            new VariantWeight { type = CameraGlitchType.Blackout,  weight = 1f,   durationRange = new Vector2(1.5f, 3f) },
            new VariantWeight { type = CameraGlitchType.GhostRoom, weight = 0.7f, durationRange = new Vector2(3f, 6f) },
            new VariantWeight { type = CameraGlitchType.Mirror,    weight = 0.5f, durationRange = new Vector2(3f, 6f) },
        };

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        public HauntLoopId LoopId => HauntLoopId.CameraBetrayal;
        public bool IsActive => _controller != null && _controller.IsGlitchActive;
        public bool IsExclusive => true;

        private CameraFeedController _controller;

        void Awake()
        {
            _controller = GetComponent<CameraFeedController>();
        }

        // No separate teardown-safety dance needed here: this loop holds no state of its own
        // (IsActive just reads the controller), and CameraFeedController's own OnDisable already
        // cancels and reverts everything cleanly.
        void OnEnable()
        {
            HauntDirector.Instance?.Register(this);

            // Touch CameraFeedHud.Instance eagerly so the camera label is already running
            // from the start of the night - if it only spawned lazily on the first glitch, the
            // player would have no "known-good" baseline to notice a frozen clock against.
            _ = CameraFeedHud.Instance;
        }

        void OnDisable() => HauntDirector.ExistingInstance?.Unregister(this);

        public void Trigger(HauntBeat beat)
        {
            if (IsActive) return; // HauntDirector already guards this - belt and braces

            var variant = PickVariant();
            float duration = Random.Range(Mathf.Min(variant.durationRange.x, variant.durationRange.y), Mathf.Max(variant.durationRange.x, variant.durationRange.y));
            bool started = _controller.PlayGlitch(variant.type, Mathf.Max(0.1f, duration));

            if (showDebugInfo)
                Debug.Log($"CameraBetrayalHaunt: fired {variant.type} for {duration:0.0}s (started={started}).", this);
        }

        // Debug panel: runs one effect now with its configured duration range, ignoring weights and the haunt schedule.
        public bool DebugTrigger(CameraGlitchType type)
        {
            var range = new Vector2(2f, 4f);
            foreach (var v in variants)
            {
                if (v != null && v.type == type) { range = v.durationRange; break; }
            }

            float duration = Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
            return _controller.PlayGlitch(type, Mathf.Max(0.1f, duration));
        }

        // Mirror needs the player's wallpaper; without one (not Windows, nothing found, or switched off) it never rolls.
        private static bool IsEligible(VariantWeight v, bool mirrorOk) =>
            v != null && v.enabled && (v.type != CameraGlitchType.Mirror || mirrorOk);

        private VariantWeight PickVariant()
        {
            bool mirrorOk = _controller.MirrorAvailable;

            float total = 0f;
            foreach (var v in variants)
                if (IsEligible(v, mirrorOk)) total += Mathf.Max(0f, v.weight);

            if (total <= 0f) return new VariantWeight { type = CameraGlitchType.Blackout, durationRange = new Vector2(1.5f, 3f) };

            float roll = Random.value * total;
            foreach (var v in variants)
            {
                if (!IsEligible(v, mirrorOk)) continue;
                roll -= Mathf.Max(0f, v.weight);
                if (roll <= 0f) return v;
            }

            // Floating-point slack only; walk back to the last eligible entry.
            for (int i = variants.Count - 1; i >= 0; i--)
            {
                if (IsEligible(variants[i], mirrorOk)) return variants[i];
            }

            return new VariantWeight { type = CameraGlitchType.Blackout, durationRange = new Vector2(1.5f, 3f) };
        }
    }
}
