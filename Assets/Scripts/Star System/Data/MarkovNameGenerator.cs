using System.Collections.Generic;
using UnityEngine;

// Data wrappers required by Unity's JsonUtility to parse nested objects
[System.Serializable]
public class MarkovData
{
    public string[] startNodes;
    public MarkovTransition[] transitions;
}

[System.Serializable]
public struct MarkovTransition
{
    public string node;
    public string[] nextNodes;
}

/// <summary>
/// Deterministic Markov Chain generator for semantic entity naming.
/// Loads probabilities dynamically from a JSON file.
/// </summary>
public class MarkovNameGenerator
{
    private string[] startNodes;
    private Dictionary<string, string[]> transitionMatrix;

    /// <summary>
    /// Initializes the generator by parsing the provided JSON string.
    /// </summary>
    /// <param name="jsonContent">The raw JSON data.</param>
    /// <returns>A MarkovNameGenerator instance ready to generate names.</returns>
    public MarkovNameGenerator(string jsonContent)
    {
        // Parse the JSON into our wrapper class
        MarkovData data = JsonUtility.FromJson<MarkovData>(jsonContent);
        
        startNodes = data.startNodes;
        transitionMatrix = new Dictionary<string, string[]>();

        // Reconstruct the C# Dictionary from the parsed JSON array
        foreach (var transition in data.transitions)
        {
            transitionMatrix.Add(transition.node, transition.nextNodes);
        }
    }

    /// <summary>
    /// Traverses the Markov Chain to generate a word.
    /// </summary>
    /// <param name="prng">A seeded pseudo-random number generator for deterministic output.</param>
    /// <param name="maxSyllables">The maximum number of syllables to generate</param>
    /// <returns>The generated system name.</returns>
    public string GenerateSystemName(System.Random prng, int maxSyllables = 3)
    {
        string name = "";
        string currentSyllable = startNodes[prng.Next(startNodes.Length)];
        name += currentSyllable;

        for (int i = 1; i < maxSyllables; i++)
        {
            if (transitionMatrix.ContainsKey(currentSyllable))
            {
                string[] possibleNext = transitionMatrix[currentSyllable];
                currentSyllable = possibleNext[prng.Next(possibleNext.Length)];
                name += currentSyllable;
                
                if (string.IsNullOrEmpty(currentSyllable)) 
                    break; 
            }
            else
            {
                break; 
            }
        }

        return name;
    }

    /// <summary>
    /// Converts a number to its Roman numeral representation.
    /// </summary>
    /// <param name="number">The number to convert.</param>
    /// <returns>The Roman numeral string.</returns>
    public string ToRoman(int number)
    {
        string[] romanNumerals = { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII" };
        if (number > 0 && number < romanNumerals.Length) return romanNumerals[number];
        return number.ToString();
    }

    /// <summary>
    /// Converts a number to its alphabetical representation.
    /// </summary>
    /// <param name="index">The index of the letter (0-25).</param>
    /// <returns>The alphabetical string.</returns>
    public string ToAlphabet(int index)
    {
        return ((char)('a' + index)).ToString();
    }
}