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
    
    [Tooltip("Algorithm version for retrocompatibility and version control.")]
    public int algorithmVersion = 1;

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
    /// </summary>
    private void GenerateCentralStar(string baseSeed)
    {
        // Derive an isolated sub-seed for the star
        string starSubSeedInput = baseSeed + "_Star_Entity";
        int starNumericalSeed = DeriveNumericalSeed(starSubSeedInput);
        System.Random starPrng = new System.Random(starNumericalSeed);

        // Placeholder for Harvard Spectral Classification (O, B, A, F, G, K, M)
        int spectralIndex = starPrng.Next(0, 7);
        string spectralClass = GetSpectralClassName(spectralIndex);

        Debug.Log($"[Star Module] Sub-Seed Input: '{starSubSeedInput}' | Numerical: {starNumericalSeed}");
        Debug.Log($"[Star Module] Central Star Class: {spectralClass}");
    }

    /// <summary>
    /// Generates the orbital layout and individual planets using dedicated entity sub-seeds.
    /// </summary>
    private void GeneratePlanetarySystem(string baseSeed)
    {
        // Derive a sub-seed specifically for layout distribution
        string layoutSubSeedInput = baseSeed + "_Planets_Layout";
        int layoutNumericalSeed = DeriveNumericalSeed(layoutSubSeedInput);
        System.Random layoutPrng = new System.Random(layoutNumericalSeed);

        int planetCount = layoutPrng.Next(3, 9); // Generates between 3 and 8 planets
        Debug.Log($"[Layout Module] Total Planets Scheduled: {planetCount}");

        for (int i = 0; i < planetCount; i++)
        {
            // Derive an isolated sub-seed for each individual planet (Prevents cascading changes)
            string planetSubSeedInput = baseSeed + $"_Planet_Entity_{i}";
            int planetNumericalSeed = DeriveNumericalSeed(planetSubSeedInput);
            System.Random planetPrng = new System.Random(planetNumericalSeed);

            // Mock calculations for preliminary testing
            float simulatedDistance = (float)(planetPrng.NextDouble() * (100.0 - 10.0) + 10.0);
            float simulatedRadius = (float)(planetPrng.NextDouble() * (3.0 - 0.5) + 0.5);

            Debug.Log($"-> Planet [{i + 1}] | Sub-Seed: {planetNumericalSeed} | Distance: {simulatedDistance:F2} AU | Radius: {simulatedRadius:F2} RE");
        }
    }

    /// <summary>
    /// Cryptographically secure and stable string-to-integer conversion method 
    /// ensuring cross-platform determinism for the PRNG.
    /// </summary>
    private int DeriveNumericalSeed(string input)
    {
        using (SHA256 sha256Hash = SHA256.Create())
        {
            byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(input));
            // Convert the first 4 bytes of the cryptographic hash into a stable integer
            return BitConverter.ToInt32(bytes, 0);
        }
    }

    /// <summary>
    /// Maps an index to Harvard Spectral Classifications.
    /// </summary>
    private string GetSpectralClassName(int index)
    {
        string[] classes = { "O (Blue)", "B (Blue-White)", "A (White)", "F (Yellow-White)", "G (Yellow - Solar)", "K (Orange)", "M (Red Dwarf)" };
        return classes[Mathf.Clamp(index, 0, classes.Length - 1)];
    }
}