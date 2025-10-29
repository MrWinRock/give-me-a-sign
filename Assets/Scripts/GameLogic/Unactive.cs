using System.Collections.Generic;
using UnityEngine;

public class Unactive : MonoBehaviour
{
    [Header("Active Objects")]
    public List<GameObject> unActiveObject = new List<GameObject>();
    
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        foreach (GameObject obj in unActiveObject)
        {
            obj.SetActive(false);
        }
    }
}
