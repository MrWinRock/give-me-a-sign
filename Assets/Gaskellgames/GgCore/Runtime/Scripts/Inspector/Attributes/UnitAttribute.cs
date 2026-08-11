using System;
using UnityEngine;

namespace Gaskellgames
{
    /// <summary>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </summary>

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class UnitAttribute : PropertyAttribute
    {
        public Units unit;
        
        public UnitAttribute(Units unit)
        {
            this.unit = unit;
        }
        
    } // class end
}