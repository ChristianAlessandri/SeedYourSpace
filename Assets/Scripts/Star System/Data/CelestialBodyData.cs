/// <summary>
/// Base data container for any orbiting celestial body (Planets and Moons).
/// </summary>
public class CelestialBodyData
{
    // Identifiers & Physical traits
    public string name;
    public string className;
    public float mass; // In Earth Masses (M_E)
    public float radius; // In Earth Radii (R_E)
    public float surfaceTemperature; // In Kelvin (K)
    public float surfaceGravity; // In Gs
    public string atmosphereType;
    
    // Orbital mechanics
    public float orbitalDistance;
    public float orbitalEccentricity;
    public float orbitalInclination;
    public float revolutionPeriod; // In Earth Days for both Planets and Moons
    
    // Rotation & Visuals
    public float rotationPeriod;
    public float axialTilt;
    public bool hasRings;
    public int ringDivisions;

    // Procedural Surface Visual Data
    public UnityEngine.Color baseColor;
    public UnityEngine.Color secondaryColor;
    public float hydrofraction; // Percentage of surface covered by liquid (0.0 to 1.0)
    public float cloudCoverage; // Percentage of cloud cover (0.0 to 1.0)
}