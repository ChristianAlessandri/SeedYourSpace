using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays the physical and orbital statistics of a selected celestial body.
/// Acts as a standalone module listening to star, planet, and moon selection events.
/// </summary>
public class CelestialBodyDetailHUD : MonoBehaviour
{
    [Header("Event Listeners")]
    public SystemListHUD systemListHUD;
    public MoonListHUD moonListHUD;

    [Header("Container")]
    public GameObject bentoBoxVisualContainer;

    [Header("UI Text Elements")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI classText;
    public TextMeshProUGUI detailsText;

    [Header("Rendering Module")]
    public CelestialMiniatureRenderer miniatureRenderer;

    private void Start()
    {
        if (bentoBoxVisualContainer != null) bentoBoxVisualContainer.SetActive(false);
    }

    private void OnEnable()
    {
        if (systemListHUD != null)
        {
            systemListHUD.OnPlanetSelected.AddListener(UpdateDetails);
            systemListHUD.OnStarSelected.AddListener(UpdateDetails); // NEW
        }

        if (moonListHUD != null)
        {
            moonListHUD.OnMoonSelected.AddListener(UpdateDetails);
        }
    }

    private void OnDisable()
    {
        if (systemListHUD != null)
        {
            systemListHUD.OnPlanetSelected.RemoveListener(UpdateDetails);
            systemListHUD.OnStarSelected.RemoveListener(UpdateDetails); // NEW
        }

        if (moonListHUD != null)
        {
            moonListHUD.OnMoonSelected.RemoveListener(UpdateDetails);
        }
    }

    /// <summary>
    /// Updates the HUD with the details of the selected celestial body, whether it's a planet, moon, or star.
    /// This method formats and displays the relevant statistics in the UI text elements and triggers the miniature renderer to visualize the body.
    /// </summary>
    /// <param name="body">The celestial body data to display.</param>
    private void UpdateDetails(CelestialBodyData body)
    {
        if (body == null) return;
        if (bentoBoxVisualContainer != null) bentoBoxVisualContainer.SetActive(true);

        if (nameText != null) nameText.text = body.name;
        if (classText != null) classText.text = body.className;

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

        if (miniatureRenderer != null) miniatureRenderer.BuildMiniature(body);
    }

    /// <summary>
    /// Updates the HUD with the details of the selected star.
    /// This method formats and displays the relevant statistics in the UI text elements and triggers the miniature renderer to visualize the star.
    /// </summary>
    /// <param name="star">The star data to display.</param>
    private void UpdateDetails(StarData star)
    {
        if (star == null) return;
        if (bentoBoxVisualContainer != null) bentoBoxVisualContainer.SetActive(true);

        if (nameText != null) nameText.text = star.name;
        if (classText != null) classText.text = $"Star ({star.spectralClass})";

        if (detailsText != null)
        {
            detailsText.text = 
                $"Temperature: {Mathf.RoundToInt(star.temperature)} K\n" +
                $"Frost Line: {star.frostLine:F2} AU\n" +
                $"Spin: {star.rotationPeriod:F1} h\n" +
                $"Weight (Mass): {star.mass:F2} M_S\n" +
                $"Radii: {star.radius:F2} R_S\n" +
                $"Magnetic Activity: {(star.magneticActivity * 100f):F1}%";
        }

        if (miniatureRenderer != null) miniatureRenderer.BuildMiniature(star);
    }

    /// <summary>
    /// Hides the detail panel when empty space is clicked.
    /// </summary>
    public void ClearDetails()
    {
        if (bentoBoxVisualContainer != null) bentoBoxVisualContainer.SetActive(false);
    }
}