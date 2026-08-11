using System.Collections.Generic;
using GameLogic.Data;
using Report;
using UnityEngine;

namespace GameLogic.Night
{
    /// <summary>
    /// Builds a night from a seed. Same seed in, same night out - every time.
    /// </summary>
    public class NightPlanGenerator
    {
        private readonly NightContentLibrary _library;
        private readonly List<RoomDefinition> _rooms;
        private readonly DifficultyProfile _difficulty;
        private readonly GlitchProfile _glitchProfile;
        private readonly HauntProfile _hauntProfile;

        public NightPlanGenerator(NightContentLibrary library, IReadOnlyList<RoomDefinition> rooms,
                                  DifficultyProfile difficulty, GlitchProfile glitchProfile,
                                  HauntProfile hauntProfile = null)
        {
            _library = library;
            _difficulty = difficulty;
            _glitchProfile = glitchProfile;
            _hauntProfile = hauntProfile;

            _rooms = new List<RoomDefinition>();
            if (rooms != null)
            {
                foreach (var room in rooms)
                {
                    if (room != null) _rooms.Add(room);
                }
            }
        }

        public string LastOutcome { get; private set; } = "(not run)";

        // ── generate → validate → retry ──────────────────────────────────────────────────

        public NightPlan GenerateValid(int nightIndex, float durationMinutes, int seed)
        {
            int maxAttempts = _difficulty != null ? _difficulty.maxAttempts : 50;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                var plan = Generate(nightIndex, durationMinutes, seed, attempt);

                if (NightPlanValidator.Validate(plan, _difficulty, _rooms.Count, out string reason))
                {
                    LastOutcome = attempt == 0
                        ? "accepted on first attempt"
                        : $"accepted on attempt {attempt + 1}";
                    return plan;
                }

                if (attempt == maxAttempts - 1)
                    LastOutcome = $"all {maxAttempts} attempts rejected (last: {reason}) - used fallback";
            }

            return GenerateFallback(nightIndex, durationMinutes, seed);
        }

        public NightPlan Generate(int nightIndex, float durationMinutes, int seed, int attempt = 0)
        {
            // The attempt number perturbs the stream so a retry differs, while the whole
            // sequence stays a pure function of the original seed.
            var rng = new System.Random(seed + attempt * 7919);

            var plan = new NightPlan
            {
                seed = seed,
                nightIndex = nightIndex,
                durationMinutes = durationMinutes,
                glitchProfile = _glitchProfile,
            };

            var selected = SelectAnomalies(nightIndex, rng);
            if (selected.Count > 0)
            {
                var times = LayOutTimes(selected, nightIndex, durationMinutes, rng);

                // Times may be fewer than selected kinds when the night is too short to fit them
                // all at a fair pace - better to run a shorter night than an unwinnable one.
                if (times.Count < selected.Count)
                    selected.RemoveRange(times.Count, selected.Count - times.Count);

                MoveCostliestLast(selected);
                var rooms = AssignRooms(selected, rng);

                for (int i = 0; i < selected.Count; i++)
                {
                    plan.anomalies.Add(new AnomalyPlacement
                    {
                        definition = selected[i],
                        room = rooms[i],
                        atMinute = times[i],
                    });
                }
            }

            PlaceGlitches(plan, nightIndex, durationMinutes, rng);
            PlaceHaunts(plan, nightIndex, durationMinutes, rng);

            plan.requiredScore = _difficulty != null
                ? _difficulty.RequiredScoreFor(plan.anomalies.Count, _difficulty.WinRatioFor(nightIndex))
                : plan.anomalies.Count;

            plan.penaltyAnomaliesPerWrongReport = _difficulty != null
                ? _difficulty.PenaltyAnomaliesFor(nightIndex)
                : 1;

            plan.SortByTime();
            return plan;
        }

        // ── which anomalies ──────────────────────────────────────────────────────────────

        private List<AnomalyDefinition> SelectAnomalies(int nightIndex, System.Random rng)
        {
            var selected = new List<AnomalyDefinition>();
            if (_library == null || _difficulty == null) return selected;

            var pool = _library.AvailableOn(nightIndex);
            if (pool.Count == 0)
            {
                Debug.LogWarning($"NightPlanGenerator: no anomaly kinds are unlocked on night {nightIndex}.");
                return selected;
            }

            int budget = _difficulty.ThreatBudgetFor(nightIndex);
            AnomalyDefinition previous = null;

            // Cheapest kind decides when the budget can no longer buy anything.
            int cheapest = int.MaxValue;
            foreach (var definition in pool) cheapest = Mathf.Min(cheapest, Mathf.Max(1, definition.threatCost));

            while (budget >= cheapest)
            {
                var affordable = Affordable(pool, budget);
                if (affordable.Count == 0) break;

                var candidates = WithoutRepeat(affordable, previous);
                var pick = candidates[rng.Next(candidates.Count)];

                selected.Add(pick);
                budget -= Mathf.Max(1, pick.threatCost);
                previous = pick;
            }

            return selected;
        }

