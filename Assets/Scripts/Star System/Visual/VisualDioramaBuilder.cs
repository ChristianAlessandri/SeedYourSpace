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
            if (starMaterial != null) starRenderer.sharedMaterial = starMaterial;

            MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
            
            // Retrieve current properties (if any) to avoid overwriting unrelated data
            starRenderer.GetPropertyBlock(propBlock);
            
            // Pass the calculated data to the shader
            propBlock.SetColor("_BaseColor", starData.baseColor);
            propBlock.SetColor("_EmissionColor", starData.baseColor * 2.5f); 
            propBlock.SetFloat("_GranulationScale", starData.granulationScale);
            propBlock.SetFloat("_MagneticActivity", starData.magneticActivity);
            
            starRenderer.SetPropertyBlock(propBlock);
        }

        // Attach and configure a point light for the central star
        Light starLight = starObj.AddComponent<Light>();
        starLight.type = LightType.Point;
        starLight.color = starData.baseColor; // Match the physical blackbody color
        
        // Extend the range to ensure it illuminates planets at the edge of the system
        starLight.range = 1000000f; 
        
        // Luminosity approximation scaled for the diorama.
        starLight.intensity = 500000f * starData.mass; 
        starLight.shadows = LightShadows.Soft;

        // Generate Planets
        foreach (PlanetData planet in planets)
        {
            GameObject planetObj = Instantiate(celestialPrefab, Vector3.zero, Quaternion.identity);
            planetObj.name = planet.name;
            planetObj.transform.localScale = Vector3.one * (planet.radius * planetSizeMultiplier);
            
            planetObj.transform.rotation = Quaternion.Euler(planet.axialTilt, 0f, 0f);

            CelestialBody planetOrbit = planetObj.GetComponent<CelestialBody>();
            planetOrbit.InitializeKinematics(planet.orbitalDistance * planetDistanceMultiplier, planet.orbitalEccentricity, planet.orbitalInclination, starObj.transform, planet.revolutionPeriod, planet.rotationPeriod);

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

            // Generate Moons
            foreach (MoonData moon in planet.moons)
            {
                GameObject moonObj = Instantiate(celestialPrefab, Vector3.zero, Quaternion.identity);
                moonObj.name = moon.name;
                moonObj.transform.localScale = Vector3.one * (moon.radius * planetSizeMultiplier);
                
                moonObj.transform.rotation = Quaternion.Euler(moon.axialTilt, 0f, 0f);

                CelestialBody moonOrbit = moonObj.GetComponent<CelestialBody>();
                moonOrbit.InitializeKinematics(moon.orbitalDistance * moonDistanceMultiplier, moon.orbitalEccentricity, moon.orbitalInclination, planetObj.transform, moon.revolutionPeriod, moon.rotationPeriod);
            
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
            }
        }
    }

    /// <summary>
    /// Procedurally generates a double-sided ring mesh around a celestial body.
    /// </summary>
    /// <param name="parentObj">The celestial body GameObject to which the rings will be attached.</param>
    /// <param name="bodyData">The data of the celestial body, including ring properties.</param>
    /// <summary>
    /// Procedurally generates a ring mesh around a celestial body.
    /// </summary>
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
        Vector3[] vertices = new Vector3[(segments + 1) * 2];
        int[] triangles = new int[segments * 6];
        Vector2[] uvs = new Vector2[(segments + 1) * 2];
        Vector3[] normals = new Vector3[(segments + 1) * 2];

        float angleStep = (Mathf.PI * 2f) / segments;

        for (int i = 0; i <= segments; i++)
        {
            float angle = i * angleStep;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            vertices[i * 2] = new Vector3(cos * localInner, 0f, sin * localInner);
            vertices[i * 2 + 1] = new Vector3(cos * localOuter, 0f, sin * localOuter);

            uvs[i * 2] = new Vector2(0f, (float)i / segments);
            uvs[i * 2 + 1] = new Vector2(1f, (float)i / segments);

            normals[i * 2] = Vector3.up;
            normals[i * 2 + 1] = Vector3.up;

            if (i < segments)
            {
                int ti = i * 6;
                int vi = i * 2;

                triangles[ti] = vi;
                triangles[ti + 1] = vi + 1;
                triangles[ti + 2] = vi + 2;
                
                triangles[ti + 3] = vi + 1;
                triangles[ti + 4] = vi + 3;
                triangles[ti + 5] = vi + 2;
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
    public void BuildSkybox(Color nebulaColor, float starDistance, float starVisibility)
    {
        if (baseSkyboxMaterial != null)
        {
            // Instantiate a copy to avoid permanently overwriting the project asset
            Material instancedSkybox = new Material(baseSkyboxMaterial);
            
            instancedSkybox.SetColor("_NebulaColor", nebulaColor);
            instancedSkybox.SetFloat("_StarDistance", starDistance);
            instancedSkybox.SetFloat("_StarVisibility", starVisibility);
            
            // Assign to the global scene environment
            RenderSettings.skybox = instancedSkybox;
            DynamicGI.UpdateEnvironment(); // Recompute ambient lighting based on the new skybox
        }
        else
        {
            Debug.LogWarning("Warning: Base Skybox Material is missing from the Diorama Builder.");
        }
    }
}