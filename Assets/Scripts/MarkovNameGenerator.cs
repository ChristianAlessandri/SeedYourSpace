using System.Collections.Generic;

/// <summary>
/// Deterministic Markov Chain generator for semantic entity naming.
/// Uses a predefined transition dictionary (Current Node -> List of possible Next Nodes).
/// </summary>
public class MarkovNameGenerator
{
    private readonly string[] startNodes;
    private readonly Dictionary<string, string[]> transitionMatrix;

    public MarkovNameGenerator()
    {
        // Initial state nodes (starting syllables)
        startNodes = new string[] { "Al", "Zen", "Cor", "Dra", "Vex", "Kael", "Sol", "Tyr", "Om", "Ly" };

        // Markov Chain transition matrix
        // Empty strings "" represent terminal states (end of the name)
        transitionMatrix = new Dictionary<string, string[]>()
        {
            { "Al", new string[] { "pha", "tair", "cor", "len" } },
            { "Zen", new string[] { "ith", "tar", "on", "ia" } },
            { "Cor", new string[] { "vus", "uscant", "ia", "on" } },
            { "Dra", new string[] { "con", "goth", "xis", "mus" } },
            { "Vex", new string[] { "ill", "on", "ar", "us" } },
            { "Kael", new string[] { "en", "ar", "thas", "ia" } },
            { "Sol", new string[] { "ar", "is", "stice", "en" } },
            { "Tyr", new string[] { "is", "an", "on", "ia" } },
            { "Om", new string[] { "ni", "icron", "eg", "ar" } },
            { "Ly", new string[] { "ra", "ris", "con", "th" } },

            // Intermediate nodes transitioning to final syllables or terminal states
            { "pha", new string[] { "ron", "lis", "" } },
            { "ia", new string[] { "n", "s", "" } },
            { "on", new string[] { "is", "ia", "" } },
            { "ar", new string[] { "is", "us", "" } },
            { "tar", new string[] { "is", "i", "" } },
            { "con", new string[] { "ar", "is", "" } }
        };
    }

    /// <summary>
    /// Traverses the Markov Chain to generate a word.
    /// </summary>
    /// <param name="prng">The deterministic PRNG instance.</param>
    /// <param name="maxSyllables">Maximum allowed depth of the chain.</param>
    /// <returns>A generated name string.</returns>
    public string GenerateSystemName(System.Random prng, int maxSyllables = 3)
    {
        string name = "";
        
        // Pick a starting node
        string currentSyllable = startNodes[prng.Next(startNodes.Length)];
        name += currentSyllable;

        // Traverse the chain dynamically
        for (int i = 1; i < maxSyllables; i++)
        {
            if (transitionMatrix.ContainsKey(currentSyllable))
            {
                string[] possibleNext = transitionMatrix[currentSyllable];
                currentSyllable = possibleNext[prng.Next(possibleNext.Length)];
                name += currentSyllable;
                
                if (string.IsNullOrEmpty(currentSyllable)) 
                    break; // Reached a terminal state
            }
            else
            {
                break; // No further transitions available
            }
        }

        return name;
    }

    /// <summary>
    /// Utility method to convert an integer to a Roman Numeral (1 to 12).
    /// </summary>
    /// <param name="number">The integer to convert.</param>
    /// <returns>The Roman Numeral string.</returns>
    public string ToRoman(int number)
    {
        string[] romanNumerals = { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII" };
        if (number > 0 && number < romanNumerals.Length) return romanNumerals[number];
        return number.ToString();
    }

    /// <summary>
    /// Utility method to convert an index to an alphabetical character (a, b, c, etc.).
    /// </summary>
    /// <param name="index">The index to convert.</param>
    /// <returns>The alphabetical character.</returns>
    public string ToAlphabet(int index)
    {
        return ((char)('a' + index)).ToString();
    }
}