using UnityEngine;

// Aliased, not imported: this file uses UnityEngine's [Min], and a plain `using Gaskellgames;`
// would make the simple name `Min` ambiguous (CS0104) because Gaskellgames ships its own.
// See CLAUDE.md - "Gaskellgames" for the project-wide rule.
using GG = Gaskellgames;

namespace GameLogic.Data
{
    /// <summary>
    /// Everything permanent about one KIND of anomaly: what it is called, what the player has
    /// to say to report it, how dangerous it is, and which rooms it may appear in.
    /// </summary>
    [CreateAssetMenu(fileName = "Anomaly_", menuName = "Give Me A Sign/Anomaly Definition")]
    public class AnomalyDefinition : ScriptableObject
    {
        [Header("Identity")]
        [GG.InfoBox("anomalyId is referenced by saves and night seeds. Renaming it after the anomaly ships breaks both.", GG.InfoMessageType.Warning)]
        [Tooltip("Permanent key. Referenced by saves and night seeds - don't change it after the anomaly ships.")]
        public string anomalyId = "shadow";

        [Tooltip("Name shown in the field manual and debug output.")]
        public string displayName = "Shadow Figure";

        [GG.Required]
        [Tooltip("คำที่นับว่าถูกทั้งหมด รวมคำที่ผู้เล่นน่าจะพูดพลาด. ANY of these spoken into the mic counts as a correct report - list the likely mishearings too.")]
        public string[] correctKeywords = { "Shadow", "Shadow Figure" };

        [Header("Spawning")]
        [GG.Required]
        [Tooltip("Must contain an Anomaly component. Without this the generator skips this kind entirely.")]
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

        [GG.InfoBox("0 = this kind never runs the player out of time. Normal anomalies use 0; only the Demon reserves a window here.")]
        [Tooltip("เวลาที่ผู้เล่นมีก่อนแพ้ หลังมันเข้าโหมดคุกคาม. 0 = never times out (it just lurks).")]
        [Min(0f)] public float threatTimeoutSeconds;

        [Header("Field Manual")]
        public Sprite manualImage;
        [TextArea(3, 8)] public string manualDescription;
        [TextArea(2, 4)] public string howToSpot;

        [Header("Links")]
        [Tooltip("Sprint 4 - not read by anything yet.")]
        public HauntLoopId linkedHaunt = HauntLoopId.None;

        public string Label => string.IsNullOrWhiteSpace(displayName) ? anomalyId : displayName;

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
