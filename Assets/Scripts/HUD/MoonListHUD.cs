using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

/// <summary>
/// Manages the dynamic list of moons in the UI for the currently selected planet.
/// Listens to SystemListHUD and toggles the visibility of its UI container based on selection.
/// </summary>
public class MoonListHUD : MonoBehaviour
{
    [Header("Core References")]
    [Tooltip("Reference to the system list to listen for selection events.")]
    public SystemListHUD systemListHUD;
    
    [Tooltip("The child GameObject containing all the visual elements (Background, ScrollView, etc.).")]
    public GameObject bentoBoxVisualContainer; 
    
    [Header("UI Elements")]
    public TextMeshProUGUI parentPlanetNameText; 
    public Transform scrollContent;
    public GameObject moonButtonPrefab;
    
    [Header("Selection Visuals")]
    public Color normalColor = new Color(1f, 1f, 1f, 0f); 
    public Color selectedColor = new Color(0f, 0.5f, 1f, 0.5f); 
    private Image currentSelectedButtonImage;

    [Header("Events")]
    public UnityEvent<MoonData> OnMoonSelected;

    private void Start()
    {
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
            systemListHUD.OnStarSelected.AddListener(HideMoonList); 
        }
    }

    private void OnDisable()
    {
        if (systemListHUD != null)
        {
            systemListHUD.OnPlanetSelected.RemoveListener(PopulateMoonList);
            systemListHUD.OnStarSelected.RemoveListener(HideMoonList); 
        }
    }

    /// <summary>
    /// Hides the entire moon list UI. Triggered when a star is selected.
    /// </summary>
    /// <param name="starData">The star data that was selected.</param>
    private void HideMoonList(StarData starData)
    {
        if (bentoBoxVisualContainer != null)
        {
            bentoBoxVisualContainer.SetActive(false);
        }
    }

    /// <summary>
    /// Clears the current list, checks for moons, toggles visibility, and populates the UI.
    /// </summary>
    /// <param name="activePlanet">The currently selected planet whose moons will be displayed.</param>
    private void PopulateMoonList(PlanetData activePlanet)
    {
        foreach (Transform child in scrollContent)
        {
            Destroy(child.gameObject);
        }
        currentSelectedButtonImage = null;

        if (activePlanet.moons == null || activePlanet.moons.Count == 0)
        {
            if (bentoBoxVisualContainer != null)
            {
                bentoBoxVisualContainer.SetActive(false);
            }
            return;
        }

        if (bentoBoxVisualContainer != null)
        {
            bentoBoxVisualContainer.SetActive(true);
        }

        if (parentPlanetNameText != null)
        {
            parentPlanetNameText.text = $"{activePlanet.name} Moons";
        }

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
    /// Handles the click event for a moon button.
    /// </summary>
    /// <param name="selectedMoon">The moon data for the clicked button.</param>
    /// <param name="clickedImage">The image component of the clicked button.</param>
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