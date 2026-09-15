using UnityEngine;
using System;
using System.Collections.Generic;

public class StarSystemGenerator : MonoBehaviour
{
    [Header("Generation Settings")]
    public string masterSeed = "0";

    [Header("Diorama Settings")]
    public VisualDioramaBuilder dioramaBuilder;

    [Header("UI Events")]
    public UnityEngine.Events.UnityEvent OnSystemGenerated;

    [Tooltip("Controller for global orbital lines visibility.")]
    public OrreryController orreryController;
    
    [HideInInspector]
    public int algorithmVersion = 1; // Web3 Smart Contract backward compatibility

    public int TotalPlanets { get; private set; }
    public int TotalMoons { get; private set; }
    public int TotalRings { get; private set; }
    public string SystemName { get; private set; }
    public StarData SystemStar { get; private set; }
    public List<PlanetData> SystemPlanets { get; private set; } = new List<PlanetData>();
    public List<AsteroidBeltData> SystemBelts { get; private set; } = new List<AsteroidBeltData>();

    private INameGenerator nameGenerator;
    private IStochasticMath stochasticMath;
    private IAstrophysicsRules astroRules; 
    private GenerationData generationData; 
    private float currentSystemFrostLine;

    private void Start()
    {
        GenerateCompleteStarSystem(masterSeed);
    }

    public void GenerateCompleteStarSystem(string seed)
    {
        if (!InitializeGenerators()) return;

        ResetCounters();

        System.Random systemPrng = new System.Random(stochasticMath.DeriveNumericalSeed(seed));
        SystemName = nameGenerator.GenerateSystemName(systemPrng);
        
        GenerateSkybox(systemPrng);
        SystemStar = GenerateCentralStar(seed, SystemName);
        SystemPlanets = GeneratePlanetarySystem(seed, SystemName, SystemStar);
        SystemBelts = GenerateAsteroidBelts(seed, SystemPlanets);

        // Notify UI and render the diorama only after all calculations are complete
        OnSystemGenerated?.Invoke();
        BuildVisualRepresentation();
    }

    private bool InitializeGenerators()
    {
        // Initialize Math Core
        if (stochasticMath == null)
        {
            stochasticMath = StochasticMathFactory.CreateMath(algorithmVersion);
        }

        // Initialize Name Generator
        if (nameGenerator == null)
        {
            TextAsset jsonNameFile = Resources.Load<TextAsset>($"markov_data_v{algorithmVersion}");
            if (jsonNameFile == null)
            {
                Debug.LogError($"Critical Error: markov_data_v{algorithmVersion} not found in Resources folder!");
                return false;
            }
            nameGenerator = NameGeneratorFactory.CreateNameGenerator(algorithmVersion, jsonNameFile.text);
        }

        // Initialize Astrophysics Rules via Factory
        if (astroRules == null)
        {
            astroRules = AstrophysicsRulesFactory.CreateRules(algorithmVersion);
            
            TextAsset jsonRulesFile = Resources.Load<TextAsset>($"generation_data_v{algorithmVersion}");
            if (jsonRulesFile == null)
            {
                Debug.LogError($"Critical Error: generation_data_v{algorithmVersion} not found in Resources folder!");
                return false;
            }
            generationData = JsonUtility.FromJson<GenerationData>(jsonRulesFile.text);
            
            astroRules.Initialize(generationData, stochasticMath);
        }

        return true;
    }

    private void ResetCounters()
    {
        TotalPlanets = 0;
        TotalMoons = 0;
        TotalRings = 0;
    }

    private void GenerateSkybox(System.Random systemPrng)
    {
        float starDistance = (float)systemPrng.NextDouble() * 25f + 75f; 
        float starVisibility = (float)systemPrng.NextDouble() * 50f + 225f; 

        // Primary Hue (Deep Space colors extracted to JSON)
        float hue1 = Mathf.Lerp(generationData.skyboxHueMin, generationData.skyboxHueMax, (float)systemPrng.NextDouble());
        
        // Secondary Hue (Shifted slightly to create beautiful analog gradients, e.g., Blue to Magenta)
        float hueShift = Mathf.Lerp(0.1f, 0.25f, (float)systemPrng.NextDouble());
        float hue2 = Mathf.Repeat(hue1 + hueShift, 1.0f);

        float sat = Mathf.Lerp(generationData.skyboxSaturationMin, generationData.skyboxSaturationMax, (float)systemPrng.NextDouble());
        float val = Mathf.Lerp(0.05f, 0.15f, (float)systemPrng.NextDouble()); 
        
        Color baseColor1 = Color.HSVToRGB(hue1, sat, val);
        Color baseColor2 = Color.HSVToRGB(hue2, sat, val);

        float hdrIntensity = Mathf.Lerp(1.2f, 2.0f, (float)systemPrng.NextDouble());
        float alphaTransparency = Mathf.Lerp(0.2f, 0.4f, (float)systemPrng.NextDouble());

        Color hdrNebulaColor1 = new Color(
            baseColor1.r * hdrIntensity, baseColor1.g * hdrIntensity, baseColor1.b * hdrIntensity, alphaTransparency
        );

        Color hdrNebulaColor2 = new Color(
            baseColor2.r * hdrIntensity, baseColor2.g * hdrIntensity, baseColor2.b * hdrIntensity, alphaTransparency
        );

        if (dioramaBuilder != null)
        {
            dioramaBuilder.BuildSkybox(hdrNebulaColor1, hdrNebulaColor2, starDistance, starVisibility);
        }
    }

