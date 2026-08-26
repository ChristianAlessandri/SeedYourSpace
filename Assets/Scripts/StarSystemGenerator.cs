using UnityEngine;
using System;
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
    public int algorithmVersion = 1; // Incremented version for the new statistical engine

    private MarkovNameGenerator nameGenerator;
    private float currentSystemFrostLine;

    private void Start()
    {
        GenerateCompleteStarSystem(masterSeed);
    }

    /// <summary>
    /// Generates a complete star system, including the central star and its planetary bodies, based on the provided seed.
    /// </summary>
    /// <param name="seed">The seed for the star system.</param>
    public void GenerateCompleteStarSystem(string seed)
    {
        Debug.Log($"=== STARTING STAR SYSTEM GENERATION (Algorithm v{algorithmVersion}) ===");
        
        // Load the JSON file dynamically from the Resources folder
        TextAsset jsonFile = Resources.Load<TextAsset>("markov_data");
        if (jsonFile == null)
        {
            Debug.LogError("Error: Markov data file not found! Ensure 'markov_data.json' is inside a 'Resources' folder.");
            return;
        }

        // Initialize the semantic generator with the loaded JSON
        nameGenerator = new MarkovNameGenerator(jsonFile.text);

        // Generate the root name for the entire system using the Master Seed
        System.Random systemPrng = new System.Random(DeriveNumericalSeed(seed));
        string rootSystemName = nameGenerator.GenerateSystemName(systemPrng);
        
        Debug.Log($"[Semantic Module] Root System Name: {rootSystemName}");

        GenerateCentralStar(seed, rootSystemName);
        GeneratePlanetarySystem(seed, rootSystemName);
    }

    /// <summary>
    /// Generates the central star using realistic astrophysics distributions.
    /// Red Dwarfs (M) are vastly more common than massive Blue (O) stars.
    /// </summary>
    /// <param name="baseSeed">The base seed for the star system.</param>
    /// <param name="rootName">The root name for the star system.</param>
    private void GenerateCentralStar(string baseSeed, string rootName)
    {
        string starSubSeedInput = baseSeed + "_Star_Entity";
        int starNumericalSeed = DeriveNumericalSeed(starSubSeedInput);
        System.Random starPrng = new System.Random(starNumericalSeed);

        // Stellar distribution weights reflecting actual universe demographics
        // O(Blue), B, A, F, G(Solar), K, M(Red Dwarf)
        float[] stellarWeights = { 0.1f, 1.0f, 2.0f, 4.0f, 8.0f, 15.0f, 70.0f };
        int spectralIndex = GetWeightedIndex(stellarWeights, starPrng);
        string spectralClass = GetSpectralClassName(spectralIndex);

        // Base Frost Line distances in AU
        float[] baseFrostLines = { 15.0f, 10.0f, 6.0f, 4.0f, 2.7f, 1.5f, 0.5f };
        float baseLine = baseFrostLines[spectralIndex];

        // Apply NORMAL DISTRIBUTION to the Frost Line oscillation (Mean: 0, StdDev: 0.05)
        float oscillation = GetNormalValue(starPrng, 0f, 0.05f);
        oscillation = Mathf.Clamp(oscillation, -0.20f, 0.20f); // Cap extreme outliers
        currentSystemFrostLine = baseLine * (1f + oscillation);

        string starName = rootName + " Prime";
        Debug.Log($"[Star Module] Name: {starName} | Class: {spectralClass} | Frost Line: {currentSystemFrostLine:F2} AU");
    }

    /// <summary>
    /// Generates the planetary system based on the central star's properties and the provided seed.
    /// </summary>
    /// <param name="baseSeed">The base seed for the planetary system.</param>
    /// <param name="rootName">The root name for the star system.</param>
    private void GeneratePlanetarySystem(string baseSeed, string rootName)
    {
        string layoutSubSeedInput = baseSeed + "_Planets_Layout";
        int layoutNumericalSeed = DeriveNumericalSeed(layoutSubSeedInput);
        System.Random layoutPrng = new System.Random(layoutNumericalSeed);

        // Apply NORMAL DISTRIBUTION for planet count (Mean: 5.5, StdDev: 2.0)
        float rawPlanetCount = GetNormalValue(layoutPrng, 5.5f, 2.0f);
        int planetCount = Mathf.Clamp(Mathf.RoundToInt(rawPlanetCount), 1, 12);
        
        Debug.Log($"[Layout Module] Total Planets Scheduled: {planetCount}");

        for (int i = 0; i < planetCount; i++)
        {
            string planetSubSeedInput = baseSeed + $"_Planet_Entity_{i}";
            int planetNumericalSeed = DeriveNumericalSeed(planetSubSeedInput);
            System.Random planetPrng = new System.Random(planetNumericalSeed);

            float orbitalDistance = CalculateOrbitalDistance(i, planetPrng);
            PlanetProfile selectedClass = ClassifyPlanet(orbitalDistance, planetPrng, currentSystemFrostLine);
            
            // Planetary radius uses Box-Muller via the generic helper now
            float planetaryRadius = GetNormalValue(planetPrng, selectedClass.radiusMean, selectedClass.radiusStdDev);
            planetaryRadius = Mathf.Max(planetaryRadius, 0.1f); // Minimum size safety

            float orbitalEccentricity = CalculateEccentricity(planetPrng);

            // Generate semantic name for the planet
            string planetName = rootName + " " + nameGenerator.ToRoman(i + 1);

            CalculateRings(selectedClass.className, planetPrng, out bool hasRings, out int ringDivisions);
            int moonCount = CalculateMoons(planetaryRadius, planetPrng);

            string ringData = hasRings ? $"Yes ({ringDivisions})" : "No";
            
            // Print planet data
            Debug.Log($"-> {planetName} | Dist: {orbitalDistance:F2} AU | Ecc: {orbitalEccentricity:F3} | Class: {selectedClass.className} | Rad: {planetaryRadius:F2} RE | Rings: {ringData} | Moons: {moonCount}");
        }
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
    /// Executes a deterministic Roulette Wheel Selection to classify the planet.
    /// </summary>
    /// <param name="planetaryRadius">The radius of the planet.</param>
    /// <param name="prng">The isolated PRNG for this planet.</param>
    /// <returns>An integer representing the number of moons for the planet.</returns>
    private int CalculateMoons(float planetaryRadius, System.Random prng)
    {
        float scalingFactor = 3.0f;
        float maxTheoreticalMoons = planetaryRadius * scalingFactor;
        
        // Normal Distribution centered around a logical mean (e.g. 30% of theoretical max)
        float meanMoons = maxTheoreticalMoons * 0.3f;
        float stdDevMoons = maxTheoreticalMoons * 0.2f;

        int moonCount = Mathf.RoundToInt(GetNormalValue(prng, meanMoons, stdDevMoons));
        
        return Mathf.Clamp(moonCount, 0, Mathf.FloorToInt(maxTheoreticalMoons));
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
        // Base profiles: Name, Mean Radius (RE), StdDev, Base Weight
        PlanetProfile terrestrial = new PlanetProfile("Terrestrial", 1.0f, 0.3f, 10f);
        PlanetProfile superEarth = new PlanetProfile("Super-Earth", 2.0f, 0.5f, 5f);
        PlanetProfile iceGiant = new PlanetProfile("Ice Giant", 4.0f, 1.0f, 2f);
        PlanetProfile gasGiant = new PlanetProfile("Gas Giant", 11.2f, 2.5f, 1f); // Jupiter size baseline

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