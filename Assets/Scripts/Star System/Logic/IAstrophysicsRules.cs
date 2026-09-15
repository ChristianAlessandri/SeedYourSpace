using UnityEngine;
using System;

[Serializable]
public class GenerationData
{
    public float[] stellarWeights;
    public float[] massMeans;
    public float[] tempMeans;
    public float[] radiusMeans;
    public float[] baseFrostLines;
    public string[] spectralClasses;
}

public interface IAstrophysicsRules
{
    void Initialize(GenerationData data);
    string GetSpectralClassName(int index);
    void CalculateStellarSurface(float temperature, float mass, float radius, float rotationPeriod, System.Random prng, out Color baseColor, out float magneticActivity, out float granulationScale);
    float CalculateOrbitalDistance(int planetIndex, System.Random prng);
    PlanetProfile ClassifyPlanet(float distance, System.Random prng, float systemFrostLine);
    string DetermineAtmosphere(string className, float gravity, float distance, float frostLine, System.Random prng);
    void CalculateAtmosphereVisuals(string atmosphereType, out Color atmosColor, out Color cloudColor, out float atmosScale);
    void CalculatePlanetVisuals(string className, float temperature, string atmosphere, System.Random prng, out Color baseColor, out Color secondaryColor, out float hydrofraction, out float cloudCoverage);
    float CalculateEccentricity(System.Random prng);
    void CalculateRings(string planetClass, float bodyRadius, System.Random prng, out bool hasRings, out int ringCount, out float innerRadius, out float outerRadius, out Color ringColor);
    string ClassifyMoon(float planetDistance, float systemFrostLine, System.Random prng);
}