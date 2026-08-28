using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;

/// <summary>
/// Core procedural generator responsible for deterministic star system generation.
/// Implements Box-Muller normal distributions and weighted statistical probabilities.
/// </summary>
public class StarSystemGenerator : MonoBehaviour
{
    [Header("Generation Settings")]
    public string masterSeed = "0xF5a9b2323e7f1C0C40843B33E7cEB2Ef4caAB895";
    
    [HideInInspector]
    public int algorithmVersion = 1;

    private MarkovNameGenerator nameGenerator;
    private float currentSystemFrostLine;

    private void Start()
    {
        GenerateCompleteStarSystem(masterSeed);
    }

    /// <summary>
    /// Generates a complete star system based on the provided seed, including the central star and its planetary system.
    /// </summary>
    /// <param name="seed">The seed for generating the star system.</param>
    public void GenerateCompleteStarSystem(string seed)
    {
        Debug.Log($"=== STARTING STAR SYSTEM GENERATION (Algorithm v{algorithmVersion}) ===");
        
        TextAsset jsonFile = Resources.Load<TextAsset>("markov_data");
        if (jsonFile == null)
        {
            Debug.LogError("Error: Markov data file not found!");
            return;
        }

        nameGenerator = new MarkovNameGenerator(jsonFile.text);
        System.Random systemPrng = new System.Random(DeriveNumericalSeed(seed));
        string rootSystemName = nameGenerator.GenerateSystemName(systemPrng);
        
        Debug.Log($"[Semantic Module] Root System Name: {rootSystemName}");

        StarData centralStar = GenerateCentralStar(seed, rootSystemName);
        GeneratePlanetarySystem(seed, rootSystemName, centralStar.mass);
    }

    /// <summary>
    /// Generates the central star of the system, determining its spectral class and frost line based on weighted probabilities and normal distributions.
    /// </summary>
    /// <param name="baseSeed">The seed for generating the central star.</param>
    /// <param name="rootName">The name of the root system.</param>
    private StarData GenerateCentralStar(string baseSeed, string rootName)
    {
        string starSubSeedInput = baseSeed + "_Star_Entity";
        int starNumericalSeed = DeriveNumericalSeed(starSubSeedInput);
        System.Random starPrng = new System.Random(starNumericalSeed);

        // O(Blue), B, A, F, G(Solar), K, M(Red Dwarf)
        float[] stellarWeights = { 0.1f, 1.0f, 2.0f, 4.0f, 8.0f, 15.0f, 70.0f };
        int spectralIndex = GetWeightedIndex(stellarWeights, starPrng);
        
        // Base physics approximations matching Harvard Spectral Classification
        float[] massMeans = { 40.0f, 6.0f, 2.0f, 1.3f, 1.0f, 0.7f, 0.3f };
        float[] tempMeans = { 35000f, 15000f, 8500f, 6500f, 5500f, 4500f, 3000f };

        StarData star = new StarData();
        star.name = rootName + " Prime";
        star.spectralClass = GetSpectralClassName(spectralIndex);
        
        // Use normal distribution for organic variety in mass and temp
        star.mass = Mathf.Max(GetNormalValue(starPrng, massMeans[spectralIndex], massMeans[spectralIndex] * 0.1f), 0.08f);
        star.temperature = Mathf.Max(GetNormalValue(starPrng, tempMeans[spectralIndex], tempMeans[spectralIndex] * 0.05f), 2000f);

        float[] baseFrostLines = { 15.0f, 10.0f, 6.0f, 4.0f, 2.7f, 1.5f, 0.5f };
        float baseLine = baseFrostLines[spectralIndex];

        float oscillation = GetNormalValue(starPrng, 0f, 0.05f);
        oscillation = Mathf.Clamp(oscillation, -0.20f, 0.20f);
        
        currentSystemFrostLine = baseLine * (1f + oscillation);
        star.frostLine = currentSystemFrostLine;

        Debug.Log($"[Star Module] {star.name} | Class: {star.spectralClass} | Mass: {star.mass:F2} SM | Temp: {Mathf.RoundToInt(star.temperature)} K | Frost Line: {star.frostLine:F2} AU");
        
        return star;
    }

