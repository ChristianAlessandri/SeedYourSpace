using System;
using System.Text;
using System.Security.Cryptography;

/// <summary>
/// Mathematical and cryptographic library for deterministic procedural generation.
/// </summary>
public static class StochasticMath
{
    /// <summary>
    /// Generic helper applying the Box-Muller transform to generate normally distributed variables.
    /// </summary>
    /// <param name="prng">A System.Random instance for generating uniform random numbers.</param>
    /// <param name="mean">The mean (average) of the desired normal distribution.</param>
    /// <param name="stdDev">The standard deviation of the desired normal distribution.</param>
    /// <returns>A float value sampled from the specified normal distribution.</returns>
    public static float GetNormalValue(System.Random prng, float mean, float stdDev)
    {
        double u1 = 1.0 - prng.NextDouble(); 
        double u2 = 1.0 - prng.NextDouble();
        double standardNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        
        return mean + stdDev * (float)standardNormal;
    }

    /// <summary>
    /// Helper for Roulette Wheel Selection on arrays of raw weights.
    /// </summary>
    /// <param name="weights">An array of raw weights for each option.</param>
    /// <param name="prng">A System.Random instance for generating uniform random numbers.</param>
    /// <returns>The index of the selected option based on the weighted probabilities.</returns>
    public static int GetWeightedIndex(float[] weights, System.Random prng)
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
    /// Cryptographically secure and stable string-to-integer conversion method.
    /// </summary>
    /// <param name="input">The input string to convert.</param>
    /// <returns>The derived numerical seed.</returns>
    public static int DeriveNumericalSeed(string input)
    {
        using (SHA256 sha256Hash = SHA256.Create())
        {
            byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToInt32(bytes, 0);
        }
    }
}