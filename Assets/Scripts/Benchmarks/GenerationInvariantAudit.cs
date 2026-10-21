using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

// 500 seed x 3 reps
public sealed class GenerationInvariantAudit : MonoBehaviour
{
    [SerializeField] private StarSystemGenerator generator;
    [SerializeField] private bool runOnStart = true;
    [SerializeField, Min(1)] private int seedCount = 500;
    [SerializeField, Min(2)] private int repetitions = 3;
    [SerializeField] private int firstSeed = 1000000;
    [SerializeField, Min(1)] private int yieldEverySeeds = 10;

    private sealed class FrostProbe
    {
        public float distance, frost, selectedWeight;
        public string className;
    }
    private sealed class FrostVector
    {
        public float distance, frost;
        public PlanetProfile[] profiles;
    }
    private readonly List<FrostProbe> observed = new List<FrostProbe>();
    private readonly List<FrostVector> weightsObserved = new List<FrostVector>();
    private GenerationData config;
    private int checks, failures;

    private void Start() { if (runOnStart) StartCoroutine(Run()); }

    public IEnumerator Run()
    {
        if (generator == null || generator.enabled || generator.dioramaBuilder != null)
        {
            Debug.LogError("[SYS Audit] Assign a DISABLED generator, without dioramaBuilder.");
            yield break;
        }
        if (repetitions < 2 || seedCount < 1)
        {
            Debug.LogError("[SYS Audit] Sample not valid.");
            yield break;
        }
        TextAsset json = Resources.Load<TextAsset>("generation_data_v1");
        TextAsset markov = Resources.Load<TextAsset>("markov_data_v1");
        if (json == null || markov == null)
        {
            Debug.LogError("[SYS Audit] Place the JSON files in Assets/Resources without an extension in the Resources.Load filename.");
            yield break;
        }
        config = JsonUtility.FromJson<GenerationData>(json.text);
        if (config == null || config.planetClasses == null || config.planetClasses.Length == 0)
        {
            Debug.LogError("[SYS Audit] Invalid `generation_data_v1` configuration.");
            yield break;
        }
        generator.algorithmVersion = 1;
        generator.benchmarkLogicOnly = true;
        SysBenchmarkCsv.WriteEnvironment("invariants");
        Action<float, float, string, float> handler = (distance, frost, cls, weight) =>
        {
            observed.Add(new FrostProbe { distance = distance, frost = frost,
                className = cls, selectedWeight = weight });
        };
        Action<float, float, PlanetProfile[]> vectorHandler = (distance, frost, profiles) =>
        {
            weightsObserved.Add(new FrostVector {
                distance = distance, frost = frost, profiles = profiles });
        };
        AstrophysicsRulesV1.BenchmarkClassified += handler;
        AstrophysicsRulesV1.BenchmarkWeightVector += vectorHandler;
        string target = "";
        try
        {
            using (var csv = new SysBenchmarkCsv("generation_invariants.csv",
                       "seed", "version", "repetition", "body_id", "invariant_id",
                       "passed", "measured_value", "expected_min", "expected_max"))
            {
                for (int s = 0; s < seedCount; ++s)
                {
                    string seed = ((long)firstSeed + s).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    string baseline = null;
                    for (int r = 0; r < repetitions; ++r)
                    {
                        observed.Clear();
                        weightsObserved.Clear();
                        generator.GenerateCompleteStarSystem(seed);
                        if (generator.SystemStar == null || generator.SystemPlanets == null)
                            throw new InvalidOperationException("Generazione nulla sul seed " + seed);
                        string fingerprint = Digest(generator);
                        if (r == 0) baseline = fingerprint;
                        else Add(csv, seed, r, "system", "DET_MODEL_MATCH",
                            fingerprint == baseline, fingerprint, baseline, baseline);
                        CheckGenerated(csv, seed, r);
                    }
                    if ((s + 1) % yieldEverySeeds == 0) yield return null;
                }
                csv.Flush();
                target = csv.PathName;
            }
        }
        finally
        {
            AstrophysicsRulesV1.BenchmarkClassified -= handler;
            AstrophysicsRulesV1.BenchmarkWeightVector -= vectorHandler;
        }
        Debug.Log("[SYS Audit] " + checks + " controlli, " + failures +
                  " violazioni. CSV: " + target);
    }

