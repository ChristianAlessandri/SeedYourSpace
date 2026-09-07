using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays the physical and orbital statistics of a selected celestial body.
/// Acts as a standalone module listening to both planet and moon selection events.
/// </summary>
public class CelestialBodyDetailHUD : MonoBehaviour
{
    [Header("Event Listeners")]
    [Tooltip("Listen for planet selections.")]
    public SystemListHUD systemListHUD;
    [Tooltip("Listen for moon selections.")]
    public MoonListHUD moonListHUD;

    [Header("Container")]
    [Tooltip("The main visual container to toggle visibility.")]
    public GameObject bentoBoxVisualContainer;

    [Header("UI Text Elements")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI classText;
    public TextMeshProUGUI detailsText;

    [Header("3D Miniature Placeholder (Future Phase)")]
    [Tooltip("Will be used later for the Render Texture projection.")]
    public RawImage miniatureDisplay;

    private void Start()
    {
        // Hide panel on start
        if (bentoBoxVisualContainer != null)
        {
            bentoBoxVisualContainer.SetActive(false);
        }
    }

    private void OnEnable()
    {
        // Subscribe to events. We can cast PlanetData and MoonData to CelestialBodyData implicitly.
        if (systemListHUD != null)
        {
            systemListHUD.OnPlanetSelected.AddListener(UpdateDetails);
        }

        if (moonListHUD != null)
        {
            moonListHUD.OnMoonSelected.AddListener(UpdateDetails);
        }
    }

    private void OnDisable()
    {
        // Unsubscribe to prevent memory leaks
        if (systemListHUD != null)
        {
            systemListHUD.OnPlanetSelected.RemoveListener(UpdateDetails);
        }

        if (moonListHUD != null)
        {
            moonListHUD.OnMoonSelected.RemoveListener(UpdateDetails);
        }
    }

    /// <summary>
    /// Populates the UI with the selected body's data and shows the panel.
    /// Accepts CelestialBodyData, meaning it works for both Planets and Moons.
    /// </summary>
    /// <param name="body">The data of the selected celestial body.</param>
    private void UpdateDetails(CelestialBodyData body)
    {
        if (body == null) return;

        // Show the container
        if (bentoBoxVisualContainer != null)
        {
            bentoBoxVisualContainer.SetActive(true);
        }

        // Update Headers
        if (nameText != null) nameText.text = body.name;
        if (classText != null) classText.text = body.className;

        // Update Stats Block
        if (detailsText != null)
        {
            detailsText.text = 
                $"Temperature: {Mathf.RoundToInt(body.surfaceTemperature)} K\n" +
                $"Eccentricity: {body.orbitalEccentricity:F3}\n" +
                $"Spin: {body.rotationPeriod:F1} h\n" +
                $"Weight (Mass): {body.mass:F2} M_E\n" +
                $"Radii: {body.radius:F2} R_E\n" +
                $"Atmosphere: {body.atmosphereType}";
        }
    }
}