using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders a dynamic 3D miniature of the selected celestial body for the UI, 
/// or displays a 2D procedural atlas map.
/// </summary>
public class CelestialMiniatureRenderer : MonoBehaviour
{
    [Header("UI Reference")]
    public RawImage miniatureDisplay;

    [Header("3D Assets")]
    public GameObject celestialPrefab;

    [Header("Rendering Setup")]
    public string miniatureLayerName = "UI"; 
    public Vector3 isolatedPosition = new Vector3(0, 100000, 0);

    [Header("Materials Reference")]
    public Material starMaterial;
    public Material planetMaterial;
    public Material atmosphereMaterial;
    public Material ringMaterial;
    
    [Header("Atlas Mode")]
    [Tooltip("The 2D Unlit material using the AtlasShader.")]
    public Material atlasBaseMaterial;

    public bool IsAtlasMode { get; private set; } = false;

    private Camera renderCamera;
    private RenderTexture renderTexture;
    private GameObject currentMiniatureBody;
    private GameObject currentMiniatureRings;
    private Material atlasInstancedMaterial;
    
    private CelestialBodyData currentPlanetData;
    private StarData currentStarData;

    private float visualRotationSpeed = 20f;

    private void Awake()
    {
        InitializeRenderStudio();
    }

    /// <summary>
    /// Initializes the off-screen camera and render texture for rendering miniatures.
    /// </summary>
    private void InitializeRenderStudio()
    {
        renderTexture = new RenderTexture(512, 512, 16);
        renderTexture.Create();

        if (miniatureDisplay != null)
        {
            miniatureDisplay.texture = renderTexture;
        }

        GameObject camObj = new GameObject("Miniature_Camera");
        camObj.transform.position = isolatedPosition + new Vector3(0f, 1.2f, -3.2f); 
        camObj.transform.LookAt(isolatedPosition);
        
        renderCamera = camObj.AddComponent<Camera>();
        renderCamera.targetTexture = renderTexture;
        renderCamera.clearFlags = CameraClearFlags.SolidColor;
        renderCamera.backgroundColor = new Color(0, 0, 0, 0); 
        
        int layerIndex = LayerMask.NameToLayer(miniatureLayerName);
        if (layerIndex != -1)
        {
            renderCamera.cullingMask = 1 << layerIndex;
        }

        GameObject lightObj = new GameObject("Miniature_Light");
        lightObj.transform.SetParent(camObj.transform, false);
        lightObj.transform.localPosition = new Vector3(-2.5f, 2f, -1.5f); 
        
        Light miniLight = lightObj.AddComponent<Light>();
        miniLight.type = LightType.Point; 
        miniLight.range = 20f; 
        miniLight.intensity = 15f; 
        miniLight.cullingMask = renderCamera.cullingMask;
    }

    /// <summary>
    /// Toggles between the 3D Render Texture view and the 2D Procedural Atlas Material.
    /// </summary>
    public void ToggleViewMode()
    {
        IsAtlasMode = !IsAtlasMode;
        RefreshDisplayMode();
    }

    /// <summary>
    /// Forces the renderer back to 3D mode (useful when selecting a new planet).
    /// </summary>
    public void ResetTo3DMode()
    {
        IsAtlasMode = false;
        RefreshDisplayMode();
    }

    /// <summary>
    /// Updates the display based on the current mode (3D miniature or 2D atlas).
    /// </summary>
    private void RefreshDisplayMode()
    {
        if (miniatureDisplay == null) return;

        if (IsAtlasMode && currentPlanetData != null)
        {
            // 2D Atlas Mode: Turn off 3D camera to save performance, apply UI material
            if (renderCamera != null) renderCamera.enabled = false;
            
            if (atlasInstancedMaterial == null && atlasBaseMaterial != null)
            {
                atlasInstancedMaterial = new Material(atlasBaseMaterial);
            }

            if (atlasInstancedMaterial != null)
            {
                atlasInstancedMaterial.SetColor("_BaseColor", currentPlanetData.baseColor);
                atlasInstancedMaterial.SetColor("_SecondaryColor", currentPlanetData.secondaryColor);
                atlasInstancedMaterial.SetFloat("_Hydrofraction", currentPlanetData.hydrofraction);
                
                float seedOffset = (currentPlanetData.name.GetHashCode() % 1000) / 10f;
                atlasInstancedMaterial.SetVector("_Offset", new Vector4(seedOffset, seedOffset * 2.5f, seedOffset * -1.3f, 0f));
                
                miniatureDisplay.texture = null;
                miniatureDisplay.material = atlasInstancedMaterial;
            }
        }
        else
        {
            // 3D Miniature Mode: Turn camera back on, restore Render Texture
            if (renderCamera != null) renderCamera.enabled = true;
            miniatureDisplay.material = null;
            miniatureDisplay.texture = renderTexture;
        }
    }

