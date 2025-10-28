using UnityEngine;

public class UnActiveTogether : MonoBehaviour
{
    [Header("UnActive Together Objects")]
    public GameObject checkObjects;
    public GameObject unActiveObjects;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (!checkObjects.activeSelf)
        {
            unActiveObjects.SetActive(false);
        }
    }
}