    private void Add(SysBenchmarkCsv csv, string seed, int repetition,
        string body, string id, bool passed, object measured, object min, object max)
    {
        checks++;
        if (!passed) failures++;
        csv.Write(seed, 1, repetition, body, id, passed, measured, min, max);
    }

    private static bool Between(float x, float min, float max)
    {
        return SysBenchmarkCsv.Finite(x) && x >= min && x <= max;
    }

    private void CheckGenerated(SysBenchmarkCsv csv, string seed, int rep)
    {
        var star = generator.SystemStar;
        var planets = generator.SystemPlanets;
        Add(csv, seed, rep, "system", "INV_PLANET_COUNT",
            planets.Count >= config.minPlanets && planets.Count <= config.maxPlanets,
            planets.Count, config.minPlanets, config.maxPlanets);
        Add(csv, seed, rep, "star", "INV_PHYS_DOMAIN",
            Positive(star.mass) && Positive(star.radius) && Positive(star.temperature) &&
            Positive(star.rotationPeriod) && Positive(star.frostLine),
            "mass=" + SysBenchmarkCsv.F(star.mass) + ";radius=" + SysBenchmarkCsv.F(star.radius), ">0", "finite");

        Add(csv, seed, rep, "system", "INV_FROST_WEIGHTS",
            observed.Count == planets.Count && weightsObserved.Count == planets.Count,
            observed.Count + "/" + weightsObserved.Count, planets.Count, planets.Count);
        for (int p = 0; p < planets.Count; ++p)
        {
            PlanetData planet = planets[p];
            string planetId = "planet:" + p;
            float nominal = p == 0 ? 0.4f : 0.4f + 0.3f * Mathf.Pow(2f, p - 1);
            float min = nominal * 0.85f;
            float max = nominal * 1.15f;
            Add(csv, seed, rep, planetId, "INV_SEMIMAJOR",
                Between(planet.orbitalDistance, min - 1e-5f, max + 1e-5f) && planet.orbitalDistance > 0,
                SysBenchmarkCsv.F(planet.orbitalDistance), SysBenchmarkCsv.F(min), SysBenchmarkCsv.F(max));
            Add(csv, seed, rep, planetId, "INV_PLANET_ECC",
                Between(planet.orbitalEccentricity, 0f, 0.99f),
                SysBenchmarkCsv.F(planet.orbitalEccentricity), 0, 0.99f);
            Add(csv, seed, rep, planetId, "INV_PHYS_DOMAIN", ValidBody(planet),
                "mass=" + SysBenchmarkCsv.F(planet.mass) + ";radius=" + SysBenchmarkCsv.F(planet.radius) +
                ";period=" + SysBenchmarkCsv.F(planet.revolutionPeriod), ">0", "finite");

            FrostProbe found = null;
            for (int j = 0; j < observed.Count; ++j)
                if (observed[j].distance == planet.orbitalDistance &&
                    observed[j].className == planet.className)
                { found = observed[j]; break; }
            PlanetClassParams matching = new PlanetClassParams();
            bool exists = false;
            foreach (PlanetClassParams c in config.planetClasses)
                if (c.className == planet.className) { matching = c; exists = true; break; }
            float expected = planet.orbitalDistance > star.frostLine ?
                matching.outsideFrostWeight : matching.insideFrostWeight;
            FrostVector actualVector = null;
            foreach (FrostVector v in weightsObserved)
                if (v.distance == planet.orbitalDistance) { actualVector = v; break; }
            bool vectorMatches = actualVector != null &&
                actualVector.profiles.Length == config.planetClasses.Length &&
                Mathf.Abs(actualVector.frost - star.frostLine) < 1e-5f;
            if (vectorMatches)
            {
                for (int j = 0; j < config.planetClasses.Length; j++)
                {
                    PlanetClassParams cfg = config.planetClasses[j];
                    float targetWeight = planet.orbitalDistance > star.frostLine ?
                        cfg.outsideFrostWeight : cfg.insideFrostWeight;
                    PlanetProfile actual = actualVector.profiles[j];
                    if (actual.className != cfg.className ||
                        Mathf.Abs(actual.currentWeight - targetWeight) > 1e-5f)
                    { vectorMatches = false; break; }
                }
            }
            bool frostOk = vectorMatches && exists && found != null &&
                Mathf.Abs(found.frost - star.frostLine) < 1e-5f &&
                Mathf.Abs(found.selectedWeight - expected) < 1e-5f && expected > 0;
            Add(csv, seed, rep, planetId, "INV_FROST_WEIGHTS", frostOk,
                (planet.orbitalDistance > star.frostLine ? "outside" : "inside") +
                ";weight=" + (found != null ? SysBenchmarkCsv.F(found.selectedWeight) : "missing") +
                ";class=" + planet.className,
                SysBenchmarkCsv.F(expected), SysBenchmarkCsv.F(expected));

            int m = 0;
            foreach (MoonData moon in planet.moons)
            {
                string moonId = planetId + "/moon:" + m++;
                Add(csv, seed, rep, moonId, "INV_MOON_ECC",
                    Between(moon.orbitalEccentricity, 0f, 0.05f),
                    SysBenchmarkCsv.F(moon.orbitalEccentricity), 0, 0.05f);
                Add(csv, seed, rep, moonId, "INV_PHYS_DOMAIN", ValidBody(moon),
                    "mass=" + SysBenchmarkCsv.F(moon.mass) + ";radius=" + SysBenchmarkCsv.F(moon.radius) +
                    ";period=" + SysBenchmarkCsv.F(moon.revolutionPeriod), ">0", "finite");
                bool moonAllowed = planet.orbitalDistance > star.frostLine ?
                    moon.className == "Rocky Moon" || moon.className == "Icy Moon" :
                    moon.className == "Rocky Moon";
                Add(csv, seed, rep, moonId, "INV_FROST_WEIGHTS", moonAllowed,
                    moon.className + (planet.orbitalDistance > star.frostLine ? ";outside" : ";inside"),
                    planet.orbitalDistance > star.frostLine ? "Rocky Moon|Icy Moon" : "Rocky Moon", "");
            }
        }
    }

