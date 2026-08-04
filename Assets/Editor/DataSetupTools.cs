using System.Collections.Generic;
using System.IO;
using GameLogic;
using GameLogic.Data;
using GameLogic.Night;
using UnityEditor;
using UnityEngine;

namespace GiveMeASign.EditorTools
{
    /// <summary>
    /// One-shot migration for the Sprint 1 data refactor. Run the two menu items in order, then
    /// 'Validate Data'. Once the validator is clean this whole file can be deleted - it exists
    /// only to move data that used to live in code and prefabs into assets.
    ///
    /// Everything it touches is under version control, so `git checkout Assets/Prefabs
    /// Assets/Scenes` undoes a bad run; scene changes are also registered with Undo.
    /// </summary>
    public static class DataSetupTools
    {
        private const string SettingsFolder = "Assets/Settings";
        private const string RoomFolder = SettingsFolder + "/Rooms";
        private const string AnomalyFolder = SettingsFolder + "/Anomalies";
        private const string PrefabFolder = "Assets/Prefabs";
        private const string ResourcesFolder = "Assets/Resources";

        /// <summary>
        /// The rooms as they were hardcoded in GameManager.CameraPositionsX, which is the only
        /// place these numbers ever existed. Note they are NOT the background sprite positions
        /// (0 / 17.96 / 36.19) - the camera parks slightly off them, so the values have to be
        /// carried over verbatim rather than derived from the scene.
        ///
        /// displayName is a first guess: index 1 is "Bedroom" because that is the room the
        /// DemonAnomaly prefab (x = 17.96) already claimed. Rename any of them freely in the
        /// asset afterwards - nothing keys off displayName.
        /// </summary>
        private static readonly (string id, string name, float cameraX)[] Rooms =
        {
            ("hallway", "Hallway", 0f),
            ("bedroom", "Bedroom", 17.73f),
            ("kitchen", "Kitchen", 36.12f),
        };

        // ── Step 1: rooms ────────────────────────────────────────────────────────────────

        [MenuItem("Tools/Give Me A Sign/Setup/1. Create Rooms And Anchors")]
        public static void CreateRoomsAndAnchors()
        {
            EnsureFolder(RoomFolder);

            var definitions = new List<RoomDefinition>();
            for (int i = 0; i < Rooms.Length; i++)
            {
                var (id, displayName, cameraX) = Rooms[i];
                definitions.Add(CreateOrUpdateRoom(id, displayName, cameraX, cameraOrder: i));
            }

            AssetDatabase.SaveAssets();

            int placed = PlaceAnchors(definitions);

            Debug.Log(
                $"DataSetupTools: {definitions.Count} RoomDefinition asset(s) in {RoomFolder}, " +
                $"{placed} RoomAnchor(s) placed in the open scene. Save the scene to keep them.");
        }

        private static RoomDefinition CreateOrUpdateRoom(string id, string displayName, float cameraX, int cameraOrder)
        {
            string path = $"{RoomFolder}/Room_{displayName.Replace(" ", "")}.asset";

            var room = AssetDatabase.LoadAssetAtPath<RoomDefinition>(path);
            bool isNew = room == null;
            if (isNew)
            {
                room = ScriptableObject.CreateInstance<RoomDefinition>();
                AssetDatabase.CreateAsset(room, path);
            }

            // Only ever (re)write the wiring values - a displayName the user has since changed
            // is left alone so re-running this is safe.
            room.roomId = id;
            room.cameraX = cameraX;
            room.cameraOrder = cameraOrder;
            if (isNew) room.displayName = displayName;

            EditorUtility.SetDirty(room);
            return room;
        }