        private static List<AnomalyDefinition> Affordable(List<AnomalyDefinition> pool, int budget)
        {
            var affordable = new List<AnomalyDefinition>();
            foreach (var definition in pool)
            {
                if (Mathf.Max(1, definition.threatCost) <= budget) affordable.Add(definition);
            }
            return affordable;
        }

        private static List<AnomalyDefinition> WithoutRepeat(List<AnomalyDefinition> options, AnomalyDefinition previous)
        {
            if (previous == null || options.Count <= 1) return options;

            var filtered = new List<AnomalyDefinition>();
            foreach (var option in options)
            {
                if (option != previous) filtered.Add(option);
            }

            return filtered.Count > 0 ? filtered : options;
        }

        private static void MoveCostliestLast(List<AnomalyDefinition> selected)
        {
            if (selected.Count < 2) return;

            int best = 0;
            for (int i = 1; i < selected.Count; i++)
            {
                if (selected[i].threatCost > selected[best].threatCost) best = i;
            }

            int last = selected.Count - 1;
            if (best == last) return;

            (selected[best], selected[last]) = (selected[last], selected[best]);
        }

        // ── when ─────────────────────────────────────────────────────────────────────────

        private List<float> LayOutTimes(List<AnomalyDefinition> selected, int nightIndex,
                                        float durationMinutes, System.Random rng)
        {
            var times = new List<float>();
            if (_difficulty == null || selected.Count == 0) return times;

            float startSeconds = Mathf.Max(0f, _difficulty.firstSpawnMinute) * 60f;
            float endSeconds = durationMinutes * 60f * Mathf.Clamp01(_difficulty.lastSpawnFraction);
            float usable = endSeconds - startSeconds;
            if (usable <= 0f) return times;

            // A slot has to hold the longest threat window in play, otherwise two anomalies could
            // be live at once and the player is asked to be in two rooms at once.
            float longestThreat = 0f;
            foreach (var definition in selected) longestThreat = Mathf.Max(longestThreat, definition.threatTimeoutSeconds);

            float requiredGap = Mathf.Max(_difficulty.MinimumSpacingFor(nightIndex), longestThreat);
            if (requiredGap <= 0f) requiredGap = 1f;

            int fits = Mathf.FloorToInt(usable / requiredGap) + 1;
            int count = Mathf.Min(selected.Count, Mathf.Max(1, fits));

            float slot = count > 1 ? usable / (count - 1) : usable;
            float jitterRoom = Mathf.Max(0f, slot - requiredGap);

            for (int i = 0; i < count; i++)
            {
                float baseSeconds = count > 1 ? startSeconds + i * slot : startSeconds + usable * 0.5f;

                // Jitter forward only, by less than the slack in the slot, so the guaranteed gap
                // survives even in the worst pairing of neighbouring jitters.
                float jitter = (float)rng.NextDouble() * jitterRoom;
                float seconds = Mathf.Clamp(baseSeconds + jitter - jitterRoom * 0.5f, startSeconds, endSeconds);

                times.Add(seconds / 60f);
            }

            times.Sort();
            return times;
        }

        // ── where ────────────────────────────────────────────────────────────────────────

        private List<RoomDefinition> AssignRooms(List<AnomalyDefinition> selected, System.Random rng)
        {
            var assigned = new List<RoomDefinition>(selected.Count);
            if (_rooms.Count == 0)
            {
                for (int i = 0; i < selected.Count; i++) assigned.Add(null);
                return assigned;
            }

            var bag = new List<RoomDefinition>();
            RoomDefinition previous = null;

            for (int i = 0; i < selected.Count; i++)
            {
                var allowed = _library != null
                    ? _library.RoomsFor(selected[i], _rooms)
                    : new List<RoomDefinition>(_rooms);

                if (allowed.Count == 0)
                {
                    // The kind's allowedRooms excludes every room this scene has - the validator
                    // will report it; place it somewhere so the plan is still inspectable.
                    assigned.Add(_rooms[rng.Next(_rooms.Count)]);
                    continue;
                }

                if (bag.Count == 0) RefillBag(bag, rng);

                var room = TakeFromBag(bag, allowed, previous, rng);
                assigned.Add(room);
                previous = room;
            }

            return assigned;
        }

        private void RefillBag(List<RoomDefinition> bag, System.Random rng)
        {
            bag.AddRange(_rooms);

            // Fisher-Yates on our own RNG, so bag order is part of the seed.
            for (int i = bag.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (bag[i], bag[j]) = (bag[j], bag[i]);
            }
        }

        private RoomDefinition TakeFromBag(List<RoomDefinition> bag, List<RoomDefinition> allowed,
                                           RoomDefinition previous, System.Random rng)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                bool avoidPrevious = pass == 0;

