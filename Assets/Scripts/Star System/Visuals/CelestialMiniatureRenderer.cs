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
    public CelestialBodyData CurrentPlanetData { get; private set; }

    private Camera renderCamera;
    private RenderTexture renderTexture;
    private GameObject currentMiniatureBody;
    private GameObject currentMiniatureRings;
    private Material atlasInstancedMaterial;
    
    private StarData currentStarData;
    private float visualRotationSpeed = 20f;

    private void Awake()
    {
        InitializeRenderStudio();
    }

    /// <summary>
    /// Initializes the off-screen rendering setup, including the camera, render texture, and lighting for the miniature display.
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
    /// Toggles between 3D miniature view and 2D atlas view.
    /// </summary>
    public void ToggleViewMode()
    {
        IsAtlasMode = !IsAtlasMode;
        RefreshDisplayMode();
    }

    /// <summary>
    /// Resets the display to 3D miniature mode, disabling atlas mode and re-enabling the render camera.
    /// </summary>
    public void ResetTo3DMode()
    {
        IsAtlasMode = false;
        RefreshDisplayMode();
    }

    /// <summary>
    /// Refreshes the display mode based on the current settings, updating the material and texture of the RawImage accordingly.
    /// </summary>
    private void RefreshDisplayMode()
    {
        if (miniatureDisplay == null) return;

        if (IsAtlasMode && CurrentPlanetData != null)
        {
            if (renderCamera != null) renderCamera.enabled = false;
            
            if (atlasInstancedMaterial == null && atlasBaseMaterial != null)
            {
                atlasInstancedMaterial = new Material(atlasBaseMaterial);
            }

            if (atlasInstancedMaterial != null)
            {
                CelestialMaterialHelper.ApplySurfaceProperties(CurrentPlanetData, atlasInstancedMaterial);
                
                miniatureDisplay.texture = null;
                miniatureDisplay.material = atlasInstancedMaterial;
            }
        }
        else
        {
            if (renderCamera != null) renderCamera.enabled = true;
            miniatureDisplay.material = null;
            miniatureDisplay.texture = renderTexture;
        }
    }

    /// <summary>
    /// Builds a 3D miniature of the specified celestial body, applying procedural properties, rings, and atmosphere as needed.
    /// </summary>
    /// <param name="bodyData">The data defining the celestial body, including surface, ring, and atmosphere parameters.</param>
    public void BuildMiniature(CelestialBodyData bodyData)
    {
        if (bodyData == null || celestialPrefab == null) return;
        
        CurrentPlanetData = bodyData;
        currentStarData = null;

        SetupBaseMiniatureObject(bodyData.name, bodyData.axialTilt);
        int layerIndex = LayerMask.NameToLayer(miniatureLayerName);

        Renderer mr = currentMiniatureBody.GetComponent<Renderer>();
        if (mr != null)
        {
            if (planetMaterial != null) mr.sharedMaterial = planetMaterial;

            MaterialPropertyBlock props = new MaterialPropertyBlock();
            mr.GetPropertyBlock(props); 
            CelestialMaterialHelper.ApplySurfaceProperties(bodyData, props);
            mr.SetPropertyBlock(props);
        }

        currentMiniatureRings = CelestialVisualUtility.BuildRingSystem(currentMiniatureBody, bodyData, ringMaterial, layerIndex);

        float scaleAdjustment = bodyData.hasRings ? 0.6f : 1.0f;
        currentMiniatureBody.transform.localScale = Vector3.one * scaleAdjustment;

        CelestialVisualUtility.BuildAtmosphere(currentMiniatureBody, bodyData, celestialPrefab, atmosphereMaterial, layerIndex);
        
        RefreshDisplayMode();
    }

    /// <summary>
    /// Builds a 3D miniature of the specified star, applying procedural properties and emission settings.
    /// </summary>
    /// <param name="starData">The data defining the star, including color and granulation parameters.</param>
    public void BuildMiniature(StarData starData)
    {
        if (starData == null || celestialPrefab == null) return;
        
        currentStarData = starData;
        CurrentPlanetData = null;
        IsAtlasMode = false; 

        SetupBaseMiniatureObject(starData.name, starData.axialTilt);

        Renderer mr = currentMiniatureBody.GetComponent<Renderer>();
        if (mr != null)
        {
            if (starMaterial != null) mr.sharedMaterial = starMaterial;
            CelestialVisualUtility.ApplyStarProperties(starData, mr);
        }
        
        currentMiniatureBody.transform.localScale = Vector3.one * 0.8f;
        RefreshDisplayMode();
    }

    /// <summary>
    /// Sets up the base miniature object by instantiating the celestial prefab, positioning it, and applying the specified axial tilt. Cleans up any previous miniature objects.
    /// </summary>
    /// <param name="objectName">The name to assign to the miniature object.</param>
    /// <param name="axialTilt">The axial tilt to apply to the miniature object.</param>
    private void SetupBaseMiniatureObject(string objectName, float axialTilt)
    {
        if (currentMiniatureBody != null) Destroy(currentMiniatureBody);
        if (currentMiniatureRings != null) Destroy(currentMiniatureRings);

        currentMiniatureBody = Instantiate(celestialPrefab, isolatedPosition, Quaternion.Euler(axialTilt, 0f, 0f));
        currentMiniatureBody.name = $"Miniature_{objectName}";
        CelestialVisualUtility.SetLayerRecursively(currentMiniatureBody, LayerMask.NameToLayer(miniatureLayerName));

        CelestialBody orbitScript = currentMiniatureBody.GetComponent<CelestialBody>();
        if (orbitScript != null) Destroy(orbitScript);
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