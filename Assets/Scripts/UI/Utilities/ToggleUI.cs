using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Toggles the visibility of a specified UI element in the scene.
/// </summary>
public class ToggleUI : MonoBehaviour {
    [SerializeField] private List<GameObject> uiElements = new List<GameObject>();
    [SerializeField] private bool disableOnStart = true;

    public void Start()
    {
        if (disableOnStart)
        {
            foreach (GameObject element in uiElements)
            {
                if (element != null)
                {
                    element.SetActive(false);
                }
            }
        }
        else
        {
            foreach (GameObject element in uiElements)
            {
                if (element != null)
                {
                    element.SetActive(true);
                }
            }
        }
    }
    
    public void Toggle()
    {
        bool isActive = uiElements.Count > 0 && uiElements[0].activeSelf;

        foreach (GameObject element in uiElements)
        {
            if (element != null)
            {
                element.SetActive(!isActive);
            }
        }
    }
}