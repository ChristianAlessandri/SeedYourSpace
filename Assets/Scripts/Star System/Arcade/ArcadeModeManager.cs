using UnityEngine;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// Manages the deterministic spawning of interplanetary traffic lines
/// when toggled via the UI.
/// </summary>
public class ArcadeModeManager : MonoBehaviour
{
    [Header("Arcade Assets")]
    public GameObject[] spacecraftPrefabs;
    
    [Header("UI Reference")]
    public TextMeshProUGUI toggleButtonText;

    [Header("Settings")]
    public float globalTrafficDensity = 1.0f;
    public string dioramaLayerName = "Default";

    private bool isArcadeActive = false;
    private List<GameObject> activeSpacecraft = new List<GameObject>();
    public int ActiveShipCount => activeSpacecraft.Count;

    private void Start()
    {
        if (toggleButtonText != null)
        {
            toggleButtonText.text = "Arcade\nDISABLED";
        }
    }

    /// <summary>
    /// Toggles the arcade mode on or off, updating the UI and generating or clearing interplanetary traffic accordingly.
    /// </summary>
    public void ToggleArcadeMode()
    {
        isArcadeActive = !isArcadeActive;

        if (toggleButtonText != null)
        {
            toggleButtonText.text = isArcadeActive ? "Arcade\nENABLED" : "Arcade\nDISABLED";
        }

        if (isArcadeActive)
        {
            GenerateTraffic();
        }
        else
        {
            ClearTraffic();
        }
    }

    /// <summary>
    /// Generates interplanetary traffic by instantiating spacecraft between random celestial bodies in the scene.
    /// </summary>
    private void GenerateTraffic()
    {
        if (spacecraftPrefabs == null || spacecraftPrefabs.Length == 0) return;

        CelestialBody[] allBodies = FindObjectsByType<CelestialBody>(FindObjectsSortMode.None);
        List<CelestialBody> validDestinations = new List<CelestialBody>();

        foreach (CelestialBody body in allBodies)
        {
            if (!body.name.Contains("Prime")) validDestinations.Add(body);
        }

        if (validDestinations.Count < 2) return;

        System.Random arcadePrng = new System.Random(validDestinations.Count * 42);
        int totalShips = Mathf.RoundToInt(validDestinations.Count * 2.5f * globalTrafficDensity);

        for (int i = 0; i < totalShips; i++)
        {
            CelestialBody startBody = validDestinations[arcadePrng.Next(validDestinations.Count)];
            CelestialBody targetBody = validDestinations[arcadePrng.Next(validDestinations.Count)];
            
            while (targetBody == startBody) 
            {
                targetBody = validDestinations[arcadePrng.Next(validDestinations.Count)];
            }

            int prefabIndex = arcadePrng.Next(0, spacecraftPrefabs.Length);
            GameObject shipObj = Instantiate(spacecraftPrefabs[prefabIndex]);
            shipObj.name = $"Spaceship_{startBody.name}_to_{targetBody.name}";
            
            SetLayerRecursively(shipObj, LayerMask.NameToLayer(dioramaLayerName));

            // The spaceship's scale is set to half the minimum diameter of the start and target celestial bodies
            float minDiameter = Mathf.Min(startBody.transform.localScale.x, targetBody.transform.localScale.x);
            shipObj.transform.localScale = Vector3.one * (minDiameter * 0.5f);

            float travelSpeed = ((float)arcadePrng.NextDouble() * 0.05f) + 0.02f;
            float timeOffset = (float)arcadePrng.NextDouble() * 100f;
            float arcHeight = ((float)arcadePrng.NextDouble() * 400f + 100f) * (arcadePrng.NextDouble() > 0.5 ? 1f : -1f);

            SpacecraftKinematics kinematics = shipObj.AddComponent<SpacecraftKinematics>();
            kinematics.InitializeInterplanetaryPath(
                startBody.transform, 
                targetBody.transform, 
                travelSpeed, 
                timeOffset, 
                arcHeight
            );

            activeSpacecraft.Add(shipObj);
        }
    }

    /// <summary>
    /// Clears all active interplanetary traffic by destroying instantiated spacecraft and resetting the active list.
    /// </summary>
    private void ClearTraffic()
    {
        foreach (GameObject ship in activeSpacecraft)
        {
            if (ship != null) Destroy(ship);
        }
        activeSpacecraft.Clear();
    }

    /// <summary>
    /// Sets the layer of the given GameObject and all its children recursively to the specified new layer.
    /// </summary>
    /// <param name="obj">The GameObject to set the layer for.</param>
    /// <param name="newLayer">The new layer to assign.</param>
    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (newLayer == -1 || obj == null) return;
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }
}