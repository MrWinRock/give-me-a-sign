using System.Collections.Generic;
using GameLogic.Data;
using GameLogic.Flow;
using GameLogic.SpawnAndTime;
using UnityEngine;

namespace GameLogic.Night
{
    /// <summary>
    /// Generates the night's plan and publishes it. One of these in the gameplay scene is all the
    /// procedural system needs.
    ///
    /// It runs in Start, by which point every RoomAnchor has registered itself, so the plan can be
    /// built against the rooms the camera can genuinely reach. The schedulers deliberately do NOT
    /// read the plan in their own Start - they build their timelines on the night timer's first
    /// tick, which is guaranteed to be after every Start in the scene. That removes the script
    /// execution order question entirely instead of relying on getting it right.
    /// </summary>
    public class NightPlanRunner : MonoBehaviour
    {
        [Header("Content")]
        [Tooltip("Leave empty to load 'NightContentLibrary' from a Resources folder.")]
        [SerializeField] private NightContentLibrary library;

        [Header("Night")]
        [Tooltip("Which night to generate. 0 = use the unlocked-night progression in PlayerPrefs.")]
        [Min(0)] [SerializeField] private int nightIndexOverride;

        [Tooltip("Fixed seed for reproducing a specific night. 0 = roll a fresh one.")]
        [SerializeField] private int seedOverride;

        [Header("Rooms")]
        [Tooltip("Build the plan from the RoomAnchors present in this scene. Off = use the library's room list (needed if rooms live in another scene).")]
        [SerializeField] private bool useSceneRooms = true;

        [Header("Debug")]
        [SerializeField] private bool logPlanOnStart = true;

        /// <summary>PlayerPrefs key for how far the player has got. Progression is the only thing PlayerPrefs still tracks.</summary>
        public const string UnlockedNightKey = "UnlockedNight";

        public NightPlan Plan { get; private set; }

        void Start()
        {
            var resolved = library != null ? library : NightContentLibrary.Load();
            if (resolved == null)
            {
                Debug.LogError("NightPlanRunner: no NightContentLibrary - the night cannot be generated.", this);
                return;
            }

            if (resolved.difficulty == null)
            {
                Debug.LogError($"NightPlanRunner: library '{resolved.name}' has no DifficultyProfile assigned.", this);
                return;
            }

            int nightIndex = ResolveNightIndex();
            int seed = seedOverride != 0 ? seedOverride : NightPlanProvider.NextSeed();
            float duration = ResolveDurationMinutes();

            var generator = new NightPlanGenerator(resolved, ResolveRooms(resolved), resolved.difficulty, resolved.glitch, resolved.haunt);
            Plan = generator.GenerateValid(nightIndex, duration, seed);

            NightPlanProvider.Publish(Plan);

            // Recorded on the night's result, so a bug report can name the exact night to replay.
            GameFlowManager.CurrentNightIndex = nightIndex;
            GameFlowManager.CurrentSeed = seed;

            ApplyGlitchProfile(resolved, nightIndex);

            if (logPlanOnStart)
                Debug.Log(Describe(Plan, generator.LastOutcome), this);
        }

        private int ResolveNightIndex()
        {
            if (nightIndexOverride > 0) return nightIndexOverride;

            return Mathf.Max(1, PlayerPrefs.GetInt(UnlockedNightKey, 1));
        }

        private float ResolveDurationMinutes()
        {
            var timer = FindFirstObjectByType<NightTimer>();
            if (timer != null) return timer.NightDurationMinutes;

            Debug.LogWarning("NightPlanRunner: no NightTimer in the scene - assuming a 5 minute night.", this);
            return 5f;
        }

        /// <summary>
        /// Scene anchors by default: a plan that places an anomaly in a room the camera can't reach
        /// is a wasted anomaly. Falls back to the library when the scene has no anchors.
        /// </summary>
        private List<RoomDefinition> ResolveRooms(NightContentLibrary resolved)
        {
            var rooms = new List<RoomDefinition>();

            if (useSceneRooms)
            {
                foreach (var anchor in RoomRegistry.All)
                {
                    if (anchor.Room != null) rooms.Add(anchor.Room);
                }
            }

            if (rooms.Count > 0) return rooms;

            if (useSceneRooms)
            {
                Debug.LogWarning(
                    "NightPlanRunner: no RoomAnchors registered - falling back to the library's room list. " +
                    "Run 'Tools/Give Me A Sign/Setup/1. Create Rooms And Anchors' and save the scene.", this);
            }

            rooms.AddRange(resolved.rooms);
            return rooms;
        }

