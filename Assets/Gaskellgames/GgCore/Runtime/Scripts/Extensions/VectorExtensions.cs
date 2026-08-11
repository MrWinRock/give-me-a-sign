using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    public static class VectorExtensions
    {
        #region Clamp
        
        /// <summary>
        /// Clamps the given value between the given minimum and maximum values.
        /// </summary>
        /// <param name="value">The value to restrict inside the range defined by the minimum and maximum values.</param>
        /// <param name="min">The minimum value to compare against.</param>
        /// <param name="max">The maximum value to compare against.</param>
        /// <returns>Returns the given value within the minimum and maximum range.</returns>
        public static Vector2 Clamp(this Vector2 value, Vector2 min, Vector2 max)
        {
            float x = Mathf.Clamp(value.x, min.x, max.x);
            float y = Mathf.Clamp(value.y, min.y, max.y);
            return new Vector2(x, y);
        }
        
        /// <summary>
        /// Clamps the given value between the given minimum and maximum values.
        /// </summary>
        /// <param name="value">The value to restrict inside the range defined by the minimum and maximum values.</param>
        /// <param name="min">The minimum value to compare against.</param>
        /// <param name="max">The maximum value to compare against.</param>
        /// <returns>Returns the given value within the minimum and maximum range.</returns>
        public static Vector3 Clamp(this Vector3 value, Vector3 min, Vector3 max)
        {
            float x = Mathf.Clamp(value.x, min.x, max.x);
            float y = Mathf.Clamp(value.y, min.y, max.y);
            float z = Mathf.Clamp(value.z, min.z, max.z);
            return new Vector3(x, y, z);
        }
        
        /// <summary>
        /// Clamps the given value between the given minimum and maximum values.
        /// </summary>
        /// <param name="value">The value to restrict inside the range defined by the minimum and maximum values.</param>
        /// <param name="min">The minimum value to compare against.</param>
        /// <param name="max">The maximum value to compare against.</param>
        /// <returns>Returns the given value within the minimum and maximum range.</returns>
        public static Vector4 Clamp(this Vector4 value, Vector4 min, Vector4 max)
        {
            float x = Mathf.Clamp(value.x, min.x, max.x);
            float y = Mathf.Clamp(value.y, min.y, max.y);
            float z = Mathf.Clamp(value.z, min.z, max.z);
            float w = Mathf.Clamp(value.w, min.w, max.w);
            return new Vector4(x, y, z, w);
        }
        
        #endregion
        
    	//----------------------------------------------------------------------------------------------------
        
        #region Clamp01
        
        /// <summary>
        /// Clamps the given value between the vector's 0 and 1 value.
        /// </summary>
        /// <param name="value">The value to restrict inside the range of 0 and 1.</param>
        /// <returns></returns>
        public static Vector2 Clamp01(this Vector2 value)
        {
            return value.Clamp(Vector2.zero, Vector2.one);
        }
        
        /// <summary>
        /// Clamps the given value between the vector's 0 and 1 value.
        /// </summary>
        /// <param name="value">The value to restrict inside the range of 0 and 1.</param>
        /// <returns></returns>
        public static Vector3 Clamp01(this Vector3 value)
        {
            return value.Clamp(Vector3.zero, Vector3.one);
        }
        
        /// <summary>
        /// Clamps the given value between the vector's 0 and 1 value.
        /// </summary>
        /// <param name="value">The value to restrict inside the range of 0 and 1.</param>
        /// <returns></returns>
        public static Vector4 Clamp01(this Vector4 value)
        {
            return value.Clamp(Vector4.zero, Vector4.one);
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Average
        
        /// <summary>
        /// Get the average value of a vectors components.
        /// </summary>
        /// <param name="value">The value to average its components.</param>
        /// <returns>Returns the average value of each component of the vector.</returns>
        public static float Average(this Vector2 value)
        {
            return (value.x + value.y) / 2f;
        }
        
        /// <summary>
        /// Get the average value of a vectors components.
        /// </summary>
        /// <param name="value">The value to average its components.</param>
        /// <returns>Returns the average value of each component of the vector.</returns>
        public static float Average(this Vector3 value)
        {
            return (value.x + value.y + value.z) / 3f;
        }
        
        /// <summary>
        /// Get the average value of a vectors components.
        /// </summary>
        /// <param name="value">The value to average its components.</param>
        /// <returns>Returns the average value of each component of the vector.</returns>
        public static float Average(this Vector4 value)
        {
            return (value.x + value.y + value.z + value.w) / 4f;
        }
        
        #endregion
        
    } // class end
}