        /// <summary>Creates a "Rooms" parent in the open scene with one anchor per room definition.</summary>
        private static int PlaceAnchors(List<RoomDefinition> definitions)
        {
            var existingAnchors = Object.FindObjectsByType<RoomAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var byRoomId = new Dictionary<string, RoomAnchor>();
            foreach (var anchor in existingAnchors)
            {
                if (anchor.Room != null && !byRoomId.ContainsKey(anchor.Room.roomId))
                    byRoomId[anchor.Room.roomId] = anchor;
            }

            GameObject parent = GameObject.Find("Rooms");
            if (parent == null)
            {
                parent = new GameObject("Rooms");
                Undo.RegisterCreatedObjectUndo(parent, "Create Rooms parent");
            }

            int placed = 0;
            foreach (var room in definitions)
            {
                if (byRoomId.ContainsKey(room.roomId)) continue;

                var host = new GameObject($"Room_{room.Label}");
                Undo.RegisterCreatedObjectUndo(host, "Create RoomAnchor");
                host.transform.SetParent(parent.transform);
                host.transform.position = new Vector3(room.cameraX, 0f, 0f);

                var anchor = host.AddComponent<RoomAnchor>();

                // `room` is a private serialized field, so it has to be written through
                // SerializedObject rather than assigned directly.
                var so = new SerializedObject(anchor);
                so.FindProperty("room").objectReferenceValue = room;
                so.ApplyModifiedPropertiesWithoutUndo();

                // spawnPoints is deliberately left empty - RoomAnchor.GetSpawnPoint falls back to
                // its own transform, so this is a working default rather than invented data.
                placed++;
            }

            if (placed > 0)
                EditorSceneMarkDirty();

            return placed;
        }

        // ── Step 2: anomaly prefabs ──────────────────────────────────────────────────────

        [MenuItem("Tools/Give Me A Sign/Setup/2. Migrate Anomaly Prefabs")]
        public static void MigrateAnomalyPrefabs()
        {
            EnsureFolder(AnomalyFolder);

            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder });
            int migrated = 0, skipped = 0;
            var keywordOwners = new Dictionary<string, List<string>>();

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var anomaly = contents.GetComponentInChildren<Anomaly>(true);
                    if (anomaly == null) continue;

                    if (anomaly.IsMigrated)
                    {
                        skipped++;
                        continue;
                    }

                    string prefabName = Path.GetFileNameWithoutExtension(path);
                    var definition = MigrateOne(anomaly, path, prefabName);

                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                    migrated++;