        private void ApplyGlitchProfile(NightContentLibrary resolved, int nightIndex)
        {
            var director = FindFirstObjectByType<Report.GlitchDirector>();
            if (director == null) return;

            if (resolved.glitch != null)
                director.SetIntensity(resolved.glitch.intensity);

            // Sprint 6, S-604: night 1 is the tutorial. GlitchDirector needs a matching
            // "AlwaysWhenFlagSet" blackout entry (flagName "tutorial") in its own Inspector list to
            // actually go quiet on this flag - add one by hand if the scene's GlitchDirector
            // predates this. HauntDirector reads this same flag directly (see HauntDirector.Fire)
            // to suppress every haunt beat on night 1 with no extra authoring needed there.
            director.SetFlag("tutorial", nightIndex == 1);
        }

        // ── Debug ────────────────────────────────────────────────────────────────────────

        [ContextMenu("Dump Night Plan")]
        public void DumpNightPlan()
        {
            if (Plan == null)
            {
                Debug.Log("NightPlanRunner: no plan yet - press Play first.", this);
                return;
            }

            Debug.Log(Describe(Plan, "(dumped on request)"), this);
        }

        /// <summary>Whole night as a readable table - the fast way to see what a seed produced.</summary>
        public static string Describe(NightPlan plan, string outcome = null)
        {
            if (plan == null) return "NightPlan: (null)";

            var report = new System.Text.StringBuilder();
            report.AppendLine($"=== Night {plan.nightIndex} | seed {plan.seed} | {plan.durationMinutes:0.##} min ===");
            report.AppendLine($"  anomalies : {plan.anomalies.Count} (threat cost {plan.TotalThreatCost})");
            report.AppendLine($"  required  : {plan.requiredScore} to survive");
            if (!string.IsNullOrEmpty(outcome))
                report.AppendLine($"  generator : {outcome}");

            report.AppendLine("  --- anomalies ---");
            foreach (var placement in plan.anomalies)
            {
                report.AppendLine(
                    $"    {placement.atMinute,6:0.00}m  {ClockLabel(placement.atMinute, plan.durationMinutes),-9}  " +
                    $"{placement.definition?.Label ?? "(none)",-20} in {placement.room?.Label ?? "(no room)",-12} " +
                    $"cost {placement.ThreatCost}  timeout {(placement.HasDeadline ? $"{placement.definition.threatTimeoutSeconds:0}s" : "none")}");
            }

            if (plan.glitches.Count > 0)
            {
                report.AppendLine("  --- glitches ---");
                foreach (var glitch in plan.glitches)
                {
                    report.AppendLine(
                        $"    {glitch.atMinute,6:0.00}m  {ClockLabel(glitch.atMinute, plan.durationMinutes),-9}  " +
                        $"{glitch.type}{(glitch.fireDelay > 0f ? $"  (+{glitch.fireDelay:0.0}s)" : "")}");
                }
            }

            if (plan.haunts.Count > 0)
            {
                report.AppendLine("  --- haunts ---");
                foreach (var haunt in plan.haunts)
                {
                    report.AppendLine(
                        $"    {haunt.atMinute,6:0.00}m  {ClockLabel(haunt.atMinute, plan.durationMinutes),-9}  " +
                        $"{haunt.loop} in {haunt.room?.Label ?? "(no room)"}");
                }
            }

            return report.ToString();
        }

        private static string ClockLabel(float minute, float durationMinutes)
        {
            if (durationMinutes <= 0f) durationMinutes = 5f;
            float hours = Mathf.Clamp01(minute / durationMinutes) * NightTimer.GameHoursPerNight;
            return NightTimer.FormatGameTime(hours, includeSeconds: false);
        }

        /// <summary>
        /// Marks each planned anomaly on its room's anchor in the Scene view, labelled with when it
        /// arrives. Only possible during Play, since the rooms are chosen at runtime.
        /// </summary>
        void OnDrawGizmosSelected()
        {
            if (Plan == null) return;

            Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.9f);

            // Stack the labels when several anomalies share a room, so they don't overprint.
            var seenPerRoom = new Dictionary<string, int>();

            foreach (var placement in Plan.anomalies)
            {
                var anchor = RoomRegistry.Get(placement.room);
                if (anchor == null) continue;

                string roomId = placement.room.roomId;
                seenPerRoom.TryGetValue(roomId, out int stack);
                seenPerRoom[roomId] = stack + 1;

                Vector3 at = anchor.transform.position + Vector3.up * (1.4f + stack * 0.45f);
                Gizmos.DrawWireSphere(at, 0.2f);

#if UNITY_EDITOR
                UnityEditor.Handles.Label(
                    at + Vector3.right * 0.3f,
                    $"{placement.atMinute:0.00}m  {placement.definition?.Label}");
#endif
            }
        }
    }
}
