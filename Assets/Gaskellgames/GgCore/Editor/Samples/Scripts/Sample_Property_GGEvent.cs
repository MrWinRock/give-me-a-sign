using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    public class Sample_Property_GGEvent : MonoBehaviour
    {
        // ---------- GGEvent ----------

        [Tooltip("Zero argument event.")]
        public GgEvent zeroArgsEvent;

        [Tooltip("Single argument event.")]
        public GgEvent<int> singleArgsEvent;

        [Tooltip("Double argument event.")]
        public GgEvent<int, int> doubleArgsEvent;

        [Tooltip("Tripple argument event.")]
        public GgEvent<int, int, int> tripleArgsEvent;

    } // class end
}
