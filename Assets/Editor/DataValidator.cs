using System.Collections.Generic;
using System.IO;
using System.Text;
using GameLogic;
using GameLogic.Data;
using UnityEditor;
using UnityEngine;

namespace GiveMeASign.EditorTools
{
    /// <summary>
    /// Checks that the game's data actually hangs together, so a broken asset is caught here
    /// rather than as a night that cannot be won.
    ///
    /// This is what makes it safe to delete the legacy fields: run it, get a clean bill of
    /// health, THEN remove Anomaly.Legacy.cs. Unity gives no warning when a serialized field
    /// disappears - the values just silently vanish.
    /// </summary>
    public static class DataValidator
    {
        private const string PrefabFolder = "Assets/Prefabs";

        [MenuItem("Tools/Give Me A Sign/Validate Data")]
        public static void Validate()
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            var rooms = LoadAll<RoomDefinition>();
            var definitions = LoadAll<AnomalyDefinition>();

            CheckRooms(rooms, errors, warnings);
            CheckDefinitions(definitions, rooms, errors, warnings);
            CheckKeywordsAreUnique(definitions, errors);
            CheckPrefabs(definitions, errors, warnings);

            Report(rooms.Count, definitions.Count, errors, warnings);
        }

        // ── Rooms ────────────────────────────────────────────────────────────────────────

        private static void CheckRooms(List<RoomDefinition> rooms, List<string> errors, List<string> warnings)
        {
            if (rooms.Count == 0)
            {
                errors.Add("No RoomDefinition assets exist. Run 'Setup/1. Create Rooms And Anchors'.");
                return;
            }

            var seenIds = new HashSet<string>();
            var seenOrders = new Dictionary<int, string>();

            foreach (var room in rooms)
            {
                if (string.IsNullOrWhiteSpace(room.roomId))
                    errors.Add($"Room '{room.name}' has an empty roomId.");
                else if (!seenIds.Add(room.roomId))
                    errors.Add($"Duplicate roomId '{room.roomId}' (on '{room.name}'). Room ids must be unique.");

                if (seenOrders.TryGetValue(room.cameraOrder, out string other))
                    errors.Add($"Rooms '{room.name}' and '{other}' share cameraOrder {room.cameraOrder} - the camera switcher needs a strict order.");
                else
                    seenOrders[room.cameraOrder] = room.name;
            }

            // An asset with no anchor in the scene is a room the camera can never reach.
            var anchors = Object.FindObjectsByType<RoomAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var anchoredIds = new HashSet<string>();
            foreach (var anchor in anchors)
            {
                if (anchor.Room == null)
                {
                    errors.Add($"RoomAnchor '{anchor.name}' in the open scene has no RoomDefinition assigned.");
                    continue;
                }

                if (!anchoredIds.Add(anchor.Room.roomId))
                    errors.Add($"More than one RoomAnchor claims room '{anchor.Room.roomId}' in the open scene.");
            }

            foreach (var room in rooms)
            {
                if (!string.IsNullOrWhiteSpace(room.roomId) && !anchoredIds.Contains(room.roomId))
                    warnings.Add($"Room '{room.Label}' has no RoomAnchor in the OPEN scene - it won't appear in the game (fine if that room belongs to another scene).");
            }
        }

        // ── Anomaly definitions ──────────────────────────────────────────────────────────

        private static void CheckDefinitions(List<AnomalyDefinition> definitions, List<RoomDefinition> rooms,
                                             List<string> errors, List<string> warnings)
        {
            if (definitions.Count == 0)
            {
                errors.Add("No AnomalyDefinition assets exist. Run 'Setup/2. Migrate Anomaly Prefabs'.");
                return;
            }

            var seenIds = new HashSet<string>();
            var knownRoomIds = new HashSet<string>();
            foreach (var room in rooms) knownRoomIds.Add(room.roomId);

            foreach (var definition in definitions)
            {
                string label = definition.name;

                if (string.IsNullOrWhiteSpace(definition.anomalyId))
                    errors.Add($"Anomaly '{label}' has an empty anomalyId.");
                else if (!seenIds.Add(definition.anomalyId))
                    errors.Add($"Duplicate anomalyId '{definition.anomalyId}' (on '{label}').");

                if (definition.prefab == null)
                    errors.Add($"Anomaly '{label}' has no prefab assigned - it can never spawn.");
                else if (definition.prefab.GetComponentInChildren<Anomaly>(true) == null)
                    errors.Add($"Anomaly '{label}' points at prefab '{definition.prefab.name}', which has no Anomaly component.");

                if (definition.correctKeywords == null || definition.correctKeywords.Length == 0)
                    errors.Add($"Anomaly '{label}' has no correctKeywords - it can never be reported correctly.");
                else
                {
                    foreach (var keyword in definition.correctKeywords)
                    {
                        if (string.IsNullOrWhiteSpace(keyword))
                            errors.Add($"Anomaly '{label}' has a blank entry in correctKeywords.");
                    }
                }

                if (definition.respondType == Anomaly.RespondType.MoveToTargetThenDisappear &&
                    definition.threatTimeoutSeconds <= 0f)
                {
                    warnings.Add($"Anomaly '{label}' moves in and waits for a prayer but has threatTimeoutSeconds = 0, so it never actually kills the player.");
                }

                if (definition.allowedRooms == null) continue;

                foreach (var allowed in definition.allowedRooms)
                {
                    if (allowed == null)
                        errors.Add($"Anomaly '{label}' has an empty slot in allowedRooms.");
                    else if (!knownRoomIds.Contains(allowed.roomId))
                        errors.Add($"Anomaly '{label}' allows room '{allowed.roomId}', which is not a known RoomDefinition.");
                }
            }
        }

