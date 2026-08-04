using System.Collections.Generic;
using Report;
using UnityEngine;

namespace GameLogic.Night
{
    /// <summary>
    /// Which form glitches a night is allowed to schedule, and how heavily. Referenced by the
    /// generated <see cref="NightPlan"/> so a night's glitch character is data, not code.
    ///
    /// This governs the SCHEDULED beats only. GlitchDirector's own ambient rolls and scripted
    /// report-count/game-hour beats are unaffected - they layer on top.
    /// </summary>
    [CreateAssetMenu(fileName = "GlitchProfile", menuName = "Give Me A Sign/Glitch Profile")]
    public class GlitchProfile : ScriptableObject
    {
        [System.Serializable]
        public class TypeWeight
        {
            public GlitchType type;
            [Tooltip("Relative chance of being picked. 0 = never.")]
            [Min(0f)] public float weight = 1f;
        }

        [Tooltip("Glitch types this night may schedule, with relative weights. Leave empty to allow all types equally.")]
        public List<TypeWeight> weights = new List<TypeWeight>();

        [Tooltip("GlitchDirector intensity multiplier applied for the whole night. 1 = normal.")]
        [Min(0.1f)] public float intensity = 1f;

        [Tooltip("Random stagger added to each scheduled glitch so several near each other don't flash on the same frame.")]
        [Min(0f)] public float maxFireDelay = 0.6f;

        /// <summary>
        /// Weighted pick using the caller's RNG. Falls back to a uniform pick across all glitch
        /// types when no weights are configured, so a blank profile still works.
        /// </summary>
        public GlitchType PickType(System.Random rng)
        {
            float total = 0f;
            if (weights != null)
            {
                foreach (var entry in weights)
                {
                    if (entry != null) total += Mathf.Max(0f, entry.weight);
                }
            }

            if (total <= 0f)
            {
                var all = (GlitchType[])System.Enum.GetValues(typeof(GlitchType));
                return all[rng.Next(all.Length)];
            }

            double roll = rng.NextDouble() * total;
            foreach (var entry in weights)
            {
                if (entry == null) continue;

                roll -= Mathf.Max(0f, entry.weight);
                if (roll <= 0d) return entry.type;
            }

            // Floating-point slack only; the last positive entry is the right answer.
            for (int i = weights.Count - 1; i >= 0; i--)
            {
                if (weights[i] != null && weights[i].weight > 0f) return weights[i].type;
            }

            return GlitchType.StatusIntrusion;
        }
    }
}