    /// <summary>
    /// Generates the planetary system of the star, determining the number and characteristics of the planets.
    /// </summary>
    /// <param name="baseSeed">The seed for generating the planetary system.</param>
    /// <param name="rootName">The name of the root system.</param>
    /// <param name="starMass">The mass of the central star.</param>
    private void GeneratePlanetarySystem(string baseSeed, string rootName, float starMass)
    {
        string layoutSubSeedInput = baseSeed + "_Planets_Layout";
        int layoutNumericalSeed = DeriveNumericalSeed(layoutSubSeedInput);
        System.Random layoutPrng = new System.Random(layoutNumericalSeed);

        float rawPlanetCount = GetNormalValue(layoutPrng, 5.5f, 2.0f);
        int planetCount = Mathf.Clamp(Mathf.RoundToInt(rawPlanetCount), 1, 12);
        
        Debug.Log($"[Layout Module] Total Planets Scheduled: {planetCount}");

        List<PlanetData> systemPlanets = new List<PlanetData>();

        for (int i = 0; i < planetCount; i++)
        {
            string planetSubSeedInput = baseSeed + $"_Planet_Entity_{i}";
            int planetNumericalSeed = DeriveNumericalSeed(planetSubSeedInput);
            System.Random planetPrng = new System.Random(planetNumericalSeed);

            PlanetData planet = new PlanetData();
            planet.name = rootName + " " + nameGenerator.ToRoman(i + 1);
            planet.orbitalDistance = CalculateOrbitalDistance(i, planetPrng);

            PlanetProfile selectedClass = ClassifyPlanet(planet.orbitalDistance, planetPrng, currentSystemFrostLine);
            planet.className = selectedClass.className;
            
            planet.radius = GetNormalValue(planetPrng, selectedClass.radiusMean, selectedClass.radiusStdDev);
            planet.radius = Mathf.Max(planet.radius, 0.1f);

            // Mass calculation (M = R^3 * Density)
            float density = Mathf.Max(GetNormalValue(planetPrng, selectedClass.densityMean, 0.1f), 0.1f);
            planet.mass = Mathf.Pow(planet.radius, 3) * density;

            // Kepler's Third Law for Planetary Revolution (Earth Years)
            planet.revolutionPeriod = Mathf.Sqrt(Mathf.Pow(planet.orbitalDistance, 3) / starMass);

            // Rotation Period (Hours) using Box-Muller. 
            // Gas Giants rotate extremely fast (e.g., Jupiter ~10h), Terrestrials vary wildly.
            float baseRotation = (planet.className == "Gas Giant" || planet.className == "Ice Giant") ? 12f : 24f;
            planet.rotationPeriod = Mathf.Max(GetNormalValue(planetPrng, baseRotation, baseRotation * 0.5f), 2f); // Min 2 hours
            
            // Extreme anomalies close to the star get tidally locked to the star
            planet.isTidallyLocked = (planet.orbitalDistance < 0.2f);
            if (planet.isTidallyLocked) 
                planet.rotationPeriod = planet.revolutionPeriod * 365.25f * 24f; // Convert years to hours
            
            planet.orbitalEccentricity = CalculateEccentricity(planetPrng);
            CalculateRings(planet.className, planetPrng, out planet.hasRings, out planet.ringDivisions);

            // Generate MoonData structures
            planet.moons = GenerateMoons(planetSubSeedInput, planet.name, planet.radius, planet.mass, planetPrng);

            systemPlanets.Add(planet);

            // Print planet data
            string ringOutput = planet.hasRings ? $"Yes ({planet.ringDivisions})" : "No";
            Debug.Log($"-> {planet.name} | mass: {planet.mass:F2} ME | Dist: {planet.orbitalDistance:F2} AU | Rev Period: {planet.revolutionPeriod:F3} Yrs | Rot Period: {planet.rotationPeriod:F3} Hrs | Class: {planet.className} | Rad: {planet.radius:F2} RE | Rings: {ringOutput} | Moons: {planet.moons.Count}");
            
            // Print moon data
            foreach (MoonData moon in planet.moons)
            {
                string moonRingOutput = moon.hasRings ? $"Yes ({moon.ringDivisions})" : "No";
                Debug.Log($"   └─ {moon.name} | mass: {moon.mass:F5} ME | Dist: {moon.orbitalDistance:F2} LU | Ecc: {moon.orbitalEccentricity:F3} | Rad: {moon.radius:F2} RE | Rings: {moonRingOutput}");
            }
        }
    }