    private StarData GenerateCentralStar(string baseSeed, string rootName)
    {
        string starSubSeedInput = baseSeed + "_Star_Entity";
        int starNumericalSeed = stochasticMath.DeriveNumericalSeed(starSubSeedInput);
        System.Random starPrng = new System.Random(starNumericalSeed);

        int spectralIndex = stochasticMath.GetWeightedIndex(generationData.stellarWeights, starPrng);
        
        StarData star = new StarData();
        star.name = rootName + " Prime";
        star.spectralClass = astroRules.GetSpectralClassName(spectralIndex);
        
        star.mass = Mathf.Max(stochasticMath.GetNormalValue(starPrng, generationData.massMeans[spectralIndex], generationData.massMeans[spectralIndex] * 0.1f), 0.08f);
        star.temperature = Mathf.Max(stochasticMath.GetNormalValue(starPrng, generationData.tempMeans[spectralIndex], generationData.tempMeans[spectralIndex] * 0.05f), 2000f);
        star.radius = Mathf.Max(stochasticMath.GetNormalValue(starPrng, generationData.radiusMeans[spectralIndex], generationData.radiusMeans[spectralIndex] * 0.1f), 0.1f);

        float oscillation = Mathf.Clamp(stochasticMath.GetNormalValue(starPrng, 0f, 0.05f), -0.20f, 0.20f);
        
        currentSystemFrostLine = generationData.baseFrostLines[spectralIndex] * (1f + oscillation);
        star.frostLine = currentSystemFrostLine;

        star.axialTilt = Mathf.Abs(stochasticMath.GetNormalValue(starPrng, 7.25f, 2f));
        star.rotationPeriod = Mathf.Max(stochasticMath.GetNormalValue(starPrng, 600f, 150f), 100f);

        astroRules.CalculateStellarSurface(
            star.temperature, star.mass, star.radius, star.rotationPeriod, starPrng, 
            out star.baseColor, out star.magneticActivity, out star.granulationScale
        );

        return star;
    }

