using UnityEngine;

namespace GameLogic.Data
{
    /// <summary>
    /// Everything permanent about one KIND of anomaly: what it is called, what the player has
    /// to say to report it, how dangerous it is, and which rooms it may appear in.
    ///
    /// The rule that makes procedural nights possible:
    ///   kind = static (lives here, in the asset)
    ///   room = runtime (chosen when it spawns, via Anomaly.AssignRoom)
    ///
    /// Prefabs used to carry both, which meant the room was baked in at author time and could
    /// not be randomised. Adding a new anomaly kind is now one asset plus one prefab.
    /// </summary>
    [CreateAssetMenu(fileName = "Anomaly_", menuName = "Give Me A Sign/Anomaly Definition")]
    public class AnomalyDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Permanent key. Referenced by saves and night seeds - don't change it after the anomaly ships.")]
        public string anomalyId = "shadow";

        [Tooltip("Name shown in the field manual and debug output.")]
        public string displayName = "Shadow Figure";

        [Tooltip("คำที่นับว่าถูกทั้งหมด รวมคำที่ผู้เล่นน่าจะพูดพลาด. ANY of these spoken into the mic counts as a correct report - list the likely mishearings too.")]
        public string[] correctKeywords = { "Shadow", "Shadow Figure" };

        [Header("Spawning")]
        public GameObject prefab;
        public Anomaly.RespondType respondType = Anomaly.RespondType.MoveToTargetThenDisappear;

        [Tooltip("ราคาในงบภัยคุกคามของคืน ยิ่งสูง = ยิ่งอันตราย. Spent from the night's threat budget by the generator.")]
        [Min(1)] public int threatCost = 1;

        [Tooltip("ห้ามโผล่ก่อนคืนที่เท่าไหร่ (1 = โผล่ได้ตั้งแต่คืนแรก).")]
        [Min(1)] public int minNightIndex = 1;

        [Tooltip("เว้นว่าง = เกิดได้ทุกห้อง. Restricts which rooms the generator may place this in.")]
        public RoomDefinition[] allowedRooms;

        [Header("Timing")]
        public float moveSpeed = 3f;

        [Tooltip("เวลาที่ผู้เล่นมีก่อนแพ้ หลังมันเข้าโหมดคุกคาม. 0 = never times out (it just lurks).")]
        public float threatTimeoutSeconds = 30f;

        [Header("Field Manual")]
        public Sprite manualImage;
        [TextArea(3, 8)] public string manualDescription;
        [TextArea(2, 4)] public string howToSpot;

        [Header("Links")]
        [Tooltip("Sprint 4 - not read by anything yet.")]
        public HauntLoopId linkedHaunt = HauntLoopId.None;

        /// <summary>displayName, falling back to anomalyId so debug output is never blank.</summary>
        public string Label => string.IsNullOrWhiteSpace(displayName) ? anomalyId : displayName;

        /// <summary>True when this kind may be placed in the given room (empty allowedRooms = anywhere).</summary>
        public bool AllowsRoom(RoomDefinition room)
        {
            if (allowedRooms == null || allowedRooms.Length == 0) return true;
            if (room == null) return false;

            foreach (var allowed in allowedRooms)
            {
                if (allowed != null && allowed.roomId == room.roomId) return true;
            }
            return false;
        }
    }
}
