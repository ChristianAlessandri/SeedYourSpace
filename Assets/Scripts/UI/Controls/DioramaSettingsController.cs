using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DioramaSettingsController : MonoBehaviour
{
    [Header("Core References")]
    public VisualDioramaBuilder dioramaBuilder;

    [Header("Sliders")]
    public Slider starSizeSlider;
    public Slider planetSizeSlider;
    public Slider planetDistSlider;
    public Slider moonDistSlider;

    [Header("Text Labels")]
    public TextMeshProUGUI starSizeText;
    public TextMeshProUGUI planetSizeText;
    public TextMeshProUGUI planetDistText;
    public TextMeshProUGUI moonDistText;

    void Start()
    {
        // STAR SIZE (Default: 100 | Range: 10 - 500)
        starSizeSlider.wholeNumbers = true;
        starSizeSlider.minValue = 10f;
        starSizeSlider.maxValue = 500f;
        starSizeSlider.value = 75f;

        // PLANET SIZE (Default: 1 | Range: 1 - 20)
        planetSizeSlider.wholeNumbers = true;
        planetSizeSlider.minValue = 1f;
        planetSizeSlider.maxValue = 20f;
        planetSizeSlider.value = 1f;

        // PLANET DISTANCE (Default: 200 | Range: 50 - 1000)
        planetDistSlider.wholeNumbers = true;
        planetDistSlider.minValue = 50f;
        planetDistSlider.maxValue = 1000f;
        planetDistSlider.value = 900f;

        // MOON DISTANCE (Default: 0.15 | Range: 0.05 - 5.0)
        moonDistSlider.wholeNumbers = false; 
        moonDistSlider.minValue = 0.05f;
        moonDistSlider.maxValue = 5f;
        moonDistSlider.value = 0.75f;

        // Subscribe to slider value changes to update the diorama settings in real-time
        starSizeSlider.onValueChanged.AddListener(UpdateSettings);
        planetSizeSlider.onValueChanged.AddListener(UpdateSettings);
        planetDistSlider.onValueChanged.AddListener(UpdateSettings);
        moonDistSlider.onValueChanged.AddListener(UpdateSettings);
        
        // Initialize the UI (passing a dummy value to activate the UI)
        UpdateSettings(0f); 
    }

    private void UpdateSettings(float dummyValue)
    {
        float starSize = starSizeSlider.value;
        float planetSize = planetSizeSlider.value;
        float planetDist = planetDistSlider.value;
        float moonDist = moonDistSlider.value;

        // Update the UI text labels to reflect the current slider values
        starSizeText.text = $"Star Size Mul: {starSize}x";
        planetSizeText.text = $"Planet Size Mul: {planetSize}x";
        planetDistText.text = $"Planet Distance Mul: {planetDist}x";
        moonDistText.text = $"Moon Distance Mul: {moonDist:F2}x";

        // Send the updated multipliers to the diorama builder for real-time adjustments
        if (dioramaBuilder != null)
        {
            dioramaBuilder.UpdateMultipliers(starSize, planetSize, planetDist, moonDist);
        }
    }
}