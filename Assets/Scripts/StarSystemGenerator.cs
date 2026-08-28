using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Core procedural orchestrator responsible for deterministic star system generation.
/// Delegates complex calculations to StochasticMath and AstrophysicsRules.
/// </summary>
public class StarSystemGenerator : MonoBehaviour
{
    [Header("Generation Settings")]
    public string masterSeed = "0xF5a9b2323e7f1C0C40843B33E7cEB2Ef4caAB895";

    [Header("Diorama Settings")]
    public VisualDioramaBuilder dioramaBuilder;
    
    [HideInInspector]
    public int algorithmVersion = 1;

    private MarkovNameGenerator nameGenerator;
    private float currentSystemFrostLine;

    private void Start()
    {
        GenerateCompleteStarSystem(masterSeed);
    }

    /// <summary>
    /// Generates a complete star system based on the provided seed.
    /// </summary>
    /// <param name="seed">Seed for deterministic generation.</param>
    public void GenerateCompleteStarSystem(string seed)
    {
        Debug.Log($"=== STARTING STAR SYSTEM GENERATION (Algorithm v{algorithmVersion}) ===");
        
        TextAsset jsonFile = Resources.Load<TextAsset>("markov_data");
        if (jsonFile == null)
        {
            Debug.LogError("Error: Markov data file not found!");
            return;
        }

        nameGenerator = new MarkovNameGenerator(jsonFile.text);
        System.Random systemPrng = new System.Random(StochasticMath.DeriveNumericalSeed(seed));
        string rootSystemName = nameGenerator.GenerateSystemName(systemPrng);
        
        Debug.Log($"[Semantic Module] Root System Name: {rootSystemName}");

        StarData centralStar = GenerateCentralStar(seed, rootSystemName);
        GeneratePlanetarySystem(seed, rootSystemName, centralStar);
    }

    /// <summary>
    /// Generates the central star of the system based on the provided seed and root name.
    /// </summary>
    /// <param name="baseSeed">Seed for deterministic generation.</param>
    /// <param name="rootName">The root name for the system.</param>
    /// <returns>The generated central star data.</returns>
    private StarData GenerateCentralStar(string baseSeed, string rootName)
    {
        string starSubSeedInput = baseSeed + "_Star_Entity";
        int starNumericalSeed = StochasticMath.DeriveNumericalSeed(starSubSeedInput);
        System.Random starPrng = new System.Random(starNumericalSeed);

        float[] stellarWeights = { 0.1f, 1.0f, 2.0f, 4.0f, 8.0f, 15.0f, 70.0f };
        int spectralIndex = StochasticMath.GetWeightedIndex(stellarWeights, starPrng);
        
        float[] massMeans = { 40.0f, 6.0f, 2.0f, 1.3f, 1.0f, 0.7f, 0.3f };
        float[] tempMeans = { 35000f, 15000f, 8500f, 6500f, 5500f, 4500f, 3000f };
        float[] radiusMeans = { 15.0f, 4.0f, 1.7f, 1.3f, 1.0f, 0.8f, 0.3f };

        StarData star = new StarData();
        star.name = rootName + " Prime";
        star.spectralClass = AstrophysicsRules.GetSpectralClassName(spectralIndex);
        
        star.mass = Mathf.Max(StochasticMath.GetNormalValue(starPrng, massMeans[spectralIndex], massMeans[spectralIndex] * 0.1f), 0.08f);
        star.temperature = Mathf.Max(StochasticMath.GetNormalValue(starPrng, tempMeans[spectralIndex], tempMeans[spectralIndex] * 0.05f), 2000f);
        star.radius = Mathf.Max(StochasticMath.GetNormalValue(starPrng, radiusMeans[spectralIndex], radiusMeans[spectralIndex] * 0.1f), 0.1f);

        float[] baseFrostLines = { 15.0f, 10.0f, 6.0f, 4.0f, 2.7f, 1.5f, 0.5f };
        float oscillation = Mathf.Clamp(StochasticMath.GetNormalValue(starPrng, 0f, 0.05f), -0.20f, 0.20f);
        
        currentSystemFrostLine = baseFrostLines[spectralIndex] * (1f + oscillation);
        star.frostLine = currentSystemFrostLine;

        Debug.Log($"[Star Module] {star.name} | Class: {star.spectralClass} | Mass: {star.mass:F2} SM | Temp: {Mathf.RoundToInt(star.temperature)} K | Frost Line: {star.frostLine:F2} AU");
        
        return star;
    }

