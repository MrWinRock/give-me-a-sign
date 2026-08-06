using System.Collections.Generic;
using System.IO;
using GameLogic.Data;
using GameLogic.Night;
using UnityEditor;
using UnityEngine;

namespace GiveMeASign.EditorTools
{
    /// <summary>
    /// Ongoing content setup helpers for the data-driven room/anomaly/night pipeline.
    ///
    /// The one-shot legacy migration this file used to also contain (pulling
    /// correctAnomalyType/respondType/etc. off the pre-split Anomaly fields into
    /// AnomalyDefinition assets) is gone - every prefab has been through it and
    /// Anomaly.Legacy.cs has been deleted, so there is nothing left to migrate FROM.
    ///
    /// What is left are the two steps you re-run whenever content changes:
    ///   1. Create Rooms And Anchors  - add a new room, or fix up anchors after a scene edit.
    ///   3. Create Night Content Library - re-sweep after adding/renaming an AnomalyDefinition
    ///      or RoomDefinition asset, so NightPlanGenerator sees it.
    ///
    /// Everything it touches is under version control, so `git checkout Assets/Prefabs
    /// Assets/Scenes Assets/Settings` undoes a bad run; scene changes are also registered with Undo.
    /// </summary>
    public static class DataSetupTools
    {
        private const string SettingsFolder = "Assets/Settings";
        private const string RoomFolder = SettingsFolder + "/Rooms";
        private const string ResourcesFolder = "Assets/Resources";

        /// <summary>
        /// The rooms as they were originally hardcoded in GameManager.CameraPositionsX. Note
        /// these are NOT the background sprite positions (0 / 17.96 / 36.19) - the camera parks
        /// slightly off them, so the values have to be carried over verbatim rather than derived
        /// from the scene.
        ///
        /// Only used to seed the FIRST three rooms; add a fourth/fifth room by creating a new
        /// RoomDefinition asset directly (Assets > Create > Give Me A Sign > Room Definition)
        /// and dropping a RoomAnchor for it in the scene, then re-run
        /// 'Setup/3. Create Night Content Library' to pick it up.
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
            library.haunt = FindOrCreate<HauntProfile>($"{SettingsFolder}/HauntProfile.asset");

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
