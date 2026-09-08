using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events; 
using System.Collections.Generic;

/// <summary>
/// Manages the dynamic list of celestial bodies in the UI, allowing users to view and select them.
/// Supports external selection via 3D picking.
/// </summary>
public class SystemListHUD : MonoBehaviour
{
    [Header("Core References")]
    public StarSystemGenerator generator;
    
    [Header("UI Elements")]
    public TextMeshProUGUI systemNameText;
    public Transform scrollContent;
    public GameObject planetButtonPrefab;

    private Color normalColor = new Color(1f, 1f, 1f, 0f); 
    private Color selectedColor = new Color(0f, 0f, 0f, 0.33f);
    private Image currentSelectedButtonImage;

    [Header("Events")]
    public UnityEvent<PlanetData> OnPlanetSelected;
    public UnityEvent<StarData> OnStarSelected; 

    // Registry mapping body names to their physical UI buttons
    private Dictionary<string, Button> buttonRegistry = new Dictionary<string, Button>();

    private void Awake()
    {
        if (generator != null)
        {
            generator.OnSystemGenerated.AddListener(PopulatePlanetList);
        }
    }

    /// <summary>
    /// Populates the UI list with buttons for each celestial body in the generated star system.
    /// </summary>
    private void PopulatePlanetList()
    {
        systemNameText.text = generator.SystemName;

        foreach (Transform child in scrollContent)
        {
            Destroy(child.gameObject);
        }

        currentSelectedButtonImage = null; 
        buttonRegistry.Clear(); 

        if (generator.SystemStar != null) SpawnButton(generator.SystemStar.name, generator.SystemStar, null);

        if (generator.SystemPlanets != null)
        {
            foreach (PlanetData planet in generator.SystemPlanets)
            {
                SpawnButton(planet.name, null, planet);
            }
        }
    }

    /// <summary>
    /// Spawns a new button in the UI list for the specified celestial body.
    /// </summary>
    /// <param name="buttonName">The name of the button to create.</param>
    /// <param name="starData">The star data associated with the button, if applicable.</param>
    /// <param name="planetData">The planet data associated with the button, if applicable.</param>
    private void SpawnButton(string buttonName, StarData starData, PlanetData planetData)
    {
        GameObject newButtonObj = Instantiate(planetButtonPrefab, scrollContent);
            
        TextMeshProUGUI btnText = newButtonObj.GetComponentInChildren<TextMeshProUGUI>();
        if (btnText != null) btnText.text = buttonName;

        Button btn = newButtonObj.GetComponent<Button>();
        Image btnImage = newButtonObj.GetComponent<Image>();
            
        if (btnImage != null) btnImage.color = normalColor;

        if (btn != null)
        {
            btn.onClick.AddListener(() => 
            {
                if (currentSelectedButtonImage != null) currentSelectedButtonImage.color = normalColor;
                
                currentSelectedButtonImage = btnImage;
                if (currentSelectedButtonImage != null) currentSelectedButtonImage.color = selectedColor;

                if (starData != null) OnStarSelected?.Invoke(starData);
                else if (planetData != null) OnPlanetSelected?.Invoke(planetData);
            });

            // Store reference for external 3D clicking
            buttonRegistry[buttonName] = btn;
        }
    }

    /// <summary>
    /// Simulates a UI click if the body name exists in this list.
    /// </summary>
    /// <param name="name">The name of the body to select.</param>
    /// <returns>True if the body was found and selected, false otherwise.</returns>
    public bool TrySelectBody(string name)
    {
        if (buttonRegistry.TryGetValue(name, out Button btn))
        {
            btn.onClick.Invoke(); 
            return true;
        }
        return false;
    }

    /// <summary>
    /// Clears the visual selection state of the buttons.
    /// </summary>
    public void DeselectAll()
    {
        if (currentSelectedButtonImage != null)
        {
            currentSelectedButtonImage.color = normalColor;
            currentSelectedButtonImage = null;
        }
    }
}