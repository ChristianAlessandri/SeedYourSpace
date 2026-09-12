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

    public Material starMaterial;
    public Material planetMaterial;
    public Material atmosphereMaterial;
    public Material ringMaterial;
    public GameObject asteroidPrefab;
    public Material baseSkyboxMaterial;

    [Header("Diorama Scale Multipliers")]
    [Tooltip("Adjust these values to balance the visual representation in the scene.")]
    [SerializeField] private float starSizeMultiplier = 100.0f; 
    [SerializeField] private float planetSizeMultiplier = 1.0f; 
    [SerializeField] private float planetDistanceMultiplier = 200.0f; 
    [SerializeField] private float moonDistanceMultiplier = 0.15f; 

    /// <summary>
    /// Builds the entire star system diorama, including the central star, planets, moons, and asteroid belts.
    /// </summary>
    /// <param name="starData">The data defining the central star.</param>
    /// <param name="planets">The list of planets in the system.</param>
    /// <param name="belts">The list of asteroid belts in the system.</param>
    public void BuildUniverse(StarData starData, List<PlanetData> planets, List<AsteroidBeltData> belts)
    {
        Transform starTransform = BuildCentralStar(starData);

        foreach (PlanetData planet in planets)
        {
            Transform planetTransform = BuildPlanet(planet, starTransform);

            foreach (MoonData moon in planet.moons)
            {
                BuildMoon(moon, planetTransform);
            }
        }

        foreach (AsteroidBeltData beltData in belts)
        {
            GameObject beltObj = new GameObject($"ProceduralBelt_{beltData.name}");
            beltObj.transform.position = starTransform.position;
            beltObj.transform.SetParent(this.transform, true); 
            
            AsteroidBeltRenderer renderer = beltObj.AddComponent<AsteroidBeltRenderer>();
            renderer.InitializeBelt(beltData, planetDistanceMultiplier, asteroidPrefab);
        }
    }

    /// <summary>
    /// Builds the central star of the system, applying procedural properties and adding a light source.
    /// </summary>
    /// <param name="starData">The data defining the star.</param>
    /// <returns>The transform of the created star object.</returns>
    private Transform BuildCentralStar(StarData starData)
    {
        GameObject starObj = Instantiate(celestialPrefab, Vector3.zero, Quaternion.identity);
        starObj.name = starData.name;
        starObj.transform.localScale = Vector3.one * (starData.radius * starSizeMultiplier);
        starObj.transform.rotation = Quaternion.Euler(starData.axialTilt, 0f, 0f);
        
        CelestialBody starOrbit = starObj.GetComponent<CelestialBody>();
        starOrbit.InitializeKinematics(0f, 0f, 0f, null, 0f, starData.rotationPeriod);

        Renderer starRenderer = starObj.GetComponent<Renderer>();
        if (starRenderer != null)
        {
            if (starMaterial != null) starRenderer.sharedMaterial = starMaterial;
            CelestialVisualUtility.ApplyStarProperties(starData, starRenderer);
        }

        Light starLight = starObj.AddComponent<Light>();
        starLight.type = LightType.Point;
        starLight.color = starData.baseColor; 
        starLight.range = 1000000f; 
        starLight.intensity = 500000f * starData.mass; 
        starLight.shadows = LightShadows.Soft;

        return starObj.transform;
    }

    /// <summary>
    /// Builds a planet, applying procedural properties, and optionally adding rings and an atmosphere.
    /// </summary>
    /// <param name="planet">The data defining the planet.</param>
    /// <param name="starTransform">The transform of the central star to orbit around.</param>
    /// <returns>The transform of the created planet object.</returns>
    private Transform BuildPlanet(PlanetData planet, Transform starTransform)
    {
        GameObject planetObj = Instantiate(celestialPrefab, Vector3.zero, Quaternion.identity);
        planetObj.name = planet.name;
        planetObj.transform.localScale = Vector3.one * (planet.radius * planetSizeMultiplier);
        planetObj.transform.rotation = Quaternion.Euler(planet.axialTilt, 0f, 0f);

        CelestialBody planetOrbit = planetObj.GetComponent<CelestialBody>();
        planetOrbit.InitializeKinematics(planet.orbitalDistance * planetDistanceMultiplier, planet.orbitalEccentricity, planet.orbitalInclination, starTransform, planet.revolutionPeriod, planet.rotationPeriod);

        Renderer planetRenderer = planetObj.GetComponent<Renderer>();
        if (planetRenderer != null)
        {
            if (planetMaterial != null) planetRenderer.sharedMaterial = planetMaterial;

            MaterialPropertyBlock planetProps = new MaterialPropertyBlock();
            planetRenderer.GetPropertyBlock(planetProps);
            CelestialMaterialHelper.ApplySurfaceProperties(planet, planetProps);
            planetRenderer.SetPropertyBlock(planetProps);
            
            CelestialVisualUtility.BuildRingSystem(planetObj, planet, ringMaterial);
        }

        CelestialVisualUtility.BuildAtmosphere(planetObj, planet, celestialPrefab, atmosphereMaterial);
        return planetObj.transform;
    }

    /// <summary>
    /// Builds a moon, applying procedural properties, and optionally adding rings and an atmosphere.
    /// </summary>
    /// <param name="moon">The data defining the moon.</param>
    /// <param name="planetTransform">The transform of the parent planet to orbit around.</param>
    private void BuildMoon(MoonData moon, Transform planetTransform)
    {
        GameObject moonObj = Instantiate(celestialPrefab, Vector3.zero, Quaternion.identity);
        moonObj.name = moon.name;
        moonObj.transform.localScale = Vector3.one * (moon.radius * planetSizeMultiplier);
        moonObj.transform.rotation = Quaternion.Euler(moon.axialTilt, 0f, 0f);

        CelestialBody moonOrbit = moonObj.GetComponent<CelestialBody>();
        moonOrbit.InitializeKinematics(moon.orbitalDistance * moonDistanceMultiplier, moon.orbitalEccentricity, moon.orbitalInclination, planetTransform, moon.revolutionPeriod, moon.rotationPeriod);
    
        Renderer moonRenderer = moonObj.GetComponent<Renderer>();
        if (moonRenderer != null)
        {
            if (planetMaterial != null) moonRenderer.sharedMaterial = planetMaterial;

            MaterialPropertyBlock moonProps = new MaterialPropertyBlock();
            moonRenderer.GetPropertyBlock(moonProps);
            CelestialMaterialHelper.ApplySurfaceProperties(moon, moonProps);
            moonRenderer.SetPropertyBlock(moonProps);
            
            CelestialVisualUtility.BuildRingSystem(moonObj, moon, ringMaterial);
        }

        CelestialVisualUtility.BuildAtmosphere(moonObj, moon, celestialPrefab, atmosphereMaterial);
    }

    /// <summary>
    /// Builds a skybox for the diorama, applying nebula colors and star visibility settings.
    /// </summary>
    /// <param name="nebulaColor1">The primary color of the nebula.</param>
    /// <param name="nebulaColor2">The secondary color of the nebula.</param>
    /// <param name="starDistance">The distance at which stars are rendered.</param>
    /// <param name="starVisibility">The visibility of stars in the skybox.</param>
    public void BuildSkybox(Color nebulaColor1, Color nebulaColor2, float starDistance, float starVisibility)
    {
        if (baseSkyboxMaterial != null)
        {
            Material instancedSkybox = new Material(baseSkyboxMaterial);
            instancedSkybox.SetColor("_NebulaColor", nebulaColor1);
            instancedSkybox.SetColor("_NebulaColor2", nebulaColor2);
            instancedSkybox.SetFloat("_StarDistance", starDistance);
            instancedSkybox.SetFloat("_StarVisibility", starVisibility);
            
            RenderSettings.skybox = instancedSkybox;
            DynamicGI.UpdateEnvironment(); 
        }
        else
        {
            Debug.LogWarning("Warning: Base Skybox Material is missing from the Diorama Builder.");
        }
    }
}