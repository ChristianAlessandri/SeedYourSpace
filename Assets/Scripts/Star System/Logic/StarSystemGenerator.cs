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

    [Header("UI Events")]
    public UnityEngine.Events.UnityEvent OnSystemGenerated;

    [Tooltip("Controller for global orbital lines visibility.")]
    public OrreryController orreryController;
    
    [HideInInspector]
    public int algorithmVersion = 1; // Maintained for Web3 Smart Contract backward compatibility

    public int TotalPlanets { get; private set; }
    public int TotalMoons { get; private set; }
    public int TotalRings { get; private set; }
    public string SystemName { get; private set; }
    public StarData SystemStar { get; private set; }
    public List<PlanetData> SystemPlanets { get; private set; } = new List<PlanetData>();

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
        if (!InitializeGenerators()) return;

        ResetCounters();

        System.Random systemPrng = new System.Random(StochasticMath.DeriveNumericalSeed(seed));
        SystemName = nameGenerator.GenerateSystemName(systemPrng);
        
        GenerateSkybox(systemPrng);
        SystemStar = GenerateCentralStar(seed, SystemName);
        SystemPlanets = GeneratePlanetarySystem(seed, SystemName, SystemStar);

        // Notify UI and render the diorama only after all calculations are complete
        OnSystemGenerated?.Invoke();
        BuildVisualRepresentation();
    }

    /// <summary>
    /// Initializes the Markov name generator using a JSON data file. Returns false if the file is missing or corrupted.
    /// </summary>
    /// <returns>True if initialization is successful, false otherwise.</returns>
    private bool InitializeGenerators()
    {
        if (nameGenerator != null) return true;

        TextAsset jsonFile = Resources.Load<TextAsset>("markov_data");
        if (jsonFile == null)
        {
            Debug.LogError("Critical Error: Markov data file not found in Resources folder!");
            return false;
        }

        nameGenerator = new MarkovNameGenerator(jsonFile.text);
        return true;
    }

    /// <summary>
    /// Resets the counters for planets, moons, and rings to zero before generating a new system.
    /// </summary> 
    private void ResetCounters()
    {
        TotalPlanets = 0;
        TotalMoons = 0;
        TotalRings = 0;
    }

    /// <summary>
    /// Generates the skybox for the star system, restricted to deep space colors and softer intensities.
    /// </summary>
    /// <param name="systemPrng">The random number generator for the system.</param>
    private void GenerateSkybox(System.Random systemPrng)
    {
        float starDistance = (float)systemPrng.NextDouble() * 25f + 75f; 
        float starVisibility = (float)systemPrng.NextDouble() * 50f + 225f; 

        // Restrict hue to deep blues, purples, cyans, and magentas (0.55 to 0.85)
        float hue = Mathf.Lerp(0.55f, 0.85f, (float)systemPrng.NextDouble());
        float sat = Mathf.Lerp(0.6f, 0.9f, (float)systemPrng.NextDouble());
        
        // Lower the base value (brightness) to make it look ethereal
        float val = Mathf.Lerp(0.05f, 0.15f, (float)systemPrng.NextDouble()); 
        
        Color baseNebulaColor = Color.HSVToRGB(hue, sat, val);

        // Keep the HDR bloom subtle
        float hdrIntensity = Mathf.Lerp(1.2f, 2.0f, (float)systemPrng.NextDouble());
        
        // Set a low alpha (e.g., 0.2 to 0.4) to allow the shader to blend with the black background
        float alphaTransparency = Mathf.Lerp(0.2f, 0.4f, (float)systemPrng.NextDouble());

        Color hdrNebulaColor = new Color(
            baseNebulaColor.r * hdrIntensity, 
            baseNebulaColor.g * hdrIntensity, 
            baseNebulaColor.b * hdrIntensity, 
            alphaTransparency
        );

        if (dioramaBuilder != null)
        {
            dioramaBuilder.BuildSkybox(hdrNebulaColor, starDistance, starVisibility);
        }
    }

    /// <summary>
    /// Generates the central star of the system based on the provided seed and root name.
    /// </summary>
    /// <param name="baseSeed">The base seed for the star generation.</param>
    /// <param name="rootName">The root name for the star.</param>
    /// <returns>The generated star data.</returns>
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

        star.axialTilt = Mathf.Abs(StochasticMath.GetNormalValue(starPrng, 7.25f, 2f));
        star.rotationPeriod = Mathf.Max(StochasticMath.GetNormalValue(starPrng, 600f, 150f), 100f);

        AstrophysicsRules.CalculateStellarSurface(
            star.temperature, star.mass, star.radius, star.rotationPeriod, starPrng, 
            out star.baseColor, out star.magneticActivity, out star.granulationScale
        );

        return star;
    }

    /// <summary>
    /// Generates the planetary system for the star system.
    /// </summary>
    /// <param name="baseSeed">The base seed for the planetary system generation.</param>
    /// <param name="rootName">The root name for the planetary system.</param>
    /// <param name="centralStar">The central star of the system.</param>
    /// <returns>The list of generated planets.</returns>
    private List<PlanetData> GeneratePlanetarySystem(string baseSeed, string rootName, StarData centralStar)
    {
        string layoutSubSeedInput = baseSeed + "_Planets_Layout";
        int layoutNumericalSeed = StochasticMath.DeriveNumericalSeed(layoutSubSeedInput);
        System.Random layoutPrng = new System.Random(layoutNumericalSeed);

        float rawPlanetCount = StochasticMath.GetNormalValue(layoutPrng, 5.5f, 2.0f);
        int planetCount = Mathf.Clamp(Mathf.RoundToInt(rawPlanetCount), 1, 12);
        
        List<PlanetData> generatedPlanets = new List<PlanetData>();

        for (int i = 0; i < planetCount; i++)
        {
            string planetSubSeedInput = baseSeed + $"_Planet_Entity_{i}";
            PlanetData planet = GeneratePlanetEntity(planetSubSeedInput, rootName, i, centralStar);
            
            planet.moons = GenerateMoons(planetSubSeedInput, planet);
            generatedPlanets.Add(planet);
        }

        return generatedPlanets;
    }

    /// <summary>
    /// Generates a single planet entity based on the provided seed and root name.
    /// </summary>
    /// <param name="planetSeedInput">The seed input for the planet generation.</param>
    /// <param name="rootName">The root name for the planet.</param>
    /// <param name="planetIndex">The index of the planet in the system.</param>
    /// <param name="centralStar">The central star of the system.</param>
    /// <returns>The generated planet data.</returns>
    private PlanetData GeneratePlanetEntity(string planetSeedInput, string rootName, int planetIndex, StarData centralStar)
    {
        int planetNumericalSeed = StochasticMath.DeriveNumericalSeed(planetSeedInput);
        System.Random planetPrng = new System.Random(planetNumericalSeed);

        PlanetData planet = new PlanetData();
        planet.name = rootName + " " + nameGenerator.ToRoman(planetIndex + 1);
        planet.orbitalDistance = AstrophysicsRules.CalculateOrbitalDistance(planetIndex, planetPrng);

        PlanetProfile selectedClass = AstrophysicsRules.ClassifyPlanet(planet.orbitalDistance, planetPrng, currentSystemFrostLine);
        planet.className = selectedClass.className;
        
        planet.radius = Mathf.Max(StochasticMath.GetNormalValue(planetPrng, selectedClass.radiusMean, selectedClass.radiusStdDev), 0.1f);
        float density = Mathf.Max(StochasticMath.GetNormalValue(planetPrng, selectedClass.densityMean, 0.1f), 0.1f);
        planet.mass = Mathf.Pow(planet.radius, 3) * density;
        planet.surfaceGravity = planet.mass / (planet.radius * planet.radius);

        float distanceInSolarRadii = planet.orbitalDistance * 215.03f; 
        planet.surfaceTemperature = centralStar.temperature * Mathf.Sqrt(centralStar.radius / (2f * distanceInSolarRadii)) * 0.9f;
        
        planet.axialTilt = Mathf.Abs(StochasticMath.GetNormalValue(planetPrng, 23.5f, 15f));
        planet.orbitalInclination = StochasticMath.GetNormalValue(planetPrng, 0f, 3f);
        planet.atmosphereType = AstrophysicsRules.DetermineAtmosphere(planet.className, planet.surfaceGravity, planet.orbitalDistance, currentSystemFrostLine, planetPrng);
        AstrophysicsRules.CalculateAtmosphereVisuals(
            planet.atmosphereType,
            out planet.atmosphereColor, 
            out planet.cloudColor, 
            out planet.atmosphereScale
        );

        AstrophysicsRules.CalculatePlanetVisuals(
            planet.className, planet.surfaceTemperature, planet.atmosphereType, planetPrng, 
            out planet.baseColor, out planet.secondaryColor, out planet.hydrofraction, out planet.cloudCoverage
        );

        float revolutionYears = Mathf.Sqrt(Mathf.Pow(planet.orbitalDistance, 3) / centralStar.mass);
        planet.revolutionPeriod = revolutionYears * 365.25f;
        
        float baseRotation = (planet.className == "Gas Giant" || planet.className == "Ice Giant") ? 12f : 24f;
        planet.rotationPeriod = Mathf.Max(StochasticMath.GetNormalValue(planetPrng, baseRotation, baseRotation * 0.5f), 2f); 
        
        if (planet.orbitalDistance < 0.2f) 
        {
            planet.rotationPeriod = planet.revolutionPeriod * 24f; 
        }
        
        planet.orbitalEccentricity = AstrophysicsRules.CalculateEccentricity(planetPrng);
        AstrophysicsRules.CalculateRings(planet.className, planet.radius, planetPrng, 
            out planet.hasRings, out planet.ringDivisions, out planet.ringInnerRadius, out planet.ringOuterRadius, out planet.ringColor);
        
        TotalPlanets++;
        if (planet.hasRings)
        {
            TotalRings += planet.ringDivisions;
        }

        return planet;
    }

    /// <summary>
    /// Generates the moons for a given planet.
    /// </summary>
    /// <param name="planetSeedInput">The seed input for the planet generation.</param>
    /// <param name="parentPlanet">The parent planet for which to generate moons.</param>
    /// <returns>The list of generated moon data.</returns>
    private List<MoonData> GenerateMoons(string planetSeedInput, PlanetData parentPlanet)
    {
        List<MoonData> generatedMoons = new List<MoonData>();
        System.Random layoutPrng = new System.Random(StochasticMath.DeriveNumericalSeed(planetSeedInput + "_MoonLayout"));
        
        float maxTheoreticalMoons = parentPlanet.radius * 3.0f;
        int moonCount = Mathf.Clamp(Mathf.RoundToInt(StochasticMath.GetNormalValue(layoutPrng, maxTheoreticalMoons * 0.3f, maxTheoreticalMoons * 0.2f)), 0, Mathf.FloorToInt(maxTheoreticalMoons));
        float currentOrbitalDistance = parentPlanet.radius * 2.0f;

        for (int m = 0; m < moonCount; m++)
        {
            string moonSubSeedInput = planetSeedInput + $"_Moon_Entity_{m}";
            MoonData moon = GenerateMoonEntity(moonSubSeedInput, parentPlanet, m, ref currentOrbitalDistance);
            generatedMoons.Add(moon);
        }

        return generatedMoons;
    }

    /// <summary>
    /// Generates a single moon entity based on the provided seed and parent planet.
    /// </summary>
    /// <param name="moonSeedInput">The seed input for the moon generation.</param>
    /// <param name="parentPlanet">The parent planet for which the moon is generated.</param>
    /// <param name="moonIndex">The index of the moon in the planet's moon list.</param>
    /// <param name="currentOrbitalDistance">Reference to the current orbital distance for moon placement, updated after each moon generation.</param>
    /// <returns>The generated moon data.</returns>
    private MoonData GenerateMoonEntity(string moonSeedInput, PlanetData parentPlanet, int moonIndex, ref float currentOrbitalDistance)
    {
        System.Random moonPrng = new System.Random(StochasticMath.DeriveNumericalSeed(moonSeedInput));
        MoonData moon = new MoonData();
        
        moon.name = parentPlanet.name + "-" + nameGenerator.ToAlphabet(moonIndex);
        moon.radius = Mathf.Max(StochasticMath.GetNormalValue(moonPrng, parentPlanet.radius * 0.15f, parentPlanet.radius * 0.05f), 0.01f); 

        float orbitalGap = Mathf.Max(StochasticMath.GetNormalValue(moonPrng, 5.0f, 1.5f), 1.0f);
        currentOrbitalDistance += orbitalGap + (moon.radius * 2f);
        moon.orbitalDistance = currentOrbitalDistance;

        float moonDensity = Mathf.Max(StochasticMath.GetNormalValue(moonPrng, 0.8f, 0.1f), 0.1f);
        moon.mass = Mathf.Pow(moon.radius, 3) * moonDensity;
        moon.surfaceGravity = moon.mass / (moon.radius * moon.radius);

        moon.orbitalInclination = StochasticMath.GetNormalValue(moonPrng, 0f, 1f);
        moon.axialTilt = Mathf.Abs(StochasticMath.GetNormalValue(moonPrng, 5f, 5f));

        moon.revolutionPeriod = 3.0f * Mathf.Sqrt(Mathf.Pow(moon.orbitalDistance, 3) / Mathf.Max(parentPlanet.mass, 0.001f));
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

        moon.orbitalEccentricity = Mathf.Clamp(Mathf.Abs(StochasticMath.GetNormalValue(moonPrng, 0.01f, 0.01f)), 0f, 0.05f);
        moon.className = AstrophysicsRules.ClassifyMoon(parentPlanet.orbitalDistance, currentSystemFrostLine, moonPrng);
        
        AstrophysicsRules.CalculateRings(moon.className, moon.radius, moonPrng, 
            out moon.hasRings, out moon.ringDivisions, out moon.ringInnerRadius, out moon.ringOuterRadius, out moon.ringColor);
        
        float tempVariance = StochasticMath.GetNormalValue(moonPrng, 1.0f, 0.05f);
        moon.surfaceTemperature = parentPlanet.surfaceTemperature * tempVariance;

        moon.atmosphereType = AstrophysicsRules.DetermineAtmosphere(moon.className, moon.surfaceGravity, parentPlanet.orbitalDistance, currentSystemFrostLine, moonPrng);
        AstrophysicsRules.CalculateAtmosphereVisuals(
            moon.atmosphereType,
            out moon.atmosphereColor, 
            out moon.cloudColor, 
            out moon.atmosphereScale
        );

        AstrophysicsRules.CalculatePlanetVisuals(
            moon.className, moon.surfaceTemperature, moon.atmosphereType, moonPrng, 
            out moon.baseColor, out moon.secondaryColor, out moon.hydrofraction, out moon.cloudCoverage
        );

        TotalMoons++;
        if (moon.hasRings)
        {
            TotalRings += moon.ringDivisions;
        }

        return moon;
    }

    /// <summary>
    /// Builds the visual representation of the star system and initializes global orbital lines.
    /// </summary>
    private void BuildVisualRepresentation()
    {
        if (dioramaBuilder != null)
        {
            dioramaBuilder.BuildUniverse(SystemStar, SystemPlanets);

            // Initialize the orrery lines after all celestial bodies are instantiated
            if (orreryController != null)
            {
                orreryController.InitializeOrrery();
            }
        }
        else
        {
            Debug.LogWarning("Diorama Builder is not assigned. Visual representation will not be generated.");
        }
    }
}