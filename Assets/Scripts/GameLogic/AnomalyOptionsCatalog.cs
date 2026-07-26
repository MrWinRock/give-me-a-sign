using System.Collections.Generic;
using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// Single shared list of valid anomaly type names and room/location names. Add new
    /// anomaly types or rooms here and every Anomaly prefab's dropdown (and, for rooms,
    /// IncidentReportManager's own list) can pick them up - no need to type matching text
    /// into each prefab by hand.
    ///
    /// This is a plain data asset (ScriptableObject), loaded once and cached by the editor
    /// drawer. It has no runtime cost - nothing at runtime reads from it; Anomaly's fields
    /// are still just plain strings that get compared exactly as before.
    /// </summary>
    [CreateAssetMenu(fileName = "AnomalyOptions", menuName = "Give Me A Sign/Anomaly Options Catalog")]
    public class AnomalyOptionsCatalog : ScriptableObject
    {
        [Tooltip("Anomaly type names selectable in each Anomaly prefab's 'Correct Anomaly Type' dropdown. This is also what the player must say into the mic.")]
        public List<string> anomalyTypes = new List<string> { "Shadow" };

        [Tooltip("Room names selectable in each Anomaly prefab's 'Correct Location Name' dropdown. Keep in sync with IncidentReportManager's Room Names list.")]
        public List<string> locations = new List<string>
        {
            "Hallway", "Kitchen", "Bedroom", "Living room", "Basement", "Attic"
        };
    }
}
