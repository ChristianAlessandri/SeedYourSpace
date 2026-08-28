using System.Collections.Generic;

/// <summary>
/// Data container for a planet, which can hold a list of orbiting moons.
/// </summary>
public class PlanetData : CelestialBodyData
{
    public List<MoonData> moons = new List<MoonData>();
}