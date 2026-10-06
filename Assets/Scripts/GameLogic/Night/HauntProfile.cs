using System.Collections.Generic;
using GameLogic.Data;
using UnityEngine;

namespace GameLogic.Night
{
    /// <summary>
    /// Which haunt loops (Radio Check / Camera Betrayal /
    /// Impostor Case) a night is allowed to schedule, and how many. Mirrors
    /// <see cref="GlitchProfile"/>'s shape on purpose - same weighted-pick idea, same reason:
    /// a night's character is data, not code.
    /// </summary>
    [CreateAssetMenu(fileName = "HauntProfile", menuName = "Give Me A Sign/Haunt Profile")]
    public class HauntProfile : ScriptableObject
    {
        [System.Serializable]
        public class LoopWeight
        {
            public HauntLoopId loop = HauntLoopId.RadioCheck;
            public bool enabled = true;
            [Tooltip("Relative chance of being picked. 0 = never.")]
            [Min(0f)] public float weight = 1f;
            [Tooltip("This loop is never picked before this night.")]
            [Min(1)] public int minNightIndex = 1;
            [Tooltip("From this night on the weight is multiplied by Boost Multiplier (e.g. 4 = after night 3). 0 = never boosted.")]
            [Min(0)] public int boostFromNight;
            [Tooltip("How much likelier this loop gets once Boost From Night is reached.")]
            [Min(1f)] public float boostMultiplier = 2f;

            public float WeightOn(int nightIndex) =>
                Mathf.Max(0f, weight) * (boostFromNight > 0 && nightIndex >= boostFromNight ? boostMultiplier : 1f);
        }

        [Tooltip("Haunt loops this night may schedule, with relative weights.")]
        public List<LoopWeight> loops = new List<LoopWeight>
        {
            new LoopWeight { loop = HauntLoopId.RadioCheck, weight = 1f, minNightIndex = 1 },
        };

        [Header("How many")]
        [Tooltip("Scheduled haunt beats on night 1.")]
        [Min(0)] public int baseHauntCount = 1;

        [Tooltip("Extra beats per night after the first.")]
        [Min(0f)] public float hauntGrowthPerNight = 0.5f;

        [Tooltip("Hard ceiling, so a late night can't stack haunt beats on top of every anomaly.")]
        [Min(0)] public int maxHauntCount = 4;

        public int HauntCountFor(int nightIndex)
        {
            int extra = Mathf.RoundToInt(hauntGrowthPerNight * Mathf.Max(0, nightIndex - 1));
            return Mathf.Clamp(baseHauntCount + extra, 0, maxHauntCount);
        }

        public HauntLoopId PickLoop(int nightIndex, System.Random rng)
        {
            if (loops == null || loops.Count == 0) return HauntLoopId.None;

            float total = 0f;
            foreach (var entry in loops)
            {
                if (entry == null || !entry.enabled || entry.minNightIndex > nightIndex) continue;
                total += entry.WeightOn(nightIndex);
            }

            if (total <= 0f) return HauntLoopId.None;

            double roll = rng.NextDouble() * total;
            foreach (var entry in loops)
            {
                if (entry == null || !entry.enabled || entry.minNightIndex > nightIndex) continue;

                roll -= entry.WeightOn(nightIndex);
                if (roll <= 0d) return entry.loop;
            }

            // Floating-point slack only; walk back to the last eligible entry.
            for (int i = loops.Count - 1; i >= 0; i--)
            {
                var entry = loops[i];
                if (entry != null && entry.enabled && entry.minNightIndex <= nightIndex && entry.weight > 0f)
                    return entry.loop;
            }

            return HauntLoopId.None;
        }
    }
}
