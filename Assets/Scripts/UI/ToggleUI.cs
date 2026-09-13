using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Toggles the visibility of a specified UI element in the scene.
/// </summary>
public class ToggleUI : MonoBehaviour {
    [SerializeField] private List<GameObject> uiElements = new List<GameObject>();
    
    public void Toggle()
    {
        foreach (GameObject element in uiElements)
        {
            if (element != null)
            {
                element.SetActive(!element.activeSelf);
            }
        }
    }
}