using UnityEngine;
using System;

public class AstrophysicsRulesV1: IAstrophysicsRules
{
    private GenerationData generationData;
    private IStochasticMath math;

    public void Initialize(GenerationData data, IStochasticMath mathCore)
    {
        this.generationData = data;
        this.math = mathCore;
    }

    public string GetSpectralClassName(int index)
    {
        return generationData.spectralClasses[Mathf.Clamp(index, 0, generationData.spectralClasses.Length - 1)];
    }

    // ==============================================================================
    // PLANETARY ORBITS
    // ==============================================================================
    public float CalculateOrbitalDistance(int planetIndex, System.Random prng)
    {
        float baseDistance = (planetIndex > 0) ? (0.4f + 0.3f * Mathf.Pow(2, planetIndex - 1)) : 0.4f;
        float varianceModifier = math.GetNormalValue(prng, 0f, 0.05f);
        varianceModifier = Mathf.Clamp(varianceModifier, -0.15f, 0.15f); 
        
        return baseDistance * (1f + varianceModifier);
    }

    public float CalculateEccentricity(System.Random prng)
    {
        float eccentricity = math.GetNormalValue(prng, 0.05f, 0.08f);
        return Mathf.Clamp(Mathf.Abs(eccentricity), 0f, 0.99f);
    }

    // ==============================================================================
    // PLANETARY CLASSIFICATION
    // ==============================================================================
    public PlanetProfile ClassifyPlanet(float distance, System.Random prng, float systemFrostLine)
    {
        bool isOutsideFrostLine = distance > systemFrostLine;
        float totalWeight = 0f;
        
        PlanetProfile[] profiles = new PlanetProfile[generationData.planetClasses.Length];

        for (int i = 0; i < generationData.planetClasses.Length; i++)
        {
            var p = generationData.planetClasses[i];
            float currentWeight = isOutsideFrostLine ? p.outsideFrostWeight : p.insideFrostWeight;
            profiles[i] = new PlanetProfile(p.className, p.radiusMean, p.radiusStdDev, p.densityMean, currentWeight);
            totalWeight += currentWeight;
        }

        float randomSpin = (float)(prng.NextDouble() * totalWeight);
        float cumulativeWeight = 0f;

        foreach (var p in profiles)
        {
            cumulativeWeight += p.currentWeight;
            if (randomSpin <= cumulativeWeight) return p;
        }

        return profiles[0]; // Fallback
    }

    public string ClassifyMoon(float planetDistance, float systemFrostLine, System.Random prng)
    {
        if (planetDistance > systemFrostLine)
        {
            return (prng.NextDouble() < generationData.chanceIcyMoon) ? "Icy Moon" : "Rocky Moon";
        }
        return "Rocky Moon"; 
    }

    // ==============================================================================
    // RING SYSTEMS
    // ==============================================================================
    public void CalculateRings(string planetClass, float bodyRadius, System.Random prng, out bool hasRings, out int ringCount, out float innerRadius, out float outerRadius, out Color ringColor)
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

    private bool DetermineRingPresence(string planetClass, System.Random prng, out int ringCount)
    {
        ringCount = 0;
        double chance = prng.NextDouble();

        if (planetClass.Contains("Giant"))
        {
            if (chance <= generationData.chanceGiantRings)
            {
                ringCount = Mathf.Clamp(Mathf.RoundToInt(math.GetNormalValue(prng, 3f, 1f)), 1, 6);
                return true;
            }
        }
        else if (chance <= generationData.chanceTerrestrialRings)
        {
            ringCount = 1;
            return true;
        }
        
        return false;
    }

    private void CalculateRingBoundaries(string planetClass, float bodyRadius, System.Random prng, out float innerRadius, out float outerRadius)
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

    private Color GetRingColor(string planetClass)
    {
        if (planetClass == "Ice Giant") return new Color(0.7f, 0.85f, 0.95f, 0.6f); 
        if (planetClass.Contains("Giant")) return new Color(0.6f, 0.5f, 0.4f, 0.7f); 
        return new Color(0.4f, 0.4f, 0.4f, 0.8f); 
    }

