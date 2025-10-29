using UnityEngine;

namespace GameLogic
{
    public class UnActiveTogether : MonoBehaviour
    {
        [Header("UnActive Together Objects")]
        public GameObject checkObjects;
        public GameObject unActiveObjects;

        // Update is called once per frame
        void Update()
        {
            if (!checkObjects.activeSelf)
            {
                unActiveObjects.SetActive(false);
            }
        }
    }
}
