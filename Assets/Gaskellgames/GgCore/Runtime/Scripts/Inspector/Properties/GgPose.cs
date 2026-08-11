using System;
using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [System.Serializable]
    public class GgPose : IEquatable<GgPose>
    {
        #region Variables
        
        public Vector3 position;
        public Vector3 eulerAngles;
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Constructors
        
        /// <summary>
        /// Empty constructor.
        /// </summary>
        public GgPose()
        {
            this.position = Vector3.zero;
            this.eulerAngles = Vector3.zero;
        }
        
        /// <summary>
        /// Explicit constructor.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="eulerAngles"></param>
        public GgPose(Vector3 position, Vector3 eulerAngles)
        {
            this.position = position;
            this.eulerAngles = eulerAngles;
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Static Getter
        
        /// <summary>
        /// Shorthand for writing new Pose(Vector3.zero, Vector3.zero).
        /// </summary>
        public static GgPose Zero => new GgPose(Vector3.zero, Vector3.zero);
        
        /// <summary>
        /// Returns a formatted string for this pose.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return string.Format("({0}, {1})", (object) this.position.ToString(), (object) this.eulerAngles.ToString());
        }
        
        /// <summary>
        /// Returns a formatted string for this pose.
        /// </summary>
        /// <param name="format">Specifies culture-specific formatting.</param>
        /// <returns></returns>
        public string ToString(string format)
        {
            return string.Format("({0}, {1})", (object) this.position.ToString(format), (object) this.eulerAngles.ToString(format));
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Getter / Setter
        
        /// <summary>
        /// Get the Quaternion value from the pose eulerAngles.
        /// </summary>
        public Quaternion Rotation => Quaternion.Euler(eulerAngles);
        
        /// <summary>
        /// Returns the forward vector of the pose.
        /// </summary>
        public Vector3 Forward => this.Rotation * Vector3.forward;

        /// <summary>
        /// Returns the right vector of the pose.
        /// </summary>
        public Vector3 Right => this.Rotation * Vector3.right;

        /// <summary>
        /// Returns the up vector of the pose.
        /// </summary>
        public Vector3 Up => this.Rotation * Vector3.up;
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Static Conversion
        
        /// <summary>
        /// Allows direct conversion of <see cref="GgPose"/> to <see cref="UnityEngine.Pose"/> without showing an error.
        /// </summary>
        /// <param name="ggPose"></param>
        /// <returns></returns>
        public static implicit operator Pose(GgPose ggPose)
        {
            return new Pose(ggPose.position, Quaternion.Euler(ggPose.eulerAngles));
        }
        
        /// <summary>
        /// Allows direct conversion of <see cref="UnityEngine.Pose"/> to <see cref="GgPose"/> without showing an error.
        /// </summary>
        /// <param name="pose"></param>
        /// <returns></returns>
        public static implicit operator GgPose(Pose pose)
        {
            return new GgPose(pose.position, pose.rotation.eulerAngles);
        }
        
        /// <summary>
        /// Allows direct conversion of <see cref="Transform"/> to <see cref="GgPose"/> without showing an error.
        /// </summary>
        /// <param name="transform"></param>
        /// <returns></returns>
        public static implicit operator GgPose(Transform transform)
        {
            return new GgPose(transform.position, transform.rotation.eulerAngles);
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Public Methods
        
        /// <summary>
        /// Transforms the current pose into the local space of the provided pose.
        /// </summary>
        /// <param name="lhs"></param>
        /// <returns></returns>
        public GgPose GetTransformedBy(GgPose lhs)
        {
            return new GgPose()
            {
                position = lhs.position + lhs.Rotation * this.position,
                eulerAngles = (lhs.Rotation * this.Rotation).eulerAngles
            };
        }
        
        /// <summary>
        /// Transforms the current pose into the local space of the provided pose.
        /// </summary>
        /// <param name="lhs"></param>
        /// <returns></returns>
        public GgPose GetTransformedBy(Transform lhs)
        {
            return new GgPose()
            {
                position = lhs.TransformPoint(this.position),
                eulerAngles = (lhs.rotation * this.Rotation).eulerAngles
            };
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region IEquatable
        
        public override bool Equals(object obj) => obj is GgPose other && this.Equals(other);
        
        public bool Equals(GgPose other)
        {
            return this.position.Equals(other.position) && this.eulerAngles.Equals(other.eulerAngles);
        }
        
        public override int GetHashCode()
        {
            return this.position.GetHashCode() ^ this.eulerAngles.GetHashCode() << 1;
        }
        
        public static bool operator == (GgPose a, GgPose b)
        {
            return a.position == b.position && a.eulerAngles.Equals(b.eulerAngles);
        }
        
        public static bool operator != (GgPose a, GgPose b) => !(a == b);
        
        #endregion
        
    } // class end
}
