using UnityEngine;

/// <summary>
/// A centralized utility class for generating procedural celestial visuals.
/// Used by both the Diorama Builder and the Miniature Renderer to ensure DRY principles.
/// </summary>
public static class CelestialVisualUtility
{
    /// <summary>
    /// Applies procedural properties to a Star's material.
    /// </summary>
    /// <param name="starData">The data defining the star, including color and granulation parameters.</param>
    /// <param name="mr">The Renderer component of the star GameObject.</param>
    public static void ApplyStarProperties(StarData starData, Renderer mr)
    {
        if (mr == null || starData == null) return;

        MaterialPropertyBlock props = new MaterialPropertyBlock();
        mr.GetPropertyBlock(props); 
        props.SetColor("_BaseColor", starData.baseColor);
        props.SetColor("_EmissionColor", starData.baseColor * 2.5f); 
        props.SetFloat("_GranulationScale", starData.granulationScale);
        props.SetFloat("_MagneticActivity", starData.magneticActivity);
        mr.SetPropertyBlock(props);
    }

    /// <summary>
    /// Instantiates a scaled-up transparent sphere to represent the atmosphere, clouds, and auroras.
    /// </summary>
    /// <param name="parentObj">The celestial body GameObject to attach the atmosphere to.</param>
    /// <param name="bodyData">The data defining the celestial body, including atmosphere parameters.</param>
    /// <param name="prefab">The prefab to use for the atmosphere.</param>
    /// <param name="mat">The material to apply to the atmosphere.</param>
    /// <param name="layerIndex">Optional layer index to assign to the atmosphere.</param>
    public static void BuildAtmosphere(GameObject parentObj, CelestialBodyData bodyData, GameObject prefab, Material mat, int layerIndex = -1)
    {
        if (bodyData.atmosphereType.Contains("Vacuum") || bodyData.atmosphereType.Contains("None")) return;

        GameObject atmosObj = Object.Instantiate(prefab, parentObj.transform.position, parentObj.transform.rotation);
        atmosObj.name = "Procedural_Atmosphere";
        atmosObj.transform.SetParent(parentObj.transform, true);
        atmosObj.transform.localScale = Vector3.one * bodyData.atmosphereScale; 

        if (layerIndex != -1) atmosObj.layer = layerIndex;

        // Clean up unnecessary components from the base prefab
        CelestialBody orbitScript = atmosObj.GetComponent<CelestialBody>();
        if (orbitScript != null) Object.Destroy(orbitScript);

        Collider atmosCollider = atmosObj.GetComponent<Collider>();
        if (atmosCollider != null) Object.Destroy(atmosCollider);

        Renderer mr = atmosObj.GetComponent<Renderer>();
        if (mr != null && mat != null)
        {
            mr.sharedMaterial = mat;
            MaterialPropertyBlock props = new MaterialPropertyBlock();
            
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

            // Procedural Auroras Logic
            float magneticRoll = (float)atmosPrng.NextDouble();
            float magneticActivity = (magneticRoll > 0.4f) ? (float)atmosPrng.NextDouble() : 0f;

            Color baseAurora = Color.Lerp(
                new Color(0.0f, 1.0f, 0.4f, 1f), 
                new Color(0.0f, 0.8f, 1.0f, 1f), 
                (float)atmosPrng.NextDouble()
            );

            Color secondaryAurora = Color.Lerp(
                new Color(0.5f, 0.1f, 1.0f, 1f), 
                new Color(1.0f, 0.2f, 0.5f, 1f), 
                (float)atmosPrng.NextDouble()
            );

            props.SetColor("_AuroraColor", baseAurora);
            props.SetColor("_AuroraColor2", secondaryAurora);
            props.SetFloat("_MagneticActivity", magneticActivity);

            mr.SetPropertyBlock(props);
        }
    }

    /// <summary>
    /// Procedurally generates a double-sided ring mesh around a celestial body.
    /// </summary>
    /// <param name="parentObj">The celestial body GameObject to attach the rings to.</param>
    /// <param name="bodyData">The data defining the celestial body, including ring parameters.</param>
    /// <param name="mat">The material to apply to the rings.</param>
    /// <param name="layerIndex">Optional layer index to assign to the rings.</param>
    public static GameObject BuildRingSystem(GameObject parentObj, CelestialBodyData bodyData, Material mat, int layerIndex = -1)
    {
        if (!bodyData.hasRings) return null;

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

        if (mat != null)
        {
            mr.sharedMaterial = mat;
            MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
            propBlock.SetColor("_BaseColor", bodyData.ringColor);
            mr.SetPropertyBlock(propBlock);
        }

        if (layerIndex != -1) SetLayerRecursively(ringObj, layerIndex);

        return ringObj;
    }

    /// <summary>
    /// Helper method to ensure the prefab and all its contents are properly isolated on a specific layer.
    /// </summary>
    /// <param name="obj">The root GameObject to set the layer for.</param>
    /// <param name="newLayer">The layer index to assign.</param>
    public static void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (newLayer == -1 || obj == null) return;
        
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }
}