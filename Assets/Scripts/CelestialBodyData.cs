/// <summary>
/// Base data container for any celestial body in the system.
/// </summary>
public class CelestialBodyData
{
    public string name;
    public float orbitalDistance;
    public float orbitalEccentricity;
    public string className;
    public float radius;
    public float mass;
    public bool hasRings;
    public int ringDivisions;
    public float revolutionPeriod; // For planets: Earth Years. For moons: Earth Days.
    public float rotationPeriod;   // In Hours
    public float axialTilt;         // Axial tilt in degrees
    public float orbitalInclination;// Orbital inclination in degrees
    public float surfaceGravity;    // Surface gravity in Gs (Earth = 1.0)
    public string atmosphereType;   // Atmosphere type/biome
    public bool isTidallyLocked; // For moons
}