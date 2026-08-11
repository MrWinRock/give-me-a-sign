#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    public static class GgAlignToolUtilities
    {
        public enum Axis
        {
            X,
            Y,
            Z
        }
        
        private static Axis selectedAxis = Axis.X;
        
        public static event Action<Axis> onAxisChanged;
        
        public static Axis SelectedAxis
        {
            get => selectedAxis;
            set
            {
                selectedAxis = value;
                onAxisChanged?.Invoke(value);
            }
        }
        
        public static void AlignMin(Axis axis)
        {
            Transform[] transformTargets = Selection.transforms;
            if (transformTargets.Length <= 1) { return; }
			
            float min = GetMinPosition(transformTargets, axis);
            Undo.RecordObjects(transformTargets, $"AlignMin{axis}");
            foreach (Transform target in transformTargets)
            {
                Vector3 position = target.position;
                switch (axis)
                {
                    case Axis.X:
                        position = new Vector3(min, position.y, position.z);
                        break;
					
                    case Axis.Y:
                        position = new Vector3(position.x, min, position.z);
                        break;
					
                    case Axis.Z:
                        position = new Vector3(position.x, position.y, min);
                        break;
                }
                target.position = position;
            }
        }
		
        public static void AlignMid(Axis axis)
        {
            Transform[] transformTargets = Selection.transforms;
            if (transformTargets.Length <= 1) { return; }
			
            float min = GetMinPosition(transformTargets, axis);
            float max = GetMaxPosition(transformTargets, axis);
            float mid = (min + max) * 0.5f;
            Undo.RecordObjects(transformTargets.ToArray(), $"AlignMid{axis}");
            foreach (Transform target in transformTargets)
            {
                Vector3 position = target.position;
                switch (axis)
                {
                    case Axis.X:
                        position = new Vector3(mid, position.y, position.z);
                        break;
					
                    case Axis.Y:
                        position = new Vector3(position.x, mid, position.z);
                        break;
					
                    case Axis.Z:
                        position = new Vector3(position.x, position.y, mid);
                        break;
                }
                target.position = position;
            }
        }
		
        public static void AlignMax(Axis axis)
        {
            Transform[] transformTargets = Selection.transforms;
            if (transformTargets.Length <= 1) { return; }
			
            float max = GetMaxPosition(transformTargets, axis);
            Undo.RecordObjects(transformTargets.ToArray(), $"AlignMax{axis}");
            foreach (Transform target in transformTargets)
            {
                Vector3 position = target.position;
                switch (axis)
                {
                    case Axis.X:
                        position = new Vector3(max, position.y, position.z);
                        break;
					
                    case Axis.Y:
                        position = new Vector3(position.x, max, position.z);
                        break;
					
                    case Axis.Z:
                        position = new Vector3(position.x, position.y, max);
                        break;
                }
                target.position = position;
            }
        }
		
        public static void AlignDistribute(Axis axis)
        {
            Transform[] transformTargets = Selection.transforms;
            if (transformTargets.Length <= 1) { return; }
			
            float min = GetMinPosition(transformTargets, axis);
            float max = GetMaxPosition(transformTargets, axis);
            float gap = transformTargets.Length <= 1 ? 0 : (max - min) / (float)(transformTargets.Length - 1);

            IOrderedEnumerable<Transform> orderedEnumerable = null;
            switch (axis)
            {
                case Axis.X:
                    orderedEnumerable = transformTargets.OrderBy((c) => c.transform.position.x);
                    break;
					
                case Axis.Y:
                    orderedEnumerable = transformTargets.OrderBy((c) => c.transform.position.y);
                    break;
					
                case Axis.Z:
                    orderedEnumerable = transformTargets.OrderBy((c) => c.transform.position.z);
                    break;
            }
            List<Transform> sortedTargets = new List<Transform>(orderedEnumerable);
            if (sortedTargets.Count <= 0) { return; }

            Undo.RecordObjects(sortedTargets.ToArray(), $"AlignDistribute{axis}");
            for (int i = 0; i < sortedTargets.Count; i++)
            {
                Vector3 position = sortedTargets[i].position;
                switch (axis)
                {
                    case Axis.X:
                        position = new Vector3(min + (gap * i), position.y, position.z);
                        break;
					
                    case Axis.Y:
                        position = new Vector3(position.x, min + (gap * i), position.z);
                        break;
					
                    case Axis.Z:
                        position = new Vector3(position.x, position.y, min + (gap * i));
                        break;
                }
                sortedTargets[i].position = position;
            }
        }
		
        public static float GetMinPosition(Transform[] transformTargets, Axis axis)
        {
            bool isFirst = true;
            float min = 0;
            switch (axis)
            {
                case Axis.X:
                    foreach (Transform target in transformTargets)
                    {
                        if (min < target.position.x && !isFirst) { continue;}
                        min = target.position.x;
                        isFirst = false;
                    }
                    break;
				
                case Axis.Y:
                    foreach (Transform target in transformTargets)
                    {
                        if (min < target.position.y && !isFirst) { continue;}
                        min = target.position.y;
                        isFirst = false;
                    }
                    break;
				
                case Axis.Z:
                    foreach (Transform target in transformTargets)
                    {
                        if (min < target.position.z && !isFirst) { continue;}
                        min = target.position.z;
                        isFirst = false;
                    }
                    break;
            }
            return min;
        }

        public static float GetMaxPosition(Transform[] transformTargets, Axis axis)
        {
            bool isFirst = true;
            float max = 0;
            switch (axis)
            {
                case Axis.X:
                    foreach (Transform target in transformTargets)
                    {
                        if (target.position.x < max && !isFirst) { continue;}
                        max = target.position.x;
                        isFirst = false;
                    }
                    break;
				
                case Axis.Y:
                    foreach (Transform target in transformTargets)
                    {
                        if (target.position.y < max && !isFirst) { continue;}
                        max = target.position.y;
                        isFirst = false;
                    }
                    break;
				
                case Axis.Z:
                    foreach (Transform target in transformTargets)
                    {
                        if (target.position.z < max && !isFirst) { continue;}
                        max = target.position.z;
                        isFirst = false;
                    }
                    break;
            }
            return max;
        }
        
    } // class end
}
#endif