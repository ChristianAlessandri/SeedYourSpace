using System;
using System.Text;
using System.Security.Cryptography;

public class StochasticMathV1 : IStochasticMath
{
    public float GetNormalValue(System.Random prng, float mean, float stdDev)
    {
        double u1 = 1.0 - prng.NextDouble(); 
        double u2 = 1.0 - prng.NextDouble();
        double standardNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        
        return mean + stdDev * (float)standardNormal;
    }

    public int GetWeightedIndex(float[] weights, System.Random prng)
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

    public int DeriveNumericalSeed(string input)
    {
        using (SHA256 sha256Hash = SHA256.Create())
        {
            byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToInt32(bytes, 0);
        }
    }
}