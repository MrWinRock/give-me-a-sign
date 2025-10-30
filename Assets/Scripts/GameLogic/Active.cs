using System.Collections.Generic;
using UnityEngine;

public class Active : MonoBehaviour
{
    [Header("Active Objects")]
    public List<GameObject> activeObject = new List<GameObject>();
    
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        foreach (GameObject obj in activeObject)
        {
            obj.SetActive(true);
        }
    }
}
