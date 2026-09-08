using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;
using System.Collections.Generic;

/// <summary>
/// Manages the dynamic list of moons. Supports external selection via 3D picking.
/// </summary>
public class MoonListHUD : MonoBehaviour
{
    [Header("Core References")]
    public SystemListHUD systemListHUD;
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

    // Registry mapping body names to their physical UI buttons
    private Dictionary<string, Button> buttonRegistry = new Dictionary<string, Button>();

    private void Start()
    {
        HideCompletely();
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

    private void HideMoonList(StarData starData) => HideCompletely();

    /// <summary>
    /// Populates the moon list based on the selected planet's moons. If no moons exist, the list is hidden.
    /// </summary>
    /// <param name="activePlanet">The currently selected planet.</param>
    private void PopulateMoonList(PlanetData activePlanet)
    {
        foreach (Transform child in scrollContent)
        {
            Destroy(child.gameObject);
        }
        
        currentSelectedButtonImage = null;
        buttonRegistry.Clear();

        if (activePlanet.moons == null || activePlanet.moons.Count == 0)
        {
            HideCompletely();
            return;
        }

        if (bentoBoxVisualContainer != null) bentoBoxVisualContainer.SetActive(true);
        if (parentPlanetNameText != null) parentPlanetNameText.text = $"{activePlanet.name} Moons";

        foreach (MoonData moon in activePlanet.moons)
        {
            GameObject newButtonObj = Instantiate(moonButtonPrefab, scrollContent);
            
            TextMeshProUGUI btnText = newButtonObj.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null) btnText.text = moon.name;

            Button btn = newButtonObj.GetComponent<Button>();
            Image btnImage = newButtonObj.GetComponent<Image>();
            
            if (btnImage != null) btnImage.color = normalColor;

            if (btn != null)
            {
                btn.onClick.AddListener(() => OnMoonClicked(moon, btnImage));
                buttonRegistry[moon.name] = btn;
            }
        }
    }

    /// <summary>
    /// Handles the click event for a moon button.
    /// </summary>
    /// <param name="selectedMoon">The moon data associated with the clicked button.</param>
    /// <param name="clickedImage">The image component of the clicked button.</param>
    private void OnMoonClicked(MoonData selectedMoon, Image clickedImage)
    {
        if (currentSelectedButtonImage != null) currentSelectedButtonImage.color = normalColor;
        currentSelectedButtonImage = clickedImage;
        if (currentSelectedButtonImage != null) currentSelectedButtonImage.color = selectedColor;

        OnMoonSelected?.Invoke(selectedMoon);
    }

    /// <summary>
    /// Simulates a UI click if the moon name exists in this list.
    /// </summary>
    /// <param name="name">The name of the moon to select.</param>
    /// <returns>True if the moon was found and selected, false otherwise.</returns>
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
    /// Deselects any currently selected moon button, resetting its visual state.
    /// </summary>
    public void DeselectAll()
    {
        if (currentSelectedButtonImage != null)
        {
            currentSelectedButtonImage.color = normalColor;
            currentSelectedButtonImage = null;
        }
    }

    /// <summary>
    /// Hides the moon list and its container completely, typically used when no moons are available or when a star is selected.
    /// </summary>
    public void HideCompletely()
    {
        if (bentoBoxVisualContainer != null) bentoBoxVisualContainer.SetActive(false);
    }
}