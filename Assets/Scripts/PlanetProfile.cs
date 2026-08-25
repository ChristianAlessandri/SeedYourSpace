/// <summary>
/// Represents the statistical profile of a planetary taxonomy class.
/// </summary>
public class PlanetProfile
{
    public string className;
    public float radiusMean;
    public float radiusStdDev;
    public float currentWeight; // Statistical weight for the Roulette Wheel

    public PlanetProfile(string name, float mean, float stdDev, float weight)
    {
        className = name;
        radiusMean = mean;
        radiusStdDev = stdDev;
        currentWeight = weight;
    }
}