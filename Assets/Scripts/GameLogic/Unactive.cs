using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps every listed object inactive. Runs every frame so it still wins if another
/// script enables one of them, but only touches objects whose state actually changed.
/// </summary>
public class Unactive : MonoBehaviour
{
    [Header("Unactive Objects")]
    public List<GameObject> unActiveObject = new List<GameObject>();

    void Update()
    {
        for (int i = 0; i < unActiveObject.Count; i++)
        {
            GameObject obj = unActiveObject[i];
            if (obj != null && obj.activeSelf)
                obj.SetActive(false);
        }
    }
}