    /// <summary>
    /// Generates detailed data for moons orbiting a specific planet using hierarchical sub-seeding.
    /// </summary>
    /// <param name="planetSeedInput">The seed for generating the planet's moons.</param>
    /// <param name="planetName">The name of the host planet.</param>
    /// <param name="planetaryRadius">The radius of the host planet.</param>
    /// <param name="planetaryMass">The mass of the host planet.</param>
    /// <param name="planetPrng">The random number generator for the planet.</param>
    /// <returns>A list of generated moon data structures.</returns>
    private List<MoonData> GenerateMoons(string planetSeedInput, string planetName, float planetaryRadius, float planetaryMass, System.Random planetPrng)
    {
        List<MoonData> generatedMoons = new List<MoonData>();
        
        float scalingFactor = 3.0f;
        float maxTheoreticalMoons = planetaryRadius * scalingFactor;
        
        float meanMoons = maxTheoreticalMoons * 0.3f;
        float stdDevMoons = maxTheoreticalMoons * 0.2f;

        int moonCount = Mathf.RoundToInt(GetNormalValue(planetPrng, meanMoons, stdDevMoons));
        moonCount = Mathf.Clamp(moonCount, 0, Mathf.FloorToInt(maxTheoreticalMoons));

        for (int m = 0; m < moonCount; m++)
        {
            // Hierarchical Sub-Seeding: Planet Seed -> Moon Seed
            string moonSubSeedInput = planetSeedInput + $"_Moon_Entity_{m}";
            int moonNumericalSeed = DeriveNumericalSeed(moonSubSeedInput);
            System.Random moonPrng = new System.Random(moonNumericalSeed);

            MoonData moon = new MoonData();
            
            // Uses the ToAlphabet method from MarkovNameGenerator
            moon.name = planetName + "-" + nameGenerator.ToAlphabet(m);
            
            // Radius scaled relative to the host planet (roughly 10% to 25% of planet size)
            moon.radius = GetNormalValue(moonPrng, planetaryRadius * 0.15f, planetaryRadius * 0.05f);
            moon.radius = Mathf.Max(moon.radius, 0.01f); // Minimum safety bound

            // Moons are mostly rocky/icy, avg density 0.8 compared to Earth
            float moonDensity = Mathf.Max(GetNormalValue(moonPrng, 0.8f, 0.1f), 0.1f);
            moon.mass = Mathf.Pow(moon.radius, 3) * moonDensity;

            // Simple incremental orbital distance for moons (LU: Lunar Units placeholder)
            moon.orbitalDistance = (m + 1) * (moon.radius * 2f + planetaryRadius * 0.5f);

            // Kepler's Third Law applied to moons (Simulated proportional constant for LU to Days)
            float keplerConstant = 3.0f; 
            moon.revolutionPeriod = keplerConstant * Mathf.Sqrt(Mathf.Pow(moon.orbitalDistance, 3) / Mathf.Max(planetaryMass, 0.001f));

            // Tidal Locking. 85% of moons in our simulation become tidally locked.
            moon.isTidallyLocked = (moonPrng.NextDouble() <= 0.85);

            if (moon.isTidallyLocked)
            {
                // Rotation perfectly matches revolution (Days to Hours)
                moon.rotationPeriod = moon.revolutionPeriod * 24f;
            }
            else
            {
                // Unlocked anomalous rotation (e.g., newly captured asteroids)
                moon.rotationPeriod = Mathf.Max(GetNormalValue(moonPrng, 48f, 24f), 5f);
            }

            moon.orbitalEccentricity = CalculateEccentricity(moonPrng);
            
            // Moons are generally rocky bodies. We pass "Terrestrial" to give them the rare 4% ring anomaly.
            moon.className = "Rocky Moon";
            CalculateRings("Terrestrial", moonPrng, out moon.hasRings, out moon.ringDivisions);

            generatedMoons.Add(moon);
        }

        return generatedMoons;
    }