    private static bool Positive(float x) { return SysBenchmarkCsv.Finite(x) && x > 0f; }
    private static bool ValidBody(CelestialBodyData x)
    {
        return Positive(x.mass) && Positive(x.radius) && Positive(x.revolutionPeriod) &&
               Positive(x.rotationPeriod) && Positive(x.orbitalDistance) &&
               SysBenchmarkCsv.Finite(x.surfaceTemperature) &&
               SysBenchmarkCsv.Finite(x.surfaceGravity);
    }

    private static string Digest(StarSystemGenerator generator)
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream, Encoding.UTF8))
        {
            Serialize(writer, generator.SystemName);
            Serialize(writer, generator.SystemStar);
            Serialize(writer, generator.SystemPlanets);
            Serialize(writer, generator.SystemBelts);
            Serialize(writer, generator.TotalPlanets);
            Serialize(writer, generator.TotalMoons);
            Serialize(writer, generator.TotalRings);
            writer.Flush();
            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(stream.ToArray());
                return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            }
        }
    }

    private static void Serialize(BinaryWriter writer, object value)
    {
        if (value == null) { writer.Write((byte)0); return; }
        writer.Write((byte)1);
        if (value is string) { writer.Write((string)value); return; }
        if (value is int) { writer.Write((int)value); return; }
        if (value is float) { writer.Write((float)value); return; }
        if (value is double) { writer.Write((double)value); return; }
        if (value is bool) { writer.Write((bool)value); return; }
        if (value is Color)
        {
            Color c = (Color)value;
            writer.Write(c.r); writer.Write(c.g); writer.Write(c.b); writer.Write(c.a);
            return;
        }
        if (value is IList)
        {
            var list = (IList)value;
            writer.Write(list.Count);
            foreach (object item in list) Serialize(writer, item);
            return;
        }
        FieldInfo[] fields = value.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
        Array.Sort(fields, (a, b) => string.CompareOrdinal(a.Name, b.Name));
        writer.Write(fields.Length);
        foreach (FieldInfo f in fields)
        {
            writer.Write(f.Name);
            Serialize(writer, f.GetValue(value));
        }
    }
}