    // ==============================================================================
    // ATMOSPHERICS
    // ==============================================================================
    public string DetermineAtmosphere(string className, float gravity, float distance, float frostLine, System.Random prng)
    {
        if (gravity < 0.25f) return "None (Vacuum)"; 

        if (className.Contains("Giant"))
        {
            return GenerateGiantAtmosphere(className, prng);
        }

        return GenerateRockyAtmosphere(gravity, distance, frostLine, prng);
    }

    private string GenerateGiantAtmosphere(string className, System.Random prng)
    {
        float h2 = Mathf.Clamp(math.GetNormalValue(prng, 75f, 5f), 65f, 85f);
        float he = Mathf.Clamp(99f - h2, 10f, 30f);
        float trace = Mathf.Max(100f - (h2 + he), 0.1f);
        
        string traceGas = className.Contains("Ice Giant") ? "CH4/NH3" : "CH4";
        return $"Dense Gas | H2 ({h2:F1}%), He ({he:F1}%), {traceGas} ({trace:F1}%)";
    }

    private string GenerateRockyAtmosphere(float gravity, float distance, float frostLine, System.Random prng)
    {
        double anomaly = prng.NextDouble();
        string density = (gravity > 1.2f) ? "Thick" : (gravity < 0.6f) ? "Thin" : "Moderate";

        if (distance > frostLine) 
        {
            if (gravity > 1.5f) return $"{density} | H2, He (Primordial)";
            
            if (anomaly < generationData.anomalyChanceFrozen) 
            {
                float n2 = Mathf.Clamp(math.GetNormalValue(prng, 90f, 5f), 80f, 98f);
                float ch4 = Mathf.Max(100f - n2, 0.1f);
                return $"{density} | N2 ({n2:F1}%), CH4 ({ch4:F1}%)";
            }
            return "Trace (Frozen CO2/CH4)";
        } 
        
        if (anomaly < generationData.anomalyChanceHabitable) 
        {
            float n2 = Mathf.Clamp(math.GetNormalValue(prng, 75f, 5f), 60f, 85f);
            float o2 = Mathf.Clamp(math.GetNormalValue(prng, 21f, 3f), 15f, 30f);
            float trace = Mathf.Max(100f - (n2 + o2), 0.1f);
            return $"{density} (Habitable) | N2 ({n2:F1}%), O2 ({o2:F1}%), Ar/CO2 ({trace:F1}%)";
        }

        if (anomaly < generationData.anomalyChanceToxic) 
        {
            float co2 = Mathf.Clamp(math.GetNormalValue(prng, 95f, 2f), 90f, 98f);
            float n2 = Mathf.Max(100f - co2, 0.1f);
            return $"{density} (Toxic) | CO2 ({co2:F1}%), N2/SO2 ({n2:F1}%)";
        }
        
        float co2thin = Mathf.Clamp(math.GetNormalValue(prng, 95f, 3f), 90f, 98f);
        return $"{density} | CO2 ({co2thin:F1}%), Ar/N2 ({100f - co2thin:F1}%)";
    }

    // ==============================================================================
    // PROCEDURAL VISUALS
    // ==============================================================================
    public void CalculateStellarSurface(float temperature, float mass, float radius, float rotationPeriod, System.Random prng, out Color baseColor, out float magneticActivity, out float granulationScale)
    {
        baseColor = CalculateStellarColor(temperature);
        granulationScale = CalculateStellarGranulation(mass, radius, prng);
        magneticActivity = CalculateStellarMagneticActivity(mass, rotationPeriod, prng);
    }

    private Color CalculateStellarColor(float temperature)
    {
        float t = Mathf.InverseLerp(3000f, 30000f, temperature);
        Color redDwarf = new Color(1.0f, 0.4f, 0.1f);
        Color sunYellow = new Color(1.0f, 0.9f, 0.8f);
        Color blueGiant = new Color(0.5f, 0.7f, 1.0f);
        
        return t < 0.2f ? Color.Lerp(redDwarf, sunYellow, t / 0.2f) : Color.Lerp(sunYellow, blueGiant, (t - 0.2f) / 0.8f);
    }

