using System.Collections.Generic;
using GameLogic.Data;
using UnityEngine;

namespace GameLogic.Night
{
    /// <summary>
    /// Judges whether a generated night is actually playable. A rejected plan is simply re-rolled,
    /// which is far easier to reason about (and debug) than a generator clever enough to never
    /// produce a bad one.
    ///
    /// <see cref="IsSolvable"/> is the important one: it simulates a flawless player to prove the
    /// night can be won at all. That check is what stops the original "need 9 points from 8
    /// anomalies" class of bug from ever shipping again, no matter how the numbers are tuned.
    /// </summary>
    public static class NightPlanValidator
    {
        /// <summary>All checks. Returns false with a human-readable reason on the first failure.</summary>
        public static bool Validate(NightPlan plan, DifficultyProfile difficulty, int roomCount, out string reason)
        {
            reason = null;
            if (plan == null)
            {
                reason = "plan is null";
                return false;
            }

            if (plan.anomalies.Count == 0)
            {
                reason = "no anomalies placed";
                return false;
            }

            foreach (var placement in plan.anomalies)
            {
                if (placement.definition == null)
                {
                    reason = "a placement has no AnomalyDefinition";
                    return false;
                }
            }

            return CheckSpacing(plan, difficulty, out reason)
                && CheckNoOverlap(plan, out reason)
                && CheckRoomSpread(plan, roomCount, out reason)
                && CheckTypeSpread(plan, out reason)
                && CheckOnboarding(plan, difficulty, out reason)
                && CheckClimax(plan, difficulty, out reason)
                && CheckSolvable(plan, difficulty, out reason);
        }

        // ── Minimum Spacing ──────────────────────────────────────────────────────────────

        private static bool CheckSpacing(NightPlan plan, DifficultyProfile difficulty, out string reason)
        {
            reason = null;

            // Per-night, not the shared value: a late night deliberately runs a tighter pace, and
            // validating it against the shared minimum would reject every plan it produces.
            float minimum = difficulty != null ? difficulty.MinimumSpacingFor(plan.nightIndex) : 25f;

            for (int i = 1; i < plan.anomalies.Count; i++)
            {
                float gap = plan.anomalies[i].AtSeconds - plan.anomalies[i - 1].AtSeconds;
                if (gap >= minimum) continue;

                reason = $"anomalies {i - 1} and {i} are only {gap:0.0}s apart (minimum {minimum:0.0}s)";
                return false;
            }

            return true;
        }

        // ── No Overlap ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// Two anomalies with live threat windows at the same moment would need the player in two
        /// rooms at once. Kinds without a deadline can happily coexist.
        /// </summary>
        private static bool CheckNoOverlap(NightPlan plan, out string reason)
        {
            reason = null;

            for (int i = 1; i < plan.anomalies.Count; i++)
            {
                var previous = plan.anomalies[i - 1];
                var current = plan.anomalies[i];

                if (!previous.HasDeadline || !current.HasDeadline) continue;
                if (current.AtSeconds >= previous.DeadlineSeconds) continue;

                reason =
                    $"'{previous.definition.Label}' is still live until {previous.DeadlineSeconds:0.0}s " +
                    $"when '{current.definition.Label}' arrives at {current.AtSeconds:0.0}s";
                return false;
            }

            return true;
        }

        // ── Room Spread ──────────────────────────────────────────────────────────────────

        private static bool CheckRoomSpread(NightPlan plan, int roomCount, out string reason)
        {
            reason = null;

            for (int i = 1; i < plan.anomalies.Count; i++)
            {
                var previous = plan.anomalies[i - 1].room;
                var current = plan.anomalies[i].room;

                if (previous == null || current == null) continue;
                if (previous.roomId != current.roomId) continue;

                reason = $"room '{current.Label}' is used twice in a row (positions {i - 1} and {i})";
                return false;
            }

            // Only demand full coverage when there are enough anomalies to go round - a short
            // early night legitimately can't visit every room.
            if (roomCount <= 0 || plan.anomalies.Count < roomCount) return true;

            var used = new HashSet<string>();
            foreach (var placement in plan.anomalies)
            {
                if (placement.room != null) used.Add(placement.room.roomId);
            }

            if (used.Count >= roomCount) return true;

            reason = $"only {used.Count} of {roomCount} rooms are used";
            return false;
        }

        // ── Type Spread ──────────────────────────────────────────────────────────────────

        private static bool CheckTypeSpread(NightPlan plan, out string reason)
        {
            reason = null;

            // With a single kind available, back-to-back repeats are unavoidable and not a fault.
            var distinct = new HashSet<string>();
            foreach (var placement in plan.anomalies) distinct.Add(placement.definition.anomalyId);
            if (distinct.Count <= 1) return true;

            for (int i = 1; i < plan.anomalies.Count; i++)
            {
                string previous = plan.anomalies[i - 1].definition.anomalyId;
                string current = plan.anomalies[i].definition.anomalyId;

                if (previous != current) continue;

                reason = $"kind '{current}' appears twice in a row (positions {i - 1} and {i})";
                return false;
            }

            return true;
        }

