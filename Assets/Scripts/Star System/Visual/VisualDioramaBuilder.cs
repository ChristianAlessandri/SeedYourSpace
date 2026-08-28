using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Translates the generated data layer into physical 3D GameObjects.
/// Applies visual scaling to make the astronomical distances readable on screen.
/// </summary>
public class VisualDioramaBuilder : MonoBehaviour
{
    [Header("3D Assets")]
    [Tooltip("A 3D Sphere prefab with the CelestialBody script attached.")]
    public GameObject celestialPrefab; 

    [Header("Diorama Scale Multipliers")]
    [Tooltip("Adjust these values to balance the visual representation in the scene.")]
    [SerializeField] private float starSizeMultiplier = 100.0f; 
    [SerializeField] private float planetSizeMultiplier = 1.0f; 
    [SerializeField] private float planetDistanceMultiplier = 200.0f; 
    [SerializeField] private float moonDistanceMultiplier = 0.15f; 

    /// <summary>
    /// Instantiates the entire star system hierarchy.
    /// </summary>
    /// <param name="starData">The data for the central star.</param>
    /// <param name="planets">The list of planet data to instantiate.</param>
    public void BuildUniverse(StarData starData, List<PlanetData> planets)
    {
        // Generate the Central Star
        GameObject starObj = Instantiate(celestialPrefab, Vector3.zero, Quaternion.identity);
        starObj.name = starData.name;
        starObj.transform.localScale = Vector3.one * (starData.radius * starSizeMultiplier);
        
        // Set the star's axial tilt
        starObj.transform.rotation = Quaternion.Euler(starData.axialTilt, 0f, 0f);
        
        CelestialBody starOrbit = starObj.GetComponent<CelestialBody>();
        starOrbit.InitializeKinematics(0f, 0f, 0f, null, 0f, starData.rotationPeriod);

        // Apply procedural visual data to the star's material
        Renderer starRenderer = starObj.GetComponent<Renderer>();
        if (starRenderer != null)
        {
            MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
            
            // Retrieve current properties (if any) to avoid overwriting unrelated data
            starRenderer.GetPropertyBlock(propBlock);
            
            // Pass the calculated data to the shader
            // Multiply the base color by an intensity factor to make it glow (Emission)
            propBlock.SetColor("_BaseColor", starData.baseColor);
            propBlock.SetColor("_EmissionColor", starData.baseColor * 2.5f); 
            propBlock.SetFloat("_GranulationScale", starData.granulationScale);
            propBlock.SetFloat("_MagneticActivity", starData.magneticActivity);
            
            starRenderer.SetPropertyBlock(propBlock);
        }

        // Generate Planets
        foreach (PlanetData planet in planets)
        {
            GameObject planetObj = Instantiate(celestialPrefab, Vector3.zero, Quaternion.identity);
            planetObj.name = planet.name;
            planetObj.transform.localScale = Vector3.one * (planet.radius * planetSizeMultiplier);
            
            planetObj.transform.rotation = Quaternion.Euler(planet.axialTilt, 0f, 0f);

            CelestialBody planetOrbit = planetObj.GetComponent<CelestialBody>();
            planetOrbit.InitializeKinematics(planet.orbitalDistance * planetDistanceMultiplier, planet.orbitalEccentricity, planet.orbitalInclination, starObj.transform, planet.revolutionPeriod, planet.rotationPeriod);

            // Generate Moons
            foreach (MoonData moon in planet.moons)
            {
                GameObject moonObj = Instantiate(celestialPrefab, Vector3.zero, Quaternion.identity);
                moonObj.name = moon.name;
                moonObj.transform.localScale = Vector3.one * (moon.radius * planetSizeMultiplier);
                
                moonObj.transform.rotation = Quaternion.Euler(moon.axialTilt, 0f, 0f);

                CelestialBody moonOrbit = moonObj.GetComponent<CelestialBody>();
                moonOrbit.InitializeKinematics(moon.orbitalDistance * moonDistanceMultiplier, moon.orbitalEccentricity, moon.orbitalInclination, planetObj.transform, moon.revolutionPeriod, moon.rotationPeriod);
            }
        }
    }
}