using System.Collections.Generic;
using System.Text;
using GameLogic.Data;
using GameLogic.Night;
using UnityEditor;
using UnityEngine;

namespace GiveMeASign.EditorTools
{
    /// <summary>
    /// Inspect and stress-test night generation without playing the game.
    ///
    /// The batch run is the important part: the spec's acceptance test is a thousand seeds across
    /// five nights with no unwinnable night among them. Doing that by hand at five minutes a night
    /// would take three weeks, which is precisely why the bug it guards against shipped.
    /// </summary>
    public class NightPlanDebugWindow : EditorWindow
    {
        private NightContentLibrary _library;
        private int _nightIndex = 1;
        private int _seed;
        private float _durationMinutes = 5f;

        private int _batchSeeds = 1000;
        private int _batchNights = 5;

        private string _output = "";
        private Vector2 _scroll;

        [MenuItem("Tools/Give Me A Sign/Night Plan Debugger")]
        public static void Open()
        {
            var window = GetWindow<NightPlanDebugWindow>("Night Plans");
            window.minSize = new Vector2(520f, 400f);
        }

        void OnEnable()
        {
            if (_library == null)
                _library = FindLibrary();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Content", EditorStyles.boldLabel);
            _library = (NightContentLibrary)EditorGUILayout.ObjectField("Library", _library, typeof(NightContentLibrary), false);

            if (_library == null)
            {
                EditorGUILayout.HelpBox(
                    "No NightContentLibrary. Run 'Tools/Give Me A Sign/Setup/3. Create Night Content Library'.",
                    MessageType.Error);
                return;
            }

            if (_library.difficulty == null)
            {
                EditorGUILayout.HelpBox("The library has no DifficultyProfile assigned - generation cannot run.", MessageType.Error);
                return;
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Single Plan", EditorStyles.boldLabel);
            _nightIndex = Mathf.Max(1, EditorGUILayout.IntField("Night Index", _nightIndex));
            _durationMinutes = Mathf.Max(0.1f, EditorGUILayout.FloatField("Duration (real min)", _durationMinutes));

            using (new EditorGUILayout.HorizontalScope())
            {
                _seed = EditorGUILayout.IntField("Seed", _seed);
                if (GUILayout.Button("Random", GUILayout.Width(70f)))
                    _seed = Random.Range(1, int.MaxValue);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate & Dump"))
                    DumpSinglePlan();

                if (GUILayout.Button("Check Determinism (x10)"))
                    CheckDeterminism();
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Batch Test", EditorStyles.boldLabel);
            _batchSeeds = Mathf.Clamp(EditorGUILayout.IntField("Seeds", _batchSeeds), 1, 20000);
            _batchNights = Mathf.Clamp(EditorGUILayout.IntField("Nights", _batchNights), 1, 20);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button($"Run {_batchSeeds} x {_batchNights}"))
                    RunBatch(writeCsv: false);

                if (GUILayout.Button("Run + Export CSV"))
                    RunBatch(writeCsv: true);
            }

            EditorGUILayout.Space(6);
            if (!string.IsNullOrEmpty(_output))
            {
                using (var scope = new EditorGUILayout.ScrollViewScope(_scroll))
                {
                    _scroll = scope.scrollPosition;
                    EditorGUILayout.TextArea(_output, GUILayout.ExpandHeight(true));
                }
            }
        }

        // ── Single plan ──────────────────────────────────────────────────────────────────

        private NightPlanGenerator BuildGenerator()
        {
            var rooms = _library.rooms != null ? _library.rooms : new List<RoomDefinition>();
            return new NightPlanGenerator(_library, rooms, _library.difficulty, _library.glitch, _library.haunt);
        }

        private void DumpSinglePlan()
        {
            var generator = BuildGenerator();
            int seed = _seed != 0 ? _seed : Random.Range(1, int.MaxValue);
            _seed = seed;

            var plan = generator.GenerateValid(_nightIndex, _durationMinutes, seed);

            var report = new StringBuilder(NightPlanRunner.Describe(plan, generator.LastOutcome));
            report.AppendLine();
            AppendPlanChecks(report, plan);

            _output = report.ToString();
            Debug.Log(_output);
        }

