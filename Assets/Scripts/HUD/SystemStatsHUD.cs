using UnityEngine;
using TMPro;

/// <summary>
/// Handles the real-time display of system statistics and performance metrics on the UI.
/// </summary>
public class SystemStatsHUD : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI statsText;
    public StarSystemGenerator generator;

    [Header("Settings")]
    [Tooltip("How often the FPS counter updates (in seconds).")]
    public float refreshRate = 0.5f;

    private float timer;
    private int frameCount;

    private void Update()
    {
        // FPS calculation using unscaled time to ignore time scaling effects
        timer += Time.unscaledDeltaTime;
        frameCount++;

        if (timer >= refreshRate)
        {
            int currentFps = Mathf.RoundToInt(frameCount / timer);
            UpdateDisplay(currentFps);
            
            timer -= refreshRate;
            frameCount = 0;
        }
    }

    /// <summary>
    /// Updates the text component with the latest data from the generator and performance metrics.
    /// </summary>
    /// <param name="fps">The currently calculated frames per second.</param>
    private void UpdateDisplay(int fps)
    {
        if (generator == null || statsText == null) return;

        // Central star (1) + planets + moons + rings
        int totalEntities = 1 + generator.TotalPlanets + generator.TotalMoons + generator.TotalRings;

        statsText.text = $"FPS: {fps}\n" +
                         $"Entities: {totalEntities}\n" +
                         $"Planets: {generator.TotalPlanets}\n" +
                         $"Moons: {generator.TotalMoons}\n" +
                         $"Rings: {generator.TotalRings}";
    }
}