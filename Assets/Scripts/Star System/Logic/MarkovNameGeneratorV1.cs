using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MarkovData
{
    public string[] startNodes;
    public MarkovTransition[] transitions;
    public string[] romanNumerals;
}

[System.Serializable]
public struct MarkovTransition
{
    public string node;
    public string[] nextNodes;
}

public class MarkovNameGeneratorV1 : INameGenerator
{
    private string[] startNodes;
    private string[] romanNumerals;
    private Dictionary<string, string[]> transitionMatrix;

    public MarkovNameGeneratorV1(string jsonContent)
    {
        MarkovData data = JsonUtility.FromJson<MarkovData>(jsonContent);
        
        startNodes = data.startNodes;
        romanNumerals = data.romanNumerals;
        transitionMatrix = new Dictionary<string, string[]>();

        foreach (var transition in data.transitions)
        {
            transitionMatrix.Add(transition.node, transition.nextNodes);
        }
    }

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

    public string ToRoman(int number)
{
    // Convert 1-based number to 0-based array index
    int index = number - 1;

    // Fallback if JSON doesn't provide them or index is out of bounds
    if (romanNumerals != null && index >= 0 && index < romanNumerals.Length) 
        return romanNumerals[index];
        
    return number.ToString();
}

    public string ToAlphabet(int index)
    {
        return ((char)('a' + index)).ToString();
    }
}