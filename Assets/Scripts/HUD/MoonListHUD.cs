using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

/// <summary>
/// Manages the dynamic list of moons in the UI for the currently selected planet.
/// Listens to SystemListHUD and toggles the visibility of its UI container based on moon presence.
/// </summary>
public class MoonListHUD : MonoBehaviour
{
    [Header("Core References")]
    [Tooltip("Reference to the planet list to listen for selection events.")]
    public SystemListHUD systemListHUD;
    
    [Tooltip("The child GameObject containing all the visual elements (Background, ScrollView, etc.).")]
    public GameObject bentoBoxVisualContainer; 
    
    [Header("UI Elements")]
    public TextMeshProUGUI parentPlanetNameText; 
    public Transform scrollContent;
    public GameObject moonButtonPrefab;
    
    private Color normalColor = new Color(1f, 1f, 1f, 0f); 
    private Color selectedColor = new Color(0f, 0f, 0f, 0.33f);
    private Image currentSelectedButtonImage;

    [Header("Events")]
    public UnityEvent<MoonData> OnMoonSelected;

    private void Start()
    {
        // Hide the UI completely on start, waiting for a planet selection
        if (bentoBoxVisualContainer != null)
        {
            bentoBoxVisualContainer.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (systemListHUD != null)
        {
            systemListHUD.OnPlanetSelected.AddListener(PopulateMoonList);
        }
    }

    private void OnDisable()
    {
        if (systemListHUD != null)
        {
            systemListHUD.OnPlanetSelected.RemoveListener(PopulateMoonList);
        }
    }

    /// <summary>
    /// Clears the current list, checks for moons, toggles visibility, and populates the UI.
    /// </summary>
    /// <param name="activePlanet">The planet data broadcasted by SystemListHUD.</param>
    private void PopulateMoonList(PlanetData activePlanet)
    {
        // Clear existing buttons
        foreach (Transform child in scrollContent)
        {
            Destroy(child.gameObject);
        }
        currentSelectedButtonImage = null;

        // Check if the planet has moons
        if (activePlanet.moons == null || activePlanet.moons.Count == 0)
        {
            // No moons: Hide the Bento Box and stop executing
            if (bentoBoxVisualContainer != null)
            {
                bentoBoxVisualContainer.SetActive(false);
            }
            return;
        }

        // Planet has moons: Show the Bento Box
        if (bentoBoxVisualContainer != null)
        {
            bentoBoxVisualContainer.SetActive(true);
        }

        if (parentPlanetNameText != null)
        {
            parentPlanetNameText.text = $"{activePlanet.name} Moons";
        }

        // Spawn buttons
        foreach (MoonData moon in activePlanet.moons)
        {
            GameObject newButtonObj = Instantiate(moonButtonPrefab, scrollContent);
            
            TextMeshProUGUI btnText = newButtonObj.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
            {
                btnText.text = moon.name;
            }

            Button btn = newButtonObj.GetComponent<Button>();
            Image btnImage = newButtonObj.GetComponent<Image>();
            
            if (btnImage != null)
            {
                btnImage.color = normalColor;
            }

            if (btn != null)
            {
                btn.onClick.AddListener(() => OnMoonClicked(moon, btnImage));
            }
        }
    }

    /// <summary>
    /// Handles the visual selection state and broadcasts the selected moon data.
    /// </summary>
    private void OnMoonClicked(MoonData selectedMoon, Image clickedImage)
    {
        if (currentSelectedButtonImage != null)
        {
            currentSelectedButtonImage.color = normalColor;
        }

        currentSelectedButtonImage = clickedImage;
        
        if (currentSelectedButtonImage != null)
        {
            currentSelectedButtonImage.color = selectedColor;
        }

        OnMoonSelected?.Invoke(selectedMoon);
    }
}