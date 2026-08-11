using UnityEngine;
using System;

namespace Gaskellgames
{
    /// <summary>
    /// Code updated by Gaskellgames: https://github.com/Gaskellgames
    /// Original code created by ghysc: https://github.com/ghysc/SwitchAttribute
    /// </summary>

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Struct, Inherited = true)]
    public class RequiredIfAttribute : PropertyAttribute
    {
        public byte R;
        public byte G;
        public byte B;
        public byte A;
        
        public LogicGate LogicGate;
		
        public (string field, object comparison)[] conditions;
		
        /// <summary>
        /// Enables the property in the inspector if the specified field's value is equal to the comparison object's value.
        /// </summary>
        /// <param name="field"></param>
        /// <param name="r"></param>
        /// <param name="g"></param>
        /// <param name="b"></param>
        /// <param name="a"></param>
        public RequiredIfAttribute(string field, byte r = 255, byte g = 000, byte b = 000, byte a = 255)
        {
            R = r;
            G = g;
            B = b;
            A = a;
            
            conditions = new (string, object)[] { (field, true) };
            LogicGate = LogicGate.AND;
        }
		
        /// <summary>
        /// Enables the property in the inspector if the specified field's value is equal to the comparison object's value.
        /// </summary>
        /// <param name="field"></param>
        /// <param name="comparison"></param>
        /// <param name="r"></param>
        /// <param name="g"></param>
        /// <param name="b"></param>
        /// <param name="a"></param>
        public RequiredIfAttribute(string field, object comparison, byte r = 255, byte g = 000, byte b = 000, byte a = 255)
        {
            R = r;
            G = g;
            B = b;
            A = a;
            
            conditions = new (string, object)[] { (field, comparison) };
            LogicGate = LogicGate.AND;
        }
		
        /// <summary>
        /// Enables the property in the inspector if all specified field values are equal to their comparison object's value.
        /// </summary>
        /// <param name="fields"></param>
        /// <param name="comparisons"></param>
        /// <param name="logicGate"></param>
        /// <param name="r"></param>
        /// <param name="g"></param>
        /// <param name="b"></param>
        /// <param name="a"></param>
        public RequiredIfAttribute(string[] fields, object[] comparisons, LogicGate logicGate = LogicGate.AND, byte r = 255, byte g = 000, byte b = 000, byte a = 255)
        {
            R = r;
            G = g;
            B = b;
            A = a;
            
            this.LogicGate = logicGate;
			
            if (fields == null)
            {
                throw new NullReferenceException("Fields[] cannot be null");
            }
            if (comparisons == null)
            {
                throw new NullReferenceException("Comparisons[] cannot be null");
            }
            if (fields.Length != comparisons.Length)
            {
                throw new ArgumentException("Field and comparison arrays must be same length!");
            }

            conditions = new (string, object)[fields.Length];

            for (int i = 0; i < fields.Length; i++)
            {
                conditions[i] = (fields[i], comparisons[i]);
            }
        }

    } // class end
}