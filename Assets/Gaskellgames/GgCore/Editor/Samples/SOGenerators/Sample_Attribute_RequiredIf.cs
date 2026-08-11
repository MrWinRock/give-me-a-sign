#if UNITY_EDITOR
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    [CreateAssetMenu(fileName = "Sample_Attribute_RequiredIf", menuName = "Gaskellgames/Samples/GgSamplePage")]
    public class Sample_Attribute_RequiredIf : ScriptableObject
    {
        // ---------- RequiredIf ----------

        [SerializeField]
        private bool value1;

        [SerializeField]
        private bool value2;

        [SerializeField, RequiredIf(nameof(value1))]
        private Object requiredIfValue1True;

        [field: SerializeField, RequiredIf(nameof(value2))]
        private string RequiredIfValue2True { get; set; }

        [SerializeField, RequiredIf(new string[] { nameof(value1), nameof(value2) }, new object[] { true, true }, LogicGate.AND)]
        private Object requiredIfBothTrue;

        [SerializeField, RequiredIf(new string[] { nameof(value1), nameof(value2) }, new object[] { true, true }, LogicGate.OR)]
        private string requiredIfEitherTrue;
        
    } // class end
}

#endif