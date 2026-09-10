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
    public Material baseSkyboxMaterial;

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
        Transform starTransform = BuildCentralStar(starData);

        foreach (PlanetData planet in planets)
        {
            Transform planetTransform = BuildPlanet(planet, starTransform);

            foreach (MoonData moon in planet.moons)
            {
                BuildMoon(moon, planetTransform);
            }
        }
    }

    /// <summary>
    /// Instantiates the central star of the system.
    /// </summary>
    /// <param name="starData">The data for the central star.</param>
    /// <returns>The Transform of the instantiated star.</returns>
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

            MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
            starRenderer.GetPropertyBlock(propBlock);
            propBlock.SetColor("_BaseColor", starData.baseColor);
            propBlock.SetColor("_EmissionColor", starData.baseColor * 2.5f); 
            propBlock.SetFloat("_GranulationScale", starData.granulationScale);
            propBlock.SetFloat("_MagneticActivity", starData.magneticActivity);
            starRenderer.SetPropertyBlock(propBlock);
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
    /// Instantiates a planet within the system.
    /// </summary>
    /// <param name="planet">The data for the planet.</param>
    /// <param name="starTransform">The transform of the central star.</param>
    /// <returns>The Transform of the instantiated planet.</returns>
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
            planetProps.SetColor("_BaseColor", planet.baseColor);
            planetProps.SetColor("_SecondaryColor", planet.secondaryColor);
            planetProps.SetFloat("_Hydrofraction", planet.hydrofraction);
            planetProps.SetFloat("_CloudCoverage", planet.cloudCoverage);
            planetRenderer.SetPropertyBlock(planetProps);
            
            BuildRingSystem(planetObj, planet);
        }

        BuildAtmosphere(planetObj, planet);
        return planetObj.transform;
    }

    /// <summary>
    /// Instantiates a moon around a planet.
    /// </summary>
    /// <param name="moon">The data for the moon.</param>
    /// <param name="planetTransform">The transform of the parent planet.</param>
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
            moonProps.SetColor("_BaseColor", moon.baseColor);
            moonProps.SetColor("_SecondaryColor", moon.secondaryColor);
            moonProps.SetFloat("_Hydrofraction", moon.hydrofraction);
            moonProps.SetFloat("_CloudCoverage", moon.cloudCoverage);
            moonRenderer.SetPropertyBlock(moonProps);
            
            BuildRingSystem(moonObj, moon);
        }

        BuildAtmosphere(moonObj, moon);
    }

    /// <summary>
    /// Instantiates a scaled-up transparent sphere to represent the atmosphere and clouds.
    /// </summary>
    /// <param name="parentObj">The parent GameObject (the planet) to which the atmosphere will be attached.</param>
    /// <param name="bodyData">The data for the celestial body, including atmosphere parameters.</param>
    /// <param name="layerIndex">The layer index to assign to the atmosphere for proper rendering.</param>
    private void BuildAtmosphere(GameObject parentObj, CelestialBodyData bodyData, int layerIndex = -1)
    {
        // If the celestial body has no atmosphere, we skip this step.
        if (bodyData.atmosphereType.Contains("Vacuum") || bodyData.atmosphereType.Contains("None")) return;

        // Instance the atmosphere prefab as a child of the planet, slightly larger to simulate the atmospheric layer.
        GameObject atmosObj = Instantiate(celestialPrefab, parentObj.transform.position, parentObj.transform.rotation);
        atmosObj.name = "Procedural_Atmosphere";
        atmosObj.transform.SetParent(parentObj.transform, true);
        
        // Use the dynamically calculated procedural scale
        atmosObj.transform.localScale = Vector3.one * bodyData.atmosphereScale; 

        if (layerIndex != -1) atmosObj.layer = layerIndex;

        CelestialBody orbitScript = atmosObj.GetComponent<CelestialBody>();
        if (orbitScript != null) Destroy(orbitScript);

        Collider atmosCollider = atmosObj.GetComponent<Collider>();
        if (atmosCollider != null) Destroy(atmosCollider);

        Renderer mr = atmosObj.GetComponent<Renderer>();
        if (mr != null && atmosphereMaterial != null)
        {
            mr.sharedMaterial = atmosphereMaterial;

            MaterialPropertyBlock props = new MaterialPropertyBlock();
            
            // Apply procedural coverage and chemical colors
            props.SetFloat("_CloudCoverage", bodyData.cloudCoverage);
            props.SetColor("_BaseColor", bodyData.atmosphereColor);
            props.SetColor("_CloudColor", bodyData.cloudColor);

            System.Random atmosPrng = new System.Random(bodyData.name.GetHashCode());
            
            float speedX = (float)(atmosPrng.NextDouble() * 0.04 + 0.01) * (atmosPrng.NextDouble() > 0.5 ? 1f : -1f);
            float speedY = (float)(atmosPrng.NextDouble() * 0.015 + 0.001) * (atmosPrng.NextDouble() > 0.5 ? 1f : -1f);
            float speedZ = (float)(atmosPrng.NextDouble() * 0.015 + 0.001) * (atmosPrng.NextDouble() > 0.5 ? 1f : -1f);

            props.SetFloat("_CloudSpeedX", speedX);
            props.SetFloat("_CloudSpeedY", speedY);
            props.SetFloat("_CloudSpeedZ", speedZ);

            mr.SetPropertyBlock(props);
        }
    }

    /// <summary>
    /// Procedurally generates a double-sided ring mesh around a celestial body.
    /// </summary>
    /// <param name="parentObj">The parent GameObject to which the rings will be attached.</param>
    /// <param name="bodyData">The data for the celestial body, including ring parameters if applicable.</param>
    private void BuildRingSystem(GameObject parentObj, CelestialBodyData bodyData)
    {
        if (!bodyData.hasRings) return;

        GameObject ringObj = new GameObject("Procedural_Rings");
        ringObj.transform.SetParent(parentObj.transform, false);

        float localInner = bodyData.ringInnerRadius / bodyData.radius;
        float localOuter = bodyData.ringOuterRadius / bodyData.radius;

        MeshFilter mf = ringObj.AddComponent<MeshFilter>();
        MeshRenderer mr = ringObj.AddComponent<MeshRenderer>();
        
        int segments = 64;
        int divisions = Mathf.Max(1, bodyData.ringDivisions); 

        float totalThickness = localOuter - localInner;
        float gapRatio = 0.3f; 
        float ringWidth = (divisions == 1) ? totalThickness : totalThickness / (divisions + (divisions - 1) * gapRatio);
        float gapWidth = ringWidth * gapRatio;

        Vector3[] vertices = new Vector3[(segments + 1) * 2 * divisions];
        int[] triangles = new int[segments * 6 * divisions];
        Vector2[] uvs = new Vector2[(segments + 1) * 2 * divisions];
        Vector3[] normals = new Vector3[(segments + 1) * 2 * divisions];

        float angleStep = (Mathf.PI * 2f) / segments;
        int vIndex = 0;
        int tIndex = 0;

        for (int d = 0; d < divisions; d++)
        {
            float currentInner = localInner + (d * (ringWidth + gapWidth));
            float currentOuter = currentInner + ringWidth;

            for (int i = 0; i <= segments; i++)
            {
                float angle = i * angleStep;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                vertices[vIndex] = new Vector3(cos * currentInner, 0f, sin * currentInner);
                vertices[vIndex + 1] = new Vector3(cos * currentOuter, 0f, sin * currentOuter);

                uvs[vIndex] = new Vector2(0f, (float)i / segments);
                uvs[vIndex + 1] = new Vector2(1f, (float)i / segments);

                normals[vIndex] = Vector3.up;
                normals[vIndex + 1] = Vector3.up;

                if (i < segments)
                {
                    triangles[tIndex] = vIndex;
                    triangles[tIndex + 1] = vIndex + 1;
                    triangles[tIndex + 2] = vIndex + 2;
                    
                    triangles[tIndex + 3] = vIndex + 1;
                    triangles[tIndex + 4] = vIndex + 3;
                    triangles[tIndex + 5] = vIndex + 2;
                    tIndex += 6;
                }
                vIndex += 2;
            }
        }

        Mesh ringMesh = new Mesh();
        ringMesh.name = "Procedural_Ring_Mesh";
        ringMesh.vertices = vertices;
        ringMesh.triangles = triangles;
        ringMesh.uv = uvs;
        ringMesh.normals = normals;
        mf.mesh = ringMesh;

        if (ringMaterial != null)
        {
            mr.sharedMaterial = ringMaterial;
            MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
            propBlock.SetColor("_BaseColor", bodyData.ringColor);
            mr.SetPropertyBlock(propBlock);
        }
    }

    /// <summary>
    /// Instantiates a dynamic skybox material and applies the procedurally generated parameters.
    /// </summary>
    /// <param name="nebulaColor">The color of the nebula in the skybox.</param>
    /// <param name="starDistance">The distance of stars in the skybox.</param>
    /// <param name="starVisibility">The visibility factor of stars in the skybox.</param>
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