        // ── Onboarding ───────────────────────────────────────────────────────────────────

        private static bool CheckOnboarding(NightPlan plan, DifficultyProfile difficulty, out string reason)
        {
            reason = null;
            if (difficulty == null) return true;

            float quietUntil = plan.durationMinutes * Mathf.Clamp01(difficulty.onboardingQuietFraction);
            if (quietUntil <= 0f) return true;

            foreach (var glitch in plan.glitches)
            {
                if (glitch.atMinute >= quietUntil) continue;

                reason = $"glitch {glitch.type} at {glitch.atMinute:0.00}m breaks the quiet opening (until {quietUntil:0.00}m)";
                return false;
            }

            foreach (var haunt in plan.haunts)
            {
                if (haunt.atMinute >= quietUntil) continue;

                reason = $"haunt {haunt.loop} at {haunt.atMinute:0.00}m breaks the quiet opening";
                return false;
            }

            return true;
        }

        // ── Climax ───────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The nastiest thing of the night should land near the end. Skipped when every kind costs
        /// the same, because then there is no "nastiest" to place.
        /// </summary>
        private static bool CheckClimax(NightPlan plan, DifficultyProfile difficulty, out string reason)
        {
            reason = null;
            if (difficulty == null || plan.anomalies.Count < 2) return true;

            int highest = int.MinValue, lowest = int.MaxValue;
            foreach (var placement in plan.anomalies)
            {
                highest = Mathf.Max(highest, placement.ThreatCost);
                lowest = Mathf.Min(lowest, placement.ThreatCost);
            }

            if (highest == lowest) return true;

            float climaxStarts = plan.durationMinutes * (1f - Mathf.Clamp01(difficulty.climaxFraction));

            foreach (var placement in plan.anomalies)
            {
                if (placement.ThreatCost != highest) continue;
                if (placement.atMinute >= climaxStarts) return true;
            }

            reason = $"no tier-{highest} anomaly lands after {climaxStarts:0.00}m (the closing stretch)";
            return false;
        }

        // ── Solvability ──────────────────────────────────────────────────────────────────

        private static bool CheckSolvable(NightPlan plan, DifficultyProfile difficulty, out string reason)
        {
            reason = null;

            float handleCost = difficulty != null ? difficulty.handleCostSeconds : 10f;
            int resolvable = CountResolvable(plan, handleCost);

            if (resolvable >= plan.requiredScore) return true;

            reason =
                $"unwinnable: a perfect player can only handle {resolvable} of {plan.anomalies.Count} " +
                $"anomalies but {plan.requiredScore} are required";
            return false;
        }

        /// <summary>
        /// Simulates a player who never makes a mistake, spending handleCost seconds per anomaly
        /// (open form, pick room, speak, wait for speech-to-text, submit, switch camera) and
        /// carrying that cost forward - so anomalies bunched together genuinely can't all be caught.
        /// </summary>
        public static int CountResolvable(NightPlan plan, float handleCostSeconds)
        {
            if (plan == null || plan.anomalies.Count == 0) return 0;

            // The plan is kept sorted, but never trust that here - this is the safety net.
            var ordered = new List<AnomalyPlacement>(plan.anomalies);
            ordered.Sort((a, b) => a.atMinute.CompareTo(b.atMinute));

            float playerFreeAt = 0f;
            int resolvable = 0;

            foreach (var placement in ordered)
            {
                float appearsAt = placement.AtSeconds;
                float startsAt = Mathf.Max(appearsAt, playerFreeAt);

                // No deadline means it waits patiently; the player can always get to it eventually.
                float deadline = placement.HasDeadline ? placement.DeadlineSeconds : plan.DurationSeconds;

                if (startsAt + handleCostSeconds > deadline) continue;

                resolvable++;
                playerFreeAt = startsAt + handleCostSeconds;
            }

            return resolvable;
        }

        /// <summary>Convenience for the debug tools: can this plan be won at all?</summary>
        public static bool IsSolvable(NightPlan plan, float handleCostSeconds) =>
            plan != null && CountResolvable(plan, handleCostSeconds) >= plan.requiredScore;

        /// <summary>Narrowest gap between consecutive anomalies, in seconds. Used for batch statistics.</summary>
        public static float TightestGapSeconds(NightPlan plan)
        {
            if (plan == null || plan.anomalies.Count < 2) return float.PositiveInfinity;

            float tightest = float.PositiveInfinity;
            for (int i = 1; i < plan.anomalies.Count; i++)
                tightest = Mathf.Min(tightest, plan.anomalies[i].AtSeconds - plan.anomalies[i - 1].AtSeconds);

            return tightest;
        }
    }
}
