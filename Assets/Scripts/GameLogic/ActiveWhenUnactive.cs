using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ActiveWhenUnactive : MonoBehaviour
{
    [Header("Active&Unactive Objects")]
    public List<GameObject> activeUnactiveObject = new List<GameObject>();
    
    [Header("Checker Object")]
    [SerializeField] private GameObject checkerObject1;
    [SerializeField] private GameObject checkerObject2;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (checkerObject1.activeSelf)
        {
            foreach (GameObject obj in activeUnactiveObject)
            {
                obj.SetActive(false);
            }
        }
        else if (!checkerObject1.activeSelf)
        {
            foreach (GameObject obj in activeUnactiveObject)
            {
                obj.SetActive(true);
            }
        }
    }
}