                for (int i = 0; i < bag.Count; i++)
                {
                    if (!allowed.Contains(bag[i])) continue;
                    if (avoidPrevious && bag[i] == previous) continue;

                    var room = bag[i];
                    bag.RemoveAt(i);
                    return room;
                }
            }

            // Nothing in the bag fits this kind - take any allowed room instead.
            return allowed[rng.Next(allowed.Count)];
        }

        // ── glitches ─────────────────────────────────────────────────────────────────────

        private void PlaceGlitches(NightPlan plan, int nightIndex, float durationMinutes, System.Random rng)
        {
            if (_difficulty == null) return;

            int count = _difficulty.GlitchCountFor(nightIndex);
            if (count <= 0) return;

            // Onboarding: the opening stretch stays clean so the form can be learned before it
            // starts lying.
            float startMinute = durationMinutes * Mathf.Clamp01(_difficulty.onboardingQuietFraction);
            float usable = durationMinutes - startMinute;
            if (usable <= 0f) return;

            float slot = usable / count;
            float maxDelay = _glitchProfile != null ? _glitchProfile.maxFireDelay : 0f;

            for (int i = 0; i < count; i++)
            {
                float atMinute = startMinute + i * slot + (float)rng.NextDouble() * slot;

                plan.glitches.Add(new GlitchBeat
                {
                    type = _glitchProfile != null
                        ? _glitchProfile.PickType(rng)
                        : (GlitchType)rng.Next(System.Enum.GetValues(typeof(GlitchType)).Length),
                    atMinute = Mathf.Min(atMinute, durationMinutes),
                    overrideText = "",
                    fireDelay = maxDelay > 0f ? (float)rng.NextDouble() * maxDelay : 0f,
                });
            }
        }

        // ── haunts ───────────────────────────────────────────────────────────────────────

        private void PlaceHaunts(NightPlan plan, int nightIndex, float durationMinutes, System.Random rng)
        {
            if (_difficulty == null || _hauntProfile == null) return;

            int count = _hauntProfile.HauntCountFor(nightIndex);
            if (count <= 0) return;

            float startMinute = durationMinutes * Mathf.Clamp01(_difficulty.onboardingQuietFraction);
            float usable = durationMinutes - startMinute;
            if (usable <= 0f) return;

            float slot = usable / count;

            for (int i = 0; i < count; i++)
            {
                var loop = _hauntProfile.PickLoop(nightIndex, rng);
                if (loop == HauntLoopId.None) continue;

                float atMinute = Mathf.Min(startMinute + i * slot + (float)rng.NextDouble() * slot, durationMinutes);

                plan.haunts.Add(new HauntBeat
                {
                    loop = loop,
                    room = _rooms.Count > 0 ? _rooms[rng.Next(_rooms.Count)] : null,
                    atMinute = atMinute,
                });
            }
        }

        // ── fallback ─────────────────────────────────────────────────────────────────────

        public NightPlan GenerateFallback(int nightIndex, float durationMinutes, int seed)
        {
            var plan = new NightPlan
            {
                seed = seed,
                nightIndex = nightIndex,
                durationMinutes = durationMinutes,
                glitchProfile = _glitchProfile,
            };

            var pool = _library != null ? _library.AvailableOn(nightIndex) : new List<AnomalyDefinition>();
            if (pool.Count == 0 || _difficulty == null)
            {
                Debug.LogError("NightPlanGenerator: cannot build even a fallback plan - the content library is empty.");
                return plan;
            }

            float startSeconds = Mathf.Max(0f, _difficulty.firstSpawnMinute) * 60f;
            float endSeconds = durationMinutes * 60f * Mathf.Clamp01(_difficulty.lastSpawnFraction);

            float longestThreat = 0f;
            foreach (var definition in pool) longestThreat = Mathf.Max(longestThreat, definition.threatTimeoutSeconds);

            float gap = Mathf.Max(_difficulty.MinimumSpacingFor(nightIndex), longestThreat) + _difficulty.handleCostSeconds;
            int count = Mathf.Max(1, Mathf.FloorToInt((endSeconds - startSeconds) / Mathf.Max(1f, gap)) + 1);

            for (int i = 0; i < count; i++)
            {
                plan.anomalies.Add(new AnomalyPlacement
                {
                    definition = pool[i % pool.Count],
                    room = _rooms.Count > 0 ? _rooms[i % _rooms.Count] : null,
                    atMinute = (startSeconds + i * gap) / 60f,
                });
            }

            plan.requiredScore = _difficulty.RequiredScoreFor(plan.anomalies.Count, _difficulty.WinRatioFor(nightIndex));
            plan.penaltyAnomaliesPerWrongReport = _difficulty.PenaltyAnomaliesFor(nightIndex);
            plan.SortByTime();

            Debug.LogWarning(
                $"NightPlanGenerator: fell back to a guaranteed-solvable plan for night {nightIndex} " +
                $"(seed {seed}): {plan.anomalies.Count} anomalies, need {plan.requiredScore}.");

            return plan;
        }
    }
}
