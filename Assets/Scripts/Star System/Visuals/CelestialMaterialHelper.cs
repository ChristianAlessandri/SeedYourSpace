using UnityEngine;

/// <summary>
/// A utility class to centralize the logic for calculating and applying procedural 
/// material properties for celestial bodies across different renderers.
/// </summary>
public static class CelestialMaterialHelper
{
    /// <summary>
    /// Calculates procedural colors and applies them along with base data to a MaterialPropertyBlock.
    /// </summary>
    /// <param name="bodyData">The data defining the celestial body.</param>
    /// <param name="props">The MaterialPropertyBlock to apply the properties to.</param>
    public static void ApplySurfaceProperties(CelestialBodyData bodyData, MaterialPropertyBlock props)
    {
        if (bodyData == null || props == null) return;

        props.SetColor("_BaseColor", bodyData.baseColor);
        props.SetColor("_SecondaryColor", bodyData.secondaryColor);
        props.SetFloat("_Hydrofraction", bodyData.hydrofraction);
        props.SetFloat("_CloudCoverage", bodyData.cloudCoverage);

        // Procedural Topography Colors
        Color deepWater = new Color(
            bodyData.secondaryColor.r * 0.3f, 
            bodyData.secondaryColor.g * 0.3f, 
            bodyData.secondaryColor.b * 0.3f, 
            1f);
        
        float gray = (bodyData.baseColor.r + bodyData.baseColor.g + bodyData.baseColor.b) / 3f;
        Color highland = new Color(
            Mathf.Lerp(bodyData.baseColor.r, gray, 0.5f) * 0.8f,
            Mathf.Lerp(bodyData.baseColor.g, gray, 0.5f) * 0.8f,
            Mathf.Lerp(bodyData.baseColor.b, gray, 0.5f) * 0.8f,
            1f);
        
        Color peak = new Color(
            Mathf.Lerp(bodyData.baseColor.r, 1f, 0.7f),
            Mathf.Lerp(bodyData.baseColor.g, 1f, 0.7f),
            Mathf.Lerp(bodyData.baseColor.b, 1f, 0.7f),
            1f);

        props.SetColor("_DeepWaterColor", deepWater);
        props.SetColor("_HighlandColor", highland);
        props.SetColor("_PeakColor", peak);
        
        // Offset for procedural noise mapping
        float seedOffset = (bodyData.name.GetHashCode() % 1000) / 1000f;
        props.SetVector("_Offset", new Vector4(seedOffset, seedOffset * 2.5f, seedOffset * -1.3f, 0f));
    }

    /// <summary>
    /// Overload for applying properties directly to a Material instance (used by the Atlas).
    /// </summary>
    /// <param name="bodyData">The data defining the celestial body.</param>
    /// <param name="material">The Material to apply the properties to.</param>
    public static void ApplySurfaceProperties(CelestialBodyData bodyData, Material material)
    {
        if (bodyData == null || material == null) return;

        material.SetColor("_BaseColor", bodyData.baseColor);
        material.SetColor("_SecondaryColor", bodyData.secondaryColor);
        material.SetFloat("_Hydrofraction", bodyData.hydrofraction);

        Color deepWater = new Color(
            bodyData.secondaryColor.r * 0.3f, 
            bodyData.secondaryColor.g * 0.3f, 
            bodyData.secondaryColor.b * 0.3f, 
            1f);
        
        float gray = (bodyData.baseColor.r + bodyData.baseColor.g + bodyData.baseColor.b) / 3f;
        Color highland = new Color(
            Mathf.Lerp(bodyData.baseColor.r, gray, 0.5f) * 0.8f,
            Mathf.Lerp(bodyData.baseColor.g, gray, 0.5f) * 0.8f,
            Mathf.Lerp(bodyData.baseColor.b, gray, 0.5f) * 0.8f,
            1f);
        
        Color peak = new Color(
            Mathf.Lerp(bodyData.baseColor.r, 1f, 0.7f),
            Mathf.Lerp(bodyData.baseColor.g, 1f, 0.7f),
            Mathf.Lerp(bodyData.baseColor.b, 1f, 0.7f),
            1f);

        material.SetColor("_DeepWaterColor", deepWater);
        material.SetColor("_HighlandColor", highland);
        material.SetColor("_PeakColor", peak);
        
        float seedOffset = (bodyData.name.GetHashCode() % 1000) / 1000f;
        material.SetVector("_Offset", new Vector4(seedOffset, seedOffset * 2.5f, seedOffset * -1.3f, 0f));
    }
}