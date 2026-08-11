using System;
using UnityEngine;

namespace Gaskellgames
{
    /// <summary>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </summary>

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
    public class InLineButtonAttribute : PropertyAttribute
    {
        public string methodName;

        public InLineButtonAttribute(string methodName)
        {
            this.methodName = methodName;
        }
        
    } // class end
}

