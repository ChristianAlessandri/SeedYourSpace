using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays the physical and orbital statistics of a selected celestial body.
/// Manages the transition between 3D Miniature and 2D Atlas modes.
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

    [Header("Atlas UI")]
    public Button viewToggleButton;
    public TextMeshProUGUI viewToggleText;

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
            systemListHUD.OnStarSelected.AddListener(UpdateDetails);
        }

        if (moonListHUD != null)
        {
            moonListHUD.OnMoonSelected.AddListener(UpdateDetails);
        }

        if (viewToggleButton != null)
        {
            viewToggleButton.onClick.AddListener(OnViewToggleClicked);
        }
    }

    private void OnDisable()
    {
        if (systemListHUD != null)
        {
            systemListHUD.OnPlanetSelected.RemoveListener(UpdateDetails);
            systemListHUD.OnStarSelected.RemoveListener(UpdateDetails);
        }

        if (moonListHUD != null)
        {
            moonListHUD.OnMoonSelected.RemoveListener(UpdateDetails);
        }

        if (viewToggleButton != null)
        {
            viewToggleButton.onClick.RemoveListener(OnViewToggleClicked);
        }
    }

    /// <summary>
    /// Handles the click event for the Atlas/Miniature switch button.
    /// </summary>
    private void OnViewToggleClicked()
    {
        if (miniatureRenderer != null)
        {
            miniatureRenderer.ToggleViewMode();
            UpdateButtonText();
        }
    }

    /// <summary>
    /// Updates the text on the toggle button based on the renderer's current state.
    /// </summary>
    private void UpdateButtonText()
    {
        if (viewToggleText != null && miniatureRenderer != null)
        {
            viewToggleText.text = miniatureRenderer.IsAtlasMode ? "Switch to Miniature Mode" : "Switch to Atlas Mode";
        }
    }

    /// <summary>
    /// Updates the details display for a selected celestial body.
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
            bool isMoon = body.name.Contains("-");
            string distanceLabel = isMoon ? "Orbital Dist (Planet)" : "Orbital Dist (Star)";
            string distanceUnit = isMoon ? "R_E" : "AU";

            string revolutionString = isMoon ? $"{body.revolutionPeriod:F1} d" : $"{(body.revolutionPeriod / 365.25f):F2} y";

            detailsText.text = 
                $"Temperature: {Mathf.RoundToInt(body.surfaceTemperature)} K\n" +
                $"{distanceLabel}: {body.orbitalDistance:F2} {distanceUnit}\n" +
                $"Eccentricity: {body.orbitalEccentricity:F3}\n" +
                $"Revolution: {revolutionString}\n" +
                $"Axial Tilt: {body.axialTilt:F1}°\n" +
                $"Spin: {body.rotationPeriod:F1} h\n" +
                $"Weight (Mass): {body.mass:F2} M_E\n" +
                $"Radii: {body.radius:F2} R_E\n" +
                $"Atmosphere: {body.atmosphereType}";
        }

        // Enable the Atlas button for planets/moons and reset to 3D mode on fresh click
        if (viewToggleButton != null) viewToggleButton.gameObject.SetActive(true);
        if (miniatureRenderer != null)
        {
            miniatureRenderer.ResetTo3DMode();
            miniatureRenderer.BuildMiniature(body);
        }
        UpdateButtonText();
    }

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
                $"Axial Tilt: {star.axialTilt:F1}°\n" +
                $"Spin: {star.rotationPeriod:F1} h\n" +
                $"Weight (Mass): {star.mass:F2} M_S\n" +
                $"Radii: {star.radius:F2} R_S\n" +
                $"Magnetic Activity: {(star.magneticActivity * 100f):F1}%";
        }

        // Hide the Atlas button for the sun, since we cannot map/land on it
        if (viewToggleButton != null) viewToggleButton.gameObject.SetActive(false);
        if (miniatureRenderer != null) miniatureRenderer.BuildMiniature(star);
    }

    public void ClearDetails()
    {
        if (bentoBoxVisualContainer != null) bentoBoxVisualContainer.SetActive(false);
    }
}