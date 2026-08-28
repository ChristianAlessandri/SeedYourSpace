using UnityEngine;
using System;

/// <summary>
/// Encapsulates the physical rules and taxonomy logic for celestial bodies.
/// </summary>
public static class AstrophysicsRules
{
    /// <summary>
    /// Gets the spectral class name based on the given index.
    /// </summary>
    /// <param name="index">The index of the spectral class.</param>
    /// <returns>The name of the spectral class.</returns>
    public static string GetSpectralClassName(int index)
    {
        string[] classes = { "O (Blue)", "B (Blue-White)", "A (White)", "F (Yellow-White)", "G (Yellow - Solar)", "K (Orange)", "M (Red Dwarf)" };
        return classes[Mathf.Clamp(index, 0, classes.Length - 1)];
    }

    /// <summary>
    /// Calculates the orbital distance of a planet based on its index and a random number generator.
    /// </summary>
    /// <param name="planetIndex">The index of the planet.</param>
    /// <param name="prng">The random number generator.</param>
    /// <returns>The calculated orbital distance.</returns>
    public static float CalculateOrbitalDistance(int planetIndex, System.Random prng)
    {
        float baseDistance = (planetIndex > 0) ? (0.4f + 0.3f * Mathf.Pow(2, planetIndex - 1)) : 0.4f;
        float varianceModifier = StochasticMath.GetNormalValue(prng, 0f, 0.05f);
        varianceModifier = Mathf.Clamp(varianceModifier, -0.15f, 0.15f); 
        
        return baseDistance * (1f + varianceModifier);
    }

    /// <summary>
    /// Classifies a planet based on its distance from the star and other factors.
    /// </summary>
    /// <param name="distance">The orbital distance of the planet.</param>
    /// <param name="prng">The random number generator.</param>
    /// <param name="systemFrostLine">The frost line of the star system.</param>
    /// <returns>The classified planet profile.</returns>
    public static PlanetProfile ClassifyPlanet(float distance, System.Random prng, float systemFrostLine)
    {
        PlanetProfile terrestrial = new PlanetProfile("Terrestrial", 1.0f, 0.3f, 1.0f, 10f);
        PlanetProfile superEarth = new PlanetProfile("Super-Earth", 2.0f, 0.5f, 1.2f, 5f);
        PlanetProfile iceGiant = new PlanetProfile("Ice Giant", 4.0f, 1.0f, 0.3f, 2f);
        PlanetProfile gasGiant = new PlanetProfile("Gas Giant", 11.2f, 2.5f, 0.22f, 1f);

        if (distance > systemFrostLine)
        {
            iceGiant.currentWeight += 15f;
            gasGiant.currentWeight += 20f;
            terrestrial.currentWeight = 2f; 
        }
        else
        {
            terrestrial.currentWeight += 15f;
            superEarth.currentWeight += 10f;
            gasGiant.currentWeight += 1f; 
        }

        PlanetProfile[] profiles = { terrestrial, superEarth, iceGiant, gasGiant };
        float totalWeight = 0f;
        foreach (var p in profiles) totalWeight += p.currentWeight;

        float randomSpin = (float)(prng.NextDouble() * totalWeight);
        float cumulativeWeight = 0f;

        foreach (var p in profiles)
        {
            cumulativeWeight += p.currentWeight;
            if (randomSpin <= cumulativeWeight) return p;
        }

        return terrestrial;
    }

    /// <summary>
    /// Classifies a moon based on the distance of its parent planet and the system's frost
    /// line, using a random number generator.
    /// </summary>
    /// <param name="planetDistance">The orbital distance of the parent planet.</param>
    /// <param name="systemFrostLine">The frost line of the star system.</param>
    /// <param name="prng">The random number generator.</param>
    /// <returns>The classified moon type.</returns>
    public static string ClassifyMoon(float planetDistance, float systemFrostLine, System.Random prng)
    {
        if (planetDistance > systemFrostLine)
        {
            // Beyond the frost line, moons are more likely to be icy.
            return (prng.NextDouble() < 0.75) ? "Icy Moon" : "Rocky Moon";
        }
        return "Rocky Moon"; 
    }