    /// <summary>
    /// Builds a miniature of the given celestial body and renders it to the UI.
    /// </summary>
    /// <param name="bodyData">The data for the celestial body to render.</param>
    public void BuildMiniature(CelestialBodyData bodyData)
    {
        if (bodyData == null || celestialPrefab == null) return;
        
        currentPlanetData = bodyData;
        currentStarData = null;

        SetupBaseMiniatureObject(bodyData.name, bodyData.axialTilt);

        Renderer mr = currentMiniatureBody.GetComponent<Renderer>();
        if (mr != null)
        {
            if (planetMaterial != null) mr.sharedMaterial = planetMaterial;

            MaterialPropertyBlock props = new MaterialPropertyBlock();
            mr.GetPropertyBlock(props); 
            props.SetColor("_BaseColor", bodyData.baseColor);
            props.SetColor("_SecondaryColor", bodyData.secondaryColor);
            props.SetFloat("_Hydrofraction", bodyData.hydrofraction);
            props.SetFloat("_CloudCoverage", bodyData.cloudCoverage);
            mr.SetPropertyBlock(props);
        }

        if (bodyData.hasRings) BuildMiniatureRings(currentMiniatureBody, bodyData, LayerMask.NameToLayer(miniatureLayerName));

        float scaleAdjustment = bodyData.hasRings ? 0.6f : 1.0f;
        currentMiniatureBody.transform.localScale = Vector3.one * scaleAdjustment;

        BuildAtmosphere(currentMiniatureBody, bodyData, LayerMask.NameToLayer(miniatureLayerName));
        
        RefreshDisplayMode();
    }

    /// <summary>
    /// Renders a dynamic 3D miniature specifically for the Central Star.
    /// </summary>
    /// <param name="starData">The data for the central star.</param>
    public void BuildMiniature(StarData starData)
    {
        if (starData == null || celestialPrefab == null) return;
        
        currentStarData = starData;
        currentPlanetData = null;
        IsAtlasMode = false; // Stars cannot be viewed in Atlas mode

        SetupBaseMiniatureObject(starData.name, starData.axialTilt);

        Renderer mr = currentMiniatureBody.GetComponent<Renderer>();
        if (mr != null)
        {
            if (starMaterial != null) mr.sharedMaterial = starMaterial;

            MaterialPropertyBlock props = new MaterialPropertyBlock();
            mr.GetPropertyBlock(props); 
            props.SetColor("_BaseColor", starData.baseColor);
            props.SetColor("_EmissionColor", starData.baseColor * 2.5f); 
            props.SetFloat("_GranulationScale", starData.granulationScale);
            props.SetFloat("_MagneticActivity", starData.magneticActivity);
            mr.SetPropertyBlock(props);
        }
        
        currentMiniatureBody.transform.localScale = Vector3.one * 0.8f;
        RefreshDisplayMode();
    }

    /// <summary>
    /// Handles the common instantiation, layer setting, and cleanup for all miniatures.
    /// </summary>
    /// <param name="objectName">The name of the celestial body.</param>
    /// <param name="axialTilt">The axial tilt of the celestial body.</param>
    private void SetupBaseMiniatureObject(string objectName, float axialTilt)
    {
        if (currentMiniatureBody != null) Destroy(currentMiniatureBody);
        if (currentMiniatureRings != null) Destroy(currentMiniatureRings);

        currentMiniatureBody = Instantiate(celestialPrefab, isolatedPosition, Quaternion.Euler(axialTilt, 0f, 0f));
        currentMiniatureBody.name = $"Miniature_{objectName}";
        SetLayerRecursively(currentMiniatureBody, LayerMask.NameToLayer(miniatureLayerName));

        CelestialBody orbitScript = currentMiniatureBody.GetComponent<CelestialBody>();
        if (orbitScript != null) Destroy(orbitScript);
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
    /// Builds a simple ring mesh for the miniature if the celestial body has rings.
    /// </summary>
    /// <param name="parentObj">The parent GameObject to which the rings will be attached.</param>
    /// <param name="bodyData">The data for the celestial body, including ring parameters
    /// if applicable.</param>
    /// <param name="layerIndex">The layer index to assign to the rings for proper rendering.</param>
    private void BuildMiniatureRings(GameObject parentObj, CelestialBodyData bodyData, int layerIndex)
    {
        currentMiniatureRings = new GameObject("Miniature_Rings");
        currentMiniatureRings.transform.SetParent(parentObj.transform, false);
        SetLayerRecursively(currentMiniatureRings, layerIndex);

        float localInner = bodyData.ringInnerRadius / bodyData.radius;
        float localOuter = bodyData.ringOuterRadius / bodyData.radius;

        MeshFilter mf = currentMiniatureRings.AddComponent<MeshFilter>();
        MeshRenderer mr = currentMiniatureRings.AddComponent<MeshRenderer>();
        
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
    /// Helper method to ensure the prefab and all its contents are properly isolated from the main camera.
    /// </summary>
    /// <param name="obj">The GameObject to set the layer for.</param>
    /// <param name="newLayer">The layer index to assign.</param>
    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (newLayer == -1) return;
        
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }

    private void Update()
    {
        if (currentMiniatureBody != null && !IsAtlasMode)
        {
            currentMiniatureBody.transform.Rotate(Vector3.up, visualRotationSpeed * Time.unscaledDeltaTime, Space.Self);
        }
    }

    private void OnDestroy()
    {
        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }
    }
}