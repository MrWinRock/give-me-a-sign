using System.Collections.Generic;
using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    public class Sample_Property_AudioClipImportData : MonoBehaviour
    {
        // ---------- AudioClipImportData ----------

        [SerializeField]
        private AudioClipImportData audioClipImportData;

        [SerializeField]
        private AudioClipImportData audioClipImportData2;

        [SerializeField, Space]
        private List<AudioClipImportData> audioClipImportDataList = new List<AudioClipImportData>();

        private void RemoveConsoleWarning()
        {
            if (audioClipImportData != null) { }
            if (audioClipImportData2 != null) { }
            if (audioClipImportDataList != null) { }
        }

    } // class end
}
