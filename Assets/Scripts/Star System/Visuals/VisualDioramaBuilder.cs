using UnityEngine;
using System.Collections.Generic;

public class VisualDioramaBuilder : MonoBehaviour
{
    [Header("3D Assets")]
    [Tooltip("A 3D Sphere prefab with the CelestialBody script attached.")]
    public GameObject celestialPrefab; 

    [Header("Materials")]
    public Material starMaterial;
    public Material planetMaterial;
    public Material atmosphereMaterial;
    public Material ringMaterial;
    public GameObject asteroidPrefab;
    public Material baseSkyboxMaterial;

    [Header("Environment FX")]
    [Tooltip("Material for 3D Nebula Clouds (URP/Particles/Unlit, Transparent, Alpha or Additive)")]
    public Material volumetricNebulaMaterial;

    [Header("Diorama Scale Multipliers")]
    [Tooltip("Adjust these values to balance the visual representation in the scene.")]
    [SerializeField] private float starSizeMultiplier = 100.0f; 
    [SerializeField] private float planetSizeMultiplier = 1.0f; 
    [SerializeField] private float planetDistanceMultiplier = 200.0f; 
    [SerializeField] private float moonDistanceMultiplier = 0.15f; 

    // Caches for the currently instantiated objects to allow dynamic updates when multipliers change
    private GameObject currentStarObj;
    private StarData currentStarData;

    private class PlanetCache { public GameObject obj; public PlanetData data; public CelestialBody orbit; }
    private class MoonCache { public GameObject obj; public MoonData data; public CelestialBody orbit; }

    private List<PlanetCache> activePlanets = new List<PlanetCache>();
    private List<MoonCache> activeMoons = new List<MoonCache>();

    public void BuildUniverse(StarData starData, List<PlanetData> planets, List<AsteroidBeltData> belts)
    {
        // Clean up any previously instantiated objects before building a new diorama
        activePlanets.Clear();
        activeMoons.Clear();

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

    public void UpdateMultipliers(float newStarSize, float newPlanetSize, float newPlanetDist, float newMoonDist)
    {
        starSizeMultiplier = newStarSize;
        planetSizeMultiplier = newPlanetSize;
        planetDistanceMultiplier = newPlanetDist;
        moonDistanceMultiplier = newMoonDist;

        // Update the central star's scale if it exists
        if (currentStarObj != null && currentStarData != null)
        {
            currentStarObj.transform.localScale = Vector3.one * (currentStarData.radius * starSizeMultiplier);
        }

        // Update all planets (Scale and Orbital Distance)
        foreach (var p in activePlanets)
        {
            if (p.obj != null && p.orbit != null)
            {
                p.obj.transform.localScale = Vector3.one * (p.data.radius * planetSizeMultiplier);
                p.orbit.semiMajorAxis = p.data.orbitalDistance * planetDistanceMultiplier;
            }
        }

        // Update all moons (Scale and Orbital Distance)
        foreach (var m in activeMoons)
        {
            if (m.obj != null && m.orbit != null)
            {
                // Note: Planet size multiplier is used for moons as well to maintain relative scale
                m.obj.transform.localScale = Vector3.one * (m.data.radius * planetSizeMultiplier);
                m.orbit.semiMajorAxis = m.data.orbitalDistance * moonDistanceMultiplier;
            }
        }
    }

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

        // Save the current star data and object for future updates when multipliers change
        currentStarData = starData;
        currentStarObj = starObj;

        return starObj.transform;
    }

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

        // Save in Cache for future updates when multipliers change
        activePlanets.Add(new PlanetCache { obj = planetObj, data = planet, orbit = planetOrbit });

        return planetObj.transform;
    }

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

        // Save in Cache for future updates when multipliers change
        activeMoons.Add(new MoonCache { obj = moonObj, data = moon, orbit = moonOrbit });
    }

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