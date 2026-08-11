#if UNITY_EDITOR
#if !UNITY_6000_3_OR_NEWER

using System;
using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [System.Serializable]
    public class EntityId : IEquatable<EntityId>
    {
        #region Variables
        
        [SerializeField]
        private ulong id;
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Constructors
        
        public EntityId(int instanceID)
        {
            id = (ulong)instanceID;
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Private Methods
        
        private int ToInstanceID()
        {
            return (int)id;
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Public Methods
        
        public override string ToString()
        {
            return id.ToString();
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region IEquatable
        
        public bool Equals(EntityId other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return id == other.id;
        }
        
        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((EntityId)obj);
        }
        
        public override int GetHashCode()
        {
            return id.GetHashCode();
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Implicit Conversions
        
        /// <summary>
        /// Allows direct conversion of <see cref="Gaskellgames.EntityId"/> to <see cref="int"/> without showing an error.
        /// </summary>
        /// <param name="instanceID"></param>
        /// <returns></returns>
        public static implicit operator EntityId(int instanceID)
        {
            return new EntityId(instanceID);
        }
        
        /// <summary>
        /// Allows direct conversion of <see cref="int"/> to <see cref="Gaskellgames.EntityId"/> without showing an error.
        /// </summary>
        /// <param name="entityId"></param>
        /// <returns></returns>
        public static implicit operator int(EntityId entityId)
        {
            return entityId.ToInstanceID();
        }
        
        #endregion
        
    } // class end
}

#endif
#endif