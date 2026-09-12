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

    private void Start()
    {
        if (toggleButtonText != null)
        {
            toggleButtonText.text = "DISABLED";
        }
    }

    public void ToggleArcadeMode()
    {
        isArcadeActive = !isArcadeActive;

        if (toggleButtonText != null)
        {
            toggleButtonText.text = isArcadeActive ? "ENABLED" : "DISABLED";
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

            float randomScale = (float)arcadePrng.NextDouble() + 0.5f;
            shipObj.transform.localScale = Vector3.one * randomScale;

            float travelSpeed = ((float)arcadePrng.NextDouble() * 0.05f) + 0.02f;
            float timeOffset = (float)arcadePrng.NextDouble() * 100f;
            float arcHeight = ((float)arcadePrng.NextDouble() * 400f + 100f) * (arcadePrng.NextDouble() > 0.5 ? 1f : -1f);

            // Calculate world-space surface radii based on object scale (assuming base sphere radius is 0.5)
            float startRadius = startBody.transform.localScale.x * 0.5f;
            float targetRadius = targetBody.transform.localScale.x * 0.5f;

            SpacecraftKinematics kinematics = shipObj.AddComponent<SpacecraftKinematics>();
            kinematics.InitializeInterplanetaryPath(
                startBody.transform, 
                targetBody.transform, 
                travelSpeed, 
                timeOffset, 
                arcHeight, 
                startRadius, 
                targetRadius
            );

            activeSpacecraft.Add(shipObj);
        }
    }

    private void ClearTraffic()
    {
        foreach (GameObject ship in activeSpacecraft)
        {
            if (ship != null) Destroy(ship);
        }
        activeSpacecraft.Clear();
    }

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