    private float CalculateStellarGranulation(float mass, float radius, System.Random prng)
    {
        float surfaceGravity = mass / (radius * radius);
        float baseGranulation = 1f / Mathf.Max(surfaceGravity, 0.01f);
        float variance = math.GetNormalValue(prng, 1.0f, 0.1f);
        
        return Mathf.Clamp(baseGranulation * variance, 0.1f, 50f);
    }

    private float CalculateStellarMagneticActivity(float mass, float rotationPeriod, System.Random prng)
    {
        float rotationFactor = 1000f / Mathf.Max(rotationPeriod, 1f); 
        float massFactor = 1f / Mathf.Max(mass, 0.1f);
        float rawActivity = (rotationFactor * 0.4f) + (massFactor * 0.6f);
        float activityNoise = math.GetNormalValue(prng, 0f, 0.15f);
        
        return Mathf.Clamp01((rawActivity / 5f) + activityNoise);
    }

    public void CalculatePlanetVisuals(string className, float temperature, string atmosphere, System.Random prng, out Color baseColor, out Color secondaryColor, out float hydrofraction, out float cloudCoverage)
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

    private void CalculateGiantVisuals(string className, out Color baseColor, out Color secondaryColor, out float hydrofraction, out float cloudCoverage)
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

    private void CalculateTerrestrialVisuals(float temperature, string atmosphere, System.Random prng, out Color baseColor, out Color secondaryColor, out float hydrofraction, out float cloudCoverage)
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

    public void CalculateAtmosphereVisuals(string atmosphereType, out Color atmosColor, out Color cloudColor, out float atmosScale)
    {
        // Default fallbacks
        atmosColor = new Color(0.4f, 0.6f, 1.0f); 
        cloudColor = Color.white;
        atmosScale = 1.10f;

        if (atmosphereType.Contains("Vacuum") || atmosphereType.Contains("None"))
        {
            atmosScale = 1.0f;
            atmosColor = Color.clear;
            cloudColor = Color.clear;
            return;
        }

        // Calculate Thickness Scale based on Density descriptor
        if (atmosphereType.Contains("Thick")) atmosScale = 1.25f;
        else if (atmosphereType.Contains("Moderate") || atmosphereType.Contains("Habitable")) atmosScale = 1.15f;
        else if (atmosphereType.Contains("Thin")) atmosScale = 1.08f;
        else if (atmosphereType.Contains("Trace")) atmosScale = 1.03f;
        else if (atmosphereType.Contains("Dense Gas")) atmosScale = 1.12f; 

        // Calculate Colors based on Chemical Composition
        if (atmosphereType.Contains("Habitable") || atmosphereType.Contains("O2"))
        {
            atmosColor = new Color(0.3f, 0.6f, 1.0f); // Earth Blue
            cloudColor = new Color(1.0f, 1.0f, 1.0f); // Pure White Clouds
        }
        else if (atmosphereType.Contains("Toxic") || atmosphereType.Contains("SO2"))
        {
            atmosColor = new Color(0.6f, 0.7f, 0.2f); // Sickly Venus Green-Yellow
            cloudColor = new Color(0.8f, 0.9f, 0.5f); // Sulphur/Pale Yellow Clouds
        }
        else if (atmosphereType.Contains("CH4"))
        {
            if (atmosphereType.Contains("Dense Gas")) // Neptune/Uranus Ice Giants
            {
                atmosColor = new Color(0.1f, 0.3f, 0.8f); // Deep Methane Blue
                cloudColor = new Color(0.7f, 0.8f, 1.0f); // Bright blue cirrus
            }
            else // Titan-like
            {
                atmosColor = new Color(0.9f, 0.5f, 0.1f); // Thick Orange Haze
                cloudColor = new Color(1.0f, 0.8f, 0.5f); 
            }
        }
        else if (atmosphereType.Contains("CO2"))
        {
            atmosColor = new Color(0.8f, 0.4f, 0.2f); // Mars Dust / Rusty Orange
            cloudColor = new Color(0.9f, 0.7f, 0.6f); 
        }
        else if (atmosphereType.Contains("H2") || atmosphereType.Contains("He"))
        {
            atmosColor = new Color(0.6f, 0.65f, 0.7f); // Pale Gas Giant
            cloudColor = new Color(0.9f, 0.85f, 0.8f); // Cream/Beige Bands
        }
    }
}