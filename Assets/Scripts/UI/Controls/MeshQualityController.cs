using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MeshQualityController : MonoBehaviour
{
    [Header("UI References")]
    public Slider detailSlider;
    public TextMeshProUGUI detailText;

    void Start()
    {
        // Set the slider's range and ensure it snaps to whole numbers
        detailSlider.minValue = 0f;
        detailSlider.maxValue = 3f;
        detailSlider.wholeNumbers = true;
        
        // Subscribe to the slider's value change event purely via code
        detailSlider.onValueChanged.AddListener(UpdateMeshQuality);
        
        // Initialize the UI text and models
        UpdateMeshQuality(detailSlider.value);
    }

    private void UpdateMeshQuality(float newDetail)
    {
        int subdivisions = Mathf.RoundToInt(newDetail);

        // Update the UI Text based on the subdivision tier
        switch (subdivisions)
        {
            case 0: detailText.text = "Mesh Detail: Low (20 Faces)"; break;
            case 1: detailText.text = "Mesh Detail: Medium (80 Faces)"; break;
            case 2: detailText.text = "Mesh Detail: High (320 Faces)"; break;
            case 3: detailText.text = "Mesh Detail: Ultra (1280 Faces)"; break;
        }

        // Find all dynamically generated celestial bodies in the scene
        LowPolyMeshGenerator[] generators = Object.FindObjectsByType<LowPolyMeshGenerator>(FindObjectsSortMode.None);
        
        // Apply the new quality setting to all of them
        foreach (var gen in generators)
        {
            gen.SetSubdivisions(subdivisions);
        }
    }
}