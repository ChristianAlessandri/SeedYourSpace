using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the dynamic list of planets in the UI, allowing users to view and select planets from the generated star system.
/// </summary>
public class SystemListHUD : MonoBehaviour
{
    [Header("Core References")]
    public StarSystemGenerator generator;
    
    [Header("UI Elements")]
    public TextMeshProUGUI systemNameText;
    public Transform scrollContent;
    public GameObject planetButtonPrefab;

    [Header("Selection Visuals")]
    public Color normalColor = new Color(1f, 1f, 1f, 0f); 
    public Color selectedColor = new Color(0f, 0f, 0f, 0.33f); 
    private Image currentSelectedButtonImage;

    private void Start()
    {
        if (generator != null)
        {
            generator.OnSystemGenerated.AddListener(PopulatePlanetList);
        }
    }

    private void PopulatePlanetList()
    {
        systemNameText.text = generator.SystemName;

        foreach (Transform child in scrollContent)
        {
            Destroy(child.gameObject);
        }

        currentSelectedButtonImage = null; 

        foreach (PlanetData planet in generator.SystemPlanets)
        {
            GameObject newButtonObj = Instantiate(planetButtonPrefab, scrollContent);
            
            TextMeshProUGUI btnText = newButtonObj.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
            {
                btnText.text = planet.name;
            }

            Button btn = newButtonObj.GetComponent<Button>();
            Image btnImage = newButtonObj.GetComponent<Image>();
            
            if (btnImage != null)
            {
                btnImage.color = normalColor;
            }

            if (btn != null)
            {
                btn.onClick.AddListener(() => OnPlanetClicked(planet, btnImage));
            }
        }
    }

    private void OnPlanetClicked(PlanetData selectedPlanet, Image clickedImage)
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

        //! PLACEHOLDER
        Debug.Log($"Active Planet: {selectedPlanet.name}");
    }
}