    /// <summary>
    /// Calculates the eccentricity of a planet's orbit.
    /// </summary>
    /// <param name="prng">The random number generator.</param>
    /// <returns>The calculated eccentricity.</returns>
    public static float CalculateEccentricity(System.Random prng)
    {
        float eccentricity = StochasticMath.GetNormalValue(prng, 0.05f, 0.08f);
        return Mathf.Clamp(Mathf.Abs(eccentricity), 0f, 0.99f);
    }

    /// <summary>
    /// Calculates whether a planet has rings and how many divisions those rings have, based on its class and a random number generator.
    /// </summary>
    /// <param name="planetClass">The class of the planet.</param>
    /// <param name="prng">The random number generator.</param>
    /// <param name="hasRings">Whether the planet has rings.</param>
    /// <param name="ringCount">The number of ring divisions.</param>
    public static void CalculateRings(string planetClass, System.Random prng, out bool hasRings, out int ringCount)
    {
        hasRings = false;
        ringCount = 0;
        double ringChance = prng.NextDouble();

        if (planetClass == "Gas Giant" || planetClass == "Ice Giant")
        {
            if (ringChance <= 0.85)
            {
                hasRings = true;
                ringCount = Mathf.Clamp(Mathf.RoundToInt(StochasticMath.GetNormalValue(prng, 3f, 1f)), 1, 6);
            }
        }
        else if (ringChance <= 0.04)
        {
            hasRings = true;
            ringCount = 1;
        }
    }

    /// <summary>
    /// Determines the type of atmosphere a celestial body has based on its class, gravity, distance
    /// from the star, frost line, and a random number generator.
    /// </summary>
    /// <param name="className">The class of the celestial body.</param>
    /// <param name="gravity">The surface gravity of the celestial body.</param>
    /// <param name="distance">The orbital distance of the celestial body.</param>
    /// <param name="frostLine">The frost line of the star system.</param>
    /// <param name="prng">The random number generator.</param>
    /// <returns>The determined atmosphere type.</returns>
    public static string DetermineAtmosphere(string className, float gravity, float distance, float frostLine, System.Random prng)
    {
        // Gravity threshold for retaining an atmosphere (Jeans escape)
        if (gravity < 0.25f) return "None (Vacuum)"; 

        // Gas Giants and Ice Giants (Primordial Atmospheres)
        if (className.Contains("Giant"))
        {
            // Gaussian distribution for H2 and He percentages, with constraints to ensure they sum to 100%
            float h2 = Mathf.Clamp(StochasticMath.GetNormalValue(prng, 75f, 5f), 65f, 85f);
            float he = Mathf.Clamp(99f - h2, 10f, 30f);
            float trace = Mathf.Max(100f - (h2 + he), 0.1f);
            
            string traceGas = className.Contains("Ice Giant") ? "CH4/NH3" : "CH4";
            return $"Dense Gas | H2 ({h2:F1}%), He ({he:F1}%), {traceGas} ({trace:F1}%)";
        }

        // Rocky Planets
        double anomaly = prng.NextDouble();
        string density = (gravity > 1.2f) ? "Thick" : (gravity < 0.6f) ? "Thin" : "Moderate";

        if (distance > frostLine) 
        {
            // Extremely Cold Zone (Beyond Frost Line)
            if (gravity > 1.5f) return $"{density} | H2, He (Primordial)";
            if (anomaly < 0.3) 
            {
                // Titanic-like Atmosphere (N2 + CH4)
                float n2 = Mathf.Clamp(StochasticMath.GetNormalValue(prng, 90f, 5f), 80f, 98f);
                float ch4 = Mathf.Max(100f - n2, 0.1f);
                return $"{density} | N2 ({n2:F1}%), CH4 ({ch4:F1}%)";
            }
            return "Trace (Frozen CO2/CH4)";
        } 
        else 
        {
            // Hot Zone
            if (anomaly < 0.05) 
            {
                // Habitable Atmosphere (N2 + O2)
                float n2 = Mathf.Clamp(StochasticMath.GetNormalValue(prng, 75f, 5f), 60f, 85f);
                float o2 = Mathf.Clamp(StochasticMath.GetNormalValue(prng, 21f, 3f), 15f, 30f);
                float trace = Mathf.Max(100f - (n2 + o2), 0.1f);
                return $"{density} (Habitable) | N2 ({n2:F1}%), O2 ({o2:F1}%), Ar/CO2 ({trace:F1}%)";
            }
            if (anomaly < 0.5) 
            {
                // Greenhouse Atmosphere (CO2 + N2)
                float co2 = Mathf.Clamp(StochasticMath.GetNormalValue(prng, 95f, 2f), 90f, 98f);
                float n2 = Mathf.Max(100f - co2, 0.1f);
                return $"{density} (Toxic) | CO2 ({co2:F1}%), N2/SO2 ({n2:F1}%)";
            }
            
            // Thin Atmosphere (CO2 + Ar/N2)
            float co2thin = Mathf.Clamp(StochasticMath.GetNormalValue(prng, 95f, 3f), 90f, 98f);
            return $"{density} | CO2 ({co2thin:F1}%), Ar/N2 ({100f - co2thin:F1}%)";
        }
    }

