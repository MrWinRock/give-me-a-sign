using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps every listed object active. Runs every frame so it still wins if another
/// script disables one of them, but only touches objects whose state actually changed.
/// </summary>
public class Active : MonoBehaviour
{
    [Header("Active Objects")]
    public List<GameObject> activeObject = new List<GameObject>();

    void Update()
    {
        for (int i = 0; i < activeObject.Count; i++)
        {
            GameObject obj = activeObject[i];
            if (obj != null && !obj.activeSelf)
                obj.SetActive(true);
        }
    }
}
