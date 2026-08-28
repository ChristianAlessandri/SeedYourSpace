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

    [Header("Diorama Scale Factors")]
    public float starSizeMultiplier = 2.0f;
    public float planetSizeMultiplier = 0.5f;
    public float planetDistanceMultiplier = 15.0f; 
    public float moonDistanceMultiplier = 2.0f;

    /// <summary>
    /// Instantiates the entire star system hierarchy.
    /// </summary>
    public void BuildUniverse(StarData starData, List<PlanetData> planets)
    {
        // 1. Generate the Central Star
        GameObject starObj = Instantiate(celestialPrefab, Vector3.zero, Quaternion.identity);
        starObj.name = starData.name;
        
        // Scale the star and remove the kinematic script since it doesn't orbit anything
        starObj.transform.localScale = Vector3.one * (starData.radius * starSizeMultiplier);
        Destroy(starObj.GetComponent<CelestialBody>());

        // 2. Generate Planets
        foreach (PlanetData planet in planets)
        {
            GameObject planetObj = Instantiate(celestialPrefab, Vector3.zero, Quaternion.identity);
            planetObj.name = planet.name;
            
            planetObj.transform.localScale = Vector3.one * (planet.radius * planetSizeMultiplier);
            
            // Apply Axial Tilt (rotating the mesh physically on the X/Z axis)
            planetObj.transform.rotation = Quaternion.Euler(planet.axialTilt, 0f, planet.orbitalInclination);

            // Hook up the kinematic engine
            CelestialBody planetOrbit = planetObj.GetComponent<CelestialBody>();
            planetOrbit.InitializeOrbit(planet.orbitalDistance * planetDistanceMultiplier, planet.orbitalEccentricity, starObj.transform);

            // 3. Generate Moons
            foreach (MoonData moon in planet.moons)
            {
                GameObject moonObj = Instantiate(celestialPrefab, Vector3.zero, Quaternion.identity);
                moonObj.name = moon.name;
                
                moonObj.transform.localScale = Vector3.one * (moon.radius * planetSizeMultiplier);
                moonObj.transform.rotation = Quaternion.Euler(moon.axialTilt, 0f, moon.orbitalInclination);

                // Moons use the planet as their centralStar transform[cite: 21]
                CelestialBody moonOrbit = moonObj.GetComponent<CelestialBody>();
                moonOrbit.InitializeOrbit(moon.orbitalDistance * moonDistanceMultiplier, moon.orbitalEccentricity, planetObj.transform);
            }
        }
    }
}