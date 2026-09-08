/// <summary>
/// Data container specifically for the central star.
/// </summary>
public class StarData
{
    public string name;
    public string spectralClass;
    public float frostLine;
    public float axialTilt; 
    public float rotationPeriod;
    public float mass; // In Solar Masses (M_S)
    public float temperature; // In Kelvin (K)
    public float radius; // In Solar Radii (R_S)

    // Visual & Procedural Surface Data
    public UnityEngine.Color baseColor;
    public float magneticActivity; // 0.0f to 1.0f (density of starspots and flares)
    public float granulationScale; // Relative size of surface convection cells
}