        private void AppendPlanChecks(StringBuilder report, NightPlan plan)
        {
            float handleCost = _library.difficulty.handleCostSeconds;
            int resolvable = NightPlanValidator.CountResolvable(plan, handleCost);
            float tightest = NightPlanValidator.TightestGapSeconds(plan);

            report.AppendLine("  --- checks ---");
            report.AppendLine($"    perfect player can handle : {resolvable} / {plan.anomalies.Count}");
            report.AppendLine($"    required to survive       : {plan.requiredScore}");
            report.AppendLine($"    winnable                  : {(resolvable >= plan.requiredScore ? "YES" : "NO")}");
            report.AppendLine($"    tightest gap              : {(float.IsInfinity(tightest) ? "n/a" : $"{tightest:0.0}s")}");

            bool valid = NightPlanValidator.Validate(plan, _library.difficulty, CountRooms(), out string reason);
            report.AppendLine($"    passes all rules          : {(valid ? "YES" : $"NO - {reason}")}");
        }

        /// <summary>
        /// Same seed must produce the same night, every time. If this ever fails, something in the
        /// generation path has reached for UnityEngine.Random.
        /// </summary>
        private void CheckDeterminism()
        {
            int seed = _seed != 0 ? _seed : 12345;
            _seed = seed;

            string reference = null;
            for (int run = 0; run < 10; run++)
            {
                var plan = BuildGenerator().GenerateValid(_nightIndex, _durationMinutes, seed);
                string signature = Signature(plan);

                if (reference == null)
                {
                    reference = signature;
                    continue;
                }

                if (signature == reference) continue;

                _output =
                    $"DETERMINISM FAILED on run {run + 1} for seed {seed}.\n" +
                    $"  expected: {reference}\n" +
                    $"  got     : {signature}\n" +
                    "Something in generation is using shared random state (UnityEngine.Random).";
                Debug.LogError(_output);
                return;
            }

            _output = $"Determinism OK: seed {seed} produced an identical night 10 times.\n  {reference}";
            Debug.Log(_output);
        }

        /// <summary>Compact fingerprint of a plan - enough to spot any difference.</summary>
        private static string Signature(NightPlan plan)
        {
            var sb = new StringBuilder($"n{plan.nightIndex}|req{plan.requiredScore}|");
            foreach (var placement in plan.anomalies)
            {
                sb.Append($"{placement.definition?.anomalyId}@{placement.atMinute:0.0000}/{placement.room?.roomId};");
            }
            foreach (var glitch in plan.glitches)
            {
                sb.Append($"{glitch.type}@{glitch.atMinute:0.0000};");
            }
            foreach (var haunt in plan.haunts)
            {
                sb.Append($"{haunt.loop}@{haunt.atMinute:0.0000}/{haunt.room?.roomId};");
            }
            return sb.ToString();
        }

        // ── Batch ────────────────────────────────────────────────────────────────────────

