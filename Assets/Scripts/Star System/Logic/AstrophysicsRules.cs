using UnityEngine;
using System;

/// <summary>
/// Encapsulates the physical rules and taxonomy logic for celestial bodies.
/// </summary>
public static class AstrophysicsRules
{
    // ==============================================================================
    // ORBITAL MECHANICS & CLASSIFICATION
    // ==============================================================================

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
            return (prng.NextDouble() < 0.75) ? "Icy Moon" : "Rocky Moon";
        }
        return "Rocky Moon"; 
    }

    // ==============================================================================
    // RING SYSTEMS
    // ==============================================================================

    /// <summary>
    /// Calculates whether a planet has rings and generates their physical and visual properties.
    /// Acts as an orchestrator for single-responsibility internal methods.
    /// </summary>
    /// <param name="planetClass">The class of the planet.</param>
    /// <param name="bodyRadius">The radius of the planet.</param>
    /// <param name="prng">The random number generator.</param>
    /// <param name="hasRings">Output parameter indicating if the planet has rings.</param>
    /// <param name="ringCount">Output parameter for the number of rings.</param>
    /// <param name="innerRadius">Output parameter for the inner radius of the rings.</param>
    /// <param name="outerRadius">Output parameter for the outer radius of the rings.</param>
    /// <param name="ringColor">Output parameter for the color of the rings.</param>
    public static void CalculateRings(string planetClass, float bodyRadius, System.Random prng, out bool hasRings, out int ringCount, out float innerRadius, out float outerRadius, out Color ringColor)
    {
        hasRings = DetermineRingPresence(planetClass, prng, out ringCount);

        if (hasRings)
        {
            CalculateRingBoundaries(planetClass, bodyRadius, prng, out innerRadius, out outerRadius);
            ringColor = GetRingColor(planetClass);
        }
        else
        {
            innerRadius = 0f;
            outerRadius = 0f;
            ringColor = Color.clear;
        }
    }

    /// <summary>
    /// Determines if a planet has rings and calculates their count.
    /// </summary>
    /// <param name="planetClass">The class of the planet.</param>
    /// <param name="prng">The random number generator.</param>
    /// <param name="ringCount">Output parameter for the number of rings.</param>
    /// <returns>True if the planet has rings, false otherwise.</returns>
    private static bool DetermineRingPresence(string planetClass, System.Random prng, out int ringCount)
    {
        ringCount = 0;
        double chance = prng.NextDouble();

        if (planetClass.Contains("Giant"))
        {
            if (chance <= 0.85)
            {
                ringCount = Mathf.Clamp(Mathf.RoundToInt(StochasticMath.GetNormalValue(prng, 3f, 1f)), 1, 6);
                return true;
            }
        }
        else if (chance <= 0.04)
        {
            ringCount = 1;
            return true;
        }
        
        return false;
    }

    /// <summary>
    /// Calculates the inner and outer boundaries of a planet's rings based on its class and radius
    /// and a random number generator.
    /// </summary>
    /// <param name="planetClass">The class of the planet.</param>
    /// <param name="bodyRadius">The radius of the planet.</param>
    /// <param name="prng">The random number generator.</param>
    /// <param name="innerRadius">Output parameter for the inner radius of the rings.</param>
    /// <param name="outerRadius">Output parameter for the outer radius of the rings.</param>
    private static void CalculateRingBoundaries(string planetClass, float bodyRadius, System.Random prng, out float innerRadius, out float outerRadius)
    {
        if (planetClass.Contains("Giant"))
        {
            innerRadius = bodyRadius * (float)(1.2 + prng.NextDouble() * 0.5); 
            outerRadius = innerRadius + bodyRadius * (float)(0.5 + prng.NextDouble() * 2.0);
        }
        else
        {
            innerRadius = bodyRadius * 1.3f;
            outerRadius = innerRadius + bodyRadius * 0.4f;
        }
    }

    /// <summary>
    /// Gets the color of the rings based on the planet's class.
    /// </summary>
    /// <param name="planetClass">The class of the planet.</param>
    /// <returns>The color of the rings.</returns>
    private static Color GetRingColor(string planetClass)
    {
        if (planetClass == "Ice Giant") return new Color(0.7f, 0.85f, 0.95f, 0.6f); 
        if (planetClass.Contains("Giant")) return new Color(0.6f, 0.5f, 0.4f, 0.7f); 
        return new Color(0.4f, 0.4f, 0.4f, 0.8f); 
    }

    // ==============================================================================
    // ATMOSPHERICS
    // ==============================================================================

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
        if (gravity < 0.25f) return "None (Vacuum)"; 

        if (className.Contains("Giant"))
        {
            return GenerateGiantAtmosphere(className, prng);
        }

        return GenerateRockyAtmosphere(gravity, distance, frostLine, prng);
    }

    /// <summary>
    /// Generates the atmosphere for a gas giant based on its class and a random number generator.
    /// </summary>
    /// <param name="className">The class of the gas giant.</param>
    /// <param name="prng">The random number generator.</param>
    /// <returns>The generated atmosphere type.</returns>
    private static string GenerateGiantAtmosphere(string className, System.Random prng)
    {
        float h2 = Mathf.Clamp(StochasticMath.GetNormalValue(prng, 75f, 5f), 65f, 85f);
        float he = Mathf.Clamp(99f - h2, 10f, 30f);
        float trace = Mathf.Max(100f - (h2 + he), 0.1f);
        
        string traceGas = className.Contains("Ice Giant") ? "CH4/NH3" : "CH4";
        return $"Dense Gas | H2 ({h2:F1}%), He ({he:F1}%), {traceGas} ({trace:F1}%)";
    }

    /// <summary>
    /// Generates the atmosphere for a rocky planet based on its gravity, distance from the star,
    /// frost line, and a random number generator.
    /// </summary>
    /// <param name="gravity">The surface gravity of the rocky planet.</param>
    /// <param name="distance">The orbital distance of the rocky planet.</param>
    /// <param name="frostLine">The frost line of the star system.</param>
    /// <param name="prng">The random number generator.</param>
    /// <returns>The generated atmosphere type.</returns>
    private static string GenerateRockyAtmosphere(float gravity, float distance, float frostLine, System.Random prng)
    {
        double anomaly = prng.NextDouble();
        string density = (gravity > 1.2f) ? "Thick" : (gravity < 0.6f) ? "Thin" : "Moderate";

        if (distance > frostLine) 
        {
            if (gravity > 1.5f) return $"{density} | H2, He (Primordial)";
            
            if (anomaly < 0.3) 
            {
                float n2 = Mathf.Clamp(StochasticMath.GetNormalValue(prng, 90f, 5f), 80f, 98f);
                float ch4 = Mathf.Max(100f - n2, 0.1f);
                return $"{density} | N2 ({n2:F1}%), CH4 ({ch4:F1}%)";
            }
            return "Trace (Frozen CO2/CH4)";
        } 
        
        if (anomaly < 0.05) 
        {
            float n2 = Mathf.Clamp(StochasticMath.GetNormalValue(prng, 75f, 5f), 60f, 85f);
            float o2 = Mathf.Clamp(StochasticMath.GetNormalValue(prng, 21f, 3f), 15f, 30f);
            float trace = Mathf.Max(100f - (n2 + o2), 0.1f);
            return $"{density} (Habitable) | N2 ({n2:F1}%), O2 ({o2:F1}%), Ar/CO2 ({trace:F1}%)";
        }

        if (anomaly < 0.5) 
        {
            float co2 = Mathf.Clamp(StochasticMath.GetNormalValue(prng, 95f, 2f), 90f, 98f);
            float n2 = Mathf.Max(100f - co2, 0.1f);
            return $"{density} (Toxic) | CO2 ({co2:F1}%), N2/SO2 ({n2:F1}%)";
        }
        
        float co2thin = Mathf.Clamp(StochasticMath.GetNormalValue(prng, 95f, 3f), 90f, 98f);
        return $"{density} | CO2 ({co2thin:F1}%), Ar/N2 ({100f - co2thin:F1}%)";
    }

    // ==============================================================================
    // PROCEDURAL VISUALS
    // ==============================================================================

    /// <summary>
    /// Calculates the procedural visual parameters of a star based on its physical properties.
    /// Acts as an orchestrator for single-responsibility internal methods.
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
        baseColor = CalculateStellarColor(temperature);
        granulationScale = CalculateStellarGranulation(mass, radius, prng);
        magneticActivity = CalculateStellarMagneticActivity(mass, rotationPeriod, prng);
    }

    /// <summary>
    /// Calculates the blackbody color of a star based on its surface temperature using a simplified approximation.
    /// </summary>
    /// <param name="temperature">The surface temperature in Kelvin.</param>
    /// <returns>The calculated blackbody color.</returns>
    private static Color CalculateStellarColor(float temperature)
    {
        float t = Mathf.InverseLerp(3000f, 30000f, temperature);
        Color redDwarf = new Color(1.0f, 0.4f, 0.1f);
        Color sunYellow = new Color(1.0f, 0.9f, 0.8f);
        Color blueGiant = new Color(0.5f, 0.7f, 1.0f);
        
        return t < 0.2f ? Color.Lerp(redDwarf, sunYellow, t / 0.2f) : Color.Lerp(sunYellow, blueGiant, (t - 0.2f) / 0.8f);
    }

    /// <summary>
    /// Calculates the granulation scale of a star based on its mass and radius.
    /// </summary>
    /// <param name="mass">The mass of the star in Solar Masses.</param>
    /// <param name="radius">The radius of the star in Solar Radii.</param>
    /// <param name="prng">The random number generator.</param>
    /// <returns>The calculated granulation scale.</returns>
    private static float CalculateStellarGranulation(float mass, float radius, System.Random prng)
    {
        float surfaceGravity = mass / (radius * radius);
        float baseGranulation = 1f / Mathf.Max(surfaceGravity, 0.01f);
        float variance = StochasticMath.GetNormalValue(prng, 1.0f, 0.1f);
        
        return Mathf.Clamp(baseGranulation * variance, 0.1f, 50f);
    }

    /// <summary>
    /// Calculates the magnetic activity of a star based on its mass and rotation period.
    /// </summary>
    /// <param name="mass">The mass of the star in Solar Masses.</param>
    /// <param name="rotationPeriod">The rotation period of the star in hours.</param>
    /// <param name="prng">The random number generator.</param>
    /// <returns>The calculated magnetic activity (0.0 to 1.0).</returns>
    private static float CalculateStellarMagneticActivity(float mass, float rotationPeriod, System.Random prng)
    {
        float rotationFactor = 1000f / Mathf.Max(rotationPeriod, 1f); 
        float massFactor = 1f / Mathf.Max(mass, 0.1f);
        float rawActivity = (rotationFactor * 0.4f) + (massFactor * 0.6f);
        float activityNoise = StochasticMath.GetNormalValue(prng, 0f, 0.15f);
        
        return Mathf.Clamp01((rawActivity / 5f) + activityNoise);
    }

    /// <summary>
    /// Calculates procedural visual parameters for planets and moons based on their physical properties.
    /// Acts as an orchestrator for single-responsibility internal methods.
    /// </summary>
    /// <param name="className">Taxonomic class of the body.</param>
    /// <param name="temperature">Surface temperature in Kelvin.</param>
    /// <param name="atmosphere">Atmosphere type description string.</param>
    /// <param name="prng">The random number generator.</param>
    /// <param name="baseColor">Primary terrain/surface color.</param>
    /// <param name="secondaryColor">Secondary color (oceans, ice caps, or secondary bands).</param>
    /// <param name="hydrofraction">Liquid coverage ratio.</param>
    /// <param name="cloudCoverage">Cloud coverage ratio.</param>
    public static void CalculatePlanetVisuals(string className, float temperature, string atmosphere, System.Random prng, out Color baseColor, out Color secondaryColor, out float hydrofraction, out float cloudCoverage)
    {
        if (className.Contains("Giant"))
        {
            CalculateGiantVisuals(className, out baseColor, out secondaryColor, out hydrofraction, out cloudCoverage);
        }
        else
        {
            CalculateTerrestrialVisuals(temperature, atmosphere, prng, out baseColor, out secondaryColor, out hydrofraction, out cloudCoverage);
        }
    }

    /// <summary>
    /// Calculates procedural visual parameters for giant planets.
    /// </summary>
    /// <param name="className">Taxonomic class of the body.</param>
    /// <param name="baseColor">Primary terrain/surface color.</param>
    /// <param name="secondaryColor">Secondary color (oceans, ice caps, or secondary bands).</param>
    /// <param name="hydrofraction">Liquid coverage ratio.</param>
    /// <param name="cloudCoverage">Cloud coverage ratio.</param>
    private static void CalculateGiantVisuals(string className, out Color baseColor, out Color secondaryColor, out float hydrofraction, out float cloudCoverage)
    {
        hydrofraction = 0f;
        cloudCoverage = 1.0f; 

        if (className.Contains("Gas"))
        {
            baseColor = new Color(0.8f, 0.6f, 0.4f); 
            secondaryColor = new Color(0.9f, 0.8f, 0.7f);
        }
        else
        {
            baseColor = new Color(0.3f, 0.5f, 0.7f); 
            secondaryColor = new Color(0.2f, 0.3f, 0.5f);
        }
    }

    /// <summary>
    /// Calculates procedural visual parameters for terrestrial planets.
    /// </summary>
    /// <param name="temperature">Surface temperature.</param>
    /// <param name="atmosphere">Atmospheric composition.</param>
    /// <param name="prng">Random number generator.</param>
    /// <param name="baseColor">Primary terrain/surface color.</param>
    /// <param name="secondaryColor">Secondary color (oceans, ice caps, or secondary bands).</param>
    /// <param name="hydrofraction">Liquid coverage ratio.</param>
    /// <param name="cloudCoverage">Cloud coverage ratio.</param>
    private static void CalculateTerrestrialVisuals(float temperature, string atmosphere, System.Random prng, out Color baseColor, out Color secondaryColor, out float hydrofraction, out float cloudCoverage)
    {
        bool hasAtmosphere = !atmosphere.Contains("None") && !atmosphere.Contains("Vacuum");
        
        if (temperature > 200f && temperature < 350f && hasAtmosphere && (prng.NextDouble() < 0.4f))
        {
            hydrofraction = (float)prng.NextDouble() * 0.6f + 0.3f; 
            baseColor = new Color(0.2f, 0.4f, 0.15f); 
            secondaryColor = new Color(0.05f, 0.2f, 0.5f); 
            cloudCoverage = (float)prng.NextDouble() * 0.5f + 0.2f;
        }
        else if (temperature <= 273f)
        {
            hydrofraction = 0f;
            baseColor = new Color(0.8f, 0.85f, 0.9f); 
            secondaryColor = new Color(0.5f, 0.6f, 0.7f);
            cloudCoverage = (float)prng.NextDouble() * 0.3f;
        }
        else
        {
            hydrofraction = 0f;
            float r = (float)prng.NextDouble() * 0.4f + 0.3f;
            float g = r * 0.6f;
            float b = g * 0.5f;
            baseColor = new Color(r, g, b); 
            secondaryColor = baseColor * 0.6f;
            cloudCoverage = hasAtmosphere ? (float)prng.NextDouble() * 0.15f : 0f;
        }
    }
}