    private List<PlanetData> GeneratePlanetarySystem(string baseSeed, string rootName, StarData centralStar)
    {
        string layoutSubSeedInput = baseSeed + "_Planets_Layout";
        int layoutNumericalSeed = stochasticMath.DeriveNumericalSeed(layoutSubSeedInput);
        System.Random layoutPrng = new System.Random(layoutNumericalSeed);

        // Planet count logic driven by JSON data
        float rawPlanetCount = stochasticMath.GetNormalValue(layoutPrng, generationData.planetCountMean, generationData.planetCountStdDev);
        int planetCount = Mathf.Clamp(Mathf.RoundToInt(rawPlanetCount), generationData.minPlanets, generationData.maxPlanets);
        
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

    private PlanetData GeneratePlanetEntity(string planetSeedInput, string rootName, int planetIndex, StarData centralStar)
    {
        int planetNumericalSeed = stochasticMath.DeriveNumericalSeed(planetSeedInput);
        System.Random planetPrng = new System.Random(planetNumericalSeed);

        PlanetData planet = new PlanetData();
        planet.name = rootName + " " + nameGenerator.ToRoman(planetIndex + 1);
        planet.orbitalDistance = astroRules.CalculateOrbitalDistance(planetIndex, planetPrng);

        PlanetProfile selectedClass = astroRules.ClassifyPlanet(planet.orbitalDistance, planetPrng, currentSystemFrostLine);
        planet.className = selectedClass.className;
        
        planet.radius = Mathf.Max(stochasticMath.GetNormalValue(planetPrng, selectedClass.radiusMean, selectedClass.radiusStdDev), 0.1f);
        float density = Mathf.Max(stochasticMath.GetNormalValue(planetPrng, selectedClass.densityMean, 0.1f), 0.1f);
        planet.mass = Mathf.Pow(planet.radius, 3) * density;
        planet.surfaceGravity = planet.mass / (planet.radius * planet.radius);

        float distanceInSolarRadii = planet.orbitalDistance * 215.03f; 
        planet.surfaceTemperature = centralStar.temperature * Mathf.Sqrt(centralStar.radius / (2f * distanceInSolarRadii)) * 0.9f;
        
        planet.axialTilt = Mathf.Abs(stochasticMath.GetNormalValue(planetPrng, 23.5f, 15f));
        planet.orbitalInclination = stochasticMath.GetNormalValue(planetPrng, 0f, 3f);
        planet.atmosphereType = astroRules.DetermineAtmosphere(planet.className, planet.surfaceGravity, planet.orbitalDistance, currentSystemFrostLine, planetPrng);
        astroRules.CalculateAtmosphereVisuals(
            planet.atmosphereType,
            out planet.atmosphereColor, 
            out planet.cloudColor, 
            out planet.atmosphereScale
        );

        astroRules.CalculatePlanetVisuals(
            planet.className, planet.surfaceTemperature, planet.atmosphereType, planetPrng, 
            out planet.baseColor, out planet.secondaryColor, out planet.hydrofraction, out planet.cloudCoverage
        );

        float revolutionYears = Mathf.Sqrt(Mathf.Pow(planet.orbitalDistance, 3) / centralStar.mass);
        planet.revolutionPeriod = revolutionYears * 365.25f;
        
        float baseRotation = (planet.className == "Gas Giant" || planet.className == "Ice Giant") ? 12f : 24f;
        planet.rotationPeriod = Mathf.Max(stochasticMath.GetNormalValue(planetPrng, baseRotation, baseRotation * 0.5f), 2f); 
        
        if (planet.orbitalDistance < 0.2f) 
        {
            planet.rotationPeriod = planet.revolutionPeriod * 24f; 
        }
        
        planet.orbitalEccentricity = astroRules.CalculateEccentricity(planetPrng);
        astroRules.CalculateRings(planet.className, planet.radius, planetPrng, 
            out planet.hasRings, out planet.ringDivisions, out planet.ringInnerRadius, out planet.ringOuterRadius, out planet.ringColor);
        
        TotalPlanets++;
        if (planet.hasRings)
        {
            TotalRings += planet.ringDivisions;
        }

        return planet;
    }

    private List<MoonData> GenerateMoons(string planetSeedInput, PlanetData parentPlanet)
    {
        List<MoonData> generatedMoons = new List<MoonData>();
        System.Random layoutPrng = new System.Random(stochasticMath.DeriveNumericalSeed(planetSeedInput + "_MoonLayout"));
        
        float maxTheoreticalMoons = parentPlanet.radius * generationData.maxTheoreticalMoonsMultiplier;
        
        float mean = maxTheoreticalMoons * generationData.moonCountMeanMultiplier;
        float stdDev = maxTheoreticalMoons * generationData.moonCountStdDevMultiplier;
        
        int moonCount = Mathf.Clamp(Mathf.RoundToInt(stochasticMath.GetNormalValue(layoutPrng, mean, stdDev)), 0, Mathf.FloorToInt(maxTheoreticalMoons));
        float currentOrbitalDistance = parentPlanet.radius * 2.0f;

        for (int m = 0; m < moonCount; m++)
        {
            string moonSubSeedInput = planetSeedInput + $"_Moon_Entity_{m}";
            MoonData moon = GenerateMoonEntity(moonSubSeedInput, parentPlanet, m, ref currentOrbitalDistance);
            generatedMoons.Add(moon);
        }

        return generatedMoons;
    }

    private MoonData GenerateMoonEntity(string moonSeedInput, PlanetData parentPlanet, int moonIndex, ref float currentOrbitalDistance)
    {
        System.Random moonPrng = new System.Random(stochasticMath.DeriveNumericalSeed(moonSeedInput));
        MoonData moon = new MoonData();
        
        moon.name = parentPlanet.name + "-" + nameGenerator.ToAlphabet(moonIndex);
        moon.radius = Mathf.Max(stochasticMath.GetNormalValue(moonPrng, parentPlanet.radius * 0.15f, parentPlanet.radius * 0.05f), 0.01f); 

        float orbitalGap = Mathf.Max(stochasticMath.GetNormalValue(moonPrng, 5.0f, 1.5f), 1.0f);
        currentOrbitalDistance += orbitalGap + (moon.radius * 2f);
        moon.orbitalDistance = currentOrbitalDistance;

        float moonDensity = Mathf.Max(stochasticMath.GetNormalValue(moonPrng, 0.8f, 0.1f), 0.1f);
        moon.mass = Mathf.Pow(moon.radius, 3) * moonDensity;
        moon.surfaceGravity = moon.mass / (moon.radius * moon.radius);

        moon.orbitalInclination = stochasticMath.GetNormalValue(moonPrng, 0f, 1f);
        moon.axialTilt = Mathf.Abs(stochasticMath.GetNormalValue(moonPrng, 5f, 5f));

        moon.revolutionPeriod = 3.0f * Mathf.Sqrt(Mathf.Pow(moon.orbitalDistance, 3) / Mathf.Max(parentPlanet.mass, 0.001f));
        moon.isTidallyLocked = (moonPrng.NextDouble() <= generationData.moonTidalLockChance);

        if (moon.isTidallyLocked)
        {
            moon.rotationPeriod = moon.revolutionPeriod * 24f;
            moon.axialTilt = 0f;
        }
        else
        {
            moon.rotationPeriod = Mathf.Max(stochasticMath.GetNormalValue(moonPrng, 48f, 24f), 5f);
        }

        moon.orbitalEccentricity = Mathf.Clamp(Mathf.Abs(stochasticMath.GetNormalValue(moonPrng, 0.01f, 0.01f)), 0f, 0.05f);
        moon.className = astroRules.ClassifyMoon(parentPlanet.orbitalDistance, currentSystemFrostLine, moonPrng);
        
        astroRules.CalculateRings(moon.className, moon.radius, moonPrng, 
            out moon.hasRings, out moon.ringDivisions, out moon.ringInnerRadius, out moon.ringOuterRadius, out moon.ringColor);
        
        float tempVariance = stochasticMath.GetNormalValue(moonPrng, 1.0f, 0.05f);
        moon.surfaceTemperature = parentPlanet.surfaceTemperature * tempVariance;

        moon.atmosphereType = astroRules.DetermineAtmosphere(moon.className, moon.surfaceGravity, parentPlanet.orbitalDistance, currentSystemFrostLine, moonPrng);
        astroRules.CalculateAtmosphereVisuals(
            moon.atmosphereType,
            out moon.atmosphereColor, 
            out moon.cloudColor, 
            out moon.atmosphereScale
        );

        astroRules.CalculatePlanetVisuals(
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

    private void BuildVisualRepresentation()
    {
        if (dioramaBuilder != null)
        {
            dioramaBuilder.BuildUniverse(SystemStar, SystemPlanets, SystemBelts);

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

    private List<AsteroidBeltData> GenerateAsteroidBelts(string baseSeed, List<PlanetData> planets)
    {
        List<AsteroidBeltData> belts = new List<AsteroidBeltData>();
        System.Random beltPrng = new System.Random(stochasticMath.DeriveNumericalSeed(baseSeed + "_Belts"));

        planets.Sort((p1, p2) => p1.orbitalDistance.CompareTo(p2.orbitalDistance));
        float safeMargin = 0.4f; 

        for (int i = 0; i < planets.Count - 1; i++)
        {
            float gap = planets[i + 1].orbitalDistance - planets[i].orbitalDistance;

            if (gap > 1.2f && gap < 5.0f && beltPrng.NextDouble() <= generationData.innerBeltChance)
            {
                AsteroidBeltData belt = new AsteroidBeltData();
                belt.name = $"{SystemName} Inner Belt";
                belt.innerRadius = planets[i].orbitalDistance + safeMargin;
                belt.outerRadius = planets[i + 1].orbitalDistance - safeMargin;
                
                belt.asteroidCount = Mathf.Clamp(Mathf.RoundToInt(gap * 400f), generationData.minBeltAsteroids, 1200);
                belt.seed = beltPrng.Next();
                
                belts.Add(belt);
            }
        }

        if (planets.Count > 0 && beltPrng.NextDouble() <= generationData.kuiperBeltChance)
        {
            float lastOrbit = planets[planets.Count - 1].orbitalDistance;
            
            if (lastOrbit < generationData.maxKuiperDistance)
            {
                AsteroidBeltData outerBelt = new AsteroidBeltData();
                outerBelt.name = $"{SystemName} Kuiper Belt";
                outerBelt.innerRadius = lastOrbit + 2.0f;
                outerBelt.outerRadius = outerBelt.innerRadius + (float)(beltPrng.NextDouble() * 2f + 1f);
                
                outerBelt.asteroidCount = Mathf.Clamp(Mathf.RoundToInt(outerBelt.outerRadius * 50f), generationData.minBeltAsteroids * 2, 2500);
                outerBelt.seed = beltPrng.Next();
                
                belts.Add(outerBelt);
            }
        }

        return belts;
    }
}