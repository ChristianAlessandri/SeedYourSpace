using UnityEngine;

public static class CelestialMaterialHelper
{
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