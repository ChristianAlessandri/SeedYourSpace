using UnityEngine;
using System;

public class StarSystemGenerator : MonoBehaviour
{
    [Header("Test Seed")]
    public string hexSeed = "0xF5a9b2323e7f1C0C40843B33E7cEB2Ef4caAB895";

    // Using System.Random for deterministic random number generation
    private System.Random prng;

    void Start()
    {
        GenerateStarSystem(hexSeed);
    }

    public void GenerateStarSystem(string seed)
    {
        // Converts the hex seed to a numerical seed for the PRNG
        int numericalSeed = GetStableHash(seed);
        
        // Initialize the PRNG with the numerical seed
        prng = new System.Random(numericalSeed);

        Debug.Log($"--- STAR SYSTEM GENERATION ---");
        Debug.Log($"Original Seed: {seed} | Numerical Seed: {numericalSeed}");

        // Generate the central star type
        int starTypeIndex = prng.Next(1, 5); // Es. 1: Nana Rossa, 2: Gialla, 3: Gigante Blu, 4: Nana Bianca
        Debug.Log($"Central Star Generated: Type {starTypeIndex}");

        // Generate the number of planets
        int planetCount = prng.Next(3, 9); // Between 3 and 8 planets
        Debug.Log($"Planets in orbit: {planetCount}");

        // Generate each planet's properties
        for (int i = 0; i < planetCount; i++)
        {
            // Generate a float for the distance (between 10.0 and 100.0)
            float distance = (float)(prng.NextDouble() * (100.0 - 10.0) + 10.0);
            
            // Generate a float for the radius (between 0.5 and 3.0)
            float radius = (float)(prng.NextDouble() * (3.0 - 0.5) + 0.5);

            Debug.Log($"Planet {i+1} | Distance: {distance:F2} | Radius: {radius:F2}");
        }
    }

    // Custom hash function to convert a string into a stable integer seed to avoid the PRNG.
    // This ensures that the same string always produces the same integer.
    private int GetStableHash(string text)
    {
        unchecked // Disable overflow checking for the hash calculation
        {
            int hash = 23;
            foreach (char c in text)
            {
                hash = hash * 31 + c;
            }
            return hash;
        }
    }
}