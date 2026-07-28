using System.Collections.Generic;
using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// Mirrors two object groups off a pair of "checker" objects:
    /// while either checker is active, activeObject items are shown and
    /// activeUnactiveObject items are hidden - and vice versa.
    /// SetActive is only called when a state actually changes.
    /// </summary>
    public class ActiveWhenUnactive : MonoBehaviour
    {
        [Header("Active&Unactive Objects")]
        public List<GameObject> activeUnactiveObject = new List<GameObject>();
        public List<GameObject> activeObject = new List<GameObject>();

        [Header("Checker Object")]
        [SerializeField] private GameObject checkerObject1;
        [SerializeField] private GameObject checkerObject2;

        void Update()
        {
            bool anyCheckerActive = checkerObject1.activeSelf || checkerObject2.activeSelf;

            SetAll(activeObject, anyCheckerActive);
            SetAll(activeUnactiveObject, !anyCheckerActive);
        }

        private static void SetAll(List<GameObject> objects, bool active)
        {
            for (int i = 0; i < objects.Count; i++)
            {
                GameObject obj = objects[i];
                if (obj != null && obj.activeSelf != active)
                    obj.SetActive(active);
            }
        }
    }
}