    /// <summary>
    /// Calculates orbital distance applying a Normal Distribution variance.
    /// Most planets will stay close to the Titius-Bode prediction.
    /// </summary>
    /// <param name="planetIndex">The index of the planet in the system.</param>
    /// <param name="prng">The isolated PRNG for this planet.</param>
    /// <returns>A float representing the orbital distance in AU.</returns>
    private float CalculateOrbitalDistance(int planetIndex, System.Random prng)
    {
        float baseDistance = (planetIndex > 0) ? (0.4f + 0.3f * Mathf.Pow(2, planetIndex - 1)) : 0.4f;

        // Apply Box-Muller jitter (Mean: 0, StdDev: 0.05)
        float varianceModifier = GetNormalValue(prng, 0f, 0.05f);
        varianceModifier = Mathf.Clamp(varianceModifier, -0.15f, 0.15f); 
        
        return baseDistance * (1f + varianceModifier);
    }

    /// <summary>
    /// Generic helper applying the Box-Muller transform to generate normally distributed variables.
    /// </summary>
    /// <param name="prng">The isolated PRNG for this planet.</param>
    /// <param name="mean">The mean value for the distribution.</param>
    /// <param name="stdDev">The standard deviation for the distribution.</param>
    /// <returns>A float value sampled from the specified normal distribution.</returns>
    private float GetNormalValue(System.Random prng, float mean, float stdDev)
    {
        double u1 = 1.0 - prng.NextDouble(); 
        double u2 = 1.0 - prng.NextDouble();
        double standardNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        
        return mean + stdDev * (float)standardNormal;
    }