    /// <summary>
    /// Generates the planetary system around the central star based on the provided seed and root name.
    /// </summary>
    /// <param name="baseSeed">Seed for deterministic generation.</param>
    /// <param name="rootName">The root name for the system.</param>
    /// <param name="centralStar">The central star data.</param>
    /// <returns>List of generated planetary data.</returns>
    private void GeneratePlanetarySystem(string baseSeed, string rootName, StarData centralStar)
    {
        string layoutSubSeedInput = baseSeed + "_Planets_Layout";
        int layoutNumericalSeed = StochasticMath.DeriveNumericalSeed(layoutSubSeedInput);
        System.Random layoutPrng = new System.Random(layoutNumericalSeed);

        float rawPlanetCount = StochasticMath.GetNormalValue(layoutPrng, 5.5f, 2.0f);
        int planetCount = Mathf.Clamp(Mathf.RoundToInt(rawPlanetCount), 1, 12);
        
        Debug.Log($"[Layout Module] Total Planets Scheduled: {planetCount}");

        List<PlanetData> systemPlanets = new List<PlanetData>();

        for (int i = 0; i < planetCount; i++)
        {
            string planetSubSeedInput = baseSeed + $"_Planet_Entity_{i}";
            int planetNumericalSeed = StochasticMath.DeriveNumericalSeed(planetSubSeedInput);
            System.Random planetPrng = new System.Random(planetNumericalSeed);

            PlanetData planet = new PlanetData();
            planet.name = rootName + " " + nameGenerator.ToRoman(i + 1);
            planet.orbitalDistance = AstrophysicsRules.CalculateOrbitalDistance(i, planetPrng);

            PlanetProfile selectedClass = AstrophysicsRules.ClassifyPlanet(planet.orbitalDistance, planetPrng, currentSystemFrostLine);
            planet.className = selectedClass.className;
            
            planet.radius = Mathf.Max(StochasticMath.GetNormalValue(planetPrng, selectedClass.radiusMean, selectedClass.radiusStdDev), 0.1f);
            float density = Mathf.Max(StochasticMath.GetNormalValue(planetPrng, selectedClass.densityMean, 0.1f), 0.1f);
            planet.mass = Mathf.Pow(planet.radius, 3) * density;
            planet.surfaceGravity = planet.mass / (planet.radius * planet.radius);

            // Simplified Equilibrium Temperature
            // Assumes average albedo (reflectivity) of 0.3
            float distanceInSolarRadii = planet.orbitalDistance * 215.03f; // 1 AU = ~215 Solar Radii
            planet.surfaceTemperature = centralStar.temperature * Mathf.Sqrt(centralStar.radius / (2f * distanceInSolarRadii)) * 0.9f;
            
            planet.axialTilt = Mathf.Abs(StochasticMath.GetNormalValue(planetPrng, 23.5f, 15f));
            planet.orbitalInclination = StochasticMath.GetNormalValue(planetPrng, 0f, 3f);
            planet.atmosphereType = AstrophysicsRules.DetermineAtmosphere(planet.className, planet.surfaceGravity, planet.orbitalDistance, currentSystemFrostLine, planetPrng);

            planet.revolutionPeriod = Mathf.Sqrt(Mathf.Pow(planet.orbitalDistance, 3) / centralStar.mass);
            
            float baseRotation = (planet.className == "Gas Giant" || planet.className == "Ice Giant") ? 12f : 24f;
            planet.rotationPeriod = Mathf.Max(StochasticMath.GetNormalValue(planetPrng, baseRotation, baseRotation * 0.5f), 2f); 
            
            bool lockedToStar = (planet.orbitalDistance < 0.2f);
            if (lockedToStar) 
            {
                planet.rotationPeriod = planet.revolutionPeriod * 365.25f * 24f; 
            }
            
            planet.orbitalEccentricity = AstrophysicsRules.CalculateEccentricity(planetPrng);
            AstrophysicsRules.CalculateRings(planet.className, planetPrng, out planet.hasRings, out planet.ringDivisions);

            planet.moons = GenerateMoons(planetSubSeedInput, planet.name, planet.radius, planet.mass, planet.surfaceTemperature, planet.orbitalDistance, planetPrng);
            systemPlanets.Add(planet);

            string ringOutput = planet.hasRings ? $"Yes ({planet.ringDivisions})" : "No";
            Debug.Log($"-> {planet.name} | mass: {planet.mass:F2} ME | Dist: {planet.orbitalDistance:F2} AU | Class: {planet.className} | Rad: {planet.radius:F2} RE | Atmos: {planet.atmosphereType} | Rings: {ringOutput} | Moons: {planet.moons.Count}");
        }

        if (dioramaBuilder != null)
        {
            dioramaBuilder.BuildUniverse(centralStar, systemPlanets);
        }
        else
        {
            Debug.LogWarning("Warning: Diorama Builder is not assigned. Visual representation will not be generated.");
        }
    }

