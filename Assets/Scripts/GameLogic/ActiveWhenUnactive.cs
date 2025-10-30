using System.Collections.Generic;
using UnityEngine;

namespace GameLogic
{
    public class ActiveWhenUnactive : MonoBehaviour
    {
        [Header("Active&Unactive Objects")]
        public List<GameObject> activeUnactiveObject = new List<GameObject>();
        public List<GameObject> activeObject = new List<GameObject>();
    
        [Header("Checker Object")]
        [SerializeField] private GameObject checkerObject1;
        [SerializeField] private GameObject checkerObject2;

        // Update is called once per frame
        void Update()
        {
            if (checkerObject1.activeSelf || checkerObject2.activeSelf)
            {
                foreach (GameObject obj in activeObject)
                {
                    obj.SetActive(true);
                }
                foreach (GameObject obj in activeUnactiveObject)
                {
                    obj.SetActive(false);
                }
            }
            else if (!checkerObject1.activeSelf || !checkerObject2.activeSelf)
            {
                foreach (GameObject obj in activeObject)
                {
                    obj.SetActive(false);
                }
                foreach (GameObject obj in activeUnactiveObject)
                {
                    obj.SetActive(true);
                }
            }
        }
    }
}
