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
    public bool isTidallyLocked;
}