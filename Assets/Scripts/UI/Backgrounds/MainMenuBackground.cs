using UnityEngine;
using System.Collections.Generic;
using System;

public class MainMenuBackground : MonoBehaviour
{
    [Header("Dependencies")]
    public VisualDioramaBuilder dioramaBuilder;
    public int algorithmVersion = 1;

    [Header("Menu Aesthetics")]
    [Tooltip("Forced rotation period in hours to make the star spin visibly in the menu.")]
    public float menuRotationSpeed = 24f; 
    public float menuStarRadius = 5f;

    private IStochasticMath stochasticMath;
    private IAstrophysicsRules astroRules;
    private GenerationData generationData;

    private void Start()
    {
        GenerateMenuEnvironment();
    }

    private void GenerateMenuEnvironment()
    {
        // Initialize logic modules via Factories
        stochasticMath = StochasticMathFactory.CreateMath(algorithmVersion);
        astroRules = AstrophysicsRulesFactory.CreateRules(algorithmVersion);
        
        TextAsset jsonRules = Resources.Load<TextAsset>($"generation_data_v{algorithmVersion}");
        if (jsonRules == null)
        {
            Debug.LogError("[MainMenuBackground] Generation data JSON missing!");
            return;
        }
        
        generationData = JsonUtility.FromJson<GenerationData>(jsonRules.text);
        astroRules.Initialize(generationData, stochasticMath);

        // Generate a totally random seed for the menu session
        string randomSeed = Guid.NewGuid().ToString();
        System.Random prng = new System.Random(stochasticMath.DeriveNumericalSeed(randomSeed));

        // Build Skybox
        GenerateMenuSkybox(prng);

        // Generate Star Data
        StarData menuStar = GenerateMenuStarData(prng);

        // Build physical representation using empty lists for planets and belts
        if (dioramaBuilder != null)
        {
            dioramaBuilder.BuildUniverse(menuStar, new List<PlanetData>(), new List<AsteroidBeltData>());
        }
    }

    private StarData GenerateMenuStarData(System.Random prng)
    {
        int spectralIndex = stochasticMath.GetWeightedIndex(generationData.stellarWeights, prng);
        
        StarData star = new StarData();
        star.name = "Menu_Anomaly";
        star.spectralClass = astroRules.GetSpectralClassName(spectralIndex);
        
        // Fixed radius for consistent menu framing, random temperature/mass for color variety
        star.radius = menuStarRadius; 
        star.mass = Mathf.Max(stochasticMath.GetNormalValue(prng, generationData.massMeans[spectralIndex], 0.5f), 0.5f);
        star.temperature = Mathf.Max(stochasticMath.GetNormalValue(prng, generationData.tempMeans[spectralIndex], generationData.tempMeans[spectralIndex] * 0.1f), 2000f);
        
        star.axialTilt = 15f; // Slight tilt looks cinematic
        star.rotationPeriod = menuRotationSpeed; // Fast enough to see the spin

        astroRules.CalculateStellarSurface(
            star.temperature, star.mass, star.radius, star.rotationPeriod, prng, 
            out star.baseColor, out star.magneticActivity, out star.granulationScale
        );

        return star;
    }

    private void GenerateMenuSkybox(System.Random prng)
    {
        float hue1 = Mathf.Lerp(generationData.skyboxHueMin, generationData.skyboxHueMax, (float)prng.NextDouble());
        float hueShift = Mathf.Lerp(0.1f, 0.25f, (float)prng.NextDouble());
        float hue2 = Mathf.Repeat(hue1 + hueShift, 1.0f);

        float sat = Mathf.Lerp(generationData.skyboxSaturationMin, generationData.skyboxSaturationMax, (float)prng.NextDouble());
        float val = Mathf.Lerp(0.05f, 0.15f, (float)prng.NextDouble()); 
        
        Color baseColor1 = Color.HSVToRGB(hue1, sat, val);
        Color baseColor2 = Color.HSVToRGB(hue2, sat, val);

        float hdrIntensity = Mathf.Lerp(1.2f, 2.0f, (float)prng.NextDouble());
        Color hdrNebula1 = new Color(baseColor1.r * hdrIntensity, baseColor1.g * hdrIntensity, baseColor1.b * hdrIntensity, 0.3f);
        Color hdrNebula2 = new Color(baseColor2.r * hdrIntensity, baseColor2.g * hdrIntensity, baseColor2.b * hdrIntensity, 0.3f);

        if (dioramaBuilder != null)
        {
            dioramaBuilder.BuildSkybox(hdrNebula1, hdrNebula2, 100f, 250f);
        }
    }
}