                    foreach (var keyword in definition.correctKeywords)
                    {
                        string key = keyword.Trim().ToLowerInvariant();
                        if (!keywordOwners.TryGetValue(key, out var owners))
                            keywordOwners[key] = owners = new List<string>();
                        owners.Add(prefabName);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"DataSetupTools: migrated {migrated} prefab(s), skipped {skipped} already-migrated.");
            ReportSharedKeywords(keywordOwners);
        }

        /// <summary>
        /// The 7 anomaly prefabs all shipped with correctAnomalyType = "Shadow", so migration
        /// faithfully copies that and every definition ends up with the same keyword. That is the
        /// problem the validator is built to catch - shout about it here so the follow-up is obvious.
        /// </summary>
        private static void ReportSharedKeywords(Dictionary<string, List<string>> keywordOwners)
        {
            foreach (var pair in keywordOwners)
            {
                if (pair.Value.Count < 2) continue;

                Debug.LogWarning(
                    $"DataSetupTools: keyword \"{pair.Key}\" is claimed by {pair.Value.Count} anomaly kinds " +
                    $"({string.Join(", ", pair.Value)}). They are indistinguishable to the player until each " +
                    $"gets its own words - edit correctKeywords in {AnomalyFolder}.");
            }
        }

        private static AnomalyDefinition MigrateOne(Anomaly anomaly, string prefabPath, string prefabName)
        {
            var legacy = new SerializedObject(anomaly);

            string legacyType = legacy.FindProperty("correctAnomalyType").stringValue;
            int respondType = legacy.FindProperty("respondType").intValue;
            float timeToDisappear = legacy.FindProperty("timeToDisappear").floatValue;
            float moveSpeed = legacy.FindProperty("moveSpeed").floatValue;

            // The demon enforces its own deadline (DemonAnomaly.timeLimitSeconds) rather than going
            // through Anomaly's threat timer, so its legacy timeToDisappear is 0. Carrying that
            // straight over would tell the night planner the demon can be left indefinitely and
            // the solvability check would overestimate what a night allows.
            float threatTimeout = DemonTimeLimitOr(anomaly, timeToDisappear);

            var definition = CreateOrUpdateDefinition(
                prefabName, prefabPath, legacyType, respondType, moveSpeed, threatTimeout);

            CopyLegacyIntoSiblings(anomaly, legacy);

            legacy.FindProperty("definition").objectReferenceValue = definition;
            legacy.FindProperty("migrated").boolValue = true;
            legacy.ApplyModifiedPropertiesWithoutUndo();

            AssignDemonRoomIfNeeded(anomaly);

            return definition;
        }

        private static AnomalyDefinition CreateOrUpdateDefinition(
            string prefabName, string prefabPath, string legacyType,
            int respondType, float moveSpeed, float timeToDisappear)
        {
            string assetPath = $"{AnomalyFolder}/Anomaly_{prefabName}.asset";

            var definition = AssetDatabase.LoadAssetAtPath<AnomalyDefinition>(assetPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<AnomalyDefinition>();
                AssetDatabase.CreateAsset(definition, assetPath);
            }

            definition.anomalyId = prefabName.ToLowerInvariant();
            definition.displayName = string.IsNullOrWhiteSpace(legacyType) ? prefabName : legacyType;
            definition.correctKeywords = string.IsNullOrWhiteSpace(legacyType)
                ? new[] { prefabName }
                : new[] { legacyType };
            definition.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            definition.respondType = (Anomaly.RespondType)respondType;
            definition.moveSpeed = moveSpeed;
            definition.threatTimeoutSeconds = timeToDisappear;

            EditorUtility.SetDirty(definition);
            return definition;
        }

        /// <summary>Adds the three split components and copies the pre-split values into them.</summary>
        private static void CopyLegacyIntoSiblings(Anomaly anomaly, SerializedObject legacy)
        {
            var go = anomaly.gameObject;

            var movement = GetOrAdd<AnomalyMovement>(go);
            Copy(legacy, movement, ("moveTarget", "moveTarget"), ("moveSpeed", "moveSpeed"),
                 ("scaleUpAmount", "scaleUpAmount"), ("scaleAnimationSpeed", "scaleAnimationSpeed"));

            var presenter = GetOrAdd<AnomalyPresenter>(go);
            Copy(legacy, presenter, ("anomalyAnimator", "animator"),
                 ("moveTriggerName", "moveTriggerName"), ("idleTriggerName", "idleTriggerName"),
                 ("jumpScareAudioSource", "jumpScareAudioSource"), ("fightAudioSource", "fightAudioSource"));

            var timer = GetOrAdd<AnomalyThreatTimer>(go);
            Copy(legacy, timer, ("timeToDisappear", "timeoutSeconds"));
        }

        /// <summary>Copies serialized values field-by-field, matching whatever type each one is.</summary>
        private static void Copy(SerializedObject from, Component to, params (string src, string dst)[] fields)
        {
            var target = new SerializedObject(to);

            foreach (var (src, dst) in fields)
            {
                var source = from.FindProperty(src);
                var destination = target.FindProperty(dst);
                if (source == null || destination == null)
                {
                    Debug.LogWarning($"DataSetupTools: could not map '{src}' -> '{dst}' on {to.GetType().Name}.");
                    continue;
                }

                switch (source.propertyType)
                {
                    case SerializedPropertyType.Float:
                        destination.floatValue = source.floatValue; break;
                    case SerializedPropertyType.String:
                        destination.stringValue = source.stringValue; break;
                    case SerializedPropertyType.ObjectReference:
                        destination.objectReferenceValue = source.objectReferenceValue; break;
                    case SerializedPropertyType.Boolean:
                        destination.boolValue = source.boolValue; break;
                    default:
                        Debug.LogWarning($"DataSetupTools: unhandled property type {source.propertyType} for '{src}'.");
                        break;
                }
            }

            target.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>DemonAnomaly's own time limit, if this is a demon; otherwise the value passed in.</summary>
        private static float DemonTimeLimitOr(Anomaly anomaly, float fallback)
        {
            var demon = anomaly.GetComponent<DemonAnomaly>();
            if (demon == null) return fallback;

            var limit = new SerializedObject(demon).FindProperty("timeLimitSeconds");
            if (limit == null || limit.floatValue <= 0f) return fallback;

            Debug.Log($"DataSetupTools: using DemonAnomaly's own {limit.floatValue:0}s time limit as its threat window.");
            return limit.floatValue;
        }

        /// <summary>The demon's room used to be inferred from its X position; now it is a reference.</summary>
        private static void AssignDemonRoomIfNeeded(Anomaly anomaly)
        {
            var demon = anomaly.GetComponent<DemonAnomaly>();
            if (demon == null) return;

            var so = new SerializedObject(demon);
            var roomProperty = so.FindProperty("room");
            if (roomProperty == null || roomProperty.objectReferenceValue != null) return;

            var room = NearestRoomTo(anomaly.transform.position.x);
            if (room == null) return;

            roomProperty.objectReferenceValue = room;
            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log($"DataSetupTools: assigned room '{room.Label}' to DemonAnomaly (x = {anomaly.transform.position.x:0.##}).");
        }

        private static RoomDefinition NearestRoomTo(float x)
        {
            RoomDefinition best = null;
            float bestDistance = float.MaxValue;

            foreach (var guid in AssetDatabase.FindAssets("t:RoomDefinition"))
            {
                var room = AssetDatabase.LoadAssetAtPath<RoomDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (room == null) continue;

                float distance = Mathf.Abs(room.cameraX - x);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = room;
            }

            if (best == null)
                Debug.LogWarning("DataSetupTools: no RoomDefinition assets found - run '1. Create Rooms And Anchors' first.");

            return best;
        }

        // ── Step 3: night content library ────────────────────────────────────────────────

        [MenuItem("Tools/Give Me A Sign/Setup/3. Create Night Content Library")]
        public static void CreateNightContentLibrary()
        {
            EnsureFolder(ResourcesFolder);

            string libraryPath = $"{ResourcesFolder}/{NightContentLibrary.ResourceName}.asset";
            var library = AssetDatabase.LoadAssetAtPath<NightContentLibrary>(libraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<NightContentLibrary>();
                AssetDatabase.CreateAsset(library, libraryPath);
            }

            // Always re-sweep the content lists: adding a new anomaly or room should be picked up
            // by re-running this, not by remembering to also drag it in here.
            library.anomalies = LoadAllSorted<AnomalyDefinition>((a, b) => string.Compare(a.anomalyId, b.anomalyId, System.StringComparison.Ordinal));
            library.rooms = LoadAllSorted<RoomDefinition>((a, b) => a.cameraOrder.CompareTo(b.cameraOrder));

            library.difficulty = FindOrCreate<DifficultyProfile>($"{SettingsFolder}/DifficultyProfile.asset");
            library.glitch = FindOrCreate<GlitchProfile>($"{SettingsFolder}/GlitchProfile.asset");

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            NightContentLibrary.ClearCache();

            Debug.Log(
                $"DataSetupTools: night content library at {libraryPath} - " +
                $"{library.anomalies.Count} anomaly kind(s), {library.rooms.Count} room(s). " +
                "Add a NightPlanRunner to GameManager.unity if it isn't there yet, then check " +
                "'Tools/Give Me A Sign/Night Plan Debugger'.");

            Selection.activeObject = library;
            EditorGUIUtility.PingObject(library);
        }

        private static List<T> LoadAllSorted<T>(System.Comparison<T> comparison) where T : ScriptableObject
        {
            var results = new List<T>();
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) results.Add(asset);
            }
            results.Sort(comparison);
            return results;
        }

        /// <summary>Existing asset of this type anywhere in the project, or a fresh one at the given path.</summary>
        private static T FindOrCreate<T>(string path) where T : ScriptableObject
        {
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
            {
                var existing = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (existing != null) return existing;
            }

            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            Debug.Log($"DataSetupTools: created {typeof(T).Name} at {path} (default values - tune to taste).");
            return created;
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────────

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var existing = go.GetComponent<T>();
            return existing != null ? existing : go.AddComponent<T>();
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || folder == "Assets") return;
            if (AssetDatabase.IsValidFolder(folder)) return;

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private static void EditorSceneMarkDirty()
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        }
    }
}