    /// <summary>
    /// Calculates the procedural visual parameters of a star based on its physical properties.
    /// </summary>
    /// <param name="temperature">Surface temperature in Kelvin.</param>
    /// <param name="mass">Mass in Solar Masses.</param>
    /// <param name="radius">Radius in Solar Radii.</param>
    /// <param name="rotationPeriod">Rotation period in hours.</param>
    /// <param name="prng">The random number generator.</param>
    /// <param name="baseColor">Calculated blackbody RGB color.</param>
    /// <param name="magneticActivity">Calculated magnetic activity (0.0 to 1.0).</param>
    /// <param name="granulationScale">Calculated granulation cell size multiplier.</param>
    public static void CalculateStellarSurface(float temperature, float mass, float radius, float rotationPeriod, System.Random prng, out Color baseColor, out float magneticActivity, out float granulationScale)
    {
        // Base Color (Simplified Blackbody to RGB approximation)
        // Adjusting temperature to a 0-1 lerp factor roughly between 3000K and 35000K
        float t = Mathf.InverseLerp(3000f, 30000f, temperature);
        Color redDwarf = new Color(1.0f, 0.4f, 0.1f);
        Color sunYellow = new Color(1.0f, 0.9f, 0.8f);
        Color blueGiant = new Color(0.5f, 0.7f, 1.0f);
        
        if (t < 0.2f) 
            baseColor = Color.Lerp(redDwarf, sunYellow, t / 0.2f);
        else 
            baseColor = Color.Lerp(sunYellow, blueGiant, (t - 0.2f) / 0.8f);

        // Granulation Scale (Inversely proportional to surface gravity)
        // Surface gravity g = M / R^2. Granulation size is roughly proportional to 1/g.
        float surfaceGravity = mass / (radius * radius);
        float baseGranulation = 1f / Mathf.Max(surfaceGravity, 0.01f);
        
        // Add slight stochastic variance (+/- 10%)
        float variance = StochasticMath.GetNormalValue(prng, 1.0f, 0.1f);
        granulationScale = Mathf.Clamp(baseGranulation * variance, 0.1f, 50f);

        // Magnetic Activity (Dynamo effect)
        // Fast rotation (low period) and low mass (deep convection zones) increase activity.
        float rotationFactor = 1000f / Mathf.Max(rotationPeriod, 1f); 
        float massFactor = 1f / Mathf.Max(mass, 0.1f);
        
        float rawActivity = (rotationFactor * 0.4f) + (massFactor * 0.6f);
        float activityNoise = StochasticMath.GetNormalValue(prng, 0f, 0.15f);
        
        magneticActivity = Mathf.Clamp01((rawActivity / 5f) + activityNoise);
    }
}