using UnityEngine;
using System;
using System.Text;
using System.Security.Cryptography;

/// <summary>
/// Core procedural generator responsible for deterministic star system generation 
/// based on cryptographic sub-seeding and a Separation of Concerns architecture.
/// </summary>
public class StarSystemGenerator : MonoBehaviour
{
    [Header("Generation Settings")]
    [Tooltip("The Master Seed derived from the blockchain (On-Chain VRF layer).")]
    public string masterSeed = "0xF5a9b2323e7f1C0C40843B33E7cEB2Ef4caAB895";
    
    [HideInInspector]
    public int algorithmVersion = 1; // Algorithm versioning for future-proofing and backward compatibility

    private float currentSystemFrostLine;

    private void Start()
    {
        GenerateCompleteStarSystem(masterSeed);
    }

    /// <summary>
    /// Executes the full generation pipeline for the star system.
    /// </summary>
    /// <param name="seed">The incoming master seed string.</param>
    public void GenerateCompleteStarSystem(string seed)
    {
        Debug.Log($"=== STARTING STAR SYSTEM GENERATION (Algorithm v{algorithmVersion}) ===");
        Debug.Log($"Master Seed: {seed}");

        // Central Star Generation via Sub-Seeding
        GenerateCentralStar(seed);

        // Orbital Layout and Planetary Taxonomy via Sub-Seeding
        GeneratePlanetarySystem(seed);
    }

    /// <summary>
    /// Generates the central star parameters using an isolated cryptographic sub-seed.
    /// Calculates the dynamic Frost Line based on the stellar class.
    /// </summary>
    /// <param name="baseSeed">The master seed for the star system.</param>
    private void GenerateCentralStar(string baseSeed)
    {
        string starSubSeedInput = baseSeed + "_Star_Entity";
        int starNumericalSeed = DeriveNumericalSeed(starSubSeedInput);
        System.Random starPrng = new System.Random(starNumericalSeed);

        // 0: O, 1: B, 2: A, 3: F, 4: G(Solar), 5: K, 6: M(Red Dwarf)
        int spectralIndex = starPrng.Next(0, 7);
        string spectralClass = GetSpectralClassName(spectralIndex);

        // Base Frost Line distances in AU based on standard astrophysics estimates
        float[] baseFrostLines = { 15.0f, 10.0f, 6.0f, 4.0f, 2.7f, 1.5f, 0.5f };
        float baseLine = baseFrostLines[spectralIndex];

        // Apply a deterministic oscillation/jitter between -15% and +15%
        float oscillation = (float)(starPrng.NextDouble() * 0.30 - 0.15);
        currentSystemFrostLine = baseLine * (1f + oscillation);

        Debug.Log($"[Star Module] Class: {spectralClass} | Dynamic Frost Line: {currentSystemFrostLine:F2} AU");
    }

    /// <summary>
    /// Generates the orbital layout and individual planets using dedicated entity sub-seeds.
    /// </summary>
    /// <param name="baseSeed">The master seed for the star system.</param>
    private void GeneratePlanetarySystem(string baseSeed)
    {
        string layoutSubSeedInput = baseSeed + "_Planets_Layout";
        int layoutNumericalSeed = DeriveNumericalSeed(layoutSubSeedInput);
        System.Random layoutPrng = new System.Random(layoutNumericalSeed);

        int planetCount = layoutPrng.Next(3, 9); // Generates between 3 and 8 planets
        Debug.Log($"[Layout Module] Total Planets Scheduled: {planetCount}");

        for (int i = 0; i < planetCount; i++)
        {
            string planetSubSeedInput = baseSeed + $"_Planet_Entity_{i}";
            int planetNumericalSeed = DeriveNumericalSeed(planetSubSeedInput);
            System.Random planetPrng = new System.Random(planetNumericalSeed);

            // Calculate Spatial Layout
            float orbitalDistance = CalculateOrbitalDistance(i, planetPrng);
            
            // Determine Taxonomy based on distance
            PlanetProfile selectedClass = ClassifyPlanet(orbitalDistance, planetPrng, currentSystemFrostLine);

            // Calculate Physical Constraints via Box-Muller using the class parameters
            float planetaryRadius = CalculatePlanetaryRadius(
                planetPrng, 
                selectedClass.radiusMean, 
                selectedClass.radiusStdDev
            );

            // Calculate Ring System anomalies
            bool hasRings;
            int ringDivisions;
            CalculateRings(selectedClass.className, planetPrng, out hasRings, out ringDivisions);

            // Calculate Satellite generation scaled by planetary radius
            int moonCount = CalculateMoons(planetaryRadius, planetPrng);

            // Format the final output string for the console
            string ringData = hasRings ? $"Yes ({ringDivisions})" : "No";

            Debug.Log($"-> Planet [{i + 1}] | Dist: {orbitalDistance:F2} AU | Class: {selectedClass.className} | Rad: {planetaryRadius:F2} RE | Rings: {ringData} | Moons: {moonCount}");
        }
    }