        private void RunBatch(bool writeCsv)
        {
            var generator = BuildGenerator();
            int roomCount = CountRooms();
            float handleCost = _library.difficulty.handleCostSeconds;

            var csv = new StringBuilder("night,seed,anomalies,threatCost,required,resolvable,winnable,tightestGapSeconds,glitches,roomsUsed\n");
            var failures = new List<string>();
            var roomUse = new Dictionary<string, int>();
            var kindUse = new Dictionary<string, int>();

            int total = 0, unwinnable = 0, ruleFailures = 0;
            long anomalySum = 0;
            float tightestOverall = float.PositiveInfinity;

            try
            {
                for (int night = 1; night <= _batchNights; night++)
                {
                    EditorUtility.DisplayProgressBar("Night Plan Batch", $"Night {night} of {_batchNights}", (night - 1f) / _batchNights);

                    for (int i = 0; i < _batchSeeds; i++)
                    {
                        // Spread out with two large primes so consecutive seeds aren't near-neighbours.
                        int seed = Mathf.Abs(1 + i * 7919 + night * 104729);

                        var plan = generator.GenerateValid(night, _durationMinutes, seed);
                        total++;
                        anomalySum += plan.anomalies.Count;

                        int resolvable = NightPlanValidator.CountResolvable(plan, handleCost);
                        bool winnable = resolvable >= plan.requiredScore;
                        if (!winnable)
                        {
                            unwinnable++;
                            if (failures.Count < 20)
                                failures.Add($"night {night} seed {seed}: {resolvable}/{plan.requiredScore} handleable");
                        }

                        if (!NightPlanValidator.Validate(plan, _library.difficulty, roomCount, out string reason))
                        {
                            ruleFailures++;
                            if (failures.Count < 20)
                                failures.Add($"night {night} seed {seed}: {reason}");
                        }

                        float tightest = NightPlanValidator.TightestGapSeconds(plan);
                        if (!float.IsInfinity(tightest))
                            tightestOverall = Mathf.Min(tightestOverall, tightest);

                        var rooms = new HashSet<string>();
                        foreach (var placement in plan.anomalies)
                        {
                            if (placement.room != null)
                            {
                                rooms.Add(placement.room.roomId);
                                Bump(roomUse, placement.room.roomId);
                            }
                            if (placement.definition != null)
                                Bump(kindUse, placement.definition.anomalyId);
                        }

                        if (writeCsv)
                        {
                            csv.AppendLine(
                                $"{night},{seed},{plan.anomalies.Count},{plan.TotalThreatCost},{plan.requiredScore}," +
                                $"{resolvable},{winnable},{(float.IsInfinity(tightest) ? "" : tightest.ToString("0.0"))}," +
                                $"{plan.glitches.Count},{rooms.Count}");
                        }
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            _output = Summarise(total, unwinnable, ruleFailures, anomalySum, tightestOverall, roomUse, kindUse, failures);

            if (unwinnable > 0 || ruleFailures > 0)
                Debug.LogError(_output);
            else
                Debug.Log(_output);

            if (writeCsv)
                SaveCsv(csv.ToString());
        }

        private string Summarise(int total, int unwinnable, int ruleFailures, long anomalySum,
                                 float tightestOverall, Dictionary<string, int> roomUse,
                                 Dictionary<string, int> kindUse, List<string> failures)
        {
            var report = new StringBuilder();
            report.AppendLine($"=== {total} plans generated ({_batchSeeds} seeds x {_batchNights} nights, {_durationMinutes:0.##} min) ===");
            report.AppendLine($"  unwinnable      : {unwinnable}   {(unwinnable == 0 ? "(PASS)" : "(FAIL)")}");
            report.AppendLine($"  rule failures   : {ruleFailures} {(ruleFailures == 0 ? "(PASS)" : "(these fell back)")}");
            report.AppendLine($"  avg anomalies   : {(total > 0 ? (double)anomalySum / total : 0d):0.00}");
            report.AppendLine($"  tightest gap    : {(float.IsInfinity(tightestOverall) ? "n/a" : $"{tightestOverall:0.0}s")}");

            // A lopsided distribution here means the picking is biased even when every plan is legal.
            report.AppendLine("  room use:");
            foreach (var pair in roomUse)
                report.AppendLine($"    {pair.Key,-16} {pair.Value}");

            report.AppendLine("  kind use:");
            foreach (var pair in kindUse)
                report.AppendLine($"    {pair.Key,-16} {pair.Value}");

            if (failures.Count > 0)
            {
                report.AppendLine($"  first {failures.Count} problems:");
                foreach (var failure in failures)
                    report.AppendLine($"    {failure}");
            }

            return report.ToString();
        }

        private static void Bump(Dictionary<string, int> counts, string key)
        {
            counts.TryGetValue(key, out int value);
            counts[key] = value + 1;
        }

        private void SaveCsv(string csv)
        {
            string path = EditorUtility.SaveFilePanel("Save night plan statistics", "", "night-plans.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;

            System.IO.File.WriteAllText(path, csv);
            Debug.Log($"NightPlanDebugWindow: wrote {path}");
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────────

        private int CountRooms() => _library.rooms != null ? _library.rooms.Count : 0;

        private static NightContentLibrary FindLibrary()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:NightContentLibrary"))
            {
                var library = AssetDatabase.LoadAssetAtPath<NightContentLibrary>(AssetDatabase.GUIDToAssetPath(guid));
                if (library != null) return library;
            }
            return null;
        }
    }
}
