using UnityEngine;

namespace GameLogic.Data
{
    /// <summary>
    /// Everything about one room that does NOT depend on the scene: its permanent id, the
    /// name the player sees, and where the camera sits to look at it.
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

        public string Label => string.IsNullOrWhiteSpace(displayName) ? roomId : displayName;
    }
}
