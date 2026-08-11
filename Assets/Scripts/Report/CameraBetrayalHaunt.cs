using System.Collections.Generic;
using GameLogic.Data;
using GameLogic.Night;
using UnityEngine;

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
            [Min(0f)] public float weight = 1f;
        }

        [Header("Variant weights")]
        [SerializeField]
        private List<VariantWeight> variants = new List<VariantWeight>
        {
            new VariantWeight { type = CameraGlitchType.Loop,      weight = 1.2f },
            new VariantWeight { type = CameraGlitchType.Frozen,    weight = 1f },
            new VariantWeight { type = CameraGlitchType.Blackout,  weight = 1f },
            new VariantWeight { type = CameraGlitchType.GhostRoom, weight = 0.7f },
            new VariantWeight { type = CameraGlitchType.Mirror,    weight = 0.5f },
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

            // Touch CameraFeedHud.Instance eagerly so the watermark/timestamp is already running
            // from the start of the night - if it only spawned lazily on the first glitch, the
            // player would have no "known-good" baseline to notice a frozen clock against.
            _ = CameraFeedHud.Instance;
        }

        void OnDisable() => HauntDirector.ExistingInstance?.Unregister(this);

        public void Trigger(HauntBeat beat)
        {
            if (IsActive) return; // HauntDirector already guards this - belt and braces

            var type = PickVariant();
            bool started = _controller.PlayGlitch(type);

            if (showDebugInfo)
                Debug.Log($"CameraBetrayalHaunt: fired {type} (started={started}).", this);
        }

        private CameraGlitchType PickVariant()
        {
            float total = 0f;
            foreach (var v in variants)
                if (v != null && v.enabled) total += Mathf.Max(0f, v.weight);

            if (total <= 0f) return CameraGlitchType.Blackout;

            float roll = Random.value * total;
            foreach (var v in variants)
            {
                if (v == null || !v.enabled) continue;
                roll -= Mathf.Max(0f, v.weight);
                if (roll <= 0f) return v.type;
            }

            // Floating-point slack only; walk back to the last enabled entry.
            for (int i = variants.Count - 1; i >= 0; i--)
            {
                if (variants[i] != null && variants[i].enabled) return variants[i].type;
            }

            return CameraGlitchType.Blackout;
        }
    }
}