    /// <summary>
    /// Calculates the orbital distance using an adapted Titius-Bode law.
    /// Incorporates deterministic PRNG noise to ensure unique but physically safe distributions.
    /// </summary>
    /// <param name="planetIndex">The index of the planet in the system.</param>
    /// <param name="prng">The isolated PRNG for this planet.</param>
    private float CalculateOrbitalDistance(int planetIndex, System.Random prng)
    {
        // Classic Titius-Bode formula base: a = 0.4 + 0.3 * 2^n
        // For index 0, n is usually treated as negative infinity (0.3 * 0 = 0)
        float baseDistance = 0.4f;
        if (planetIndex > 0)
        {
            baseDistance = 0.4f + 0.3f * Mathf.Pow(2, planetIndex - 1);
        }

        // Generate a deterministic variance between -15% and +15% using the planet's isolated PRNG
        float varianceModifier = (float)(prng.NextDouble() * 0.30 - 0.15); // Range: [-0.15, 0.15]
        
        // Apply variance to the base distance
        float finalDistance = baseDistance * (1f + varianceModifier);
        
        return finalDistance;
    }

    /// <summary>
    /// Generates a normally distributed value using the Box-Muller transform.
    /// Perfect for calculating physical attributes like Mass and Radius.
    /// </summary>
    /// <param name="prng">The isolated PRNG for this planet.</param>
    /// <param name="mean">The mean value for the distribution.</param>
    /// <param name="stdDev">The standard deviation for the distribution.</param>
    private float CalculatePlanetaryRadius(System.Random prng, float mean, float stdDev)
    {
        // U1 must be strictly greater than 0 to avoid Math.Log(0) error
        double u1 = 1.0 - prng.NextDouble(); 
        double u2 = 1.0 - prng.NextDouble();

        // Box-Muller Transformation Formula
        double standardNormalDistribution = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);

        // Shift the standard normal distribution to our desired mean and standard deviation
        float generatedRadius = mean + stdDev * (float)standardNormalDistribution;

        // Clamp the radius to prevent physically impossible negative or microscopic planets
        return Mathf.Max(generatedRadius, 0.1f);
    }

    /// <summary>
    /// Cryptographically secure and stable string-to-integer conversion method.
    /// </summary>
    /// <param name="input">The input string to be hashed and converted.</param>
    private int DeriveNumericalSeed(string input)
    {
        using (SHA256 sha256Hash = SHA256.Create())
        {
            byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToInt32(bytes, 0);
        }
    }

    /// <summary>
    /// Maps an index to Harvard Spectral Classifications.
    /// </summary>
    /// <param name="index">The index of the spectral class.</param>
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
    PlanetProfile ClassifyPlanet(float distance, System.Random prng, float systemFrostLine)
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
    /// Calculates the probability of a planet having a ring system based on its taxonomy.
    /// Gas and Ice Giants have a very high probability, while rocky planets have a rare anomaly chance.
    /// </summary>
    /// <param name="planetClass">The assigned taxonomy class of the planet.</param>
    /// <param name="prng">The isolated PRNG for this specific planet.</param>
    /// <param name="hasRings">Output boolean indicating if rings are present.</param>
    /// <param name="ringCount">Output integer indicating the number of major ring divisions.</param>
    private void CalculateRings(string planetClass, System.Random prng, out bool hasRings, out int ringCount)
    {
        hasRings = false;
        ringCount = 0;
        
        // Extract a uniform probability float between 0.0 and 1.0
        double ringChance = prng.NextDouble();

        if (planetClass == "Gas Giant" || planetClass == "Ice Giant")
        {
            // 85% chance for massive gaseous bodies to develop ring systems
            if (ringChance <= 0.85)
            {
                hasRings = true;
                ringCount = prng.Next(1, 5); // Generates between 1 and 4 distinct ring divisions
            }
        }
        else // Terrestrial or Super-Earth
        {
            // 4% anomaly chance for rocky planets (e.g., destroyed moon debris)
            if (ringChance <= 0.04)
            {
                hasRings = true;
                ringCount = 1; // Usually just a single faint debris ring
            }
        }
    }

    /// <summary>
    /// Calculates the number of moons orbiting the planet.
    /// The maximum possible number of moons scales linearly with the planet's radius 
    /// (acting as a proxy for the Hill sphere and gravitational mass).
    /// </summary>
    /// <param name="planetaryRadius">The generated radius of the planet in Earth Radii (RE).</param>
    /// <param name="prng">The isolated PRNG for this specific planet.</param>
    /// <returns>An integer representing the number of moons.</returns>
    private int CalculateMoons(float planetaryRadius, System.Random prng)
    {
        // Determine the theoretical maximum number of moons.
        // A scaling factor of 3.0 means a massive 11 RE Gas Giant could have up to 33 major moons,
        // while a 1 RE Terrestrial planet is capped at 3.
        float scalingFactor = 3.0f;
        int maxMoons = Mathf.FloorToInt(planetaryRadius * scalingFactor);

        // Generate a deterministic number between 0 and the calculated maximum.
        // The upper bound is exclusive in System.Random.Next, so we add 1.
        return prng.Next(0, maxMoons + 1);
    }
}