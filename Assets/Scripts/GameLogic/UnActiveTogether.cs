using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// When checkObjects turns inactive, unActiveObjects is turned off with it.
    /// </summary>
    public class UnActiveTogether : MonoBehaviour
    {
        [Header("UnActive Together Objects")]
        public GameObject checkObjects;
        public GameObject unActiveObjects;

        void Update()
        {
            if (!checkObjects.activeSelf && unActiveObjects.activeSelf)
            {
                unActiveObjects.SetActive(false);
            }
        }
    }
}
