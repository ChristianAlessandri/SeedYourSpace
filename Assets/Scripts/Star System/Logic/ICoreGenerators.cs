using System;

public interface INameGenerator
{
    string GenerateSystemName(System.Random prng, int maxSyllables = 3);
    string ToRoman(int number);
    string ToAlphabet(int index);
}

public interface IStochasticMath
{
    float GetNormalValue(System.Random prng, float mean, float stdDev);
    int GetWeightedIndex(float[] weights, System.Random prng);
    int DeriveNumericalSeed(string input);
}