    /// <summary>
    /// Generates moons for a given planet based on its properties and a random number generator.
    /// </summary>
    /// <param name="planetSeedInput">The seed input for generating moon data.</param>
    /// <param name="planetName">The name of the planet.</param>
    /// <param name="planetaryRadius">The radius of the planet.</param>
    /// <param name="planetaryMass">The mass of the planet.</param>
    /// <param name="planetTemperature">The surface temperature of the planet.</param>
    /// <param name="planetDistance">The orbital distance of the planet.</param>
    /// <param name="planetPrng">The random number generator for the planet.</param>
    /// <returns>A list of generated moon data.</returns>
    private List<MoonData> GenerateMoons(string planetSeedInput, string planetName, float planetaryRadius, float planetaryMass, float planetTemperature, float planetDistance, System.Random planetPrng)
    {
        List<MoonData> generatedMoons = new List<MoonData>();
        
        float maxTheoreticalMoons = planetaryRadius * 3.0f;
        int moonCount = Mathf.Clamp(Mathf.RoundToInt(StochasticMath.GetNormalValue(planetPrng, maxTheoreticalMoons * 0.3f, maxTheoreticalMoons * 0.2f)), 0, Mathf.FloorToInt(maxTheoreticalMoons));
        float currentOrbitalDistance = planetaryRadius * 2.0f;

        for (int m = 0; m < moonCount; m++)
        {
            string moonSubSeedInput = planetSeedInput + $"_Moon_Entity_{m}";
            System.Random moonPrng = new System.Random(StochasticMath.DeriveNumericalSeed(moonSubSeedInput));

            MoonData moon = new MoonData();
            moon.name = planetName + "-" + nameGenerator.ToAlphabet(m);
            moon.radius = Mathf.Max(StochasticMath.GetNormalValue(moonPrng, planetaryRadius * 0.15f, planetaryRadius * 0.05f), 0.01f); 

            float orbitalGap = Mathf.Max(StochasticMath.GetNormalValue(moonPrng, 5.0f, 1.5f), 1.0f);
            currentOrbitalDistance += orbitalGap + (moon.radius * 2f);
            moon.orbitalDistance = currentOrbitalDistance;

            float moonDensity = Mathf.Max(StochasticMath.GetNormalValue(moonPrng, 0.8f, 0.1f), 0.1f);
            moon.mass = Mathf.Pow(moon.radius, 3) * moonDensity;
            moon.surfaceGravity = moon.mass / (moon.radius * moon.radius);

            moon.orbitalInclination = StochasticMath.GetNormalValue(moonPrng, 0f, 1f);
            moon.axialTilt = Mathf.Abs(StochasticMath.GetNormalValue(moonPrng, 5f, 5f));

            moon.revolutionPeriod = 3.0f * Mathf.Sqrt(Mathf.Pow(moon.orbitalDistance, 3) / Mathf.Max(planetaryMass, 0.001f));
            moon.isTidallyLocked = (moonPrng.NextDouble() <= 0.85);

            if (moon.isTidallyLocked)
            {
                moon.rotationPeriod = moon.revolutionPeriod * 24f;
                moon.axialTilt = 0f;
            }
            else
            {
                moon.rotationPeriod = Mathf.Max(StochasticMath.GetNormalValue(moonPrng, 48f, 24f), 5f);
            }

            moon.orbitalEccentricity = AstrophysicsRules.CalculateEccentricity(moonPrng);
            moon.className = AstrophysicsRules.ClassifyMoon(planetDistance, currentSystemFrostLine, moonPrng);
            
            AstrophysicsRules.CalculateRings(moon.className, moonPrng, out moon.hasRings, out moon.ringDivisions);
            
            // Moons inherit their thermal zone from the host planet's distance to the star.
            // We assign the planet's temperature with a tiny variance (+/- 5%)
            float tempVariance = StochasticMath.GetNormalValue(moonPrng, 1.0f, 0.05f);
            
            moon.surfaceTemperature = planetTemperature * tempVariance;

            // Use planetDistance (AU) and the real system frost line (AU) to determine the moon's atmosphere
            moon.atmosphereType = AstrophysicsRules.DetermineAtmosphere(moon.className, moon.surfaceGravity, planetDistance, currentSystemFrostLine, moonPrng);

            generatedMoons.Add(moon);
        }

        return generatedMoons;
    }
}