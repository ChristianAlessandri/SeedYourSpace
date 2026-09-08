using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events; 

/// <summary>
/// Manages the dynamic list of celestial bodies in the UI, allowing users to view and select them.
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

    private void Awake()
    {
        if (generator != null)
        {
            generator.OnSystemGenerated.AddListener(PopulatePlanetList);
        }
    }

    /// <summary>
    /// Populates the UI list with buttons representing the star and planets in the generated system. Each button is set up to trigger the appropriate event when clicked.
    /// </summary>
    private void PopulatePlanetList()
    {
        systemNameText.text = generator.SystemName;

        foreach (Transform child in scrollContent)
        {
            Destroy(child.gameObject);
        }

        currentSelectedButtonImage = null;

        // Spawn the Star Button first
        if (generator.SystemStar != null)
        {
            SpawnButton(generator.SystemStar.name, generator.SystemStar, null);
        }

        // Spawn the Planet Buttons
        if (generator.SystemPlanets != null)
        {
            foreach (PlanetData planet in generator.SystemPlanets)
            {
                SpawnButton(planet.name, null, planet);
            }
        }
    }

    /// <summary>
    /// Spawns a button in the UI for either a star or a planet, based on the provided data.
    /// </summary>
    /// <param name="buttonName">The display name for the button.</param>
    /// <param name="starData">The data for the star, if applicable.</param
    /// <param name="planetData">The data for the planet, if applicable.</param>
    private void SpawnButton(string buttonName, StarData starData, PlanetData planetData)
    {
        GameObject newButtonObj = Instantiate(planetButtonPrefab, scrollContent);
            
        TextMeshProUGUI btnText = newButtonObj.GetComponentInChildren<TextMeshProUGUI>();
        if (btnText != null)
        {
            btnText.text = buttonName;
        }

        Button btn = newButtonObj.GetComponent<Button>();
        Image btnImage = newButtonObj.GetComponent<Image>();
            
        if (btnImage != null)
        {
            btnImage.color = normalColor;
        }

        if (btn != null)
        {
            btn.onClick.AddListener(() => 
            {
                if (currentSelectedButtonImage != null) currentSelectedButtonImage.color = normalColor;
                
                currentSelectedButtonImage = btnImage;
                if (currentSelectedButtonImage != null) currentSelectedButtonImage.color = selectedColor;

                // Fire the appropriate event based on what was passed
                if (starData != null) OnStarSelected?.Invoke(starData);
                else if (planetData != null) OnPlanetSelected?.Invoke(planetData);
            });
        }
    }
}