        /// <summary>
        /// Two kinds sharing a keyword are indistinguishable to the player - saying the word
        /// reports both. This is the check that stops the original "all 7 anomalies are Shadow"
        /// situation from coming back.
        /// </summary>
        private static void CheckKeywordsAreUnique(List<AnomalyDefinition> definitions, List<string> errors)
        {
            var owners = new Dictionary<string, List<string>>();

            foreach (var definition in definitions)
            {
                if (definition.correctKeywords == null) continue;

                foreach (var keyword in definition.correctKeywords)
                {
                    if (string.IsNullOrWhiteSpace(keyword)) continue;

                    string key = keyword.Trim().ToLowerInvariant();
                    if (!owners.TryGetValue(key, out var list))
                        owners[key] = list = new List<string>();

                    if (!list.Contains(definition.name))
                        list.Add(definition.name);
                }
            }

            foreach (var pair in owners)
            {
                if (pair.Value.Count < 2) continue;

                errors.Add($"Keyword \"{pair.Key}\" is used by {pair.Value.Count} anomaly kinds ({string.Join(", ", pair.Value)}) - give each its own words.");
            }
        }

        // ── Prefabs ──────────────────────────────────────────────────────────────────────

        private static void CheckPrefabs(List<AnomalyDefinition> definitions, List<string> errors, List<string> warnings)
        {
            var definedPrefabs = new HashSet<GameObject>();
            foreach (var definition in definitions)
            {
                if (definition.prefab != null) definedPrefabs.Add(definition.prefab);
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                var anomaly = prefab.GetComponentInChildren<Anomaly>(true);
                if (anomaly == null) continue;

                string name = Path.GetFileName(path);

                if (anomaly.Definition == null)
                    errors.Add($"Prefab '{name}' has an Anomaly with no AnomalyDefinition assigned.");
                else if (!definedPrefabs.Contains(prefab))
                    warnings.Add($"Prefab '{name}' uses definition '{anomaly.Definition.name}', but that definition's prefab field points somewhere else.");

                if (!anomaly.IsMigrated)
                    errors.Add($"Prefab '{name}' has not been migrated - run 'Setup/2. Migrate Anomaly Prefabs'.");

                CheckSplitComponents(anomaly, name, errors);
            }
        }

        private static void CheckSplitComponents(Anomaly anomaly, string prefabName, List<string> errors)
        {
            var go = anomaly.gameObject;

            if (go.GetComponent<AnomalyMovement>() == null)
                errors.Add($"Prefab '{prefabName}' is missing its AnomalyMovement component.");
            if (go.GetComponent<AnomalyPresenter>() == null)
                errors.Add($"Prefab '{prefabName}' is missing its AnomalyPresenter component.");
            if (go.GetComponent<AnomalyThreatTimer>() == null)
                errors.Add($"Prefab '{prefabName}' is missing its AnomalyThreatTimer component.");

            // A type that walks in needs somewhere to walk to. MoveOnly deliberately tolerates a
            // missing target - that is how the demon survives a wrong report.
            var movement = go.GetComponent<AnomalyMovement>();
            if (movement != null &&
                anomaly.EffectiveRespondType == Anomaly.RespondType.MoveToTargetThenDisappear &&
                !movement.HasTarget)
            {
                errors.Add($"Prefab '{prefabName}' moves toward a target but has no moveTarget assigned.");
            }
        }

        // ── Reporting ────────────────────────────────────────────────────────────────────

        private static void Report(int roomCount, int definitionCount, List<string> errors, List<string> warnings)
        {
            var report = new StringBuilder();
            report.AppendLine($"=== Validate Data: {roomCount} room(s), {definitionCount} anomaly kind(s) ===");

            foreach (var warning in warnings) report.AppendLine($"  WARN   {warning}");
            foreach (var error in errors) report.AppendLine($"  ERROR  {error}");

            if (errors.Count == 0 && warnings.Count == 0)
                report.AppendLine("  All checks passed. Safe to delete Anomaly.Legacy.cs and its call in Anomaly.Awake.");
            else if (errors.Count == 0)
                report.AppendLine($"  No errors ({warnings.Count} warning(s)). Safe to delete Anomaly.Legacy.cs.");
            else
                report.AppendLine($"  {errors.Count} error(s) - do NOT delete the legacy fields yet.");

            if (errors.Count > 0)
                Debug.LogError(report.ToString());
            else if (warnings.Count > 0)
                Debug.LogWarning(report.ToString());
            else
                Debug.Log(report.ToString());
        }

        private static List<T> LoadAll<T>() where T : ScriptableObject
        {
            var results = new List<T>();
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) results.Add(asset);
            }
            return results;
        }
    }
}
