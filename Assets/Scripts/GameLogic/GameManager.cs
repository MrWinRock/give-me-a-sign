using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Background Settings")]
    public List<GameObject> backgrounds = new List<GameObject>();
    private int currentBackgroundIndex = 0;
    [Header("CameraOBJ")]
    public GameObject cameraObjects;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        Debug.Log(backgrounds.Count);
        Debug.Log(currentBackgroundIndex);
        if (currentBackgroundIndex == 0)
        {
            Vector3 position = cameraObjects.transform.position;
            position.x = 0;
            cameraObjects.transform.position = position;
        }
        else if (currentBackgroundIndex == 1)
        {
            Vector3 position = cameraObjects.transform.position;
            position.x = 17.73f;
            cameraObjects.transform.position = position;
        }
        else if (currentBackgroundIndex == 2)
        {
            Vector3 position = cameraObjects.transform.position;
            position.x = 36.12f;
            cameraObjects.transform.position = position;
        }
    }
    
    private void OnNextClick()
    {
        currentBackgroundIndex = currentBackgroundIndex + 1;
    }
    
    private void OnPreviousClick()
    {
        currentBackgroundIndex = currentBackgroundIndex - 1;
    }
}