    /// <summary>
    /// Helper for Roulette Wheel Selection on arrays of raw weights.
    /// </summary>
    /// <param name="weights">Array of weights for selection.</param>
    /// <param name="prng">The isolated PRNG for this selection.</param>
    /// <returns>The index of the selected weight.</returns>
    private int GetWeightedIndex(float[] weights, System.Random prng)
    {
        float totalWeight = 0f;
        foreach (float w in weights) totalWeight += w;

        float randomSpin = (float)(prng.NextDouble() * totalWeight);
        float cumulative = 0f;

        for (int i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];
            if (randomSpin <= cumulative) return i;
        }
        return weights.Length - 1;
    }

    /// <summary>
    /// Calculates whether a planet has rings and how many divisions it has based on its class and a probabilistic model.
    /// </summary>
    /// <param name="planetClass">The class of the planet.</param>
    /// <param name="prng">The isolated PRNG for this planet.</param>
    /// <param name="hasRings">Indicates whether the planet has rings.</param>
    /// <param name="ringCount">The number of ring divisions.</param>
    private void CalculateRings(string planetClass, System.Random prng, out bool hasRings, out int ringCount)
    {
        hasRings = false;
        ringCount = 0;
        double ringChance = prng.NextDouble();

        if (planetClass == "Gas Giant" || planetClass == "Ice Giant")
        {
            if (ringChance <= 0.85)
            {
                hasRings = true;
                // Normal Distribution for rings (Mean 3, StdDev 1)
                ringCount = Mathf.Clamp(Mathf.RoundToInt(GetNormalValue(prng, 3f, 1f)), 1, 6);
            }
        }
        else if (ringChance <= 0.04)
        {
            hasRings = true;
            ringCount = 1;
        }
    }

    /// <summary>
    /// Calculates the orbital eccentricity of a planet using a Normal Distribution.
    /// Most planets have near-circular orbits (e ~ 0), with high eccentricities being rare anomalies.
    /// </summary>
    /// <param name="prng">The isolated PRNG for this specific planet.</param>
    /// <returns>A float representing the orbital eccentricity, strictly clamped between 0.0 and 0.99.</returns>
    private float CalculateEccentricity(System.Random prng)
    {
        // A mean of 0.05 and stdDev of 0.08 models a realistic distribution where
        // most planets are circular (e < 0.1), but allows for rare, highly elliptical orbits.
        float meanEccentricity = 0.05f;
        float stdDevEccentricity = 0.08f;

        float eccentricity = GetNormalValue(prng, meanEccentricity, stdDevEccentricity);

        // Transform negative eccentricities to positive, as they are physically meaningless
        eccentricity = Mathf.Abs(eccentricity); 

        return Mathf.Clamp(eccentricity, 0f, 0.99f);
    }

    /// <summary>
    /// Maps an index to Harvard Spectral Classifications.
    /// </summary>
    /// <param name="index">The index of the spectral class.</param>
    /// <returns>The string representation of the spectral class.</returns>
    private string GetSpectralClassName(int index)
    {
        string[] classes = { "O (Blue)", "B (Blue-White)", "A (White)", "F (Yellow-White)", "G (Yellow - Solar)", "K (Orange)", "M (Red Dwarf)" };
        return classes[Mathf.Clamp(index, 0, classes.Length - 1)];
    }

    /// <summary>
    /// Executes a deterministic Roulette Wheel Selection to classify the planet.
    /// </summary>
    /// <param name="distance">The orbital distance of the planet.</param>
    /// <param name="prng">The isolated PRNG for this planet.</param>
    /// <param name="systemFrostLine">The Frost Line of the star system.</param>
    /// <returns>A PlanetProfile object representing the classified planetary taxonomy.</returns>
    private PlanetProfile ClassifyPlanet(float distance, System.Random prng, float systemFrostLine)
    {
        // Base profiles: Name, Mean Radius (RE), StdDev, Mean Density (Earth=1), Base Weight
        PlanetProfile terrestrial = new PlanetProfile("Terrestrial", 1.0f, 0.3f, 1.0f, 10f);
        PlanetProfile superEarth = new PlanetProfile("Super-Earth", 2.0f, 0.5f, 1.2f, 5f);
        PlanetProfile iceGiant = new PlanetProfile("Ice Giant", 4.0f, 1.0f, 0.3f, 2f);
        PlanetProfile gasGiant = new PlanetProfile("Gas Giant", 11.2f, 2.5f, 0.22f, 1f);

        if (distance > systemFrostLine) // Beyond the Frost Line
        {
            iceGiant.currentWeight += 15f;
            gasGiant.currentWeight += 20f;
            terrestrial.currentWeight = 2f; 
        }
        else // Inner System
        {
            terrestrial.currentWeight += 15f;
            superEarth.currentWeight += 10f;
            gasGiant.currentWeight += 1f; 
        }

        // Build the Roulette Wheel
        PlanetProfile[] profiles = { terrestrial, superEarth, iceGiant, gasGiant };
        float totalWeight = 0f;
        foreach (var p in profiles) totalWeight += p.currentWeight;

        // Spin the wheel deterministically using the planet's isolated PRNG
        float randomSpin = (float)(prng.NextDouble() * totalWeight);
        float cumulativeWeight = 0f;

        foreach (var p in profiles)
        {
            cumulativeWeight += p.currentWeight;
            if (randomSpin <= cumulativeWeight)
                return p;
        }

        return terrestrial; // Fallback for compiler safety
    }

    /// <summary>
    /// Cryptographically secure and stable string-to-integer conversion method.
    /// </summary>
    /// <param name="input">The input string to be hashed and converted.</param>
    /// <returns>An integer derived from the SHA256 hash of the input string.</returns>
    private int DeriveNumericalSeed(string input)
    {
        using (SHA256 sha256Hash = SHA256.Create())
        {
            byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToInt32(bytes, 0);
        }
    }
}