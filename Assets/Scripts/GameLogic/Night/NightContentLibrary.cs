using System.Collections.Generic;
using GameLogic.Data;
using UnityEngine;

namespace GameLogic.Night
{
    /// <summary>
    /// Everything the night generator is allowed to draw from, plus the profiles that shape it.
    /// One asset, so the generator has no dependency on the scene and can be run from an editor
    /// window to batch-test thousands of seeds.
    ///
    /// Lives in a Resources folder (like the AudioManager prefab) so <see cref="Load"/> works
    /// without any scene wiring and survives into builds.
    /// </summary>
    [CreateAssetMenu(fileName = "NightContentLibrary", menuName = "Give Me A Sign/Night Content Library")]
    public class NightContentLibrary : ScriptableObject
    {
        /// <summary>Asset name inside Resources. Kept in one place so the editor tools agree with runtime.</summary>
        public const string ResourceName = "NightContentLibrary";

        [Header("Content")]
        [Tooltip("Every anomaly kind that may appear. The generator filters this by minNightIndex.")]
        public List<AnomalyDefinition> anomalies = new List<AnomalyDefinition>();

        [Tooltip("Every room anomalies may be placed in. Should match the RoomAnchors in the gameplay scene.")]
        public List<RoomDefinition> rooms = new List<RoomDefinition>();

        [Header("Profiles")]
        public DifficultyProfile difficulty;
        public GlitchProfile glitch;
        [Tooltip("Sprint 4. Optional - a library with none configured simply never schedules a haunt loop.")]
        public HauntProfile haunt;

        private static NightContentLibrary _cached;

        /// <summary>The project's library, or null when it hasn't been created yet.</summary>
        public static NightContentLibrary Load()
        {
            if (_cached != null) return _cached;

            _cached = Resources.Load<NightContentLibrary>(ResourceName);
            if (_cached == null)
            {
                Debug.LogError(
                    $"NightContentLibrary: no '{ResourceName}' asset in a Resources folder. " +
                    "Run 'Tools/Give Me A Sign/Setup/3. Create Night Content Library'.");
            }
            return _cached;
        }

        /// <summary>Anomaly kinds unlocked by the given night, skipping blank and prefab-less entries.</summary>
        public List<AnomalyDefinition> AvailableOn(int nightIndex)
        {
            var pool = new List<AnomalyDefinition>();

            foreach (var definition in anomalies)
            {
                if (definition == null || definition.prefab == null) continue;
                if (definition.minNightIndex > nightIndex) continue;

                pool.Add(definition);
            }

            return pool;
        }

        /// <summary>Rooms usable by a given anomaly kind (its allowedRooms filter, intersected with this library).</summary>
        public List<RoomDefinition> RoomsFor(AnomalyDefinition definition, IReadOnlyList<RoomDefinition> available)
        {
            var usable = new List<RoomDefinition>();

            foreach (var room in available)
            {
                if (room == null) continue;
                if (definition != null && !definition.AllowsRoom(room)) continue;

                usable.Add(room);
            }

            return usable;
        }

        /// <summary>Clears the cached lookup - used by the editor tools after (re)creating the asset.</summary>
        public static void ClearCache() => _cached = null;
    }
}
