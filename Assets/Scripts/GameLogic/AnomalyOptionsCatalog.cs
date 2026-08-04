using System.Collections.Generic;
using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// LEGACY - being retired. Anomaly identity now lives in
    /// <see cref="GameLogic.Data.AnomalyDefinition"/> assets and rooms in
    /// <see cref="GameLogic.Data.RoomDefinition"/> assets, each owning its own data instead of
    /// this shared list of loose strings.
    ///
    /// Kept only so the [AnomalyOption] dropdown still works on the legacy string fields that
    /// remain on Anomaly until 'Tools/Give Me A Sign/Validate Data' passes. Delete this file (and
    /// AnomalyOptionAttribute + AnomalyOptionDrawer) once those fields are gone.
    ///
    /// The room list that used to live here has already been removed: rooms come from
    /// RoomRegistry now, so there is nothing left to keep in sync by hand.
    /// </summary>
    [CreateAssetMenu(fileName = "AnomalyOptions", menuName = "Give Me A Sign/Anomaly Options Catalog (legacy)")]
    public class AnomalyOptionsCatalog : ScriptableObject
    {
        [Tooltip("LEGACY. New work should add an AnomalyDefinition asset instead of a name here.")]
        public List<string> anomalyTypes = new List<string> { "Shadow" };
    }
}
