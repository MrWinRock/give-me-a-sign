using UnityEngine;

namespace GameLogic.Data
{
    /// <summary>
    /// Everything about one room that does NOT depend on the scene: its permanent id, the
    /// name the player sees, and where the camera sits to look at it.
    ///
    /// This is the single source of truth for "what rooms exist". Adding a room = creating
    /// one of these assets and dropping a <see cref="RoomAnchor"/> in the scene - no code
    /// change anywhere. Previously the same information was spread across
    /// GameManager.CameraPositionsX, IncidentReportManager.roomNames and
    /// AnomalyOptionsCatalog.locations, which had to be kept in sync by hand.
    ///
    /// Scene positions (spawn points) deliberately live on RoomAnchor instead: a
    /// ScriptableObject cannot hold a reference to a Transform in a scene.
    /// </summary>
    [CreateAssetMenu(fileName = "Room_", menuName = "Give Me A Sign/Room Definition")]
    public class RoomDefinition : ScriptableObject
    {
        [Tooltip("คีย์ถาวร ห้ามเปลี่ยนหลังใช้งานแล้ว (ใช้ใน save/seed). Permanent key - safe to reference from saves and night seeds.")]
        public string roomId = "hallway";

        [Tooltip("ชื่อที่แสดงใน dropdown และคู่มือ. Free to rename at any time; nothing keys off it.")]
        public string displayName = "Hallway";

        [Tooltip("ตำแหน่ง X ของกล้องสำหรับห้องนี้. The camera parks here when this room is selected.")]
        public float cameraX;

        [Tooltip("ลำดับในตัวสลับกล้อง. Rooms are cycled in this order by the Next/Previous buttons.")]
        public int cameraOrder;

        [TextArea] public string manualNote;

        /// <summary>displayName, falling back to roomId so a half-filled asset never shows blank in a dropdown.</summary>
        public string Label => string.IsNullOrWhiteSpace(displayName) ? roomId : displayName;
    }
}
